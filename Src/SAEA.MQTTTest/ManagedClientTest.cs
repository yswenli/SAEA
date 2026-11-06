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
    /// <summary>
    /// 托管 MQTT 客户端演示，覆盖自动重连、待发消息持久化与业务消息接收
    /// </summary>
    public static class ManagedClientTest
    {
        /// <summary>
        /// 使用托管客户端连接公共 broker，按不同 QoS 发布并订阅主题后等待回车
        /// </summary>
        /// <returns>表示流程执行完成的任务</returns>
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


        /// <summary>
        /// 客户端凭据实现，用户名固定而密码每次访问都重新生成
        /// </summary>
        public class RandomPassword : IMqttClientCredentials
        {
            /// <summary>
            /// 密码，取新 Guid 的字节表示，每次访问都会变化
            /// </summary>
            public byte[] Password => Guid.NewGuid().ToByteArray();

            /// <summary>
            /// 固定的用户名 the_static_user
            /// </summary>
            public string Username => "the_static_user";
        }

        /// <summary>
        /// 托管客户端存储实现，把待发消息队列以 JSON 文件保存在工作目录
        /// </summary>
        public class ClientRetainedMessageHandler : IManagedMqttClientStorage
        {
            private const string Filename = @"RetainedMessages.json";

            /// <summary>
            /// 把待发消息队列序列化为 JSON 写入固定文件
            /// </summary>
            /// <param name="messages">待持久化的消息队列</param>
            /// <returns>已完成的空任务</returns>
            public Task SaveQueuedMessagesAsync(IList<ManagedMqttApplicationMessage> messages)
            {
                File.WriteAllText(Filename, JsonConvert.SerializeObject(messages));
                return Task.FromResult(0);
            }

            /// <summary>
            /// 从固定文件读取待发消息队列
            /// </summary>
            /// <returns>反序列化得到的消息队列，文件不存在时为空列表</returns>
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
