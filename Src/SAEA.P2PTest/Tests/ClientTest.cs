using System;
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
    /// P2P 客户端自测：创建后的初始状态、事件订阅、未连接时的发送守卫、重复释放、无信令入口的启动，以及接入本地信令服务器的注册流程
    /// </summary>
    public static class ClientTest
    {
        /// <summary>
        /// 依次校验客户端创建后的节点状态、事件订阅、未注册发送、重复释放、无信令入口，以及注册到本地信令服务器的流程
        /// </summary>
        /// <returns>异步任务，完成后该组场景与断言已全部执行</returns>
        public static async Task RunAsync()
        {
            TestHarness.Section("ClientTest");

            TestClientCreation();
            TestClientEvents();
            TestClientSendBeforeRegister();
            TestClientDisposeIsIdempotent();
            TestLocalNetEntry();
            await TestClientRegister();

            TestHarness.WriteSummary("ClientTest");
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

        /// <summary>
        /// 用默认 ICE 配置创建客户端，信令配置由调用方提供
        /// </summary>
        /// <param name="signaling">信令配置</param>
        /// <returns>尚未注册的客户端实例</returns>
        static P2PClient CreateClient(SignalingOptions signaling)
        {
            return new P2PClient(signaling, new IceSessionOptions());
        }

        static void TestClientCreation()
        {
            TestHarness.Section("client creation");

            var client = CreateClient(CreateSignaling("client-create"));
            try
            {
                TestHarness.Expect(client.PeerId == "client-create", "PeerId comes from the signaling options", client.PeerId);
                TestHarness.Expect(!client.IsRegistered, "client is not registered before RegisterAsync", client.IsRegistered.ToString());
            }
            finally
            {
                client.Dispose();
            }
        }

        static void TestClientEvents()
        {
            TestHarness.Section("client events");

            var client = CreateClient(CreateSignaling("client-events"));
            bool subscribed;
            try
            {
                // 回调体留空：本用例只验证五个事件都能挂接，不触发任何回调
                client.OnPeerConnected += connection => { };
                client.OnPeerDisconnected += (peerId, reason) => { };
                client.OnPeerDiscovered += node => { };
                client.OnDataReceived += (peerId, data) => { };
                client.OnError += (code, message) => { };
                subscribed = true;
            }
            catch (Exception ex)
            {
                subscribed = false;
                TestHarness.Expect(false, "all five client events accept subscriptions", ex.GetType().Name + ": " + ex.Message);
            }

            if (subscribed)
            {
                TestHarness.Expect(true, "all five client events accept subscriptions");
            }

            client.Dispose();
        }

        static void TestClientSendBeforeRegister()
        {
            TestHarness.Section("client send before register");

            var client = CreateClient(CreateSignaling("client-send"));
            try
            {
                // Send 只校验会话是否可用，未注册时没有会话，必须报 NotConnected 而不是静默丢弃
                var sendError = TestHarness.Throws<P2PException>(() => client.Send("peer", new byte[] { 1 }),
                    "send before register throws P2PException");
                TestHarness.Expect(sendError != null && sendError.ErrorCode == ErrorCode.NotConnected,
                    "send before register reports NotConnected", sendError?.ErrorCode);
            }
            finally
            {
                client.Dispose();
            }
        }

        static void TestClientDisposeIsIdempotent()
        {
            TestHarness.Section("client dispose");

            var client = CreateClient(CreateSignaling("client-dispose"));
            string? failure = null;
            try
            {
                client.Dispose();
                client.Dispose();
            }
            catch (Exception ex)
            {
                failure = ex.GetType().Name + ": " + ex.Message;
            }

            TestHarness.Expect(failure == null, "Dispose can be called repeatedly", failure);
        }

        static void TestLocalNetEntry()
        {
            TestHarness.Section("local-only entry");

            // 无信令入口已恢复，两个重载都应返回可直接启动发现的客户端，不再依赖信令服务器
            var single = P2PQuick.LocalNet("client-local");
            try
            {
                TestHarness.Expect(single != null, "P2PQuick.LocalNet(nodeId) returns a client");
                TestHarness.Expect(single.PeerId == "client-local", "LocalNet keeps the node id", single.PeerId);
                single.StartLocalAsync().GetAwaiter().GetResult();
                TestHarness.Expect(single.IsRegistered, "LocalNet client is registered after StartLocalAsync", single.IsRegistered.ToString());
            }
            finally
            {
                single.Dispose();
            }

            var withDiscovery = P2PQuick.LocalNet("client-local", 39655, "224.0.0.250");
            try
            {
                TestHarness.Expect(withDiscovery != null, "P2PQuick.LocalNet(nodeId, port, multicast) returns a client");
                TestHarness.Expect(withDiscovery.PeerId == "client-local", "LocalNet keeps the node id with explicit discovery options", withDiscovery.PeerId);
                withDiscovery.StartLocalAsync().GetAwaiter().GetResult();
                TestHarness.Expect(withDiscovery.IsRegistered, "LocalNet client is registered after StartLocalAsync", withDiscovery.IsRegistered.ToString());
            }
            finally
            {
                withDiscovery.Dispose();
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

        static async Task TestClientRegister()
        {
            TestHarness.Section("client register");

            int port = TestHarness.GetFreeTcpPort();
            // 共享密钥留空：服务端不启鉴权挑战，注册即视为鉴权通过
            var serverOptions = new P2PServerBuilder()
                .SetPort(port)
                .SetIP("127.0.0.1")
                .SetMaxNodes(10)
                .SetFreeTime(60000)
                .EnableLogging(4)
                .Build();

            var server = new P2PServer(serverOptions);
            var client = CreateClient(CreateSignaling("client-register", "127.0.0.1", port));

            try
            {
                server.Start();
                TestHarness.Expect(await TestHarness.WaitUntil(() => server.IsRunning, 3000),
                    "server starts listening for client registration", server.IsRunning.ToString());

                var registerFailure = await TryRegisterAsync(client, 15000);
                TestHarness.Expect(registerFailure == null, "client registers with the local signaling server", registerFailure);
                TestHarness.Expect(client.IsRegistered, "client reports IsRegistered after RegisterAsync");
            }
            finally
            {
                // 先释放客户端即断开信令连接，再停服务端，避免服务端在停止过程中处理断开
                client.Dispose();
                server.Stop();
            }
        }
    }
}