# P2-2 验收记录：`SAEA.WebSocket`（2026-11-10）

**计划：** `docs/superpowers/plans/2026-11-09-socket-pipeline-migration-plan.md`（§四 P2-2、P2-3）

**设计：** `docs/superpowers/specs/2026-11-06-socket-pipeline-migration-design.md` §5.3（协议切换屏障）、§D（协议升级）、§E（跨帧重组）、§八（长度域与分帧收口）、§4.1（装配入口）、§九（破坏面）

**范围：** `SAEA.WebSocket` 改写 4 个 + 新增适配层 10 个、`Src/SAEA.WebSocket.Tests`（新建）、`Src/SAEA.WebSocketTest/Program.cs`、`SAEA.WebSocket` 中英 README；`SAEA.MVC` 未改（`WSCoder` 的直接依赖保持可用）

---

## 一、构建与测试

| 命令 | 退出码 | 结果 |
|---|---|---|
| `dotnet build Src/SAEA.Sockets.sln -c Debug` | 0 | 0 errors；全量重编 3672 warnings（主体为 `CS1591` 缺失 XML 注释，与 B0 基线同量级），增量构建 1 warning（`SAEA.P2PTest` 存量 `CS8600`/`CS8604`） |
| `dotnet build Src/SAEA.WebSocket/… -t:Rebuild` | 0 | 0 errors；除 `SAEA.Common`/`SAEA.Sockets` 存量 `CS3021`/`CS0168`/`CS0067` 外本项目 0 告警 |
| `dotnet build Src/SAEA.WebSocketTest/… -c Debug` | 0 | 0 errors / 0 warnings |
| `dotnet test Src/SAEA.WebSocket.Tests/… -c Release` | 0 | **10 / 10**（新建工程，约 1 s） |
| `dotnet test Src/SAEA.Http.Tests/… -c Release` | 0 | 28 / 28 |
| `dotnet test Src/SAEA.Sockets.Pipeline.Tests/… -c Release` | 0 | 260 / 260 |
| `dotnet test Src/SAEA.Common.Tests/… -c Release` | 0 | 59 / 59 |
| `dotnet test Src/SAEA.MQTT.Tests/… -c Release` | 0 | 153 / 153 |
| `dotnet test Src/SAEA.FTP.Tests/… -c Release` | 0 | 13 / 13 |
| `dotnet test Src/SAEA.FileSocket.Tests/… -c Release` | 0 | 10 / 10 |

`SAEA.WebSocket.Tests` 为 `net10.0` + MSTest，只引用 `SAEA.WebSocket` 与 `SAEA.Sockets.Pipeline`，不入 sln，不依赖外部服务（TLS 用例的证书在用例内用 `SaeaCertificateFactory` 现场生成）。

---

## 二、单测覆盖（10 条）

| 用例 | 覆盖点 |
|---|---|
| `WsFrameDecoder_FragmentedMessageWithInterleavedPing_ReassemblesWholeMessage` | continuation 拼接，分片之间穿插 Ping 不打断重组 |
| `WsFrameDecoder_NewDataFrameBeforeFragmentsEnd_ThrowsInterleavedDataFrame` | 上一条分片消息未结束时又来数据帧 → `InterleavedDataFrame` |
| `WsFrameDecoder_FragmentsExceedMaxMessageSize_ThrowsMessageTooLarge` | 跨帧累计超 `MaxMessageSize` → `MessageTooLarge` |
| `WsFrameDecoder_UnmaskedFrameWhenMaskRequired_ThrowsMissingMask` | 服务端方向缺掩码 → `MissingMask` |
| `WsHandshakeSplitter_SwitchableBarrier_TakesLargerLimitOfBothPhases` | 屏障上限取 `max(握手报头上限, 帧上限)` |
| `WsHandshakeSplitter_HeaderComplete_StopsAfterCurrentThenSwitchesToFrameSplitter` | 握手块产出后 `StopAfterCurrent` 置起、`SwitchTo` 只在此时允许 |
| `WsHandshakeHandler_RequestFollowedByFrame_Writes101ThenDeliversMessage` | 服务端 101 应答后，紧随握手的首个数据帧能派发到下游 |
| `WSServer_PlainClient_ReceivesTextMessageThroughConfigurePipeline` | 服务端明文端到端：`ConfigurePipeline` 装配 → 收到文本帧 |
| `WSServer_WithTlsCertificate_ReceivesFrameFromTlsClient` | TLS 服务端接收 TLS 客户端帧 |
| `WSClient_WithTlsOptions_ReceivesServerReply` | 客户端 `Action<SaeaTlsOptions>` 重载 + 收服务端回包 |

---

## 三、全链路冒烟（`SAEA.WebSocketTest` 性能模式）

同一进程内启动 `WSServer(maxConnects: 1)` + `WSClient`，服务端 `ServerEchoHandler` 原样回包，客户端连发 100000 条文本。

| 项 | 结果 |
|---|---|
| 发送条数 | 100000 |
| 发送失败（出站被拒） | 0 |
| 等待回包超时 | 0 |
| 服务端实收条数 | 100000（序号 0 … 99999 连续，日志按条打印） |
| 总耗时 | 7.5762394 s（含最后一批回包的等待） |
| 结束日志 | `转发测试已完成，用时：7.5762394秒，回车结束测试` |

发送循环结束后补了一次有界等待（`WaitForEcho(_maxSize, 10000)`），让最后一批在途回包收完再统计，因此耗时覆盖整轮往返；未补该等待时主线程会先退出，末批回包与总耗时都看不到。

该冒烟覆盖：`ConfigurePipeline` 双向装配、服务端握手 → 切帧分帧器 → 帧解码 → `Reply` 写出、客户端握手请求/校验 → 帧解码 → 业务处理器读帧体。

---

## 四、P2-2 条目对照

| 验收项 | 证据 |
|---|---|
| 服务端迁链 | `WSServerImpl.RunPipelineAsync` 用 `UseStream` + `UseInputHandler`/`UseConnectionOpened` 接管读循环，每连接新建 `WsHandshakeSplitter`/`WsFrameSplitter`/`SwitchableSplitter` |
| 客户端迁链 | `WSClient.RunPipelineAsync` 同上，握手处理器为客户端方向，`Connect()` 等 `_handshakeDone` 后再返回 |
| 对称装配入口 | `WSServer.ConfigurePipeline` / `WSClient.ConfigurePipeline`，类型均为 `Action<string, IChannelPipeline>` |
| 分帧器只切 wire frame | `WsFrameSplitter` 只按 RFC 6455 帧头算长度，不做掩码/分片语义 |
| 解码器持分片状态 | `WsFrameDecoder` 存 `_fragmentType`/`_fragment`，`MaxMessageSize` 在解码器执行（默认 64MB） |
| 分片间允许控制帧 | Ping/Pong 立即产出，不打断重组（有单测） |
| 拒绝数据帧交织 | `InterleavedDataFrame`（有单测） |
| 握手块不交下游 | `WsHandshakeSplitter` 产出后停切，`WsHandshakeHandler` 校验后 `SwitchTo(frameSplitter)`，握手块本身不 `FireChannelRead` |
| 协议切换屏障 | `SwitchableSplitter` 上限取 `max(64KB, 帧上限)`，每次连接新建；漏切由 `SplitterSwitchTimeout` 收口 |
| `WSCoder` 直接依赖不受影响 | `SAEA.MVC/MVCWebSocketConnection.cs` 仍直接用 `WSCoder`（`RequireMask = true`、`MaxMessageSize = MaxQueuedBytes`）；`WSCoder` 本轮的改动只有使用顺序整理，语义未变；含 MVC 的 sln 构建 0 错误 |
| 内置心跳 | 服务端 `SessionHandler` / 客户端 `ClientSessionHandler`：Ping→Pong、Close→回 Close 后收口；两者都不进业务处理器，数据帧与 Pong 才向下派发 |
| 池化语义 | `WSProtocal.Content` 仍是池化缓冲的只读视图，跨回调使用需复制；`WsFrameDecoder` 产出消息按 `MemoryPoolManager.Rent/Return` 管理，不做整帧复制 |
| TLS 仍由 `WithSsl` | 服务端沿用构造函数的 pfx 参数走 `SocketOptionBuilder.WithSsl`；客户端新增 4 个 `Action<SaeaTlsOptions>` 重载 |
| 事件删除 | `WSServer` 3 个、`WSServerImpl` 3 个、`WSClient` 5 个共 11 处声明删除，`IWSServer` 的 3 处接口声明同步删除 |

---

## 五、与计划的偏差（已回填说明）

1. **「删除 11 个事件」的口径**：计划写 11，实际删掉 11 处实现声明（`WSServer` 3 + `WSServerImpl` 3 + `WSClient` 5），另加 `IWSServer` 的 3 处接口声明。对外可观察的独立事件是 8 个（服务端 3 + 客户端 5），`WSServer` 与 `WSServerImpl` 是同一组事件的转发层。
2. **握手完成后的帧转发（缺陷修复）**：初版 `WsHandshakeHandler` 在 `_completed` 之后对继续到达的 `PooledMessage` 抛 `UnexpectedHandshakeFrame`，当对端把首个数据帧与握手请求写在同一个 TCP 段里时该帧被丢弃（`WsHandshakeHandler_RequestFollowedByFrame_Writes101ThenDeliversMessage` 复现）。已改为在 `_completed` 时直接 `FireChannelReadAsync(msg)` 交给下游解码器，并从 `WebSocketProtocolErrors` 删除该 token。
3. **连接/断开通知（D1 = A1）**：不再提供 `OnConnected`/`OnDisconnected`，改用处理器里的 `ChannelActiveAsync`/`ChannelInactiveAsync`，服务端 `Clients` 快照同步维护。`ChannelActiveAsync` 是传输级通知，服务端早于握手完成；需要「握手完成」的确切时机以收到首帧为准，README 已写明差异。
4. **客户端 TLS 重载（D2 = B1）**：新增 `Action<SaeaTlsOptions>` 重载（`string url`/`Uri` × 带/不带完整参数），转发给 `SocketOptionBuilder.WithSsl`；既有构造重载签名不变。
5. **控制帧语义（D3）**：保留自动 Ping→Pong 与 Close→回 Close 后断开；数据帧与 Pong 交下游，Close 不进链。
6. **池化语义（D4）**：`WSProtocal` 的 `Content` 继续引用池化缓冲，由 `BodyLength` 触发归还；解码器不做整帧复制，代价是业务侧跨回调使用必须先复制，README 与 demo 注释均已写明。
7. **出站预算在 demo 侧节流**：性能冒烟首版一次性灌 10 万条，客户端出现 `OutboundSerializingHandler` 的 `MaxPendingWrites=256` 拒写日志（属出站背压设计，不是迁移缺陷）。demo 改为 128 条窗口逐步推进并统计超时，最终 0 失败、0 超时；README 的 FAQ Q9 给出同样的节流写法。
8. **`ProjectReference`**：`SAEA.WebSocket.csproj` 增加指向 `SAEA.Sockets.Pipeline` 的工程引用（经用户确认），未新增 NuGet 包、未改包版本。
9. **`WSCoder` 只整理 using**：P2-2 期间该文件仅调整 `using` 顺序并移除未使用的 `System.Threading.Tasks`，无行为改动；`修改标记` 已按本次修改覆盖。
10. **banner 校正**：`WSServer.cs` 的 `唯一标识` 曾被改成新 GUID，已恢复原值 `795e4b86-…` 与原 `描述`；P2-2 涉及文件的 `修改时间` 统一按实际修改时刻回填，不再出现晚于本机时间的时间戳。

---

## 六、未运行项

| 项 | 原因 |
|---|---|
| README 中的 `EchoHandler` / `JsonHandler` / `LifecycleHandler` 示例 | 文档示例，未编译进任何工程；示例里用到的 `ConfigurePipeline`、`IChannelHandlerContext`、`WSProtocal`、`Reply`/`Disconnect` 签名已逐项核对 |
| 浏览器侧（真 Chrome/Edge）握手与收发 | 本轮用 `WSClient` + 手写握手报文验证；浏览器差异（`Origin`、`Sec-WebSocket-Extensions`、代理）不在本轮范围 |
| `permessage-deflate` 扩展、子协议服务端回显 | 组件未实现扩展协商；子协议仍按原逻辑在请求头携带，服务端不校验 |
| 超大消息（>64MB）与 1000+ 并发连接压测 | 属容量/性能验证；`MaxMessageSize` 超限路径已由单测覆盖 |
| clean rebuild 告警基线复核 | 只做增量构建与 WebSocket 单工程 rebuild；全量告警基线见 `baseline-2026-11-09.md` |

---

## 七、遗留风险

1. `WSClient.Send` 在出站预算打满时只记录失败日志、不向调用方抛异常；批量发送必须自行做回包节流，否则消息会静默丢失（README FAQ Q9 已提示）。
2. 握手阶段没有独立的超时收口：若对端连上后一个字节都不发，只能由传输层或调用方超时处理，`WebSocketProtocolErrors.HandshakeTimeout` 目前只供显式配置使用。
3. 服务端 `ChannelActiveAsync` 早于握手完成，业务若在此刻直接 `Reply` 会因 `WsHandshakeState` 未完成而抛 `ChannelClosedException`；需要「可发数据」的时机应以首帧或客户端 `Connect()` 返回为准。
4. `WsFrameDecoder` 分片累积使用池化缓冲并按需扩容，单条 64MB 上限的消息仍会瞬时占用接近该上限的内存；多连接同时传大消息时需下调 `MaxMessageSize`。

---

## 八、下一步

按计划 §五进入 P3（`RPC` + `QueueSocket` + `MessageSocket`）：先 `RPC`（`1 / 4 / -4 / 0` 分帧、`MaxConcurrentMessages = 1`），再 `QueueSocket`（保留 `OnBatched` 批处理语义），最后 `MessageSocket`（包边界职责移到分帧器）。