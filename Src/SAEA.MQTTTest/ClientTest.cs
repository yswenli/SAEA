/****************************************************************************
*项目名称：SAEA.MQTTTest
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTTTest
*类 名 称：ClientTest
*版 本 号： v5.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 16:13:54
*描述：
*=====================================================================
*修改时间：2019/1/15 16:13:54
*修 改 人： yswenli
*版 本 号： v5.0.0.1
*描    述：
*****************************************************************************/
using SAEA.MQTT;
using SAEA.MQTT.Client;
using SAEA.MQTT.Client.Connecting;
using SAEA.MQTT.Client.Disconnecting;
using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Client.Receiving;
using SAEA.MQTT.Protocol;
using System;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.MQTTTest
{
    public static class ClientTest
    {
        public static async Task RunAsync()
        {
            try
            {
                MqttNetConsoleLogger.ForwardToConsole();

                var factory = new MqttFactory();
                var client = factory.CreateMqttClient();
                var clientOptions = new MqttClientOptions
                {
                    ChannelOptions = new MqttClientTcpOptions
                    {
                        Server = "127.0.0.1"
                    }
                };

                client.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate(e =>
                {
                    Console.WriteLine("### RECEIVED APPLICATION MESSAGE ###");
                    Console.WriteLine($"+ Topic = {e.ApplicationMessage.Topic}");
                    Console.WriteLine($"+ Payload = {Encoding.UTF8.GetString(e.ApplicationMessage.Payload)}");
                    Console.WriteLine($"+ QoS = {e.ApplicationMessage.QualityOfServiceLevel}");
                    Console.WriteLine($"+ Retain = {e.ApplicationMessage.Retain}");
                    Console.WriteLine();
                });

                client.ConnectedHandler = new MqttClientConnectedHandlerDelegate(async e =>
                {
                    Console.WriteLine("### CONNECTED WITH SERVER ###");

                    await client.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic("#").Build());

                    Console.WriteLine("### SUBSCRIBED ###");
                });

                client.DisconnectedHandler = new MqttClientDisconnectedHandlerDelegate(async e =>
                {
                    Console.WriteLine("### DISCONNECTED FROM SERVER ###");
                    await Task.Delay(TimeSpan.FromSeconds(5));

                    try
                    {
                        await client.ConnectAsync(clientOptions);
                    }
                    catch
                    {
                        Console.WriteLine("### RECONNECTING FAILED ###");
                    }
                });

                try
                {
                    await client.ConnectAsync(clientOptions);
                }
                catch (Exception exception)
                {
                    Console.WriteLine("### CONNECTING FAILED ###" + Environment.NewLine + exception);
                }

                Console.WriteLine("### WAITING FOR APPLICATION MESSAGES ###");

                while (true)
                {
                    Console.ReadLine();

                    await client.SubscribeAsync(new MqttTopicFilter { Topic = "test", QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce });

                    var applicationMessage = new MqttApplicationMessageBuilder()
                        .WithTopic("A/B/C")
                        .WithPayload("Hello World")
                        .WithAtLeastOnceQoS()
                        .Build();

                    await client.PublishAsync(applicationMessage);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }
    }
}
