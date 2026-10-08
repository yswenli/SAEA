using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Common.Caching;
using SAEA.QueueSocket;
using SAEA.QueueSocket.Model;
using SAEA.QueueSocket.Net;
using SAEA.QueueSocket.Type;

namespace SAEA.QueueSocketTest
{
    public static class FunctionalTests
    {
        const int SettleMs = 400;

        public static async Task RunAllAsync()
        {
            TestHarness.Section("QueueSocket functional regression");
            await SafeAsync("FT1", FT1_SingleProducerSingleConsumerLossless);
            await SafeAsync("FT2", FT2_Broadcast);
            await SafeAsync("FT3", FT3_MultiTopicIsolation);
            await SafeAsync("FT4", FT4_UnsubscribeTakesEffect);
            await SafeAsync("FT5", FT5_LargeAndFramedMessages);
            await SafeAsync("FT6", FT6_BurstLossless);
            await SafeAsync("FT7", FT7_DisconnectCleanup);
            await SafeAsync("FT8", FT8_ReconnectUsable);
            await SafeAsync("FT9", () => { FT9_EncodeDecodeRoundtrip(); return Task.CompletedTask; });
            await SafeAsync("FT10", FT10_DisconnectCleansSubscription);
            await SafeAsync("FT11", () => { FT11_BatchEncodeMatchesPerFrame(); return Task.CompletedTask; });
            await SafeAsync("FT12", () => { FT12_BatcherCapacityBalanced(); return Task.CompletedTask; });
            await SafeAsync("FT13", () => { FT13_BatcherConcatenationExact(); return Task.CompletedTask; });
            await SafeAsync("FT-Pool-Batch", () => { FT_PoolBatchMergeOwnershipConservation(); return Task.CompletedTask; });
            await SafeAsync("FT-Pool-1", FT_Pool1_OwnershipTransferAsync);
            await SafeAsync("FT-Pool-2", FT_Pool2_PoolConservationAsync);
            await SafeAsync("FT-Pool-3", FT_Pool3_BoundedQueueOverflowAsync);
            await SafeAsync("FT-Pool-4", FT_Pool4_DispatchMergedWriterAsync);
            await SafeAsync("FT-Pool-5", FT_Pool5_ClientBatchMergeAsync);
            await SafeAsync("FT-Pool-6", FT_Pool6_BatcherClearDrainsAsync);
            await SafeAsync("FT-Pool-7", FT_Pool7_LargeWriterPooledAsync);
        }

        static async Task SafeAsync(string name, Func<Task> test)
        {
            try
            {
                await test();
            }
            catch (Exception ex)
            {
                TestHarness.Expect(false, name + " no unhandled exception", ex.Message);
            }
        }

        static async Task FT1_SingleProducerSingleConsumerLossless()
        {
            var topic = QueueServerHarness.NewTopic("ft1");
            int n = 500;

            var received = new ConcurrentDictionary<string, byte>();
            var consumer = QueueServerHarness.CreateConsumer(topic);
            consumer.OnMessage += obj =>
            {
                try { received.TryAdd(Encoding.UTF8.GetString(obj.Data.Span), 0); }
                finally { obj.Dispose(); }
            };

            await Task.Delay(SettleMs);

            var producer = QueueServerHarness.CreateProducer();
            long sent = 0;
            producer.OnMessagesSent += c => Interlocked.Add(ref sent, c);

            try
            {
                for (int i = 0; i < n; i++) producer.Publish(topic, "ft1-" + i);

                TestHarness.Expect(await TestHarness.WaitUntil(() => Interlocked.Read(ref sent) >= n, 30000), "FT1 producer flushed all", "sent=" + Interlocked.Read(ref sent));
                TestHarness.Expect(await TestHarness.WaitUntil(() => received.Count >= n, 15000), "FT1 consumer received all", "received=" + received.Count);
                TestHarness.Expect(received.Count == n, "FT1 no duplicates and no loss", "count=" + received.Count);
            }
            finally
            {
                producer.Dispose();
                consumer.Dispose();
            }
        }

        static async Task FT2_Broadcast()
        {
            var topic = QueueServerHarness.NewTopic("ft2");
            int n = 300;
            int consumers = 3;

            var counts = new int[consumers];
            var list = new List<Consumer>();
            for (int i = 0; i < consumers; i++)
            {
                int idx = i;
                var c = QueueServerHarness.CreateConsumer(topic);
                c.OnMessage += obj =>
                {
                    try { Interlocked.Increment(ref counts[idx]); }
                    finally { obj.Dispose(); }
                };
                list.Add(c);
            }

            await Task.Delay(SettleMs);

            var producer = QueueServerHarness.CreateProducer();
            long sent = 0;
            producer.OnMessagesSent += c => Interlocked.Add(ref sent, c);

            try
            {
                for (int i = 0; i < n; i++) producer.Publish(topic, "ft2-" + i);

                TestHarness.Expect(await TestHarness.WaitUntil(() => Interlocked.Read(ref sent) >= n, 30000), "FT2 producer flushed all");
                TestHarness.Expect(await TestHarness.WaitUntil(() => counts[0] >= n && counts[1] >= n && counts[2] >= n, 15000), "FT2 all consumers received all", counts[0] + "/" + counts[1] + "/" + counts[2]);
            }
            finally
            {
                producer.Dispose();
                foreach (var c in list) c.Dispose();
            }
        }

        static async Task FT3_MultiTopicIsolation()
        {
            var t1 = QueueServerHarness.NewTopic("ft3a");
            var t2 = QueueServerHarness.NewTopic("ft3b");
            int n = 200;

            long c1 = 0, c2 = 0;
            var ca = QueueServerHarness.CreateConsumer(t1);
            ca.OnMessage += obj => { try { Interlocked.Increment(ref c1); } finally { obj.Dispose(); } };
            var cb = QueueServerHarness.CreateConsumer(t2);
            cb.OnMessage += obj => { try { Interlocked.Increment(ref c2); } finally { obj.Dispose(); } };

            await Task.Delay(SettleMs);

            var producer = QueueServerHarness.CreateProducer();
            long sent = 0;
            producer.OnMessagesSent += c => Interlocked.Add(ref sent, c);

            try
            {
                for (int i = 0; i < n; i++) producer.Publish(t1, "a-" + i);
                for (int i = 0; i < n; i++) producer.Publish(t2, "b-" + i);

                TestHarness.Expect(await TestHarness.WaitUntil(() => c1 >= n && c2 >= n, 15000), "FT3 both topics delivered", "c1=" + c1 + " c2=" + c2);
                TestHarness.Expect(Interlocked.Read(ref c1) == n && Interlocked.Read(ref c2) == n, "FT3 no cross-topic leakage", "c1=" + c1 + " c2=" + c2);
            }
            finally
            {
                producer.Dispose();
                ca.Dispose();
                cb.Dispose();
            }
        }

        static async Task FT4_UnsubscribeTakesEffect()
        {
            var topic = QueueServerHarness.NewTopic("ft4");
            int warmup = 50;

            long received = 0;
            var client = QueueServerHarness.CreateSubscriber(topic);
            client.OnMessage += obj => { try { Interlocked.Increment(ref received); } finally { obj.Dispose(); } };

            await Task.Delay(SettleMs);

            var producer = QueueServerHarness.CreateProducer();
            long sent = 0;
            producer.OnMessagesSent += c => Interlocked.Add(ref sent, c);

            try
            {
                for (int i = 0; i < warmup; i++) producer.Publish(topic, "warm-" + i);
                TestHarness.Expect(await TestHarness.WaitUntil(() => Interlocked.Read(ref received) >= warmup, 15000), "FT4 warmup delivered", "received=" + received);

                client.Unsubscribe(topic);
                await Task.Delay(500);
                var before = Interlocked.Read(ref received);

                for (int i = 0; i < warmup; i++) producer.Publish(topic, "after-" + i);
                await Task.Delay(800);

                var after = Interlocked.Read(ref received);
                TestHarness.Expect(after == before, "FT4 no messages after unsubscribe", "before=" + before + " after=" + after);
            }
            finally
            {
                producer.Dispose();
                client.Dispose();
            }
        }

        static async Task FT5_LargeAndFramedMessages()
        {
            int[] sizes = { 1, 64, 4096, 65536 };

            foreach (var size in sizes)
            {
                var topic = QueueServerHarness.NewTopic("ft5");
                var payload = new byte[size];
                for (int i = 0; i < size; i++) payload[i] = (byte)(i % 128);

                bool delivered = false;
                bool identical = false;
                int gotLen = -1;
                var consumer = QueueServerHarness.CreateConsumer(topic);
                consumer.OnMessage += obj =>
                {
                    try
                    {
                        var d = obj.Data;
                        gotLen = d.Length;
                        identical = d.Length == size;
                        if (identical)
                        {
                            for (int i = 0; i < size; i++)
                            {
                                if (d.Span[i] != payload[i]) { identical = false; break; }
                            }
                        }
                        delivered = true;
                    }
                    finally { obj.Dispose(); }
                };

                await Task.Delay(SettleMs);

                var producer = QueueServerHarness.CreateProducer();
                long sent = 0;
                producer.OnMessagesSent += c => Interlocked.Add(ref sent, c);

                try
                {
                    producer.Publish(topic, Encoding.UTF8.GetString(payload));

                    TestHarness.Expect(await TestHarness.WaitUntil(() => delivered, 15000), "FT5 size=" + size + " received");
                    TestHarness.Expect(identical, "FT5 size=" + size + " bytes identical", "len=" + gotLen);
                }
                finally
                {
                    producer.Dispose();
                    consumer.Dispose();
                }
            }
        }

        static async Task FT6_BurstLossless()
        {
            var topic = QueueServerHarness.NewTopic("ft6");
            int producers = 3;
            int perProducer = 100;
            int consumers = 2;
            int total = producers * perProducer;

            var counts = new int[consumers];
            var list = new List<Consumer>();
            for (int i = 0; i < consumers; i++)
            {
                int idx = i;
                var c = QueueServerHarness.CreateConsumer(topic);
                c.OnMessage += obj => { try { Interlocked.Increment(ref counts[idx]); } finally { obj.Dispose(); } };
                list.Add(c);
            }

            await Task.Delay(SettleMs);

            var plist = new List<Producer>();
            long sent = 0;
            for (int p = 0; p < producers; p++)
            {
                var prod = QueueServerHarness.CreateProducer();
                prod.OnMessagesSent += c => Interlocked.Add(ref sent, c);
                plist.Add(prod);
            }

            try
            {
                var tasks = new List<Task>();
                for (int p = 0; p < producers; p++)
                {
                    int idx = p;
                    tasks.Add(Task.Run(() =>
                    {
                        for (int i = 0; i < perProducer; i++) plist[idx].Publish(topic, "ft6-" + idx + "-" + i);
                    }));
                }
                await Task.WhenAll(tasks);

                TestHarness.Expect(await TestHarness.WaitUntil(() => Interlocked.Read(ref sent) >= total, 30000), "FT6 producer flushed all", "sent=" + sent);
                TestHarness.Expect(await TestHarness.WaitUntil(() => counts[0] >= total && counts[1] >= total, 15000), "FT6 both consumers received all", counts[0] + "/" + counts[1]);
            }
            finally
            {
                foreach (var p in plist) p.Dispose();
                foreach (var c in list) c.Dispose();
            }
        }

        static async Task FT7_DisconnectCleanup()
        {
            var topic = QueueServerHarness.NewTopic("ft7");
            var before = QueueServerHarness.DisconnectedCount;

            var consumer = QueueServerHarness.CreateConsumer(topic);
            await Task.Delay(SettleMs);
            consumer.Dispose();

            TestHarness.Expect(await TestHarness.WaitUntil(() => QueueServerHarness.DisconnectedCount > before, 10000), "FT7 server observed disconnect", "before=" + before + " after=" + QueueServerHarness.DisconnectedCount);
        }

        static async Task FT8_ReconnectUsable()
        {
            var topic = QueueServerHarness.NewTopic("ft8");

            long first = 0;
            var c1 = QueueServerHarness.CreateConsumer(topic);
            c1.OnMessage += obj => { try { Interlocked.Increment(ref first); } finally { obj.Dispose(); } };
            await Task.Delay(SettleMs);
            c1.Dispose();

            long second = 0;
            var c2 = QueueServerHarness.CreateConsumer(topic);
            c2.OnMessage += obj => { try { Interlocked.Increment(ref second); } finally { obj.Dispose(); } };
            await Task.Delay(SettleMs);

            var producer = QueueServerHarness.CreateProducer();
            long sent = 0;
            producer.OnMessagesSent += c => Interlocked.Add(ref sent, c);

            try
            {
                for (int i = 0; i < 100; i++) producer.Publish(topic, "ft8-" + i);

                TestHarness.Expect(await TestHarness.WaitUntil(() => Interlocked.Read(ref sent) >= 100, 30000), "FT8 producer flushed all");
                TestHarness.Expect(await TestHarness.WaitUntil(() => second >= 100, 15000), "FT8 new consumer received", "second=" + second);
            }
            finally
            {
                producer.Dispose();
                c2.Dispose();
            }
        }

        static void FT9_EncodeDecodeRoundtrip()
        {
            var cases = new List<Tuple<string, string, byte[]>>
            {
                new Tuple<string, string, byte[]>("producer", "topic", new byte[0]),
                new Tuple<string, string, byte[]>("生产者", "主题", new byte[] { 1, 2, 3 }),
                new Tuple<string, string, byte[]>("a", "b", new byte[4096])
            };

            int idx = 0;
            foreach (var c in cases)
            {
                idx++;
                var name = c.Item1;
                var topic = c.Item2;
                var data = c.Item3;
                for (int i = 0; i < data.Length; i++) data[i] = (byte)(i % 250);

                var frame = QueueCoder.Encode(new QueueSocketMsg(QueueSocketMsgType.Data, name, topic, data));
                var list = QueueCoder.Decode(frame, out int offset);

                var ok = list != null && list.Count == 1 && offset == frame.Length;
                if (ok)
                {
                    var m = list[0];
                    ok = m.Type == QueueSocketMsgType.Data
                        && m.Name == name
                        && m.Topic == topic
                        && m.Data.Length == data.Length;
                    if (ok && data.Length > 0)
                    {
                        for (int i = 0; i < data.Length; i++)
                        {
                            if (m.Data.Span[i] != data[i]) { ok = false; break; }
                        }
                    }
                    if (ok) m.Dispose();
                }

                TestHarness.Expect(ok, "FT9 roundtrip case" + idx, "name=" + name + " topic=" + topic + " len=" + data.Length);
            }
        }

        static readonly FieldInfo ExchangeField = typeof(QServer).GetField("_exchange", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo SubscribersField = typeof(QServer).Assembly.GetType("SAEA.QueueSocket.Model.Exchange")?.GetField("_subscribers", BindingFlags.NonPublic | BindingFlags.Instance);

        static IDictionary GetSubscribers()
        {
            var server = QueueServerHarness.Server;
            if (server == null || ExchangeField == null || SubscribersField == null) return null;
            var exchange = ExchangeField.GetValue(server);
            if (exchange == null) return null;
            return SubscribersField.GetValue(exchange) as IDictionary;
        }

        static int TopicSubscriberCount(string topic)
        {
            var subs = GetSubscribers();
            if (subs == null || !subs.Contains(topic)) return 0;
            var inner = subs[topic] as IDictionary;
            return inner == null ? 0 : inner.Count;
        }

        static bool TopicEntryExists(string topic)
        {
            var subs = GetSubscribers();
            return subs != null && subs.Contains(topic);
        }

        static async Task FT10_DisconnectCleansSubscription()
        {
            var topic = QueueServerHarness.NewTopic("ft10");

            var consumer = QueueServerHarness.CreateConsumer(topic);
            await Task.Delay(SettleMs);

            TestHarness.Expect(await TestHarness.WaitUntil(() => TopicSubscriberCount(topic) == 1, 5000), "FT10 subscription registered", "count=" + TopicSubscriberCount(topic));

            consumer.Dispose();

            TestHarness.Expect(await TestHarness.WaitUntil(() => TopicSubscriberCount(topic) == 0, 10000), "FT10 disconnect removes subscriber", "count=" + TopicSubscriberCount(topic));
            TestHarness.Expect(await TestHarness.WaitUntil(() => !TopicEntryExists(topic), 5000), "FT10 empty topic entry removed", "exists=" + TopicEntryExists(topic));
        }

        static void FT11_BatchEncodeMatchesPerFrame()
        {
            var writeFrame = typeof(QueueCoder).GetMethod("WriteFrame", BindingFlags.NonPublic | BindingFlags.Static);
            TestHarness.Expect(writeFrame != null, "FT11 internal WriteFrame available");
            if (writeFrame == null) return;

            var cases = new List<Tuple<string, string, int>>
            {
                new Tuple<string, string, int>("producer", "topic", 0),
                new Tuple<string, string, int>("", "", 8),
                new Tuple<string, string, int>("生产者", "主题", 1),
                new Tuple<string, string, int>("p", "t", 64),
                new Tuple<string, string, int>("p", "t", 4096)
            };

            int idx = 0;
            foreach (var c in cases)
            {
                idx++;
                var name = c.Item1;
                var topic = c.Item2;
                var size = c.Item3;

                var data = new byte[size];
                for (int i = 0; i < size; i++) data[i] = (byte)(i % 250);

                var frames = new[] { data, data, data };

                var perFrame = new List<byte>();
                foreach (var d in frames)
                {
                    perFrame.AddRange(QueueCoder.Encode(new QueueSocketMsg(QueueSocketMsgType.Data, name, topic, d)));
                }

                var nameBytes = string.IsNullOrEmpty(name) ? null : Encoding.UTF8.GetBytes(name);
                var topicBytes = string.IsNullOrEmpty(topic) ? null : Encoding.UTF8.GetBytes(topic);
                var fixedLen = 1 + 12 + (nameBytes == null ? 0 : nameBytes.Length) + (topicBytes == null ? 0 : topicBytes.Length);

                long bufferSize = 0;
                foreach (var d in frames) bufferSize += fixedLen + d.Length;

                var buffer = new byte[bufferSize];
                var offset = 0;
                foreach (var d in frames)
                {
                    offset = (int)writeFrame.Invoke(null, new object[] { buffer, offset, QueueSocketMsgType.Data, nameBytes, topicBytes, d });
                }

                var ok = offset == buffer.Length && buffer.Length == perFrame.Count;
                if (ok)
                {
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        if (buffer[i] != perFrame[i]) { ok = false; break; }
                    }
                }

                TestHarness.Expect(ok, "FT11 batch matches per-frame case" + idx, "name=" + name + " topic=" + topic + " size=" + size + " len=" + buffer.Length);
            }
        }

        static void FT12_BatcherCapacityBalanced()
        {
            int overflow = 0;
            EventHandler<FirstChanceExceptionEventArgs> onFirstChance = (s, e) =>
            {
                if (e.Exception is SemaphoreFullException) Interlocked.Increment(ref overflow);
            };
            AppDomain.CurrentDomain.FirstChanceException += onFirstChance;
            try
            {
                var flushed = new TaskCompletionSource<bool>();
                int delivered = 0;
                var batcher = new Batcher<byte[]>(size: 8, timeout: 20, max: 64, name: "ft12");
                batcher.OnBatched += (s, list) =>
                {
                    if (Interlocked.Add(ref delivered, list.Count) >= 40) flushed.TrySetResult(true);
                };
                for (int i = 0; i < 40; i++) batcher.Insert(new byte[] { (byte)i });
                flushed.Task.Wait(3000);
                batcher.Dispose();
                TestHarness.Expect(overflow == 0, "FT12 batcher no SemaphoreFullException", "overflow=" + overflow);
                TestHarness.Expect(delivered == 40, "FT12 batcher flushed all items", "delivered=" + delivered);
            }
            finally
            {
                AppDomain.CurrentDomain.FirstChanceException -= onFirstChance;
            }
        }

        static void FT13_BatcherConcatenationExact()
        {
            var flushed = new TaskCompletionSource<byte[]>();
            var batcher = new Batcher(size: 64, timeout: 20, max: 128, name: "ft13");
            batcher.OnBatched += (s, data) => flushed.TrySetResult(data);

            var expected = new List<byte>();
            var rnd = new Random(12345);
            for (int i = 0; i < 5; i++)
            {
                var len = 1 + rnd.Next(0, 90000);
                var item = new byte[len];
                rnd.NextBytes(item);
                expected.AddRange(item);
                batcher.Insert(item);
            }
            batcher.Insert(Array.Empty<byte>());

            byte[] actual = null;
            if (flushed.Task.Wait(5000)) actual = flushed.Task.Result;

            var ok = actual != null && actual.Length == expected.Count;
            if (ok)
            {
                for (int i = 0; i < actual.Length; i++)
                {
                    if (actual[i] != expected[i]) { ok = false; break; }
                }
            }

            TestHarness.Expect(ok, "FT13 non-generic batcher concatenation exact", actual == null ? "no flush" : "expected=" + expected.Count + " actual=" + actual.Length);
            batcher.Dispose();
        }

        static void FT_PoolBatchMergeOwnershipConservation()
        {
            var payload1 = Encoding.UTF8.GetBytes("pool-batch-A");
            var payload2 = Encoding.UTF8.GetBytes("pool-batch-BB");
            var expected = new List<byte>();
            expected.AddRange(payload1);
            expected.AddRange(payload2);

            var before = MemoryPoolManager.GetStatistics();

            var captured = new TaskCompletionSource<PooledBufferWriter>();
            int capturedCount = -1;
            var batcher = new PooledBatcher(2, 100);
            batcher.OnBatched += (w, count) =>
            {
                capturedCount = count;
                captured.TrySetResult(w);
            };

            var w1 = new PooledBufferWriter(64);
            var s1 = w1.GetSpan(payload1.Length);
            new ReadOnlySpan<byte>(payload1).CopyTo(s1);
            w1.Advance(payload1.Length);

            var w2 = new PooledBufferWriter(64);
            var s2 = w2.GetSpan(payload2.Length);
            new ReadOnlySpan<byte>(payload2).CopyTo(s2);
            w2.Advance(payload2.Length);

            TestHarness.Expect(batcher.Insert(w1), "FT-Pool-Batch insert w1 accepted");
            TestHarness.Expect(batcher.Insert(w2), "FT-Pool-Batch insert w2 accepted");

            PooledBufferWriter merged = null;
            if (captured.Task.Wait(2000)) merged = captured.Task.Result;

            TestHarness.Expect(merged != null, "FT-Pool-Batch callback fired");
            TestHarness.Expect(capturedCount == 2, "FT-Pool-Batch merged count is 2", "count=" + capturedCount);

            bool exact = merged != null;
            if (exact)
            {
                var span = merged.WrittenSpan;
                exact = span.Length == expected.Count;
                if (exact)
                {
                    for (int i = 0; i < expected.Count; i++)
                    {
                        if (span[i] != expected[i]) { exact = false; break; }
                    }
                }
            }
            TestHarness.Expect(exact, "FT-Pool-Batch merged bytes exact", merged == null ? "no merged writer" : "len=" + merged.WrittenSpan.Length + " expected=" + expected.Count);

            if (merged != null) merged.Dispose();
            batcher.Dispose();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            bool conserved = TestHarness.WaitUntil(() =>
            {
                var after = MemoryPoolManager.GetStatistics();
                return (after.SmallPoolRented - before.SmallPoolRented) == (after.SmallPoolReturned - before.SmallPoolReturned);
            }, 2000).GetAwaiter().GetResult();

            var final = MemoryPoolManager.GetStatistics();
            TestHarness.Expect(conserved, "FT-Pool-Batch small pool rented == returned", "rented+" + (final.SmallPoolRented - before.SmallPoolRented) + " returned+" + (final.SmallPoolReturned - before.SmallPoolReturned));

            var returnedBefore = MemoryPoolManager.GetStatistics().SmallPoolReturned;
            var rented = MemoryPoolManager.RentPooled(1024);
            rented.Dispose();
            var returnedAfter = MemoryPoolManager.GetStatistics().SmallPoolReturned;
            TestHarness.Expect(returnedAfter == returnedBefore + 1, "FT-Pool-Batch NotifyReturned increments small returned");

            var rejected = new PooledBufferWriter(16);
            TestHarness.Expect(!batcher.Insert(rejected), "FT-Pool-Batch insert after dispose returns false");
            rejected.Dispose();
        }

        static async Task<bool> PoolBalancedAsync(MemoryPoolStatistics before, int timeoutMs = 3000)
        {
            return await TestHarness.WaitUntil(() =>
            {
                var after = MemoryPoolManager.GetStatistics();
                return (after.SmallPoolRented - before.SmallPoolRented) == (after.SmallPoolReturned - before.SmallPoolReturned)
                    && (after.MediumPoolRented - before.MediumPoolRented) == (after.MediumPoolReturned - before.MediumPoolReturned)
                    && (after.LargePoolRented - before.LargePoolRented) == (after.LargePoolReturned - before.LargePoolReturned);
            }, timeoutMs);
        }

        static async Task FT_Pool1_OwnershipTransferAsync()
        {
            var before = MemoryPoolManager.GetStatistics();

            var buffer = MemoryPoolManager.RentPooled(1024);
            var msg = QueueMsgPool.Rent();
            msg.SetOwner(buffer);

            TestHarness.Expect(msg.Data.Length == buffer.Length, "FT-Pool-1 SetOwner binds Data to owner");

            var detached = msg.DetachOwner();
            TestHarness.Expect(ReferenceEquals(detached, buffer), "FT-Pool-1 DetachOwner returns the same owner");

            msg.Dispose();
            var afterMsgDispose = MemoryPoolManager.GetStatistics();
            TestHarness.Expect(afterMsgDispose.SmallPoolReturned == before.SmallPoolReturned, "FT-Pool-1 Dispose after detach leaves owner alive");

            detached.Dispose();
            var afterDetach = MemoryPoolManager.GetStatistics();
            TestHarness.Expect(afterDetach.SmallPoolReturned == before.SmallPoolReturned + 1, "FT-Pool-1 detached owner returned exactly once");
            QueueMsgPool.Return(msg);

            TestHarness.Expect(await PoolBalancedAsync(before), "FT-Pool-1 ownership transfer balanced");
        }

        static async Task FT_Pool2_PoolConservationAsync()
        {
            var before = MemoryPoolManager.GetStatistics();

            var buffers = new List<PooledBuffer>();
            for (int i = 0; i < 64; i++) buffers.Add(MemoryPoolManager.RentPooled(256 + i * 64));
            foreach (var b in buffers) b.Dispose();

            var writer = new PooledBufferWriter(512);
            writer.Advance(200);
            writer.Dispose();

            TestHarness.Expect(await PoolBalancedAsync(before), "FT-Pool-2 local rents balanced");
        }

        static async Task FT_Pool3_BoundedQueueOverflowAsync()
        {
            var before = MemoryPoolManager.GetStatistics();

            var mq = new MessageQueue(4);

            for (int i = 0; i < 4; i++)
            {
                var b = MemoryPoolManager.RentPooled(128);
                bool enqueued = mq.TryEnqueue("ft-pool3", b);
                TestHarness.Expect(enqueued, "FT-Pool-3 enqueue within capacity " + i);
                if (!enqueued) b.Dispose();
            }

            var extra = MemoryPoolManager.RentPooled(128);
            TestHarness.Expect(!mq.TryEnqueue("ft-pool3", extra), "FT-Pool-3 overflow rejected without throw");
            extra.Dispose();

            for (int i = 0; i < 4; i++)
            {
                var ok = mq.TryDequeue("ft-pool3", out var d) && d != null;
                TestHarness.Expect(ok, "FT-Pool-3 dequeue " + i);
                if (d != null) d.Dispose();
            }

            mq.Dispose();

            TestHarness.Expect(await PoolBalancedAsync(before), "FT-Pool-3 bounded queue balanced");
        }

        static async Task FT_Pool4_DispatchMergedWriterAsync()
        {
            var topic = QueueServerHarness.NewTopic("ft-pool4");
            int n = 200;

            var r1 = new ConcurrentDictionary<string, byte>();
            var r2 = new ConcurrentDictionary<string, byte>();

            var c1 = QueueServerHarness.CreateConsumer(topic);
            c1.OnMessage += obj => { try { r1.TryAdd(Encoding.UTF8.GetString(obj.Data.Span), 0); } finally { obj.Dispose(); } };

            var c2 = QueueServerHarness.CreateConsumer(topic);
            c2.OnMessage += obj => { try { r2.TryAdd(Encoding.UTF8.GetString(obj.Data.Span), 0); } finally { obj.Dispose(); } };

            await Task.Delay(SettleMs);

            var producer = QueueServerHarness.CreateProducer();
            try
            {
                for (int i = 0; i < n; i++) producer.Publish(topic, "ft-pool4-" + i);

                TestHarness.Expect(await TestHarness.WaitUntil(() => r1.Count >= n, 30000), "FT-Pool-4 sub1 received all", "r1=" + r1.Count);
                TestHarness.Expect(await TestHarness.WaitUntil(() => r2.Count >= n, 30000), "FT-Pool-4 sub2 received all", "r2=" + r2.Count);
            }
            finally
            {
                producer.Dispose();
                c1.Dispose();
                c2.Dispose();
            }
        }

        static async Task FT_Pool5_ClientBatchMergeAsync()
        {
            var topic = QueueServerHarness.NewTopic("ft-pool5");
            int n = 300;

            var received = new ConcurrentDictionary<string, byte>();
            var consumer = QueueServerHarness.CreateConsumer(topic);
            consumer.OnMessage += obj => { try { received.TryAdd(Encoding.UTF8.GetString(obj.Data.Span), 0); } finally { obj.Dispose(); } };

            await Task.Delay(SettleMs);

            var producer = QueueServerHarness.CreateProducer();
            long sent = 0;
            long batches = 0;
            producer.OnMessagesSent += c => { Interlocked.Increment(ref batches); Interlocked.Add(ref sent, c); };

            try
            {
                for (int i = 0; i < n; i++) producer.Publish(topic, "ft-pool5-" + i);

                TestHarness.Expect(await TestHarness.WaitUntil(() => Interlocked.Read(ref sent) >= n, 30000), "FT-Pool-5 all client batches submitted", "sent=" + Interlocked.Read(ref sent));
                TestHarness.Expect(await TestHarness.WaitUntil(() => received.Count >= n, 30000), "FT-Pool-5 merged batches received", "received=" + received.Count);
                TestHarness.Expect(Interlocked.Read(ref batches) < n, "FT-Pool-5 publishes merged into fewer batches", "batches=" + Interlocked.Read(ref batches));
            }
            finally
            {
                producer.Dispose();
                consumer.Dispose();
            }
        }

        static async Task FT_Pool6_BatcherClearDrainsAsync()
        {
            await Task.Delay(SettleMs);

            var before = MemoryPoolManager.GetStatistics();

            int callbacks = 0;
            var batcher = new PooledBatcher(100, 60000);
            batcher.OnBatched += (w, c) => { Interlocked.Increment(ref callbacks); w.Dispose(); };

            for (int i = 0; i < 5; i++)
            {
                var w = new PooledBufferWriter(64);
                w.Advance(4);
                TestHarness.Expect(batcher.Insert(w), "FT-Pool-6 insert " + i);
            }

            batcher.Clear();
            TestHarness.Expect(Volatile.Read(ref callbacks) == 0, "FT-Pool-6 clear drains without callback");

            var w2 = new PooledBufferWriter(64);
            TestHarness.Expect(batcher.Insert(w2), "FT-Pool-6 insert after clear accepted");
            batcher.Dispose();

            TestHarness.Expect(await PoolBalancedAsync(before), "FT-Pool-6 batcher drain balanced");
        }

        static async Task FT_Pool7_LargeWriterPooledAsync()
        {
            var before = MemoryPoolManager.GetStatistics();

            int size = 3 * 1024 * 1024;
            var first = MemoryPoolManager.RentPooled(size);
            TestHarness.Expect(first.Tier == BufferSizeTier.Large, "FT-Pool-7 3MB rents from large tier");
            TestHarness.Expect(first.Capacity >= size, "FT-Pool-7 large capacity covers request");
            var rentedArray = first.Buffer;
            first.Dispose();

            var second = MemoryPoolManager.RentPooled(size);
            TestHarness.Expect(ReferenceEquals(second.Buffer, rentedArray), "FT-Pool-7 above-1MB array reused from pool");
            second.Dispose();

            var after = MemoryPoolManager.GetStatistics();
            TestHarness.Expect(after.LargePoolRented - before.LargePoolRented >= 2, "FT-Pool-7 large pool rented counted");
            TestHarness.Expect(after.LargePoolReturned - before.LargePoolReturned >= 2, "FT-Pool-7 large pool returned counted");

            TestHarness.Expect(await PoolBalancedAsync(before), "FT-Pool-7 large buffer balanced");
        }
    }
}
