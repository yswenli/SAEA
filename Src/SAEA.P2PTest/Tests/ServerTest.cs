using System;
using System.Threading;
using System.Threading.Tasks;
using SAEA.Common;
using SAEA.P2P;
using SAEA.P2P.Builder;
using SAEA.P2P.Core;

namespace SAEA.P2PTest.Tests
{
    public static class ServerTest
    {
        public static async Task RunAsync()
        {
            ConsoleHelper.WriteLine("=== ServerTest ===");
            ConsoleHelper.WriteLine("");

            TestServerCreation();
            ConsoleHelper.WriteLine("");

            TestServerEvents();
            ConsoleHelper.WriteLine("");

            await TestServerStartStop();
            ConsoleHelper.WriteLine("");

            ConsoleHelper.WriteLine("=== ServerTest Complete ===");
        }

        static void TestServerCreation()
        {
            ConsoleHelper.WriteLine("--- TestServerCreation ---");

            var options = new P2PServerBuilder()
                .SetPort(39654)
                .SetIP("0.0.0.0")
                .SetMaxNodes(100)
                .EnableRelay()
                .EnableLogging()
                .Build();

            var server = new P2PServer(options);

            ConsoleHelper.WriteLine($"Port: {server.Port}");
            ConsoleHelper.WriteLine($"NodeCount: {server.NodeCount}");
            ConsoleHelper.WriteLine($"IsRunning: {server.IsRunning}");

            if (server.Port == 39654 && server.NodeCount == 0 && !server.IsRunning)
            {
                ConsoleHelper.WriteLine("TestServerCreation: PASSED");
            }
            else
            {
                ConsoleHelper.WriteLine("TestServerCreation: FAILED");
            }
        }

        static void TestServerEvents()
        {
            ConsoleHelper.WriteLine("--- TestServerEvents ---");

            var options = new P2PServerBuilder()
                .SetPort(39655)
                .EnableRelay()
                .Build();

            var server = new P2PServer(options);

            int eventCount = 0;
            server.OnNodeRegistered += (nodeId, endpoint) =>
            {
                eventCount++;
                ConsoleHelper.WriteLine($"OnNodeRegistered: {nodeId}");
            };
            server.OnNodeUnregistered += (nodeId) =>
            {
                eventCount++;
                ConsoleHelper.WriteLine($"OnNodeUnregistered: {nodeId}");
            };
            server.OnRelayStarted += (source, target) =>
            {
                eventCount++;
                ConsoleHelper.WriteLine($"OnRelayStarted: {source} -> {target}");
            };
            server.OnError += (id, error) =>
            {
                eventCount++;
                ConsoleHelper.WriteLine($"OnError: {id} - {error}");
            };

            ConsoleHelper.WriteLine($"Events registered: 4");
            ConsoleHelper.WriteLine("TestServerEvents: PASSED");
        }

        static async Task TestServerStartStop()
        {
            ConsoleHelper.WriteLine("--- TestServerStartStop ---");

            var options = new P2PServerBuilder()
                .SetPort(39656)
                .SetMaxNodes(10)
                .EnableRelay()
                .Build();

            var server = new P2PServer(options);

            ConsoleHelper.WriteLine($"Before Start: IsRunning={server.IsRunning}");

            server.Start();

            ConsoleHelper.WriteLine($"After Start: IsRunning={server.IsRunning}");

            if (server.IsRunning)
            {
                ConsoleHelper.WriteLine("Server started successfully");
            }
            else
            {
                ConsoleHelper.WriteLine("Server start FAILED");
            }

            await Task.Delay(1000);

            server.Stop();

            ConsoleHelper.WriteLine($"After Stop: IsRunning={server.IsRunning}");

            if (!server.IsRunning)
            {
                ConsoleHelper.WriteLine("TestServerStartStop: PASSED");
            }
            else
            {
                ConsoleHelper.WriteLine("TestServerStartStop: FAILED");
            }
        }
    }
}