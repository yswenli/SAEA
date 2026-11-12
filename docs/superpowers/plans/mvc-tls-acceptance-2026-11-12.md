# MVC TLS 入口与链式改造验收记录（2026-11-12）

**范围：** `SAEAMvcApplication` → `WebHost` → `HttpSocket` 链式装配的端到端验证；新增同一端口承载 HTTPS 与 WSS 的 TLS 入口；新增测试工程 `Src/SAEA.MVC.Tests`（`net10.0`，未加入 `Src/SAEA.Sockets.sln`）。

**结论：** `SAEA.MVC.Tests` 17/17、`SAEA.Http.Tests` 30/30 全部通过；`SAEA.MVC` Debug 构建 0 error / 0 warning。验收期间修复了一个既有路由缺陷：未知 action 由 500 改为 404。

---

## 一、新增公开 API

| 入口 | 语义 |
|---|---|
| `WebConfig.UseSsl(Action<SaeaTlsOptions> configure)`（`Src/SAEA.Http/Model/WebConfig.cs`） | 在 `WebHost.Start()` 之前设置 TLS 配置；`configure` 为 null 抛 `ArgumentNullException`；重复调用以最后一次为准；`Start()` 之后调用抛 `InvalidOperationException`。未配置时保持明文 HTTP。 |
| `SAEAMvcApplication.UseSsl(Action<SaeaTlsOptions> configure)`（`Src/SAEA.MVC/SAEAMvcApplication.cs`） | 直接透传给 `_webHost.WebConfig.UseSsl`，因此冻结语义与上面一致。 |
| `WebHost` socket 延迟创建（`Src/SAEA.Http/WebHost.cs`） | `HttpSocket` 改到 `Start()` 内创建并读取当前 TLS 配置，构造之后、`Start()` 之前仍可调用 `UseSsl`；`Stop()` 置空内部 socket 引用，故 `Stop()` 后可再次 `Start()` 按当前配置重建；`Start()` 先冻结 pipeline 与 TLS 配置再监听，避免首个连接早于冻结建链。 |

同一端口同时服务 HTTPS 与 WSS：WebSocket 升级与普通请求共用同一条加密连接。

## 二、测试工程与用例

`Src/SAEA.MVC.Tests`：`net10.0`、`LangVersion 10.0`，`MSTest.TestAdapter` / `MSTest.TestFramework` 3.1.1、`Microsoft.NET.Test.Sdk` 17.8.0；引用 `SAEA.MVC`、`SAEA.Http`、`SAEA.Sockets`、`SAEA.Sockets.Pipeline`。用例全部走本机回环随机端口 + 临时站点根目录，不依赖外部服务。

| 文件 | 用例 |
|---|---|
| `Tests/HttpRoutingTests.cs` | `Controller_HttpGet_Returns200AndBody`、`Controller_HttpPostForm_BindsParameter`、`Controller_UnknownAction_Returns404`、`Controller_UnknownPostAction_Returns404`、`Controller_JsonAction_ReturnsApplicationJson`、`ActionFilter_ExecutesBeforeAction`、`StaticFile_Exists_ReturnsContent` |
| `Tests/TlsTests.cs` | `Https_Get_Returns200AndBody`、`Https_PostForm_BindsParameter`、`Wss_ClientEcho_Works` |
| `Tests/WebSocketTests.cs` | `Ws_ClientEcho_Works`、`Ws_NonUpgradeGet_Returns400` |
| `Tests/LifecycleTests.cs` | `Stop_ThenStart_ReleasesPort` |
| `Tests/PipelineWiringTests.cs` | `ConfigurePipeline_AfterStart_ThrowsInvalidOperationException`、`ConfigurePipeline_OncePerConnection_ReceivesChannelActive`、`UseSsl_NullConfigure_ThrowsArgumentNullException`、`UseSsl_AfterStart_ThrowsInvalidOperationException` |

实测结果：

```
已通过! - 失败: 0，通过: 17，已跳过: 0，总计: 17 - SAEA.MVC.Tests.dll (net10.0)   exit 0
已通过! - 失败: 0，通过: 30，已跳过: 0，总计: 30 - SAEA.Http.Tests.dll (net10.0)  exit 0
```

`SAEA.Http.Tests` 回归通过，说明 socket 延迟到 `Start()` 创建没有破坏 `WebHost` 既有行为。

## 三、行为变更：未知 action 由 500 改为 404

- 位置：`Src/SAEA.MVC/RouteTable.cs` 的 `GetOrAdd(Type, string, bool)`。
- 原行为：GET 与 POST 两个 `ConcurrentDictionary.GetOrAdd` 工厂分支在解析不到 action 时 `throw new Exception("...找不到此action!")`，异常冒到 `HttpContext.GetActionResult` 的 catch 被映射成 `InternalServerError`，使 `MvcInvoker` 里 `routing == null` 的 404 分支永远不可达。
- 现行为：两个分支改为 `return null`，由 `MvcInvoker.InvokeResult` 返回 404 与「找不到：{controller}/{action}」提示。重复 action 的异常保留（属于编程错误，继续 500）。
- 缓存语义：`ConcurrentDictionary.GetOrAdd` 会把工厂返回的 null 存为该键的值，后续相同请求直接命中 null 并稳定返回 404，不重复反射解析。`Controller_UnknownPostAction_Returns404` 在同一用例里连续请求两次，实测第二次不再抛异常且仍为 404。
- 覆盖用例：`Controller_UnknownAction_Returns404`（GET）与 `Controller_UnknownPostAction_Returns404`（POST，连续两次请求验证缓存路径）。

## 四、已知遗留（仅记录，未在本轮处理）

1. WSS 关闭没有 TLS `close_notify`：`Src/SAEA.Http/Base/Net/HttpWebSocketTransport.cs` 的 `ShutdownSend()` 直接 `Socket.Shutdown(SocketShutdown.Send)`，部分客户端会看到 1006 异常关闭码。
2. `SAEAMvcApplication.Start()` 把所有启动期异常（含证书加载失败）包装成「当前端口已被其他程序占用」，排查时容易误判。
3. `UseSsl` 一旦调用过 `Start()` 即永久冻结（`WebConfig.FreezeSsl` 不随 `Stop()` 复位），换证书需新建 `SAEAMvcApplication` 实例。

## 五、依赖与命令

```bash
# 单元测试（两个工程都不在 sln 中，需单独指定）
dotnet test Src/SAEA.MVC.Tests/SAEA.MVC.Tests.csproj -c Release
dotnet test Src/SAEA.Http.Tests/SAEA.Http.Tests.csproj -c Release

# 目标库构建
dotnet build Src/SAEA.MVC/SAEA.MVC.csproj -c Debug
```

需要 .NET 10 SDK（实测 10.0.403）。库项目保持 `netstandard2.0` + C# 8；测试工程为 `net10.0`。