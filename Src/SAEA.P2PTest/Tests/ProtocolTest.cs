using System;
using System.Text;

using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.P2P.Protocol;

namespace SAEA.P2PTest.Tests
{
    public static class ProtocolTest
    {
        public static void Run()
        {
            ConsoleHelper.WriteLine("=== ProtocolTest ===");
ConsoleHelper.WriteLine("");

            TestMessageTypeEnum();
            ConsoleHelper.WriteLine("");

            TestP2PProtocol();
            ConsoleHelper.WriteLine("");

            TestP2PCoder();
            ConsoleHelper.WriteLine("");

            ConsoleHelper.WriteLine("=== ProtocolTest Complete ===");
        }

        static void TestMessageTypeEnum()
        {
            ConsoleHelper.WriteLine("--- TestMessageTypeEnum ---");

            ConsoleHelper.WriteLine($"Register: 0x{((byte)P2PMessageType.Register):X2} ({(byte)P2PMessageType.Register})");
            ConsoleHelper.WriteLine($"NatProbe: 0x{((byte)P2PMessageType.NatProbe):X2} ({(byte)P2PMessageType.NatProbe})");
            ConsoleHelper.WriteLine($"PunchRequest: 0x{((byte)P2PMessageType.PunchRequest):X2} ({(byte)P2PMessageType.PunchRequest})");
            ConsoleHelper.WriteLine($"RelayRequest: 0x{((byte)P2PMessageType.RelayRequest):X2} ({(byte)P2PMessageType.RelayRequest})");
            ConsoleHelper.WriteLine($"LocalDiscover: 0x{((byte)P2PMessageType.LocalDiscover):X2} ({(byte)P2PMessageType.LocalDiscover})");
            ConsoleHelper.WriteLine($"AuthChallenge: 0x{((byte)P2PMessageType.AuthChallenge):X2} ({(byte)P2PMessageType.AuthChallenge})");
            ConsoleHelper.WriteLine($"Heartbeat: 0x{((byte)P2PMessageType.Heartbeat):X2} ({(byte)P2PMessageType.Heartbeat})");
            ConsoleHelper.WriteLine($"UserData: 0x{((byte)P2PMessageType.UserData):X2} ({(byte)P2PMessageType.UserData})");

            if ((byte)P2PMessageType.Register == 0x10 &&
                (byte)P2PMessageType.Heartbeat == 0x70 &&
                (byte)P2PMessageType.UserData == 0x80)
            {
                ConsoleHelper.WriteLine("TestMessageTypeEnum: PASSED");
            }
            else
            {
                ConsoleHelper.WriteLine("TestMessageTypeEnum: FAILED");
            }
        }

        static void TestP2PProtocol()
        {
            ConsoleHelper.WriteLine("--- TestP2PProtocol ---");

            int passed = 0;
            int total = 4;

            var heartbeat = P2PProtocol.Create(P2PMessageType.Heartbeat);
            ConsoleHelper.WriteLine($"Heartbeat Type: {heartbeat.GetMessageType()}");
            ConsoleHelper.WriteLine($"Heartbeat Content: {(heartbeat.Content.Length == 0 ? "null" : heartbeat.Content.Length + " bytes")}");
            if (heartbeat.GetMessageType() == P2PMessageType.Heartbeat && heartbeat.Content.Length == 0)
            {
                passed++;
            }

            byte[] heartbeatBytes;
            using (var w = new PooledBufferWriter(64))
            {
                heartbeat.WriteTo(w);
                heartbeatBytes = w.WrittenSpan.ToArray();
            }
            ConsoleHelper.WriteLine($"Heartbeat ToBytes: {heartbeatBytes.Length} bytes");
            if (heartbeatBytes.Length > 0)
            {
                passed++;
            }

            var userData = P2PProtocol.Create(P2PMessageType.UserData, "Hello P2P");
            ConsoleHelper.WriteLine($"UserData Type: {userData.GetMessageType()}");
            ConsoleHelper.WriteLine($"UserData Content: {userData.GetContentAsString()}");
            if (userData.GetMessageType() == P2PMessageType.UserData && userData.GetContentAsString() == "Hello P2P")
            {
                passed++;
            }

            byte[] userDataBytes;
            using (var w = new PooledBufferWriter(64))
            {
                userData.WriteTo(w);
                userDataBytes = w.WrittenSpan.ToArray();
            }
            ConsoleHelper.WriteLine($"UserData ToBytes: {userDataBytes.Length} bytes");
            if (userDataBytes.Length > 0)
            {
                passed++;
            }

            ConsoleHelper.WriteLine($"TestP2PProtocol: {passed}/{total} PASSED");
        }

        static void TestP2PCoder()
        {
            ConsoleHelper.WriteLine("--- TestP2PCoder ---");

            int passed = 0;
            int total = 4;

            var coder = new P2PCoder();

            var encodedHeartbeat = coder.EncodeP2P(P2PMessageType.Heartbeat);
            ConsoleHelper.WriteLine($"Encoded Heartbeat: {encodedHeartbeat.Length} bytes");
            if (encodedHeartbeat.Length > 0)
            {
                passed++;
            }

            var encodedUserData = coder.EncodeP2P(P2PMessageType.UserData, "Test message");
            ConsoleHelper.WriteLine($"Encoded UserData: {encodedUserData.Length} bytes");
            if (encodedUserData.Length > 0)
            {
                passed++;
            }

            using (var decodedHeartbeat = coder.DecodeP2P(encodedHeartbeat))
            {
                ConsoleHelper.WriteLine($"Decoded Heartbeat count: {decodedHeartbeat.Count}");
                if (decodedHeartbeat.Count == 1 && (P2PMessageType)decodedHeartbeat[0].Type == P2PMessageType.Heartbeat)
                {
                    passed++;
                }
            }

            using (var decodedUserData = coder.DecodeP2P(encodedUserData))
            {
                ConsoleHelper.WriteLine($"Decoded UserData count: {decodedUserData.Count}");
                ConsoleHelper.WriteLine($"Decoded UserData content: {Encoding.UTF8.GetString(decodedUserData[0].Content.Span)}");
                if (decodedUserData.Count == 1 &&
                    (P2PMessageType)decodedUserData[0].Type == P2PMessageType.UserData &&
                    Encoding.UTF8.GetString(decodedUserData[0].Content.Span) == "Test message")
                {
                    passed++;
                }
            }

            ConsoleHelper.WriteLine($"TestP2PCoder: {passed}/{total} PASSED");
        }
    }
}