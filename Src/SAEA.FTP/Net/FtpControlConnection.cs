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
*修改时间：2026/11/11 17:52:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：数据连接改走处理器链与 FtpDataConnector，OpenPassive 返回 FtpDataConnection
*
*****************************************************************************/
using SAEA.Common;
using SAEA.FTP.Model;
using SAEA.Sockets;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;
using System;
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

        readonly ChannelPipelineOptions _options;

        readonly ChannelPipelineOptions _dataOptions = new ChannelPipelineOptions
        {
            // 数据通道无消息边界：一次读取就是一块数据，缓冲下限必须覆盖单次读取。
            Splitter = new TakeAvailableSplitter(64 * 1024),
            MaxBufferedBytes = 64 * 1024 + FrameConstants.P_Head,
            ReadBufferSize = 64 * 1024,
            // 传输是长任务，不设单消息超时；队列满时由解码器阻塞形成背压。
            MessageTimeout = TimeSpan.Zero,
            MaxConcurrentMessages = 1,
            QueueCapacity = 8,
            // 泵读到 EOF 会丢弃队列帧，同步入队才能保住同批尾帧。
            DispatchMode = ChannelDispatchMode.Inline
        };

        readonly PipelineHostRegistry _host = new PipelineHostRegistry();

        readonly Action<string, IChannelPipeline> _configurePipeline;

        readonly object _syncRoot = new object();

        IClientSocket _client;

        FtpControlReplyHandler _replies;

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
                MaxConcurrentMessages = 1
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

                try
                {
                    // 与旧实现一致：写出后立刻阻塞等对应应答，同一时刻只有一条命令在途。
                    pipeline.WriteAsync(command).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
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
        /// 从有界队列取一条应答；解析失败或连接收口时抛 <see cref="IOException"/>
        /// </summary>
        public FtpReply ReadReply()
        {
            var replies = _replies;

            if (replies == null)
            {
                throw new IOException("FTP control connection is closed");
            }

            object item;

            if (!replies.TryTake(_commandTimeout, out item))
            {
                throw new IOException("FTP control connection read timeout: " + _config.IP);
            }

            var fault = item as FtpReplyFault;
            if (fault != null)
            {
                throw new IOException(fault.Error.Message, fault.Error);
            }

            return (FtpReply)item;
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