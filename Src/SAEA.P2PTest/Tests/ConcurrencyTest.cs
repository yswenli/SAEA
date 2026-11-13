using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SAEA.P2P.Builder;
using SAEA.P2P.Channel;
using SAEA.P2P.Core;
using SAEA.P2P.Protocol;

namespace SAEA.P2PTest.Tests
{
    /// <summary>
    /// 并发安全测试：会话标识与活跃时间、编解码器（每线程独立实例）、构建器、通道类型。
    /// </summary>
    public static class ConcurrencyTest
    {
        /// <summary>
        /// 依次运行会话标识、活跃时间、编解码器、构建器与通道类型的多线程并发断言
        /// </summary>
        public static void Run()
        {
            TestHarness.Section("ConcurrencyTest");

            ParallelSessionIdentity();
            ParallelSessionActive();
            ParallelCoderRoundTrips();
            ParallelBuilderBuilds();
            ParallelChannelTypeReadWrite();

            TestHarness.WriteSummary("ConcurrencyTest");
        }

        static void ParallelSessionIdentity()
        {
            TestHarness.Section("parallel PeerSession identity");

            var session = new PeerSession("s", "p");
            var sessionIds = new ConcurrentDictionary<string, byte>();
            var peerIds = new ConcurrentDictionary<string, byte>();
            int errors = 0;

            Parallel.For(0, 20000, _ =>
            {
                // 标识创建后不可变，多线程反复读取必须始终看到同一对取值
                if (session.SessionId != "s" || session.PeerId != "p")
                {
                    Interlocked.Increment(ref errors);
                }

                sessionIds[session.SessionId] = 0;
                peerIds[session.PeerId] = 0;
            });

            TestHarness.Expect(errors == 0, "identity reads stable under contention", errors.ToString());
            TestHarness.Expect(sessionIds.Count == 1, "session id never changes", sessionIds.Count.ToString());
            TestHarness.Expect(peerIds.Count == 1, "peer id never changes", peerIds.Count.ToString());
        }

        static void ParallelSessionActive()
        {
            TestHarness.Section("parallel PeerSession Active");

            var session = new PeerSession("s", "p");
            var created = session.CreatedTime;
            var stamps = new ConcurrentBag<DateTime>();

            Parallel.For(0, 10000, _ =>
            {
                session.Active();
                stamps.Add(session.LastActiveTime);
            });

            TestHarness.Expect(stamps.Count == 10000, "all Active calls collected", stamps.Count.ToString());
            TestHarness.Expect(stamps.All(stamp => stamp >= created), "active timestamps never precede creation");
            TestHarness.Expect(session.LastActiveTime >= created, "concurrent Active keeps session alive");
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
                    using (var decoded = coder.DecodeP2P(coder.EncodeP2P(P2PMessageType.SessionOffer, text)))
                    {
                        if (decoded.Count != 1 || Encoding.UTF8.GetString(decoded[0].Content.Span) != text)
                        {
                            Interlocked.Increment(ref errors);
                        }
                    }
                }
            });

            TestHarness.Expect(errors == 0, "per-coder parallel round trips stable", errors.ToString());
        }

        static void ParallelBuilderBuilds()
        {
            TestHarness.Section("parallel builder builds");

            int errors = 0;

            Parallel.For(0, 1000, _ =>
            {
                try
                {
                    // BuildClient 会建生命周期对象，必须释放；端口不监听，只验证并发装配本身
                    using (var client = new P2PClientBuilder().SetServer("127.0.0.1", 1).SetNodeId("x").BuildClient())
                    {
                        if (client.PeerId != "x")
                        {
                            Interlocked.Increment(ref errors);
                        }
                    }
                }
                catch
                {
                    Interlocked.Increment(ref errors);
                }
            });

            TestHarness.Expect(errors == 0, "independent builder instances are thread-safe", errors.ToString());
        }

        static void ParallelChannelTypeReadWrite()
        {
            TestHarness.Section("parallel PeerSession channel writes");

            var session = new PeerSession("s", "p");
            var values = (ChannelType[])Enum.GetValues(typeof(ChannelType));
            int errors = 0;

            Parallel.For(0, 20000, i =>
            {
                // 通道类型是普通属性，多线程只要求读到已定义取值，不要求写入顺序
                session.Channel = values[i % values.Length];
                if (!Enum.IsDefined(typeof(ChannelType), session.Channel))
                {
                    Interlocked.Increment(ref errors);
                }
            });

            TestHarness.Expect(errors == 0, "channel reads always see a defined value", errors.ToString());
            TestHarness.Expect(values.Contains(session.Channel), "final channel is one of the written values", session.Channel.ToString());
        }
    }
}