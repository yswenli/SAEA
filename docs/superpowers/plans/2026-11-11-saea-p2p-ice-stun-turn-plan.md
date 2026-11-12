# SAEA.P2P ICE/STUN/TURN 实施计划

- 日期：2026-11-11
- 上游依据：`docs/superpowers/specs/2026-11-11-saea-p2p-ice-stun-turn-design.md`（第四版，下文简称「设计」）；引用章节号处均指该文件。开工前的 6 处回写已完成，见 §2.2
- 状态：**未开工**。阶段 0 可随时开始
- 平台：Windows 10/11、非管理员；.NET 10 SDK 10.0.403；验收口径见设计 §8.3
- 范围：设计 §十一 阶段 0–9 全量。**代码全部落在现有 `Src/SAEA.P2P`**（新增 `Stun/`、`Turn/`、`StunServer/`、`TurnServer/`、`Auth/`、`Ice/`、`Transport/`、`Signaling/` 目录），服务端宿主复用 `Src/SAEA.P2PTest` 的 `--server` 模式；唯一新建工程是测试专用 `Src/SAEA.P2P.Tests`（不产出 NuGet 包）
- 与设计 A1 的差异：本轮**不新建** `SAEA.P2P.StunTurn` 与 `SAEA.P2PServerTest` 两个工程（用户决定），因此依赖方向、部署方式、README 位置按 §二 R1 调整，并回写设计

---

## 一、怎么用这份计划

1. **阶段串行**：阶段 0→9 顺序执行，阶段内任务可并行但出口条件必须逐条满足；不满足就停下来修，不用后续阶段兜底。
2. **每任务 TDD**：先写失败用例（红）→ 最小实现（绿）→ 重构 → 提交。测试方法名统一 `被测对象_场景_期望`，一个用例一个行为。
3. **每任务自带三件套**：验证命令、预期结果、提交信息。提交前必须本机跑过该命令，输出贴进提交说明或交付报告。
4. **前 5 个阶段不接进 `P2PClient` 的公开客户端 API**：只新增 `Stun/`、`Turn/`、`StunServer/`、`TurnServer/`、`Auth/`、`Ice/`、`Transport/` 类型（`Transport` 为可注入而公开，但阶段 6 前不出现在 `P2PClient` 上）；公开 API 的替换集中在阶段 6，避免边写边弄坏旧调用方（阶段 0–5 期间旧 `SAEA.P2PTest` 仍能编译）。
5. **新增 `.cs` 必须带仓库 banner**（模板见 `AGENTS.md`）：`版本号` 取所属 `.csproj` 的 `<Version>`（`SAEA.P2P` 当前为 `26.11.5.1`；`SAEA.P2PTest` 无 `<Version>`，按其目标框架 `net8.0` 填写），`唯一标识` 每文件用 `[guid]::NewGuid().ToString()` 现场生成，`修改标记` 只在改既有文件时整体覆盖为本次信息。内嵌第三方目录与新工程 `obj/`、`bin/` 排除。
6. **语法上限**：`SAEA.P2P` 是 `netstandard2.0` + 默认 C# 7.3 上限，不写 record / file-scoped namespace / init / 目标类型 `new` / 全局 using；`SAEA.P2PTest` 为 `net8.0` 已有 `ImplicitUsings` 与 `Nullable`，新增文件按该项目既有风格写；`SAEA.P2P.Tests` 为 `net10.0`，按该工程 `LangVersion` 放宽。
7. **池化 buffer**：借出即 `try/finally` 归还 `MemoryPoolManager.Return`；`OnServerReceiveSpan` 家族的 `ReadOnlySpan<byte>` 只在回调内有效，跨回调必须复制。
8. **一个包、分层目录**：STUN/TURN 服务端与客户端同属 `SAEA.P2P` 程序集，靠目录与命名空间（`SAEA.P2P.Stun`、`SAEA.P2P.Turn` 等）分层；服务端类型不得反向依赖 `P2PClient` 与信令类型，避免把服务端拖进客户端流程。

### 本计划明确不做

- 不 `git push`、不 `reset --hard`、不 `clean`、不改写已有提交。
- 不碰与本次任务无关的 `Src/SAEA.FTP/**`、`Src/SAEA.FTP.Tests/**`；FTP 控制通道改动已由你提交为 `68701195`，本项目视其为干净基线，回归失败按真问题处理而不是「既有改动导致」。起手先确认 `git status` 只有本计划与设计文档两份未跟踪文件。
- 不改任何既有 `.csproj` 的 `<Version>`，不增删/升降 `PackageReference`；本轮不新建任何带 `<Version>` 的工程，`SAEA.P2P` 的包版本由你后续统一管理。
- 不做公网互通实测，只交付核对清单并标注「未运行」（设计 §8.3 第 5 条）。
- 不实现加密数据面（D15）；不实现 NAT66（D13）；Windows 上不启用 TCP 同时打开（D12，代码路径保留供 Linux，验收侧标记跳过并记录原因）。

---

## 二、已拍板决定与设计回写

### 2.1 已拍板（用户，2026-11-11）

| 编号 | 决定 | 对计划的影响 |
|---|---|---|
| R1 | **不新建库工程**：STUN/TURN 全部在 `Src/SAEA.P2P` 下实现；服务端宿主复用 `Src/SAEA.P2PTest` 的 `--server` 模式；只新建测试专用 `Src/SAEA.P2P.Tests` | 目录与命名空间见 §四；不新增 `ProjectReference`（`SAEA.P2P` 已引用 `SAEA.Common` + `SAEA.Sockets`）；不新增 `.sln` 条目；不改任何 `<Version>` |
| R2 | 测试虚拟主机地址用 **`IceOptions.AllowLoopbackCandidates`**（默认 `false`）进入 host 候选 | 替换原候选方案 `HostAddresses`；设计 §7.3 已回写；矩阵用例必须额外断言 selected pair 两端都在 `127.0.0.x` |
| R3 | **一任务一提交**，任务号进提交标题前缀 | 按 §四 每任务的提交信息执行；不合并跨任务提交 |
| R4 | 旧公开 API 在**阶段 6 一次性替换**，不做双轨 | 阶段 6 按 T6.1→T6.5 完成并编译通过后再删旧（T6.6） |

### 2.2 开工前必须回写的设计（已完成）

如实记录：本节 6 处回写在进入阶段 0 之前已全部落到设计文档（同期落地第四版），下面是「要求」与「实际结果」的对照。若要复核，直接读设计文档对应章节。

| 设计位置 | 回写结果 |
|---|---|
| 头部「日期 / 涉及组件」 | 第四版；涉及组件改为 `Src/SAEA.P2P` 内新增目录 + `Src/SAEA.P2PTest --server` + 新测试工程 |
| 四、决定表 D4 | 改为「独立部署，不与信令服务器同进程；代码同属 `SAEA.P2P`，部署宿主为 `P2PTest --server`」 |
| 四、决定表新增 D17 / D18 | D17 记「不新建库工程 + 复用 `--server` + 仅新建 `SAEA.P2P.Tests`」；D18 记 `AllowLoopbackCandidates` 与 host 候选取 `LocalEndPoint` |
| A1（工程布局与依赖方向） | 单包分层：`SAEA.P2P` 内新增 `Stun/`、`Turn/`、`StunServer/`、`TurnServer/`、`Auth/`、`Ice/`、`Transport/`、`Signaling/`，并写明目录级引用约束；`SAEA.P2PTest --server` 为宿主；`SAEA.P2P.Tests` 为唯一新工程，不进 `.sln`、不产出包 |
| A2（包版本由用户管理） | 改为「本轮不新增包工程，无包版本变更；`SAEA.P2P` 的 `<Version>` 不动」 |
| A12（语言版本） | 去掉 `SAEA.P2P.StunTurn` 分支，改为单程序集 C# 7.3 + 宿主 `net8.0` + 测试 `net10.0` |
| §六 架构图与职责边界 | 去掉 `SAEA.P2P.StunTurn`、`SAEA.P2PServerTest` 两个节点，改为 `SAEA.P2P` 内部目录分层 + `P2PTest --server` 宿主；新增「服务端目录不得引用客户端类型」边界条 |
| §十 迁移与文档 | 新增文件清单路径改为 `Src/SAEA.P2P/{Stun,Turn,StunServer,TurnServer,Auth,Ice,Transport,Signaling}/...`；服务端 README 合并进 `SAEA.P2P/README` 的服务端章节；明确「不新增 `.sln` 条目」 |
| §十一 实施阶段 | 阶段 1/3 的「新建工程」措辞改为「新增目录」；阶段 3 补 `P2PTest --server` 宿主；阶段 9 部署说明同步 |
| §7.3 候选收集（R2） | 新增 `IceOptions.AllowLoopbackCandidates`（默认 `false`）、host 候选取 `LocalEndPoint` 的取值来源与理由、「测试侧硬约束：selected pair 两端都落在虚拟主机地址集内」 |

同一轮顺手对齐的两处（不在原 6 处清单内，但不改会造成设计内部与计划冲突）：

- §7.11 公开 API 增加 `OnPeerDiscovered`（无信令局域网模式专用），并写明「入向会话不设单独 `Accept` 入口，选路完成后同样触发 `OnPeerConnected`」；计划 T6.4 已同步删掉 `AcceptAsync` / `OnConnectionStateChanged`。
- §7.12 默认值表补 `AllowLoopbackCandidates` 与 `AllowCrossFamilyPairs` 两行；§十二 风险表补「单包分层被反向引用」与「`AllowLoopbackCandidates` 可能让矩阵因为错误原因通过」两条。

R3（一任务一提交）不需改设计；R4（阶段 6 一次性替换旧 API）已在设计 A8 与 §十 中。**本节无剩余动作，可直接进阶段 0。**

---

## 三、仓库事实与底层缺口（已读代码 + 探针实测）

这一节是阶段 0 的输入，也是后续阶段的「不要再踩」清单。**标「实测」的条目是本机探针结论，不是推断。**

### 3.1 `SAEA.Sockets` 缺口（对应设计 §9）

| 编号 | 缺口 | 证据（文件 + 符号） | 处理任务 |
|---|---|---|---|
| G1 | UDP 回调没有独立来源地址 | `Core/Udp/SaeaUdpServerSocket.cs`、`Core/Udp/SaeaUdpClientSocket.cs` 的 `OnServerReceiveSpan(IUserToken, ReadOnlySpan<byte>)`；地址只能从 `userToken.ReadArgs.RemoteEndPoint` 取，回调返回后该对象被复用 | T0.1 / T0.3 |
| G2 | 向任意端点发送会改写端口且要求会话存在 | `SaeaUdpServerSocket.SendAsync(IPEndPoint, ...)` 先 `SessionManager.Get(ipEndPoint.ToString())`，再 `new IPEndPoint(ipEndPoint.Address, SocketOption.Port)` | T0.1 |
| G3 | 两个 UDP 类型都没有 `LocalEndPoint` | 同上两个文件，公开成员清单无该属性 | T0.1 / T0.3 |
| G4 | 未 `Connect` 不能收包 | `SaeaUdpClientSocket` 只在 `ProcessConnected` 之后启动接收循环 | T0.3 |
| G5 | 无 `DualMode` 选项，双栈不可用 | `ISocketOption` / `Model/SocketOption.cs` 无该字段；`Core/Tcp/SaeaServerSocket.cs:193 Start` 绑 `IPv6Any` 时未设 `DualMode` | T0.2 |
| G6 | TCP 只能连构造期定死的端点，失败即关 socket | `Core/Tcp/SaeaClientSocket.cs` 构造时 `_connectArgs.RemoteEndPoint` 写死；超时路径直接关 socket | T0.4 |
| G7 | `SaeaUdpServerSocket.Start()` 的 v6 分支必抛（缺陷） | 同文件 `Start()`：先建 `AddressFamily.InterNetwork` 的 socket 再去 bind `IPAddress.IPv6Any`；多播也固定 `SocketOptionLevel.IP` | T0.2 |

### 3.2 平台事实（探针实测，Windows 非管理员）

| 事实 | 对设计的影响 |
|---|---|
| `127.0.0.2`~`127.0.0.8` 可 bind UDP/TCP，源地址保留 | 虚拟主机模型可行（设计 §8.1） |
| 同一本地 (地址, 端口) 不能建第二个 socket | 候选与 NAT 映射必须一端口一 socket；不允许「监听 socket 兼发」 |
| `::2`、`fd00::1` 等不可用，仅 `::1` | IPv6 验收只做可自动化部分（D13） |
| 双栈监听（`IPv6Any` + `DualMode`）可接受 v4 连接，远端显示 `[::ffff:127.0.0.1]` | 收包侧必须 `MapToIPv4()` 归一化后才能进 STUN 属性/候选（设计 §7.3） |
| TCP `Connect` 失败后同一 socket 可重试 | G6 的重试路径可实现 |
| 进程内无旁路观察点观察别的 socket 出站 | NAT 只能代理模型（设计 §8.1） |

### 3.3 实现时容易犯的错（各阶段自查项）

- `SaeaUdpServerSocket.SendAsync(IPEndPoint, ...)` **不能**用作 ICE/TURN 的发送原语：端口会被改写、无会话不发。必须走 T0.1 的 `SendToAsync`。
- ICE 用的 UDP socket **不得 `Connect`**（否则失去对多个候选收发的自由），且必须 `SessionManager.SetNoTimeout`，否则空闲回收会关掉整条 socket。
- 回调里的 `ReadOnlySpan<byte>` 与 `IPEndPoint` 只在本次回调有效；进度检查表、候选表里只能存复制后的值。
- 每 peer 会话一套独立端口集（设计 §7.6.6 与 §五 评审结论），不能多 peer 共用一个 TURN allocation。
- STUN 的 CRC-32 与 `RandomNumberGenerator.Fill` 在 `netstandard2.0` 都不可用：CRC-32 自实现，随机数用 `RandomNumberGenerator.Create().GetBytes(...)`。
- 库项目里 `Span`/`Memory` 的重载要用 `netstandard2.0` 可用的 `System.Memory` 形式（仓库既有代码已统一，照抄同目录写法）。
- 测试工程里禁止引用被测的内部实现来做断言；NAT 模拟器不引 `SAEA.Sockets`，避免互相掩护（设计 §8.1）。

---

## 四、阶段与任务

每个任务包含：文件锚点、实现要点、测试、验证命令、提交信息。`dotnet test` 的路径按工程实际位置写；MSTest 工程一律 `-c Release`。

---

### 阶段 0：`SAEA.Sockets` 增量 API + 可注入传输 + NAT 代理骨架

**为什么先做**：设计 §8.1 要求「两个虚拟主机经代理互发字节」，这是后面所有矩阵的地基；同时 T0.1–T0.4 是 ICE/TURN 的发送原语，缺一个都写不下去。

**出口条件**：`SAEA.P2P.Tests` 的冒烟用例能让 A（`127.0.0.2`）经 NAT 代理与 B（`127.0.0.3`）双向收发字节；`dotnet build Src/SAEA.Sockets.sln -c Debug` 0 errors；`SAEA.Sockets.Pipeline.Tests` 全绿。

#### T0.1 UDP 发送原语与来源地址（G1/G2/G3，服务端侧）

- 文件：`Src/SAEA.Sockets/Handler/OnServerReceiveSpanFromHandler.cs`（新增）、`Src/SAEA.Sockets/Core/Udp/SaeaUdpServerSocket.cs`
- 要点：
  - 新增公开委托 `public delegate void OnServerReceiveSpanFromHandler(IPEndPoint remoteEndPoint, ReadOnlySpan<byte> data);`，XML 注释写明「地址与数据仅在回调内有效，跨回调须复制」。
  - `SaeaUdpServerSocket` 新增 `public event OnServerReceiveSpanFromHandler OnServerReceiveSpanFrom;`，在收到数据、已从 `ReadArgs.RemoteEndPoint` **复制**出 `IPEndPoint` 后触发；原 `OnServerReceiveSpan` 行为不变（两个事件同时触发）。
  - 新增 `public IPEndPoint LocalEndPoint { get; }`：`Start()` 成功后取 `((IPEndPoint)_socket.LocalEndPoint)`；未启动返回 `null`。
  - 新增 `public Task SendToAsync(IPEndPoint remoteEndPoint, ReadOnlyMemory<byte> data)`：**不改端口、不查 `SessionManager`**，直接 `_socket.SendToAsync`；长度上限沿用 `SocketOption.UDPMaxLength` 校验；若实现内部借池化 buffer，`try/finally` 归还。
- 测试：`Src/SAEA.Sockets.Pipeline.Tests/EndpointApiTests.cs`（新增；sockets 层回归留在既有 sockets MSTest，新 `SAEA.P2P.Tests` 只放 P2P 用例）：`SendToAsync_不同端口两次发送_两端各自收到`、`LocalEndPoint_Start后_等于配置端口`、`OnServerReceiveSpanFrom_收到报文_地址为发送端真实端点`、`OnServerReceiveSpanFrom_回调后修改地址对象_不影响已复制结果`。
- 验证：`dotnet test Src/SAEA.Sockets.Pipeline.Tests/SAEA.Sockets.Pipeline.Tests.csproj -c Release --filter "FullyQualifiedName~EndpointApi"`
- 提交：`feat(sockets): UDP 新增 SendToAsync、LocalEndPoint 与带来源地址的接收回调`

#### T0.2 `SaeaUdpServerSocket.Start()` 地址族修复 + `DualMode`（G5/G7）

- 文件：`Src/SAEA.Sockets/Core/Udp/SaeaUdpServerSocket.cs`、`Src/SAEA.Sockets/ISocketOption.cs`、`Src/SAEA.Sockets/Model/SocketOption.cs`、`Src/SAEA.Sockets/SocketOptionBuilder.cs`、`Src/SAEA.Sockets/Core/Tcp/SaeaServerSocket.cs`
- 要点：
  - `Start()`：先按 `SocketOption.UseIPV6` 决定 `AddressFamily` 再建 socket（当前顺序反了，v6 必抛）；`UseIPV6` 且 IP 为空时绑 `IPv6Any`，否则绑配置地址；多播级别按地址族取 `SocketOptionLevel.IP` / `SocketOptionLevel.IPv6`。
  - `ISocketOption` / `SocketOption` 新增 `bool DualMode { get; set; }`（默认 `false`）；`SocketOptionBuilder` 新增 `UseDualMode()`。
  - `SaeaServerSocket.Start()`：`UseIPV6` 且绑 `IPv6Any` 时按 `DualMode` 设置 `Socket.DualMode`。
  - 这是设计 A5 唯一允许的行为修复，提交信息里点明「修复缺陷」，不要顺手改其它分支。
- 测试：`EndpointApiTests.cs` 追加 `SaeaUdpServerSocket_UseIPV6_Start不抛异常且LocalEndPoint为IPv6`、`SaeaUdpServerSocket_UseIPV6双栈_收到IPv4报文来源为v4映射地址`、`SaeaServerSocket_UseDualMode_IPv4客户端可连接`。
- 验证：同上 e2e 命令（filter `EndpointApi`）。
- 提交：`fix(sockets): UDP 服务端按配置选择地址族并修正多播选项级别，新增 DualMode`

#### T0.3 `SaeaUdpClientSocket` 端点与未连接接收（G3/G4）

- 文件：`Src/SAEA.Sockets/Handler/OnClientReceiveSpanFromHandler.cs`（新增）、`Src/SAEA.Sockets/Core/Udp/SaeaUdpClientSocket.cs`
- 要点：
  - 新增委托 `OnClientReceiveSpanFromHandler(IPEndPoint remoteEndPoint, ReadOnlySpan<byte> data)` 与同名事件，语义同 T0.1。
  - 新增 `public IPEndPoint LocalEndPoint { get; }`。
  - 新增 `public void StartReceive()`：幂等；未 `Connect` 时也能启动接收循环（`ReceiveFrom` 路径），供局域网发现与 ICE 使用；已有 `Connect` 流程改为复用该方法，行为不变。
  
- 测试：`EndpointApiTests.cs` 追加 `StartReceive_未连接_可收到任意来源报文`、`LocalEndPoint_绑定后_等于实际端口`、`StartReceive_重复调用_只启动一个接收循环`。
- 验证：同上 e2e 命令。
- 提交：`feat(sockets): UDP 客户端新增 LocalEndPoint、StartReceive 与带来源地址回调`

#### T0.4 `SaeaClientSocket` 按端点连接与失败重试（G6）

- 文件：`Src/SAEA.Sockets/Core/Tcp/SaeaClientSocket.cs`
- 要点：
  - 新增 `public void Connect(IPEndPoint remoteEndPoint)` 与 `public void ConnectAsync(IPEndPoint remoteEndPoint, Action<SocketError> callBack = null)`；连接前按端点地址族校验/重建 socket（`InterNetwork` / `InterNetworkV6`），`DualMode` 由选项决定。
  - 提供连接失败**不关闭** socket 的路径（例如 `ConnectAsync(..., closeOnFailure: false)` 或单独 `TryConnectAsync`，二选一在实现时按同文件风格定），供 ICE-TCP 多候选串行尝试；既有 `Connect()` / `ConnectAsync()` 的超时关 socket 语义保持不变。
- 测试：`EndpointApiTests.cs` 追加 `Connect_IPEndPoint_连接成功且RemoteEndPoint一致`、`ConnectAsync_首个端点拒绝_不关socket可重试第二个端点`。
- 验证：同上 e2e 命令。
- 提交：`feat(sockets): TCP 客户端支持按端点连接与失败重试`

#### T0.5 新建 `SAEA.P2P.Tests`

- 文件：`Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj`、`Src/SAEA.P2P.Tests/Nat/VirtualHost.cs`
- 要点：`net10.0`，`Microsoft.NET.Test.Sdk` 17.8.0 + MSTest 3.1.1（照抄 `Src/SAEA.Common.Tests/SAEA.Common.Tests.csproj`）；无 `<Version>`；`ProjectReference` 只指向 `SAEA.P2P`（STUN/TURN 代码也在该程序集内，不需要第二个引用）；**不进 `.sln`**。
- 说明：sockets 层用例放 `SAEA.Sockets.Pipeline.Tests`，P2P 用例放本工程，避免新工程同时承担底层 socket 回归。
- 测试：`VirtualHost_绑定127_0_0_2_可获得独立本地端点`。
- 验证：`dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "FullyQualifiedName~VirtualHost"`
- 提交：`test(p2p): 新建 SAEA.P2P.Tests 与虚拟主机夹具`

#### T0.6 可注入传输抽象

- 文件：`Src/SAEA.P2P/Transport/ITransport.cs`、`IUdpTransport.cs`、`ITcpTransport.cs`、`SaeaUdpTransport.cs`、`SaeaTcpTransport.cs`
- 要点：
  - `ITransport`：`IPEndPoint LocalEndPoint { get; }`、`bool IsConnected { get; }`、`void Dispose()`。
  - `IUdpTransport`：`event Action<IPEndPoint, ReadOnlyMemory<byte>> DatagramReceived;`（回调里**复制后**再交给上层，避免 span 生命周期问题）、`Task SendToAsync(IPEndPoint, ReadOnlyMemory<byte>, CancellationToken)`、`void StartReceive()`、`Task BindAsync(IPEndPoint, CancellationToken)`。
  - `ITcpTransport`：`Task ConnectAsync(IPEndPoint, CancellationToken)`、`Task<ITcpTransport> AcceptAsync(CancellationToken)`（仅监听侧实现）、`Stream Stream { get; }`、`event Action<ITcpTransport> Accepted;`。
  - 生产实现走 `SAEA.Sockets`（UDP 用 T0.1/T0.3 的能力；TCP 用 `UseStream()` + `GetStream()`），全部不 `Connect` UDP；socket 的 `SessionManager.SetNoTimeout` 在建立时调用。
  - 这些是**新增公开类型**，阶段 6 才写进 README 与 §7.11 的 API 清单；本阶段不接进 `P2PClient`。
- 测试：`Src/SAEA.P2P.Tests/Transport/SaeaTransportTests.cs`：`SaeaUdpTransport_绑定后_LocalEndPoint为实际端口`、`SaeaUdpTransport_双向发送_对端收到原始字节`、`SaeaUdpTransport_收到的Memory_在回调返回后仍可读`（断言已复制）。
- 验证：`dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "FullyQualifiedName~SaeaTransport"`
- 提交：`feat(p2p): 新增可注入的 UDP/TCP 传输抽象与 SAEA.Sockets 实现`

#### T0.7 NAT 代理模拟器

- 文件：`Src/SAEA.P2P.Tests/Nat/NatBox.cs`、`NatConfig.cs`、`NatMapping.cs`、`NatFilter.cs`、`NatPreset.cs`
- 要点：
  - 每虚拟主机绑独立回环地址与端口（一端口一 socket）；NAT 用「外部地址 socket」重新发/连，回程按映射表反查改写。
  - 预设按设计 §8.1 表：`FullCone`（EIM/EIF/端口保持/hairpin 开）、`Restricted`（EIM/ADF）、`PortRestricted`（EIM/APDF，hairpin 关）等；预设定义**独立写在测试工程**，不引用被测代码常量。
  - 映射行为：EIM / ADM / APDM；过滤行为：EIF / ADF / APDF；支持端口保持开关、映射超时（注入假时钟或可注入计时器）、hairpinning。
  - UDP 与 TCP 两条路径都要有；TCP 只做 `connect` 方向放行/拒绝。
- 测试：`Src/SAEA.P2P.Tests/Nat/NatBoxTests.cs`：`NatBox_端口保持_出入端口一致`、`NatBox_APDF_未授权来源被丢弃`、`NatBox_hairpin_内部互访可达`、`NatBox_TCP_未放行方向连接被拒`。
- 验证：`dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "FullyQualifiedName~NatBox"`
- 提交：`test(p2p): 新增用户态 NAT 代理模拟器与三种预设`

#### T0.8 阶段 0 冒烟

- 文件：`Src/SAEA.P2P.Tests/Nat/NatSmokeTests.cs`
- 要点：A（`127.0.0.2`）→ NAT(`127.0.0.10`) → B（`127.0.0.3`），双向各发 3 个不同长度报文（0 字节边界、1 字节、接近 `UDPMaxLength`）；断言内容、来源端点、NAT 映射表条目。
- 验证：`dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "FullyQualifiedName~NatSmoke"`
- 提交：`test(p2p): 阶段 0 冒烟——两个虚拟主机经 NAT 代理双向收发`

---

### 阶段 1：STUN 编解码与事务层

**路径约定**：本阶段起，任务里的相对路径（如 `Stun/StunClient.cs`）都以 `Src/SAEA.P2P/` 为根；测试文件路径显式写 `Src/SAEA.P2P.Tests/...`。

**出口条件**：RFC 5769 四个样例向量逐字节一致；`FINGERPRINT` 与 coturn 校验规则一致（length 回填 + 异或常量）；事务层重传时刻表与 39.5s 判定有可注入时钟的用例。

#### T1.1 `Stun/` 目录与 STUN 消息头

- 文件：`Src/SAEA.P2P/Stun/StunMessageType.cs`、`Src/SAEA.P2P/Stun/StunClass.cs`、`Src/SAEA.P2P/Stun/StunMessage.cs`、`Src/SAEA.P2P/Stun/StunConstants.cs`
- 要点：
  - **不新建工程**：代码进 `Src/SAEA.P2P/Stun/`，命名空间 `SAEA.P2P.Stun`；banner 的 `CLR版本` 填 `netstandard2.0`、`版本号` 填 `v26.11.5.1`（取自 `SAEA.P2P.csproj`）。
  - 类型编码：`type = (method & 0x0F80) << 2 | (class & 0x2) << 7 | (method & 0x0070) << 1 | class & 0x1 | (method & 0x000F)`；实现编码与解码互逆，并校验最高两位为 0。
  - `StunMessage`：`MessageType`、`Class`、`TransactionId`（16 字节）、`Attributes`（保持插入顺序的列表，同一属性可重复）、`Length` 计算时把补齐字节计入。
  - 解析入口先校验：长度 ≥ 20、最高两位为 0、magic cookie 正确、`length == 剩余字节数`、`length % 4 == 0`；不合法返回结构化失败原因而不是抛裸异常。
  - 从本任务起，服务端侧类型只允许引用 `Stun/`、`Turn/`、`Auth/`、`SAEA.Common`、`SAEA.Sockets`，不得反向引用 `Core/P2PClient.cs` 等客户端类型。
- 测试：`Src/SAEA.P2P.Tests/Stun/StunHeaderTests.cs`：`StunMessage_方法类组合编码_与RFC5769请求头一致`、`StunMessage_解析长度不匹配_返回失败不抛异常`、`StunMessage_最高位非零_拒绝解析`。
- 验证：`dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "FullyQualifiedName~StunHeader"`
- 提交：`feat(stun): 新增 STUN 消息头编解码`

#### T1.2 属性表与 TLV 编解码

- 文件：`Stun/StunAttributeType.cs`、`Stun/StunAttribute.cs`、`Stun/StunAttributeCodec.cs`、`Stun/StunAddressCodec.cs`
- 要点：
  - 按设计 §7.1 三张表分组建枚举：RFC 8489 核心（含 ICE 专用 `PRIORITY` 0x0024、`USE-CANDIDATE` 0x0025、`ICE-CONTROLLED` 0x8029、`ICE-CONTROLLING` 0x802A）、RFC 5780 扩展（`CHANGE-REQUEST` 0x0003、`PADDING` 0x0026、`RESPONSE-PORT` 0x0027、`RESPONSE-ORIGIN` 0x802B、`OTHER-ADDRESS` 0x802C）、TURN 专用（阶段 3 增补，本任务只留枚举位）。
  - 地址属性统一：家族字节 0x01/0x02 + 端口 + 地址；`XOR-MAPPED-ADDRESS` 用 cookie 与 transaction ID 做 XOR；v4-mapped v6 在编码前 `MapToIPv4()`。
  - 补齐规则：值区补齐到 4 字节倍数、补齐字节为零、计入 length；未知 comprehension-optional（最高位为 1）属性原样保留，未知 comprehension-required 返回「需要 420」的结构化结果。
  - 提供 `TryGet<T>`/`GetAll` 读取语义，重复属性按 RFC 顺序返回。
- 测试：`Stun/StunAttributeTests.cs`：`StunAttribute_值长度非4倍数_补齐且length含补齐`、`StunMessage_未知必选属性_返回需420`、`StunMessage_未知可选属性_原样保留`、`XorMappedAddress_v4映射v6_编码为v4`、`StunMessage_重复属性_按顺序返回全部`。
- 验证：filter `StunAttribute`。
- 提交：`feat(stun): 属性 TLV 编解码与地址属性规范化`

#### T1.3 CRC-32 与 `FINGERPRINT`

- 文件：`Common/Crc32.cs`、`Stun/StunFingerprint.cs`
- 要点：
  - 表驱动 CRC-32（多项式 `0xEDB88320`），`netstandard2.0` 自实现；不引入新包。
  - 编码：先把 header length 改为「含 `FINGERPRINT` 的 8 字节」、属性值填 0，CRC 覆盖到该属性之前，`XOR 0x5354554E` 后回填。
  - 校验：按同样规则重算并比对；`FINGERPRINT` 必须是最后一个属性，否则视为非法。
- 测试：`Stun/StunFingerprintTests.cs`：`Crc32_标准向量_结果匹配`、`StunFingerprint_编码后校验通过`、`StunFingerprint_只改值不改length_校验失败`、`StunFingerprint_非末位_判非法`。
- 验证：filter `StunFingerprint`（`Crc32_` 用例同过滤器）。
- 提交：`feat(stun): 自实现 CRC-32 与 FINGERPRINT 计算`

#### T1.4 `MESSAGE-INTEGRITY`

- 文件：`Stun/StunIntegrity.cs`、`Stun/StunPasswordAlgorithm.cs`
- 要点：
  - SHA-1 路径：计算前 header length 临时改写为「到该属性结尾」、值位填 0，HMAC 输入止于该属性之前；算完回填并恢复 length。校验同规则。
  - 该属性之后的属性只处理 `MESSAGE-INTEGRITY-SHA256` 与 `FINGERPRINT`，其余忽略。
  - `MESSAGE-INTEGRITY-SHA256`/`PASSWORD-ALGORITHM(S)`：本轮只签发 SHA-1，但必须能解析对端算法列表并显式选择；不支持的算法返回 400/420，不静默忽略。
- 测试：`Stun/StunIntegrityTests.cs`：`StunIntegrity_SHA1_编码后校验通过`、`StunIntegrity_length未回填_校验失败`、`StunIntegrity_尾部有指纹_仍可校验`、`StunIntegrity_仅支持SHA256的对端_返回明确不支持`。
- 验证：filter `StunIntegrity`。
- 提交：`feat(stun): MESSAGE-INTEGRITY 编码校验与算法协商`

#### T1.5 事务层

- 文件：`Stun/StunTransaction.cs`、`Stun/StunTransactionLayer.cs`、`Stun/IRetryClock.cs`（或 `IClock`，实现时统一命名）
- 要点：
  - UDP：`Rc=7`、`Rm=16`、首发 + 6 次重传，时刻 0/500/1500/3500/7500/15500/31500ms，最后再等 `16×500ms` 判失败（约 39.5s）；计时可注入，测试不真等。
  - TCP/TLS：不重传，39.5s 超时。
  - 同一 transaction ID 只允许一个未决请求，重复注册直接抛/返回失败。
  - 收到响应按 transaction ID 完成；迟到响应丢弃并记日志。
  - ICE 期间 RTO 支持外部覆盖：`MAX(500ms, Ta × N × (waiting + inProgress))`。
- 测试：`Stun/StunTransactionTests.cs`：`StunTransaction_无响应_重传时刻与设计一致`、`StunTransaction_超过39_5秒_判失败`、`StunTransaction_TCP模式_不重传`、`StunTransaction_同ID重复注册_被拒绝`、`StunTransaction_迟到响应_不影响已结束事务`。
- 验证：filter `StunTransaction`。
- 提交：`feat(stun): STUN 事务层与可注入重传计时`

#### T1.6 RFC 5769 向量验收

- 文件：`Src/SAEA.P2P.Tests/Stun/Rfc5769VectorTests.cs`
- 要点：四个官方样例——sample request（含 `SOFTWARE`/`PRIORITY`/`ICE-CONTROLLED`/`USERNAME`/`MESSAGE-INTEGRITY`/`FINGERPRINT`）、IPv4 response、IPv6 response、long-term credential request。逐字节比对编码结果，并做解码回环。
- 验证：`dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "FullyQualifiedName~Rfc5769"`
- 提交：`test(stun): RFC 5769 四个向量逐字节验收`

---

### 阶段 2：STUN 客户端/服务端 + RFC 5780 行为发现

**出口条件**：客户端经 NAT 代理能从自建 STUN 服务端拿到正确的 srflx；`CHANGE-REQUEST` 两个地址/端口有效；映射与过滤分类结果被记录下来但不参与选路。

#### T2.1 `StunClient`

- 文件：`Stun/StunClient.cs`、`Stun/StunBindingResult.cs`
- 要点：
  - `BindingAsync(IPEndPoint server, ...)`：构造 `Binding` 请求（可选 `SOFTWARE`），发到指定端点，返回 `MappedAddress`、`XorMappedAddress`、`ResponseOrigin`、`OtherAddress`、耗时。
  - 走 T0.6 的 `IUdpTransport`/`ITcpTransport`，事务层用 T1.5；401/438 时按 T1.4 的凭据路径重试（同一凭据组合最多重试 1 次）。
  - 超时与取消尊重 `CancellationToken`；失败返回带错误码的结果对象，不抛裸 `SocketException`。
- 测试：`Stun/StunClientTests.cs`（起一个假服务端，用 `IUdpTransport` 直连）：`StunClient_正常响应_返回映射地址`、`StunClient_401一次_用新nonce重试成功`、`StunClient_超时_返回超时错误码`。
- 验证：filter `StunClient`。
- 提交：`feat(stun): STUN 客户端与 Binding 结果模型`

#### T2.2 `StunServer`

- 文件：`StunServer/StunServer.cs`、`StunServer/StunServerOptions.cs`、`StunServer/StunServerEndpoint.cs`
- 要点：
  - 每监听端点（UDP / TCP / TLS 由阶段 3 复用同一编解码）处理 `Binding` 请求与 indication；`CHANGE-REQUEST` 支持「换地址」「换端口」「两者」三种组合，需要至少两个绑定地址/端口。
  - 响应携带 `XOR-MAPPED-ADDRESS`、`MAPPED-ADDRESS`、`RESPONSE-ORIGIN`、`OTHER-ADDRESS`、`SOFTWARE`；按请求决定是否带 `FINGERPRINT`。
  - 未知必选属性回 420 + `UNKNOWN-ATTRIBUTES`；indication 静默丢弃。
  - 端口同时应答 Binding 与 TURN（设计 §7.6）；本任务先只做 Binding，TURN 分支阶段 3 接入，接口预留分派点。
- 测试：`StunServer/StunServerTests.cs`：`StunServer_绑定请求_响应含xor映射地址`、`StunServer_CHANGE_REQUEST换端口_响应用另一端口发出`、`StunServer_未知必选属性_返回420带列表`、`StunServer_indication_不响应`。
- 验证：filter `StunServer`。
- 提交：`feat(stun): STUN 服务端与 RFC 5780 换地址换端口响应`

#### T2.3 NAT 行为发现（只读展示）

- 文件：`Stun/NatBehaviorProbe.cs`、`Stun/NatBehaviorInfo.cs`
- 要点：
  - 映射行为：同一本地端口向两个不同 STUN 端点发请求，比较映射地址 → EIM / ADM / APDM。
  - 过滤行为：按 `CHANGE-REQUEST` 组合探测 → EIF / ADF / APDF。
  - 结果只进 `NatBehaviorInfo` 供日志/诊断展示，**不参与候选优先级、不参与选路阶梯**（评审结论）。
  - 探测失败不影响连接流程，返回 `Unknown`。
- 测试：`Stun/NatBehaviorProbeTests.cs`：`NatBehaviorProbe_EIM_识别为EIM`、`NatBehaviorProbe_APDF_识别为APDF`、`NatBehaviorProbe_探测超时_返回Unknown且不抛`。
- 验证：filter `NatBehaviorProbe`。
- 提交：`feat(stun): NAT 映射与过滤行为探测（只读）`

#### T2.4 经 NAT 代理的 STUN 集成

- 文件：`Src/SAEA.P2P.Tests/Stun/StunNatIntegrationTests.cs`
- 要点：STUN 服务端放在公网侧虚拟地址，客户端在 NAT 后；验证 srflx 地址等于 NAT 外部端点、映射表条目正确、`CHANGE-REQUEST` 换端口后仍能收到。
- 测试：`StunNatIntegration_经NAT_映射地址为NAT外部端点`、`StunNatIntegration_CHANGE_REQUEST换端口_客户端收到响应`。
- 验证：filter `StunNatIntegration`。
- 提交：`test(stun): 经 NAT 代理的 STUN 映射与换端口集成`

---

### 阶段 3：TURN（UDP / TCP / TLS 控制连接 + RFC 6062）

**出口条件**：三种控制传输上都能 `Allocate` 并完成双向中继；`permission`/`channel`/`allocation` 三层计时分别刷新且可注入时钟；RFC 6062 的 `Connect`→`ConnectionBind` 与 `ConnectionAttempt` 两条路径都通；TLS 数据连接拒绝明文；`SAEA.P2PTest --server` 能起进程并被测试侧 Binding 命中。

#### T3.1 TURN 属性与 `ChannelData` 编解码

- 文件：`Src/SAEA.P2P/Turn/TurnAttributeType.cs`、`Turn/TurnMethod.cs`、`Turn/TurnErrorCode.cs`、`Turn/ChannelData.cs`、`Turn/TurnMessage.cs`
- 要点：
  - 属性按设计 §7.6.5：`XOR-PEER-ADDRESS`、`XOR-RELAYED-ADDRESS`、`XOR-MAPPED-ADDRESS`、`CHANNEL-NUMBER`、`LIFETIME`、`DATA`、`REQUESTED-TRANSPORT`、`REQUESTED-ADDRESS-FAMILY`(0x0017)、`ADDITIONAL-ADDRESS-FAMILY`(0x8000)、`EVEN-PORT`(0x0018)、`RESERVATION-TOKEN`(0x0022)、`DONT-FRAGMENT`(0x001A)、`CONNECTION-ID`(0x002A)。
  - 方法：`Allocate`/`Refresh`/`CreatePermission`/`ChannelBind`/`Send`/`Data`/`Connect`/`ConnectionBind`/`ConnectionAttempt`。
  - `ChannelData`：2 字节 channel + 2 字节 length（不含头），TCP/TLS 上补齐到 4 字节倍数；channel 号只接受 `0x4000`~`0x4FFF`，`0x5000`~`0xFFFF` 静默丢弃。
  - `EVEN-PORT`/`RESERVATION-TOKEN` 不实现保留机制 → 回 420 + `UNKNOWN-ATTRIBUTES`；`DONT-FRAGMENT` 按 RFC 8656 §13/§14 忽略，不回 420（不要「顺手统一」成 420）。
  - 错误码枚举覆盖 400/401/403/420/437/438/440/441/442/443/446/447/486/508，`ERROR-CODE` 的 class/number/reason 编码按 RFC。
- 测试：`Turn/TurnCodecTests.cs`：`ChannelData_TCP补齐_长度为4倍数`、`ChannelData_0x5000以上通道号_静默丢弃`、`TurnMessage_EVEN_PORT_返回420`、`TurnMessage_DONT_FRAGMENT_不返回420`、`TurnMessage_ERROR_CODE_编码与解析回环`。
- 验证：filter `TurnCodec`。
- 提交：`feat(turn): TURN 属性、方法与 ChannelData 编解码`

#### T3.2 鉴权与凭据服务

- 文件：`Turn/TurnCredentialService.cs`、`Turn/TurnNonceService.cs`、`Turn/TurnAuthContext.cs`
- 要点：
  - 第一层：`username = "<过期Unix时间戳><分隔符><用户标识>"`，`password = Base64(HMAC-SHA1(SharedSecret, username))`；分隔符可配。
  - 第二层：`key = MD5(username ":" realm ":" password)`，用 key 算 `MESSAGE-INTEGRITY`；**禁止**直接把 Base64 密码当 HMAC key。
  - 校验顺序：时间戳未过期 → HMAC 匹配 → realm 匹配 → nonce 有效；失败回 401（带 `REALM`+`NONCE`）或 438（带 `NONCE`+`REALM`）。
  - nonce = 服务端密钥 + 时间片 + 客户端地址的 HMAC，不对两个不同 `IP:port` 复用，默认 600s。
  - 日志/异常/`ToString()` 不输出共享密钥、密码、完整凭据。
- 测试：`Turn/TurnAuthTests.cs`：`TurnAuth_时间戳过期_返回401`、`TurnAuth_realm不匹配_返回401`、`TurnAuth_同一nonce跨源地址_被拒`、`TurnAuth_用Base64密码当key_校验失败`、`TurnAuth_正确凭据_通过`。
- 验证：filter `TurnAuth`。
- 提交：`feat(turn): 两层鉴权、nonce 与临时凭据服务`

#### T3.3 `TurnServer`：allocation、中继 socket、权限、channel、配额

- 文件：`Src/SAEA.P2P/TurnServer/TurnServer.cs`、`TurnAllocation.cs`、`TurnAllocationTable.cs`、`TurnPermission.cs`、`TurnChannel.cs`、`TurnServerOptions.cs`、`RelayPortAllocator.cs`、`ITurnClock.cs`
- 要点：
  - 分配表按「客户端五元组 + 传输类型」隔离；中继地址从 `RelayPortRange` 随机分配（RFC 6056），避免可预测。
  - **每个 UDP allocation 一个裸 `System.Net.Sockets.Socket`** 绑中继地址与端口，自有接收循环；禁止每 allocation 建 `SaeaUdpServerSocket`。中继发送不能用监听 socket 的 `SendAsync(IPEndPoint, ...)`。
  - `permission` 固定 300s / 刷新 240s，只按 IP；`channel` 600s / 刷新 480s；allocation 默认 600s / 刷新 500s，`LIFETIME=0` 立即删除；三层独立计时，可注入时钟。
  - `ChannelBind` 同时创建/刷新 permission；同一 IP 任意端口的中继数据都放行，未授权数据丢弃并计数。
  - 配额：并发分配超限 486，端口或容量耗尽 508；`MaxAllocationsPerUser`、`MaxBytes` 也映射 508 并在 reason 说明。
  - 监听端口同时应答 Binding（阶段 2 的 `StunServer` 分派点接入），STUN 独立端口不受影响。
  - 配置项按设计 §7.6.6 全量实现（JSON + 命令行覆盖，命令行优先），JSON 用 `SAEA.Common.Newtonsoft.Json`，不新增包。
- 测试：`TurnServer/TurnAllocationTests.cs`：`TurnAllocation_LIFETIME为0_立即删除`、`TurnAllocation_permission仅按IP_不同端口放行`、`TurnChannel_过期后同号绑定他人_需等待5分钟`、`TurnServer_超配额_返回486`、`TurnServer_端口耗尽_返回508`。
- 验证：filter `TurnServer`（`TurnAllocation` 用例同过滤器）。
- 提交：`feat(turn): TURN 服务端分配表、中继 socket、权限通道与配额`

#### T3.4 TURN over TCP 控制连接

- 文件：`TurnServer/TurnTcpListener.cs`、`Turn/TurnStreamFramer.cs`
- 要点：
  - TCP 上**不加长度前缀**：按 STUN 自身 length 自定界，靠首字节解复用（`0b00` → STUN，`0x40`~`0x4F` → ChannelData），其余丢弃。
  - 服务端用 `InputHandler` 或 `GetStream(userToken)` 自建缓冲，禁止按回调边界解析。
  - 客户端侧同样 `.UseStream()` + `GetStream()`，解析器状态可跨 read 调用保留。
- 测试：`Turn/TurnStreamFramerTests.cs`：`TurnStreamFramer_半包_不产出消息`、`TurnStreamFramer_粘包_产出两条`、`TurnStreamFramer_首字节非法_丢弃并继续`；`TurnServer/TurnTcpTests.cs`：`TurnTcp_Allocate后双向中继_数据一致`。
- 验证：filter `TurnStreamFramer` 与 `TurnTcp`。
- 提交：`feat(turn): TURN over TCP 控制连接与流式解复用`

#### T3.5 TURN over TLS/443

- 文件：`TurnServer/TurnTlsListener.cs`、`Turn/TurnTlsClientTransport.cs`
- 要点：沿用 `SocketOptionBuilder.WithSsl(...)`（PFX + 口令，设计 A4）；握手完成后再开始 TURN 解析；证书校验策略与既有 TLS 测试一致；控制连接与数据连接使用同一证书与校验策略。
- 测试：`Turn/TurnTlsTests.cs`：`TurnTls_Allocate后双向中继_数据一致`、`TurnTls_证书校验失败_连接中止`。
- 验证：filter `TurnTls`。
- 提交：`feat(turn): TURN over TLS/443 控制连接`

#### T3.6 `TurnClient`

- 文件：`Turn/TurnClient.cs`、`Turn/TurnAllocationClient.cs`、`Turn/TurnClientOptions.cs`
- 要点：
  - 方法集：`Allocate`（含 `REQUESTED-TRANSPORT=17|6`）、`Refresh`、`CreatePermission`、`ChannelBind`、`Send`/`Data` 指示、`ChannelData`。
  - 401/438 按 T3.2 流程重试，同一凭据组合最多 1 次；401 不带 nonce cookie 也要容忍。
  - 刷新：allocation 500s、permission 240s、channel 480s，各自独立定时器（可注入）；刷新失败按退避重连并重新 `Allocate`（500ms → 8s）。
  - 客户端直配 `username` + `password`（互通 coturn）与信令下发凭据两种入口。
- 测试：`Turn/TurnClientTests.cs`：`TurnClient_401后重试_只重试一次`、`TurnClient_permission到期前_自动刷新`、`TurnClient_断线_按退避重连并重新Allocate`。
- 验证：filter `TurnClient`。
- 提交：`feat(turn): TURN 客户端与三层刷新、重连退避`

#### T3.7 RFC 6062 服务端

- 文件：`TurnServer/TurnTcpRelay.cs`、`TurnServer/TurnConnectionTable.cs`
- 要点：
  - TCP 分配必须来自 TCP 或 TLS 控制连接（`REQUESTED-TRANSPORT=6`），UDP 控制 + TCP 中继不存在。
  - `Connect` 成功返回 `CONNECTION-ID`；客户端另开连接发 `ConnectionBind` 并回显 ID；对端主动连入时发 `ConnectionAttempt`（`CONNECTION-ID` + `XOR-PEER-ADDRESS`）。
  - 重复 `Connect` 同一 peer → 446；连接失败/超时 → 447；30s 内未 `ConnectionBind` 关闭 peer 连接。
  - TLS 控制连接的数据连接必须 TLS，明文拒绝。
  - 数据连接只跑裸数据，不承载 TURN 消息。
- 测试：`TurnServer/Turn6062Tests.cs`：`Turn6062_Connect后另开连接Binding_双向数据一致`、`Turn6062_重复Connect同一peer_返回446`、`Turn6062_30秒未Bind_连接关闭`、`Turn6062_TLS控制连接_明文数据连接被拒`。
- 验证：filter `Turn6062`。
- 提交：`feat(turn): RFC 6062 TCP 中继与连接绑定`

#### T3.8 TURN 三种控制传输的集成矩阵

- 文件：`Src/SAEA.P2P.Tests/Turn/TurnTransportMatrixTests.cs`
- 要点：客户端在 NAT 后，TURN 服务端在公网侧虚拟地址；`over UDP`、`over TCP`、`over TLS` 三格各做：Allocate → CreatePermission → ChannelBind → Send/Data → 双向字节校验 → `LIFETIME=0` 释放后中继端口不可再用。
- 测试：`TurnMatrix_UDP控制_双向中继成功`、`TurnMatrix_TCP控制_双向中继成功`、`TurnMatrix_TLS控制_双向中继成功`、`TurnMatrix_释放后_中继端口回收`。
- 验证：`dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "FullyQualifiedName~TurnMatrix"`
- 提交：`test(turn): UDP/TCP/TLS 三种控制传输的中继矩阵`

#### T3.9 服务端宿主：`SAEA.P2PTest --server`

- 文件：`Src/SAEA.P2PTest/Program.cs`、`Src/SAEA.P2PTest/Server/ServerHost.cs`、`Src/SAEA.P2PTest/ServerSampleConfig.json`
- 要点：
  - 新增 `--server` 模式：启动 STUN 与 TURN（UDP/TCP/TLS）监听；`--config <path>` 读 JSON（复用 `SAEA.Common.Newtonsoft.Json`），命令行覆盖优先；Ctrl+C 优雅关停。
  - 只引用 `SAEA.P2P`（不需要新 `ProjectReference`）；配置项按设计 §7.6.6 全量；`SharedSecret`、`TlsCertificatePassword` 等不得出现在 `--help`、日志、异常文本。
  - `SAEA.P2PTest` 是 `net8.0` + `ImplicitUsings` + `Nullable`，新增文件按该项目既有风格写；banner `版本号` 填 `net8.0`。
  - 默认无参数仍进原交互菜单，不破坏既有用法。
- 测试：宿主属演示工程，不写 MSTest；用「起进程 + 测试工程发 Binding」的冒烟：`StunNatIntegration` 用例指向该进程端口。
- 验证：`dotnet run --project Src/SAEA.P2PTest -c Release -- --server --config <临时配置>`，另开进程跑 `dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "FullyQualifiedName~StunNatIntegration"`；`--help` 输出人工核对无密钥。
- 提交：`feat(p2ptest): 新增 --server 宿主模式承载 STUN/TURN`

---

### 阶段 4：ICE Agent（UDP）

**出口条件**：两个虚拟主机在不同 NAT 预设下能选出 `selected pair` 并双向收发业务帧；UDP 穿透矩阵（设计 §8.2）逐格符合期望，直连成功时不得选中 relay；角色冲突与 consent 有专门用例。

#### T4.1 候选与优先级模型

- 文件：`Src/SAEA.P2P/Ice/CandidateType.cs`、`Candidate.cs`、`CandidatePair.cs`、`IcePriority.cs`、`IceTransportType.cs`
- 要点：
  - 类型优先级 host=126、peer-reflexive=110、server-reflexive=100、relay=0。
  - `priority = (2^24)×typePref + (2^8)×localPref + (256 - componentId)`；TCP 的 `localPref = (2^13)×directionPref + other`，host 方向偏好 active=6 / passive=4 / so=2。
  - 字段：foundation、component、transport、priority、address、port、type、related address/port、tcpType。
  - foundation 计算需稳定：类型 + base 地址 + 协议 + STUN 服务器（有则）；同 host 的两个 port 若来自同一接口应同 foundation（按 RFC 8445 §5.1.1.3 的规则实现，不按端口区分）。
  - `CandidatePair` 保存本地/远端候选、状态、`valid`、提名标记，pair priority 按 `G/D` 公式分别计算控制方/受控方。
- 测试：`Ice/IcePriorityTests.cs`：`IcePriority_host候选_等于公式值`、`IcePriority_relay为0_排序在最后`、`IceCandidatePair_控制方与受控方_优先级方向不同`、`IceFoundation_同接口不同端口_相同`。
- 验证：filter `IcePriority` 与 `IceCandidatePair`、`IceFoundation`。
- 提交：`feat(ice): 候选、候选对与优先级计算`

#### T4.2 候选收集（host + srflx，两阶段的第一阶段）

- 文件：`Src/SAEA.P2P/Ice/CandidateGatherer.cs`、`IceOptions.cs`、`HostCandidateProvider.cs`
- 要点：
  - `IceOptions.AllowLoopbackCandidates`（默认 `false`）：为 `true` 时按接口枚举并**保留** loopback 候选（本地测试用），仍排除 v4-mapped；为 `false` 时按原规则排除 loopback 与 v4-mapped。测试进程靠该开关收集 `127.0.0.x` 候选。
  - host 候选地址取该端口 socket 的**实际绑定地址**（`LocalEndPoint`），不按网卡枚举另挑地址：虚拟主机绑 `127.0.0.2`~`127.0.0.8`，而枚举只给 `127.0.0.1`，用枚举值会让候选地址与 socket 不一致、检查包发不出去。开启开关时，不在枚举列表但确属本地绑定地址的回环地址也允许成为候选。
  - 集成与矩阵用例必须额外断言 selected pair 的本端与对端地址都落在本次虚拟主机地址集（`127.0.0.2`~`127.0.0.8`）内；开启开关后会带入真实网卡候选，不这样断言可能经真实网卡直连而误判通过。
  - 每 peer 会话独立端口集（A13），每个本地端口一个 host 候选；UDP socket 不 `Connect`，建立时 `SetNoTimeout`。
  - srflx 按地址族向 STUN 申请，多个 STUN 服务器取第一个成功；失败不阻断，只记日志。
  - 收集预算默认 3s（可配），到时先返回已有候选；trickle 回调 `OnCandidateDiscovered` 逐条吐出，结束时 `EndOfCandidates`。
  - v4-mapped v6 在进候选表前 `MapToIPv4()`；link-local 候选不与非 link-local 配对（过滤放在配对阶段，本任务只标属性）。
- 测试：`Ice/CandidateGathererTests.cs`：`CandidateGatherer_开启AllowLoopbackCandidates_产生回环host候选`、`CandidateGatherer_绑定127_0_0_3_候选地址等于绑定地址`、`CandidateGatherer_默认收集_不含loopback与v4映射`、`CandidateGatherer_收集超时_返回已有候选并标记结束`、`CandidateGatherer_两个会话_端口集不重叠`。
- 验证：filter `CandidateGatherer`。
- 提交：`feat(ice): host 与 srflx 候选收集及两阶段第一阶段`

#### T4.3 配对与检查表状态机

- 文件：`Src/SAEA.P2P/Ice/CheckList.cs`、`PairSelector.cs`、`PairState.cs`
- 要点：
  - 同地址族、同 component、transport 兼容才成对；UDP 只与 UDP 配；本地 `passive` 的 pair 剪除（TCP 规则阶段 5 接入）。
  - pair 状态机 `Frozen`/`Waiting`/`InProgress`/`Succeeded`/`Failed`；支持 triggered check 队列与解冻规则（同 foundation 的 pair 按顺序解冻），不做无状态串行扫描。
  - 排序按 pair priority 降序；`Ta=50ms` 起步，按进行中检查数放大。
  - 新候选到达时增量配对、增量插入，不重建整表；提名后的候选忽略并记日志。
- 测试：`Ice/CheckListTests.cs`：`CheckList_新候选到达_增量配对不重建`、`CheckList_同foundation_解冻顺序正确`、`CheckList_本地passive对_被剪除`、`CheckList_提名后新候选_被忽略`。
- 验证：filter `CheckList`。
- 提交：`feat(ice): 检查表状态机、解冻与增量配对`

#### T4.4 连通性检查与角色冲突

- 文件：`Src/SAEA.P2P/Ice/ConnectivityCheck.cs`、`Ice/RoleConflict.cs`、`Ice/IceRole.cs`
- 要点：
  - 请求携带 `USERNAME=remoteUfrag:localUfrag`、`MESSAGE-INTEGRITY`（对端 password）、`PRIORITY`（按 peer-reflexive 类型算，不是本地真实类型）、`ICE-CONTROLLING`/`ICE-CONTROLLED`（每个请求必带）、`USE-CANDIDATE`（仅提名）；响应也带 `MESSAGE-INTEGRITY`。
  - 487 处理严格按设计 §7.3：controlling 侧本地 tiebreaker ≥ 对端 → 回 487 保持 controlling，< 则转 controlled；controlled 侧 ≥ 对端 → 转 controlling，< 则回 487 保持 controlled；收到 487 必须切换角色 + 换 tiebreaker + 该 pair 回 triggered 队列重发，不限制翻转次数。
  - 从 relay 候选发起检查前，先对**对端所有候选 IP** 做 `CreatePermission`。
  - 每个 peer 独立 ufrag/pwd；密码不进日志与异常消息。
- 测试：`Ice/RoleConflictTests.cs`：`RoleConflict_控制方tiebreaker较大_回487并保持`、`RoleConflict_控制方tiebreaker较小_切换为受控`、`RoleConflict_收到487_更换tiebreaker并重发`、`RoleConflict_连续翻转_仍能收敛`；`Ice/ConnectivityCheckTests.cs`：`ConnectivityCheck_请求_PRIORITY按prflx类型计算`、`ConnectivityCheck_relay发起_先建立权限`。
- 验证：filter `RoleConflict` 与 `ConnectivityCheck`。
- 提交：`feat(ice): 连通性检查与 487 角色冲突收敛`

#### T4.5 提名与 peer-reflexive 候选

- 文件：`Src/SAEA.P2P/Ice/Nominator.cs`、`Ice/SelectedPair.cs`
- 要点：
  - regular nomination：对已成功 pair 用**全新 transaction ID** 再发 Binding 请求并带 `USE-CANDIDATE`，不是重传上一包。
  - 不支持 aggressive nomination；存在 TCP 候选时 regular 强制。
  - prflx 两条产生路径：收到检查请求且本地无对应远端候选时创建；收到响应且 `XOR-MAPPED-ADDRESS` 不匹配任何已知本地候选时创建（priority 取请求 `PRIORITY`，base 取该 pair 的本地候选）。
  - `SelectedPair` 发布后 `P2PClient.Send` 才允许发业务数据；未选出前 `Send` 直接抛错。
  - 被动侧不得自行提名。
- 测试：`Ice/NominatorTests.cs`：`Nominator_提名_使用新transactionId`、`Nominator_prflx_从请求创建`、`Nominator_prflx_从响应创建`、`Nominator_未选出pair_Send抛错`、`Nominator_被动侧_不提名`。
- 验证：filter `Nominator`。
- 提交：`feat(ice): regular 提名、peer-reflexive 候选与选中对`

#### T4.6 保活与 consent freshness

- 文件：`Src/SAEA.P2P/Ice/KeepAlive.cs`、`Ice/ConsentFreshness.cs`
- 要点：
  - 保活：选中 pair 上每 `Tr=15s`（不得小于 15s）发 **Binding Indication**（不认证，可带 `FINGERPRINT`）。
  - consent：每约 5s 发带认证的 Binding 请求，间隔 0.8~1.2 抖动；30s 内无有效响应即失效，停止该 pair 发送并丢弃。
  - 两套机制独立，不合并成一个定时器。
  - 计时器可注入，测试不真等。
- 测试：`Ice/KeepAliveTests.cs`：`KeepAlive_Tr小于15秒_被拒绝`、`ConsentFreshness_30秒无响应_pair被丢弃`、`ConsentFreshness_抖动在0_8到1_2之间`、`KeepAlive_Indication_不带认证`。
- 验证：filter `KeepAlive` 与 `ConsentFreshness`。
- 提交：`feat(ice): 保活指示与 consent freshness`

#### T4.7 数据面封装与解复用

- 文件：`Src/SAEA.P2P/Transport/DataPlaneCodec.cs`、`Transport/UdpDataChannel.cs`
- 要点：
  - 业务帧统一 1 字节前缀 `0xC0`；UDP 解复用：首字节 `0x00`~`0x3F` → STUN，`0xC0` → 业务，其余丢弃并计数。
  - TCP 直连统一 RFC 4571 两字节大端长度前缀（阶段 5 复用）；TURN `ChannelData` 路径按 T3.1 规则。
  - 长度上限与 `SocketOption.UDPMaxLength` 对齐；超长直接拒绝，不截断。
  - 一次回调恰好一条业务帧，不允许把两次发送合并或把一帧拆两次交付。
  - `Send` 为同步语义，内部按 `ITransport.SendToAsync` 发出；buffer 生命周期在调用返回后结束。
- 测试：`Transport/DataPlaneCodecTests.cs`：`DataPlaneCodec_业务帧_以0xC0开头`、`DataPlaneCodec_STUN首字节_归类为STUN`、`DataPlaneCodec_未知首字节_丢弃`、`DataPlaneCodec_超长帧_拒绝`、`UdpDataChannel_两次发送_两次独立回调`。
- 验证：filter `DataPlaneCodec` 与 `UdpDataChannel`。
- 提交：`feat(p2p): 数据面 0xC0 前缀封装与解复用`

#### T4.8 UDP 穿透矩阵集成

- 文件：`Src/SAEA.P2P.Tests/Ice/UdpMatrixTests.cs`、`Ice/ExpectedConnectivity.cs`
- 要点：
  - 期望值由测试内**独立的**「映射 × 过滤 → 连通性」判定函数推导，禁止调用被测 ICE 代码（含常量）。
  - 按设计 §8.2 表逐格断言：FullCone/Restricted/PortRestricted/Symmetric 四类互连；`relay` 格必须经 TURN 且双向数据往返成功；出现静默失败即失败；UDP 直连成功却选中 TCP/relay 判失败。
  - 每格用新的会话与端口集，避免复用映射表。
  - 每格额外断言 selected pair 的本端与对端地址都落在本次虚拟主机地址集（`127.0.0.2`~`127.0.0.8`）内，否则判失败并输出实际选中端点；`AllowLoopbackCandidates` 会带入真实网卡候选，不这样断言可能经真实网卡直连而误判通过。
- 测试：`UdpMatrix_NAT预设组合_期望与实测一致`（数据驱动逐格）、`UdpMatrix_PortRestricted对Symmetric_经relay成功`。
- 验证：`dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "FullyQualifiedName~UdpMatrix"`
- 提交：`test(ice): UDP 穿透矩阵与独立期望判定`

---

### 阶段 5：ICE-TCP（RFC 6544）+ RFC 6062 接入

**出口条件**：`active ↔ passive` 直连在 NAT 预置端口映射下通过；`so` 用例在 Windows 标记跳过并记录原因、代码路径存在；relay 候选经 RFC 6062 承载业务数据；选路阶梯按「UDP 直连 → TCP 直连 → TURN over UDP → over TCP → over TLS」顺序回退且每级有超时预算。

#### T5.1 TCP 候选收集

- 文件：`Src/SAEA.P2P/Ice/TcpCandidateGatherer.cs`、`Ice/CandidateCodec.cs`（`Ice/TcpCandidateType.cs` 已存在，直接复用）
- 要点：
  - 每接口每 component 收集 `passive` 与 `so` 两个 host 候选，各占不同端口；平台不支持 `so` 只收 `passive`。
  - `EnableTcpSimultaneousOpen` 默认 `false`；Windows 上强制 `false` 并记录一次日志（设计 §7.5 平台限制）。
  - `active` 候选在候选串里端口必须写 9，真实端口取自对端 passive 候选。
  - 第二阶段触发时机：UDP 全部 pair 失败后才补收集 TCP host 与 relay。
- 测试：`Ice/TcpCandidateGathererTests.cs`：`TcpCandidateGatherer_Windows_不收集so`、`TcpCandidateGatherer_每接口_产生passive候选`、`TcpCandidate_序列化_active端口写9`。
- 验证：filter `TcpCandidateGatherer` 与 `TcpCandidate_`。
- 提交：`feat(ice): TCP 候选收集与 Windows so 降级`

#### T5.2 TCP 直连连接与检查

- 文件：`Src/SAEA.P2P/Ice/TcpDirectChannel.cs`、`Transport/Rfc4571Framer.cs`
- 要点：
  - bind 前设地址复用；connect 失败按退避重试，`EADDRINUSE` 必须换本地端口重绑（单靠退避不会成功）。
  - STUN 与业务帧在 TCP 直连上按 RFC 4571 两字节大端长度前缀承载；TCP 上不做 STUN 层重传。
  - 连接建立后第一个响应不是 STUN 消息 → 关闭连接并把该远端候选的所有 pair 判 `Failed`。
  - 连接成功不等于打通：必须完成一次 Binding 事务才算检查通过。
  - 被动侧只应答入向连接与 STUN 请求，不主动发 ordinary check，不自行提名。
- 测试：`Transport/Rfc4571FramerTests.cs`：`Rfc4571_零长度帧_合法`、`Rfc4571_半包_等待`、`Rfc4571_粘包_逐帧产出`、`Rfc4571_超长帧_拒绝`；`Ice/TcpDirectTests.cs`：`TcpDirect_active连接passive_检查通过`、`TcpDirect_首响应非STUN_关闭并把pair判失败`。
- 验证：filter `Rfc4571` 与 `TcpDirect`。
- 提交：`feat(ice): TCP 直连通道、RFC 4571 分帧与连接检查`

#### T5.3 relay 候选接入（含 RFC 6062）

- 文件：`Src/SAEA.P2P/Ice/RelayCandidateProvider.cs`、`Transport/TurnDataChannel.cs`
- 要点：
  - UDP relay：`Allocate` → relay 候选（priority 0，related address 为映射地址）→ 检查前对端 IP `CreatePermission` → channel 或 Send/Data 承载业务。
  - TCP relay（RFC 6062）：TCP/TLS 控制连接申请 `REQUESTED-TRANSPORT=6`，`Connect` 后另开连接 `ConnectionBind`；数据连接建立后先跑一次 Binding 检查，随后 STUN 帧与业务帧都按 RFC 4571 分帧（`ConnectionBind` 自身不加长度前缀）。
  - relay 候选必须带 `relatedAddress`/`relatedPort`；释放时 `LIFETIME=0`。
  - 每 peer 独立 allocation，不与别的 peer 共享。
- 测试：`Ice/RelayCandidateTests.cs`：`RelayCandidate_优先级为0`、`RelayCandidate_检查前_已对peerIP建权限`、`RelayCandidate_TCP中继_数据连接仅承载业务帧`、`RelayCandidate_每peer独立_不复用allocation`。
- 验证：filter `RelayCandidate`。
- 提交：`feat(ice): relay 候选与 TURN UDP/TCP 数据通道接入`

#### T5.4 选路阶梯与两阶段收集编排

- 文件：`Src/SAEA.P2P/Ice/IceSession.cs`、`Ice/IceSessionOptions.cs`、`Ice/RouteLadder.cs`
- 要点：
  - 阶梯逐级、成功即停、失败进下一级：UDP 直连 → TCP 直连 → TURN over UDP → over TCP → over TLS；阶梯不读 NAT 行为标签。
  - 预算：UDP 直连 10s、TCP 直连 10s、每个 TURN 阶段 15s、会话总预算 90s（默认且必须 ≥ 收集 3s + 各级之和），全部可配；单 pair 超时 `min(RTO 推导值, 所属阶段剩余预算)`。
  - 第二阶段收集在 UDP 全失败后触发；`EndOfCandidates` 后不再等待新候选。
  - 阶段切换必须清理上一阶段的非选中 pair 与临时 socket，避免端口泄漏。
- 测试：`Ice/RouteLadderTests.cs`：`RouteLadder_UDP直连成功_不进入TCP`、`RouteLadder_UDP全失败_补收集TCP与relay`、`RouteLadder_TURN三级_按UDP到TLS顺序`、`RouteLadder_总预算超时_抛出带阶段信息的错误`。
- 验证：filter `RouteLadder`（`IceSession` 用例同过滤器）。
- 提交：`feat(ice): 选路阶梯、阶段预算与两阶段收集编排`

#### T5.5 TCP 穿透矩阵

- 文件：`Src/SAEA.P2P.Tests/Ice/TcpMatrixTests.cs`
- 要点：按设计 §8.2 的 TCP 场景表逐格断言；`so` 用例 `Assert.Inconclusive` 并写明「Windows 不支持同时打开（D12）」，不得静默跳过；passive 侧 NAT 必须预置端口保持映射；直连成功的格额外断言两端端点都在虚拟主机地址集内。
- 测试：`TcpMatrix_active对passive_直连成功`、`TcpMatrix_passive侧端口保持_可被连接`、`TcpMatrix_so组合_标记跳过并记录原因`、`TcpMatrix_TCP失败_回退TURN UDP`。
- 验证：`dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "FullyQualifiedName~TcpMatrix"`
- 提交：`test(ice): TCP 穿透矩阵与 so 跳过记录`

#### T5.6 TURN 级内 RFC 6062 回退

- 文件：`Src/SAEA.P2P/Ice/TcpRelayCandidateProvider.cs`、`Ice/IceSession.cs`、`Src/SAEA.P2P.Tests/Ice/RelayCandidateTests.cs`、`Ice/IceSessionTests.cs`
- 要点：
  - 第 4、5 级内固定两段：半个阶段预算试 UDP 中继，剩余预算试 RFC 6062 TCP 中继；第 3 级只用满额预算做 UDP 中继，不试 TCP 中继（UDP 控制不支持 `Connect`/`ConnectionBind`）。
  - `TcpRelayCandidateProvider` 构造时即订阅 `ConnectionAttemptReceived`（先订阅后申请分配，避免事件早于订阅丢失），产出 `Type=Relay`、`Transport=Tcp`、`TcpType=None` 的候选，不补 `relatedAddress`/`relatedPort`。
  - 会话级强制分配传输：UDP 段 `REQUESTED-TRANSPORT=17`、TCP 段 `6`；TLS 段为数据连接配 `SaeaTlsTcpTransport`，满足服务端「数据连接与控制连接同加密层」的要求。
  - 控制方 `Connect` + `ConnectionBind` 后以 `USE-CANDIDATE` 做一次检查并选中；受控方等入向 `ConnectionAttempt` 并 `ConnectionBind`，自身检查成功即选中；数据连接复用 `TcpDirectChannel` 的 RFC 4571 分帧。
  - 成功时 `_tcpRelayClient`/`_tcpRelayProvider`/`_tcpDirectChannel` 由会话持有并负责刷新，失败与 `Dispose` 路径发 `LIFETIME=0` 后释放；`IceSessionResult` 用 `IsRelay && UsesTcp` 标识 TCP 中继，不加字段。
  - 已知风险：UDP 段在对端没有 UDP 中继候选时必然吃满半额预算；阶段不对称（一端 UDP 中继成功、另一端失败）不修；受控方提前判定成功存在窄窗口。用例阶段预算随之上调到 10s。
- 测试：`TcpRelayCandidate_申请与入向连接_产出TCP中继候选并可Bind`、`StartAsync_UdpRelayBlocked_SelectsTurnTcpRelay`、`StartAsync_UdpAndTcpControlBlocked_SelectsTurnTlsTcpRelay`。
- 验证：`dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "FullyQualifiedName~TcpRelayCandidate|FullyQualifiedName~SelectsTurnTcpRelay|FullyQualifiedName~SelectsTurnTlsTcpRelay"`；全量 Release 为 `197 passed / 1 skipped / 0 failed`。
- 提交：`feat(ice): TURN TCP 中继候选提供器`、`feat(ice): TURN 级内接入 RFC 6062 TCP 中继回退`、`feat(ice): TLS 控制下接入 TCP 中继数据连接`、`docs(ice): 回写 TCP 中继级内两段设计`

---

### 阶段 6：信令重构 + 数据面接管 + 删除旧代码

**出口条件**：新 `P2PClient` 能在两个虚拟主机间完成「注册 → 交换候选 → 选路 → 双向业务帧」；`Src/SAEA.P2P` 里不再有 `Relay/`、`NAT/HolePuncher`、旧 `Channel/` 类型；`SAEA.P2PTest` 全部用例可运行；`dotnet build Src/SAEA.Sockets.sln -c Debug` 0 errors。

**顺序约束**：T6.1→T6.2/6.3→T6.4/6.5 完成并编译通过后，才允许 T6.6 删旧类型；删旧与重写 `SAEA.P2PTest` 必须同一提交序列内完成，避免中间态长期不可编译。

#### T6.1 信令消息模型与编解码

- 文件：`Src/SAEA.P2P/Protocol/P2PMessageType.cs`、`P2PProtocol.cs`、`P2PCoder.cs`、`Signaling/SignalingMessage.cs`、`Signaling/SignalingJsonCodec.cs`
- 要点：
  - 报文 = 长度前缀 + 类型 + 负载（与既有 `P2PCoder` 对齐，不重造帧格式）；负载用 `SAEA.Common.Newtonsoft.Json` 序列化，不新增包。
  - 消息集按设计 §7.9：`Register`/`RegisterAck`/`AuthChallenge`/`AuthResponse`/`ConnectRequest`/`SessionOffer`/`SessionAnswer`/`TrickleCandidate`/`EndOfCandidates`/`TurnCredential`/`SessionClose`/`Error`。
  - `candidates[]` 每项字段与 ICE 候选一一对应：foundation、component、transport、priority、address、port、type、relatedAddress、relatedPort、tcpType；地址族由 address 判定，不另设字段。
  - `SessionAnswer` 的 role/tiebreaker 填**应答方自己的值**。
  - 协议版本字段与不认识的字段：版本不匹配回 `Error`，未知字段忽略（向前兼容）。
- 测试：`Src/SAEA.P2P.Tests/Signaling/SignalingCodecTests.cs`：`SignalingCodec_全消息集_编码解码回环`、`SignalingCodec_SessionAnswer_role为应答方自身值`、`SignalingCodec_未知字段_忽略`、`SignalingCodec_版本不匹配_返回Error`。
- 验证：filter `SignalingCodec`。
- 提交：`feat(p2p): 新信令消息集与 JSON 编解码`

#### T6.2 `SignalingClient`

- 文件：`Src/SAEA.P2P/Signaling/SignalingClient.cs`、`SignalingOptions.cs`
- 要点：
  - TCP（可开 TLS，`SocketOptionBuilder.WithSsl`）；只承担注册、候选交换、凭据下发、会话拆除；不承载业务数据。
  - 注册 + `AuthChallenge`/`AuthResponse` HMAC；断线自动重连并重新注册，重连期间不丢已知候选（按 sessionId 缓冲后重发）。
  - 服务端下发的 `TurnCredential` 透传给 TURN 客户端（realm/username/password/ttl/urls）。
  - 不把信令连接的对端地址当作打洞地址（设计 §六 职责边界）。
  - 密码、凭据不进日志与异常文本。
- 测试：`Signaling/SignalingClientTests.cs`（进程内起 `P2PServer`）：`SignalingClient_注册_收到RegisterAck`、`SignalingClient_断线重连_重发未完成候选`、`SignalingClient_TurnCredential_透传字段完整`。
- 验证：filter `SignalingClient`。
- 提交：`feat(p2p): 信令客户端与重连、凭据透传`

#### T6.3 `P2PServer` 收窄为信令服务器

- 文件：`Src/SAEA.P2P/Core/P2PServer.cs`、`Security/*`、`Builder/P2PServerOptions.cs`
- 要点：
  - 保留：节点注册与唯一性、按需转发候选、下发 TURN 临时凭据、会话生命周期与超时清理。
  - 删除：`ActiveRelaySessionCount`、`OnRelayStarted`、`OnRelayEnded`、`ProcessRelayData`、`ServerRelayOptions`（含 `MaxRelaySessions`）、`ErrorCode.RelayFailed` 引用。
  - 会话超时清理与 `SessionClose` 处理；同一 peerId 重复注册按配置拒绝或顶替，行为写进 README。
  - 信令服务器配置里必须有与 TURN 相同的 `SharedSecret`/`Realm`/`CredentialTtlSeconds`（设计 §7.7 签发链）。
- 测试：`Signaling/P2PServerTests.cs`：`P2PServer_重复peerId_按策略处理`、`P2PServer_会话超时_清理并通知`、`P2PServer_不转发业务数据`（断言无该消息类型）。
- 验证：filter `P2PServerTests`。
- 提交：`refactor(p2p): 信令服务器收窄，删除自定义中继职责`

#### T6.4 公开 API 重写

- 文件：`Src/SAEA.P2P/Core/P2PClient.cs`、`Core/P2PConnection.cs`、`Channel/ChannelType.cs`、`Channel/ChannelState.cs`、`Common/ErrorCode.cs`、`Common/NodeState.cs`、`Core/PeerSession.cs`
- 要点按设计 §7.11：
  - `ChannelType`：`UdpDirect=0`、`TcpDirect=1`、`RelayUdp=2`、`RelayTcp=3`、`RelayTls=4`、`Local=5`。
  - `public delegate void P2PDataReceivedHandler(string peerId, ReadOnlySpan<byte> data);`（ref struct 不能作泛型实参）。
  - `P2PConnection`：`PeerId`、`Channel`、`LocalEndPoint`、`RemoteEndPoint`。
  - `P2PClient`：`RegisterAsync`、`ConnectToPeerAsync`、`Send(string, ReadOnlySpan<byte>)`、`DisconnectPeerAsync`、`OnPeerConnected`、`OnPeerDisconnected`、`OnPeerDiscovered`（仅无信令局域网模式）、`OnDataReceived`；`IDisposable`。入向会话不设单独 `Accept` 入口，选路完成后同样触发 `OnPeerConnected`（设计 §7.11 语义约定）。
  - `Send` 在未选出 pair 时抛 `P2PException(NotConnected)`，不排队；对未知 peerId 同样抛错，不静默丢弃。
  - `ErrorCode` 新增 `Ice*`、`Turn*`、`NoCandidatePair`、`NotConnected`；删除 `Punch*`、`HolePunch*`、`Relay*`；`NodeState` 删除 `HolePunching`/`Authenticating` 等与新流程不对应的值。
  - `PeerSession` 删除 `RelaySessionId`，状态机改成「Signaling → Gathering → Checking → Connected → Closed」。
  - 每个公开成员补齐 `summary`/`param`/`returns` 注释。
- 测试：`Src/SAEA.P2P.Tests/Core/P2PClientApiTests.cs`：`P2PClient_未连接Send_抛NotConnected`、`P2PClient_连接后Channel_与选中对一致`、`P2PClient_Dispose_释放所有会话端口`、`P2PClient_OnDataReceived_一次回调一条帧`。
- 验证：filter `P2PClientApi`。
- 提交：`feat(p2p): 按 ICE 重写客户端公开 API 与状态模型`

#### T6.5 Builder / Options / Quick 调整

- 文件：`Src/SAEA.P2P/Builder/P2PClientBuilder.cs`、`P2POptions.cs`、`P2PServerBuilder.cs`、`P2PServerOptions.cs`、`Shortcut/P2PQuick.cs`
- 要点：
  - 删 `HolePunchOptions`、`RelayOptions`、`HolePunchStrategy`、`EnableHolePunch(...)`、`Server(..., relay)`；新增 `IceOptions`、`TurnOptions`、`SignalingOptions`，并有对应 `UseIce(...)`/`UseTurn(...)`/`UseSignaling(...)`。
  - `Validate()`：互斥项（如 `TcpSimultaneousOpen` 与 Windows）给出明确错误；旧的 `ErrorCode.RelayFailed` 换新错误码。
  - `P2PQuick`：`Client`/`ClientFull`/`Server` 三入口按新 builder 调整，`LocalNet` 语义保留；示例参数与 README 同步。
- 测试：`Src/SAEA.P2P.Tests/Core/BuilderTests.cs`：`P2PClientBuilder_默认配置_校验通过`、`P2PClientBuilder_Windows启用so_校验失败并说明原因`、`P2PQuick_ClientFull_不接收旧策略参数`（编译期即验证，用例只断言默认值）。
- 验证：filter `P2PClientBuilder` 与 `P2PQuick`。
- 提交：`refactor(p2p): builder 与快捷入口改用 ICE/TURN 配置`

#### T6.6 删除旧类型

- 文件：删除 `Src/SAEA.P2P/NAT/HolePuncher.cs`、`HolePunchResult.cs`、`HolePunchStrategy.cs`、`NATDetector.cs`、`NATType.cs`；删除 `Src/SAEA.P2P/Channel/TCPChannel.cs`、`UDPChannel.cs`；删除整个 `Src/SAEA.P2P/Relay/` 目录
- 要点：按设计 §十 清单逐项删；`P2PMessageType` 的打洞与中继消息、`ErrorCode` 的 `Punch*`/`HolePunch*`/`Relay*` 同步删；删后全仓搜残留引用（含 README、`SAEA.P2PTest`、`SAEA.Mvc*` 若有）。
- 测试：删除后 `dotnet build Src/SAEA.Sockets.sln -c Debug` 必须 0 errors；`Src/SAEA.P2PTest` 编译通过。
- 验证：`dotnet build Src/SAEA.Sockets.sln -c Debug`
- 提交：`refactor(p2p): 删除自定义打洞、NAT 探测与中继实现`

#### T6.7 重写 `SAEA.P2PTest`

- 文件：`Src/SAEA.P2PTest/Tests/` 下 `RelayTest.cs`、`RelayAdvancedTest.cs`、`HolePunchTest.cs`、`ProtocolTest.cs`、`ProtocolAdvancedTest.cs`、`ClientTest.cs`、`IntegrationTest.cs`、`BuilderTest.cs`、`QuickStartTest.cs`、`LocalDiscoveryTest.cs`、`LegacyDecoder.cs`、`ServerTest.cs`、`ErrorCodeTest.cs`、`LifecycleTest.cs`、`AuthEncryptionTest.cs`、`SecurityAdvancedTest.cs`、`PerformanceTest.cs`、`ConcurrencyTest.cs`、`EdgeCaseTest.cs`、`Program.cs`
- 要点：
  - 删除引用旧类型的用例；`RelayTest`/`RelayAdvancedTest` 改成「TURN 中继」用例，`HolePunchTest` 改成「ICE 打洞」用例。
  - `IntegrationTest` 的 `RelaySessionId` 断言换成 `ChannelType` 断言。
  - 保持不动：`SpanPipelineTest`、`StreamPipelineTest`、`StreamDecoderTest`、`UdpPipelineTest`、`TlsTest`、`TlsInteropTest`、`TlsStressTest`、`TlsBenchmark`、`IocpBenchmark`。
  - `Program.cs` 菜单与命令行参数同步；自动化场景必须显式传参（不落交互菜单）；**保留 T3.9 的 `--server` / `--config` 模式**，重写后仍能起 STUN/TURN 宿主。
- 验证：`dotnet build Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Release`，再跑 `dotnet run --project Src/SAEA.P2PTest -c Release -- --smoke`（若 `Program.cs` 无该参数则本任务补一个只跑不依赖外网的入口），最后确认 `--server --help` 仍可用。
- 提交：`test(p2p): 重写自测入口到 ICE/TURN 用例`

#### T6.8 端到端：两客户端 + 信令 + 三种通道

- 文件：`Src/SAEA.P2P.Tests/Integration/EndToEndTests.cs`
- 要点：进程内起信令服务器 + STUN + TURN；A、B 各在 NAT 后，走完整 `RegisterAsync` → 候选交换 → 选路 → 双向业务帧；分别断言 `ChannelType.UdpDirect`、`TcpDirect`、`RelayUdp`；断言数据不经过信令连接（信令侧统计不到业务帧）。
- 测试：`EndToEnd_UDP直连_通道类型与双向数据正确`、`EndToEnd_TCP直连_通道类型正确`、`EndToEnd_UDP失败_回退RelayUdp`、`EndToEnd_信令连接_不承载业务数据`。
- 验证：filter `EndToEnd`。
- 提交：`test(p2p): 两客户端经信令与 NAT 的端到端用例`

#### T6.9 全仓回归

- 验证：
  - `dotnet build Src/SAEA.Sockets.sln -c Debug`
  - `dotnet test Src/SAEA.Common.Tests/SAEA.Common.Tests.csproj -c Release`
  - `dotnet test Src/SAEA.MQTT.Tests/SAEA.MQTT.Tests.csproj -c Release`
  - `dotnet test Src/SAEA.FTP.Tests/SAEA.FTP.Tests.csproj -c Release`（FTP 控制通道改动已在 `68701195` 合入，此项失败按真问题处理；本次任务不改 FTP 代码）
  - `dotnet test Src/SAEA.Sockets.Pipeline.Tests/SAEA.Sockets.Pipeline.Tests.csproj -c Release`
  - `dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release`
- 提交：`test(p2p): 阶段 6 全仓回归通过`

---

### 阶段 7：无信令局域网直连

**出口条件**：同一机器上两个 `P2PClient` 不连信令服务器，经组播发现交换 ufrag/pwd/候选并完成直连，`ChannelType.Local`；HMAC 校验失败时拒绝并不抛裸异常。

#### T7.1 发现报文扩展

- 文件：`Src/SAEA.P2P/Discovery/DiscoveryPacket.cs`、`Discovery/DiscoveryHmac.cs`、`Discovery/DiscoveredNode.cs`
- 要点：
  - 报文携带 nodeId、nodeName、ufrag、pwd、候选列表、协议版本、时间戳；`LocalAddress` 不再恒为 null（补齐真实本地端点）。
  - HMAC 只保证完整性，不提供身份鉴权：密钥来自配置；README 必须写明「无信令模式没有身份鉴权，只应在本链路使用」。
  - 报文长度上限与 `UDPMaxLength` 对齐；超长直接丢弃。
- 测试：`Discovery/DiscoveryPacketTests.cs`：`DiscoveryPacket_编码解码回环_字段完整`、`DiscoveryPacket_HMAC不匹配_拒绝`、`DiscoveryPacket_超长_丢弃`。
- 验证：filter `DiscoveryPacket`。
- 提交：`feat(p2p): 局域网发现报文扩展与 HMAC 校验`

#### T7.2 `LocalDiscovery` 改造与直连建立

- 文件：`Src/SAEA.P2P/Discovery/LocalDiscovery.cs`
- 要点：改用 T0.3 的 `StartReceive()` + `OnClientReceiveSpanFrom`（未连接也能收），组播地址与选项按地址族设置；发现到的节点带候选，直接交给 `IceSession` 走内网 host 候选直连；`ChannelType.Local` 与 `UdpDirect` 的语义差异写进注释与 README。
- 测试：`Discovery/LocalDiscoveryTests.cs`：`LocalDiscovery_两台实例_互相发现`、`LocalDiscovery_HMAC错误_不加入列表`、`LocalDiscovery_发现后_建立Local通道`。
- 验证：filter `LocalDiscovery`。
- 提交：`feat(p2p): 无信令局域网发现与 Local 通道`

#### T7.3 阶段 7 集成

- 测试：`Src/SAEA.P2P.Tests/Integration/LocalNetTests.cs`：`LocalNet_无信令_双向业务帧成功`、`LocalNet_过期节点_从列表移除`。
- 验证：`dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "FullyQualifiedName~LocalNet"`
- 提交：`test(p2p): 无信令局域网直连集成`

---

### 阶段 8：IPv6

**出口条件**：`::1` 上全链路可用（host/srflx/relay）；v6 中继可分配；双栈监听能同时收 v4/v6；跨族配对默认关闭、开启后可配对；v4-mapped 在候选与 STUN 属性里被归一化。

#### T8.1 双栈监听与 v6 中继

- 文件：`Src/SAEA.P2P/StunServer/StunServerEndpoint.cs`、`TurnServer/TurnServer.cs`、`RelayPortAllocator.cs`
- 要点：`EnableIpv6` / `EnableDualMode` 配置生效；`IPv6Any` + `DualMode` 同时接受 v4/v6；`REQUESTED-ADDRESS-FAMILY` / `ADDITIONAL-ADDRESS-FAMILY` 支持 v6 中继，不支持回 440；v6 中继地址从配置范围分配。
- 测试：`Src/SAEA.P2P.Tests/DualStack/DualStackTests.cs`：`StunServer_双栈监听_IPv4与IPv6均可Binding`、`TurnServer_请求IPv6中继_返回v6地址`、`TurnServer_不支持地址族_返回440`。
- 验证：filter `DualStack`。
- 提交：`feat(stun/turn): 双栈监听与 IPv6 中继`

#### T8.2 v6 候选与 `::1` 全链路

- 文件：`Src/SAEA.P2P/Ice/CandidateGatherer.cs`（v6 分支）、`Ice/IceSession.cs`
- 要点：按地址族独立收集；`::1` 靠 `AllowLoopbackCandidates = true` 进入候选（测试专用，不靠自动收集）；v4-mapped 归一化在候选、STUN 属性、配对三处都执行。
- 测试：`Ice/Ipv6CandidateTests.cs`：`CandidateGatherer_指定IPv6回环_产生v6候选`、`CandidateGatherer_v4映射地址_归一化为v4`、`IceSession_IPv6回环_直连成功`。
- 验证：filter `Ipv6`。
- 提交：`feat(ice): IPv6 候选收集与回环全链路`

#### T8.3 跨地址族配对开关

- 文件：`Src/SAEA.P2P/Ice/IceOptions.cs`、`Ice/CheckList.cs`
- 要点：`AllowCrossFamilyPairs` 默认 `false`；开启后 v4 与 v6 候选可配对，但 v4-mapped 仍不参与；关闭时静默不配对并计数。
- 测试：`Ice/CrossFamilyTests.cs`：`CheckList_跨族默认关闭_不配对`、`CheckList_开启跨族_v4与v6配对成功`。
- 验证：filter `CrossFamily`。
- 提交：`feat(ice): 跨地址族配对开关`

#### T8.4 IPv6 用例边界记录

- 要点：NAT66 不实现（D13）；`::1` 之外的非管理员 v6 地址不可用（探针实测）；交付报告必须写明「IPv6 仅验证可自动化部分」。
- 验证：`dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "FullyQualifiedName~Ipv6|FullyQualifiedName~CrossFamily|FullyQualifiedName~DualStack"`
- 提交：`test(p2p): 阶段 8 IPv6 用例边界与跳过说明`

---

### 阶段 9：全矩阵 + 文档 + 互通清单

**出口条件**：一条命令跑完全部 P2P 用例；README 中英一致；互通清单每条都标注「未运行」或有本机输出；交付报告写明已知限制与未覆盖面。

#### T9.1 统一验收入口

- 文件：`Src/SAEA.P2P.Tests/P2PTestSuite.cs`（或等价的测试过滤器约定）、`Src/SAEA.P2PTests.runsettings`（可选）
- 要点：用 `--filter` 组合或 `[TestCategory]` 分组（`Unit`/`Nat`/`Matrix`/`Ipv6`/`LocalNet`），保证一条命令可跑全矩阵；长耗时用例（矩阵）与单元用例分开，报告里分别给耗时。
- 验证：`dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release`
- 提交：`test(p2p): 统一验收入口与用例分组`

#### T9.2 README 中英同步

- 文件：`Src/SAEA.P2P/README.md` + `README.en.md`（客户端与服务端两大部分合并在此，不再单列 StunTurn 工程）、根 `README.md` + `README.en.md`
- 要点：
  - 删「99% 穿透率」等无依据数字、端到端加密承诺、旧架构图中的 `HolePuncher`/`PunchSync`/`RelayManager`/`RelaySession`。
  - 写清 `ChannelType` 语义、直连与 TURN 回退顺序、IPv6 与局域网模式的安全边界、数据面不加密。
  - 替换所有 `EnableHolePunch`、`HolePunchStrategy`、`P2PQuick.Server(..., relay)` 示例。
  - 服务端章节：配置项、`dotnet run --project Src/SAEA.P2PTest -c Release -- --server --config <path>` 的部署方式、鉴权模型、端口同时应答 Binding、与 coturn 对齐点。
  - 根 README 组件清单与依赖方向同步（`SAEA.P2P` 现在内含 STUN/TURN 服务端与客户端）。
- 验证：中英两份逐段对照（同一小节标题一一对应，示例代码一致）。
- 提交：`docs(p2p): 中英 README 同步到 ICE/STUN/TURN 架构`

#### T9.3 互通核对清单

- 文件：`docs/superpowers/specs/2026-11-11-p2p-interop-checklist.md`
- 要点：公共 STUN（如 stun.l.google.com）、coturn 的 UDP/TCP/TLS 三种传输、RFC 5769 工具；每条给命令、预期输出、实际结果列；**本轮全部标「未运行」**并写明原因（无公网环境、D9）。不得写成「已验证」。
- 提交：`docs(p2p): 交付 STUN/TURN 互通核对清单（未运行）`

#### T9.4 交付报告

- 文件：`docs/superpowers/plans/2026-11-11-p2p-acceptance.md`
- 要点：设计 §8.3 每条验收标准对应实测输出；已知限制（D12/D13/D15、NAT 代理模型边界、TCP 成功率不承诺）；未覆盖面清单；`so` 代码路径未在本平台验证的说明。
- 提交：`docs(p2p): 交付验收报告与已知限制`

---

## 五、验收命令与预期

全量（含矩阵，时间较长）：

```bash
dotnet build Src/SAEA.Sockets.sln -c Debug
dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release
dotnet test Src/SAEA.Sockets.Pipeline.Tests/SAEA.Sockets.Pipeline.Tests.csproj -c Release
```

既有回归（四个 MSTest 不在 sln，必须逐个跑）：

```bash
dotnet test Src/SAEA.Common.Tests/SAEA.Common.Tests.csproj -c Release
dotnet test Src/SAEA.MQTT.Tests/SAEA.MQTT.Tests.csproj -c Release
dotnet test Src/SAEA.FTP.Tests/SAEA.FTP.Tests.csproj -c Release
dotnet test Src/SAEA.Sockets.Pipeline.Tests/SAEA.Sockets.Pipeline.Tests.csproj -c Release
```

自测入口（非 MSTest，需 .NET 8 runtime）：

```bash
# 客户端流程冒烟（不依赖外网）
dotnet run --project Src/SAEA.P2PTest -c Release -- --smoke

# 服务端宿主：起 STUN/TURN（T3.9）
dotnet run --project Src/SAEA.P2PTest -c Release -- --server --config <临时配置路径>
```

服务端宿主的验收方式：`--server` 进程保持运行，另一个进程跑 `dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "FullyQualifiedName~StunNatIntegration"`，测试侧命中该进程端口即算通过；`--help` 输出人工核对无 `SharedSecret`、证书口令。

记录口径：构建 0 errors；测试给 `Passed!/Failed!` 与用例数；环境缺失（如需管理员、无公网）写「环境原因」，只做静态核对写「未运行」。

---

## 六、风险与未覆盖面

| 风险 | 触发条件 | 计划内对策 |
|---|---|---|
| 阶段 0 的 `SAEA.Sockets` 增量改坏既有用法 | 新增 `StartReceive()` 复用连接接收路径时改了原语义 | T0.1–T0.4 每任务跑 `SAEA.Sockets.Pipeline.Tests` 全量，不只跑新用例 |
| 两阶段收集依赖 trickle，trickle 有缺陷会让 TCP/relay 回退整体失效 | 阶段 4 的增量配对写错 | T4.3 专门覆盖「新候选增量配对」；T5.4 覆盖阶段切换清理 |
| NAT 代理只能证明模型，不能证明内核 NAT | 任何矩阵结论 | 报告里写明边界（设计 §8.1）；不用矩阵结果承诺公网成功率 |
| Windows 无法验证 `so` 与 TCP 同时打开 | D12 | 用例 `Assert.Inconclusive` + 记录原因；代码路径保留 |
| 非管理员只有 `::1` | D13 | IPv6 只做可自动化部分；NAT66 跳过并记录 |
| 数据面不加密 | D15 | README 中英明确告知；不写「端到端加密」 |
| 端口/句柄泄漏导致矩阵后段失败 | 每会话独立端口集、阶段切换 | T4.2/T5.4 断言端口集不重叠、阶段清理；矩阵每格新会话 |
| 全仓回归里 `SAEA.FTP.Tests` 失败 | FTP 基线本身的问题（非本任务引入） | T6.9 如实记录失败用例；不改 FTP 代码，报告里标明「与本任务无关」 |
| `AllowLoopbackCandidates` 带入真实网卡，矩阵「因为错误原因通过」 | R2 决策的固有副作用 | T4.8/T5.5 断言 selected pair 两端都落在 `127.0.0.2`~`127.0.0.8`，越界判失败并打印实际端点 |
| 单包分层后服务端类型被客户端流程反向依赖，服务端被拖进客户端生命周期 | 阶段 1–3 写服务端时顺手引用 `P2PClient` / 信令类型 | T1.1 起限定服务端侧只依赖 `Stun/`、`Turn/`、`Auth/`、`SAEA.Common`、`SAEA.Sockets`；代码审查按此条拦 |

未覆盖面（必须写进交付报告）：

- 公网 STUN/coturn 互通未运行（仅清单）。
- NAT66、TCP 同时打开、Windows 上的多 IPv6 地址。
- 真实运营商级 NAT 的差异；矩阵只覆盖代理模型下的四类行为。
- TURN 的带宽/字节配额只做错误码映射与单元断言，不做压测。

---

## 七、交付物清单

- 代码：`Src/SAEA.P2P/` 下新增 `Stun/`、`Turn/`、`StunServer/`、`TurnServer/`、`Auth/`、`Ice/`、`Transport/`、`Signaling/`，以及按新架构重写的 `Core/`、`Channel/`、`Protocol/`、`Discovery/`、`Builder/`、`Shortcut/`；`Src/SAEA.P2PTest/` 新增 `--server` 宿主模式。
- 测试：`Src/SAEA.P2P.Tests/`（不进 sln）、`Src/SAEA.Sockets.Pipeline.Tests/EndpointApiTests.cs`。
- 文档：`Src/SAEA.P2P/README.md` + `README.en.md`（客户端 + 服务端）、根 `README.md` + `README.en.md`、`docs/superpowers/specs/2026-11-11-p2p-interop-checklist.md`、`docs/superpowers/plans/2026-11-11-p2p-acceptance.md`。
- 设计回写（开工前完成）：A1 工程布局、A2 版本口径、§六 架构图、§7.3 增加 `AllowLoopbackCandidates`、§十 文件清单路径、§十一 阶段措辞；`SAEA.P2P.Tests` 的用例落点若与设计 §8.2 表述有差异，一并回写。