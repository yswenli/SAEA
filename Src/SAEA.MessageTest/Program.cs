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
*电子邮箱：yswenli@outlook.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 12:00:00
*修改人： yswenli
*版本号： v7.0.0.1
*描述：改为处理器链装配，新增 --functional 自动化入口
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Common.Threading;
using SAEA.MessageSocket;
using SAEA.MessageSocket.Model.Business;
using SAEA.MessageSocket.Model.Communication;
using SAEA.Sockets.Pipeline;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MessageTest
{
    class Program
    {
        static int Main(string[] args)
        {
            ConsoleHelper.Title = $"SAEA.Message测试 -- {DateTimeHelper.Now}";

            if (args != null && args.Length > 0 && args[0] == "--functional")
            {
                return FunctionalTest();
            }

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

            return 0;
        }

        #region 处理器

        /// <summary>
        /// 服务端观察者：打印接入、断开与链路异常，不改变消息走向
        /// </summary>
        private sealed class ServerObserver : InboundHandlerAdapter
        {
            public override Task ChannelActiveAsync(IChannelHandlerContext ctx)
            {
                ConsoleHelper.WriteLine("SAEA.Message服务器收到连接:" + ctx.ChannelId);
                return Task.CompletedTask;
            }

            public override Task ChannelInactiveAsync(IChannelHandlerContext ctx)
            {
                ConsoleHelper.WriteLine(ctx.ChannelId + "已断开连接", ConsoleColor.Red);
                return Task.CompletedTask;
            }

            public override Task ChannelExceptionCaughtAsync(IChannelHandlerContext ctx, Exception ex)
            {
                ConsoleHelper.WriteLine($"SAEA.Message服务器发生异常:{ctx.ChannelId}, err:{ex.Message}");
                return ctx.FireExceptionCaughtAsync(ex);
            }
        }

        /// <summary>
        /// 客户端状态：记录已收到的各类消息与应答，供演示与自测断言
        /// </summary>
        private sealed class ClientState
        {
            private int _privateCount;
            private int _channelCount;
            private int _groupCount;

            public string Tag;

            public volatile bool Subscribed;

            public volatile bool CreateGroupAnswered;

            public volatile bool AddMemberAnswered;

            public PrivateMessage LastPrivate;

            public ChannelMessage LastChannel;

            public GroupMessage LastGroup;

            public int PrivateCount
            {
                get { return Volatile.Read(ref _privateCount); }
            }

            public int ChannelCount
            {
                get { return Volatile.Read(ref _channelCount); }
            }

            public int GroupCount
            {
                get { return Volatile.Read(ref _groupCount); }
            }

            public void OnPrivate(PrivateMessage message)
            {
                LastPrivate = message;
                Interlocked.Increment(ref _privateCount);
            }

            public void OnChannel(ChannelMessage message)
            {
                LastChannel = message;
                Interlocked.Increment(ref _channelCount);
            }

            public void OnGroup(GroupMessage message)
            {
                LastGroup = message;
                Interlocked.Increment(ref _groupCount);
            }
        }

        /// <summary>
        /// 客户端观察者：连接建立时打印，按协议类型分发到状态对象
        /// </summary>
        private sealed class ClientObserver : InboundHandlerAdapter
        {
            private readonly ClientState _state;

            public ClientObserver(ClientState state)
            {
                _state = state;
            }

            public override Task ChannelActiveAsync(IChannelHandlerContext ctx)
            {
                ConsoleHelper.WriteLine($"[{_state.Tag}] 已连接:{ctx.ChannelId}");
                return Task.CompletedTask;
            }

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var chatMessage = msg as ChatMessage;
                if (chatMessage == null)
                    return Task.CompletedTask;

                switch (chatMessage.Type)
                {
                    case ChatMessageType.PrivateMessage:
                        var privateMessage = chatMessage.GetIMessage<PrivateMessage>();
                        if (privateMessage != null)
                        {
                            _state.OnPrivate(privateMessage);
                            ConsoleHelper.WriteLine($"[{_state.Tag}] 收到私信：{privateMessage.Content}");
                        }
                        break;

                    case ChatMessageType.ChannelMessage:
                        var channelMessage = chatMessage.GetIMessage<ChannelMessage>();
                        if (channelMessage != null)
                        {
                            _state.OnChannel(channelMessage);
                            ConsoleHelper.WriteLine($"[{_state.Tag}] 收到频道消息：{channelMessage.Content}");
                        }
                        break;

                    case ChatMessageType.GroupMessage:
                        var groupMessage = chatMessage.GetIMessage<GroupMessage>();
                        if (groupMessage != null)
                        {
                            _state.OnGroup(groupMessage);
                            ConsoleHelper.WriteLine($"[{_state.Tag}] 收到群组信息：{groupMessage.Content}");
                        }
                        break;

                    case ChatMessageType.SubscribeAnswer:
                        _state.Subscribed = chatMessage.Content == "1";
                        break;

                    case ChatMessageType.CreateGroupAnswer:
                        _state.CreateGroupAnswered = chatMessage.Content == "1";
                        break;

                    case ChatMessageType.AddMemberAnswer:
                        _state.AddMemberAnswered = chatMessage.Content == "1";
                        break;
                }

                return Task.CompletedTask;
            }
        }

        #endregion

        #region 功能自测

        /// <summary>
        /// 自动化功能自测：登录、私信、频道、群组四段，全部通过返回 0
        /// </summary>
        private static int FunctionalTest()
        {
            ConsoleHelper.WriteLine("SAEA.Message 功能自测开始...");

            var port = GetFreePort();

            var server = new MessageServer(port);
            server.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new ServerObserver());
            server.Start();

            var aliceState = new ClientState { Tag = "alice" };
            var bobState = new ClientState { Tag = "bob" };

            try
            {
                var alice = new MessageClient(102400, "127.0.0.1", port);
                alice.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new ClientObserver(aliceState));

                var bob = new MessageClient(102400, "127.0.0.1", port);
                bob.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new ClientObserver(bobState));

                alice.Connect();
                bob.Connect();

                alice.Login();
                bob.Login();

                if (!WaitUntil(() => alice.Logined && bob.Logined, TimeSpan.FromSeconds(10)))
                    return Fail("两端登录");

                // 私信：alice 发给 bob，服务端填入发送者后转发。
                alice.SendPrivateMsg(bob.ID, "functional-private");

                if (!WaitUntil(() => bobState.PrivateCount >= 1, TimeSpan.FromSeconds(10)))
                    return Fail("私信转发");

                var privateMessage = bobState.LastPrivate;

                if (privateMessage.Receiver != bob.ID || privateMessage.Sender != alice.ID
                    || privateMessage.Content != "functional-private")
                    return Fail("私信内容");

                // 频道：两端订阅同一频道后，alice 发送，bob 收到。
                const string ChannelName = "functional-channel";

                alice.Subscribe(ChannelName);
                bob.Subscribe(ChannelName);

                if (!WaitUntil(() => aliceState.Subscribed && bobState.Subscribed, TimeSpan.FromSeconds(10)))
                    return Fail("频道订阅");

                alice.SendChannelMsg(ChannelName, "functional-channel-msg");

                if (!WaitUntil(() => bobState.ChannelCount >= 1, TimeSpan.FromSeconds(10)))
                    return Fail("频道转发");

                var channelMessage = bobState.LastChannel;

                if (channelMessage.Name != ChannelName || channelMessage.Sender != alice.ID
                    || channelMessage.Content != "functional-channel-msg")
                    return Fail("频道消息内容");

                // 群组：alice 建群后 bob 入群，群消息只转发给其他成员。
                const string GroupName = "functional-group";

                alice.SendCreateGroup(GroupName);

                if (!WaitUntil(() => aliceState.CreateGroupAnswered, TimeSpan.FromSeconds(10)))
                    return Fail("创建群组");

                bob.SendAddMember(GroupName);

                if (!WaitUntil(() => bobState.AddMemberAnswered, TimeSpan.FromSeconds(10)))
                    return Fail("加入群组");

                alice.SendGroupMessage(GroupName, "functional-group-msg");

                if (!WaitUntil(() => bobState.GroupCount >= 1, TimeSpan.FromSeconds(10)))
                    return Fail("群组转发");

                var groupMessage = bobState.LastGroup;

                if (groupMessage.Name != GroupName || groupMessage.Sender != alice.ID
                    || groupMessage.Content != "functional-group-msg")
                    return Fail("群组消息内容");

                ConsoleHelper.WriteLine("SAEA.Message 功能自测全部通过。");
                return 0;
            }
            finally
            {
                server.Stop();
            }
        }

        private static int Fail(string stage)
        {
            ConsoleHelper.WriteLine("SAEA.Message 功能自测失败于：" + stage, ConsoleColor.Red);
            return 1;
        }

        private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;

            while (!condition())
            {
                if (DateTime.UtcNow >= deadline)
                    return false;

                ThreadHelper.Sleep(20);
            }

            return true;
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        #endregion

        #region MyRegion

        private static void ServerInit(int count = 1000, int size = 102400)
        {
            ConsoleHelper.WriteLine("SAEA.Message服务器正在启动...");

            MessageServer server = new MessageServer(39654, size, count);

            server.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new ServerObserver());

            server.Start();

            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    ConsoleHelper.Title = "MessageServer ClientCounts: " + server.ClientCounts;

                    ThreadHelper.Sleep(1000);
                }
            }, TaskCreationOptions.LongRunning);

            ConsoleHelper.WriteLine("SAEA.Message服务器已就绪!");
        }

        static int _connectCount = 0;

        /// <summary>
        /// 单机连接测试：只统计链激活次数，不订阅业务消息
        /// </summary>
        private static void LinkTest()
        {
            ConsoleHelper.WriteLine("回车开始连接测试...");

            ConsoleHelper.ReadLine();

            var count = 60 * 1000;

            Task.Run(() =>
            {
                ConsoleHelper.WriteLine("单机连接测试正在初始化...");

                for (int i = 0; i < count; i++)
                {
                    var mclient = new MessageClient();
                    mclient.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new ConnectingObserver());
                    mclient.ConnectAsync();
                }

                Task.Run(() =>
                {
                    while (_connectCount < count)
                    {
                        ThreadHelper.Sleep(1000);
                        ConsoleHelper.WriteLine($"单机连接测试已建立连接：{_connectCount} 未连接：{count - _connectCount}");
                    }
                    ConsoleHelper.WriteLine($"单机{count}连接已完成！");
                });
            });
        }

        /// <summary>
        /// 连接计数处理器：链激活时累加连接数
        /// </summary>
        private sealed class ConnectingObserver : InboundHandlerAdapter
        {
            public override Task ChannelActiveAsync(IChannelHandlerContext ctx)
            {
                Interlocked.Increment(ref _connectCount);
                return Task.CompletedTask;
            }
        }

        private static void FunTest()
        {
            //===============================================================
            ConsoleHelper.WriteLine("回车开始私信测试开始...");
            ConsoleHelper.ReadLine();

            var cc1State = new ClientState { Tag = "cc1" };
            var cc2State = new ClientState { Tag = "cc2" };

            var cc1 = new MessageClient();
            cc1.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new ClientObserver(cc1State));

            var cc2 = new MessageClient();
            cc2.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new ClientObserver(cc2State));

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
                    ThreadHelper.Sleep(500);
                }
            }, TaskCreationOptions.LongRunning);

            //===============================================================
            ConsoleHelper.WriteLine("回车开始频道测试开始...");
            ConsoleHelper.ReadLine();

            var clientState = new ClientState { Tag = "channel" };

            MessageClient client = new MessageClient();

            client.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new ClientObserver(clientState));

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

                    ThreadHelper.Sleep(500);
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
                    c.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new ClientObserver(new ClientState { Tag = "channel-" + i }));
                    list.Add(c);
                    c.Connect();
                    c.Login();
                    c.Subscribe(channelName);
                }
            }, TaskCreationOptions.LongRunning);
            //===============================================================

            ConsoleHelper.WriteLine("回车开始群组测试");
            ConsoleHelper.ReadLine();

            var groupName = "萌一号蠢";

            cc1.SendCreateGroup(groupName);
            cc2.SendAddMember(groupName);

            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    cc1.SendGroupMessage(groupName, "群主广播了！");
                    cc2.SendGroupMessage(groupName, "群主万岁！");
                    ThreadHelper.Sleep(100);
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
                    ccc.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new CountingObserver());
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
                        ThreadHelper.Sleep(1000);
                    }
                }, TaskCreationOptions.LongRunning);
            });

            ConsoleHelper.ReadLine();
        }

        static int calc = 0;

        static int cmc = 0;

        /// <summary>
        /// 频道消息计数处理器：只统计频道消息条数
        /// </summary>
        private sealed class CountingObserver : InboundHandlerAdapter
        {
            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var chatMessage = msg as ChatMessage;
                if (chatMessage != null && chatMessage.Type == ChatMessageType.ChannelMessage)
                    Interlocked.Increment(ref cmc);

                return Task.CompletedTask;
            }
        }
        #endregion

        #region PerformanceTest


        static void PerformanceTest()
        {
            ConsoleHelper.WriteLine("回车开始私信测试开始...");
            ConsoleHelper.ReadLine();

            var cc1 = new MessageClient();
            cc1.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new PrivateCountingObserver());

            var cc2 = new MessageClient();
            cc2.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new PrivateCountingObserver());

            ConsoleHelper.WriteLine("cc1 connecting...");
            cc1.Connect();
            ConsoleHelper.WriteLine("cc1 connected, ID=" + cc1.ID);

            ConsoleHelper.WriteLine("cc2 connecting...");
            cc2.Connect();
            ConsoleHelper.WriteLine("cc2 connected, ID=" + cc2.ID);

            cc1.Login();
            cc2.Login();

            Stopwatch stopwatch = Stopwatch.StartNew();

            int size = 1000000;

            Task.Run(async () =>
            {
                for (int i = 0; i < size; i++)
                {
                    await cc1.SendPrivateMsgAsync(cc2.ID, "你好呀,cc2！");
                    await cc2.SendPrivateMsgAsync(cc1.ID, "你好呀,cc1！");
                }
            });

            while (true)
            {
                ThreadHelper.Sleep(1000);
                ConsoleHelper.WriteLine("已收到转发的私信" + _pcount + "条");
                if (_pcount >= 2 * size)
                {
                    stopwatch.Stop();
                    ConsoleHelper.WriteLine("私信转发测试已完成，速度：" + _pcount / stopwatch.Elapsed.TotalSeconds);
                    break;
                }
            }
        }

        static int _pcount = 0;

        /// <summary>
        /// 私信计数处理器：只统计私信条数
        /// </summary>
        private sealed class PrivateCountingObserver : InboundHandlerAdapter
        {
            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var chatMessage = msg as ChatMessage;
                if (chatMessage != null && chatMessage.Type == ChatMessageType.PrivateMessage)
                    Interlocked.Increment(ref _pcount);

                return Task.CompletedTask;
            }
        }
        #endregion
    }
}