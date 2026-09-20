using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SAEA.P2P.Builder;
using SAEA.P2P.Common;
using SAEA.P2P.Core;

namespace SAEA.P2PTest.Tests
{
    /// <summary>
    /// 客户端生命周期测试：纯局域网连接、守卫子句、连接服务器与注销。
    /// </summary>
    public static class LifecycleTest
    {
        public static async Task RunAsync()
        {
            TestHarness.Section("LifecycleTest");

            await LocalOnlyLifecycle();
            await ClientGuardClauses();
            await ServerConnectLifecycle();

            TestHarness.WriteSummary("LifecycleTest");
        }

        static void Quiet(P2POptions options)
        {
            options.Discovery.EnableLocalDiscovery = false;
            options.Logging.Level = 4;
        }

        static async Task LocalOnlyLifecycle()
        {
            TestHarness.Section("local-only lifecycle");

            var options = new P2PClientBuilder().SetNodeId("life-local").Build();
            Quiet(options);

            var client = new P2PClient(options);
            var states = new List<NodeState>();
            client.OnStateChanged += (_, next) => states.Add(next);

            await client.ConnectAsync();

            TestHarness.Expect(client.State == NodeState.Connected, "local-only connect reaches Connected");
            TestHarness.Expect(client.IsConnected, "IsConnected true in local-only mode");

            bool secondThrew = false;
            try { await client.ConnectAsync(); }
            catch (P2PException) { secondThrew = true; }
            TestHarness.Expect(secondThrew, "second connect throws");

            client.Disconnect();
            TestHarness.Expect(client.State == NodeState.Disconnected, "disconnect sets Disconnected");
            TestHarness.Expect(states.Contains(NodeState.Connecting) && states.Contains(NodeState.Connected) && states.Contains(NodeState.Disconnected),
                "state transitions observed");

            client.Disconnect();
            TestHarness.Expect(true, "double disconnect is safe");
        }

        static async Task ClientGuardClauses()
        {
            TestHarness.Section("client guard clauses");

            var options = new P2PClientBuilder().SetNodeId("life-guards").Build();
            Quiet(options);

            var client = new P2PClient(options);

            TestHarness.Throws<P2PException>(() => client.Send("peer", new byte[] { 1 }), "send with unknown session throws");
            TestHarness.Throws<P2PException>(() => client.Send("peer", new byte[0]), "send empty payload throws");
            TestHarness.Throws<P2PException>(() => client.Send("peer", null), "send null payload throws");
            TestHarness.Throws<P2PException>(() => client.SendRelayRequest("peer"), "relay request without signal socket throws");
            TestHarness.Throws<P2PException>(() => client.SendHeartbeat(), "heartbeat without signal socket throws");

            bool punchThrew = false;
            try { await client.ConnectToPeerAsync("peer"); }
            catch (P2PException) { punchThrew = true; }
            TestHarness.Expect(punchThrew, "connect to peer before register throws");
        }

        static async Task ServerConnectLifecycle()
        {
            TestHarness.Section("server connect lifecycle");

            int port = TestHarness.GetFreeTcpPort();
            var serverOptions = new P2PServerBuilder().SetPort(port).SetMaxNodes(20).EnableRelay().Build();
            serverOptions.Logging.Level = 4;

            var server = new P2PServer(serverOptions);
            server.Start();
            await Task.Delay(200);

            var options = new P2PClientBuilder().SetServer("127.0.0.1", port).SetNodeId("life-server").Build();
            Quiet(options);

            var client = new P2PClient(options);
            bool serverConnected = false;
            client.OnServerConnected += () => serverConnected = true;

            try
            {
                await client.ConnectAsync();
            }
            catch (Exception ex)
            {
                TestHarness.Expect(false, "client connect threw unexpectedly", ex.Message);
            }

            bool registered = await TestHarness.WaitUntil(() => client.State == NodeState.Registered, 6000);
            TestHarness.Expect(registered, "client registers with server", client.State.ToString());
            TestHarness.Expect(serverConnected, "OnServerConnected fired");
            TestHarness.Expect(server.NodeCount == 1, "server tracks the node", server.NodeCount.ToString());

            await client.ConnectToPeerAsync("ghost-peer");
            await Task.Delay(200);
            TestHarness.Expect(true, "punch request to unknown peer does not crash");

            client.Disconnect();
            bool dropped = await TestHarness.WaitUntil(() => server.NodeCount == 0, 6000);
            TestHarness.Expect(dropped, "server drops node after disconnect", server.NodeCount.ToString());

            server.Stop();
        }
    }
}