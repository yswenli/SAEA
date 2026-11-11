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
*唯一标识：913c7859-1666-4029-9689-4771f4294c65
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 13:20:00
*描述：可注入传输抽象在 UDP/TCP 上的收发与端点验收
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 13:20:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 UDP/TCP 传输用例
*
*****************************************************************************/
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.P2P.Transport;

namespace SAEA.P2P.Tests.Transport
{
    /// <summary>
    /// 传输抽象：端点、双向收发与回调数据的生命周期。
    /// </summary>
    [TestClass]
    public class SaeaTransportTests
    {
        private const int WaitTimeout = 5000;

        /// <summary>
        /// UDP 传输绑定到 127.0.0.2 的随机端口后，本地端点报告实际端口
        /// </summary>
        [TestMethod]
        public async Task SaeaUdpTransport_绑定后_LocalEndPoint为实际端口()
        {
            using (var transport = new SaeaUdpTransport())
            {
                Assert.IsNull(transport.LocalEndPoint, "未绑定时本地端点应为 null");
                Assert.IsFalse(transport.IsConnected, "未绑定时不应标记为可收发");

                await transport.BindAsync(new IPEndPoint(IPAddress.Parse("127.0.0.2"), 0), CancellationToken.None);

                Assert.IsTrue(transport.IsConnected);
                Assert.IsNotNull(transport.LocalEndPoint);
                Assert.AreEqual(IPAddress.Parse("127.0.0.2"), transport.LocalEndPoint.Address);
                Assert.AreNotEqual(0, transport.LocalEndPoint.Port);
            }
        }

        /// <summary>
        /// 两个 UDP 传输互发报文，各自收到对端的原始字节
        /// </summary>
        [TestMethod]
        public async Task SaeaUdpTransport_双向发送_对端收到原始字节()
        {
            using (var hostA = new SaeaUdpTransport())
            using (var hostB = new SaeaUdpTransport())
            {
                var receivedByA = new TaskCompletionSource<byte[]>();
                var receivedByB = new TaskCompletionSource<byte[]>();

                hostA.DatagramReceived += (remote, data) => receivedByA.TrySetResult(data.ToArray());
                hostB.DatagramReceived += (remote, data) => receivedByB.TrySetResult(data.ToArray());

                await hostA.BindAsync(new IPEndPoint(IPAddress.Parse("127.0.0.2"), 0), CancellationToken.None);
                await hostB.BindAsync(new IPEndPoint(IPAddress.Parse("127.0.0.3"), 0), CancellationToken.None);
                hostA.StartReceive();
                hostB.StartReceive();

                var toB = Encoding.UTF8.GetBytes("A->B");
                var toA = Encoding.UTF8.GetBytes("B->A");

                await hostA.SendToAsync(hostB.LocalEndPoint, toB, CancellationToken.None);
                await hostB.SendToAsync(hostA.LocalEndPoint, toA, CancellationToken.None);

                CollectionAssert.AreEqual(toB, await WithTimeout(receivedByB.Task));
                CollectionAssert.AreEqual(toA, await WithTimeout(receivedByA.Task));
            }
        }

        /// <summary>
        /// 回调交出的 Memory 是复制品：后续报文到达后，先前拿到的数据仍可读
        /// </summary>
        [TestMethod]
        public async Task SaeaUdpTransport_收到的Memory_在回调返回后仍可读()
        {
            using (var hostA = new SaeaUdpTransport())
            using (var hostB = new SaeaUdpTransport())
            {
                var first = new TaskCompletionSource<ReadOnlyMemory<byte>>();
                var second = new TaskCompletionSource<ReadOnlyMemory<byte>>();
                var count = 0;

                hostB.DatagramReceived += (remote, data) =>
                {
                    // 只保存 Memory，不在回调里复制，用于验证传输层已经替上层复制过
                    if (Interlocked.Increment(ref count) == 1) first.TrySetResult(data);
                    else second.TrySetResult(data);
                };

                await hostA.BindAsync(new IPEndPoint(IPAddress.Parse("127.0.0.2"), 0), CancellationToken.None);
                await hostB.BindAsync(new IPEndPoint(IPAddress.Parse("127.0.0.3"), 0), CancellationToken.None);
                hostB.StartReceive();

                var firstPayload = Encoding.UTF8.GetBytes("first-datagram");
                var secondPayload = Encoding.UTF8.GetBytes("second-datagram-longer");

                await hostA.SendToAsync(hostB.LocalEndPoint, firstPayload, CancellationToken.None);
                var firstMemory = await WithTimeout(first.Task);

                await hostA.SendToAsync(hostB.LocalEndPoint, secondPayload, CancellationToken.None);
                var secondMemory = await WithTimeout(second.Task);

                CollectionAssert.AreEqual(firstPayload, firstMemory.ToArray());
                CollectionAssert.AreEqual(secondPayload, secondMemory.ToArray());
            }
        }

        /// <summary>
        /// TCP 连接侧连上监听侧后，两侧字节流可双向传输
        /// </summary>
        [TestMethod]
        public async Task SaeaTcpTransport_连接后_两侧字节流可双向传输()
        {
            using (var listener = new SaeaTcpTransport(new IPEndPoint(IPAddress.Loopback, 0)))
            {
                Assert.IsNotNull(listener.LocalEndPoint);
                Assert.AreNotEqual(0, listener.LocalEndPoint.Port);

                var acceptTask = listener.AcceptAsync(CancellationToken.None);

                using (var client = new SaeaTcpTransport())
                {
                    await client.ConnectAsync(listener.LocalEndPoint, CancellationToken.None);

                    Assert.IsTrue(client.IsConnected);
                    Assert.IsNotNull(client.Stream);
                    Assert.AreEqual(IPAddress.Loopback, client.LocalEndPoint.Address);

                    using (var accepted = await WithTimeout(acceptTask))
                    {
                        Assert.IsTrue(accepted.IsConnected);
                        Assert.IsNotNull(accepted.Stream);

                        var up = Encoding.UTF8.GetBytes("client->server");
                        await client.Stream.WriteAsync(up, 0, up.Length);
                        CollectionAssert.AreEqual(up, await ReadExactAsync(accepted.Stream, up.Length));

                        var down = Encoding.UTF8.GetBytes("server->client");
                        await accepted.Stream.WriteAsync(down, 0, down.Length);
                        CollectionAssert.AreEqual(down, await ReadExactAsync(client.Stream, down.Length));
                    }
                }
            }
        }

        /// <summary>
        /// 连接侧传输不能接受连接
        /// </summary>
        [TestMethod]
        public void SaeaTcpTransport_连接侧_AcceptAsync抛NotSupported()
        {
            using (var client = new SaeaTcpTransport())
            {
                Assert.ThrowsException<NotSupportedException>(() => client.AcceptAsync(CancellationToken.None));
            }
        }

        /// <summary>
        /// 监听侧传输不能主动连接
        /// </summary>
        [TestMethod]
        public async Task SaeaTcpTransport_监听侧_ConnectAsync抛NotSupported()
        {
            using (var listener = new SaeaTcpTransport(new IPEndPoint(IPAddress.Loopback, 0)))
            {
                await Assert.ThrowsExceptionAsync<NotSupportedException>(
                    () => listener.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 1), CancellationToken.None));
            }
        }

        /// <summary>
        /// 从流里读满指定字节数，读不满视为失败
        /// </summary>
        /// <param name="stream">字节流</param>
        /// <param name="length">期望读取的字节数</param>
        /// <returns>读到的字节</returns>
        private static async Task<byte[]> ReadExactAsync(System.IO.Stream stream, int length)
        {
            var buffer = new byte[length];
            var offset = 0;
            while (offset < length)
            {
                var read = await stream.ReadAsync(buffer, offset, length - offset);
                if (read <= 0) Assert.Fail($"流已结束，仅读到 {offset}/{length} 字节");
                offset += read;
            }
            return buffer;
        }

        /// <summary>
        /// 等待任务完成，超时即判定失败，避免用例挂死
        /// </summary>
        /// <typeparam name="T">结果类型</typeparam>
        /// <param name="task">待等待的任务</param>
        /// <returns>任务结果</returns>
        private static async Task<T> WithTimeout<T>(Task<T> task)
        {
            var completed = await Task.WhenAny(task, Task.Delay(WaitTimeout));
            if (completed != task) Assert.Fail($"等待超时（{WaitTimeout}ms）");
            return await task;
        }
    }
}