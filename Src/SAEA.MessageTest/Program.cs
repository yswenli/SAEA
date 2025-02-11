/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.MessageTest
*文件名： Class1
*版本号： v7.0.0.1
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 15:54:21
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Common.Threading;
using SAEA.MessageSocket;
using SAEA.MessageSocket.Model.Business;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MessageTest
{
    class Program
    {
        static void Main(string[] args)
        {
            ConsoleHelper.Title = $"SAEA.Message测试 -- {DateTimeHelper.Now}";

            ConsoleHelper.WriteLine("Message testing ");

            ConsoleHelper.WriteLine("S boot server \r\n\t\t\t F starts functional testing \r\n\t\t\t L start pressure testing\r\n\t\t\t T start channelmessage testing\r\n\t\t\t P start performance testing");

            var input = ConsoleHelper.ReadLine().ToUpper();

            switch (input)
            {
                case "S":
                    ServerInit();
                    break;

                case "F":
                    FunTest();
                    break;

                case "L":
                    LinkTest();
                    break;
                case "T":
                    ChannelMsgTest();
                    break;

                case "P":
                    PerformanceTest();
                    break;

                case "SF":
                    ServerInit();
                    FunTest();
                    break;
                case "SL":
                    ServerInit(100 * 1000, 100);
                    LinkTest();
                    break;
                case "ST":
                    ServerInit();
                    ChannelMsgTest();
                    break;
                case "SP":
                    ServerInit();
                    PerformanceTest();
                    break;
                default:
                    ServerInit();
                    FunTest();
                    break;
            }

            ConsoleHelper.WriteLine("Press enter to finish.");
            ConsoleHelper.ReadLine();
        }

        private static void Client_OnGroupMessage(GroupMessage msg)
        {
            ConsoleHelper.WriteLine("收到群组信息：{0}", msg.Content);
        }

        private static void Client_OnPrivateMessage(PrivateMessage msg)
        {
            ConsoleHelper.WriteLine("收到私信：{0}", msg.Content);
        }

        private static void Client_OnChannelMessage(ChannelMessage msg)
        {
            ConsoleHelper.WriteLine("收到频道消息：{0}", msg.Content);
        }

        private static void Client_OnChannelMessage2(ChannelMessage msg)
        {
            ConsoleHelper.WriteLine("收到频道消息2：{0}", msg.Content);
        }

        private static void Server_OnDisconnected(string ID, Exception ex)
        {
            ConsoleHelper.WriteLine($"{ID}已断开连接，ex:{ex?.Message}", ConsoleColor.Red);
        }


        #region MyRegion

        private static void ServerInit(int count = 1000, int size = 102400)
        {
            ConsoleHelper.WriteLine("SAEA.Message服务器正在启动...");

            MessageServer server = new MessageServer(39654, size, count);

            server.OnAccepted += Server_OnAccepted;

            server.OnError += Server_OnError;

            server.OnDisconnected += Server_OnDisconnected;

            server.Start();

            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    ConsoleHelper.Title = "MessageServer ClientCounts: " + server.ClientCounts;

                    Thread.Sleep(1000);
                }
            }, TaskCreationOptions.LongRunning);

            ConsoleHelper.WriteLine("SAEA.Message服务器已就绪!");
        }

        private static void Server_OnError(string ID, Exception ex)
        {
            ConsoleHelper.WriteLine($"SAEA.Message服务器发生异常:{ID}, err:{ex.Message}");
        }

        private static void Server_OnAccepted(object obj)
        {
            //ConsoleHelper.WriteLine("SAEA.Message服务器收到连接" + ((IUserToken)obj).ID);
        }


        static int _connectCount = 0;
        private static void LinkTest()
        {
            ConsoleHelper.WriteLine("回车开始连接测试...");

            ConsoleHelper.ReadLine();

            var count = 60 * 1000;

            MessageClient mclient;

            Task.Run(() =>
            {
                ConsoleHelper.WriteLine("单机连接测试正在初始化...");

                for (int i = 0; i < count; i++)
                {
                    mclient = new MessageClient();
                    mclient.OnConnected += Mclient_OnConnected;
                    mclient.ConnectAsync();
                }

                Task.Run(() =>
                {
                    while (_connectCount < count)
                    {
                        Thread.Sleep(1000);
                        ConsoleHelper.WriteLine($"单机连接测试已建立连接：{_connectCount} 未连接：{count - _connectCount}");
                    }
                    ConsoleHelper.WriteLine($"单机{count}连接已完成！");
                });
            });
        }

        private static void Mclient_OnConnected(MessageClient messageClient)
        {
            Interlocked.Increment(ref _connectCount);
        }

        private static void FunTest()
        {
            //===============================================================
            ConsoleHelper.WriteLine("回车开始私信测试开始...");
            ConsoleHelper.ReadLine();
            var cc1 = new MessageClient();
            cc1.OnPrivateMessage += Client_OnPrivateMessage;

            var cc2 = new MessageClient();
            cc2.OnPrivateMessage += Client_OnPrivateMessage;

            cc1.Connect();
            cc2.Connect();

            cc1.Login();
            cc2.Login();

            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    cc1.SendPrivateMsg(cc2.ID, "你好呀,cc2！");
                    cc2.SendPrivateMsg(cc1.ID, "你好呀,cc1！");
                    Thread.Sleep(500);
                }
            }, TaskCreationOptions.LongRunning);

            //===============================================================
            ConsoleHelper.WriteLine("回车开始频道测试开始...");
            ConsoleHelper.ReadLine();

            MessageClient client = new MessageClient();

            client.OnChannelMessage += Client_OnChannelMessage;

            ConsoleHelper.WriteLine("客户正在连接...");

            string channelName = "频道测试一";

            client.Connect();

            ConsoleHelper.WriteLine("客户端就绪");

            client.Login();
            ConsoleHelper.WriteLine("客户端登录成功");

            client.Subscribe(channelName);
            ConsoleHelper.WriteLine("客户端订阅成功");

            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    client.SendChannelMsg(channelName, "hello!");

                    Thread.Sleep(500);
                }

            }, TaskCreationOptions.LongRunning);

            //===============================================================
            ConsoleHelper.WriteLine("回车开始订阅频道消息...");
            ConsoleHelper.ReadLine();

            List<MessageClient> list = new List<MessageClient>();
            Task.Factory.StartNew(() =>
            {
                for (int i = 0; i < 10; i++)
                {
                    var c = new MessageClient();
                    c.OnChannelMessage += Client_OnChannelMessage2;
                    list.Add(c);
                    c.Connect();
                    c.Login();
                    c.Subscribe(channelName);
                }
            }, TaskCreationOptions.LongRunning);
            //===============================================================

            ConsoleHelper.WriteLine("回车开始群组测试");
            ConsoleHelper.ReadLine();

            cc1.OnGroupMessage += Client_OnGroupMessage;
            cc2.OnGroupMessage += Client_OnGroupMessage;

            var groupName = "萌一号蠢";

            cc1.SendCreateGroup(groupName);
            cc2.SendAddMember(groupName);


            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    cc1.SendGroupMessage(groupName, "群主广播了！");
                    cc2.SendGroupMessage(groupName, "群主万岁！");
                    Thread.Sleep(100);
                }
            }, TaskCreationOptions.LongRunning);



            ConsoleHelper.ReadLine();
            ConsoleHelper.WriteLine("The function test has been completed.");
            //===============================================================

            cc2.SendRemoveMember(groupName);
            cc1.SendRemoveGroup(groupName);
            client.Unsubscribe(channelName);
        }


        private static void ChannelMsgTest()
        {
            ConsoleHelper.WriteLine("发布订阅测试开始...");

            var channelName = "testChannel";

            var cc1 = new MessageClient();
            cc1.Connect();
            cc1.Subscribe(channelName);


            List<MessageClient> list = new List<MessageClient>();

            Task.Run(() =>
            {
                ConsoleHelper.WriteLine("正在开始初始化客户端...");

                for (int i = 0; i < 1000; i++)
                {
                    var ccc = new MessageClient();
                    ccc.OnChannelMessage += Ccc_OnChannelMessage;
                    list.Add(ccc);
                }

                ConsoleHelper.WriteLine("正在建立连接...");

                foreach (var item in list)
                {
                    item.Connect();
                }

                ConsoleHelper.WriteLine("正在订阅...");

                foreach (var item in list)
                {
                    item.Subscribe(channelName);
                }

                ConsoleHelper.WriteLine("正在转发...");

                cc1.SendChannelMsg(channelName, channelName);

                Task.Factory.StartNew(() =>
                {
                    while (true)
                    {
                        ConsoleHelper.WriteLine("当前已接收到消息数：" + cmc);
                        Interlocked.Increment(ref calc);
                        if (cmc >= 1000)
                        {
                            ConsoleHelper.WriteLine("测试完毕，当前用时：" + calc + "秒");
                            break;
                        }
                        Thread.Sleep(1000);
                    }
                }, TaskCreationOptions.LongRunning);
            });

            ConsoleHelper.ReadLine();
        }

        static int calc = 0;

        static int cmc = 0;

        private static void Ccc_OnChannelMessage(ChannelMessage obj)
        {
            Interlocked.Increment(ref cmc);
        }
        #endregion

        #region PerformanceTest


        static void PerformanceTest()
        {
            ConsoleHelper.WriteLine("回车开始私信测试开始...");
            ConsoleHelper.ReadLine();
            var cc1 = new MessageClient();
            cc1.OnPrivateMessage += Client_OnPrivateMessage1;

            var cc2 = new MessageClient();
            cc2.OnPrivateMessage += Client_OnPrivateMessage1;

            cc1.Connect();
            cc2.Connect();

            cc1.Login();
            cc2.Login();

            Stopwatch stopwatch = Stopwatch.StartNew();

            int size = 100000;

            TaskHelper.LongRunning(() =>
            {
                for (int i = 0; i < size; i++)
                {
                    cc1.SendPrivateMsg(cc2.ID, "你好呀,cc2！");
                    cc2.SendPrivateMsg(cc1.ID, "你好呀,cc2！");
                }
            });

            while (true)
            {
                if (_pcount < 2 * size)
                {
                    ConsoleHelper.WriteLine($"已处理私信{_pcount}条");
                    Thread.Sleep(1000);
                }
                else
                {
                    stopwatch.Stop();
                    ConsoleHelper.WriteLine($"私信测试已完成，速度：{_pcount / stopwatch.Elapsed.TotalSeconds}");
                    break;
                }
            }
        }

        static int _pcount = 0;
        private static void Client_OnPrivateMessage1(PrivateMessage msg)
        {
            Interlocked.Increment(ref _pcount);
        }
        #endregion
    }
}
