using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SAEA.P2P.Builder;
using SAEA.P2P.Core;
using SAEA.P2P.Protocol;
using SAEA.P2P.Relay;

namespace SAEA.P2PTest.Tests
{
    /// <summary>
    /// 并发安全测试：会话序号、中继会话表、编解码器（每线程独立实例）、构建器。
    /// </summary>
    public static class ConcurrencyTest
    {
        public static void Run()
        {
            TestHarness.Section("ConcurrencyTest");

            ParallelSessionSequences();
            ParallelRelaySessions();
            ParallelCoderRoundTrips();
            ParallelBuilderBuilds();
            ParallelSessionActive();

            TestHarness.WriteSummary("ConcurrencyTest");
        }

        static void ParallelSessionSequences()
        {
            TestHarness.Section("parallel PeerSession sequences");

            var session = new PeerSession("s", "p");
            var sequences = new ConcurrentBag<long>();

            Parallel.For(0, 20000, _ => sequences.Add(session.NextSendSeq()));

            TestHarness.Expect(sequences.Count == 20000, "all calls collected");
            TestHarness.Expect(sequences.Distinct().Count() == 20000, "sequence numbers are unique");
            TestHarness.Expect(sequences.Max() == 20000, "counter reached expected maximum");
        }

        static void ParallelRelaySessions()
        {
            TestHarness.Section("parallel relay session creation");

            var manager = new RelayManager();
            var ids = new ConcurrentDictionary<string, byte>();

            Parallel.For(0, 4000, _ => ids[manager.CreateSession("a", "b").SessionId] = 0);

            TestHarness.Expect(ids.Count == 4000, "relay ids unique under contention");
            TestHarness.Expect(manager.ActiveSessionCount == 4000, "relay sessions all tracked");
        }

        static void ParallelCoderRoundTrips()
        {
            TestHarness.Section("parallel coder round trips");

            int errors = 0;

            Parallel.For(0, 8, worker =>
            {
                var coder = new P2PCoder();
                for (int i = 0; i < 2000; i++)
                {
                    var text = $"w{worker}-i{i}";
                    var decoded = coder.DecodeP2P(coder.EncodeP2P(P2PMessageType.UserData, text));
                    if (decoded.Count != 1 || decoded[0].GetContentAsString() != text)
                    {
                        Interlocked.Increment(ref errors);
                    }
                }
            });

            TestHarness.Expect(errors == 0, "per-coder parallel round trips stable", errors.ToString());
        }

        static void ParallelBuilderBuilds()
        {
            TestHarness.Section("parallel builder builds");

            int errors = 0;

            Parallel.For(0, 5000, _ =>
            {
                try
                {
                    var options = new P2PClientBuilder().SetNodeId("n").EnableRelay().Build();
                    if (options.NodeId != "n") Interlocked.Increment(ref errors);
                }
                catch
                {
                    Interlocked.Increment(ref errors);
                }
            });

            TestHarness.Expect(errors == 0, "independent builder instances are thread-safe", errors.ToString());
        }

        static void ParallelSessionActive()
        {
            TestHarness.Section("parallel PeerSession Active");

            var session = new PeerSession("s", "p");

            Parallel.For(0, 10000, _ => session.Active());

            TestHarness.Expect(!session.IsExpired(60000), "concurrent Active keeps session alive");
        }
    }
}