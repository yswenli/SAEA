# Plan 2B: Stream `PipeReader` + Stream/Shortcut token model + UDP + Consumer Adaptation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the Stream backend a real per-connection `IUserToken` so `StreamServerSocket` can raise `OnServerReceiveSpan` from a per-connection `PipeReader` loop, finish the UDP Span/Memory send surface, modernize the `Shortcut/` adapters and their consumers (`SAEA.MQTT`, `SAEA.DNS`, `SAEA.Sockets.UdpTest`), and add the missing Stream/UDP tests — all while the full solution stays green.

**Architecture:** **Convergence, not removal.** Plan 2A added the Span/Memory surface as *additive* stubs; 2B makes the Stream/UDP/Shortcut implementations *real* (no gratuitous `.ToArray()`), and introduces the Stream session identity that Plan 2A deferred. The legacy `byte[]` interface members are still **kept and functional**; their removal (and the `_isBase*Type`/`.ToArray()` compatibility branches) is **Plan 2C**.

**Tech Stack:** C# / .NET Standard 2.0 (`SAEA.Sockets`), `System.IO.Pipelines` 10.0.6 + `Pipelines.Sockets.Unofficial` 2.2.8 (already referenced in `SAEA.Sockets.csproj:50-51`), `SAEA.Common` (`PooledBufferWriter`, `MemoryPoolManager`), `SAEA.P2PTest` console harness (`TestHarness`, `Program.cs --all`).

**Baseline:** HEAD `8846aee4`; suite **288/288**; Plan 2A closed.

---

## Scope

### In scope (Plan 2B)
- **Stream token model:** new `StreamUserToken : BaseUserToken` (carries `Stream` + `PipeReader Input`); `ChannelInfo.UserToken`; `StreamServerSocket` creates/propagates it; `OnAccepted` keeps its `ChannelInfo` payload (no consumer regression), `OnServerReceiveSpan` carries the `IUserToken`.
- **Stream receive:** `StreamServerSocket` accept read loop rewritten to per-connection `PipeReader.Create(networkStream)` replacing the shared `_receiveBuffer` + `Stream.Read`; single-segment delivered as `ReadOnlySpan<byte>`, multi-segment delivered per segment; raises `OnServerReceiveSpan` **and** the legacy `OnReceive` (byte[] retained through 2C).
- **Stream send:** real Span/Memory implementations (server + client) that avoid needless `.ToArray()` where the contract/`Stream` allows, preserving the existing sync/async split of §5.3.
- **UDP:** remove `dataSpan.ToArray()` in receive; Span/Memory send overloads rent a pooled buffer once (ns2.0 has no `Socket.SendTo(Span)`) with ownership returned on completion.
- **Shortcut:** `TCPClient`/`TCPServer`/`UDPClient`/`UDPServer` events → `ReadOnlyMemory<byte>`/span; `Send`/`SendAsync` onto the new surface; `TCPServer.OnAccepted`/session handling made token-correct for both IOCP and Stream.
- **Consumers adapted:** `SAEA.MQTT` (`MqttTcpServerListener`/`MqttTcpChannel`), `SAEA.DNS` (`DnsServer.cs`, `Coder/UdpRequestCoder.cs`), `SAEA.Sockets.UdpTest/Program.cs`.
- **Tests:** Stream server span/PipeReader test, UDP span test, and a Shortcut smoke test in `SAEA.P2PTest`.
- **Spec amendment:** `docs/superpowers/specs/2026-09-21-span-memory-pipeline-design.md` §5.3 — replace the "keep `SessionManager` throwing, do not add semantics" instruction with the approved Stream token model.

### Explicitly out of scope (deferred)
- **Removal** of `byte[]` interface members, `_isBase*Type`/`.ToArray()` branches, `SocketStream` byte[] bridge → **Plan 2C**.
- `SAEA.P2P` / relay optimization → **Plan 2C** (spec §六).
- Dead-code deletion `Src/SAEA.Sockets/Core/RioExtention.cs` → **Plan 2C**.
- Version bump `26.9.21.1`, README updates, final DoD → **Plan 2C**.
- `SAEA.Http`/`WebSocket`/`FTP`/`FileSocket`/`MessageSocket`/`QueueSocket`/`RPC`/`RedisSocket` consumer migration → 2C (their `WebHost.Send/End(IUserToken, byte[])` follows the 2C API removal).

### Non-negotiable invariants
- **Wire format unchanged**: `[8B little-endian length][1B Type][body]`; `P_LEN=8`, `P_Type=1`, `P_Head=9`.
- **Timing semantics unchanged** (spec §2.3 #8/#9): connection/disconnect/timeout/send-complete/`Dispose` ordering and heartbeat/send/receive callback ordering must not change. In particular preserve `StreamServerSocket.SendAsync(string,...)` fire-and-forget vs `Send`/`End` synchronous `Stream.Write` (§5.3).
- The solution must **compile** after every task; `--all` must pass at the end of every task.
- No `.cs` file outside `Src/SAEA.Sockets/`, `Src/SAEA.P2PTest/`, `Src/SAEA.MQTT/`, `Src/SAEA.DNS/`, `Src/SAEA.Sockets.UdpTest/` is modified.
- Never use `git add -A`/`git add .`; stage explicit paths only and verify with `git diff --cached --name-only`.
- **No inline `//` comments** (XML `///` docs allowed only).
- Public API of untouched consumers (`SAEA.Audio.Net`, `SAEA.FTPTest`, `SAEA.Mvc.ServiceTest`, `TestConnectionReuseApp`) must not break (they consume the published package, PROJECT-REF projects must remain compilable).

## Acceptance (definition of done for 2B)
- `dotnet build Src/SAEA.Sockets.sln -c Debug` → 0 errors.
- `dotnet build Src/SAEA.Sockets.sln -c Release` → 0 errors.
- `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug -- --all` → all pass (288 baseline + new Stream/UDP/Shortcut assertions).
- `StreamServerSocket` no longer uses the shared `_receiveBuffer` for receiving and raises `OnServerReceiveSpan(IUserToken, ReadOnlySpan<byte>)` from a `PipeReader.ReadAsync` loop.
- Stream/UDP `Send(ReadOnlySpan<byte>)`/`SendAsync(ReadOnlyMemory<byte>)` do not allocate a `byte[]` on the fast path beyond what ns2.0 `Stream.WriteAsync(ReadOnlyMemory)`/`Socket.SendTo` mandate (documented at each site).
- `SAEA.MQTT` and `SAEA.DNS` compile and their existing behavior is preserved.
- Spec §5.3 amended; a short "Plan 2B outcome" note appended at closeout.

## Design decisions (approved)
1. **Stream gets a real per-connection `IUserToken`** (user-approved option): a new `StreamUserToken : BaseUserToken` carrying the connection `Stream` and its `PipeReader Input`. It is the payload of `OnServerReceiveSpan` and is stored on `ChannelInfo`. Rationale: `OnServerReceiveSpanHandler` requires `IUserToken`; remote-identity consistency with IOCP; lets `TCPServer`/`DnsServer` treat both backends uniformly.
2. **`OnAccepted` payload stays `ChannelInfo`** for the Stream backend (MQTT depends on `ci.Stream`/`ci.ID`); the token is reachable via `ChannelInfo.UserToken`. Do not change the `OnAcceptedHandler(object)` delegate.
3. **`StreamServerSocket.SessionManager` remains `NotImplementedException`.** `SessionManager` is an IOCP-specific pool/binder (`IContext<ICoder>`, `SocketAsyncEventArgs`, pooling) and reusing it for Stream would change timing (violates §2.3 #8). The real identity now lives on `ChannelInfo.UserToken`; the throwing property is retained and documented. Spec §5.3 is amended to say exactly this. *(If this is unacceptable, it is a one-line escalation before Task 1.)*
4. **Both receive events are raised during 2B** (byte[] + span) so existing consumers keep working until 2C deletes the byte[] surface.

## File Structure
- **Add** `Src/SAEA.Sockets/Model/StreamUserToken.cs` — `StreamUserToken : BaseUserToken` (`Stream`, `PipeReader Input`).
- **Modify** `Src/SAEA.Sockets/Model/ChannelInfo.cs` — add `IUserToken UserToken { get; set; }`.
- **Modify** `Src/SAEA.Sockets/Core/Tcp/StreamServerSocket.cs` — token creation, `PipeReader` loop, span raise, real span/memory sends.
- **Modify** `Src/SAEA.Sockets/Core/Tcp/StreamClientSocket.cs` — real span/memory sends (no read loop added).
- **Modify** `Src/SAEA.Sockets/Core/Udp/UdpClientSocket.cs` — remove receive `ToArray()`, pooled send ownership.
- **Modify** `Src/SAEA.Sockets/Core/Udp/UdpServerSocket.cs` — pooled send ownership.
- **Modify** `Src/SAEA.Sockets/Shortcut/{TCPClient,TCPServer,UDPClient,UDPServer}.cs` — span/memory events + sends.
- **Modify** `Src/SAEA.MQTT/Implementations/{MqttTcpServerListener,MqttTcpChannel}.cs` — Stream token/ChannelInfo wiring.
- **Modify** `Src/SAEA.DNS/{DnsServer.cs,Coder/UdpRequestCoder.cs}` — byte[] consumer adaptation.
- **Modify** `Src/SAEA.Sockets.UdpTest/Program.cs` — Shortcut event/send adaptation.
- **Add tests** `Src/SAEA.P2PTest/Tests/StreamPipelineTest.cs`, `Src/SAEA.P2PTest/Tests/UdpPipelineTest.cs`.
- **Modify** `Src/SAEA.P2PTest/Program.cs` — register the new test runs.
- **Modify** `docs/superpowers/specs/2026-09-21-span-memory-pipeline-design.md` — §5.3 amendment.
- **Modify** `docs/superpowers/plans/2026-09-21-span-memory-pipeline-stream-udp-shortcut.md` — outcome note.

---

### Task 1: Stream token model + `ChannelInfo.UserToken` + spec §5.3 amendment

**Why first:** every later Stream task depends on the token type and its `ChannelInfo` slot.

**Files:**
- Add: `Src/SAEA.Sockets/Model/StreamUserToken.cs`
- Modify: `Src/SAEA.Sockets/Model/ChannelInfo.cs`
- Modify: `docs/superpowers/specs/2026-09-21-span-memory-pipeline-design.md` (§5.3)

- [ ] **Step 1: Add `StreamUserToken`**

Create `Src/SAEA.Sockets/Model/StreamUserToken.cs` (same license header style as sibling files), namespace `SAEA.Sockets.Model`:

```csharp
public class StreamUserToken : SAEA.Sockets.Base.BaseUserToken
{
    public System.IO.Stream Stream { get; set; }

    public System.IO.Pipelines.PipeReader Input { get; set; }
}
```

Keep `Clear()` inherited (it closes `Socket`, which for `new NetworkStream(clientSocket, true)` also disposes the stream). Do **not** override unless a leak is proven in Task 2's test; if an override is needed, call `base.Clear()` and complete `Input`.

- [ ] **Step 2: Add the slot to `ChannelInfo`**

In `Src/SAEA.Sockets/Model/ChannelInfo.cs`, after the `Stream` property (line 58), add:

```csharp
        /// <summary>
        /// 获取或设置通道对应的用户令牌
        /// </summary>
        public SAEA.Sockets.Interface.IUserToken UserToken { get; set; }
```

- [ ] **Step 3: Amend the spec §5.3**

In `docs/superpowers/specs/2026-09-21-span-memory-pipeline-design.md` §5.3, replace the two "保持不回归 / 保持抛 `NotImplementedException` / 不强行新增语义" statements (lines ~326 and ~329) with the approved model: `StreamServerSocket` creates a `StreamUserToken` per accepted connection (carried on `ChannelInfo.UserToken`, exposed as the `OnServerReceiveSpan` payload), and `SessionManager` intentionally remains `NotImplementedException` because it is IOCP-specific and reusing it would change timing (§2.3 #8). Keep the `PipeReader Input` exposure statement.

- [ ] **Step 4: Build**

Run: `dotnet build Src/SAEA.Sockets/SAEA.Sockets.csproj -c Debug`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 5: Commit**

```powershell
git add Src/SAEA.Sockets/Model/StreamUserToken.cs Src/SAEA.Sockets/Model/ChannelInfo.cs docs/superpowers/specs/2026-09-21-span-memory-pipeline-design.md
git diff --cached --name-only
git commit -m "feat(sockets): add StreamUserToken and ChannelInfo.UserToken"
```

---

### Task 2: `StreamServerSocket` `PipeReader` accept loop + `OnServerReceiveSpan`

**Files:**
- Modify: `Src/SAEA.Sockets/Core/Tcp/StreamServerSocket.cs`

- [ ] **Step 1: Create the token in `ProcessAccepte`**

In `ProcessAccepte` (line ~176), after `ChannelManager.Instance.Set(...)` (line 210) and before `OnAccepted?.Invoke(ci)`:
- create a `StreamUserToken` with `ID = id`, `Socket = clientSocket`, `Stream = nsStream`, a freshly-created `Coder` of the configured type (mirror `SAEA.Sockets.Core.UserTokenFactory.Create(IContext<ICoder>)`: instance of `SocketOption.Context.Unpacker.GetType()`; if `Context`/`Unpacker` is unavailable on this path, default to `new SAEA.Sockets.Base.BaseCoder()` and note it),
- set `ci.UserToken = token`,
- create `token.Input = System.IO.Pipelines.PipeReader.Create(nsStream)`,
- start `_ = Task.Run(() => ProcessAccepted(ci, token));`.
Keep `OnAccepted?.Invoke(ci)` before the task start (payload unchanged).

Add `using System.IO.Pipelines;` (or fully-qualify) and `using SAEA.Sockets.Model;` as needed.

- [ ] **Step 2: Rewrite the read loop**

Replace `ProcessAccepted(string id, Stream nsStream)` (lines 249+) with `async Task ProcessAccepted(ChannelInfo ci, StreamUserToken token)`:

```csharp
var reader = token.Input;
while (!_isStoped && (OnReceive != null || OnServerReceiveSpan != null))
{
    ReadResult result;
    try { result = await reader.ReadAsync(_cancellationToken).ConfigureAwait(false); }
    catch (Exception ex) { OnDisconnected?.Invoke(ci.ID, ex); break; }

    var buffer = result.Buffer;
    try
    {
        foreach (var segment in buffer)
        {
            if (segment.Length == 0) continue;
            ChannelManager.Instance.Refresh(ci.ID);
            OnServerReceiveSpan?.Invoke(token, segment.Span);
            OnReceive?.Invoke(token, segment.ToArray());
        }
    }
    finally { reader.AdvanceTo(buffer.End); }

    if (result.IsCompleted) break;
}

reader.Complete();
```

Notes:
- The loop is **gated on a receive subscriber** (`!_isStoped && (OnReceive != null || OnServerReceiveSpan != null)`), exactly preserving the old `!_isStoped && OnReceive != null` semantics. Several `UseStream()` servers consume `ci.Stream` themselves through `OnAccepted` and never subscribe a receive event — `SAEA.MQTT/Implementations/MqttTcpServerListener.cs:142`, `SAEA.WebSocket/Core/WSSServerImpl.cs:112/130/149`, `SAEA.Socket5/Server/Socks5Server.cs:111/354/469` — so an unconditional loop would race them for the same `Stream`. Accept-time vs in-loop check is equivalent: the old loop also never executed (and permanently exited) when no subscriber was attached at accept.
- Deliver **per segment**: single-segment buffers pass a zero-copy `segment.Span`; multi-segment buffers are delivered segment by segment (spec §5.3).
- The legacy `OnReceive` is **still raised** (byte[] retained through 2C), with `token` as the `ISession` payload — this is a **change** from the old `new Session(id)`; verified no Stream-server consumer depends on the concrete `Session` type (no `(Session)`/`as Session`/`new Session(` in `Src`; MQTT/WSS/Socks5 use `OnAccepted`/`ChannelInfo` and read `ci.Stream` directly, hence the gate above).
- Remove the shared `_receiveBuffer` field usage for receiving; if the field becomes unused, remove it. Keep `_receiveBuffer` only if another code path uses it (verify with grep).
- On exit, ensure `OnDisconnected` is raised for error paths as before and `reader.Complete()` runs; wire `Stop`/`Dispose` to cancel the token/complete readers (see Step 3).

- [ ] **Step 3: Stream token/reader lifecycle**

Decide and implement reader/stream ownership for `StreamUserToken`:
- **Option A (`leaveOpen: true`)** — create the reader via `PipeReader.Create(nsStream, new StreamPipeReaderOptions(leaveOpen: true))` and make stream disposal explicit and single-owner (the accept loop / channel-removal path).
- **Option B (default `leaveOpen: false`)** — treat `Input` as owning the stream and dispose it exactly once on the loop's exit path (including error/cancel paths), so completing `Input` also closes the network stream.

**Decision (implemented): Option A (`leaveOpen: true`).** The `NetworkStream` is shared with `ChannelInfo.Stream` and, when the loop is gated off, is owned by the `OnAccepted` consumer (MQTT/WSS/Socks5). Completing the reader must therefore never dispose it. `Input` stays inert until `ReadAsync` is first called, so exposing it is safe even when the gate keeps the loop from running.

Then:
- Override `StreamUserToken.Clear()` to complete `Input` in coordination with the loop exit (so a concurrent `ReadAsync` cannot surface an `ObjectDisposedException`), null `Stream`/`Input`, and call `base.Clear()` last; document the required ordering (cancel/complete reader → loop exits → `base.Clear()`). Note `BaseUserToken.Clear()` is **not `virtual`**, so the derived member is hidden with `new`; the only in-repo caller (`StreamServerSocket.Stop`) invokes it through the concrete `StreamUserToken` type.
- Wire `StreamServerSocket.Stop`/`Dispose` to complete each channel's `Input`. Note `ChannelManager.Clear()`/`Remove` do **not** touch `ChannelInfo.UserToken`, so `Stop`/`Dispose` must iterate the channels/tokens itself and complete every reader.
- Do not change disconnect timing (§2.3 #8).

- [ ] **Step 4: Build + smoke**

Run: `dotnet build Src/SAEA.Sockets.sln -c Debug` → 0 errors.
Run the new Stream test (added in Task 7) or, if Task 7 is not yet done, temporarily verify with `Src/SAEA.Sockets.TcpTest` conceptual path; do **not** commit a temporary test.

- [ ] **Step 5: Commit**

```powershell
git add Src/SAEA.Sockets/Core/Tcp/StreamServerSocket.cs
git diff --cached --name-only
git commit -m "feat(sockets): StreamServerSocket PipeReader receive loop with per-connection token"
```

---

### Task 3: Stream real Span/Memory send paths

**Files:**
- Modify: `Src/SAEA.Sockets/Core/Tcp/StreamServerSocket.cs`
- Modify: `Src/SAEA.Sockets/Core/Tcp/StreamClientSocket.cs`

- [ ] **Step 1: Server sends — remove the copy-through-`ToArray()` stubs**

Replace the Plan 2A stubs (`StreamServerSocket.cs:~346-378`) with implementations that use the existing helpers:
- `Send(string id, ReadOnlySpan<byte>)` → resolve `channel.Stream`, write via the same synchronous `Stream.Write` path used by the byte[] `Send` (ns2.0 has no `Stream.Write(ReadOnlySpan)`; a single `byte[]` copy at this boundary is required and **must be documented** in the XML doc). Keep the sync/async split: `SendAsync(string, ReadOnlyMemory<byte>)` stays fire-and-forget over `Stream.WriteAsync` (ns2.0 has no `WriteAsync(ReadOnlyMemory)` → one `ToArray()` at the boundary, documented); `SendAsync(string, ISocketProtocal)` encodes into a `PooledBufferWriter` (Plan 1) then sends and disposes the writer exactly once; `End(string, ReadOnlyMemory<byte>)` mirrors `End(string, byte[])` synchronously.
- Do not change `End`'s disconnect timing.

- [ ] **Step 2: Client sends**

Same for `StreamClientSocket.cs:~311-341`: route `Send(ReadOnlySpan<byte>)`, `SendAsync(ReadOnlyMemory<byte>)`, `SendAsync(ISocketProtocal)`, `SendAsync(ReadOnlyMemory<byte>, CancellationToken)` onto `_stream` with the existing sync/async semantics. No receive loop is added (spec §5.3: avoid a second source of truth).

- [ ] **Step 3: Build + suite**

Run: `dotnet build Src/SAEA.Sockets.sln -c Debug` → 0 errors.
Run: `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug -- --all` → 288/288.

- [ ] **Step 4: Commit**

```powershell
git add Src/SAEA.Sockets/Core/Tcp/StreamServerSocket.cs Src/SAEA.Sockets/Core/Tcp/StreamClientSocket.cs
git diff --cached --name-only
git commit -m "feat(sockets): real Span/Memory send paths for Stream sockets"
```

---

### Task 4: UDP receive + pooled send ownership

**Files:**
- Modify: `Src/SAEA.Sockets/Core/Udp/UdpClientSocket.cs`
- Modify: `Src/SAEA.Sockets/Core/Udp/UdpServerSocket.cs`

- [ ] **Step 1: Receive**

Confirm `UdpClientSocket.OnReceived` (`:227-262`) and `UdpServerSocket.OnReceiveBytes` (`:187-241`) already raise the span event with zero-copy; remove any residual `dataSpan.ToArray()` on that path. The byte[] `OnReceive` is still raised (2C removal).

- [ ] **Step 2: Send ownership**

Replace the Plan 2A `ToArray()` stubs (`UdpClientSocket.cs:~386-411`, `UdpServerSocket.cs:~475-507`) with a pooled copy and explicit ownership release:
- `Send(ReadOnlySpan<byte>)` / `SendAsync(ReadOnlyMemory<byte>)`: if `MemoryMarshal.TryGetArray` succeeds and the array is exact, send directly; otherwise rent once (SmallThreshold-aware `MemoryPoolManager`), copy, and release on send completion.
- `SendAsync(ISocketProtocal)`: encode into a `PooledBufferWriter`, send, dispose once (same ownership discipline as IOCP Task 4/5 in Plan 2A — use `TakeSendingOwner()` semantics if the UDP send path has a token; otherwise a `try/finally` with a `transferred` flag).
- ns2.0 has no `Socket.SendTo(Span)`/`SendTo(ReadOnlySpan)`, so a boundary copy is expected; document it.
- Preserve `ReceiveAsync`/`GetStream` throwing `NotSupportedException` (`UdpClientSocket.cs:460/465`).

- [ ] **Step 3: Build + suite**

Run: `dotnet build Src/SAEA.Sockets.sln -c Debug` → 0 errors; `--all` → 288/288.

- [ ] **Step 4: Commit**

```powershell
git add Src/SAEA.Sockets/Core/Udp/UdpClientSocket.cs Src/SAEA.Sockets/Core/Udp/UdpServerSocket.cs
git diff --cached --name-only
git commit -m "feat(sockets): zero-copy receive and pooled send ownership for UDP sockets"
```

---

### Task 5: Shortcut adapters

**Files:**
- Modify: `Src/SAEA.Sockets/Shortcut/TCPClient.cs`
- Modify: `Src/SAEA.Sockets/Shortcut/TCPServer.cs`
- Modify: `Src/SAEA.Sockets/Shortcut/UDPClient.cs`
- Modify: `Src/SAEA.Sockets/Shortcut/UDPServer.cs`

- [ ] **Step 1: Events**

`TCPClient.OnReceive(byte[])`, `TCPServer.OnReceive(..., byte[])`, `UDPClient.OnReceive`, `UDPServer.OnReceive(..., string, ISocketProtocal)` → migrate to the Span/Memory surface: subscribe to `OnClientReceiveSpan`/`OnServerReceiveSpan` and expose span/`ReadOnlyMemory<byte>` payloads. Keep the generic constraint `where Coder : class, ICoder` unchanged. `ISocketProtocal.Content` is now `ReadOnlyMemory<byte>` (Plan 1), so `UDPServer`/`UDPClient` decode consumers must accept that.

- [ ] **Step 2: Sends**

Route `TCPClient.SendAsync(byte[])`/`SendAsync(string)` and `TCPServer.SendAsync(string, byte[])`/`SendAsync(string, string)` onto the new `IClientSocket`/`IServerSocket` Span/Memory members. Replace any `BaseSocketProtocal.Parse(...).ToBytes()` with `SendAsync(ISocketProtocal)`.

- [ ] **Step 3: `TCPServer` token correctness**

`TCPServer` currently casts `OnAccepted` `obj` to `IUserToken` (`:141`) and the receive handler casts `(IUserToken)currentSession` (`:124`). Confirm these hold for the IOCP backend and make the Stream backend produce an `IUserToken` (Task 2 already does via `ChannelInfo.UserToken`/span token). If `TCPServer` supports `UseStream()`, resolve the token from `ChannelInfo` when `obj` is a `ChannelInfo`.

- [ ] **Step 4: Build + suite**

Build the solution Debug → 0 errors. `--all` → 288/288.
Note: migrational consumers (`SAEA.DNS`, `SAEA.Sockets.UdpTest`) are fixed in Task 6; if the Shortcut change alone breaks their compile, land Steps 1–4 and Task 6 in the **same** commit window (the "green after every task" rule is satisfied by committing Tasks 5+6 together).

- [ ] **Step 5: Commit (may be combined with Task 6)**

```powershell
git add Src/SAEA.Sockets/Shortcut/TCPClient.cs Src/SAEA.Sockets/Shortcut/TCPServer.cs Src/SAEA.Sockets/Shortcut/UDPClient.cs Src/SAEA.Sockets/Shortcut/UDPServer.cs
git diff --cached --name-only
git commit -m "feat(sockets): modernize Shortcut adapters to span/memory surface"
```

---

### Task 6: Consumer adaptation (`SAEA.MQTT`, `SAEA.DNS`, `SAEA.Sockets.UdpTest`)

**Files:**
- Modify: `Src/SAEA.MQTT/Implementations/MqttTcpServerListener.cs`, `Src/SAEA.MQTT/Implementations/MqttTcpChannel.cs`
- Modify: `Src/SAEA.DNS/DnsServer.cs`, `Src/SAEA.DNS/Coder/UdpRequestCoder.cs`
- Modify: `Src/SAEA.Sockets.UdpTest/Program.cs`

- [ ] **Step 1: MQTT**

`MqttTcpServerListener` uses `UseStream()` + `OnAccepted` casting `(ChannelInfo)obj` and **reads `ci.Stream` directly** (`:117/140/142/144/164`) — unchanged by Task 2 (payload kept), and it is precisely why Task 2 Step 2 gates the pipe loop on a receive subscriber: with no `OnReceive`/`OnServerReceiveSpan` attached it keeps sole ownership of the stream. The same applies to `SAEA.WebSocket/Core/WSSServerImpl.cs` and `SAEA.Socket5/Server/Socks5Server.cs`. `MqttTcpChannel` casts to `StreamClientSocket` and calls `ConnectAsync` (`:84`) — verify it still compiles against Task 3's send changes. No behavior change.

- [ ] **Step 2: DNS**

`DnsServer.cs` uses `UseIocp<BaseCoder>()` + `OnReceive +=` (`:163`) and `SendAsync(sessionID, response.ToArray())` (`:227/246`) — adapt to the span event (`OnServerReceiveSpan`) and `SendAsync(string, ReadOnlyMemory<byte>)`. `UdpRequestCoder.cs` uses `Shortcut.UDPClient.OnReceive` and `SendAsync(byte[])` (`:92/98/111`) — adapt to the Task 5 Shortcut surface. Preserve request/response behavior.

- [ ] **Step 3: `SAEA.Sockets.UdpTest`**

Migrate the Shortcut UDP `OnReceive`/`SendAsync(string, byte[])` usage (`Program.cs:21-28/44/50/53`) to the Task 5 surface; `ISocketProtocal.Content` is `ReadOnlyMemory<byte>`.

- [ ] **Step 4: Build + suite**

Run: `dotnet build Src/SAEA.Sockets.sln -c Debug` → 0 errors (all 35 entries).
Run: `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug -- --all` → 288/288.

- [ ] **Step 5: Commit (may be combined with Task 5)**

```powershell
git add Src/SAEA.MQTT/Implementations/MqttTcpServerListener.cs Src/SAEA.MQTT/Implementations/MqttTcpChannel.cs Src/SAEA.DNS/DnsServer.cs Src/SAEA.DNS/Coder/UdpRequestCoder.cs Src/SAEA.Sockets.UdpTest/Program.cs
git diff --cached --name-only
git commit -m "fix(consumers): adapt MQTT/DNS/UdpTest to span/memory surface"
```

---

### Task 7: Stream + UDP + Shortcut tests

**Files:**
- Add: `Src/SAEA.P2PTest/Tests/StreamPipelineTest.cs`
- Add: `Src/SAEA.P2PTest/Tests/UdpPipelineTest.cs`
- Modify: `Src/SAEA.P2PTest/Program.cs`

- [ ] **Step 1: Stream server span + PipeReader test**

`StreamPipelineTest.RunAsync()`:
- Start a `StreamServerSocket` on a free port (`SocketOptionBuilder.Instance.UseStream().SetIP("127.0.0.1").SetPort(port)...`).
- Subscribe `OnServerReceiveSpan`; assert the payload is a non-null `IUserToken` (`StreamUserToken` with non-null `Input`).
- Connect a `TcpClient`, write a `BuildFrame(...)` (reuse `StreamDecoderTest.BuildFrame`), `await TestHarness.WaitUntil(() => received != null, 3000)`, assert the delivered bytes equal the frame. Assert `token.ID` matches the `ChannelInfo` id.
- Exercise `server.SendAsync(id, new ReadOnlyMemory<byte>(frame))` and read it back on the client; assert full delivery.
- Dispose server/connection in `finally`.

- [ ] **Step 2: UDP span test**

`UdpPipelineTest.RunAsync()`: bind a `UdpServerSocket`/`UdpClientSocket` on free ports, send a datagram, assert `OnServerReceiveSpan`/`OnClientReceiveSpan` receive the exact bytes and the span path is used. Bound all waits (no unbounded blocking reads).

- [ ] **Step 3: Shortcut smoke (optional if time) / registration**

If a cheap Shortcut smoke is feasible, add it; otherwise register only the two tests above. Add `StreamPipelineTest.RunAsync()` and `UdpPipelineTest.RunAsync()` to `Program.cs` (both in the `--all` path and, if cheap, the menu). Ensure `--all` includes them.

- [ ] **Step 4: Run**

Run: `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug -- --all`
Expected: all pass; count increases by the new assertions over 288.

- [ ] **Step 5: Commit**

```powershell
git add Src/SAEA.P2PTest/Tests/StreamPipelineTest.cs Src/SAEA.P2PTest/Tests/UdpPipelineTest.cs Src/SAEA.P2PTest/Program.cs
git diff --cached --name-only
git commit -m "test(sockets): Stream PipeReader span and UDP span coverage"
```

---

### Task 8: Green gate + plan closeout

- [ ] **Step 1: Full builds**

Run:
```powershell
dotnet build Src/SAEA.Sockets.sln -c Debug
dotnet build Src/SAEA.Sockets.sln -c Release
```
Expected: both `Build succeeded`, 0 errors (35 entries).

- [ ] **Step 2: Full suite**

Run: `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug -- --all` → all pass.

- [ ] **Step 3: Scope check**

Run: `git status --porcelain` and `git diff --name-only <2B-base>..HEAD`. Expected only files under `Src/SAEA.Sockets/`, `Src/SAEA.P2PTest/`, `Src/SAEA.MQTT/`, `Src/SAEA.DNS/`, `Src/SAEA.Sockets.UdpTest/` plus the spec/plan docs. Investigate anything else.

- [ ] **Step 4: Outcome note + commit**

Append a "Plan 2B outcome" section (commit range, pass count, note that Stream now raises span with a real token and `SessionManager` remains `NotImplementedException` by design) to this plan file:

```powershell
git add docs/superpowers/plans/2026-09-21-span-memory-pipeline-stream-udp-shortcut.md
git commit -m "docs(plan): record Plan 2B outcome"
```

---

## Risks / watch-items
- **PipeReader + cancellation/Stop timing.** `reader.ReadAsync` must observe `Stop`/`Dispose` (via `_cancellationToken`/`PipeReader.CancelPendingRead`); otherwise `StreamServerSocket.Dispose` hangs. Verify with the Task 7 test's cleanup, and do not change disconnect ordering (§2.3 #8).
- **Token lifetime.** `StreamUserToken.Clear()` (hidden with `new`, since `BaseUserToken.Clear()` is not `virtual`) cancels/completes `Input` before calling `base.Clear()`, which closes `Socket`. The reader is created with `leaveOpen: true`, so `Complete()` never disposes the shared `NetworkStream`; the stream stays owned by `ChannelInfo.Stream`/the `OnAccepted` consumer. `StreamServerSocket.Stop`/`Dispose` iterate `_tokens` and complete every reader exactly once.
- **`OnReceive` payload type change.** Stream server now raises `OnReceive` with `StreamUserToken` instead of `new Session(id)`. Grep confirmed no in-repo consumer depends on the concrete `Session` type (`(Session)`/`as Session`/`new Session(` absent from `Src`). No Stream-server `OnReceive` subscriber exists at all; IOCP/UDP handlers cast to `IUserToken`, which remains valid.
- **Gated receive loop (MQTT/WSS/Socks5).** The `StreamServerSocket` pipe loop runs only when `OnReceive`/`OnServerReceiveSpan` is subscribed, preserving the old `OnReceive != null` gate. Otherwise `OnAccepted` consumers that read `ci.Stream` directly (`MqttTcpServerListener`, `WSSServerImpl`, `Socks5Server`) would compete with the loop for the same stream. Any future Stream server that wants span delivery **must** subscribe `OnServerReceiveSpan` (or `OnReceive`) at accept time; a subscription added later is not honored, matching prior behavior.
- **Multi-segment delivery semantics.** Existing frame consumers assume a single contiguous block per callback (spec §5.3 "复用 §4 拆帧内核"). Delivering per segment is the approved behavior; consumers that need contiguity must use the `IFrameCoder.DecodeStream`/`DecodedFrames` kernel, not assume one callback == one PDU.
- **Shortcut breaking ripple.** Changing Shortcut events/sends ripples to `SAEA.DNS` and `SAEA.Sockets.UdpTest`; Tasks 5+6 must be committed together if compile is coupled.
- **ns2.0 write boundaries.** `Stream.Write(ReadOnlySpan)`/`WriteAsync(ReadOnlyMemory)`/`Socket.SendTo(Span)` do not exist in netstandard2.0; each boundary copy must be intentional, documented, and not expanded to the receive path.
- **`SessionManager` escalation.** Decision 3 keeps it throwing. If the user wants a real Stream session manager, stop before Task 1 — it materially expands scope and risks §2.3 #8.
- **No comments policy** applies to all touched files; XML docs only.
- **Staging discipline.** Never `git add -A`; stage explicit paths and verify.