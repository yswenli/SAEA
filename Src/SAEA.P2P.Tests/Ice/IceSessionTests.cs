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
*文件名： IceSessionTests
*版本号： net10.0
*唯一标识：5105406a-32df-42b8-aecb-2fd964f2160c
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 15:00:08
*描述：ICE 会话端到端用例：UDP 直连、TCP 回退与启动校验
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
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Channel;
using SAEA.P2P.Common;
using SAEA.P2P.Ice;
using SAEA.P2P.StunServer;
using SAEA.P2P.Transport;
using SAEA.P2P.Turn;
using SAEA.P2P.TurnServer;

using StunServerHost = SAEA.P2P.StunServer.StunServer;
using TurnServerHost = SAEA.P2P.TurnServer.TurnServer;

namespace SAEA.P2P.Tests.Ice
{
    /// <summary>
    /// ICE 会话端到端用例：两个回环地址上的会话互相 trickle 候选，
    /// 分别验证 UDP 直连选路与业务帧双向转发、UDP 不通时回退 TCP 直连、只有中继候选时落 TURN over UDP，
    /// 控制连接改走 TCP 时落 TURN over TCP、改走 TLS 时落 TURN over TLS，以及未连接时的发送与重复启动校验。
    /// UDP 中继拿不到对端候选时在 TURN over TCP 级内改走 RFC 6062 TCP 中继。
    /// TLS 控制下同样在级内改走 RFC 6062，数据连接与控制连接同层加密。
    /// </summary>
    [TestCategory(P2PTestSuite.Unit)]
    [TestClass]
    public class IceSessionTests
    {
        /// <summary>
        /// 控制方绑定地址
        /// </summary>
        private static readonly IPAddress HostA = IPAddress.Parse("127.0.0.41");

        /// <summary>
        /// 受控方绑定地址
        /// </summary>
        private static readonly IPAddress HostB = IPAddress.Parse("127.0.0.42");

        /// <summary>
        /// TURN 中继对外地址，也是 STUN/TURN 监听地址
        /// </summary>
        private static readonly IPAddress TurnAddress = IPAddress.Parse("127.0.0.50");

        /// <summary>
        /// TURN 测试用共享密钥
        /// </summary>
        private const string TurnSharedSecret = "shared-secret-for-ice-session";

        /// <summary>
        /// TURN 测试用认证域
        /// </summary>
        private const string TurnRealm = "saea.p2p";

        /// <summary>
        /// TURN over TLS 测试证书口令
        /// </summary>
        private const string CertificatePassword = "saea-turn-tls-test-password";

        /// <summary>
        /// 控制方 ufrag
        /// </summary>
        private const string UfragA = "ufragA";

        /// <summary>
        /// 控制方短凭据口令
        /// </summary>
        private const string PwdA = "passwordA0123456789";

        /// <summary>
        /// 受控方 ufrag
        /// </summary>
        private const string UfragB = "ufragB";

        /// <summary>
        /// 受控方短凭据口令
        /// </summary>
        private const string PwdB = "passwordB0123456789";

        /// <summary>
        /// 等待选路完成的上限，比会话总预算更宽，避免收集与检查变慢时误判
        /// </summary>
        private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(35);

        /// <summary>
        /// 等待业务帧到达的上限
        /// </summary>
        private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// UDP 直连：两端只 trickle UDP host 候选，应选中 UDP 直连并双向转发业务帧
        /// </summary>
        [TestMethod]
        [Timeout(60000)]
        public async Task StartAsync_UdpDirectPair_SelectsPairAndRelaysFrames()
        {
            IceSession controller = null;
            IceSession controlled = null;

            try
            {
                controller = CreateSession(HostA, TimeSpan.FromSeconds(5), true, UfragA, PwdA, UfragB, PwdB, 1);
                controlled = CreateSession(HostB, TimeSpan.FromSeconds(5), false, UfragB, PwdB, UfragA, PwdA, 2);

                // 只转发 UDP host 候选，避免 IPv6 或其它传输候选干扰本用例要验证的 UDP 直连路径
                controller.LocalCandidateDiscovered += candidate =>
                {
                    if (candidate.Transport == IceTransportType.Udp && candidate.Type == CandidateType.Host)
                    {
                        controlled.AddRemoteCandidate(candidate);
                    }
                };
                controlled.LocalCandidateDiscovered += candidate =>
                {
                    if (candidate.Transport == IceTransportType.Udp && candidate.Type == CandidateType.Host)
                    {
                        controller.AddRemoteCandidate(candidate);
                    }
                };

                var controllerFrames = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                var controlledFrames = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                controller.FrameReceived += CreateFrameHandler(controllerFrames);
                controlled.FrameReceived += CreateFrameHandler(controlledFrames);

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                {
                    var controllerTask = controller.StartAsync(cts.Token);
                    var controlledTask = controlled.StartAsync(cts.Token);

                    // 分别等待而不合并，超时失败时才能定位是哪一端没走出选路
                    var controllerResult = await WaitAsync(controllerTask, StartTimeout, "控制方 StartAsync").ConfigureAwait(false);
                    var controlledResult = await WaitAsync(controlledTask, StartTimeout, "受控方 StartAsync").ConfigureAwait(false);

                    Assert.AreEqual(IceStage.UdpDirect, controllerResult.Stage, "控制方应选中 UDP 直连阶段");
                    Assert.AreEqual(IceStage.UdpDirect, controlledResult.Stage, "受控方应选中 UDP 直连阶段");

                    Assert.IsTrue(controller.IsConnected, "控制方选路完成后 IsConnected 应为 true");
                    Assert.IsTrue(controlled.IsConnected, "受控方选路完成后 IsConnected 应为 true");

                    Assert.IsFalse(controllerResult.IsRelay, "控制方 UDP 直连不应是中继通道");
                    Assert.IsFalse(controlledResult.IsRelay, "受控方 UDP 直连不应是中继通道");
                    Assert.IsFalse(controllerResult.UsesTcp, "控制方 UDP 直连不应使用 TCP");
                    Assert.IsFalse(controlledResult.UsesTcp, "受控方 UDP 直连不应使用 TCP");

                    Assert.AreEqual(HostA, controllerResult.LocalEndPoint.Address, "控制方本地端点地址应为绑定地址 " + HostA);
                    Assert.AreEqual(HostB, controllerResult.RemoteEndPoint.Address, "控制方远端端点地址应为对端绑定地址 " + HostB);
                    Assert.AreEqual(HostB, controlledResult.LocalEndPoint.Address, "受控方本地端点地址应为绑定地址 " + HostB);
                    Assert.AreEqual(HostA, controlledResult.RemoteEndPoint.Address, "受控方远端端点地址应为对端绑定地址 " + HostA);

                    var payload1 = Encoding.UTF8.GetBytes("udp-direct-hello 控制方直连问好");
                    controller.Send(payload1);
                    var received1 = await WaitAsync(controlledFrames.Task, FrameTimeout, "受控方接收控制方业务帧").ConfigureAwait(false);
                    CollectionAssert.AreEqual(payload1, received1, "受控方收到的业务帧与控制方发送的内容不一致");

                    var payload2 = Encoding.UTF8.GetBytes("udp-direct-reply 受控方直连回执");
                    controlled.Send(payload2);
                    var received2 = await WaitAsync(controllerFrames.Task, FrameTimeout, "控制方接收受控方业务帧").ConfigureAwait(false);
                    CollectionAssert.AreEqual(payload2, received2, "控制方收到的业务帧与受控方发送的内容不一致");
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
            }
        }

        /// <summary>
        /// UDP 不通回退：两端只 trickle TCP 候选，UDP 阶段必然失败，应回退到 TCP 直连并双向转发业务帧
        /// </summary>
        [TestMethod]
        [Timeout(60000)]
        public async Task StartAsync_UdpBlocked_FallsBackToTcpDirect()
        {
            IceSession controller = null;
            IceSession controlled = null;

            try
            {
                controller = CreateSession(HostA, TimeSpan.FromSeconds(1), true, UfragA, PwdA, UfragB, PwdB, 1);
                controlled = CreateSession(HostB, TimeSpan.FromSeconds(1), false, UfragB, PwdB, UfragA, PwdA, 2);

                // 只转发 TCP 候选，UDP 阶段拿不到对端候选，必然落到 TCP 直连阶段
                controller.LocalCandidateDiscovered += candidate =>
                {
                    if (candidate.Transport == IceTransportType.Tcp)
                    {
                        controlled.AddRemoteCandidate(candidate);
                    }
                };
                controlled.LocalCandidateDiscovered += candidate =>
                {
                    if (candidate.Transport == IceTransportType.Tcp)
                    {
                        controller.AddRemoteCandidate(candidate);
                    }
                };

                var controllerFrames = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                var controlledFrames = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                controller.FrameReceived += CreateFrameHandler(controllerFrames);
                controlled.FrameReceived += CreateFrameHandler(controlledFrames);

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                {
                    // 受控方先进 TCP 被动阶段等待接入，随后控制方才发起主动连接
                    var controlledTask = controlled.StartAsync(cts.Token);
                    var controllerTask = controller.StartAsync(cts.Token);

                    var controllerResult = await WaitAsync(controllerTask, StartTimeout, "控制方 StartAsync").ConfigureAwait(false);
                    var controlledResult = await WaitAsync(controlledTask, StartTimeout, "受控方 StartAsync").ConfigureAwait(false);

                    Assert.AreEqual(IceStage.TcpDirect, controllerResult.Stage, "控制方应回退到 TCP 直连阶段");
                    Assert.AreEqual(IceStage.TcpDirect, controlledResult.Stage, "受控方应回退到 TCP 直连阶段");

                    Assert.IsTrue(controller.IsConnected, "控制方 TCP 直连完成后 IsConnected 应为 true");
                    Assert.IsTrue(controlled.IsConnected, "受控方 TCP 直连完成后 IsConnected 应为 true");

                    Assert.IsFalse(controllerResult.IsRelay, "控制方 TCP 直连不应是中继通道");
                    Assert.IsFalse(controlledResult.IsRelay, "受控方 TCP 直连不应是中继通道");
                    Assert.IsTrue(controllerResult.UsesTcp, "控制方 TCP 直连应标记使用 TCP");
                    Assert.IsTrue(controlledResult.UsesTcp, "受控方 TCP 直连应标记使用 TCP");

                    // 主动候选的本地端口由连接时分配，候选里记 0，这里只比对地址
                    Assert.AreEqual(HostA, controllerResult.LocalEndPoint.Address, "控制方本地端点地址应为绑定地址 " + HostA);
                    Assert.AreEqual(HostB, controllerResult.RemoteEndPoint.Address, "控制方远端端点地址应为对端绑定地址 " + HostB);
                    Assert.AreEqual(HostB, controlledResult.LocalEndPoint.Address, "受控方本地端点地址应为绑定地址 " + HostB);
                    Assert.AreEqual(HostA, controlledResult.RemoteEndPoint.Address, "受控方远端端点地址应为对端主动候选地址 " + HostA);

                    var payload1 = Encoding.UTF8.GetBytes("tcp-direct-hello 控制方回退问好");
                    controller.Send(payload1);
                    var received1 = await WaitAsync(controlledFrames.Task, FrameTimeout, "受控方接收控制方业务帧").ConfigureAwait(false);
                    CollectionAssert.AreEqual(payload1, received1, "受控方经 TCP 收到的业务帧与控制方发送的内容不一致");

                    var payload2 = Encoding.UTF8.GetBytes("tcp-direct-reply 受控方回退回执");
                    controlled.Send(payload2);
                    var received2 = await WaitAsync(controllerFrames.Task, FrameTimeout, "控制方接收受控方业务帧").ConfigureAwait(false);
                    CollectionAssert.AreEqual(payload2, received2, "控制方经 TCP 收到的业务帧与受控方发送的内容不一致");
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
            }
        }

        /// <summary>
        /// 生命周期校验：未选出通道时发送抛 NotConnected，会话只能启动一次，重复释放不抛异常
        /// </summary>
        [TestMethod]
        [Timeout(60000)]
        public async Task Session_NotStarted_SendThrowsAndStartTwiceRejected()
        {
            var session = CreateSession(HostA, TimeSpan.FromSeconds(1), true, UfragA, PwdA, UfragB, PwdB, 7);

            try
            {
                var payload = Encoding.UTF8.GetBytes("not-connected 未连接发送");

                var sendError = Assert.ThrowsExactly<P2PException>(() => session.Send(payload));
                Assert.AreEqual(ErrorCode.NotConnected, sendError.ErrorCode, "未选出通道时发送应返回 EI03 错误码");
                Assert.IsFalse(string.IsNullOrEmpty(sendError.Message), "未选出通道时发送的异常消息不应为空");

                try
                {
                    // 没有任何对端候选，选路必然失败并抛 P2PException，这里只关心"已启动"这一事实
                    await session.StartAsync(CancellationToken.None).ConfigureAwait(false);
                    Assert.Fail("没有对端候选时不应选出可用通道");
                }
                catch (P2PException)
                {
                }

                await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                    () => session.StartAsync(CancellationToken.None)).ConfigureAwait(false);
            }
            finally
            {
                // 释放必须幂等，重复调用不能抛异常
                session.Dispose();
                session.Dispose();
            }

            Assert.IsFalse(session.IsConnected, "未选出通道的会话在释放后 IsConnected 应为 false");
        }

        /// <summary>
        /// TURN over UDP：两端只转发 relay 候选，两级直连拿不到对端候选必然失败，应落到 TURN 中继阶段并双向转发业务帧
        /// </summary>
        [TestMethod]
        [Timeout(120000)]
        public async Task StartAsync_DirectBlocked_SelectsTurnUdpRelay()
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
                    Credential = turnServer.CredentialService.Create("ice-user-a"),
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
                    Credential = turnServer.CredentialService.Create("ice-user-b"),
                    RtoMilliseconds = 100,
                    AllocationRefreshInterval = TimeSpan.FromSeconds(30),
                    PermissionRefreshInterval = TimeSpan.FromSeconds(30),
                    ChannelRefreshInterval = TimeSpan.FromSeconds(30),
                    RefreshTimeout = TimeSpan.FromSeconds(10)
                };

                controller = new IceSession(CreateTurnOptions(HostA, controllerTurn), CreateParameters(true, UfragA, PwdA, UfragB, PwdB, 1));
                controlled = new IceSession(CreateTurnOptions(HostB, controlledTurn), CreateParameters(false, UfragB, PwdB, UfragA, PwdA, 2));

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
                    Assert.IsFalse(controllerResult.UsesTcp, "控制方 TURN over UDP 不应使用 TCP");
                    Assert.IsFalse(controlledResult.UsesTcp, "受控方 TURN over UDP 不应使用 TCP");
                    Assert.AreEqual(ChannelType.RelayUdp, controllerResult.ChannelType, "控制方通道类型应为 UDP 中继");
                    Assert.AreEqual(ChannelType.RelayUdp, controlledResult.ChannelType, "受控方通道类型应为 UDP 中继");

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
                    Assert.AreNotEqual(controllerResult.LocalEndPoint, controllerResult.RemoteEndPoint, "两端是不同的 allocation，中继端口不应相同");

                    var payload1 = Encoding.UTF8.GetBytes("turn-udp-hello 控制方经中继问好");
                    controller.Send(payload1);
                    var received1 = await WaitAsync(controlledFrames.Task, FrameTimeout, "受控方接收控制方中继业务帧").ConfigureAwait(false);
                    CollectionAssert.AreEqual(payload1, received1, "受控方经中继收到的业务帧与控制方发送的内容不一致");

                    var payload2 = Encoding.UTF8.GetBytes("turn-udp-reply 受控方经中继回执");
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
        /// TURN over TCP：服务端只开 TCP 控制监听，UDP 控制的分配收不到应答必然超时，
        /// 应落到 TURN over TCP 阶段，数据面仍是 UDP 中继并双向转发业务帧
        /// </summary>
        [TestMethod]
        [Timeout(120000)]
        public async Task StartAsync_UdpControlBlocked_SelectsTurnOverTcp()
        {
            IceSession controller = null;
            IceSession controlled = null;
            TurnTcpListener tcpListener = null;
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

                // 只开 TCP 控制监听、不建 UDP STUN 监听：第 3 级走 UDP 控制的分配拿不到任何应答
                tcpListener = new TurnTcpListener(new IPEndPoint(TurnAddress, 0), turnServer);
                tcpListener.Start();
                var turnEndPoint = tcpListener.LocalEndPoint;

                // 传输工厂仍是 UDP 控制，这是第 3 级用的配置；第 4 级会由会话换成内置 TCP 控制传输
                var controllerTurn = new TurnClientOptions
                {
                    ServerEndPoint = turnEndPoint,
                    TransportFactory = () => new UdpTurnClientTransport(new IPEndPoint(HostA, 0), turnEndPoint),
                    Credential = turnServer.CredentialService.Create("ice-tcp-user-a"),
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
                    Credential = turnServer.CredentialService.Create("ice-tcp-user-b"),
                    RtoMilliseconds = 100,
                    AllocationRefreshInterval = TimeSpan.FromSeconds(30),
                    PermissionRefreshInterval = TimeSpan.FromSeconds(30),
                    ChannelRefreshInterval = TimeSpan.FromSeconds(30),
                    RefreshTimeout = TimeSpan.FromSeconds(10)
                };

                controller = new IceSession(CreateTurnTcpOptions(HostA, controllerTurn), CreateParameters(true, UfragA, PwdA, UfragB, PwdB, 1));
                controlled = new IceSession(CreateTurnTcpOptions(HostB, controlledTurn), CreateParameters(false, UfragB, PwdB, UfragA, PwdA, 2));

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
                    // 受控方只等提名，先进入中继阶段完成分配，随后控制方才开始选路
                    var controlledTask = controlled.StartAsync(cts.Token);
                    var controllerTask = controller.StartAsync(cts.Token);

                    var controllerResult = await WaitAsync(controllerTask, StartTimeout, "控制方 StartAsync").ConfigureAwait(false);
                    var controlledResult = await WaitAsync(controlledTask, StartTimeout, "受控方 StartAsync").ConfigureAwait(false);

                    // 落在第 4 级说明第 3 级 UDP 控制确实失败，只有 TCP 控制拿到了分配
                    Assert.AreEqual(IceStage.TurnTcp, controllerResult.Stage, "控制方应落在 TURN over TCP 阶段");
                    Assert.AreEqual(IceStage.TurnTcp, controlledResult.Stage, "受控方应落在 TURN over TCP 阶段");

                    Assert.IsTrue(controller.IsConnected, "控制方中继选路完成后 IsConnected 应为 true");
                    Assert.IsTrue(controlled.IsConnected, "受控方中继选路完成后 IsConnected 应为 true");

                    Assert.IsTrue(controllerResult.IsRelay, "控制方应经中继转发");
                    Assert.IsTrue(controlledResult.IsRelay, "受控方应经中继转发");

                    // 只有控制连接走 TCP，数据面仍是 UDP 中继
                    Assert.IsFalse(controllerResult.UsesTcp, "控制方数据面不应使用 TCP");
                    Assert.IsFalse(controlledResult.UsesTcp, "受控方数据面不应使用 TCP");
                    Assert.AreEqual(ChannelType.RelayTcp, controllerResult.ChannelType, "控制方通道类型应为 TCP 中继");
                    Assert.AreEqual(ChannelType.RelayTcp, controlledResult.ChannelType, "受控方通道类型应为 TCP 中继");

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

                    var payload1 = Encoding.UTF8.GetBytes("turn-tcp-hello 控制方经 TCP 控制中继问好");
                    controller.Send(payload1);
                    var received1 = await WaitAsync(controlledFrames.Task, FrameTimeout, "受控方接收控制方中继业务帧").ConfigureAwait(false);
                    CollectionAssert.AreEqual(payload1, received1, "受控方经中继收到的业务帧与控制方发送的内容不一致");

                    var payload2 = Encoding.UTF8.GetBytes("turn-tcp-reply 受控方经 TCP 控制中继回执");
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

                if (tcpListener != null)
                {
                    tcpListener.Dispose();
                }

                if (turnServer != null)
                {
                    turnServer.Dispose();
                }
            }
        }

        /// <summary>
        /// TURN over TLS：服务端只开 TLS 控制监听，UDP 控制拿不到应答、明文 TCP 控制连到 TLS 端口后被握手中断，
        /// 应落到 TURN over TLS 阶段，数据面仍是 UDP 中继并双向转发业务帧
        /// </summary>
        [TestMethod]
        [Timeout(120000)]
        public async Task StartAsync_UdpAndTcpControlBlocked_SelectsTurnOverTls()
        {
            IceSession controller = null;
            IceSession controlled = null;
            TurnTlsListener tlsListener = null;
            TurnServerHost turnServer = null;

            var certificatePath = CreateCertificateFile();

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

                // 只开 TLS 控制监听、不建 UDP STUN 监听与明文 TCP 监听：前两级中继都必须失败
                tlsListener = new TurnTlsListener(new IPEndPoint(TurnAddress, 0), turnServer, certificatePath, CertificatePassword);
                tlsListener.Start();
                var turnEndPoint = tlsListener.LocalEndPoint;

                // 传输工厂仍是 UDP 控制，这是第 3 级用的配置；第 4、5 级由会话换成内置 TCP 与 TLS 控制传输
                var controllerTurn = new TurnClientOptions
                {
                    ServerEndPoint = turnEndPoint,
                    TransportFactory = () => new UdpTurnClientTransport(new IPEndPoint(HostA, 0), turnEndPoint),
                    Credential = turnServer.CredentialService.Create("ice-tls-user-a"),
                    RtoMilliseconds = 100,
                    AllocationRefreshInterval = TimeSpan.FromSeconds(30),
                    PermissionRefreshInterval = TimeSpan.FromSeconds(30),
                    ChannelRefreshInterval = TimeSpan.FromSeconds(30),
                    RefreshTimeout = TimeSpan.FromSeconds(2)
                };
                var controlledTurn = new TurnClientOptions
                {
                    ServerEndPoint = turnEndPoint,
                    TransportFactory = () => new UdpTurnClientTransport(new IPEndPoint(HostB, 0), turnEndPoint),
                    Credential = turnServer.CredentialService.Create("ice-tls-user-b"),
                    RtoMilliseconds = 100,
                    AllocationRefreshInterval = TimeSpan.FromSeconds(30),
                    PermissionRefreshInterval = TimeSpan.FromSeconds(30),
                    ChannelRefreshInterval = TimeSpan.FromSeconds(30),
                    RefreshTimeout = TimeSpan.FromSeconds(2)
                };

                controller = new IceSession(CreateTurnTlsOptions(HostA, controllerTurn), CreateParameters(true, UfragA, PwdA, UfragB, PwdB, 1));
                controlled = new IceSession(CreateTurnTlsOptions(HostB, controlledTurn), CreateParameters(false, UfragB, PwdB, UfragA, PwdA, 2));

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

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90)))
                {
                    // 受控方只等提名，先进入中继阶段完成分配，随后控制方才开始选路
                    var controlledTask = controlled.StartAsync(cts.Token);
                    var controllerTask = controller.StartAsync(cts.Token);

                    var controllerResult = await WaitAsync(controllerTask, StartTimeout, "控制方 StartAsync").ConfigureAwait(false);
                    var controlledResult = await WaitAsync(controlledTask, StartTimeout, "受控方 StartAsync").ConfigureAwait(false);

                    // 落在第 5 级说明第 3 级 UDP 控制与第 4 级明文 TCP 控制都确实失败，只有 TLS 控制拿到了分配
                    Assert.AreEqual(IceStage.TurnTls, controllerResult.Stage, "控制方应落在 TURN over TLS 阶段");
                    Assert.AreEqual(IceStage.TurnTls, controlledResult.Stage, "受控方应落在 TURN over TLS 阶段");

                    Assert.IsTrue(controller.IsConnected, "控制方中继选路完成后 IsConnected 应为 true");
                    Assert.IsTrue(controlled.IsConnected, "受控方中继选路完成后 IsConnected 应为 true");

                    Assert.IsTrue(controllerResult.IsRelay, "控制方应经中继转发");
                    Assert.IsTrue(controlledResult.IsRelay, "受控方应经中继转发");

                    // 只有控制连接走 TLS，数据面仍是 UDP 中继
                    Assert.IsFalse(controllerResult.UsesTcp, "控制方数据面不应使用 TCP");
                    Assert.IsFalse(controlledResult.UsesTcp, "受控方数据面不应使用 TCP");
                    Assert.AreEqual(ChannelType.RelayTls, controllerResult.ChannelType, "控制方通道类型应为 TLS 中继");
                    Assert.AreEqual(ChannelType.RelayTls, controlledResult.ChannelType, "受控方通道类型应为 TLS 中继");

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

                    var payload1 = Encoding.UTF8.GetBytes("turn-tls-hello 控制方经 TLS 控制中继问好");
                    controller.Send(payload1);
                    var received1 = await WaitAsync(controlledFrames.Task, FrameTimeout, "受控方接收控制方中继业务帧").ConfigureAwait(false);
                    CollectionAssert.AreEqual(payload1, received1, "受控方经中继收到的业务帧与控制方发送的内容不一致");

                    var payload2 = Encoding.UTF8.GetBytes("turn-tls-reply 受控方经 TLS 控制中继回执");
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

                if (tlsListener != null)
                {
                    tlsListener.Dispose();
                }

                if (turnServer != null)
                {
                    turnServer.Dispose();
                }

                TryDelete(certificatePath);
            }
        }

        /// <summary>
        /// TURN TCP 中继：服务端只开 TCP 控制监听，两端又只互发 TCP 中继候选，第 3 级 UDP 控制与本级的 UDP 中继都不通，
        /// 应落在 TURN over TCP 阶段并通过 RFC 6062 的 TCP 中继建立数据面，双向转发业务帧
        /// </summary>
        [TestMethod]
        [Timeout(120000)]
        public async Task StartAsync_UdpRelayBlocked_SelectsTurnTcpRelay()
        {
            IceSession controller = null;
            IceSession controlled = null;
            TurnTcpListener tcpListener = null;
            TurnServerHost turnServer = null;

            try
            {
                // TCP 中继要独占一个监听端口，与 UDP 中继共用同一段区间，预留时同时确认两种协议都空闲
                var relayPorts = ReserveTcpRelayPorts(6);

                turnServer = new TurnServerHost(new TurnServerOptions
                {
                    SharedSecret = TurnSharedSecret,
                    Realm = TurnRealm,
                    RelayAddress = TurnAddress,
                    RelayPortMin = relayPorts[0],
                    RelayPortMax = relayPorts[relayPorts.Length - 1]
                });

                // 只开 TCP 控制监听、不建 UDP STUN 监听：第 3 级 UDP 控制的分配拿不到任何应答
                tcpListener = new TurnTcpListener(new IPEndPoint(TurnAddress, 0), turnServer);
                tcpListener.Start();
                var turnEndPoint = tcpListener.LocalEndPoint;

                // 传输工厂仍是 UDP 控制，这是第 3 级用的配置；第 4 级由会话换成内置 TCP 控制传输
                var controllerTurn = new TurnClientOptions
                {
                    ServerEndPoint = turnEndPoint,
                    TransportFactory = () => new UdpTurnClientTransport(new IPEndPoint(HostA, 0), turnEndPoint),
                    Credential = turnServer.CredentialService.Create("ice-tcp-relay-user-a"),
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
                    Credential = turnServer.CredentialService.Create("ice-tcp-relay-user-b"),
                    RtoMilliseconds = 100,
                    AllocationRefreshInterval = TimeSpan.FromSeconds(30),
                    PermissionRefreshInterval = TimeSpan.FromSeconds(30),
                    ChannelRefreshInterval = TimeSpan.FromSeconds(30),
                    RefreshTimeout = TimeSpan.FromSeconds(10)
                };

                controller = new IceSession(CreateTurnTcpOptions(HostA, controllerTurn), CreateParameters(true, UfragA, PwdA, UfragB, PwdB, 1));
                controlled = new IceSession(CreateTurnTcpOptions(HostB, controlledTurn), CreateParameters(false, UfragB, PwdB, UfragA, PwdA, 2));

                // 只转发 TCP 中继候选：本级的 UDP 中继段拿不到对端 UDP 中继候选，只能退到 RFC 6062
                controller.LocalCandidateDiscovered += candidate =>
                {
                    if (candidate.Type == CandidateType.Relay && candidate.Transport == IceTransportType.Tcp)
                    {
                        controlled.AddRemoteCandidate(candidate);
                    }
                };
                controlled.LocalCandidateDiscovered += candidate =>
                {
                    if (candidate.Type == CandidateType.Relay && candidate.Transport == IceTransportType.Tcp)
                    {
                        controller.AddRemoteCandidate(candidate);
                    }
                };

                var controllerFrames = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                var controlledFrames = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                controller.FrameReceived += CreateFrameHandler(controllerFrames);
                controlled.FrameReceived += CreateFrameHandler(controlledFrames);

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90)))
                {
                    // 受控方只等对向连接与提名，先进入中继阶段完成分配，随后控制方才开始选路
                    var controlledTask = controlled.StartAsync(cts.Token);
                    var controllerTask = controller.StartAsync(cts.Token);

                    var controllerResult = await WaitAsync(controllerTask, StartTimeout, "控制方 StartAsync").ConfigureAwait(false);
                    var controlledResult = await WaitAsync(controlledTask, StartTimeout, "受控方 StartAsync").ConfigureAwait(false);

                    // 落在第 4 级说明第 3 级 UDP 控制确实失败，且本级最终选中的是 RFC 6062 而不是 UDP 中继
                    Assert.AreEqual(IceStage.TurnTcp, controllerResult.Stage, "控制方应落在 TURN over TCP 阶段");
                    Assert.AreEqual(IceStage.TurnTcp, controlledResult.Stage, "受控方应落在 TURN over TCP 阶段");

                    Assert.IsTrue(controller.IsConnected, "控制方 TCP 中继选路完成后 IsConnected 应为 true");
                    Assert.IsTrue(controlled.IsConnected, "受控方 TCP 中继选路完成后 IsConnected 应为 true");

                    Assert.AreEqual(ChannelType.RelayTcp, controllerResult.ChannelType, "控制方通道类型应为 TCP 中继");
                    Assert.AreEqual(ChannelType.RelayTcp, controlledResult.ChannelType, "受控方通道类型应为 TCP 中继");

                    // TCP 中继的数据面就是 Connect/ConnectionBind 得到的 TCP 连接，与 UDP 中继的 UsesTcp=false 相反
                    Assert.IsTrue(controllerResult.UsesTcp, "控制方数据面应使用 TCP 中继");
                    Assert.IsTrue(controlledResult.UsesTcp, "受控方数据面应使用 TCP 中继");

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
                    Assert.AreNotEqual(
                        controllerResult.LocalEndPoint.Port,
                        controlledResult.LocalEndPoint.Port,
                        "两端 TCP 中继应各占一个中继端口");

                    var payload1 = Encoding.UTF8.GetBytes("turn-tcp-relay-hello 控制方经 RFC 6062 中继问好");
                    controller.Send(payload1);
                    var received1 = await WaitAsync(controlledFrames.Task, FrameTimeout, "受控方接收控制方 TCP 中继业务帧").ConfigureAwait(false);
                    CollectionAssert.AreEqual(payload1, received1, "受控方经 TCP 中继收到的业务帧与控制方发送的内容不一致");

                    var payload2 = Encoding.UTF8.GetBytes("turn-tcp-relay-reply 受控方经 RFC 6062 中继回执");
                    controlled.Send(payload2);
                    var received2 = await WaitAsync(controllerFrames.Task, FrameTimeout, "控制方接收受控方 TCP 中继业务帧").ConfigureAwait(false);
                    CollectionAssert.AreEqual(payload2, received2, "控制方经 TCP 中继收到的业务帧与受控方发送的内容不一致");
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

                if (tcpListener != null)
                {
                    tcpListener.Dispose();
                }

                if (turnServer != null)
                {
                    turnServer.Dispose();
                }
            }
        }

        /// <summary>
        /// TURN over TLS 下的 TCP 中继：服务端只开 TLS 控制监听，两端又只互发 TCP 中继候选，
        /// UDP 控制与明文 TCP 控制都拿不到分配，应落在 TURN over TLS 阶段并经 TLS 上的 RFC 6062 数据连接双向转发业务帧
        /// </summary>
        [TestMethod]
        [Timeout(150000)]
        public async Task StartAsync_UdpAndTcpControlBlocked_SelectsTurnTlsTcpRelay()
        {
            IceSession controller = null;
            IceSession controlled = null;
            TurnTlsListener tlsListener = null;
            TurnServerHost turnServer = null;

            var certificatePath = CreateCertificateFile();

            try
            {
                // TCP 中继要独占一个监听端口，与 UDP 中继共用同一段区间，预留时同时确认两种协议都空闲
                var relayPorts = ReserveTcpRelayPorts(6);

                turnServer = new TurnServerHost(new TurnServerOptions
                {
                    SharedSecret = TurnSharedSecret,
                    Realm = TurnRealm,
                    RelayAddress = TurnAddress,
                    RelayPortMin = relayPorts[0],
                    RelayPortMax = relayPorts[relayPorts.Length - 1]
                });

                // 只开 TLS 控制监听：第 3 级 UDP 控制拿不到应答，第 4 级明文 TCP 控制连不上
                tlsListener = new TurnTlsListener(new IPEndPoint(TurnAddress, 0), turnServer, certificatePath, CertificatePassword);
                tlsListener.Start();
                var turnEndPoint = tlsListener.LocalEndPoint;

                // 传输工厂仍是 UDP 控制，这是第 3 级用的配置；第 4、5 级由会话换成内置 TCP 与 TLS 控制传输
                var controllerTurn = new TurnClientOptions
                {
                    ServerEndPoint = turnEndPoint,
                    TransportFactory = () => new UdpTurnClientTransport(new IPEndPoint(HostA, 0), turnEndPoint),
                    Credential = turnServer.CredentialService.Create("ice-tls-relay-user-a"),
                    RtoMilliseconds = 100,
                    AllocationRefreshInterval = TimeSpan.FromSeconds(30),
                    PermissionRefreshInterval = TimeSpan.FromSeconds(30),
                    ChannelRefreshInterval = TimeSpan.FromSeconds(30),
                    RefreshTimeout = TimeSpan.FromSeconds(2)
                };
                var controlledTurn = new TurnClientOptions
                {
                    ServerEndPoint = turnEndPoint,
                    TransportFactory = () => new UdpTurnClientTransport(new IPEndPoint(HostB, 0), turnEndPoint),
                    Credential = turnServer.CredentialService.Create("ice-tls-relay-user-b"),
                    RtoMilliseconds = 100,
                    AllocationRefreshInterval = TimeSpan.FromSeconds(30),
                    PermissionRefreshInterval = TimeSpan.FromSeconds(30),
                    ChannelRefreshInterval = TimeSpan.FromSeconds(30),
                    RefreshTimeout = TimeSpan.FromSeconds(2)
                };

                controller = new IceSession(CreateTurnTlsOptions(HostA, controllerTurn), CreateParameters(true, UfragA, PwdA, UfragB, PwdB, 1));
                controlled = new IceSession(CreateTurnTlsOptions(HostB, controlledTurn), CreateParameters(false, UfragB, PwdB, UfragA, PwdA, 2));

                // 只转发 TCP 中继候选：本级的 UDP 中继段拿不到对端 UDP 中继候选，只能退到 RFC 6062
                controller.LocalCandidateDiscovered += candidate =>
                {
                    if (candidate.Type == CandidateType.Relay && candidate.Transport == IceTransportType.Tcp)
                    {
                        controlled.AddRemoteCandidate(candidate);
                    }
                };
                controlled.LocalCandidateDiscovered += candidate =>
                {
                    if (candidate.Type == CandidateType.Relay && candidate.Transport == IceTransportType.Tcp)
                    {
                        controller.AddRemoteCandidate(candidate);
                    }
                };

                var controllerFrames = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                var controlledFrames = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                controller.FrameReceived += CreateFrameHandler(controllerFrames);
                controlled.FrameReceived += CreateFrameHandler(controlledFrames);

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120)))
                {
                    var controlledTask = controlled.StartAsync(cts.Token);
                    var controllerTask = controller.StartAsync(cts.Token);

                    var controllerResult = await WaitAsync(controllerTask, StartTimeout, "控制方 StartAsync").ConfigureAwait(false);
                    var controlledResult = await WaitAsync(controlledTask, StartTimeout, "受控方 StartAsync").ConfigureAwait(false);

                    // 落在第 5 级说明前两级的 UDP 与 TCP 控制都确实失败，且本级最终选中的是 RFC 6062 而不是 UDP 中继
                    Assert.AreEqual(IceStage.TurnTls, controllerResult.Stage, "控制方应落在 TURN over TLS 阶段");
                    Assert.AreEqual(IceStage.TurnTls, controlledResult.Stage, "受控方应落在 TURN over TLS 阶段");

                    Assert.IsTrue(controller.IsConnected, "控制方 TLS 上的 TCP 中继选路完成后 IsConnected 应为 true");
                    Assert.IsTrue(controlled.IsConnected, "受控方 TLS 上的 TCP 中继选路完成后 IsConnected 应为 true");

                    Assert.AreEqual(ChannelType.RelayTls, controllerResult.ChannelType, "控制方通道类型应为 TLS 中继");
                    Assert.AreEqual(ChannelType.RelayTls, controlledResult.ChannelType, "受控方通道类型应为 TLS 中继");

                    Assert.IsTrue(controllerResult.UsesTcp, "控制方数据面应使用 TCP 中继");
                    Assert.IsTrue(controlledResult.UsesTcp, "受控方数据面应使用 TCP 中继");

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
                    Assert.AreNotEqual(
                        controllerResult.LocalEndPoint.Port,
                        controlledResult.LocalEndPoint.Port,
                        "两端 TCP 中继应各占一个中继端口");

                    var payload1 = Encoding.UTF8.GetBytes("turn-tls-tcp-relay-hello 控制方经 TLS 上的 RFC 6062 中继问好");
                    controller.Send(payload1);
                    var received1 = await WaitAsync(controlledFrames.Task, FrameTimeout, "受控方接收控制方 TLS 中继业务帧").ConfigureAwait(false);
                    CollectionAssert.AreEqual(payload1, received1, "受控方经 TLS 上的 TCP 中继收到的业务帧与控制方发送的内容不一致");

                    var payload2 = Encoding.UTF8.GetBytes("turn-tls-tcp-relay-reply 受控方经 TLS 上的 RFC 6062 中继回执");
                    controlled.Send(payload2);
                    var received2 = await WaitAsync(controllerFrames.Task, FrameTimeout, "控制方接收受控方 TLS 中继业务帧").ConfigureAwait(false);
                    CollectionAssert.AreEqual(payload2, received2, "控制方经 TLS 上的 TCP 中继收到的业务帧与受控方发送的内容不一致");
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

                if (tlsListener != null)
                {
                    tlsListener.Dispose();
                }

                if (turnServer != null)
                {
                    turnServer.Dispose();
                }

                TryDelete(certificatePath);
            }
        }

        /// <summary>
        /// 构造会话配置：绑定一个回环地址，收集与 TURN 阶段用短预算，总预算按校验要求放宽
        /// </summary>
        /// <param name="bind">本端绑定地址</param>
        /// <param name="udpBudget">UDP 直连阶段预算</param>
        /// <returns>可直接传给会话的配置</returns>
        private static IceSessionOptions CreateOptions(IPAddress bind, TimeSpan udpBudget)
        {
            var ice = new IceOptions();
            ice.AllowLoopbackCandidates = true;
            ice.BindAddresses.Add(bind);
            ice.GatheringTimeout = TimeSpan.FromSeconds(2);

            var options = new IceSessionOptions();
            options.Ice = ice;
            options.GatheringBudget = TimeSpan.FromSeconds(1);
            options.UdpDirectBudget = udpBudget;
            options.TcpDirectBudget = TimeSpan.FromSeconds(6);
            options.TurnStageBudget = TimeSpan.FromSeconds(1);
            options.SessionBudget = TimeSpan.FromSeconds(40);

            return options;
        }

        /// <summary>
        /// 构造走 TURN 中继的会话配置：直连阶段各 1s，TURN 阶段 10s，总预算容纳收集与三级 TURN 阶段
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
            options.TcpDirectBudget = TimeSpan.FromSeconds(1);
            options.TurnStageBudget = TimeSpan.FromSeconds(10);
            options.SessionBudget = TimeSpan.FromSeconds(40);
            options.Turn = turn;
            return options;
        }

        /// <summary>
        /// 构造 TURN over TCP 用例的会话配置：直连阶段各 1s，TURN 阶段 10s，总预算容纳 UDP 控制失败与随后的 TCP 控制重试
        /// （该级先用半个阶段预算试 UDP 中继，剩余预算试 RFC 6062 TCP 中继）
        /// </summary>
        /// <param name="bind">本端绑定地址</param>
        /// <param name="turn">TURN 客户端配置</param>
        /// <returns>可直接传给会话的配置</returns>
        private static IceSessionOptions CreateTurnTcpOptions(IPAddress bind, TurnClientOptions turn)
        {
            var ice = new IceOptions();
            ice.AllowLoopbackCandidates = true;
            ice.BindAddresses.Add(bind);
            ice.GatheringTimeout = TimeSpan.FromSeconds(2);

            var options = new IceSessionOptions();
            options.Ice = ice;
            options.GatheringBudget = TimeSpan.FromSeconds(1);
            options.UdpDirectBudget = TimeSpan.FromSeconds(1);
            options.TcpDirectBudget = TimeSpan.FromSeconds(1);
            options.TurnStageBudget = TimeSpan.FromSeconds(10);
            options.SessionBudget = TimeSpan.FromSeconds(40);
            options.Turn = turn;
            return options;
        }

        /// <summary>
        /// 构造 TURN over TLS 用例的会话配置：直连阶段各 1s，TURN 阶段 10s，总预算容纳 UDP 与明文 TCP 控制两次失败后再走 TLS
        /// （该级先用半个阶段预算试 UDP 中继，剩余预算试 RFC 6062 TCP 中继）
        /// </summary>
        /// <param name="bind">本端绑定地址</param>
        /// <param name="turn">TURN 客户端配置</param>
        /// <returns>可直接传给会话的配置</returns>
        private static IceSessionOptions CreateTurnTlsOptions(IPAddress bind, TurnClientOptions turn)
        {
            var ice = new IceOptions();
            ice.AllowLoopbackCandidates = true;
            ice.BindAddresses.Add(bind);
            ice.GatheringTimeout = TimeSpan.FromSeconds(2);

            var options = new IceSessionOptions();
            options.Ice = ice;
            options.GatheringBudget = TimeSpan.FromSeconds(1);
            options.UdpDirectBudget = TimeSpan.FromSeconds(1);
            options.TcpDirectBudget = TimeSpan.FromSeconds(1);
            options.TurnStageBudget = TimeSpan.FromSeconds(10);
            options.SessionBudget = TimeSpan.FromSeconds(60);
            options.Turn = turn;
            // 自签证书只写 CN=localhost，测试用回环地址连接，必须显式给出域名并忽略证书错误
            options.TurnTlsTargetHost = "localhost";
            options.TurnTlsAllowInvalidCertificates = true;
            return options;
        }

        /// <summary>
        /// 按给定绑定地址与角色构造 ICE 会话
        /// </summary>
        /// <param name="bind">本端绑定地址</param>
        /// <param name="udpBudget">UDP 直连阶段预算</param>
        /// <param name="controlling">是否为控制方</param>
        /// <param name="localUfrag">本端 ufrag</param>
        /// <param name="localPwd">本端短凭据口令</param>
        /// <param name="remoteUfrag">对端 ufrag</param>
        /// <param name="remotePwd">对端短凭据口令</param>
        /// <param name="tiebreaker">角色冲突时使用的 tiebreaker</param>
        /// <returns>未启动的会话</returns>
        private static IceSession CreateSession(
            IPAddress bind,
            TimeSpan udpBudget,
            bool controlling,
            string localUfrag,
            string localPwd,
            string remoteUfrag,
            string remotePwd,
            ulong tiebreaker)
        {
            var options = CreateOptions(bind, udpBudget);
            return new IceSession(options, CreateParameters(controlling, localUfrag, localPwd, remoteUfrag, remotePwd, tiebreaker));
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
        /// 等待任务完成，超时直接判失败并带上场景名，便于定位卡在哪一端
        /// </summary>
        /// <param name="task">待等待的任务</param>
        /// <param name="timeout">等待上限</param>
        /// <param name="scene">失败信息里的场景描述</param>
        /// <returns>任务结果</returns>
        private static async Task<T> WaitAsync<T>(Task<T> task, TimeSpan timeout, string scene = null)
        {
            var completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
            if (!ReferenceEquals(completed, task))
            {
                var name = string.IsNullOrEmpty(scene) ? "等待任务" : scene;
                Assert.Fail(name + " 超时（" + (int)timeout.TotalMilliseconds + "ms）");
            }

            return await task.ConfigureAwait(false);
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
        /// 在 TURN 地址上取若干个 UDP 与 TCP 同时空闲的端口并立即释放，用于收窄 TCP 中继用例的中继端口区间
        /// </summary>
        /// <param name="count">端口数量</param>
        /// <returns>升序端口数组</returns>
        private static int[] ReserveTcpRelayPorts(int count)
        {
            var udpProbes = new List<SaeaUdpTransport>();
            var tcpProbes = new List<TcpListener>();
            try
            {
                var ports = new int[count];
                var found = 0;

                // 单轮可能撞上已被占用的端口，逐个重试直到凑齐，重试次数上限防止极端情况下死循环
                for (var attempt = 0; found < count && attempt < count * 50; attempt++)
                {
                    var probe = new SaeaUdpTransport();
                    probe.BindAsync(new IPEndPoint(TurnAddress, 0), CancellationToken.None).GetAwaiter().GetResult();
                    udpProbes.Add(probe);
                    var port = probe.LocalEndPoint.Port;

                    TcpListener listener;
                    try
                    {
                        listener = new TcpListener(new IPEndPoint(TurnAddress, port));
                        listener.Start();
                    }
                    catch (SocketException)
                    {
                        // 该端口 TCP 已被占用，换下一个候选
                        continue;
                    }

                    tcpProbes.Add(listener);
                    ports[found] = port;
                    found++;
                }

                Assert.AreEqual(count, found, "没有找到足够的 UDP/TCP 同时空闲的中继端口");
                Array.Sort(ports);
                return ports;
            }
            finally
            {
                for (var i = 0; i < tcpProbes.Count; i++)
                {
                    tcpProbes[i].Stop();
                }

                for (var i = 0; i < udpProbes.Count; i++)
                {
                    udpProbes[i].Dispose();
                }
            }
        }

        /// <summary>
        /// 运行时生成自签 PFX 并写入临时文件；同名主题与口令只用于测试
        /// </summary>
        /// <returns>临时证书文件路径</returns>
        private static string CreateCertificateFile()
        {
            using (var rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
                request.CertificateExtensions.Add(new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));

                using (var certificate = request.CreateSelfSigned(
                    DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1)))
                {
                    var path = Path.Combine(Path.GetTempPath(), "saea-turn-tls-" + Guid.NewGuid().ToString("N") + ".pfx");
                    File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, CertificatePassword));
                    return path;
                }
            }
        }

        /// <summary>
        /// 删除临时证书文件；文件已不存在时忽略
        /// </summary>
        /// <param name="path">证书文件路径</param>
        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}