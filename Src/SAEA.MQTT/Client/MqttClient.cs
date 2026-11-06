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
*命名空间：SAEA.MQTT.Client
*文件名： MqttClient
*版本号： v26.4.23.1
*唯一标识：b1e00558-5181-4a21-a73f-063b1f6dff56
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClient接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.MQTT.Adapter;
using SAEA.MQTT.Client.Connecting;
using SAEA.MQTT.Client.Disconnecting;
using SAEA.MQTT.Client.ExtendedAuthenticationExchange;
using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Client.Publishing;
using SAEA.MQTT.Client.Receiving;
using SAEA.MQTT.Client.Subscribing;
using SAEA.MQTT.Client.Unsubscribing;
using SAEA.MQTT.Diagnostics;
using SAEA.MQTT.Exceptions;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Internal;
using SAEA.MQTT.PacketDispatcher;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.Client
{
    /// <summary>
    /// MQTT 客户端实现，负责连接管理、报文收发、保活与主题别名处理
    /// </summary>
    public class MqttClient : Disposable, IMqttClient
    {
        readonly MqttPacketIdentifierProvider _packetIdentifierProvider = new MqttPacketIdentifierProvider();
        readonly MqttPacketDispatcher _packetDispatcher = new MqttPacketDispatcher();
        readonly Stopwatch _sendTracker = new Stopwatch();
        readonly Stopwatch _receiveTracker = new Stopwatch();
        readonly object _disconnectLock = new object();

        readonly IMqttClientAdapterFactory _adapterFactory;
        readonly IMqttNetScopedLogger _logger;

        CancellationTokenSource _backgroundCancellationTokenSource;
        Task _packetReceiverTask;
        Task _keepAlivePacketsSenderTask;
        Task _publishPacketReceiverTask;

        AsyncQueue<MqttPublishPacket> _publishPacketReceiverQueue;

        IMqttChannelAdapter _adapter;
        bool _cleanDisconnectInitiated;
        long _isDisconnectPending;
        bool _isConnected;
        MqttClientDisconnectReason _disconnectReason;

        MqttConnectionCapabilities _capabilities;
        MqttInboundFlowControl _inboundFlowControl;
        MqttTopicAliasResolver _topicAliasResolver;
        MqttTopicAliasAssigner _topicAliasAssigner;

        // 出站 Receive Maximum 配额：由服务端 CONNACK 决定，每次连接重建。
        SemaphoreSlim _publishQuota;
        CancellationTokenSource _publishQuotaCancellationTokenSource;

        /// <summary>
        /// 创建 MQTT 客户端
        /// </summary>
        /// <param name="channelFactory">客户端通道适配器工厂，不能为 null</param>
        /// <param name="logger">日志记录器，不能为 null</param>
        public MqttClient(IMqttClientAdapterFactory channelFactory, IMqttNetLogger logger)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            _adapterFactory = channelFactory ?? throw new ArgumentNullException(nameof(channelFactory));
            _logger = logger.CreateScopedLogger(nameof(MqttClient));
        }

        /// <summary>
        /// 连接建立后的回调处理器，null 表示不处理
        /// </summary>
        public IMqttClientConnectedHandler ConnectedHandler { get; set; }

        /// <summary>
        /// 连接断开后的回调处理器，null 表示不处理
        /// </summary>
        public IMqttClientDisconnectedHandler DisconnectedHandler { get; set; }

        /// <summary>
        /// 收到应用消息时的回调处理器，null 表示不处理
        /// </summary>
        public IMqttApplicationMessageReceivedHandler ApplicationMessageReceivedHandler { get; set; }

        /// <summary>
        /// 是否已连接且未处于断开过程中
        /// </summary>
        public bool IsConnected => _isConnected && Interlocked.Read(ref _isDisconnectPending) == 0;

        /// <summary>
        /// 最近一次 ConnectAsync 使用的选项，尚未调用过 ConnectAsync 时为 null
        /// </summary>
        public IMqttClientOptions Options { get; private set; }

        /// <summary>
        /// 连接服务端并等待 CONNACK，成功后启动收包与保活任务
        /// </summary>
        /// <param name="options">连接选项，不能为 null 且必须设置 ChannelOptions</param>
        /// <param name="cancellationToken">取消令牌，同时作用于连接建立与认证</param>
        /// <returns>服务端返回的连接认证结果</returns>
        public async Task<MqttClientAuthenticateResult> ConnectAsync(IMqttClientOptions options, CancellationToken cancellationToken)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options.ChannelOptions == null) throw new ArgumentException("ChannelOptions are not set.");

            ThrowIfConnected("It is not allowed to connect with a server after the connection is established.");

            ThrowIfDisposed();

            MqttClientAuthenticateResult authenticateResult = null;

            try
            {
                Options = options;

                _packetIdentifierProvider.Reset();
                _packetDispatcher.Cancel();

                _backgroundCancellationTokenSource = new CancellationTokenSource();
                var backgroundCancellationToken = _backgroundCancellationTokenSource.Token;

                _isDisconnectPending = 0;
                var adapter = _adapterFactory.CreateClientAdapter(options);
                _adapter = adapter;

                _capabilities = new MqttConnectionCapabilities
                {
                    ProtocolVersion = options.ProtocolVersion,
                    TopicAliasMaximum = options.TopicAliasMaximum ?? 0,
                    ReceiveMaximum = options.ReceiveMaximum ?? ushort.MaxValue,
                    MaximumPacketSize = options.MaximumPacketSize ?? 268435455
                };

                var mqttChannelAdapter = adapter as MqttChannelAdapter;
                if (mqttChannelAdapter != null)
                {
                    mqttChannelAdapter.MaximumInboundPacketSize = _capabilities.MaximumPacketSize;
                    // CONNACK 之前不知道对端上限，先不限制出站。
                    mqttChannelAdapter.MaximumPacketSize = 268435455;
                }

                _inboundFlowControl = new MqttInboundFlowControl(_capabilities.ReceiveMaximum);

                adapter.PacketFormatterAdapter.AttachCapabilities(_capabilities);
                _topicAliasResolver = new MqttTopicAliasResolver(_capabilities.TopicAliasMaximum);
                _topicAliasAssigner = null;

                using (var combined = CancellationTokenSource.CreateLinkedTokenSource(backgroundCancellationToken, cancellationToken))
                {
                    _logger.Verbose($"Trying to connect with server '{options.ChannelOptions}' (Timeout={options.CommunicationTimeout}).");
                    await _adapter.ConnectAsync(options.CommunicationTimeout, combined.Token).ConfigureAwait(false);
                    _logger.Verbose("Connection with server established.");

                    _publishPacketReceiverQueue = new AsyncQueue<MqttPublishPacket>();
                    _publishPacketReceiverTask = Task.Run(() => ProcessReceivedPublishPackets(backgroundCancellationToken), backgroundCancellationToken);

                    _packetReceiverTask = Task.Run(() => TryReceivePacketsAsync(backgroundCancellationToken), backgroundCancellationToken);

                    authenticateResult = await AuthenticateAsync(adapter, options.WillMessage, combined.Token).ConfigureAwait(false);
                }

                // CONNACK 中的 TopicAliasMaximum 约束客户端出站别名；每次连接重新创建，绝不跨连接复用。
                _capabilities.ClientTopicAliasMaximum = authenticateResult.TopicAliasMaximum ?? 0;
                _topicAliasAssigner = new MqttTopicAliasAssigner(_capabilities.ClientTopicAliasMaximum);

                // CONNACK 中的 Maximum Packet Size 约束客户端出站。
                _capabilities.ClientMaximumPacketSize = authenticateResult.MaximumPacketSize ?? 268435455;
                if (mqttChannelAdapter != null)
                {
                    mqttChannelAdapter.MaximumPacketSize = _capabilities.ClientMaximumPacketSize;
                }

                // 服务端 Receive Maximum 约束客户端 QoS>0 的在途发布数量。
                _capabilities.ClientReceiveMaximum = authenticateResult.ReceiveMaximum ?? ushort.MaxValue;
                InitializePublishQuota(_capabilities.ClientReceiveMaximum);

                _sendTracker.Restart();
                _receiveTracker.Restart();

                if (Options.KeepAlivePeriod != TimeSpan.Zero)
                {
                    _keepAlivePacketsSenderTask = Task.Run(() => TrySendKeepAliveMessagesAsync(backgroundCancellationToken), backgroundCancellationToken);
                }

                _isConnected = true;

                _logger.Info("Connected.");

                var connectedHandler = ConnectedHandler;
                if (connectedHandler != null)
                {
                    await connectedHandler.HandleConnectedAsync(new MqttClientConnectedEventArgs(authenticateResult)).ConfigureAwait(false);
                }

                return authenticateResult;
            }
            catch (Exception exception)
            {
                _disconnectReason = MqttClientDisconnectReason.UnspecifiedError;

                _logger.Error(exception, "Error while connecting with server.");

                if (!DisconnectIsPending())
                {
                    await DisconnectInternalAsync(null, exception, authenticateResult).ConfigureAwait(false);
                }

                throw;
            }
        }

        /// <summary>
        /// 发送 DISCONNECT 报文并释放连接资源；CONNECT 中会话过期时间为 0 时，此处传入非 0 值会抛出协议异常
        /// </summary>
        /// <param name="options">断开选项，不能为 null</param>
        /// <param name="cancellationToken">取消令牌，仅作用于发送 DISCONNECT 报文</param>
        /// <returns>断开完成的任务</returns>
        public async Task DisconnectAsync(MqttClientDisconnectOptions options, CancellationToken cancellationToken)
        {
            if (options is null) throw new ArgumentNullException(nameof(options));

            ThrowIfDisposed();

            if (options.SessionExpiryInterval.HasValue &&
                options.SessionExpiryInterval.Value != 0 &&
                (Options.SessionExpiryInterval ?? 0) == 0)
            {
                throw new MqttProtocolViolationException(
                    "It is not allowed to set a non-zero session expiry interval in the DISCONNECT packet when it was zero in the CONNECT packet.");
            }

            try
            {
                // 不覆盖用户指定的原因码；未指定时 MqttClientDisconnectOptions 默认 NormalDisconnection。
                _disconnectReason = (MqttClientDisconnectReason)options.ReasonCode;
                _cleanDisconnectInitiated = true;

                if (IsConnected)
                {
                    var disconnectPacket = _adapter.PacketFormatterAdapter.DataConverter.CreateDisconnectPacket(options);
                    await SendAsync(disconnectPacket, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                if (!DisconnectIsPending())
                {
                    await DisconnectInternalAsync(null, null, null).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// 发送 PINGREQ 并等待服务端的 PINGRESP
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>收到 PINGRESP 后完成的任务</returns>
        public Task PingAsync(CancellationToken cancellationToken)
        {
            return SendAndReceiveAsync<MqttPingRespPacket>(new MqttPingReqPacket(), cancellationToken);
        }

        /// <summary>
        /// 回复服务端的 AUTH 报文，认证方法沿用 CONNECT 中提交的值
        /// </summary>
        /// <param name="data">要发送的认证数据，不能为 null</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>报文发出后完成的任务</returns>
        public Task SendExtendedAuthenticationExchangeDataAsync(MqttExtendedAuthenticationExchangeData data, CancellationToken cancellationToken)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            ThrowIfDisposed();
            ThrowIfNotConnected();

            return SendAsync(new MqttAuthPacket
            {
                Properties = new MqttAuthPacketProperties
                {
                    // This must always be equal to the value from the CONNECT packet. So we use it here to ensure that.
                    AuthenticationMethod = Options.AuthenticationMethod,
                    AuthenticationData = data.AuthenticationData,
                    ReasonString = data.ReasonString,
                    UserProperties = data.UserProperties
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 发送 SUBSCRIBE 报文并等待 SUBACK
        /// </summary>
        /// <param name="options">订阅选项，不能为 null</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>包含各主题过滤器授权 QoS 的订阅结果</returns>
        public async Task<MqttClientSubscribeResult> SubscribeAsync(MqttClientSubscribeOptions options, CancellationToken cancellationToken)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            ThrowIfDisposed();
            ThrowIfNotConnected();

            var subscribePacket = _adapter.PacketFormatterAdapter.DataConverter.CreateSubscribePacket(options);
            subscribePacket.PacketIdentifier = _packetIdentifierProvider.GetNextPacketIdentifier();

            var subAckPacket = await SendAndReceiveAsync<MqttSubAckPacket>(subscribePacket, cancellationToken).ConfigureAwait(false);
            return _adapter.PacketFormatterAdapter.DataConverter.CreateClientSubscribeResult(subscribePacket, subAckPacket);
        }

        /// <summary>
        /// 发送 UNSUBSCRIBE 报文并等待 UNSUBACK
        /// </summary>
        /// <param name="options">取消订阅选项，不能为 null</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>包含各主题过滤器处理结果的取消订阅结果</returns>
        public async Task<MqttClientUnsubscribeResult> UnsubscribeAsync(MqttClientUnsubscribeOptions options, CancellationToken cancellationToken)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            ThrowIfDisposed();
            ThrowIfNotConnected();

            var unsubscribePacket = _adapter.PacketFormatterAdapter.DataConverter.CreateUnsubscribePacket(options);
            unsubscribePacket.PacketIdentifier = _packetIdentifierProvider.GetNextPacketIdentifier();

            var unsubAckPacket = await SendAndReceiveAsync<MqttUnsubAckPacket>(unsubscribePacket, cancellationToken).ConfigureAwait(false);
            return _adapter.PacketFormatterAdapter.DataConverter.CreateClientUnsubscribeResult(unsubscribePacket, unsubAckPacket);
        }

        /// <summary>
        /// 发布应用消息，按消息的 QoS 等级选择对应的确认流程，主题不合法时抛出协议异常
        /// </summary>
        /// <param name="applicationMessage">待发布的消息</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>发布结果；QoS 大于 0 时需先占用服务端协商的 Receive Maximum 配额</returns>
        public async Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken)
        {
            MqttTopicValidator.ThrowIfInvalid(applicationMessage);

            ThrowIfDisposed();
            ThrowIfNotConnected();

            var publishPacket = _adapter.PacketFormatterAdapter.DataConverter.CreatePublishPacket(applicationMessage);

            ApplyOutboundTopicAlias(publishPacket, applicationMessage);

            if (applicationMessage.QualityOfServiceLevel == MqttQualityOfServiceLevel.AtMostOnce)
            {
                // QoS0 不占用 Receive Maximum 配额。
                return await PublishAtMostOnce(publishPacket, cancellationToken).ConfigureAwait(false);
            }

            var quota = await AcquirePublishQuotaAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                switch (applicationMessage.QualityOfServiceLevel)
                {
                    case MqttQualityOfServiceLevel.AtLeastOnce:
                        {
                            return await PublishAtLeastOnceAsync(publishPacket, cancellationToken).ConfigureAwait(false);
                        }
                    case MqttQualityOfServiceLevel.ExactlyOnce:
                        {
                            return await PublishExactlyOnceAsync(publishPacket, cancellationToken).ConfigureAwait(false);
                        }
                    default:
                        {
                            throw new NotSupportedException();
                        }
                }
            }
            finally
            {
                ReleasePublishQuota(quota);
            }
        }

        async Task<SemaphoreSlim> AcquirePublishQuotaAsync(CancellationToken cancellationToken)
        {
            var quota = _publishQuota;
            if (quota == null)
            {
                return null;
            }

            var quotaCancellationTokenSource = _publishQuotaCancellationTokenSource;
            if (quotaCancellationTokenSource == null)
            {
                await quota.WaitAsync(cancellationToken).ConfigureAwait(false);
                return quota;
            }

            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, quotaCancellationTokenSource.Token))
            {
                await quota.WaitAsync(linked.Token).ConfigureAwait(false);
            }

            return quota;
        }

        void ReleasePublishQuota(SemaphoreSlim quota)
        {
            if (quota == null)
            {
                return;
            }

            try
            {
                quota.Release();
            }
            catch (ObjectDisposedException)
            {
                // 连接已重建，旧配额不再需要归还。
            }
            catch (SemaphoreFullException)
            {
            }
        }

        void InitializePublishQuota(ushort receiveMaximum)
        {
            var oldCancellationTokenSource = _publishQuotaCancellationTokenSource;
            var oldQuota = _publishQuota;

            _publishQuotaCancellationTokenSource = new CancellationTokenSource();
            _publishQuota = new SemaphoreSlim(receiveMaximum == 0 ? 1 : receiveMaximum, int.MaxValue);

            // 防止旧连接的等待者永久阻塞。
            try
            {
                oldCancellationTokenSource?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            oldCancellationTokenSource?.Dispose();
            oldQuota?.Dispose();
        }

        void CancelPublishQuotaWaiters()
        {
            var cancellationTokenSource = _publishQuotaCancellationTokenSource;
            if (cancellationTokenSource == null)
            {
                return;
            }

            try
            {
                cancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        void Cleanup()
        {
            CancelPublishQuotaWaiters();

            try
            {
                _backgroundCancellationTokenSource?.Cancel(false);
            }
            finally
            {
                _backgroundCancellationTokenSource?.Dispose();
                _backgroundCancellationTokenSource = null;

                _publishPacketReceiverQueue?.Dispose();
                _publishPacketReceiverQueue = null;

                _adapter?.Dispose();
            }
        }

        /// <summary>
        /// 释放通道适配器、后台任务与内部队列
        /// </summary>
        /// <param name="disposing">true 表示由 Dispose() 主动调用，false 表示由终结器调用</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Cleanup();
            }

            base.Dispose(disposing);
        }

        async Task<MqttClientAuthenticateResult> AuthenticateAsync(IMqttChannelAdapter channelAdapter, MqttApplicationMessage willApplicationMessage, CancellationToken cancellationToken)
        {
            var connectPacket = channelAdapter.PacketFormatterAdapter.DataConverter.CreateConnectPacket(
                willApplicationMessage,
                Options);

            var connAckPacket = await SendAndReceiveAsync<MqttConnAckPacket>(connectPacket, cancellationToken).ConfigureAwait(false);
            var result = channelAdapter.PacketFormatterAdapter.DataConverter.CreateClientConnectResult(connAckPacket);

            if (result.ResultCode != MqttClientConnectResultCode.Success)
            {
                throw new MqttConnectingFailedException(result);
            }

            _logger.Verbose("Authenticated MQTT connection with server established.");

            return result;
        }

        void ThrowIfNotConnected()
        {
            if (!IsConnected || Interlocked.Read(ref _isDisconnectPending) == 1)
            {
                throw new MqttCommunicationException("The client is not connected.");
            }
        }

        void ThrowIfConnected(string message)
        {
            if (IsConnected) throw new MqttProtocolViolationException(message);
        }

        async Task DisconnectInternalAsync(Task sender, Exception exception, MqttClientAuthenticateResult authenticateResult)
        {
            var clientWasConnected = _isConnected;
            
            TryInitiateDisconnect();
            _isConnected = false;
            CancelPublishQuotaWaiters();

            try
            {
                if (_adapter != null)
                {
                    _logger.Verbose("Disconnecting [Timeout={0}]", Options.CommunicationTimeout);
                    await _adapter.DisconnectAsync(Options.CommunicationTimeout, CancellationToken.None).ConfigureAwait(false);
                }

                _logger.Verbose("Disconnected from adapter.");
            }
            catch (Exception adapterException)
            {
                _logger.Warning(adapterException, "Error while disconnecting from adapter.");
            }

            try
            {
                var receiverTask = WaitForTaskAsync(_packetReceiverTask, sender);
                var publishPacketReceiverTask = WaitForTaskAsync(_publishPacketReceiverTask, sender);
                var keepAliveTask = WaitForTaskAsync(_keepAlivePacketsSenderTask, sender);

                await Task.WhenAll(receiverTask, publishPacketReceiverTask, keepAliveTask).ConfigureAwait(false);

                _publishPacketReceiverQueue?.Dispose();
            }
            catch (Exception e)
            {
                _logger.Warning(e, "Error while waiting for internal tasks.");
            }
            finally
            {
                Cleanup();
                _cleanDisconnectInitiated = false;

                _logger.Info("Disconnected.");

                var disconnectedHandler = DisconnectedHandler;
                if (disconnectedHandler != null)
                {
                    // This handler must be executed in a new thread because otherwise a dead lock may happen
                    // when trying to reconnect in that handler etc.
                    Task.Run(() => disconnectedHandler.HandleDisconnectedAsync(new MqttClientDisconnectedEventArgs(clientWasConnected, exception, authenticateResult, _disconnectReason))).Forget(_logger);
                }
            }
        }

        void TryInitiateDisconnect()
        {
            lock (_disconnectLock)
            {
                try
                {
                    if (_backgroundCancellationTokenSource?.IsCancellationRequested == true)
                    {
                        return;
                    }

                    _backgroundCancellationTokenSource?.Cancel(false);
                }
                catch (Exception exception)
                {
                    _logger.Warning(exception, "Error while initiating disconnect.");
                }
            }
        }

        Task SendAsync(MqttBasePacket packet, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _sendTracker.Restart();

            return _adapter.SendPacketAsync(packet, Options.CommunicationTimeout, cancellationToken);
        }

        async Task<TResponsePacket> SendAndReceiveAsync<TResponsePacket>(MqttBasePacket requestPacket, CancellationToken cancellationToken) where TResponsePacket : MqttBasePacket
        {
            cancellationToken.ThrowIfCancellationRequested();

            ushort identifier = 0;
            if (requestPacket is IMqttPacketWithIdentifier packetWithIdentifier && packetWithIdentifier.PacketIdentifier > 0)
            {
                identifier = packetWithIdentifier.PacketIdentifier;
            }

            using (var packetAwaiter = _packetDispatcher.AddAwaiter<TResponsePacket>(identifier))
            {
                try
                {
                    _sendTracker.Restart();

                    await _adapter.SendPacketAsync(requestPacket, Options.CommunicationTimeout, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    if (e is MqttMaximumPacketSizeExceededException)
                    {
                        // 保留原始异常，避免被等待超时覆盖。
                        packetAwaiter.Cancel();
                        throw;
                    }

                    _logger.Warning(e, "Error when sending packet of type '{0}'.", typeof(TResponsePacket).Name);
                    packetAwaiter.Cancel();
                }

                try
                {
                    return await packetAwaiter.WaitOneAsync(Options.CommunicationTimeout).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    if (exception is MqttCommunicationTimedOutException)
                    {
                        _logger.Warning(null, "Timeout while waiting for packet of type '{0}'.", typeof(TResponsePacket).Name);
                    }

                    throw;
                }
            }
        }

        async Task TrySendKeepAliveMessagesAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.Verbose("Start sending keep alive packets.");

                var keepAlivePeriod = Options.KeepAlivePeriod;

                while (!cancellationToken.IsCancellationRequested)
                {
                    // Values described here: [MQTT-3.1.2-24].
                    var waitTime = keepAlivePeriod - _sendTracker.Elapsed;

                    if (waitTime <= TimeSpan.Zero)
                    {
                        await SendAndReceiveAsync<MqttPingRespPacket>(new MqttPingReqPacket(), cancellationToken).ConfigureAwait(false);
                    }

                    // Wait a fixed time in all cases. Calculation of the remaining time is complicated
                    // due to some edge cases and was buggy in the past. Now we wait half a second because the
                    // min keep alive value is one second so that the server will wait 1.5 seconds for a PING
                    // packet.
                    await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                if (_cleanDisconnectInitiated)
                {
                    return;
                }

                if (exception is OperationCanceledException)
                {
                    return;
                }
                else if (exception is MqttCommunicationException)
                {
                    _logger.Warning(exception, "Communication error while sending/receiving keep alive packets.");
                }
                else
                {
                    _logger.Error(exception, "Error exception while sending/receiving keep alive packets.");
                }

                if (!DisconnectIsPending())
                {
                    await DisconnectInternalAsync(_keepAlivePacketsSenderTask, exception, null).ConfigureAwait(false);
                }
            }
            finally
            {
                _logger.Verbose("Stopped sending keep alive packets.");
            }
        }

        async Task TryReceivePacketsAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.Verbose("Start receiving packets.");

                while (!cancellationToken.IsCancellationRequested)
                {
                    var packet = await _adapter.ReceivePacketAsync(cancellationToken).ConfigureAwait(false);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (packet == null)
                    {
                        if (!DisconnectIsPending())
                        {
                            await DisconnectInternalAsync(_packetReceiverTask, null, null).ConfigureAwait(false);
                        }

                        return;
                    }

                    await TryProcessReceivedPacketAsync(packet, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (MqttMaximumPacketSizeExceededException exception)
            {
                if (_cleanDisconnectInitiated)
                {
                    return;
                }

                _logger.Error(exception, "Received a packet exceeding the maximum packet size.");

                if (_adapter != null && Options.ProtocolVersion == MqttProtocolVersion.V500)
                {
                    await TrySendDisconnectAsync(MqttDisconnectReasonCode.PacketTooLarge).ConfigureAwait(false);
                }

                _packetDispatcher.Dispatch(exception);

                if (!DisconnectIsPending())
                {
                    await DisconnectInternalAsync(_packetReceiverTask, exception, null).ConfigureAwait(false);
                }
            }
            catch (MqttMalformedPacketException exception)
            {
                if (_cleanDisconnectInitiated)
                {
                    return;
                }

                _logger.Error(exception, "Received a malformed packet.");

                if (Options.ProtocolVersion == MqttProtocolVersion.V500)
                {
                    await TrySendDisconnectAsync(MqttDisconnectReasonCode.MalformedPacket).ConfigureAwait(false);
                }

                _packetDispatcher.Dispatch(exception);

                if (!DisconnectIsPending())
                {
                    await DisconnectInternalAsync(_packetReceiverTask, exception, null).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                if (_cleanDisconnectInitiated)
                {
                    return;
                }

                if (exception is OperationCanceledException)
                {
                }
                else if (exception is MqttCommunicationException)
                {
                    _logger.Warning(exception, "Communication error while receiving packets.");
                }
                else
                {
                    _logger.Error(exception, "Error while receiving packets.");
                }

                if (Options.ProtocolVersion == MqttProtocolVersion.V500 &&
                    exception is MqttProtocolViolationException)
                {
                    await TrySendDisconnectAsync(MqttDisconnectReasonCode.ProtocolError).ConfigureAwait(false);
                }

                _packetDispatcher.Dispatch(exception);

                if (!DisconnectIsPending())
                {
                    await DisconnectInternalAsync(_packetReceiverTask, exception, null).ConfigureAwait(false);
                }
            }
            finally
            {
                _logger.Verbose("Stopped receiving packets.");
            }
        }

        async Task TryProcessReceivedPacketAsync(MqttBasePacket packet, CancellationToken cancellationToken)
        {
            try
            {
                _receiveTracker.Restart();

                if (packet is MqttPublishPacket publishPacket)
                {
                    if (Options.ProtocolVersion == MqttProtocolVersion.V500)
                    {
                        bool isAliasFailure;
                        string error;
                        if (!TryApplyInboundTopicAlias(publishPacket, out isAliasFailure, out error))
                        {
                            if (Options.ProtocolVersion == MqttProtocolVersion.V500)
                            {
                                await TrySendDisconnectAsync(
                                    isAliasFailure
                                        ? MqttDisconnectReasonCode.TopicAliasInvalid
                                        : MqttDisconnectReasonCode.TopicNameInvalid).ConfigureAwait(false);
                            }

                            throw new MqttProtocolViolationException(error);
                        }
                    }

                    if (publishPacket.QualityOfServiceLevel > MqttQualityOfServiceLevel.AtMostOnce &&
                        !_inboundFlowControl.TryAcquire(publishPacket.PacketIdentifier, publishPacket.QualityOfServiceLevel))
                    {
                        if (Options.ProtocolVersion == MqttProtocolVersion.V500)
                        {
                            await TrySendDisconnectAsync(MqttDisconnectReasonCode.ReceiveMaximumExceeded).ConfigureAwait(false);
                        }

                        throw new MqttProtocolViolationException("The server exceeded the receive maximum.");
                    }

                    EnqueueReceivedPublishPacket(publishPacket);
                }
                else if (packet is MqttPubRelPacket pubRelPacket)
                {
                    await SendAsync(new MqttPubCompPacket
                    {
                        PacketIdentifier = pubRelPacket.PacketIdentifier,
                        ReasonCode = MqttPubCompReasonCode.Success
                    }, cancellationToken).ConfigureAwait(false);

                    // QoS2 配额在发出 PUBCOMP 之后才释放。
                    _inboundFlowControl.Release(pubRelPacket.PacketIdentifier);
                }
                else if (packet is MqttPingReqPacket)
                {
                    await SendAsync(new MqttPingRespPacket(), cancellationToken).ConfigureAwait(false);
                }
                else if (packet is MqttDisconnectPacket disconnectPacket)
                {
                    _disconnectReason = (MqttClientDisconnectReason)(disconnectPacket.ReasonCode ?? MqttDisconnectReasonCode.NormalDisconnection);

                    // Also dispatch disconnect to waiting threads to generate a proper exception.
                    _packetDispatcher.Dispatch(packet);

                    if (!DisconnectIsPending())
                    {
                        await DisconnectInternalAsync(_packetReceiverTask, null, null);
                    }
                }
                else if (packet is MqttAuthPacket authPacket)
                {
                    var extendedAuthenticationExchangeHandler = Options.ExtendedAuthenticationExchangeHandler;
                    if (extendedAuthenticationExchangeHandler != null)
                    {
                        await extendedAuthenticationExchangeHandler.HandleRequestAsync(new MqttExtendedAuthenticationExchangeContext(authPacket, this)).ConfigureAwait(false);
                    }
                }
                else
                {
                    _packetDispatcher.Dispatch(packet);
                }
            }
            catch (Exception exception)
            {
                if (_cleanDisconnectInitiated)
                {
                    return;
                }

                if (exception is OperationCanceledException)
                {
                }
                else if (exception is MqttCommunicationException)
                {
                    _logger.Warning(exception, "Communication error while receiving packets.");
                }
                else
                {
                    _logger.Error(exception, "Error while receiving packets.");
                }

                _packetDispatcher.Dispatch(exception);

                if (!DisconnectIsPending())
                {
                    await DisconnectInternalAsync(_packetReceiverTask, exception, null).ConfigureAwait(false);
                }
            }
        }

        bool TryApplyInboundTopicAlias(MqttPublishPacket publishPacket, out bool isAliasFailure, out string error)
        {
            isAliasFailure = false;
            error = null;

            var topicAlias = publishPacket.Properties?.TopicAlias;
            if (!topicAlias.HasValue)
            {
                if (string.IsNullOrEmpty(publishPacket.Topic))
                {
                    error = "Received a PUBLISH packet with an empty topic and no Topic Alias.";
                    return false;
                }

                return true;
            }

            isAliasFailure = true;

            if (_topicAliasResolver == null)
            {
                error = "Received a PUBLISH packet with a Topic Alias before the connection was initialized.";
                return false;
            }

            string resolvedTopic;
            MqttTopicAliasResolver.AliasError aliasError;
            if (!_topicAliasResolver.TryResolve(topicAlias.Value, publishPacket.Topic, out resolvedTopic, out aliasError))
            {
                error = "Received an invalid Topic Alias '" + topicAlias.Value + "' (" + aliasError + ").";
                return false;
            }

            publishPacket.Topic = resolvedTopic;
            publishPacket.Properties.TopicAlias = null;
            isAliasFailure = false;
            return true;
        }

        async Task TrySendDisconnectAsync(MqttDisconnectReasonCode reasonCode)
        {
            try
            {
                if (_adapter == null)
                {
                    return;
                }

                await _adapter.SendPacketAsync(
                    new MqttDisconnectPacket
                    {
                        ReasonCode = reasonCode
                    },
                    Options.CommunicationTimeout,
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.Warning(exception, "Error while sending DISCONNECT packet.");
            }
        }

        void ApplyOutboundTopicAlias(MqttPublishPacket publishPacket, MqttApplicationMessage applicationMessage)
        {
            if (Options.ProtocolVersion != MqttProtocolVersion.V500)
            {
                return;
            }

            // 用户显式设置的别名优先，框架不再改写。
            if (applicationMessage.TopicAlias.HasValue || _topicAliasAssigner == null || publishPacket.Properties == null)
            {
                return;
            }

            ushort alias;
            bool includeTopic;
            if (!_topicAliasAssigner.TryAssign(publishPacket.Topic, out alias, out includeTopic))
            {
                return;
            }

            publishPacket.Properties.TopicAlias = alias;

            if (!includeTopic)
            {
                publishPacket.Topic = null;
            }
        }

        void EnqueueReceivedPublishPacket(MqttPublishPacket publishPacket)
        {
            try
            {
                _publishPacketReceiverQueue.Enqueue(publishPacket);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Error while queueing application message.");
            }
        }

        async Task ProcessReceivedPublishPackets(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var publishPacketDequeueResult = await _publishPacketReceiverQueue.TryDequeueAsync(cancellationToken);
                    if (!publishPacketDequeueResult.IsSuccess)
                    {
                        return;
                    }

                    var publishPacket = publishPacketDequeueResult.Item;

                    if (publishPacket.QualityOfServiceLevel == MqttQualityOfServiceLevel.AtMostOnce)
                    {
                        await HandleReceivedApplicationMessageAsync(publishPacket).ConfigureAwait(false);
                    }
                    else if (publishPacket.QualityOfServiceLevel == MqttQualityOfServiceLevel.AtLeastOnce)
                    {
                        if (await HandleReceivedApplicationMessageAsync(publishPacket).ConfigureAwait(false))
                        {
                            await SendAsync(new MqttPubAckPacket
                            {
                                PacketIdentifier = publishPacket.PacketIdentifier,
                                ReasonCode = MqttPubAckReasonCode.Success
                            }, cancellationToken).ConfigureAwait(false);

                            // QoS1 配额在发出 PUBACK 之后释放。
                            _inboundFlowControl.Release(publishPacket.PacketIdentifier);
                        }
                    }
                    else if (publishPacket.QualityOfServiceLevel == MqttQualityOfServiceLevel.ExactlyOnce)
                    {
                        if (await HandleReceivedApplicationMessageAsync(publishPacket).ConfigureAwait(false))
                        {
                            var pubRecPacket = new MqttPubRecPacket
                            {
                                PacketIdentifier = publishPacket.PacketIdentifier,
                                ReasonCode = MqttPubRecReasonCode.Success
                            };

                            await SendAsync(pubRecPacket, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        throw new MqttProtocolViolationException("Received a not supported QoS level.");
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception exception)
                {
                    _logger.Error(exception, "Error while handling application message.");
                }
            }
        }

        async Task<MqttClientPublishResult> PublishAtMostOnce(MqttPublishPacket publishPacket, CancellationToken cancellationToken)
        {
            // No packet identifier is used for QoS 0 [3.3.2.2 Packet Identifier]
            await SendAsync(publishPacket, cancellationToken).ConfigureAwait(false);
            return _adapter.PacketFormatterAdapter.DataConverter.CreatePublishResult(null);
        }

        async Task<MqttClientPublishResult> PublishAtLeastOnceAsync(MqttPublishPacket publishPacket, CancellationToken cancellationToken)
        {
            publishPacket.PacketIdentifier = _packetIdentifierProvider.GetNextPacketIdentifier();
            var response = await SendAndReceiveAsync<MqttPubAckPacket>(publishPacket, cancellationToken).ConfigureAwait(false);
            return _adapter.PacketFormatterAdapter.DataConverter.CreatePublishResult(response);
        }

        async Task<MqttClientPublishResult> PublishExactlyOnceAsync(MqttPublishPacket publishPacket, CancellationToken cancellationToken)
        {
            publishPacket.PacketIdentifier = _packetIdentifierProvider.GetNextPacketIdentifier();

            var pubRecPacket = await SendAndReceiveAsync<MqttPubRecPacket>(publishPacket, cancellationToken).ConfigureAwait(false);

            var pubRelPacket = new MqttPubRelPacket
            {
                PacketIdentifier = publishPacket.PacketIdentifier,
                ReasonCode = MqttPubRelReasonCode.Success
            };

            var pubCompPacket = await SendAndReceiveAsync<MqttPubCompPacket>(pubRelPacket, cancellationToken).ConfigureAwait(false);

            return _adapter.PacketFormatterAdapter.DataConverter.CreatePublishResult(pubRecPacket, pubCompPacket);
        }

        async Task<bool> HandleReceivedApplicationMessageAsync(MqttPublishPacket publishPacket)
        {
            var applicationMessage = _adapter.PacketFormatterAdapter.DataConverter.CreateApplicationMessage(publishPacket);

            var handler = ApplicationMessageReceivedHandler;
            if (handler != null)
            {
                var eventArgs = new MqttApplicationMessageReceivedEventArgs(Options.ClientId, applicationMessage);
                await handler.HandleApplicationMessageReceivedAsync(eventArgs).ConfigureAwait(false);
                return !eventArgs.ProcessingFailed;
            }

            return true;
        }

        async Task WaitForTaskAsync(Task task, Task sender)
        {
            if (task == null)
            {
                return;
            }

            if (task == sender)
            {
                // Return here to avoid deadlocks, but first any eventual exception in the task
                // must be handled to avoid not getting an unhandled task exception
                if (!task.IsFaulted)
                {
                    return;
                }

                // By accessing the Exception property the exception is considered handled and will
                // not result in an unhandled task exception later by the finalizer
                _logger.Warning(task.Exception, "Error while waiting for background task.");
                return;
            }

            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        bool DisconnectIsPending()
        {
            return Interlocked.CompareExchange(ref _isDisconnectPending, 1, 0) != 0;
        }
    }
}
