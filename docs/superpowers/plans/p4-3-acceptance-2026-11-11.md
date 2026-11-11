# P4-3 验收记录：FTP 数据通道迁移到处理器链（2026-11-11）

**计划：** `docs/superpowers/plans/2026-11-11-p4-data-channel-plan.md`（D1–D6 与验收命令）

**方案：** `docs/superpowers/specs/2026-11-06-socket-pipeline-migration-design.md` §八 C（无边界透传）

**提交：** 7 条，按时间正序见第一节。

**范围：** 服务端 PASV/EPSV 数据监听与接受、客户端数据连接建立与关闭、`HandleList`/`HandleRetr`/`HandleStor` 的数据读写、`FTPClient` 上传/下载/并行分片的数据连接取用，全部从 `TcpListener`/`TcpClient` + `NetworkStream` 换到 SAEA 流式 socket + 处理器链。`PORT`/`EPRT` 主动模式（现状即禁用）、`FTPClient` 的分片编排/重试/进度逻辑、`Core/FTPDataManager.cs` 遗留物不纳入。控制通道已在 P4-2 完成。

**结论：** 数据通道已全部跑在链上；`SAEA.FTP.Tests` 44/44、`SAEA.Sockets.Pipeline.Tests` 280/280、十一套合计 727 条全部通过。期间发现并修复了「无边界分帧器超过单帧上限即抛异常」引起的并行下载回归（第六节）。

---

## 一、提交序列

以下只列 P4-3 相关提交，同期另有 P2P/TURN 提交，不属本次范围。数据通道适配层（`FtpDataChunk`、`FtpDataChunkDecoder`、`FtpDataChunkEncoder`、`ChannelStreamAdapter` 与 `FtpDataChannelTests.cs`）在 `3069b4d3` 已先行落地，下列提交把它接到真实数据连接上。

| 提交 | 说明 |
|---|---|
| `15f457d6` | `fix(sockets)`：流模式下对端 FIN 视为入站结束并补处理器收尾断开（`SaeaServerSocket.cs`、`SaeaClientSocket.cs`、新增 `RemoteFinTests.cs` 5 条用例；3 文件 +521/−13） |
| `af05791e` | `test(sockets)`：`InputClaimTests.cs` 适配「输入处理器返回即断开」新契约（用 `TaskCompletionSource` 保活处理器，断言未放宽；1 文件 +13/−6） |
| `1d250612` | `feat(ftp)`：服务端数据通道（`FTPServer.cs`、新增 `Net/FtpDataConnection.cs`、`Net/FtpDataListener.cs`、`Net/Pipeline/FtpDataFaultHandler.cs`、`PipelineWiring.cs`、`FTPTransferTests.cs`；6 文件 +843/−61） |
| `de67f3c0` | `docs(ftp)`：P4-3 计划文档（1 文件 +110/−0） |
| `06922ff7` | `feat(ftp)`：客户端数据通道（`FTPClient.cs`、`Net/FtpControlConnection.cs`、`Net/FtpDataConnection.cs`、`Net/FtpDataListener.cs`、新增 `Net/FtpDataConnector.cs`、新增 `Properties/AssemblyInfo.cs`、`README.md`、`README.en.md`、新增 `FtpDataConnectionTests.cs`；9 文件 +742/−69） |
| `62ffecd4` | `fix(ftp)`：统一新增文件 banner（4 个文件第 8 行归一为 62 空格）+ 补 `Dir_上传文件后_MLSD列出远端文件名`、`Dir_上传文件后_LIST列出远端文件名` 两条用例；7 文件 +76/−21 |
| `33162839` | `fix(sockets-pipeline)`：无边界分帧器超过单帧上限时切块而非抛异常（`TakeAvailableSplitter.cs` + 新增 `TakeAvailableSplitterTests.cs` + 调整 `OtherSplittersTests.cs` 一条 + `Src/SAEA.Sockets.Pipeline/README*.md` + `Src/SAEA.Sockets/README*.md` + 设计稿与 P0 计划文档措辞；10 文件 +234/−28） |

## 二、链路装配与参数

数据连接与控制连接同构：`ChannelInfo` + `PipelineWiring.RunAsync` + 处理器链。服务端用 `FtpDataListener`（一次性监听、只服务首条连接），客户端用 `FtpDataConnector`（一次性连接器）。

| 侧 | 链路顺序（自出站首节点到链尾） |
|---|---|
| 服务端 | `FtpDataChunkEncoder` → `OutboundSerializingHandler` → `FtpDataChunkDecoder` → `FtpDataFaultHandler` → `ExceptionTerminalHandler` |
| 客户端 | 同上 |

| 参数 | 取值 | 说明 |
|---|---|---|
| `Splitter` | `TakeAvailableSplitter(64 * 1024)` | 消息边界 = 一次读取；超过单帧上限按上限切块（第六节修复后的语义） |
| `ReadBufferSize` | `64 * 1024` | 与单帧上限同量级 |
| `MaxBufferedBytes` | `64 * 1024 + FrameConstants.P_Head` | `Create` 的缓冲下限校验（`P_Head = P_LEN(8) + P_Type(1) = 9`） |
| `MaxConcurrentMessages` | `1` | 与现状单读循环一致 |
| `QueueCapacity` | `8` | 默认 1024 × 64KB 过大 |
| `MessageTimeout` | `TimeSpan.Zero` | 传输是长任务，不设单消息超时 |
| `DispatchMode` | `Inline` | 见第八节 |
| Adapter 队列 | 8 块 / 512KB | 满了阻塞泵线程，形成背压 |

服务端 `FTPServer._dataOptions` 与客户端 `Net/FtpControlConnection.cs` 里的 `_dataOptions` 是同一组参数的两份拷贝，代码注释已互相指名对方文件；只改一边会导致两端分帧与背压行为不一致。

## 三、同步门面与交接时机

| 环节 | 实现 |
|---|---|
| 有界队列 + `Stream` | `ChannelStreamAdapter`（默认 8 块 / 512KB），队列满时阻塞泵线程形成背压 |
| 数据门面 | `FtpDataConnection`（`public sealed`）暴露 `Stream`、`ReadTimeout`、`WriteTimeout`、`ShutdownSend()`、`Close()`、`Dispose()`；关闭语义仍是「先半关闭发 FIN，再断开」 |
| 两个工厂 | `CreateForServer(IServerSocket owner, string channelId, Socket socket, ChannelStreamAdapter adapter)` / `CreateForClient(IClientSocket client, Socket socket, ChannelStreamAdapter adapter)`，均 `internal static` |
| 交接时机 | 链启动（`PipelineWiring.RunAsync` 的 `onStarted(channelId, pipeline)` 回调，D6）之后才把连接交给传输代码，否则首块数据可能早于写入口绑定被丢弃 |
| 半关闭 | D1：直接 `Socket.Shutdown(SocketShutdown.Send)`，服务端从 `ChannelInfo.ClientSocket` 取、客户端从 `SaeaClientSocket.Socket` 取；不改 `SAEA.Sockets` 公开 API |
| 监听器生命周期 | D2：每个 PASV/EPSV 命令新建监听器、只服务首条连接，传完先关数据连接再释放监听器；`Stop()`/`Dispose()` 会连带杀掉已接受连接，故监听器最后释放 |
| 完成应答 | 服务端 `226`（`ServerResponseCode.结束数据连接`）在数据连接关闭之后写出 |

## 四、公开 API 变化

| 入口 | 变化 |
|---|---|
| `FtpControlConnection.OpenPassive` | 返回类型由 `TcpClient` 改为 `FtpDataConnection`（D3，公开 API 破坏性变更；`Src/SAEA.FTP/README.md` / `README.en.md` 已同步） |
| `FTPClient` | 公开方法保持同步；`GetStream()` → `.Stream`、`Client.Shutdown(SocketShutdown.Send)` → `ShutdownSend()`，共 20 处数据连接调用点（D4） |
| `PipelineWiring.RunAsync` | 新增可选参数 `Action<string, IChannelPipeline> onStarted = null`（D6，源兼容） |
| 新增公开类型 | `FtpDataConnection`、`FtpDataFaultHandler` |
| 新增内部类型 | `FtpDataListener`、`FtpDataConnector`（`internal sealed`，不扩公开面） |
| 测试可见性 | 新增 `Src/SAEA.FTP/Properties/AssemblyInfo.cs` 的 `[assembly: InternalsVisibleTo("SAEA.FTP.Tests")]`，用于直连 `internal` 的 `FtpControlConnection` 验证数据门面；未改任何 `.csproj`、未为此放宽生产类型可见性 |

## 五、语义变更：流模式对端 FIN

`Src/SAEA.Sockets` 流模式下「对端 FIN」的处理口径变了，这一节属于基础库语义变更，需单独记录：

| 模式 | 迁移前 | 迁移后 |
|---|---|---|
| 流模式 | 读到 0 字节即按断开处理 | 入站结束投递 `CompleteInput()`，处理器读完 `Pipe` 剩余字节后返回，框架随即 `Disconnect` |
| 事件 / 包模式 | 0 字节即断开 | 不变 |

动机是数据连接收口不能截断尾字节：若「读到 0 就断开」，处理器尚未读走的同批数据会被一起丢掉。影响面是所有流模式使用者，不只是 FTP。契约由 `Src/SAEA.Sockets.Pipeline.Tests/Tests/Integration/RemoteFinTests.cs` 的 5 条用例固定（`Server_PeerFin_InputHandlerReadsAllBytesThenSeesEof`、`Server_PeerFin_CanStillSendBack`、`Server_PeerFin_DisconnectRaisedAfterHandlerCompletes`、`Client_PeerFin_InputHandlerReadsAllBytesThenSeesEof`、`Client_PeerFin_NoSpin_DisconnectRaised`），并与 `InputClaimTests` 的「输入处理器返回即断开」契约（`af05791e`）配套。

## 六、期间发现并修复的回归：无边界分帧器超限

**现象：** 并行下载回归复现器 `ParallelDownload_UsesMultipleSessions` 隔离连跑 10 次，迁移后 8–10 次失败，报 `expected parallel sessions, got 1`（并行分片失败后退回单会话）。

**根因：** 数据链 `TakeAvailableSplitter(64KB)` 的「超限抛异常」语义与 `SaeaConnection` 的 Pipe 暂停阈值 `ReadBufferSize × 4`（64KB 配置下为 256KB）不匹配。泵被 `ChannelStreamAdapter` 有界队列背压拖慢时，`PipeReader.ReadAsync` 一次会交出 128/192/256KB 的合并缓冲，`TakeAvailableSplitter.TrySplit` 直接抛 `FrameProtocolException`：

```
无边界分帧的可用缓冲长度 196608 超过单次读取上限 65536
```

分片下载因此失败，`DownloadParallel` 回退单会话。

**定性证据：** 基线 `3069b4d3`（迁移前，`TcpClient`/`NetworkStream` 数据面）同一用例连跑 10 次 **10/10 通过**，确认是本次迁移引入而非既有缺陷。

**修复：** `TakeAvailableSplitter.TrySplit` 在可用缓冲超过 `MaxFrameLength` 时改为「按上限切块」——`consumed = _maxFrameLength`、`frame = buffer.Slice(0, _maxFrameLength)`，其余字节留给下一次；`MaxFrameLength` 语义从「超过即报错」变为「单帧硬上界」。选这条路而非在 FTP 侧抬高上限的原因：后者把 Sockets 内部的 Pipe 阈值公式耦合进 FTP，且会把单帧放大到 320KB；切块方案帧大小重新有界、不丢字节，对同用该分帧器的 HTTP 协议升级通道同样是健壮性提升。

**护栏用例：**

| 文件 | 用例 | 断言要点 |
|---|---|---|
| `Src/SAEA.FTP.Tests/FtpDataChannelTests.cs` | `DataChannel_一次到达超过单帧上限_切块读出且不丢字节` | 一次写入 256KB（4×64KB），经 `FtpDataChunkDecoder` + `ChannelStreamAdapter` 读出的总长与逐字节内容一致 |
| `Src/SAEA.Sockets.Pipeline.Tests/Tests/Splitters/TakeAvailableSplitterTests.cs` | `TrySplit_缓冲超过单帧上限_按上限切块且不抛异常` | 上限 64KB、缓冲 3×64KB+100，依次切 64K/64K/64K/100，累计消费等于总长 |
| 同上 | `TrySplit_缓冲未超上限_整段一帧` | 32KB 缓冲一帧 32KB、`consumed` = 32KB |
| 同上 | `TrySplit_空缓冲_返回false` | 空缓冲不产帧、`consumed` = 0 |

既有 `TakeAvailable_ExceedsMaxFrameLength_Throws` 随语义变更改名为 `TakeAvailable_ExceedsMaxFrameLength_SplitsAtLimit` 并改为切块断言；其余既有断言未放宽、未新增 `[Ignore]`。

## 七、构建与测试

| 命令 / 项 | 结果 |
|---|---|
| `dotnet build Src\SAEA.Sockets.sln -c Debug` | 0 错误；此前一次全量构建为 0 警告，分帧器修复后一次增量构建出现 1 条 `SAEA.MVCTest\Controllers\EventStreamController.cs(51,30) CS0168`，与本次无关 |
| `dotnet test Src\SAEA.FTP.Tests\SAEA.FTP.Tests.csproj -c Release` | **44/44 通过，连跑两遍一致** |
| `dotnet test Src\SAEA.Sockets.Pipeline.Tests\SAEA.Sockets.Pipeline.Tests.csproj -c Release` | **280/280 通过** |
| 其余九套（逐个指定 csproj，`-c Release`） | Common 59、MQTT 153、Http 29、WebSocket 12、RPC 14、QueueSocket 16、MessageSocket 17、FileSocket 11、P2P 92，全部 0 失败 |
| 十一套合计 | **727 条通过、0 失败、0 跳过** |
| 并行下载回归 | `ParallelDownload_UsesMultipleSessions` 连跑 **10/10 通过** |
| 并行上传 | `ParallelUpload_UsesMultipleSessions` 连跑 **5/5 通过** |

本次新增/调整的用例如下（适配器 12 条与 `DataChannel_入站字节…`、`DataChannel_出站写入…` 在 `3069b4d3` 已落地）：

| 文件 | 用例 | 断言要点 |
|---|---|---|
| `FtpDataChannelTests.cs` | `DataChannel_一次到达超过单帧上限_切块读出且不丢字节` | 见第六节 |
| `FtpDataConnectionTests.cs`（新增） | `OpenPassive_下载_读到全部字节并读到结束` | 数据门面直连 `internal` 控制连接，读到全部字节后读到 EOF |
| 同上 | `OpenPassive_上传_半关闭后收到226且远端完整` | `ShutdownSend()` 后收到 `226`，远端文件内容完整 |
| 同上 | `Close_重复调用_不抛异常且控制连接仍可用` | `Close()` 幂等，控制连接仍可继续命令 |
| `FTPTransferTests.cs` | `Dir_上传文件后_MLSD列出远端文件名` | 上传后 MLSD 列出远端文件名 |
| 同上 | `Dir_上传文件后_LIST列出远端文件名` | 上传后 LIST 列出远端文件名 |
| `RemoteFinTests.cs`（`15f457d6`，新增） | 5 条 | 第五节 |

## 八、偏离设计稿的取舍

设计稿 `docs/superpowers/specs/2026-11-06-socket-pipeline-migration-design.md` §八 C 要求数据链 `DispatchMode = Queued`；实际取 `Inline`（计划文档 D5 已记录）。

原因：`ChannelPipelineExecutor` 泵读到 EOF 会立即 `BeginShutdown("remote-eof")` 并丢弃队列中尚未执行的帧，`Queued` 会复发尾帧截断；`Inline` 由解码器在泵线程同步入队，保住同批尾帧。代价是入队阻塞会直接压在泵线程上——适配器消费者是文件读写循环，不会反过来等待泵线程，因此不构成互锁。

## 九、遗留与范围外（只列不改）

1. **控制连接应答流未重同步：** `FTPClient` 在 `RETR`/`STOR` 失败后未对控制连接做应答流重同步，上一轮中断留下的 `426`、或被动端口应答 `229`，会被当成本轮命令回复，级联成不可重试错误。属既有弱点，本次未处理。
2. **残帧检查恒不触发：** `ChannelPipelineExecutor` 的 `MaxBufferedBytes` 残帧检查在无边界分帧器下恒不触发（每轮 `consumed` 等于全部可用字节，残帧恒为 0）；真正的上界由 Pipe 的 `pauseWriterThreshold` 与 `MaxFrameLength` 共同保证。
3. **HTTP 协议升级通道：** `Src/SAEA.Http/Base/Net/HttpSocket.cs` 的升级通道也用 `TakeAvailableSplitter(_bufferSize)`，本次语义变更后超限由「抛异常断链」变为「切块继续」，方向更健壮；但现有升级用例用的是 `FixedLengthSplitter`，没有专门覆盖超限场景（Http 29/29、WebSocket 12/12 通过）。

## 十、环境说明

全量构建 `Src\SAEA.Sockets.sln` 里含 .NET Framework 4.6.2/4.8 演示工程，需要 `Src\packages` 目录（NuGet packages 目录，不入版本库）。隔离 worktree 验证时以目录 junction 指向主工作区的 `Src\packages`，库工程与单测不依赖该目录。