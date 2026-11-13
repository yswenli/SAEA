using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using SAEA.P2P;
using SAEA.P2P.Builder;
using SAEA.P2P.Common;
using SAEA.P2P.Core;
using SAEA.P2P.Ice;
using SAEA.P2P.Model;
using SAEA.P2P.Signaling;

namespace SAEA.P2PTest.Tests
{
    /// <summary>
    /// 客户端生命周期测试：无信令入口被明确拒绝、配置守卫子句、接入真实服务端后的注册与注销。
    /// </summary>
    public static class LifecycleTest
    {
        /// <summary>
        /// 依次校验无信令局域网入口的拒绝、信令配置非法值抛错，以及接入真实服务端后的注册与注销流程
        /// </summary>
        /// <returns>异步任务，完成后该组场景与断言已全部执行</returns>
        public static async Task RunAsync()
        {
            TestHarness.Section("LifecycleTest");

            LocalNetUnsupported();
            await ClientGuardClauses();
            await ServerRegisterLifecycle();

            TestHarness.WriteSummary("LifecycleTest");
        }

        /// <summary>
        /// 构造指向指定信令服务器的客户端配置，超时取秒级以便用例快速失败
        /// </summary>
        /// <param name="peerId">本端节点标识</param>
        /// <param name="serverAddress">信令服务器地址</param>
        /// <param name="serverPort">信令服务器端口</param>
        /// <returns>可直接交给客户端构造器的信令配置</returns>
        static SignalingOptions CreateSignaling(string peerId, string serverAddress = "127.0.0.1", int serverPort = 39654)
        {
            return new SignalingOptions
            {
                PeerId = peerId,
                ServerAddress = serverAddress,
                ServerPort = serverPort,
                ConnectTimeoutMs = 5000,
                RegisterTimeoutMs = 8000,
                HeartbeatIntervalMs = 30000
            };
        }

        static P2PClient CreateClient(SignalingOptions signaling)
        {
            return new P2PClient(signaling, new IceSessionOptions());
        }

        static void LocalNetUnsupported()
        {
            TestHarness.Section("local-only lifecycle");

            // 无信令局域网直连已下线，两个重载都必须显式拒绝而不是返回半可用客户端
            var single = TestHarness.Throws<NotSupportedException>(() => P2PQuick.LocalNet("life-local"),
                "P2PQuick.LocalNet(nodeId) throws NotSupportedException");
            TestHarness.Expect(single != null && single.Message.Contains("信令服务器"),
                "LocalNet message explains the signaling server is required", single?.Message);
            TestHarness.Throws<NotSupportedException>(() => P2PQuick.LocalNet("life-local", 39655, "224.0.0.250"),
                "P2PQuick.LocalNet(nodeId, port, multicast) throws NotSupportedException");
        }

        static async Task ClientGuardClauses()
        {
            TestHarness.Section("client guard clauses");

            var badInterval = TestHarness.Throws<P2PException>(
                () => new SignalingOptions { PeerId = "life-guards", ReconnectIntervalMs = 0 }.Validate(),
                "SignalingOptions.Validate rejects zero reconnect interval");
            TestHarness.Expect(badInterval != null && badInterval.ErrorCode == ErrorCode.RegisterServerUnavailable,
                "guard failure reports RegisterServerUnavailable", badInterval?.ErrorCode);
            TestHarness.Throws<P2PException>(
                () => new SignalingOptions { PeerId = "life-guards", ServerPort = 70000 }.Validate(),
                "SignalingOptions.Validate rejects out-of-range port");

            var client = CreateClient(CreateSignaling("life-guards"));
            try
            {
                // Send 只校验会话是否可用，不校验载荷是否为空，因此这里只断言未连接路径
                var sendError = TestHarness.Throws<P2PException>(() => client.Send("peer", new byte[] { 1 }),
                    "send with unknown session throws");
                TestHarness.Expect(sendError != null && sendError.ErrorCode == ErrorCode.NotConnected,
                    "send reports NotConnected", sendError?.ErrorCode);

                bool connectThrew = false;
                try
                {
                    await client.ConnectToPeerAsync("peer");
                }
                catch (P2PException)
                {
                    connectThrew = true;
                }
                TestHarness.Expect(connectThrew, "connect to peer before register throws");
            }
            finally
            {
                client.Dispose();
            }
        }

        /// <summary>
        /// 等待注册完成；返回 null 表示成功，否则返回失败原因，避免用例无限等待
        /// </summary>
        /// <param name="client">待注册的客户端</param>
        /// <param name="timeoutMs">等待注册完成的毫秒数</param>
        /// <returns>成功时为 null；超时或异常时为失败原因</returns>
        static async Task<string?> TryRegisterAsync(P2PClient client, int timeoutMs)
        {
            Task<NodeInfo> registerTask;
            try
            {
                registerTask = client.RegisterAsync();
            }
            catch (Exception ex)
            {
                return ex.GetType().Name + ": " + ex.Message;
            }

            var completed = await Task.WhenAny(registerTask, Task.Delay(timeoutMs));
            if (!ReferenceEquals(completed, registerTask))
            {
                return "register timed out after " + timeoutMs + "ms";
            }

            try
            {
                await registerTask;
                return client.IsRegistered ? null : "client reports not registered";
            }
            catch (Exception ex)
            {
                return ex.GetType().Name + ": " + ex.Message;
            }
        }

        static async Task ServerRegisterLifecycle()
        {
            TestHarness.Section("server register lifecycle");

            int port = TestHarness.GetFreeTcpPort();
            var serverOptions = new P2PServerBuilder()
                .SetPort(port)
                .SetIP("127.0.0.1")
                .SetMaxNodes(20)
                .SetFreeTime(60000)
                .EnableLogging(4)
                .Build();

            var server = new P2PServer(serverOptions);
            var unregisteredNodes = new List<string>();
            server.OnNodeUnregistered += nodeId =>
            {
                lock (unregisteredNodes)
                {
                    unregisteredNodes.Add(nodeId);
                }
            };

            var client = CreateClient(CreateSignaling("life-server", "127.0.0.1", port));

            try
            {
                server.Start();
                TestHarness.Expect(await TestHarness.WaitUntil(() => server.IsRunning, 3000),
                    "server starts listening", server.IsRunning.ToString());

                var registerFailure = await TryRegisterAsync(client, 15000);
                TestHarness.Expect(registerFailure == null, "client registers with server", registerFailure);
                TestHarness.Expect(client.IsRegistered, "client reports IsRegistered after RegisterAsync");
                TestHarness.Expect(await TestHarness.WaitUntil(() => server.NodeCount == 1, 5000),
                    "server tracks the registered node", server.NodeCount.ToString());
            }
            finally
            {
                // 释放客户端即断开信令连接，服务端应据此移除节点并触发注销事件
                client.Dispose();
            }

            try
            {
                TestHarness.Expect(await TestHarness.WaitUntil(() => server.NodeCount == 0, 10000),
                    "server drops node after client dispose", server.NodeCount.ToString());

                bool sawUnregister;
                lock (unregisteredNodes)
                {
                    sawUnregister = unregisteredNodes.Contains("life-server");
                }
                TestHarness.Expect(sawUnregister, "OnNodeUnregistered fired for the disposed client");
            }
            finally
            {
                server.Stop();
            }
        }
    }
}