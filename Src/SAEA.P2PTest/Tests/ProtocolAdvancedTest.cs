using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SAEA.P2P.Protocol;
using SAEA.Sockets.Base;

namespace SAEA.P2PTest.Tests
{
    /// <summary>
    /// 协议编解码进阶测试：二进制安全、粘包/拆包、边界长度、大载荷。
    /// </summary>
    public static class ProtocolAdvancedTest
    {
        public static void Run()
        {
            TestHarness.Section("ProtocolAdvancedTest");

            RoundTripBinary();
            EmptyContent();
            MultipleFramesInOneBuffer();
            SplitFrame();
            MessageTypeLookup();
            ShortDataGuard();
            FrameHeaderLayout();
            UnicodeRoundTrip();
            LargePayload();

            TestHarness.WriteSummary("ProtocolAdvancedTest");
        }

        static void RoundTripBinary()
        {
            TestHarness.Section("binary round trip");
            var coder = new P2PCoder();
            var content = new byte[256];
            for (int i = 0; i < content.Length; i++) content[i] = (byte)i;

            var decoded = coder.DecodeP2P(coder.EncodeP2P(P2PMessageType.UserData, content));

            TestHarness.Expect(decoded.Count == 1, "one message decoded");
            TestHarness.Expect(decoded[0].GetMessageType() == P2PMessageType.UserData, "message type preserved");
            TestHarness.Expect(decoded[0].Content != null && decoded[0].Content.SequenceEqual(content), "binary content byte-exact");
        }

        static void EmptyContent()
        {
            TestHarness.Section("empty content");
            var coder = new P2PCoder();
            var decoded = coder.DecodeP2P(coder.EncodeP2P(P2PMessageType.Heartbeat));

            TestHarness.Expect(decoded.Count == 1, "empty message decoded");
            TestHarness.Expect(decoded[0].GetMessageType() == P2PMessageType.Heartbeat, "empty message type preserved");
            TestHarness.Expect(decoded[0].BodyLength == 0, "empty body length is 0");
            TestHarness.Expect(decoded[0].Content == null || decoded[0].Content.Length == 0, "empty content is null or zero length");
        }

        static void MultipleFramesInOneBuffer()
        {
            TestHarness.Section("multiple frames in one buffer");
            var coder = new P2PCoder();
            var all = new List<byte>();
            all.AddRange(coder.EncodeP2P(P2PMessageType.UserData, "one"));
            all.AddRange(coder.EncodeP2P(P2PMessageType.Register, "two"));
            all.AddRange(coder.EncodeP2P(P2PMessageType.HeartbeatAck));

            var decoded = coder.DecodeP2P(all.ToArray());

            TestHarness.Expect(decoded.Count == 3, "three messages decoded", decoded.Count.ToString());
            TestHarness.Expect(decoded[0].GetContentAsString() == "one", "first message content");
            TestHarness.Expect(decoded[1].GetContentAsString() == "two", "second message content");
            TestHarness.Expect(decoded[2].GetMessageType() == P2PMessageType.HeartbeatAck, "third message type");
        }

        static void SplitFrame()
        {
            TestHarness.Section("split frame");
            var coder = new P2PCoder();
            var frame = coder.EncodeP2P(P2PMessageType.UserData, "split-payload");
            var half = frame.Length / 2;

            var first = new byte[half];
            Buffer.BlockCopy(frame, 0, first, 0, half);
            var firstResult = coder.DecodeP2P(first);
            TestHarness.Expect(firstResult.Count == 0, "partial frame yields no message");

            var second = new byte[frame.Length - half];
            Buffer.BlockCopy(frame, half, second, 0, second.Length);
            var secondResult = coder.DecodeP2P(second);
            TestHarness.Expect(secondResult.Count == 1, "remaining bytes complete the frame");
            TestHarness.Expect(secondResult.Count == 1 && secondResult[0].GetContentAsString() == "split-payload", "reassembled payload intact");
        }

        static void MessageTypeLookup()
        {
            TestHarness.Section("message type lookup");
            var coder = new P2PCoder();
            var frame = coder.EncodeP2P(P2PMessageType.RelayRequest, "x");
            TestHarness.Expect(P2PCoder.GetP2PMessageType(frame) == P2PMessageType.RelayRequest, "GetP2PMessageType reads type");
        }

        static void ShortDataGuard()
        {
            TestHarness.Section("short data guard");
            TestHarness.Throws<ArgumentException>(() => P2PCoder.GetP2PMessageType(new byte[3]), "too short throws");
            TestHarness.Throws<ArgumentException>(() => P2PCoder.GetP2PMessageType(null), "null throws");
            TestHarness.Throws<ArgumentException>(() => BaseCoder.GetLength(new byte[2]), "BaseCoder.GetLength too short throws");
        }

        static void FrameHeaderLayout()
        {
            TestHarness.Section("frame header layout");
            var coder = new P2PCoder();
            var content = Encoding.UTF8.GetBytes("hello");
            var frame = coder.EncodeP2P(P2PMessageType.NodeList, content);

            TestHarness.Expect(frame.Length == BaseCoder.P_Head + content.Length, "frame length = head + content", frame.Length.ToString());
            TestHarness.Expect(frame[BaseCoder.P_LEN] == (byte)P2PMessageType.NodeList, "type byte located at P_LEN");
            TestHarness.Expect(BaseCoder.GetLength(frame) == content.Length, "length prefix equals content length");
        }

        static void UnicodeRoundTrip()
        {
            TestHarness.Section("unicode round trip");
            var coder = new P2PCoder();
            var text = "你好，P2P 世界！émoji: 🚀";
            var decoded = coder.DecodeP2P(coder.EncodeP2P(P2PMessageType.UserData, text));
            TestHarness.Expect(decoded.Count == 1 && decoded[0].GetContentAsString() == text, "unicode content preserved");
        }

        static void LargePayload()
        {
            TestHarness.Section("large payload (>=4KB pooled path)");
            var coder = new P2PCoder();
            var content = new byte[100_000];
            new Random(42).NextBytes(content);

            var decoded = coder.DecodeP2P(coder.EncodeP2P(P2PMessageType.UserData, content));

            TestHarness.Expect(decoded.Count == 1, "large message decoded");
            TestHarness.Expect(decoded.Count == 1 && decoded[0].Content != null && decoded[0].Content.Length == content.Length,
                "large payload exact length", decoded.Count == 1 && decoded[0].Content != null ? decoded[0].Content.Length.ToString() : "n/a");
            TestHarness.Expect(decoded.Count == 1 && decoded[0].Content != null && decoded[0].Content.SequenceEqual(content), "large payload byte-exact");
        }
    }
}