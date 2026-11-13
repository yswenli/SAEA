using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SAEA.Common;
using SAEA.P2P.Channel;
using SAEA.P2P.Ice;

namespace SAEA.P2PTest.Tests
{
    /// <summary>
    /// 打洞自测：两个回环地址上的 ICE 会话互转候选后选中 UDP 直连并互通业务帧，
    /// 以及凭据缺失拒绝启动、重复释放幂等两条轻量校验。
    /// </summary>
    public static class HolePunchTest
    {
        /// <summary>
        /// 控制方绑定地址
        /// </summary>
        private static readonly IPAddress HostA = IPAddress.Parse("127.0.0.2");

        /// <summary>
        /// 受控方绑定地址
        /// </summary>
        private static readonly IPAddress HostB = IPAddress.Parse("127.0.0.3");

        /// <summary>
        /// 等待选路完成的上限，比会话总预算更宽，避免收集变慢时误判
        /// </summary>
        private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(35);

        /// <summary>
        /// 等待 RouteSelected 事件的上限
        /// </summary>
        private static readonly TimeSpan RouteTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 等待业务帧到达的上限
        /// </summary>
        private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 依次执行 ICE 打洞、凭据校验与重复释放三段自测
        /// </summary>
        /// <returns>异步任务，完成后该组场景与断言已全部执行</returns>
        public static async Task RunAsync()
        {
            ConsoleHelper.WriteLine("=== HolePunchTest ===");
            ConsoleHelper.WriteLine("");

            await TestUdpHolePunchAsync();
            ConsoleHelper.WriteLine("");

            TestInvalidCredentialsRejected();
            ConsoleHelper.WriteLine("");

            TestDisposeIdempotent();
            ConsoleHelper.WriteLine("");

            TestHarness.WriteSummary("HolePunchTest");
        }

        /// <summary>
        /// 两端同时开跑并互转 UDP host 候选，应各自选中 UDP 直连并收到对方发送的同一份载荷
        /// </summary>
        /// <returns>异步任务</returns>
        static async Task TestUdpHolePunchAsync()
        {
            TestHarness.Section("udp hole punch");

            IceSession? controller = null;
            IceSession? controlled = null;

            try
            {
                // 每轮换新凭据，避免上一轮的会话状态（同 ufrag 的应答器）干扰本次检查
                var ufragA = "a" + Guid.NewGuid().ToString("N").Substring(0, 8);
                var pwdA = Guid.NewGuid().ToString("N");
                var ufragB = "b" + Guid.NewGuid().ToString("N").Substring(0, 8);
                var pwdB = Guid.NewGuid().ToString("N");

                controller = CreateSession(HostA, true, ufragA, pwdA, ufragB, pwdB, 0x1122334455667788UL);
                controlled = CreateSession(HostB, false, ufragB, pwdB, ufragA, pwdA, 0x8877665544332211UL);

                var controllerRoutes = new TaskCompletionSource<IceSessionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                var controlledRoutes = new TaskCompletionSource<IceSessionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                var controllerFrames = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                var controlledFrames = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

                // 只互转 UDP host 候选：其它传输或中继候选会把选路带离 UDP 直连，干扰本用例要验证的打洞路径
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

                controller.RouteSelected += result => controllerRoutes.TrySetResult(result);
                controlled.RouteSelected += result => controlledRoutes.TrySetResult(result);
                controller.FrameReceived += CreateFrameHandler(controllerFrames);
                controlled.FrameReceived += CreateFrameHandler(controlledFrames);

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                {
                    // 两侧必须同时开跑：先跑完一侧时对端还没把候选送过来，打洞就没有双向可达性
                    var controllerTask = controller.StartAsync(cts.Token);
                    var controlledTask = controlled.StartAsync(cts.Token);

                    // 分别等待而不合并，超时失败时才能定位是哪一端没走出选路
                    var controllerResult = await WaitAsync(controllerTask, StartTimeout, "控制方 StartAsync");
                    var controlledResult = await WaitAsync(controlledTask, StartTimeout, "受控方 StartAsync");

                    var controllerRoute = await WaitAsync(controllerRoutes.Task, RouteTimeout, "控制方 RouteSelected");
                    var controlledRoute = await WaitAsync(controlledRoutes.Task, RouteTimeout, "受控方 RouteSelected");

                    TestHarness.Expect(ReferenceEquals(controllerResult, controllerRoute), "controller route event carries start result");
                    TestHarness.Expect(ReferenceEquals(controlledResult, controlledRoute), "controlled route event carries start result");

                    TestHarness.Expect(controllerResult.ChannelType == ChannelType.UdpDirect,
                        "controller selected udp direct", controllerResult.ChannelType.ToString());
                    TestHarness.Expect(controlledResult.ChannelType == ChannelType.UdpDirect,
                        "controlled selected udp direct", controlledResult.ChannelType.ToString());

                    TestHarness.Expect(controller.IsConnected, "controller is connected");
                    TestHarness.Expect(controlled.IsConnected, "controlled is connected");

                    TestHarness.Expect(controllerResult.LocalEndPoint != null && controllerResult.RemoteEndPoint != null,
                        "controller endpoints resolved", controllerResult.ToString());
                    TestHarness.Expect(controlledResult.LocalEndPoint != null && controlledResult.RemoteEndPoint != null,
                        "controlled endpoints resolved", controlledResult.ToString());

                    TestHarness.Expect(controllerResult.LocalEndPoint != null && controllerResult.LocalEndPoint.Address.Equals(HostA),
                        "controller local endpoint on bind address", Describe(controllerResult.LocalEndPoint));
                    TestHarness.Expect(controlledResult.LocalEndPoint != null && controlledResult.LocalEndPoint.Address.Equals(HostB),
                        "controlled local endpoint on bind address", Describe(controlledResult.LocalEndPoint));

                    var payload = Encoding.UTF8.GetBytes("ice-hole-punch 打洞载荷");
                    controller.Send(payload);
                    var received = await WaitAsync(controlledFrames.Task, FrameTimeout, "受控方接收业务帧");

                    TestHarness.Expect(received.AsSpan().SequenceEqual(payload),
                        "payload arrived byte-identical over punched path", received.Length + " B");
                }
            }
            catch (Exception ex)
            {
                // 演示程序不能因单个场景失败而中断，失败按断言计数后继续后续场景
                TestHarness.Expect(false, "udp hole punch completed", ex.GetType().Name + ": " + ex.Message);
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
        /// 凭据缺失时启动被拒绝：校验在会话构造阶段执行，因此按「创建并启动」整体断言抛 ArgumentException
        /// </summary>
        static void TestInvalidCredentialsRejected()
        {
            TestHarness.Section("invalid ice credentials rejected");

            ExpectStartRejected("缺少本端 ufrag", parameters => parameters.LocalUfrag = null);
            ExpectStartRejected("缺少本端口令", parameters => parameters.LocalPassword = string.Empty);
            ExpectStartRejected("缺少对端 ufrag", parameters => parameters.RemoteUfrag = null);
            ExpectStartRejected("缺少对端口令", parameters => parameters.RemotePassword = null);
        }

        /// <summary>
        /// 用破坏后的凭据创建并启动会话，断言被 ArgumentException 拒绝
        /// </summary>
        /// <param name="scene">失败信息里的场景描述</param>
        /// <param name="breakCredential">把某一项凭据改成缺失的委托</param>
        static void ExpectStartRejected(string scene, Action<IceSessionParameters> breakCredential)
        {
            var parameters = CreateParameters(true, "ufrag-a", "pwd-a-0123456789", "ufrag-b", "pwd-b-0123456789", 1);
            breakCredential(parameters);

            TestHarness.Throws<ArgumentException>(() =>
            {
                using (var session = new IceSession(CreateOptions(HostA, TimeSpan.FromSeconds(1)), parameters))
                {
                    // 校验若没在构造时拦下，这里会因没有对端候选而以 P2PException 结束，同样说明启动未被拒绝
                    session.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                }
            }, scene + " 时启动被拒绝");
        }

        /// <summary>
        /// 重复释放会话不抛异常，且未选路的会话释放后 IsConnected 为 false
        /// </summary>
        static void TestDisposeIdempotent()
        {
            TestHarness.Section("session dispose twice");

            var session = new IceSession(
                CreateOptions(HostA, TimeSpan.FromSeconds(1)),
                CreateParameters(true, "ufrag-a", "pwd-a-0123456789", "ufrag-b", "pwd-b-0123456789", 1));

            try
            {
                session.Dispose();
                session.Dispose();
                TestHarness.Expect(true, "repeated Dispose does not throw");
            }
            catch (Exception ex)
            {
                TestHarness.Expect(false, "repeated Dispose does not throw", ex.GetType().Name + ": " + ex.Message);
            }

            TestHarness.Expect(!session.IsConnected, "disposed session is not connected");
        }

        /// <summary>
        /// 构造会话配置：绑定一个回环地址，收集预算与总预算按校验要求放宽
        /// </summary>
        /// <param name="bind">本端绑定地址</param>
        /// <param name="udpBudget">UDP 直连阶段预算</param>
        /// <returns>可直接传给会话的配置</returns>
        static IceSessionOptions CreateOptions(IPAddress bind, TimeSpan udpBudget)
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
        /// <param name="controlling">是否为控制方</param>
        /// <param name="localUfrag">本端 ufrag</param>
        /// <param name="localPwd">本端短凭据口令</param>
        /// <param name="remoteUfrag">对端 ufrag</param>
        /// <param name="remotePwd">对端短凭据口令</param>
        /// <param name="tiebreaker">角色冲突时使用的 tiebreaker</param>
        /// <returns>未启动的会话</returns>
        static IceSession CreateSession(IPAddress bind, bool controlling, string localUfrag, string localPwd,
            string remoteUfrag, string remotePwd, ulong tiebreaker)
        {
            var options = CreateOptions(bind, TimeSpan.FromSeconds(5));
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
        static IceSessionParameters CreateParameters(bool controlling, string localUfrag, string localPwd,
            string remoteUfrag, string remotePwd, ulong tiebreaker)
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
        /// 等待任务完成，超时抛带场景名的 TimeoutException，便于定位卡在哪一端
        /// </summary>
        /// <param name="task">待等待的任务</param>
        /// <param name="timeout">等待上限</param>
        /// <param name="scene">失败信息里的场景描述</param>
        /// <returns>任务结果</returns>
        /// <exception cref="TimeoutException">超过等待上限仍未完成</exception>
        static async Task<T> WaitAsync<T>(Task<T> task, TimeSpan timeout, string scene)
        {
            try
            {
                return await task.WaitAsync(timeout);
            }
            catch (TimeoutException)
            {
                throw new TimeoutException(scene + " 超时（" + (int)timeout.TotalMilliseconds + "ms）");
            }
        }

        /// <summary>
        /// 构造业务帧处理器：只保留第一帧，避免后续帧覆盖本次断言的样本
        /// </summary>
        /// <param name="source">接收完成源</param>
        /// <returns>可挂到 FrameReceived 上的处理器</returns>
        static Action<ReadOnlyMemory<byte>> CreateFrameHandler(TaskCompletionSource<byte[]> source)
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
        /// 把端点压成日志文本，端点为空时给出可读占位
        /// </summary>
        /// <param name="endPoint">待描述的端点，可为 null</param>
        /// <returns>端点文本</returns>
        static string Describe(IPEndPoint? endPoint)
        {
            return endPoint == null ? "null" : endPoint.ToString();
        }
    }
}