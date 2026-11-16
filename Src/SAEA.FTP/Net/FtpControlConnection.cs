/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.FTP.Net
*文件名： FtpControlConnection
*版本号： v26.11.5.1
*唯一标识：9811ff44-df7f-4dc1-9d1f-ffa709309264
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/5 11:37:27
*描述：FTP客户端控制连接（同步、支持多行响应、EPSV/PASV、REST）
*
*=====================================================================
*修改标记
*修改时间：2026/11/16 10:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：控制与数据通道配置显式开启启动期链尾校验
*
*****************************************************************************/
using SAEA.Common;
using SAEA.FTP.Model;
using SAEA.Sockets;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.FTP.Net
{
    /// <summary>
    /// FTP 响应
    /// </summary>
    public sealed class FtpReply
    {
        /// <summary>
        /// 三位响应码
        /// </summary>
        public int Code { get; set; }

        /// <summary>
        /// 多行响应时，各行以 \n 拼接
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 未经解析的原始响应文本，多行以 \n 连接
        /// </summary>
        public string Raw { get; set; } = string.Empty;

        /// <summary>
        /// 是否为 2xx 成功响应
        /// </summary>
        public bool IsPositive
        {
            get { return Code >= 200 && Code < 300; }
        }

        /// <summary>
        /// 返回「状态码 + 空格 + 响应文本」
        /// </summary>
        /// <returns>便于日志输出的响应描述</returns>
        public override string ToString()
        {
            return Code + " " + Message;
        }
    }

    /// <summary>
    /// FTP 命令失败
    /// </summary>
    public class FTPCommandException : IOException
    {
        /// <summary>
        /// 服务器返回的响应码
        /// </summary>
        public int Code { get; private set; }

        /// <summary>
        /// 服务器返回的响应文本
        /// </summary>
        public string Reply { get; private set; }

        /// <summary>
        /// 以响应码与响应文本构造异常
        /// </summary>
        /// <param name="code">服务器返回的响应码</param>
        /// <param name="reply">服务器返回的响应文本</param>
        public FTPCommandException(int code, string reply)
            : base("code:" + code + ",reply:" + reply)
        {
            Code = code;
            Reply = reply;
        }
    }

    /// <summary>
    /// 本地文件/磁盘错误，不参与网络重试
    /// </summary>
    public class FTPLocalException : IOException
    {
        /// <summary>
        /// 以错误描述与内部异常构造本地错误
        /// </summary>
        /// <param name="message">错误描述</param>
        /// <param name="inner">内部异常</param>
        public FTPLocalException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    /// <summary>
    /// 单条 FTP 控制连接。每条并行分片使用独立实例。
    /// </summary>
    internal sealed class FtpControlConnection : IDisposable
    {
        readonly ClientConfig _config;

        readonly Encoding _encoding;

        /// <summary>
        /// 连续丢弃应答的上限，达到即判定控制通道失步
        /// </summary>
        const int MaxConsecutiveStaleReplies = 3;

        readonly ChannelPipelineOptions _options;

        readonly ChannelPipelineOptions _dataOptions = new ChannelPipelineOptions
        {
            // 服务端 FTPServer.cs 的 CreateDataOptions 是同一组参数的另一份拷贝，只改一边会导致两端分帧与背压行为不一致。
            // 数据通道无消息边界：一次读取就是一块数据，缓冲下限必须覆盖单次读取。
            Splitter = new TakeAvailableSplitter(64 * 1024),
            MaxBufferedBytes = 64 * 1024 + FrameConstants.P_Head,
            ReadBufferSize = 64 * 1024,
            // 传输是长任务，不设单消息超时；队列满时由解码器阻塞形成背压。
            MessageTimeout = TimeSpan.Zero,
            MaxConcurrentMessages = 1,
            QueueCapacity = 8,
            // 泵读到 EOF 会丢弃队列帧，同步入队才能保住同批尾帧。
            DispatchMode = ChannelDispatchMode.Inline,
            RequireTerminalHandler = true
        };

        readonly PipelineHostRegistry _host = new PipelineHostRegistry();

        readonly Action<string, IChannelPipeline> _configurePipeline;

        readonly object _syncRoot = new object();

        /// <summary>
        /// 传输类命令：先收 125/150 打开数据连接，数据通道结束后再收一条完成码
        /// </summary>
        static readonly HashSet<string> TransferCommands = new HashSet<string>(StringComparer.Ordinal)
        {
            "RETR",
            "STOR",
            "APPE",
            "LIST",
            "NLST",
            "MLSD"
        };

        /// <summary>
        /// 非传输命令的终态码表。
        /// <para>
        /// 表内按 RFC 959/3659 的规范码列举，另含少数服务端常用的替代码：CWD 收 250 或 200、
        /// MKD 收 250 或 257、RNFR 收 350 或 250。未登记的命令不做严格匹配，走 null 兜底值只认
        /// 2xx/3xx，属安全降级。
        /// </para>
        /// <para>
        /// 表外码会被判为不匹配并丢弃，连续 3 条即收口。这是「不按 RFC 回码的服务端会先丢应答
        /// 再断连」的取舍，登记时宁可放宽到调用方也接受的码，也不要让正常应答落进丢弃路径。
        /// </para>
        /// </summary>
        static readonly Dictionary<string, int[]> TerminalCodes = new Dictionary<string, int[]>(StringComparer.Ordinal)
        {
            { "USER", new[] { ServerResponseCode.登录成功, ServerResponseCode.要求密码, ServerResponseCode.要求帐号 } },
            { "PASS", new[] { ServerResponseCode.初始命令没有执行, ServerResponseCode.登录成功, ServerResponseCode.要求帐号 } },
            { "FEAT", new[] { ServerResponseCode.系统状态回复 } },
            { "OPTS", new[] { ServerResponseCode.成功 } },
            { "TYPE", new[] { ServerResponseCode.成功 } },
            { "MODE", new[] { ServerResponseCode.成功 } },
            { "STRU", new[] { ServerResponseCode.成功 } },
            { "PWD", new[] { ServerResponseCode.路径名建立 } },
            { "CWD", new[] { ServerResponseCode.文件行为完成, ServerResponseCode.成功 } },
            { "CDUP", new[] { ServerResponseCode.成功, ServerResponseCode.文件行为完成 } },
            { "MKD", new[] { ServerResponseCode.文件行为完成, ServerResponseCode.路径名建立 } },
            { "RMD", new[] { ServerResponseCode.文件行为完成 } },
            { "DELE", new[] { ServerResponseCode.文件行为完成 } },
            { "RNFR", new[] { ServerResponseCode.文件行为暂停, ServerResponseCode.文件行为完成 } },
            { "RNTO", new[] { ServerResponseCode.文件行为完成 } },
            { "REST", new[] { ServerResponseCode.文件行为暂停 } },
            { "SIZE", new[] { ServerResponseCode.文件状态回复 } },
            { "MDTM", new[] { ServerResponseCode.文件状态回复 } },
            { "PASV", new[] { ServerResponseCode.进入被动模式 } },
            { "EPSV", new[] { ServerResponseCode.进入扩展被动模式 } },
            { "NOOP", new[] { ServerResponseCode.成功 } },
            { "QUIT", new[] { ServerResponseCode.退出网络 } },
            { "SYST", new[] { ServerResponseCode.系统类型回复 } },
            { "STAT", new[] { ServerResponseCode.系统状态回复, ServerResponseCode.目录状态回复, ServerResponseCode.文件状态回复 } },
            { "ABOR", new[] { ServerResponseCode.打开数据连接, ServerResponseCode.结束数据连接 } }
        };

        IClientSocket _client;

        FtpControlReplyHandler _replies;

        /// <summary>
        /// 在途命令名，取命令行首个空格前的 token 并转大写
        /// </summary>
        string _pendingCommand;

        /// <summary>
        /// 在途命令是否属于传输类
        /// </summary>
        bool _pendingTransfer;

        /// <summary>
        /// 传输命令是否已收到 125/150、正在等完成码
        /// </summary>
        bool _awaitingCompletion;

        /// <summary>
        /// 是否有在途命令的应答尚未取走
        /// </summary>
        bool _hasPending;

        /// <summary>
        /// 当前阶段允许的终态码；null 表示没有可依的码表——传输命令的开启与完成两个阶段、
        /// 以及未登记的非传输命令都取 null，具体规则由 <see cref="MatchCode"/> 按阶段判定
        /// </summary>
        int[] _pendingTerminal;

        /// <summary>
        /// 尚未被读走的迟到传输完成应答数量
        /// </summary>
        int _owedReplies;

        /// <summary>
        /// 累计丢弃的迟到或不匹配应答条数
        /// </summary>
        int _staleReplyCount;

        /// <summary>
        /// 本次读取中连续丢弃的应答条数，命中一条即清零
        /// </summary>
        int _discardStreak;

        /// <summary>
        /// 超时命令遗留的终态码表快照，仅在 <see cref="_hasStaleExpectation"/> 为真时有效
        /// </summary>
        int[] _staleTerminal;

        /// <summary>
        /// 超时命令是否属于传输类
        /// </summary>
        bool _staleTransfer;

        /// <summary>
        /// 超时命令当时是否已进入传输完成阶段
        /// </summary>
        bool _staleAwaitingCompletion;

        /// <summary>
        /// 是否还有一条超时命令的迟到应答待丢弃
        /// </summary>
        bool _hasStaleExpectation;

        /// <summary>
        /// 读取时加锁，供用例与诊断使用
        /// </summary>
        internal int StaleReplyCount
        {
            get
            {
                lock (_syncRoot)
                {
                    return _staleReplyCount;
                }
            }
        }

        public FtpReply Greeting { get; private set; }

        public bool SupportsRest { get; private set; }

        public bool SupportsMdtm { get; private set; }

        public bool SupportsMlsd { get; private set; }

        public bool SupportsEpsv { get; private set; }

        public bool SupportsSize { get; private set; }

        public string OverwritePolicyHint { get; private set; }

        public string ControlHost
        {
            get
            {
                try
                {
                    var socket = _client == null ? null : _client.Socket;
                    var ep = socket == null ? null : socket.RemoteEndPoint as IPEndPoint;
                    return ep != null ? ep.Address.ToString() : _config.IP;
                }
                catch
                {
                    return _config.IP;
                }
            }
        }

        public bool Connected
        {
            get { return _client != null && _client.Connected; }
        }

        public ClientConfig Config
        {
            get { return _config; }
        }

        /// <summary>
        /// 数据连接读写超时
        /// </summary>
        public TimeSpan DataTimeout { get; set; }

        TimeSpan _commandTimeout;

        /// <summary>
        /// 控制命令响应超时，仅作为等待应答队列的上限
        /// </summary>
        public TimeSpan CommandTimeout
        {
            get { return _commandTimeout; }
            set { _commandTimeout = value; }
        }

        public FtpControlConnection(ClientConfig config, Action<string, IChannelPipeline> configurePipeline)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _config = config;
            _encoding = config.Encoding ?? Encoding.UTF8;
            _commandTimeout = config.CommandTimeout > TimeSpan.Zero ? config.CommandTimeout : TimeSpan.FromSeconds(30);
            DataTimeout = config.DataTimeout > TimeSpan.Zero ? config.DataTimeout : TimeSpan.FromSeconds(60);
            _configurePipeline = configurePipeline;

            _options = new ChannelPipelineOptions
            {
                Splitter = new LineSplitter(8 * 1024),
                MaxBufferedBytes = 8 * 1024 + FrameConstants.P_Head,
                ReadBufferSize = 64 * 1024,
                // 控制通道严格一问一答，应答必须按到达顺序入队。
                MaxConcurrentMessages = 1,
                RequireTerminalHandler = true
            };
        }

        #region 连接与登录

        public void Connect()
        {
            Close();

            var timeout = _config.ConnectTimeout;
            if (timeout <= TimeSpan.Zero) timeout = TimeSpan.FromSeconds(30);

            // 应答收集器随连接一起创建：旧连接收口时会对旧实例置关闭，新连接不受影响。
            var replies = new FtpControlReplyHandler();
            _replies = replies;

            // 新连接从零开始配对应答，旧连接遗留的债务与丢弃计数不能带过来。
            ResetReplyState();

            var option = new SocketOptionBuilder()
                .SetSocket(SAEASocketType.Tcp)
                .UseStream()
                .UseConnectionOpened(OnConnectionOpened)
                .UseInputHandler((userToken, reader, flow, token) => RunPipelineAsync(replies, userToken, reader, flow, token))
                .SetIP(_config.IP)
                .SetPort(_config.Port)
                .SetReadBufferSize(64 * 1024)
                .SetWriteBufferSize(64 * 1024)
                .SetConnectTimeout((int)Math.Max(1, timeout.TotalMilliseconds))
                .Build();

            var client = SocketFactory.CreateClientSocket(option);

            client.OnError += (id, ex) => LogHelper.Debug("FTP控制连接传输层异常:" + id + "," + ex.Message);

            _client = client;

            try
            {
                client.Connect();

                if (!replies.WaitReady(timeout))
                {
                    throw new IOException("FTP control connection pipeline not ready: " + _config.IP + ":" + _config.Port);
                }
            }
            catch
            {
                Close();
                throw;
            }

            Greeting = ReadReply();

            if (Greeting.Code != ServerResponseCode.服务就绪)
            {
                Close();
                throw new FTPCommandException(Greeting.Code, Greeting.Message);
            }

            try
            {
                Login();

                Feat();

                try
                {
                    Send("OPTS UTF8 ON");
                    // 忽略不支持 UTF8 的服务器
                }
                catch
                {
                }

                try
                {
                    var typeReply = Send("TYPE I");
                    if (typeReply.Code != ServerResponseCode.成功)
                    {
                        // 少数服务器拒绝 TYPE I，继续按字节传输
                    }
                }
                catch
                {
                }
            }
            catch
            {
                Close();
                throw;
            }
        }

        void Login()
        {
            var reply = Send(FTPCommand.USER + " " + _config.UserName);

            if (reply.Code != ServerResponseCode.登录成功 && reply.Code != ServerResponseCode.要求密码)
            {
                throw new FTPCommandException(reply.Code, reply.Message);
            }

            if (reply.Code == ServerResponseCode.要求密码)
            {
                reply = Send(FTPCommand.PASS + " " + _config.Password);

                if (reply.Code != ServerResponseCode.登录成功 && reply.Code != ServerResponseCode.初始命令没有执行)
                {
                    throw new FTPCommandException(reply.Code, reply.Message);
                }
            }
        }

        void Feat()
        {
            try
            {
                var reply = Send(FTPCommand.FEAT.ToString());

                if (reply.Code != ServerResponseCode.系统状态回复) return;

                var text = reply.Message ?? string.Empty;

                SupportsRest = Contains(text, "REST STREAM");
                SupportsSize = Contains(text, " SIZE") || text.StartsWith("SIZE", StringComparison.OrdinalIgnoreCase);
                SupportsMdtm = Contains(text, "MDTM");
                SupportsMlsd = Contains(text, "MLSD");
                SupportsEpsv = Contains(text, "EPSV");

                var idx = text.IndexOf("SAEA-OVERWRITE=", StringComparison.OrdinalIgnoreCase);
                if (idx > -1)
                {
                    var start = idx + "SAEA-OVERWRITE=".Length;
                    var end = text.IndexOfAny(new[] { '\r', '\n', ' ' }, start);
                    OverwritePolicyHint = end > start ? text.Substring(start, end - start) : text.Substring(start);
                }

                // FEAT 不可靠时按保守值处理
                if (!SupportsRest && !Contains(text, "REST"))
                {
                    SupportsRest = false;
                }
            }
            catch
            {
            }
        }

        static bool Contains(string text, string value)
        {
            return text != null && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) > -1;
        }

        #endregion

        #region 命令与响应

        public FtpReply Send(string command)
        {
            lock (_syncRoot)
            {
                if (!Connected)
                {
                    throw new IOException("FTP control connection is closed");
                }

                var pipeline = _replies == null ? null : _replies.Pipeline;
                if (pipeline == null)
                {
                    throw new IOException("FTP control connection is closed");
                }

                // 登记必须早于写出：应答可能先于写完成进入队列，晚登记会把应答错配给上一条命令。
                BeginCommand(command);

                try
                {
                    // 与旧实现一致：写出后立刻阻塞等对应应答，同一时刻只有一条命令在途。
                    pipeline.WriteAsync(command).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    // 写出失败说明命令没有真正发出，登记随之作废，后续应答不会错配给它。
                    ClearPending();
                    throw new IOException("FTP command send failed: " + command, ex);
                }

                return ReadReply();
            }
        }

        public FtpReply SendExpect(string command, params int[] expected)
        {
            var reply = Send(command);

            if (expected == null || expected.Length == 0) return reply;

            foreach (var code in expected)
            {
                if (reply.Code == code) return reply;
            }

            throw new FTPCommandException(reply.Code, reply.Message);
        }

        /// <summary>
        /// 从有界队列取一条与本轮在途命令匹配的应答；解析失败或连接收口时抛 <see cref="IOException"/>
        /// </summary>
        /// <returns>匹配当前在途命令的应答；无在途命令时返回队列里的第一条应答</returns>
        public FtpReply ReadReply()
        {
            var replies = _replies;

            if (replies == null)
            {
                throw new IOException("FTP control connection is closed");
            }

            while (true)
            {
                object item;

                // 阻塞取值不持锁：写方与收口方需要同一把锁更新状态。
                if (!replies.TryTake(_commandTimeout, out item))
                {
                    lock (_syncRoot)
                    {
                        // 控制连接应答有序，被放弃命令的应答一定先于下一条命令的应答到达，
                        // 所以只丢弃紧邻的一条，记过快照即可覆盖整个窗口。
                        if (_hasPending && !(_pendingTransfer && _awaitingCompletion))
                        {
                            // 传输完成阶段的应答已在 BeginCommand 记进债务，这里重复记录会多丢一条。
                            _staleTerminal = _pendingTerminal;
                            _staleTransfer = _pendingTransfer;
                            _staleAwaitingCompletion = _awaitingCompletion;
                            _hasStaleExpectation = true;
                        }
                    }

                    throw new IOException("FTP control connection read timeout: " + _config.IP);
                }

                var fault = item as FtpReplyFault;
                if (fault != null)
                {
                    throw new IOException(fault.Error.Message, fault.Error);
                }

                var reply = (FtpReply)item;

                // 421 表示服务端主动下线，任何阶段都不能当成普通应答交给调用方。
                if (reply.Code == ServerResponseCode.服务关闭)
                {
                    throw CloseWithError("FTP control connection closed by server: " + reply);
                }

                bool terminal;

                lock (_syncRoot)
                {
                    // 迟到应答必然先到，早于本轮债务与本轮应答，故先消化超时命令的期望。
                    if (_hasStaleExpectation)
                    {
                        bool staleTerminal;

                        if (MatchCode(reply.Code, _staleTransfer, _staleAwaitingCompletion, _staleTerminal, out staleTerminal))
                        {
                            _hasStaleExpectation = false;
                            _staleTerminal = null;
                            _staleReplyCount++;

                            LogHelper.Debug("FTP控制连接丢弃超时命令的迟到应答:" + reply);

                            continue;
                        }

                        // 有序窗口已过：本条不是被放弃命令的应答，既不能丢也不能计入丢弃次数。
                        _hasStaleExpectation = false;
                        _staleTerminal = null;
                    }

                    // 债务只吃完成码：3xx 是中间应答，不属于迟到的传输完成。
                    if (_owedReplies > 0 && IsCompletionCode(reply.Code))
                    {
                        _owedReplies--;
                        _staleReplyCount++;

                        LogHelper.Debug("FTP控制连接丢弃迟到的传输完成应答:" + reply);

                        continue;
                    }

                    // 无在途命令（如连接问候）不参与配对，原样返回。
                    if (!_hasPending)
                    {
                        return reply;
                    }

                    if (TryMatch(reply.Code, out terminal))
                    {
                        // 本命令的应答已经对上，说明迟到的完成码不会再来，剩余债务作废，
                        // 否则会吃掉后续命令自己的完成应答。
                        _owedReplies = 0;
                        _discardStreak = 0;

                        if (terminal)
                        {
                            ClearPending();
                        }
                        else
                        {
                            _awaitingCompletion = true;
                        }

                        return reply;
                    }

                    _staleReplyCount++;
                    _discardStreak++;

                    LogHelper.Debug("FTP控制连接丢弃不匹配应答:" + _pendingCommand + "," + reply);

                    if (_discardStreak >= MaxConsecutiveStaleReplies)
                    {
                        throw CloseWithError("FTP control connection out of sync: unexpected reply " + reply + " while waiting for " + _pendingCommand);
                    }
                }
            }
        }

        /// <summary>
        /// 写出前登记在途命令与其允许的应答码族
        /// </summary>
        /// <param name="command">完整命令行</param>
        void BeginCommand(string command)
        {
            // 上一条传输命令的完成码没被读走，说明调用方放弃了那条应答；
            // 它一定会先于本轮应答到达，记一笔债务在读取时先丢弃。
            // 已知边界：债务深度只记 1，中断的传输常成对发出 426+226，尾随那条会落到
            // 未登记命令上（走 null 兜底接受任意 2xx），代价是下一条命令可能错位一条应答。
            if (_hasPending && _pendingTransfer && _awaitingCompletion)
            {
                _owedReplies++;
            }

            _discardStreak = 0;

            var name = CommandName(command);

            _pendingCommand = name;
            _pendingTransfer = TransferCommands.Contains(name);
            _awaitingCompletion = false;
            _pendingTerminal = _pendingTransfer ? null : LookupTerminalCodes(name);
            _hasPending = true;
        }

        /// <summary>
        /// 清空在途命令登记，应答已经交给调用方或命令没有发出时调用
        /// </summary>
        void ClearPending()
        {
            _pendingCommand = null;
            _pendingTransfer = false;
            _awaitingCompletion = false;
            _pendingTerminal = null;
            _hasPending = false;
            _discardStreak = 0;
            _staleTerminal = null;
            _staleTransfer = false;
            _staleAwaitingCompletion = false;
            _hasStaleExpectation = false;
        }

        /// <summary>
        /// 新连接建立时重置应答配对状态与计数
        /// </summary>
        void ResetReplyState()
        {
            lock (_syncRoot)
            {
                ClearPending();
                _owedReplies = 0;
                _staleReplyCount = 0;
            }
        }

        /// <summary>
        /// 判断应答码是否属于当前在途命令的允许集合
        /// </summary>
        /// <param name="code">应答码</param>
        /// <param name="terminal">命中且为本轮终态时为 true；传输命令的 125/150 为 false</param>
        /// <returns>属于当前命令时返回 true</returns>
        bool TryMatch(int code, out bool terminal)
        {
            return MatchCode(code, _pendingTransfer, _awaitingCompletion, _pendingTerminal, out terminal);
        }

        /// <summary>
        /// 按「是否传输命令 + 所处阶段 + 码表」判断应答码是否命中期望，超时留下的快照与在途命令共用同一套规则
        /// </summary>
        /// <param name="code">应答码</param>
        /// <param name="transfer">该命令是否属于传输类</param>
        /// <param name="awaitingCompletion">传输命令是否已收到 125/150、正在等完成码</param>
        /// <param name="terminal">期望的终态码表；null 表示没有可依的码表</param>
        /// <param name="isTerminal">命中且为终态时为 true；传输命令的 125/150 为 false</param>
        /// <returns>命中期望时返回 true</returns>
        static bool MatchCode(int code, bool transfer, bool awaitingCompletion, int[] terminal, out bool isTerminal)
        {
            isTerminal = false;

            // 4xx/5xx 一律作为当前命令的失败结果返回，不在码表里逐条列举。
            // 已知窗口：债务为 0 时（上一次完成应答没来或已复位），迟到的 4xx/5xx 会被当作
            // 下一条命令的失败结果；影响面是下一条命令错一条应答，随后由丢弃计数自愈，连续 3 条则收口。
            if (code >= 400 && code < 600)
            {
                isTerminal = true;
                return true;
            }

            if (transfer && !awaitingCompletion)
            {
                if (code == ServerResponseCode.打开数据连接开始传输 || code == ServerResponseCode.打开连接)
                {
                    return true;
                }

                return false;
            }

            if (transfer)
            {
                // 完成阶段各服务端用 226/250/200 不等，按整族 2xx 接受。
                if (code >= 200 && code < 300)
                {
                    isTerminal = true;
                    return true;
                }

                return false;
            }

            var allowed = terminal;

            if (allowed == null)
            {
                // 未登记的命令没有码表可依，只要求是终态码，避免把正常应答误判成失步。
                if (code >= 200 && code < 400)
                {
                    isTerminal = true;
                    return true;
                }

                return false;
            }

            for (var i = 0; i < allowed.Length; i++)
            {
                if (allowed[i] == code)
                {
                    isTerminal = true;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 取命令名：命令行首个空格前的 token，统一转大写
        /// </summary>
        /// <param name="command">完整命令行</param>
        /// <returns>命令名；空命令返回空串</returns>
        static string CommandName(string command)
        {
            if (string.IsNullOrEmpty(command)) return string.Empty;

            var index = command.IndexOf(' ');

            var name = index < 0 ? command : command.Substring(0, index);

            return name.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// 取命令的终态码表
        /// </summary>
        /// <param name="command">命令名</param>
        /// <returns>允许的终态码；未登记的命令返回 null</returns>
        static int[] LookupTerminalCodes(string command)
        {
            int[] codes;

            return TerminalCodes.TryGetValue(command, out codes) ? codes : null;
        }

        /// <summary>
        /// 判断应答码是否属于传输完成码
        /// </summary>
        /// <param name="code">应答码</param>
        /// <returns>2xx/4xx/5xx 返回 true</returns>
        static bool IsCompletionCode(int code)
        {
            // 3xx 是中间应答（如 350 等待后续命令），任何传输都不会用它收尾，不能算进债务。
            return (code >= 200 && code < 300) || (code >= 400 && code < 600);
        }

        /// <summary>
        /// 收口当前连接并生成要抛出的错误
        /// </summary>
        /// <param name="message">错误描述</param>
        /// <returns>已绑定关闭原因的异常，由调用方直接抛出</returns>
        IOException CloseWithError(string message)
        {
            var error = new IOException(message);

            var replies = _replies;

            // 先落关闭原因再收口：Close 内的 MarkClosed(null) 不会覆盖已登记的异常。
            if (replies != null)
            {
                replies.MarkClosed(error);
            }

            Close();

            return error;
        }

        #endregion

        #region 处理器链

        /// <summary>
        /// 连接门面登记钩子：处理器链只依赖连接标识，这里仅满足「输入处理器必须与连接钩子成对配置」的约定
        /// </summary>
        /// <param name="info">底层通道信息</param>
        static void OnConnectionOpened(ChannelInfo info)
        {
        }

        /// <summary>
        /// 接管本连接的读循环：装配处理器链并运行到收口
        /// </summary>
        /// <param name="replies">本次连接专属的应答收集器</param>
        /// <param name="userToken">连接令牌，用于取连接标识</param>
        /// <param name="reader">接收方向的 PipeReader</param>
        /// <param name="flow">应用级流量控制闸门</param>
        /// <param name="token">连接级取消令牌</param>
        /// <returns>执行器收口完成后结束的任务</returns>
        async Task RunPipelineAsync(
            FtpControlReplyHandler replies,
            IUserToken userToken,
            PipeReader reader,
            IReceiveFlowControl flow,
            CancellationToken token)
        {
            var channelId = userToken == null ? null : userToken.ID;

            if (string.IsNullOrEmpty(channelId))
                return;

            var client = _client;

            if (client == null)
                return;

            var stream = new PipelineRawByteStream(
                channelId,
                reader,
                flow,
                (data, owner) => client.SendAsync(data, owner));

            await PipelineWiring.RunAsync(
                channelId,
                stream,
                flow,
                _options,
                token,
                _host,
                (id, pipeline) => ConfigureConnection(replies, id, pipeline))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 装配本连接的处理器链：出站前缀 → 应答解码 → 应答入队 → 使用者扩展 → 链尾异常处理器
        /// </summary>
        /// <param name="replies">本次连接专属的应答收集器</param>
        /// <param name="channelId">连接标识</param>
        /// <param name="pipeline">尚未启动的处理器链</param>
        void ConfigureConnection(FtpControlReplyHandler replies, string channelId, IChannelPipeline pipeline)
        {
            // 出站必须是链首连续前缀，串行器固定在最右，命令写出才不会乱序。
            pipeline.AddLast(new FtpCommandEncoder(_encoding));
            pipeline.AddLast(new OutboundSerializingHandler());

            pipeline.AddLast(new FtpReplyDecoder(_encoding));
            pipeline.AddLast(replies);

            var configure = _configurePipeline;
            if (configure != null)
                configure(channelId, pipeline);

            pipeline.AddLast(new ExceptionTerminalHandler());
        }

        #endregion

        #region 数据连接

        /// <summary>
        /// 打开被动模式数据连接，优先 EPSV，失败回退 PASV。
        /// <para>
        /// 返回的是链已启动、可直接读写的数据连接门面；调用方用完必须调用 <see cref="FtpDataConnection.Close"/> 收口。
        /// </para>
        /// </summary>
        /// <param name="timeout">建立连接并等待链启动的上限，小于等于 0 时按 60 秒处理</param>
        /// <returns>已启动数据链的连接门面</returns>
        public FtpDataConnection OpenPassive(TimeSpan timeout)
        {
            if (SupportedEpsv())
            {
                try
                {
                    var reply = Send(FTPCommand.EPSV.ToString());
                    if (reply.Code == ServerResponseCode.进入扩展被动模式)
                    {
                        return ConnectData(ControlHost, ParseEpsvPort(reply.Message), timeout);
                    }
                }
                catch (FTPCommandException)
                {
                    // 回退 PASV
                }
            }

            var pasv = Send(FTPCommand.PASV.ToString());

            if (pasv.Code != ServerResponseCode.进入被动模式)
            {
                throw new FTPCommandException(pasv.Code, pasv.Message);
            }

            string ip;
            int port;
            ParsePasv(pasv.Message, out ip, out port);

            if (string.IsNullOrEmpty(ip) || ip == "0.0.0.0")
            {
                ip = ControlHost;
            }

            return ConnectData(ip, port, timeout);
        }

        bool SupportedEpsv()
        {
            return SupportsEpsv;
        }

        static int ParseEpsvPort(string message)
        {
            var start = message.IndexOf('(');
            var end = message.IndexOf(')');

            if (start < 0 || end <= start)
            {
                throw new IOException("Malformed EPSV reply: " + message);
            }

            var inner = message.Substring(start + 1, end - start - 1);
            var parts = inner.Split('|');

            int port;
            if (parts.Length >= 4 && int.TryParse(parts[3], out port))
            {
                return port;
            }

            throw new IOException("Malformed EPSV reply: " + message);
        }

        static void ParsePasv(string message, out string ip, out int port)
        {
            ip = null;
            port = 0;

            var start = message.IndexOf('(');
            var end = message.IndexOf(')');

            if (start < 0 || end <= start)
            {
                throw new IOException("Malformed PASV reply: " + message);
            }

            var inner = message.Substring(start + 1, end - start - 1);
            var parts = inner.Split(',');

            if (parts.Length != 6)
            {
                throw new IOException("Malformed PASV reply: " + message);
            }

            ip = parts[0] + "." + parts[1] + "." + parts[2] + "." + parts[3];

            int high, low;
            if (!int.TryParse(parts[4], out high) || !int.TryParse(parts[5], out low))
            {
                throw new IOException("Malformed PASV reply: " + message);
            }

            port = (high << 8) + low;
        }

        /// <summary>
        /// 连到被动端口并返回数据连接门面；连接器只负责建链，交接后的收口由门面负责。
        /// </summary>
        /// <param name="host">数据端口主机</param>
        /// <param name="port">数据端口</param>
        /// <param name="timeout">连接与等待链启动的上限，小于等于 0 时按 60 秒处理</param>
        /// <returns>已启动数据链的连接门面</returns>
        FtpDataConnection ConnectData(string host, int port, TimeSpan timeout)
        {
            // 与旧 NetworkStream 一致：读写等待至少 1 秒，避免 0 被当成无限等待。
            var dataTimeout = (int)Math.Max(1000, DataTimeout.TotalMilliseconds);

            // 交接成功后 FtpDataConnector.Dispose 是空操作，客户端实例归 FtpDataConnection 所有，这里的 using 只负责失败路径。
            using (var connector = new FtpDataConnector(
                (id, ex) => LogHelper.Debug("FTP数据连接传输层异常:" + id + "," + ex.Message),
                _dataOptions))
            {
                return connector.Connect(host, port, timeout, dataTimeout, dataTimeout);
            }
        }

        #endregion

        public void Close()
        {
            var client = _client;
            _client = null;

            if (client != null)
            {
                try { client.Disconnect(); } catch { }
                try { client.Dispose(); } catch { }
            }

            var replies = _replies;
            _replies = null;

            // 唤醒还在等应答的线程，避免它挂到超时；队列关闭后迟到的应答直接丢弃。
            if (replies != null)
            {
                try { replies.MarkClosed(null); } catch { }
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}