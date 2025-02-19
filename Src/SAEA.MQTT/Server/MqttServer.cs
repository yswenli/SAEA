using SAEA.MQTT.Adapter;
using SAEA.MQTT.Client.Publishing;
using SAEA.MQTT.Client.Receiving;
using SAEA.MQTT.Diagnostics;
using SAEA.MQTT.Exceptions;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Server.Status;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SAEA.MQTT.Implementations;
using SAEA.MQTT.Internal;

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 定义MqttServer类，继承自Disposable，实现IMqttServer接口
    /// </summary>
    public class MqttServer : Disposable, IMqttServer
    {
        readonly MqttServerEventDispatcher _eventDispatcher;
        readonly ICollection<IMqttServerAdapter> _adapters;
        readonly IMqttNetLogger _rootLogger;
        readonly IMqttNetScopedLogger _logger;

        MqttClientSessionsManager _clientSessionsManager;
        IMqttRetainedMessagesManager _retainedMessagesManager;
        MqttServerKeepAliveMonitor _keepAliveMonitor;
        CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// 初始化MqttServer类的新实例
        /// </summary>
        /// <param name="adapters">MQTT服务器适配器集合</param>
        /// <param name="logger">日志记录器</param>
        public MqttServer(IEnumerable<IMqttServerAdapter> adapters, IMqttNetLogger logger)
        {
            if (adapters == null) throw new ArgumentNullException(nameof(adapters));
            _adapters = adapters.ToList();

            if (logger == null) throw new ArgumentNullException(nameof(logger));
            _logger = logger.CreateScopedLogger(nameof(MqttServer));
            _rootLogger = logger;

            _eventDispatcher = new MqttServerEventDispatcher(logger);
        }

        /// <summary>
        /// 获取服务器是否已启动
        /// </summary>
        public bool IsStarted => _cancellationTokenSource != null;

        /// <summary>
        /// 获取或设置服务器启动处理程序
        /// </summary>
        public IMqttServerStartedHandler StartedHandler { get; set; }

        /// <summary>
        /// 获取或设置服务器停止处理程序
        /// </summary>
        public IMqttServerStoppedHandler StoppedHandler { get; set; }

        /// <summary>
        /// 获取或设置客户端连接处理程序
        /// </summary>
        public IMqttServerClientConnectedHandler ClientConnectedHandler
        {
            get => _eventDispatcher.ClientConnectedHandler;
            set => _eventDispatcher.ClientConnectedHandler = value;
        }

        /// <summary>
        /// 获取或设置客户端断开连接处理程序
        /// </summary>
        public IMqttServerClientDisconnectedHandler ClientDisconnectedHandler
        {
            get => _eventDispatcher.ClientDisconnectedHandler;
            set => _eventDispatcher.ClientDisconnectedHandler = value;
        }

        /// <summary>
        /// 获取或设置客户端订阅主题处理程序
        /// </summary>
        public IMqttServerClientSubscribedTopicHandler ClientSubscribedTopicHandler
        {
            get => _eventDispatcher.ClientSubscribedTopicHandler;
            set => _eventDispatcher.ClientSubscribedTopicHandler = value;
        }

        /// <summary>
        /// 获取或设置客户端取消订阅主题处理程序
        /// </summary>
        public IMqttServerClientUnsubscribedTopicHandler ClientUnsubscribedTopicHandler
        {
            get => _eventDispatcher.ClientUnsubscribedTopicHandler;
            set => _eventDispatcher.ClientUnsubscribedTopicHandler = value;
        }

        /// <summary>
        /// 获取或设置应用消息接收处理程序
        /// </summary>
        public IMqttApplicationMessageReceivedHandler ApplicationMessageReceivedHandler
        {
            get => _eventDispatcher.ApplicationMessageReceivedHandler;
            set => _eventDispatcher.ApplicationMessageReceivedHandler = value;
        }

        /// <summary>
        /// 获取服务器选项
        /// </summary>
        public IMqttServerOptions Options { get; private set; }

        /// <summary>
        /// 获取客户端状态的异步方法
        /// </summary>
        /// <returns>客户端状态列表</returns>
        public Task<IList<IMqttClientStatus>> GetClientStatusAsync()
        {
            ThrowIfDisposed();
            ThrowIfNotStarted();

            return _clientSessionsManager.GetClientStatusAsync();
        }

        /// <summary>
        /// 获取会话状态的异步方法
        /// </summary>
        /// <returns>会话状态列表</returns>
        public Task<IList<IMqttSessionStatus>> GetSessionStatusAsync()
        {
            ThrowIfDisposed();
            ThrowIfNotStarted();

            return _clientSessionsManager.GetSessionStatusAsync();
        }

        /// <summary>
        /// 获取保留的应用消息的异步方法
        /// </summary>
        /// <returns>保留的应用消息列表</returns>
        public Task<IList<MqttApplicationMessage>> GetRetainedApplicationMessagesAsync()
        {
            ThrowIfDisposed();
            ThrowIfNotStarted();

            return _retainedMessagesManager.GetMessagesAsync();
        }

        /// <summary>
        /// 清除保留的应用消息的异步方法
        /// </summary>
        /// <returns>任务</returns>
        public Task ClearRetainedApplicationMessagesAsync()
        {
            ThrowIfDisposed();
            ThrowIfNotStarted();

            return _retainedMessagesManager?.ClearMessagesAsync() ?? PlatformAbstractionLayer.CompletedTask;
        }

        /// <summary>
        /// 订阅主题的异步方法
        /// </summary>
        /// <param name="clientId">客户端ID</param>
        /// <param name="topicFilters">主题过滤器集合</param>
        /// <returns>任务</returns>
        public Task SubscribeAsync(string clientId, ICollection<MqttTopicFilter> topicFilters)
        {
            if (clientId == null) throw new ArgumentNullException(nameof(clientId));
            if (topicFilters == null) throw new ArgumentNullException(nameof(topicFilters));

            ThrowIfDisposed();
            ThrowIfNotStarted();

            return _clientSessionsManager.SubscribeAsync(clientId, topicFilters);
        }

        /// <summary>
        /// 取消订阅主题的异步方法
        /// </summary>
        /// <param name="clientId">客户端ID</param>
        /// <param name="topicFilters">主题过滤器集合</param>
        /// <returns>任务</returns>
        public Task UnsubscribeAsync(string clientId, ICollection<string> topicFilters)
        {
            if (clientId == null) throw new ArgumentNullException(nameof(clientId));
            if (topicFilters == null) throw new ArgumentNullException(nameof(topicFilters));

            ThrowIfDisposed();
            ThrowIfNotStarted();

            return _clientSessionsManager.UnsubscribeAsync(clientId, topicFilters);
        }

        /// <summary>
        /// 发布消息的异步方法
        /// </summary>
        /// <param name="applicationMessage">应用消息</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>发布结果</returns>
        public Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken)
        {
            if (applicationMessage == null) throw new ArgumentNullException(nameof(applicationMessage));

            ThrowIfDisposed();

            MqttTopicValidator.ThrowIfInvalid(applicationMessage.Topic);

            ThrowIfNotStarted();

            _clientSessionsManager.DispatchApplicationMessage(applicationMessage, null);

            return Task.FromResult(new MqttClientPublishResult());
        }

        /// <summary>
        /// 启动服务器的异步方法
        /// </summary>
        /// <param name="options">服务器选项</param>
        /// <returns>任务</returns>
        public async Task StartAsync(IMqttServerOptions options)
        {
            ThrowIfDisposed();
            ThrowIfStarted();

            Options = options ?? throw new ArgumentNullException(nameof(options));

            _cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _cancellationTokenSource.Token;

            _retainedMessagesManager = Options.RetainedMessagesManager ?? throw new MqttConfigurationException("options.RetainedMessagesManager should not be null.");

            await _retainedMessagesManager.Start(Options, _rootLogger).ConfigureAwait(false);
            await _retainedMessagesManager.LoadMessagesAsync().ConfigureAwait(false);

            _clientSessionsManager = new MqttClientSessionsManager(Options, _retainedMessagesManager, _eventDispatcher, _rootLogger);
            _clientSessionsManager.Start(cancellationToken);

            _keepAliveMonitor = new MqttServerKeepAliveMonitor(Options, _clientSessionsManager, _rootLogger);
            _keepAliveMonitor.Start(cancellationToken);

            foreach (var adapter in _adapters)
            {
                adapter.ClientHandler = c => OnHandleClient(c, cancellationToken);
                await adapter.StartAsync(Options).ConfigureAwait(false);
            }

            _logger.Info("Started.");

            var startedHandler = StartedHandler;
            if (startedHandler != null)
            {
                await startedHandler.HandleServerStartedAsync(EventArgs.Empty).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 停止服务器的异步方法
        /// </summary>
        /// <returns>任务</returns>
        public async Task StopAsync()
        {
            try
            {
                if (_cancellationTokenSource == null)
                {
                    return;
                }

                _cancellationTokenSource.Cancel(false);

                await _clientSessionsManager.CloseAllConnectionsAsync().ConfigureAwait(false);

                foreach (var adapter in _adapters)
                {
                    adapter.ClientHandler = null;
                    await adapter.StopAsync().ConfigureAwait(false);
                }

                _logger.Info("Stopped.");
            }
            finally
            {
                _clientSessionsManager?.Dispose();
                _clientSessionsManager = null;

                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;

                _retainedMessagesManager = null;
            }

            var stoppedHandler = StoppedHandler;
            if (stoppedHandler != null)
            {
                await stoppedHandler.HandleServerStoppedAsync(EventArgs.Empty).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 释放资源的方法
        /// </summary>
        /// <param name="disposing">是否释放托管资源</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopAsync().GetAwaiter().GetResult();

                foreach (var adapter in _adapters)
                {
                    adapter.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// 处理客户端连接的异步方法
        /// </summary>
        /// <param name="channelAdapter">通道适配器</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务</returns>
        Task OnHandleClient(IMqttChannelAdapter channelAdapter, CancellationToken cancellationToken)
        {
            return _clientSessionsManager.HandleClientConnectionAsync(channelAdapter, cancellationToken);
        }

        /// <summary>
        /// 检查服务器是否已启动的方法
        /// </summary>
        void ThrowIfStarted()
        {
            if (_cancellationTokenSource != null)
            {
                throw new InvalidOperationException("The MQTT server is already started.");
            }
        }

        /// <summary>
        /// 检查服务器是否未启动的方法
        /// </summary>
        void ThrowIfNotStarted()
        {
            if (_cancellationTokenSource == null)
            {
                throw new InvalidOperationException("The MQTT server is not started.");
            }
        }
    }
}
