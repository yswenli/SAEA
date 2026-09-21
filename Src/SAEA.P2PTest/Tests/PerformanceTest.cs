using System;
using System.Buffers;
using System.Diagnostics;
using System.Linq;
using System.Text;
using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.P2P.Protocol;
using SAEA.P2P.Relay;
using SAEA.P2P.Security;
using SAEA.Sockets.Base;
using SAEA.Sockets.Model;

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
            StreamingDecodeBenchmark();

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
                using (coder.DecodeP2P(frame)) { }
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
            using (var decoded = coder.DecodeP2P(frame))
            {
                sw.Stop();

                TestHarness.Expect(decoded.Count == 1 && decoded[0].Content.Span.SequenceEqual(content),
                    "1MB round trip integrity");
            }
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

        static void StreamingDecodeBenchmark()
        {
            TestHarness.Section("streaming zero-copy decode");

            foreach (var size in new[] { 64, 4 * 1024, 1024 * 1024 })
            {
                var content = new byte[size];
                new Random(7).NextBytes(content);
                byte[] frame;
                using (var w = new PooledBufferWriter(64))
                {
                    new BaseSocketProtocal(size, (byte)SocketProtocalType.RequestSend, content).WriteTo(w);
                    frame = w.WrittenSpan.ToArray();
                }

                int iterations = size >= 1024 * 1024 ? 100 : 20000;

                var legacyOps = MeasureOps(iterations, () => LegacyDecoder.Decode(frame), out var legacyAlloc);

                // 每次迭代同一帧都会被完整消费，复用 coder 以隔离拆帧内核的开销（不把构造 allocation 计入）。
                var pooledCoder = new BaseCoder();
                var newOps = MeasureOps(iterations, () => { using (pooledCoder.Decode(new ReadOnlySequence<byte>(frame))) { } }, out var newAlloc);

                var streamingCoder = new BaseCoder();
                var handler = new CountingHandler();
                var streamOps = MeasureOps(iterations, () => streamingCoder.DecodeStream(frame, handler), out var streamAlloc);

                ConsoleHelper.WriteLine($"[{size,7}B] legacy {legacyOps:F0} ops/s ({legacyAlloc} B/op) | " +
                    $"new {newOps:F0} ops/s ({newAlloc} B/op) | stream {streamOps:F0} ops/s ({streamAlloc} B/op)");

                TestHarness.Expect(handler.TotalBytes > 0, $"stream benchmark decoded {size}B payload");
            }
        }

        static double MeasureOps(int iterations, Action action, out long bytesPerOp)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++) action();
            sw.Stop();
            long delta = GC.GetAllocatedBytesForCurrentThread() - before;
            bytesPerOp = delta / iterations;
            return iterations / sw.Elapsed.TotalSeconds;
        }

        sealed class CountingHandler : SAEA.Sockets.Interface.IFrameHandler
        {
            public long TotalBytes;

            public void OnFrame(in SocketFrame frame)
            {
                TotalBytes += frame.Content.Length;
            }
        }
    }
}