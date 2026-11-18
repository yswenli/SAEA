# SAEA.MVC 属性路由设计

## 1. 问题

SAEA.MVC 的地址完全由「URL 末尾两段」的位置约定产生：

- `Src/SAEA.MVC/HttpContext.cs` → `GetActionResult()` 中 `url.Split("/", RemoveEmptyEntries)` 后取 `arr[arr.Length - 2]` 为控制器名、`arr[arr.Length - 1]` 为 action 名；前缀段被静默丢弃。
- 控制器名必须等于 `类名 + "controller"`（`ConstHelper.CONTROLLERNAME = "controller"`，忽略大小写）。
- `RouteTable.GetOrAdd` 的缓存键是 `类型名 + URL中的action原文 + "_GET/_POST"`，`Routing` 不携带任何路径模板信息，因此**同一 action 无法声明多个路径**，也无法声明静态段、路径参数、版本前缀。

结论：缺的不只是一个特性，而是「路径模板 + 匹配器 + 路径参数绑定」三块。

## 2. 目标

| 项 | 内容 |
| --- | --- |
| 静态段 | `[Route("api/v1/users/list")]` |
| 路径参数 | `[Route("users/{id}")]`，必选、单段 |
| 类级前缀 | 类 `[Route("api/users")]` + 方法 `[Route("{id}")]` → `/api/users/{id}` |
| 路径令牌 | `[controller]`（类名去掉 `Controller` 后缀）与 `[action]`（方法名去掉 `Async` 后缀）：替换忽略大小写，令牌必须独占一段 |
| 动词限定 | `[HttpGet]` / `[HttpPost]`（可带路径）真正拦截动词，不匹配返回 405 |
| 零破坏 | 不使用 `[HttpGet]` / `[HttpPost]` 的控制器继续走两段式、不限动词，行为一字不变 |
| WebSocket | WS 入口走同一套匹配，`[Router]` 同样生效 |
| 启动期校验 | 同一路径 + 同一动词被两个 action 声明 → 启动时抛异常 |

## 3. 非目标

- 可选参数 `{id?}`、通配 `{*path}`、类型约束 `{id:int}` / `{id:guid}`。
- `~/` 绝对前缀（忽略类级模板）语法。
- `SetDefault(...)` 支持模板；`/` 的默认路由仍为两段式语义。
- 修改 `SAEA.Http` 的任何 URL 语义或公共契约。
- 批量迁移 `Src/SAEA.MVCTest`、`Src/SAEA.Mvc.ServiceTest` 中已有的两段式地址。
- `ForbiddenAccessList` 的匹配算法调整。

两段式动词严格化与 `OutputCache` 缓存键改造已在后续迭代中落地，见 5.2 与 8.1。

## 4. 公开 API

### 4.1 `RouteAttribute`（新增，`Src/SAEA.MVC/RouteAttribute.cs`）

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RouteAttribute : Attribute
{
    public RouteAttribute(string template);
    public string Template { get; }
}
```

不实现 `IFilter`：它不参与过滤器链，仅被路由构建器读取。必须是 `sealed` + 无参构造之外的单参构造，`netstandard2.0` / C# 8 上限内。

### 4.2 `HttpGet` / `HttpPost` 扩展

保留无参构造（源码兼容），新增可选模板参数与只读属性：

```csharp
public HttpGet();
public HttpGet(string template);
public string Template { get; }
```

二者仍为 `sealed`，仍实现 `IFilter`（现有发现逻辑按 `GetType().Name` 匹配，不能改类型名）。

### 4.3 `Routing` 扩展

新增两个属性，供 SDK 生成器与调试使用，运行时不依赖：

```csharp
public string Template { get; set; }        // 归一化后的绝对路径，含 {name} 占位符；两段式路由为 null
public string[] HttpMethods { get; set; }   // 限定动词；null 表示不限定（两段式或纯 [Route]）
```

### 4.4 `HttpContext` 扩展

新增两个只读属性：`RoutingPath` 供 `OutputCacheAttribute` 取到「本次实际命中的规范路径」，`RequestAborted` 暴露本次 action 调用的取消令牌：

```csharp
public string RoutingPath { get; internal set; }   // 属性路由命中时为本次请求的归一化实际路径（如 /users/5），否则为 null
public CancellationToken RequestAborted { get; internal set; }   // 单次 action 调用的取消令牌，超时后转为已取消
```

`RoutingPath` 必须存**实际请求路径**而不是路由模板：存模板会让 `/users/5` 与 `/users/7` 得到同一个键，路径参数场景下缓存依旧串号。模板本身只作为匹配器的中间结果，不对外暴露。

`RequestAborted` 加在 `SAEA.MVC.HttpContext` 上，而不是 `SAEA.Http.IHttpContext`：不改 `SAEA.Http` 的公共契约，普通 HTTP 宿主不受影响。取用时需要从 `HttpContext.Current` 判型或强转（其静态类型是 `SAEA.Http.Model.IHttpContext`）。它是**协作式取消**：只有观察令牌的代码才会停止，没观察令牌的同步代码无法被强制终止。令牌只在本次 action 调用期间有效，action 返回后取消源即释放，不要继续注册回调或交给后台任务长期持有；未进入 action 的请求（404、405、静态文件、`RequestDelegate`）读到的是不可取消的默认令牌。

## 5. 模板语法与归一化

### 5.1 语法

- 段分隔符 `/`；首尾 `/` 忽略；连续 `/` 合并为一个。
- `{name}` 为必选参数，`name` 只允许字母、数字、下划线，且非空。
- 其他任何 `{...}` 变体（`{id?}`、`{*x}`、`{id:int}`）在构建期抛 `ArgumentException`，不做静默降级。
- 类级模板与方法级模板用 `/` 拼接；类级为空时方法级模板即绝对路径。
- 归一化结果：单前导 `/`、无尾斜杠、段内保留原大小写（比较时忽略大小写）。

### 5.2 动词

| 声明 | 结果 |
| --- | --- |
| 方法上只有 `[Route("x")]` | 路径 `/x`，GET/POST/PUT/DELETE 全部接受 |
| `[Route("x")]` + `[HttpGet]` | 路径 `/x`，仅 GET |
| `[HttpGet("x")]` | 路径 `/x`，仅 GET |
| 类上 `[Route("api")]` + 方法上 `[HttpGet("x")]` | 路径 `/api/x`，仅 GET |
| 类上 `[Route("api")]` + 方法上 `[HttpGet]`（无参） | 路径 `/api`，仅 GET |
| 类上 `[Route("api")]` + 方法上无任何路由特性 | **不进入属性路由**，仍走两段式（避免类级模板意外吞掉全部 action） |
| 只有 `[HttpGet]` / `[HttpPost]`（无参、类上无模板） | 不进入属性路由，仍走两段式 |

方法上同时声明 `[Route("x")]` 与 `[HttpGet("y")]` → 构建期抛 `ArgumentException`（路径来源必须唯一）。

两段式 action 走同一套动词判定：`MVCInvoker.InvokeResult` 在 `RouteTable.GetOrAdd` 拿到 `Routing` 之后、执行过滤器与 WebSocket 入口之前，取 action 上 `[HttpGet]` / `[HttpPost]` 的并集校验 `Request.Method`（`OrdinalIgnoreCase` 比较）。两者都未声明时返回 null 表示不限动词，行为与未加校验时一致；声明了则只接受并集内的动词，不匹配时设置 `Allow`（按 `GET, POST` 顺序，只列声明了的）并返回 **405 Method Not Allowed**，action、过滤器与 WebSocket 入口都不执行。`WebConfig.DefaultRoute` 默认路由与 `/{Controller}/{Action}` 两条两段式分支都经过这里；`OPTIONS` 在 `HttpContext` 的请求方式分支里直接返回 `EmptyResult()`，不进入路由，因此不受影响。

### 5.3 冲突

同一「归一化路径 + 动词」在**同一应用**内被两个 action 声明 → `SAEAMvcApplication` 构造期抛异常，消息包含路径、动词与两个 `Controller/Action`。
同一路径不同动词合法共存。

### 5.4 路径令牌

模板段可以是 `[controller]` 或 `[action]` 两个令牌，构建期替换为实际名称：

| 令牌 | 取值 | 示例 |
| --- | --- | --- |
| `[controller]` | 控制器类名去掉结尾 `Controller` 后缀 | `UserController` → `User` |
| `[action]` | action 方法名去掉结尾 `Async` 后缀 | `GetAsync` → `Get` |

- 令牌识别与后缀剥离都忽略大小写（`[Controller]` 与 `getasync` → `get` 同样生效）。
- 令牌必须独占一整段：`[controller]`、`[action]` 单独成段合法；`v1-[action]`、`[controller]s`、`{[action]}` 这类与字面量或花括号混写构建期抛 `ArgumentException`。
- 段里出现方括号但既不是 `[controller]` 也不是 `[action]`（含 `[foo]`、未闭合的 `[controller`）构建期抛 `ArgumentException`；这类写法此前会被当作字面静态段静默注册，现在启动即失败。
- 后缀剥离后为空（类名恰好是 `Controller`、方法名恰好是 `Async`）构建期抛 `ArgumentException`。
- 类级 `[Route]` 模板同样可以写 `[action]`，按每个 action 分别替换：`[Route("api/v1/[controller]/[action]")]` 让该控制器每个 action 各走自己的地址。
- 替换发生在属性路由表构建期，替换后的绝对路径即 `Routing.Template`，SDK 生成器输出与冲突检测都使用替换后的结果；匹配语义（逐段静态比较、忽略大小写、尾斜杠等价、`{id}` 单段必选、query 不参与匹配）不变。
- 同一控制器里 `Foo` 与 `FooAsync` 都用 `[action]` 时解析到同一路径，动词相同即构建期 `InvalidOperationException` 冲突；需要共存就显式写不同模板或分动词。

## 6. 匹配模型

### 6.1 输入来源

属性路由**不使用** `Request.Url`：`SAEA.Http/Base/RequestDataReader.Analysis` 会把末段纯数字从 `Url` 剥离并写入 `Query["id"]`（`/users/5` → `Url=/users` + `Query["id"]=5`），路径参数会因此丢失。

改用 `Request.RelativeUrl`（`Src/SAEA.Http/Base/HttpBase.cs` 的 public 属性，存 `NormalizeTarget` 处理后的原始 request-target），按 `?` 裁掉 query 得到原始路径。`SAEA.Http` 一行不改。

两段式兜底继续使用 `Request.Url`，保持既有行为不变。

### 6.2 归一化

- 路径按 `/` 切段（手工索引扫描，不产生临时数组），段内容做一次 URL 解码。
- 解码结果含 `/` 时该段判为不匹配（防 `%2F` 越段）。
- 段比较忽略大小写（`string.Compare(..., ignoreCase: true)`），与现有控制器名比较一致。
- 尾斜杠等价、`//` 合并、query 不参与匹配。

### 6.3 顺序

1. **属性路由：静态命中**（模板无参数）— 归一化路径查 `Dictionary<string, Entry>`，O(1)。
2. **属性路由：参数命中** — 按「段数」分桶，桶内按「静态段数量降序 → 声明顺序升序」线性匹配，静态段多者优先。
3. **两段式约定** — 现有 `GetActionResult` 逻辑原样保留（`Request.Url`）。
4. **静态文件** — 现有逻辑原样保留。
5. 404。

命中路径但动词不匹配：若该路径存在其它动词的条目 → 返回 405，响应体列出允许的动词，并设置 `Allow` 响应头。若路径完全没有属性路由条目 → 继续走第 3 步（不能因为属性路由存在就抢掉两段式）。

### 6.4 执行

匹配器只负责「找到 `(Type, MethodInfo, verbs, template, pathParams)`」，执行阶段**复用** `RouteTable.GetOrAdd(controllerType, methodName, isPost)`：实例缓存、过滤器解析、未知 action 处理全部沿用现有实现，避免复制 `RouteTable` 里那段分支逻辑。

`isPost` 映射：`POST` → `true`，其余（`GET`/`PUT`/`DELETE`）→ `false`，与现有 `HttpContext.HttpHandle` 的取值一致。

## 7. 路径参数绑定

1. 命中的路径参数写入 `Request.Parmas[name] = decodedValue`，**覆盖**同名 query/form 值（更具体者优先）。
2. 之后与两段式一致：`Request.Parmas.ToNameValueCollection()` 交给 `MvcInvoker.InvokeResult`。
3. `ParamsHelper.FillPamars` 一行不改：它按参数名取值并做基础类型转换，路径参数因此天然可用。

## 8. 与既有设施的交互

### 8.1 OutputCache

`OutputCacheAttribute` 的缓存键从 `Request.Url` 改为「请求动词 + 规范路径 + 规范化 query」：规范路径取 `RoutingPath ?? Request.Url` 并去尾斜杠；query 按键名 `Ordinal` 升序排序后以 `k=v` 用 `&` 连接，值为 null 时按空串拼接。无 query 时为 `GET:/users/5`，有 query 时为 `GET:/users/5?a=1&b=2`，因此 `?a=1&b=2` 与 `?b=2&a=1` 同键；无 query 的键与加 query 之前逐字符一致。键在每次读写时即时计算，不存到过滤器实例上，因为过滤器实例被 `RouteTable` 缓存并跨线程复用。

原因：属性路由允许同路径不同动词，且路径参数不会被写进 `Request.Url`（末段数字被 Http 层剥离），继续用 `Request.Url` 会导致 `/users/5` 与 `/users/7` 共用一条缓存。`RoutingPath` 因此必须承载**实际命中路径**，不能是模板，否则参数化路由仍然串号（`/users/{id}` 对 5 和 7 是同一个键）。

已知残余：表单体（`Request.Forms`）没有纳入缓存键，表单内容不同的两次 POST 仍会命中同一条缓存。需要按参数区分时，把参数做成路径参数或 query。

### 8.2 ForbiddenAccessList

不改。它按 `Request.Url` 做子串 + 正则匹配，属性路由命中时 `Request.Url` 仍是原始（剥数字后）路径，行为与现在一致。

### 8.3 WebSocket

WS 升级沿用 `GetActionResult` 的同一条路径，属性路由对 WS 入口直接生效；`MvcInvoker` 里现有的「`WSController` 非 `[WebSocket]` action → 400」「非 `WSController` 的 `[WebSocket]` action → 400」校验不变。

### 8.4 SDK 代码生成器

`Tool/CodeGenerte/ApiMapping.cs` 构建 `Routing` 时填充 `Template` / `HttpMethods`；`Tool/APISdkCodeGenerator.cs` 在 `Template` 非空时输出模板路径（`{name}` 原样保留，由调用方替换），为空时维持 `{base}api/{Controller}/{Action}` 不变。

顺带修复 `ApiMapping.cs` 中 `actionAttrs.ToList().ConvertAll(b => b as IFilter)` 产生 `null` 后 `OrderBy(b => b.Order)` 的 NRE（与本路径直接相关，不修则任何带非 filter 特性的 action 都会让生成器崩）。

### 8.5 启动期构建与生命周期

- 构建点：`SAEAMvcApplication` 构造函数内、`AreaCollection.RegistAll()` 之后，表建好后不可变。
- 构建输入：**本次 `RegistAll` 发现的类型**，不是静态 `RouteTable.Types`（后者跨应用累加，测试进程内多应用会互相污染并误报冲突）。
- 表挂在 `RouteTable` 实例上（每个 `AreaCollection` 一个），随应用生命周期销毁。

## 9. 实现结构

| 文件 | 改动 |
| --- | --- |
| `Src/SAEA.MVC/RouteAttribute.cs` | 新增 |
| `Src/SAEA.MVC/HttpGet.cs`、`HttpPost.cs` | 加可选模板构造与 `Template` 属性 |
| `Src/SAEA.MVC/AttributeRouteTable.cs` | 新增：构建、归一化、冲突校验、匹配、405 判定（internal） |
| `Src/SAEA.MVC/Routing.cs` | 加 `Template`、`HttpMethods` |
| `Src/SAEA.MVC/AreaCollection.cs` | 记录本次发现的类型供构建器使用 |
| `Src/SAEA.MVC/SAEAMvcApplication.cs` | 构造期构建属性路由表并注入 |
| `Src/SAEA.MVC/HttpContext.cs` | 匹配入口前置、`RoutingPath`、路径参数写入 `Parmas`、405 |
| `Src/SAEA.MVC/MVCInvoker.cs` | 两段式 action 的动词校验与 405 + `Allow` |
| `Src/SAEA.MVC/OutputCacheAttribute.cs` | 缓存键改为动词 + 规范路径 + 规范化 query |
| `Src/SAEA.MVC/Tool/**` | 生成器读模板、修 NRE |

## 10. 性能

- 构建期一次性完成，运行期只读，无锁。
- 匹配不使用 `String.Split`、不产生临时数组；路径按索引扫描，段比较用 `string.Compare` 的重载。
- 静态路由 O(1) 字典命中；参数路由按段数分桶，桶内线性。
- 不使用 `Span`（`netstandard2.0` 需额外包），也不引入新依赖。

## 11. 验收

单测落在既有 `Src/SAEA.MVC.Tests`（net10.0 + MSTest，不入 sln），新增用例覆盖：

1. 静态路径命中（含多段前缀）。
2. 路径参数单段命中，并绑定到 action 参数。
3. 路径参数覆盖同名 query。
4. 类级 + 方法级模板拼接。
5. 同路径不同动词分别命中；缺动词返回 405 且带 `Allow`。
6. 同路径同动词冲突 → 应用构造抛异常。
7. 属性路由未命中时两段式仍可用。
8. 单段属性路由 `/health` 命中（当前单段只查静态文件的死角被补上）。
9. 大小写不敏感、尾斜杠等价、`%2F` 越段不匹配。
10. WS 入口用 `[Route]` 声明后可升级。
11. 未命中任何路由 → 404 不变。
12. `OutputCache` 在路径参数下不串号。
13. 两段式路由的动词：只声明 `[HttpGet]` 的 action 收到 POST / PUT 返回 405 且带 `Allow: GET`；只声明 `[HttpPost]` 的收到 GET 返回 405 且带 `Allow: POST`；同时声明两者时 GET / POST 正常执行、PUT 返回 405 且 `Allow` 按 `GET, POST` 顺序列出；未声明任何动词特性的 action 对 GET / POST / PUT 全部放行；`WebConfig.DefaultRoute` 默认路由分支同样按声明拦截。对应用例：`Src/SAEA.MVC.Tests/Tests/TwoSegmentVerbTests.cs`。
14. `OutputCache` 的 query 隔离：同一路径下 query 值不同不共用缓存条目（`?tag=1` 与 `?tag=2` 各自取到自己的结果）；解析顺序不同但内容相同（`?a=1&b=2` 与 `?b=2&a=1`）共用同一条目，返回相同正文。对应用例：`Src/SAEA.MVC.Tests/Tests/OutputCacheQueryTests.cs`。

手工验收：`dotnet run --project Src/SAEA.MVCTest`，访问新增演示控制器的属性路由地址。
构建验收：`dotnet build Src/SAEA.Sockets.sln -c Debug` 0 error；`dotnet test Src/SAEA.MVC.Tests` 全绿。

## 12. 风险

| 风险 | 处置 |
| --- | --- |
| 一个请求里存在「两个路径视图」（属性路由看 `RelativeUrl`，两段式看 `Url`） | 文档写明；两段式行为完全不变 |
| 属性路由优先命中会遮蔽同路径的静态文件 | 显式声明优先，文档写明；用户可用更具体的模板规避 |
| 405 是新状态码 | 属性路由与两段式两条路径都会产生，文案与 `Allow` 格式一致，文档写明 |
| `Routing.Template` 变更影响生成器输出 | 生成器按模板非空分支处理，空模板保持旧输出 |

## 13. 补充修订（2026-11-17）

1. **两段式 `Async` 回退放开到所有 Controller**（`RouteTable.ResolveActions`）：先按地址里的 action 名精确匹配，未命中再尝试 `actionName + "Async"`，`/{Controller}/{Action}` 与 `WebConfig.DefaultRoute` 两条两段式分支都生效，所以 `/test/sleep` 命中 `TestController.SleepAsync`。精确名优先，`Foo` 与 `FooAsync` 同时存在时 `/controller/foo` 命中 `Foo`；回退不限定返回类型；属性路由按模板匹配，与方法名无关，不走这套回退；`WSController` 派生类未 override 时回落到基类 `OpenAsync` 的兜底仍只对其派生类生效。
2. **action 超时的协作式取消与观察兜底**（`MVCInvoker.MethodInvoke`、`HttpContext.RequestAborted`）：每次调用创建一个 `CancellationTokenSource`，声明 `CancellationToken` 形参时由框架注入，否则从 `RequestAborted` 读取；等待超过 action 超时秒数返回 408 并 `Cancel()` 该令牌（action 超时读 `WebConfig.ActionTimeout`，为 0 时沿用 `Timeout`），未完成的 Task 交给观察回调收尾——异常被丢弃，不会再写响应。取消放在线程池执行（`Task.Run` 调 `CancelQuietly`），因为 `CancellationTokenSource.Cancel()` 会在调用线程同步执行用户注册的回调，阻塞的回调会把 408 拖后甚至自锁；取消与任务完成后的释放可能并发，取消回调自身抛出的异常被吞掉。`Timeout` 仍是 `SAEA.Http` 的连接空闲回收时间，只为缩短动作超时而调小它会在 408 写出前回收连接，这种场景应改配 `ActionTimeout`。不观察令牌的同步代码无法被强制终止，会继续在后台跑完。
3. **文案与内容类型修正**：404 的「找不到：Controller/Action 当前请求为:」输出请求行的真实方法（GET/POST/PUT/DELETE），不再是特性名 `HttpGet`/`HttpPost`（`MVCInvoker`）；405 文案改用原始 request-target `Request.RelativeUrl`，数字末段不会再被 `SAEA.Http` 的请求解析剥掉（`HttpContext`）；`ContentResult(string, Encoding)` 的默认内容类型由 `text/plane` 修正为 `text/plain; charset=utf-8`（`ContentResult`）。404/405 正文按 `text/html` 输出，因此拼进正文的 action 名与完整 request-target（含查询串）都先做 HTML 编码。

## 14. 补充修订（2026-11-18）

1. **属性路由路径令牌**：模板段新增 `[controller]`（类名去掉结尾 `Controller` 后缀）与 `[action]`（方法名去掉结尾 `Async` 后缀）两个令牌，令牌识别与后缀剥离都忽略大小写。令牌必须独占一整段：与字面量或花括号混写（`v1-[action]`、`[controller]s`、`{[action]}`）、未知或畸形方括号（`[foo]`、未闭合的 `[controller`）、后缀剥离后为空（类名恰好是 `Controller`、方法名恰好是 `Async`）都在构建期抛 `ArgumentException`，不再把方括号内容当字面静态段静默注册。类级 `[Route]` 可写 `[action]` 并按每个 action 分别替换；替换在属性路由表构建期完成，替换后的绝对路径即 `Routing.Template`，SDK 生成器输出与冲突检测都使用替换后的结果。`Foo` 与 `FooAsync` 同用 `[action]` 会解析到同一路径，动词相同即构建期 `InvalidOperationException` 冲突，需要共存就显式写不同模板或分动词。
