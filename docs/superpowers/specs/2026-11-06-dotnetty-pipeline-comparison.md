# Socket Pipeline 迁移设计与 DotNetty 官方源码对照

日期：2026-11-06  
对照设计：`docs/superpowers/specs/2026-11-06-socket-pipeline-migration-design.md`  
DotNetty 仓库：`Azure/DotNetty`  
固定提交：`379d8cc1d32d2b557347aad1f7b6c48656212a4b`（`dev` 分支核对时的提交）

## 一、结论摘要

> **文档状态：已核验并回写主设计。** 下文「本项目设计」列保留的是本次审查时初稿的被审表述及当时章节号，用于记录纠错依据，**不是现行设计摘录**；现行约束一律以主设计为准。初稿中的 `channelInactive`、handler 时序、decoder 防护字段和 close API 错误已删除或改正，协议升级也已明确为「运行中冻结链、升级不换节点」，不是重建链。

对 DotNetty 机制本身，**双向链表传播方向、write/flush 分离、decoder 输出所有权、write 高低水位、WebSocket 分片与控制帧约束**可按固定提交源码作为行为参照；初稿则有以下问题，不能当成 DotNetty 原样语义：

1. **明确错误，已修正：「DotNetty `channelInactive` 里还能有效写」不成立。** DotNetty 关闭时先使 outbound buffer 失效并关闭 channel，再触发 inactive；inactive 后的新写入会释放消息，并返回以 `ClosedChannelException` 失败的 Task。主设计 §5.2 已改为双方都不能在 inactive 后有效发送。
2. **明确错误，已划清归属：初稿的 handler 回调顺序与 DotNetty 相反。** DotNetty 新增 handler 是「先链接进 pipeline，再调用 `HandlerAdded`，失败再回滚」；替换是「新 handler `HandlerAdded` 先于旧 handler `HandlerRemoved`」。主设计 §5.2 的「Added 成功后挂链」现在只声明为 SAEA 自有算法，不再冒充 DotNetty 时序。
3. **明确错误，已移除：初稿把 `decodeRemovalReentryProtection` 当作当前 DotNetty 的实际字段。** 固定提交中没有该字段；实际防护是每轮 decode 检查 `context.Removed`，并在 `HandlerRemoved` 中转发残余输入。现行主设计冻结运行中链，不再引用这个字段。
4. **表述不严谨，已移除：初稿的「`close(voidPromise)` 等待 close」不是可照搬的公开 API 或关闭语义。** DotNetty 关闭不会等待待写队列自然排空；官方 WebSocket 握手等代码采用「`WriteAndFlushAsync` 的 Task 完成后 continuation 再执行后续动作」。主设计 §5.8 的 `CloseAfterWriteAsync` 是 SAEA 自有的有界 drain，不宣称等同 DotNetty close。
5. **合理有意偏离，已在主设计标注：SAEA 冻结运行中链路、decoder 不累加、由执行器释放原始帧、terminal 处理任意 `IDisposable`、业务写入口与有界待写项预算、HTTP 400/413/431 策略。** 这些均可成为本项目设计，但不能描述成 DotNetty 的既有行为。
6. **HTTP 与 WebSocket 的关键差异：** DotNetty 原生 HTTP decoder 支持 chunked；413 来自独立 aggregator，且并非一律关闭；头部超限不会自动发送 431。WebSocket 官方握手则在运行中 remove/replace/add handler，恰好证明换链不是 DotNetty 所禁止的操作；SAEA 不换节点是为适配自身同步嵌套回调与所有权模型所作的独立选择。

结论分类：

- **事实错误**：初稿对固定提交中 DotNetty 行为的描述与源码直接冲突。
- **表述不严谨**：方向大致正确，但省略了会改变使用方式的边界、时序或 API 条件。
- **合理有意偏离**：与 DotNetty 不同，但可由 SAEA 的内存所有权、同步事件链、线程模型或协议策略独立论证。

以下仅作静态源码对照；未运行 DotNetty 或本项目代码。

## 二、Pipeline 方向与生命周期

| DotNetty 事实 | 初稿被审表述（非现行约束） | 结论 | 一手来源 |
|---|---|---|---|
| `DefaultChannelPipeline` 是双向链表；入站从 head 找下一个 inbound handler，出站从 tail 找前一个 outbound handler。`FireChannelRead` 从 head 开始，`WriteAsync` 从 tail 开始。 | §5.1「入站 head→tail、出站 tail→head」；§5.5 同向传播。 | **正确。** | [DefaultChannelPipeline.cs：head/tail 初始化](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/DefaultChannelPipeline.cs#L58-L69)、[DefaultChannelPipeline.cs：read 从 head、write 从 tail](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/DefaultChannelPipeline.cs#L785-L825)、[AbstractChannelHandlerContext.cs：入站 Next/出站 Prev](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/AbstractChannelHandlerContext.cs#L920-L939) |
| `ChannelRegistered` 在 register 完成后触发；`ChannelActive` 只在首次注册且已 active 时触发；关闭路径先执行 close，再触发 inactive 和 deregister。registered 不等于 active。 | §5.2 要拆分 registered/active/inactive/deregistered，并规定 active 前业务不可写。 | **方向正确。**「active 前业务不可写」是 SAEA 额外的业务闸门，不是 DotNetty 的统一公开写限制。 | [AbstractChannel.cs：registered 与首次 active](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/AbstractChannel.cs#L354-L388)、[AbstractChannel.cs：close 后触发 inactive/deregister](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/AbstractChannel.cs#L476-L569) |
| `CloseAsync` 先取得并置空 `outboundBuffer`，再执行 `DoClose0` 和关闭清理，随后触发 inactive/deregister。inactive 后调用写操作会进入 `AbstractUnsafe.WriteAsync`：buffer 已关闭时释放消息并返回 `ClosedChannelException` faulted Task。 | §5.2：「DotNetty `channelInactive` 里还能写，而 OnDisconnected 后 SAEA 底层已失效」。 | **事实错误。** inactive 回调中当然可以调用写 API，但不会形成有效写入；这与「还能写」的通常含义相反。SAEA 与 DotNetty 都必须在关闭后拒绝有效发送，差别不应写成 DotNetty 允许 inactive 后写。 | [AbstractChannel.cs：close 先失效 buffer 再发 inactive](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/AbstractChannel.cs#L476-L569)、[AbstractChannel.cs：关闭后写失败并释放消息](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/AbstractChannel.cs#L674-L710) |
| `FireExceptionCaught` 找后续 inbound handler；异常处理器自身再抛错会记录日志。outbound `WriteAsync` 的 handler 异常通过 faulted Task 返回，跨 executor 调度失败还会失败 promise 并释放消息；它不是一律转成入站 exceptionCaught。 | §5.5 要求异常统一入站传播，异步失败回灌 event loop。 | **表述不严谨。** 入站异常传播方向正确；但 outbound 写异常还以 Task/promise 失败为核心，不能概括为全部转入站。 | [AbstractChannelHandlerContext.cs：异常向下游传播与再抛错日志](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/AbstractChannelHandlerContext.cs#L401-L456)、[AbstractChannelHandlerContext.cs：写异常与跨 executor 失败释放](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/AbstractChannelHandlerContext.cs#L797-L918)、[同文件：SafeExecuteOutbound](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/AbstractChannelHandlerContext.cs#L956-L972) |
| tail 的未消费入站消息只自动调用 `ReferenceCountUtil.Release`，并记录丢弃日志；不会自动处理任意 `IDisposable`。 | §5.5 要求 terminal 兜底释放包括 `IDisposable` 在内的资源。 | **合理有意偏离。** SAEA 所有 `IMessage` 均实现 `IDisposable`，该兜底符合本项目资源模型，但不是 DotNetty 原样能力。 | [DefaultChannelPipeline.cs：tail 未消费消息只 release + log](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/DefaultChannelPipeline.cs#L967-L1017)、[ReferenceCountUtil.cs：仅对 IReferenceCounted 调用 Release](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Common/Utilities/ReferenceCountUtil.cs#L74-L87) |

## 三、Decoder 累加与引用计数

| DotNetty 事实 | 初稿被审表述（非现行约束） | 结论 | 一手来源 |
|---|---|---|---|
| `ByteToMessageDecoder.ChannelRead` 接管首个输入 buffer：无既有 cumulation 时直接把输入赋给 `cumulation`，后续输入由 cumulator 合并；最终在 finally 释放不可读的 cumulation，并派发输出。 | §5.3：「累加缓冲区生命周期归 decoder，业务层拿到的只是共享内存上的输出视图」。 | **前半句符合 DotNetty，后半句不是其通用规则。** DotNetty 的 `MessageToMessageDecoder` 输出并未强制为共享内存视图；它是泛型对象列表。SAEA 的共享数组视图是自身零拷贝设计。 | [ByteToMessageDecoder.cs：ChannelRead 接管/合并/释放输入](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs/ByteToMessageDecoder.cs#L177-L225)、[MessageToMessageDecoder.cs：输出为泛型列表](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs/MessageToMessageDecoder.cs#L19-L58) |
| 默认 `MergeCumulator` 会按需扩容并复制；旧 cumulation 在扩容后释放，后续输入在合并后释放。它并非「不累加」，也不由事件执行器统一释放所有原始帧。 | §5.3：SAEA 不累加，原始帧由事件执行器 finally 归还；跨事件数据必须复制。 | **合理有意偏离。** 该方案与 SAEA 回调内 Span 生命周期一致，但不能说成 DotNetty 的 decoder 也不累加。 | [ByteToMessageDecoder.cs：MergeCumulator 合并与释放](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs/ByteToMessageDecoder.cs#L17-L43)、[同文件：默认 cumulator 与扩容](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs/ByteToMessageDecoder.cs#L89-L90)、[同文件：ExpandCumulation](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs/ByteToMessageDecoder.cs#L142-L149) |
| `CallDecode` 将 decoder 产生的对象加入输出列表；`ChannelRead` 在 finally 逐条 `FireChannelRead`，**不 retain、不 release 输出对象**。`MessageToMessageDecoder` 同样只 release 输入，输出所有权转给下游。 | §5.3 第 8 条：「decoder 输出对象**不自动 release**，所有权归下游」。 | **正确。** SAEA 对共享输出逐条 `Retain` 是本项目的额外所有权协议，不应理解为 DotNetty 自动做了同样的 retain。 | [ByteToMessageDecoder.cs：输出 finally 派发且不释放输出](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs/ByteToMessageDecoder.cs#L204-L219)、[MessageToMessageDecoder.cs：释放输入并派发输出](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs/MessageToMessageDecoder.cs#L19-L58) |
| 固定提交没有名为 `decodeRemovalReentryProtection` 的字段。`CallDecode` 在每轮 `Decode` 后检查 `context.Removed`；`HandlerRemoved` 若仍有可读 cumulation，会转发残余字节，再触发 readComplete。 | §5.3：「运行中移除时靠 `decodeRemovalReentryProtection` 防护」。 | **事实错误（名称及机制均不准确）。** 可概括为「Removed 状态逐轮检查 + 移除时残余输入转发」，不能引用不存在的字段。 | [ByteToMessageDecoder.cs：HandlerRemoved 残余转发](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs/ByteToMessageDecoder.cs#L151-L171)、[ByteToMessageDecoder.cs：CallDecode 检查 Removed](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs/ByteToMessageDecoder.cs#L308-L354) |
| `ReferenceCountUtil.Release` 只对 `IReferenceCounted` 调用 `Release()`；`SafeRelease` 捕获并记录异常。底层 `AbstractReferenceCountedByteBuffer.Release` 使用原子计数，重复释放会抛 `IllegalReferenceCountException`，不是幂等 Dispose。 | §5.3 以共享所有权和 `Retain/Release` 管理输出；§4.5 要求清理幂等。 | **模型不同但可兼容。** 不应把 DotNetty 的 release 当作天然幂等；SAEA 若要求清理幂等，需要由自身 `IMessage`/资源包装另行保证。 | [ReferenceCountUtil.cs：Release/SafeRelease](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Common/Utilities/ReferenceCountUtil.cs#L74-L120)、[AbstractReferenceCountedByteBuffer.cs：非幂等原子释放](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Buffers/AbstractReferenceCountedByteBuffer.cs#L61-L91) |

## 四、Write / Flush / Close

| DotNetty 事实 | 初稿被审表述（非现行约束） | 结论 | 一手来源 |
|---|---|---|---|
| `WriteAsync` 经 outbound context 到 `AbstractUnsafe.WriteAsync`，只把消息和 promise 加入 `ChannelOutboundBuffer`；真正 socket 写发生在 `Flush` → `Flush0` → `DoWrite`。`WriteAndFlushAsync` 是先 Write 再 Flush，并返回该写的 Task。 | §5.6：write 只入队、flush 才发送；写完 resolve Task。 | **正确。** | [AbstractChannelHandlerContext.cs：Write/Flush/WriteAndFlush](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/AbstractChannelHandlerContext.cs#L797-L895)、[AbstractChannel.cs：WriteAsync 只 AddMessage，Flush 调 DoWrite](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/AbstractChannel.cs#L674-L777) |
| `ChannelOutboundBuffer.AddMessage` 保存消息和 promise；成功写完时 `Remove` 释放消息、完成 promise 并扣减待写字节。Task 完成表示本次 channel 写已处理，不表示对端已接收。 | §5.6 以写 Task 作为后续关闭 continuation 的依据。 | **正确，需保留完成语义边界。** | [ChannelOutboundBuffer.cs：AddMessage](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/ChannelOutboundBuffer.cs#L55-L84)、[ChannelOutboundBuffer.cs：Remove 完成 promise 并释放](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/ChannelOutboundBuffer.cs#L188-L220) |
| close 不等待待写队列自然排空。unsafe close 将 buffer 置空后，`FailFlushed`/`Close` 会失败并释放尚未完成的消息；`Flush0` 发现 channel 不可写时也会 fail flushed 并返回。 | §5.6：不能在关闭路径同步等待 Task，改用写完成 continuation。 | **结论正确，但理由应精确。** continuation 用于「先发完最后响应，再 close」；close 本身不是 drain 机制，也不保证此前所有写都成功。 | [AbstractChannel.cs：关闭时失败待写项](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/AbstractChannel.cs#L476-L549)、[AbstractChannel.cs：Flush0 不可写时失败 flushed 项](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/AbstractChannel.cs#L727-L777)、[ChannelOutboundBuffer.cs：FailFlushed/Close 释放并失败写 Task](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/ChannelOutboundBuffer.cs#L590-L680) |
| 官方 WebSocket 握手发送 101 使用 `WriteAndFlushAsync(response).ContinueWith(...)`，成功后才从出站移除 HTTP encoder；`CloseAsync` 则在 Close frame 的写 Task 完成后 close channel（该 continuation 未筛选成功状态）。 | §5.6：「DotNetty 用 `close(voidPromise)` 等待 close」。 | **表述不严谨。** 固定提交的公开 API 是返回 Task 的 `CloseAsync()`，没有 `close(voidPromise)` 这一 C# 公开重载；内部实际也是 `CloseAsync(...)`/`DoClose0(...)`。设计真正应对齐的是写 Task continuation 模式。 | [WebSocketServerHandshaker.cs：101 写完成后 continuation](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs.Http/WebSockets/WebSocketServerHandshaker.cs#L79-L133)、[WebSocketServerHandshaker.cs：Close 帧写完后关闭 channel](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs.Http/WebSockets/WebSocketServerHandshaker.cs#L217-L223)、[AbstractChannel.cs：CloseAsync/DoClose0](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/AbstractChannel.cs#L469-L567) |

## 五、运行中增加、删除、替换 Handler

| DotNetty 事实 | 初稿被审表述（非现行约束） | 结论 | 一手来源 |
|---|---|---|---|
| `AddLast` 先创建 context 并链接进双向链表，再按 executor 情况调用或调度 `CallHandlerAdded0`。若 added 失败，会 unlink，并尝试回调 `HandlerRemoved`，随后 `FireExceptionCaught`。 | §5.4：「先 `HandlerAdded` 成功再挂链」。 | **事实错误（若该句用于描述 DotNetty）。** 作为 SAEA 自有算法可以选择，但它不是 DotNetty 时序，也缺少 DotNetty 的「挂链后 added 失败再回滚」语义。 | [DefaultChannelPipeline.cs：AddLast 先链接再 CallHandlerAdded0](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/DefaultChannelPipeline.cs#L161-L201)、[DefaultChannelPipeline.cs：added 失败回滚](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/DefaultChannelPipeline.cs#L512-L551) |
| `Replace` 在原位置替换节点；先调用新 handler 的 `CallHandlerAdded0`，再调用旧 handler 的 `CallHandlerRemoved0`。源码注释明确说明新 Added 必须先于旧 Removed。 | §5.4：「旧 `HandlerRemoved` 再新 `HandlerAdded`」。 | **事实错误（若该句用于描述 DotNetty）。** 官方顺序是新 Added → 旧 Removed。 | [DefaultChannelPipeline.cs：Replace 与新 Added/旧 Removed 顺序](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/DefaultChannelPipeline.cs#L419-L475) |
| `Remove` 支持从 event loop 内同步移除，也从 loop 外提交任务；`Replace` 同样支持。WebSocket 官方握手实际执行 remove aggregator、replace decoder、add encoder，并在 101 写成功后 remove encoder。 | §5.4：「运行中只增不删，替换必须重新建链」；§5.9 HTTP→WS 重新建链。 | **合理有意偏离，但不是 DotNetty 限制。** 冻结链可简化 SAEA 同步嵌套回调的一致性，但论证必须基于本项目模型；不能用来解释 DotNetty 官方 WS 握手。 | [DefaultChannelPipeline.cs：Remove](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/DefaultChannelPipeline.cs#L344-L379)、[DefaultChannelPipeline.cs：Replace 调度](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/DefaultChannelPipeline.cs#L419-L475)、[WebSocketServerHandshaker.cs：运行中 remove/replace/add](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs.Http/WebSockets/WebSocketServerHandshaker.cs#L79-L133) |
| context 的 `HandlerState` 是 Init/Added/Removed；未注册或跨 executor 时，pipeline 会延迟或调度生命周期回调。设计若完全同步执行回调，行为会不同。 | §5.4 选择同线程执行生命周期回调、冻结运行链。 | **合理有意偏离，需自行证明回调重入与异常边界。** 它不是对 DotNetty 回调时序的复刻。 | [AbstractChannelHandlerContext.cs：HandlerState](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/AbstractChannelHandlerContext.cs#L181-L232)、[DefaultChannelPipeline.cs：添加时延迟/调度回调](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/DefaultChannelPipeline.cs#L161-L190)、[同文件：CallHandlerCallbackLater](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/DefaultChannelPipeline.cs#L926-L945) |

## 六、Backpressure

| DotNetty 事实 | 初稿被审表述（非现行约束） | 结论 | 一手来源 |
|---|---|---|---|
| 每次 `AddMessage` 按消息大小增加待写字节；达到高水位时立即设为不可写。每条写完成、失败或取消时扣减，降至低水位或归零时恢复可写。状态翻转触发 `FireChannelWritabilityChanged`。 | §5.7：待写字节数累计，高于高水位置不可写，低于低水位恢复。 | **核心正确，但触发时机需写准。** 高水位在 Write/AddMessage 路径检查，不是 Flush 才检查；低水位靠完成/失败/取消后的扣减恢复，不是调用一次 Flush 就必然恢复。 | [ChannelOutboundBuffer.cs：AddMessage 时累计](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/ChannelOutboundBuffer.cs#L55-L84)、[ChannelOutboundBuffer.cs：高低水位检查](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/ChannelOutboundBuffer.cs#L122-L163)、[同文件：写完成后扣减](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/ChannelOutboundBuffer.cs#L188-L215) |
| 默认高水位 64 KiB、低水位 32 KiB；状态变化通过 pipeline 的 `ChannelWritabilityChanged` 事件传播。DotNetty 不在 `WriteAsync` 内自动阻塞等待可写，调用方需观察 `IsWritable`/事件并自律。 | §5.7：业务可在 `OnWritableChanged` 恢复业务发送。 | **正确。** 恢复发送由业务触发，不是 pipeline 自动重放所有暂停写。 | [DefaultChannelConfiguration.cs：默认高低水位](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/DefaultChannelConfiguration.cs#L20-L27)、[ChannelOutboundBuffer.cs：状态翻转时发事件](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/ChannelOutboundBuffer.cs#L532-L576)、[DefaultChannelPipeline.cs：可写状态事件从 head 传播](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/DefaultChannelPipeline.cs#L801-L804) |
| DotNetty 的计数位于 outbound buffer；写仍先入队，没有设计所述的 terminal 写前闸门及事件执行器暂停。 | §5.7：WriteAsync 检查闸门；terminal 达高水位暂停事件执行器。 | **合理有意偏离。** 这是更强的背压策略，可能影响事件循环吞吐、关闭和控制事件，不能称为 DotNetty 原样机制。 | [AbstractChannel.cs：WriteAsync 无高水位等待/拒绝](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Transport/Channels/AbstractChannel.cs#L674-L710) |

## 七、HTTP：Chunked、413、431

| DotNetty 事实 | 初稿被审表述（非现行约束） | 结论 | 一手来源 |
|---|---|---|---|
| `HttpObjectDecoder` 原生识别 `Transfer-Encoding: chunked`，按 chunk-size、chunk、delimiter、footer 解码并产生 `HttpContent`/`LastHttpContent`；`HttpObjectAggregator` 再把 content 聚合成 full message。 | §5.8：SAEA 不支持 chunked，收到后显式 400。 | **合理有意偏离。** 必须标为 SAEA 协议能力裁剪；不是 DotNetty 的 decoder 行为。 | [HttpObjectDecoder.cs：进入 chunked 状态](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs.Http/HttpObjectDecoder.cs#L130-L159)、[HttpObjectDecoder.cs：chunk 与 footer 解码](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs.Http/HttpObjectDecoder.cs#L244-L325)、[MessageAggregator.cs：聚合 content 并输出 full message](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs/MessageAggregator.cs#L171-L241) |
| 413 由 `HttpObjectAggregator` 产生，而不是基础 decoder 自动产生。开始聚合前若 `Content-Length > maxContentLength`，调用 `HandleOversizedMessage`；聚合过程中累计超限时也进入同一 oversize 处理。 | §5.8：超过 maxBodySize 回复 413。 | **总体策略相近，但归属与时机需写准。** 只有 aggregator 做 body 上限与 413，decoder 本身没有该响应职责。 | [MessageAggregator.cs：Content-Length 早判与累计超限](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs/MessageAggregator.cs#L108-L203)、[HttpObjectAggregator.cs：Content-Length 判断及 413 实现入口](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs.Http/HttpObjectAggregator.cs#L56-L66) |
| 对 oversized request，若已是 full message，或未要求 100-continue 且非 keep-alive，则发送带 `Connection: close` 的 413 并在写完成后关闭；其他请求发送不带 close 的 413，只有写失败时才关闭。oversized response 则直接关闭。 | §5.8：413 后关闭。 | **合理有意偏离，但「DotNetty 语义」不能如此概括。** DotNetty 并非所有 413 都关闭；SAEA 可选更严格的 413 关闭策略。 | [HttpObjectAggregator.cs：413 响应及分支关闭条件](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs.Http/HttpObjectAggregator.cs#L151-L200) |
| 初始行或 header 超过配置上限会抛 `TooLongFrameException`；外层 Decode 捕获后生成带 `DecoderResult.Failure` 的 invalid message/chunk。基础 decoder 不自动写 431。 | §5.8：超过 header 上限回复 431。 | **合理有意偏离。** 431 是 SAEA 的显式协议映射，不是 DotNetty 原生自动响应。 | [HttpObjectDecoder.cs：解析异常转 invalid message](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs.Http/HttpObjectDecoder.cs#L85-L195)、[HttpObjectDecoder.cs：invalid message/chunk](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs.Http/HttpObjectDecoder.cs#L489-L525)、[同文件：header 上限与 TooLongFrameException](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs.Http/HttpObjectDecoder.cs#L830-L865) |
| HTTP decoder 默认 `maxInitialLineLength=4096`、`maxHeaderSize=8192`、`maxChunkSize=8192`，均可配置；解析状态机还有升级后残余字节转发。 | §4.6 规划 `HttpDecoderHandler`；§5.8 规划统一 400/413/431。 | **设计可简化，但能力清单不能与 DotNetty 对齐混写。** 尤其 chunked、aggregated/full message 与升级残余字节是不同职责。 | [HttpObjectDecoder.cs：默认上限与构造配置](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs.Http/HttpObjectDecoder.cs#L52-L83)、[HttpObjectDecoder.cs：升级后残余字节处理](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs.Http/HttpObjectDecoder.cs#L334-L344) |

## 八、WebSocket：升级、Continuation 与 Control Frame

| DotNetty 事实 | 初稿被审表述（非现行约束） | 结论 | 一手来源 |
|---|---|---|---|
| 官方 server handshaker 在握手期间调整现有 pipeline：移除 aggregator/compressor（若存在）、以 `NewWebsocketDecoder()` 替换 HTTP decoder、加入 `NewWebSocketEncoder()`；101 写成功后移除 HTTP encoder。 | §5.9：协议升级销毁 HTTP 链并按 WS 模板重建。 | **合理有意偏离。** 重建链可适应 SAEA 冻结策略，但官方做法是运行中原位换链，不能把重建说成 DotNetty 的必需步骤。 | [WebSocketServerHandshaker.cs：Handshake 换链](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs.Http/WebSockets/WebSocketServerHandshaker.cs#L79-L133) |
| decoder 用 `fragmentedFramesCount` 跟踪分片消息。Continuation 只能出现在已有分片消息中；非 Continuation 数据帧不能在分片尚未结束时开始。 | §5.9：WS 聚合 Continuation 后形成完整 Text/Binary。 | **正确。** 还应保留「Continuation 不能独立出现」这一协议约束。 | [WebSocket08FrameDecoder.cs：分片与 Continuation 校验](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs.Http/WebSockets/WebSocket08FrameDecoder.cs#L157-L180) |
| 控制帧必须 `FIN=1`、payload ≤125；控制帧走独立分支，可穿插在分片消息中。`WebSocketProtocolHandler` 自动把 Ping 转成 Pong 写回，Pong 默认不向应用传播。 | §5.9：Ping/Pong/Close 由 WS 层处理。 | **基本正确。** SAEA 若沿 DotNetty 语义，Ping 自动 Pong、Pong 默认不上抛，而不是作为普通应用消息继续传播。 | [WebSocket08FrameDecoder.cs：控制帧 FIN/长度校验](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs.Http/WebSockets/WebSocket08FrameDecoder.cs#L99-L156)、[WebSocketProtocolHandler.cs：Ping/Pong 处理](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs.Http/WebSockets/WebSocketProtocolHandler.cs#L23-L39) |
| `WebSocketProtocolHandler` 没有 Close 专用分支，Close frame 会 retain 后继续下发；server handshaker 提供的 `CloseAsync` 先写 Close frame，写 Task 完成后再 close channel。 | §5.9：Close「由会话层关闭」；§5.6 关闭采用写完成 continuation。 | **方向可成立，但需补协议动作。** 官方并未在通用 protocol handler 内自动完成 Close 握手；若使用 handshaker 的关闭路径，应先完成 WS Close 帧写，再关闭 TCP，不能只释放会话或直接断 socket。 | [WebSocketProtocolHandler.cs：Close 未特判而继续输出](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs.Http/WebSockets/WebSocketProtocolHandler.cs#L23-L45)、[WebSocketServerHandshaker.cs：Close 帧写后关 channel](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs.Http/WebSockets/WebSocketServerHandshaker.cs#L217-L223) |
| `WebSocketFrameAggregator` 只把 Text/Binary 识别为起始帧、Continuation 识别为 content；Ping/Pong/Close 等控制帧仍独立处理。 | §5.9：聚合 Continuation，应用主要收 Text/Binary。 | **正确。** 不应把所有 frame 无差别送入 aggregator。 | [WebSocketFrameAggregator.cs：聚合类型判断](https://github.com/Azure/DotNetty/blob/379d8cc1d32d2b557347aad1f7b6c48656212a4b/src/DotNetty.Codecs.Http/WebSockets/WebSocketFrameAggregator.cs#L11-L32) |

## 九、总体判断

### 9.1 可直接保留的 DotNetty 对齐结论

- 双向链表的入站/出站传播方向。
- write 入队、flush 实际写、写 Task 在完成路径 resolve。
- decoder 输出不自动 release，所有权交下游；输入与输出必须分开讨论。
- 高低水位基于待写字节数，状态变化通过独立事件通知。
- WebSocket Text/Binary + Continuation 聚合，控制帧独立处理。
- 关闭最后响应时采用写完成 continuation，避免在 event loop 上同步等待。

### 9.2 初稿中必须视为纠错、且不得重新引入的表述

- `channelInactive` 后仍可有效写入。
- 把「`HandlerAdded` 成功后才挂链」描述成 DotNetty 时序；SAEA 可把它作为自有算法，但必须明确归属。
- Replace 是旧 Removed 后新 Added。
- 当前 DotNetty 使用 `decodeRemovalReentryProtection` 字段。
- `close(voidPromise)` 是等待关闭的公开使用方式。
- DotNetty 的 413 一律关闭连接，或头部超限自动回复 431。

### 9.3 可以保留、但必须标为 SAEA 自有设计的部分

- 运行中冻结链，HTTP→WS 不增删链节点而由入口 handler 内部切换协议阶段。
- 不累加 decoder，原始帧由事件执行器归还，跨事件复制。
- 共享数组输出的 `Retain/Release` 与任意 `IDisposable` terminal 兜底。
- `IChannelPipeline.WriteAsync` 业务入口与 `MaxPendingWrites` 待写项数预算；不宣称采用 DotNetty 的高低水位或执行器暂停。
- 不支持 chunked 时显式 400、413 后关闭、头部超限 431。

这些偏离并非因此不合理；问题在于**设计依据的归属**。只有把「DotNetty 原样语义」与「SAEA 为同步事件链、零拷贝和协议裁剪所作的选择」分开，后续实现与验收才不会拿错误的基线互相校验。
