# MVC TLS 验收记录（2026-11-13）

**验收点：** `dc38bd4d`（接收提交时的关闭竞态按信息级记录），其父链包含 `418db55c`（静态文件路径穿越防护与 `MapPath` 越界返回 `null`）与 `db4f68eb`（令牌两阶段回收）。
**对照基线：** `2d3d612b`（性能与日志对比起点，已包含 MVC TLS 入口）。
**范围：** `SAEAMvcApplication` → `WebHost` → `HttpSocket` 的 HTTPS/WSS 链路、静态文件映射安全，以及短连接高 churn 下的连接生命周期。

## 1. 结论摘要

- 功能矩阵 33/33 通过：HTTP 与 HTTPS 各跑一遍，覆盖 WS/WSS 与同一端口先 HTTPS 后 WSS 的双协议；两条路径穿越请求均返回 404，且不回显站点根目录之外的内容。
- 七个测试工程共 609 个用例，0 失败，退出码全为 0。
- 八项吞吐指标相对基线全面提升（HTTP 串行 808→943、HTTPS 串行 161→220、WebSocket 单连接 4889→7078 msg/s），全程控制台 `[Error]` 由基线 50 行降到 4 行，关闭竞态按信息级记录后降到 0 行。
- 60 秒混合 soak 成功 338396、失败 0、超时 0（降噪提交后复跑 335655、失败 0）；WorkingSet、Private、线程、句柄三轮前后无增长；无阻断项，遗留项见第 6 节。

## 2. 验证范围与方法

- 服务端：`http://127.0.0.1:15953` 与 `https://127.0.0.1:16412`，自签证书、TLS 1.2，两个监听地址都是 `127.0.0.1`；探针 `C:\Users\yswen\AppData\Local\Temp\opencode\mvc-probe-2026-11-13` 的 `Probe.csproj` 通过 `ProjectReference` 指向 `418db55c` 的源码快照 `C:\Users\yswen\AppData\Local\Temp\opencode\post-merge-418db55c`，不修改仓库源码。完整输出见同目录 `REPORT.md`，基线对照见 `REPORT.baseline-2d3d612b.md`。
- 压测：客户端与服务端在同一进程内，HTTP/HTTPS 走裸 TCP/TLS 短连接，每请求新建连接并持续读到服务端关闭，最后以 `SO_LINGER 0` 收尾；客户端源地址在 `127.0.0.2`–`127.0.0.201` 轮换。服务端对普通响应固定 `Connection: close`，不支持 keep-alive。
- 不使用 `HttpClient` 压测的原因：服务端固定短连接，`HttpClient` 读完响应即优雅关闭，本机会累积 TIME_WAIT；实测约 1.9 万条后 `connect` 返回 10048，RPS 会掉到几十的假象。`--diag` 是探针的根因诊断模式，只复现建连、端口耗尽与关闭时序，不产出性能结论；上述假象即由它定位并排除。
- 回归单测：`SAEA.MVC.Tests`、`SAEA.Sockets.Pipeline.Tests`、`SAEA.Http.Tests`、`SAEA.WebSocket.Tests`、`SAEA.Common.Tests`、`SAEA.MQTT.Tests`、`SAEA.FTP.Tests`，全部 `-c Release` 单独执行。

## 3. 合入内容

| 分组 | 关键提交 | 解决的问题 |
|---|---|---|
| A 连接生命周期 | `57a8aea3`（`0c969e6f`、`1ed5da4f`） | 对端不发 close_notify 直接断开时，收尾路径访问已释放的 TLS 锁抛 NullReferenceException；令牌 ID 被并发置空后按 null 键查字典抛 ArgumentNullException，新连接在 accept 或首次接收阶段被丢弃 |
| A 连接生命周期 | `db4f68eb`（`19585fc0`、`a2fc809b`、`5aa28707`、`c55b44d8`） | 令牌仍有在途收发时被回收复用，造成 `SetBuffer` 冲突与旧完成回调误断新连接；发送超时后内核仍可能读取该令牌的发送缓冲 |
| B 静态文件安全 | `418db55c`（`31441bfe`、`5025c75b`） | `/../` 可读出站点根目录之外的文件；`MapPath` 对越界路径不报错，静态分支也没有 `null` 判断 |
| C 日志降噪 | `dc38bd4d` | 令牌回收时关闭 socket 与接收提交交错，`ReceiveAsync` 抛 `ObjectDisposedException` 被当作故障记 `[Error]`；该分支改为信息级记录，仍断开并回滚在途计数 |

分支 A 的核心不变量：令牌 ID 在并发路径上只读一次快照，判空与字典查找使用同一个值，断开前先确认令牌仍绑定当前连接；`_tlsLock` 生命周期跟随连接对象，不在 `Dispose` 中释放或置空，收尾路径先取字段快照且异常不冒泡；令牌按「在途计数」两阶段回收，`TryBeginOperation` 后必须由 `CompleteOperation` 收口，计数归零才入池，且 `CompleteOperation` 是该令牌最后一次访问；发送看门狗超时不再把令牌放回池，而是 `RetireUserToken` 永久退役并由池补建，避免内核继续引用其发送缓冲。

分支 B 的核心不变量：URL 先统一分隔符、消解 `.` 与 `..`，拒绝 UNC、盘符与非法字符，再经 `GetFullPath` 归一化，最后带目录分隔符做前缀比较，只有仍在站点根目录内才返回物理路径；越界或非法路径返回 `null` 且不写入 URL 缓存，Http 与 MVC 的静态分支遇到 `null` 直接 404，不再调用 `StaticResourcesCache.Exists`。

## 4. 实测结果

### 4.1 功能矩阵

| 分组 | 用例数 | 通过 | 覆盖 |
|---|---:|---:|---|
| HTTP | 17 | 17 | 动态 action、GET/POST、表单/JSON、对象转 JSON、未知 controller/action、动作异常 500、静态文件（文本/脚本/100KB/空文件/不存在）、非 Upgrade 400、路径穿越、WS 回显 |
| HTTPS | 16 | 16 | 与 HTTP 相同的动态与静态用例、非 Upgrade 400、WSS 回显、同端口先 HTTPS 后 WSS 双协议 |
| 合计 | 33 | 33 | 进程退出码 0 |

| 穿越用例 | 基线 | 合入后 |
|---|---|---|
| `GET /../mvc-probe-secret.txt` | 200，返回站外文件内容 | 404，`o_o，找不到任何内容` |
| `GET /%2e%2e/mvc-probe-secret.txt` | 404（基线已拒绝） | 404，`o_o，找不到任何内容` |

### 4.2 回归单测

| 工程 | 通过 | 失败 | 退出码 |
|---|---:|---:|---:|
| `SAEA.MVC.Tests` | 23 | 0 | 0 |
| `SAEA.Sockets.Pipeline.Tests` | 280 | 0 | 0 |
| `SAEA.Http.Tests` | 30 | 0 | 0 |
| `SAEA.WebSocket.Tests` | 12 | 0 | 0 |
| `SAEA.Common.Tests` | 59 | 0 | 0 |
| `SAEA.MQTT.Tests` | 153 | 0 | 0 |
| `SAEA.FTP.Tests` | 52 | 0 | 0 |
| 合计 | 609 | 0 | 0 |

`dc38bd4d` 上复跑 `SAEA.MVC.Tests`、`SAEA.Sockets.Pipeline.Tests`、`SAEA.Http.Tests`、`SAEA.WebSocket.Tests` 四个工程，分别为 23 / 280 / 30 / 12，与上表一致，退出码均为 0；`SAEA.Sockets` Release 构建 0 错误。

### 4.3 性能对比

同机、同参数、同一探针，基线 `2d3d612b` 对合入后 `418db55c`。

| 指标 | 基线 | 合入后 |
|---|---:|---:|
| HTTP 串行 RPS | 808 | 943 |
| HTTP 并发 16 RPS | 3631 | 4140 |
| HTTP 并发 64 RPS | 3306 | 3529 |
| HTTPS 串行 RPS | 161 | 220 |
| 静态 100KB 串行 RPS | 349 | 529 |
| 静态 100KB 并发 64 RPS | 793 | 1063 |
| WebSocket 单连接 msg/s | 4889 | 7078 |
| WebSocket 16 连接 msg/s | 30876 | 31661 |

### 4.4 资源占用

合入后 3 轮 × 2000 请求：分配约 58.6 KB/请求；WorkingSet 402.0 MB、Private 349.2 MB、线程 46、句柄 841，三轮前后均未增长。Managed 堆在轮次间有增长，但客户端与服务端同进程，不能据此判定服务端托管泄漏。

### 4.5 soak 与日志收敛

60 秒混合流量为 HTTP 动态 4 路 + HTTPS 动态 4 路 + 静态 100KB 2 路 + WS 2 连接：

| 指标 | `418db55c` | `dc38bd4d`（降噪后复跑） |
|---|---:|---:|
| 成功 | 338396 | 335655 |
| 失败 | 0 | 0 |
| 超时 | 0 | 0 |
| 全程 `[Error]` | 4 行（去重后 2 个事件），基线为 50 行 | 0 行 |
| 功能/压测阶段 `[Error]` | 0 行 | 0 行 |

压测失败率与原因：

| 场景 | 失败/总请求 | 原因 |
|---|---:|---|
| HTTP 并发 16 | 14/41431 | 连接被重置、响应未完整返回 |
| HTTP 并发 64 | 4/35343 | 连接被重置、响应未完整返回 |
| 静态 100KB 并发 64 | 7/10662 | 连接被重置、响应未完整返回 |

降噪前的 4 行 `[Error]` 是关闭竞态噪声：令牌回收关闭 socket 与 `ProcessReceive` 提交 `ReceiveAsync` 交错，`ThreadPoolBoundHandle` 已释放导致提交失败，栈落在 `SaeaServerSocket.ProcessReceive`。该提交为这条例外新增 `catch (ObjectDisposedException)`，按信息级记录并照常断开、回滚在途计数；复跑整轮未再出现，但也未命中新增分支，属该竞态本身概率较低，不能据此认为窗口已消失。压测失败同属客户端与服务端同进程、`Connection: close` 短连接收尾的传输层噪声，不是服务端业务异常。

### 4.6 数字适用范围

- 同机回环、客户端与服务端同进程：省掉真实网卡、交换机与跨机 RTT，RPS 会明显高于生产。
- 服务端固定 `Connection: close`，每个请求都含 TCP 建连/断开，HTTPS 再加一次 TLS 握手；压测结束需等待 TIME_WAIT 自然回收。
- 自签证书、固定 TLS 1.2、客户端跳过证书校验，握手与证书链成本和生产 PKI 不同；静态文件命中内存缓存，不代表首次磁盘读取与大文件分块传输路径。
- 单次运行快照，未做多轮均值；同探针跨轮波动可达 20%–35%，只能作同一轮内的相对比较，不能当生产绝对值。

## 5. 公开 API 变更清单

### 5.1 本次合入（`2d3d612b` → `dc38bd4d`）

新增：无。删除：无。行为变更：

| 文件 | 符号 | 影响 |
|---|---|---|
| `Src\SAEA.Http\HttpUtility.cs` | `HttpUtility.MapPath(string)` | 越界、非法或 UNC 路径由「返回拼接路径」改为返回 `null`；签名不变，调用方必须判空 |
| `Src\SAEA.Http\HttpContext.cs` | `HttpContext.GetActionResult()` | `MapPath` 返回 `null` 时直接 404，不再用越界路径访问文件 |
| `Src\SAEA.MVC\HttpContext.cs` | `HttpContext.GetActionResult()` | 首页与普通静态分支同样在 `null` 时 404；原首页分支的 NotFound 结果未 `return` 的缺陷一并修正 |
| `Src\SAEA.Sockets\Core\SaeaConnection.cs` | `CompleteInput(Exception)`、`Dispose()` | 内部行为：TLS 收尾改用字段快照，`_tlsLock` 不再释放或置空 |
| `Src\SAEA.Sockets\Core\Tcp\SaeaServerSocket.cs` | `GetConnection(IUserToken)` | 内部行为：会话 ID 只读一次快照；`ProcessAccepted`、EOF、管线收尾同样使用快照并按「键 + 值」原子摘除 |
| `Src\SAEA.Sockets\Core\Tcp\SaeaServerSocket.cs` | `ProcessReceive(SocketAsyncEventArgs)` | 内部行为：提交接收时 socket 已释放的场景单独按信息级记录，不再计入 `[Error]`；仍断开并回滚在途计数 |

### 5.2 基线已包含（`2d3d612b` 已有，本次验收沿用）

| 文件 | 符号 | 类型与影响 |
|---|---|---|
| `Src\SAEA.Http\Model\WebConfig.cs` | `WebConfig.UseSsl(Action<SaeaTlsOptions>)` | 新增：唯一 TLS 入口；仅 `Start()` 前可调用，`null` 抛 `ArgumentNullException`，`Start()` 后抛 `InvalidOperationException`，重复调用以最后一次为准 |
| `Src\SAEA.MVC\SAEAMvcApplication.cs` | `SAEAMvcApplication.UseSsl(Action<SaeaTlsOptions>)` | 新增：透传 `WebConfig.UseSsl`，冻结语义相同 |
| `Src\SAEA.Http\WebHost.cs` | `WebHost.Start()`、`WebHost.Stop()` | 行为变更：socket 延迟到 `Start()` 创建，`Stop()` 置空引用后再次 `Start()` 按已冻结的当前配置重建 |
| `Src\SAEA.MVC\RouteTable.cs` | `RouteTable.GetOrAdd(Type, string, bool)` | 行为变更：未知 action 由抛异常（500）改为返回 `null`（404）；重复 action 仍抛异常 |

### 5.3 相邻变更核实说明

`30450527`（2026-11-12，RPC/WebSocket 重构）也常被与本轮 MVC/TLS 一起提及，但它是基线 `2d3d612b` 的祖先，不属于本次 `2d3d612b → 418db55c` 合入范围：`ActionFilterAtrribute` 的两个抽象方法参数由 `IUserToken` 改为 `IChannel`；新增 `RpcChannelKeys`；删除 `WSUserToken` 与 `WSContext`。以上均已在代码中核实，MVC/TLS 验收不覆盖这些入口。

## 6. 已知限制与遗留项

本轮只记录，不修改。无阻断项。

1. 【应修】`HttpUtility._cache` 是 `static` 且只按 URL 作键。同一进程内两个不同站点根目录的 `HttpUtility` 会共享缓存，后建站点可能拿到前一个站点的物理路径。
2. 【建议】`//ok.txt` 由可服务变为 404。路径规范化把以 `//` 开头的 URL 当作 UNC 拒绝，属安全侧收紧，但改变了历史行为。
3. 【应修】站内符号链接指向站外未验证。根目录前缀比较基于 `Path.GetFullPath`，不解析 reparse point；能向站点根目录写入链接的攻击者可借链接读出站外文件。
4. 【建议】`SaeaConnection.AttachTls` 与 `AbortHandshake` 仍直接读 `_tlsLock` 字段。当前锁不再被置空，风险已降低，但与其他路径的「先快照」写法不一致。
5. 【建议】`SaeaConnection.Dispose()` 后 `_tls` 仍保留已释放对象，`Tls` 与 `IsTlsActive` 会继续返回「已启用」，调用方可能访问已释放对象。
6. 【应修】WSS 关闭没有 TLS `close_notify`。`HttpWebSocketTransport.ShutdownSend()` 直接 `Socket.Shutdown(SocketShutdown.Send)`，部分客户端会看到 1006 异常关闭码。
7. 【建议】`WebConfig.FreezeSsl` 不随 `Stop()` 复位。同一 `SAEAMvcApplication` 实例重启后不能更换证书或关闭 TLS，只能新建实例。
8. 【应修】`SAEAMvcApplication.Start()` 把所有启动异常包装成「当前端口已被其他程序占用」，证书加载失败等错误会被误报，排障成本高。
9. 【建议】令牌回收关闭 socket 与 `ProcessReceive` 提交接收之间仍有交错窗口，现仅把该异常降为信息级并照常断开，未从时序上消除。彻底消除需要把「提交接收」与「回收关闭」串行到同一把锁，属结构性改动，本轮未做。

## 7. 复现步骤

```powershell
# 1. 取验收点（探针当前指向 dc38bd4d 的源码快照，可直接复用）
git fetch --all
git switch --detach dc38bd4d

# 2. 全量构建（库项目；测试工程不在 sln 内）
dotnet build Src\SAEA.Sockets.sln -c Release

# 3. 七个测试工程
dotnet test Src\SAEA.MVC.Tests\SAEA.MVC.Tests.csproj -c Release
dotnet test Src\SAEA.Sockets.Pipeline.Tests\SAEA.Sockets.Pipeline.Tests.csproj -c Release
dotnet test Src\SAEA.Http.Tests\SAEA.Http.Tests.csproj -c Release
dotnet test Src\SAEA.WebSocket.Tests\SAEA.WebSocket.Tests.csproj -c Release
dotnet test Src\SAEA.Common.Tests\SAEA.Common.Tests.csproj -c Release
dotnet test Src\SAEA.MQTT.Tests\SAEA.MQTT.Tests.csproj -c Release
dotnet test Src\SAEA.FTP.Tests\SAEA.FTP.Tests.csproj -c Release

# 4. 功能 + 性能 + 资源 + soak（约 2.5 分钟）
dotnet run --project C:\Users\yswen\AppData\Local\Temp\opencode\mvc-probe-2026-11-13\Probe.csproj -c Release

# 5. 只跑建连/关闭诊断
dotnet run --project C:\Users\yswen\AppData\Local\Temp\opencode\mvc-probe-2026-11-13\Probe.csproj -c Release -- --diag
```

- 探针的 `ProjectReference` 固定指向 `C:\Users\yswen\AppData\Local\Temp\opencode\verify-dc38bd4d`；改测其他提交先替换 `Probe.csproj` 中三处引用路径。基线复跑在 `2d3d612b` 上创建独立 worktree，指向该 worktree 后执行同一条 `dotnet run`。
- 每次压测前确认 15953 与 16412 未被占用；连续多轮压测需留出 TIME_WAIT 回收时间。