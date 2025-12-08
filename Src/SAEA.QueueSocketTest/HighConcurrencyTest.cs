using SAEA.Common;
using SAEA.Common.Threading;
using SAEA.QueueSocket;
using SAEA.QueueSocket.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.QueueSocketTest
{
    class HighConcurrencyTest
    {
        public static void Run()
        {
            ConsoleHelper.Title = "SAEA.QueueSocket High Concurrency Test";
            ConsoleHelper.WriteLine("Starting high concurrency test...");

            // 启动服务器
            ServerInit();

            // 等待服务器启动
            Thread.Sleep(1000);

            // 启动多个生产者
            int producerCount = 20;
            int consumerCount = 20;

            ConsoleHelper.WriteLine($"Starting {producerCount} producers and {consumerCount} consumers...");

            // 启动生产者
            for (int i = 0; i < producerCount; i++)
            {
                Task.Run(() =>
                {
                    ProducerInit("127.0.0.1:39654", "test-topic");
                });
            }

            // 启动消费者
            for (int i = 0; i < consumerCount; i++)
            {
                Task.Run(() =>
                {
                    ConsumerInit("127.0.0.1:39654", "test-topic");
                });
            }

            ConsoleHelper.WriteLine("Test started. Program will run for 30 seconds...");
            Thread.Sleep(30000);

            _server?.Stop();
        }

        static QServer _server;

        static void ServerInit()
        {
            _server = new QServer();
            _server.OnDisconnected += Server_OnDisconnected;
            _server.CalcInfo((ci, qi) =>
            {
                var result = string.Format("Producers: {0} Consumers: {1} Received: {2} Sent: {3}{4}", ci.Item1, ci.Item2, ci.Item3, ci.Item4, Environment.NewLine);

                qi.ForEach((item) =>
                {
                    result += string.Format("Queue: {0} Messages: {1} {2}", item.Item1, item.Item2, Environment.NewLine);
                });
                ConsoleHelper.WriteLine(result);
            });
            _server.Start();
            ConsoleHelper.WriteLine("Server started on port 39654");
        }

        private static void Server_OnDisconnected(string ID, Exception ex)
        {
            _server.Clear(ID);
            if (ex != null)
            {
                ConsoleHelper.WriteLine($"{ID} disconnected from server, error: {ex.Message}");
            }
        }

        static void ProducerInit(string ipPort, string topic)
        {
            long pNum = 0;
            string producerID = "producer-" + Guid.NewGuid().ToString("N");
            string msg = "test message " + producerID;

            Producer producer = new Producer(producerID, ipPort);

            producer.OnError += (ID, ex) =>
            {
                ConsoleHelper.WriteLine($"Producer {ID} error: {ex.Message}");
            };

            producer.OnDisconnected += (ID, ex) =>
            {
                ConsoleHelper.WriteLine($"Producer {ID} disconnected");
            };

            // 发送消息的统计任务
            TaskHelper.LongRunning(() =>
            {
                var old = 0L;
                var speed = 0L;
                var lastTime = DateTime.Now;
                while (producer.Connected)
                {
                    Thread.Sleep(1000);
                    var current = Interlocked.Read(ref pNum);
                    var currentTime = DateTime.Now;
                    var elapsed = (currentTime - lastTime).TotalSeconds;
                    speed = (long)((current - old) / elapsed);
                    old = current;
                    lastTime = currentTime;
                    ConsoleHelper.WriteLine($"Producer {producerID} sent: {current} speed: {speed}/s");
                }
            });

            // 订阅消息实际发送完成事件
            producer.OnMessagesSent += (count) =>
            {
                Interlocked.Add(ref pNum, count);
            };

            // 发送消息的主任务，限制发送速度
            Stopwatch stopwatch = Stopwatch.StartNew();
            int messagesSent = 0;
            int maxMessagesPerSecond = 1000; // 限制每秒发送的消息数
            
            while (producer.Connected)
            {
                try
                {
                    producer.Publish(topic, msg);
                    messagesSent++;
                    
                    // 限制发送速度
                    if (messagesSent >= maxMessagesPerSecond)
                    {
                        var elapsed = stopwatch.ElapsedMilliseconds;
                        if (elapsed < 1000)
                        {
                            Thread.Sleep(1000 - (int)elapsed);
                        }
                        messagesSent = 0;
                        stopwatch.Restart();
                    }
                }
                catch (Exception ex)
                {
                    ConsoleHelper.WriteLine($"Producer {producerID} publish error: {ex.Message}");
                    Thread.Sleep(100);
                }
            }
        }

        static void ConsumerInit(string ipPort, string topic)
        {
            string consumerID = "consumer-" + Guid.NewGuid().ToString("N");
            Consumer consumer = new Consumer(consumerID, ipPort);

            consumer.OnMessage += (QueueMsg obj) =>
            {
                if (obj != null)
                {
                    Interlocked.Increment(ref _outNum);
                }
            };

            consumer.OnDisconnected += (ID, ex) =>
            {
                ConsoleHelper.WriteLine($"Consumer {ID} disconnected");
            };

            consumer.OnError += (ID, ex) =>
            {
                ConsoleHelper.WriteLine($"Consumer {ID} error: {ex.Message}");
            };

            consumer.Subscribe(topic);
            consumer.Start();

            // 接收消息的统计任务
            TaskHelper.LongRunning(() =>
            {
                var old = 0;
                var speed = 0;
                while (consumer.Connected)
                {
                    speed = _outNum - old;
                    old = _outNum;
                    ConsoleHelper.WriteLine($"Total messages received: {_outNum} speed: {speed}/s");
                    Thread.Sleep(1000);
                }
            });
        }

        static int _outNum = 0;
    }
}
