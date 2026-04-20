using System;

using SAEA.Common;
using SAEA.P2P.Builder;
using SAEA.P2P.NAT;

namespace SAEA.P2PTest.Tests
{
    public static class QuickStartTest
    {
        public static void Run()
        {
            ConsoleHelper.WriteLine("=== QuickStartTest ===");
            ConsoleHelper.WriteLine("");

            DemoClientBuilder();
            ConsoleHelper.WriteLine("");

            DemoServerBuilder();
            ConsoleHelper.WriteLine("");

            ConsoleHelper.WriteLine("=== QuickStartTest Complete ===");
        }

        static void DemoClientBuilder()
        {
            ConsoleHelper.WriteLine("--- DemoClientBuilder ---");

            var options = new P2PClientBuilder()
                .SetServer("p2p.example.com", 39654)
                .SetNodeId("client-001")
                .EnableHolePunch()
                .EnableRelay()
                .EnableLocalDiscovery()
                .Build();

            ConsoleHelper.WriteLine("Client options configured:");
            ConsoleHelper.WriteLine($"  Server: {options.ServerAddress}:{options.ServerPort}");
            ConsoleHelper.WriteLine($"  NodeId: {options.NodeId}");
            ConsoleHelper.WriteLine($"  HolePunch: {options.HolePunch.Enabled}");
            ConsoleHelper.WriteLine($"  Relay: {options.Relay.Enabled}");
            ConsoleHelper.WriteLine($"  LocalDiscovery: {options.Discovery.EnableLocalDiscovery}");

            ConsoleHelper.WriteLine("DemoClientBuilder: PASSED");
        }

        static void DemoServerBuilder()
        {
            ConsoleHelper.WriteLine("--- DemoServerBuilder ---");

            var options = new P2PServerBuilder()
                .SetPort(39654)
                .SetIP("0.0.0.0")
                .EnableRelay()
                .EnableLogging()
                .Build();

            ConsoleHelper.WriteLine("Server options configured:");
            ConsoleHelper.WriteLine($"  Bind: {options.BindIP}:{options.Port}");
            ConsoleHelper.WriteLine($"  MaxNodes: {options.MaxNodes}");
            ConsoleHelper.WriteLine($"  Relay: {options.Relay.Enabled}");
            ConsoleHelper.WriteLine($"  Logging: {options.Logging.Enabled}");

            ConsoleHelper.WriteLine("DemoServerBuilder: PASSED");
        }
    }
}