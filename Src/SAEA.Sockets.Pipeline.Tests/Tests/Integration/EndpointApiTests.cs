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
*命名空间：SAEA.Sockets.Pipeline.Tests.Tests.Integration
*文件名： EndpointApiTests
*版本号： net10.0
*唯一标识：f817635a-ead0-40b0-bf3c-60178bad77f9
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 10:10:00
*描述：UDP 服务端 SendToAsync、LocalEndPoint 与带来源地址回调的验收用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 10:10:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 UDP 发送原语与来源地址回调用例
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Sockets;
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Core.Udp;
using SAEA.Sockets.Model;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Integration
{
    /// <summary>
    /// UDP 服务端的发送原语、本地端点与带来源地址的接收回调。
    /// </summary>
    [TestClass]
    public class EndpointApiTests
    {
        private static ISocketOption BuildUdpOption(int port, bool useIpv6 = false, bool dualMode = false)
        {
            var builder = SocketOptionBuilder.Instance
                .SetSocket(SAEASocketType.Udp)
                .UsePacket()
                .SetIP(useIpv6 ? string.Empty : "127.0.0.1")
                .SetPort(port)
                .SetActionTimeOut(10 * 1000)
                .SetFreeTime(60 * 1000);

            if (useIpv6) builder.UseIPv6();
            if (dualMode) builder.UseDualMode();

            return builder.Build();
        }

        private static Socket CreateBoundClient()
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            socket.ReceiveTimeout = 5000;
            return socket;
        }

        private static int GetFreeUdpPort()
        {
            using (var probe = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                probe.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                return ((IPEndPoint)probe.LocalEndPoint).Port;
            }
        }

        private static int GetFreeTcpPort()
        {
            using (var probe = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                probe.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                return ((IPEndPoint)probe.LocalEndPoint).Port;
            }
        }

        private static byte[] ReceiveBytes(Socket socket, int length)
        {
            var buffer = new byte[length];
            var read = socket.Receive(buffer);
            Assert.AreEqual(length, read, "收到的字节数与发送不一致");
            return buffer;
        }

        /// <summary>
        /// 同一服务端向两个不同端口的客户端各发一次，两端都收到自己的内容
        /// </summary>
        [TestMethod]
        public void SendToAsync_不同端口两次发送_两端各自收到()
        {
            using (var server = new SaeaUdpServerSocket(BuildUdpOption(0)))
            using (var clientA = CreateBoundClient())
            using (var clientB = CreateBoundClient())
            {
                server.Start();

                var first = Encoding.UTF8.GetBytes("alpha");
                var second = Encoding.UTF8.GetBytes("beta");

                var sendA = server.SendToAsync((IPEndPoint)clientA.LocalEndPoint, first);
                var sendB = server.SendToAsync((IPEndPoint)clientB.LocalEndPoint, second);

                Assert.IsTrue(Task.WaitAll(new[] { sendA, sendB }, 5000), "SendToAsync 未在超时前完成");

                CollectionAssert.AreEqual(first, ReceiveBytes(clientA, first.Length));
                CollectionAssert.AreEqual(second, ReceiveBytes(clientB, second.Length));
            }
        }

        /// <summary>
        /// 未启动时本地端点为 null，启动后端口与配置一致
        /// </summary>
        [TestMethod]
        public void LocalEndPoint_Start后_等于配置端口()
        {
            var port = GetFreeUdpPort();
            using (var server = new SaeaUdpServerSocket(BuildUdpOption(port)))
            {
                Assert.IsNull(server.LocalEndPoint, "未启动时本地端点应为 null");

                server.Start();

                Assert.IsNotNull(server.LocalEndPoint);
                Assert.AreEqual(port, server.LocalEndPoint.Port);
                Assert.AreEqual(IPAddress.Loopback, server.LocalEndPoint.Address);
            }
        }

        /// <summary>
        /// 回调收到的来源端点等于发送端真实的本地端点，数据内容与发送一致
        /// </summary>
        [TestMethod]
        public void OnServerReceiveSpanFrom_收到报文_地址为发送端真实端点()
        {
            using (var server = new SaeaUdpServerSocket(BuildUdpOption(0)))
            using (var client = CreateBoundClient())
            {
                var received = new ManualResetEventSlim(false);
                IPEndPoint from = null;
                byte[] payload = null;

                server.OnServerReceiveSpanFrom += (remoteEndPoint, data) =>
                {
                    from = new IPEndPoint(remoteEndPoint.Address, remoteEndPoint.Port);
                    payload = data.ToArray();
                    received.Set();
                };

                server.Start();

                var sender = new IPEndPoint(IPAddress.Loopback, ((IPEndPoint)client.LocalEndPoint).Port);
                var message = Encoding.UTF8.GetBytes("ping");
                client.SendTo(message, server.LocalEndPoint);

                Assert.IsTrue(received.Wait(5000), "未在超时前收到报文");
                CollectionAssert.AreEqual(message, payload);
                Assert.AreEqual(sender, from);
            }
        }

        /// <summary>
        /// 回调里修改传入的地址对象不会污染下一次回调：每次回调拿到的是当次报文来源的独立副本
        /// </summary>
        [TestMethod]
        public void OnServerReceiveSpanFrom_回调后修改地址对象_不影响已复制结果()
        {
            using (var server = new SaeaUdpServerSocket(BuildUdpOption(0)))
            using (var client = CreateBoundClient())
            {
                var senderPort = ((IPEndPoint)client.LocalEndPoint).Port;
                var seenPorts = new List<int>();
                var countdown = new CountdownEvent(2);

                server.OnServerReceiveSpanFrom += (remoteEndPoint, data) =>
                {
                    lock (seenPorts)
                    {
                        seenPorts.Add(remoteEndPoint.Port);
                    }
                    // 模拟回调方误改地址对象：若不复制而直接暴露底层复用的端点，下一次回调的端口会被改成 1
                    remoteEndPoint.Port = 1;
                    countdown.Signal();
                };

                server.Start();

                client.SendTo(Encoding.UTF8.GetBytes("one"), server.LocalEndPoint);
                client.SendTo(Encoding.UTF8.GetBytes("two"), server.LocalEndPoint);

                Assert.IsTrue(countdown.Wait(5000), "未在超时前收到两条报文");
                CollectionAssert.AreEqual(new[] { senderPort, senderPort }, seenPorts);
            }
        }

        /// <summary>
        /// UseIPv6 配置下 UDP 服务端按配置建 IPv6 套接字，启动成功且本地端点为 IPv6Any
        /// </summary>
        [TestMethod]
        public void SaeaUdpServerSocket_UseIPV6_Start不抛异常且LocalEndPoint为IPv6()
        {
            using (var server = new SaeaUdpServerSocket(BuildUdpOption(0, true)))
            {
                server.Start();

                Assert.IsNotNull(server.LocalEndPoint);
                Assert.AreEqual(AddressFamily.InterNetworkV6, server.LocalEndPoint.AddressFamily);
                Assert.AreEqual(IPAddress.IPv6Any, server.LocalEndPoint.Address);
            }
        }

        /// <summary>
        /// 双栈 UDP 服务端收到 IPv4 报文时，来源地址是 v4 映射的 IPv6 形式
        /// </summary>
        [TestMethod]
        public void SaeaUdpServerSocket_UseIPV6双栈_收到IPv4报文来源为v4映射地址()
        {
            using (var server = new SaeaUdpServerSocket(BuildUdpOption(0, true, true)))
            using (var client = CreateBoundClient())
            {
                var received = new ManualResetEventSlim(false);
                IPEndPoint from = null;
                byte[] payload = null;

                server.OnServerReceiveSpanFrom += (remoteEndPoint, data) =>
                {
                    from = new IPEndPoint(remoteEndPoint.Address, remoteEndPoint.Port);
                    payload = data.ToArray();
                    received.Set();
                };

                server.Start();

                var message = Encoding.UTF8.GetBytes("v4-in-v6");
                client.SendTo(message, new IPEndPoint(IPAddress.Loopback, server.LocalEndPoint.Port));

                Assert.IsTrue(received.Wait(5000), "未在超时前收到 IPv4 报文");
                CollectionAssert.AreEqual(message, payload);
                Assert.AreEqual(AddressFamily.InterNetworkV6, from.AddressFamily);
                Assert.IsTrue(from.Address.IsIPv4MappedToIPv6, "双栈收到的 IPv4 来源应为 v4 映射地址");
            }
        }

        /// <summary>
        /// 双栈 TCP 监听可以接受 IPv4 客户端连接
        /// </summary>
        [TestMethod]
        public void SaeaServerSocket_UseDualMode_IPv4客户端可连接()
        {
            var port = GetFreeTcpPort();
            var option = SocketOptionBuilder.Instance
                .SetSocket(SAEASocketType.Tcp)
                .UsePacket()
                .UseIPv6()
                .UseDualMode()
                .SetPort(port)
                .SetActionTimeOut(10 * 1000)
                .SetFreeTime(60 * 1000)
                .Build();

            using (var server = new SaeaServerSocket(option))
            using (var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                var accepted = new ManualResetEventSlim(false);
                server.OnAccepted += _ => accepted.Set();

                server.Start();
                client.Connect(new IPEndPoint(IPAddress.Loopback, port));

                Assert.IsTrue(accepted.Wait(5000), "双栈监听未接受 IPv4 连接");
            }
        }
    }
}