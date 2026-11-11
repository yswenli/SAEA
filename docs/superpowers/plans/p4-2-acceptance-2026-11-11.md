# P4-2 验收记录：FTP 控制通道迁移到处理器链（2026-11-11）

**计划：** `docs/superpowers/plans/2026-11-09-socket-pipeline-migration-plan.md`（§六 P4 第 2 步）

**方案：** `docs/superpowers/plans/2026-11-11-p4-control-channel-plan.md`（D1–D4 与验收命令）

**提交：** `68701195 refactor(ftp): 控制通道迁移到 SAEA.Sockets.Pipeline`（20 files，+2783/−323）；后续修正单独成提交。

**范围：** `FTPServer` 控制连接从 `TcpListener` 改为 SAEA 流式服务器 + 处理器链，`FtpControlConnection` 从 `TcpClient` 同步读写改为处理器链 + 同步桥接；`FTPClient` 删除两个公开生命周期事件、新增 `ConfigurePipeline`；`SAEA.FTP.csproj` 增加 `SAEA.Sockets.Pipeline` 项目引用；中英 README 同步。**被动模式（PASV/EPSV）数据连接本步仍用 `TcpClient`**，留待第 3 步。

---

## 一、链路装配

| 侧 | 链路顺序（自出站首节点到链尾） |
|---|---|
| 服务端 | `FtpReplyEncoder` → `OutboundSerializingHandler` → `FtpCommandDecoder` → `FtpServerCommandHandler` → 使用者 `ConfigurePipeline` 扩展 → `ExceptionTerminalHandler` |
| 客户端 | `FtpCommandEncoder` → `OutboundSerializingHandler` → `FtpReplyDecoder` → `FtpControlReplyHandler` → 使用者 `ConfigurePipeline` 扩展 → `ExceptionTerminalHandler` |

- 两侧都用 `SocketOptionBuilder().SetSocket(Tcp).UseStream().UseConnectionOpened(...).UseInputHandler(...)`；`UseConnectionOpened` 与 `UseInputHandler` 成对配置是 `SocketOptionBuilder` 的硬校验。
- 分帧统一 `LineSplitter(8 * 1024)`，`MaxBufferedBytes = 8 * 1024 + FrameConstants.P_Head`（`P_Head = P_LEN(8) + P_Type(1) = 9`）。8KB 上限按**整帧（含换行符）**计；超出走 `FrameProtocolException`，由执行器收口，服务端不会无限累积。
- `MaxConcurrentMessages = 1`：控制命令严格一问一答，同一会话必须按到达顺序处理。
- `FtpCommandDecoder` 丢弃空行，保留原始命令文本 `ClientRequest.Raw` 仅用于日志与审计；`FtpReplyDecoder` 持有跨帧多行状态（首行第 4 字符为 `-` 进入多行，直到「同状态码 + 空格」开头才收口）。

## 二、服务端会话生命周期

| 阶段 | 位置 | 动作 |
|---|---|---|
| 接受连接 | `FTPServer.OnConnectionOpened(ChannelInfo)` | 建 `ServerSession(channelId)`，登记 `_sessions[channelId]`，`SessionManager.SetNoTimeout(id, true)`（旧实现控制连接无空闲超时，无流量时不主动踢） |
| 装配 | `FTPServer.ConfigureConnection(channelId, pipeline)` | 装内置链，再调用使用者回调，最后追加 `ExceptionTerminalHandler` |
| 激活 | `FtpServerCommandHandler.ChannelActiveAsync` | `session.AttachPipeline(ctx.Pipeline)` 后写 `220 Welcome to SAEA.FTPServer!` |
| 命令 | `FtpServerCommandHandler.ChannelReadAsync` | 取会话后 `session.Handle(request)` **内联同步执行**，再 `FireChannelReadAsync` 下传；`session.QuitRequested` 为真时 `CloseAsync("ftp-quit")` |
| 失活 | `FtpServerCommandHandler.ChannelInactiveAsync` | `session.MarkClosed()` → `_server.RemoveSession(session)`（后者递减 `_userSessionCount` 并 `Dispose`） |
| 异常 | `FtpServerCommandHandler.ChannelExceptionCaughtAsync` | `IOException`/`ObjectDisposedException`/`ChannelClosedException` 静默，其余记 `OnLog("FTP会话异常", ex)`，随后 `CloseAsync` |

`ServerSession.MarkClosed()` 置 `_disposed`、**释放被动监听器 `ClosePassive()`**、清空 `_pipeline`。释放动作必须在此处：`Dispose()` 有 `_disposed` 提前返回守卫，若只在 `Dispose` 里关监听器，收口后再 `Dispose` 会被跳过，被动端口一直占用到进程退出。

回复统一 `pipeline.WriteAsync(msg).GetAwaiter().GetResult()`（`FtpReply` 单行自动补 CRLF，`FtpRawText` 视为已带换行的整块文本，`FEAT` 的 `211-…211 End` 走后者）。出站执行在调用线程上启动、不依赖接收泵，因此在入站处理器内同步等待不会自锁；写失败只记日志，不向命令处理抛。

## 三、客户端同步桥接

| 环节 | 实现 |
|---|---|
| 发送 | `FtpCommandEncoder` 按 `ClientConfig.Encoding`（默认 UTF-8）编码 `命令 + CRLF` 成 `RawFrame` |
| 接收 | `FtpReplyDecoder` 产出 `FtpReply`；非法应答不抛异常，改为产出 `FtpReplyFault`，由等待方线程抛 `IOException`，连接保持可用 |
| 暂存 | `FtpControlReplyHandler` 有界队列容量 32 + `BlockingCollection`；队列满时阻塞分发线程形成 TCP 反压，属预期 |
| 取应答 | `TryTake(CommandTimeout)` 超时抛 `IOException("FTP control connection read timeout: …")` |
| 收口唤醒 | `ChannelInactiveAsync` 中 `CompleteAdding()` 并置错误，等待方立即抛 `IOException("FTP control connection closed by server")`，不会挂到超时 |
| 发送失败 | `ChannelClosedException` 统一翻译为 `IOException("FTP command send failed: …")`，与迁移前同型同前缀 |

`FTPClient.Connect()`/`ConnectAsync()` 首行 `FreezeConfigurePipeline()`，随后用当前回调值建 `FtpControlConnection`；`CreateSession()`（并行分片）读取同一属性值，故并行会话共用同一装配回调。

## 四、公开 API 变化

| 入口 | 变化 |
|---|---|
| `FTPClient` | 删除 `public event Action OnConnected` / `Ondisconnected`；新增 `ConfigurePipeline`（首次 `Connect`/`ConnectAsync` 之后赋值抛 `InvalidOperationException`）；新增只读 `Config` |
| `FTPServer` | 新增 `ConfigurePipeline`（`Start()` 之后赋值抛 `InvalidOperationException`）；`OnLog` 保留 |
| 新增类型 | `FtpReplyEncoder`、`FtpReplyDecoder`、`FtpCommandEncoder`、`FtpCommandDecoder`、`FtpControlReplyHandler`、`FtpServerCommandHandler`、`FtpRawText`、`PipelineRawByteStream`、`PipelineWiring`、`PipelineHostRegistry`，全部 `public` 且补齐 XML 注释 |
| 依赖 | `SAEA.FTP.csproj` 增加 `SAEA.Sockets.Pipeline` 项目引用；未动 `PackageReference`、未动 `<Version>` |

`PipelineRawByteStream` / `PipelineWiring` / `PipelineHostRegistry` 是 `SAEA.MessageSocket` 同名文件的逐包副本，与原件逐行比对的差异只有 banner 字段与命名空间（`36` / `18` / 少量行）。

## 五、构建与测试

| 命令 | 结果 |
|---|---|
| `dotnet build Src\SAEA.Sockets.sln -c Debug` | 0 errors（3718 warnings，全部为存量 `CS1591`/Newtonsoft 家族） |
| `dotnet test Src\SAEA.FTP.Tests\SAEA.FTP.Tests.csproj -c Release` | **23 / 23 passed**（13 存量 + 10 新增：控制通道 8 条 + 冻结 2 条） |

新增用例：

| 文件 | 用例 | 断言 |
|---|---|---|
| `FtpControlChannelTests.cs` | `Server_Greeting_OnConnect` | 裸 `TcpClient` 连上首行以 `220 ` 开头 |
| | `Server_FeatReply_MultiLine` | 登录后 `FEAT` 收到 `211-` 开头、`211 ` 结束的多行块 |
| | `Server_UnknownCommand_RepliesNotImplemented` | 未登录发 `FOO` → `502`，连接仍可继续 `USER` |
| | `Server_Quit_Replies221ThenCloses` | 发 `QUIT` → 先收到 `221`，随后读到 EOF |
| | `Server_OverlongCommandLine_ClosesConnection` | 发 9KB 无换行 → 连接在超时内关闭 |
| | `Client_MultiLineGreeting_Connects` | 假服务器发 `220-Welcome\r\n220 Ready\r\n`，`Connect()` 成功 |
| | `Client_MalformedReply_ThrowsIOException` | 假服务器发 `abc\r\n` → `Connect()` 抛 `IOException` |
| | `Client_ServerClosedWhileWaiting_ThrowsIOException` | 假服务器发问候后关闭 → 下一命令抛 `IOException`，耗时 < 5s（不等满超时） |
| `ConfigurePipelineFreezeTests.cs` | `FTPServer_ConfigurePipelineAfterStart_Throws` | `Start()` 后赋值抛 `InvalidOperationException` |
| | `FTPClient_ConfigurePipelineAfterConnect_Throws` | 首次 `Connect()` 后赋值抛 `InvalidOperationException` |

假服务器用进程内 `TcpListener(IPAddress.Loopback, 0)` + `FakeFtpServer`，不依赖外部服务；既有 13 条传输用例未改断言、未加 `[Ignore]`。

## 六、深度审查结论

审查覆盖链路时序、会话生命周期、同步桥接、分帧与编码、冻结语义、注释与文档六块，逐项结论：

1. **问候时序**：`ChannelActiveAsync` 早于泵首次读取，220 一定先于任何命令回复；`FtpServerCommandHandler` 在会话缺失时直接 `CloseAsync("ftp-session-missing")` 收口而非空转。
2. **QUIT 顺序**：`session.Handle` 内 `WriteAsync` 已同步等到所有权交接才返回，随后才 `CloseAsync`，`221` 先于 FIN 落地（`Server_Quit_Replies221ThenCloses` 覆盖）。
3. **被动端口释放**：`MarkClosed` 提前 `ClosePassive`（见上文第一节）；`_sessions` 与 `_userSessionCount` 的增删对称，`Stop()` 再兜底逐会话 `Dispose` 并清表。
4. **不再持有链字典**：会话的写入口只经 `AttachPipeline` 注入，`FTPServer` 不另存 `channelId → pipeline`，因此不存在「字典已移除但链仍存活」的悬挂引用；全仓已无 `_pipelines` 残留（首轮实现曾有，审查中删除并同步方案文档）。
5. **冻结时机**：`Start()` 首行先冻结再监听、`Connect()` 首行冻结再建链，消除「首个连接在冻结前装配」的窗口；getter 不加抛，重连沿用首次回调。
6. **编码等价**：`FtpReplyEncoder` 用 `new UTF8Encoding(false).GetBytes`，与旧 `Encoding.UTF8.GetBytes` 输出逐字节一致（均不写 BOM）；命令侧保留 `Trim` 语义。
7. **banner 与文档**：10 个新文件模板偏移与文档口径问题已修，见下节。

## 七、审查修正（后续提交）

| 类别 | 内容 |
|---|---|
| 格式 | `FtpRawText.cs`、`FtpCommandDecoder.cs`、`PipelineHostRegistry.cs`、`PipelineRawByteStream.cs`、`PipelineWiring.cs`、`FtpControlChannelTests.cs`、`ConfigurePipelineFreezeTests.cs` 的 ASCII banner 第 6 行少 1 个空格（60 vs 模板 61），已补齐；`FTPClient.cs`/`FTPServer.cs` 同一行是存量偏移，按「既有 banner 保持原值」未动 |
| 文档 | 方案文档 6 处与实现不符：新增文件 8→10、副本来源 `SAEA.RPC`→`SAEA.MessageSocket`、删除已移除的 `_pipelines` 描述、失活动作补「释放被动监听器」、用例名 `Replies500`→`RepliesNotImplemented`（实际是 502）、新增用例 7→10 与验收 20→23 |
| 用例 | 新增 `Server_Quit_Replies221ThenCloses`，覆盖本步改动的「回复后关链」语义（原实现的 QUIT 关链路径无用例） |
| 示例 | 中英 README 的连接示例补 `using System.Threading.Tasks;`（示例里的 `ControlLifecycleHandler` 返回 `Task`，原文缺该 using 无法直接编译） |

## 八、未运行项

| 项 | 原因 |
|---|---|
| `Src/SAEA.FTPTest`（WinExe / net462）人工 smoke | 该 demo 仍引用 `..\packages\SAEA.FTP.26.4.23.1` 旧包 dll，且 `FtpClientForm.cs:529` 仍用已删除的 `Ondisconnected`；切项目引用与改订阅方法属第 6 步，本步不做 |
| 其余九套 MSTest | 本步只动 `SAEA.FTP`，未触及它们的输入；改动的共享面仅 `SAEA.Sockets.Pipeline`（只读引用，未改） |
| 全量 clean rebuild | 用增量构建 + 单包 `-c Release` 测试覆盖改动面 |

## 九、遗留风险

1. **写方向无超时**：旧 `NetworkStream.WriteTimeout = CommandTimeout`，新 `WriteAsync(...).GetAwaiter().GetResult()` 无上限。控制包 <8KB、发送缓冲 64KB，实际不会挂住；要完全等价需加带超时的等待。
2. **装配回调抛异常**：客户端要等满 `ConnectTimeout` 才以 "pipeline not ready" 失败，原始异常原因不带回调用方。
3. **`CloseAsync` 只保证交接**：`221` 只保证进入内核发送缓冲，不保证对端已收到，与 `IChannelPipeline.CloseAsync` 的统一语义一致。
4. **队列容量 32 是经验值**：FTP 严格一问一答，正常最多暂存 150/226 两条；若将来出现流水线式命令，需重新评估或改成按请求配完成源。

## 十、下一步

按方案文档推进 P4 第 3 步（数据通道）：`TakeAvailableSplitter` + `FtpDataChunkDecoder` + `ChannelStreamAdapter`，保持 PASV / REST / 上下行方向 / 226 完成应答 / 关闭时序；`FTPClient` 现依赖的 `data.GetStream()`、`data.Client.Shutdown(SocketShutdown.Send)`、`data.Close()` 需先确定与适配器的对应关系。第 3 步开工前先出 2–3 个方案对比。