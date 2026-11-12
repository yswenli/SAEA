# SAEA.WebSocket - High-performance WebSocket Server/Client 🔌

[![NuGet version](https://img.shields.io/nuget/v/SAEA.WebSocket.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.WebSocket)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**English Version** | **[中文版](README.md)**

> A high-performance WebSocket component for .NET Standard 2.0 with a complete RFC 6455 implementation, built for high-concurrency connections.
>
> ⚡ **Zero-copy core**: runs on SAEA.Sockets' Span/Memory zero-copy IO, pooled buffers, and the `SAEA.Sockets.Pipeline` handler chain, keeping allocations and GC low.
>
> 🧩 **Handler-chain assembly**: receive, send, handshake, and framing are all pipeline nodes. You write business logic in your own handler; there are no user-facing send/receive events anymore.

## Quick Navigation 🧭

| Section | Content |
|---------|---------|
| [⚡ Quick Start in 30s](#quick-start-in-30s) | The simplest example |
| [🎯 Key Features](#key-features) | What the component offers |
| [📐 Architecture](#architecture) | Components and data flow |
| [💡 Use Cases](#use-cases) | When to choose SAEA.WebSocket |
| [📊 Performance](#performance) | Comparison with alternatives |
| [❓ FAQ](#faq) | Quick answers |
| [🔧 Core Types](#core-types) | Main classes |
| [📝 Examples](#examples) | Detailed code samples |

---

## Quick Start in 30s ⚡

Three steps to a running WebSocket server:

### Step 1: Install the NuGet package

```bash
dotnet add package SAEA.WebSocket
```

### Step 2: Create the WebSocket server

```csharp
using SAEA.Sockets.Pipeline;
using SAEA.WebSocket;
using SAEA.WebSocket.Model;

var server = new WSServer(port: 39654);

// Assembled once per connection: echo whatever arrives
server.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new EchoHandler(server, id));
server.Start();

sealed class EchoHandler : InboundHandlerAdapter
{
    private readonly WSServer _server;
    private readonly string _id;

    public EchoHandler(WSServer server, string id)
    {
        _server = server;
        _id = id;
    }

    public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
    {
        var frame = msg as WSProtocal;
        if (frame == null)
            return ctx.FireChannelReadAsync(msg);

        // The payload lives in a pooled buffer; copy before using it elsewhere or later
        _server.Reply(_id, new WSProtocal(frame.Type, frame.Content.ToArray()));
        return Task.CompletedTask;
    }
}
```

### Step 3: Create the WebSocket client

```csharp
using SAEA.Sockets.Pipeline;
using SAEA.WebSocket;
using SAEA.WebSocket.Model;

var client = new WSClient("ws://127.0.0.1:39654");
client.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new PrintHandler());

if (client.Connect())
{
    client.Send("Hello WebSocket!");
}

sealed class PrintHandler : InboundHandlerAdapter
{
    public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
    {
        if (msg is WSProtocal frame)
            Console.WriteLine(Encoding.UTF8.GetString(frame.Content.Span));

        return Task.CompletedTask;
    }
}
```

**That's it!** 🎉 You now have a high-performance WebSocket system built for concurrent connections.

---

## Key Features 🎯

| Feature | Description | Benefit |
|---------|-------------|---------|
| 🚀 **Cross-platform async IO** | The .NET runtime picks the underlying IO | High CPU efficiency under concurrency |
| 📜 **Complete RFC 6455** | Standard WebSocket protocol | Works with browsers and standard clients |
| 🧩 **Handler chain** | `ConfigurePipeline` assembles each connection | Clean split between protocol and business logic |
| 🔒 **SSL/TLS** | `wss://` and certificate configuration, TLS via BouncyCastle | No `SslStream` dependency, consistent across platforms |
| 💬 **Frame types** | Text, Binary, Ping/Pong, Close | Covers common messaging needs |
| 💓 **Built-in heartbeat replies** | Ping answered with Pong, Close answered then closed | Keep-alive needs no business code |
| 🧵 **Fragment reassembly** | Continuation frames joined, control frames allowed in between | Large messages delivered on message boundaries |
| 📏 **Message size limit** | 64MB per message by default, oversized messages close the connection | Protects memory from giant messages |
| 📡 **Sub-protocol negotiation** | `Sec-WebSocket-Protocol` support | Flexible protocol extension |
| 📦 **Pooled buffers** | Payloads come from `MemoryPoolManager` | Lower GC pressure |

---

## Architecture 📐

### Component Diagram

```
┌───────────────────────────────────────────────────────────────┐
│                    SAEA.WebSocket architecture                 │
├───────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌──────────────┐        ┌──────────────┐                    │
│  │   WSServer   │        │   WSClient   │                    │
│  │  (server)    │        │  (client)    │                    │
│  └──────┬───────┘        └──────┬───────┘                    │
│         │  one chain per conn   │                             │
│         └──────────┬───────────┘                              │
│                    ▼                                           │
│         ┌──────────────────────┐                               │
│         │   IChannelPipeline   │  ← SAEA.Sockets.Pipeline      │
│         │   (nodes in order)   │                               │
│         └──────────┬───────────┘                               │
│                    │                                           │
│   outbound ◄───────┼───────────► inbound                       │
│  WsMessageEncoder  │          WsHandshakeHandler               │
│  OutboundSerializing│          WsFrameDecoder                  │
│                    │          business handler → terminal      │
└───────────────────────────────────────────────────────────────┘
```

### Chain Order

| Direction | Nodes (left to right) | Responsibility |
|-----------|-----------------------|----------------|
| Outbound | `WsMessageEncoder` → `OutboundSerializingHandler` | Frame encoding (server unmasked, client masked), batched writes |
| Inbound | `WsHandshakeHandler` → `WsFrameDecoder` → **business handler** → `ExceptionTerminalHandler` | Handshake and protocol switch, fragment reassembly, business logic, error termination |

A session handler (server `SessionHandler`, client `ClientSessionHandler`) sits in the inbound path too. It answers Ping with Pong and closes on Close; neither control frame reaches business handlers, while data frames and Pong do.

Assign `ConfigurePipeline` before the server `Start()` or the client's first `Connect()`; assigning it after the chain starts throws `InvalidOperationException`. Reconnects reuse the callback assigned the first time.

### Flow

```
Client connection:

client ──► TCP established ──► ChannelActiveAsync (transport up, handshake not done yet)
                                    │
                                    ▼
                        WsHandshakeSplitter cuts the handshake block
                                    │
                                    ▼
                        WsHandshakeHandler validates and writes 101
                                    │
                                    ▼
                        switch to WsFrameSplitter (protocol upgraded)
                                    │
                                    ▼
                        first frame = data phase begins

Receive path:

TCP bytes ──► WsFrameSplitter ──► WsFrameDecoder
                                        │
                                        ▼
                          ┌────────────────────────────┐
                          │ reassembly / size checks   │
                          └─────────────┬──────────────┘
                                        │
                                        ▼
                             business handler ChannelReadAsync
                                        │
                                        ▼
                          ctx.Pipeline.WriteAsync(WSProtocal)
                                        │
                                        ▼
                    WsMessageEncoder ──► frame written to client
```

---

## Use Cases 💡

### ✅ Good fit

| Scenario | Description | Why |
|----------|-------------|-----|
| 💬 **Messaging** | Private/group chat, support desks | Bidirectional, low latency |
| 📊 **Live data push** | Quotes, scores, monitoring | High throughput, real time |
| 🎮 **Game traffic** | Real-time matches, state sync | Async IO handles many players |
| 🤖 **IoT devices** | Monitoring, remote control | Many concurrent connections |
| 📝 **Collaboration** | Editors, whiteboards | Bidirectional sync |
| 🔔 **Notifications** | System and order messages | Instant delivery, no polling |

### ❌ Poor fit

| Scenario | Recommended alternative |
|----------|-------------------------|
| Simple HTTP APIs | SAEA.Http or ASP.NET |
| Raw TCP protocols | SAEA.Sockets |
| MQTT devices | SAEA.MQTT |
| Redis caching | SAEA.RedisSocket |

---

## Performance 📊

### Compared with traditional WebSocket stacks

| Metric | SAEA.WebSocket | Traditional | Advantage |
|--------|----------------|-------------|-----------|
| **Concurrent connections** | High concurrency oriented | Moderate | **Async IO** |
| **CPU utilization** | High | Medium | **Async IO** |
| **Memory footprint** | Pooled reuse | Frequent allocation | **Less GC pressure** |
| **Latency** | Low | Higher | **Snappier responses** |
| **Throughput** | High | Medium | **High throughput** |

### Compared with SignalR

| Aspect | SAEA.WebSocket | SignalR | Notes |
|--------|----------------|---------|-------|
| **Dependencies** | Lightweight | ASP.NET Core | SAEA is lighter |
| **Protocols** | Pure WebSocket | WebSocket + LongPolling etc. | SignalR is broader |
| **Performance** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | SAEA async IO tuning |
| **Learning curve** | Simple | Moderate | SAEA is easier to start |
| **Cross-platform** | .NET Standard | .NET Core | Both are cross-platform |

> 💡 **Tip**: SAEA.WebSocket relies on cross-platform async IO, so it fits workloads with strict performance requirements.

---

## FAQ ❓

### Q1: How is WebSocket different from HTTP long polling?

**A**: WebSocket is full duplex:
- Connection: stays open after the handshake, no repeated handshakes
- Timeliness: the server can push immediately
- Efficiency: no HTTP header overhead per message
- Resources: fewer connections and less bandwidth

### Q2: How do I enable SSL/TLS?

**A**: Start the server with a certificate; use `wss://` or explicit TLS options on the client:

```csharp
// Server: pfx certificate
var server = new WSServer(port: 443, protocols: SslProtocols.Tls12,
    pfxPath: "server.pfx", pwd: "password");
server.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new EchoHandler(server, id));
server.Start();

// Client: wss:// enables TLS
var client = new WSClient("wss://127.0.0.1:443");
```

For self-signed certificates or a custom target host, use the `Action<SaeaTlsOptions>` overload:

```csharp
var client = new WSClient("127.0.0.1", 443, SubProtocolType.Default, "", 64 * 1024, tls =>
{
    tls.TargetHost = "127.0.0.1";
    tls.AllowInvalidCertificates = true;   // test environments only
});
```

### Q3: How do I handle a custom protocol or message format?

**A**: The payload is still your own bytes; serialize in your handler:

```csharp
sealed class JsonHandler : InboundHandlerAdapter
{
    public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
    {
        if (msg is WSProtocal frame)
        {
            // Valid only during this callback; copy before using it elsewhere
            var json = Encoding.UTF8.GetString(frame.Content.Span);
            var my = JsonConvert.DeserializeObject<MyMessage>(json);
            // ...
        }

        return Task.CompletedTask;
    }
}

client.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new JsonHandler());
client.Send(JsonConvert.SerializeObject(new MyMessage { Type = 1, Content = "Hello" }));
```

### Q4: How does the heartbeat work?

**A**: The chain answers automatically: Ping is answered with Pong, Close is answered with Close before teardown. Business handlers only see data frames and Pong.

```csharp
// Client-initiated heartbeat
client.Ping();

// Pong arrives as WSProtocal (Type = Pong)
if (frame.Type == WSProtocalType.Pong)
    Console.WriteLine("server is alive");
```

Sending a Ping every 30 seconds is a common choice; a missing Pong within the timeout means the connection is gone.

### Q5: How do I set a sub-protocol?

**A**: Pass it to the client constructor:

```csharp
var client = new WSClient("ws://127.0.0.1:39654", SubProtocolType.Json);

// Server-side decisions happen inside your business handler
server.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new ChatHandler(id));
```

### Q6: How many connections are supported?

**A**: The server defaults to `maxConnects: 1000` and can be raised:

```csharp
var server = new WSServer(port: 39654, bufferSize: 64 * 1024, maxConnects: 10000);
server.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new EchoHandler(server, id));
server.Start();
```

Real limits depend on hardware and network conditions.

### Q7: How do I handle binary payloads?

**A**: Send and receive byte arrays directly:

```csharp
// Send binary
byte[] binaryData = new byte[] { 0x01, 0x02, 0x03 };
client.Send(binaryData, WSProtocalType.Binary);

// Branch on the frame type
sealed class BinaryAwareHandler : InboundHandlerAdapter
{
    public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
    {
        if (msg is WSProtocal frame)
        {
            if (frame.Type == WSProtocalType.Binary)
                ProcessBinary(frame.Content.ToArray());
            else if (frame.Type == WSProtocalType.Text)
                Console.WriteLine(Encoding.UTF8.GetString(frame.Content.Span));
        }

        return Task.CompletedTask;
    }
}
```

### Q8: Where are connect/disconnect notifications?

**A**: Override `ChannelActiveAsync` / `ChannelInactiveAsync` in your own handler, or inspect `Clients` on the server.

```csharp
sealed class LifecycleHandler : InboundHandlerAdapter
{
    public override Task ChannelActiveAsync(IChannelHandlerContext ctx)
    {
        // Transport is up; on the server the WebSocket handshake may still be in progress
        return Task.CompletedTask;
    }

    public override Task ChannelInactiveAsync(IChannelHandlerContext ctx)
    {
        // Connection closed; clean up session state here
        return Task.CompletedTask;
    }

    public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
    {
        // The first frame means the handshake succeeded and the data phase started
        return ctx.FireChannelReadAsync(msg);
    }
}
```

Difference from the old `OnConnected` event: `ChannelActiveAsync` is a **transport-level** notification, so on the server it fires right after TCP accept (before the handshake completes). On the client, `Connect()` waits for the handshake, so it is normally observed after a successful handshake. Use the first inbound frame when you need the exact "handshake completed" moment.

### Q9: Why is a send rejected?

**A**: Outbound pending writes are bounded (`MaxPendingWrites = 256` by default). Sends beyond that are rejected with an exception; `WSClient.Send` logs the failure instead of throwing into your thread.

```csharp
// Throttle batches by the acknowledged count so the outbound budget is never saturated
int sent = 0;
int acked = 0;   // Interlocked.Increment inside your handler's ChannelReadAsync

for (int i = 0; i < total; i++)
{
    client.Send(payload);
    Interlocked.Increment(ref sent);

    while (Volatile.Read(ref acked) < Volatile.Read(ref sent) - 128)
        Thread.Sleep(1);
}
```

You can add your own handlers in `ConfigurePipeline`, but the outbound prefix at the head of the chain and the terminal error handler at the tail are reserved by the framework.

---

## Core Types 🔧

| Type | Description |
|------|-------------|
| `WSServer` | WebSocket server with SSL/TLS; assemble the chain via `ConfigurePipeline` |
| `WSClient` | WebSocket client for `ws://`, `wss://`, and the TLS option overloads |
| `WSProtocal` | WebSocket frame wrapper; `Content` is a read-only view over a pooled buffer |
| `WSProtocalType` | Frame type enum (Text/Binary/Ping/Pong/Close) |
| `WSCoder` | Standalone codec for scenarios that bypass the handler chain |
| `WsFrameDecoder` | Fragment reassembly, control-frame interleaving, message size checks |
| `WsMessageEncoder` | Outbound frame encoder (server unmasked, client masked) |
| `WsHandshakeHandler` | Handshake validation, 101/request handling, protocol switch |

---

## Examples 📝

### WebSocket server

```csharp
using SAEA.Sockets.Pipeline;
using SAEA.WebSocket;
using SAEA.WebSocket.Model;

var server = new WSServer(port: 39654);

server.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new EchoHandler(server, id));
server.Start();

Console.WriteLine($"Server started, clients: {server.Clients.Count}");
```

### Secure WSS server

```csharp
using System.Security.Authentication;
using SAEA.WebSocket;

var server = new WSServer(port: 443, protocols: SslProtocols.Tls12,
    pfxPath: "server.pfx", pwd: "password");

server.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new EchoHandler(server, id));
server.Start();
```

### WebSocket client

```csharp
using SAEA.Sockets.Pipeline;
using SAEA.WebSocket;

var client = new WSClient("ws://127.0.0.1:39654");
client.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new PrintHandler());

if (client.Connect())
{
    client.Send("Hello WebSocket!");
    client.Ping();
}
```

### Writing from a handler

```csharp
sealed class ReplyHandler : InboundHandlerAdapter
{
    public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
    {
        if (!(msg is WSProtocal frame))
            return ctx.FireChannelReadAsync(msg);

        // Write through this connection's chain; encoding is done by WsMessageEncoder
        return ctx.Pipeline.WriteAsync(new WSProtocal(frame.Type, frame.Content.ToArray()));
    }
}
```

On the server you can also use `server.Reply(id, frame)` / `server.Disconnect(id)` from any handler.

### URL formats

```csharp
var client1 = new WSClient("ws://127.0.0.1:39654");
var client2 = new WSClient("wss://secure.example.com:443");  // SSL
var client3 = new WSClient("127.0.0.1", 39654);  // IP + port
var client4 = new WSClient(new Uri("ws://example.com/ws"));
```

### Sub-protocol negotiation

```csharp
// Constants: Empty / Default / Json / Protocol2; custom strings also work
var client = new WSClient("ws://127.0.0.1:39654", SubProtocolType.Json);
```

### Handling frame types

```csharp
using SAEA.WebSocket.Model;
using SAEA.WebSocket.Type;

sealed class FrameSwitchHandler : InboundHandlerAdapter
{
    public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
    {
        if (!(msg is WSProtocal frame))
            return ctx.FireChannelReadAsync(msg);

        switch (frame.Type)
        {
            case WSProtocalType.Text:
                Console.WriteLine($"text: {Encoding.UTF8.GetString(frame.Content.Span)}");
                break;
            case WSProtocalType.Binary:
                Console.WriteLine($"binary: {frame.Content.Length} bytes");
                break;
            case WSProtocalType.Pong:
                Console.WriteLine("heartbeat response");
                break;
        }

        return Task.CompletedTask;
    }
}
```

> ⚠️ `Close` frames are handled by the built-in session handler (reply then teardown) and never reach business handlers.

---

## Frame Types

```csharp
public enum WSProtocalType : byte
{
    Cont = 0,      // continuation
    Text = 1,      // text message
    Binary = 2,    // binary message
    Close = 8,     // close frame
    Ping = 9,      // heartbeat request
    Pong = 10      // heartbeat response
}
```

---

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| SAEA.Sockets | 26.11.5.1 | Socket transport |
| SAEA.Sockets.Pipeline | 26.11.5.1 | Handler chain and frame executor |
| SAEA.Common | 26.11.5.1 | Shared utilities |

---

## More Resources

- [GitHub repository](https://github.com/yswenli/SAEA)
- [NuGet package](https://www.nuget.org/packages/SAEA.WebSocket)
- [Author's blog](https://www.cnblogs.com/yswenli/)

---

## License

Apache License 2.0