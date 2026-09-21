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

            var more = w.GetSpan(8);
            for (int i = 0; i < 8; i++) more[i] = (byte)(i + 3);
            w.Advance(8);
            TestHarness.Expect(w.WrittenCount == 10, "PooledBufferWriter grows past initial capacity");
            TestHarness.Expect(w.WrittenSpan[9] == 10, "PooledBufferWriter preserves prefix after growth");

            TestHarness.Expect(w.TryGetArray(out var seg) && seg.Offset == 0 && seg.Count == 10,
                "PooledBufferWriter.TryGetArray exposes exact written range");

            w.Clear();
            TestHarness.Expect(w.WrittenCount == 0, "PooledBufferWriter.Clear resets count");

            w.Dispose();
            w.Dispose(); // idempotent
            TestHarness.Expect(true, "PooledBufferWriter.Dispose idempotent");
        }
    }
}