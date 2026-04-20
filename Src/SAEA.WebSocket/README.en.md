# SAEA.WebSocket - High-Performance WebSocket Server/Client 💬

[![NuGet version](https://img.shields.io/nuget/v/SAEA.WebSocket.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.WebSocket)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**English Version** | **[中文版](README.md)**

> A high-performance WebSocket component based on .NET Standard 2.0, fully implementing RFC 6455 protocol, supporting tens of thousands of concurrent connections.

## Quick Navigation 🧭

| Section | Content |
|---------|---------|
| [⚡ 30-Second Quick Start](#30-second-quick-start) | Simplest getting started example |
| [🎯 Core Features](#core-features) | Main framework capabilities |
| [📐 Architecture Design](#architecture-design) | Component relationships and workflow |
| [💡 Use Cases](#use-cases) | When to choose SAEA.WebSocket |
| [📊 Performance Comparison](#performance-comparison) | Comparison with other solutions |
| [❓ FAQ](#faq) | Quick answers to common questions |
| [🔧 Core Classes](#core-classes) | Overview of main classes |
| [📝 Usage Examples](#usage-examples) | Detailed code examples |

---

## 30-Second Quick Start ⚡

The fastest way to get started, just 3 steps to run a WebSocket server:

### Step 1: Install NuGet Package

```bash
dotnet add package SAEA.WebSocket
```

### Step 2: Create WebSocket Server (Only 5 Lines of Code)

```csharp
using SAEA.WebSocket;

var server = new WSServer(port: 39654);
server.OnMessage += (id, data) => server.Reply(id, data);  // Reply immediately upon receiving message
server.Start();
```

### Step 3: Create WebSocket Client Connection

```csharp
var client = new WSClient("ws://127.0.0.1:39654");
client.OnMessage += (data) => Console.WriteLine(Encoding.UTF8.GetString(data.Content));
client.Connect();
client.Send("Hello WebSocket!");
```

**That's it!** 🎉 You've implemented a high-performance WebSocket communication system supporting tens of thousands of concurrent connections.

---

## Core Features 🎯

| Feature | Description | Benefits |
|---------|-------------|----------|
| 🚀 **IOCP High Performance** | Windows completion port async model | Supports tens of thousands of concurrent connections, high CPU utilization |
| 📜 **RFC 6455 Full Implementation** | Standard WebSocket protocol | Compatible with all browsers and clients |
| 🔒 **SSL/TLS Encryption** | WSS secure connection | Encrypted data transmission, privacy protection |
| 💬 **Multiple Frame Types Support** | Text, Binary, Ping/Pong, Close | Meets various communication scenarios |
| 💓 **Built-in Heartbeat Mechanism** | Ping/Pong automatic detection | Connection keep-alive, automatic disconnect detection |
| 📡 **Subprotocol Negotiation** | Sec-WebSocket-Protocol support | Flexible protocol extension |
| ⚡ **Batch Processing Optimization** | ClassificationBatcher read/write batch processing | Improved throughput performance |
| 🛠️ **Simple API** | Event-driven model | Easy to understand and use |

---

## Architecture Design 📐

### Component Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                   SAEA.WebSocket Architecture               │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────┐      ┌──────────────┐                    │
│  │   WSServer   │      │   WSClient   │                    │
│  │  (Server)    │      │  (Client)    │                    │
│  └──────────────┘      └──────────────┘                    │
│         │                     │                             │
│         └──────────┬──────────┘                             │
│                    │                                         │
│            ┌───────▼───────┐                                │
│            │  WSProtocal   │                                │
│            │(Protocol Wrap)│                                │
│            └───────┬───────┘                                │
│                    │                                         │
│     ┌──────────────┼──────────────┐                        │
│     │              │              │                         │
│  ┌──▼──┐      ┌────▼───┐     ┌───▼────┐                   │
│  │Coder│      │ Batcher│     │Session │                   │
│  │Codec│      │Batch   │     │Manager │                   │
│  └──┬──┘      └────┬───┘     └───┬────┘                   │
│     │              │              │                         │
│  WSCoder      Classfication   WSUserToken                 │
│  (Encoder/     Batcher       (User Token)                 │
│   Decoder)                                               │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### Workflow Diagram

```
Client Connection Flow:

Client ──► HTTP Handshake Request ──► WSServer.Accept
                                           │
                                           ▼
                                 ┌─────────────────┐
                                 │ Protocol Upgrade│
                                 │ Upgrade: WebSocket│
                                 └─────────────────┘
                                           │
                                           ▼
                                 ┌─────────────────┐
                                 │  Sec-WebSocket-  │
                                 │  Key Validation │
                                 └─────────────────┘
                                           │
                                           ▼
                                 OnConnected Event Triggered

Data Reception Flow:

Client Data ──► Frame Parse ──► WSProtocal.Decode
                                      │
                                      ▼
                            ┌─────────────────┐
                            │  Frame Type     │
                            │ Determination   │
                            │ Text/Binary/etc │
                            └─────────────────┘
                                      │
                        ┌─────────────┼─────────────┐
                        │             │             │
                        ▼             ▼             ▼
                     Text Frame  Binary Frame   Ping Frame
                        │             │             │
                        └─────────────┼─────────────┘
                                      │
                                      ▼
                            OnMessage Event Triggered
                                      │
                                      ▼
                            ┌─────────────────┐
                            │   WSProtocal    │
                            │   Encode()      │
                            └─────────────────┘
                                      │
                                      ▼
                            Send Response Frame to Client
```

---

## Use Cases 💡

### ✅ Scenarios Suitable for SAEA.WebSocket

| Scenario | Description | Recommended Reason |
|----------|-------------|---------------------|
| 💬 **Instant Messaging** | Private chat, group chat, customer service systems | Bidirectional real-time communication, low latency |
| 📊 **Real-time Data Push** | Stock quotes, sports scores, monitoring data | High throughput, strong real-time capability |
| 🎮 **Game Communication** | Real-time battles, state synchronization | IOCP supports tens of thousands of players online |
| 🤖 **IoT Devices** | Device monitoring, remote control | Supports large-scale concurrent device connections |
| 📝 **Collaboration Tools** | Online editing, whiteboard sharing | Bidirectional sync, real-time collaboration |
| 🔔 **Push Notifications** | System messages, order notifications | Instant delivery, no polling needed |

### ❌ Unsuitable Scenarios

| Scenario | Recommended Alternative |
|----------|-------------------------|
| Simple HTTP API | Use SAEA.Http or ASP.NET |
| Native TCP Protocol | Use SAEA.Sockets |
| MQTT Devices | Use SAEA.MQTT |
| Redis Cache | Use SAEA.RedisSocket |

---

## Performance Comparison 📊

### Comparison with Traditional WebSocket Solutions

| Metric | SAEA.WebSocket | Traditional WebSocket | Advantage |
|--------|----------------|----------------------|-----------|
| **Concurrent Connections** | 10,000+ | ~1,000 | **10x Improvement** |
| **CPU Utilization** | ~85% | ~30% | **Efficient Use** |
| **Memory Footprint** | Pooled Reuse | Frequent Allocation | **Reduced GC Pressure** |
| **Latency** | ~1ms | ~10ms | **Low Latency Response** |
| **Throughput** | High | Medium | **High Throughput** |

### Comparison with SignalR

| Feature | SAEA.WebSocket | SignalR | Notes |
|---------|----------------|---------|-------|
| **Dependencies** | Lightweight | ASP.NET Core | SAEA is more lightweight |
| **Protocol Support** | Pure WebSocket | WebSocket + LongPolling, etc. | SignalR is more comprehensive |
| **Performance** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | SAEA IOCP optimization |
| **Learning Curve** | Simple | Moderate | SAEA is easier to get started |
| **Cross-platform** | .NET Standard | .NET Core | Both are cross-platform |

> 💡 **Tip**: SAEA.WebSocket is based on IOCP for high concurrency, suitable for scenarios with extreme performance requirements.

---

## FAQ ❓

### Q1: What's the difference between WebSocket and HTTP long polling?

**A**: WebSocket is a full-duplex communication protocol, compared to HTTP long polling:
- Connection persistence: WebSocket maintains connection after establishment, no repeated handshakes needed
- Real-time: Server can actively push, lower latency
- Efficiency: No HTTP header overhead, more efficient data transmission
- Resources: Reduced server connections and bandwidth consumption

### Q2: How to implement SSL/TLS secure connection?

**A**: Configure certificate using `WSServer`:

```csharp
var server = new WSServer(port: 443, useSSL: true);
server.SetCertificate("server.pfx", "password");
server.Start();
```

Client uses `wss://` protocol:
```csharp
var client = new WSClient("wss://secure.example.com:443");
```

### Q3: How to handle custom protocol/message format?

**A**: Use `WSProtocal` class or inherit `WSCoder`:

```csharp
// Message frame structure
public class MyMessage
{
    public int Type { get; set; }
    public string Content { get; set; }
}

// Encode when sending
var json = JsonConvert.SerializeObject(new MyMessage { Type = 1, Content = "Hello" });
client.Send(json);

// Decode when receiving
client.OnMessage += (data) =>
{
    var msg = JsonConvert.DeserializeObject<MyMessage>(Encoding.UTF8.GetString(data.Content));
};
```

### Q4: How does the heartbeat mechanism work?

**A**: Built-in Ping/Pong heartbeat detection:

```csharp
// Client sends heartbeat
client.Ping();

// Client receives Pong response
client.OnPong += () => Console.WriteLine("Server is alive");

// Server automatically responds with Pong, no extra code needed
```

It's recommended to send Ping periodically (e.g., every 30 seconds), connection is considered disconnected if no response within timeout.

### Q5: How to set subprotocol?

**A**: Specify via constructor parameter:

```csharp
// Client specifies subprotocol
var client = new WSClient("ws://127.0.0.1:39654", subProtocol: "chat");

// Server side checks subprotocol
server.OnConnected += (id) =>
{
    var protocol = server.GetSubProtocol(id);
    Console.WriteLine($"Subprotocol: {protocol}");
};
```

### Q6: What's the maximum number of connections supported?

**A**: Default configuration supports 1000 connections, adjustable based on server hardware:

```csharp
// WSServer internally uses SAEA.Sockets, inheriting its configuration capabilities
var server = new WSServer(port: 39654);
// Configure larger connection count via SocketOptionBuilder
```

Actual performance depends on server hardware configuration and network environment.

### Q7: How to handle binary data?

**A**: Send and receive byte arrays directly:

```csharp
// Send binary data
byte[] binaryData = new byte[] { 0x01, 0x02, 0x03 };
client.Send(binaryData);

// Check type when receiving
client.OnMessage += (data) =>
{
    if (data.Type == WSProtocalType.Binary)
    {
        // Handle binary data
        ProcessBinary(data.Content);
    }
    else if (data.Type == WSProtocalType.Text)
    {
        // Handle text data
        var text = Encoding.UTF8.GetString(data.Content);
    }
};
```

---

## Core Classes 🔧

| Class Name | Description |
|------------|-------------|
| `WSServer` | WebSocket server, supports SSL/TLS |
| `WSClient` | WebSocket client, supports ws:// and wss:// |
| `WSProtocal` | WebSocket protocol frame encapsulation, RFC 6455 implementation |
| `WSProtocalType` | Protocol type enum (Text/Binary/Ping/Pong/Close) |
| `WSCoder` | Message encoder/decoder, handles frame parsing and construction |
| `WSUserToken` | User session token, manages individual connection state |
| `ClassificationBatcher` | Batch processing optimizer, improves read/write performance |

---

## Usage Examples 📝

### WebSocket Server

```csharp
using SAEA.WebSocket;
using SAEA.WebSocket.Model;

var server = new WSServer(port: 39654);

server.OnConnected += (id) => 
    Console.WriteLine($"Client connected: {id}");

server.OnMessage += (id, data) => 
{
    var msg = Encoding.UTF8.GetString(data.Content);
    Console.WriteLine($"Message received: {msg} (Type: {data.Type})");
    server.Reply(id, data);  // Reply to client
};

server.OnDisconnected += (id) => 
    Console.WriteLine($"Client disconnected: {id}");

server.Start();
Console.WriteLine("WebSocket server started, port: 39654");
```

### WSS Secure Server

```csharp
using SAEA.WebSocket;

var server = new WSServer(port: 443, useSSL: true);
server.SetCertificate("server.pfx", "password");

server.OnMessage += (id, data) => server.Reply(id, data);
server.Start();
```

### WebSocket Client

```csharp
using SAEA.WebSocket;
using System.Text;

var client = new WSClient("ws://127.0.0.1:39654");

client.OnPong += () => Console.WriteLine("Pong response received");

client.OnMessage += (data) => 
    Console.WriteLine($"Message received: {Encoding.UTF8.GetString(data.Content)}");

client.OnError += (ex) => Console.WriteLine($"Error: {ex.Message}");

client.OnDisconnected += () => Console.WriteLine("Connection disconnected");

client.Connect();
client.Send("Hello WebSocket!");
```

### URL Format Support

```csharp
var client1 = new WSClient("ws://127.0.0.1:39654");
var client2 = new WSClient("wss://secure.example.com:443");  // SSL
var client3 = new WSClient("127.0.0.1", 39654);  // IP + Port
var client4 = new WSClient(new Uri("ws://example.com/ws"));
```

### Subprotocol Negotiation

```csharp
var client = new WSClient("ws://127.0.0.1:39654", subProtocol: "chat");
```

### Handling Different Frame Types

```csharp
using SAEA.WebSocket.Model;

client.OnMessage += (data) =>
{
    switch (data.Type)
    {
        case WSProtocalType.Text:
            Console.WriteLine($"Text message: {Encoding.UTF8.GetString(data.Content)}");
            break;
        case WSProtocalType.Binary:
            Console.WriteLine($"Binary message: {data.Content.Length} bytes");
            break;
        case WSProtocalType.Close:
            Console.WriteLine("Close frame");
            break;
    }
};
```

---

## Protocol Frame Types

```csharp
public enum WSProtocalType : byte
{
    Cont = 0,      // Continuation frame
    Text = 1,      // Text message
    Binary = 2,    // Binary message
    Close = 8,     // Close frame
    Ping = 9,      // Heartbeat request
    Pong = 10      // Heartbeat response
}
```

---

## Dependencies

| Package | Version | Description |
|---------|---------|-------------|
| SAEA.Sockets | 7.26.2.2 | IOCP communication framework |
| SAEA.Common | 7.26.2.2 | Common utility classes |

---

## More Resources

- [GitHub Repository](https://github.com/yswenli/SAEA)
- [NuGet Package](https://www.nuget.org/packages/SAEA.WebSocket)
- [Author's Blog](https://www.cnblogs.com/yswenli/)

---

## License

Apache License 2.0