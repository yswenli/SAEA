using SAEA.MQTT.Client.Receiving;
using SAEA.MQTT.Diagnostics;
using System;
using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// MQTT服务器事件分发器
    /// </summary>
    public sealed class MqttServerEventDispatcher
    {
        readonly IMqttNetScopedLogger _logger;

        /// <summary>
        /// 初始化MqttServerEventDispatcher类的新实例
        /// </summary>
        /// <param name="logger">日志记录器</param>
        public MqttServerEventDispatcher(IMqttNetLogger logger)
        {
            if (logger is null) throw new ArgumentNullException(nameof(logger));

            _logger = logger.CreateScopedLogger(nameof(MqttServerEventDispatcher));
        }

        /// <summary>
        /// 获取或设置客户端连接处理程序
        /// </summary>
        public IMqttServerClientConnectedHandler ClientConnectedHandler { get; set; }

        /// <summary>
        /// 获取或设置客户端断开连接处理程序
        /// </summary>
        public IMqttServerClientDisconnectedHandler ClientDisconnectedHandler { get; set; }

        /// <summary>
        /// 获取或设置客户端订阅主题处理程序
        /// </summary>
        public IMqttServerClientSubscribedTopicHandler ClientSubscribedTopicHandler { get; set; }

        /// <summary>
        /// 获取或设置客户端取消订阅主题处理程序
        /// </summary>
        public IMqttServerClientUnsubscribedTopicHandler ClientUnsubscribedTopicHandler { get; set; }

        /// <summary>
        /// 获取或设置应用消息接收处理程序
        /// </summary>
        public IMqttApplicationMessageReceivedHandler ApplicationMessageReceivedHandler { get; set; }

        /// <summary>
        /// 安全地通知客户端连接的异步方法
        /// </summary>
        /// <param name="clientId">客户端ID</param>
        /// <returns>任务</returns>
        public async Task SafeNotifyClientConnectedAsync(string clientId)
        {
            try
            {
                var handler = ClientConnectedHandler;
                if (handler == null)
                {
                    return;
                }

                await handler.HandleClientConnectedAsync(new MqttServerClientConnectedEventArgs(clientId)).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Error while handling custom 'ClientConnected' event.");
            }
        }

        /// <summary>
        /// 安全地通知客户端断开连接的异步方法
        /// </summary>
        /// <param name="clientId">客户端ID</param>
        /// <param name="disconnectType">断开连接类型</param>
        /// <returns>任务</returns>
        public async Task SafeNotifyClientDisconnectedAsync(string clientId, MqttClientDisconnectType disconnectType)
        {
            try
            {
                var handler = ClientDisconnectedHandler;
                if (handler == null)
                {
                    return;
                }

                await handler.HandleClientDisconnectedAsync(new MqttServerClientDisconnectedEventArgs(clientId, disconnectType)).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Error while handling custom 'ClientDisconnected' event.");
            }
        }

        /// <summary>
        /// 安全地通知客户端订阅主题的异步方法
        /// </summary>
        /// <param name="clientId">客户端ID</param>
        /// <param name="topicFilter">主题过滤器</param>
        /// <returns>任务</returns>
        public async Task SafeNotifyClientSubscribedTopicAsync(string clientId, MqttTopicFilter topicFilter)
        {
            try
            {
                var handler = ClientSubscribedTopicHandler;
                if (handler == null)
                {
                    return;
                }

                await handler.HandleClientSubscribedTopicAsync(new MqttServerClientSubscribedTopicEventArgs(clientId, topicFilter)).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Error while handling custom 'ClientSubscribedTopic' event.");
            }
        }

        /// <summary>
        /// 安全地通知客户端取消订阅主题的异步方法
        /// </summary>
        /// <param name="clientId">客户端ID</param>
        /// <param name="topicFilter">主题过滤器</param>
        /// <returns>任务</returns>
        public async Task SafeNotifyClientUnsubscribedTopicAsync(string clientId, string topicFilter)
        {
            try
            {
                var handler = ClientUnsubscribedTopicHandler;
                if (handler == null)
                {
                    return;
                }

                await handler.HandleClientUnsubscribedTopicAsync(new MqttServerClientUnsubscribedTopicEventArgs(clientId, topicFilter)).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Error while handling custom 'ClientUnsubscribedTopic' event.");
            }
        }

        /// <summary>
        /// 安全地通知应用消息接收的异步方法
        /// </summary>
        /// <param name="senderClientId">发送者客户端ID</param>
        /// <param name="applicationMessage">应用消息</param>
        /// <returns>任务</returns>
        public async Task SafeNotifyApplicationMessageReceivedAsync(string senderClientId, MqttApplicationMessage applicationMessage)
        {
            try
            {
                var handler = ApplicationMessageReceivedHandler;
                if (handler == null)
                {
                    return;
                }

                await handler.HandleApplicationMessageReceivedAsync(new MqttApplicationMessageReceivedEventArgs(senderClientId, applicationMessage)).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Error while handling custom 'ApplicationMessageReceived' event.");
            }
        }
    }
}
