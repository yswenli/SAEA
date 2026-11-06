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

## 🧱 Architecture

```
Inbound (head → tail):
  Pipe → receive pump (framing + backpressure) → PooledMessage → CodecDecoderHandler → AuthHandler
       → EnvelopeUnwrapHandler → TokenAuthHandler → InboundLoggingHandler
       → MessageDispatcher → ExceptionTerminalHandler

Outbound (tail → head):
  business ctx.WriteAsync(entity) → EnvelopeWrapHandler → CodecEncoderHandler
       → FrameEncoderHandler → chain head → IRawByteStream.SendAsync(frame, owner) → transport
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
// so add the handler closest to the transport first (FrameEncoderHandler) and the handler
// closest to business code last (EnvelopeWrapHandler).
// Outbound traversal is tail → head, so the actual order is:
// EnvelopeWrapHandler → CodecEncoderHandler → FrameEncoderHandler → transport.
pipeline.AddLast(new FrameEncoderHandler(new LengthFieldFrameEncoder()));
pipeline.AddLast(new CodecEncoderHandler(codec));
pipeline.AddLast(new EnvelopeWrapHandler(registry, new JsonMessageCodec()));

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

        return (data, owner) =>
        {
            server.SendAsync(channelId, data, owner);
            return Task.CompletedTask;
        };
    }

    public string ChannelId
    {
        get { return _channelId; }
    }

    public PipeReader Input
    {
        get { return _input; }
    }

    public Task SendAsync(ReadOnlyMemory<byte> data, IDisposable owner)
    {
        try
        {
            // Pass owner straight through: the transport releases it when sending completes or fails.
            // The adapter must never dispose it before returning.
            return _send(data, owner);
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

- `SendAsync` returning `Task.CompletedTask` means only that ownership of the frame has been handed to the transport — not that the peer received it. `owner` must stay undisposed until the task returns.
- When the connection is closed, `SendAsync` must throw `ChannelClosedException` immediately instead of queueing or silently succeeding; other transport exceptions are translated to `ChannelClosedException` too.
- Both server and client support integration; both sides share the same `UseInputHandler` / `UseConnectionOpened` hooks and the same configuration validation. On the client, wire it inside `UseInputHandler`:

  ```csharp
  var adapter = new SocketStreamAdapter(userToken.ID, input, flow, (data, owner) =>
  {
      client.SendAsync(data, owner);   // the client copies as needed and releases owner when done
      return Task.CompletedTask;
  });
  ```

---

## 🔐 Message Ownership

- A `PooledMessage` starts with reference count 1 and its ownership belongs to the chain. The execution wrapper for every message calls `Release()` once in a `finally`, so the count returns to zero no matter where an exception is thrown; `Release()` is a no-op on an already released instance.
- An inbound handler must not use a message after passing it downstream. To keep it across an `await` or a callback boundary, call `Retain()` first and `Release()` that reference when your handler's lifetime ends. The executor releases the queue-entry reference; the two are not interchangeable.
- `CodecDecoderHandler` releases the queue entry as soon as decoding succeeds. If decoding throws, it does not release there; the execution wrapper's `finally` handles it.
- The backing store of `PooledMessage.Frame` depends on length: `>= SmallFrameInlineThreshold` (512B by default) rents from `MemoryPoolManager` and sets `IsPooled == true`; smaller frames use an exact `new byte[len]` with `IsPooled == false` and are left to the GC. Accessing `Frame` after release throws `ObjectDisposedException`.
- `Envelope.Payload` is always an independent `new byte[len]` and never points at pooled backing storage, so it carries no return obligation. `EnvelopeCopyThreshold` (4KB by default) only controls a `Warn` log line; it does not change how copying works.
- For raw frame bytes (packet capture, auditing, binary passthrough), use `FramePassthroughHandler`: it calls `Retain()` and forwards the `PooledMessage` unchanged, and the branch's own tail handler releases that reference. This branch is mutually exclusive with the codec branch and cannot share a chain with it.
- Outbound intermediates `EncodedBody` / `TransportFrame` stay inside the outbound chain. When `await ctx.WriteAsync(...)` returns normally, ownership has moved to the chain head/transport; if it throws, the creator disposes them.

---

## ⏱️ Lifecycle and Close Semantics

- `WriteAsync` before `Attach` is invalid: the head cannot find an executor or byte stream and throws `InvalidOperationException("pipeline 尚未 Attach 到传输层。")`. `ctx.Channel` is unavailable for the same reason.
- `ChannelPipelineExecutor.Attach(pipeline)` must complete before `Start()`; otherwise `Start()` throws `InvalidOperationException`.
- `CloseAsync(reason)` only means "shutdown has begun": it returns immediately while shutdown continues in the background. Wait on `ChannelPipelineExecutor.Completion` to know that resources have been reclaimed.
- After shutdown begins, `WriteAsync` throws `ChannelClosedException`; `IRawByteStream.SendAsync` must throw the same exception once the connection is closed. This check precedes message-type validation: an old `ctx` from an already-removed node still reports closed even if the message is not a `TransportFrame`.
- Never call `GetStream()` or `ChannelInfo.Stream.ReadAsync` again — it would compete with the pump for the `PipeReader`. Once `UseInputHandler` is configured, the pump is the sole reader for that connection; a second reader steals data and triggers illegal concurrent reads on the pipe. Outbound `Stream.WriteAsync` is unaffected.
- Once the hooks are enabled, the event pump is not started for that connection: neither server `OnServerReceiveSpan` nor client `OnClientReceiveSpan` fires.
- `UseInputHandler` must be paired with `UseConnectionOpened` and requires `.UseStream()`. `SocketOptionBuilder.Build()` validates this and throws `InvalidOperationException`.
- `UseConnectionOpened` runs synchronously: on the server inside `RaiseAccepted`, on the client before the connect callback. Keep it to lightweight registration (create the `IChannel`, write initial attributes). Blocking I/O and `await` are forbidden.

---

## 💰 Known Costs

- Inbound costs exactly **1 copy per frame** on the executor pump path: frames are cut directly out of the `Pipe` buffer and copied once into a `PooledMessage` (`AllocateFrame`). `AdvanceTo` only advances to the end of the last complete frame, so un-framed leftover bytes stay in the `Pipe` for the next `ReadAsync`; the queue entry survives `await`s, so this one copy cannot be skipped and its backing store must not point at `Pipe` memory.
- Pure chaining (no executor, framing done by `FrameSplitterHandler` at the chain head) still costs **2 copies**: ① `Pipe` → `FrameAccumulator`, ② accumulator → standalone `PooledMessage`.
- Outbound costs exactly **3 copies**: ① entity → `Envelope.Payload` (the `byte[]` produced by the codec), ② `Payload` → `EncodedBody` (codec writer), ③ `EncodedBody` → final frame buffer (frame writer). The transport takes over that same buffer under `owner` semantics and the pipeline copies it no further; an adapter that wants to retain bytes pays for that copy itself.
- Allocation profile: small frames (`< 512B` by default) use exact allocation, so each frame adds one `byte[len]` plus one `PooledMessage`; large frames rent their backing store from `MemoryPoolManager` and add only a `PooledMessage`. These are the accepted costs of orthogonal two-level codecs and a non-prependable `IBufferWriter`; do not judge this pipeline by zero-copy expectations.
- Measured profile: `PipelineTest --bench` (265B frames) reports about 480B/frame, of which the pipeline itself accounts for about 352B (`byte[265]` at 296B plus a 56B `PooledMessage`); the rest comes from segment allocations while the benchmark harness feeds 64KB chunks into the `Pipe`.

---

## 🧵 Message Execution Model

- One consumer coroutine per connection: after dequeuing a message and acquiring a concurrency slot it **starts the execution chain inline** — no `await`, no extra thread-pool dispatch. A message that completes synchronously just returns to the consumer loop for the next frame.
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
| Oversized single frame | Checked inside `IFrameSplitter.TrySplit`, which throws `FrameProtocolException` |

- The default `MaxBufferedBytes = 1MB` only fits `MaxFrameLength` up to roughly 960KB; configure it explicitly when the protocol allows larger frames.
- `ReadBufferSize` is only headroom for "the largest legal frame plus a little tail". It does not limit how much a single `ReadAsync` returns and does not affect the `Pipe` capacity or how leftover bytes are accounted.
- `MaxBufferedBytes` measures un-framed **leftover bytes** in the `Pipe` (the same quantity as `ChannelTraffic.BufferedBytes`), not the size of a single frame. Complete frames are drained before the check, so a burst of legal frames is never killed by mistake.

---

## 🐞 Debug Assertion Boundary

- In `Debug` builds a lightweight outbound check exists: **the same handler node** may call `ctx.WriteAsync` with the same `msg` instance only once; a second write triggers the assertion. Different nodes forwarding the same instance (for example a logging handler passing an intermediate carrier through) is legal. That covers "one handler writes the same message twice".
- It does **not** cover "forgot to forward": returning without forwarding and deliberately dropping a message are indistinguishable at runtime, so this relies on code review and end-to-end tests that assert the reply actually arrives.
- Calling `IRawByteStream.SendAsync` directly and bypassing the chain head is not detected at runtime either; code review forbids it.

---

## 📋 Built-in Handlers

| Handler | Direction | Purpose |
|---------|-----------|---------|
| `FrameEncoderHandler` | Outbound | Completes an `EncodedBody` into a full frame and wraps it as `TransportFrame` for the head |
| `CodecEncoderHandler` | Outbound | `Envelope` → `EncodedBody` via `ICodec.Encode` |
| `EnvelopeWrapHandler` | Outbound | Business entity → `Envelope.Type` + `Envelope.Payload` |
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

Pluggable implementations: `LengthFieldSplitter`, `DelimiterSplitter`, `FixedLengthSplitter`, `LineSplitter`, `LengthFieldFrameEncoder`, `EnvelopeCodec`, `JsonMessageCodec`, `ProtobufMessageCodec`, `RawCopyCodec`, `TypeRegistry`.

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