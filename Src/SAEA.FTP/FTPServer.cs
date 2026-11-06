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
*修改时间：2026/11/6 12:52:28
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Common;
using SAEA.FTP.Core;
using SAEA.FTP.Model;
using SAEA.FTP.Net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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

        TcpListener _listener;

        CancellationTokenSource _cts;

        Task _acceptTask;

        bool _running;

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

            _cts = new CancellationTokenSource();

            IPAddress address = IPAddress.Any;

            IPAddress parsed;
            if (!string.IsNullOrEmpty(_serverConfig.IP) && IPAddress.TryParse(_serverConfig.IP, out parsed) && !parsed.Equals(IPAddress.Any))
            {
                address = parsed;
            }

            _listener = new TcpListener(address, _serverConfig.Port);
            _listener.Start();

            _running = true;

            var token = _cts.Token;

            _acceptTask = Task.Run(() => AcceptLoop(token));
        }

        /// <summary>
        /// 停止监听并关闭全部现存会话，未运行时直接返回
        /// </summary>
        public void Stop()
        {
            if (!_running) return;

            _running = false;

            try { _cts.Cancel(); } catch { }

            try { _listener.Stop(); } catch { }

            foreach (var session in _sessions.Values)
            {
                session.Dispose();
            }

            _sessions.Clear();
        }

        async Task AcceptLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client = null;

                try
                {
                    client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException)
                {
                    if (token.IsCancellationRequested) break;
                    continue;
                }
                catch (Exception ex)
                {
                    if (token.IsCancellationRequested) break;
                    Log("接受连接失败", ex);
                    continue;
                }

                if (client == null) continue;

                var session = new ServerSession(this, client);

                _sessions[session.Id] = session;

                _ = Task.Run(() => session.Run());
            }
        }

        /// <summary>
        /// 停止服务端并释放监听器，可重复调用
        /// </summary>
        public void Dispose()
        {
            Stop();
            try { _listener = null; } catch { }
        }

        #endregion

        #region 会话

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

            readonly TcpClient _control;

            readonly NetworkStream _stream;

            readonly byte[] _readBuffer = new byte[8192];

            int _readPos;

            int _readLen;

            TcpListener _passive;

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

            public ServerSession(FTPServer server, TcpClient control)
            {
                _server = server;
                _control = control;
                _control.NoDelay = true;
                _stream = control.GetStream();
                Id = Guid.NewGuid().ToString("N");
            }

            public void Run()
            {
                try
                {
                    Reply(ServerResponseCode.服务就绪, "Welcome to SAEA.FTPServer!");

                    while (!_disposed && !_quit)
                    {
                        var line = ReadCommandLine();

                        if (line == null) break;

                        if (string.IsNullOrWhiteSpace(line)) continue;

                        Handle(line.Trim());
                    }
                }
                catch (IOException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
                catch (Exception ex)
                {
                    _server.Log("FTP会话异常", ex);
                }
                finally
                {
                    _server.RemoveSession(this);
                }
            }

            #region 命令读取与回复

            int ReadByte()
            {
                if (_readPos >= _readLen)
                {
                    _readLen = _stream.Read(_readBuffer, 0, _readBuffer.Length);
                    _readPos = 0;

                    if (_readLen <= 0) return -1;
                }

                return _readBuffer[_readPos++];
            }

            string ReadCommandLine()
            {
                var bytes = new List<byte>(256);

                while (true)
                {
                    var value = ReadByte();

                    if (value < 0)
                    {
                        return bytes.Count == 0 ? null : Encoding.UTF8.GetString(bytes.ToArray());
                    }

                    if (value == '\n')
                    {
                        if (bytes.Count > 0 && bytes[bytes.Count - 1] == '\r')
                        {
                            bytes.RemoveAt(bytes.Count - 1);
                        }
                        break;
                    }

                    bytes.Add((byte)value);
                }

                return Encoding.UTF8.GetString(bytes.ToArray());
            }

            internal void Reply(int code, string message)
            {
                WriteLine(code + " " + message);
            }

            internal void ReplyRaw(string text)
            {
                var data = Encoding.UTF8.GetBytes(text);
                _stream.Write(data, 0, data.Length);
                _stream.Flush();
            }

            void WriteLine(string text)
            {
                var data = Encoding.UTF8.GetBytes(text + "\r\n");
                _stream.Write(data, 0, data.Length);
                _stream.Flush();
            }

            #endregion

            #region 命令分发

            void Handle(string line)
            {
                _server.Log("收到命令：" + line, null);

                ClientRequest request;

                try
                {
                    request = ClientRequest.Parse(line);
                }
                catch
                {
                    Reply(ServerResponseCode.错误参数, "Invalid command.");
                    return;
                }

                if (request == null)
                {
                    Reply(ServerResponseCode.错误参数, "Invalid command.");
                    return;
                }

                FTPCommand command;
                if (!Enum.TryParse(request.Cmd, true, out command))
                {
                    switch (request.Cmd.ToUpperInvariant())
                    {
                        case "SIZE": command = FTPCommand.SIZE; break;
                        case "MDTM": command = FTPCommand.MDTM; break;
                        case "REST": command = FTPCommand.REST; break;
                        case "EPSV": command = FTPCommand.EPSV; break;
                        case "EPRT": command = FTPCommand.EPRT; break;
                        case "ABOR": command = FTPCommand.ABOR; break;
                        case "FEAT": command = FTPCommand.FEAT; break;
                        default:
                            Reply(ServerResponseCode.命令没有执行, "Command not implemented.");
                            return;
                    }
                }

                switch (command)
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

                switch (command)
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

                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
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

                var port = ((IPEndPoint)listener.LocalEndpoint).Port;

                Reply(ServerResponseCode.进入扩展被动模式, "Entering Extended Passive Mode (|||" + port + "|)");
            }

            TcpListener CreatePassiveListener()
            {
                ClosePassive();

                var min = _server.Config.PassivePortMin;
                var max = _server.Config.PassivePortMax;

                if (min > 0 && max >= min)
                {
                    for (int port = min; port <= max; port++)
                    {
                        try
                        {
                            var listener = new TcpListener(IPAddress.Any, port);
                            listener.Start();
                            _passive = listener;
                            return listener;
                        }
                        catch (SocketException)
                        {
                        }
                    }
                }

                try
                {
                    var listener = new TcpListener(IPAddress.Any, 0);
                    listener.Start();
                    _passive = listener;
                    return listener;
                }
                catch (Exception ex)
                {
                    _server.Log("创建被动端口失败", ex);
                    return null;
                }
            }

            TcpClient AcceptDataConnection()
            {
                if (_passive == null)
                {
                    throw new FTPCommandException(ServerResponseCode.无法打开数据连接, "Use PASV or EPSV first.");
                }

                var timeout = _server.Config.DataTimeout;
                if (timeout <= TimeSpan.Zero) timeout = TimeSpan.FromSeconds(60);

                var acceptTask = _passive.AcceptTcpClientAsync();

                if (!acceptTask.Wait(timeout))
                {
                    throw new TimeoutException("Waiting for FTP data connection timed out.");
                }

                var client = acceptTask.Result;
                client.NoDelay = true;
                return client;
            }

            void ClosePassive()
            {
                if (_passive != null)
                {
                    try { _passive.Stop(); } catch { }
                    _passive = null;
                }
            }

            void CloseData(TcpClient client)
            {
                if (client == null) return;

                try { client.Client.Shutdown(SocketShutdown.Send); } catch { }

                try { client.Close(); } catch { }
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

                    using (var data = AcceptDataConnection())
                    {
                        var stream = data.GetStream();

                        foreach (var line in BuildListLines(path, detailed))
                        {
                            var bytes = Encoding.UTF8.GetBytes(line + "\r\n");
                            stream.Write(bytes, 0, bytes.Length);
                        }

                        stream.Flush();
                        CloseData(data);
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

                    using (var data = AcceptDataConnection())
                    {
                        var stream = data.GetStream();
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
                        CloseData(data);
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

                    using (var data = AcceptDataConnection())
                    {
                        var stream = data.GetStream();
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

                        CloseData(data);
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

                try { _stream.Dispose(); } catch { }
                try { _control.Close(); } catch { }
            }
        }

        #endregion
    }
}