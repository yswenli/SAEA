# Plan 2C — P2P/relay span-ification, byte[] surface removal, docs, release

> **For agentic workers:** Execute task-by-task with the Subagent-Driven workflow. Each task must end with a green build and a commit. Never `git add -A`; stage explicit paths and verify with `git diff --cached --name-only`.

**Goal:** Finish the SAEA socket span/memory modernization: remove the legacy `byte[]` public surface, migrate every in-repo consumer to the span/Memory surface, span-ify the P2P/relay decode path, update docs, and publish a release (version bump + DoD evidence).

**Scope:** `Src/SAEA.Sockets`, `Src/SAEA.P2P`, and every project-referenced consumer lib, plus test projects and docs. Out of scope: `SAEA.Socket5` (uses `System.Net.Sockets.UdpClient`), `SAEA.Audio.Net` (consumes the published package, not a project reference), and the generated `Src/packages/SAEA.Sockets.26.4.23.1/**`.

**Base:** `ad5e578c` (Plan 2B outcome). Current suite: `--all` → 301/301.

---

## Design decisions (resolved)

- **A. Orphan throwing `ReceiveAsync(byte[])`.** Remove the `IClientSocket.ReceiveAsync(byte[],int,int,CT)` interface member only. Keep the concrete `IocpClientSocket.ReceiveAsync` (`KernelException`) and `UdpClientSocket.ReceiveAsync` (`NotSupportedException`) implementations as-is (spec §5.1). Raw reads move to the retained `GetStream()`.
- **B. Shortcut byte[] convenience overloads.** Remove `TCPClient`/`TCPServer`/`UDPClient`/`UDPServer` `byte[]` send overloads in the removal task; route callers to the Memory/Span overloads (Q8: remove the byte[] public surface).
- **C. `WSCoder`/`WSUserToken` internal `List<byte>`.** Leave unchanged (spec §4.6 preserves behavior; not on the `DecodeStream` path).
- **D. Shortcut `OnReceive` breaking signature.** Published via the version bump plus a migration table; no compatibility shim.
- **E. DoD number.** Authoritative evidence is the fresh `TestHarness.WriteSummary` output from `dotnet run … -- --all` after all changes. The xUnit `SAEA.Common.Tests` count is reported separately, not folded in.
- **Minor.** Leave `SAEA.Socket5` at `26.9.20.1`; leave the stale `Src/packages/**` README; leave `SAEA.Audio.Net` untouched.

---

## Task 1: `SAEA.Sockets` internal modernization (additive, non-breaking)

**Files:**
- Modify: `Src/SAEA.Sockets/Core/SocketStream.cs`
- Modify: `Src/SAEA.Sockets/Core/Udp/UdpServerSocket.cs`
- Delete: `Src/SAEA.Sockets/Core/RioExtention.cs`

- [ ] **Step 1: `SocketStream` span subscription.** Replace `_client.OnReceive += _client_OnReceive` (`:67`) with `_client.OnClientReceiveSpan += _client_OnReceiveSpan`; the handler (`:72`) does a one-time `data.ToArray()` into the existing `BlockingQueue<byte[]>` (span is callback-scoped). Change the write at `:142` from `_client.SendAsync(data)` to `_client.SendAsync(new ReadOnlyMemory<byte>(data))`. Keep the public `Stream` `Read`/`Write` contract (used by `SAEA.Sockets.TcpTest/JClient2.cs:57,72`).
- [ ] **Step 2: `UdpServerSocket.Stop()` teardown stall.** Replace the blocking `_udpSocket.Close(10 * 1000)` (`:767`) with a fast close that cancels the pending `ReceiveFromAsync` (dispose the socket; keep `_sessionManager.Clear()` and the null-out ordering). Verify teardown no longer takes ~10s.
- [ ] **Step 3: Delete `RioExtention.cs`** (self-referenced only; spec §6).
- [ ] **Step 4: Verify + commit.**
  - `dotnet build Src/SAEA.Sockets.sln -c Debug` → 0 errors.
  - `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug -- --all` → 301/301 (unchanged).
  - Commit `refactor(sockets): span SocketStream, fast UDP stop, drop RioExtention`.

## Task 2: Migrate project-referenced consumer libs to span/Memory (additive)

**Files:** one or more call sites per project, all still compiling against the retained byte[] surface until Task 5:
- `Src/SAEA.FileSocket/{Server.cs:85,Client.cs:88}`
- `Src/SAEA.FTP/{Net/ServerSocket.cs:75,Net/ClientSocket.cs:81,253,Core/FTPDataSocketManager.cs:68}`
- `Src/SAEA.WebSocket/{WSClient.cs:108,Core/WSServerImpl.cs:97}`
- `Src/SAEA.Http/{Base/Net/HttpSocketDebug.cs:70,Base/Net/HttpSocket.cs:87}`
- `Src/SAEA.MessageSocket/{MessageClient.cs:107,MessageServer.cs:96}`
- `Src/SAEA.RPC/{Net/RServer.cs:78,Net/RClient.cs:102}`
- `Src/SAEA.QueueSocket/{QServer.cs:87,QClient.cs:128}`
- `Src/SAEA.RedisSocket/Base/Net/RClient.cs` (verify it still builds after base-class changes)

- [ ] **Step 1:** For each `OnReceive +=` (byte[]), switch to `OnClientReceiveSpan`/`OnServerReceiveSpan`, copying the span synchronously (`span.ToArray()`) before any async use.
- [ ] **Step 2:** Replace byte[] send calls (`Send(id, byte[])`, `SendAsync(byte[])`, `End(id, byte[])`) with the Memory overloads.
- [ ] **Step 3: Verify + commit.** Build the full solution Debug → 0 errors; `--all` → 301/301. Commit `refactor(consumers): migrate project-referenced libs to span/memory`.

## Task 3: P2P/relay span-ification

**Files:**
- `Src/SAEA.P2P/Core/P2PServer.cs`, `Core/P2PClient.cs`, `Relay/RelayManager.cs`, `Discovery/LocalDiscovery.cs`, `NAT/HolePuncher.cs`, `Channel/{TCPChannel,UDPChannel}.cs`, `Protocol/P2PCoder.cs`
- `Src/SAEA.P2PTest` (any affected assertion)

- [ ] **Step 1: Decode path.** Convert `ProcessMessage(string, ISocketProtocal)` (`P2PServer.cs:137`) and `ProcessSignalMessage(ISocketProtocal)` (`P2PClient.cs:195`) to operate on `ISocketProtocal` without materializing `Content.ToArray()`; use `Content.Span`/`DecodedFrames` slices. Replace `Array.IndexOf` (`P2PServer.cs:360,362`, `P2PClient.cs:372,374,376,393,398`, `RelayManager.cs:140,142,144`) with `Span.IndexOf`. Remove `new byte[]`+`BlockCopy` staging where a span slice suffices (`P2PServer.cs:331-332,379-382`, `P2PClient.cs:381,383,451-453`, `RelayManager.cs:123-125,152,154`).
- [ ] **Step 2: Channel/discovery.** Replace `frame.Content.ToArray()` (`UDPChannel.cs:110`, `TCPChannel.cs:104`, `LocalDiscovery.cs:150,154`, `HolePuncher.cs:110`) with span-based handling; keep the 7 `DecodeP2P` call sites compiling.
- [ ] **Step 3: Keep encode-side allocations** (`P2PCoder.EncodeP2P` `new byte[]`, encode merges) — explicit non-goal.
- [ ] **Step 4: Verify + commit.** Build Debug → 0 errors; `--all` → all pass; run the P2P/relay suites specifically. Commit `perf(p2p): span-based decode path for P2P and relay`.

## Task 4: Migrate test projects + benchmark

**Files:**
- `Src/SAEA.Sockets.UdpTest/Program.cs:22,27`
- `Src/SAEA.Sockets.TcpTest/{Program.cs:22,27,JServer.cs:40,JClient.cs:44}`
- `Src/SAEA.P2PTest/Tests/{StreamDecoderTest.cs:280,IocpBenchmark.cs:116}`
- `Src/SAEA.RedisSocketTest/RedisStreamTest.cs:79`

- [ ] **Step 1:** Migrate `OnReceive`/byte[] send usages to the span/Memory surface.
- [ ] **Step 2:** Fix `IocpBenchmark.cs:116` — it uses `client.UserToken.Socket`, and `IocpClientSocket.UserToken` is removed in Task 5; rewrite the readiness assertion to `client.Connected` (or another retained signal).
- [ ] **Step 3: Verify + commit.** Build Debug → 0 errors; `--all` → all pass. Commit `test(consumers): migrate test projects and benchmark to span/memory`.

## Task 5: Remove the byte[] public surface

**Files:**
- `Src/SAEA.Sockets/IClientSocket.cs`, `IServerSocket.cs`
- `Src/SAEA.Sockets/Handler/{OnReceiveHandler.cs,OnClientReceiveHandler.cs,OnClientReceiveBytesHandler.cs,OnServerReceiveBytesHandler.cs}` (delete the byte[] delegates if now unreferenced)
- `Src/SAEA.Sockets/Core/Tcp/{IocpClientSocket,IocpServerSocket,StreamClientSocket,StreamServerSocket}.cs`
- `Src/SAEA.Sockets/Core/Udp/{UdpClientSocket,UdpServerSocket}.cs`
- `Src/SAEA.Sockets/Shortcut/{TCPClient,TCPServer,UDPClient,UDPServer}.cs`

- [ ] **Step 1: Interfaces.** Delete `IClientSocket` `OnReceive` (`:71`), `BeginSend(byte[])` (`:114`), `Send(byte[])` (`:120`), `SendAsync(byte[])` (`:126`), `SendAsync(byte[],int,int,CT)` (`:162`), `ReceiveAsync(byte[],int,int,CT)` (`:172`); delete `IServerSocket` `OnReceive` (`:64`), `SendAsync(string,byte[])` (`:104`), `Send(string,byte[])` (`:146`), `End(string,byte[])` (`:153`), `SendAsync(IPEndPoint,byte[])` (`:160`). Keep `GetStream()` and all span/Memory members.
- [ ] **Step 2: Implementers.** Delete the byte[] members/events and the `_isBaseClientType`/`_isBaseServerType` fields and `.ToArray()` compat branches; keep the span-only invoke path. Delete `IocpClientSocket.UserToken` (`:108`) (Task 4 already fixed the only consumer). Keep `BeginSend` removal contained to `SAEA.Sockets` (no external callers). Keep the orphan `ReceiveAsync(byte[])` impls per decision A.
- [ ] **Step 3: Shortcut.** Remove the byte[] convenience send overloads (decision B); all callers were migrated in Tasks 2–4.
- [ ] **Step 4: Verify + commit.** Build the full solution Debug → 0 errors; `--all` → all pass; build `SAEA.MQTT`, `SAEA.DNS`, `SAEA.WebSocket`, `SAEA.Socket5`, `SAEA.RedisSocket`, `SAEA.P2P`. Commit `refactor(sockets)!: remove legacy byte[] public surface`.

## Task 6: Docs + migration table

**Files:**
- `README.md:41,273,276`, `README.en.md:41,273,276`
- `Src/SAEA.Sockets/README.md` (`:49,57,59,148,157,329,358,368,384,418,423`) and `README.en.md` (mirror)
- `Src/SAEA.MessageSocket/README.md:250`, `.en.md:250`

- [ ] **Step 1:** Update the stale `OnReceive`/`SendAsync(byte[])` examples to `OnServerReceiveSpan`/`SendAsync(ReadOnlyMemory<byte>)`.
- [ ] **Step 2:** Add a short "Migrating from byte[]" table mapping removed members → replacements, and note the Shortcut `OnReceive` signature break.
- [ ] **Step 3: Verify + commit.** `git diff` review only (no build needed). Commit `docs: update socket API examples and add byte[] migration table`.

## Task 7: Version bump

**Files (14 libs → `26.9.21.1`):** `SAEA.Sockets:5`, `SAEA.Common:5`, `SAEA.P2P:16`, `SAEA.DNS:15`, `SAEA.Http:6`, `SAEA.FTP:5`, `SAEA.MessageSocket:6`, `SAEA.MQTT:8`, `SAEA.WebSocket:7`, `SAEA.RPC:7`, `SAEA.QueueSocket:9`, `SAEA.MVC:15`, `SAEA.RedisSocket:14`, `SAEA.FileSocket:7`. Leave `SAEA.Socket5` (out of scope) and test projects.

- [ ] **Step 1:** Bump the 14 `<Version>` values to `26.9.21.1`.
- [ ] **Step 2: Verify + commit.** `dotnet build Src/SAEA.Sockets.sln -c Release` → 0 errors. Commit `chore(release): bump libraries to 26.9.21.1`.

## Task 8: Green gate + Plan 2C outcome

- [ ] **Step 1:** `dotnet build Src/SAEA.Sockets.sln -c Debug` and `-c Release` → 0 errors.
- [ ] **Step 2:** `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug -- --all` → all pass; record the count (DoD number). Optionally run 3× for flakiness.
- [ ] **Step 3:** `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug -- --bench-iocp` → allocation assertions pass; record client/server B/frame.
- [ ] **Step 4:** `git status --porcelain` clean; `git diff --name-only <2C-base>..HEAD` confined to `Src/` libs + tests + docs.
- [ ] **Step 5:** Append a "Plan 2C outcome" section (commit range, pass count, benchmark numbers, version) to this plan file and commit `docs(plan): record Plan 2C outcome`.

---

## Risks / watch-items

- **One-shot breaking removal.** Task 5 only stays green if every project-referenced consumer was migrated in Tasks 1–4. Re-run a full-solution Debug build before committing Task 5; any missed call site surfaces there.
- **Span lifetime.** Every migrated `OnReceive` handler must copy the span synchronously before async use; a stored span is use-after-free.
- **P2P decode correctness.** `Span.IndexOf`/slice conversions must preserve the exact wire parsing; the P2P/relay suites plus `--all` are the guard.
- **`SAEA.Audio.Net`/`SAEA.Socket5`.** Not project-referenced to the changed surface; they must remain untouched and still build from their package.
- **`IocpClientSocket.UserToken` removal** must be preceded by the Task 4 benchmark fix.
- **UDP `Stop` change** alters shutdown timing; verify no pending-receive exception storms and that sends in flight during `Stop` are not corrupted.
- **No comments policy.** XML docs only; no inline `//`.
- **Staging discipline.** Explicit paths only; verify the staged set each commit.
