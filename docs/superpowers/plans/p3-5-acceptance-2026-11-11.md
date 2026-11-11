# P3-5 验收记录：生成器模板补全与装配回调冻结（2026-11-11）

**计划：** `docs/superpowers/plans/2026-11-09-socket-pipeline-migration-plan.md`（§五 P3-5）

**设计：** `docs/superpowers/specs/2026-11-06-socket-pipeline-migration-design.md` §4.1（装配契约：`Start()` 之后链结构冻结）

**范围：** `SAEA.RPC` 生成器模板（XML 注释、`XxxAsync` 别名、跨次生成缓存清理）＋ 六个已迁移组件（Http / WebSocket / FileSocket / RPC / QueueSocket / MessageSocket）的 `ConfigurePipeline` 冻结语义 ＋ 12 份组件中英 README 与根目录 2 份 README 同步 ＋ 6 条冻结用例。

---

## 一、装配回调冻结矩阵

语义统一为：**回调必须在启动/首次连接之前赋值，之后赋值抛 `InvalidOperationException`**。服务端在 `Start()` 首行冻结、客户端在首次 `Connect()`/`ConnectAsync()` 首行冻结，均早于监听或建链，避免首个连接在冻结前装配。

| 入口 | 属性所在位置 | 冻结时机 | 说明 |
|---|---|---|---|
| `WebHost` | `WebConfig.ConfigurePipeline` | `WebHost.Start()` 首行（`internal FreezeConfigurePipeline()`） | `WebConfig` 为公开配置对象，冻结入口改为 `internal`，避免使用者绕过 |
| `WSServer` | 转发到 `Core/WSServerImpl` | `WSServerImpl.Start()` 首行 | 外层属性只做转发 |
| `WSClient` | `WSClient` | `Connect()` 首行 | 重连沿用首次回调 |
| `SAEA.FileSocket.Server` | `Server` | `Start()` 首行 | `FileTransfer` 在构造里先赋值后 `Start()`，不受影响 |
| `RServer` / `ServiceProvider` | `RServer` | `Start()` 首行 | `ServiceProvider.Start()` 先于 `RPCMapping.Regists` 调 `_rServer.Start()` |
| `RClient` | `RClient` | `Connect()` 首行 | `Reconnect()` 复用同一回调 |
| `ConsumerMultiplexer` / `ServiceConsumer` / `RPCServiceProxy` | `ConsumerMultiplexer` | 构造末（建链完成后） | 构造即建链，只能构造参数注入；`ServiceConsumer` / 生成代理只做转发 |
| `QServer` | `QServer` | `Start()` 首行 | — |
| `QClient` / `QueueSocket.Consumer` | `QClient` | `Connect()` / `ConnectAsync()` 首行 | `Consumer` 转发到 `QClient` |
| `Producer` | 转发到内部 `QClient` | 构造期（`Connect()` 内） | 新增 `(name, serverAddress, configurePipeline)` 重载，旧两参构造保留 |
| `MessageServer` | `MessageServer` | `Start()` 首行 | — |
| `MessageClient` | `MessageClient` | `Connect()` / `ConnectAsync()` 首行 | 重连沿用首次回调 |

实现要点：

- 每处均为「`_configurePipelineLock` + `_configurePipeline` + `_configurePipelineFrozen`」三件套，getter 加锁读、setter 加锁判冻结后写，`FreezeConfigurePipeline()` 统一为 `void`（Http 原本即 `void`，本轮把其余九处由「返回最终值」收敛为 `void`，消除无人使用的返回值）。
- 连接建立时仍通过属性 getter 每连接读取一次，冻结只拒绝写入，不缓存副本，重连行为与冻结前一致。
- 所有属性的 XML 注释统一改为「必须在 `X` 之前赋值，之后抛 `InvalidOperationException`」；构造即建链的入口写「只能通过构造参数注入」。中英 README 同步同一口径。

## 二、生成器模板

`Src/SAEA.RPC/Generater/CodeGnerater.cs`：

1. 新增 `AppendSummary` / `AppendParam` / `AppendReturns` / `AppendServiceMethod` 四个助手，给服务属性、构造、`Reconnect`、通知、`Dispose`、服务类、方法、实体、枚举补齐 XML 注释。
2. 每个服务方法追加同名 `XxxAsync` 别名，**实现仍是同步远程调用**，`RemoteCall` 的服务端方法名沿用原方法名（`AppendServiceMethod` 增加 `remoteMethodName` 参数），避免别名把 `TestAsync` 当服务端方法名发出去。
3. `Generate` 开头清空 `_serviceStrs` / `_modelStrs` / `_proxyStr`，同一进程内重复生成不再叠加。
4. banner 修改标记整体覆盖为本次修改。

验证：

- `SAEA.RPCTest` 菜单 `g` 连续生成两次，两次输出逐行比对**仅时间戳不同**（`2026-11-11 10:54:01` / `11:06:20`），模板稳定。
- 生成物（660 行）与 `Src/SAEA.RPCTest/RPCServiceProxy.cs` 一致；`RPCServiceProxy.cs` 已按生成物落盘并编译通过，相对上一版多出 XML 注释与共 11 个 `XxxAsync` 别名（`GetEnumAsync`、`UpdateAsync` 原本由手工补在落盘文件里，现在由模板生成）。
- 抽检别名转发正确，例如 `TestAsync` 生成 `_serviceConsumer.RemoteCall<Dictionary<Int32,UserInfo>>("DicService", "Test", id, data)`。

## 三、构建与测试

| 命令 | 退出码 | 结果 |
|---|---|---|
| `dotnet build Src/SAEA.Sockets.sln -c Debug` | 0 | 0 errors；警告为 `SAEA.Common` 内嵌 Newtonsoft.Json 的存量 `CS3021` 家族 |
| 六个库 `dotnet build … --no-incremental`（RPC / QueueSocket / MessageSocket / WebSocket / FileSocket / Http） | 0 | 各 92 条警告，全部来自 `Src/SAEA.Common/Newtonsoft.Json/**`，本轮文件 0 警告 |
| `dotnet test Src/SAEA.Common.Tests/… -c Release` | 0 | 59 / 59 |
| `dotnet test Src/SAEA.Sockets.Pipeline.Tests/… -c Release` | 0 | 260 / 260 |
| `dotnet test Src/SAEA.Http.Tests/… -c Release` | 0 | 29 / 29（新增 1 条冻结用例） |
| `dotnet test Src/SAEA.FileSocket.Tests/… -c Release` | 0 | 11 / 11（新增 1 条） |
| `dotnet test Src/SAEA.WebSocket.Tests/… -c Release` | 0 | 12 / 12（新增 2 条） |
| `dotnet test Src/SAEA.RPC.Tests/… -c Release` | 0 | 14 / 14（新增 2 条） |
| `dotnet test Src/SAEA.MQTT.Tests/… -c Release` | 0 | 153 / 153 |
| `dotnet test Src/SAEA.FTP.Tests/… -c Release` | 0 | 13 / 13 |
| `dotnet test Src/SAEA.QueueSocket.Tests/… -c Release` | 0 | 16 / 16（新增 2 条） |
| `dotnet test Src/SAEA.MessageSocket.Tests/… -c Release` | 0 | 17 / 17（新增 2 条） |
| **合计** | 0 | **584 / 584** |

新增用例（均 `被测对象_场景_期望` 命名，不依赖外部服务，端口用 `TcpListener(IPAddress.Loopback, 0)` 现取现放）：

| 文件 | 用例 |
|---|---|
| `Src/SAEA.Http.Tests/Tests/ConfigurePipelineFreezeTests.cs` | `WebHost_ConfigurePipelineAfterStart_Throws` |
| `Src/SAEA.FileSocket.Tests/Tests/ConfigurePipelineFreezeTests.cs` | `Server_ConfigurePipelineAfterStart_Throws` |
| `Src/SAEA.WebSocket.Tests/Tests/ConfigurePipelineFreezeTests.cs` | `WSServer_ConfigurePipelineAfterStart_Throws`、`WSClient_ConfigurePipelineAfterConnect_Throws` |
| `Src/SAEA.RPC.Tests/Tests/ConfigurePipelineFreezeTests.cs` | `ServiceProvider_ConfigurePipelineAfterStart_Throws`、`ServiceConsumer_ConfigurePipelineAfterConstruction_Throws` |
| `Src/SAEA.QueueSocket.Tests/Tests/ConfigurePipelineFreezeTests.cs` | `QServer_ConfigurePipelineAfterStart_Throws`、`QClient_ConfigurePipelineAfterConnect_Throws` |
| `Src/SAEA.MessageSocket.Tests/Tests/ConfigurePipelineFreezeTests.cs` | `MessageServer_ConfigurePipelineAfterStart_Throws`、`MessageClient_ConfigurePipelineAfterConnect_Throws` |

## 四、端到端功能验证

1. **QueueSocket**：`dotnet run --project Src/SAEA.QueueSocketTest -c Release -- --functional` → `73/73 passed, 0 failed`，`EXIT=0`。
2. **MessageSocket**：`dotnet run --project Src/SAEA.MessageTest -c Release -- --functional` → 「功能自测全部通过」，`EXIT=0`。
3. **RPC**：`SAEA.RPCTest` 输入 `a` 启动 Provider 与 Consumer，`HelloService.Hello/Plus/Update/SendData`、`DicService.Test`、`GenericService.Get/GetListString`、`EnumService.Get`、`GroupService.Add/Update/GetGroupInfo` 全部返回，`EXIT=0`。

## 五、文档同步

| 文件 | 改动 |
|---|---|
| `Src/SAEA.Http/README.md` / `README.en.md` | 装配顺序条目补「`ConfigurePipeline` 必须在 `WebHost.Start()` 之前赋值」 |
| `Src/SAEA.WebSocket/README.md` / `README.en.md` | 「链路顺序」小节补服务端 `Start()` / 客户端首次 `Connect()` 之前赋值、重连沿用首次回调 |
| `Src/SAEA.FileSocket/README.md` / `README.en.md` | Q4 拒绝文件示例补冻结前置条件 |
| `Src/SAEA.RPC/README.md` / `README.en.md` | 服务端 `Start()` 前赋值；消费者构造期建链只能构造注入，之后赋值抛异常 |
| `Src/SAEA.QueueSocket/README.md` / `README.en.md` | 快速上手补 `Start()`/`Connect()` 前赋值；`Producer` 说明构造期建链只能构造注入 |
| `Src/SAEA.MessageSocket/README.md` / `README.en.md` | 快速上手补服务端 `Start()`、客户端 `Connect()`/`ConnectAsync()` 前赋值 |
| `README.md` / `README.en.md` | 「上层组件的处理器链」小节补冻结语义与构造注入说明 |

中英内容逐条对应，未新增失效 API 示例。

## 六、本轮深度审查结论

1. **冻结位置**：全部在监听/建链动作之前，且先冻结再启动，消除「首个连接在冻结前建链」的窗口。
2. **锁的使用**：getter/setter/冻结三处共用同一锁，无嵌套加锁、无锁内回调，`FreezeConfigurePipeline` 不返回引用，避免外部拿到回调后绕过冻结写回。
3. **转发链**：`ServiceProvider` → `RServer`、`ServiceConsumer` → `ConsumerMultiplexer`、`Consumer` → `QClient`、`WSServer` → `WSServerImpl`、`Producer` → `QClient` 均为单向透传，冻结判定留在最内层，不会出现「外层可写、内层已冻结」的错位。
4. **构造即建链**：`ConsumerMultiplexer` 在构造末冻结并在循环内先写回调再 `Connect()`；`Producer` 新增三参重载、在 `Connect()` 前注入；旧签名保持可用。
5. **注释一致性**：所有被改属性与私有冻结方法的注释与实际异常文案一致；`void` 化后不再出现无用的 `<returns>`。
6. **banner**：本轮涉及的 17 个库文件修改标记整体覆盖为 `2026/11/11 11:20:00` / 「装配回调改为启动前注入…」；`CodeGnerater.cs` 保留本次生成器改造的修改标记。

## 七、未运行项

| 项 | 原因 |
|---|---|
| `SAEA.QueueSocketTest --all` / `--bench-queue` | 含长时压测，本轮只跑功能入口 |
| `SAEA.MessageTest` 交互菜单与 6 万连接 / 百万私信压测 | 需人工输入与长时运行 |
| `SAEA.P2PTest` / `FTPTest` / `MVCTest` / 窗口类 demo | 分属 P4 / P5 与最终验收 |
| 全量 clean rebuild | 本轮用增量构建 + 六个库 `--no-incremental` 覆盖改动面，未做全解决方案 clean rebuild |

## 八、遗留风险

1. **冻结按对象而非按链**：`FreezeConfigurePipeline` 只拒绝后续写入，不校验「赋值是否真的在启动前完成」；通过反射或字段直写仍可绕过，属预期边界。
2. **复用连接的装配**：`ConsumerMultiplexer` 命中同一地址已有连接时，那些连接保持原有装配，新传入的回调不会补装；已在注释与文档写明。
3. **`Async` 别名不是真异步**：生成为同步远程调用别名，仅方便 `async` 调用方；如需真异步需另立需求。
4. **`RPCServiceProxy.cs` 可被生成器覆盖**：菜单 `g` 会按模板重写该文件，但当前模板已能生成同等注释与别名，覆盖后不再丢内容。

## 九、下一步

按计划 §六推进 P4（`SAEA.FTP`）：先删死代码（`Net/ServerSocket.cs`、`Net/FTPStream.cs`、`Core/FTPDataSocketManager.cs`、`Net/ClientSocket.cs`、`FTPCoder.Decode` 空实现），再给出控制通道与数据通道的重写方案等确认后动手；P5（`SAEA.P2P`）随后。