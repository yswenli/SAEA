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
*命名空间：SAEA.P2P.Tests.Stun
*文件名： StunNatIntegrationTests
*版本号： net10.0
*唯一标识：eb4ea3e7-e8e4-4135-992f-be227f515396
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 22:00:00
*描述：经用户态 NAT 代理的 STUN 映射与换端口集成验收
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 22:00:00
*修改人： yswenli
*版本号： net10.0
*描述：新增经 NAT 代理的 STUN 集成用例
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Stun;
using SAEA.P2P.Tests.Nat;
using SAEA.P2P.Transport;

using StunServerHost = SAEA.P2P.StunServer.StunServer;
using StunServerOptions = SAEA.P2P.StunServer.StunServerOptions;

namespace SAEA.P2P.Tests.Stun
{
    /// <summary>
    /// 经 NAT 代理的 STUN 集成：客户端在 NAT 后、服务端在公网侧地址，
    /// 验证映射地址等于 NAT 外部端点，以及 CHANGE-REQUEST 换端口后仍能收到响应。
    /// </summary>
    [TestCategory(P2PTestSuite.Nat)]
    [TestClass]
    public class StunNatIntegrationTests
    {
        /// <summary>
        /// NAT 对外地址
        /// </summary>
        private static readonly IPAddress NatAddress = IPAddress.Parse("127.0.0.10");

        /// <summary>
        /// NAT 后的内部客户端地址
        /// </summary>
        private static readonly IPAddress InternalHost = IPAddress.Parse("127.0.0.2");

        /// <summary>
        /// 公网侧 STUN 服务端地址
        /// </summary>
        private static readonly IPAddress PublicServerAddress = IPAddress.Parse("127.0.0.20");

        /// <summary>
        /// 经全锥形 NAT 发出 Binding 请求，映射地址应为 NAT 外部端点，且与映射表一致
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public async Task StunNatIntegration_经NAT_映射地址为NAT外部端点()
        {
            using (var nat = new NatBox(NatAddress, NatPreset.FullCone()))
            using (var server = new StunServerHost(new StunServerOptions(new IPEndPoint(PublicServerAddress, 0))))
            using (var transport = new NatClientTransport(new IPEndPoint(InternalHost, 0), nat.InternalGateway))
            using (var client = new StunClient(transport))
            {
                await server.StartAsync();
                var serverEndPoint = server.Endpoints[0].LocalEndPoint;

                var result = await client.BindingAsync(serverEndPoint);

                Assert.IsTrue(result.Succeeded, "经 NAT 的 Binding 请求应成功");
                Assert.IsNotNull(result.MappedAddress, "应解析出映射地址");

                NatMapping mapping;
                Assert.IsTrue(nat.TryGetMapping(transport.LocalEndPoint, out mapping), "NAT 应建立内部端点的映射");
                Assert.AreEqual(mapping.ExternalEndPoint, result.MappedAddress, "映射地址应为 NAT 外部端点");
                Assert.AreEqual(NatAddress, result.MappedAddress.Address, "映射地址的地址部分应为 NAT 对外地址");
                Assert.AreEqual(serverEndPoint, result.ResponseOrigin, "响应来源应为 STUN 服务端端点");
            }
        }

        /// <summary>
        /// 带 CHANGE-REQUEST 换端口的请求经 NAT 发出后，响应应从备用端口回到客户端
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public async Task StunNatIntegration_CHANGE_REQUEST换端口_客户端收到响应()
        {
            var ports = ReserveUdpPorts(2);
            var primary = new IPEndPoint(PublicServerAddress, ports[0]);
            var alternate = new IPEndPoint(PublicServerAddress, ports[1]);

            using (var nat = new NatBox(NatAddress, NatPreset.FullCone()))
            using (var server = new StunServerHost(new StunServerOptions(primary, alternate)))
            using (var transport = new NatClientTransport(new IPEndPoint(InternalHost, 0), nat.InternalGateway))
            {
                await server.StartAsync();

                var request = new StunMessage(StunMessageType.Binding, StunClass.Request);
                request.SetAttributes(new[]
                {
                    StunAttributeCodec.CreateUInt32(StunAttributeType.ChangeRequest, 0x02)
                });

                var response = await ExchangeRawAsync(transport, primary, request, TimeSpan.FromSeconds(5));

                Assert.IsNotNull(response, "客户端应收到换端口后的响应");
                Assert.AreEqual(StunClass.SuccessResponse, response.Class, "换端口请求应成功");

                IPEndPoint responseOrigin;
                Assert.IsTrue(
                    TryDecodeAddress(response, StunAttributeType.ResponseOrigin, out responseOrigin),
                    "响应应携带 RESPONSE-ORIGIN");
                Assert.AreEqual(alternate, responseOrigin, "响应应从备用端口发出");

                NatMapping mapping;
                Assert.IsTrue(nat.TryGetMapping(transport.LocalEndPoint, out mapping), "换端口后 NAT 映射应仍然可用");
            }
        }

        /// <summary>
        /// 用原始 STUN 报文经传输发出并等待第一条可解析应答；超时返回 null
        /// </summary>
        /// <param name="transport">客户端传输</param>
        /// <param name="target">目标端点</param>
        /// <param name="request">请求</param>
        /// <param name="timeout">等待上限</param>
        /// <returns>解析后的应答</returns>
        private static async Task<StunMessage> ExchangeRawAsync(IUdpTransport transport, IPEndPoint target, StunMessage request, TimeSpan timeout)
        {
            var completion = new TaskCompletionSource<StunMessage>();
            Action<IPEndPoint, ReadOnlyMemory<byte>> handler = (from, data) =>
            {
                StunMessage message;
                StunParseError error;
                if (StunMessage.TryParse(data.Span, out message, out error))
                {
                    completion.TrySetResult(message);
                }
            };

            transport.DatagramReceived += handler;
            try
            {
                await transport.SendToAsync(target, new ReadOnlyMemory<byte>(request.ToArray()), CancellationToken.None);
                var completed = await Task.WhenAny(completion.Task, Task.Delay(timeout));
                if (completed != completion.Task)
                {
                    return null;
                }

                return await completion.Task;
            }
            finally
            {
                transport.DatagramReceived -= handler;
            }
        }

        /// <summary>
        /// 解码地址属性；缺失或格式错误时返回 false
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="type">属性类型</param>
        /// <param name="endPoint">解码出的端点</param>
        /// <returns>解析成功时为 true</returns>
        private static bool TryDecodeAddress(StunMessage message, StunAttributeType type, out IPEndPoint endPoint)
        {
            endPoint = null;
            StunAttribute attribute;
            if (!message.TryGet(type, out attribute))
            {
                return false;
            }

            return StunAddressCodec.TryDecodeAddress(attribute, message.TransactionId.Span, out endPoint);
        }

        /// <summary>
        /// 预占若干个尚未使用的 UDP 端口；同时持有套接字可避免返回重复端口
        /// </summary>
        /// <param name="count">端口数量</param>
        /// <returns>端口数组</returns>
        private static int[] ReserveUdpPorts(int count)
        {
            var probes = new List<IUdpTransport>();
            try
            {
                var ports = new int[count];
                for (var i = 0; i < count; i++)
                {
                    var probe = new SaeaUdpTransport();
                    probe.BindAsync(new IPEndPoint(PublicServerAddress, 0), CancellationToken.None).GetAwaiter().GetResult();
                    probes.Add(probe);
                    ports[i] = probe.LocalEndPoint.Port;
                }
                return ports;
            }
            finally
            {
                for (var i = 0; i < probes.Count; i++)
                {
                    probes[i].Dispose();
                }
            }
        }

        /// <summary>
        /// NAT 内部主机的传输适配器：出向加 6 字节地址头发往 NAT 网关，入向按地址头还原真实来源。
        /// </summary>
        private sealed class NatClientTransport : IUdpTransport
        {
            /// <summary>
            /// 承载内部主机地址的 UDP 传输
            /// </summary>
            private readonly SaeaUdpTransport _inner = new SaeaUdpTransport();

            /// <summary>
            /// NAT 内部网关端点
            /// </summary>
            private readonly IPEndPoint _gateway;

            /// <summary>
            /// 绑定内部主机地址并订阅底层接收事件
            /// </summary>
            /// <param name="localEndPoint">内部主机端点，端口为 0 时由系统分配</param>
            /// <param name="gateway">NAT 内部网关</param>
            public NatClientTransport(IPEndPoint localEndPoint, IPEndPoint gateway)
            {
                if (localEndPoint == null) throw new ArgumentNullException(nameof(localEndPoint));
                if (gateway == null) throw new ArgumentNullException(nameof(gateway));

                _gateway = gateway;
                _inner.BindAsync(localEndPoint, CancellationToken.None).GetAwaiter().GetResult();
                _inner.DatagramReceived += OnInnerDatagram;
            }

            /// <summary>
            /// 收到数据报时触发，来源为 NAT 还原后的真实远端端点
            /// </summary>
            public event Action<IPEndPoint, ReadOnlyMemory<byte>> DatagramReceived;

            /// <summary>
            /// 内部主机本地端点
            /// </summary>
            public IPEndPoint LocalEndPoint
            {
                get { return _inner.LocalEndPoint; }
            }

            /// <summary>
            /// 底层传输是否可用
            /// </summary>
            public bool IsConnected
            {
                get { return _inner.IsConnected; }
            }

            /// <summary>
            /// 转发绑定；适配器构造时已完成绑定，再次调用会由底层实现拒绝
            /// </summary>
            /// <param name="localEndPoint">本地端点</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>绑定完成的任务</returns>
            public Task BindAsync(IPEndPoint localEndPoint, CancellationToken cancellationToken)
            {
                return _inner.BindAsync(localEndPoint, cancellationToken);
            }

            /// <summary>
            /// 启动底层接收循环
            /// </summary>
            public void StartReceive()
            {
                _inner.StartReceive();
            }

            /// <summary>
            /// 把目标端点写进地址头后发往 NAT 网关
            /// </summary>
            /// <param name="remoteEndPoint">真实目标端点</param>
            /// <param name="data">原始载荷</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>发送完成的任务</returns>
            public Task SendToAsync(IPEndPoint remoteEndPoint, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
            {
                var packet = NatPacket.Encode(remoteEndPoint, data.Span);
                return _inner.SendToAsync(_gateway, new ReadOnlyMemory<byte>(packet), cancellationToken);
            }

            /// <summary>
            /// 退订并释放底层传输
            /// </summary>
            public void Dispose()
            {
                _inner.DatagramReceived -= OnInnerDatagram;
                _inner.Dispose();
            }

            /// <summary>
            /// 解析地址头后把载荷交给上层；地址头非法时丢弃
            /// </summary>
            /// <param name="from">NAT 网关端点</param>
            /// <param name="data">带地址头的报文</param>
            private void OnInnerDatagram(IPEndPoint from, ReadOnlyMemory<byte> data)
            {
                var buffer = data.ToArray();
                IPEndPoint source;
                ArraySegment<byte> payload;
                if (!NatPacket.TryDecode(buffer, 0, buffer.Length, out source, out payload))
                {
                    return;
                }

                var handler = DatagramReceived;
                if (handler != null)
                {
                    handler(source, new ReadOnlyMemory<byte>(payload.Array, payload.Offset, payload.Count));
                }
            }
        }
    }
}