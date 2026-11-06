/****************************************************************************
*项目名称：SAEA.MQTTTest
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTTTest
*类 名 称：PublicBrokerTest
*版本号： v7.0.0.1
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/1/15 16:39:45
*描述：
*=====================================================================
*修改时间：2019/1/15 16:39:45
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using SAEA.Common.Newtonsoft.Json;
using SAEA.MQTT;
using SAEA.MQTT.Client;
using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Client.Receiving;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Protocol;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTTTest
{
    /// <summary>
    /// 公共 MQTT broker 连通性演示，逐个 broker 做订阅、发布与回环校验
    /// </summary>
    public static class PublicBrokerTest
    {
        /// <summary>
        /// 依次测试多个公共 broker 的 TCP、WebSocket 与 TLS 组合，存在本地配置时再测 CloudMQTT
        /// </summary>
        /// <returns>表示流程执行完成的任务</returns>
        public static async Task RunAsync()
        {
            //MqttNetConsoleLogger.ForwardToConsole();

            // iot.eclipse.org
            await ExecuteTestAsync("iot.eclipse.org TCP",
                new MqttClientOptionsBuilder().WithTcpServer("iot.eclipse.org", 1883).WithProtocolVersion(MqttProtocolVersion.V311).Build());

            await ExecuteTestAsync("iot.eclipse.org WS",
                new MqttClientOptionsBuilder().WithWebSocketServer("iot.eclipse.org:80/mqtt").WithProtocolVersion(MqttProtocolVersion.V311).Build());

            await ExecuteTestAsync("iot.eclipse.org WS TLS",
                new MqttClientOptionsBuilder().WithWebSocketServer("iot.eclipse.org:443/mqtt").WithProtocolVersion(MqttProtocolVersion.V311).WithTls().Build());

            // test.mosquitto.org
            await ExecuteTestAsync("test.mosquitto.org TCP",
                new MqttClientOptionsBuilder().WithTcpServer("test.mosquitto.org", 1883).WithProtocolVersion(MqttProtocolVersion.V311).Build());

            await ExecuteTestAsync("test.mosquitto.org TCP TLS",
                new MqttClientOptionsBuilder().WithTcpServer("test.mosquitto.org", 8883).WithProtocolVersion(MqttProtocolVersion.V311).WithTls().Build());

            await ExecuteTestAsync("test.mosquitto.org WS",
                new MqttClientOptionsBuilder().WithWebSocketServer("test.mosquitto.org:8080/mqtt").WithProtocolVersion(MqttProtocolVersion.V311).Build());

            await ExecuteTestAsync("test.mosquitto.org WS TLS",
                new MqttClientOptionsBuilder().WithWebSocketServer("test.mosquitto.org:8081/mqtt").WithProtocolVersion(MqttProtocolVersion.V311).WithTls().Build());

            // broker.hivemq.com
            await ExecuteTestAsync("broker.hivemq.com TCP",
                new MqttClientOptionsBuilder().WithTcpServer("broker.hivemq.com", 1883).WithProtocolVersion(MqttProtocolVersion.V311).Build());

            await ExecuteTestAsync("broker.hivemq.com WS",
                new MqttClientOptionsBuilder().WithWebSocketServer("broker.hivemq.com:8000/mqtt").WithProtocolVersion(MqttProtocolVersion.V311).Build());

            // mqtt.swifitch.cz
            await ExecuteTestAsync("mqtt.swifitch.cz",
                new MqttClientOptionsBuilder().WithTcpServer("mqtt.swifitch.cz", 1883).WithProtocolVersion(MqttProtocolVersion.V311).Build());

            // broker.emqx.io (MQTT 5.0)
            await ExecuteTestAsync("broker.emqx.io TCP V311",
                new MqttClientOptionsBuilder().WithTcpServer("broker.emqx.io", 1883).WithProtocolVersion(MqttProtocolVersion.V311).Build());

            await ExecuteTestAsync("broker.emqx.io TCP V500",
                new MqttClientOptionsBuilder().WithTcpServer("broker.emqx.io", 1883).WithProtocolVersion(MqttProtocolVersion.V500).Build());

            await ExecuteTestAsync("broker.emqx.io WS V500",
                new MqttClientOptionsBuilder().WithWebSocketServer("broker.emqx.io:8083/mqtt").WithProtocolVersion(MqttProtocolVersion.V500).Build());

            // CloudMQTT
            var configFile = Path.Combine("E:\\CloudMqttTestConfig.json");
            if (File.Exists(configFile))
            {
                var config = JsonConvert.DeserializeObject<MqttConfig>(File.ReadAllText(configFile));

                await ExecuteTestAsync("CloudMQTT TCP",
                    new MqttClientOptionsBuilder().WithTcpServer(config.Server, config.Port).WithCredentials(config.Username, config.Password).WithProtocolVersion(MqttProtocolVersion.V311).Build());

                await ExecuteTestAsync("CloudMQTT TCP TLS",
                    new MqttClientOptionsBuilder().WithTcpServer(config.Server, config.SslPort).WithCredentials(config.Username, config.Password).WithTls().WithProtocolVersion(MqttProtocolVersion.V311).Build());

                await ExecuteTestAsync("CloudMQTT WS TLS",
                    new MqttClientOptionsBuilder().WithWebSocketServer(config.Server + ":" + config.SslWebSocketPort + "/mqtt").WithCredentials(config.Username, config.Password).WithTls().WithProtocolVersion(MqttProtocolVersion.V311).Build());
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
                //factory.UseWebSocket4Net();
                var client = factory.CreateMqttClient();
                var topic = Guid.NewGuid().ToString();

                MqttApplicationMessage receivedMessage = null;
                client.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate(e => receivedMessage = e.ApplicationMessage);

                await client.ConnectAsync(options);
                await client.SubscribeAsync(topic, MqttQualityOfServiceLevel.AtLeastOnce);
                await client.PublishAsync(topic, "Hello_World", MqttQualityOfServiceLevel.AtLeastOnce);

                SpinWait.SpinUntil(() => receivedMessage != null, 5000);

                if (receivedMessage?.Topic != topic || receivedMessage?.ConvertPayloadToString() != "Hello_World")
                {
                    throw new Exception("Message invalid.");
                }

                await client.UnsubscribeAsync(topic);
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

        /// <summary>
        /// CloudMQTT 连接配置，从本地 JSON 文件反序列化得到
        /// </summary>
        public class MqttConfig
        {
            /// <summary>
            /// 服务器地址
            /// </summary>
            public string Server { get; set; }

            /// <summary>
            /// 登录用户名
            /// </summary>
            public string Username { get; set; }

            /// <summary>
            /// 登录密码
            /// </summary>
            public string Password { get; set; }

            /// <summary>
            /// 明文 TCP 端口
            /// </summary>
            public int Port { get; set; }

            /// <summary>
            /// TLS 端口
            /// </summary>
            public int SslPort { get; set; }

            /// <summary>
            /// WebSocket 明文端口，当前演示未使用
            /// </summary>
            public int WebSocketPort { get; set; }

            /// <summary>
            /// WebSocket over TLS 端口
            /// </summary>
            public int SslWebSocketPort { get; set; }
        }
    }
}
