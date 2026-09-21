# Span/Memory Pipeline — Plan 1: Protocol/Codec Core + Solution Compile Restoration

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the `byte[]` protocol/codec public surface with `ReadOnlyMemory`/`ReadOnlySequence`/`IBufferWriter`, add pooled batch decoding (`DecodedFrames`) and the `IFrameCoder` split, and adapt every implementer/caller in the solution so `Src/SAEA.Sockets.sln` compiles green with all 283 tests (255 existing + 28 new) passing.

**Architecture:** `PooledBufferWriter` (new, `SAEA.Common`) is the single write primitive. `ISocketProtocal` becomes read-only + `WriteTo(IBufferWriter<byte>)`. `ICoder` becomes `Encode(ISocketProtocal, IBufferWriter<byte>)` + stateful `Decode(ReadOnlySequence<byte>) -> DecodedFrames` + `Clear()`. Frame-aware coders additionally implement `IFrameCoder.DecodeStream`. `BaseCoder` keeps its existing `FrameDecoder`; BigData output becomes a span (no `byte[]`). The socket layer (`IClientSocket`/`IServerSocket`) is **untouched** in Plan 1 — it still uses `byte[]`; a later plan (Plan 2) modernizes it.

**Tech Stack:** C# 8, `netstandard2.0`, `System.Memory 4.6.3`, `System.IO.Pipelines 10.0.6`, `ArrayPool<byte>` via `SAEA.Common.Caching.MemoryPoolManager`, console test harness in `SAEA.P2PTest`.

**Spec:** `docs/superpowers/specs/2026-09-21-span-memory-pipeline-design.md` (rev.3 + corrections ⑯⑰). This plan implements spec §3, §4, §6 (codec part), §7 (codec/caller part), and stages §12.1–12.3.

---

## Scope Check

The spec covers two dependency-atomic subsystems:

1. **Protocol/codec public surface** (`ISocketProtocal`/`ICoder`/`IFrameCoder`/`BaseCoder`/`P2PCoder` + all in-solution coders and their callers). Changing these breaks every project; the only "green build" boundary that includes all of them is "adapt the whole solution".
2. **Socket layer** (`IClientSocket`/`IServerSocket`/Handlers/`IUserToken`/IOCP/Stream/UDP/Shortcut). Changing these breaks the Shortcut, P2P, MQTT, DNS and test projects.

Each subsystem is one plan. **This is Plan 1 (codec core).** Plan 2 (socket layer + PipeReader stream + benchmark + docs/version) will be authored after Plan 1 is executed and green.

**Known intentional double-churn:** `byte[]` send call sites (e.g. `msg.ToBytes()` → `SendAsync(byte[])`) are adapted mechanically in Plan 1 to keep `IClientSocket.SendAsync(byte[])` compiling, then rewritten to `SendAsync(ISocketProtocal)` in Plan 2. This matches spec §12 ordering.

---

## File Structure

**New**
- `Src/SAEA.Common/Caching/PooledBufferWriter.cs` — `IBufferWriter<byte>` + `IDisposable`, pooled, `TryGetArray`.
- `Src/SAEA.Sockets/Base/DecodedFrames.cs` — `IDisposable` frame batch + internal `FramesCollector`.
- `Src/SAEA.Sockets/Interface/IFrameCoder.cs` — `IFrameCoder` + `FileSpanHandler` delegate.
- `Src/SAEA.P2PTest/Tests/SpanPipelineTest.cs` — new tests, registered in `Program.RunAllAsync`.

**Rewritten**
- `Src/SAEA.Sockets/Interface/ISocketProtocal.cs`, `Interface/ICoder.cs`.
- `Src/SAEA.Sockets/Base/BaseSocketProtocal.cs`, `Base/BaseCoder.cs`, `Base/FrameDecoder.cs`.
- `Src/SAEA.P2P/Protocol/P2PCoder.cs`, `Protocol/P2PProtocol.cs`.

**Adapted (compile-only unless noted)** — every `ICoder`/`ISocketProtocal` implementer and every `ToBytes()`/`Decode(byte[])`/object-initializer caller; full list in Task 8/9.

---

## Task 1: `PooledBufferWriter`

**Files:**
- Create: `Src/SAEA.Common/Caching/PooledBufferWriter.cs`
- Test: `Src/SAEA.P2PTest/Tests/SpanPipelineTest.cs` (create)
- Modify: `Src/SAEA.P2PTest/Program.cs` (register `SpanPipelineTest.Run()`)

- [ ] **Step 1: Write the failing test**

Create `Src/SAEA.P2PTest/Tests/SpanPipelineTest.cs`:

```csharp
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
```

- [ ] **Step 2: Register the test and run it to verify it fails**

In `Src/SAEA.P2PTest/Program.cs`, inside `RunAllAsync()` add after `EdgeCaseTest.Run();`:

```csharp
            SpanPipelineTest.Run();
```

Run: `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug`
Expected: build FAIL — `PooledBufferWriter` does not exist.

- [ ] **Step 3: Implement `PooledBufferWriter`**

Create `Src/SAEA.Common/Caching/PooledBufferWriter.cs`:

```csharp
using System;
using System.Buffers;

namespace SAEA.Common.Caching
{
    /// <summary>
    /// 池化写入器：数组背衬、可归还、可零拷贝直发（TryGetArray 供 SocketAsyncEventArgs.SetBuffer）。
    /// 非线程安全。注意：GetSpan/GetMemory 取得的 span 在 Dispose 后失效，Dispose 前不得让其外泄。
    /// </summary>
    public sealed class PooledBufferWriter : IBufferWriter<byte>, IDisposable
    {
        private byte[] _buffer;
        private int _requestedSize;
        private int _written;
        private bool _disposed;

        public PooledBufferWriter(int initialCapacity = 4096)
        {
            if (initialCapacity <= 0) initialCapacity = 4096;
            _requestedSize = initialCapacity;
            _buffer = MemoryPoolManager.Rent(initialCapacity);
        }

        public int WrittenCount
        {
            get { ThrowIfDisposed(); return _written; }
        }

        public ReadOnlySpan<byte> WrittenSpan
        {
            get { ThrowIfDisposed(); return _buffer.AsSpan(0, _written); }
        }

        public ReadOnlyMemory<byte> WrittenMemory
        {
            get { ThrowIfDisposed(); return _buffer.AsMemory(0, _written); }
        }

        public void Advance(int count)
        {
            ThrowIfDisposed();
            if (count < 0 || count > _buffer.Length - _written)
                throw new ArgumentOutOfRangeException(nameof(count));
            _written += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            Ensure(sizeHint);
            return _buffer.AsMemory(_written);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            Ensure(sizeHint);
            return _buffer.AsSpan(_written);
        }

        public bool TryGetArray(out ArraySegment<byte> segment)
        {
            ThrowIfDisposed();
            segment = new ArraySegment<byte>(_buffer, 0, _written);
            return true;
        }

        /// <summary>复位写入位置，不归还缓冲。</summary>
        public void Clear()
        {
            ThrowIfDisposed();
            _written = 0;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_buffer != null)
            {
                MemoryPoolManager.Return(_buffer, _requestedSize);
                _buffer = null;
            }
            _written = 0;
        }

        private void Ensure(int sizeHint)
        {
            ThrowIfDisposed();
            if (sizeHint <= 0) sizeHint = 1;

            long required = (long)_written + sizeHint;
            if (required <= _buffer.Length) return;
            if (required > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(sizeHint));

            var newSize = _buffer.Length;
            while (newSize < required)
            {
                if (newSize > int.MaxValue / 2) { newSize = (int)required; break; }
                newSize <<= 1;
            }

            var bigger = MemoryPoolManager.Rent(newSize);
            Buffer.BlockCopy(_buffer, 0, bigger, 0, _written);
            MemoryPoolManager.Return(_buffer, _requestedSize);
            _buffer = bigger;
            _requestedSize = newSize;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PooledBufferWriter));
        }
    }
}
```

> **Post-implementation correction (code-quality review of Task 1):** the growth loop `while (newSize < required) newSize <<= 1;` overflows `int` and never terminates for `required > 2^30`; guarded above. `Return(_buffer, _buffer.Length)` mis-tiers the pooled array (a 3000-byte request rents a 4096-byte array from Small but returns it to Medium); the `_requestedSize` field makes the return pool-exact. `Advance`/`Ensure` additions are overflow-checked.

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug -- --all`
Expected: `SpanPipelineTest` 7/7 PASS; overall count `262/262` (255 existing + 7).

- [ ] **Step 5: Commit**

```bash
git add Src/SAEA.Common/Caching/PooledBufferWriter.cs Src/SAEA.P2PTest/Tests/SpanPipelineTest.cs Src/SAEA.P2PTest/Program.cs
git commit -m "feat(common): add PooledBufferWriter with IBufferWriter + TryGetArray"
```

---

## Task 2: `ISocketProtocal` + `BaseSocketProtocal`

**Files:**
- Modify: `Src/SAEA.Sockets/Interface/ISocketProtocal.cs`
- Modify: `Src/SAEA.Sockets/Base/BaseSocketProtocal.cs`
- Test: `Src/SAEA.P2PTest/Tests/SpanPipelineTest.cs`

- [ ] **Step 1: Write the failing test**

Add to `SpanPipelineTest.Run()` before the final `w.Dispose()` block (keep `w` tests as-is; append after them):

```csharp
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
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug`
Expected: build FAIL — `BaseSocketProtocal` has no `(byte, ReadOnlyMemory<byte>)` ctor / `WriteTo`.

- [ ] **Step 3: Rewrite the interface**

Replace the body of `Src/SAEA.Sockets/Interface/ISocketProtocal.cs` (keep the file header, replace `using`/interface):

```csharp
using System;
using System.Buffers;

namespace SAEA.Sockets.Interface
{
    public interface ISocketProtocal
    {
        long BodyLength { get; }
        byte Type { get; }
        ReadOnlyMemory<byte> Content { get; }

        /// <summary>按线格式（8B 小端长度 + 1B Type + body）写入 writer。</summary>
        void WriteTo(IBufferWriter<byte> writer);
    }
}
```

- [ ] **Step 4: Rewrite `BaseSocketProtocal`**

Replace the class body in `Src/SAEA.Sockets/Base/BaseSocketProtocal.cs` (keep header; adjust `using` to `System`, `System.Buffers`, `System.Buffers.Binary`, `SAEA.Sockets.Interface`, `SAEA.Sockets.Model`):

```csharp
    /// <summary>
    /// 系统默认消息协议（非 sealed：P2PProtocol 等派生）。
    /// </summary>
    public class BaseSocketProtocal : ISocketProtocal
    {
        public long BodyLength { get; protected set; }
        public byte Type { get; protected set; }
        public ReadOnlyMemory<byte> Content { get; protected set; } = ReadOnlyMemory<byte>.Empty;

        public BaseSocketProtocal() { }

        public BaseSocketProtocal(byte type, ReadOnlyMemory<byte> content)
        {
            Type = type;
            Content = content;
            BodyLength = content.Length;
        }

        /// <summary>
        /// 注意：BodyLength 可大于 Content.Length（BigData 帧只携带分块），基类不做一致性校验。
        /// </summary>
        public BaseSocketProtocal(long bodyLength, byte type, ReadOnlyMemory<byte> content)
        {
            BodyLength = bodyLength;
            Type = type;
            Content = content;
        }

        public void WriteTo(IBufferWriter<byte> writer)
        {
            var span = writer.GetSpan(BaseCoder.P_Head);
            BinaryPrimitives.WriteInt64LittleEndian(span, BodyLength);
            span[BaseCoder.P_LEN] = Type;
            writer.Advance(BaseCoder.P_Head);
            if (!Content.IsEmpty)
                writer.Write(Content.Span);
        }

        public static BaseSocketProtocal Parse(byte[] data, SocketProtocalType type)
        {
            return Parse(data, (byte)type);
        }

        public static BaseSocketProtocal Parse(byte[] data, byte type)
        {
            var len = data == null ? 0 : data.Length;
            return new BaseSocketProtocal(len, type, len > 0 ? new ReadOnlyMemory<byte>(data) : ReadOnlyMemory<byte>.Empty);
        }

        public static BaseSocketProtocal ParseRequest(byte[] data)
        {
            return Parse(data, SocketProtocalType.RequestSend);
        }

        public static BaseSocketProtocal ParseStream(byte[] data)
        {
            var len = data == null ? 0 : data.Length;
            return new BaseSocketProtocal(len, (byte)SocketProtocalType.BigData, len > 0 ? new ReadOnlyMemory<byte>(data) : ReadOnlyMemory<byte>.Empty);
        }
    }
```

- [ ] **Step 5: Run to verify it passes**

The build will now fail in every `ToBytes()` caller — that is expected; Tasks 5–9 fix them. Verify only that the new symbols compile by building the two leaf projects that have no protocol callers yet:

Run: `dotnet build Src/SAEA.Sockets/SAEA.Sockets.csproj -c Debug`
Expected: FAIL with `ICoder`/`BaseCoder`/Shortcut errors (expected; do not fix here).

Proceed to Task 3; the end-to-end green run is asserted in Task 10.

- [ ] **Step 6: Commit**

```bash
git add Src/SAEA.Sockets/Interface/ISocketProtocal.cs Src/SAEA.Sockets/Base/BaseSocketProtocal.cs Src/SAEA.P2PTest/Tests/SpanPipelineTest.cs
git commit -m "feat(sockets): ISocketProtocal read-only + WriteTo; BaseSocketProtocal ctor/protected set"
```

---

## Task 3: `DecodedFrames` + `FramesCollector`

**Files:**
- Create: `Src/SAEA.Sockets/Base/DecodedFrames.cs`
- Test: `Src/SAEA.P2PTest/Tests/SpanPipelineTest.cs`

- [ ] **Step 1: Write the failing test**

Add to `SpanPipelineTest.Run()`:

```csharp
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
```

Add this helper class at the bottom of `SpanPipelineTest.cs` (inside the namespace), alongside `FrameProbe` (defined in Task 5):

```csharp
    internal sealed class DisposableFrame : SAEA.Sockets.Interface.ISocketProtocal, IDisposable
    {
        public long BodyLength => 0;
        public byte Type => 0;
        public ReadOnlyMemory<byte> Content => ReadOnlyMemory<byte>.Empty;
        public void WriteTo(System.Buffers.IBufferWriter<byte> writer) { }
        public bool Disposed;
        public void Dispose() { Disposed = true; }
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug`
Expected: build FAIL — `DecodedFrames` does not exist.

- [ ] **Step 3: Implement `DecodedFrames` + collector**

Create `Src/SAEA.Sockets/Base/DecodedFrames.cs`:

```csharp
using System;
using System.Buffers;
using System.Collections.Generic;
using SAEA.Common.Caching;
using SAEA.Sockets.Interface;

namespace SAEA.Sockets.Base
{
    /// <summary>
    /// 解码帧批次。Batch 模式由 <see cref="BaseCoder"/> 提供单块池化背衬；
    /// Borrowed 模式（如 WSCoder）由各帧自持内存。Dispose 归还背衬并释放实现 IDisposable 的帧。
    /// 无终结器：忘记 Dispose 将静默丢失池化缓冲。
    /// </summary>
    public sealed class DecodedFrames : IDisposable
    {
        private ISocketProtocal[] _frames;
        private int _count;
        private byte[] _pooledBuffer;
        private int _pooledRequestedSize;
        private bool _disposed;

        public DecodedFrames(int capacity = 4)
        {
            if (capacity <= 0) capacity = 4;
            _frames = new ISocketProtocal[capacity];
        }

        public int Count
        {
            get { ThrowIfDisposed(); return _count; }
        }

        public ISocketProtocal this[int index]
        {
            get { ThrowIfDisposed(); return _frames[index]; }
        }

        /// <summary>帧切片；仅在批次 Dispose 前有效。</summary>
        public ReadOnlySpan<ISocketProtocal> Frames
        {
            get { ThrowIfDisposed(); return _frames.AsSpan(0, _count); }
        }

        /// <summary>供非 BaseCoder 的 coder 组装帧。</summary>
        public void Add(ISocketProtocal frame)
        {
            ThrowIfDisposed();
            if (_count == _frames.Length)
            {
                var bigger = new ISocketProtocal[_frames.Length * 2];
                Array.Copy(_frames, bigger, _count);
                _frames = bigger;
            }
            _frames[_count++] = frame;
        }

        internal void SetPooledBuffer(byte[] buffer, int requestedSize)
        {
            _pooledBuffer = buffer;
            _pooledRequestedSize = requestedSize;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // 先释放帧，再归还池化背衬：批次帧虽约定“非持有者”，但若其 Dispose 触碰背衬，
            // 归还后可能被并发租出，形成 use-after-return（code-quality 审查发现）。
            try
            {
                for (var i = 0; i < _count; i++)
                {
                    var d = _frames[i] as IDisposable;
                    if (d != null) d.Dispose();
                }
            }
            finally
            {
                _count = 0;
                if (_pooledBuffer != null)
                {
                    MemoryPoolManager.Return(_pooledBuffer, _pooledRequestedSize);
                    _pooledBuffer = null;
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DecodedFrames));
        }
    }

    internal enum FrameEventKind { Heart, File, Data }

    internal struct FrameEvent
    {
        public FrameEventKind Kind;
        public long BodyLength;
        public byte Type;
        public int Offset;
        public int Length;
        public DateTime HeartAt;
    }

    /// <summary>
    /// 驱动 per-instance FrameDecoder 时，把帧体复制进单块可增长池化缓冲；
    /// 回调延迟到缓冲定型后按原顺序触发，从而让 ReadOnlyMemory 在批次 Dispose 前稳定有效。
    /// </summary>
    internal sealed class FramesCollector : IFrameHandler, IDisposable
    {
        private byte[] _pooled;
        private int _pooledRequestedSize;
        private int _offset;
        private FrameEvent[] _events = new FrameEvent[4];
        private int _eventCount;
        private int _dataCount;

        public void OnFrame(in SocketFrame frame)
        {
            var off = Append(frame.Content);
            AddEvent(new FrameEvent
            {
                Kind = FrameEventKind.Data,
                BodyLength = frame.BodyLength,
                Type = frame.Type,
                Offset = off,
                Length = frame.Content.Length
            });
            _dataCount++;
        }

        public void AddHeart(DateTime at)
        {
            AddEvent(new FrameEvent { Kind = FrameEventKind.Heart, HeartAt = at });
        }

        public void AddFile(ReadOnlySpan<byte> content)
        {
            var off = Append(content);
            AddEvent(new FrameEvent { Kind = FrameEventKind.File, Offset = off, Length = content.Length });
        }

        public DecodedFrames Build(Action<DateTime> onHeart, Action<ReadOnlyMemory<byte>> onFile)
        {
            var frames = new DecodedFrames(_dataCount == 0 ? 1 : _dataCount);
            frames.SetPooledBuffer(_pooled, _pooledRequestedSize);

            var buffer = _pooled;
            _pooled = null; // 所有权转移给 frames（在触发回调前转移，异常时由下方 catch 释放）

            try
            {
                for (var i = 0; i < _eventCount; i++)
                {
                    var e = _events[i];
                    switch (e.Kind)
                    {
                        case FrameEventKind.Heart:
                            if (onHeart != null) onHeart(e.HeartAt);
                            break;
                        case FrameEventKind.File:
                            if (onFile != null)
                                onFile(e.Length == 0 ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte>(buffer, e.Offset, e.Length));
                            break;
                        case FrameEventKind.Data:
                            var content = e.Length == 0 ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte>(buffer, e.Offset, e.Length);
                            frames.Add(new BaseSocketProtocal(e.BodyLength, e.Type, content));
                            break;
                    }
                }
                return frames;
            }
            catch
            {
                frames.Dispose();
                throw;
            }
            finally
            {
                _offset = 0;
                _eventCount = 0;
                _dataCount = 0;
            }
        }

        /// <summary>Build 未被调用时归还池化缓冲，避免异常路径泄漏（BaseCoder 在 finally 中调用）。</summary>
        public void Dispose()
        {
            if (_pooled != null)
            {
                MemoryPoolManager.Return(_pooled, _pooledRequestedSize);
                _pooled = null;
            }
            _offset = 0;
            _eventCount = 0;
            _dataCount = 0;
        }

        private int Append(ReadOnlySpan<byte> content)
        {
            if (content.Length == 0) return -1;
            Ensure(content.Length);
            var off = _offset;
            content.CopyTo(_pooled.AsSpan(off));
            _offset += content.Length;
            return off;
        }

        private void AddEvent(FrameEvent e)
        {
            if (_eventCount == _events.Length)
            {
                var bigger = new FrameEvent[_events.Length * 2];
                Array.Copy(_events, bigger, _eventCount);
                _events = bigger;
            }
            _events[_eventCount++] = e;
        }

        private void Ensure(int incoming)
        {
            if (_pooled == null)
            {
                _pooledRequestedSize = Math.Max(MemoryPoolManager.SmallThreshold, incoming);
                _pooled = MemoryPoolManager.Rent(_pooledRequestedSize);
                return;
            }
            if ((long)_offset + incoming <= _pooled.Length) return;

            long required = (long)_offset + incoming;
            if (required > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(incoming));

            var newSize = _pooled.Length;
            while (newSize < required)
            {
                if (newSize > int.MaxValue / 2) { newSize = (int)required; break; }
                newSize <<= 1;
            }

            // 归还旧缓冲必须用其“原始请求大小”，否则会被错误分级到更大的池（code-quality 审查发现）。
            var oldSize = _pooledRequestedSize;
            var bigger = MemoryPoolManager.Rent(newSize);
            Buffer.BlockCopy(_pooled, 0, bigger, 0, _offset);
            MemoryPoolManager.Return(_pooled, oldSize);
            _pooled = bigger;
            _pooledRequestedSize = newSize;
        }
    }
}
```

- [ ] **Step 4: Run to verify the new type compiles**

Run: `dotnet build Src/SAEA.Sockets/SAEA.Sockets.csproj -c Debug`
Expected: FAIL only in `BaseCoder`/Shortcut (Task 5/7), no `DecodedFrames` errors.

- [ ] **Step 5: Commit**

```bash
git add Src/SAEA.Sockets/Base/DecodedFrames.cs Src/SAEA.P2PTest/Tests/SpanPipelineTest.cs
git commit -m "feat(sockets): add DecodedFrames batch + FramesCollector"
```

---

## Task 4: `ICoder` + `IFrameCoder` + `FileSpanHandler`

**Files:**
- Modify: `Src/SAEA.Sockets/Interface/ICoder.cs`
- Create: `Src/SAEA.Sockets/Interface/IFrameCoder.cs`

- [ ] **Step 1: Rewrite `ICoder`**

Replace body of `Src/SAEA.Sockets/Interface/ICoder.cs` (keep header; `using System; using System.Buffers; using SAEA.Sockets.Base;`):

```csharp
    /// <summary>
    /// 通信数据编解码器。
    /// </summary>
    public interface ICoder
    {
        /// <summary>编码协议对象，写入 writer。</summary>
        void Encode(ISocketProtocal protocal, IBufferWriter<byte> writer);

        /// <summary>
        /// 有状态解码（复用半包缓存）。多段序列按段喂入。
        /// 返回的 <see cref="DecodedFrames"/> 必须 using，帧内存仅在其 Dispose 前有效。
        /// 非线程安全；不得并发调用，且与 DecodeStream 共享解码状态。
        /// </summary>
        /// <param name="data">待解码数据；多段序列按段喂入。</param>
        /// <param name="onHeart">心跳回调（构造批次时按原顺序触发）。</param>
        /// <param name="onFile">文件/大帧回调；其 ReadOnlyMemory 仅在返回的 DecodedFrames Dispose 前有效，不得跨 Dispose 捕获。</param>
        DecodedFrames Decode(ReadOnlySequence<byte> data, Action<DateTime> onHeart = null, Action<ReadOnlyMemory<byte>> onFile = null);

        /// <summary>清除内部状态（丢弃半包/被拒帧缓存；解码抛错后应调用）。</summary>
        void Clear();
    }
```

- [ ] **Step 2: Create `IFrameCoder`**

Create `Src/SAEA.Sockets/Interface/IFrameCoder.cs` (with the project's standard file header):

```csharp
using System;

namespace SAEA.Sockets.Interface
{
    /// <summary>
    /// 帧式（9 字节头 + body）编解码器专用：仅 BaseCoder 及其派生实现。
    /// </summary>
    public interface IFrameCoder : ICoder
    {
        /// <summary>
        /// 增量零拷贝解码：frame.Content / onFile 仅在回调期间有效。
        /// 非线程安全；与 Decode 共享解码状态，不得并发调用。
        /// </summary>
        void DecodeStream(ReadOnlySpan<byte> data, IFrameHandler handler, Action<DateTime> onHeart = null, FileSpanHandler onFile = null);
    }

    /// <summary>
    /// span 版文件回调。C# 不允许 Action&lt;ReadOnlySpan&lt;byte&gt;&gt;，故用具名委托。
    /// </summary>
    public delegate void FileSpanHandler(ReadOnlySpan<byte> content);
}
```

- [ ] **Step 3: Commit**

```bash
git add Src/SAEA.Sockets/Interface/ICoder.cs Src/SAEA.Sockets/Interface/IFrameCoder.cs
git commit -m "feat(sockets): ICoder on IBufferWriter/DecodedFrames; add IFrameCoder + FileSpanHandler"
```

---

## Task 5: `FrameDecoder` BigData span + `BaseCoder` rewrite

**Files:**
- Modify: `Src/SAEA.Sockets/Base/FrameDecoder.cs`
- Modify: `Src/SAEA.Sockets/Base/BaseCoder.cs`
- Test: `Src/SAEA.P2PTest/Tests/SpanPipelineTest.cs`

- [ ] **Step 1: Write the failing test**

Add to `SpanPipelineTest.Run()`:

```csharp
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

            // 多段序列：帧头与帧体跨非连续段，单次 Decode 按段喂入
            var multi = Frame(3, 2, new byte[] { 4, 5, 6 });
            var seg1 = new ByteSegment(new ReadOnlyMemory<byte>(multi, 0, 5));
            var seg2 = seg1.Append(new ReadOnlyMemory<byte>(multi, 5, multi.Length - 5));
            using (var ms = coder.Decode(new ReadOnlySequence<byte>(seg1, 0, seg2, seg2.Memory.Length)))
            {
                TestHarness.Expect(ms.Count == 1 && ms[0].BodyLength == 3, "BaseCoder.Decode handles multi-segment sequence");
                TestHarness.Expect(ms[0].Content.Span[0] == 4 && ms[0].Content.Span[2] == 6, "BaseCoder.Decode multi-segment body");
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
            TestHarness.Expect(probe.Count == 1 && probe.LastLength == 3 && probe.LastContent != null && probe.LastContent[1] == 2,
                "BaseCoder.DecodeStream invokes handler per frame with intact body");
```

Add these helper classes at the bottom of `SpanPipelineTest.cs` (inside the namespace). Note: `SocketFrame` is a `readonly ref struct`, so `Func<SocketFrame, int>` would be illegal C# (same class of error as spec ⑯) — the probe must implement the interface directly. `ByteSegment` builds a genuine multi-segment `ReadOnlySequence<byte>`:

```csharp
    internal sealed class ByteSegment : ReadOnlySequenceSegment<byte>
    {
        public ByteSegment(ReadOnlyMemory<byte> memory) { Memory = memory; }

        public ByteSegment Append(ReadOnlyMemory<byte> memory)
        {
            var next = new ByteSegment(memory) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;
            return next;
        }
    }

    internal sealed class FrameProbe : SAEA.Sockets.Interface.IFrameHandler
    {
        public int Count;
        public int LastLength;
        public byte[]? LastContent;
        public void OnFrame(in SAEA.Sockets.Base.SocketFrame frame)
        {
            Count++;
            LastLength = frame.Content.Length;
            LastContent = frame.Content.ToArray();
        }
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug`
Expected: build FAIL — `Decode(ReadOnlySequence)`, `DecodeStream`, `DecodedFrames` not present on `BaseCoder`.
Note: the test project is intentionally uncompilable from here through Task 9 (existing callers in `StreamDecoderTest`/`PerformanceTest`/`ProtocolTest`/`IocpBenchmark` still use the removed `Decode(byte[])`/`ToBytes()`/`Encode(protocal)` APIs and are migrated in Tasks 8–9). Do NOT chase those errors in this task; the real green gate is Task 10.

- [ ] **Step 3: Change `FrameDecoder.TryReadFrame` BigData to span**

In `Src/SAEA.Sockets/Base/FrameDecoder.cs`, replace the `TryReadFrame` signature and the BigData block, and switch to `BinaryPrimitives`:

```csharp
        public bool TryReadFrame(out FrameKind kind, out SocketFrame frame, out ReadOnlySpan<byte> fileContent, out DateTime heartAt)
        {
            kind = FrameKind.Data;
            frame = default;
            fileContent = default;
            heartAt = default;

            if (_end - _start < BaseCoder.P_Head) return false;

            var bodyLen = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(_buffer.AsSpan(_start, BaseCoder.P_LEN));
            var type = _buffer[_start + BaseCoder.P_LEN];

            if (bodyLen < 0 || bodyLen > _maxFrameLength)
                throw new KernelException($"非法的数据帧长度: {bodyLen}");

            if (bodyLen == 0 && type == (byte)SocketProtocalType.Heart)
            {
                Consume(BaseCoder.P_Head);
                heartAt = DateTimeHelper.Now;
                kind = FrameKind.Heart;
                return true;
            }

            var total = BaseCoder.P_Head + (int)bodyLen;
            if (_end - _start < total) return false;

            if (type == (byte)SocketProtocalType.BigData)
            {
                fileContent = _buffer.AsSpan(_start + BaseCoder.P_Head, (int)bodyLen);
                Consume(total);
                kind = FrameKind.File;
                return true;
            }

            frame = new SocketFrame(bodyLen, type, _buffer.AsSpan(_start + BaseCoder.P_Head, (int)bodyLen));
            Consume(total);
            kind = FrameKind.Data;
            return true;
        }
```

(Delete the old `BitConverter.ToInt64(_buffer, _start)` line and the `new byte[(int)bodyLen]`/`Buffer.BlockCopy` BigData block.)

Also harden `EnsureCapacity` against the same `int` overflow class found in Task 1/3 (a ~1GB+ accumulator would otherwise loop forever on `newSize <<= 1`):

```csharp
        private void EnsureCapacity(int incoming)
        {
            if (_start > 0 && _buffer.Length - _end < incoming)
            {
                var len = _end - _start;
                Buffer.BlockCopy(_buffer, _start, _buffer, 0, len);
                _start = 0;
                _end = len;
            }

            long required = (long)_end + incoming;
            if (required <= _buffer.Length) return;
            if (required > int.MaxValue)
                throw new KernelException($"数据帧过大: {required}");

            var newSize = _buffer.Length;
            while (newSize < required)
            {
                if (newSize > int.MaxValue / 2) { newSize = (int)required; break; }
                newSize <<= 1;
            }

            var bigger = ArrayPool<byte>.Shared.Rent(newSize);
            Buffer.BlockCopy(_buffer, _start, bigger, 0, _end - _start);
            _end -= _start;
            _start = 0;
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = bigger;
        }
```

Also clamp the `FrameDecoder` constructor's upper bound, so a deployer cannot set `MaxFrameLength` beyond `int.MaxValue - P_Head` and make `total = P_Head + bodyLen` overflow negative:

```csharp
            // 上限夹逼：total = P_Head + bodyLen 走 int 运算，避免部署方把 MaxFrameLength 设得过大导致 total 溢出为负。
            _maxFrameLength = maxFrameLength < BaseCoder.P_Head
                ? int.MaxValue - BaseCoder.P_Head
                : Math.Min(maxFrameLength, int.MaxValue - BaseCoder.P_Head);
```

- [ ] **Step 4: Rewrite `BaseCoder`**

Replace the class body in `Src/SAEA.Sockets/Base/BaseCoder.cs` (keep header; `using System; using System.Buffers; using System.Buffers.Binary; using SAEA.Sockets.Interface;` — drop `SAEA.Common`, `SAEA.Common.Caching`, `SAEA.Sockets.Model`, `System.Collections.Generic`, `System.IO`, which are no longer referenced):

```csharp
    /// <summary>
    /// 帧式编解码基类。
    /// </summary>
    public class BaseCoder : IFrameCoder
    {
        public const int P_LEN = 8;
        public const int P_Type = 1;
        public const int P_Head = 9;
        public const int SmallDataThreshold = 4 * 1024;

        /// <summary>单帧最大帧体长度，可由部署方收紧。构造 FrameDecoder 时捕获。</summary>
        public static int MaxFrameLength { get; set; } = int.MaxValue - P_Head;

        private FrameDecoder _decoder = new FrameDecoder(MaxFrameLength);

        public void Encode(ISocketProtocal protocal, IBufferWriter<byte> writer)
        {
            protocal.WriteTo(writer);
        }

        /// <summary>有状态批量解码。返回批次须 using。</summary>
        public DecodedFrames Decode(ReadOnlySequence<byte> data, Action<DateTime> onHeart = null, Action<ReadOnlyMemory<byte>> onFile = null)
        {
            var collector = new FramesCollector();
            try
            {
                if (data.IsSingleSegment)
                {
                    // netstandard2.0 System.Memory has no ReadOnlySequence<T>.FirstSpan; First.Span is equivalent for a single segment.
                    DecodeStream(data.First.Span, collector, collector.AddHeart, collector.AddFile);
                }
                else
                {
                    foreach (var segment in data)
                        DecodeStream(segment.Span, collector, collector.AddHeart, collector.AddFile);
                }
                return collector.Build(onHeart, onFile);
            }
            finally
            {
                // Build 成功后 _pooled 已转移（Dispose 为 no-op）；异常路径（如非法帧 KernelException）在此归还缓冲。
                collector.Dispose();
            }
        }

        /// <summary>零拷贝流式解码：帧体仅在回调期间有效。</summary>
        public void DecodeStream(ReadOnlySpan<byte> data, IFrameHandler handler, Action<DateTime> onHeart = null, FileSpanHandler onFile = null)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            _decoder.Append(data);

            while (_decoder.TryReadFrame(out var kind, out var frame, out var fileContent, out var heartAt))
            {
                switch (kind)
                {
                    case FrameKind.Heart:
                        if (onHeart != null) onHeart(heartAt);
                        break;
                    case FrameKind.File:
                        if (onFile != null) onFile(fileContent);
                        break;
                    case FrameKind.Data:
                        handler.OnFrame(in frame);
                        break;
                }
            }
        }

        /// <summary>从帧头读取长度（8B 小端）。</summary>
        public static long GetLength(ReadOnlySpan<byte> data)
        {
            if (data.Length < P_LEN)
                throw new ArgumentException("数据长度不足");
            return BinaryPrimitives.ReadInt64LittleEndian(data);
        }

        public void Clear()
        {
            _decoder?.Clear();
        }
    }
```

Note: `collector.AddHeart`/`AddFile` are method-group conversions to `Action<DateTime>` / `FileSpanHandler` — both legal.

- [ ] **Step 5: Build `SAEA.Sockets` (expect only caller errors outside these files)**

Run: `dotnet build Src/SAEA.Sockets/SAEA.Sockets.csproj -c Debug`
Expected: remaining errors are in `Shortcut/*` and `Iocp*/Udp*/Stream*` due to old `Decode(byte[])`/`ToBytes()` usage (Tasks 6–7). No errors in `BaseCoder`/`FrameDecoder`/`DecodedFrames`.

- [ ] **Step 6: Commit**

```bash
git add Src/SAEA.Sockets/Base/FrameDecoder.cs Src/SAEA.Sockets/Base/BaseCoder.cs Src/SAEA.P2PTest/Tests/SpanPipelineTest.cs
git commit -m "feat(sockets): BaseCoder IFrameCoder with DecodedFrames + span BigData"
```

---

## Task 6: `P2PCoder` + `P2PProtocol` + 7 P2P call sites

**Files:**
- Modify: `Src/SAEA.P2P/Protocol/P2PCoder.cs`, `Src/SAEA.P2P/Protocol/P2PProtocol.cs`
- Modify: `Src/SAEA.P2P/Core/P2PClient.cs:185`, `Core/P2PServer.cs:128`, `Discovery/LocalDiscovery.cs:144`, `Channel/TCPChannel.cs:98`, `Channel/UDPChannel.cs:104`, `Relay/RelayManager.cs:132`, `NAT/HolePuncher.cs:104`
- Test: `Src/SAEA.P2PTest/Tests/ProtocolTest.cs` / `ProtocolAdvancedTest.cs` (existing — must still pass)

- [ ] **Step 1: Rewrite `P2PProtocol`**

Replace `Src/SAEA.P2P/Protocol/P2PProtocol.cs` body (keep header; `using System; using SAEA.Sockets.Base;`):

```csharp
    /// <summary>P2P 协议（编码便捷类型；解码统一走 DecodedFrames + BaseSocketProtocal）。</summary>
    public class P2PProtocol : BaseSocketProtocal
    {
        public P2PProtocol() { }

        public P2PProtocol(P2PMessageType messageType, byte[] content)
            : base((byte)messageType, content == null ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte>(content))
        {
        }

        public static P2PProtocol Create(P2PMessageType messageType)
        {
            return new P2PProtocol(messageType, null);
        }

        public static P2PProtocol Create(P2PMessageType messageType, byte[] content)
        {
            return new P2PProtocol(messageType, content);
        }

        public static P2PProtocol Create(P2PMessageType messageType, string content)
        {
            byte[] data = null;
            if (!string.IsNullOrEmpty(content))
                data = System.Text.Encoding.UTF8.GetBytes(content);
            return new P2PProtocol(messageType, data);
        }

        public P2PMessageType GetMessageType()
        {
            return (P2PMessageType)Type;
        }

        public string GetContentAsString()
        {
            if (Content.Length == 0) return string.Empty;
            return System.Text.Encoding.UTF8.GetString(Content.Span.ToArray());
        }
    }
```

- [ ] **Step 2: Rewrite `P2PCoder`**

Replace class body of `Src/SAEA.P2P/Protocol/P2PCoder.cs` (keep header; add `using System.Buffers; using SAEA.Common.Caching;`):

```csharp
    public class P2PCoder : BaseCoder
    {
        /// <summary>有状态解码（保留半包缓存）；BigData/File 对 P2P 静默丢弃。</summary>
        public DecodedFrames DecodeP2P(ReadOnlySequence<byte> data, Action<DateTime> onHeart = null)
        {
            return Decode(data, onHeart);
        }

        public DecodedFrames DecodeP2P(byte[] data, Action<DateTime> onHeart = null)
        {
            return Decode(new ReadOnlySequence<byte>(data), onHeart);
        }

        public byte[] EncodeP2P(P2PMessageType messageType)
        {
            return EncodeToArray(P2PProtocol.Create(messageType));
        }

        public byte[] EncodeP2P(P2PMessageType messageType, byte[] content)
        {
            return EncodeToArray(P2PProtocol.Create(messageType, content));
        }

        public byte[] EncodeP2P(P2PMessageType messageType, string content)
        {
            return EncodeToArray(P2PProtocol.Create(messageType, content));
        }

        /// <summary>零拷贝重载。</summary>
        public void EncodeP2P(P2PMessageType messageType, IBufferWriter<byte> writer)
        {
            Encode(P2PProtocol.Create(messageType), writer);
        }

        private byte[] EncodeToArray(ISocketProtocal protocal)
        {
            using (var writer = new PooledBufferWriter(64))
            {
                Encode(protocal, writer);
                return writer.WrittenSpan.ToArray();
            }
        }

        public static P2PMessageType GetP2PMessageType(byte[] data)
        {
            if (data == null || data.Length < P_LEN + 1)
                throw new ArgumentException("Data length is insufficient");
            return (P2PMessageType)data[P_LEN];
        }
    }
```

- [ ] **Step 3: Update the 7 call sites**

All 7 sites share the shape `var protocols = _coder.DecodeP2P(data); foreach (var p in protocols) { ... }`. `DecodeP2P` now returns a pooled `DecodedFrames`, so wrap the loop in `using` and iterate `frames.Frames` (a `ReadOnlySpan<ISocketProtocal>`), renaming `p` → `frame`:

```csharp
// BEFORE
var protocols = _coder.DecodeP2P(data);
foreach (var p in protocols) { /* body */ }

// AFTER
using (var frames = _coder.DecodeP2P(data))
{
    foreach (var frame in frames.Frames) { /* body, p -> frame */ }
}
```

Per-file body changes (the batch buffer is returned on `Dispose`, so anything handed to a `byte[]` API or retained must be copied with `frame.Content.ToArray()`):

- `Core/P2PClient.cs:183` (`OnSignalReceive`): `ProcessSignalMessage(p)` → `ProcessSignalMessage(frame)` (param type changes in Step 4).
- `Core/P2PServer.cs:126` (`OnReceive`): `ProcessMessage(session.ID, p)` → `ProcessMessage(session.ID, frame)`.
- `Discovery/LocalDiscovery.cs:144`: `p.GetMessageType()` → `(P2PMessageType)frame.Type`; `ProcessDiscoveryPacket(p.Content)` / `ProcessDiscoveryAck(p.Content)` → `(frame.Content.ToArray())` (both helpers are typed `byte[]` and parse/retain).
- `Channel/TCPChannel.cs:94` and `Channel/UDPChannel.cs:100`: `p.GetMessageType() == P2PMessageType.UserData && p.Content != null` → `(P2PMessageType)frame.Type == P2PMessageType.UserData && !frame.Content.IsEmpty`; `OnDataReceived?.Invoke(p.Content)` → `OnDataReceived?.Invoke(frame.Content.ToArray())` (the event is `Action<byte[]>` and consumers may retain past `Dispose`).
- `NAT/HolePuncher.cs:102`: `OnPunchPacketReceived?.Invoke(source, p.Content)` → `(source, frame.Content.ToArray())`.
- `Relay/RelayManager.cs:130` (`DecodeRelayData`): `p.GetMessageType() == P2PMessageType.RelayData && p.Content != null` → `(P2PMessageType)frame.Type == P2PMessageType.RelayData && !frame.Content.IsEmpty`; `var content = p.Content;` → `var content = frame.Content.ToArray();` (the returned `payload` outlives the batch, so the copy is required).

- [ ] **Step 4: Change `ProcessMessage`/`ProcessSignalMessage` parameter types**

- `Core/P2PClient.cs`: `private void ProcessSignalMessage(ISocketProtocal protocol)`.
- `Core/P2PServer.cs`: `private void ProcessMessage(string sessionId, ISocketProtocal protocol)`.

Add `using SAEA.Sockets.Interface;` to both files. Then:
- `protocol.GetMessageType()` → `(P2PMessageType)protocol.Type`.
- Each switch arm that passes content to an existing `byte[]` helper must pass an explicit copy, e.g. `ProcessRegisterAck(protocol.Content.ToArray())`, `ProcessRegister(sessionId, protocol.Content.ToArray())` (the helpers JSON-parse/serialize and, for channels, hand `byte[]` to event consumers; the batch is disposed at the end of the `using`). Arms that take no content (`ProcessAuthSuccess()`, `ProcessNatProbe(sessionId)`, `SendHeartbeatAck(sessionId)`) are unchanged.
- Do **not** change the `byte[]` helper signatures (avoids cascading churn; keeps behavior identical to the previous materialized-`byte[]` semantics).

- [ ] **Step 5: Build `SAEA.P2P`**

Run: `dotnet build Src/SAEA.P2P/SAEA.P2P.csproj -c Debug`
Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add Src/SAEA.P2P
git commit -m "refactor(p2p): DecodeP2P returns DecodedFrames; P2PProtocol encode-only"
```

---

## Task 7: `SAEA.Sockets` internal compile (Shortcut + socket impls)

**Files:**
- Modify: `Src/SAEA.Sockets/Shortcut/TCPClient.cs`, `TCPServer.cs`, `UDPClient.cs`, `UDPServer.cs`, and any `Core/Tcp|Udp/*` / `Handler/*` flagged by the compiler
- Modify: `Src/SAEA.Sockets/Handler/OnReceiveHandler.cs` (only if required by Task 7 errors; interface change is Plan 2)

- [ ] **Step 1: Fix `Shortcut/UDPClient.cs`**

Replace `SendAsync(BaseSocketProtocal)` and the receive handler:

```csharp
        protected void SendAsync(BaseSocketProtocal protocal)
        {
            using (var w = new SAEA.Common.Caching.PooledBufferWriter(64))
            {
                protocal.WriteTo(w);
                _udpClient.SendAsync(w.WrittenSpan.ToArray());
            }
        }
```

```csharp
        private void UdpClient_OnReceive(byte[] data)
        {
            using (var msgs = _baseUnpacker.Decode(new System.Buffers.ReadOnlySequence<byte>(data)))
            {
                if (msgs.Count < 1) return;
                foreach (var msg in msgs.Frames)
                    OnReceive?.Invoke(this, msg);
            }
        }
```

- [ ] **Step 2: Fix `Shortcut/UDPServer.cs`**

```csharp
        protected void SendAsync(string id, BaseSocketProtocal baseSocketProtocal)
        {
            using (var w = new SAEA.Common.Caching.PooledBufferWriter(64))
            {
                baseSocketProtocal.WriteTo(w);
                _udpServer.SendAsync(id, w.WrittenSpan.ToArray());
            }
        }
```

```csharp
        private void UdpServer_OnReceive(Interface.ISession currentSession, byte[] data)
        {
            var userToken = (IUserToken)currentSession;
            using (var msgs = userToken.Coder.Decode(new System.Buffers.ReadOnlySequence<byte>(data)))
            {
                if (msgs.Count == 0) return;
                foreach (var msg in msgs.Frames)
                    OnReceive?.Invoke(this, userToken.ID, msg);
            }
        }
```

- [ ] **Step 3: Fix `Shortcut/TCPClient.cs` / `TCPServer.cs`**

Apply the same two transformations wherever they call `.ToBytes()` (wrap in `PooledBufferWriter` + `WrittenSpan.ToArray()`) or `.Decode(data)` (wrap in `using` + iterate `msgs.Frames`). Use the compiler error list to enumerate exact lines.

- [ ] **Step 4: Iterate the compiler**

Run: `dotnet build Src/SAEA.Sockets/SAEA.Sockets.csproj -c Debug`
Fix each remaining error in `SAEA.Sockets` (do **not** modify `IClientSocket`/`IServerSocket` signatures — Plan 2). Typical fixes: `new BaseSocketProtocal { ... }` → ctor; `.ToBytes()` → writer; `.Decode(x)` → sequence + `using`.
Expected: `SAEA.Sockets` builds with 0 errors.

- [ ] **Step 5: Commit**

```bash
git add Src/SAEA.Sockets
git commit -m "refactor(sockets): adapt Shortcut + socket impls to new coder/protocal surface"
```

---

## Task 8: Adapt external `ICoder`/`ISocketProtocal` implementers

**Files (implementers):**
- `Src/SAEA.FTP/Net/FTPCoder.cs`
- `Src/SAEA.WebSocket/Model/WSCoder.cs`, `Model/WSProtocal.cs`, `WSClient.cs`, `Core/WSServerImpl.cs`, `Core/WSSServerImpl.cs`
- `Src/SAEA.QueueSocket/Net/QueueCoder.cs`
- `Src/SAEA.RPC/Net/RpcCoder.cs`, `Net/RServer.cs`, `Net/RClient.cs`
- `Src/SAEA.Http/Base/Net/HttpCoder.cs`
- `Src/SAEA.RedisSocket/Base/Net/RedisCoder.cs`
- `Src/SAEA.Sockets.TcpTest/JContext.cs`

- [ ] **Step 1: `FTPCoder`**

```csharp
        public void Encode(ISocketProtocal protocal, System.Buffers.IBufferWriter<byte> writer)
        {
            protocal.WriteTo(writer);
        }

        public SAEA.Sockets.Base.DecodedFrames Decode(System.Buffers.ReadOnlySequence<byte> data,
            Action<DateTime> onHeart = null, Action<ReadOnlyMemory<byte>> onFile = null)
        {
            throw new NotImplementedException();
        }
```

Keep the existing extra overload `Decode(byte[], Action<ISocketProtocal>, Action<DateTime>, Action<byte[]>)` verbatim (it is currently empty; do not change it) and keep `Clear()` as-is.

- [ ] **Step 2: `WSCoder`**

```csharp
        public void Encode(ISocketProtocal protocal, System.Buffers.IBufferWriter<byte> writer)
        {
            var ws = protocal as WSProtocal;
            if (ws != null) ws.WriteMaskedTo(writer);
            else protocal.WriteTo(writer);
        }

        public SAEA.Sockets.Base.DecodedFrames Decode(System.Buffers.ReadOnlySequence<byte> data,
            Action<DateTime> onHeart = null, Action<ReadOnlyMemory<byte>> onFile = null)
        {
            var frames = new SAEA.Sockets.Base.DecodedFrames();
            _buffer.AddRange(data.ToArray());
            // ... keep the existing frame-splitting loop verbatim ...
            // for each produced frame: frames.Add(new WSProtocal(opcode, payloadData) { IsPooled = ... });
            return frames;
        }
```

Change the private `Decode(byte[])` return type/body to append into the passed `DecodedFrames`, or inline the loop into the new method. `data.ToArray()` is an accepted one-time copy for this non-frame coder.

The legacy public overload `Decode(byte[] data, Action<DateTime> onHeart, Action<byte[]> onFile)` has no remaining external callers once the three in-repo sites (`WSClient.cs:212`, `WSServerImpl.cs:112`, `WSSServerImpl.cs:140`) are migrated to the `ReadOnlySequence` overload above. Delete both that overload and the private `Decode(byte[])` it delegates to, and fold the frame-splitting loop into the new `Decode(ReadOnlySequence<byte>, ...)`. Keep `DoMask`/`DoMaskUnsafe`/`Clear` verbatim.

- [ ] **Step 3: `WSProtocal`**

Replace the class body (keep header; add `using System.Buffers;`; keep `_mask`, ctor overloads, `IsPooled`):

```csharp
        private byte[] _buffer;

        public long BodyLength { get; protected set; }
        public byte Type { get; protected set; }
        public ReadOnlyMemory<byte> Content { get; protected set; } = ReadOnlyMemory<byte>.Empty;
        public bool IsPooled { get; set; }

        public WSProtocal(byte type, byte[] content)
        {
            Type = type;
            _buffer = content;
            BodyLength = content == null ? 0 : content.Length;
            Content = content == null ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte>(content);
        }

        public WSProtocal(WSProtocalType type, byte[] content)
        {
            Type = (byte)type;
            _buffer = content;
            BodyLength = (content == null || content.Length == 0) ? 0 : content.Length;
            Content = (content == null || content.Length == 0) ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte>(content);
        }

        public void WriteTo(IBufferWriter<byte> writer) { WriteFrame(writer, false); }

        public void WriteMaskedTo(IBufferWriter<byte> writer) { WriteFrame(writer, true); }

        private void WriteFrame(IBufferWriter<byte> writer, bool masked)
        {
            ulong len = (ulong)BodyLength;
            byte byte1 = (byte)(0x80 | Type);
            byte[] maskBytes = null;

            if (len < 126)
            {
                var s = writer.GetSpan(2);
                s[0] = byte1; s[1] = (byte)((masked ? 0x80 : 0) | (byte)len);
                writer.Advance(2);
            }
            else if (len < 65536)
            {
                var s = writer.GetSpan(4);
                s[0] = byte1; s[1] = (byte)((masked ? 0x80 : 0) | 126);
                s[2] = (byte)((ushort)len >> 8); s[3] = (byte)(ushort)len;
                writer.Advance(4);
            }
            else
            {
                var s = writer.GetSpan(10);
                s[0] = byte1; s[1] = (byte)((masked ? 0x80 : 0) | 127);
                var l = len;
                for (int i = 0; i < 8; i++) s[2 + i] = (byte)(l >> (56 - 8 * i));
                writer.Advance(10);
            }

            if (masked)
            {
                maskBytes = _mask.ToBytes();
                var m = writer.GetSpan(4);
                for (int i = 0; i < 4; i++) m[i] = maskBytes[i];
                writer.Advance(4);
            }

            if (len > 0)
            {
                var payload = writer.GetSpan((int)len);
                Content.Span.CopyTo(payload);
                if (masked)
                    for (int i = 0; i < (int)len; i++) payload[i] = (byte)(payload[i] ^ maskBytes[i % 4]);
                writer.Advance((int)len);
            }
        }

        public void Dispose()
        {
            if (_buffer != null)
            {
                if (IsPooled)
                {
                    MemoryPoolManager.Return(_buffer, _buffer.Length);
                    IsPooled = false;
                }
                _buffer = null;
            }
            Content = ReadOnlyMemory<byte>.Empty;
        }
```

- [ ] **Step 4: `WSClient` / `WSServerImpl` / `WSSServerImpl`**

- `WSClient.cs:257` `_client.SendAsync(msg.ToBytes())` →
  ```csharp
  using (var w = new SAEA.Common.Caching.PooledBufferWriter(64)) { msg.WriteMaskedTo(w); _client.SendAsync(w.WrittenSpan.ToArray()); }
  ```
- `WSClient.cs:228` `Encoding.UTF8.GetString(wsProtocal.Content)` → `Encoding.UTF8.GetString(wsProtocal.Content.Span.ToArray())`.
- `WSServerImpl.cs:112` / `WSSServerImpl.cs:140` / `WSClient.cs:212` `coder.Decode(data)` → `using (var msgs = coder.Decode(new ReadOnlySequence<byte>(data))) { foreach (var m in msgs.Frames) ... }` (keep the existing switch body; `m` is `ISocketProtocal`, so cast `(WSProtocal)m` as before).
- `WSServerImpl.cs:229` `data.ToBytes(false)` → server is **unmasked**: write via `data.WriteTo(w)` into a `PooledBufferWriter`, then `ReplyBase(id, w.WrittenSpan.ToArray())`.
- `WSServerImpl.cs:240` and its `ReplyClose` counterpart pass `data.Content` to `ReplyBase(string, WSProtocalType, byte[] content)` → `data.Content.ToArray()`.
- `WSSServerImpl.cs:179` `new WSProtocal(type, content).ToBytes()` and `WSSServerImpl.cs:186` `data.ToBytes()` are BOTH `WSProtocal.ToBytes` (not a `byte[]` extension) in `ReplyBase(Stream, ...)`. Server is **unmasked**: write via `WriteTo(w)` into a `PooledBufferWriter`, then `stream.Write(w.WrittenSpan.ToArray(), 0, len)`.
- `WSSServerImpl.cs:193/198` `ReplyBase(stream, WSProtocalType.Pong, data.Content)` → `data.Content.ToArray()`.
- `WSClient.cs:284` `var data = new WSProtocal(WSProtocalType.Pong, null).ToBytes();` is a dead local (its value is unused; the next line calls `SendBase(...)`). Delete the `var data = ...` line and keep `SendBase(new WSProtocal(WSProtocalType.Pong, null))`.

**Frame-lifetime contract (WSCoder path):** `WSCoder.Decode` produces `WSProtocal` frames that own their own buffers (`IsPooled` for >4KB, matching the existing rent-and-return semantics). `DecodedFrames.Dispose` disposes `IDisposable` frames, so under the `using` above the frames' buffers are returned at the end of the block. Consumers (the `OnMessage` event handlers in `WSClient`/`WSServerImpl`/`WSSServerImpl`) must therefore consume synchronously inside the handler (as the existing callers do); do NOT stash the `WSProtocal` for later use. This is the same consume-or-copy rule as the batch codec path. Do not add compensating copies beyond what the plan lists.

- [ ] **Step 5: Throwing coders — signature only**

For `QueueCoder`, `RpcCoder`, `HttpCoder`, `RedisCoder`, `JUnpacker`: change the interface `Encode`/`Decode` signatures to the new ones, keeping `throw new NotImplementedException()` (or the existing behavior). Keep every extra static/instance overload (`QueueCoder.Encode(QueueSocketMsg)`, `RpcCoder.Encode(RSocketMsg)`, `HttpCoder.GetRequest(...)`, `JUnpacker.Decode(byte[]) -> byte[]`, etc.) verbatim.

- [ ] **Step 6: Build the implementer projects**

Run: `dotnet build Src/SAEA.WebSocket/SAEA.WebSocket.csproj -c Debug` then `SAEA.FTP`, `SAEA.QueueSocket`, `SAEA.RPC`, `SAEA.Http`, `SAEA.RedisSocket`, `SAEA.Sockets.TcpTest`.
Expected: 0 errors in each.

- [ ] **Step 7: Commit**

```bash
git add Src/SAEA.FTP Src/SAEA.WebSocket Src/SAEA.QueueSocket Src/SAEA.RPC Src/SAEA.Http Src/SAEA.RedisSocket Src/SAEA.Sockets.TcpTest
git commit -m "refactor: adapt all ICoder/ISocketProtocal implementers to Span/Memory surface"
```

---

## Task 9: Adapt remaining callers across the solution

**Files (from `rg` sweep):** `Src/SAEA.FileSocket/{Server,Client}.cs`, `Src/SAEA.Audio.Net/Net/{TransferServer,TransferClient}.cs`, `Src/SAEA.MessageSocket/{MessageServer,MessageClient}.cs`, `Src/SAEA.MQTT/Implementations/MqttTcpChannel.cs`, `Src/SAEA.DNS/Coder/UdpRequestCoder.cs`, `Src/SAEA.Sockets.UdpTest/Program.cs`, `Src/SAEA.WebSocketTest/Program.cs`, `Src/SAEA.Sockets.TcpTest/StreamServerSocketTests.cs`, all `SAEA.*Test` projects, and the `SAEA.P2PTest` tests.

- [ ] **Step 1: Apply the caller transformations**

| Old | New |
|-----|-----|
| `new BaseSocketProtocal { BodyLength = L, Type = T, Content = C }` | `new BaseSocketProtocal(L, T, C)` (or `(T, C)` if body==content.Length) |
| `p.ToBytes()` | `using (var w = new PooledBufferWriter(64)) { p.WriteTo(w); <send> w.WrittenSpan.ToArray(); }` |
| `coder.Decode(byte[] x)` | `using (var msgs = coder.Decode(new ReadOnlySequence<byte>(x))) { foreach (var m in msgs.Frames) ... }` |
| `coder.Decode(data, onHeart, onFile)` | same, with `onFile` now `Action<ReadOnlyMemory<byte>>`; copy if retained |
| `msg.Content` used as `byte[]` | `msg.Content.Span` / `msg.Content.ToArray()` |
| `Encoding.UTF8.GetString(msg.Content)` | `Encoding.UTF8.GetString(msg.Content.Span.ToArray())` |
| `BaseCoder.GetLength(byte[])` | unchanged (implicit span conversion) |

Apply at every site flagged by the build. Then:

- [ ] **Step 2: Build the whole solution**

Run: `dotnet build Src/SAEA.Sockets.sln -c Debug`
Fix remaining errors iteratively (each is one of the patterns above).
Expected: `0 Error(s)`.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "refactor: adapt remaining protocol/coder callers solution-wide"
```

---

## Task 10: Full green gate + test migration

**Files:** `Src/SAEA.P2PTest/Tests/*`, `Src/SAEA.P2PTest/Program.cs`, `Src/SAEA.Sockets.UdpTest/Program.cs`

- [ ] **Step 1: Rebuild and run all tests**

Run: `dotnet build Src/SAEA.Sockets.sln -c Release`
Expected: `0 Error(s)`.

Run: `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug -- --all`
Expected: `ALL ADVANCED TESTS: 283/283 passed, 0 failed` (255 existing + 28 new assertions in SpanPipelineTest: Task 1 = 7, Task 2 = 7, Task 3 = 4, Task 5 = 10).

- [ ] **Step 2: Fix any legacy-test regressions**

If `StreamDecoderTest`/`ProtocolTest`/`ProtocolAdvancedTest`/`PerformanceTest` fail, adapt them to the new API and re-run until 0 failed. Preserve their assertions (they are the behavior oracle from the previous round).

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "test: migrate remaining tests to Span/Memory coder API; all green (283/283)"
```

- [ ] **Step 4: Update README test count**

In `README.md` and `README.en.md`, change `255` → `283` in the test-count line.

```bash
git add README.md README.en.md
git commit -m "docs: update test count to 283"
```

- [ ] **Step 5 (deferred, not a gate blocker): batch-path allocation reduction**

`BaseCoder.Decode` allocates a fresh `FramesCollector` (plus its `FrameEvent[4]`, a `DecodedFrames` + `ISocketProtocal[]`, and two method-group delegates per segment) on every call; a half-packet that yields nothing still costs ~5 allocations. Spec §8.2 scopes the `B/frame < 100` / `Gen0 = 0` gate to the **`DecodeStream`** path, so this does not block Plan 1. Recorded here so it is not lost: if a later benchmark targets the batch path, cache a per-instance `FramesCollector` (mirroring `_decoder`) with reusable `FrameEvent[]` and cached `Action<DateTime>`/`FileSpanHandler` delegates.

---

## Self-Review

**1. Spec coverage (Plan 1 portion)**
- §3 `PooledBufferWriter` — Task 1. ✓
- §4.1 `ISocketProtocal` — Task 2. ✓
- §4.2 `BaseSocketProtocal` — Task 2. ✓
- §4.3 `DecodedFrames` — Task 3. ✓
- §4.4 `ICoder` + `IFrameCoder` (+ ⑯`FileSpanHandler`, ⑰stateful decode) — Task 4/5. ✓
- §4.5 `BaseCoder` + `FrameDecoder` BigData span — Task 5. ✓
- §4.6 `P2PCoder`/`P2PProtocol`/7 call sites — Task 6. ✓
- §7 codec/caller adaptation (all non-socket-surface callers) — Tasks 7/8/9. ✓ (Socket-interface modernization, PipeReader stream, UDP span events, Shortcut event signatures, P2P `Span.IndexOf`, benchmark, `RioExtention` deletion, version bump = **Plan 2**, deferred.)
- §12.1–12.3 ordering — Tasks 1→5→6→7/8/9. ✓

**2. Placeholder scan**
- Task 7 Step 3 and Task 8 Step 5 and Task 9 Step 1 intentionally say "apply at every site flagged by the build" — these are mechanical, compiler-enumerated adaptations. Every *distinct* transformation is given as concrete before/after code in Tasks 7/8/9. No "TBD".
- Task 8 Step 2 shows the surrounding method shell and instructs keeping the existing loop verbatim; the loop is ~120 lines already present in `WSCoder.cs:87-204` and unchanged except `result.Add(...)` → `frames.Add(...)`. This is deliberate: reproducing it risks divergence.

**3. Type consistency**
- `DecodedFrames.Add(ISocketProtocal)` public; `SetPooledBuffer` internal (same assembly as BaseCoder/collector). WSCoder (other assembly) uses public `Add`. ✓
- `ICoder.Decode(ReadOnlySequence<byte>, Action<DateTime>, Action<ReadOnlyMemory<byte>>)` and `FramesCollector.Build(Action<DateTime>, Action<ReadOnlyMemory<byte>>)` match. ✓
- `IFrameCoder.DecodeStream(ReadOnlySpan<byte>, IFrameHandler, Action<DateTime>, FileSpanHandler)` and `FramesCollector.OnFrame(in SocketFrame)`/`AddHeart`/`AddFile` match (`AddFile(ReadOnlySpan<byte>)` → `FileSpanHandler`). ✓
- `FrameDecoder.TryReadFrame(... out ReadOnlySpan<byte> fileContent ...)` and `BaseCoder.DecodeStream` `onFile(fileContent)` match. ✓
- `BaseSocketProtocal.WriteTo` uses `BaseCoder.P_LEN/P_Head` (same namespace). ✓
- `P2PProtocol : BaseSocketProtocal` uses `base((byte)messageType, ReadOnlyMemory<byte>)` matching Task 2 ctor. ✓

**Corrections made to the spec during planning (must be reflected before Plan 2):**
- `Action<ReadOnlySpan<byte>>` → named `FileSpanHandler` (C# cannot use `ref struct` as generic type argument).
- `ICoder.Decode(ReadOnlySequence)` is **stateful** (reuses `_decoder`), matching former `Decode(byte[])`; the former stateless sequence overload had no production callers.
- `FrameDecoder` BigData output is `out ReadOnlySpan<byte>`, not an `IBufferWriter`.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-09-21-span-memory-pipeline-codec-core.md`. Two execution options:

1. **Subagent-Driven (recommended)** — fresh subagent per task, review between tasks.
2. **Inline Execution** — batch execution with checkpoints.

Which approach?