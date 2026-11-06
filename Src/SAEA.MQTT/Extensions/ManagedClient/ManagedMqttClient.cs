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
*命名空间：SAEA.MQTT.Extensions.ManagedClient
*文件名： ManagedMqttClient
*版本号： v26.4.23.1
*唯一标识：c6bc4cf3-9e3d-4ee8-b9c9-9884d63de9c4
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：ManagedMqttClient接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.MQTT.Client;
using SAEA.MQTT.Client.Connecting;
using SAEA.MQTT.Client.Disconnecting;
using SAEA.MQTT.Client.Publishing;
using SAEA.MQTT.Client.Receiving;
using SAEA.MQTT.Diagnostics;
using SAEA.MQTT.Exceptions;
using SAEA.MQTT.Internal;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.Extensions.ManagedClient
{
    /// <summary>
    /// 托管MQTT客户端，在底层客户端之上提供自动重连、待发队列与订阅同步
    /// </summary>
    public class ManagedMqttClient : Disposable, IManagedMqttClient
    {
        readonly BlockingQueue<ManagedMqttApplicationMessage> _messageQueue = new BlockingQueue<ManagedMqttApplicationMessage>();

        /// <summary>
        /// The subscriptions are managed in 2 separate buckets:
        /// <see cref="_subscriptions"/> and <see cref="_unsubscriptions"/> are processed during normal operation
        /// and are moved to the <see cref="_reconnectSubscriptions"/> when they get processed. They can be accessed by
        /// any thread and are therefore mutex'ed. <see cref="_reconnectSubscriptions"/> get sent to the broker
        ///  at reconnect and are solely owned by <see cref="MaintainConnectionAsync"/>.
        /// </summary>
        readonly Dictionary<string, MqttQualityOfServiceLevel> _reconnectSubscriptions = new Dictionary<string, MqttQualityOfServiceLevel>();
        readonly Dictionary<string, MqttQualityOfServiceLevel> _subscriptions = new Dictionary<string, MqttQualityOfServiceLevel>();
        readonly HashSet<string> _unsubscriptions = new HashSet<string>();
        readonly SemaphoreSlim _subscriptionsQueuedSignal = new SemaphoreSlim(0);

        readonly IMqttClient _mqttClient;
        readonly IMqttNetScopedLogger _logger;

        readonly AsyncLock _messageQueueLock = new AsyncLock();

        CancellationTokenSource _connectionCancellationToken;
        CancellationTokenSource _publishingCancellationToken;
        Task _maintainConnectionTask;

        ManagedMqttClientStorageManager _storageManager;

        /// <summary>
        /// 使用内部客户端与日志器初始化实例
        /// </summary>
        /// <param name="mqttClient">内部实际使用的MQTT客户端，不能为null</param>
        /// <param name="logger">根日志器，不能为null；内部会创建独立作用域的日志器</param>
        public ManagedMqttClient(IMqttClient mqttClient, IMqttNetLogger logger)
        {
            _mqttClient = mqttClient ?? throw new ArgumentNullException(nameof(mqttClient));
            InternalClient = mqttClient;

            if (logger == null) throw new ArgumentNullException(nameof(logger));
            _logger = logger.CreateScopedLogger(nameof(ManagedMqttClient));
        }

        /// <summary>
        /// 内部客户端当前是否已连接
        /// </summary>
        public bool IsConnected => _mqttClient.IsConnected;

        /// <summary>
        /// 是否已启动，以连接维护是否已启动为准
        /// </summary>
        public bool IsStarted => _connectionCancellationToken != null;
        
        /// <summary>
        /// 内部实际使用的MQTT客户端
        /// </summary>
        public IMqttClient InternalClient { get; }

        /// <summary>
        /// 排队等待发送的应用消息数量
        /// </summary>
        public int PendingApplicationMessagesCount => _messageQueue.Count;
        
        /// <summary>
        /// 当前生效的托管客户端选项，启动前为null
        /// </summary>
        public IManagedMqttClientOptions Options { get; private set; }

        /// <summary>
        /// 连接成功事件处理器，读写均转发给内部客户端
        /// </summary>
        public IMqttClientConnectedHandler ConnectedHandler
        {
            get => _mqttClient.ConnectedHandler;
            set => _mqttClient.ConnectedHandler = value;
        }

        /// <summary>
        /// 连接断开事件处理器，读写均转发给内部客户端
        /// </summary>
        public IMqttClientDisconnectedHandler DisconnectedHandler
        {
            get => _mqttClient.DisconnectedHandler;
            set => _mqttClient.DisconnectedHandler = value;
        }

        /// <summary>
        /// 应用消息接收事件处理器，读写均转发给内部客户端
        /// </summary>
        public IMqttApplicationMessageReceivedHandler ApplicationMessageReceivedHandler
        {
            get => _mqttClient.ApplicationMessageReceivedHandler;
            set => _mqttClient.ApplicationMessageReceivedHandler = value;
        }

        /// <summary>
        /// 应用消息处理完成事件处理器
        /// </summary>
        public IApplicationMessageProcessedHandler ApplicationMessageProcessedHandler { get; set; }

        /// <summary>
        /// 应用消息被跳过事件处理器
        /// </summary>
        public IApplicationMessageSkippedHandler ApplicationMessageSkippedHandler { get; set; }

        /// <summary>
        /// 连接失败事件处理器
        /// </summary>
        public IConnectingFailedHandler ConnectingFailedHandler { get; set; }

        /// <summary>
        /// 订阅同步失败事件处理器
        /// </summary>
        public ISynchronizingSubscriptionsFailedHandler SynchronizingSubscriptionsFailedHandler { get; set; }

        /// <summary>
        /// 用指定选项启动托管客户端，可选地加载已持久化的待发消息并启动连接维护
        /// </summary>
        /// <param name="options">托管客户端选项，其 ClientOptions 不能为null</param>
        /// <returns>启动完成的异步任务</returns>
        public async Task StartAsync(IManagedMqttClientOptions options)
        {
            ThrowIfDisposed();

            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options.ClientOptions == null) throw new ArgumentException("The client options are not set.", nameof(options));
            
            if (!_maintainConnectionTask?.IsCompleted ?? false) throw new InvalidOperationException("The managed client is already started.");

            Options = options;

            if (Options.Storage != null)
            {
                _storageManager = new ManagedMqttClientStorageManager(Options.Storage);
                var messages = await _storageManager.LoadQueuedMessagesAsync().ConfigureAwait(false);

                foreach (var message in messages)
                {
                    _messageQueue.Enqueue(message);
                }
            }

            if (Options.AutoReconnect)
            {
                var cancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = cancellationTokenSource.Token;
                _connectionCancellationToken = cancellationTokenSource;

                _maintainConnectionTask = Task.Run(() => MaintainConnectionAsync(cancellationToken), cancellationToken);
                _maintainConnectionTask.Forget(_logger);
            }
            
            _logger.Info("Started");
        }

        /// <summary>
        /// 停止消息发送与连接维护，并清空待发队列
        /// </summary>
        /// <returns>连接维护任务结束后的异步任务</returns>
        public async Task StopAsync()
        {
            ThrowIfDisposed();

            StopPublishing();
            StopMaintainingConnection();

            _messageQueue.Clear();

            if (_maintainConnectionTask != null)
            {
                await Task.WhenAny(_maintainConnectionTask);
                _maintainConnectionTask = null;
            }
        }

        /// <summary>
        /// 通过内部客户端发送心跳
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>心跳完成的异步任务</returns>
        public Task PingAsync(CancellationToken cancellationToken)
        {
            return _mqttClient.PingAsync(cancellationToken);
        }

        /// <summary>
        /// 将应用消息包装后放入待发队列
        /// </summary>
        /// <param name="applicationMessage">要发送的应用消息，不能为null</param>
        /// <param name="cancellationToken">取消令牌，入队本身不使用该令牌</param>
        /// <returns>始终返回未携带结果的状态对象，实际发送结果通过处理完成事件通知</returns>
        public async Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (applicationMessage == null) throw new ArgumentNullException(nameof(applicationMessage));

            await PublishAsync(new ManagedMqttApplicationMessageBuilder().WithApplicationMessage(applicationMessage).Build()).ConfigureAwait(false);
            return new MqttClientPublishResult();
        }

        /// <summary>
        /// 将托管应用消息放入待发队列，队列满时按配置的溢出策略处理
        /// </summary>
        /// <param name="applicationMessage">带唯一标识的待发消息，不能为null</param>
        /// <returns>入队完成的异步任务</returns>
        public async Task PublishAsync(ManagedMqttApplicationMessage applicationMessage)
        {
            ThrowIfDisposed();

            if (applicationMessage == null) throw new ArgumentNullException(nameof(applicationMessage));
            if (Options == null) throw new InvalidOperationException("call StartAsync before publishing messages");

            MqttTopicValidator.ThrowIfInvalid(applicationMessage.ApplicationMessage.Topic);

            ManagedMqttApplicationMessage removedMessage = null;
            ApplicationMessageSkippedEventArgs applicationMessageSkippedEventArgs = null;

            try
            {
                using (await _messageQueueLock.WaitAsync(CancellationToken.None).ConfigureAwait(false))
                {
                    if (_messageQueue.Count >= Options.MaxPendingMessages)
                    {
                        if (Options.PendingMessagesOverflowStrategy == MqttPendingMessagesOverflowStrategy.DropNewMessage)
                        {
                            _logger.Verbose("Skipping publish of new application message because internal queue is full.");
                            applicationMessageSkippedEventArgs = new ApplicationMessageSkippedEventArgs(applicationMessage);
                            return;
                        }

                        if (Options.PendingMessagesOverflowStrategy == MqttPendingMessagesOverflowStrategy.DropOldestQueuedMessage)
                        {
                            removedMessage = _messageQueue.RemoveFirst();
                            _logger.Verbose("Removed oldest application message from internal queue because it is full.");
                            applicationMessageSkippedEventArgs = new ApplicationMessageSkippedEventArgs(removedMessage);
                        }
                    }

                    _messageQueue.Enqueue(applicationMessage);

                    if (_storageManager != null)
                    {
                        if (removedMessage != null)
                        {
                            await _storageManager.RemoveAsync(removedMessage).ConfigureAwait(false);
                        }

                        await _storageManager.AddAsync(applicationMessage).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                if (applicationMessageSkippedEventArgs != null)
                {
                    var applicationMessageSkippedHandler = ApplicationMessageSkippedHandler;
                    if (applicationMessageSkippedHandler != null)
                    {
                        await applicationMessageSkippedHandler.HandleApplicationMessageSkippedAsync(applicationMessageSkippedEventArgs).ConfigureAwait(false);
                    }
                }

            }
        }

        /// <summary>
        /// 登记订阅，由后台在连接可用时下发并在重连后自动恢复
        /// </summary>
        /// <param name="topicFilters">主题过滤条件集合，不能为null</param>
        /// <returns>登记完成的任务，不等待服务端确认</returns>
        public Task SubscribeAsync(IEnumerable<MqttTopicFilter> topicFilters)
        {
            ThrowIfDisposed();

            if (topicFilters == null) throw new ArgumentNullException(nameof(topicFilters));

            lock (_subscriptions)
            {
                foreach (var topicFilter in topicFilters)
                {
                    _subscriptions[topicFilter.Topic] = topicFilter.QualityOfServiceLevel;
                    _unsubscriptions.Remove(topicFilter.Topic);
                }
            }

            _subscriptionsQueuedSignal.Release();

            return Task.FromResult(0);
        }

        /// <summary>
        /// 登记取消订阅，由后台在连接可用时下发
        /// </summary>
        /// <param name="topics">要取消订阅的主题集合，不能为null</param>
        /// <returns>登记完成的任务，不等待服务端确认</returns>
        public Task UnsubscribeAsync(IEnumerable<string> topics)
        {
            ThrowIfDisposed();

            if (topics == null) throw new ArgumentNullException(nameof(topics));

            lock (_subscriptions)
            {
                foreach (var topic in topics)
                {
                    _subscriptions.Remove(topic);
                    _unsubscriptions.Add(topic);
                }
            }
            _subscriptionsQueuedSignal.Release();

            return Task.FromResult(0);
        }

        /// <summary>
        /// 释放托管客户端，同步等待连接维护结束并释放内部客户端与队列资源
        /// </summary>
        /// <param name="disposing">为true时释放托管资源</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopPublishing();
                StopMaintainingConnection();

                if (_maintainConnectionTask != null)
                {
                    _maintainConnectionTask.GetAwaiter().GetResult();
                    _maintainConnectionTask = null;
                }

                _messageQueue.Dispose();
                _messageQueueLock.Dispose();
                _mqttClient.Dispose();
                _subscriptionsQueuedSignal.Dispose();
            }

            base.Dispose(disposing);
        }

        async Task MaintainConnectionAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await TryMaintainConnectionAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Error exception while maintaining connection.");
            }
            finally
            {
                if (!IsDisposed)
                {
                    try
                    {
                        await _mqttClient.DisconnectAsync().ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        _logger.Error(exception, "Error while disconnecting.");
                    }

                    _logger.Info("Stopped");
                }

                _reconnectSubscriptions.Clear();

                lock (_subscriptions)
                {
                    _subscriptions.Clear();
                    _unsubscriptions.Clear();
                }
            }
        }

        async Task TryMaintainConnectionAsync(CancellationToken cancellationToken)
        {
            try
            {
                var connectionState = await ReconnectIfRequiredAsync().ConfigureAwait(false);
                if (connectionState == ReconnectionResult.NotConnected)
                {
                    StopPublishing();
                    await Task.Delay(Options.AutoReconnectDelay, cancellationToken).ConfigureAwait(false);
                    return;
                }

                if (connectionState == ReconnectionResult.Reconnected)
                {
                    await PublishReconnectSubscriptionsAsync().ConfigureAwait(false);
                    StartPublishing();
                    return;
                }

                if (connectionState == ReconnectionResult.Recovered)
                {
                    StartPublishing();
                    return;
                }

                if (connectionState == ReconnectionResult.StillConnected)
                {
                    await PublishSubscriptionsAsync(Options.ConnectionCheckInterval, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (MqttCommunicationException exception)
            {
                _logger.Warning(exception, "Communication error while maintaining connection.");
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Error exception while maintaining connection.");
            }
        }

        async Task PublishQueuedMessagesAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _mqttClient.IsConnected)
                {
                    // Peek at the message without dequeueing in order to prevent the
                    // possibility of the queue growing beyond the configured cap.
                    // Previously, messages could be re-enqueued if there was an
                    // exception, and this re-enqueueing did not honor the cap.
                    // Furthermore, because re-enqueueing would shuffle the order
                    // of the messages, the DropOldestQueuedMessage strategy would
                    // be unable to know which message is actually the oldest and would
                    // instead drop the first item in the queue.
                    var message = _messageQueue.PeekAndWait(cancellationToken);
                    if (message == null)
                    {
                        continue;
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    await TryPublishQueuedMessageAsync(message).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Error while publishing queued application messages.");
            }
            finally
            {
                _logger.Verbose("Stopped publishing messages.");
            }
        }

        async Task TryPublishQueuedMessageAsync(ManagedMqttApplicationMessage message)
        {
            Exception transmitException = null;
            try
            {
                await _mqttClient.PublishAsync(message.ApplicationMessage).ConfigureAwait(false);

                using (await _messageQueueLock.WaitAsync(CancellationToken.None).ConfigureAwait(false)) //lock to avoid conflict with this.PublishAsync
                {
                    // While publishing this message, this.PublishAsync could have booted this
                    // message off the queue to make room for another (when using a cap
                    // with the DropOldestQueuedMessage strategy).  If the first item
                    // in the queue is equal to this message, then it's safe to remove
                    // it from the queue.  If not, that means this.PublishAsync has already
                    // removed it, in which case we don't want to do anything.
                    _messageQueue.RemoveFirst(i => i.Id.Equals(message.Id));

                    if (_storageManager != null)
                    {
                        await _storageManager.RemoveAsync(message).ConfigureAwait(false);
                    }
                }
            }
            catch (MqttCommunicationException exception)
            {
                transmitException = exception;

                _logger.Warning(exception, $"Publishing application ({message.Id}) message failed.");

                if (message.ApplicationMessage.QualityOfServiceLevel == MqttQualityOfServiceLevel.AtMostOnce)
                {
                    //If QoS 0, we don't want this message to stay on the queue.
                    //If QoS 1 or 2, it's possible that, when using a cap, this message
                    //has been booted off the queue by this.PublishAsync, in which case this
                    //thread will not continue to try to publish it. While this does
                    //contradict the expected behavior of QoS 1 and 2, that's also true
                    //for the usage of a message queue cap, so it's still consistent
                    //with prior behavior in that way.
                    using (await _messageQueueLock.WaitAsync(CancellationToken.None).ConfigureAwait(false)) //lock to avoid conflict with this.PublishAsync
                    {
                        _messageQueue.RemoveFirst(i => i.Id.Equals(message.Id));

                        if (_storageManager != null)
                        {
                            await _storageManager.RemoveAsync(message).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                transmitException = exception;
                _logger.Error(exception, $"Error while publishing application message ({message.Id}).");
            }
            finally
            {
                var eventHandler = ApplicationMessageProcessedHandler;
                if (eventHandler != null)
                {
                    var eventArguments = new ApplicationMessageProcessedEventArgs(message, transmitException);
                    await eventHandler.HandleApplicationMessageProcessedAsync(eventArguments).ConfigureAwait(false);
                }
            }
        }

        async Task PublishSubscriptionsAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            var endTime = DateTime.UtcNow + timeout;
            while (await _subscriptionsQueuedSignal.WaitAsync(GetRemainingTime(endTime), cancellationToken).ConfigureAwait(false))
            {
                List<MqttTopicFilter> subscriptions;
                HashSet<string> unsubscriptions;

                lock (_subscriptions)
                {
                    subscriptions = _subscriptions.Select(i => new MqttTopicFilter { Topic = i.Key, QualityOfServiceLevel = i.Value }).ToList();
                    _subscriptions.Clear();
                    unsubscriptions = new HashSet<string>(_unsubscriptions);
                    _unsubscriptions.Clear();
                }

                if (!subscriptions.Any() && !unsubscriptions.Any())
                {
                    continue;
                }

                _logger.Verbose($"Publishing subscriptions ({subscriptions.Count} subscriptions and {unsubscriptions.Count} unsubscriptions)");

                foreach (var unsubscription in unsubscriptions)
                {
                    _reconnectSubscriptions.Remove(unsubscription);
                }

                foreach (var subscription in subscriptions)
                {
                    _reconnectSubscriptions[subscription.Topic] = subscription.QualityOfServiceLevel;
                }

                try
                {
                    if (unsubscriptions.Any())
                    {
                        await _mqttClient.UnsubscribeAsync(unsubscriptions.ToArray()).ConfigureAwait(false);
                    }

                    if (subscriptions.Any())
                    {
                        await _mqttClient.SubscribeAsync(subscriptions.ToArray()).ConfigureAwait(false);
                    }
                }
                catch (Exception exception)
                {
                    await HandleSubscriptionExceptionAsync(exception).ConfigureAwait(false);
                }
            }
        }

        async Task PublishReconnectSubscriptionsAsync()
        {
            _logger.Info("Publishing subscriptions at reconnect");

            try
            {
                if (_reconnectSubscriptions.Any())
                {
                    var subscriptions = _reconnectSubscriptions.Select(i => new MqttTopicFilter { Topic = i.Key, QualityOfServiceLevel = i.Value });
                    await _mqttClient.SubscribeAsync(subscriptions.ToArray()).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                await HandleSubscriptionExceptionAsync(exception).ConfigureAwait(false);
            }
        }

        async Task HandleSubscriptionExceptionAsync(Exception exception)
        {
            _logger.Warning(exception, "Synchronizing subscriptions failed.");

            var synchronizingSubscriptionsFailedHandler = SynchronizingSubscriptionsFailedHandler;
            if (SynchronizingSubscriptionsFailedHandler != null)
            {
                await synchronizingSubscriptionsFailedHandler.HandleSynchronizingSubscriptionsFailedAsync(new ManagedProcessFailedEventArgs(exception)).ConfigureAwait(false);
            }
        }

        async Task<ReconnectionResult> ReconnectIfRequiredAsync()
        {
            if (_mqttClient.IsConnected)
            {
                return ReconnectionResult.StillConnected;
            }

            try
            {
                var result = await _mqttClient.ConnectAsync(Options.ClientOptions).ConfigureAwait(false);
                return result.IsSessionPresent ? ReconnectionResult.Recovered : ReconnectionResult.Reconnected;
            }
            catch (Exception exception)
            {
                var connectingFailedHandler = ConnectingFailedHandler;
                if (connectingFailedHandler != null)
                {
                    await connectingFailedHandler.HandleConnectingFailedAsync(new ManagedProcessFailedEventArgs(exception)).ConfigureAwait(false);
                }

                return ReconnectionResult.NotConnected;
            }
        }

        void StartPublishing()
        {
            if (_publishingCancellationToken != null)
            {
                StopPublishing();
            }

            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            _publishingCancellationToken = cancellationTokenSource;

            Task.Run(() => PublishQueuedMessagesAsync(cancellationToken), cancellationToken).Forget(_logger);
        }

        void StopPublishing()
        {
            try
            {
                _publishingCancellationToken?.Cancel(false);
            }
            finally
            {
                _publishingCancellationToken?.Dispose();
                _publishingCancellationToken = null;
            }
        }

        void StopMaintainingConnection()
        {
            try
            {
                _connectionCancellationToken?.Cancel(false);
            }
            finally
            {
                _connectionCancellationToken?.Dispose();
                _connectionCancellationToken = null;
            }
        }

        static TimeSpan GetRemainingTime(DateTime endTime)
        {
            var remainingTime = endTime - DateTime.UtcNow;
            return remainingTime < TimeSpan.Zero ? TimeSpan.Zero : remainingTime;
        }
    }
}
