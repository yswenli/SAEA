# Plan — SAEA.QueueSocket 三端性能测试与优化

> **For agentic workers:** Execute task-by-task with the Subagent-Driven workflow. Every task must end with a green build and (when requested) a commit. Never `git add -A`; stage explicit paths and verify with `git diff --cached --name-only`.

**Goal:** 让 `Src/SAEA.QueueSocketTest` 具备无头、可断言的 Producer/QServer/Consumer 功能回归与三端性能基准，并在**不改公共 API 签名 / 不改线格式**的前提下就地优化接收、编解码、批处理热路径，同时修正 `Exchange.Unsubscribe` 按会话 ID 退订（B1）与断线会话清理（M1）两处内部缺陷。

**Spec:** `docs/superpowers/specs/2026-09-22-queuesocket-3-party-bench-design.md`（已批准，commit `b9a8abe3`）

**Scope (in):** `Src/SAEA.QueueSocketTest/**`（新增 `TestHarness.cs`/`QueueServerHarness.cs`/`FunctionalTests.cs`/`QueueBenchmark.cs`，改 `Program.cs`）；`Src/SAEA.QueueSocket/{Net/QueueCoder.cs,QClient.cs,QServer.cs,Model/Exchange.cs}`。

**Scope (out):** 多进程 harness、`ArrayPool` 深度池化、新测试工程/NuGet、线格式变更、`SAEA.Sockets`/`SAEA.P2P`。

**Base:** `b9a8abe3`。既有回归基线：`SAEA.P2PTest -- --all` = 310/310，`-- --bench-iocp` = 29/29。

---

## 决策与硬不变量（来自 spec，实施时不得违背）

- **A. 单 QServer 复用.** 整个进程只创建 **1 个** `QServer`（自由端口），功能与基准共用；用例以**唯一 topic** 隔离，teardown 只释放客户端，不重建服务器（规避 `ClassificationBatcher` 进程级单例的 handler 叠加与线程泄漏）。
- **B. 退订键修正 (B1/O7).** `Exchange.Unsubscribe` 由按 `name` 改为按**会话 ID**（`Unsubscribe(string sessionID, QueueMsg)`，`QServer.ReplyUnsubscribe` 传 `ut.ID`）。public 签名不变。
- **C. 断线清理 (M1/O8).** `QServer._serverSokcet_OnDisconnected` 在空 id 守卫后调 `_exchange.SessionClosed(id)`。
- **D. 无损排空 (M2).** 无损用例/基准必须先 `WaitUntil(() => sent == N)` 排空生产者批处理，再 `Producer.Dispose()`（`Batcher.Dispose` 会先 `Clear()` 丢弃未批出数据）。
- **E. 构建命令.** 无 `Src/SAEA.QueueSocket.sln`；一律 `dotnet build Src/SAEA.Sockets.sln -c Debug|Release`（该 sln 已含 `SAEA.QueueSocketTest`）。
- **F. No-comments.** 不新增任何 `//` 行内注释（`///` XML doc 允许）。校验：`git diff -U0 -- '*.cs' | Select-String '^\+' | Where-Object { $_ -match '//' -and $_ -notmatch '///' }` 为空。
- **G. ns2.0.** 库内禁用 ns2.1+ API（`Encoding.GetString/GetBytes(ReadOnlySpan)`、`ArrayBufferWriter<T>`、`SequenceReader<T>`、`ReadOnlySequence<T>.FirstSpan`）。可用 `Span<T>`/`data.CopyTo`/`Buffer.BlockCopy`/`MemoryMarshal`/`BinaryPrimitives`。
- **H. 语言版本.** 测试工程 `LangVersion 8.0`（可用 `using var`、switch expression、`async Task Main`；**不可**用 target-typed `new`、record、init-only）。
- **I. 不调用 `QServer.CalcInfo`**（内部 `while(true)` 泄漏）。

**期望的 DoD：** spec §八（Debug/Release 构建 0 error；`--all`/`--bench-queue` 全过且 ExitCode 0；`SAEA.P2PTest` 310/310 与 29/29 不回退；无新增 `//`；public API diff 为空；B1/M1 经 FT4/FT7/FT8 验证）。

**Deviation（明确记录，实施时同步改 spec §5.2）：** 本轮**不做延迟 p50/p99** 测量。原因：`Producer.Publish` 仅接受 `string`，要内嵌 8B 时间戳需把二进制塞进 UTF8 字符串，会扰动 payload 尺寸与 B/frame 口径；且同进程 loopback 的单向延迟对本任务的吞吐/分配 KPI 无决策价值。spec §5.2 的延迟句改为「本轮不测」。

---

## Task 1: TestHarness + QueueServerHarness + Program CLI 骨架

**Files:**
- Create: `Src/SAEA.QueueSocketTest/TestHarness.cs`
- Create: `Src/SAEA.QueueSocketTest/QueueServerHarness.cs`
- Create: `Src/SAEA.QueueSocketTest/FunctionalTests.cs`（本任务仅空壳）
- Create: `Src/SAEA.QueueSocketTest/QueueBenchmark.cs`（本任务仅空壳）
- Modify: `Src/SAEA.QueueSocketTest/Program.cs`

- [ ] **Step 1: 新建 `TestHarness.cs`**（移植自 `SAEA.P2PTest/TestHarness.cs`，去掉未用成员与 `?` 注解以满足 LangVersion 8.0 且不引入 nullable 警告）：

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

using SAEA.Common;

namespace SAEA.QueueSocketTest
{
    public static class TestHarness
    {
        private static readonly object _lock = new object();
        private static int _pass;
        private static int _fail;
        private static readonly List<string> _failures = new List<string>();

        public static int PassCount { get { lock (_lock) { return _pass; } } }
        public static int FailCount { get { lock (_lock) { return _fail; } } }
        public static int TotalCount { get { lock (_lock) { return _pass + _fail; } } }
        public static bool HasFailures { get { lock (_lock) { return _fail > 0; } } }

        public static void Reset()
        {
            lock (_lock)
            {
                _pass = 0;
                _fail = 0;
                _failures.Clear();
            }
        }

        public static bool Expect(bool condition, string name)
        {
            return Expect(condition, name, null);
        }

        public static bool Expect(bool condition, string name, string detail)
        {
            lock (_lock)
            {
                if (condition)
                {
                    _pass++;
                }
                else
                {
                    _fail++;
                    _failures.Add(string.IsNullOrEmpty(detail) ? name : name + " -> " + detail);
                }
            }

            if (condition)
            {
                ConsoleHelper.WriteLine("[PASS] " + name);
            }
            else
            {
                ConsoleHelper.WriteLine("[FAIL] " + name + (string.IsNullOrEmpty(detail) ? "" : " -> " + detail));
            }

            return condition;
        }

        /// <summary>
        /// 获取一个当前空闲的 TCP 端口，降低集成测试端口冲突概率。
        /// </summary>
        public static int GetFreeTcpPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        /// <summary>
        /// 轮询等待条件成立，用于异步/网络测试，避免脆弱的固定延时。
        /// </summary>
        public static async Task<bool> WaitUntil(Func<bool> condition, int timeoutMs = 3000, int pollMs = 25)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (condition())
                {
                    return true;
                }
                await Task.Delay(pollMs);
            }
            return condition();
        }

        public static void Section(string name)
        {
            ConsoleHelper.WriteLine("");
            ConsoleHelper.WriteLine("--- " + name + " ---");
        }

        public static void WriteSummary(string title)
        {
            ConsoleHelper.WriteLine("");
            ConsoleHelper.WriteLine("=== " + title + ": " + PassCount + "/" + TotalCount + " passed, " + FailCount + " failed ===");

            if (_fail > 0)
            {
                foreach (var failure in _failures)
                {
                    ConsoleHelper.WriteLine("  FAILED: " + failure);
                }
            }
        }
    }
}
```

- [ ] **Step 2: 新建 `QueueServerHarness.cs`**（持有唯一 `QServer`；`OnDisconnected` 计数；生产/消费客户端工厂）：

```csharp
using System;
using System.Threading;

using SAEA.QueueSocket;

namespace SAEA.QueueSocketTest
{
    public static class QueueServerHarness
    {
        static readonly object _lock = new object();

        static QServer _server;
        static int _port;

        static long _disconnectedCount;

        public static int Port { get { lock (_lock) { return _port; } } }

        public static QServer Server { get { lock (_lock) { return _server; } } }

        public static string Address { get { return "127.0.0.1:" + Port; } }

        public static long DisconnectedCount { get { return Interlocked.Read(ref _disconnectedCount); } }

        public static string NewTopic(string prefix)
        {
            return prefix + "-" + Guid.NewGuid().ToString("N");
        }

        public static void Start()
        {
            lock (_lock)
            {
                if (_server != null) return;

                _port = TestHarness.GetFreeTcpPort();

                _server = new QServer(port: _port);
                _server.OnDisconnected += Server_OnDisconnected;
                _server.Start();
            }

            TestHarness.WaitUntil(() => Port > 0, 3000, 10).GetAwaiter().GetResult();
        }

        public static void Stop()
        {
            lock (_lock)
            {
                if (_server == null) return;

                try { _server.Stop(); } catch { }

                _server = null;
            }
        }

        private static void Server_OnDisconnected(string id, Exception ex)
        {
            Interlocked.Increment(ref _disconnectedCount);
        }

        public static Producer CreateProducer()
        {
            return new Producer("producer-" + Guid.NewGuid().ToString("N"), Address);
        }

        public static Consumer CreateConsumer(string topic)
        {
            var consumer = new Consumer("consumer-" + Guid.NewGuid().ToString("N"), Address);
            consumer.Subscribe(topic);
            consumer.Start();
            return consumer;
        }

        public static QClient CreateSubscriber(string topic)
        {
            var client = new QClient("subscriber-" + Guid.NewGuid().ToString("N"), Address);
            client.Connect();
            client.Subscribe(topic);
            return client;
        }
    }
}
```

- [ ] **Step 3: 新建空壳 `FunctionalTests.cs` / `QueueBenchmark.cs`**（保证 CLI 可编译，后续任务填充）：

```csharp
using System.Threading.Tasks;

namespace SAEA.QueueSocketTest
{
    public static class FunctionalTests
    {
        public static Task RunAllAsync()
        {
            return Task.CompletedTask;
        }
    }
}
```

```csharp
using System.Threading.Tasks;

namespace SAEA.QueueSocketTest
{
    public static class QueueBenchmark
    {
        public static Task RunAsync(bool enforceThresholds = true)
        {
            return Task.CompletedTask;
        }
    }
}
```

- [ ] **Step 4: 改 `Program.cs`。** 顶层 `Main` 改为 `async Task Main(string[] args)` 并加 CLI 分发；交互菜单整体保留在分发之后（`s/p/c/t/sc/sp/a` 行为不变）。替换 `static void Main(string[] args)` 开头到菜单 `while` 之前：

```csharp
        static async Task Main(string[] args)
        {
            ConsoleHelper.Title = $"SAEA.QueueSocketTest -- {DateTimeHelper.Now}";

            if (args != null && args.Length > 0)
            {
                switch (args[0].ToLowerInvariant())
                {
                    case "--all":
                        TestHarness.Reset();
                        QueueServerHarness.Start();
                        try
                        {
                            await FunctionalTests.RunAllAsync();
                            await QueueBenchmark.RunAsync();
                        }
                        finally
                        {
                            QueueServerHarness.Stop();
                        }
                        TestHarness.WriteSummary("SAEA.QueueSocketTest --all");
                        Environment.ExitCode = TestHarness.HasFailures ? 1 : 0;
                        await Task.Delay(500);
                        return;
                    case "--functional":
                        TestHarness.Reset();
                        QueueServerHarness.Start();
                        try
                        {
                            await FunctionalTests.RunAllAsync();
                        }
                        finally
                        {
                            QueueServerHarness.Stop();
                        }
                        TestHarness.WriteSummary("SAEA.QueueSocketTest --functional");
                        Environment.ExitCode = TestHarness.HasFailures ? 1 : 0;
                        await Task.Delay(500);
                        return;
                    case "--bench-queue-baseline":
                        TestHarness.Reset();
                        QueueServerHarness.Start();
                        try
                        {
                            await QueueBenchmark.RunAsync(false);
                        }
                        finally
                        {
                            QueueServerHarness.Stop();
                        }
                        TestHarness.WriteSummary("SAEA.QueueSocketTest --bench-queue-baseline");
                        Environment.ExitCode = TestHarness.HasFailures ? 1 : 0;
                        await Task.Delay(500);
                        return;
                    case "--bench-queue":
                    case "--bench":
                        TestHarness.Reset();
                        QueueServerHarness.Start();
                        try
                        {
                            await QueueBenchmark.RunAsync();
                        }
                        finally
                        {
                            QueueServerHarness.Stop();
                        }
                        TestHarness.WriteSummary("SAEA.QueueSocketTest --bench-queue");
                        Environment.ExitCode = TestHarness.HasFailures ? 1 : 0;
                        await Task.Delay(500);
                        return;
                }
            }

            var inputStr = "";

            if (args != null && args.Length > 0 && args[0].IsNotNullOrEmpty())
            {
                inputStr = args[0];
            }
```

（其余菜单代码块保持不变；确认 `using System.Threading.Tasks;` 已存在，若无则添加。）

- [ ] **Step 5: Verify + commit.**
  - `dotnet build Src/SAEA.Sockets.sln -c Debug` → 0 errors。
  - `dotnet run --project Src/SAEA.QueueSocketTest/SAEA.QueueSocketTest.csproj -c Debug -- --all` → 打印 `0/0 passed, 0 failed`，ExitCode 0。
  - `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj -c Debug -- --all` → 310/310（不回退）。
  - Commit `test(queuesocket): add harness, server reuse, and headless CLI skeleton`.

---

## Task 2: O7 — B1 退订按会话 ID（`Exchange.Unsubscribe` + `QServer.ReplyUnsubscribe`）

**Files:**
- Modify: `Src/SAEA.QueueSocket/Model/Exchange.cs`
- Modify: `Src/SAEA.QueueSocket/QServer.cs`

**Why:** `SessionManager` 把 `ut.ID` 设为 `RemoteEndPoint.ToString()`（如 `127.0.0.1:54321`），≠ 客户端 `name`；现 `Unsubscribe` 用 `sInfo.Name` 删 `_binding`/`_subscribers`（键为会话 ID）→ 恒为 no-op。FT4 依赖此修正才能真实验证。

- [ ] **Step 1: `Exchange.Unsubscribe` 改签名与键。** 将现有 `public void Unsubscribe(QueueMsg sInfo)` 整体替换为：

```csharp
        /// <summary>
        /// 取消订阅
        /// </summary>
        /// <param name="sessionID">会话ID</param>
        /// <param name="sInfo">队列消息</param>
        public void Unsubscribe(string sessionID, QueueMsg sInfo)
        {
            Interlocked.Decrement(ref _cNum);
            _binding.Del(sessionID, sInfo.Topic);

            if (_subscribers.TryGetValue(sInfo.Topic, out var topicSubscribers))
            {
                topicSubscribers.TryRemove(sessionID, out var _);

                if (topicSubscribers.IsEmpty)
                {
                    _subscribers.TryRemove(sInfo.Topic, out var _);
                }
            }
        }
```

- [ ] **Step 2: `QServer.ReplyUnsubscribe` 传 `ut.ID`：**

```csharp
        private void ReplyUnsubscribe(IUserToken ut, QueueMsg data)
        {
            _exchange.Unsubscribe(ut.ID, data);
        }
```

- [ ] **Step 3: Verify + commit.**
  - 全仓搜索确认无其它 `Unsubscribe(` 调用方需同步改：`grep -n "\.Unsubscribe(" Src/SAEA.QueueSocket`。
  - `dotnet build Src/SAEA.Sockets.sln -c Debug` → 0 errors。
  - `SAEA.P2PTest -- --all` → 310/310；`SAEA.QueueSocketTest -- --all` → 0/0。
  - Commit `fix(queuesocket): unsubscribe by session id`.

---

## Task 3.5（插入，先于 Task 3）：修复既有并发缺陷（无损投递）

**Files:**
- Modify: `Src/SAEA.Common/Caching/FastQueue.cs`
- Modify: `Src/SAEA.QueueSocket/Model/MessageQueue.cs`
- Modify: `Src/SAEA.QueueSocket/Model/Exchange.cs`

**背景：** Task 3 落地 FT6（3 生产者 × 100 → 2 消费者，无损）时稳定失败（收到 149–298/300）。诊断证明这是库既有的并发缺陷，与测试无关；不修则 FT6 不可能通过，且后续 O1–O6 优化会建立在错误结构上。

**根因（实测证据）：**
1. `MessageQueue.Enqueue`：`TryGetValue` 未命中即 `new FastQueue` 再 `TryAdd`；竞态失败者的消息写入**孤儿队列**，永不投递（实测 `calls=300 hit=297 orphan=2`）。
2. `Exchange.GetSubscribeData` 的 `_dispatchTasks.GetOrAdd(topic, factory)` 在竞态下工厂被多个调用方执行 → 同一 topic 启动 **2 个分发任务**，共同消费同一队列（实测 `tasks=2`）。
3. 分发内层循环先 `_messageQueue.GetCount(topic)>0` 再**阻塞** `DequeueAsync()`；抢输竞态时任务永久卡在 `channel.Reader.WaitToReadAsync()`，握着已积累批次不投递（实测 `deq=298 added≈296` 但 `msgs≈151`）。

**Step 1: `FastQueue` 增加非阻塞出队（加法性改动）：**

```csharp
public bool TryDequeue(out T t)
{
    if (_channel.Reader.TryRead(out t))
    {
        Interlocked.Decrement(ref _count);
        return true;
    }
    return false;
}
```

**Step 2: `MessageQueue.Enqueue` 原子化 + 增加 `TryDequeue`：**

```csharp
public ValueTask<bool> Enqueue(string topic, byte[] data)
{
    var queue = _dic.GetOrAdd(topic, t => new FastQueue<byte[]>(_maxPendingMsgCount));
    return queue.EnqueueAsync(data);
}

public bool TryDequeue(string topic, out byte[] data)
{
    data = null;
    if (_dic.TryGetValue(topic, out FastQueue<byte[]> queue))
    {
        if (queue != null)
        {
            return queue.TryDequeue(out data);
        }
    }
    return false;
}
```

**Step 3: `Exchange` 分发任务每 topic 只启动一次 + 非阻塞排空：**
- `_dispatchTasks` 类型改为 `ConcurrentDictionary<string, Lazy<Task>>`。
- 订阅侧在 `lock (_syncLocker)` 内完成「加入订阅者 + `EnsureDispatcher(topic)`」；`EnsureDispatcher` 用 `_ = _dispatchTasks.GetOrAdd(topic, t => new Lazy<Task>(() => Task.Run(() => DispatchLoop(t)), LazyThreadSafetyMode.ExecutionAndPublication)).Value;` 保证工厂/任务各只执行一次。
- `DispatchLoop(topic)` 内层用 `if (!_messageQueue.TryDequeue(topic, out var msg)) { await Task.Delay(5); continue; }`，仍受 `batchSize=1000` 与 `maxWaitTime=50ms` 约束；删除 `GetCount`+阻塞 `DequeueAsync`。
- **退出与移除同样在 `lock (_syncLocker)` 内完成**：仅当订阅者仍为空时才 `TryRemove` 本 topic 条目；否则 `continue` 复用同一任务继续排空。避免「退出任务移除新分发器 / 重订阅后无分发器」竞态（热排空循环保持无锁）。

**Step 4: Verify + commit.**
- `dotnet build Src/SAEA.Sockets.sln -c Debug` → 0 errors。
- `SAEA.QueueSocketTest -- --functional` 连续 3 次 → 25/25（FT6 = 300/300 无损）。
- `SAEA.P2PTest -- --all` → 310/310。
- Commit `fix(queuesocket): atomic enqueue, single dispatcher, non-blocking drain`。

**对后续任务的影响：** Task 10（O6）的编码/批处理优化**必须保留**本任务建立的单分发任务 + 非阻塞排空结构，不得回退为 `GetOrAdd(factory)` 或阻塞出队。

---

## Task 3: 功能回归 FT1–FT9（`FunctionalTests.cs`）

**Files:**
- Modify: `Src/SAEA.QueueSocketTest/FunctionalTests.cs`

**说明：** 共用 `QueueServerHarness` 的唯一 `QServer`；每用例唯一 topic；每用例 `try/catch` → `Expect(false, name, ex.Message)` 且 `finally` 释放该用例客户端；订阅注册用 300–500ms settle（服务器侧异步注册，无公开信号）。无损用例先等 `sent` 达标再 `Dispose`。

- [ ] **Step 1: 写入完整 `FunctionalTests.cs`：**

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SAEA.QueueSocket;
using SAEA.QueueSocket.Model;
using SAEA.QueueSocket.Net;
using SAEA.QueueSocket.Type;

namespace SAEA.QueueSocketTest
{
    public static class FunctionalTests
    {
        const int SettleMs = 400;

        public static async Task RunAllAsync()
        {
            TestHarness.Section("QueueSocket functional regression");
            await SafeAsync("FT1", FT1_SingleProducerSingleConsumerLossless);
            await SafeAsync("FT2", FT2_Broadcast);
            await SafeAsync("FT3", FT3_MultiTopicIsolation);
            await SafeAsync("FT4", FT4_UnsubscribeTakesEffect);
            await SafeAsync("FT5", FT5_LargeAndFramedMessages);
            await SafeAsync("FT6", FT6_BurstLossless);
            await SafeAsync("FT7", FT7_DisconnectCleanup);
            await SafeAsync("FT8", FT8_ReconnectUsable);
            await SafeAsync("FT9", () => { FT9_EncodeDecodeRoundtrip(); return Task.CompletedTask; });
        }

        static async Task SafeAsync(string name, Func<Task> test)
        {
            try
            {
                await test();
            }
            catch (Exception ex)
            {
                TestHarness.Expect(false, name + " no unhandled exception", ex.Message);
            }
        }

        static async Task FT1_SingleProducerSingleConsumerLossless()
        {
            var topic = QueueServerHarness.NewTopic("ft1");
            int n = 500;

            var received = new ConcurrentDictionary<string, byte>();
            var consumer = QueueServerHarness.CreateConsumer(topic);
            consumer.OnMessage += obj =>
            {
                try { received.TryAdd(Encoding.UTF8.GetString(obj.Data), 0); }
                finally { obj.Dispose(); }
            };

            await Task.Delay(SettleMs);

            var producer = QueueServerHarness.CreateProducer();
            long sent = 0;
            producer.OnMessagesSent += c => Interlocked.Add(ref sent, c);

            try
            {
                for (int i = 0; i < n; i++) producer.Publish(topic, "ft1-" + i);

                TestHarness.Expect(await TestHarness.WaitUntil(() => Interlocked.Read(ref sent) >= n, 30000), "FT1 producer flushed all", "sent=" + Interlocked.Read(ref sent));
                TestHarness.Expect(await TestHarness.WaitUntil(() => received.Count >= n, 15000), "FT1 consumer received all", "received=" + received.Count);
                TestHarness.Expect(received.Count == n, "FT1 no duplicates and no loss", "count=" + received.Count);
            }
            finally
            {
                producer.Dispose();
                consumer.Dispose();
            }
        }

        static async Task FT2_Broadcast()
        {
            var topic = QueueServerHarness.NewTopic("ft2");
            int n = 300;
            int consumers = 3;

            var counts = new int[consumers];
            var list = new List<Consumer>();
            for (int i = 0; i < consumers; i++)
            {
                int idx = i;
                var c = QueueServerHarness.CreateConsumer(topic);
                c.OnMessage += obj =>
                {
                    try { Interlocked.Increment(ref counts[idx]); }
                    finally { obj.Dispose(); }
                };
                list.Add(c);
            }

            await Task.Delay(SettleMs);

            var producer = QueueServerHarness.CreateProducer();
            long sent = 0;
            producer.OnMessagesSent += c => Interlocked.Add(ref sent, c);

            try
            {
                for (int i = 0; i < n; i++) producer.Publish(topic, "ft2-" + i);

                TestHarness.Expect(await TestHarness.WaitUntil(() => Interlocked.Read(ref sent) >= n, 30000), "FT2 producer flushed all");
                TestHarness.Expect(await TestHarness.WaitUntil(() => counts[0] >= n && counts[1] >= n && counts[2] >= n, 15000), "FT2 all consumers received all", counts[0] + "/" + counts[1] + "/" + counts[2]);
            }
            finally
            {
                producer.Dispose();
                foreach (var c in list) c.Dispose();
            }
        }

        static async Task FT3_MultiTopicIsolation()
        {
            var t1 = QueueServerHarness.NewTopic("ft3a");
            var t2 = QueueServerHarness.NewTopic("ft3b");
            int n = 200;

            long c1 = 0, c2 = 0;
            var ca = QueueServerHarness.CreateConsumer(t1);
            ca.OnMessage += obj => { try { Interlocked.Increment(ref c1); } finally { obj.Dispose(); } };
            var cb = QueueServerHarness.CreateConsumer(t2);
            cb.OnMessage += obj => { try { Interlocked.Increment(ref c2); } finally { obj.Dispose(); } };

            await Task.Delay(SettleMs);

            var producer = QueueServerHarness.CreateProducer();
            long sent = 0;
            producer.OnMessagesSent += c => Interlocked.Add(ref sent, c);

            try
            {
                for (int i = 0; i < n; i++) producer.Publish(t1, "a-" + i);
                for (int i = 0; i < n; i++) producer.Publish(t2, "b-" + i);

                TestHarness.Expect(await TestHarness.WaitUntil(() => c1 >= n && c2 >= n, 15000), "FT3 both topics delivered", "c1=" + c1 + " c2=" + c2);
                TestHarness.Expect(Interlocked.Read(ref c1) == n && Interlocked.Read(ref c2) == n, "FT3 no cross-topic leakage", "c1=" + c1 + " c2=" + c2);
            }
            finally
            {
                producer.Dispose();
                ca.Dispose();
                cb.Dispose();
            }
        }

        static async Task FT4_UnsubscribeTakesEffect()
        {
            var topic = QueueServerHarness.NewTopic("ft4");
            int warmup = 50;

            long received = 0;
            var client = QueueServerHarness.CreateSubscriber(topic);
            client.OnMessage += obj => { try { Interlocked.Increment(ref received); } finally { obj.Dispose(); } };

            await Task.Delay(SettleMs);

            var producer = QueueServerHarness.CreateProducer();
            long sent = 0;
            producer.OnMessagesSent += c => Interlocked.Add(ref sent, c);

            try
            {
                for (int i = 0; i < warmup; i++) producer.Publish(topic, "warm-" + i);
                TestHarness.Expect(await TestHarness.WaitUntil(() => Interlocked.Read(ref received) >= warmup, 15000), "FT4 warmup delivered", "received=" + received);

                client.Unsubscribe(topic);
                await Task.Delay(500);
                var before = Interlocked.Read(ref received);

                for (int i = 0; i < warmup; i++) producer.Publish(topic, "after-" + i);
                await Task.Delay(800);

                var after = Interlocked.Read(ref received);
                TestHarness.Expect(after == before, "FT4 no messages after unsubscribe", "before=" + before + " after=" + after);
            }
            finally
            {
                producer.Dispose();
                client.Dispose();
            }
        }

        static async Task FT5_LargeAndFramedMessages()
        {
            int[] sizes = { 1, 64, 4096, 65536 };

            foreach (var size in sizes)
            {
                var topic = QueueServerHarness.NewTopic("ft5");
                var payload = new byte[size];
                for (int i = 0; i < size; i++) payload[i] = (byte)(i % 128);

                bool delivered = false;
                bool identical = false;
                int gotLen = -1;
                var consumer = QueueServerHarness.CreateConsumer(topic);
                consumer.OnMessage += obj =>
                {
                    try
                    {
                        var d = obj.Data;
                        gotLen = d == null ? -1 : d.Length;
                        identical = d != null && d.Length == size;
                        if (identical)
                        {
                            for (int i = 0; i < size; i++)
                            {
                                if (d[i] != payload[i]) { identical = false; break; }
                            }
                        }
                        delivered = true;
                    }
                    finally { obj.Dispose(); }
                };

                await Task.Delay(SettleMs);

                var producer = QueueServerHarness.CreateProducer();
                long sent = 0;
                producer.OnMessagesSent += c => Interlocked.Add(ref sent, c);

                try
                {
                    producer.Publish(topic, Encoding.UTF8.GetString(payload));

                    TestHarness.Expect(await TestHarness.WaitUntil(() => delivered, 15000), "FT5 size=" + size + " received");
                    TestHarness.Expect(identical, "FT5 size=" + size + " bytes identical", "len=" + gotLen);
                }
                finally
                {
                    producer.Dispose();
                    consumer.Dispose();
                }
            }
        }

        static async Task FT6_BurstLossless()
        {
            var topic = QueueServerHarness.NewTopic("ft6");
            int producers = 3;
            int perProducer = 100;
            int consumers = 2;
            int total = producers * perProducer;

            var counts = new int[consumers];
            var list = new List<Consumer>();
            for (int i = 0; i < consumers; i++)
            {
                int idx = i;
                var c = QueueServerHarness.CreateConsumer(topic);
                c.OnMessage += obj => { try { Interlocked.Increment(ref counts[idx]); } finally { obj.Dispose(); } };
                list.Add(c);
            }

            await Task.Delay(SettleMs);

            var plist = new List<Producer>();
            long sent = 0;
            for (int p = 0; p < producers; p++)
            {
                var prod = QueueServerHarness.CreateProducer();
                prod.OnMessagesSent += c => Interlocked.Add(ref sent, c);
                plist.Add(prod);
            }

            try
            {
                var tasks = new List<Task>();
                for (int p = 0; p < producers; p++)
                {
                    int idx = p;
                    tasks.Add(Task.Run(() =>
                    {
                        for (int i = 0; i < perProducer; i++) plist[idx].Publish(topic, "ft6-" + idx + "-" + i);
                    }));
                }
                await Task.WhenAll(tasks);

                TestHarness.Expect(await TestHarness.WaitUntil(() => Interlocked.Read(ref sent) >= total, 30000), "FT6 producer flushed all", "sent=" + sent);
                TestHarness.Expect(await TestHarness.WaitUntil(() => counts[0] >= total && counts[1] >= total, 15000), "FT6 both consumers received all", counts[0] + "/" + counts[1]);
            }
            finally
            {
                foreach (var p in plist) p.Dispose();
                foreach (var c in list) c.Dispose();
            }
        }

        static async Task FT7_DisconnectCleanup()
        {
            var topic = QueueServerHarness.NewTopic("ft7");
            var before = QueueServerHarness.DisconnectedCount;

            var consumer = QueueServerHarness.CreateConsumer(topic);
            await Task.Delay(SettleMs);
            consumer.Dispose();

            TestHarness.Expect(await TestHarness.WaitUntil(() => QueueServerHarness.DisconnectedCount > before, 10000), "FT7 server observed disconnect", "before=" + before + " after=" + QueueServerHarness.DisconnectedCount);
        }

        static async Task FT8_ReconnectUsable()
        {
            var topic = QueueServerHarness.NewTopic("ft8");

            long first = 0;
            var c1 = QueueServerHarness.CreateConsumer(topic);
            c1.OnMessage += obj => { try { Interlocked.Increment(ref first); } finally { obj.Dispose(); } };
            await Task.Delay(SettleMs);
            c1.Dispose();

            long second = 0;
            var c2 = QueueServerHarness.CreateConsumer(topic);
            c2.OnMessage += obj => { try { Interlocked.Increment(ref second); } finally { obj.Dispose(); } };
            await Task.Delay(SettleMs);

            var producer = QueueServerHarness.CreateProducer();
            long sent = 0;
            producer.OnMessagesSent += c => Interlocked.Add(ref sent, c);

            try
            {
                for (int i = 0; i < 100; i++) producer.Publish(topic, "ft8-" + i);

                TestHarness.Expect(await TestHarness.WaitUntil(() => Interlocked.Read(ref sent) >= 100, 30000), "FT8 producer flushed all");
                TestHarness.Expect(await TestHarness.WaitUntil(() => second >= 100, 15000), "FT8 new consumer received", "second=" + second);
            }
            finally
            {
                producer.Dispose();
                c2.Dispose();
            }
        }

        static void FT9_EncodeDecodeRoundtrip()
        {
            var cases = new List<Tuple<string, string, byte[]>>
            {
                new Tuple<string, string, byte[]>("producer", "topic", new byte[0]),
                new Tuple<string, string, byte[]>("生产者", "主题", new byte[] { 1, 2, 3 }),
                new Tuple<string, string, byte[]>("a", "b", new byte[4096])
            };

            int idx = 0;
            foreach (var c in cases)
            {
                idx++;
                var name = c.Item1;
                var topic = c.Item2;
                var data = c.Item3;
                for (int i = 0; i < data.Length; i++) data[i] = (byte)(i % 250);

                var frame = QueueCoder.Encode(new QueueSocketMsg(QueueSocketMsgType.Data, name, topic, data));
                var list = QueueCoder.Decode(frame, out int offset);

                var ok = list != null && list.Count == 1 && offset == frame.Length;
                if (ok)
                {
                    var m = list[0];
                    ok = m.Type == QueueSocketMsgType.Data
                        && m.Name == name
                        && m.Topic == topic
                        && (m.Data == null ? 0 : m.Data.Length) == data.Length;
                    if (ok && data.Length > 0)
                    {
                        for (int i = 0; i < data.Length; i++)
                        {
                            if (m.Data[i] != data[i]) { ok = false; break; }
                        }
                    }
                    if (ok) m.Dispose();
                }

                TestHarness.Expect(ok, "FT9 roundtrip case" + idx, "name=" + name + " topic=" + topic + " len=" + data.Length);
            }
        }
    }
}
```

- [ ] **Step 2: Verify + commit.**
  - `dotnet build Src/SAEA.Sockets.sln -c Debug` → 0 errors。
  - `dotnet run --project Src/SAEA.QueueSocketTest/SAEA.QueueSocketTest.csproj -c Debug -- --functional` → 全过，ExitCode 0。若个别用例抖动，先跑 3 次判断是否稳定，不稳定则加大 `WaitUntil` 超时而非放宽断言。
  - Commit `test(queuesocket): add producer/server/consumer functional regression FT1-FT9`.

---

## Task 4: 基准（`QueueBenchmark.cs`）——先测优化前基线

**Files:**
- Modify: `Src/SAEA.QueueSocketTest/QueueBenchmark.cs`

**说明：** Tier1 微基准测精确 B/op；Tier2 端到端三端吞吐 + 系统级 B/frame（带流控的无损口径，见下方勘误）。阈值从本任务实测基线中取「宽松上界」，随后优化任务只收紧不放松。**产物：把优化前数字写入本 plan 的 “Baseline (pre-optimization)” 段并提交。**

- [x] **Step 1: 写入完整 `QueueBenchmark.cs`：**

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Common;
using SAEA.QueueSocket;
using SAEA.QueueSocket.Net;
using SAEA.QueueSocket.Type;

namespace SAEA.QueueSocketTest
{
    public static class QueueBenchmark
    {
        const int MicroN = 20000;
        const int MicroWarmup = 2000;

        static long _microEncode64;
        static long _microEncode1K;
        static long _microEncode4K;
        static long _microDecode64;
        static long _microDecode1K;
        static int _microGen0;
        static bool _enforceThresholds = true;

        public static async Task RunAsync(bool enforceThresholds = true)
        {
            _enforceThresholds = enforceThresholds;
            _microGen0 = 0;
            TestHarness.Section("QueueSocket micro benchmarks");

            MicroEncode("MicroEncode/64B", 64, out _microEncode64);
            MicroEncode("MicroEncode/1KB", 1024, out _microEncode1K);
            MicroEncode("MicroEncode/4KB", 4096, out _microEncode4K);
            MicroDecode("MicroDecode/64B", 64, out _microDecode64);
            MicroDecode("MicroDecode/1KB", 1024, out _microDecode1K);
            MicroDecodeBatch("MicroDecodeBatch/100x64B", 64, 100);

            if (enforceThresholds)
            {
                TestHarness.Expect(_microEncode64 <= 256, "MicroEncode/64B within budget", "B/op=" + _microEncode64);
                TestHarness.Expect(_microEncode1K <= 1280, "MicroEncode/1KB within budget", "B/op=" + _microEncode1K);
                TestHarness.Expect(_microEncode4K <= 4400, "MicroEncode/4KB within budget", "B/op=" + _microEncode4K);
                TestHarness.Expect(_microDecode64 <= 512, "MicroDecode/64B within budget", "B/op=" + _microDecode64);
                TestHarness.Expect(_microDecode1K <= 1600, "MicroDecode/1KB within budget", "B/op=" + _microDecode1K);
                TestHarness.Expect(_microGen0 <= 25, "Micro Gen0 within budget (5 runs x 20k)", "GC0=" + _microGen0);
            }
            else
            {
                ConsoleHelper.WriteLine("[baseline] micro B/op recorded without threshold enforcement");
            }

            TestHarness.Section("QueueSocket end-to-end benchmark");
            await RunScenarioAsync("S1", 1, 1, 64, 10000, 8192);
            await RunScenarioAsync("S2", 5, 5, 1024, 10000, 12288);

            TestHarness.WriteSummary("QueueBenchmark");
        }

        static byte[] BuildPayload(int size)
        {
            var payload = new byte[size];
            for (int i = 0; i < size; i++) payload[i] = (byte)(i % 251);
            return payload;
        }

        static void MicroEncode(string name, int payloadSize, out long bytesPerOp)
        {
            var payload = BuildPayload(payloadSize);
            var msg = new QueueSocketMsg(QueueSocketMsgType.Publish, "producer", "bench", payload);

            for (int i = 0; i < MicroWarmup; i++) QueueCoder.Encode(msg);

            var g0 = GC.CollectionCount(0);
            var before = GC.GetTotalAllocatedBytes(true);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < MicroN; i++) QueueCoder.Encode(msg);
            sw.Stop();
            var bytes = GC.GetTotalAllocatedBytes(true) - before;
            var g0After = GC.CollectionCount(0);

            _microGen0 += (g0After - g0);
            bytesPerOp = bytes / MicroN;
            ConsoleHelper.WriteLine("[micro] " + name + " | " + (sw.Elapsed.TotalMilliseconds * 1000000.0 / MicroN).ToString("F0") + " ns/op | " + bytesPerOp + " B/op | GC0=" + (g0After - g0));
        }

        static void MicroDecode(string name, int payloadSize, out long bytesPerOp)
        {
            var payload = BuildPayload(payloadSize);
            var frame = QueueCoder.Encode(new QueueSocketMsg(QueueSocketMsgType.Data, "producer", "bench", payload));
            var coder = new QueueCoder();

            for (int i = 0; i < MicroWarmup; i++)
            {
                var r = coder.GetQueueResult(frame);
                for (int j = 0; j < r.Count; j++) r[j].Dispose();
                r.Clear();
            }

            var g0 = GC.CollectionCount(0);
            var before = GC.GetTotalAllocatedBytes(true);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < MicroN; i++)
            {
                var r = coder.GetQueueResult(frame);
                for (int j = 0; j < r.Count; j++) r[j].Dispose();
                r.Clear();
            }
            sw.Stop();
            var bytes = GC.GetTotalAllocatedBytes(true) - before;
            var g0After = GC.CollectionCount(0);

            _microGen0 += (g0After - g0);
            bytesPerOp = bytes / MicroN;
            ConsoleHelper.WriteLine("[micro] " + name + " | " + (sw.Elapsed.TotalMilliseconds * 1000000.0 / MicroN).ToString("F0") + " ns/op | " + bytesPerOp + " B/op | GC0=" + (g0After - g0));
        }

        static void MicroDecodeBatch(string name, int payloadSize, int frames)
        {
            var payload = BuildPayload(payloadSize);
            var frame = QueueCoder.Encode(new QueueSocketMsg(QueueSocketMsgType.Data, "producer", "bench", payload));
            var batch = new byte[frame.Length * frames];
            for (int i = 0; i < frames; i++) Buffer.BlockCopy(frame, 0, batch, i * frame.Length, frame.Length);
            var coder = new QueueCoder();

            for (int i = 0; i < MicroWarmup; i++)
            {
                var r = coder.GetQueueResult(batch);
                for (int j = 0; j < r.Count; j++) r[j].Dispose();
                r.Clear();
            }

            var g0 = GC.CollectionCount(0);
            var before = GC.GetTotalAllocatedBytes(true);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < MicroN; i++)
            {
                var r = coder.GetQueueResult(batch);
                for (int j = 0; j < r.Count; j++) r[j].Dispose();
                r.Clear();
            }
            sw.Stop();
            var bytes = GC.GetTotalAllocatedBytes(true) - before;
            var g0After = GC.CollectionCount(0);

            var bytesPerFrame = bytes / MicroN / frames;
            ConsoleHelper.WriteLine("[micro] " + name + " | " + (sw.Elapsed.TotalMilliseconds * 1000000.0 / MicroN / frames).ToString("F0") + " ns/frame | " + bytesPerFrame + " B/frame | GC0=" + (g0After - g0));
        }

        static async Task RunScenarioAsync(string name, int producerCount, int consumerCount, int payloadSize, int durationMs, int budgetBytesPerFrame)
        {
            const int maxOutstanding = 8192;

            var topic = QueueServerHarness.NewTopic("bench-" + name);

            var chars = new char[payloadSize];
            for (int i = 0; i < payloadSize; i++) chars[i] = (char)('a' + (i % 26));
            var content = new string(chars);

            var counts = new int[consumerCount];
            var consumers = new List<Consumer>();
            for (int i = 0; i < consumerCount; i++)
            {
                int idx = i;
                var c = QueueServerHarness.CreateConsumer(topic);
                c.OnMessage += obj => { try { Interlocked.Increment(ref counts[idx]); } finally { obj.Dispose(); } };
                consumers.Add(c);
            }

            await Task.Delay(400);

            long published = 0;
            var producers = new List<Producer>();
            for (int i = 0; i < producerCount; i++)
            {
                producers.Add(QueueServerHarness.CreateProducer());
            }

            var stop = false;

            var gcBefore = GC.GetTotalAllocatedBytes(true);
            var g0 = GC.CollectionCount(0);
            var sw = Stopwatch.StartNew();

            var tasks = new List<Task>();
            for (int i = 0; i < producerCount; i++)
            {
                var producer = producers[i];
                tasks.Add(Task.Run(async () =>
                {
                    while (!Volatile.Read(ref stop))
                    {
                        if (Interlocked.Read(ref published) - MinCounts(counts) >= maxOutstanding)
                        {
                            await Task.Delay(1);
                            continue;
                        }
                        producer.Publish(topic, content);
                        Interlocked.Increment(ref published);
                    }
                }));
            }

            await Task.Delay(durationMs);
            Volatile.Write(ref stop, true);
            await Task.WhenAll(tasks);
            var publishElapsed = sw.Elapsed;

            var publishedFinal = Interlocked.Read(ref published);
            await TestHarness.WaitUntil(() => MinCounts(counts) >= publishedFinal, 30000, 50);

            sw.Stop();
            var delivered = MinCounts(counts);
            var totalDeliveries = TotalCounts(counts);
            var gcAfter = GC.GetTotalAllocatedBytes(true);
            var g0After = GC.CollectionCount(0);

            var elapsedSec = publishElapsed.TotalSeconds;
            var throughput = elapsedSec > 0 ? (long)(publishedFinal / elapsedSec) : 0;
            var bytesPerFrame = totalDeliveries > 0 ? (gcAfter - gcBefore) / totalDeliveries : 0;

            ConsoleHelper.WriteLine("[e2e] " + name + " | published=" + publishedFinal + " delivered=" + delivered + " deliveries=" + totalDeliveries + " | " + throughput + " msg/s | " + bytesPerFrame + " B/frame | GC0=" + (g0After - g0));

            TestHarness.Expect(delivered >= publishedFinal, name + " every consumer received all flushed messages", "published=" + publishedFinal + " delivered=" + delivered + " counts=" + string.Join(",", counts));
            TestHarness.Expect(totalDeliveries >= publishedFinal, name + " no message loss", "published=" + publishedFinal + " deliveries=" + totalDeliveries);
            if (_enforceThresholds)
            {
                TestHarness.Expect(throughput >= 20000, name + " throughput >= 20000 msg/s", "throughput=" + throughput);
                TestHarness.Expect(bytesPerFrame <= budgetBytesPerFrame, name + " bytes/frame within budget", "bytes/frame=" + bytesPerFrame + " budget=" + budgetBytesPerFrame);
            }

            foreach (var p in producers) p.Dispose();
            foreach (var c in consumers) c.Dispose();
        }

        static int MinCounts(int[] counts)
        {
            var min = int.MaxValue;
            for (int i = 0; i < counts.Length; i++)
            {
                var v = Volatile.Read(ref counts[i]);
                if (v < min) min = v;
            }
            return min == int.MaxValue ? 0 : min;
        }

        static long TotalCounts(int[] counts)
        {
            long total = 0;
            for (int i = 0; i < counts.Length; i++) total += Volatile.Read(ref counts[i]);
            return total;
        }
    }
}
```

> 注：`--bench-queue` 默认 `enforceThresholds=true`（§5.3 最终守门，Task 12 使用）；`--bench-queue-baseline` 传 `false`，只打印不判阈值，供 Task 4 记录优化前基线及 Task 5–11 中间验证使用（此时尚未全部优化，硬阈值必将未达标）。S1/S2 的 B/frame 预算分别取 spec §5.3 的 8192 / 12288；Task 4 以实测基线确认方向后回填 spec §5.3，若实测与阈值差距过大则一并调整两侧并记录。

> **Task 4 实测勘误（用户批准的必要修正）：** 初版 `RunScenarioAsync` 以「无背压饱和发布 10s」再断言无损，实测暴露两个问题：(1) `ClassificationBatcher.Insert` 在队列达到 `_max = size×10 = 50000` 时**静默丢弃**（`Batcher.cs:80-88`），饱和下 S1 丢约 2.8%、S2 严重失真；(2) 多消费者时用 `sum(counts)` 与 `sent` 比较的数学错误，且 `--bench-queue-baseline` 仍触发无损断言导致 ExitCode≠0。修正为：以「已发布数 − 最慢消费者已收数」为在途窗口（`maxOutstanding=8192`）对生产者施加流控，从而使端到端在**无损前提**下测最大可持续吞吐；`throughput = published/发布耗时`，`B/frame = 分配增量/总投递数`；无损断言保持硬性（背压后必然成立），性能阈值仍由 `enforceThresholds` 门控；端到端 payload 改用确定长度的 ASCII 字符串（`new string(chars)`）避免 `Encoding.UTF8.GetString` 对无效字节的替换/膨胀。此修正不改变库行为与 spec §5.3 预算。

- [x] **Step 2: 记录优化前基线。** 运行 `dotnet run --project Src/SAEA.QueueSocketTest/SAEA.QueueSocketTest.csproj -c Release -- --bench-queue-baseline`，把六项微基准 B/op 与 S1/S2 的 msg/s、B/frame、Gen0 写回本 plan 的 “Baseline (pre-optimization)” 段（下方占位）。已回填。

- [x] **Step 3: Verify + commit.** Debug 构建 0 errors；`--bench-queue-baseline` ExitCode 0。Commit `test(queuesocket): add micro + end-to-end three-party benchmark with pre-optimization baseline`。

---

## Baseline (pre-optimization)

> 由 Task 4 Step 2 回填（Release 运行，`--bench-queue-baseline`，带流控无损口径）。
> 复现：`dotnet run --project Src/SAEA.QueueSocketTest/SAEA.QueueSocketTest.csproj -c Release -- --bench-queue-baseline`

| 指标 | 值 |
|------|----|
| MicroEncode/64B | 592 B/op (692 ns/op) |
| MicroEncode/1KB | 2512 B/op (525 ns/op) |
| MicroEncode/4KB | 8656 B/op (789 ns/op) |
| MicroDecode/64B | 600 B/op (772 ns/op) |
| MicroDecode/1KB | 1560 B/op (636 ns/op) |
| MicroDecodeBatch/100x64B | 467 B/frame (173 ns/frame) |
| S1 throughput | 41817 msg/s |
| S1 B/frame | 6107 |
| S2 throughput | 34076 msg/s |
| S2 B/frame | 12978 |

---

## Task 5: O1 — `ReadInt32` 去 `temp.ToArray()`

**Files:**
- Modify: `Src/SAEA.QueueSocket/Net/QueueCoder.cs`

- [ ] **Step 1:** 替换 `private static int ReadInt32(ReadOnlySpan<byte> data, int offset)` 方法体为手工小端读取（每帧省 4 次数组分配）：

```csharp
        private static int ReadInt32(ReadOnlySpan<byte> data, int offset)
        {
            return data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24);
        }
```

- [ ] **Step 2: Verify + commit.**
  - `dotnet build Src/SAEA.Sockets.sln -c Debug` → 0 errors。
  - `SAEA.QueueSocketTest -- --functional` → 全过（FT9 守线格式）；`-- --bench-queue-baseline` 微基准 B/op 下降。
  - `SAEA.P2PTest -- --all` → 310/310。
  - Commit `perf(queuesocket): remove allocation in QueueCoder.ReadInt32`.

---

## Task 6: O5 — `QueueCoder.Encode` 单次精确分配

**Files:**
- Modify: `Src/SAEA.QueueSocket/Net/QueueCoder.cs`

- [ ] **Step 1:** 用以下两个方法替换现有 `public static byte[] Encode(QueueSocketMsg queueSocketMsg)`（移除 `List<byte>`/4×`BitConverter.GetBytes`/`ToArray`；线字节不变）：

```csharp
        /// <summary>
        /// socket 传输字节编码
        /// 格式为：1+4+4+x+4+x+x
        /// </summary>
        /// <param name="queueSocketMsg">队列消息对象</param>
        /// <returns>编码后的字节数组</returns>
        public static byte[] Encode(QueueSocketMsg queueSocketMsg)
        {
            byte[] n = null;
            byte[] tp = null;
            byte[] d = null;
            var nlen = 0;
            var tlen = 0;
            var total = 12;

            if (!string.IsNullOrEmpty(queueSocketMsg.Name))
            {
                n = Encoding.UTF8.GetBytes(queueSocketMsg.Name);
                nlen = n.Length;
                total += nlen;
            }
            if (!string.IsNullOrEmpty(queueSocketMsg.Topic))
            {
                tp = Encoding.UTF8.GetBytes(queueSocketMsg.Topic);
                tlen = tp.Length;
                total += tlen;
            }
            if (queueSocketMsg.Data != null && queueSocketMsg.Data.Length > 0)
            {
                d = queueSocketMsg.Data;
                total += d.Length;
            }

            var arr = new byte[1 + total];
            WriteFrame(arr, 0, queueSocketMsg.Type, n, tp, d);
            return arr;
        }

        /// <summary>
        /// 按 QueueSocket 线格式将一帧写入指定缓冲区，返回写入结束后的偏移。
        /// </summary>
        /// <param name="buffer">目标缓冲区</param>
        /// <param name="offset">起始偏移</param>
        /// <param name="type">消息类型</param>
        /// <param name="nameBytes">已编码的名称</param>
        /// <param name="topicBytes">已编码的主题</param>
        /// <param name="data">数据</param>
        /// <returns>写入结束后的偏移</returns>
        internal static int WriteFrame(byte[] buffer, int offset, QueueSocketMsgType type, byte[] nameBytes, byte[] topicBytes, byte[] data)
        {
            var nlen = nameBytes == null ? 0 : nameBytes.Length;
            var tlen = topicBytes == null ? 0 : topicBytes.Length;
            var dlen = data == null ? 0 : data.Length;
            var total = 12 + nlen + tlen + dlen;

            buffer[offset++] = (byte)type;
            WriteInt32(buffer, offset, total);
            offset += 4;
            WriteInt32(buffer, offset, nlen);
            offset += 4;
            if (nlen > 0)
            {
                Buffer.BlockCopy(nameBytes, 0, buffer, offset, nlen);
                offset += nlen;
            }
            WriteInt32(buffer, offset, tlen);
            offset += 4;
            if (tlen > 0)
            {
                Buffer.BlockCopy(topicBytes, 0, buffer, offset, tlen);
                offset += tlen;
            }
            if (dlen > 0)
            {
                Buffer.BlockCopy(data, 0, buffer, offset, dlen);
                offset += dlen;
            }
            return offset;
        }

        private static void WriteInt32(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }
```

- [ ] **Step 2: Verify + commit.**
  - `SAEA.QueueSocketTest -- --functional` → 全过（FT9 关键）；`-- --bench-queue-baseline` MicroEncode B/op 下降。
  - `SAEA.P2PTest -- --all` → 310/310。
  - Commit `perf(queuesocket): single-allocation QueueSocket Encode`.

---

## Task 7: O3 — `DecodeTo` 直接产出 `QueueMsg`（去双对象）

**Files:**
- Modify: `Src/SAEA.QueueSocket/Net/QueueCoder.cs`

- [ ] **Step 1:** 新增 internal `DecodeTo`（与公开 `Decode` 同样的扫描/容错语义，但直接填 `List<QueueMsg>`，不构造中间 `QueueSocketMsg`）。放在公开 `Decode(ReadOnlySpan<byte>, out int)` 之后：

```csharp
        /// <summary>
        /// 解码到 QueueMsg 列表，避免生成中间 QueueSocketMsg 对象。
        /// </summary>
        /// <param name="data">待解码的字节Span</param>
        /// <param name="result">解析结果追加目标</param>
        /// <returns>已消费的偏移量</returns>
        internal static int DecodeTo(ReadOnlySpan<byte> data, List<QueueMsg> result)
        {
            var offset = 0;
            if (data.Length < MIN)
            {
                return 0;
            }

            while (data.Length >= offset + MIN)
            {
                var typeValue = data[offset];
                if (typeValue < 1 || typeValue > 7)
                {
                    bool found = false;
                    for (var i = offset + 1; i < data.Length; i++)
                    {
                        if (data[i] >= 1 && data[i] <= 7)
                        {
                            typeValue = data[i];
                            offset = i;
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        return data.Length;
                    }
                }

                var type = (QueueSocketMsgType)typeValue;
                int packetStart = offset;
                offset += 1;

                if (offset + 4 > data.Length) { offset = packetStart; break; }
                var total = ReadInt32(data, offset);
                if (total < 0 || total > 100 * 1024 * 1024)
                {
                    offset = packetStart + 1;
                    continue;
                }
                if (data.Length < offset + total)
                {
                    offset = packetStart;
                    break;
                }

                var qm = new QueueMsg();
                qm.Type = type;
                offset += 4;

                if (offset + 4 > data.Length) { offset = packetStart; break; }
                var nameLength = ReadInt32(data, offset);
                if (nameLength < 0 || nameLength > total)
                {
                    offset = packetStart + 1;
                    continue;
                }
                offset += 4;

                if (nameLength > 0)
                {
                    if (offset + nameLength > data.Length) { offset = packetStart; break; }
                    qm.Name = Encoding.UTF8.GetString(data.Slice(offset, nameLength).ToArray());
                }
                offset += nameLength;

                if (offset + 4 > data.Length) { offset = packetStart; break; }
                var topicLength = ReadInt32(data, offset);
                if (topicLength < 0 || topicLength > total)
                {
                    offset = packetStart + 1;
                    continue;
                }
                offset += 4;

                if (topicLength > 0)
                {
                    if (offset + topicLength > data.Length) { offset = packetStart; break; }
                    qm.Topic = Encoding.UTF8.GetString(data.Slice(offset, topicLength).ToArray());
                }
                offset += topicLength;

                var dlen = total - 4 - 4 - nameLength - 4 - topicLength;
                if (dlen < 0)
                {
                    offset = packetStart + 1;
                    continue;
                }
                if (dlen > 0)
                {
                    if (offset + dlen > data.Length) { offset = packetStart; break; }
                    qm.Data = data.Slice(offset, dlen).ToArray();
                }
                offset += dlen;
                result.Add(qm);
            }

            return offset;
        }
```

- [ ] **Step 2:** 在 `GetQueueResult(byte[] data)` 内，把「`Decode` → 逐条 `new QueueMsg`」替换为 `DecodeTo`。将方法体改为：

```csharp
        public List<QueueMsg> GetQueueResult(byte[] data)
        {
            var result = new List<QueueMsg>();

            AppendData(data);

            if (_bufferCount >= MIN)
            {
                try
                {
                    var span = _buffer.AsSpan(_bufferOffset, _bufferCount);
                    var offset = DecodeTo(span, result);
                    if (result.Count > 0)
                    {
                        _bufferOffset += offset;
                        _bufferCount -= offset;
                        if (_bufferOffset > 4096 && _bufferCount < _bufferOffset)
                        {
                            CompactBuffer();
                        }
                        return result;
                    }
                    else if (offset > 0)
                    {
                        _bufferOffset += offset;
                        _bufferCount -= offset;
                    }
                }
                catch
                {
                    _bufferOffset += 1;
                    _bufferCount -= 1;
                }
            }
            return result;
        }
```

- [ ] **Step 3: Verify + commit.**
  - 确认公开 `Decode(byte[], out int)` / `Decode(ReadOnlySpan<byte>, out int)` 仍被保留（仅 `GetQueueResult` 不再调用它们）。`grep -n "Decode(" Src/SAEA.QueueSocket` 检查其它调用方未受影响。
  - `SAEA.QueueSocketTest -- --functional` → 全过；`-- --bench-queue-baseline` MicroDecode B/op 下降。
  - Commit `perf(queuesocket): decode directly into QueueMsg (drop double object)`。

---

## Task 8: O2 — 接收路径去整帧 `ToArray()`（span 重载）

**Files:**
- Modify: `Src/SAEA.QueueSocket/Net/QueueCoder.cs`
- Modify: `Src/SAEA.QueueSocket/QClient.cs`
- Modify: `Src/SAEA.QueueSocket/QServer.cs`

- [ ] **Step 1: `QueueCoder` 增 span 重载 + span `AppendData`。** 新增：

```csharp
        /// <summary>
        /// 包解析（span 版本，避免整帧复制）
        /// </summary>
        /// <param name="data">待解析的字节Span</param>
        /// <returns>解析后的队列消息列表</returns>
        internal List<QueueMsg> GetQueueResult(ReadOnlySpan<byte> data)
        {
            var result = new List<QueueMsg>();

            AppendData(data);

            if (_bufferCount >= MIN)
            {
                try
                {
                    var span = _buffer.AsSpan(_bufferOffset, _bufferCount);
                    var offset = DecodeTo(span, result);
                    if (result.Count > 0)
                    {
                        _bufferOffset += offset;
                        _bufferCount -= offset;
                        if (_bufferOffset > 4096 && _bufferCount < _bufferOffset)
                        {
                            CompactBuffer();
                        }
                        return result;
                    }
                    else if (offset > 0)
                    {
                        _bufferOffset += offset;
                        _bufferCount -= offset;
                    }
                }
                catch
                {
                    _bufferOffset += 1;
                    _bufferCount -= 1;
                }
            }
            return result;
        }

        private void AppendData(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) return;

            if (_bufferCount == 0 && _bufferOffset > 0)
            {
                _bufferOffset = 0;
            }

            int remainingSpace = _buffer.Length - _bufferOffset - _bufferCount;
            if (remainingSpace < data.Length)
            {
                if (_bufferOffset > 0)
                {
                    CompactBuffer();
                    remainingSpace = _buffer.Length - _bufferCount;
                }

                if (remainingSpace < data.Length)
                {
                    int newCapacity = Math.Max(_buffer.Length * 2, _bufferCount + data.Length);
                    byte[] newBuffer = new byte[newCapacity];
                    if (_bufferCount > 0)
                    {
                        Buffer.BlockCopy(_buffer, _bufferOffset, newBuffer, 0, _bufferCount);
                    }
                    _buffer = newBuffer;
                    _bufferOffset = 0;
                }
            }

            data.CopyTo(_buffer.AsSpan(_bufferOffset + _bufferCount));
            _bufferCount += data.Length;
        }
```

将公开 `GetQueueResult(byte[] data)` 改为委托（保留 null/空语义）：

```csharp
        public List<QueueMsg> GetQueueResult(byte[] data)
        {
            return GetQueueResult(data.AsSpan());
        }
```

删除旧的私有 `AppendData(byte[] data)`（`byte[].AsSpan()` 覆盖之）。

- [ ] **Step 2: `QClient`** — `_clientSocket_OnReceiveSpan` 内：

```csharp
            var list = _queueCoder.GetQueueResult(dataSpan);
```

- [ ] **Step 3: `QServer`** — `_serverSokcet_OnReceiveSpan` 内：

```csharp
            var list = qcoder.GetQueueResult(dataSpan);
```

- [ ] **Step 4: Verify + commit.**
  - `dotnet build Src/SAEA.Sockets.sln -c Debug` → 0 errors。
  - `SAEA.QueueSocketTest -- --functional` → 全过（FT1–FT8 覆盖粘包/分帧）；`-- --bench-queue-baseline` 端到端 B/frame 下降。
  - Commit `perf(queuesocket): receive via span overload without full-frame copy`.

---

## Task 9: O4 — `QClient._batcher_OnBatched` 单趟拼接

**Files:**
- Modify: `Src/SAEA.QueueSocket/QClient.cs`

- [ ] **Step 1:** 替换 `private void _batcher_OnBatched(IBatcher batcher, List<byte[]> data)` 方法体：

```csharp
        private void _batcher_OnBatched(IBatcher batcher, List<byte[]> data)
        {
            if (data != null && data.Count > 0)
            {
                var sentCount = data.Count;

                var totalLength = 0;
                for (int i = 0; i < data.Count; i++)
                {
                    totalLength += data[i].Length;
                }

                var buffer = new byte[totalLength];
                var offset = 0;
                for (int i = 0; i < data.Count; i++)
                {
                    var item = data[i];
                    Buffer.BlockCopy(item, 0, buffer, offset, item.Length);
                    offset += item.Length;
                }

                data.Clear();

                _clientSocket.Send(buffer.AsSpan());

                OnMessagesSent?.Invoke(sentCount);
            }
        }
```

- [ ] **Step 2: Verify + commit.**
  - `SAEA.QueueSocketTest -- --functional` → 全过；`-- --bench-queue-baseline` 端到端 B/frame 下降。
  - Commit `perf(queuesocket): single-pass batcher flush in QClient`.

---

## Task 10: O6 — `Exchange` 每订阅者每批一次编码 + 一次 `Insert`

**Files:**
- Modify: `Src/SAEA.QueueSocket/Model/Exchange.cs`

**Why:** 现每消息一次 `coder.Data(...)` 编码 + 一次 `Insert`，且每批 `subs.ToArray()`/`new List<byte[]>`。改为每订阅者每批构造单个精确缓冲（帧字节与原逐帧拼接等价），`Insert` 次数由 batchSize 降为 1。利用 Task 6 的 `QueueCoder.WriteFrame`。

- [ ] **Step 1:** 在 `Exchange.cs` 顶部 using 区补 `using System.Text;`、`using SAEA.QueueSocket.Net;`、`using SAEA.QueueSocket.Type;`。

- [ ] **Step 2:** 将 `GetSubscribeData` 内层「`messages.Count > 0`」分支替换为：

```csharp
                                if (messages.Count > 0)
                                {
                                    var currentSubs = subs.ToArray();

                                    foreach (var sub in currentSubs)
                                    {
                                        try
                                        {
                                            if (subs.TryGetValue(sub.Key, out var coder))
                                            {
                                                var bindInfo = _binding.GetBingInfo(sub.Key);
                                                if (bindInfo != null)
                                                {
                                                    var nameBytes = string.IsNullOrEmpty(bindInfo.Name) ? null : Encoding.UTF8.GetBytes(bindInfo.Name);
                                                    var topicBytes = string.IsNullOrEmpty(topic) ? null : Encoding.UTF8.GetBytes(topic);
                                                    var fixedLen = 1 + 12 + (nameBytes == null ? 0 : nameBytes.Length) + (topicBytes == null ? 0 : topicBytes.Length);

                                                    long bufferSize = 0;
                                                    for (int i = 0; i < messages.Count; i++)
                                                    {
                                                        bufferSize += fixedLen + messages[i].Length;
                                                    }

                                                    var buffer = new byte[bufferSize];
                                                    var bufOffset = 0;
                                                    for (int i = 0; i < messages.Count; i++)
                                                    {
                                                        bufOffset = QueueCoder.WriteFrame(buffer, bufOffset, QueueSocketMsgType.Data, nameBytes, topicBytes, messages[i]);
                                                        Interlocked.Increment(ref _outNum);
                                                    }

                                                    _classificationBatcher.Insert(sub.Key, buffer);
                                                }
                                            }
                                        }
                                        catch
                                        {
                                        }
                                    }
                                }
```

（`coder` 变量保留以维持 `subs.TryGetValue` 的存在性检查语义；不再调用 `coder.Data`。）

- [ ] **Step 3: Verify + commit.**
  - `dotnet build Src/SAEA.Sockets.sln -c Debug` → 0 errors。
  - `SAEA.QueueSocketTest -- --functional` → 全过（FT2 广播 / FT6 突发守）；`-- --bench-queue-baseline` S2 吞吐上升 / B/frame 下降。
  - Commit `perf(queuesocket): batch per-subscriber encode and single insert in Exchange`.

---

## Task 11: O8 — M1 断线调 `Exchange.SessionClosed`

**Files:**
- Modify: `Src/SAEA.QueueSocket/QServer.cs`

- [ ] **Step 1:** `_serverSokcet_OnDisconnected` 内，空 id 守卫之后加清理：

```csharp
        private void _serverSokcet_OnDisconnected(string id, Exception ex)
        {
            if (string.IsNullOrEmpty(id)) return;

            _exchange.SessionClosed(id);

            OnDisconnected?.Invoke(id, ex);
        }
```

- [ ] **Step 2: Verify + commit.**
  - `SAEA.QueueSocketTest -- --functional` → 全过（FT7/FT8 守）。
  - `SAEA.P2PTest -- --all` → 310/310。
  - Commit `fix(queuesocket): clean subscriptions on session disconnect`.

---

## Task 12: 收口 — 全量门禁 + 回填产物

- [x] **Step 1:** `dotnet build Src/SAEA.Sockets.sln -c Debug` 与 `-c Release` → 0 errors。
- [x] **Step 2:** `dotnet run --project Src/SAEA.QueueSocketTest/SAEA.QueueSocketTest.csproj -c Release -- --all` → 全过，ExitCode 0。
- [x] **Step 3:** `dotnet run --project Src/SAEA.QueueSocketTest/SAEA.QueueSocketTest.csproj -c Release -- --bench-queue` → ExitCode 0；此运行默认 `enforceThresholds=true`，强制校验 spec §5.3 的微基准 B/op、Micro Gen0、S1/S2 吞吐与 B/frame 全部达标；记录最终微基准 B/op 与 S1/S2 吞吐、B/frame。若任一守门未达标，继续优化热路径或（仅在实测证明阈值不合理时）按证据调整 spec §5.3 两侧阈值并记录理由，不得单纯放宽。
- [x] **Step 4:** 回归：`SAEA.P2PTest -- --all` = 310/310；`-- --bench-iocp` = 29/29。
- [x] **Step 5:** 合规：public API diff 为空（`git diff <base>..HEAD -- Src/SAEA.QueueSocket/Producer.cs Src/SAEA.QueueSocket/Consumer.cs Src/SAEA.QueueSocket/QServer.cs Src/SAEA.QueueSocket/QClient.cs` 仅 `internal`/私有改动、无 public 签名变化）；无新增 `//`（用决策 F 的命令校验）。
- [x] **Step 6:** 把最终数字回填本 plan 的 “Outcome” 段，并把 Task 4 校准后的阈值同步回 spec §5.3；提交 `docs(plan): record QueueSocket bench outcome and calibrated thresholds`。

---

## Outcome

> 由 Task 12 Step 6 回填。优化前 = Task 4 基线（`--bench-queue-baseline`，Release）；优化后 = Task 12 最终 `--all`（Release，强制 §5.3 全部通过，53/53）。

| 指标 | 优化前 | 优化后 |
|------|--------|--------|
| MicroEncode/64B (B/op) | 592 | 184 |
| MicroEncode/1KB (B/op) | 2512 | 1144 |
| MicroEncode/4KB (B/op) | 8656 | 4216 |
| MicroDecode/64B (B/op) | 600 | 360 |
| MicroDecode/1KB (B/op) | 1560 | 1320 |
| MicroDecodeBatch/100x64B (B/frame) | 467 | 293 |
| Micro Gen0（5×20k 累计，信息性） | 33 | 17（单次 0/3/10/1/3） |
| S1 throughput (msg/s) | 41817 | 72936 |
| S2 throughput (msg/s) | 34076 | 41768 |
| S1 B/frame | 6107 | 3201 |
| S2 B/frame | 12978 | 7764 |

优化提交：`70edff7a`(O1 ReadInt32) → `e8e86b98`(O5 Encode) → `677c70de`(O3 DecodeTo) → `7e34782f`(O2 span 接收) → `d410a407`(O4 batcher) → `1bda30d5`(O6 Exchange 批量编码) → `064f8fc3`(O8 断线清理) → `02b316fc`(订阅拆除串行化 + 回收 batcher) → `0be1c4d4`(FT10/FT11 + 单次 Gen0 门控)。功能回归 25/25、`--all` 53/53、P2P 310/310、`--bench-iocp` 29/29；spec §5.3 阈值无需调整。

---

## Risks / watch-items

- **O3/O5 改线格式.** FT9 往返是唯一守门；`total`/`MIN` 语义逐字保持（`total = 12 + nlen + tlen + dlen`，帧首加 1B type）。
- **O2 缓冲状态一致性.** span 重载与 `AppendData` 复用同一 `_bufferOffset/_bufferCount`；FT1–FT8 的粘包/分帧覆盖。
- **O6 合并编码.** 合并缓冲拼接必须与逐帧 `coder.Data` 字节完全相同（`WriteFrame` 单测由 FT2/FT6 间接覆盖）。
- **单例 `ClassificationBatcher`.** 严禁在任何任务里多建 `QServer`；全程 `QueueServerHarness` 单例。
- **无损排空.** 任何测 `sent` 的用例必须在 `Dispose` 前 `WaitUntil(sent >= N)`（`Batcher.Dispose` 会丢未批出数据）。
- **`--all` 同进程跑功能+基准** 共用唯一 `QServer`；若发现基准受功能残留订阅影响，确认 O8 已生效（Task 11）。
- **no-comments / staging.** 每任务提交前跑决策 F 校验与 `git diff --cached --name-only` 显式路径检查。
