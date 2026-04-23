
using SAEA.Common;
using SAEA.Common.Threading;
using SAEA.QueueSocket;
using SAEA.QueueSocket.Model;

using System;
using System.Threading;

namespace SAEA.QueueSocketTest
{
    class Program
    {
        static void Main(string[] args)
        {
            ConsoleHelper.Title = $"SAEA.QueueSocketTest -- {DateTimeHelper.Now}";

            var inputStr = "";
            var topic = "hello";

            while (true)
            {
                ConsoleHelper.WriteLine("SAEA.QueueSocketTest\r\n \t输入s启动队列服务器\r\n\t输入p启动生产者\r\n\t输入c启动消费者\r\n\t输入t启动高并发测试");

                if (args == null || args.Length < 1 || args[0].IsNullOrEmpty())
                {
                    inputStr = ConsoleHelper.ReadLine();
                }
                else
                {
                    inputStr = args[0];
                    args = null;
                }

                if (!string.IsNullOrEmpty(inputStr))
                {
                    var ipPort = "";

                    switch (inputStr.ToLower())
                    {
                        case "s":
                            ServerInit();
                            break;
                        case "p":
                            ConsoleHelper.WriteLine("输入ip:port连接到队列服务器");
                            ipPort = ConsoleHelper.ReadLine();
                            ProducerInit(ipPort, topic);
                            break;
                        case "c":
                            ConsoleHelper.WriteLine("输入ip:port连接到队列服务器");
                            ipPort = ConsoleHelper.ReadLine();
                            ConsumerInit(ipPort, topic);
                            break;
                        case "sc":
                            ServerInit();
                            Thread.Sleep(1000);
                            ConsumerInit(inputStr, topic);
                            break;
                        case "sp":
                            ServerInit();
                            Thread.Sleep(1000);
                            ipPort = ConsoleHelper.ReadLine();
                            ProducerInit(ipPort, topic);
                            break;
                        case "a":
                            ServerInit();
                            Thread.Sleep(1000);
                            ipPort = ConsoleHelper.ReadLine();
                            ConsumerInit(ipPort, topic);
                            Thread.Sleep(1000);
                            ProducerInit(ipPort, topic);
                            break;
                        case "t":
                            HighConcurrencyTest.Run();
                            break;
                        default:
                            break;
                    }
                }
            }
        }



        static QServer _server;
        static void ServerInit()
        {
            _server = new QServer();
            _server.OnDisconnected += Server_OnDisconnected;
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

        private static void Server_OnDisconnected(string ID, Exception ex)
        {
            _server.Clear(ID);
            if (ex != null)
            {
                ConsoleHelper.WriteLine("{0} 已从服务器断开，err:{1}", ID, ex.ToString());
            }
        }

        static void ProducerInit(string ipPort, string topic)
        {
            long pNum = 0;

            string msg = "123";
            if (string.IsNullOrEmpty(ipPort)) ipPort = "127.0.0.1:39654";

            Producer producer = new Producer("productor" + Guid.NewGuid().ToString("N"), ipPort);

            producer.OnError += Producer_OnError;

            producer.OnDisconnected += Client_OnDisconnected;

            // 订阅消息实际发送完成事件
            producer.OnMessagesSent += (count) =>
            {
                Interlocked.Add(ref pNum, count);
            };

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
                    ConsoleHelper.WriteLine("生产者已成功发送：{0} 速度：{1}/s", current, speed);
                }
            });

            // 发送消息的主任务，不限制速度
            while (producer.Connected)
            {
                producer.Publish(topic, msg);
            }

        }

        private static void Producer_OnError(string ID, Exception ex)
        {
            ConsoleHelper.WriteLine("id:" + ID + ",error:" + ex.Message);
        }

        static void ConsumerInit(string ipPort, string topic)
        {
            if (string.IsNullOrEmpty(ipPort)) ipPort = "127.0.0.1:39654";
            Consumer consumer = new Consumer("subscriber-" + Guid.NewGuid().ToString("N"), ipPort);
            consumer.OnMessage += Subscriber_OnMessage;
            consumer.OnDisconnected += Client_OnDisconnected;
            consumer.OnError += Producer_OnError;

            consumer.Subscribe(topic);
            consumer.Start();

            TaskHelper.LongRunning(() =>
            {
                var old = 0L;
                var speed = 0L;
                while (consumer.Connected)
                {
                    speed = _outNum - old;
                    old = _outNum;
                    ConsoleHelper.WriteLine("消费者已成功接收：{0} 速度：{1}/s", _outNum, speed);
                    Thread.Sleep(1000);
                }
            });
        }

        private static void Client_OnDisconnected(string ID, Exception ex)
        {
            ConsoleHelper.WriteLine("当前连接已关闭");
        }

        static long _outNum = 0;

        private static void Subscriber_OnMessage(QueueMsg obj)
        {
            if (obj != null)
            {
                Interlocked.Increment(ref _outNum);
                obj.Dispose();
            }
        }
    }
}
