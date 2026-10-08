# SAEA.Sockets - High-Performance IOCP Socket Communication Framework 🔌

[![NuGet version](https://img.shields.io/nuget/v/SAEA.Sockets.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.Sockets)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**English Version** | **[中文版](README.md)**

> A high-performance Socket communication framework based on .NET Standard 2.0, using Windows IOCP completion port technology, supporting tens of thousands of concurrent connections; the send/receive path is built on SAEA.Common zero-copy (Span/Memory) and pooled buffers.

## Quick Navigation 🧭

| Section | Content |
|---------|---------|
| [⚡ 30-Second Quick Start](#30-second-quick-start) | Simplest getting started example |
| [🎯 Core Features](#core-features) | Main framework capabilities |
| [📐 Architecture Design](#architecture-design) | Component relationships and workflow |
| [💡 Use Cases](#use-cases) | When to choose SAEA.Sockets |
| [📊 Performance Comparison](#performance-comparison) | Comparison with other solutions |
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

**That's it!** 🎉 You've implemented a high-performance TCP communication system supporting tens of thousands of concurrent connections.

---

## Core Features 🎯

| Feature | Description | Benefit |
|---------|-------------|---------|
| 🚀 **High-Performance IOCP** | Windows completion port async model | Supports tens of thousands of concurrent connections, high CPU utilization |
| 🔒 **SSL/TLS Encryption** | Stream mode supports secure connections | Encrypted data transmission, privacy protection |
| 📡 **Dual Protocol Support** | TCP + UDP dual mode | TCP for reliable transmission, UDP for high-speed broadcast |
| 🌐 **IPv6 Support** | Fully compatible with IPv6 protocol | Future-proof network environment |
| 💾 **Zero-Copy + Memory Pool** | `OnServerReceiveSpan` span receive, BufferManager, UserTokenPool, MemoryPoolManager | Zero-copy parsing, fewer allocations, lower GC pressure |
| 🔄 **Session Management** | SessionManager auto management | Auto timeout cleanup, connection state tracking |
| 🛠️ **Custom Protocol** | ICoder interface extension | Flexible protocol encoder/decoder |
| 🔗 **Builder Configuration** | Fluent configuration builder | Clean code, easy to understand |

---

## Architecture Design 📐

### Component Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                     SAEA.Sockets Architecture                │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────┐      ┌──────────────┐                    │
│  │ IocpSocket   │      │ StreamSocket │                    │
│  │  (IOCP Mode) │      │ (Stream Mode)│                    │
│  └──────────────┘      └──────────────┘                    │
│         │                     │                             │
│         └──────────┬──────────┘                             │
│                    │                                         │
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

Network Data ──► IOCP Complete ──► BufferManager Receive
                                      │
                                      ▼
                            ┌─────────────────┐
                            │     Coder       │ Decode data
                            │   Decode()      │
                            └─────────────────┘
                                      │
                                      ▼
                            OnServerReceiveSpan event triggered
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
| 🎮 **Game Servers** | Real-time battles, state sync | IOCP supports tens of thousands of players online simultaneously |
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

## Performance Comparison 📊

### Comparison with Traditional Socket Solutions

| Metric | SAEA.Sockets | Traditional Socket | Advantage |
|--------|--------------|-------------------|-----------|
| **Concurrent Connections** | 10,000+ | ~1,000 | **10x improvement** |
| **CPU Utilization** | ~85% | ~30% | **High efficiency** |
| **Memory Usage** | Pooled reuse | Frequent allocation | **Lower GC pressure** |
| **Latency** | ~1ms | ~10ms | **Low latency response** |
| **Throughput** | High | Medium | **High throughput** |

### IOCP vs Other Async Models

| Model | Concurrent Performance | Platform | Complexity |
|-------|----------------------|----------|------------|
| **IOCP (SAEA.Sockets)** | ⭐⭐⭐⭐⭐ | Windows | Medium |
| Select | ⭐⭐ | Cross-platform | Simple |
| Poll | ⭐⭐ | Cross-platform | Simple |
| Epoll (Linux) | ⭐⭐⭐⭐ | Linux | Medium |

> 💡 **Tip**: IOCP is the most efficient async IO model on Windows platform, designed specifically for high-concurrency scenarios.

---

## FAQ ❓

### Q1: What is IOCP? Why choose IOCP?

**A**: IOCP (I/O Completion Port) is Windows platform's completion port technology, currently the most efficient async IO model on Windows. Compared to traditional Select/Poll models:
- Supports larger concurrent connections (tens of thousands+)
- Higher CPU utilization (single thread handles multiple connections)
- Lower system resource consumption

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

**A**: Use Stream mode and configure certificate:

```csharp
var option = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)
    .UseStream()  // Use stream mode
    .UseSsl()     // Enable SSL
    .SetSslCertificate("server.pfx", "password")
    .Build();
```

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

**A**: Default configuration supports 1000 connections, adjustable via `SetCount()`:

```csharp
var option = SocketOptionBuilder.Instance
    .SetCount(10000)  // Set maximum connection count
    .Build();
```

Actual performance depends on server hardware configuration.

---

## Core Classes 🔧

| Class Name | Description |
|------------|-------------|
| `IocpServerSocket` / `IocpClientSocket` | IOCP mode TCP server/client |
| `StreamServerSocket` / `StreamClientSocket` | Stream mode TCP server/client (supports SSL) |
| `UdpServerSocket` / `UdpClientSocket` | UDP server/client |
| `SocketOptionBuilder` | Fluent configuration builder |
| `SocketFactory` | Socket factory class |
| `SessionManager` | Session manager |
| `BaseCoder` | Default protocol encoder (8-byte length + 1-byte type + content) |
| `BufferManager` | Memory buffer pool (backed by MemoryPoolManager) |
| `UserTokenPool` | User token pool |

---

## Usage Examples 📝

### TCP Server (IOCP Mode)

```csharp
using SAEA.Sockets;
using SAEA.Sockets.Handler;

// Use Builder fluent configuration for server
var option = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)  // Set Socket type to TCP
    .UseIocp()                       // Use IOCP async model
    .SetIP("127.0.0.1")              // Listen IP
    .SetPort(39654)                  // Listen port
    .SetBufferSize(1024 * 64)        // Buffer size 64KB
    .SetCount(1000)                  // Maximum connections
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

### TCP Client (IOCP Mode)

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

```csharp
using SAEA.Sockets;

var option = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)
    .UseStream()              // Use stream mode (supports SSL)
    .SetIP("127.0.0.1")
    .SetPort(443)
    .UseSsl()                 // Enable SSL/TLS
    .SetSslCertificate("server.pfx", "password")  // Configure certificate
    .Build();

var server = SocketFactory.CreateServerSocket(option);
server.Start();
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
| `IClientSocket.ReceiveAsync(byte[], int, int, CancellationToken)` (interface member) | Use the span events; the concrete `IocpClientSocket.ReceiveAsync` is retained but throws `KernelException`, and `UdpClientSocket.ReceiveAsync` throws `NotSupportedException`; use `GetStream()` for raw reads |
| `IServerSocket.SendAsync(string, byte[])` / `Send(string, byte[])` / `End(string, byte[])` / `SendAsync(IPEndPoint, byte[])` | `SendAsync(string, ReadOnlyMemory<byte>)` / `Send(string, ReadOnlySpan<byte>)` / `End(string, ReadOnlyMemory<byte>)` / `SendAsync(IPEndPoint, ReadOnlyMemory<byte>)` |
| `IocpClientSocket.UserToken` | No longer public; use `Connected`, or `ChannelInfo.UserToken` / `Context.UserToken` depending on the scenario |
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
| SAEA.Common | 26.9.21.1 | Common utility library |
| Pipelines.Sockets.Unofficial | 2.2.8 | Pipeline Socket extension |
| System.IO.Pipelines | 10.0.6 | High-performance IO pipeline |

---

## More Resources

- [GitHub Repository](https://github.com/yswenli/SAEA)
- [NuGet Package](https://www.nuget.org/packages/SAEA.Sockets)
- [Author Blog](https://www.cnblogs.com/yswenli/)

---

## License

Apache License 2.0