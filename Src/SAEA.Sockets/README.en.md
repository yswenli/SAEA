# SAEA.Sockets - Cross-Platform High-Performance Socket Framework 🔌

[![NuGet version](https://img.shields.io/nuget/v/SAEA.Sockets.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.Sockets)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**English Version** | **[中文版](README.md)**

> A cross-platform Socket framework based on .NET Standard 2.0. The underlying async IO mechanism is selected automatically by the .NET runtime: IOCP on Windows, epoll on Linux, kqueue on macOS — always behind the same API. The send/receive path is built on SAEA.Common zero-copy (Span/Memory) and pooled buffers.

## Quick Navigation 🧭

| Section | Content |
|---------|---------|
| [⚡ 30-Second Quick Start](#30-second-quick-start) | Simplest getting started example |
| [🎯 Core Features](#core-features) | Main framework capabilities |
| [🖥️ Platform Support](#platform-support) | Supported platforms and IO mechanisms |
| [📐 Architecture Design](#architecture-design) | Component relationships and workflow |
| [💡 Use Cases](#use-cases) | When to choose SAEA.Sockets |
| [📊 Implementation Modes](#implementation-modes) | The two transports and how to choose |
| [❓ FAQ](#faq) | Quick answers |
| [🔧 Core Classes](#core-classes) | Overview of main classes |
| [📝 Usage Examples](#usage-examples) | Detailed code examples |

---

## 30-Second Quick Start ⚡

The fastest way to get started - run a TCP server in just 3 steps:

### Step 1: Install NuGet Package

```bash
dotnet add package SAEA.Sockets
```

### Step 2: Create TCP Server (Only 5 Lines of Code)

```csharp
using SAEA.Sockets;
using SAEA.Sockets.Handler;

// Create server configuration
var option = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)
    .UseIocp()
    .SetPort(39654)
    .Build();

var server = SocketFactory.CreateServerSocket(option);
server.OnServerReceiveSpan += (userToken, data) => server.Send(userToken.ID, data);  // Echo message immediately
server.Start();
```

### Step 3: Create TCP Client Connection

```csharp
var client = SocketFactory.CreateClientSocket(option);
client.OnClientReceiveSpan += (data) => Console.WriteLine(Encoding.UTF8.GetString(data.ToArray()));
client.Connect();
client.SendAsync(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("Hello SAEA!")));
```

**That's it!** 🎉 You've implemented a TCP communication system built for high-concurrency scenarios.

---

## Core Features 🎯

| Feature | Description | Benefit |
|---------|-------------|---------|
| 🚀 **Cross-Platform Async IO** | The .NET runtime picks IOCP / epoll / kqueue automatically on Windows/Linux/macOS | One API on all three platforms, no conditional compilation or implementation switching |
| 🔒 **SSL/TLS Encryption** | Built-in BouncyCastle TLS 1.2/1.3 for both event and stream modes | Automatic certificate loading, SNI, mutual TLS and handshake callbacks without `SslStream` |
| 📡 **Dual Protocol Support** | TCP + UDP dual mode | TCP for reliable transmission, UDP for high-speed broadcast |
| 🌐 **IPv6 Support** | Fully compatible with IPv6 protocol | Future-proof network environment |
| 💾 **Zero-Copy + Memory Pool** | `OnServerReceiveSpan` span receive, BufferManager, UserTokenPool, MemoryPoolManager | Zero-copy parsing, fewer allocations, lower GC pressure |
| 🔄 **Session Management** | SessionManager auto management | Auto timeout cleanup, connection state tracking |
| 🛠️ **Custom Protocol** | ICoder interface extension | Flexible protocol encoder/decoder |
| 🔗 **Builder Configuration** | Fluent configuration builder | Clean code, easy to understand |

---

## Platform Support 🖥️

| Platform | Underlying async IO mechanism |
|----------|-------------------------------|
| Windows | IOCP (I/O Completion Port) |
| Linux | epoll |
| macOS | kqueue |

All three platforms share one API — no conditional compilation and no implementation switching. The mechanism is chosen by the .NET runtime for the current OS; this framework only owns send/receive scheduling, protocol codecs, and session management.

> ⚠️ **Naming note**: `UseIocp()` is a historical name for the `SocketAsyncEventArgs` transport, **not a Windows requirement** — it works just as well on Linux/macOS, backed by epoll/kqueue respectively. TCP has exactly one engine (`SaeaServerSocket` / `SaeaClientSocket`).
>
> `UseStream()` no longer switches engines. It only exposes a duplex `Stream` facade (`GetStream()` / `ChannelInfo.Stream`) on the same engine, and that facade exclusively consumes inbound data. TLS works with both event and stream modes.

---

## Architecture Design 📐

### Component Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                     SAEA.Sockets Architecture                │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────────┐      ┌──────────────────┐            │
│  │ SaeaServerSocket │      │ SaeaClientSocket │            │
│  │ (SocketAsyncEvt) │      │ (SocketAsyncEvt) │            │
│  └────────┬─────────┘      └────────┬─────────┘            │
│           │        SaeaConnection (Pipe + TLS)              │
│           └──────────────┬──────────┘                       │
│                          │                                   │
│            ┌───────▼───────┐                                │
│            │  BaseSocket   │                                │
│            │   (Base Class)│                                │
│            └───────┬───────┘                                │
│                    │                                         │
│     ┌──────────────┼──────────────┐                        │
│     │              │              │                         │
│  ┌──▼──┐      ┌────▼───┐     ┌───▼────┐                   │
│  │Pool │      │ Session│     │  Coder │                   │
│  │(Pool)│     │Manager │     │(Encode/│                   │
│  └──┬──┘      └────┬───┘     │ Decode)│                   │
│     │              │         └───┬────┘                   │
│  BufferPool    UserToken      ICoder                      │
│  (Memory Pool) (User Token)   (Protocol Interface)         │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### Workflow Diagram

```
Client Connection Flow:

Client ──► Connect ──► Server.Accepted
                              │
                              ▼
                    ┌─────────────────┐
                    │  UserTokenPool  │ Allocate user token
                    │   Get Token     │
                    └─────────────────┘
                              │
                              ▼
                    ┌─────────────────┐
                    │ SessionManager  │ Register session
                    │  Add Session    │
                    └─────────────────┘
                              │
                              ▼
                    OnAccepted event triggered

Data Receiving Flow:

Network Data ──► Async IO Complete (IOCP/epoll/kqueue) ──► BufferManager Receive
                                      │
                                      ▼
                            ┌─────────────────┐
                            │ SaeaConnection  │ (optional) TLS decrypt
                            └─────────────────┘
                                      │
                          ┌───────────┴───────────┐
                          ▼                       ▼
                 ┌─────────────────┐     ┌─────────────────┐
                 │ Stream mode:    │     │ Event mode:     │
                 │ Pipe / Stream   │     │ Span callbacks  │
                 └─────────────────┘     └─────────────────┘
                                      │
                                      ▼
                            OnServerReceiveSpan event triggered (event mode)
                                      │
                                      ▼
                            ┌─────────────────┐
                            │     Coder       │ Encode response
                            │   Encode()      │
                            └─────────────────┘
                                      │
                                      ▼
                            SendAsync(ReadOnlyMemory<byte>) send data
```

---

## Use Cases 💡

### ✅ Scenarios Suitable for SAEA.Sockets

| Scenario | Description | Recommendation Reason |
|----------|-------------|----------------------|
| 🎮 **Game Servers** | Real-time battles, state sync | Async IO plus session management, suited to large numbers of long-lived connections |
| 📊 **Real-time Data Push** | Stock quotes, sports scores | High throughput, low latency |
| 🤖 **IoT Device Communication** | Sensor data reporting | Supports massive concurrent device connections |
| 💬 **Instant Messaging** | Private chat, group chat, customer service | Complete session management, event-driven |
| 📁 **File Transfer** | Large file chunked transfer | Memory pool optimization, reduces GC |
| 🔗 **RPC Communication** | Microservice inter-communication | Binary protocol, efficient transmission |

### ❌ Unsuitable Scenarios

| Scenario | Recommended Alternative |
|----------|------------------------|
| Simple HTTP API | Use SAEA.Http or ASP.NET |
| Browser Clients | Use SAEA.WebSocket |
| MQTT Devices | Use SAEA.MQTT |
| Redis Cache | Use SAEA.RedisSocket |

---

## Implementation Modes 📊

TCP has exactly one underlying engine. `UseIocp()` and `UseStream()` only choose how data is delivered:

| Aspect | Event mode (`UseIocp()`) | Stream mode (`UseStream()`) |
|--------|--------------------------|------------------------------|
| Main types | `SaeaServerSocket` / `SaeaClientSocket` | Same (same engine) |
| Receive path | One `SocketAsyncEventArgs` per connection, delivered via `On*ReceiveSpan` | One `PipeReader` per connection, read/write via `GetStream()` / `ChannelInfo.Stream` |
| UDP | ✅ Supported (UDP is event-mode only) | ❌ Not supported |
| SSL/TLS | ✅ Supported (decrypted data goes to span callbacks) | ✅ Supported (decrypted data enters the `Pipe`) |
| Data consumption | Span events only | The `Stream` facade exclusively consumes inbound data |
| Underlying IO | Chosen by the .NET runtime: Windows IOCP / Linux epoll / macOS kqueue | Same |

> Naming note: `UseIocp()` means the `SocketAsyncEventArgs` transport; it has nothing to do with the operating system. The two modes are mutually exclusive; the last call wins.

### About performance numbers

This README does not publish cross-framework performance numbers (for comparisons with TouchSocket, DotNetty, etc., benchmark your own workload). The only reproducible measurement here is the zero-copy receive/send path: run `SAEA.P2PTest --bench-iocp` (4KB frames, single-machine loopback); results are documented in the root README.

---

## FAQ ❓

### Q1: Does this framework only run on Windows?

**A**: No. Windows, Linux and macOS share one API. The underlying async IO mechanism is selected by the .NET runtime for the current OS — IOCP on Windows, epoll on Linux, kqueue on macOS — while send/receive scheduling, codecs and session management stay identical. No conditional compilation is required.

The "Iocp" in `UseIocp()` is a historical name for the `SocketAsyncEventArgs` transport; it does not mean Windows-only.

### Q2: How to implement custom protocol?

**A**: Implement the `ICoder` interface or inherit from `BaseCoder`:

```csharp
using System;
using System.Buffers;
using System.Text;
using SAEA.Sockets.Base;

// Custom coder: shows "reuse BaseCoder's frame-based codec + filter by Type".
// Note BaseCoder.Encode/Decode are not virtual and cannot be overridden; EncodeText/Handle are
// only wrappers illustrating the base capabilities — the framework calls the base Encode/Decode via ICoder.
// To customize the wire format, implement ICoder directly.
public class MyCoder : BaseCoder
{
    public const byte Text = 1;

    // Encode: wrap the payload into a custom-type frame, then write it to the writer
    public void EncodeText(ReadOnlyMemory<byte> payload, IBufferWriter<byte> writer)
    {
        Encode(new BaseSocketProtocal(Text, payload), writer);
    }

    // Decode: pass in the received sequence; the returned batch must be disposed (frame memory is valid until Dispose)
    public void Handle(ReadOnlySequence<byte> data)
    {
        using (var frames = Decode(data))
        {
            foreach (var frame in frames.Frames)
            {
                if (frame.Type == Text)
                    Console.WriteLine($"Received text frame: {Encoding.UTF8.GetString(frame.Content.ToArray())}");
            }
        }
    }
}
```

Wiring (`UseIocp<T>()` internally builds `BaseContext<T>` and hands the coder to the framework):

```csharp
using SAEA.Sockets;

var option = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)
    .UseIocp<MyCoder>()   // inject the custom coder
    .SetPort(39654)
    .Build();
```

### Q3: How to configure SSL/TLS encryption?

**A**: Configure everything through the single `WithSsl(...)` entry point; both event and stream modes support TLS:

```csharp
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using SAEA.Sockets;
using SAEA.Sockets.Ssl;

// Server: load the PFX automatically; TLS 1.2 + TLS 1.3 are enabled by default.
var serverOption = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)
    .SetIP("0.0.0.0")
    .SetPort(443)
    .WithSsl(o =>
    {
        o.ServerCertificate = SaeaCertificateLoader.FromPfx("server.pfx", "password");
        o.Protocols = SaeaTlsProtocols.Default;
        o.ServerCertificateSelector = sni => sni switch
        {
            "api.example.com" => apiCertificate,
            _ => defaultCertificate
        };
        o.ClientCertificateRequired = true;            // mutual TLS (mTLS)
        o.HandshakeCompleted = context =>
            Console.WriteLine($"TLS handshake completed: {context.NegotiatedProtocol}");
    })
    .Build();

// Client: configure the SNI target host and certificate validation policy.
var clientOption = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)
    .SetIP("127.0.0.1")                    // Actual endpoint: SetIP only accepts IP literals
    .SetPort(443)
    .WithSsl(o =>
    {
        o.TargetHost = "api.example.com";  // Used for SNI and certificate hostname validation
        o.ClientCertificates = new X509Certificate2Collection(clientCertificate);
        o.CertificateValidationCallback = (sender, cert, chain, errors) => errors == SslPolicyErrors.None;
    })
    .Build();
```

`OnAccepted` (server) and `ConnectAsync` (client) only complete after a successful handshake; failures are reported through `OnError` / `SaeaTlsOptions.HandshakeFailed` (or `OnTlsHandshake(failed: ...)`). `ChannelInfo.Tls` / `StreamUserToken.Tls` expose the negotiated protocol, cipher suite and remote certificate. The TLS stack is implemented on BouncyCastle rather than `SslStream`, so it is not tied to stream mode.

### Q4: What is the purpose of memory pool?

**A**: `BufferManager` and `UserTokenPool` reduce GC (garbage collection) pressure through pre-allocation and memory reuse:
- Avoid frequent memory allocation/deallocation
- Improve overall performance and stability

### Q5: How to choose between TCP and UDP?

**A**: 
- **TCP**: Reliable transmission, ordered delivery, suitable for games, chat, RPC
- **UDP**: High-speed transmission, connectionless, suitable for video streaming, real-time broadcast

SAEA.Sockets supports both modes, choose flexibly based on your scenario.

### Q6: What is the maximum number of connections supported?

**A**: `MaxConnects` defaults to 1000 and can be adjusted with `SetMaxConnects()`:

```csharp
var option = SocketOptionBuilder.Instance
    .SetMaxConnects(10000)  // Set maximum connection count
    .Build();
```

The usable connection count is also bounded by the OS: on Linux/macOS raise the file-descriptor limit (e.g. `ulimit -n`); on Windows it depends on handles and memory. Treat benchmark results as the source of truth.

---

## Core Classes 🔧

| Class Name | Description |
|------------|-------------|
| `SaeaServerSocket` / `SaeaClientSocket` | TCP server/client (single IOCP engine; event mode, stream mode and built-in TLS) |
| `SaeaConnection` | Per-connection Pipe + TLS facade (backpressure, encryption and handshake state) |
| `SaeaTlsOptions` / `SaeaTlsProtocols` | TLS configuration, protocol versions and callbacks |
| `SaeaCertificateLoader` | PFX/P12, PEM, certificate store and directory loading (directory mode supports SNI matching) |
| `ChannelInfo` | Stream-mode connection info (`Stream`, `Tls`, `UserToken`) |
| `UdpServerSocket` / `UdpClientSocket` | UDP server/client |
| `SocketOptionBuilder` | Fluent configuration builder |
| `SocketFactory` | Socket factory class |
| `SessionManager` | Session manager |
| `BaseCoder` | Default protocol encoder (8-byte length + 1-byte type + content) |
| `BufferManager` | Memory buffer pool (reuses `byte[]` buffers) |
| `UserTokenPool` | User token pool |

---

## Usage Examples 📝

### TCP Server (SocketAsyncEventArgs Mode)

```csharp
using SAEA.Sockets;
using SAEA.Sockets.Handler;

// Use Builder fluent configuration for server
var option = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)  // Set Socket type to TCP
    .UseIocp()                       // SocketAsyncEventArgs mode (cross-platform)
    .SetIP("127.0.0.1")              // Listen IP
    .SetPort(39654)                  // Listen port
    .SetReadBufferSize(1024 * 64)    // Read buffer size 64KB
    .SetMaxConnects(1000)            // Maximum connections
    .Build();                        // Build configuration

var server = SocketFactory.CreateServerSocket(option);

// Register event handlers
server.OnAccepted += (obj) => 
    Console.WriteLine($"Client connected: {((SAEA.Sockets.Interface.IUserToken)obj).ID}");

server.OnServerReceiveSpan += (userToken, data) => 
{
    var message = Encoding.UTF8.GetString(data.ToArray());
    Console.WriteLine($"Received data: {message}");
    server.Send(userToken.ID, data);  // Reply to client
};

server.OnDisconnected += (id, ex) => 
    Console.WriteLine($"Client disconnected: {id}");

server.Start();
Console.WriteLine("Server started!");
```

### TCP Client (SocketAsyncEventArgs Mode)

```csharp
using SAEA.Sockets;

var option = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)
    .UseIocp()
    .SetIP("127.0.0.1")
    .SetPort(39654)
    .Build();

var client = SocketFactory.CreateClientSocket(option);

// Register event handlers
client.OnClientReceiveSpan += (data) => 
    Console.WriteLine($"Received: {Encoding.UTF8.GetString(data.ToArray())}");

client.OnDisconnected += (id, ex) => 
    Console.WriteLine("Connection disconnected");

// Connect to server
client.Connect();

// Send data
client.SendAsync(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("Hello SAEA!")));
```

### UDP Server

```csharp
using SAEA.Sockets;

var option = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Udp)  // UDP mode
    .SetIP("127.0.0.1")
    .SetPort(39655)
    .Build();

var server = SocketFactory.CreateServerSocket(option);

server.OnServerReceiveSpan += (userToken, data) => 
{
    Console.WriteLine($"Received UDP data");
    server.Send(userToken.ID, data);  // Send data back
};

server.Start();
```

### SSL/TLS Secure Connection

Server (event mode; decrypted plaintext is delivered through `OnServerReceiveSpan`):

```csharp
using SAEA.Sockets;
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Model;
using SAEA.Sockets.Ssl;

var option = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)
    .SetIP("127.0.0.1")
    .SetPort(443)
    .WithSsl(o =>
    {
        o.ServerCertificate = SaeaCertificateLoader.FromPfx("server.pfx", "password");
        o.Protocols = SaeaTlsProtocols.Default;
    })
    .Build();

var server = new SAEA.Sockets.Core.Tcp.SaeaServerSocket(option);
server.Start();
```

Stream mode (read/write through `ChannelInfo.Stream` after the handshake):

```csharp
var option = SocketOptionBuilder.Instance
    .UseStream()
    .SetIP("127.0.0.1")
    .SetPort(443)
    .WithSsl(o => o.ServerCertificate = SaeaCertificateLoader.FromPfx("server.pfx", "password"))
    .Build();

var server = new SAEA.Sockets.Core.Tcp.SaeaServerSocket(option);
server.OnAccepted += payload =>
{
    var channel = (ChannelInfo)payload;
    var buffer = new byte[4096];
    var read = channel.Stream.Read(buffer, 0, buffer.Length);
    channel.Stream.Write(buffer, 0, read);
};
server.Start();
```

Client:

```csharp
var option = SocketOptionBuilder.Instance
    .UseStream()                       // omit for event mode
    .SetIP("127.0.0.1")
    .SetPort(443)
    .WithSsl(o =>
    {
        o.TargetHost = "localhost";    // SNI + certificate host-name validation
        o.AllowInvalidCertificates = true;
    })
    .Build();

using (var client = new SaeaClientSocket(option))
{
    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        await client.ConnectAsync(cts.Token);

    var stream = client.GetStream();   // duplex stream in stream mode
    var payload = Encoding.UTF8.GetBytes("hello");
    await stream.WriteAsync(payload, 0, payload.Length);
}
```

### Shortcut Wrapper Classes

```csharp
using SAEA.Sockets.Shortcut;

// TCP server shortcut wrapper
var tcpServer = new TCPServer(39654);
tcpServer.OnReceive += (sender, userToken, data) => tcpServer.SendAsync(userToken.ID, data);
tcpServer.Start();

// TCP client shortcut wrapper
var tcpClient = new TCPClient("127.0.0.1", 39654);
tcpClient.OnReceive += (sender, data) => Console.WriteLine(Encoding.UTF8.GetString(data.ToArray()));
tcpClient.Connect();
tcpClient.SendAsync("Hello!");

// UDP shortcut wrapper
var udpServer = new UDPServer(39655);
var udpClient = new UDPClient("127.0.0.1", 39655);
```

### Custom Protocol Encoder

```csharp
using System;
using System.Buffers;
using System.Text;
using SAEA.Sockets.Base;

// Custom coder: shows "reuse BaseCoder's frame-based codec + filter by Type".
// Note BaseCoder.Encode/Decode are not virtual and cannot be overridden; EncodeText/Handle are
// only wrappers illustrating the base capabilities — the framework calls the base Encode/Decode via ICoder.
// To customize the wire format, implement ICoder directly.
public class MyCoder : BaseCoder
{
    public const byte Text = 1;

    // Encode: wrap the payload into a custom-type frame, then write it to the writer
    public void EncodeText(ReadOnlyMemory<byte> payload, IBufferWriter<byte> writer)
    {
        Encode(new BaseSocketProtocal(Text, payload), writer);
    }

    // Decode: pass in the received sequence; the returned batch must be disposed (frame memory is valid until Dispose)
    public void Handle(ReadOnlySequence<byte> data)
    {
        using (var frames = Decode(data))
        {
            foreach (var frame in frames.Frames)
            {
                if (frame.Type == Text)
                    Console.WriteLine($"Received text frame: {Encoding.UTF8.GetString(frame.Content.ToArray())}");
            }
        }
    }
}
```

Wiring (`UseIocp<T>()` internally builds `BaseContext<T>` and hands the coder to the framework):

```csharp
using SAEA.Sockets;

var option = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)
    .UseIocp<MyCoder>()   // inject the custom coder
    .SetPort(39654)
    .Build();
```

---

## Migrating from byte[] 🔄

Recent versions removed the `byte[]` public surface in favor of `Span` / `Memory`. The table below maps removed members to their replacements:

| Removed | Replacement |
|---------|-------------|
| `IClientSocket.OnReceive` (`OnClientReceiveHandler`, `byte[]`) | `OnClientReceiveSpan` (`ReadOnlySpan<byte>`; consume/copy synchronously inside the callback) |
| `IServerSocket.OnReceive` (`OnReceiveHandler`, `(ISession, byte[])`) | `OnServerReceiveSpan` (`(IUserToken, ReadOnlySpan<byte>)`) |
| `IClientSocket.BeginSend(byte[])` / `Send(byte[])` / `SendAsync(byte[])` / `SendAsync(byte[], int, int, CancellationToken)` | `Send(ReadOnlySpan<byte>)` / `SendAsync(ReadOnlyMemory<byte>)` / `SendAsync(ReadOnlyMemory<byte>, CancellationToken)`; express the old offset/count semantics with `data.AsMemory(offset, count)` |
| `IClientSocket.ReceiveAsync(byte[], int, int, CancellationToken)` (interface member) | Use the span events; the concrete `SaeaClientSocket.ReceiveAsync` is retained and reads from the connection `Pipe` (throws `KernelException` when no connection facade exists), and `UdpClientSocket.ReceiveAsync` throws `NotSupportedException` |
| `IServerSocket.SendAsync(string, byte[])` / `Send(string, byte[])` / `End(string, byte[])` / `SendAsync(IPEndPoint, byte[])` | `SendAsync(string, ReadOnlyMemory<byte>)` / `Send(string, ReadOnlySpan<byte>)` / `End(string, ReadOnlyMemory<byte>)` / `SendAsync(IPEndPoint, ReadOnlyMemory<byte>)` |
| `IocpClientSocket.UserToken` | No longer public; use `Connected`, or `ChannelInfo.UserToken` / `Context.UserToken` depending on the scenario |
| `IocpServerSocket` / `IocpClientSocket` | Renamed to `SaeaServerSocket` / `SaeaClientSocket` (`SocketFactory` already returns the new types) |
| `StreamServerSocket` / `StreamClientSocket` | Removed; use `SaeaServerSocket` / `SaeaClientSocket` with `UseStream()` and read/write through `GetStream()` or `ChannelInfo.Stream` |
| `UseSsl()` / `SetSslCertificate(...)` and direct `SslStream` usage | Replaced by `WithSsl(...)` + `SaeaTlsOptions`; TLS is implemented on BouncyCastle and works in both event and stream modes |
| `ChannelManager.Instance` / `ChannelManager.GetInstance` | Each server instance owns its own `ChannelManager`; the old static entries remain source-compatible only and are marked `[Obsolete]` |
| Shortcut `TCPClient` / `TCPServer` / `UDPClient` / `UDPServer` `OnReceive` | `TCPClient` / `TCPServer.OnReceive` now delivers `ReadOnlyMemory<byte>` instead of `byte[]` (`UDPClient` / `UDPServer.OnReceive` delivers `ISocketProtocal` frames); **source/binary breaking, recompile required** |

> ⚠️ **Migration notes**
> 1. The Shortcut `OnReceive` signature change (`byte[]` → `ReadOnlyMemory<byte>`) is a **source/binary breaking change** carried by the version bump. There is **no compatibility shim**; recompile your callers.
> 2. The old `OnReceive` event is removed; use `OnClientReceiveSpan` / `OnServerReceiveSpan` instead.
> 3. The span callback argument (`data`) is **only valid for the duration of the callback**. Consume it synchronously or `ToArray()`-copy it inside the callback. **Never store the span in a field or use it across threads/async**, which is a use-after-free.

---

## Default Protocol Format

Default message protocol implemented by `BaseCoder`:

```
| 8-byte Length | 1-byte Type | N-byte Content |
```

- **Length**: Total data length (for message boundary identification)
- **Type**: Message type identifier (extensible)
- **Content**: Actual message data

---

## Dependencies

| Package | Version | Description |
|---------|---------|-------------|
| SAEA.Common | 26.11.5.1 | Common utility library |
| BouncyCastle.Cryptography | 2.6.2 | Built-in TLS 1.2/1.3 protocol implementation |
| Pipelines.Sockets.Unofficial | 2.2.16 | Pipeline Socket extension |
| System.IO.Pipelines | 10.0.14 | High-performance IO pipeline |

---

## More Resources

- [GitHub Repository](https://github.com/yswenli/SAEA)
- [NuGet Package](https://www.nuget.org/packages/SAEA.Sockets)
- [Author Blog](https://www.cnblogs.com/yswenli/)

---

## License

Apache License 2.0