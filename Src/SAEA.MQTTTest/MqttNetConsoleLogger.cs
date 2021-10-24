/****************************************************************************
*项目名称：SAEA.MQTTTest
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.MQTTTest
*类 名 称：MqttNetConsoleLogger
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/3/10 18:14:26
*描述：
*=====================================================================
*修改时间：2021/3/10 18:14:26
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using SAEA.MQTT.Diagnostics;
using System;
using System.Text;

namespace SAEA.MQTTTest
{
    public static class MqttNetConsoleLogger
    {
        static readonly object _lock = new object();

        public static void ForwardToConsole()
        {
            MqttNetGlobalLogger.LogMessagePublished -= PrintToConsole;
            MqttNetGlobalLogger.LogMessagePublished += PrintToConsole;
        }

        public static void PrintToConsole(string message, ConsoleColor color)
        {
            lock (_lock)
            {
                var backupColor = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.Write(message);
                Console.ForegroundColor = backupColor;
            }
        }

        static void PrintToConsole(object sender, MqttNetLogMessagePublishedEventArgs e)
        {
            var output = new StringBuilder();
            output.AppendLine($">> [{e.LogMessage.Timestamp:O}] [{e.LogMessage.ThreadId}] [{e.LogMessage.Source}] [{e.LogMessage.Level}]: {e.LogMessage.Message}");
            if (e.LogMessage.Exception != null)
            {
                output.AppendLine(e.LogMessage.Exception.ToString());
            }

            var color = ConsoleColor.Red;
            switch (e.LogMessage.Level)
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
