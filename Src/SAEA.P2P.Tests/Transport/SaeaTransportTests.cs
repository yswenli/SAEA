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
*命名空间：SAEA.P2P.Tests.Transport
*文件名： SaeaTransportTests
*版本号： net10.0
*唯一标识：24527dc4-73c2-464b-8c7c-c8575a71e5d5
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 14:40:00
*描述：可注入传输抽象的生产实现验收
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 16:13:51
*修改人： yswenli
*版本号： net10.0
*描述：补充并发连接、接受队列上限与事件订阅者异常用例，并给 AcceptAsync 等待加超时守护
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.P2P.Transport;

namespace SAEA.P2P.Tests.Transport
{
    /// <summary>
    /// SAEA.Sockets 传输实现：绑定、双向收发、内存生命周期与端点能力。
    /// </summary>
    [TestCategory(P2PTestSuite.Unit)]
    [TestClass]
    public class SaeaTransportTests
    {
        /// <summary>
        /// 用例等待上限，避免底层不回调时挂住整个测试进程
        /// </summary>
        static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// UDP 绑定回环地址与 0 端口后，本地端点应是系统分配的实际端口
        /// </summary>
        [TestMethod]
        public async Task SaeaUdpTransport_绑定后_LocalEndPoint为实际端口()
        {
            using (var transport = new SaeaUdpTransport())
            {
                await transport.BindAsync(new IPEndPoint(IPAddress.Loopback, 0), CancellationToken.None);

                Assert.IsTrue(transport.IsConnected);
                Assert.IsNotNull(transport.LocalEndPoint);
                Assert.AreEqual(IPAddress.Loopback, transport.LocalEndPoint.Address);
                Assert.AreNotEqual(0, transport.LocalEndPoint.Port);
            }
        }

        /// <summary>
        /// 两个未连接 UDP 传输之间按数据报双向收发，来源端点与内容都与发送方一致
        /// </summary>
        [TestMethod]
        public async Task SaeaUdpTransport_双向发送_对端收到原始字节()
        {
            using (var first = new SaeaUdpTransport())
            using (var second = new SaeaUdpTransport())
            {
                await first.BindAsync(new IPEndPoint(IPAddress.Loopback, 0), CancellationToken.None);
                await second.BindAsync(new IPEndPoint(IPAddress.Loopback, 0), CancellationToken.None);

                first.StartReceive();
                second.StartReceive();

                var atSecond = new TaskCompletionSource<(IPEndPoint From, byte[] Data)>(TaskCreationOptions.RunContinuationsAsynchronously);
                var atFirst = new TaskCompletionSource<(IPEndPoint From, byte[] Data)>(TaskCreationOptions.RunContinuationsAsynchronously);

                second.DatagramReceived += (from, data) => atSecond.TrySetResult((from, data.ToArray()));
                first.DatagramReceived += (from, data) => atFirst.TrySetResult((from, data.ToArray()));

                var request = Encoding.UTF8.GetBytes("hello-udp");
                await first.SendToAsync(second.LocalEndPoint, request, CancellationToken.None);

                var received = await atSecond.Task.WaitAsync(WaitTimeout);

                CollectionAssert.AreEqual(request, received.Data);
                Assert.AreEqual(first.LocalEndPoint, received.From);

                var reply = Encoding.UTF8.GetBytes("pong-udp");
                await second.SendToAsync(first.LocalEndPoint, reply, CancellationToken.None);

                var echo = await atFirst.Task.WaitAsync(WaitTimeout);

                CollectionAssert.AreEqual(reply, echo.Data);
                Assert.AreEqual(second.LocalEndPoint, echo.From);
            }
        }

        /// <summary>
        /// 回调交出的内存已复制：后续数据报复用底层缓冲后，先前拿到的内容依然可读
        /// </summary>
        [TestMethod]
        public async Task SaeaUdpTransport_收到的Memory_在回调返回后仍可读()
        {
            using (var sender = new SaeaUdpTransport())
            using (var receiver = new SaeaUdpTransport())
            {
                await sender.BindAsync(new IPEndPoint(IPAddress.Loopback, 0), CancellationToken.None);
                await receiver.BindAsync(new IPEndPoint(IPAddress.Loopback, 0), CancellationToken.None);

                receiver.StartReceive();

                var firstPayload = Encoding.UTF8.GetBytes("first-datagram-payload");
                var secondPayload = Encoding.UTF8.GetBytes("second-datagram-payload-longer");

                var first = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
                var second = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);

                receiver.DatagramReceived += (from, data) =>
                {
                    if (!first.TrySetResult(data)) second.TrySetResult(data);
                };

                await sender.SendToAsync(receiver.LocalEndPoint, firstPayload, CancellationToken.None);

                var captured = await first.Task.WaitAsync(WaitTimeout);

                // 再发一包，覆盖底层可能复用的接收缓冲
                await sender.SendToAsync(receiver.LocalEndPoint, secondPayload, CancellationToken.None);
                await second.Task.WaitAsync(WaitTimeout);

                CollectionAssert.AreEqual(firstPayload, captured.ToArray());
            }
        }

        /// <summary>
        /// 监听 0 端口后，本地端点是实际端口，且传输处于可用状态
        /// </summary>
        [TestMethod]
        public void SaeaTcpTransport_端口0监听_LocalEndPoint为实际端口()
        {
            using (var listener = new SaeaTcpTransport(new IPEndPoint(IPAddress.Loopback, 0)))
            {
                Assert.IsTrue(listener.IsConnected);
                Assert.IsNotNull(listener.LocalEndPoint);
                Assert.AreEqual(IPAddress.Loopback, listener.LocalEndPoint.Address);
                Assert.AreNotEqual(0, listener.LocalEndPoint.Port);
                Assert.IsNull(listener.Stream);
            }
        }

        /// <summary>
        /// 连接侧连上监听侧后，双方都能通过字节流双向收发，且本地端点各自可见
        /// </summary>
        [TestMethod]
        public async Task SaeaTcpTransport_连接侧连上监听侧_双向字节流往返()
        {
            using (var listener = new SaeaTcpTransport(new IPEndPoint(IPAddress.Loopback, 0)))
            using (var client = new SaeaTcpTransport())
            {
                await client.ConnectAsync(listener.LocalEndPoint, CancellationToken.None);

                Assert.IsTrue(client.IsConnected);
                Assert.IsNotNull(client.LocalEndPoint);
                Assert.AreNotEqual(0, client.LocalEndPoint.Port);

                using (var timeout = new CancellationTokenSource(WaitTimeout))
                using (var accepted = await listener.AcceptAsync(CancellationToken.None).WaitAsync(WaitTimeout))
                {
                    Assert.IsTrue(accepted.IsConnected);
                    Assert.IsNotNull(accepted.LocalEndPoint);
                    Assert.AreEqual(listener.LocalEndPoint.Port, accepted.LocalEndPoint.Port);

                    var payload = Encoding.UTF8.GetBytes("tcp-payload");
                    await client.Stream.WriteAsync(payload, 0, payload.Length, timeout.Token);

                    var buffer = new byte[payload.Length];
                    var read = await ReadExactAsync(accepted.Stream, buffer, timeout.Token);

                    Assert.AreEqual(payload.Length, read);
                    CollectionAssert.AreEqual(payload, buffer);

                    var reply = Encoding.UTF8.GetBytes("tcp-reply");
                    await accepted.Stream.WriteAsync(reply, 0, reply.Length, timeout.Token);

                    var echo = new byte[reply.Length];
                    var echoed = await ReadExactAsync(client.Stream, echo, timeout.Token);

                    Assert.AreEqual(reply.Length, echoed);
                    CollectionAssert.AreEqual(reply, echo);
                }
            }
        }

        /// <summary>
        /// 监听侧的 Accepted 事件与 AcceptAsync 是两条取用通道：事件先收到第一条，AcceptAsync 也能取到已入队的连接，
        /// 第二条连接才由等待中的 AcceptAsync 拿到
        /// </summary>
        [TestMethod]
        public async Task SaeaTcpTransport_监听侧_事件与AcceptAsync都交付已建链连接()
        {
            using (var listener = new SaeaTcpTransport(new IPEndPoint(IPAddress.Loopback, 0)))
            using (var firstClient = new SaeaTcpTransport())
            using (var secondClient = new SaeaTcpTransport())
            using (var timeout = new CancellationTokenSource(WaitTimeout))
            {
                var viaEvent = new TaskCompletionSource<ITcpTransport>(TaskCreationOptions.RunContinuationsAsynchronously);
                listener.Accepted += transport => viaEvent.TrySetResult(transport);

                await firstClient.ConnectAsync(listener.LocalEndPoint, CancellationToken.None);

                var fromEvent = await viaEvent.Task.WaitAsync(WaitTimeout);

                Assert.IsNotNull(fromEvent);
                Assert.IsTrue(fromEvent.IsConnected);

                // 第一条连接同时已进入接受队列，事件交付不消费队列
                var queued = await listener.AcceptAsync(timeout.Token);

                Assert.AreSame(fromEvent, queued);
                Assert.IsTrue(queued.IsConnected);

                var pending = listener.AcceptAsync(timeout.Token);

                await secondClient.ConnectAsync(listener.LocalEndPoint, CancellationToken.None);

                using (var fromAccept = await pending.WaitAsync(WaitTimeout))
                {
                    Assert.IsNotNull(fromAccept);
                    Assert.IsTrue(fromAccept.IsConnected);
                    Assert.AreNotSame(fromEvent, fromAccept);
                }

                queued.Dispose();
            }
        }

        /// <summary>
        /// 同一连接侧实例并发发起两次连接：只有一条能建立，另一条以 InvalidOperationException 拒绝，
        /// 监听侧也只收到一条连接
        /// </summary>
        [TestMethod]
        public async Task ConnectAsync_并发两次调用_只成功一条()
        {
            using (var listener = new SaeaTcpTransport(new IPEndPoint(IPAddress.Loopback, 0)))
            using (var client = new SaeaTcpTransport())
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                var first = CaptureAsync(client.ConnectAsync(listener.LocalEndPoint, CancellationToken.None));
                var second = CaptureAsync(client.ConnectAsync(listener.LocalEndPoint, CancellationToken.None));

                var outcomes = await Task.WhenAll(first, second).WaitAsync(timeout.Token);

                Assert.AreEqual(1, outcomes.Count(error => error == null), "并发连接只允许成功一条");
                Assert.AreEqual(1, outcomes.Count(error => error is InvalidOperationException), "被拒绝的一条应报重复连接");

                using (var accepted = await listener.AcceptAsync(CancellationToken.None).WaitAsync(WaitTimeout))
                {
                    Assert.IsTrue(accepted.IsConnected);
                }

                // 失败的那次调用根本没有建链，监听侧不存在第二条连接
                await Assert.ThrowsExceptionAsync<TimeoutException>(
                    () => listener.AcceptAsync(CancellationToken.None).WaitAsync(WaitTimeout));
            }
        }

        /// <summary>
        /// 不调用 AcceptAsync 时接受队列在上限处截断：连上 40 条（多于上限）后能取走的仍不超过上限，
        /// 溢出的连接在入队前就被释放，取空后继续等待只会超时，监听侧没有卡死
        /// </summary>
        [TestMethod]
        public async Task AcceptAsync_未取走的连接不超过上限_溢出连接被释放()
        {
            // 连接条数取一个明显大于当前上限的固定值，保证一定触发溢出；上限本身从实现读取，不在用例里硬编码
            const int total = 40;

            using (var listener = new SaeaTcpTransport(new IPEndPoint(IPAddress.Loopback, 0)))
            using (var drainTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                // 每条连接都会触发 Accepted：等全部到达就说明监听侧已处理完所有连接，队列状态稳定，可以断言
                var acceptedSeen = 0;
                var allSeen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                listener.Accepted += _ =>
                {
                    if (Interlocked.Increment(ref acceptedSeen) >= total) allSeen.TrySetResult(true);
                };

                var clients = new List<SaeaTcpTransport>();

                try
                {
                    for (var i = 0; i < total; i++)
                    {
                        var client = new SaeaTcpTransport();
                        clients.Add(client);

                        await client.ConnectAsync(listener.LocalEndPoint, CancellationToken.None).WaitAsync(WaitTimeout);
                    }

                    await allSeen.Task.WaitAsync(WaitTimeout);

                    var taken = new List<ITcpTransport>();

                    try
                    {
                        // 一直取到等待超时：既数出实际能取走的条数，也证明监听侧没有卡死
                        while (true)
                        {
                            try
                            {
                                taken.Add(await listener.AcceptAsync(drainTimeout.Token).WaitAsync(WaitTimeout));
                            }
                            catch (TimeoutException)
                            {
                                break;
                            }
                        }

                        Assert.IsTrue(taken.Count < total, "溢出的连接应在入队前就被释放");

                        var limit = ReadPendingAcceptedLimit();

                        Assert.IsTrue(limit < total, "用例连接条数应大于接受队列上限");
                        Assert.IsTrue(taken.Count <= limit, "可取走的连接不应超过接受队列上限");
                    }
                    finally
                    {
                        foreach (var transport in taken) transport.Dispose();
                    }
                }
                finally
                {
                    foreach (var client in clients) client.Dispose();
                }
            }
        }

        /// <summary>
        /// Accepted 订阅者抛异常不影响底层接收循环：订阅者异常不再连带断掉刚接受的连接，
        /// 该连接仍能收发字节，随后建立的第二个连接也能正常取到
        /// </summary>
        [TestMethod]
        public async Task Accepted_订阅者抛异常_不影响后续接受()
        {
            using (var listener = new SaeaTcpTransport(new IPEndPoint(IPAddress.Loopback, 0)))
            using (var firstClient = new SaeaTcpTransport())
            using (var secondClient = new SaeaTcpTransport())
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                var notified = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                listener.Accepted += _ =>
                {
                    notified.TrySetResult(true);
                    throw new InvalidOperationException("订阅者故意抛出的异常");
                };

                await firstClient.ConnectAsync(listener.LocalEndPoint, CancellationToken.None);

                Assert.IsTrue(await notified.Task.WaitAsync(WaitTimeout));

                using (var firstAccepted = await listener.AcceptAsync(timeout.Token).WaitAsync(WaitTimeout))
                {
                    Assert.IsTrue(firstAccepted.IsConnected);

                    // 只有当异常被底层吞掉、没有连带断开该连接时，这条字节流才还能用
                    var payload = Encoding.UTF8.GetBytes("after-handler-exception");
                    await firstClient.Stream.WriteAsync(payload, 0, payload.Length, timeout.Token);

                    var buffer = new byte[payload.Length];
                    var read = await ReadExactAsync(firstAccepted.Stream, buffer, timeout.Token);

                    Assert.AreEqual(payload.Length, read);
                    CollectionAssert.AreEqual(payload, buffer);
                }

                await secondClient.ConnectAsync(listener.LocalEndPoint, CancellationToken.None);

                using (var secondAccepted = await listener.AcceptAsync(timeout.Token).WaitAsync(WaitTimeout))
                {
                    Assert.IsTrue(secondAccepted.IsConnected);
                }
            }
        }

        /// <summary>
        /// 监听侧只承担接受连接，主动连接应被拒绝
        /// </summary>
        [TestMethod]
        public async Task SaeaTcpTransport_监听侧调用ConnectAsync_抛NotSupportedException()
        {
            using (var listener = new SaeaTcpTransport(new IPEndPoint(IPAddress.Loopback, 0)))
            {
                await Assert.ThrowsExceptionAsync<NotSupportedException>(
                    () => listener.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 1), CancellationToken.None));
            }
        }

        /// <summary>
        /// 连接侧只承担主动连接，等待接受应被拒绝
        /// </summary>
        [TestMethod]
        public void SaeaTcpTransport_连接侧调用AcceptAsync_抛NotSupportedException()
        {
            using (var client = new SaeaTcpTransport())
            {
                Assert.ThrowsException<NotSupportedException>(
                    () => client.AcceptAsync(CancellationToken.None));
            }
        }

        /// <summary>
        /// 监听侧释放后，仍在等待的 AcceptAsync 立即以取消结束，不会悬挂
        /// </summary>
        [TestMethod]
        public async Task SaeaTcpTransport_释放监听侧_等待中的AcceptAsync被取消()
        {
            var listener = new SaeaTcpTransport(new IPEndPoint(IPAddress.Loopback, 0));

            var pending = listener.AcceptAsync(CancellationToken.None);

            listener.Dispose();

            await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => pending);
        }

        /// <summary>
        /// 连接侧未连接时不报告可用
        /// </summary>
        [TestMethod]
        public void SaeaTcpTransport_未连接_IsConnected为false()
        {
            using (var client = new SaeaTcpTransport())
            {
                Assert.IsFalse(client.IsConnected);
                Assert.IsNull(client.LocalEndPoint);
                Assert.IsNull(client.Stream);
            }
        }

        /// <summary>
        /// 读取传输实现的接受队列上限；上限是内部约束，按实现值驱动用例，避免第二处硬编码随实现漂移
        /// </summary>
        /// <returns>接受队列上限</returns>
        static int ReadPendingAcceptedLimit()
        {
            var field = typeof(SaeaTcpTransport).GetField("MaxPendingAccepted", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(field, "未找到接受队列上限常量");

            return (int)field.GetRawConstantValue();
        }

        /// <summary>
        /// 等待任务结束并返回其异常，用于同时观察并发调用的成功与失败
        /// </summary>
        /// <param name="task">被观察的任务</param>
        /// <returns>任务成功时为 null，失败时为捕获到的异常</returns>
        static async Task<Exception> CaptureAsync(Task task)
        {
            try
            {
                await task;
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        /// <summary>
        /// 读到指定长度或对端结束为止
        /// </summary>
        /// <param name="stream">字节流</param>
        /// <param name="buffer">目标缓冲</param>
        /// <param name="token">取消令牌</param>
        /// <returns>实际读到的字节数</returns>
        static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken token)
        {
            var total = 0;

            while (total < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer, total, buffer.Length - total, token);

                if (read <= 0) break;

                total += read;
            }

            return total;
        }
    }
}