using SAEA.Common;
using SAEA.Common.Threading;
using SAEA.QueueSocket;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.QueueSocketTest
{
    class HighConcurrencyTest
    {
        static long _producerSentCount = 0;
        static long _consumerReceivedCount = 0;
        static int _producerCount = 5;
        static int _consumerCount = 5;
        static QServer _server;

        public static void Run()
        {
            ConsoleHelper.Title = "SAEA.QueueSocket High Concurrency Test";
            ConsoleHelper.WriteLine("[Main] Starting high concurrency test...", ConsoleColor.White);

            ServerInit();
            Thread.Sleep(1000);

            ConsoleHelper.WriteLine($"[Main] Starting {_producerCount} producers and {_consumerCount} consumers...", ConsoleColor.White);

            for (int i = 0; i < _consumerCount; i++)
            {
                Task.Run(() => ConsumerInit("127.0.0.1:39654", "test-topic"));
            }

            Thread.Sleep(3000);

            for (int i = 0; i < _producerCount; i++)
            {
                Task.Run(() => ProducerInit("127.0.0.1:39654", "test-topic"));
            }

            StartStatistics();

            ConsoleHelper.WriteLine("[Main] Test started. Program will run for 30 seconds...", ConsoleColor.White);
            Thread.Sleep(30000);
            _server?.Stop();
        }

        static void StartStatistics()
        {
            TaskHelper.LongRunning(() =>
            {
                long oldProducerCount = 0;
                long oldConsumerCount = 0;
                var lastTime = DateTime.Now;

                while (_server.Status)
                {
                    var currentTime = DateTime.Now;
                    var elapsed = (currentTime - lastTime).TotalSeconds;

                    var currentProducerCount = Interlocked.Read(ref _producerSentCount);
                    var currentConsumerCount = Interlocked.Read(ref _consumerReceivedCount);

                    var producerSpeed = (long)((currentProducerCount - oldProducerCount) / elapsed);
                    var consumerSpeed = (long)((currentConsumerCount - oldConsumerCount) / elapsed);

                    oldProducerCount = currentProducerCount;
                    oldConsumerCount = currentConsumerCount;
                    lastTime = currentTime;

                    ConsoleHelper.WriteLine($"[Statistics] Producers sent: {currentProducerCount} ({producerSpeed}/s) | Consumers received: {currentConsumerCount} ({consumerSpeed}/s)", ConsoleColor.Magenta);

                    Thread.Sleep(2000);
                }
            });
        }

        static void ServerInit()
        {
            _server = new QServer();
            // 服务端不再暴露断开事件，断开清理与统计改由处理器链的 ChannelInactive 通知承担。
            _server.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new DisconnectCounter(ServerOnDisconnected));
            _server.CalcInfo((ci, qi) =>
            {
                var result = string.Format("[Server] Producers: {0} Consumers: {1} Received: {2} Sent: {3}", ci.Item1, ci.Item2, ci.Item3, ci.Item4);
                qi.ForEach((item) =>
                {
                    result += string.Format(" | Queue[{0}]: {1}", item.Item1, item.Item2);
                });
                ConsoleHelper.WriteLine(result, ConsoleColor.Cyan);
            });
            _server.Start();
            ConsoleHelper.WriteLine("[Server] Server started on port 39654", ConsoleColor.Cyan);
        }

        private static void ServerOnDisconnected(string ID)
        {
            _server.Clear(ID);
            ConsoleHelper.WriteLine($"[Server] {ID.Substring(0, 16)}... disconnected", ConsoleColor.Red);
        }

        static void ProducerInit(string ipPort, string topic)
        {
            string producerID = "producer-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string msg = "test-" + producerID;

            Producer producer = new Producer(producerID, ipPort);

            // 发送完成与异常不再通过事件上报，这里按已提交条数统计发送量。
            while (producer.Connected)
            {
                try
                {
                    producer.Publish(topic, msg);
                    Interlocked.Increment(ref _producerSentCount);
                }
                catch (Exception ex)
                {
                    ConsoleHelper.WriteLine($"[Producer Error] {producerID}: {ex.Message}", ConsoleColor.Red);
                    break;
                }
            }
        }

        static void ConsumerInit(string ipPort, string topic)
        {
            string consumerID = "consumer-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            Consumer consumer = new Consumer(consumerID, ipPort);

            // 接收改由处理器链装配的入站处理器承担，回调内计数并归还池化缓冲。
            consumer.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new QueueMessageSink(obj =>
            {
                Interlocked.Increment(ref _consumerReceivedCount);
                obj.Dispose();
            }));

            consumer.Subscribe(topic);
            consumer.Start();
        }
    }
}
