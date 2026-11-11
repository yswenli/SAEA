# P4-3：SAEA.FTP 数据通道迁移到 SAEA.Sockets + Pipeline

- 日期：2026-11-11
- 上游依据：`docs/superpowers/specs/2026-11-06-socket-pipeline-migration-design.md` §八 C（无边界透传）；`docs/superpowers/plans/2026-11-09-socket-pipeline-migration-plan.md` §六 P4
- 前序：`docs/superpowers/plans/2026-11-11-p4-control-channel-plan.md`（控制通道）、`docs/superpowers/plans/p4-2-acceptance-2026-11-11.md`（控制通道验收）
- 现状：控制通道已全部跑在链上；数据面仍是 `TcpListener`/`TcpClient` + `NetworkStream` 同步阻塞读写

---

## 一、范围

| 纳入 | 不纳入 |
|---|---|
| PASV/EPSV 数据监听与接受 | `PORT`/`EPRT` 主动模式（现状即禁用，保持） |
| 客户端数据连接建立与关闭 | `FtpReply`/`ClientRequest` 等控制通道模型 |
| `HandleList`/`HandleRetr`/`HandleStor` 的数据读写 | `FTPClient` 的分片编排、重试、进度回调逻辑 |
| `FTPClient` 上传/下载/并行分片中的数据连接取用 | `Core/FTPDataManager.cs`（遗留物，不复用） |
| 数据通道适配层与用例 | `Src/SAEA.Sockets.Pipeline/**`（禁改） |

---

## 二、已确认决策

| 编号 | 决策 | 理由 |
|---|---|---|
| D1 | 半关闭直接用底层 `Socket.Shutdown(SocketShutdown.Send)`：服务端从 `ChannelInfo.ClientSocket` 取，客户端从 `SaeaClientSocket.Socket` 取 | `SAEA.Sockets` 无公开半关闭 API，改基础库会影响全部上层包；FTP 只借用已有公开的 `Socket` 引用 |
| D2 | 每个 PASV/EPSV 命令新建一个 `SaeaServerSocket`，只服务首条连接，后续连接立即 `Disconnect`；传完先关数据连接再 `Dispose` 监听器 | 保持「一个被动端口一条数据连接」的现状语义 |
| D3 | `FtpControlConnection.OpenPassive` 返回类型改为 `FtpDataConnection`（公开 API 破坏性变更，README 中英同步） | 避免包内长期并存两个数据入口 |
| D4 | 数据面保留同步 `Stream` 门面：新增 `ChannelStreamAdapter`，现有 `FileStream` 读写循环不改 | 改动面集中，`FTPClient` 全部公开方法保持同步 |
| D5 | 数据链 `DispatchMode = Inline`、`MessageTimeout = 0` | 设计稿 §八 C.1 要求 `Queued`；本实施按已确认取舍选 `Inline`：泵读到 EOF 会丢弃队列帧，`Queued` 下同批尾部数据会丢，`Inline` 下解码器同步入队可保尾帧。差异与反例用例记入验收文档 |
| D6 | `PipelineWiring.RunAsync` 增加可选参数 `Action onStarted = null`，在 `executor.Start()` 之后回调 | 数据连接必须在链启动后才能交给传输代码，否则首帧可能早于 `Attach` |

---

## 三、链路形态与参数

```
出站：FtpDataChunkEncoder → OutboundSerializingHandler
入站：FtpDataChunkDecoder(adapter) → ExceptionTerminalHandler
```

| 参数 | 取值 | 说明 |
|---|---|---|
| `Splitter` | `new TakeAvailableSplitter(64 * 1024)` | 消息边界 = 一次读取 |
| `ReadBufferSize` | `64 * 1024` | 与分帧上限一致，单次读取不会超帧 |
| `MaxBufferedBytes` | `64 * 1024 + FrameConstants.P_Head` | `Create` 的下限校验 |
| `DispatchMode` | `Inline` | D5 |
| `MessageTimeout` | `TimeSpan.Zero` | 传输是长任务，不设单消息超时 |
| `MaxConcurrentMessages` | `1` | 与现状单读循环一致 |
| `QueueCapacity` | `8` | 默认 1024 × 64KB 过大 |
| `MaxPendingWrites` | `256` | 默认值 |
| Adapter 队列 | 8 块 / 512KB 上限 | 满了阻塞泵线程，形成背压 |

数据连接的一次性语义：连接 → 传输 → 半关闭发送 → 关闭。`Stream.Read` 在对端 FIN 时返回 0；`ReadTimeout`/`WriteTimeout` 超时抛 `IOException`（与 `NetworkStream` 一致）。

---

## 四、文件清单

### 新增

| 文件 | 职责 |
|---|---|
| `Src/SAEA.FTP/Net/Pipeline/FtpDataChunk.cs` | 数据块，持有数组所有权，`Dispose` 归还池 |
| `Src/SAEA.FTP/Net/Pipeline/FtpDataChunkDecoder.cs` | 入站帧 → `FtpDataChunk` → 入 adapter 有界队列 |
| `Src/SAEA.FTP/Net/Pipeline/FtpDataChunkEncoder.cs` | 出站 `FtpDataChunk` → `RawFrame`，所有权交给传输层 |
| `Src/SAEA.FTP/Net/Pipeline/ChannelStreamAdapter.cs` | 有界队列 + `Stream` 门面（`Read`/`Write`/`Flush`/超时/EOF/异常/`Dispose` 唤醒） |
| `Src/SAEA.FTP/Net/FtpDataConnection.cs` | 数据连接门面：`Stream`、`ReadTimeout`/`WriteTimeout`、`ShutdownSend()`、`Close()`；服务端接受与客户端连接两个工厂 |
| `Src/SAEA.FTP.Tests/FtpDataChannelTests.cs` | 适配器与数据通道用例 |

### 修改

| 文件 | 改动 |
|---|---|
| `Src/SAEA.FTP/Net/Pipeline/PipelineWiring.cs` | D6 的可选回调 |
| `Src/SAEA.FTP/Net/FtpControlConnection.cs` | `OpenPassive`/`ConnectData` 返回 `FtpDataConnection` |
| `Src/SAEA.FTP/FTPClient.cs` | 全部 `data.GetStream()`/`data.Client.Shutdown`/`data.Close()` 调用点 |
| `Src/SAEA.FTP/FTPServer.cs` | `ServerSession` 的 `_passive`、`CreatePassiveListener`、`AcceptDataConnection`、`ClosePassive`、`CloseData`、`HandlePasv`/`HandleEpsv`/`HandleList`/`HandleRetr`/`HandleStor` |
| `README.md` / `README.en.md`、`Src/SAEA.FTP/README.md` / `README.en.md` | 数据通道能力与新公开类型 |

---

## 五、语义保持清单

1. PASV 回复 `227 (h1,h2,h3,h4,p1,p2)`、EPSV 回复 `229 (|||port|)` 的端口取值不变；`PassivePortMin..Max` 顺序尝试、失败回退系统分配。
2. `150` 先于数据连接建立后写出；`226` 只在数据连接关闭后写出（服务端）。
3. 客户端 `OpenPassive` 仍先建数据连接再发命令（`Dir`/`Retr`/`Stor` 的现有顺序不能反转）。
4. `REST` 偏移、上传覆盖策略（Reject/Rename/Overwrite）、`STOR` 的 `FileMode` 选择不变。
5. 客户端并行分片的多会话语义不变（每个会话各自的数据连接）。
6. 现有 `SAEA.FTP.Tests` 13 个数据面用例逐字节比对结果不变。

---

## 六、分步与验收

| 步 | 内容 | 验收 |
|---|---|---|
| 1 | `FtpDataChunk`、`FtpDataChunkDecoder`、`FtpDataChunkEncoder`、`ChannelStreamAdapter` + 适配器用例 | `dotnet test Src/SAEA.FTP.Tests` 中新增用例全绿；现有 23 条不回归 |
| 2 | 服务端：`FtpDataConnection` 接受工厂、PASV 监听器重写、`HandleList/Retr/Stor` 换门面 | `SAEA.FTP.Tests` 13 条数据面用例全绿 |
| 3 | 客户端：`OpenPassive`/`ConnectData` 与 `FTPClient` 全部调用点 | 同上，且并行上传/下载用例通过 |
| 4 | README 中英同步、验收文档 | `dotnet build Src/SAEA.Sockets.sln` 0 errors |

---

## 七、风险

1. `Socket.Shutdown(Send)` 在 SAEA 持有的 `Socket` 上调用，所有权边界必须写进注释；`ShutdownSend` 只能在所有写完成交接后调用。
2. `SaeaServerSocket.Dispose()` 会连带杀掉已接受连接，数据连接必须先关闭再释放监听器，否则尾部数据可能被截断。
3. `SaeaServerSocket` 持续接受连接，多余连接必须主动 `Disconnect`，否则会成为无主的空会话。
4. `Inline` 分发要求解码器同步入队；若队列满，泵线程阻塞，必须确认消费者（文件读写循环）不会反过来等待泵线程。