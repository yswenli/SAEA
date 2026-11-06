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
*命名空间：SAEA.MQTT.Diagnostics
*文件名： MqttNetLogger
*版本号： v26.4.23.1
*唯一标识：52f581f6-7a36-4219-8aef-34b229336fb0
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttNetLogger类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;

namespace SAEA.MQTT.Diagnostics
{
    /// <summary>
    /// MQTTNet日志记录器
    /// </summary>
    public class MqttNetLogger : IMqttNetLogger
    {
        readonly string _logId;

        /// <summary>
        /// 创建不带日志标识的日志记录器
        /// </summary>
        public MqttNetLogger()
        {
        }

        /// <summary>
        /// 创建带日志标识的日志记录器
        /// </summary>
        /// <param name="logId">日志标识，写入每条日志消息的 LogId，可为 null</param>
        public MqttNetLogger(string logId)
        {
            _logId = logId;
        }

        /// <summary>
        /// 本记录器发布日志消息时触发
        /// </summary>
        public event EventHandler<MqttNetLogMessagePublishedEventArgs> LogMessagePublished;

        /// <summary>
        /// 创建绑定指定来源的作用域日志记录器
        /// </summary>
        /// <param name="source">日志来源标识，通常为组件名，不能为 null</param>
        /// <returns>固定使用该来源的作用域日志记录器</returns>
        public IMqttNetScopedLogger CreateScopedLogger(string source)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));

            return new MqttNetScopedLogger(this, source);
        }

        /// <summary>
        /// 按指定级别发布日志消息，无本地与全局监听器时直接丢弃
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="source">日志来源标识，通常为组件名，可为 null</param>
        /// <param name="message">消息模板，含 {0} 等占位符，为 null 时按空字符串处理</param>
        /// <param name="parameters">格式化消息模板的参数数组，不能为 null</param>
        /// <param name="exception">关联异常，无异常时为 null</param>
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