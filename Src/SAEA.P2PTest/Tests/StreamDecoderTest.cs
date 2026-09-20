using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SAEA.Sockets.Base;
using SAEA.Sockets.Model;

namespace SAEA.P2PTest.Tests
{
    /// <summary>
    /// 流式零拷贝解码测试：行为特征、新旧对比、边界与 IOCP Span 路径。
    /// </summary>
    public static class StreamDecoderTest
    {
        public static void Run()
        {
            TestHarness.Section("StreamDecoderTest");

            PartialFrame();
            FragmentedAcrossAppends();
            MultipleFramesInOneAppend();
            Heartbeat();
            EmptyBody();
            BigData();
            LargeFrame();
            MalformedLength();
            ZeroCopyStream();
            Parity();

            TestHarness.WriteSummary("StreamDecoderTest");
        }

        internal static byte[] BuildFrame(byte type, byte[] content)
        {
            return new BaseSocketProtocal
            {
                BodyLength = content == null ? 0 : content.Length,
                Type = type,
                Content = content
            }.ToBytes();
        }

        static void PartialFrame()
        {
            TestHarness.Section("partial frame");
            var coder = new BaseCoder();
            var frame = BuildFrame((byte)SocketProtocalType.RequestSend, Encoding.UTF8.GetBytes("partial"));

            for (int i = 0; i < frame.Length - 1; i++)
            {
                var r = coder.Decode(new[] { frame[i] });
                TestHarness.Expect(r.Count == 0, $"byte {i} yields no frame");
            }

            var last = coder.Decode(new[] { frame[frame.Length - 1] });
            TestHarness.Expect(last.Count == 1, "final byte completes the frame");
            TestHarness.Expect(last.Count == 1 && last[0].Content.SequenceEqual(Encoding.UTF8.GetBytes("partial")), "payload intact");
        }

        static void FragmentedAcrossAppends()
        {
            TestHarness.Section("fragmented across appends");
            var coder = new BaseCoder();
            var content = new byte[1000];
            new Random(1).NextBytes(content);
            var frame = BuildFrame((byte)SocketProtocalType.RequestSend, content);

            var first = coder.Decode(frame.AsSpan(0, 300).ToArray());
            TestHarness.Expect(first.Count == 0, "first fragment buffered");

            var second = coder.Decode(frame.AsSpan(300, 400).ToArray());
            TestHarness.Expect(second.Count == 0, "second fragment buffered");

            var third = coder.Decode(frame.AsSpan(700).ToArray());
            TestHarness.Expect(third.Count == 1, "third fragment completes");
            TestHarness.Expect(third.Count == 1 && third[0].Content.SequenceEqual(content), "fragmented payload byte-exact");
        }

        static void MultipleFramesInOneAppend()
        {
            TestHarness.Section("multiple frames in one append");
            var coder = new BaseCoder();
            var all = new List<byte>();
            all.AddRange(BuildFrame((byte)SocketProtocalType.RequestSend, Encoding.UTF8.GetBytes("one")));
            all.AddRange(BuildFrame((byte)SocketProtocalType.ChatMessage, Encoding.UTF8.GetBytes("two")));
            all.AddRange(BuildFrame((byte)SocketProtocalType.AllowReceive, Encoding.UTF8.GetBytes("three")));

            var decoded = coder.Decode(all.ToArray());
            TestHarness.Expect(decoded.Count == 3, "three frames decoded", decoded.Count.ToString());
            TestHarness.Expect(decoded[0].Content.SequenceEqual(Encoding.UTF8.GetBytes("one")), "frame 1 content");
            TestHarness.Expect(decoded[1].Content.SequenceEqual(Encoding.UTF8.GetBytes("two")), "frame 2 content");
            TestHarness.Expect(decoded[2].Content.SequenceEqual(Encoding.UTF8.GetBytes("three")), "frame 3 content");
        }

        static void Heartbeat()
        {
            TestHarness.Section("heartbeat");
            var coder = new BaseCoder();
            var frame = BuildFrame((byte)SocketProtocalType.Heart, null);
            int heartCount = 0;

            var decoded = coder.Decode(frame, _ => heartCount++);
            TestHarness.Expect(decoded.Count == 0, "heartbeat produces no frame");
            TestHarness.Expect(heartCount == 1, "heartbeat callback fired");
        }

        static void EmptyBody()
        {
            TestHarness.Section("empty body");
            var coder = new BaseCoder();
            var frame = BuildFrame((byte)SocketProtocalType.RequestSend, null);
            var decoded = coder.Decode(frame);

            TestHarness.Expect(decoded.Count == 1, "empty body frame decoded");
            TestHarness.Expect(decoded[0].Content != null && decoded[0].Content.Length == 0, "empty content is non-null empty");
            TestHarness.Expect(decoded[0].BodyLength == 0, "empty body length is 0");
        }

        static void BigData()
        {
            TestHarness.Section("big data");
            var coder = new BaseCoder();
            var content = Encoding.UTF8.GetBytes("file-payload");
            var frame = BuildFrame((byte)SocketProtocalType.BigData, content);
            byte[] fileContent = null;

            var decoded = coder.Decode(frame, null, f => fileContent = f);
            TestHarness.Expect(decoded.Count == 0, "big data produces no frame");
            TestHarness.Expect(fileContent != null && fileContent.SequenceEqual(content), "file callback content");
        }

        static void LargeFrame()
        {
            TestHarness.Section("1MB frame");
            var coder = new BaseCoder();
            var content = new byte[1024 * 1024];
            new Random(9).NextBytes(content);
            var decoded = coder.Decode(BuildFrame((byte)SocketProtocalType.RequestSend, content));

            TestHarness.Expect(decoded.Count == 1 && decoded[0].Content != null && decoded[0].Content.SequenceEqual(content),
                "1MB payload byte-exact");
        }

        static void MalformedLength()
        {
            TestHarness.Section("malformed length");
            var original = BaseCoder.MaxFrameLength;
            try
            {
                BaseCoder.MaxFrameLength = 1024;

                var coder = new BaseCoder();

                var negative = new byte[BaseCoder.P_Head];
                BitConverter.GetBytes(-1L).CopyTo(negative, 0);
                negative[BaseCoder.P_LEN] = (byte)SocketProtocalType.RequestSend;
                TestHarness.Throws<KernelException>(() => coder.Decode(negative), "negative length throws");

                var tooBig = new byte[BaseCoder.P_Head];
                BitConverter.GetBytes(2048L).CopyTo(tooBig, 0);
                tooBig[BaseCoder.P_LEN] = (byte)SocketProtocalType.RequestSend;
                TestHarness.Throws<KernelException>(() => coder.Decode(tooBig), "length over MaxFrameLength throws");
            }
            finally
            {
                BaseCoder.MaxFrameLength = original;
            }
        }

        static void ZeroCopyStream()
        {
            TestHarness.Section("zero-copy stream");

            var coder = new BaseCoder();
            var payload = Encoding.UTF8.GetBytes("zero-copy-payload");
            var frame = BuildFrame((byte)SocketProtocalType.RequestSend, payload);

            var handler = new CapturingHandler();
            coder.DecodeStream(frame.AsSpan(0, 5), handler);
            TestHarness.Expect(handler.Frames == 0, "partial stream input buffered");

            coder.DecodeStream(frame.AsSpan(5), handler);
            TestHarness.Expect(handler.Frames == 1, "stream frame delivered");
            TestHarness.Expect(handler.LastCopied != null && handler.LastCopied.SequenceEqual(payload), "stream payload byte-exact");
            TestHarness.Expect(handler.LastType == (byte)SocketProtocalType.RequestSend, "stream frame type");
        }

        sealed class CapturingHandler : SAEA.Sockets.Interface.IFrameHandler
        {
            public int Frames;
            public byte[] LastCopied;
            public byte LastType;

            public void OnFrame(in SocketFrame frame)
            {
                LastCopied = frame.Content.ToArray();
                LastType = frame.Type;
                Frames++;
            }
        }

        static void Parity()
        {
            TestHarness.Section("legacy parity matrix");
            var cases = new List<byte[]>();
            cases.Add(BuildFrame((byte)SocketProtocalType.RequestSend, Encoding.UTF8.GetBytes("a")));
            cases.Add(BuildFrame((byte)SocketProtocalType.RequestSend, null));
            cases.Add(BuildFrame((byte)SocketProtocalType.BigData, Encoding.UTF8.GetBytes("big")));
            cases.Add(BuildFrame((byte)SocketProtocalType.Heart, null));

            foreach (var frame in cases)
            {
                int legacyHearts = 0, newHearts = 0;
                var legacyFiles = new List<byte[]>();
                var newFiles = new List<byte[]>();

                var legacy = LegacyDecoder.Decode(frame, _ => legacyHearts++, f => legacyFiles.Add(f));
                var actual = new BaseCoder().Decode(frame, _ => newHearts++, f => newFiles.Add(f));

                TestHarness.Expect(legacy.Count == actual.Count, "parity frame count");
                for (int i = 0; i < Math.Min(legacy.Count, actual.Count); i++)
                {
                    TestHarness.Expect(legacy[i].BodyLength == actual[i].BodyLength, "parity body length");
                    TestHarness.Expect(legacy[i].Type == actual[i].Type, "parity type");
                    TestHarness.Expect(legacy[i].Content.SequenceEqual(actual[i].Content), "parity content");
                }
                TestHarness.Expect(legacyHearts == newHearts, "parity heartbeat count");
                TestHarness.Expect(legacyFiles.Count == newFiles.Count, "parity file count");
            }
        }
    }
}