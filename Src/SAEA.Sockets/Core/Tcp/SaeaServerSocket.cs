/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Sockets.Core.Tcp
*文件名： SaeaServerSocket
*版本号： v26.4.23.1
*唯一标识：428f30fc-7c1c-4a82-a168-5af3b03cc7fc
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/01/23 11:16:53
*描述：SaeaServerSocket接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 17:33:09
*修改人： yswenli
*版本号： v26.11.5.1
*描述：接收提交时的 ObjectDisposedException 单独分支处理，按信息级记录关闭竞态
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Sockets.Base;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using SAEA.Sockets.Ssl;
using SAEA.Sockets.Ssl.Internal;

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets.Core.Tcp
{
    /// <summary>
    /// saea 服务器 socket
    /// 支持使用自定义 IContext 来扩展
    /// </summary>
    public class SaeaServerSocket : IServerSocket, IServerConnectionProvider, IDisposable
    {
        // 监听Socket
        Socket _listener = null;

        // 客户端连接数量
        int _clientCounts;

        // 会话管理器
        private SessionManager _sessionManager;

        // 每服务器独立的通道管理器
        private readonly ChannelManager _channelManager;

        // 每连接的流 / TLS 门面
        private readonly ConcurrentDictionary<string, SaeaConnection> _connections =
            new ConcurrentDictionary<string, SaeaConnection>(StringComparer.Ordinal);

        // 是否启用流模式
        private readonly bool _useStream;

        // 是否启用 TLS
        private readonly bool _tlsEnabled;

        // 连接建立钩子：通道登记完成后、通知 OnAccepted 之前同步调用
        private readonly Action<ChannelInfo> _connectionOpened;

        // 输入处理器：接管该连接的读循环
        private readonly Func<IUserToken, PipeReader, IReceiveFlowControl, CancellationToken, Task> _inputHandler;

        // 输入处理器的取消源，按会话 ID 索引
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _pipelineCts =
            new ConcurrentDictionary<string, CancellationTokenSource>(StringComparer.Ordinal);

        // 获取会话管理器
        /// <summary>
        /// 会话管理器，负责令牌池、会话缓存与空闲超时清理
        /// </summary>
        public SessionManager SessionManager
        {
            get { return _sessionManager; }
        }

        /// <summary>
        /// 获取本服务器的通道管理器（每服务器独立实例）。
        /// </summary>
        public ChannelManager ChannelManager
        {
            get { return _channelManager; }
        }

        // 是否已释放资源
        /// <summary>
        /// 是否已释放资源
        /// </summary>
        public bool IsDisposed
        {
            get; set;
        } = false;

        // 获取客户端连接数量
        /// <summary>
        /// 当前已连接的客户端数量，随连接建立与释放增减
        /// </summary>
        public int ClientCounts { get => _clientCounts; private set => _clientCounts = value; }

        // Socket选项
        /// <summary>
        /// 本服务器使用的套接字选项
        /// </summary>
        public ISocketOption SocketOption { get; set; }

        /// <summary>
        /// 监听套接字实际绑定的端点；未启动或已释放时为 null
        /// </summary>
        public IPEndPoint LocalEndPoint
        {
            get
            {
                try
                {
                    return _listener?.LocalEndPoint as IPEndPoint;
                }
                catch
                {
                    return null;
                }
            }
        }

        #region events

        // 客户端连接事件
        /// <summary>
        /// 客户端连接建立后触发，参数为通道对象（流模式为 ChannelInfo，否则为会话令牌）
        /// </summary>
        public event OnAcceptedHandler OnAccepted;

        // 错误事件
        /// <summary>
        /// 收发或内部处理异常时触发，参数为会话标识与异常
        /// </summary>
        public event OnErrorHandler OnError;

        // 客户端断开连接事件
        /// <summary>
        /// 客户端断开后触发，参数为会话标识与导致断开的异常（正常断开为 null）
        /// </summary>
        public event OnDisconnectedHandler OnDisconnected;

        /// <summary>
        /// 接收数据事件（Span 版本）。data 仅在回调期间有效。
        /// </summary>
        public event OnServerReceiveSpanHandler OnServerReceiveSpan;

        #endregion

        /// <summary>
        /// saea 服务器 socket
        /// </summary>>
        /// <param name="socketOption"></param>
        public SaeaServerSocket(ISocketOption socketOption)
        {
            if (socketOption == null) throw new ArgumentNullException(nameof(socketOption));

            // Stream / TLS 模式可能未设置上下文；补齐默认上下文，避免令牌池初始化失败。
            if (socketOption.Context == null)
                socketOption.Context = new BaseContext<BaseCoder>();

            // 初始化会话管理器
            _sessionManager = new SessionManager(socketOption.Context,
                socketOption.ReadBufferSize,
                socketOption.MaxConnects, IO_Completed,
                new TimeSpan(0, 0, 0, 0, socketOption.FreeTime));
            _sessionManager.OnTimeOut += _sessionManager_OnTimeOut;
            SocketOption = socketOption;

            _useStream = socketOption.UseStream;
            _tlsEnabled = socketOption.Tls != null && socketOption.Tls.Enabled;
            _channelManager = new ChannelManager(Math.Max(1, socketOption.FreeTime / 1000));

            var pipelineOption = socketOption as IPipelineSocketOption;
            if (pipelineOption != null)
            {
                _connectionOpened = pipelineOption.ConnectionOpened;
                _inputHandler = pipelineOption.InputHandler;
            }
        }

        /// <summary>
        /// 启动服务
        /// </summary>
        /// <param name="backlog"></param>
        public void Start(int backlog = 10 * 1000)
        {
            if (_listener == null)
            {
                IPEndPoint ipEndPoint;

                if (SocketOption.UseIPV6)
                {
                    _listener = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
                    if (string.IsNullOrEmpty(SocketOption.IP))
                        ipEndPoint = (new IPEndPoint(IPAddress.IPv6Any, SocketOption.Port));
                    else
                        ipEndPoint = (new IPEndPoint(IPAddress.Parse(SocketOption.IP), SocketOption.Port));

                    // 只在绑定 IPv6Any 时启用双栈，指定具体地址时保持单栈语义
                    if (SocketOption.DualMode && string.IsNullOrEmpty(SocketOption.IP))
                        _listener.DualMode = true;
                }
                else
                {
                    _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    if (string.IsNullOrEmpty(SocketOption.IP))
                        ipEndPoint = (new IPEndPoint(IPAddress.Any, SocketOption.Port));
                    else
                        ipEndPoint = (new IPEndPoint(IPAddress.Parse(SocketOption.IP), SocketOption.Port));
                }
                if (SocketOption.ReusePort)
                    _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, SocketOption.ReusePort);
                _listener.NoDelay = SocketOption.NoDelay;
                _listener.SendBufferSize = SocketOption.WriteBufferSize;
                _listener.ReceiveBufferSize = SocketOption.ReadBufferSize;
                _listener.Bind(ipEndPoint);
                _listener.Listen(backlog);

                ProcessAccept(null);
            }
        }

        private void AccepteArgs_Completed(object sender, SocketAsyncEventArgs acceptArgs)
        {
            if (acceptArgs.LastOperation == SocketAsyncOperation.Accept)
            {
                ProcessAccepted(acceptArgs);
            }
        }

        private void ProcessAccept(SocketAsyncEventArgs acceptArgs)
        {
            if (acceptArgs == null)
            {
                acceptArgs = new SocketAsyncEventArgs();
                acceptArgs.Completed += new EventHandler<SocketAsyncEventArgs>(AccepteArgs_Completed);
            }
            else
            {
                acceptArgs.AcceptSocket = null;
            }
            try
            {
                if (!IsDisposed && _listener != null && acceptArgs.SocketError == SocketError.Success)
                {
                    if (!_listener.AcceptAsync(acceptArgs))
                        ProcessAccepted(acceptArgs);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke("SaeaServerSocket.ProcessAccept", ex);
            }
        }

        private void ProcessAccepted(SocketAsyncEventArgs acceptArgs)
        {
            // 提到 try 外：接入失败时要按是否已绑定令牌决定收口方式
            IUserToken userToken = null;
            try
            {
                var socket = acceptArgs.AcceptSocket;

                if (socket == null || socket.Connected == false)
                {
                    return;
                }

                socket.NoDelay = SocketOption.NoDelay;

                socket.ReceiveBufferSize = SocketOption.ReadBufferSize;

                socket.SendBufferSize = SocketOption.WriteBufferSize;

                socket.ReceiveTimeout = socket.SendTimeout = SocketOption.ActionTimeout;

                userToken = _sessionManager.BindUserToken(socket, SocketOption.ConnectTimeout);

                if (userToken == null)
                {
                    return;
                }

                // 令牌绑定成功即计入连接数：此后任何失败路径都经 Disconnect 收口，
                // 由同一次 Free 负责减计数，避免失败路径出现无配对的减计数。
                Interlocked.Increment(ref _clientCounts);

                // 绑定后立刻取一次会话标识：并发断开（读错误、管线收口）会经 Free 把 ID 置空，
                // 后续再读会拿到 null，字典键为 null 会抛 ArgumentNullException。
                var sessionId = userToken.ID;
                if (string.IsNullOrEmpty(sessionId))
                {
                    return;
                }

                var readArgs = userToken.ReadArgs;

                SaeaConnection connection = null;

                if (_useStream || _tlsEnabled)
                {
                    connection = new SaeaConnection(sessionId, socket, SocketOption,
                        (segment, owner) => SendAsyncRaw(userToken, segment, owner),
                        () => ResumeReceive(userToken));

                    _connections[sessionId] = connection;

                    if (_useStream && userToken is StreamUserToken streamToken)
                    {
                        streamToken.Stream = connection.Stream;
                        streamToken.Input = connection.Input;
                        streamToken.Connection = connection;
                    }

                    if (_tlsEnabled)
                    {
                        try
                        {
                            var tls = SaeaTlsTransport.CreateServer(SocketOption.Tls);
                            connection.AttachTls(tls);
                        }
                        catch (Exception ex)
                        {
                            SaeaConnection removed;
                            _connections.TryRemove(sessionId, out removed);
                            try { connection.Dispose(); } catch { }
                            OnError?.Invoke(sessionId, ex);
                            Disconnect(userToken, ex);
                            return;
                        }
                    }
                }

                ProcessReceive(readArgs);

                // 事件消费：由后台泵从 Pipe 读取解密后的数据。流模式与 TLS 事件模式共用该路径。
                // 配置了输入处理器时读循环由处理器接管，事件泵不再启动。
                if (connection != null && OnServerReceiveSpan != null && _inputHandler == null)
                    StartSpanPump(userToken, connection);

                if (_tlsEnabled && connection != null)
                {
                    // TLS 握手成功后才通知接入；失败则直接断开。
                    connection.HandshakeTask.ContinueWith(t =>
                    {
                        if (t.IsFaulted || t.IsCanceled)
                        {
                            var ex = GetHandshakeException(t);
                            OnError?.Invoke(userToken.ID, ex);
                            Disconnect(userToken, ex);
                            return;
                        }

                        try
                        {
                            RaiseAccepted(userToken, connection);
                            StartPipelineIfNeeded(userToken, connection);
                        }
                        catch (Exception ex)
                        {
                            OnError?.Invoke(userToken.ID, ex);
                            Disconnect(userToken, ex);
                        }
                    }, TaskContinuationOptions.ExecuteSynchronously);
                }
                else
                {
                    try
                    {
                        RaiseAccepted(userToken, connection);
                        StartPipelineIfNeeded(userToken, connection);
                    }
                    catch (Exception ex)
                    {
                        OnError?.Invoke(userToken.ID, ex);
                        Disconnect(userToken, ex);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke("SaeaServerSocket.ProcessAccepted", ex);

                // 接入失败不能只记日志：已绑定令牌的走统一收口（计数、缓存、OnDisconnected 保持一致），
                // 尚未绑定令牌的 socket 无人持有，必须在此关闭，否则句柄泄漏。
                if (userToken != null)
                {
                    try { Disconnect(userToken, ex); } catch { }
                }
                else
                {
                    try { acceptArgs.AcceptSocket?.Close(); } catch { }
                }
            }
            finally
            {
                ProcessAccept(acceptArgs);
            }
        }

        /// <summary>
        /// 触发 OnAccepted。流模式传 <see cref="ChannelInfo"/>，事件模式传 <see cref="IUserToken"/>。
        /// 连接建立钩子在通道登记完成后、通知 OnAccepted 之前同步执行。
        /// </summary>
        private void RaiseAccepted(IUserToken userToken, SaeaConnection connection)
        {
            var handler = OnAccepted;

            // 接入与并发断开交错时 ID 可能已被 Free 置空：此时连接已收口，
            // 再登记通道会用 null 键写入字典并抛异常。
            var sessionId = userToken.ID;
            if (string.IsNullOrEmpty(sessionId)) return;

            if (_useStream && connection != null)
            {
                if (_connectionOpened == null && handler == null) return;

                var channel = _channelManager.Set(sessionId, userToken.Socket, connection.Stream, connection.Tls);
                if (channel == null) return;

                channel.UserToken = userToken;

                _connectionOpened?.Invoke(channel);
                handler?.Invoke(channel);
            }
            else
            {
                handler?.Invoke(userToken);
            }
        }

        /// <summary>
        /// 启动接管读循环的输入处理器。仅在配置了输入处理器且连接门面存在时启动。
        /// 处理器返回即让出连接所有权：不论正常返回、异常还是取消，都由框架在返回后断开连接，
        /// 应用侧不得再持有该会话的读写所有权。
        /// </summary>
        private void StartPipelineIfNeeded(IUserToken userToken, SaeaConnection connection)
        {
            if (_inputHandler == null || connection == null) return;

            var sessionId = userToken.ID;
            if (string.IsNullOrEmpty(sessionId)) return;

            var cts = new CancellationTokenSource();
            _pipelineCts[sessionId] = cts;

            // PipeReader 只允许一个读者：接管后 ChannelInfo.Stream / GetStream() 的读取会立即报错，
            // 防止应用侧再读时与链的接收泵争抢同一条入站字节流。
            connection.ClaimInput("输入处理器（UseInputHandler）");

            var flow = new ReceiveFlowControl(connection, () =>
            {
                var current = GetConnection(userToken);
                // 回调可能晚于令牌回收与复用：只处理令牌当前仍绑定本连接的情况，
                // 否则会把同一令牌对象上已绑定的新会话误断（对端端口复用时会话标识也可能相同）。
                if (ReferenceEquals(current, connection))
                    Disconnect(userToken);
            });
            var handler = _inputHandler;

            _ = Task.Run(async () =>
            {
                try
                {
                    await handler(userToken, connection.Input, flow, cts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(sessionId, ex);
                }
                finally
                {
                    // 只有本次处理器仍是登记的接管者才收口：断开路径会先摘除登记，据此判定为幂等兜底，
                    // 既不会重复触发 OnDisconnected，也不会在令牌入池复用后误断新连接。
                    // 按「键 + 值」原子摘除，避免同一会话标识的新一代登记被本次收尾误删。
                    var wasRegistered = ((ICollection<KeyValuePair<string, CancellationTokenSource>>)_pipelineCts)
                        .Remove(new KeyValuePair<string, CancellationTokenSource>(sessionId, cts));
                    cts.Dispose();

                    if (wasRegistered && ReferenceEquals(GetConnection(userToken), connection))
                    {
                        try { Disconnect(userToken); } catch { }
                    }
                }
            });
        }

        /// <summary>
        /// 取消该会话输入处理器的令牌。取消只置位，取消源由处理器自身的收口负责释放。
        /// </summary>
        private void CancelPipeline(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return;

            CancellationTokenSource cts;
            if (_pipelineCts.TryRemove(sessionId, out cts))
            {
                try { cts.Cancel(); } catch (ObjectDisposedException) { }
            }
        }

        private static Exception GetHandshakeException(Task task)
        {
            if (task.IsCanceled) return new SaeaTlsException("TLS 握手已取消。");
            var exception = task.Exception;
            return exception?.GetBaseException() ?? new SaeaTlsException("TLS 握手失败。");
        }

        private SaeaConnection GetConnection(IUserToken userToken)
        {
            if (userToken == null) return null;

            // ID 会被 SessionManager / UserTokenPool 与接收回调并发置空，只读一次局部副本：
            // 多次读取会让空判断与实际字典查找看到不同的值，null 键会让 ConcurrentDictionary 抛异常。
            var sessionId = userToken.ID;
            if (string.IsNullOrEmpty(sessionId)) return null;

            SaeaConnection connection;
            return _connections.TryGetValue(sessionId, out connection) ? connection : null;
        }

        /// <inheritdoc />
        public Stream GetStream(IUserToken userToken) => GetConnection(userToken)?.Stream;

        /// <inheritdoc />
        public ISaeaTlsContext GetTlsContext(IUserToken userToken) => GetConnection(userToken)?.Tls;

        /// <summary>
        /// 背压恢复入口。
        /// </summary>
        private void ResumeReceive(IUserToken userToken)
        {
            var readArgs = userToken?.ReadArgs;
            if (readArgs == null) return;
            if (userToken.Socket == null || !userToken.Socket.Connected) return;

            var connection = GetConnection(userToken);
            if (connection != null && connection.IsReceivePaused) return;

            ProcessReceive(readArgs);
        }

        /// <summary>
        /// 连接门面的事件消费泵：从 Pipe 读取解密后的明文并回调 <see cref="OnServerReceiveSpan"/>。
        /// 流模式与 TLS 事件模式共用该路径，且仅在有订阅者时启动。
        /// </summary>
        private void StartSpanPump(IUserToken userToken, SaeaConnection connection)
        {
            var reader = connection.Input;
            if (reader == null) return;

            Task.Run(async () =>
            {
                while (true)
                {
                    ReadResult result;
                    try
                    {
                        result = await reader.ReadAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        break;
                    }

                    var buffer = result.Buffer;
                    try
                    {
                        if (!buffer.IsEmpty)
                        {
                            foreach (var segment in buffer)
                            {
                                if (segment.Length == 0) continue;
                                try
                                {
                                    OnServerReceiveSpan?.Invoke(userToken, segment.Span);
                                }
                                catch (Exception ex)
                                {
                                    OnError?.Invoke(userToken?.ID ?? "", ex);
                                }
                            }
                        }
                    }
                    finally
                    {
                        reader.AdvanceTo(buffer.End);
                    }

                    if (result.IsCompleted) break;
                }
            });
        }

        /// <summary>
        /// TLS 模式下的应用数据发送。返回 true 表示已由 TLS 通道接管。
        /// </summary>
        private bool TrySendViaTls(IUserToken userToken, ReadOnlySpan<byte> data)
        {
            if (!_tlsEnabled) return false;

            var connection = GetConnection(userToken);
            if (connection == null || connection.Tls == null)
            {
                OnError?.Invoke(userToken?.ID ?? "", new SaeaTlsException("TLS 连接尚未就绪。"));
                return true;
            }

            connection.SendApplicationData(data).ContinueWith(
                t => OnError?.Invoke(userToken?.ID ?? "", t.Exception.GetBaseException()),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return true;
        }

        private void IO_Completed(object sender, SocketAsyncEventArgs e)
        {
            var userToken = (IUserToken)e.UserToken;
            try
            {
                switch (e.LastOperation)
                {
                    case SocketAsyncOperation.Receive:
                        ProcessReceived(e);
                        break;
                    case SocketAsyncOperation.Send:
                        ProcessSended(e);
                        break;
                    default:
                        Disconnect(userToken, new KernelException("Operation-exceptions，SocketAsyncOperation：" + e.LastOperation));
                        break;
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke("", ex);
                Disconnect(userToken, ex);
            }
        }

        /// <summary>
        /// 处理接收数据
        /// </summary>
        /// <param name="readArgs"></param>
        private void ProcessReceive(SocketAsyncEventArgs readArgs)
        {
            if (readArgs == null) return;
            var userToken = (IUserToken)readArgs.UserToken;
            try
            {
                if (userToken == null) return;
                if (userToken.Socket != null && userToken.Socket.Connected)
                {
                    lock (readArgs)
                    {
                        if (userToken.Socket != null && userToken.Socket.Connected)
                        {
                            bool willRaiseEvent;

                            // 回收已请求时不得再提交：关闭 socket 不会撤销已投递的接收，
                            // 复用时对同一 args 再次 SetBuffer/ReceiveAsync 会抛异常并丢掉令牌。
                            var baseToken = userToken as BaseUserToken;
                            if (baseToken != null && !baseToken.TryBeginOperation())
                            {
                                Disconnect(userToken, new KernelException("连接正在回收，停止接收。"));
                                return;
                            }

                            try
                            {
                                willRaiseEvent = userToken.Socket.ReceiveAsync(readArgs);
                            }
                            catch (ObjectDisposedException ex)
                            {
                                // 连接正在关闭时 socket 句柄已释放，提交接收必然失败：属预期竞态，按信息记录，
                                // 仍要断开并回滚在途计数，CompleteOperation 之后不再触碰令牌。
                                var tokenId = userToken.ID;
                                try
                                {
                                    LogHelper.Info($"接收提交时连接已关闭:{tokenId}/{ex.ObjectName}");
                                }
                                finally
                                {
                                    try { Disconnect(userToken, null); } catch { }
                                    _sessionManager.CompleteOperation(userToken);
                                }
                                return;
                            }
                            catch (InvalidOperationException ex)
                            {
                                // 入池会清空 ID：日志、回调与断开都用入池前的快照；
                                // 先断开再回滚在途计数，CompleteOperation 之后不再触碰令牌。
                                var tokenId = userToken.ID;
                                var kex = new KernelException("An exception occurs when receiving:" + ex.Message, ex);
                                try
                                {
                                    LogHelper.Error($"An exception occurs when receiving:{tokenId}", kex);
                                    OnError?.Invoke(tokenId, kex);
                                }
                                finally
                                {
                                    try { Disconnect(userToken, kex); } catch { }
                                    _sessionManager.CompleteOperation(userToken);
                                }
                                return;
                            }
                            catch (Exception ex)
                            {
                                // 入池会清空 ID：日志、回调与断开都用入池前的快照；
                                // 先断开再回滚在途计数，CompleteOperation 之后不再触碰令牌。
                                var tokenId = userToken.ID;
                                var kex = new KernelException("An exception occurs when receiving:" + ex.Message, ex);
                                try
                                {
                                    LogHelper.Error($"An exception occurs when receiving:{tokenId}", kex);
                                    OnError?.Invoke(tokenId, kex);
                                }
                                finally
                                {
                                    try { Disconnect(userToken, kex); } catch { }
                                    _sessionManager.CompleteOperation(userToken);
                                }
                                return;
                            }

                            if (!willRaiseEvent)
                            {
                                ThreadPool.QueueUserWorkItem((state) =>
                                {
                                    ProcessReceived(readArgs);
                                });
                            }
                        }
                        else
                        {
                            Disconnect(userToken, new KernelException("The remote client has been disconnected."));
                        }
                    }
                }
                else
                {
                    Disconnect(userToken, new KernelException("The remote client has been disconnected."));
                }
            }
            catch (Exception exp)
            {
                var kex = new KernelException("An exception occurs when a message is received:" + exp.Message, exp);
                LogHelper.Error($"An exception occurs when receiving:{userToken?.ID}", kex);
                OnError?.Invoke(userToken?.ID, kex);
            }
        }

        /// <summary>
        /// 处理接收到数据。配置了输入处理器（流模式）时，对端 FIN（0 字节且无错误）视为
        /// 「入站结束」而不是「连接断开」：只投递 <see cref="SaeaConnection.CompleteInput"/>，
        /// 让处理器读完 Pipe 剩余字节并看到结束标记，随后由处理器返回后的收口断开连接；
        /// 该阶段仍允许写方向继续发送。未配置输入处理器（事件/包模式）时 0 字节仍按断开处理。
        /// </summary>
        /// <param name="readArgs"></param>
        void ProcessReceived(SocketAsyncEventArgs readArgs)
        {
            try
            {
                if (readArgs == null || readArgs.UserToken == null) return;

                if (readArgs.BytesTransferred > 0 && readArgs.SocketError == SocketError.Success)
                {
                    var userToken = readArgs.UserToken as IUserToken;
                    var connection = GetConnection(userToken);
                    try
                    {
                        userToken.Actived = DateTimeHelper.Now;
                        var dataSpan = readArgs.Buffer.AsSpan().Slice(readArgs.Offset, readArgs.BytesTransferred);
                        _sessionManager.Active(userToken.ID);
                        if (_useStream) _channelManager.Refresh(userToken.ID);

                        if (connection != null)
                        {
                            // 流 / TLS 模式：原始字节交给连接门面（TLS 解密后写入 Pipe）。
                            connection.OnRawReceived(dataSpan);
                        }
                        else
                        {
                            OnServerReceiveSpan?.Invoke(userToken, dataSpan);
                        }
                    }
                    catch (Exception ex)
                    {
                        OnError?.Invoke(userToken?.ID ?? "", ex);
                        LogHelper.Error($"An exception occurs when receiving:{userToken?.ID}", ex);
                    }

                    // Pipe 背压暂停：等待 ResumeReceive 恢复。
                    if (connection != null && connection.IsReceivePaused) return;

                    ProcessReceive(readArgs);
                }
                else
                {
                    var eofToken = readArgs.UserToken as IUserToken;
                    var eofConnection = GetConnection(eofToken);

                    // 与接收回调并发时 ID 可能被置空，只读一次局部副本，空判断与字典查找用同一个值。
                    var eofSessionId = eofToken?.ID;

                    // 对端 FIN 只把「入站结束」交给连接门面：由输入处理器读完 Pipe 剩余字节后
                    // 自行收口，期间写方向仍可用。带 SocketError 的失败不等于 FIN，仍按断开处理。
                    if (readArgs.SocketError == SocketError.Success
                        && !string.IsNullOrEmpty(eofSessionId)
                        && eofConnection != null
                        && _pipelineCts.ContainsKey(eofSessionId))
                    {
                        eofConnection.CompleteInput();
                        // 对端不会再发数据，不再 re-arm 接收；断开由处理器返回后的兜底完成。
                        return;
                    }

                    try { Disconnect(eofToken); } catch (Exception ex) { LogHelper.Error($"An exception occurs when disconnecting:{eofSessionId}", ex); }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke("", ex);
            }
            finally
            {
                // 与 ProcessReceive 的 TryBeginOperation 配对：同步完成、异常、背压暂停都在这里减一次，
                // 延迟回收的令牌会在计数归零时由这唯一入口入池。lock 已退出，避免在持锁时入池。
                _sessionManager.CompleteOperation(readArgs?.UserToken as IUserToken);
            }
        }

        void ProcessSended(SocketAsyncEventArgs e)
        {
            try
            {
                var token = e.UserToken as IUserToken;
                if (token == null) return;
                lock (token)
                {
                    if (!token.IsSending) return;
                    token.IsSending = false;

                    // 内置令牌以每次发送的操作对象为唯一完成点；完成回调只完成本次操作，
                    // 迟到的旧回调拿不到新操作，因此不会误释放下一次发送的 owner。
                    var baseToken = token as BaseUserToken;
                    var operation = baseToken != null ? baseToken.TakePendingSend() : null;

                    if (operation != null)
                    {
                        if (e.SocketError != SocketError.Success)
                            operation.TryCompleteException(new SocketException((int)e.SocketError));
                        else if (operation.IsTimedOut)
                            operation.TryCompleteException(new TimeoutException("Sending data timeout"));
                        else
                            operation.TryCompleteSuccess();
                    }
                    else
                    {
                        // 非内置令牌的回退路径：沿用旧的所有权字段。
                        var owner = token.TakeSendingOwner();
                        if (owner != null)
                        {
                            try { owner.Dispose(); } catch { }
                        }
                    }

                    token.Actived = DateTimeHelper.Now;
                    token.ReleaseWrite();
                }
            }
            catch (Exception ex)
            {
                // 同步完成回调中的异常不能冒泡回提交流程：那里会再次触碰可能已入池的令牌。
                // 回池会清空 ID，日志与回调都用快照。
                var tokenId = (e.UserToken as IUserToken)?.ID;
                LogHelper.Error($"An exception occurs when sended:{tokenId}", ex);
                try { OnError?.Invoke(tokenId, ex); } catch { }
            }
            finally
            {
                // 与 SendAsyncRaw/End 的 TryBeginOperation 配对；lock 已退出，入池不在持锁时进行
                _sessionManager.CompleteOperation(e.UserToken as IUserToken);
            }
        }

        #region send method

        private Task SendAsyncRaw(IUserToken userToken, ArraySegment<byte> seg, IDisposable owner)
        {
            if (userToken == null || seg.Array == null || seg.Count == 0)
            {
                owner?.Dispose();
                return Task.CompletedTask;
            }

            var operation = new SendOperation(owner);
            var baseToken = userToken as BaseUserToken;
            var associated = false;

            try { _sessionManager.Active(userToken.ID); } catch { }

            bool acquired = false;
            try
            {
                acquired = userToken.WaitWrite(SocketOption.ActionTimeout);
                if (!acquired)
                {
                    operation.TryCompleteException(new TimeoutException("Sending data timeout"));
                    OnError?.Invoke($"An exception occurs when a message is sending:{userToken.ID}", new TimeoutException("Sending data timeout"));
                    return operation.Task;
                }

                if (userToken.Socket == null || !userToken.Socket.Connected)
                {
                    operation.TryCompleteException(new KernelException("发送失败，当前连接已断开。"));
                    OnError?.Invoke($"An exception occurs when a message is sending:{userToken.ID}", new KernelException("发送失败，当前连接已断开。"));
                    return operation.Task;
                }

                var writeArgs = userToken.WriteArgs;
                if (writeArgs == null)
                {
                    operation.TryCompleteException(new KernelException("发送失败，写入参数未初始化。"));
                    return operation.Task;
                }

                // 关联点必须在拿到写闸门之后：并发发送在闸门外排队，提前写入会覆盖在途操作，
                // 让完成回调取走并释放下一次发送的 owner。
                if (baseToken != null)
                {
                    baseToken.PendingSend = operation;
                    associated = true;
                }
                else
                {
                    userToken.SendingOwner = owner;
                }

                userToken.IsSending = true;
                writeArgs.SetBuffer(seg.Array, seg.Offset, seg.Count);

                bool asyncPending;

                // 回收已请求时不得提交：writeArgs 可能与即将入池的令牌属于同一连接，
                // 提交后下一次复用会在 SetBuffer 处失败并丢失令牌。
                if (baseToken != null && !baseToken.TryBeginOperation())
                {
                    userToken.IsSending = false;
                    if (associated) baseToken.TakePendingSend();
                    associated = false;
                    try { userToken.ReleaseWrite(); } catch { }
                    acquired = false;
                    var closedEx = new KernelException("连接正在回收，发送已取消。");
                    operation.TryCompleteException(closedEx);
                    OnError?.Invoke($"An exception occurs when a message is sending:{userToken.ID}", closedEx);
                    try { Disconnect(userToken, closedEx); } catch { }
                    return operation.Task;
                }

                try
                {
                    asyncPending = userToken.Socket.SendAsync(writeArgs);
                }
                catch (Exception ex)
                {
                    // 入池会清空 ID，日志用快照
                    var tokenId = userToken.ID;

                    // SendAsync 抛异常表示本次发送未被内核排队，完成回调不会再来：
                    // 这里收回关联并释放写闸门，由本方法唯一完成操作。
                    try
                    {
                        userToken.IsSending = false;
                        if (associated) baseToken.TakePendingSend();
                        associated = false;
                        try { userToken.ReleaseWrite(); } catch { }
                        acquired = false;
                        operation.TryCompleteException(ex);
                        OnError?.Invoke($"An exception occurs when a message is sending:{tokenId}", ex);
                    }
                    finally
                    {
                        // 计数归零后令牌可能立即被复用，必须是本方法对令牌的最后一次访问
                        _sessionManager.CompleteOperation(userToken);
                    }
                    return operation.Task;
                }

                // 所有权已交给完成回调，finally 不再释放写闸门。
                associated = false;
                acquired = false;

                if (!asyncPending)
                {
                    ProcessSended(writeArgs);
                }
                else
                {
                    StartSendWatchdog(userToken, operation);
                }

                return operation.Task;
            }
            catch (Exception ex)
            {
                if (associated) baseToken.TakePendingSend();
                operation.TryCompleteException(ex);
                OnError?.Invoke($"An exception occurs when a message is sending:{userToken?.ID}", ex);
                if (userToken.Socket == null || !userToken.Socket.Connected)
                    Disconnect(userToken, ex);
                return operation.Task;
            }
            finally
            {
                if (acquired)
                {
                    try { userToken.ReleaseWrite(); } catch { }
                }
            }
        }

        /// <summary>
        /// 启动发送超时看门狗。超时先关闭 socket 取消在途发送，再等待完成回调证明交接已终止；
        /// 无法证明时不释放 owner（内核可能仍在读该缓冲），只让等待方以超时失败。
        /// </summary>
        private void StartSendWatchdog(IUserToken userToken, SendOperation operation)
        {
            var timeout = SocketOption.ActionTimeout > 0 ? SocketOption.ActionTimeout : 180 * 1000;
            _ = RunSendWatchdogAsync(userToken, operation, timeout);
        }

        private async Task RunSendWatchdogAsync(IUserToken userToken, SendOperation operation, int timeout)
        {
            await Task.Delay(timeout).ConfigureAwait(false);

            if (!operation.TryMarkTimedOut()) return;

            // 先关闭 socket，促使内核取消在途发送并由完成回调收口。
            try { userToken.Socket?.Shutdown(SocketShutdown.Both); } catch { }
            try { userToken.Socket?.Close(); } catch { }

            // 再等一次完成回调；1 秒内证明不了完成，就放弃释放 owner 并解除写闸门。
            var completed = await Task.WhenAny(operation.Task, Task.Delay(1000)).ConfigureAwait(false);
            if (completed != operation.Task)
            {
                operation.TryAbandon(new TimeoutException("Sending data timeout"));
                try { userToken.ReleaseWrite(); } catch { }

                // 内核可能仍在读该令牌的发送缓冲：让它永久退役（不回池、不归还缓冲），
                // 由池核销创建计数后补建新令牌。退役不动在途计数；即便迟到的完成回调把计数减到 0，
                // 退役位仍会拦住入池。
                _sessionManager.RetireUserToken(userToken);
            }

            OnError?.Invoke($"An exception occurs when a message is sending:{userToken?.ID}", new TimeoutException("Sending data timeout"));
        }

        /// <summary>
        /// 向指定会话发送数据：TLS 激活时交给 TLS 通道，否则复制到池化缓冲再异步发出；流模式下同时续期通道空闲超时
        /// </summary>
        /// <param name="sessionID">会话标识</param>
        /// <param name="data">待发送数据，为空或会话不存在时直接返回</param>
        public void Send(string sessionID, ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) return;
            var userToken = _sessionManager.Get(sessionID);
            if (userToken == null) return;
            if (_useStream) _channelManager.Refresh(sessionID);
            if (TrySendViaTls(userToken, data)) return;
            var writer = new PooledBufferWriter(data.Length);
            data.CopyTo(writer.GetSpan(data.Length));
            writer.Advance(data.Length);
            writer.TryGetArray(out var rented);
            SendAsyncRaw(userToken, rented, writer);
        }

        /// <summary>
        /// 向指定会话异步发送：能直接取到底层数组时零拷贝直发，否则复制进池化缓冲；TLS 激活时走 TLS 通道
        /// </summary>
        /// <param name="sessionID">会话标识</param>
        /// <param name="data">待发送数据，为空或会话不存在时直接返回</param>
        public void SendAsync(string sessionID, ReadOnlyMemory<byte> data)
        {
            if (data.Length == 0) return;
            var userToken = _sessionManager.Get(sessionID);
            if (userToken == null) return;
            if (_useStream) _channelManager.Refresh(sessionID);
            if (TrySendViaTls(userToken, data.Span)) return;
            if (MemoryMarshal.TryGetArray(data, out var seg))
            {
                SendAsyncRaw(userToken, seg, null);
                return;
            }
            var writer = new PooledBufferWriter(data.Length);
            data.Span.CopyTo(writer.GetSpan(data.Length));
            writer.Advance(data.Length);
            writer.TryGetArray(out var rented);
            SendAsyncRaw(userToken, rented, writer);
        }

        /// <summary>
        /// 向指定会话异步发送并转移缓冲所有权；返回的 Task 在底层交接完成后完成，失败可观察。
        /// 会话不存在时先释放 owner 再同步抛出，调用方不得再次释放 owner。
        /// </summary>
        /// <param name="sessionID">会话标识</param>
        /// <param name="data">待发送数据，为空时只释放 owner</param>
        /// <param name="owner">data 所占缓冲的所有权对象，可为 null</param>
        /// <returns>底层交接完成的任务</returns>
        /// <exception cref="KernelException">会话不存在</exception>
        public Task SendAsync(string sessionID, ReadOnlyMemory<byte> data, IDisposable owner)
        {
            var userToken = _sessionManager.Get(sessionID);
            if (userToken == null)
            {
                owner?.Dispose();
                throw new KernelException("Failed to send data,current session does not exist！");
            }
            if (_useStream) _channelManager.Refresh(sessionID);
            return SendAsyncCore(userToken, data, owner);
        }

        private async Task SendAsyncCore(IUserToken userToken, ReadOnlyMemory<byte> data, IDisposable owner)
        {
            if (data.Length == 0)
            {
                owner?.Dispose();
                return;
            }

            if (_tlsEnabled)
            {
                var connection = GetConnection(userToken);
                if (connection == null || connection.Tls == null)
                {
                    owner?.Dispose();
                    throw new SaeaTlsException("TLS 连接尚未就绪。");
                }

                try
                {
                    // SendApplicationData 内部先复制，Task 完成点覆盖本次应用数据拆出的全部 TLS 记录。
                    await connection.SendApplicationData(data.Span).ConfigureAwait(false);
                }
                finally
                {
                    owner?.Dispose();
                }
                return;
            }

            ArraySegment<byte> segment;
            if (MemoryMarshal.TryGetArray(data, out segment) && segment.Array != null)
            {
                await SendAsyncRaw(userToken, segment, owner).ConfigureAwait(false);
                return;
            }

            PooledBufferWriter writer = null;
            try
            {
                writer = new PooledBufferWriter(data.Length);
                data.Span.CopyTo(writer.GetSpan(data.Length));
                writer.Advance(data.Length);
            }
            catch
            {
                writer?.Dispose();
                owner?.Dispose();
                throw;
            }

            if (!writer.TryGetArray(out segment) || segment.Array == null)
            {
                writer.Dispose();
                owner?.Dispose();
                return;
            }

            owner?.Dispose();
            await SendAsyncRaw(userToken, segment, writer).ConfigureAwait(false);
        }

        /// <summary>
        /// 先按线格式编码再向指定会话发送；会话不存在时直接返回，coder 未初始化或编码失败时经 OnError 报告
        /// </summary>
        /// <param name="sessionID">会话标识</param>
        /// <param name="protocal">待发送的协议对象，为 null 时直接返回</param>
        public void SendAsync(string sessionID, ISocketProtocal protocal)
        {
            if (protocal == null) return;
            var userToken = _sessionManager.Get(sessionID);
            if (userToken == null) return;
            if (_useStream) _channelManager.Refresh(sessionID);
            var coder = userToken.Coder;
            if (coder == null)
            {
                OnError?.Invoke(userToken?.ID ?? "", new InvalidOperationException("SAEA SocketError:coder 未初始化"));
                return;
            }
            var bodyLen = protocal.BodyLength;
            var size = bodyLen > 0 && bodyLen < int.MaxValue - 64 ? (int)bodyLen + 64 : 64;
            var writer = new PooledBufferWriter(size);
            try
            {
                coder.Encode(protocal, writer);
            }
            catch (Exception ex)
            {
                writer.Dispose();
                OnError?.Invoke(userToken?.ID ?? "", ex);
                return;
            }
            writer.TryGetArray(out var rented);
            if (rented.Array != null && TrySendViaTls(userToken, new ReadOnlySpan<byte>(rented.Array, rented.Offset, rented.Count)))
            {
                writer.Dispose();
                return;
            }
            SendAsyncRaw(userToken, rented, writer);
        }

        /// <summary>
        /// 发送最后一段数据后断开连接。TLS 下会等待应用数据完成加密并提交发送，避免最后一段数据丢失。
        /// </summary>
        /// <param name="sessionID">会话 ID</param>
        /// <param name="data">最后一段数据</param>
        public void End(string sessionID, ReadOnlyMemory<byte> data)
        {
            var userToken = _sessionManager.Get(sessionID);
            if (userToken == null) return;
            if (_useStream) _channelManager.Refresh(sessionID);

            if (data.Length > 0 && _tlsEnabled)
            {
                // TLS 写入是异步的：必须等应用数据加密并提交到发送路径后再断开，
                // 否则 Disconnect 会先释放连接与 Socket，导致最后一段数据丢失。
                var connection = GetConnection(userToken);
                if (connection == null || connection.Tls == null)
                {
                    OnError?.Invoke(userToken.ID ?? "", new SaeaTlsException("TLS 连接尚未就绪。"));
                    Disconnect(userToken);
                    return;
                }

                var copy = data.ToArray();
                var sessionId = userToken.ID;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await connection.WriteAsync(copy, 0, copy.Length, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        OnError?.Invoke(sessionId ?? "", ex);
                    }
                    finally
                    {
                        // 若期间已被其他路径释放（ID 被置空或会话已复用），不要误断新连接。
                        if (string.Equals(userToken.ID, sessionId, StringComparison.Ordinal))
                            Disconnect(userToken);
                    }
                });
                return;
            }
            try
            {
                if (userToken.Socket != null && userToken.Socket.Connected && data.Length > 0)
                {
                    var copy = data.ToArray();
                    var writeArgs = userToken.WriteArgs;
                    if (writeArgs != null && userToken.WaitWrite(SocketOption.ActionTimeout))
                    {
                        writeArgs.SetBuffer(copy, 0, copy.Length);
                        userToken.IsSending = true;
                        if (userToken is BaseUserToken endToken && !endToken.TryBeginOperation())
                        {
                            // 连接已在回收：不再提交，交给随后的 Disconnect 收口
                            userToken.IsSending = false;
                            userToken.ReleaseWrite();
                        }
                        else
                        {
                            try
                            {
                                if (!userToken.Socket.SendAsync(writeArgs)) ProcessSended(writeArgs);
                            }
                            catch (Exception ex)
                            {
                                // 入池会清空 ID：日志用快照；先在令牌仍归属本连接时断开，
                                // 再由 CompleteOperation 作为本方法对令牌的最后一次访问。
                                var tokenId = userToken.ID;
                                try
                                {
                                    OnError?.Invoke(tokenId ?? "", ex);
                                }
                                finally
                                {
                                    try { Disconnect(userToken); } catch { }
                                    _sessionManager.CompleteOperation(userToken);
                                }
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(userToken?.ID ?? "", ex);
            }

            // 同步完成的发送可能已是该令牌最后一个在途操作，令牌随即入池并被新连接取走；
            // 此时 userToken.ID 已是新连接的标识，无条件 Disconnect 会误断别人的连接。
            if (string.Equals(userToken.ID, sessionID, StringComparison.Ordinal))
                Disconnect(userToken);
        }

        /// <summary>
        /// 按对端地址对应的会话异步发送，等价于以 ipEndPoint 的字符串形式作为会话标识
        /// </summary>
        /// <param name="ipEndPoint">对端地址，为 null 时直接返回</param>
        /// <param name="data">待发送数据</param>
        public void SendAsync(IPEndPoint ipEndPoint, ReadOnlyMemory<byte> data)
        {
            if (ipEndPoint == null) return;
            SendAsync(ipEndPoint.ToString(), data);
        }
        #endregion

        private void _sessionManager_OnTimeOut(IUserToken userToken)
        {
            Disconnect(userToken);
        }

        /// <summary>
        /// 按会话标识取当前承载对象：流模式返回通道信息并续期，事件模式返回会话令牌
        /// </summary>
        /// <param name="sessionID">会话标识</param>
        /// <returns>流模式为 ChannelInfo、事件模式为 IUserToken，会话不存在时为 null</returns>
        public object GetCurrentObj(string sessionID)
        {
            if (_useStream)
            {
                var channel = _channelManager.Get(sessionID);
                if (channel != null) _channelManager.Refresh(sessionID);
                return channel;
            }

            return SessionManager.Get(sessionID);
        }

        /// <summary>
        /// 断开客户端连接。对端 FIN 不直接触发本方法：流模式下 FIN 先投递入站结束，
        /// 由输入处理器读完剩余字节后返回，再由本方法收口；重复调用经会话释放判定为幂等，
        /// 只有首次释放才触发 <see cref="OnDisconnected"/>。
        /// </summary>
        /// <param name="userToken">会话令牌，为空或已释放时直接返回</param>
        /// <param name="ex">触发断开的异常，正常关闭时为 null</param>
        public void Disconnect(IUserToken userToken, Exception ex = null)
        {
            try
            {
                if (userToken == null) return;
                // 只读取一次 ID：并发的 Disconnect（读错误 / 握手失败 / 空闲超时）会经
                // SessionManager.Free 把 ID 置空，二次读取可能拿到 null 并让 TryRemove 抛异常。
                var sessionId = userToken.ID;
                if (string.IsNullOrEmpty(sessionId)) return;

                // 先取消输入处理器的令牌，再释放连接，保证泵先收到取消并走收口。
                CancelPipeline(sessionId);

                SaeaConnection connection;
                if (_connections.TryRemove(sessionId, out connection))
                {
                    try { connection.Dispose(); } catch { }
                }
                _channelManager.Remove(sessionId);

                if (_sessionManager.Free(userToken))
                {
                    Interlocked.Decrement(ref _clientCounts);
                    OnDisconnected?.Invoke(sessionId, ex);
                }
            }
            catch (Exception e)
            {
                LogHelper.Error($"An exception occurs when disconnecting:{userToken?.ID}", e);
                OnError?.Invoke("", e);
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        /// <param name="sessionID"></param>
        public void Disconnect(string sessionID)
        {
            if (string.IsNullOrEmpty(sessionID))
            {
                return;
            }
            var userToken = SessionManager.Get(sessionID);
            if (userToken != null)
                Disconnect(userToken);
        }

        /// <summary>
        /// 关闭
        /// </summary>
        public void Stop()
        {
            try
            {
                foreach (var item in _pipelineCts)
                {
                    try { item.Value.Cancel(); } catch (ObjectDisposedException) { }
                }
                _pipelineCts.Clear();
            }
            catch { }
            try
            {
                foreach (var item in _connections)
                {
                    try { item.Value.Dispose(); } catch { }
                }
                _connections.Clear();
            }
            catch { }
            try
            {
                _channelManager.Clear();
            }
            catch { }
            try
            {
                _listener?.Close(10 * 1000);
            }
            catch { }
            try
            {
                _sessionManager.Clear();
            }
            catch { }
            try
            {
                _listener?.Dispose();
                _listener = null;
            }
            catch { }
        }

        /// <summary>
        /// 释放服务器资源，内部调用 Stop 停止监听并清理连接、通道与会话；异常经 OnError 报告
        /// </summary>
        public void Dispose()
        {
            try
            {
                try { Stop(); } catch { }
            }
            catch (Exception ex)
            {
                OnError?.Invoke("", ex);
            }
        }
    }
}
