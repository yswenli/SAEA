# SAEA 双向 ChannelPipeline 与可插拔编解码栈 设计文档

**日期：** 2026-11-05
**范围：** 新增 NuGet 包 `SAEA.Sockets.Pipeline`（分帧器 + 编解码器 + 入站/出站通道链 + 每连接消息队列），并在 `SAEA.Sockets` 增加一个中立的输入接管钩子；既有 `OnServerReceiveSpan` / `ICoder` / `BaseCoder` 路径保持原样。
**上游依据：** `docs/superpowers/specs/2026-09-20-readonly-sequence-zero-copy-decoder-design.md`（增量拆帧内核）、`2026-09-21-span-memory-pipeline-design.md` rev.3（Span/Memory 公共面、`netstandard2.0`/C# 8 限制清单、线格式硬不变量）、`2026-09-23-zero-copy-pooling-unification-design.md`（池化原语）。
**参考模型：** DotNetty 的 `ChannelPipeline` / `IChannelHandler` / `ByteToMessageDecoder` / `LengthFieldBasedFrameDecoder` / `MessageToMessageDecoder`。

---

## 一、动机

业务侧在 TCP 长连接里要按顺序做：分帧解析 → 协议编解码 → 鉴权 → 埋点/耗时 → 业务路由；回包同样要编码后发出。当前 `SAEA.Sockets` 只在 `OnServerReceiveSpan` 上投递原始字节块，消费方必须在自己的一大串嵌套 `if` 里既拆帧又做业务：

- 改日志会碰到编解码，加鉴权容易漏异常分支，任一环节无法单独开关或替换；
- 分帧器与序列化焊在 `BaseCoder` 一个类里（`FrameDecoder` 分帧 + `ISocketProtocal`/`Encode` 序列化），换一种序列化就重写一套分帧；
- 同样的「分包 + 日志 + 异常兜底」代码在 `Src/*Test` 和业务消费端各写一遍，正在写第三遍。

目标是把这两件事分别标准化、并让它们以 Handler 形式串在同一条双向通道链上。

---

## 二、目标 / 非目标 / 反目标

### 2.1 目标

1. **双向通道链**：每连接一条 `IChannelPipeline`，入站 head→tail、出站 tail→head；Handler 可增删、可排列、可单独禁用。
2. **两层编解码分离且正交**：`IFrameSplitter` 只切帧、不碰序列化；`ICodec` 只做 `帧字节 ↔ Envelope`，`IMessageCodec` 只做 `Payload ↔ 业务实体`。任一层可换，其它层不动。
3. **接收层只搬字节**：拆帧在接收泵里完成，产出帧后立即入该连接的队列并返回继续读；同连接的多条消息**并发执行**，单条消息内部（解码 → 鉴权 → 路由）严格串行，鉴权等 IO 只阻塞该消息，不阻塞同连接其它消息。
4. **脱离 socket 可复用**：核心包不引用 `SAEA.Sockets`，可单独单测，也可用于非 socket 的字节流。
5. **未启用即无行为变化**：未启用通道链的既有用户感知不到任何行为变化。措辞是"语义不变、代码有改动、需全量回归"，**不是"一行不改"**——接收闸门要改成双来源（§6.2），`ProcessReceive` 是所有连接共用的路径。

### 2.2 非目标

- 不改既有 `OnServerReceiveSpan` 的语义、时序、线格式；不改 `ICoder` / `BaseCoder` / `FrameDecoder` 的实现与可见性。
- 不迁移 MQTT / WebSocket / Redis / FTP / RPC 等内部消费方到新链。
- 不做 UDP 通道链（v1 仅面向 TCP 字节流；UDP 数据报语义另行设计）。
- **不做客户端通道链**：`SaeaClientSocket` 有同构的 `StartSpanPump`，v1 有意不接；客户端配置了 `UseInputHandler` / `UseConnectionOpened` 时在**客户端构造**阶段抛 `NotSupportedException`（明确不支持，v2 再补），**不做静默忽略**——"配了但悄悄不生效"是排障噩梦。服务端未开 `UseStream` 同样直接抛错（见 §6.1）。
- 不做服务端 bootstrap、EventLoop 抽象、连接池、自动重连、TLS 特化（链位于明文侧，TLS 由既有 BouncyCastle 通道处理）。
- 不引入 DotNetty 依赖，不做 `ByteBuf` 的全套引用计数语义。
- 不做 `net8.0` 多目标。

### 2.3 反目标（做得再"对"也算失败）

- 为了兼容旧写法，在链内部留 `if (isLegacy) ... else ...` 分叉。
- 把链做成"第二个框架"：为了通用性引入 `IEventLoop`、`AttributeKey` 树、异步 Future 体系。
- 静默吞异常：消息处理路径上的 handler 抛错都必须冒泡到 `ExceptionCaught`，不允许 `catch {}` 后继续。唯一例外是 `ChannelReadCompleteAsync`（批量通知，不是消息处理）——它按 §5.1 记 `Warn` 后继续，**不**断连。
- 为了微优化牺牲 API 可读性：链上所有公开类型都要有一句话说得清职责。

---

## 三、硬约束（实现前已验证）

| 约束 | 结论 | 影响 |
|---|---|---|
| 目标框架 | `netstandard2.0`，`LangVersion 8.0` | 禁用 `record` / `init` / file-scoped namespace / 可空引用类型标注；公开方法用 `Task`，不用 `ValueTask`（`System.Threading.Tasks.Extensions` 非直接依赖） |
| 现有依赖 | `SAEA.Common` 已引 `System.Threading.Channels 10.0.14`、`System.IO.Pipelines 10.0.14`、`System.Memory 4.6.3`、`protobuf-net 3.4.40`、`Nito.AsyncEx 5.1.2` | 新包无需新增第三方依赖；`protobuf-net` 可直接做 Protobuf codec |
| JSON | `SAEA.Common/Newtonsoft.Json/` 是内嵌源码（MIT） | JSON codec 不加包，但**不得**修改内嵌源码 |
| `SequenceReader<byte>` | 不可用 | 分帧器只能基于 `ReadOnlyMemory<byte>` 切片 + `Span.IndexOf` 实现 |
| 线格式 | 8B 小端 **body 长度** + 1B Type + body，`P_LEN=8` / `P_Type=1` / `P_Head=9` | 默认 `LengthFieldSplitter` / `LengthFieldFrameEncoder` 必须逐字复刻（`BaseSocketProtocal.WriteTo` 写的是 body 长度，不是整帧总长），并用 `BaseCoder` 做 parity 测试 |
| 内存租借 | `SAEA.Common/Caching/MemoryPoolManager.cs` 提供 `Rent` / `Return` | 帧体默认从池里借、`try/finally` 归还；仅小于 `SmallFrameInlineThreshold` 的帧用精确 `new byte[len]`（`Rent` 对 <4KB 请求按 4KB 发放，小帧走池是反向放大，见 §5.4） |
| 现有接收泵 | `SaeaServerSocket.StartSpanPump` 是 `connection.Input`（`PipeReader`）的唯一 reader；末尾 `AdvanceTo(buffer.End)` 后内存立即失效 | 帧要在泵内拷出才能跨 `await` 存活；一条 Pipe 只能有一个 reader，通道链启用即接管该泵 |
| PipeReader 的存在条件 | `ProcessAccepted` 仅在 `if (_useStream || _tlsEnabled)` 时构造 `SaeaConnection`（`SaeaServerSocket.cs:269`）；纯 `.UseIocp()` 路径没有 `SaeaConnection`、没有 `PipeReader`，直接在 IOCP 回调里 `Invoke` | `UseInputHandler` **强制要求 `.UseStream()`**；未开启时在 `SocketOptionBuilder.Build()` 抛明确异常，不做自动切换 |
| 背压开关 | `SaeaConnection._receivePaused` 已被 Pipe 写端背压占用：`PauseReceive(flushTask)` 置位、flush 续体清位并回调 `_receiveResume`；`ResumeReceive` 里有 `if (connection.IsReceivePaused) return;` | **不能**把应用级暂停复用成同一个 bool：flush 续体会清掉应用暂停，应用 Resume 也可能被这条 early-return 吞掉。必须引入独立的应用级暂停源（见 §6.2） |
| 发送所有权 | `SaeaServerSocket.SendAsync(string, ReadOnlyMemory<byte>)` 是 `void` + fire-and-forget；只有 `SendAsync(string, ReadOnlyMemory<byte>, IDisposable owner)` 会在 IOCP 发送完成回调里释放 owner | 出站缓冲必须经 owner 语义交接，`IRawByteStream.SendAsync` 需带 owner 参数（见 §6.1） |
| 分包位置 | `IServerSocket.GetStream(IUserToken)` 已有，`SaeaConnection.Input` 是 `PipeReader` | 钩子只需暴露 reader + 控制面，不必改 socket 内核 |

---

## 四、包结构

```
Src/SAEA.Sockets.Pipeline/            # 新包，netstandard2.0 / LangVersion 8.0
  SAEA.Sockets.Pipeline.csproj        # Version 26.11.5.1（与同批包对齐）
  Abstractions/
    IChannelHandler.cs                # IChannelHandler / IChannelInboundHandler / IChannelOutboundHandler
    IChannelPipeline.cs
    IChannelHandlerContext.cs
    IChannel.cs                       # Id / IsActive / Attributes / Close
    IRawByteStream.cs                 # 传输无关的字节流接入面
    ICodec.cs                         # 第一层：帧 ↔ Envelope
    IMessageCodec.cs                  # 第二层：Payload ↔ 业务实体（运行期 Type）
    IFrameSplitter.cs
    IFrameEncoder.cs                  # 出站镜像：body + type → 完整帧
    IMessageHandler.cs
    ITypeRegistry.cs                  # type code ↔ CLR 类型（双向映射）
    TypeRegistry.cs                   # 默认实现（Register 返回 void，与接口一致）
    IChannelGuard.cs                  # 连接级流量防护钩子
    NullChannelGuard.cs               # 默认恒 true 实现
    ChannelTraffic.cs                 # 传给 IChannelGuard 的只读快照
    IPipelineHost.cs                  # channelId → IChannel 查询（OnAccepted 写初始状态）
    ChannelClosedException.cs         # 连接已关闭仍写数据时抛出
    MessageTimeoutException.cs        # 单消息超时；链尾只 Warn、不关连接
    FrameProtocolException.cs         # 分帧/协议级错误（核心包自有，不能引用 SAEA.Sockets.Model.KernelException）
    FrameConstants.cs                 # P_LEN=8 / P_Type=1 / P_Head=9（核心包自带，不引用 SAEA.Sockets）
    Envelope.cs                       # Type + Payload（独立堆拷贝）
    ChannelHandlerAdapter.cs          # 基类：IsSharable 默认 false
    InboundHandlerAdapter.cs          # 入站基类：默认透传给下游
    OutboundHandlerAdapter.cs         # 出站基类：默认透传给下一出站 handler
  Buffers/
    FrameAccumulator.cs               # 连续池化累加器（Append / Buffered / BufferedLength / Consume / Reset）
    PooledMessage.cs                  # 帧体所有权 + Retain/Release
    EncodedBody.cs                    # 出站中间体：type + body（仅 CodecEncoder→FrameEncoder）
    TransportFrame.cs                 # 出站终态：完整帧 + owner（仅 FrameEncoder→head sink）
  Splitters/
    LengthFieldSplitter.cs            # 默认：8B 小端 body 长度 + 1B type（等价 FrameDecoder）
    DelimiterSplitter.cs              # 单/多分隔符 + 最大帧保护
    LineSplitter.cs                   # \n / \r\n，DelimiterSplitter 的特例
    FixedLengthSplitter.cs            # 定长
    LengthFieldFrameEncoder.cs        # 出站：8B 小端 body 长度 + 1B type + body
  Handlers/
    FrameSplitterHandler.cs           # 可选：非 executor 的纯链式分帧；标准装配由泵分帧，不装它
    CodecDecoderHandler.cs            # ICodec.Decode：帧 → Envelope
    EnvelopeUnwrapHandler.cs          # Envelope → 具体实体（查 ITypeRegistry）
    EnvelopeWrapHandler.cs            # 具体实体 → Envelope（出站）
    FramePassthroughHandler.cs        # 原始帧字节分支（Retain/Release 自有出口）
    CodecEncoderHandler.cs            # Envelope → ICodec.Encode 写 body，产出 EncodedBody（出站）
    FrameEncoderHandler.cs            # body + type → IFrameEncoder 补包头（出站）
    MessageDispatcher.cs              # 按消息类型路由到 IMessageHandler
    InboundLoggingHandler.cs          # 入站埋点与耗时（放在鉴权之后）
    OutboundLoggingHandler.cs         # 出站埋点：EncodedBody 的 type/body 长度与耗时（注册在出站前缀内）
    ExceptionTerminalHandler.cs       # 链尾：按异常类型记录并决定是否关连接（超时/已关闭只记、不断连）
  Runtime/
    ChannelPipeline.cs                # sealed 双向链实现 + 静态工厂 Create(options)（校验 + 建链）
    DefaultChannelHandlerContext.cs
    ChannelPipelineOptions.cs         # 队列容量 / 并发上限 / 超时 / 缓冲上限 / 断开策略
    DisconnectDrainPolicy.cs          # CancelPending / DrainInFlight
    ChannelPipelineExecutor.cs        # 每连接：泵 + 有界队列 + 并发消费
    DefaultChannel.cs                 # IChannel 默认实现（ConcurrentDictionary 背衬）
  Codecs/
    EnvelopeCodec.cs                  # [1B type][payload] ↔ Envelope（ICodec），不做序列化
    JsonMessageCodec.cs               # Newtonsoft（内嵌），Payload ↔ 实体（IMessageCodec）
    ProtobufMessageCodec.cs           # protobuf-net，Payload ↔ 实体（IMessageCodec）
    RawCopyCodec.cs                   # ICodec：拷贝帧字节为 byte[]，用于调试与基准
```

`SAEA.Sockets` 侧只增加一个钩子与一处接收闸门改动（见 §6.1/§6.2），不改任何既有 API 的签名与语义。

**工程与打包（按仓库既有惯例，实现时照抄同级包）：**

- 库工程 `Src/SAEA.Sockets.Pipeline/SAEA.Sockets.Pipeline.csproj`：`netstandard2.0`、`LangVersion 8.0`、`<Version>26.11.5.1</Version>`、`GeneratePackageOnBuild=true`、`PackageReadmeFile` 指向本项目 `README.md`。依赖：`SAEA.Common`，并**显式**引用 `System.IO.Pipelines` / `System.Threading.Channels` / `System.Memory`（版本与 `SAEA.Common` 对齐：10.0.14 / 10.0.14 / 4.6.3）。`PipeReader` / `Channel<T>` 出现在公开 API 上，不能只靠传递依赖；不新增任何第三方包。
- **库工程入 `Src/SAEA.Sockets.sln`**（与其它组件一致，构建即产出 nupkg）；演示工程 `Src/SAEA.Sockets.PipelineTest/`（控制台，`net10.0`，入 sln）。
- 单测 `Src/SAEA.Sockets.Pipeline.Tests/`（MSTest，`net10.0`，按仓库惯例**不入 sln**，用 `dotnet test <csproj>` 单独跑）。
- 本包 `README.md` + `README.en.md` 双份，内容涵盖：许可证/安装、`UseInputHandler` 接入片段、两层编解码分层图、`IPipelineHost` 用法、背压与断开策略、`OnAccepted` 禁阻塞 IO 的约束。

---

## 五、核心抽象

### 5.1 Handler 与上下文

```csharp
public interface IChannelHandler
{
    /// <summary>无连接状态、可跨连接共享实例时返回 true；默认 false。</summary>
    bool IsSharable { get; }
}

public interface IChannelInboundHandler : IChannelHandler
{
    Task ChannelReadAsync(IChannelHandlerContext ctx, object msg);

    /// <summary>异常沿链传播的入口；链尾 handler 负责记录并关闭连接，默认实现继续往下传。</summary>
    Task ChannelExceptionCaughtAsync(IChannelHandlerContext ctx, Exception ex);

    /// <summary>
    /// 一轮 Socket 读取处理完毕的通知：一次 ReadAsync 可能拆出多条帧，
    /// 本方法**只为这一轮触发一次**，不是每条消息之后各触发一次。
    /// 时机：本轮所有帧都已**入队**之后，按 head→tail 顺序调用一次（不保证执行链已开始，更不代表已完成）。
    /// 若某轮因队列满被截断，该轮通知与下一次成功排空的轮次合并（仍保证每次 ReadAsync 至多 1 次）。
    /// 与"消息执行完成"无关——消息并发执行、完成顺序不定，不能用它判断某批消息已处理完。
    /// </summary>
    Task ChannelReadCompleteAsync(IChannelHandlerContext ctx);
}

public interface IChannelOutboundHandler : IChannelHandler
{
    Task WriteAsync(IChannelHandlerContext ctx, object msg);
}

public interface IChannelHandlerContext
{
    string ChannelId { get; }                 // 连接标识；SAEA.Sockets 适配层即 IUserToken.ID
    IChannel Channel { get; }
    IChannelPipeline Pipeline { get; }
    IChannelHandler Handler { get; }
    /// <summary>
    /// 本消息的执行令牌：连接进入收口（CancelPending）或本消息超时时取消。
    /// 取消只是协作式请求，不强行终止线程；handler 内所有可取消的 IO/等待都应传入它。
    /// </summary>
    CancellationToken CancellationToken { get; }
    Task FireChannelReadAsync(object msg);    // 传到下一入站 handler
    Task WriteAsync(object msg);              // 交给下一出站 handler（tail→head）
    Task FireExceptionCaughtAsync(Exception ex);
}
```

- 入站顺序 head→tail，出站顺序 tail→head（与 DotNetty 一致）。
- **每个执行包一个 `CancellationToken`**：由 executor 为每条消息提供消息级令牌，执行包 `finally` 里收口。`MessageTimeout == Zero`（默认）时它就是连接级 `_connectionToken.Token`，不建 linked `CancellationTokenSource`；`MessageTimeout > Zero` 时才建 linked CTS（连接级 ct + 超时）并 `Dispose`。**context 是链节点、跨消息复用，不能把令牌存在 context 上**：需要超时时 executor 在 `FireChannelReadAsync` 前把该消息的 token 写入 `AsyncLocal<CancellationToken>`，`ctx.CancellationToken` 读当前异步流的值（消息之外默认是连接级 ct）。这样"断连取消"与"单消息超时"都只影响该消息，不需要 handler 自己找 token；未配置超时时它仍随连接收口取消。

**`IChannelPipeline`（此前只被引用、未定义，现补齐）：**

```csharp
public interface IChannelPipeline : IDisposable
{
    IChannelPipeline AddLast(IChannelHandler handler);
    IChannelPipeline AddFirst(IChannelHandler handler);
    IChannelPipeline Remove(IChannelHandler handler);
    IReadOnlyList<IChannelHandler> Handlers { get; }   // 快照，用于校验与日志

    Task FireChannelReadAsync(object msg);             // 入站唯一入口（泵/executor 调用）
    Task FireChannelReadCompleteAsync();               // 每轮一次，按 head→tail 只发给入站 handler
    Task FireExceptionCaughtAsync(Exception ex);       // head→tail 传播
    Task CloseAsync(string reason);                    // 走 §6.3 收口，幂等
}
```

- `AddLast` / `AddFirst` 返回 `this`（**静态类型是 `IChannelPipeline`**），便于链式。C# 8 **没有协变返回类型**，所以 `ChannelPipeline` 不能另外声明一个返回自身的同名方法（会与接口签名冲突）；需要 `Attach` / `Dispose` 等具体成员时，先 `var pipeline = ChannelPipeline.Create(options)` 拿到具体类型，再调用 `AddLast`（忽略返回值），不要在同一条表达式里一路链掉。`Dispose` 已提到接口上，链式结果也能 `using`。
- **`FireChannelReadAsync` 只允许泵/executor 调用**；handler 内部往下传必须走 `ctx.FireChannelReadAsync(msg)`，否则会跳过链位置、破坏顺序。同理，出站只能走 `ctx.WriteAsync`，不允许直接调 `pipeline` 上的方法（出站入口在 `IChannelPipeline` 上刻意不暴露）。
- `Handlers` 返回快照（`ToArray`），避免链在遍历中被修改导致 `InvalidOperationException`；`ChannelReadCompleteAsync` 通知与异常传播都基于快照。
- `ChannelReadAsync` 返回的 `Task` 必须被等待：任一 handler 不等待下游就返回，等价于"放行"但不保证顺序，属误用，文档需明写。

**pipeline 的生命周期（每连接一个，不跨连接复用）：**

- `ChannelPipeline` 是**每连接对象**：`ChannelPipeline.Create(options)` 只建链并做结构校验，不绑定传输层；executor 创建后通过内部 `Attach(IRawByteStream stream, ChannelPipelineExecutor executor)` 注入链头出口与收口入口。`Attach` 之前 `ctx.WriteAsync` 抛 `InvalidOperationException`（链还没接传输层），`CloseAsync` 只把链标记为关闭。
- 因此 §5.5 的装配示例展示的是**一次装配**；真实服务在 `UseInputHandler` 回调里按同一份 `options` 和 handler 工厂为每条连接重新 `Create` 一次。非 sharable handler 必须每次 `new`，不能把示例里的实例直接跨连接复用（§5.1.1 会抛）。
- `ChannelPipeline` 实现 `IDisposable`：`Dispose()` 对链上剩余 handler 逐个 `Count--`（幂等），由收口第 6 步调用。未 `Attach` 就被丢弃的 pipeline 由创建者负责 `Dispose`，否则 `IsSharable` 计数会泄漏。

**异常转发的落点（handler 不需要自己写 try/catch）：**

- **入站吞、出站抛，两条方向的异常契约不同**（决定 owner 能否正确归还）：
  - `DefaultChannelHandlerContext.FireChannelReadAsync` 在调用下一入站 handler 时 `try/catch`，捕获后走 `FireExceptionCaughtAsync`，**不再向调用方抛出**（同一条消息执行包只上报一次，避免 executor 与 context 重复上报）。executor 入口的 `pipeline.FireChannelReadAsync` 也包一层，保证链首 handler 的异常同样到达链尾。
  - `DefaultChannelHandlerContext.WriteAsync` **不吞异常**：出站调用失败（编码抛错、head sink 的 `SendAsync` 抛 `ChannelClosedException`）必须冒泡给发起方——`FrameEncoderHandler` 正是靠这个 `catch` 决定 `TransportFrame` 归谁释放（§5.4 第 5 条）。异常不用在这里重复上报：它沿调用栈回到业务 handler、再回到入站包装层，由上面那条统一 `FireExceptionCaughtAsync` 上报一次。业务若想就地补偿，可在自己的 `try/catch` 里接住。
  - **`OperationCanceledException` 是唯一不转成 `ExceptionCaught` 的异常**：`ctx.FireChannelReadAsync` 与 `pipeline.FireChannelReadAsync` 都必须让它原样冒泡到执行包，由 executor 按 `_closing` 与消息 token 区分"单消息超时"和"连接收口"（§6.3）；否则超时会被链尾误判成普通 handler 异常而断连。
  - 由此得到一条实现约束：**不要在 `WriteAsync` 里"记录后返回成功"**，那会让编码器以为发送成功而提前归还缓冲，且丢包对业务完全不可见。
- `FireExceptionCaughtAsync` 只向 tail 方向传播：executor/入站包装层的上报**从链首开始**；handler 自己调用 `ctx.FireExceptionCaughtAsync(ex)` 时从**该节点之后**开始，不会绕回自己（否则 handler 会收到自己刚抛出的异常，形成自我循环）。链尾 `ExceptionTerminalHandler` 是唯一决定"记录 / 是否关连接"的地方（§5.5）。`ChannelReadCompleteAsync` 的异常由泵单独记录并吞掉，不走这条链（§5.1 上文）。
- **`ChannelReadCompleteAsync` 的粒度与时机**（B1 定死，不再留两义）：一次 Socket 读取（一次 `PipeReader.ReadAsync` 返回的缓冲）可能拆出 **N 条**帧，`ChannelReadCompleteAsync` 只为这一轮调用 **1 次**。触发点 = **本轮所有帧都已入队之后**，由泵按 head→tail 顺序调用一次；它不保证执行链已开始，更**不等消息执行完成**（入队与消费者拾取是两个任务，无法在这一刻同步确认"已开始"）。若本轮因队列满被截断（有帧还留在 `FrameAccumulator`），本轮**不触发**，与下一次成功排空的轮次合并——语义仍是"每次 `ReadAsync` 至多 1 次"，但不牺牲"全部入队"的前提。因此：

  - 它可用于"本轮批量 flush / 本轮读取耗时 / 本轮帧数统计"；
  - **不可**用于"这批消息已处理完"——执行是并发的、完成顺序不定（§6.3）；
  - 泵会 `await` 它再进入下一次 `ReadAsync`，所以 handler 内**只允许做轻量同步工作**（攒批、记指标），禁止放业务 IO，否则会拖慢该连接的读取。这条与 §2.1 目标 3"接收层只搬字节"一致。
  - **本轮产出 0 帧（只有半包）时不调用**；不允许用"空通知"制造心跳。
  - `ChannelReadCompleteAsync` 抛异常由泵直接 `Warn` 记录（带 `ChannelId` 与 handler 类型）后继续下一轮，**不走 `ExceptionCaught`、不打断泵、不关闭连接**——它只是通知，不是消息处理的一环；走 `ExceptionCaught` 会被链尾当普通 handler 异常断开连接，与"通知失败不该断连"矛盾。
- **C# 8 / `netstandard2.0` 没有 default interface methods**：接口保持最小，可省略成员一律由适配器基类提供。业务 handler 继承 `InboundHandlerAdapter` / `OutboundHandlerAdapter`，不要直接实现接口：

  ```csharp
  public abstract class ChannelHandlerAdapter : IChannelHandler
  {
      public virtual bool IsSharable => false;
  }

  public abstract class InboundHandlerAdapter : ChannelHandlerAdapter, IChannelInboundHandler
  {
      public virtual Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
          => ctx.FireChannelReadAsync(msg);          // 默认透传（对齐 DotNetty）
      public virtual Task ChannelReadCompleteAsync(IChannelHandlerContext ctx)
          => Task.CompletedTask;
      public virtual Task ChannelExceptionCaughtAsync(IChannelHandlerContext ctx, Exception ex)
          => ctx.FireExceptionCaughtAsync(ex);       // 默认继续往链尾传
  }

  public abstract class OutboundHandlerAdapter : ChannelHandlerAdapter, IChannelOutboundHandler
  {
      public virtual Task WriteAsync(IChannelHandlerContext ctx, object msg)
          => ctx.WriteAsync(msg);                    // 默认透传
  }
  ```

  这样写一个 `AuthHandler` 只需 override 一个方法，不必为 `IsSharable` / `ChannelReadCompleteAsync` 填样板。**注意**：默认透传意味着"忘记 override `ChannelReadAsync` 的 handler 什么都不做、消息照样往下走"，比"什么都不做导致消息卡死"更安全，但会让漏写实现变得隐蔽——`InboundHandlerAdapter` 的子类若既不 override 也不该透传，必须在注释里写明。
  另注：`ChannelReadCompleteAsync` 由**泵按链上顺序逐个调用**（不由 handler 转发），所以适配器返回 `Task.CompletedTask` 不会截断该通知；只有 `ChannelReadAsync` 是"自己调下游"的转发式语义。
- 同一 `IChannelHandler` 实例被注册到第二条连接时，若 `IsSharable == false`，`AddLast` 抛 `InvalidOperationException`（实现见 §5.1.1 的活跃注册计数）。
- 异常：入站/出站 handler 抛出的异常一律经 `FireExceptionCaughtAsync` 沿链向后传播，链尾 `ExceptionTerminalHandler` 负责记录日志，并**按异常类型**决定是否关闭连接（见 §5.5）。

**出站 `WriteAsync` 流转契约（三条硬规则）**：

1. **要继续往 head 传，必须 `await ctx.WriteAsync(transformedMsg)`**：handler 做完自己的变换（包装、压缩、加密、改写类型）后，用**变换后的消息**调用一次 `ctx.WriteAsync`，并等待它。不调用即视为本 handler 截断了出站链，消息到此为止。合法的终止点只有链头 head sink；handler 若主动终止，必须是"明确丢弃"（第 2 条），不能是"忘了转发"。
2. **已经丢弃或已在别处完成的消息，禁止再调用 `ctx.WriteAsync()`**：handler 决定丢弃（如鉴权失败不返回业务帧）或已通过其它通道交出结果时，必须直接 `return`，不得再往下传。**handler 不允许直接调用 `IRawByteStream.SendAsync`**——出站唯一出口是链头 head sink（§5.2），绕过它会让 owner 归还散落多处。
3. **同一个 handler 节点对同一条输入消息只允许调用一次 `ctx.WriteAsync()`**：禁止拆包成多条、也禁止先传原消息再传变换结果。需要一发多帧（如推送批量），必须由业务显式构造多条消息各走一次出站，而不是在 handler 内对同一条输入调用两次。不同节点转发同一实例（如日志 handler 原样透传中间载体）是合法的。

违反第 2、3 条会产生重复输出或"幽灵帧"，且难以从抓包定位。因此 `DefaultChannelHandlerContext` 在 `Debug` 构建下做一次轻量检查：同一个 `msg` 实例在**同一个 handler 节点**上只允许穿过一次 `ctx.WriteAsync`。实现用 `[Conditional("DEBUG")]` 私有方法 + `ConditionalWeakTable<object, OutboundWriteMarker>`（引用相等，跨 `await` 线程切换仍有效）：marker 记录 `LastNode` 与写入次数，`LastNode` 仍是当前节点时再次写入即断言，换成别的节点则重置为当前节点；head sink 接受消息后 `Remove` 条目，避免把消息钉在表里。Release 构建下调用点被编译器移除，零开销。

**`IChannel` 与 `Attributes`（实现定义）：**

```csharp
public interface IChannel
{
    string Id { get; }                                   // 连接标识；SAEA.Sockets 适配层即 IUserToken.ID
    bool IsActive { get; }
    IReadOnlyDictionary<string, object> Attributes { get; }
    void SetAttribute(string key, object value);
    object GetAttribute(string key);                     // 未设置返回 null
    bool TryRemoveAttribute(string key, out object value);
    Task CloseAsync(string reason);
}
```

- **背衬**：每连接一个 `ConcurrentDictionary<string, object>`。同一连接的多条消息是并发执行链的（§6.3），所以 `Attributes` 必须线程安全；用普通 `Dictionary` 会在鉴权结果读写上直接爆。
- **生命周期**：随连接创建，随连接关闭清空并置 `IsActive = false`；不做跨连接复用。
- **与 ctx 的关系**：只在 `IChannel` 上暴露一份，**不**在 `IChannelHandlerContext` 上再开一个 `Attributes`（两套存储必然出现"读到的和写进去的不是一份"）。
- **用途**：鉴权结果、会话绑定、按连接限流计数。**禁止**放跨连接共享状态（那是 `IsSharable` 反模式，见 §5.1.1）。
- **与 `OnAccepted` 的衔接**：泵在 `OnAccepted` 之后启动（§6.4），业务可在 `OnAccepted` 里写入初始状态（如"连接已认证"），首个 handler 读到的就是它；`Attributes` 是权威存储，业务不要再另建一份连接字典。

**`IPipelineHost`（此前只被引用、未定义，现补齐）：**

```csharp
public interface IPipelineHost
{
    /// <summary>按下发的 channelId 查该连接的 IChannel；连接未建链或已关闭返回 false。</summary>
    bool TryGetChannel(string channelId, out IChannel channel);

    /// <summary>
    /// 收口第 6 步调用：解除登记并返回该 IChannel（不存在返回 false，幂等）。
    /// 适配层实现此方法时一并把 IsActive 置 false、清空 Attributes。
    /// </summary>
    bool TryRemoveChannel(string channelId, out IChannel channel);

    /// <summary>等价于 channel.CloseAsync(reason)；连接不存在时为 no-op（幂等）。</summary>
    Task CloseAsync(string channelId, string reason);
}
```

- **归属**：`IPipelineHost` 由**适配层实现并持有**（`SAEA.Sockets` 不引用 `SAEA.Sockets.Pipeline`，因此**不能**由 `SocketOptionBuilder` 创建它）；适配层内部维护 `ConcurrentDictionary<string, IChannel>`，连接建立时写入、断开收口第 6 步通过 `TryRemoveChannel` 移除（见 §6.3）。
- **创建时序（关键）**：`IChannel` 必须在 `RaiseAccepted` 内部、**调用 `OnAccepted` 订阅者之前**创建并登记，泵在 `RaiseAccepted` **返回之后**启动。现有代码只在 `RaiseAccepted` 里调用 `_channelManager.Set` 创建 `ChannelInfo`（`Src/SAEA.Sockets/Core/Tcp/SaeaServerSocket.cs` 的 `RaiseAccepted`），所以钩子不能放在 `ProcessAccepted` 的另一处再造一份 `ChannelInfo`——那会得到两个实例。落点是：`RaiseAccepted` 里 `Set` + 赋值 `UserToken` 之后、`if (handler == null) return;` **之前**同步调用 `UseConnectionOpened?.Invoke(channel)`（即使没有 `OnAccepted` 订阅者也必须执行）。适配层在这个回调里创建并登记 `IChannel`；`UseInputHandler` 仍然只负责在 `RaiseAccepted` 之后启动泵。两件事分开，才能同时满足"`OnAccepted` 一定拿得到 channel"与"首帧一定晚于 `OnAccepted`"。回调抛异常时 `RaiseAccepted` 直接向上抛，不再通知 `OnAccepted`、也不启动泵；**但既有 `ProcessAccepted` 的 catch 只调 `OnError`、不关连接，TLS 分支的续体更是无人接住异常**——因此实现必须在两条路径（普通 `RaiseAccepted` 调用点、TLS `ContinueWith` 续体）各自加显式 `try/catch`，在钩子抛异常时 `Disconnect` + `OnError`，不能依赖外层 catch。
- **`UseInputHandler` 与 `UseConnectionOpened` 必须成对配置**：只配前者时 executor 拿不到 `IChannel` / `IPipelineHost`，`SocketOptionBuilder.Build()` 直接抛 `InvalidOperationException`；只配后者是合法的（只登记 channel，不接管读循环），但两者都要求服务端开启 `.UseStream()`，纯 `.UseIocp()` 下配置任一即抛。
- **`OnAccepted` 的参数是 `ChannelInfo` 不是 `IUserToken`**（stream 模式，`SaeaServerSocket.cs:366-379`）。标准写法：

  ```csharp
  socket.OnAccepted += (ChannelInfo ch) =>
  {
      if (host.TryGetChannel(ch.UserToken.ID, out var channel))
          channel.SetAttribute("session", sessionStore.Create(ch.UserToken.ID));
  };
  ```
- 未启用 `UseInputHandler` 时 `IPipelineHost` 不存在（builder 不创建），`OnAccepted` 仍是既有行为，不做兼容层。

### 5.1.1 Handler 注册与 `IsSharable` 计数

`IsSharable` 不能用"实例是否出现过"这种黏性标记：handler 从某条链上 `Remove` 后再 `AddLast` 到另一条链，是完全合法的（无状态日志/编解码 handler 就这么用）。实现必须按**活跃注册数**判定：

```
ConditionalWeakTable<IChannelHandler, Registration>  _registry
Registration { int Count; }        // Count = 当前活跃 pipeline 上对该实例的引用数
```

- `AddLast(handler)`：`Count++`；若 `Count > 1 && !handler.IsSharable` → `Count--` 后抛 `InvalidOperationException`（消息带 handler 类型名）。
- `Remove(handler)`：从链上摘除并 `Count--`。
- **pipeline 释放 / 连接关闭**：对链上剩余的每个 handler 逐一 `Count--`。**这条最容易漏**——漏了以后连接断开不会归还计数，同一个 handler 复用到新连接时会被误判为"已被第二条链占用"。实现放在 `ChannelPipeline.Dispose()` 里（用 `Interlocked` 幂等标志保证只执行一次），由收口第 6 步调用（§6.3）。
- 计数归零后允许 `ConditionalWeakTable` 回收条目，静态表不会把 handler 钉死（否则是内存泄漏）。
- `AddLast` / `Remove` 频率低，用一把 `lock` 足够，不引入无锁结构。
- 同一条 pipeline 内重复 `AddLast` 同一实例：非 sharable 直接抛；sharable 允许（但 `InboundLoggingHandler` 之类重复注册没有意义，文档提示即可）。

### 5.2 分帧器（第一层，不碰序列化）

```csharp
public interface IFrameSplitter
{
    /// <summary>
    /// 从当前连续缓冲里切出下一帧。
    /// 返回 false 表示数据不足，需要继续读取；此时 <paramref name="consumed"/> / <paramref name="frame"/> 无效。
    /// </summary>
    /// <param name="buffered">自上次消费位置起的未消费连续缓冲。</param>
    /// <param name="consumed">本次应从缓冲中消费掉的字节数（含被剥离的包头）。</param>
    /// <param name="frame">产出的帧体切片，背衬缓冲由调用方持有。</param>
    bool TrySplit(ReadOnlyMemory<byte> buffered, out int consumed, out ReadOnlyMemory<byte> frame);

    /// <summary>
    /// 可接受的最大帧长。对默认的 LengthFieldSplitter 指**帧体（body）长度**，总帧长 = P_Head + MaxFrameLength；
    /// 对分隔符/定长分帧器指切出的整帧长度。超限抛 FrameProtocolException（与既有 FrameDecoder 的 maxFrameLength 同口径），防 OOM。
    /// </summary>
    int MaxFrameLength { get; }

    /// <summary>连接断开或握手失败时清空内部状态。</summary>
    void Reset();
}
```

- 分帧器**不产生任何协议元数据**；类型字节等头部字段由参数决定是剥掉还是留在帧内（对齐 `initialBytesToStrip`）。
- 默认 `LengthFieldSplitter` 构造参数：`maxFrameLength`、`lengthFieldOffset=0`、`lengthFieldLength=8`、`lengthAdjustment=P_Head - P_LEN`（即 **1**）、`initialBytesToStrip=8`、`isLittleEndian=true`。帧长公式必须按 DotNetty 写死：`total = lengthFieldOffset + lengthFieldLength + lengthFieldValue + lengthAdjustment`，代入得 `total = 8 + bodyLen + 1 = P_Head + bodyLen`，与 `FrameDecoder` 的 `total = P_Head + bodyLen` 一致。**不要写成 `lengthAdjustment=P_Head`**——那会得到 `bodyLen + 17`，多算 8 字节。
- 提供两个预设，避免"默认值"与"parity 口径"打架：
  - `LengthFieldSplitter.Default(maxFrameLength)`：`initialBytesToStrip=8`（只剥 8B 长度，**保留 1B type**），codec 看到 `[1B type][body]`。**这是 v1 生产默认**——type 必须在解析 Payload 之前就可用（鉴权与路由都依赖它，见 §5.3.1），不能让 codec 去反序列化 body 才知道类型。
  - `LengthFieldSplitter.LegacyCompatible(maxFrameLength)`：`initialBytesToStrip=0`，切出的帧是完整 `[8B len][1B type][body]`，**仅用于 parity 测试**。与既有 `BaseCoder.DecodeStream` 的比对不是"整帧字节相等"（`SocketFrame` 只含 body），而是逐字段：`consumed == P_Head + bodyLen`、`frame[0..8]` 小端解出 `BodyLength`、`frame[8] == Type`、`frame.Slice(P_Head)` 与 `Content` 逐字节相等。
- 非法长度（`bodyLen < 0` 或 `> MaxFrameLength`）：`consumed` 不推进、抛 `FrameProtocolException("非法的数据帧长度: {n}")`，由链尾处理并断开（与 `FrameDecoder.TryReadFrame` 的 `bodyLen < 0 || bodyLen > _maxFrameLength` 行为一致）。**核心包不能抛 `SAEA.Sockets.Model.KernelException`**——它定义在 `SAEA.Sockets`，核心包不引用该程序集；`FrameProtocolException` 是本包自有的协议级异常，适配层/链尾按普通异常处理。
- **`MaxFrameLength` 的口径**：**body 长度上限**，`0` 只允许空 body。既有 `FrameDecoder` 在 `maxFrameLength < P_Head` 时会把它夹逼成 `int.MaxValue - P_Head`（近乎不限制）；新分帧器**不复刻这个夹逼**——静默关掉上限是安全陷阱，改为按字面生效（`>= 0` 校验）。parity 用例只使用 `maxFrameLength >= P_Head` 的取值，两端在合法区间内逐帧一致。
- **常量来源**：核心包自带 `FrameConstants.P_LEN / P_Type / P_Head`（8 / 1 / 9），数值与 `BaseCoder` 一致；核心包不引用 `SAEA.Sockets`，不能直接使用 `BaseCoder.P_Head`。parity 测试**允许引用** `BaseCoder` 做对照，这正是它存在的意义。
- 心跳与大帧：既有 `SocketProtocalType.Heart`（bodyLen=0）与 `BigData` 的"不产出帧"语义**不硬编码进分帧器**；需要时由业务在分帧器之上加 handler 处理，默认 `LengthFieldSplitter` 对两者照常产出帧。

**出站镜像（同一层，方向相反）：**

```csharp
public interface IFrameEncoder
{
    /// <summary>把 body 写成完整帧（含包头/长度字段）。</summary>
    void Encode(ReadOnlyMemory<byte> body, int typeCode, IBufferWriter<byte> writer);
}
```

- `IFrameSplitter` / `IFrameEncoder` 是一层，必须成对配置；`LengthFieldFrameEncoder` 写 **`8B 小端 body 长度` + `1B type` + body**，**不是整帧总长**。依据：`BaseSocketProtocal.WriteTo` 用 `BinaryPrimitives.WriteInt64LittleEndian(span, BodyLength)`（`Src/SAEA.Sockets/Base/BaseSocketProtocal.cs` 的 `WriteTo`）；`FrameDecoder` 侧按 `total = P_Head + bodyLength` 还原，`LengthFieldSplitter` 用 `lengthAdjustment = P_Head - P_LEN = 1` 与该口径对齐（公式见本节上一条）。若误写成总长，对端会多算 9 字节并永久 desync。二者互为逆运算。
- **type code 只有 1 字节**：`IFrameEncoder.Encode` 收到 `typeCode` 必须校验 `0 <= typeCode <= 255`，越界抛 `ArgumentOutOfRangeException`；`ITypeRegistry.Register` 同样校验。否则 `(byte)typeCode` 会静默截断，帧头 type 与 `Envelope.Type` 对不上——这类错误在联调时表现为"消息偶尔路由到错误的 handler"，极难定位。
- **出站 type 的来源**：到达 `FrameEncoderHandler` 的消息在 v1 恒为 `EncodedBody`，type code 直接取 `EncodedBody.TypeCode`（该值由 `CodecEncoderHandler` 从 `Envelope.Type` 拷入）。v1 **不引入** `IMessageTypeResolver`——映射只在 `ITypeRegistry` 一处，避免第二个映射源。消息不是 `EncodedBody` 时 `FrameEncoderHandler` 直接抛 `InvalidOperationException`（说明链配错了）。
- `FrameEncoderHandler`（出站）负责：`EncodedBody` → `IFrameEncoder.Encode` 补包头 → 产出 `TransportFrame`（完整帧 + owner）继续向链头传递；**链头由 `ChannelPipeline` 内置的 head sink 统一调用 `IRawByteStream.SendAsync(data, owner)`**。handler **不允许**直接调 `SendAsync`——head sink 是唯一出口，这样所有出站字节经过同一个终点，owner 的交接点也只有一处。head sink **只接受 `TransportFrame`**：收到其它类型说明这条链缺少 `FrameEncoderHandler` 或出站顺序配错，直接抛 `InvalidOperationException`（不猜、不兜底编码）。head sink 的判定顺序固定为：**已 Attach 且已收口（`_closed` 或执行器非活动）→ `ChannelClosedException`；类型不是 `TransportFrame` → `InvalidOperationException`；未 Attach → `InvalidOperationException`**。收口判定必须先于类型判定——业务可能还持有已摘链节点的旧 `ctx`，此时写出任何消息都应得到"连接已关闭"而不是"链配错"。未 Attach 时没有执行器可判关闭，因此先报类型错（配置问题更可诊断），类型合法再报未接入。没有 `FrameEncoderHandler`，`ICodec.Encode` 写出的只是裸 body，这条链发不出任何合法字节。
- **出站不能"后补包头"**：`IBufferWriter` 没有前插能力，`CodecEncoderHandler` 先把 body 写进缓冲后，`FrameEncoderHandler` 无法再往前面塞 8B 长度 + 1B type。v1 在两级之间定死一个显式中间载体：

  ```csharp
  internal sealed class EncodedBody : IDisposable
  {
      public int TypeCode { get; }
      public ReadOnlyMemory<byte> Body { get; }
      public void Dispose();       // 幂等：归还 Body 的池化缓冲
  }

  internal sealed class TransportFrame : IDisposable
  {
      public ReadOnlyMemory<byte> Data { get; }   // 完整帧（8B 长度 + 1B type + body）
      public IDisposable Owner { get; }           // PooledBufferWriter，SendAsync 完成后由传输层释放
      public void Dispose();                      // 幂等：仅释放 Owner
  }
  ```

  流程：`CodecEncoderHandler` 用 `PooledBufferWriter` 写 body，产出 `EncodedBody`（`TypeCode` 取 `Envelope.Type`），放在 `try/finally` 里 `await ctx.WriteAsync(encodedBody)`，**`finally` 中 `Dispose()` 中间体**（下游 `FrameEncoderHandler` 会同步把 body 拷进最终缓冲，返回后即可归还；`ctx.WriteAsync` 抛异常时同样归还）。`FrameEncoderHandler` 新建最终 `PooledBufferWriter`，先写 **8B body 长度 + 1B type**、再 `Write(encodedBody.Body)`，把最终 writer 包成 `TransportFrame` 并 `await ctx.WriteAsync(transportFrame)`；**该调用抛异常时（含 `ChannelClosedException`）在 `catch` 里 `Dispose` 这个 `TransportFrame`**，成功返回表示所有权已交给链头/传输层，不再触碰。`EncodedBody` 与 `TransportFrame` 都只允许在出站链内部出现，不暴露给业务。这条路径多 1 次出站拷贝，是"两级正交"的已知成本，§8.4 的拷贝口径已按入站 2 次 / 出站 3 次分别列出。
- **v1 的 `CodecEncoderHandler` 只接受 `Envelope`**：收到其他类型直接抛 `InvalidOperationException`（链配错）。非 Envelope 拓扑需要 type code 解析器，v1 明确不做（见本节上文"出站 type 的来源"）。

### 5.3 编解码器（第二层）

```csharp
/// <summary>第一层：帧字节 ↔ 信封。分帧器产出的帧（默认 [1B type][payload]）交给它。</summary>
public interface ICodec
{
    object Decode(ReadOnlyMemory<byte> frame);
    void Encode(object message, IBufferWriter<byte> writer);
}

/// <summary>
/// 第二层：Envelope.Payload ↔ 业务实体。
/// 目标类型在运行期才确定（type code → ITypeRegistry → CLR Type），因此这里必须显式传 Type。
/// </summary>
public interface IMessageCodec
{
    object Decode(ReadOnlyMemory<byte> payload, Type targetType);
    /// <summary>返回独立数组，可直接赋给 Envelope.Payload；不得返回池化切片。</summary>
    byte[] Encode(object entity);
}
```

- **为什么拆成两个接口，而不是 `ICodec<T>` 加泛型桥接**：`netstandard2.0` + C# 8 没有 default interface methods，`ICodec<T> : ICodec` 的桥接方法必须在**每个实现**里重写一遍，收益为零；更关键的是 `EnvelopeUnwrapHandler` 在调用点只知道 `Type`（来自 `ITypeRegistry.Resolve`），泛型参数无法闭合。因此第一层用非泛型 `ICodec` 固定"帧 ↔ 信封"，第二层用"运行期 `Type` + 非泛型 `IMessageCodec`"，泛型便利只作为具体实现内部的私有细节。
- **`Decode` 的入参语义**：`ReadOnlyMemory<byte>` 是分帧器产出的帧，其内容由 `IFrameSplitter` 的 `initialBytesToStrip` 决定（剥不剥包头由配置说了算，codec 不自带偏移假设）。v1 默认 `initialBytesToStrip=8`，codec 看到 **`[1B type][payload...]`**；`LegacyCompatible` 时看到完整 `[8B len][1B type][body]`（只用于 parity 测试，生产不要这么配）。`Decode` **不得保留该内存**：返回后调用方立即 `Release` 背衬 `PooledMessage`。需要透传/留存原始字节的场景只能用 `FramePassthroughHandler`（见 §5.4）。
- `Encode` 只写 body，**不写包头**；包头由 `IFrameEncoder` 负责（见 §5.2）。
- 内置实现：`EnvelopeCodec`（`ICodec`，v1 生产默认：`[1B type][payload]` ↔ `Envelope`，**不做任何序列化**，只切出 type、拷贝 payload）、`JsonMessageCodec`（`IMessageCodec`，Newtonsoft，`Payload` ↔ 实体）、`ProtobufMessageCodec`（`IMessageCodec`，protobuf-net）、`RawCopyCodec`（`ICodec`，**拷贝**一份 `byte[]` 后返回，用于调试与基准；不做零拷贝透传，避免与 §5.4 的释放规则冲突）。
- `IMessageCodec.Decode(payload, targetType)` 的 `targetType` 由 `EnvelopeUnwrapHandler` 从 `ITypeRegistry` 解析后传入；实现内部 `JsonConvert.DeserializeObject(text, targetType)` 即按运行期类型还原。`Encode(object)` 直接返回 `byte[]`，与 `Envelope.Payload` 的类型一致，省掉一次 `IBufferWriter` 桥接；内部仍可用 `PooledBufferWriter` 生成字节后 `ToArray`，但 v1 用最直白的实现即可。
- 类型路由不归 codec：codec 只认"这条帧/这段 payload 是不是我能处理的"。**v1 不做多协议并存**，不按帧内类型字节选 codec；需要多协议时按连接隔离配置不同 pipeline（见 §5.3.1）。

### 5.3.1 Envelope 信封与实体路由（v1 统一模型）

v1 只有一种编解码拓扑，不做多协议并存：

```
帧 [1B type][payload...] ──EnvelopeCodec.Decode──> Envelope { int Type; byte[] Payload }
      ──AuthHandler──> 只看 Type 白名单
      ──EnvelopeUnwrapHandler──> 具体实体（按 Type 查 ITypeRegistry，再用 IMessageCodec 反序列化 Payload）
      ──TokenAuthHandler──> 校验业务令牌/签名（实体已还原）
      ──MessageDispatcher──> IMessageHandler（按实体 CLR 类型）
```

**`EnvelopeCodec`（v1 生产默认 codec）**：`Decode` 把帧首字节读成 `Type`、其余字节拷成 `Payload`；`Encode` 只写 `Payload`（type 由 `FrameEncoderHandler` 作为帧头写）。它不做 JSON/Protobuf，所以"信封层"与"序列化层"也是正交的——换序列化只换 `EnvelopeUnwrapHandler` / `EnvelopeWrapHandler` 持有的 `IMessageCodec`，不动信封。

```csharp
public sealed class Envelope
{
    public int Type { get; set; }
    /// <summary>
    /// 独立堆内存拷贝，绝不指向 PooledMessage 的池化背衬。
    /// 出站编码时由 EnvelopeWrapHandler 填充；入站解码时由 codec 拷贝。
    /// </summary>
    public byte[] Payload { get; set; }
}

public interface ITypeRegistry
{
    void Register(int typeCode, Type clrType);          // 双向注册：code ↔ CLR 类型；重复即抛，不做链式返回
    Type Resolve(int typeCode);                          // 未注册返回 null，由 unwrap handler 抛错
    int ResolveCode(Type clrType);                       // 未注册返回 -1，由 EnvelopeWrapHandler 抛错
}
```

- `TypeRegistry.Register` 的约束：`typeCode` 必须 `0..255`（线上 1B）；同一 code 或同一 CLR 类型重复注册抛 `InvalidOperationException`（不做覆盖，避免两份映射悄悄漂移）。`Resolve` 未注册返回 `null`，`ResolveCode` 未注册返回 `-1`，由 `EnvelopeUnwrapHandler` / `EnvelopeWrapHandler` 抛 `InvalidOperationException` 走 `ExceptionCaught`（§5.3.1 末条）。接口返回 `void`，不提供链式 `Register`：具体类若再定义一个返回自身的同名方法会与接口签名冲突（C# 8 不允许仅返回类型不同的重载），示例统一写成多条语句。实现用两个 `ConcurrentDictionary` + 原子 `TryAdd`（重复注册天然失败），注册只在启动期发生、读路径无锁。
- **`Payload` 必须拷贝，不得复用池化帧体。** Envelope 会跨 `await` 活到业务 handler，而 `PooledMessage` 在 `CodecDecoderHandler` 返回后就被 `Release` 归还；把 `ReadOnlyMemory<byte>` 直接指过去，等于拿一块已归还的池内存当业务数据。这条与 §5.3 的"`Decode` 不得保留入参内存"是同一条约束的两面。
- 拷贝成本控制：`EnvelopeCopyThreshold`（默认 `MemoryPoolManager.SmallThreshold`，4KB）**只是告警阈值，不改变拷贝方式**——所有 `Payload` 都是精确 `new byte[len]`（§十 第 6 条）。**超过阈值时不能在同一条链上临时改走 `FramePassthroughHandler`**——分支拓扑是装配期决定的，单条链里两套所有权不能并存（§5.4 第 4 条）。v1 的规则：
  - **装配期选择**：预期该连接会出现大帧，就直接装配 `FramePassthroughHandler` 分支（不装 `CodecDecoderHandler` / `EnvelopeUnwrapHandler`），由业务自行反序列化；
  - **Envelope 分支的运行时行为**：超过 `EnvelopeCopyThreshold` 的帧仍按 Envelope 路径正确拷贝（**保证不丢数据**），由 `EnvelopeUnwrapHandler` 记一条 `Warn`（带 `ChannelId`、`Payload.Length`、阈值），提示改用 passthrough 装配；
  - 阈值不是安全边界，真正的安全边界是 `MaxFrameLength` / `MaxBufferedBytes`。
- 出站对称：`EnvelopeWrapHandler`（实体 → `Envelope`，type 由 `ITypeRegistry.ResolveCode(entity.GetType())` 得到）→ `CodecEncoderHandler(new EnvelopeCodec())`（只写 Payload，并把 `Envelope.Type` 拷进 `EncodedBody.TypeCode`）→ `FrameEncoderHandler`（用 `EncodedBody.TypeCode` 写 **8B body 长度** + 1B type + body）。
- **路由只看实体 CLR 类型**：帧头 type 只用于"选出实体类型"这一步；`ITypeRegistry` 是 type code ↔ CLR 类型的唯一映射源，帧头与 `Envelope.Type` 由 `EnvelopeCodec` 保证一致，不存在第二份映射表。
- 未注册的 type → `InvalidOperationException`，走 `ExceptionCaught`，不静默丢弃。

**鉴权位置（H1：鉴权必须在反序列化之前，两级）：**

```csharp
// 入站：... → CodecDecoderHandler → AuthHandler（只看 Envelope.Type） → EnvelopeUnwrapHandler → ...
public sealed class AuthHandler : InboundHandlerAdapter
{
    public override bool IsSharable => true;
    public AuthHandler(IReadOnlyCollection<int> allowedBeforeAuth) { /* ... */ }

    public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
    {
        var env = (Envelope)msg;
        var authed = Equals(ctx.Channel.GetAttribute("authed"), true);

        if (!authed && !AllowedBeforeAuth(env.Type))   // 未认证只放行白名单 type
        {
            // 消费掉，不往下传；不解析 Payload，不抛异常（避免把攻击流量变成日志风暴）
            await ctx.Channel.CloseAsync("unauthenticated");
            return;
        }

        await ctx.FireChannelReadAsync(msg);            // 放行
    }
}
```

- **第一级（连接级，`AuthHandler`，在 unwrap 之前）**：只看 `Envelope.Type` 白名单 + `IChannel.Attributes`，**绝不解析 `Payload`**。未认证连接只允许 `Login` / `Heartbeat` 这类 type，其余直接关闭。这样未认证流量打不到 JSON/Protobuf 解析器，堵住解析型 DoS 面。
- **第二级（业务令牌级，`TokenAuthHandler`，在 unwrap 之后）**：只有第一级放行、实体已还原成 CLR 对象后，才校验业务令牌/签名。两级职责不同，不要合成一个 handler——合成后第一级必然要解析 Payload。
- 因此 v1 的入站标准顺序是：`CodecDecoder（帧→Envelope） → AuthHandler → EnvelopeUnwrap（Payload→实体） → TokenAuthHandler → Logging → Dispatcher`（分帧由泵负责，不在链上）。**日志在鉴权之后**，未认证帧不产生业务日志噪声。
- 帧长上限由 `MaxFrameLength` 兜底，鉴权之前不存在"超大 Payload"的额外风险。

### 5.4 消息所有权

```csharp
public sealed class PooledMessage : IDisposable
{
    /// <summary>队列条目持有的帧字节（默认分帧配置下为 [1B type][payload]）。</summary>
    public ReadOnlyMemory<byte> Frame { get; }
    public bool IsPooled { get; }   // false = 精确 new byte[len]，归还时直接交给 GC
    public void Retain();           // 引用计数 +1
    public void Release();          // 引用计数 -1，归零时按 IsPooled 决定归还池还是丢弃引用
    public void Dispose();          // 等价 Release
}
```

- 注意命名区分：`PooledMessage.Frame` 是**队列条目**拥有的帧字节（默认含 1B type），`Envelope.Payload` 是**业务信封**拥有的独立 `byte[]`（只有 payload）。两者不共享内存。

规则（必须写进 README 与 XML 注释）：

1. 泵把帧体拷入 `PooledMessage`，引用计数初始为 1，**所有权归链**。背衬按长度二选一：`len >= SmallFrameInlineThreshold`（默认 512B）→ 从 `MemoryPoolManager` 借；`len < 阈值` → 精确 `new byte[len]`（`IsPooled == false`）。

   **为什么必须分叉**：`MemoryPoolManager.Rent` 对小于 `SmallThreshold`（4KB）的请求按 4KB 发放（`MemoryPoolManager.cs:150-163`），一个 20B 的帧也占 4KB。若 `QueueCapacity=1024` + 在途 16，单连接最坏就是 1024×4KB ≈ 4MB，千连接到 GB 级。小帧走精确分配后，同样的 20B 帧只占 20B。**已知取舍**：`512B–4KB` 的帧走池仍会被放大到 4KB，这是"少一次精确分配"换来的；生产按实际帧长分布调 `SmallFrameInlineThreshold`，不要指望它在所有分布下都最优。默认 `QueueCapacity` 1024 是"够用"而非"省内存"，高连接数时按连接数下调。
2. 入站 handler 把消息传给下游后**不得再使用**该消息；要跨 `await` 留存必须先 `Retain()`，用完 `Release()`。
3. `CodecDecoderHandler` 在 `ICodec.Decode` **成功返回后立即** `Release()` 掉队列条目的原始 `PooledMessage`；因此 codec 契约是"不得保留入参内存"。`Decode` 抛异常时不在这里释放（还没确认解码成功），交给执行包的 `finally`（第 6 条）。executor 在执行包的 `finally` 里再 `Release()` 一次作为保底——`Release()` 对已归零实例是 no-op（见下表），所以重复调用安全，不需要"是否已释放"的判断。
4. 需要把原始帧字节交给下游（抓包、审计、二进制协议直通）时，不用 codec，改用 `FramePassthroughHandler`：它 `Retain()` 后把 `PooledMessage` 原样下传，由该分支自己的尾部 handler 负责 `Release()` 那份 Retain；executor 的 `finally` 释放的是**队列条目那一份**，两者不是同一个引用，互不替代。这条分支与 codec 分支互斥，避免两套所有权并存。
5. **出站缓冲的交接契约（所有权在 `await ctx.WriteAsync` 成功返回时转移）**：
   - 中间 `EncodedBody` 由 `CodecEncoderHandler` 在 `finally` 里 `Dispose()`——无论下游成功返回还是一路抛异常，body 都已被 `FrameEncoderHandler` 同步拷进最终缓冲（或根本没走到它），此时归还是安全的。
   - 最终 `PooledBufferWriter` 由 `FrameEncoderHandler` 包成 `TransportFrame`；**`await ctx.WriteAsync(transportFrame)` 正常返回 = 所有权已交给链头/传输层**，由发送完成回调（`owner`）释放。若该调用抛异常（含 `ChannelClosedException`），所有权并未交出去，`FrameEncoderHandler` 必须在 `catch` 里 `Dispose` 该 `TransportFrame` 后原样抛出。
   - head sink 只执行 `stream.SendAsync(frame.Data, frame.Owner)`，**不在 `catch` 里 `Dispose`**——避免与上一条的创建者双还。`TransportFrame.Dispose` / `EncodedBody.Dispose` / `PooledBufferWriter.Dispose` 都必须幂等，作为契约防线。

**异常路径（与正常路径同等约束，不是"兜底")：**

6. **executor 是最终责任人**：队列条目自带一份 `PooledMessage` 引用，每条消息的执行包在 `try/finally` 里无条件 `Release()` 一次（幂等）。因此**不管异常抛出在哪一环**（codec、鉴权、路由、出站编码），引用计数都归零。注意：分帧阶段的异常发生在入队之前，由泵自己 `Release()`（§6.2 的 `DrainToQueue`），不经过 executor。
7. **异常发生在 `CodecDecoderHandler` 之前/之中**（`Decode` 抛、unwrap 抛）：原始 `PooledMessage` 尚未释放，由第 6 条的 `finally` 收口；`Decode` 内部若已拷贝出 Envelope，拷贝出的 `byte[]` 交给 GC，不进引用计数。
8. **异常发生在出站编码**（`ICodec.Encode` 或 `IFrameEncoder.Encode` 抛）：`CodecEncoderHandler` / `FrameEncoderHandler` 各自的 `finally` / `catch` 归还中间 `EncodedBody` 与最终 `PooledBufferWriter`（规则 5），**绝不** `SendAsync`；已产生的半截字节丢弃即可（尚未交给传输层）。
9. **队列写入失败**：分两种情况，不能混。① **队列满**（`TryWrite` 返回 false 但 writer 未完成）：该帧未入队，`Release()` 掉这份拷贝，但**帧仍留在 `FrameAccumulator`**（不 `Consume`），泵 `Pause()` 并等低水位信号——这是背压，**不断开**（§6.2）。② **writer 已完成 / 已取消**（断开收口中）：`Release()` 后按 §6.3 的断开策略丢弃，不再重试。
10. **泵内异常**（`Append` 越界、splitter 抛 `FrameProtocolException`）：先由泵的 `finally` 归还 `FrameAccumulator` 已借缓冲，再按 §6.3 完整收口（队列按策略丢弃、引用计数收口），最后关闭 `IRawByteStream`。

**`PooledMessage` 的边界行为（必须实现，测试覆盖）：**

| 操作 | 行为 |
|---|---|
| `Retain()` | 引用计数 +1；对已归零实例抛 `ObjectDisposedException` |
| `Release()` | 引用计数 −1，归零时归还背衬数组并置 `IsReleased = true`；对已归零实例为 **no-op**（幂等），由计数配平测试捕获误用 |
| `Frame` | `IsReleased` 后访问抛 `ObjectDisposedException`，不允许读到已归还内存 |
| 归还 | 池化背衬（`IsPooled == true`）必须回到 `MemoryPoolManager`，且必须是当初 `Rent` 的那块整数组；调用 `Return(buffer, originalSize)` 把帧的真实长度一并传回（`MemoryPoolManager.cs:195` 支持 `originalSize` 参数），**不得**把切片当整块归还。非池化背衬（`IsPooled == false`）不归还，置空引用交给 GC |

### 5.5 路由

```csharp
public interface IMessageHandler
{
    Task HandleAsync(IChannelHandlerContext ctx, object message);
}

public sealed class MessageDispatcher : InboundHandlerAdapter
{
    public MessageDispatcher Register<TMessage>(IMessageHandler handler);
    public MessageDispatcher RegisterFallback(IMessageHandler handler);   // 可选：未命中兜底
    // ChannelReadAsync 按 message.GetType() 精确查表；未命中策略见下
}
```

- 精确按 CLR 类型匹配，**不做继承/接口匹配**：注册 `BaseRequest` 不会兜住 `LoginRequest`。
- **未命中（M3：不允许静默丢弃）**：配置了 `Fallback` → 交给它；否则记一条 `Warn`（含消息类型名与 handler 位置），再 `ctx.FireChannelReadAsync` 往下传。标准装配的下游是 `ExceptionTerminalHandler`，由它丢弃实现 `IDisposable` 的消息；若自定义装配里 `MessageDispatcher` 就是最后一个入站 handler（没有链尾），则 `Warn` 升级为 `Error` 并在这里显式丢弃（消息若实现 `IDisposable` 则 `Dispose`）。注意：这里拿到的是 unwrap 之后的业务实体，原始 `PooledMessage` 已在 `CodecDecoderHandler` 返回时归还，**不存在**对 `PooledMessage` 的二次 `Release`。
- **未知消息类型不断连接**：协议演进是常态，断连过于激进；但必须留下可检索的日志。
- 出站方向相反：`EnvelopeWrapHandler` 遇到未注册的 CLR 类型直接抛 `InvalidOperationException`（出站是我们自己的代码，静默丢回包更糟）。

业务写法（参照 DotNetty 的 handler 组合，而不是注解扫描）：

```csharp
var registry = new TypeRegistry();
registry.Register(1, typeof(LoginRequest));
registry.Register(2, typeof(Heartbeat));

var options = new ChannelPipelineOptions
{
    Splitter = LengthFieldSplitter.Default(maxFrameLength),   // 分帧器交给泵，不进链
};

var pipeline = ChannelPipeline.Create(options)
    // 出站 handler 必须先注册，构成链首的连续前缀。
    // 出站事件从 tail 向 head 传播：业务 handler 调 ctx.WriteAsync 后，
    // 依次经过 EnvelopeWrapHandler → CodecEncoderHandler → FrameEncoderHandler → 链头（传输层）。
    .AddLast(new FrameEncoderHandler(new LengthFieldFrameEncoder()))
    .AddLast(new OutboundLoggingHandler())                      // 记录 EncodedBody 的 type/body 长度与耗时
    .AddLast(new CodecEncoderHandler(new EnvelopeCodec()))     // Envelope → payload，type 留给帧头
    .AddLast(new EnvelopeWrapHandler(registry, new JsonMessageCodec()))  // 实体 → Envelope
    // 入站（head → tail）；分帧由泵负责，链上没有 FrameSplitterHandler
    .AddLast(new CodecDecoderHandler(new EnvelopeCodec()))     // [1B type][payload] → Envelope
    .AddLast(new AuthHandler(new[] { 1, 2 }))                   // 只看 Envelope.Type + Attributes
    .AddLast(new EnvelopeUnwrapHandler(registry, new JsonMessageCodec()))  // Payload → 实体
    .AddLast(new TokenAuthHandler())                            // 第二级：实体已还原后校验令牌
    .AddLast(new InboundLoggingHandler())                       // 鉴权之后，未认证帧不进业务日志
    .AddLast(new MessageDispatcher()
        .Register<LoginRequest>(new LoginHandler())
        .Register<Heartbeat>(new HeartbeatHandler()))
    .AddLast(new ExceptionTerminalHandler());                   // 入站方向最后一个
```

- **示例的链式写法只用于展示顺序**：`.AddLast(...)` 返回 `IChannelPipeline`，而 `Attach` 在具体类上。适配层的实际写法是 `var pipeline = ChannelPipeline.Create(options); pipeline.AddLast(...); ...; executor.Attach(pipeline, stream);`——不要把 `Create` 的返回值在同一条表达式里链掉，否则拿不到具体类型。
- **分帧器不是链上 handler**：标准装配里 `IFrameSplitter` 是 `ChannelPipelineOptions.Splitter`，由泵在入队前调用（§6.2），队列里放的是**已经切好的帧**。链的入站第一站是 `CodecDecoderHandler`。`FrameSplitterHandler` 仅用于"不用 executor 的纯链式"场景（单测、自定义传输），标准装配**不要**装它，否则同一批字节会被拆两次。
- **出站 handler 的注册位置是硬约束，不是风格问题**：`ctx.WriteAsync` 只向 **head** 方向找下一出站 handler。任何注册在"会写出站消息的入站 handler"**之后**（更靠 tail）的出站 handler，永远收不到该消息。把 `EnvelopeWrapHandler` / `CodecEncoderHandler` / `FrameEncoderHandler` 写在 `MessageDispatcher` 之后是典型错法，表现为"回包完全发不出去且不抛异常"。因此 `AddLast` / `AddFirst` **每次插入都做**一条**可判定**的结构校验（`Create(options)` 不跳过）：**所有 `IChannelOutboundHandler` 必须构成链首的连续前缀**（中间夹了任何入站 handler 就抛 `InvalidOperationException`，消息给出本节示例顺序）。若链首已存在出站前缀，`AddFirst` 入站 handler 会立刻破坏前缀并抛错，这是有意的——出站事件从 tail 往 head 走，插在最前面的入站 handler 拿到的是"即将发给传输层"的消息，语义与入站相反；链首还没有出站 handler 时，`AddFirst` 入站 handler 合法。这条规则覆盖了"出站 handler 落在业务 handler 之后"的全部情形，且不依赖"猜哪个入站 handler 会写"。`ExceptionTerminalHandler` 是入站 handler，天然在它之后，不受影响。
- `ExceptionTerminalHandler` 是**入站方向**的最后一个（`FireExceptionCaughtAsync` 从 head 向 tail 走），不要为了出站把它挪到最前；它不参与出站编码。它 override `ChannelExceptionCaughtAsync` 与 `ChannelReadAsync`（不 override `ChannelReadCompleteAsync`）。异常按类型分流：
  - `MessageTimeoutException`（单消息超时）：`Warn` 记录后返回，**不关闭连接**——超时只影响该消息（§6.3）。
  - `ChannelClosedException`（收口中仍尝试回包）：`Debug` 记录后返回，**不关闭连接**（连接已在收口，重复 `CloseAsync` 无意义）。
  - 其它异常：`Error` 记录（`ChannelId` + 异常 + 当前消息类型）→ `ctx.Channel.CloseAsync("handler-exception")` → 不再向下传。`CloseAsync` 自身抛出的 `ChannelClosedException` 同样吞掉，不再二次上报。
  三个异常分支都**不**继续向下传（它本来就是链尾）。`ChannelReadAsync` 的语义是"消息一路透传到链尾、没人消费"：记 `Debug`（含 `ChannelId` 与消息类型；未命中路由的 `Warn` 已由 `MessageDispatcher` 发出，这里不重复记），消息实现 `IDisposable` 时 `Dispose()`，**不关闭连接**（未命中路由已由 `MessageDispatcher` 处理过，这里只是兜底回收）。
- 出站事件的起点是"业务 handler 所在节点靠 head 一侧的邻居"，不是链头。业务在 `IMessageHandler` 内调 `ctx.WriteAsync` 时，`ctx` 必须是该消息所在链节点的 context（由 `MessageDispatcher` 传入），不要另建一个指向链头的 context。
- 所有自定义 handler 都继承 `InboundHandlerAdapter` / `OutboundHandlerAdapter`（§5.1），`AuthHandler` 只需 override `ChannelReadAsync` 与 `IsSharable`。
- **鉴权在反序列化之前**（§5.3.1 的两级鉴权）：第一级看 `Envelope.Type` 白名单，第二级 `TokenAuthHandler` 才在 unwrap 之后校验业务令牌。

---

## 六、运行时：泵、队列、并发、背压

### 6.1 传输无关接入面

核心包不认 socket，只认这条接口：

```csharp
public interface IRawByteStream
{
    string ChannelId { get; }
    PipeReader Input { get; }
    /// <summary>
    /// 把完整帧交给传输层。<paramref name="owner"/> 在底层发送真正完成（或失败）后由传输层释放；
    /// 本 Task 表示"所有权已交接"，不表示对端已收到。
    /// </summary>
    Task SendAsync(ReadOnlyMemory<byte> data, IDisposable owner);
    void Pause();                                // 暂停读取（见 6.2，独立于传输层自身背压）
    void Resume();
    Task CloseAsync(string reason);
}
```

- **`Pause` / `Resume` 是同步的**（H3）：Pipe 侧只是置位一个闸门，不需要真正的异步；同步签名让 `SAEA.Sockets` 的适配层不必凭空造 `Task`，也让"暂停"在泵循环里没有 await 点，避免暂停与恢复交错。
- `SendAsync` 在连接已关闭时**立即**抛 `ChannelClosedException`（§6.3），不排队、不静默成功。`CloseAsync` 必须**幂等**：连接已被外部 `Disconnect` / `SaeaConnection.Dispose` 关掉时直接返回 `Task.CompletedTask`，`KernelException` / `ObjectDisposedException` 一律吞掉——泵自己就是被这个 Dispose 唤醒的，收口再抛出已关闭异常只会污染清理路径。

- `owner` 参数是强制的：`SaeaServerSocket.SendAsync(string, ReadOnlyMemory<byte>, IDisposable owner)`（`Src/SAEA.Sockets/IServerSocket.cs:121`，返回 `void`）已有"发送完成回调里释放 owner"的语义。适配层接过去时：① 调完即返回 `Task.CompletedTask`（Task 只表示所有权已交接）；② 把"会话不存在"的 `KernelException` 与已关闭状态统一翻译成 `ChannelClosedException`，不要让业务看到两种异常形态。**不允许**适配器把 Task 实现成"交给 socket 就算完成"却不传 owner——那样编码缓冲会被提前归还，发出脏数据。

`SAEA.Sockets` 侧新增一个中立钩子（约 40–60 行，不含 §6.2 的双来源闸门改动）：

```csharp
// SocketOptionBuilder
public SocketOptionBuilder UseConnectionOpened(Action<ChannelInfo> callback);

public SocketOptionBuilder UseInputHandler(
    Func<IUserToken, PipeReader, IReceiveFlowControl, CancellationToken, Task> handler);

public interface IReceiveFlowControl
{
    bool IsPaused { get; }
    void Pause();
    void Resume();
    Task CloseAsync(string reason);
}
```

- **`UseConnectionOpened` 是 `IChannel` 的创建点**：在 `RaiseAccepted` 内部、`_channelManager.Set` 与 `UserToken` 赋值之后、通知 `OnAccepted` 订阅者之前**同步**调用，参数是同一个 `ChannelInfo` 实例（含 `UserToken` / `Stream` / `Tls`），不能另造一份（时序细节见 §5.1）。只允许做轻量登记（创建 `IChannel`、写初始 `Attributes`），**禁止阻塞 IO / await**；回调抛异常按该连接建立失败处理（关闭连接 + `OnError`），不影响其它连接。与 `UseInputHandler` 一样只支持 `.UseStream()`，在纯 `.UseIocp()` 下配置即 `Build()` 抛 `InvalidOperationException`。
- **`PipeReader` 是独占的**：配置了 `UseInputHandler` 后，该连接的 `PipeReader` 由泵独占读取；**禁止**再对同一连接调用 `GetStream()` 或 `ChannelInfo.Stream.ReadAsync`（`SaeaConnection.ReadAsync` 走的是同一个 `_reader`，两个读者会互相抢数据、触发 Pipe 非法并发读）。出站 `Stream.WriteAsync` 不受影响。README 必须写明。
- **`CancellationToken` 是必带的**（H3）：连接断开、服务停止时由 `SAEA.Sockets` 取消，泵循环在 `ReadAsync` 与每轮迭代开头检查它；取消后按 §6.3 的断开收口执行，不把取消当异常上报。没有这个 token，适配层只能靠"读到 EOF"发现断开，服务端停止时会挂住。

- 配置了 `UseInputHandler` 的连接：由该委托接管读循环，`StartSpanPump` 不再启动，`OnServerReceiveSpan` 对该连接不再触发。
- **未配置 `.UseStream()` 却在服务端配置了 `UseInputHandler` → `Build()` 抛 `InvalidOperationException`**，消息写明"通道链需要 `UseStream` 提供的 `PipeReader`，纯 IOCP 事件模式没有"。不自动打开 `UseStream`：那会把 `OnAccepted` 参数改成 `ChannelInfo`、启用 `ChannelManager`、把 token 换成 `StreamUserToken`，属于调用方无感的破坏性变化。
- 客户端（`SaeaClientSocket`）配置 `UseInputHandler` / `UseConnectionOpened` 时在**客户端构造**阶段抛 `NotSupportedException`，v1 不支持（见 §2.2）；不允许静默忽略。
- 未配置的连接：行为语义与今天完全一致，但代码路径有改动（§6.2 的双来源闸门是公共路径），需全量回归。
- 钩子类型定义在 `SAEA.Sockets`，因此 `SAEA.Sockets.Pipeline` 不引用 `SAEA.Sockets`；把 `IRawByteStream` 适配到 `IUserToken`/`IReceiveFlowControl` 的十来行胶水放在**使用方**，演示工程提供一份可直接复制的完整实现，README 给出全文。核心包不提供该适配器——反向依赖 `SAEA.Sockets` 会让"可脱离 socket 复用"这条目标失效。

### 6.2 接收泵（每连接一个）

```
var pendingComplete = false

loop:
  if (ct.IsCancellationRequested) break

  // 1) 先排空 accumulator：上一轮可能因队列满留下了"已拆出但未 Consume"的帧
  if (!DrainToQueue(out var queued))
  {
      // 队列满：该帧未 Consume、仍留在 accumulator；等消费侧低水位信号。
      // 关键：暂停期间绝不能调用 reader.ReadAsync，否则永远等不到新数据。
      pendingComplete |= queued
      if (!await WaitQueueSpaceAsync(ct)) break
      continue
  }
  pendingComplete |= queued

  // 2) 半包耗尽防护：完整帧已全部入队，剩下的是凑不成一帧的尾巴
  if (accumulator.BufferedLength > maxBufferedBytes)   // Create 时归一化后的有效值（0 已推导）
  {
      await ShutdownAsync("buffered-bytes-limit")   // 走完整的断开收口（§6.3），不是只关 socket
      break
  }

  // 3) 本轮（或合并的上一轮）的帧已全部入队，通知一次
  if (pendingComplete)
  {
      await SafeFireReadCompleteAsync()      // 内部记录并吞掉通知异常，不打断泵
      pendingComplete = false
  }

  // 4) 再读新字节
  ReadResult result
  try { result = await reader.ReadAsync(ct) }
  catch (OperationCanceledException) { break }           // 收口已由取消方启动；泵只做 finally 清理
  catch (Exception ex) { RecordPumpError(ex); await ShutdownAsync("read-error"); break }

  // 4.1) 传输层已 Dispose 连接（外部 Disconnect / 服务停止）：CancelPendingRead 让本轮返回 IsCanceled，
  //      必须在这里收口；当成"空读"继续循环会立刻再次取消，形成忙循环。
  if (result.IsCanceled)
  {
      SafeAdvanceTo(result.Buffer.End)       // reader 可能已被外部 Complete，失败只记 Debug
      await ShutdownAsync("transport-closed")
      break
  }

  bool complete
  bool queuedThisRead
  try
  {
      accumulator.Append(result.Buffer)      // 拷贝①：Pipe → accumulator
      complete = DrainToQueue(out queuedThisRead)   // 拷贝②发生在 DrainToQueue 内
  }
  catch (Exception ex)                       // Append 越界 / TrySplit 抛 FrameProtocolException
  {
      RecordPumpError(ex)
      await ShutdownAsync("split-error")     // 完整收口；finally 仍会放行 Pipe 缓冲
      break
  }
  finally
  {
      SafeAdvanceTo(result.Buffer.End);      // 整块放行，不按 consumed；外部可能已 Complete，失败只记 Debug
  }

  pendingComplete |= queuedThisRead
  if (complete && pendingComplete)           // 本轮完整排空才通知；被截断则与下一轮合并
  {
      await SafeFireReadCompleteAsync()
      pendingComplete = false
  }

  // 5) 对端关闭（EOF）：先把本轮缓冲处理完，再走收口；不 break 会形成忙循环
  if (result.IsCompleted) { await ShutdownAsync("remote-eof"); break }

  // 6) 只有本轮完整排空才做流量检查；截断的轮次已被闸门挡住，重复检查只会产生噪声
  if (complete && !guard.Allow(Traffic())) { await ShutdownAsync("guard-rejected"); break }
```

`DrainToQueue` 的语义（不是伪代码里的内联循环，必须单独实现）：

```
bool DrainToQueue(out bool anyQueued)          // 返回 false = 队列满，剩余帧留在 accumulator
{
    anyQueued = false
    while (splitter.TrySplit(accumulator.Buffered, out var consumed, out var frame))
    {
        var msg = AllocateFrame(frame)         // 拷贝②：accumulator → PooledMessage
        if (!queue.TryWrite(msg))
        {
            msg.Release()                      // 这一帧没有入队，归还拷贝
            SetQueuePausedOnce()               // Interlocked 0→1；调 stream.Pause()，并立即复核一次 _queued（空窗竞态，见下）
            return false                       // 不 Consume，帧留在 accumulator
        }
        accumulator.Consume(consumed)          // 只有入队成功才推进
        anyQueued = true
    }
    return true
}
```

- **`ReadAsync` 的结束态必须分开处理**：`result.IsCompleted == true`（对端半关闭/EOF）时，**先把本轮缓冲追加并排空**，再 `ShutdownAsync("remote-eof")` 退出；不处理会立刻再次返回完成态，形成忙循环把 CPU 打满。若 EOF 那一轮因队列满被截断，允许丢弃 accumulator 里尚未入队的剩余帧后直接收口——连接已断，等队列腾空也换不来新数据（收口第 3 步会连同队列内未开始的条目一起按策略丢弃）。**`result.IsCanceled` 必须单独处理且不能忽略**：既有 `SaeaConnection.Dispose()` 会先 `CancelPendingRead()` 再 `Complete()`，外部 `Disconnect` / 服务停止走的正是这条路径，所以它在本栈里**必然会发生**（不是"v1 不用它"）。收到 `IsCanceled` 时丢弃本轮 `buffer`、`ShutdownAsync("transport-closed")` 后退出；把它当空读继续循环会立刻再次取消，形成忙循环。`ReadResult.Buffer.IsEmpty` 且未完成时，正常发起下一轮读，不要自旋。
- 泵**只做这一件事**，绝不执行鉴权/序列化/业务。伪代码里的 `ShutdownAsync(reason)` 指**完整的断开收口入口**（§6.3 第 2–6 步），不是直接调 `IRawByteStream.CloseAsync`——只关 socket 会漏掉队列丢弃、引用计数配平与 accumulator 归还。
- **分帧器由泵持有**：取 `ChannelPipelineOptions.Splitter`。内置分帧器必须**无状态**（拆帧进度全在 `FrameAccumulator` 的 `_start/_end` 里），因此可以跨连接共享一个实例；`Reset()` 只是给将来有状态实现预留，内置实现为空操作。若将来引入有状态分帧器，必须改成每连接一个实例（工厂），不能复用本节的共享约定。
- **`AdvanceTo` 必须传 `buffer.End`（B2 修正）**：字节在 `Append` 时已经全量拷进 `FrameAccumulator`，Pipe 侧不存在"部分消费"的可能；传 `consumedBytes` 会与 accumulator 的内部游标打架，并且没有任何收益。
- **异常路径也必须放行 Pipe 缓冲**：`Append` / `TrySplit` / `AllocateFrame` 任一步抛异常时，本轮 `ReadAsync` 返回的 `buffer` 不能停在"未 Advance"状态。把本轮处理放进 `try/finally`，`finally` 里若尚未 `AdvanceTo` 则执行一次 `AdvanceTo(buffer.End)`，随后走关闭路径 `reader.Complete()`。不要用 `AdvanceTo(buffer.Start, buffer.End)` 把同一批数据留到下一轮——数据已拷进 accumulator，留着只会重复处理。**`AdvanceTo` 要能容忍外部已 `Complete`**：`SaeaConnection.Dispose()` 可能在本轮 `ReadAsync` 返回前后完成 reader，此时 `AdvanceTo` 会抛 `InvalidOperationException`；包一层 `SafeAdvanceTo`，捕获后只记 `Debug`，不能让 `finally` 里的异常逸出泵协程。
- **`reader.Complete()` 由泵调用且必须幂等**：钩子启用期间泵是该 `PipeReader` 的唯一 reader，退出路径（正常/异常/取消）的 `finally` 必须调一次 `reader.Complete()`；但外部 `Dispose` 可能已经先 `Complete` 过，这里要捕获 `InvalidOperationException` / `ObjectDisposedException` 并忽略，绝不让清理路径再抛。它只表示"读者收工"，不关闭 socket；socket 关闭仍由 §6.3 第 6 步负责。`Complete()` 之后不得再 `ReadAsync`。
- **守卫检查的放置**：`guard.Allow(Traffic())` 只在 `DrainToQueue` **完整排空**（`complete == true`）的轮次执行一次；因队列满走 `continue` 的轮次、以及读后 `DrainToQueue` 被截断的轮次都跳过——那些轮次没有把已读数据全部消化完，泵也已经被闸门挡住，反复检查只会产生噪声。守卫为 `NullChannelGuard` 时该调用是一次静态空实现，成本可忽略。
- **每字节固定 2 次拷贝，文档明写、不留歧义**：① Pipe → accumulator（`Append`），② accumulator → `PooledMessage`（`AllocateFrame`）。`AdvanceTo(buffer.End)` 之后 Pipe 内存失效，所以①必须在本次循环内完成；队列条目要跨 `await` 存活，所以②必须独立背衬。
  - 备选的"省掉第二次拷贝"（让 accumulator 直接把帧切走、`PooledMessage` 借用 accumulator 的背衬）需要重写 accumulator 的消费/归位语义，属里程碑 1 的范围扩张，本次**不做**；将来若做，必须回填本节与 §8.4 的口径。
- **队列满（`TryWrite` 失败）**：`stream.Pause()` 后停止本轮后续投递；**未入队的那一帧不 `Consume`、留在 `FrameAccumulator`**，所以不丢。泵随后等待消费侧的低水位信号（`WaitQueueSpaceAsync`，底层 `SemaphoreSlim(0, 1)`，带 ct），**暂停期间不调用 `ReadAsync`**——否则既读不到新数据，恢复信号也没人等。已在队列里的消息继续被消费。
- **`ChannelReadCompleteAsync` 的轮次口径**：只有 `DrainToQueue` **完整排空**（返回 true）且自上次通知以来确实入过队才触发一次；被队列满截断的轮次**不触发**，其已入队的帧记在 `pendingComplete` 上，与下一次成功排空合并。因此不变量是"**每次成功排空至多 1 次、每次 `ReadAsync` 至多 1 次，且不产生空通知**"。`SafeFireReadCompleteAsync` 内部对通知异常记录后吞掉（§5.1），不让它变成连接错误。
- **`FrameAccumulator` 的缓冲走 `MemoryPoolManager`**：容量**按需扩容**，`Append` 必须保证"现有残余 + 本次读入"装得下（本次读入长度 = 本轮 `ReadAsync` 返回的字节数，由传输层 Pipe 决定、本身有界）。**不要**把 `MaxBufferedBytes + ReadBufferSize` 当硬上限：`SaeaConnection` 的 `PauseWriterThreshold = max(4 × ReadBufferSize, 64KB)`（默认 256KB），一次 `ReadAsync` 可能返回远超 `ReadBufferSize` 的数据，固定公式会在合法突发下误杀连接。`MaxBufferedBytes` 的检查仍然只对每轮 `DrainToQueue` 之后的**残余字节**做（本节第 2 步）。热路径复用池，`Append` 扩容时先租后还，`Reset()` 归还并把游标归零。不要改用 `ArrayPool<byte>.Shared`——那套计数器不可观测，配平测试会失去依据。
- **恢复必须有低水位（M2）**：满即成 `Pause`，但**不能**"空出一格就 `Resume`"——那会变成每帧一次暂停/恢复抖动，把背压变成高频开关。`QueueCapacity` 满时暂停，**由消费侧**在出队后检查 `_queued <= ResumeWatermark`（`_queued` 是与 `TryWrite`/`TryRead` 同序维护的 `Interlocked` 计数；默认水位 `QueueCapacity / 2`）才恢复：先 `Interlocked.Exchange(ref _queuePaused, 0) == 1` 判定本次由自己恢复，再调 `stream.Resume()` 并 `_resumeSignal.Release()`（`SemaphoreSlim(0, 1)`；由状态位保证只 Release 一次，避免 `SemaphoreFullException`）。恢复动作放在消费侧而不是泵里：泵此时已被闸门挡住，不会再有下一轮迭代。
- **暂停/恢复的空窗竞态（必须处理）**：消费侧可能在泵执行 `Pause()` **之前**就把队列排空，此后不再有出队事件触发复核，连接会永久停读。因此泵在 `Pause()` 之后必须**立即再复核一次**队列长度：若已 `<= ResumeWatermark`，立刻 `Resume()` 并清暂停标记。暂停标记用 `Interlocked` 的 0/1 状态位，不要用裸 `bool`；`Resume` 只在"状态位 1→0"成功的那一方调用，保证成对且只调一次。
- **队列长度不要依赖 `Channel<T>.Count` 的实现细节**：用 `Interlocked` 自维护的 `_queued` 计数，比 `ChannelReader<T>.Count` 在 `netstandard2.0` 下更可控，也便于把"当前队列长度"暴露给 `ChannelTraffic`。**顺序必须写死**：入队路径先 `Interlocked.Increment(ref _queued)` 再 `TryWrite`，失败立即 `Decrement` 归还（若先写后加，消费者可能在加计数前就把条目取走，把计数减成负数）；出队路径 `TryRead` 成功后再 `Decrement`。低水位判定 `_queued <= ResumeWatermark` 用同一份计数。
- **`ChannelTraffic` 的字段来源**：`TotalReceivedBytes` 由泵在 `Append` 后累加（`long`，只增不减）；`BufferedBytes = accumulator.BufferedLength`；`QueuedMessages = _queued`。每轮读之后构造一次快照，不额外加锁（三个值各自原子读，允许轻微不同步——它是防护钩子，不是账本）。

**半包耗尽（slowloris）防护——`MaxFrameLength` 挡不住这个：**

`MaxFrameLength` 限制的是"单帧上限"，攻击方式是每帧都合法、但永远凑不满一帧（或每次只发 1–2 字节），于是 `FrameAccumulator` 里的未成帧字节持续增长，直到某个上限才停。两层防护都要有：

1. **缓冲硬上限**：`ChannelPipelineOptions.MaxBufferedBytes`（默认 1MB）。在每轮 `DrainToQueue` **之后**检查剩余 `BufferedLength`（此时剩下的都是凑不成一帧的尾巴；先排空完整帧，避免"一次读到很多合法帧"被误杀）；若仍 `> MaxBufferedBytes`，以 `reason = "buffered-bytes-limit"` 关闭连接，不等待、不先塞队列。`MaxBufferedBytes` 统计的是 accumulator 里未成帧的**残余字节**，不是单帧长度。
2. **连接级流量计数钩子**：`IChannelGuard`，每次读循环迭代检查一次（不是每字节），业务可据总量/速率做封禁：

```csharp
public readonly struct ChannelTraffic
{
    public long TotalReceivedBytes { get; }
    public long BufferedBytes { get; }
    public int QueuedMessages { get; }
}

public interface IChannelGuard
{
    /// <summary>返回 false 表示超限，调用方走 §6.3 收口（reason = "guard-rejected"）。</summary>
    bool Allow(in ChannelTraffic traffic);
}
```

- 默认实现 `NullChannelGuard` 恒返回 true；`ChannelPipelineOptions.Guard` 可挂业务实现（总字节上限、令牌桶速率限制均可在此做）。
- 钩子抛异常按"超限"处理并记录，不放行——防护类代码不允许 fail-open。
- `Allow(in ChannelTraffic)` 在 `netstandard2.0` 下会为 `readonly struct` 生成 defensive copy（每个读循环一次，非每字节），功能无碍、成本可忽略；这是选 `in` 的已知代价，不要为此改成 `class`（那会每轮分配一个对象）。

**`MaxBufferedBytes` 与 `MaxFrameLength` 的关系（B3，必须做配置校验）：**

`MaxBufferedBytes` 是"拆帧缓冲总量上限"，`MaxFrameLength` 是"单帧上限"。若前者小于后者，一个**合法**的大帧会先被缓冲上限杀掉，`MaxFrameLength` 形同虚设——这是最容易在联调时才发现的配置陷阱。规则：

- `MaxBufferedBytes == 0` → 自动推导为 `MaxFrameLength + P_Head + ReadBufferSize`（`ReadBufferSize` 只是给"最大合法帧 + 一点尾巴"留的余量，默认 64KB；它**不参与** `FrameAccumulator` 的物理容量，见本节上文）。**推导与校验都发生在 `ChannelPipeline.Create` 内**，结果作为"有效上限"存进 pipeline；运行期（含泵的每轮检查）一律用这个有效值，不再判断 0。
- `MaxBufferedBytes != 0` 且 `< MaxFrameLength + P_Head` → `Create()` 抛 `InvalidOperationException`（否则一个合法最大帧连整帧都攒不下），消息给出两个值和推导公式。
- 默认 1MB 只适用于 `MaxFrameLength <= 960KB`；协议允许更大帧时必须显式配置，验收用例 6/15 各覆盖一侧。
- 两个上限各自独立生效：缓冲上限在每轮 `DrainToQueue` **之后**检查剩余 `BufferedLength`（只管"攒不出来"的攻击，见本节第 1 条）；帧长上限在 `TrySplit` 里检查（只管"单帧过大"）。
- 对 `DelimiterSplitter` / `FixedLengthSplitter`，`MaxFrameLength` 按接口注释指**整帧长度**，此时 `+ P_Head` 只是让缓冲多留 9 字节余量，校验仍然成立（偏保守，不会误杀）。

**应用级暂停必须独立成源，不能复用 `IsReceivePaused`。** 既有 `_receivePaused` 是 Pipe 写端背压的单布尔：`PauseReceive(flushTask)` 置位，flush 续体无条件清位并回调 `_receiveResume`；`ResumeReceive` 又靠 `if (connection.IsReceivePaused) return;` 去重。两条独立来源共用一个布尔，必然出现"应用暂停被 flush 续体清掉"或"应用 Resume 被 early-return 吞掉，连接再也不收数据"。

落地方式：`SAEA.Sockets` 侧把接收闸门改成**双来源**——保留 Pipe 背压位，新增一个应用级挂起位（或引用计数），`ProcessReceive` 前判断"任一来源挂起则不再投递 `ReceiveAsync`"；钩子的 `Pause/Resume` 只动应用级那一位。这是 `SAEA.Sockets` 里唯一必须改动既有逻辑的地方，需单独回归（含 Pipe 背压与 TLS 路径）。

- 暂停期间已有一个在途 `ReceiveAsync`，其数据仍会写进 Pipe 最多 `ReadBufferSize`（默认 64KB）。因此"暂停后缓冲完全不增长"做不到严格断言，验收口径改为：**通道链队列长度不增长**（见 §8.1 第 4 条）。

### 6.3 队列与并发

```csharp
public enum DisconnectDrainPolicy
{
    /// <summary>网关场景：断开即放弃。在途消息立即取消，队列内未开始的丢弃并归还。</summary>
    CancelPending = 0,
    /// <summary>业务场景：断开后让已开始的消息自然跑完，队列内未开始的丢弃并归还。</summary>
    DrainInFlight = 1
}

public sealed class ChannelPipelineOptions
{
    public IFrameSplitter Splitter { get; set; }            // 泵分帧用；null → Create 抛，见 6.2
    public int QueueCapacity { get; set; } = 1024;          // 每连接有界队列
    public int MaxConcurrentMessages { get; set; } = 16;    // 每连接在途消息上限
    public int ResumeWatermark { get; set; } = 0;           // 0 = 自动取 QueueCapacity/2，见 6.2
    public TimeSpan MessageTimeout { get; set; } = TimeSpan.Zero;  // Zero = 不超时
    public DisconnectDrainPolicy DrainPolicy { get; set; } = DisconnectDrainPolicy.CancelPending;
    public TimeSpan DrainTimeout { get; set; } = TimeSpan.FromSeconds(5);  // 仅 DrainInFlight 生效，Zero = 无限等
    public long MaxBufferedBytes { get; set; } = 1024 * 1024;  // 单连接未成帧残余上限；0 = 按 MaxFrameLength 推导，见 6.2
    public int ReadBufferSize { get; set; } = 64 * 1024;    // 推导 MaxBufferedBytes 的尾部余量；不限制单次 ReadAsync 的返回长度，见 6.2
    public int SmallFrameInlineThreshold { get; set; } = 512;   // 小于此长度的帧不走池，见 5.4
    public int EnvelopeCopyThreshold { get; set; } = 4 * 1024; // 超过则 EnvelopeUnwrapHandler 记 Warn；不会改走 passthrough，见 5.3.1
    public IChannelGuard Guard { get; set; } = NullChannelGuard.Instance; // 连接级流量防护，见 6.2
}
```

**选项校验（全部在 `ChannelPipeline.Create(options)` 返回前失败，不留到运行期）：**

- `Splitter != null`（空引用直接抛，别等到第一帧）；`QueueCapacity >= 2`；`MaxConcurrentMessages >= 1`。
- `ResumeWatermark`：0 表示自动取 `max(1, QueueCapacity / 2)`；显式值必须 `0 < ResumeWatermark < QueueCapacity`，否则抛。
- `MessageTimeout >= TimeSpan.Zero`、`DrainTimeout >= TimeSpan.Zero`（`DrainInFlight` 下 Zero = 无限等，调用方需显式承担）。
- `SmallFrameInlineThreshold >= 0`（0 = 全部走池）；`EnvelopeCopyThreshold >= 0`；`ReadBufferSize > 0`（推导公式的分母，0 会让上限失效）。
- `MaxBufferedBytes` 见 §6.2；`Splitter.MaxFrameLength >= 0`（0 只对"允许空 body"的协议有意义，实现按分帧器口径解释）。
- `Guard == null` 时按 `NullChannelGuard.Instance` 处理（不抛，保持"未配置即不防护"的直觉）；若实现选择抛错，必须同步改本节。

**断开收口（两种策略共用骨架，只有第 4 步不同）：**

第 1 步是**同步且幂等**的入口 `BeginShutdown(reason)`，第 2–6 步在后台 Task 上执行：

1. 置 `_closing = true`、`IChannel.IsActive = false`；此后 `IRawByteStream.SendAsync` 与 `ctx.WriteAsync` 立即抛 `ChannelClosedException`（见本节"断开后的回包"），不排队、不静默丢。**本步不取消消息令牌**——是否取消在第 4 步按策略决定，否则 `DrainInFlight` 会退化成 `CancelPending`。
2. **停泵（仅外部触发时）**：取消泵的 `CancellationToken`（executor 内部的 `_pumpCts`，与传输层传入的 ct 通过 `CreateLinkedTokenSource` 联动；传输层 ct 直接取消时 executor 注册一次回调转调 `BeginShutdown`，保证收口不被跳过）并**等泵协程返回**——`FrameAccumulator` 有线程亲和性，只能由泵自己在 `finally` 里 `Reset()` 归还池化缓冲，收口线程不许直接动它。泵退出后 `queue.Writer.TryComplete()`。泵的等待点只有 `ReadAsync` / `WaitQueueSpaceAsync` / 轻量通知，前两者都吃 ct；通知 handler 被 §5.1 限制为轻量同步工作，因此不设额外超时。**若收口由泵自身触发**（超限、守卫拒绝、读异常、EOF），跳过本步，`accumulator.Reset()` / `reader.Complete()` 由泵的 `finally` 补做。
3. 队列内**尚未开始**的消息一律丢弃并 `Release()`（两种策略相同）。消费者循环每轮先看 `_closing`：已置位就不再启动新消息，`TryRead` 出来的条目只做 `Release()` 与计数递减，直到队列空。
4. **策略分叉**：`CancelPending` 取消 `_connectionToken`（未配置超时时消息令牌就是它；配置超时时 linked token 随之取消，见本节"消费侧"），不等自然完成，直接推进（`finally` 会很快跑完）；`DrainInFlight` **不取消** `_connectionToken`，等在途消息自然结束，最多 `DrainTimeout`。
5. 等待所有已开始的链走完其 `finally`（`CancelPending` 下即"取消落地"）；`DrainInFlight` 最多等到 `DrainTimeout`，到点后放弃等待、继续收口，剩余在途消息各自跑完后由自己的 `finally` 释放引用计数（收口不再等它们，也不阻止它们结束）。**"引用计数全部归零"只在未超时的路径上可作为收口完成的条件**；超时路径由测试轮询等待归零，不能把它当成 `Completion` 的前提。
6. 清空 `Attributes`，从 `IPipelineHost` 移除该连接，逐个 handler `Count--`（§5.1.1），最后 `IRawByteStream.CloseAsync(reason)`。

**`CloseAsync` 与 `ShutdownAsync` 的分工（防自等待死锁）：**

- `IChannel.CloseAsync` / `IChannelPipeline.CloseAsync` / `IPipelineHost.CloseAsync` 只执行第 1 步，然后**在后台启动第 2–6 步并立即返回**。返回值表示"已进入收口"，**不表示资源回收完成**。否则 handler 里 `await ctx.Channel.CloseAsync()` 会在第 5 步"等待在途消息"时等自己，永久卡死。这一点必须写进 XML 注释与 README。
- 泵自身触发的收口走内部 `ShutdownAsync(reason)`，它 `await` 第 2–6 步完成；第 2 步按上面的规则跳过自等待。
- 需要"彻底回收"的调用方（传输层、测试）等待 `executor.Completion`（第 2–6 步的后台 Task）。
- **幂等**：`BeginShutdown` 用一把锁 + `_closing` 判定保证第 2–6 步只启动一次；并发调用者都拿到同一个 `Completion`，不允许跑两遍第 3–6 步（会重复 `Release` 队列条目、重复 `Count--`）。

`DisconnectDrainPolicy` 必须可配：网关类服务选 `CancelPending`（不给慢鉴权拖住连接回收），业务服务选 `DrainInFlight`（已经开始处理的写操作要跑完，避免半成品）。`DrainInFlight` 必须有 `DrainTimeout` 兜底，否则一个卡死的 handler 会让连接永远回收不了。

**断开后的回包（H2）：`DrainInFlight` 保的是副作用，不是回包。**

- 连接一进入收口（第 1 步开始），`IRawByteStream.SendAsync` 与 `ctx.WriteAsync` 立即抛 `ChannelClosedException`，**不排队、不静默丢**——静默丢会让业务以为"回包发出去了"。该判定先于消息类型判定：即使业务持有的是已摘链节点的旧 `ctx`、传出的也不是 `TransportFrame`，只要连接在收口，报的就是 `ChannelClosedException`。
- 在途 handler 仍可跑完自己的副作用；跑到回包那一步若抛 `ChannelClosedException`，按普通 handler 异常走 `ExceptionCaught`（链尾记录），**不延长收口、不影响其它消息**。业务若希望"断线后只做副作用不回包"，调用前判断 `ctx.Channel.IsActive`，或捕获该异常并忽略。
- 这条语义写进 `DisconnectDrainPolicy` 的 XML 注释原文：**"跑完" = 让已开始的业务副作用完成，不代表回包能送达对端。**

- 消费侧：`Channel<PooledMessage>`（`System.Threading.Channels`，`BoundedChannelFullMode.Wait`）+ **单消费者循环** + `SemaphoreSlim(MaxConcurrentMessages)`：循环 `TryRead` 后先 `await` 信号量，再**在该协程上直接启动**该消息的执行链且不等待；消息同步完成时不排队、不额外分配 `Task`，只有真正 `await` 挂起的消息才由 async 状态机自带的任务继续，并登记进在途表供收口等待。每条消息起一条独立的链，**不保证完成顺序**（已确认：响应返回顺序 ≠ 接收顺序，客户端按请求 id 自行配对）。**代价**：同步阻塞或 CPU 密集的 handler 会占用消费协程并推迟同连接后续帧，这类 handler 需要自行 `Task.Run` 或改成异步；异步 handler（如 20–50ms 鉴权）不受影响，仍可并发。
- **令牌按需创建**：`MessageTimeout == Zero`（默认）时消息令牌就是 `_connectionToken.Token`，不建 linked CTS、不写 `AsyncLocal`；`MessageTimeout > Zero` 时才 `CancellationTokenSource.CreateLinkedTokenSource(_connectionToken.Token)` 并 `CancelAfter(MessageTimeout)`，执行包 `finally` 里恢复 `AsyncLocal` 并 `Dispose`。`ShutdownAsync` 的 `CancelPending` 只需取消 `_connectionToken`，所有在途消息自动收到取消。
- **执行包（所有权边界）**：`try { await pipeline.FireChannelReadAsync(msg); } finally { msg.Release(); }`——`FireChannelReadAsync` 从链首入站 handler 开始，异常已在链内转成 `ExceptionCaught`，`finally` 只负责引用计数。`QueueCapacity` 管"待处理"，`MaxConcurrentMessages` 管"在途"，两者独立，不能互相替代。
- **令牌的注入点**：只有配置了 `MessageTimeout` 才需要注入——执行包在调用 `FireChannelReadAsync` 前把该消息的 token 赋给 `AsyncLocal<CancellationToken>`，`finally` 恢复原值；未配置超时时 `ctx.CancellationToken` 直接回落到连接级 token。**不要为每条消息新建 context**（context 是链节点，数量固定）。`ChannelReadCompleteAsync` 在消息执行之外调用，读到的是连接级 ct。
- 单条消息内部严格串行：`解码 → 鉴权 → 路由`，`await` 期间不影响同连接其它消息（分帧已在入队前完成，不在这条链上）。
- 超时：`MessageTimeout > Zero` 时才给该消息的 linked CTS 调 `CancelAfter`（连接 ct + 超时 ct）；`finally` 里 `Dispose`。**判定口径**：执行链抛 `OperationCanceledException` 且此时 `_closing == false`、该消息 CTS 已取消、连接令牌未取消 → executor 构造 `MessageTimeoutException`（含 `ChannelId` 与超时时长）并 `FireExceptionCaughtAsync`，由链尾 `Warn` 记录、**不关闭连接**；连接收口导致的取消只记 `Debug`，不上报为错误。`CancelAfter` 只影响该消息，绝不取消 `_connectionToken`。**取消是协作式的**：handler 忽略 `ctx.CancellationToken` 就会照常跑完，executor 不强行中止，也不为超时额外等待（验收 24 的第二段就是这条语义）。
- 关闭：按上面的"断开收口顺序"执行；`DrainPolicy` 决定在途消息是取消还是跑完，队列内未开始的一律丢弃归还。

### 6.4 启动时序（硬要求）

泵的启动点必须晚于应用认为"连接可用了"的时刻，否则业务 handler 会在会话状态尚未建立时跑鉴权：

- **普通 `UseStream()`**：`RaiseAccepted`（`OnAccepted` 订阅者全部执行完）**之后**才启动通道链接收泵。
- **TLS**：`connection.HandshakeTask` 完全成功、且 `OnAccepted` 执行完之后才启动；握手失败直接关闭，不启动。
- 同时把一条约束写进 README：**禁止在 `OnAccepted` 回调内做长时间阻塞 IO**，否则会推迟该连接的泵启动（这正是今天 `StartSpanPump` 在 `RaiseAccepted` 之前的原因，改成之后等于把这条责任显式交给调用方）。
- 实现落点：**只对配置了 `UseInputHandler` 的连接**，把启动点挪到 `RaiseAccepted` 之后（TLS 为握手续体之后）；**未配置钩子的连接，`StartSpanPump` 保持当前位置不变**，以免改动既有"接收回调可能先于 `OnAccepted`"这一既成时序（`2026-09-21` 文档把回调顺序列为硬不变量）。
- 由此产生一条配置约束：同一进程里，配置了钩子的连接其 `OnAccepted` 一定早于首帧，未配置的连接不保证——README 需写明，避免用户拿两种连接对比时序时误判为 bug。
- **启动前到达的字节不会丢**：泵启动前的数据由 Pipe 自己承接（写端照常写入），只是暂不读取。Pipe 的 `PauseWriterThreshold` 会给写端背压，因此不会无限增长；泵启动后第一轮 `ReadAsync` 就能读到这些积压。实现者不要因为"首帧可能早于第一次 `ReadAsync`"而额外加读循环或预读缓冲。
- **`OnAccepted` 抛异常**：视为连接建立失败——不启动泵，关闭该连接并 `OnError` 上报，不能留下"channel 已登记、泵没启动"的半初始化连接。TLS 路径同样如此（续体里 `RaiseAccepted` 抛错后不得继续启动泵）。实现时把"启动泵"放在 `RaiseAccepted` 之后的显式 `try/catch` 里，而不是依赖回调不抛；`UseConnectionOpened` 的异常同样在两处调用点显式捕获并按此处理（见 §5.1）。

### 6.5 观测

`InboundLoggingHandler` 记录：`ChannelId`、入站实体 CLR 类型、耗时（`Stopwatch`，口径 = 从它调用下游到下游返回的时间，包含 `await`，不是整条链耗时）。它位于 unwrap 之后，看到的是业务实体，**没有** `Payload` 字段；payload 长度由 `EnvelopeUnwrapHandler` 的 `EnvelopeCopyThreshold` 分支负责记录。上游 handler 的异常不会经过它（走 `ExceptionCaught` 到链尾），异常类型由 `ExceptionTerminalHandler` 记录。`OutboundLoggingHandler` 记录：`ChannelId`、`EncodedBody.TypeCode`、`Body.Length`、耗时。两者都不做全局静态配置，采样率与开关由构造参数决定；耗时统计必须包含 `await` 期间的时间（这正是要观测的部分）。**出站日志看不到最终帧字节**——它在链上位于 `FrameEncoderHandler` 的 **tail 一侧**（出站方向先于 `FrameEncoderHandler` 执行），这是为了避免为日志再解一次帧头；需要抓包级原始字节时用 `FramePassthroughHandler` 分支。

---

## 七、既有部分如何处置

| 既有物 | 处置 |
|---|---|
| `OnServerReceiveSpan` / `OnClientReceiveSpan` | 保持不变；仅在配置了 `UseInputHandler` 的连接上不触发 |
| `ICoder` / `IFrameCoder` / `IContext<ICoder>` / `BaseCoder` / `BaseContext` | 保持不变，不加 `[Obsolete]`。新栈与之并列 |
| `Base/FrameDecoder.cs` | 保持不变（`internal`）；新 `FrameAccumulator` + `LengthFieldSplitter` 是它的参数化重写，不共享代码，靠 parity 测试保证一致 |
| `SaeaServerSocket.StartSpanPump` | 保持；增加"若已配置输入处理委托则跳过"的分支（该分支只在钩子启用时走新路径） |
| `SaeaConnection.IsReceivePaused` / `ProcessReceive` 的暂停 early-return | **不能直接复用**：改为双来源闸门（保留 Pipe 背压位 + 新增应用级挂起位），见 §6.2。**这是公共路径，未配钩子的连接同样走新逻辑，需全量回归（普通 / TLS / 背压三条路径）** |
| `SAEA.*Test` 控制台工程 | 不改造；新增独立演示工程 |

**为什么并列而不是替换**：`ICoder` 的实现分布在 7 个项目共 8 个类（见 `2026-09-21-span-memory-pipeline-design.md` §一），替换它们等于一次全解决方案破坏性重构；而本次要解决的是"新业务不想再写第三遍"，并列一套、老代码零感知，回滚成本最低。

---

## 八、验收

### 8.1 功能验收（`Src/SAEA.Sockets.PipelineTest --all`）

1. **回环**：内存 `IRawByteStream` 上跑完整链，`LoginRequest` 正确路由到 `LoginHandler` 并编码回包。
2. **半包/粘包**：同一帧拆成 1/2/3/7 字节喂入；三帧粘成一次 `ReadAsync`；两种情形都产出正确帧序列。
3. **慢鉴权不阻塞**：`AuthHandler` 对 5 条消息分别延迟 200/160/120/80/40ms（**必须用不同延迟**，等延迟会让"完成顺序"断言 flaky）；同连接连发 5 条，断言完成顺序为 40→80→120→160→200、完成顺序 ≠ 到达顺序、峰值并发 > 1、总耗时 < 5×200ms（应接近 200ms 量级）。
4. **背压与低水位**：`QueueCapacity=4`、`MaxConcurrentMessages=1`、消费侧人为阻塞第一条消息，断言 `Pause` 被调用、**通道链队列长度停在 4 不增长**（若并发上限不设为 1，4 条消息会全部变成"在途"，队列根本填不满，Pause 不会触发）；消费到 `ResumeWatermark`（2）之前不断言 `Resume`，到 2 时才 `Resume`，恢复后积压消息全部处理完（Pipe 内部缓冲上限是 `PauseWriterThreshold`，默认 256KB，不作断言）。再断言"空出一格不立即恢复"——队列 4→3 时 `Resume` 不得被调用。**另断言"截断帧不丢"**：队列满时被 `DrainToQueue` 挡下的那一帧仍留在 `FrameAccumulator`，恢复后必须被入队并处理（按帧序号/内容校验），且暂停期间泵没有调用 `ReadAsync`。
5. **异常**：handler 抛错 → `ExceptionCaught` 到链尾 → 记录 + `CloseAsync`，且队列内剩余消息被归还（无泄漏）。
6. **超长帧**：`bodyLen > MaxFrameLength` → `FrameProtocolException` + 断开。
7. **出站**：`ctx.WriteAsync(entity)` 经 `EnvelopeWrapHandler` + `CodecEncoderHandler` + `FrameEncoderHandler` 后落到底层，字节与"手工 `ICodec.Encode` 得到 body、再按 `BaseSocketProtocal.WriteTo` 手工拼 **8B body 长度 + 1B type**"逐字节一致；`owner` 在发送完成回调后释放（用可观测的 owner 断言）。
8. **内存**：跑 10 万帧，`MemoryPoolManager` 租借/归还计数配平，无 `PooledMessage` 泄漏。
9. **暂停来源隔离**：模拟 Pipe 写端背压（flush 续体清位）与通道链应用暂停同时发生，断言应用暂停不被清除、恢复后连接继续收数据。
10. **配置校验（三组，分别验证三处入口）**：① 服务端只开 `.UseIocp()` 却配 `UseInputHandler`/`UseConnectionOpened` → `SocketOptionBuilder.Build()` 抛 `InvalidOperationException`，且未启动监听；② `MaxBufferedBytes != 0 && MaxBufferedBytes < Splitter.MaxFrameLength + P_Head` → `ChannelPipeline.Create` 抛错；`MaxBufferedBytes = 0` 时推导为 `MaxFrameLength + P_Head + ReadBufferSize`，且一个 `MaxFrameLength` 大小的合法帧（body + 9B 头）能正常拆出（不被缓冲上限误杀）；`Splitter == null` → `Create` 抛；③ 客户端配置任一钩子 → 构造 `SaeaClientSocket` 时抛 `NotSupportedException`。
11. **启动时序**：`OnAccepted` 里写入会话映射并做 50ms 延迟，断言第一帧到达时映射已存在；TLS 下断言首帧在握手成功之后。
12. **出站契约**：① 对同一条 `msg` 调用两次 `ctx.WriteAsync` 的坏 handler，`Debug` 下断言被检出；② 只调用一次的正常 handler，断言不触发；③ **未转发即返回**（截断出站链）在原理上无法自动检测——"明确丢弃"与"忘了转发"对外不可区分，只能靠代码评审与端到端用例（断言回包确实到达）发现。README 必须写明：Debug 断言只覆盖①，不覆盖③。`IRawByteStream.SendAsync` 绕过 head sink 的情形由代码评审禁止（§5.2），不做运行时检测。
13. **`ChannelReadComplete` 粒度与时机**：一次 `ReadAsync` 产出 3 条帧，断言 `ChannelReadCompleteAsync` 只被调用 **1 次**，且发生在 3 条帧**全部入队之后**、**3 条消息都还没执行完**（用阻塞 handler 卡住，断言通知先到）。不要断言"执行链已启动"——入队与消费者拾取是异步的，没有同步保证。再用一个 50ms 的 `ChannelReadCompleteAsync`，断言它不会让消息执行串行化（3 条仍并发）。
14. **断开策略**：同一组用例分别跑 `CancelPending` 与 `DrainInFlight`——前者断言在途消息被取消、总耗时 ≈ 0；后者断言在途消息跑完、队列内未开始的消息被丢弃归还；再给一个卡死 handler，断言 `DrainTimeout` 到点后仍能回收。
15. **半包耗尽**：只发 1 字节/次且永不成帧，断言 `MaxBufferedBytes` 到点后断开、`FrameAccumulator` 缓冲归零；再用 `IChannelGuard` 恒 false 验证同路径断开。
16. **`IsSharable` 计数**：非 sharable handler `AddLast` → `Remove` → 再 `AddLast` 到新 pipeline，**必须成功**（验证计数已归还）；pipeline 未移除就复用则必须抛。
17. **`Attributes` 并发**：同连接 16 条消息并发写不同 key，断言互不覆盖、`GetAttribute` 全可读；连接关闭后 `IsActive == false`、`Attributes` 清空。
18. **Envelope 不持有池内存**：解码后强制归还全部 `PooledMessage`（模拟池被复写），断言 `Envelope.Payload` 内容仍正确。
19. **鉴权在反序列化之前**：未认证连接发一个非白名单 type 的帧，断言 `EnvelopeUnwrapHandler`（用计数 codec / 计数 handler 观测）**一次都没被调用**、连接被关闭；再发白名单 type，断言放行。
20. **断开后回包**：`DrainInFlight` 下让在途 handler 在收口后才 `ctx.WriteAsync`，断言抛 `ChannelClosedException`、副作用（写文件/计数器）已完成、收口总耗时不超过 `DrainTimeout`。
21. **取消传播**：泵运行中取消 `UseInputHandler` 的 `CancellationToken`，断言 500ms 内泵退出、队列与 accumulator 缓冲归零、引用计数配平。
22. **小帧分配（M1）**：先预热到 `FrameAccumulator` 不再扩容、且队列/在途都已清空，记录 `MemoryPoolManager` 的租借计数基线；再连续投递 10 万条 20B 帧，断言 `Rent` 增量 **0**（accumulator 已稳定，不再借新块）、每条 `PooledMessage.IsPooled == false`；再投一条 8KB 帧，断言 `PooledMessage` 走池且归还。**不要**用"按来源归属计数"的自定义计数器——用"预热后增量为 0"就足以证明小帧没走池。
23. **未命中路由（M3）**：`type code` 已注册、实体已还原，但该实体类型没有在 `MessageDispatcher` 注册且未配 `Fallback` → 断言 `Warn` 记录、后续消息仍正常处理、连接不断开；配了 `Fallback` → 断言由它处理，`Warn` 不再出现。**不要与"type code 未注册"混淆**：后者在 `EnvelopeUnwrapHandler` 就抛错并断开（§5.3.1），不走本用例。
24. **单消息超时**：`MessageTimeout=100ms`、`MaxConcurrentMessages=2`；一条 handler 等待 `ctx.CancellationToken` 直到取消，另一条 20ms 完成。断言超时消息的 token 被取消、`ExceptionCaught` 记录超时刻、连接仍 `IsActive`、后一条正常完成；再放一个忽略 token、忙等 300ms 的 handler，断言不强杀、不阻塞其它消息。
25. **外部断开（不是对端 EOF）**：连接空闲且泵正在 `ReadAsync` 时调用 `IServerSocket.Disconnect(sessionID)`（内部 `SaeaConnection.Dispose` → `CancelPendingRead`），断言 500ms 内泵退出、收口完成、引用计数配平、`Attributes` 清空；再断言泵退出后 200ms 内没有新的 `ReadAsync` 调用（不会因 `IsCanceled` 忙循环）。同时断言 `CloseAsync` 对已关闭连接不抛。

### 8.2 Parity 验收

- `LengthFieldSplitter.LegacyCompatible(...)`（`initialBytesToStrip=0`）对同一批字节输入，与 `BaseCoder.DecodeStream` 逐帧比对一致：消费字节数、`BodyLength`、`Type`、`Content` 四项分别相等（`SocketFrame` 不含包头，所以**不能**断言整帧字节相等）；覆盖含 `P_Head`、类型字节、空 body、多段序列。
- `LengthFieldSplitter.Default(...)` 的 `consumed` 与 `LegacyCompatible` 完全相同，仅 `frame` 少 8 字节（剥掉长度字段），保留 `[1B type][body]`；用同一批输入断言两条切分边界的对应关系，并断言 `Default` 的帧首字节 == `LegacyCompatible` 帧的**下标 8**（0-based，即 1B type 所在位置），不是"第 9 个字节"这种易混口径。
- **编码器 parity（B1 的守卫）**：`LengthFieldFrameEncoder.Encode(body, type, writer)` 的输出，与 `BaseSocketProtocal`（`WriteTo`）对同一 `BodyLength` / `Type` / `Content` 的输出**逐字节相等**，覆盖 body 长度为 0、1、`P_Head` 跨界、`ushort`/`int` 边界值。断言首 8 字节按小端解出的整数等于 **body.Length**（不是 `body.Length + 9`）；若实现误写总长，本用例必须失败。

### 8.3 回归验收

- `dotnet build Src/SAEA.Sockets.sln -c Debug` → 0 errors（warnings 与基线对照：当前基线 93 条，多为内嵌 Newtonsoft.Json 的 CS3021，不因本次新增而增加）。
- `dotnet test Src/SAEA.Common.Tests/...`、`SAEA.MQTT.Tests`、`SAEA.FTP.Tests` 全绿（59 / 153 / 13）。
- `SAEA.P2PTest --all`（另跑 `--tls`、`--tls-stress`）、`SAEA.QueueSocketTest --functional` 不受影响。双来源闸门是公共路径，普通流与 TLS 两条读路径都必须跑到，这是本次唯一会碰既有逻辑的改动。

### 8.4 性能门槛（不作硬门禁，作趋势）

- 与"手写回调 + `BaseCoder.DecodeStream`"的直连写法相比，同机同负载下吞吐下降不超过 15%。
- **拷贝口径（B2）**：本栈入站每帧固定 **2 次字节拷贝**（Pipe → accumulator、accumulator → `PooledMessage`）；既有直连写法直接消费 Pipe 缓冲，只有 0–1 次。出站另计 **3 次**（实体 → `Envelope.Payload` 的 `byte[]`、`Payload` → `EncodedBody`、`EncodedBody` → 最终帧缓冲），这是"两级正交 + `IBufferWriter` 不能前插"的已知成本，v1 不优化。这 2/3 次是已知预算，性能对比按此口径，**不要拿"零拷贝"要求它**。
- **分配口径**：每帧额外分配 ≤ 2 次——小帧（`< SmallFrameInlineThreshold`）为 `PooledMessage` 的精确数组 + 队列节点；大帧走池时无数组分配，仅 `PooledMessage` + 队列节点。实测若长期超过 2 次，说明有隐藏拷贝，需回填。

**实测（2026-11-06，Windows / .NET 10.0.403，Release，`Src/SAEA.Sockets.PipelineTest --bench`）**：500000 帧、body 256B、按 64KB 分块喂入，每条路径预热 1 轮后取 5 轮中位数；`MaxConcurrentMessages = 8`。同一份基线先测了 v1，再测执行器热路径改造后的 v2 第一批（内联启动 + 令牌按需创建）：

| 路径 | v1 吞吐 | v2 吞吐 | 每帧分配（v2） |
|---|---|---|---|
| 旧栈 `BaseCoder.DecodeStream` 直连回调 | 约 1370 万帧/s | 约 1290–1360 万帧/s | 约 0–1B |
| 新栈 分帧 + `EnvelopeCodec` 解码 | 约 1700 万帧/s（旧栈 0.8–1.5×） | 约 1170–1660 万帧/s（旧栈 0.8–1.3×） | 约 312B |
| 新栈 接收泵 + 入队 + 处理器 | 约 76–84 万帧/s（旧栈 5.6–6.2%） | 约 197–317 万帧/s，中位数约 267 万（旧栈 15–25%，中位数 21.4%） | 约 475–480B |

- **解码层与旧栈同量级**：分帧 + 解码一层多次重复落在旧栈的 0.8–1.3× 之间，波动来自每轮约 156MB 分配引发的 GC，而不是路径本身更慢；稳定成本是 `Envelope` 与独立 payload 拷贝（256B body 对应约 312B/帧）。
- **完整接收路径从约 6% 提到约 15–25%，"下降不超过 15%"目标仍未达成**：执行器改造把 v1 的每消息 `Task.Run` + `TaskCompletionSource` 闸门 + 每消息 linked CTS 去掉，吞吐提升到 2.4–3.8×、每帧分配从约 1.2KB 降到约 480B。剩余差距来自每帧 2 次拷贝、`PooledMessage` 与队列节点、以及 `FireChannelReadAsync` 的每帧异步链；绝对吞吐约 197–317 万帧/s（256B 帧约 500–810MB/s）。
- **口径提醒**：基准里的 handler 是同步完成的，v2 之后该路径实际在消费协程上单核串行执行，v1 则靠线程池把 8 条消息并行摊到多核。因此上面的帧/s 提升来自"不再排队"，不是并行度增加；多核同步 handler 场景可以用固定 worker 循环换吞吐，但那会把每连接常驻任务数抬到 `MaxConcurrentMessages`。
- **分配预算仍未达成**：实测约 475–480B/帧，按 256B body 估算约 4 次对象分配/帧（精确数组、`PooledMessage`、队列节点，以及执行链上的异步机构）。上面"≤2 次"只覆盖数组与队列节点，后续批次要收敛得先做单拷贝（P3）与每帧分配压降。
- 该测量是单机趋势值，跨机器比较需重跑同一条命令；`--bench` 不进 CI，也不作合并门禁。
- 基准脚本本身把整段 wire 写入 `Pipe`，含一次基准侧的 Pipe 拷贝；真实 socket 读路径没有这次拷贝，因此完整路径的实际分配应略低于上表。

---

## 九、里程碑

| # | 内容 | 交付物 |
|---|---|---|
| 1 | 核心抽象（含三个适配器基类、`FrameConstants`）+ `FrameAccumulator` + 四个分帧器（`LengthFieldSplitter` / `DelimiterSplitter` / `LineSplitter` / `FixedLengthSplitter`）+ `LengthFieldFrameEncoder` + parity 测试 | 可独立跑的 `SAEA.Sockets.Pipeline.Tests` |
| 2 | `ChannelPipeline.Create(options)`（含 Context / `Attach` / `Dispose` 计数）+ `ChannelPipelineExecutor`（队列、并发、背压与低水位、断开策略、选项校验、出站前缀校验） | 回环 + 半包/粘包 + 背压 + 断开策略用例绿 |
| 3 | `ICodec` + `IMessageCodec` + 内置 codec（`EnvelopeCodec` / `JsonMessageCodec` / `ProtobufMessageCodec` / `RawCopyCodec`）+ Envelope 系列 handler + `MessageDispatcher` + 两个日志 handler | 路由、鉴权顺序与出站用例绿 |
| 4 | `SAEA.Sockets` 输入钩子 + 双来源闸门 + 演示工程端到端 + 内存配平 | `SAEA.Sockets.PipelineTest --all` 绿、sln 0 errors、三条读路径回归 |
| 5 | README 双语 + 版本号 + 回归跑全 | `README.md` / `README.en.md` 同步 |

---

## 十、实现约定（v1 已定项）

以下几项是 v1 的实现约定；标注"可微调"的参数允许在实现期结合实际数据调整，但调整后**必须同步改本文档**：

1. **`ChannelPipeline` 的装配入口** —— 已定：公开静态工厂 `ChannelPipeline.Create(ChannelPipelineOptions options)`，校验在 `Create` 内同步执行；不再引入 `ChannelPipelineBuilder`。
2. **`ChannelReadCompleteAsync`** —— 已定：v1 要，语义见 §5.1（每轮成功排空 1 次、**全部入队之后**触发、不保证执行链已开始、不代表执行完成、不产生空通知）。
3. **两个日志 handler 的埋点出口** —— 都用 `SAEA.Common.LogHelper`（`Src/SAEA.Common/LogHelper.cs:47`，`Info/Warn/Error/Debug` 那组签名），不引入 `ILogger` 抽象。注意 `SAEA.Common` 里**没有**名为 `Logger` 的类型，早先写的 `Logger` 是笔误。
4. **`PooledMessage` 的池化阈值** —— 已定：`SmallFrameInlineThreshold`（默认 512B）以下走精确 `new byte[len]`，以上走 `MemoryPoolManager`。原因是 `Rent` 对小于 4KB 的请求按 4KB 发放（`MemoryPoolManager.cs:150-163`），小帧走池等于 200 倍放大。数值 512 可在实现时结合 `2026-09-23` 文档的池化口径微调，并回填。
5. **`FrameAccumulator` 与既有 `FrameDecoder` 是否合并** —— 倾向不合，避免动到既有内核；两者一致性靠 parity 测试守住。
6. **`Envelope.Payload` 的拷贝实现** —— 已定：独立 `byte[]`，精确 `new byte[len]`，不借用池（无归还责任）；`EnvelopeCopyThreshold` 只用于 `Warn`。若实测 JSON 反序列化开销成为瓶颈，再评估"池化 + owner"方案并回填。

**已定，不再讨论：** 强制 `.UseStream()`；v1 仅 Envelope 单拓扑 + 实体类型路由；泵在 `RaiseAccepted` 之后（TLS 在握手完成之后）启动，`IChannel` 由 `UseConnectionOpened` 在 `RaiseAccepted` 内部、通知 `OnAccepted` 之前创建；v1 仅服务端；`Envelope.Payload` 绝不指向池化帧体，大帧由**装配期**选择 `FramePassthroughHandler` 分支（单链内不按帧切换，`EnvelopeCopyThreshold` 只作 `Warn` 阈值）；断开行为用 `DisconnectDrainPolicy` 枚举，不用单一写死策略；**鉴权在反序列化之前**（两级，§5.3.1）；**handler 继承适配器基类**，不直接实现接口（C# 8 无 default interface methods，§5.1）；**`AdvanceTo(buffer.End)` + 每帧 2 次拷贝**（§6.2）；**`MaxBufferedBytes` 必须与 `MaxFrameLength` 做配置校验**（§6.2）；**背压恢复有低水位且需处理暂停空窗竞态**（§6.2）；**未命中路由不得静默丢弃**（§5.5）。

---

## 十一、风险

| 风险 | 影响 | 缓解 |
|---|---|---|
| 用户在同一 handler 实例上跨连接共享非 `IsSharable` 状态 | 串话 | `AddLast` 时检测并抛错；文档给反例 |
| handler 忘记 `await` 下游 | 静默丢消息或乱序 | 端到端用例断言消息确实到达下游 + 代码评审；没有可靠的运行时自动检测（与出站"未转发即返回"同理，§8.1 第 12 条），不能只靠日志 |
| `PooledMessage` 泄漏 | 内存涨、池被吃干 | 引用计数 + 计数配平测试 + 链尾保底 `Release` |
| 队列满时暂停读取导致同连接所有流量停摆 | 慢消费者拖死快连接 | 队列容量与并发上限可配；暂停/恢复成对出现，测试覆盖 |
| `SAEA.Sockets` 钩子破坏既有读循环 | 老用户回归 | 钩子仅在显式配置时生效；**但双来源闸门是公共路径**，语义不变、代码有改动 → 普通/TLS/背压三条路径全量回归（§8.3） |
| 未认证流量打到反序列化器（解析型 DoS） | CPU/GC 被恶意帧吃满 | 鉴权限在 unwrap 之前，只看 `Envelope.Type` 白名单（§5.3.1）；帧长再受 `MaxFrameLength` 兜底 |
| 未知消息类型被静默丢弃 | 协议演进期"消息发出去了但没人处理" | `MessageDispatcher` 默认 `Warn` + 可选 `Fallback`，链尾无下游时升级 `Error`（§5.5） |
| 调用方在 `OnAccepted` 里做阻塞 IO | 该连接泵启动被推迟，首帧延迟 | README 明写禁止；`UseConnectionOpened` 把 `acceptedAt` 写进 `IChannel.Attributes`，`InboundLoggingHandler` 在首帧记录"accepted→首帧"间隔，异常时可见 |
| 强制 `UseStream()` 让老 `UseIocp` 用户迁移 | 接入成本 | 文档给一页迁移对照（`OnAccepted` 参数、token 类型、`GetStream` 用法）；未配钩子仍是纯 IOCP |
| 半包耗尽（slowloris）长期占用连接与缓冲 | 内存被恶意连接吃满 | `MaxBufferedBytes` 硬上限 + `IChannelGuard` 流量钩子（§6.2）；两者都不 fail-open |
| 出站 handler 违反契约导致重复发送 | 客户端收到"幽灵帧"，抓包难定位 | 三条硬规则 + `Debug` 断言（§5.1）+ 验收 12 的坏 handler 用例 |
| `DisconnectDrainPolicy.DrainInFlight` 下 handler 卡死 | 连接永远回收不了 | `DrainTimeout` 兜底（默认 5s，可设零但需显式承担） |
| C# 8 下 `Task` 版 handler 带来分配 | 吞吐下降 | 同步 handler 返回 `Task.CompletedTask`（零分配）；分帧在泵内同步完成，不额外产生 Task，只有业务 handler 自己的 `await` 才有状态机 |