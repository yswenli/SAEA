using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SAEA.Common.Caching;
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
            MultiSegmentSequence();
            Parity();

            TestHarness.WriteSummary("StreamDecoderTest");
        }

        internal static byte[] BuildFrame(byte type, byte[] content)
        {
            using (var w = new PooledBufferWriter(64))
            {
                new BaseSocketProtocal(type, content ?? Array.Empty<byte>()).WriteTo(w);
                return w.WrittenSpan.ToArray();
            }
        }

        static void PartialFrame()
        {
            TestHarness.Section("partial frame");
            var coder = new BaseCoder();
            var frame = BuildFrame((byte)SocketProtocalType.RequestSend, Encoding.UTF8.GetBytes("partial"));

            for (int i = 0; i < frame.Length - 1; i++)
            {
                using (var r = coder.Decode(new ReadOnlySequence<byte>(new[] { frame[i] })))
                {
                    TestHarness.Expect(r.Count == 0, $"byte {i} yields no frame");
                }
            }

            using (var last = coder.Decode(new ReadOnlySequence<byte>(new[] { frame[frame.Length - 1] })))
            {
                TestHarness.Expect(last.Count == 1, "final byte completes the frame");
                TestHarness.Expect(last.Count == 1 && last[0].Content.Span.SequenceEqual(Encoding.UTF8.GetBytes("partial")), "payload intact");
            }
        }

        static void FragmentedAcrossAppends()
        {
            TestHarness.Section("fragmented across appends");
            var coder = new BaseCoder();
            var content = new byte[1000];
            new Random(1).NextBytes(content);
            var frame = BuildFrame((byte)SocketProtocalType.RequestSend, content);

            using (var first = coder.Decode(new ReadOnlySequence<byte>(frame.AsSpan(0, 300).ToArray())))
            {
                TestHarness.Expect(first.Count == 0, "first fragment buffered");
            }

            using (var second = coder.Decode(new ReadOnlySequence<byte>(frame.AsSpan(300, 400).ToArray())))
            {
                TestHarness.Expect(second.Count == 0, "second fragment buffered");
            }

            using (var third = coder.Decode(new ReadOnlySequence<byte>(frame.AsSpan(700).ToArray())))
            {
                TestHarness.Expect(third.Count == 1, "third fragment completes");
                TestHarness.Expect(third.Count == 1 && third[0].Content.Span.SequenceEqual(content), "fragmented payload byte-exact");
            }
        }

        static void MultipleFramesInOneAppend()
        {
            TestHarness.Section("multiple frames in one append");
            var coder = new BaseCoder();
            var all = new List<byte>();
            all.AddRange(BuildFrame((byte)SocketProtocalType.RequestSend, Encoding.UTF8.GetBytes("one")));
            all.AddRange(BuildFrame((byte)SocketProtocalType.ChatMessage, Encoding.UTF8.GetBytes("two")));
            all.AddRange(BuildFrame((byte)SocketProtocalType.AllowReceive, Encoding.UTF8.GetBytes("three")));

            using (var decoded = coder.Decode(new ReadOnlySequence<byte>(all.ToArray())))
            {
                TestHarness.Expect(decoded.Count == 3, "three frames decoded", decoded.Count.ToString());
                TestHarness.Expect(decoded[0].Content.Span.SequenceEqual(Encoding.UTF8.GetBytes("one")), "frame 1 content");
                TestHarness.Expect(decoded[1].Content.Span.SequenceEqual(Encoding.UTF8.GetBytes("two")), "frame 2 content");
                TestHarness.Expect(decoded[2].Content.Span.SequenceEqual(Encoding.UTF8.GetBytes("three")), "frame 3 content");
            }
        }

        static void Heartbeat()
        {
            TestHarness.Section("heartbeat");
            var coder = new BaseCoder();
            var frame = BuildFrame((byte)SocketProtocalType.Heart, null);
            int heartCount = 0;

            using (var decoded = coder.Decode(new ReadOnlySequence<byte>(frame), _ => heartCount++))
            {
                TestHarness.Expect(decoded.Count == 0, "heartbeat produces no frame");
            }
            TestHarness.Expect(heartCount == 1, "heartbeat callback fired");
        }

        static void EmptyBody()
        {
            TestHarness.Section("empty body");
            var coder = new BaseCoder();
            var frame = BuildFrame((byte)SocketProtocalType.RequestSend, null);

            using (var decoded = coder.Decode(new ReadOnlySequence<byte>(frame)))
            {
                TestHarness.Expect(decoded.Count == 1, "empty body frame decoded");
                TestHarness.Expect(decoded[0].Content.Length == 0, "empty content is non-null empty");
                TestHarness.Expect(decoded[0].BodyLength == 0, "empty body length is 0");
            }
        }

        static void BigData()
        {
            TestHarness.Section("big data");
            var coder = new BaseCoder();
            var content = Encoding.UTF8.GetBytes("file-payload");
            var frame = BuildFrame((byte)SocketProtocalType.BigData, content);
            byte[] fileContent = null;

            using (var decoded = coder.Decode(new ReadOnlySequence<byte>(frame), null, f => fileContent = f.ToArray()))
            {
                TestHarness.Expect(decoded.Count == 0, "big data produces no frame");
            }
            TestHarness.Expect(fileContent != null && fileContent.SequenceEqual(content), "file callback content");
        }

        static void LargeFrame()
        {
            TestHarness.Section("1MB frame");
            var coder = new BaseCoder();
            var content = new byte[1024 * 1024];
            new Random(9).NextBytes(content);
            using (var decoded = coder.Decode(new ReadOnlySequence<byte>(BuildFrame((byte)SocketProtocalType.RequestSend, content))))
            {
                TestHarness.Expect(decoded.Count == 1 && decoded[0].Content.Span.SequenceEqual(content),
                    "1MB payload byte-exact");
            }
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
                TestHarness.Throws<KernelException>(() => coder.Decode(new ReadOnlySequence<byte>(negative)), "negative length throws");

                var tooBig = new byte[BaseCoder.P_Head];
                BitConverter.GetBytes(2048L).CopyTo(tooBig, 0);
                tooBig[BaseCoder.P_LEN] = (byte)SocketProtocalType.RequestSend;
                TestHarness.Throws<KernelException>(() => coder.Decode(new ReadOnlySequence<byte>(tooBig)), "length over MaxFrameLength throws");
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

        static void MultiSegmentSequence()
        {
            TestHarness.Section("multi-segment ReadOnlySequence");

            var payload1 = Encoding.UTF8.GetBytes("segment-one");
            var payload2 = Encoding.UTF8.GetBytes("segment-two");
            var all = new List<byte>();
            all.AddRange(BuildFrame((byte)SocketProtocalType.RequestSend, payload1));
            all.AddRange(BuildFrame((byte)SocketProtocalType.ChatMessage, payload2));

            var array = all.ToArray();
            var segment = new Segment(new ReadOnlyMemory<byte>(array, 0, 7));
            segment.Append(new ReadOnlyMemory<byte>(array, 7, array.Length - 7));
            var sequence = new ReadOnlySequence<byte>(segment, 0, segment.Next, array.Length - 7);

            using (var decoded = new BaseCoder().Decode(sequence))
            {
                TestHarness.Expect(decoded.Count == 2, "two frames from multi-segment sequence", decoded.Count.ToString());
                TestHarness.Expect(decoded.Count == 2 && decoded[0].Content.Span.SequenceEqual(payload1), "first segment payload");
                TestHarness.Expect(decoded.Count == 2 && decoded[1].Content.Span.SequenceEqual(payload2), "second segment payload");
            }
        }

        sealed class Segment : ReadOnlySequenceSegment<byte>
        {
            public Segment(ReadOnlyMemory<byte> memory)
            {
                Memory = memory;
            }

            public Segment Append(ReadOnlyMemory<byte> memory)
            {
                var next = new Segment(memory) { RunningIndex = RunningIndex + Memory.Length };
                Next = next;
                return next;
            }
        }

        public static async System.Threading.Tasks.Task RunIocpClientAsync()
        {
            TestHarness.Section("IOCP client span path");

            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;

            var option = SAEA.Sockets.SocketOptionBuilder.Instance
                .UseIocp()
                .SetIP("127.0.0.1")
                .SetPort(port)
                .SetReadBufferSize(8192)
                .Build();

            var client = new SAEA.Sockets.Core.Tcp.IocpClientSocket(option);
            byte[] spanData = null;
            byte[] legacyData = null;
            client.OnClientReceiveSpan += span => spanData = span.ToArray();
            client.OnReceive += data => legacyData = data;

            client.ConnectAsync();
            var accepted = await listener.AcceptTcpClientAsync();
            var frame = BuildFrame((byte)SocketProtocalType.RequestSend, Encoding.UTF8.GetBytes("span-hello"));
            await accepted.GetStream().WriteAsync(frame, 0, frame.Length);
            await accepted.GetStream().FlushAsync();

            await TestHarness.WaitUntil(() => spanData != null, 3000);
            TestHarness.Expect(spanData != null && spanData.SequenceEqual(frame), "client span event receives frame bytes");
            TestHarness.Expect(legacyData != null && legacyData.SequenceEqual(frame), "client OnReceive still delivers bytes");

            try { client.Dispose(); } catch { }
            try { accepted.Close(); } catch { }
            listener.Stop();
        }

        public static async System.Threading.Tasks.Task RunIocpServerAsync()
        {
            TestHarness.Section("IOCP server span path");

            int port = TestHarness.GetFreeTcpPort();
            var option = SAEA.Sockets.SocketOptionBuilder.Instance
                .UseIocp()
                .SetIP("127.0.0.1")
                .SetPort(port)
                .SetReadBufferSize(8192)
                .Build();

            var server = new SAEA.Sockets.Core.Tcp.IocpServerSocket(option);
            byte[] spanData = null;
            server.OnServerReceiveSpan += (token, span) => spanData = span.ToArray();
            server.Start();
            await System.Threading.Tasks.Task.Delay(200);

            using (var tcp = new System.Net.Sockets.TcpClient())
            {
                await tcp.ConnectAsync(System.Net.IPAddress.Loopback, port);
                var frame = BuildFrame((byte)SocketProtocalType.RequestSend, Encoding.UTF8.GetBytes("server-span"));
                await tcp.GetStream().WriteAsync(frame, 0, frame.Length);
                await tcp.GetStream().FlushAsync();

                await TestHarness.WaitUntil(() => spanData != null, 3000);
                TestHarness.Expect(spanData != null && spanData.SequenceEqual(frame), "server span event receives frame bytes");
            }

            try { server.Stop(); } catch { }
            try { server.Dispose(); } catch { }
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
                using (var actual = new BaseCoder().Decode(new ReadOnlySequence<byte>(frame), _ => newHearts++, f => newFiles.Add(f.ToArray())))
                {
                    TestHarness.Expect(legacy.Count == actual.Count, "parity frame count");
                    for (int i = 0; i < Math.Min(legacy.Count, actual.Count); i++)
                    {
                        TestHarness.Expect(legacy[i].BodyLength == actual[i].BodyLength, "parity body length");
                        TestHarness.Expect(legacy[i].Type == actual[i].Type, "parity type");
                        TestHarness.Expect(legacy[i].Content.Span.SequenceEqual(actual[i].Content.Span), "parity content");
                    }
                }
                TestHarness.Expect(legacyHearts == newHearts, "parity heartbeat count");
                TestHarness.Expect(legacyFiles.Count == newFiles.Count, "parity file count");
            }
        }
    }
}