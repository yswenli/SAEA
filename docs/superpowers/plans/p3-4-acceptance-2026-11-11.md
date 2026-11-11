# P3-4 验收记录：三个 demo 与四组中英 README（2026-11-11）

**计划：** `docs/superpowers/plans/2026-11-09-socket-pipeline-migration-plan.md`（§五 P3-4）

**设计：** `docs/superpowers/specs/2026-11-06-socket-pipeline-migration-design.md` §5.3（连接处置）、§九（破坏面）；装配入口与冻结语义见 §六、§七

**范围：** `SAEA.RPC` 三个文件增加构造期装配回调（`Consumer/ConsumerMultiplexer.cs`、`Consumer/ServiceConsumer.cs`、`Generater/CodeGnerater.cs`）+ `Src/SAEA.RPCTest` 两个文件改链装配（`Program.cs`、`RPCServiceProxy.cs`）+ `SAEA.RPC` / `SAEA.QueueSocket` / `SAEA.MessageSocket` 三包中英 README + 根目录 `README.md` / `README.en.md` 迁移说明。`QueueSocketTest` / `MessageTest` 的链装配已在 P3-2 / P3-3 落地，本阶段只做自动跑与一致性核对。

---

## 一、构建与测试

| 命令 | 退出码 | 结果 |
|---|---|---|
| `dotnet build Src/SAEA.Sockets.sln -c Debug` | 0 | 0 errors / 0 warnings（增量；全量重建的 `CS1591` 存量警告量级与 B0 一致） |
| `dotnet test Src/SAEA.Common.Tests/… -c Release` | 0 | 59 / 59 |
| `dotnet test Src/SAEA.Sockets.Pipeline.Tests/… -c Release` | 0 | 260 / 260 |
| `dotnet test Src/SAEA.Http.Tests/… -c Release` | 0 | 28 / 28 |
| `dotnet test Src/SAEA.FileSocket.Tests/… -c Release` | 0 | 10 / 10 |
| `dotnet test Src/SAEA.WebSocket.Tests/… -c Release` | 0 | 10 / 10 |
| `dotnet test Src/SAEA.RPC.Tests/… -c Release` | 0 | 12 / 12 |
| `dotnet test Src/SAEA.MQTT.Tests/… -c Release` | 0 | 153 / 153 |
| `dotnet test Src/SAEA.FTP.Tests/… -c Release` | 0 | 13 / 13 |
| `dotnet test Src/SAEA.QueueSocket.Tests/… -c Release` | 0 | 14 / 14 |
| `dotnet test Src/SAEA.MessageSocket.Tests/… -c Release` | 0 | 15 / 15 |
| `dotnet run --project Src/SAEA.QueueSocketTest -c Release -- --functional` | 0 | **73 / 73 passed, 0 failed** |
| `dotnet run --project Src/SAEA.MessageTest -c Release -- --functional` | 0 | `SAEA.Message 功能自测全部通过。` |
| `dotnet run --project Src/SAEA.RPCTest -c Debug`（输入 `a`：Provider + Consumer 全链路） | 0 | Hello / Plus / Update / SendData / Dic / Generic / Enum / Group 八个服务调用全部返回，进程正常退出 |

十套 MSTest 合计 574 条，全绿；本轮没有新增或删除用例，也没有新增 `[Ignore]`。RPC 库改了构造函数签名，RPC 12 条用例与 demo 端到端都复跑确认无回归。

---

## 二、改动清单

### 2.1 RPC 库：装配回调改为构造期注入

| 文件 | 改动 |
|---|---|
| `Src/SAEA.RPC/Consumer/ConsumerMultiplexer.cs` | 私有构造与 `Create(Uri, links, timeOut, retry, configurePipeline)` 增加可选回调，在建链前写入 `_configurePipeline`；XML 注释改为「连接建立时读取并装配一次，链启动后即冻结」 |
| `Src/SAEA.RPC/Consumer/ServiceConsumer.cs` | 构造签名追加 `Action<string, IChannelPipeline> configurePipeline = null` 并透传给 `ConsumerMultiplexer.Create`；`ConfigurePipeline` 属性注释同步冻结语义 |
| `Src/SAEA.RPC/Generater/CodeGnerater.cs` | 模板输出带 `configurePipeline` 的构造函数与 `ServiceConsumer(...)` 调用；`ConfigurePipeline` / `IsConnected` 的 `get {` 空格统一；banner 修改标记整体覆盖为本次修改 |

三个文件的改动都是可选参数（默认 `null`），既有调用方签名兼容；未增删 `PackageReference`、未改 `<Version>`。

### 2.2 RPC demo：事件式改为链装配

- `Src/SAEA.RPCTest/RPCServiceProxy.cs`（567 行）：以生成器输出为基线重写，补回 XML 注释与 `GetEnumAsync` / `UpdateAsync` 两个方法；构造函数透传 `configurePipeline`。
- `Src/SAEA.RPCTest/Program.cs`（529 行）：新增 `AttachProviderObserver` / `AttachConsumerObserver` / `AttachNoticeObserver` 三个装配回调与对应的 `ProviderObserver` / `ConsumerObserver` / `NoticeObserver` 内部处理器。
  - `ProviderObserver`：`ChannelActiveAsync` / `ChannelInactiveAsync` / `ChannelExceptionCaughtAsync` 记录连接与异常，`ChannelReadAsync` 直接消费（请求已由内置分发器处理，避免链尾回收日志）。
  - `ConsumerObserver`：响应与推送统一在 `ChannelReadAsync` 观察，`rpcMsg.Type == (byte)RSocketMsgType.Notice` 判定推送。
  - `NoticeObserver`：推送测试专用的观察器，替代原 `rpcServiceProxy.OnNoticed`。
- `cp.OnErr`、`rpcServiceProxy.OnNoticed` 的引用全部清除；`ProviderInit` 保留 `ExceptionCollector.OnErr`（`ServiceProvider` 的传输异常仍走 `ExceptionCollector.Add("Provider", ex)`）。

### 2.3 四组中英 README

| 文件 | 改动 |
|---|---|
| `Src/SAEA.RPC/README.md` / `README.en.md` | Q3 异常改为 `ExceptionCollector.OnErr` + `ConfigurePipeline`（含构造参数注入说明与 `ExceptionLogHandler` 示例）；Q7 推送改为 `Notice<T>` 广播 + `RegistReceiveNotice()` / `OnNoticed`；完整服务端/客户端删 `provider.OnErr` / `proxy.OnErr`；服务端推送示例同步；「多服务地址负载均衡」按实际能力改写；核心类表 `ConsumerMultiplexer` 描述改为「同一地址多连接复用」 |
| `Src/SAEA.QueueSocket/README.md` / `README.en.md` | Step 3a/3b、订阅流程图、Q4 断线重连、完整生产者 / 完整消费者 / `QClient` 示例全部改为 `ConfigurePipeline` + `InboundHandlerAdapter`；新增 `QueueMessageHandler`（`Action<QueueMsg>` + `Dispose` 归还池化缓冲）；`Producer` 示例删不存在的 `Connect()` / `Close()` 与 `OnError` / `OnDisconnected` |
| `Src/SAEA.MessageSocket/README.md` / `README.en.md` | Q2 登录改为 `Login()`；Q7 构造参数改为命名参数并修正顺序；「基础服务器配置」与多人聊天室示例改为 `MessageServer(port:, bufferSize:, timeOut:)`；频道示例删除不存在的 `GetSubscribedChannels()`；客户端构造统一 `MessageClient(ip:, port:)` |
| `README.md` / `README.en.md` | 「实现与性能」新增「上层组件的处理器链」小节：说明六个组件不再暴露 `OnMessage` / `OnDisconnected` / `OnErr` 式事件，给出 `ConfigurePipeline` 装配示例与处理器职责 |

中英标题数逐份相同（RPC 34/34、QueueSocket 36/36、MessageSocket 39/39、根 37/37），``` 围栏成对（40/40、26/26、34/34、14/14）。

---

## 三、端到端功能验证

1. **RPC**：`SAEA.RPCTest` 输入 `a` 启动 Provider 与 Consumer，`HelloService.Hello/Plus/Update/SendData`、`DicService.Test`、`GenericService.Get/GetListString`、`EnumService.Get`、`GroupService.Add/Update/GetGroupInfo` 全部按预期返回；链尾回收日志不再出现（`ConsumerObserver` 已消费消息）。
2. **QueueSocket**：`--functional` 覆盖 Pub/Sub、池化所有权、批量、订阅重连等，`73/73 passed`、`EXIT=0`。
3. **MessageSocket**：`--functional` 覆盖登录、私信、频道订阅与广播、建群/入群/群消息四段，输出「功能自测全部通过」、`EXIT=0`。

三处 demo 的装配口径一致：`ConfigurePipeline` 在 `Start()` / `Connect()`（或构造）之前赋值，处理器只追加在链路尾部、异常继续 `ctx.FireExceptionCaughtAsync(ex)` 交给链尾。

---

## 四、P3-4 条目对照

| 验收项 | 证据 |
|---|---|
| `RPCTest` 改链装配 | `Program.cs` 三个 `Attach*Observer` + 三个内部处理器；无实例事件订阅 |
| `QueueSocketTest` 改链装配 | P3-2 已完成（`QueueMessageSink` / `DisconnectCounter`），本轮 `--functional` 73/73 核对 |
| `MessageTest` 改链装配 | P3-3 已完成（`ServerObserver` / `ClientObserver`），本轮 `--functional` 通过 |
| `QueueSocketTest --functional` 自动跑 | 退出码 0，`73/73 passed, 0 failed` |
| RPC 装配时机 | 回调只能构造期注入；`ServiceConsumer` / `ConsumerMultiplexer` / `RPCServiceProxy` 构造链透传，注释写明「链启动后冻结」 |
| 生成器与落盘代理一致 | 模板输出 `ConfigurePipeline` 透传属性、带 `configurePipeline` 的构造函数与 `ServiceConsumer` 调用；落盘 `RPCServiceProxy.cs` 同构且编译通过 |
| 三包中英 README | 三包 6 份全部改写完成，标题与代码围栏中英成对 |
| README 无失效示例 | 全量检索无 `OnMessage +=` / `OnDisconnected +=` / `OnPrivateMessage` / `proxy.Subscribe(` / `GetSubscribedChannels` / `new MessageSocket(`；`OnErr` 仅保留 `ExceptionCollector.OnErr` |
| 根 README 同步 | 新增双语「上层组件的处理器链」小节 |
| 依赖与版本 | 未增删 `PackageReference`、未改 `<Version>`、未新增项目引用 |
| 语法上限 | 本轮新增代码为 C# 8 语法（可选参数、lambda、`as`），无 C# 9+ 语法 |

---

## 五、与计划的偏差

1. **改了 RPC 库而非只改 demo**：计划 P3-4 只列 demo 与 README，但 `ChannelPipeline.AddLast` 在链启动后抛「通道链已启动并冻结」，而 `RPCServiceProxy` / `ServiceConsumer` 构造即建链，构造后赋值对已建连接无效（用户已确认口径）。因此把 `configurePipeline` 做成可选构造参数，旧签名继续可用。
2. **`RPCServiceProxy.cs` 以生成器输出为基线重写**（用户已确认）：保留生成模板的字段/属性/方法顺序，人工补回 XML 注释与 `GetEnumAsync` / `UpdateAsync`；555→567 行，注释密度与库内代理文件一致。
3. **根目录 README 增小节**：计划 §八写明「只在能力描述变化时同步」，迁移恰好改变了上层组件的能力描述，故补一节双语说明。
4. **顺带修正 README 既有错误**（属「示例与实现对齐」范围，非本次迁移引入）：
   - `ConsumerMultiplexer` 并不支持多地址负载均衡，改为「同一地址按 `links` 复用连接」，多地址示例改为使用方轮转；
   - `MessageSocket` 类型名不存在，改为 `MessageServer`；
   - `MessageClient.GetSubscribedChannels()` 不存在，删除该示例行；
   - `MessageServer(1024, 1000 * 1000, 30 * 60 * 1000)` 位置参数把端口写成 1024、把超时写进 `count`，改为命名参数；
   - `MessageClient(bufferSize, ip, port)` 的构造顺序与示例不一致，统一命名参数。

---

## 六、未运行项

| 项 | 原因 |
|---|---|
| `SAEA.RPCTest` 其余菜单入口（`p` / `c` / `g` / `t` / `s` / `n`） | 需人工输入；其中 `g` 会重写 `RPCServiceProxy.cs`（见遗留风险 1） |
| `SAEA.QueueSocketTest --all` / `--bench-queue` | 含长时压测，本轮只跑功能入口 |
| `SAEA.MessageTest` 交互菜单（`S/F/L/T/P`）与 6 万连接、百万私信压测 | 需人工输入与长时运行 |
| `FTPTest`、`MVCTest`、`WebSocketTest`、`P2PTest` 等窗口/交互 demo | 分属 P4 / P5 与最终验收 |
| 共享输出目录 | 未使用；各工程按默认 `bin/obj` 构建 |

---

## 七、遗留风险

1. **生成器会覆盖手工内容**：`SAEA.RPCTest` 菜单 `g` 按模板重写 `RPCServiceProxy.cs`，XML 注释与 `GetEnumAsync` / `UpdateAsync` 会丢失，需要按本次方式补回。
2. **装配回调的冻结语义**：`ServiceProvider` 在 `Start()` 前赋值有效；`RPCServiceProxy` / `ServiceConsumer` / `ConsumerMultiplexer` 必须构造期注入。`ConsumerMultiplexer` 复用其他实例已建连接时，那些连接保持原有装配。
3. **链尾回收日志**：使用者处理器若不消费消息，`ExceptionTerminalHandler` 会按既有规则打 Debug 日志；demo 里通过在 `ChannelReadAsync` 直接 `return Task.CompletedTask` 消费掉。
4. **多地址负载均衡仍无内置实现**：文档已按实际能力改写，真要跨地址均衡需使用方自己维护代理或加服务发现，属新需求。
5. **根 README 示例以 `MessageServer` 代表**：`WebConfig.ConfigurePipeline`（MVC）、`FTPServer`（P4 后）等入口写法略有差异，细节仍以各包 README 为准。

---

## 八、下一步

按计划 §六 / §七 推进：P4 先删 `SAEA.FTP` 死代码（`Net/ServerSocket.cs`、`Net/FTPStream.cs`、`Core/FTPDataSocketManager.cs`、`Net/ClientSocket.cs`、`FTPCoder.Decode` 空实现），再把控制通道与数据通道重写为 SAEA socket + Pipeline；P5 处理 `SAEA.P2P`（TCP 侧 `UseStream()`，UDP 侧保留 span 事件）。两者都沿用「先建适配层母本、再逐组件复制、每阶段全量构建 + 十套回归 + 验收文档 + 本地提交」的节奏。