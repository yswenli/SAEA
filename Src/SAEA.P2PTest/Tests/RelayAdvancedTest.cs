using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using SAEA.P2P.Common;
using SAEA.P2P.Protocol;
using SAEA.P2P.Relay;

namespace SAEA.P2PTest.Tests
{
    /// <summary>
    /// 中继进阶测试：会话生命周期、配额、二进制安全、激活语义、并发创建。
    /// </summary>
    public static class RelayAdvancedTest
    {
        public static void Run()
        {
            TestHarness.Section("RelayAdvancedTest");

            UnknownSessionThrows();
            QuotaEnforced();
            QuotaZeroUnlimited();
            EncodeDecodeRoundTrip();
            ProcessRelayDataOnlyActive();
            CloseSessionBehavior();
            CleanupExpired();
            ConcurrentCreateUnique();

            TestHarness.WriteSummary("RelayAdvancedTest");
        }

        static void UnknownSessionThrows()
        {
            TestHarness.Section("relay unknown session");

            var manager = new RelayManager();
            TestHarness.Throws<P2PException>(
                () => manager.EncodeRelayData("nope", "a", "b", new byte[] { 1 }),
                "encode with unknown session throws");

            var decoded = manager.DecodeRelayData(new P2PCoder().EncodeP2P(P2PMessageType.Heartbeat));
            TestHarness.Expect(decoded.payload == null && decoded.sessionId == null, "decode non-relay message returns nulls");
        }

        static void QuotaEnforced()
        {
            TestHarness.Section("relay quota enforced");

            var manager = new RelayManager(defaultQuota: 10);
            var session = manager.CreateSession("a", "b");
            TestHarness.Expect(session.MaxQuota == 10, "session inherits default quota");

            manager.EncodeRelayData(session.SessionId, "a", "b", new byte[11]);
            TestHarness.Expect(session.BytesTransferred == 11, "bytes transferred accounted");

            TestHarness.Throws<P2PException>(
                () => manager.EncodeRelayData(session.SessionId, "a", "b", new byte[1]),
                "over quota throws");
        }

        static void QuotaZeroUnlimited()
        {
            TestHarness.Section("relay quota zero unlimited");

            var manager = new RelayManager(defaultQuota: 0);
            var session = manager.CreateSession("a", "b");

            for (int i = 0; i < 100; i++)
            {
                manager.EncodeRelayData(session.SessionId, "a", "b", new byte[1000]);
            }

            TestHarness.Expect(session.BytesTransferred == 100_000 && !session.IsOverQuota, "quota 0 never exceeds");
        }

        static void EncodeDecodeRoundTrip()
        {
            TestHarness.Section("relay encode/decode round trip");

            var manager = new RelayManager();
            var session = manager.CreateSession("src", "dst");
            var payload = Enumerable.Range(0, 300).Select(i => (byte)i).ToArray();

            var frame = manager.EncodeRelayData(session.SessionId, "src", "dst", payload);
            var (sessionId, sourceId, targetId, decoded) = manager.DecodeRelayData(frame);

            TestHarness.Expect(sessionId == session.SessionId, "session id parsed");
            TestHarness.Expect(sourceId == "src" && targetId == "dst", "source/target parsed");
            TestHarness.Expect(decoded != null && decoded.SequenceEqual(payload), "payload byte-exact");
        }

        static void ProcessRelayDataOnlyActive()
        {
            TestHarness.Section("relay ProcessRelayData activation");

            var manager = new RelayManager();
            var session = manager.CreateSession("src", "dst");

            byte[] captured = null;
            string capturedTarget = null;
            manager.OnRelayData += (_, target, data) => { capturedTarget = target; captured = data; };

            var payload = new byte[] { 9, 8, 7 };
            var frame = manager.EncodeRelayData(session.SessionId, "src", "dst", payload);

            manager.ProcessRelayData(frame);
            TestHarness.Expect(captured == null, "pending session does not raise relay data");

            manager.ActivateSession(session.SessionId);
            manager.ProcessRelayData(frame);
            TestHarness.Expect(captured != null && captured.SequenceEqual(payload), "active session raises payload");
            TestHarness.Expect(capturedTarget == "dst", "relay data target passed through");
        }

        static void CloseSessionBehavior()
        {
            TestHarness.Section("relay CloseSession");

            var manager = new RelayManager();
            int ended = 0;
            manager.OnRelayEnded += _ => ended++;

            manager.CloseSession("missing");
            TestHarness.Expect(true, "closing unknown session does not throw");

            var session = manager.CreateSession("a", "b");
            TestHarness.Expect(manager.ActiveSessionCount == 1, "session tracked");

            manager.CloseSession(session.SessionId);
            TestHarness.Expect(manager.GetSession(session.SessionId) == null, "session removed");
            TestHarness.Expect(manager.ActiveSessionCount == 0, "active count decremented");
            TestHarness.Expect(ended == 1, "OnRelayEnded fired once");
            TestHarness.Expect(session.State == RelayState.Closed, "session state closed");
        }

        static void CleanupExpired()
        {
            TestHarness.Section("relay CleanupExpiredSessions");

            var manager = new RelayManager(timeout: 1);
            var session = manager.CreateSession("a", "b");
            session.LastActiveTime = DateTime.UtcNow.AddMilliseconds(-50);

            manager.CleanupExpiredSessions();
            TestHarness.Expect(manager.ActiveSessionCount == 0, "expired session removed");
        }

        static void ConcurrentCreateUnique()
        {
            TestHarness.Section("relay concurrent session creation");

            var manager = new RelayManager();
            var ids = new ConcurrentDictionary<string, byte>();

            Parallel.For(0, 2000, _ =>
            {
                var session = manager.CreateSession("a", "b");
                ids[session.SessionId] = 0;
            });

            TestHarness.Expect(ids.Count == 2000, "all session ids unique", ids.Count.ToString());
            TestHarness.Expect(manager.ActiveSessionCount == 2000, "all sessions tracked", manager.ActiveSessionCount.ToString());
        }
    }
}