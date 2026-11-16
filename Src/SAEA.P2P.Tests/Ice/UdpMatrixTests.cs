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
*文件名： UdpMatrixTests
*版本号： net10.0
*唯一标识：1f425152-ef05-4906-b023-6f8853b0e07c
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 11:11:14
*描述：NAT 预设两两组合下 ICE 收发矩阵与数据面往返用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 11:46:53
*修改人： yswenli
*版本号： net10.0
*描述：用例名改为 NAT预设组合，明确覆盖不同预设的两两组合
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Ice;
using SAEA.P2P.Tests.Nat;
using SAEA.P2P.Transport;

using StunServerHost = SAEA.P2P.StunServer.StunServer;
using StunServerOptions = SAEA.P2P.StunServer.StunServerOptions;
using TurnServerHost = SAEA.P2P.TurnServer.TurnServer;
using TurnServerOptions = SAEA.P2P.TurnServer.TurnServerOptions;

namespace SAEA.P2P.Tests.Ice
{
    /// <summary>
    /// 在独立回环地址上搭建双 NAT、STUN、TURN 环境，逐格验证 NAT 预设组合的选中候选对与数据面往返。
    /// </summary>
    [TestCategory(P2PTestSuite.Matrix)]
    [TestClass]
    public class UdpMatrixTests
    {
        /// <summary>
        /// 单个候选对检查的最大轮数：prflx 配对由上一轮检查插入，需要后续轮次才能被检查到
        /// </summary>
        private const int CheckRounds = 6;

        /// <summary>
        /// 单次连通性检查或提名的上限
        /// </summary>
        private static readonly TimeSpan CheckTimeSpan = TimeSpan.FromSeconds(2);

        /// <summary>
        /// 单帧数据面往返的上限
        /// </summary>
        private static readonly TimeSpan DataTimeSpan = TimeSpan.FromSeconds(3);

        /// <summary>
        /// 主机 A 的 NAT 内部地址
        /// </summary>
        private static readonly IPAddress HostAAddress = IPAddress.Parse("127.0.0.2");

        /// <summary>
        /// 主机 B 的 NAT 内部地址
        /// </summary>
        private static readonly IPAddress HostBAddress = IPAddress.Parse("127.0.0.3");

        /// <summary>
        /// NAT A 的外部地址
        /// </summary>
        private static readonly IPAddress NatAAddress = IPAddress.Parse("127.0.0.4");

        /// <summary>
        /// NAT B 的外部地址
        /// </summary>
        private static readonly IPAddress NatBAddress = IPAddress.Parse("127.0.0.5");

        /// <summary>
        /// STUN 服务地址
        /// </summary>
        private static readonly IPAddress StunAddress = IPAddress.Parse("127.0.0.6");

        /// <summary>
        /// TURN 服务地址，同时用作中继地址
        /// </summary>
        private static readonly IPAddress TurnAddress = IPAddress.Parse("127.0.0.7");

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
        /// STUN 服务端，类级共享
        /// </summary>
        private static StunServerHost _stunServer;

        /// <summary>
        /// STUN 实际监听端点
        /// </summary>
        private static IPEndPoint _stunEndPoint;

        /// <summary>
        /// TURN 服务端，类级共享
        /// </summary>
        private static TurnServerHost _turnServer;

        /// <summary>
        /// 承载 TURN 报文处理的 STUN 服务端，类级共享
        /// </summary>
        private static StunServerHost _turnStunServer;

        /// <summary>
        /// TURN 实际监听端点
        /// </summary>
        private static IPEndPoint _turnEndPoint;

        /// <summary>
        /// 启动 STUN 与 TURN 服务端
        /// </summary>
        /// <param name="context">测试上下文</param>
        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _stunServer = new StunServerHost(new StunServerOptions(new IPEndPoint(StunAddress, 0)));
            _stunServer.StartAsync().GetAwaiter().GetResult();
            _stunEndPoint = _stunServer.Endpoints[0].LocalEndPoint;

            _turnServer = new TurnServerHost(new TurnServerOptions
            {
                SharedSecret = "matrix-test-secret",
                Realm = "matrix.test",
                RelayAddress = TurnAddress,
                RelayPortMin = 56000,
                RelayPortMax = 56999
            });

            // TURN 报文复用 STUN 的编解码与监听，只把请求与数据报转给 TURN 服务端处理
            _turnStunServer = new StunServerHost(new StunServerOptions(new IPEndPoint(TurnAddress, 0))
            {
                RequestHandler = _turnServer.HandleRequest,
                DatagramHandler = _turnServer.HandleDatagram
            });
            _turnStunServer.StartAsync().GetAwaiter().GetResult();
            _turnEndPoint = _turnStunServer.Endpoints[0].LocalEndPoint;
        }

        /// <summary>
        /// 逆序释放 TURN 与 STUN 服务端
        /// </summary>
        [ClassCleanup]
        public static void ClassCleanup()
        {
            if (_turnStunServer != null)
            {
                _turnStunServer.Dispose();
                _turnStunServer = null;
            }

            if (_turnServer != null)
            {
                _turnServer.Dispose();
                _turnServer = null;
            }

            if (_stunServer != null)
            {
                _stunServer.Dispose();
                _stunServer = null;
            }

            _stunEndPoint = null;
            _turnEndPoint = null;
        }

        /// <summary>
        /// 不同 NAT 预设两两组合的连通性矩阵，期望值来自独立的连通性判定，不依赖本用例实现
        /// </summary>
        /// <param name="local">本端 NAT 预设</param>
        /// <param name="remote">对端 NAT 预设</param>
        /// <param name="expected">期望形态</param>
        [DataTestMethod]
        [DataRow(MatrixProfile.FullCone, MatrixProfile.FullCone, MatrixOutcome.Direct)]
        [DataRow(MatrixProfile.FullCone, MatrixProfile.Restricted, MatrixOutcome.Direct)]
        [DataRow(MatrixProfile.FullCone, MatrixProfile.PortRestricted, MatrixOutcome.Direct)]
        [DataRow(MatrixProfile.FullCone, MatrixProfile.Symmetric, MatrixOutcome.Direct)]
        [DataRow(MatrixProfile.Restricted, MatrixProfile.FullCone, MatrixOutcome.Direct)]
        [DataRow(MatrixProfile.Restricted, MatrixProfile.Restricted, MatrixOutcome.Direct)]
        [DataRow(MatrixProfile.Restricted, MatrixProfile.PortRestricted, MatrixOutcome.Direct)]
        [DataRow(MatrixProfile.Restricted, MatrixProfile.Symmetric, MatrixOutcome.Direct)]
        [DataRow(MatrixProfile.PortRestricted, MatrixProfile.FullCone, MatrixOutcome.Direct)]
        [DataRow(MatrixProfile.PortRestricted, MatrixProfile.Restricted, MatrixOutcome.Direct)]
        [DataRow(MatrixProfile.PortRestricted, MatrixProfile.PortRestricted, MatrixOutcome.Direct)]
        [DataRow(MatrixProfile.PortRestricted, MatrixProfile.Symmetric, MatrixOutcome.Relay)]
        [DataRow(MatrixProfile.Symmetric, MatrixProfile.FullCone, MatrixOutcome.Direct)]
        [DataRow(MatrixProfile.Symmetric, MatrixProfile.Restricted, MatrixOutcome.Direct)]
        [DataRow(MatrixProfile.Symmetric, MatrixProfile.PortRestricted, MatrixOutcome.Relay)]
        [DataRow(MatrixProfile.Symmetric, MatrixProfile.Symmetric, MatrixOutcome.Relay)]
        [Timeout(180000)]
        public async Task UdpMatrix_NAT预设组合_期望与实测一致(MatrixProfile local, MatrixProfile remote, MatrixOutcome expected)
        {
            await RunCellAsync(local, remote, expected);
        }

        /// <summary>
        /// 端口受限对对称型必须走中继，单独成例便于快速定位该格的回归
        /// </summary>
        [TestMethod]
        [Timeout(180000)]
        public async Task UdpMatrix_PortRestricted对Symmetric_经relay成功()
        {
            await RunCellAsync(MatrixProfile.PortRestricted, MatrixProfile.Symmetric, MatrixOutcome.Relay);
        }

        /// <summary>
        /// 跑一格：建 NAT、收集候选、双向检查、提名选对、判定期望并做数据面往返
        /// </summary>
        /// <param name="localProfile">主机 A（控制方）的 NAT 预设</param>
        /// <param name="remoteProfile">主机 B（被动方）的 NAT 预设</param>
        /// <param name="expected">期望形态</param>
        private static async Task RunCellAsync(MatrixProfile localProfile, MatrixProfile remoteProfile, MatrixOutcome expected)
        {
            var configA = ToNatConfig(localProfile);
            var configB = ToNatConfig(remoteProfile);

            // NAT 必须先于主机存在，主机 socket 建在其内部网段
            using (var natA = new NatBox(NatAAddress, configA))
            using (var natB = new NatBox(NatBAddress, configB))
            {
                // 每格换新凭据，避免跨格复用同一对 ufrag/password 掩盖状态泄漏
                var ufragA = "a" + Guid.NewGuid().ToString("N").Substring(0, 8);
                var pwdA = Guid.NewGuid().ToString("N");
                var ufragB = "b" + Guid.NewGuid().ToString("N").Substring(0, 8);
                var pwdB = Guid.NewGuid().ToString("N");

                var credentialA = _turnServer.CredentialService.Create("matrix-a");
                var credentialB = _turnServer.CredentialService.Create("matrix-b");

                using (var hostA = new MatrixHost(HostAAddress, natA.InternalGateway, NatAAddress, _stunEndPoint,
                    _turnEndPoint, credentialA, true, ufragA, pwdA))
                using (var hostB = new MatrixHost(HostBAddress, natB.InternalGateway, NatBAddress, _stunEndPoint,
                    _turnEndPoint, credentialB, false, ufragB, pwdB))
                {
                    await hostA.GatherAsync(CancellationToken.None);
                    await hostB.GatherAsync(CancellationToken.None);

                    hostA.AddLocalCandidates(hostA.Candidates);
                    hostB.AddLocalCandidates(hostB.Candidates);

                    hostA.AddRemoteCandidates(hostB.Candidates);
                    hostB.AddRemoteCandidates(hostA.Candidates);

                    Assert.AreEqual(4, hostA.SnapshotPairs().Length, "主机 A 的两个本端候选应与两个远端候选两两配对");
                    Assert.AreEqual(4, hostB.SnapshotPairs().Length, "主机 B 的两个本端候选应与两个远端候选两两配对");

                    Assert.AreEqual(2, hostA.Candidates.Count, "主机 A 应收集到 srflx 与 relay 两个候选");
                    Assert.AreEqual(2, hostB.Candidates.Count, "主机 B 应收集到 srflx 与 relay 两个候选");

                    const ulong tiebreakerA = 0x1122334455667788UL;
                    const ulong tiebreakerB = 0x8877665544332211UL;

                    // 两个方向必须同时开跑：受限类 NAT 的过滤器只在对端向内网发过包之后才放行，
                    // 先跑完一侧再跑另一侧会让先跑的一侧三轮全丢
                    await Task.WhenAll(
                        RunChecksAsync(hostA, ufragB, pwdB, tiebreakerA),
                        RunChecksAsync(hostB, ufragA, pwdA, tiebreakerB));

                    var succeededA = hostA.SnapshotPairs()
                        .Where(p => p.State == PairState.Succeeded)
                        .OrderByDescending(p => p.Priority)
                        .ToArray();

                    // 数据面要双向可达：镜像对未成功说明该方向回不来，选中它数据面必然超时，
                    // 只能改选下一个已成功 pair；这不是放宽期望，而是筛掉不可用的方向
                    var pairOrder = succeededA.OrderByDescending(p => IsMirrorSucceeded(hostB, p)).ToArray();

                    CandidatePair chosen = null;
                    foreach (var pair in pairOrder)
                    {
                        if (!IsMirrorSucceeded(hostB, pair)) continue;

                        using (var cts = new CancellationTokenSource(CheckTimeSpan))
                        {
                            if (await hostA.NominateAsync(pair, ufragB, pwdB, tiebreakerA, cts.Token))
                            {
                                chosen = pair;
                                break;
                            }
                        }
                    }

                    if (chosen == null)
                    {
                        Assert.Fail("主机 A 没有可提名的双向候选对；A 的 pair：" + DescribePairs(hostA.SnapshotPairs())
                            + "；B 的 pair：" + DescribePairs(hostB.SnapshotPairs())
                            + "；responder handled=" + hostA.Responder.HandledRequestCount
                            + " integrityFailure=" + hostA.Responder.IntegrityFailureCount
                            + " useCandidate=" + hostA.Responder.UseCandidateCount
                            + " observerFailure=" + hostA.Responder.ObserverFailureCount);
                        return;
                    }

                    Assert.IsNotNull(hostA.SelectedPair, "主机 A 提名成功后应产出选中对");
                    var selA = hostA.SelectedPair;

                    // 被动侧不跑提名，按控制方的选中对取镜像并直接发布，只为驱动数据面
                    var mirror = FindMirror(hostB, selA.Pair);

                    if (mirror == null || mirror.State != PairState.Succeeded)
                    {
                        Assert.Fail("主机 B 找不到与 A 选中对互逆的成功 pair；A 选中："
                            + selA.Pair.Local.EndPoint + " -> " + selA.Pair.Remote.EndPoint
                            + "；B 的 pair：" + DescribePairs(hostB.SnapshotPairs()));
                        return;
                    }

                    hostB.MarkSelected(mirror);
                    var selB = hostB.SelectedPair;
                    Assert.IsNotNull(selB, "主机 B 发布镜像后应产出选中对");

                    Assert.IsTrue(IsScenarioAddress(selA.Pair.Local.EndPoint),
                        "主机 A 选中对本端地址越界：" + selA.Pair.Local.EndPoint);
                    Assert.IsTrue(IsScenarioAddress(selA.Pair.Remote.EndPoint),
                        "主机 A 选中对对端地址越界：" + selA.Pair.Remote.EndPoint);
                    Assert.IsTrue(IsScenarioAddress(selB.Pair.Local.EndPoint),
                        "主机 B 选中对本端地址越界：" + selB.Pair.Local.EndPoint);
                    Assert.IsTrue(IsScenarioAddress(selB.Pair.Remote.EndPoint),
                        "主机 B 选中对对端地址越界：" + selB.Pair.Remote.EndPoint);

                    // 期望判定已由独立用例逐格覆盖，这里只做自洽校验，防止 DataRow 与设计表写偏
                    Assert.AreEqual(expected, ExpectedConnectivity.Evaluate(localProfile, remoteProfile),
                        "DataRow 的期望值与连通性判定不一致");

                    var endpoints = " A=" + selA.Pair.Local.EndPoint + "->" + selA.Pair.Remote.EndPoint
                        + " B=" + selB.Pair.Local.EndPoint + "->" + selB.Pair.Remote.EndPoint;

                    if (expected == MatrixOutcome.Direct)
                    {
                        // SelectedPair.IsRelay 已同时覆盖 Relay 类型与 TCP 语义
                        Assert.IsFalse(selA.IsRelay, "期望直连但主机 A 选中了中继对" + endpoints);
                        Assert.IsFalse(selB.IsRelay, "期望直连但主机 B 选中了中继对" + endpoints);
                    }
                    else
                    {
                        Assert.IsTrue(selA.IsRelay || selB.IsRelay, "期望中继但两侧都未选中中转对" + endpoints);
                    }

                    using (var dataA = new UdpDataChannel(hostA.TransportFor(selA.Pair.Local), selA.Pair.Remote.EndPoint))
                    using (var dataB = new UdpDataChannel(hostB.TransportFor(selB.Pair.Local), selB.Pair.Remote.EndPoint))
                    {
                        var toB = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                        var toA = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

                        dataB.FrameReceived += frame => toB.TrySetResult(frame.ToArray());
                        dataA.FrameReceived += frame => toA.TrySetResult(frame.ToArray());

                        dataA.Start();
                        dataB.Start();

                        dataA.Send(new byte[] { 0x11, 0x12 });

                        byte[] frameB;
                        try
                        {
                            frameB = await toB.Task.WaitAsync(DataTimeSpan);
                        }
                        catch (TimeoutException)
                        {
                            Assert.Fail("B 未在限定时间内收到 A 的数据帧，端点" + endpoints);
                            return;
                        }

                        Assert.AreEqual(2, frameB.Length, "B 收到的帧长度不符，端点" + endpoints);
                        Assert.AreEqual<byte>(0x11, frameB[0], "B 收到的第 1 字节不符，端点" + endpoints);
                        Assert.AreEqual<byte>(0x12, frameB[1], "B 收到的第 2 字节不符，端点" + endpoints);

                        dataB.Send(new byte[] { 0x21, 0x22, 0x23 });

                        byte[] frameA;
                        try
                        {
                            frameA = await toA.Task.WaitAsync(DataTimeSpan);
                        }
                        catch (TimeoutException)
                        {
                            Assert.Fail("A 未在限定时间内收到 B 的数据帧，端点" + endpoints);
                            return;
                        }

                        Assert.AreEqual(3, frameA.Length, "A 收到的帧长度不符，端点" + endpoints);
                        Assert.AreEqual<byte>(0x21, frameA[0], "A 收到的第 1 字节不符，端点" + endpoints);
                        Assert.AreEqual<byte>(0x22, frameA[1], "A 收到的第 2 字节不符，端点" + endpoints);
                        Assert.AreEqual<byte>(0x23, frameA[2], "A 收到的第 3 字节不符，端点" + endpoints);
                    }

                    Assert.IsTrue(hostA.Responder.IntegrityFailureCount == 0,
                        "主机 A 应答器出现完整性校验失败：" + hostA.Responder.IntegrityFailureCount);
                    Assert.IsTrue(hostB.Responder.IntegrityFailureCount == 0,
                        "主机 B 应答器出现完整性校验失败：" + hostB.Responder.IntegrityFailureCount);
                    Assert.IsTrue(hostA.Responder.HandledRequestCount > 0, "主机 A 应答器未处理任何请求");
                    Assert.IsTrue(hostB.Responder.HandledRequestCount > 0, "主机 B 应答器未处理任何请求");

                    // using 之后 Dispose 幂等，这里只显式释放一次 TURN 分配
                    await hostA.ReleaseAsync(CancellationToken.None);
                    await hostB.ReleaseAsync(CancellationToken.None);
                }
            }
        }

        /// <summary>
        /// 对本端检查表里未成功的候选对反复检查，直到出现成功对或轮数用尽
        /// </summary>
        /// <param name="host">本端主机</param>
        /// <param name="remoteUfrag">对端 ufrag</param>
        /// <param name="remotePassword">对端口令</param>
        /// <param name="tiebreaker">本端角色裁决值</param>
        private static async Task RunChecksAsync(MatrixHost host, string remoteUfrag, string remotePassword, ulong tiebreaker)
        {
            for (var round = 0; round < CheckRounds; round++)
            {
                var pending = host.SnapshotPairs().Where(p => p.State != PairState.Succeeded).ToArray();
                if (pending.Length == 0) break;

                var succeededBefore = CountSucceeded(host.SnapshotPairs());

                var tasks = new Task[pending.Length];
                for (var i = 0; i < pending.Length; i++)
                {
                    tasks[i] = RunOneCheckAsync(host, pending[i], remoteUfrag, remotePassword, tiebreaker);
                }

                await Task.WhenAll(tasks);

                var after = host.SnapshotPairs();
                var succeededAfter = CountSucceeded(after);
                var pendingAfter = after.Count(p => p.State != PairState.Succeeded);

                // 本轮没有新增成功对且待检对数量没变，说明再等也不会有进展；prflx 配对会让待检数增加，必须继续下一轮
                if (succeededAfter == succeededBefore && pendingAfter == pending.Length) break;

                // 第一轮的双向检查可能被对端 NAT 过滤器丢弃，靠后几轮补齐
                await Task.Delay(100);
            }

            if (!host.SnapshotPairs().Any(p => p.State == PairState.Succeeded))
            {
                Assert.Fail("本端检查表中没有任何成功的候选对；pair 状态：" + DescribePairs(host.SnapshotPairs()));
            }
        }

        /// <summary>
        /// 统计已成功的候选对数量
        /// </summary>
        /// <param name="pairs">候选对集合</param>
        /// <returns>状态为成功候选对的数量</returns>
        private static int CountSucceeded(IEnumerable<CandidatePair> pairs)
        {
            return pairs.Count(p => p.State == PairState.Succeeded);
        }

        /// <summary>
        /// 单对候选对的连通性检查，超过上限即视为本轮失败
        /// </summary>
        /// <param name="host">本端主机</param>
        /// <param name="pair">候选对</param>
        /// <param name="remoteUfrag">对端 ufrag</param>
        /// <param name="remotePassword">对端口令</param>
        /// <param name="tiebreaker">本端角色裁决值</param>
        private static async Task RunOneCheckAsync(MatrixHost host, CandidatePair pair, string remoteUfrag,
            string remotePassword, ulong tiebreaker)
        {
            using (var cts = new CancellationTokenSource(CheckTimeSpan))
            {
                await host.CheckPairAsync(pair, remoteUfrag, remotePassword, tiebreaker, cts.Token);
            }
        }

        /// <summary>
        /// 把 NAT 预设映射为 NAT 配置
        /// </summary>
        /// <param name="profile">NAT 预设</param>
        /// <returns>NAT 配置</returns>
        /// <exception cref="ArgumentOutOfRangeException">预设不在已知取值内</exception>
        private static NatConfig ToNatConfig(MatrixProfile profile)
        {
            switch (profile)
            {
                case MatrixProfile.FullCone:
                    return NatPreset.FullCone();
                case MatrixProfile.Restricted:
                    return NatPreset.Restricted();
                case MatrixProfile.PortRestricted:
                    return NatPreset.PortRestricted();
                case MatrixProfile.Symmetric:
                    return NatPreset.Symmetric();
                default:
                    throw new ArgumentOutOfRangeException(nameof(profile), profile, "未知的 NAT 预设");
            }
        }

        /// <summary>
        /// 在对端检查表里找与本端候选对端点互逆的 pair
        /// </summary>
        /// <param name="host">对端主机</param>
        /// <param name="pair">本端候选对</param>
        /// <returns>互逆的候选对，找不到时为 null</returns>
        private static CandidatePair FindMirror(MatrixHost host, CandidatePair pair)
        {
            return host.SnapshotPairs().FirstOrDefault(p =>
                p.Local.EndPoint.Equals(pair.Remote.EndPoint)
                && p.Remote.EndPoint.Equals(pair.Local.EndPoint));
        }

        /// <summary>
        /// 判断该候选对在对端检查表里是否也已成功
        /// </summary>
        /// <param name="host">对端主机</param>
        /// <param name="pair">本端候选对</param>
        /// <returns>互逆对存在且已成功时为 true</returns>
        private static bool IsMirrorSucceeded(MatrixHost host, CandidatePair pair)
        {
            var mirror = FindMirror(host, pair);
            return mirror != null && mirror.State == PairState.Succeeded;
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
        /// 把候选对列表压成便于失败消息阅读的一行一条文本
        /// </summary>
        /// <param name="pairs">候选对集合</param>
        /// <returns>每对输出本端类型/端点、对端类型/端点、优先级与状态</returns>
        private static string DescribePairs(IEnumerable<CandidatePair> pairs)
        {
            var builder = new StringBuilder();
            foreach (var pair in pairs)
            {
                builder.Append(pair.Local.Type).Append('@').Append(pair.Local.EndPoint)
                    .Append(" -> ").Append(pair.Remote.Type).Append('@').Append(pair.Remote.EndPoint)
                    .Append(" / priority=").Append(pair.Priority)
                    .Append(" / state=").Append(pair.State)
                    .AppendLine();
            }

            return builder.ToString();
        }
    }
}