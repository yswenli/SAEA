# SAEA.FileSocket - High-Performance File Transfer Component 📁

[![NuGet version](https://img.shields.io/nuget/v/SAEA.FileSocket.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.FileSocket)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**English Version** | **[中文版](README.md)**

> A high-performance file transfer component based on SAEA.Sockets, using async IO, supporting large file chunked transfer, resumable upload/download, and real-time progress monitoring.
>
> ⚡ **Zero-copy core**: built on the SAEA.Sockets Span/Memory zero-copy send/receive path and pooled buffers, significantly reducing allocations and GC.

## Quick Navigation 🧭

| Section | Content |
|---------|---------|
| [⚡ 30-Second Quick Start](#30-second-quick-start-⚡) | Simplest getting started example |
| [🎯 Core Features](#core-features-🎯) | Main features of the component |
| [📐 Architecture Design](#architecture-design-📐) | Transfer flow and protocol |
| [💡 Use Cases](#use-cases-💡) | When to choose SAEA.FileSocket |
| [📊 Performance Comparison](#performance-comparison-📊) | Transfer performance data |
| [❓ FAQ](#faq-❓) | Quick answers to common questions |
| [🔧 Core Classes](#core-classes-🔧) | Overview of main classes |
| [📝 Usage Examples](#usage-examples-📝) | Detailed code examples |

---

## 30-Second Quick Start ⚡

The fastest way to get started - implement file transfer in just 3 steps:

### Step 1: Install NuGet Package

```bash
dotnet add package SAEA.FileSocket
```

### Step 2: Create File Receiving Server (Only 6 Lines of Code)

```csharp
using SAEA.FileSocket;

var fileTransfer = new FileTransfer(@"C:\ReceivedFiles", port: 39654);
fileTransfer.OnReceiveEnd += (path) => Console.WriteLine($"Receive completed: {path}");
fileTransfer.Start();
```

### Step 3: Send File

```csharp
fileTransfer.SendFile(@"C:\Data\video.mp4", "192.168.1.100");
```

**That's it!** 🎉 You've implemented a high-performance file transfer system with support for large files and resumable transfers.

---

## Core Features 🎯

| Feature | Description | Benefit |
|---------|-------------|---------|
| 🚀 **Cross-Platform Async IO** | Based on SAEA.Sockets async IO | Supports high-concurrency file transfers |
| 📦 **Large File Support** | Chunked transfer mechanism | Supports TB-level ultra-large files |
| 🔄 **Resumable Transfer** | offset parameter to resume from specified position | Recoverable from network interruptions |
| 📊 **Real-time Progress** | Send/receive speed, data statistics | Visual monitoring of transfer status |
| 💓 **Heartbeat Keep-alive** | 10-second heartbeat interval maintains connection | Automatic connection status detection |
| 📡 **Handler chain** | Inbound traffic is driven by `SAEA.Sockets.Pipeline`; `ConfigurePipeline` appends handlers on demand | Extend receive logic without forking the component |
| 🤝 **Four-stage Handshake** | Request/Allow/Reject/Data stream protocol | Secure and reliable transfer control |

---

## Architecture Design 📐

### Component Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                   SAEA.FileSocket Architecture              │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │                   FileTransfer                        │  │
│  │          (File Transfer Coordinator Main Class)       │  │
│  └───────────────────────┬──────────────────────────────┘  │
│                          │                                  │
│           ┌──────────────┴──────────────┐                  │
│           │                             │                   │
│  ┌────────▼────────┐          ┌────────▼────────┐        │
│  │      Client     │          │      Server      │        │
│  │    (Sender)     │          │   (Receiver)     │        │
│  └────────┬────────┘          └────────┬────────┘        │
│           │                             │                   │
│           │     ┌───────────────┐       │                   │
│           └────►│ FileMessage   │◄──────┘                  │
│                 │  (Message     │                           │
│                 │   Model)      │                           │
│                 └───────┬───────┘                          │
│                         │                                   │
│              ┌──────────┴──────────┐                       │
│              │                     │                        │
│       ┌──────▼──────┐      ┌───────▼──────┐               │
│       │  Request    │      │   Chunk      │               │
│       │  (Request)  │      │  (Data Chunk)│               │
│       └─────────────┘      └──────────────┘               │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐  │
│  │  SAEA.Sockets.Pipeline (framing / decoding / chain) │  │
│  └─────────────────────────────────────────────────────┘  │
│  ┌─────────────────────────────────────────────────────┐  │
│  │               SAEA.Sockets (Async IO)               │  │
│  └─────────────────────────────────────────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### File Transfer Flow Diagram

```
Complete File Transfer Flow:

Sender                                     Receiver
  │                                         │
  │  1. SendFile(fileName, targetIp)        │
  │────────────────────────────────────────►│
  │                                         │
  │  2. Request to send (RequestMessage)    │
  │     ├── FileName                        │
  │     └── FileLength                      │
  │────────────────────────────────────────►│
  │                                         │
  │                              3. Request handler
  │                                 ├── Server.Allow(id)  allow
  │                                 └── Server.Refuse(id) reject
  │                                         │
  │  4. Allow receive (AllowReceive)       │
  │◄────────────────────────────────────────│
  │                                         │
  │  5. Send data stream in chunks          │
  │     ┌─────────────────────────────────┐ │
  │     │ Chunk 1 (100KB)                 │ │
  │     │────────────────────────────────►│ │
  │     │ Chunk 2 (100KB)                 │ │
  │     │────────────────────────────────►│ │
  │     │ Chunk 3 (100KB)                 │ │
  │     │────────────────────────────────►│ │
  │     │ ...                             │ │
  │     │ Chunk N (remaining part)        │ │
  │     │────────────────────────────────►│ │
  │     └─────────────────────────────────┘ │
  │                                         │
  │                              6. Write file (File.WriteAllText)
  │                                         │
  │                              7. Built-in receive handler writes to disk
  │                                 └── FileTransfer raises OnReceiveEnd
  │                                         │
  │  8. Send complete callback              │
  │◄────────────────────────────────────────│
  │                                         │

Resumable Transfer Flow:

  │  SendFile(fileName, offset)             │
  │────────────────────────────────────────►│
  │  Continue sending from offset position  │
  │  ├── Skip already transferred part      │
  │  └── Only send remaining data           │
  │────────────────────────────────────────►│
```

### Transfer Protocol Details

```
┌─────────────────────────────────────────────────────────────┐
│                  Four-stage Handshake Protocol              │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Stage 1: Request (RequestMessage)                          │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ Message Type: Request                                │   │
│  │ Content: FileName + FileLength                      │   │
│  │ Description: Sender requests to send file           │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  Stage 2: Response (ResponseMessage)                       │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ AllowReceive: Allow receive                          │   │
│  │ RefuseReceive: Reject receive                        │   │
│  │ Description: Receiver decides whether to accept      │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  Stage 3: Data Stream (DataMessage)                         │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ Chunk size: Default 100KB                           │   │
│  │ Send method: SendAsync asynchronous sending          │   │
│  │ Description: Transfer file content in chunks         │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  Stage 4: Complete                                          │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ Trigger: built-in receive handler completes the file and FileTransfer raises OnReceiveEnd event │
│  │ Action: Save file, cleanup resources                 │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## Use Cases 💡

### ✅ Scenarios Suitable for SAEA.FileSocket

| Scenario | Description | Reason for Recommendation |
|----------|-------------|---------------------------|
| 📁 **LAN File Sharing** | Fast transfer of large files | cross-platform async IO, near wire speed on LAN |
| 💾 **Data Backup** | Scheduled backup of large data | Resumable transfer ensures reliability |
| 🔄 **File Synchronization** | Multi-server file sync | Chunked transfer, supports large files |
| 🎬 **Video Transfer** | Large video file transfer | Supports TB-level files |
| 🏢 **Enterprise Internal Transfer** | Secure intranet file distribution | Private protocol, controllable security |
| ☁️ **Cloud Data Migration** | Cross-server data migration | High throughput, progress monitoring |

### ❌ Unsuitable Scenarios

| Scenario | Recommended Alternative |
|----------|------------------------|
| Requires encrypted transfer | Use SAEA.Sockets + SSL |
| Cross-internet transfer | Use HTTPS or FTPS |
| Requires authentication | Add authentication layer yourself |

---

## Performance Comparison 📊

### Transfer Performance Data

| Metric | SAEA.FileSocket | Traditional HTTP Upload | Advantage |
|--------|-----------------|------------------------|-----------|
| **Transfer Speed** | High (wire speed on LAN) | Medium | **Higher throughput** |
| **Max File Size** | Supports TB-level | Limited by memory | **Ultra-large files** |
| **Resumable Transfer** | ✅ Native support | ❌ Requires extra implementation | **High reliability** |
| **Memory Usage** | Low (chunked transfer) | High (full cache) | **Memory friendly** |
| **Concurrent Transfer** | Supported | Requires multi-threading | **High concurrency** |

### Chunked Transfer Efficiency

| File Size | Default Chunk Size (100KB) | Recommended Chunk Size | Transfer Time (1Gbps) |
|-----------|---------------------------|-----------------------|----------------------|
| 100MB | 1000 chunks | 100KB | ~1 second |
| 1GB | 10000 chunks | 1MB | ~10 seconds |
| 10GB | 100000 chunks | 10MB | ~100 seconds |
| 100GB | 1000000 chunks | 10MB | ~17 minutes |

> 💡 **Tip**: Adjust the `bufferSize` parameter based on network environment and file size to optimize transfer efficiency.

### Comparison with Other Solutions

| Solution | Resumable | Progress Monitoring | Large File Support | Implementation Complexity |
|----------|-----------|---------------------|-------------------|---------------------------|
| **SAEA.FileSocket** | ✅ | ✅ | ✅ TB-level | ⭐ Simple |
| FTP | ✅ | ❌ | ✅ | ⭐⭐ Medium |
| HTTP Upload | ❌ | ✅ | ❌ Memory limit | ⭐⭐ Medium |
| Custom Socket | Needs implementation | Needs implementation | Needs implementation | ⭐⭐⭐ Complex |

---

## FAQ ❓

### Q1: How to implement resumable transfer?

**A**: Use the `offset` parameter to continue transfer from a specified position:

```csharp
// Record the transferred position
long lastOffset = GetLastOffset(fileName);

// Resume from breakpoint
client.SendFile(fileName, lastOffset, (success) => 
{
    if (success)
        Console.WriteLine("Resume successful");
});

// Receiver will continue writing from offset position
```

### Q2: How to adjust transfer chunk size?

**A**: Set the `bufferSize` parameter in the constructor:

```csharp
// Small files or unstable network: use smaller chunks
var fileTransfer = new FileTransfer(path, port, bufferSize: 50 * 1024);

// Large files or high-speed network: use larger chunks
var fileTransfer = new FileTransfer(path, port, bufferSize: 1024 * 1024);  // 1MB
```

### Q3: How to monitor transfer progress?

**A**: Use the `OnDisplay` event:

```csharp
fileTransfer.OnDisplay += (info) => 
{
    Console.WriteLine(info);  // Contains send/receive speed
};

fileTransfer.OnReceiveEnd += (filePath) => 
{
    Console.WriteLine($"Receive completed: {filePath}");
};
```

### Q4: How to reject receiving specific files?

**A**: Append a handler to the receive chain via `ConfigurePipeline` and call `Refuse` or let it pass:

```csharp
using SAEA.Common.Serialization;
using SAEA.FileSocket;
using SAEA.FileSocket.Model;
using SAEA.Sockets.Base;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;

var server = new Server(port: 39654);

server.ConfigurePipeline = (id, pipeline) =>
{
    pipeline.AddLast(new RejectLargeFileHandler(server));
};

server.Start();

sealed class RejectLargeFileHandler : InboundHandlerAdapter
{
    private readonly Server _server;

    public RejectLargeFileHandler(Server server)
    {
        _server = server;
    }

    public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
    {
        var protocal = msg as BaseSocketProtocal;
        if (protocal != null && protocal.Type == (byte)SocketProtocalType.RequestSend)
        {
            var file = SerializeHelper.PBDeserialize<FileMessage>(protocal.Content.ToArray());
            if (file.Length > 1024L * 1024 * 1024)
            {
                // Not forwarding downstream ends processing for this request.
                _server.Refuse(ctx.Channel.Id);
                return;
            }
        }

        await ctx.FireChannelReadAsync(msg);
    }
}
```

To accept, call `_server.Allow(ctx.Channel.Id)` and then `await ctx.FireChannelReadAsync(msg)` so the built-in handler still writes the file.

### Q5: Does it support transferring multiple files simultaneously?

**A**: Yes. Multiple `Client` instances each own a connection and send concurrently; a single `Client` sends serially:

```csharp
// Method 1: Use FileTransfer for sequential sending
fileTransfer.SendFile(file1, targetIp);
fileTransfer.SendFile(file2, targetIp);

// Method 2: Create multiple Clients for concurrent sending; endpoint lives on the constructor
var client1 = new Client(bufferSize: 100 * 1024, ip: "192.168.1.100", port: 39654);
var client2 = new Client(bufferSize: 100 * 1024, ip: "192.168.1.100", port: 39654);
client1.Connect();
client2.Connect();
client1.SendFile(file1, 0, success => Console.WriteLine("file1: " + success));
client2.SendFile(file2, 0, success => Console.WriteLine("file2: " + success));
```

### Q6: How to recover after transfer interruption?

**A**: FileSocket supports resumable transfer, just record the transferred bytes:

```csharp
// 1. Periodically save transferred position
long savedOffset = GetTransferredBytes(fileName);
SaveProgress(fileName, savedOffset);

// 2. Resume from saved position after interruption
long lastOffset = LoadProgress(fileName);
client.SendFile(fileName, lastOffset, callback);
```

### Q7: How to customize receive path?

**A**: The receive path comes from the `filePath` argument of `FileTransfer`; `Server` itself never writes files:

```csharp
// Received files land under C:\CustomPath, keeping the sender-side file name
var fileTransfer = new FileTransfer(@"C:\CustomPath", port: 39654);
fileTransfer.Start();
```

---

## Core Classes 🔧

| Class Name | Description |
|------------|-------------|
| `FileTransfer` | File transfer main coordinator class, supports both sending and receiving |
| `Client` | File sending client, used to actively send files |
| `Server` | File receiving server, exposes `Allow` / `Refuse` replies and the `ConfigurePipeline` extension point |
| `FileMessage` | File message data model, defines transfer protocol |
| `FileMessageDecoder` | Inbound decoder that turns a complete frame into a protocol object and drops heartbeat frames |
| `FileMessageEncoder` | Outbound encoder that writes protocol objects as complete wire frames |
| `FileFrameSplitter` | Wire-format framing configuration with a single frame-length ceiling |

---

## Usage Examples 📝

### Basic File Transfer

```csharp
using SAEA.FileSocket;

var fileTransfer = new FileTransfer(@"C:\ReceivedFiles", port: 39654);

fileTransfer.OnReceiveEnd += (filePath) => 
    Console.WriteLine($"File receive completed: {filePath}");

fileTransfer.OnDisplay += (info) => 
    Console.WriteLine($"Transfer status: {info}");

fileTransfer.Start();
fileTransfer.SendFile(@"C:\Data\document.pdf", "192.168.1.100");
```

### Server-side Custom Handling

```csharp
using SAEA.FileSocket;
using SAEA.Sockets.Pipeline;

var server = new Server(port: 39654, bufferSize: 100 * 1024);

// Append handlers to the receive chain; without one only the built-in disk writer runs.
server.ConfigurePipeline = (id, pipeline) =>
{
    pipeline.AddLast(new LoggingHandler());
};

server.Start();

sealed class LoggingHandler : InboundHandlerAdapter
{
    public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
    {
        Console.WriteLine($"Message from {ctx.Channel.Id}: {msg.GetType().Name}");

        // Hand back downstream so the built-in handler can still write the file.
        await ctx.FireChannelReadAsync(msg);
    }
}
```

### Client-side Sending (with Callback)

```csharp
using SAEA.FileSocket;

// The endpoint lives on the client; connect before sending.
var client = new Client(bufferSize: 100 * 1024, ip: "192.168.1.100", port: 39654);

client.Connect();

client.SendFile(@"C:\Data\document.pdf", 0, success =>
{
    if (success)
        Console.WriteLine("Send successful");
    else
        Console.WriteLine("Send failed");
});

// Resumable transfer: offset is the number of bytes already transferred
long offset = 1024 * 1024 * 100;
client.SendFile(@"C:\Data\large_file.dat", offset, success =>
    Console.WriteLine($"Resume result: {success}"));
```

### Custom Buffer Size

```csharp
using SAEA.FileSocket;

// Small buffer: unstable network scenario
var smallBuffer = new FileTransfer(path, port, bufferSize: 50 * 1024);

// Large buffer: high-speed network large file scenario
var largeBuffer = new FileTransfer(path, port, bufferSize: 1024 * 1024);
```

---

## Chunked Transfer Mechanism

| Parameter | Default Value | Description |
|-----------|---------------|-------------|
| `bufferSize` | 100KB | Size of each data chunk |
| `heartSpan` | 10 seconds | Heartbeat keep-alive interval |
| Send Method | `SendAsync` | Asynchronous chunk sending |
| Thread Safety | `Interlocked` | Thread-safe counting |

---

## Transfer Protocol Format

FileSocket uses a four-stage handshake protocol:

```
┌──────────────┐     Request      ┌──────────────┐
│    Sender    │ ────────────────► │   Receiver   │
│   (Client)   │                   │   (Server)   │
└──────────────┘                   └──────────────┘
       │                                  │
       │◄───── Allow/Refuse ─────────────│
       │                                  │
       │═══════ Data Chunks ═════════════►│
       │                                  │
       │◄───── Complete ──────────────────│
```

---

## Dependencies

| Package | Version | Description |
|---------|---------|-------------|
| SAEA.Sockets | 26.11.5.1 | Socket communication framework |
| SAEA.Sockets.Pipeline | 26.11.5.1 | Framing, codecs and handler chain |
| SAEA.Common | 26.11.5.1 | Common utilities |

---

## More Resources

- [GitHub Repository](https://github.com/yswenli/SAEA)
- [NuGet Package](https://www.nuget.org/packages/SAEA.FileSocket)
- [Author's Blog](https://www.cnblogs.com/yswenli/)

---

## License

Apache License 2.0