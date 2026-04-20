using System;
using System.Net;

using SAEA.Common;
using SAEA.P2P.Builder;
using SAEA.P2P.Common;
using SAEA.P2P.NAT;

namespace SAEA.P2PTest.Tests
{
    public static class BuilderTest
    {
        public static void Run()
        {
            ConsoleHelper.WriteLine("=== BuilderTest ===");
            ConsoleHelper.WriteLine("");

            TestClientBuilderBasic();
            ConsoleHelper.WriteLine("");

            TestClientBuilderFull();
            ConsoleHelper.WriteLine("");

            TestClientBuilderValidation();
            ConsoleHelper.WriteLine("");

            TestServerBuilder();
            ConsoleHelper.WriteLine("");

            ConsoleHelper.WriteLine("=== BuilderTest Complete ===");
        }

        static void TestClientBuilderBasic()
        {
            ConsoleHelper.WriteLine("--- TestClientBuilderBasic ---");

            var builder = new P2PClientBuilder();
            var options = builder
                .SetServer("192.168.1.100", 39654)
                .SetNodeId("test-node-001")
                .Build();

            ConsoleHelper.WriteLine($"ServerAddress: {options.ServerAddress}");
            ConsoleHelper.WriteLine($"ServerPort: {options.ServerPort}");
            ConsoleHelper.WriteLine($"NodeId: {options.NodeId}");

            if (options.ServerAddress == "192.168.1.100" &&
                options.ServerPort == 39654 &&
                options.NodeId == "test-node-001")
            {
                ConsoleHelper.WriteLine("TestClientBuilderBasic: PASSED");
            }
            else
            {
                ConsoleHelper.WriteLine("TestClientBuilderBasic: FAILED");
            }
        }

        static void TestClientBuilderFull()
        {
            ConsoleHelper.WriteLine("--- TestClientBuilderFull ---");

            var endpoint = new IPEndPoint(IPAddress.Parse("10.0.0.1"), 40000);
            var options = new P2PClientBuilder()
                .SetServer(endpoint)
                .SetNodeId("full-test-node")
                .SetNodeIdPassword("secret123")
                .SetTimeout(15000)
                .EnableHolePunch(HolePunchStrategy.PreferDirect)
                .SetHolePunchTimeout(8000)
                .SetHolePunchRetry(10)
                .SetNATType(NATType.FullCone)
                .EnableRelay(30000, 1000000)
                .EnableLocalDiscovery(39655, "239.255.255.250")
                .SetDiscoveryInterval(3000)
                .EnableEncryption("1234567890123456")
                .EnableTls()
                .SetFreeTime(120000)
                .SetPeerHeartbeat(20000)
                .EnableLogging(2)
                .SetLogToConsole(true)
                .Build();

            ConsoleHelper.WriteLine($"ServerAddress: {options.ServerAddress}");
            ConsoleHelper.WriteLine($"ServerPort: {options.ServerPort}");
            ConsoleHelper.WriteLine($"NodeId: {options.NodeId}");
            ConsoleHelper.WriteLine($"HolePunch.Enabled: {options.HolePunch.Enabled}");
            ConsoleHelper.WriteLine($"HolePunch.Strategy: {options.HolePunch.Strategy}");
            ConsoleHelper.WriteLine($"HolePunch.NATType: {options.HolePunch.NATType}");
            ConsoleHelper.WriteLine($"Relay.Enabled: {options.Relay.Enabled}");
            ConsoleHelper.WriteLine($"Discovery.EnableLocalDiscovery: {options.Discovery.EnableLocalDiscovery}");
            ConsoleHelper.WriteLine($"Encryption.Enabled: {options.Encryption.Enabled}");
            ConsoleHelper.WriteLine($"Encryption.TlsEnabled: {options.Encryption.TlsEnabled}");
            ConsoleHelper.WriteLine($"Logging.Enabled: {options.Logging.Enabled}");

            if (options.ServerAddress == "10.0.0.1" &&
                options.ServerPort == 40000 &&
                options.NodeId == "full-test-node" &&
                options.HolePunch.Enabled &&
                options.HolePunch.Strategy == HolePunchStrategy.PreferDirect &&
                options.HolePunch.NATType == NATType.FullCone &&
                options.Relay.Enabled &&
                options.Discovery.EnableLocalDiscovery &&
                options.Encryption.Enabled &&
                options.Encryption.TlsEnabled &&
                options.Logging.Enabled)
            {
                ConsoleHelper.WriteLine("TestClientBuilderFull: PASSED");
            }
            else
            {
                ConsoleHelper.WriteLine("TestClientBuilderFull: FAILED");
            }
        }

        static void TestClientBuilderValidation()
        {
            ConsoleHelper.WriteLine("--- TestClientBuilderValidation ---");

            int passed = 0;
            int total = 3;

            try
            {
                new P2PClientBuilder()
                    .SetServer("127.0.0.1", 39654)
                    .Build();
                ConsoleHelper.WriteLine("EP02 validation: FAILED (should throw)");
            }
            catch (P2PException ex)
            {
                if (ex.ErrorCode == ErrorCode.RegisterPeerIdMissing)
                {
                    ConsoleHelper.WriteLine("EP02 (NodeId missing) validation: PASSED");
                    passed++;
                }
                else
                {
                    ConsoleHelper.WriteLine($"EP02 validation: FAILED (wrong error code: {ex.ErrorCode})");
                }
            }

            try
            {
                new P2PClientBuilder()
                    .SetServer("127.0.0.1", 39654)
                    .SetNodeId(new string('a', 100))
                    .Build();
                ConsoleHelper.WriteLine("EP02 (NodeId too long) validation: FAILED (should throw)");
            }
            catch (P2PException ex)
            {
                if (ex.ErrorCode == ErrorCode.RegisterPeerIdInvalid)
                {
                    ConsoleHelper.WriteLine("EP02 (NodeId invalid) validation: PASSED");
                    passed++;
                }
                else
                {
                    ConsoleHelper.WriteLine($"EP02 validation: FAILED (wrong error code: {ex.ErrorCode})");
                }
            }

            try
            {
                new P2PClientBuilder()
                    .SetNodeId("test-node")
                    .Build();
                ConsoleHelper.WriteLine("EP07 validation: FAILED (should throw)");
            }
            catch (P2PException ex)
            {
                if (ex.ErrorCode == ErrorCode.RegisterServerUnavailable)
                {
                    ConsoleHelper.WriteLine("EP07 (Server unavailable) validation: PASSED");
                    passed++;
                }
                else
                {
                    ConsoleHelper.WriteLine($"EP07 validation: FAILED (wrong error code: {ex.ErrorCode})");
                }
            }

            ConsoleHelper.WriteLine($"TestClientBuilderValidation: {passed}/{total} PASSED");
        }

        static void TestServerBuilder()
        {
            ConsoleHelper.WriteLine("--- TestServerBuilder ---");

            var options = new P2PServerBuilder()
                .SetPort(39654)
                .SetIP("0.0.0.0")
                .SetMaxNodes(5000)
                .EnableRelay(1000, 10000000)
                .SetFreeTime(90000)
                .EnableLogging()
                .Build();

            ConsoleHelper.WriteLine($"BindIP: {options.BindIP}");
            ConsoleHelper.WriteLine($"Port: {options.Port}");
            ConsoleHelper.WriteLine($"MaxNodes: {options.MaxNodes}");
            ConsoleHelper.WriteLine($"Relay.Enabled: {options.Relay.Enabled}");
            ConsoleHelper.WriteLine($"Relay.MaxRelaySessions: {options.Relay.MaxRelaySessions}");
            ConsoleHelper.WriteLine($"Logging.Enabled: {options.Logging.Enabled}");

            if (options.BindIP == "0.0.0.0" &&
                options.Port == 39654 &&
                options.MaxNodes == 5000 &&
                options.Relay.Enabled &&
                options.Relay.MaxRelaySessions == 1000 &&
                options.Logging.Enabled)
            {
                ConsoleHelper.WriteLine("TestServerBuilder: PASSED");
            }
            else
            {
                ConsoleHelper.WriteLine("TestServerBuilder: FAILED");
            }
        }
    }
}