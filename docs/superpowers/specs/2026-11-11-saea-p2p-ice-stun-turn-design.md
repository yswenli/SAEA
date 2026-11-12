# SAEA.P2P 重构设计：ICE 式协商 + STUN + TURN + UDP/TCP 打洞

- 日期：2026-11-11（第四版，按用户拍板的工程布局与候选开关回写：不新建库工程、服务端宿主复用 `SAEA.P2PTest`、`IceOptions.AllowLoopbackCandidates`）
- 状态：用户已放行进入计划阶段（实现未开始）
- 涉及组件：`Src/SAEA.P2P`（新增 `Stun/`、`Turn/`、`StunServer/`、`TurnServer/`、`Auth/`、`Ice/`、`Transport/`、`Signaling/`）、`Src/SAEA.P2PTest`（新增 `--server` 宿主模式）、新增测试工程 `Src/SAEA.P2P.Tests`、连带改动 `Src/SAEA.Sockets`
- 规范依据：RFC 8489（STUN）、RFC 8656（TURN）、RFC 6062（TURN TCP 中继）、RFC 8445（ICE）、RFC 6544（ICE-TCP）、RFC 5780（NAT 行为发现）、RFC 7675（consent freshness）、RFC 8838（trickle）、RFC 4571（TCP 上承载数据帧）、RFC 7983（首字节解复用）

---

## 一、意图与背景

现有 `SAEA.P2P` 在“不同运营商、不同 NAT 后的两台真实客户端”场景下不可用。根因是穿透能力整体缺失，不是单点缺陷：

- `NAT/HolePuncher.cs` 的 `PunchAsync` 无条件返回失败，且 `P2PClient` 从未调用它。
- `Channel/TCPChannel.cs` 是普通 TCP 连接器，没有本地端口绑定、复用、重试或同时打开，且全仓无调用方。
- `Core/P2PClient.ProcessPunchReady` 只建会话对象，`Channel` 停在默认值 `ChannelType.Direct`；`Send` 实际始终经信令服务器转发。
- `NAT/NATDetector.DetectAsync` 恒返回 `FullCone`，服务端固定回 `Unknown`。

本次重构按 ICE 模型收集候选、配对检查、提名，UDP 与 TCP 都能建立直连，失败按固定阶梯降级到自实现的 TURN 中继，并与标准 STUN/TURN 实现对齐互通。

## 二、目标

1. 客户端具备 ICE 式协商：候选收集（host / srflx / prflx / relay）→ 配对与优先级 → 连通性检查 → 提名 → 数据通道接管。
2. UDP 打洞可用；TCP 打洞按 RFC 6544 实现 `active` / `passive` / `so` 三类候选，其中 `so` 受平台限制（见 §7.5 与 D12）。
3. 自实现 STUN 客户端与服务端，含 RFC 5780 的映射行为与过滤行为发现；结果只作只读诊断展示，不参与候选优先级计算、方向偏好与选路判定。
4. 自实现 TURN 客户端与服务端，客户端到 TURN 支持 UDP、TCP、TLS/443 三种传输；中继对端支持 UDP 与 TCP（RFC 6062，TCP 中继必须用 TCP/TLS 控制连接，不存在“UDP 控制 + TCP 中继”组合）。
5. TURN 鉴权采用共享密钥 + 带有效期的临时凭据（coturn REST 风格）+ 标准 long-term credential 两层。
6. 与标准实现对齐：属性、方法、错误码、realm/nonce、分帧逐项按 RFC 实现，交付互通核对清单（本轮不做联网实测，见 D14）。
7. IPv4 与 IPv6：候选收集、srflx、relay、连通性检查、TURN 传输都支持 v6，地址族严格隔离。
8. 删除信令服务器上的自定义中继（`RelayManager`、`RelaySession`、`RelayData`），回退统一由 TURN 承担。
9. 提供本地模拟 NAT 的自动化测试闭环（UDP 与 TCP 都覆盖），作为验收依据。
10. 保留“无信令服务器”的局域网用法：发现报文携带 ICE 参数，实现真正的局域网直连。

## 三、非目标与反目标

非目标：

- 不做公网实测验收；自动化测试不依赖外网服务。
- 不改 `AuthManager` / `CryptoService` 的算法；只改承载报文（见 §7.9、§10）。
- 数据面不加应用层加密（D15），README 按实修正。
- 不实现 SDP/WebRTC 兼容层，不做浏览器互通。
- 不实现 aggressive nomination（RFC 8445 已移除该模式）。
- 不实现 OS 级真实 NAT 拦截（需管理员与 WFP/虚拟网卡），模拟 NAT 采用代理模型（§8.1）。
- 不做 NAT66 自动化（非管理员无法创建 `::1` 外的 v6 地址，见 D13）。
- STUN/TURN 服务端按“功能性服务端”交付：规范正确性、配额与错误码、明确资源上限；抗攻击加固、限流治理、监控、压测、崩溃恢复不做。
- 不实现 TURN 带宽计费；RFC 8656 没有字节/带宽配额错误码，配额超限统一按 §7.6 映射到 486/508。

反目标（技术上能跑但视为失败）：

- 候选交换成功却仍把业务数据经信令服务器转发，`Direct` 只是字段标签。
- 直连失败时静默丢弃数据或阻塞不报错。
- 只支持自家实现互连，属性/错误码/分帧与 coturn 不一致。
- NAT 类型靠写死或猜测，或让行为发现结果短路连通性检查。
- IPv6 当作“v4 代码加开关”，导致地址族混用或 v4-mapped 地址参与配对。
- 用被测 ICE 代码自己推导“可直连”期望值（同义反复的验收）。

## 四、已拍板的决定（用户）

| 编号 | 决定 |
|------|------|
| D1 | 参考 ICE 重做，公开 API、信令报文格式、`ChannelType` 语义均可推倒，不保证向后兼容 |
| D2 | UDP 与 TCP 打洞都必须实现；TCP 的 `so` 受限（见 D12） |
| D3 | STUN/TURN 服务端由本仓库自实现 |
| D4 | STUN/TURN 服务端**独立部署**：独立进程运行，不与信令服务器同进程。代码本身与客户端同处 `Src/SAEA.P2P` 程序集，部署宿主为 `Src/SAEA.P2PTest --server`（见 D17） |
| D5 | 与标准实现互通：按 RFC 逐项对齐，交付核对清单（与 D14 一并解释） |
| D6 | TURN 客户端传输支持 UDP、TCP、TLS/443 三种 |
| D7 | TURN 鉴权用共享密钥 + 带有效期的临时凭据 |
| D8 | 现有自定义中继彻底删除，回退交给 TURN |
| D9 | 验收靠本地模拟 NAT 的自动化测试，不做公网实测 |
| D10 | 增加 IPv6 支持 |
| D11 | 先出设计文档，交独立 builder 评审，再进计划阶段 |
| D12 | TCP 打洞验收口径：本地只保证 active↔passive（passive 侧 NAT 预置端口映射）；双 NAT 的 `so` 用例在 Windows 显式跳过并记录，代码保留 `so` 路径供 Linux |
| D13 | IPv6 验收降级为可自动化部分：`::1` 端口隔离的 host 直连、v6 中继、双栈监听、v6 地址编码单测；NAT66 用例标注需管理员预置、默认跳过 |
| D14 | 互通验证：只交付核对清单，本轮不实际联网验证，交付报告写明“未运行”，不得声称已验证 |
| D15 | 数据面不加应用层加密；`Src/SAEA.P2P/README.md` 与 `README.en.md` 删除端到端加密的承诺，改为如实描述 |
| D16 | 保活“无信令服务器”的局域网直连：发现报文携带 ufrag/pwd/候选，实现真正直连 |
| D17 | **不新建库工程**：STUN/TURN 编解码、客户端、服务端、凭据服务全部在 `Src/SAEA.P2P` 内按目录分层实现；服务端部署宿主复用 `Src/SAEA.P2PTest` 的 `--server` 模式；唯一新建工程是测试专用 `Src/SAEA.P2P.Tests`（不产出 NuGet 包、不进 `.sln`）。取代 A1 原布局与 D4 的“独立工程”表述，不改动任何 `.csproj` 的 `<Version>` |
| D18 | 测试虚拟主机地址进入 host 候选用 `IceOptions.AllowLoopbackCandidates`（默认 `false`）：开启后按接口枚举并保留 loopback 候选，仍排除 v4-mapped；host 候选地址取该端口 socket 的实际绑定地址（`LocalEndPoint`），不按枚举值另挑地址 |

## 五、作者的决定（计划阶段按此展开，可被用户推翻）

- A1 工程布局与依赖方向（已按 D17 改写；原「新建 StunTurn 工程 + 独立宿主工程」的布局作废）：
  - `Src/SAEA.P2P`（单包分层）：新增 `Stun/`、`Turn/`、`StunServer/`、`TurnServer/`、`Auth/`、`Ice/`、`Transport/`、`Signaling/`；命名空间对应 `SAEA.P2P.Stun`、`SAEA.P2P.Turn`、`SAEA.P2P.StunServer`、`SAEA.P2P.TurnServer`、`SAEA.P2P.Auth`、`SAEA.P2P.Ice`、`SAEA.P2P.Transport`、`SAEA.P2P.Signaling`。
  - `Stun/`、`Turn/`、`Auth/` 为协议与协议无关的通用层，只依赖 `SAEA.Common`（必要时 `SAEA.Sockets`），不得引用 `Core/`、`Signaling/` 里的客户端类型；`StunServer/`、`TurnServer/` 只依赖上述三层 + `SAEA.Common` + `SAEA.Sockets`，不得引用 `P2PClient`。
  - `Ice/`、`Transport/`、`Signaling/`、`Core/` 为客户端侧，可依赖 `Stun/`、`Turn/`、`Auth/`。
  - `Src/SAEA.P2PTest`：控制台工程，新增 `--server` 模式承载 STUN/TURN 宿主；只引用 `SAEA.P2P`，不需要新 `ProjectReference`。
  - `Src/SAEA.P2P.Tests`：MSTest `net10.0`，只引用 `SAEA.P2P`，不进 `.sln`（与现有四个 MSTest 工程一致），不产出包。
  - 无工程间依赖方向问题：只有一个库程序集，靠目录与命名空间分层，不引入循环依赖。
- A2 包版本由用户管理：本轮不新建任何带 `<Version>` 的工程（`SAEA.P2P.Tests` 为测试工程，按仓库惯例无 `<Version>`）；`SAEA.P2P.csproj` 的 `<Version>` 不动，不增删/升降 `PackageReference`。
- A3 服务端配置：JSON 文件 + 命令行覆盖。字段见 §7.6.6。
- A4 TLS 证书沿用 PFX 文件 + 口令，由 `SAEA.Sockets` 的 BouncyCastle TLS 承载。
- A5 `SAEA.Sockets` 只做增量改动：新增公开类型/重载/选项，不改既有公开成员行为；唯一例外是修复 `SaeaUdpServerSocket.Start()` 的 `UseIPV6` 分支缺陷（该分支当前必抛异常，见 §9）。测试夹具（NAT 模拟器）与 TURN 中继面（每 allocation 的中继 socket、RFC 6062 中继连接）允许直接用 `System.Net.Sockets`，后者理由见 §7.6.6。
- A6 信令连接为 TCP（可开 TLS），只承担注册、候选交换、凭据下发、会话拆除；不承载业务数据。无信令局域网模式例外（§7.10）。
- A7 地址族策略：v4 与 v6 各自独立收集候选；v4-mapped v6 不收集也不配对；跨族配对默认关闭，可配置开启（默认关）。
- A8 兼容策略：旧类型（`HolePuncher`、`NATDetector`、`TCPChannel`、`UDPChannel`、`RelayManager`、`RelaySession`、旧 `ChannelType`）删除或替换；`P2PClient` 保留类名与“注册 → 连接对端 → 收发”的流程语义，签名可改。
- A9 选路阶梯：UDP 直连 → TCP 直连 → TURN/UDP → TURN/TCP → TURN/TLS，逐级尝试、成功即停；阶梯不读 NAT 行为标签。
- A10 两阶段收集：默认先收集 UDP host/srflx 并 trickle 给对端，UDP 全部 pair 失败后才补收集 TCP host 与 relay 分配并再次 trickle；配置 `GatherAllInOnePhase` 可切换为一次性收集。
- A11 公开 API 草案见 §7.11，字段与语义可在计划阶段微调，但错误码与通道枚举是跨进程契约，改动需同步测试与 README。
- A12 语言版本：`SAEA.P2P` 未设置 `LangVersion`（`netstandard2.0` 默认 C# 7.3），新增代码全部按 C# 7.3 编写，不新增 `LangVersion`；`Src/SAEA.P2PTest` 为 `net8.0`，新增文件按该项目既有风格；`SAEA.P2P.Tests` 为 `net10.0`，按该工程设置放宽。
- A13 端口集：每个 peer 会话一套独立本地端口（候选、检查、保活按会话隔离）；共享 port set 留给后续，不做。
- A14 测试时间源可注入：TURN 生命周期、nonce 有效期、保活与超时用例必须能把时间压缩到秒级，否则 §8.2 生命周期用例无法在可接受时间内完成。

## 六、架构

```
单个程序集 Src/SAEA.P2P（一个 NuGet 包，靠目录分层）
应用
 └─ P2PClient（公开 API §7.11）
     ├─ SignalingClient（TCP/TLS，仅协调 §7.9）
     ├─ IceAgent
     │   ├─ CandidateGatherer ── Host / ServerReflexive / PeerReflexive / Relay
     │   ├─ CheckList / PairSelector / Nominator
     │   └─ TransportManager ── UdpTransport / TcpTransport（生产实现走 SAEA.Sockets）
     ├─ ITransport（可注入：生产=SAEA.Sockets；测试=NAT 代理适配器 §8.1）
     └─ Stun/ · Turn/ · Auth/（客户端侧：StunClient / TurnClient / 编解码 / 凭据）

服务端分层（同属 Src/SAEA.P2P，但不引用客户端类型）
 ├─ Stun/ Turn/ Auth/
 │   ├─ StunCodec / TurnCodec（属性、消息、错误码）
 │   ├─ StunClient / TurnClient
 │   └─ CredentialService（临时凭据 + long-term credential 校验）
 ├─ StunServer/（Binding、5780 辅助）
 ├─ TurnServer/（Allocation 表、Permission、Channel、中继 socket、6062 数据连接、UDP/TCP/TLS 监听）
 └─ TransportListeners（UDP / TCP / TLS/443）

独立部署（独立进程）
 └─ Src/SAEA.P2PTest --server（宿主，只引用 SAEA.P2P，读 JSON 配置）
```

职责边界：

- 信令服务器只转发“谁想连谁、双方候选与凭据”，不转发业务数据；不得把信令连接的对端地址当作打洞地址下发。
- STUN/TURN 服务端不感知节点身份，只按 TURN 会话与鉴权工作。
- 客户端数据面只认 ICE 选出的 `selected pair`；未选出前不发送业务数据，调用 `Send` 直接抛错。
- 编解码只在 `Stun/`、`Turn/` 各一份，避免客户端与服务端各写一套导致字节级偏差。
- 服务端侧目录（`StunServer/`、`TurnServer/`）不得引用 `Core/P2PClient.cs`、`Signaling/` 等客户端类型，避免把服务端拖进客户端生命周期。

## 七、协议与行为规格

### 7.1 STUN（RFC 8489）

报文格式：

- 头 20 字节：type（2 字节，最高 2 位必须为 0）、length（2 字节，不含 20 字节头，且必须是 4 的倍数）、magic cookie `0x2112A442`、96 位 transaction ID。
- 属性 TLV，值区补齐到 4 字节倍数，补齐字节计入 length。

属性分三张表，不混写（混写会让实现者误以为公共 STUN 不支持 `CHANGE-REQUEST` 就是 bug）：

| 分组 | 属性 |
|------|------|
| RFC 8489 核心 | `XOR-MAPPED-ADDRESS`、`MAPPED-ADDRESS`、`USERNAME`、`MESSAGE-INTEGRITY`(SHA-1)、`MESSAGE-INTEGRITY-SHA256`、`FINGERPRINT`、`SOFTWARE`、`ERROR-CODE`、`UNKNOWN-ATTRIBUTES`、`REALM`、`NONCE`；ICE 专用：`PRIORITY`、`USE-CANDIDATE`、`ICE-CONTROLLED`、`ICE-CONTROLLING` |
| RFC 5780 扩展 | `CHANGE-REQUEST`、`RESPONSE-ORIGIN`、`OTHER-ADDRESS`、`RESPONSE-PORT`、`PADDING` |
| TURN 专用 | 见 §7.6.5 |

两条必错点必须按此实现：

- `FINGERPRINT` = 整包的 CRC-32，**再与 `0x5354554e` 异或**。计算前必须先把头部 length 改为“包含 `FINGERPRINT` 属性（8 字节）”的值，属性值先填 0 占位；CRC 覆盖到该属性之前为止，算完再回填属性值。只排除 “`FINGERPRINT` 属性本身” 但不改 length 会与 coturn 校验不一致，直接发裸 CRC-32 同样会被判校验失败。BCL 与仓内都没有 CRC-32，需自实现表驱动版本。
- `MESSAGE-INTEGRITY` 计算前，把头部 length 临时改写为“到该属性结尾”的长度，属性值位填 0 占位；HMAC 的输入止于该属性之前（不含属性头与属性值），算完回填属性值并恢复 length。校验时同样先回填。该属性之后的属性只有 `MESSAGE-INTEGRITY-SHA256` 与 `FINGERPRINT` 参与处理，其余忽略。

其它规则：

- 未知 comprehension-required 属性：request 回 420 + `UNKNOWN-ATTRIBUTES`；indication 静默丢弃（indication 无事务，不得回错误）。
- `MESSAGE-INTEGRITY-SHA256`、`PASSWORD-ALGORITHM(S)`、`USERHASH`：本轮默认 SHA-1 签发，但必须能解析 401/438 中的算法列表并做出明确选择；不支持的算法回 400/420，不得静默忽略。RFC 5769 测试向量只覆盖 SHA-1 路径，不能作为 SHA-256 的正确性保证。
- 事务 ID 用 `RandomNumberGenerator.Create().GetBytes(...)`；`RandomNumberGenerator.Fill/GetInt32` 在 `netstandard2.0` 不可用。

事务层：

- UDP：RTO 初始 500ms，`Rc=7` 表示**总共发送 7 次**（首发 + 6 次重传），时刻为 0/500/1500/3500/7500/15500/31500 ms；最后一次之后仍要等 `Rm × RTO`，`Rm=16`，即约 39.5s 才判失败。
- TCP/TLS：不做 STUN 层重传，只用 39.5s 判超时。
- 同一 transaction ID 只允许一个未决请求。
- ICE 检查期间 RTO 取 `MAX(500ms, Ta × N × (waiting + inProgress))`，`Ta` 默认 50ms。

### 7.2 NAT 行为发现（RFC 5780）

映射行为（3 个独立测试，不使用 `CHANGE-REQUEST`，只换目标地址与端口）：

1. 主地址 + 主端口；
2. 备用地址 + 主端口；
3. 备用地址 + 备用端口。

比较三次的 `XOR-MAPPED-ADDRESS`：全同 → `EndpointIndependent`；仅端口变 → `AddressDependent`；地址或端口变 → `AddressAndPortDependent`。

过滤行为（3 个测试）：

1. 基线请求；
2. `CHANGE-REQUEST = 0x06`（同时改 IP 与端口）；
3. `CHANGE-REQUEST = 0x02`（只改端口）。

`change IP` 位是 `0x04`，`change port` 位是 `0x02`。据此判 `EndpointIndependent` / `AddressDependent` / `AddressAndPortDependent`。

服务端要求：

- 必须真实监听**两个 IP**，每个传输协议在**两个 IP 上使用同一对端口号**；同时返回 `MAPPED-ADDRESS` 与 `XOR-MAPPED-ADDRESS`，并回填 `RESPONSE-ORIGIN`、`OTHER-ADDRESS`。
- 只有单个 IP 时不得编造 `OTHER-ADDRESS`，且对带 `CHANGE-REQUEST` 的请求回 420。
- 客户端新事务速率不超过每秒 10 个，并打散 RTO 避免同步。

结果使用与降级：

- 仅作只读诊断展示，**不参与候选优先级计算、TCP 方向偏好、选路与回退判据，也不得短路连通性检查**；会话控制流不读取行为标签。
- 探测失败（对端不支持 `CHANGE-REQUEST`、单地址部署、超时、鉴权失败）不阻断会话，退回纯连通性检查推进。
- 经典 FullCone / Restricted / PortRestricted / Symmetric 只是由双维度派生的只读展示属性，不参与任何分支判断。

### 7.3 ICE（RFC 8445）与候选

候选定义：

- 类型优先级：host=126、peer-reflexive=110、server-reflexive=100、relay=0。
- 优先级：`priority = (2^24)×typePref + (2^8)×localPref + (256 - componentId)`。
- 字段：foundation、component、transport（udp/tcp）、priority、address、port、type、related address/port、TCP type（active/passive/so）。
- peer-reflexive 候选不从信令获得，只能由检查过程动态产生。

收集：

- host（每个可用接口、每个地址族，排除 loopback 与 v4-mapped）→ srflx（按地址族向 STUN 申请）→ relay（按 A10 在回退阶段分配）。
- host 候选地址的取值来源：取该本地端口所绑定 socket 的实际绑定地址（`LocalEndPoint`），不从网卡枚举里另挑一个地址重新 bind。原因：候选地址必须与实际 socket 一致，否则检查包会从错误的源地址发出。正常部署下两者一致；测试虚拟主机绑 `127.0.0.2`~`127.0.0.8` 时只有取绑定地址才正确。
- `IceOptions.AllowLoopbackCandidates`（`bool`，默认 `false`）：为 `true` 时按接口枚举并**保留** loopback 候选，仍排除 v4-mapped；为 `false` 时按上面的规则排除 loopback 与 v4-mapped。开启后，不在网卡枚举列表但确属本地绑定地址的回环地址也允许成为候选。该开关只为本地自动化测试（§8.1 虚拟主机）而设，生产默认关闭。
- 开启了该开关的测试环境必须额外校验：selected pair 的本端与对端地址都落在本次虚拟主机地址集内；否则会带入本机真实网卡候选，可能出现两个测试进程经真实网卡直连而“因为错误原因通过”。
- 两阶段收集（A10）：阶段一发 host/srflx；UDP 全部 pair 失败后阶段二补 TCP host 与 relay。
- 收集预算：阶段一默认 3s（可配），到时未收齐也先发已有候选，避免长尾阻塞。

Trickle（必选）：

- 候选随时可通过 `TrickleCandidate` 增量下发；收集结束发 `EndOfCandidates`，对端据此停止等待。
- 提名完成后到达的候选一律忽略并记日志，不触发重选。

角色：

- **发起 `ConnectRequest` 的一方为 controlling**；tiebreaker 为 64 位随机数，在 `SessionOffer` / `SessionAnswer` 中交换。
- 双方同时发起（都自认 controlling）时，不引入 ufrag/nodeId 等明文比较规则，统一由 tiebreaker 与下面的 487 流程收敛。
- 角色冲突（RFC 8445 §7.3.1.1）：收到与自己角色冲突的检查请求时按本地当前角色判定——controlling 侧 tiebreaker **大于等于**对端值则回 **487 (Role Conflict)** 并保持 controlling，小于则切换为 controlled；controlled 侧 tiebreaker 大于等于对端值则切换为 controlling，小于则回 487 并保持 controlled。收到 487 的一方必须切换角色、**更换 tiebreaker**，并把该 pair 放回 triggered-check 队列重发。不限制翻转次数。
- controlling/controlled 与“谁发起 TCP 连接”是两件事，不要混用。

配对：

- 同地址族、同 component、transport 兼容才成对；UDP 只与 UDP 配，TCP 按 §7.5 的 tcptype 矩阵配。
- pair priority 按 `G/D` 公式计算，控制方与受控方方向不同。
- 本地候选为 `passive` 的 pair 必须剪除。

连通性检查：

- 向对端候选发 STUN Binding 请求，携带：`USERNAME` = `remoteUfrag:localUfrag`、`MESSAGE-INTEGRITY`（用对端 password）、`PRIORITY`（**按 peer-reflexive 类型优先级计算**，不是本地候选真实类型）、`ICE-CONTROLLING` 或 `ICE-CONTROLLED`（每个请求必带）、`USE-CANDIDATE`（仅提名时）。
- 响应也要带 `MESSAGE-INTEGRITY`。
- 从 relay 候选发起检查前，必须先对**对端所有候选的 IP** 做 `CreatePermission`，否则中继侧直接丢弃。
- peer-reflexive 候选两条产生路径：收到检查请求且本地无对应远端候选时创建；收到响应且 `XOR-MAPPED-ADDRESS` 不匹配任何已知本地候选时也创建（priority 取请求里的 `PRIORITY`，base 取该 pair 的本地候选）。

检查调度：

- pair 状态机：`Frozen` / `Waiting` / `InProgress` / `Succeeded` / `Failed`；支持 triggered check 队列与解冻规则，不做无状态的“串行扫描”。
- pacing：`Ta=50ms` 起步，按进行中的检查数放大。

提名：

- 默认 regular nomination：对已成功的 pair **再发一个全新 transaction ID 的 Binding 请求**并带 `USE-CANDIDATE`，不是重传上一包。
- 不支持 aggressive nomination；存在 TCP 候选时 regular 是强制的。

保活与存活判定（两套机制，不要合并）：

- 保活：选中 pair 上周期发 **STUN Binding Indication**（不认证，可带 `FINGERPRINT`），`Tr=15s`，不得小于 15s。
- consent freshness（RFC 7675）：每约 5s 发一个**带认证**的 Binding 请求（间隔 0.8~1.2 抖动）；**30s 内无有效响应即 consent 失效**，必须停止在该 pair 发送并丢弃。

ICE restart：

- 触发时**同时更换 ufrag 与 pwd**，重建检查表；旧 pair 全部作废。本轮实现 API 与状态机，验收只覆盖“重启后能重新选出 pair”一条用例。

授权：

- 每个 peer 会话独立 ufrag/pwd；密码不进日志、不进异常消息。

### 7.4 UDP 打洞

- 用同一 UDP socket 向对端 srflx/host 候选发检查包，即完成 NAT 映射开孔。
- 每个会话独立一套本地端口（A13），不同本地端口各算一个 host 候选。
- 回退完全由检查结果驱动，不读 NAT 行为标签。
- 共享 UDP socket 若被 `Connect` 过，`SAEA.Sockets` 的会话回收会关闭整条 socket；因此 ICE 使用的 UDP socket 不得 `Connect`，且会话需关闭空闲回收（见 §9）。

### 7.5 TCP 打洞（RFC 6544）

候选与配对规则：

- `passive`：本地 bind + listen，等待对端连接。
- `active`：向对端 passive 候选连接；**active 候选在候选串里的端口必须写 9**，真实端口取自对端 passive 候选。
- `so`：本地 bind 固定端口后与对端 `so` 候选同时发起 connect（TCP 同时打开）。
- 配对矩阵（RFC 6544 §6.2）：允许 `active ↔ passive`、`passive ↔ active`、`so ↔ so`；禁止 `active ↔ active`、`so ↔ active`、`passive ↔ passive`；剪除检查表时**必须**移除本地候选为 `passive` 的 pair（被动侧不主动发检查，只应答对端发起的连接与 STUN 请求）。
- 每个接口每个 component 收集两个 host 候选（`passive` 与 `so`），各占不同端口；平台不支持 `so` 时只收集 `passive`。
- 方向偏好由候选自身的 tcptype 决定（与 NAT 行为发现无关），参与 local preference：`localPref = (2^13)×directionPref + other`，host 的方向偏好 active=6 / passive=4 / so=2。

连接与检查：

- 本地端口复用：bind 前设地址复用；connect 失败按退避重试，**`EADDRINUSE` 必须换本地端口重绑**，单靠退避不会成功。
- 检查报文在 TCP 直连通道上按 RFC 4571 两字节大端长度前缀承载 STUN。
- TCP 上不做 STUN 层重传（传输本身可靠）。
- 连接建立后若第一个响应不是 STUN 消息，说明对端地址不是该 peer：**关闭连接并把该远端候选的所有 pair 判 Failed**。
- 连接成功不等于打通：必须完成一次 STUN Binding 事务才算检查通过。
- 被动侧：剪除的只是“本端主动发 ordinary check”的 pair；收到入向连接后按 STUN 服务端流程应答（TCP 上通常产生 peer-reflexive 远端候选），triggered check 成功即形成 valid pair，随后等 controlling 侧提名，被动侧不得自行提名。

平台限制（实测，Windows 非管理员）：

- 监听中的 socket 调 `Connect()` 抛 `InvalidOperationException`；两端同端口 bind 后一方 `AddressAlreadyInUse`。因此 **Windows 上不启用 `so`**：`EnableTcpSimultaneousOpen` 默认 `false`，在 Windows 上强制 `false` 并记录一次日志。
- 由此 TCP 直连的本地验收口径按 D12：只保证 `active ↔ passive`，且 passive 侧 NAT 预置了端口映射（端口保持）。双 NAT 的 `so` 用例标记跳过并记录原因，代码路径保留供 Linux。

### 7.6 TURN（RFC 8656 + RFC 6062）

#### 7.6.1 客户端传输与分帧

- 客户端到 TURN：UDP、TCP、TLS/443 三种。
- **TCP/TLS 上不加任何长度前缀**：报文靠自身长度字段自定界，靠首字节解复用（STUN 首字节 `0b00`，ChannelData 首字节 `0x40`~`0x4F`），其余丢弃。RFC 4571 只用于 ICE-TCP 直连数据通道，不用于 TURN 控制连接。
- 读取方式：TURN/TCP 与 TURN/TLS 客户端必须使用流模式（`.UseStream()`）并从 `GetStream()` 自建读取缓冲；服务端用 `InputHandler` 或 `GetStream(userToken)`。事件模式的回调只保证“一次读取”，不保证 STUN 报文或 `ChannelData` 边界，禁止按回调边界解析。
- 数据连接（RFC 6062）上只跑裸数据，不再承载 TURN 消息；TURN over TLS 下数据连接同样必须 TLS（与控件连接同一证书与校验策略）。

#### 7.6.2 方法与生命周期

| 项 | 生命周期 | 刷新时机 | 说明 |
|---|---|---|---|
| allocation | 默认 600s，服务端上限可配（建议 3600s） | 约 83%（500s） | `LIFETIME=0` 表示立即删除 |
| permission | **固定 300s，不可配置** | 约 80%（240s） | 只按 IP 标识，端口不参与 |
| channel | 600s | 约 80%（480s） | 绑定过期后需等待 5 分钟才能把同一 channel 号绑到别的地址 |

- 三层计时分别维护、分别刷新；permission 不能靠 allocation 刷新顺带。
- `ChannelBind` 会同时创建或刷新对应 permission，不要求先发 `CreatePermission`。
- 同一 IP 下任意端口发来的中继数据都必须放行；未授权数据丢弃并计数。

#### 7.6.3 客户端方法集

`Allocate`、`Refresh`、`CreatePermission`、`ChannelBind`、`Send`/`Data` 指示、`ChannelData`。

#### 7.6.4 RFC 6062（TCP 中继）

- **TCP 分配必须用 TCP 或 TLS 控制连接申请**，`REQUESTED-TRANSPORT=6`。“UDP 控制 + TCP 中继”这一组合不存在，测试矩阵不得设计该格。
- `Connect` 成功返回 `CONNECTION-ID`；客户端必须**另开一条 TCP 连接**（同一服务端，本地传输地址不同），在该连接上发 `ConnectionBind` 并回显 `CONNECTION-ID`。TLS 控制连接对应的数据连接也必须走 TLS，明文连接会被服务端拒绝。
- 对端主动连入时，服务端在控制连接上发 `ConnectionAttempt`（含 `CONNECTION-ID` + `XOR-PEER-ADDRESS`），客户端同样另开连接 + `ConnectionBind`。
- 30s 内未收到 `ConnectionBind`，服务端关闭该 peer 数据连接。

客户端侧（ICE 集成）策略（与 §7.12 一致）：

- 会话在 **TCP / TLS 控制**的中继级内分两段：先用半个阶段预算试 UDP 中继（`REQUESTED-TRANSPORT=17`），失败后用剩余预算试 RFC 6062 TCP 中继（`REQUESTED-TRANSPORT=6`）；两段都失败时把两段原因合并上报。固定五五分，不新增公开配置。
- `TurnUdp` 级（UDP 控制传输）只做 UDP 中继、不尝试 TCP 中继：UDP 控制承载不了 `Connect`/`ConnectionBind`，多打一次分配必然被拒。
- 本地 TCP 中继候选记为 `Type=Relay`、`Transport=Tcp`、`TcpType=None`，不带 `relatedAddress`/`relatedPort`（中继端点是服务端分配的独立监听，与本地映射地址无对应关系）。选路结果用 `IsRelay && UsesTcp` 共同标识「TCP 中继」，不新增结果字段。
- 控制方在数据连接上以 `USE-CANDIDATE` 发起一次检查并直接选中；受控方等控制连接上的 `ConnectionAttempt`，用 `ConnectionBind` 拿到数据连接后做自己的检查，自身检查成功即视为选中。
- `ConnectionBind` 之后的连接按 RFC 4571 两字节长度前缀分帧（同 §7.13 TCP 直连），`Connect`/`ConnectionBind` 自身的 STUN 报文不加长度前缀。
- 与公共 TURN 服务端的 RFC 6062 互操作本轮只给核对清单，标「未运行」。

#### 7.6.5 属性与错误码

必须实现的属性：`XOR-PEER-ADDRESS`、`XOR-RELAYED-ADDRESS`、`XOR-MAPPED-ADDRESS`、`CHANNEL-NUMBER`、`LIFETIME`、`DATA`、`REQUESTED-TRANSPORT`、`REQUESTED-ADDRESS-FAMILY`(0x0017)、`ADDITIONAL-ADDRESS-FAMILY`(0x8000)、`EVEN-PORT`(0x0018)、`RESERVATION-TOKEN`(0x0022)、`DONT-FRAGMENT`(0x001A)、`CONNECTION-ID`(0x002A)。

- `ChannelData` 头 4 字节（2 字节 channel 号 + 2 字节 length，length 不含头）；TCP/TLS 上必须补齐到 4 字节倍数。
- channel 号只允许 `0x4000`~`0x4FFF`；收到 `0x5000`~`0xFFFF` 的 ChannelData 静默丢弃（RFC 7983 留给 DTLS-SRTP 复用）。
- v6 中继依赖 `REQUESTED-ADDRESS-FAMILY` / `ADDITIONAL-ADDRESS-FAMILY`；不支持时回 440。
- `EVEN-PORT`、`RESERVATION-TOKEN` 按本文实现；确实不实现保留机制时按 RFC 8489 回 420 + `UNKNOWN-ATTRIBUTES`，不得静默忽略。`DONT-FRAGMENT` 不适用 420：RFC 8656 §13/§14 规定 UDP-to-UDP 中继时服务端必须忽略该属性，照此处理。
- 错误码必须覆盖 400 / 401 / 403 / 420 / 437 / 438 / 440 / 441 / 442（Unsupported Transport Protocol）/ 443 / 446（Connection Already Exists）/ 447（Connection Timeout or Failure）/ 486 / 508。
- 配额映射：并发分配数超限 → 486；中继端口或容量耗尽 → 508；字节/带宽配额用 508 并在 `ERROR-CODE` reason 里说明。

#### 7.6.6 服务端行为与配置

- 分配表按五元组 + 传输隔离；中继地址从配置的端口范围分配，分配需随机化（RFC 6056），避免可预测。
- 中继面与客户端面分离：每个 UDP allocation 用一个裸 `System.Net.Sockets.Socket` 绑定到中继地址与中继端口，由服务端自有接收循环直接收发；不得给每个 allocation 建一个 `SaeaUdpServerSocket` 实例（每实例自带 `SessionManager`、`UserTokenPool` 预热令牌与超时扫描，按 allocation 线性膨胀）。客户端面监听 socket 只处理 STUN/TURN 报文与 `ChannelData`，不承担中继流量。
- 中继 socket 发出的数据报源地址必须是 `XOR-RELAYED-ADDRESS` 里的地址与端口，因此不能用监听 socket 的 `SendAsync(IPEndPoint, ...)`（该实现会把端口改写成 `SocketOption.Port`）。
- TCP 分配（RFC 6062）的中继连接由服务端 `Connect`/`Accept` 的裸 TCP socket 承载，与客户端控制连接不共享 socket。
- 支持 v4 与 v6 中继地址；双栈监听方式见 §7.8。
- 配置项（JSON + 命令行覆盖）：`ListenAddress`、`StunPort`、`TurnPort`、`TurnTlsPort`、`EnableIpv6`、`EnableDualMode`、`Realm`、`SharedSecret`、`CredentialTtlSeconds`、`NonceLifetimeSeconds`、`RelayPortRange`、`MaxAllocations`、`MaxAllocationsPerUser`、`MaxBytes`、`MaxLifetimeSeconds`、`TlsCertificatePath`、`TlsCertificatePassword`、`ChangeRequestAddressPair`（RFC 5780 的第二地址）。JSON 用 `SAEA.Common` 内嵌的 `SAEA.Common.Newtonsoft.Json` 解析，不新增 `PackageReference`；命令行覆盖优先于配置文件。
- 配置中的口令与共享密钥不得出现在日志、异常消息与 `--help` 输出中。
- TURN 监听端口同时应答 Binding 请求（便于与只发 Binding 的探测互通）；STUN 独立端口只做 Binding 与行为发现。

### 7.7 TURN 鉴权（两层）

第一层：临时凭据派生（REST 风格）

- 用户名：`<过期 Unix 时间戳>:<用户标识>`；分隔符默认 `:`，可配置，必须与对端一致。
- 密码：`Base64(HMAC-SHA1(共享密钥, 用户名))`。

第二层：标准 long-term credential

- `key = MD5(username ":" realm ":" password)`，再用该 16 字节 key 算 `MESSAGE-INTEGRITY`。
- 直接把 `Base64(HMAC-SHA1(...))` 当 HMAC key 会 401，这是必须避免的实现错误。

签发链（跨进程契约）：

- 临时凭据由**信令服务器**签发；信令服务器配置必须包含与 TURN 服务端**相同的** `SharedSecret`、`Realm`、`CredentialTtlSeconds`，否则客户端拿 A 侧签的凭据被 B 侧校验必然 401。
- 信令服务器通过 `TurnCredential` 下发 `realm` / `username` / `password` / `ttl` / `urls`。
- 客户端也支持直接配置 `username` + `password`，便于与 coturn 互通。

校验与 nonce：

- 服务端校验顺序：时间戳未过期 → HMAC 匹配 → realm 匹配 → nonce 有效；失败回 401 或 438 并附新 nonce。
- 401 响应必须带 `REALM` + `NONCE`；438 必须带 `NONCE` + `REALM`。
- nonce = 服务端密钥 + 时间片 + 客户端地址的 HMAC，不得对两个不同源 `IP:port` 复用；有效期默认 600s。
- 客户端必须容忍不带 nonce cookie 的 401（部分 coturn 配置如此）；同一凭据组合最多重试 1 次，避免死循环。

禁止项：

- 共享密钥只从配置读取；日志、异常、`ToString()` 都不得输出密钥、密码或完整凭据。

### 7.8 IPv6

收集与配对：

- 按接口与地址族分别收集；loopback 不收集；v4-mapped v6 不收集（也不能出现在配对里）。
- 双栈监听收到 v4 客户端时，内核给出的是 v4-mapped 地址（实测 `[::ffff:127.0.0.1]:port`）。服务端在生成候选、编码 `MAPPED-ADDRESS`/`XOR-MAPPED-ADDRESS`、比较配对之前必须先 `MapToIPv4()`，不得把 v4-mapped 形态写进 STUN 地址属性或候选；客户端收到 v6 形态的映射地址后同样归一化。
- 同一接口已收集到临时（隐私）地址时，link-local **不收集**；link-local 只能与 link-local 配对（MUST NOT 与非 link-local 配对）。
- STUN 地址属性只有 16 字节地址，**不携带 scope id**；scope id 只作本地元数据按接口解析，不跨信令传输。候选串格式里也不出现 `%zone` 后缀。

编码与监听：

- `XOR-MAPPED-ADDRESS` 的 v6 掩码用 magic cookie + transaction ID 共 16 字节。
- 双栈监听：`Socket.DualMode` 在 .NET 中默认 `false`，必须显式开启；可选用“双 socket”或“单 socket DualMode”，但必须通过 v4/v6 两组用例。现有 `SocketOption` 无 `DualMode`，需新增（§9）。
- 跨族配对默认关闭（A7）；开启时只允许 host/relay 类候选，且有明确开关与日志。

验收限制（D13）：

- 非管理员下只有 `::1` 可用于绑定（`::2`、`fd00::1`、`fe80::1` 均返回 `AddressNotAvailable`），因此 NAT66 的“地址相关映射/过滤”无法自动化。
- v6 验收覆盖：`::1` 端口隔离的 host 直连、TURN v6 中继、双栈监听、v6 地址编码单元测试。NAT66 用例标注“需管理员预置 `fd00::/8` 地址”，默认跳过并记录。

### 7.9 信令协议（重设计）

传输与编码：

- TCP，可开 TLS；报文 = 长度前缀 + 类型 + 负载。
- 编码格式（结构化文本或二进制 TLV）由 builder 定，但**字段语义必须按下表固定**，因为客户端、服务端、测试夹具三方共用。

消息与字段：

| 消息 | 字段 |
|------|------|
| `Register` | peerId、nodeName、protocolVersion、capabilities（udp/tcp/ipv6/turn） |
| `RegisterAck` | peerId、sessionId、serverTime |
| `AuthChallenge` | nonce |
| `AuthResponse` | response（HMAC 结果） |
| `ConnectRequest` | targetPeerId、sessionId |
| `SessionOffer` | sessionId、ufrag、pwd、role（controlling/controlled）、tiebreaker（64 位）、candidates[]、endOfCandidates |
| `SessionAnswer` | 同上字段，role 与 tiebreaker 填应答方自己的值 |
| `TrickleCandidate` | sessionId、candidates[] |
| `EndOfCandidates` | sessionId |
| `TurnCredential` | realm、username、password、ttl、urls[] |
| `SessionClose` | sessionId、reason |
| `Error` | code、message |

- `candidates[]` 每项字段与 ICE 候选一一对应：foundation、component、transport、priority、address、port、type、relatedAddress、relatedPort、tcpType。地址族由 address 本身判定，不另设字段。
- 服务端职责：节点注册与唯一性、按需转发候选、下发 TURN 临时凭据、会话生命周期与超时清理。
- 服务端不得把信令连接的对端地址当作打洞地址下发。
- 会话拆除：`SessionClose` 任一端发起、双端幂等；发起方同时对本会话的 TURN 分配发 `Refresh(LIFETIME=0)` 并删除本地会话。信令连接断开不拆除已建立的直连通道。

### 7.10 无信令服务器的局域网直连

前置依赖：

- 现有 `Discovery/LocalDiscovery` 只 `Bind` 不 `Connect`，而 `SaeaUdpClientSocket` 只在 `ProcessConnected` 后才启动接收，导致发现报文**能发不能收**。必须先在 `SAEA.Sockets` 提供“未连接也能收 + 回调带来源地址”（§9），本功能才能成立。

报文与流程：

- 发现报文扩展为：协议版本、peerId、nodeName、ufrag、pwd、role、tiebreaker、candidates[]、可选 HMAC 标签。
- 收到对端报文后，双方直接进入 ICE 检查与提名，不经过信令服务器；除非本机配置了 TURN，否则不产生 relay 候选。
- 角色：双方都先声明 controlling 并交换 tiebreaker，按 §7.3 的 487 规则收敛；**不使用“nodeId 字典序”这类明文可伪造的规则决定角色**。
- 安全边界：报文不加密，可选局域网共享口令用 HMAC 保证完整性；HMAC **只提供完整性，不提供机密性**，同一链路上的窃听者仍可看到 ufrag/pwd，README 必须写明这一点。
- `P2PQuick.LocalNet` 语义保留为“无信令直连”。

### 7.11 客户端公开 API 草案

通道枚举（跨进程契约，改动需同步测试与 README）：

```csharp
public enum ChannelType
{
    UdpDirect = 0,
    TcpDirect = 1,
    RelayUdp  = 2,
    RelayTcp  = 3,
    RelayTls  = 4,
    Local     = 5
}
```

客户端入口（`ReadOnlySpan<byte>` 是 ref struct，不能作泛型实参，因此数据回调必须用自定义委托）：

```csharp
public delegate void P2PDataReceivedHandler(string peerId, ReadOnlySpan<byte> data);

public sealed class P2PConnection
{
    public string PeerId { get; }
    public ChannelType Channel { get; }
    public IPEndPoint LocalEndPoint { get; }
    public IPEndPoint RemoteEndPoint { get; }
}

public sealed class P2PClient : IDisposable
{
    Task<NodeInfo> RegisterAsync(CancellationToken cancellationToken = default);
    Task<P2PConnection> ConnectToPeerAsync(string peerId, CancellationToken cancellationToken = default);
    void Send(string peerId, ReadOnlySpan<byte> data);
    Task DisconnectPeerAsync(string peerId, CancellationToken cancellationToken = default);

    event Action<P2PConnection> OnPeerConnected;
    event Action<string, string> OnPeerDisconnected;   // peerId, reason
    event Action<NodeInfo> OnPeerDiscovered;           // 仅无信令局域网模式（§7.10）
    event P2PDataReceivedHandler OnDataReceived;       // span 仅在回调内有效
}
```

语义约定：

- `ConnectToPeerAsync` 在“选中 pair”或“降级通道建立”时完成，返回实际通道；全部阶梯失败时抛 `P2PException` 并带新错误码（`NoCandidatePair` / `IceTimeout` / `TurnUnavailable` / `TurnAuthFailed`）。
- 入向会话不提供单独的 `Accept` 入口：对端发起的会话在选路完成后同样触发 `OnPeerConnected` 与 `OnDataReceived`，由应用按 `P2PConnection.PeerId` 区分。
- `OnPeerDiscovered` 只在无信令局域网模式（§7.10）触发，用于把发现到的节点交给应用选择；信令模式不触发。
- 未选出通道前调用 `Send` 抛 `P2PException(NotConnected)`，不排队、不静默丢弃。
- `OnDataReceived` 的 `ReadOnlySpan<byte>` 只在回调期间有效；跨回调保存必须复制（与仓库零拷贝约定一致）。
- `ChannelType` 由选路结果写入，不是默认值。

### 7.12 选路阶梯与默认值

阶梯（逐级，成功即停，失败进下一级；阶梯不读 NAT 行为标签）：

1. UDP 直连
2. TCP 直连（active ↔ passive，passive 侧需端口映射；`so` 受平台限制）
3. TURN over UDP 中继
4. TURN over TCP 中继
5. TURN over TLS/443 中继

第 4、5 级内部再分两段：先用半个阶段预算试 UDP 中继，剩余预算试 RFC 6062 TCP 中继（§7.6.4）；第 3 级用满额预算只做 UDP 中继，不尝试 TCP 中继。

默认值：

| 项 | 默认 | 说明 |
|---|---|---|
| `Ta`（检查 pacing） | 50ms | 按进行中检查数放大 |
| 单 pair 检查超时 | 由 RTO 公式推导，上限 39.5s；实际取 `min(该值, 所属阶段剩余预算)` | TCP 上不重传 |
| UDP 直连阶段预算 | 10s | 可配 |
| TCP 直连阶段预算 | 10s | 可配 |
| 每个 TURN 阶段预算 | 15s | 可配；第 4/5 级内 UDP 段与 TCP 段共用该预算 |
| 会话总预算 | 90s | 必须 ≥ 收集预算 + 各级阶段预算之和（默认 3 + 10 + 10 + 15×3 = 68s）；可配，超时抛错 |
| 候选收集阶段一预算 | 3s | 到时先发已有候选 |
| 保活 `Tr` | 15s | 不得小于 15s |
| consent 间隔 | 5s（0.8~1.2 抖动） | RFC 7675 |
| consent 失效 | 30s | 无有效响应即停发并丢弃 pair |
| allocation | 600s / 刷新 500s | 服务端上限可配 |
| permission | 300s / 刷新 240s | 固定不可配 |
| channel | 600s / 刷新 480s | 固定 |
| 临时凭据 TTL | 3600s | 信令与 TURN 两侧一致 |
| nonce 有效期 | 600s | 可配 |
| TURN 重连退避 | 500ms → 8s | 断线后重连并重新 `Allocate` |
| `AllowLoopbackCandidates` | `false` | 仅本地自动化测试开启；生产不得开启（§7.3） |
| `AllowCrossFamilyPairs` | `false` | v4 与 v6 跨族配对开关（A7） |

### 7.13 数据面封装与解复用

选中的 pair 上同时承载 STUN（保活、consent、提名后的响应）与业务数据，两种实现必须用同一套规则区分，否则两端字节级不一致：

- **业务帧**：1 字节前缀 `0xC0` + 调用方原始负载。`0xC0` 落在 RFC 7983 未分配区间，与 STUN（首字节 `0x00`~`0x3F`）、TURN `ChannelData`（`0x40`~`0x4F`）不冲突。
- **UDP 直连**：一个数据报一帧；首字节 `0x00`~`0x3F` 按 STUN 处理，`0xC0` 按业务帧处理，其余丢弃并计数。
- **TCP 直连**：整条连接统一用 RFC 4571 两字节大端长度前缀分帧，检查帧与业务帧共用这套分帧；帧内首字节 `0x00`~`0x3F` 是 STUN，`0xC0` 是业务，其余整帧丢弃并记日志。
- **TURN 中继**：`ChannelData` 或 `Data` 指示的载荷就是业务帧（含 `0xC0` 前缀）；TURN 控制连接本身的报文仍按 §7.6.1 的首字节解复用处理，两者不在同一层冲突。
- **TURN TCP 中继（RFC 6062）**：`ConnectionBind` 之后的中继数据连接按 TCP 直连规则分帧（RFC 4571 两字节大端长度前缀），帧内首字节同上；`Connect`/`ConnectionBind` 自身的 STUN 报文不加长度前缀。
- **长度上限**：UDP 帧 ≤ 65507 字节、TURN `ChannelData` 载荷 ≤ 65535 字节、TCP 帧 ≤ 1 MiB（均含前缀）；超限整帧丢弃并抛 `P2PException(PayloadTooLarge)`，不部分投递。
- **语义**：`Send(peerId, span)` 一次调用对应一个业务帧，`OnDataReceived` 一次回调对应一个完整业务帧，跨传输一致；半包、粘包、超长、未知首字节都必须由接收侧安全处理。

## 八、本地模拟 NAT 与测试设计

### 8.1 NAT 模拟器（代理模型）

为什么是代理模型（实测，非推断）：Windows 上两个 socket 之间报文直投，进程内没有可插入的旁路观察点；绑定 `127.0.0.x` 只是给自己一个本地端点，不等于能观察别的 socket 的出站流量。因此“用户态 NAT 截获真实 socket 流量”不可行，需管理员与 WFP/虚拟网卡，与 D9 冲突。

模型：

- 每个虚拟主机绑定独立回环地址（IPv4 `127.0.0.x`，实测可直接 bind 且来源地址保留）。
- NAT 代理：被测传输把“下一跳”指向 NAT 入口；NAT 用绑定在外部地址的 socket 重新发起连接或发包，回程按“内部端点 ↔ 外部端点”映射表反查改写。
- 被测端需要一条可注入的传输缝：`IUdpTransport` / `ITcpTransport`。生产实现走 `SAEA.Sockets`；测试实现指向 NAT 代理（仍是真实 socket，只是下一跳是代理）。这是测试侧的唯一例外，且只存在于测试工程。
- NAT 模拟器允许直接用 `System.Net.Sockets`，不引 `SAEA.Sockets`，避免与被测实现互相掩护。

能力与边界（必须写进交付报告）：

- 能验证：映射行为（EIM/ADM/APDM）、过滤行为（EIF/ADF/APDF）、端口保持、映射超时、hairpinning、TCP connect 方向的放行与拒绝。
- 不能验证：内核 NAT 的真实实现差异；TCP 同时开口语（Windows 不支持，且代理无法证明 OS 行为）。
- 不接受“地址级旁观”类需求：所有虚拟主机的候选必须显式绑定到各自 `127.0.0.x`，端口保持必须一端口一 socket，不能用 `ReceiveMessageFrom` 的 `PacketInformation` 判断本地地址（回环上该信息无效）。

预设组合（测试内独立定义，不引用被测代码）：

| 预设 | 映射 | 过滤 | 端口保持 | hairpin |
|---|---|---|---|---|
| FullCone | EIM | EIF | 是 | 开 |
| Restricted | EIM | ADF | 是 | 开 |
| PortRestricted | EIM | APDF | 是 | 开 |
| Symmetric | APDM | APDF | 否 | 关 |
| SymmetricKeepPort | APDM | APDF | 是 | 关 |
| TcpMapped | EIM | EIF | 是 | 开（含静态端口映射） |

### 8.2 自动化测试矩阵（MSTest `net10.0`）

单元级：

- STUN 编解码：RFC 5769 向量、属性 4 字节对齐、未知属性 420、indication 静默丢弃、`FINGERPRINT` 异或常量、`MESSAGE-INTEGRITY` 的 length 回填。
- STUN 事务层：重传时刻表、39.5s 判定、TCP 上不重传、同一事务 ID 只允许一个未决请求。
- TURN：属性与错误码、`ChannelData` 分帧与 4 字节对齐、channel 号范围与静默丢弃、TURN/TCP 与 TURN/TLS 上无长度前缀的首字节解复用（STUN 首字节 `0b00`、ChannelData 首字节 `0x40`~`0x4F`，其余丢弃）、`LIFETIME=0`。
- ICE-TCP：RFC 4571 两字节大端长度前缀的编解码边界（0 长度、半包、跨包粘连、超长包拒绝）。
- 数据面：`0xC0` 业务前缀、UDP 首字节解复用（STUN `0x00`~`0x3F` / 业务 `0xC0` / 其余丢弃）、TCP 分帧半包与粘包、超长帧拒绝、`OnDataReceived` 一次回调恰好一条业务帧。
- 鉴权：`Base64(HMAC-SHA1(...))` 派生、`MD5(username:realm:password)` key、过期、realm 不匹配、nonce 跨地址不复用、401 无 cookie 容忍。
- ICE：优先级公式、pair 排序、487 与 tiebreaker 轮换、候选去重与规范化、v4-mapped 排除、link-local 不与非 link-local 配对、tcptype 配对矩阵与 passive pair 剪除。

集成级（经 NAT 代理的真实 socket）：

UDP 穿透矩阵（期望值由测试内独立的“映射×过滤 → 连通性”判定函数推导，禁止调用被测 ICE 代码）：

| 本地 \ 对端 | FullCone | Restricted | PortRestricted | Symmetric |
|---|---|---|---|---|
| FullCone | UDP 直连 | UDP 直连 | UDP 直连 | UDP 直连 |
| Restricted | UDP 直连 | UDP 直连 | UDP 直连 | UDP 直连 |
| PortRestricted | UDP 直连 | UDP 直连 | UDP 直连 | relay |
| Symmetric | UDP 直连 | UDP 直连 | relay | relay |

- 判定依据：只有“对端 NAT 入向过滤按端口（PortRestricted / Symmetric）”或“对端为 Symmetric 导致其 srflx 端口只对 STUN 服务端有效”才会阻断双向检查；FullCone / Restricted 的过滤不按端口，对称侧主动发出的检查在对端开的孔可承载回程。
- “relay”表示必须经 TURN 且双向数据往返成功；出现静默失败即判失败。
- 若某组合 UDP 直连成功却选中 TCP 或 relay，同样判失败。

TCP 穿透矩阵（Windows，`so` 默认关闭）：

| 场景 | 期望 |
|---|---|
| passive 侧 NAT 配置静态端口映射（TcpMapped） | TCP 直连，检查事务完成，双向字节一致 |
| 两侧均无静态端口映射 | 回退 relay，双向数据成功 |
| passive 侧为 Symmetric 且无静态端口映射 | 回退 relay |
| `so` 组合（`EnableTcpSimultaneousOpen=true`） | Windows 跳过并记录原因；不计入通过 |

- 判定依据：RFC 6544 只有主动侧发起 TCP 连接，直连成立的条件是 passive 侧的映射端口可被外部连入；`active` 侧自身的 NAT 类型不影响出向连接，因此“任一侧为 Symmetric”不构成判据，只看 passive 侧。

TURN 传输矩阵：

- 控制连接 UDP / TCP / TLS 三种 × 分配、刷新、权限、`ChannelData`；TLS 用运行时生成的自签 PFX（口令不入版本库）。
- 中继对端：UDP 中继一组；RFC 6062 TCP 中继一组，**只用 TCP/TLS 控制连接**（UDP 控制 + TCP 中继的组合不存在），TLS 下数据连接也必须 TLS。
- TURN 断线重连与重新 `Allocate` 一组。

IPv6：

- `::1` 端口隔离的 host 直连；v6 中继；双栈监听（v4/v6 各一组，v4 组断言 `XOR-MAPPED-ADDRESS` 的 family 为 IPv4、不带 v4-mapped 形态）；v6 地址编码单测。
- NAT66 与“地址相关映射”用例默认跳过，报告中列出跳过项与原因。

其余：

- 端到端：两个虚拟主机注册 → 候选交换 → 打洞 → 选中 pair → 双向业务数据；再强制直连失败验证 relay 降级。
- 无信令局域网直连：两个客户端仅靠发现报文完成 ufrag/pwd 与候选交换，随后 UDP 直连并双向收发；另测 HMAC 校验失败被丢弃。
- 生命周期：会话超时、consent 失效后重选、TURN 分配过期、permission 回收、进程停止清理；全部通过注入时间源把时长压到秒级。
- 负向：鉴权失败、配额超限（486/508）、非法报文、半包与超长包不崩溃。

### 8.3 验收标准

0. 验收平台为 Windows（本机开发环境，非管理员）；代码避免平台专有调用，但不承诺同一套用例在 Linux/macOS 自动通过。
1. 上述测试全部通过，可离线重复运行，不依赖外网，不依赖固定端口（统一用 `Port=0` + `LocalEndPoint` 取实际端口）。
2. 穿透矩阵与 §8.2 期望表逐格一致；期望值不得引用被测 ICE 代码。
3. 删除自定义中继后，`RelayManager`、`RelaySession`、`RelayData` 在可执行代码与面向用户的文档中不再出现（检查命令：`git grep -n -E "RelayManager|RelaySession|RelayData" -- Src`，结果为空）。`docs/superpowers/` 下的历史设计与本设计文档属于变更记录，不在该检查范围内。`Src/SAEA.P2P/README.md` 与 `README.en.md` 架构图中的 `RelayManager` / `RelaySession` 两行必须一并删除，否则本项不通过。
4. IPv4 与 IPv6 两组端到端用例通过，无信令局域网直连用例通过。
5. 与标准实现的互通交付线下核对清单（环境准备、命令、预期输出、失败排查点）；**本轮不实际执行联网互通，交付报告写明“未运行”，不得声称已验证**。
6. STUN/TURN 服务端按功能性服务端验收：错误码、配额上限、过期与鉴权路径正确；不含抗攻击、监控与压测结论。
7. 跳过白名单仅两项：“Windows 无 TCP 同时打开”、“NAT66 需管理员预置”。白名单外的任何跳过计为失败；报告中必须列出全部跳过项与原因。

### 8.4 测试环境前置

- .NET 10 SDK（当前 10.0.403 已实测）；测试工程目标 `net10.0`。
- 无需管理员权限；不写系统路由、不装虚拟网卡。
- 自签 PFX 在测试运行时生成，口令不入版本库，测试结束删除。

## 九、连带改动：`SAEA.Sockets`

只做增量与一处缺陷修复（A5）。以下缺口均已读代码确认：

| 缺口 | 现状 | 改动 |
|---|---|---|
| UDP 收包无独立来源地址参数 | `OnServerReceiveSpan(IUserToken, ReadOnlySpan<byte>)` 只给令牌与数据，地址要从 `userToken.ReadArgs.RemoteEndPoint` 取，回调结束后即被复用，调用方容易误存 | 新增 `OnServerReceiveSpanFrom(IPEndPoint, ReadOnlySpan<byte>)`；文档注明地址仅在回调内有效，跨回调须复制 |
| UDP 不能向任意端点发送 | `SaeaUdpServerSocket.SendAsync(IPEndPoint, ...)` 会把端口改写成 `SocketOption.Port`，且会话不存在时只上报错误不发送 | 新增 `SendToAsync(IPEndPoint, ReadOnlyMemory<byte>)`，不改端口、不要求会话存在 |
| 拿不到实际端口 | 两个 UDP 类型都没有 `LocalEndPoint` | 新增 `public IPEndPoint LocalEndPoint { get; }` |
| 未连接不能收 | `SaeaUdpClientSocket` 只在 `ProcessConnected` 后启动接收 | 新增 `StartReceive()` + `OnClientReceiveSpanFrom`，供局域网发现使用 |
| 双栈不可用 | `SocketOption` 无 `DualMode`；`SaeaServerSocket.Start()` 绑 `IPv6Any` 时未设置 `DualMode` | 新增 `DualMode` 选项与 `SocketOptionBuilder.UseDualMode()` |
| TCP 不能按端点连接与重试 | `SaeaClientSocket._connectArgs.RemoteEndPoint` 构造时定死；超时直接关 socket | 新增 `Connect(IPEndPoint)` / `ConnectAsync(IPEndPoint, Action<SocketError>)`，并提供不关 socket 的重试路径 |
| `SaeaUdpServerSocket.Start()` 的 v6 分支必抛（缺陷） | 先写 `AddressFamily.InterNetwork` 却去 bind `IPv6Any`；同方法的多播也用 `SocketOptionLevel.IP` | 按配置选择地址族，多播级别按地址族取 `SocketOptionLevel.IP` / `SocketOptionLevel.IPv6`；这是 A5 唯一允许的行为修复 |

约束：

- 不改既有公开成员的行为；新成员按仓库 banner 与 XML 注释规范补齐；`SAEA.Sockets` 的 README 中英同步。
- TURN 中继面（每 allocation 的中继 socket 与 RFC 6062 中继连接）直接用 `System.Net.Sockets.Socket`，是 A5 的第二处显式例外；理由与资源模型见 §7.6.6。
- ICE 使用的 UDP socket 不得 `Connect`；会话需关闭空闲回收（`SessionManager.SetNoTimeout`），否则超时会回收 `ReadArgs` 并可能关闭整条 socket。
- STUN 的 CRC-32 必须自实现（BCL 与 BouncyCastle 的 ns2.0 程序集都不提供）；`RandomNumberGenerator.Fill/GetInt32` 在 `netstandard2.0` 不可用。

## 十、迁移与文档

`Src/SAEA.P2P` 删除：

- `NAT/HolePuncher.cs`、`NAT/HolePunchResult.cs`、`NAT/HolePunchStrategy.cs`、`NAT/NATDetector.cs`、`NAT/NATType.cs`
- `Channel/TCPChannel.cs`、`Channel/UDPChannel.cs`
- `Relay/RelayManager.cs`、`Relay/RelaySession.cs`（含 `RelayState` 枚举），整个 `Relay/` 目录删除
- `Builder/P2POptions.cs` 的 `HolePunchOptions`、`RelayOptions`
- `Builder/P2PServerOptions.cs` 的 `ServerRelayOptions`
- `Protocol/P2PMessageType.cs` 中的打洞与中继消息
- `Common/ErrorCode.cs` 中的 `Punch*`、`HolePunch*`、`Relay*`

`Src/SAEA.P2P` 改造（文件 + 关键符号）：

- `Core/P2PClient.cs`：公开 API 按 §7.11 重写；删除 `ProcessPunchReady` 的“默认 Direct”路径；删除 `_relayManager` 字段与其构造、`Send` 的 Relay 分支、`session.RelaySessionId` 赋值、`ProcessRelayData` 与 `P2PMessageType.RelayData` 分支
- `Core/P2PServer.cs`：信令职责收窄；删除 `ActiveRelaySessionCount`、`OnRelayStarted`、`OnRelayEnded`、`ProcessRelayData`（同名事件与方法在 `RelayManager` 里还有一份，随该类一起删）
- `Core/PeerSession.cs`：会话状态与新状态机对齐；删除 `RelaySessionId` 等中继字段，`IntegrationTest` 的对应断言同步
- `Channel/ChannelType.cs`：新枚举值（§7.11）；`Channel/ChannelState.cs` 同步
- `Common/NodeState.cs`：删除 `HolePunching` / `Authenticating` 等与新流程不对应的状态
- `Common/ErrorCode.cs`：新增 `Ice*`、`Turn*`、`NoCandidatePair`、`NotConnected`
- `Discovery/LocalDiscovery.cs`、`Discovery/DiscoveredNode.cs`：报文携带 ICE 参数；`LocalAddress` 不再恒为 null
- `Protocol/P2PCoder.cs`、`Protocol/P2PProtocol.cs`、`Protocol/P2PMessageType.cs`：新消息集（§7.9）
- `Security/AuthChallenge.cs`、`Security/AuthManager.cs`、`Security/KeyExchange.cs`、`Security/CryptoService.cs`：算法不变，仅随新帧格式调整承载
- `Builder/P2PClientBuilder.cs`、`Builder/P2POptions.cs`：`EnableHolePunch(HolePunchStrategy)` 与 `HolePunchOptions.Strategy` 随 `HolePunchStrategy` 删除而移除，替换为 `IceOptions` / `TurnOptions` / `SignalingOptions`
- `Builder/P2PServerBuilder.cs`、`Builder/P2PServerOptions.cs`：删除 `Relay` 段与 `ServerRelayOptions`（含 `MaxRelaySessions`）；`Validate()` 中 `ErrorCode.RelayFailed` 换成新错误码
- `Shortcut/P2PQuick.cs`：`Client` / `ClientFull` / `Server` 三个入口随新 builder 调整；`ClientFull` 的 `HolePunchStrategy` 参数删除；`Server(int, int, bool relay = true)` 的 `relay` 参数删除（不再有自定义中继）；`LocalNet` 语义保留

`Src/SAEA.P2PTest`（自测入口，非 MSTest）：

- 重写：`RelayTest`、`RelayAdvancedTest`（`OnRelayStarted` / `OnRelayEnded` 调用点）、`HolePunchTest`、`ProtocolTest`、`ProtocolAdvancedTest`、`ClientTest`、`IntegrationTest`（`RelaySessionId` 断言）、`BuilderTest`、`QuickStartTest`、`LocalDiscoveryTest`、`LegacyDecoder`、`ServerTest`、`ErrorCodeTest`、`LifecycleTest`、`AuthEncryptionTest`、`SecurityAdvancedTest`、`PerformanceTest`（`RelayManager` / `P2PMessageType.UserData`）、`ConcurrencyTest`（`RelayManager` / `PeerSession`）、`EdgeCaseTest`（`RelaySession` / `RelayState` / `ErrorCode.Relay*` / `ChannelType.Direct`）
- 保持不动：`SpanPipelineTest`、`StreamPipelineTest`、`StreamDecoderTest`、`UdpPipelineTest`、`TlsTest`、`TlsInteropTest`、`TlsStressTest`、`TlsBenchmark`、`IocpBenchmark`（这些只测底层 socket 能力，不引用任何待删类型）
- `Program.cs` 菜单与命令行参数同步

新增目录与文件（全部按仓库 banner 与 XML 注释规范，均在 `Src/SAEA.P2P/` 下，不新建库工程）：

- `Src/SAEA.P2P/Stun/`：编解码、属性、事务层、`StunClient`、`NatBehaviorProbe`
- `Src/SAEA.P2P/Turn/`：编解码、`TurnClient`、`ChannelData`、`TurnStreamFramer`、`TurnClientOptions`
- `Src/SAEA.P2P/Auth/`：`TurnCredentialService`、`TurnNonceService`、`TurnAuthContext`
- `Src/SAEA.P2P/StunServer/`：`StunServer`、`StunServerOptions`、`StunServerEndpoint`
- `Src/SAEA.P2P/TurnServer/`：`TurnServer`、Allocation/Permission/Channel 表、`RelayPortAllocator`、6062 中继、UDP/TCP/TLS 监听、`ITurnClock`
- `Src/SAEA.P2P/Ice/`、`Src/SAEA.P2P/Transport/`、`Src/SAEA.P2P/Signaling/`：ICE Agent、可注入传输、信令客户端
- `Src/SAEA.P2PTest/Server/`（`--server` 宿主）+ 示例配置文件
- `Src/SAEA.P2P.Tests/`：单元、NAT 代理、集成矩阵（唯一新工程）
- 工程挂载与引用：**不新增 `.sln` 条目**（`SAEA.P2PTest` 已在 `.sln`）；`SAEA.P2P.Tests` 按仓库惯例不进 `.sln`，只 `ProjectReference` 到 `SAEA.P2P`。程序集保持 `netstandard2.0` + C# 7.3，`SAEA.P2P.Tests` 为 `net10.0`，`SAEA.P2PTest` 维持 `net8.0`。

文档：

- `Src/SAEA.P2P/README.md` 与 `README.en.md`：删除“99% 穿透率”等无依据数字与端到端加密承诺；删除架构图中的 `HolePuncher` / `PunchSync` / `RelayManager` / `RelaySession` 行，改为 ICE/STUN/TURN 组件；替换所有用到 `EnableHolePunch`、`HolePunchStrategy`、`P2PQuick.Server(..., relay)` 的示例代码；写明 `ChannelType` 语义、直连与 TURN 回退顺序、IPv6 与局域网模式的安全边界。
- `Src/SAEA.P2P/README.md` 与 `README.en.md` 的服务端章节（不再单列组件 README）：服务端配置项、`dotnet run --project Src/SAEA.P2PTest -c Release -- --server --config <path>` 的部署方式、鉴权模型、监听端口同时应答 Binding、与 coturn 的对齐点。
- 根 `README.md` / `README.en.md`：组件清单与依赖关系同步（`SAEA.P2P` 现在同时内含客户端与服务端）。
- 互通核对清单：`docs/superpowers/specs/` 下单独文件，含命令与预期输出。

## 十一、实施阶段

0. `SAEA.Sockets` 增量 API + `UseIPV6` 缺陷修复 + NAT 代理骨架（含可注入传输接口）。此阶段结束即可跑通“两个虚拟主机经代理互发字节”。
1. STUN 编解码与事务层（RFC 5769 向量、FINGERPRINT、MESSAGE-INTEGRITY、CRC-32 自实现）。在 `Src/SAEA.P2P/Stun/` 下新增目录，不新建工程。
2. STUN 客户端 + 服务端 + RFC 5780 行为发现；把 UDP NAT 代理纳入测试。
3. TURN 客户端 + 服务端（UDP/TCP/TLS 控制连接、权限、channel、临时凭据、生命周期计时可注入）；同时给 `Src/SAEA.P2PTest` 加 `--server` 宿主模式，服务端以此独立进程部署。
4. ICE Agent（UDP）：候选、trickle、配对、检查状态机、提名、487、保活与 consent。
5. ICE-TCP（active/passive）+ RFC 6062 TCP 中继；TCP 侧 NAT 代理。
6. 信令重构与数据面接管；删除旧中继与旧打洞代码。
7. 无信令局域网直连（发现报文扩展 + HMAC 校验）。
8. IPv6 全链路与跨族开关（双栈监听、v6 中继、编码用例）。
9. 完整矩阵 + README 双语更新 + 互通核对清单。

每阶段独立可测，不引入“临时假实现”。无法一次完成的 RFC 能力必须在阶段说明中标注范围与后续项，不得以“跳过”代替实现。

## 十二、风险与已知限制

已确认的平台限制（实测，非推测）：

| 项 | 结论 | 影响 |
|---|---|---|
| 进程内用户态 NAT 截获出站流量 | 不可行 | 采用代理模型（§8.1）；测试只能证明 NAT 行为模型，不构成内核 NAT 结论 |
| Windows TCP 同时打开 | 不可行（listen 后 connect 抛异常；同端口双 bind 一侧 `AddressAlreadyInUse`） | `so` 在 Windows 默认关闭（D12） |
| 非管理员多 IPv6 地址 | 不可行（仅 `::1`） | NAT66 默认跳过（D13） |
| 未连接的 UDP 接收 | `SaeaUdpClientSocket` 只连接后才收 | 局域网直连依赖 §9 的新增能力 |
| 后端无 CRC-32、无 `RandomNumberGenerator.Fill` | 确认 | 需自实现 CRC-32；随机数用 `Create().GetBytes` |

工程风险：

- TCP 直连的真实成功率仍受运营商与防火墙影响；设计以“检查失败即回退”为硬约束，不以成功率承诺代替实现。
- `so` 代码路径在 Windows 无法验收，属于“交付但未在本平台验证”的能力，交付报告必须写明。
- TURN 服务端涉及端口范围、配额与并发；资源上限在计划中写死为配置项默认值。
- 两阶段收集（A10）是对 RFC 8445 单一收集阶段的偏离，依赖 trickle 正确性；若 trickle 有缺陷，TCP/relay 回退会整体失效，因此阶段 4 必须先打通 trickle。
- 数据面不加密（D15）意味着直连通道上的业务数据在公网裸传，README 必须明确告知。
- 无信令局域网模式没有身份鉴权，HMAC 只保证完整性；默认只应在本链路可用。
- 单包分层（D17）后服务端目录与客户端目录共处一个程序集，存在被反向引用、把服务端拖进客户端生命周期的风险；靠目录级引用约束与代码审查拦，不加程序集级隔离。
- `AllowLoopbackCandidates`（D18）开启后会带入本机真实网卡候选，测试矩阵可能经真实网卡直连而“因为错误原因通过”；矩阵必须校验 selected pair 两端都在虚拟主机地址集内。