# 上层包接入 ChannelPipeline 设计文档

**日期：** 2026-11-06

**范围：** 把依赖 `SAEA.Sockets` 的上层包，从「事件回调 + 硬编码解码」的接收路径，统一迁到 `SAEA.Sockets.Pipeline` 的 `ChannelPipeline` + `ChannelHandler` + 字节→消息解码器；删除这些包面向使用者的接收/生命周期事件，由使用者在每连接链上自行装配。

**状态：** 设计阶段。发送 API、FTP 与 P2P 的补充范围已确定，当前等待人工审查；代码侧只完成了 `WSSServerImpl` 合并删除（并入 `WSServerImpl`，TLS 由 `WithSsl` 承载），其余改造尚未开始，本文件经人工确认后才写实施计划。

---

## 一、动机

现状的接收路径有三个结构性问题，逐包重复：

1. **回调里必然拷一次。** `OnServerReceiveSpan` 的 `dataSpan` 只在回调期间有效，包内第一件事都是 `dataSpan.ToArray()`（`SAEA.FileSocket/Server.cs:121`、`SAEA.Http/Base/Net/HttpSocket.cs:106`；`SAEA.FTP/Net/ServerSocket.cs:115` 也这么写，但该类已无调用方，见 §八. B）。
2. **解码器状态挂在 socket 令牌上。** `IUserToken.Coder` 由 `BaseContext<TCoder>` 在构造时创建，生命周期与语义都不属于业务（`SAEA.Sockets/Base/BaseContext.cs:60`）。换协议就要换 `ICoder` 实现，分帧与序列化焊在一个类里。
3. **包的公开面绑死在传输细节上。** 使用者拿到的是 `IUserToken` 和事件；包自己无法控制分发顺序、背压、超时、并发，也没有连接生命周期钩子。

`SAEA.Sockets.Pipeline` 已经解决了这三件事，`IRawByteStream` / `IPipelineHost` 也已在库内；尚缺的是面向真实 socket 的包内实现。仓库里唯一完整的参考实现是 `Src/SAEA.Sockets.PipelineTest/SocketStreamAdapter.cs`（公开类，服务端与客户端两条发送委托）与同目录的 `PipelineHost.cs`，外加测试替身 `FakeRawByteStream`。它们是演示工程里的产物，迁移包不能直接引用，因此本次要补的是各包传输适配层，不是重复新增这两张接口。

---

## 二、目标 / 非目标 / 反目标

### 2.1 目标

- 除 §七选定的 P2P 例外外，每个迁移包对外只保留一种接收形态：**每连接 handler 工厂**，使用者在链上装自己的 handler；P2P 只在包内 `TCPChannel` 接链，对外继续暴露现有领域化传输事件。
- 分帧与解码可插拔：分帧用 `IFrameSplitter`，字节→消息用新的 `ByteToMessageDecoder`，包内只提供协议解码器，不再有 `ICoder`。
- 删除迁移包的接收事件与连接生命周期事件；P2P 仅删 `TCPChannel` 内部传输事件，`P2PClient` / `P2PServer` 的现有公开事件按 §七例外保留。连接标识从 `IUserToken` 收敛到 `IChannel`。
- 迁移后的包不再在接收路径上做整段 `ToArray()`；`SAEA.Http` 的 `_cache.AddRange + ToArray` 的 O(n²) 必须消失。
- 迁到 `UseStream()` + `UseInputHandler` 后，同一连接的读取只有一个读者，`GetStream()` 与链不再争抢。

### 2.2 非目标

- 不改协议、不改线格式、不改业务语义。
- 不迁 UDP：`SAEA.Sockets` 的 `OnServerReceiveSpan` / `OnClientReceiveSpan` 保留给 UDP，`SAEA.Audio.Net`、`SAEA.DNS` 的 UDP 服务端、`SAEA.P2P` 的 `UDPChannel` / `LocalDiscovery` 继续走事件。
- 不为迁移引入新的性能门槛；性能只做「不劣化」核对。
- 不新增业务功能（重连策略、限流、鉴权语义等一律不动）。

### 2.3 反目标

- **双路径并存**：迁移后包内还留着 span 事件作为「兼容入口」，等于没迁。
- **事件改名保留**：把 `OnMessage` 换个名字包一层再暴露，使用者仍然拿不到链。唯一例外是 §七选定的 P2P：保留 `P2PClient` / `P2PServer` 现有事件，不新增别名或第二套入口，也不要求外部使用者直接拿 `TCPChannel` 的链。
- **拷贝不减反增**：链上再 `ToArray()` 一次，或用池化切片喂给需要长期持有的业务对象。
- **使用者写不出等价实现**：删了事件却没给出协议解码器和连接状态入口，使用者只能自己重写协议。

---

## 三、硬约束

| 约束 | 结论 |
|---|---|
| 目标框架 | 库项目 `netstandard2.0` + `LangVersion 8.0`；禁用 `record` / `init` / file-scoped namespace / 目标类型 `new` |
| 包依赖方向 | `SAEA.Sockets.Pipeline` 不得引用 `SAEA.Sockets`。传输适配层必须放在两者之外 |
| 版本与依赖 | 不动任何 `.csproj` 的 `<Version>`，也不新增、删除、升级或降级 `PackageReference` |
| 变更性质 | 破坏性变更：删事件、换公开类型，必须同步 10 个迁移演示工程与 9×2 份迁移组件 README（8 个迁移包 + MVC）；P0 另同步 `PipelineTest` 参考工程，以及 `SAEA.Sockets` / `SAEA.Sockets.Pipeline` 各中英 README；P2P 例外保留的公开事件不计入删除项 |
| 现有测试 | `SAEA.FTP.Tests`（13）随 FTP 改造同步调整；`SAEA.MQTT.Tests`（153）不受本批影响（MQTT 不迁），改动后仍必须全绿，作为「没误伤相邻包」的回归证据 |
| 操作 | 不 `git push`、不 `reset --hard`、不改写已有提交 |

---

## 四、目标架构

每条 TCP 连接一份，自上而下：

```
业务 handler（使用者装）        ← 只认消息对象与 IChannelHandlerContext
协议解码器 ByteToMessageDecoder ← 包内提供：字节帧 → 消息对象
（可选中继 handler：鉴权/日志/限流）
───────────────────────────────── 链（ChannelPipeline，每连接独占）
ChannelPipelineExecutor          ← 队列、并发、超时、内联、收口
IRawByteStream ← [包内新增] 传输适配层实现
PipeReader / IReceiveFlowControl / SendAsync(owner) ← SAEA.Sockets 公开面
SaeaConnection（UseStream + TLS）
Socket
```

分帧不在链上，仍在执行器的泵里（见 §六）。链首收到的是**已切好的单帧**。

### 4.1 装配契约

包内负责传输接线，使用者只提供链配置；配置挂在已有的 `WebConfig` 上，不新增 `WebHostOptions`：

```csharp
var server = new WebHost(port: 8080);
server.WebConfig.ConfigurePipeline = (id, pipeline) =>
{
    pipeline.AddLast(new MyHttpHandler());
};
server.Start();
```

- `WebConfig.ConfigurePipeline` 是 `Action<string, IChannelPipeline>`，每条连接在 `Attach` 之后、`Start` 之前调用一次，`channelId` 为该连接标识。
- 包内先装好自己的协议解码器与协议处理器，再调用 `ConfigurePipeline`，最后追加链尾异常终结器，保证使用者拿到的链已经能产出消息对象。
- 工厂是**每连接**调用的，`IChannelPipeline` 不跨连接复用。
- 使用者在入站或生命周期 handler 中发起一条新的业务写，通过 `ctx.Pipeline.WriteAsync(msg)` 从出站链最右侧进入；出站 handler 加工当前写时只能调用 `ctx.WriteAsync(msg)` 逐节点转发。没有 `IChannel.WriteAsync`。禁止在出站 `WriteAsync` 回调中再通过 `ctx.Pipeline.WriteAsync` 发起并等待新业务写：新写会排在当前写之后，当前写等待它就会自死锁；独立新写必须在当前写完成后由业务侧发起，检测与别名边界见 §5.8。

**装配顺序是硬约束，必须写进每包装配代码与 README：**

- 出站 handler 必须构成链首连续前缀。`AddLast(出站)` 在链上已有入站节点后抛 `InvalidOperationException`（`ChannelPipeline.AddLast` 的出站前缀校验），`AddFirst(入站)` 在链首已是出站前缀时也抛。
- 因此顺序只能是：**先把所有出站 handler 加完（`AddLast` 或 `AddFirst` 均可），再加第一个入站 handler**。之后要补出站 handler 只能用 `AddFirst`（向左扩展前缀），要补入站 handler 只能用 `AddLast`。
- 包内装配固定为：出站（编码 → 帧编码 → **`OutboundSerializingHandler` 最后**，理由见 §5.9）→ 入站（解码器 → … ）→ `ConfigurePipeline` 给使用者加业务 handler → 链尾 `ExceptionTerminalHandler`。
- **`Start()` 之后链结构冻结**：`AddFirst` / `AddLast` / `Remove` 一律抛 `InvalidOperationException`。不冻结的话，§5.7 的链尾校验与串行器位置校验都只在启动时成立一次，之后仍可被改坏；§5.2 里「`ChannelActiveAsync` 中可以增删节点」的写法随之取消，连接级初始化只做登记，不动链。

### 4.2 连接生命周期

`IUserToken` 上的建连/断开不再以事件暴露，改为链回调：

- 建链：执行器启动泵之前派发 `ChannelActiveAsync`（链首→链尾）。
- 断开：收口开始时派发 `ChannelInactiveAsync`（链首→链尾），之后才按 `DrainPolicy` 处理在途消息。

包自己的连接登记表**只有一张**：`IPipelineHost` 的实现（§5.5），键是连接标识、值是 `IChannel`，登记在 `UseConnectionOpened` / wiring 里做，摘除在执行器收口时做。包内会话对象挂在 `IChannel` 的属性上，**不另建第二张 `IChannel ↔ 会话` 字典**。需要主动推送的包，由包内第一个 handler 在 `ChannelActiveAsync` / `ChannelInactiveAsync` 里把 ctx 存进同一个会话对象；只有不需要推送的包可以省掉这一步。

### 4.3 出站

出站沿用现有双向链：业务 `ctx.Pipeline.WriteAsync(消息对象)` → 最右侧串行器 → 编码 handler 通过 `ctx.WriteAsync` 逐节点转发 → `IRawByteStream.SendAsync(frame, owner)`。包内不再通过 `userToken.Coder.Encode` 直接写线格式。

HTTP 响应、FTP 应答这类「请求-响应」模型，由包内 handler 把待写响应挂到 connection state 上，业务只操作响应对象。

### 4.4 连接标识

包内公开 API 里出现 `IUserToken` 的位置换成 `IChannel`（如 `HttpContext.Channel`）。`IUserToken` 只留在包内传输接线代码里，不再出现在公开签名中。

### 4.5 服务端主动推送

「使用者自己装 handler」只覆盖了**入站**。服务端主动推送（`WebHost.Send(channelId, ...)`、`FTPServer` 应答、`MessageServer` 广播）仍然需要包提供入口，否则业务线程没有任何回包手段。

做法：复用 §4.2 的唯一登记表，按 id 取到会话对象再取其 ctx，`ctx.Pipeline.WriteAsync(msg)`，**不**绕过链直接 `IServerSocket.SendAsync`，也不把包级推送误当作当前出站 handler 的内部转发。ctx 由包内第一个 handler 在 `ChannelActiveAsync` 登记、在 `ChannelInactiveAsync` 摘除。

- 查不到 ctx 时返回失败或静默丢弃，由包决定并在文档写明。
- 同一连接的链写入必须串行：统一在出站前缀最右侧装 `OutboundSerializingHandler`（§5.9），不在各包内自己加锁。
- `IChannel` 本身不加 `WriteAsync`，避免出现「拿到 channel 就能绕过链」的第二条写路径。

### 4.6 资源上限

`ChannelPipelineOptions.MaxBufferedBytes` 默认 1MB，`ChannelPipeline.Create` 的校验分两档，文档与实现都按这两档说，不要混：

- **显式设置（非 0）**：只要求 `>= Splitter.MaxFrameLength + P_Head`。
- **自动推导（为 0）**：取 `MaxFrameLength + P_Head + ReadBufferSize`。

所以「`MaxFrameLength` 放大就必须把 `MaxBufferedBytes` 提高到 `MaxFrameLength + ReadBufferSize`」这句不准确：只有走自动推导时才会带上 `ReadBufferSize`，显式设置的合法下限是 `MaxFrameLength + P_Head`，`ReadBufferSize` 属于**建议预留**而非校验要求。另外 `P_Head` 是信封线格式的头长，通用 `LengthFieldSplitter` 的头长与长度修正值都可配，统一套 `P_Head` 对所有线格式并不充分；本次不改这个校验公式，只要求各包在 README 写清自己协议的头长是否超出 `P_Head`，超出就必须显式给足 `MaxBufferedBytes`。

- HTTP：新增 `MaxRequestSize` 配置项，默认 64MB，**只限制请求 body，不含请求行与头部**（现状是无上限，这里是有意的行为变更）；`Content-Length` 超限返回 413 并关闭连接。请求行与头部另受 64KB 上限约束，整帧分帧上限才是 `MaxRequestSize + 头部上限`。**body 上限必须在头部解析后立刻判定**，不能等 body 收完——否则 64MB 已经进内存，413 只是事后通知。明细见 §八. F。
- 其余包：`MaxFrameLength` 取现有 `ICoder` 的 `maxFrameLength` 语义，不放大。

**内存账要分项写，`MaxConnects × N` 不是总上界。** `MaxBufferedBytes = N` 只约束**单连接未成帧输入残余**。除此以外每条连接还可能有：`Pipe` 的瞬态段、已切出并排队的帧与在途帧、解码后的业务消息、出站排队项（由 §5.9 的 `MaxPendingWrites` 约束项数，单项大小由协议上限约束）、TLS 加密复制与 socket 发送缓冲、池化保留。只有每一项都有容量约束时才谈得上总最坏值；本设计只承诺「输入残余字节」与「出站待写项数」两项有界，其余在 README 单列，**不宣称存在固定总上界**。HTTP 的 64MB 只适合低并发场景，高并发下必须由使用方调低 `MaxRequestSize`。

---

## 五、`SAEA.Sockets.Pipeline` 需要新增的能力

九处缺口。5.2 / 5.8 扩展现有 handler 与 context 接口，5.8 另给 `IChannelPipeline` 增加业务写入口；5.7 / 5.9 新增可选项与处理器，其余为新增类型。Pipeline 侧不改任何既有成员的**签名**，但新增接口成员与既有行为变化必须单独记录：§4.1 在 `Start()` 后冻结链结构；§5.1 把普通入站异常从「通知后吞掉」改为「通知一次后继续冒泡」，并在未成功转发时按逐次调用所有权补偿；§5.3 用 `SplitterSwitchTimeout` 把漏切换从永久挂起改为超时断开；§5.8 统一关闭状态机、扩展 `DrainTimeout` 的出站等待口径（`DrainInFlight` 的零值语义不变），并让 `ctx.WriteAsync` 在出站回调外改为抛 `InvalidOperationException`；§八. F 把分帧异常从直接断开扩成可先上链收尾。另有已确定的 `SAEA.Sockets` 公开签名变更：§5.5 方案 A 把两个带 `owner` 的发送重载从 `void` 改为 `Task`，以暴露失败并等待非 TLS/TLS 的真实交接。

### 5.1 `ByteToMessageDecoder`

现状：泵切好的帧进链后，只有 `CodecDecoderHandler` 会把 `PooledMessage` 还原成 `Envelope`，没有「帧 → 任意业务消息」的公共基类。

```csharp
public abstract class ByteToMessageDecoder : InboundHandlerAdapter
{
    /// <summary>把一帧解成 0..N 条消息追加到 output；默认不得让消息引用帧内存。</summary>
    protected abstract void Decode(
        IChannelHandlerContext ctx,
        PooledMessage frame,
        List<object> output);
}
```

- **输入必须是 `PooledMessage`，不能只是 `ReadOnlySequence<byte>`。** 否则子类拿不到可 `Retain` 的对象，「把原始帧所有权移交给下游」根本写不出来；`PooledMessage` 的构造函数是 `internal`，迁移包也无法自己造一个。只读字节通过 `frame.Frame` 取。
- 签名保持同步，与 DotNetty 的 `Decode(..., List<object> output)` 一致：`Decode` 返回后基类按顺序逐条 `await ctx.FireChannelReadAsync(...)`。
- 不采用「异步 `Emit` + 同步等待」：那等于在 `Inline` 分发下用阻塞等待真实异步，死锁与顺序都无法保证。
- 一帧多消息用 `output` 追加（HTTP pipelining 之外的批量协议也适用）；零消息直接不追加。
- **所有权：基类不释放输入帧。** 队列条目那一份原始引用始终由执行器释放（`ExecuteMessageAsync` / `FinishInline` 的 `finally`），与现有 `FramePassthroughHandler` 的约定一致。基类若再 `Release` 一次，会把子类为 `output` 特意 `Retain` 出来的那份引用也吃掉（2 → 1 → 0），下游拿到的就是已归还池的缓冲。`PooledMessage.Release` 的幂等只覆盖「计数已归零后的重复调用」，不是「任意两次调用都无害」。
- 因此规则只有两条：默认输出自带内存（`frame.Frame.ToArray()` 或全新对象）；要把原始帧留给下游，就**每放一个进 `output` 调一次 `Retain()`**，由最终消费方释放。多条输出共享同一帧就要 Retain 多次。下游若要跨回调持有，必须自己再 `Retain`。
- `Decode` 自身抛出时，先把异常交给 `ctx.FireExceptionCaughtAsync` 通知一次，再照常派发已追加的消息；输入帧仍由执行器统一释放，不允许静默丢弃半帧。通知自身失败只记录，不替换原始 Decode 异常。
- **入站普通异常统一改为「通知一次后冒泡」（已选定），不再由 `FireChannelReadFromAsync` 通知后吞掉。** 每次入站派发携带内部 `InboundDispatch`：记录当前消息是否由链承担异常补偿、异常是否已通知。handler 自己抛出的普通异常从该节点的下游开始 `FireExceptionCaughtAsync`，随后原异常继续向调用方冒泡；上游节点看到已通知标记后不再重复通知，执行器也只兜底记录，不再次全链上报。`OperationCanceledException` 保持原语义，不做普通异常通知，但同样触发所有权补偿并向上冒泡。
- **异常补偿按逐次派发的所有权标记，不按 `PooledMessage` 类型一刀切。** 执行器派发原始帧时标记为「执行器持有」，链边界不因 handler 异常释放它，仍由执行器 `finally` 释放原始引用；decoder 每放一个输出开始派发时标记为「链持有」，即使输出是对同一 `PooledMessage` `Retain` 后的同一实例，也由链边界承担该项的异常补偿。handler 透传同一引用时保留标记，换成新对象时新消息按链持有处理、旧消息仍由替换它的 handler 按原契约释放。
- 每个入站调用边界记录当前消息是否已经成功交给下一节点。handler 在未成功转发时抛错或取消，且消息标记为链持有，则该边界对 `IDisposable` 调一次 `Dispose` / `Release`；已经调往下一节点后由下游边界负责，父节点不再补偿。正常返回且未转发仍表示该 handler 消费消息，通用边界不擅自释放，消费 handler 或链尾按既有契约处理。若链持有消息被转发时已无下一入站节点，终点记录 Debug 并释放它，避免缺少 terminal 时静默泄漏。
- **多输出的中途失败因此可由基类观察并清理余项。** 基类按 output 下标逐条 `await` 可冒泡的 `FireChannelReadAsync`；某条调用一旦开始，该条所有权即移交入站链，基类不再补偿。若该次调用同步抛错、异步失败或取消，基类停止继续派发，并逐个 `Release` / `Dispose` 尚未开始派发的余项；当前项由上述入站边界或消费方释放，基类不得再次释放。随后只向上传播一次异常。P0 用「两条共享同一输入帧、第一条在转发前抛错 / 转发后下游抛错 / 取消」分别验证输入与各 Retain 引用最终恰好归零，并验证普通异常只通知一次。
- **不负责累加**：半包由泵拦在 `Pipe` 里，基类拿到的永远是一整帧。这是与 Netty 同名类最大的差别，也是本设计的关键取舍（§六）。

### 5.2 处理器生命周期与活动通知

`IChannelHandler` 增加两个同步回调，`IChannelInboundHandler` 增加两个异步通知，适配器给默认实现（下面只列新增成员，既有的 `IsSharable`、`ChannelReadAsync`、`ChannelExceptionCaughtAsync`、`ChannelReadCompleteAsync` 不变）：

```csharp
public interface IChannelHandler
{
    bool IsSharable { get; }
    /// <summary>挂上链之前同步调用；用于登记 ctx、绑定连接级状态。抛出时本次 Add 失败，不挂链。</summary>
    void HandlerAdded(IChannelHandlerContext ctx);
    /// <summary>从链上摘除之后同步调用；用于释放连接级状态、注销登记。异常只记录，不阻断摘除。</summary>
    void HandlerRemoved(IChannelHandlerContext ctx);
}

public interface IChannelInboundHandler : IChannelHandler
{
    Task ChannelActiveAsync(IChannelHandlerContext ctx);
    Task ChannelInactiveAsync(IChannelHandlerContext ctx);
}
```

> 这是本次接口扩展的一组：`IChannelHandler` 与 `IChannelInboundHandler` 共增加 4 个成员；另两组是 §5.8 的 `IChannelHandlerContext.CloseAfterWriteAsync` 与 `IChannelPipeline.WriteAsync`。仓库内所有 handler 都继承适配器，不受影响；外部直接实现这些接口的代码会编译失败。C# 8 在 `netstandard2.0` 上不能用默认接口实现，无法规避。

**单次调用的状态放在调用绑定的 context 视图，不放在常驻节点字段，也不使用 `AsyncLocal`。** `DefaultChannelHandlerContext` 继续作为节点常驻上下文，仅供 `HandlerAdded` / `HandlerRemoved` 等连接级回调保存；每次入站或出站 handler 调用则从节点租用一个轻量视图（每节点一个单槽，槽被并发占用时新建，不做无界池），视图绑定当前节点及本次 `InboundDispatch` / 出站操作，并沿下游调用传递同一操作对象。这样并发写不会互相覆盖操作号、额度或异常标记；回调结束后视图失效并归还，跨回调保存的旧引用要么已失效抛 `InvalidOperationException`，要么已被下一次调用复用而静默作用到那次调用，不能继续转发消息。视图只委托连接属性与生命周期能力，不改变公开接口签名。

执行器需要新增派发点：

- `Start()` 成功后、泵首次读取前派发 `ChannelActiveAsync`（链首→链尾，逐条等待）。此时流已 `Attach`、执行器未进入收口，**允许回包**（欢迎语、握手响应）。
- 进入收口时、`DrainPolicy` 生效前派发 `ChannelInactiveAsync`；此时 `IChannel.IsActive` 已为 false，**该回调只做通知与登记摘除，禁止回包**（收尾帧必须在最后一条消息的处理器里写完再 `CloseAsync`）。DotNetty 也不是「`channelInactive` 里还能有效写」：其 close 会先使 outbound buffer 失效并失败、释放待写项，再触发 inactive，后续写返回 `ClosedChannelException` faulted Task。SAEA 此处额外把禁止回包作为明确装配契约，依据见同目录 DotNetty 对照文档 §二。
- 两者都必须幂等；handler 抛异常只记录，不阻断收口。

`HandlerAdded` / `HandlerRemoved` 在 `ChannelPipeline.AddFirst/AddLast/Remove/Dispose` 内同步调用，用于登记与释放**连接级**状态（ctx 登记表、解析器实例）。

**不要借它做协议升级换链。** 入站是嵌套 `await`：下游 handler 里 `Remove` 上游 handler 时，上游的 `ChannelReadAsync` 仍在栈上。`HandlerRemoved` 一旦释放当前消息相关缓冲，就会伤到正在执行的消息。因此：`HandlerRemoved` 只能释放连接级状态；升级用 §八. D 的「解码器内部切阶段」，不增删链节点。链上增删只允许发生在 `Start()` 之前（§4.1 启动后冻结）；启动前 `Remove` 会同步触发 `HandlerRemoved`，启动后则在 `Dispose` 收口时触发。

**调用顺序是硬要求：先建节点与 ctx、调 `HandlerAdded(ctx)`，成功后再挂链。** 现有 `AddFirst` / `AddLast` 的 `catch` 只做 `HandlerRegistrationTable.Release(handler)`，节点一旦链上就不会回滚；若先挂链再调 `HandlerAdded` 且它抛异常，链上会留一个半挂节点（已连、未登记）。`HandlerRemoved` 相反：先 `Unlink` 再调 `HandlerRemoved(ctx)`，保证它抛异常也不影响链结构，只记录日志。

**代价：`HandlerAdded` 拿到的 ctx 是半初始化的，必须限制它能做的事。** 节点尚未链接时 `_node.Next` / `_node.Prev` 都是 null，`FireChannelReadAsync` 会静默找不到下游，`WriteAsync` 会越过整个出站前缀直达链头；若此时还没 `Attach`，读 `ctx.Channel` 会抛、`ChannelId` 为 null。所以：

- `HandlerAdded` **只允许保存 ctx、创建连接级状态**；禁止传播消息、禁止写出、禁止读 `Channel`。需要 `Channel` / 回包的初始化一律放到 `ChannelActiveAsync`。
- 装配顺序固定为 `ChannelPipeline.Create` → `executor.Attach` → 包内装配 + `ConfigurePipeline` → `Start()`。这与 `PipelineTest` 现有的「先 BuildPipeline 后 Attach」不同，P0 要同步改演示。

**保存连接级状态的 handler 必须 `IsSharable = false`。** 可共享实例会在多条链上各收到一次 `HandlerAdded(ctx)`，把 ctx 存进单个字段就会跨连接互相覆盖，`HandlerRegistrationTable` 不会拦（实例自己声明了可共享）。可共享 handler 的回调只能用参数，不能落字段。

**执行器要补状态门，否则会出现「先 Inactive 后 Active」。** 构造函数里注册的 `transportToken` 若已取消会立刻进入收口，而此时 `_pipeline` 可能还没 `Attach`、泵还没启动；现有 `Attach` / `Start` 都不看 `_closing`。P0 增加 Created / Attached / Started / Closing / Completed 状态：进入 Closing 后 `Attach` / `Start` 直接抛；启动与关闭竞态下保证 `ChannelActiveAsync` 不会晚于 `ChannelInactiveAsync`，且已建资源统一走同一条收口路径释放。

**Inactive 前不能先清连接属性。** 当前 `BeginShutdown` 先调用 `DefaultChannel.MarkInactive()`，而后者立即 `_attributes.Clear()`；这与 §4.2 允许 `ChannelInactiveAsync` 读取会话状态的约定冲突。P0 改为只在进入收口时原子翻转 `IsActive`，属性保持到所有 `ChannelInactiveAsync` 与 host 摘除完成之后，再由唯一清理点释放；关闭竞态下也只能清理一次。

### 5.3 可切换分帧器（含切换屏障）

HTTP→WebSocket 需要在同一条连接上换分帧方式。这里有一个必须先解决的时序问题：

- 泵在**一次** `ReadAsync` 之后，用一个内层循环把整段缓冲连续切帧入队（`ChannelPipelineExecutor.PumpAsync` 第 4 步）。
- 默认 `DispatchMode = Queued`，升级请求的 handler 是**异步**执行的；等它决定切换时，泵很可能已经把同一段里紧跟其后的 WebSocket 首帧按 HTTP 规则切完并入了队。

所以「handler 调一下 `SwitchTo` 就完事」不成立，必须有屏障：

```csharp
public sealed class SwitchableSplitter : IFrameSplitter
{
    public IFrameSplitter Current { get; }

    /// <summary>
    /// 本帧已产出后停止继续切帧，直到 <see cref="SwitchTo"/> 被调用。
    /// 由分帧器在识别到「本协议最后一帧」时设置。
    /// </summary>
    public bool StopAfterCurrent { get; set; }

    /// <summary>切换分帧器并解除屏障；残余字节原样交给新分帧器。</summary>
    public void SwitchTo(IFrameSplitter splitter);
}
```

执行器配合（P0 中执行器改动最集中的一处；其余执行器改动见 §5.2 的状态门、§5.7 的启动校验、§5.8 的关闭状态机与 §八. F 的协议错误上链）：

1. 每切一帧后检查 `StopAfterCurrent`；为真则**立即停止本次内层切帧循环**，把 `consumed` 之后的字节留在 `Pipe` 里，不 `AdvanceTo` 到缓冲末尾。
2. 在**同一份 `ReadResult` 与同一 `consumed` 位置**上等待 `SwitchTo` 被调用（`TaskCompletionSource` + 连接级取消 + 新增 `SplitterSwitchTimeout`，默认 10 秒）；超时或连接关闭则按 `FrameProtocolException` 收口。等待期间不得 `AdvanceTo(consumed, buffer.End)`：那会把升级后的残帧标为已检查，没有新字节时后续 `ReadAsync` 可能不再返回它；也不得先 `AdvanceTo` 再丢掉该 `ReadResult` 另等读取。切换完成后继续对原缓冲的 `consumed` 之后部分切帧，全部处理完再一次性 `AdvanceTo`。
3. 已入队的旧协议消息仍然执行——它们本来就是旧协议的合法消息。
4. 泵在 `PumpAsync` 开头把 `_options.Splitter` 取成了局部变量，所以 `SwitchTo` 必须作用在**同一个 `SwitchableSplitter` 实例**上：包的 `options.Splitter` 直接放这个实例，不要再包一层转发器，否则切换对泵不可见。

**切换后缓冲上限不会自己跟着变，必须提前钉死。** `ChannelPipeline.Create` 只用初始 `Splitter.MaxFrameLength` 算一次 `MaxBufferedBytes` 下限，`_effectiveMaxBufferedBytes` 是只读快照；泵虽然持有同一个 `SwitchableSplitter`，`SwitchTo` 不会重算它。若新分帧器允许更大的帧，旧上限会在新帧凑齐前以 `buffered-bytes-limit` 断开合法连接。取**固定上限**方案：`SwitchableSplitter.MaxFrameLength` 在创建时返回所有可切换协议的最大值，`SwitchTo` 拒绝超过该值的新分帧器；不做「切换时原子改上限」，那会把背压闸门变成运行期可变，内存账算不清。HTTP→WS 即 `max(HTTP 请求上限, WS 单帧上限)`。

**决定不升级时也必须解除屏障**：调用 `SwitchTo(Current)` 或等价方法把 `StopAfterCurrent` 复位。漏掉这一步，泵会一直等到 `SplitterSwitchTimeout` 后按协议错误断开。

**`Inline` 分发要单独对待。** 泵的内层循环里有一条内联快路径（`ChannelPipelineExecutor` 第 4 步的 `CanDispatchInline` / `TryStartInline`）：handler 同步跑完时，切帧循环根本没有离开过接收线程，handler 里同步调用 `SwitchTo` 就能立刻生效，屏障形同虚设；一旦 handler `await` 挂起，在途数变非零，后续帧才回落到队列。因此屏障逻辑必须同时覆盖两条路径：

- 内联分支每次返回后重新读一次 `StopAfterCurrent`，为真就停切；
- 排队分支按第 1、2 条停切并等待。

只实现排队分支会在 `DispatchMode = Inline` 下退化成「偶尔错位」，比稳定失败更难查。

分帧器责任：只在协议边界设置 `StopAfterCurrent`（HTTP 升级请求的 body 已读完且其后字节归属新协议）。做不到就抛 `FrameProtocolException` 断开，不猜。

> **每连接一个实例。** `ChannelPipeline.Create` 按引用持有 `Splitter`，而 `Splitter` 又挂在 `ChannelPipelineOptions` 上，所以 options 也必须每连接新建，不能跨连接复用。`LineSplitter` / `DelimiterSplitter` / `LengthFieldSplitter` 其实**无可累积状态**（`Reset` 为空），复用它们不会出错；真正必须每连接新建的是 `SwitchableSplitter`、HTTP 专用分帧器这类保存连接状态或可变配置的类型。统一按「一律每连接新建」写进装配代码与 README，是为了不给使用方留判断题，不是因为每个分帧器都有状态。

### 5.4 无边界分帧器

FTP 数据通道、Socket5 隧道这类协议没有帧边界，语义是「读多少算多少」：

```csharp
public sealed class TakeAvailableSplitter : IFrameSplitter
{
    /// <summary>把当前可用缓冲整体作为一帧返回；空缓冲返回 false。</summary>
}
```

`MaxFrameLength` 对该分帧器取「单帧硬上界」：可用缓冲超过该值时按上限切块，剩余字节留给下一次 `TrySplit`，**不抛异常**。原因是泵被下游背压拖慢时，`Pipe` 会把多次读取合并成一段交出，一次 `ReadResult` 常常是 `ReadBufferSize` 的数倍；把这种合并缓冲当成「超限」会让合法的透传流量整条断开。使用它意味着消息边界通常等于读边界，只有超限时才切成多帧，文档必须写清楚。

**与 `MaxBufferedBytes` 校验的关系：不豁免，撤回「可以独立给低值」的说法。** `ChannelPipeline.Create` 对显式设置的 `MaxBufferedBytes` 仍强制 `>= Splitter.MaxFrameLength + P_Head`，即使无边界分帧器根本不累积残帧也一样。本设计**不**为此给 `IFrameSplitter` 再加「是否累积半帧」的能力标记——那只是为一个包省一点缓冲，却要让所有分帧器多实现一个成员。取值规则改为：`MaxFrameLength` 填调用方真正能接受的最大单块（数据通道即块大小上限，通常 64KB 量级），`MaxBufferedBytes` 按同一量级给，天然满足公式；不要把「单帧上限」和「累积缓冲上限」当成可以各自独立取小值的两个旋钮。

### 5.5 传输适配层（每包自带一份）

`IRawByteStream` + `IPipelineHost` 的实现需要同时看到 `SAEA.Sockets` 和 `SAEA.Sockets.Pipeline`，而两者不能互相引用。已定：**不新增共享包**，每个迁移包自带一份适配层。

**参考实现已经存在，但必须先修正语义再移植，不能原样冻结：**

| 文件 | 现成内容 |
|---|---|
| `Src/SAEA.Sockets.PipelineTest/SocketStreamAdapter.cs` | `IRawByteStream`：服务端构造（`IServerSocket.SendAsync(id, data, owner)`）与客户端构造（发送委托），当前只把**同步**异常翻译成 `ChannelClosedException`，`Pause/Resume/CloseAsync` 转发给 `IReceiveFlowControl` |
| `Src/SAEA.Sockets.PipelineTest/PipelineHost.cs` | `IPipelineHost`：`ConcurrentDictionary<string, IChannel>`，`TryRemoveChannel` 里释放通道属性 |

各包按下面的结构落副本：

```
<包>/Net/PipelineRawByteStream.cs   : IRawByteStream   （PipeReader + IReceiveFlowControl + SendAsync(owner)）
<包>/Net/PipelineHostRegistry.cs    : IPipelineHost    （ConcurrentDictionary<string, IChannel> + 收口摘除）
<包>/Net/PipelineWiring.cs          （UseInputHandler 回调体：建 Channel → 建执行器 → Start → 等 Completion → 清理）
```

**wiring 必须整体包 `try/catch/finally`，不能只写「Start → 等 Completion → 清理」的顺路径。** 输入处理器由后台 `Task.Run` 调用，异常在服务端只触发 `OnError`、客户端只 `RaiseError`，都不会自动 `Disconnect`。建链、`Attach`、`ConfigurePipeline`、`HandlerAdded`、§5.7 的启动校验、`Start` 任一步失败，都会跳过等待与清理，留下已连接的 socket 和已登记的 channel。规则：未成功启动时直接 `flow.CloseAsync` 并摘除 host；执行器已创建则走它的收口；清理幂等，且不假设 `Completion` 一定已启动或已完成。

- 只用 `SAEA.Sockets` 的公开面：`UseInputHandler` 提供的 `PipeReader` / `IReceiveFlowControl` / `IUserToken`，加 §5.5 选定的可等待、可观测失败的发送 API。
- `UseInputHandler` 必须与 `UseConnectionOpened` 成对配置（`SocketOptionBuilder` 在缺少 `UseConnectionOpened` 时同步抛 `InvalidOperationException`），所以每个包的 wiring 要同时接这两条，并在 `UseConnectionOpened` 里登记发送所需的 `IUserToken` / 连接标识。
- `SaeaConnection` 是 `internal`，包内适配层不得直接访问；但 §5.5 的 TLS 可等待发送改造必须在 `SAEA.Sockets` 内部修改它，不能把「不公开」误写成「不修改」。
- **发送失败的可观测边界（这条推翻了「原样冻结参考实现」的打算）：** `IServerSocket.SendAsync(id, ...)` / `IClientSocket` 同名重载当前返回 `void`，且底层大量失败**不抛异常**：服务端发送超时、socket 已断开只触发 `OnError` 后返回；客户端 `_userToken` 为空或未连接直接返回；TLS 未就绪同样只上报。此时 `SocketStreamAdapter.SendAsync` 返回已完成 Task，`CloseAfterWriteAsync` 会把这次写当成「已交接」，实际数据已丢。发送前自查也堵不住，因为检查与发送之间存在竞态。
- **方案 A（已选定）：把两个带 `IDisposable owner` 的重载返回类型从 `void` 改为 `Task`，不再另发一套平行 API。** 会话不存在、未连接、`WaitWrite` 超时、TLS 未就绪都以同步异常或 faulted Task 暴露；`owner` 在成功、同步异常及能够确认 socket 操作已退出的异步失败/取消路径上恰好释放一次。非 TLS 至少等待该次 socket 发送完成/写闸门释放；TLS 则必须等本次应用数据对应的全部 TLS 记录都完成底层 socket 交接，不能只等加密或把记录交给 `FlushOutput` 就返回。适配层直接 `await` 这个 Task，其余重载与事件行为不动。唯一的回收例外是下条所述「无法证明内核已退出」的超时路径：为防止池化缓冲覆写，允许放弃回收，不能宣称此时仍恰好释放一次。
- **每次发送要有独立完成源，不能只给现有 `IUserToken` 再加一个共享 Task。** `SendingOwner` / `IsSending` 按连接复用，完成回调、异常、同步完成和超时必须共同完成同一发送操作一次；失败进入 `SocketError` / 断开路径时也要完成对应 Task，不能只触发 `OnError`。当前 `FlushOutput` 的 `_rawSend` 是 `Action`、后台写是 fire-and-forget，P0 要一并改成可等待链，保留单连接单发送者与记录 FIFO。
- **超时不能立即归还 owner。** `SaeaServerSocket.AbandonSendingOwner` 目前故意只摘除不释放，因为内核可能仍读取该缓冲；直接 `Dispose` 会让池化数组被复用覆写。P0 的发送超时必须先关闭该 socket 以取消在途操作，再等 `SocketAsyncEventArgs.Completed` 确认操作退出后，由唯一完成路径归还 owner；超时 Task 可报告失败，但不能以「超时」为由提前回收缓冲。若实现无法证明完成回调已到，则保持放弃回收而不是冒险归还，并在测试中覆盖「超时后迟完成不得释放新一次发送的 owner」。
- 方案 A 是一处公开签名变更：普通调用方写 `socket.SendAsync(data, owner);` 在 C# 中仍可编译，但不 `await` 就观察不到异步失败；外部自定义实现 `IServerSocket` / `IClientSocket` 的代码必须改签名。它与「只改行为、不改签名」不是一回事，必须写进 `SAEA.Sockets` 破坏性变更说明；`RedisSocket` 等未迁移包列入回归。两个接口还各有一个 UDP 实现，四个实现都要同步签名并编译回归；本批只要求 TCP 新增长完成语义，UDP 的接收与分帧仍保持现状。
- **未采用的方案 B：保留现有 `void` 重载，新增 `Task SendOwnedAsync(...)`。** 它虽能保持既有调用方的运行行为，但接口仍需扩成员、外部实现同样受影响，而且会长期保留一条静默失败的旧发送路径，因此不进入实施。方案 A 的硬要求是 owner 不得重复释放，且在成功与所有可确认操作退出的异常路径上恰好释放一次；唯独无法确认内核退出的超时路径按安全优先放弃回收。
- **为什么不能只靠链首串行器解决 TLS：** 现状 `SaeaConnection.SendApplicationData` 只是 `Task.Run(WriteAsync)` 就返回并释放 owner，适配层若仍拿到已完成 Task，串行器会立刻放行下一帧，多个后台写抢 `_tlsLock` 的顺序不等于业务顺序；写计数也会提前归零，`CloseAfterWriteAsync` 可能关掉仍在等 `_tlsLock` 的连接。`SaeaServerSocket.End` 已经为同类问题专门等过 `connection.WriteAsync`，说明普通 TLS 投递返回不是安全关闭点。
- 在可等待发送落地之前，§5.9 的逐写失败语义只能收窄为：**只隔离链路与同步投递中可观察的异常；连接级失败让当前及后续写统一失败**，不承诺把每次异步发送失败精确归因到某个调用方。
- `SocketStreamAdapter.SendAsync` 必须真正 `await` 新发送 Task，并在 `try/catch` 中同时翻译同步异常与异步 fault；现有直接 `return _send(...)` 只会翻译前者。owner 已随调用移交底层，adapter 在异常时只翻译、不再次 `Dispose`。
- `IRawByteStream.SendAsync` 的 Task 只表示「本次发送已按上述口径交接」，不表示对端收到；README 不能写成「对端接收完成」。
- 现有两份实现是 `net10.0` 演示工程代码，抄进库项目要降语法、补文件头 banner、把 `IsSharable` 一类的演示假设去掉。
- **代价，已知并接受**：约 200 行代码 × 8 个包，任何一处修 bug 都要同步改所有副本。降低漂移靠两条：P0 把这两份实现补上单测并冻结语义；各包副本只改命名空间与前缀，并在 README 记录副本清单。

### 5.6 原始字节出站

现有出站链只认信封：`CodecEncoderHandler` 强制 `msg as Envelope`，`FrameEncoderHandler` 只接受 `internal` 的 `EncodedBody`，链头只接受 `internal` 的 `TransportFrame`。HTTP 响应、FTP 应答、WS 帧都不该为了过链去伪造一个 `Envelope`。

新增一个公开的最终形态：

```csharp
/// <summary>已经编码完成的原始帧，链上不再加工，直达传输层。</summary>
public sealed class RawFrame : IDisposable
{
    public ReadOnlyMemory<byte> Bytes { get; }
    /// <summary>随帧移交的所有权对象；发送完成或失败后由传输层释放，可为 null。</summary>
    public IDisposable Owner { get; }
    public void Dispose();
}
```

- 所有权用**原子移交标记**表达，不靠「编码 handler 在 catch 里一律 `Dispose`」：`RawFrame` 内部持有一个 `int` 移交标志，链头在调 `IRawByteStream.SendAsync` **之前** `Interlocked.CompareExchange` 置位；尚未置位时的 `Dispose` 负责且只负责释放一次 `Owner`，置位后 `Dispose` 不再碰 `Owner`。传输层一旦接管，无论成功还是异常都只由它释放 `Owner` 一次，§5.5 所述无法确认内核退出的超时路径除外。这条是必须的：`SaeaServerSocket.SendAsync(id, data, owner)` 在会话不存在时**先 `owner.Dispose()` 再抛异常**，`Owner` 是任意 `IDisposable`，不能假定幂等，否则池化缓冲会被重复归还。P0 必须带「会话不存在」「适配器同步抛错」两条所有权用例。
- **业务入口与每个出站转发边界共同保证未移交补偿，不能只靠编码器。** `ChannelPipeline.WriteAsync` 在首节点接管前校验或计数失败时，若输入是 `RawFrame` 就 `Dispose`。出站调用视图每次调用 handler 时记录本次是否已通过 `ctx.WriteAsync` 移交当前消息：正常返回且未移交表示该 handler 消费消息，`RawFrame` 在此释放；抛同步异常或返回 faulted/canceled Task 且未成功移交时，也在这里补偿释放。成功移交后父节点不再释放，由子节点或传输层负责；下游调用已开始但失败时由该下游边界补偿，父节点不重复处理。自定义 handler 与内置 handler 走同一边界，因此任意位置吞掉或抛错都不会泄漏，也不会双释放。若 handler 用另一个对象替换当前消息，旧对象仍按现有所有权契约由该 handler 自行释放，不能把「已转发新对象」误当作「旧对象也已释放」。通用出站边界只对 `RawFrame` 提供上述自动补偿；普通业务 `IDisposable` 被 handler 正常吞写或替换时仍由该 handler / 创建方按契约回收。
- **同一 `RawFrame` 在同一出站节点、同一操作中最多转发一次，跨节点透传仍然合法。** 重复转发直接抛 `InvalidOperationException`，不得再次发送，也不得再次释放已经移交下游或传输层的 `Owner`；该运行时检查属于 Release 所有权保证，不能只用现有 Debug 单写标记代替。
- 链头 sink 接受 `TransportFrame` 或 `RawFrame`，后者直接调 `IRawByteStream.SendAsync(Bytes, Owner)`。**链头对成功 CAS 之前的所有退出路径负责**：关闭、未 Attach、类型错误或其他前置校验导致同步异常 / faulted Task 时，若输入是 `RawFrame` 必须先 `Dispose` 再让失败冒泡；CAS 成功后才调用传输层，此后只能由传输层释放。P0 补「已到最后一个 handler，但链头因关闭或未 Attach 同步 / 异步拒绝」用例，堵住父节点已移交而传输尚未接管的所有权空窗。
- **`RawFrame` 路径与信封路径互斥，不能只让 `FrameEncoderHandler` 透传。** `CodecEncoderHandler` 对非 `Envelope` 直接抛，只要链上还留着信封编码节点，`RawFrame` 就到不了链头。迁移包的出站链一律**不装** `CodecEncoderHandler`；两种编码模型不出现在同一条链上。P0 加两组装配测试：含全部标准节点的既有信封链保持原行为；不含 `CodecEncoderHandler` 的迁移包装配让 `RawFrame` 直达链头，而不是只测 `FrameEncoderHandler` 透传。
- 各包自己的编码 handler 把业务对象编码成 `RawFrame` 后 `ctx.WriteAsync`；不再经过 `ICodec` / `Envelope`。信封模型保留给使用 `JsonMessageCodec` / `ProtobufMessageCodec` 的既有场景，不删。

### 5.7 链尾兜底缺失即拒绝启动

现状：`ChannelPipeline.Create` 不自动追加 `ExceptionTerminalHandler`，`FireChannelReadFromAsync` 走到链尾找不到入站节点就直接 `return`，普通异常也只是尝试通知后吞掉。后果是缺少统一的未消费诊断与异常收口策略，排障只剩「明明发了，handler 没收到」。

先把边界说清，避免误判：

- **池化原始帧不会泄漏。** `ChannelPipelineExecutor.ExecuteMessageAsync` 的 `finally` 无条件 `msg.Release()`，队列条目那一份原始引用由执行器负责，与链尾是否有人消费无关。
- §5.1 新边界会对「链持有」输出在无下一节点或异常时补记录、补释放，因此 terminal 不再是唯一的资源保险；它仍提供统一的未消费日志、异常分类与 `handler-exception` 关闭策略。正常吞掉消息却不释放的自定义 handler 仍是所有权违约，不能靠 terminal 补救。

新增校验字段：

```csharp
public bool RequireTerminalHandler { get; set; }   // 默认 false，保持既有行为
```

- 为 true 时，`ChannelPipelineExecutor.Start()` 在派发 `ChannelActiveAsync` 之前检查链尾最后一个入站节点是否为 `ExceptionTerminalHandler`，不是就抛 `InvalidOperationException` 并直接收口。同一次校验还要确认 `OutboundSerializingHandler` **恰好一个**且位于出站前缀最右侧（§5.9），缺失、重复、错位同样拒绝启动——现有前缀校验只能区分入站/出站，发现不了这三类装配错误。
- 不变量靠 §4.1 的「`Start()` 后冻结链结构」维持，否则启动后仍能移除兜底、在兜底后追加入站节点，校验就只在启动那一瞬间成立。
- 8 个迁移包的装配代码统一设为 true；既有测试与演示不设，默认行为不变。
- 选 `Start()` 而不是 `Create()`：链在 `Create()` 之后才由使用者的 `ConfigurePipeline` 装配完毕，创建时检查必然误报。

### 5.8 收口写窗口

现状：`BeginShutdown` 一置 `_closing = 1`，`HeadSinkWriteAsync` 立刻抛 `ChannelClosedException`；`DrainInFlight` 只等**在途消息**，不管出站写有没有落地。于是出现两个死角：

- handler 里「先写最后一条响应，再 `CloseAsync`」很容易写成 `CloseAsync` 先到，响应被拒；
- `ChannelInactiveAsync` 里想回一个收尾帧，必然失败（§5.2 已明确禁止，但那是靠约定，不是靠机制）。

新增一个出站感知的关闭入口：

```csharp
public interface IChannelHandlerContext
{
    /// <summary>等本连接已提交的出站写全部交接给传输层后再收口；连接已关闭时为 no-op。</summary>
    Task CloseAfterWriteAsync(string reason);
}
```

- **出站业务入口与 handler 转发是不同公开职责（已选定）。** 新增 `IChannelPipeline.WriteAsync(object msg)` 作为唯一业务入口：业务代码通过 `ctx.Pipeline.WriteAsync(msg)` 从最右侧出站节点启动发送；它只负责领取一次操作号、额度与写计数，不直接绕过链。`IChannelHandlerContext.WriteAsync` 专职向当前节点的上一个出站节点转发，通过 §5.2 的调用绑定视图沿用当前操作，既不重新计数、占额，也不因 draining 拒绝已进入链的旧写；在出站回调外无有效操作时调用该转发 API 必须抛出 `InvalidOperationException`，不得悄悄当作新业务写。外部自定义 handler 与内置编码器使用同一公开转发路径，不需要 `AsyncLocal`。`IChannel` 不另增绕链的 `WriteAsync`，`IRawByteStream.SendAsync` 继续保持原始发送语义，不是业务写入口。
- **每次业务写都有独立 `OutboundOperation` 与公共完成源。** 操作状态为 `Queued → HandedOff → Completed`，其中「已出队但尚未调下游」仍属 `Queued` 并带 active 标记；提前失败或放弃可从 `Queued` 直接到 `Completed`。串行器在同一同步边界内检查状态并把 active 项标成 `HandedOff`，随后才调用下游。调用方拿到的是公共 `TaskCompletionSource.Task`，不是某个任意自定义 handler 的 Task；真实链 Task 的正常完成、失败或取消只通过操作的唯一 `TryComplete` 路径驱动公共 Task。
- `ChannelPipeline` 维护未完成出站写计数：仅在上述**业务入口** +1，在 `OutboundOperation.TryComplete` 首次成功时 -1，计数归零时唤醒等待者。**计数点必须放在入口而不是链头**，否则还排在 `OutboundSerializingHandler` 队列里、尚未到链头的写不会被算作「已提交」。同一业务写只领取一个操作号与一份额度，任一后续出站节点沿用该操作。
- 计数必须 **exactly-once**：业务入口 +1 后用 `try/catch` 兜住同步异常并调用 `TryComplete`，真实链 Task 的成功 / 失败 / 取消以及关闭超时、串行器移除都竞争同一个原子完成路径。公共 Task 用 `TrySetResult` / `TrySetException` / `TrySetCanceled` 只完成一次；迟到的真实链完成只让当前所有权边界清理资源，不再次完成公共 Task、归还额度或减计数。
- **关闭要走单一状态机 `Open → DrainingWrites → Closing`，普通 `CloseAsync` 与 `CloseAfterWriteAsync` 共用它。** 「停止接纳新写」与「快照并等待已提交写」必须在同一个同步边界内完成，否则计数归零到 `BeginShutdown` 之间还能插进新写；等待期间另一线程调普通 `CloseAsync` 也不能抢先置 `_closing`，否则已排队的写会以 `ChannelClosedException` 告终，语义被破坏。普通关闭在 `Open` 时直接进入 `Closing`，在 `DrainingWrites` 时加入现有等待而不是取消它；重复 `CloseAfterWriteAsync` 复用同一个完成源，进入 `Closing` 后两者均为 no-op。
- `CloseAfterWriteAsync` 先 `await` 计数归零，再推进到 `Closing`。等待上限复用 `DrainTimeout`，但**零值不能沿用**：`WaitForInFlightAsync` 把 `TimeSpan.Zero` 定义为无限等待，直接复用会让一个卡住的写永久挂住收口。规则改为：该入口下 `DrainTimeout <= 0` 一律取 5 秒默认值，超时强制收口，并对待完成操作的**公共 Task** 调 `TryComplete` 使其失败。对 `Queued`（含 active-not-handed）项同时禁止后续移交并按 §5.6 补偿 `RawFrame`；对 `HandedOff` 项不伪装强制完成真实 handler Task、不释放其消息，由下游与迟完成回调继续清理。`DrainInFlight` 的零值语义保持不变，选项 XML 把两种口径分别写清。
- **禁止出站回调内的两种重入。** 当前出站 handler 不得在自己的 `WriteAsync` 栈里调用 `CloseAfterWriteAsync`，否则当前写会自等；也不得通过本次 `ctx.Pipeline.WriteAsync` 发起并等待另一笔独立业务写，否则新写排在当前写之后形成串行器自死锁。出站调用视图的 `Pipeline` 属性返回整条链共用的守卫视图（业务写入口恒抛，不按操作区分），这两类直接重入均同步抛 `InvalidOperationException`；当前写加工只能 `ctx.WriteAsync`，独立新写必须在当前写完成后由业务侧发起。通过外部保存的 pipeline / 常驻 ctx 别名绕过守卫视图同样属于装配违约，无 `AsyncLocal` 方案不承诺识别所有调用别名，但不得把这种别名当成支持的写法。P0 覆盖两种重入及串行器不自死锁用例。
- 语义边界写清楚：只保证「已提交给传输层」，不保证对端已收到；要等对端确认由协议层自己做（FTP `226`、WS close 握手）。「已提交」的完成点依赖 §5.5 的可等待发送 Task：非 TLS 要等 socket 发送完成/写闸门释放，TLS 要等本次应用数据的全部 TLS 记录完成底层发送，不能只等加密与 `FlushOutput` 入队，否则计数归零不代表最后一帧已安全交接。
- `ChannelInactiveAsync` 里调用它不生效（已进 `Closing`），这是有意的：收尾帧必须在最后一条消息的处理器里写完。
- 计数口径改为：**凡被业务写入口接受的写都计入一次，与调用方是否 `await` 返回的 Task 无关**；尚未调到入口、或在入口计数之前就失败的写不计入，出站链内部转发不新增计数。关闭请求之后新发起的业务写一律拒绝，已接受写的内部转发不在此列。

> 除 §5.2 的 4 个 handler 生命周期成员外，本节还扩展两个接口：`IChannelHandlerContext` 增加 `CloseAfterWriteAsync`，`IChannelPipeline` 增加 `WriteAsync`。仓库内分别只有 `DefaultChannelHandlerContext` 与 `ChannelPipeline` 实现；外部自定义实现会编译失败，三组新增成员都记录进包 README 的破坏性变更节。

### 5.9 出站写入串行化

`IChannelPipeline.WriteAsync` 允许多线程并发发起业务写。HTTP 响应的 handler 与主动推送的线程可能同时写同一条连接；`ctx.WriteAsync` 只承担链内转发，不是这条并发入口。

**先纠正一个容易搞错的点：这里不会出现「字节交错」——坏的是顺序。**

- `SaeaServerSocket.SendAsyncRaw` 用 `userToken.WaitWrite(...)` 加每个令牌唯一的 `WriteArgs`，同一时刻只有一个发送者在用 socket，所以单次发送是原子的，两帧的字节不会互相穿插。
- TLS 路径另有 `SaeaConnection.FlushOutput` 的 `_drainingOutput` 单发送者队列（`SaeaConnection.cs` 的 `FlushOutput`），同样不会穿插。
- 但两者都是**抢锁/抢入队权**，而 `SemaphoreSlim` / `ConcurrentQueue` 的唤醒顺序不等于调用顺序。`HTTP/1.1` 要求同一连接的响应按请求顺序出现，WS 的帧顺序直接决定消息语义——乱序就是协议错误，且不会报错，只在客户端表现为「拿到了别人的响应」。

新增每连接的单写者闸门：

```csharp
/// <summary>把同一连接上的出站写串成 FIFO；必须是出站前缀中最靠近业务侧的那一个节点。</summary>
public sealed class OutboundSerializingHandler : OutboundHandlerAdapter
{
    public override Task WriteAsync(IChannelHandlerContext ctx, object msg);
}
```

- **位置在出站前缀的最右侧（紧贴第一个入站节点），不是链首。** 出站是沿 `Prev` 方向走的：业务 `ctx.Pipeline.WriteAsync` 先碰到最右的出站节点，最后才到链头。若串行器装在链首，业务写得先穿过编码器才排队，只要编码器里有异步等待，先发起的写就可能后到串行器——即使业务严格按顺序调用也照样乱序。装在最右侧才能「先排队、再编码」。装配因此固定为：编码类出站 handler 先 `AddLast`，**串行器最后 `AddLast`**，然后才允许加第一个入站 handler。空闲（无写者在途、队列为空）时串行器在锁内取得写者角色后直接调下游，不建排队项；「到达即排队」与 `MaxPendingWrites` 预算只约束有写在途的情形，FIFO 语义不变。
- 保证的边界：单线程业务顺序 = 上线顺序。多线程**并发**调用业务 `WriteAsync` 本身没有定义顺序，串行器只保证到达即排队；要并发下也严格有序，得在公共写入口统一发序号，本设计不做。
- 实现：`lock` + `Queue<PendingWrite>` + 一个 `_active` 标志及 `_activeItem`。业务入口已创建 `OutboundOperation` 与公共完成源；调用方在锁内入队，空闲的那个调用方成为写者。写者每次在锁内取队首、登记 `_activeItem` 并把仍为 `Queued` 的项原子推进为 `HandedOff`，随后在锁外 `await ctx.WriteAsync(msg)`，按真实结果调用 `TryComplete`；再回锁内清 active、取下一个，队列空时清 `_active`。若关闭或移除已先把 active-not-handed 项完成，写者必须跳过下游调用。**不能用 `SemaphoreSlim` 的唤醒顺序当 FIFO 依据**，它不保证先到先得。
- **必须有背压预算，「等待者列表」不等于有上限。** 推送/广播/fire-and-forget 可以不 `await` 返回的 Task 而持续入队。新增 `MaxPendingWrites`（默认 256，可配）：超限时本次写直接失败，不静默排队；若消息已是 `RawFrame`，串行器按 §5.6 的「未移交」路径补偿释放，尚未编码的业务对象则由创建方在写失败后回收，串行器不猜测任意 `IDisposable` 的所有权。串行器位于编码器之前，排队项可能是任意业务对象，通用层无法可靠计算字节数，因此**不设虚假的 `MaxPendingBytes`**；这里只承诺待写项数有界，单项大小仍由协议自己的帧/请求上限约束，总字节数没有固定上界。
- 失败语义：某个写失败只让它自己的 Task 失败并冒泡，不阻断后续写；但可观察范围受 §5.5 限制——连接级失败让当前及后续写统一按 `ChannelClosedException` 失败，不承诺逐写精确归因。
- **不声明 `IDisposable`。** `ChannelPipeline.Dispose` 与 `HandlerRegistrationTable.Release` 都不会调它，声明了只会制造「会被自动释放」的假期望。清理挂在 `HandlerRemoved`，必须同时处理队列与 `_activeItem`：置关闭态后，所有 queued / active-not-handed 项禁止移交，公共 Task 以 `ChannelClosedException` 完成，`RawFrame` 走未移交补偿；active-handed-off 项只让公共 Task 失败，消息仍由下游负责，迟完成不重复减计数。其他任意业务对象不在通用层猜测所有权，仍由创建方依据失败 Task 回收。P0 分别覆盖 queued、active-not-handed、active-handed-off 三类移除与超时竞态。
- 已知代价：同一连接上「一慢全慢」，一个卡住的写会顶住后续所有写。要区分优先级（WS 控制帧插队）属于协议层自己的事，本 handler 不做插队。

**入站保序是协议迁移的强制装配项，不能只靠出站串行。** 当前 `ChannelPipelineOptions.MaxConcurrentMessages` 默认 16，`ConsumeAsync` 会逐条 `StartMessage` 而不等待前一条链完成；因此 HTTP keep-alive 的响应、RPC 同连接请求/响应匹配、WebSocket 消息处理及 FTP 控制应答都可能乱序。所有本批迁移的**单工请求/响应或要求消息顺序的协议链**统一设 `MaxConcurrentMessages = 1`；消费循环在下一条消息上会等待额度，保证前一条链完成后才继续。允许并发业务处理的包必须显式选择并证明响应可乱序，不能沿用默认值。`Inline` 快路径在 handler 异步挂起后同样允许后续消息排队并发，不能当成保序替代品。出站串行只约束写顺序，`MaxPendingWrites` 只限制已提交但尚未交接的写，两者都不提供入站业务保序。

---

## 六、分帧归属：为什么留在泵里

你要求的是 `ByteToMessageDecoder` 的形态。Netty 里它是「累加 + 解码」一体的；本设计只取「解码」，累加仍在执行器的泵里。

**理由：**

1. **拷贝与分配可控。** 泵直接在 `Pipe` 缓冲上切帧，每帧复制一次进 `PooledMessage`（`AllocateFrame`）。**「链上零拷贝」不成立，撤回这个说法**：§5.1 允许并要求业务消息自带内存，解码阶段完全可能再 `ToArray()` 一次。准确口径是「分帧到输入帧只复制一次；解码是否追加复制由消息所有权决定」——透传并 `Retain` 时没有第二次复制，构造独立业务对象时有。链内累加也可以用分段序列或池化缓冲避免额外拷贝，所以这一条不是「替代方案必然更差」，而是**在当前实现上**维持既有分配基线最省事。
2. **背压口径统一。** `MaxBufferedBytes` 由执行器统一执行；链内缓存理论上也能通过共享预算上报纳入背压，但那要新增一套链内缓存与执行器之间的记账协议，本次不想引入。
3. **改造范围。** 本批迁移包的协议边界都能用 `IFrameSplitter` 表达（长度域、行、整段、可切换），没有必须靠链内累加才能解析的。

**结论性质：这是工程取舍，不是技术必然。** 选它的真实理由是实现统一、所有权清晰、内存账集中、与上一轮性能工作不冲突。

**代价，必须接受：**

- `ByteToMessageDecoder` 不接受半包，子类实现比 Netty 简单，但语义不同，文档要写明。
- 链上不能装 `FrameSplitterHandler`（既有约定），装了会二次切帧。
- 任意字节流协议必须提供一个分帧器，不能只写解码器。

**被否掉的替代方案：** 泵只搬运原始字节、全部交给链累加。在本仓库当前实现下，它要重写背压记账并改变既有分配基线，收益抵不过成本；不是说这条路在别的实现里走不通。

---

## 七、包迁移矩阵

「现状」列取自代码实测；「阶段」列是本批实施顺序，标明「不改」或「可延后」的包不进入 P0–P5。

| 包 | 现状 | 是否删事件 | 建议阶段 | 风险 |
|---|---|---|---|---|
| `SAEA.Http` | `UsePacket<HttpCoder>`，`dataSpan.ToArray()`；`HttpCoder._cache` 每轮 `ToArray()` | 9 个（对外 3 个在 `WebHost`；`HttpSocket` / `HttpSocketDebug` 另 6 个同签名） | **P1 试点** | WebSocket 升级要换分帧器；`IUserToken` 引用 31 处 |
| `SAEA.WebSocket` | 服务端 `WSServerImpl` 与客户端 `WSClient` 各一条 `UsePacket`+span 栈；TLS 由 `SocketOptionBuilder.WithSsl` 承载（原 `WSSServerImpl` 已合并删除，服务端不再有两套实现） | 11 个 | P2 | 服务端与客户端都要迁；WS 分片重组不能套 `ByteToMessageDecoder`（§八. E） |
| `SAEA.FileSocket` | `UsePacket<BaseCoder>`，两次 `ToArray` | 5 个 | P2 | 文件块大帧的 `MaxFrameLength` |
| `SAEA.RPC` | `UsePacket<RpcCoder>` | 10 个 | P3 | `RSocketMsg` 编解码要从 `RpcCoder` 拆到链上 |
| `SAEA.QueueSocket` | `UsePacket<QueueCoder>` | 12 个 | P3 | `Exchange.OnBatched` 是批处理语义，不能简单删 |
| `SAEA.MessageSocket` | `UsePacket(_messageContext)`，包边界由 `MessageContext` / `Unpacker` 完成，再反序列化成领域消息 | 8 个 | P3 | 编解码固定在 `MessageContext` 里、链外不可插拔；不是「没有 coder」 |
| `SAEA.FTP` | **活代码不依赖 `SAEA.Sockets`**：`FTPServer` 用 `TcpListener`/`TcpClient`（`FTPServer.cs:60,138,319`），`FtpControlConnection` 用 `TcpClient`（`Net/FtpControlConnection.cs:139,233`），数据通道同。`Net/ServerSocket.cs`、`Net/FTPStream.cs`、`Core/FTPDataSocketManager.cs`、`Net/ClientSocket.cs` 才是 `UsePacket` + span，且均无调用方 | 6 个（全在死代码侧） | **P4：连传输一起重写** | 已明确不是普通迁移：服务端与客户端的控制/数据连接都换成 SAEA socket，回归面显著大于其他包 |
| `SAEA.P2P` | TCP 通道 `UsePacket`+span；UDP 通道/发现保持事件 | 仅 `TCPChannel` 3 个；`P2PClient` / `P2PServer` 公开事件保留 | P5 | 包内双栈共存；它是不对称例外，外部使用者不直接拿 TCP 链 |
| `SAEA.DNS` | UDP 服务端 `UsePacket`；TCP 查询端已是 `UseStream`+`GetStream()` | `OnRequested/OnResponded` 属领域事件 | P6（可延后） | 同步阻塞读模型 |
| `SAEA.MQTT` | 已是 `UseStream`，自身 `MqttTcpChannel` 从 `Stream` 解析 | 无接收事件（只有 1 个日志事件） | P6（可延后） | 36k 行；重写解析层无收益 |
| `SAEA.Socket5` | 已是 `UseStream`+`GetStream()`，0 个事件 | 无 | **不建议改** | 纯隧道，改造只增适配层 |
| `SAEA.MVC` | 间接依赖 Http/WebSocket，2 个事件镜像 `WebHost` | 2 个 | 随 P1 | 只是透传，工作量小 |
| `SAEA.Audio.Net` | 纯 UDP | — | 不改 | — |
| `SAEA.RedisSocket` | **确实在用 `SAEA.Sockets`**：`RClient : SaeaClientSocket` + `BaseContext<RedisCoder>`（`Base/Net/RClient.cs:47,71`），有 `OnMessage` / `OnActived`；上层 `RedisConnection` / `RedisConsumer` 另有领域事件 | 不在本批 | 不改 | 先前列它「引了但代码未使用」是错的。本批不改它，但 §5.5 对 `SAEA.Sockets` 发送失败语义的修改会影响它，必须列入回归对照 |

**结论：**迁移范围是 8 个包，其中 7 个（Http、WebSocket、FileSocket、RPC、QueueSocket、MessageSocket、P2P-TCP）的现行接收路径确实依赖 `SAEA.Sockets` 的事件或包内固定解码。FTP 活代码使用 `TcpListener` / `TcpClient`，但已决定保留在 P4，按传输重写而非普通迁移实施。MQTT / Socket5 / DNS-TCP 已经是流式，改造同样是重写而非迁移；RedisSocket 虽符合事件画像，本批明确不改，只做底层行为回归。

**P2P 范围已选 A：只删 `TCPChannel` 3 个内部传输事件。** `P2PClient` 的 `OnMessageReceived(string, byte[])`、`OnServerConnected` / `OnServerDisconnected`、`OnPeerConnected` / `OnPeerDisconnected` 以及 `P2PServer` 的现有公开事件全部保留；`OnStateChanged`、`OnLocalNodeDiscovered` 等领域事件也保留。它是「传输层在包内走链、对外仍是领域化传输事件」的唯一例外，§2.1 / §2.3 已同步限定。已接受的代价是 P2P 使用者拿不到链，与其他迁移包不对称；不采用再删 `P2PClient` 5 个事件的方案 B。

**FTP 范围已选 B：留在 P4，连传输一起重写。** 服务端与客户端的控制/数据连接都换成 SAEA socket，再按 §八. B/C 接链，同时删除旧实现；现行 `TcpListener` / `TcpClient` 不再保留为第二路径。线格式与 FTP 命令语义不变，但实现面、测试面与回归风险显著高于其他包，`SAEA.FTP.Tests` 13 个现有用例必须保持，并补 greeting/多行应答、PASV、REST 与数据通道收尾。旧死代码清理包含在本阶段，不另立前置任务。

---

## 八、五类改造模式

### A. 长度域帧（`RPC` / `QueueSocket` / `MessageSocket` / `FileSocket`）

四包都可复用 `LengthFieldSplitter` 的通用构造，但**线格式并不统一，不能都套 `Default`**。以下配置依次对应 `lengthFieldOffset / lengthFieldLength / lengthAdjustment / initialBytesToStrip`，长度均为小端：

| 包 | 现有线格式 | 长度语义与整帧长度 | 分帧配置 |
|---|---|---|---|
| `FileSocket` | `[8B bodyLength@0][1B type@8][body]` | 纯 body 长度；整帧 `9 + bodyLength` | `0 / 8 / +1 / 0` 保留完整帧；若解码器只接收 `[type][body]` 才用 `strip=8` |
| `MessageSocket` | 同上，经 `MessageContext` / `BaseCoder` | 同上 | 同上 |
| `RPC` | `[1B type@0][4B total@1][8B sequence][4B serviceLen][service][4B methodLen][method][data]` | `total` 含长度字段自身及其后全部字段；整帧 `1 + total` | `1 / 4 / -4 / 0` |
| `QueueSocket` | `[1B type@0][4B total@1][4B nameLen][name][4B topicLen][topic][data]` | 同 RPC；整帧 `1 + total` | `1 / 4 / -4 / 0` |

`MaxFrameLength` 当前直接约束分帧器读到的原始长度字段值，因此 RPC/QueueSocket 的上限口径是 `total`，不是纯 data body；要限制业务 body，必须把协议固定包含的 4 字节及可变字段纳入计算。P2/P3 分别用真实编码器产出的帧做跨段、粘包与边界长度测试，不以统一 9 字节头假设代替协议用例。

包内只需：
- 一个 `ByteToMessageDecoder` 子类：完整帧 → 包内的协议对象；
- 一个出站编码 handler：协议对象 → `RawFrame`（含上表所列传输头在内的完整线格式）。

**出站不再复用 `FrameEncoderHandler`。** 它只接受 `internal` 的 `EncodedBody`，迁移包在程序集外根本构造不出它的输入；既然无法复用，A 类包就统一走 §5.6 的 `RawFrame` 直达链头，不在链上保留第二套「body + 类型码」的形态。`FrameEncoderHandler` 与信封模型一起留给既有 `JsonMessageCodec` / `ProtobufMessageCodec` 场景。

`IUserToken.Coder` 的所有用法收敛到这两个类。

### B. 文本行（`FTP` 控制通道）

`LineSplitter` 切 CRLF。**P4 已确定连传输一起换成 SAEA socket**（§七），所以本节同时适用于现行服务端与客户端活代码；它们目前使用 `TcpListener` / `TcpClient`，不能只把旧 span 死代码换成链就宣告完成。

现存三类控制连接实现，处置各不相同：

| 位置 | 形态 | 处置 |
|---|---|---|
| `SAEA.FTP/Net/ServerSocket.cs`（internal `ServerSocket`） | span 事件 → `FTPStream.ReadLine()` | **死代码，删**：全仓没有 `new ServerSocket(` |
| `SAEA.FTP/FTPServer.cs` 的 `ServerSession` | `TcpClient` → `GetStream()` → `ReadCommandLine()`（`FTPServer.cs:324`） | 活，服务端，迁 |
| `SAEA.FTP/Net/FtpControlConnection.cs` | `TcpClient` → `GetStream()` → `ReadLine()`（`Net/FtpControlConnection.cs:253`） | 活，客户端，迁 |

**服务端与客户端需要两个不同的解码器，不能共用一个 `ClientRequest` 解码器：**

- 服务端：`FtpCommandDecoder` → `ClientRequest`。
- 客户端：`FtpReplyDecoder` → `FtpReply`，必须覆盖 greeting、`ReadReply()` 的多行应答（首行 `xyz-`，直到 `xyz ` 终止）、非法状态码、`FEAT` 的 `ParseCapabilities()`、以及等待应答期间连接关闭的唤醒。这些不是 `ClientRequest` 的逆向形式。
- 客户端 `Send()` 现在是「写请求 → 同步阻塞等应答」；迁到链上后要改成「`await ctx.Pipeline.WriteAsync(command)` → 等待该请求的 `FtpReply` 完成源」，并保留现有超时与请求-应答串行约束，不能变成并发发命令，也不能从入站 handler 误用专职转发的 `ctx.WriteAsync`。

`FTPCoder.Decode` 是空实现，随死代码一起删。不存在「同一连接双读者」：服务端与客户端本就不共享连接，真正的风险是漏删死代码。

### C. 无边界透传（`FTP` 数据通道）

**P4 已确定把 FTP 活代码的传输也换成 SAEA socket。** 数据通道不是只替换 `GetStream()` 读取方式，而是与控制通道同属一次传输重写。

现行数据连接有两套活实现，另有两类旧实现，不能只用一句「活代码都是 `GetStream()`」糊过去：

| 位置 | 当前职责 | 迁移口径 |
|---|---|---|
| `FTPServer.ServerSession` | 服务端 PASV 监听、接受数据连接、上传/下载流、REST 与完成应答 | 保留 PASV 建连与传输语义；已接受的数据连接改接链 |
| `Net/FtpControlConnection.cs` | 客户端主动连接被动端口、上传/下载流、超时与关闭 | 保留 Connect、超时与方向语义；已建立的数据连接改接链 |
| `Core/FTPDataSocketManager.cs` | 旧服务端 `UsePacket()` + span | 无现行调用方，随死代码删除 |
| `Net/ClientSocket.cs` | 旧客户端数据通道 | 无现行调用方，随死代码删除 |

链只接管**已建立连接后的读写**。本批不把「为了用 Pipeline 而替换 `TcpListener` / `TcpClient`」藏在数据通道适配层里：P4 必须把控制通道与数据通道的建连替换作为同一个传输重写任务显式实施，PASV、REST、上传/下载方向、`226` 完成应答与关闭时序逐项保持。

- 分帧：`TakeAvailableSplitter`，消息边界 = 一次读取；一次读取超过单帧上限时按上限切成多帧（泵落后于下游背压时 `Pipe` 会把多次读取合并交出，不能因此报错断链）；
- 解码：`FtpDataChunkDecoder : ByteToMessageDecoder`，把帧转成 `FtpDataChunk`（持有帧的独立拷贝或 `PooledMessage` 所有权）；
- 传输语义：数据通道是一次性的（连接 → 传完 → 关闭），包内用 `ChannelStreamAdapter` 把「链上分块消息」包成 `Stream` 给现有的文件读写代码用。

`ChannelStreamAdapter` 的线程与收尾契约必须写进注释和测试：

1. **强制 `DispatchMode.Queued` 是本设计的约束，不是「Inline 必然死锁」的推论。** 现有 Inline 快路径在 handler 只同步入队并通知等待者时可以工作；真正危险的是 adapter 在接收调用栈里同步阻塞、消费者又等待接收线程，或 handler 等待 `Stream.Read` 的消费结果。为消除这类接线组合，数据通道统一 Queued，并增加「Inline 快路径 + 独立消费者」反例用例，不能用未经验证的互锁结论代替测试。
2. adapter 用有界队列连接 handler 与 `Stream` 消费者，明确容量、超限等待/拒绝策略及池化帧所有权；`Read`/`ReadAsync` 的 EOF、取消、底层异常必须唤醒并传播给所有等待者，`Dispose` 也必须解除阻塞，不能只在正常 EOF 时唤醒。
3. `Write`/`WriteAsync` 只做「编码 + `ctx.Pipeline.WriteAsync`」发起业务写，不保证对端已收到；`Flush`/`FlushAsync` 等待已提交写交接，关闭前还要经 §5.8 的有界收尾，否则截断。背压等待不得堵死执行器分发线程。

`SAEA.Socket5` 不在本批（见 §七），它仍是纯隧道。

### D. 协议升级（`Http`→WebSocket）

同一连接上换协议。**升级由业务发起，不是解码器自动识别**：现状是业务调 `HttpResponse.Upgrade(handshake, onData, onError)`（`HttpResponse.cs:290`），它先把 101 入队，再调 `HttpCoder.UpgradeRaw` 把 `_cache` 里的残余字节原样 `DispatchRaw` 给 WS（`HttpCoder.cs` 的 `UpgradeRaw`）。迁移后这个「coder 内部把残帧倒给新协议」的动作正是屏障要替代的机制。

1. HTTP 分帧器产出**任何带 `Upgrade: websocket` 头的请求帧**时设置 `SwitchableSplitter.StopAfterCurrent = true`（屏障，见 §5.3）。分帧器无法预知业务是否接受升级，所以一律停，不停就挡不住紧随其后的 WS 首帧。`ChannelPipelineOptions`、HTTP 分帧器、`SwitchableSplitter` 与 WS 分帧器全部每连接新建；泵和该连接的 `HttpProtocolHandler` 持有**同一个** `SwitchableSplitter` 实例，绝不在监听器级共享；
2. 泵切完这一帧后停住，残余字节留在 `Pipe`；
3. 业务在 handler 里决定接受升级：包内 `await ctx.Pipeline.WriteAsync(101 握手响应)`，**只有该写 Task 成功完成后**才调 `SwitchableSplitter.SwitchTo(WS 分帧器)` 解除屏障；写失败则不切换并按连接失败收口，拒绝升级则按 §5.3 复位屏障，继续当普通 HTTP；
4. **链结构不变**：`HttpProtocolHandler` 内部同时持有 HTTP 帧解码与 WS 帧重组两套状态机，收到升级完成信号后只把后续 `PooledMessage` 原样交给 WS 组帧器。一个包内的 `HttpRequestDecoder` / `WsFrameAssembler` 仍是两个独立类，由这个入口 handler 组合，不合并成一个「既懂 HTTP 又懂 WS」的巨型解码器；
5. 泵恢复，残余字节由 WS 分帧器继续切。

**为什么不 `Remove(旧解码器)` + `AddLast(新解码器)`：** 入站是嵌套 `await`，升级 handler 执行时上游解码器的 `ChannelReadAsync` 仍在栈上；此时 `HandlerRemoved` 释放解码器状态会伤到正在处理的消息。改链节点还会把新解码器放到业务 handler 之后，顺序也错。链上节点增删只允许在 `Start()` 前（§5.2、§4.1）。

HTTP 升级请求之后可能紧跟客户端首帧，这正是屏障要挡住的情形。**没有 FTP `AUTH TLS`**：仓库里没有这个命令（`SAEA.FTP` 中出现的 `AUTH*` 全是 `Authenticated` 登录状态，没有 `AUTH TLS` / `PBSZ` / `PROT`），TLS 只在建连时由 `SocketOptionBuilder.WithSsl` 固定，中途换加密层也不是分帧器能做的事。若要加是独立特性，不属本次重构。

### E. 跨帧重组（`WebSocket`）

WS 的消息边界不等于 wire frame 边界：continuation 分片要拼，控制帧可以在分片之间插入（`SAEA.WebSocket/Model/WSCoder.cs` 已在做这件事）。

因此 WS **不用** `ByteToMessageDecoder`，用普通 `InboundHandlerAdapter` 持有每连接的分片状态：

- 分帧器只切 wire frame，`MaxFrameLength` 是单帧上限；
- 解码 handler 负责拼帧、按规则处理分片之间穿插的控制帧、拒绝在分片消息中交织新的数据帧，并执行 `MaxMessageSize`。

这条也说明 §5.1 的边界：`ByteToMessageDecoder` 只适合无跨帧状态的协议；有状态的自己写 handler，不要硬套基类。HTTP 升级路径可把同一套组帧器作为 `HttpProtocolHandler` 的字段复用，入口 handler 负责按阶段分派（§八. D）。

### F. 请求上限（`Http`）

`MaxRequestSize` 只限制请求 body，不是含头部的整帧上限。`sae` 现状 body 无上限，本次取 64MB 默认值并可配置（§4.6）；整帧 `MaxFrameLength` 还要加上独立的头部上限。

**但只把 `MaxFrameLength` 设成 64MB 防不住慢速超大请求。** 单长度分帧器必须先攒够整帧才产帧，等 `TrySplit` 返回成功时 64MB 已经在 `Pipe` / 池里了，此时再判 413 只是事后通知，内存早就被占住。要真正起作用，HTTP 分帧器必须**先解析头部、再等 body**：

1. 读到 `\r\n\r\n` 为止，头部本身设独立上限（默认 64KB，超限 431/400 + 断开），避免「只发头不发 body」的变种打爆内存；
2. 从头部取 `Content-Length`。**`Transfer-Encoding: chunked` 维持现状不支持**（`RequestDataReader` 现在就没有 chunked 解码），迁移后显式返回 400 并断开，不把 chunk 字节误当 `Content-Length: 0` 后的下一条请求；支持 chunked 属于新增协议功能，与 §2.2「不新增业务功能」冲突，不在本批；
3. `Content-Length > MaxRequestSize` 时**立刻**判超限，不等 body；
4. 否则按 `Content-Length` 等齐 body，再产出整帧。

**报头结束符已到齐但解析失败只能判 400。** 此时流里已有完整 `\r\n\r\n`，不存在「再等几字节就能解析」的情况；若仍按数据不足返回，请求行非法的连接既收不到响应也不会断开，直到对端主动关闭并发起上限，等于给了「发畸形请求占住连接」的口子。因此解析失败一律抛 400 token 走与 413/431 相同的上链收尾路径。

**分帧器自己发不出 413，必须走链。** `IFrameSplitter.TrySplit` 没有 ctx 也没有出站能力，泵对分帧异常现在只记录并 `BeginShutdown`——直接抛 `FrameProtocolException` 的结果就是「断开但客户端收不到任何响应」。P0 因此要给泵补一条路径：分帧器抛出协议错误时**停止继续切帧**，`await FireExceptionCaughtAsync` 把异常派上链，由包内 handler 生成 413/431 的 `RawFrame`，先 `await ctx.Pipeline.WriteAsync(response)`，再经 `CloseAfterWriteAsync` 有界收尾。异常派发返回后若连接仍是 `Open`，表示没有 handler 发起收尾，泵立即进入强制收口；若已是 `DrainingWrites` / `Closing`，统一交给 §5.8 的关闭状态机等待，不能再起第二个关闭流程。分帧器本身绝不直接写 socket，那等于绕开链，重新制造所有权与串行化问题。

所以 HTTP 的分帧器不是 `LengthFieldSplitter`，是包内专用实现；`MaxFrameLength = MaxRequestSize + 头部上限`，`MaxBufferedBytes` 按 §4.6 的显式下限与建议预留设置。

---

## 九、破坏面

| 类别 | 数量 | 处理 |
|---|---|---|
| 接口签名与新增成员 | Pipeline 4 个接口共新增 6 个成员：`IChannelHandler` 2、`IChannelInboundHandler` 2、`IChannelHandlerContext` 1、`IChannelPipeline` 1；`SAEA.Sockets` 的 `IServerSocket` / `IClientSocket` 各有一个带 `owner` 重载 `void → Task`，TCP/UDP 四个实现同步变更 | 外部直接实现这些接口必须同步修改；Pipeline 适配器提供生命周期默认实现，但无法替外部直接实现接口规避编译破坏。另记录入站异常通知后冒泡、`ctx.WriteAsync` 误用改为抛错、`Start()` 后冻结链等行为变化，README 与验收分别覆盖 |
| 迁移矩阵所选事件声明 | 66 个具体声明（含 18 个在 `internal` 类中，公开可见 48 个）：Http 9（公开 3）、WebSocket 11（公开 8）、FileSocket 5、RPC 10（公开 5）、QueueSocket 12（公开 11）、MessageSocket 8、FTP 6（公开 3）、MVC 2、P2P 传输 3；不含 P2P 领域事件与 MQTT/DNS/Socket5。**66/48 只复核矩阵选中的声明，不是完整公开 API 破坏面总数**；接口、抽象声明、override 与转发声明的计数口径此处不混用 | 接收/生命周期类整删；内部类的声明随类一起删；保留的领域事件逐包列进实施计划。完整破坏面另列清单，至少补 `HttpContextBase.OnException/OnRequestDelegate`、Http/MVC 的 override、`SAEAMvcApplication` 两个事件及接口声明，不能拿 66 当验收上限；当前 FTP/P2P 决定下 66/48 的矩阵口径不变，实施计划仍须另列完整破坏面 |
| 演示工程 | 10 个迁移 demo（`FileTest` `FTPTest` `HttpTest` `RPCTest` `QueueSocketTest` `MessageTest` `WebSocketTest` `P2PTest` `MVCTest` `Mvc.ServiceTest`），另有 `PipelineTest` 参考工程 | 10 个迁移 demo 全部改为链装配写法，自测参数保持可用；P2P 按例外保留公开事件；`Mvc.ServiceTest` 创建 `SAEAMvcApplication`，必须随 MVC 验收；`PipelineTest` 在 P0 同步新装配顺序与发送语义；`MQTTTest` 不动 |
| 单元测试 | 四个 MSTest 工程均纳入回归；其中 `SAEA.FTP.Tests` 13、`SAEA.MQTT.Tests` 153，Pipeline 数量以 P0 基线实测为准 | FTP 随包改；MQTT 不动；Common 与 Pipeline 分别指定 csproj 运行，不能只跑受影响包 |
| README | 迁移侧 9 个组件（8 个迁移包 + MVC）× 中英两份，共 18 份；基础设施侧再加 `SAEA.Sockets` / `SAEA.Sockets.Pipeline` 各中英两份，共 4 份 | 迁移组件的示例、能力清单、事件表全部重写；P2P 明确对外事件保留的例外。基础设施 README 必须列出接口新增成员、发送 `void → Task`、链冻结、RawFrame/解码器/关闭语义等公开变化，不能只改迁移包。根目录 `README.md` / `README.en.md` 已核对：只有包清单与能力简述，没有事件示例，不属于必然破坏面，只在能力描述变化时同步 |
| 设计文档 | 本文件 + `2026-11-05-socket-pipeline-design.md` §7 / §8.1 | 验收清单补迁移项 |

---

## 十、阶段划分与验收

每阶段独立可验证、独立可提交。

| 阶段 | 内容 | 验收 |
|---|---|---|
| **P0** | 先跑基线并记录数值（Pipeline 单测 Debug/Release 计数、`PipelineTest --bench` 输出），再动手。`ByteToMessageDecoder`（输入 `PooledMessage`）；生命周期 + 执行器状态门 + Inactive 前保留属性 + `Start()` 后冻结链；`SwitchableSplitter`（屏障两分支、同一 ReadResult 续切、超时、固定上限）；`TakeAvailableSplitter`；`RawFrame`（互斥装配 + 原子所有权移交）；`RequireTerminalHandler` + 串行器装配校验；`CloseAfterWriteAsync`（单一关闭状态机、业务入口/内部转发分离、exactly-once 计数、有界等待）；`OutboundSerializingHandler`（最右出站位置 + 待写项数预算）；分帧协议错误上链；`SAEA.Sockets` 按 §5.5 方案 A 把两个带 `owner` 重载改为可等待 `Task`，补齐 TCP 独立完成源、超时关闭与迟完成所有权、TLS 全记录等待，并同步 UDP 实现签名；修正并冻结 `SocketStreamAdapter` / `PipelineHost` 母本 | Pipeline 单测全绿；新增用例覆盖：四种解码路径、多输出在转发前 / 后失败及取消的引用计数、普通异常只通知一次后冒泡、屏障两分支与无新字节续切、切换上限、启动前关闭顺序、Inactive 读属性、wiring 启动失败清理、`RawFrame` 迁移链与既有信封全标准链的装配、业务入口 / 自定义 handler 吞写或抛错 / 下游失败 / 链头关闭或未 Attach 拒绝 / 同节点重复转发的异常所有权、出站回调外误用 `ctx.WriteAsync`、同步/异步写计数、关闭与独立业务写重入、零超时、queued / active-not-handed / active-handed-off 的移除与超时竞态及公共 Task 迟到完成、draining 中旧写跨 handler 转发、异步编码器下的 FIFO、协议链入站保序、待写队列超限、socket 发送失败/超时后迟完成、TLS 最后一帧、413/431 上链收尾；`PipelineTest --all` 仍过；`--bench` 吞吐不低于基线 95%，每帧分配只记录趋势并说明来源（调用绑定视图与派发状态属 §5.1 / §5.2 的固定成本，不再按 5% 卡分配；详见 §十二 第 28 条） |
| **P1** | `SAEA.Http` + `SAEA.MVC` 试点；删事件；WebSocket 升级（不换链节点，§八. D）；HTTP 专用分帧器（先头后 body，头部上限 + 413 早判） | 现有 `HttpTest` 场景逐条通过；`Content-Length` 请求体、静态资源、101 写成功后才切换及写失败不切换、body 超大请求、头部上限、chunked 显式 400 拒绝、报头完整但畸形 400 拒绝各有用例；无整段 `ToArray` |
| **P2** | `SAEA.FileSocket`、`SAEA.WebSocket` | 文件收发、WS 服务端与客户端均通过；FileSocket 用 `0/8/+1` 配置覆盖跨段、粘包与边界长度；WS 增加 continuation 分片间穿插控制帧、非法数据帧交织与消息上限用例；对应 demo 编译并完成人工 smoke |
| **P3** | `SAEA.RPC`、`SAEA.QueueSocket`、`SAEA.MessageSocket` | RPC/QueueSocket 用 `1/4/-4`、MessageSocket 用 `0/8/+1` 配置覆盖真实编码帧、跨段、粘包与边界长度；三包 demo 编译并完成人工 smoke；已有非交互自测入口按全局验收执行并保存结果 |
| **P4** | 删除旧死代码；`SAEA.FTP` 服务端/客户端的控制与数据连接传输重写 + 链接入（含流适配层） | `SAEA.FTP.Tests` 13 个现有用例全绿并补 greeting/多行应答/PASV/REST/数据收尾；`FTPTest` 用本次构建 DLL 做窗口人工 smoke |
| **P5** | `SAEA.P2P` TCP 接链；只删 `TCPChannel` 3 个内部事件，`P2PClient` / `P2PServer` 公开事件原样保留 | `--all`、`--tls` 通过；公开事件行为回归通过；UDP 部分零改动 |
| **不在本批** | `SAEA.MQTT`、`SAEA.DNS` TCP 查询端、`SAEA.Socket5`、`SAEA.RedisSocket` | 单独评估，不占用本批验收；RedisSocket 仍须做 §5.5 底层签名/行为变化的编译与运行回归 |

**全局验收拆成三类，不把交互程序写成自动测试：**

1. **自动构建与测试**：`dotnet build Src/SAEA.Sockets.sln -c Debug` 0 errors；四个 MSTest 工程分别指定 csproj 并全绿；`PipelineTest --all` / `--bench`、`QueueSocketTest --functional`、`P2PTest --all` / `--tls` 等已有非交互入口按对应阶段执行，保存命令、退出码和关键输出。
2. **demo 编译 + 人工 smoke**：10 个迁移 demo 逐个核对链装配，`PipelineTest` 另按自动入口回归；窗口程序与交互菜单只写人工步骤和预期结果，不声称一条命令即可自动完成基本流程。`FTPTest`、`Mvc.ServiceTest` 属窗口/服务程序，`FileTest`、`MessageTest` 等需要控制台输入；未实际操作的项记「未运行」，不得记通过。未改动的 `SAEA.MQTTTest` 同样作为人工回归对照。
3. **旧式工程必须验证实际引用**：`FTPTest` 与 `Mvc.ServiceTest` 是 .NET Framework 4.6.2 旧式工程，当前用固定 `HintPath` 指向 `Src/packages/SAEA.*.26.4.23.1`，只跑它们可能仍在验证旧 DLL。实施时优先把 SAEA 引用切到 `ProjectReference`（不改版本号或 `PackageReference`）；若保留 `HintPath`，必须指向本次构建输出，并在 smoke 记录中核对实际加载程序集路径。其余旧式工程在实施计划中做同样核查。

---

## 十一、风险

| 风险 | 影响 | 应对 |
|---|---|---|
| 删事件后使用者无路可走 | 破坏性反馈 | 每包必须同时交付协议解码器 + `ConfigurePipeline` 装配点 + README 示例 |
| `ByteToMessageDecoder` 语义与 Netty 不同 | 误用 | XML 注释与 README 明写「不累加」；`FrameSplitterHandler` 标注不可与泵共存 |
| 协议升级切换分帧器时字节错位 | 数据损坏 | `StopAfterCurrent` 屏障 + 泵停切；用例覆盖「101 响应与 WS 首帧同一 TCP 段」 |
| 屏障等待没有上限 | 升级 handler 漏调 `SwitchTo` 时连接永久挂起 | `SplitterSwitchTimeout` 默认 10 秒，超时按 `FrameProtocolException` 收口（§5.3） |
| 升级时在消息处理中增删链节点 | 上游 handler 仍在栈上，`HandlerRemoved` 释放状态会伤到当前消息 | 升级不换链节点（§八. D）；`Start()` 后链结构冻结，增删只允许在启动前（§4.1、§5.2） |
| `CloseAfterWriteAsync` 计数点放在链头，或把 handler 转发当新写 | 队列中的写不被等待，或 draining 中旧写被误拒/重复计数 | 计数只放在业务写入口，内部转发沿用同一操作上下文（§5.8） |
| Pipeline 接口变更 | 外部实现编译失败 | handler 两个接口合计加 4 个成员（`HandlerAdded`/`HandlerRemoved`/`ChannelActiveAsync`/`ChannelInactiveAsync`），context 与 pipeline 各加 1 个；适配器给生命周期默认实现；4 接口 6 成员全部记录在包 README 的破坏性变更节 |
| `IUserToken` → `IChannel` 的替换面被低估 | P3 工期 | P1 试点先跑通完整替换，再复制模式 |
| 适配层复制 8 份后漂移 | 修一处漏七处 | P0 参考实现带单测；各包副本只改命名空间；README 记录副本清单 |
| FTP 流适配器阻塞、背压或关闭唤醒设计不全 | 传输卡死、等待者不唤醒、池化资源滞留 | 数据通道强制 Queued 是设计约束；有界队列 + EOF/取消/异常/Dispose 唤醒用例（§八. C） |
| 演示工程回归被漏掉 | 发布即坏 | P1 起每个阶段都要求对应 demo 实跑，不只看编译 |
| `CloseAfterWriteAsync` 等写出队列清空 | 对端不读时关闭被拖到 `DrainTimeout` | 超时按超时收口并记 Warn；不无限等 |
| 5.7 的 `RequireTerminalHandler` 误伤 | 已有装配少装链尾导致启动失败 | 默认 false；只在迁移包显式开启 |
| HTTP 上限只设 `MaxFrameLength` | 413 在 64MB 到齐后才发，防不住慢速攻击 | 分帧器先解析头、早判 `Content-Length`，头另设 64KB 上限（§八. F） |
| 出站乱序（不是交错） | 响应顺序错、WS 帧序错，且不报错 | 出站前缀最右侧 `OutboundSerializingHandler` 空闲直写、有写在途时排队再编码；用异步编码器验证 FIFO（§5.9） |
| 默认 16 路入站并发打乱协议顺序 | 出站 FIFO 仍会按业务完成先后发出错误响应序列 | 请求/响应及有序消息链显式 `MaxConcurrentMessages = 1`，并加慢请求阻塞后续请求的用例（§5.9） |
| socket 发送超时立即归还 owner | 内核迟完成仍读旧缓冲，池化数组复用后造成数据损坏 | 超时先关 socket，等完成回调后由唯一路径回收；无法证明退出则放弃回收（§5.5） |
| FTP 死代码被当成活代码迁移 | 白做一套服务端，且仓库留下两套实现 | P4 先删 `Net/ServerSocket.cs` / `Net/FTPStream.cs` / `Core/FTPDataSocketManager.cs` / `Net/ClientSocket.cs`，再重写现行控制/数据传输；FTP 不存在同连接双读者 |
| 底层发送静默失败或 TLS 后台写提前返回 | 丢数据且无异常，FIFO 与 `CloseAfterWriteAsync` 均失效 | P0 按 §5.5 方案 A 把两个带 `owner` 重载改为可等待 `Task`，覆盖真实完成点与可确认退出路径的 `owner` 恰好释放一次；无法确认退出的超时路径放弃回收 |
| `RawFrame` 已移交后异常路径再次 `Dispose` | 池化缓冲重复归还或任意 `Owner` 被重复释放 | 原子移交标记；传输接管后只由传输释放；会话不存在与适配器异常用例（§5.6） |
| 出站队列无界或关闭状态竞争 | 内存持续增长、关闭漏等写或重入自等待 | `MaxPendingWrites` + 单一关闭状态机 + exactly-once 计数 + 重入拒绝 + 有界超时（§5.8/§5.9） |
| 切换分帧器后缓冲上限仍是旧值 | 新协议合法大帧被 `buffered-bytes-limit` 断开 | `SwitchableSplitter.MaxFrameLength` 取所有协议最大值，`SwitchTo` 拒绝超限（§5.3） |
| 启动前连接已关闭 | 先 `Inactive` 后 `Active`，或关闭后仍起泵 | 执行器补状态门，Closing 后禁止 `Attach`/`Start`（§5.2） |
| `HandlerAdded` 里写消息或读 `Channel` | 未链接节点的 `Next`/`Prev` 为 null，写出站直达链头、读 `Channel` 抛 | `HandlerAdded` 只许存 ctx；初始化放 `ChannelActiveAsync`；装配顺序 `Create → Attach → 装配 → Start`（§5.2） |
| 四类长度域协议被套成同一种帧头 | RPC/QueueSocket 帧长计算错误，FileSocket/MessageSocket 解错 type 与 body | 按 §八. A 的逐包布局与 splitter 参数实施，用真实编码帧做跨段/粘包测试 |

---

## 十二、已确认的决定与我的取舍

**你已决定：**

1. 不新增共享包，每个迁移包自带一份传输适配层（§5.5）。
2. P2P 选 A：只删 `TCPChannel` 的 3 个内部传输事件，`P2PClient` / `P2PServer` 的公开事件全部保留；它是对外不直接暴露链的唯一例外（§七）。
3. `SAEA.MQTT`、`SAEA.DNS` TCP 查询端、`SAEA.Socket5` 不在本批（§七、§十）。
4. FTP 选 B：保留在 P4，服务端与客户端的控制/数据连接都从 `TcpListener` / `TcpClient` 重写为 SAEA socket + Pipeline，旧死代码随本阶段删除（§七、§八. B/C）。
5. 破坏性版本号由你定，我不动 `.csproj`。
6. 底层发送 API 选 A：`IServerSocket` / `IClientSocket` 两个带 `owner` 重载从 `void` 改为 `Task`，不新增平行 API（§5.5）。
7. 入站异常选「全链通知一次后冒泡」，让 decoder 能观察下游失败并清理未派发余项；不保留 decoder 专用传播模式，也不收窄既定所有权保证（§5.1）。

**我按你的决定做了这些取舍，请在看文档时顺手核对：**

| # | 取舍 | 位置 |
|---|---|---|
| 1 | 分帧留在泵里，`ByteToMessageDecoder` 只做「帧 → 消息」，不累加 | §六 |
| 2 | 新增公开 `RawFrame` 作为出站最终形态，避免为过链伪造 `Envelope` | §5.6 |
| 3 | 升级切换加 `StopAfterCurrent` 屏障，执行器在切完当前帧后停住等 `SwitchTo` | §5.3、§八. D |
| 4 | 分帧器与 options 一律每连接新建（理由是免判断，不是因为每个分帧器都有状态） | §5.3 |
| 5 | WebSocket 不用 `ByteToMessageDecoder`，用带分片状态的普通 handler；允许控制帧穿插在 continuation 分片之间，拒绝新的数据帧交织 | §八. E |
| 6 | 包保留服务端主动推送 API；连接登记表只有 `IPipelineHost` 一张，ctx 挂在会话对象上，不绕过链 | §4.2、§4.5 |
| 7 | HTTP 单请求上限默认 64MB，超限 413 + 断开（现状无上限，属有意收紧）；上限在头部解析后立刻判定，且头部另有 64KB 上限 | §4.6、§八. F |
| 8 | 除 P2P 的既定例外外，客户端与服务器对称：都删接收事件、都给 `ConfigurePipeline` | §4.1、§七 |
| 9 | 迁移包启用 `RequireTerminalHandler` 后，链上缺 `ExceptionTerminalHandler` 时 `Start()` 直接失败，不允许静默丢消息与业务侧 `IDisposable`（池化帧本身由执行器兜底，不泄漏）；既有场景默认不启用 | §5.7 |
| 10 | 收口用 `CloseAfterWriteAsync` 等已提交的出站写交接完再断；`ChannelInactiveAsync` 里仍禁止回包 | §5.8 |
| 11 | 出站串行器放在出站前缀最右侧，空闲直写、有写在途时排队再编码；`MaxPendingWrites` 限制待写项数，各包不再自己加锁 | §5.9 |
| 12 | 协议升级不换链节点：入口 `HttpProtocolHandler` 内部按阶段分派，避免在途 handler 被 `HandlerRemoved` 释放；`SplitterSwitchTimeout` 默认 10 秒兜底 | §八. D、§5.3 |
| 13 | `CloseAfterWriteAsync` 只在业务写入口计数，handler 转发沿用同一操作；普通/写出感知关闭共用状态机；复用 `DrainTimeout` 但该入口不接受零值无限等待 | §5.8 |
| 14 | **深审后改**：`ByteToMessageDecoder.Decode` 输入改为 `PooledMessage`，基类**不**释放输入帧，原始引用仍由执行器释放 | §5.1 |
| 15 | **深审后改**：`HandlerAdded` 只许保存 ctx，装配顺序固定 `Create → Attach → 装配 → Start`；执行器补状态门 | §5.2 |
| 16 | **深审后改**：`SwitchableSplitter` 用固定上限，不做切换时改 `MaxBufferedBytes`；`TakeAvailableSplitter` 不豁免 `Create` 校验 | §5.3、§5.4 |
| 17 | **深审后新增，现已确定**：发送 API 选 A，两个带 `owner` 重载 `void → Task`；暴露失败、等待非 TLS/TLS 真实交接；发送超时先关 socket 并等迟完成，不提前归还 owner，否则既有静默丢包与缓冲覆写都会进入新链 | §5.5 |
| 18 | **深审后改，现已确定**：FTP 按传输重写留在 P4，客户端需要独立 `FtpReplyDecoder`；P2P 选 A，保留对外公开事件 | §七、§八. B/C |
| 19 | **深审后改**：串行器不能放链首；`RawFrame` 与信封编码路径互斥且所有权原子移交；`Start()` 后冻结链 | §4.1、§5.6、§5.9 |
| 20 | **深审后改**：413/431 必须由分帧异常上链后经 `RawFrame` + 有界收尾发出；chunked 请求体维持不支持，但新增显式 400 拒绝，避免误解析后续字节 | §八. F |
| 21 | **深审后改**：demo 验收拆自动测试与人工 smoke；补 `Mvc.ServiceTest`，旧式 `HintPath` 工程必须核对实际加载 DLL | §九、§十 |
| 22 | **源码对照后改**：协议链显式逐条保序；Inactive 通知前保留连接属性；多输出解码中途失败清理未派发余项 | §5.1、§5.2、§5.9 |
| 23 | **Pipe 语义复核后改**：升级屏障在同一份 `ReadResult` 上等待并续切，不把残帧标成已检查后另等新字节 | §5.3 |
| 24 | **线格式复核后改**：FileSocket/MessageSocket 用 `0/8/+1`，RPC/QueueSocket 用 `1/4/-4`；不再声称四包统一 9 字节头或 length@8 | §八. A |
| 25 | **出站 API 已选定**：新增 `IChannelPipeline.WriteAsync` 作为唯一业务入口；`ctx.WriteAsync` 专职链内转发，写计数与 draining 只在前者生效，不引入 `AsyncLocal` | §4.1、§4.3、§4.5、§5.8 |
| 26 | **终审后补齐**：单次调用用 context 视图携带操作；写操作有独立公共完成源与 `Queued / HandedOff / Completed` 状态，关闭和移除只补偿未移交项；禁止出站关闭与独立写重入 | §5.2、§5.8、§5.9 |
| 27 | **入站异常已选定**：普通异常通知一次后冒泡，按逐次调用所有权补偿未转发项；原始帧与 decoder 输出即使引用同一 `PooledMessage` 也分别记账 | §5.1、§5.2 |
| 28 | **P0 实测后定**：`--bench` 门禁改为「吞吐不低于基线 95% + 每帧分配记录趋势」。实测吞吐提升 43%–53%，每帧分配增加约 135 B，来源是 §5.2 强制的调用绑定视图与 §5.1 的派发状态对象，属固定成本；不再按 5% 卡分配，避免用热路径优化推翻「每次调用独占一个视图」的并发前提 | §十 |
| 29 | **P0 实施中校正**：§十 P0 验收里的「413/431 上链收尾」拆开理解——P0 只交付泵侧「分帧协议错误上链 + 有界收尾」能力；413/431 响应由 HTTP 分帧器与 handler 生成，随 P1 交付与验收 | §八. F、§十 |

当前等待你人工审查本文件；在你最终确认前不创建实施计划、不继续迁移代码。确认后才按 P0 → P1 写实施计划（`docs/superpowers/plans/2026-11-06-*.md`）。