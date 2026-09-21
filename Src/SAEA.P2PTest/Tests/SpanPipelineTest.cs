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

            w.Dispose();
            w.Dispose(); // idempotent
            TestHarness.Expect(true, "PooledBufferWriter.Dispose idempotent");
        }
    }
}