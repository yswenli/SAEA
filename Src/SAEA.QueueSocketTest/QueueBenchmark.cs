using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Common;
using SAEA.QueueSocket;
using SAEA.QueueSocket.Net;
using SAEA.QueueSocket.Type;

namespace SAEA.QueueSocketTest
{
    public static class QueueBenchmark
    {
        const int MicroN = 20000;
        const int MicroWarmup = 2000;

        static long _microEncode64;
        static long _microEncode1K;
        static long _microEncode4K;
        static long _microDecode64;
        static long _microDecode1K;
        static int _microGen0;
        static bool _enforceThresholds = true;

        public static async Task RunAsync(bool enforceThresholds = true)
        {
            _enforceThresholds = enforceThresholds;
            _microGen0 = 0;
            TestHarness.Section("QueueSocket micro benchmarks");

            MicroEncode("MicroEncode/64B", 64, out _microEncode64);
            MicroEncode("MicroEncode/1KB", 1024, out _microEncode1K);
            MicroEncode("MicroEncode/4KB", 4096, out _microEncode4K);
            MicroDecode("MicroDecode/64B", 64, out _microDecode64);
            MicroDecode("MicroDecode/1KB", 1024, out _microDecode1K);
            MicroDecodeBatch("MicroDecodeBatch/100x64B", 64, 100);

            if (enforceThresholds)
            {
                TestHarness.Expect(_microEncode64 <= 256, "MicroEncode/64B within budget", "B/op=" + _microEncode64);
                TestHarness.Expect(_microEncode1K <= 1280, "MicroEncode/1KB within budget", "B/op=" + _microEncode1K);
                TestHarness.Expect(_microEncode4K <= 4400, "MicroEncode/4KB within budget", "B/op=" + _microEncode4K);
                TestHarness.Expect(_microDecode64 <= 512, "MicroDecode/64B within budget", "B/op=" + _microDecode64);
                TestHarness.Expect(_microDecode1K <= 1600, "MicroDecode/1KB within budget", "B/op=" + _microDecode1K);
                TestHarness.Expect(_microGen0 <= 25, "Micro Gen0 within budget (5 runs x 20k)", "GC0=" + _microGen0);
            }
            else
            {
                ConsoleHelper.WriteLine("[baseline] micro B/op recorded without threshold enforcement");
            }

            TestHarness.Section("QueueSocket end-to-end benchmark");
            await RunScenarioAsync("S1", 1, 1, 64, 10000, 8192);
            await RunScenarioAsync("S2", 5, 5, 1024, 10000, 12288);

            TestHarness.WriteSummary("QueueBenchmark");
        }

        static byte[] BuildPayload(int size)
        {
            var payload = new byte[size];
            for (int i = 0; i < size; i++) payload[i] = (byte)(i % 251);
            return payload;
        }

        static void MicroEncode(string name, int payloadSize, out long bytesPerOp)
        {
            var payload = BuildPayload(payloadSize);
            var msg = new QueueSocketMsg(QueueSocketMsgType.Publish, "producer", "bench", payload);

            for (int i = 0; i < MicroWarmup; i++) QueueCoder.Encode(msg);

            var g0 = GC.CollectionCount(0);
            var before = GC.GetTotalAllocatedBytes(true);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < MicroN; i++) QueueCoder.Encode(msg);
            sw.Stop();
            var bytes = GC.GetTotalAllocatedBytes(true) - before;
            var g0After = GC.CollectionCount(0);

            _microGen0 += (g0After - g0);
            bytesPerOp = bytes / MicroN;
            ConsoleHelper.WriteLine("[micro] " + name + " | " + (sw.Elapsed.TotalMilliseconds * 1000000.0 / MicroN).ToString("F0") + " ns/op | " + bytesPerOp + " B/op | GC0=" + (g0After - g0));
        }

        static void MicroDecode(string name, int payloadSize, out long bytesPerOp)
        {
            var payload = BuildPayload(payloadSize);
            var frame = QueueCoder.Encode(new QueueSocketMsg(QueueSocketMsgType.Data, "producer", "bench", payload));
            var coder = new QueueCoder();

            for (int i = 0; i < MicroWarmup; i++)
            {
                var r = coder.GetQueueResult(frame);
                for (int j = 0; j < r.Count; j++) r[j].Dispose();
                r.Clear();
            }

            var g0 = GC.CollectionCount(0);
            var before = GC.GetTotalAllocatedBytes(true);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < MicroN; i++)
            {
                var r = coder.GetQueueResult(frame);
                for (int j = 0; j < r.Count; j++) r[j].Dispose();
                r.Clear();
            }
            sw.Stop();
            var bytes = GC.GetTotalAllocatedBytes(true) - before;
            var g0After = GC.CollectionCount(0);

            _microGen0 += (g0After - g0);
            bytesPerOp = bytes / MicroN;
            ConsoleHelper.WriteLine("[micro] " + name + " | " + (sw.Elapsed.TotalMilliseconds * 1000000.0 / MicroN).ToString("F0") + " ns/op | " + bytesPerOp + " B/op | GC0=" + (g0After - g0));
        }

        static void MicroDecodeBatch(string name, int payloadSize, int frames)
        {
            var payload = BuildPayload(payloadSize);
            var frame = QueueCoder.Encode(new QueueSocketMsg(QueueSocketMsgType.Data, "producer", "bench", payload));
            var batch = new byte[frame.Length * frames];
            for (int i = 0; i < frames; i++) Buffer.BlockCopy(frame, 0, batch, i * frame.Length, frame.Length);
            var coder = new QueueCoder();

            for (int i = 0; i < MicroWarmup; i++)
            {
                var r = coder.GetQueueResult(batch);
                for (int j = 0; j < r.Count; j++) r[j].Dispose();
                r.Clear();
            }

            var g0 = GC.CollectionCount(0);
            var before = GC.GetTotalAllocatedBytes(true);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < MicroN; i++)
            {
                var r = coder.GetQueueResult(batch);
                for (int j = 0; j < r.Count; j++) r[j].Dispose();
                r.Clear();
            }
            sw.Stop();
            var bytes = GC.GetTotalAllocatedBytes(true) - before;
            var g0After = GC.CollectionCount(0);

            var bytesPerFrame = bytes / MicroN / frames;
            ConsoleHelper.WriteLine("[micro] " + name + " | " + (sw.Elapsed.TotalMilliseconds * 1000000.0 / MicroN / frames).ToString("F0") + " ns/frame | " + bytesPerFrame + " B/frame | GC0=" + (g0After - g0));
        }

        static async Task RunScenarioAsync(string name, int producerCount, int consumerCount, int payloadSize, int durationMs, int budgetBytesPerFrame)
        {
            const int maxOutstanding = 8192;

            var topic = QueueServerHarness.NewTopic("bench-" + name);

            var chars = new char[payloadSize];
            for (int i = 0; i < payloadSize; i++) chars[i] = (char)('a' + (i % 26));
            var content = new string(chars);

            var counts = new int[consumerCount];
            var consumers = new List<Consumer>();
            for (int i = 0; i < consumerCount; i++)
            {
                int idx = i;
                var c = QueueServerHarness.CreateConsumer(topic);
                c.OnMessage += obj => { try { Interlocked.Increment(ref counts[idx]); } finally { obj.Dispose(); } };
                consumers.Add(c);
            }

            await Task.Delay(400);

            long published = 0;
            var producers = new List<Producer>();
            for (int i = 0; i < producerCount; i++)
            {
                producers.Add(QueueServerHarness.CreateProducer());
            }

            var stop = false;

            var gcBefore = GC.GetTotalAllocatedBytes(true);
            var g0 = GC.CollectionCount(0);
            var sw = Stopwatch.StartNew();

            var tasks = new List<Task>();
            for (int i = 0; i < producerCount; i++)
            {
                var producer = producers[i];
                tasks.Add(Task.Run(async () =>
                {
                    while (!Volatile.Read(ref stop))
                    {
                        if (Interlocked.Read(ref published) - MinCounts(counts) >= maxOutstanding)
                        {
                            await Task.Delay(1);
                            continue;
                        }
                        producer.Publish(topic, content);
                        Interlocked.Increment(ref published);
                    }
                }));
            }

            await Task.Delay(durationMs);
            Volatile.Write(ref stop, true);
            await Task.WhenAll(tasks);
            var publishElapsed = sw.Elapsed;

            var publishedFinal = Interlocked.Read(ref published);
            await TestHarness.WaitUntil(() => MinCounts(counts) >= publishedFinal, 30000, 50);

            sw.Stop();
            var delivered = MinCounts(counts);
            var totalDeliveries = TotalCounts(counts);
            var gcAfter = GC.GetTotalAllocatedBytes(true);
            var g0After = GC.CollectionCount(0);

            var elapsedSec = publishElapsed.TotalSeconds;
            var throughput = elapsedSec > 0 ? (long)(publishedFinal / elapsedSec) : 0;
            var bytesPerFrame = totalDeliveries > 0 ? (gcAfter - gcBefore) / totalDeliveries : 0;

            ConsoleHelper.WriteLine("[e2e] " + name + " | published=" + publishedFinal + " delivered=" + delivered + " deliveries=" + totalDeliveries + " | " + throughput + " msg/s | " + bytesPerFrame + " B/frame | GC0=" + (g0After - g0));

            TestHarness.Expect(delivered >= publishedFinal, name + " every consumer received all flushed messages", "published=" + publishedFinal + " delivered=" + delivered + " counts=" + string.Join(",", counts));
            TestHarness.Expect(totalDeliveries >= publishedFinal, name + " no message loss", "published=" + publishedFinal + " deliveries=" + totalDeliveries);
            if (_enforceThresholds)
            {
                TestHarness.Expect(throughput >= 20000, name + " throughput >= 20000 msg/s", "throughput=" + throughput);
                TestHarness.Expect(bytesPerFrame <= budgetBytesPerFrame, name + " bytes/frame within budget", "bytes/frame=" + bytesPerFrame + " budget=" + budgetBytesPerFrame);
            }

            foreach (var p in producers) p.Dispose();
            foreach (var c in consumers) c.Dispose();
        }

        static int MinCounts(int[] counts)
        {
            var min = int.MaxValue;
            for (int i = 0; i < counts.Length; i++)
            {
                var v = Volatile.Read(ref counts[i]);
                if (v < min) min = v;
            }
            return min == int.MaxValue ? 0 : min;
        }

        static long TotalCounts(int[] counts)
        {
            long total = 0;
            for (int i = 0; i < counts.Length; i++) total += Volatile.Read(ref counts[i]);
            return total;
        }
    }
}