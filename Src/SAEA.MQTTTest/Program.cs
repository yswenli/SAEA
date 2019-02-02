/****************************************************************************
*项目名称：SAEA.MQTTTest
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTTTest
*类 名 称：Program
*版 本 号： V4.1.2.2
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/14 14:11:15
*描述：
*=====================================================================
*修改时间：2019/1/14 14:11:15
*修 改 人： yswenli
*版 本 号： V4.1.2.2
*描    述：
*****************************************************************************/

using SAEA.MQTT;
using SAEA.MQTT.Common;
using SAEA.MQTT.Common.Log;
using SAEA.MQTT.Core.Implementations;
using SAEA.MQTT.Core.Protocol;
using SAEA.MQTT.Model;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTTTest
{
    class Program
    {
        static void Main(string[] args)
        {                    

            while (true)
            {
                Console.Title = "SAEA.MQTT Test";

                Console.WriteLine("1 = Start client");
                Console.WriteLine("2 = Start server");
                Console.WriteLine("3 = Start performance test");
                Console.WriteLine("4 = Start managed client");
                Console.WriteLine("5 = Start public broker test");
                Console.WriteLine("6 = Start server & client");
                Console.WriteLine("7 = Client flow test");
                Console.WriteLine("8 = Start performance test (client only)");
                Console.WriteLine("9 = Start server (no trace)");

                var pressedKey = Console.ReadLine();

                switch (pressedKey)
                {
                    case "1":
                        Task.Run(ClientTest.RunAsync);
                        break;
                    case "2":
                        Task.Run(ServerTest.RunAsync);
                        break;
                    case "3":
                        PerformanceTest.RunClientAndServer();
                        break;
                    case "4":
                        Task.Run(ManagedClientTest);
                        break;
                    case "5":
                        Task.Run(PublicBrokerTest.RunAsync);
                        break;
                    case "6":
                        Task.Run(ServerAndClientTestRunAsync);
                        break;
                    case "7":
                        Task.Run(ClientFlowTest);
                        break;
                    case "8":
                        PerformanceTest.RunClientOnly();
                        break;
                    case "9":
                        ServerTest.RunEmptyServer();
                        break;
                }
            }
        }






        /// <summary>
        /// 4
        /// </summary>
        /// <returns></returns>
        static async Task ManagedClientTest()
        {
            var ms = new ClientRetainedMessageHandler();

            var options = new MqttManagedClientOptions
            {
                ClientOptions = new MqttClientOptions
                {
                    ClientId = "MQTTnetManagedClientTest",
                    Credentials = new MqttClientCredentials()
                    {
                        Username = "the_static_user",
                        Password = Guid.NewGuid().ToString()
                    },
                    ChannelOptions = new MqttClientTcpOptions
                    {
                        Server = "broker.hivemq.com"
                    }
                },

                AutoReconnectDelay = TimeSpan.FromSeconds(1),
                Storage = ms
            };

            try
            {
                var managedClient = new MqttFactory().CreateManagedMqttClient();

                managedClient.ApplicationMessageReceived += (s, e) =>
                {
                    Console.WriteLine(">> RECEIVED: " + e.ApplicationMessage.Topic);
                };

                await managedClient.PublishAsync(builder => builder.WithTopic("Step").WithPayload("1"));
                await managedClient.PublishAsync(builder => builder.WithTopic("Step").WithPayload("2").WithAtLeastOnceQoS());

                await managedClient.StartAsync(options);

                await managedClient.SubscribeAsync(new TopicFilter("xyz", MqttQualityOfServiceLevel.AtMostOnce));
                await managedClient.SubscribeAsync(new TopicFilter("abc", MqttQualityOfServiceLevel.AtMostOnce));

                await managedClient.PublishAsync(builder => builder.WithTopic("Step").WithPayload("3"));

                Console.WriteLine("Managed client started.");
                Console.ReadLine();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }


        /// <summary>
        /// 6
        /// </summary>
        /// <returns></returns>
        static async Task ServerAndClientTestRunAsync()
        {
            MqttNetConsoleLogger.ForwardToConsole();

            var factory = new MqttFactory();

            var server = factory.CreateMqttServer();
            var client = factory.CreateMqttClient();


            var serverOptions = new MqttServerOptionsBuilder().Build();
            server.ApplicationMessageReceived += Server_ApplicationMessageReceived;
            await server.StartAsync(serverOptions);

            var clientOptions = new MqttClientOptionsBuilder().WithTcpServer("127.0.0.1").Build();
            client.ApplicationMessageReceived += Client_ApplicationMessageReceived;


            await client.ConnectAsync(clientOptions);

            Task.Run(() =>
            {
                while (client.IsConnected)
                {
                    client.PublishAsync("test/topic", "hello").GetAwaiter().GetResult();
                    Thread.Sleep(500);
                }
            });

            client.SubscribeAsync("test/topic");

        }

        private static void Server_ApplicationMessageReceived(object sender, MQTT.Event.MqttMessageReceivedEventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine($"Server收到消息，ClientId:{e.ClientId}，{Encoding.UTF8.GetString(e.ApplicationMessage.Payload)}");
        }

        private static void Client_ApplicationMessageReceived(object sender, MQTT.Event.MqttMessageReceivedEventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"client:{e.ClientId}收到消息:{Encoding.UTF8.GetString(e.ApplicationMessage.Payload)}");
        }

        /// <summary>
        /// 7
        /// </summary>
        /// <returns></returns>
        static async Task ClientFlowTest()
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
