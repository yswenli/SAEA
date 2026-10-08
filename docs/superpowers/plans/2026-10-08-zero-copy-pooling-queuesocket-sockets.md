# Zero-Copy Pooling — Flagship (SAEA.Sockets send surface + SAEA.QueueSocket) Implementation Plan

> **For the implementer (subagent-driven):** REQUIRED SUB-SKILL: use `subagent-driven-development` to implement this plan task-by-task. Follow the no-comments policy strictly (no new `//` line comments; XML doc `///` allowed; do not delete existing comments). Never `git add -A`/`git add .`; stage explicit paths and verify with `git diff --cached --name-only` first.

**Spec:** `docs/superpowers/specs/2026-09-23-zero-copy-pooling-unification-design.md` (approved, commit `109cf417` — including chapter 十四 deep-review corrections and rulings).

**Goal:** Remove per-frame heap allocations (`new byte[]`, `ToArray()`, `List<byte[]>` concatenation, per-frame `List<QueueMsg>`) from the QueueSocket send/receive hot path by routing buffers through `MemoryPoolManager`, introducing pooled batchers, adding owner-aware async send overloads to the socket layer, and making the client write gate strictly mutually exclusive. After the change a steady QueueSocket workload must be per-frame zero-GC, with pooled buffers returned exactly once.

**Scope of THIS plan (flagship):** `SAEA.Sockets` send surface + `SAEA.QueueSocket` full chain + new `SAEA.Common/Caching/{PooledBatcher,PooledClassificationBatcher}`. This is the P0–P5 + P7 group of the spec.

**Deferred to separate plans (P6):** the other 16 consumers (RPC, P2P, Http, MVC, MQTT, DNS, WebSocket, Socket5, Audio.Net, FileSocket, MessageSocket, RedisSocket, FTP). They are independent subsystems; per the spec each gets its own plan producing independently testable software. Do NOT touch them in this plan.

**Tech stack:** C# (LangVersion 8.0), netstandard2.0 for `SAEA.Common`/`SAEA.Sockets`/`SAEA.QueueSocket`; net10.0 for `SAEA.QueueSocketTest`. Build with `dotnet build Src/SAEA.Sockets.sln -c Release`.

## ns2.0 API constraints (re-check every task)

Unavailable: `SequenceReader<T>`, `Encoding.GetString/GetBytes(ReadOnlySpan)`, `Socket.Send(Span)`, `ArrayBufferWriter<T>`, target-typed `new`, records, init-only, switch expressions, `is not`, `ReadOnlySequence<T>.FirstSpan`.
Available: `MemoryMarshal`, `Span.IndexOf`, `BinaryPrimitives`, `Buffer.BlockCopy`, `Encoding.UTF8.GetString(byte[], int, int)`, implicit `byte[]`→`ReadOnlyMemory<byte>` conversion.
`ReadOnlySequence<T>` first segment: use `data.First.Span` (not `.FirstSpan`).

## Ownership invariants (must hold at all times)

- **INV-1** wire format unchanged: `[1B Type][4B Total][4B NameLen][Name][4B TopicLen][Topic][Data]`, little-endian, `Total = 12 + NameLen + TopicLen + DataLen`.
- **INV-2** every owner is returned exactly once; on timeout/abandon, ownership is dropped (no return).
- **INV-3** a buffer must stay alive until the send completes.
- **INV-4** public compatibility surface: only additive method changes, except the explicitly-approved `QueueMsg.Data`/`QueueSocketMsg.Data`→`ReadOnlyMemory<byte>` break.
- **INV-5** diagnostic conservation: after quiescence, `MemoryPoolManager.GetStatistics()` each layer `Rented == Returned`.
- **R1** single owner. **R2** receive payload: `RentPooled(dlen)` → `qm.Data = pb.AsMemory()`, owner on `QueueMsg`; `AcceptPublish` transfers owner via `DetachOwner()`; all other branches return at end of frame handling. **R3** dispatch buffer writer is the owner handed to `SendAsync`, returned in `ProcessSended`. **R4** timeout uses `AbandonSendingOwner`. **R5** client concat writer returned via IOCP `SendAsync(...,writer)`. **R6** failure must Dispose the owner.

## Acceptance gates (run after EACH task unless noted)

1. `dotnet build Src/SAEA.Sockets.sln -c Debug` → 0 errors.
2. `dotnet build Src/SAEA.Sockets.sln -c Release` → 0 errors.
3. No new `//`: `git diff -U0 -- '*.cs' | Select-String '^\+' | Where-Object { $_ -match '//' -and $_ -notmatch '///' }` → empty.
4. No public/protected signature diff except the approved `Data` type change (Task 8) and the new additive overloads.
5. Functional + full suites (after Tasks that change behavior):
   `dotnet run --project Src/SAEA.QueueSocketTest -c Release -- --functional` → 37/37 (plus new FT-Pool tests).
   `dotnet run --project Src/SAEA.QueueSocketTest -c Release -- --all` → all pass.
   `dotnet run --project Src/SAEA.P2PTest -c Release -- --all` → 310/310.
   `dotnet run --project Src/SAEA.P2PTest -c Release -- --bench-iocp` → 29/29.

## File map (flagship)

| Area | Files |
|---|---|
| New pooled batchers | `Src/SAEA.Common/Caching/PooledBatcher.cs` (new), `Src/SAEA.Common/Caching/PooledClassificationBatcher.cs` (new) |
| Socket interfaces | `Src/SAEA.Sockets/IServerSocket.cs`, `Src/SAEA.Sockets/IClientSocket.cs` |
| User token / write gate | `Src/SAEA.Sockets/Base/BaseUserToken.cs` |
| IOCP server | `Src/SAEA.Sockets/Core/Tcp/IocpServerSocket.cs` |
| IOCP client | `Src/SAEA.Sockets/Core/Tcp/IocpClientSocket.cs` |
| Stream/UDP sockets | `Src/SAEA.Sockets/Core/Tcp/StreamServerSocket.cs`, `StreamClientSocket.cs`, `Core/Udp/UdpServerSocket.cs`, `UdpClientSocket.cs` |
| QueueSocket coder | `Src/SAEA.QueueSocket/Net/QueueCoder.cs` |
| QueueSocket model | `Src/SAEA.QueueSocket/Model/QueueMsg.cs`, `QueueMsgPool.cs`, `QueueMsgListPool.cs`, `MessageQueue.cs`, `Exchange.cs` |
| QueueSocket protocal | `Src/SAEA.QueueSocket/Net/QueueSocketMsg.cs` |
| QueueSocket endpoints | `Src/SAEA.QueueSocket/QServer.cs`, `QClient.cs` |
| Tests + docs | `Src/SAEA.QueueSocketTest/{FunctionalTests.cs,TestHarness.cs,Program.cs,QueueBenchmark.cs}`, `Src/SAEA.QueueSocket/README.md`, `README.en.md` |

## Task order rationale

`PooledBatcher` (Task 1) is a leaf and can be built/tested alone. Interface overloads (Tasks 2–3) unlock owner sends. Write gate (Task 4) must precede client sync-send de-`ToArray()` to stay correct (Q10=A). QueueSocket model/coder (Tasks 5–7) then endpoints (Tasks 8–9). Pool tests and gates last (Task 10–11).

---

## Task 1: `PooledBatcher` + `PooledClassificationBatcher` (SAEA.Common)

**Files:**
- Create: `Src/SAEA.Common/Caching/PooledBatcher.cs`
- Create: `Src/SAEA.Common/Caching/PooledClassificationBatcher.cs`
- Modify: `Src/SAEA.Common/Caching/PooledBuffer.cs` (returned-counter hook)
- Modify: `Src/SAEA.Common/Caching/MemoryPoolManager.cs` (add `NotifyReturned`)
- Test: `Src/SAEA.QueueSocketTest/FunctionalTests.cs` (add FT-Pool-Batch)

**Interfaces (consumed by later tasks):**
- `public delegate void OnPooledBatchedHandler(PooledBufferWriter batch, int count);`
- `public sealed class PooledBatcher : IDisposable`
  - `PooledBatcher(int size = 1000, int timeout = 1000, int max = -1, string name = "")`
  - `bool Insert(PooledBufferWriter writer)` — takes ownership; returns `false` when stopped or at capacity (caller must Dispose the writer).
  - `void Clear()` — drains and Dispose()es every queued writer, no callback.
  - `void Dispose()` — stop, `Clear()`; the capacity semaphore is intentionally never disposed (see XML note).
  - `event OnPooledBatchedHandler OnBatched` — invoked with a *merged* writer that owns all queued buffers plus the number of merged writers (`count`); if no handler, the merged writer is disposed.
  - `string Name { get; }`, `int PendingCount { get; }`.
- `public delegate void OnPooledClassificationBatchedHandler(string id, PooledBufferWriter batch, int count);`
- `public sealed class PooledClassificationBatcher : IDisposable`
  - `PooledClassificationBatcher(int size = 1000, int timeout = 1000, int max = -1)`
  - `bool Insert(string id, PooledBufferWriter writer)`
  - `void Clear(string id)`
  - `void Dispose()`

**Contract notes (document as XML `///`, not `//`):**
- `Insert` ownership transfer: the batcher owns the writer after a `true` return; on `false` the caller still owns it.
- `Dispose` drains without invoking `OnBatched` (lossy, leak-free). This intentionally differs from the legacy `Batcher.Dispose`, because QClient unsubscribes its handler before Dispose — firing the callback would leak.
- Merging concatenates `WrittenSpan` of each queued writer into one `PooledBufferWriter`; queued writers are disposed right after their bytes are copied.

- [ ] **Step 1: Create `PooledBatcher.cs`.**

```csharp
using SAEA.Common.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace SAEA.Common.Caching
{
    /// <summary>
    /// 池化批处理器：收集 PooledBufferWriter，满 size 或超时后合并为一个 writer 并回调。
    /// 所有权约定：Insert 返回 true 后由本类持有 writer；返回 false 时调用方仍持有并须自行 Dispose。
    /// </summary>
    public delegate void OnPooledBatchedHandler(PooledBufferWriter batch, int count);

    /// <summary>
    /// 池化批处理器。与旧 <see cref="Batcher"/> 不同，承载的是 <see cref="PooledBufferWriter"/> 而非 byte[]；
    /// Dispose/Clear 会 Dispose 队列内所有 writer 且不触发回调（避免所有权悬空与泄漏）。
    /// 注意：_capacitySemaphore 永不 Dispose——后台 Handler 的 Flush 可能与 Dispose 并发 Release，
    /// 释放信号量会抛 ObjectDisposedException；SemaphoreSlim 不释放仅占用极小托管内存。
    /// </summary>
    public sealed class PooledBatcher : IDisposable
    {
        readonly int _size;
        readonly int _timeout;
        readonly int _max;
        readonly ConcurrentQueue<PooledBufferWriter> _queue;
        readonly SemaphoreSlim _capacitySemaphore;
        readonly object _sync = new object();
        volatile bool _stopped;

        public event OnPooledBatchedHandler OnBatched;

        public string Name { get; private set; }

        public int PendingCount { get { return _queue.Count; } }

        public PooledBatcher(int size = 1000, int timeout = 1000, int max = -1, string name = "")
        {
            _size = size > 0 ? size : 1000;
            _timeout = timeout > 0 ? timeout : 1000;
            _max = max == -1 ? _size * 10 : max;
            if (_max < _size) throw new ArgumentOutOfRangeException(nameof(max), "max不能小于size");
            Name = name ?? string.Empty;
            _queue = new ConcurrentQueue<PooledBufferWriter>();
            _capacitySemaphore = new SemaphoreSlim(_max, _max);
            _stopped = false;
            TaskHelper.LongRunning(Handler);
        }

        public bool Insert(PooledBufferWriter writer)
        {
            if (writer == null) return false;
            lock (_sync)
            {
                if (_stopped) return false;
                if (!_capacitySemaphore.Wait(0)) return false;
                _queue.Enqueue(writer);
                return true;
            }
        }

        void Handler()
        {
            var stopwatch = Stopwatch.StartNew();
            while (!_stopped)
            {
                var count = _queue.Count;
                if (count >= _size || (count > 0 && stopwatch.ElapsedMilliseconds >= _timeout))
                {
                    try { Flush(); } catch { }
                    stopwatch.Restart();
                }
                else
                {
                    ThreadHelper.Sleep(Math.Min(_timeout, 100));
                }
            }
        }

        void Flush()
        {
            var list = new List<PooledBufferWriter>();
            var take = Math.Min(_queue.Count, _size);
            for (var i = 0; i < take; i++)
            {
                if (_queue.TryDequeue(out var w))
                {
                    list.Add(w);
                    try { _capacitySemaphore.Release(); } catch (SemaphoreFullException) { }
                }
            }
            if (list.Count == 0) return;

            long total = 0;
            for (var i = 0; i < list.Count; i++) total += list[i].WrittenCount;

            PooledBufferWriter merged = null;
            try
            {
                if (total <= 0 || total > int.MaxValue) return;
                merged = new PooledBufferWriter((int)total);
                for (var i = 0; i < list.Count; i++)
                {
                    var span = list[i].WrittenSpan;
                    span.CopyTo(merged.GetSpan(span.Length));
                    merged.Advance(span.Length);
                }
                var handler = OnBatched;
                if (handler != null)
                {
                    handler(merged, list.Count);
                    merged = null;
                }
            }
            finally
            {
                if (merged != null) merged.Dispose();
                for (var i = 0; i < list.Count; i++) { try { list[i].Dispose(); } catch { } }
            }
        }

        public void Clear()
        {
            lock (_sync)
            {
                DrainLocked();
            }
        }

        void DrainLocked()
        {
            while (_queue.TryDequeue(out var w))
            {
                try { w.Dispose(); } catch { }
                try { _capacitySemaphore.Release(); } catch (SemaphoreFullException) { }
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                _stopped = true;
                DrainLocked();
            }
        }
    }
}
```

- [ ] **Step 2: Create `PooledClassificationBatcher.cs`.**

```csharp
using System;
using System.Collections.Concurrent;

namespace SAEA.Common.Caching
{
    /// <summary>
    /// 按 id 分类的池化批处理器。每个 id 一个 <see cref="PooledBatcher"/>，回调携带 id。
    /// </summary>
    public delegate void OnPooledClassificationBatchedHandler(string id, PooledBufferWriter batch, int count);

    /// <summary>
    /// 池化分类批处理器，替代全局单例 <see cref="ClassificationBatcher"/> 以解除跨子系统参数耦合。
    /// </summary>
    public sealed class PooledClassificationBatcher : IDisposable
    {
        readonly ConcurrentDictionary<string, Lazy<PooledBatcher>> _dic;
        readonly int _size;
        readonly int _timeout;
        readonly int _max;
        volatile bool _disposed;

        public event OnPooledClassificationBatchedHandler OnBatched;

        public PooledClassificationBatcher(int size = 1000, int timeout = 1000, int max = -1)
        {
            _size = size > 0 ? size : 1000;
            _timeout = timeout > 0 ? timeout : 1000;
            _max = max == -1 ? _size * 10 : max;
            if (_max < _size) throw new ArgumentOutOfRangeException(nameof(max), "max不能小于size");
            _dic = new ConcurrentDictionary<string, Lazy<PooledBatcher>>();
        }

        PooledBatcher GetOrCreate(string id)
        {
            var lazy = _dic.GetOrAdd(id, n => new Lazy<PooledBatcher>(() =>
            {
                var b = new PooledBatcher(_size, _timeout, _max, n);
                var captured = n;
                b.OnBatched += (w, count) =>
                {
                    var h = OnBatched;
                    if (h != null) h(captured, w, count);
                    else w.Dispose();
                };
                return b;
            }));
            return lazy.Value;
        }

        public bool Insert(string id, PooledBufferWriter writer)
        {
            if (string.IsNullOrEmpty(id) || writer == null) return false;
            if (_disposed) return false;
            return GetOrCreate(id).Insert(writer);
        }

        public void Clear(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (_dic.TryRemove(id, out var lazy) && lazy.IsValueCreated) lazy.Value.Dispose();
        }

        public void Dispose()
        {
            _disposed = true;
            foreach (var kv in _dic)
            {
                try { if (kv.Value.IsValueCreated) kv.Value.Value.Dispose(); } catch { }
            }
            _dic.Clear();
        }
    }
}
```

- [ ] **Step 3: Make `PooledBuffer` returns count toward `MemoryPoolManager` conservation (INV-5).**

  Today `PooledBuffer.Dispose` calls `_pool.Return(Buffer)` directly, so buffers rented via `MemoryPoolManager.RentPooled` (which increments `_*PoolRented`) are never counted as returned — the receive-payload path would leave each layer `Rented != Returned` and fail FT-Pool-2. `PooledBufferWriter` is unaffected (it already routes through `MemoryPoolManager.Rent`/`Return`). Patch minimally:

  In `MemoryPoolManager.cs`, add an `internal` hook (same assembly as `PooledBuffer`) next to `Return` (`:189`):
```csharp
        internal static void NotifyReturned(BufferSizeTier tier)
        {
            switch (tier)
            {
                case BufferSizeTier.Small: Interlocked.Increment(ref _smallPoolReturned); break;
                case BufferSizeTier.Medium: Interlocked.Increment(ref _mediumPoolReturned); break;
                case BufferSizeTier.Large: Interlocked.Increment(ref _largePoolReturned); break;
                default: Interlocked.Increment(ref _smallPoolReturned); break;
            }
        }
```
  Use the exact private counter-field names found in `MemoryPoolManager.cs` at implementation time. In `PooledBuffer.cs` `Dispose`, keep the existing `_pool.Return(Buffer)` (so the `PooledBufferTests` custom-pool/`AreSame` contract is untouched) and add the accounting call:
```csharp
        public void Dispose()
        {
            if (!_disposed)
            {
                _pool.Return(Buffer);
                MemoryPoolManager.NotifyReturned(Tier);
                _disposed = true;
            }
        }
```
  Rationale: for `RentPooled` buffers, `Tier` and `_pool` are the tier's own pool, so the buffer returns to the correct pool and the counter now matches. For arbitrary direct-ctor usage (unit tests) the extra count is harmless because no test asserts cross-path equality there.

- [ ] **Step 4: Build** `dotnet build Src/SAEA.Common/SAEA.Common.csproj -c Release` → 0 errors.

- [ ] **Step 5: Test** — add `FT-Pool-Batch` to `FunctionalTests.cs` and register in `Program.cs` `--functional`/`--all` routing. The test:
  1. `var batcher = new PooledBatcher(2, 100);` subscribe `OnBatched` to capture the merged writer; call `batcher.Insert` with two `PooledBufferWriter`s each containing a known byte string.
  2. Wait until callback fires (poll up to 2s).
  3. Assert merged `WrittenSpan` equals the concatenation; record `MemoryPoolManager.GetStatistics()` before/after; after `batcher.Dispose()` and a GC, assert `Rented == Returned` for small layer.
  4. Exercise the new `NotifyReturned` hook directly: rent `var pooled = MemoryPoolManager.RentPooled(1024)`, capture `SmallPoolReturned` before/after `pooled.Dispose()`, and assert it increased by 1.
  5. Assert `Insert` returns `false` after `Dispose`.

- [ ] **Step 6: Commit**
```
git add Src/SAEA.Common/Caching/PooledBatcher.cs Src/SAEA.Common/Caching/PooledClassificationBatcher.cs Src/SAEA.Common/Caching/PooledBuffer.cs Src/SAEA.Common/Caching/MemoryPoolManager.cs Src/SAEA.QueueSocketTest/FunctionalTests.cs Src/SAEA.QueueSocketTest/Program.cs
git diff --cached --name-only
git commit -m "feat(common): add PooledBatcher, PooledClassificationBatcher and tracked PooledBuffer returns"
```

---

## Task 2: Server owner-aware send overload (`IServerSocket`)

**Files:**
- Modify: `Src/SAEA.Sockets/IServerSocket.cs` (add overload)
- Modify: `Src/SAEA.Sockets/Core/Tcp/IocpServerSocket.cs`
- Modify: `Src/SAEA.Sockets/Core/Tcp/StreamServerSocket.cs`
- Modify: `Src/SAEA.Sockets/Core/Udp/UdpServerSocket.cs`

**Design:** `void SendAsync(string sessionID, ReadOnlyMemory<byte> data, IDisposable owner)`. For IOCP, if `data` is array-backed (`MemoryMarshal.TryGetArray` succeeds) pass the segment + the caller's `owner` down to `SendAsyncRaw` (true zero-copy); the owner is disposed in `ProcessSended`. If `data` is not array-backed, copy into a new `PooledBufferWriter`, dispose the caller's `owner`, and send that writer as the owner. `StreamServerSocket`'s 2-arg `SendAsync` eagerly copies (`data.ToArray()`), so its overload delegates and disposes `owner` immediately. `UdpServerSocket`'s 2-arg `SendAsync` is **zero-copy for array-backed data** (it calls `SendAsyncRaw(userToken, seg, null)` feeding an async `SendToAsync`), so disposing `owner` right after the call would return a pooled array while the send is in flight — its overload must therefore mirror the IOCP logic: pass the segment + `owner` to `SendAsyncRaw`, or copy into a `PooledBufferWriter` (disposing the caller's `owner`) and send that writer as the owner.

- [ ] **Step 1: Add interface method** in `IServerSocket.cs` after the existing `SendAsync(string, ISocketProtocal)` (near `:113`):

```csharp
        /// <summary>
        /// 携带所有权对象的异步发送：发送完成后由实现方归还/释放 <paramref name="owner"/>。
        /// </summary>
        /// <param name="sessionID">会话ID</param>
        /// <param name="data">数据</param>
        /// <param name="owner">数据的所有者，发送完成或失败时释放</param>
        void SendAsync(string sessionID, ReadOnlyMemory<byte> data, IDisposable owner);
```

- [ ] **Step 2: Implement in `IocpServerSocket`** next to `SendAsync(string, ReadOnlyMemory<byte>)` (`:459`). Insert:

```csharp
        public void SendAsync(string sessionID, ReadOnlyMemory<byte> data, IDisposable owner)
        {
            var userToken = _sessionManager.Get(sessionID);
            if (userToken == null)
            {
                owner?.Dispose();
                throw new KernelException("Failed to send data,current session does not exist！");
            }
            SendAsync(userToken, data, owner);
        }

        void SendAsync(IUserToken userToken, ReadOnlyMemory<byte> data, IDisposable owner)
        {
            if (data.Length == 0)
            {
                owner?.Dispose();
                return;
            }
            ArraySegment<byte> rented;
            if (MemoryMarshal.TryGetArray(data, out var segment) && segment.Array != null)
            {
                SendAsyncRaw(userToken, segment, owner);
                return;
            }
            PooledBufferWriter writer = null;
            try
            {
                writer = new PooledBufferWriter(data.Length);
                data.Span.CopyTo(writer.GetSpan(data.Length));
                writer.Advance(data.Length);
            }
            catch
            {
                if (writer != null)
                {
                    writer.Dispose();
                    writer = null;
                }
                owner?.Dispose();
                throw;
            }
            if (!writer.TryGetArray(out rented) || rented.Array == null)
            {
                writer.Dispose();
                writer = null;
                owner?.Dispose();
                return;
            }
            owner?.Dispose();
            SendAsyncRaw(userToken, rented, writer);
        }
```

Reuse the existing `SendAsyncRaw(userToken, ArraySegment<byte>, IDisposable)` overload already present (`:369`). Confirm `using System;`, `System.Runtime.InteropServices;`, `SAEA.Common.Caching;` are present; add only if missing.

- [ ] **Step 3: Implement in `StreamServerSocket`** next to `SendAsync(string, ReadOnlyMemory<byte>)` (`:457`):

```csharp
        public void SendAsync(string sessionID, ReadOnlyMemory<byte> data, IDisposable owner)
        {
            try
            {
                SendAsync(sessionID, data);
            }
            finally
            {
                owner?.Dispose();
            }
        }
```

- [ ] **Step 4: Implement in `UdpServerSocket`** next to `SendAsync(string, ReadOnlyMemory<byte>)` (`:397`). NOTE: unlike Stream, UDP's 2-arg `SendAsync` is zero-copy for array-backed data, so this must hand the owner down to `SendAsyncRaw` (owned until send completion) instead of disposing it immediately:

```csharp
        public void SendAsync(string sessionID, ReadOnlyMemory<byte> data, IDisposable owner)
        {
            if (data.Length == 0)
            {
                owner?.Dispose();
                return;
            }
            var userToken = _sessionManager.Get(sessionID);
            if (userToken == null)
            {
                owner?.Dispose();
                throw new KernelException("Failed to send data,current session does not exist！");
            }
            if (data.Length > Model.SocketOption.UDPMaxLength)
            {
                owner?.Dispose();
                throw new ArgumentException("SendAsync Incorrect length of data sent");
            }
            ArraySegment<byte> rented;
            if (MemoryMarshal.TryGetArray(data, out var segment) && segment.Array != null)
            {
                SendAsyncRaw(userToken, segment, owner);
                return;
            }
            PooledBufferWriter writer = null;
            try
            {
                writer = new PooledBufferWriter(data.Length);
                data.Span.CopyTo(writer.GetSpan(data.Length));
                writer.Advance(data.Length);
            }
            catch
            {
                if (writer != null)
                {
                    writer.Dispose();
                    writer = null;
                }
                owner?.Dispose();
                throw;
            }
            if (!writer.TryGetArray(out rented) || rented.Array == null)
            {
                writer.Dispose();
                writer = null;
                owner?.Dispose();
                return;
            }
            owner?.Dispose();
            SendAsyncRaw(userToken, rented, writer);
        }
```

- [ ] **Step 5: Build** Release sln → 0 errors. Run `--functional` all pass (baseline 37 + FT-Pool, behavior unchanged).

- [ ] **Step 6: Commit**
```
git add Src/SAEA.Sockets/IServerSocket.cs Src/SAEA.Sockets/Core/Tcp/IocpServerSocket.cs Src/SAEA.Sockets/Core/Tcp/StreamServerSocket.cs Src/SAEA.Sockets/Core/Udp/UdpServerSocket.cs
git commit -m "feat(sockets): add owner-aware server SendAsync overload"
```

---

## Task 3: Client owner-aware send overload (`IClientSocket`)

**Files:**
- Modify: `Src/SAEA.Sockets/IClientSocket.cs`
- Modify: `Src/SAEA.Sockets/Core/Tcp/IocpClientSocket.cs`
- Modify: `Src/SAEA.Sockets/Core/Tcp/StreamClientSocket.cs`
- Modify: `Src/SAEA.Sockets/Core/Udp/UdpClientSocket.cs`

**Design:** `void SendAsync(ReadOnlyMemory<byte> data, IDisposable owner)`. IOCP: same array-backed fast path as the server but through the client's `SendAsyncRaw(ArraySegment<byte>, IDisposable owner)` (`:523`). Stream client is copy-at-boundary (its 2-arg `SendAsync` eagerly `data.ToArray()`s), so its overload delegates and disposes `owner` in `finally`. UDP client's 2-arg `SendAsync(ReadOnlyMemory<byte>)` forwards to `SendAsync(IPEndPoint, ReadOnlyMemory<byte>)`, which is **zero-copy for array-backed data** (calls `SendAsyncRaw(_remoteEndPoint, seg, null)` feeding an async `SendToAsync`), so its overload must mirror the IOCP logic (hand the owner down to `SendAsyncRaw`, or copy into a writer and send that writer as the owner) rather than disposing immediately.

- [ ] **Step 1: Add interface method** in `IClientSocket.cs` after `SendAsync(ReadOnlyMemory<byte>)` (`:115`):

```csharp
        /// <summary>
        /// 携带所有权对象的异步发送：发送完成后由实现方归还/释放 <paramref name="owner"/>。
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="owner">数据的所有者，发送完成或失败时释放</param>
        void SendAsync(ReadOnlyMemory<byte> data, IDisposable owner);
```

- [ ] **Step 2: Implement in `IocpClientSocket`** next to `SendAsync(ReadOnlyMemory<byte>)` (`:601`):

```csharp
        public void SendAsync(ReadOnlyMemory<byte> data, IDisposable owner)
        {
            if (data.Length == 0)
            {
                owner?.Dispose();
                return;
            }
            if (MemoryMarshal.TryGetArray(data, out var segment) && segment.Array != null)
            {
                SendAsyncRaw(segment, owner);
                return;
            }
            PooledBufferWriter writer = null;
            ArraySegment<byte> rented;
            try
            {
                writer = new PooledBufferWriter(data.Length);
                data.Span.CopyTo(writer.GetSpan(data.Length));
                writer.Advance(data.Length);
            }
            catch
            {
                if (writer != null)
                {
                    writer.Dispose();
                    writer = null;
                }
                owner?.Dispose();
                throw;
            }
            if (!writer.TryGetArray(out rented) || rented.Array == null)
            {
                writer.Dispose();
                writer = null;
                owner?.Dispose();
                return;
            }
            owner?.Dispose();
            SendAsyncRaw(rented, writer);
        }
```

Confirm `using System.Runtime.InteropServices;` and `SAEA.Common.Caching;` exist; add if missing.

- [ ] **Step 3: Implement in `StreamClientSocket`** next to `SendAsync(ReadOnlyMemory<byte>)` (`:334`):

```csharp
        public void SendAsync(ReadOnlyMemory<byte> data, IDisposable owner)
        {
            try
            {
                SendAsync(data);
            }
            finally
            {
                owner?.Dispose();
            }
        }
```

- [ ] **Step 4: Implement in `UdpClientSocket`** next to `SendAsync(ReadOnlyMemory<byte>)` (`:399`). NOTE: UDP client's 2-arg `SendAsync` is zero-copy for array-backed data, so hand the owner down to `SendAsyncRaw` (owned until send completion) instead of disposing it immediately:

```csharp
        public void SendAsync(ReadOnlyMemory<byte> data, IDisposable owner)
        {
            if (data.Length == 0)
            {
                owner?.Dispose();
                return;
            }
            if (data.Length > Model.SocketOption.UDPMaxLength)
            {
                owner?.Dispose();
                throw new ArgumentOutOfRangeException("SendAsync Incorrect length of data sent");
            }
            ArraySegment<byte> rented;
            if (MemoryMarshal.TryGetArray(data, out var segment) && segment.Array != null)
            {
                SendAsyncRaw(_remoteEndPoint, segment, owner);
                return;
            }
            PooledBufferWriter writer = null;
            try
            {
                writer = new PooledBufferWriter(data.Length);
                data.Span.CopyTo(writer.GetSpan(data.Length));
                writer.Advance(data.Length);
            }
            catch
            {
                if (writer != null)
                {
                    writer.Dispose();
                    writer = null;
                }
                owner?.Dispose();
                throw;
            }
            if (!writer.TryGetArray(out rented) || rented.Array == null)
            {
                writer.Dispose();
                writer = null;
                owner?.Dispose();
                return;
            }
            owner?.Dispose();
            SendAsyncRaw(_remoteEndPoint, rented, writer);
        }
```

Confirm `_remoteEndPoint` is the field used by the 2-arg overload, and that `System.Runtime.InteropServices` / `SAEA.Common.Caching` usings exist.

- [ ] **Step 5: Build** Release → 0 errors. `--functional` all pass (baseline 37 + FT-Pool).

- [ ] **Step 6: Commit**
```
git add Src/SAEA.Sockets/IClientSocket.cs Src/SAEA.Sockets/Core/Tcp/IocpClientSocket.cs Src/SAEA.Sockets/Core/Tcp/StreamClientSocket.cs Src/SAEA.Sockets/Core/Udp/UdpClientSocket.cs
git commit -m "feat(sockets): add owner-aware client SendAsync overload"
```

---

## Task 4: Strict write gate + single-release send ownership

**Files:**
- Modify: `Src/SAEA.Sockets/Base/BaseUserToken.cs`
- Modify: `Src/SAEA.Sockets/Core/Tcp/IocpServerSocket.cs`
- Modify: `Src/SAEA.Sockets/Core/Tcp/IocpClientSocket.cs`

**Problem (spec §3.3 erratum + ruling Q10=A):** `_writeAutoResetEvent` (`BaseUserToken.cs:43`) is an `AutoResetEvent` single token with 4 release callers (`ProcessSended`, the send-timeout task, `Clear()`, `UserTokenPool.Return` at `:172`) and 1 waiter. A late completion/timeout releases a gate held by a *different* send, so sync `Send` can interleave with an async send. Second bug: `IocpServerSocket.SendAsyncRaw` sets `IsSending = true` only on the async branch (`:395`, after `Socket.SendAsync`) and `IocpServerSocket.End` (`:504`) never sets it, so an idempotent `!IsSending` guard would turn the sync-complete `ProcessSended` into a no-op and leak the gate.

**Fix:** gate = one `SemaphoreSlim(1,1)` created once; set `IsSending = true` BEFORE `Socket.SendAsync` on every IOCP send; `ProcessSended` releases the gate exactly once under `lock (token)` keyed on `IsSending`; the timeout path only abandons the owner and never touches the gate/flag.

**Scope:** `IocpServerSocket`, `IocpClientSocket`, `BaseUserToken`. UDP/Stream send logic unchanged (`StreamUserToken` inherits but never uses `WaitWrite`/`ReleaseWrite`).

- [ ] **Step 1: `BaseUserToken.cs` gate primitive.** Replace the field at `:43` with:
  ```csharp
        protected readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1, 1);
  ```
  Delete the ctor re-assignment at `:48` (a `readonly` field cannot be reassigned). Keep `bool _isSending = false;` at `:44`. Replace `WaitWrite`/`ReleaseWrite` (`:87`/`:92`):
  ```csharp
        public bool WaitWrite(int timeout)
        {
            return _writeSemaphore.Wait(timeout);
        }

        public void ReleaseWrite()
        {
            try { _writeSemaphore.Release(); }
            catch (SemaphoreFullException) { }
        }
  ```
  In `Clear()` (`:97`) delete the line `_writeAutoResetEvent?.Close();` (`:102`); do NOT dispose `_writeSemaphore` (tokens are pooled/reused via `UserTokenPool.Return` at `:173` and a late completion may still call `ReleaseWrite`; a `SemaphoreSlim` with no allocated wait handle is GC-clean). No `_disposed` flag is needed because the semaphore is never disposed. Rationale: with `AutoResetEvent` an extra release is invisible; with `SemaphoreSlim` a *missing* release now blocks (correct) and an *extra* release is absorbed instead of corrupting a different send's gate.

- [ ] **Step 2: `ProcessSended` releases once, guarded by `IsSending`, under `lock`.** Server `IocpServerSocket.ProcessSended` (`:346`):
  ```csharp
        void ProcessSended(SocketAsyncEventArgs e)
        {
            try
            {
                var token = e.UserToken as IUserToken;
                if (token == null) return;
                lock (token)
                {
                    if (!token.IsSending) return;
                    token.IsSending = false;
                    var owner = token.TakeSendingOwner();
                    if (owner != null)
                    {
                        try { owner.Dispose(); } catch { }
                    }
                    token.Actived = DateTimeHelper.Now;
                    token.ReleaseWrite();
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke("", ex);
            }
        }
  ```
  Client `IocpClientSocket.ProcessSended` (`:501`) — ignore `e`, use `_userToken`:
  ```csharp
        void ProcessSended(SocketAsyncEventArgs e)
        {
            try
            {
                var token = _userToken;
                if (token == null) return;
                lock (token)
                {
                    if (!token.IsSending) return;
                    token.IsSending = false;
                    var owner = token.TakeSendingOwner();
                    if (owner != null)
                    {
                        try { owner.Dispose(); } catch { }
                    }
                    token.Actived = DateTimeHelper.Now;
                    token.ReleaseWrite();
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(_userToken == null ? "" : _userToken.ID, ex);
            }
        }
  ```

- [ ] **Step 3: Server `SendAsyncRaw` (`:369`) — arm `IsSending` before `SendAsync`; timeout abandons only; release the gate on every non-transferred path.** Introduce `bool acquired = false;` beside `transferred`, and replace the `if (userToken.WaitWrite(...) && Socket != null && Connected)` condition with `acquired = userToken.WaitWrite(SocketOption.ActionTimeout); if (acquired && userToken.Socket != null && userToken.Socket.Connected)`. Replace the `if (writeArgs != null)` block (`:383-410`) with:
  ```csharp
                    if (writeArgs != null)
                    {
                        userToken.SendingOwner = owner;
                        userToken.IsSending = true;
                        transferred = true;
                        writeArgs.SetBuffer(seg.Array, seg.Offset, seg.Count);
                        bool asyncPending = userToken.Socket.SendAsync(writeArgs);
                        if (!asyncPending)
                        {
                            ProcessSended(writeArgs);
                        }
                        else
                        {
                            Task.Run(async () =>
                            {
                                await Task.Delay(SocketOption.ActionTimeout);
                                lock (userToken)
                                {
                                    if (userToken.IsSending)
                                    {
                                        AbandonSendingOwner(userToken);
                                    }
                                }
                            });
                        }
                    }
  ```
  The timeout callback must NOT clear `IsSending` and must NOT call `ReleaseWrite`; only the eventual `ProcessSended` releases. Also replace the `transferred` branch of the `catch` (`:419-422`) so failure releases the gate too:
  ```csharp
                if (transferred)
                {
                    ProcessSended(userToken.WriteArgs);
                }
                else
                {
                    owner?.Dispose();
                    transferred = true;
                    if (acquired)
                    {
                        try { userToken.ReleaseWrite(); } catch { }
                        acquired = false;
                    }
                }
  ```
  And extend the `finally` so the two gate-leak paths are closed:
  ```csharp
                if (!transferred) owner?.Dispose();
                if (acquired && !transferred)
                {
                    try { userToken.ReleaseWrite(); } catch { }
                }
  ```
  `IsSending` is armed before `SetBuffer` so that if `SetBuffer` throws, the `catch` branch's `ProcessSended` is not a no-op — it still disposes the owner and releases the gate. (Previously this branch disposed the owner but never released the gate.) The `finally` release covers the `WaitWrite`-succeeded-but-socket-null/disconnected path and the `writeArgs == null` path, both of which previously held the gate forever (the old `WaitWrite(...) && Socket != null` short-circuit). The trade-off: a hung send holds the gate until completion or session teardown, which is strictly safer than the old ABA window.

- [ ] **Step 3b:** `_isSending` may be promoted to `protected` (harmless); keep `_writeSemaphore` `protected readonly`. Do not add `//` comments.

- [ ] **Step 4: Server `End` (`:504`) must arm `IsSending`.** At `:514-518`:
  ```csharp
                    if (writeArgs != null && userToken.WaitWrite(SocketOption.ActionTimeout))
                    {
                        writeArgs.SetBuffer(copy, 0, copy.Length);
                        userToken.IsSending = true;
                        if (!userToken.Socket.SendAsync(writeArgs)) ProcessSended(writeArgs);
                    }
  ```
  Without this, the new `!IsSending` guard makes the sync-complete `ProcessSended` a no-op and the gate is never released on the `End`/HTTP path.

- [ ] **Step 5: Client `SendAsyncRaw` (`:523`) — same ordering; failure releases via `ProcessSended`.** Replace the method body:
  ```csharp
        private void SendAsyncRaw(ArraySegment<byte> seg, IDisposable owner)
        {
            var userToken = _userToken;
            if (seg.Array == null || seg.Count == 0)
            {
                owner?.Dispose();
                return;
            }
            bool transferred = false;
            try
            {
                if (userToken != null && userToken.Socket != null && userToken.Socket.Connected)
                {
                    if (userToken.WaitWrite(SocketOption.ActionTimeout))
                    {
                        userToken.SendingOwner = owner;
                        userToken.IsSending = true;
                        transferred = true;
                        var writeArgs = userToken.WriteArgs;
                        writeArgs.SetBuffer(seg.Array, seg.Offset, seg.Count);
                        if (!userToken.Socket.SendAsync(writeArgs))
                        {
                            ProcessSended(writeArgs);
                        }
                    }
                    else
                    {
                        OnError?.Invoke($"SAEA SocketError:发送消息时发生异常,{userToken?.ID}", new TimeoutException("发送数据超时"));
                    }
                }
            }
            catch (Exception ex)
            {
                if (transferred)
                {
                    ProcessSended(userToken.WriteArgs);
                }
                else
                {
                    owner?.Dispose();
                    transferred = true;
                }
                OnError?.Invoke(userToken?.ID ?? "", ex);
                try { Disconnect(); } catch { }
            }
            finally
            {
                if (!transferred) owner?.Dispose();
            }
        }
  ```
  The client has no send-timeout task; do not add one. `IsSending` is set immediately after ownership is transferred and before anything that can throw, so every failure path reaches `ProcessSended` (which disposes the owner and releases the gate exactly once).

- [ ] **Step 6: Client sync `Send` (`:574`) holds the gate for its whole duration.** Replace with:
  ```csharp
        public void Send(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) return;
            if (!Connected)
            {
                OnError?.Invoke("", new Exception("SAEA SocketError:发送失败,当前连接已断开"));
                return;
            }
            var userToken = _userToken;
            if (userToken == null) return;
            if (!userToken.WaitWrite(SocketOption.ActionTimeout))
            {
                OnError?.Invoke($"SAEA SocketError:发送消息时发生异常,{userToken.ID}", new TimeoutException("发送数据超时"));
                return;
            }
            try
            {
                var copy = data.ToArray();
                var offset = 0;
                do
                {
                    var iResult = _socket.BeginSend(copy, offset, copy.Length - offset, SocketFlags.None, null, null);
                    offset += _socket.EndSend(iResult);
                }
                while (offset < copy.Length);
                userToken.Actived = DateTimeHelper.Now;
            }
            catch (Exception ex)
            {
                Disconnect(ex);
            }
            finally
            {
                userToken.ReleaseWrite();
            }
        }
  ```
  `ReadOnlySpan<byte>` cannot recover an array-backed buffer in netstandard2.0 (no `MemoryMarshal.TryGetArray` overload for spans), so exactly one boundary `data.ToArray()` remains on this synchronous API only. The async first-class path used by QueueSocket batches is fully zero-copy. Sync `Send` deliberately does not set `IsSending` (it never touches `WriteArgs`/`ProcessSended`) and holds the gate itself, so it cannot interleave with an in-flight async send. All callers (`HeartAsync:256`, `Subscribe:292`, `Unsubscribe:301`, `Close:315`) already use `Send`, so they inherit the gate.

- [ ] **Step 7: Build** Release → 0 errors. Run `--all` (all pass, baseline 56 + FT-Pool) and `--bench-iocp` 29/29. Stress note: rapid alternating sync `Send` and async `SendAsync` on one client must not throw, deadlock, or leak (watch for a stuck `WaitWrite` timeout and for `SemaphoreFullException`).

- [ ] **Step 8: Commit**
```
git add Src/SAEA.Sockets/Base/BaseUserToken.cs Src/SAEA.Sockets/Core/Tcp/IocpServerSocket.cs Src/SAEA.Sockets/Core/Tcp/IocpClientSocket.cs
git commit -m "fix(sockets): strict SemaphoreSlim write gate and single-release send ownership"
```

---

## Task 5: `QueueMsg` → `ReadOnlyMemory<byte>` + `PooledBuffer` owner + pool fixes

**Files:**
- Modify: `Src/SAEA.QueueSocket/Model/QueueMsg.cs`
- Modify: `Src/SAEA.QueueSocket/Model/QueueMsgPool.cs`
- Modify: `Src/SAEA.QueueSocket/Net/QueueSocketMsg.cs`
- Modify: `Src/SAEA.QueueSocket/README.md`, `README.en.md` (minimal sample edits)
- Test: `Src/SAEA.QueueSocketTest/{FunctionalTests.cs,TestHarness.cs}` (minimal `Data` reads)

**Design (Q7=A, Q11=delete IsPooled):**
- `QueueMsg.Data` type `byte[]` → `ReadOnlyMemory<byte>`; delete `internal bool IsPooled`; add `PooledBuffer _owner`.
- `DetachOwner()` returns the `PooledBuffer` and nulls the field so a subsequent `Dispose()` is a no-op.
- `Dispose()` returns `_owner` (idempotent via `PooledBuffer.Dispose`), resets `Data = ReadOnlyMemory<byte>.Empty`.
- `QueueSocketMsg.Data` type `byte[]` → `ReadOnlyMemory<byte>`; delete `public bool IsPooled`; `Dispose()` disposes its `PooledBuffer` owner if any (not `ArrayPool.Shared`).
- `QueueMsgPool.Rent` (`:49-58`) currently sets `msg.Type = Ping; msg.Name = null; msg.Topic = null; msg.Data = null;` and **`:58 msg.IsPooled = false;`**. The `IsPooled` assignment no longer compiles once the field is deleted, so it must be removed; `_owner` is private to `QueueMsg`, so the reset is exposed via `internal void Reset()` (defined in Step 1).

- [ ] **Step 1: Rewrite `QueueMsg.cs`.** Replace the `Data`/`IsPooled` fields and `Dispose`:

```csharp
        public ReadOnlyMemory<byte> Data { get; set; }

        PooledBuffer _owner;

        public void SetOwner(PooledBuffer owner)
        {
            _owner = owner;
            if (owner != null) Data = owner.AsMemory();
        }

        public PooledBuffer DetachOwner()
        {
            var owner = _owner;
            _owner = null;
            return owner;
        }

        public void Dispose()
        {
            var owner = _owner;
            _owner = null;
            Data = ReadOnlyMemory<byte>.Empty;
            if (owner != null) owner.Dispose();
        }

        internal void Reset()
        {
            _owner = null;
            Data = ReadOnlyMemory<byte>.Empty;
        }
```

Add `using SAEA.Common.Caching;`. Keep the existing class shape/properties (`Name`, `Topic`, `Type`, etc.) untouched.

- [ ] **Step 2: Fix `QueueMsgPool.Rent`** (`:49-58`): delete the `msg.IsPooled = false;` line (`:58`) and replace the field resets so the pooled instance is clean:
```csharp
            msg.Type = QueueSocketMsgType.Ping;
            msg.Name = null;
            msg.Topic = null;
            msg.Reset();
```
  Keep `Return` calling `msg.Dispose()` (now idempotent/no-op if already detached).

- [ ] **Step 3: Rewrite `QueueSocketMsg` `Data`/`Dispose`** (`:83`, `:89`, `:113`, `:124-136`): `public ReadOnlyMemory<byte> Data { get; set; }`; delete `IsPooled`; the ctor `byte[] data` assigns `Data = data` (implicit) unless null → `ReadOnlyMemory<byte>.Empty`; `Dispose()` disposes a stored `PooledBuffer` owner once (add `PooledBuffer _owner` + `SetOwner`/`DetachOwner` mirroring `QueueMsg`). Remove the `ArrayPool<byte>.Shared.Return` block **and** the `else if (Data.Length > 0) Array.Clear(Data, 0, Data.Length)` branch (`Data` is now `ReadOnlyMemory<byte>`, so that branch no longer compiles); after the change `Dispose()` is `var owner=_owner; _owner=null; Data=ReadOnlyMemory<byte>.Empty; if(owner!=null) owner.Dispose();`.

- [ ] **Step 4: Fix `QueueCoder` + `Exchange` compile fallout.** In `QueueCoder` (legacy `Encode(QueueSocketMsg)` path): at `:212` `queueSocketMsg.Data != null` → `queueSocketMsg.Data.Length > 0`; at `:214` `d = queueSocketMsg.Data;` — a literal `d = queueSocketMsg.Data.ToArray();` breaks the `--all` MicroEncode allocation budget (the copy adds payload+24 B/op and `QueueBenchmark.cs` is out of Task 5 scope), so instead bridge zero-copy via `MemoryMarshal.TryGetArray`: `if (MemoryMarshal.TryGetArray(queueSocketMsg.Data, out var dataSegment) && dataSegment.Offset == 0 && dataSegment.Count == dataSegment.Array.Length) { d = dataSegment.Array; } else { d = queueSocketMsg.Data.ToArray(); }` (add `using System.Runtime.InteropServices;`). This keeps `WriteFrame(byte[]...)` unchanged and restores the baseline 184/1144/4216 B/op. `Encode(QueueSocketMsg)`'s signature stays unchanged in Task 6. At `:506` the span `DecodeTo` `qm.Data = data.Slice(offset, dlen).ToArray();` still compiles (implicit `byte[]`→`ReadOnlyMemory`); that whole span decoder is superseded by the array decoder in Task 6. In `Exchange.cs:140` `_messageQueue.Enqueue(pInfo.Topic, pInfo.Data);` must be temporarily bridged to `_messageQueue.Enqueue(pInfo.Topic, pInfo.Data.ToArray());` because `MessageQueue.Enqueue` still takes `byte[]` until Task 7 Step 2 replaces this exact line with `DetachOwner()` ownership transfer (`pInfo.Data` is now `ReadOnlyMemory<byte>` and cannot implicitly convert to `byte[]`).

- [ ] **Step 5: Minimal test/doc compat edits (allowed by Q7=A).**
  - `FunctionalTests.cs:62` `Encoding.UTF8.GetString(obj.Data)` → `Encoding.UTF8.GetString(obj.Data.Span)`.
  - `FunctionalTests.cs:216-223` `var d = obj.Data; gotLen = d.Length; identical = d.Length == size; d.Span[i]`.
  - `FunctionalTests.cs:377/382` `(m.Data == null ? 0 : m.Data.Length)` → `m.Data.Length == 0` (default empty equals absent), and `m.Data[i]` (`:382`) → `m.Data.Span[i]` (`m` is `QueueSocketMsg`, so its `Data` is now `ReadOnlyMemory<byte>` too).
  - `TestHarness.cs`: grep confirms it has no `.Data` usage — **no edit needed** (the earlier note was speculative; verify with `rg '\.Data'` before touching).
  - `QueueSocket README.md:58/370/402`, `README.en.md:58/373/405`: `Encoding.UTF8.GetString(msg.Data)` → `Encoding.UTF8.GetString(msg.Data.ToArray())` (NOT `.Span`: `Encoding.GetString(ReadOnlySpan<byte>)` does not exist on netstandard2.0, the library's TFM, so the sample would not compile for its own consumers).

- [ ] **Step 6: Build** Release → 0 errors. `--functional` 45/45 pass, `--all` 64/64 pass (current baseline after Task 1's FT-Pool-Batch; do not assume the old 37/56).

- [ ] **Step 7: Commit**
```
git add Src/SAEA.QueueSocket/Model/QueueMsg.cs Src/SAEA.QueueSocket/Model/QueueMsgPool.cs Src/SAEA.QueueSocket/Net/QueueSocketMsg.cs Src/SAEA.QueueSocket/Net/QueueCoder.cs Src/SAEA.QueueSocket/Model/Exchange.cs Src/SAEA.QueueSocket/README.md Src/SAEA.QueueSocket/README.en.md Src/SAEA.QueueSocketTest/FunctionalTests.cs
git commit -m "refactor(queuesocket): QueueMsg.Data to ReadOnlyMemory<byte> with PooledBuffer owner"
```

---

## Task 6: `QueueCoder` array decode + writer send

**Files:**
- Modify: `Src/SAEA.QueueSocket/Net/QueueCoder.cs`
- Modify: `Src/SAEA.QueueSocketTest/QueueBenchmark.cs` (caller compat: return the pooled list)

**Design (spec §2.3/§四.2 + plan-review corrections):**
- Add `internal static int DecodeTo(byte[] buffer, int start, int count, List<QueueMsg> result)` mirroring the existing span decoder (`:414`) exactly, but with `buffer`-relative offsets and pooled payloads.
- Add `internal static void WriteFrameTo(IBufferWriter<byte> writer, QueueSocketMsgType type, byte[] nameBytes, byte[] topicBytes, ReadOnlySpan<byte> data)` — a DIFFERENT name, not an overload. FT11 reflects `Type.GetMethod("WriteFrame")`; adding a second method with that name throws `AmbiguousMatchException`. Keep `WriteFrame(byte[]...)` (`:233`), `Encode(QueueSocketMsg)` (`:191`), and the public `GetQueueResult(byte[])` (`:83`) names/signatures unchanged.
- Rewire `GetQueueResult(ReadOnlySpan<byte>)` (`:93`) to rent the result list from `QueueMsgListPool` and decode from the internal `_buffer` array.
- Do NOT add `GetQueueResult(byte[], int, int)`: `_buffer` is private to `QueueCoder`, so `QServer`/`QClient` keep calling `GetQueueResult(dataSpan)` and `GetQueueResult` owns the `_buffer` scratch.
- Keep the existing span `DecodeTo` (`:414`); it is left as internal scratch code.

There is no `FrameHeaderLength` in the file; the only constant is `MIN = 1+4+4+0+4+0+0 = 13` (`:49`). Header layout is `1 type + 4 total` (total at `+1`), then `4 nameLen` (at `+5`), name, `4 topicLen`, topic, data; `dlen = total - 4 - 4 - nameLen - 4 - topicLen`.

- [ ] **Step 1: Add the array `DecodeTo` and an array `ReadInt32` helper** next to the span decoder (`:414`). Create the `QueueMsg` only after every bounds check passes, so a resync `continue` never orphans a pooled instance:

```csharp
        internal static int DecodeTo(byte[] buffer, int start, int count, List<QueueMsg> result)
        {
            var offset = start;
            var end = start + count;
            if (count < MIN)
            {
                return 0;
            }

            while (end - offset >= MIN)
            {
                var typeValue = buffer[offset];
                if (typeValue < 1 || typeValue > 7)
                {
                    bool found = false;
                    for (var i = offset + 1; i < end; i++)
                    {
                        if (buffer[i] >= 1 && buffer[i] <= 7)
                        {
                            typeValue = buffer[i];
                            offset = i;
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        return end - start;
                    }
                }

                var type = (QueueSocketMsgType)typeValue;
                var packetStart = offset;
                offset += 1;

                if (offset + 4 > end) { offset = packetStart; break; }
                var total = ReadInt32(buffer, offset);
                if (total < 0 || total > 100 * 1024 * 1024)
                {
                    offset = packetStart + 1;
                    continue;
                }
                if (end - offset < total)
                {
                    offset = packetStart;
                    break;
                }
                offset += 4;

                if (offset + 4 > end) { offset = packetStart; break; }
                var nameLength = ReadInt32(buffer, offset);
                if (nameLength < 0 || nameLength > total)
                {
                    offset = packetStart + 1;
                    continue;
                }
                offset += 4;

                if (nameLength > 0 && offset + nameLength > end) { offset = packetStart; break; }
                var name = nameLength > 0 ? Encoding.UTF8.GetString(buffer, offset, nameLength) : null;
                offset += nameLength;

                if (offset + 4 > end) { offset = packetStart; break; }
                var topicLength = ReadInt32(buffer, offset);
                if (topicLength < 0 || topicLength > total)
                {
                    offset = packetStart + 1;
                    continue;
                }
                offset += 4;

                if (topicLength > 0 && offset + topicLength > end) { offset = packetStart; break; }
                var topic = topicLength > 0 ? Encoding.UTF8.GetString(buffer, offset, topicLength) : null;
                offset += topicLength;

                var dlen = total - 4 - 4 - nameLength - 4 - topicLength;
                if (dlen < 0)
                {
                    offset = packetStart + 1;
                    continue;
                }
                if (dlen > 0 && offset + dlen > end)
                {
                    offset = packetStart;
                    break;
                }

                var qm = QueueMsgPool.Rent();
                qm.Type = type;
                qm.Name = name;
                qm.Topic = topic;
                if (dlen > 0)
                {
                    var pb = MemoryPoolManager.RentPooled(dlen);
                    Buffer.BlockCopy(buffer, offset, pb.Buffer, 0, dlen);
                    qm.SetOwner(pb);
                }
                else
                {
                    qm.Data = ReadOnlyMemory<byte>.Empty;
                }
                result.Add(qm);
                offset += dlen;
            }

            return offset - start;
        }

        private static int ReadInt32(byte[] buffer, int offset)
        {
            return buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16) | (buffer[offset + 3] << 24);
        }
```

Add `using SAEA.Common.Caching;` for `MemoryPoolManager` (`QueueMsgPool`/`QueueMsgListPool` are already in `SAEA.QueueSocket.Model`, imported at `:33`). No `System.Buffers.Binary` needed — the little-endian helper is hand-written like the existing `ReadInt32`/`WriteInt32`. The overload `ReadInt32(byte[], int)` coexists with the existing `ReadInt32(ReadOnlySpan<byte>, int)`.

- [ ] **Step 2: Rewire `GetQueueResult(ReadOnlySpan<byte>)` (`:93`)** to rent a pooled list and decode from the array:

```csharp
        internal List<QueueMsg> GetQueueResult(ReadOnlySpan<byte> data)
        {
            var result = QueueMsgListPool.Rent();

            AppendData(data);

            if (_bufferCount >= MIN)
            {
                try
                {
                    var offset = DecodeTo(_buffer, _bufferOffset, _bufferCount, result);
                    if (result.Count > 0)
                    {
                        _bufferOffset += offset;
                        _bufferCount -= offset;
                        if (_bufferOffset > 4096 && _bufferCount < _bufferOffset)
                        {
                            CompactBuffer();
                        }
                        return result;
                    }
                    else if (offset > 0)
                    {
                        _bufferOffset += offset;
                        _bufferCount -= offset;
                    }
                }
                catch
                {
                    _bufferOffset += 1;
                    _bufferCount -= 1;
                }
            }
            return result;
        }
```

The public `GetQueueResult(byte[] data)` (`:83`) still delegates to this overload. Callers must return the list via `QueueMsgListPool.Return` (Tasks 8/9). The old span `DecodeTo` (`:414`) is retained but no longer called.

- [ ] **Step 3: Add `WriteFrameTo` (different name — see design).** Insert after `WriteFrame` (`:263`):

```csharp
        internal static void WriteFrameTo(System.Buffers.IBufferWriter<byte> writer, QueueSocketMsgType type, byte[] nameBytes, byte[] topicBytes, ReadOnlySpan<byte> data)
        {
            var nlen = nameBytes == null ? 0 : nameBytes.Length;
            var tlen = topicBytes == null ? 0 : topicBytes.Length;
            var dlen = data.Length;
            var total = 12 + nlen + tlen + dlen;

            var span = writer.GetSpan(1 + total);
            var offset = 0;
            span[offset++] = (byte)type;
            WriteInt32(span, offset, total); offset += 4;
            WriteInt32(span, offset, nlen); offset += 4;
            if (nlen > 0)
            {
                nameBytes.AsSpan().CopyTo(span.Slice(offset));
                offset += nlen;
            }
            WriteInt32(span, offset, tlen); offset += 4;
            if (tlen > 0)
            {
                topicBytes.AsSpan().CopyTo(span.Slice(offset));
                offset += tlen;
            }
            if (dlen > 0)
            {
                data.CopyTo(span.Slice(offset));
                offset += dlen;
            }
            writer.Advance(offset);
        }

        private static void WriteInt32(Span<byte> span, int offset, int value)
        {
            span[offset] = (byte)value;
            span[offset + 1] = (byte)(value >> 8);
            span[offset + 2] = (byte)(value >> 16);
            span[offset + 3] = (byte)(value >> 24);
        }
```

The writer must have capacity `>= 1 + total` (callers size it from the frame lengths). The existing `WriteInt32(byte[], int, int)` stays for `WriteFrame`.

- [ ] **Step 4: Fix `QueueBenchmark` decode loops (caller compat).** `MicroDecode` (`:110-113`, `:120-123`) and `MicroDecodeBatch` (`:144-147`, `:154-157`) call `coder.GetQueueResult(...)` and then `for (j...) r[j].Dispose(); r.Clear();`. Once `GetQueueResult` rents from `QueueMsgListPool`, `r.Clear()` drops the rented `List` (and its pooled `QueueMsg`s) without returning them. Replace each `dispose-loop + r.Clear();` pair with a single `QueueMsgListPool.Return(r);` (which disposes every element's owner via `QueueMsgPool.Return` and returns the list). Add `using SAEA.QueueSocket.Model;` (QueueBenchmark currently imports `SAEA.Common`, `SAEA.QueueSocket`, `SAEA.QueueSocket.Net`, `SAEA.QueueSocket.Type` — not `.Model`).

- [ ] **Step 5: Build** Release → 0 errors. `--functional` all pass (baseline 45/45), `--all` all pass (baseline 64/64). INV-5 pool conservation is asserted by FT-Pool-2 (Task 10).

> Review follow-up (code-quality review of Task 6): (a) `QServer.cs`/`QClient.cs` `list.Clear()` and `Exchange.cs:140` `pInfo.Data.ToArray()` now leak pooled payloads until they are rewired in Tasks 7–9 — this intermediate state is intentional and must NOT be released before Task 9; FT-Pool-2 (Task 10) is the guard. (b) `WriteFrameTo` is not yet covered by an equivalence test: the test project has no `InternalsVisibleTo` and reaches internals via reflection (FT11), but reflection cannot pass a `ReadOnlySpan<byte>` (ref struct cannot be boxed), so an equivalence assertion belongs where `WriteFrameTo` is actually wired (Task 8), not via reflection. (c) The public `GetQueueResult(byte[])` XML doc now documents the `QueueMsgListPool.Return` contract.

- [ ] **Step 6: Commit**
```
git add Src/SAEA.QueueSocket/Net/QueueCoder.cs Src/SAEA.QueueSocketTest/QueueBenchmark.cs
git commit -m "perf(queuesocket): array zero-copy decode with pooled payloads and writer send"
```

---

## Task 7: `MessageQueue` pooled payloads + `AcceptPublish` ownership transfer

> **Execution order correction (orchestrator):** Tasks 7 and 8 are compile-coupled and MUST land as ONE task. (1) Task 7 turns `DispatchLoop`'s `messages` into `List<PooledBuffer>`, but the frame build at `Exchange.cs:252` calls `WriteFrame(..., byte[] data)`; a `PooledBuffer` is not a `byte[]` and `PooledBuffer.Buffer.Length` is tier capacity, not payload length, so it cannot be passed as-is — the per-subscriber `PooledBufferWriter` from Task 8 Step 3 is required for a compiling, correct state. (2) Task 8 changes `Exchange.OnBatched` to `Action<string, PooledBufferWriter>`, which breaks `QServer._exchange_OnBatched(string, byte[])` (`QServer.cs:75/99`) until Task 9's QServer edit. Therefore this task also includes the QServer `_exchange_OnBatched` signature change (from Task 9). The remainder of Task 9 (QServer receive-list `Return` + all QClient work) becomes the next task; old Tasks 10/11 renumber to 9/10.

**Files:**
- Modify: `Src/SAEA.Common/Caching/FastQueue.cs` (`TryEnqueue`; single `Complete` in `Dispose`)
- Modify: `Src/SAEA.QueueSocket/Model/MessageQueue.cs`
- Modify: `Src/SAEA.QueueSocket/Model/Exchange.cs` (`AcceptPublish` + `DispatchLoop` + batcher + name cache)
- Modify: `Src/SAEA.QueueSocket/Model/Binding.cs` (`Dispose` no longer nulls `_cahce`)
- Modify: `Src/SAEA.QueueSocket/QServer.cs` (`_exchange_OnBatched` signature + owner send)

**Design:** `MessageQueue` stores the `PooledBuffer` payload owner instead of `byte[]`. Storing a `PooledBuffer` (rather than the whole `QueueMsg`) keeps the queue independent of the message object lifecycle and is exactly what `DispatchLoop` needs to write the wire frames. `FastQueue<T>` originally exposed only async `EnqueueAsync` (`FastQueue.cs:79`); this task adds a non-blocking `TryEnqueue(T)` (backed by `Channel.Writer.TryWrite`) so the receive thread never blocks. `MessageQueue.TryEnqueue` is non-blocking and returns `false` when the bounded channel is full OR completed, in which case `AcceptPublish` disposes the payload (R6). A `_disposed` guard plus completing each channel in `Dispose` closes the shutdown enqueue/orphan race.

> **Review-followup (orchestrator, post-implementation):** the code-quality review of the first Task 7 commit required changes, landed in `7a4aa224`:
> - `SAEA.Common/Caching/FastQueue.cs`: add `public bool TryEnqueue(T)` (`TryWrite` + `Interlocked.Increment`); `Dispose` calls `Writer.Complete()` once (was twice).
> - `MessageQueue`: add `private volatile bool _disposed`; `TryEnqueue` double-checks it and uses `queue.TryEnqueue(data)`; `Dispose` sets `_disposed`, calls `queue.Dispose()` (completes the channel) BEFORE draining, then drains and `_dic.Clear()`.
> - `Binding.Dispose` no longer sets `_cahce = null` (avoids an NRE when `AcceptPublish` races `Exchange.Dispose`).
> - `Exchange.DispatchLoop`: hoist `PooledBufferWriter writer = null;` outside the per-subscriber `try`, set `writer = null` after a successful `Insert`, and dispose in a `finally` — closes the exception-path writer leak. The `catch { }` no longer needs its own dispose.
> The code snippets below reflect the ORIGINAL plan text; the committed implementation uses the hardened forms above.

- [ ] **Step 1: Change `MessageQueue` to carry `PooledBuffer`.** `_dic` (`:43`) becomes `ConcurrentDictionary<string, FastQueue<PooledBuffer>>`; update the ctor initializer at `:63`. Replace `Enqueue` (`:68`) with:
```csharp
        public ValueTask<bool> Enqueue(string topic, PooledBuffer data)
        {
            var queue = _dic.GetOrAdd(topic, t => new FastQueue<PooledBuffer>(_maxPendingMsgCount));
            return queue.EnqueueAsync(data);
        }

        /// <summary>
        /// 非阻塞入队；返回 false 表示队列已满或已关闭，此时调用方仍持有并须释放 data。
        /// </summary>
        public bool TryEnqueue(string topic, PooledBuffer data)
        {
            if (_disposed) return false;
            var queue = _dic.GetOrAdd(topic, t => new FastQueue<PooledBuffer>(_maxPendingMsgCount));
            if (_disposed) return false;
            return queue.TryEnqueue(data);
        }
```
  Update `DequeueAsync` (`:75`), `TryDequeue` (`:88`), `ToList` (`:103`), `GetCount` (`:108`) to `PooledBuffer`. Replace `Dispose` (`:120`) so it completes each channel then drains and Disposes each payload (currently only `_dic.Clear()`, which would leak pooled buffers):
```csharp
        public void Dispose()
        {
            _disposed = true;
            foreach (var queue in _dic.Values)
            {
                try { queue.Dispose(); } catch { }
                while (queue.TryDequeue(out var payload))
                {
                    try { payload?.Dispose(); } catch { }
                }
            }
            _dic.Clear();
        }
```

- [ ] **Step 2: `Exchange.AcceptPublish` (`:134`) transfers ownership.** Replace `:140` `_messageQueue.Enqueue(pInfo.Topic, pInfo.Data);` with:
```csharp
            var payload = pInfo.DetachOwner();
            if (payload != null && !_messageQueue.TryEnqueue(pInfo.Topic, payload))
            {
                payload.Dispose();
            }
            pInfo.Dispose();
```
  `pInfo.DetachOwner()` makes the later `pInfo.Dispose()` (via `QueueMsgPool.Return` from `QueueMsgListPool.Return`) a no-op. If `TryEnqueue` fails (queue closed) the payload is freed immediately; never drop it silently (R6).

- [ ] **Step 3: `DispatchLoop` dequeue type (`:214`).** `_messageQueue.TryDequeue(topic, out var payload)` now yields a `PooledBuffer`; change `List<byte[]>` (`:188`) to `List<PooledBuffer>` and change the guard at `:220` to:
```csharp
                        if (payload != null)
                        {
                            if (payload.Length > 0) messages.Add(payload);
                            else payload.Dispose();
                        }
```
  A zero-length payload is drained immediately so it cannot be dropped without disposal. Non-empty payloads are disposed after every subscriber has copied them (Step 4).
- [ ] **Step 4: Build** Release → 0 errors. `--functional` all pass (baseline 45/45), `--all` all pass (baseline 64/64).

- [ ] **Step 5: Commit** (this task is Task 7 Steps 1–3 + Task 8 Steps 1–5 + the QServer `_exchange_OnBatched` change, landed together)
```
git add Src/SAEA.QueueSocket/Model/MessageQueue.cs Src/SAEA.QueueSocket/Model/Exchange.cs Src/SAEA.QueueSocket/QServer.cs
git commit -m "perf(queuesocket): pooled message-queue payloads and pooled dispatch writers"
```

- [x] **Step 6 (DONE):** first landed as `23834275`; review-required fixes landed as `7a4aa224` (`FastQueue.TryEnqueue` non-blocking, `MessageQueue` `_disposed` guard + channel completion, `Binding.Dispose` no-null, `DispatchLoop` writer-leak `finally`). Gates: Release 0 errors, `--functional` 45/45, `--all` 64/64, no new `//`. spec review PASS, code-quality re-review APPROVED.

---

## Task 8 (MERGED INTO TASK 7 — do not run separately)

**Files:**
- Modify: `Src/SAEA.QueueSocket/Model/Exchange.cs`

**Design (spec §4.2 correction):** dispatch cardinality is `batch × subscribers`. Replace the per-subscriber `new byte[bufferSize]` (`:248`) with ONE `PooledBufferWriter` per subscriber, filled with `QueueCoder.WriteFrameTo`, then handed to a `PooledClassificationBatcher`; the merged writer is delivered via the event to `QServer` (Task 9), which owns/disposes it after the send. Cache each subscriber's name bytes once (not per dispatch) in an `Exchange`-local `ConcurrentDictionary<string, byte[]>` (do NOT change `Binding`'s public surface). The payload owners are released after all subscribers have copied into their own writers.

- [ ] **Step 1: Swap the batcher instance and event signature.** Replace `ClassificationBatcher _classificationBatcher;` (`:55`) with `PooledClassificationBatcher _pooledBatcher;`. Replace `:60` with:
```csharp
        // 池化分发回调：id 为订阅会话，writer 所有权随回调转移
        public event Action<string, PooledBufferWriter> OnBatched;
```
  Replace `:110-112`:
```csharp
            _pooledBatcher = new PooledClassificationBatcher(5000, 100);
            _pooledBatcher.OnBatched += _pooledBatcher_OnBatched;
```
  Replace the old handler (`:124`):
```csharp
        private void _pooledBatcher_OnBatched(string id, PooledBufferWriter writer, int count)
        {
            var handler = OnBatched;
            if (handler != null)
            {
                handler(id, writer);
            }
            else
            {
                writer.Dispose();
            }
        }
```
  If no subscriber is attached the merged writer MUST be disposed (R6). This removes the dependency on the global `ClassificationBatcher` singleton shared with `MessageSocket`/`WebSocket`.

- [ ] **Step 2: Add the name-bytes cache.** Add a field:
```csharp
        private readonly ConcurrentDictionary<string, byte[]> _nameBytesCache = new ConcurrentDictionary<string, byte[]>();
```
  Add a helper:
```csharp
        private byte[] GetNameBytes(string sessionID, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return _nameBytesCache.GetOrAdd(sessionID, n => Encoding.UTF8.GetBytes(name));
        }
```
  Populate it in `GetSubscribeData` (`:155`) right after `_binding.Set(...)` (using `sInfo.Name`). Clear the entry for a session in `SessionClosed` (`:372`) and `Clear(string sessionID)` (`:313`) via `_nameBytesCache.TryRemove(sessionID, out var _);`. `Clear` here is the per-session clear, not the buffer reset.

- [ ] **Step 3: Build a writer per subscriber in `DispatchLoop` (`:230-273`).** Replace the body of the `foreach (var sub in currentSubs)` block:
```csharp
                            try
                            {
                                if (subs.TryGetValue(sub.Key, out var coder))
                                {
                                    var bindInfo = _binding.GetBingInfo(sub.Key);
                                    if (bindInfo != null)
                                    {
                                        var nameBytes = GetNameBytes(sub.Key, bindInfo.Name);
                                        var nameLen = nameBytes == null ? 0 : nameBytes.Length;
                                        var topicLen = topicBytes == null ? 0 : topicBytes.Length;

                                        long total = 0;
                                        for (int i = 0; i < messages.Count; i++)
                                        {
                                            total += 1 + 12 + nameLen + topicLen + messages[i].Length;
                                        }
                                        if (total <= 0 || total > int.MaxValue)
                                        {
                                            continue;
                                        }
                                        var writer = new PooledBufferWriter((int)total);
                                        for (int i = 0; i < messages.Count; i++)
                                        {
                                            QueueCoder.WriteFrameTo(writer, QueueSocketMsgType.Data, nameBytes, topicBytes, messages[i].AsSpan());
                                        }

                                        lock (_syncLocker)
                                        {
                                            if (_disposed || !subs.ContainsKey(sub.Key))
                                            {
                                                writer.Dispose();
                                                continue;
                                            }

                                            if (_pooledBatcher.Insert(sub.Key, writer))
                                            {
                                                Interlocked.Add(ref _outNum, messages.Count);
                                            }
                                            else
                                            {
                                                writer.Dispose();
                                            }
                                        }
                                    }
                                }
                            }
                            catch
                            {
                            }
```
  `messages[i].AsSpan()` supplies the payload span. If the subscriber vanished, the writer is disposed (no leak); if `Insert` returns false (capacity), the writer is disposed (R6).

- [ ] **Step 4: Release payload owners in the OUTER `try`'s `finally` (`:207` / `:276`).** The outer `try` wraps both the collection loop and the dispatch block, so the release MUST sit on the outer `try` — putting it inside the `if (messages.Count > 0)` dispatch block would miss a throw during collection, leaving already-collected payloads to be dropped (un-disposed) by the next iteration's `messages.Clear()`:
```csharp
                catch
                {
                    await Task.Delay(10);
                }
                finally
                {
                    for (int i = 0; i < messages.Count; i++)
                    {
                        try { messages[i].Dispose(); } catch { }
                    }
                }
```
  Every subscriber has already copied each payload into its own writer, so the payload returns to the pool here. The `messages.Clear()` at the top of each iteration (`:209`) resets the list for the next batch. `PooledBuffer.Dispose` is idempotent, so a later `Dispose` from another path is safe.

- [ ] **Step 5: `Dispose` (`:401`).** Replace `_classificationBatcher.OnBatched -= _classificationBatcher_OnBatched;` (`:411`) with `_pooledBatcher.OnBatched -= _pooledBatcher_OnBatched;`, replace the `_classificationBatcher.Clear(id)` in the subscriber loop (`:422`) with `_pooledBatcher.Clear(id)`, and dispose the batcher:
```csharp
                if (_pooledBatcher != null)
                {
                    _pooledBatcher.Dispose();
                }
```
  In `SessionClosed` (`:394`) replace `_classificationBatcher.Clear(sessionID)` with `_pooledBatcher.Clear(sessionID)`.

- [ ] **Step 6: Build** Release → 0 errors. `--all` all pass (baseline 56 + FT-Pool); FT-Pool-4 asserts the merged per-subscriber stream is byte-identical and correctly framed.

- [ ] **Step 7: Commit**
```
git add Src/SAEA.QueueSocket/Model/Exchange.cs
git commit -m "perf(queuesocket): pooled dispatch writers, cached name bytes, decoupled classification batcher"
```

---

## Task 8: `QServer` receive-list return + `QClient` pooled batcher

> Renumbered: this is the remainder of the original Task 9 (its QServer `_exchange_OnBatched` change moved into Task 7).

**Files:**
- Modify: `Src/SAEA.QueueSocket/QServer.cs`
- Modify: `Src/SAEA.QueueSocket/QClient.cs`

**QServer:**
- (MOVED to Task 7) `_exchange_OnBatched` (`:99`) signature becomes `(string id, PooledBufferWriter writer)` and forwards the owner: `_serverSokcet.SendAsync(id, writer.WrittenMemory, writer);` (owner overload from Task 2). Do not redo this here.
- `_serverSokcet_OnReceiveSpan` (`:124`) keeps `qcoder.GetQueueResult(dataSpan)` (now `QueueMsgListPool`-backed, Task 6). The current guard is `if (list != null && list.Count > 0)` (`:129`) and `list.Clear()` (`:135`) sits INSIDE it; with a pooled list, an empty parse (Count==0, e.g. a partial frame) would skip `Return` and leak the rented `List` on every incomplete frame. Change the guard so `Return` always runs:
```csharp
            if (list != null)
            {
                if (list.Count > 0)
                {
                    foreach (var item in list)
                    {
                        Reply(userToken, item);
                    }
                }
                QueueMsgListPool.Return(list);
            }
```
  A `QueueMsg` whose owner was transferred by `AcceptPublish` has already had `DetachOwner` called, so its `Dispose` inside `Return` is a no-op (no double return). (`QClient` already uses `if (list != null)` at `:192`, so it has no such leak.)
- `ReplyPong` (`:200`) keeps the sync `Send(ut.ID, qcoder.Pong(data.Name).AsSpan())`.

**QClient:**
- Add `using SAEA.QueueSocket.Type;` (QClient.cs does not currently import it; the new `Publish` references `QueueSocketMsgType`).
- `_clientSocket_OnReceiveSpan` (`:188`) replaces `list.Clear()` (`:198`) with `QueueMsgListPool.Return(list);`.
- Field/ctor: `Batcher<byte[]> _batcher` (`:67`) becomes `PooledBatcher _batcher`; `new Batcher<byte[]>(1000, 50)` (`:113`) becomes `new PooledBatcher(1000, 50)`; add `byte[] _nameBytes;` set in the ctor (`_nameBytes = string.IsNullOrEmpty(name) ? null : Encoding.UTF8.GetBytes(name);`).
- `_batcher_OnBatched` (`:212`) signature becomes `(PooledBufferWriter writer, int count)`; replace the `new byte[totalLength]`/`Buffer.BlockCopy` concatenation (`:218-235`) with:
```csharp
        private void _batcher_OnBatched(PooledBufferWriter writer, int count)
        {
            _clientSocket.SendAsync(writer.WrittenMemory, writer);
            try
            {
                OnMessagesSent?.Invoke(count);
            }
            catch { }
        }
```
  The owner overload (Task 3) makes the socket layer dispose `writer` after send. `OnMessagesSent` MUST be wrapped in try/catch: `SendAsync` hands the writer to the socket (async in-flight), and `PooledBatcher.Flush` only clears its `merged = null` disposal guard on normal handler return — a throwing subscriber would unwind into `Flush`'s `finally` and dispose a writer the socket still owns (in-flight pooled-buffer reuse). Swallowing subscriber exceptions matches the repo's `RaiseError` convention.
- `Publish` (`:277`):
```csharp
        public void Publish(string topic, string content)
        {
            var topicBytes = string.IsNullOrEmpty(topic) ? null : Encoding.UTF8.GetBytes(topic);
            var contentBytes = Encoding.UTF8.GetBytes(content);
            var nameLen = _nameBytes == null ? 0 : _nameBytes.Length;
            var topicLen = topicBytes == null ? 0 : topicBytes.Length;
            var writer = new PooledBufferWriter(1 + 12 + nameLen + topicLen + contentBytes.Length);
            QueueCoder.WriteFrameTo(writer, QueueSocketMsgType.Publish, _nameBytes, topicBytes, contentBytes);
            if (!_batcher.Insert(writer))
            {
                writer.Dispose();
            }
        }
```
  Residual (documented, out of scope for ns2.0): `Encoding.UTF8.GetBytes(content)` and `GetBytes(topic)` still allocate; span-based UTF-8 encoding is unavailable on netstandard2.0.
- `Close` (`:310`) keeps the sync `Send`; the write gate guarantees the Close frame is submitted before `Disconnect`. `Close` already unsubscribes `_batcher_OnBatched` then disposes the batcher — `PooledBatcher.Dispose` drains/disposes queued writers.
- Add XML `///` to `OnMessagesSent` documenting that it now means "submitted to the socket" (the owner handoff completed), not "acked by the peer".

- [x] **Step 1: QServer edits** as above (receive-list guard + `QueueMsgListPool.Return`; ONBATCHED ALREADY DONE IN TASK 7).
- [x] **Step 2: QClient edits** as above.
- [x] **Step 3: Build** Release → 0 errors. `--functional` all pass, `--all` all pass.
- [x] **Step 4: Commit**
```
git add Src/SAEA.QueueSocket/QServer.cs Src/SAEA.QueueSocket/QClient.cs
git commit -m "perf(queuesocket): pool server/client receive lists and client batch sends"
```
- [x] **Step 5 (DONE)** Implemented in `1c0b7041` (QServer.cs +5/-3, QClient.cs +28/-35). `--functional` 45/45, `--all` 64/64, Debug/Release 0 errors, no new `//`.
  - **Review follow-up:** spec review PASS (wire-format equivalence of `WriteFrameTo(Publish,...)` vs old `Publish` confirmed byte-identical). Code-quality review = CHANGES REQUIRED → I-1 (Important): a throwing `OnMessagesSent` subscriber unwound through `PooledBatcher.Flush`'s `finally`, disposing an in-flight writer already handed to the socket; fixed in `88b43a38` by wrapping `OnMessagesSent?.Invoke(count)` in `try { } catch { }`, inlining the `sentCount` local (M-3) and dropping the now-unused `using System.Collections.Generic;` (M-1). Re-review APPROVED.
  - Accepted residuals: M-2 receive-path list leak if `OnMessage`/`Reply` throws (pre-existing, not a regression — BASE also skipped `list.Clear()` on throw); M-4 per-`Publish` `PooledBufferWriter` object allocation (only the backing array is pooled; documented along with the `GetBytes` residual).

---

## Task 9: FT-Pool regression tests

**Files:**
- Modify: `Src/SAEA.QueueSocketTest/FunctionalTests.cs` only. (`Program.cs` needs no change: both `--functional` and `--all` call `FunctionalTests.RunAllAsync()`, where the tests are registered.)

Add and register:
- **FT-Pool-1 Ownership transfer:** after a Publish, `MessageQueue` holds the owner; the source `QueueMsg.Dispose()` is a no-op; no double-return; pool stats balanced.
- **FT-Pool-2 Pool conservation:** after a workload + idle, `MemoryPoolManager.GetStatistics()` each layer `Rented == Returned`.
- **FT-Pool-3 Bounded queue overflow:** fill `MessageQueue` beyond capacity; assert no payload leak and no exception; dropped/queued ownership handled.
- **FT-Pool-4 Dispatch merged writer:** multi-subscriber, multi-message batch; each subscriber receives a byte-identical, correctly framed stream.
- **FT-Pool-5 Client batch merge:** rapid Publishes are merged; received frames identical.
- **FT-Pool-6 Batcher clear/dispose drains:** `PooledBatcher.Clear()`/`Dispose()` disposes queued writers; pool balanced.

- [x] **Step 1:** Implement tests (helper `PoolBalancedAsync` + `FT_Pool1..6`).
- [x] **Step 2:** Register in `RunAllAsync` (covers `--functional` and `--all`); `Program.cs` unchanged.
- [x] **Step 3:** Run `--functional` and `--all`; all pass (0 failures). Initial: `--functional` 72/72, `--all` 91/91.
- [x] **Step 4: Commit** `ec6f49a5`:
```
git add Src/SAEA.QueueSocketTest/FunctionalTests.cs
git commit -m "test(queuesocket): add FT-Pool ownership and pool-conservation tests"
```
- [x] **Step 5: Review follow-up (DONE)** — code-quality review of `ec6f49a5` returned CHANGES REQUIRED; fixed in `c01f9995` (`test(queuesocket): strengthen FT-Pool merge and ownership assertions`):
  - **Important:** FT-Pool-5 asserted only `sent>=n`/`received>=n`, which passes even if client batching regresses to per-message sends. Added a per-callback `batches` counter and `batches < n` assertion.
  - FT-Pool-1 replaced a tautological `detached.Length == buffer.Length` with `SmallPoolReturned` snapshots (unchanged after `msg.Dispose()`, `+1` after `detached.Dispose()`).
  - FT-Pool-3 disposes the rented buffer on the unexpected-reject path (no leak on regression).
  - FT-Pool-6 awaits `SettleMs` before snapshotting pool stats to avoid cross-test background-activity flakiness.
  - Trailing newline added.
  - Accepted residuals (Minor): FT-Pool-2 exercises local rent/dispose rather than a live workload (conservation is asserted by the network-backed FT-Pool-4/5/6 helper runs); FT-Pool-4 asserts completeness/uniqueness but not merged-frame count. Final: `--functional` 74/74, `--all` 93/93.

---

## Task 10: Gates + benchmark comparison

- [x] **Step 1: Full gate matrix.** DONE (HEAD `ecc737ec`). Debug + Release `SAEA.Sockets.sln` 0 errors; `SAEA.QueueSocketTest --functional` **80/80**; `--all` **99/99**; `SAEA.P2PTest --all` **310/310**; `SAEA.P2PTest --bench-iocp` **29/29**.
- [x] **Step 2: no-new-`//` check.** DONE — clean over range `109cf417..HEAD`.
- [x] **Step 3: Public API diff.** DONE. `git diff 109cf417..HEAD --stat` = 27 files, +1206/-168. Additive owner overloads on `IServerSocket`/`IClientSocket`; approved changes: `QueueMsg.Data`/`QueueSocketMsg.Data` `byte[]`→`ReadOnlyMemory<byte>`, `QueueMsg.IsPooled` removed, `MessageQueue` payload `byte[]`→`PooledBuffer`, `Exchange.OnBatched`→`Action<string, PooledBufferWriter>`. Legacy `Batcher<T>`/`ClassificationBatcher` untouched (only new `PooledBatcher`/`PooledClassificationBatcher` added).
- [x] **Step 4: Benchmark** DONE (Release, `--all` e2e). Final: S1 `70926 msg/s / 1469 B/frame / GC0=131`; S2 `45876 / 1672 / GC0=377`. vs baseline S1 `61688 / 2657 / 167`, S2 `46945 / 5048 / 804`: S1 throughput +15%, B/frame **-45%**, GC0 -22%; S2 throughput -2%, B/frame **-67%**, GC0 **-53%**. Harness budget assertions PASS. **Target `B/frame<100`, `GC0≈0` NOT met** — see residual analysis below.
- [x] **Step 5: Commit results/notes.** DONE — no tracked README perf table exists, so no doc commit needed; results recorded here.

### Step 4 residual analysis (post-pooling)

Two residual sources, one fixed, one metric-bound:

1. **FIXED — large-pool cap (dominant).** `MemoryPoolManager._largePool` was `ArrayPool.Create(1MB, 50)`; `ArrayPool` never pools arrays larger than `maxArrayLength`, so every merged batch writer >1MB (client batcher ~1MB, server dispatch writer ~1MB for 1000 frames) was freshly allocated per flush. Fixed in `1251ba60` (`maxArrayLength`→16MB) and `1c3e0b92`/`ecc737ec` (`maxArraysPerBucket` 50→8, bounded to ~256MB worst-case across the large-tier buckets; 4 caused reuse starvation → S2 2183 B/frame, 8 → 1672). Regression test `FT-Pool-7` (`FunctionalTests.cs`) asserts a >1MB array is reused from the pool. Effect: S2 B/frame 3390→1672.
2. **METRIC FLOOR (not code-fixable).** `QueueBenchmark` measures `(GC.GetTotalAllocatedBytes after-before) / totalDeliveries` — total *process* allocation ÷ deliveries — not pooling-layer/wire bytes. The producer calls `Encoding.UTF8.GetBytes(content)` per `Publish` (S1 64B, S2 1024B) and `QClient` encodes the topic per publish, entirely outside the pooling layer. For S2 the content encoding alone is ≥~205 B/frame after dividing across 5 consumer deliveries, so `<100` is unreachable under this harness definition regardless of pooling. Remaining per-frame cost is dominated by app-layer string→byte[] encoding (`Producer.Publish`/`QClient`) and per-frame `Encoding.UTF8.GetString` for Name/Topic on the receive path — both outside the network zero-copy scope.

**Accepted outcome:** the network send/receive hot path is zero-copy and pool-conserved (INV-5 asserted by FT-Pool-2/4/5/6/7); the measured -45%/-67% B/frame and -22%/-53% GC0 are real. The `<100 B/frame` target is not meaningful against this harness metric and is superseded by the measured deltas above.


---

## Notes / risks

- **Resident memory:** pooled writers queued in `PooledBatcher` (up to `max` per id) are not GC-collected; if for a given subscriber `Rented==Returned` but RSS grows, add a byte cap to `PooledBatcher`. `MemoryPoolManager._largePool` now keeps up to 8 arrays per size bucket (64KB–16MB), worst case ~256MB; lower `maxArraysPerBucket` if RSS is a concern, but note benchmark reuse degrades below 8 (see Task 10 Step 4). Monitor during Task 11.
- **`QueueMsgListPool.Return`** assumes no returned `QueueMsg` still owns a buffer; `DetachOwner` must be used for every transferred/temporarily-kept message (Tasks 7–9).
- **If `Insert` returns false** anywhere (batcher capacity), the caller MUST `Dispose()` the writer (R6). Reviewers: verify each `Insert` call site.
- **Do not touch the other 16 projects** — that is P6, separate plans.
- **Do not modify** the legacy `Batcher<T>`/`ClassificationBatcher` (still used by MessageSocket/WebSocket/RedisSocket).
- **Public `Consumer`/`QClient.OnMessage` reuse hazard:** after Task 9, `QClient._clientSocket_OnReceiveSpan` returns the list via `QueueMsgListPool.Return` immediately after the synchronous `OnMessage?.Invoke(item)` callbacks, so both the `QueueMsg` instance and its pooled payload are recycled. Any handler that retains the `QueueMsg`/`Data` beyond the callback will observe reused memory. This matches the pre-existing object-pool intent, but must be called out in the `OnMessage` XML doc and in `README.md`; tests that retain messages (e.g. `Consumer.OnMessage`) must copy out (`item.Data.ToArray()`).
- **`Exchange._nameBytesCache` lifetime:** entries are per subscriber session and removed in `SessionClosed`/`Clear(sessionID)`; verify no growth across subscribe/unsubscribe churn in FT-Pool-4.