# P2-1 验收记录：`SAEA.FileSocket`（2026-11-10）

**计划：** `docs/superpowers/plans/2026-11-09-socket-pipeline-migration-plan.md`（§四 P2-1）

**设计：** `docs/superpowers/specs/2026-11-06-socket-pipeline-migration-design.md` §八. A（长度域帧配置）、§八. F（分帧收口）、§4.1（装配入口）、§4.6（资源上限）、§九（破坏面）

**范围：** `SAEA.FileSocket` 源码 4 个 + 新增适配层 6 个、`Src/SAEA.FileSocket.Tests`、中英 README；`Src/SAEA.FileTest` demo 未改代码

---

## 一、构建与测试

| 命令 | 退出码 | 结果 |
|---|---|---|
| `dotnet build Src/SAEA.Sockets.sln -c Debug` | 0 | 0 errors / 0 warnings（增量；P1 记录中的 `CS0168` 属 MVCTest 存量，本轮未重编） |
| `dotnet build Src/SAEA.FileTest/SAEA.FileTest.csproj -c Debug` | 0 | 0 errors / 0 warnings |
| `dotnet test Src/SAEA.FileSocket.Tests/… -c Release` | 0 | **10 / 10**（新建工程） |
| `dotnet test Src/SAEA.Http.Tests/… -c Release` | 0 | 28 / 28 |
| `dotnet test Src/SAEA.Sockets.Pipeline.Tests/… -c Release` | 0 | 260 / 260 |
| `dotnet test Src/SAEA.Common.Tests/… -c Release` | 0 | 59 / 59 |
| `dotnet test Src/SAEA.MQTT.Tests/… -c Release` | 0 | 153 / 153 |
| `dotnet test Src/SAEA.FTP.Tests/… -c Release` | 0 | 13 / 13 |

`SAEA.FileSocket.Tests` 为 `net10.0` + MSTest，只引用 `SAEA.FileSocket` 与 `SAEA.Sockets.Pipeline`，不加入 sln，不依赖 demo 与外部服务。10 条用例：

- 跨段：`FileMessageDecoder_SplitAcrossReads_ProducesSingleMessage`（只写前 4 字节时不产出消息）
- 粘包：`FileMessageDecoder_TwoFramesInOneRead_ProducesTwoMessagesInOrder`
- 边界长度：`…_ZeroBodyNonHeartFrame_ProducesEmptyMessage`、`…_SingleByteBody_KeepsBoundary`、`…_BodyAtMaxLength_ProducesFullMessage`
- 心跳丢弃：`FileMessageDecoder_EmptyHeartFrame_ProducesNoMessage`
- 出站：`FileMessageEncoder_WithBody_WritesFullWireFrame`、`…_EmptyBody_WritesHeaderOnly`、`…_RawFrame_PassesThroughUnchanged`
- 发送完成时序：`ClientSendFileTests.SendFile_WhenSendCompletes_ReportsFullLengthAndDeliversAllBytes`（服务端暂停收包时完成回调不得提前触发，放行后 `Client.Out` 等于文件长度且收端字节数一致）

---

## 二、全链路冒烟（`SAEA.FileTest`）

`dotnet SAEA.FileTest.dll` 作为接收端启动内置 `FileTransfer`（默认端口 39654），从标准输入执行 `send 127.0.0.1 <file>`，发送与接收在**同一进程**内完成（demo 自身的 `FileTransfer` 既监听又发送）。

| 项 | 结果 |
|---|---|
| 测试文件 | 3,145,728 字节（3 MB 随机数据） |
| 源 SHA256 | `9DBEF588BEC40843B4141323E754528984F53824ED25A9B3C7BB472C05EE782D` |
| 收端 SHA256 | `9DBEF588BEC40843B4141323E754528984F53824ED25A9B3C7BB472C05EE782D` |
| 字节一致 | 是 |
| 日志 | `正在连接IP:127.0.0.1...` → `成功连接到IP…正在准备发送文件` → `文件接收完毕 <路径>` → `发送文件已完成` |

该冒烟同时覆盖：`ConfigurePipeline` 装配接收处理器、请求帧解码 → `Allow` 应答 → 文件块逐帧落盘 → 完成判定。

---

## 三、P2-1 条目对照

| 验收项 | 证据 |
|---|---|
| 长度域 `0 / 8 / +1 / 0`（保留完整帧） | `FileFrameSplitter.Create` 参数写死 `Offset=0 / LengthFieldLength=8 / LengthAdjustment=+1 / InitialBytesToStrip=0`；测试用同一配置 |
| `MaxFrameLength` 不放大 | `FileFrameSplitter.ResolveMaxFrameLength()` 取 `BaseCoder.MaxFrameLength`，并按 `FrameDecoder` 的 `[0, int.MaxValue - P_Head]` 夹逼 |
| 单帧上限与缓冲上限一致 | `Server`/`Client` 的 `MaxBufferedBytes = MaxFrameLength + FrameConstants.P_Head`，显式给出，避免自动推导在默认上限下溢出 int |
| `MaxConcurrentMessages = 1` | `Server`/`Client` 的 `ChannelPipelineOptions` 均为 1，保证文件块顺序落盘 |
| 解码器校验长度字段 | `FileMessageDecoder` 比对声明长度与实际帧体，不一致抛 `FrameProtocolException` |
| 池化帧不泄漏到业务 | 解码时把帧体复制成独立数组（`MemoryMarshal.TryGetArray` 命中时直接写，未命中再 `ToArray`） |
| 心跳语义对齐 | `bodyLength == 0 && type == Heart` 直接丢弃；零长度非心跳帧仍产出消息（有用例） |
| 公开事件删除 | `Server.OnRequested` / `OnFile` / `OnError` 三个公开事件已删，改为 `ConfigurePipeline`（`Action<string, IChannelPipeline>`）；内部订阅 `_client.OnClientReceiveSpan`、`_receiver.OnError` 同步移除 |
| 主动应答入口 | `Server.Allow(id)` / `Refuse(id)` 保留签名，内部经 `_pipelines[id].WriteAsync(...)` 写出 |
| 客户端建链就绪 | `Client.Connect()` 等 `_pipelineReady`（上限 5000ms），避免首帧写在没有链的连接上 |
| 无整帧 `ToArray()` 收发路径 | 发送侧 `FileMessageEncoder` 直接 `protocal.WriteTo(writer)`；接收侧只在解码时把帧体复制成消息体（消息生命周期要求） |

---

## 四、与计划的偏差（已回填说明）

1. **删除事件数**：计划写「删除 5 个事件」，实际公开事件是 `Server` 的 3 个；另 2 个是组件内部的接收回调订阅（`_client.OnClientReceiveSpan`、`_receiver.OnError`），随迁移一起移除，不影响公开 API 面。
2. **`FileTest` demo 未改源码**：demo 只用 `FileTransfer` 的 `OnReceiveEnd` / `OnDisplay`，这两个传输级事件保留；处理器链封装在 `FileTransfer` 构造函数里（`_receiver.ConfigurePipeline`），因此无需改 demo。`README` 已给出直接使用 `Server.ConfigurePipeline` 的示例。
3. **接收处理器放在 `FileTransfer` 内部**：`ReceiveHandler` 是 `FileTransfer` 的私有嵌套类，`RequestSend` → `OnRequested`，`BigData` → `OnFile`，其余消息 `FireChannelReadAsync` 交下游。
4. **新增 `ProjectReference`**：`SAEA.FileSocket.csproj` 增加 `SAEA.Sockets.Pipeline` 工程引用（与 `SAEA.Http` 一致），未新增 NuGet 包，已获用户同意。
5. **`UseConnectionOpened` 成对配置**：`SocketOptionBuilder.Build()` 要求 `UseInputHandler` 与 `UseConnectionOpened` 成对；文件传输链只用连接标识，故补了一个空实现的 `OnConnectionOpened`，未引入连接级状态。
6. **发送完成回调时序按审查结论修正**：审查发现迁移后 `Client.SendFile` 在收到「允许」时即回调并把 `_autoResetEvent` 放开，与基线「整份文件写出后回调」不一致，会让 demo 提前打印完成、并允许同一连接上的两轮文件块交错。已改为 `SendFileAndCompleteAsync` 在发送任务 `finally` 里回调并放开信号量（回调抛异常也保证 `Set`），对外签名不变；新增 `ClientSendFileTests` 用「服务端暂停收包」构造发送未完成的窗口，回退到旧时序时该用例失败（`Assert.IsFalse 失败。服务端暂停收包时发送尚未结束，完成回调不得提前触发。`），修正后连续 3 次运行均 10/10 通过。

---

## 五、未运行项

| 项 | 原因 |
|---|---|
| `README` 中的 `LoggingHandler` / `RejectLargeFileHandler` 示例 | 文档示例，未编译进任何工程；示例 API 与当前 `Server` / `IChannelPipeline` 签名逐项核对过 |
| 断点续传（非 0 `offset`）端到端冒烟 | 需先有半截文件与配套发送端脚本，本轮只验证 `offset = 0` 全链路；`offset` 由原有 `SendFile` 参数透传，逻辑未改 |
| 大文件（≥ 2GB）与多连接并发 | 本轮用 3MB 单连接验证语义；并发与超大文件属性能/容量验证，不在 P2-1 功能验收内 |
| clean rebuild 告警数 | 只做增量构建；告警基线见 B0（约 3.8k CS1591） |

---

## 六、遗留风险

1. `Client.Connect()` 等待链就绪有 5000ms 上限；超时后不抛错，首次发送会以 `ChannelClosedException` 被 `TrySendCoreAsync` 记录为调试日志。连接极慢时业务侧只看到「没发出去」。
2. `FileTransfer` 的接收状态字段（`_current` / `_length` / `_fileStream`）不支持同一实例并发接收多个文件；同一连接串行（`MaxConcurrentMessages = 1`）保证了单文件顺序，多连接并发仍需业务自行隔离实例。
3. 心跳线程在未连接时以 1ms 间隔轮询（沿袭原有实现），本轮未改；若要降 CPU 占用需另行调整。
4. 解码时帧体复制成独立数组是有意取舍：换取消息生命周期不依赖池化帧，代价是每帧一次 body 拷贝（每帧分配趋势已在 B0 记录口径下说明来源）。

---

## 七、下一步

按计划 §四 P2-2 处理 `SAEA.WebSocket`；其目录中未提交的 TLS 改动已由用户手动提交，P2-2 以该提交为起点。随后 P2-3 汇总 demo 与 README。