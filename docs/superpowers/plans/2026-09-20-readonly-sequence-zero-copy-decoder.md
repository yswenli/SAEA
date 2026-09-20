# SAEA.Sockets Streaming Zero-Copy Decoder Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `BaseCoder`'s `MemoryStream` decoder with an incremental pooled `FrameDecoder`, add additive `Decode(ReadOnlySequence<byte>)` and zero-copy `DecodeStream`, and expose opt-in Span receive events on the TCP IOCP sockets.

**Architecture:** A new internal `FrameDecoder` keeps unconsumed bytes in an `ArrayPool` buffer with head/tail indices and parses frames with `SequenceReader`/`BinaryPrimitives`. `BaseCoder` delegates its existing `Decode` overloads to it and adds two additive entry points. IOCP sockets raise a public Span event and skip the legacy `ToArray()` only when the concrete type is the base socket and there is no `OnReceive` subscriber.

**Tech Stack:** C# 8 / netstandard2.0 (`SAEA.Sockets`), `System.Memory` (`ReadOnlySequence`, `SequenceReader`, `BinaryPrimitives`), `ArrayPool<byte>`, net8.0 test harness (`SAEA.P2PTest`, custom `TestHarness`).

**Spec:** `docs/superpowers/specs/2026-09-20-readonly-sequence-zero-copy-decoder-design.md`

---

## File Structure

| File | Responsibility |
|------|----------------|
| `Src/SAEA.Sockets/Base/SocketFrame.cs` | Public `readonly ref struct` frame view (slice valid during callback). |
| `Src/SAEA.Sockets/Base/FrameDecoder.cs` | Internal incremental framing engine. |
| `Src/SAEA.Sockets/Interface/IFrameHandler.cs` | Public zero-copy frame callback contract. |
| `Src/SAEA.Sockets/Handler/OnClientReceiveSpanHandler.cs` | Public client span event delegate. |
| `Src/SAEA.Sockets/Handler/OnServerReceiveSpanHandler.cs` | Public server span event delegate. |
| `Src/SAEA.Sockets/Base/BaseCoder.cs` | Rewire existing `Decode`; add `Decode(ReadOnlySequence)`, `DecodeStream`, `MaxFrameLength`. |
| `Src/SAEA.Sockets/Core/Tcp/IocpClientSocket.cs` | Public span event + conditional legacy delivery. |
| `Src/SAEA.Sockets/Core/Tcp/IocpServerSocket.cs` | Public span event + conditional legacy delivery. |
| `Src/SAEA.P2PTest/Tests/LegacyDecoder.cs` | Test-only copy of the old algorithm (parity baseline). |
| `Src/SAEA.P2PTest/Tests/StreamDecoderTest.cs` | Functional, parity, malformed and IOCP span tests. |
| `Src/SAEA.P2PTest/Tests/PerformanceTest.cs` | Legacy vs new vs zero-copy benchmark. |
| `Src/SAEA.P2PTest/Program.cs` | Register new tests in `--all` and menu. |

**Commands used throughout (run from repo root `D:\WorkBench\Walle\SAEA`):**

- Build: `dotnet build Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug --nologo`
- Run all: `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug -- --all`

Baseline before any change: `=== ALL ADVANCED TESTS: 189/189 passed, 0 failed ===`.

---

## Task 1: Characterize current decode behavior with tests

Add a test-only copy of the current `MemoryStream` decoder and characterization tests that must **pass against the current `BaseCoder`**. These become the parity baseline and regression net for Task 2.

**Files:**
- Create: `Src/SAEA.P2PTest/Tests/LegacyDecoder.cs`
- Create: `Src/SAEA.P2PTest/Tests/StreamDecoderTest.cs`
- Modify: `Src/SAEA.P2PTest/Program.cs`

- [ ] **Step 1: Write the legacy decoder copy**

```csharp
// Src/SAEA.P2PTest/Tests/LegacyDecoder.cs
using System;
using System.Collections.Generic;
using System.IO;
using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

namespace SAEA.P2PTest.Tests
{
    /// <summary>
    /// 旧 BaseCoder 解码算法的测试副本，仅用于新旧结果逐字节对比。
    /// </summary>
    internal static class LegacyDecoder
    {
        public static List<ISocketProtocal> Decode(byte[] data, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
        {
            var result = new List<ISocketProtocal>();
            using (var buffer = new MemoryStream())
            {
                buffer.Write(data, 0, data.Length);
                buffer.Position = 0;

                while (buffer.Length - buffer.Position >= BaseCoder.P_Head)
                {
                    buffer.Position = 0;
                    var lenBytes = new byte[BaseCoder.P_LEN];
                    buffer.Read(lenBytes, 0, BaseCoder.P_LEN);
                    long bodyLen = BitConverter.ToInt64(lenBytes, 0);

                    buffer.Position = BaseCoder.P_LEN;
                    var type = (SocketProtocalType)buffer.ReadByte();
                    buffer.Position = 0;

                    if (bodyLen == 0 && type == SocketProtocalType.Heart)
                    {
                        Remove(buffer, BaseCoder.P_Head);
                        onHeart?.Invoke(DateTime.Now);
                    }
                    else if (buffer.Length >= BaseCoder.P_Head + bodyLen)
                    {
                        byte[] content;
                        if (bodyLen <= 0)
                        {
                            content = Array.Empty<byte>();
                        }
                        else
                        {
                            content = new byte[(int)bodyLen];
                            buffer.Position = BaseCoder.P_Head;
                            buffer.Read(content, 0, (int)bodyLen);
                            buffer.Position = 0;
                        }

                        if (type == SocketProtocalType.BigData)
                        {
                            onFile?.Invoke(content);
                        }
                        else
                        {
                            result.Add(new BaseSocketProtocal { BodyLength = bodyLen, Type = (byte)type, Content = content });
                        }

                        Remove(buffer, (int)(BaseCoder.P_Head + bodyLen));
                    }
                    else
                    {
                        buffer.Position = buffer.Length;
                        break;
                    }
                }
            }
            return result;
        }

        static void Remove(MemoryStream buffer, int length)
        {
            long remaining = buffer.Length - length;
            if (remaining <= 0)
            {
                buffer.SetLength(0);
                buffer.Position = 0;
                return;
            }

            var temp = new byte[remaining];
            buffer.Position = length;
            buffer.Read(temp, 0, (int)remaining);
            buffer.SetLength(0);
            buffer.Position = 0;
            buffer.Write(temp, 0, (int)remaining);
            buffer.Position = 0;
        }
    }
}
```

- [ ] **Step 2: Write the characterization tests**

```csharp
// Src/SAEA.P2PTest/Tests/StreamDecoderTest.cs
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
```

- [ ] **Step 3: Register the test in `Program.cs`**

Add to `RunAllAsync()`, before `TestHarness.WriteSummary`:

```csharp
            StreamDecoderTest.Run();
```

Add a menu entry after `19 = Run all advanced tests`:

```csharp
                ConsoleHelper.WriteLine("20 = StreamDecoderTest");
```

And a case before `case "19":`:

```csharp
                    case "20":
                        StreamDecoderTest.Run();
                        break;
```

- [ ] **Step 4: Build and run**

Run: `dotnet build Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug --nologo`
Expected: Build succeeded.

Run: `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug -- --all`
Expected: All `StreamDecoderTest` cases `[PASS]` and the summary shows `0 failed` (total > 189).

- [ ] **Step 5: Commit**

```bash
git add Src/SAEA.P2PTest/Tests/LegacyDecoder.cs Src/SAEA.P2PTest/Tests/StreamDecoderTest.cs Src/SAEA.P2PTest/Program.cs
git commit -m "test: characterize BaseCoder decode behavior and add legacy parity baseline"
```

---

## Task 2: Replace BaseCoder internals with the pooled FrameDecoder

**Files:**
- Create: `Src/SAEA.Sockets/Base/SocketFrame.cs`
- Create: `Src/SAEA.Sockets/Base/FrameDecoder.cs`
- Modify: `Src/SAEA.Sockets/Base/BaseCoder.cs`

- [ ] **Step 1: Create `SocketFrame`**

```csharp
// Src/SAEA.Sockets/Base/SocketFrame.cs
using System;

namespace SAEA.Sockets.Base
{
    /// <summary>
    /// 单帧视图。<see cref="Content"/> 仅在回调期间有效，回调返回后不得引用。
    /// </summary>
    public readonly ref struct SocketFrame
    {
        /// <summary>
        /// 帧体长度
        /// </summary>
        public readonly long BodyLength;

        /// <summary>
        /// 帧类型
        /// </summary>
        public readonly byte Type;

        /// <summary>
        /// 帧体切片（仅在回调期间有效）
        /// </summary>
        public readonly ReadOnlySpan<byte> Content;

        public SocketFrame(long bodyLength, byte type, ReadOnlySpan<byte> content)
        {
            BodyLength = bodyLength;
            Type = type;
            Content = content;
        }
    }
}
```

- [ ] **Step 2: Create `FrameDecoder`**

```csharp
// Src/SAEA.Sockets/Base/FrameDecoder.cs
using System;
using System.Buffers;
using SAEA.Common;
using SAEA.Sockets.Model;

namespace SAEA.Sockets.Base
{
    internal enum FrameKind
    {
        Data,
        Heart,
        File
    }

    /// <summary>
    /// 增量式拆帧器：池化累加器 + head/tail 索引。非线程安全，按会话单线程使用。
    /// </summary>
    internal sealed class FrameDecoder
    {
        private byte[] _buffer;
        private int _start;
        private int _end;
        private readonly int _maxFrameLength;

        public FrameDecoder(int maxFrameLength)
        {
            _buffer = ArrayPool<byte>.Shared.Rent(1024);
            _maxFrameLength = maxFrameLength < BaseCoder.P_Head ? int.MaxValue - BaseCoder.P_Head : maxFrameLength;
        }

        public int BufferedLength => _end - _start;

        public void Append(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) return;

            // Clear() 会归还池化缓冲并把 _buffer 置空；再次复用同一 coder（UserToken 池化场景）时按需重新租用。
            if (_buffer == null)
                _buffer = ArrayPool<byte>.Shared.Rent(Math.Max(1024, data.Length));

            EnsureCapacity(data.Length);
            data.CopyTo(_buffer.AsSpan(_end));
            _end += data.Length;
        }

        public bool TryReadFrame(out FrameKind kind, out SocketFrame frame, out byte[] fileContent, out DateTime heartAt)
        {
            kind = FrameKind.Data;
            frame = default;
            fileContent = null;
            heartAt = default;

            if (_end - _start < BaseCoder.P_Head) return false;

            var bodyLen = BitConverter.ToInt64(_buffer, _start);
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
                fileContent = new byte[(int)bodyLen];
                if (bodyLen > 0)
                    Buffer.BlockCopy(_buffer, _start + BaseCoder.P_Head, fileContent, 0, (int)bodyLen);
                Consume(total);
                kind = FrameKind.File;
                return true;
            }

            frame = new SocketFrame(bodyLen, type, _buffer.AsSpan(_start + BaseCoder.P_Head, (int)bodyLen));
            Consume(total);
            kind = FrameKind.Data;
            return true;
        }

        private void Consume(int count)
        {
            _start += count;
            if (_start == _end)
            {
                _start = 0;
                _end = 0;
            }
        }

        private void EnsureCapacity(int incoming)
        {
            if (_start > 0 && _buffer.Length - _end < incoming)
            {
                var len = _end - _start;
                Buffer.BlockCopy(_buffer, _start, _buffer, 0, len);
                _start = 0;
                _end = len;
            }

            var required = _end + incoming;
            if (required <= _buffer.Length) return;

            var newSize = _buffer.Length;
            while (newSize < required) newSize <<= 1;

            var bigger = ArrayPool<byte>.Shared.Rent(newSize);
            Buffer.BlockCopy(_buffer, _start, bigger, 0, _end - _start);
            _end -= _start;
            _start = 0;
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = bigger;
        }

        public void Clear()
        {
            if (_buffer != null)
            {
                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = null;
            }
            _start = 0;
            _end = 0;
        }
    }
}
```

- [ ] **Step 3: Replace the `MemoryStream` field in `BaseCoder`**

In `Src/SAEA.Sockets/Base/BaseCoder.cs`, replace:

```csharp
        // 定义一个私有字段 _buffer，用于存储接收到的数据
        private MemoryStream _buffer = new MemoryStream();
```

with:

```csharp
        /// <summary>
        /// 单帧最大帧体长度，可由部署方收紧
        /// </summary>
        public static int MaxFrameLength { get; set; } = int.MaxValue - P_Head;

        // 增量式拆帧内核
        private FrameDecoder _decoder = new FrameDecoder(MaxFrameLength);
```

- [ ] **Step 4: Replace the `Decode(ReadOnlySpan<byte>)` body**

Replace the whole body of the existing `Decode(ReadOnlySpan<byte> data, ...)` method (from `OnReceiveSpan?.Invoke(data);` through the closing `return result;`) with:

```csharp
            OnReceiveSpan?.Invoke(data);

            var result = new List<ISocketProtocal>();

            _decoder.Append(data);

            while (_decoder.TryReadFrame(out var kind, out var frame, out var fileContent, out var heartAt))
            {
                switch (kind)
                {
                    case FrameKind.Heart:
                        onHeart?.Invoke(heartAt);
                        break;
                    case FrameKind.File:
                        onFile?.Invoke(fileContent);
                        break;
                    case FrameKind.Data:
                        var content = frame.Content.Length == 0 ? Array.Empty<byte>() : frame.Content.ToArray();
                        result.Add(new BaseSocketProtocal() { BodyLength = frame.BodyLength, Type = frame.Type, Content = content });
                        break;
                }
            }

            return result;
```

- [ ] **Step 5: Replace `Clear()` and delete the now-unused private helpers**

Replace the existing `Clear()` body with:

```csharp
            _decoder?.Clear();
```

Delete these now-unused private methods entirely: `ReadLengthFromBuffer`, `ReadContentFromBufferSpan`, `ReadContentFromBuffer`, `RemoveFromBuffer`.

- [ ] **Step 6: Build**

Run: `dotnet build Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug --nologo`
Expected: Build succeeded, no `CS0169`-style unused-field errors.

- [ ] **Step 7: Run all tests (parity must hold)**

Run: `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug -- --all`
Expected: `0 failed`; `StreamDecoderTest` parity cases `[PASS]`; total unchanged from Task 1.

- [ ] **Step 8: Commit**

```bash
git add Src/SAEA.Sockets/Base/SocketFrame.cs Src/SAEA.Sockets/Base/FrameDecoder.cs Src/SAEA.Sockets/Base/BaseCoder.cs
git commit -m "perf: replace MemoryStream decoder with pooled incremental FrameDecoder"
```

---

## Task 3: Malformed-length guard tests

**Files:**
- Modify: `Src/SAEA.P2PTest/Tests/StreamDecoderTest.cs`

- [ ] **Step 1: Add the tests and register them**

In `Run()`, add `MalformedLength();` before `Parity();`.

Add this method to `StreamDecoderTest`:

```csharp
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
```

Add `using SAEA.Sockets.Model;` if not already present (it is present in Task 1's file).

- [ ] **Step 2: Run**

Run: `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug -- --all`
Expected: Both malformed cases `[PASS]`, `0 failed`.

- [ ] **Step 3: Commit**

```bash
git add Src/SAEA.P2PTest/Tests/StreamDecoderTest.cs
git commit -m "test: cover malformed frame length guard"
```

---

## Task 4: Add zero-copy `DecodeStream` and `IFrameHandler`

**Files:**
- Create: `Src/SAEA.Sockets/Interface/IFrameHandler.cs`
- Modify: `Src/SAEA.Sockets/Base/BaseCoder.cs`
- Modify: `Src/SAEA.P2PTest/Tests/StreamDecoderTest.cs`

- [ ] **Step 1: Create `IFrameHandler`**

```csharp
// Src/SAEA.Sockets/Interface/IFrameHandler.cs
namespace SAEA.Sockets.Interface
{
    /// <summary>
    /// 零拷贝帧回调。frame.Content 仅在本次调用期间有效。
    /// </summary>
    public interface IFrameHandler
    {
        /// <summary>
        /// 收到完整数据帧
        /// </summary>
        void OnFrame(in SAEA.Sockets.Base.SocketFrame frame);
    }
}
```

- [ ] **Step 2: Write the failing test**

In `Run()`, add `ZeroCopyStream();` before `Parity();`.

Add to `StreamDecoderTest`:

```csharp
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
```

Add `using SAEA.Sockets.Interface;` to the test file if missing.

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet build Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug --nologo`
Expected: FAIL with `error CS1061: 'BaseCoder' does not contain a definition for 'DecodeStream'`.

- [ ] **Step 4: Implement `DecodeStream`**

Add to `BaseCoder` (after the `Decode(ReadOnlySpan<byte>)` method):

```csharp
        /// <summary>
        /// 零拷贝流式解码：帧体以切片交付，仅在 handler 回调期间有效。
        /// </summary>
        public void DecodeStream(ReadOnlySpan<byte> data, IFrameHandler handler, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            OnReceiveSpan?.Invoke(data);

            _decoder.Append(data);

            while (_decoder.TryReadFrame(out var kind, out var frame, out var fileContent, out var heartAt))
            {
                switch (kind)
                {
                    case FrameKind.Heart:
                        onHeart?.Invoke(heartAt);
                        break;
                    case FrameKind.File:
                        onFile?.Invoke(fileContent);
                        break;
                    case FrameKind.Data:
                        handler.OnFrame(in frame);
                        break;
                }
            }
        }
```

- [ ] **Step 5: Run tests**

Run: `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug -- --all`
Expected: `ZeroCopyStream` cases `[PASS]`, `0 failed`.

- [ ] **Step 6: Commit**

```bash
git add Src/SAEA.Sockets/Interface/IFrameHandler.cs Src/SAEA.Sockets/Base/BaseCoder.cs Src/SAEA.P2PTest/Tests/StreamDecoderTest.cs
git commit -m "feat: add zero-copy DecodeStream and IFrameHandler"
```

---

## Task 5: Add stateless `Decode(ReadOnlySequence<byte>)`

**Files:**
- Modify: `Src/SAEA.Sockets/Base/BaseCoder.cs`
- Modify: `Src/SAEA.P2PTest/Tests/StreamDecoderTest.cs`

- [ ] **Step 1: Write the failing test**

In `Run()`, add `MultiSegmentSequence();` before `Parity();`.

Add:

```csharp
        static void MultiSegmentSequence()
        {
            TestHarness.Section("multi-segment ReadOnlySequence");

            var payload1 = Encoding.UTF8.GetBytes("segment-one");
            var payload2 = Encoding.UTF8.GetBytes("segment-two");
            var all = new List<byte>();
            all.AddRange(BuildFrame((byte)SocketProtocalType.RequestSend, payload1));
            all.AddRange(BuildFrame((byte)SocketProtocalType.ChatMessage, payload2));

            var first = new ReadOnlyMemory<byte>(all.ToArray(), 0, 7);
            var second = new ReadOnlyMemory<byte>(all.ToArray(), 7, all.Count - 7);
            var sequence = new System.Buffers.ReadOnlySequence<byte>(new[] { first, second });

            var decoded = new BaseCoder().Decode(sequence);

            TestHarness.Expect(decoded.Count == 2, "two frames from multi-segment sequence", decoded.Count.ToString());
            TestHarness.Expect(decoded.Count == 2 && decoded[0].Content.SequenceEqual(payload1), "first segment payload");
            TestHarness.Expect(decoded.Count == 2 && decoded[1].Content.SequenceEqual(payload2), "second segment payload");
        }
```

Add `using System.Buffers;` to the test file if missing.

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet build Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug --nologo`
Expected: FAIL with `error CS1501`/`CS1061` because no `Decode(ReadOnlySequence<byte>, ...)` overload exists.

- [ ] **Step 3: Implement the overload**

Add to `BaseCoder` and add `using System.Buffers;` and `using System.Buffers.Binary;` to the file:

```csharp
        /// <summary>
        /// 无状态解码：直接在只读序列上解析（支持多段），帧体物化为精确 byte[]。
        /// </summary>
        public List<ISocketProtocal> Decode(ReadOnlySequence<byte> data, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
        {
            var result = new List<ISocketProtocal>();
            var reader = new SequenceReader<byte>(data);
            Span<byte> header = stackalloc byte[P_Head];

            while (reader.Remaining >= P_Head)
            {
                if (!reader.TryCopyTo(header)) break;

                var bodyLen = BinaryPrimitives.ReadInt64LittleEndian(header);
                var type = header[P_LEN];

                if (bodyLen < 0 || bodyLen > MaxFrameLength)
                    throw new KernelException($"非法的数据帧长度: {bodyLen}");

                if (bodyLen == 0 && type == (byte)SocketProtocalType.Heart)
                {
                    reader.Advance(P_Head);
                    onHeart?.Invoke(DateTimeHelper.Now);
                    continue;
                }

                if (reader.Remaining < P_Head + bodyLen) break;

                reader.Advance(P_Head);

                byte[] content;
                if (bodyLen == 0)
                {
                    content = Array.Empty<byte>();
                }
                else
                {
                    content = reader.Sequence.Slice(reader.Position, bodyLen).ToArray();
                    reader.Advance(bodyLen);
                }

                if (type == (byte)SocketProtocalType.BigData)
                    onFile?.Invoke(content);
                else
                    result.Add(new BaseSocketProtocal() { BodyLength = bodyLen, Type = type, Content = content });
            }

            return result;
        }
```

- [ ] **Step 4: Run tests**

Run: `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug -- --all`
Expected: `MultiSegmentSequence` cases `[PASS]`, `0 failed`.

- [ ] **Step 5: Commit**

```bash
git add Src/SAEA.Sockets/Base/BaseCoder.cs Src/SAEA.P2PTest/Tests/StreamDecoderTest.cs
git commit -m "feat: add stateless Decode(ReadOnlySequence<byte>) overload"
```

---

## Task 6: IOCP client Span event and conditional legacy delivery

**Files:**
- Create: `Src/SAEA.Sockets/Handler/OnClientReceiveSpanHandler.cs`
- Modify: `Src/SAEA.Sockets/Core/Tcp/IocpClientSocket.cs`
- Modify: `Src/SAEA.P2PTest/Tests/StreamDecoderTest.cs`
- Modify: `Src/SAEA.P2PTest/Program.cs`

- [ ] **Step 1: Create the handler delegate**

```csharp
// Src/SAEA.Sockets/Handler/OnClientReceiveSpanHandler.cs
using System;

namespace SAEA.Sockets.Handler
{
    /// <summary>
    /// 客户端接收数据（Span 版本）。data 仅在回调期间有效。
    /// </summary>
    public delegate void OnClientReceiveSpanHandler(ReadOnlySpan<byte> data);
}
```

- [ ] **Step 2: Update `IocpClientSocket`**

Add `using SAEA.Sockets.Handler;` to the file.

Add a field next to `OnClientReceive`:

```csharp
        private readonly bool _isBaseClientType;
```

Set it in the constructor `public IocpClientSocket(ISocketOption socketOption)` (after `Context = socketOption.Context;`):

```csharp
            _isBaseClientType = GetType() == typeof(IocpClientSocket);
```

Replace the internal delegate + event (lines declaring `internal delegate void OnClientReceiveSpanHandler(...)` and `internal event ... OnClientReceiveSpan;`) with:

```csharp
        /// <summary>
        /// 接收数据事件（Span 版本）。data 仅在回调期间有效。
        /// </summary>
        public event OnClientReceiveSpanHandler OnClientReceiveSpan;
```

> Note: `UdpClientSocket` (out of scope) declares its own nested `OnClientReceiveSpanHandler`. A nested type shadows an imported namespace type, so that file still compiles unchanged; only `IocpClientSocket`'s nested declaration is removed so its event binds to the new public `SAEA.Sockets.Handler.OnClientReceiveSpanHandler`.

In `ProcessReceived`, replace this block:

```csharp
                        // 使用Span获取数据，避免立即复制
                        var dataSpan = readArgs.Buffer.AsSpan(readArgs.Offset, readArgs.BytesTransferred);

                        // 触发内部Span事件
                        OnClientReceiveSpan?.Invoke(dataSpan);

                        // 复制到精确大小的数组，避免内存池返回的超大数组导致下游逻辑错误
                        var buffer = dataSpan.ToArray();

                        try
                        {
                            OnClientReceive?.Invoke(buffer);
                        }
                        catch (Exception ex)
                        {
                            OnError?.Invoke(UserToken.ID, ex);
                        }
```

with:

```csharp
                        // 使用Span获取数据，避免立即复制
                        var dataSpan = readArgs.Buffer.AsSpan(readArgs.Offset, readArgs.BytesTransferred);

                        // 零拷贝路径：始终触发（无分配）
                        OnClientReceiveSpan?.Invoke(dataSpan);

                        // 兼容路径：仅当存在 byte[] 消费方时才复制
                        if (!_isBaseClientType || OnReceive != null)
                        {
                            var buffer = dataSpan.ToArray();

                            try
                            {
                                OnClientReceive?.Invoke(buffer);
                            }
                            catch (Exception ex)
                            {
                                OnError?.Invoke(UserToken.ID, ex);
                            }
                        }
```

- [ ] **Step 3: Write the failing client test**

Do **not** call the async method from `Run()`; it is registered in `Program.cs` (Step 4) so it runs exactly once and its results roll into the global `--all` summary. Keep `Run()` synchronous. Add these members to `StreamDecoderTest`:

```csharp
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
```

- [ ] **Step 4: Register in `Program.cs`**

In `RunAllAsync()`, add after `StreamDecoderTest.Run();`:

```csharp
            await StreamDecoderTest.RunIocpClientAsync();
```

- [ ] **Step 5: Build and run**

Run: `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug -- --all`
Expected: `client span event receives frame bytes` and `client OnReceive still delivers bytes` `[PASS]`, `0 failed`.

- [ ] **Step 6: Commit**

```bash
git add Src/SAEA.Sockets/Handler/OnClientReceiveSpanHandler.cs Src/SAEA.Sockets/Core/Tcp/IocpClientSocket.cs Src/SAEA.P2PTest/Tests/StreamDecoderTest.cs Src/SAEA.P2PTest/Program.cs
git commit -m "feat: expose IocpClientSocket span event and skip legacy copy when unused"
```

---

## Task 7: IOCP server Span event and conditional legacy delivery

**Files:**
- Create: `Src/SAEA.Sockets/Handler/OnServerReceiveSpanHandler.cs`
- Modify: `Src/SAEA.Sockets/Core/Tcp/IocpServerSocket.cs`
- Modify: `Src/SAEA.P2PTest/Tests/StreamDecoderTest.cs`
- Modify: `Src/SAEA.P2PTest/Program.cs`

- [ ] **Step 1: Create the handler delegate**

```csharp
// Src/SAEA.Sockets/Handler/OnServerReceiveSpanHandler.cs
using System;
using SAEA.Sockets.Interface;

namespace SAEA.Sockets.Handler
{
    /// <summary>
    /// 服务端接收数据（Span 版本）。data 仅在回调期间有效。
    /// </summary>
    public delegate void OnServerReceiveSpanHandler(IUserToken userToken, ReadOnlySpan<byte> data);
}
```

- [ ] **Step 2: Update `IocpServerSocket`**

Add `using SAEA.Sockets.Handler;` to the file.

Add a field near the other fields (after `private OnServerReceiveBytesHandler OnServerReceiveBytes;`):

```csharp
        private readonly bool _isBaseServerType;
```

Set it in the constructor `public IocpServerSocket(ISocketOption socketOption)` (before `OnServerReceiveBytes = new OnServerReceiveBytesHandler(OnReceiveBytes);`):

```csharp
            _isBaseServerType = GetType() == typeof(IocpServerSocket);
```

Add the public event next to `public event OnReceiveHandler OnReceive;`:

```csharp
        /// <summary>
        /// 接收数据事件（Span 版本）。data 仅在回调期间有效。
        /// </summary>
        public event OnServerReceiveSpanHandler OnServerReceiveSpan;
```

In `ProcessReceived`, replace:

```csharp
                        userToken.Actived = DateTimeHelper.Now;
                        var buffer = readArgs.Buffer.AsSpan().Slice(readArgs.Offset, readArgs.BytesTransferred).ToArray();
                        _sessionManager.Active(userToken.ID);
                        OnServerReceiveBytes.Invoke(userToken, buffer);
```

with:

```csharp
                        userToken.Actived = DateTimeHelper.Now;
                        var dataSpan = readArgs.Buffer.AsSpan().Slice(readArgs.Offset, readArgs.BytesTransferred);
                        _sessionManager.Active(userToken.ID);

                        OnServerReceiveSpan?.Invoke(userToken, dataSpan);

                        if (!_isBaseServerType || OnReceive != null)
                            OnServerReceiveBytes.Invoke(userToken, dataSpan.ToArray());
```

- [ ] **Step 3: Write the failing server test**

Do **not** call the async method from `Run()`; it is registered in `Program.cs` (Step 4) so it runs exactly once. Add:

```csharp
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
```

- [ ] **Step 4: Register in `Program.cs`**

In `RunAllAsync()`, add after the client call:

```csharp
            await StreamDecoderTest.RunIocpServerAsync();
```

- [ ] **Step 5: Build and run**

Run: `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug -- --all`
Expected: `server span event receives frame bytes` `[PASS]`, `0 failed`.

- [ ] **Step 6: Commit**

```bash
git add Src/SAEA.Sockets/Handler/OnServerReceiveSpanHandler.cs Src/SAEA.Sockets/Core/Tcp/IocpServerSocket.cs Src/SAEA.P2PTest/Tests/StreamDecoderTest.cs Src/SAEA.P2PTest/Program.cs
git commit -m "feat: expose IocpServerSocket span event and skip legacy copy when unused"
```

---

## Task 8: Benchmark legacy vs new vs zero-copy decode

**Files:**
- Modify: `Src/SAEA.P2PTest/Tests/PerformanceTest.cs`

- [ ] **Step 1: Add the benchmark section**

Add `using SAEA.Sockets.Base;`, `using SAEA.Sockets.Model;`, and `using System.Buffers;` to the file.

In `Run()`, add `StreamingDecodeBenchmark();` after `RelayEncodeThroughput();`.

Add these members:

```csharp
        static void StreamingDecodeBenchmark()
        {
            TestHarness.Section("streaming zero-copy decode");

            foreach (var size in new[] { 64, 4 * 1024, 1024 * 1024 })
            {
                var content = new byte[size];
                new Random(7).NextBytes(content);
                var frame = new BaseSocketProtocal
                {
                    BodyLength = size,
                    Type = (byte)SocketProtocalType.RequestSend,
                    Content = content
                }.ToBytes();

                int iterations = size >= 1024 * 1024 ? 100 : 20000;

                var legacyOps = MeasureOps(iterations, () => LegacyDecoder.Decode(frame), out var legacyAlloc);

                // 每次迭代同一帧都会被完整消费，复用 coder 以隔离拆帧内核的开销（不把构造 allocation 计入）。
                var pooledCoder = new BaseCoder();
                var newOps = MeasureOps(iterations, () => pooledCoder.Decode(frame), out var newAlloc);

                var streamingCoder = new BaseCoder();
                var handler = new CountingHandler();
                var streamOps = MeasureOps(iterations, () => streamingCoder.DecodeStream(frame, handler), out var streamAlloc);

                ConsoleHelper.WriteLine($"[{size,7}B] legacy {legacyOps:F0} ops/s ({legacyAlloc} B/op) | " +
                    $"new {newOps:F0} ops/s ({newAlloc} B/op) | stream {streamOps:F0} ops/s ({streamAlloc} B/op)");

                TestHarness.Expect(handler.TotalBytes > 0, $"stream benchmark decoded {size}B payload");
            }
        }

        static double MeasureOps(int iterations, Action action, out long bytesPerOp)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++) action();
            sw.Stop();
            long delta = GC.GetAllocatedBytesForCurrentThread() - before;
            bytesPerOp = delta / iterations;
            return iterations / sw.Elapsed.TotalSeconds;
        }

        sealed class CountingHandler : SAEA.Sockets.Interface.IFrameHandler
        {
            public long TotalBytes;

            public void OnFrame(in SocketFrame frame)
            {
                TotalBytes += frame.Content.Length;
            }
        }
```

- [ ] **Step 2: Run**

Run: `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug -- --all`
Expected: Prints three benchmark lines (allocations for `stream` far below `legacy`/`new`), all `stream benchmark decoded ...B payload` `[PASS]`, `0 failed`.

- [ ] **Step 3: Update the README performance section**

In both `Src/SAEA.P2P/README.md` and `Src/SAEA.P2P/README.en.md`, inside the existing `Component Benchmarks (SAEA.P2PTest)` list, add a bullet with the observed zero-copy numbers printed in Step 2. Use the actual values from the run, e.g.:

```markdown
- Streaming zero-copy decode (`DecodeStream`): <N> ops/sec for 64B, <N> ops/sec for 4KB, ~0 B/op allocation
```

Run: `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug -- --all` (confirm still `0 failed`).

- [ ] **Step 4: Commit**

```bash
git add Src/SAEA.P2PTest/Tests/PerformanceTest.cs Src/SAEA.P2P/README.md Src/SAEA.P2P/README.en.md
git commit -m "bench: add legacy vs pooled vs zero-copy decode benchmark"
```

---

## Final Verification

- [ ] **Full regression**

Run: `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug -- --all`
Expected: `=== ALL ADVANCED TESTS: <N>/<N> passed, 0 failed ===` with `N > 189`.

- [ ] **Library build (netstandard2.0)**

Run: `dotnet build Src/SAEA.Sockets/SAEA.Sockets.csproj -c Release --nologo`
Expected: Build succeeded (confirms `ReadOnlySequence`/`SequenceReader`/`stackalloc` compile on netstandard2.0 / C# 8).

- [ ] **Confirm spec coverage**

Verify each spec section maps to completed work:
- §2 components: Tasks 2, 4 (`FrameDecoder`, `SocketFrame`, `IFrameHandler`).
- §3 additive API: Tasks 2, 4, 5.
- §4 IOCP wiring: Tasks 6, 7.
- §5 malformed guard: Task 3.
- §6 tests + benchmark: Tasks 1, 3, 4, 5, 6, 7, 8.
- §7 risks covered by parity/lifecycle/multi-segment tests.