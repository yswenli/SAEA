/****************************************************************************
*项目名称：SAEA.MQTT.Common.Log
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Common.Log
*类 名 称：MqttNetConsoleLogger
*版 本 号： v4.3.2.5
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 16:15:26
*描述：
*=====================================================================
*修改时间：2019/1/15 16:15:26
*修 改 人： yswenli
*版 本 号： v4.3.2.5
*描    述：
*****************************************************************************/
using System;
using System.Text;

namespace SAEA.MQTT.Common.Log
{
    public static class MqttNetConsoleLogger
    {
        private static readonly object Lock = new object();

        public static void ForwardToConsole()
        {
            MqttNetGlobalLogger.LogMessagePublished -= PrintToConsole;
            MqttNetGlobalLogger.LogMessagePublished += PrintToConsole;
        }

        public static void PrintToConsole(string message, ConsoleColor color)
        {
            lock (Lock)
            {
                var backupColor = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.Write(message);
                Console.ForegroundColor = backupColor;
            }
        }

        private static void PrintToConsole(object sender, MqttNetLogMessagePublishedEventArgs e)
        {
            var output = new StringBuilder();
            output.AppendLine($">> [{e.TraceMessage.Timestamp:O}] [{e.TraceMessage.ThreadId}] [{e.TraceMessage.Source}] [{e.TraceMessage.Level}]: {e.TraceMessage.Message}");
            if (e.TraceMessage.Exception != null)
            {
                output.AppendLine(e.TraceMessage.Exception.ToString());
            }

            var color = ConsoleColor.Red;
            switch (e.TraceMessage.Level)
            {
                case MqttNetLogLevel.Error:
                    color = ConsoleColor.Red;
                    break;
                case MqttNetLogLevel.Warning:
                    color = ConsoleColor.Yellow;
                    break;
                case MqttNetLogLevel.Info:
                    color = ConsoleColor.Green;
                    break;
                case MqttNetLogLevel.Verbose:
                    color = ConsoleColor.Gray;
                    break;
            }

            PrintToConsole(output.ToString(), color);
        }
    }
}
