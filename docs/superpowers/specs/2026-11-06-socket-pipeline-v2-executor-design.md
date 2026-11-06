# SAEA.Sockets.Pipeline v2 第一批设计：执行器热路径

上游依据：`docs/superpowers/specs/2026-11-05-socket-pipeline-design.md` §8.4 的实测结论与 v2 优先项。

本批只解决执行器的每消息机构成本，不动分帧、编解码、handler 契约、公开 API 签名与选项默认值。

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

## 八、实测结果（2026-11-06，Windows / .NET 10.0.403，Release）

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