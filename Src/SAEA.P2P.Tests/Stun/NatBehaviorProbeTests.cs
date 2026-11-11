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
*文件名： NatBehaviorProbeTests
*版本号： net10.0
*唯一标识：2dd53b03-8f1b-47ed-8598-dd7e2c925b44
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 21:30:00
*描述：NAT 映射与过滤行为探测验收
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 21:30:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 NAT 行为探测用例
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Stun;
using SAEA.P2P.Transport;

namespace SAEA.P2P.Tests.Stun
{
    /// <summary>
    /// NAT 行为探测：映射地址一致判 EIM、换端口与换地址都被拦判 APDF、无应答时返回 Unknown 且不抛异常。
    /// 服务端用用户态假实现，按目标端点返回固定映射地址，不依赖真实 NAT。
    /// </summary>
    [TestClass]
    public class NatBehaviorProbeTests
    {
        /// <summary>
        /// 主监听地址
        /// </summary>
        private static readonly IPAddress PrimaryAddress = IPAddress.Loopback;

        /// <summary>
        /// 备用监听地址
        /// </summary>
        private static readonly IPAddress AlternateAddress = IPAddress.Parse("127.0.0.2");

        /// <summary>
        /// 三处目标返回同一映射地址且都响应换地址换端口请求时，应判定为 EIM / EIF
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task NatBehaviorProbe_EIM_识别为EIM()
        {
            using (var primary = new FakeBehaviorServer(PrimaryAddress, 0, null, false))
            using (var alternateSamePort = new FakeBehaviorServer(AlternateAddress, primary.Port, null, false))
            using (var alternateOtherPort = new FakeBehaviorServer(AlternateAddress, 0, null, false))
            using (var transport = new SaeaUdpTransport())
            using (var probe = new NatBehaviorProbe(transport))
            {
                primary.OtherAddress = new IPEndPoint(AlternateAddress, alternateOtherPort.Port);

                await transport.BindAsync(new IPEndPoint(PrimaryAddress, 0), CancellationToken.None);

                var info = await probe.ProbeAsync(primary.EndPoint);

                Assert.AreEqual(NatBehaviorKind.EndpointIndependent, info.Mapping, "三处目标看到的映射地址一致，应判定为 EIM");
                Assert.AreEqual(NatBehaviorKind.EndpointIndependent, info.Filtering, "换地址换端口仍能收到应答，应判定为 EIF");
                Assert.AreEqual(transport.LocalEndPoint, info.MappedAddress, "映射地址应为客户端本地端点");
                Assert.AreEqual(primary.EndPoint, info.PrimaryServer, "应记录主服务端端点");
                Assert.IsNotNull(info.AlternateServer, "应记录备用端点");
            }
        }

        /// <summary>
        /// 映射地址随目标变化，且换地址与换端口的请求都被丢弃时，应判定为 APDM / APDF
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task NatBehaviorProbe_APDF_识别为APDF()
        {
            var mapped1 = new IPEndPoint(IPAddress.Parse("203.0.113.1"), 40001);
            var mapped2 = new IPEndPoint(IPAddress.Parse("203.0.113.2"), 40002);
            var mapped3 = new IPEndPoint(IPAddress.Parse("203.0.113.3"), 40003);

            using (var primary = new FakeBehaviorServer(PrimaryAddress, 0, mapped1, true))
            using (var alternateSamePort = new FakeBehaviorServer(AlternateAddress, primary.Port, mapped2, true))
            using (var alternateOtherPort = new FakeBehaviorServer(AlternateAddress, 0, mapped3, true))
            using (var transport = new SaeaUdpTransport())
            using (var probe = new NatBehaviorProbe(transport))
            {
                primary.OtherAddress = new IPEndPoint(AlternateAddress, alternateOtherPort.Port);

                // 两次换地址换端口探测都会走到重传，压小 RTO 避免用例真等几十秒
                probe.RtoMilliseconds = 5;

                await transport.BindAsync(new IPEndPoint(PrimaryAddress, 0), CancellationToken.None);

                var info = await probe.ProbeAsync(primary.EndPoint);

                Assert.AreEqual(NatBehaviorKind.AddressAndPortDependent, info.Mapping, "映射随目标地址与端口一起变化，应判定为 APDM");
                Assert.AreEqual(NatBehaviorKind.AddressAndPortDependent, info.Filtering, "换地址与换端口的请求都收不到应答，应判定为 APDF");
            }
        }

        /// <summary>
        /// 服务端完全无应答时应返回 Unknown，不得抛异常
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task NatBehaviorProbe_探测超时_返回Unknown且不抛()
        {
            var silentPort = ReserveUdpPort();

            using (var transport = new SaeaUdpTransport())
            using (var probe = new NatBehaviorProbe(transport))
            {
                probe.RtoMilliseconds = 5;

                await transport.BindAsync(new IPEndPoint(PrimaryAddress, 0), CancellationToken.None);

                var info = await probe.ProbeAsync(new IPEndPoint(PrimaryAddress, silentPort));

                Assert.IsNotNull(info, "探测失败也应返回结果对象");
                Assert.AreEqual(NatBehaviorKind.Unknown, info.Mapping, "基线无应答时映射行为应为未知");
                Assert.AreEqual(NatBehaviorKind.Unknown, info.Filtering, "基线无应答时过滤行为应为未知");
                Assert.IsNull(info.MappedAddress, "基线失败不应带回映射地址");
                Assert.IsFalse(info.HasMappedAddress, "基线失败时不应标记为已拿到映射地址");
            }
        }

        /// <summary>
        /// 预占一个尚未使用的 UDP 端口；拿到端口后立即释放，供「无应答」用例使用
        /// </summary>
        /// <returns>本地端口</returns>
        private static int ReserveUdpPort()
        {
            using (var probe = new SaeaUdpTransport())
            {
                probe.BindAsync(new IPEndPoint(PrimaryAddress, 0), CancellationToken.None).GetAwaiter().GetResult();
                return probe.LocalEndPoint.Port;
            }
        }

        /// <summary>
        /// 用户态假 STUN 服务端：按配置返回固定映射地址，并可丢弃带 CHANGE-REQUEST 的请求。
        /// </summary>
        private sealed class FakeBehaviorServer : IDisposable
        {
            /// <summary>
            /// 底层 UDP 传输
            /// </summary>
            private readonly SaeaUdpTransport _transport = new SaeaUdpTransport();

            /// <summary>
            /// 固定映射地址；为 null 时直接回显客户端来源端点
            /// </summary>
            private readonly IPEndPoint _mappedAddress;

            /// <summary>
            /// 是否丢弃带 CHANGE-REQUEST 的请求，用于模拟地址端口相关过滤
            /// </summary>
            private readonly bool _dropChangeRequest;

            /// <summary>
            /// 绑定指定地址与端口并开始接收
            /// </summary>
            /// <param name="address">监听地址</param>
            /// <param name="port">监听端口，0 表示由系统分配</param>
            /// <param name="mappedAddress">固定映射地址；为 null 时回显来源端点</param>
            /// <param name="dropChangeRequest">是否丢弃带 CHANGE-REQUEST 的请求</param>
            public FakeBehaviorServer(IPAddress address, int port, IPEndPoint mappedAddress, bool dropChangeRequest)
            {
                _mappedAddress = mappedAddress;
                _dropChangeRequest = dropChangeRequest;
                _transport.BindAsync(new IPEndPoint(address, port), CancellationToken.None).GetAwaiter().GetResult();
                _transport.DatagramReceived += OnDatagramReceived;
            }

            /// <summary>
            /// 本地监听端点
            /// </summary>
            public IPEndPoint EndPoint
            {
                get { return _transport.LocalEndPoint; }
            }

            /// <summary>
            /// 本地监听端口
            /// </summary>
            public int Port
            {
                get { return _transport.LocalEndPoint.Port; }
            }

            /// <summary>
            /// 成功应答应携带的 OTHER-ADDRESS；为 null 时不携带
            /// </summary>
            public IPEndPoint OtherAddress { get; set; }

            /// <summary>
            /// 退订并释放传输
            /// </summary>
            public void Dispose()
            {
                _transport.DatagramReceived -= OnDatagramReceived;
                _transport.Dispose();
            }

            /// <summary>
            /// 解析请求并按配置构造成功应答；需要丢弃时不回包
            /// </summary>
            /// <param name="remoteEndPoint">客户端来源端点</param>
            /// <param name="data">数据报内容</param>
            private void OnDatagramReceived(IPEndPoint remoteEndPoint, ReadOnlyMemory<byte> data)
            {
                StunMessage request;
                StunParseError error;
                if (!StunMessage.TryParse(data.Span, out request, out error))
                {
                    return;
                }

                StunAttribute change;
                if (_dropChangeRequest && request.TryGet(StunAttributeType.ChangeRequest, out change))
                {
                    return;
                }

                var mapped = _mappedAddress ?? remoteEndPoint;
                var transactionId = request.TransactionId.ToArray();
                var response = new StunMessage(StunMessageType.Binding, StunClass.SuccessResponse, transactionId);
                var attributes = new List<StunAttribute>
                {
                    StunAddressCodec.EncodeAddress(StunAttributeType.XorMappedAddress, mapped, true, transactionId),
                    StunAddressCodec.EncodeAddress(StunAttributeType.MappedAddress, mapped, false),
                    StunAddressCodec.EncodeAddress(StunAttributeType.ResponseOrigin, _transport.LocalEndPoint, false)
                };

                if (OtherAddress != null)
                {
                    attributes.Add(StunAddressCodec.EncodeAddress(StunAttributeType.OtherAddress, OtherAddress, false));
                }

                response.SetAttributes(attributes);
                _transport.SendToAsync(remoteEndPoint, new ReadOnlyMemory<byte>(response.ToArray()), CancellationToken.None).GetAwaiter().GetResult();
            }
        }
    }
}