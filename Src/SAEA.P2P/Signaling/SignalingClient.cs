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
*命名空间：SAEA.P2P.Signaling
*文件名： SignalingClient
*版本号： v26.11.5.1
*唯一标识：770a3dd8-8c4c-4f5c-aed2-6805efb25f68
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 09:53:18
*描述：信令客户端
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 09:53:18
*修改人： yswenli
*版本号： v26.11.5.1
*描述：信令客户端
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SAEA.P2P.Common;
using SAEA.P2P.Protocol;
using SAEA.Sockets;
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

namespace SAEA.P2P.Signaling
{
    /// <summary>
    /// 信令客户端：维护到信令服务器的长连接，负责注册、鉴权、心跳、断线重连与会话报文收发
    /// </summary>
    public sealed class SignalingClient : IDisposable
    {
        private readonly SignalingOptions _options;

        private readonly object _syncRoot = new object();

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private readonly Dictionary<string, List<SignalingMessage>> _candidateOutbox = new Dictionary<string, List<SignalingMessage>>(StringComparer.Ordinal);

        private SaeaClientSocket _socket;

        private P2PCoder _coder;

        private TaskCompletionSource<SignalingMessage> _registerTcs;

        private Task _registerTask;

        private Timer _heartbeatTimer;

        private volatile string _serverSessionId;

        private volatile bool _isConnected;

        private volatile bool _isRegistered;

        private int _disposed;

        private int _reconnectState;

        /// <summary>
        /// 按配置创建信令客户端并校验配置
        /// </summary>
        /// <param name="options">客户端配置，不可为 null</param>
        /// <exception cref="ArgumentNullException">options 为 null</exception>
        /// <exception cref="P2PException">配置校验失败</exception>
        public SignalingClient(SignalingOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            options.Validate();
            _options = options;
        }

        /// <summary>
        /// 当前使用的客户端配置
        /// </summary>
        public SignalingOptions Options => _options;

        /// <summary>
        /// 本端节点标识，取自配置
        /// </summary>
        public string PeerId => _options.PeerId;

        /// <summary>
        /// 注册应答中的服务端会话标识，未注册时为 null
        /// </summary>
        public string ServerSessionId => _serverSessionId;

        /// <summary>
        /// 与信令服务器的 socket 是否处于连接状态
        /// </summary>
        public bool IsConnected => _isConnected;

        /// <summary>
        /// 是否已收到 RegisterAck
        /// </summary>
        public bool IsRegistered => _isRegistered;

        /// <summary>
        /// 收到 RegisterAck 时触发，每次重连成功都会再次触发；在 socket 接收线程上同步执行，处理函数应尽快返回
        /// </summary>
        public event Action<SignalingMessage> OnRegistered;

        /// <summary>
        /// 收到 ConnectRequest 时触发；在 socket 接收线程上同步执行，处理函数应尽快返回
        /// </summary>
        public event Action<SignalingMessage> OnConnectRequest;

        /// <summary>
        /// 收到 SessionOffer 时触发；在 socket 接收线程上同步执行，处理函数应尽快返回
        /// </summary>
        public event Action<SignalingMessage> OnSessionOffer;

        /// <summary>
        /// 收到 SessionAnswer 时触发；在 socket 接收线程上同步执行，处理函数应尽快返回
        /// </summary>
        public event Action<SignalingMessage> OnSessionAnswer;

        /// <summary>
        /// 收到 TrickleCandidate 时触发；在 socket 接收线程上同步执行，处理函数应尽快返回
        /// </summary>
        public event Action<SignalingMessage> OnTrickleCandidate;

        /// <summary>
        /// 收到 EndOfCandidates 时触发；在 socket 接收线程上同步执行，处理函数应尽快返回
        /// </summary>
        public event Action<SignalingMessage> OnEndOfCandidates;

        /// <summary>
        /// 收到服务端下发的 TURN 临时凭据时触发；在 socket 接收线程上同步执行，处理函数应尽快返回，且不得把凭据写入日志
        /// </summary>
        public event Action<SignalingMessage> OnTurnCredential;

        /// <summary>
        /// 收到 SessionClose 时触发；在 socket 接收线程上同步执行，处理函数应尽快返回
        /// </summary>
        public event Action<SignalingMessage> OnSessionClose;

        /// <summary>
        /// 与信令服务器断开时触发，参数为中文断开原因；主动 Dispose 不触发
        /// </summary>
        public event Action<string> OnDisconnected;

        /// <summary>
        /// 发生错误时触发，参数为错误码与中文说明
        /// </summary>
        public event Action<string, string> OnError;

        /// <summary>
        /// 连接信令服务器并完成注册；已注册时直接返回，注册进行中时返回同一个任务
        /// </summary>
        /// <param name="cancellationToken">取消令牌，取消时抛出 OperationCanceledException</param>
        /// <returns>收到 RegisterAck 后完成的任务；连接失败、超时或服务端回 Error 时以 P2PException 失败</returns>
        public Task RegisterAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (_isRegistered)
            {
                return Task.CompletedTask;
            }

            lock (_syncRoot)
            {
                if (_isRegistered)
                {
                    return Task.CompletedTask;
                }

                if (_registerTask != null)
                {
                    return _registerTask;
                }

                var task = RegisterCoreAsync(cancellationToken);
                _registerTask = task;

                // 结束后复位，失败或取消后调用方可以再次发起注册
                task.ContinueWith(t =>
                {
                    lock (_syncRoot)
                    {
                        if (ReferenceEquals(_registerTask, t))
                        {
                            _registerTask = null;
                        }
                    }
                }, TaskScheduler.Default);

                return task;
            }
        }

        /// <summary>
        /// 发送会话报文；候选类报文在未注册时先入队，注册或重连成功后按会话补发
        /// </summary>
        /// <param name="message">待发送报文，不可为 null</param>
        /// <param name="cancellationToken">取消令牌，取消时抛出 OperationCanceledException</param>
        /// <returns>发送完成的任务</returns>
        /// <exception cref="ArgumentNullException">message 为 null</exception>
        /// <exception cref="ArgumentException">消息类型不允许客户端发送，或缺少必需字段</exception>
        /// <exception cref="P2PException">非候选类报文在未注册时发送</exception>
        public Task SendAsync(SignalingMessage message, CancellationToken cancellationToken = default)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            cancellationToken.ThrowIfCancellationRequested();
            ValidateSendableMessage(message);

            if (IsCandidateMessage(message.Type))
            {
                lock (_syncRoot)
                {
                    List<SignalingMessage> queue;
                    if (!_candidateOutbox.TryGetValue(message.SessionId, out queue))
                    {
                        queue = new List<SignalingMessage>();
                        _candidateOutbox[message.SessionId] = queue;
                    }
                    queue.Add(message);

                    // 已注册时立刻补发；发送失败只保留队列等重连后补发，不把异常抛给调用方
                    if (_isRegistered)
                    {
                        FlushSessionLocked(message.SessionId);
                    }
                }

                return Task.CompletedTask;
            }

            SaeaClientSocket socket;
            P2PCoder coder;
            lock (_syncRoot)
            {
                if (!_isRegistered)
                {
                    throw new P2PException(ErrorCode.NotConnected, "信令连接未就绪，不能发送该报文");
                }

                socket = _socket;
                coder = _coder;
            }

            if (socket == null || coder == null)
            {
                throw new P2PException(ErrorCode.NotConnected, "信令连接未就绪，不能发送该报文");
            }

            socket.SendAsync(coder.EncodeP2P(message.Type, SignalingJsonCodec.Encode(message)));
            return Task.CompletedTask;
        }

        /// <summary>
        /// 停止心跳、断开并释放信令连接，清空待发队列与事件；可重复调用，不触发 OnDisconnected
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            try
            {
                _cts.Cancel();
            }
            catch (Exception ex)
            {
                P2PLogHelper.Warning(PeerId, "取消信令内部令牌失败：" + ex.GetType().Name);
            }

            StopHeartbeat();

            SaeaClientSocket socket;
            TaskCompletionSource<SignalingMessage> pending;
            lock (_syncRoot)
            {
                socket = _socket;
                _socket = null;
                _coder = null;
                _isConnected = false;
                _isRegistered = false;
                _serverSessionId = null;
                pending = _registerTcs;
                _registerTcs = null;
                _registerTask = null;
                _candidateOutbox.Clear();
            }

            // 先清空 socket 字段再关闭，断开事件里的旧连接判断会直接忽略，不会触发 OnDisconnected
            SafeClose(socket);

            if (pending != null)
            {
                pending.TrySetCanceled();
            }

            OnRegistered = null;
            OnConnectRequest = null;
            OnSessionOffer = null;
            OnSessionAnswer = null;
            OnTrickleCandidate = null;
            OnEndOfCandidates = null;
            OnTurnCredential = null;
            OnSessionClose = null;
            OnDisconnected = null;
            OnError = null;
        }

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        /// <summary>
        /// 已释放时抛出 ObjectDisposedException
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        /// <summary>
        /// 按配置构造 socket 选项，启用 TLS 时用目标主机名校验证书
        /// </summary>
        /// <returns>socket 选项</returns>
        private ISocketOption BuildSocketOption()
        {
            var builder = SocketOptionBuilder.Instance
                .SetSocket(SAEASocketType.Tcp)
                .UsePacket()
                .SetIP(_options.ServerAddress)
                .SetPort(_options.ServerPort)
                .SetConnectTimeout(_options.ConnectTimeoutMs)
                // 客户端自己按 HeartbeatIntervalMs 发心跳，底层空闲回收只作兜底，放宽到 24 小时避免误断
                .SetFreeTime(24 * 60 * 60 * 1000);

            if (_options.EnableTls)
            {
                var targetHost = string.IsNullOrEmpty(_options.TlsTargetHost) ? _options.ServerAddress : _options.TlsTargetHost;
                builder.WithSsl(o =>
                {
                    o.TargetHost = targetHost;
                    o.AllowInvalidCertificates = _options.TlsAllowInvalidCertificates;
                });
            }

            return builder.Build();
        }

        /// <summary>
        /// 注册主体：失败时释放本次连接，保证下一个注册请求可以重新建连
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>注册完成的任务</returns>
        private async Task RegisterCoreAsync(CancellationToken cancellationToken)
        {
            try
            {
                await ConnectAndRegisterAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                ReleaseConnection();
                throw;
            }
        }

        /// <summary>
        /// 建立连接、替换当前 socket、发送 Register 并等待 RegisterAck
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>注册完成的任务</returns>
        private async Task ConnectAndRegisterAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            var option = BuildSocketOption();
            var socket = SocketFactory.CreateClientSocket(option) as SaeaClientSocket;
            if (socket == null)
            {
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "无法创建信令客户端");
            }

            // 半包缓存是解码器实例字段，每次连接必须新建，不能跨连接复用；
            // 接收回调绑定本条连接自己的解码器，重连后迟到的旧连接数据不会污染新连接
            var coder = new P2PCoder();
            socket.OnClientReceiveSpan += data => OnReceive(coder, data);
            socket.OnDisconnected += (id, ex) => HandleDisconnected(socket);
            socket.OnError += (id, ex) => HandleSocketError(ex);

            try
            {
                socket.Connect();
            }
            catch (Exception ex)
            {
                SafeClose(socket);
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "无法连接信令服务器", ex);
            }

            if (!socket.Connected)
            {
                SafeClose(socket);
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "无法连接信令服务器");
            }

            var registerTcs = new TaskCompletionSource<SignalingMessage>();
            SaeaClientSocket previousSocket;
            lock (_syncRoot)
            {
                previousSocket = _socket;
                _socket = socket;
                _coder = coder;
                _isConnected = true;
                _isRegistered = false;
                _serverSessionId = null;
                _registerTcs = registerTcs;
            }

            // 旧连接在替换完成后再关，关闭事件会因 socket 字段已更新而被忽略
            SafeClose(previousSocket);

            var register = SignalingMessage.Create(P2PMessageType.Register);
            register.PeerId = PeerId;
            register.NodeName = string.IsNullOrEmpty(_options.NodeName) ? PeerId : _options.NodeName;
            register.Capabilities = _options.Capabilities == null || _options.Capabilities.Length == 0
                ? new[] { "udp", "tcp" }
                : (string[])_options.Capabilities.Clone();

            socket.SendAsync(coder.EncodeP2P(register.Type, SignalingJsonCodec.Encode(register)));
            P2PLogHelper.Info(PeerId, "已发送信令注册请求");

            await WaitForRegisterAsync(registerTcs, cancellationToken).ConfigureAwait(false);

            // 注册前允许候选类报文先入队，注册成功后必须补发，否则这些候选会一直滞留到下一次重连
            FlushAllCandidates();
        }

        /// <summary>
        /// 等待注册应答，超时或外部取消时抛出异常
        /// </summary>
        /// <param name="registerTcs">本次注册的应答信号</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>注册完成的任务</returns>
        private async Task WaitForRegisterAsync(TaskCompletionSource<SignalingMessage> registerTcs, CancellationToken cancellationToken)
        {
            var canceled = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(() => canceled.TrySetResult(true)))
            {
                var timeoutTask = Task.Delay(_options.RegisterTimeoutMs);
                var completed = await Task.WhenAny(registerTcs.Task, timeoutTask, canceled.Task).ConfigureAwait(false);

                if (completed == registerTcs.Task)
                {
                    await registerTcs.Task.ConfigureAwait(false);
                    return;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                throw new P2PException(ErrorCode.RegisterTimeout, "等待注册应答超时");
            }
        }

        /// <summary>
        /// 接收回调：按本条连接的解码器拆帧，再逐帧分派
        /// </summary>
        /// <param name="coder">本条连接专用的解码器</param>
        /// <param name="data">接收到的字节，仅在回调期间有效</param>
        private void OnReceive(P2PCoder coder, ReadOnlySpan<byte> data)
        {
            if (coder == null || IsDisposed)
            {
                return;
            }

            using (var frames = coder.DecodeP2P(data))
            {
                foreach (var frame in frames.Frames)
                {
                    try
                    {
                        Dispatch(frame);
                    }
                    catch (Exception ex)
                    {
                        // 接收线程上抛异常会打断后续帧的处理，这里兜住只记日志
                        P2PLogHelper.Error(PeerId, ErrorCode.ProtocolMalformed, "信令报文处理失败", ex);
                    }
                }
            }
        }

        /// <summary>
        /// 按消息类型分派一帧；控制类报文内部处理，会话类报文解码后触发对应事件
        /// </summary>
        /// <param name="frame">解码出的帧，内容仅在解码批次释放前有效</param>
        private void Dispatch(ISocketProtocal frame)
        {
            var type = (P2PMessageType)frame.Type;
            switch (type)
            {
                case P2PMessageType.Heartbeat:
                case P2PMessageType.HeartbeatAck:
                case P2PMessageType.NodeList:
                    // 心跳与在线列表不参与信令会话，且空负载会被解码成错误报文
                    return;
                default:
                    break;
            }

            var message = SignalingJsonCodec.Decode(frame.Content.ToArray());
            if (type != P2PMessageType.Error && message.Type == P2PMessageType.Error)
            {
                // 解码失败只报错不丢连接，后续帧继续处理
                RaiseError(message.Code, message.Message);
                return;
            }

            switch (type)
            {
                case P2PMessageType.RegisterAck:
                    HandleRegisterAck(message);
                    break;
                case P2PMessageType.AuthChallenge:
                    HandleAuthChallenge(message);
                    break;
                case P2PMessageType.AuthSuccess:
                    P2PLogHelper.Info(PeerId, "信令鉴权通过");
                    break;
                case P2PMessageType.ConnectRequest:
                    OnConnectRequest?.Invoke(message);
                    break;
                case P2PMessageType.SessionOffer:
                    OnSessionOffer?.Invoke(message);
                    break;
                case P2PMessageType.SessionAnswer:
                    OnSessionAnswer?.Invoke(message);
                    break;
                case P2PMessageType.TrickleCandidate:
                    OnTrickleCandidate?.Invoke(message);
                    break;
                case P2PMessageType.EndOfCandidates:
                    OnEndOfCandidates?.Invoke(message);
                    break;
                case P2PMessageType.TurnCredential:
                    OnTurnCredential?.Invoke(message);
                    break;
                case P2PMessageType.SessionClose:
                    OnSessionClose?.Invoke(message);
                    break;
                case P2PMessageType.Error:
                    HandleError(message);
                    break;
                default:
                    P2PLogHelper.Debug(PeerId, "忽略消息类型 " + (byte)type);
                    break;
            }
        }

        /// <summary>
        /// 处理注册应答：记录服务端会话标识、启动心跳并放行等待中的注册任务
        /// </summary>
        /// <param name="message">RegisterAck 报文</param>
        private void HandleRegisterAck(SignalingMessage message)
        {
            TaskCompletionSource<SignalingMessage> pending;
            lock (_syncRoot)
            {
                _serverSessionId = message.SessionId;
                _isConnected = true;
                _isRegistered = true;
                pending = _registerTcs;
                _registerTcs = null;
            }

            StartHeartbeat();
            P2PLogHelper.Info(PeerId, "信令注册成功，会话 " + message.SessionId);

            try
            {
                OnRegistered?.Invoke(message);
            }
            finally
            {
                // 事件处理抛异常也要放行注册任务，避免调用方一直等到超时
                if (pending != null)
                {
                    pending.TrySetResult(message);
                }
            }
        }

        /// <summary>
        /// 处理鉴权挑战：按 HMAC-SHA256 计算应答并回 AuthResponse；未配置共享密钥时忽略
        /// </summary>
        /// <param name="message">AuthChallenge 报文</param>
        private void HandleAuthChallenge(SignalingMessage message)
        {
            if (string.IsNullOrEmpty(_options.SharedSecret))
            {
                // 未配置共享密钥时不具备应答能力，忽略即可，也不要把服务端挑战内容写进日志
                P2PLogHelper.Debug(PeerId, "忽略鉴权挑战：未配置共享密钥");
                return;
            }

            if (string.IsNullOrEmpty(message.Nonce))
            {
                RaiseError(ErrorCode.AuthFailed, "鉴权挑战缺少随机数");
                return;
            }

            var response = SignalingMessage.Create(P2PMessageType.AuthResponse);
            response.Response = ComputeAuthResponse(_options.SharedSecret, message.Nonce, PeerId);

            if (TrySendFrame(response))
            {
                P2PLogHelper.Info(PeerId, "已回应信令鉴权挑战");
            }
            else
            {
                P2PLogHelper.Warning(PeerId, "信令鉴权应答发送失败");
            }
        }

        /// <summary>
        /// 按 HMAC-SHA256(共享密钥, nonce + "|" + peerId) 的 Base64 计算鉴权应答
        /// </summary>
        /// <param name="secret">与服务端一致的共享密钥</param>
        /// <param name="nonce">服务端挑战随机数</param>
        /// <param name="peerId">本端节点标识</param>
        /// <returns>Base64 编码的应答值</returns>
        private static string ComputeAuthResponse(string secret, string nonce, string peerId)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(nonce + "|" + peerId));
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// 处理服务端下发的 Error：触发错误事件，注册期间的错误同时让注册任务失败
        /// </summary>
        /// <param name="message">Error 报文</param>
        private void HandleError(SignalingMessage message)
        {
            RaiseError(message.Code, message.Message);

            TaskCompletionSource<SignalingMessage> pending;
            lock (_syncRoot)
            {
                pending = _registerTcs;
                _registerTcs = null;
            }

            if (pending != null)
            {
                var text = string.IsNullOrEmpty(message.Message) ? "信令服务器返回错误" : message.Message;
                pending.TrySetException(new P2PException(ErrorCode.RegisterFailed, text));
            }
        }

        /// <summary>
        /// 记录错误码并触发错误事件；日志只写错误码，不写报文内容
        /// </summary>
        /// <param name="code">错误码</param>
        /// <param name="message">中文说明</param>
        private void RaiseError(string code, string message)
        {
            P2PLogHelper.Warning(PeerId, "信令错误 " + code);
            OnError?.Invoke(code, message);
        }

        /// <summary>
        /// 处理 socket 断开：复位连接状态、放行等待中的注册任务，并按需启动重连循环
        /// </summary>
        /// <param name="socket">触发断开事件的连接</param>
        private void HandleDisconnected(SaeaClientSocket socket)
        {
            TaskCompletionSource<SignalingMessage> pending;
            lock (_syncRoot)
            {
                // 重连换下来的旧连接也会回调这里，只处理当前连接
                if (socket == null || !ReferenceEquals(_socket, socket))
                {
                    return;
                }

                _socket = null;
                _coder = null;
                _isConnected = false;
                _isRegistered = false;
                _serverSessionId = null;
                pending = _registerTcs;
                _registerTcs = null;
                _registerTask = null;
            }

            StopHeartbeat();
            SafeClose(socket);

            if (pending != null)
            {
                pending.TrySetException(new P2PException(ErrorCode.RegisterFailed, "信令服务器连接已断开"));
            }

            if (IsDisposed)
            {
                return;
            }

            P2PLogHelper.Warning(PeerId, "信令服务器连接已断开");
            OnDisconnected?.Invoke("信令服务器连接已断开");
            TryStartReconnect();
        }

        /// <summary>
        /// 处理 socket 错误：记录日志并触发错误事件
        /// </summary>
        /// <param name="ex">socket 异常</param>
        private void HandleSocketError(Exception ex)
        {
            if (ex == null)
            {
                return;
            }

            P2PLogHelper.Error(PeerId, ErrorCode.RegisterServerUnavailable, "信令连接发生 socket 错误", ex);
            OnError?.Invoke(ErrorCode.RegisterServerUnavailable, "信令连接发生 socket 错误");
        }

        /// <summary>
        /// 启动后台重连循环，同一时刻只允许一个循环
        /// </summary>
        private void TryStartReconnect()
        {
            if (IsDisposed)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _reconnectState, 1, 0) != 0)
            {
                return;
            }

            Task.Run((Func<Task>)ReconnectLoopAsync);
        }

        /// <summary>
        /// 重连循环：按间隔重试注册，成功后按会话补发候选；失败只记日志并继续下一轮
        /// </summary>
        /// <returns>循环结束的任务</returns>
        private async Task ReconnectLoopAsync()
        {
            var attempts = 0;
            try
            {
                while (!IsDisposed)
                {
                    try
                    {
                        await Task.Delay(_options.ReconnectIntervalMs, _cts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    if (IsDisposed)
                    {
                        return;
                    }

                    try
                    {
                        await RegisterAsync(_cts.Token).ConfigureAwait(false);
                        FlushAllCandidates();
                        P2PLogHelper.Info(PeerId, "信令重连成功");
                        return;
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        attempts++;
                        P2PLogHelper.Error(PeerId, ErrorCode.RegisterServerUnavailable, "信令重连失败，第 " + attempts + " 次", ex);

                        if (_options.MaxReconnectAttempts > 0 && attempts >= _options.MaxReconnectAttempts)
                        {
                            OnError?.Invoke(ErrorCode.RegisterServerUnavailable, "重连次数已达上限");
                            return;
                        }
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _reconnectState, 0);
            }
        }

        /// <summary>
        /// 按会话标识排序补发全部待发候选，成功发送的项从队列移除
        /// </summary>
        private void FlushAllCandidates()
        {
            var sessionIds = new List<string>();
            lock (_syncRoot)
            {
                sessionIds.AddRange(_candidateOutbox.Keys);
            }

            sessionIds.Sort(StringComparer.Ordinal);

            foreach (var sessionId in sessionIds)
            {
                lock (_syncRoot)
                {
                    FlushSessionLocked(sessionId);
                }
            }
        }

        /// <summary>
        /// 在已持有 <see cref="_syncRoot"/> 的前提下补发指定会话的候选，发送失败时保留剩余队列
        /// </summary>
        /// <param name="sessionId">会话标识</param>
        private void FlushSessionLocked(string sessionId)
        {
            if (sessionId == null)
            {
                return;
            }

            List<SignalingMessage> queue;
            if (!_candidateOutbox.TryGetValue(sessionId, out queue) || queue.Count == 0)
            {
                return;
            }

            var socket = _socket;
            var coder = _coder;
            if (!_isRegistered || socket == null || coder == null)
            {
                return;
            }

            while (queue.Count > 0)
            {
                var message = queue[0];
                try
                {
                    socket.SendAsync(coder.EncodeP2P(message.Type, SignalingJsonCodec.Encode(message)));
                }
                catch (Exception ex)
                {
                    P2PLogHelper.Error(PeerId, ErrorCode.NotConnected, "候选报文补发失败，保留队列等待重连", ex);
                    return;
                }

                queue.RemoveAt(0);
            }

            _candidateOutbox.Remove(sessionId);
        }

        /// <summary>
        /// 编码并发送一条内部报文，连接不可用时返回 false 而不抛异常
        /// </summary>
        /// <param name="message">待发送报文</param>
        /// <returns>已交给 socket 发送时返回 true</returns>
        private bool TrySendFrame(SignalingMessage message)
        {
            SaeaClientSocket socket;
            P2PCoder coder;
            lock (_syncRoot)
            {
                socket = _socket;
                coder = _coder;
            }

            if (socket == null || coder == null || !socket.Connected)
            {
                return false;
            }

            try
            {
                socket.SendAsync(coder.EncodeP2P(message.Type, SignalingJsonCodec.Encode(message)));
                return true;
            }
            catch (Exception ex)
            {
                P2PLogHelper.Error(PeerId, ErrorCode.RegisterServerUnavailable, "信令报文发送失败", ex);
                return false;
            }
        }

        /// <summary>
        /// 启动心跳定时器，已启动时不重复创建
        /// </summary>
        private void StartHeartbeat()
        {
            lock (_syncRoot)
            {
                if (_heartbeatTimer != null)
                {
                    return;
                }

                _heartbeatTimer = new Timer(OnHeartbeatTick, null, _options.HeartbeatIntervalMs, _options.HeartbeatIntervalMs);
            }
        }

        /// <summary>
        /// 停止并释放心跳定时器
        /// </summary>
        private void StopHeartbeat()
        {
            Timer timer;
            lock (_syncRoot)
            {
                timer = _heartbeatTimer;
                _heartbeatTimer = null;
            }

            if (timer != null)
            {
                timer.Dispose();
            }
        }

        /// <summary>
        /// 心跳回调：已连接时发送 Heartbeat，失败只记日志，断线由 socket 事件接管
        /// </summary>
        /// <param name="state">定时器状态参数，未使用</param>
        private void OnHeartbeatTick(object state)
        {
            try
            {
                SaeaClientSocket socket;
                P2PCoder coder;
                lock (_syncRoot)
                {
                    socket = _socket;
                    coder = _coder;
                }

                if (!_isConnected || socket == null || coder == null)
                {
                    return;
                }

                socket.SendAsync(coder.EncodeP2P(P2PMessageType.Heartbeat));
            }
            catch (Exception ex)
            {
                P2PLogHelper.Error(PeerId, ErrorCode.RegisterServerUnavailable, "发送信令心跳失败", ex);
            }
        }

        /// <summary>
        /// 注册失败后复位连接状态，不触发断开事件也不启动重连
        /// </summary>
        private void ReleaseConnection()
        {
            SaeaClientSocket socket;
            lock (_syncRoot)
            {
                socket = _socket;
                _socket = null;
                _coder = null;
                _isConnected = false;
                _isRegistered = false;
                _serverSessionId = null;
                _registerTcs = null;
            }

            StopHeartbeat();
            SafeClose(socket);
        }

        /// <summary>
        /// 安全检查点关闭并释放 socket，异常只记日志
        /// </summary>
        /// <param name="socket">待关闭的连接，可为 null</param>
        private void SafeClose(SaeaClientSocket socket)
        {
            if (socket == null)
            {
                return;
            }

            try
            {
                socket.Disconnect();
            }
            catch (Exception ex)
            {
                P2PLogHelper.Error(PeerId, ErrorCode.NotConnected, "关闭信令连接失败", ex);
            }

            try
            {
                socket.Dispose();
            }
            catch (Exception ex)
            {
                P2PLogHelper.Error(PeerId, ErrorCode.NotConnected, "释放信令连接失败", ex);
            }
        }

        /// <summary>
        /// 校验报文是否允许由客户端发送，并检查该类型必需的字段
        /// </summary>
        /// <param name="message">待发送报文</param>
        private static void ValidateSendableMessage(SignalingMessage message)
        {
            switch (message.Type)
            {
                case P2PMessageType.ConnectRequest:
                    RequireField(message.TargetPeerId, nameof(message.TargetPeerId), message);
                    RequireField(message.SessionId, nameof(message.SessionId), message);
                    break;
                case P2PMessageType.SessionOffer:
                case P2PMessageType.SessionAnswer:
                    RequireField(message.Ufrag, nameof(message.Ufrag), message);
                    RequireField(message.Pwd, nameof(message.Pwd), message);
                    RequireField(message.Role, nameof(message.Role), message);
                    RequireField(message.SessionId, nameof(message.SessionId), message);
                    break;
                case P2PMessageType.TrickleCandidate:
                case P2PMessageType.EndOfCandidates:
                case P2PMessageType.SessionClose:
                    RequireField(message.SessionId, nameof(message.SessionId), message);
                    break;
                default:
                    throw new ArgumentException("消息类型 " + message.Type + " 不能由客户端发送", nameof(message));
            }
        }

        /// <summary>
        /// 校验必需字段非空，缺失时抛 ArgumentException
        /// </summary>
        /// <param name="value">字段值</param>
        /// <param name="fieldName">字段名</param>
        /// <param name="message">所属报文，用于拼错误信息</param>
        private static void RequireField(string value, string fieldName, SignalingMessage message)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("消息类型 " + message.Type + " 缺少必需字段 " + fieldName, nameof(message));
            }
        }

        /// <summary>
        /// 是否为需要进待发队列的候选类报文
        /// </summary>
        /// <param name="type">消息类型</param>
        /// <returns>是候选类报文时返回 true</returns>
        private static bool IsCandidateMessage(P2PMessageType type)
        {
            return type == P2PMessageType.TrickleCandidate || type == P2PMessageType.EndOfCandidates;
        }
    }
}