# SAEA.P2P - ICE 式打洞与 STUN/TURN 中继的 P2P 通信组件

<p align="center">
  <img src="https://img.shields.io/badge/版本-26.11.5.1-blue?style=for-the-badge" alt="Version">
  <img src="https://img.shields.io/badge/NuGet-SAEA.P2P-green?style=for-the-badge" alt="NuGet">
  <img src="https://img.shields.io/badge/.NET-Standard%202.0-purple?style=for-the-badge" alt=".NET">
  <img src="https://img.shields.io/badge/协议-Apache%202-orange?style=for-the-badge" alt="License">
</p>

<p align="center">
  <b>🚀 先打洞，打不通再中继：节点通信不用先架一台转发服务器</b>
</p>

<p align="center">
  <a href="README.en.md">English</a> | <b>中文</b>
</p>

---

## 🎯 它解决什么问题

两个节点各自躲在 NAT 后面，想直接通信却不知道对方的公网地址，也不知道这层 NAT 放不放行。SAEA.P2P 把这件事拆成三步：

1. 用 ICE 的候选收集与连通性检查把 UDP / TCP 直连打通；
2. 直连打不通时，按阶梯回退到自研 STUN/TURN 的 TURN 中继（UDP → TCP → TLS）；
3. 没有信令服务器的局域网里，用组播发现交换同一套 ICE 参数。

STUN 与 TURN 都是本仓实现的，不装 coturn 也能跑通中继；需要与外部实现对齐时，口径见下文「服务端」一节的「与 coturn 的对齐点」。

### 能力一览

| 能力 | 说明 | 主要实现 |
|------|------|----------|
| 🧭 **候选收集** | host / srflx / prflx / relay 四类候选；映射地址来自 STUN Binding，中继地址来自 TURN Allocate | `Ice/CandidateGatherer`、`Stun/StunClient`、`Turn/TurnClient` |
| 🕳️ **UDP 打洞** | 按候选对做连通性检查，选中的候选对承载业务帧 | `Ice/CheckList`、`Ice/PairSelector` |
| 🔌 **TCP 打洞** | active ↔ passive 单向主动连接；同时打开需显式开启，Windows 不支持 | `Ice/TcpCandidateGatherer` |
| 🔄 **TURN 回退** | 直连失败后按阶梯尝试 TURN over UDP / TCP / TLS | `Ice/RouteLadder` |
| 🌐 **IPv6** | 收集 IPv6 候选；`StunServerOptions.EnableDualMode` 可双栈监听 | `Ice/IceOptions`、`StunServer/StunServer` |
| 📡 **局域网无信令发现** | 组播 / 广播发现报文交换候选与 ufrag/pwd，不经过信令服务器 | `Discovery/LocalDiscovery` |
| 🔑 **TURN 双鉴权** | 临时凭据（HMAC-SHA1 派生）与标准 long-term credential 两种入口 | `Turn/TurnCredentialService`、`Turn/TurnAuthContext` |
| 📦 **零外部依赖** | STUN / TURN 服务端内置，无需额外部署第三方组件 | `StunServer/`、`TurnServer/` |

---

## 🚀 快速开始

```csharp
using System;
using System.Text;
using SAEA.P2P;
using SAEA.P2P.Channel;

// 1. 创建客户端并注册到信令服务器
var client = P2PQuick.Client("10.0.0.1", 39654, "user-A");

// 对端会话建立并选出通道时触发，P2PConnection.Channel 即选路结果
client.OnPeerConnected += conn =>
    Console.WriteLine($"🔗 已连上 {conn.PeerId}，通道 {conn.Channel}");

// 收到业务帧：data 是 ReadOnlySpan<byte>，只在回调期间有效，留存必须先复制
client.OnDataReceived += (peerId, data) =>
    Console.WriteLine($"📨 {peerId}: {Encoding.UTF8.GetString(data.ToArray())}");

// 错误回调参数为错误码与中文说明
client.OnError += (code, message) => Console.WriteLine($"❌ {code}: {message}");

await client.RegisterAsync();

// 2. 与对端建链：先经信令交换 ICE 参数，再由 ICE 会话选路
var connection = await client.ConnectToPeerAsync("user-B");
Console.WriteLine($"✅ 通道类型：{connection.Channel}，对端端点：{connection.RemoteEndPoint}");

// 3. 发送业务帧
client.Send("user-B", Encoding.UTF8.GetBytes("Hello! 👋"));
```

- 需要节点口令时用 `P2PQuick.Client(host, port, nodeId, password)`，注册阶段启用挑战应答认证。
- 需要调 ICE 预算或直接给 TURN 凭据时用 `P2PQuick.ClientFull(...)`，或直接用 `P2PClientBuilder` 链式装配后 `BuildClient()`。
- `Send` 的载荷是 `ReadOnlySpan<byte>`，仅在调用期间读取；`OnDataReceived` 的 `data` 同理只在回调期间有效。

---

## 🔀 连接流程

1. **注册**：`RegisterAsync()` 连接信令服务器（默认 TCP，`EnableTls()` 可改走 TLS），按 peerId 完成挑战应答。
2. **交换 ICE 参数**：发起方发 `ConnectRequest` 与携带 ufrag / pwd / role / tiebreaker 的 `SessionOffer`，应答方回 `SessionAnswer`；后续候选以 trickle 增量补发。这些信令消息的类型落在 `0x90` 段。
3. **连通性检查**：双方按候选对互发 STUN Binding，`MESSAGE-INTEGRITY` 的密钥由 ufrag / pwd 派生。
4. **提名与通道接管**：选中的候选对写入 `ChannelType`，数据面从 STUN 报文切到 `0xC0` 前缀的业务帧。
5. **失败回退**：某一级打不通就按 `IceStage` 阶梯进入下一级，直到 TURN over TLS。

```
  A                        信令服务器                        B
  │  ① ConnectRequest ────────>│                            │
  │                           │──── ConnectRequest ───────>│
  │  ② SessionOffer(ufrag/pwd) >│                           │
  │                           │──── SessionOffer ─────────>│
  │                           │<─── SessionAnswer(ufrag/pwd)│
  │<── SessionAnswer ─────────│                            │
  │  ③ trickle 候选双向增量互发（信令消息 0x90 段）         │
  │  ④ UDP 直连连通性检查（STUN Binding + MESSAGE-INTEGRITY）│
  │  ⑤ 提名选中候选对 → ⑥ 数据面接管                        │
  │<════════ 业务帧（1 字节前缀 0xC0）════════════════════>│
  │  ⑦ 直连全失败：TURN over UDP → TCP → TLS 逐级回退       │
```

---

## 🛣️ 通道与选路阶梯

### ChannelType 取值

| 枚举值 | 数值 | 含义 | 何时选中 |
|--------|------|------|----------|
| `UdpDirect` | 0 | UDP 直连 | UDP 候选对连通性检查通过 |
| `TcpDirect` | 1 | TCP 直连（active ↔ passive） | 一端主动连接、另一端被动监听成功 |
| `RelayUdp` | 2 | TURN over UDP 中继 | 直连全失败且 TURN UDP 分配可用 |
| `RelayTcp` | 3 | TURN over TCP 中继 | UDP 中继不可用，控制连接走 TCP |
| `RelayTls` | 4 | TURN over TLS 中继 | 前两级中继都不可用，或要求控制连接加密 |
| `Local` | 5 | 局域网发现直连 | 无信令模式，参数经发现报文交换，数据面仍是 UDP 直连 |

`Local` 与 `UdpDirect` 的差别只在 ICE 参数的来源与上报的通道标识，数据面同样是 UDP 直连。

### 阶梯顺序与预算

`IceStage` 的枚举顺序就是回退优先级：`UdpDirect` → `TcpDirect` → `TurnUdp` → `TurnTcp` → `TurnTls`。某一级打通立即停止，五级全失败抛 `NoCandidatePair`，超出会话总预算抛 `IceTimeout`。

| 预算项 | 字段 | 默认值 |
|--------|------|--------|
| 候选收集 | `IceSessionOptions.GatheringBudget` | 3s |
| UDP 直连阶段 | `IceSessionOptions.UdpDirectBudget` | 10s |
| TCP 直连阶段 | `IceSessionOptions.TcpDirectBudget` | 10s |
| 单级 TURN 阶段 | `IceSessionOptions.TurnStageBudget` | 15s |
| 会话总预算 | `IceSessionOptions.SessionBudget` | 90s |

未配置 `TurnClientOptions` 时 TURN 三级直接跳过，阶梯只剩直连两级。

### 什么时候会用到中继

- 双方都是对称型 NAT，或端口受限 NAT 且打洞失败；
- 网络只放行 TCP，或只放行 443 这类固定出向端口；
- 部署要求所有流量走固定出口以便审计与限速。

---

## 📡 局域网直连（无信令）

`P2PQuick.LocalNet` 已按默认配置（UDP 39655、组播 224.0.0.250）启动发现，返回的客户端可以直接 `ConnectToPeerAsync`。

```csharp
using SAEA.P2P;
using SAEA.P2P.Channel;

// 自定义发现端口与组播地址：P2PQuick.LocalNet("file-server", 39655, "224.0.0.250")
var client = P2PQuick.LocalNet("file-server");

// 发现事件只上报节点，是否连接由应用决定；两端都要调 ConnectToPeerAsync 才会建链
client.OnPeerDiscovered += node =>
{
    Console.WriteLine($"🔍 发现 {node.NodeId}（{node.LocalAddress}:{node.LocalPort}）");
    _ = client.ConnectToPeerAsync(node.NodeId);
};

client.OnPeerConnected += conn =>
    Console.WriteLine($"✅ 已连上 {conn.PeerId}，通道 {conn.Channel}");   // ChannelType.Local
```

流程是：用 `new P2PClient(nodeId, LocalDiscoveryOptions, IceSessionOptions)` 装配发现参数 → `StartLocalAsync()` 启动发现 → `OnPeerDiscovered` 上报节点 → 应用调 `ConnectToPeerAsync(peerId)` → ICE 用发现报文里的参数与候选选路 → 通道标识为 `ChannelType.Local`。发现报文周期性补发，节点超时后从列表移除，但已建立的数据面不会因发现超时而拆除。

### 🔐 无信令模式的安全边界

- **没有身份鉴权**：同一广播域内任何主机都能发发现报文并自称任意 `NodeId`。
- **HMAC 只保证完整性，不提供机密性**：配置 `LocalDiscoveryOptions.SharedKey` 会给报文加 HMAC-SHA256 标签，但不加密内容；候选地址与 ufrag / pwd 仍是明文。
- `SharedKey` 为空时不带标签，任何主机都能伪造报文。
- **只应在本链路使用**：需要认证或跨公网时请改用信令模式。

---

## 🖥️ 服务端

服务端有两个角色，都在本仓实现：

- **信令服务端**：`P2PServer` 由 `P2PServerBuilder` 装配，负责节点注册、在线状态维护与 ICE 参数转发，默认监听 `39654`；配置项在 `P2PServerOptions`（`Port`、`MaxNodes`、`Tls`、`SharedSecret`、`Realm`、`CredentialTtlSeconds`、`TurnUrls`）。
- **STUN / TURN 监听**：`StunServer` 与 `TurnServer` 两个类，`Src/SAEA.P2PTest` 是现成宿主，按 JSON 配置决定监听地址、端口、realm、共享密钥与中继端口区间。

宿主这样启动：

```bash
dotnet run --project Src/SAEA.P2PTest -c Release -- --server --config <配置路径>
```

`--help` 会列出全部开关（`--listen`、`--stun-port`、`--turn-port`、`--tls-port`、`--realm`、`--shared-secret`、`--credential-ttl`、`--nonce-lifetime`、`--relay-min`、`--relay-max`、`--max-allocations`、`--max-allocations-per-user`、`--max-bytes`、`--max-lifetime`、`--tls-cert`、`--tls-cert-password`、`--change-address`、`--ipv6`、`--dual-mode`）；省略 `--config` 时用内建默认值加命令行覆盖。共享密钥与证书口令不会打印。

### 配置键名与默认值

下表是自测宿主的配置键（STUN / TURN 监听）：

| 键名 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `ListenAddress` | string | `0.0.0.0` | 监听地址；`EnableIpv6` 为 true 且仍是默认值时改监听 `IPv6Any` |
| `StunPort` | int | `3478` | STUN 独立监听端口；`0` 表示不单独监听 |
| `TurnPort` | int | `3478` | TURN 的 UDP 与 TCP 同号监听，协议不同不冲突 |
| `TurnTlsPort` | int | `0` | TURN over TLS 端口；`0` 表示关闭，启用时必须给证书 |
| `Realm` | string | 空（必填） | 认证域，需与签发临时凭据的信令服务端 `P2PServerOptions.Realm` 一致 |
| `SharedSecret` | string | 空（必填） | 校验 TURN 临时凭据用的共享密钥；信令鉴权的密钥在 `P2PServerOptions` 单独配置 |
| `EnableIpv6` | bool | `false` | 是否改用 IPv6 监听 |
| `EnableDualMode` | bool | `false` | 是否启用双栈监听：STUN 独立监听与 TURN 的 UDP 监听在监听 IPv6 任意地址时用一个套接字同时收 IPv4/IPv6，其他监听地址下不生效 |
| `TlsCertificatePath` | string | `null` | PFX 证书路径；启用 TLS 时必填且文件必须存在 |
| `TlsCertificatePassword` | string | `null` | PFX 口令，无口令时留空 |
| `CredentialTtlSeconds` | int | `600` | 下发 TURN 临时凭据的有效期 |
| `NonceLifetimeSeconds` | int | `600` | TURN nonce 有效期 |
| `RelayPortRange` | 对象 | `Min=49152`、`Max=65535` | 中继端口区间 |
| `MaxAllocations` | int | `1024` | 并发分配总数上限 |
| `MaxAllocationsPerUser` | int | `8` | 单用户并发分配上限 |
| `MaxLifetimeSeconds` | uint | `3600` | 分配生命周期上限，请求更长时按上限回填 |

另有两个键同属配置对象：`MaxBytes`（默认 `0`，表示不限制单个分配的累计中继字节）与 `ChangeRequestAddressPair`（默认 `null`，RFC 5780 第二地址）。

### 鉴权模型

**信令注册鉴权**：客户端用 `SetNodeIdPassword` / `P2PQuick.Client(..., password)` 设置口令后，注册阶段走挑战应答，答复值为 `Base64(HMAC-SHA256(SharedSecret, nonce + "|" + peerId))`；`SharedSecret` 来自信令服务端的 `P2PServerOptions`，为空即视为不需要鉴权。

**TURN 第一层：临时凭据（coturn `use-auth-secret` 口径）**：`P2PServerOptions.SharedSecret` 非空且配置了 `TurnUrls` 时，信令服务端在鉴权通过后单独下发一条 `TurnCredential` 信令报文，带 realm、用户名、口令、ttl 与 `Urls`。

- 用户名 = 「过期 Unix 秒数」+ `:` + 「用户标识」，用户标识不能含分隔符；
- 口令 = `Base64(HMAC-SHA1(共享密钥, 用户名))`（是 SHA1，不是 SHA256）；
- `TurnUrls` 未配置或未配共享密钥时不下发凭据，客户端只会走直连。

**TURN 第二层：标准 long-term credential**：直接给 `TurnClientOptions.Username` / `TurnClientOptions.Password`，用于与 coturn 等外部实现互通；与信令下发的 `TurnCredential` 二选一。

### STUN / TURN 同端口共享 Binding

`StunPort` 与 `TurnPort` 相同时（默认都是 3478），宿主不再单独绑定 STUN 端口，Binding 请求由 TURN 的 UDP 监听应答——两者共用同一个 `StunServer` 实例：数据报先给 TURN 的 ChannelData 入口一次机会，再按 STUN 解析，`Method == Binding` 的请求由 STUN 侧自己应答，其余方法才分派给 TURN。`StunPort` 配成其他值时才额外起独立 STUN 监听。

### 与 coturn 的对齐点

| 对齐项 | 对应规范 | 本仓实现要点 |
|--------|----------|--------------|
| STUN 方法与属性 | RFC 8489 | Binding 请求 / 成功应答、XOR-MAPPED-ADDRESS、SOFTWARE、FINGERPRINT |
| TURN 方法与属性 | RFC 8656 | Allocate / Refresh / CreatePermission / ChannelBind / Send，XOR-RELAYED-ADDRESS、XOR-PEER-ADDRESS、LIFETIME |
| TCP 中继与数据连接 | RFC 6062 | `REQUESTED-TRANSPORT=6` 申请分配，Connect 回 `CONNECTION-ID`，另开连接发 ConnectionBind |
| 候选、配对与提名 | RFC 8445 | 候选优先级、配对状态机、控制 / 受控角色与 tiebreaker 裁决 |
| 首字节复用区间 | RFC 7983 | `0x00`–`0x3F` 为 STUN，`0x40`–`0x4F` 为 ChannelData，`0xC0` 为业务帧 |
| realm / nonce 与长期凭据 | RFC 8489 long-term credential | 401 挑战带 REALM / NONCE，nonce 绑定来源地址并在 `NonceLifetimeSeconds` 后失效 |
| 完整性校验向量 | RFC 5769 | MESSAGE-INTEGRITY / FINGERPRINT 与官方样例逐字节一致 |
| 分帧 | RFC 4571 与 STUN 自定界 | TCP 直连业务帧加 2 字节长度前缀；TURN over TCP/TLS 控制连接不加长度前缀 |

---

## 📦 传输与分帧

| 场景 | 分帧方式 | 说明 |
|------|----------|------|
| UDP 直连 / `Local` | 1 字节前缀 `0xC0` | 单报上限 65506 字节，扣除前缀后业务载荷上限 65505 |
| TCP 直连 | RFC 4571 | 每帧 2 字节大端长度前缀，帧内仍是 `0xC0` + 载荷 |
| TURN over UDP | TURN ChannelData | 首字节 `0x40`–`0x4F`，Length 字段定界 |
| TURN over TCP / TLS 控制连接 | 不加长度前缀 | 按 STUN 自身 20 字节头 + Length 自定界，通道数据按 ChannelData 4 字节头自定界 |

UDP 上按首字节解复用：`0x00`–`0x3F` 交给 STUN，`0xC0` 视为业务帧，其余丢弃。

---

## 🌐 IPv6

- **候选收集**：`IceOptions.EnableIpv6` 默认 true，同时收集 IPv4 与 IPv6 地址；关闭后不再收集 IPv6。
- **双栈监听**：`StunServerOptions.EnableDualMode` 在主端点为 `::` 且未配置备用端点时生效，用一个套接字同时收发 IPv4 与 IPv6；自测宿主把 `EnableDualMode` 原样传给 STUN 独立监听与 TURN UDP 监听的 `StunServerOptions`，仅在主端点为 IPv6 任意地址时生效。TURN over TCP/TLS 监听在 IPv6 任意地址上固定开启双栈，不受该开关影响。
- **v4-mapped 归一化**：候选构造、候选对配对、TCP 被动匹配三处都会把 `::ffff:a.b.c.d` 还原成对应 IPv4 地址，避免同一地址被当成两个族。
- **跨族不配对**：`IceOptions.AllowCrossFamilyPairs` 默认 `false`，IPv4 候选不会与 IPv6 候选组成候选对。
- **中继**：IPv6 中继地址可以分配；`Allocate` 的 `REQUESTED-ADDRESS-FAMILY` 与中继地址族不一致时回 440（`AddressFamilyNotSupported`），`CreatePermission` / `Connect` 的对端地址族与中继地址族不一致时回 443（`PeerAddressFamilyMismatch`）。
- **NAT66**：没有自动化用例，需要管理员权限配置网络；公网 IPv6 场景按直连处理。

---

## 🔐 安全边界

- **数据面不做应用层加密**：业务帧明文传输，需要保密请自己在载荷层处理。
- **TLS 只用于控制连接**：信令连接（`EnableTls()`）与 TURN over TLS（`ChannelType.RelayTls`）的控制连接；UDP / TCP 直连与 TURN over UDP/TCP 的中继数据都不加密。
- **凭据与共享密钥不入日志**：日志只记 realm、ttl、端点与通道类型，用户名、口令与共享密钥不落盘。
- **信令注册鉴权是按 peerId 的挑战应答**，不是身份体系；`SharedSecret` 为空即不鉴权，不能当作访问控制使用。
- 无信令局域网模式的边界见上文「局域网直连（无信令）」的「无信令模式的安全边界」。

---

## ⚠️ 已知限制

- Windows 下 TCP 打洞只保证 active ↔ passive 组合；两端同为 passive 的用例标为不支持。
- TCP 端到端用例不经 NAT 穿越（没有传输注入口），只覆盖直连路径。
- NAT66 无自动化用例，需要管理员权限配置网络。
- 不实现 SDP / WebRTC，不与浏览器互通。
- 不实现 aggressive nomination，提名按常规流程进行。
- 与公共 STUN / coturn 的互通**本轮未运行**，只交付核对清单：[STUN/TURN 互通核对清单](../../docs/superpowers/specs/2026-11-11-p2p-interop-checklist.md)。

---

## 🧪 测试与验收

```bash
# 全量：243 通过 / 2 跳过 / 0 失败，约 4 分 17 秒
dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release

# 单元类：190 条（编解码、协议头、状态机与本地装配）
dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "TestCategory=Unit"

# 传输矩阵：29 条（含 1 条跳过，耗时最长）
dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "TestCategory=Matrix"

# IPv6 + 局域网发现 + NAT 模拟：26 条（含 1 条跳过）
dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "TestCategory=Ipv6|TestCategory=LocalNet|TestCategory=Nat"
```

分组由各测试类的类级 `TestCategory` 标注，常量定义在 `Src/SAEA.P2P.Tests/P2PTestSuite.cs`。跑单个用例用 `--filter "FullyQualifiedName~<用例名>"`。

---

## 📦 安装

```bash
# NuGet 安装
dotnet add package SAEA.P2P

# 或通过 Package Manager
Install-Package SAEA.P2P
```

---

## 🔗 相关资源

| 资源 | 链接 |
|------|------|
| 📂 **GitHub** | [yswenli/SAEA](https://github.com/yswenli/SAEA) |
| 📦 **NuGet** | [SAEA.P2P](https://www.nuget.org/packages/SAEA.P2P) |
| 📖 **传输层文档** | [SAEA.Sockets](../SAEA.Sockets/README.md) |
| 🧪 **自测宿主** | [SAEA.P2PTest](../SAEA.P2PTest) |
| 📝 **博客** | [作者博客](https://www.cnblogs.com/yswenli/) |

---

## 📄 许可证

Apache License 2.0 - 自由使用、修改、分发！

---

<p align="center">
  <b>💡 有问题？欢迎提 Issue 或 PR！</b>
</p>

<p align="center">
  Made with ❤️ by <a href="https://github.com/yswenli">yswenli</a>
</p>