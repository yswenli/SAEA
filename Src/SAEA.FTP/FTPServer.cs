/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \  ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               
  
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.FTP
*文件名： FTPServer
*版本号： v26.4.23.1
*唯一标识：87bf9252-2073-4145-813e-efe265ad75bb
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/09/27 17:50:27
*描述：FTPServer服务端类
*
*=====================================================================
*修改标记
*修改时间：2026/11/16 10:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：控制与数据通道配置改为每连接新建，并显式开启启动期链尾校验
*
*****************************************************************************/
using SAEA.Common;
using SAEA.FTP.Core;
using SAEA.FTP.Model;
using SAEA.FTP.Net;
using SAEA.Sockets;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.FTP
{
    /// <summary>
    /// 轻量 FTP 服务端，支持被动模式数据连接、断点续传与同名覆盖策略
    /// </summary>
    public class FTPServer : IDisposable
    {
        readonly ServerConfig _serverConfig;

        readonly ConcurrentDictionary<string, ServerSession> _sessions = new ConcurrentDictionary<string, ServerSession>();

        readonly ConcurrentDictionary<string, int> _userSessionCount = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        readonly PipelineHostRegistry _host = new PipelineHostRegistry();

        IServerSocket _server;

        readonly object _configurePipelineLock = new object();

        Action<string, IChannelPipeline> _configurePipeline;

        bool _configurePipelineFrozen;

        bool _running;

        /// <summary>
        /// 处理器链装配回调：每条连接在解码之后、异常收口之前调用一次，
        /// 参数为连接标识与尚未启动的链，使用者在此追加自己的入站处理器；
        /// 为 null 时只保留内置处理器。必须在 Start 之前赋值，Start 之后再赋值会抛 InvalidOperationException。
        /// </summary>
        public Action<string, IChannelPipeline> ConfigurePipeline
        {
            get
            {
                lock (_configurePipelineLock)
                {
                    return _configurePipeline;
                }
            }
            set
            {
                lock (_configurePipelineLock)
                {
                    if (_configurePipelineFrozen)
                        throw new InvalidOperationException("处理器链装配回调只能在 Start 之前赋值，服务端启动后已冻结。");

                    _configurePipeline = value;
                }
            }
        }

        /// <summary>
        /// 冻结装配回调，由 Start 调用
        /// </summary>
        void FreezeConfigurePipeline()
        {
            lock (_configurePipelineLock)
            {
                _configurePipelineFrozen = true;
            }
        }

        /// <summary>
        /// 是否正在监听客户端连接
        /// </summary>
        public bool Running
        {
            get { return _running; }
        }

        /// <summary>
        /// 服务端日志事件，参数为消息与异常，无异常时为 null
        /// </summary>
        public event Action<string, Exception> OnLog;

        /// <summary>
        /// 基于全局配置创建服务端，参数非空/非 0 时覆盖对应配置并立即保存
        /// </summary>
        /// <param name="ip">监听地址，为空时沿用配置文件中的值</param>
        /// <param name="port">监听端口，0 表示沿用配置文件中的值</param>
        /// <param name="bufferSize">读写缓冲区大小，0 表示沿用配置文件中的值</param>
        public FTPServer(string ip, ushort port = 21, int bufferSize = 0)
        {
            _serverConfig = FTPServerConfigManager.Get();

            if (!string.IsNullOrEmpty(ip)) _serverConfig.IP = ip;

            if (port > 0) _serverConfig.Port = port;

            if (bufferSize > 0) _serverConfig.BufferSize = bufferSize;

            FTPServerConfigManager.Save();
        }

        /// <summary>
        /// 使用显式配置创建服务端（不读写全局配置文件）
        /// </summary>
        /// <param name="config">服务端配置</param>
        public FTPServer(ServerConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            _serverConfig = config;
        }

        /// <summary>
        /// 当前生效的服务端配置
        /// </summary>
        public ServerConfig Configuration
        {
            get { return _serverConfig; }
        }

        #region 生命周期

        /// <summary>
        /// 开始监听客户端连接，已在运行时直接返回
        /// </summary>
        public void Start()
        {
            if (_running) return;

            // 先冻结再监听：接受连接可能在 Start 返回前发生，冻结必须早于第一个连接建链。
            FreezeConfigurePipeline();

            IPAddress address = IPAddress.Any;

            IPAddress parsed;
            if (!string.IsNullOrEmpty(_serverConfig.IP) && IPAddress.TryParse(_serverConfig.IP, out parsed) && !parsed.Equals(IPAddress.Any))
            {
                address = parsed;
            }

            var option = new SocketOptionBuilder()
                .SetSocket(SAEASocketType.Tcp)
                .UseStream()
                .UseConnectionOpened(OnConnectionOpened)
                .UseInputHandler(RunPipelineAsync)
                .SetIP(address.Equals(IPAddress.Any) ? string.Empty : _serverConfig.IP)
                .SetPort(_serverConfig.Port)
                .SetReadBufferSize(64 * 1024)
                .SetWriteBufferSize(64 * 1024)
                .Build();

            _server = SocketFactory.CreateServerSocket(option);

            _server.OnError += (id, ex) => Log("FTP传输层异常:" + id, ex);

            _server.Start();

            _running = true;
        }

        /// <summary>
        /// 停止监听并关闭全部现存会话，未运行时直接返回
        /// </summary>
        public void Stop()
        {
            if (!_running) return;

            _running = false;

            try { if (_server != null) _server.Stop(); } catch { }

            foreach (var session in _sessions.Values)
            {
                session.Dispose();
            }

            _sessions.Clear();
        }

        /// <summary>
        /// 连接建立钩子：按连接标识建立会话并登记
        /// </summary>
        /// <param name="info">底层通道信息，含连接标识</param>
        void OnConnectionOpened(ChannelInfo info)
        {
            if (info == null || string.IsNullOrEmpty(info.ID)) return;

            // 旧实现的控制连接没有空闲超时；这里显式豁免，避免长传输或客户端停顿被会话清理断开。
            try { _server.SessionManager.SetNoTimeout(info.ID, true); } catch { }

            _sessions[info.ID] = new ServerSession(this, info.ID);
        }

        /// <summary>
        /// 创建一条控制连接的通道链配置：分帧器与配置都按连接新建，跨连接共享会让分帧状态互相干扰。
        /// </summary>
        /// <returns>尚未被任何链使用的独立配置实例</returns>
        ChannelPipelineOptions CreateOptions()
        {
            return new ChannelPipelineOptions
            {
                Splitter = new LineSplitter(8 * 1024),
                MaxBufferedBytes = 8 * 1024 + FrameConstants.P_Head,
                ReadBufferSize = 64 * 1024,
                // 控制命令严格一问一答，同一会话必须按到达顺序处理。
                MaxConcurrentMessages = 1,
                RequireTerminalHandler = true
            };
        }

        /// <summary>
        /// 创建一条数据连接的通道链配置：分帧器与配置都按连接新建，跨连接共享会让分帧状态互相干扰。
        /// </summary>
        /// <returns>尚未被任何链使用的独立配置实例</returns>
        internal ChannelPipelineOptions CreateDataOptions()
        {
            return new ChannelPipelineOptions
            {
                // 客户端 Net\FtpControlConnection.cs 内 _dataOptions 是同一组参数的拷贝，只改一边会导致两端分帧与背压行为不一致。
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
        }

        /// <summary>
        /// 接管一条连接的读循环：装配处理器链并运行到收口
        /// </summary>
        /// <param name="userToken">连接令牌，用于取连接标识</param>
        /// <param name="reader">接收方向的 PipeReader</param>
        /// <param name="flow">应用级流量控制闸门</param>
        /// <param name="token">连接级取消令牌</param>
        /// <returns>执行器收口完成后结束的任务</returns>
        async Task RunPipelineAsync(
            IUserToken userToken,
            PipeReader reader,
            IReceiveFlowControl flow,
            CancellationToken token)
        {
            var channelId = userToken == null ? null : userToken.ID;

            if (string.IsNullOrEmpty(channelId))
                return;

            var stream = new PipelineRawByteStream(channelId, reader, flow, _server);

            await PipelineWiring.RunAsync(
                channelId,
                stream,
                flow,
                CreateOptions(),
                token,
                _host,
                ConfigureConnection)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 装配单条控制连接的处理器链：出站前缀 → 命令解码 → 命令执行 → 使用者扩展 → 链尾异常处理器
        /// </summary>
        /// <param name="channelId">连接标识</param>
        /// <param name="pipeline">尚未启动的处理器链</param>
        void ConfigureConnection(string channelId, IChannelPipeline pipeline)
        {
            // 出站必须是链首连续前缀，串行器固定在最右，命令回复才不会乱序。
            pipeline.AddLast(new FtpReplyEncoder());
            pipeline.AddLast(new OutboundSerializingHandler());

            pipeline.AddLast(new FtpCommandDecoder());
            pipeline.AddLast(new FtpServerCommandHandler(this));

            var configure = ConfigurePipeline;
            if (configure != null)
                configure(channelId, pipeline);

            pipeline.AddLast(new ExceptionTerminalHandler());
        }

        /// <summary>
        /// 停止服务端并释放监听器，可重复调用
        /// </summary>
        public void Dispose()
        {
            Stop();

            try
            {
                if (_server != null) _server.Dispose();
            }
            catch { }

            _server = null;
        }

        #endregion

        #region 会话

        /// <summary>
        /// 按连接标识取会话；连接建立钩子已登记时返回实例，否则为 null
        /// </summary>
        /// <param name="channelId">连接标识</param>
        /// <returns>对应的会话实例</returns>
        internal ServerSession GetSession(string channelId)
        {
            if (string.IsNullOrEmpty(channelId)) return null;

            ServerSession session;
            _sessions.TryGetValue(channelId, out session);

            return session;
        }

        internal void RemoveSession(ServerSession session)
        {
            ServerSession removed;
            _sessions.TryRemove(session.Id, out removed);

            if (!string.IsNullOrEmpty(session.UserName) && session.Authenticated)
            {
                _userSessionCount.AddOrUpdate(session.UserName, 0, (k, v) => v > 0 ? v - 1 : 0);
            }

            session.Dispose();
        }

        internal bool TryRegisterLogin(string userName)
        {
            var max = _serverConfig.MaxSessionsPerUser;

            if (max <= 0) return true;

            var count = _userSessionCount.AddOrUpdate(userName, 1, (k, v) => v + 1);

            if (count > max)
            {
                _userSessionCount.AddOrUpdate(userName, 0, (k, v) => v > 0 ? v - 1 : 0);
                return false;
            }

            return true;
        }

        internal void Log(string message, Exception ex)
        {
            var handler = OnLog;
            if (handler != null)
            {
                try { handler(message, ex); } catch { }
            }
        }

        internal ServerConfig Config
        {
            get { return _serverConfig; }
        }

        #endregion

        #region 会话实现

        internal sealed class ServerSession : IDisposable
        {
            readonly FTPServer _server;

            // 链激活时写入，收口时清空；回复写出只依赖本字段。
            volatile IChannelPipeline _pipeline;

            FtpDataListener _passive;

            bool _disposed;

            bool _quit;

            public string Id { get; private set; }

            public string PendingUserName { get; set; }

            public string UserName { get; set; }

            public bool Authenticated { get; set; }

            public string RootPath { get; set; }

            public string CurrentFsPath { get; set; }

            public string CurrentFtpPath { get; set; } = "/";

            public long RestOffset { get; set; }

            public bool RestIssued { get; set; }

            public string RenameFrom { get; set; }

            /// <summary>
            /// 是否已收到 QUIT 或需要主动结束会话的命令
            /// </summary>
            public bool QuitRequested
            {
                get { return _quit; }
            }

            public string OverwritePolicyHint
            {
                get
                {
                    switch (_server.Config.OverwritePolicy)
                    {
                        case FTPOverwritePolicy.Reject: return "Reject";
                        case FTPOverwritePolicy.Rename: return "Rename";
                        default: return "Overwrite";
                    }
                }
            }

            public ServerSession(FTPServer server, string channelId)
            {
                _server = server;
                Id = channelId;
            }

            /// <summary>
            /// 绑定处理器链的写入口，由命令处理器在链激活时调用
            /// </summary>
            /// <param name="pipeline">已启动的处理器链</param>
            internal void AttachPipeline(IChannelPipeline pipeline)
            {
                _pipeline = pipeline;
            }

            /// <summary>
            /// 标记连接已收口，释放会话持有的被动监听器，之后的回复写出静默丢弃
            /// </summary>
            internal void MarkClosed()
            {
                _disposed = true;

                // 收口早于 Dispose 调用，被动监听器必须在这里释放，否则端口会一直被占用到进程退出。
                ClosePassive();

                _pipeline = null;
            }

            #region 回复写出

            internal void Reply(int code, string message)
            {
                Write(new FtpReply
                {
                    Code = code,
                    Message = message
                });
            }

            internal void ReplyRaw(string text)
            {
                Write(new FtpRawText(text));
            }

            void Write(object msg)
            {
                var pipeline = _pipeline;

                // 链未激活或已收口：命令处理可能晚于失活回调，静默丢弃而不是抛出。
                if (pipeline == null) return;

                try
                {
                    // 出站执行在调用线程上启动，不依赖接收泵，因此在入站处理里同步等待不会自死锁。
                    pipeline.WriteAsync(msg).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _server.Log("FTP回复写出失败", ex);
                }
            }

            #endregion

            #region 命令分发

            internal void Handle(ClientRequest request)
            {
                _server.Log("收到命令：" + (request == null ? string.Empty : request.Raw), null);

                if (request == null)
                {
                    Reply(ServerResponseCode.错误参数, "Invalid command.");
                    return;
                }

                var command = request.Cmd;

                FTPCommand commandType;
                if (!Enum.TryParse(command, true, out commandType))
                {
                    switch (command.ToUpperInvariant())
                    {
                        case "SIZE": commandType = FTPCommand.SIZE; break;
                        case "MDTM": commandType = FTPCommand.MDTM; break;
                        case "REST": commandType = FTPCommand.REST; break;
                        case "EPSV": commandType = FTPCommand.EPSV; break;
                        case "EPRT": commandType = FTPCommand.EPRT; break;
                        case "ABOR": commandType = FTPCommand.ABOR; break;
                        case "FEAT": commandType = FTPCommand.FEAT; break;
                        default:
                            Reply(ServerResponseCode.命令没有执行, "Command not implemented.");
                            return;
                    }
                }

                switch (commandType)
                {
                    case FTPCommand.USER: HandleUser(request.Arg); return;
                    case FTPCommand.PASS: HandlePass(request.Arg); return;
                    case FTPCommand.SYST: Reply(ServerResponseCode.系统类型回复, "WINDOWS Type: SAEA FTP Server"); return;
                    case FTPCommand.FEAT: HandleFeat(); return;
                    case FTPCommand.OPTS: Reply(ServerResponseCode.成功, "UTF8 enabled."); return;
                    case FTPCommand.TYPE: Reply(ServerResponseCode.成功, "Type set to I"); return;
                    case FTPCommand.NOOP: Reply(ServerResponseCode.成功, "Command okay."); return;
                    case FTPCommand.QUIT:
                        Reply(ServerResponseCode.退出网络, "SAEA FTP: Goodbye.");
                        _quit = true;
                        return;
                    case FTPCommand.ABOR:
                        RestOffset = 0;
                        Reply(ServerResponseCode.成功, "No transfer in progress.");
                        return;
                }

                if (!Authenticated)
                {
                    Reply(ServerResponseCode.错误指令序列, "Login with USER first.");
                    return;
                }

                switch (commandType)
                {
                    case FTPCommand.PWD: Reply(ServerResponseCode.路径名建立, "\"" + CurrentFtpPath + "\""); return;
                    case FTPCommand.CWD: HandleCwd(request.Arg); return;
                    case FTPCommand.CDUP: HandleCdup(); return;
                    case FTPCommand.PASV: HandlePasv(); return;
                    case FTPCommand.EPSV: HandleEpsv(); return;
                    case FTPCommand.PORT:
                    case FTPCommand.EPRT:
                        Reply(ServerResponseCode.命令没有执行, "Active mode is disabled.");
                        return;
                    case FTPCommand.REST: HandleRest(request.Arg); return;
                    case FTPCommand.SIZE: HandleSize(request.Arg); return;
                    case FTPCommand.MDTM: HandleMdtm(request.Arg); return;
                    case FTPCommand.MLSD:
                    case FTPCommand.LIST: HandleList(request.Arg, true); return;
                    case FTPCommand.NLST: HandleList(request.Arg, false); return;
                    case FTPCommand.RETR: HandleRetr(request.Arg); return;
                    case FTPCommand.STOR: HandleStor(request.Arg); return;
                    case FTPCommand.MKD: HandleMkd(request.Arg); return;
                    case FTPCommand.RMD: HandleRmd(request.Arg); return;
                    case FTPCommand.DELE: HandleDele(request.Arg); return;
                    case FTPCommand.RNFR: HandleRnfr(request.Arg); return;
                    case FTPCommand.RNTO: HandleRnto(request.Arg); return;
                    default:
                        Reply(ServerResponseCode.命令没有执行, "Command not implemented.");
                        return;
                }
            }

            #endregion

            #region 登录

            void HandleUser(string userName)
            {
                if (string.IsNullOrWhiteSpace(userName))
                {
                    Reply(ServerResponseCode.错误参数, "User name required.");
                    return;
                }

                var user = GetConfiguredUser(userName);

                if (user == null && !string.Equals(userName, "anonymous", StringComparison.OrdinalIgnoreCase))
                {
                    Reply(ServerResponseCode.无效的用户名, "Invalid user name.");
                    return;
                }

                PendingUserName = userName;

                if (string.Equals(userName, "anonymous", StringComparison.OrdinalIgnoreCase))
                {
                    if (CompleteLogin(userName, user != null && !string.IsNullOrEmpty(user.Root) ? user.Root : AppDomain.CurrentDomain.BaseDirectory))
                    {
                        Reply(ServerResponseCode.登录成功, "Anonymous user logged in.");
                    }
                    return;
                }

                Reply(ServerResponseCode.要求密码, "User name okay, need password.");
            }

            void HandlePass(string password)
            {
                if (string.IsNullOrEmpty(PendingUserName))
                {
                    Reply(ServerResponseCode.错误指令序列, "Login with USER first.");
                    return;
                }

                var user = GetConfiguredUser(PendingUserName);

                if (user == null && !string.Equals(PendingUserName, "anonymous", StringComparison.OrdinalIgnoreCase))
                {
                    Reply(ServerResponseCode.无效的用户名, "Authentication failed.");
                    return;
                }

                if (string.Equals(PendingUserName, "anonymous", StringComparison.OrdinalIgnoreCase))
                {
                    if (CompleteLogin(PendingUserName, user != null && !string.IsNullOrEmpty(user.Root) ? user.Root : AppDomain.CurrentDomain.BaseDirectory))
                    {
                        Reply(ServerResponseCode.登录成功, "Anonymous user logged in.");
                    }
                    return;
                }

                if (user.Password == password)
                {
                    if (CompleteLogin(PendingUserName, user.Root ?? "C:\\"))
                    {
                        Reply(ServerResponseCode.登录成功, "User logged in, proceed.");
                    }
                }
                else
                {
                    Reply(ServerResponseCode.无效的用户名, "Authentication failed.");
                }
            }

            FTPUser GetConfiguredUser(string userName)
            {
                FTPUser user;

                if (_server.Config.Users != null && _server.Config.Users.TryGetValue(userName, out user))
                {
                    return user;
                }

                return null;
            }

            bool CompleteLogin(string userName, string root)
            {
                if (!Authenticated && !_server.TryRegisterLogin(userName))
                {
                    Reply(ServerResponseCode.服务关闭, "Too many sessions for this user.");
                    _quit = true;
                    return false;
                }

                Authenticated = true;
                UserName = userName;
                RootPath = NormalizeRoot(root);
                CurrentFsPath = RootPath;
                CurrentFtpPath = "/";

                return true;
            }

            static string NormalizeRoot(string root)
            {
                if (string.IsNullOrWhiteSpace(root)) return AppDomain.CurrentDomain.BaseDirectory;

                try
                {
                    var full = Path.GetFullPath(root);
                    return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }
                catch
                {
                    return root;
                }
            }

            #endregion

            #region 路径

            internal string ResolvePath(string arg)
            {
                var name = (arg ?? string.Empty).Replace('\\', '/');

                string path;

                if (name.StartsWith("/", StringComparison.Ordinal))
                {
                    path = Path.Combine(RootPath, name.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                }
                else
                {
                    path = Path.Combine(CurrentFsPath, name.Replace('/', Path.DirectorySeparatorChar));
                }

                path = Path.GetFullPath(path);

                var root = Path.GetFullPath(RootPath);

                if (!string.Equals(path, root, StringComparison.OrdinalIgnoreCase) &&
                    !path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException("Path is outside of the root directory.");
                }

                return path;
            }

            internal string ToFtpPath(string fsPath)
            {
                var root = Path.GetFullPath(RootPath);
                var full = Path.GetFullPath(fsPath);

                if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase)) return "/";

                if (full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    return "/" + full.Substring(root.Length + 1).Replace('\\', '/');
                }

                return "/";
            }

            #endregion

            #region 目录与文件信息

            void HandleFeat()
            {
                var sb = new StringBuilder();
                sb.Append("211-Extensions supported:\r\n");
                sb.Append(" REST STREAM\r\n");
                sb.Append(" SIZE\r\n");
                sb.Append(" MDTM\r\n");
                sb.Append(" MLSD\r\n");
                sb.Append(" UTF8\r\n");
                sb.Append(" EPSV\r\n");
                sb.Append(" SAEA-OVERWRITE=" + OverwritePolicyHint + "\r\n");
                sb.Append("211 End\r\n");
                ReplyRaw(sb.ToString());
            }

            void HandleRest(string arg)
            {
                long offset;
                if (!long.TryParse(arg, out offset) || offset < 0)
                {
                    Reply(ServerResponseCode.错误参数, "Invalid REST offset.");
                    return;
                }

                RestOffset = offset;
                RestIssued = true;
                Reply(ServerResponseCode.文件行为暂停, "Restarting at " + offset + ". Send STORE or RETRIEVE.");
            }

            void HandleSize(string arg)
            {
                try
                {
                    var path = ResolvePath(arg);

                    if (!File.Exists(path))
                    {
                        Reply(ServerResponseCode.找不到文件或文件夹, "No such file.");
                        return;
                    }

                    Reply(ServerResponseCode.文件状态回复, new FileInfo(path).Length.ToString());
                }
                catch (Exception ex)
                {
                    _server.Log("SIZE 失败", ex);
                    Reply(ServerResponseCode.找不到文件或文件夹, "No such file.");
                }
            }

            void HandleMdtm(string arg)
            {
                try
                {
                    var path = ResolvePath(arg);

                    if (!File.Exists(path))
                    {
                        Reply(ServerResponseCode.找不到文件或文件夹, "No such file.");
                        return;
                    }

                    Reply(ServerResponseCode.文件状态回复, File.GetLastWriteTimeUtc(path).ToString("yyyyMMddHHmmss"));
                }
                catch (Exception ex)
                {
                    _server.Log("MDTM 失败", ex);
                    Reply(ServerResponseCode.找不到文件或文件夹, "No such file.");
                }
            }

            void HandleCwd(string arg)
            {
                try
                {
                    var path = ResolvePath(arg);

                    if (!Directory.Exists(path))
                    {
                        Reply(ServerResponseCode.找不到文件或文件夹, "No such directory.");
                        return;
                    }

                    CurrentFsPath = path;
                    CurrentFtpPath = ToFtpPath(path);
                    Reply(ServerResponseCode.文件行为完成, "Command okay.");
                }
                catch (Exception ex)
                {
                    _server.Log("CWD 失败", ex);
                    Reply(ServerResponseCode.找不到文件或文件夹, "No such directory.");
                }
            }

            void HandleCdup()
            {
                try
                {
                    var root = Path.GetFullPath(RootPath);
                    var parent = Directory.GetParent(CurrentFsPath);

                    if (parent == null || parent.FullName.Length < root.Length)
                    {
                        CurrentFsPath = root;
                    }
                    else
                    {
                        CurrentFsPath = parent.FullName;
                    }

                    CurrentFtpPath = ToFtpPath(CurrentFsPath);
                    Reply(ServerResponseCode.文件行为完成, "Command okay.");
                }
                catch (Exception ex)
                {
                    _server.Log("CDUP 失败", ex);
                    Reply(ServerResponseCode.找不到文件或文件夹, "No such directory.");
                }
            }

            void HandleMkd(string arg)
            {
                try
                {
                    var path = ResolvePath(arg);
                    Directory.CreateDirectory(path);
                    Reply(ServerResponseCode.路径名建立, "\"" + ToFtpPath(path) + "\"");
                }
                catch (Exception ex)
                {
                    _server.Log("MKD 失败", ex);
                    Reply(ServerResponseCode.找不到文件或文件夹, "Cannot create directory.");
                }
            }

            void HandleRmd(string arg)
            {
                try
                {
                    var path = ResolvePath(arg);
                    Directory.Delete(path, false);
                    Reply(ServerResponseCode.文件行为完成, "Directory removed.");
                }
                catch (Exception ex)
                {
                    _server.Log("RMD 失败", ex);
                    Reply(ServerResponseCode.找不到文件或文件夹, "Cannot remove directory.");
                }
            }

            void HandleDele(string arg)
            {
                try
                {
                    var path = ResolvePath(arg);
                    File.Delete(path);
                    Reply(ServerResponseCode.文件行为完成, "File removed.");
                }
                catch (Exception ex)
                {
                    _server.Log("DELE 失败", ex);
                    Reply(ServerResponseCode.找不到文件或文件夹, "Cannot remove file.");
                }
            }

            void HandleRnfr(string arg)
            {
                try
                {
                    var path = ResolvePath(arg);

                    if (!File.Exists(path) && !Directory.Exists(path))
                    {
                        Reply(ServerResponseCode.找不到文件或文件夹, "No such file or directory.");
                        return;
                    }

                    RenameFrom = path;
                    Reply(ServerResponseCode.文件行为暂停, "Ready for RNTO.");
                }
                catch (Exception ex)
                {
                    _server.Log("RNFR 失败", ex);
                    Reply(ServerResponseCode.找不到文件或文件夹, "No such file or directory.");
                }
            }

            void HandleRnto(string arg)
            {
                if (string.IsNullOrEmpty(RenameFrom))
                {
                    Reply(ServerResponseCode.错误指令序列, "Use RNFR first.");
                    return;
                }

                try
                {
                    var path = ResolvePath(arg);

                    if (File.Exists(RenameFrom))
                    {
                        File.Move(RenameFrom, path);
                    }
                    else if (Directory.Exists(RenameFrom))
                    {
                        Directory.Move(RenameFrom, path);
                    }

                    RenameFrom = null;
                    Reply(ServerResponseCode.文件行为完成, "Rename successful.");
                }
                catch (Exception ex)
                {
                    _server.Log("RNTO 失败", ex);
                    Reply(ServerResponseCode.找不到文件或文件夹, "Rename failed.");
                }
            }

            #endregion

            #region 被动模式

            void HandlePasv()
            {
                var listener = CreatePassiveListener();

                if (listener == null)
                {
                    Reply(ServerResponseCode.无法打开数据连接, "Cannot open passive port.");
                    return;
                }

                var port = listener.Port;
                var ip = _server.Config.IP;

                if (string.IsNullOrEmpty(ip)) ip = "127.0.0.1";

                var pasv = ip.Replace(".", ",") + "," + (port / 256) + "," + (port % 256);

                Reply(ServerResponseCode.进入被动模式, "Entering Passive Mode (" + pasv + ")");
            }

            void HandleEpsv()
            {
                var listener = CreatePassiveListener();

                if (listener == null)
                {
                    Reply(ServerResponseCode.无法打开数据连接, "Cannot open passive port.");
                    return;
                }

                var port = listener.Port;

                Reply(ServerResponseCode.进入扩展被动模式, "Entering Extended Passive Mode (|||" + port + "|)");
            }

            FtpDataListener CreatePassiveListener()
            {
                ClosePassive();

                var listener = new FtpDataListener(_server, _server.CreateDataOptions());

                if (listener.TryStart())
                {
                    _passive = listener;
                    return listener;
                }

                listener.Dispose();
                return null;
            }

            FtpDataConnection AcceptDataConnection()
            {
                var listener = _passive;

                if (listener == null)
                {
                    throw new FTPCommandException(ServerResponseCode.无法打开数据连接, "Use PASV or EPSV first.");
                }

                var timeout = _server.Config.DataTimeout;
                if (timeout <= TimeSpan.Zero) timeout = TimeSpan.FromSeconds(60);

                FtpDataConnection data;

                try
                {
                    data = listener.Accept(timeout);
                }
                catch
                {
                    // 被动端口已随接受失败收口，清掉引用，让下一条传输命令重新 PASV。
                    _passive = null;
                    throw;
                }

                // 与旧实现用同一个等待上限约束单次收发，避免对端停住后传输线程永久挂起。
                var milliseconds = timeout.TotalMilliseconds;
                if (milliseconds > int.MaxValue) milliseconds = int.MaxValue;
                data.ReadTimeout = (int)milliseconds;
                data.WriteTimeout = (int)milliseconds;

                return data;
            }

            void ClosePassive()
            {
                var listener = _passive;

                if (listener == null) return;

                _passive = null;

                try { listener.Dispose(); } catch { }
            }

            static void CloseData(FtpDataConnection data)
            {
                if (data == null) return;

                // 先半关闭发送方向发出 FIN，再断开；只有全部写完成交接后才会走到这里。
                data.ShutdownSend();

                try { data.Close(); } catch { }
            }

            #endregion

            #region 传输

            void HandleList(string arg, bool detailed)
            {
                try
                {
                    var path = ResolvePath(arg);

                    if (Directory.Exists(path))
                    {
                        // 目录本身
                    }
                    else if (File.Exists(path))
                    {
                        // 单文件
                    }
                    else
                    {
                        Reply(ServerResponseCode.找不到文件或文件夹, "No such directory.");
                        return;
                    }

                    Reply(ServerResponseCode.打开连接, "File status okay; about to open data connection.");

                    FtpDataConnection data = null;

                    try
                    {
                        data = AcceptDataConnection();

                        var stream = data.Stream;

                        foreach (var line in BuildListLines(path, detailed))
                        {
                            var bytes = Encoding.UTF8.GetBytes(line + "\r\n");
                            stream.Write(bytes, 0, bytes.Length);
                        }

                        stream.Flush();
                    }
                    finally
                    {
                        // 先关数据连接再释放监听器，底层释放会连带杀掉已接受连接。
                        CloseData(data);
                        ClosePassive();
                    }

                    Reply(ServerResponseCode.结束数据连接, "Transfer complete.");
                }
                catch (Exception ex)
                {
                    _server.Log("LIST/MLSD 失败", ex);
                    Reply(ServerResponseCode.无法打开数据连接, "Transfer failed.");
                }
            }

            IEnumerable<string> BuildListLines(string path, bool detailed)
            {
                if (File.Exists(path))
                {
                    yield return BuildLine(new FileInfo(path), detailed);
                    yield break;
                }

                var dirs = Directory.GetDirectories(path);

                foreach (var dir in dirs)
                {
                    var info = new DirectoryInfo(dir);
                    yield return "type=dir;modify=" + info.LastWriteTimeUtc.ToString("yyyyMMddHHmmss") + "; " + info.Name;
                }

                var files = Directory.GetFiles(path);

                foreach (var file in files)
                {
                    yield return BuildLine(new FileInfo(file), detailed);
                }
            }

            static string BuildLine(FileInfo info, bool detailed)
            {
                if (!detailed)
                {
                    return info.Name;
                }

                return "type=file;size=" + info.Length + ";modify=" + info.LastWriteTimeUtc.ToString("yyyyMMddHHmmss") + "; " + info.Name;
            }

            void HandleRetr(string arg)
            {
                string path;

                try
                {
                    path = ResolvePath(arg);
                }
                catch (Exception ex)
                {
                    _server.Log("RETR 路径失败", ex);
                    Reply(ServerResponseCode.找不到文件或文件夹, "No such file.");
                    return;
                }

                if (!File.Exists(path))
                {
                    Reply(ServerResponseCode.找不到文件或文件夹, "No such file.");
                    return;
                }

                var offset = RestOffset;
                RestOffset = 0;
                RestIssued = false;

                var length = new FileInfo(path).Length;

                if (offset > length)
                {
                    Reply(ServerResponseCode.找不到文件或文件夹, "Restart offset is beyond end of file.");
                    return;
                }

                try
                {
                    Reply(ServerResponseCode.打开连接, "Opening data connection.");

                    FtpDataConnection data = null;

                    try
                    {
                        data = AcceptDataConnection();

                        var stream = data.Stream;
                        var bufferSize = Math.Max(4096, _server.Config.BufferSize);
                        var buffer = new byte[bufferSize];

                        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            fs.Seek(offset, SeekOrigin.Begin);

                            while (true)
                            {
                                var count = fs.Read(buffer, 0, buffer.Length);
                                if (count <= 0) break;

                                stream.Write(buffer, 0, count);
                            }
                        }

                        stream.Flush();
                    }
                    finally
                    {
                        // 先关数据连接再释放监听器，底层释放会连带杀掉已接受连接，截断尾部数据。
                        CloseData(data);
                        ClosePassive();
                    }

                    Reply(ServerResponseCode.结束数据连接, "Transfer complete.");
                }
                catch (Exception ex)
                {
                    // 客户端按分片读取后主动关闭属于正常现象，不记录为错误
                    if (!(ex is IOException) && !(ex is SocketException))
                    {
                        _server.Log("RETR 传输中断", ex);
                    }

                    Reply(ServerResponseCode.结束连接, "Connection closed; transfer aborted.");
                }
            }

            void HandleStor(string arg)
            {
                string path;

                try
                {
                    path = ResolvePath(arg);
                }
                catch (Exception ex)
                {
                    _server.Log("STOR 路径失败", ex);
                    Reply(ServerResponseCode.找不到文件或文件夹, "Invalid path.");
                    return;
                }

                var offset = RestOffset;
                var restIssued = RestIssued;
                RestOffset = 0;
                RestIssued = false;

                string newName = null;

                if (!restIssued && offset == 0 && File.Exists(path))
                {
                    if (_server.Config.OverwritePolicy == FTPOverwritePolicy.Reject)
                    {
                        Reply(ServerResponseCode.找不到文件或文件夹, "File already exists.");
                        return;
                    }

                    if (_server.Config.OverwritePolicy == FTPOverwritePolicy.Rename)
                    {
                        path = BuildUniquePath(path);
                        newName = Path.GetFileName(path);
                    }
                }

                try
                {
                    Reply(ServerResponseCode.打开连接, "Opening data connection.");

                    FtpDataConnection data = null;

                    try
                    {
                        data = AcceptDataConnection();

                        var stream = data.Stream;
                        var bufferSize = Math.Max(4096, _server.Config.BufferSize);
                        var buffer = new byte[bufferSize];

                        var mode = !restIssued && offset == 0 ? FileMode.Create : FileMode.OpenOrCreate;

                        using (var fs = new FileStream(path, mode, FileAccess.Write, FileShare.ReadWrite))
                        {
                            if (offset > 0 || restIssued)
                            {
                                fs.Seek(offset, SeekOrigin.Begin);
                            }

                            while (true)
                            {
                                var count = stream.Read(buffer, 0, buffer.Length);
                                if (count <= 0) break;
                                fs.Write(buffer, 0, count);
                            }

                            fs.Flush();
                        }
                    }
                    finally
                    {
                        // 先关数据连接再释放监听器，底层释放会连带杀掉已接受连接。
                        CloseData(data);
                        ClosePassive();
                    }

                    var message = "Transfer complete.";
                    if (!string.IsNullOrEmpty(newName)) message += " NEWNAME=" + newName;

                    Reply(ServerResponseCode.结束数据连接, message);
                }
                catch (Exception ex)
                {
                    _server.Log("STOR 传输中断", ex);
                    Reply(ServerResponseCode.结束连接, "Connection closed; transfer aborted.");
                }
            }

            static string BuildUniquePath(string path)
            {
                var dir = Path.GetDirectoryName(path) ?? string.Empty;
                var name = Path.GetFileNameWithoutExtension(path);
                var ext = Path.GetExtension(path);

                for (int i = 1; i < 1000; i++)
                {
                    var candidate = Path.Combine(dir, name + " (" + i + ")" + ext);
                    if (!File.Exists(candidate)) return candidate;
                }

                return Path.Combine(dir, name + " (" + Guid.NewGuid().ToString("N") + ")" + ext);
            }

            #endregion

            public void Dispose()
            {
                if (_disposed) return;

                _disposed = true;

                ClosePassive();

                _pipeline = null;
            }
        }

        #endregion
    }
}