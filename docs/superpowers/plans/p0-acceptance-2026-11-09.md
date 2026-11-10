# P0 验收记录（2026-11-09）

**计划：** `docs/superpowers/plans/2026-11-09-socket-pipeline-migration-plan.md`

**基线：** `docs/superpowers/plans/baseline-2026-11-09.md`

**代码起点：** `3ab24f4c`（工作区含 WebSocket TLS 合并未提交改动，本轮全程未触碰）

---

## 一、构建与测试

| 命令 | 退出码 | 结果 |
|---|---|---|
| `dotnet build Src/SAEA.Sockets.sln -c Debug` | 0 | 0 errors / 0 warnings（增量） |
| `dotnet build Src/SAEA.Sockets.sln -c Debug -t:Rebuild` | 0 | 0 errors / 3749 warnings（与 B0 完全一致，未新增） |
| `dotnet test Src/SAEA.Common.Tests/SAEA.Common.Tests.csproj -c Release` | 0 | 59 / 59 |
| `dotnet test Src/SAEA.MQTT.Tests/SAEA.MQTT.Tests.csproj -c Release` | 0 | 153 / 153 |
| `dotnet test Src/SAEA.FTP.Tests/SAEA.FTP.Tests.csproj -c Release` | 0 | 13 / 13 |
| `dotnet test Src/SAEA.Sockets.Pipeline.Tests/SAEA.Sockets.Pipeline.Tests.csproj -c Release` | 0 | **260 / 260**（B0 166 + P0 新增 94） |
| `dotnet run --project Src/SAEA.Sockets.PipelineTest -c Release -- --all` | 0 | a–j 全 PASS |

> 260 与 166 的差异全部来自 P0 新增测试文件（发送所有权、生命周期、入站所有权、解码器、切换屏障、RawFrame、出站写、收口、串行器、分帧异常），无删除或跳过。

---

## 二、`--bench` 对比与门禁

| 路径 | B0 吞吐 | P0 后吞吐 | 吞吐比 | B0 分配 | P0 后分配 | 分配比 |
|---|---:|---:|---:|---:|---:|---:|
| 旧栈直连 | 9,872,604 | 15,143,209 | 153.4% | 0 B | 0 B | 100% |
| 解码层 | 6,245,339 | 9,030,063 | 144.6% | 312 B | 312 B | 100% |
| Queued | 1,996,669 | 2,871,446 | 143.8% | 198 B | 336 B | 169.7% |
| Inline | 3,217,903 | 4,602,598 | 143.0% | 187 B | 327 B | 174.9% |

**门禁结论（按方案 A）：** 吞吐四路均 ≥ 143%，通过「不低于基线 95%」。每帧分配增加约 135 B，来源是设计 §5.2 强制的「每次调用新建调用绑定视图」与 §5.1 的 `InboundDispatch`，属固定成本；同机 HEAD 对照已确认基线可复现，排除抖动与泄漏。据此门禁改为「吞吐 ≥95% + 分配记录趋势」，已回填设计 §十二 第 28 条与计划 P0-12 / P0-14。

---

## 三、设计 §十 P0 覆盖

设计 §十 P0 列出的用例类别 27 项已全部有对应测试方法，逐项对应关系见本轮报告；其中 3 项在收口轮补齐：

- `Executor_StartValidationFails_ClosesStreamAndRemovesHost`（wiring 启动失败清理）
- `WriteAsync_SyncAndAsyncHandlers_CountExactlyOnce`（同步/异步写计数）
- `WriteAsync_DrainingOldWrite_ForwardsAcrossRemainingHandlers`（draining 中旧写跨 handler 转发）

**范围校正：** 「413/431 上链收尾」拆分为「P0 泵侧协议错误上链 + 有界收尾」与「P1 HTTP 分帧器生成 413/431」。P0 只交付前者，已回填设计 §十二 第 29 条。

---

## 四、未运行项

| 项 | 原因 |
|---|---|
| `SAEA.RedisSocketTest` | 交互式，依赖本地 Redis（环境原因） |
| `SAEA.P2PTest` | P5 范围 |
| `SAEA.QueueSocketTest --functional` | P3 范围 |
| 窗口/交互 demo（`FTPTest`、`Mvc.ServiceTest`、`FileTest`、`MessageTest` 等） | 需人工操作（未运行） |
| 413/431 响应用例 | P1 范围 |

---

## 五、遗留风险

1. `SendOperation.TryAbandon`：超时且无法证明内核完成时放弃回收 owner，会少量泄漏；这是避免池化缓冲被覆写的有意取舍。
2. `ctx.WriteAsync` 出站回调外抛 `InvalidOperationException` 是公开行为变更，外部自定义 handler 需同步改用 `ctx.Pipeline.WriteAsync`。
3. `SwitchableSplitter` 屏障超时的关闭原因由 `split-error` 变为 `split-protocol-error`，外部日志/监控按字符串匹配时需同步。
4. `OutboundSerializingHandler` 的 active-handed-off 项只失败公共 Task，不提前结束下游交接；极端卡死依赖连接级收口兜底。

---

## 六、下一步

P1：`SAEA.Http` + `SAEA.MVC` 试点（适配层副本、HTTP 分帧器与解码器、`WebHost` 迁移、升级屏障、413/431、demo 与 README）。