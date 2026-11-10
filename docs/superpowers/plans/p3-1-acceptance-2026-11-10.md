# P3-1 验收记录：`SAEA.RPC`（2026-11-10）

**计划：** `docs/superpowers/plans/2026-11-09-socket-pipeline-migration-plan.md`（§五 P3-1）

**设计：** `docs/superpowers/specs/2026-11-06-socket-pipeline-migration-design.md` §5.3（连接处置）、§D（跨帧重组）、§E（协议错误）、§八（长度域帧配置）、§九（破坏面）

**范围：** `SAEA.RPC` 源码 9 个改写（`RServer` / `RClient` / `RpcCoder` / `RSocketMsg` / `ServiceProvider` / `ServiceConsumer` / `ConsumerMultiplexer` / `CodeGnerater` / `SAEA.RPC.csproj`）+ 新增适配层 6 个（`Src/SAEA.RPC/Net/Pipeline/`）+ `SAEA.Common/DisorderSyncHelper.cs`（缺陷修复，见 §三. 2，用户批准方案 A）+ 新建 `Src/SAEA.RPC.Tests`（12 条）；`Src/SAEA.RPCTest` 只改 `Program.cs` 的错误订阅，demo 与中英 README 留 P3-4

---

## 一、构建与测试

| 命令 | 退出码 | 结果 |
|---|---|---|
| `dotnet build Src/SAEA.Sockets.sln -c Debug` | 0 | 0 errors / 3672 warnings（增量；主体为存量 `CS1591`，与 B0 的约 3.8k 同量级） |
| `dotnet build Src/SAEA.RPC/SAEA.RPC.csproj -c Debug` | 0 | 0 errors / 0 warnings（增量） |
| `dotnet build Src/SAEA.Common/SAEA.Common.csproj -c Debug --no-incremental` | 0 | 0 errors / 90 warnings（88 `CS3021` + 2 `CS0168`，均存量） |
| `dotnet test Src/SAEA.RPC.Tests/… -c Debug` | 0 | **12 / 12**（新建工程，未入 sln） |
| `dotnet test Src/SAEA.Common.Tests/… -c Release` | 0 | 59 / 59 |
| `dotnet test Src/SAEA.Sockets.Pipeline.Tests/… -c Release` | 0 | 260 / 260 |
| `dotnet test Src/SAEA.Http.Tests/… -c Release` | 0 | 28 / 28 |
| `dotnet test Src/SAEA.FileSocket.Tests/… -c Release` | 0 | 10 / 10 |
| `dotnet test Src/SAEA.WebSocket.Tests/… -c Release` | 0 | 10 / 10 |
| `dotnet test Src/SAEA.MQTT.Tests/… -c Release` | 0 | 153 / 153 |
| `dotnet test Src/SAEA.FTP.Tests/… -c Release` | 0 | 13 / 13 |

`SAEA.RPC.Tests` 为 `net10.0` + MSTest，只引用 `SAEA.RPC` 与 `SAEA.Sockets.Pipeline`，不加入 sln，不依赖外部服务。12 条用例：

- 跨段：`RpcMessageDecoder_SplitAcrossReads_ProducesSingleMessage`（只写半帧时不产出消息）
- 粘包：`RpcMessageDecoder_TwoFramesInOneRead_ProducesTwoMessagesInOrder`
- 边界 `total`：`RpcMessageDecoder_MinimalTotal_ProducesEmptyFields`、`RpcMessageDecoder_TotalBelowFixedLength_Throws`
- 越界字段：`…_ServiceLengthBeyondFrame_Throws`、`…_MethodLengthBeyondFrame_Throws`
- 帧体所有权：`RpcMessageDecoder_DataCopied_SurvivesFrameRelease`
- 出站线格式：`RpcMessageEncoder_RequestWithData_MatchesWireLayout`、`…_EmptyFields_WritesMinimalTotal`、`…_Utf8Names_WritesByteLength`、`…_NullMessage_Throws`
- 兼容入口：`RpcCoder_Encode_DelegatesToEncoder`

---

## 二、端到端验证（真实 socket 往返）

探针工程（临时目录，不入库）：`ServiceProvider` 起真实监听 → `ServiceConsumer` 单连接调用 → 三个方法各压一个方向：

| 方法 | 方向 | 校验内容 |
|---|---|---|
| `ProbeService.Make(int)` | 服务端 → 客户端（响应体） | 返回值按索引模式逐字节比对 |
| `ProbeService.Check(byte[])` | 客户端 → 服务端（请求体） | 服务端回算 FNV 与客户端期望一致 |
| `ProbeService.Echo(byte[])` | 双向 | 回显字节按索引模式逐字节比对 |

尺寸覆盖 `16 / 1024 / 4096 / 8192 / 10240 / 12021 / 16384 / 20000 / 60000 / 120000 / 200000`，每尺寸 2 轮，连跑 5 轮：

| 轮次 | 调用数 | 断言数 | 结果 |
|---|---|---|---|
| 1–5 | 22 / 轮（共 110） | 330 | 全部 `OK`，每轮 `ALL PASS` |

阈值点说明：`10240`、`12021` 覆盖 `MemoryPoolManager` 的 4KB/64KB 分级边界与 `SmallFrameInlineThreshold` 附近；`200000` 覆盖跨多个 8KB 写分片的整帧。

---

## 三、本轮修掉的两个数据缺陷

首轮验证暴露「大响应体被零填充/错位」与「偶发返回 null」，均为真实缺陷，不是迁移引入的用法错误（触发条件与帧大小正相关）。

### 1. 响应体被清零：`RSocketMsg.Dispose`

- 现象：`response run@0 len=186 zeros=True frameOff=25`，随后每段 250 字节全零、段间隔 251；`badBytes=19921/20000`。
- 定位：解码侧日志显示帧体哈希正确、业务侧哈希全零，说明数据在「解码之后、业务使用之前」被改写。
- 根因：`RSocketMsg` 实现 `IDisposable`，处理器链尾 `ExceptionTerminalHandler` 会 `Dispose()` 未消费的入站消息；`Dispose()` 里 `Array.Clear(Data)` 清零的正是已交给 `DisorderSyncHelper`（进而交给业务线程）的同一个数组。
- 修复：`Dispose()` 不再清零 `Data`，只把引用置 null（消息对象本身不池化，清零无收益），并同步 XML 注释。
- 复验：修复后零填充现象消失。

### 2. 偶发返回 null：`DisorderSyncHelper.Set` 的发布顺序

- 现象：`size=20000 round=0 … response=(null) BAD`，且日志里响应帧已到达（`in type=4 seq=… len=27`）而请求侧 `result=null`；8 轮里 4 轮出现 `BAD`。
- 根因：`Set` 先写 `v.HasVal = true` 再写 `v.Data = t`，`Wait` 的自旋循环只读 `HasVal`，等待方可在这两句之间看到「已就绪但 `Data` 仍为 null」，随即返回 `default(T)`。
- 修复（用户选定方案 A）：`Set` 先落 `Data` 再发布 `HasVal`；`DisorderSyncItem.HasVal` 改为背衬字段 + `Volatile.Read/Write`，保证发布语义在弱内存序平台（ARM64）同样成立。公开 API 不变。
- 复验：仅调换顺序即由「8 轮 4 轮失败」变为 10/10 全绿；补 `Volatile` 后 §二 的 330 次断言全绿。

该文件属 `SAEA.Common`，不在 P3-1 声明范围；全仓库只有 `RClient` 使用它，改动已获用户批准并单独说明。

---

## 四、P3-1 条目对照

| 验收项 | 证据 |
|---|---|
| 分帧 `1 / 4 / -4 / 0` | `RpcFrameSplitter.Create` 参数写死 `Offset=1 / LengthFieldLength=4 / LengthAdjustment=-4 / InitialBytesToStrip=0`，小端；`HeaderLength = 5` |
| `MaxFrameLength` 不放大 | `RpcFrameSplitter.ResolveMaxFrameLength()` 取 `BaseCoder.MaxFrameLength` 并按 `[0, int.MaxValue - P_Head]` 夹逼 |
| 编解码从 `RpcCoder` 拆出 | 编码 → `RpcMessageEncoder.WriteAsync`（池化 `PooledBufferWriter` → `RawFrame`）；解码 → `RpcMessageDecoder.Decode`（长度字段与实际帧长比对，不一致抛 `FrameProtocolException`）；`RpcCoder` 收敛为 `public byte[] Encode(RSocketMsg)` 委托编码器 |
| 池化帧不泄漏到业务 | 解码时帧体复制成独立数组并置 `IsPooled = false`，业务可跨回调持有 |
| `MaxConcurrentMessages = 1` | `RServer` / `RClient` 的 `ChannelPipelineOptions` 均为 1，保证同一连接请求-响应串行匹配 |
| 链装配顺序 | `RpcMessageEncoder → OutboundSerializingHandler → RpcMessageDecoder → InboundDispatcher → ActiveHandler → 使用者 → ExceptionTerminalHandler`（服务端为 `RpcMessageEncoder → OutboundSerializingHandler → RpcMessageDecoder → InboundDispatcher → ExceptionTerminalHandler`） |
| 公开事件删除 | 运行时事件共 10 个：公开面 5 个中删 4 个（`ServiceProvider.OnErr`、`ConsumerMultiplexer.OnNoticed/OnDisconnected/OnError`），`ServiceConsumer.OnNoticed` 按「推送通知属领域事件」保留；内部类 `RServer`（`OnMsg`/`OnError`）、`RClient`（`OnNoticed`/`OnDisconnected`/`OnError`）的 5 个事件一并删除，改为 `MessageHandler`/`ErrorHandler`/`NoticeHandler`/`DisconnectedHandler` 委托属性 |
| 装配入口 | `ServiceProvider` / `ServiceConsumer` / `ConsumerMultiplexer` 各加 `Action<string, IChannelPipeline> ConfigurePipeline`，逐层向下转发到 `RServer`/`RClient` 的建链回调 |
| 生成代理 | `CodeGnerater` 模板删 `OnErr`/`OnNoticed` 事件与两个转发方法，改生成 `ConfigurePipeline` 透传属性；错误经 `ExceptionCollector.OnErr` 观察 |
| 发送入口 | `RServer.ReplyAsync` / `RClient.SendBase` 走 `IChannelPipeline.WriteAsync`；应答改 `await`，不再有裸 `Send` 快路径 |
| 依赖 | `SAEA.RPC.csproj` 增加 `ProjectReference` 指向 `SAEA.Sockets.Pipeline`，未新增 NuGet 包 |

---

## 五、代码生成模板验证

模板改动无法被单测覆盖（生成物要能编译），故用临时工程做两步验证（不入库）：

1. `RPCServiceProxy.cs` 由 `CodeGnerater.Generate(folder, "RpcGen", typeof(ProbeGenSvc))` 生成到临时目录；服务含 `byte[]` 返回值、`byte[]` 入参与自定义实体返回值，覆盖 Service / Model 两条生成分支。
2. 把生成文件链接进第二个工程编译并跑端到端。

| 项 | 结果 |
|---|---|
| 生成物包含 `ConfigurePipeline` 透传属性 | 是（`get/set` 转发到 `_serviceConsumer`） |
| 生成物是否残留 `OnErr` / `OnNoticed` | 否 |
| 生成物编译 | 0 errors / 0 warnings |
| `proxy.ConfigurePipeline = hook` 取值 | 往返一致（`pipeline-hook=ok`） |
| 经生成代理调用 `16 / 12021 / 200000` | 响应与请求方向全部 `OK`，`ALL PASS` |

附带发现（既有行为，本轮未改）：生成器固定输出 `using {spaceName}.Consumer.Model;`，若服务没有实体参数/返回值则 `Model` 命名空间不存在，生成物无法编译；本次验证通过给服务加实体方法绕开。若后续要支持「纯基础类型服务」，需在生成器里按需输出该 using，属生成器独立改进项。

---

## 六、与计划的偏差

1. **删除事件数口径**：计划写「删 10 个事件（公开 5）」。实际运行时事件共 10 个，其中公开面 5 个 = `ServiceConsumer.OnNoticed`（保留）+ `ServiceProvider.OnErr`（删）+ `ConsumerMultiplexer` 3 个（删）；`RServer`/`RClient` 的 5 个事件挂在内部类上，随迁移一并删除。另删生成模板里的 2 个事件（`OnErr`/`OnNoticed`），不计入上面的 10 个。
2. **`SAEA.Common/DisorderSyncHelper.cs` 属范围外修复**：见 §三. 2，方案由用户在 A/B/C 中选定 A。
3. **`RSocketMsg.Dispose()` 语义调整**：不再清零 `Data`。数据数组所有权归业务方，清理会改写别人仍在使用的内存；`RSocketMsg` 不池化，清零本来也没有复用收益。
4. **`RpcCoder` 不是整体删除**：保留 `public byte[] Encode(RSocketMsg)` 作为兼容入口并委托 `RpcMessageEncoder`，删掉 `ICoder` 实现与 `Unpack`/`Decode`/`Clear`（用户确定的口径）。
5. **`SAEA.RPCTest` demo 未改**：其 `RPCServiceProxy.cs` 仍是旧模板的生成物（自带 `OnErr`/`OnNoticed` 事件），本轮只把服务端错误订阅改为 `ExceptionCollector.OnErr`；demo 重新生成代理与 README 同步归 P3-4。

---

## 七、未运行项

| 项 | 原因 |
|---|---|
| `dotnet run --project Src/SAEA.RPCTest` 交互式 smoke | demo 是菜单式控制台程序，需人工输入 url 与序号；且其代理文件待 P3-4 重新生成，现在跑的是旧模板产物 |
| 多连接/并发请求压测 | `MaxConcurrentMessages = 1` 是设计口径（请求-响应匹配），并发能力不在 P3-1 验收内 |
| `Notice` 推送方向端到端 | 现有服务实现没有推送方法，探针无法触发；`Notice` 分发逻辑与保留的 `ServiceConsumer.OnNoticed` 一致，只做了静态核对 |
| RPC 性能基线（`--bench`） | RPC 无 bench 入口，性能门禁按计划只对 socket 组件执行 |

---

## 八、遗留风险

1. **`DisorderSyncHelper` 的超时单位是秒**（`item.DateTime.AddSeconds(_timeOut)`），而调用方传的是毫秒（`RClient` 传 `timeOut`，探针为 15000）。实际效果是「几乎不超时」：响应丢失时调用线程会无限自旋。本轮只修发布顺序，未改这个语义——改成毫秒会让现有调用方立刻出现 15 秒级超时，需要单独立项并全仓库核对调用方。
2. **解码每帧复制一次帧体**：换取消息生命周期不依赖池化帧，与 FileSocket/WebSocket 同口径；分配趋势按 B0 的记录方式说明来源，不按阈值判定。
3. **建链窗口**：`RClient.SendBase` 在链未激活时等待 `_pipelineReady`（上限 `_timeOut`），超时后抛 `RPCSocketException`；连接极慢时业务侧只会看到有一次调用失败。
4. **事件删除后的可观测性**：断开与收发异常现在只能经 `ExceptionCollector.OnErr`（服务端名 `Provider`、客户端名 `Consumer` 等）观察，不再有实例级事件。
5. **`ServiceConsumer.OnNoticed` 是唯一保留的实例事件**：它在 `InboundDispatcher` 里直接调用，不进链；需要更细粒度通知时用 `ConfigurePipeline`。

---

## 九、下一步

按计划 §五 P3-2 处理 `SAEA.QueueSocket`：分帧 `1 / 4 / -4 / 0`，删 12 个事件（公开 11），保留 `Exchange.OnBatched` 的批处理语义（batch 作为一次业务处理），并给 `--functional` 入口补上跨段/粘包/边界用例。