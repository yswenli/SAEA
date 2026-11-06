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
*命名空间：SAEA.MQTT.Server
*文件名： MqttClientConnection
*版本号： v26.4.23.1
*唯一标识：ea63d1be-ecbc-462f-96cf-2c7454ec68e0
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientConnection接口
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
using SAEA.MQTT.Client;
using SAEA.MQTT.Diagnostics;
using SAEA.MQTT.Exceptions;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Implementations;
using SAEA.MQTT.Internal;
using SAEA.MQTT.PacketDispatcher;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Server.Status;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 单个客户端连接的运行载体，负责收包分发、待发消息发送、主题别名与流量控制
    /// </summary>
    public sealed class MqttClientConnection : IDisposable
    {
        readonly MqttTopicAliasResolver _topicAliasResolver;
        readonly MqttTopicAliasAssigner _topicAliasAssigner;
        readonly MqttInboundFlowControl _inboundFlowControl;
        readonly MqttPacketIdentifierProvider _packetIdentifierProvider = new MqttPacketIdentifierProvider();
        readonly MqttPacketDispatcher _packetDispatcher = new MqttPacketDispatcher();
        readonly CancellationTokenSource _cancellationToken = new CancellationTokenSource();

        readonly IMqttRetainedMessagesManager _retainedMessagesManager;
        readonly MqttClientSessionsManager _sessionsManager;

        readonly IMqttNetScopedLogger _logger;
        readonly IMqttServerOptions _serverOptions;

        readonly IMqttChannelAdapter _channelAdapter;
        readonly MqttConnectionValidatorContext _connectionValidatorContext;
        readonly IMqttDataConverter _dataConverter;
        readonly string _endpoint;
        readonly DateTime _connectedTimestamp;
        readonly long _connectionEpoch;
        readonly MqttApplicationMessage _willMessage;
        readonly uint _willDelayInterval;

        int _willHandled;
        int _outboundPublishInFlight;
        bool _stopRequested;
        bool _disconnectPacketReceived;
        bool _protocolError;
        MqttDisconnectReasonCode _clientDisconnectReason;

        volatile Task _packageReceiverTask;
        DateTime _lastNonKeepAlivePacketReceivedTimestamp;

        // CONNECT 在连接对象创建前已被读取，这里补计 1 次接收。
        long _receivedPacketsCount = 1;
        long _sentPacketsCount;
        long _receivedApplicationMessagesCount;
        long _sentApplicationMessagesCount;
        MqttDisconnectReasonCode _disconnectReason;

        /// <summary>
        /// 创建客户端连接，从通道适配器协商出的能力初始化主题别名与入站流量控制
        /// </summary>
        /// <param name="connectPacket">客户端提交的 CONNECT 报文，可提供遗嘱消息与遗嘱延迟</param>
        /// <param name="channelAdapter">该连接的通道适配器，负责收发报文</param>
        /// <param name="session">连接绑定的客户端会话</param>
        /// <param name="connectionValidatorContext">连接校验上下文，用于生成 CONNACK</param>
        /// <param name="serverOptions">服务端配置</param>
        /// <param name="sessionsManager">会话管理器，用于断线清理与消息分发</param>
        /// <param name="retainedMessagesManager">保留消息管理器</param>
        /// <param name="logger">根日志器，内部创建按类型隔离的作用域日志器</param>
        public MqttClientConnection(MqttConnectPacket connectPacket,
            IMqttChannelAdapter channelAdapter,
            MqttClientSession session,
            MqttConnectionValidatorContext connectionValidatorContext,
            IMqttServerOptions serverOptions,
            MqttClientSessionsManager sessionsManager,
            IMqttRetainedMessagesManager retainedMessagesManager,
            IMqttNetLogger logger)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            _serverOptions = serverOptions ?? throw new ArgumentNullException(nameof(serverOptions));
            _sessionsManager = sessionsManager ?? throw new ArgumentNullException(nameof(sessionsManager));
            _retainedMessagesManager = retainedMessagesManager ?? throw new ArgumentNullException(nameof(retainedMessagesManager));

            _channelAdapter = channelAdapter ?? throw new ArgumentNullException(nameof(channelAdapter));
            _connectionValidatorContext = connectionValidatorContext ?? throw new ArgumentNullException(nameof(connectionValidatorContext));
            _dataConverter = _channelAdapter.PacketFormatterAdapter.DataConverter;

            var capabilities = _channelAdapter.PacketFormatterAdapter.Capabilities;
            _topicAliasResolver = new MqttTopicAliasResolver(capabilities?.TopicAliasMaximum ?? 0);
            _topicAliasAssigner = new MqttTopicAliasAssigner(capabilities?.ClientTopicAliasMaximum ?? 0);
            _inboundFlowControl = new MqttInboundFlowControl(capabilities?.ReceiveMaximum ?? ushort.MaxValue);
            _endpoint = _channelAdapter.Endpoint;
            ConnectPacket = connectPacket ?? throw new ArgumentNullException(nameof(connectPacket));

            if (logger == null) throw new ArgumentNullException(nameof(logger));
            _logger = logger.CreateScopedLogger(nameof(MqttClientConnection));

            _connectedTimestamp = DateTime.UtcNow;
            _connectionEpoch = Session.ConnectionEpoch;
            LastPacketReceivedTimestamp = _connectedTimestamp;
            _lastNonKeepAlivePacketReceivedTimestamp = LastPacketReceivedTimestamp;

            _willMessage = connectPacket.WillMessage;
            _willDelayInterval = connectPacket.Properties?.WillDelayInterval ?? 0;
        }

        /// <summary>
        /// 连接的当前运行状态，由收包循环推进
        /// </summary>
        public MqttClientConnectionStatus Status { get; private set; } = MqttClientConnectionStatus.Initializing;

        /// <summary>
        /// 客户端提交的 CONNECT 报文，连接期间只读
        /// </summary>
        public MqttConnectPacket ConnectPacket { get; }

        /// <summary>
        /// 客户端标识，取自 CONNECT 报文
        /// </summary>
        public string ClientId => ConnectPacket.ClientId;

        /// <summary>
        /// 通道适配器当前是否正在读取报文
        /// </summary>
        public bool IsReadingPacket => _channelAdapter.IsReadingPacket;

        internal bool IsV5 => _channelAdapter.PacketFormatterAdapter.Capabilities?.IsV5 ??
                              (_channelAdapter.PacketFormatterAdapter.ProtocolVersion == MqttProtocolVersion.V500);

        /// <summary>
        /// 最近一次收到报文的时间（UTC），初始为连接建立时刻
        /// </summary>
        public DateTime LastPacketReceivedTimestamp { get; private set; }

        /// <summary>
        /// 连接绑定的客户端会话
        /// </summary>
        public MqttClientSession Session { get; }

        /// <summary>
        /// 停止连接并等待收包循环退出
        /// </summary>
        /// <param name="reason">断开原因，接管与保活超时需先向客户端发送 DISCONNECT</param>
        /// <returns>连接停止并等待收包循环退出后完成的任务</returns>
        public async Task StopAsync(MqttDisconnectReasonCode reason)
        {
            Status = MqttClientConnectionStatus.Finalizing;
            _disconnectReason = reason;
            _stopRequested = true;

            if (reason == MqttDisconnectReasonCode.SessionTakenOver || reason == MqttDisconnectReasonCode.KeepAliveTimeout)
            {
                // Is is very important to send the DISCONNECT packet here BEFORE cancelling the
                // token because the entire connection is closed (disposed) as soon as the cancellation
                // token is cancelled. To there is no chance that the DISCONNECT packet will ever arrive
                // at the client!
                try
                {
                    await _channelAdapter.SendPacketAsync(new MqttDisconnectPacket
                    {
                        ReasonCode = reason
                    }, _serverOptions.DefaultCommunicationTimeout, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    _logger.Error(exception, "Client '{0}': Error while sending DISCONNECT packet after takeover.", ClientId);
                }
            }

            StopInternal();

            await (_packageReceiverTask ?? PlatformAbstractionLayer.CompletedTask);
        }

        /// <summary>
        /// 清零通道适配器的收发字节与报文统计，应用层消息计数不受影响
        /// </summary>
        public void ResetStatistics()
        {
            _channelAdapter.ResetStatistics();
        }

        /// <summary>
        /// 把当前连接的标识、协议版本与统计快照写入状态对象，供状态查询使用
        /// </summary>
        /// <param name="status">待填充的客户端状态对象，其字段会被整体覆盖</param>
        public void FillStatus(MqttClientStatus status)
        {
            status.ClientId = ClientId;
            status.Endpoint = _endpoint;
            status.ProtocolVersion = _channelAdapter.PacketFormatterAdapter.ProtocolVersion;

            status.ReceivedApplicationMessagesCount = Interlocked.Read(ref _receivedApplicationMessagesCount);
            status.SentApplicationMessagesCount = Interlocked.Read(ref _sentApplicationMessagesCount);

            status.ReceivedPacketsCount = Interlocked.Read(ref _receivedPacketsCount);
            status.SentPacketsCount = Interlocked.Read(ref _sentPacketsCount);

            status.ConnectedTimestamp = _connectedTimestamp;
            status.LastPacketReceivedTimestamp = LastPacketReceivedTimestamp;
            status.LastNonKeepAlivePacketReceivedTimestamp = _lastNonKeepAlivePacketReceivedTimestamp;

            status.BytesSent = _channelAdapter.BytesSent;
            status.BytesReceived = _channelAdapter.BytesReceived;
        }

        /// <summary>
        /// 释放主题别名表与取消令牌，不负责关闭底层通道
        /// </summary>
        public void Dispose()
        {
            _topicAliasResolver.Clear();
            _topicAliasAssigner.Clear();
            _cancellationToken.Dispose();
        }

        /// <summary>
        /// 启动收包循环并返回其任务，需由调用方等待该任务观察连接结束
        /// </summary>
        /// <returns>收包循环任务，连接关闭后完成</returns>
        public Task RunAsync()
        {
            _packageReceiverTask = RunInternalAsync(_cancellationToken.Token);
            return _packageReceiverTask;
        }

        async Task RunInternalAsync(CancellationToken cancellationToken)
        {
            var disconnectType = MqttClientDisconnectType.NotClean;
            try
            {
                _logger.Info("Client '{0}': Session started.", ClientId);

                await SendAsync(_channelAdapter.PacketFormatterAdapter.DataConverter.CreateConnAckPacket(_connectionValidatorContext), cancellationToken).ConfigureAwait(false);

                Task.Run(() => SendPendingPacketsAsync(cancellationToken), cancellationToken).Forget(_logger);

                Session.IsCleanSession = false;

                while (!cancellationToken.IsCancellationRequested)
                {
                    Status = MqttClientConnectionStatus.Running;

                    var packet = await _channelAdapter.ReceivePacketAsync(cancellationToken).ConfigureAwait(false);
                    if (packet == null)
                    {
                        // The client has closed the connection gracefully.
                        return;
                    }

                    Interlocked.Increment(ref _receivedPacketsCount);
                    LastPacketReceivedTimestamp = DateTime.UtcNow;

                    if (!(packet is MqttPingReqPacket || packet is MqttPingRespPacket))
                    {
                        _lastNonKeepAlivePacketReceivedTimestamp = LastPacketReceivedTimestamp;
                    }

                    if (packet is MqttPublishPacket publishPacket)
                    {
                        await HandleIncomingPublishPacketAsync(publishPacket, cancellationToken).ConfigureAwait(false);
                    }
                    else if (packet is MqttPubRelPacket pubRelPacket)
                    {
                        await HandleIncomingPubRelPacketAsync(pubRelPacket, cancellationToken).ConfigureAwait(false);
                    }
                    else if (packet is MqttSubscribePacket subscribePacket)
                    {
                        await HandleIncomingSubscribePacketAsync(subscribePacket, cancellationToken).ConfigureAwait(false);
                    }
                    else if (packet is MqttUnsubscribePacket unsubscribePacket)
                    {
                        await HandleIncomingUnsubscribePacketAsync(unsubscribePacket, cancellationToken).ConfigureAwait(false);
                    }
                    else if (packet is MqttPingReqPacket)
                    {
                        await SendAsync(MqttPingRespPacket.Instance, cancellationToken).ConfigureAwait(false);
                    }
                    else if (packet is MqttDisconnectPacket disconnectPacket)
                    {
                        _disconnectPacketReceived = true;
                        _clientDisconnectReason = disconnectPacket.ReasonCode ?? MqttDisconnectReasonCode.NormalDisconnection;

                        var requestedExpiry = disconnectPacket.Properties?.SessionExpiryInterval;
                        if (requestedExpiry.HasValue)
                        {
                            if (Session.SessionExpiryInterval == 0 && requestedExpiry.Value != 0)
                            {
                                // [MQTT-3.1.2-23] 类规则：CONNECT 的 Session Expiry 为 0 时，DISCONNECT 不得改大。
                                _protocolError = true;
                                _clientDisconnectReason = MqttDisconnectReasonCode.ProtocolError;
                            }
                            else
                            {
                                var capabilities = _channelAdapter.PacketFormatterAdapter.Capabilities;
                                var max = capabilities?.MaximumSessionExpiryInterval ?? uint.MaxValue;
                                Session.SessionExpiryInterval = requestedExpiry.Value < max ? requestedExpiry.Value : max;
                            }
                        }

                        disconnectType = MqttClientDisconnectType.Clean;

                        StopInternal();
                        return;
                    }
                    else
                    {
                        _packetDispatcher.Dispatch(packet);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (MqttMaximumPacketSizeExceededException exception)
            {
                if (exception.Direction == MqttPacketSizeDirection.Inbound)
                {
                    _logger.Warning(exception, "Client '{0}': Received a packet exceeding the maximum packet size.", ClientId);
                    _protocolError = true;
                    _clientDisconnectReason = MqttDisconnectReasonCode.PacketTooLarge;

                    if (IsV5)
                    {
                        await SendDisconnectAsync(MqttDisconnectReasonCode.PacketTooLarge).ConfigureAwait(false);
                    }
                }
                else
                {
                    // 本端发包超限：不向对端发送原因码（对端并未违规），直接关闭。
                    _logger.Error(exception, "Client '{0}': A control packet exceeds the peer maximum packet size.", ClientId);
                }

                StopInternal();
            }
            catch (MqttMalformedPacketException exception)
            {
                _logger.Warning(exception, "Client '{0}': Received a malformed packet.", ClientId);
                _protocolError = true;
                _clientDisconnectReason = MqttDisconnectReasonCode.MalformedPacket;

                if (IsV5)
                {
                    await SendDisconnectAsync(MqttDisconnectReasonCode.MalformedPacket).ConfigureAwait(false);
                }

                StopInternal();
            }
            catch (Exception exception)
            {
                if (exception is MqttCommunicationException)
                {
                    _logger.Warning(exception, "Client '{0}': Communication exception while receiving client packets.", ClientId);
                }
                else
                {
                    _logger.Error(exception, "Client '{0}': Error while receiving client packets.", ClientId);
                }

                if (IsV5 && exception is MqttProtocolViolationException)
                {
                    _protocolError = true;
                    _clientDisconnectReason = MqttDisconnectReasonCode.ProtocolError;
                    await SendDisconnectAsync(MqttDisconnectReasonCode.ProtocolError).ConfigureAwait(false);
                }

                StopInternal();
            }
            finally
            {
                if (_disconnectReason == MqttDisconnectReasonCode.SessionTakenOver)
                {
                    disconnectType = MqttClientDisconnectType.Takeover;
                }

                _packetDispatcher.Cancel();

                _logger.Info("Client '{0}': Connection stopped.", ClientId);

                var willDisposition = DetermineWillDisposition();
                if (Interlocked.CompareExchange(ref _willHandled, 1, 0) != 0)
                {
                    willDisposition = MqttWillDisposition.DoNotPublish;
                }

                try
                {
                    _sessionsManager.HandleConnectionClosed(
                        ClientId,
                        _connectionEpoch,
                        disconnectType,
                        IsV5,
                        _willMessage,
                        _willDelayInterval,
                        willDisposition,
                        this);
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Client '{0}': Error while handling connection close", ClientId);
                }

                try
                {
                    await _sessionsManager.CleanUpClient(ClientId, _channelAdapter, disconnectType);
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Client '{0}': Error while cleaning up", ClientId);
                }
            }
        }

        void StopInternal()
        {
            _cancellationToken.Cancel();
        }

        /// <summary>
        /// 根据本次连接的断开原因决定 Will 的处置。只由连接自身调用一次。
        /// </summary>
        MqttWillDisposition DetermineWillDisposition()
        {
            if (_willMessage == null)
            {
                return MqttWillDisposition.DoNotPublish;
            }

            if (_disconnectPacketReceived)
            {
                return _clientDisconnectReason == MqttDisconnectReasonCode.NormalDisconnection
                    ? MqttWillDisposition.DoNotPublish
                    : MqttWillDisposition.PublishAfterDelay;
            }

            if (_protocolError)
            {
                return MqttWillDisposition.PublishAfterDelay;
            }

            if (_stopRequested)
            {
                // 服务端主动停止（shutdown/删除会话）按规范允许发布，处置与异常关闭一致。
                return MqttWillDisposition.PublishAfterDelay;
            }

            // 异常、I/O 错误、keep-alive 超时、对端关闭：发布（受 Will Delay 约束）。
            return MqttWillDisposition.PublishAfterDelay;
        }

        /// <summary>
        /// 会话被新连接接管时，由新连接在停止旧连接之前调用。
        /// 通过 CAS 保证与 <see cref="DetermineWillDisposition"/> 只有一方生效。
        /// </summary>
        internal void ResolveWillOnTakeover(bool newCleanStart)
        {
            if (Interlocked.CompareExchange(ref _willHandled, 1, 0) != 0)
            {
                return;
            }

            var willMessage = _willMessage;
            if (willMessage == null)
            {
                return;
            }

            if (newCleanStart || _willDelayInterval == 0)
            {
                _sessionsManager.DispatchApplicationMessage(willMessage, this);
            }
            // 否则（CleanStart=0 且 WillDelay>0）：取消旧 will，不发布。
        }

        Task EnqueueSubscribedRetainedMessagesAsync(ICollection<MqttTopicFilter> topicFilters)
        {
            return Session.EnqueueRetainedMessagesAsync(topicFilters);
        }

        async Task HandleIncomingPubRelPacketAsync(MqttPubRelPacket pubRelPacket, CancellationToken cancellationToken)
        {
            var pubCompPacket = new MqttPubCompPacket
            {
                PacketIdentifier = pubRelPacket.PacketIdentifier,
                ReasonCode = MqttPubCompReasonCode.Success
            };

            await SendAsync(pubCompPacket, cancellationToken).ConfigureAwait(false);

            // QoS2 配额只有在发出 PUBCOMP 之后才释放（不是收到 PUBREL 或发出 PUBREC 时）。
            _inboundFlowControl.Release(pubRelPacket.PacketIdentifier);
        }

        async Task HandleIncomingSubscribePacketAsync(MqttSubscribePacket subscribePacket, CancellationToken cancellationToken)
        {
            var subscribeResult = await Session.SubscriptionsManager.SubscribeAsync(subscribePacket, ConnectPacket).ConfigureAwait(false);

            // V5 协议错误：规范要求发送 DISCONNECT，而不是 SUBACK。
            if (subscribeResult.DisconnectReasonCode.HasValue)
            {
                var reason = subscribeResult.DisconnectReasonCode.Value;
                _protocolError = true;
                _clientDisconnectReason = reason;

                await SendDisconnectAsync(reason).ConfigureAwait(false);
                StopInternal();
                return;
            }

            var subAckPacket = _channelAdapter.PacketFormatterAdapter.DataConverter.CreateSubAckPacket(subscribePacket, subscribeResult);

            await SendAsync(subAckPacket, cancellationToken).ConfigureAwait(false);

            if (subscribeResult.CloseConnection)
            {
                StopInternal();
                return;
            }

            await EnqueueSubscribedRetainedMessagesAsync(subscribeResult.RetainedMessageFilters).ConfigureAwait(false);
        }

        async Task HandleIncomingUnsubscribePacketAsync(MqttUnsubscribePacket unsubscribePacket, CancellationToken cancellationToken)
        {
            var reasonCodes = await Session.SubscriptionsManager.UnsubscribeAsync(unsubscribePacket).ConfigureAwait(false);
            var unsubAckPacket = _channelAdapter.PacketFormatterAdapter.DataConverter.CreateUnsubAckPacket(unsubscribePacket, reasonCodes);

            await SendAsync(unsubAckPacket, cancellationToken).ConfigureAwait(false);
        }

        async Task HandleIncomingPublishPacketAsync(MqttPublishPacket publishPacket, CancellationToken cancellationToken)
        {
            MqttDisconnectReasonCode disconnectReason;
            if (!TryApplyTopicAlias(publishPacket, out disconnectReason))
            {
                _protocolError = true;
                _clientDisconnectReason = disconnectReason;

                await SendDisconnectAsync(disconnectReason).ConfigureAwait(false);
                StopInternal();
                return;
            }

            if (IsV5 && !TryValidatePublishCapabilities(publishPacket, out disconnectReason))
            {
                _protocolError = true;
                _clientDisconnectReason = disconnectReason;

                await SendDisconnectAsync(disconnectReason).ConfigureAwait(false);
                StopInternal();
                return;
            }

            if (publishPacket.QualityOfServiceLevel > MqttQualityOfServiceLevel.AtMostOnce &&
                !_inboundFlowControl.TryAcquire(publishPacket.PacketIdentifier, publishPacket.QualityOfServiceLevel))
            {
                _protocolError = true;
                _clientDisconnectReason = MqttDisconnectReasonCode.ReceiveMaximumExceeded;

                await SendDisconnectAsync(MqttDisconnectReasonCode.ReceiveMaximumExceeded).ConfigureAwait(false);
                StopInternal();
                return;
            }

            Interlocked.Increment(ref _receivedApplicationMessagesCount);

            var applicationMessage = _dataConverter.CreateApplicationMessage(publishPacket);
            _sessionsManager.DispatchApplicationMessage(applicationMessage, this);

            switch (publishPacket.QualityOfServiceLevel)
            {
                case MqttQualityOfServiceLevel.AtMostOnce:
                    {
                        return;
                    }
                case MqttQualityOfServiceLevel.AtLeastOnce:
                    {
                        var pubAckPacket = _dataConverter.CreatePubAckPacket(publishPacket);
                        await SendAsync(pubAckPacket, cancellationToken).ConfigureAwait(false);
                        _inboundFlowControl.Release(publishPacket.PacketIdentifier);
                        return;
                    }
                case MqttQualityOfServiceLevel.ExactlyOnce:
                    {
                        var pubRecPacket = _dataConverter.CreatePubRecPacket(publishPacket);
                        await SendAsync(pubRecPacket, cancellationToken).ConfigureAwait(false);
                        return;
                    }
                default:
                    {
                        throw new MqttProtocolViolationException("Received a not supported QoS level.");
                    }
            }
        }

        bool TryApplyTopicAlias(MqttPublishPacket publishPacket, out MqttDisconnectReasonCode disconnectReason)
        {
            disconnectReason = MqttDisconnectReasonCode.NormalDisconnection;

            var topicAlias = publishPacket.Properties?.TopicAlias;
            if (!topicAlias.HasValue)
            {
                // V3 没有别名概念，空 Topic 的既有处理路径保持不变。
                if (IsV5 && string.IsNullOrEmpty(publishPacket.Topic))
                {
                    _logger.Warning(
                        null,
                        "Client '{0}': Received a PUBLISH packet with an empty topic and no Topic Alias.",
                        ClientId);
                    disconnectReason = MqttDisconnectReasonCode.TopicNameInvalid;
                    return false;
                }

                return true;
            }

            string resolvedTopic;
            MqttTopicAliasResolver.AliasError error;
            if (!_topicAliasResolver.TryResolve(topicAlias.Value, publishPacket.Topic, out resolvedTopic, out error))
            {
                _logger.Warning(
                    null,
                    "Client '{0}': Received an invalid Topic Alias '{1}' ({2}).",
                    ClientId,
                    topicAlias.Value,
                    error);
                disconnectReason = MqttDisconnectReasonCode.TopicAliasInvalid;
                return false;
            }

            publishPacket.Topic = resolvedTopic;

            // 入站别名已完成解析，后续分发与持久化不再携带该属性。
            publishPacket.Properties.TopicAlias = null;
            return true;
        }

        bool TryValidatePublishCapabilities(MqttPublishPacket publishPacket, out MqttDisconnectReasonCode disconnectReason)
        {
            disconnectReason = MqttDisconnectReasonCode.NormalDisconnection;

            var capabilities = _channelAdapter.PacketFormatterAdapter.Capabilities;
            if (capabilities == null)
            {
                return true;
            }

            if (!capabilities.RetainAvailable && publishPacket.Retain)
            {
                // [MQTT-3.3.1-11]：Retain Available 为 0 时客户端不得发送 Retain=1。
                _logger.Warning(null, "Client '{0}': Received a retained PUBLISH while retain is not available.", ClientId);
                disconnectReason = MqttDisconnectReasonCode.RetainNotSupported;
                return false;
            }

            if ((int)publishPacket.QualityOfServiceLevel > (int)capabilities.MaximumQoS)
            {
                // [MQTT-3.3.1-9]：QoS 超过 Maximum QoS 属于协议错误。
                _logger.Warning(
                    null,
                    "Client '{0}': Received a PUBLISH with QoS '{1}' above the maximum QoS '{2}'.",
                    ClientId,
                    publishPacket.QualityOfServiceLevel,
                    capabilities.MaximumQoS);
                disconnectReason = MqttDisconnectReasonCode.QoSNotSupported;
                return false;
            }

            if (publishPacket.Properties?.SubscriptionIdentifiers != null &&
                publishPacket.Properties.SubscriptionIdentifiers.Count > 0)
            {
                // [MQTT-3.3.4-6]：客户端发往服务端的 PUBLISH 不得包含 Subscription Identifier。
                _logger.Warning(null, "Client '{0}': Received a PUBLISH with a Subscription Identifier.", ClientId);
                disconnectReason = MqttDisconnectReasonCode.ProtocolError;
                return false;
            }

            return true;
        }

        bool TryBeginOutboundPublish()
        {
            var capabilities = _channelAdapter.PacketFormatterAdapter.Capabilities;
            var maximum = capabilities == null ? ushort.MaxValue : capabilities.ClientReceiveMaximum;
            if (maximum == 0)
            {
                maximum = 1;
            }

            var inFlight = Interlocked.Increment(ref _outboundPublishInFlight);
            if (inFlight > maximum)
            {
                Interlocked.Decrement(ref _outboundPublishInFlight);
                return false;
            }

            return true;
        }

        void EndOutboundPublish()
        {
            Interlocked.Decrement(ref _outboundPublishInFlight);
        }

        async Task SendDisconnectAsync(MqttDisconnectReasonCode reasonCode)
        {
            if (!IsV5)
            {
                return;
            }

            try
            {
                await _channelAdapter.SendPacketAsync(
                    new MqttDisconnectPacket
                    {
                        ReasonCode = reasonCode
                    },
                    _serverOptions.DefaultCommunicationTimeout,
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.Warning(exception, "Client '{0}': Error while sending DISCONNECT packet.", ClientId);
            }
        }

        void ApplyOutboundTopicAlias(MqttPublishPacket publishPacket)
        {
            if (!IsV5 || publishPacket?.Properties == null)
            {
                return;
            }

            if (_topicAliasAssigner.Maximum == 0)
            {
                publishPacket.Properties.TopicAlias = null;
                return;
            }

            ushort alias;
            bool includeTopic;
            if (!_topicAliasAssigner.TryAssign(publishPacket.Topic, out alias, out includeTopic))
            {
                publishPacket.Properties.TopicAlias = null;
                return;
            }

            publishPacket.Properties.TopicAlias = alias;

            if (!includeTopic)
            {
                // 已建立映射：后续 PUBLISH 使用空 Topic + 别名。
                publishPacket.Topic = null;
            }
        }

        async Task SendPendingPacketsAsync(CancellationToken cancellationToken)
        {
            MqttQueuedApplicationMessage queuedApplicationMessage = null;
            MqttPublishPacket publishPacket = null;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    queuedApplicationMessage = await Session.ApplicationMessagesQueue.DequeueAsync(cancellationToken).ConfigureAwait(false);
                    if (queuedApplicationMessage == null)
                    {
                        return;
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (queuedApplicationMessage.ExpiresAt.HasValue &&
                        MqttTime.UtcNow >= queuedApplicationMessage.ExpiresAt.Value)
                    {
                        _logger.Verbose(
                            "Dropping expired queued message for client '{0}' (topic '{1}').",
                            ClientId,
                            queuedApplicationMessage.ApplicationMessage.Topic);
                        continue;
                    }

                    publishPacket = _dataConverter.CreatePublishPacket(queuedApplicationMessage.ApplicationMessage);
                    publishPacket.QualityOfServiceLevel = queuedApplicationMessage.SubscriptionQualityOfServiceLevel;

                    // Set the retain flag to true according to [MQTT-3.3.1-8] and [MQTT-3.3.1-9].
                    // RetainAsPublished=false 时转发消息的 Retain 恒为 false；retained 补发恒为 true。
                    publishPacket.Retain = queuedApplicationMessage.IsRetainedMessage
                        ? true
                        : (queuedApplicationMessage.RetainAsPublished && queuedApplicationMessage.ApplicationMessage.Retain);

                    if (_serverOptions.ClientMessageQueueInterceptor != null)
                    {
                        var context = new MqttClientMessageQueueInterceptorContext(
                            queuedApplicationMessage.SenderClientId,
                            ClientId,
                            queuedApplicationMessage.ApplicationMessage,
                            queuedApplicationMessage.SubscriptionQualityOfServiceLevel);

                        if (_serverOptions.ClientMessageQueueInterceptor != null)
                        {
                            await _serverOptions.ClientMessageQueueInterceptor.InterceptClientMessageQueueEnqueueAsync(context).ConfigureAwait(false);
                        }

                        if (!context.AcceptEnqueue || context.ApplicationMessage == null)
                        {
                            return;
                        }

                        publishPacket.Topic = context.ApplicationMessage.Topic;
                        publishPacket.Payload = context.ApplicationMessage.Payload;
                        publishPacket.QualityOfServiceLevel = context.SubscriptionQualityOfServiceLevel;
                    }

                    if (IsV5 && queuedApplicationMessage.ExpiresAt.HasValue && publishPacket.Properties != null)
                    {
                        // 会话中排队的时间也计入过期；转发时写入绝对到期时间对应的剩余整秒。
                        var remainingSeconds = MqttMessageExpiry.GetRemainingSeconds(queuedApplicationMessage.ExpiresAt, MqttTime.UtcNow);
                        if (!remainingSeconds.HasValue || remainingSeconds.Value == 0)
                        {
                            _logger.Verbose(
                                "Dropping expired queued message for client '{0}' (topic '{1}').",
                                ClientId,
                                queuedApplicationMessage.ApplicationMessage.Topic);
                            continue;
                        }

                        publishPacket.Properties.MessageExpiryInterval = remainingSeconds.Value;
                    }

                    if (publishPacket.Properties != null)
                    {
                        var capabilities = _channelAdapter.PacketFormatterAdapter.Capabilities;
                        var identifiersAvailable = capabilities == null || capabilities.SubscriptionIdentifiersAvailable;

                        if (IsV5 && identifiersAvailable &&
                            queuedApplicationMessage.SubscriptionIdentifiers != null &&
                            queuedApplicationMessage.SubscriptionIdentifiers.Count > 0)
                        {
                            publishPacket.Properties.SubscriptionIdentifiers = new List<uint>(queuedApplicationMessage.SubscriptionIdentifiers);
                        }
                        else
                        {
                            // 防止把入站应用消息上残留的标识符透传给订阅者。
                            publishPacket.Properties.SubscriptionIdentifiers = null;
                        }
                    }

                    ApplyOutboundTopicAlias(publishPacket);

                    if (publishPacket.QualityOfServiceLevel > 0)
                    {
                        publishPacket.PacketIdentifier = _packetIdentifierProvider.GetNextPacketIdentifier();
                    }

                    var outboundInFlight = false;
                    if (publishPacket.QualityOfServiceLevel > MqttQualityOfServiceLevel.AtMostOnce)
                    {
                        outboundInFlight = TryBeginOutboundPublish();
                        if (!outboundInFlight)
                        {
                            // D3：保持串行发送，此断言仅用于发现并发发送导致的配额越界。
                            _logger.Error(
                                null,
                                "Client '{0}': Outbound in-flight publishes exceeded the client receive maximum.",
                                ClientId);
                            _protocolError = true;
                            _clientDisconnectReason = MqttDisconnectReasonCode.ProtocolError;
                            StopInternal();
                            return;
                        }
                    }

                    try
                    {
                        if (publishPacket.QualityOfServiceLevel == MqttQualityOfServiceLevel.AtMostOnce)
                        {
                            await SendAsync(publishPacket, cancellationToken).ConfigureAwait(false);
                        }
                        else if (publishPacket.QualityOfServiceLevel == MqttQualityOfServiceLevel.AtLeastOnce)
                        {
                            var awaiter = _packetDispatcher.AddAwaiter<MqttPubAckPacket>(publishPacket.PacketIdentifier);
                            await SendAsync(publishPacket, cancellationToken).ConfigureAwait(false);
                            await awaiter.WaitOneAsync(_serverOptions.DefaultCommunicationTimeout).ConfigureAwait(false);
                        }
                        else if (publishPacket.QualityOfServiceLevel == MqttQualityOfServiceLevel.ExactlyOnce)
                        {
                            using (var awaiter1 = _packetDispatcher.AddAwaiter<MqttPubRecPacket>(publishPacket.PacketIdentifier))
                            using (var awaiter2 = _packetDispatcher.AddAwaiter<MqttPubCompPacket>(publishPacket.PacketIdentifier))
                            {
                                await SendAsync(publishPacket, cancellationToken).ConfigureAwait(false);
                                await awaiter1.WaitOneAsync(_serverOptions.DefaultCommunicationTimeout).ConfigureAwait(false);

                                await SendAsync(new MqttPubRelPacket
                                {
                                    PacketIdentifier = publishPacket.PacketIdentifier,
                                    ReasonCode = MqttPubRelReasonCode.Success
                                }, cancellationToken).ConfigureAwait(false);

                                await awaiter2.WaitOneAsync(_serverOptions.DefaultCommunicationTimeout).ConfigureAwait(false);
                            }
                        }
                    }
                    finally
                    {
                        if (outboundInFlight)
                        {
                            EndOutboundPublish();
                        }
                    }

                    _logger.Verbose("Queued application message sent (ClientId: {0}).", ClientId);
                }
            }
            catch (Exception exception)
            {
                if (exception is OperationCanceledException)
                {
                }
                else if (exception is MqttCommunicationTimedOutException)
                {
                    _logger.Warning(exception, "Sending publish packet failed: Timeout (ClientId: {0}).", ClientId);
                }
                else if (exception is MqttCommunicationException)
                {
                    _logger.Warning(exception, "Sending publish packet failed: Communication exception (ClientId: {0}).", ClientId);
                }
                else
                {
                    _logger.Error(exception, "Sending publish packet failed (ClientId: {0}).", ClientId);
                }

                if (publishPacket?.QualityOfServiceLevel > MqttQualityOfServiceLevel.AtMostOnce)
                {
                    queuedApplicationMessage.IsDuplicate = true;

                    Session.ApplicationMessagesQueue.Enqueue(queuedApplicationMessage);
                }

                StopInternal();
            }
        }

        async Task SendAsync(MqttBasePacket packet, CancellationToken cancellationToken)
        {
            try
            {
                await _channelAdapter.SendPacketAsync(packet, _serverOptions.DefaultCommunicationTimeout, cancellationToken).ConfigureAwait(false);
            }
            catch (MqttMaximumPacketSizeExceededException exception)
            {
                if (packet is MqttPublishPacket)
                {
                    _logger.Warning(exception, "Client '{0}': Dropping an oversized PUBLISH packet.", ClientId);
                    return;
                }

                _logger.Error(exception, "Client '{0}': A control packet exceeds the maximum packet size.", ClientId);
                throw;
            }

            Interlocked.Increment(ref _sentPacketsCount);

            if (packet is MqttPublishPacket)
            {
                Interlocked.Increment(ref _sentApplicationMessagesCount);
            }
        }
    }
}
