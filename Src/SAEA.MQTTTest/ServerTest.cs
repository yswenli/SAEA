/****************************************************************************
*项目名称：SAEA.MQTTTest
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTTTest
*类 名 称：ServerTest
*版本号： v7.0.0.1
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/1/15 16:22:11
*描述：
*=====================================================================
*修改时间：2019/1/15 16:22:11
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Newtonsoft.Json;
using SAEA.MQTT;
using SAEA.MQTT.Client.Receiving;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.MQTTTest
{
    /// <summary>
    /// MQTT 服务端演示，含连接校验、消息与订阅拦截以及保留消息持久化
    /// </summary>
    public static class ServerTest
    {
        /// <summary>
        /// 以默认选项启动服务端，按回车结束
        /// </summary>
        public static void RunEmptyServer()
        {
            var mqttServer = new MqttFactory().CreateMqttServer();
            mqttServer.StartAsync(new MqttServerOptions()).GetAwaiter().GetResult();

            Console.WriteLine("Press any key to exit.");
            Console.ReadLine();
        }

        /// <summary>
        /// 启动带校验与拦截规则的服务端，按回车后停止
        /// </summary>
        /// <returns>表示流程执行完成的任务</returns>
        public static async Task RunAsync()
        {
            try
            {
                var options = new MqttServerOptions
                {
                    ConnectionValidator = new MqttServerConnectionValidatorDelegate(p =>
                    {
                        if (p.ClientId == "SpecialClient")
                        {
                            if (p.Username != "USER" || p.Password != "PASS")
                            {
                                p.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
                            }
                        }
                    }),

                    Storage = new RetainedMessageHandler(),

                    ApplicationMessageInterceptor = new MqttServerApplicationMessageInterceptorDelegate(context =>
                    {
                        if (MqttTopicFilterComparer.IsMatch(context.ApplicationMessage.Topic, "/myTopic/WithTimestamp/#"))
                        {
                            // Replace the payload with the timestamp. But also extending a JSON 
                            // based payload with the timestamp is a suitable use case.
                            context.ApplicationMessage.Payload = Encoding.UTF8.GetBytes(DateTimeHelper.Now.ToString("O"));
                        }

                        if (context.ApplicationMessage.Topic == "not_allowed_topic")
                        {
                            context.AcceptPublish = false;
                            context.CloseConnection = true;
                        }
                    }),

                    SubscriptionInterceptor = new MqttServerSubscriptionInterceptorDelegate(context =>
                    {
                        if (context.TopicFilter.Topic.StartsWith("admin/foo/bar") && context.ClientId != "theAdmin")
                        {
                            context.AcceptSubscription = false;
                        }

                        if (context.TopicFilter.Topic.StartsWith("the/secret/stuff") && context.ClientId != "Imperator")
                        {
                            context.AcceptSubscription = false;
                            context.CloseConnection = true;
                        }
                    })
                };

                // Extend the timestamp for all messages from clients.
                // Protect several topics from being subscribed from every client.

                //var certificate = new X509Certificate(@"C:\certs\test\test.cer", "");
                //options.TlsEndpointOptions.Certificate = certificate.Export(X509ContentType.Cert);
                //options.ConnectionBacklog = 5;
                //options.DefaultEndpointOptions.IsEnabled = true;
                //options.TlsEndpointOptions.IsEnabled = false;

                var mqttServer = new MqttFactory().CreateMqttServer();

                mqttServer.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate(e =>
                {
                    MqttNetConsoleLogger.PrintToConsole(
                        $"'{e.ClientId}' reported '{e.ApplicationMessage.Topic}' > '{Encoding.UTF8.GetString(e.ApplicationMessage.Payload ?? new byte[0])}'",
                        ConsoleColor.Magenta);
                });

                //options.ApplicationMessageInterceptor = c =>
                //{
                //    if (c.ApplicationMessage.Payload == null || c.ApplicationMessage.Payload.Length == 0)
                //    {
                //        return;
                //    }

                //    try
                //    {
                //        var content = JObject.Parse(Encoding.UTF8.GetString(c.ApplicationMessage.Payload));
                //        var timestampProperty = content.Property("timestamp");
                //        if (timestampProperty != null && timestampProperty.Value.Type == JTokenType.Null)
                //        {
                //            timestampProperty.Value = DateTimeHelper.Now.ToString("O");
                //            c.ApplicationMessage.Payload = Encoding.UTF8.GetBytes(content.ToString());
                //        }
                //    }
                //    catch (Exception)
                //    {
                //    }
                //};

                mqttServer.ClientConnectedHandler = new MqttServerClientConnectedHandlerDelegate(e =>
                {
                    Console.Write("Client disconnected event fired.");
                });

                await mqttServer.StartAsync(options);

                Console.WriteLine("Press any key to exit.");
                Console.ReadLine();

                await mqttServer.StopAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            Console.ReadLine();
        }
    }

    /// <summary>
    /// 服务端存储实现，把保留消息以 JSON 文件持久化到固定目录
    /// </summary>
    public class RetainedMessageHandler : IMqttServerStorage
    {
        private const string Filename = "C:\\MQTT\\RetainedMessages.json";

        /// <summary>
        /// 把保留消息序列化为 JSON 写入固定文件，目录不存在时先创建
        /// </summary>
        /// <param name="messages">待保存的保留消息集合</param>
        /// <returns>已完成的空任务</returns>
        public Task SaveRetainedMessagesAsync(IList<MqttApplicationMessage> messages)
        {
            var directory = Path.GetDirectoryName(Filename);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(Filename, JsonConvert.SerializeObject(messages));
            return Task.FromResult(0);
        }

        /// <summary>
        /// 从固定文件读取保留消息
        /// </summary>
        /// <returns>反序列化得到的保留消息集合，文件不存在时为空集合</returns>
        public Task<IList<MqttApplicationMessage>> LoadRetainedMessagesAsync()
        {
            IList<MqttApplicationMessage> retainedMessages;
            if (File.Exists(Filename))
            {
                var json = File.ReadAllText(Filename);
                retainedMessages = JsonConvert.DeserializeObject<List<MqttApplicationMessage>>(json);
            }
            else
            {
                retainedMessages = new List<MqttApplicationMessage>();
            }

            return Task.FromResult(retainedMessages);
        }
    }
}
