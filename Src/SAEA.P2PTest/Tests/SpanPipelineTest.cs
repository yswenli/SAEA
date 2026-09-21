using System;
using System.Buffers;
using SAEA.Common.Caching;

namespace SAEA.P2PTest.Tests
{
    public static class SpanPipelineTest
    {
        public static void Run()
        {
            TestHarness.Section("SpanPipelineTest");

            var w = new PooledBufferWriter(8);
            var span = w.GetSpan(4);
            span[0] = 1; span[1] = 2;
            w.Advance(2);
            TestHarness.Expect(w.WrittenCount == 2, "PooledBufferWriter.Advance tracks WrittenCount");
            TestHarness.Expect(w.WrittenSpan[0] == 1 && w.WrittenSpan[1] == 2, "PooledBufferWriter.WrittenSpan content");

            var more = w.GetSpan(20);
            for (int i = 0; i < 20; i++) more[i] = (byte)(i + 3);
            w.Advance(20);
            TestHarness.Expect(w.WrittenCount == 22, "PooledBufferWriter grows past initial capacity");
            TestHarness.Expect(w.WrittenSpan[0] == 1 && w.WrittenSpan[1] == 2 && w.WrittenSpan[21] == 22,
                "PooledBufferWriter preserves prefix after growth");

            TestHarness.Expect(w.TryGetArray(out var seg) && seg.Offset == 0 && seg.Count == 22,
                "PooledBufferWriter.TryGetArray exposes exact written range");

            w.Clear();
            TestHarness.Expect(w.WrittenCount == 0, "PooledBufferWriter.Clear resets count");

            var p = new SAEA.Sockets.Base.BaseSocketProtocal(7, new byte[] { 10, 20, 30 });
            TestHarness.Expect(p.BodyLength == 3 && p.Type == 7, "BaseSocketProtocal ctor derives BodyLength/Type");
            TestHarness.Expect(p.Content.Length == 3 && p.Content.Span[1] == 20, "BaseSocketProtocal Content is ReadOnlyMemory");

            using (var pw = new PooledBufferWriter(16))
            {
                p.WriteTo(pw);
                TestHarness.Expect(pw.WrittenCount == 9 + 3, "BaseSocketProtocal.WriteTo writes 9-byte header + body");
                TestHarness.Expect(pw.WrittenSpan[8] == 7, "BaseSocketProtocal.WriteTo writes Type at offset 8");
                TestHarness.Expect(pw.WrittenSpan[9] == 10 && pw.WrittenSpan[11] == 30, "BaseSocketProtocal.WriteTo writes body");
            }

            var empty = new SAEA.Sockets.Base.BaseSocketProtocal((byte)1, ReadOnlyMemory<byte>.Empty);
            TestHarness.Expect(empty.Content.Length == 0, "BaseSocketProtocal empty body is non-null (length 0)");

            var parsed = SAEA.Sockets.Base.BaseSocketProtocal.Parse(new byte[] { 1, 2 }, SAEA.Sockets.Model.SocketProtocalType.ChatMessage);
            TestHarness.Expect(parsed.BodyLength == 2 && parsed.Type == (byte)SAEA.Sockets.Model.SocketProtocalType.ChatMessage,
                "BaseSocketProtocal.Parse preserves body length + type");

            w.Dispose();
            w.Dispose(); // idempotent
            TestHarness.Expect(true, "PooledBufferWriter.Dispose idempotent");
        }
    }
}