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
*命名空间：SAEA.P2P.Tests.Nat
*文件名： NatBoxTests
*版本号： net10.0
*唯一标识：96a32e51-3e2f-4268-a96b-af80a912f914
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 14:10:00
*描述：NAT 代理模拟器的端口保持、过滤、hairpin、超时与 TCP 放行验收
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 14:10:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 NAT 代理模拟器用例
*
*****************************************************************************/
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SAEA.P2P.Tests.Nat
{
    /// <summary>
    /// NAT 代理模拟器：只验证代理自身的行为判定，不涉及被测 ICE 实现。
    /// </summary>
    [TestCategory(P2PTestSuite.Nat)]
    [TestClass]
    public class NatBoxTests
    {
        /// <summary>
        /// 对外地址，所有用例统一使用，避免与虚拟主机地址冲突
        /// </summary>
        private static readonly IPAddress NatAddress = IPAddress.Parse("127.0.0.10");

        /// <summary>
        /// 内部主机 A 的地址
        /// </summary>
        private static readonly IPAddress HostA = IPAddress.Parse("127.0.0.2");

        /// <summary>
        /// 内部主机 B 的地址
        /// </summary>
        private static readonly IPAddress HostB = IPAddress.Parse("127.0.0.3");

        /// <summary>
        /// 公网侧目标地址
        /// </summary>
        private static readonly IPAddress ExternalTarget = IPAddress.Parse("127.0.0.20");

        /// <summary>
        /// 公网侧另一台主机地址，用于未授权来源
        /// </summary>
        private static readonly IPAddress ExternalOther = IPAddress.Parse("127.0.0.21");

        /// <summary>
        /// 端口保持开启时，外部端口应与内部端口一致
        /// </summary>
        [TestMethod]
        public async Task NatBox_端口保持_出入端口一致()
        {
            using (var nat = new NatBox(NatAddress, NatPreset.FullCone()))
            using (var host = new UdpHost(HostA))
            using (var target = new UdpHost(ExternalTarget, false))
            {
                host.SendTo(nat.InternalGateway, target.LocalEndPoint, new byte[] { 1, 2, 3 });

                var received = await target.ReceiveAsync(3000);
                Assert.IsNotNull(received, "目标主机未收到经 NAT 转发的报文");

                NatMapping mapping;
                Assert.IsTrue(nat.TryGetMapping(host.LocalEndPoint, out mapping), "未找到内部端点的映射");
                Assert.AreEqual(host.LocalEndPoint.Port, mapping.ExternalEndPoint.Port, "端口保持开启时外部端口应等于内部端口");
                Assert.AreEqual(mapping.ExternalEndPoint, received.Remote, "目标主机看到的来源应为 NAT 外部端点");
            }
        }

        /// <summary>
        /// 地址端口相关过滤下，未被内部主机访问过的来源会被丢弃，已访问来源可回包
        /// </summary>
        [TestMethod]
        public async Task NatBox_APDF_未授权来源被丢弃()
        {
            using (var nat = new NatBox(NatAddress, NatPreset.PortRestricted()))
            using (var host = new UdpHost(HostA))
            using (var authorized = new UdpHost(ExternalTarget, false))
            using (var stranger = new UdpHost(ExternalOther, false))
            {
                host.SendTo(nat.InternalGateway, authorized.LocalEndPoint, new byte[] { 9 });
                var firstArrival = await authorized.ReceiveAsync(3000);
                Assert.IsNotNull(firstArrival, "授权目标未收到报文");

                NatMapping mapping;
                Assert.IsTrue(nat.TryGetMapping(host.LocalEndPoint, out mapping), "未找到内部端点的映射");

                stranger.SendRawTo(mapping.ExternalEndPoint, new byte[] { 0xEE });
                var intruded = await host.ReceiveAsync(600);
                Assert.IsNull(intruded, "未授权来源的报文不应穿过 APDF 过滤");

                authorized.SendRawTo(mapping.ExternalEndPoint, new byte[] { 0xAB, 0xCD });
                var replied = await host.ReceiveAsync(3000);
                Assert.IsNotNull(replied, "已授权来源的回包应能穿透过滤");
                CollectionAssert.AreEqual(new byte[] { 0xAB, 0xCD }, replied.Payload);
                Assert.AreEqual(authorized.LocalEndPoint, replied.Remote, "回程报文应还原为真实来源端点");
            }
        }

        /// <summary>
        /// hairpin 开启时，同一 NAT 后的两台主机可通过外部映射互访
        /// </summary>
        [TestMethod]
        public async Task NatBox_hairpin_内部互访可达()
        {
            using (var nat = new NatBox(NatAddress, NatPreset.FullCone()))
            using (var hostA = new UdpHost(HostA))
            using (var hostB = new UdpHost(HostB))
            using (var target = new UdpHost(ExternalTarget, false))
            {
                // 先让 B 出向一次，NAT 才知道 B 的外部映射
                hostB.SendTo(nat.InternalGateway, target.LocalEndPoint, new byte[] { 0x01 });
                Assert.IsNotNull(await target.ReceiveAsync(3000), "B 的出向报文未到达目标");

                NatMapping mappingB;
                Assert.IsTrue(nat.TryGetMapping(hostB.LocalEndPoint, out mappingB), "未找到 B 的映射");

                var payload = new byte[] { 0x10, 0x20, 0x30, 0x40 };
                hostA.SendTo(nat.InternalGateway, mappingB.ExternalEndPoint, payload);

                var received = await hostB.ReceiveAsync(3000);
                Assert.IsNotNull(received, "hairpin 未把报文回环给 B");
                CollectionAssert.AreEqual(payload, received.Payload);

                NatMapping mappingA;
                Assert.IsTrue(nat.TryGetMapping(hostA.LocalEndPoint, out mappingA), "未找到 A 的映射");
                Assert.AreEqual(mappingA.ExternalEndPoint, received.Remote, "B 看到的来源应为 A 的外部映射");
            }
        }

        /// <summary>
        /// 映射空闲超过时限后被清理，外部端口随之释放
        /// </summary>
        [TestMethod]
        public async Task NatBox_映射空闲超时_被清理()
        {
            var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var config = NatPreset.FullCone();
            config.MappingLifetime = TimeSpan.FromSeconds(30);

            using (var nat = new NatBox(NatAddress, config, () => now))
            using (var host = new UdpHost(HostA))
            using (var target = new UdpHost(ExternalTarget, false))
            {
                host.SendTo(nat.InternalGateway, target.LocalEndPoint, new byte[] { 0x55 });
                Assert.IsNotNull(await target.ReceiveAsync(3000), "出向报文未到达目标");
                Assert.AreEqual(1, nat.Mappings.Count, "应存在一条映射");
                Assert.AreEqual(0, nat.PurgeExpiredMappings(), "未超时的映射不应被清理");

                now = now.AddMinutes(5);
                Assert.AreEqual(1, nat.PurgeExpiredMappings(), "超时映射应被清理");
                Assert.AreEqual(0, nat.Mappings.Count, "清理后映射表应为空");
            }
        }

        /// <summary>
        /// 未建立静态映射时外部无法接入，建立后可正常连通
        /// </summary>
        [TestMethod]
        public async Task NatBox_TCP_未放行方向连接被拒()
        {
            using (var nat = new NatBox(NatAddress, NatPreset.TcpMapped()))
            using (var server = new TcpHost(HostB))
            {
                var refused = false;
                try
                {
                    using (var probe = new TcpClient())
                    {
                        await probe.ConnectAsync(NatAddress, server.LocalEndPoint.Port);
                    }
                }
                catch (SocketException)
                {
                    refused = true;
                }
                Assert.IsTrue(refused, "未建立静态映射时外部连接应被拒绝");

                var mapping = nat.AddStaticTcpMapping(server.LocalEndPoint);
                using (var client = new TcpClient())
                {
                    await client.ConnectAsync(mapping.ExternalEndPoint);

                    var accepted = await server.AcceptAsync(3000);
                    Assert.IsNotNull(accepted, "静态映射未把外部连接转发到内部服务");

                    var payload = new byte[] { 0x61, 0x62, 0x63 };
                    await client.GetStream().WriteAsync(payload, 0, payload.Length);
                    CollectionAssert.AreEqual(payload, await accepted.GetStream().ReadExactAsync(payload.Length));
                }
            }
        }

        /// <summary>
        /// 一次接收结果：还原后的来源端点与载荷。
        /// </summary>
        private sealed class Received
        {
            /// <summary>
            /// 创建接收结果
            /// </summary>
            /// <param name="remote">真实来源端点</param>
            /// <param name="payload">载荷</param>
            public Received(IPEndPoint remote, byte[] payload)
            {
                Remote = remote;
                Payload = payload;
            }

            /// <summary>
            /// 真实来源端点
            /// </summary>
            public IPEndPoint Remote { get; }

            /// <summary>
            /// 载荷
            /// </summary>
            public byte[] Payload { get; }
        }

        /// <summary>
        /// 宿主在虚拟地址上的 UDP 端点，按 NAT 内部报文格式收发。
        /// </summary>
        private sealed class UdpHost : IDisposable
        {
            /// <summary>
            /// 接收结果队列
            /// </summary>
            private readonly System.Threading.Channels.Channel<Received> _received = System.Threading.Channels.Channel.CreateUnbounded<Received>();

            /// <summary>
            /// 关闭接收循环的取消源
            /// </summary>
            private readonly CancellationTokenSource _cts = new CancellationTokenSource();

            /// <summary>
            /// 绑定套接字
            /// </summary>
            private readonly Socket _socket;

            /// <summary>
            /// 是否按 NAT 内部报文格式解码地址头；公网侧目标为 false
            /// </summary>
            private readonly bool _decodeHeader;

            /// <summary>
            /// 接收循环任务
            /// </summary>
            private readonly Task _loop;

            /// <summary>
            /// 绑定到指定地址的随机端口并启动接收
            /// </summary>
            /// <param name="address">虚拟主机地址</param>
            /// <param name="decodeHeader">true 表示按 NAT 内部报文格式解码地址头</param>
            public UdpHost(IPAddress address, bool decodeHeader = true)
            {
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _socket.Bind(new IPEndPoint(address, 0));
                LocalEndPoint = (IPEndPoint)_socket.LocalEndPoint;
                _decodeHeader = decodeHeader;
                _loop = Task.Run(() => LoopAsync(_cts.Token));
            }

            /// <summary>
            /// 本机端点
            /// </summary>
            public IPEndPoint LocalEndPoint { get; }

            /// <summary>
            /// 按内部报文格式向 NAT 网关发送报文
            /// </summary>
            /// <param name="gateway">NAT 内部网关</param>
            /// <param name="destination">报文声明的目标端点</param>
            /// <param name="payload">载荷</param>
            public void SendTo(IPEndPoint gateway, IPEndPoint destination, byte[] payload)
            {
                var packet = NatPacket.Encode(destination, payload);
                _socket.SendTo(packet, gateway);
            }

            /// <summary>
            /// 发送不带 NAT 地址头的原始报文，模拟公网侧直接发包
            /// </summary>
            /// <param name="remote">目标端点</param>
            /// <param name="payload">载荷</param>
            public void SendRawTo(IPEndPoint remote, byte[] payload)
            {
                _socket.SendTo(payload, remote);
            }

            /// <summary>
            /// 等待一条报文，超时返回 null
            /// </summary>
            /// <param name="timeoutMs">超时毫秒</param>
            /// <returns>接收结果或 null</returns>
            public async Task<Received> ReceiveAsync(int timeoutMs)
            {
                using (var timeout = new CancellationTokenSource(timeoutMs))
                {
                    try
                    {
                        return await _received.Reader.ReadAsync(timeout.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return null;
                    }
                }
            }

            /// <summary>
            /// 释放套接字并结束接收循环
            /// </summary>
            public void Dispose()
            {
                try { _cts.Cancel(); } catch { }
                try { _socket.Dispose(); } catch { }
                try { _loop.Wait(1000); } catch { }
                _cts.Dispose();
            }

            /// <summary>
            /// 接收循环：解码 NAT 地址头后入队
            /// </summary>
            /// <param name="cancellationToken">关闭令牌</param>
            /// <returns>循环结束的任务</returns>
            private async Task LoopAsync(CancellationToken cancellationToken)
            {
                var buffer = new byte[65535];
                EndPoint any = new IPEndPoint(IPAddress.Any, 0);

                while (!cancellationToken.IsCancellationRequested)
                {
                    SocketReceiveFromResult result;
                    try
                    {
                        result = await _socket.ReceiveFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None, any, cancellationToken);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (ObjectDisposedException) { break; }
                    catch (SocketException) { continue; }

                    IPEndPoint remote;
                    byte[] copy;

                    if (_decodeHeader)
                    {
                        ArraySegment<byte> payload;
                        if (!NatPacket.TryDecode(buffer, 0, result.ReceivedBytes, out remote, out payload)) continue;

                        copy = new byte[payload.Count];
                        Buffer.BlockCopy(payload.Array, payload.Offset, copy, 0, payload.Count);
                    }
                    else
                    {
                        remote = (IPEndPoint)result.RemoteEndPoint;
                        copy = new byte[result.ReceivedBytes];
                        Buffer.BlockCopy(buffer, 0, copy, 0, result.ReceivedBytes);
                    }

                    _received.Writer.TryWrite(new Received(remote, copy));
                }
            }
        }

        /// <summary>
        /// 宿主在虚拟地址上的 TCP 服务端，用于静态映射转发验收。
        /// </summary>
        private sealed class TcpHost : IDisposable
        {
            /// <summary>
            /// 监听器
            /// </summary>
            private readonly TcpListener _listener;

            /// <summary>
            /// 在指定地址的随机端口上开始监听
            /// </summary>
            /// <param name="address">虚拟主机地址</param>
            public TcpHost(IPAddress address)
            {
                _listener = new TcpListener(address, 0);
                _listener.Start(8);
                LocalEndPoint = (IPEndPoint)_listener.LocalEndpoint;
            }

            /// <summary>
            /// 监听端点
            /// </summary>
            public IPEndPoint LocalEndPoint { get; }

            /// <summary>
            /// 等待一条入向连接，超时返回 null
            /// </summary>
            /// <param name="timeoutMs">超时毫秒</param>
            /// <returns>已连接的客户端或 null</returns>
            public async Task<TcpClient> AcceptAsync(int timeoutMs)
            {
                using (var timeout = new CancellationTokenSource(timeoutMs))
                {
                    try
                    {
                        return await _listener.AcceptTcpClientAsync(timeout.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return null;
                    }
                }
            }

            /// <summary>
            /// 停止监听
            /// </summary>
            public void Dispose()
            {
                try { _listener.Stop(); } catch { }
            }
        }
    }

    /// <summary>
    /// 流读取辅助。
    /// </summary>
    internal static class StreamReadExtensions
    {
        /// <summary>
        /// 从流里读满指定字节数，读不满视为失败
        /// </summary>
        /// <param name="stream">字节流</param>
        /// <param name="length">期望读取的字节数</param>
        /// <returns>读到的字节</returns>
        public static async Task<byte[]> ReadExactAsync(this System.IO.Stream stream, int length)
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
    }
}