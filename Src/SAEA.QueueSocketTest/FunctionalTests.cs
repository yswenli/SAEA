using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
                try { received.TryAdd(Encoding.UTF8.GetString(obj.Data), 0); }
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
                        gotLen = d == null ? -1 : d.Length;
                        identical = d != null && d.Length == size;
                        if (identical)
                        {
                            for (int i = 0; i < size; i++)
                            {
                                if (d[i] != payload[i]) { identical = false; break; }
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
                        && (m.Data == null ? 0 : m.Data.Length) == data.Length;
                    if (ok && data.Length > 0)
                    {
                        for (int i = 0; i < data.Length; i++)
                        {
                            if (m.Data[i] != data[i]) { ok = false; break; }
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
    }
}