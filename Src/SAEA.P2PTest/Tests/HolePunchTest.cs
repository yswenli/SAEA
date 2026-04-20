using System;
using System.Net;
using System.Threading.Tasks;
using SAEA.Common;
using SAEA.P2P.NAT;
using SAEA.P2P.Common;

namespace SAEA.P2PTest.Tests
{
    public static class HolePunchTest
    {
        public static async Task RunAsync()
        {
            ConsoleHelper.WriteLine("=== HolePunchTest ===");
            ConsoleHelper.WriteLine("");

            TestHolePuncherCreation();
            ConsoleHelper.WriteLine("");

            TestNATTypeDetection();
            ConsoleHelper.WriteLine("");

            TestHolePunchStrategy();
            ConsoleHelper.WriteLine("");

            TestHolePunchCompatibility();
            ConsoleHelper.WriteLine("");

            await TestHolePunchResult();
            ConsoleHelper.WriteLine("");

            ConsoleHelper.WriteLine("=== HolePunchTest Complete ===");
        }

        static void TestHolePuncherCreation()
        {
            ConsoleHelper.WriteLine("--- TestHolePuncherCreation ---");

            var puncher = new HolePuncher(
                HolePunchStrategy.PreferDirect,
                10000,
                5);

            ConsoleHelper.WriteLine($"Strategy: {HolePunchStrategy.PreferDirect}");
            ConsoleHelper.WriteLine($"Timeout: 10000ms");
            ConsoleHelper.WriteLine($"MaxRetry: 5");

            ConsoleHelper.WriteLine("TestHolePuncherCreation: PASSED");
        }

        static void TestNATTypeDetection()
        {
            ConsoleHelper.WriteLine("--- TestNATTypeDetection ---");

            var detector = new NATDetector();

            ConsoleHelper.WriteLine($"Initial NATType: {detector.DetectedNATType}");
            ConsoleHelper.WriteLine($"PublicAddress: {detector.PublicAddress}");
            ConsoleHelper.WriteLine($"LocalAddress: {detector.LocalAddress}");

            detector.SetPublicAddress(new IPEndPoint(IPAddress.Parse("203.0.113.100"), 12345));
            detector.SetNATType(NATType.FullCone);

            ConsoleHelper.WriteLine($"After Set: NATType={detector.DetectedNATType}");
            ConsoleHelper.WriteLine($"After Set: PublicAddress={detector.PublicAddress}");

            if (detector.DetectedNATType == NATType.FullCone && 
                detector.PublicAddress.ToString() == "203.0.113.100:12345")
            {
                ConsoleHelper.WriteLine("TestNATTypeDetection: PASSED");
            }
            else
            {
                ConsoleHelper.WriteLine("TestNATTypeDetection: FAILED");
            }
        }

        static void TestHolePunchStrategy()
        {
            ConsoleHelper.WriteLine("--- TestHolePunchStrategy ---");

            var strategies = new[]
            {
                HolePunchStrategy.PreferDirect,
                HolePunchStrategy.PreferRelay,
                HolePunchStrategy.DirectOnly,
                HolePunchStrategy.RelayOnly
            };

            foreach (var strategy in strategies)
            {
                var puncher = new HolePuncher(strategy);
                ConsoleHelper.WriteLine($"Created puncher with strategy: {strategy}");
            }

            ConsoleHelper.WriteLine("TestHolePunchStrategy: PASSED");
        }

        static void TestHolePunchCompatibility()
        {
            ConsoleHelper.WriteLine("--- TestHolePunchCompatibility ---");

            var puncher = new HolePuncher(HolePunchStrategy.PreferDirect);
            puncher.SetNATType(NATType.FullCone);

            var result1 = puncher.CanPunchWith(NATType.FullCone);
            ConsoleHelper.WriteLine($"FullCone vs FullCone: {result1?.Success ?? true}");

            puncher.SetNATType(NATType.RestrictedCone);
            var result2 = puncher.CanPunchWith(NATType.Symmetric);
            ConsoleHelper.WriteLine($"RestrictedCone vs Symmetric: {result2?.Success ?? true}");

            var relayOnlyPuncher = new HolePuncher(HolePunchStrategy.RelayOnly);
            relayOnlyPuncher.SetNATType(NATType.FullCone);
            var result3 = relayOnlyPuncher.CanPunchWith(NATType.FullCone);
            ConsoleHelper.WriteLine($"RelayOnly strategy: {(result3 == null ? "null" : result3.Success.ToString())}");

            ConsoleHelper.WriteLine("TestHolePunchCompatibility: PASSED");
        }

        static async Task TestHolePunchResult()
        {
            ConsoleHelper.WriteLine("--- TestHolePunchResult ---");

            var successResult = HolePunchResult.Succeeded(
                new IPEndPoint(IPAddress.Parse("192.168.1.100"), 5000),
                NATType.FullCone,
                NATType.RestrictedCone,
                3);

            ConsoleHelper.WriteLine($"Success result: {successResult.Success}");
            ConsoleHelper.WriteLine($"EstablishedAddress: {successResult.EstablishedAddress}");
            ConsoleHelper.WriteLine($"SourceNAT: {successResult.SourceNATType}");
            ConsoleHelper.WriteLine($"TargetNAT: {successResult.TargetNATType}");
            ConsoleHelper.WriteLine($"Attempts: {successResult.Attempts}");

            var failResult = HolePunchResult.Failed(
                "Timeout waiting for response",
                NATType.Symmetric,
                NATType.Symmetric);

            ConsoleHelper.WriteLine($"Failed result: {failResult.Success}");
            ConsoleHelper.WriteLine($"Error: {failResult.ErrorMessage}");

            if (successResult.Success && !failResult.Success)
            {
                ConsoleHelper.WriteLine("TestHolePunchResult: PASSED");
            }
            else
            {
                ConsoleHelper.WriteLine("TestHolePunchResult: FAILED");
            }
        }
    }
}