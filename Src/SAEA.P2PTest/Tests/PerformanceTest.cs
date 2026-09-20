using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using SAEA.Common;
using SAEA.P2P.Protocol;
using SAEA.P2P.Relay;
using SAEA.P2P.Security;

namespace SAEA.P2PTest.Tests
{
    /// <summary>
    /// 性能测试：协议编解码、大载荷、加解密、中继编码吞吐。阈值宽松以避免抖动。
    /// </summary>
    public static class PerformanceTest
    {
        public static void Run()
        {
            TestHarness.Section("PerformanceTest");

            ProtocolThroughput();
            LargePayloadThroughput();
            CryptoThroughput();
            RelayEncodeThroughput();

            TestHarness.WriteSummary("PerformanceTest");
        }

        static void ProtocolThroughput()
        {
            TestHarness.Section("protocol decode throughput");

            var coder = new P2PCoder();
            var frame = coder.EncodeP2P(P2PMessageType.UserData, "perf-payload");
            const int iterations = 50000;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                coder.DecodeP2P(frame);
            }
            sw.Stop();

            double ops = iterations / sw.Elapsed.TotalSeconds;
            ConsoleHelper.WriteLine($"Protocol decode: {ops:F0} ops/sec ({sw.ElapsedMilliseconds} ms)");
            TestHarness.Expect(ops > 2000, "protocol throughput above floor", $"{ops:F0} ops/sec");
        }

        static void LargePayloadThroughput()
        {
            TestHarness.Section("1MB payload throughput");

            var coder = new P2PCoder();
            var content = new byte[1024 * 1024];
            new Random(1).NextBytes(content);

            var sw = Stopwatch.StartNew();
            var frame = coder.EncodeP2P(P2PMessageType.UserData, content);
            var decoded = coder.DecodeP2P(frame);
            sw.Stop();

            TestHarness.Expect(decoded.Count == 1 && decoded[0].Content != null && decoded[0].Content.SequenceEqual(content),
                "1MB round trip integrity");
            ConsoleHelper.WriteLine($"1MB round trip: {sw.ElapsedMilliseconds} ms");
            TestHarness.Expect(sw.ElapsedMilliseconds < 10000, "1MB round trip under 10s", $"{sw.ElapsedMilliseconds}ms");
        }

        static void CryptoThroughput()
        {
            TestHarness.Section("crypto throughput");

            var crypto = new CryptoService("0123456789abcdef");
            var data = Encoding.UTF8.GetBytes("performance-payload");
            const int iterations = 5000;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                crypto.Decrypt(crypto.Encrypt(data));
            }
            sw.Stop();

            double ops = iterations / sw.Elapsed.TotalSeconds;
            ConsoleHelper.WriteLine($"Crypto encrypt+decrypt: {ops:F0} ops/sec ({sw.ElapsedMilliseconds} ms)");
            TestHarness.Expect(ops > 200, "crypto throughput above floor", $"{ops:F0} ops/sec");
        }

        static void RelayEncodeThroughput()
        {
            TestHarness.Section("relay encode throughput");

            var manager = new RelayManager(defaultQuota: 0);
            var session = manager.CreateSession("a", "b");
            var payload = new byte[256];
            const int iterations = 20000;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                manager.EncodeRelayData(session.SessionId, "a", "b", payload);
            }
            sw.Stop();

            double ops = iterations / sw.Elapsed.TotalSeconds;
            ConsoleHelper.WriteLine($"Relay encode: {ops:F0} ops/sec ({sw.ElapsedMilliseconds} ms)");
            TestHarness.Expect(sw.ElapsedMilliseconds < 15000, "relay encode throughput acceptable", $"{sw.ElapsedMilliseconds}ms");
        }
    }
}