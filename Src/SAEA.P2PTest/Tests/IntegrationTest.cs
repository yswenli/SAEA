using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using SAEA.P2P.Builder;
using SAEA.P2P.Common;
using SAEA.P2P.Core;
using SAEA.P2P.Ice;
using SAEA.P2P.Model;
using SAEA.P2P.Signaling;

namespace SAEA.P2PTest.Tests
{
    /// <summary>
    /// 端到端集成测试：进程内信令服务端加 3 个客户端，校验注册结果、重复节点标识顶替、无可用 ICE 路径时建连失败与逐个释放后的节点清理
    /// </summary>
    public static class IntegrationTest
    {
        /// <summary>
        /// 注册、建连与节点数变化的等待上限，单位毫秒
        /// </summary>
        private const int ResultTimeoutMs = 15000;

        /// <summary>
        /// 用临时端口启动真实信令服务端并接入 3 个客户端，逐项校验注册结果、重复标识策略、无路径建连失败与释放清理
        /// </summary>
        /// <returns>异步任务，完成后该组场景与断言已全部执行</returns>
        public static async Task RunAsync()
        {
            TestHarness.Section("IntegrationTest");

            int port = TestHarness.GetFreeTcpPort();
            // 共享密钥留空：服务端不启鉴权挑战，注册即视为鉴权通过
            var serverOptions = new P2PServerBuilder()
                .SetPort(port)
                .SetIP("127.0.0.1")
                .SetMaxNodes(50)
                .SetFreeTime(60000)
                .EnableLogging(4)
                .Build();

            var server = new P2PServer(serverOptions);
            var clients = new List<P2PClient>();

            try
            {
                server.Start();
                TestHarness.Expect(await TestHarness.WaitUntil(() => server.IsRunning, 3000),
                    "server starts listening on 127.0.0.1", server.IsRunning.ToString());

                // 重复标识场景用独立的一对客户端，结束后把节点数清回 0，后续用例从干净状态开始计数
                await DuplicatePeerIdHandledAsync(server, port);

                var iceOptions = CreateShortBudgetIceOptions();
                clients.Add(CreateClient(port, "it-node-0", iceOptions));
                clients.Add(CreateClient(port, "it-node-1", iceOptions));
                // 只绑定回环地址且不收集回环候选：本端没有可用候选，建连凑不出候选对
                clients.Add(CreateClient(port, "it-node-2", CreateNoCandidateIceOptions()));

                await RegisterClientsAsync(server, clients);
                await ConnectWithoutIcePathFailsAsync(clients);
                await DisposeClientsAsync(server, clients);

                server.Stop();
                TestHarness.Expect(!server.IsRunning, "server reports IsRunning == false after Stop", server.IsRunning.ToString());
            }
            finally
            {
                foreach (var client in clients)
                {
                    try { client.Dispose(); }
                    catch (Exception ex) { TestHarness.Expect(false, "client dispose during cleanup", ex.GetType().Name + ": " + ex.Message); }
                }

                if (server.IsRunning)
                {
                    try { server.Stop(); }
                    catch (Exception ex) { TestHarness.Expect(false, "server stop during cleanup", ex.GetType().Name + ": " + ex.Message); }
                }
            }
        }

        /// <summary>
        /// 构造指向本地信令服务端的客户端配置
        /// </summary>
        /// <param name="port">信令服务端端口</param>
        /// <param name="peerId">本端节点标识</param>
        /// <param name="iceOptions">ICE 会话配置</param>
        /// <returns>尚未注册的客户端实例</returns>
        static P2PClient CreateClient(int port, string peerId, IceSessionOptions iceOptions)
        {
            var signaling = new SignalingOptions
            {
                PeerId = peerId,
                ServerAddress = "127.0.0.1",
                ServerPort = port,
                ConnectTimeoutMs = 5000,
                RegisterTimeoutMs = 8000,
                HeartbeatIntervalMs = 30000
            };

            return new P2PClient(signaling, iceOptions);
        }

        /// <summary>
        /// 把选路各级预算压到秒级，让失败场景在 15s 内给出结论；会话总预算必须覆盖收集预算与各级阶段预算之和
        /// </summary>
        /// <returns>短预算 ICE 会话配置</returns>
        static IceSessionOptions CreateShortBudgetIceOptions()
        {
            return new IceSessionOptions
            {
                GatheringBudget = TimeSpan.Zero,
                UdpDirectBudget = TimeSpan.FromSeconds(2),
                TcpDirectBudget = TimeSpan.FromSeconds(2),
                TurnStageBudget = TimeSpan.FromSeconds(1),
                SessionBudget = TimeSpan.FromSeconds(8)
            };
        }

        /// <summary>
        /// 构造一个本端零候选的 ICE 配置，用于制造「无可用 ICE 路径」
        /// </summary>
        /// <returns>只允许回环地址却不收集回环候选的 ICE 会话配置</returns>
        static IceSessionOptions CreateNoCandidateIceOptions()
        {
            var options = CreateShortBudgetIceOptions();
            // 绑定地址被限定为回环，而回环候选未开启，两者叠加保证收集结果为空
            options.Ice.BindAddresses.Add(IPAddress.Loopback);
            options.Ice.AllowLoopbackCandidates = false;
            return options;
        }

        /// <summary>
        /// 以重复节点标识注册两次，校验服务端「新连接顶替旧连接、旧连接收到 Error、节点表不增加」的实际策略
        /// </summary>
        /// <param name="server">已启动的信令服务端</param>
        /// <param name="port">信令服务端端口</param>
        /// <returns>异步任务，完成后重复标识场景已断言且节点数回到 0</returns>
        static async Task DuplicatePeerIdHandledAsync(P2PServer server, int port)
        {
            TestHarness.Section("duplicate peer id");

            var iceOptions = CreateShortBudgetIceOptions();
            var first = CreateClient(port, "it-dup-node", iceOptions);
            var second = CreateClient(port, "it-dup-node", iceOptions);

            string duplicateError = null;
            first.OnError += (code, message) => { if (code == ErrorCode.RegisterPeerIdDuplicate) duplicateError = code; };

            try
            {
                await first.RegisterAsync().WaitAsync(TimeSpan.FromMilliseconds(ResultTimeoutMs));
                bool firstCounted = await TestHarness.WaitUntil(() => server.NodeCount == 1, ResultTimeoutMs);
                TestHarness.Expect(firstCounted, "the first holder of the peer id is counted once", server.NodeCount.ToString());

                await second.RegisterAsync().WaitAsync(TimeSpan.FromMilliseconds(ResultTimeoutMs));

                // 服务端默认策略是新连接顶替旧连接：先给旧连接回 Error，再延迟断开，节点表沿用同一个键
                bool oldKicked = await TestHarness.WaitUntil(
                    () => duplicateError != null || !first.IsRegistered, ResultTimeoutMs);
                bool nodeCountFlat = server.NodeCount == 1;

                TestHarness.Expect(second.IsRegistered, "the duplicate registration itself succeeds", second.IsRegistered.ToString());
                TestHarness.Expect(nodeCountFlat, "the duplicate registration does not grow server NodeCount", server.NodeCount.ToString());
                TestHarness.Expect(oldKicked && (duplicateError != null || nodeCountFlat),
                    "duplicate peer id is taken over: the old connection gets Error or NodeCount stays flat",
                    "error=" + (duplicateError ?? "none") + ", oldRegistered=" + first.IsRegistered);
            }
            finally
            {
                first.Dispose();
                second.Dispose();
            }

            bool cleared = await TestHarness.WaitUntil(() => server.NodeCount == 0, ResultTimeoutMs);
            TestHarness.Expect(cleared, "duplicate scenario leaves no registered node", server.NodeCount.ToString());
        }

        /// <summary>
        /// 依次注册 3 个客户端，校验注册结果中的节点标识、客户端注册状态与服务端节点数
        /// </summary>
        /// <param name="server">已启动的信令服务端</param>
        /// <param name="clients">待注册的客户端列表</param>
        /// <returns>异步任务，完成后注册相关断言已全部执行</returns>
        static async Task RegisterClientsAsync(P2PServer server, List<P2PClient> clients)
        {
            TestHarness.Section("client registration");

            for (int i = 0; i < clients.Count; i++)
            {
                var client = clients[i];
                NodeInfo node = null;
                string failure = null;

                try
                {
                    node = await client.RegisterAsync().WaitAsync(TimeSpan.FromMilliseconds(ResultTimeoutMs));
                }
                catch (Exception ex)
                {
                    failure = ex.GetType().Name + ": " + ex.Message;
                }

                TestHarness.Expect(failure == null && node != null, "client " + client.PeerId + " registers", failure);
                TestHarness.Expect(node != null && string.Equals(node.NodeId, client.PeerId, StringComparison.Ordinal),
                    "registration result NodeId matches the configured peer id (" + client.PeerId + ")", node == null ? "null" : node.NodeId);
            }

            bool allRegistered = await TestHarness.WaitUntil(() => clients.All(c => c.IsRegistered), ResultTimeoutMs);
            TestHarness.Expect(allRegistered, "all 3 clients report IsRegistered",
                string.Join(",", clients.Select(c => c.PeerId + "=" + c.IsRegistered)));

            bool counted = await TestHarness.WaitUntil(() => server.NodeCount == 3, ResultTimeoutMs);
            TestHarness.Expect(counted, "server NodeCount == 3 after 3 registrations", server.NodeCount.ToString());
        }

        /// <summary>
        /// 在本端零候选时发起建连，校验 15s 内给出失败结论：抛 P2PException 或始终不触发 OnPeerConnected
        /// </summary>
        /// <param name="clients">已注册的客户端列表</param>
        /// <returns>异步任务，完成后无路径建连相关断言已执行</returns>
        static async Task ConnectWithoutIcePathFailsAsync(List<P2PClient> clients)
        {
            TestHarness.Section("connect without an ICE path");

            var initiator = clients[0];
            var unreachable = clients[2];

            bool peerConnected = false;
            initiator.OnPeerConnected += connection =>
            {
                if (connection != null && string.Equals(connection.PeerId, unreachable.PeerId, StringComparison.Ordinal))
                {
                    peerConnected = true;
                }
            };

            Exception failure = null;
            bool timedOut = false;
            var connectTask = initiator.ConnectToPeerAsync(unreachable.PeerId);

            try
            {
                await connectTask.WaitAsync(TimeSpan.FromMilliseconds(ResultTimeoutMs));
            }
            catch (TimeoutException)
            {
                timedOut = true;
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            if (timedOut)
            {
                // 超时后建连任务仍在后台收尾，显式观察异常，避免无人接管的任务异常
                connectTask.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            }

            // 口径：无可用 ICE 路径时要么抛 P2PException，要么 15s 内不触发 OnPeerConnected
            bool threw = failure is P2PException;
            TestHarness.Expect(threw || (timedOut && !peerConnected),
                "ConnectToPeerAsync without an ICE path throws P2PException or fires no OnPeerConnected within 15s",
                "failure=" + (failure == null ? "none" : failure.GetType().Name)
                + ", timedOut=" + timedOut + ", peerConnected=" + peerConnected);
        }

        /// <summary>
        /// 逐个释放客户端，校验服务端节点数同步递减、掉线事件标识与释放的标识一致
        /// </summary>
        /// <param name="server">运行中的信令服务端</param>
        /// <param name="clients">已注册的客户端列表</param>
        /// <returns>异步任务，完成后释放清理相关断言已执行</returns>
        static async Task DisposeClientsAsync(P2PServer server, List<P2PClient> clients)
        {
            TestHarness.Section("dispose cleanup");

            var unregistered = new List<string>();
            server.OnNodeUnregistered += nodeId =>
            {
                if (nodeId == null)
                {
                    return;
                }

                lock (unregistered)
                {
                    unregistered.Add(nodeId);
                }
            };

            for (int i = 0; i < clients.Count; i++)
            {
                var client = clients[i];
                int before = server.NodeCount;
                client.Dispose();

                int after = server.NodeCount;
                bool decreased = await TestHarness.WaitUntil(() => (after = server.NodeCount) == before - 1, ResultTimeoutMs);
                TestHarness.Expect(decreased, "dispose of " + client.PeerId + " decrements server NodeCount",
                    before + " -> " + after);
            }

            bool empty = await TestHarness.WaitUntil(() => server.NodeCount == 0, ResultTimeoutMs);
            TestHarness.Expect(empty, "server has no registered node after disposing every client", server.NodeCount.ToString());

            List<string> received;
            lock (unregistered)
            {
                received = unregistered.ToList();
            }

            var expectedIds = clients.Select(c => c.PeerId).OrderBy(id => id, StringComparer.Ordinal).ToList();
            var receivedIds = received.Distinct().OrderBy(id => id, StringComparer.Ordinal).ToList();
            TestHarness.Expect(expectedIds.SequenceEqual(receivedIds), "OnNodeUnregistered ids match the disposed peer ids",
                "expected=" + string.Join(",", expectedIds) + "; received=" + string.Join(",", receivedIds));

            // 留出窗口让服务端处理完最后的断开回调，避免停止监听时仍在处理断开
            await Task.Delay(150);
        }
    }
}