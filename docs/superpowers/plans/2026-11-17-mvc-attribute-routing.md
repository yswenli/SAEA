# SAEA.MVC 属性路由实施计划

上游设计：`docs/superpowers/specs/2026-11-17-mvc-attribute-routing-design.md`

## 任务分解

| # | 任务 | 产出 | 依赖 | 验收 |
| --- | --- | --- | --- | --- |
| T1 | 路由核心 | `RouteAttribute.cs`、`AttributeRouteTable.cs`、`HttpGet`/`HttpPost` 模板构造、`Routing.Template`/`HttpMethods`、`AreaCollection` 记录本次类型、`SAEAMvcApplication` 构建期建表、`HttpContext` 匹配入口 + `RoutingPath` + 路径参数写 `Parmas` + 405、`MVCInvoker` 两段式动词校验（按 `[HttpGet]`/`[HttpPost]` 并集，未声明不限动词，不符返回 405 + `Allow`） | 无 | `dotnet build Src/SAEA.Sockets.sln -c Debug` 0 error；两段式既有用例仍全绿 |
| T2 | 单测 | `Src/SAEA.MVC.Tests` 新增路由、两段式动词与输出缓存 query 用例（14 条验收点，新增 `Tests/TwoSegmentVerbTests.cs`、`Tests/OutputCacheQueryTests.cs`） | T1 | `dotnet test Src/SAEA.MVC.Tests -c Release` 全绿 |
| T3 | SDK 生成器 | `Tool/CodeGenerte/ApiMapping.cs` 填 `Template`/`HttpMethods` 并修 NRE；`Tool/APISdkCodeGenerator.cs` 按模板输出；按限定的动词过滤产出的 GET / POST 方法（两段式 action 也按 `[HttpGet]`/`[HttpPost]` 过滤）；带 `ActionFilterAttribute` 但无动词特性的 action 不再被吞掉 | T1 | 生成器对属性路由控制器输出真实路径，对旧控制器输出不变 |
| T4 | OutputCache | 缓存键改为动词 + 规范路径 + 规范化 query（query 按键名 `Ordinal` 升序、`k=v` 用 `&` 连接、值为 null 按空串；无 query 时键形态与改动前逐字符一致） | T1 | 用例 12、14 通过 |
| T5 | 文档 | `Src/SAEA.MVC/README.md` + `README.en.md` 新增「路由」章节；修正 WS 入口表述、`OnActionExecuted` 签名示例；更新 `2026-11-04-mvc-websocket-design.md` 的「不做 WS 路径参数」非目标；`AGENTS.md` 的单测工程清单补 `SAEA.MVC.Tests` | T1–T4 | 中英一致，无失效示例 |
| T6 | 演示 | `Src/SAEA.MVCTest` 新增属性路由演示控制器 + 页面入口 | T1 | 手工访问可用 |
| T7 | 缺陷修复批次 | 两段式 `Async` 回退放开到所有 Controller（`RouteTable.cs`，精确名优先、不限定返回类型、属性路由不受影响）；action 超时的协作式取消与 `RequestAborted` 令牌（`MVCInvoker.cs`、`HttpContext.cs`）；404/405 文案与默认内容类型修正（`MVCInvoker.cs`、`HttpContext.cs`、`ContentResult.cs`）；用例 `Controllers/TimeoutCancelController.cs`、`Controllers/AsyncFallbackController.cs`、`Tests/TimeoutCancellationTests.cs`、`Tests/AsyncSuffixFallbackTests.cs`；复核修正：404 文案按请求行真实方法输出并对 action 名做 HTML 编码，405 文案对完整 request-target 做 HTML 编码，超时取消改到线程池执行，补 `RequestAborted` 生命周期说明，补 PUT 动词 404 与令牌居中形参两条用例；新增 `ActionTimeout` 解耦动作超时与连接空闲回收；修正 `SAEA.Http` `HttpContentResult` 的 `text/plane` 笔误 | T1–T6 | `dotnet build Src/SAEA.Sockets.sln -c Debug` 0 error；`SAEA.MVC.Tests` 69 passed |
| T8 | 属性路由令牌（已完成） | `AttributeRouteTable.cs` 支持 `[controller]` / `[action]` 令牌解析与构建期校验：类名去 `Controller`、方法名去 `Async`（含大小写不敏感）、必须独占一段、未知或畸形方括号、后缀剥离为空、`Foo`/`FooAsync` 同路径冲突；替换结果即 `Routing.Template`，SDK 生成器与冲突检测使用替换后路径 | T1 | 已完成：令牌正常替换并命中；混写、未知或畸形方括号、剥离为空、同路径同动词冲突均在构建期报错 |

## 执行顺序

T1 → （T2 / T3 / T4 可并行，T4 与 T2 有同文件外的独立性）→ T5 / T6。

T7 在 T1 之后可独立进行，与 T2–T6 无先后依赖。

## 约束

- 库项目 `netstandard2.0` + `LangVersion 8.0`，禁止 C# 9+ 语法。
- 不修改 `.csproj` 的 `<Version>` 与 `PackageReference`。
- 新文件必须带仓库统一 banner，`版本号` 取 `SAEA.MVC.csproj` 当前 `<Version>`，`唯一标识` 每个文件新 GUID。
- 不执行 `git push` / `reset --hard` / `clean` / 改写提交。