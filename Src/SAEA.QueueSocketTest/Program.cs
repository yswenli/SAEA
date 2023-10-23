
using SAEA.Common;
using SAEA.Common.Threading;
using SAEA.QueueSocket;
using SAEA.QueueSocket.Model;
using System;
using System.Collections.Generic;
using System.Threading;

namespace SAEA.QueueSocketTest
{
    class Program
    {
        static void Main(string[] args)
        {
            do
            {
                ConsoleHelper.WriteLine("输入s启动队列服务器,输入p启动生产者，输入c启动消费者");

                var inputStr = ConsoleHelper.ReadLine();

                if (!string.IsNullOrEmpty(inputStr))
                {
                    var topic = "测试频道";

                    switch (inputStr.ToLower())
                    {
                        case "s":
                            ConsoleHelper.Title = "SAEA.QueueServer";
                            ServerInit();
                            break;
                        case "p":
                            ConsoleHelper.Title = "SAEA.QueueProducer";
                            ConsoleHelper.WriteLine("输入ip:port连接到队列服务器");
                            inputStr = ConsoleHelper.ReadLine();
                            ProducerInit(inputStr, topic);
                            break;
                        case "c":
                            ConsoleHelper.Title = "SAEA.QueueConsumer";
                            ConsoleHelper.WriteLine("输入ip:port连接到队列服务器");
                            inputStr = ConsoleHelper.ReadLine();
                            ConsumerInit(inputStr, topic);
                            break;
                        default:
                            ServerInit();
                            inputStr = "127.0.0.1:39654";
                            ProducerInit(inputStr, topic);
                            ConsumerInit(inputStr, topic);
                            break;
                    }
                    ConsoleHelper.WriteLine("回车退出！");
                    ConsoleHelper.ReadLine();
                    return;
                }
            }
            while (true);
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
            int pNum = 0;

            //string msg = "主要原因是由于在高并发环境下，由于来不及同步处理，请求往往会发生堵塞，比如说，大量的insert，update之类的请求同时到达MySQL，直接导致无数的行锁表锁，甚至最后请求会堆积过多，从而触发too many connections错误。通过使用消息队列，我们可以异步处理请求，从而缓解系统的压力。";
            string msg = "123";
            if (string.IsNullOrEmpty(ipPort)) ipPort = "127.0.0.1:39654";

            Producer producer = new Producer("productor" + Guid.NewGuid().ToString("N"), ipPort);

            producer.OnError += Producer_OnError;

            producer.OnDisconnected += Client_OnDisconnected;

            TaskHelper.LongRunning(() =>
            {
                var old = 0;
                var speed = 0;
                while (producer.Connected)
                {
                    speed = pNum - old;
                    old = pNum;
                    ConsoleHelper.WriteLine("生产者已成功发送：{0} 速度：{1}/s", pNum, speed);
                    Thread.Sleep(1000);
                }
            });

            while (producer.Connected)
            {

                producer.Publish(topic, msg);

                Interlocked.Increment(ref pNum);
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

            consumer.Subscribe(topic);
            consumer.Start();

            TaskHelper.LongRunning(() =>
            {
                var old = 0;
                var speed = 0;
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

        static int _outNum = 0;

        private static void Subscriber_OnMessage(QueueResult obj)
        {
            if (obj != null)
                _outNum += 1;
        }
    }
}
