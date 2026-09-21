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
                TestHarness.Expect(System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(pw.WrittenSpan) == 3L
                        && pw.WrittenSpan.Slice(9, 3).SequenceEqual(new byte[] { 10, 20, 30 }),
                    "BaseSocketProtocal.WriteTo writes 8-byte little-endian length and full body");
            }

            var empty = new SAEA.Sockets.Base.BaseSocketProtocal((byte)1, ReadOnlyMemory<byte>.Empty);
            TestHarness.Expect(empty.Content.Length == 0, "BaseSocketProtocal empty body is non-null (length 0)");

            var parsed = SAEA.Sockets.Base.BaseSocketProtocal.Parse(new byte[] { 1, 2 }, SAEA.Sockets.Model.SocketProtocalType.ChatMessage);
            TestHarness.Expect(parsed.BodyLength == 2 && parsed.Type == (byte)SAEA.Sockets.Model.SocketProtocalType.ChatMessage,
                "BaseSocketProtocal.Parse preserves body length + type");

            var batch = new SAEA.Sockets.Base.DecodedFrames(2);
            batch.Add(new SAEA.Sockets.Base.BaseSocketProtocal(1, new ReadOnlyMemory<byte>(new byte[] { 99 })));
            TestHarness.Expect(batch.Count == 1 && batch[0].Content.Span[0] == 99, "DecodedFrames indexer + Count");
            TestHarness.Expect(batch.Frames.Length == 1, "DecodedFrames.Frames span");
            batch.Dispose();
            batch.Dispose();
            var threw = false;
            try { var _ = batch.Count; } catch (ObjectDisposedException) { threw = true; }
            TestHarness.Expect(threw, "DecodedFrames throws after Dispose");

            // Dispose 必须先释放 IDisposable 帧，再归还池化背衬（否则帧 Dispose 可能读到已归还的缓冲）
            var disposable = new DisposableFrame();
            var batch2 = new SAEA.Sockets.Base.DecodedFrames(1);
            batch2.Add(disposable);
            batch2.Dispose();
            TestHarness.Expect(disposable.Disposed, "DecodedFrames.Dispose disposes IDisposable frames");

            w.Dispose();
            w.Dispose(); // idempotent
            TestHarness.Expect(true, "PooledBufferWriter.Dispose idempotent");

            // 线格式：8B 小端长度 + 1B type + body
            byte[] Frame(long len, byte type, byte[] body)
            {
                var buf = new byte[9 + body.Length];
                BitConverter.GetBytes(len).CopyTo(buf, 0);
                buf[8] = type;
                body.CopyTo(buf, 9);
                return buf;
            }

            var coder = new SAEA.Sockets.Base.BaseCoder();

            // 半包：先喂 5 字节，不应产出，再喂剩余
            var full = Frame(3, 2, new byte[] { 1, 2, 3 });
            var half = coder.Decode(new ReadOnlySequence<byte>(full, 0, 5));
            TestHarness.Expect(half.Count == 0, "BaseCoder.Decode buffers partial frame");
            half.Dispose();
            using (var rest = coder.Decode(new ReadOnlySequence<byte>(full, 5, full.Length - 5)))
            {
                TestHarness.Expect(rest.Count == 1, "BaseCoder.Decode emits frame after completion");
                TestHarness.Expect(rest[0].BodyLength == 3 && rest[0].Type == 2, "BaseCoder.Decode frame header");
                TestHarness.Expect(rest[0].Content.Span[0] == 1 && rest[0].Content.Span[2] == 3, "BaseCoder.Decode frame body");
            }

            // 心跳：bodyLen=0 type=Heart → 回调、不产出
            DateTime? heart = null;
            using (var hb = coder.Decode(new ReadOnlySequence<byte>(Frame(0, (byte)SAEA.Sockets.Model.SocketProtocalType.Heart, new byte[0])),
                       t => heart = t))
            {
                TestHarness.Expect(hb.Count == 0 && heart.HasValue, "BaseCoder.Decode heart consumed, no frame");
            }

            // BigData → onFile，不产出；span 有效
            byte[]? fileCopy = null;
            using (var big = coder.Decode(new ReadOnlySequence<byte>(Frame(2, (byte)SAEA.Sockets.Model.SocketProtocalType.BigData, new byte[] { 8, 9 })),
                       null, m => fileCopy = m.ToArray()))
            {
                TestHarness.Expect(big.Count == 0 && fileCopy != null && fileCopy[0] == 8, "BaseCoder.Decode BigData onFile, no frame");
            }

            // 非法长度 → KernelException
            var bad = new byte[9];
            BitConverter.GetBytes((long)-1).CopyTo(bad, 0);
            TestHarness.Throws<SAEA.Sockets.Model.KernelException>(
                () => { using (coder.Decode(new ReadOnlySequence<byte>(bad))) { } },
                "BaseCoder.Decode rejects negative length");
            coder.Clear(); // 丢弃被拒帧，避免污染后续流式解码（_decoder 已缓存该非法帧）

            // DecodeStream 零拷贝
            var probe = new FrameProbe();
            coder.DecodeStream(full, probe);
            TestHarness.Expect(probe.Count == 1 && probe.LastLength == 3, "BaseCoder.DecodeStream invokes handler per frame");
        }
    }

    internal sealed class FrameProbe : SAEA.Sockets.Interface.IFrameHandler
    {
        public int Count;
        public int LastLength;
        public void OnFrame(in SAEA.Sockets.Base.SocketFrame frame)
        {
            Count++;
            LastLength = frame.Content.Length;
        }
    }

    internal sealed class DisposableFrame : SAEA.Sockets.Interface.ISocketProtocal, IDisposable
    {
        public long BodyLength => 0;
        public byte Type => 0;
        public ReadOnlyMemory<byte> Content => ReadOnlyMemory<byte>.Empty;
        public void WriteTo(System.Buffers.IBufferWriter<byte> writer) { }
        public bool Disposed;
        public void Dispose() { Disposed = true; }
    }
}