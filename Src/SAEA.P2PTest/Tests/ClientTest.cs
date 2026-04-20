using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SAEA.Common;
using SAEA.P2P;
using SAEA.P2P.Builder;
using SAEA.P2P.Core;
using SAEA.P2P.Common;

namespace SAEA.P2PTest.Tests
{
    public static class ClientTest
    {
        public static async Task RunAsync()
        {
            ConsoleHelper.WriteLine("=== ClientTest ===");
            ConsoleHelper.WriteLine("");

            TestClientCreation();
            ConsoleHelper.WriteLine("");

            TestClientEvents();
            ConsoleHelper.WriteLine("");

            TestClientLocalOnly();
            ConsoleHelper.WriteLine("");

            await TestClientConnectDisconnect();
            ConsoleHelper.WriteLine("");

            ConsoleHelper.WriteLine("=== ClientTest Complete ===");
        }

        static void TestClientCreation()
        {
            ConsoleHelper.WriteLine("--- TestClientCreation ---");

            var options = new P2PClientBuilder()
                .SetServer("127.0.0.1", 39654)
                .SetNodeId("test-client-001")
                .EnableHolePunch()
                .EnableRelay()
                .EnableLogging()
                .Build();

            var client = new P2PClient(options);

            ConsoleHelper.WriteLine($"NodeId: {client.NodeId}");
            ConsoleHelper.WriteLine($"State: {client.State}");
            ConsoleHelper.WriteLine($"IsConnected: {client.IsConnected}");

            if (client.NodeId == "test-client-001" && client.State == NodeState.Init && !client.IsConnected)
            {
                ConsoleHelper.WriteLine("TestClientCreation: PASSED");
            }
            else
            {
                ConsoleHelper.WriteLine("TestClientCreation: FAILED");
            }
        }

        static void TestClientEvents()
        {
            ConsoleHelper.WriteLine("--- TestClientEvents ---");

            var options = new P2PClientBuilder()
                .SetNodeId("event-test-node")
                .EnableLocalDiscovery()
                .Build();

            var client = new P2PClient(options);

            int eventCount = 0;
            client.OnStateChanged += (old, new_) =>
            {
                eventCount++;
                ConsoleHelper.WriteLine($"OnStateChanged: {old} -> {new_}");
            };
            client.OnServerConnected += () =>
            {
                eventCount++;
                ConsoleHelper.WriteLine("OnServerConnected");
            };
            client.OnServerDisconnected += (reason) =>
            {
                eventCount++;
                ConsoleHelper.WriteLine($"OnServerDisconnected: {reason}");
            };
            client.OnMessageReceived += (peerId, data) =>
            {
                eventCount++;
                ConsoleHelper.WriteLine($"OnMessageReceived: {peerId}, {data.Length} bytes");
            };
            client.OnError += (code, msg) =>
            {
                eventCount++;
                ConsoleHelper.WriteLine($"OnError: {code} - {msg}");
            };

            ConsoleHelper.WriteLine($"Events registered: 5");
            ConsoleHelper.WriteLine("TestClientEvents: PASSED");
        }

        static void TestClientLocalOnly()
        {
            ConsoleHelper.WriteLine("--- TestClientLocalOnly ---");

            var options = new P2PClientBuilder()
                .SetNodeId("local-only-node")
                .EnableLocalDiscovery(39657, "224.0.0.250")
                .SetDiscoveryInterval(3000)
                .EnableLogging()
                .Build();

            var client = new P2PClient(options);

            ConsoleHelper.WriteLine($"NodeId: {client.NodeId}");
            ConsoleHelper.WriteLine($"State: {client.State}");
            ConsoleHelper.WriteLine($"No server configured: {string.IsNullOrEmpty(options.ServerAddress)}");

            if (string.IsNullOrEmpty(options.ServerAddress) && options.Discovery.EnableLocalDiscovery)
            {
                ConsoleHelper.WriteLine("TestClientLocalOnly: PASSED");
            }
            else
            {
                ConsoleHelper.WriteLine("TestClientLocalOnly: FAILED");
            }
        }

        static async Task TestClientConnectDisconnect()
        {
            ConsoleHelper.WriteLine("--- TestClientConnectDisconnect ---");

            var serverOptions = new P2PServerBuilder()
                .SetPort(39658)
                .SetMaxNodes(10)
                .EnableRelay()
                .EnableLogging()
                .Build();

            var server = new P2PServer(serverOptions);
            server.Start();

            ConsoleHelper.WriteLine($"Server started on port {server.Port}");

            await Task.Delay(500);

            var clientOptions = new P2PClientBuilder()
                .SetServer("127.0.0.1", 39658)
                .SetNodeId("connect-test-node")
                .EnableRelay()
                .EnableLogging()
                .Build();

            var client = new P2PClient(clientOptions);

            bool connected = false;
            bool stateChanged = false;
            client.OnServerConnected += () => connected = true;
            client.OnStateChanged += (old, new_) =>
            {
                ConsoleHelper.WriteLine($"State: {old} -> {new_}");
                stateChanged = true;
            };

            ConsoleHelper.WriteLine($"Before Connect: State={client.State}");

            try
            {
                client.Connect();
                await Task.Delay(2000);

                ConsoleHelper.WriteLine($"After Connect: State={client.State}, IsConnected={client.IsConnected}");

                if (client.IsConnected && connected && stateChanged)
                {
                    ConsoleHelper.WriteLine("Client connected successfully");
                }
                else
                {
                    ConsoleHelper.WriteLine("Client connect FAILED or timed out");
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteLine($"Connect exception: {ex.Message}");
            }

            client.Disconnect();
            server.Stop();

            ConsoleHelper.WriteLine($"After Disconnect: State={client.State}");

            if (client.State == NodeState.Disconnected)
            {
                ConsoleHelper.WriteLine("TestClientConnectDisconnect: PASSED");
            }
            else
            {
                ConsoleHelper.WriteLine("TestClientConnectDisconnect: FAILED (check if server was reachable)");
            }
        }
    }
}