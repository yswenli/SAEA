/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MessageSocket.Tests
*文件名： MessageIntegrationTests
*版本号： net10.0
*唯一标识：f9c02b6d-4a83-4712-9d6e-3b5f8c1e7a96
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 11:30:00
*描述：消息服务端与客户端在本机回环上的端到端用例：私信转发与频道广播
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 11:30:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，私信转发与频道广播端到端用例
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MessageSocket.Model.Business;
using SAEA.MessageSocket.Model.Communication;
using SAEA.Sockets.Pipeline;

namespace SAEA.MessageSocket.Tests
{
    /// <summary>
    /// 服务端与客户端在本机回环上的端到端用例：登录、私信转发、频道广播都经处理器链完成。
    /// </summary>
    [TestClass]
    public class MessageIntegrationTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(20);

        /// <summary>
        /// 私信：服务端收到请求并在链上可见，接收方拿到填入发送者与时间的私信，发送方收到应答。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task MessageServer_PrivateMessage_ForwardsToReceiverThroughConfigurePipeline()
        {
            var port = GetFreePort();
            var serverSeen = new CaptureSink();
            var receiverSeen = new CaptureSink();
            var senderSeen = new CaptureSink();

            var server = new MessageServer(port);

            // 一个处理器实例只能挂一条链，按连接新建并共享同一个捕获汇。
            server.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new CapturingChatHandler(serverSeen));
            server.Start();

            try
            {
                var alice = new MessageClient(102400, "127.0.0.1", port);
                alice.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new CapturingChatHandler(senderSeen));

                var bob = new MessageClient(102400, "127.0.0.1", port);
                bob.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new CapturingChatHandler(receiverSeen));

                alice.Connect();
                bob.Connect();

                alice.Login();
                bob.Login();

                // 登录应答到达说明两端链路都已启动、服务端已登记接收方的写入口。
                Assert.IsTrue(await WaitUntilAsync(() => bob.Logined, WaitTimeout), "接收方应收到登录应答。");
                Assert.IsTrue(await WaitUntilAsync(() => alice.Logined, WaitTimeout), "发送方应收到登录应答。");

                alice.SendPrivateMsg(bob.ID, "hello-bob");

                var received = await receiverSeen.WaitForAsync(
                    m => m.Type == ChatMessageType.PrivateMessage, WaitTimeout).ConfigureAwait(false);

                var privateMessage = received.GetIMessage<PrivateMessage>();

                Assert.IsNotNull(privateMessage, "私信内容应能还原成领域对象。");
                Assert.AreEqual(alice.ID, privateMessage.Sender, "服务端应填入发送者标识。");
                Assert.AreEqual(bob.ID, privateMessage.Receiver, "接收者应保持为请求中的标识。");
                Assert.AreEqual("hello-bob", privateMessage.Content, "私信正文应原样送达。");

                var answer = await senderSeen.WaitForAsync(
                    m => m.Type == ChatMessageType.PrivateMessageAnswer, WaitTimeout).ConfigureAwait(false);

                Assert.AreEqual(string.Empty, answer.Content, "私信应答不携带额外内容。");

                Assert.IsTrue(
                    await serverSeen.WaitCountAsync(1, WaitTimeout),
                    "服务端使用者处理器应能看到这条请求。");
            }
            finally
            {
                server.Stop();
            }
        }

        /// <summary>
        /// 频道广播：同频道其他成员收到填入发送者与时间的频道消息，非成员不参与。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task MessageServer_ChannelMessage_FansOutToOtherSubscribers()
        {
            var port = GetFreePort();
            var aliceSeen = new CaptureSink();
            var bobSeen = new CaptureSink();

            var server = new MessageServer(port);
            server.Start();

            try
            {
                const string ChannelName = "integration-channel";

                var alice = new MessageClient(102400, "127.0.0.1", port);
                alice.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new CapturingChatHandler(aliceSeen));

                var bob = new MessageClient(102400, "127.0.0.1", port);
                bob.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new CapturingChatHandler(bobSeen));

                alice.Connect();
                bob.Connect();

                alice.Login();
                bob.Login();

                Assert.IsTrue(await WaitUntilAsync(() => alice.Logined, WaitTimeout), "发送方应收到登录应答。");
                Assert.IsTrue(await WaitUntilAsync(() => bob.Logined, WaitTimeout), "接收方应收到登录应答。");

                alice.Subscribe(ChannelName);
                bob.Subscribe(ChannelName);

                // 订阅应答内容为 "1" 表示服务端已把该连接加入频道。
                var aliceReady = await aliceSeen.WaitForAsync(
                    m => m.Type == ChatMessageType.SubscribeAnswer, WaitTimeout).ConfigureAwait(false);
                var bobReady = await bobSeen.WaitForAsync(
                    m => m.Type == ChatMessageType.SubscribeAnswer, WaitTimeout).ConfigureAwait(false);

                Assert.AreEqual("1", aliceReady.Content, "发送方应订阅成功。");
                Assert.AreEqual("1", bobReady.Content, "接收方应订阅成功。");

                alice.SendChannelMsg(ChannelName, "hello-channel");

                var received = await bobSeen.WaitForAsync(
                    m => m.Type == ChatMessageType.ChannelMessage, WaitTimeout).ConfigureAwait(false);

                var channelMessage = received.GetIMessage<ChannelMessage>();

                Assert.IsNotNull(channelMessage, "频道消息内容应能还原成领域对象。");
                Assert.AreEqual(ChannelName, channelMessage.Name, "频道名应原样保留。");
                Assert.AreEqual(alice.ID, channelMessage.Sender, "服务端应填入发送者标识。");
                Assert.AreEqual("hello-channel", channelMessage.Content, "频道消息正文应原样送达。");

                // 发送者是频道成员，按既有语义不回发给自己。
                Assert.IsFalse(
                    aliceSeen.Contains(m => m.Type == ChatMessageType.ChannelMessage),
                    "频道消息不应回发给发送者本人。");
            }
            finally
            {
                server.Stop();
            }
        }

        /// <summary>
        /// 轮询等待条件成立，用于等待链路状态位与应答到达。
        /// </summary>
        /// <param name="condition">判定条件。</param>
        /// <param name="timeout">最长等待时间。</param>
        /// <returns>条件成立返回 true，超时返回 false。</returns>
        private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (!condition())
            {
                if (DateTime.UtcNow >= deadline)
                    return false;

                await Task.Delay(20).ConfigureAwait(false);
            }

            return true;
        }

        /// <summary>
        /// 取一个本机空闲端口，避免用例之间互相占用。
        /// </summary>
        /// <returns>可用的 TCP 端口。</returns>
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

        /// <summary>
        /// 入站消息捕获处理器：把消息交给连接间共享的捕获汇。
        /// </summary>
        private sealed class CapturingChatHandler : InboundHandlerAdapter
        {
            private readonly CaptureSink _sink;

            /// <summary>
            /// 绑定捕获汇。
            /// </summary>
            /// <param name="sink">连接间共享的捕获汇。</param>
            public CapturingChatHandler(CaptureSink sink)
            {
                _sink = sink;
            }

            /// <inheritdoc />
            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var chatMessage = msg as ChatMessage;
                if (chatMessage != null)
                    _sink.Add(chatMessage);

                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// 捕获汇：按到达顺序保存消息，可按类型等待；多连接写入，线程安全。
        /// </summary>
        private sealed class CaptureSink
        {
            private readonly ConcurrentQueue<ChatMessage> _messages = new ConcurrentQueue<ChatMessage>();

            /// <summary>已捕获的消息数量。</summary>
            public int Count
            {
                get { return _messages.Count; }
            }

            /// <summary>
            /// 记录一条消息。
            /// </summary>
            /// <param name="message">已解码的消息。</param>
            public void Add(ChatMessage message)
            {
                _messages.Enqueue(message);
            }

            /// <summary>
            /// 判断已捕获的消息中是否存在满足条件的项。
            /// </summary>
            /// <param name="predicate">判定条件。</param>
            /// <returns>存在返回 true。</returns>
            public bool Contains(Func<ChatMessage, bool> predicate)
            {
                foreach (var message in _messages)
                {
                    if (predicate(message))
                        return true;
                }

                return false;
            }

            /// <summary>
            /// 等待至少一条满足条件的消息。
            /// </summary>
            /// <param name="predicate">判定条件。</param>
            /// <param name="timeout">最长等待时间。</param>
            /// <returns>第一条满足条件的消息。</returns>
            public async Task<ChatMessage> WaitForAsync(Func<ChatMessage, bool> predicate, TimeSpan timeout)
            {
                var deadline = DateTime.UtcNow + timeout;
                while (true)
                {
                    foreach (var message in _messages)
                    {
                        if (predicate(message))
                            return message;
                    }

                    if (DateTime.UtcNow >= deadline)
                        throw new TimeoutException("等待消息超时，已捕获 " + _messages.Count + " 条。");

                    await Task.Delay(20).ConfigureAwait(false);
                }
            }

            /// <summary>
            /// 等待至少收到指定数量的消息。
            /// </summary>
            /// <param name="count">期望的消息数量。</param>
            /// <param name="timeout">最长等待时间。</param>
            /// <returns>在超时前达到数量返回 true；超时返回 false。</returns>
            public async Task<bool> WaitCountAsync(int count, TimeSpan timeout)
            {
                var deadline = DateTime.UtcNow + timeout;
                while (_messages.Count < count)
                {
                    if (DateTime.UtcNow >= deadline)
                        return false;

                    await Task.Delay(20).ConfigureAwait(false);
                }

                return true;
            }
        }
    }
}