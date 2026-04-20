# SAEA.FileSocket - High-Performance File Transfer Component 📁

[![NuGet version](https://img.shields.io/nuget/v/SAEA.FileSocket.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.FileSocket)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**English Version** | **[中文版](README.md)**

> A high-performance file transfer component based on SAEA.Sockets, using IOCP completion port technology, supporting large file chunked transfer, resumable upload/download, and real-time progress monitoring.

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
| 🚀 **IOCP High Performance** | Based on SAEA.Sockets completion port technology | Supports high-concurrency file transfers |
| 📦 **Large File Support** | Chunked transfer mechanism | Supports TB-level ultra-large files |
| 🔄 **Resumable Transfer** | offset parameter to resume from specified position | Recoverable from network interruptions |
| 📊 **Real-time Progress** | Send/receive speed, data statistics | Visual monitoring of transfer status |
| 💓 **Heartbeat Keep-alive** | 10-second heartbeat interval maintains connection | Automatic connection status detection |
| 📡 **Event-driven** | Comprehensive event callback mechanism | Flexible handling of transfer states |
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
│  │              SAEA.Sockets (IOCP Layer)               │  │
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
  │                              3. OnRequested event
  │                                 ├── return true (allow)
  │                                 └── return false (reject)
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
  │                              7. OnReceiveEnd event
  │                                 └── Trigger receive complete callback
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
│  │ Trigger: OnReceiveEnd event                          │   │
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
| 📁 **LAN File Sharing** | Fast transfer of large files | IOCP high performance, 100MB/s transfer speed |
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
| **Transfer Speed** | ~100MB/s (LAN) | ~30MB/s | **3x improvement** |
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

**A**: Use the `OnRequested` event of the `Server` class:

```csharp
var server = new Server(port: 39654);

server.OnRequested += (ID, fileName, length) => 
{
    // Decide whether to accept based on file name or size
    if (length > 1024 * 1024 * 1024)  // Exceeds 1GB
    {
        Console.WriteLine($"Reject large file: {fileName}");
        return false;  // Reject
    }
    return true;  // Allow
};

server.Start();
```

### Q5: Does it support transferring multiple files simultaneously?

**A**: Yes. Create multiple `Client` instances or use the same `FileTransfer` to send multiple files:

```csharp
// Method 1: Use FileTransfer for sequential sending
fileTransfer.SendFile(file1, targetIp);
fileTransfer.SendFile(file2, targetIp);

// Method 2: Create multiple Clients for concurrent sending
var client1 = new Client();
var client2 = new Client();
client1.Connect(ip, port);
client2.Connect(ip, port);
client1.SendFile(file1, callback);
client2.SendFile(file2, callback);
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

**A**: Specify when constructing `FileTransfer` or `Server`:

```csharp
// FileTransfer method
var fileTransfer = new FileTransfer(@"C:\CustomPath", port: 39654);

// Server method
var server = new Server(port: 39654, filePath: @"C:\CustomPath");
```

---

## Core Classes 🔧

| Class Name | Description |
|------------|-------------|
| `FileTransfer` | File transfer main coordinator class, supports both sending and receiving |
| `Client` | File sending client, used to actively send files |
| `Server` | File receiving server, used to listen and receive files |
| `FileMessage` | File message data model, defines transfer protocol |
| `FileMessageCoder` | File message encoder/decoder |

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

var server = new Server(port: 39654, bufferSize: 100 * 1024);

server.OnRequested += (ID, fileName, length) => 
{
    Console.WriteLine($"Request to receive file: {fileName}, Size: {length / 1024 / 1024}MB");
    return true;
};

server.OnFile += (userToken, content) => 
{
    // Custom handling of received data chunks
};

server.OnError += (ID, ex) => 
    Console.WriteLine($"Transfer error: {ex.Message}");

server.Start();
```

### Client-side Sending (with Callback)

```csharp
using SAEA.FileSocket;

var client = new Client(bufferSize: 100 * 1024);

client.Connect("192.168.1.100", 39654);

client.SendFile(@"C:\Data\document.pdf", (success) => 
{
    if (success)
        Console.WriteLine("Send successful");
    else
        Console.WriteLine("Send failed");
});

// Resumable transfer
long offset = 1024 * 1024 * 100;
client.SendFile(@"C:\Data\large_file.dat", offset, (success) => 
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
| SAEA.Sockets | 7.26.2.2 | IOCP communication framework |
| SAEA.Common | 7.26.2.2 | Common utilities |

---

## More Resources

- [GitHub Repository](https://github.com/yswenli/SAEA)
- [NuGet Package](https://www.nuget.org/packages/SAEA.FileSocket)
- [Author's Blog](https://www.cnblogs.com/yswenli/)

---

## License

Apache License 2.0