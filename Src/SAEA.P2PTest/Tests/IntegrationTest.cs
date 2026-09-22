using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SAEA.P2P.Builder;
using SAEA.P2P.Channel;
using SAEA.P2P.Common;
using SAEA.P2P.Core;

namespace SAEA.P2PTest.Tests
{
    /// <summary>
    /// 端到端集成测试：多客户端注册、重复 NodeId、打洞会话、中继双向收发、断开清理。
    /// </summary>
    public static class IntegrationTest
    {
        public static async Task RunAsync()
        {
            TestHarness.Section("IntegrationTest");

            int port = TestHarness.GetFreeTcpPort();
            var serverOptions = new P2PServerBuilder().SetPort(port).SetMaxNodes(50).EnableRelay().Build();
            serverOptions.Logging.Level = 4;

            var server = new P2PServer(serverOptions);
            server.Start();
            await Task.Delay(250);

            var clients = new List<P2PClient>();
            for (int i = 0; i < 3; i++)
            {
                clients.Add(CreateClient(port, $"it-node-{i}"));
            }

            foreach (var client in clients)
            {
                try { await client.ConnectAsync(); }
                catch (Exception ex) { TestHarness.Expect(false, $"client {client.NodeId} connect threw", ex.Message); }
            }

            bool allRegistered = await TestHarness.WaitUntil(() => clients.All(c => c.State == NodeState.Registered), 8000);
            TestHarness.Expect(allRegistered, "all 3 clients registered",
                string.Join(",", clients.Select(c => c.State.ToString())));
            TestHarness.Expect(server.NodeCount == 3, "server NodeCount == 3", server.NodeCount.ToString());

            bool knows = await TestHarness.WaitUntil(() => clients[0].KnownNodes.Count == 2, 5000);
            TestHarness.Expect(knows, "client0 knows the other 2 peers", clients[0].KnownNodes.Count.ToString());

            await DuplicateNodeRejected(port);
            await PunchSessions(clients);
            await DirectUserDataRoundTrip(clients);
            await RelayRoundTrip(server, clients);
            await DisconnectDropsNode(server, clients);
            await StopAll(clients, server);
        }

        static P2PClient CreateClient(int port, string nodeId)
        {
            var options = new P2PClientBuilder()
                .SetServer("127.0.0.1", port)
                .SetNodeId(nodeId)
                .EnableRelay()
                .Build();

            options.Discovery.EnableLocalDiscovery = false;
            options.Logging.Level = 4;
            return new P2PClient(options);
        }

        static async Task DuplicateNodeRejected(int port)
        {
            TestHarness.Section("duplicate node id");

            var duplicate = CreateClient(port, "it-node-0");
            bool errorRaised = false;
            duplicate.OnError += (code, _) => { if (code == ErrorCode.RegisterPeerIdDuplicate) errorRaised = true; };

            try { await duplicate.ConnectAsync(); }
            catch { }

            bool rejected = await TestHarness.WaitUntil(() => duplicate.State == NodeState.Error || errorRaised, 5000);
            TestHarness.Expect(rejected, "duplicate node id is rejected", duplicate.State.ToString());

            duplicate.Disconnect();
            await Task.Delay(150);
        }

        static async Task PunchSessions(List<P2PClient> clients)
        {
            TestHarness.Section("punch sessions via signal server");

            var a = clients[0];
            var b = clients[1];

            await a.ConnectToPeerAsync("it-node-1");

            bool punched = await TestHarness.WaitUntil(
                () => a.GetSession("it-node-1") != null && b.GetSession("it-node-0") != null, 6000);
            TestHarness.Expect(punched, "punch establishes sessions on both sides");

            var session = a.GetSession("it-node-1");
            TestHarness.Expect(session != null && session.Channel == ChannelType.Direct, "punch session uses Direct channel");
            TestHarness.Expect(session != null && !string.IsNullOrEmpty(session.PublicAddress), "punch session carries public address");
            TestHarness.Expect(session != null && session.PublicPort > 0, "punch session carries public port");
        }

        static async Task DirectUserDataRoundTrip(List<P2PClient> clients)
        {
            TestHarness.Section("direct user-data round trip");

            var a = clients[0];
            var b = clients[1];

            TestHarness.Expect(a.GetSession("it-node-1") != null &&
                               a.GetSession("it-node-1").Channel == ChannelType.Direct,
                "client0 session is direct before forwarding");

            byte[] fromA = null;
            string sourceA = null;
            b.OnMessageReceived += (peer, data) => { sourceA = peer; fromA = data; };

            var payload = Encoding.UTF8.GetBytes("direct-hello");
            a.Send("it-node-1", payload);

            bool delivered = await TestHarness.WaitUntil(() => fromA != null, 6000);
            TestHarness.Expect(delivered, "direct user-data delivered to peer");
            TestHarness.Expect(delivered && sourceA == "it-node-0", "direct user-data source id correct", sourceA);
            TestHarness.Expect(delivered && fromA.SequenceEqual(payload), "direct user-data payload byte-exact");
        }

        static async Task RelayRoundTrip(P2PServer server, List<P2PClient> clients)
        {
            TestHarness.Section("relay round trip");

            var a = clients[0];
            var b = clients[1];

            byte[] fromA = null;
            string sourceA = null;
            b.OnMessageReceived += (peer, data) => { sourceA = peer; fromA = data; };

            a.SendRelayRequest("it-node-1");

            bool relayReady = await TestHarness.WaitUntil(
                () => a.GetSession("it-node-1") != null &&
                      a.GetSession("it-node-1").Channel == ChannelType.Relay &&
                      b.GetSession("it-node-0") != null &&
                      b.GetSession("it-node-0").Channel == ChannelType.Relay, 6000);
            TestHarness.Expect(relayReady, "relay ack binds sessions on both sides");

            var sessionA = a.GetSession("it-node-1");
            TestHarness.Expect(sessionA != null && !string.IsNullOrEmpty(sessionA.RelaySessionId), "relay session id assigned");

            var payload = Encoding.UTF8.GetBytes("relay-hello");
            a.Send("it-node-1", payload);

            bool delivered = await TestHarness.WaitUntil(() => fromA != null, 6000);
            TestHarness.Expect(delivered, "relay delivered message to peer");
            TestHarness.Expect(delivered && sourceA == "it-node-0", "relay source id correct", sourceA);
            TestHarness.Expect(delivered && fromA.SequenceEqual(payload), "relay payload byte-exact");
            TestHarness.Expect(server.ActiveRelaySessionCount >= 1, "server counts active relay session", server.ActiveRelaySessionCount.ToString());

            byte[] fromB = null;
            string sourceB = null;
            a.OnMessageReceived += (peer, data) => { sourceB = peer; fromB = data; };

            var payloadB = Encoding.UTF8.GetBytes("relay-back");
            b.Send("it-node-0", payloadB);

            bool deliveredBack = await TestHarness.WaitUntil(() => fromB != null, 6000);
            TestHarness.Expect(deliveredBack, "reverse relay delivered");
            TestHarness.Expect(deliveredBack && sourceB == "it-node-1", "reverse relay source id correct", sourceB);
            TestHarness.Expect(deliveredBack && fromB.SequenceEqual(payloadB), "reverse relay payload byte-exact");
        }

        static async Task DisconnectDropsNode(P2PServer server, List<P2PClient> clients)
        {
            TestHarness.Section("disconnect cleanup");

            clients[2].Disconnect();

            bool decreased = await TestHarness.WaitUntil(() => server.NodeCount == 2, 6000);
            TestHarness.Expect(decreased, "server drops disconnected node", server.NodeCount.ToString());
        }

        static async Task StopAll(List<P2PClient> clients, P2PServer server)
        {
            foreach (var client in clients)
            {
                client.Disconnect();
            }

            await Task.Delay(200);
            server.Stop();
            TestHarness.Expect(true, "integration environment torn down");
        }
    }
}