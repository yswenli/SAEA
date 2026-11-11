# P3-3 验收记录：`SAEA.MessageSocket`（2026-11-11）

**计划：** `docs/superpowers/plans/2026-11-09-socket-pipeline-migration-plan.md`（§五 P3-3）

**设计：** `docs/superpowers/specs/2026-11-06-socket-pipeline-migration-design.md` §5.3（连接处置）、§D（跨帧重组）、§E（协议错误）、§八（长度域帧配置）、§九（破坏面）；MessageSocket 行见 §迁移矩阵

**范围：** `SAEA.MessageSocket` 改写 4 个（`MessageServer` / `MessageClient` / `Model/Communication/ChatMessage` / `ChatMessageType`）+ 删除 2 个（`Model/MessageContext` / `Model/MessageUserToken`）+ 新增适配层 6 个（`Src/SAEA.MessageSocket/Net/Pipeline/`）+ `SAEA.MessageSocket.csproj` 增加 `ProjectReference` + 新建 `Src/SAEA.MessageSocket.Tests`（15 条）+ `Src/SAEA.MessageTest` 改链装配并新增 `--functional` 入口。中英 README 与其余 demo 归 P3-4。

---

## 一、构建与测试

| 命令 | 退出码 | 结果 |
|---|---|---|
| `dotnet build Src/SAEA.Sockets.sln -c Debug` | 0 | 0 errors / 3672 warnings，耗时 16.20 s（增量；主体为存量 `CS1591`，与 B0 同量级） |
| `dotnet build Src/SAEA.MessageSocket/SAEA.MessageSocket.csproj -c Debug` | 0 | 0 errors / 0 warnings |
| `dotnet test Src/SAEA.MessageSocket.Tests/… -c Release` | 0 | **15 / 15**（新建工程，未入 sln） |
| `dotnet run --project Src/SAEA.MessageTest -c Debug -- --functional` | 0 | **全部通过**，`EXIT=0` |
| `dotnet test Src/SAEA.Common.Tests/… -c Release` | 0 | 59 / 59 |
| `dotnet test Src/SAEA.Sockets.Pipeline.Tests/… -c Release` | 0 | 260 / 260 |
| `dotnet test Src/SAEA.Http.Tests/… -c Release` | 0 | 28 / 28 |
| `dotnet test Src/SAEA.FileSocket.Tests/… -c Release` | 0 | 10 / 10 |
| `dotnet test Src/SAEA.WebSocket.Tests/… -c Release` | 0 | 10 / 10 |
| `dotnet test Src/SAEA.RPC.Tests/… -c Release` | 0 | 12 / 12 |
| `dotnet test Src/SAEA.MQTT.Tests/… -c Release` | 0 | 153 / 153 |
| `dotnet test Src/SAEA.FTP.Tests/… -c Release` | 0 | 13 / 13 |
| `dotnet test Src/SAEA.QueueSocket.Tests/… -c Release` | 0 | 14 / 14 |

`SAEA.MessageSocket.Tests` 为 `net10.0` + `LangVersion 10.0` + MSTest（`Microsoft.NET.Test.Sdk 17.8.0`、`MSTest.* 3.1.1`），只引用 `SAEA.MessageSocket` 与 `SAEA.Sockets.Pipeline`，不加入 sln，不依赖外部服务。

本轮改动只落在 `SAEA.MessageSocket`、其测试工程与 `SAEA.MessageTest`；上表后九行是回归确认，全部与迁移前同数。

---

## 二、新增用例（15 条）

`Tests/MessageDecoderTests.cs`（9 条，走真实 `ChannelPipeline` + 内存字节流）：

| 用例 | 覆盖点 |
|---|---|
| `MessageDecoder_SplitAcrossReads_ProducesSingleMessage` | 跨段：先写 4 字节（连 9 字节帧头都未到齐）不产出，补发剩余字节后恰好产出 1 条 |
| `MessageDecoder_TwoFramesInOneRead_ProducesTwoMessagesInOrder` | 粘包：两帧一次写入 → 两条消息且类型/内容不串 |
| `MessageDecoder_HeartbeatFrame_ProducesNoMessage` | 心跳帧（`Heart`、body 0）只保活，不投递业务消息 |
| `MessageDecoder_EmptyBody_ProducesNoMessage` | 长度字段 0 的普通帧无 body 可解，不产出消息 |
| `MessageDecoder_BodyAtMaxFrameLength_IsAccepted` | 边界上限：帧体恰好等于 `maxFrameLength` 时正常解出 |
| `MessageDecoder_BodyAboveMaxFrameLength_Throws` | 帧体超上限 → 分帧器抛 `FrameProtocolException("非法的数据帧长度")` |
| `MessageDecoder_NegativeBodyLength_Throws` | 长度字段为负 → 分帧器抛 `FrameProtocolException("非法的数据帧长度")` |
| `MessageDecoder_InvalidProtobufBody_Throws` | 帧体不是合法 protobuf → `"消息帧反序列化失败"` |
| `MessageDecoder_BodyCopied_SurvivesFrameRelease` | 所有权：2 KB 内容取出后跨收口、帧缓冲释放仍完整可读 |

`Tests/MessageEncoderTests.cs`（4 条）：

| 用例 | 覆盖点 |
|---|---|
| `MessageEncoder_TextMessage_MatchesWireLayout` | 线格式：`[8B bodyLength 小端][1B type][body]`，长度只计帧体 |
| `MessageEncoder_ThenDecoder_RoundTripsFields` | 编码 → 链上解码往返，类型/内容/时间戳一致 |
| `MessageEncoder_EmptyContent_RoundTripsEmptyString` | 空内容编成合法帧，解码还原为空字符串 |
| `MessageEncoder_NullMessage_Throws` | `Encode(null)` → `ArgumentNullException` |

`Tests/MessageIntegrationTests.cs`（2 条，本机回环真实 socket）：

| 用例 | 覆盖点 |
|---|---|
| `MessageServer_PrivateMessage_ForwardsToReceiverThroughConfigurePipeline` | 登录 → 私信：服务端使用者处理器可见请求，接收方拿到填入发送者/时间的私信，发送方收到 `PrivateMessageAnswer` |
| `MessageServer_ChannelMessage_FansOutToOtherSubscribers` | 订阅 → 频道广播：同频道其他成员收到填入发送者/时间的频道消息，发送者本人不回发 |

非法帧用例统一断言「链上冒泡协议错误 + 不产出业务消息」，即协议错误不会以半帧形式投递给业务。

---

## 三、端到端功能验证

`SAEA.MessageTest --functional` 覆盖登录、私信、频道订阅与广播、建群/入群/群消息四段，输出 `SAEA.Message 功能自测全部通过。`、`EXIT=0`。

用例侧只把事件订阅换成处理器：

- 服务端 `ServerObserver` 观察 `ChannelActiveAsync` / `ChannelInactiveAsync` / `ChannelExceptionCaughtAsync`，异常继续 `ctx.FireExceptionCaughtAsync(ex)` 交给链尾。
- 客户端 `ClientObserver` 在 `ChannelReadAsync` 里按 `ChatMessageType` 分发到状态对象，并把应答位（订阅成功、建群成功、入群成功）记在状态里。
- `LinkTest` / `ChannelMsgTest` / `PerformanceTest` 的计数同样改为处理器内累加，不再订阅实例事件。

---

## 四、P3-3 条目对照

| 验收项 | 证据 |
|---|---|
| 分帧 `0 / 8 / +1 / 0` | `MessageFrameSplitter.Create` 写死 `Offset=0 / LengthFieldLength=8 / LengthAdjustment=+1 / InitialBytesToStrip=0`，小端；`HeaderLength = 9` |
| `MaxFrameLength` 不放大 | `MessageFrameSplitter.ResolveMaxFrameLength()` 取 `BaseCoder.MaxFrameLength`，并按既有 `FrameDecoder` 规则夹逼到 `[P_Head, int.MaxValue - P_Head]` |
| 输入残余上限 | `MessageServer` / `MessageClient` 均为 `MaxBufferedBytes = maxFrameLength + FrameConstants.P_Head`，与分帧器推导一致 |
| 同连接串行 | `MessageServer` / `MessageClient` 的 `ChannelPipelineOptions.MaxConcurrentMessages = 1`（登录、订阅、转发必须按到达顺序处理） |
| 线格式与心跳 | 类型字节固定在帧头第 9 字节（`P_LEN`）；`SocketProtocalType.ChatMessage` 承载业务，`Heart` 只保活；`MessageClient.WriteHeartAsync` 用 `BaseSocketProtocal(...).WriteTo(writer)` 生成完整心跳帧后包 `RawFrame` |
| 包边界职责移出 `MessageContext` | 删除 `Model/MessageContext.cs` 与 `Model/MessageUserToken.cs`；分帧由 `LengthFieldSplitter` 完成，`MessageDecoder` 只做长度一致性校验与 protobuf 反序列化 |
| 解码器产出类型 | `ChatMessage` 与 `ChatMessageType` 改为 `public`，解码器直接产出 `ChatMessage`，下游按 RPC / QueueSocket 同口径消费 |
| 解码后载荷所有权 | `MessageDecoder` 把帧体复制成独立数组后再 `PBDeserialize`，消息生命周期不依赖池化帧，下游无需 `Dispose` |
| 公开事件删除 | 迁移前共 8 处公开事件：`MessageServer` 3（`OnAccepted` / `OnDisconnected` / `OnError`）、`MessageClient` 5（`OnConnected` / `OnPrivateMessage` / `OnChannelMessage` / `OnGroupMessage` / `OnError`），全部删除 |
| 装配入口 | `MessageServer` / `MessageClient` 各加 `Action<string, IChannelPipeline> ConfigurePipeline`；`ConfigurePipeline` 在每条连接建链时执行一次 |
| 链装配顺序（服务端） | `MessageEncoder → OutboundSerializingHandler → MessageDecoder → InboundDispatcher（内置处理）→ [使用者 ConfigurePipeline] → ExceptionTerminalHandler` |
| 链装配顺序（客户端） | `MessageEncoder → OutboundSerializingHandler → MessageDecoder → InboundDispatcher（登记登录状态）→ ActiveHandler(this) → [使用者 ConfigurePipeline] → ExceptionTerminalHandler` |
| 入站下传语义 | `InboundDispatcher` 先完成内置处理，再 `ctx.FireChannelReadAsync(chatMessage)` 下传，使用者处理器看到的是「已处理过」的消息，只用于审计与统计；非 `ChatMessage` 直接下传 |
| 发送入口 | 入站回调内一律 `ctx.Pipeline.WriteAsync(...)`（回包与转发都是整链写）；未连接时按底层发送语义失败并记录日志，不阻塞同连接后续消息 |
| 连接与异常可见性 | 删除事件后改由 `ConfigurePipeline` 追加处理器观察：连接建立/断开看 `ChannelActiveAsync` / `ChannelInactiveAsync`，异常看 `ChannelExceptionCaughtAsync` |
| 依赖 | `SAEA.MessageSocket.csproj` 增加 `ProjectReference` 指向 `SAEA.Sockets.Pipeline`，未新增 NuGet 包 |
| 传输模式 | TCP 统一 `.UseStream()`（替换原 `.UsePacket(...)`），不再使用 span 接收事件 |

---

## 五、与计划的偏差

1. **删除范围比计划多两项**：`MessageContext` / `MessageUserToken` 的包边界与登录时间职责被链与 `InboundDispatcher` 接管后已无调用方，随迁移删除；计划只写「把 `MessageContext` / `Unpacker` 的包边界职责移到分帧器」，未明确删除文件。
2. **`ChatMessage` / `ChatMessageType` 改为 `public`**（用户确定的口径）：解码器直接产出 `ChatMessage`，下游与测试都不再需要反射或包装类型；两文件的 `public` 成员同步补齐 XML 注释，类型说明改为完整中文句子。
3. **`InboundDispatcher` 排在 `ConfigurePipeline` 之前**：服务端「先内置处理、再下传」，使用者处理器可用于审计/统计但不能拦截内置分发；客户端 `InboundDispatcher` 只负责登记登录状态，随后 `ActiveHandler` 触发连接通知。这与 RPC / QueueSocket 的「使用者处理器在内置分发之前」不同，原因是本组件的内置分发包含私信/频道/群组的落库与转发，若先用者后内置，使用者侧看到的发送者/时间字段可能还是未填充值。
4. **`MessageClient.ID` 实现改为直接读底层 socket 的本地端点**：与 `SaeaClientSocket` 建链时写入 `UserToken.ID` 的口径一致（客户端取本地地址、服务端取对端地址），避免登录前的空值回退到远端地址（多客户端会得到同一个远端地址）；对外语义仍是「服务端定位连接的标识」。
5. **`MessageClient.WriteHeartAsync` 保留心跳任务**：心跳帧不入 `ChatMessage` 解码路径（解码器遇 `Heart` 直接返回），发送侧用 `BaseSocketProtocal` 生成原始帧后包 `RawFrame` 走链；心跳失败按原语义退出循环。
6. **`SAEA.MessageTest` 整文件重写**：原 443 行的事件式 demo 全部改为链装配，并新增 `--functional` 非交互入口；交互菜单 `S/F/L/T/P` 与组合键保持不变。

---

## 六、未运行项

| 项 | 原因 |
|---|---|
| `dotnet run --project Src/SAEA.MessageTest`（交互式菜单：S/F/L/T/P） | 需人工输入，自动化场景不适用；其核心流程已由 `--functional` 与集成用例覆盖 |
| 连接压测 6 万条（`LinkTest`）与百万条私信压测（`PerformanceTest`） | 长时压测，需人工触发；本轮只做功能 smoke 与回归 |
| `Src/SAEA.MessageSocket` 中英 README | 计划把 demo 与 README 统一放 P3-4；当前 README 仍是事件式示例，属已知未同步项 |
| 共享输出目录 | 未使用；各工程按默认 `bin/obj` 构建 |

---

## 七、遗留风险

1. **`ConfigurePipeline` 是属性而非事件**：只在建链时读取一次，连接建立后再赋值不会补装到已存在连接；使用者需在 `Start()` / `Connect()` 之前装配。
2. **入站下传的「已处理」语义**：使用者处理器不能阻止内置分发（私信转发、频道广播、群组落库已经发生）；若要拦截必须改链序，会影响服务端转发时序，需单独立项。
3. **无使用者处理器时的链尾回收日志**：内置处理完成后消息仍会下传到链尾，未再被消费时链尾按既有规则回收并打 `[Debug]` 日志；这是链的通用行为，不是消息泄漏。
4. **心跳与登录状态的可观测性**：事件删除后，登录成功只能由 `LoginAnswer` 到达驱动，没有实例级回调；`Logined` 仍是公开状态位，等待应答需自行轮询或用处理器记录。
5. **`MessageContext` / `MessageUserToken` 删除**：仓库内已无引用（构建与回归确认），但它们是公开类型，外部使用者若直接引用会编译失败；包版本由用户统一发布，本轮未改。

---

## 八、下一步

按计划 §五 P3-4 收口：`RPCTest` / `QueueSocketTest` / `MessageTest` 三处 demo 的链装配统一核对，补 `QueueSocketTest --functional` 自动跑；改写 `SAEA.RPC` / `SAEA.QueueSocket` / `SAEA.MessageSocket` 三包中英 README 的事件式示例，并同步根目录 README 的迁移说明。