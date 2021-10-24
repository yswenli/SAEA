/****************************************************************************
*项目名称：SAEA.MQTTTest
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.MQTTTest
*类 名 称：ManagedClientTest
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/3/10 18:29:19
*描述：
*=====================================================================
*修改时间：2021/3/10 18:29:19
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using SAEA.Common.Newtonsoft.Json;
using SAEA.MQTT;
using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Client.Receiving;
using SAEA.MQTT.Extensions.ManagedClient;
using SAEA.MQTT.Protocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.MQTTTest
{
    public static class ManagedClientTest
    {
        public static async Task RunAsync()
        {
            var ms = new ClientRetainedMessageHandler();

            var options = new ManagedMqttClientOptions
            {
                ClientOptions = new MqttClientOptions
                {
                    ClientId = "MQTTnetManagedClientTest",
                    Credentials = new RandomPassword(),
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
                managedClient.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate(e =>
                {
                    Console.WriteLine(">> RECEIVED: " + e.ApplicationMessage.Topic);
                });

                await managedClient.StartAsync(options);

                await managedClient.PublishAsync(builder => builder.WithTopic("Step").WithPayload("1"));
                await managedClient.PublishAsync(builder => builder.WithTopic("Step").WithPayload("2").WithAtLeastOnceQoS());

                await managedClient.SubscribeAsync(new MqttTopicFilter { Topic = "xyz", QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce });
                await managedClient.SubscribeAsync(new MqttTopicFilter { Topic = "abc", QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce });

                await managedClient.PublishAsync(builder => builder.WithTopic("Step").WithPayload("3"));

                Console.WriteLine("Managed client started.");
                Console.ReadLine();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }


        public class RandomPassword : IMqttClientCredentials
        {
            public byte[] Password => Guid.NewGuid().ToByteArray();

            public string Username => "the_static_user";
        }

        public class ClientRetainedMessageHandler : IManagedMqttClientStorage
        {
            private const string Filename = @"RetainedMessages.json";

            public Task SaveQueuedMessagesAsync(IList<ManagedMqttApplicationMessage> messages)
            {
                File.WriteAllText(Filename, JsonConvert.SerializeObject(messages));
                return Task.FromResult(0);
            }

            public Task<IList<ManagedMqttApplicationMessage>> LoadQueuedMessagesAsync()
            {
                IList<ManagedMqttApplicationMessage> retainedMessages;
                if (File.Exists(Filename))
                {
                    var json = File.ReadAllText(Filename);
                    retainedMessages = JsonConvert.DeserializeObject<List<ManagedMqttApplicationMessage>>(json);
                }
                else
                {
                    retainedMessages = new List<ManagedMqttApplicationMessage>();
                }

                return Task.FromResult(retainedMessages);
            }
        }
    }
}
