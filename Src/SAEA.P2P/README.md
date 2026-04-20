# SAEA.P2P - P2P通信组件

[![NuGet version](https://img.shields.io/nuget/v/SAEA.P2P.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.P2P)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**[English Version](README.en.md)** | **中文版**

> 基于 SAEA.Sockets 的高性能 P2P 通信组件，支持 NAT 穿透、中继降级、局域网发现、身份认证与加密。让节点间通信更简单、更高效！

---

## 快速导航

| 章节 | 内容 |
|------|------|
| [30秒快速开始](#30秒快速开始) | 最简单的上手方式 |
| [场景示例](#场景示例) | 多场景实战代码 |
| [核心特性](#核心特性) | 组件的主要功能 |
| [架构设计](#架构设计) | 组件关系与工作流程 |
| [Builder 配置详解](#builder-配置详解) | 链式配置完整指南 |
| [NAT 穿透原理](#nat-穿透原理) | 打洞机制详解 |
| [错误码参考](#错误码参考) | 错误码对照表 |
| [核心类一览](#核心类一览) | 主要类说明 |
| [协议格式](#协议格式) | 消息协议说明 |
| [依赖项](#依赖项) | 引用的包 |

---

## 30秒快速开始

### 方式 1: 最简启动（推荐新手）

```bash
dotnet add package SAEA.P2P
```

**启动信令服务器（3行代码）：**
```csharp
using SAEA.P2P;

var server = P2PQuick.Server(39654);
server.Start();
Console.WriteLine("P2P服务器已启动！");
```

**创建客户端连接（5行代码）：**
```csharp
using SAEA.P2P;
using System.Text;

var client = P2PQuick.Client("127.0.0.1", 39654, "my-node");
client.OnMessageReceived += (peerId, data) => Console.WriteLine($"收到: {Encoding.UTF8.GetString(data)}");
await client.ConnectAsync();
```

**就这么简单！** 你已经实现了一个支持 NAT 穿透和中继降级的 P2P 系统。

### 方式 2: 局域网直连（无需服务器）

```csharp
using SAEA.P2P;

// 无需信令服务器，局域网自动发现
var client = P2PQuick.LocalNet("local-node");
client.OnLocalNodeDiscovered += (node) => Console.WriteLine($"发现节点: {node.NodeId} at {node.LocalAddress}");
await client.ConnectAsync();
```

---

## 场景示例

### 场景 1: 即时通讯聊天

```csharp
using SAEA.P2P;
using SAEA.P2P.Core;
using SAEA.P2P.Builder;
using System.Text;

// 创建聊天客户端
var options = new P2PClientBuilder()
    .SetServer("chat.example.com", 39654)
    .SetNodeId($"user-{Guid.NewGuid():N}")
    .EnableHolePunch()          // NAT 穿透
    .EnableRelay()              // 穿透失败自动中继
    .EnableEncryption("chat-secret-key-16")  // 加密聊天内容
    .EnableLogging()
    .Build();

var client = new P2PClient(options);

// 接收消息
client.OnMessageReceived += (peerId, data) =>
{
    var message = Encoding.UTF8.GetString(data);
    Console.WriteLine($"[{peerId}] {message}");
};

// 节点上线通知
client.OnServerConnected += () => Console.WriteLine("已连接到聊天服务器");

await client.ConnectAsync();

// 发送消息
await client.ConnectToPeerAsync("user-target");
client.Send("user-target", Encoding.UTF8.GetBytes("Hello!"));

// 获取在线用户列表
var onlineUsers = client.KnownNodes.Keys;
Console.WriteLine($"在线用户: {string.Join(", ", onlineUsers)}");
```

### 场景 2: 局域网文件共享

```csharp
using SAEA.P2P;
using SAEA.P2P.Builder;
using SAEA.P2P.Core;

// 创建局域网发现客户端
var options = new P2PClientBuilder()
    .SetNodeId($"file-server-{Environment.MachineName}")
    .EnableLocalDiscovery(39655, "224.0.0.250")
    .SetDiscoveryInterval(3000)  // 3秒广播一次
    .EnableLogging()
    .Build();

var client = new P2PClient(options);

// 发现新节点时的处理
client.OnLocalNodeDiscovered += (node) =>
{
    Console.WriteLine($"发现文件节点: {node.NodeId}");
    // 可以建立连接并请求文件
};

await client.ConnectAsync();

// 查看发现的节点
foreach (var node in client.KnownNodes)
{
    Console.WriteLine($"节点: {node.Key} - {node.Value.LastActiveTime}");
}
```

### 场景 3: IoT 设备通信

```csharp
using SAEA.P2P;
using SAEA.P2P.Builder;
using SAEA.P2P.Core;
using SAEA.P2P.NAT;

// IoT 设备客户端（稳定优先）
var options = new P2PClientBuilder()
    .SetServer("iot-hub.example.com", 39654)
    .SetNodeId($"device-{Environment.MachineName}")
    .SetNodeIdPassword("device-secret")
    .EnableHolePunch(HolePunchStrategy.PreferRelay)  // 稳定性优先
    .EnableRelay()
    .SetFreeTime(300000)  // 5分钟心跳
    .EnableLogging()
    .Build();

var client = new P2PClient(options);

// 状态变化监控
client.OnStateChanged += (old, new_) => 
    Console.WriteLine($"状态: {old} -> {new_}");

// 异常处理
client.OnError += (code, msg) => 
    Console.WriteLine($"错误 [{code}]: {msg}");

await client.ConnectAsync();

// 发送传感器数据
var sensorData = new { Temperature = 25.5, Humidity = 60 };
var json = System.Text.Json.JsonSerializer.Serialize(sensorData);
client.Send("control-center", Encoding.UTF8.GetBytes(json));
```

### 场景 4: 游戏对战同步

```csharp
using SAEA.P2P;
using SAEA.P2P.Builder;
using SAEA.P2P.Core;
using SAEA.P2P.NAT;

// 游戏客户端（低延迟优先）
var options = new P2PClientBuilder()
    .SetServer("game.example.com", 39654)
    .SetNodeId($"player-{Guid.NewGuid():N}")
    .EnableHolePunch(HolePunchStrategy.DirectOnly)  // 仅直连，最低延迟
    .SetHolePunchTimeout(5000)
    .SetHolePunchRetry(5)
    .EnableLogging()
    .Build();

var client = new P2PClient(options);

// 对手连接成功
client.OnPeerConnected += (peerId, channelType) =>
{
    Console.WriteLine($"对手连接成功: {peerId} ({channelType})");
    if (channelType == ChannelType.Direct)
        Console.WriteLine("直连模式 - 最低延迟！");
};

await client.ConnectAsync();
await client.ConnectToPeerAsync("player-opponent");

// 发送游戏状态
var gameState = new { X = 100, Y = 200, Action = "jump" };
client.Send("player-opponent", Encoding.UTF8.GetBytes(
    System.Text.Json.JsonSerializer.Serialize(gameState)));
```

### 场景 5: 分布式计算节点

```csharp
using SAEA.P2P;
using SAEA.P2P.Builder;
using SAEA.P2P.Core;

// 计算节点（完整配置）
var options = new P2PClientBuilder()
    .SetServer("compute-hub.example.com", 39654)
    .SetNodeId($"compute-{Environment.MachineName}-{Guid.NewGuid():N}")
    .SetNodeIdPassword("compute-network-secret")
    .EnableHolePunch()
    .EnableRelay(60000, 1024 * 1024 * 1024)  // 1GB 中继配额
    .EnableEncryption("compute-network-key-32")  // AES-256
    .EnableTls()              // TLS 加密信令通道
    .SetTimeout(10000)
    .EnableLogging(1, "logs/compute.log")  // Debug 级别日志
    .Build();

var client = new P2PClient(options);

// 节点列表更新
client.OnServerConnected += async () =>
{
    // 获取所有计算节点
    var nodes = client.KnownNodes;
    Console.WriteLine($"可用计算节点: {nodes.Count}");
    
    // 连接到其他计算节点
    foreach (var node in nodes.Take(3))
    {
        await client.ConnectToPeerAsync(node.Key);
    }
};

await client.ConnectAsync();

// 分发计算任务
var task = new { TaskId = Guid.NewGuid(), Data = "compute-payload" };
client.Send("compute-node-1", Encoding.UTF8.GetBytes(
    System.Text.Json.JsonSerializer.Serialize(task)));
```

### 场景 6: 企业级信令服务器

```csharp
using SAEA.P2P;
using SAEA.P2P.Builder;
using SAEA.P2P.Core;

// 企业级服务器配置
var options = new P2PServerBuilder()
    .SetPort(39654)
    .SetIP("0.0.0.0")
    .SetMaxNodes(5000)         // 最大 5000 节点
    .EnableRelay(500, 1024 * 1024 * 1024 * 10)  // 500中继, 10GB配额
    .EnableTls("server.pfx", "tls-password")     // TLS 加密
    .SetFreeTime(120000)      // 2分钟心跳
    .EnableLogging(2, "logs/server.log")
    .Build();

var server = new P2PServer(options);

// 监控节点活动
server.OnNodeRegistered += (nodeId, endpoint) =>
    Console.WriteLine($"[+] 节点上线: {nodeId} ({endpoint})");

server.OnNodeUnregistered += (nodeId) =>
    Console.WriteLine($"[-] 节点离线: {nodeId}");

server.OnRelayStarted += (source, target) =>
    Console.WriteLine($"[RELAY] {source} -> {target}");

server.OnError += (id, error) =>
    Console.WriteLine($"[ERROR] {id}: {error}");

server.Start();
Console.WriteLine($"企业级 P2P 服务器已启动，端口: {server.Port}");
```

---

## 核心特性

| 特性 | 说明 | 优势 |
|------|------|------|
| **NAT 穿透** | UDP 打洞直连通信 | 无需公网服务器中转，延迟降低 90% |
| **中继降级** | 穿透失败自动中继 | 100% 连通性保证，永不掉线 |
| **局域网发现** | UDP 组播自动发现 | 无需配置，零成本本地通信 |
| **身份认证** | 挑战-响应式认证 | SHA256 验证，防伪造 |
| **AES 加密** | 可配置密钥加密 | 128/192/256 位可选 |
| **Builder 配置** | 链式配置构建 | 25+ 配置方法，灵活组合 |
| **事件驱动** | 完整事件体系 | 异步友好，易于集成 |
| **日志追踪** | 节点动作日志 | 问题定位，行为审计 |

### 连接策略对比

| 策略 | 行为 | 延迟 | 连通性 | 适用场景 |
|------|------|------|--------|----------|
| `PreferDirect` | 先打洞，失败中继 | 低 | 高 | **默认推荐** |
| `PreferRelay` | 快速尝试后中继 | 中 | 高 | 稳定性优先 |
| `DirectOnly` | 仅打洞，失败报错 | 低 | 中 | 局域网/可控环境 |
| `RelayOnly` | 直接中继 | 高 | 高 | NAT 困难环境 |

---

## 架构设计

### 模块架构图

```
┌─────────────────────────────────────────────────────────────────┐
│                       SAEA.P2P 架构                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────── 快捷入口 (P2PQuick) ───────────┐                  │
│  │                                           │                  │
│  │  Client() / Server() / LocalNet()        │                  │
│  │  ClientFull() / Server(自定义)           │                  │
│  │                                           │                  │
│  └────────────────────────────────────────────┘                  │
│                                                                 │
│  ┌─────────── Builder 配置层 ────────────────┐                  │
│  │                                           │                  │
│  │  P2PClientBuilder   P2PServerBuilder      │                  │
│  │  25+ 链式方法      13+ 链式方法           │                  │
│  │                                           │                  │
│  └────────────────────────────────────────────┘                  │
│                                                                 │
│  ┌─────────── 核心层 (Core) ─────────────────┐                  │
│  │                                           │                  │
│  │  ┌─────────────┐     ┌─────────────┐      │                  │
│  │  │  P2PClient  │     │  P2PServer  │      │                  │
│  │  │  (客户端)   │     │  (服务器)   │      │                  │
│  │  └──────┬──────┘     └──────┬──────┘      │                  │
│  │         │                   │              │                  │
│  │  ┌──────┴───────────────────┴──────┐      │                  │
│  │  │        PeerSession / NodeInfo    │      │                  │
│  │  └─────────────────────────────────┘      │                  │
│  │                                           │                  │
│  └────────────────────────────────────────────┘                  │
│                                                                 │
│  ┌─────────── 功能模块层 ───────────────────┐                  │
│  │                                           │                  │
│  │  ┌────────────┐  ┌────────────┐          │                  │
│  │  │    NAT     │  │  Security  │          │                  │
│  │  │ HolePuncher│  │AuthManager │          │                  │
│  │  │NATDetector │  │CryptoService│          │                  │
│  │  └─────┬──────┘  └─────┬──────┘          │                  │
│  │        │               │                  │                  │
│  │  ┌─────┴──────┐  ┌─────┴──────┐          │                  │
│  │  │  Discovery │  │   Relay    │          │                  │
│  │  │LocalDiscov │  │RelayManager│          │                  │
│  │  └─────┬──────┘  └─────┬──────┘          │                  │
│  │        │               │                  │                  │
│  │  ┌─────┴───────────────┴──────┐          │                  │
│  │  │      Channel (UDP/TCP)     │          │                  │
│  │  └───────────────────────────┘           │                  │
│  │                                           │                  │
│  └────────────────────────────────────────────┘                  │
│                                                                 │
│  ┌─────────── 协议层 ───────────────────────┐                  │
│  │                                           │                  │
│  │  P2PCoder (编解码)   P2PProtocol (消息)  │                  │
│  │  P2PMessageType (类型枚举)               │                  │
│  │                                           │                  │
│  └────────────────────────────────────────────┘                  │
│                                                                 │
│  ┌─────────── 传输层 (SAEA.Sockets) ─────────┐                  │
│  │                                           │                  │
│  │  IocpServerSocket   IocpClientSocket      │                  │
│  │  UdpServerSocket   UdpClientSocket        │                  │
│  │                                           │                  │
│  └────────────────────────────────────────────┘                  │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### NAT 穿透流程图

```
     NodeA                SignalServer                NodeB
        │                      │                       │
        │──── 1. Register ────>│                       │
        │<─── RegisterAck ─────│                       │
        │                      │                       │
        │──── 2. NatProbe ────>│                       │
        │<─── NatProbeAck ─────│                       │
        │    (获取外部地址)    │                       │
        │                      │                       │
        │──── 3. PunchRequest ─>│──── PunchReady ────> │
        │<─── PunchReady ──────│                       │
        │    (含B的外部地址)   │                       │
        │                      │                       │
        │──── 4. UDP PunchSync (多次) ────────────────>│
        │<────────────────────── PunchSync (多次) ──── │
        │                      │                       │
        │──── 5. PunchAck ────────────────────────────>│
        │<────────────────────── PunchAck ──────────── │
        │                      │                       │
        │ ═══════ 6. UDP直连通道建立 ════════════════ │
        │                      │                       │
        │                      │                       │
        │    ┌─ 打洞成功 ───────│──────────────────────>│
        │    │                 │                       │
        │    │ 打洞失败        │                       │
        │    └─ 7. RelayRequest ──────────────────────>│
        │       <── RelayAck ──│                       │
        │                      │                       │
        │<─── 8. RelayData ────│─── RelayData ────────>│
        │                      │                       │
```

---

## Builder 配置详解

### P2PClientBuilder 完整配置清单

```csharp
var options = new P2PClientBuilder()
    // === 基础配置 ===
    .SetServer("127.0.0.1", 39654)       // 信令服务器地址（支持域名/IP）
    .SetServer(new IPEndPoint(...))       // 或使用 IPEndPoint
    .SetNodeId("unique-node-id")          // 节点唯一标识
    .SetNodeIdPassword("auth-secret")     // 认证密码
    .SetTimeout(5000)                     // 连接超时(ms)
    
    // === NAT 穿透配置 ===
    .EnableHolePunch()                    // 启用打洞（默认 PreferDirect）
    .EnableHolePunch(HolePunchStrategy.PreferRelay)  // 指定策略
    .SetHolePunchTimeout(10000)           // 打洞超时(ms)
    .SetHolePunchRetry(5)                 // 重试次数
    .SetNATType(NATType.FullCone)         // 强制指定 NAT 类型
    
    // === 中继配置 ===
    .EnableRelay()                        // 启用中继降级
    .EnableRelay(60000, 10 * 1024 * 1024) // 超时+配额
    
    // === 局域网发现配置 ===
    .EnableLocalDiscovery()               // 默认端口和组播
    .EnableLocalDiscovery(39655)          // 自定义端口
    .EnableLocalDiscovery(39655, "224.0.0.250")  // 端口+组播地址
    .SetDiscoveryInterval(5000)           // 发现广播间隔
    
    // === 安全配置 ===
    .EnableEncryption()                   // 使用协商密钥
    .EnableEncryption("aes-key-12345678") // 自定义密钥(16/24/32位)
    .EnableTls()                          // TLS 加密信令通道
    
    // === 心跳配置 ===
    .SetFreeTime(180000)                  // 信令心跳间隔
    .SetPeerHeartbeat(30000)              // 节点间心跳间隔
    .SetPeerHeartbeatRetry(3)             // 心跳重试次数
    
    // === 日志配置 ===
    .EnableLogging()                      // 默认 Info 级别
    .EnableLogging(1)                     // Debug 级别
    .EnableLogging(0, "logs/p2p.log")     // Trace 级别+自定义路径
    .SetLogToConsole(false)               // 关闭控制台输出
    
    .Build();                             // 构建并验证
```

### P2PServerBuilder 完整配置清单

```csharp
var options = new P2PServerBuilder()
    // === 基础配置 ===
    .SetPort(39654)                       // 监听端口
    .SetIP("0.0.0.0")                     // 绑定地址
    .SetMaxNodes(1000)                    // 最大节点数
    
    // === 中继配置 ===
    .EnableRelay()                        // 启用中继
    .EnableRelay(100, 1024 * 1024 * 1024) // 最大中继数+总配额
    
    // === 安全配置 ===
    .EnableTls("server.pfx", "password")  // TLS 加密
    
    // === 心跳配置 ===
    .SetFreeTime(180000)                  // 心跳间隔
    
    // === 日志配置 ===
    .EnableLogging()
    .EnableLogging(2, "logs/server.log")
    
    .Build();
```

---

## NAT 穿透原理

### NAT 类型分类

| NAT 类型 | 特征 | 穿透难度 | 穿透方法 | 成功率 |
|----------|------|----------|----------|--------|
| **Full Cone** | 任何外部地址可发送 | 低 | 直接发 PunchSync | ~99% |
| **Restricted Cone** | 需先接收才能发送 | 中 | 双方同时发送 | ~90% |
| **Port Restricted** | 需精确端口匹配 | 中高 | 精确时序配合 | ~80% |
| **Symmetric** | 每次连接换端口 | 高 | 降级中继 | ~30% |

### 打洞过程详解

**Step 1: 注册与地址探测**
- 客户端向服务器注册，发送 NatProbe
- 服务器返回客户端的外部地址 (IP:Port)
- 客户端记录自己的公网地址

**Step 2: 打洞协调**
- A 发送 PunchRequest(B) 给服务器
- 服务器查询 B 的地址，发送 PunchReady 给双方
- PunchReady 包含对方的外部地址和 NAT 类型

**Step 3: UDP 打洞执行**
- A 和 B 同时向对方外部地址发送 PunchSync
- 双方持续发送直到收到对方的 PunchSync
- 收到后发送 PunchAck 确认

**Step 4: 直连建立或中继降级**
- 成功收到 PunchAck → 直连通道建立
- 超时未收到 → 自动 RelayRequest 降级

---

## 错误码参考

### 配置错误 (EPxx)

| 错误码 | 说明 | 解决建议 |
|--------|------|----------|
| EP01 | 服务器地址未配置 | 调用 SetServer() |
| EP02 | 服务器地址格式错误 | 使用有效域名/IP |
| EP03 | 端口范围无效 | 使用 1-65535 |
| EP04 | 节点ID未设置 | 调用 SetNodeId() |
| EP05 | 节点ID格式错误 | 长度≤64，字母数字 |
| EP06 | 加密密钥未设置 | EnableEncryption(key) |
| EP07 | 加密密钥长度错误 | 16/24/32 字节 |
| EP08 | 超时值无效 | 设置合理超时值 |
| EP09 | Builder配置不完整 | 检查必要配置项 |

### 数据错误 (EDxx)

| 错误码 | 说明 | 解决建议 |
|--------|------|----------|
| ED01 | 发送数据为空 | 提供有效数据 |
| ED02 | 发送数据超长 | UDP ≤ 64KB，或分片 |
| ED03 | 消息解码失败 | 检查编码方式 |
| ED06 | 目标节点不存在 | 先建立连接 |

### 操作错误 (EOxx)

| 错误码 | 说明 | 解决建议 |
|--------|------|----------|
| EO01 | 未连接时发送 | 先调用 Connect() |
| EO02 | 重复连接 | 检查 Connected 状态 |
| EO03 | 连接超时 | 检查网络/增大超时 |

### NAT/打洞错误 (EHxx)

| 错误码 | 说明 | 解决建议 |
|--------|------|----------|
| EH01 | NAT不支持穿透 | 自动降级中继 |
| EH02 | 打洞超时 | 增大超时/重试 |

### 中继错误 (ERxx)

| 错误码 | 说明 | 解决建议 |
|--------|------|----------|
| ER01 | 中继未启用 | EnableRelay() |
| ER02 | 中继服务器断开 | 检查服务器状态 |
| ER04 | 中继配额超限 | 增大配额或升级 |

### 认证/加密错误 (EAxx/EExx)

| 错误码 | 说明 | 解决建议 |
|--------|------|----------|
| EA01 | 认证失败 | 检查 NodeIdPassword |
| EA03 | 节点ID重复 | 更换唯一ID |
| EE01 | 加密初始化失败 | 检查密钥格式 |
| EE02 | 解密失败 | 检查密钥是否匹配 |

---

## 核心类一览

### 核心组件

| 类名 | 命名空间 | 说明 |
|------|----------|------|
| `P2PClient` | SAEA.P2P.Core | 客户端核心类 |
| `P2PServer` | SAEA.P2P.Core | 信令服务器核心类 |
| `P2PQuick` | SAEA.P2P | 快捷静态方法 |
| `P2PClientBuilder` | SAEA.P2P.Builder | 客户端配置构建器 |
| `P2PServerBuilder` | SAEA.P2P.Builder | 服务器配置构建器 |

### 协议与编码

| 类名 | 说明 |
|------|------|
| `P2PProtocol` | 协议消息类，继承 BaseSocketProtocal |
| `P2PCoder` | 编解码器，继承 BaseCoder |
| `P2PMessageType` | 消息类型枚举 |

### NAT 穿透

| 类名 | 说明 |
|------|------|
| `HolePuncher` | UDP 打洞执行器 |
| `NATDetector` | NAT 类型检测器 |
| `NATType` | NAT 类型枚举 |
| `HolePunchStrategy` | 打洞策略枚举 |

### 中继与发现

| 类名 | 说明 |
|------|------|
| `RelayManager` | 中继会话管理 |
| `RelaySession` | 中继会话模型 |
| `LocalDiscovery` | 局域网发现服务 |
| `DiscoveredNode` | 发现的节点信息 |

### 安全模块

| 类名 | 说明 |
|------|------|
| `CryptoService` | AES 加密服务 |
| `AuthManager` | 认证管理器 |
| `AuthChallenge` | 认证挑战模型 |
| `KeyExchange` | 密钥交换 |

### 通道与模型

| 类名 | 说明 |
|------|------|
| `UDPChannel` | UDP 通道封装 |
| `TCPChannel` | TCP 通道封装 |
| `PeerSession` | 节点会话模型 |
| `NodeInfo` | 节点信息模型 |

---

## 协议格式

### 消息结构

```
| 8字节长度 | 1字节类型 | N字节内容 |
```

复用 SAEA.Sockets BaseSocketProtocal 格式，确保兼容性。

### 消息类型定义

| 类型值 | 名称 | 方向 | 说明 |
|--------|------|------|------|
| 0x10 | Register | C→S | 节点注册请求 |
| 0x11 | RegisterAck | S→C | 注册响应 |
| 0x12 | Unregister | C→S | 节点注销 |
| 0x13 | NodeList | S→C | 节点列表推送 |
| 0x20 | NatProbe | C→S | NAT 探测请求 |
| 0x21 | NatProbeAck | S→C | NAT 探测响应 |
| 0x30 | PunchRequest | C→S | 打洞请求 |
| 0x31 | PunchReady | S→C | 打洞就绪通知 |
| 0x33 | PunchSync | C→C | 打洞同步包(UDP) |
| 0x34 | PunchAck | C→C | 打洞成功确认 |
| 0x40 | RelayRequest | C→S | 中继请求 |
| 0x41 | RelayAck | S→C | 中继响应 |
| 0x42 | RelayData | S→C | 中继数据转发 |
| 0x50 | LocalDiscover | C→LAN | 局域网发现广播 |
| 0x51 | LocalDiscoverAck | C→LAN | 发现响应 |
| 0x60 | AuthChallenge | S→C | 认证挑战 |
| 0x61 | AuthResponse | C→S | 认证响应 |
| 0x62 | AuthSuccess | S→C | 认证成功 |
| 0x70 | Heartbeat | C→S | 心跳请求 |
| 0x71 | HeartbeatAck | S→C | 心跳响应 |
| 0x80 | UserData | C→C | 用户数据 |
| 0x81 | UserDataAck | C→C | 数据送达确认 |

---

## 依赖项

| 包名 | 版本 | 说明 |
|------|------|------|
| SAEA.Sockets | 7.26+ | IOCP 高性能 Socket 框架 |
| SAEA.Common | 7.26+ | AESHelper、LogHelper、SerializeHelper |

---

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包列表](https://www.nuget.org/packages?q=saea)
- [作者博客](https://www.cnblogs.com/yswenli/)
- [SAEA.Sockets 文档](../SAEA.Sockets/README.md)
- [SAEA.Common 文档](../SAEA.Common/README.md)

---

## 许可证

Apache License 2.0