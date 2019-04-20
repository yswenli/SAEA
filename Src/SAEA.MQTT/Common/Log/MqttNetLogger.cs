/****************************************************************************
*项目名称：SAEA.MQTT.Common.Log
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Common.Log
*类 名 称：MqttNetLogger
*版 本 号： v4.3.3.7
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 10:33:08
*描述：
*=====================================================================
*修改时间：2019/1/15 10:33:08
*修 改 人： yswenli
*版 本 号： v4.3.3.7
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.MQTT.Common.Log
{
    public class MqttNetLogger : IMqttNetLogger
    {
        private readonly string _logId;

        public MqttNetLogger(string logId = null)
        {
            _logId = logId;
        }

        public event EventHandler<MqttNetLogMessagePublishedEventArgs> LogMessagePublished;

        public IMqttNetChildLogger CreateChildLogger(string source = null)
        {
            return new MqttNetChildLogger(this, source);
        }

        public void Publish(MqttNetLogLevel logLevel, string source, string message, object[] parameters, Exception exception)
        {
            var hasLocalListeners = LogMessagePublished != null;
            var hasGlobalListeners = MqttNetGlobalLogger.HasListeners;

            if (!hasLocalListeners && !hasGlobalListeners)
            {
                return;
            }

            if (parameters?.Length > 0)
            {
                try
                {
                    message = string.Format(message, parameters);
                }
                catch
                {
                    message = "MESSAGE FORMAT INVALID: " + message;
                }
            }

            var traceMessage = new MqttNetLogMessage(_logId, DateTime.UtcNow, Environment.CurrentManagedThreadId, source, logLevel, message, exception);

            if (hasGlobalListeners)
            {
                MqttNetGlobalLogger.Publish(traceMessage);
            }

            if (hasLocalListeners)
            {
                LogMessagePublished?.Invoke(this, new MqttNetLogMessagePublishedEventArgs(traceMessage));
            }
        }
    }
}
