/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.MessageTest
*文件名： Class1
*版本号： V1.0.0.0
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
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/

using SAEA.MessageSocket;
using SAEA.MessageSocket.Model.Business;
using SAEA.Commom;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MessageTest
{
    class Program
    {
        static void Main(string[] args)
        {
            ConsoleHelper.WriteLine("Message Test");

            ConsoleHelper.WriteLine("服务器正在启动...");

            MessageServer server = new MessageServer();

            server.Start();

            Task.Run(() =>
            {
                while (true)
                {
                    ConsoleHelper.Title = "MessageServer ClientCounts: " + server.ClientCounts;

                    Thread.Sleep(1000);
                }
            });

            ConsoleHelper.WriteLine("服务器就绪");

            //===============================================================

            MessageClient client = new MessageClient();
            client.OnChannelMessage += Client_OnChannelMessage;
            client.OnPrivateMessage += Client_OnPrivateMessage;

            ConsoleHelper.WriteLine("客户正在连接...");

            string channelName = "频道测试一";

            client.ConnectAsync((state) =>
            {
                ConsoleHelper.WriteLine("客户就绪");

                if (state == System.Net.Sockets.SocketError.Success)
                {

                    client.Login();
                    ConsoleHelper.WriteLine("客户登录成功");

                    client.Subscribe(channelName);
                    ConsoleHelper.WriteLine("客户订阅成功");

                    Task.Run(() =>
                    {

                        while (true)
                        {
                            client.SendChannelMsg(channelName, "hello!");

                            Thread.Sleep(60 * 1000);
                        }

                    });


                    ConsoleHelper.WriteLine("客户发送频道消息成功");
                }
            });


            //===============================================================



            ConsoleHelper.ReadLine();
            ConsoleHelper.WriteLine("单机连接测试...");
            List<MessageClient> slist = new List<MessageClient>();
            Task.Run(() =>
            {
                for (int i = 0; i < 50000; i++)
                {
                    var c = new MessageClient();
                    c.ConnectAsync((state) =>
                    {
                        slist.Add(c);
                    });
                    Thread.Sleep(1);
                    if (i > 1000)
                        Thread.Sleep(10);
                    if (i > 5000)
                        Thread.Sleep(100);
                    if (i > 10000)
                        Thread.Sleep(1000);
                }
                ConsoleHelper.WriteLine("单机5W连接就绪...");
            });




            //===============================================================




            ConsoleHelper.ReadLine();
            ConsoleHelper.WriteLine("开始订阅测试...");
            List<MessageClient> list = new List<MessageClient>();
            for (int i = 0; i < 100; i++)
            {
                var c = new MessageClient();
                if (i < 10)
                    c.OnChannelMessage += Client_OnChannelMessage2;
                c.ConnectAsync((state) =>
                {
                    c.Login();
                    c.Subscribe(channelName);
                    list.Add(c);
                });
                Thread.Sleep(10);
            }



            //===============================================================



            ConsoleHelper.ReadLine();
            ConsoleHelper.WriteLine("私信测试");

            var cc1 = new MessageClient();
            cc1.OnPrivateMessage += Client_OnPrivateMessage;

            var cc2 = new MessageClient();
            cc2.OnPrivateMessage += Client_OnPrivateMessage;

            cc1.ConnectAsync((state) =>
            {
                cc1.Login();

                Task.Run(() =>
                {
                    while (true)
                    {
                        if (cc2.Connected)
                            cc1.SendPrivateMsg(cc2.UserToken.ID, "你好呀,cc2！");
                        Thread.Sleep(1000);
                    }
                });

                cc1.Subscribe(channelName);

                list.Add(cc1);
            });


            cc2.ConnectAsync((state) =>
            {
                cc2.Login();

                Task.Run(() =>
                {
                    while (true)
                    {
                        if (cc1.Connected)
                            cc2.SendPrivateMsg(cc1.UserToken.ID, "你好呀,cc1！");
                        Thread.Sleep(1000);
                    }
                });

                cc2.Subscribe(channelName);

                list.Add(cc2);
            });




            //===============================================================





            ConsoleHelper.ReadLine();
            ConsoleHelper.WriteLine("群组测试");

            cc1.OnGroupMessage += Client_OnGroupMessage;
            cc2.OnGroupMessage += Client_OnGroupMessage;

            var groupName = "萌一号蠢";

            cc1.SendCreateGroup(groupName);
            cc2.SendAddMember(groupName);


            Task.Run(() =>
            {
                while (true)
                {
                    cc1.SendGroupMessage(groupName, "群主广播了！");
                    cc2.SendGroupMessage(groupName, "群主万岁！");
                    Thread.Sleep(100);
                }
            });




            //===============================================================




            ConsoleHelper.ReadLine();

            cc2.SendRemoveMember(groupName);
            cc1.SendRemoveGroup(groupName);
            client.Unsubscribe(channelName);


            ConsoleHelper.WriteLine("测试完成");
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
    }
}
