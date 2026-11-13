using System;
using System.Text;

using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.P2P.Protocol;

namespace SAEA.P2PTest.Tests
{
    /// <summary>
    /// 协议基础自测：消息类型枚举取值、协议帧结构与编解码器
    /// </summary>
    public static class ProtocolTest
    {
        /// <summary>
        /// 依次校验消息类型枚举取值、协议帧构造与编解码器的编解码结果
        /// </summary>
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
            ConsoleHelper.WriteLine($"LocalDiscover: 0x{((byte)P2PMessageType.LocalDiscover):X2} ({(byte)P2PMessageType.LocalDiscover})");
            ConsoleHelper.WriteLine($"AuthChallenge: 0x{((byte)P2PMessageType.AuthChallenge):X2} ({(byte)P2PMessageType.AuthChallenge})");
            ConsoleHelper.WriteLine($"Heartbeat: 0x{((byte)P2PMessageType.Heartbeat):X2} ({(byte)P2PMessageType.Heartbeat})");
            ConsoleHelper.WriteLine($"SessionOffer: 0x{((byte)P2PMessageType.SessionOffer):X2} ({(byte)P2PMessageType.SessionOffer})");

            // 逐项比对期望取值；用表驱动避免全常量条件被判定为恒真而让分支不可达
            var expectedTypes = new[]
            {
                (Type: P2PMessageType.Register, Value: (byte)0x10),
                (Type: P2PMessageType.Heartbeat, Value: (byte)0x70),
                (Type: P2PMessageType.SessionOffer, Value: (byte)0x91)
            };

            bool enumMatched = true;
            foreach (var expected in expectedTypes)
            {
                if ((byte)expected.Type != expected.Value)
                {
                    enumMatched = false;
                }
            }

            if (enumMatched)
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

            var sessionOffer = P2PProtocol.Create(P2PMessageType.SessionOffer, "Hello P2P");
            ConsoleHelper.WriteLine($"SessionOffer Type: {sessionOffer.GetMessageType()}");
            ConsoleHelper.WriteLine($"SessionOffer Content: {sessionOffer.GetContentAsString()}");
            if (sessionOffer.GetMessageType() == P2PMessageType.SessionOffer && sessionOffer.GetContentAsString() == "Hello P2P")
            {
                passed++;
            }

            byte[] sessionOfferBytes;
            using (var w = new PooledBufferWriter(64))
            {
                sessionOffer.WriteTo(w);
                sessionOfferBytes = w.WrittenSpan.ToArray();
            }
            ConsoleHelper.WriteLine($"SessionOffer ToBytes: {sessionOfferBytes.Length} bytes");
            if (sessionOfferBytes.Length > 0)
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

            var encodedSessionOffer = coder.EncodeP2P(P2PMessageType.SessionOffer, "Test message");
            ConsoleHelper.WriteLine($"Encoded SessionOffer: {encodedSessionOffer.Length} bytes");
            if (encodedSessionOffer.Length > 0)
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

            using (var decodedSessionOffer = coder.DecodeP2P(encodedSessionOffer))
            {
                ConsoleHelper.WriteLine($"Decoded SessionOffer count: {decodedSessionOffer.Count}");
                ConsoleHelper.WriteLine($"Decoded SessionOffer content: {Encoding.UTF8.GetString(decodedSessionOffer[0].Content.Span)}");
                if (decodedSessionOffer.Count == 1 &&
                    (P2PMessageType)decodedSessionOffer[0].Type == P2PMessageType.SessionOffer &&
                    Encoding.UTF8.GetString(decodedSessionOffer[0].Content.Span) == "Test message")
                {
                    passed++;
                }
            }

            ConsoleHelper.WriteLine($"TestP2PCoder: {passed}/{total} PASSED");
        }
    }
}