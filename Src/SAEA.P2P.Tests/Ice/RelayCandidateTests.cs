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
*命名空间：SAEA.P2P.Tests.Ice
*文件名： RelayCandidateTests
*版本号： net10.0
*唯一标识：cf6a9ab6-e13d-43bf-919c-e44c81f595d7
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 20:00:00
*描述：TURN 中继候选与中继数据通道验收
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 11:39:41
*修改人： yswenli
*版本号： net10.0
*描述：权限用例改为在首个检查包到达时断言权限已建立
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Ice;
using SAEA.P2P.Stun;
using SAEA.P2P.StunServer;
using SAEA.P2P.Transport;
using SAEA.P2P.Turn;
using SAEA.P2P.TurnServer;

using StunServerHost = SAEA.P2P.StunServer.StunServer;
using TurnServerHost = SAEA.P2P.TurnServer.TurnServer;

namespace SAEA.P2P.Tests.Ice
{
    /// <summary>
    /// TURN 中继候选：relay 候选优先级、检查前对 peer IP 建权限、每 peer 独立 allocation。
    /// </summary>
    [TestClass]
    public class RelayCandidateTests
    {
        /// <summary>
        /// 中继对外地址，也是 STUN/TURN 监听地址
        /// </summary>
        private static readonly IPAddress RelayAddress = IPAddress.Parse("127.0.0.10");

        /// <summary>
        /// 裸 STUN 对端地址
        /// </summary>
        private static readonly IPAddress PeerAddress = IPAddress.Parse("127.0.0.20");

        /// <summary>
        /// 首个客户端本地地址
        /// </summary>
        private static readonly IPAddress ClientAddress = IPAddress.Parse("127.0.0.2");

        /// <summary>
        /// 第二个客户端本地地址
        /// </summary>
        private static readonly IPAddress SecondClientAddress = IPAddress.Parse("127.0.0.3");

        /// <summary>
        /// 测试用共享密钥
        /// </summary>
        private const string SharedSecret = "shared-secret-for-tests";

        /// <summary>
        /// 测试用认证域
        /// </summary>
        private const string Realm = "saea.p2p";

        /// <summary>
        /// 中继候选的类型优先级为 0，按 UDP 公式算出的优先级低于其余候选类型，且重复申请复用同一候选与分配。
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task RelayCandidate_优先级为0()
        {
            using (var harness = await TurnHarness.StartAsync(2))
            using (var client = harness.CreateClient(new IPEndPoint(ClientAddress, 0)))
            using (var provider = new RelayCandidateProvider(client, 1))
            {
                var candidate = await provider.AllocateAsync(CancellationToken.None);

                Assert.IsNotNull(candidate, "中继候选应申请成功");
                Assert.AreEqual(CandidateType.Relay, candidate.Type, "候选类型应为 relay");
                Assert.AreEqual(IceTransportType.Udp, candidate.Transport, "UDP 中继候选的传输类型应为 Udp");
                Assert.AreEqual(1, candidate.Component, "候选分量应与构造参数一致");
                Assert.AreEqual(0, IcePriority.GetTypePreference(candidate.Type), "relay 的类型优先级应为 0");
                Assert.AreEqual(
                    IcePriority.ComputeForUdp(CandidateType.Relay, 1),
                    candidate.Priority,
                    "relay 候选应按 UDP 公式自动算出优先级");
                Assert.IsTrue(
                    candidate.Priority < IcePriority.ComputeForUdp(CandidateType.ServerReflexive, 1),
                    "relay 候选优先级应低于 srflx 候选");

                Assert.IsNotNull(provider.Channel, "申请后应持有中继数据通道");
                Assert.AreEqual(candidate.EndPoint, provider.Channel.LocalEndPoint, "通道本地端点应为中继端点");

                var mapped = client.Allocation.MappedEndPoint;
                if (mapped != null)
                {
                    Assert.AreEqual(mapped.Address, candidate.RelatedAddress, "关联地址应为分配前的本地映射地址");
                    Assert.AreEqual(mapped.Port, candidate.RelatedPort, "关联端口应为分配前的本地映射端口");
                }

                var again = await provider.AllocateAsync(CancellationToken.None);
                Assert.AreSame(candidate, again, "重复申请应返回同一候选");
                Assert.AreEqual(1, harness.Server.AllocationCount, "重复申请不应新建分配");

                await provider.ReleaseAsync(CancellationToken.None);
                Assert.IsNull(provider.Candidate, "释放后不应保留候选");
                Assert.IsTrue(
                    await WaitUntilAsync(() => harness.Server.AllocationCount == 0, TimeSpan.FromSeconds(5)),
                    "释放后服务端分配应删除，实际剩余 " + harness.Server.AllocationCount);
            }
        }

        /// <summary>
        /// 从 relay 候选发起检查前，先对 peer IP 建一条权限，且检查包从本通道的中继端点到达对端。
        /// 权限的先后顺序只能靠「对端收到首个检查包的瞬间读取 PermissionCount」来证明：
        /// 对端是裸 STUN 服务端、不校验权限，检查结束后再数一遍无法区分权限是建在检查包之前还是之后。
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task RelayCandidate_检查前_已对peerIP建权限()
        {
            var peerOptions = new StunServerOptions(new IPEndPoint(PeerAddress, 0));
            var recorder = new BindingRecorder();
            peerOptions.DatagramHandler = recorder.Handle;

            using (var harness = await TurnHarness.StartAsync(2))
            using (var peer = new StunServerHost(peerOptions))
            using (var client = harness.CreateClient(new IPEndPoint(ClientAddress, 0)))
            using (var provider = new RelayCandidateProvider(client, 1))
            {
                // 在检查包到达对端的同一时刻取权限数快照，这才是权限先于检查包的证据
                int? permissionCountAtRequest = null;
                recorder.RequestObserved = () => { permissionCountAtRequest = client.Allocation.PermissionCount; };

                await peer.StartAsync();
                var peerEndPoint = peer.Endpoints[0].LocalEndPoint;

                var candidate = await provider.AllocateAsync(CancellationToken.None);
                Assert.IsNotNull(candidate, "中继候选应申请成功");

                using (var check = new ConnectivityCheck("local-ufrag", "local-password"))
                {
                    var result = await check.CheckAsync(
                        provider.Channel,
                        peerEndPoint,
                        "peer-ufrag",
                        "peer-password",
                        IceRole.Controlling,
                        0x0000000000000001UL,
                        check.ComputePeerReflexivePriority(1),
                        false,
                        provider.Channel,
                        new[] { PeerAddress },
                        CancellationToken.None);

                    // 对端是裸 STUN 服务端，应答不带 MESSAGE-INTEGRITY，只能确认检查确实跑完
                    Assert.IsNotNull(result, "检查应返回结果");
                }

                Assert.IsNotNull(permissionCountAtRequest, "对端应收到检查包");
                Assert.AreEqual(1, permissionCountAtRequest.Value, "首个检查包到达对端时，peer IP 的权限应已建立");
                Assert.AreEqual(1, client.Allocation.PermissionCount, "检查后仍只对 peer IP 保留一条权限");
                Assert.IsNotNull(recorder.SourceEndPoint, "对端应收到检查包");
                Assert.AreEqual(RelayAddress, recorder.SourceEndPoint.Address, "对端看到的来源地址应为中继地址");
                Assert.AreEqual(peerEndPoint, recorder.DestinationEndPoint, "检查包目标应为对端 STUN 端点");
                Assert.AreEqual(provider.Channel.LocalEndPoint, recorder.SourceEndPoint, "对端看到的来源应为通道的中继端点");

                await provider.ReleaseAsync(CancellationToken.None);
            }
        }

        /// <summary>
        /// 两个客户端各自申请中继，候选与中继端点互不相同，服务端同时保留两条 allocation。
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task RelayCandidate_每peer独立_不复用allocation()
        {
            using (var harness = await TurnHarness.StartAsync(4))
            using (var first = harness.CreateClient(new IPEndPoint(ClientAddress, 0)))
            using (var second = harness.CreateClient(new IPEndPoint(SecondClientAddress, 0)))
            using (var firstProvider = new RelayCandidateProvider(first, 1))
            using (var secondProvider = new RelayCandidateProvider(second, 1))
            {
                var firstCandidate = await firstProvider.AllocateAsync(CancellationToken.None);
                var secondCandidate = await secondProvider.AllocateAsync(CancellationToken.None);

                Assert.IsNotNull(firstCandidate, "首个客户端应拿到中继候选");
                Assert.IsNotNull(secondCandidate, "第二个客户端应拿到中继候选");
                Assert.AreNotSame(firstProvider.Channel, secondProvider.Channel, "每个客户端应持有独立的中继通道");
                Assert.AreNotEqual(firstCandidate.EndPoint, secondCandidate.EndPoint, "两个客户端的候选端点不应相同");
                Assert.AreNotEqual(
                    firstProvider.Channel.LocalEndPoint,
                    secondProvider.Channel.LocalEndPoint,
                    "两个客户端的中继端点不应相同");
                Assert.AreEqual(2, harness.Server.AllocationCount, "两个客户端应各占一条服务端分配");

                await firstProvider.ReleaseAsync(CancellationToken.None);
                await secondProvider.ReleaseAsync(CancellationToken.None);
                Assert.IsTrue(
                    await WaitUntilAsync(() => harness.Server.AllocationCount == 0, TimeSpan.FromSeconds(5)),
                    "全部释放后服务端不应保留分配，实际剩余 " + harness.Server.AllocationCount);
            }
        }

        /// <summary>
        /// 轮询等待条件成立
        /// </summary>
        /// <param name="condition">条件</param>
        /// <param name="timeout">等待上限</param>
        /// <returns>成立返回 true，超时返回 false</returns>
        private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (condition())
                {
                    return true;
                }
                await Task.Delay(20);
            }
            return condition();
        }

        /// <summary>
        /// 在 127.0.0.10 上取若干个空闲 UDP 端口并立即释放，用于收窄中继端口区间
        /// </summary>
        /// <param name="count">端口数量</param>
        /// <returns>升序端口数组</returns>
        private static int[] ReserveRelayPorts(int count)
        {
            var probes = new List<SaeaUdpTransport>();
            try
            {
                var ports = new int[count];
                for (var i = 0; i < count; i++)
                {
                    var probe = new SaeaUdpTransport();
                    probe.BindAsync(new IPEndPoint(RelayAddress, 0), CancellationToken.None).GetAwaiter().GetResult();
                    probes.Add(probe);
                    ports[i] = probe.LocalEndPoint.Port;
                }

                Array.Sort(ports);
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
        /// 记录首个可解析的 Binding 请求的来源与目标，其余数据报原样交回 STUN 层
        /// </summary>
        private sealed class BindingRecorder
        {
            /// <summary>
            /// 保护记录字段的同步对象
            /// </summary>
            private readonly object _sync = new object();

            /// <summary>
            /// 请求来源端点
            /// </summary>
            private IPEndPoint _sourceEndPoint;

            /// <summary>
            /// 请求目标端点
            /// </summary>
            private IPEndPoint _destinationEndPoint;

            /// <summary>
            /// 首个 Binding 请求的来源端点，未收到时为 null
            /// </summary>
            public IPEndPoint SourceEndPoint
            {
                get { lock (_sync) { return _sourceEndPoint; } }
            }

            /// <summary>
            /// 首个 Binding 请求的目标端点，未收到时为 null
            /// </summary>
            public IPEndPoint DestinationEndPoint
            {
                get { lock (_sync) { return _destinationEndPoint; } }
            }

            /// <summary>
            /// 记录到首个 Binding 请求的同一时刻回调一次，用于读取此刻的外部状态
            /// </summary>
            public Action RequestObserved { get; set; }

            /// <summary>
            /// 数据报入口：只记录不拦截，返回 false 让 STUN 层继续应答
            /// </summary>
            /// <param name="data">数据报内容</param>
            /// <param name="remoteEndPoint">来源端点</param>
            /// <param name="receivedOn">收到报文的监听端点</param>
            /// <returns>恒为 false</returns>
            public bool Handle(ReadOnlyMemory<byte> data, IPEndPoint remoteEndPoint, StunServerEndpoint receivedOn)
            {
                StunMessage message;
                StunParseError error;
                if (!StunMessage.TryParse(data.Span, out message, out error))
                {
                    return false;
                }
                if (message.Class != StunClass.Request || message.Method != StunMessageType.Binding)
                {
                    return false;
                }

                var firstObserved = false;
                lock (_sync)
                {
                    if (_sourceEndPoint == null)
                    {
                        _destinationEndPoint = receivedOn.LocalEndPoint;
                        _sourceEndPoint = remoteEndPoint;
                        firstObserved = true;
                    }
                }

                if (firstObserved)
                {
                    var observer = RequestObserved;
                    if (observer != null)
                    {
                        try
                        {
                            observer();
                        }
                        catch
                        {
                            // 测试夹具的订阅者异常不应影响 STUN 层应答
                        }
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// TURN 服务端与承接其报文的 STUN 监听端点
        /// </summary>
        private sealed class TurnHarness : IDisposable
        {
            /// <summary>
            /// 底层 STUN 服务端
            /// </summary>
            private readonly StunServerHost _stun;

            /// <summary>
            /// TURN 服务端
            /// </summary>
            private readonly TurnServerHost _server;

            /// <summary>
            /// 是否已释放
            /// </summary>
            private bool _disposed;

            /// <summary>
            /// 创建宿主
            /// </summary>
            /// <param name="stun">STUN 服务端</param>
            /// <param name="server">TURN 服务端</param>
            /// <param name="endPoint">客户端应访问的端点</param>
            private TurnHarness(StunServerHost stun, TurnServerHost server, IPEndPoint endPoint)
            {
                _stun = stun;
                _server = server;
                EndPoint = endPoint;
            }

            /// <summary>
            /// TURN 服务端
            /// </summary>
            public TurnServerHost Server
            {
                get { return _server; }
            }

            /// <summary>
            /// 客户端应访问的端点
            /// </summary>
            public IPEndPoint EndPoint { get; private set; }

            /// <summary>
            /// 按给定数量预留中继端口后启动服务端
            /// </summary>
            /// <param name="relayPortCount">预留的中继端口数量</param>
            /// <returns>宿主</returns>
            public static async Task<TurnHarness> StartAsync(int relayPortCount)
            {
                var ports = ReserveRelayPorts(relayPortCount);
                var server = new TurnServerHost(new TurnServerOptions
                {
                    SharedSecret = SharedSecret,
                    Realm = Realm,
                    RelayAddress = RelayAddress,
                    RelayPortMin = ports[0],
                    RelayPortMax = ports[ports.Length - 1]
                });

                var options = new StunServerOptions(new IPEndPoint(RelayAddress, 0))
                {
                    RequestHandler = server.HandleRequest,
                    DatagramHandler = server.HandleDatagram
                };
                var stun = new StunServerHost(options);
                await stun.StartAsync();

                return new TurnHarness(stun, server, stun.Endpoints[0].LocalEndPoint);
            }

            /// <summary>
            /// 用测试凭据创建绑定到指定本地端点的客户端
            /// </summary>
            /// <param name="local">本地端点</param>
            /// <returns>TURN 客户端</returns>
            public TurnClient CreateClient(IPEndPoint local)
            {
                var options = new TurnClientOptions
                {
                    ServerEndPoint = EndPoint,
                    TransportFactory = () => new UdpTurnClientTransport(local, EndPoint),
                    Credential = _server.CredentialService.Create("relay-user"),
                    RtoMilliseconds = 100,
                    AllocationRefreshInterval = TimeSpan.FromSeconds(30),
                    PermissionRefreshInterval = TimeSpan.FromSeconds(30),
                    ChannelRefreshInterval = TimeSpan.FromSeconds(30),
                    RefreshTimeout = TimeSpan.FromSeconds(10)
                };
                return new TurnClient(options);
            }

            /// <summary>
            /// 依次释放 TURN 服务端与 STUN 服务端；重复调用为空操作
            /// </summary>
            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _server.Dispose();
                _stun.Dispose();
            }
        }
    }
}