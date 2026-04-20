# SAEA.P2P 设计文档

> 基于 SAEA.Sockets 的 P2P 通信组件设计

**版本**: v1.0.0  
**日期**: 2026-04-20  
**作者**: yswenli

---

## 1. 项目概述

### 1.1 项目背景

SAEA.P2P 是基于 SAEA.Sockets 高性能 IOCP 框架的 P2P 通信组件，提供完整的点对点通信解决方案。

### 1.2 核心特性

| 特性 | 说明 |
|------|------|
| UDP 打洞 | NAT 穿透实现直连通信 |
| 中继降级 | 打洞失败自动切换中继转发 |
| 局域网发现 | UDP 广播/组播自动发现本地节点 |
| 身份认证 | 挑战-响应式认证机制 |
| AES 加密 | 节点间通信加密 |
| 混合模式 | 同时支持 TCP 和 UDP |
| Builder 配置 | 链式配置，灵活易用 |
| 快捷入口 | 多场景快捷封装 |

### 1.3 目标用户

- 需要 P2P 实时通信的应用开发者
- 游戏服务器开发（对战、状态同步）
- 即时通讯系统开发
- IoT 设备间通信
- 局域网协作应用

---

## 2. 架构设计

### 2.1 整体架构

```
┌─────────────────────────────────────────────────────┐
│                  SAEA.P2P 架构                        │
├─────────────────────────────────────────────────────┤
│                                                     │
│  ┌─────────────── 应用层（快捷入口）─────────┐       │
│  │                                           │       │
│  │  QuickStart:                              │       │
│  │  P2PQuick.Client()   → 快速客户端         │       │
│  │  P2PQuick.Server()   → 快速信令服务器     │       │
│  │  P2PQuick.LocalNet() → 快速局域网节点     │       │
│  │                                           │       │
│  │  Builder模式:                             │       │
│  │  P2PClientBuilder                        │       │
│  │    .SetServer("127.0.0.1", 39654)         │       │
│  │    .SetNodeId("node-001")                 │       │
│  │    .EnableHolePunch()                     │       │
│  │    .EnableRelay()                         │       │
│  │    .EnableEncryption()                    │       │
│  │    .EnableLocalDiscovery()                │       │
│  │    .Build()                               │       │
│  │                                           │       │
│  │  P2PServerBuilder                        │       │
│  │    .SetPort(39654)                        │       │
│  │    .SetMaxNodes(1000)                     │       │
│  │    .EnableRelay()                         │       │
│  │    .Build()                               │       │
│  │                                           │       │
│  └────────────────────────────────────────────┘       │
│                                                     │
│  ┌─────────────── 核心组件 ──────────────────┐       │
│  │  P2PClient  │  P2PServer  │  P2PNode     │       │
│  └─────────────┬──────────────────────────────┘       │
│                │                                     │
│  ┌─────────────── 功能层 ────────────────────┐       │
│  │  HolePuncher │ RelayManager │ Discovery  │       │
│  │  AuthManager │ CryptoService │ Session   │       │
│  └─────────────┬──────────────────────────────┘       │
│                │                                     │
│  ┌─────────────── 协议与传输层 ──────────────┐       │
│  │  P2PProtocol │ P2PCoder │ UDP/TCP Channel │       │
│  │  (复用SAEA.Sockets)                       │       │
│  └────────────────────────────────────────────┘       │
│                                                     │
└─────────────────────────────────────────────────────┘
```

### 2.2 模块职责

| 模块 | 职责 |
|------|------|
| P2PServer | 信令服务器，管理节点注册、发现、NAT 类型检测、打洞协调 |
| P2PClient | 客户端，封装节点连接、消息收发、NAT 穿透逻辑 |
| HolePuncher | UDP 打洞核心，实现 STUN 式 NAT 探测和穿透 |
| RelayManager | 中继服务，打洞失败时通过信令服务器转发 |
| Discovery | 局域网发现，UDP 广播/组播自动发现节点 |
| AuthManager | 身份认证，挑战-响应式认证 |
| CryptoService | 加密服务，AES 对称加密 |

---

## 3. 协议设计

### 3.1 协议格式

复用 SAEA.Sockets BaseSocketProtocal 格式：

```
[8字节长度][1字节类型][消息体]
```

### 3.2 消息类型枚举

```csharp
public enum P2PMessageType : byte
{
    // 信令消息 (0x10-0x1F)
    Register       = 0x10,  // 节点注册
    RegisterAck    = 0x11,  // 注册确认/拒绝
    Unregister     = 0x12,  // 节点注销
    NodeList       = 0x13,  // 节点列表推送
    NodeQuery      = 0x14,  // 查询特定节点
    NodeQueryAck   = 0x15,  // 查询结果
    
    // NAT探测消息 (0x20-0x2F)
    NatProbe       = 0x20,  // NAT探测请求
    NatProbeAck    = 0x21,  // NAT探测响应(返回外部地址)
    NatTypeReport  = 0x22,  // NAT类型上报
    
    // 打洞协调消息 (0x30-0x3F)
    PunchRequest   = 0x30,  // 打洞请求(含目标节点信息)
    PunchReady     = 0x31,  // 打洞就绪通知
    PunchStart     = 0x32,  // 开始打洞指令
    PunchSync      = 0x33,  // 打洞同步包(UDP直发)
    PunchAck       = 0x34,  // 打洞成功确认
    
    // 中继消息 (0x40-0x4F)
    RelayRequest   = 0x40,  // 请求中继转发
    RelayAck       = 0x41,  // 中继请求确认
    RelayData      = 0x42,  // 中继数据转发
    RelayEnd       = 0x43,  // 结束中继
    
    // 局域网发现 (0x50-0x5F)
    LocalDiscover  = 0x50,  // 局域网广播发现
    LocalDiscoverAck = 0x51, // 发现响应
    
    // 认证与安全 (0x60-0x6F)
    AuthChallenge  = 0x60,  // 认证挑战(服务器发起)
    AuthResponse   = 0x61,  // 认证响应(客户端计算)
    AuthSuccess    = 0x62,  // 认证成功
    AuthFailed     = 0x63,  // 认证失败
    KeyExchange    = 0x64,  // 密钥交换(协商会话密钥)
    
    // 心跳与状态 (0x70-0x7F)
    Heartbeat      = 0x70,  // 心跳包
    HeartbeatAck   = 0x71,  // 心跳响应
    StatusReport   = 0x72,  // 状态上报
    
    // 用户数据 (0x80-0x8F)
    UserData       = 0x80,  // 用户消息数据
    UserDataAck    = 0x81,  // 数据送达确认(可选)
    StreamData     = 0x82,  // 流式大数据(分片)
}
```

### 3.3 关键消息结构

| 消息 | 字段 | 说明 |
|------|------|------|
| Register | nodeId, nodeType, publicAddr, localAddr | nodeId:唯一标识; nodeType:客户端类型; publicAddr:外部地址; localAddr:本地地址 |
| RegisterAck | code, nodeId, sessionId | code:成功/错误码; sessionId:分配的会话ID |
| PunchRequest | sourceId, targetId, sourceAddr | sourceId:发起者ID; targetId:目标节点ID; sourceAddr:发起者外部地址 |
| PunchSync | sessionId, timestamp | 打洞同步包，UDP直接发送给目标地址 |
| RelayData | sourceId, targetId, payload | payload:实际用户数据 |
| UserData | seqId, payload | seqId:序列号(可选确认); payload:用户数据 |
| LocalDiscover | nodeId, nodeName, localAddr, services, timestamp | 局域网发现广播 |

---

## 4. NAT 穿透设计

### 4.1 NAT 类型分类

| NAT类型 | 穿透难度 | 穿透方法 | 成功率 |
|---------|---------|---------|--------|
| Full Cone | 低 | 收到打洞指令后，直接向目标地址发包 | ~99% |
| Restricted Cone | 中 | 需先让目标节点向自己发包"开洞"，再双向通信 | ~90% |
| Port Restricted | 中高 | 双方需同时发包，精确时序配合 | ~80% |
| Symmetric | 高 | 打洞成功率低，建议降级中继 | ~30% |

### 4.2 NAT 类型检测流程（类似 STUN）

```
1. 客户端向信令服务器发送NatProbe请求
2. 服务器返回客户端的外部地址(IP:Port)
3. 客户端从不同端口发送第二个请求
4. 服务器比较两次请求的外部地址变化

判断规则：
- 两次外部地址相同 → Full Cone NAT (易穿透)
- 两次外部地址不同 → Symmetric NAT (难穿透)
- 仅端口变化 → Restricted Cone NAT (中等难度)
```

### 4.3 打洞流程

```
场景：节点A想连接节点B

步骤1: A向信令服务器发送PunchRequest(B的ID)
       ├─ 服务器查询B的外部地址和NAT类型
       ├─ 服务器转发PunchReady给B，包含A的地址信息
       └─ 服务器同时回复A，包含B的地址信息

步骤2: A和B收到PunchReady后开始打洞
       ├─ A向B的外部地址发送PunchSync包(多次)
       ├─ B同时向A的外部地址发送PunchSync包(多次)
       ├─ 双方持续发送直到收到对方的PunchSync

步骤3: 成功建立直连
       ├─ A收到B的PunchSync → 发送PunchAck确认
       ├─ B收到A的PunchAck → 打洞成功
       └─ 双方切换到UDP直连通信模式

失败处理：
       ├─ 超时未收到PunchAck → 打洞失败
       ├─ 自动切换到RelayRequest请求中继
       ├─ 服务器建立中继通道转发数据
```

### 4.4 打洞时序图

```
NodeA          SignalServer          NodeB
  │                 │                  │
  │ PunchRequest    │                  │
  │────────────────►│                  │
  │                 │   PunchReady     │
  │                 │─────────────────►│
  │ PunchReady      │                  │
  │◄───────────────│                  │
  │                 │                  │
  │ PunchSync ───────────────────────►│ (UDP直发)
  │◄────────────────────────────── PunchSync │
  │ PunchAck ───────────────────────►│
  │◄────────────────────────────── PunchAck │
  │                 │                  │
  │ === UDP直连通道建立 ===            │
```

### 4.5 打洞策略配置

```csharp
// 方式1：完全自动（推荐大多数用户）
.EnableHolePunch()  // 自动检测NAT类型，自动选择策略

// 方式2：指定偏好策略
.EnableHolePunch(HolePunchStrategy.PreferRelay)     // 优先中继，打洞失败快速降级
.EnableHolePunch(HolePunchStrategy.PreferDirect)    // 优先打洞，尽量直连
.EnableHolePunch(HolePunchStrategy.DirectOnly)      // 仅打洞，不降级中继
.EnableHolePunch(HolePunchStrategy.RelayOnly)       // 仅中继，不尝试打洞

// 方式3：覆盖NAT类型（检测不准确时手动指定）
.SetNATType(NATType.Symmetric)  // 强制指定，跳过自动检测

// 方式4：调整打洞参数
.SetHolePunchTimeout(10000)     // 打洞超时10秒
.SetHolePunchRetry(5)           // 重试5次
```

### 4.6 策略枚举

| 策略 | 行为 | 适用场景 |
|------|------|---------|
| PreferDirect | 先打洞，失败后降级中继 | 通用场景 |
| PreferRelay | 快速尝试打洞后立即降级 | 稳定性优先 |
| DirectOnly | 仅打洞，失败报错 | 已知网络环境良好 |
| RelayOnly | 直接使用中继 | 已知无法打洞 |

---

## 5. 中继转发设计

### 5.1 中继架构

```
中继模式：信令服务器兼任中继转发功能

┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   NodeA     │◄───►│SignalServer │◄───►│   NodeB     │
│  (发送方)    │     │  (中继转发)  │     │  (接收方)    │
└─────────────┘     └─────────────┘     └─────────────┘

数据流向：
A发送 → 服务器接收 → 服务器转发 → B接收
```

### 5.2 中继流程

```
中继建立流程：

1. 打洞失败触发
   ├─ 打洞超时或NAT类型不兼容
   ├─ 自动发送RelayRequest到信令服务器
   └─ 请求中包含targetId

2. 服务器分配中继通道
   ├─ 服务器创建中继Session
   ├─ 返回RelayAck确认
   ├─ 通知目标节点RelayRequest
   └─ 目标节点响应RelayAck

3. 中继数据传输
   ├─ 发送方发送RelayData
   ├─ 服务器转发给接收方
   ├─ 接收方处理数据
   └─ 可选：接收方回复送达确认

4. 中继结束
   ├─ 任一方发送RelayEnd
   ├─ 服务器释放中继Session
   └─ 双方断开中继通道
```

### 5.3 中继时序图

```
NodeA          SignalServer          NodeB
  │                 │                  │
  │ RelayRequest    │                  │
  │────────────────►│                  │
  │                 │ RelayRequest     │
  │                 │─────────────────►│
  │ RelayAck        │ RelayAck         │
  │◄───────────────│◄─────────────────│
  │                 │                  │
  │ RelayData       │                  │
  │────────────────►│ RelayData        │
  │                 │─────────────────►│
  │                 │                  │
  │ RelayEnd        │                  │
  │────────────────►│ RelayEnd         │
  │                 │─────────────────►│
```

### 5.4 中继通道状态

| 状态 | 说明 |
|------|------|
| Pending | 等待目标节点确认 |
| Active | 中继通道已建立，可传输数据 |
| Idle | 通道空闲，等待数据 |
| Closed | 中继已关闭 |

### 5.5 中继Builder配置

```csharp
// 客户端中继配置
.EnableRelay()                              // 默认配置

.EnableRelay(options =>
    options.SetRelayTimeout(60000)          // 中继通道超时ms
           .SetMaxRelayQuota(1024 * 1024)   // 最大流量配额（字节）
           .SetRelayBufferSize(8192))       // 中继缓冲区大小

// 服务端中继配置
new P2PServerBuilder()
    .SetPort(39654)
    .EnableRelay()                           // 启用中继功能
    .SetMaxRelayConnections(100)            // 最大中继连接数
    .SetMaxRelayQuotaPerNode(10 * 1024 * 1024) // 每节点配额
    .Build();
```

---

## 6. 局域网发现设计

### 6.1 发现方式对比

| 方式 | 机制 | 适用场景 | 端口 |
|------|------|---------|------|
| UDP广播 | 向255.255.255.255广播 | 小型局域网 | 固定端口如39655 |
| UDP组播 | 向组播地址224.x.x.x发送 | 大型局域网，避免广播风暴 | 39655 |
| TCP扫描 | 遍知IP范围扫描 | 已知IP范围 | 动态端口 |

### 6.2 发现流程

```
局域网发现流程：

1. 启动监听
   ├─ 绑定UDP端口39655（可配置）
   ├─ 加入组播组 224.0.0.250（默认）
   └─ 开启广播接收

2. 发送发现请求
   ├─ LocalDiscover消息包含：nodeId, localAddr, services
   ├─ 发送到广播地址或组播地址
   └─ 定期发送（心跳间隔可配置）

3. 接收响应
   ├─ 其他节点收到LocalDiscover
   ├─ 发送LocalDiscoverAck响应
   ├─ 响应包含：nodeId, localAddr, services
   └─ 双方建立本地节点列表

4. 直连建立
   ├─ 收到Ack后可直接TCP/UDP连接
   ├─ 无需信令服务器协调
   └─ 适合内网文件共享、协作等场景
```

### 6.3 发现时序图

```
NodeA                Network                NodeB
  │                     │                     │
  │ LocalDiscover(广播) │                     │
  │────────────────────►│────────────────────►│
  │                     │                     │
  │                     │    LocalDiscoverAck │
  │◄────────────────────│◄────────────────────│
  │                     │                     │
  │ === 直接建立连接 === │                     │
```

### 6.4 发现Builder配置

```csharp
// 启用局域网发现
.EnableLocalDiscovery()                      // 使用默认配置

// 自定义局域网发现参数
.EnableLocalDiscovery(options => 
    options.SetPort(39655)                   // 发现端口
           .SetMulticastGroup("224.0.0.250") // 组播地址
           .SetBroadcastInterval(5000)       // 广播间隔ms
           .SetDiscoveryTimeout(30000))      // 发现超时ms

// 仅局域网模式（无需信令服务器）
new P2PClientBuilder()
    .EnableLocalDiscovery()
    .SetNodeId("local-node-001")
    .Build();  // 不调用SetServer，纯局域网模式

// 混合模式（局域网+信令）
new P2PClientBuilder()
    .SetServer("signal.example.com", 39654)  // 远程信令
    .EnableLocalDiscovery()                   // 同时局域网发现
    .Build();
```

---

## 7. 身份认证与加密设计

### 7.1 认证流程

```
认证机制：基于NodeId + NodeIdPassword的挑战-响应认证

流程：
1. 客户端发送Register请求
2. 服务器生成随机Challenge
3. 客户端计算 Response = HMAC(Challenge, NodeIdPassword)
4. 服务器验证Response
5. 双方协商会话密钥用于后续加密通信
```

### 7.2 认证时序图

```
Client                    SignalServer
   │                          │
   │ Register(nodeId)         │
   │─────────────────────────►│
   │                          │
   │ AuthChallenge(challenge) │
   │◄─────────────────────────│
   │                          │
   │ AuthResponse(response)   │
   │─────────────────────────►│
   │                          │
   │ AuthSuccess/AuthFailed   │
   │◄─────────────────────────│
   │                          │
   │ KeyExchange(sessionKey)  │  ← 协商会话密钥
   │◄────────────────────────►│
```

### 7.3 加密层级

```
加密层级：
┌─────────────────────────────────────┐
│ 信令通道加密（可选TLS）               │  ← TCP连接层
├─────────────────────────────────────┤
│ 会话加密（AES对称加密）               │  ← 节点间通信
├─────────────────────────────────────┤
│ 消息完整性校验（HMAC-SHA256）         │  ← 防篡改
└─────────────────────────────────────┘

加密流程：
发送：原始数据 → AES加密 → HMAC签名 → 发送
接收：接收数据 → HMAC验证 → AES解密 → 原始数据
```

### 7.4 Builder配置

```csharp
// 认证配置
.SetNodeId("node-001")
.SetNodeIdPassword("my-secret-key")  // 用于身份验证

// 加密配置
.EnableEncryption()                   // 使用协商的会话密钥
.EnableEncryption("custom-aes-key")   // 自定义密钥（需双方一致）

// 信令通道加密（TLS）
.EnableTls()                          // 连接信令服务器使用TLS
```

### 7.5 密钥协商方式

| 方式 | 适用场景 | 说明 |
|------|---------|------|
| 自动协商 | 推荐 | 信令服务器协助双方交换密钥，一次一密 |
| 自定义密钥 | 私有网络 | 用户预共享密钥，需双方配置相同 |
| 无加密 | 内部网络 | EnableEncryption未调用时不加密 |

---

## 8. 会话管理与节点生命周期

### 8.1 节点状态定义

```
节点生命周期状态：

Init        → 初始化，Builder配置完成
Connecting  → 正在连接信令服务器
Authenticating → 正在进行身份认证
Registered  → 已注册到信令服务器
HolePunching → 正在进行UDP打洞
Relaying    → 使用中继模式通信
Connected   → 已与目标节点建立通信
Idle        → 空闲状态，等待操作
Disconnected → 已断开连接
Error       → 发生错误
```

### 8.2 状态流转图

```
┌───────┐
│ Init  │
└───┬───┘
    │ Connect()
    ▼
┌───────────┐     失败      ┌──────────┐
│Connecting │──────────────►│  Error   │
└─────┬─────┘               └──────────┘
      │ 成功
      ▼
┌───────────────┐   失败    ┌──────────┐
│Authenticating │──────────►│  Error   │
└───────┬───────┘           └──────────┘
        │ 成功
        ▼
┌───────────┐
│Registered │◄──────────────┐
└─────┬─────┘               │
      │ ConnectToPeer()     │ Disconnect()
      ▼                     │
┌──────────────┐            │
│HolePunching  │──失败──►────┤
└──────┬───────┘            │
       │ 成功               │
       ▼                    │
┌──────────┐                │
│Connected │◄───────────────┤
└────┬─────┘                │
     │ Disconnect()         │
     └──────────────────────►
                              ▼
                        ┌─────────────┐
                        │Disconnected │
                        └─────────────┘
```

### 8.3 PeerSession结构

```csharp
class PeerSession
{
    string SessionId;           // 会话唯一标识
    string PeerId;              // 目标节点ID
    PeerState State;            // 当前状态
    IPEndPoint PublicAddr;      // 对方外部地址
    IPEndPoint LocalAddr;       // 对方本地地址
    ChannelType Channel;        // 通道类型: Direct/Relay/Local
    
    DateTime CreatedTime;       // 创建时间
    DateTime LastActive;        // 最后活动时间
    
    UDPSocket DirectChannel;    // 直连UDP通道
    RelayInfo RelayInfo;        // 中继信息（若使用中继）
    
    int SendSeq;                // 发送序列号
    int ReceiveSeq;             // 接收序列号
}
```

### 8.4 心跳保活机制

```
心跳规则：

1. 信令通道心跳
   ├─ 客户端每隔FreeTime发送Heartbeat
   ├─ 服务器响应HeartbeatAck
   └─ 超时未响应视为断开，自动重连

2. 节点间通道心跳（直连/中继）
   ├─ 每隔HeartbeatInterval发送心跳
   ├─ 连续N次未响应视为通道失效
   └─ 触发重连或重新打洞

Builder配置：
.SetFreeTime(180000)           // 信令心跳间隔ms
.SetPeerHeartbeat(30000)       // 节点间心跳间隔ms
.SetPeerHeartbeatRetry(3)      // 心跳重试次数
```

### 8.5 事件通知

```csharp
// P2PClient事件
client.OnStateChanged += (oldState, newState) => { };
client.OnServerConnected += () => { };
client.OnServerDisconnected += (reason) => { };
client.OnPeerConnected += (peerId, channelType) => { };
client.OnPeerDisconnected += (peerId, reason) => { };
client.OnMessageReceived += (peerId, data) => { };
client.OnError += (errorCode, message) => { };
client.OnLocalNodeDiscovered += (nodeId, addr) => { };

// P2PServer事件
server.OnNodeRegistered += (nodeId, addr) => { };
server.OnNodeUnregistered += (nodeId) => { };
server.OnRelayStarted += (sourceId, targetId) => { };
server.OnRelayEnded += (sessionId) => { };
server.OnError += (errorCode, message) => { };
```

---

## 9. 错误码设计

### 9.1 错误码格式

```
错误码格式: E[P/D/O][模块][序号]
E = Error前缀
P = 配置类错误
D = 数据类错误  
O = 操作类错误
模块: S(Server) C(Client) N(Network) H(HolePunch) R(Relay) A(Auth) E(Encrypt)
```

### 9.2 配置类错误 (EP)

| 错误码 | 描述 | 原因 | 建议 |
|--------|------|------|------|
| EP01 | 服务器地址未配置 | SetServer未调用 | 先调用SetServer设置信令地址 |
| EP02 | 服务器地址格式错误 | 值为空或格式无效 | 使用有效域名/IP: "example.com" 或 "192.168.1.1" |
| EP03 | 端口范围无效 | 端口不在1-65535范围 | 使用有效端口如39654 |
| EP04 | 节点ID未设置 | SetNodeId未调用 | 调用SetNodeId设置唯一标识 |
| EP05 | 节点ID格式错误 | 含非法字符或长度超限 | 使用字母数字组合，长度≤64 |
| EP06 | 加密密钥未设置 | EnableEncryption无参数 | 提供AES密钥: EnableEncryption("your-key") |
| EP07 | 加密密钥长度错误 | 密钥长度不符合AES要求 | 使用16/24/32字节密钥 |
| EP08 | 超时值无效 | 超时值≤0或过大 | 设置合理超时: SetTimeout(5000) |
| EP09 | Builder未完成配置 | Build时缺少必要参数 | 确保调用所有必要Set方法 |
| EP10 | 端口复用配置冲突 | ReusePort与SSL同时启用 | 选择其一或调整配置 |

### 9.3 数据类错误 (ED)

| 错误码 | 描述 | 原因 | 建议 |
|--------|------|------|------|
| ED01 | 发送数据为空 | Send(null)或空数组 | 提供有效数据 |
| ED02 | 发送数据超长 | UDP数据超过64KB限制 | 分片发送或使用TCP通道 |
| ED03 | 消息解码失败 | 协议格式不匹配或数据损坏 | 检查发送端编码方式 |
| ED04 | 消息序列化失败 | 对象无法序列化 | 确保对象可序列化 |
| ED05 | 加密数据损坏 | 解密时数据格式异常 | 检查加密密钥是否匹配 |
| ED06 | 目标节点不存在 | 发送给未连接节点 | 先建立连接或检查节点ID |
| ED07 | 消息类型不支持 | 未知的消息类型 | 使用支持的协议消息类型 |

### 9.4 操作类错误 (EO)

| 错误码 | 描述 | 原因 | 建议 |
|--------|------|------|------|
| EO01 | 未连接时发送 | Connect未完成 | 先调用Connect并等待成功 |
| EO02 | 重复连接 | 已连接时再次Connect | 检查Connected状态 |
| EO03 | 连接超时 | 网络不可达或服务器无响应 | 检查网络或增大超时值 |
| EO04 | 连接被拒绝 | 服务器拒绝连接 | 检查节点ID是否被允许 |
| EO05 | 主动断开连接 | Disconnect被调用 | 正常行为，可重新连接 |
| EO06 | 被强制断开 | 服务器踢出或网络中断 | 检查服务端日志 |

### 9.5 打洞类错误 (EH)

| 错误码 | 描述 | 原因 | 建议 |
|--------|------|------|------|
| EH01 | NAT类型不支持穿透 | 对端NAT为Symmetric类型 | 自动降级为中继模式 |
| EH02 | 打洞超时 | 双方未能在时限内完成打洞 | 增大超时或使用中继 |
| EH03 | 打洞包丢失 | 网络不稳定导致握手失败 | 重试或切换中继 |
| EH04 | NAT探测失败 | 无法获取外部地址 | 检查网络连接 |

### 9.6 中继类错误 (ER)

| 错误码 | 描述 | 原因 | 建议 |
|--------|------|------|------|
| ER01 | 中继服务未启用 | EnableRelay未配置 | 启用中继降级功能 |
| ER02 | 中继服务器断开 | 信令服务器不可用 | 检查服务器状态 |
| ER03 | 中继转发超时 | 服务器转发延迟过大 | 检查服务器负载 |
| ER04 | 中继配额超限 | 节点流量超过限制 | 联系管理员或升级配额 |

### 9.7 认证类错误 (EA)

| 错误码 | 描述 | 原因 | 建议 |
|--------|------|------|------|
| EA01 | 身份认证失败 | 密钥不匹配或节点未注册 | 检查NodeIdPassword配置 |
| EA02 | 认证超时 | 服务器响应慢或网络问题 | 增大超时或检查网络 |
| EA03 | 节点ID重复 | ID已被其他节点占用 | 更换唯一节点ID |
| EA04 | 权限不足 | 操作需要更高权限 | 联系管理员获取权限 |

### 9.8 加密类错误 (EE)

| 错误码 | 描述 | 原因 | 建议 |
|--------|------|------|------|
| EE01 | 加密初始化失败 | 密钥格式错误 | 使用正确格式密钥 |
| EE02 | 解密失败 | 密钥不匹配或数据损坏 | 确保双方使用相同密钥 |
| EE03 | 加密模式不支持 | 选择了不支持的加密方式 | 使用AES-128/256 |

---

## 10. 日志设计

### 10.1 基于LogHelper调整

LogHelper需要增加以下功能：

```csharp
// 新增LogLevel枚举
public enum LogLevel
{
    Trace = 0,    // 新增：最详细追踪
    Debug = 1,
    Info = 2,
    Warn = 3,
    Error = 4,
    Off = 5       // 新增：关闭日志
}

// LogHelper新增方法
public static class LogHelper
{
    // 当前日志级别
    public static LogLevel CurrentLevel { get; set; } = LogLevel.Debug;
    
    // 设置日志级别
    public static void SetLevel(LogLevel level);
    
    // Trace级别
    public static void Trace(string des, params object[] @params);
    
    // 带nodeId的日志方法
    public static void Trace(string nodeId, string des, params object[] @params);
    public static void Debug(string nodeId, string des, params object[] @params);
    public static void Info(string nodeId, string des, params object[] @params);
    public static void Warn(string nodeId, string des, params object[] @params);
    public static void Error(string nodeId, string errorCode, string des, Exception ex = null);
    
    // 自定义日志路径
    public static void SetLogPath(string path);
    
    // 是否输出到Console
    public static bool OutputToConsole { get; set; } = true;
    
    // 日志文件大小限制
    public static void SetMaxFileSize(long maxSize);       // 单文件最大大小
    public static void SetMaxLogFileCount(int maxCount);   // 最大日志文件数
}
```

### 10.2 日志级别说明

| 级别 | 说明 | 示例场景 |
|------|------|---------|
| Trace | 详细追踪 | 每个消息收发、心跳发送 |
| Debug | 调试信息 | NAT检测结果、打洞尝试 |
| Info | 常规信息 | 连接/断开、注册成功 |
| Warning | 警告 | 打洞失败降级中继、心跳超时 |
| Error | 错误 | 认证失败、连接断开异常 |

### 10.3 节点动作日志点

```
日志记录点（每个节点的关键动作）：

Init阶段:
├─ [Info] Node {nodeId} initialized with config: {serverAddr}, {strategy}

Connect阶段:
├─ [Info] Node {nodeId} connecting to server {serverAddr}
├─ [Debug] Connection attempt #{retryCount}
├─ [Info] Node {nodeId} connected to server
├─ [Error] Node {nodeId} connection failed: {errorCode} - {message}

Auth阶段:
├─ [Info] Node {nodeId} starting authentication
├─ [Debug] Received challenge: {challengeId}
├─ [Info] Node {nodeId} authenticated successfully
├─ [Error] Node {nodeId} authentication failed: {errorCode}

Register阶段:
├─ [Info] Node {nodeId} registered to server, sessionId={sessionId}
├─ [Debug] Public address detected: {publicAddr}, NAT type: {natType}

HolePunch阶段:
├─ [Info] Node {nodeId} starting hole punch to {targetId}
├─ [Debug] Target address: {targetAddr}, NAT type pair: {sourceNat}-{targetNat}
├─ [Trace] Sending punch sync #{seq} to {targetAddr}
├─ [Info] Node {nodeId} hole punch succeeded with {targetId}
├─ [Warning] Node {nodeId} hole punch timeout, falling back to relay

Relay阶段:
├─ [Info] Node {nodeId} relay channel established, sessionId={relaySessionId}
├─ [Trace] Node {nodeId} sending relay data #{seq}, size={dataSize}

LocalDiscovery阶段:
├─ [Info] Node {nodeId} starting local discovery on port {port}
├─ [Info] Node {nodeId} discovered local node: {discoveredNodeId} at {addr}

Send/Receive阶段:
├─ [Trace] Node {nodeId} sending data to {targetId}, seq={seq}, size={size}
├─ [Trace] Node {nodeId} received data from {sourceId}, seq={seq}, size={size}

Disconnect阶段:
├─ [Info] Node {nodeId} disconnecting from server
├─ [Info] Node {nodeId} disconnected from {peerId}, reason={reason}
```

### 10.4 Builder配置日志

```csharp
.EnableLogging()                              // 默认Debug级别
.EnableLogging(LogLevel.Info)                 // 设置级别
.EnableLogging(LogLevel.Trace, "logs/p2p")   // 自定义路径
.SetLogOutputToConsole(false)                 // 关闭Console输出
```

---

## 11. 项目文件结构

### 11.1 SAEA.P2P 结构

```
SAEA.P2P/
│
├── Core/
│   ├── P2PClient.cs              // 核心客户端实现
│   ├── P2PServer.cs              // 信令服务器实现
│   ├── P2PNode.cs                // 节点信息模型
│   ├── PeerSession.cs            // 节点会话管理
│   └── SessionManager.cs         // 会话管理器
│
├── Builder/
│   ├── P2PClientBuilder.cs       // 客户端Builder
│   ├── P2PServerBuilder.cs       // 服务器Builder
│   ├── P2POptions.cs             // 配置选项模型
│   ├── HolePunchOptions.cs       // 打洞配置
│   ├── RelayOptions.cs           // 中继配置
│   ├── DiscoveryOptions.cs       // 发现配置
│   ├── SecurityOptions.cs        // 安全配置
│   └── LoggingOptions.cs         // 日志配置
│
├── Protocol/
│   ├── P2PMessageType.cs         // 消息类型枚举
│   ├── P2PProtocol.cs            // 协议实现
│   ├── P2PCoder.cs               // 编解码器
│   ├── P2PMessageFactory.cs      // 消息工厂
│   └── Messages/
│       ├── RegisterMessage.cs
│       ├── PunchMessage.cs
│       ├── RelayMessage.cs
│       ├── AuthMessage.cs
│       ├── DiscoveryMessage.cs
│       └── UserDataMessage.cs
│
├── NAT/
│   ├── HolePuncher.cs            // 打洞核心逻辑
│   ├── NATType.cs                // NAT类型枚举
│   ├── NATDetector.cs            // NAT检测器
│   ├── HolePunchStrategy.cs      // 打洞策略枚举
│   └── HolePunchResult.cs        // 打洞结果
│
├── Relay/
│   ├── RelayManager.cs           // 中继管理器
│   ├── RelaySession.cs           // 中继会话
│   └── RelayChannel.cs           // 中继通道
│
├── Discovery/
│   ├── LocalDiscovery.cs         // 局域网发现
│   ├── DiscoveryBroadcaster.cs   // 广播发送器
│   ├── DiscoveryListener.cs      // 发现监听器
│   └── DiscoveredNode.cs         // 发现的节点信息
│
├── Security/
│   ├── AuthManager.cs            // 认证管理器
│   ├── CryptoService.cs          // 加密服务
│   ├── KeyExchange.cs            // 密钥交换
│   └── AuthChallenge.cs          // 认证挑战
│
├── Channel/
│   ├── UDPChannel.cs             // UDP通道
│   ├── TCPChannel.cs             // TCP通道
│   └── ChannelType.cs            // 通道类型枚举
│
├── Shortcut/
│   ├── P2PQuick.cs               // 快捷入口
│   ├── QuickClient.cs            // 快速客户端
│   └── QuickServer.cs            // 快速服务器
│
├── Common/
│   ├── ErrorCode.cs              // 错误码定义
│   ├── P2PException.cs           // P2P异常类
│   ├── P2PLogHelper.cs           // 日志封装
│   └── NodeState.cs              // 节点状态枚举
│
├── Model/
│   ├── NodeInfo.cs               // 节点信息
│   ├── PeerInfo.cs               // 对端信息
│   └── ServerInfo.cs             // 服务器信息
│
└── README.md                     // 项目文档
```

### 11.2 SAEA.P2PTest 结构

```
SAEA.P2PTest/
│
├── Program.cs                   // 测试入口（菜单式）
│
├── Tests/
│   ├── ClientBuilderTest.cs     // Builder配置测试
│   ├── ServerBuilderTest.cs     // 服务器Builder测试
│   ├── QuickStartTest.cs        // 快捷方式测试
│   ├── HolePunchTest.cs         // 打洞功能测试
│   ├── RelayTest.cs             // 中继功能测试
│   ├── LocalDiscoveryTest.cs    // 局域网发现测试
│   ├── AuthTest.cs              // 认证测试
│   ├── EncryptionTest.cs        // 加密测试
│   ├── MessageTest.cs           // 消息收发测试
│   ├── MultiNodeTest.cs         // 多节点测试
│   └── ErrorCodeTest.cs         // 错误码测试
│
├── Scenarios/
│   ├── ChatScenario.cs          // 聊天场景模拟
│   ├── FileShareScenario.cs     // 文件共享场景
│   └── MultiPeerScenario.cs     // 多节点协作场景
│
└── Utils/
    ├── TestHelper.cs             // 测试辅助工具
    └── MockServer.cs             // 模拟服务器
```

---

## 12. 复用策略

### 12.1 复用 SAEA.Sockets

| 功能 | 复用来源 | 说明 |
|------|----------|------|
| TCP Server | IocpServerSocket | 信令服务器基础 |
| TCP Client | IocpClientSocket | 信令客户端基础 |
| UDP Server | UdpServerSocket | 打洞/发现基础 |
| UDP Client | UdpClientSocket | 打洞客户端基础 |
| 协议编解码 | BaseCoder, BaseSocketProtocal | 继承扩展 |
| 会话管理 | SessionManager | 节点会话管理 |
| 内存池 | BufferManager, UserTokenPool | 高性能内存管理 |
| SocketOption | SocketOptionBuilder | 配置构建器模式 |

### 12.2 复用 SAEA.Common

| 功能 | 复用来源 | 说明 |
|------|----------|------|
| AES加密 | AESHelper | 对称加密 |
| 日志 | LogHelper | 日志记录（需调整增加功能） |
| 序列化 | SerializeHelper | JSON序列化 |
| 日期时间 | DateTimeHelper | 时间戳处理 |
| 内存池 | MemoryPoolManager | 内存池管理 |
| 字节转换 | ByteHelper | 字节操作 |

---

## 13. 使用示例

### 13.1 快捷方式

```csharp
using SAEA.P2P;

// 场景1：连接到远程信令服务器，启用所有功能
var client = P2PQuick.Client("signal.example.com", 39654, "my-node-id");

// 场景2：仅局域网P2P，无需信令服务器
var client = P2PQuick.LocalNet(); // 自动发现局域网节点

// 场景3：快速启动信令服务器
var server = P2PQuick.Server(39654);

client.OnPeerConnected += (peerId) => Console.WriteLine($"Connected: {peerId}");
client.OnMessageReceived += (peerId, data) => 
    Console.WriteLine($"Message from {peerId}: {Encoding.UTF8.GetString(data)}");

client.Connect();
client.Send("target-node-id", Encoding.UTF8.GetBytes("Hello P2P!"));
```

### 13.2 Builder完整配置

```csharp
using SAEA.P2P;

// 客户端完整配置
var client = new P2PClientBuilder()
    .SetServer("signal.example.com", 39654)   // 支持域名/IP
    .SetNodeId("node-001")
    .SetNodeIdPassword("secret-key")          // 身份认证密码
    .EnableHolePunch()                         // UDP打洞（自动策略）
    .EnableHolePunch(HolePunchStrategy.PreferDirect) // 指定策略
    .EnableRelay()                             // 中继降级
    .EnableEncryption()                        // AES加密
    .EnableEncryption("custom-aes-key")        // 自定义密钥
    .EnableLocalDiscovery()                    // 局域网发现
    .EnableLocalDiscovery(options =>
        options.SetPort(39655)
               .SetMulticastGroup("224.0.0.250"))
    .SetTimeout(5000)                          // 连接超时
    .SetFreeTime(180000)                       // 心跳间隔
    .EnableLogging(LogLevel.Debug)             // 日志配置
    .Build();

// 服务器配置
var server = new P2PServerBuilder()
    .SetPort(39654)
    .SetIP("0.0.0.0")                          // 支持IPv6
    .SetMaxNodes(1000)
    .EnableRelay()
    .SetMaxRelayConnections(100)
    .EnableLogging(LogLevel.Info)
    .Build();
```

### 13.3 P2PTest菜单示例

```csharp
// SAEA.P2PTest/Program.cs
class Program
{
    static async Task Main(string[] args)
    {
        while (true)
        {
            ConsoleHelper.Title = "SAEA.P2P Test";
            ConsoleHelper.WriteLine("SAEA.P2P Test");
            ConsoleHelper.WriteLine("1 = Start client test");
            ConsoleHelper.WriteLine("2 = Start server test");
            ConsoleHelper.WriteLine("3 = Start hole punch test");
            ConsoleHelper.WriteLine("4 = Start relay test");
            ConsoleHelper.WriteLine("5 = Start local discovery test");
            ConsoleHelper.WriteLine("6 = Start auth & encryption test");
            ConsoleHelper.WriteLine("7 = Start multi-node test");
            ConsoleHelper.WriteLine("8 = Start chat scenario");
            ConsoleHelper.WriteLine("9 = Quick start demo");
            
            var key = ConsoleHelper.ReadLine();
            switch (key)
            {
                case "1": await ClientTest.RunAsync(); break;
                case "2": await ServerTest.RunAsync(); break;
                case "3": await HolePunchTest.RunAsync(); break;
                case "4": await RelayTest.RunAsync(); break;
                case "5": await LocalDiscoveryTest.RunAsync(); break;
                case "6": await AuthTest.RunAsync(); break;
                case "7": await MultiNodeTest.RunAsync(); break;
                case "8": await ChatScenario.RunAsync(); break;
                case "9": await QuickStartTest.RunAsync(); break;
            }
        }
    }
}
```

---

## 14. README.md 结构

参照 SAEA.Sockets README 格式，包含以下章节：

```
# SAEA.P2P - P2P通信组件

## 快速导航

## 30秒快速开始
- P2PQuick快捷入口示例
- Builder链式配置示例

## 核心特性
- UDP打洞、中继降级、局域网发现、认证加密、混合模式、Builder配置

## 架构设计

## 应用场景
- 游戏服务器、即时通讯、IoT设备、局域网协作

## NAT穿透说明
- NAT类型分类、穿透策略、成功率

## 核心类一览

## 使用示例
- 快捷方式、Builder配置、各功能模块示例

## 错误码说明

## 默认协议格式

## 依赖项
- SAEA.Sockets、SAEA.Common

## 许可证
```

---

## 15. 实现优先级

### Phase 1: 核心框架
1. P2POptions + Builder
2. P2PProtocol + P2PCoder
3. P2PClient/P2PServer 基础框架
4. ErrorCode + P2PException

### Phase 2: NAT穿透
1. NATDetector
2. HolePuncher
3. 打洞流程实现

### Phase 3: 中继与发现
1. RelayManager
2. LocalDiscovery
3. UDP/TCP Channel

### Phase 4: 安全与日志
1. AuthManager
2. CryptoService
3. LogHelper调整
4. P2PLogHelper

### Phase 5: 测试与文档
1. SAEA.P2PTest 各测试类
2. README.md
3. 示例场景

---

## 附录

### A. 依赖关系图

```
SAEA.P2P
    │
    ├── SAEA.Sockets (TCP/UDP通信、协议编解码、会话管理)
    │       │
    │       └── SAEA.Common (工具类)
    │
    └── SAEA.Common (加密、日志、序列化、内存池)
```

### B. 设计决策记录

| 决策 | 选择 | 原因 |
|------|------|------|
| P2P模式 | 混合模式 | 灵活适应不同场景 |
| NAT穿透策略 | 自动+可配置 | 用户体验与控制平衡 |
| 信令服务器 | 内置 | 一体化解决方案 |
| 日志 | LogHelper调整 | 统一日志体系 |
| 复用策略 | 最大化复用 | 减少重复开发，保持一致性 |

---

**文档结束**