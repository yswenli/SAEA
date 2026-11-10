/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.FileSocket.Tests
*文件名： ClientSendFileTests
*版本号： net10.0
*唯一标识：27f01f12-47ed-4823-9d58-b820844b60e7
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 11:52:00
*描述：文件发送用例：完成回调必须在整份文件写出后触发
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 11:52:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖发送完成回调与信号量的时序
*
*****************************************************************************/
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Sockets.Base;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;

namespace SAEA.FileSocket.Tests
{
    /// <summary>
    /// 文件发送：完成回调只在整份文件写出后触发，并同步放开下一次发送。
    /// </summary>
    [TestClass]
    public class ClientSendFileTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(15);

        /// <summary>
        /// 发送完成后回调时，客户端已累计写出的字节数应等于文件长度，服务端应收到同样多的字节。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task SendFile_WhenSendCompletes_ReportsFullLengthAndDeliversAllBytes()
        {
            var port = GetFreePort();
            var filePath = CreateTempFile(8 * 1024 * 1024);
            var expected = new FileInfo(filePath).Length;

            var received = new ReceivedTracker(expected);
            var gate = new ManualResetEventSlim(false);

            var server = new Server(port, 4 * 1024);
            server.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new AcceptHandler(server, received, gate));

            Client client = null;

            try
            {
                server.Start();

                // 小发送缓冲让客户端在服务端暂停收包时很快写满内核缓冲并进入 await，时序差异才可观察。
                client = new Client(4 * 1024, "127.0.0.1", port);
                client.Connect();

                Assert.IsTrue(client.Connected, "客户端应已连接服务端。");

                var completed = new ManualResetEventSlim(false);
                var accepted = false;
                var outAtComplete = -1L;

                client.SendFile(filePath, 0, d =>
                {
                    accepted = d;
                    outAtComplete = client.Out;
                    completed.Set();
                });

                await Task.Delay(500).ConfigureAwait(false);
                Assert.IsFalse(
                    completed.IsSet,
                    "服务端暂停收包时发送尚未结束，完成回调不得提前触发。");

                gate.Set();

                Assert.IsTrue(await WaitAsync(completed, WaitTimeout), "发送完成回调应在超时前触发。");
                Assert.IsTrue(accepted, "服务端允许接收后回调参数应为 true。");

                Assert.AreEqual(expected, outAtComplete, "回调触发时整份文件应已写出。");
                Assert.IsTrue(await received.WaitAsync(WaitTimeout), "服务端应收满全部字节。");
            }
            finally
            {
                gate.Set();
                client?.Disconnect();
                server.Stop();

                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        /// <summary>
        /// 以等待句柄和截止时间等待异步信号。
        /// </summary>
        /// <param name="handle">待等待的句柄。</param>
        /// <param name="timeout">最长等待时间。</param>
        /// <returns>在超时前被置位返回 true。</returns>
        private static Task<bool> WaitAsync(ManualResetEventSlim handle, TimeSpan timeout)
        {
            return Task.Run(() => handle.Wait(timeout));
        }

        /// <summary>
        /// 申请一个当前空闲的本地端口。
        /// </summary>
        /// <returns>可用端口号。</returns>
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
        /// 在临时目录生成指定长度的随机文件。
        /// </summary>
        /// <param name="length">文件长度（字节）。</param>
        /// <returns>生成的文件完整路径。</returns>
        private static string CreateTempFile(int length)
        {
            var directory = Path.Combine(Path.GetTempPath(), "saea-filesocket-tests");
            Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".bin");
            var data = new byte[length];
            new Random(20261110).NextBytes(data);

            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(data, 0, data.Length);
            }

            return path;
        }

        /// <summary>
        /// 统计服务端已收到的文件字节数的等待器。
        /// </summary>
        private sealed class ReceivedTracker
        {
            private readonly ManualResetEventSlim _reached = new ManualResetEventSlim(false);

            private readonly long _expected;

            private long _total;

            /// <summary>
            /// 绑定期望收到的总长度。
            /// </summary>
            /// <param name="expected">期望的总长度（字节）。</param>
            public ReceivedTracker(long expected)
            {
                _expected = expected;
            }

            /// <summary>
            /// 累加一次接收长度，达到期望值后置位等待句柄。
            /// </summary>
            /// <param name="length">本次收到的帧体长度。</param>
            public void Add(int length)
            {
                if (Interlocked.Add(ref _total, length) >= _expected)
                    _reached.Set();
            }

            /// <summary>
            /// 等待收满期望字节数。
            /// </summary>
            /// <param name="timeout">最长等待时间。</param>
            /// <returns>在超时前收满返回 true。</returns>
            public Task<bool> WaitAsync(TimeSpan timeout)
            {
                return Task.Run(() =>
                {
                    if (Interlocked.Read(ref _total) >= _expected)
                        return true;

                    return _reached.Wait(timeout) && Interlocked.Read(ref _total) >= _expected;
                });
            }
        }

        /// <summary>
        /// 服务端测试处理器：收到传输请求即允许，并统计文件块字节数。
        /// </summary>
        private sealed class AcceptHandler : InboundHandlerAdapter
        {
            private readonly Server _server;

            private readonly ReceivedTracker _tracker;

            private readonly ManualResetEventSlim _gate;

            private int _gateHeld;

            /// <summary>
            /// 绑定服务端、字节统计器与收包闸门。
            /// </summary>
            /// <param name="server">所属服务端。</param>
            /// <param name="tracker">字节统计器。</param>
            /// <param name="gate">收包闸门，首个文件块到达后阻塞到测试放行。</param>
            public AcceptHandler(Server server, ReceivedTracker tracker, ManualResetEventSlim gate)
            {
                _server = server;
                _tracker = tracker;
                _gate = gate;
            }

            /// <summary>
            /// 请求帧回复允许，文件块帧直接统计，其它消息交给下游。
            /// </summary>
            /// <param name="ctx">当前处理器在链上的节点上下文。</param>
            /// <param name="msg">已解帧的消息。</param>
            /// <returns>处理完成后结束的任务。</returns>
            public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var protocal = msg as BaseSocketProtocal;
                if (protocal != null && protocal.Type == (byte)SocketProtocalType.RequestSend)
                {
                    _server.Allow(ctx.Channel.Id);
                    return;
                }

                if (protocal != null && protocal.Type == (byte)SocketProtocalType.BigData)
                {
                    // 首个文件块到达即暂停收包，让发送方写满内核缓冲后停在 await 上。
                    if (Interlocked.Exchange(ref _gateHeld, 1) == 0)
                        _gate.Wait(TimeSpan.FromSeconds(10));

                    _tracker.Add(protocal.Content.Length);
                    return;
                }

                await ctx.FireChannelReadAsync(msg).ConfigureAwait(false);
            }
        }
    }
}