# P3-2 验收记录：`SAEA.QueueSocket`（2026-11-10）

**计划：** `docs/superpowers/plans/2026-11-09-socket-pipeline-migration-plan.md`（§五 P3-2，589–593 行）

**设计：** `docs/superpowers/specs/2026-11-06-socket-pipeline-migration-design.md` §5.3（连接处置）、§D（跨帧重组）、§E（协议错误）、§八（长度域帧配置）、§九（破坏面）；QueueSocket 行见 §迁移矩阵

**范围：** `SAEA.QueueSocket` 改写 7 个（`QServer` / `QClient` / `Consumer` / `Producer` / `Net/QueueCoder` / `Model/Exchange` / `SAEA.QueueSocket.csproj`）+ 新增适配层 6 个（`Src/SAEA.QueueSocket/Net/Pipeline/`）+ `Src/SAEA.QueueSocketTest` 改写 6 个 + 新建 `Src/SAEA.QueueSocket.Tests`（14 条）。中英 README 与 demo 的手工 smoke 归 P3-4。

---

## 一、构建与测试

| 命令 | 退出码 | 结果 |
|---|---|---|
| `dotnet build Src/SAEA.Sockets.sln -c Debug` | 0 | 0 errors / 3672 warnings，耗时 20.94 s（增量；主体为存量 `CS1591`，与 B0 的约 3.8k 同量级） |
| `dotnet build Src/SAEA.QueueSocket/SAEA.QueueSocket.csproj -c Debug` | 0 | 0 errors / 0 warnings（增量） |
| `dotnet test Src/SAEA.QueueSocket.Tests/… -c Release` | 0 | **14 / 14**（新建工程，未入 sln） |
| `dotnet run --project Src/SAEA.QueueSocketTest -c Release -- --functional` | 0 | **73 / 73**，`0 failed` |
| `dotnet run --project Src/SAEA.QueueSocketTest -c Release -- --bench-queue` | 0 | **19 / 19**，`0 failed` |
| `dotnet test Src/SAEA.Common.Tests/… -c Release` | 0 | 59 / 59 |
| `dotnet test Src/SAEA.Sockets.Pipeline.Tests/… -c Release` | 0 | 260 / 260 |
| `dotnet test Src/SAEA.Http.Tests/… -c Release` | 0 | 28 / 28 |
| `dotnet test Src/SAEA.FileSocket.Tests/… -c Release` | 0 | 10 / 10 |
| `dotnet test Src/SAEA.WebSocket.Tests/… -c Release` | 0 | 10 / 10 |
| `dotnet test Src/SAEA.RPC.Tests/… -c Release` | 0 | 12 / 12 |
| `dotnet test Src/SAEA.MQTT.Tests/… -c Release` | 0 | 153 / 153 |
| `dotnet test Src/SAEA.FTP.Tests/… -c Release` | 0 | 13 / 13 |

`SAEA.QueueSocket.Tests` 为 `net10.0` + `LangVersion 10.0` + MSTest（`Microsoft.NET.Test.Sdk 17.8.0`、`MSTest.* 3.1.1`），只引用 `SAEA.QueueSocket` 与 `SAEA.Sockets.Pipeline`，不加入 sln，不依赖外部服务。

本轮改动只落在 `SAEA.QueueSocket` 及其测试工程，其余组件没有被引用或修改；上表后七行是回归确认，全部与迁移前同数。

---

## 二、新增用例（14 条）

`Tests/QueueMessageDecoderTests.cs`（8 条，走真实 `ChannelPipeline` + 内存字节流）：

| 用例 | 覆盖点 |
|---|---|
| `QueueMessageDecoder_SplitAcrossReads_ProducesSingleMessage` | 跨段：先写 3 字节（连 5 字节帧头都未到齐）不产出，补发剩余字节后恰好产出 1 条 |
| `QueueMessageDecoder_TwoFramesInOneRead_ProducesTwoMessagesInOrder` | 粘包：两帧一次写入 → 两条消息且顺序与字段不串 |
| `QueueMessageDecoder_MinimalFrame_ProducesEmptyFields` | 边界下限：`MINLENGTH = 13` 的心跳帧（`total = 12`）解出空名称/空主题/零长数据 |
| `QueueMessageDecoder_DataCopied_SurvivesFrameRelease` | 所有权：4 KB 数据取出后跨收口、帧缓冲释放仍完整可读 |
| `QueueMessageDecoder_TotalBelowFixedLength_Throws` | `total = 11 < 12` → `FrameProtocolException("非法的队列帧长度")` |
| `QueueMessageDecoder_NameLengthBeyondFrame_Throws` | 名称长度越过帧尾 → `"队列帧名称长度越界"` |
| `QueueMessageDecoder_TopicLengthBeyondFrame_Throws` | 主题长度越过帧尾 → `"队列帧主题长度越界"` |
| `QueueMessageDecoder_InvalidType_Throws` | 类型字节 0（不在 `Ping…Data` 范围）→ `"非法的队列消息类型"` |

`Tests/QueueMessageEncoderTests.cs`（6 条）：

| 用例 | 覆盖点 |
|---|---|
| `QueueMessageEncoder_PublishWithData_MatchesWireLayout` | 线格式逐字段比对：`1B type + 4B total + 4B nLen + name + 4B tLen + topic + data`，`total = 12 + nLen + tLen + dLen` |
| `QueueMessageEncoder_EmptyFields_WritesMinimalTotal` | 空名称/空主题/无数据 → 整帧 13 字节、`total = 12` |
| `QueueMessageEncoder_Utf8Names_WritesByteLength` | 中文名称/主题按 UTF-8 **字节数**写长度字段 |
| `QueueMessageEncoder_NullMessage_Throws` | `Encode(null)` → `ArgumentNullException` |
| `QueueCoder_Encode_DelegatesToEncoder` | 兼容入口 `QueueCoder.Encode(QueueSocketMsg)` 与编码器输出逐字节一致 |
| `QueueMessageEncoder_EncodeTo_AppendsFramesSequentially` | 同一 `PooledBufferWriter` 连续编码 3 帧 == 逐帧编码再拼接 |

非法帧用例统一断言「链上冒泡协议错误 + 不产出业务消息」，即协议错误不会以半帧形式投递给业务。

---

## 三、端到端功能验证

`SAEA.QueueSocketTest --functional` 覆盖 FT1–FT13 与 FT-Pool-* 共 73 项，结果为 `73/73 passed, 0 failed`（`EXIT=0`）。用例侧只把「订阅/接收/断开/错误」从事件回调换成了处理器：

- `TestHarness` 新增 `QueueMessageSink`（捕获入站消息并在回调后归还池化缓冲）与 `DisconnectCounter`（在 `ChannelInactiveAsync` 计数）。
- `QueueServerHarness.CreateConsumer(topic, onMessage)` / `CreateSubscriber(topic, onMessage)` 把回调装配进 `ConfigurePipeline`；断开计数改由链观测。
- FT1–FT8、FT-Pool-5 去掉事件订阅；FT11 原本用反射调 `QueueCoder.WriteFrame` 拼帧，改为 `QueueMessageEncoder.EncodeTo`（`WriteFrame` 已随迁移删除）。
- `QClient.OnMessagesSent` / `Producer.OnMessagesSent` 删除后，发送完成一律以「对端实际收到的条数」判定，不再依赖本地回调。

---

## 四、性能对比（同机 A/B，间隔数分钟）

**测量方法**：`git worktree add … HEAD`（`25c68ce9`，即 P3-2 之前）建基线工作树，跑 `--bench-queue-baseline`（旧库 + 旧事件装配、不启用阈值）；当前工作区跑 `--bench-queue`（新链、启用全部阈值）。两次均为 `-c Release`、同机同进程形态（loopback），基线工作树用完已 `git worktree remove`。

### 1. 端到端

| 场景 | 基线吞吐 | 迁移后吞吐 | 吞吐比 | 基线 B/frame | 迁移后 B/frame | 基线 GC0 | 迁移后 GC0 |
|---|---|---|---|---|---|---|---|
| S1（1 生产者 → 1 消费者） | 69 427 msg/s | 71 206 msg/s | **102.6 %** | 1 495 | 2 883 | 157 | 294 |
| S2（1 生产者 → 5 消费者） | 46 959 msg/s | 45 655 msg/s | **97.2 %** | 1 680 | 2 862 | 396 | 788 |

吞吐门禁（≥ 基线 95 %）：S1 102.6 %、S2 97.2 %，**均通过**。两次运行 `published == delivered`，无损前提成立；`--bench-queue` 的 19 项阈值断言（吞吐 ≥ 20 000 msg/s、B/frame 预算、微基准 B/op）全部 `PASS`。

### 2. 微基准（趋势，不作门禁）

| 项 | 基线 | 迁移后 |
|---|---|---|
| MicroEncode/64B | 353 ns/op / 184 B/op | 589 ns/op / 224 B/op |
| MicroEncode/1KB | 290 ns/op / 1 144 B/op | 468 ns/op / 1 184 B/op |
| MicroEncode/4KB | 284 ns/op / 4 216 B/op | 594 ns/op / 4 256 B/op |
| MicroDecode/64B | 724 ns/op / 120 B/op | 720 ns/op / 120 B/op |
| MicroDecode/1KB | 492 ns/op / 120 B/op | 489 ns/op / 120 B/op |
| MicroDecodeBatch/100x64B | 314 ns/frame / 120 B/frame | 307 ns/frame / 120 B/frame |

解码三项基本持平；编码 B/op 只多 40 B（一次 `PooledBufferWriter` 包装），但 ns/op 上升 60–110 %——编码微基准走的是 `QueueCoder.Encode` → `QueueMessageEncoder.Encode` 单帧快照，每次都要新建写入器并归还，属「每条一次分配 + 一次池往返」的口径变化，不是热路径（生产路径走 `Publish` 的批量拼接与 `Exchange` 的分类批处理）。

### 3. 每帧分配上升的来源（只记趋势，不判定）

B/frame 约翻倍、GC0 同步翻倍，与吞吐无明显相关（S1 吞吐反升）。已核实的结构性差异：

1. **入站多一层池化包装**：解码把帧体复制进 `MemoryPoolManager.RentPooled(len)` 的 `PooledBuffer`（缓冲本身来自 `ArrayPool`，`PooledBuffer` 是每条消息一个新对象），基线的 `QueueCoder.DecodeTo` 直接 `ToArray()` 成消息字段。业务侧仍必须 `Dispose()` 归还，所有权语义没变。
2. **同一帧的字节在进出两侧各占一份池空间**：入站池化缓冲 + 出站批量写入缓冲（`PooledClassificationBatcher` / `PooledBatcher`）同时存在，在途缓冲总量随链上加长而变大。
3. **池保留量不随在途量增长**：`MemoryPoolManager` 的中型池为 `ArrayPool<byte>.Create(64 KB, 100)`、大型池 `ArrayPool<byte>.Create(16 MB, 8)`，保留不能覆盖翻倍后的在途缓冲，超出部分每帧现分配，`GC.GetTotalAllocatedBytes` 直接反映出来。

即：翻倍主要来自「包装对象 + 池容量不足时的现分配」，不是消息泄漏（功能用例与 FT-Pool-* 均要求显式 `Dispose`，`QueueMsg.Dispose` → `PooledBuffer.Dispose` → `ArrayPool.Return` 链路完好）。按计划口径，本轮只记录趋势与来源，不按百分比判定；若后续要压回基线水平，方向是让分帧后的帧缓冲直接交给解码器（免复制）或调高中型池保留量，两者都会改动 `SAEA.Common` 的口径，需单独立项。

---

## 五、P3-2 条目对照

| 验收项 | 证据 |
|---|---|
| 分帧 `1 / 4 / -4 / 0` | `QueueFrameSplitter.Create` 写死 `Offset=1 / LengthFieldLength=4 / LengthAdjustment=-4 / InitialBytesToStrip=0`，小端；`HeaderLength = 5` |
| `MaxFrameLength` 不放大 | `QueueFrameSplitter.ResolveMaxFrameLength()` 取 `BaseCoder.MaxFrameLength` 并按 `[0, int.MaxValue - P_Head]` 夹逼 |
| 输入残余上限 | `QServer` / `QClient` 均为 `MaxBufferedBytes = maxFrameLength + FrameConstants.P_Head`，与分帧器推导一致 |
| 同连接串行 | `QServer` / `QClient` 的 `ChannelPipelineOptions.MaxConcurrentMessages = 1`（订阅与发布必须按到达顺序处理） |
| 编解码从 `QueueCoder` 拆出 | 编码 → `QueueMessageEncoder.WriteAsync`（`PooledBufferWriter` → `RawFrame`，另含 `Encode` / `EncodeTo` / `WriteFrameTo` 三个静态入口供批量投递与测试）；解码 → `QueueMessageDecoder.Decode`（`total < 12`、`total` 与实际帧长不一致、名称/主题长度越界、类型越界均抛 `FrameProtocolException`） |
| 链装配顺序（服务端） | `QueueMessageEncoder → OutboundSerializingHandler → QueueMessageDecoder → [使用者 ConfigurePipeline] → Dispatcher → ExceptionTerminalHandler` |
| 链装配顺序（客户端） | `QueueMessageEncoder → OutboundSerializingHandler → QueueMessageDecoder → ActiveHandler(this) → [使用者 ConfigurePipeline] → ExceptionTerminalHandler` |
| 公开事件删除 | 迁移前 12 处事件声明（`QServer` 1、`QClient` 4、`Consumer` 3、`Producer` 3、`Exchange` 1），删 11 处、全部为公开事件；仅保留 `Exchange.OnBatched` |
| 装配入口 | `QServer` / `QClient` / `Consumer` / `Producer` 各加 `Action<string, IChannelPipeline> ConfigurePipeline`；`Consumer` / `Producer` 只做透传，建链时机不变 |
| `ConfigurePipeline` 位置 | 服务端把使用者处理器插在 `Dispatcher` **之前**（解码 → 使用者 → 分发 → 链尾），使用者可先看到消息再进内置分发；客户端插在 `ActiveHandler` 之后、链尾之前 |
| 批处理语义 | `Exchange._subscribers` 值类型由 `Net.QueueCoder` 改为 `byte`；`GetSubscribeData` 去掉编码器参数；合批仍由 `PooledClassificationBatcher(5000, 100)` 完成，一批作为一次写发出，不拆分链上顺序 |
| 发送入口 | 入站回调内一律 `ctx.Pipeline.WriteAsync(...)`；`QClient.Publish` 走 `PooledBatcher` 合批后 `pipeline.WriteAsync(RawFrame)`；`ReplyCloseAsync` 对 `ChannelClosedException` 静默（对端发完关闭帧即断属正常） |
| 清理任务删除 | 删 `QClient.ClearCoderBuffer()` 与 `Consumer` 每分钟清理编码器缓冲的后台任务——链上编解码不再持有跨消息缓冲 |
| 依赖 | `SAEA.QueueSocket.csproj` 增加 `ProjectReference` 指向 `SAEA.Sockets.Pipeline`，未新增 NuGet 包 |

---

## 六、与计划的偏差

1. **删除事件数口径**：计划写「删 12 个事件（公开 11）」。实测迁移前共 12 处事件声明，删 11 处且全部是公开事件，保留 `Exchange.OnBatched`（性能保留项，批处理作为一次业务处理，不拆分链上顺序）。计划里的「12 / 11」应理解为 12 处声明、11 处删除。
2. **`QClient.OnMessagesSent` / `Producer.OnMessagesSent` 直接删除**（用户确定的口径）：发送完成不再有本地回调，测试改用「对端已收到条数」判定；`QClient.ClearCoderBuffer()` 与 `Consumer` 的每分钟清理任务同批删除。
3. **`QueueCoder` 不是整体删除**：去掉 `ICoder` 实现与两个 `NotImplementedException` 接口方法，`Encode(QueueSocketMsg)` 改为委托 `QueueMessageEncoder`；离线解码入口（`GetQueueResult` / `DecodeTo`）与 `Encode(QueueSocketMsgType,…)` / `EncodeForList` 保留，注释已写明「只供离线校验与基准测试，不参与连接收发」。仓库内除测试与基准外已无调用方。
4. **`QServer` / `QClient` 整文件重写**：为让链装配、心跳、批量发布、应答与连接收口在同一处可读，两个文件基本重写而非局部修改（`QServer` +247/−97，`QClient` +252/−73）；对外方法签名保持不变，`GetStream()` / `Dispose()` / `Publish` / `Subscribe` 等入口逐个核对过。
5. **`Program.cs` 只做入口接线**：`--functional` / `--bench-queue` / `--bench-queue-baseline` 三个非交互入口统一先起服务端、跑完再停，交互菜单保留；`--all` 未改语义。

---

## 七、未运行项

| 项 | 原因 |
|---|---|
| `dotnet run --project Src/SAEA.QueueSocketTest`（交互式菜单：s/p/c/t） | 需人工输入，自动化场景不适用；其底层流程已被 `--functional` 覆盖 |
| `--all` | 由 `--functional` + `--bench-queue` 组成，两者已分别运行并通过 |
| `Src/SAEA.QueueSocket` 中英 README 与 demo 手工 smoke | 计划把 demo 与 README 统一放 P3-4；当前 README 仍是事件式示例，属已知未同步项 |
| `SAEA.RedisSocketTest` / `SAEA.P2PTest` / 窗口 demo | 本任务范围外（前者依赖本地 Redis） |

---

## 八、遗留风险

1. **每帧分配约翻倍**（见 §四. 3）：吞吐达标、无泄漏证据，但 B/frame 与 GC0 明显高于基线；要压回基线需要改解码零拷贝或池保留量，均触及 `SAEA.Common` 的公共口径，需单独立项并做全组件回归。
2. **解码每帧复制一次帧体**：与 FileSocket / WebSocket / RPC 同口径，换取消息生命周期不依赖池化帧；业务必须 `Dispose()`，否则缓冲不归还（`--functional` 的 FT-Pool-* 覆盖了归还路径）。
3. **断链与收发异常的可观测性**：事件删除后只能经 `ConfigurePipeline` 追加处理器（`ChannelInactiveAsync` / `ChannelExceptionCaughtAsync`）观察，或看 `Exchange.OnBatched` 的投递，不再有实例级事件。
4. **链未就绪时的发布**：`QClient.Publish` 只把帧放进 `PooledBatcher`，真正写出时由 `GetPipeline(_timeOut)` 取链，取不到则丢弃该批并归还写入器（不抛异常）。这与迁移前「未连接即发失败」的可见性不同，属于既有「发送是通知性质」语义的延续，排障时需注意「发布了但没收到」可能只是链尚未就绪。
5. **`QueueCoder` 的兼容面**：`Encode(QueueSocketMsgType,…)` / `EncodeForList` / 离线解码入口已无仓库内调用方，保留仅为兼容；后续若要删需评估外部使用者。

---

## 九、下一步

按计划 §五 P3-3 处理 `SAEA.MessageSocket`：分帧 `0 / 8 / +1 / 0`，把 `MessageContext` / `Unpacker` 的包边界职责移到分帧器（解码器只做反序列化），删 8 个事件，并给真实编码帧补跨段/粘包/边界用例。