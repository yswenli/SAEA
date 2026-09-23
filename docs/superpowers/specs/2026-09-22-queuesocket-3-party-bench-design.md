# SAEA.QueueSocket 三端性能测试与优化设计文档

**日期：** 2026-09-22
**范围：** `SAEA.QueueSocketTest` 新增无头功能回归 + 三端（Producer/QServer/Consumer）性能基准；`SAEA.QueueSocket` 热路径优化（公共 API 签名不变）
**方案：** 方案 A —— 进程内三端 harness + 就地小步优化（分层基准：微基准精确测 B/op，端到端测吞吐）

---

## 概述

`SAEA.QueueSocket` 目前只有一个**交互式**测试工程 `SAEA.QueueSocketTest`（菜单 `s/p/c/t` 与硬编码 5×5、`127.0.0.1:39654`、30s 的 `HighConcurrencyTest`），既无法在 CI 中断言功能正确，也无法量化性能。同时其编解码/接收/批处理热路径存在明显的逐帧分配：

- `QClient.cs:189` / `QServer.cs:123`：收到 `ReadOnlySpan<byte>` 先 `.ToArray()`，再被 coder 拷进内部缓冲 → 每帧双拷贝。
- `QueueCoder.cs:392` `ReadInt32`：`temp.ToArray()`，每帧读 4 个 int 就分配 4 次数组（注释声称「避免堆分配」，与实现相反）。
- `QueueCoder.Encode`（`:204`）：`List<byte>` + 4×`BitConverter.GetBytes` + `ToArray`，单条消息 6+ 次分配。
- `QueueCoder.GetQueueResult`（`:101`）：先把帧解成 `QueueSocketMsg` 再逐条复制成 `QueueMsg`，双对象。
- `QClient._batcher_OnBatched`（`:214`）：`new List<byte>()` + 逐条 `AddRange` + `ToArray`。
- `Exchange.GetSubscribeData`（`:212`）：每批 `subs.ToArray()` + `new List<byte[]>` + 逐条 `coder.Data(...)` 编码后再交 batcher 拼接。

本设计在**不改公共 API 签名**的前提下：① 给 `SAEA.QueueSocketTest` 增加无头功能回归与三端基准；② 逐项优化上述热路径，并以功能回归作护栏、微基准作前后对比。

**兼容性：** 无破坏性；`Producer`/`Consumer`/`QServer`/`QClient`/`QueueMsg` 的 public 签名不变（新增 `internal` 成员不属公共面）。
**非目标：** 多进程真实三端 harness、`ArrayPool` 深度池化重写、不引入新测试工程、不改线格式、不改批处理/心跳/断开时序。

---

## 一、关键事实核对（实现前已验证）

| 事实 | 结论 / 出处 | 对设计的影响 |
|------|------------|-------------|
| 测试工程现状 | `Src/SAEA.QueueSocketTest`：`net10.0`、`LangVersion 8.0`、`OutputType=Exe`、`StartupObject` 空；仅 `Program.cs`（交互菜单）与 `HighConcurrencyTest.cs` | 单工程改造，保留菜单 |
| 库目标框架 | `SAEA.QueueSocket` 为 `netstandard2.0`；测试工程 `net10.0` | 库内禁用 ns2.1+ API；测试可用现代 API |
| 传递引用 | `SAEA.QueueSocketTest` → `SAEA.QueueSocket` → `SAEA.Common`（`ConsoleHelper`/`TaskHelper`） | 无需新增包引用 |
| `ConsoleHelper` 语义 | 异步队列输出，退出前需留刷出时间 | `Main` 用 `async Task`，退出前 `await Task.Delay(500)` |
| `QServer` 生命周期 | `QServer(port=39654, ip, bufferSize, maxConnects, maxPendingMsgCount)`；`Start(backlog=10000)`/`Stop()`/`Status`；`OnDisconnected(id, ex)`；`Clear(id)`（仅清 binding） | 测试用自由端口；**断线清理需在 `QServer` 内调 `_exchange.SessionClosed(id)`（本次修复 M1）**，`Clear` 不清 `_subscribers` |
| `QServer.CalcInfo` 泄漏 | 内部 `TaskHelper.LongRunning(() => while(true){...})` 永不退出 | 测试/基准**不调用** `CalcInfo`，自采计数 |
| `Producer` 语义 | ctor 内 `new QClient(name, ipPort)` 并立即 `Connect()`；`OnMessagesSent(int)`；`Dispose()`→`QClient.Close()` | 先起服务器再建 Producer |
| `Consumer` 语义 | ctor 不连接；`Subscribe(topic)` 仅记 topic；`Start()` 才 `Connect()`+发订阅+启动清理任务；`OnMessage(Action<QueueMsg>)`；`Dispose()` 取消清理任务并断开 | 先 `Subscribe` 再 `Start`，务必 `Dispose` |
| 投递语义 | `Exchange`/`MessageQueue` 为**队列语义**：订阅前发布的消息缓冲在 topic 队列，订阅者出现后投递 | 功能用例不假设「迟到订阅者收不到历史消息」 |
| 心跳任务 | `QClient.HeartAsync` 为长跑任务，`Close()` 置 `_isClosed` 后退出 | 每个客户端必须 `Dispose/Close` |
| 线格式（QueueSocket） | `type(1) + total(4) + nameLen(4) + name + topicLen(4) + topic + data`，小端；`total = 12 + nameLen + topicLen + dataLen`；`MIN = 1+4+4+0+4+0+0 = 13` | O3/O5 必须逐字节保持；FT9 守 |
| 分类批处理器单例 | `Exchange` ctor 用 `ClassificationBatcher.GetInstance(5000,100)`（进程级单例，`Exchange.cs:105`/`ClassificationBatcher.cs:67`），`OnBatched +=` 且 `Dispose` 不退订 | **全程仅建 1 个 `QServer` 并复用**（用例用唯一 topic 隔离），避免 handler 叠加与每会话批处理线程泄漏 |
| 退订键不匹配 | `Exchange.Unsubscribe` 按 `sInfo.Name` 删除，但 `_subscribers`/`_binding` 键为会话 ID；`SessionManager.cs:110` `ut.ID = RemoteEndPoint.ToString()` ≠ 客户端 name | 属实现 bug；本次修正为按会话 ID 退订（`ReplyUnsubscribe` 传 `ut.ID`），FT4 方可真实验证 |
| 关闭丢未批出数据 | `QClient.Close` 先退订 `OnBatched` 再 `Dispose`；`Batcher.Dispose` 先 `Clear()`（丢弃）后 flush | 无损用例/基准必须 `WaitUntil(sent==N)` 排空后再 `Dispose()` |
| 既有基准范式 | `Src/SAEA.P2PTest/TestHarness.cs`（`Section/Expect/Throws/WaitUntil/GetFreeTcpPort/WriteSummary/PassCount/FailCount`）；`Tests/IocpBenchmark.cs`（warmup + `GC.GetTotalAllocatedBytes` + 多模式 + `[PASS]` 输出） | 直接照搬范式 |
| 既有测试基线 | 本轮实跑：`SAEA.P2PTest -- --all` = **310/310 passed**；`-- --bench-iocp` = **29/29 passed** | 回归门禁基线 |
| 端口冲突 | `GetFreeTcpPort()` 已存在（bind 0 后取端口） | 整套共用一个自由端口（唯一 QServer 全程复用） |

---

## 二、目标 / 范围 / 硬不变量

### 2.1 目标

1. `SAEA.QueueSocketTest` 支持无头 CLI：`--all`（功能回归 + 基准）、`--functional`、`--bench-queue`，返回退出码。
2. 覆盖 Producer/QServer/Consumer 全链路功能回归（无损、广播、隔离、退订、分帧、突发、断线、重连、编解码往返）。
3. 提供分层基准：微基准精确 B/op，端到端三端吞吐 + 系统级 B/frame，带防回归硬断言。
4. 优化 O1–O6 热路径，公共 API 签名不变，线格式不变，功能不回退。
5. 修正内部实现缺陷：`Exchange.Unsubscribe` 改按会话 ID 退订（B1）、`QServer` 断线调 `Exchange.SessionClosed` 清理订阅（M1）；公共 API 不变。
6. 修正既有并发缺陷（无 FT6 无损前提）：`MessageQueue.Enqueue` 原子化（`GetOrAdd`，消除孤儿队列）、`Exchange` 每 topic 仅启动一个分发任务（`Lazy<Task>`）、分发内层改非阻塞 `TryDequeue` + 短延迟排空。详见 2.4。

### 2.4 本轮修复的既有并发缺陷（先于功能性回归验证）

FT6（3 生产者 × 100 → 2 消费者，无损）在原实现下稳定丢包（收到 149–298/300）。根因：**#1** `MessageQueue.Enqueue` 在 `TryGetValue` 未命中时 `new FastQueue` 后 `TryAdd`，竞态失败者消息进入孤儿队列永不投递；**#2** `Exchange` 用 `_dispatchTasks.GetOrAdd(topic, factory)`，工厂竞态下被多次执行 → 同 topic 出现 2 个分发任务；**#3** 内层先 `GetCount>0` 再阻塞 `DequeueAsync()`，抢输者永久卡在 `WaitToReadAsync()` 并扣留已积累批次。

修复：`FastQueue.TryDequeue(out T)`（加法性）、`MessageQueue` 用 `_dic.GetOrAdd(topic, ...)` 原子入队并新增 `TryDequeue`、`Exchange` 改用 `ConcurrentDictionary<string, Lazy<Task>>`（`LazyThreadSafetyMode.ExecutionAndPublication`）保证任务唯一、内层改非阻塞轮询（保留 `batchSize=1000`/`maxWaitTime=50ms`）。`Exchange` 的「订阅者注册 + 确保分发器」「分发器退出 + 移除条目」以及 `Unsubscribe`/`SessionClosed` 的订阅字典改动统一在 `_syncLocker` 下串行化（热排空循环保持无锁），消除重订阅时任务被提前移除/无分发器的竞态。公共 API 除新增 `public TryDequeue` 外签名不变；后续 O6 优化须保留该结构。

### 2.2 范围内 / 范围外

**范围内**

| 领域 | 处理方式 |
|------|----------|
| `Src/SAEA.QueueSocketTest/` | 新增 `TestHarness.cs`、`QueueServerHarness.cs`、`FunctionalTests.cs`、`QueueBenchmark.cs`；`Program.cs` 加 CLI 分发 |
| `Src/SAEA.QueueSocket/Net/QueueCoder.cs` | O1 `ReadInt32`、O2 内部 span 重载、O3 `DecodeTo`、O5 `Encode` 单分配 |
| `Src/SAEA.QueueSocket/QClient.cs` | O2 接收去 `ToArray`、O4 batcher 单趟 |
| `Src/SAEA.QueueSocket/QServer.cs` | O2 接收去 `ToArray`；M1 断线调 `Exchange.SessionClosed` |
| `Src/SAEA.QueueSocket/Model/Exchange.cs` | O6 分发降分配；B1 `Unsubscribe` 改按会话 ID |

**范围外**

- 多进程三端 harness（`--server`/`--producer`/`--consumer` 编排）。
- `ArrayPool` 深度池化 / `QClient.Publish` 的二次 `Encoding.UTF8.GetBytes`。
- 新测试工程、新 NuGet 依赖、线格式变更、批处理/心跳时序变更。
- 修改 `SAEA.Sockets`/`SAEA.P2P`。

### 2.3 硬不变量（验收必须逐条成立）

1. 线格式字节不变：`type(1)+total(4)+nameLen(4)+name+topicLen(4)+topic+data`，`total=12+nlen+tlen+dlen`。
2. `Producer`/`Consumer`/`QServer`/`QClient`/`QueueMsg` 的 **public 成员签名不变**（仅新增 `internal`）。
3. 批处理语义不变：`Batcher` 合并后的字节流与原逐条拼接等价。
4. 心跳/断开/发送完成时序不变。
5. 无新增 `//` 行内注释（`///` XML doc 允许）。
6. 既有 `SAEA.P2PTest -- --all`（310/310）与 `-- --bench-iocp`（29/29）不回退。

---

## 三、架构与工程结构

**单一测试工程**：继续用 `Src/SAEA.QueueSocketTest`（net10.0），不新增项目。保留现有交互菜单为默认行为（无参/`s`/`p`/`c`/`t`/`sc`/`sp`/`a` 均不变）。

**CLI 分发**（`Program.Main` 最前面判参，命中即执行后 `return`，并设 `Environment.ExitCode`）：

| 参数 | 行为 |
|------|------|
| `--all` | 功能回归 + 基准；`ExitCode = HasFailures ? 1 : 0` |
| `--functional` | 仅功能回归 |
| `--bench-queue` / `--bench` | 仅基准（强制 §5.3 阈值） |
| `--bench-queue-baseline` | 仅基准；记录 B/op 与吞吐但不强制阈值，`ExitCode=0`（用于校准备份） |
| 无参 / 其他 | 落入现有交互菜单 |

**新增文件**（均在 `Src/SAEA.QueueSocketTest/`，namespace `SAEA.QueueSocketTest`）：

| 文件 | 职责 |
|------|------|
| `TestHarness.cs` | 移植 `SAEA.P2PTest/TestHarness.cs`：`Section/Expect/Expect(bool,string,string)/Throws<T>/WaitUntil/GetFreeTcpPort/WriteSummary/PassCount/FailCount/TotalCount/HasFailures/Reset` |
| `QueueServerHarness.cs` | 测试装配器：`GetFreeTcpPort()` 起**唯一** `QServer(port)`；创建/销毁 `Producer`/`Consumer`；包装计数器与 `WaitUntil` 辅助；统一 start/teardown；**不调用 `CalcInfo`** |
| `FunctionalTests.cs` | `RunAll()` 跑 FT1–FT9 |
| `QueueBenchmark.cs` | `RunAsync()` 跑微基准 + 端到端基准 |

**端口**：一律 `TestHarness.GetFreeTcpPort()`；`Producer`/`Consumer` 传 `127.0.0.1:{port}`。
**服务器复用**：整个进程（`--functional` 或 `--bench-queue` 的全程）只创建 **1 个 `QServer`**（自由端口），所有用例共用；用例间以**唯一 topic** 隔离，teardown 只释放客户端，不重建服务器（规避 `ClassificationBatcher` 进程级单例导致的 handler 叠加与线程泄漏）。
**计数**：从生产者 `OnMessagesSent` 与消费者 `OnMessage` 两端取，不依赖服务端 `Exchange` 计数。

---

## 四、功能回归用例（`FunctionalTests.RunAll()`）

整套共用**单个 `QServer`**（自由端口），每个用例使用**唯一 topic** 隔离，并各自创建/释放客户端；跑完即 teardown 客户端（不重建服务器）；一律 `WaitUntil` 轮询而非固定 sleep；消费者回调内校验负载并 `obj.Dispose()`；每个用例 try/catch 包住，异常记 `Expect(false, name, ex.Message)`，单条失败不中断整套。无损类用例（FT1/FT2/FT6）需先 `WaitUntil(() => sent == N)` 排空生产者批处理再 `Producer.Dispose()`。

| 编号 | 名称 | 断言 |
|------|------|------|
| FT1 | 单发单收·无损 | 消费者先订阅，生产者发 N=500 条唯一负载；收到数 == N，集合无重复、无丢失、内容逐条相等 |
| FT2 | 同 topic 广播 | 3 消费者订阅同一 topic，发 N=300；每个消费者都收到全部 N 条 |
| FT3 | 多 topic 隔离 | cA 订阅 `t1`、cB 订阅 `t2`，分别发 N 条；cA 只收 `t1`、cB 只收 `t2`，无跨 topic 泄漏 |
| FT4 | 取消订阅生效 | 先收发热身并排空；经 `QClient.Unsubscribe(topic)`（**不断开连接**）后再发 N 条，断言取消后计数不再增长（留 settle 超时）。**依赖 B1 修正**，否则服务端退订为 no-op |
| FT5 | 大消息 / 分帧重组 | 负载 1B、64B、4096B、64KB；逐条断言字节完全一致 |
| FT6 | 突发无损 | 3 生产者 × 100 = 300 发到 2 消费者；每个消费者收满 300，无重复、无丢失 |
| FT7 | 断线清理 | 服务端 `OnDisconnected` 计数；消费者 `Dispose()` 后断言服务端在超时内观测到断开 |
| FT8 | 重连可用 | 旧消费者断开后新消费者接入同 topic，能收到后续新消息 |
| FT9 | 编解码往返 | 多种 name/topic/负载尺寸下 `Encode`→`Decode` 的 type/name/topic/data 与 offset 完全一致 |

**语义说明**：`Exchange`/`MessageQueue` 为**队列语义**——订阅前发布的消息会缓冲并在订阅者出现后投递，故 FT 不假设「迟到订阅者收不到历史消息」；FT4 用热身 + settle 规避在途批次的抖动，且必须用**未断开连接的 `QClient`** 发退订，避免「断开后自然收不到」的假通过。名称/topic 由 `Encode` 经 `Encoding.UTF8` 编码、`Decode` 还原，FT9 覆盖空/非空/多字节。

---

## 五、基准设计（`QueueBenchmark`）

**两层：微基准测精确 B/op，端到端测三端吞吐。**

### 5.1 Tier 1 — 微基准（无 socket）

| 名称 | 说明 |
|------|------|
| `MicroEncode` | 循环 `QueueCoder.Encode(new QueueSocketMsg(Publish, "producer", "bench", payload))`，warmup 2000 + 计 N=20000，结果丢弃 |
| `MicroDecode` | 预构造单帧 `byte[]`，循环调用公开的 `GetQueueResult(byte[])`（`byte[]` 精确匹配该重载，非新增 span 重载；每次消费完一帧） |
| `MicroDecodeBatch` | 预构造 K=100 帧拼接缓冲，一次解码后按帧摊薄 |

- 负载：64B / 1KB / 4KB。
- 采集：`GC.GetTotalAllocatedBytes(precise:true)` 差值 ÷ N = **B/op**；`Stopwatch` = ns/op；`GC.CollectionCount(0/1/2)`。
- 输出仿 `IocpBenchmark`：`[micro] <name> | <ns/op> | <B/op> | GC0=x`。

### 5.2 Tier 2 — 端到端三端（真实 loopback TCP，自由端口）

- 场景参数化：`S1` = 1P/1C/1topic/64B/10s；`S2` = 5P/5C/1topic/1KB/10s。
- **带流控的无损口径（Task 4 实测勘误，用户批准）**：`Exchange` 分发经进程级单例 `ClassificationBatcher`，其 `Insert` 在队列达到 `_max = size×10 = 50000` 时**静默丢弃**（`Batcher.cs:80-88`）。因此端到端基准不再「无背压饱和发布」，而是对生产者施加在途窗口流控：以「已发布数 − 最慢消费者已收数」为在途量，超过 `maxOutstanding=8192` 即短暂让出，从而在**无损前提**下测最大可持续吞吐。计数：发布侧用本地精确计数（每次 `Publish` 后自增），消费者计数取 `OnMessage`；无损断言保持硬性（`every consumer received all` 且 `totalDeliveries >= published`），不再用 0.99 容忍。
- 到时长 → 停生产者 → `WaitUntil` 所有消费者追上已发布数 → 断言**无损**。
- 指标：**吞吐 = published ÷ 发布阶段耗时**（系统入站吞吐，广播场景不再按投递数重复计数）；系统级 **B/frame** = `GC.GetTotalAllocatedBytes` 差 ÷ 总投递数（`sum(counts)`）；Gen0/1/2。**延迟 p50/p99 本轮不测**：`Producer.Publish` 仅接受 `string`，内嵌 8B 时间戳需把二进制塞进 UTF8 字符串，会扰动 payload 尺寸与 B/frame 口径，且同进程 loopback 单向延迟对吞吐/分配 KPI 无决策价值（列为后续可选）。

### 5.3 硬断言阈值（防回归；已用优化前基线校准并经优化后实测守门）

| 项 | 场景 | 断言 |
|----|------|------|
| Micro Encode | 64B / 1KB / 4KB | ≤ 256 / 1280 / 4400 B/op |
| Micro Decode | 64B / 1KB | ≤ 512 / 1600 B/op |
| Micro Gen0 | 全部微基准累计（5×20k） | ≤ 25 |
| End-to-end 吞吐 | S1 / S2 | ≥ 20000 msg/s |
| End-to-end B/frame | S1 / S2 | ≤ 8192 / 12288 B/frame |
| 无损 | S1 / S2 | 最慢消费者已收 delivered >= published 且总投递 totalDeliveries >= published（流控下硬性；多消费者广播时 totalDeliveries 可为 published 的倍数） |

**基线证据流**：先记录优化前 B/op 与吞吐（写入 plan outcome），优化后断言「B/op 下降 / 吞吐不降」并留余量；上表为最终守门阈值。优化后实测（Release，`--all` 39/39、`--bench-queue` 14/14 全通过）：MicroEncode 184/1144/4216 B/op、MicroDecode 360/1320 B/op、Micro Gen0 累计 ≈17（其中 4KB 单次约 10）、S1 72967 msg/s / 3076 B/frame、S2 45624 msg/s / 7807 B/frame，阈值无需再调整。

---

## 六、优化清单（公共 API 签名不变）

| 编号 | 位置 | 内容 |
|------|------|------|
| O1 | `Net/QueueCoder.cs:392`（private static） | `ReadInt32` 去 `temp.ToArray()`，改手工小端读取 `data[o] \| data[o+1]<<8 \| ...`；每帧省 4 次分配 |
| O2 | `QClient.cs:189`、`QServer.cs:123`；`QueueCoder` | 新增 `internal List<QueueMsg> GetQueueResult(ReadOnlySpan<byte>)` + 私有 `AppendData(ReadOnlySpan<byte>)`；两处接收改调它，去整帧 `.ToArray()`；原 `public GetQueueResult(byte[])` 保留 |
| O3 | `Net/QueueCoder.cs` | 新增内部 `int DecodeTo(ReadOnlySpan<byte>, List<QueueMsg>)`（返回已消费字节数）直接产出 `QueueMsg`，去掉「`QueueSocketMsg` → `QueueMsg`」双对象；公开 `Decode(...)` 保留 |
| O4 | `QClient.cs:214`（private） | `_batcher_OnBatched` 去 `List<byte>`+`AddRange`+`ToArray`，先求和再精确分配并 `Buffer.BlockCopy`，`Send(span)` |
| O5 | `Net/QueueCoder.cs:204`（public static） | `Encode` 去 `List<byte>` 与 4×`BitConverter.GetBytes`，精确分配后手工写入 type/长度字段，UTF8 与 data 直接拷贝；线格式字节不变 |
| O6 | `Model/Exchange.cs:176-232`（private） | `subs.ToArray()` 改复用快照、去每批 `new List<byte[]>`；把逐条 `coder.Data` 再交 batcher 拼接改为**每订阅者每批编码进单个精确缓冲后 `Insert` 一次**，`Insert` 次数由 batchSize 降为 1，线内容等价 |
| O7 | `Model/Exchange.cs:255` + `QServer.cs:220`（private/internal） | B1：`Unsubscribe` 改为按会话 ID 清理（`Exchange.Unsubscribe(string sessionID, QueueMsg)`，`ReplyUnsubscribe` 传 `ut.ID`）；public 签名不变 |
| O8 | `QServer.cs:108`（private） | M1：`_serverSokcet_OnDisconnected` 调 `_exchange.SessionClosed(id)`，清理 binding 与 `_subscribers`（`Clear` 不清订阅） |

**不做（本轮 YAGNI）**：`QClient.Publish` 的二次 `Encoding.UTF8.GetBytes`、`ArrayPool` 深度池化、多进程 harness。

**风险与护栏**：O3/O5 动线格式 → FT9；O2 → FT1–FT8；O4/O6 动批处理 → FT2/FT6；O7 → FT4；O8 → FT7/FT8。全程不新增 `//` 注释。

---

## 七、错误处理、生命周期与稳定性

- **装配顺序**：先起**唯一**的 `QServer(自由端口)` 并确认可连接 → 再建 Consumer（`Subscribe`+`Start`）→ 再建 Producer（ctor 内 `Connect`）。
- **确定性 teardown**：无损用例先 `WaitUntil(sent == N)` 排空 → 停生产者 → `Producer.Dispose()` → `Consumer.Dispose()` → 短暂 settle；**服务器全程复用，仅在所有用例结束后 `QServer.Stop()` 一次**。每个用例 `finally` 释放该用例的客户端；下一用例换用**新的唯一 topic**（不重建服务器）。
- **不调用 `QServer.CalcInfo`**（`while(true)` 泄漏）；基准/回归自采计数。
- **防抖**：全部 `WaitUntil` 轮询（功能 10s、排空 30s），不用固定 sleep 做时序断言；基准阈值保守。
- **用例隔离**：try/catch → `Expect(false, name, ex.Message)`，单条失败不中断整套。
- **后台任务收尾**：`QClient.HeartAsync` 与 `Consumer` 清理任务靠 `Dispose/Close` 退出，保证每个客户端都释放。
- **输出刷出**：`Main` 为 `async Task`，退出前 `await Task.Delay(500)`。

---

## 八、验证与完成标准（DoD）

1. `dotnet build Src/SAEA.Sockets.sln -c Debug` 与 `-c Release` 均 **0 errors**（重点 `SAEA.QueueSocket`、`SAEA.QueueSocketTest`）。
2. `dotnet run --project Src/SAEA.QueueSocketTest -- --all` → 功能回归 + 基准全过，`ExitCode=0`，汇总 `N/N passed, 0 failed`。
3. `dotnet run --project Src/SAEA.QueueSocketTest -- --bench-queue` → 打印各场景 ns/op、B/op、msg/s、B/frame、Gen0/1/2，`ExitCode=0`。
4. 优化前后证据：记录优化前微基准 B/op 与端到端吞吐，优化后断言「B/op 下降 / 吞吐不降」并留余量；最终按 §5.3 守门。
5. `SAEA.P2PTest -- --all` 仍 **310/310**、`-- --bench-iocp` 仍 **29/29**。
6. 无新增 `//` 注释（`git diff -U0 -- '*.cs'` 过滤 `+` 行、排除 `///`，结果为空）。
7. 公共 API 签名无变更（Review `QueueCoder`/`Producer`/`Consumer`/`QServer`/`QClient`/`QueueMsg` 的 public 成员 diff）。
8. 提交只 stage 显式路径、不用 `git add -A`；未经明确要求不提交。
9. B1/M1 生效：FT4 在**未断开连接**下退订后计数不再增长；消费者断开后服务端 binding/`_subscribers` 经 `Exchange.SessionClosed` 被清理。

---

## 九、风险与缓解

| 风险 | 缓解 |
|------|------|
| O3/O5 改动破坏线格式 | FT9 往返断言守门；`total`/`MIN` 语义逐字保持 |
| O2 内部 span 重载与 `AppendData` 缓冲逻辑不一致 | 复用同一 `_bufferOffset/_bufferCount` 状态；FT1–FT8 端到端覆盖分帧/粘包 |
| O6 合并编码改变批处理字节流 | 线内容等价（同 name/topic 逐帧拼接）；FT2/FT6 覆盖率与无损 |
| 端到端基准偶发抖动导致假失败 | 阈值保守；无损断言用「停产后排空」；延迟不做硬断言 |
| 三端同进程导致 B/frame 汇总口径混淆 | 精确 B/op 由 Tier 1 微基准承担，端到端 B/frame 仅作趋势与宽容守门 |
| 端口/后台任务残留 | 自由端口 + 全量 `Dispose` + try/catch 隔离 + `finally` teardown |
| 单例 `ClassificationBatcher` 跨 QServer 串扰 | 全程仅 1 个 QServer；用例用唯一 topic 隔离（不每例重建） |
| 退订按 name 清理为 no-op | B1 改按会话 ID 退订；FT4 用未断开连接直测（O7 护栏） |
| 断连后服务端订阅残留 | M1 在 `OnDisconnected` 调 `Exchange.SessionClosed` 清理（O8 护栏） |
| 关闭丢弃未批出数据 | 无损用例先 `WaitUntil(sent == N)` 排空（`OnMessagesSent`）后再 `Dispose()`；端到端基准以在途窗口流控 + `WaitUntil` 消费者追上已发布数保证无损 |
| ns2.0 缺 `Encoding.GetString(ReadOnlySpan)` | 不引入该 API；name/topic 仍走 `ToArray()`+`GetString`（帧内小开销） |

---

## 十、修改文件清单

**新增（`Src/SAEA.QueueSocketTest/`）**

| 文件 | 说明 |
|------|------|
| `TestHarness.cs` | 断言/等待/自由端口/汇总工具（移植自 `SAEA.P2PTest`） |
| `QueueServerHarness.cs` | 服务器 + 生产/消费客户端装配与 teardown |
| `FunctionalTests.cs` | FT1–FT9 |
| `QueueBenchmark.cs` | 微基准 + 端到端基准 |

**改（`Src/SAEA.QueueSocketTest/`）**

`Program.cs`（CLI 分发 + `async Main`）

**改（`Src/SAEA.QueueSocket/`）**

`Net/QueueCoder.cs`（O1/O2/O3/O5）、`QClient.cs`（O2/O4）、`QServer.cs`（O2/M1）、`Model/Exchange.cs`（O6/B1，并发缺陷修复）、`Model/MessageQueue.cs`（并发缺陷修复）

**改（`Src/SAEA.Common/`）**

`Caching/FastQueue.cs`（新增 `TryDequeue`，加法性；并发缺陷修复需要）

**不新增/不修改**：`SAEA.Sockets`、`SAEA.P2P`、`.csproj` 依赖项、线格式相关常量。

---

## 十一、实施顺序（高层，writing-plans 细化）

1. 落 `TestHarness.cs` + `QueueServerHarness.cs` + `Program.cs` CLI 骨架（`--functional`/`--bench-queue`/`--all`）；`QueueServerHarness` 持有**唯一** `QServer` 复用。
2. 落 FT1–FT9（对齐现有实现，全绿，作为后续优化的护栏）；FT4 需先落 B1 修正（`Exchange.Unsubscribe` 按会话 ID）方能真实验证。
2.5 落并发缺陷修复（2.4：`FastQueue.TryDequeue` / `MessageQueue` 原子入队 / `Exchange` 单分发任务 + 非阻塞排空），使 FT6 无损；否则 FT1–FT9 无法全绿。
3. 落 `QueueBenchmark.cs`：微基准 + 端到端，先测**优化前**基线并记录。
4. O1–O6 逐项优化，并实施 O7（B1 退订）/O8（M1 断线清理）修正（每项后跑功能回归 + 微基准对比）。
5. 复核线格式/API/注释合规，跑 `--all`、`--bench-queue`、Debug/Release 构建、`SAEA.P2PTest` 回归，记录最终数字。

---

## 十二、预期收益

- `SAEA.QueueSocketTest` 具备无头、可断言、CI 友好的功能回归与三端基准，退出码可用于门禁。
- 接收/编码/批处理热路径逐帧分配显著下降（微基准 B/op 对比量化），吞吐不退化。
- 线格式、公共 API、批处理/心跳/断开时序全部保持不变，既有 310/310 + 29/29 回归不回退。
- 修正 `Unsubscribe` 按会话 ID 退订与断连会话清理两处内部缺陷（公共 API 不变），消除订阅/绑定泄漏。