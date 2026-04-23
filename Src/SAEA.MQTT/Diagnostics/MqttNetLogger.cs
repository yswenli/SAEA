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
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MqttNetLogger类
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