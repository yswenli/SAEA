# SAEA.Sockets.Pipeline - Bidirectional Channel Pipeline with Pluggable Codecs

<p align="center">
  <img src="https://img.shields.io/badge/Version-26.11.5.1-blue?style=for-the-badge" alt="Version">
  <img src="https://img.shields.io/badge/NuGet-SAEA.Sockets.Pipeline-green?style=for-the-badge" alt="NuGet">
  <img src="https://img.shields.io/badge/.NET-Standard%202.0-purple?style=for-the-badge" alt=".NET">
  <img src="https://img.shields.io/badge/License-Apache%202-orange?style=for-the-badge" alt="License">
</p>

<p align="center">
  <b>🚀 One handler chain per connection: framing, codecs, authentication and business logic each in their own place!</b>
</p>

<p align="center">
  <b>中文</b> | <a href="README.md">English</a>
</p>

---

> **🎯 The problem:** Writing business logic directly in socket callbacks quickly tangles partial frames, sticky packets, authentication, timeouts and backpressure into one knot — and every protocol change touches the receive loop.
>
> **The SAEA.Sockets.Pipeline approach:** One bidirectional chain per connection. The receive pump owns the `PipeReader` and handles framing and backpressure; each handler does one job; replies travel back along the same chain and ownership is handed to the transport at the head.

---

## ✨ Why Choose SAEA.Sockets.Pipeline?

| Concern | Hand-written receive loop | SAEA.Sockets.Pipeline |
|---------|---------------------------|-----------------------|
| **Partial / sticky packets** | Maintain buffers and cursors yourself | Pump plus pluggable splitter, unified through `TrySplit` |
| **Auth / timeout / backpressure** | Interleaved with business code | Dedicated handlers, per-message `CancellationToken` |
| **Encoding** | Protocol changes touch the receive loop | Two-level codec: frame → `Envelope` → entity |
| **Outbound** | Every call site touches the socket | Reverse traversal of the same chain, single hand-off at the head |
| **Connection isolation** | State leaks between connections easily | Chain, queue and executor are all per connection |

---

## 🚀 30-Second Integration

The following matches `Src/SAEA.Sockets.PipelineTest/Program.cs`: `PipelineHost` and `SocketStreamAdapter` live in that demo project and can be copied as-is; `BuildPipeline(options)` is the standard assembly from the next section.

```csharp
var executors = new ConcurrentDictionary<string, ChannelPipelineExecutor>();
var host = new PipelineHost();                                    // IPipelineHost implementation from the demo project
var options = new ChannelPipelineOptions { Splitter = LengthFieldSplitter.Default(64 * 1024) };
SaeaServerSocket server = null;

var option = new SocketOptionBuilder()
    .UseStream()                                                  // hooks require the PipeReader provided by UseStream
    .SetPort(39654)
    .UseConnectionOpened(info =>
    {
        var channel = new DefaultChannel(info.ID, reason => executors.TryGetValue(info.ID, out var running) ? running.BeginShutdown(reason) : Task.CompletedTask);
        channel.SetAttribute(TokenAuthHandler.TokenAttributeKey, "seed-token");
        host.Set(channel);
    })
    .UseInputHandler(async (userToken, input, flow, token) =>
    {
        var stream = new SocketStreamAdapter(userToken.ID, input, flow, server);
        var executor = new ChannelPipelineExecutor(stream, host.Get(userToken.ID), options, token, host);
        executor.Attach(BuildPipeline(options));                  // attach the chain before starting the pump
        executors[userToken.ID] = executor;
        executor.Start();
        try { await executor.Completion; }
        finally { executors.TryRemove(userToken.ID, out _); }
    })
    .Build();

server = new SaeaServerSocket(option);
server.Start();
```

---

## ⚠️ Breaking Changes (P0)

The following 11 changes affect both `SAEA.Sockets` and `SAEA.Sockets.Pipeline`; check every call site before upgrading:

1. **Four Pipeline interfaces gain six new members**: `IChannelHandler` adds `HandlerAdded` / `HandlerRemoved`, `IChannelInboundHandler` adds `ChannelActiveAsync` / `ChannelInactiveAsync`, `IChannelHandlerContext` adds `CloseAfterWriteAsync`, and `IChannelPipeline` adds `WriteAsync`. Code that implements these interfaces directly fails to compile; inheriting `ChannelHandlerAdapter` / `InboundHandlerAdapter` / `OutboundHandlerAdapter` is unaffected. `netstandard2.0` with C# 8 has no default interface implementations, so there is no way to hide this with defaults.
2. **`ctx.WriteAsync` is now legal only inside an outbound callback**: it is a chain-forwarding call and must be used inside `IChannelOutboundHandler.WriteAsync`. Calling it from business handlers, connection lifecycle callbacks, or async continuations throws `InvalidOperationException` synchronously. Use `ctx.Pipeline.WriteAsync(entity)` for business sends.
3. **Inbound ordinary exceptions are notified once and then rethrown**: they are no longer swallowed. `ChannelExceptionCaughtAsync` is notified once, then the exception keeps propagating to the chain tail where `ExceptionTerminalHandler` records it and decides whether to disconnect. Cancellation is not notified but still propagates.
4. **The chain freezes after `Start()`**: the fixed order is `Create → Attach → assemble → Start`; after `Start()` succeeds, `AddLast` / `AddFirst` / `Remove` all throw `InvalidOperationException`. Do not use the receive pump when handlers must be added or removed at runtime.
5. **The two `owner`-carrying send overloads in `SAEA.Sockets` change from `void` to `Task`**: `IServerSocket.SendAsync(string, ReadOnlyMemory<byte>, IDisposable)` and `IClientSocket.SendAsync(ReadOnlyMemory<byte>, IDisposable)` now return `Task`, and all four implementations change together: `SaeaServerSocket` / `SaeaClientSocket` / `SaeaUdpServerSocket` / `SaeaUdpClientSocket`. Existing calls still compile without `await`, but async failures become unobservable; external interface implementations must change their signatures.
6. **`owner` release contract**: released exactly once on success and on every path whose exit can be confirmed; a missing session or synchronous exception is released by the transport; async failure/cancellation is released after the socket operation is confirmed to have exited; a send-timeout path that cannot prove the kernel has exited may abandon the buffer instead of reclaiming it. The adapter only translates exceptions and never disposes `owner` a second time.
7. **`ByteToMessageDecoder` does not accumulate partial packets**: its input must be a complete `PooledMessage` cut by the pump; partial bytes stay in the `Pipe` / accumulator. `FrameSplitterHandler` is only for pure chaining without the executor pump; using both splits the same bytes twice.
8. **`RawFrame` and the envelope model are mutually exclusive**: the `FramePassthroughHandler` raw-frame bypass and the `Envelope*` / `Codec*` envelope chain cannot live on the same chain; pick one.
9. **`CloseAfterWriteAsync` only guarantees submission to the transport**: a successful return means submitted writes completed their handoff, not that the peer received them; `DrainTimeout <= 0` is treated as 5 seconds at this entry, and a timeout forces shutdown while compensating un-handed-off `RawFrame` instances.
10. **Only two memory quantities are bounded**: `MaxBufferedBytes` bounds per-connection un-framed input leftovers, and `MaxPendingWrites` bounds the outbound pending-write item count. Queued frames, in-flight frames, decoded business messages, TLS encryption copies, socket send buffers, and pool retention are not covered by a fixed total upper bound.
11. **Assembly positions and limits**: exactly one `OutboundSerializingHandler` must sit at the rightmost end of the outbound prefix (immediately next to the first inbound node) so writes are queued before encoding; `SwitchableSplitter` must be one instance per connection, `SwitchTo` is the only way to release the `StopAfterCurrent` barrier, its `MaxFrameLength` is fixed at construction to the maximum of every switchable protocol and a switch target may not exceed it, and `SplitterSwitchTimeout` defaults to 10 seconds before failing with `FrameProtocolException`; `TakeAvailableSplitter` treats one read as one message boundary, its `MaxFrameLength` is the hard per-frame upper bound (buffered bytes beyond it are split at the limit instead of throwing an exception), and it does not exempt `ChannelPipeline.Create` from the buffer lower-bound check.

---

## 🧱 Architecture

```
Inbound (head → tail):
  Pipe → receive pump (framing + backpressure) → PooledMessage → CodecDecoderHandler → AuthHandler
       → EnvelopeUnwrapHandler → TokenAuthHandler → InboundLoggingHandler
       → MessageDispatcher → ExceptionTerminalHandler

Outbound (tail → head):
  business IChannelPipeline.WriteAsync(entity) → OutboundSerializingHandler → EnvelopeWrapHandler
       → CodecEncoderHandler → FrameEncoderHandler → chain head
       → IRawByteStream.SendAsync(frame, owner) → transport
```

---

## 📐 Wire Format Invariants

The default framing configuration (`LengthFieldSplitter.Default` + `LengthFieldFrameEncoder`) always uses this layout:

```
 0        1        2        3        4        5        6        7        8        9 ...
 +--------+--------+--------+--------+--------+--------+--------+--------+--------+----------------+
 |                    body length (8 bytes, little-endian int64)           |  type  |     body       |
 +--------+--------+--------+--------+--------+--------+--------+--------+--------+----------------+
 |<-------------------------- 9-byte header P_Head ----------------------->|<--- body length --->|

 The length field is the number of body bytes (the 9-byte header is NOT included); it is not the total frame length.
 Total frame length = 9 + body length. An empty body (length 0) is valid.
 type occupies 1 byte, range 0..255, mapped to a CLR type by TypeRegistry.
```

- `FrameConstants.P_LEN = 8`, `FrameConstants.P_Type = 1`, `FrameConstants.P_Head = 9`.
- `LengthFieldSplitter.Default` strips only the 8-byte length field, so `PooledMessage.Frame` has the layout `[1B type][body]`; `EnvelopeCodec` then removes the type byte and produces an independent `Envelope.Payload`.
- `LengthFieldFrameEncoder.Encode` writes `body.Length` into the length field, and the type code must be within `0..255`.

---

## 🧩 Standard Assembly Order

```csharp
var pipeline = ChannelPipeline.Create(options);
var codec = new EnvelopeCodec();

// Outbound handlers must form a contiguous prefix at the head. AddLast appends at the tail,
// so add the handler closest to the transport first (FrameEncoderHandler), then
// CodecEncoderHandler and EnvelopeWrapHandler, and finally OutboundSerializingHandler
// immediately next to the first inbound node.
// Outbound traversal is tail → head, so the actual order is:
// OutboundSerializingHandler → EnvelopeWrapHandler → CodecEncoderHandler
// → FrameEncoderHandler → transport.
pipeline.AddLast(new FrameEncoderHandler(new LengthFieldFrameEncoder()));
pipeline.AddLast(new CodecEncoderHandler(codec));
pipeline.AddLast(new EnvelopeWrapHandler(registry, new JsonMessageCodec()));

// Exactly one serializer, at the rightmost end of the outbound prefix:
// queue before encoding so an async encoder cannot reorder writes.
pipeline.AddLast(new OutboundSerializingHandler());

// Inbound: the pump already performs framing, so no FrameSplitterHandler is needed here.
pipeline.AddLast(new CodecDecoderHandler(codec));
pipeline.AddLast(new AuthHandler(new[] { 1, 3 }));                 // type codes allowed before authentication
pipeline.AddLast(new EnvelopeUnwrapHandler(registry, new JsonMessageCodec()));
pipeline.AddLast(new TokenAuthHandler());
pipeline.AddLast(new InboundLoggingHandler());
pipeline.AddLast(new MessageDispatcher()
    .Register<LoginRequest>(loginHandler)
    .Register<Heartbeat>(heartbeatHandler));
pipeline.AddLast(new ExceptionTerminalHandler());
```

- Outbound handlers must form a contiguous prefix at the head and cannot sit behind inbound handlers. `AddLast` validates this: once an inbound handler is at the head, appending another outbound handler throws `InvalidOperationException`; use `AddFirst` instead.
- `AddFirst` enforces the symmetric rule: an inbound handler cannot be inserted before an outbound handler already at the head.
- A handler implementing both inbound and outbound interfaces is rejected; a non-sharable instance cannot be attached to more than one chain (`IChannelHandler.IsSharable`).
- With `RequireTerminalHandler = true`, `Start()` validates that the last inbound node is an `ExceptionTerminalHandler` and that exactly one `OutboundSerializingHandler` sits at the rightmost end of the outbound prefix; otherwise startup fails and shutdown begins immediately.

---

## 🔌 Connecting a Transport: the IRawByteStream Adapter

The core package does not reference `SAEA.Sockets`, so the consumer adapts `IServerSocket` (server) or `SaeaClientSocket` (client) to `IRawByteStream`. The complete implementation below can be copied directly (it matches `Src/SAEA.Sockets.PipelineTest/SocketStreamAdapter.cs`):

```csharp
using System;
using System.IO.Pipelines;
using System.Threading.Tasks;
using SAEA.Sockets;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Pipeline;

public sealed class SocketStreamAdapter : IRawByteStream
{
    private readonly string _channelId;
    private readonly PipeReader _input;
    private readonly IReceiveFlowControl _flow;
    private readonly Func<ReadOnlyMemory<byte>, IDisposable, Task> _send;

    // Server: wrap IServerSocket into a send delegate.
    public SocketStreamAdapter(string channelId, PipeReader input, IReceiveFlowControl flow, IServerSocket server)
        : this(channelId, input, flow, CreateServerSend(channelId, server))
    {
    }

    // Client: pass client.SendAsync(data, owner) as the send delegate.
    public SocketStreamAdapter(
        string channelId,
        PipeReader input,
        IReceiveFlowControl flow,
        Func<ReadOnlyMemory<byte>, IDisposable, Task> send)
    {
        if (string.IsNullOrEmpty(channelId))
            throw new ArgumentException("Channel id must not be empty.", nameof(channelId));
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        if (flow == null)
            throw new ArgumentNullException(nameof(flow));
        if (send == null)
            throw new ArgumentNullException(nameof(send));

        _channelId = channelId;
        _input = input;
        _flow = flow;
        _send = send;
    }

    private static Func<ReadOnlyMemory<byte>, IDisposable, Task> CreateServerSend(
        string channelId,
        IServerSocket server)
    {
        if (server == null)
            throw new ArgumentNullException(nameof(server));

        return (data, owner) => server.SendAsync(channelId, data, owner);
    }

    public string ChannelId
    {
        get { return _channelId; }
    }

    public PipeReader Input
    {
        get { return _input; }
    }

    public async Task SendAsync(ReadOnlyMemory<byte> data, IDisposable owner)
    {
        Task sending;
        try
        {
            // Pass owner straight through: the transport releases it when sending completes or fails.
            // The adapter must never dispose it before returning.
            sending = _send(data, owner);
        }
        catch (ChannelClosedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ChannelClosedException("Connection closed or send failed:" + _channelId + "," + ex.Message, ex);
        }

        if (sending == null)
            return;

        try
        {
            // Actually await the transport handoff: the Task means ownership was handed off,
            // not that the peer has received the bytes.
            await sending.ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ChannelClosedException("Connection closed or send failed:" + _channelId + "," + ex.Message, ex);
        }
    }

    public void Pause()
    {
        _flow.Pause();
    }

    public void Resume()
    {
        _flow.Resume();
    }

    public Task CloseAsync(string reason)
    {
        return _flow.CloseAsync(reason);
    }
}
```

- `SendAsync` must actually `await` the send delegate. The returned Task means only that ownership of the frame has been handed to the transport — not that the peer received it. Synchronous exceptions and asynchronous faults are both translated to `ChannelClosedException`; the transport releases `owner`, and the adapter never disposes it a second time.
- When the connection is closed, `SendAsync` must throw `ChannelClosedException` immediately instead of queueing or silently succeeding; other transport exceptions are translated to `ChannelClosedException` too.
- Both server and client support integration; both sides share the same `UseInputHandler` / `UseConnectionOpened` hooks and the same configuration validation. On the client, wire it inside `UseInputHandler`:

  ```csharp
  var adapter = new SocketStreamAdapter(userToken.ID, input, flow, (data, owner) =>
      // The client send overload returns a Task; the adapter awaits it, and the transport
      // releases owner after the underlying handoff completes or fails.
      client.SendAsync(data, owner));
  ```

---

## 🔐 Message Ownership

- A `PooledMessage` starts with reference count 1 and its ownership belongs to the chain. The execution wrapper for every message calls `Release()` once in a `finally`, so the count returns to zero no matter where an exception is thrown; `Release()` is a no-op on an already released instance.
- An inbound handler must not use a message after passing it downstream. To keep it across an `await` or a callback boundary, call `Retain()` first and `Release()` that reference when your handler's lifetime ends. The executor releases the queue-entry reference; the two are not interchangeable.
- `CodecDecoderHandler` releases the queue entry as soon as decoding succeeds. If decoding throws, it does not release there; the execution wrapper's `finally` handles it.
- The backing store of `PooledMessage.Frame` depends on length: a frame with `length > 0` and `>= SmallFrameInlineThreshold` rents from `MemoryPoolManager` and sets `IsPooled == true`; shorter frames (and zero-length frames, since `Rent(0)` is rounded up to the smallest pool bucket) use an exact `new byte[len]` with `IsPooled == false` and are left to the GC. The threshold defaults to **0**, so every non-empty frame is pooled: `ArrayPool.Shared` hands out power-of-two buckets (a 257B frame occupies a 512B bucket), which can double in-flight memory in exchange for cutting per-frame allocation to roughly one third. Accessing `Frame` after release throws `ObjectDisposedException`.
- `Envelope.Payload` is always an independent `new byte[len]` and never points at pooled backing storage, so it carries no return obligation. `EnvelopeCopyThreshold` (4KB by default) only controls a `Warn` log line; it does not change how copying works.
- For raw frame bytes (packet capture, auditing, binary passthrough), use `FramePassthroughHandler`: it calls `Retain()` and forwards the `PooledMessage` unchanged, and the branch's own tail handler releases that reference. This branch is mutually exclusive with the codec branch and cannot share a chain with it.
- Outbound intermediates `EncodedBody` / `TransportFrame` stay inside the outbound chain. When `await ctx.WriteAsync(...)` returns normally, ownership has moved to the chain head/transport; if it throws, the creator disposes them.
- All business writes go through `IChannelPipeline.WriteAsync`: each write claims one operation and one pending-write slot, starting from the rightmost outbound node. `ctx.WriteAsync` only forwards along the same operation from inside an outbound callback; it never increments the count or consumes another slot.
- A `RawFrame` is compensated by the outbound boundary until the chain head CAS accepts it for handoff; after a successful handoff only the transport releases it. The outbound boundary does not take ownership of ordinary business `IDisposable` objects.

---

## ⏱️ Lifecycle and Close Semantics

- `IChannelPipeline.WriteAsync` before `Attach` is invalid: the head cannot find an executor or byte stream and throws `InvalidOperationException("pipeline 尚未 Attach 到传输层。")`. `ctx.Channel` is unavailable for the same reason.
- `ChannelPipelineExecutor.Attach(pipeline)` must complete before `Start()`; otherwise `Start()` throws `InvalidOperationException`.
- `CloseAsync(reason)` only means "shutdown has begun": it returns immediately while shutdown continues in the background. Wait on `ChannelPipelineExecutor.Completion` to know that resources have been reclaimed.
- The close state machine is `Open → DrainingWrites → Closing`. `CloseAfterWriteAsync` stops accepting new writes first, then waits in the same synchronization boundary for submitted writes to reach zero; a zero `DrainTimeout` means 5 seconds. On timeout only writes still in `Queued` are force-completed and their `RawFrame` compensated; handed-off writes are never faked as complete or released. A plain `CloseAsync` during draining joins the same wait, and repeated calls reuse the same completion.
- After shutdown begins, `IChannelPipeline.WriteAsync` throws `ChannelClosedException`; `IRawByteStream.SendAsync` must throw the same exception once the connection is closed. This check precedes message-type validation: an old `ctx` from an already-removed node still reports closed even if the message is not a `TransportFrame`.
- Once `UseInputHandler` is configured, the pump owns that connection's `PipeReader` and the runtime marks inbound data as claimed: later calls to `GetStream()`, `ReceiveAsync()` or `ChannelInfo.Stream.ReadAsync` throw `InvalidOperationException` immediately (the message names the owner and the supported alternative), so two readers can no longer steal bytes from each other. Outbound `Stream.WriteAsync` is unaffected.
- Once the hooks are enabled, the event pump is not started for that connection: neither server `OnServerReceiveSpan` nor client `OnClientReceiveSpan` fires.
- `UseInputHandler` must be paired with `UseConnectionOpened` and requires `.UseStream()`. `SocketOptionBuilder.Build()` validates this and throws `InvalidOperationException`.
- `UseConnectionOpened` runs synchronously: on the server inside `RaiseAccepted`, on the client before the connect callback. Keep it to lightweight registration (create the `IChannel`, write initial attributes). Blocking I/O and `await` are forbidden.

---

## 💰 Known Costs

- Inbound costs exactly **1 copy per frame** on the executor pump path: frames are cut directly out of the `Pipe` buffer and copied once into a `PooledMessage` (`AllocateFrame`). `AdvanceTo` only advances to the end of the last complete frame, so un-framed leftover bytes stay in the `Pipe` for the next `ReadAsync`; the queue entry survives `await`s, so this one copy cannot be skipped and its backing store must not point at `Pipe` memory.
- Pure chaining (no executor, framing done by `FrameSplitterHandler` at the chain head) still costs **2 copies**: ① `Pipe` → `FrameAccumulator`, ② accumulator → standalone `PooledMessage`.
- Outbound costs exactly **3 copies**: ① entity → `Envelope.Payload` (the `byte[]` produced by the codec), ② `Payload` → `EncodedBody` (codec writer), ③ `EncodedBody` → final frame buffer (frame writer). The transport takes over that same buffer under `owner` semantics and the pipeline copies it no further; an adapter that wants to retain bytes pays for that copy itself.
- Allocation profile: with the default `SmallFrameInlineThreshold = 0`, an inbound frame adds only a 56B `PooledMessage` plus a queue node, and the frame bytes are reused from the pool. A positive threshold makes shorter frames use exact allocation instead, trading in-flight memory for an extra `byte[len]` per frame.
- Measured profile: `PipelineTest --bench` (265B frames) reports 191B/frame for inline dispatch and 191–244B/frame for the queued pump. Setting the threshold back to 512 raises this to 477–480B/frame, of which the pipeline itself accounts for about 352B (`byte[265]` at 296B plus a 56B `PooledMessage`); the rest comes from segment allocations while the benchmark harness feeds 64KB chunks into the `Pipe`.

---

## 🧵 Message Execution Model

- One consumer coroutine per connection: after dequeuing a message and acquiring a concurrency slot it **starts the execution chain inline** — no `await`, no extra thread-pool dispatch. A message that completes synchronously just returns to the consumer loop for the next frame.
- `DispatchMode` defaults to `Queued` (the path above). With `DispatchMode = Inline`, the receive pump executes a frame **on the receive thread** whenever the queue is empty and no message is in flight; as soon as one message suspends on `await`, later frames fall back to queued dispatch and inline resumes once the in-flight count returns to zero. `MessageTimeout != 0` always forces `Queued`, because inline would leak the per-message token into the receive thread's async flow.
- In the benchmark, `Inline` runs about 1.8× faster than `Queued` by skipping the per-frame queue round-trip and thread wake-up. The cost is that **a blocking or CPU-bound handler stalls the receive thread itself**, delaying reads of subsequent frames on that connection and also delaying connection shutdown until the handler returns; keep the `Queued` default when that isolation matters.
- The concurrency cap is `MaxConcurrentMessages`, and real parallelism only happens when a message suspends on `await`. Within one message (decode → auth → dispatch) everything is strictly sequential; response order is not receive order.
- **A blocking or CPU-bound handler occupies the consumer coroutine** and delays later frames on the same connection. Offload such work with `Task.Run` or make it asynchronous. Async handlers (for example a 20–50ms remote auth call) are unaffected.
- With `MessageTimeout == 0` (default) `ctx.CancellationToken` *is* the connection token and no linked token is created. A per-message linked CTS is allocated only when a timeout is configured, and is restored/released in the message's `finally`.

---

## 🛡️ Capacity Limits: MaxBufferedBytes and MaxFrameLength

| Setting | Rule |
|---------|------|
| `Splitter` | Required; when null, `ChannelPipeline.Create` throws `ArgumentException` |
| `MaxBufferedBytes == 0` | Derived as `Splitter.MaxFrameLength + FrameConstants.P_Head + ReadBufferSize`, i.e. `MaxFrameLength + 9 + ReadBufferSize` |
| `MaxBufferedBytes != 0` | Must be `>= MaxFrameLength + P_Head`; otherwise `Create` throws `ArgumentOutOfRangeException` (a legal maximum frame could not be buffered whole) |
| Derived or explicit value > `int.MaxValue` | `Create` throws `ArgumentOutOfRangeException` |
| Runtime overflow | After each round drains every complete frame into the queue, leftover bytes still above `MaxBufferedBytes` close the connection with `reason = "buffered-bytes-limit"` |
| Oversized single frame | Checked inside `IFrameSplitter.TrySplit`, which throws `FrameProtocolException`; `TakeAvailableSplitter` is the exception — it splits at the limit instead of throwing |

- The default `MaxBufferedBytes = 1MB` only fits `MaxFrameLength` up to roughly 960KB; configure it explicitly when the protocol allows larger frames.
- `ReadBufferSize` is only headroom for "the largest legal frame plus a little tail". It does not limit how much a single `ReadAsync` returns and does not affect the `Pipe` capacity or how leftover bytes are accounted.
- `MaxBufferedBytes` measures un-framed **leftover bytes** in the `Pipe` (the same quantity as `ChannelTraffic.BufferedBytes`), not the size of a single frame. Complete frames are drained before the check, so a burst of legal frames is never killed by mistake.

---

## 🐞 Outbound Forwarding Checks

- The same outbound node may call `ctx.WriteAsync` at most once per business write; a second forward throws `InvalidOperationException`, and this check is active in `Release` builds too. Different nodes forwarding the same instance in turn (for example a logging handler passing an intermediate carrier through) is legal.
- A context view is only valid inside its outbound callback: calling `ctx.WriteAsync` outside a callback, or `CloseAfterWriteAsync` / `ctx.Pipeline.WriteAsync` inside an outbound callback, throws `InvalidOperationException` synchronously.
- It does **not** cover "forgot to forward": returning without forwarding and deliberately dropping a message are indistinguishable at runtime. The outbound boundary only compensates a `RawFrame` that was not handed off; ordinary business objects remain the business code's responsibility.
- Calling `IRawByteStream.SendAsync` directly and bypassing the chain head is not detected at runtime either; code review forbids it.

---

## 📋 Built-in Handlers

| Handler | Direction | Purpose |
|---------|-----------|---------|
| `FrameEncoderHandler` | Outbound | Completes an `EncodedBody` into a full frame and wraps it as `TransportFrame` for the head |
| `CodecEncoderHandler` | Outbound | `Envelope` → `EncodedBody` via `ICodec.Encode` |
| `EnvelopeWrapHandler` | Outbound | Business entity → `Envelope.Type` + `Envelope.Payload` |
| `OutboundSerializingHandler` | Outbound | Single-writer FIFO serializer: queue before encoding; must be the rightmost outbound node |
| `OutboundLoggingHandler` | Outbound | Logs the outbound message type and forwards it |
| `CodecDecoderHandler` | Inbound | `PooledMessage` → `Envelope`, then releases the frame bytes |
| `AuthHandler` | Inbound | First-level authentication: checks only the `Envelope.Type` allow-list, never parses the payload |
| `EnvelopeUnwrapHandler` | Inbound | `Envelope` → business entity, routed through `TypeRegistry` |
| `TokenAuthHandler` | Inbound | Second-level authentication: validates the connection token after deserialization |
| `InboundLoggingHandler` | Inbound | Logs the inbound message type and forwards it |
| `MessageDispatcher` | Inbound | Invokes `IMessageHandler` by exact CLR type, with an optional fallback |
| `ExceptionTerminalHandler` | Inbound | Chain tail: logs exceptions, decides whether to disconnect, reclaims unconsumed messages |
| `FramePassthroughHandler` | Inbound | Raw-frame bypass (`Retain` then forward); mutually exclusive with the codec branch |
| `FrameSplitterHandler` | Inbound | Only for manual in-chain splitting when not using the receive pump |

Pluggable implementations: `LengthFieldSplitter`, `DelimiterSplitter`, `FixedLengthSplitter`, `LineSplitter`, `SwitchableSplitter`, `TakeAvailableSplitter`, `LengthFieldFrameEncoder`, `EnvelopeCodec`, `JsonMessageCodec`, `ProtobufMessageCodec`, `RawCopyCodec`, `TypeRegistry`.

---

## 🧪 Demo and Self-Tests

```bash
dotnet run --project Src/SAEA.Sockets.PipelineTest -c Release -- --all     # in-memory loopback, full chain
dotnet run --project Src/SAEA.Sockets.PipelineTest -c Release -- --socket  # real SaeaServerSocket integration
dotnet run --project Src/SAEA.Sockets.PipelineTest -c Release -- --tls     # real socket + TLS, first frame after handshake
dotnet run --project Src/SAEA.Sockets.PipelineTest -c Release -- --bench   # throughput/allocation trend vs. BaseCoder
```

Without arguments it only prints usage, so automation must pass one explicitly; `--bench` is a trend observation, not a gate. The unit-test project is not in the solution and must be run on its own:

```bash
dotnet test Src/SAEA.Sockets.Pipeline.Tests/SAEA.Sockets.Pipeline.Tests.csproj -c Debug
```

---

## 📦 Installation

```bash
# NuGet
dotnet add package SAEA.Sockets.Pipeline

# or Package Manager
Install-Package SAEA.Sockets.Pipeline
```

---

## 🔗 Related Resources

| Resource | Link |
|----------|------|
| 📂 **GitHub** | [yswenli/SAEA](https://github.com/yswenli/SAEA) |
| 📦 **NuGet** | [SAEA.Sockets.Pipeline](https://www.nuget.org/packages/SAEA.Sockets.Pipeline) |
| 📖 **Docs** | [SAEA.Sockets](../SAEA.Sockets/README.en.md) |
| 🧪 **Demo and self-tests** | [SAEA.Sockets.PipelineTest](../SAEA.Sockets.PipelineTest) |
| 📝 **Blog** | [Author's blog](https://www.cnblogs.com/yswenli/) |

---

## 📄 License

Apache License 2.0 - free to use, modify and distribute!

---

<p align="center">
  <b>💡 Questions? Issues and PRs are welcome!</b>
</p>

<p align="center">
  Made with ❤️ by <a href="https://github.com/yswenli">yswenli</a>
</p>