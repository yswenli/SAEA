# SAEA.P2P - P2P Communication Component

[![NuGet version](https://img.shields.io/nuget/v/SAEA.P2P.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.P2P)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**[中文版](README.md)** | **English Version**

> High-performance P2P communication component based on SAEA.Sockets, supporting NAT traversal, relay fallback, local discovery, authentication and encryption. Make peer-to-peer communication simpler and more efficient!

---

## Quick Navigation

| Section | Content |
|---------|---------|
| [Quick Start in 30 Seconds](#quick-start-in-30-seconds) | Simplest way to get started |
| [Scenario Examples](#scenario-examples) | Real-world code examples |
| [Core Features](#core-features) | Main capabilities |
| [Architecture Design](#architecture-design) | Component relationships |
| [Builder Configuration Guide](#builder-configuration-guide) | Complete chain configuration |
| [NAT Traversal Principles](#nat-traversal-principles) | Hole punching explained |
| [Error Codes Reference](#error-codes-reference) | Error code lookup table |
| [Core Classes Overview](#core-classes-overview) | Main classes explained |
| [Protocol Format](#protocol-format) | Message protocol details |
| [Dependencies](#dependencies) | Required packages |

---

## Quick Start in 30 Seconds

### Method 1: Minimal Setup (Recommended for Beginners)

```bash
dotnet add package SAEA.P2P
```

**Start signaling server (3 lines):**
```csharp
using SAEA.P2P;

var server = P2PQuick.Server(39654);
server.Start();
Console.WriteLine("P2P server started!");
```

**Create client connection (5 lines):**
```csharp
using SAEA.P2P;
using System.Text;

var client = P2PQuick.Client("127.0.0.1", 39654, "my-node");
client.OnMessageReceived += (peerId, data) => Console.WriteLine($"Received: {Encoding.UTF8.GetString(data)}");
await client.ConnectAsync();
```

**That's it!** You've implemented a P2P system with NAT traversal and relay fallback.

### Method 2: Local Network Direct Connection (No Server Required)

```csharp
using SAEA.P2P;

// No signaling server needed, auto-discover on LAN
var client = P2PQuick.LocalNet("local-node");
client.OnLocalNodeDiscovered += (node) => Console.WriteLine($"Found node: {node.NodeId} at {node.LocalAddress}");
await client.ConnectAsync();
```

---

## Scenario Examples

### Scenario 1: Instant Messaging Chat

```csharp
using SAEA.P2P;
using SAEA.P2P.Core;
using SAEA.P2P.Builder;
using System.Text;

// Create chat client
var options = new P2PClientBuilder()
    .SetServer("chat.example.com", 39654)
    .SetNodeId($"user-{Guid.NewGuid():N}")
    .EnableHolePunch()          // NAT traversal
    .EnableRelay()              // Relay fallback on failure
    .EnableEncryption("chat-secret-key-16")  // Encrypt chat content
    .EnableLogging()
    .Build();

var client = new P2PClient(options);

// Receive messages
client.OnMessageReceived += (peerId, data) =>
{
    var message = Encoding.UTF8.GetString(data);
    Console.WriteLine($"[{peerId}] {message}");
};

// Notify when connected to server
client.OnServerConnected += () => Console.WriteLine("Connected to chat server");

await client.ConnectAsync();

// Send message
await client.ConnectToPeerAsync("user-target");
client.Send("user-target", Encoding.UTF8.GetBytes("Hello!"));

// Get online users list
var onlineUsers = client.KnownNodes.Keys;
Console.WriteLine($"Online users: {string.Join(", ", onlineUsers)}");
```

### Scenario 2: LAN File Sharing

```csharp
using SAEA.P2P;
using SAEA.P2P.Builder;
using SAEA.P2P.Core;

// Create LAN discovery client
var options = new P2PClientBuilder()
    .SetNodeId($"file-server-{Environment.MachineName}")
    .EnableLocalDiscovery(39655, "224.0.0.250")
    .SetDiscoveryInterval(3000)  // Broadcast every 3 seconds
    .EnableLogging()
    .Build();

var client = new P2PClient(options);

// Handle discovered nodes
client.OnLocalNodeDiscovered += (node) =>
{
    Console.WriteLine($"Found file node: {node.NodeId}");
    // Connect and request files
};

await client.ConnectAsync();

// View discovered nodes
foreach (var node in client.KnownNodes)
{
    Console.WriteLine($"Node: {node.Key} - {node.Value.LastActiveTime}");
}
```

### Scenario 3: IoT Device Communication

```csharp
using SAEA.P2P;
using SAEA.P2P.Builder;
using SAEA.P2P.Core;
using SAEA.P2P.NAT;

// IoT device client (stability priority)
var options = new P2PClientBuilder()
    .SetServer("iot-hub.example.com", 39654)
    .SetNodeId($"device-{Environment.MachineName}")
    .SetNodeIdPassword("device-secret")
    .EnableHolePunch(HolePunchStrategy.PreferRelay)  // Stability first
    .EnableRelay()
    .SetFreeTime(300000)  // 5-minute heartbeat
    .EnableLogging()
    .Build();

var client = new P2PClient(options);

// Monitor state changes
client.OnStateChanged += (old, new_) => 
    Console.WriteLine($"State: {old} -> {new_}");

// Handle errors
client.OnError += (code, msg) => 
    Console.WriteLine($"Error [{code}]: {msg}");

await client.ConnectAsync();

// Send sensor data
var sensorData = new { Temperature = 25.5, Humidity = 60 };
var json = System.Text.Json.JsonSerializer.Serialize(sensorData);
client.Send("control-center", Encoding.UTF8.GetBytes(json));
```

### Scenario 4: Game Battle Synchronization

```csharp
using SAEA.P2P;
using SAEA.P2P.Builder;
using SAEA.P2P.Core;
using SAEA.P2P.NAT;

// Game client (low latency priority)
var options = new P2PClientBuilder()
    .SetServer("game.example.com", 39654)
    .SetNodeId($"player-{Guid.NewGuid():N}")
    .EnableHolePunch(HolePunchStrategy.DirectOnly)  // Direct only, lowest latency
    .SetHolePunchTimeout(5000)
    .SetHolePunchRetry(5)
    .EnableLogging()
    .Build();

var client = new P2PClient(options);

// Opponent connected successfully
client.OnPeerConnected += (peerId, channelType) =>
{
    Console.WriteLine($"Opponent connected: {peerId} ({channelType})");
    if (channelType == ChannelType.Direct)
        Console.WriteLine("Direct connection mode - lowest latency!");
};

await client.ConnectAsync();
await client.ConnectToPeerAsync("player-opponent");

// Send game state
var gameState = new { X = 100, Y = 200, Action = "jump" };
client.Send("player-opponent", Encoding.UTF8.GetBytes(
    System.Text.Json.JsonSerializer.Serialize(gameState)));
```

### Scenario 5: Distributed Computing Nodes

```csharp
using SAEA.P2P;
using SAEA.P2P.Builder;
using SAEA.P2P.Core;

// Compute node (full configuration)
var options = new P2PClientBuilder()
    .SetServer("compute-hub.example.com", 39654)
    .SetNodeId($"compute-{Environment.MachineName}-{Guid.NewGuid():N}")
    .SetNodeIdPassword("compute-network-secret")
    .EnableHolePunch()
    .EnableRelay(60000, 1024 * 1024 * 1024)  // 1GB relay quota
    .EnableEncryption("compute-network-key-32")  // AES-256
    .EnableTls()              // TLS encrypted signaling channel
    .SetTimeout(10000)
    .EnableLogging(1, "logs/compute.log")  // Debug level log
    .Build();

var client = new P2PClient(options);

// Node list update
client.OnServerConnected += async () =>
{
    // Get all compute nodes
    var nodes = client.KnownNodes;
    Console.WriteLine($"Available compute nodes: {nodes.Count}");
    
    // Connect to other compute nodes
    foreach (var node in nodes.Take(3))
    {
        await client.ConnectToPeerAsync(node.Key);
    }
};

await client.ConnectAsync();

// Distribute compute task
var task = new { TaskId = Guid.NewGuid(), Data = "compute-payload" };
client.Send("compute-node-1", Encoding.UTF8.GetBytes(
    System.Text.Json.JsonSerializer.Serialize(task)));
```

### Scenario 6: Enterprise Signaling Server

```csharp
using SAEA.P2P;
using SAEA.P2P.Builder;
using SAEA.P2P.Core;

// Enterprise server configuration
var options = new P2PServerBuilder()
    .SetPort(39654)
    .SetIP("0.0.0.0")
    .SetMaxNodes(5000)         // Max 5000 nodes
    .EnableRelay(500, 1024 * 1024 * 1024 * 10)  // 500 relays, 10GB quota
    .EnableTls("server.pfx", "tls-password")     // TLS encryption
    .SetFreeTime(120000)      // 2-minute heartbeat
    .EnableLogging(2, "logs/server.log")
    .Build();

var server = new P2PServer(options);

// Monitor node activity
server.OnNodeRegistered += (nodeId, endpoint) =>
    Console.WriteLine($"[+] Node online: {nodeId} ({endpoint})");

server.OnNodeUnregistered += (nodeId) =>
    Console.WriteLine($"[-] Node offline: {nodeId}");

server.OnRelayStarted += (source, target) =>
    Console.WriteLine($"[RELAY] {source} -> {target}");

server.OnError += (id, error) =>
    Console.WriteLine($"[ERROR] {id}: {error}");

server.Start();
Console.WriteLine($"Enterprise P2P server started on port: {server.Port}");
```

---

## Core Features

| Feature | Description | Advantage |
|---------|-------------|-----------|
| **NAT Traversal** | UDP hole punching direct connection | No public server relay needed, 90% latency reduction |
| **Relay Fallback** | Auto relay on traversal failure | 100% connectivity guarantee, never disconnect |
| **LAN Discovery** | UDP multicast auto-discovery | Zero config, zero-cost local communication |
| **Authentication** | Challenge-response auth | SHA256 verification, anti-fake |
| **AES Encryption** | Configurable key encryption | 128/192/256-bit options |
| **Builder Config** | Chain configuration | 25+ config methods, flexible combination |
| **Event Driven** | Complete event system | Async-friendly, easy integration |
| **Log Tracing** | Node action logging | Problem diagnosis, behavior audit |

### Connection Strategy Comparison

| Strategy | Behavior | Latency | Connectivity | Use Case |
|----------|----------|---------|--------------|----------|
| `PreferDirect` | Punch first, relay on fail | Low | High | **Default recommendation** |
| `PreferRelay` | Quick try then relay | Medium | High | Stability priority |
| `DirectOnly` | Only punch, fail throws error | Low | Medium | LAN/controlled environment |
| `RelayOnly` | Direct relay | High | High | Difficult NAT environment |

---

## Architecture Design

### Module Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                       SAEA.P2P Architecture                      │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────── Quick Entry (P2PQuick) ──────────┐               │
│  │                                              │               │
│  │  Client() / Server() / LocalNet()           │               │
│  │  ClientFull() / Server(custom)              │               │
│  │                                              │               │
│  └─────────────────────────────────────────────┘               │
│                                                                 │
│  ┌─────────── Builder Configuration Layer ────┐               │
│  │                                              │               │
│  │  P2PClientBuilder   P2PServerBuilder        │               │
│  │  25+ chain methods  13+ chain methods       │               │
│  │                                              │               │
│  └─────────────────────────────────────────────┘               │
│                                                                 │
│  ┌─────────── Core Layer ─────────────────────┐               │
│  │                                              │               │
│  │  ┌─────────────┐     ┌─────────────┐       │               │
│  │  │  P2PClient  │     │  P2PServer  │       │               │
│  │  │  (Client)   │     │  (Server)   │       │               │
│  │  └──────┬──────┘     └──────┬──────┘       │               │
│  │         │                   │               │               │
│  │  ┌──────┴───────────────────┴──────┐       │               │
│  │  │        PeerSession / NodeInfo    │       │               │
│  │  └─────────────────────────────────┘       │               │
│  │                                              │               │
│  └─────────────────────────────────────────────┘               │
│                                                                 │
│  ┌─────────── Feature Module Layer ───────────┐               │
│  │                                              │               │
│  │  ┌────────────┐  ┌────────────┐            │               │
│  │  │    NAT     │  │  Security  │            │               │
│  │  │ HolePuncher│  │AuthManager │            │               │
│  │  │NATDetector │  │CryptoService│            │               │
│  │  └─────┬──────┘  └─────┬──────┘            │               │
│  │        │               │                     │               │
│  │  ┌─────┴──────┐  ┌─────┴──────┐            │               │
│  │  │  Discovery │  │   Relay    │            │               │
│  │  │LocalDiscov │  │RelayManager│            │               │
│  │  └─────┬──────┘  └─────┬──────┘            │               │
│  │        │               │                     │               │
│  │  ┌─────┴───────────────┴──────┐            │               │
│  │  │      Channel (UDP/TCP)     │            │               │
│  │  └───────────────────────────┘             │               │
│  │                                              │               │
│  └─────────────────────────────────────────────┘               │
│                                                                 │
│  ┌─────────── Protocol Layer ─────────────────┐               │
│  │                                              │               │
│  │  P2PCoder (Encode/Decode)  P2PProtocol      │               │
│  │  P2PMessageType (Enum)                      │               │
│  │                                              │               │
│  └─────────────────────────────────────────────┘               │
│                                                                 │
│  ┌─────────── Transport Layer (SAEA.Sockets) ─┐               │
│  │                                              │               │
│  │  IocpServerSocket   IocpClientSocket        │               │
│  │  UdpServerSocket   UdpClientSocket          │               │
│  │                                              │               │
│  └─────────────────────────────────────────────┘               │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### NAT Traversal Flow Diagram

```
     NodeA                SignalServer                NodeB
        │                      │                       │
        │──── 1. Register ────>│                       │
        │<─── RegisterAck ─────│                       │
        │                      │                       │
        │──── 2. NatProbe ────>│                       │
        │<─── NatProbeAck ─────│                       │
        │    (Get external IP) │                       │
        │                      │                       │
        │──── 3. PunchRequest ─>│──── PunchReady ────> │
        │<─── PunchReady ──────│                       │
        │    (Contains B's IP) │                       │
        │                      │                       │
        │──── 4. UDP PunchSync (multiple) ────────────>│
        │<────────────────────── PunchSync (multiple)─ │
        │                      │                       │
        │──── 5. PunchAck ────────────────────────────>│
        │<────────────────────── PunchAck ──────────── │
        │                      │                       │
        │ ═══════ 6. UDP Direct Channel Established ═══│
        │                      │                       │
        │                      │                       │
        │    ┌─ Punch Success ──│──────────────────────>│
        │    │                 │                       │
        │    │ Punch Failed    │                       │
        │    └─ 7. RelayRequest ──────────────────────>│
        │       <── RelayAck ──│                       │
        │                      │                       │
        │<─── 8. RelayData ────│─── RelayData ────────>│
        │                      │                       │
```

---

## Builder Configuration Guide

### P2PClientBuilder Complete Configuration List

```csharp
var options = new P2PClientBuilder()
    // === Basic Configuration ===
    .SetServer("127.0.0.1", 39654)       // Signaling server address (domain/IP)
    .SetServer(new IPEndPoint(...))       // Or use IPEndPoint
    .SetNodeId("unique-node-id")          // Node unique identifier
    .SetNodeIdPassword("auth-secret")     // Authentication password
    .SetTimeout(5000)                     // Connection timeout(ms)
    
    // === NAT Traversal Configuration ===
    .EnableHolePunch()                    // Enable punching (default PreferDirect)
    .EnableHolePunch(HolePunchStrategy.PreferRelay)  // Specify strategy
    .SetHolePunchTimeout(10000)           // Punch timeout(ms)
    .SetHolePunchRetry(5)                 // Retry count
    .SetNATType(NATType.FullCone)         // Force specify NAT type
    
    // === Relay Configuration ===
    .EnableRelay()                        // Enable relay fallback
    .EnableRelay(60000, 10 * 1024 * 1024) // Timeout + quota
    
    // === LAN Discovery Configuration ===
    .EnableLocalDiscovery()               // Default port and multicast
    .EnableLocalDiscovery(39655)          // Custom port
    .EnableLocalDiscovery(39655, "224.0.0.250")  // Port + multicast address
    .SetDiscoveryInterval(5000)           // Discovery broadcast interval
    
    // === Security Configuration ===
    .EnableEncryption()                   // Use negotiated key
    .EnableEncryption("aes-key-12345678") // Custom key (16/24/32 bytes)
    .EnableTls()                          // TLS encrypted signaling channel
    
    // === Heartbeat Configuration ===
    .SetFreeTime(180000)                  // Signaling heartbeat interval
    .SetPeerHeartbeat(30000)              // Peer heartbeat interval
    .SetPeerHeartbeatRetry(3)             // Heartbeat retry count
    
    // === Logging Configuration ===
    .EnableLogging()                      // Default Info level
    .EnableLogging(1)                     // Debug level
    .EnableLogging(0, "logs/p2p.log")     // Trace level + custom path
    .SetLogToConsole(false)               // Disable console output
    
    .Build();                             // Build and validate
```

### P2PServerBuilder Complete Configuration List

```csharp
var options = new P2PServerBuilder()
    // === Basic Configuration ===
    .SetPort(39654)                       // Listen port
    .SetIP("0.0.0.0")                     // Bind address
    .SetMaxNodes(1000)                    // Max nodes
    
    // === Relay Configuration ===
    .EnableRelay()                        // Enable relay
    .EnableRelay(100, 1024 * 1024 * 1024) // Max relays + total quota
    
    // === Security Configuration ===
    .EnableTls("server.pfx", "password")  // TLS encryption
    
    // === Heartbeat Configuration ===
    .SetFreeTime(180000)                  // Heartbeat interval
    
    // === Logging Configuration ===
    .EnableLogging()
    .EnableLogging(2, "logs/server.log")
    
    .Build();
```

---

## NAT Traversal Principles

### NAT Type Classification

| NAT Type | Characteristic | Traversal Difficulty | Traversal Method | Success Rate |
|----------|----------------|----------------------|------------------|--------------|
| **Full Cone** | Any external address can send | Low | Direct PunchSync | ~99% |
| **Restricted Cone** | Must receive before sending | Medium | Both send simultaneously | ~90% |
| **Port Restricted** | Exact port match required | Medium-High | Precise timing coordination | ~80% |
| **Symmetric** | New port per connection | High | Fallback to relay | ~30% |

### Hole Punching Process Details

**Step 1: Registration & Address Detection**
- Client registers with server, sends NatProbe
- Server returns client's external address (IP:Port)
- Client records its public address

**Step 2: Punch Coordination**
- A sends PunchRequest(B) to server
- Server queries B's address, sends PunchReady to both parties
- PunchReady contains peer's external address and NAT type

**Step 3: UDP Hole Punch Execution**
- A and B simultaneously send PunchSync to peer's external address
- Both keep sending until receiving peer's PunchSync
- Upon receipt, send PunchAck confirmation

**Step 4: Direct Connection or Relay Fallback**
- Successfully received PunchAck → Direct channel established
- Timeout without receipt → Auto RelayRequest fallback

---

## Error Codes Reference

### Configuration Errors (EPxx)

| Code | Description | Suggestion |
|------|-------------|------------|
| EP01 | Server address not configured | Call SetServer() |
| EP02 | Server address format invalid | Use valid domain/IP |
| EP03 | Port range invalid | Use 1-65535 |
| EP04 | Node ID not set | Call SetNodeId() |
| EP05 | Node ID format invalid | Length ≤64, alphanumeric |
| EP06 | Encryption key not set | EnableEncryption(key) |
| EP07 | Encryption key length invalid | 16/24/32 bytes |
| EP08 | Timeout value invalid | Set reasonable timeout |
| EP09 | Builder config incomplete | Check required settings |

### Data Errors (EDxx)

| Code | Description | Suggestion |
|------|-------------|------------|
| ED01 | Send data empty | Provide valid data |
| ED02 | Send data too long | UDP ≤ 64KB, or fragment |
| ED03 | Message decode failed | Check encoding method |
| ED06 | Target node not found | Establish connection first |

### Operation Errors (EOxx)

| Code | Description | Suggestion |
|------|-------------|------------|
| EO01 | Send without connection | Call Connect() first |
| EO02 | Duplicate connection | Check Connected state |
| EO03 | Connection timeout | Check network/increase timeout |

### NAT/Punch Errors (EHxx)

| Code | Description | Suggestion |
|------|-------------|------------|
| EH01 | NAT不支持穿透 | Auto fallback to relay |
| EH02 | Punch timeout | Increase timeout/retry |

### Relay Errors (ERxx)

| Code | Description | Suggestion |
|------|-------------|------------|
| ER01 | Relay not enabled | EnableRelay() |
| ER02 | Relay server disconnected | Check server status |
| ER04 | Relay quota exceeded | Increase quota or upgrade |

### Auth/Encryption Errors (EAxx/EExx)

| Code | Description | Suggestion |
|------|-------------|------------|
| EA01 | Auth failed | Check NodeIdPassword |
| EA03 | Node ID duplicate | Change to unique ID |
| EE01 | Encryption init failed | Check key format |
| EE02 | Decryption failed | Check key match |

---

## Core Classes Overview

### Core Components

| Class | Namespace | Description |
|-------|-----------|-------------|
| `P2PClient` | SAEA.P2P.Core | Client core class |
| `P2PServer` | SAEA.P2P.Core | Signaling server core class |
| `P2PQuick` | SAEA.P2P | Quick static methods |
| `P2PClientBuilder` | SAEA.P2P.Builder | Client config builder |
| `P2PServerBuilder` | SAEA.P2P.Builder | Server config builder |

### Protocol & Encoding

| Class | Description |
|-------|-------------|
| `P2PProtocol` | Protocol message class, inherits BaseSocketProtocal |
| `P2PCoder` | Encoder/decoder, inherits BaseCoder |
| `P2PMessageType` | Message type enum |

### NAT Traversal

| Class | Description |
|-------|-------------|
| `HolePuncher` | UDP hole punch executor |
| `NATDetector` | NAT type detector |
| `NATType` | NAT type enum |
| `HolePunchStrategy` | Punch strategy enum |

### Relay & Discovery

| Class | Description |
|-------|-------------|
| `RelayManager` | Relay session management |
| `RelaySession` | Relay session model |
| `LocalDiscovery` | LAN discovery service |
| `DiscoveredNode` | Discovered node info |

### Security Modules

| Class | Description |
|-------|-------------|
| `CryptoService` | AES encryption service |
| `AuthManager` | Authentication manager |
| `AuthChallenge` | Auth challenge model |
| `KeyExchange` | Key exchange |

### Channels & Models

| Class | Description |
|-------|-------------|
| `UDPChannel` | UDP channel wrapper |
| `TCPChannel` | TCP channel wrapper |
| `PeerSession` | Peer session model |
| `NodeInfo` | Node info model |

---

## Protocol Format

### Message Structure

```
| 8-byte length | 1-byte type | N-byte content |
```

Reuses SAEA.Sockets BaseSocketProtocal format for compatibility.

### Message Type Definitions

| Type Value | Name | Direction | Description |
|------------|------|-----------|-------------|
| 0x10 | Register | C→S | Node registration request |
| 0x11 | RegisterAck | S→C | Registration response |
| 0x12 | Unregister | C→S | Node unregister |
| 0x13 | NodeList | S→C | Node list push |
| 0x20 | NatProbe | C→S | NAT probe request |
| 0x21 | NatProbeAck | S→C | NAT probe response |
| 0x30 | PunchRequest | C→S | Punch request |
| 0x31 | PunchReady | S→C | Punch ready notification |
| 0x33 | PunchSync | C→C | Punch sync packet (UDP) |
| 0x34 | PunchAck | C→C | Punch success confirmation |
| 0x40 | RelayRequest | C→S | Relay request |
| 0x41 | RelayAck | S→C | Relay response |
| 0x42 | RelayData | S→C | Relay data forwarding |
| 0x50 | LocalDiscover | C→LAN | LAN discovery broadcast |
| 0x51 | LocalDiscoverAck | C→LAN | Discovery response |
| 0x60 | AuthChallenge | S→C | Auth challenge |
| 0x61 | AuthResponse | C→S | Auth response |
| 0x62 | AuthSuccess | S→C | Auth success |
| 0x70 | Heartbeat | C→S | Heartbeat request |
| 0x71 | HeartbeatAck | S→C | Heartbeat response |
| 0x80 | UserData | C→C | User data |
| 0x81 | UserDataAck | C→C | Data delivery confirmation |

---

## Dependencies

| Package | Version | Description |
|---------|---------|-------------|
| SAEA.Sockets | 7.26+ | IOCP high-performance Socket framework |
| SAEA.Common | 7.26+ | AESHelper, LogHelper, SerializeHelper |

---

## More Resources

- [GitHub Repository](https://github.com/yswenli/SAEA)
- [NuGet Package List](https://www.nuget.org/packages?q=saea)
- [Author Blog](https://www.cnblogs.com/yswenli/)
- [SAEA.Sockets Documentation](../SAEA.Sockets/README.en.md)
- [SAEA.Common Documentation](../SAEA.Common/README.md)

---

## License

Apache License 2.0