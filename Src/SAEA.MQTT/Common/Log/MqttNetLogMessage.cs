/****************************************************************************
*项目名称：SAEA.MQTT.Common.Log
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Common.Log
*类 名 称：MqttNetLogMessage
*版 本 号： v4.3.2.5
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 10:18:43
*描述：
*=====================================================================
*修改时间：2019/1/15 10:18:43
*修 改 人： yswenli
*版 本 号： v4.3.2.5
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.MQTT.Common.Log
{
    public class MqttNetLogMessage
    {
        public MqttNetLogMessage(string logId, DateTime timestamp, int threadId, string source, MqttNetLogLevel level, string message, Exception exception)
        {
            LogId = logId;
            Timestamp = timestamp;
            ThreadId = threadId;
            Source = source;
            Level = level;
            Message = message;
            Exception = exception;
        }

        public string LogId { get; }

        public DateTime Timestamp { get; }

        public int ThreadId { get; }

        public string Source { get; }

        public MqttNetLogLevel Level { get; }

        public string Message { get; }

        public Exception Exception { get; }

        public override string ToString()
        {
            var result = $"[{Timestamp:O}] [{LogId}] [{ThreadId}] [{Source}] [{Level}]: {Message}";
            if (Exception != null)
            {
                result += Environment.NewLine + Exception;
            }

            return result;
        }
    }
}
