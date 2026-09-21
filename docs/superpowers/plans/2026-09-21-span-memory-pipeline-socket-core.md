# Plan 2A: Socket Interface Modernization + IOCP Span/Memory + Benchmark (additive)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the new Span/Memory public surface to `IClientSocket`, `IServerSocket` and `IUserToken`, implement it in all six socket implementers, make the IOCP send path zero-copy with pooled-buffer ownership, make `OnClientReceiveSpan`/`OnServerReceiveSpan` the primary receive events, and migrate `IocpBenchmark` to exercise the new path end-to-end.

**Architecture:** **Additive only.** New members are *added* to the interfaces; the legacy `byte[]` members are kept and remain functional so the whole solution stays green. `OnClientReceiveSpan`/`OnServerReceiveSpan` already exist on the IOCP classes but are not on the interfaces — this plan promotes them onto the interfaces and wires the other implementers. The `byte[]` surface (and the `_isBase*Type`/`.ToArray()` compatibility branches) is **removed in Plan 2C**, after all consumers have migrated in 2B/2C.

**Tech Stack:** C# / .NET Standard 2.0 (`SAEA.Sockets`), `SAEA.Common` (`PooledBufferWriter`, `MemoryPoolManager`), `SAEA.P2PTest` console harness (`TestHarness`, `Program.cs --all`).

---

## Scope

### In scope (Plan 2A)
- `IUserToken`: add `IDisposable SendingOwner { get; set; }` (send-buffer ownership hand-off).
- `IClientSocket`: add `event OnClientReceiveSpanHandler OnClientReceiveSpan`, `void Send(ReadOnlySpan<byte>)`, `void SendAsync(ReadOnlyMemory<byte>)`, `void SendAsync(ISocketProtocal)`, `Task SendAsync(ReadOnlyMemory<byte>, CancellationToken)`.
- `IServerSocket`: add `event OnServerReceiveSpanHandler OnServerReceiveSpan`, `void Send(string, ReadOnlySpan<byte>)`, `void SendAsync(string, ReadOnlyMemory<byte>)`, `void SendAsync(string, ISocketProtocal)`, `void End(string, ReadOnlyMemory<byte>)`, `void SendAsync(IPEndPoint, ReadOnlyMemory<byte>)`.
- Implement the new surface in the six implementers: `IocpClientSocket`, `IocpServerSocket`, `StreamClientSocket`, `StreamServerSocket`, `UdpClientSocket`, `UdpServerSocket`.
- `BaseUserToken.Clear()` releases any pending `SendingOwner`.
- IOCP `ProcessSended` releases the pooled send buffer exactly once.
- `IocpBenchmark` migrated to span-only receive + memory/protocal send, plus an allocation assertion.

### Explicitly out of scope (deferred)
- Stream server `PipeReader` rewrite, UDP internals, Shortcut adapters → **Plan 2B**.
- Consumer migration (DNS/MQTT/WebSocket/HTTP/FTP/FileSocket/MessageSocket/QueueSocket/RPC/RedisSocket/Socket5/P2P/tests) → **Plan 2B/2C**.
- **Removal** of the `byte[]` interface members and the `_isBase*Type`/`.ToArray()` compatibility branches → **Plan 2C**.
- Version bump `26.9.21.1`, README, final DoD → **Plan 2C**.

### Non-negotiable invariants
- **Wire format unchanged**: `[8B little-endian length][1B Type][body]`.
- The solution must compile and `--all` must pass after **every** task (additive, so no red window).
- Never use `git add -A`/`git add .`; stage explicit paths only. Do not stage `Src/SAEA.Sockets/Core/Tcp/IocpServerSocket.cs`'s pre-existing stat-only dirty entry if it reappears (verify with `git status`; the file *will* legitimately change in Task 5).

## Acceptance (definition of done for 2A)
- `dotnet build Src/SAEA.Sockets.sln -c Debug` → 0 errors.
- `dotnet build Src/SAEA.Sockets.sln -c Release` → 0 errors.
- `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug -- --all` → all pass (baseline 283 assertions + new ones).
- `--bench-iocp` runs to completion; span-only benchmark shows no regression and the `SpanDecodeStream` B/frame stays below the Plan 1 ICE.
- `IocpBenchmark` no longer subscribes to any `byte[]` receive event.
- No `.cs` file outside `Src/SAEA.Sockets/` + `Src/SAEA.P2PTest/` is modified.

## File Structure
- **Modify** `Src/SAEA.Sockets/Interface/IUserToken.cs` — add `SendingOwner`.
- **Modify** `Src/SAEA.Sockets/Base/BaseUserToken.cs` — implement `SendingOwner`, release in `Clear()`.
- **Modify** `Src/SAEA.Sockets/IClientSocket.cs` — add span event + memory/span send members.
- **Modify** `Src/SAEA.Sockets/IServerSocket.cs` — add span event + memory/span send members.
- **Modify** `Src/SAEA.Sockets/Core/Tcp/IocpClientSocket.cs` — implement new members; add owner release to `ProcessSended`.
- **Modify** `Src/SAEA.Sockets/Core/Tcp/IocpServerSocket.cs` — implement new members; add owner release to `ProcessSended`.
- **Modify** `Src/SAEA.Sockets/Core/Tcp/StreamClientSocket.cs` — implement new members (minimal delegation).
- **Modify** `Src/SAEA.Sockets/Core/Tcp/StreamServerSocket.cs` — implement new members (minimal delegation + span event raise).
- **Modify** `Src/SAEA.Sockets/Core/Udp/UdpClientSocket.cs` — make span event public (interface-compatible) + memory send.
- **Modify** `Src/SAEA.Sockets/Core/Udp/UdpServerSocket.cs` — add span event + memory send.
- **Modify** `Src/SAEA.P2PTest/Tests/IocpBenchmark.cs` — span-only receive, memory/protocal send, allocation assertion.

---

### Task 1: `IUserToken.SendingOwner` + ownership release in `BaseUserToken`

**Why first:** the IOCP send tasks (4, 5) depend on the ownership slot and its release-on-clear.

**Files:**
- Modify: `Src/SAEA.Sockets/Interface/IUserToken.cs`
- Modify: `Src/SAEA.Sockets/Base/BaseUserToken.cs`

- [ ] **Step 1: Add the member to the interface**

In `Src/SAEA.Sockets/Interface/IUserToken.cs`, after the `ICoder Coder { get; set; }` block (currently ends line 93), insert:

```csharp
        /// <summary>
        /// 发送缓冲区的所有权对象。当发送数据为零拷贝时（调用方内存直接发送）为 null；
        /// 当库内从池中租用了缓冲区时，指向该池化对象，由发送完成回调负责释放。
        /// </summary>
        IDisposable SendingOwner { get; set; }

        /// <summary>
        /// 原子地取出并清空发送缓冲区所有权对象；取出后由调用方负责释放。
        /// 发送完成回调与断开清理可能并发，必须通过本方法保证恰好释放一次。
        /// </summary>
        IDisposable TakeSendingOwner();
```

`using System;` is already present (line 32), so `IDisposable` resolves.

- [ ] **Step 2: Implement it in `BaseUserToken` and release on `Clear()`**

In `Src/SAEA.Sockets/Base/BaseUserToken.cs`, add a field-backed property and the atomic taker after `public ICoder Coder { get; set; }` (line 66):

```csharp
        IDisposable _sendingOwner;

        public IDisposable SendingOwner
        {
            get { return _sendingOwner; }
            set { Volatile.Write(ref _sendingOwner, value); }
        }

        public IDisposable TakeSendingOwner()
        {
            return Interlocked.Exchange(ref _sendingOwner, null);
        }
```

(`System.Threading` is already imported at line 34, so `Interlocked` resolves.)

Then change `Clear()` (lines 84–94) to **close the socket first**, then atomically take and release the pending owner before nulling fields:

```csharp
        public void Clear()
        {
            Socket?.Close();
            try { TakeSendingOwner()?.Dispose(); } catch { }
            Coder?.Clear();
            _writeAutoResetEvent?.Close();
            ReadArgs?.Dispose();
            WriteArgs?.Dispose();
            Socket = null;
            ReadArgs = null;
            WriteArgs = null;
        }
```

> Ordering rationale: closing the socket aborts any in-flight send so the OS can no longer read the pooled buffer; only then is it safe to return the buffer to the pool. `TakeSendingOwner()` uses `Interlocked.Exchange` so a concurrent `ProcessSended` cannot observe the same owner.

- [ ] **Step 3: Build the sockets project**

Run: `dotnet build Src/SAEA.Sockets/SAEA.Sockets.csproj -c Debug`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 4: Add a unit test for release-on-clear**

In `Src/SAEA.P2PTest/Tests/SpanPipelineTest.cs`, add a test method and register it (follow the existing pattern in that file where similar `PooledBufferWriter` tests are asserted via `TestHarness.Expect`). The test:

```csharp
public static void SendingOwnerIsReleasedOnClear()
{
    var live = new SAEA.Sockets.Base.BaseUserToken();
    var liveOwner = new TrackingDisposable();
    live.SendingOwner = liveOwner;
    var taken = live.TakeSendingOwner();
    TestHarness.Expect(ReferenceEquals(taken, liveOwner) && live.TakeSendingOwner() == null,
        "TakeSendingOwner returns the owner once and empties the slot");

    var token = new SAEA.Sockets.Base.BaseUserToken();
    var owner = new TrackingDisposable();
    token.SendingOwner = owner;

    token.Clear();

    TestHarness.Expect(owner.Disposed, "IUserToken.Clear releases SendingOwner");
    TestHarness.Expect(token.SendingOwner == null, "IUserToken.Clear nulls SendingOwner");
    TestHarness.Expect(token.TakeSendingOwner() == null, "TakeSendingOwner returns null after Clear");

    var throwing = new ThrowingDisposable();
    token.SendingOwner = throwing;
    var threw = false;
    try { token.Clear(); } catch { threw = true; }
    TestHarness.Expect(!threw && throwing.Attempted, "IUserToken.Clear swallows dispose exceptions");
}

sealed class TrackingDisposable : IDisposable
{
    public bool Disposed;
    public void Dispose() { Disposed = true; }
}

sealed class ThrowingDisposable : IDisposable
{
    public bool Attempted;
    public void Dispose() { Attempted = true; throw new InvalidOperationException("boom"); }
}
```

Register it wherever the other `SpanPipelineTest` methods are invoked (in `SpanPipelineTest.Run()`), and ensure `Program.cs` reaches it via the existing `SpanPipelineTest.Run();` call at `Program.cs:135`.

- [ ] **Step 5: Run the suite**

Run: `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug -- --all`
Expected: all pass; count increases by 5 over the 283 baseline (288 total).

- [ ] **Step 6: Commit**

```powershell
git add Src/SAEA.Sockets/Interface/IUserToken.cs Src/SAEA.Sockets/Base/BaseUserToken.cs Src/SAEA.P2PTest/Tests/SpanPipelineTest.cs
git diff --cached --name-only
git commit -m "feat(sockets): add IUserToken.SendingOwner and release it on Clear"
```

---

### Task 2: Additive `IClientSocket` surface

**Files:**
- Modify: `Src/SAEA.Sockets/IClientSocket.cs`

- [ ] **Step 1: Add the span receive event**

After `event OnClientReceiveHandler OnReceive;` (line 71) insert:

```csharp
        /// <summary>
        /// 接收数据事件（Span 版本）。data 仅在回调期间有效，消费方如需跨回调保存必须自行复制。
        /// </summary>
        event OnClientReceiveSpanHandler OnClientReceiveSpan;
```

`using SAEA.Sockets.Handler;` (line 32) already imports `OnClientReceiveSpanHandler`; `using System;` (line 35) resolves `ReadOnlySpan<byte>` / `ReadOnlyMemory<byte>`; `using System.Threading.Tasks;` (line 40) resolves `Task`; `using System.Threading;` (line 39) resolves `CancellationToken`. `ISocketProtocal` lives in `SAEA.Sockets.Interface`, imported at line 33.

- [ ] **Step 2: Add the memory/span/protocal send members**

After `void SendAsync(byte[] data);` (line 121) insert:

```csharp
        /// <summary>
        /// 同步发送（Span）。ns2.0 下会租用池化缓冲区拷贝一次后发送。
        /// </summary>
        /// <param name="data">数据</param>
        void Send(ReadOnlySpan<byte> data);

        /// <summary>
        /// iocp 发送（Memory）。可用时零拷贝，否则租用池化缓冲区拷贝一次。
        /// </summary>
        /// <param name="data">数据</param>
        void SendAsync(ReadOnlyMemory<byte> data);

        /// <summary>
        /// 编码并发送协议对象（零拷贝优先）。
        /// </summary>
        /// <param name="protocal">协议对象</param>
        void SendAsync(ISocketProtocal protocal);

        /// <summary>
        /// 异步流发送（Memory）。
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken);
```

- [ ] **Step 3: Confirm the project no longer compiles (expected red)**

Run: `dotnet build Src/SAEA.Sockets/SAEA.Sockets.csproj -c Debug`
Expected: **errors** — the six implementers do not yet implement the new members. This red is expected and will be cleared by Tasks 4–7. Do **not** commit here.

> Do NOT commit Task 2 in isolation. Tasks 2+3+4+5 are committed together in Task 5 Step 6 (or Task 3 declares the same red and it is cleared at Task 5). If you prefer green-per-commit, implement Task 4/5 in the same working session before committing.

---

### Task 3: Additive `IServerSocket` surface

**Files:**
- Modify: `Src/SAEA.Sockets/IServerSocket.cs`

- [ ] **Step 1: Add the span receive event**

After `event OnReceiveHandler OnReceive;` (line 63) insert:

```csharp
        /// <summary>
        /// 接收数据事件（Span 版本）。data 仅在回调期间有效。
        /// </summary>
        event OnServerReceiveSpanHandler OnServerReceiveSpan;
```

`using SAEA.Sockets.Handler;` (line 36) imports `OnServerReceiveSpanHandler`. `using System;` (line 32) resolves span/memory. `System.Threading.Tasks` / `System.Threading` are **not** imported — this task adds no `Task`/`CancellationToken` members (the server send surface is `void`), so no new using is needed. `ISocketProtocal` is in `SAEA.Sockets.Interface` — **add** `using SAEA.Sockets.Interface;` to the file's using block.

- [ ] **Step 2: Add the memory/span/protocal send members**

After `void SendAsync(string sessionID, byte[] data);` (line 98) insert:

```csharp
        /// <summary>
        /// 同步发送（Span）。ns2.0 下会租用池化缓冲区拷贝一次后异步发送。
        /// </summary>
        /// <param name="sessionID">会话ID</param>
        /// <param name="data">数据</param>
        void Send(string sessionID, ReadOnlySpan<byte> data);

        /// <summary>
        /// 异步发送（Memory）。可用时零拷贝，否则租用池化缓冲区拷贝一次。
        /// </summary>
        /// <param name="sessionID">会话ID</param>
        /// <param name="data">数据</param>
        void SendAsync(string sessionID, ReadOnlyMemory<byte> data);

        /// <summary>
        /// 编码并发送协议对象（零拷贝优先）。
        /// </summary>
        /// <param name="sessionID">会话ID</param>
        /// <param name="protocal">协议对象</param>
        void SendAsync(string sessionID, ISocketProtocal protocal);

        /// <summary>
        /// http end。
        /// </summary>
        /// <param name="sessionID">会话ID</param>
        /// <param name="data">数据</param>
        void End(string sessionID, ReadOnlyMemory<byte> data);

        /// <summary>
        /// 定向发送（Memory）。
        /// </summary>
        /// <param name="ipEndPoint">目标地址</param>
        /// <param name="data">数据</param>
        void SendAsync(IPEndPoint ipEndPoint, ReadOnlyMemory<byte> data);
```

- [ ] **Step 3: Expected red continues**

Run: `dotnet build Src/SAEA.Sockets/SAEA.Sockets.csproj -c Debug`
Expected: errors listing every unimplemented member of `IClientSocket` and `IServerSocket` in the six implementers. Proceed to Tasks 4–7 without committing.

---

### Task 4: Implement the new `IClientSocket` surface in `IocpClientSocket`

**Files:**
- Modify: `Src/SAEA.Sockets/Core/Tcp/IocpClientSocket.cs`

**Usings to add** (top of file, after line 42): `using System.Buffers;`, `using System.Runtime.InteropServices;`, `using SAEA.Common.Caching;`, `using SAEA.Sockets.Base;`. (`System.Threading.Tasks` line 37, `SAEA.Sockets.Interface` line 41 already present.)

- [ ] **Step 1: Release the pooled send owner in `ProcessSended`**

Replace `ProcessSended` (lines 492–503) with:

```csharp
        void ProcessSended(SocketAsyncEventArgs e)
        {
            try
            {
                var owner = _userToken.TakeSendingOwner();
                _userToken.Actived = DateTimeHelper.Now;
                if (owner != null)
                {
                    try { owner.Dispose(); } catch { }
                }
                _userToken.ReleaseWrite();
            }
            catch (Exception ex)
            {
                OnError?.Invoke(_userToken?.ID ?? "", ex);
            }
        }
```

- [ ] **Step 2: Add the internal owner-aware async sender**

Immediately after `SendAsync(IUserToken userToken, byte[] data)` (ends line 539), insert:

```csharp
        /// <summary>
        /// iocp 异步发送核心：seg 指向的内存由 owner 持有；owner 为 null 表示调用方内存（零拷贝）。
        /// 所有权在发送完成（ProcessSended）或失败路径释放。
        /// </summary>
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
                userToken.TakeSendingOwner()?.Dispose();
                transferred = true;
                OnError?.Invoke(userToken?.ID ?? "", ex);
                try { userToken?.ReleaseWrite(); } catch { }
                try { Disconnect(); } catch { }
            }
            finally
            {
                if (!transferred) owner?.Dispose();
            }
        }
```

- [ ] **Step 3: Add the `Send(ReadOnlySpan<byte>)` member**

Insert after the existing `Send(byte[] data)` method (ends line 580):

```csharp
        public void Send(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) return;
            if (!Connected)
            {
                OnError?.Invoke("", new Exception("SAEA SocketError:发送失败,当前连接已断开"));
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

                _userToken.Actived = DateTimeHelper.Now;
            }
            catch (Exception ex)
            {
                Disconnect(ex);
            }
        }
```

> **Note:** `.NET Standard 2.0` exposes no `Socket.BeginSend(ReadOnlySpan<byte>, ...)`. `data.ToArray()` is a single unavoidable copy for the *synchronous* span overload; the asynchronous `SendAsync(ReadOnlyMemory<byte>)` below is the zero-copy path. This matches spec §5.1 ("租池拷贝一次").

- [ ] **Step 4: Add `SendAsync(ReadOnlyMemory<byte>)`, `SendAsync(ISocketProtocal)`, `SendAsync(ReadOnlyMemory<byte>, CancellationToken)`**

Insert after the new `Send(ReadOnlySpan<byte>)`:

```csharp
        public void SendAsync(ReadOnlyMemory<byte> data)
        {
            if (data.Length == 0) return;
            if (MemoryMarshal.TryGetArray(data, out var seg))
            {
                SendAsyncRaw(seg, null);
                return;
            }
            var writer = new PooledBufferWriter(data.Length);
            data.Span.CopyTo(writer.GetSpan(data.Length));
            writer.Advance(data.Length);
            writer.TryGetArray(out var rented);
            SendAsyncRaw(rented, writer);
        }

        public void SendAsync(ISocketProtocal protocal)
        {
            if (protocal == null) return;
            var coder = _userToken?.Coder;
            if (coder == null)
            {
                OnError?.Invoke(_userToken?.ID ?? "", new InvalidOperationException("SAEA SocketError:coder 未初始化"));
                return;
            }
            var bodyLen = protocal.BodyLength;
            var size = bodyLen > 0 && bodyLen < int.MaxValue - 64 ? (int)bodyLen + 64 : 64;
            var writer = new PooledBufferWriter(size);
            try
            {
                coder.Encode(protocal, writer);
            }
            catch (Exception ex)
            {
                writer.Dispose();
                OnError?.Invoke(_userToken?.ID ?? "", ex);
                return;
            }
            writer.TryGetArray(out var rented);
            SendAsyncRaw(rented, writer);
        }

        public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            return Task.Run(() => SendAsync(data), cancellationToken);
        }
```

> `ICoder.Encode(ISocketProtocal, IBufferWriter<byte>)` is the Plan 1 surface; `PooledBufferWriter` implements `IBufferWriter<byte>` and `TryGetArray`. If the coder's `Encode` signature differs, inspect `Src/SAEA.Sockets/Interface/ICoder.cs` and adapt the call — do **not** change the coder.

- [ ] **Step 5: Build**

Run: `dotnet build Src/SAEA.Sockets/SAEA.Sockets.csproj -c Debug`
Expected: `IocpClientSocket` errors gone; remaining interface-satisfaction errors are in the Stream/UDP/Shortcut implementers (cleared in Tasks 5–7).

- [ ] **Step 6: Do NOT commit yet** — the assembly is still red until the remaining implementers are done. Continue to Task 5.

---

### Task 5: Implement the new `IServerSocket` surface in `IocpServerSocket`

**Files:**
- Modify: `Src/SAEA.Sockets/Core/Tcp/IocpServerSocket.cs`

**Usings to add:** `using System.Buffers;`, `using System.Runtime.InteropServices;`, `using SAEA.Common.Caching;`. (`System`, `System.Threading.Tasks`, `SAEA.Sockets.Interface` already present.)

- [ ] **Step 1: Release the pooled send owner in `ProcessSended`**

Replace `ProcessSended` (lines 372–386) with:

```csharp
        void ProcessSended(SocketAsyncEventArgs e)
        {
            try
            {
                var token = e.UserToken as IUserToken;
                if (token == null) return;
                var owner = token.TakeSendingOwner();
                if (owner != null)
                {
                    try { owner.Dispose(); } catch { }
                }
                token.IsSending = false;
                token.Actived = DateTimeHelper.Now;
                token.ReleaseWrite();
            }
            catch (Exception ex)
            {
                OnError?.Invoke("", ex);
            }
        }
```

- [ ] **Step 2: Add the internal owner-aware async sender**

Insert after the existing `SendAsync(IUserToken userToken, byte[] data)` (ends line 446):

```csharp
        private void SendAsyncRaw(IUserToken userToken, ArraySegment<byte> seg, IDisposable owner)
        {
            if (userToken == null || seg.Array == null || seg.Count == 0)
            {
                owner?.Dispose();
                return;
            }
            bool transferred = false;
            try { _sessionManager.Active(userToken.ID); } catch { }
            if (userToken.WaitWrite(SocketOption.ActionTimeout) && userToken.Socket != null && userToken.Socket.Connected)
            {
                try
                {
                    var writeArgs = userToken.WriteArgs;
                    if (writeArgs != null)
                    {
                        userToken.SendingOwner = owner;
                        transferred = true;
                        writeArgs.SetBuffer(seg.Array, seg.Offset, seg.Count);
                        bool asyncPending = userToken.Socket.SendAsync(writeArgs);
                        if (!asyncPending)
                        {
                            ProcessSended(writeArgs);
                        }
                        else
                        {
                            userToken.IsSending = true;
                            Task.Run(async () =>
                            {
                                await Task.Delay(SocketOption.ActionTimeout);
                                lock (userToken)
                                {
                                    if (userToken.IsSending)
                                    {
                                        try { userToken.TakeSendingOwner()?.Dispose(); } catch { }
                                        userToken.IsSending = false;
                                        userToken.ReleaseWrite();
                                    }
                                }
                            });
                        }
                    }
                    else
                    {
                        userToken.TakeSendingOwner()?.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    userToken.TakeSendingOwner()?.Dispose();
                    transferred = true;
                    OnError?.Invoke($"An exception occurs when a message is sending:{userToken?.ID}", ex);
                }
            }
            else
            {
                OnError?.Invoke($"An exception occurs when a message is sending:{userToken?.ID}", new TimeoutException("Sending data timeout"));
            }
            if (!transferred) owner?.Dispose();
        }
```

> The `Task.Delay(ActionTimeout)` watchdog mirrors the existing `SendAsync(IUserToken, byte[])` timeout behavior. On timeout it now also releases the owner so the pool buffer cannot leak.

- [ ] **Step 3: Add the public memory/span/protocal/end/endpoint send members**

Insert after the existing `Send(IUserToken userToken, byte[] data)` method (ends around line 494):

```csharp
        public void Send(string sessionID, ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) return;
            var userToken = _sessionManager.Get(sessionID);
            if (userToken == null) return;
            var writer = new PooledBufferWriter(data.Length);
            data.CopyTo(writer.GetSpan(data.Length));
            writer.Advance(data.Length);
            writer.TryGetArray(out var rented);
            SendAsyncRaw(userToken, rented, writer);
        }

        public void SendAsync(string sessionID, ReadOnlyMemory<byte> data)
        {
            if (data.Length == 0) return;
            var userToken = _sessionManager.Get(sessionID);
            if (userToken == null) return;
            if (MemoryMarshal.TryGetArray(data, out var seg))
            {
                SendAsyncRaw(userToken, seg, null);
                return;
            }
            var writer = new PooledBufferWriter(data.Length);
            data.Span.CopyTo(writer.GetSpan(data.Length));
            writer.Advance(data.Length);
            writer.TryGetArray(out var rented);
            SendAsyncRaw(userToken, rented, writer);
        }

        public void SendAsync(string sessionID, ISocketProtocal protocal)
        {
            if (protocal == null) return;
            var userToken = _sessionManager.Get(sessionID);
            if (userToken == null) return;
            var coder = userToken.Coder;
            if (coder == null)
            {
                OnError?.Invoke(userToken?.ID ?? "", new InvalidOperationException("SAEA SocketError:coder 未初始化"));
                return;
            }
            var bodyLen = protocal.BodyLength;
            var size = bodyLen > 0 && bodyLen < int.MaxValue - 64 ? (int)bodyLen + 64 : 64;
            var writer = new PooledBufferWriter(size);
            try
            {
                coder.Encode(protocal, writer);
            }
            catch (Exception ex)
            {
                writer.Dispose();
                OnError?.Invoke(userToken?.ID ?? "", ex);
                return;
            }
            writer.TryGetArray(out var rented);
            SendAsyncRaw(userToken, rented, writer);
        }

        public void End(string sessionID, ReadOnlyMemory<byte> data)
        {
            var userToken = _sessionManager.Get(sessionID);
            if (userToken == null) return;
            try
            {
                if (userToken.Socket != null && userToken.Socket.Connected && data.Length > 0)
                {
                    var copy = data.ToArray();
                    var writeArgs = userToken.WriteArgs;
                    if (writeArgs != null && userToken.WaitWrite(SocketOption.ActionTimeout))
                    {
                        writeArgs.SetBuffer(copy, 0, copy.Length);
                        if (!userToken.Socket.SendAsync(writeArgs)) ProcessSended(writeArgs);
                    }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(userToken?.ID ?? "", ex);
            }
            Disconnect(userToken);
        }

        public void SendAsync(IPEndPoint ipEndPoint, ReadOnlyMemory<byte> data)
        {
            if (ipEndPoint == null) return;
            SendAsync(ipEndPoint.ToString(), data);
        }
```

> `Send(string, ReadOnlySpan<byte>)` transfers the rented `writer` to `userToken.SendingOwner`; it is **not** wrapped in `using`. That is intentional — disposal happens in `ProcessSended`.

- [ ] **Step 4: Build**

Run: `dotnet build Src/SAEA.Sockets/SAEA.Sockets.csproj -c Debug`
Expected: IOCP server errors gone; Stream/UDP implementer errors remain.

- [ ] **Step 5: Commit Tasks 2–5 together (first green checkpoint for interfaces + IOCP)**

Tasks 2 and 3 introduced interface members and Task 4/5 implemented them on IOCP, but the assembly is still red until Stream/UDP are done in Tasks 6–7. To keep commits green, **do not commit until Task 7 Step 4**. This step is a placeholder; skip to Task 6.

---

### Task 6: Implement the new surface in the Stream implementers (minimal, real work in 2B)

**Files:**
- Modify: `Src/SAEA.Sockets/Core/Tcp/StreamClientSocket.cs`
- Modify: `Src/SAEA.Sockets/Core/Tcp/StreamServerSocket.cs`

**Intent:** satisfies the interface minimally so the solution is green; the proper `PipeReader`-based receive and true zero-copy send land in Plan 2B. The span receive event is raised alongside the existing byte[] event (additive).

- [ ] **Step 1: Inspect both files**

Read `StreamClientSocket.cs` and `StreamServerSocket.cs` completely. Note: `StreamClientSocket.OnReceive` is marked obsolete and never raised; `StreamServerSocket` runs a read loop with a single shared `_receiveBuffer`. Locate every existing `Send`/`SendAsync`/`End` member and the `IUserToken` receive raise site.

- [ ] **Step 2: `StreamClientSocket` — add span event + send members**

Add the event next to `OnReceive`:

```csharp
        public event OnClientReceiveSpanHandler OnClientReceiveSpan;
```

Add the send members (adapt to the existing field/method names found in Step 1 — the pattern is: materialize to `byte[]` then delegate to the existing byte[] path):

```csharp
        public void Send(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) return;
            Send(data.ToArray());
        }

        public void SendAsync(ReadOnlyMemory<byte> data)
        {
            if (data.Length == 0) return;
            SendAsync(data.ToArray());
        }

        public void SendAsync(ISocketProtocal protocal)
        {
            if (protocal == null || _userToken?.Coder == null) return;
            using (var writer = new PooledBufferWriter(protocal.BodyLength > 0 && protocal.BodyLength < int.MaxValue - 64 ? (int)protocal.BodyLength + 64 : 64))
            {
                _userToken.Coder.Encode(protocal, writer);
                SendAsync(writer.WrittenSpan.ToArray());
            }
        }

        public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            return Task.Run(() => SendAsync(data), cancellationToken);
        }
```

Add usings as needed: `using System.Buffers;` (not strictly needed here), `using SAEA.Common.Caching;`, `using SAEA.Sockets.Interface;`, `using SAEA.Sockets.Handler;`.

- [ ] **Step 3: `StreamServerSocket` — add span event, raise it, add send members**

Add next to the existing `OnReceive`:

```csharp
        public event OnServerReceiveSpanHandler OnServerReceiveSpan;
```

In the read loop, immediately after the existing receive-byte handling and before/alongside `OnReceive?.Invoke(...)`, raise:

```csharp
        OnServerReceiveSpan?.Invoke(userToken, buffer.AsSpan(0, bytesRead));
```

(exact variable names depend on Step 1 inspection; the span must cover exactly the bytes read). Keep the existing `OnReceive` raise untouched (additive).

Add the send members (delegating to the existing byte[] server send path, e.g. `SendAsync(sessionID, byte[])` / `End`):

```csharp
        public void Send(string sessionID, ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) return;
            Send(sessionID, data.ToArray());
        }

        public void SendAsync(string sessionID, ReadOnlyMemory<byte> data)
        {
            if (data.Length == 0) return;
            SendAsync(sessionID, data.ToArray());
        }

        public void SendAsync(string sessionID, ISocketProtocal protocal)
        {
            if (protocal == null) return;
            var userToken = _sessionManager.Get(sessionID);
            if (userToken?.Coder == null) return;
            using (var writer = new PooledBufferWriter(protocal.BodyLength > 0 && protocal.BodyLength < int.MaxValue - 64 ? (int)protocal.BodyLength + 64 : 64))
            {
                userToken.Coder.Encode(protocal, writer);
                SendAsync(sessionID, writer.WrittenSpan.ToArray());
            }
        }

        public void End(string sessionID, ReadOnlyMemory<byte> data)
        {
            End(sessionID, data.ToArray());
        }

        public void SendAsync(IPEndPoint ipEndPoint, ReadOnlyMemory<byte> data)
        {
            SendAsync(ipEndPoint.ToString(), data.ToArray());
        }
```

If `StreamServerSocket` has no `End(string, byte[])`/`SendAsync(IPEndPoint, byte[])`, implement these directly against its `Send(sessionID, byte[])`/session lookup, matching the semantics of the IOCP versions (End = send then `Disconnect(sessionID)`).

- [ ] **Step 4: Build**

Run: `dotnet build Src/SAEA.Sockets/SAEA.Sockets.csproj -c Debug`
Expected: Stream errors gone; UDP implementer errors remain.

---

### Task 7: Implement the new surface in the UDP implementers

**Files:**
- Modify: `Src/SAEA.Sockets/Core/Udp/UdpClientSocket.cs`
- Modify: `Src/SAEA.Sockets/Core/Udp/UdpServerSocket.cs`

**Known issue:** `UdpClientSocket` declares its **own nested** `internal delegate void OnClientReceiveSpanHandler` and `internal event OnClientReceiveSpan` — these shadow the public handler and will **not** satisfy `IClientSocket`. Fix by deleting the nested delegate and switching the event to the public `SAEA.Sockets.Handler.OnClientReceiveSpanHandler` type (add `using SAEA.Sockets.Handler;`). The raise site stays as-is.

- [ ] **Step 1: `UdpClientSocket` — de-shadow the span event**

In `UdpClientSocket.cs`: delete the nested `internal delegate void OnClientReceiveSpanHandler(...)` declaration; change `internal event OnClientReceiveSpanHandler OnClientReceiveSpan;` to `public event OnClientReceiveSpanHandler OnClientReceiveSpan;` resolving to `SAEA.Sockets.Handler.OnClientReceiveSpanHandler`. Add `using SAEA.Sockets.Handler;` if absent.

- [ ] **Step 2: `UdpClientSocket` — add send members**

Add `Send(ReadOnlySpan<byte>)`, `SendAsync(ReadOnlyMemory<byte>)`, `SendAsync(ISocketProtocal)`, `Task SendAsync(ReadOnlyMemory<byte>, CancellationToken)` following the exact delegate-to-existing-byte[] pattern from Task 6 Step 2, using `_userToken.Coder` for the protocal overload. `UdpClientSocket` is a datagram socket; its byte[] `Send`/`SendAsync` already exist (confirm in Step 1 inspection) — delegate to them. For the protocal overload, encode into `PooledBufferWriter` then `writer.WrittenSpan.ToArray()`.

- [ ] **Step 3: `UdpServerSocket` — add span event + send members**

Add `public event OnServerReceiveSpanHandler OnServerReceiveSpan;` (public handler type). Raise it in the datagram receive path from the received buffer span, alongside the existing `OnReceive`. Add the five `IServerSocket` send members (`Send(string, ReadOnlySpan<byte>)`, `SendAsync(string, ReadOnlyMemory<byte>)`, `SendAsync(string, ISocketProtocal)`, `End(string, ReadOnlyMemory<byte>)`, `SendAsync(IPEndPoint, ReadOnlyMemory<byte>)`) delegating to the existing byte[] methods; for `End`, follow the IOCP/Stream End semantics (send then disconnect the session/channel).

- [ ] **Step 4: Build — now green**

Run: `dotnet build Src/SAEA.Sockets.sln -c Debug`
Expected: **0 errors**, full solution (interfaces + all six implementers satisfied).

- [ ] **Step 5: Run the full suite**

Run: `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug -- --all`
Expected: all pass (288 baseline — 283 Plan 1 + Task 1's 5 assertions). No new failures.

- [ ] **Step 6: Commit Tasks 2–7 (interfaces + all implementers)**

```powershell
git add Src/SAEA.Sockets/IClientSocket.cs Src/SAEA.Sockets/IServerSocket.cs Src/SAEA.Sockets/Core/Tcp/IocpClientSocket.cs Src/SAEA.Sockets/Core/Tcp/IocpServerSocket.cs Src/SAEA.Sockets/Core/Tcp/StreamClientSocket.cs Src/SAEA.Sockets/Core/Tcp/StreamServerSocket.cs Src/SAEA.Sockets/Core/Udp/UdpClientSocket.cs Src/SAEA.Sockets/Core/Udp/UdpServerSocket.cs
git diff --cached --name-only
git commit -m "feat(sockets): add Span/Memory send + span receive surface to IClientSocket/IServerSocket and all implementers"
```

> Verify staged names match the eight files above before committing. Do not stage unrelated files.

---

### Task 8: Migrate `IocpBenchmark` to the new span/memory path + allocation assertion

**Files:**
- Modify: `Src/SAEA.P2PTest/Tests/IocpBenchmark.cs`

**Baseline facts (current file):** `ClientReceiveAsync` subscribes to `client.OnReceive +=` for `LegacyDecode` (lines 74–83) and to `client.OnClientReceiveSpan +=` for both span modes (84–96). `ServerReceiveAsync` similarly uses `server.OnReceive +=` (125–134) and `server.OnServerReceiveSpan +=` (135–147). `Drive` measures `GC.GetTotalAllocatedBytes(true)` per run (lines 166–194). `Report` prints B/frame (line 210) and asserts completeness (215–220).

- [ ] **Step 1: Keep a legacy-comparison mode but drive it via the span event**

The `LegacyDecode` mode currently proves the *old* receive event's cost. Keep the comparison meaningful while removing the `byte[]` **event** dependency: subscribe to `client.OnClientReceiveSpan` and inside that handler materialize `span.ToArray()` + `decoder.Decode(new ReadOnlySequence<byte>(copy))`, i.e. the handler emulates "one copy + materialized decode". Rename nothing publicly; only change the subscription.

For `ClientReceiveAsync`, replace lines 74–96 with:

```csharp
            if (mode == ReceiveMode.LegacyDecode)
                client.OnClientReceiveSpan += span =>
                {
                    var copy = span.ToArray();
                    counter.AddChunk(copy.Length);
                    using (var d = decoder.Decode(new ReadOnlySequence<byte>(copy)))
                    {
                        counter.AddFrames(d.Count);
                    }
                    counter.Complete();
                };
            else if (mode == ReceiveMode.SpanDecodeStream)
                client.OnClientReceiveSpan += span =>
                {
                    counter.AddChunk(span.Length);
                    decoder.DecodeStream(span, counter);
                    counter.Complete();
                };
            else
                client.OnClientReceiveSpan += span =>
                {
                    counter.AddChunk(span.Length);
                    counter.Complete();
                };
```

Do the same for `ServerReceiveAsync`, replacing lines 125–147 so all three modes use `server.OnServerReceiveSpan` (with `(token, span) =>`). **After this task no `OnReceive` byte[] event is referenced anywhere in `IocpBenchmark.cs`.**

- [ ] **Step 2: Exercise the new send path**

In `ClientReceiveAsync`, after `client.ConnectAsync()` and `await listener.AcceptTcpClientAsync()`, before `Drive`, add a smoke assertion that the new memory send path works (send one frame via `SendAsync(ReadOnlyMemory<byte>)` and confirm the server-side raw socket receives `frame.Length` bytes):

```csharp
            client.SendAsync(new ReadOnlyMemory<byte>(frame));
            var sendCheck = new byte[frame.Length];
            int read = 0;
            while (read < frame.Length)
            {
                int n = accepted.Client.Receive(sendCheck, read, frame.Length - read, SocketFlags.None);
                if (n <= 0) break;
                read += n;
            }
            TestHarness.Expect(read == frame.Length, "client SendAsync(ReadOnlyMemory) delivers full frame", $"read={read}");
```

Do an equivalent server-side smoke assertion in `ServerReceiveAsync` using `server.SendAsync(sessionID, new ReadOnlyMemory<byte>(frame))` with the accepted `TcpClient` reading it back. (The server's session id is available via `server.SessionManager`; wait for the session with `TestHarness.WaitUntil(() => server.ClientCounts > 0)` before sending.) Also send at least one frame through `SendAsync(ISocketProtocal)` in each side and assert delivery, to cover the encode+owner path.

- [ ] **Step 3: Add the allocation assertion for the span path**

In `Report`, after the existing completeness assertions, add a soft assertion that the zero-copy span path allocates materially less per frame than the legacy copy path. Because the two modes run in separate `Drive` calls, capture the `SpanDecodeStream` B/frame in a static field during `ClientReceiveAsync`/`ServerReceiveAsync` and compare in `RunAsync` after both have run:

```csharp
        static long _clientSpanBytesPerFrame;
        static long _clientLegacyBytesPerFrame;
```

In `Report`, when `side == "client"`, record `bytesPerFrame` into `_clientLegacyBytesPerFrame` for `LegacyDecode` and `_clientSpanBytesPerFrame` for `SpanDecodeStream`. Then at the end of `RunAsync`, before `WriteSummary`:

```csharp
            TestHarness.Expect(_clientSpanBytesPerFrame > 0 && _clientSpanBytesPerFrame < _clientLegacyBytesPerFrame,
                "SpanDecodeStream allocates less per frame than LegacyDecode",
                $"span={_clientSpanBytesPerFrame} legacy={_clientLegacyBytesPerFrame}");
```

- [ ] **Step 4: Run the benchmark**

Run: `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug -- --bench-iocp`
Expected: all six runs complete; new smoke + allocation assertions pass; `SpanDecodeStream` B/frame ≤ Plan 1 ICE (no regression).

- [ ] **Step 5: Run the full suite**

Run: `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug -- --all`
Expected: all pass.

- [ ] **Step 6: Commit**

```powershell
git add Src/SAEA.P2PTest/Tests/IocpBenchmark.cs
git diff --cached --name-only
git commit -m "test(bench): drive IocpBenchmark via span receive and new memory/protocal send"
```

---

### Task 9: Green gate + plan closeout

- [ ] **Step 1: Debug + Release build of the full solution**

Run:
```powershell
dotnet build Src/SAEA.Sockets.sln -c Debug
dotnet build Src/SAEA.Sockets.sln -c Release
```
Expected: both `Build succeeded`, 0 errors.

- [ ] **Step 2: Full test suite**

Run: `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug -- --all`
Expected: all pass.

- [ ] **Step 3: Confirm no out-of-scope files changed**

Run: `git status --porcelain` and `git diff --name-only HEAD~4..HEAD`
Expected: only files under `Src/SAEA.Sockets/` and `Src/SAEA.P2PTest/Tests/IocpBenchmark.cs` were modified across Tasks 1–8. If anything else appears, investigate before closing.

- [ ] **Step 4: Confirm the byte[] receive events are no longer used by the benchmark**

Run (grep): search `Src/SAEA.P2PTest/Tests/IocpBenchmark.cs` for `OnReceive +=` — expected: no matches.

- [ ] **Step 5: Record outcome**

Append a short "Plan 2A outcome" note (commit range, pass count, benchmark B/frame before/after) to `docs/superpowers/plans/2026-09-21-span-memory-pipeline-socket-core.md` and commit:

```powershell
git add docs/superpowers/plans/2026-09-21-span-memory-pipeline-socket-core.md
git commit -m "docs(plan): record Plan 2A outcome"
```

---

## Risks / watch-items
- **Interface widening breaks external implementers.** Only the six implementers live in-repo; `SAEA.Audio.Net` etc. consume the *published* package and are unaffected. Verified: `SAEA.Sockets` holds all `IClientSocket`/`IServerSocket` implementations.
- **Send-buffer lifetime.** Every rented `PooledBufferWriter` must be disposed exactly once: on `ProcessSended`, on the `SendAsyncRaw` failure/timeout/completion paths, and on `BaseUserToken.Clear()`. The watchdog in `IocpServerSocket` also releases on `ActionTimeout`. Ownership publication uses `Volatile.Write` in the `SendingOwner` setter and `Interlocked.Exchange` in `TakeSendingOwner()` so a concurrent `Clear()` observes the owner.
- **Accepted 2A limitation (residual buffer-return timing).** `Socket.Close()` aborts a pending overlapped send but does not guarantee the kernel has finished reading the buffer before it returns, so a pooled buffer could in principle be returned while still in use. This is strictly better than the previous ordering and is knowingly accepted for 2A; a completion-drain / deferred-return belongs in a later pass.
- **`WaitWrite` serialization.** One in-flight send per token means a single `SendingOwner` slot is sufficient. If a future change allows concurrent sends, this must become a queue.
- **UDP shadowed delegate.** `UdpClientSocket`'s nested `OnClientReceiveSpanHandler` will silently fail interface satisfaction; Task 7 Step 1 is mandatory.
- **`End` correctness.** `End` materializes a plain `byte[]` so it is safe across the immediate `Disconnect`; it is intentionally not zero-alloc (not a hot path).
- **Do not touch** `Src/SAEA.Sockets/Core/Tcp/IocpServerSocket.cs`'s pre-existing ghost stat entry — but note Task 5 legitimately modifies this file, so `git add` of that path is correct at Task 7 Step 6.
- **No comments policy**: the codebase forbids added comments; keep XML doc comments consistent with surrounding style only, and do not add inline `//` commentary.
