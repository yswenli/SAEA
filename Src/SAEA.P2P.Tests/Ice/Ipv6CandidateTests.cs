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
*文件名： Ipv6CandidateTests
*版本号： net10.0
*唯一标识：6137d63c-fe20-488d-a559-94362ca5c076
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 21:00:00
*描述：IPv6 候选收集与回环直连用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 21:00:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 IPv6 候选与回环直连用例
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

using SAEA.P2P.Ice;

namespace SAEA.P2P.Tests.Ice
{
    /// <summary>
    /// IPv6 用例：绑定 ::1 时产出 IPv6 host 候选、v4-mapped 地址在候选层归一化为 IPv4，
    /// 以及两端都绑定 ::1 时 UDP 直连能选中候选对并双向转发业务帧。
    /// </summary>
    [TestClass]
    public class Ipv6CandidateTests
    {
        /// <summary>
        /// 控制方 ufrag
        /// </summary>
        private const string UfragA = "ufragV6A";

        /// <summary>
        /// 控制方短凭据口令
        /// </summary>
        private const string PwdA = "passwordV6A0123456789";

        /// <summary>
        /// 受控方 ufrag
        /// </summary>
        private const string UfragB = "ufragV6B";

        /// <summary>
        /// 受控方短凭据口令
        /// </summary>
        private const string PwdB = "passwordV6B0123456789";

        /// <summary>
        /// 等待选路完成的上限，比会话总预算更宽，避免收集与检查变慢时误判
        /// </summary>
        private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(35);

        /// <summary>
        /// 等待业务帧到达的上限
        /// </summary>
        private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 指定绑定 ::1 时，收集结果里应出现地址为 ::1 的 UDP host 候选
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public async Task CandidateGatherer_指定IPv6回环_产生v6候选()
        {
            // 回环候选默认被过滤，只有显式打开开关才能观察到 ::1 的 host 候选
            var options = new IceOptions
            {
                AllowLoopbackCandidates = true,
                GatheringTimeout = TimeSpan.FromSeconds(2)
            };
            options.BindAddresses.Add(IPAddress.IPv6Loopback);

            using var gatherer = new CandidateGatherer(options);
            await gatherer.GatherAsync(CancellationToken.None).ConfigureAwait(false);

            var candidates = new List<Candidate>(gatherer.Candidates);

            var found = false;
            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate.Type == CandidateType.Host
                    && candidate.Transport == IceTransportType.Udp
                    && candidate.Address.AddressFamily == AddressFamily.InterNetworkV6
                    && candidate.Address.Equals(IPAddress.IPv6Loopback))
                {
                    found = true;
                    break;
                }
            }

            Assert.IsTrue(found, "应产出地址为 ::1 的 UDP host 候选，实际候选：" + DescribeCandidates(candidates));
        }

        /// <summary>
        /// 绑定 ::ffff:127.0.0.1 时归一化为 IPv4 回环，候选层也不残留 v4-mapped 地址
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public async Task CandidateGatherer_v4映射地址_归一化为v4()
        {
            var options = new IceOptions
            {
                AllowLoopbackCandidates = true,
                GatheringTimeout = TimeSpan.FromSeconds(2)
            };
            options.BindAddresses.Add(IPAddress.Parse("::ffff:127.0.0.1"));

            using var gatherer = new CandidateGatherer(options);
            await gatherer.GatherAsync(CancellationToken.None).ConfigureAwait(false);

            var candidates = new List<Candidate>(gatherer.Candidates);
            for (int i = 0; i < candidates.Count; i++)
            {
                Assert.IsFalse(candidates[i].Address.IsIPv4MappedToIPv6, "收集结果不应残留 v4-mapped 候选：" + candidates[i]);
            }

            var found = false;
            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate.Type == CandidateType.Host
                    && candidate.Transport == IceTransportType.Udp
                    && candidate.Address.AddressFamily == AddressFamily.InterNetwork
                    && candidate.Address.Equals(IPAddress.Loopback))
                {
                    found = true;
                    break;
                }
            }

            Assert.IsTrue(found, "v4-mapped 绑定地址应产出 127.0.0.1 的 UDP host 候选，实际候选：" + DescribeCandidates(candidates));

            // 直接构造候选，确认归一化发生在候选层而不只是收集层
            var constructed = new Candidate(
                CandidateType.Host,
                IceTransportType.Udp,
                1,
                IPAddress.Parse("::ffff:127.0.0.1"),
                5000,
                TcpCandidateType.None,
                null,
                0);

            Assert.AreEqual(AddressFamily.InterNetwork, constructed.Address.AddressFamily, "候选地址族应归一化为 IPv4");
            Assert.AreEqual(IPAddress.Loopback, constructed.Address, "候选地址应归一化为 127.0.0.1");
            Assert.AreEqual(AddressFamily.InterNetwork, constructed.EndPoint.AddressFamily, "候选端点地址族应归一化为 IPv4");
        }

        /// <summary>
        /// 两端都绑定 ::1 时，应选中 UDP 直连并双向转发业务帧
        /// </summary>
        [TestMethod]
        [Timeout(60000)]
        public async Task IceSession_IPv6回环_直连成功()
        {
            IceSession controller = null;
            IceSession controlled = null;

            try
            {
                // 两端各自持有独立的配置实例，避免共享可变状态
                controller = new IceSession(
                    CreateOptions(IPAddress.IPv6Loopback, TimeSpan.FromSeconds(5)),
                    CreateParameters(true, UfragA, PwdA, UfragB, PwdB, 1));
                controlled = new IceSession(
                    CreateOptions(IPAddress.IPv6Loopback, TimeSpan.FromSeconds(5)),
                    CreateParameters(false, UfragB, PwdB, UfragA, PwdA, 2));

                // 只转发 UDP host 候选，避免 TCP 候选干扰本用例要验证的 UDP 直连路径
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

                    Assert.AreEqual(IPAddress.IPv6Loopback, controllerResult.LocalEndPoint.Address, "控制方本地端点地址应为 ::1");
                    Assert.AreEqual(IPAddress.IPv6Loopback, controllerResult.RemoteEndPoint.Address, "控制方远端端点地址应为 ::1");
                    Assert.AreEqual(IPAddress.IPv6Loopback, controlledResult.LocalEndPoint.Address, "受控方本地端点地址应为 ::1");
                    Assert.AreEqual(IPAddress.IPv6Loopback, controlledResult.RemoteEndPoint.Address, "受控方远端端点地址应为 ::1");

                    var payload1 = Encoding.UTF8.GetBytes("ipv6-direct-hello 控制方直连问好");
                    controller.Send(payload1);
                    var received1 = await WaitAsync(controlledFrames.Task, FrameTimeout, "受控方接收控制方业务帧").ConfigureAwait(false);
                    CollectionAssert.AreEqual(payload1, received1, "受控方收到的业务帧与控制方发送的内容不一致");

                    var payload2 = Encoding.UTF8.GetBytes("ipv6-direct-reply 受控方直连回执");
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
        /// <typeparam name="T">任务结果类型</typeparam>
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

                // 帧内存只在回调期间有效，必须复制后再交给断言线程
                source.TrySetResult(frame.ToArray());
            };
        }

        /// <summary>
        /// 把候选列表拼成一行文本，用于失败信息里定位实际收集结果
        /// </summary>
        /// <param name="candidates">候选列表</param>
        /// <returns>候选的文本描述</returns>
        private static string DescribeCandidates(IReadOnlyList<Candidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return "（空）";
            }

            var builder = new StringBuilder();
            for (int i = 0; i < candidates.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append("；");
                }
                builder.Append(candidates[i]);
            }
            return builder.ToString();
        }
    }
}