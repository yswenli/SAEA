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
*文件名： FTPClient
*版本号： v26.4.23.1
*唯一标识：56e93077-2428-4192-8773-2891e3321bef
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/09/27 17:50:27
*描述：FTPClient接口
*
*=====================================================================
*修改标记
*修改时间：2019/09/27 17:50:27
*修改人： yswenli
*版本号： v26.4.23.1
*描述：FTPClient接口
*
*****************************************************************************/
using SAEA.Common;
using SAEA.FTP.Model;
using SAEA.FTP.Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.FTP
{
    /// <summary>
    /// FTP 客户端。支持冲突处理、自动重试、断点续传与单文件多会话并行传输。
    /// </summary>
    public class FTPClient : IDisposable
    {
        readonly ClientConfig _config;

        readonly object _syncRoot = new object();

        FtpControlConnection _control;

        string _remoteCwd = "/";

        bool _disposed;

        public event Action OnConnected;

        public event Action Ondisconnected;

        public ClientConfig Config
        {
            get { return _config; }
        }

        public bool Connected
        {
            get { return _control != null && _control.Connected; }
        }

        public FTPClient(ClientConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            _config = config;

            if (string.IsNullOrEmpty(_config.IP)) throw new ArgumentException("FTP server ip is required.", nameof(config));

            if (_config.Port <= 0) throw new ArgumentException("FTP server port is required.", nameof(config));
        }

        public FTPClient(string ip, int port, string userName, string password, int bufferSize = 0)
            : this(new ClientConfig()
            {
                IP = ip,
                Port = port,
                UserName = userName,
                Password = password,
                BufferSize = bufferSize > 0 ? bufferSize : 64 * 1024
            })
        {
        }

        #region 连接与会话

        public void Connect()
        {
            lock (_syncRoot)
            {
                if (_control != null && _control.Connected) return;

                var control = new FtpControlConnection(_config);

                control.Connect();

                _control = control;

                if (!string.IsNullOrEmpty(_remoteCwd) && _remoteCwd != "/")
                {
                    try { control.Send("CWD " + _remoteCwd); } catch { }
                }
            }

            var handler = OnConnected;
            if (handler != null) handler();
        }

        void EnsureConnected()
        {
            if (_control != null && _control.Connected) return;

            lock (_syncRoot)
            {
                if (_control != null && _control.Connected) return;

                var control = new FtpControlConnection(_config);

                control.Connect();

                _control = control;

                if (!string.IsNullOrEmpty(_remoteCwd) && _remoteCwd != "/")
                {
                    try { control.Send("CWD " + _remoteCwd); } catch { }
                }
            }

            var handler = OnConnected;
            if (handler != null) handler();
        }

        void DropControl()
        {
            var control = _control;

            _control = null;

            if (control != null)
            {
                try { control.Dispose(); } catch { }
            }
        }

        /// <summary>
        /// 为并行分片创建一条独立控制会话（同账号重复登录）。
        /// </summary>
        FtpControlConnection CreateSession(TimeSpan dataTimeout)
        {
            var session = new FtpControlConnection(_config);

            session.DataTimeout = dataTimeout;

            session.CommandTimeout = _control != null ? _control.CommandTimeout : _config.CommandTimeout;

            try
            {
                session.Connect();

                if (!string.IsNullOrEmpty(_remoteCwd) && _remoteCwd != "/")
                {
                    var reply = session.Send("CWD " + _remoteCwd);

                    if (reply.Code != ServerResponseCode.文件行为完成 && reply.Code != ServerResponseCode.成功)
                    {
                        throw new FTPCommandException(reply.Code, reply.Message);
                    }
                }
            }
            catch
            {
                try { session.Dispose(); } catch { }

                throw;
            }

            return session;
        }

        public void Noop()
        {
            try
            {
                EnsureConnected();

                _control.Send(FTPCommand.NOOP.ToString());
            }
            catch (Exception ex)
            {
                LogHelper.Error("FTPClient Noop", ex);
            }
        }

        #endregion

        #region 目录与文件命令

        /// <summary>
        /// 更改工作目录
        /// </summary>
        public bool ChangeDir(string pathName)
        {
            EnsureConnected();

            var reply = _control.Send(FTPCommand.CWD + " " + pathName);

            if (reply.Code == ServerResponseCode.文件行为完成 || reply.Code == ServerResponseCode.成功)
            {
                _remoteCwd = NormalizeRemotePath(pathName);
                return true;
            }

            if (reply.Code == ServerResponseCode.找不到文件或文件夹)
            {
                return false;
            }

            throw new FTPCommandException(reply.Code, reply.Message);
        }

        /// <summary>
        /// 更改工作目录到父目录
        /// </summary>
        public bool ChangeToParentDir()
        {
            EnsureConnected();

            var reply = _control.Send(FTPCommand.CDUP.ToString());

            if (reply.Code == ServerResponseCode.文件行为完成 || reply.Code == ServerResponseCode.成功)
            {
                _remoteCwd = GetParentRemotePath(_remoteCwd);
                return true;
            }

            if (reply.Code == ServerResponseCode.找不到文件或文件夹)
            {
                return false;
            }

            throw new FTPCommandException(reply.Code, reply.Message);
        }

        /// <summary>
        /// 返回当前工作目录
        /// </summary>
        public string CurrentDir()
        {
            EnsureConnected();

            var reply = _control.Send(FTPCommand.PWD.ToString());

            if (reply.Code == ServerResponseCode.路径名建立)
            {
                var dir = reply.Message ?? string.Empty;

                var first = dir.IndexOf('"');
                var last = dir.LastIndexOf('"');

                if (first > -1 && last > first)
                {
                    dir = dir.Substring(first + 1, last - first - 1);
                }

                _remoteCwd = string.IsNullOrEmpty(dir) ? "/" : dir;

                return _remoteCwd;
            }

            throw new FTPCommandException(reply.Code, reply.Message);
        }

        /// <summary>
        /// 返回指定路径下的子目录及文件列表，默认为当前工作地址
        /// </summary>
        public List<string> Dir(string pathName = "/", DirType dirType = DirType.MLSD)
        {
            EnsureConnected();

            var command = dirType.ToString() + " " + (string.IsNullOrEmpty(pathName) ? string.Empty : pathName);

            var data = _control.OpenPassive(_control.DataTimeout);

            var reply = _control.Send(command);

            if (reply.Code != ServerResponseCode.打开连接 && reply.Code != ServerResponseCode.打开数据连接开始传输)
            {
                try { data.Close(); } catch { }

                if (reply.Code == ServerResponseCode.找不到文件或文件夹)
                {
                    if (ChangeDir(pathName)) return new List<string>();

                    return null;
                }

                throw new FTPCommandException(reply.Code, reply.Message);
            }

            var buffer = new byte[Math.Max(4096, _config.BufferSize)];

            var list = new List<byte>();

            try
            {
                using (var stream = data.GetStream())
                {
                    while (true)
                    {
                        var count = stream.Read(buffer, 0, buffer.Length);

                        if (count <= 0) break;

                        for (int i = 0; i < count; i++) list.Add(buffer[i]);
                    }
                }
            }
            finally
            {
                try { data.Close(); } catch { }
            }

            // 读取传输结束响应
            try { _control.ReadReply(); } catch { }

            if (list.Count == 0) return new List<string>();

            var text = (_config.Encoding ?? Encoding.UTF8).GetString(list.ToArray());

            return new List<string>(text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries));
        }

        public void MakeDir(string pathName)
        {
            EnsureConnected();

            var reply = _control.Send(FTPCommand.MKD + " " + pathName);

            if (reply.Code != ServerResponseCode.文件行为完成 && reply.Code != ServerResponseCode.路径名建立)
            {
                throw new FTPCommandException(reply.Code, reply.Message);
            }
        }

        public void RemoveDir(string pathName)
        {
            EnsureConnected();

            var reply = _control.Send(FTPCommand.RMD + " " + pathName);

            if (reply.Code != ServerResponseCode.文件行为完成)
            {
                throw new FTPCommandException(reply.Code, reply.Message);
            }
        }

        public void Rename(string oldName, string newName)
        {
            EnsureConnected();

            var reply = _control.Send(FTPCommand.RNFR + " " + oldName);

            if (reply.Code != ServerResponseCode.文件行为暂停 && reply.Code != ServerResponseCode.文件行为完成)
            {
                throw new FTPCommandException(reply.Code, reply.Message);
            }

            reply = _control.Send(FTPCommand.RNTO + " " + newName);

            if (reply.Code != ServerResponseCode.文件行为完成)
            {
                throw new FTPCommandException(reply.Code, reply.Message);
            }
        }

        public void Delete(string fileName)
        {
            EnsureConnected();

            var reply = _control.Send(FTPCommand.DELE + " " + fileName);

            if (reply.Code != ServerResponseCode.文件行为完成)
            {
                throw new FTPCommandException(reply.Code, reply.Message);
            }
        }

        public long FileSize(string fileName)
        {
            EnsureConnected();

            var state = QueryRemote(_control, fileName);

            if (state.Exists) return state.Size;

            throw new FTPCommandException(ServerResponseCode.找不到文件或文件夹, "No such file: " + fileName);
        }

        #endregion

        #region 路径辅助

        static string NormalizeRemotePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "/";

            path = path.Replace('\\', '/');

            if (!path.StartsWith("/", StringComparison.Ordinal))
            {
                path = "/" + path;
            }

            return path;
        }

        static string GetParentRemotePath(string path)
        {
            if (string.IsNullOrEmpty(path) || path == "/") return "/";

            var index = path.TrimEnd('/').LastIndexOf('/');

            if (index <= 0) return "/";

            return path.Substring(0, index);
        }

        static void SplitRemotePath(string remotePath, out string dir, out string name)
        {
            var path = (remotePath ?? string.Empty).Replace('\\', '/');

            var index = path.LastIndexOf('/');

            if (index > -1)
            {
                dir = index == 0 ? "/" : path.Substring(0, index);
                name = path.Substring(index + 1);
            }
            else
            {
                dir = string.Empty;
                name = path;
            }
        }

        static string CombineRemotePath(string dir, string name)
        {
            if (string.IsNullOrEmpty(dir)) return name;

            if (dir.EndsWith("/", StringComparison.Ordinal)) return dir + name;

            return dir + "/" + name;
        }

        static string CombineRemoteTarget(string cwd, string remotePath)
        {
            if (string.IsNullOrEmpty(remotePath)) return remotePath;

            var path = remotePath.Replace('\\', '/');

            if (path.StartsWith("/", StringComparison.Ordinal)) return path;

            if (path.StartsWith("./", StringComparison.Ordinal)) path = path.Substring(2);

            return CombineRemotePath(string.IsNullOrEmpty(cwd) ? "/" : cwd, path);
        }

        static string BuildRenamedPath(string path, string pattern, int index, bool localPath)
        {
            string dir;
            string name;

            if (localPath)
            {
                dir = Path.GetDirectoryName(path) ?? string.Empty;
                name = Path.GetFileName(path);
            }
            else
            {
                SplitRemotePath(path, out dir, out name);
            }

            var baseName = Path.GetFileNameWithoutExtension(name);
            var ext = Path.GetExtension(name);

            if (string.IsNullOrEmpty(pattern)) pattern = "{name} ({index}){ext}";

            var newName = pattern
                .Replace("{name}", baseName)
                .Replace("{index}", index.ToString())
                .Replace("{ext}", ext);

            if (localPath)
            {
                return string.IsNullOrEmpty(dir) ? newName : Path.Combine(dir, newName);
            }

            return CombineRemotePath(dir, newName);
        }

        #endregion

        #region 上传

        /// <summary>
        /// 上传文件（兼容旧签名）。同名冲突默认跳过；传输失败时抛异常。
        /// </summary>
        public void Upload(string filePath, Action<long, long> uploading = null)
        {
            var result = Upload(filePath, (string)null, new FTPTransferOptions(), uploading);

            if (result.Status == FTPTransferStatus.Failed)
            {
                if (result.Error != null) throw result.Error;

                throw new IOException("FTP upload failed.");
            }
        }

        /// <summary>
        /// 上传文件，返回详细结果
        /// </summary>
        public FTPTransferResult Upload(string filePath, FTPTransferOptions options, Action<long, long> uploading = null)
        {
            return Upload(filePath, (string)null, options, uploading);
        }

        /// <summary>
        /// 上传文件到指定远端文件名，返回详细结果
        /// </summary>
        public FTPTransferResult Upload(string filePath, string remoteFileName, FTPTransferOptions options, Action<long, long> uploading = null)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));

            if (!File.Exists(filePath)) throw new FileNotFoundException("Local file not found.", filePath);

            if (options == null) options = new FTPTransferOptions();

            var localSize = new FileInfo(filePath).Length;

            var remotePath = string.IsNullOrEmpty(remoteFileName)
                ? CombineRemotePath(_remoteCwd, Path.GetFileName(filePath))
                : CombineRemoteTarget(_remoteCwd, remoteFileName);

            EnsureConnected();

            var dataTimeout = options.ResolveDataTimeout(_config);

            _control.DataTimeout = dataTimeout;

            _control.CommandTimeout = options.ResolveCommandTimeout(_config);

            var remoteState = QueryRemote(_control, remotePath);

            FTPConflictAction? action = null;

            if (remoteState.Exists || remoteState.Unknown)
            {
                var decision = ResolveConflict(FTPTransferDirection.Upload, filePath, remotePath, remoteState, localSize, options);

                if (decision.Action == FTPConflictAction.Skip)
                {
                    return BuildResult(FTPTransferStatus.Skipped, filePath, remotePath, 0, 0, 1, false, decision.Action, null);
                }

                action = decision.Action;

                if (decision.Action == FTPConflictAction.Rename)
                {
                    remotePath = ResolveRenamedRemotePath(_control, remotePath, decision.NewName, options);

                    remoteState = QueryRemote(_control, remotePath);
                }
            }

            var sessionCount = ComputeSessionCount(localSize, options);

            if (sessionCount > 1 && IsRenamePolicy(_control.OverwritePolicyHint))
            {
                sessionCount = 1;
            }

            if (sessionCount > 1)
            {
                if (ProbeParallelUpload(remotePath, dataTimeout, options.ResolveBufferSize(_config)))
                {
                    var parallel = UploadParallel(filePath, remotePath, localSize, sessionCount, options, uploading, action);

                    if (parallel != null) return parallel;
                }

                DropControl();
            }

            return UploadSingle(filePath, remotePath, localSize, options, uploading, action);
        }

        FTPTransferResult UploadSingle(string localPath, string remotePath, long localSize, FTPTransferOptions options, Action<long, long> callback, FTPConflictAction? action)
        {
            var progress = new ProgressTracker(localSize, callback);

            var dataTimeout = options.ResolveDataTimeout(_config);

            var commandTimeout = options.ResolveCommandTimeout(_config);

            var bufferSize = options.ResolveBufferSize(_config);

            var adaptive = new AdaptiveBuffer(bufferSize, options);

            var metaKey = GetUploadMetaPath(remotePath);

            long sent = 0;

            var resumed = false;

            var attempts = 0;

            UploadMeta meta = null;

            if (options.EnableResumeAcrossCalls)
            {
                meta = UploadMetaStore.Load(metaKey);

                if (meta != null)
                {
                    try
                    {
                        var lastWrite = File.GetLastWriteTimeUtc(localPath).Ticks;

                        if (meta.LocalLength != localSize || meta.LocalWriteTicks != lastWrite) meta = null;
                    }
                    catch
                    {
                        meta = null;
                    }
                }
            }

            for (int attempt = 1; ; attempt++)
            {
                attempts = attempt;

                try
                {
                    EnsureConnected();

                    var conn = _control;

                    conn.DataTimeout = dataTimeout;

                    conn.CommandTimeout = commandTimeout;

                    long offset = 0;

                    if (attempt == 1 && meta != null && meta.Sent > 0 && meta.Sent < localSize)
                    {
                        var remote = QueryRemote(conn, remotePath);

                        if (remote.Exists && remote.Size == meta.Sent)
                        {
                            offset = meta.Sent;
                            resumed = true;
                        }
                    }
                    else if (attempt > 1 && options.EnableResume && sent > 0)
                    {
                        var remote = QueryRemote(conn, remotePath);

                        if (remote.Exists && remote.Size == sent)
                        {
                            offset = sent;
                            resumed = true;
                        }
                    }

                    if (offset > 0)
                    {
                        var rest = conn.Send(FTPCommand.REST + " " + offset);

                        if (rest.Code != ServerResponseCode.文件行为暂停) offset = 0;
                    }

                    sent = offset;

                    progress.Set(offset);

                    using (var data = conn.OpenPassive(dataTimeout))
                    {
                        var reply = conn.Send(FTPCommand.STOR + " " + remotePath);

                        if (reply.Code != ServerResponseCode.打开连接 && reply.Code != ServerResponseCode.打开数据连接开始传输)
                        {
                            throw new FTPCommandException(reply.Code, reply.Message);
                        }

                        var buffer = adaptive.Current;

                        using (var fs = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            fs.Seek(offset, SeekOrigin.Begin);

                            var stream = data.GetStream();

                            while (true)
                            {
                                var count = fs.Read(buffer, 0, buffer.Length);

                                if (count <= 0) break;

                                stream.Write(buffer, 0, count);

                                sent += count;

                                progress.Add(count);

                                adaptive.Add(count);

                                buffer = adaptive.Current;
                            }
                        }

                        try { data.Client.Shutdown(SocketShutdown.Send); } catch { }
                    }

                    var final = conn.ReadReply();

                    if (final.Code != ServerResponseCode.结束数据连接 && final.Code != ServerResponseCode.文件行为完成)
                    {
                        throw new FTPCommandException(final.Code, final.Message);
                    }

                    var newName = ParseNewName(final.Message);

                    if (!string.IsNullOrEmpty(newName))
                    {
                        string dir, name;
                        SplitRemotePath(remotePath, out dir, out name);

                        remotePath = CombineRemotePath(dir, newName);

                        UploadMetaStore.Remove(metaKey);

                        progress.Report(true);

                        return BuildResult(FTPTransferStatus.Renamed, localPath, remotePath, localSize, attempt, 1, false, FTPConflictAction.Rename, null);
                    }

                    UploadMetaStore.Remove(metaKey);

                    progress.Report(true);

                    return BuildResult(FTPTransferStatus.Completed, localPath, remotePath, localSize, attempt, 1, resumed || offset > 0, action, null);
                }
                catch (Exception ex)
                {
                    if (options.EnableResumeAcrossCalls && sent > 0 && sent < localSize)
                    {
                        try
                        {
                            UploadMetaStore.Save(metaKey, new UploadMeta()
                            {
                                LocalLength = localSize,
                                LocalWriteTicks = File.GetLastWriteTimeUtc(localPath).Ticks,
                                Sent = sent
                            });
                        }
                        catch { }
                    }

                    if (attempt > options.MaxRetries || !IsRetryable(ex))
                    {
                        return BuildResult(FTPTransferStatus.Failed, localPath, remotePath, sent, attempt, 1, resumed, action, ex);
                    }

                    adaptive.Rollback();

                    DropControl();

                    SleepBackoff(options, attempt);
                }
            }
        }

        FTPTransferResult UploadParallel(string localPath, string remotePath, long localSize, int sessionCount, FTPTransferOptions options, Action<long, long> callback, FTPConflictAction? action)
        {
            var dataTimeout = options.ResolveDataTimeout(_config);

            var bufferSize = options.ResolveBufferSize(_config);

            var sessions = new FtpControlConnection[sessionCount];

            var extras = new List<FtpControlConnection>();

            sessions[0] = _control;

            try
            {
                for (int i = 1; i < sessionCount; i++)
                {
                    var session = CreateSession(dataTimeout);

                    extras.Add(session);

                    sessions[i] = session;
                }
            }
            catch
            {
                DisposeAll(extras);

                return null;
            }

            try
            {
                PreTruncateRemote(sessions[0], remotePath, dataTimeout);
            }
            catch
            {
                DisposeAll(extras);

                return null;
            }

            var tracker = new ProgressTracker(localSize, callback);

            var state = new ParallelState();

            var gate = new ManualResetEventSlim(false);

            var tasks = new Task[sessionCount];

            for (int i = 0; i < sessionCount; i++)
            {
                var index = i;

                var offset = (long)index * (long)Math.Ceiling((double)localSize / sessionCount);

                var length = Math.Min((long)Math.Ceiling((double)localSize / sessionCount), localSize - offset);

                if (length <= 0) continue;

                tasks[i] = Task.Factory.StartNew(() =>
                {
                    UploadSegment(sessions[index], useMainSession: index == 0, localPath, remotePath, offset, length, bufferSize, dataTimeout, options, tracker, state, gate);
                }, TaskCreationOptions.LongRunning);
            }

            try
            {
                Task.WaitAll(tasks);
            }
            catch (AggregateException)
            {
            }

            if (state.Failed)
            {
                DisposeAll(extras);

                DropControl();

                return null;
            }

            try
            {
                var remote = QueryRemote(sessions[0], remotePath);

                if (!remote.Exists || remote.Size != localSize)
                {
                    DisposeAll(extras);

                    DropControl();

                    return null;
                }
            }
            catch
            {
                DisposeAll(extras);

                DropControl();

                return null;
            }

            DisposeAll(extras);

            tracker.Report(true);

            return BuildResult(FTPTransferStatus.Completed, localPath, remotePath, localSize, 1, sessionCount, false, action, null);
        }

        void UploadSegment(FtpControlConnection initialSession, bool useMainSession, string localPath, string remotePath, long offset, long length, int bufferSize, TimeSpan dataTimeout, FTPTransferOptions options, ProgressTracker tracker, ParallelState state, ManualResetEventSlim gate)
        {
            var session = initialSession;

            var adaptive = new AdaptiveBuffer(bufferSize, options);

            try
            {
                // 分片 0 先建立数据连接并固定目标名，其余分片再开始写入
                if (!useMainSession)
                {
                    gate.Wait();

                    if (state.Failed) return;
                }

                for (int attempt = 1; ; attempt++)
                {
                    try
                    {
                        if (state.Failed) return;

                        if (!session.Connected)
                        {
                            if (useMainSession)
                            {
                                DropControl();
                                EnsureConnected();
                                session = _control;
                            }
                            else
                            {
                                session.Dispose();
                                session = CreateSession(dataTimeout);
                            }
                        }

                        session.DataTimeout = dataTimeout;

                        session.CommandTimeout = options.ResolveCommandTimeout(_config);

                        var rest = session.Send(FTPCommand.REST + " " + offset);

                        if (rest.Code != ServerResponseCode.文件行为暂停)
                        {
                            throw new FTPCommandException(rest.Code, rest.Message);
                        }

                        using (var data = session.OpenPassive(dataTimeout))
                        {
                            var reply = session.Send(FTPCommand.STOR + " " + remotePath);

                            if (reply.Code != ServerResponseCode.打开连接 && reply.Code != ServerResponseCode.打开数据连接开始传输)
                            {
                                throw new FTPCommandException(reply.Code, reply.Message);
                            }

                            if (useMainSession) gate.Set();

                            var buffer = adaptive.Current;

                            using (var fs = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                fs.Seek(offset, SeekOrigin.Begin);

                                var stream = data.GetStream();

                                var remaining = length;

                                while (remaining > 0)
                                {
                                    if (state.Failed) throw new IOException("Parallel upload aborted.");

                                    var want = (int)Math.Min(buffer.Length, remaining);

                                    var count = fs.Read(buffer, 0, want);

                                    if (count <= 0) throw new FTPLocalException("Local file ended unexpectedly.", null);

                                    stream.Write(buffer, 0, count);

                                    remaining -= count;

                                    tracker.Add(count);

                                    adaptive.Add(count);

                                    buffer = adaptive.Current;
                                }
                            }

                            try { data.Client.Shutdown(SocketShutdown.Send); } catch { }
                        }

                        var final = session.ReadReply();

                        if (final.Code != ServerResponseCode.结束数据连接 && final.Code != ServerResponseCode.文件行为完成)
                        {
                            throw new FTPCommandException(final.Code, final.Message);
                        }

                        return;
                    }
                    catch (Exception ex)
                    {
                        if (attempt > options.MaxRetries || !IsRetryable(ex))
                        {
                            state.Fail(ex);

                            return;
                        }

                        adaptive.Rollback();

                        SleepBackoff(options, attempt);

                        if (!useMainSession)
                        {
                            try { session.Dispose(); } catch { }

                            try
                            {
                                session = CreateSession(dataTimeout);
                            }
                            catch (Exception createEx)
                            {
                                state.Fail(createEx);
                                return;
                            }
                        }
                    }
                }
            }
            finally
            {
                if (useMainSession) gate.Set();
            }
        }

        void PreTruncateRemote(FtpControlConnection session, string remotePath, TimeSpan dataTimeout)
        {
            using (var data = session.OpenPassive(dataTimeout))
            {
                var reply = session.Send(FTPCommand.STOR + " " + remotePath);

                if (reply.Code != ServerResponseCode.打开连接 && reply.Code != ServerResponseCode.打开数据连接开始传输)
                {
                    throw new FTPCommandException(reply.Code, reply.Message);
                }

                try { data.Client.Shutdown(SocketShutdown.Send); } catch { }
            }

            var final = session.ReadReply();

            if (final.Code != ServerResponseCode.结束数据连接 && final.Code != ServerResponseCode.文件行为完成)
            {
                throw new FTPCommandException(final.Code, final.Message);
            }
        }

        bool ProbeParallelUpload(string remotePath, TimeSpan dataTimeout, int bufferSize)
        {
            string dir, name;
            SplitRemotePath(remotePath, out dir, out name);

            var probePath = CombineRemotePath(dir, "." + name + ".saea-probe-" + Guid.NewGuid().ToString("N"));

            try
            {
                var first = new byte[1024];

                var second = new byte[1024];

                for (int i = 0; i < first.Length; i++)
                {
                    first[i] = (byte)(i % 251);
                    second[i] = (byte)((i * 7 + 13) % 251);
                }

                if (!StorChunk(_control, probePath, 0, first, false, dataTimeout)) return false;

                if (!StorChunk(_control, probePath, 2048, second, true, dataTimeout)) return false;

                var remote = QueryRemote(_control, probePath);

                if (remote.Unknown || !remote.Exists || remote.Size != 3072) return false;

                using (var data = _control.OpenPassive(dataTimeout))
                {
                    var rest = _control.Send(FTPCommand.REST + " 2048");

                    if (rest.Code != ServerResponseCode.文件行为暂停) return false;

                    var reply = _control.Send(FTPCommand.RETR + " " + probePath);

                    if (reply.Code != ServerResponseCode.打开连接 && reply.Code != ServerResponseCode.打开数据连接开始传输) return false;

                    var buffer = new byte[second.Length];

                    var read = 0;

                    try
                    {
                        var stream = data.GetStream();

                        while (read < buffer.Length)
                        {
                            var count = stream.Read(buffer, read, buffer.Length - read);

                            if (count <= 0) break;

                            read += count;
                        }
                    }
                    finally
                    {
                        try { data.Close(); } catch { }
                    }

                    if (read != second.Length) return false;

                    for (int i = 0; i < second.Length; i++)
                    {
                        if (buffer[i] != second[i]) return false;
                    }
                }

                try { _control.ReadReply(); } catch { }

                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                try { _control.Send(FTPCommand.DELE + " " + probePath); } catch { }
            }
        }

        bool StorChunk(FtpControlConnection session, string remotePath, long offset, byte[] payload, bool useRest, TimeSpan dataTimeout)
        {
            if (useRest)
            {
                var rest = session.Send(FTPCommand.REST + " " + offset);

                if (rest.Code != ServerResponseCode.文件行为暂停) return false;
            }

            using (var data = session.OpenPassive(dataTimeout))
            {
                var reply = session.Send(FTPCommand.STOR + " " + remotePath);

                if (reply.Code != ServerResponseCode.打开连接 && reply.Code != ServerResponseCode.打开数据连接开始传输) return false;

                try
                {
                    data.GetStream().Write(payload, 0, payload.Length);

                    data.GetStream().Flush();

                    data.Client.Shutdown(SocketShutdown.Send);
                }
                catch
                {
                    return false;
                }
            }

            try
            {
                var final = session.ReadReply();

                return final.Code == ServerResponseCode.结束数据连接 || final.Code == ServerResponseCode.文件行为完成;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region 下载

        /// <summary>
        /// 下载文件（兼容旧签名）。同名冲突默认跳过；跳过或失败返回 null。
        /// </summary>
        public string Download(string fileName, string filePath, Action<long, long> downing = null)
        {
            var result = Download(fileName, filePath, new FTPTransferOptions(), downing);

            if (result == null) return null;

            return result.Status == FTPTransferStatus.Completed || result.Status == FTPTransferStatus.Renamed
                ? result.LocalPath
                : null;
        }

        /// <summary>
        /// 下载文件，返回详细结果
        /// </summary>
        public FTPTransferResult Download(string fileName, string filePath, FTPTransferOptions options, Action<long, long> downing = null)
        {
            if (string.IsNullOrEmpty(fileName)) throw new ArgumentNullException(nameof(fileName));

            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));

            if (options == null) options = new FTPTransferOptions();

            fileName = CombineRemoteTarget(_remoteCwd, fileName);

            EnsureConnected();

            var dataTimeout = options.ResolveDataTimeout(_config);

            var commandTimeout = options.ResolveCommandTimeout(_config);

            _control.DataTimeout = dataTimeout;

            _control.CommandTimeout = commandTimeout;

            var remoteState = QueryRemote(_control, fileName);

            if (!remoteState.Exists && !remoteState.Unknown)
            {
                return BuildResult(FTPTransferStatus.Failed, filePath, fileName, 0, 0, 1, false, null, new FTPCommandException(ServerResponseCode.找不到文件或文件夹, "No such file: " + fileName));
            }

            var total = remoteState.Exists ? remoteState.Size : 0;

            var localTarget = filePath;

            FTPConflictAction? action = null;

            if (File.Exists(localTarget))
            {
                var decision = ResolveConflict(FTPTransferDirection.Download, localTarget, fileName, remoteState, total, options);

                if (decision.Action == FTPConflictAction.Skip)
                {
                    return BuildResult(FTPTransferStatus.Skipped, localTarget, fileName, 0, 0, 1, false, decision.Action, null);
                }

                action = decision.Action;

                if (decision.Action == FTPConflictAction.Rename)
                {
                    localTarget = ResolveRenamedLocalPath(localTarget, decision.NewName, options);
                }
            }

            var sessionCount = total > 0 && !remoteState.Unknown ? ComputeSessionCount(total, options) : 1;

            if (sessionCount > 1)
            {
                var parallel = DownloadParallel(fileName, localTarget, total, sessionCount, options, downing, action, remoteState);

                if (parallel != null) return parallel;

                DropControl();
            }

            return DownloadSingle(fileName, localTarget, total, options, downing, action, remoteState);
        }

        FTPTransferResult DownloadSingle(string remotePath, string localTarget, long total, FTPTransferOptions options, Action<long, long> callback, FTPConflictAction? action, RemoteFileState remoteState)
        {
            var progress = new ProgressTracker(total, callback);

            var dataTimeout = options.ResolveDataTimeout(_config);

            var commandTimeout = options.ResolveCommandTimeout(_config);

            var bufferSize = options.ResolveBufferSize(_config);

            var adaptive = new AdaptiveBuffer(bufferSize, options);

            string metaPath;

            var temp = GetDownloadTempPath(localTarget, options, out metaPath);

            long offset = 0;

            var resumed = false;

            var attempts = 0;

            if (options.EnableResumeAcrossCalls && File.Exists(temp) && metaPath != null)
            {
                var valid = false;

                var meta = TransferMetaStore.Load(metaPath);

                if (meta != null && meta.ContainsKey("remoteSize"))
                {
                    long knownSize;
                    long knownTicks;

                    if (long.TryParse(meta["remoteSize"], out knownSize) &&
                        (!remoteState.Modified.HasValue || !long.TryParse(GetValue(meta, "remoteTicks"), out knownTicks) || knownTicks == remoteState.Modified.Value.Ticks) &&
                        (total <= 0 || knownSize == total))
                    {
                        valid = true;
                    }
                }

                if (valid)
                {
                    try
                    {
                        offset = new FileInfo(temp).Length;
                    }
                    catch
                    {
                        offset = 0;
                    }

                    if (offset > total && total > 0) offset = 0;
                }
                else
                {
                    TryDelete(temp);
                    TryDelete(metaPath);
                }
            }

            try
            {
                if (offset == 0)
                {
                    using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) { }
                }
            }
            catch
            {
                return BuildResult(FTPTransferStatus.Failed, localTarget, remotePath, 0, 0, 1, false, action, null);
            }

            if (metaPath != null)
            {
                var meta = new Dictionary<string, string>();
                meta["remoteSize"] = total.ToString();
                if (remoteState.Modified.HasValue) meta["remoteTicks"] = remoteState.Modified.Value.Ticks.ToString();
                TransferMetaStore.Save(metaPath, meta);
            }

            for (int attempt = 1; ; attempt++)
            {
                attempts = attempt;

                try
                {
                    EnsureConnected();

                    var conn = _control;

                    conn.DataTimeout = dataTimeout;

                    conn.CommandTimeout = commandTimeout;

                    offset = File.Exists(temp) ? new FileInfo(temp).Length : 0;

                    if (total > 0 && offset > total)
                    {
                        using (var truncate = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) { }

                        offset = 0;
                    }

                    // 同一次调用内的重试：校验远端文件未变化，变化则从 0 重新下载
                    if (offset > 0 && remoteState.Exists && total > 0)
                    {
                        var current = QueryRemote(conn, remotePath);

                        var changed = current.Unknown || !current.Exists ||
                            current.Size != remoteState.Size ||
                            (current.Modified.HasValue && remoteState.Modified.HasValue && current.Modified.Value != remoteState.Modified.Value);

                        if (changed)
                        {
                            using (var truncate = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) { }

                            offset = 0;

                            if (current.Exists) total = current.Size;
                        }
                    }

                    if (total > 0 && offset == total)
                    {
                        ReplaceLocalFile(temp, localTarget);

                        if (metaPath != null) TransferMetaStore.Remove(metaPath);

                        progress.Report(true);

                        return BuildResult(FTPTransferStatus.Completed, localTarget, remotePath, total, attempt, 1, offset > 0, action, null);
                    }

                    if (offset > 0)
                    {
                        var rest = conn.Send(FTPCommand.REST + " " + offset);

                        if (rest.Code != ServerResponseCode.文件行为暂停)
                        {
                            using (var truncate = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) { }

                            offset = 0;
                        }
                    }

                    if (offset > 0) resumed = true;

                    progress.Set(offset);

                    var received = offset;

                    using (var data = conn.OpenPassive(dataTimeout))
                    {
                        var reply = conn.Send(FTPCommand.RETR + " " + remotePath);

                        if (reply.Code != ServerResponseCode.打开连接 && reply.Code != ServerResponseCode.打开数据连接开始传输)
                        {
                            throw new FTPCommandException(reply.Code, reply.Message);
                        }

                        var buffer = adaptive.Current;

                        using (var fs = new FileStream(temp, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
                        {
                            fs.Seek(offset, SeekOrigin.Begin);

                            var stream = data.GetStream();

                            while (true)
                            {
                                var count = stream.Read(buffer, 0, buffer.Length);

                                if (count <= 0) break;

                                fs.Write(buffer, 0, count);

                                received += count;

                                progress.Add(count);

                                adaptive.Add(count);

                                buffer = adaptive.Current;
                            }

                            fs.Flush();
                        }

                        try { data.Client.Shutdown(SocketShutdown.Send); } catch { }
                    }

                    var final = conn.ReadReply();

                    if (final.Code != ServerResponseCode.结束数据连接 && final.Code != ServerResponseCode.文件行为完成)
                    {
                        throw new FTPCommandException(final.Code, final.Message);
                    }

                    if (total > 0 && received != total)
                    {
                        throw new IOException("Download incomplete: expected " + total + " bytes, got " + received + ".");
                    }

                    ReplaceLocalFile(temp, localTarget);

                    if (metaPath != null) TransferMetaStore.Remove(metaPath);

                    progress.Report(true);

                    return BuildResult(FTPTransferStatus.Completed, localTarget, remotePath, received, attempt, 1, resumed || offset > 0, action, null);
                }
                catch (Exception ex)
                {
                    if (attempt > options.MaxRetries || !IsRetryable(ex))
                    {
                        if (!options.KeepPartialOnFailure)
                        {
                            TryDelete(temp);
                            if (metaPath != null) TransferMetaStore.Remove(metaPath);
                        }

                        return BuildResult(FTPTransferStatus.Failed, localTarget, remotePath, 0, attempt, 1, resumed, action, ex);
                    }

                    adaptive.Rollback();

                    DropControl();

                    SleepBackoff(options, attempt);
                }
            }
        }

        FTPTransferResult DownloadParallel(string remotePath, string localTarget, long total, int sessionCount, FTPTransferOptions options, Action<long, long> callback, FTPConflictAction? action, RemoteFileState remoteState)
        {
            var dataTimeout = options.ResolveDataTimeout(_config);

            var bufferSize = options.ResolveBufferSize(_config);

            string ignoredMeta;

            var temp = GetDownloadTempPath(localTarget, new FTPTransferOptions() { EnableResumeAcrossCalls = false }, out ignoredMeta);

            try
            {
                using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    fs.SetLength(total);
                }
            }
            catch
            {
                return null;
            }

            var sessions = new FtpControlConnection[sessionCount];

            var extras = new List<FtpControlConnection>();

            sessions[0] = _control;

            try
            {
                for (int i = 1; i < sessionCount; i++)
                {
                    var session = CreateSession(dataTimeout);

                    extras.Add(session);

                    sessions[i] = session;
                }
            }
            catch
            {
                DisposeAll(extras);

                TryDelete(temp);

                return null;
            }

            var tracker = new ProgressTracker(total, callback);

            var state = new ParallelState();

            var segmentSize = (long)Math.Ceiling((double)total / sessionCount);

            var tasks = new Task[sessionCount];

            for (int i = 0; i < sessionCount; i++)
            {
                var index = i;

                var offset = (long)index * segmentSize;

                var length = Math.Min(segmentSize, total - offset);

                if (length <= 0) continue;

                tasks[i] = Task.Factory.StartNew(() =>
                {
                    DownloadSegment(sessions[index], index == 0, remotePath, temp, offset, length, bufferSize, dataTimeout, options, tracker, state);
                }, TaskCreationOptions.LongRunning);
            }

            try
            {
                Task.WaitAll(tasks);
            }
            catch (AggregateException)
            {
            }

            if (state.Failed)
            {
                DisposeAll(extras);

                TryDelete(temp);

                return null;
            }

            try
            {
                ReplaceLocalFile(temp, localTarget);
            }
            catch
            {
                DisposeAll(extras);

                TryDelete(temp);

                return null;
            }

            DisposeAll(extras);

            tracker.Report(true);

            return BuildResult(FTPTransferStatus.Completed, localTarget, remotePath, total, 1, sessionCount, false, action, null);
        }

        void DownloadSegment(FtpControlConnection initialSession, bool useMainSession, string remotePath, string temp, long offset, long length, int bufferSize, TimeSpan dataTimeout, FTPTransferOptions options, ProgressTracker tracker, ParallelState state)
        {
            var session = initialSession;

            var adaptive = new AdaptiveBuffer(bufferSize, options);

            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    if (state.Failed) return;

                    if (!session.Connected)
                    {
                        if (useMainSession)
                        {
                            DropControl();
                            EnsureConnected();
                            session = _control;
                        }
                        else
                        {
                            session.Dispose();
                            session = CreateSession(dataTimeout);
                        }
                    }

                    session.DataTimeout = dataTimeout;

                    session.CommandTimeout = options.ResolveCommandTimeout(_config);

                    var rest = session.Send(FTPCommand.REST + " " + offset);

                    if (rest.Code != ServerResponseCode.文件行为暂停)
                    {
                        throw new FTPCommandException(rest.Code, rest.Message);
                    }

                    using (var data = session.OpenPassive(dataTimeout))
                    {
                        var reply = session.Send(FTPCommand.RETR + " " + remotePath);

                        if (reply.Code != ServerResponseCode.打开连接 && reply.Code != ServerResponseCode.打开数据连接开始传输)
                        {
                            throw new FTPCommandException(reply.Code, reply.Message);
                        }

                        var buffer = adaptive.Current;

                        using (var fs = new FileStream(temp, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
                        {
                            fs.Seek(offset, SeekOrigin.Begin);

                            var stream = data.GetStream();

                            var remaining = length;

                            while (remaining > 0)
                            {
                                if (state.Failed) throw new IOException("Parallel download aborted.");

                                var want = (int)Math.Min(buffer.Length, remaining);

                                var count = stream.Read(buffer, 0, want);

                                if (count <= 0) throw new IOException("Data connection closed before segment completed.");

                                fs.Write(buffer, 0, count);

                                remaining -= count;

                                tracker.Add(count);

                                adaptive.Add(count);

                                buffer = adaptive.Current;
                            }

                            fs.Flush();
                        }

                        // 分片边界：读满后主动关闭数据连接
                        try { data.Close(); } catch { }
                    }

                    // 服务器可能回复 226（正常）或 426（检测到客户端提前关闭），都视为该分片成功
                    try
                    {
                        var final = session.ReadReply();

                        if (final.Code != ServerResponseCode.结束数据连接 &&
                            final.Code != ServerResponseCode.文件行为完成 &&
                            final.Code != ServerResponseCode.结束连接)
                        {
                            throw new FTPCommandException(final.Code, final.Message);
                        }
                    }
                    catch (FTPCommandException)
                    {
                        throw;
                    }
                    catch
                    {
                    }

                    return;
                }
                catch (Exception ex)
                {
                    if (attempt > options.MaxRetries || !IsRetryable(ex))
                    {
                        state.Fail(ex);

                        return;
                    }

                    adaptive.Rollback();

                    SleepBackoff(options, attempt);

                    if (!useMainSession)
                    {
                        try { session.Dispose(); } catch { }

                        try
                        {
                            session = CreateSession(dataTimeout);
                        }
                        catch (Exception createEx)
                        {
                            state.Fail(createEx);
                            return;
                        }
                    }
                }
            }
        }

        #endregion

        #region 公共辅助

        sealed class RemoteFileState
        {
            public bool Exists;
            public bool Unknown;
            public long Size;
            public DateTime? Modified;
        }

        sealed class UploadMeta
        {
            public long LocalLength;
            public long LocalWriteTicks;
            public long Sent;
        }

        sealed class AdaptiveBuffer
        {
            readonly bool _enabled;

            readonly int _min;

            readonly int _max;

            int _current;

            long _moved;

            byte[] _buffer;

            public AdaptiveBuffer(int initial, FTPTransferOptions options)
            {
                _min = options != null && options.MinBufferSize > 0 ? options.MinBufferSize : 16 * 1024;

                _max = options != null && options.MaxBufferSize > 0 ? options.MaxBufferSize : 1024 * 1024;

                _enabled = options == null || options.EnableAdaptiveBuffer;

                _current = initial;

                if (_current < _min) _current = _min;

                if (_current > _max) _current = _max;
            }

            public byte[] Current
            {
                get
                {
                    if (_buffer == null || _buffer.Length != _current)
                    {
                        _buffer = new byte[_current];
                    }

                    return _buffer;
                }
            }

            public int Size
            {
                get { return _current; }
            }

            public void Add(long bytes)
            {
                if (!_enabled || _current >= _max || bytes <= 0) return;

                _moved += bytes;

                if (_moved < 4L * 1024 * 1024) return;

                _moved = 0;

                var next = _current * 2;

                if (next > _max) next = _max;

                _current = next;
            }

            public void Rollback()
            {
                if (!_enabled) return;

                _moved = 0;

                var next = _current / 2;

                if (next < _min) next = _min;

                _current = next;
            }
        }

        sealed class ProgressTracker
        {
            long _done;
            readonly long _total;
            readonly Action<long, long> _callback;
            int _lastTick;
            int _reporting;

            public ProgressTracker(long total, Action<long, long> callback)
            {
                _total = total;
                _callback = callback;
                _lastTick = Environment.TickCount;
            }

            public void Set(long value)
            {
                Interlocked.Exchange(ref _done, value);
                Report(false);
            }

            public void Add(long value)
            {
                Interlocked.Add(ref _done, value);
                Report(false);
            }

            public void Report(bool force)
            {
                var callback = _callback;
                if (callback == null) return;

                var now = Environment.TickCount;

                if (!force && unchecked(now - _lastTick) < 150) return;

                if (Interlocked.CompareExchange(ref _reporting, 1, 0) != 0) return;

                try
                {
                    _lastTick = now;

                    callback(Interlocked.Read(ref _done), _total);
                }
                catch
                {
                }
                finally
                {
                    Interlocked.Exchange(ref _reporting, 0);
                }
            }
        }

        sealed class ParallelState
        {
            int _failed;

            public Exception Error { get; private set; }

            public bool Failed
            {
                get { return Volatile.Read(ref _failed) != 0; }
            }

            public void Fail(Exception ex)
            {
                if (Interlocked.CompareExchange(ref _failed, 1, 0) == 0)
                {
                    Error = ex;
                }
            }
        }

        static class TransferMetaStore
        {
            static readonly string Root = Path.Combine(Path.GetTempPath(), "SAEA.FTP");

            public static string GetPath(string key)
            {
                try { Directory.CreateDirectory(Root); } catch { }

                return Path.Combine(Root, Hash(key) + ".meta");
            }

            static string Hash(string value)
            {
                uint hash = 2166136261;

                foreach (var ch in value ?? string.Empty)
                {
                    hash ^= ch;
                    hash *= 16777619;
                }

                return hash.ToString("x8");
            }

            public static Dictionary<string, string> Load(string path)
            {
                try
                {
                    if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

                    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var line in File.ReadAllLines(path))
                    {
                        var index = line.IndexOf('=');

                        if (index <= 0) continue;

                        result[line.Substring(0, index)] = line.Substring(index + 1);
                    }

                    return result;
                }
                catch
                {
                    return null;
                }
            }

            public static void Save(string path, Dictionary<string, string> values)
            {
                try
                {
                    if (string.IsNullOrEmpty(path) || values == null) return;

                    var dir = Path.GetDirectoryName(path);

                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                    var builder = new StringBuilder();

                    foreach (var item in values)
                    {
                        builder.AppendLine(item.Key + "=" + item.Value);
                    }

                    File.WriteAllText(path, builder.ToString());
                }
                catch
                {
                }
            }

            public static void Remove(string path)
            {
                try
                {
                    if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path);
                }
                catch
                {
                }
            }
        }

        static class UploadMetaStore
        {
            public static UploadMeta Load(string path)
            {
                var values = TransferMetaStore.Load(path);

                if (values == null) return null;

                long localLength;
                long localWriteTicks;
                long sent;

                if (!long.TryParse(GetValue(values, "localLength"), out localLength)) return null;
                if (!long.TryParse(GetValue(values, "localWriteTicks"), out localWriteTicks)) return null;
                if (!long.TryParse(GetValue(values, "sent"), out sent)) return null;

                return new UploadMeta()
                {
                    LocalLength = localLength,
                    LocalWriteTicks = localWriteTicks,
                    Sent = sent
                };
            }

            public static void Save(string path, UploadMeta meta)
            {
                if (meta == null) return;

                TransferMetaStore.Save(path, new Dictionary<string, string>()
                {
                    { "localLength", meta.LocalLength.ToString() },
                    { "localWriteTicks", meta.LocalWriteTicks.ToString() },
                    { "sent", meta.Sent.ToString() }
                });
            }

            public static void Remove(string path)
            {
                TransferMetaStore.Remove(path);
            }
        }

        string GetUploadMetaPath(string remotePath)
        {
            return TransferMetaStore.GetPath("upload|" + _config.IP + "|" + _config.Port + "|" + _config.UserName + "|" + remotePath);
        }

        RemoteFileState QueryRemote(FtpControlConnection conn, string remotePath)
        {
            var state = new RemoteFileState();

            try
            {
                var reply = conn.Send(FTPCommand.SIZE + " " + remotePath);

                if (reply.Code == ServerResponseCode.文件状态回复)
                {
                    long size;

                    if (long.TryParse((reply.Message ?? string.Empty).Trim(), out size))
                    {
                        state.Exists = true;
                        state.Size = size;
                    }
                    else
                    {
                        state.Unknown = true;
                    }

                    return state;
                }

                if (reply.Code == ServerResponseCode.找不到文件或文件夹)
                {
                    state.Exists = false;

                    return state;
                }

                state.Unknown = true;
            }
            catch (FTPCommandException ex)
            {
                if (ex.Code == ServerResponseCode.找不到文件或文件夹)
                {
                    state.Exists = false;

                    return state;
                }

                state.Unknown = true;
            }
            catch
            {
                state.Unknown = true;
            }

            try
            {
                string dir;
                string name;

                SplitRemotePath(remotePath, out dir, out name);

                var names = ListNames(conn, string.IsNullOrEmpty(dir) ? "/" : dir);

                if (names != null)
                {
                    state.Unknown = false;

                    foreach (var item in names)
                    {
                        if (string.Equals(item, name, StringComparison.OrdinalIgnoreCase))
                        {
                            state.Exists = true;

                            break;
                        }
                    }
                }
            }
            catch
            {
            }

            if (state.Exists)
            {
                try
                {
                    var mdtm = conn.Send(FTPCommand.MDTM + " " + remotePath);

                    if (mdtm.Code == ServerResponseCode.文件状态回复)
                    {
                        DateTime parsed;

                        if (DateTime.TryParseExact((mdtm.Message ?? string.Empty).Trim(), "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out parsed))
                        {
                            state.Modified = parsed;
                        }
                    }
                }
                catch
                {
                }
            }

            return state;
        }

        List<string> ListNames(FtpControlConnection conn, string dir)
        {
            var data = conn.OpenPassive(conn.DataTimeout);

            var reply = conn.Send(FTPCommand.MLSD + " " + dir);

            if (reply.Code != ServerResponseCode.打开连接 && reply.Code != ServerResponseCode.打开数据连接开始传输)
            {
                try { data.Close(); } catch { }

                if (reply.Code == ServerResponseCode.找不到文件或文件夹) return null;

                throw new FTPCommandException(reply.Code, reply.Message);
            }

            var bytes = new List<byte>();

            var buffer = new byte[8192];

            try
            {
                var stream = data.GetStream();

                while (true)
                {
                    var count = stream.Read(buffer, 0, buffer.Length);

                    if (count <= 0) break;

                    for (int i = 0; i < count; i++) bytes.Add(buffer[i]);
                }
            }
            finally
            {
                try { data.Close(); } catch { }
            }

            try { conn.ReadReply(); } catch { }

            var names = new List<string>();

            var text = (_config.Encoding ?? Encoding.UTF8).GetString(bytes.ToArray());

            foreach (var line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var index = line.LastIndexOf(';');

                var name = index > -1 ? line.Substring(index + 1).Trim() : line.Trim();

                if (name.Length > 0) names.Add(name);
            }

            return names;
        }

        FTPConflictResult ResolveConflict(FTPTransferDirection direction, string localPath, string remotePath, RemoteFileState remote, long incomingSize, FTPTransferOptions options)
        {
            FTPConflictResult result = null;

            if (options.ConflictHandler != null)
            {
                var context = new FTPConflictContext()
                {
                    Direction = direction,
                    LocalPath = localPath,
                    RemotePath = remotePath,
                    TargetExists = remote != null && remote.Exists,
                    ExistsUnknown = remote != null && remote.Unknown,
                    LocalSize = direction == FTPTransferDirection.Download && File.Exists(localPath) ? (long?)new FileInfo(localPath).Length : null,
                    RemoteSize = remote != null && remote.Exists ? (long?)remote.Size : null,
                    RemoteModified = remote != null ? remote.Modified : null,
                    IncomingSize = incomingSize
                };

                try
                {
                    result = options.ConflictHandler(context);
                }
                catch (Exception ex)
                {
                    throw new FTPLocalException("Conflict handler failed.", ex);
                }
            }

            if (result == null)
            {
                result = new FTPConflictResult()
                {
                    Action = options.DefaultConflictAction
                };
            }

            return result;
        }

        string ResolveRenamedRemotePath(FtpControlConnection conn, string remotePath, string newName, FTPTransferOptions options)
        {
            string dir;
            string name;

            SplitRemotePath(remotePath, out dir, out name);

            if (!string.IsNullOrEmpty(newName))
            {
                var candidate = CombineRemotePath(dir, newName);

                if (!QueryRemote(conn, candidate).Exists) return candidate;
            }

            for (int i = 1; i < 1000; i++)
            {
                var candidate = BuildRenamedPath(remotePath, options.RenamePattern, i, false);

                if (!QueryRemote(conn, candidate).Exists) return candidate;
            }

            throw new IOException("Unable to find an available remote file name.");
        }

        static string ResolveRenamedLocalPath(string localPath, string newName, FTPTransferOptions options)
        {
            var dir = Path.GetDirectoryName(localPath) ?? string.Empty;

            if (!string.IsNullOrEmpty(newName))
            {
                var candidate = string.IsNullOrEmpty(dir) ? newName : Path.Combine(dir, newName);

                if (!File.Exists(candidate)) return candidate;
            }

            for (int i = 1; i < 1000; i++)
            {
                var candidate = BuildRenamedPath(localPath, options.RenamePattern, i, true);

                if (!File.Exists(candidate)) return candidate;
            }

            throw new IOException("Unable to find an available local file name.");
        }

        static string GetDownloadTempPath(string localTarget, FTPTransferOptions options, out string metaPath)
        {
            var dir = Path.GetDirectoryName(localTarget);

            if (string.IsNullOrEmpty(dir)) dir = ".";

            var name = Path.GetFileName(localTarget);

            if (options.EnableResumeAcrossCalls)
            {
                var temp = Path.Combine(dir, "." + name + ".saea-part");

                metaPath = temp + ".meta";

                return temp;
            }

            metaPath = null;

            return Path.Combine(dir, "." + name + ".saea-part-" + Guid.NewGuid().ToString("N"));
        }

        static void ReplaceLocalFile(string temp, string target)
        {
            if (File.Exists(target))
            {
                try
                {
                    File.Replace(temp, target, null);

                    return;
                }
                catch
                {
                }

                try { File.Delete(target); } catch { }
            }

            File.Move(temp, target);
        }

        static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path);
            }
            catch
            {
            }
        }

        static void DisposeAll(List<FtpControlConnection> sessions)
        {
            if (sessions == null) return;

            foreach (var session in sessions)
            {
                try { session.Dispose(); } catch { }
            }
        }

        static int ComputeSessionCount(long size, FTPTransferOptions options)
        {
            if (options == null || !options.EnableParallel) return 1;

            if (size < options.ParallelThresholdBytes) return 1;

            var max = Math.Max(1, options.MaxSessions);

            var min = Math.Max(1, options.MinSegmentSize);

            var byMin = (size + min - 1) / min;

            return (int)Math.Min(max, Math.Max(1, byMin));
        }

        static bool IsRenamePolicy(string policy)
        {
            return !string.IsNullOrEmpty(policy) && string.Equals(policy.Trim(), "Rename", StringComparison.OrdinalIgnoreCase);
        }

        static string ParseNewName(string message)
        {
            if (string.IsNullOrEmpty(message)) return null;

            var index = message.IndexOf("NEWNAME=", StringComparison.OrdinalIgnoreCase);

            if (index < 0) return null;

            var start = index + "NEWNAME=".Length;

            // 服务端把 NEWNAME 放在回复末尾，文件名允许包含空格
            return message.Substring(start).Trim(new[] { ' ', '\t', '\r', '\n' });
        }

        static string GetValue(Dictionary<string, string> values, string key)
        {
            string value;

            if (values != null && values.TryGetValue(key, out value)) return value;

            return null;
        }

        static bool IsRetryable(Exception ex)
        {
            if (ex == null) return false;

            var ftp = ex as FTPCommandException;

            if (ftp != null)
            {
                switch (ftp.Code)
                {
                    case 421:
                    case 425:
                    case 426:
                    case 450:
                    case 451:
                        return true;
                    default:
                        return false;
                }
            }

            if (ex is FTPLocalException) return false;

            if (ex is AggregateException)
            {
                foreach (var inner in ((AggregateException)ex).InnerExceptions)
                {
                    if (!IsRetryable(inner)) return false;
                }

                return true;
            }

            if (ex is IOException) return true;

            if (ex is SocketException) return true;

            if (ex is TimeoutException) return true;

            if (ex is ObjectDisposedException) return true;

            return false;
        }

        static void SleepBackoff(FTPTransferOptions options, int attempt)
        {
            var baseMs = options != null && options.RetryDelay > TimeSpan.Zero ? options.RetryDelay.TotalMilliseconds : 500;

            if (baseMs <= 0) baseMs = 500;

            var delay = Math.Min(10000.0, baseMs * Math.Pow(2, Math.Max(0, attempt - 1)));

            var jitter = Math.Abs(unchecked(Environment.TickCount * 31 + attempt)) % 100;

            Thread.Sleep((int)delay + jitter);
        }

        static FTPTransferResult BuildResult(FTPTransferStatus status, string localPath, string remotePath, long bytes, int attempts, int sessions, bool resumed, FTPConflictAction? action, Exception error)
        {
            return new FTPTransferResult()
            {
                Status = status,
                LocalPath = localPath,
                RemotePath = remotePath,
                BytesTransferred = bytes,
                Attempts = attempts,
                SessionsUsed = sessions,
                Resumed = resumed,
                ConflictAction = action,
                Error = error
            };
        }

        #endregion

        #region 生命周期

        public void Quit()
        {
            var control = _control;

            if (control != null && control.Connected)
            {
                try { control.Send(FTPCommand.QUIT.ToString()); } catch { }

                try { control.Close(); } catch { }
            }

            DropControl();

            var handler = Ondisconnected;

            if (handler != null)
            {
                try { handler(); } catch { }
            }

            _disposed = true;
        }

        public void Dispose()
        {
            if (_disposed) return;

            try { Quit(); } catch { }
        }

        #endregion
    }
}