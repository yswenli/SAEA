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
*命名空间：SAEA.P2P.Turn
*文件名： TurnClient
*版本号： v26.11.5.1
*唯一标识：0f524a14-9075-4bc8-9268-85097c4f0da3
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 05:00:00
*描述：TURN 客户端——鉴权挑战、分配、帧分派与断线重连
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 14:05:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 RFC 6062 控制面：按 REQUESTED-TRANSPORT 申请中继、Connect 取连接号与 ConnectionAttempt 事件
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Common;
using SAEA.P2P.Stun;

namespace SAEA.P2P.Turn
{
    /// <summary>
    /// TURN 客户端：在已打开的 <see cref="ITurnClientTransport"/> 上完成鉴权挑战与 Allocate，
    /// 把收到的 STUN 应答交给事务层、Data 指示与 ChannelData 交给事件，
    /// 并在任一层刷新失败时按指数退避重建传输、重新 Allocate 与重放已登记的 permission / channel。
    /// </summary>
    public sealed class TurnClient : IDisposable
    {
        /// <summary>
        /// 默认分配生命周期，单位秒
        /// </summary>
        private const uint DefaultLifetimeSeconds = 600;

        /// <summary>
        /// 释放事务自身的取消上限：只给 LIFETIME=0 请求一个短超时，避免死服务端把最后一步拖长
        /// </summary>
        private static readonly TimeSpan ReleaseTransactionTimeout = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// 释放时调用方等待应答的上限；必须明显大于 <see cref="ReleaseTransactionTimeout"/>，
        /// 否则等待先到期，正常往返的应答也会被当成释放超时
        /// </summary>
        private static readonly TimeSpan ReleaseWaitTimeout = TimeSpan.FromSeconds(2);

        /// <summary>
        /// TURN 客户端配置
        /// </summary>
        private readonly TurnClientOptions _options;

        /// <summary>
        /// STUN 事务层，按事务 ID 匹配应答并驱动重传
        /// </summary>
        private readonly StunTransactionLayer _transactions;

        /// <summary>
        /// 串行化 Allocate，避免并发调用各自建立一次分配
        /// </summary>
        private readonly SemaphoreSlim _allocateGate = new SemaphoreSlim(1, 1);

        /// <summary>
        /// 释放信号；所有可取消操作都链接到这里
        /// </summary>
        private readonly CancellationTokenSource _disposeCts = new CancellationTokenSource();

        /// <summary>
        /// 重连循环的取消源；释放时立即取消以打断退避等待
        /// </summary>
        private readonly CancellationTokenSource _reconnectCts = new CancellationTokenSource();

        /// <summary>
        /// 保护传输、分配与鉴权状态的同步对象
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 用户名；由凭据入口解析后固定
        /// </summary>
        private readonly string _username;

        /// <summary>
        /// 口令；只用于派生长期密钥，不进入日志与异常
        /// </summary>
        private readonly string _password;

        /// <summary>
        /// 凭据自带的认证域；服务端挑战未给 REALM 时作为回退
        /// </summary>
        private readonly string _credentialRealm;

        /// <summary>
        /// 当前传输；断线重连期间会被替换
        /// </summary>
        private ITurnClientTransport _transport;

        /// <summary>
        /// 当前分配；初次 Allocate 成功后才存在，重连时复用同一实例
        /// </summary>
        private TurnAllocationClient _allocation;

        /// <summary>
        /// 服务端挑战下发的认证域
        /// </summary>
        private string _realm;

        /// <summary>
        /// 服务端挑战下发的 nonce；重连换源地址后必然失效，需要重新挑战
        /// </summary>
        private string _nonce;

        /// <summary>
        /// 长期密钥 MD5(username:realm:password)；为 null 表示服务端未要求鉴权
        /// </summary>
        private byte[] _integrityKey;

        /// <summary>
        /// 是否正在重连；期间对外表现为未连接
        /// </summary>
        private bool _reconnecting;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 创建 TURN 客户端并校验凭据组合
        /// </summary>
        /// <param name="options">客户端配置</param>
        /// <param name="clock">事务层时钟；为 null 时使用系统时间</param>
        /// <exception cref="ArgumentNullException">options 为 null</exception>
        /// <exception cref="InvalidOperationException">凭据入口冲突或缺失</exception>
        public TurnClient(TurnClientOptions options, IRetryClock clock = null)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            options.Validate();
            _options = options;

            // 事务层必须自带计时器，否则刷新请求不会重传也不会超时
            _transactions = new StunTransactionLayer(clock ?? new SystemRetryClock(), TimeSpan.FromMilliseconds(50));

            string username;
            string password;
            string realm;
            ResolveCredential(out username, out password, out realm);
            _username = username;
            _password = password;
            _credentialRealm = realm;
        }

        /// <summary>
        /// 当前分配；尚未 Allocate 或已释放时为 null
        /// </summary>
        public TurnAllocationClient Allocation
        {
            get
            {
                lock (_sync)
                {
                    return _allocation;
                }
            }
        }

        /// <summary>
        /// 是否已连接：传输已打开、存在未过期的分配且不处于重连窗口
        /// </summary>
        public bool IsConnected
        {
            get
            {
                lock (_sync)
                {
                    if (_disposed || _reconnecting || _transport == null || _allocation == null)
                    {
                        return false;
                    }
                    return _transport.IsConnected && !_allocation.IsExpired;
                }
            }
        }

        /// <summary>
        /// 收到 Data 指示时触发；参数为对端地址与应用数据副本
        /// </summary>
        public event Action<IPEndPoint, byte[]> DataReceived;

        /// <summary>
        /// 收到 ChannelData 时触发；参数为通道号与应用数据副本
        /// </summary>
        public event Action<ushort, byte[]> ChannelDataReceived;

        /// <summary>
        /// 断线重连并重新 Allocate 成功后触发；参数为复用后的分配实例
        /// </summary>
        public event Action<TurnAllocationClient> Reallocated;

        /// <summary>
        /// 对端主动连入中继端口时触发；订阅方应据连接号另开数据连接完成 ConnectionBind
        /// </summary>
        public event Action<TurnConnectionAttempt> ConnectionAttemptReceived;

        /// <summary>
        /// 申请分配：先发无鉴权请求，收到 401/438 后用长期密钥签名重试一次。
        /// 第二次仍返回 401/438 时立即抛异常，不会发出第三次请求。
        /// </summary>
        /// <param name="lifetime">请求的生命周期，单位秒；为 null 时由服务端使用默认值</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>分配客户端</returns>
        /// <exception cref="TurnClientException">服务端拒绝、鉴权失败或请求超时</exception>
        public async Task<TurnAllocationClient> AllocateAsync(uint? lifetime = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            ThrowIfDisposed();

            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token))
            {
                await _allocateGate.WaitAsync(linked.Token).ConfigureAwait(false);
                try
                {
                    return await AllocateCoreAsync(lifetime, linked.Token, null, false).ConfigureAwait(false);
                }
                finally
                {
                    _allocateGate.Release();
                }
            }
        }

        /// <summary>
        /// 发起 TURN over TCP 的 Connect：请求服务端向对端建立一条中继连接，并返回服务端分配的连接号
        /// </summary>
        /// <param name="peerEndPoint">对端地址</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>服务端分配的连接号，用于后续 ConnectionBind</returns>
        /// <exception cref="ArgumentNullException">peerEndPoint 为 null</exception>
        /// <exception cref="ObjectDisposedException">客户端已释放</exception>
        /// <exception cref="InvalidOperationException">当前分配不是 TCP 中继</exception>
        /// <exception cref="TurnClientException">服务端拒绝、应答应缺少 CONNECTION-ID 或请求超时</exception>
        public async Task<uint> ConnectAsync(IPEndPoint peerEndPoint, CancellationToken cancellationToken)
        {
            if (peerEndPoint == null)
            {
                throw new ArgumentNullException(nameof(peerEndPoint));
            }
            ThrowIfDisposed();

            if (_options.RequestedTransportProtocol != 6)
            {
                throw new InvalidOperationException("只有 TCP 中继分配才能发起 Connect");
            }

            var response = await SendAuthenticatedAsync(TurnMessage.CreateConnect(peerEndPoint), cancellationToken).ConfigureAwait(false);

            uint connectionId;
            if (!TurnMessage.TryGetConnectionId(response, out connectionId))
            {
                throw new TurnClientException(TurnErrorCode.BadRequest, "Connect 应答应携带 CONNECTION-ID");
            }

            return connectionId;
        }

        /// <summary>
        /// 释放客户端：停定时器、打断重连、尽力发 LIFETIME=0 并释放事务层与传输。重复调用为空操作。
        /// </summary>
        public void Dispose()
        {
            TurnAllocationClient allocation;
            ITurnClientTransport transport;
            byte[] key;
            string realm;
            string nonce;

            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
                allocation = _allocation;
                _allocation = null;
                transport = _transport;
                _transport = null;
                key = _integrityKey;
                realm = _realm;
                nonce = _nonce;
            }

            try
            {
                _reconnectCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // 已释放时取消为空操作
            }
            try
            {
                _disposeCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // 已释放时取消为空操作
            }

            if (allocation != null)
            {
                allocation.Dispose();
            }

            // 尽力删除服务端分配；失败或超时都不影响本地释放
            if (transport != null && key != null && !string.IsNullOrEmpty(nonce))
            {
                try
                {
                    var request = TurnMessage.CreateRefresh(0);
                    var signed = CreateSignedCopy(request, _username, realm, nonce, key);
                    var releasing = RunOnTransportAsync(transport, signed, ReleaseTransactionTimeout);
                    releasing.Wait(ReleaseWaitTimeout);
                }
                catch (Exception ex)
                {
                    P2PLogHelper.Debug("TurnClient", "释放分配失败：" + ex.GetType().Name);
                }
            }

            _transactions.Dispose();

            if (transport != null)
            {
                try
                {
                    transport.FrameReceived -= OnFrameReceived;
                }
                catch (ObjectDisposedException)
                {
                    // 传输已释放时退订为空操作
                }
                try
                {
                    transport.Dispose();
                }
                catch (Exception ex)
                {
                    P2PLogHelper.Debug("TurnClient", "释放传输失败：" + ex.GetType().Name);
                }
            }

            _reconnectCts.Dispose();
            _disposeCts.Dispose();
        }

        /// <summary>
        /// 发送已签名的请求；供分配层刷新、授权与通道绑定使用。
        /// 服务端未要求鉴权时按无鉴权发送；收到 401/438 时换新 nonce 重签一次。
        /// </summary>
        /// <param name="request">未签名的请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>成功应答</returns>
        /// <exception cref="TurnClientException">服务端拒绝或请求超时</exception>
        internal async Task<StunMessage> SendAuthenticatedAsync(StunMessage request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            ThrowIfDisposed();

            byte[] key;
            string realm;
            string nonce;
            lock (_sync)
            {
                key = _integrityKey;
                realm = _realm;
                nonce = _nonce;
            }

            if (key == null || string.IsNullOrEmpty(nonce))
            {
                var plain = await RunRequestAsync(CloneMessage(request), cancellationToken, _options.RefreshTimeout).ConfigureAwait(false);
                if (plain.Class == StunClass.SuccessResponse)
                {
                    return plain;
                }
                throw CreateError(plain);
            }

            var signed = CreateSignedCopy(request, _username, realm, nonce, key);
            var response = await RunRequestAsync(signed, cancellationToken, _options.RefreshTimeout).ConfigureAwait(false);
            if (response.Class == StunClass.SuccessResponse)
            {
                return response;
            }

            var code = GetErrorCode(response);
            if (code != TurnErrorCode.Unauthorized && code != TurnErrorCode.StaleNonce)
            {
                throw CreateError(response);
            }

            var renewedRealm = ReadString(response, StunAttributeType.Realm);
            if (string.IsNullOrEmpty(renewedRealm))
            {
                renewedRealm = realm ?? string.Empty;
            }
            var renewedNonce = ReadString(response, StunAttributeType.Nonce);
            if (string.IsNullOrEmpty(renewedNonce))
            {
                throw new TurnClientException(TurnErrorCode.Unauthorized, "服务端未下发 NONCE");
            }

            var renewedKey = StunIntegrity.CreateLongTermKey(_username, renewedRealm, _password);
            var retry = CreateSignedCopy(request, _username, renewedRealm, renewedNonce, renewedKey);
            var retryResponse = await RunRequestAsync(retry, cancellationToken, _options.RefreshTimeout).ConfigureAwait(false);
            if (retryResponse.Class != StunClass.SuccessResponse)
            {
                throw CreateError(retryResponse);
            }

            lock (_sync)
            {
                _realm = renewedRealm;
                _nonce = renewedNonce;
                _integrityKey = renewedKey;
            }
            return retryResponse;
        }

        /// <summary>
        /// 登记一个一次性定时器；释放后不再执行回调
        /// </summary>
        /// <param name="interval">触发间隔</param>
        /// <param name="action">触发动作</param>
        /// <returns>可释放的定时器句柄</returns>
        internal IDisposable Schedule(TimeSpan interval, Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            lock (_sync)
            {
                if (_disposed)
                {
                    return new NoopDisposable();
                }
            }

            return _options.TimerFactory(
                interval,
                () =>
                {
                    if (IsDisposed)
                    {
                        return;
                    }
                    action();
                });
        }

        /// <summary>
        /// 直接发送一整帧，不做事务登记；用于 Send 指示与 ChannelData
        /// </summary>
        /// <param name="frame">完整帧</param>
        internal void SendFrame(byte[] frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            ITurnClientTransport transport;
            lock (_sync)
            {
                transport = _transport;
            }

            if (transport == null || !transport.IsConnected)
            {
                P2PLogHelper.Debug("TurnClient", "传输未连接，丢弃待发送帧");
                return;
            }

            transport.Send(frame);
        }

        /// <summary>
        /// 当前传输是否为可靠传输；ChannelData 在 TCP 与 TLS 上需要补齐到 4 字节倍数
        /// </summary>
        internal bool IsReliable
        {
            get
            {
                ITurnClientTransport transport;
                lock (_sync)
                {
                    transport = _transport;
                }
                return transport != null && transport.IsReliable;
            }
        }

        /// <summary>
        /// 分配层任一层刷新失败时回调；同一时刻只启动一条重连循环
        /// </summary>
        /// <param name="allocation">失败的分配</param>
        /// <param name="error">失败原因</param>
        internal void OnRefreshFailed(TurnAllocationClient allocation, Exception error)
        {
            lock (_sync)
            {
                if (_disposed || allocation == null || !ReferenceEquals(allocation, _allocation) || _reconnecting)
                {
                    return;
                }
                _reconnecting = true;
            }

            P2PLogHelper.Debug("TurnClient", "刷新失败，开始重连：" + (error == null ? "未知原因" : error.GetType().Name));

            var ignored = Task.Run((Func<Task>)ReconnectLoopAsync);
        }

        /// <summary>
        /// 执行一次 Allocate；首次公开调用与重连共用同一条挑战流程
        /// </summary>
        /// <param name="lifetime">生命周期，单位秒</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="requestTimeout">单次请求等待上限；为 null 时按事务层默认时刻表</param>
        /// <param name="reconnect">是否重连路径；为 true 时复用已有分配实例</param>
        /// <returns>分配客户端</returns>
        private async Task<TurnAllocationClient> AllocateCoreAsync(uint? lifetime, CancellationToken cancellationToken, TimeSpan? requestTimeout, bool reconnect)
        {
            var transport = await EnsureTransportAsync(cancellationToken).ConfigureAwait(false);

            // TCP 中继的数据连接要靠控制连接复用同一套凭据与连接记录，UDP 控制连接承载不了 RFC 6062
            if (_options.RequestedTransportProtocol == 6 && !transport.IsReliable)
            {
                try
                {
                    transport.Dispose();
                }
                catch
                {
                    // 建立到一半的传输可能已在内部释放，这里只关心把异常上级路径挡住
                }
                throw new InvalidOperationException("TCP 中继必须使用 TCP 或 TLS 控制连接");
            }

            var request = TurnMessage.CreateAllocate(_options.RequestedTransportProtocol, lifetime);

            var first = await RunRequestAsync(request, cancellationToken, requestTimeout).ConfigureAwait(false);
            if (first.Class == StunClass.SuccessResponse)
            {
                // 服务端未启用鉴权；后续刷新按无鉴权发送
                return ApplyAllocation(first, transport, reconnect);
            }

            var code = GetErrorCode(first);
            if (code != TurnErrorCode.Unauthorized && code != TurnErrorCode.StaleNonce)
            {
                throw CreateError(first);
            }

            var realm = ReadString(first, StunAttributeType.Realm);
            if (string.IsNullOrEmpty(realm))
            {
                realm = _credentialRealm ?? string.Empty;
            }
            var nonce = ReadString(first, StunAttributeType.Nonce);
            if (string.IsNullOrEmpty(nonce))
            {
                throw new TurnClientException(TurnErrorCode.Unauthorized, "服务端未下发 NONCE");
            }

            var key = StunIntegrity.CreateLongTermKey(_username, realm, _password);
            var signed = CreateSignedCopy(request, _username, realm, nonce, key);
            var second = await RunRequestAsync(signed, cancellationToken, requestTimeout).ConfigureAwait(false);
            if (second.Class != StunClass.SuccessResponse)
            {
                // 只重试一次：这里直接抛出，不再发第三次
                throw CreateError(second);
            }

            lock (_sync)
            {
                _realm = realm;
                _nonce = nonce;
                _integrityKey = key;
            }

            return ApplyAllocation(second, transport, reconnect);
        }

        /// <summary>
        /// 从 Allocate 应答写入分配信息；首次创建实例，重连复用并更新
        /// </summary>
        /// <param name="response">成功应答</param>
        /// <param name="transport">当前传输</param>
        /// <param name="reconnect">是否重连路径</param>
        /// <returns>分配客户端</returns>
        private TurnAllocationClient ApplyAllocation(StunMessage response, ITurnClientTransport transport, bool reconnect)
        {
            IPEndPoint relayed;
            if (!TryGetAddress(response, StunAttributeType.XorRelayedAddress, out relayed))
            {
                throw new TurnClientException(0, "Allocate 应答缺少 XOR-RELAYED-ADDRESS");
            }

            IPEndPoint mapped;
            if (!TryGetAddress(response, StunAttributeType.XorMappedAddress, out mapped))
            {
                mapped = transport.LocalEndPoint;
            }

            uint lifetime;
            if (!TurnMessage.TryGetLifetime(response, out lifetime) || lifetime == 0)
            {
                lifetime = DefaultLifetimeSeconds;
            }

            if (reconnect)
            {
                var existing = Allocation;
                if (existing == null)
                {
                    throw new TurnClientException(0, "重连后缺少分配实例");
                }
                existing.ApplyAllocation(relayed, mapped, lifetime, transport.ServerEndPoint);
                return existing;
            }

            var created = new TurnAllocationClient(
                this,
                relayed,
                mapped,
                lifetime,
                transport.ServerEndPoint,
                _options.AllocationRefreshInterval,
                _options.PermissionRefreshInterval,
                _options.ChannelRefreshInterval);

            lock (_sync)
            {
                _allocation = created;
            }
            created.Start();
            return created;
        }

        /// <summary>
        /// 确保存在已打开的传输；必要时调用工厂新建并订阅帧事件
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>可用的传输</returns>
        private async Task<ITurnClientTransport> EnsureTransportAsync(CancellationToken cancellationToken)
        {
            ITurnClientTransport current;
            lock (_sync)
            {
                ThrowIfDisposedLocked();
                current = _transport;
            }

            if (current != null && current.IsConnected)
            {
                return current;
            }

            var created = _options.TransportFactory();
            if (created == null)
            {
                throw new TurnClientException(0, "传输工厂未返回实例");
            }

            try
            {
                await created.OpenAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                created.Dispose();
                throw;
            }

            ITurnClientTransport previous;
            lock (_sync)
            {
                if (_disposed)
                {
                    created.Dispose();
                    throw new ObjectDisposedException(GetType().Name);
                }
                previous = _transport;
                _transport = created;
            }

            if (previous != null && !ReferenceEquals(previous, created))
            {
                try
                {
                    previous.FrameReceived -= OnFrameReceived;
                }
                catch (ObjectDisposedException)
                {
                    // 旧传输已释放时退订为空操作
                }
                try
                {
                    previous.Dispose();
                }
                catch (Exception ex)
                {
                    P2PLogHelper.Debug("TurnClient", "释放旧传输失败：" + ex.GetType().Name);
                }
            }

            created.FrameReceived += OnFrameReceived;
            return created;
        }

        /// <summary>
        /// 注册事务并把应答交回调用方；超时、取消与传输失败统一转成 <see cref="TurnClientException"/>
        /// </summary>
        /// <param name="message">待发送报文</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="timeout">等待上限；为 null 时按事务层默认时刻表</param>
        /// <returns>应答消息</returns>
        private async Task<StunMessage> RunRequestAsync(StunMessage message, CancellationToken cancellationToken, TimeSpan? timeout)
        {
            ITurnClientTransport transport;
            lock (_sync)
            {
                ThrowIfDisposedLocked();
                transport = _transport;
            }
            if (transport == null || !transport.IsConnected)
            {
                throw new TurnClientException(0, "TURN 传输未连接");
            }

            StunMessage response;
            using (var timeoutCts = timeout.HasValue ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken) : null)
            {
                var token = cancellationToken;
                if (timeoutCts != null)
                {
                    timeoutCts.CancelAfter(timeout.Value);
                    token = timeoutCts.Token;
                }

                StunTransaction transaction;
                if (!_transactions.TryBegin(message, transport.IsReliable, frame => transport.Send(frame), out transaction, _options.RtoMilliseconds, token))
                {
                    throw new TurnClientException(0, "TURN 事务注册失败");
                }

                var result = await transaction.Completion.ConfigureAwait(false);
                if (result.Succeeded)
                {
                    response = result.Response;
                }
                else
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }
                    if (result.Failure == StunTransactionFailure.Timeout || (timeoutCts != null && timeoutCts.IsCancellationRequested))
                    {
                        throw new TurnClientException(0, "TURN 请求超时");
                    }
                    if (result.Failure == StunTransactionFailure.Cancelled)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }
                    throw new TurnClientException(0, "TURN 传输失败");
                }
            }

            return response;
        }

        /// <summary>
        /// 在指定传输上发一个事务并等待完成；用于释放阶段，绕过已置位的释放标志
        /// </summary>
        /// <param name="transport">目标传输</param>
        /// <param name="message">待发送报文</param>
        /// <param name="timeout">等待上限</param>
        /// <returns>等待任务</returns>
        private async Task RunOnTransportAsync(ITurnClientTransport transport, StunMessage message, TimeSpan timeout)
        {
            using (var cts = new CancellationTokenSource(timeout))
            {
                StunTransaction transaction;
                if (!_transactions.TryBegin(message, transport.IsReliable, frame => transport.Send(frame), out transaction, _options.RtoMilliseconds, cts.Token))
                {
                    return;
                }
                await transaction.Completion.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 重连循环：先按当前退避等待，再重建传输与分配；失败则退避翻倍直到上限
        /// </summary>
        /// <returns>重连结束任务</returns>
        private async Task ReconnectLoopAsync()
        {
            var delay = _options.ReconnectBackoffInitial;
            var token = _reconnectCts.Token;

            while (!token.IsCancellationRequested && !IsDisposed)
            {
                try
                {
                    await Task.Delay(delay, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (token.IsCancellationRequested || IsDisposed)
                {
                    return;
                }

                try
                {
                    await ReconnectOnceAsync(token).ConfigureAwait(false);
                    return;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    P2PLogHelper.Debug("TurnClient", "重连失败，稍后重试：" + ex.GetType().Name);
                }

                var next = delay.TotalMilliseconds * 2;
                var cap = _options.ReconnectBackoffMax.TotalMilliseconds;
                delay = TimeSpan.FromMilliseconds(Math.Min(next, cap));
            }
        }

        /// <summary>
        /// 执行一次重连：新传输、新 Allocate、重放 permission 与 channel，最后触发 <see cref="Reallocated"/>
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>重连任务</returns>
        private async Task ReconnectOnceAsync(CancellationToken cancellationToken)
        {
            var created = _options.TransportFactory();
            if (created == null)
            {
                throw new TurnClientException(0, "传输工厂未返回实例");
            }

            try
            {
                await created.OpenAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                created.Dispose();
                throw;
            }

            ITurnClientTransport previous;
            lock (_sync)
            {
                if (_disposed)
                {
                    created.Dispose();
                    throw new ObjectDisposedException(GetType().Name);
                }
                previous = _transport;
                _transport = created;
            }

            if (previous != null && !ReferenceEquals(previous, created))
            {
                try
                {
                    previous.FrameReceived -= OnFrameReceived;
                }
                catch (ObjectDisposedException)
                {
                    // 旧传输已释放时退订为空操作
                }
                try
                {
                    previous.Dispose();
                }
                catch (Exception ex)
                {
                    P2PLogHelper.Debug("TurnClient", "释放旧传输失败：" + ex.GetType().Name);
                }
            }

            created.FrameReceived += OnFrameReceived;

            try
            {
                // 刷新路径用更短的等待上限，避免死服务端把一次重连拖满事务时刻表
                await AllocateCoreAsync(null, cancellationToken, _options.RefreshTimeout, true).ConfigureAwait(false);

                var allocation = Allocation;
                if (allocation == null)
                {
                    throw new TurnClientException(0, "重连后缺少分配实例");
                }

                await allocation.RestoreAsync(cancellationToken).ConfigureAwait(false);
                allocation.MarkRestored();
            }
            catch
            {
                DetachTransport(created);
                throw;
            }

            lock (_sync)
            {
                _reconnecting = false;
            }

            RaiseReallocated(Allocation);
        }

        /// <summary>
        /// 把失败的重连传输从客户端摘除并释放
        /// </summary>
        /// <param name="transport">失败的传输</param>
        private void DetachTransport(ITurnClientTransport transport)
        {
            lock (_sync)
            {
                if (ReferenceEquals(_transport, transport))
                {
                    _transport = null;
                }
            }

            try
            {
                transport.FrameReceived -= OnFrameReceived;
            }
            catch (ObjectDisposedException)
            {
                // 已释放时退订为空操作
            }
            try
            {
                transport.Dispose();
            }
            catch (Exception ex)
            {
                P2PLogHelper.Debug("TurnClient", "释放失败传输失败：" + ex.GetType().Name);
            }
        }

        /// <summary>
        /// 传输帧回调：STUN 交事务层，Data 指示与 ChannelData 交事件，非法帧静默丢弃。
        /// 释放阶段只丢用户事件，STUN 应答仍交事务层。
        /// </summary>
        /// <param name="frame">完整帧副本</param>
        private void OnFrameReceived(byte[] frame)
        {
            if (frame == null || frame.Length == 0)
            {
                return;
            }

            // 只取一次，避免每次判断都进锁；释放阶段仍要接收 STUN 应答，
            // 因为 Dispose 先置位释放标志再发 LIFETIME=0，整体丢弃会让该请求只能等超时
            var disposed = IsDisposed;

            try
            {
                // STUN 首字节高两位为 0；其余首字节先按 ChannelData 解析，非法通道号静默丢弃
                if ((frame[0] & 0xC0) != 0)
                {
                    // 释放后不再触发用户事件，ChannelData 直接丢弃
                    if (disposed)
                    {
                        return;
                    }
                    HandleChannelDataFrame(frame);
                    return;
                }

                StunMessage message;
                StunParseError error;
                if (!StunMessage.TryParse(frame, out message, out error))
                {
                    P2PLogHelper.Debug("TurnClient", "丢弃无法解析的 STUN 报文：" + error);
                    return;
                }

                if (message.Class == StunClass.Indication && message.Method == StunMessageType.Data)
                {
                    // 释放后不再触发用户事件
                    if (disposed)
                    {
                        return;
                    }
                    HandleDataIndication(message);
                    return;
                }

                if (message.Class == StunClass.Indication && message.Method == StunMessageType.ConnectionAttempt)
                {
                    // 释放后不再触发用户事件
                    if (disposed)
                    {
                        return;
                    }
                    HandleConnectionAttempt(message);
                    return;
                }

                // 应答无论是否已释放都交事务层：层已释放时只会查不到事务并记调试日志
                _transactions.HandleResponse(message);
            }
            catch (Exception ex)
            {
                // 回调异常不能打断传输层的接收循环
                P2PLogHelper.Debug("TurnClient", "处理帧失败：" + ex.GetType().Name);
            }
        }

        /// <summary>
        /// 解析 ChannelData 并触发事件；通道号非法或长度不足时记调试日志后丢弃
        /// </summary>
        /// <param name="frame">完整帧</param>
        private void HandleChannelDataFrame(byte[] frame)
        {
            ushort channelNumber;
            ReadOnlySpan<byte> payload;
            int consumed;
            if (!ChannelData.TryDecode(frame, IsReliable, out channelNumber, out payload, out consumed))
            {
                P2PLogHelper.Debug("TurnClient", "丢弃非法通道号或长度不足的 ChannelData");
                return;
            }

            RaiseChannelData(channelNumber, payload.ToArray());
        }

        /// <summary>
        /// 解析 Data 指示并触发事件
        /// </summary>
        /// <param name="message">Data 指示</param>
        private void HandleDataIndication(StunMessage message)
        {
            IPEndPoint peer;
            if (!TurnMessage.TryGetPeerAddress(message, out peer))
            {
                P2PLogHelper.Debug("TurnClient", "丢弃缺少 XOR-PEER-ADDRESS 的 Data 指示");
                return;
            }

            ReadOnlyMemory<byte> data;
            if (!TurnMessage.TryGetData(message, out data))
            {
                P2PLogHelper.Debug("TurnClient", "丢弃缺少 DATA 的 Data 指示");
                return;
            }

            RaiseDataReceived(peer, data.ToArray());
        }

        /// <summary>
        /// 解析 ConnectionAttempt 指示并触发事件；缺属性或完整性校验失败时丢弃
        /// </summary>
        /// <param name="message">ConnectionAttempt 指示</param>
        private void HandleConnectionAttempt(StunMessage message)
        {
            uint connectionId;
            if (!TurnMessage.TryGetConnectionId(message, out connectionId))
            {
                P2PLogHelper.Debug("TurnClient", "丢弃缺少 CONNECTION-ID 的 ConnectionAttempt");
                return;
            }

            IPEndPoint peer;
            if (!TurnMessage.TryGetPeerAddress(message, out peer))
            {
                P2PLogHelper.Debug("TurnClient", "丢弃缺少 XOR-PEER-ADDRESS 的 ConnectionAttempt");
                return;
            }

            byte[] key;
            lock (_sync)
            {
                key = _integrityKey;
            }

            // 服务端要求鉴权时连接号必须可证明来源；未启用鉴权时没有密钥可校验
            if (key != null && !StunIntegrity.TryVerify(message, key))
            {
                P2PLogHelper.Debug("TurnClient", "丢弃 MESSAGE-INTEGRITY 校验失败的 ConnectionAttempt");
                return;
            }

            RaiseConnectionAttempt(new TurnConnectionAttempt(connectionId, peer));
        }

        /// <summary>
        /// 逐订阅者触发 ConnectionAttempt 事件；单个订阅者异常不影响其余订阅者
        /// </summary>
        /// <param name="attempt">连接尝试</param>
        private void RaiseConnectionAttempt(TurnConnectionAttempt attempt)
        {
            var handler = ConnectionAttemptReceived;
            if (handler == null)
            {
                return;
            }

            var subscribers = handler.GetInvocationList();
            for (var i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    ((Action<TurnConnectionAttempt>)subscribers[i])(attempt);
                }
                catch (Exception ex)
                {
                    P2PLogHelper.Debug("TurnClient", "ConnectionAttempt 事件订阅者异常：" + ex.GetType().Name);
                }
            }
        }

        /// <summary>
        /// 逐订阅者触发 Data 事件；单个订阅者异常不影响其余订阅者
        /// </summary>
        /// <param name="peer">对端地址</param>
        /// <param name="data">应用数据副本</param>
        private void RaiseDataReceived(IPEndPoint peer, byte[] data)
        {
            var handler = DataReceived;
            if (handler == null)
            {
                return;
            }

            var subscribers = handler.GetInvocationList();
            for (var i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    ((Action<IPEndPoint, byte[]>)subscribers[i])(peer, data);
                }
                catch (Exception ex)
                {
                    P2PLogHelper.Debug("TurnClient", "Data 事件订阅者异常：" + ex.GetType().Name);
                }
            }
        }

        /// <summary>
        /// 逐订阅者触发 ChannelData 事件；单个订阅者异常不影响其余订阅者
        /// </summary>
        /// <param name="channelNumber">通道号</param>
        /// <param name="data">应用数据副本</param>
        private void RaiseChannelData(ushort channelNumber, byte[] data)
        {
            var handler = ChannelDataReceived;
            if (handler == null)
            {
                return;
            }

            var subscribers = handler.GetInvocationList();
            for (var i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    ((Action<ushort, byte[]>)subscribers[i])(channelNumber, data);
                }
                catch (Exception ex)
                {
                    P2PLogHelper.Debug("TurnClient", "ChannelData 事件订阅者异常：" + ex.GetType().Name);
                }
            }
        }

        /// <summary>
        /// 逐订阅者触发重连完成事件
        /// </summary>
        /// <param name="allocation">复用后的分配</param>
        private void RaiseReallocated(TurnAllocationClient allocation)
        {
            var handler = Reallocated;
            if (handler == null || allocation == null)
            {
                return;
            }

            var subscribers = handler.GetInvocationList();
            for (var i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    ((Action<TurnAllocationClient>)subscribers[i])(allocation);
                }
                catch (Exception ex)
                {
                    P2PLogHelper.Debug("TurnClient", "Reallocated 事件订阅者异常：" + ex.GetType().Name);
                }
            }
        }

        /// <summary>
        /// 解析凭据入口；Credential 与 Username/Password 互斥，异常消息不含口令
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">口令</param>
        /// <param name="realm">凭据自带认证域</param>
        private void ResolveCredential(out string username, out string password, out string realm)
        {
            var hasUserName = !string.IsNullOrEmpty(_options.Username);
            var hasPassword = !string.IsNullOrEmpty(_options.Password);
            var hasDirect = hasUserName || hasPassword;

            if (_options.Credential != null && hasDirect)
            {
                throw new InvalidOperationException("Credential 与 Username/Password 只能二选一");
            }

            if (_options.Credential != null)
            {
                username = _options.Credential.Username;
                password = _options.Credential.Password;
                realm = _options.Credential.Realm;
                return;
            }

            if (!hasUserName || !hasPassword)
            {
                throw new InvalidOperationException("必须提供 Credential 或 Username/Password");
            }

            username = _options.Username;
            password = _options.Password;
            realm = null;
        }

        /// <summary>
        /// 复制一条消息并改成带鉴权属性的版本；事务 ID 与已有属性保持不变，保证地址属性的异或编码仍然有效
        /// </summary>
        /// <param name="request">原请求</param>
        /// <param name="username">用户名</param>
        /// <param name="realm">认证域</param>
        /// <param name="nonce">nonce</param>
        /// <param name="key">长期密钥</param>
        /// <returns>已签名请求</returns>
        private static StunMessage CreateSignedCopy(StunMessage request, string username, string realm, string nonce, byte[] key)
        {
            var attributes = new List<StunAttribute>();
            for (var i = 0; i < request.Attributes.Count; i++)
            {
                var attribute = request.Attributes[i];
                if (attribute.Type == (ushort)StunAttributeType.Username
                    || attribute.Type == (ushort)StunAttributeType.Realm
                    || attribute.Type == (ushort)StunAttributeType.Nonce
                    || attribute.Type == (ushort)StunAttributeType.MessageIntegrity)
                {
                    continue;
                }
                attributes.Add(attribute);
            }

            attributes.Add(new StunAttribute(StunAttributeType.Username, Encoding.UTF8.GetBytes(username)));
            if (!string.IsNullOrEmpty(realm))
            {
                attributes.Add(new StunAttribute(StunAttributeType.Realm, Encoding.UTF8.GetBytes(realm)));
            }
            attributes.Add(new StunAttribute(StunAttributeType.Nonce, Encoding.UTF8.GetBytes(nonce)));

            var copy = new StunMessage(request.Method, request.Class, request.TransactionId.ToArray());
            copy.SetAttributes(attributes);
            StunIntegrity.Append(copy, key);
            return copy;
        }

        /// <summary>
        /// 复制一条消息，保持方法、类别、事务 ID 与属性不变
        /// </summary>
        /// <param name="request">原请求</param>
        /// <returns>副本</returns>
        private static StunMessage CloneMessage(StunMessage request)
        {
            var copy = new StunMessage(request.Method, request.Class, request.TransactionId.ToArray());
            copy.SetAttributes(request.Attributes);
            return copy;
        }

        /// <summary>
        /// 读取指定类型的字符串属性；缺失时返回 null
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="type">属性类型</param>
        /// <returns>UTF-8 字符串</returns>
        private static string ReadString(StunMessage message, StunAttributeType type)
        {
            StunAttribute attribute;
            if (!message.TryGet(type, out attribute))
            {
                return null;
            }
            return Encoding.UTF8.GetString(attribute.Value.ToArray());
        }

        /// <summary>
        /// 解码异或地址属性
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="type">属性类型</param>
        /// <param name="endPoint">解码出的端点</param>
        /// <returns>存在且能解码时返回 true</returns>
        private static bool TryGetAddress(StunMessage message, StunAttributeType type, out IPEndPoint endPoint)
        {
            endPoint = null;
            StunAttribute attribute;
            if (!message.TryGet(type, out attribute))
            {
                return false;
            }
            return StunAddressCodec.TryDecodeAddress(attribute, message.TransactionId.Span, out endPoint);
        }

        /// <summary>
        /// 读取错误码；缺失时返回 0
        /// </summary>
        /// <param name="response">应答</param>
        /// <returns>错误码</returns>
        private static int GetErrorCode(StunMessage response)
        {
            int code;
            string reason;
            return StunErrorCode.TryGet(response, out code, out reason) ? code : 0;
        }

        /// <summary>
        /// 把错误应答转成异常；缺少错误码时按本机失败处理
        /// </summary>
        /// <param name="response">错误应答</param>
        /// <returns>异常</returns>
        private static TurnClientException CreateError(StunMessage response)
        {
            int code;
            string reason;
            if (!StunErrorCode.TryGet(response, out code, out reason) || code == 0)
            {
                return new TurnClientException(0, "TURN 应答缺少错误码");
            }
            return new TurnClientException(code, reason);
        }

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool IsDisposed
        {
            get
            {
                lock (_sync)
                {
                    return _disposed;
                }
            }
        }

        /// <summary>
        /// 释放前检查；已释放时抛 <see cref="ObjectDisposedException"/>
        /// </summary>
        private void ThrowIfDisposed()
        {
            lock (_sync)
            {
                ThrowIfDisposedLocked();
            }
        }

        /// <summary>
        /// 释放前检查；调用方必须已持有锁
        /// </summary>
        private void ThrowIfDisposedLocked()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        /// <summary>
        /// 空定时器句柄，用于释放后调用 <see cref="Schedule"/> 的路径
        /// </summary>
        private sealed class NoopDisposable : IDisposable
        {
            /// <summary>
            /// 空操作
            /// </summary>
            public void Dispose()
            {
            }
        }
    }
}