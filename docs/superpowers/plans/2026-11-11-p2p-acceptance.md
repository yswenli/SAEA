# SAEA.P2P 交付验收报告（ICE / STUN / TURN）

- 报告日期：2026-11-16
- 被测组件：`Src/SAEA.P2P`（客户端 + STUN/TURN 服务端），测试工程 `Src/SAEA.P2P.Tests`，宿主与自测入口 `Src/SAEA.P2PTest`
- 关联文档：[设计文档](../specs/2026-11-11-saea-p2p-ice-stun-turn-design.md)、[实施计划](./2026-11-11-saea-p2p-ice-stun-turn-plan.md)、[互通核对清单](../specs/2026-11-11-p2p-interop-checklist.md)
- 数据口径：本报告全部数字取自本轮在本机的实测输出，未复跑、未估算；未执行的项一律写「未运行」；无法核实的事项写「未核实」。

## 一、结论

1. 本地自动化验收通过：`dotnet build Src/SAEA.Sockets.sln -c Debug` 0 错误（17.49 s，退出码 0）；`Src/SAEA.P2P.Tests` 全量 245 个用例中 243 通过、2 跳过，耗时 4 分 17 秒。
2. 两个跳过项都落在「8.3 验收标准」第 7 条的白名单内：Windows 无 TCP 同时打开、NAT66 需管理员预置。
3. 面向用户的文档（`Src/SAEA.P2P/README.md`、`README.en.md`、根 `README.md`、`README.en.md`）已无旧中继命名，旧架构图整节删除；可执行代码侧 `git grep -n -E "RelayManager|RelaySession|RelayData" -- Src` 无输出。设计与历史文档中出现的旧名属变更记录，不在该检查范围内；组件 README 仍如实写明数据面不加密。
4. 本轮**未做联网互通**：13 条互通项全部「未运行」，因此本报告不给出任何与 coturn / 公共 STUN 的互通结论。
5. 三个能力缺口必须在交付说明中保留：Windows 上 `so`（TCP 同时打开）路径未在本平台验证；NAT66 与 `::1` 以外的 IPv6 地址未覆盖；TCP 直连成功率不对公网做承诺。
6. 第 1 条标准的「统一 `Port=0`」措辞对 TURN 中继端口不适用：监听端点用 `Port=0`，中继端口在配置区间内随机分配并在占用时重试；建议对齐措辞，详见第二节第 1 条。

## 二、验收标准逐条对照

对照对象为设计文档「8.3 验收标准」的 0–7 条。

| 条目 | 要求摘要 | 实测证据 | 结论 |
|---|---|---|---|
| 0 | 验收平台为 Windows、非管理员；不承诺同一套用例在 Linux/macOS 自动通过 | 本轮全部命令在 Windows（`Microsoft Windows NT 10.0.26300.0`）非管理员账户下执行（`IsInRole(Administrator)` = `False`）；矩阵经回环地址段 `127.0.0.x` 的虚拟主机完成（`Ice/UdpMatrixTests` 的 `ScenarioAddresses` 为 `127.0.0.2`–`127.0.0.8`，共 7 个） | 通过 |
| 1 | 测试全部通过、可离线重复运行、不依赖外网、不依赖固定端口（统一 `Port=0` + `LocalEndPoint` 取实际端口） | 全量 243 通过 / 2 跳过 / 共 245；无外网依赖（STUN/TURN 服务端在用例进程内启动）；监听端点使用 `Port=0`。但 TURN 中继端口不是 `Port=0`：`Ice/UdpMatrixTests` 用 56000–56999，`Integration/EndToEndTests` 用 57000–57999，`Ice/TcpMatrixTests` 用 `ReserveRelayPorts` 预留端口 | 符合（附口径差异）：离线可重复、不依赖外网与固定单端口；监听端点用 `Port=0`，中继端口用配置区间并随机探测、绑定失败重试 |
| 2 | 穿透矩阵与设计期望表逐格一致，期望值不得引用被测 ICE 代码 | `Ice/UdpMatrixTests` 16 个 `DataRow` 覆盖全表，`Ice/ExpectedConnectivity` 为独立的「映射×过滤 → 连通性」判定函数（不引用被测 ICE 实现），另有 `Ice/ExpectedConnectivityTests` 逐格核对表；`Ice/TcpMatrixTests` 4 个场景、`Turn/TurnTransportMatrixTests` 4 个场景；矩阵断言 selected pair 两端端点必须属于本场景地址集 | 通过 |
| 3 | 删除自定义中继后 `RelayManager`、`RelaySession`、`RelayData` 在可执行代码与面向用户文档中不再出现 | `git grep -n -E "RelayManager\|RelaySession\|RelayData" -- Src` 无输出、返回码 1；组件 README 中英两份已整体重写，旧架构章节（含 `RelayManager` / `RelaySession` 两行）整节删除 | 通过 |
| 4 | IPv4 与 IPv6 两组端到端用例通过，无信令局域网直连用例通过 | `Integration/EndToEndTests` 的 4 个用例（含 `EndToEnd_UDP失败_回退RelayUdp`）通过；`Ice/Ipv6CandidateTests` 的 `IceSession_IPv6回环_直连成功` 通过；`Integration/LocalNetTests` 的 `LocalNet_无信令_双向业务帧成功` 通过 | 通过 |
| 5 | 交付互通线下核对清单；本轮不实际执行联网互通，报告写明「未运行」 | 核对清单已交付，A1–A3、B1–B6、C1-UDP、C1-TCP、C1-TLS、C2 共 13 条统一标「未运行」，原因统一为「无公网环境，本轮只交付核对清单」 | 通过 |
| 6 | STUN/TURN 服务端按功能性服务端验收：错误码、配额上限、过期与鉴权路径正确；不含抗攻击、监控与压测结论 | `StunServer/StunServerTests`（含 420 未知必选属性、indication 不响应、指纹、CHANGE_REQUEST）；`TurnServer/TurnServerRelayTests`（401 挑战、486 超配额、508 端口耗尽、Refresh 生命周期 0 删除分配）；`TurnServer/TurnAllocationTests`（权限 300 秒、通道过期禁改绑、端口耗尽）；`TurnServer/Turn6062Tests` 与 `TurnServer/TurnTcpTests`、`TurnServer/TurnDualModeTests`；`Turn/TurnAuthTests`、`Turn/TurnTlsTests` | 通过（功能性范围；抗攻击、监控与压测不在本轮结论内） |
| 7 | 跳过白名单仅两项，白名单外的跳过计为失败；报告列出全部跳过项与原因 | 全量 2 个跳过，分别是 `so` 组合与 NAT66，均在白名单内；其余用例无跳过 | 通过 |

补充说明（第 1 条的口径差异）：`Ice/UdpMatrixTests` 的 `RelayPortMin = 56000` / `RelayPortMax = 56999`，`Integration/EndToEndTests` 的 `TurnRelayPortMin = 57000` / `TurnRelayPortMax = 57999`，`Ice/TcpMatrixTests` 通过 `ReserveRelayPorts` 向操作系统申请空闲端口。这些区间是为让 TURN 分配落在可预期的端口段，属于配置区间或预留端口，不是 `Port=0`。中继端口本身不依赖某个固定端口空闲：`TurnServer/RelayPortAllocator` 在配置区间内取随机起点线性探测（`TryAllocate` 中的 `_random.Next(count)`），`TurnServer` 的 `TryCreateRelaySocket`（UDP 中继）与 `TryCreateTcpRelayListener`（TCP 中继）以 `RelayBindAttempts = 16` 为上限，绑定失败时把该端口丢弃在池外并换下一个候选重试。因此实现只要求「区间内至少有一个端口可绑」。

## 三、测试分组与耗时

| 分组 | 过滤条件 | 通过 / 跳过 / 总计 | 耗时 |
|---|---|---|---|
| 构建（`Src/SAEA.Sockets.sln`，Debug） | — | 0 错误，退出码 0 | 17.49 s |
| 全量 | 无过滤 | 243 / 2 / 245 | 4 分 17 秒 |
| Unit | `TestCategory=Unit` | 190 / 0 / 190 | 1 分 42 秒 |
| Matrix | `TestCategory=Matrix` | 28 / 1 / 29 | 2 分 28 秒 |
| Ipv6 + LocalNet + Nat | `TestCategory=Ipv6\|TestCategory=LocalNet\|TestCategory=Nat` | 25 / 1 / 26 | 未记录 |
| 自测入口 `--smoke` | `dotnet run --project Src/SAEA.P2PTest -c Release -- --smoke` | 232 通过 / 0 失败 | 未记录 |

口径核对：190 + 29 + 26 = 245，与全量的总计一致；190 + 28 + 25 = 243，与全量的通过数一致。分组名来自 `Src/SAEA.P2P.Tests/P2PTestSuite.cs` 的 `Unit` / `Nat` / `Matrix` / `Ipv6` / `LocalNet` 常量。第三组的耗时本轮未记录，不做推算。

矩阵分组的 29 个用例构成：`Ice/UdpMatrixTests` 1 个 `DataTestMethod` + 16 个 `DataRow`，`Ice/TcpMatrixTests` 4 个，`Integration/EndToEndTests` 4 个，`Turn/TurnTransportMatrixTests` 4 个。

既有回归（这些测试工程不在 `.sln` 内，必须逐个指定）：

| 工程 | 结果 | 退出码 |
|---|---|---|
| `Src/SAEA.Common.Tests` | 59 通过 / 0 跳过 | 0 |
| `Src/SAEA.MQTT.Tests` | 153 通过 / 0 跳过 | 0 |
| `Src/SAEA.FTP.Tests` | 52 通过 / 0 跳过 | 0 |
| `Src/SAEA.Sockets.Pipeline.Tests` | 未运行：本工作区不存在该工程目录 | — |

## 四、跳过项

| 用例 | 文件与方法 | 跳过原因 | 是否白名单 |
|---|---|---|---|
| TCP 同时打开组合 | `Src/SAEA.P2P.Tests/Ice/TcpMatrixTests.cs` 的 `TcpMatrix_so组合_标记跳过并记录原因` | Windows TCP 栈不支持同时打开，本机不能同时作为主动与被动侧发起 `so` 连接，该格不参与判定 | 是（Windows 无 TCP 同时打开） |
| NAT66 | `Src/SAEA.P2P.Tests/Ice/Ipv6BoundaryTests.cs` 的 `Nat66_需管理员预置地址_默认跳过` | 需管理员预置 `fd00::/8` 地址并构造 v6 NAT 代理；非管理员环境只能绑定 `::1` | 是（NAT66 需管理员预置） |

不计入跳过但容易被误读的两项：

- `Ipv6BoundaryTests` 的 `Ipv6_非回环地址绑定_观测结果符合平台限制` 是**通过**的用例，不是跳过：它去绑定 `fd00::1`，实测失败为 `SocketError=AddressNotAvailable`，落在用例接受范围内即判定通过，并把观测结果写入测试输出。
- `Core/BuilderTests` 的 `P2PClientBuilder_Windows启用同时打开_校验失败并说明原因` 与 `Ice/TcpCandidateGathererTests` 的 `TcpCandidateGatherer_Windows_不收集so` 只在**非 Windows** 平台调用 `Assert.Inconclusive`，Windows 上走断言分支，因此 Unit 分组 0 跳过与此一致。

## 五、未运行与未覆盖面

1. **联网互通 13 条未运行**：A1、A2、A3、B1、B2、B3、B4、B5、B6、C1-UDP、C1-TCP、C1-TLS、C2。原因是无公网环境；本轮只交付命令模板、预期行为与判定口径。
2. **`so`（TCP 同时打开）路径未在本平台验证**：Windows 上该格显式跳过，相关代码路径保留供 Linux 使用；现有证据只覆盖「Windows 上识别为不支持」这一段：`BuilderTests` 验证构建期拦截并抛 `IceTcpSimultaneousOpenUnsupported`，`TcpCandidateGathererTests` 验证降级为 passive 候选。
3. **NAT66 未覆盖**：非管理员环境只有 `::1` 可用，IPv6 验收只覆盖 `::1` 主机直连、v6 中继、双栈监听与地址编码单测。
4. **NAT 代理模型的边界**：矩阵经进程内代理模型构造映射与过滤行为，只证明在给定 NAT 行为模型下的连通性与选路，不构成内核 NAT 或真实家用路由器行为的结论。
5. **公网 TCP 打洞成功率未验证**：设计以「检查失败即回退」为硬约束，本轮只验证回退路径（`TcpMatrix_TCP失败_回退TURN_UDP`、`EndToEnd_UDP失败_回退RelayUdp`），不给出成功率数字。
6. **`--server` 宿主进程级冒烟未执行**：`Src/SAEA.P2PTest` 只实测了 `--smoke`（232 通过 / 0 失败）与 `--help`（退出码 0，输出不含密钥）。与 NAT 组合的 STUN 验证是进程内集成用例 `Stun/StunNatIntegrationTests`（`StunNatIntegration_经NAT_映射地址为NAT外部端点`、`StunNatIntegration_CHANGE_REQUEST换端口_客户端收到响应`），不覆盖宿主进程的启动、配置文件加载与监听绑定路径。
7. **计划中的 `Src/SAEA.Sockets.Pipeline.Tests` 回归命令未执行**：该工程目录在本工作区不存在，`dotnet test` 无目标可跑；回归证据仅来自 Common / MQTT / FTP 三个工程。

## 六、已知限制逐条核对

对照对象为设计文档「十二、风险与已知限制」。

| 风险 / 限制 | 本轮状态与证据 |
|---|---|
| 进程内用户态 NAT 截获出站流量不可行，采用代理模型 | 已采纳代理模型（`Nat/NatBox`、`Nat/VirtualHost`、`Nat/MappedUdpTransport`）；结论只覆盖 NAT 行为模型，见第五节第 4 条 |
| Windows TCP 同时打开不可行 | `so` 默认关闭且显式跳过；`BuilderTests` 在构建期拦截并抛 `IceTcpSimultaneousOpenUnsupported` |
| 非管理员只有 `::1` | `Ipv6BoundaryTests` 把 `fd00::1` 绑定失败断言成明确结果；NAT66 用例跳过 |
| 未连接 UDP 不能接收 | 已由 `SAEA.Sockets` 新增的 `StartReceive` / `SendToAsync` / `LocalEndPoint` 能力支撑；`Integration/LocalNetTests` 与 `Transport/SaeaTransportTests`（13 个用例）覆盖 |
| 后端无 CRC-32、无 `RandomNumberGenerator.Fill` | 自实现 CRC-32、随机数走 `Create().GetBytes`；`Stun/Rfc5769VectorTests`、`Stun/StunFingerprintTests`、`Stun/StunIntegrityTests` 覆盖 |
| TCP 直连真实成功率受运营商与防火墙影响 | 不承诺成功率；回退路径有用例覆盖（见第五节第 5 条） |
| `so` 代码路径属「交付但未在本平台验证」 | 本报告第五节第 2 条已写明，不宣称已验证 |
| TURN 服务端端口范围、配额与并发 | 配额 486 与端口耗尽 508 有用例（`TurnServer/TurnServerRelayTests`）；`TurnServer/TurnAllocationTests` 覆盖 `RelayPortAllocator_端口耗尽_返回false`；并发压测不在范围 |
| 两阶段收集依赖 trickle 正确性 | Unit 分组通过（含 `Ice/IceSessionTests` 的 8 个会话与阶梯用例、`Ice/RouteLadderTests` 的 4 个阶梯用例）；公网条件下的 trickle 未验证 |
| 数据面不加密，业务数据裸传 | `Src/SAEA.P2P/README.md` 已如实告知（「数据面不做应用层加密」「TLS 只用于控制连接」），中英两份口径一致 |
| 无信令局域网模式无身份鉴权，HMAC 只保证完整性 | README 明确写「HMAC 只保证完整性，不提供机密性」，默认只在本链路可用；`Discovery/LocalDiscoveryTests` 覆盖两台实例互相发现、HMAC 错误不加入列表、发现后建立 Local 通道，`Integration/LocalNetTests` 覆盖过期节点淘汰 |
| 服务端与客户端同程序集，存在被反向引用风险 | 靠目录级引用约束与代码审查拦，无程序集级隔离；本轮未新增隔离机制 |
| `AllowLoopbackCandidates` 开启后可能「因错误原因通过」 | 矩阵已断言 selected pair 两端都落在虚拟主机地址集内（`Ice/UdpMatrixTests`、`Ice/TcpMatrixTests` 的 `IsScenarioAddress`） |

## 七、可粘贴命令

以下命令均在仓库根目录 `D:\WorkBench\Walle\SAEA` 下执行，`bash` 与 PowerShell 均可直接粘贴。

```bash
# 1. 构建（实测 0 错误，17.49 s，退出码 0）
dotnet build Src/SAEA.Sockets.sln -c Debug

# 2. P2P 全量（实测 243 通过 / 2 跳过 / 共 245，4 分 17 秒）
dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release

# 3. 分组跑（Unit 实测 190 通过、1 分 42 秒）
dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "TestCategory=Unit"

# 4. 矩阵分组（实测 28 通过 / 1 跳过 / 共 29，2 分 28 秒）
dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "TestCategory=Matrix"

# 5. IPv6 + 局域网 + NAT 分组（实测 25 通过 / 1 跳过 / 共 26）
dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "TestCategory=Ipv6|TestCategory=LocalNet|TestCategory=Nat"

# 6. 单用例（按方法名模糊匹配）
dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "FullyQualifiedName~TcpMatrix_so组合"
```

```bash
# 7. 既有回归（不在 .sln 内，必须逐个指定）
dotnet test Src/SAEA.Common.Tests/SAEA.Common.Tests.csproj -c Release                     # 实测 59 通过
dotnet test Src/SAEA.MQTT.Tests/SAEA.MQTT.Tests.csproj -c Release                         # 实测 153 通过
dotnet test Src/SAEA.FTP.Tests/SAEA.FTP.Tests.csproj -c Release                           # 实测 52 通过
# 本工作区无 Src/SAEA.Sockets.Pipeline.Tests 工程目录，以下命令无可执行目标
# dotnet test Src/SAEA.Sockets.Pipeline.Tests/SAEA.Sockets.Pipeline.Tests.csproj -c Release
```

```bash
# 8. 宿主自测入口（需 .NET 8 runtime；不带参数会进交互菜单，自动化必须显式传参）
dotnet run --project Src/SAEA.P2PTest -c Release -- --smoke   # 实测 232 通过 / 0 失败
dotnet run --project Src/SAEA.P2PTest -c Release -- --help    # 实测退出码 0，输出不含密钥
# 服务端宿主（本轮未运行宿主级冒烟，仅确认参数存在）
dotnet run --project Src/SAEA.P2PTest -c Release -- --server --config <path>
```

```bash
# 9. 旧中继命名检查（实测无输出，返回码 1 表示无匹配）
git grep -n -E "RelayManager|RelaySession|RelayData" -- Src
```

环境记录（实测）：SDK 版本 `dotnet --version` = `11.0.100`；已安装运行时 `Microsoft.NETCore.App 8.0.14`、`10.0.15`（另有 3.1.32、6.0.36、11.0.0）。`Src/SAEA.P2P.Tests` 目标框架 `net10.0`，`Src/SAEA.P2PTest` 目标框架 `net8.0`。

## 八、后续项

1. 对齐「8.3 验收标准」第 1 条的表述：写明监听端点用 `Port=0`，中继端口按配置区间分配（区间内随机起点探测、绑定失败换端口重试），避免「统一 `Port=0`」的字面歧义。
2. 在 Linux 环境补跑 `so` 组合，把「Windows 跳过」升级为跨平台证据。
3. 管理员环境补 NAT66：预置 `fd00::/8` 地址后启用 `Ipv6BoundaryTests` 的 NAT66 用例。
4. 按互通核对清单在具备公网的环境执行 13 条，并逐条回填实际结果；未执行前继续保持「未运行」。
5. 补 `--server` 宿主进程级冒烟：启动宿主、按配置加载、监听绑定、一条 Binding 往返，覆盖当前只有进程内集成用例的空档。
6. 根目录 `README.md` / `README.en.md` 的测试工程清单与实际不符：清单中的 `SAEA.Sockets.Pipeline.Tests` 在仓库里没有对应目录，且把 `SAEA.P2P.Tests` 标注为「需 .NET 8 runtime」，与其 `net10.0` 不符（`net8.0` 是 `Src/SAEA.P2PTest` 的目标框架）；`AGENTS.md` 的 SDK 版本（10.0.403）与 FTP 单测数量（13）也与本轮实测（11.0.100、52）不一致。
7. `Src/SAEA.Sockets.Pipeline.Tests` 工程在工作区缺失，需确认是未纳入当前分支还是被移除，再决定回归命令如何落地。