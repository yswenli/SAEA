# Pipeline / Codecs 应用审查与修复记录（2026-11-16）

**审查范围：** 各组件对 `SAEA.Sockets.Pipeline` 的装配方式，以及编码 / 解码 / 分帧（Codecs）在链上的归属与内存处理。
**判定依据：** `docs/superpowers/specs/2026-11-06-socket-pipeline-migration-design.md` 的 §4.1 装配契约、§5.1 解码所有权、§5.3 每连接新建、§5.6 原始字节出站、§5.9 出站写入串行化。
**代码基线：** `41558446`；分支 `fix/pipeline-codecs-review`。

## 1. 结论摘要

- 修复前 12 条判据中 9 条满足、3 条部分不满足（原始字节出站口径、热路径冗余复制、服务端配置跨连接共享）；本次修复后为 10 条满足、2 条部分不满足（仅剩判据 1 的设计允许路径与判据 12 的必要复制），无阻断项。
- 审查另出 1 项应修与 6 项建议；本次修掉 4 项（1 应修 + 3 建议），其余进第 7 节。
- 9 个受影响库 Release 重构建 `errors=0`；10 个测试工程合计 518 通过 / 0 失败 / 0 跳过。
- 提交序列见文末「提交」段；代码改动与注释修正在前，记录与勘误在后。未合入 master、未推送。

## 2. Pipeline / Codecs 采用现状

### 2.1 采用方式

| 组件 | 接入方式 | 关键位置 |
|---|---|---|
| Http | 流模式接链 | `Src\SAEA.Http\Base\Net\HttpSocket.cs` → `ConfigurePipeline(...)` |
| WebSocket | 流模式接链（服务端与客户端两条路径） | `Src\SAEA.WebSocket\Core\WSServerImpl.cs` → `ConfigureConnection(...)`；`Src\SAEA.WebSocket\WSClient.cs` → `ConfigureConnection(...)` |
| FTP | 流模式接链，控制通道与数据通道各一条链 | 服务端 `Src\SAEA.FTP\FTPServer.cs` → `ConfigureConnection(...)`；客户端 `Src\SAEA.FTP\Net\FtpControlConnection.cs`、`FtpDataConnector.cs`、`FtpDataListener.cs` |
| MVC | 间接接链 | `Src\SAEA.MVC\WebSocketsFeature.cs` → `MVCWebSocketConnection` → `IWebSocketTransport.Send`（`Src\SAEA.Http\Model\IWebSocketTransport.cs`）→ `Src\SAEA.Http\Base\Net\HttpWebSocketTransport.cs` → `HttpConnectionSession.SendRaw` → `ChannelPipeline.WriteAsync(new RawFrame(data, null))` |
| RPC | 流模式接链 | `Src\SAEA.RPC\Net\RServer.cs`、`RClient.cs` → `CreateOptions()` 与 `ConfigureConnection(...)` |
| FileSocket | 流模式接链 | `Src\SAEA.FileSocket\Server.cs`、`Client.cs` |
| QueueSocket | 流模式接链 | `Src\SAEA.QueueSocket\QServer.cs`、`QClient.cs` |
| MessageSocket | 流模式接链 | `Src\SAEA.MessageSocket\MessageServer.cs`、`MessageClient.cs` |
| MQTT | `UseStream` 但不接链，自持流读写 | `Src\SAEA.MQTT\Implementations\MqttTcpServerListener.cs` → `_serverSokcet_OnAccepted`（`Task.Run` 内阻塞整会话）；`MqttTcpChannel.cs` 调 `UseStream()` 后直接读写 `_stream` |
| Socket5 | `UseStream` 但不接链，阻塞读 | `Src\SAEA.Socket5\Server\Socks5Server.cs`；`Protocol\Socks5Codec.cs` 的 `ReadExactly` |
| DNS | UDP packet；TCP 自写 2B 长度前缀 | `Src\SAEA.DNS\DnsServer.cs`；`Coder\UdpRequestCoder.cs`、`Coder\TcpRequestCoder.cs` |
| RedisSocket | packet 旧路径 | `Src\SAEA.RedisSocket\Base\Net\RClient.cs` 继承 `BaseClient`，上下文为 `BaseContext<RedisCoder>` |
| Audio.Net | packet 旧路径 | `Src\SAEA.Audio.Net\Net\TransferServer.cs`（`.UsePacket()`）、`TransferClient.cs`（`.UsePacket(bContext)`） |
| P2P | UDP packet；TCP/TLS `UseStream` 未接链 | `Src\SAEA.P2P\Transport\Rfc4571Framer.cs` 手工分帧 |

### 2.2 Codecs

| 协议 | 分帧器 | 编解码器 | 绑定环节 | 池化 / 复制 | 测试工程 |
|---|---|---|---|---|---|
| Http | `HttpRequestSplitter` | `HttpRequestDecoder`、`HttpResponseEncoder` | 入站解码在链首，出站编码在链首前缀 | 只复制请求体；升级后的池化帧 `Retain()` 交 `HttpRawFrameSinkHandler`，该处再整帧 `ToArray()` | `SAEA.Http.Tests` |
| WebSocket | `WsHandshakeSplitter`、`WsFrameSplitter`、`SwitchableSplitter` | `WsHandshakeHandler`、`WsFrameDecoder`、`WsMessageEncoder` | 握手完成后切到 `WsFrameSplitter`，解码器紧随握手处理器 | 超过 `MemoryPoolManager.SmallThreshold` 的负载 `Rent`/`Return`，否则 `new byte[]`；分片重组用容量翻倍 `byte[]` | `SAEA.WebSocket.Tests` |
| FTP 控制 | `LineSplitter` | `FtpCommandDecoder`、`FtpReplyDecoder`、`FtpCommandEncoder`、`FtpReplyEncoder` | 命令解码后交服务端命令处理器，回复编码在链首 | 行文本 `Encoding.UTF8.GetString(bytes.ToArray())`，复制后即丢帧 | `SAEA.FTP.Tests` |
| FTP 数据 | `TakeAvailableSplitter` | `FtpDataChunkDecoder`、`FtpDataChunkEncoder` | 数据块不派发下游，解码器直接写入 `ChannelStreamAdapter` 有界队列 | `FtpDataChunk.CopyFrom` 深拷贝；队列满是数据通道背压点 | `SAEA.FTP.Tests` |
| RPC | `RpcFrameSplitter`（1B 类型 + 4B 总长） | `RpcMessageDecoder`、`RpcMessageEncoder` | 入站解码、出站编码 | 各字段 `ToArray()` 复制；解码器二次校验长度字段与实帧一致 | `SAEA.RPC.Tests` |
| FileSocket | `FileFrameSplitter`（0 / 8 / +1 / 0） | `FileMessageDecoder`、`FileMessageEncoder` | 入站解码、出站编码 | 帧体 `ToArray()` 复制 | `SAEA.FileSocket.Tests` |
| QueueSocket | `QueueFrameSplitter`（1 / 4 / -4 / 0） | `QueueMessageDecoder`、`QueueMessageEncoder` | 入站解码、出站编码 | 负载走 `MemoryPoolManager.RentPooled` | `SAEA.QueueSocket.Tests` |
| MessageSocket | `MessageFrameSplitter`（0 / 8 / +1 / 0） | `MessageDecoder`、`MessageEncoder` | 入站解码、出站编码 | 帧体 `ToArray()` 复制 | `SAEA.MessageSocket.Tests` |
| DNS | 无（UDP 一包一报文，TCP 2B 长度前缀） | `UdpRequestCoder`、`TcpRequestCoder`、`FallbackRequestCoder`、`NullRequestCoder` | socket 回调或流读写内直接编解码 | 无池化 | 无 |
| Socket5 | 无 | `Socks5Codec` | 同步 `ReadExactly` | 无池化 | 无 |
| RedisSocket | 无 | `Core\RedisCoder`（RESP 解析）、`Core\Stream\RedisCoder`、`Base\Net\RedisCoder`（泛型占位） | packet 路径整包读写 | 无池化 | 无（`SAEA.RedisSocketTest` 为交互式演示，需本地 Redis） |
| Audio.Net | 无（一包一帧） | `Core\*ChatCodec.cs`（ALaw / G722 / Gsm610 / MuLaw / Speex / Pcm 等） | packet 回调内编解码 | 无池化 | 无 |
| P2P | `Rfc4571Framer`（2B 帧头） | `DataPlaneCodec`、`Rfc4571Framer` | 自读流后手工分帧 | 无池化 | `SAEA.P2P.Tests`（本次未运行） |

DNS、Socket5、RedisSocket、Audio.Net 四个目录下目前没有测试工程。

## 3. 12 条判据裁定

下表是修复前的裁定。判据 7 由本次修复转为满足；判据 1 与判据 12 各留一处，前者是设计允许的原始字节路径，后者是必要的整帧复制。

| # | 判据 | 裁定 | 证据 |
|---|---|---|---|
| 1 | 出站字节只经 `RawFrame` 出链，响应编码归编码器（§5.6） | 部分不满足 | `HttpResponse.WriteRaw` → `HttpConnectionSession.SendRaw` 直接 `new RawFrame(data, null)`，不经 `HttpResponseEncoder`。设计 §5.6 把这种写法列为允许的第二类原始字节路径，属口径偏差而非缺陷 |
| 2 | 出站是链首连续前缀，编码器排在串行器之前（§4.1） | 满足 | 7 个包各一份 `Net\Pipeline\PipelineWiring.cs`，15 条装配路径都是先 `AddLast(encoder)`、再 `AddLast(new OutboundSerializingHandler())` |
| 3 | 出站串行器唯一且位于出站前缀最右（§5.9） | 满足 | 每条链恰好一个 `OutboundSerializingHandler`，其后全部是入站处理器 |
| 4 | 链尾兜底为 `ExceptionTerminalHandler` 且有启动期校验（§5.7） | 满足 | 15 条装配路径末尾都 `AddLast(new ExceptionTerminalHandler())`；`RequireTerminalHandler` 原为全包默认 `false`，本次在 13 个文件 15 处显式开启 |
| 5 | 解码所有权：基类不释放输入帧，输出默认自带内存，跨回调持有需 `Retain`（§5.1） | 满足 | `ByteToMessageDecoder.ChannelReadAsync` 只做解码与派发，输入帧由执行器按队列条目释放 |
| 6 | 半包留在泵里，解码器不自行累加（§5.1、§六） | 满足 | 各 `*Decoder` 只接 `PooledMessage` 完整帧，残缺字节留在执行器的每连接缓冲 |
| 7 | 每连接一个可切换分帧器与切换屏障（§5.3） | 部分不满足 | 可切换分帧器（Http 的 `SwitchableSplitter`、WebSocket 的 `WsHandshakeSplitter` + `WsFrameSplitter`）确为每连接新建，满足；5 个服务端的 `ChannelPipelineOptions` 与无状态分帧器实例跨连接共享，违反 §5.3，本次已修（见 4.1） |
| 8 | 有状态分帧器在连接结束清空 | 满足 | `FrameSplitterHandler.Dispose` → `_splitter.Reset()` |
| 9 | 协议错误上链而非就地吞掉（§八.F） | 满足 | 解码器抛 `FrameProtocolException`，由链尾 `ExceptionTerminalHandler` 收口 |
| 10 | 原始字节出站与普通写共用关闭 / 写窗口状态机（§5.8） | 满足 | `SendRaw` 与普通响应写（`WriteBytes`）都汇到 `WriteBytesAsync` 的同一条 `ChannelPipeline.WriteAsync(new RawFrame(...))`，共用 `_pendingWrites` 计数与排空等待 |
| 11 | 帧上限、读缓冲、最大缓存在每连接配置中显式给出（§4.6） | 满足 | 各 `CreateOptions()` 显式设置分帧器的 `MaxFrameLength` 与 `MaxBufferedBytes`、`ReadBufferSize`，解码器再校验长度字段 |
| 12 | 热路径不做整帧冗余复制（§5.1 所有权） | 部分不满足 | `HttpRawFrameSinkHandler` 整帧 `ToArray()`（必要：帧要跨回调交给业务）；`WSCoder` 接收路径再整段复制一次（多余，本次已修） |

## 4. 本次修复

### 4.1 服务端通道链配置改为每连接新建

- **问题：** 5 个服务端把 `ChannelPipelineOptions` 与分帧器放在实例字段上，所有连接共用一份配置对象，与 §4.1「配置按连接提供」的装配约定不符。
- **改法：** 每连接调一次 `CreateOptions()`（FTP 另有 `CreateDataOptions()`）新建配置与分帧器，参数值与原字段逐项一致；`MaxFrameLength` 等依赖构造参数的项在构造函数里解析一次存为 `readonly` 字段，工厂仍按同一快照创建。`FTPServer.DataOptions`（internal 属性）随字段一起删除，被动监听器改调 `CreateDataOptions()`。
- **涉及文件：** `Src\SAEA.FTP\FTPServer.cs`、`Src\SAEA.FileSocket\Server.cs`、`Src\SAEA.RPC\Net\RServer.cs`、`Src\SAEA.QueueSocket\QServer.cs`、`Src\SAEA.MessageSocket\MessageServer.cs`。
- **客户端侧不改：** `FtpControlConnection._options/_dataOptions` 与各 `*Client` 的 `_options` 保持实例字段。理由是一客户端同一时刻只有一条链，重连时旧链 `Dispose` 已 `Reset()` 分帧器，不存在跨连接并发复用。
- **事实边界：** 现用 `LineSplitter`、`TakeAvailableSplitter`、`LengthFieldSplitter`、`DelimiterSplitter`、`FixedLengthSplitter` 的字段全是 `readonly` 配置，`Reset()` 为空实现，残余字节存放在执行器的每连接缓冲里，因此共享实例**当前不会串帧**。本次改动消除的是契约违规与「将来引入有状态分帧器即互相干扰」的风险，不是修已发生的线上缺陷。
- **验证：** 5 个库重构建 `errors=0`；FTP 52、FileSocket 11、RPC 15、QueueSocket 16、MessageSocket 17 全通过。

### 4.2 `WSCoder` 新增 `Decode(byte[])` 重载

- **问题：** MVC 的 `MVCWebSocketConnection.OnData` 拿到的是 `byte[]`，却要装进 `ReadOnlySequence<byte>` 调序列重载，序列重载再把整段追加进内部缓冲，每消息多一次整段复制。
- **改法：** 新增 `Decode(byte[])`；公共解码循环抽成 `DecodeBuffered()`，异常清理语义不变。序列重载单段且底层是数组时走 `MemoryMarshal.TryGetArray` 直接追加 `ArraySegment`，多段才逐段追加。`OnData` 改调 `_coder.Decode(data)`。
- **涉及文件：** `Src\SAEA.WebSocket\Model\WSCoder.cs`、`Src\SAEA.MVC\MVCWebSocketConnection.cs`、`Src\SAEA.WebSocket.Tests\Tests\WSCoderTests.cs`。
- **验证：** 新增 `WSCoder_DecodeByteArray_MatchesSequenceInput`（断言两种入口解出的帧数一致、逐帧 `Type` 一致、`Content` 逐字节一致）与 `WSCoder_SplitFrameAcrossCalls_EmitsAfterCompletion`（断言半帧时不产出、补齐后产出 1 帧且 `Type` 与帧体正确）；`SAEA.WebSocket.Tests` 14 通过。

### 4.3 迁移包显式开启 `RequireTerminalHandler`

- **问题：** 该开关默认 `false`，装配漏掉链尾兜底或串行器位置写错时启动期不报错，问题延后到运行期才暴露。
- **改法：** Http、WebSocket 客户端与服务端、FTP 控制链与数据链、RPC、FileSocket、QueueSocket、MessageSocket 的配置全部显式置 `true`。校验语义：`Start()` 检查链尾最后一个是 `ExceptionTerminalHandler`、出站前缀最右恰好一个 `OutboundSerializingHandler`，不满足即抛 `InvalidOperationException` 并直接收口。
- **涉及文件：** `Src\SAEA.Http\Base\Net\HttpSocket.cs`、`Src\SAEA.WebSocket\WSClient.cs`、`Src\SAEA.WebSocket\Core\WSServerImpl.cs`、`Src\SAEA.FTP\FTPServer.cs`、`Src\SAEA.FTP\Net\FtpControlConnection.cs`、`Src\SAEA.RPC\Net\RServer.cs`、`RClient.cs`、`Src\SAEA.FileSocket\Server.cs`、`Client.cs`、`Src\SAEA.QueueSocket\QServer.cs`、`QClient.cs`、`Src\SAEA.MessageSocket\MessageServer.cs`、`MessageClient.cs`。
- **验证：** 15 条装配路径逐条核对链尾无遗漏；13 个文件 15 处开启后全部测试工程通过，无启动期校验失败。

### 4.4 删除失效的 `FTPCoder`，明确 `RedisCoder` 定位

- **问题：** `SAEA.FTP.FTPCoder`（public）仓内零引用，`Decode` / `Clear` 恒抛 `NotImplementedException`；`SAEA.RedisSocket.Base.Net.RedisCoder` 的 `Encode` / `Decode` 同样是永不实现的桩。
- **改法：** 删除 `Src\SAEA.FTP\Net\FTPCoder.cs`；`RedisCoder` 的 `Encode` / `Decode` 改抛 `NotSupportedException` 并补类型级说明，表明它只用于泛型约束，RESP 解析由 `Core.RedisCoder` 负责。
- **涉及文件：** 删除 `Src\SAEA.FTP\Net\FTPCoder.cs`；修改 `Src\SAEA.RedisSocket\Base\Net\RedisCoder.cs`。
- **验证：** `SAEA.FTP`、`SAEA.RedisSocket` 重构建 `errors=0`；`SAEA.FTP.Tests` 52、相关工程全部通过。

## 5. 验证证据

### 5.1 构建

以下构建与测试都跑在 `8e2ae4b5` 的代码状态上；其后 `ed42d354` 只改注释、本次提交只加文档，均未重跑。

| 库 | 命令参数 | errors | 改动文件新增警告 |
|---|---|---:|---:|
| SAEA.FTP | `-c Release -t:Rebuild` | 0 | 0 |
| SAEA.FileSocket | 同上 | 0 | 0 |
| SAEA.Http | 同上 | 0 | 0 |
| SAEA.MVC | 同上 | 0 | 0 |
| SAEA.WebSocket | 同上 | 0 | 0 |
| SAEA.QueueSocket | 同上 | 0 | 0 |
| SAEA.MessageSocket | 同上 | 0 | 0 |
| SAEA.RPC | 同上 | 0 | 0 |
| SAEA.RedisSocket | 同上 | 0 | 0 |

其余警告为存量（CS1591 缺 XML 注释；`SAEA.RedisSocket` 另有 CS3021、CS0067、CS0168、CS0219），改动文件本身无新增警告。

### 5.2 测试

| 工程 | 通过 | 失败 | 跳过 |
|---|---:|---:|---:|
| `SAEA.Common.Tests` | 59 | 0 | 0 |
| `SAEA.Sockets.Pipeline.Tests` | 281 | 0 | 0 |
| `SAEA.WebSocket.Tests` | 14 | 0 | 0 |
| `SAEA.FTP.Tests` | 52 | 0 | 0 |
| `SAEA.FileSocket.Tests` | 11 | 0 | 0 |
| `SAEA.Http.Tests` | 30 | 0 | 0 |
| `SAEA.MVC.Tests` | 23 | 0 | 0 |
| `SAEA.QueueSocket.Tests` | 16 | 0 | 0 |
| `SAEA.MessageSocket.Tests` | 17 | 0 | 0 |
| `SAEA.RPC.Tests` | 15 | 0 | 0 |
| 合计 | 518 | 0 | 0 |

主会话独立复跑 WebSocket 14、FTP 52、MVC 23，与上表一致。

### 5.3 未运行

- `SAEA.MQTT.Tests`、`SAEA.P2P.Tests`：本次未触及对应代码。
- `Src\SAEA.Sockets.TcpTest`：未构建，缺 `Src\packages`，环境原因。

## 6. 公开 API 变更

- `SAEA.FTP.FTPCoder` 已删除（public）。`SAEA.FTP` 与 `SAEA.RedisSocket` 的中英文 README 均未列出该类型（FTP 的「核心类」表也不含它），无需改文档；`SAEA.FTP.csproj` 的 `<Version>` 是否升版由使用者决定，本次未改。
- `SAEA.RedisSocket.Base.Net.RedisCoder` 的 `Encode` / `Decode` 异常类型由 `NotImplementedException` 改为 `NotSupportedException`，若有外部 `catch (NotImplementedException)` 会漏捕。
- `WSCoder.Decode(byte[])` 为新增重载，属增量 API。

## 7. 遗留项（只列，本次未改）

- WebSocket 同一协议两套实现：`WSCoder`（MVC 用）与 `WsFrameDecoder` / `WsMessageEncoder`（链上用）。上限口径（默认 64MB 对 MVC 侧配置 4MB）、异常类型（`InvalidOperationException` 对 `FrameProtocolException`）、分片重组承载（`List<byte>` 对容量翻倍 `byte[]`）都不一致；`WSCoder` 也没有「headerLen + payloadLen == 帧长」的一致性校验。
- 长度前缀分帧 4 份预设重复：File / Message 同为 `0 / 8 / +1 / 0`，RPC / Queue 同为 `1 / 4 / -4 / 0`。
- `Net\Pipeline\` 三件套（`PipelineHostRegistry`、`PipelineWiring`、`PipelineRawByteStream`）在 7 个包各一份，逐字相同（FTP 版多一个可选回调），当前无语义漂移。
- 未迁移包仍是裸流自读自写：MQTT 在 `Task.Run` 内阻塞整会话，Socket5 阻塞 `ReadExactly`，DNS TCP 自写 2B 长度前缀，P2P 自读流加 `Rfc4571Framer` 手工分帧。
- P2P（设计中的 P5）在 `docs/superpowers/plans` 下没有验收记录。
- 无测试工程的编解码：DNS 的 `IRequestCoder` 四个实现、`SAEA.RedisSocket\Core\RedisCoder` 的 RESP 解析、`Socket5Codec`、`Audio.Net` 的帧与音频 codec。
- `WSCoder._buffer` 是 `List<byte>`，容量只增不减；本次只去掉一次冗余复制，未处理容量回收。

## 8. 复核步骤

```powershell
# 1. 基于修复分支建 worktree（路径按需替换；若该分支已被其它 worktree 占用，先移除旧 worktree 或改用 --detach）
git worktree add <你的临时目录>\pipeline-codecs-verify fix/pipeline-codecs-review

# 2. 逐项目重构建（不要构建整个 sln）
foreach ($p in 'SAEA.FTP','SAEA.FileSocket','SAEA.Http','SAEA.MVC','SAEA.WebSocket','SAEA.QueueSocket','SAEA.MessageSocket','SAEA.RPC','SAEA.RedisSocket') {
    dotnet build "Src\$p\$p.csproj" -c Release -t:Rebuild
}

# 3. 逐个跑测试工程（测试工程都不在 sln 内，必须逐个指定工程路径）
foreach ($t in 'SAEA.Common.Tests','SAEA.Sockets.Pipeline.Tests','SAEA.WebSocket.Tests','SAEA.FTP.Tests','SAEA.FileSocket.Tests','SAEA.Http.Tests','SAEA.MVC.Tests','SAEA.QueueSocket.Tests','SAEA.MessageSocket.Tests','SAEA.RPC.Tests') {
    dotnet test "Src\$t\$t.csproj" -c Release
}
```

提交：

- `a1fb98c0` 每连接新建与启动期链尾校验。
- `cb72d03e` `WSCoder` byte[] 重载与用例。
- `8e2ae4b5` 删除 `FTPCoder`、`RedisCoder` 收口。
- `ed42d354` 注释依据修正。
- `6dc21c52` 审查与修复记录。
- `40906c1b` 判据口径与复核命令勘误。

本次勘误提交（消除提交数硬编码）另计一个。