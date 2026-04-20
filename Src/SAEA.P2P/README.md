# SAEA.P2P - P2P通信组件

[![NuGet version](https://img.shields.io/nuget/v/SAEA.P2P.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.P2P)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

> 基于 SAEA.Sockets 的 P2P 通信组件，支持NAT穿透、中继降级、局域网发现、身份认证加密。

## 快速导航

| 章节 | 内容 |
|------|------|
| [30秒快速开始](#30秒快速开始) | 最简单的上手示例 |
| [核心特性](#核心特性) | 组件的主要功能 |
| [架构设计](#架构设计) | 组件关系与工作流程 |
| [应用场景](#应用场景) | 何时选择 SAEA.P2P |
| [错误码参考](#错误码参考) | 错误码对照表 |
| [核心类](#核心类) | 主要类一览 |
| [默认协议格式](#默认协议格式) | 消息协议说明 |
| [依赖项](#依赖项) | 引用的包 |

---

## 30秒快速开始

### Step 1: 安装 NuGet 包

```bash
dotnet add package SAEA.P2P
```

### Step 2: 创建 P2P 服务器（仅需5行代码）

```csharp
using SAEA.P2P;

// 使用 P2PQuick 快捷创建服务器
var server = P2PQuick.Server(39654);
server.OnNodeRegistered += (nodeId, endpoint) => 
    Console.WriteLine($"节点注册: {nodeId}");
server.Start();
```

### Step 3: 创建 P2P 客户端连接

```csharp
using SAEA.P2P;

// 使用 P2PQuick 快捷创建客户端
var client = P2PQuick.Client("127.0.0.1", 39654, "node-001");
client.OnServerConnected += () => Console.WriteLine("已连接到服务器");
client.OnMessageReceived += (peerId, data) => 
    Console.WriteLine($"收到来自 {peerId} 的消息: {Encoding.UTF8.GetString(data)}");

await client.ConnectAsync();

// 连接其他节点
await client.ConnectToPeerAsync("node-002");
client.Send("node-002", Encoding.UTF8.GetBytes("Hello P2P!"));
```

**就这么简单！** 你已经实现了一个支持 NAT 穿透和中继降级的 P2P 通信系统。

---

## 核心特性

| 特性 | 说明 | 优势 |
|------|------|------|
| **NAT 穿透** | 支持 NAT 打洞直连 | 无需公网服务器中转，降低延迟 |
| **中继降级** | 穿透失败自动中继 | 100% 连通性保证 |
| **局域网发现** | 组播发现局域网节点 | 无需信令服务器即可本地通信 |
| **身份认证** | 节点 ID + 密码认证 | 防止未授权节点接入 |
| **数据加密** | AES 加密通信 | 保护数据传输安全 |
| **Builder 配置** | 链式配置构建器 | 代码简洁，易于理解 |
| **事件驱动** | 完整的事件体系 | 异步处理，易于集成 |

---

## 架构设计

### 组件架构图

```
┌─────────────────────────────────────────────────────────────────┐
│                       SAEA.P2P 架构                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌──────────────────┐         ┌──────────────────┐            │
│  │    P2PClient     │         │    P2PServer     │            │
│  │    (客户端)      │         │    (服务器)      │            │
│  └────────┬─────────┘         └────────┬─────────┘            │
│           │                            │                       │
│  ┌────────┴────────────────────────────┴────────┐              │
│  │                   Core                        │              │
│  │  ┌─────────┐  ┌──────────┐  ┌────────────┐  │              │
│  │  │Channel  │  │PeerSession│ │ NodeInfo   │  │              │
│  │  │(通道)   │  │(节点会话) │ │ (节点信息) │  │              │
│  │  └─────────┘  └──────────┘  └────────────┘  │              │
│  └──────────────────────────────────────────────┘              │
│           │                            │                        │
│  ┌────────┴────────┐        ┌─────────┴─────────┐              │
│  │      NAT         │        │     Security     │              │
│  │ ┌──────────────┐ │        │ ┌──────────────┐ │              │
│  │ │ HolePuncher  │ │        │ │ AuthManager  │ │              │
│  │ │ NATDetector  │ │        │ │ CryptoService│ │              │
│  │ └──────────────┘ │        │ └──────────────┘ │              │
│  └──────────────────┘        └──────────────────┘              │
│           │                            │                        │
│  ┌────────┴────────┐        ┌─────────┴─────────┐              │
│  │    Discovery    │        │      Relay        │              │
│  │ ┌─────────────┐ │        │ ┌──────────────┐ │              │
│  │ │LocalDiscovery│ │        │ │ RelayManager │ │              │
│  │ └─────────────┘ │        │ │ RelaySession │ │              │
│  └─────────────────┘        │ └──────────────┘ │              │
│                             └──────────────────┘              │
│  ┌──────────────────────────────────────────────────┐         │
│  │                   Protocol                        │         │
│  │  ┌─────────────┐  ┌──────────────────────────┐  │         │
│  │  │ P2PCoder    │  │    P2PMessageType        │  │         │
│  │  │ (编解码器)   │  │ (Register/Punch/Data等) │  │         │
│  │  └─────────────┘  └──────────────────────────┘  │         │
│  └──────────────────────────────────────────────────┘         │
│                             │                                  │
│  ┌──────────────────────────┴─────────────────────────┐       │
│  │              SAEA.Sockets (底层传输)                │       │
│  └────────────────────────────────────────────────────┘       │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 连接流程图

```
客户端A                信令服务器               客户端B
   │                      │                      │
   │──── Register ───────>│                      │
   │<─── RegisterAck ─────│                      │
   │                      │<──── Register ───────│
   │                      │───── RegisterAck ───>│
   │                      │                      │
   │<──── NodeList ────────│──── NodeList ──────>│
   │                      │                      │
   │──── PunchRequest ────>│                      │
   │                      │<─── PunchRequest ────│
   │<──── PunchReady ──────│───── PunchReady ────>│
   │                      │                      │
   │<────── UDP Hole Punch (直连尝试) ──────────>│
   │                      │                      │
   │         ┌── 直连成功 ──────────────────────>│
   │         │          │                      │
   │         │      直连失败                    │
   │         └── RelayRequest ──────>│          │
   │             <── RelayAck ───────│          │
   │                      │          │          │
   │<─── RelayData ───────│<─────────│          │
   │                      │──── RelayData ────>│
   │                      │                      │
```

---

## 应用场景

### 适合使用 SAEA.P2P 的场景

| 场景 | 描述 | 推荐理由 |
|------|------|----------|
| **即时通讯** | P2P聊天、文件传输 | 直连传输，低延迟 |
| **在线游戏** | P2P对战、状态同步 | 无需中转服务器 |
| **文件共享** | P2P文件分发 | 节省带宽成本 |
| **IoT 设备** | 局域网设备通信 | 自动发现，无需配置 |
| **视频会议** | P2P音视频通话 | 直连传输，低延迟 |
| **分布式计算** | 节点间通信 | 支持大规模节点 |

### 连接策略选择

| 策略 | 说明 | 适用场景 |
|------|------|----------|
| `PreferDirect` | 优先直连，失败中继 | 一般场景（默认） |
| `PreferRelay` | 优先中继，稳定优先 | 对延迟不敏感 |
| `DirectOnly` | 仅直连，不中继 | 局域网环境 |
| `RelayOnly` | 仅中继，不尝试打洞 | NAT穿透困难的环境 |

---

## 错误码参考

### 注册相关 (EPxx)

| 错误码 | 名称 | 说明 |
|--------|------|------|
| EP01 | RegisterPeerIdMissing | 注册请求缺少节点 ID |
| EP02 | RegisterPeerIdInvalid | 节点 ID 无效 |
| EP03 | RegisterPeerIdDuplicate | 节点 ID 已被注册 |
| EP04 | RegisterNATInfoMissing | 注册请求缺少 NAT 信息 |
| EP05 | RegisterTimeout | 注册超时 |
| EP06 | RegisterFailed | 注册失败 |
| EP07 | RegisterServerUnavailable | 信令服务器不可用 |
| EP08 | RegisterAuthFailed | 注册认证失败 |
| EP09 | RegisterRejected | 注册被拒绝 |

### 发现相关 (EDxx)

| 错误码 | 名称 | 说明 |
|--------|------|------|
| ED01 | DiscoveryNoResponse | 发现无响应 |
| ED02 | DiscoveryTimeout | 发现超时 |
| ED03 | DiscoveryNoCandidates | 未发现候选节点 |
| ED04 | DiscoveryNatDetectionFailed | NAT 检测失败 |
| ED05 | DiscoveryAddressInvalid | 发现地址无效 |
| ED06 | DiscoveryPortBlocked | 发现端口被阻止 |
| ED07 | DiscoveryFailed | 发现失败 |

### 打洞相关 (EOxx/EHxx)

| 错误码 | 名称 | 说明 |
|--------|------|------|
| EO01 | PunchFailed | 打洞失败 |
| EO02 | PunchTimeout | 打洞超时 |
| EO03 | PunchNoRoute | 无法路由到节点 |
| EH01 | HolePunchFailed | NAT 穿透失败 |
| EH02 | HolePunchTimeout | NAT 穿透超时 |

### 中继相关 (ERxx)

| 错误码 | 名称 | 说明 |
|--------|------|------|
| ER01 | RelayFailed | 中继失败 |
| ER02 | RelayServerUnavailable | 中继服务器不可用 |
| ER03 | RelaySessionNotFound | 中继会话不存在 |
| ER04 | RelayQuotaExceeded | 中继配额超限 |

### 认证/加密相关 (EAxx/EExx)

| 错误码 | 名称 | 说明 |
|--------|------|------|
| EA01 | AuthFailed | 认证失败 |
| EA03 | AuthTimeout | 认证超时 |
| EE01 | EncryptionFailed | 加密失败 |
| EE02 | DecryptionFailed | 解密失败 |

---

## 核心类

| 类名 | 说明 |
|------|------|
| `P2PClient` | P2P 客户端，管理节点连接和通信 |
| `P2PServer` | P2P 信令服务器，处理节点注册和中继 |
| `P2PClientBuilder` | 客户端链式配置构建器 |
| `P2PServerBuilder` | 服务器链式配置构建器 |
| `HolePuncher` | NAT 穿透处理器 |
| `NATDetector` | NAT 类型检测器 |
| `RelayManager` | 中继管理器 |
| `LocalDiscovery` | 局域网发现服务 |
| `CryptoService` | AES 加密服务 |
| `AuthManager` | 身份认证管理器 |
| `P2PQuick` | 快捷静态方法类 |

---

## 默认协议格式

`P2PCoder` 实现的消息协议：

```
| 8字节长度 | 1字节类型 | N字节内容 |
```

- **长度**: 数据总长度（用于消息边界识别）
- **类型**: P2PMessageType 枚举值
- **内容**: 实际消息数据

### 消息类型 (P2PMessageType)

| 类型值 | 名称 | 说明 |
|--------|------|------|
| 0x01 | Register | 节点注册请求 |
| 0x02 | RegisterAck | 注册响应 |
| 0x03 | AuthChallenge | 认证挑战 |
| 0x04 | AuthResponse | 认证响应 |
| 0x05 | AuthSuccess | 认证成功 |
| 0x10 | NodeList | 节点列表 |
| 0x20 | NatProbe | NAT 探测请求 |
| 0x21 | NatProbeAck | NAT 探测响应 |
| 0x30 | PunchRequest | 打洞请求 |
| 0x31 | PunchReady | 打洞就绪 |
| 0x40 | RelayRequest | 中继请求 |
| 0x41 | RelayAck | 中继响应 |
| 0x42 | RelayData | 中继数据 |
| 0x50 | UserData | 用户数据 |
| 0x60 | Heartbeat | 心跳请求 |
| 0x61 | HeartbeatAck | 心跳响应 |

---

## 使用示例

### 快捷创建客户端/服务器

```csharp
using SAEA.P2P;
using SAEA.P2P.NAT;

// 快捷创建服务器
var server = P2PQuick.Server(39654);
server.Start();

// 快捷创建客户端（带密码认证）
var client = P2PQuick.Client("127.0.0.1", 39654, "node-001", "password123");
await client.ConnectAsync();

// 完整配置客户端
var fullClient = P2PQuick.ClientFull(
    "127.0.0.1", 39654, "node-002",
    strategy: HolePunchStrategy.PreferDirect,
    encryption: true, key: "1234567890123456",
    localDiscovery: true);

// 局域网模式（无服务器）
var localClient = P2PQuick.LocalNet("local-node", port: 39655, multicast: "224.0.0.250");
await localClient.ConnectAsync();

// 快捷创建服务器（自定义参数）
var customServer = P2PQuick.Server(39654, maxNodes: 1000, relay: true);
```

### Builder 链式配置

```csharp
using SAEA.P2P.Builder;
using SAEA.P2P.Core;
using SAEA.P2P.NAT;

// 客户端完整配置
var clientOptions = new P2PClientBuilder()
    .SetServer("127.0.0.1", 39654)
    .SetNodeId("node-001")
    .SetNodeIdPassword("secret")
    .SetTimeout(30000)
    .EnableHolePunch(HolePunchStrategy.PreferDirect)
    .SetHolePunchTimeout(10000)
    .SetHolePunchRetry(3)
    .EnableRelay(5000, 1024 * 1024 * 100)  // 5秒超时, 100MB配额
    .EnableLocalDiscovery(39656, "224.0.0.250")
    .SetDiscoveryInterval(5000)
    .EnableEncryption("1234567890123456")   // AES-128
    .EnableLogging()
    .Build();

var client = new P2PClient(clientOptions);

// 服务器完整配置
var serverOptions = new P2PServerBuilder()
    .SetPort(39654)
    .SetIP("0.0.0.0")
    .SetMaxNodes(5000)
    .EnableRelay(1000, 1024 * 1024 * 1024)  // 1000会话, 1GB总配额
    .EnableTls("server.pfx", "password")
    .SetFreeTime(60000)
    .EnableLogging()
    .Build();

var server = new P2PServer(serverOptions);
```

### 事件处理

```csharp
// 服务器事件
server.OnNodeRegistered += (nodeId, endpoint) =>
    Console.WriteLine($"节点注册: {nodeId} from {endpoint}");

server.OnNodeUnregistered += (nodeId) =>
    Console.WriteLine($"节点注销: {nodeId}");

server.OnRelayStarted += (sourceId, targetId) =>
    Console.WriteLine($"中继开始: {sourceId} -> {targetId}");

server.OnError += (id, error) =>
    Console.WriteLine($"错误: {id} - {error}");

// 客户端事件
client.OnStateChanged += (oldState, newState) =>
    Console.WriteLine($"状态变更: {oldState} -> {newState}");

client.OnServerConnected += () =>
    Console.WriteLine("已连接到服务器");

client.OnServerDisconnected += (reason) =>
    Console.WriteLine($"断开连接: {reason}");

client.OnPeerConnected += (peerId, channelType) =>
    Console.WriteLine($"节点连接: {peerId} via {channelType}");

client.OnMessageReceived += (peerId, data) =>
    Console.WriteLine($"收到消息: {peerId} - {data.Length} bytes");

client.OnLocalNodeDiscovered += (node) =>
    Console.WriteLine($"发现局域网节点: {node.NodeId}");
```

---

## 依赖项

| 包名 | 版本 | 说明 |
|------|------|------|
| SAEA.Sockets | 7.26.2.2 | 高性能 Socket 通信框架 |
| SAEA.Common | 7.26.2.2 | 通用工具类库 |

---

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包](https://www.nuget.org/packages/SAEA.P2P)
- [作者博客](https://www.cnblogs.com/yswenli/)

---

## 许可证

Apache License 2.0