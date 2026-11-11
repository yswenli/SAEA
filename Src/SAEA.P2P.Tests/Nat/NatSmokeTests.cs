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
*文件名： NatSmokeTests
*版本号： net10.0
*唯一标识：19e49763-1cc7-4d0d-90c3-9fdfc3828817
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 14:40:00
*描述：阶段 0 冒烟——两台虚拟主机经 NAT 代理双向收发
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 14:40:00
*修改人： yswenli
*版本号： net10.0
*描述：新增阶段 0 冒烟用例
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
    /// 阶段 0 冒烟：两台虚拟主机经同一 NAT 代理互发不同长度的报文，验证映射、hairpin 与地址头改写。
    /// </summary>
    [TestClass]
    public class NatSmokeTests
    {
        /// <summary>
        /// NAT 对外地址
        /// </summary>
        private static readonly IPAddress NatAddress = IPAddress.Parse("127.0.0.10");

        /// <summary>
        /// 内部主机 A
        /// </summary>
        private static readonly IPAddress HostA = IPAddress.Parse("127.0.0.2");

        /// <summary>
        /// 内部主机 B
        /// </summary>
        private static readonly IPAddress HostB = IPAddress.Parse("127.0.0.3");

        /// <summary>
        /// 外部对端，仅用于让双方建立映射并取得各自的外部端点
        /// </summary>
        private static readonly IPAddress ExternalPeer = IPAddress.Parse("127.0.0.20");

        /// <summary>
        /// 大报文长度。单条 UDP 载荷上限为 65507，内部通道还要加 6 字节地址头，
        /// 这里留出余量取 65000，既接近上限又不踩边界。
        /// </summary>
        private const int NearMaxPayload = 65000;

        /// <summary>
        /// 双向各发 0 字节、1 字节与接近上限三种长度，断言内容、来源端点与映射条目
        /// </summary>
        [TestMethod]
        public async Task NatSmoke_两个虚拟主机经NAT代理_双向收发()
        {
            using (var nat = new NatBox(NatAddress, NatPreset.FullCone()))
            using (var hostA = new SmokeHost(HostA))
            using (var hostB = new SmokeHost(HostB))
            using (var peer = new SmokeHost(ExternalPeer, false))
            {
                // 双方各自向外部打一次，等价于真实流程里的 STUN 探测：NAT 借此建立映射，
                // 双方也由此得到各自的外部端点
                hostA.SendTo(nat.InternalGateway, peer.LocalEndPoint, new byte[] { 0xA1 });
                var probeA = await peer.ReceiveAsync(3000);
                Assert.IsNotNull(probeA, "A 的探测报文未到达外部对端");

                hostB.SendTo(nat.InternalGateway, peer.LocalEndPoint, new byte[] { 0xB1 });
                var probeB = await peer.ReceiveAsync(3000);
                Assert.IsNotNull(probeB, "B 的探测报文未到达外部对端");

                var externalA = probeA.Remote;
                var externalB = probeB.Remote;
                Assert.AreNotEqual(externalA.Port, externalB.Port, "A 与 B 的外部映射端口应不同");

                var lengths = new[] { 0, 1, NearMaxPayload };

                // A -> NAT -> B
                foreach (var length in lengths)
                {
                    var payload = BuildPayload(length);
                    hostA.SendTo(nat.InternalGateway, externalB, payload);

                    var received = await hostB.ReceiveAsync(5000);
                    Assert.IsNotNull(received, $"B 未收到长度 {length} 的报文");
                    CollectionAssert.AreEqual(payload, received.Payload, $"B 收到的长度 {length} 报文内容不一致");
                    Assert.AreEqual(externalA, received.Remote, "B 看到的来源应为 A 的外部端点");
                }

                // B -> NAT -> A
                foreach (var length in lengths)
                {
                    var payload = BuildPayload(length);
                    hostB.SendTo(nat.InternalGateway, externalA, payload);

                    var received = await hostA.ReceiveAsync(5000);
                    Assert.IsNotNull(received, $"A 未收到长度 {length} 的报文");
                    CollectionAssert.AreEqual(payload, received.Payload, $"A 收到的长度 {length} 报文内容不一致");
                    Assert.AreEqual(externalB, received.Remote, "A 看到的来源应为 B 的外部端点");
                }

                // 映射表只应保留 A、B 两条，且外部端点与探测结果一致
                Assert.AreEqual(2, nat.Mappings.Count, "只应存在 A 与 B 两条映射");

                NatMapping mappingA;
                NatMapping mappingB;
                Assert.IsTrue(nat.TryGetMapping(hostA.LocalEndPoint, out mappingA), "未找到 A 的映射");
                Assert.IsTrue(nat.TryGetMapping(hostB.LocalEndPoint, out mappingB), "未找到 B 的映射");
                Assert.AreEqual(externalA, mappingA.ExternalEndPoint, "A 的映射外部端点与探测结果不一致");
                Assert.AreEqual(externalB, mappingB.ExternalEndPoint, "B 的映射外部端点与探测结果不一致");
            }
        }

        /// <summary>
        /// 生成指定长度、内容可区分的载荷
        /// </summary>
        /// <param name="length">长度</param>
        /// <returns>载荷</returns>
        private static byte[] BuildPayload(int length)
        {
            var payload = new byte[length];
            for (var i = 0; i < length; i++) payload[i] = (byte)(i * 31 + length);
            return payload;
        }

        /// <summary>
        /// 一次接收结果。
        /// </summary>
        private sealed class Received
        {
            /// <summary>
            /// 创建接收结果
            /// </summary>
            /// <param name="remote">来源端点</param>
            /// <param name="payload">载荷</param>
            public Received(IPEndPoint remote, byte[] payload)
            {
                Remote = remote;
                Payload = payload;
            }

            /// <summary>
            /// 来源端点
            /// </summary>
            public IPEndPoint Remote { get; }

            /// <summary>
            /// 载荷
            /// </summary>
            public byte[] Payload { get; }
        }

        /// <summary>
        /// 冒烟用的虚拟主机端点：内部侧按 NAT 地址头收发，外部侧收原始报文。
        /// </summary>
        private sealed class SmokeHost : IDisposable
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
            /// 是否按 NAT 内部报文格式解码地址头
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
            public SmokeHost(IPAddress address, bool decodeHeader = true)
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
            /// 按内部报文格式向 NAT 网关发送
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
            /// 接收循环
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
    }
}