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
*命名空间：SAEA.P2P.Tests.Integration
*文件名： EndToEndTests
*版本号： net10.0
*唯一标识：a68df886-a4c3-420b-a560-56faf6371c68
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 14:29:29
*描述：两个 NAT 后客户端经信令、STUN、TURN 的端到端用例骨架
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 15:45:08
*修改人： yswenli
*版本号： net10.0
*描述：修正 TCP 直连用例说明，让回退中继用例注入 STUN 收集 srflx 并相应放宽 TURN 阶段预算
*
*****************************************************************************/
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Builder;
using SAEA.P2P.Channel;
using SAEA.P2P.Core;
using SAEA.P2P.Ice;
using SAEA.P2P.Signaling;
using SAEA.P2P.Tests.Nat;
using SAEA.P2P.Turn;

using StunServerHost = SAEA.P2P.StunServer.StunServer;
using StunServerOptions = SAEA.P2P.StunServer.StunServerOptions;
using TurnServerHost = SAEA.P2P.TurnServer.TurnServer;
using TurnServerOptions = SAEA.P2P.TurnServer.TurnServerOptions;

namespace SAEA.P2P.Tests.Integration
{
    /// <summary>
    /// 端到端用例：进程内起信令、STUN、TURN 服务，两个客户端分别位于双 NAT 之后，
    /// 走完整注册、候选交换、选路与双向业务帧收发，断言选出的通道类型与数据路径。
    /// </summary>
    [TestCategory(P2PTestSuite.Matrix)]
    [TestClass]
    public class EndToEndTests
    {
        /// <summary>
        /// 发起方节点标识
        /// </summary>
        private const string PeerA = "e2e-a";

        /// <summary>
        /// 应答方节点标识
        /// </summary>
        private const string PeerB = "e2e-b";

        /// <summary>
        /// 等待事件通知的默认预算
        /// </summary>
        private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 客户端 A 的 NAT 内部地址
        /// </summary>
        private static readonly IPAddress HostAAddress = IPAddress.Parse("127.0.0.2");

        /// <summary>
        /// 客户端 B 的 NAT 内部地址
        /// </summary>
        private static readonly IPAddress HostBAddress = IPAddress.Parse("127.0.0.3");

        /// <summary>
        /// NAT A 的对外地址
        /// </summary>
        private static readonly IPAddress NatAAddress = IPAddress.Parse("127.0.0.4");

        /// <summary>
        /// NAT B 的对外地址
        /// </summary>
        private static readonly IPAddress NatBAddress = IPAddress.Parse("127.0.0.5");

        /// <summary>
        /// STUN 服务地址
        /// </summary>
        private static readonly IPAddress StunAddress = IPAddress.Parse("127.0.0.6");

        /// <summary>
        /// TURN 服务地址
        /// </summary>
        private static readonly IPAddress TurnAddress = IPAddress.Parse("127.0.0.7");

        /// <summary>
        /// 测试内信令服务端与 TURN 服务端共用的共享密钥
        /// </summary>
        private const string TurnSharedSecret = "e2e-test-secret";

        /// <summary>
        /// 测试内 TURN 认证域，信令下发凭据与 TURN 校验必须一致
        /// </summary>
        private const string TurnRealm = "e2e.test";

        /// <summary>
        /// TURN 中继端口下界，避开其他 ICE 用例占用的区间
        /// </summary>
        private const int TurnRelayPortMin = 57000;

        /// <summary>
        /// TURN 中继端口上界，两个客户端各占一个 allocation
        /// </summary>
        private const int TurnRelayPortMax = 57999;

        /// <summary>
        /// UDP 直连：通道类型应为 UDP 直连，且业务帧双向完整到达
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task EndToEnd_UDP直连_通道类型与双向数据正确()
        {
            // STUN 必须先于客户端存在：srflx 候选在候选收集阶段就要向它发 Binding 请求
            using (var stun = new StunServerHost(new StunServerOptions(new IPEndPoint(StunAddress, 0))))
            {
                await stun.StartAsync().ConfigureAwait(false);

                using (var pair = await ConnectPairAsync(ReserveTcpPort(), stun.Endpoints[0].LocalEndPoint).ConfigureAwait(false))
                {
                    Assert.AreEqual(ChannelType.UdpDirect, pair.ConnectionA.Channel, "发起方应选出 UDP 直连通道");
                    Assert.AreEqual(ChannelType.UdpDirect, pair.ConnectionB.Channel, "应答方应选出 UDP 直连通道");

                    Assert.IsNotNull(pair.ConnectionA.RemoteEndPoint, "发起方连接的远端端点不应为空");
                    Assert.AreEqual(NatBAddress, pair.ConnectionA.RemoteEndPoint.Address, "发起方的远端应为 NAT B 的对外地址");
                    Assert.IsNotNull(pair.ConnectionB.RemoteEndPoint, "应答方连接的远端端点不应为空");
                    Assert.AreEqual(NatAAddress, pair.ConnectionB.RemoteEndPoint.Address, "应答方看到的来源应为 NAT A 的对外地址");

                    var aReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var bReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

                    // data 是只在回调期间有效的 Span，必须先复制再交给断言逻辑
                    pair.ClientA.OnDataReceived += (peerId, data) => aReceived.TrySetResult(data.ToArray());
                    pair.ClientB.OnDataReceived += (peerId, data) => bReceived.TrySetResult(data.ToArray());

                    var outgoing = new byte[1024];
                    for (var i = 0; i < outgoing.Length; i++)
                    {
                        outgoing[i] = (byte)(i % 251);
                    }

                    pair.ClientA.Send(PeerB, outgoing);
                    var receivedByB = await WaitAsync(bReceived.Task, EventTimeout).ConfigureAwait(false);
                    CollectionAssert.AreEqual(outgoing, receivedByB, "应答方收到的载荷应与发送方一致");

                    var reply = Encoding.UTF8.GetBytes("p2p-e2e-反向业务帧");
                    pair.ClientB.Send(PeerA, reply);
                    var receivedByA = await WaitAsync(aReceived.Task, EventTimeout).ConfigureAwait(false);
                    CollectionAssert.AreEqual(reply, receivedByA, "发起方收到的应答应与应答方发送的一致");
                }
            }
        }

        /// <summary>
        /// TCP 直连：两端在 NAT 后，UDP 直连失败后应选出 TCP 直连通道
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task EndToEnd_TCP直连_通道类型正确()
        {
            // 不配置 STUN/TURN：内部地址不可路由，UDP 直连必然失败，选路阶梯落到 TCP 直连。
            // TCP 候选收集器没有可注入传输的口子，直接在 BindAddresses 上绑真实监听并主动连接，
            // 因此本用例只验证 TCP 直连选路与数据面，不经过 NAT 转发。
            using (var pair = await ConnectPairAsync(ReserveTcpPort(), null).ConfigureAwait(false))
            {
                Assert.AreEqual(ChannelType.TcpDirect, pair.ConnectionA.Channel, "发起方应选出 TCP 直连通道");
                Assert.AreEqual(ChannelType.TcpDirect, pair.ConnectionB.Channel, "应答方应选出 TCP 直连通道");

                var aReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                var bReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

                // data 是只在回调期间有效的 Span，必须先复制再交给断言逻辑
                pair.ClientA.OnDataReceived += (peerId, data) => aReceived.TrySetResult(data.ToArray());
                pair.ClientB.OnDataReceived += (peerId, data) => bReceived.TrySetResult(data.ToArray());

                var outgoing = new byte[1024];
                for (var i = 0; i < outgoing.Length; i++)
                {
                    outgoing[i] = (byte)(i % 241);
                }

                pair.ClientA.Send(PeerB, outgoing);
                var receivedByB = await WaitAsync(bReceived.Task, EventTimeout).ConfigureAwait(false);
                CollectionAssert.AreEqual(outgoing, receivedByB, "应答方收到的载荷应与发送方一致");

                var reply = Encoding.UTF8.GetBytes("p2p-e2e-tcp-反向业务帧");
                pair.ClientB.Send(PeerA, reply);
                var receivedByA = await WaitAsync(aReceived.Task, EventTimeout).ConfigureAwait(false);
                CollectionAssert.AreEqual(reply, receivedByA, "发起方收到的应答应与应答方发送的一致");
            }
        }

        /// <summary>
        /// UDP 直连被阻断：直连阶段失败后应回退到 TURN 中继，通道类型为 RelayUdp
        /// </summary>
        [TestMethod]
        [Timeout(400000)]
        public async Task EndToEnd_UDP失败_回退RelayUdp()
        {
            using (var turnServer = new TurnServerHost(new TurnServerOptions
            {
                SharedSecret = TurnSharedSecret,
                Realm = TurnRealm,
                RelayAddress = TurnAddress,
                RelayPortMin = TurnRelayPortMin,
                RelayPortMax = TurnRelayPortMax
            }))
            using (var stun = new StunServerHost(new StunServerOptions(new IPEndPoint(StunAddress, 0))))
            // TURN 报文复用 STUN 的编解码与监听，只把请求与数据报转给 TURN 服务端处理
            using (var turnHost = new StunServerHost(new StunServerOptions(new IPEndPoint(TurnAddress, 0))
            {
                RequestHandler = turnServer.HandleRequest,
                DatagramHandler = turnServer.HandleDatagram
            }))
            {
                await stun.StartAsync().ConfigureAwait(false);
                await turnHost.StartAsync().ConfigureAwait(false);

                var stunEndPoint = stun.Endpoints[0].LocalEndPoint;
                var turnEndPoint = turnHost.Endpoints[0].LocalEndPoint;

                // 两端都置于对称型 NAT：直连打洞必然失败，只有中继可用
                Func<IPAddress, IPEndPoint, IceSessionOptions> iceOptionsFactory = (bindAddress, natGateway) =>
                {
                    // 两端都在对称 NAT 后，srflx 候选拿到的公网映射也打不通对端：中继阶段按对端候选
                    // 优先级逐个检查，host 与 srflx 两个候选对会各白耗一次完整事务超时（39.5s），
                    // 因此仍按真实流程注入 STUN 收集 srflx，并把 TURN 阶段预算放宽到 100s
                    var options = CreateIceOptions(bindAddress, natGateway, stunEndPoint);

                    // TCP 候选绑定真实内部地址并在本机直接建链，不经 NAT 模拟，必然能打通；
                    // 本用例要验证的是 UDP 失败后回退中继，用极小预算让该级在握手前失效
                    options.TcpDirectBudget = TimeSpan.FromTicks(1);
                    // 单个不可达候选对的检查上限是 39.5s，且取该值与阶段剩余预算的较小者：
                    // 阶段预算必须大于对端 host 与 srflx 各一次超时之和，中继候选对才轮得到
                    options.TurnStageBudget = TimeSpan.FromSeconds(100);
                    // 总预算不能小于收集与各级阶段预算之和：2 + 3 + 0 + 100*3 = 305
                    options.SessionBudget = TimeSpan.FromSeconds(310);

                    options.Turn = new TurnClientOptions
                    {
                        ServerEndPoint = turnEndPoint,
                        // TURN 控制面直接绑定内部地址连服务端，不走 NAT 模拟，否则分配请求会被模拟器改写
                        TransportFactory = () => new UdpTurnClientTransport(new IPEndPoint(bindAddress, 0), turnEndPoint),
                        RtoMilliseconds = 100,
                        AllocationRefreshInterval = TimeSpan.FromSeconds(30),
                        PermissionRefreshInterval = TimeSpan.FromSeconds(30),
                        ChannelRefreshInterval = TimeSpan.FromSeconds(30),
                        RefreshTimeout = TimeSpan.FromSeconds(10)
                    };
                    return options;
                };

                using (var pair = await ConnectPairAsync(
                    ReserveTcpPort(),
                    stunEndPoint,
                    NatPreset.Symmetric(),
                    iceOptionsFactory,
                    options =>
                    {
                        // 与 TURN 服务端使用同一套共享密钥与认证域，客户端才会收到可用的临时凭据
                        options.SharedSecret = TurnSharedSecret;
                        options.Realm = TurnRealm;
                        options.TurnUrls = new[] { "turn:" + turnEndPoint + "?transport=udp" };
                    },
                    TurnSharedSecret,
                    WaitForTurnCredentialAsync,
                    TimeSpan.FromSeconds(320)).ConfigureAwait(false))
                {
                    Assert.AreEqual(ChannelType.RelayUdp, pair.ConnectionA.Channel, "发起方应回退到 UDP 中继通道");
                    Assert.AreEqual(ChannelType.RelayUdp, pair.ConnectionB.Channel, "应答方应回退到 UDP 中继通道");

                    Assert.IsNotNull(pair.ConnectionA.LocalEndPoint, "发起方中继连接的本地端点不应为空");
                    Assert.AreEqual(TurnAddress, pair.ConnectionA.LocalEndPoint.Address, "发起方的本地端点应为 TURN 中继地址");
                    Assert.IsNotNull(pair.ConnectionA.RemoteEndPoint, "发起方中继连接的远端端点不应为空");
                    Assert.AreEqual(TurnAddress, pair.ConnectionA.RemoteEndPoint.Address, "发起方的远端应为对端 TURN 中继地址");

                    Assert.IsNotNull(pair.ConnectionB.LocalEndPoint, "应答方中继连接的本地端点不应为空");
                    Assert.AreEqual(TurnAddress, pair.ConnectionB.LocalEndPoint.Address, "应答方的本地端点应为 TURN 中继地址");
                    Assert.IsNotNull(pair.ConnectionB.RemoteEndPoint, "应答方中继连接的远端端点不应为空");
                    Assert.AreEqual(TurnAddress, pair.ConnectionB.RemoteEndPoint.Address, "应答方的远端应为对端 TURN 中继地址");

                    var aReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var bReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

                    // data 是只在回调期间有效的 Span，必须先复制再交给断言逻辑
                    pair.ClientA.OnDataReceived += (peerId, data) => aReceived.TrySetResult(data.ToArray());
                    pair.ClientB.OnDataReceived += (peerId, data) => bReceived.TrySetResult(data.ToArray());

                    var outgoing = new byte[1024];
                    for (var i = 0; i < outgoing.Length; i++)
                    {
                        outgoing[i] = (byte)(i % 239);
                    }

                    pair.ClientA.Send(PeerB, outgoing);
                    var receivedByB = await WaitAsync(bReceived.Task, EventTimeout).ConfigureAwait(false);
                    CollectionAssert.AreEqual(outgoing, receivedByB, "应答方经中继收到的载荷应与发送方一致");

                    var reply = Encoding.UTF8.GetBytes("p2p-e2e-relay-反向业务帧");
                    pair.ClientB.Send(PeerA, reply);
                    var receivedByA = await WaitAsync(aReceived.Task, EventTimeout).ConfigureAwait(false);
                    CollectionAssert.AreEqual(reply, receivedByA, "发起方经中继收到的应答应与应答方发送的一致");
                }
            }
        }

        /// <summary>
        /// 数据路径：业务帧只走选中的数据通道，不应经信令连接转发
        /// </summary>
        [TestMethod]
        [Timeout(60000)]
        public async Task EndToEnd_信令连接_不承载业务数据()
        {
            using (var stun = new StunServerHost(new StunServerOptions(new IPEndPoint(StunAddress, 0))))
            {
                await stun.StartAsync().ConfigureAwait(false);

                using (var pair = await ConnectPairAsync(ReserveTcpPort(), stun.Endpoints[0].LocalEndPoint).ConfigureAwait(false))
                {
                    Assert.AreEqual(ChannelType.UdpDirect, pair.ConnectionA.Channel, "对照条件：先建立 UDP 直连通道");

                    var disconnects = 0;
                    pair.ClientA.OnPeerDisconnected += (peerId, reason) => Interlocked.Increment(ref disconnects);
                    pair.ClientB.OnPeerDisconnected += (peerId, reason) => Interlocked.Increment(ref disconnects);

                    // 停止信令服务端：监听已关闭、节点与会话映射被清空，服务端不再具备转发信令的能力；
                    // 业务帧若仍能到达，只可能走已建立的 ICE 数据通道。
                    // 服务端停止不会主动关闭已接入的信令连接，客户端 IsRegistered 不会翻转，不能拿它当判据
                    pair.Server.Stop();
                    Assert.IsFalse(pair.Server.IsRunning, "信令服务端应处在停止状态");

                    Assert.AreEqual(ChannelType.UdpDirect, pair.ConnectionA.Channel, "信令断开后发起方通道不应改变");
                    Assert.AreEqual(ChannelType.UdpDirect, pair.ConnectionB.Channel, "信令断开后应答方通道不应改变");

                    var aReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var bReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

                    // data 是只在回调期间有效的 Span，必须先复制再交给断言逻辑
                    pair.ClientA.OnDataReceived += (peerId, data) => aReceived.TrySetResult(data.ToArray());
                    pair.ClientB.OnDataReceived += (peerId, data) => bReceived.TrySetResult(data.ToArray());

                    var outgoing = new byte[512];
                    for (var i = 0; i < outgoing.Length; i++)
                    {
                        outgoing[i] = (byte)(i % 233);
                    }

                    pair.ClientA.Send(PeerB, outgoing);
                    var receivedByB = await WaitAsync(bReceived.Task, EventTimeout).ConfigureAwait(false);
                    CollectionAssert.AreEqual(outgoing, receivedByB, "信令断开后应答方仍应收全载荷");

                    var reply = Encoding.UTF8.GetBytes("p2p-e2e-no-signaling-反向业务帧");
                    pair.ClientB.Send(PeerA, reply);
                    var receivedByA = await WaitAsync(aReceived.Task, EventTimeout).ConfigureAwait(false);
                    CollectionAssert.AreEqual(reply, receivedByA, "信令断开后发起方仍应收到应答");

                    Assert.AreEqual(0, Volatile.Read(ref disconnects), "信令服务端停止不应拆除已建立的对端连接");
                }
            }
        }

        /// <summary>
        /// 构造只监听回环的信令服务端配置，日志压到错误级并关闭中继，避免用例输出与无关模块干扰
        /// </summary>
        /// <param name="port">预占的监听端口</param>
        /// <returns>服务端配置</returns>
        private static P2PServerOptions CreateServerOptions(int port)
        {
            var options = new P2PServerOptions
            {
                BindIP = IPAddress.Loopback.ToString(),
                Port = port,
                SharedSecret = null,
                RejectDuplicatePeer = false
            };
            options.Logging.Level = 4;
            return options;
        }

        /// <summary>
        /// 构造指向本机信令服务端的客户端配置，心跳间隔放宽避免用例期间出现无关心跳
        /// </summary>
        /// <param name="peerId">本端节点标识</param>
        /// <param name="port">信令服务端端口</param>
        /// <param name="sharedSecret">信令鉴权共享密钥，为 null 时不启用鉴权</param>
        /// <returns>信令配置</returns>
        private static SignalingOptions CreateSignaling(string peerId, int port, string sharedSecret = null)
        {
            return new SignalingOptions
            {
                ServerAddress = IPAddress.Loopback.ToString(),
                ServerPort = port,
                PeerId = peerId,
                NodeName = peerId,
                SharedSecret = sharedSecret,
                ConnectTimeoutMs = 5000,
                RegisterTimeoutMs = 5000,
                HeartbeatIntervalMs = 30000
            };
        }

        /// <summary>
        /// 构造只收集指定内部地址 host 候选的 ICE 配置，传输工厂按延迟绑定方式创建 NAT 适配器
        /// </summary>
        /// <param name="bindAddress">内部主机地址</param>
        /// <param name="natGateway">本侧 NAT 的内部网关端点</param>
        /// <param name="stunEndPoint">STUN 服务端点，为 null 时不收集 srflx 候选</param>
        /// <returns>ICE 会话配置</returns>
        private static IceSessionOptions CreateIceOptions(IPAddress bindAddress, IPEndPoint natGateway, IPEndPoint stunEndPoint)
        {
            var ice = new IceOptions();
            ice.AllowLoopbackCandidates = true;
            ice.EnableIpv6 = false;
            ice.BindAddresses.Add(bindAddress);
            ice.GatheringTimeout = TimeSpan.FromSeconds(2);
            if (stunEndPoint != null)
            {
                ice.StunServers.Add(stunEndPoint);
            }

            // 工厂只记住 NAT 网关，绑定由候选收集器按 BindAddresses 完成；
            // 两个内部地址在本场景不可路由，直发必须丢弃，否则会绕过 NAT 变成同机直连
            ice.TransportFactory = () =>
            {
                var transport = new MappedUdpTransport(natGateway);
                transport.UnroutableDestinations.Add(HostAAddress);
                transport.UnroutableDestinations.Add(HostBAddress);
                return transport;
            };

            var options = new IceSessionOptions();
            options.Ice = ice;
            options.GatheringBudget = TimeSpan.FromSeconds(2);
            options.UdpDirectBudget = TimeSpan.FromSeconds(3);
            options.TcpDirectBudget = TimeSpan.FromSeconds(3);
            options.TurnStageBudget = TimeSpan.FromSeconds(2);
            // 会话总预算必须不小于收集预算与各级阶段预算之和：2 + 3 + 3 + 2*3 = 14
            options.SessionBudget = TimeSpan.FromSeconds(20);
            return options;
        }

        /// <summary>
        /// 预占一个尚未使用的 TCP 端口：先监听取到系统分配端口，再立即停止交给被测服务端
        /// </summary>
        /// <returns>端口号</returns>
        private static int ReserveTcpPort()
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
        /// 在预算内等待任务完成，超时抛 TimeoutException 而不是让用例一直挂到 [Timeout] 上限
        /// </summary>
        /// <typeparam name="T">任务结果类型</typeparam>
        /// <param name="task">被等待的任务</param>
        /// <param name="timeout">等待预算</param>
        /// <returns>任务结果</returns>
        private static async Task<T> WaitAsync<T>(Task<T> task, TimeSpan timeout)
        {
            var completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
            if (completed != task)
            {
                throw new TimeoutException("等待任务超时：" + (long)timeout.TotalMilliseconds + " ms");
            }
            return await task.ConfigureAwait(false);
        }

        /// <summary>
        /// 在预算内等待无结果的任务完成，超时抛 TimeoutException
        /// </summary>
        /// <param name="task">被等待的任务</param>
        /// <param name="timeout">等待预算</param>
        private static async Task WaitAsync(Task task, TimeSpan timeout)
        {
            var completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
            if (completed != task)
            {
                throw new TimeoutException("等待任务超时：" + (long)timeout.TotalMilliseconds + " ms");
            }
            await task.ConfigureAwait(false);
        }

        /// <summary>
        /// 在预算内轮询条件，超时抛 TimeoutException
        /// </summary>
        /// <param name="condition">条件委托</param>
        /// <param name="timeout">等待预算</param>
        private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (!condition())
            {
                if (DateTime.UtcNow >= deadline)
                {
                    throw new TimeoutException("等待条件超时：" + (long)timeout.TotalMilliseconds + " ms");
                }

                await Task.Delay(50).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 等待两端都收到 TURN 临时凭据：凭据在注册与鉴权成功后才异步下发，连接前必须到位
        /// </summary>
        /// <param name="pair">已注册两端客户端的夹具</param>
        private static Task WaitForTurnCredentialAsync(PairFixture pair)
        {
            return WaitUntilAsync(
                () => pair.IceA.Turn != null && pair.IceA.Turn.Credential != null
                    && pair.IceB.Turn != null && pair.IceB.Turn.Credential != null,
                EventTimeout);
        }

        /// <summary>
        /// 搭起双 NAT 环境并让两端注册、连接：起信令服务端，按各自 NAT 网关创建客户端，
        /// 等待双方都收到 OnPeerConnected 后返回夹具
        /// </summary>
        /// <param name="signalingPort">信令服务端端口</param>
        /// <param name="stunEndPoint">STUN 服务端点</param>
        /// <param name="natConfig">两端共用的 NAT 预设，为 null 时使用全锥形</param>
        /// <param name="iceOptionsFactory">按本侧内部地址与 NAT 网关生成 ICE 配置的工厂，为 null 时使用默认直连配置</param>
        /// <param name="configureServer">信令服务端配置回调，用于启用鉴权与 TURN 凭据下发</param>
        /// <param name="sharedSecret">信令鉴权共享密钥，必须与服务端配置一致</param>
        /// <param name="afterRegister">两端注册完成后的等待动作，例如等待 TURN 临时凭据下发</param>
        /// <param name="connectWait">等待连接建立的上限，为 null 时取默认值</param>
        /// <returns>已建立连接的夹具，调用方负责释放</returns>
        private static async Task<PairFixture> ConnectPairAsync(
            int signalingPort,
            IPEndPoint stunEndPoint,
            NatConfig natConfig = null,
            Func<IPAddress, IPEndPoint, IceSessionOptions> iceOptionsFactory = null,
            Action<P2PServerOptions> configureServer = null,
            string sharedSecret = null,
            Func<PairFixture, Task> afterRegister = null,
            TimeSpan? connectWait = null)
        {
            var fixture = new PairFixture();

            try
            {
                // NAT 必须先于客户端存在，客户端传输才有可用的内部网关；
                // 两个 NAT 各取一份预设实例，避免用例改一侧配置时影响另一侧
                fixture.NatA = new NatBox(NatAAddress, natConfig ?? NatPreset.FullCone());
                fixture.NatB = new NatBox(NatBAddress, natConfig ?? NatPreset.FullCone());

                var serverOptions = CreateServerOptions(signalingPort);
                if (configureServer != null)
                {
                    configureServer(serverOptions);
                }

                fixture.Server = new P2PServer(serverOptions);
                fixture.Server.Start();

                fixture.IceA = iceOptionsFactory != null
                    ? iceOptionsFactory(HostAAddress, fixture.NatA.InternalGateway)
                    : CreateIceOptions(HostAAddress, fixture.NatA.InternalGateway, stunEndPoint);
                fixture.IceB = iceOptionsFactory != null
                    ? iceOptionsFactory(HostBAddress, fixture.NatB.InternalGateway)
                    : CreateIceOptions(HostBAddress, fixture.NatB.InternalGateway, stunEndPoint);

                fixture.ClientA = new P2PClient(CreateSignaling(PeerA, signalingPort, sharedSecret), fixture.IceA);
                fixture.ClientB = new P2PClient(CreateSignaling(PeerB, signalingPort, sharedSecret), fixture.IceB);

                // 两端都可能在发起方任务返回前就触发事件，用异步续延避免回调重入选路线程
                var aConnected = new TaskCompletionSource<P2PConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
                var bConnected = new TaskCompletionSource<P2PConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
                fixture.ClientA.OnPeerConnected += connection => aConnected.TrySetResult(connection);
                fixture.ClientB.OnPeerConnected += connection => bConnected.TrySetResult(connection);

                await fixture.ClientA.RegisterAsync().ConfigureAwait(false);
                await fixture.ClientB.RegisterAsync().ConfigureAwait(false);

                if (afterRegister != null)
                {
                    await afterRegister(fixture).ConfigureAwait(false);
                }

                var connectTask = fixture.ClientA.ConnectToPeerAsync(PeerB);
                // 等待上限应高于 ICE 会话总预算，让会话自身先以明确错误失败，而不是被这里掩盖成超时
                var wait = connectWait ?? TimeSpan.FromSeconds(25);
                var completed = await Task.WhenAny(connectTask, Task.Delay(wait)).ConfigureAwait(false);
                if (completed != connectTask)
                {
                    throw new TimeoutException("等待客户端连接超时：" + (long)wait.TotalMilliseconds + " ms");
                }

                fixture.ConnectionA = await connectTask.ConfigureAwait(false);
                fixture.ConnectionB = await WaitAsync(bConnected.Task, EventTimeout).ConfigureAwait(false);
                // 发起方的事件可能早于 ConnectToPeerAsync 返回，单独等一次事件以确认通知确实到达
                await WaitAsync(aConnected.Task, EventTimeout).ConfigureAwait(false);
                return fixture;
            }
            catch
            {
                // 搭建中途失败也要先收回已启动的服务端、NAT 与客户端，否则端口与会话会泄漏到后续用例
                fixture.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 一对已建立连接的客户端及其双 NAT、信令服务端，释放时按客户端、服务端、NAT 顺序收尾
        /// </summary>
        private sealed class PairFixture : IDisposable
        {
            /// <summary>
            /// 被测信令服务端
            /// </summary>
            public P2PServer Server;

            /// <summary>
            /// 客户端 A 的 NAT 代理
            /// </summary>
            public NatBox NatA;

            /// <summary>
            /// 客户端 B 的 NAT 代理
            /// </summary>
            public NatBox NatB;

            /// <summary>
            /// 客户端 A 实际使用的 ICE 配置，释放后仍可用于断言尝试
            /// </summary>
            public IceSessionOptions IceA;

            /// <summary>
            /// 客户端 B 实际使用的 ICE 配置
            /// </summary>
            public IceSessionOptions IceB;

            /// <summary>
            /// 发起方客户端
            /// </summary>
            public P2PClient ClientA;

            /// <summary>
            /// 应答方客户端
            /// </summary>
            public P2PClient ClientB;

            /// <summary>
            /// 发起方连接描述
            /// </summary>
            public P2PConnection ConnectionA;

            /// <summary>
            /// 应答方连接描述
            /// </summary>
            public P2PConnection ConnectionB;

            /// <summary>
            /// 释放客户端、信令服务端与 NAT；各步异常互相隔离，避免收尾中断掩盖用例本身的失败
            /// </summary>
            public void Dispose()
            {
                if (ClientA != null)
                {
                    try { ClientA.Dispose(); } catch { }
                }

                if (ClientB != null)
                {
                    try { ClientB.Dispose(); } catch { }
                }

                if (Server != null)
                {
                    try { Server.Stop(); } catch { }
                }

                if (NatA != null)
                {
                    try { NatA.Dispose(); } catch { }
                }

                if (NatB != null)
                {
                    try { NatB.Dispose(); } catch { }
                }
            }
        }
    }
}
