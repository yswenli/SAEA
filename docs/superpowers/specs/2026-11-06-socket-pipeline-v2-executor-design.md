# SAEA.Sockets.Pipeline v2 设计：执行器热路径与单拷贝接收泵

上游依据：`docs/superpowers/specs/2026-11-05-socket-pipeline-design.md` §8.4 的实测结论与 v2 优先项。

- 第一批（§一–§八）：执行器每消息机构成本，不动分帧、编解码、handler 契约、公开 API 签名与选项默认值。
- 第二批（§九–§十）：接收泵去掉 `FrameAccumulator`，入站拷贝从 2 次降到 1 次，分帧输入改为 `ReadOnlySequence<byte>`。

## 一、问题

v1 实测完整接收路径约 76–84 万帧/s（旧栈 5.6–6.2%）、约 1.2KB/帧、约 6 次对象分配/帧。每帧固定成本来自执行器：

1. 每消息 `Task.Run`：一次线程池排队，再加一次调度唤醒；
2. 每消息 `TaskCompletionSource` 闸门：`Task.Run` 内先 `await gate.Task` 才真正开始执行；
3. 每消息 `_inFlight` 字典条目（`ConcurrentDictionary` 写入）；
4. 每消息 `CancellationTokenSource.CreateLinkedTokenSource(_connectionToken)`；
5. 每消息 `AsyncLocal<CancellationToken>.Value` 赋值与恢复。

同步完成的 handler（演示与基准里的计数 handler）走完这五步却只做了几纳秒的实际工作，因此排队开销决定了吞吐上限。

## 二、变更

### 2.1 内联启动（替代 `Task.Run` + 闸门）

- 消费循环取得并发额度后**直接调用** `ExecuteMessageAsync(msg, id)` 且不 `await`。
- 消息同步完成时：返回已完成的 `Task`，不分配 `Task`、不进线程池，执行发生在消费协程的栈上。
- 消息挂起时（`await` 未同步完成）：由 `async` 状态机自带的那一个 `Task` 承担，登记进 `_inFlight` 供收口等待。
- 登记竞态：`ExecuteMessageAsync` 可能在登记前就已跑完 `finally` 并 `TryRemove`（此时是空操作）。为避免残留已完成条目，登记后再复查一次 `IsCompleted` 并移除。

### 2.2 令牌来源按需创建

- `MessageTimeout == TimeSpan.Zero`（默认）：消息令牌**就是** `_connectionToken.Token`，不建 linked CTS，也不写 `AsyncLocal`；`CurrentToken` 在无注入值时本来就回落到连接令牌，语义不变。
- `MessageTimeout > Zero`：保留 v1 行为（linked CTS + `CancelAfter` + `AsyncLocal` 注入 + `finally` 恢复）。
- 超时判定同步收紧为「存在 CTS 且其已取消且连接令牌未取消且未在收口」，避免无超时路径误判。

## 三、必须保持的不变量

- 并发上限仍为 `MaxConcurrentMessages`，额度由消费循环通过信号量获取，在该消息的 `finally` 中释放。
- 单条消息内部严格串行（解码 → 鉴权 → 路由）；响应顺序不等于接收顺序。
- 收口四步、两种 `DisconnectDrainPolicy`、幂等入口、`CancelPending` 取消在途消息全部不变。
- 超时映射 `MessageTimeoutException`、只 `Warn` 不断连。
- 队列满背压与低水位恢复、`ChannelReadCompleteAsync` 语义、`PooledMessage` 引用计数都未改动。

## 四、已知行为差异

1. **同步阻塞的 handler 会占用消费协程**：v1 用 `Task.Run` 隔离，v2 直接在消费协程上同步执行。异步 `await`（如 20–50ms 鉴权）不受影响，仍可并发；同步阻塞或 CPU 密集的 handler 会推迟同连接后续帧，需要自行 `Task.Run` 或改成异步。
2. **同步完成的 handler 不再并行到线程池**：多核并行变为单协程顺序执行。这是"去掉调度开销"的对价，吞吐提升来自不再排队，而不是并行度。
3. 无超时配置时，handler 拿到的 `ctx.CancellationToken` 就是连接令牌（同一个令牌值），因此连接收口取消会直接反映到在途消息，这一点与 v1 等价。

## 五、度量口径

沿用 `Src/SAEA.Sockets.PipelineTest --bench`：500000 帧、body 256B、64KB 分块喂入，每条路径预热 1 轮后取 5 轮中位数；比较改造前后的帧/s 与 B/帧，并回填 v1 设计文档 §8.4。

## 六、风险与缓解

| 风险 | 影响 | 缓解 |
|---|---|---|
| 同步长阻塞 handler 拖慢同连接 | 首帧延迟、吞吐下降 | XML 注释与 README 明写；`Acceptance3_SlowAuth...` 守住异步并发；`--bench` 记录同步路径表现 |
| 挂起后 `_inFlight` 登记竞态 | 收口等待到已完成的条目，或残留条目 | 登记后复查 `IsCompleted` 并移除；收口以计数为零为完成条件 |
| 无 CTS 路径取消语义被误改 | 超时不生效或连接取消丢消息 | 断言「无超时时消息令牌 == 连接令牌」与「连接取消仍能终止在途消息」 |
| 内联执行改变同连接顺序保证 | 用例回归 | 保持 `MaxConcurrentMessages == 1` 时严格按接收顺序；现有并发用例覆盖 |

## 七、验收

1. 现有用例全绿（Debug 160 / Release 158），`--all`、`--socket`、`--tls` 保持通过。
2. 新增用例覆盖无超时令牌等价、内联启动下慢鉴权并发、`CancelPending` 取消在途。
3. `--bench` 前后对照写入 v1 设计文档 §8.4。

## 八、第一批实测结果（2026-11-06，Windows / .NET 10.0.403，Release）

5 次独立进程运行、每次 500000 帧、`MaxConcurrentMessages = 8`：

| 路径 | v1 | v2 第一批 | 每帧分配 |
|---|---|---|---|
| 旧栈直连回调 | 约 1370 万帧/s | 约 1290–1360 万帧/s | 约 0–1B |
| 分帧 + `EnvelopeCodec` 解码 | 约 1700 万帧/s | 约 1170–1660 万帧/s | 约 312B |
| 接收泵 + 入队 + 处理器 | 约 76–84 万帧/s（旧栈 5.6–6.2%） | 约 197–317 万帧/s（中位数 267 万，旧栈 21.4%） | 约 475–480B |

结论：

- 内联启动 + 令牌按需创建生效：完整接收路径提升到 2.4–3.8×，每帧分配从约 1.2KB 降到约 480B。
- 仍未达到"吞吐下降不超过 15%"的目标，剩余成本是每帧 2 次拷贝、`PooledMessage` 与队列节点、以及每帧一条异步链。
- 基准 handler 是同步的，改造后该路径单协程串行执行（v1 靠线程池并行摊到多核），所以提升来自"不再排队"，不是并行度。多核同步 handler 场景若要更高吞吐，需另起固定 worker 循环方案，代价是每连接常驻 `MaxConcurrentMessages` 个任务。
- 无超时路径的令牌等价已由 `Token_NoMessageTimeout_UsesConnectionTokenDirectly` 用例守住。

## 九、第二批：接收泵单拷贝

### 9.1 问题

第一批之后，入站每帧仍要拷两次：① `Pipe` 缓冲 → `FrameAccumulator.Append`（`AdvanceTo(buffer.End)` 前必须把字节搬出 Pipe）；② 累加器 → `PooledMessage`（队列条目要跨 `await`）。累加器还带来扩容搬运、一次额外租借，以及"残字节必须搬进自己的缓冲"的固定成本。

### 9.2 变更

1. **分帧输入改为序列**：`IFrameSplitter.TrySplit` 签名变为
   `bool TrySplit(ReadOnlySequence<byte> buffer, out long consumed, out ReadOnlySequence<byte> frame)`，
   `consumed` 由 `int` 放宽到 `long`，不保留 `ReadOnlyMemory` 重载。
2. **四个分帧器改序列实现**：跨段长度字段先并入 8 字节栈缓冲再按字节序解析（不产生堆分配）；分隔符按段扫描并对跨段边界逐字节比对；行分帧的帧尾 `\r` 判定取帧尾单字节切片；定长分帧直接引用原序列。三处分帧器不依赖 `SequenceReader<T>`：`netstandard2.0` 引用程序集里该类型不可见（实测编译报 CS0246）。
3. **执行器删除累加器**：`ChannelPipelineExecutor` 不再持有 `FrameAccumulator`；`PumpAsync` 直接在 `ReadResult.Buffer` 上循环切帧，消费位置用 `SequencePosition` 推进；队列满时该帧不消费、暂停期间不读新数据，残字节留在 `Pipe` 交给下一次 `ReadAsync`；`AllocateFrame` 直接接收 `ReadOnlySequence<byte>`，只做 1 次拷贝；`Traffic().BufferedBytes` 改由新的 `_bufferedBytes` 提供，每次 `AdvanceTo` 后写入未成帧残余长度。
4. **纯链式保留累加器**：没有 executor 时由 `FrameSplitterHandler` 在链首分帧，它仍用累加器保存半包，把连续缓冲包成单段 `ReadOnlySequence` 传入新签名，语义与拷贝数不变。`FrameAccumulator` 因此仍留在包内，但只服务这条路径。

### 9.3 必须保持的不变量

- 队列满时该帧不得 `Consume`；`Pause` 到 `Resume` 之间不得调用 `ReadAsync`。
- `ChannelReadCompleteAsync` 每次成功排空的 `ReadAsync` 至多回调 1 次，被截断的轮次顺延合并。
- EOF、`CancelPendingRead`、异常路径都必须 `AdvanceTo` 放行缓冲，不能让 `PipeReader` 停在未消费状态。
- 分帧器返回 `consumed <= 0` 视为协议错误，抛 `FrameProtocolException`，避免原地打转。
- `frame` 切片只在当次 `buffer` 有效期内可读，调用方必须先复制成独立背衬再消费缓冲。

### 9.4 已知行为差异

1. `MaxBufferedBytes` 的度量对象从"累加器里的残余"变成"`Pipe` 里的残余"，数值口径与 `ChannelTraffic.BufferedBytes` 一致；超限仍以 `reason = "buffered-bytes-limit"` 关闭连接。
2. 未成帧残字节不再被搬进包内缓冲，内存占用改由 `Pipe` 的 `pauseWriterThreshold` 与 `MaxBufferedBytes` 共同约束，不再有累加器的两倍扩容路径。
3. 纯链式路径（`FrameSplitterHandler`）行为与拷贝数不变，仍是 2 次拷贝。

## 十、第二批实测与分配归因（2026-11-06，Release）

5 次独立进程运行、每次 500000 帧、body 256B、`MaxConcurrentMessages = 8`：

| 路径 | v2 第一批 | v2 第二批 | 每帧分配 |
|---|---|---|---|
| 旧栈直连回调 | 约 1290–1360 万帧/s | 约 1297–1401 万帧/s | 约 0–1B |
| 分帧 + `EnvelopeCodec` 解码 | 约 1170–1660 万帧/s | 约 950–2001 万帧/s | 约 312B |
| 接收泵 + 入队 + 处理器 | 197–317 万帧/s（中位数 267 万，旧栈 21.4%） | 227–268 万帧/s（中位数 232 万，旧栈 17.4%） | 478–480B |

结论：

- 拷贝数按设计从每帧 2 次降到 1 次，由代码结构与 `SplitterParityTests`（把同一批字节构造成多段 `ReadOnlySequence` 后与 `BaseCoder.DecodeStream` 逐字段比对）守住。
- 吞吐与分配都在测量噪声范围内：两次采样的区间重叠（197–317 万 vs 227–268 万），分配 475–480B 对 478–480B。说明这条路径的瓶颈不是这次 `memcpy`，而是每帧一次精确数组分配与随后的 GC、以及每帧一次异步派发。
- "吞吐下降不超过 15%"仍未达成（中位数 17.4%）。

分配归因（265B 帧，隔离测量，不依赖 `--bench`）：

| 组成部分 | 每帧 | 说明 |
|---|---|---|
| `byte[265]` 精确数组 | 296B | `SmallFrameInlineThreshold = 512` 时小帧走 `ToArray()`，不租池 |
| `PooledMessage` | 56B | 队列条目对象本身 |
| 有界队列 `TryWrite`/`TryRead` | 0B | 通道不按条目分配节点 |
| `WaitToReadAsync` 消费循环 | 0B | 复用同一个等待对象 |
| `FireChannelReadAsync` 派发 | 0B | 同步完成，不分配状态机 |
| `ExecuteMessageAsync` 路径 | 0B | 同步完成，`PooledMessage` 之外无额外分配 |
| 基准夹具 64KB 分块写 `Pipe` | 约 128B | `BufferSegment` 采样占比约 27%，属传输层/夹具成本，不是管线开销 |

因此管线自身约 352B/帧，与 `--bench` 的 478–480B 之差来自 `Pipe` 段分配。下一批若要继续压降，只有三个方向：小帧改租池（换取归还成本与碎片）、`PooledMessage` 池化复用、或让解码层直接消费 `Pipe` 序列（把 `PooledMessage` 的生命周期交给 `Pipe`）。本批只度量，不改代码。