# ![Logo](/logo.jpg) SAEA - High-Performance Network Communication Framework Family 🚀

[![NuGet version (SAEA)](https://img.shields.io/nuget/v/SAEA.Sockets.svg?style=flat-square)](https://www.nuget.org/packages?q=saea)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**English Version** | **[中文版](README.md)**

> A high-performance IOCP network communication framework based on .NET Standard 2.0, providing complete network application solutions.

**SAEA.Socket** is an IOCP high-performance sockets network framework. The Src directory contains its usage scenarios, such as large file transfer, WebSocket client and server, high-performance message queue, RPC, Redis driver, HTTP Server, MQTT, MVC, DNS, message server, etc.

---

## Quick Navigation 🧭

| Section | Content |
|---------|---------|
| [⚡ 30-Second Quick Start](#30-second-quick-start) | Easiest way to get started |
| [🧩 Component Navigation](#component-navigation) | Choose the right component |
| [📐 Project Architecture](#project-architecture) | Overall architecture design |
| [✨ Core Features](#core-features) | Key functionality highlights |
| [📊 Performance Comparison](#performance-comparison) | Comparison with similar frameworks |
| [❓ FAQ](#faq) | Quick answers |
| [🔗 Resource Links](#resource-links) | More learning resources |

---

## 30-Second Quick Start ⚡

### Option 1: Install Core Socket Framework

```bash
dotnet add package SAEA.Sockets
```

```csharp
// Create a TCP server (only 5 lines of code)
var server = SocketFactory.CreateServerSocket(
    SocketOptionBuilder.Instance.SetSocket(SAEASocketType.Tcp).UseIocp().SetPort(39654).Build()
);
server.OnReceive += (id, data) => server.Send(id, data);
server.Start();
```

### Option 2: Choose the Right Component for Your Needs

Select the appropriate component based on your requirements:

| Your Needs | Recommended Component | Install Command |
|------------|----------------------|-----------------|
| Web API Development | **SAEA.MVC** | `dotnet add package SAEA.MVC` |
| WebSocket Real-time Communication | **SAEA.WebSocket** | `dotnet add package SAEA.WebSocket` |
| P2P Direct Communication | **SAEA.P2P** | `dotnet add package SAEA.P2P` |
| Redis Cache Operations | **SAEA.RedisSocket** | `dotnet add package SAEA.RedisSocket` |
| Microservice RPC | **SAEA.RPC** | `dotnet add package SAEA.RPC` |
| IoT Device Communication | **SAEA.MQTT** | `dotnet add package SAEA.MQTT` |
| File Transfer | **SAEA.FileSocket** | `dotnet add package SAEA.FileSocket` |

---

## Component Navigation 🧩

### Core Components

| Component | Function | NuGet | 中文文档 | English Docs | Use Cases |
|-----------|----------|-------|---------|--------------|-----------|
| 🔌 **SAEA.Sockets** | IOCP Socket communication framework | [NuGet](https://www.nuget.org/packages/SAEA.Sockets) | [中文](Src/SAEA.Sockets/README.md) | [English](Src/SAEA.Sockets/README.en.md) | Game servers, real-time communication, IoT |
| 🔧 **SAEA.Common** | Common utility library | [NuGet](https://www.nuget.org/packages/SAEA.Common) | [中文](Src/SAEA.Common/README.md) | [English](Src/SAEA.Common/README.en.md) | Serialization, caching, encryption |

### Web Application Components

| Component | Function | NuGet | 中文文档 | English Docs | Use Cases |
|-----------|----------|-------|---------|--------------|-----------|
| 🌐 **SAEA.Http** | HTTP server | [NuGet](https://www.nuget.org/packages/SAEA.Http) | [中文](Src/SAEA.Http/README.md) | [English](Src/SAEA.Http/README.en.md) | RESTful API, static file serving |
| 🎨 **SAEA.MVC** | MVC Web framework | [NuGet](https://www.nuget.org/packages/SAEA.MVC) | [中文](Src/SAEA.MVC/README.md) | [English](Src/SAEA.MVC/README.en.md) | Web applications, API services |

### Real-time Communication Components

| Component | Function | NuGet | 中文文档 | English Docs | Use Cases |
|-----------|----------|-------|---------|--------------|-----------|
| 💬 **SAEA.WebSocket** | WebSocket server/client | [NuGet](https://www.nuget.org/packages/SAEA.WebSocket) | [中文](Src/SAEA.WebSocket/README.md) | [English](Src/SAEA.WebSocket/README.en.md) | Real-time chat, push notifications |
| 🤖 **SAEA.MQTT** | MQTT protocol implementation | [NuGet](https://www.nuget.org/packages/SAEA.MQTT) | [中文](Src/SAEA.MQTT/README.md) | [English](Src/SAEA.MQTT/README.en.md) | IoT devices, smart home |
| 📨 **SAEA.MessageSocket** | Message server | [NuGet](https://www.nuget.org/packages/SAEA.MessageSocket) | [中文](Src/SAEA.MessageSocket/README.md) | [English](Src/SAEA.MessageSocket/README.en.md) | Instant messaging, online customer service |

### P2P Communication Components

| Component | Function | NuGet | 中文文档 | English Docs | Use Cases |
|-----------|----------|-------|---------|--------------|-----------|
| 🔗 **SAEA.P2P** | P2P direct communication | [NuGet](https://www.nuget.org/packages/SAEA.P2P) | [中文](Src/SAEA.P2P/README.md) | [English](Src/SAEA.P2P/README.en.md) | NAT traversal, LAN discovery, instant messaging, game battles |

### Data & Storage Components

| Component | Function | NuGet | 中文文档 | English Docs | Use Cases |
|-----------|----------|-------|---------|--------------|-----------|
| 📦 **SAEA.RedisSocket** | Redis client | [NuGet](https://www.nuget.org/packages/SAEA.RedisSocket) | [中文](Src/SAEA.RedisSocket/README.md) | [English](Src/SAEA.RedisSocket/README.en.md) | Redis cache, distributed lock |
| 📁 **SAEA.FileSocket** | File transfer component | [NuGet](https://www.nuget.org/packages/SAEA.FileSocket) | [中文](Src/SAEA.FileSocket/README.md) | [English](Src/SAEA.FileSocket/README.en.md) | Large file transfer, resumable upload |
| 🗂️ **SAEA.FTP** | FTP server/client | [NuGet](https://www.nuget.org/packages/SAEA.FTP) | [中文](Src/SAEA.FTP/README.md) | [English](Src/SAEA.FTP/README.en.md) | File servers, FTP clients |

### Distributed System Components

| Component | Function | NuGet | 中文文档 | English Docs | Use Cases |
|-----------|----------|-------|---------|--------------|-----------|
| 🔗 **SAEA.RPC** | RPC remote invocation | [NuGet](https://www.nuget.org/packages/SAEA.RPC) | [中文](Src/SAEA.RPC/README.md) | [English](Src/SAEA.RPC/README.en.md) | Microservice communication, remote calls |
| 📊 **SAEA.QueueSocket** | In-memory message queue | [NuGet](https://www.nuget.org/packages/SAEA.QueueSocket) | [中文](Src/SAEA.QueueSocket/README.md) | [English](Src/SAEA.QueueSocket/README.en.md) | Pub/Sub, event notifications |

### Network Utility Components

| Component | Function | NuGet | 中文文档 | English Docs | Use Cases |
|-----------|----------|-------|---------|--------------|-----------|
| 🌍 **SAEA.DNS** | DNS server/client | [NuGet](https://www.nuget.org/packages/SAEA.DNS) | [中文](Src/SAEA.DNS/README.md) | [English](Src/SAEA.DNS/README.en.md) | DNS proxy, domain resolution |

---

## Project Architecture 📐

### SAEA Component Family Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                 SAEA Component Family Overview                   │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│   ┌───────────── Application Layer ─────────────┐               │
│   │                                              │               │
│   │  ┌─────────┐  ┌──────────┐                  │               │
│   │  │  MVC    │  │ WebSocket│                  │               │
│   │  │(Web)    │  │(Realtime)│                  │               │
│   │  └────┬────┘  └────┬─────┘                  │               │
│   │       │            │                         │               │
│   │  ┌────▼────┐  ┌───▼────┐  ┌─────────┐      │               │
│   │  │  RPC    │  │ MQTT   │  │ Message │      │               │
│   │  │(Remote) │  │ (IoT)  │  │ Socket  │      │               │
│   │  └────┬────┘  └───┬────┘  └────┬────┘      │               │
│   │       │           │            │            │               │
│   │  ┌────▼────┐  ┌───▼────┐       │            │               │
│   │  │  P2P    │  │ Redis  │       │            │               │
│   │  │(NAT)    │  │(Cache) │       │            │               │
│   │  └────┬────┘  └───┬────┘       │            │               │
│   │       │           │            │            │               │
│   └─────────┼───────────┼────────────┼──────────┘               │
│             │           │            │                           │
│   ┌─────────▼───────────▼────────────▼─────────┐               │
│   │           SAEA.Sockets (Core Layer)          │               │
│   │        IOCP High-Performance Socket          │               │
│   │  ┌──────────┐  ┌──────────┐  ┌──────┐      │               │
│   │  │IocpSocket│  │StreamSock│  │UdpSock│      │               │
│   │  │ (IOCP)   │  │(Stream)  │  │ (UDP) │      │               │
│   │  └──────────┘  └──────────┘  └──────┘      │               │
│   │  ┌──────────────────────────────────┐       │               │
│   │  │  BufferPool │ Session │  Coder   │       │               │
│   │  │  (Memory)   │(Session)│(Codec)   │       │               │
│   │  └──────────────────────────────────┘       │               │
│   └──────────────────┬────────────────────┘                   │
│                      │                                          │
│   ┌──────────────────▼────────────────────┐                   │
│   │         SAEA.Common (Base Layer)      │                   │
│   │    Common Utilities (Serialize/Cache/Encrypt)│             │
│   │  ┌────────┐  ┌───────┐  ┌───────┐    │                   │
│   │  │Serialize│  │ Cache │  │Encrypt│    │                   │
│   │  │  (Ser)  │  │(Cache)│  │(Enc)  │    │                   │
│   │  └────────┘  └───────┘  └───────┘    │                   │
│   └───────────────────────────────────────┘                   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Data Flow Architecture

```
Client Request Flow:

┌─────────┐      ┌──────────┐      ┌──────────┐
│ Client  │─────►│ Sockets  │─────►│  Coder   │
│(TCP/UDP)│      │IOCP Recv │      │  Decode  │
└─────────┘      └──────────┘      └──────────┘
                                          │
                                          ▼
                     ┌────────────────────────────────┐
                     │    Application Layer (Business) │
                     │  MVC / RPC / WebSocket / MQTT    │
                     └────────────────────────────────┘
                                          │
                                          ▼
                     ┌──────────┐      ┌──────────┐
                     │  Coder   │─────►│ Sockets  │─────► Client
                     │  Encode  │      │IOCP Send │
                     └──────────┘      └──────────┘
```

---

## Core Features ✨

| Feature | Description |
|---------|-------------|
| 🚀 **High-Performance IOCP** | Windows completion port technology, supporting tens of thousands of concurrent connections |
| 📦 **Memory Pool Optimization** | BufferManager, UserTokenPool reduce GC pressure |
| 🔒 **SSL/TLS Encryption** | Stream mode supports secure connections |
| 🌐 **IPv6 Support** | Full compatibility with IPv6 protocol |
| 🔄 **Session Management** | SessionManager automatically manages connection sessions |
| 🛠️ **Custom Protocol** | ICoder interface supports flexible protocol encoding/decoding |
| 🔗 **Fluent Configuration** | SocketOptionBuilder is concise and easy to use |
| 📝 **Complete Components** | MVC, WebSocket, RPC, Redis, MQTT, etc. covering all scenarios |

---

## Performance Comparison 📊

### SAEA.Sockets vs Traditional Socket

| Metric | SAEA.Sockets | Traditional Socket | Improvement |
|--------|--------------|-------------------|-------------|
| **Concurrent Connections** | 10,000+ | ~1,000 | **10x** |
| **CPU Utilization** | ~85% | ~30% | **Efficient** |
| **Memory Usage** | Pooled reuse | Frequent allocation | **Low GC** |
| **Latency** | ~1ms | ~10ms | **Low Latency** |

### SAEA.RPC vs HTTP RPC

| Feature | SAEA.RPC | HTTP RPC |
|---------|----------|----------|
| **Serialization** | Protobuf | JSON |
| **Transport Protocol** | TCP | HTTP |
| **Connection Mode** | Persistent | Short-lived |
| **Concurrency Model** | IOCP | Blocking |
| **Performance** | **High** | Medium |

---

## FAQ ❓

### Q1: What projects is SAEA suitable for?

**A**: SAEA is suitable for the following scenarios:
- 🎮 Game servers (real-time battles, state synchronization)
- 📊 Real-time data push (stock quotes, sports scores)
- 🤖 IoT device communication (sensor data reporting)
- 💬 Instant messaging (private chat, group chat, customer service systems)
- 🔗 Microservice communication (RPC remote calls)
- 📁 Large file transfer (resumable upload, high-speed transfer)
- 🌐 P2P direct communication (NAT traversal, LAN discovery)

### Q2: How to choose the right component?

**A**: Choose based on your needs:

```
Your Needs                        Recommended Component
─────────────────────────────────────────────
Web API Development         →  SAEA.MVC
Real-time Chat/Push         →  SAEA.WebSocket
P2P Direct Communication    →  SAEA.P2P
IoT Device Communication    →  SAEA.MQTT
Redis Cache Operations      →  SAEA.RedisSocket
Microservice RPC            →  SAEA.RPC
File Transfer               →  SAEA.FileSocket
Custom Protocol             →  SAEA.Sockets
```

### Q3: Is cross-platform supported?

**A**: Based on .NET Standard 2.0, supports:
- ✅ Windows (IOCP best performance)
- ✅ Linux (Async Socket)
- ✅ macOS

### Q4: How to install via NuGet?

**A**: Search for "SAEA" or install directly:

```bash
# Package Manager
Install-Package SAEA.Sockets -Version 7.26.2.2

# .NET CLI
dotnet add package SAEA.Sockets --version 7.26.2.2
```

[NuGet Link](https://www.nuget.org/packages?q=saea)

### Q5: How to get started quickly?

**A**: 3-step quick start:
1. Install the NuGet package for the corresponding component
2. Read the "30-Second Quick Start" section in the component's README
3. Check the complete sample code

Each component has detailed usage examples and comments.

### Q6: Are there sample projects?

**A**: The Src directory contains multiple test projects demonstrating real-world usage scenarios:
- `SAEA.SocketsTest` - Socket usage examples
- `SAEA.WebSocketTest` - WebSocket examples
- `SAEA.MVCTest` - MVC Web application examples
- `SAEA.RedisTest` - Redis operation examples
- `SAEA.RPCTest` - RPC call examples
- `SAEA.P2PTest` - P2P NAT traversal examples

---

## Screenshots 🖼️

<img src="https://github.com/yswenli/SAEA/blob/master/FileSocketTest.png?raw=true" width="400"/>
<img src="https://github.com/yswenli/SAEA/blob/master/QueueSocketTest.png?raw=true" width="400"/>
<img src="https://github.com/yswenli/SAEA/blob/master/SAEA.MVC.png?raw=true" width="400"/>
<img src="https://github.com/yswenli/SAEA/blob/master/SAEA.MVCTest.png?raw=true" width="400"/>
<img src="https://github.com/yswenli/SAEA/blob/master/SAEA.RedisTest.png?raw=true" width="400"/>
<img src="https://github.com/yswenli/SAEA/blob/master/SAEA.WebAPITest.png?raw=true" width="400"/>
<img src="https://github.com/yswenli/SAEA/blob/master/WebsocketTest.png?raw=true" width="400"/>
<img src="https://github.com/yswenli/SAEA/blob/master/redis%20cluster%20test.png?raw=true" width="400"/>
<img src="https://github.com/yswenli/SAEA/blob/master/rpc.png?raw=true" width="400"/>

---

## Resource Links 🔗

### Official Resources

- [GitHub Repository](https://github.com/yswenli/SAEA)
- [NuGet Package List](https://www.nuget.org/packages?q=saea)
- [Author's Blog](https://www.cnblogs.com/yswenli/)

### Related Projects

- [WebRedisManager](https://github.com/yswenli/WebRedisManager) - Redis management tool based on SAEA.RedisSocket
- [SAEA.Rested](https://github.com/yswenli/SAEA.Rested) - REST API example based on SAEA.MVC
- [GFF](https://github.com/yswenli/GFF) - QQ-like communication program based on SAEA

### Support

- QQ Group: 788260487
- Issues: [GitHub Issues](https://github.com/yswenli/SAEA/issues)

---

## License

Apache License 2.0