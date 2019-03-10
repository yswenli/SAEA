/****************************************************************************
*项目名称：SAEA.MQTTTest
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTTTest
*类 名 称：PerformanceTest
*版 本 号： v4.2.1.6
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/16 10:16:24
*描述：
*=====================================================================
*修改时间：2019/1/16 10:16:24
*修 改 人： yswenli
*版 本 号： v4.2.1.6
*描    述：
*****************************************************************************/
using SAEA.MQTT;
using SAEA.MQTT.Common;
using SAEA.MQTT.Core.Protocol;
using SAEA.MQTT.Interface;
using SAEA.MQTT.Model;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTTTest
{
    public static class PerformanceTest
    {
        public static void RunClientOnly()
        {
            try
            {
                var options = new MqttClientOptions
                {
                    ChannelOptions = new MqttClientTcpOptions
                    {
                        Server = "127.0.0.1"
                    },
                    CleanSession = true
                };

                var client = new MqttFactory().CreateMqttClient();
                client.ConnectAsync(options).GetAwaiter().GetResult();

                var message = CreateMessage();
                var stopwatch = new Stopwatch();

                for (var i = 0; i < 10; i++)
                {
                    var sentMessagesCount = 0;

                    stopwatch.Restart();
                    while (stopwatch.ElapsedMilliseconds < 1000)
                    {
                        client.PublishAsync(message).GetAwaiter().GetResult();
                        sentMessagesCount++;
                    }

                    Console.WriteLine($"Sending {sentMessagesCount} messages per second. #" + (i + 1));

                    GC.Collect();
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }

        public static void RunClientAndServer()
        {
            try
            {
                var mqttServer = new MqttFactory().CreateMqttServer();
                mqttServer.StartAsync(new MqttServerOptions()).GetAwaiter().GetResult();

                var options = new MqttClientOptions
                {
                    ChannelOptions = new MqttClientTcpOptions
                    {
                        Server = "127.0.0.1"
                    },
                    CleanSession = true
                };

                var client = new MqttFactory().CreateMqttClient();
                client.ConnectAsync(options).GetAwaiter().GetResult();

                var message = CreateMessage();
                var stopwatch = new Stopwatch();

                for (var i = 0; i < 10; i++)
                {
                    stopwatch.Restart();

                    var sentMessagesCount = 0;
                    while (stopwatch.ElapsedMilliseconds < 1000)
                    {
                        client.PublishAsync(message).GetAwaiter().GetResult();
                        sentMessagesCount++;
                    }

                    Console.WriteLine($"Sending {sentMessagesCount} messages per second. #" + (i + 1));
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }

        private static Task RunClientsAsync(int msgChunkSize, TimeSpan interval, bool concurrent)
        {
            return Task.WhenAll(Enumerable.Range(0, 3).Select(i => Task.Run(() => RunClientAsync(msgChunkSize, interval, concurrent))));
        }

        private static async Task RunClientAsync(int msgChunkSize, TimeSpan interval, bool concurrent)
        {
            try
            {
                var options = new MqttClientOptions
                {
                    ChannelOptions = new MqttClientTcpOptions { Server = "localhost" },
                    ClientId = "Client1",
                    CleanSession = true,
                    CommunicationTimeout = TimeSpan.FromMinutes(10)
                };

                var client = new MqttFactory().CreateMqttClient();

                try
                {
                    await client.ConnectAsync(options).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    Console.WriteLine("### CONNECTING FAILED ###" + Environment.NewLine + exception);
                }

                var message = CreateMessage();
                var stopwatch = Stopwatch.StartNew();

                var testMessageCount = 10000;
                for (var i = 0; i < testMessageCount; i++)
                {
                    await client.PublishAsync(message);
                }

                stopwatch.Stop();
                Console.WriteLine($"Sent 10.000 messages within {stopwatch.ElapsedMilliseconds} ms ({stopwatch.ElapsedMilliseconds / (float)testMessageCount} ms / message).");

                var last = DateTime.Now;
                var msgCount = 0;

                while (true)
                {
                    var msgs = Enumerable.Range(0, msgChunkSize)
                        .Select(i => CreateMessage())
                        .ToList();

                    if (concurrent)
                    {
                        //send concurrent (test for raceconditions)
                        var sendTasks = msgs
                            .Select(msg => PublishSingleMessage(client, msg, ref msgCount))
                            .ToList();

                        await Task.WhenAll(sendTasks);
                    }
                    else
                    {
                        await client.PublishAsync(msgs);
                        msgCount += msgs.Count;
                        //send multiple
                    }

                    var now = DateTime.Now;
                    if (last < now - TimeSpan.FromSeconds(1))
                    {
                        Console.WriteLine($"sending {msgCount} intended {msgChunkSize / interval.TotalSeconds}");
                        msgCount = 0;
                        last = now;
                    }

                    await Task.Delay(interval).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }

        private static MqttMessage CreateMessage()
        {
            return new MqttMessage
            {
                Topic = "A/B/C",
                Payload = Encoding.UTF8.GetBytes("Hello World"),
                QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce
            };
        }

        private static Task PublishSingleMessage(IMqttClient client, MqttMessage applicationMessage, ref int count)
        {
            Interlocked.Increment(ref count);
            return Task.Run(() => client.PublishAsync(applicationMessage));
        }
    }
}
