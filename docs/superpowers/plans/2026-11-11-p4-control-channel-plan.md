# P4 控制通道重写方案（SAEA.FTP）

- 日期：2026-11-11
- 上游依据：`docs/superpowers/specs/2026-11-06-socket-pipeline-migration-design.md` §4.1 装配契约、§四 传输层约定、§八 B（文本行）、§八 C（无边界透传）
- 总计划：`docs/superpowers/plans/2026-11-09-socket-pipeline-migration-plan.md` §六 P4
- 现状：P4 第 1 步（删死代码）已提交；本文件只覆盖**控制通道**，数据通道另案

---

## 一、范围

| 纳入 | 不纳入（本步不动） |
|---|---|
| `FTPServer` 控制端口的监听与每会话命令收发 | 被动模式监听器 `TcpListener _passive`、`AcceptDataConnection` |
| `FTPServer.ServerSession` 的命令读取与回复写出 | `OpenPassive` 返回的 `TcpClient` 与全部数据读写（`GetStream()`） |
| `Net/FtpControlConnection` 的连接、握手、`Send`/`ReadReply` | `FTPClient` 的传输编排（上传下载分片、REST、进度） |
| 服务端/客户端各自的解码与编码 handler | `FTPCoder`（保留 `ICoder` 实现，`Encode` 不变） |

数据通道仍走 `TcpListener`/`TcpClient` 是**过渡态**：P4 第 3 步会把 PASV 监听与数据读写一起换成 SAEA socket + `TakeAvailableSplitter`，收口为设计 §八 C 的形态。本步结束后包内会短暂同时存在「链 + 裸 `TcpClient`」，验收时按控制通道用例判定，不宣称 P4 完成。

---

## 二、待确认决策

### D1 `FTPClient` 的公开生命周期事件与 `ConfigurePipeline`

现状：`FTPClient` 有 `public event Action OnConnected`、`public event Action Ondisconnected`（`FTPClient.cs:63`、`:68`），`Connect()`/`DropControl()` 里触发。

| 方案 | 内容 | 权衡 |
|---|---|---|
| **A（推荐）** | 删两个事件，`FTPClient` 加 `ConfigurePipeline`（`Action<string, IChannelPipeline>`），建连/断开通知改由使用者 handler 的 `ChannelActiveAsync`/`ChannelInactiveAsync` 接收 | 与 §4.2、P2-2（`WSClient`）同口径；`FTPTest` 的 `FtpClientForm.cs:529` 需在 P4 第 6 步改为链回调 |
| B | 保留两个事件，另加 `ConfigurePipeline` | 事件与链回调双轨通知，违背「对外入口不再暴露生命周期事件」；但 demo 零改动 |
| C | 都不动 | P4 结束后 FTP 成为唯一带接收/生命周期事件的上层包，契约破口 |

`FTPServer.OnLog` 是日志事件，不是接收/生命周期事件，三种方案下都保留。

### D2 服务端命令的执行位置

| 方案 | 内容 | 权衡 |
|---|---|---|
| **A（推荐）** | 入站 handler 内联执行 `ServerSession.Handle(...)`（`DispatchMode = Queued`，`MaxConcurrentMessages = 1`） | 改动最小，命令处理代码零重写；阻塞发生在该连接的消费循环上，语义等同现状「单会话单读循环」。传输期间不读后续命令，与现在一致（现在是 `ReadCommandLine` 阻塞） |
| B | 入站 handler 只入队，`ServerSession` 起独立 worker 线程串行执行 | 消费循环不被文件传输阻塞；但多一套队列/线程与取消收尾，且有界队列反压会把分发线程堵住，收益有限 |

A 的风险：`Stop()` 时正在传输的会话会被收口流程按 `DrainTimeout`（默认 5 秒）截断。现状 `Stop()` 直接 `Dispose` 会话，同样是截断，行为等价。

### D3 依赖与分步

- `Src/SAEA.FTP/SAEA.FTP.csproj` 需新增 `<ProjectReference Include="..\SAEA.Sockets.Pipeline\SAEA.Sockets.Pipeline.csproj" />`（其余六个已迁移包同款）。**这是依赖新增，需明确许可。**
- 本步 `OpenPassive` 继续返回 `TcpClient`，数据通道代码零改动，等第 3 步一起换。

### D4 客户端同步 API 的桥接

`FTPClient` 全部公开方法是同步的。方案：`FtpControlConnection.Send`/`ReadReply` 保持同步签名，内部用 `pipeline.WriteAsync(...).GetAwaiter().GetResult()` 与有界应答队列的阻塞取用；超时映射为 `IOException`（现状 `NetworkStream` 读超时抛的也是 `IOException`）。不新增异步重载（属新增 API，需另行许可）。

**请确认：D1 选 A/B/C，D2 选 A/B，D3 是否许可新增项目引用。**

---

## 三、文件清单

### 新增（8 个）

| 文件 | 类型 | 职责 |
|---|---|---|
| `Src/SAEA.FTP/Net/Pipeline/PipelineRawByteStream.cs` | `public sealed` | 逐包副本，取自 `SAEA.RPC` 同文件，仅改命名空间为 `SAEA.FTP` |
| `Src/SAEA.FTP/Net/Pipeline/PipelineWiring.cs` | `public static` | 同上 |
| `Src/SAEA.FTP/Net/Pipeline/PipelineHostRegistry.cs` | `public sealed` | 同上 |
| `Src/SAEA.FTP/Net/Pipeline/FtpCommandDecoder.cs` | `public sealed : ByteToMessageDecoder` | 服务端：一行帧 → `ClientRequest` |
| `Src/SAEA.FTP/Net/Pipeline/FtpReplyEncoder.cs` | `public sealed : OutboundHandlerAdapter` | 服务端：`FtpReply` / `FtpRawText` → `RawFrame` |
| `Src/SAEA.FTP/Net/Pipeline/FtpServerCommandHandler.cs` | `public sealed : InboundHandlerAdapter` | 服务端：命令交给 `ServerSession`；活动/失活登记 |
| `Src/SAEA.FTP/Net/Pipeline/FtpCommandEncoder.cs` | `public sealed : OutboundHandlerAdapter` | 客户端：`string` → `RawFrame`（UTF-8 + CRLF，按 `ClientConfig.Encoding`） |
| `Src/SAEA.FTP/Net/Pipeline/FtpReplyDecoder.cs` | `public sealed : InboundHandlerAdapter` | 客户端：累加行帧，产出 `FtpReply`（greeting、多行、非法状态码） |
| `Src/SAEA.FTP/Net/Pipeline/FtpControlReplyHandler.cs` | `public sealed : InboundHandlerAdapter` | 客户端：应答入队、失活唤醒、异常转交 |
| `Src/SAEA.FTP/Model/FtpRawText.cs` | `public sealed` | 服务端原样输出多行文本（`FEAT` 的 `211-…211 End`） |

### 修改（4 个）

| 文件 | 改动 |
|---|---|
| `Src/SAEA.FTP/FTPServer.cs` | `TcpListener` + `AcceptLoop` → `IServerSocket` + `PipelineWiring`；`ServerSession` 去掉 `TcpClient`/`NetworkStream`/读缓冲，保留全部命令处理方法；`Reply`/`ReplyRaw` 改走链 |
| `Src/SAEA.FTP/Net/FtpControlConnection.cs` | `TcpClient`/`NetworkStream` → `IClientSocket` + 链；`Send`/`ReadReply` 内部改队列等待；`OpenPassive` 与 `ConnectData` 保持 `TcpClient` |
| `Src/SAEA.FTP/FTPClient.cs` | 按 D1 处理事件；`ConfigurePipeline` 冻结；三处 `new FtpControlConnection(...)` 传入装配回调 |
| `Src/SAEA.FTP/SAEA.FTP.csproj` | 新增 `SAEA.Sockets.Pipeline` 项目引用 |

`Model/ClientRequest.cs` 需加一个 `internal string Raw { get; set; }`（供现有「收到命令：」日志原样输出，不加公开成员）。

---

## 四、服务端链路

### 4.1 装配

```
FtpReplyEncoder            （出站，链首）
OutboundSerializingHandler （出站最右，§4.1 硬约束）
FtpCommandDecoder          （入站：LineSplitter 的行 → ClientRequest）
FtpServerCommandHandler    （入站：交给 ServerSession 执行）
[使用者 ConfigurePipeline]
ExceptionTerminalHandler   （链尾）
```

- `Splitter = new LineSplitter(8 * 1024)`；`MaxBufferedBytes = 8 * 1024 + FrameConstants.P_Head`；`ReadBufferSize = 64 * 1024`；`MaxConcurrentMessages = 1`；`DispatchMode = Queued`（默认）。
- 超出 8KB 未遇到 `\n` 的行由分帧器判协议错误，链收口关闭连接，不产生回复（现状会无限累积到 `List<byte>`，这里收紧为有界）。
- 出站必须先于入站装配：`AddLast` 出站 → `AddLast` 入站，顺序固定。

### 4.2 会话生命周期

| 阶段 | 位置 | 动作 |
|---|---|---|
| 接受连接 | `FTPServer.OnConnectionOpened(ChannelInfo)` | 建 `ServerSession(channelId)`，登记 `_sessions[channelId]`（键从 GUID 改为 channelId） |
| 装配 | `ConfigureConnection(channelId, pipeline)` | 装链；登记 `_pipelines[channelId] = pipeline`（供会话回复） |
| 激活 | `FtpServerCommandHandler.ChannelActiveAsync` | `session.AttachPipeline(ctx.Pipeline)`；`await ctx.Pipeline.WriteAsync(new FtpReply { Code = 220, Message = "Welcome to SAEA.FTPServer!" })` |
| 命令 | `FtpServerCommandHandler.ChannelReadAsync` | `session.Handle(request)`（同步，见 D2-A） |
| 失活 | `FtpServerCommandHandler.ChannelInactiveAsync` | `session.MarkClosed()`，`_server.RemoveSession(this)` |
| 异常 | `ChannelExceptionCaughtAsync` | 原 `Run()` 里的分类记录：`IOException`/`ObjectDisposedException` 静默，其余 `Log("FTP会话异常", ex)` |

`ChannelActiveAsync` 内回包是执行器明确支持的（`ChannelPipelineExecutor.BeginStartAsync` 的注释：「活动通知早于泵首次读取，处理器可在其中回包」），依赖 `ServerSession` 在 `OnConnectionOpened` 已建立，故 handler 构造时拿到的是已存在的会话实例。

`FTPServer.Start()`：`SocketOptionBuilder().SetSocket().UseStream().UseConnectionOpened(OnConnectionOpened).UseInputHandler(RunPipelineAsync).SetIP(...).SetPort(config.Port).SetReadBufferSize(64KB).SetWriteBufferSize(64KB)` → `SocketFactory.CreateServerSocket(option)` → `_server.Start()`。

IP 映射保持现状：`config.IP` 为空、不能解析或等于 `0.0.0.0` 时 `SetIP("")`（监听 Any），否则 `SetIP(config.IP)`。

`RunPipelineAsync(IUserToken userToken, PipeReader reader, IReceiveFlowControl flow, CancellationToken token)`：
- `channelId = userToken.ID`；
- `new PipelineRawByteStream(channelId, reader, flow, _server)`；
- `await PipelineWiring.RunAsync(channelId, stream, flow, _options, token, _host, ConfigureConnection)`；
- `finally` 里移除 `_pipelines[channelId]`（`_sessions` 由失活回调移除）。

`Stop()`：`_server.Stop()`（关闭全部通道）→ 逐个 `session.Dispose()` → 清空两张表；`Dispose()` 追加 `_server.Dispose()`。

### 4.3 回复写出

`ServerSession` 不再持有流：

```csharp
internal void Reply(int code, string message)   // 单行
{
    Write(new FtpReply { Code = code, Message = message });
}

internal void ReplyRaw(string text)             // 多行原样（FEAT）
{
    Write(new FtpRawText(text));
}

void Write(object msg)
{
    var pipeline = _pipeline;
    if (pipeline == null) return;               // 链未激活或已收口，静默丢弃
    try { pipeline.WriteAsync(msg).GetAwaiter().GetResult(); }
    catch (Exception ex) { _server.Log("FTP回复写出失败", ex); }
}
```

`WriteAsync` 的出站执行在调用线程上启动（`ChannelPipeline.WriteAsync` → `RunOutboundAsync`），不依赖接收泵，因此在入站 handler 里同步等待不会自死锁；`MaxPendingWrites`（默认 256）提供超限拒绝。

命令处理代码（`Handle`/`HandleUser`/`HandleList`/`HandleRetr`/`HandleStor`/`HandlePasv`/`ResolvePath` 等约 900 行）**逐行保留**，只删 `ReadByte`/`ReadCommandLine`/`WriteLine` 与流字段。

### 4.4 服务端解码

```csharp
protected override void Decode(IChannelHandlerContext ctx, ReadOnlySequence<byte> frame, List<object> output)
{
    if (frame.Length == 0) return;                      // 空行：现状 continue
    var line = Encoding.UTF8.GetString(...);            // 兼容 netstandard2.0，按段拼接
    var request = ClientRequest.Parse(line);            // 空白行返回 null
    if (request == null) return;
    request.Raw = line;
    output.Add(request);
}
```

`Handle` 里原有的 `try/catch` 与 `if (request == null)` 分支保留（解析已提前，防御分支不删；`ClientRequest.Parse` 当前不抛，保留 catch 不影响行为）。

---

## 五、客户端链路

### 5.1 装配

```
FtpCommandEncoder          （出站，链首）
OutboundSerializingHandler （出站最右）
FtpReplyDecoder            （入站：行 → FtpReply，跨行状态机）
FtpControlReplyHandler     （入站：入队 + 失活唤醒）
[使用者 ConfigurePipeline]
ExceptionTerminalHandler   （链尾）
```

- `Splitter = new LineSplitter(8 * 1024)`；`MaxBufferedBytes = 8 * 1024 + FrameConstants.P_Head`；`ReadBufferSize = 64 * 1024`；`MaxConcurrentMessages = 1`。
- 客户端 socket：`SetReadBufferSize(64 * 1024)`、`SetWriteBufferSize(64 * 1024)`、`SetConnectTimeout((int)ConnectTimeout.TotalMilliseconds)`，与现状 `ReceiveBufferSize/SendBufferSize = 64KB`、`BeginConnect` 超时对应。

### 5.2 同步桥接

```csharp
public FtpReply Send(string command)      // 现状：写 + 阻塞读
{
    EnsureReady();                        // _pipeline 已就绪
    _pipeline.WriteAsync(command).GetAwaiter().GetResult();   // 抛 ChannelClosedException
    return ReadReply();
}

public FtpReply ReadReply()               // 现状：从流里读一条完整应答
{
    FtpReplyItem item;
    if (!_replies.TryTake(out item, _commandTimeout))         // 有界队列，容量 32
        throw new IOException("FTP control connection read timeout: " + _config.IP);
    if (item.Error != null) throw new IOException(item.Error.Message, item.Error);
    return item.Reply;
}
```

- 写出失败（`ChannelClosedException`）翻译为 `IOException("FTP command send failed: " + command, ex)`，与现状同型同前缀。
- 连接失活：`FtpControlReplyHandler.ChannelInactiveAsync` 调 `_replies.CompleteAdding()` 并置错误，等待方立刻抛 `IOException("FTP control connection closed by server")`，不会挂到超时。
- `ReadReply` 等待期间对端关闭 → 同上。
- 队列容量 32、`Add` 阻塞：FTP 是严格的一问一答（最多一条 226 暂存），不会触发满队列；满队列即客户端不读，阻塞消费线程形成 TCP 反压，属预期。

### 5.3 连接与登录

`Connect()` 保持现有步骤与顺序：

1. `Close()`（幂等）；
2. 建 socket 并 `Connect()`；超时抛 `TimeoutException`（现状在 `Connect` 里抛 `TimeoutException`，消息文本改为框架统一文案，记录在验收文档）；
3. 等 `_pipelineReady`（上限 `ConnectTimeout`），超时抛 `IOException`；
4. `Greeting = ReadReply()`；码不是 `220` → `Close()` + `FTPCommandException`；
5. `Login()` → `Feat()` → `OPTS UTF8 ON`（失败忽略）→ `TYPE I`（失败忽略）；
6. 任一步失败 `Close()` 后原样抛出。

`Feat()` 的能力解析（`REST STREAM`/`SIZE`/`MDTM`/`MLSD`/`EPSV`/`SAEA-OVERWRITE=`）完全不动，输入仍是 `FtpReply.Message`（多行以 `\n` 连接）。

### 5.4 应答解码（`FtpReplyDecoder`）

`LineSplitter` 已按行切帧，多行应答是**跨帧状态**，按 §5.1 不用 `ByteToMessageDecoder`，用 `InboundHandlerAdapter` 持有：

| 状态 | 处理 |
|---|---|
| 无挂起 | 行长度 < 3 或前三位非数字 → 产出错误（`Malformed FTP reply: <line>`）；第 4 字符为 `-` → 挂起，记 `code` 与首行消息；否则直接产出单行 `FtpReply` |
| 有挂起 | 追加到 `raw`；行以 `code + " "` 开头 → 结束并产出（消息按 `\n` 拼接）；否则继续累加 |

- 行的编码用 `_config.Encoding`（默认 UTF-8，允许 GBK）。
- `Code`/`Message`/`Raw` 三个字段的填充口径与现状 `ReadReply()` 一致（`Raw` 多行时以 `\n` 连接）。
- 非法状态码：现状由 `ReadReply` 抛 `IOException`；新版把错误对象交给等待方，`ReadReply` 抛同文案 `IOException`，连接保持可用（与现状一致，未强制断开）。

### 5.5 其它成员

| 成员 | 新版实现 |
|---|---|
| `Connected` | `_client != null && _client.Connected` |
| `ControlHost` | `_client.Socket?.RemoteEndPoint as IPEndPoint`，失败回退 `_config.IP`（现状同） |
| `CommandTimeout` | 仅作为队列等待上限；不再设流超时 |
| `Close()`/`Dispose()` | `_client.Disconnect()`/`Dispose()`，再 `CompleteAdding` 唤醒等待方；幂等 |
| `OpenPassive`/`ConnectData`/`ParsePasv`/`ParseEpsvPort` | **不改**，仍返回 `TcpClient` |

`FtpControlConnection` 构造函数增加 `Action<string, IChannelPipeline> configurePipeline` 参数（`FTPClient` 三处创建点传入）。

---

## 六、公开 API 变化

| 成员 | 变化 | 说明 |
|---|---|---|
| `FTPServer.ConfigurePipeline` | 新增 | `Action<string, IChannelPipeline>`，`Start()` 后赋值抛 `InvalidOperationException` |
| `FTPClient.ConfigurePipeline` | 新增 | 同上，冻结于首次 `Connect()`/`EnsureConnected()`；`CreateSession` 创建的分片会话同样使用该回调 |
| `FTPClient.OnConnected` / `Ondisconnected` | 按 D1 | A：删除；B：保留 |
| `FtpCommandDecoder` 等 5 个 handler | 新增 public | 与 RPC/QueueSocket 的 decoder 同类，必须带 XML 注释 |
| `FtpRawText` | 新增 public | 仅服务端出站使用 |

新增公开成员全部补 XML 注释（体例同 `SAEA.Common/IPHelper.cs`），文件头 banner 按模板填（`CLR版本 netstandard2.0`、`版本号 v26.11.5.1`、新 GUID）。

---

## 七、测试计划（`SAEA.FTP.Tests`，现有 13 条 + 新增 7 条）

| 用例 | 断言 |
|---|---|
| `Server_Greeting_OnConnect` | 裸 `TcpClient` 连上后首行以 `220 ` 开头 |
| `Server_FeatReply_MultiLine` | 登录后 `FEAT` 收到 `211-` 开头、`211 ` 结束的多行块 |
| `Server_UnknownCommand_Replies500` | 未登录发 `FOO` → `500`，连接仍可继续 `USER` |
| `Server_OverlongCommandLine_ClosesConnection` | 发 9KB 无换行 → 连接在超时内关闭 |
| `Client_MultiLineGreeting_Connects` | 假服务器发 `220-Welcome\r\n220 Ready\r\n`，`FTPClient.Connect()` 成功（解析错会抛） |
| `Client_MalformedReply_ThrowsIOException` | 假服务器发 `abc\r\n` → `Connect()` 抛 `IOException` |
| `Client_ServerClosedWhileWaiting_ThrowsIOException` | 假服务器发 greeting 后关闭 → 下一命令抛 `IOException` 而非超时 |

假服务器用进程内 `TcpListener`（随机端口），不依赖外部服务；每条用例独立实例。

既有 13 条（上传/下载/PASV/REST/并行/覆盖策略）全部保留，逐条不得改断言、不得加 `[Ignore]`。

若 D1 取 A，另加 `FTPClient_ConfigurePipeline_AfterConnect_Throws`、`FTPServer_ConfigurePipeline_AfterStart_Throws` 两条冻结用例（模板见 P3-5 的 `*FreezeTests.cs`）。

---

## 八、风险

| 风险 | 处置 |
|---|---|
| 入站 handler 内同步等 `WriteAsync` 造成死锁 | `WriteAsync` 的出站执行不依赖接收泵，已在 `ChannelPipeline.WriteAsync` 确认；用例覆盖每个命令的回复 |
| 传输期间消费循环被阻塞 | 与现状单读循环等价；`Stop()` 走 `DrainTimeout` 截断，不会永久挂住 |
| 控制通道换链期间数据通道仍是裸 `TcpClient` | 本步不宣称 P4 完成；第 3 步一并替换后统一验收 |
| `LineSplitter` 8KB 上限收紧 | 现状无上限，属主动收紧；RFC 959 命令行上限 512 字节，8KB 已留足余量，README 写明 |
| 客户端阻塞等待的线程占用 | 一对一问答、`CommandTimeout` 兜底；与现状 `NetworkStream` 阻塞读一致 |
| 失败回滚 | 改动集中在 `FTPServer.cs`、`FtpControlConnection.cs`、`FTPClient.cs` 与 8 个新增文件，`git checkout -- Src/SAEA.FTP` 可整体回退 |

---

## 九、验收命令

```bash
dotnet build Src/SAEA.FTP/SAEA.FTP.csproj -c Debug --no-incremental   # 0 errors，警告只来自内嵌 Newtonsoft
dotnet test  Src/SAEA.FTP.Tests/SAEA.FTP.Tests.csproj -c Release      # 20 passed（13 存量 + 7 新增）
dotnet build Src/SAEA.Sockets.sln -c Debug                            # 0 errors
```

`FTPTest`（WinExe / net462）本步不改；D1 取 A 时在第 6 步把 `FtpClientForm` 的 `Ondisconnected` 改为链回调，并记录人工 smoke 结果（未实际操作写「未运行」）。