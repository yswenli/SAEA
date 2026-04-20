using System;
using System.Text;
using System.Threading.Tasks;
using SAEA.Common;
using SAEA.P2P.Relay;
using SAEA.P2P.Common;
using SAEA.P2P.Protocol;

namespace SAEA.P2PTest.Tests
{
    public static class RelayTest
    {
        public static async Task RunAsync()
        {
            ConsoleHelper.WriteLine("=== RelayTest ===");
            ConsoleHelper.WriteLine("");

            TestRelayManagerCreation();
            ConsoleHelper.WriteLine("");

            TestRelaySessionCreation();
            ConsoleHelper.WriteLine("");

            TestRelaySessionQuota();
            ConsoleHelper.WriteLine("");

            TestRelayDataEncoding();
            ConsoleHelper.WriteLine("");

            TestRelaySessionExpiration();
            ConsoleHelper.WriteLine("");

            ConsoleHelper.WriteLine("=== RelayTest Complete ===");
        }

        static void TestRelayManagerCreation()
        {
            ConsoleHelper.WriteLine("--- TestRelayManagerCreation ---");

            var manager = new RelayManager(60000, 10 * 1024 * 1024);

            ConsoleHelper.WriteLine($"Timeout: 60000ms");
            ConsoleHelper.WriteLine($"DefaultQuota: 10MB");
            ConsoleHelper.WriteLine($"ActiveSessionCount: {manager.ActiveSessionCount}");

            if (manager.ActiveSessionCount == 0)
            {
                ConsoleHelper.WriteLine("TestRelayManagerCreation: PASSED");
            }
            else
            {
                ConsoleHelper.WriteLine("TestRelayManagerCreation: FAILED");
            }
        }

        static void TestRelaySessionCreation()
        {
            ConsoleHelper.WriteLine("--- TestRelaySessionCreation ---");

            var manager = new RelayManager();

            var session = manager.CreateSession("node-A", "node-B");

            ConsoleHelper.WriteLine($"SessionId: {session.SessionId}");
            ConsoleHelper.WriteLine($"SourceNodeId: {session.SourceNodeId}");
            ConsoleHelper.WriteLine($"TargetNodeId: {session.TargetNodeId}");
            ConsoleHelper.WriteLine($"State: {session.State}");
            ConsoleHelper.WriteLine($"MaxQuota: {session.MaxQuota}");
            ConsoleHelper.WriteLine($"BytesTransferred: {session.BytesTransferred}");

            var retrieved = manager.GetSession(session.SessionId);

            if (retrieved != null && retrieved.SessionId == session.SessionId)
            {
                ConsoleHelper.WriteLine("TestRelaySessionCreation: PASSED");
            }
            else
            {
                ConsoleHelper.WriteLine("TestRelaySessionCreation: FAILED");
            }

            manager.CloseSession(session.SessionId);
            ConsoleHelper.WriteLine($"After Close: ActiveSessionCount={manager.ActiveSessionCount}");
        }

        static void TestRelaySessionQuota()
        {
            ConsoleHelper.WriteLine("--- TestRelaySessionQuota ---");

            var manager = new RelayManager(60000, 1000);
            var session = manager.CreateSession("node-A", "node-B", 1000);

            ConsoleHelper.WriteLine($"MaxQuota: {session.MaxQuota}");
            ConsoleHelper.WriteLine($"IsOverQuota: {session.IsOverQuota}");

            session.AddBytes(500);
            ConsoleHelper.WriteLine($"After 500 bytes: BytesTransferred={session.BytesTransferred}, IsOverQuota={session.IsOverQuota}");

            session.AddBytes(600);
            ConsoleHelper.WriteLine($"After 1100 bytes: BytesTransferred={session.BytesTransferred}, IsOverQuota={session.IsOverQuota}");

            if (!session.IsOverQuota && session.BytesTransferred == 1100)
            {
                var overQuotaSession = manager.CreateSession("node-C", "node-D", 100);
                overQuotaSession.AddBytes(150);
                ConsoleHelper.WriteLine($"Over quota session: IsOverQuota={overQuotaSession.IsOverQuota}");

                if (overQuotaSession.IsOverQuota)
                {
                    ConsoleHelper.WriteLine("TestRelaySessionQuota: PASSED");
                }
                else
                {
                    ConsoleHelper.WriteLine("TestRelaySessionQuota: FAILED (over quota check)");
                }
            }
            else
            {
                ConsoleHelper.WriteLine("TestRelaySessionQuota: FAILED");
            }
        }

        static void TestRelayDataEncoding()
        {
            ConsoleHelper.WriteLine("--- TestRelayDataEncoding ---");

            var manager = new RelayManager();
            var session = manager.CreateSession("source-node", "target-node");

            var payload = Encoding.UTF8.GetBytes("Hello Relay World!");
            var encodedData = manager.EncodeRelayData(session.SessionId, "source-node", "target-node", payload);

            ConsoleHelper.WriteLine($"Encoded data length: {encodedData.Length}");

            var (sessionId, sourceId, targetId, decodedPayload) = manager.DecodeRelayData(encodedData);

            ConsoleHelper.WriteLine($"Decoded SessionId: {sessionId}");
            ConsoleHelper.WriteLine($"Decoded SourceId: {sourceId}");
            ConsoleHelper.WriteLine($"Decoded TargetId: {targetId}");
            ConsoleHelper.WriteLine($"Decoded Payload: {Encoding.UTF8.GetString(decodedPayload)}");

            if (sessionId == session.SessionId &&
                sourceId == "source-node" &&
                targetId == "target-node" &&
                Encoding.UTF8.GetString(decodedPayload) == "Hello Relay World!")
            {
                ConsoleHelper.WriteLine("TestRelayDataEncoding: PASSED");
            }
            else
            {
                ConsoleHelper.WriteLine("TestRelayDataEncoding: FAILED");
            }

            manager.CloseSession(session.SessionId);
        }

        static async Task TestRelaySessionExpiration()
        {
            ConsoleHelper.WriteLine("--- TestRelaySessionExpiration ---");

            var manager = new RelayManager(500, 10000);
            var session = manager.CreateSession("node-X", "node-Y");

            ConsoleHelper.WriteLine($"Session created: {session.SessionId}");
            ConsoleHelper.WriteLine($"Session State: {session.State}");
            ConsoleHelper.WriteLine($"ActiveSessionCount: {manager.ActiveSessionCount}");

            session.Activate();
            ConsoleHelper.WriteLine($"After Activate: State={session.State}");

            await Task.Delay(600);

            var expired = session.IsExpired(500);
            ConsoleHelper.WriteLine($"After 600ms delay: IsExpired={expired}");

            manager.CleanupExpiredSessions();
            ConsoleHelper.WriteLine($"After Cleanup: ActiveSessionCount={manager.ActiveSessionCount}");

            if (expired && manager.ActiveSessionCount == 0)
            {
                ConsoleHelper.WriteLine("TestRelaySessionExpiration: PASSED");
            }
            else
            {
                ConsoleHelper.WriteLine("TestRelaySessionExpiration: FAILED");
            }
        }
    }
}