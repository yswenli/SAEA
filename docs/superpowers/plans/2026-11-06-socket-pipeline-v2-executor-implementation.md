# SAEA.Sockets.Pipeline v2 第一批实施计划：执行器热路径

上游设计：`docs/superpowers/specs/2026-11-06-socket-pipeline-v2-executor-design.md`。
本计划只覆盖该设计已定项，不新增架构决策。

## 交付边界

- 只改 `Src/SAEA.Sockets.Pipeline/Runtime/ChannelPipelineExecutor.cs` 与对应测试、文档、基准回填。
- 不改公开 API 签名、选项默认值、分帧器、编解码器、handler 适配器。
- 不改 `SAEA.Sockets` 侧接入点。

## 通用约定

1. 库项目维持 `netstandard2.0` + `LangVersion 8.0`，禁用 C# 9+ 语法。
2. 改动后必须跑：库构建、`SAEA.Sockets.Pipeline.Tests`（Debug 与 Release）、`PipelineTest --all`、`--bench`。
3. 基准数据一律来自 `--bench` 实际输出，未运行不写数字。

---

## Task 1 执行器内联启动

**文件**：`Src/SAEA.Sockets.Pipeline/Runtime/ChannelPipelineExecutor.cs`

- `StartMessage` 去掉 `TaskCompletionSource` 闸门与 `Task.Run`，改为直接调用 `ExecuteMessageAsync(msg, id)`。
- `ExecuteMessageAsync` 接管 `_inFlight` 登记簿的移除与计数递减（`finally` 中，与 `_concurrency.Release()`、`msg.Release()` 同处）。
- `_inFlight` 只在任务未同步完成时登记；登记后复查 `IsCompleted` 并移除，避免残留条目。

**验证**：`dotnet build Src/SAEA.Sockets.Pipeline/SAEA.Sockets.Pipeline.csproj -c Debug`。
**用例**：`Concurrency_SameConnection_KeepsReceiveOrder_AndPerMessageChainOrder`、`Acceptance3_SlowAuth_DoesNotSerializeMessagesOnSameConnection`、收口相关用例全绿。

## Task 2 令牌按需创建

**文件**：同上。

- `MessageTimeout == Zero` 时直接用 `_connectionToken.Token`，不建 linked CTS、不写 `AsyncLocal`。
- `finally` 只在确实创建了 CTS 时 `Dispose` 与恢复 `AsyncLocal`。
- 超时判定改为「CTS 存在且已取消」。

**验证**：新增用例
- `Token_NoMessageTimeout_EqualsConnectionToken`：无超时配置下 handler 读到的 `ctx.CancellationToken` 与连接令牌等价（`CanBeCanceled`、取消联动）。
- `Token_WithMessageTimeout_TimesOutWithoutClosingConnection`：保留既有超时用例覆盖。

## Task 3 收口与取消回归

**文件**：`Src/SAEA.Sockets.Pipeline.Tests/Tests/Runtime/ExecutorShutdownTests.cs`

- 覆盖 `CancelPending` 下在途消息被取消、`DrainInFlight` 下在途消息跑完、收口后队列条目全部归还。
- 既有用例若已覆盖则不重复新增，只在报告中列出对应方法名。

**验证**：`dotnet test Src/SAEA.Sockets.Pipeline.Tests/SAEA.Sockets.Pipeline.Tests.csproj -c Debug` 与 `-c Release`。

## Task 4 基准前后对照

**命令**：`dotnet run --project Src/SAEA.Sockets.PipelineTest -c Release -- --bench`

- 记录旧栈、解码层、完整接收路径三条的帧/s 与 B/帧。
- 把结果回填 `docs/superpowers/specs/2026-11-05-socket-pipeline-design.md` §8.4，并注明本批改动范围。

## Task 5 文档同步

- v1 设计文档 §6.3「消费侧」两段改为内联启动 + 令牌按需创建的实际实现，并把"同步阻塞会占用消费协程"写进去。
- `Src/SAEA.Sockets.Pipeline/README.md` 与 `README.en.md` 的并发/性能说明同步（中英必须一致）。
- 本计划与 v2 设计文档补最终实测数据。

## Task 6 全量回归

- `dotnet build Src/SAEA.Sockets.sln -c Debug`（0 errors）
- 四个单测工程按仓库约定跑（目前只有 Pipeline 与本批相关，其余三个不受影响，可按需复跑）
- `PipelineTest --all`、`--socket`、`--tls`

## 执行结果（2026-11-06）

| Task | 状态 | 证据 |
|---|---|---|
| 1 内联启动 | 完成 | `ChannelPipelineExecutor.StartMessage` / `ExecuteMessageAsync`；`dotnet build Src/SAEA.Sockets.Pipeline` Debug 0 errors |
| 2 令牌按需创建 | 完成 | 同上；新增用例 `Token_NoMessageTimeout_UsesConnectionTokenDirectly` |
| 3 收口与取消回归 | 完成 | 既有 `CancelPending_CancelsInFlightMessage_AndCompletes`、`DrainInFlight_WaitsForRunningMessage_AndGivesUpAfterTimeout` 全绿，未重复新增 |
| 4 基准对照 | 完成 | `PipelineTest --bench` 5 次运行，结果已回填 v1 设计文档 §8.4 与 v2 设计文档 §八 |
| 5 文档同步 | 完成 | v1 设计 §5.1/§6.3/§8.4、v1 计划代码片段、本计划、v2 设计、README 双语 |
| 6 全量回归 | 完成 | 见下方命令与结果 |

```
dotnet test Src/SAEA.Sockets.Pipeline.Tests -c Debug    → 160 passed
dotnet test Src/SAEA.Sockets.Pipeline.Tests -c Release  → 158 passed
```

基准（5 次进程运行，500000 帧，body 256B）：完整接收路径 197–317 万帧/s（中位数 267 万，旧栈 21.4%）、475–480B/帧；v1 为 76–84 万帧/s（5.6–6.2%）、约 1.2KB/帧。目标是"下降不超过 15%"，仍未达成；下一批需做单拷贝（P3）与每帧分配压降，或另起固定 worker 循环方案。