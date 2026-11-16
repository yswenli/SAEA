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
*文件名： TcpMatrixTests
*版本号： net10.0
*唯一标识：fb1f9833-4e9d-4bcf-8b5e-43f3cd1bbe53
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 16:44:01
*描述：TCP 直连矩阵用例：NAT 拨出、被动侧端口保持与 TCP 失败后的 TURN 回退
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 10:21:07
*修改人： yswenli
*版本号： net10.0
*描述：通道类型断言改为 ICE 选路结果
*
 *****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Channel;
using SAEA.P2P.Ice;
using SAEA.P2P.StunServer;
using SAEA.P2P.Tests.Nat;
using SAEA.P2P.Transport;
using SAEA.P2P.Turn;
using SAEA.P2P.TurnServer;

using StunServerHost = SAEA.P2P.StunServer.StunServer;
using TurnServerHost = SAEA.P2P.TurnServer.TurnServer;

namespace SAEA.P2P.Tests.Ice
{
    /// <summary>
    /// TCP 直连矩阵用例：验证内部主机经用户态 NAT 拨出、被动侧端口保持可被连接，
    /// 以及 TCP 直连失败后回退到 TURN over UDP 中继。
    /// </summary>
    [TestCategory(P2PTestSuite.Matrix)]
    [TestClass]
    public class TcpMatrixTests
    {
        /// <summary>
        /// 主机 A 的 NAT 内部地址
        /// </summary>
        private static readonly IPAddress HostA = IPAddress.Parse("127.0.0.2");

        /// <summary>
        /// 主机 B 的 NAT 内部地址
        /// </summary>
        private static readonly IPAddress HostB = IPAddress.Parse("127.0.0.3");

        /// <summary>
        /// NAT A 的外部地址
        /// </summary>
        private static readonly IPAddress NatA = IPAddress.Parse("127.0.0.4");

        /// <summary>
        /// NAT B 的外部地址
        /// </summary>
        private static readonly IPAddress NatB = IPAddress.Parse("127.0.0.5");

        /// <summary>
        /// TURN 服务地址，同时用作中继地址
        /// </summary>
        private static readonly IPAddress TurnAddress = IPAddress.Parse("127.0.0.7");

        /// <summary>
        /// 外部探测源地址
        /// </summary>
        private static readonly IPAddress ProbeAddress = IPAddress.Parse("127.0.0.8");

        /// <summary>
        /// 本场景允许出现的全部地址，用于拦截把流量打到真实网络或公网地址
        /// </summary>
        private static readonly IPAddress[] ScenarioAddresses = new IPAddress[]
        {
            IPAddress.Parse("127.0.0.2"),
            IPAddress.Parse("127.0.0.3"),
            IPAddress.Parse("127.0.0.4"),
            IPAddress.Parse("127.0.0.5"),
            IPAddress.Parse("127.0.0.6"),
            IPAddress.Parse("127.0.0.7"),
            IPAddress.Parse("127.0.0.8")
        };

        /// <summary>
        /// TURN 测试用共享密钥
        /// </summary>
        private const string TurnSharedSecret = "shared-secret-for-tcp-matrix";

        /// <summary>
        /// TURN 测试用认证域
        /// </summary>
        private const string TurnRealm = "saea.p2p";

        /// <summary>
        /// 控制方 ufrag
        /// </summary>
        private const string UfragA = "tcpMatrixUfragA";

        /// <summary>
        /// 控制方短凭据口令
        /// </summary>
        private const string PwdA = "tcpMatrixPasswordA0123456789";

        /// <summary>
        /// 受控方 ufrag
        /// </summary>
        private const string UfragB = "tcpMatrixUfragB";

        /// <summary>
        /// 受控方短凭据口令
        /// </summary>
        private const string PwdB = "tcpMatrixPasswordB0123456789";

        /// <summary>
        /// 等待选路完成的上限，比会话总预算更宽，避免收集与检查变慢时误判
        /// </summary>
        private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(35);

        /// <summary>
        /// 等待业务帧到达的上限
        /// </summary>
        private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 主动侧经 NAT 拨出：端点无关过滤加静态映射，应直连成功并双向转发业务帧
        /// </summary>
        [TestMethod]
        [Timeout(120000)]
        public async Task TcpMatrix_active对passive_直连成功()
        {
            using var natA = new NatBox(NatA, NatPreset.TcpMapped());
            using var natB = new NatBox(NatB, NatPreset.TcpMapped());

            using var listener = new SaeaTcpTransport(new IPEndPoint(HostB, 0));
            var passivePort = listener.LocalEndPoint.Port;
            var mapping = natB.AddStaticTcpMapping(listener.LocalEndPoint, passivePort);

            Assert.AreEqual(passivePort, mapping.ExternalEndPoint.Port, "静态映射应保持被动侧内部监听端口");
            Assert.IsTrue(mapping.ExternalEndPoint.Address.Equals(NatB), "静态映射应监听 NAT B 的外部地址");

            // 期望由本文件独立判定函数推出，不直接硬写结果
            var expected = EvaluateTcpCell(true, true);
            Assert.AreEqual(MatrixOutcome.Direct, expected, "静态映射与端点无关过滤同时满足时 TCP 应可直连");

            using var active = new TcpDirectChannel("ufragA", "pwdA", () => new NatTcpClientTransport(natA.InternalTcpGateway));
            using var passive = new TcpDirectChannel("ufragB", "pwdB");

            var connectTask = active.ConnectAsync(
                new IPEndPoint(HostA, 0),
                new IPEndPoint(NatB, passivePort),
                "ufragB",
                "pwdB",
                IceRole.Controlling,
                12345UL,
                IcePriority.ComputeForTcp(CandidateType.Host, 1, TcpCandidateType.Active),
                true,
                CancellationToken.None);

            var accepted = await WaitAsync(
                listener.AcceptAsync(CancellationToken.None),
                TimeSpan.FromSeconds(10),
                "被动侧接受 TCP 连接").ConfigureAwait(false);
            var attachTask = passive.AttachAsync(accepted, CancellationToken.None);

            // 两个任务同时存在：先等连接侧检查，再等接受侧检查，串行等待会互相卡住
            var connectResult = await WaitAsync(connectTask, TimeSpan.FromSeconds(30), "主动侧 TCP 连接检查").ConfigureAwait(false);
            var attachResult = await WaitAsync(attachTask, TimeSpan.FromSeconds(30), "被动侧 TCP 连接检查").ConfigureAwait(false);

            Assert.IsTrue(connectResult.Succeeded, "主动侧检查应通过：" + connectResult.Failure + " " + connectResult.ErrorReason);
            Assert.IsTrue(attachResult.Succeeded, "被动侧检查应通过：" + attachResult.Failure + " " + attachResult.ErrorReason);
            Assert.IsTrue(active.IsConnected, "主动侧应处在数据面可用状态");
            Assert.IsTrue(passive.IsConnected, "被动侧应处在数据面可用状态");

            Assert.IsTrue(IsScenarioAddress(connectResult.MappedAddress), "主动侧拿到的映射地址应落在场景地址内：" + connectResult.MappedAddress);
            Assert.IsTrue(IsScenarioAddress(active.RemoteEndPoint), "主动侧远端端点应落在场景地址内：" + active.RemoteEndPoint);
            Assert.IsTrue(IsScenarioAddress(passive.LocalEndPoint), "被动侧本地端点应落在场景地址内：" + passive.LocalEndPoint);

            var passiveReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            passive.FrameReceived += frame => passiveReceived.TrySetResult(frame.ToArray());

            active.Send(new byte[] { 1, 2, 3 });

            var passiveFrame = await WaitAsync(passiveReceived.Task, FrameTimeout, "被动侧接收主动侧业务帧").ConfigureAwait(false);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, passiveFrame, "被动侧经 NAT 收到的载荷应原样");

            var activeReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            active.FrameReceived += frame => activeReceived.TrySetResult(frame.ToArray());

            passive.Send(new byte[] { 9, 8 });

            var activeFrame = await WaitAsync(activeReceived.Task, FrameTimeout, "主动侧接收被动侧业务帧").ConfigureAwait(false);
            CollectionAssert.AreEqual(new byte[] { 9, 8 }, activeFrame, "主动侧经 NAT 收到的载荷应原样");
        }

        /// <summary>
        /// 被动侧端口保持：外部探测源直连 NAT 外部端口应被转发到内部监听器并双向互通
        /// </summary>
        [TestMethod]
        [Timeout(60000)]
        public async Task TcpMatrix_passive侧端口保持_可被连接()
        {
            using var natB = new NatBox(NatB, NatPreset.TcpMapped());
            using var listener = new SaeaTcpTransport(new IPEndPoint(HostB, 0));

            var mapping = natB.AddStaticTcpMapping(listener.LocalEndPoint, listener.LocalEndPoint.Port);

            Assert.AreEqual(MatrixOutcome.Direct, EvaluateTcpCell(true, true), "静态映射与端点无关过滤同时满足时应可直连");
            Assert.IsTrue(mapping.ExternalEndPoint.Address.Equals(NatB), "静态映射应监听 NAT B 的外部地址");
            Assert.AreEqual(listener.LocalEndPoint.Port, mapping.ExternalEndPoint.Port, "外部端口应与内部监听端口相同（端口保持）");

            var probe = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            ITcpTransport accepted = null;

            try
            {
                probe.Bind(new IPEndPoint(ProbeAddress, 0));
                await WaitAsync(probe.ConnectAsync(mapping.ExternalEndPoint), TimeSpan.FromSeconds(10), "外部探测源连接 NAT 静态映射").ConfigureAwait(false);

                accepted = await WaitAsync(
                    listener.AcceptAsync(CancellationToken.None),
                    TimeSpan.FromSeconds(10),
                    "被动侧接受 NAT 转入的连接").ConfigureAwait(false);

                await probe.SendAsync(new ArraySegment<byte>(new byte[] { 0x5A }), SocketFlags.None).ConfigureAwait(false);

                var received = await WaitAsync(ReadStreamAsync(accepted.Stream, 1), FrameTimeout, "被动侧读取探测源写入的字节").ConfigureAwait(false);
                CollectionAssert.AreEqual(new byte[] { 0x5A }, received, "被动侧读回的字节应与探测源写入的一致");

                await accepted.Stream.WriteAsync(new byte[] { 0x5A }, 0, 1).ConfigureAwait(false);
                await accepted.Stream.FlushAsync().ConfigureAwait(false);

                var echoed = await WaitAsync(ReceiveSocketAsync(probe, 1), FrameTimeout, "探测源读取被动侧回写的字节").ConfigureAwait(false);
                CollectionAssert.AreEqual(new byte[] { 0x5A }, echoed, "探测源读回的字节应与被动侧回写的一致");
            }
            finally
            {
                try { probe.Dispose(); } catch { }
                if (accepted != null)
                {
                    try { accepted.Dispose(); } catch { }
                }
            }
        }

        /// <summary>
        /// TCP 同时打开在本机不可用，标记跳过并记录原因，不静默忽略该格
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public void TcpMatrix_so组合_标记跳过并记录原因()
        {
            Assert.Inconclusive("Windows 不支持 TCP 同时打开（D12）：本机不能同时作为主动与被动侧发起 so 连接，该格不参与判定，跳过而非静默忽略。");
        }

        /// <summary>
        /// TCP 直连失败回退：对端只有不可达的 TCP 被动候选与 relay 候选，两级直连都失败，应落 TURN over UDP 中继
        /// </summary>
        [TestMethod]
        [Timeout(120000)]
        public async Task TcpMatrix_TCP失败_回退TURN_UDP()
        {
            IceSession controller = null;
            IceSession controlled = null;
            StunServerHost stunHost = null;
            TurnServerHost turnServer = null;

            try
            {
                // 中继端口区间收窄到预留的 4 个端口，两端各占一个 allocation
                var relayPorts = ReserveRelayPorts(4);

                turnServer = new TurnServerHost(new TurnServerOptions
                {
                    SharedSecret = TurnSharedSecret,
                    Realm = TurnRealm,
                    RelayAddress = TurnAddress,
                    RelayPortMin = relayPorts[0],
                    RelayPortMax = relayPorts[relayPorts.Length - 1]
                });

                var stunOptions = new StunServerOptions(new IPEndPoint(TurnAddress, 0))
                {
                    RequestHandler = turnServer.HandleRequest,
                    DatagramHandler = turnServer.HandleDatagram
                };
                stunHost = new StunServerHost(stunOptions);
                await stunHost.StartAsync(CancellationToken.None).ConfigureAwait(false);
                var turnEndPoint = stunHost.Endpoints[0].LocalEndPoint;

                // 两端各自申请一份分配，凭据分别由服务端凭据服务按用户名签发
                var controllerTurn = new TurnClientOptions
                {
                    ServerEndPoint = turnEndPoint,
                    TransportFactory = () => new UdpTurnClientTransport(new IPEndPoint(HostA, 0), turnEndPoint),
                    Credential = turnServer.CredentialService.Create("tcp-matrix-user-a"),
                    RtoMilliseconds = 100,
                    AllocationRefreshInterval = TimeSpan.FromSeconds(30),
                    PermissionRefreshInterval = TimeSpan.FromSeconds(30),
                    ChannelRefreshInterval = TimeSpan.FromSeconds(30),
                    RefreshTimeout = TimeSpan.FromSeconds(10)
                };
                var controlledTurn = new TurnClientOptions
                {
                    ServerEndPoint = turnEndPoint,
                    TransportFactory = () => new UdpTurnClientTransport(new IPEndPoint(HostB, 0), turnEndPoint),
                    Credential = turnServer.CredentialService.Create("tcp-matrix-user-b"),
                    RtoMilliseconds = 100,
                    AllocationRefreshInterval = TimeSpan.FromSeconds(30),
                    PermissionRefreshInterval = TimeSpan.FromSeconds(30),
                    ChannelRefreshInterval = TimeSpan.FromSeconds(30),
                    RefreshTimeout = TimeSpan.FromSeconds(10)
                };

                controller = new IceSession(CreateTurnOptions(HostA, controllerTurn), CreateParameters(true, UfragA, PwdA, UfragB, PwdB, 1));
                controlled = new IceSession(CreateTurnOptions(HostB, controlledTurn), CreateParameters(false, UfragB, PwdB, UfragA, PwdA, 2));

                // 每个会话只拿到一个不可达的 TCP 被动候选：UDP 阶段拿不到对端 host 候选，TCP 阶段只能连死端口
                controller.AddRemoteCandidate(new Candidate(
                    CandidateType.Host,
                    IceTransportType.Tcp,
                    1,
                    NatA,
                    ReserveDeadTcpPort(NatA),
                    TcpCandidateType.Passive,
                    null,
                    0));
                controlled.AddRemoteCandidate(new Candidate(
                    CandidateType.Host,
                    IceTransportType.Tcp,
                    1,
                    NatB,
                    ReserveDeadTcpPort(NatB),
                    TcpCandidateType.Passive,
                    null,
                    0));

                // 只转发 relay 候选：两端都拿不到对端 host 候选，UDP 与 TCP 直连必然失败，只能走 TURN 中继
                controller.LocalCandidateDiscovered += candidate =>
                {
                    if (candidate.Type == CandidateType.Relay && candidate.Transport == IceTransportType.Udp)
                    {
                        controlled.AddRemoteCandidate(candidate);
                    }
                };
                controlled.LocalCandidateDiscovered += candidate =>
                {
                    if (candidate.Type == CandidateType.Relay && candidate.Transport == IceTransportType.Udp)
                    {
                        controller.AddRemoteCandidate(candidate);
                    }
                };

                var controllerFrames = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                var controlledFrames = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                controller.FrameReceived += CreateFrameHandler(controllerFrames);
                controlled.FrameReceived += CreateFrameHandler(controlledFrames);

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
                {
                    // 受控方只等提名，先进 TURN 阶段完成分配，随后控制方才开始选路
                    var controlledTask = controlled.StartAsync(cts.Token);
                    var controllerTask = controller.StartAsync(cts.Token);

                    var controllerResult = await WaitAsync(controllerTask, StartTimeout, "控制方 StartAsync").ConfigureAwait(false);
                    var controlledResult = await WaitAsync(controlledTask, StartTimeout, "受控方 StartAsync").ConfigureAwait(false);

                    Assert.AreEqual(IceStage.TurnUdp, controllerResult.Stage, "控制方应落在 TURN over UDP 阶段");
                    Assert.AreEqual(IceStage.TurnUdp, controlledResult.Stage, "受控方应落在 TURN over UDP 阶段");

                    Assert.IsTrue(controller.IsConnected, "控制方中继选路完成后 IsConnected 应为 true");
                    Assert.IsTrue(controlled.IsConnected, "受控方中继选路完成后 IsConnected 应为 true");

                    Assert.IsTrue(controllerResult.IsRelay, "控制方应经中继转发");
                    Assert.IsTrue(controlledResult.IsRelay, "受控方应经中继转发");
                    Assert.AreEqual(ChannelType.RelayUdp, controllerResult.ChannelType, "控制方通道类型应为 UDP 中继");
                    Assert.AreEqual(ChannelType.RelayUdp, controlledResult.ChannelType, "受控方通道类型应为 UDP 中继");
                    Assert.IsFalse(controllerResult.UsesTcp, "控制方数据面不应使用 TCP");
                    Assert.IsFalse(controlledResult.UsesTcp, "受控方数据面不应使用 TCP");

                    Assert.AreEqual(TurnAddress, controllerResult.LocalEndPoint.Address, "控制方本地端点应为中继地址");
                    Assert.AreEqual(TurnAddress, controllerResult.RemoteEndPoint.Address, "控制方远端端点应为对端中继地址");
                    Assert.AreEqual(TurnAddress, controlledResult.LocalEndPoint.Address, "受控方本地端点应为中继地址");
                    Assert.AreEqual(TurnAddress, controlledResult.RemoteEndPoint.Address, "受控方远端端点应为对端中继地址");

                    Assert.IsTrue(
                        controllerResult.LocalEndPoint.Port >= relayPorts[0] && controllerResult.LocalEndPoint.Port <= relayPorts[relayPorts.Length - 1],
                        "控制方中继端口应落在预留区间内");
                    Assert.IsTrue(
                        controlledResult.LocalEndPoint.Port >= relayPorts[0] && controlledResult.LocalEndPoint.Port <= relayPorts[relayPorts.Length - 1],
                        "受控方中继端口应落在预留区间内");

                    // TCP 阶段确实收集并发布了本端候选，回退到中继不是因为该阶段空转
                    Assert.IsTrue(controller.LocalCandidates.Any(c => c.Transport == IceTransportType.Tcp), "控制方应有 TCP 候选参与选路");
                    Assert.IsTrue(controlled.LocalCandidates.Any(c => c.Transport == IceTransportType.Tcp), "受控方应有 TCP 候选参与选路");

                    var payload1 = Encoding.UTF8.GetBytes("tcp-matrix-hello 控制方经中继问好");
                    controller.Send(payload1);
                    var received1 = await WaitAsync(controlledFrames.Task, FrameTimeout, "受控方接收控制方中继业务帧").ConfigureAwait(false);
                    CollectionAssert.AreEqual(payload1, received1, "受控方经中继收到的业务帧与控制方发送的内容不一致");

                    var payload2 = Encoding.UTF8.GetBytes("tcp-matrix-reply 受控方经中继回执");
                    controlled.Send(payload2);
                    var received2 = await WaitAsync(controllerFrames.Task, FrameTimeout, "控制方接收受控方中继业务帧").ConfigureAwait(false);
                    CollectionAssert.AreEqual(payload2, received2, "控制方经中继收到的业务帧与受控方发送的内容不一致");
                }
            }
            finally
            {
                if (controller != null)
                {
                    controller.Dispose();
                }

                if (controlled != null)
                {
                    controlled.Dispose();
                }

                if (stunHost != null)
                {
                    stunHost.Dispose();
                }

                if (turnServer != null)
                {
                    turnServer.Dispose();
                }
            }
        }

        /// <summary>
        /// 测试内独立的期望判定：不调用被测 ICE 代码，只按 TCP 的入向约束推导结果
        /// </summary>
        /// <param name="passiveHasStaticMapping">被动侧是否存在静态端口映射</param>
        /// <param name="passiveFilterAllowsAnyExternal">被动侧过滤是否放行任意外部地址</param>
        /// <returns>直连或中继</returns>
        private static MatrixOutcome EvaluateTcpCell(bool passiveHasStaticMapping, bool passiveFilterAllowsAnyExternal)
        {
            // 被动侧没有静态映射时外部无法主动建链
            if (!passiveHasStaticMapping) return MatrixOutcome.Relay;

            // TCP 无法先出后入：过滤只放行已通信过的外部地址时，主动侧的首个 SYN 仍会被丢弃
            if (!passiveFilterAllowsAnyExternal) return MatrixOutcome.Relay;

            return MatrixOutcome.Direct;
        }

        /// <summary>
        /// 判断端点地址是否属于本场景的回环地址段
        /// </summary>
        /// <param name="endPoint">待判定端点</param>
        /// <returns>属于场景地址时为 true</returns>
        private static bool IsScenarioAddress(IPEndPoint endPoint)
        {
            if (endPoint == null) return false;
            return ScenarioAddresses.Contains(endPoint.Address);
        }

        /// <summary>
        /// 构造走 TURN 中继的会话配置：直连阶段按短预算收口，TURN 阶段 8s，总预算容纳收集与三级 TURN 阶段
        /// </summary>
        /// <param name="bind">本端绑定地址</param>
        /// <param name="turn">TURN 客户端配置</param>
        /// <returns>可直接传给会话的配置</returns>
        private static IceSessionOptions CreateTurnOptions(IPAddress bind, TurnClientOptions turn)
        {
            var ice = new IceOptions();
            ice.AllowLoopbackCandidates = true;
            ice.BindAddresses.Add(bind);
            ice.GatheringTimeout = TimeSpan.FromSeconds(2);

            var options = new IceSessionOptions();
            options.Ice = ice;
            options.GatheringBudget = TimeSpan.FromSeconds(1);
            options.UdpDirectBudget = TimeSpan.FromSeconds(1);
            options.TcpDirectBudget = TimeSpan.FromSeconds(3);
            options.TurnStageBudget = TimeSpan.FromSeconds(8);
            options.SessionBudget = TimeSpan.FromSeconds(40);
            options.Turn = turn;
            return options;
        }

        /// <summary>
        /// 构造 ICE 会话参数：角色、本地与对端短凭据、角色冲突用的 tiebreaker
        /// </summary>
        /// <param name="controlling">是否为控制方</param>
        /// <param name="localUfrag">本端 ufrag</param>
        /// <param name="localPwd">本端短凭据口令</param>
        /// <param name="remoteUfrag">对端 ufrag</param>
        /// <param name="remotePwd">对端短凭据口令</param>
        /// <param name="tiebreaker">角色冲突时使用的 tiebreaker</param>
        /// <returns>可直接传给会话的参数</returns>
        private static IceSessionParameters CreateParameters(
            bool controlling,
            string localUfrag,
            string localPwd,
            string remoteUfrag,
            string remotePwd,
            ulong tiebreaker)
        {
            var parameters = new IceSessionParameters();
            parameters.LocalUfrag = localUfrag;
            parameters.LocalPassword = localPwd;
            parameters.RemoteUfrag = remoteUfrag;
            parameters.RemotePassword = remotePwd;
            parameters.Role = controlling ? IceRole.Controlling : IceRole.Controlled;
            parameters.Tiebreaker = tiebreaker;
            return parameters;
        }

        /// <summary>
        /// 等待任务完成，超时直接判失败并带上场景名，便于定位卡在哪一步
        /// </summary>
        /// <param name="task">待等待的任务</param>
        /// <param name="timeout">等待上限</param>
        /// <param name="scene">失败信息里的场景描述</param>
        /// <returns>任务结果</returns>
        private static async Task<T> WaitAsync<T>(Task<T> task, TimeSpan timeout, string scene)
        {
            var completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
            if (!ReferenceEquals(completed, task))
            {
                Assert.Fail("等待超时：" + scene);
            }

            return await task.ConfigureAwait(false);
        }

        /// <summary>
        /// 等待无返回值的任务完成，超时直接判失败并带上场景名
        /// </summary>
        /// <param name="task">待等待的任务</param>
        /// <param name="timeout">等待上限</param>
        /// <param name="scene">失败信息里的场景描述</param>
        /// <returns>等待完成的任务</returns>
        private static async Task WaitAsync(Task task, TimeSpan timeout, string scene)
        {
            var completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
            if (!ReferenceEquals(completed, task))
            {
                Assert.Fail("等待超时：" + scene);
            }

            await task.ConfigureAwait(false);
        }

        /// <summary>
        /// 构造业务帧处理器：只保留第一帧，避免后续帧覆盖本次断言的样本
        /// </summary>
        /// <param name="source">接收完成源</param>
        /// <returns>可挂到 FrameReceived 上的处理器</returns>
        private static Action<ReadOnlyMemory<byte>> CreateFrameHandler(TaskCompletionSource<byte[]> source)
        {
            var completed = 0;
            return frame =>
            {
                if (Interlocked.Exchange(ref completed, 1) != 0)
                {
                    return;
                }

                source.TrySetResult(frame.ToArray());
            };
        }

        /// <summary>
        /// 从字节流读满指定字节数；读到流结束说明对端提前关闭，直接判失败
        /// </summary>
        /// <param name="stream">字节流</param>
        /// <param name="count">期望字节数</param>
        /// <returns>读到的字节</returns>
        private static async Task<byte[]> ReadStreamAsync(Stream stream, int count)
        {
            var buffer = new byte[count];
            var offset = 0;

            while (offset < count)
            {
                var read = await stream.ReadAsync(buffer, offset, count - offset).ConfigureAwait(false);
                if (read <= 0)
                {
                    Assert.Fail("字节流提前结束，期望 " + count + " 字节，实际 " + offset + " 字节");
                }

                offset += read;
            }

            return buffer;
        }

        /// <summary>
        /// 从套接字读满指定字节数；读到对端关闭直接判失败
        /// </summary>
        /// <param name="socket">已连接的套接字</param>
        /// <param name="count">期望字节数</param>
        /// <returns>读到的字节</returns>
        private static async Task<byte[]> ReceiveSocketAsync(Socket socket, int count)
        {
            var buffer = new byte[count];
            var offset = 0;

            while (offset < count)
            {
                var read = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, count - offset), SocketFlags.None).ConfigureAwait(false);
                if (read <= 0)
                {
                    Assert.Fail("套接字已关闭，期望 " + count + " 字节，实际 " + offset + " 字节");
                }

                offset += read;
            }

            return buffer;
        }

        /// <summary>
        /// 在 TURN 地址上取若干个空闲 UDP 端口并立即释放，用于收窄中继端口区间
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
                    probe.BindAsync(new IPEndPoint(TurnAddress, 0), CancellationToken.None).GetAwaiter().GetResult();
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
        /// 取一个确定无人监听的 TCP 端口：绑定后立即停止监听
        /// </summary>
        /// <param name="address">绑定地址</param>
        /// <returns>已释放的端口号</returns>
        private static int ReserveDeadTcpPort(IPAddress address)
        {
            var listener = new TcpListener(address, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}