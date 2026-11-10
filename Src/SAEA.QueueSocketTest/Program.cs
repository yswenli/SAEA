
using SAEA.Common;
using SAEA.Common.Threading;
using SAEA.QueueSocket;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.QueueSocketTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            ConsoleHelper.Title = $"SAEA.QueueSocketTest -- {DateTimeHelper.Now}";

            if (args != null && args.Length > 0)
            {
                switch (args[0].ToLowerInvariant())
                {
                    case "--all":
                        TestHarness.Reset();
                        QueueServerHarness.Start();
                        try
                        {
                            await FunctionalTests.RunAllAsync();
                            await QueueBenchmark.RunAsync();
                        }
                        finally
                        {
                            QueueServerHarness.Stop();
                        }
                        TestHarness.WriteSummary("SAEA.QueueSocketTest --all");
                        Environment.ExitCode = TestHarness.HasFailures ? 1 : 0;
                        await Task.Delay(500);
                        return;
                    case "--functional":
                        TestHarness.Reset();
                        QueueServerHarness.Start();
                        try
                        {
                            await FunctionalTests.RunAllAsync();
                        }
                        finally
                        {
                            QueueServerHarness.Stop();
                        }
                        TestHarness.WriteSummary("SAEA.QueueSocketTest --functional");
                        Environment.ExitCode = TestHarness.HasFailures ? 1 : 0;
                        await Task.Delay(500);
                        return;
                    case "--bench-queue-baseline":
                        TestHarness.Reset();
                        QueueServerHarness.Start();
                        try
                        {
                            await QueueBenchmark.RunAsync(false);
                        }
                        finally
                        {
                            QueueServerHarness.Stop();
                        }
                        TestHarness.WriteSummary("SAEA.QueueSocketTest --bench-queue-baseline");
                        Environment.ExitCode = TestHarness.HasFailures ? 1 : 0;
                        await Task.Delay(500);
                        return;
                    case "--bench-queue":
                    case "--bench":
                        TestHarness.Reset();
                        QueueServerHarness.Start();
                        try
                        {
                            await QueueBenchmark.RunAsync();
                        }
                        finally
                        {
                            QueueServerHarness.Stop();
                        }
                        TestHarness.WriteSummary("SAEA.QueueSocketTest --bench-queue");
                        Environment.ExitCode = TestHarness.HasFailures ? 1 : 0;
                        await Task.Delay(500);
                        return;
                }
            }

            var inputStr = "";

            if (args != null && args.Length > 0 && args[0].IsNotNullOrEmpty())
            {
                inputStr = args[0];
            }

            var topic = "hello";

            while (true)
            {
                ConsoleHelper.WriteLine("SAEA.QueueSocketTest\r\n \t输入s启动队列服务器\r\n\t输入p启动生产者\r\n\t输入c启动消费者\r\n\t输入t启动高并发测试");

                if (inputStr.IsNullOrEmpty())
                {
                    inputStr = ConsoleHelper.ReadLine();
                }

                if (!string.IsNullOrEmpty(inputStr))
                {
                    var ipPort = "";

                    switch (inputStr.ToLower())
                    {
                        case "s":
                            ServerInit();
                            inputStr = "";
                            break;
                        case "p":
                            ConsoleHelper.WriteLine("输入ip:port连接到队列服务器");
                            ipPort = ConsoleHelper.ReadLine();
                            ProducerInit(ipPort, topic);
                            return;
                        case "c":
                            ConsoleHelper.WriteLine("输入ip:port连接到队列服务器");
                            ipPort = ConsoleHelper.ReadLine();
                            KeepConsumerAlive(ConsumerInit(ipPort, topic));
                            return;
                        case "sc":
                            ServerInit();
                            Thread.Sleep(1000);
                            KeepConsumerAlive(ConsumerInit("127.0.0.1:39654", topic));
                            return;
                        case "sp":
                            ServerInit();
                            Thread.Sleep(1000);
                            ProducerInit("127.0.0.1:39654", topic);
                            return;
                        case "a":
                            ServerInit();
                            Thread.Sleep(1000);
                            ConsumerInit("127.0.0.1:39654", topic);
                            Thread.Sleep(1000);
                            ProducerInit("127.0.0.1:39654", topic);
                            return;
                        case "t":
                            HighConcurrencyTest.Run();
                            return;
                        default:
                            break;
                    }
                }


                inputStr = "";
            }
        }



        static QServer _server;
        static void ServerInit()
        {
            _server = new QServer();
            // 服务端不再暴露断开事件，断开清理与统计改由处理器链的 ChannelInactive 通知承担。
            _server.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new DisconnectCounter(ServerOnDisconnected));
            _server.CalcInfo((ci, qi) =>
            {
                var result = string.Format("生产者：{0} 消费者：{1} 收到消息:{2} 推送消息:{3}{4}", ci.Item1, ci.Item2, ci.Item3, ci.Item4, Environment.NewLine);

                qi.ForEach((item) =>
                {
                    result += string.Format("队列名称：{0} 堆积消息数：{1} {2}", item.Item1, item.Item2, Environment.NewLine);
                });
                ConsoleHelper.WriteLine(result);
            });
            _server.Start();
        }

        private static void ServerOnDisconnected(string ID)
        {
            _server.Clear(ID);
            ConsoleHelper.WriteLine("{0} 已从服务器断开", ID);
        }

        static void ProducerInit(string ipPort, string topic)
        {
            string msg = "123";
            if (string.IsNullOrEmpty(ipPort)) ipPort = "127.0.0.1:39654";

            Producer producer = new Producer("productor" + Guid.NewGuid().ToString("N"), ipPort);

            // 发送完成与异常不再通过事件上报，这里统计已提交的发布条数用于展示发送速率。
            long submitted = 0;

            TaskHelper.LongRunning(() =>
            {
                var old = 0L;
                while (producer.Connected)
                {
                    Thread.Sleep(1000);
                    var current = Interlocked.Read(ref submitted);
                    ConsoleHelper.WriteLine("生产者已提交发送：{0} 速度：{1}/s", current, current - old);
                    old = current;
                }
            });

            // 发送消息的主任务，不限制速度
            while (producer.Connected)
            {
                producer.Publish(topic, msg);
                Interlocked.Increment(ref submitted);
            }

        }

        static Consumer ConsumerInit(string ipPort, string topic)
        {
            if (string.IsNullOrEmpty(ipPort)) ipPort = "127.0.0.1:39654";
            Consumer consumer = new Consumer("subscriber-" + Guid.NewGuid().ToString("N"), ipPort);

            // 接收改由处理器链装配的入站处理器承担，回调内计数并归还池化缓冲。
            consumer.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new QueueMessageSink(obj =>
            {
                Interlocked.Increment(ref _outNum);
                obj.Dispose();
            }));

            consumer.Subscribe(topic);
            consumer.Start();

            TaskHelper.LongRunning(() =>
            {
                var old = 0L;
                while (consumer.Connected)
                {
                    var current = Interlocked.Read(ref _outNum);
                    ConsoleHelper.WriteLine("消费者已成功接收：{0} 速度：{1}/s", current, current - old);
                    old = current;
                    Thread.Sleep(1000);
                }
            });

            return consumer;
        }

        static void KeepConsumerAlive(Consumer consumer)
        {
            while (consumer != null && consumer.Connected)
            {
                Thread.Sleep(1000);
            }
        }

        static long _outNum = 0;
    }
}
