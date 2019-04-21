/****************************************************************************
*项目名称：SAEA.MQTTTest
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTTTest
*类 名 称：PublicBrokerTest
*版 本 号： v4.5.1.2
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 16:39:45
*描述：
*=====================================================================
*修改时间：2019/1/15 16:39:45
*修 改 人： yswenli
*版 本 号： v4.5.1.2
*描    述：
*****************************************************************************/
using SAEA.Common;
using SAEA.MQTT;
using SAEA.MQTT.Common;
using SAEA.MQTT.Core.Protocol;
using SAEA.MQTT.Interface;
using SAEA.MQTT.Model;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTTTest
{
    public static class PublicBrokerTest
    {
        public static async Task RunAsync()
        {
            //MqttNetConsoleLogger.ForwardToConsole();

            // iot.eclipse.org
            await ExecuteTestAsync("iot.eclipse.org TCP",
                new MqttClientOptionsBuilder().WithTcpServer("iot.eclipse.org", 1883).Build());

            await ExecuteTestAsync("iot.eclipse.org WS",
                new MqttClientOptionsBuilder().WithWebSocketServer("iot.eclipse.org:80/mqtt").Build());

            await ExecuteTestAsync("iot.eclipse.org WS TLS",
                new MqttClientOptionsBuilder().WithWebSocketServer("iot.eclipse.org:443/mqtt").WithTls().Build());

            // test.mosquitto.org
            await ExecuteTestAsync("test.mosquitto.org TCP",
                new MqttClientOptionsBuilder().WithTcpServer("test.mosquitto.org", 1883).Build());

            await ExecuteTestAsync("test.mosquitto.org TCP TLS",
                new MqttClientOptionsBuilder().WithTcpServer("test.mosquitto.org", 8883).WithTls().Build());

            await ExecuteTestAsync("test.mosquitto.org WS",
                new MqttClientOptionsBuilder().WithWebSocketServer("test.mosquitto.org:8080/mqtt").Build());

            await ExecuteTestAsync("test.mosquitto.org WS TLS",
                new MqttClientOptionsBuilder().WithWebSocketServer("test.mosquitto.org:8081/mqtt").WithTls().Build());

            // broker.hivemq.com
            await ExecuteTestAsync("broker.hivemq.com TCP",
                new MqttClientOptionsBuilder().WithTcpServer("broker.hivemq.com", 1883).Build());

            await ExecuteTestAsync("broker.hivemq.com WS",
                new MqttClientOptionsBuilder().WithWebSocketServer("broker.hivemq.com:8000/mqtt").Build());

            // mqtt.swifitch.cz
            await ExecuteTestAsync("mqtt.swifitch.cz",
                new MqttClientOptionsBuilder().WithTcpServer("mqtt.swifitch.cz", 1883).Build());

            // CloudMQTT
            var configFile = Path.Combine("E:\\CloudMqttTestConfig.json");
            if (File.Exists(configFile))
            {
                var config = MqttConfig.Parse(File.ReadAllText(configFile));

                await ExecuteTestAsync("CloudMQTT TCP",
                    new MqttClientOptionsBuilder().WithTcpServer(config.Server, config.Port).WithCredentials(config.Username, config.Password).Build());

                await ExecuteTestAsync("CloudMQTT TCP TLS",
                    new MqttClientOptionsBuilder().WithTcpServer(config.Server, config.SslPort).WithCredentials(config.Username, config.Password).WithTls().Build());

                await ExecuteTestAsync("CloudMQTT WS TLS",
                    new MqttClientOptionsBuilder().WithWebSocketServer(config.Server + ":" + config.SslWebSocketPort + "/mqtt").WithCredentials(config.Username, config.Password).WithTls().Build());
            }

            Write("Finished.", ConsoleColor.White);
            Console.ReadLine();
        }

        private static async Task ExecuteTestAsync(string name, IMqttClientOptions options)
        {
            try
            {
                Write("Testing '" + name + "'... ", ConsoleColor.Gray);
                var factory = new MqttFactory();
                var client = factory.CreateMqttClient();
                var topic = Guid.NewGuid().ToString();

                MqttMessage receivedMessage = null;
                client.ApplicationMessageReceived += (s, e) => receivedMessage = e.ApplicationMessage;

                await client.ConnectAsync(options);
                await client.SubscribeAsync(topic, MqttQualityOfServiceLevel.AtLeastOnce);
                await client.PublishAsync(topic, "Hello_World", MqttQualityOfServiceLevel.AtLeastOnce);

                SpinWait.SpinUntil(() => receivedMessage != null, 5000);

                if (receivedMessage?.Topic != topic || receivedMessage?.ConvertPayloadToString() != "Hello_World")
                {
                    throw new Exception("Message invalid.");
                }

                await client.UnsubscribeAsync("test");
                await client.DisconnectAsync();

                Write("[OK]\n", ConsoleColor.Green);
            }
            catch (Exception e)
            {
                Write("[FAILED] " + e.Message + "\n", ConsoleColor.Red);
            }
        }

        private static void Write(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.Write(message);
        }

    }
}
