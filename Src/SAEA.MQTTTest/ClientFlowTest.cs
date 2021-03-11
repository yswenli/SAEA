/****************************************************************************
*项目名称：SAEA.MQTTTest
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.MQTTTest
*类 名 称：ClientFlowTest
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/3/10 19:36:53
*描述：
*=====================================================================
*修改时间：2021/3/10 19:36:53
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.MQTT;
using SAEA.MQTT.Client;
using SAEA.MQTT.Client.Options;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.MQTTTest
{
    public static class ClientFlowTest
    {
        public static async Task RunAsync()
        {
            MqttNetConsoleLogger.ForwardToConsole();
            try
            {
                var factory = new MqttFactory();
                var client = factory.CreateMqttClient();

                var options = new MqttClientOptionsBuilder()
                    .WithTcpServer("localhost")
                    .Build();

                Console.WriteLine("BEFORE CONNECT");
                await client.ConnectAsync(options);
                Console.WriteLine("AFTER CONNECT");

                Console.WriteLine("BEFORE SUBSCRIBE");
                await client.SubscribeAsync("test/topic");
                Console.WriteLine("AFTER SUBSCRIBE");

                Console.WriteLine("BEFORE PUBLISH");
                await client.PublishAsync("test/topic", "payload");
                Console.WriteLine("AFTER PUBLISH");

                await Task.Delay(1000);

                Console.WriteLine("BEFORE DISCONNECT");
                await client.DisconnectAsync();
                Console.WriteLine("AFTER DISCONNECT");

                Console.WriteLine("FINISHED");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
