using System;

namespace SAEA.MQTT.Diagnostics
{
    /// <summary>
    /// MQTTNet日志记录器
    /// </summary>
    public class MqttNetLogger : IMqttNetLogger
    {
        readonly string _logId;

        // 默认构造函数
        public MqttNetLogger()
        {
        }

        // 带有logId参数的构造函数
        public MqttNetLogger(string logId)
        {
            _logId = logId;
        }

        // 当日志消息发布时触发的事件
        public event EventHandler<MqttNetLogMessagePublishedEventArgs> LogMessagePublished;

        // 创建一个具有指定来源的作用域日志记录器
        public IMqttNetScopedLogger CreateScopedLogger(string source)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));

            return new MqttNetScopedLogger(this, source);
        }

        // 使用指定参数发布日志消息
        public void Publish(MqttNetLogLevel level, string source, string message, object[] parameters, Exception exception)
        {
            // 检查是否有本地或全局监听器
            var hasLocalListeners = LogMessagePublished != null;
            var hasGlobalListeners = MqttNetGlobalLogger.HasListeners;

            // 如果没有监听器，提前返回
            if (!hasLocalListeners && !hasGlobalListeners)
            {
                return;
            }

            // 使用提供的参数格式化消息
            try
            {
                message = string.Format(message ?? string.Empty, parameters);
            }
            catch (FormatException)
            {
                message = "消息格式无效: " + message;
            }

            // 创建一个新的日志消息
            var logMessage = new MqttNetLogMessage
            {
                LogId = _logId,
                Timestamp = DateTime.UtcNow,
                Source = source,
                ThreadId = Environment.CurrentManagedThreadId,
                Level = level,
                Message = message,
                Exception = exception
            };

            // 如果有全局监听器，发布日志消息
            if (hasGlobalListeners)
            {
                MqttNetGlobalLogger.Publish(logMessage);
            }

            // 如果有本地监听器，发布日志消息
            if (hasLocalListeners)
            {
                LogMessagePublished?.Invoke(this, new MqttNetLogMessagePublishedEventArgs(logMessage));
            }
        }
    }
}