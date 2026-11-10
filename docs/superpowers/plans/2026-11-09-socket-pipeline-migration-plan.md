# 上层包接入 ChannelPipeline 实施计划

**日期：** 2026-11-09

**上游设计：** `docs/superpowers/specs/2026-11-06-socket-pipeline-migration-design.md`（下称「设计」）、`docs/superpowers/specs/2026-11-06-dotnetty-pipeline-comparison.md`

**状态：** 设计已定稿并经人工批准，进入实施。P0 先落地基础设施与 `SAEA.Sockets` 发送语义，P1–P5 逐包迁移。

**执行方式：** 按阶段推进，每阶段独立构建、独立跑测试、独立交付报告。**不执行 `git push` / `reset --hard` / `clean`，不自动提交**；提交点由用户决定。

---

## 零、全局工作规则

1. **范围**：只改本计划列出的文件。`Src/SAEA.Common/ConsoleHelper.cs`、`Src/SAEA.Common/README*.md`、`Src/SAEA.Sockets/Ssl/**`、`Src/SAEA.Sockets.SslTlsTest/**`、证书工厂设计文档属于他人的并行改动，**一个字节都不要动**。
2. **版本**：任何 `.csproj` 的 `<Version>` 不改，`PackageReference` 不增删不升降。当前 `SAEA.Sockets` / `SAEA.Sockets.Pipeline` 均为 `26.11.5.1`。
3. **语法**：库项目 `netstandard2.0` + `LangVersion 8.0`。禁用 record、init、file-scoped namespace、目标类型 `new`、全局 using、`switch` 表达式。测试工程 `net10.0`（`LangVersion 10.0`）可放宽。
4. **文件头 banner**：新增 `.cs` 必须有统一 banner（模板见 `AGENTS.md`），`CLR版本` 写目标框架，`版本号` 写 `v26.11.5.1`，`唯一标识` 逐文件新 GUID。修改已有文件时，把 `修改标记` 段整体覆盖为本次修改信息。
5. **注释**：`public` / `protected` 成员按 `Src/SAEA.Common/IPHelper.cs` 体例写 `summary` / `param` / `returns`；行内注释只讲「为什么」和生命周期/线程/池化约束，不写溯源标记。
6. **池化**：`MemoryPoolManager.Rent` 必须 `try/finally` 归还；`PooledMessage.Release` 只在确定归零时调用一次（幂等只覆盖归零后的重复调用）。
7. **测试**：新增用例放 `Src/SAEA.Sockets.Pipeline.Tests/Tests/<分类>/`，方法名 `被测对象_场景_期望`，一个用例一个行为，不依赖外部服务（需要 socket 的用回环 + 随机端口）。
8. **证据**：未实际运行的构建/测试必须写「未运行」，不得写通过。每条命令记录退出码与关键输出。

### 依赖顺序

```
B0 基线
 └─ P0-1 发送 API  ──┐
 └─ P0-2 接口扩展    │
     └─ P0-3 生命周期/状态门/启动校验
         └─ P0-4 inbound dispatch 与所有权
             └─ P0-5 ByteToMessageDecoder
         └─ P0-6 SwitchableSplitter + 切换屏障
             └─ P0-7 TakeAvailableSplitter
         └─ P0-8 RawFrame
             └─ P0-9 业务写入口 + 收口状态机
                 └─ P0-10 OutboundSerializingHandler
                     └─ P0-11 分帧异常上链
                         └─ P0-12 适配层母本 + PipelineTest
                             └─ P0-13 README
                                 └─ P0-14 P0 验收
```

P0-1 与 P0-2～P0-11 可以并行（不同工程、不同文件），但**不允许两个执行者同时跑 `dotnet build` 到同一 `obj/`**；串行执行时按编号顺序。

### 子代理切分

| 轮次 | 任务 | 说明 |
|---|---|---|
| R1 | P0-1 | `SAEA.Sockets` 发送语义，独立工程，先做，避免后续适配层建立在假 Task 上 |
| R2 | P0-2 → P0-3 → P0-4 → P0-5 | 接口、生命周期、入站所有权、解码器基类，必须按序 |
| R3 | P0-6 → P0-7 → P0-8 → P0-9 → P0-10 → P0-11 | 分帧切换、无边界分帧、原始帧、写入口与串行器、泵异常路径 |
| R4 | P0-12 → P0-13 → P0-14 | 适配层母本、演示工程、基础设施 README、整体验收 |

每轮结束时必须：`dotnet build Src/SAEA.Sockets.sln -c Debug` 0 errors；`dotnet test Src/SAEA.Sockets.Pipeline.Tests/SAEA.Sockets.Pipeline.Tests.csproj -c Release` 全绿；报告新增用例数与本轮改动文件清单。

---

## 一、B0：基线采集（不改代码）

必须先记录，否则 P0 验收里的「不劣化」没有参照。

```powershell
dotnet build Src/SAEA.Sockets.sln -c Debug          # 记录 errors / warnings 数
dotnet test Src/SAEA.Common.Tests/SAEA.Common.Tests.csproj -c Release
dotnet test Src/SAEA.MQTT.Tests/SAEA.MQTT.Tests.csproj -c Release
dotnet test Src/SAEA.FTP.Tests/SAEA.FTP.Tests.csproj -c Release
dotnet test Src/SAEA.Sockets.Pipeline.Tests/SAEA.Sockets.Pipeline.Tests.csproj -c Release
dotnet run --project Src/SAEA.Sockets.PipelineTest -c Release -- --all
dotnet run --project Src/SAEA.Sockets.PipelineTest -c Release -- --bench
```

**产出**：`docs/superpowers/plans/baseline-2026-11-09.md`，含每条命令的退出码、通过数、`--bench` 的吞吐与每帧分配数值。四项 MSTest 工程的预期基线为 Common 59 / MQTT 153 / FTP 13 / Pipeline 以实测为准（最近一次 Debug 168、Release 166，仅作对照，不作承诺）。

> 若当前工作区有未提交改动（WebSocket TLS 合并），基线记录的提交号写 `3ab24f4c` + `git status --short` 输出，便于回溯。

---

## 二、P0：基础设施

### P0-1 `SAEA.Sockets` 发送 API 改为可等待 `Task`

**目标**：把两个带 `IDisposable owner` 的发送重载从「投递即返回、失败静默」改成「可等待真实交接、失败可观察、owner 恰好释放一次」，并补 TLS 全记录等待。

**文件**

| 文件 | 改动 |
|---|---|
| `Src/SAEA.Sockets/IServerSocket.cs`（`SendAsync(string, ReadOnlyMemory<byte>, IDisposable)`，约 :121） | 返回类型 `void` → `Task`，补 XML 注释写明完成点与 owner 语义 |
| `Src/SAEA.Sockets/IClientSocket.cs`（约 :128） | 同上 |
| `Src/SAEA.Sockets/Core/Tcp/SaeaServerSocket.cs`（`SendAsync` :860、`SendAsync(IUserToken,…)` :871、`SendAsyncRaw` :723、`AbandonSendingOwner`） | 每次发送独立完成源；超时先关 socket 等 `Completed`；owner 单点释放 |
| `Src/SAEA.Sockets/Core/Tcp/SaeaClientSocket.cs`（`SendAsyncRaw` :882、`SendAsync` :1000） | 同上，客户端独立完成源；未连接/`_userToken` 为空改为同步异常 |
| `Src/SAEA.Sockets/Core/Udp/SaeaUdpServerSocket.cs`（:450）、`Src/SAEA.Sockets/Core/Udp/SaeaUdpClientSocket.cs`（:444） | 仅签名同步为 `Task`；UDP 接收与分帧不变 |
| `Src/SAEA.Sockets/Core/SaeaConnection.cs`（`SendApplicationData`、`FlushOutput`、`_drainingOutput`、`_rawSend`） | `_rawSend` 由 `Action` 改为可等待委托；一次应用数据的所有 TLS 记录完成后才完成返回的 Task |
| `Src/SAEA.Sockets/Core/*`（`IUserToken` / 发送状态） | `SendingOwner` / `IsSending` 扩为「操作对象」，完成回调、异常、同步完成、超时共同竞争同一个 `TryComplete` |

**步骤**

1. 先通读 `SaeaServerSocket.SendAsyncRaw`、`SaeaClientSocket.SendAsyncRaw`、`SaeaConnection.FlushOutput` / `_drainingOutput` / `_rawSend`、`AbandonSendingOwner` 的现有实现，写一段现状说明（谁在何时释放 owner、哪些路径静默失败），贴进报告。
2. 引入每发送操作对象（建议名 `SendOperation`，`internal sealed`），字段：`TaskCompletionSource<bool>`（`RunContinuationsAsynchronously`）、`IDisposable Owner`、`int Completed`、`bool TimedOut`。规则：
   - 成功：底层 `Completed` 回调里 `Release owner` 一次 → `TrySetResult` 一次；
   - 同步异常（会话不存在、未连接、TLS 未就绪、`WaitWrite` 失败）：`Release owner` 一次 → 同步抛或返回 faulted Task；
   - 异步失败/断开：`Release owner` 一次 → `TrySetException`；
   - 超时：先 `socket.Close/Dispose` 取消在途操作，再等 `Completed`；确认到完成 → 唯一完成路径释放并失败；**无法证明完成回调已到 → 不释放 owner**，这是唯一允许「不恰好一次」的路径，必须写进注释。
3. `SaeaServerSocket` 的超时分支不得再「只触发 `OnError` 后返回」；`SocketError` / 断开路径必须完成对应操作。
4. TLS：`SaeaConnection.SendApplicationData` 必须等本次应用数据拆出的**全部** TLS 记录完成 `socket` 层交接后才完成返回的 Task；不得在 `Task.Run(WriteAsync)` 入队后立即返回。保留单连接单发送者与记录 FIFO。
5. UDP 两个实现同步签名即可；不得改变 UDP 的接收/分帧行为。
6. 更新所有调用方编译错误：仓库内 `SendAsync(..., owner)` 调用点、`SocketStreamAdapter` 母本、`RedisSocket` 等未迁移包按「普通调用可继续编译」处理（不 `await` 仍可编译，但实现方签名必须改）。

**测试**（`Src/SAEA.Sockets.Pipeline.Tests/Tests/Integration/SendOwnershipTests.cs`）

- `ServerSendAsync_ConnectedClient_CompletesAfterSocketSend`：回环两端，发送后 `await`，对端收到。
- `ServerSendAsync_SessionMissing_ThrowsAndReleasesOwnerOnce`：owner 用计数 `IDisposable`，断言释放 1 次且 Task faulted/同步抛。
- `ServerSendAsync_ClientDisconnected_CompletesWithFailure`：断开后发送，Task 必须失败，不得静默成功。
- `ClientSendAsync_BeforeConnect_Fails`：未连接即发送。
- `SendAsync_OwnerCount`：成功路径 owner 恰好 1 次；超时路径覆盖「迟完成不得释放新一次发送的 owner」。
- `TlsSend_LastRecordHandedOff`：TLS 回环，发送一个大于单记录长度的应用帧，`await` 返回时底层已全部交接（用计数器或发送完成事件断言）。

> 端口一律 `new TcpListener(IPAddress.Loopback, 0)` 后取实际端口；TLS 证书用仓内既有测试证书工具，不得新增依赖。

**验收**

```powershell
dotnet build Src/SAEA.Sockets.sln -c Debug                 # 0 errors
dotnet test Src/SAEA.Sockets.Pipeline.Tests/SAEA.Sockets.Pipeline.Tests.csproj -c Release
dotnet test Src/SAEA.FTP.Tests/SAEA.FTP.Tests.csproj -c Release
dotnet test Src/SAEA.MQTT.Tests/SAEA.MQTT.Tests.csproj -c Release
```

**完成判据**：四个实现签名一致；所有失败路径可观察；owner 释放次数用例全绿；无 `Task.Run(...)` 后立即返回的 TLS 发送。

---

### P0-2 接口扩展（4 个接口 6 个成员）

**文件**

| 文件 | 改动 |
|---|---|
| `Src/SAEA.Sockets.Pipeline/Abstractions/IChannelHandler.cs` | 增加 `void HandlerAdded(IChannelHandlerContext)`、`void HandlerRemoved(IChannelHandlerContext)` |
| `Src/SAEA.Sockets.Pipeline/Abstractions/IChannelInboundHandler`（在 `IChannelHandler.cs` 内） | 增加 `Task ChannelActiveAsync(IChannelHandlerContext)`、`Task ChannelInactiveAsync(IChannelHandlerContext)` |
| `Src/SAEA.Sockets.Pipeline/Abstractions/IChannelHandlerContext.cs` | 增加 `Task CloseAfterWriteAsync(string reason)` |
| `Src/SAEA.Sockets.Pipeline/Abstractions/IChannelPipeline.cs` | 增加 `Task WriteAsync(object msg)` |
| `Src/SAEA.Sockets.Pipeline/Abstractions/ChannelHandlerAdapter.cs`、`InboundHandlerAdapter.cs`、`OutboundHandlerAdapter.cs` | 四个生命周期成员给空实现 / `Task.CompletedTask` |
| `Src/SAEA.Sockets.Pipeline/Runtime/DefaultChannelHandlerContext.cs` | 先给编译通过的最小实现（`CloseAfterWriteAsync` 转调 pipeline 的关闭协调；`WriteAsync` 转调节点 Prev），P0-9 再补状态机 |
| `Src/SAEA.Sockets.Pipeline/Runtime/ChannelPipeline.cs` | 增加 `WriteAsync` 骨架（P0-9 完成），先直通链尾出站入口 |

**步骤**

1. 接口成员 XML 注释按设计 §5.2 / §5.8 原文口径写：完成点、owner、禁止事项、误用后果。
2. 编译驱动：先改接口，再逐个修实现与调用点。仓库内所有 handler 都继承适配器，理论上只改适配器即可。
3. 不改任何既有成员签名。

**测试**：`Tests/Runtime/PipelineStructureTests.cs` 补一条 `ChannelHandlerAdapter_LifecycleDefaults_DoNotThrow`；属编译级保护。

**完成判据**：`dotnet build Src/SAEA.Sockets.sln -c Debug` 0 errors；`PipelineTest --all` 仍过。

---

### P0-3 生命周期、执行器状态门、启动校验、链冻结、属性清理时序

**文件**

| 文件 | 改动 |
|---|---|
| `ChannelPipelineExecutor.cs` | 新增 `Created / Attached / Started / Closing / Completed` 状态；`Attach` / `Start` 在 Closing 后抛；`Start()` 成功后、首读前派发 `ChannelActiveAsync`；收口时、`DrainPolicy` 前派发 `ChannelInactiveAsync`（幂等，异常只记录） |
| `ChannelPipeline.cs` | `AddFirst/AddLast/Remove` 在 `Start()` 后用 `_frozen` 抛 `InvalidOperationException`；`HandlerAdded` 先建节点+ctx 再挂链、失败不挂链；`HandlerRemoved` 先 Unlink 再回调、异常只记录 |
| `ChannelPipelineOptions.cs` | 新增 `bool RequireTerminalHandler`（默认 false）、`TimeSpan SplitterSwitchTimeout`（默认 10s）、`int MaxPendingWrites`（默认 256） |
| `ChannelPipelineExecutor.Start()` | `RequireTerminalHandler == true` 时校验「链尾最后一个入站节点是 `ExceptionTerminalHandler`」+「`OutboundSerializingHandler` 恰好一个且在出站前缀最右侧」，不满足抛 `InvalidOperationException` 并直接收口 |
| `DefaultChannel.cs` | `MarkInactive` 只原子翻转 `IsActive`；新增 `ClearAttributes()` 由执行器在 `ChannelInactiveAsync` 与 host 摘除后调用，且只清一次 |
| `DefaultChannelHandlerContext.cs` | `Channel` / `ChannelId` 读半初始化节点时按设计 §5.2 行为（`HandlerAdded` 阶段不可读 `Channel`） |

**步骤**

1. 状态门先落地：`Attach`、`Start`、`BeginShutdown` 三者用同一把锁或 `Interlocked` 状态机，保证 Closing 后 `Attach`/`Start` 必抛。
2. `Start()` 顺序：状态 → `RequireTerminalHandler` 校验 → 建消费循环 → `ChannelActiveAsync`（链首→链尾逐条 await）→ 冻链 → 启动泵。**冻链必须早于泵首读**。
3. 收口顺序：置 Closing → `ChannelInactiveAsync` → `DrainPolicy` → 收帧 → host `TryRemoveChannel` → `ClearAttributes` → `Completed`。
4. `ChannelActiveAsync` 抛异常只记录，不阻止后续 handler 与泵启动；`ChannelInactiveAsync` 抛异常只记录，不阻断收口。

**测试**（`Tests/Runtime/` 新增或扩展现有）

- `Executor_AttachAfterClosing_Throws`、`Executor_StartAfterClosing_Throws`
- `Executor_StartBeforeAttach_Throws`
- `Pipeline_AddAfterStart_Throws`（AddFirst / AddLast / Remove 各一条）
- `Executor_RequireTerminal_MissingTerminal_StartFails`
- `Executor_RequireTerminal_TwoSerializers_StartFails`、`..._SerializerNotRightmost_StartFails`
- `Executor_ChannelInactive_CanReadChannelAttributes`、`Executor_AttributesClearedOnceAfterInactive`
- `Executor_ActiveNotDispatchedAfterInactive`（竞态：构造即取消 transportToken，再 `Start`）
- `Pipeline_HandlerAddedThrows_NodeNotLinked`、`Pipeline_HandlerRemovedThrows_NodeStillUnlinked`

**验收**：Pipeline.Tests 全绿；`PipelineTest --all` 通过。

---

### P0-4 入站分派所有权与异常语义

**目标**：实现设计 §5.1 的 `InboundDispatch`：逐次调用所有权标记、普通异常「通知一次后冒泡」、未转发补偿、原始帧与 decoder 输出分别记账。

**文件**

| 文件 | 改动 |
|---|---|
| `ChannelPipeline.cs`（`FireChannelReadFromAsync` / `FireChannelReadAsync` 路径） | 每次派发携带 `InboundDispatch`；通知标记；按边界补偿 |
| `ChannelPipelineExecutor.cs`（`ExecuteMessageAsync` / `FinishInline`） | 原始帧标记「执行器持有」，`finally` 仍只释放原始引用 |
| `DefaultChannelHandlerContext.cs` | 每次调用创建调用绑定视图，携带 `InboundDispatch` 与当前操作；跨回调保存视图不可用 |
| 新增 `Runtime/InboundDispatch.cs` | `internal sealed`：`bool ChainOwned`、`bool ExceptionNotified`、`bool Forwarded` |

**步骤**

1. 明确三种所有权来源：执行器原始帧（executor-owned）、decoder 每个 output（chain-owned）、handler 替换的新对象（chain-owned）。
2. 未成功转发即抛/取消：仅 chain-owned + `IDisposable` 释放一次；已转发由下游边界负责。
3. 正常返回未转发：视为消费，不擅自释放（除链尾无下一节点时记录 Debug 并释放 chain-owned）。
4. 异常：从当前节点下游 `FireExceptionCaughtAsync` 通知一次 → 冒泡；上游看到已通知不再通知；执行器只兜底记录。`OperationCanceledException` 不通知，但仍补偿并冒泡。
5. 删除旧的「通知后吞掉」行为。

**测试**（`Tests/Runtime/InboundOwnershipTests.cs`）

- `Inbound_HandlerThrows_NotifiesOnceAndBubbles`
- `Inbound_HandlerThrowsBeforeForward_ReleasesChainOwnedMessageOnce`
- `Inbound_HandlerThrowsAfterForward_DoesNotDoubleRelease`
- `Inbound_Canceled_CompensatesOwnershipAndBubbles`
- `Inbound_NoNextInboundNode_ReleasesChainOwnedAndLogs`
- `Inbound_ExecutorOwnedOriginalFrame_ReleasedByExecutorOnly`
- 用计数 `IDisposable` 断言精确次数（0/1/2 都要有）。

**验收**：Pipeline.Tests 全绿，且原有「异常通知」用例按新语义更新（不得保留吞异常断言）。

---

### P0-5 `ByteToMessageDecoder`

**文件**：新增 `Src/SAEA.Sockets.Pipeline/Handlers/ByteToMessageDecoder.cs`（`public abstract class`，继承 `InboundHandlerAdapter`）。

**步骤**

1. 签名严格按设计 §5.1：`protected abstract void Decode(IChannelHandlerContext ctx, PooledMessage frame, List<object> output);`
2. 基类**不释放**输入帧；`output` 逐条按序 `await ctx.FireChannelReadAsync`。
3. 某条调用开始即所有权移交；同步抛/异步失败/取消 → 停止派发，逐个 `Release`/`Dispose` 尚未开始的余项，随后只向上传播一次异常。
4. `Decode` 自身抛出：先 `ctx.FireExceptionCaughtAsync` 通知一次（通知失败只记录），再照常派发已追加消息，最后冒泡原始异常。
5. XML 注释写明「不累加半包」「多输出共享一帧需每次 `Retain`」「下游跨回调持有需自行 `Retain`」。

**测试**（`Tests/Handlers/ByteToMessageDecoderTests.cs`）

- `Decode_ZeroOutput_CompletesWithNoDispatch`
- `Decode_MultipleOutputs_DispatchedInOrder`
- `Decode_SharesFrameAcrossTwoOutputs_RefCountReachesZero`
- `Decode_FirstOutputThrowsBeforeForward_BothRetainsReleasedAndInputReleasedOnce`
- `Decode_SecondOutputFails_RemainingOutputsReleasedOnce`
- `Decode_Canceled_RemainingReleasedOnceAndBubbles`
- `Decode_Throws_ExceptionNotifiedOnceThenBubbles`
- `Decode_DoesNotReleaseInputFrame`（执行器侧计数）

**验收**：Pipeline.Tests 全绿；`PipelineTest --all` 通过。

---

### P0-6 `SwitchableSplitter` 与泵切换屏障

**文件**

| 文件 | 改动 |
|---|---|
| 新增 `Src/SAEA.Sockets.Pipeline/Splitters/SwitchableSplitter.cs` | `Current`、`StopAfterCurrent`、`SwitchTo(IFrameSplitter)`、固定 `MaxFrameLength = max(all)`、拒绝超限 |
| `ChannelPipelineExecutor.cs`（`PumpAsync` 第 4 步） | 排队分支：切完当前帧立即停切、同一 `ReadResult` + `consumed` 上等 `SwitchTo`（TCS + 连接取消 + `SplitterSwitchTimeout`）、完成后续切同一缓冲、最后一次 `AdvanceTo`；**内联分支**每次返回后复查 `StopAfterCurrent` |
| `ChannelPipelineExecutor.cs` | 超时/连接关闭按 `FrameProtocolException` 收口；等待期间禁止 `AdvanceTo(consumed, buffer.End)` |

**步骤**

1. 每连接新建 `SwitchableSplitter`；`options.Splitter` 直接放该实例，不包转发器（泵取 `_options.Splitter` 局部变量）。
2. `SwitchTo` 只允许在屏障开启时调用；`SwitchTo(Current)` 等价复位；切换后 `Current` 替换、`StopAfterCurrent=false`、唤醒等待者。
3. `MaxFrameLength` 在构造时定死为所有可切换协议最大值；`SwitchTo` 超限抛 `InvalidOperationException`（不重算 `MaxBufferedBytes`）。
4. 内联与排队两条路径都要覆盖：内联路径若 `StopAfterCurrent` 为真，停止本次切帧并用同一等待逻辑。
5. 屏障等待的可取消性：连接取消 → 收口；超时 → 协议错误。

**测试**（`Tests/Runtime/SplitterSwitchTests.cs`）

- `SwitchableSplitter_MaxFrameLength_IsMaxOfAll`
- `SwitchableSplitter_SwitchToOversized_Throws`
- `SwitchableSplitter_SwitchToCurrent_ResetsBarrier`
- `Pump_StopAfterCurrent_Queued_ResumesSameReadResult`（升级帧与 WS 首帧同一 TCP 段）
- `Pump_StopAfterCurrent_Inline_StopsAfterCurrentFrame`
- `Pump_NoSwitch_BarrierTimesOutAndShutsDown`（`SplitterSwitchTimeout = 100ms`）
- `Pump_SwitchTo_WaitsOnSameReadResultWithoutNewBytes`
- `Pump_ConnectionCanceledDuringBarrier_ShutsDown`

**验收**：Pipeline.Tests 全绿；`PipelineTest --all` 与 `--bench` 不劣化 >5%。

---

### P0-7 `TakeAvailableSplitter`

**文件**：新增 `Src/SAEA.Sockets.Pipeline/Splitters/TakeAvailableSplitter.cs`。

**步骤**：空缓冲返回 false；否则整段作为一帧、`consumed = buffer.Length`；`MaxFrameLength` = 单次读取上限，超限抛 `FrameProtocolException`；`Create` 校验不豁免（文档写明）。

**测试**（`Tests/Splitters/OtherSplittersTests.cs` 追加）

- `TakeAvailable_EmptyBuffer_ReturnsFalse`
- `TakeAvailable_MultiSegment_ReturnsWholeSequence`
- `TakeAvailable_ExceedsMaxFrameLength_Throws`
- `TakeAvailable_ConsumesWholeBuffer`

---

### P0-8 `RawFrame` 与出站所有权

**文件**

| 文件 | 改动 |
|---|---|
| 新增 `Src/SAEA.Sockets.Pipeline/Codecs/RawFrame.cs` | `public sealed class RawFrame : IDisposable`，`Bytes`、`Owner`、`int` 移交标志、`Dispose` 仅在未 CAS 时释放 `Owner` |
| `ChannelPipeline` 链头 sink | 接受 `TransportFrame` 或 `RawFrame`；CAS 成功前所有退出路径（关闭/未 Attach/类型错误/同步异常）先 `Dispose` 再失败；CAS 成功后只由传输层释放 |
| `DefaultChannelHandlerContext.WriteAsync` 出站边界 | 记录本次是否移交；正常返回未移交 → 释放 `RawFrame`；抛错/失败/取消且未移交 → 释放；已移交不重复释放；同节点同操作重复转发抛 `InvalidOperationException` |
| `CodecEncoderHandler` | 对非 `Envelope` 继续抛（不变）；迁移装配不装它 |

**步骤**

1. `RawFrame` 的 `Dispose` 用 `Interlocked.CompareExchange` 置位；置位后不再触碰 `Owner`。
2. 链头 CAS 必须在调用 `IRawByteStream.SendAsync` **之前**；`SendAsync` 抛异常后也不得再 `Dispose`（传输层已接管）。
3. 出站边界对 `RawFrame` 自动补偿；普通业务 `IDisposable` 不代管。
4. 重复转发检查用每次调用视图里的转发标记（Release 也生效，不能只在 Debug）。

**测试**（`Tests/Runtime/RawFrameTests.cs`）

- `RawFrame_DisposeBeforeHandoff_ReleasesOwnerOnce`
- `RawFrame_DisposeAfterHandoff_DoesNotReleaseOwner`
- `Pipeline_HeadSinkClosed_DisposesRawFrame`
- `Pipeline_NotAttached_DisposesRawFrame`
- `Pipeline_AdapterThrowsAfterCas_TransportReleasesOwnerOnly`
- `Pipeline_HandlerSwallowsRawFrame_ReleasesOnce`
- `Pipeline_HandlerThrowsBeforeForward_ReleasesOnce`
- `Pipeline_HandlerForwardsTwice_ThrowsAndReleasesOnce`
- `Pipeline_EnvelopeChain_StillWorks`（既有标准节点全装配，行为不变）
- `Pipeline_MigrationChainWithoutCodecEncoder_RawFrameReachesHead`

**验收**：Pipeline.Tests 全绿；无「已移交后再次 Dispose」路径。

---

### P0-9 业务写入口 `IChannelPipeline.WriteAsync` 与收口状态机

**目标**：实现设计 §5.8：`OutboundOperation`、exactly-once 计数、`Open → DrainingWrites → Closing`、`CloseAfterWriteAsync`、重入拒绝。

**文件**

| 文件 | 改动 |
|---|---|
| 新增 `Runtime/OutboundOperation.cs` | `Queued / HandedOff / Completed` + active 标记 + 公共 `TaskCompletionSource` + 唯一 `TryComplete` |
| `ChannelPipeline.cs` | `WriteAsync(object)` 业务入口：领取操作号/额度/计数 → 从最右出站节点启动；同步异常也 `TryComplete`；计数 exactly-once；`MaxPendingWrites` 超限直接失败 |
| `ChannelPipeline.cs` | 关闭状态机：`Open → DrainingWrites → Closing`；普通 `CloseAsync` 与 `CloseAfterWriteAsync` 共用；重复调用复用同一完成源；进入 Closing 后均 no-op |
| `DefaultChannelHandlerContext.cs` | `ctx.WriteAsync` 仅转发、沿用当前操作；出站回调外调用抛 `InvalidOperationException`；`CloseAfterWriteAsync` 在出站回调内调用抛 `InvalidOperationException` |
| `ChannelPipelineOptions.cs` | `DrainTimeout <= 0` 在 `CloseAfterWriteAsync` 口径下取 5 秒；`DrainInFlight` 零值语义不变（XML 注释分开写） |
| `DefaultChannelHandlerContext.Pipeline` | 返回同操作守卫视图，`WriteAsync` 重入抛错 |

**步骤**

1. 计数只在业务入口 +1，`OutboundOperation.TryComplete` 首次成功 -1，归零唤醒。
2. 关闭：同一同步边界内「停止接纳 + 快照等待」；`DrainingWrites` 中普通 `CloseAsync` 加入等待而非抢占。
3. 超时：强制完成 `Queued`（含 active-not-handed）项的公共 Task；`RawFrame` 走未移交补偿；`HandedOff` 项不伪造完成、不释放消息，由下游/迟完成清理。
4. 重入：当前出站 handler 内 `CloseAfterWriteAsync` 或 `ctx.Pipeline.WriteAsync` 同步抛 `InvalidOperationException`；`ctx.WriteAsync` 照常可用。
5. `ChannelInactiveAsync` 中 `CloseAfterWriteAsync` 为 no-op（已在 Closing）。

**测试**（`Tests/Runtime/OutboundWriteTests.cs`、`CloseDrainTests.cs`）

- `WriteAsync_SingleWrite_CompletesAfterHandoff`
- `WriteAsync_HandlerThrows_CompletesOnceWithFailure`
- `WriteAsync_CustomHandlerSwallows_CompletesAndNoLeak`（`RawFrame`）
- `WriteAsync_HeadClosedBeforeHandoff_FailsOnce`
- `WriteAsync_NotAttached_FailsAndDisposesRawFrame`
- `CtxWriteAsync_OutsideOutboundCallback_Throws`
- `CtxWriteAsync_InsideOutboundCallback_ForwardsSameOperation`
- `CtxWriteAsync_RepeatedSameNode_Throws`
- `CloseAfterWriteAsync_InsideOutboundCallback_Throws`
- `CloseAfterWriteAsync_WaitsSubmittedWrites`
- `CloseAfterWriteAsync_DrainTimeoutZero_UsesFiveSeconds`（用极小/零值 + 可控慢写，不真等 5 秒：注入时间源或断言默认值）
- `CloseAfterWriteAsync_Timeout_FailsQueuedOnly`
- `CloseAfterWriteAsync_Twice_ReusesSameCompletion`
- `Close_WhileDraining_JoinsInsteadOfCanceling`
- `WriteAsync_AfterCloseRequested_Rejected`
- `WriteAsync_ConcurrentWrites_CountExactlyOnce`（压力：N 线程 × M 次，断言计数归零一次唤醒）
- `Outbound_Queued_ActiveNotHanded_ActiveHandedOff_RemovalSemantics`

**验收**：Pipeline.Tests 全绿；无永久等待用例（全部带超时断言）。

---

### P0-10 `OutboundSerializingHandler`

**文件**：新增 `Src/SAEA.Sockets.Pipeline/Handlers/OutboundSerializingHandler.cs`（`public sealed`，继承 `OutboundHandlerAdapter`）。

**步骤**

1. `lock` + `Queue<PendingWrite>` + `_active` + `_activeItem`；**不用 `SemaphoreSlim` 当 FIFO**。
2. 写者锁内取队首、登记 `_activeItem`、`Queued → HandedOff`，锁外 `await ctx.WriteAsync`，按真实结果 `TryComplete`，回锁取下一个。
3. `MaxPendingWrites`（默认 256）超限：本次写直接失败，`RawFrame` 按未移交补偿，其他对象不代管。
4. 不声明 `IDisposable`；清理挂 `HandlerRemoved`：queued / active-not-handed 禁止移交、公共 Task `ChannelClosedException`、`RawFrame` 补偿；active-handed-off 只让公共 Task 失败，消息由下游负责，迟完成不重复减计数。
5. 一个写失败只让自己失败并冒泡，不阻断后续（连接级失败统一 `ChannelClosedException`）。

**测试**（`Tests/Handlers/OutboundSerializingHandlerTests.cs`）

- `Serializer_AsyncEncoder_MaintainsFifo`（编码器内随机 `await Task.Yield`）
- `Serializer_ConcurrentBusinessWrites_NoInterleaving`（断言到达顺序 = 入队顺序，不承诺并发调用顺序）
- `Serializer_ExceedsMaxPendingWrites_FailsNewWrite`
- `Serializer_RemovedWhileQueued_FailsWithoutHandoff`
- `Serializer_RemovedWhileActiveHandedOff_DoesNotDoubleComplete`
- `Serializer_TimeoutRace_LateCompletionDoesNotDecrementTwice`
- `Serializer_OneWriteFails_DoesNotBlockFollowingWrites`

**验收**：Pipeline.Tests 全绿。

---

### P0-11 分帧协议错误上链

**文件**：`ChannelPipelineExecutor.PumpAsync` 的 split 异常分支。

**步骤**

1. 分帧器抛 `FrameProtocolException`：停止切帧，`await FireExceptionCaughtAsync` 把异常派上链。
2. 派发返回后若连接仍为 `Open` → 立即强制收口；已是 `DrainingWrites` / `Closing` → 交给 P0-9 状态机，不再起第二个关闭流程。
3. 分帧器本身绝不写 socket。
4. 其他异常保持原有「记录 + 收口」。

**测试**（`Tests/Runtime/PumpProtocolErrorTests.cs`）

- `Pump_SplitProtocolError_ExceptionReachesChain`
- `Pump_SplitProtocolError_HandlerWritesResponseThenCloses_ResponseHandedOff`
- `Pump_SplitProtocolError_NoHandlerCloses_ForcesShutdown`
- `Pump_SplitProtocolError_HandlerAlreadyDraining_NoSecondClose`

---

### P0-12 适配层母本冻结与 `PipelineTest` 同步

**文件**

| 文件 | 改动 |
|---|---|
| `Src/SAEA.Sockets.PipelineTest/SocketStreamAdapter.cs` | `SendAsync` 真正 `await` 新 Task，`catch` 同时翻译同步异常与异步 fault；owner 不二次释放 |
| `Src/SAEA.Sockets.PipelineTest/PipelineHost.cs` | `TryRemoveChannel` 时序与 `ClearAttributes` 对齐（不重复释放） |
| `Src/SAEA.Sockets.PipelineTest/Program.cs` | 装配顺序改 `Create → Attach → 装配 → Start`；出站先加、串行器最后；`MaxConcurrentMessages = 1` 的用例单独标注 |
| `Src/SAEA.Sockets.PipelineTest/EchoHandlers.cs` | 生命周期成员按新接口实现（若继承适配器则不动） |

**步骤**

1. 母本加 XML 注释写明：Task 只表示「交接」，不表示对端收到；异步 fault 也必须翻译。
2. `--all` 用例拆成「回环 + 真实 socket」两类；`--bench` 保持原输出格式（验收要对比数值）。
3. 演示工程新增一条断言：`Start()` 后 `AddLast` 抛 `InvalidOperationException`。

**验收**

```powershell
dotnet run --project Src/SAEA.Sockets.PipelineTest -c Release -- --all
dotnet run --project Src/SAEA.Sockets.PipelineTest -c Release -- --bench
```

`--bench` 与 B0 基线对比：**吞吐不低于基线 95%**；每帧分配只记录趋势并说明来源（调用绑定视图与派发状态属设计固定成本，不按 5% 卡分配，依据设计 §十二 第 28 条）。

---

### P0-13 基础设施 README（4 份）

**文件**

- `Src/SAEA.Sockets.Pipeline/README.md` / `README.en.md`
- `Src/SAEA.Sockets/README.md` / `README.en.md`

**必须写明的破坏性变更**

1. `IChannelHandler` / `IChannelInboundHandler` / `IChannelHandlerContext` / `IChannelPipeline` 共 6 个新成员，直接实现接口会编译失败，继承适配器不受影响。
2. `ctx.WriteAsync` 改为「仅出站回调内合法」，回调外抛 `InvalidOperationException`。
3. 入站普通异常语义：通知一次后冒泡（不再吞掉）。
4. `Start()` 后链结构冻结。
5. `SAEA.Sockets` 两个带 `owner` 发送重载 `void → Task`；未 `await` 仍可编译，但观察不到异步失败；自定义实现必须改签名；UDP 四个实现同步。
6. `owner` 释放口径：成功与可确认退出路径恰好一次；无法确认内核退出的超时路径放弃回收。
7. `ByteToMessageDecoder` 不累加半包；`FrameSplitterHandler` 不可与泵共存。
8. `RawFrame` 与信封模型互斥，同一链不得同时存在。
9. `CloseAfterWriteAsync` 只保证「已提交传输层」，不保证对端收到；`DrainTimeout <= 0` 在该入口取 5 秒。
10. 内存上限只承诺「输入残余字节」与「待写项数」两项有界。

中英两份内容必须一致；示例代码可编译（不要写伪 API）。

**验收**：`git diff --stat` 显示 4 份 README 均改动；人工核对中英条目逐条对应。

---

### P0-14 P0 收口验收

```powershell
dotnet build Src/SAEA.Sockets.sln -c Debug
dotnet test Src/SAEA.Common.Tests/SAEA.Common.Tests.csproj -c Release
dotnet test Src/SAEA.MQTT.Tests/SAEA.MQTT.Tests.csproj -c Release
dotnet test Src/SAEA.FTP.Tests/SAEA.FTP.Tests.csproj -c Release
dotnet test Src/SAEA.Sockets.Pipeline.Tests/SAEA.Sockets.Pipeline.Tests.csproj -c Release
dotnet run --project Src/SAEA.Sockets.PipelineTest -c Release -- --all
dotnet run --project Src/SAEA.Sockets.PipelineTest -c Release -- --bench
```

**P0 完成判据**

- 0 errors；四个 MSTest 工程全绿（Common 59 / MQTT 153 / FTP 13 / Pipeline = B0 + 新增）。
- `--all` 通过；`--bench` 吞吐不低于基线 95%，每帧分配记录趋势并说明来源。
- 设计与计划对 P0 验收的交叉项已校正：P0 只交付「分帧协议错误上链 + 有界收尾」能力；413/431 响应生成随 P1 的 HTTP 分帧器交付与验收（见设计 §十二 第 29 条）。
- 设计 §十 P0 验收列出的每一类用例在测试文件中有对应方法（逐条勾选，缺一不可）。
- 无 `#pragma warning disable` 新增、无 `[Ignore]`、无删除既有断言换绿。

---

## 三、P1：`SAEA.Http` + `SAEA.MVC` 试点

### P1-1 适配层副本

**新增**（命名空间随包）

- `Src/SAEA.Http/Net/PipelineRawByteStream.cs`：`IRawByteStream`（`PipeReader` + `IReceiveFlowControl` + `SendAsync(owner)` 真 `await`）
- `Src/SAEA.Http/Net/PipelineHostRegistry.cs`：`IPipelineHost`
- `Src/SAEA.Http/Net/PipelineWiring.cs`：`Create → Attach → 装配 → Start → 等 Completion → 清理`，整体 `try/catch/finally`，失败路径关 socket + 摘 host

要求：只改命名空间与前缀；带 banner；`netstandard2.0` + C# 8。

### P1-2 HTTP 分帧器与解码器

- `Src/SAEA.Http/Net/HttpRequestSplitter.cs`：先头（`\r\n\r\n`，头上限 64KB，超限 431/400）后 body；`Content-Length > MaxRequestSize` 立刻停；`Transfer-Encoding: chunked` 标记为「显式 400」；带 `Upgrade: websocket` 的请求置 `StopAfterCurrent = true`。
- `Src/SAEA.Http/Net/HttpRequestDecoder.cs`：`ByteToMessageDecoder`，帧 → `HttpRequest`（或现有模型）。
- `Src/SAEA.Http/Net/HttpResponseEncoder.cs`：`IChannelOutboundHandler`，响应对象 → `RawFrame`。
- `Src/SAEA.Http/Net/HttpProtocolHandler.cs`：内部双状态机（HTTP / WS），按阶段分派，不换链节点。

### P1-3 `WebHost` / `HttpSocket` 迁移

- 装配入口挂已有的 `WebConfig.ConfigurePipeline`（`public Action<string, IChannelPipeline>`），不新增 `WebHostOptions`；同处承载 `RequestDelegate` / `ExceptionHandler` / `ConnectionClosed` 三个委托。
- 删除 9 个接收/生命周期事件（`WebHost` 3 个公开 + `HttpSocket` 3 个 + `HttpCoder` 的原始字节回调）；`HttpSocketDebug` 与 `HttpCoder` 整类删除，其原始字节通道改由 `HttpRawFrameSinkHandler` 承担。
- 一律 `UseStream()` + `UseInputHandler` + `UseConnectionOpened`，`MaxConcurrentMessages = 1`。
- `IUserToken` → `IChannel` 的公开签名替换（31 处引用收敛）。
- 主动推送 `WebHost.Send(channelId, ...)` → 查 host → 会话 ctx → `ctx.Pipeline.WriteAsync`。
- 400/413/431 由 `HttpProtocolHandler` 生成 `HttpResponse` 对象经 `HttpResponseEncoder` 写出，再 `CloseAfterWriteAsync`；`RawFrame` 只用于 `Send(byte[])` 主动推送。

### P1-4 MVC 透传

- `SAEA.MVC` 不再镜像 `WebHost` 的接收/生命周期事件，一并删除；`SAEAMvcApplication.OnException` / `OnRequestDelegate` 作为应用级公开事件保留，在 `Start()` 里转赋给 `WebConfig.ExceptionHandler` / `RequestDelegate`，断开通知由 `WebConfig.ConnectionClosed` 驱动。
- `HttpContextBase.OnException/OnRequestDelegate` 删除，改由 `HttpDispatchHandler` 在链上统一异常与终结点处理。

### P1-5 演示与文档

- `Src/SAEA.HttpTest` 改为链装配写法；保留自测入口。
- `Src/SAEA.MVCTest`、`Src/SAEA.Mvc.ServiceTest` 沿用 `SAEAMvcApplication` 应用级事件，源码无需改动；`Mvc.ServiceTest` 的 4 个 SAEA `HintPath` 引用换成 `ProjectReference`（`SAEA.Common` / `SAEA.Http` / `SAEA.MVC`），并与 `packages.config` 同步。
- `SAEA.Http`、`SAEA.MVC` 各中英 README 重写：能力清单、事件表删除、装配示例、上限说明（`MaxRequestSize` 64MB 只限 body，头 64KB，`MaxBufferedBytes` 显式下限）。

### P1-6 验收

- `HttpTest` 场景逐条通过：`Content-Length` 请求体、静态资源、101 写成功后才切换、写失败不切换、body 超限 413、头部超限、chunked 显式 400、同 TCP 段「101 + WS 首帧」。
- 报头结束符已到齐但解析失败（请求行/报头畸形）必须立即 400 并收口，不能按数据不足继续等。
- 断言无整段 `ToArray()`（`grep` 接收路径）。
- HTTP 相关单测不得新增对 demo 的依赖。

---

## 四、P2：`SAEA.FileSocket` + `SAEA.WebSocket`

### P2-1 FileSocket（已完成，见 `p2-1-acceptance-2026-11-10.md`）

- 适配层 3 件套副本。
- `LengthFieldSplitter` 配置 `0 / 8 / +1 / 0`（保留完整帧）；`MaxFrameLength` 取现有 coder 语义，不放大。
- `FileMessageDecoder : ByteToMessageDecoder`；`FileMessageEncoder` → `RawFrame`。
- 删除 `Server` 的 3 个公开事件（`OnRequested` / `OnFile` / `OnError`）与 2 个内部接收订阅；`ConfigurePipeline`；`MaxConcurrentMessages = 1`。
- 测试：跨段、粘包、边界长度（`bodyLength = 0 / 1 / max`）、发送完成回调时序（服务端暂停收包时不得提前回调）。

### P2-2 WebSocket（已完成，见 `p2-2-acceptance-2026-11-10.md`）

- 服务端 `WSServerImpl` 与客户端 `WSClient` 都迁；TLS 仍由 `WithSsl`。
- WS 分帧器只切 wire frame；`WsFrameDecoder : InboundHandlerAdapter` 持分片状态：continuation 拼接、分片间允许控制帧、拒绝新数据帧交织、执行 `MaxMessageSize`。
- 删除 11 个事件；`ConfigurePipeline`（服务端与客户端对称）。
- 测试：continuation 分片 + 穿插控制帧、非法数据帧交织、超限消息、`max(HTTP, WS)` 上限、TLS 服务端与客户端各一条。

### P2-3 演示与 README（已完成，见 `p2-2-acceptance-2026-11-10.md`）

- `FileTest`、`WebSocketTest` 改链装配并人工 smoke；`WebSocketTest` 若为窗口程序，记录人工步骤。`FileTest` 只用 `FileTransfer` 的传输级事件、链在 `FileTransfer` 内装配，源码无需改动。
- `SAEA.FileSocket`、`SAEA.WebSocket` 中英 README。

---

## 五、P3：`RPC` + `QueueSocket` + `MessageSocket`

### P3-1 RPC

- 分帧 `1 / 4 / -4 / 0`；`RSocketMsg` 编解码从 `RpcCoder` 拆到 `RpcMessageDecoder` + `RpcMessageEncoder`（→ `RawFrame`）。
- 删 10 个事件（公开 5）；`MaxConcurrentMessages = 1`（请求/响应匹配）。
- 测试：真实编码帧跨段、粘包、边界 `total`。

### P3-2 QueueSocket

- 分帧 `1 / 4 / -4 / 0`；删 12 个事件（公开 11）。
- `Exchange.OnBatched` 批处理语义保留：batch 作为一次业务处理，不拆分链上顺序。
- 测试：`--functional` 入口仍可用，另加跨段/粘包/边界。

### P3-3 MessageSocket

- 分帧 `0 / 8 / +1 / 0`；把 `MessageContext` / `Unpacker` 的包边界职责移到分帧器，解码器只做反序列化。
- 删 8 个事件；测试真实编码帧跨段、粘包、边界。

### P3-4 演示与 README

- `RPCTest`、`QueueSocketTest`、`MessageTest` 改链装配；`QueueSocketTest --functional` 自动跑。
- 对应 3 包中英 README。

---

## 六、P4：`SAEA.FTP` 传输重写

1. **先删死代码**：`Net/ServerSocket.cs`、`Net/FTPStream.cs`、`Core/FTPDataSocketManager.cs`、`Net/ClientSocket.cs`、`FTPCoder.Decode` 空实现。
2. **控制通道**：服务端 `FTPServer.ServerSession` 与客户端 `Net/FtpControlConnection` 从 `TcpClient` 重写为 SAEA socket + Pipeline。
   - 服务端 `FtpCommandDecoder → ClientRequest`（`LineSplitter`）。
   - 客户端 `FtpReplyDecoder → FtpReply`：greeting、多行 `xyz-` … `xyz `、非法状态码、`FEAT` 能力解析、等待期间连接关闭唤醒。
   - 客户端 `Send()` 改为 `await ctx.Pipeline.WriteAsync(command)` + 等该请求 `FtpReply` 完成源；保留超时与请求-应答串行（`MaxConcurrentMessages = 1`）。
3. **数据通道**：`TakeAvailableSplitter` + `FtpDataChunkDecoder` + `ChannelStreamAdapter`（有界队列、`DispatchMode.Queued`、EOF/取消/异常/Dispose 全唤醒、背压不堵分发线程）。
4. **PASV / REST / 上传下载方向 / 226 完成应答 / 关闭时序**逐项保持。
5. **测试**：`SAEA.FTP.Tests` 13 个现有用例全绿，补 greeting、多行应答、PASV、REST、数据通道收尾、adapter 反例（Inline 快路径 + 独立消费者）。
6. **演示与 README**：`FTPTest`（WinExe / .NET Framework 4.6.2）引用切 `ProjectReference` 或用本次构建输出 `HintPath`，人工 smoke 记录实际加载程序集路径；`SAEA.FTP` 中英 README。

---

## 七、P5：`SAEA.P2P` TCP 接链

1. `TCPChannel` 改 `UseStream` + Pipeline，只删 3 个内部传输事件。
2. `P2PClient` / `P2PServer` 现有公开事件、`OnStateChanged`、`OnLocalNodeDiscovered` 原样保留，不加别名。
3. `UDPChannel` / `LocalDiscovery` 零改动。
4. 验收：`dotnet run --project Src/SAEA.P2PTest -c Release -- --all`、`--tls` 通过；公开事件行为回归；`P2PTest` 人工 smoke。
5. `SAEA.P2P` 中英 README 写明「对外不暴露链」的例外。

---

## 八、README 与 demo 总清单

| 组件 | README 中/英 | demo |
|---|---|---|
| `SAEA.Http` | 2 | `HttpTest` |
| `SAEA.MVC` | 2 | `MVCTest`、`Mvc.ServiceTest` |
| `SAEA.FileSocket` | 2 | `FileTest` |
| `SAEA.WebSocket` | 2 | `WebSocketTest` |
| `SAEA.RPC` | 2 | `RPCTest` |
| `SAEA.QueueSocket` | 2 | `QueueSocketTest` |
| `SAEA.MessageSocket` | 2 | `MessageTest` |
| `SAEA.FTP` | 2 | `FTPTest` |
| `SAEA.P2P` | 2 | `P2PTest` |
| `SAEA.Sockets` | 2 | `PipelineTest`（P0） |
| `SAEA.Sockets.Pipeline` | 2 | — |

根目录 `README.md` / `README.en.md` 只在能力描述变化时同步（当前无事件示例）。

---

## 九、最终验收

1. **构建**：`dotnet build Src/SAEA.Sockets.sln -c Debug` 0 errors。
2. **单测**：Common 59、MQTT 153、FTP ≥13、Pipeline ≥P0 基线，四工程分别指定 csproj。
3. **非交互自测**：`PipelineTest --all` / `--bench`、`QueueSocketTest --functional`、`P2PTest --all` / `--tls`，保存命令、退出码、关键输出。
4. **demo**：10 个迁移 demo 逐个核对链装配；窗口/交互程序只记人工步骤与结果，未实际操作写「未运行」。
5. **破坏面核对**：设计 §九 的 66/48 声明逐条对照，另附完整公开 API 破坏清单（含接口、override、转发声明、`HttpContextBase.OnException/OnRequestDelegate`、`SAEAMvcApplication` 两个事件）。
6. **语法**：`grep` 检查库项目无 C# 9+ 语法。
7. **README**：18 份迁移侧 + 4 份基础设施，中英一致、无失效示例。
8. **未运行项**：单独列出并说明原因（环境/窗口程序/外部服务）。

---

## 十、风险与回滚

| 风险 | 处置 |
|---|---|
| P0-1 发送语义改动波及未迁移包（RedisSocket 等） | 每轮全量构建 + MQTT/FTP 回归；RedisSocket 只做编译与手工人机回归，记录「未运行」 |
| 两执行者同时构建导致 `obj/` 冲突 | 子代理串行执行，一轮结束再下一轮 |
| Pipeline 接口破坏 | 已限定为 6 个新成员 + 适配器默认实现，仓库内无直接实现接口的类 |
| 适配层 8 份副本漂移 | P0-12 冻结母本 + 单测；各副本只改命名空间；README 记录副本清单 |
| HTTP 上限行为变更 | README 明写 64MB 只限 body、头 64KB、chunked 显式 400 |
| FTP 重写回归面大 | P4 独立阶段，先删死代码再改活代码；13 个既有用例作为及格线 |
| 回滚 | 每阶段改动集中在独立文件与包内，按阶段 `git checkout -- <包目录>` 可回到上一阶段；不自动提交，用户可自行分批提交 |

---

## 十一、执行提示（给子代理）

1. 先读本计划对应任务 + 设计对应章节，再读目标文件现状，再动手。
2. 一次只做一个任务，做完立即构建 + 跑该任务相关测试 + 全量 Pipeline.Tests。
3. 报告格式：任务编号 / 改动文件清单 / 新增或修改的用例名 / 命令与退出码 / 未完成项与原因。
4. 遇到与设计冲突或设计未覆盖的选择：停下报告，给 2–3 个方案，不自行换方案。
5. 不提交、不推送、不改版本、不动他人并行文件。