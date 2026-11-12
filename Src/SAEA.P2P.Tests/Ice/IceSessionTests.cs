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
*修改时间：2026/11/12 15:00:08
*修改人： yswenli
*版本号： net10.0
*描述：ICE 会话端到端用例：UDP 直连、TCP 回退与启动校验
*
*****************************************************************************/
using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Common;
using SAEA.P2P.Ice;

namespace SAEA.P2P.Tests.Ice
{
    /// <summary>
    /// ICE 会话端到端用例：两个回环地址上的会话互相 trickle 候选，
    /// 分别验证 UDP 直连选路与业务帧双向转发、UDP 不通时回退 TCP 直连，以及未连接时的发送与重复启动校验。
    /// </summary>
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

                var sendError = Assert.ThrowsException<P2PException>(() => session.Send(payload));
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

                await Assert.ThrowsExceptionAsync<InvalidOperationException>(
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
            var parameters = new IceSessionParameters();
            parameters.LocalUfrag = localUfrag;
            parameters.LocalPassword = localPwd;
            parameters.RemoteUfrag = remoteUfrag;
            parameters.RemotePassword = remotePwd;
            parameters.Role = controlling ? IceRole.Controlling : IceRole.Controlled;
            parameters.Tiebreaker = tiebreaker;

            return new IceSession(CreateOptions(bind, udpBudget), parameters);
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
    }
}