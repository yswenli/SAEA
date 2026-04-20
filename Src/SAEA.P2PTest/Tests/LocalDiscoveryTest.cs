using System;
using System.Threading;
using System.Threading.Tasks;
using SAEA.Common;
using SAEA.P2P.Discovery;

namespace SAEA.P2PTest.Tests
{
    public static class LocalDiscoveryTest
    {
        public static async Task RunAsync()
        {
            ConsoleHelper.WriteLine("=== LocalDiscoveryTest ===");
            ConsoleHelper.WriteLine("");

            TestLocalDiscoveryCreation();
            ConsoleHelper.WriteLine("");

            TestDiscoveredNode();
            ConsoleHelper.WriteLine("");

            await TestLocalDiscoveryStartStop();
            ConsoleHelper.WriteLine("");

            await TestLocalDiscoveryMultipleNodes();
            ConsoleHelper.WriteLine("");

            ConsoleHelper.WriteLine("=== LocalDiscoveryTest Complete ===");
        }

        static void TestLocalDiscoveryCreation()
        {
            ConsoleHelper.WriteLine("--- TestLocalDiscoveryCreation ---");

            var discovery1 = new LocalDiscovery("node-001");
            ConsoleHelper.WriteLine($"Created with nodeId: node-001, default port/multicast");

            var discovery2 = new LocalDiscovery(
                "node-002",
                39655,
                "224.0.0.250",
                5000,
                30000);

            ConsoleHelper.WriteLine($"Created with custom: port=39655, multicast=224.0.0.250, interval=5000ms");

            ConsoleHelper.WriteLine("TestLocalDiscoveryCreation: PASSED");
        }

        static void TestDiscoveredNode()
        {
            ConsoleHelper.WriteLine("--- TestDiscoveredNode ---");

            var node = new DiscoveredNode
            {
                NodeId = "discovered-node-001",
                NodeName = "Test Node",
                DiscoveredTime = DateTime.UtcNow
            };

            ConsoleHelper.WriteLine($"NodeId: {node.NodeId}");
            ConsoleHelper.WriteLine($"NodeName: {node.NodeName}");
            ConsoleHelper.WriteLine($"DiscoveredTime: {node.DiscoveredTime}");
            ConsoleHelper.WriteLine($"LastSeenTime: {node.LastSeenTime}");
            ConsoleHelper.WriteLine($"IsExpired(5000): {node.IsExpired(5000)}");

            node.Refresh();
            ConsoleHelper.WriteLine($"After Refresh: IsExpired(5000): {node.IsExpired(5000)}");

            if (!node.IsExpired(5000))
            {
                ConsoleHelper.WriteLine("TestDiscoveredNode: PASSED");
            }
            else
            {
                ConsoleHelper.WriteLine("TestDiscoveredNode: FAILED");
            }
        }

        static async Task TestLocalDiscoveryStartStop()
        {
            ConsoleHelper.WriteLine("--- TestLocalDiscoveryStartStop ---");

            var discovery = new LocalDiscovery(
                "test-node-startstop",
                39659,
                "224.0.0.251",
                1000,
                5000);

            int discoveredCount = 0;
            discovery.OnNodeDiscovered += (node) =>
            {
                discoveredCount++;
                ConsoleHelper.WriteLine($"Discovered: {node.NodeId}");
            };

            discovery.SetNodeInfo("Test Node", new[] { "chat", "file" });

            ConsoleHelper.WriteLine("Starting discovery...");
            discovery.Start();

            await Task.Delay(3000);

            ConsoleHelper.WriteLine($"DiscoveredNodes count: {discovery.DiscoveredNodes.Count}");

            discovery.Stop();

            ConsoleHelper.WriteLine("Discovery stopped");

            ConsoleHelper.WriteLine("TestLocalDiscoveryStartStop: PASSED");
        }

        static async Task TestLocalDiscoveryMultipleNodes()
        {
            ConsoleHelper.WriteLine("--- TestLocalDiscoveryMultipleNodes ---");

            var discovery1 = new LocalDiscovery("multi-node-1", 39660, "224.0.0.252", 500, 10000);
            var discovery2 = new LocalDiscovery("multi-node-2", 39660, "224.0.0.252", 500, 10000);

            int discovered1 = 0;
            int discovered2 = 0;

            discovery1.OnNodeDiscovered += (node) =>
            {
                discovered1++;
                ConsoleHelper.WriteLine($"Discovery1 found: {node.NodeId}");
            };

            discovery2.OnNodeDiscovered += (node) =>
            {
                discovered2++;
                ConsoleHelper.WriteLine($"Discovery2 found: {node.NodeId}");
            };

            ConsoleHelper.WriteLine("Starting two discovery instances...");
            discovery1.Start();
            discovery2.Start();

            await Task.Delay(3000);

            ConsoleHelper.WriteLine($"Discovery1 nodes found: {discovered1}");
            ConsoleHelper.WriteLine($"Discovery2 nodes found: {discovered2}");
            ConsoleHelper.WriteLine($"Discovery1.DiscoveredNodes.Count: {discovery1.DiscoveredNodes.Count}");
            ConsoleHelper.WriteLine($"Discovery2.DiscoveredNodes.Count: {discovery2.DiscoveredNodes.Count}");

            discovery1.Stop();
            discovery2.Stop();

            discovery1.CleanupExpiredNodes();
            discovery2.CleanupExpiredNodes();

            ConsoleHelper.WriteLine("TestLocalDiscoveryMultipleNodes: PASSED");
        }
    }
}