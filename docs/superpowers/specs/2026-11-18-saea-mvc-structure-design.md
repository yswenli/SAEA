# SAEA.MVC 结构重构设计

## 1. 背景与目标

`Src/SAEA.MVC` 现在有 34 个手写 `.cs` 文件、合计 6541 行，全部平铺在工程根目录，只有 `Tool/` 与 `Tool/CodeGenerte/` 两级子目录。文件里出现的命名空间共三个：`SAEA.MVC`（29 个文件）、`SAEA.MVC.Tool`（2 个文件，`APISdkCodeGenerator.cs`、`CodeType.cs`）、`SAEA.MVC.Tool.CodeGenerte`（3 个文件，`ApiMapping.cs`、`CSharpTemple.cs`、`JsTemple.cs`）。

> 行数口径：按 `(Get-Content <文件>).Count` 统计，含空行与注释行。PowerShell 的 `Measure-Object -Line` 不计空行，同一批文件会得到 5611 行，两个数都成立但不可混用；本文统一用前者。

当前状态的两个结构性问题：

- **目录不表达职责。** 六个文件各占 300–1000 行，彼此靠 `using` 平铺引用，读者无法从目录判断「请求管线」「路由匹配」「连接管理」的边界在哪里。
- **巨型类承担多份职责。** 单文件内既有协议细节（握手、编解码队列），又有反射与策略（action 解析、动词拦截、过滤器排序），改动任一处都要在千行文件里定位。

目标只有两条：

1. 目录结构与命名空间反映职责，每个目录映射到同名子命名空间，作者面向的公开类型也随目录进入子命名空间。
2. 把 6 个巨型类按单职责切成小类型，新增类型全部 `internal`，**公开类型名与公开成员签名冻结**（行为变更只有第 5 节切 4 的 `_sweeper` 释放；类型改名只有 `Routing` → `RouteEntry` 一处，见第 4.5 节）。

本次不做性能优化，也不改协议语义；除第 5 节切 4 明确列出的 `_sweeper` 释放外，其余搬迁必须行为等价。

## 2. 约束

| 约束 | 内容 |
| --- | --- |
| 目标框架 | `netstandard2.0`，`LangVersion 8.0`，禁用 C# 9+ 语法（record、init、目标类型 `new`、文件级命名空间等） |
| 可空引用类型 | 不启用，不为局部改动单独开启 |
| 包与依赖 | 不改 `Src/SAEA.MVC/SAEA.MVC.csproj` 的 `<Version>`（当前 `26.11.16.1`），不增删、不升降 `PackageReference`，不动四个 `ProjectReference`（`SAEA.Common`、`SAEA.Http`、`SAEA.Sockets`、`SAEA.WebSocket`） |
| 文件头 banner | 每个改动文件的「命名空间」「文件名」字段按实际内容改写；「修改标记」段整体覆盖为本次修改信息，不追加历史；`唯一标识` 保持原值不变；`CLR版本` 仍为 `netstandard2.0`；`版本号` 随 csproj 现值 |
| 公开 API | 改名与迁移共三处：第 4.5 节的 `Routing` → `RouteEntry` 改名、第 4.2 节的 `WSMessage` / `WSMessageType` 迁入 `SAEA.MVC.WebSockets`，与第 4.2 节的 `HttpGet` / `HttpPost` 改名为 `HttpGetAttribute` / `HttpPostAttribute`；`Hosting/`、`Controllers/`、`Results/`、`Attributes/` 下 22 个类型的命名空间统一迁到同名子命名空间（第 4.3 节）。上述之外，公开类型名与公开成员签名冻结，`MvcInvoker`、`RouteTable` 等名称不改 |
| 行尾与编码 | 改既有文件前先确认其行尾与 BOM 并保持原样，禁止整文件重写造成全文件 diff |
| 命名空间搬迁 | 所有目录都映射同名子命名空间，22 个公开类型随目录迁移，属源码级破坏，消费方必须补 `using` 并重新编译（第 4.3 节、第 6.1 节） |

## 3. 现状问题清单

### 3.1 六个巨型文件的职责混合

六个文件的行数为实测值（口径同第 1 节）：

| 文件 | 行数 | 混合的职责 |
| --- | --- | --- |
| `AttributeRouteTable.cs` | 982 | 模板分词与校验、`[controller]`/`[action]` 令牌替换、路径归一化与逐段解码、段匹配与 `Allow` 收集、路由注册表与冲突检测、`ConditionalWeakTable` 索引 |
| `MVCInvoker.cs` | 672 | 动词判定、过滤器前置/后置反射调用、JSON 体参数注入、参数填充与 `CancellationToken` 注入、超时/取消/异常重抛、WebSocket 入口特例 |
| `MVCWebSocketConnection.cs` | 535 | 收发泵、分片队列与预算、关闭握手、`CallContext` 上下文恢复、控制器回调分发 |
| `WebSocketsFeature.cs` | 460 | 升级请求判定、握手拼装与错误响应、连接注册表、5 分钟清扫计时器、握手期字节暂存 |
| `HttpContext.cs` | 357 | 参数合并、`RequestDelegate` 分流、属性路由派发、两段式路由、静态文件兜底、响应收尾 |
| `RouteTable.cs` | 313 | 反射解析 action、GET/POST 双份过滤器收集、动词特性拦截、未知 action 负缓存 |

### 3.2 GET/POST 的 `BindingFlags` 不一致

- `RouteTable.cs` 的 `RouteTable.GetOrAdd`：GET 分支在第 75 行传 `BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly`；POST 分支在第 183 行传 `BindingFlags.Instance | BindingFlags.Public`，没有 `DeclaredOnly`。
- 后果：`ResolveActions` 第 287 行的 `flags` 参数只影响前两轮 `GetMethods`（第 289、296 行）的可见范围，因此**普通控制器从自定义基类继承的 action 对 GET 不可见**；`WSController` 的四个生命周期方法不受该差异影响——第 302–307 行的兜底分支硬编码 `BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly` 作用在 `typeof(WSController)` 上，**完全不读 `flags` 参数**，所以派生类即使不 override，`GET /{controller}/open` 仍命中基类 `OpenAsync`（`WSController.cs` 第 55–56 行，用例 `Src/SAEA.MVC.Tests/Tests/WebSocketTests.cs` 第 66 行用 GET 升级 `/echo/open` 并断言成功），GET 与 POST 两侧一致。
- 该差异在 `ResolveActions` 的注释里被当成契约写了下来（第 285 行「GET 分支声明层限定，POST 分支包含继承成员」），说明它不是笔误，而是遗留的不对称实现。

本次重构保留该差异，不纳入本次范围（第 5.1 节）。

### 3.3 未知 action 的 null 被负缓存

- `RouteTable.cs` 第 77–81 行与第 185–189 行：`ResolveActions` 返回空列表时 `return null`，注释明确写着「`GetOrAdd` 会把 null 作为该键的缓存值，后续请求直接命中」。
- 覆盖该行为的用例是 `Src/SAEA.MVC.Tests/Tests/HttpRoutingTests.cs` 的 `Controller_UnknownPostAction_Returns404`（第 149 行），循环在第 161–168 行连续发两次 POST，注释写明「第一次解析未命中，第二次走路由缓存」。
- GET 侧只有单请求用例 `Controller_UnknownAction_Returns404`（第 121 行），没有二次请求断言，所以「GET 分支的负缓存」目前仅有实现注释背书、没有用例兜住。

### 3.4 `_sweeper` 静态计时器不释放

- `WebSocketsFeature.cs` 第 57 行声明 `static Timer _sweeper`；第 355–365 行 `EnsureSweeper` 用 `Interlocked.CompareExchange` 保证只建一个 5 分钟周期的计时器。
- 第 345–353 行 `StopAll()` 只对 `_connections` 逐个 `Abort()` 再 `Clear()`，既不 `Dispose` 计时器也不把 `_sweeper` 置空。
- 后果：应用 `Stop()`（`SAEAMvcApplication.cs` 第 252–265 行，第 256 行调用 `WebSocketsFeature.StopAll()`）之后计时器继续每 5 分钟触发一次空遍历；同一进程内反复 `Start`/`Stop` 时计时器不会重建，但也不归还，属于宿主生命周期泄漏。

### 3.5 上下文经字符串键存取

- `MVCWebSocketConnection.cs` 第 509–518 行：`SetCurrentContext()` 在第 512 行写 `CallContext<IHttpContext>.SetData("ContextBase.Current", _context)`，`ClearCurrentContext()` 在第 517 行写 `null`。
- 对端读写同样硬编码该字符串：`Src/SAEA.Http/Base/HttpContextBase.cs` 第 122 行 `GetData("ContextBase.Current")`、第 144 行 `SetData("ContextBase.Current", this)`。
- 问题不是「不该有上下文恢复」，而是键名散落在两个程序集、四个位置，字符串拼错不会有编译期错误；`MVCWebSocketConnection` 自身也因此离不开 `SAEA.Http.Base` 的实现细节。

### 3.6 测试工程无法触达 internal 类型

- `Src/SAEA.MVC/SAEA.MVC.csproj` 全文没有 `InternalsVisibleTo`，程序集也未强签名（`SignAssembly` 为 `False`）。
- 测试因此改用反射绕行：`Src/SAEA.MVC.Tests/Tests/AttributeRouteBuildTests.cs` 第 177–196 行的 `BuildAttributeRouteTable` 在第 179 行写 `typeof(SAEAMvcApplication).Assembly.GetType("SAEA.MVC.AttributeRouteTable", true)`；`Src/SAEA.MVC.Tests/Tests/RouteTokenTests.cs` 第 323–337 行重复了同一套反射。
- 后果：切分出的 12 个新 internal 类型若继续靠反射测试，用例会退化成「字符串 + 方法名」断言，重构时无法靠编译器兜住；且第 4.2 节、第 6.1 节的类型搬迁（`AttributeRouteTable` 迁入 `SAEA.MVC.Routing`）会让第 179 行与第 325 行的字符串同时失效。

### 3.7 文件名与类型名不一致

- `Src/SAEA.MVC/MVCInvoker.cs` 第 48 行声明的是 `public static class MvcInvoker`，banner 的「文件名」字段（第 15 行）写的是 `MVCInvoker`。
- 同一工程内 `MVCWebSocketConnection.cs` 与 `WebSocketsFeature.cs` 的文件名与类型名是一致的，所以 `MVCInvoker.cs` 属于孤例，按「文件名与类型名一致」的仓库规则应改名为 `MvcInvoker.cs`（类型名不动）。

### 3.8 附带发现：一个无调用方的私有包装

`MVCInvoker.cs` 第 376–379 行的 `static ActionResult MethodInvoke(...)` 全仓没有调用方（唯一命中就是该声明本身），实际入口都走第 389 行的 `MethodInvokeAsync`。第 5 节切 3 时应一并删除。

### 3.9 已核实不成立的一条：过滤器排序不存在两种语义

初看 GET 与 POST 分支的写法不同，容易被判成语义分叉，实测**结果等价**，不构成缺陷：

| 位置 | GET（`RouteTable.cs`） | POST（`RouteTable.cs`） |
| --- | --- | --- |
| 类级过滤器 | 第 97–101 行：`Where(...).ToList().ConvertAll(b => b as IFilter)` 后再 `.OrderBy(b => b.Order)` | 第 205–209 行：`ConvertAll(b => b as IFilter).OrderBy(b => b.Order)` |
| 方法级过滤器 | 第 136–140 行：`filterAttrs.ConvertAll(...)` 后再 `filters.OrderBy(b => b.Order)` | 第 243 行：`filterAttrs.ToList().ConvertAll(...).OrderBy(b => b.Order)` |

等价依据：`Where`/`ToList` 保持原顺序，`ConvertAll` 也保持原顺序，只有 `OrderBy` 会重排；两边的排序键都是 `IFilter.Order`，而 LINQ `OrderBy` 是稳定排序，因此「先转换后排序」与「先排序后转换」得到同一序列。切 1 合并实现时按任一种写法落地都不会改变行为，不需要为它写行为变更说明。

## 4. 目标结构

### 4.1 目录树

新增文件用「新增」标注，其余为现有文件搬迁。

```
Src/SAEA.MVC/
├─ Hosting/
│  ├─ SAEAMvcApplication.cs
│  ├─ SAEAMvcApplicationConfig.cs
│  ├─ SAEAMvcApplicationConfigBuilder.cs
│  ├─ HttpContext.cs
│  └─ MvcInvoker.cs                       （原 MVCInvoker.cs 改名，类型名不变）
├─ Controllers/
│  ├─ Controller.cs
│  └─ WSController.cs
├─ Results/
│  ├─ ActionResult.cs
│  ├─ JsonResult.cs
│  ├─ ContentResult.cs
│  ├─ DataResult.cs
│  ├─ FileResult.cs
│  ├─ BigDataResult.cs
│  ├─ EventStream.cs
│  └─ EmptyResult.cs
├─ Attributes/
│  ├─ IFilter.cs
│  ├─ ActionFilterAttribute.cs
│  ├─ OutputCacheAttribute.cs
│  ├─ HttpGetAttribute.cs                  （原 HttpGet.cs 改名，类型改名 HttpGet → HttpGetAttribute）
│  ├─ HttpPostAttribute.cs                 （原 HttpPost.cs 改名，类型改名 HttpPost → HttpPostAttribute）
│  ├─ RouteAttribute.cs
│  └─ WebSocketAttribute.cs
├─ Routing/
│  ├─ RouteTable.cs
│  ├─ RouteEntry.cs                        （原 Routing.cs 改名，类型改名 Routing → RouteEntry）
│  ├─ AreaCollection.cs
│  ├─ AttributeRouteTable.cs
│  ├─ AttributeRouteEntry.cs               （新增，原嵌套 Entry）
│  ├─ RouteTemplateParser.cs               （新增）
│  ├─ RoutePath.cs                         （新增）
│  └─ RouteSegmentMatcher.cs               （新增）
├─ Invocation/
│  ├─ ActionResolver.cs                    （新增）
│  ├─ FilterCollector.cs                   （新增）
│  ├─ ActionInvoker.cs                     （新增）
│  ├─ ParameterBinder.cs                   （新增）
│  ├─ VerbPolicy.cs                        （新增）
│  └─ RequestPipeline.cs                   （新增）
├─ WebSockets/
│  ├─ WebSocketsFeature.cs
│  ├─ MVCWebSocketConnection.cs
│  ├─ WSMessage.cs                         （含 WSMessageType）
│  ├─ WebSocketHandshake.cs                （新增）
│  └─ WebSocketConnectionRegistry.cs       （新增）
├─ Tool/
│  ├─ APISdkCodeGenerator.cs
│  ├─ CodeType.cs
│  └─ CodeGenerte/
│     ├─ ApiMapping.cs
│     ├─ CSharpTemple.cs
│     └─ JsTemple.cs
├─ README.md
├─ README.en.md
└─ SAEA.MVC.csproj
```

### 4.2 目录、文件与命名空间对照

| 目录 | 文件 | 命名空间 |
| --- | --- | --- |
| `Hosting/` | `SAEAMvcApplication`、`SAEAMvcApplicationConfig`、`SAEAMvcApplicationConfigBuilder`、`HttpContext`、`MvcInvoker` | `SAEA.MVC.Hosting` |
| `Controllers/` | `Controller`、`WSController` | `SAEA.MVC.Controllers` |
| `Results/` | `ActionResult`、`JsonResult`、`ContentResult`、`DataResult`、`FileResult`、`BigDataResult`、`EventStream`、`EmptyResult` | `SAEA.MVC.Results` |
| `Attributes/` | `IFilter`、`ActionFilterAttribute`、`OutputCacheAttribute`、`HttpGetAttribute`、`HttpPostAttribute`、`RouteAttribute`、`WebSocketAttribute` | `SAEA.MVC.Attributes` |
| `Routing/` | `RouteTable`、`RouteEntry`（原 `Routing`）、`AreaCollection`、`AttributeRouteTable`，新增 `AttributeRouteEntry`、`RouteTemplateParser`、`RoutePath`、`RouteSegmentMatcher` | `SAEA.MVC.Routing` |
| `Invocation/` | 新增 `ActionResolver`、`FilterCollector`、`ActionInvoker`、`ParameterBinder`、`VerbPolicy`、`RequestPipeline` | `SAEA.MVC.Invocation` |
| `WebSockets/` | `WebSocketsFeature`、`MVCWebSocketConnection`、`WSMessage`（含 `WSMessageType`），新增 `WebSocketHandshake`、`WebSocketConnectionRegistry` | `SAEA.MVC.WebSockets` |
| `Tool/` | `APISdkCodeGenerator`、`CodeType` | `SAEA.MVC.Tool` |
| `Tool/CodeGenerte/` | `ApiMapping`、`CSharpTemple`、`JsTemple` | `SAEA.MVC.Tool.CodeGenerte` |

归属核对：上表 9 个目录合计 34 个现有文件，与第 1 节的实测文件数一致，每个文件只出现在一处，无重复、无遗漏。

> 目录名说明：指令给的目标目录写作 `Tool/CodeGenerate/`，现盘实际是 `Tool/CodeGenerte/`，且该目录下三个文件的命名空间是 `SAEA.MVC.Tool.CodeGenerte`。按「不改 `CodeGenerte` / `Temple` 拼写」的边界，本次**保留现盘目录名与命名空间**，只做搬迁；把目录名改成 `CodeGenerate` 会让目录与命名空间继续错位，属另立条目处理。

### 4.3 决策反转：四个作者面向目录同样映射子命名空间

原方案给 `Hosting/`、`Controllers/`、`Results/`、`Attributes/` 四个目录开了例外，让其中的类型统一留在 `SAEA.MVC`。**2026-11-19 决定取消该例外**：四个目录与其他目录一样映射同名子命名空间，`SAEA.MVC` 根命名空间下不再保留任何类型。

- 取消例外的理由：保留例外时「目录 = 命名空间」只能写成带例外的规则，读者按目录推断命名空间会推错，按目录补 `using` 的直觉也对不上；取消后规则是一句话——所有目录都映射同名子命名空间，没有例外（第 4.2 节）。
- 影响范围：22 个公开类型随目录迁移——`Controllers/` 的 `Controller`、`WSController`，`Results/` 的 `ActionResult`、`JsonResult`、`ContentResult`、`DataResult`、`FileResult`、`BigDataResult`、`EventStream`、`EmptyResult`，`Attributes/` 的 `IFilter`、`ActionFilterAttribute`、`OutputCacheAttribute`、`HttpGetAttribute`、`HttpPostAttribute`、`RouteAttribute`、`WebSocketAttribute`，`Hosting/` 的 `SAEAMvcApplication`、`SAEAMvcApplicationConfig`、`SAEAMvcApplicationConfigBuilder`、`HttpContext`、`MvcInvoker`。
- 代价：这是对外可见的破坏性变更，消费方只换包版本不够，必须为每个引用的类型补对应子命名空间的 `using` 后重新编译（第 6.1 节）；`Src/SAEA.Common/ConstHelper.cs` 的两条反射字符串同步改为新全名。因此本次变更需要升包版本号，并在发布说明里把命名空间重组单列为破坏性条目；版本号由版本管理方统一调整，不在本次代码改动里顺手改。
- 连带口径：`SAEA.MVC` 根命名空间现已为空，`using SAEA.MVC;` 仍可编译但不再引入任何类型，文档示例一律改为写实际用到的子命名空间。

### 4.4 同文件保留的类型

不机械按「一文件一类型」拆分强绑定类型，以下三处同文件保留：

| 文件 | 同文件类型 | 保留理由 |
| --- | --- | --- |
| `WebSockets/WSMessage.cs` | `WSMessageType`（第 40 行）与 `WSMessage`（第 56 行） | 枚举只服务 `WSMessage.Type`，拆开后读者要在两个文件间来回对照，且 `WSMessage` 的构造函数只接受该枚举 |
| `Attributes/OutputCacheAttribute.cs` | `OutputCacheManager`（第 136 行，internal static） | 管理器只被该特性使用，拆开会让 internal 细节多一个公开入口文件 |
| `Tool/CodeGenerte/CSharpTemple.cs` | `CSharpTemple`（第 41 行）与 `SaeaApiSdk`（第 57 行） | 模板文本与模板产出类型同源，改一处必看另一处；两者都在 `SAEA.MVC.Tool.CodeGenerte` |

### 4.5 类型与命名空间不可同名

类型 `Routing` 与命名空间 `SAEA.MVC.Routing` 的完全限定名会撞成同一个字符串，**这不是合法写法，用全名也规避不了**。最小复现实测（`net10.0`，同一程序集两个文件）：

```csharp
// A.cs
namespace A { public class B { } }

// B.cs
namespace A.B { public class C { public B field; } }
```

编译结果为 `error CS0101: 命名空间"A"已经包含"B"的定义`。原因：声明 `namespace A.B` 时编译器先解析限定名 `A.B`，而该位置已经是类型，命名空间无法建立；反向同理，`SAEA.MVC.Routing` 会被优先解析成命名空间，类型引用写全名也解析不到。

因此本条裁决：**类型 `Routing` 改名为 `RouteEntry`，文件 `Routing.cs` 改名为 `RouteEntry.cs`，命名空间仍为 `SAEA.MVC.Routing`。** 命名依据是它的实际内容——它是单条 action 的路由条目（控制器实例、`MethodInfo`、过滤器列表、`ParmaTypes`、`Template`、`HttpMethods`），叫 `Routing` 会被误读成「路由系统」；`ParmaTypes` 属既有公开成员名，拼写本次不动。

**这是全文唯一一处公开类型改名**，其余公开类型名（含 `RouteTable`、`AreaCollection`、`AttributeRouteTable`）与公开成员签名全部冻结。受影响的引用处与文档按第 6.1 节、第 6.2 节处理。

## 5. 六条切割线

| 序号 | 切割线 | 涉及文件与符号（行号为核对值） | 新增 internal 类型 | 行为等价性 |
| --- | --- | --- | --- | --- |
| 1 | 两段式路由的 action 解析与过滤器收集 | `RouteTable.cs`：`GetOrAdd`（61）、GET 分支（71–178）、POST 分支（179–268）、`ResolveActions`（287–310） | `ActionResolver`、`FilterCollector` | **无行为变更**：两段重复实现合并为一处，`BindingFlags` 差异按 `isPost` 入参保留（第 5.1 节）。写键结构两侧在可达路径上等价，以用例锁定现状。过滤器排序属等价（第 3.9 节） |
| 2 | 属性路由表的模板、路径、段匹配三分 | `AttributeRouteTable.cs`：`Build`（94–142）、`From`（149–156）、`Match`（164–226）、`Add`（550–590）、`CreateEntry`（242–329）、`SplitTemplate`（417–442）、`ValidateSegment`（450–487）、`ReplaceTokens`（339–354）、`TrimSuffix`（365–374）、`NormalizePath`（630–659）、`BuildNormalizedPath`（661–691）、`TryDecodePath`（699–729）、`CountSegments`（731–743）、`TryMatchSegments`（782–830）、`TryDecodeSegment`（840–866）、`CollectAllow`（868–887）、`TryGetDeclaredRoute`（384–409） | `AttributeRouteEntry`、`RouteTemplateParser`、`RoutePath`、`RouteSegmentMatcher` | 纯搬迁，行为等价 |
| 3 | 调用层：动词策略、参数绑定、方法执行 | `MVCInvoker.cs`：`InvokeResult`（59–62）、`InvokeResultAsync`（73–290）、`GetAllowedMethods`（297–324）、`IsMethodAllowed`（332–340）、`GetActionTimeoutSeconds`（359–366）、`MethodInvoke`（376–379，无调用方）、`MethodInvokeAsync`（389–534）、`WaitWithTimeoutAsync`（542–563）、`RethrowLikeWait`（569–576）、`FillParameters`（585–634）、`CancelQuietly`（640–654）、`ObserveAndDispose`（661–670）、`Unwrap`（345–353） | `VerbPolicy`、`ParameterBinder`、`ActionInvoker` | HTTP 语义等价；差异仅在「上下文由静态环境读取」改为「入口取一次后显式传参」 |
| 4 | WebSocket 握手与连接表 | `WebSocketsFeature.cs`：`_connections`（54–55）、`_sweeper`（57）、`AcceptWebSocketAsync`（124–221）、`Register`（306–320）、`Unregister`（322–327）、`OnDisconnected`（332–340）、`StopAll`（345–353）、`EnsureSweeper`（355–365）、`Sweep`（367–383）、`ComputeAcceptKey`（389–396）、`BuildHandshake`（398–413）、`WriteError`（415–456） | `WebSocketHandshake`、`WebSocketConnectionRegistry` | **有行为变更**：`StopAll()` 释放计时器并置空 |
| 5 | 请求管线外提 | `HttpContext.cs`：`HttpHandleAsync`（112–191）、`GetActionResultAsync`（206–355） | `RequestPipeline` | 等价，前提是第 5.5 节列出的分支判断全部保留 |
| 6 | MVC 结果类型与 `SAEA.Http.Model.Http*Result` 的接缝 | `Results/` 全部结果类型与 `Src/SAEA.Http/Model/` 的 `HttpActionResult`、`HttpContentResult`、`HttpFileResult`、`HttpBigDataResult`、`HttpEmptyResult` | 无 | 本次不做，只记录为独立立项（跨包公开类型变更） |

### 5.1 切 1：`RouteTable` 拆 `ActionResolver` 与 `FilterCollector`

`RouteTable.GetOrAdd` 现在是「GET 一份、POST 一份」的近 100 行重复体（第 71–178 行与第 179–268 行），差异集中在三处：`BindingFlags`；`IsPost` 初值交换（第 114 行与第 222 行，配套的是写键时选 `routing` 还是 `routing2`）；写键的结构（真正无条件写双键的是 GET 第 161–166、168–173 两处，第 154–159 行不可达，理由见下面第 2 条；POST 在第 245–257 行只在命中 `dGet` / `dPost` 时各写一个键）。

- `ActionResolver`：承载 `ResolveActions`（第 287–310 行）与 `BindingFlags` 策略。精确名优先（第 289–292 行）、`Async` 后缀回退（第 294–299 行）、`WSController` 基类声明层兜底（第 302–307 行）全部保留原顺序。
- `FilterCollector`：承载类级（第 93–103 行 / 第 201–211 行）与方法级（第 128–140 行 / 第 235–243 行）过滤器收集，输出已按 `Order` 升序的 `List<IFilter>`。**只负责收集，不负责执行**：`OnActionExecuting` / `OnActionExecuted` 的反射调用仍在 `MvcInvoker` 的公开入口内（第 130–155 行、第 259–287 行），避免把「策略」与「管道」混进同一个类型。
- 合并方式：GET/POST 双份实现收成一处，`isPost` 入参只决定传给 `ResolveActions` 的 `BindingFlags`，键名与 `IsPost` 初值沿用两侧既有取值。这是机械合并，不是语义统一。

两处必须写进变更说明：

1. **合并只去掉重复实现，`DeclaredOnly` 差异原样保留。** POST 传 `BindingFlags.Instance | BindingFlags.Public`（第 183 行现值），GET 传 `BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly`（第 75 行现值），两侧行为与合并前完全一致：普通控制器从自定义基类继承的 action 仍只对 POST 可见；`WSController` 的兜底分支（第 302–307 行）不读 `flags`，两侧行为同样不变。不统一为单一语义的理由：
   - 统一为 GET 语义（两边都带 `DeclaredOnly`）会让「在自定义基类里写 CRUD」的控制器 POST 从可用变 404，属功能回退。
   - 统一为 POST 语义（两边都不带 `DeclaredOnly`）会让 `GetMethods(Instance | Public)` 把 `object` 的 `ToString`、`Equals`、`GetHashCode`、`GetType` 一并纳入，`/{controller}/tostring` 这类地址变成 GET 可达（POST 今天已经如此，因为第 289–290 行只按名字匹配、没有 `DeclaringType` 过滤），属可达面扩大。
   - 语义统一单独立项，不混在结构重构里。
2. **写键结构两侧在可达路径上等价，按 POST 分支结构落地。** 真正无条件写双键的是 GET 第 161–166、168–173 两处；第 154–159 行不可达——第 134 行已限定 `filterAttrs.Count > 0`，第 136 行 `filterAttrs.ConvertAll(...)` 保序保长度，因此第 138 行 `filters.Any()` 恒为真，该分支永远走不到，合并后随之消失，不算行为变更。POST 只在命中 `dGet` / `dPost` 时各写一个键（第 245–257 行）。但 `_cDic.GetOrAdd` 会把工厂返回的 `routing` 缓存到当前键，所以 POST 分支缺 `TryAdd` 时仍能凭本键缓存命中，**不能从代码直接断言「POST 由 404 变 405」**；两份实现在「返回哪个 `RouteEntry`」上也还会因 `IsPost` 初值交换而互换对象。合并时按 POST 分支结构落地（少一层不可达判断），两种结构在可达路径上等价，用第 7.2 节 `RouteTable_HttpGetWithoutFilters_PostStillReturns405` 之类的用例把真实行为锁住，不把它写成发布级行为变更。

### 5.2 切 2：`AttributeRouteTable` 拆三个纯函数类型

主类只留四个入口、查询入口 `TryGetDeclaredRoute`、匹配结果助手 `Found`、注册表与排序/比较两把辅助：

- `Build`（第 94–142 行，含 `ConditionalWeakTable` 注册）
- `From`（第 149–156 行）
- `Match`（第 164–226 行，保留静态路径优先、参数桶按段数分桶、路径命中但动词不符回 405 的三段结构）
- `Add`（第 550–590 行，含冲突检测与静态/参数分桶）
- `Found`（第 228–238 行，构造 `MatchResult`，只被 `Match` 的第 190、214 行调用，随 `Match` 留在主类）
- `TryGetDeclaredRoute`（第 384–409 行，`ApiMapping` 调用的内部查询入口，签名冻结）

外提部分：

| 新类型 | 承接符号（行号） |
| --- | --- |
| `RouteTemplateParser` | `SplitTemplate`（417–442）、`ValidateSegment`（450–487）、`ReplaceTokens`（339–354）、`TrimSuffix`（365–374）、`BuildTemplate`（489–502）、`BuildShape`（509–528）、`BuildSegments`（530–548）、`CreateEntry`（242–329）中与模板相关的部分 |
| `RoutePath` | `NormalizePath`（630–659）、`BuildNormalizedPath`（661–691）、`TryDecodePath`（699–729）、`CountSegments`（731–743）、`TryReadSegment`（753–775） |
| `RouteSegmentMatcher` | `TryMatchSegments`（782–830）、`SegmentEquals`（777–780）、`TryDecodeSegment`（840–866）、`CollectAllow`（868–887） |

类型归属：

- 原嵌套 `Entry`（第 904–954 行）外提为顶层 `AttributeRouteEntry`，原嵌套 `Segment`（第 894–899 行）随之内嵌到 `AttributeRouteEntry` 里，因为它只被 `Segments` 数组使用。
- `MatchResult`（第 959–980 行）**保持嵌套**在 `AttributeRouteTable` 内：它是匹配结果，只有 `Match` 返回、`HttpContext` 消费，外提反而多一个 internal 入口。
- `SortParameterRoutes`（592–598）与 `CompareParameterEntry`（600–605）留在主类，属注册表内部排序。
- `Describe`（第 616–619 行）生成 `SplitTemplate` 的「来源」描述（第 113、264、397 行），归 `RouteTemplateParser`；`GetAttribute<T>`（第 607–614 行）是 `Build` / `CreateEntry` / `TryGetDeclaredRoute` 共用的取特性助手（第 108、244、246、248、394 行），留在主类。主类仍在调用 `Describe`：`Build` 第 113 行与 `TryGetDeclaredRoute` 第 397 行，因此 `Describe` 在 `RouteTemplateParser` 上须声明为 `internal static`，主类以 `RouteTemplateParser.Describe(...)` 调用，不复制实现。

本切**行为等价**，不得改动任何异常消息文本（第 267、371、456、470、477、485、561 行）与匹配优先级。

### 5.3 切 3：`MVCInvoker` 拆 `VerbPolicy`、`ParameterBinder`、`ActionInvoker`

公开面冻结：`MvcInvoker.InvokeResult`（第 59–62 行）与 `MvcInvoker.InvokeResultAsync`（第 73–290 行）的名称、参数表、返回类型不变，签名行为也保持不变（同步入口继续是 `GetAwaiter().GetResult()`）。

- `VerbPolicy`：`GetAllowedMethods`（297–324）、`IsMethodAllowed`（332–340）。按 `is HttpGetAttribute` / `is HttpPostAttribute` 类型判断，取代原先的 `GetType().Name` 与 `ConstHelper.HTTPGET` / `HTTPPOST` 字符串比较；允许方法与 405 判定结果不变。
- `ParameterBinder`：`FillParameters`（585–634）。`CancellationToken` 类型的参数不走 `ParamsHelper.FillPamars`，而是先剔除再按原声明顺序插回（第 609–633 行），这段必须原样保留。
- `ActionInvoker`：`MethodInvokeAsync`（389–534）、`WaitWithTimeoutAsync`（542–563）、`RethrowLikeWait`（569–576）、`CancelQuietly`（640–654）、`ObserveAndDispose`（661–670）、`GetActionTimeoutSeconds`（359–366）。超时返回 408 的两段文案（第 477、506 行）、`AggregateException` 的复现形态（第 438、457 行）、`cts` 的 `handedToTimeout` 释放协议（第 405、499、516 行）全部保留。

依赖获取方式的变化：公开入口在开头从 `HttpContext.Current` 取一次上下文（`Request`、`Response`、`WebSockets`、`WebConfig`），随后把需要的对象显式传给三个新类型，新类型内部不再读静态环境。这属实现细节调整，HTTP 语义不变。

`Unwrap`（345–353）随 `ActionInvoker` 走；`MethodInvoke`（376–379）无调用方，直接删除（第 3.8 节）。

### 5.4 切 4：`WebSocketsFeature` 拆握手与连接表

- `WebSocketHandshake`：`ComputeAcceptKey`（389–396）、`BuildHandshake`（398–413）、`WriteError`（415–456），以及 `AcceptWebSocketAsync` 里的协议校验段（第 135–158 行：GET 校验、`IsWebSocketRequest`、版本 13、101 拼装）。`AcceptGuid` 常量（第 52 行）随之下移。
- `WebSocketConnectionRegistry`：`_connections`（54–55）、`_sweeper`（57）、`Register`（306–320）、`Unregister`（322–327）、`OnDisconnected`（332–340）、`StopAll`（345–353）、`EnsureSweeper`（355–365）、`Sweep`（367–383）。
- `WebSocketsFeature` 保留「一个 HTTP 上下文一份」的职责：`_context`、`_gate`、`_controller`、`_connection`、`_pending`、`_draining`、`_earlyError`，以及 `IsWebSocketRequest`（89–114）、`Current`（119）、`Attach`（81–84）、`HandleActionException`（226–240）、`OnRawData`（242–259）、`DrainPending`（264–284）、`OnRawError`（286–302）。
- `MVCWebSocketConnection` 保持连接对象职责，不改对外形状（`IWebSocketConnection` 实现、`MaxQueuedBytes` 常量、`OnData`、`SendAsync`、`CloseAsync`、`Abort`、`Fail`、`HandleDisconnected`）。第 3.5 节的 `CallContext` 字符串键本次不动，只是把它记成独立的清理项。

**行为变更**：`StopAll()`（345–353）在 `Abort` 与 `Clear` 之后补 `_sweeper?.Dispose()` 并把 `_sweeper` 置 `null`，使下一次 `Register` 能重建计时器、宿主停止后不再遗留 5 分钟空转。这是本次唯一涉及资源生命周期的变更。

### 5.5 切 5：`HttpContext` 拆 `RequestPipeline`

`HttpContext` 只保留 `HttpContextBase` 适配：构造（第 60–65 行绑定 `RouteTable` 与 `AttributeRouteTable`）、`RoutingPath`（70）、`RequestAborted`（78）、`WebSockets` 懒加载（80–94）、`HttpHandle`（101–104）、`GetActionResult`（197–200）。

`RequestPipeline` 承接两段：

- 请求段（`HttpHandleAsync`，112–191）：参数合并（127–140）→ `RequestDelegate` 分流（141–144）→ 结果归一（158–168）→ 收尾（171–189）。
- 路由段（`GetActionResultAsync`，206–355）：禁止访问清单（213–228）→ 属性路由优先匹配与 405（230–262）→ 两段式 0/1/default 分支（264–352）。

等价的前提条件（这些分支一条都不能合并或提前返回）：

| 分支 | 位置 | 必须保持的行为 |
| --- | --- | --- |
| 异常兜底 | 158–168 | `WebConfig.ExceptionHandler` 为 null 或本身返回 null 时，`result` 落为 500 的 `ContentResult`；文案「请求发生异常：」逐字不变 |
| 收尾跳过 | 171 | `result is IBigDataResult` 或 `result is IEventStream` 时不 `SetCached`、不 `Send`/`End` |
| 已升级/已手工写 | 171 | `Response.IsUpgraded` 或 `Response.IsHandled` 时同样跳过收尾 |
| 连接策略 | 174–188 | `SetCached(result, Session.CacheCalcString)` 后按 `ApplyConnectionPolicy(Request.Protocal, Connection 头)` 决定 `SendAsync` 还是 `EndAsync` |
| 文件名兜底 | 264–352 | `arr.Length` 为 0/1/default 三支各自的静态文件与 404 文案不变 |

### 5.6 切 6：与 `SAEA.Http.Model` 接缝，本次不做

`Results/` 下的 `ActionResult`、`JsonResult`、`ContentResult`、`DataResult`、`FileResult`、`BigDataResult`、`EventStream`、`EmptyResult` 与 `Src/SAEA.Http/Model/` 下的 `HttpActionResult`、`HttpContentResult`、`HttpFileResult`、`HttpBigDataResult`、`HttpEmptyResult` 存在概念重叠（两套结果类型各自实现 `IHttpResult`）。合并会改动跨包的公开类型体系与 `Response.SetCached` 的输入契约，风险与本次结构重构不同量级，**只记录为独立立项**，本次不动。

## 6. 消费者影响与文档同步

### 6.1 受影响工程（核对结果）

| 工程 | 类型 | 实际需要改动 | 核对依据 |
| --- | --- | --- | --- |
| `Src/SAEA.MVC.Tests` | SDK 式，`net10.0`，在 sln 内 | 2 个文件补 `using SAEA.MVC.Routing;`：`Tests/AttributeRouteBuildTests.cs`（第 157、162、189 行显式写 `RouteTable`）、`Tests/RouteTokenTests.cs`（第 335 行 `new RouteTable()`）；2 处反射字符串改为 `SAEA.MVC.Routing.AttributeRouteTable`：`AttributeRouteBuildTests.cs` 第 179 行、`RouteTokenTests.cs` 第 325 行；另有 2 个控制器补 `using SAEA.MVC.WebSockets;`：`Controllers/EchoController.cs`（第 48 行）、`Controllers/RouteWsController.cs`（第 59 行），因为 `MessageAsync(WSMessage message)` 的参数类型迁了命名空间；测试控制器里的 `[HttpGet]` 已统一写成 `[HttpGetAttribute]`（等价写法），`[HttpGet]` 简写仍可用。四个作者面向目录改子命名空间后，本工程所有直接书写这些类型的文件（`Controllers/` 与 `Attributes/` 下的全部文件，以及 `Tests/` 里声明 `ActionResult`、调用 `SAEAMvcApplication` / `HttpContext` 的用例）还要补 `SAEA.MVC.Controllers` / `SAEA.MVC.Results` / `SAEA.MVC.Attributes` / `SAEA.MVC.Hosting` 中实际用到的 using | 逐文件检索 `RouteTable` / `AttributeRouteTable` 得到的命中共 2 个文件；检索 `Controller` / `ActionResult` / 特性命中 `Controllers/`、`Attributes/` 全部文件与部分测试文件 |
| `Src/SAEA.MVCTest` | 旧式 csproj，`net4.6.2`，在 sln 内 | `Controllers/ChatController.cs` 第 165 行的 `MessageAsync(WSMessage message)` 需要补 `using SAEA.MVC.WebSockets;`；四个作者面向目录改子命名空间后，`Attrubutes/` 的 2 个过滤器、`Controllers/` 的 13 个控制器与 `Program.cs`（共 16 个文件）要补 `SAEA.MVC.Controllers` / `SAEA.MVC.Results` / `SAEA.MVC.Attributes` / `SAEA.MVC.Hosting` 中实际用到的 using；其余 6 个文件不写这些类型，无需改。全工程没有文件显式写出 `RouteTable` / `Routing` / `AreaCollection` / `AttributeRouteTable` / `WebSocketsFeature`，唯一涉及路由的 `Controllers/SaeaSdkTestController.cs` 用 `var` 接 `ApiMapping.GetMapping()`（第 36 行）并只做成员访问 | 检索 22 个 `.cs`：`RouteTable` / `Routing` / `AreaCollection` / `AttributeRouteTable` / `WebSocketsFeature` 无命中，`Controller` / `ActionResult` / 特性命中 16 个文件 |
| `Src/SAEA.Mvc.ServiceTest` | 旧式 csproj，`net4.6.2`，在 sln 内 | 2 个文件要补 using：`Controllers/HomeController.cs` 写 `Controller` 与 `ActionResult`，需 `using SAEA.MVC.Controllers;` 与 `using SAEA.MVC.Results;`；`Service1.cs` 写 `SAEAMvcApplication` / `SAEAMvcApplicationConfig` / `SAEAMvcApplicationConfigBuilder`，需 `using SAEA.MVC.Hosting;`。`Program.cs` 不写这些类型，无需改；原 `using SAEA.MVC;` 可以保留，但它已不引入任何类型 | 检索 14 个 `.cs` 后命中 2 个文件 |
| `Src/SAEA.MVC`（库内调用方） | 库工程自身，`netstandard2.0` | 补 `using SAEA.MVC.Routing;` 的 5 个文件：`MvcInvoker.cs`（第 59、73 行公开签名里的 `RouteTable`）、`HttpContext.cs`（第 52、54、62、64 行，以及第 292、294、323、325 行的 `RouteTable.Types`）、`SAEAMvcApplication.cs`（第 49、121、123、131、135 行的 `AreaCollection` 与 `AttributeRouteTable`）、`Tool/CodeGenerte/ApiMapping.cs`（第 45、52 行 `AreaCollection`、第 65 行 `RouteTable.Types`、第 113 行 `AttributeRouteTable.TryGetDeclaredRoute`）、`Tool/APISdkCodeGenerator.cs`（第 53、70、147、212 行的 `Routing`，位于 `namespace SAEA.MVC.Tool`，类型改名后为 `RouteEntry`）；补 `using SAEA.MVC.WebSockets;` 的 4 个文件：`MvcInvoker.cs`（第 108 行 `WebSocketsFeature`）、`HttpContext.cs`（第 80、90 行 `WebSocketsFeature`）、`SAEAMvcApplication.cs`（第 138、256 行 `WebSocketsFeature`）、`Controllers/WSController.cs`（第 67 行 `MessageAsync(WSMessage message)`），即前三个文件两个 `using` 都要补；改类型名 `Routing` → `RouteEntry`：`RouteTable.cs`（第 46、61、65、107、117、215、225 行，同命名空间无需 using）、`Tool/CodeGenerte/ApiMapping.cs`（第 59、61、89、99、232 行）、`Tool/APISdkCodeGenerator.cs`（第 53、70、147、212 行，含第 29 行 banner 描述里的 `Routing.Template`）；`RouteTable`、`MvcInvoker`、`ApiMapping` 的动词判定改为按 `HttpGetAttribute` / `HttpPostAttribute` 类型判断 | 在 `Src/SAEA.MVC` 内逐文件检索 `RouteTable` / `Routing` / `AreaCollection` / `AttributeRouteTable` / `WebSocketsFeature` 得到的命中共 5 个文件；`WSMessage` 引用另有 4 处，见同行 |

因此改动面比「三个消费工程各补 using」的原预期更宽：`Routing/`、`WebSockets/` 的迁移仍以上表逐文件列出的位置为准，而四个作者面向目录改子命名空间后，凡是直接书写 `Controller`、`WSController`、`ActionResult` 及结果类型、`SAEAMvcApplication` 及其配置类、`HttpContext`、`MvcInvoker`、`[HttpGet]` / `[HttpPost]` / `[Route]` / `[OutputCache]` / `[WebSocket]` / `IFilter` / `ActionFilterAttribute` 的文件，都要补对应子命名空间的 `using`：控制器文件通常补 `Controllers` + `Results` + `Attributes`，启动文件补 `Hosting`，WebSocket 控制器再补 `WebSockets`。库内影响另见上表最后一行（5 个文件补 `using SAEA.MVC.Routing;`，其中前 3 个文件再各补一条 `using SAEA.MVC.WebSockets;`，`Controllers/WSController.cs` 同样补这一条，另有 3 个文件把 `Routing` 类型名改成 `RouteEntry`；四个新子命名空间之间的交叉引用同样要补 `using`）。`SAEAMvcApplication.cs` 第 138 行的 `ConnectionClosed += WebSocketsFeature.OnDisconnected` 是方法组订阅 `EventHandler`，类型迁到 `SAEA.MVC.WebSockets` 后签名不变，补 `using` 即可，不需要改成实例订阅。三个消费工程改完都要各跑一次构建取证据（原因见第 10 节第 3 条），不能只凭库工程构建通过收工。

**反射字符串已随本次变更新全名**：`Src/SAEA.Common/ConstHelper.cs` 第 52 行的 `"SAEA.MVC.Controllers.Controller"` 与第 336 行的 `"SAEA.MVC.Attributes.OutputCacheAttribute"`（第 29 行 banner 描述同步说明）。这两条是类型全名字符串，必须与 `Controller`、`OutputCacheAttribute` 的实际命名空间一致；类型再迁移时同步改这里，不要按「批量替换 `SAEA.MVC.*` 字符串」处理。

### 6.2 README 同步

- `Src/SAEA.MVC/README.md` 与 `Src/SAEA.MVC/README.en.md` 各 1185 行，行号一一对应。
- 两份里写出 `using SAEA.MVC.*` 的代码块各 15 处，全部按块内实际用到的类型带上子命名空间（`SAEA.MVC.Hosting` / `SAEA.MVC.Controllers` / `SAEA.MVC.Results` / `SAEA.MVC.Attributes` / `SAEA.MVC.Routing` / `SAEA.MVC.WebSockets`），示例里不再保留只写 `using SAEA.MVC;` 的引入行，也没有 `using SAEA.MVC.Tool;` 混在其中。除示例之外还必须新增内容：
  1. 新增「目录与命名空间结构」章节（紧随「核心类」章节），给出第 4.2 节那张表。
  2. 新增「旧 → 新命名空间对照表」，至少覆盖：

     | 旧写法 | 新写法 |
     | --- | --- |
     | `SAEA.MVC.RouteTable` | `SAEA.MVC.Routing.RouteTable` |
     | `SAEA.MVC.Routing`（类型） | `SAEA.MVC.Routing.RouteEntry`（改名） |
     | `SAEA.MVC.AreaCollection`（internal） | `SAEA.MVC.Routing.AreaCollection` |
     | `SAEA.MVC.AttributeRouteTable`（internal） | `SAEA.MVC.Routing.AttributeRouteTable` |
     | `SAEA.MVC.WSMessage` / `SAEA.MVC.WSMessageType` | `SAEA.MVC.WebSockets.WSMessage` / `SAEA.MVC.WebSockets.WSMessageType` |
     | `SAEA.MVC.SAEAMvcApplication`、`SAEAMvcApplicationConfig`、`SAEAMvcApplicationConfigBuilder`、`HttpContext`、`MvcInvoker` | `SAEA.MVC.Hosting.*` |
     | `SAEA.MVC.Controller`、`WSController` | `SAEA.MVC.Controllers.*` |
     | `SAEA.MVC.ActionResult`、`JsonResult`、`ContentResult`、`DataResult`、`FileResult`、`BigDataResult`、`EventStream`、`EmptyResult` | `SAEA.MVC.Results.*` |
     | `SAEA.MVC.IFilter`、`ActionFilterAttribute`、`OutputCacheAttribute`、`HttpGetAttribute`、`HttpPostAttribute`、`RouteAttribute`、`WebSocketAttribute` | `SAEA.MVC.Attributes.*` |

  3. 凡示例直接出现 `RouteTable` / `RouteEntry` 类型名的地方补 `using SAEA.MVC.Routing;`（现有示例只有「目录与命名空间结构」章节那一处涉及，已带该 `using`；新增示例必须补）。
  4. 两份 README 的「属性路由写法」章节都把令牌替换后的绝对路径写作 `RouteEntry.Template`（中英同批，各自保持现语句式）。
  5. 两份 README 的 ASCII 结构图与「核心类」表格里也出现 `RouteTable`，实测都是简单类型名、没有命名空间前缀，**不需要改**；「目录与命名空间结构」章节按第 4.2 节写清 `RouteTable` 位于 `SAEA.MVC.Routing` 即可；WebSocket 长连接示例的 `using` 块必须带 `using SAEA.MVC.WebSockets;`，否则 `MessageAsync(WSMessage message)` 编译不过。
  6. 两份 README 的核心类表与「目录与命名空间结构」表里的 `HttpGet` / `HttpPost` 类型名改为 `HttpGetAttribute` / `HttpPostAttribute`，并说明 `[HttpGet]` / `[HttpPost]` 简写仍可用；示例代码不变。
- 中英两份必须同批改完、内容对齐，不允许只改一份。

### 6.3 文档与注释

- 受影响的其他设计文档共两篇，随切分落地同步：
  - `docs/superpowers/specs/2026-11-17-mvc-attribute-routing-design.md`：第 209 行的 `Src/SAEA.MVC/AttributeRouteTable.cs` 改为 `Src/SAEA.MVC/Routing/AttributeRouteTable.cs`；第 210 行的 `Src/SAEA.MVC/Routing.cs` 改为 `Src/SAEA.MVC/Routing/RouteEntry.cs`；第 214 行的 `Src/SAEA.MVC/MVCInvoker.cs` 改为 `Src/SAEA.MVC/Hosting/MvcInvoker.cs`；第 259 行的 `MVCInvoker.MethodInvoke` 指向按第 3.8 节删除的方法，需改写为 `MvcInvoker.InvokeResultAsync` 与 `ActionInvoker` 的对应表述。该文档只写文件路径，没有 `AttributeRouteTable` 的类型全名，不需要改类型名。
  - `docs/superpowers/specs/2026-11-04-mvc-websocket-design.md`：第 158、182 行引用 `MvcInvoker.MethodInvoke`，该方法删除后需同步改写这两处表述。
- 内容发生变化的文件才同步 banner（口径见第 8 节第 1 步）：「命名空间」「文件名」字段与实际一致，`修改标记` 整体覆盖为本次修改；`唯一标识` 保持原 GUID。

## 7. 测试策略

### 7.1 打开 internal 可见性

在 `Src/SAEA.MVC/SAEA.MVC.csproj` 增加一个 `ItemGroup`（SDK 式工程支持 `InternalsVisibleTo` 项，会生成对应特性）：

```xml
<ItemGroup>
  <InternalsVisibleTo Include="SAEA.MVC.Tests" />
</ItemGroup>
```

已实测：`netstandard2.0` + `LangVersion 8.0` 下上述写法在本机 SDK 构建 0 错误，`obj` 下生成 `[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("SAEA.MVC.Tests")]`；`SAEA.MVC` 未强签名（`SignAssembly` 为 `False`），因此不需要写 `PublicKey=`。`SAEA.MVC.Tests.csproj` 未覆盖 `AssemblyName`，程序集名就是 `SAEA.MVC.Tests`。该 `InternalsVisibleTo` 项的展开行为取决于 MSBuild SDK 版本，与 `LangVersion` 无关。

打开之后，测试工程里现有的两处反射（`AttributeRouteBuildTests.cs` 第 177–196 行、`RouteTokenTests.cs` 第 323–337 行）应改为直接调用 `AttributeRouteTable.Build`，把 `GetType("SAEA.MVC.AttributeRouteTable", true)` 这类字符串断言彻底去掉。

### 7.2 新增用例清单

全部放 `Src/SAEA.MVC.Tests/Tests/`，命名遵循 `被测对象_场景_期望`，只覆盖「提取后等价」的等价点与一条行为变更（`_sweeper` 释放）。

| 建议用例名 | 一句话说明 |
| --- | --- |
| `RouteTable_InheritedAction_GetReturns404_PostResolves` | 基类声明的 action 在 GET 下仍不可达（404）、在 POST 下可解析（200 或 405，按动词校验实际结果断言）；合并后行为与合并前完全一致。测试工程现有用例没有覆盖基类 action 的可见性，需新增 |
| `RouteTable_UnknownActionTwice_SecondHitStillNull` | 同一未知 action 连续两次解析都返回 null，负缓存语义保留 |
| `RouteTable_HttpGetWithoutFilters_PostStillReturns405` | 仅标注 `[HttpGet]`、无过滤器特性时，POST 仍返回 405 而不是 404（锁住写键结构统一后的真实行为） |
| `ActionResolver_ExactNameAndAsyncSuffix_PrefersExact` | `Foo` 与 `FooAsync` 同时存在时，`/c/foo` 命中 `Foo`（精确名优先） |
| `ActionResolver_WsControllerNotOverridden_FallsBackToBaseAsync` | 派生类未 override `OpenAsync` 时仍回落到 `WSController` 声明层 |
| `FilterCollector_ClassAndMethodFilters_SortedByOrder` | 类级与方法级过滤器各自按 `Order` 升序返回（第 3.9 节的等价结论） |
| `RouteTemplateParser_TokenAndPlaceholder_ProducesSameTemplate` | 拆分前后同一模板产出相同的归一化路径与形状键 |
| `RoutePath_EscapedSlash_DecodesToNotMatched` | 路径含 `%2F` 越段时仍判为不匹配，不退化成两段 |
| `RouteSegmentMatcher_SameShapeDifferentParamName_StillConflicts` | 只有占位名不同的等价模板在构建期仍按形状报冲突 |
| `ParameterBinder_CancellationToken_InjectedAtDeclaredPosition` | `CancellationToken` 参数按原声明顺序插回，其余参数照常绑定 |
| `ActionInvoker_ActionTimeoutConfigured_Returns408` | 配置了 `ActionTimeout` 时超时仍返回 408 与原文案 |
| `RequestPipeline_ExceptionWithoutHandler_Returns500` | `ExceptionHandler` 为 null 时仍落 500 与「请求发生异常：」文案 |
| `RequestPipeline_BigDataResult_SkipsResponseFinalize` | `IBigDataResult` 结果不触发 `SetCached` 与 `Send`/`End` |
| `WebSocketConnectionRegistry_StopAll_DisposesSweeper` | `StopAll()` 之后清扫计时器被释放且字段置空（新行为） |
| `MvcInvoker_InvokeResult_KeepsPublicSignature` | 反射断言 `InvokeResult` / `InvokeResultAsync` 的公开性、静态性与参数表未变 |

## 8. 三步交付与验收

每步一个本地提交，不 push。回退方式统一为「回到上一个检查点」：未提交的改动用 `git checkout -- <路径>` 丢弃；已提交但需重做时用 `git reset --soft HEAD~1` 取回改动再重写，不使用 `reset --hard`、不 `clean`、不改写更早的提交。

### 第 1 步：纯搬迁

- 34 个文件移入第 4.1 节目录树（4 个改名的文件：`MVCInvoker.cs` 改名为 `MvcInvoker.cs`；`Routing.cs` 改名为 `RouteEntry.cs` 且类型同名改名；`HttpGet.cs` 改名为 `HttpGetAttribute.cs`、`HttpPost.cs` 改名为 `HttpPostAttribute.cs`，两个类型同名改名）。
- `Routing/` 与 `WebSockets/` 下现有的 7 个文件（`RouteTable`、`Routing`、`AreaCollection`、`AttributeRouteTable`、`WebSocketsFeature`、`MVCWebSocketConnection`、`WSMessage`）把 `namespace` 改为 `SAEA.MVC.Routing` / `SAEA.MVC.WebSockets`，其中 `WSMessage.cs` 同文件的 `WSMessageType` 一并改为 `SAEA.MVC.WebSockets`；`Hosting/`、`Controllers/`、`Results/`、`Attributes/` 下的 22 个文件同样把 `namespace` 改为 `SAEA.MVC.Hosting` / `SAEA.MVC.Controllers` / `SAEA.MVC.Results` / `SAEA.MVC.Attributes`（第 4.3 节），`SAEA.MVC` 根命名空间随之为空；`Tool/` 与 `Tool/CodeGenerte/` 沿用现有命名空间；`Invocation/` 在第 2 步才新增。
- banner 口径：只有内容发生变化的文件才同步 banner——29 个 `namespace` 变化的文件（`Routing/`、`WebSockets/` 的 7 个与四个作者面向目录的 22 个）改「命名空间」字段并整体覆盖「修改标记」（`Routing.cs` 同时命中改名口径）；4 个改名的文件（`MVCInvoker.cs`、`Routing.cs`、`HttpGet.cs`、`HttpPost.cs`）改「文件名」「描述」并覆盖「修改标记」。补 `using` 的范围包括原先列出的 `Routing` / `WebSockets` 位置、四个新子命名空间之间的交叉引用，以及消费方引用这 22 个类型的每一处（第 6.1 节），文件清单以构建报错为准逐个补齐；测试/演示工程的存量文件按仓库约定不回填 banner。纯物理移动到子目录、内容一字不变的文件不得改 banner、不得整文件重写。`Src/SAEA.MVC.Tests` 的存量文件同样不回填 banner。
- 行尾与 BOM 必须保持原样：`Src/SAEA.MVC` 的 34 个 `.cs` 实测纯 LF 16 个、纯 CRLF 11 个、混合 7 个（`BigDataResult.cs`、`DataResult.cs`、`EventStream.cs`、`Tool/APISdkCodeGenerator.cs`、`Tool/CodeGenerte/CSharpTemple.cs`、`Tool/CodeGenerte/JsTemple.cs`、`Tool/CodeType.cs`）、无 BOM 7 个（`AttributeRouteTable.cs`、`MVCWebSocketConnection.cs`、`RouteAttribute.cs`、`WebSocketAttribute.cs`、`WebSocketsFeature.cs`、`WSController.cs`、`WSMessage.cs`）。其中 `AttributeRouteTable.cs`、`MVCWebSocketConnection.cs`、`WebSocketsFeature.cs`、`WSMessage.cs` 同时命中「无 BOM + 必须改 `namespace`」，只能逐行编辑，重写整文件会去掉 BOM。
- 消费工程按第 6.1 节补齐 using：`Routing` / `WebSockets` 的迁移位置 + 四个新子命名空间带来的全部引用点；库内同批补 `using` 并同步类型改名与 `ConstHelper.cs` 的两条反射字符串。
- `README.md` 与 `README.en.md` 同步补「目录与命名空间结构」章节与对照表，示例 `using` 全部换成实际用到的子命名空间（第 6.2 节）。
- 验收：
  - `dotnet build Src/SAEA.Sockets.sln -c Debug`（0 errors）
  - `dotnet test Src/SAEA.MVC.Tests/SAEA.MVC.Tests.csproj -c Release`
  - 旧规则下的验收基线（仓库副本上的可行性演练，按第 4.1、4.2、4.5 节走完本步）为构建 0 错误、单测 88 个用例通过、两个旧式演示工程正常生成；该基线不含命名空间反转带来的消费工程改动，正式执行仍需在本仓库重跑上述命令，并覆盖 `Src/SAEA.MVCTest`、`Src/SAEA.Mvc.ServiceTest` 两个演示工程取证据。

### 第 2 步：切割线 1–5

- 落地第 5 节的 1–5 条，第 6 条不做。
- 新增 12 个 internal 类型：`Routing/` 4 个（`AttributeRouteEntry`、`RouteTemplateParser`、`RoutePath`、`RouteSegmentMatcher`）、`Invocation/` 6 个（`ActionResolver`、`FilterCollector`、`ActionInvoker`、`ParameterBinder`、`VerbPolicy`、`RequestPipeline`）、`WebSockets/` 2 个（`WebSocketHandshake`、`WebSocketConnectionRegistry`）。
- `SAEA.MVC.csproj` 加 `InternalsVisibleTo`，测试改直调，补齐第 7.2 节用例。
- 一条行为变更单独在提交信息里点名：`_sweeper` 释放；GET/POST 的 `BindingFlags` 差异本次不统一（第 5.1 节），合并前后行为一致。
- 验收：同上两条命令，且第 7.2 节新增用例全绿。

### 第 3 步：收尾

- README 中英终稿对齐（章节顺序、表格、示例 `using` 一致）。
- banner 与 XML 注释收尾核对：`public`/`protected` 成员的 `summary`/`param`/`returns` 齐全且与签名、单位、边界一致；行内注释只留「为什么」。
- 验收：同上两条命令。

## 9. 明确不做

- 不改 `Tool/CodeGenerte/`、`CSharpTemple`、`JsTemple` 的拼写（属公开 API 变更，单独立项）。
- 不重命名 `MvcInvoker`、`WSMessage`、`WSMessageType`、`RouteTable` 等公开名称；本次例外共三处：`Routing` → `RouteEntry`（理由见第 4.5 节）、`WSMessage` / `WSMessageType` 名称不变但迁入 `SAEA.MVC.WebSockets`（第 4.2 节，属命名空间搬迁而非改名）、`HttpGet` / `HttpPost` → `HttpGetAttribute` / `HttpPostAttribute`（第 4.2 节、第 5.3 节）。
- 不把 `Tool/` 拆成独立包：`ApiMapping` 依赖 internal 的 `AreaCollection`（`ApiMapping.cs` 第 45、52、63 行）与 `AttributeRouteTable.TryGetDeclaredRoute`（第 113 行），拆包就得把这些内部类型公开化。
- 不动 `SAEA.Http` 的 `Http*Result` 系列与 `IHttpResult` 契约（第 5.6 节）。
- 不机械按「一文件一类型」拆分强绑定类型（第 4.4 节三处保持同文件）。
- 不改目标框架与语言版本，不启用可空引用类型，不动包版本与依赖。
- 不在本次清理第 3.5 节的 `CallContext` 字符串键，只记录。
- 不统一 GET/POST 的 `BindingFlags` 语义：统一为 `DeclaredOnly` 会让基类 CRUD 的 POST 回退，去掉 `DeclaredOnly` 会把 `object` 的方法放成 GET 可达，两者都是行为变更，另立项。

## 10. 风险与待确认

1. **「公开 API 冻结」需要界定到哪一层。** `RouteTable`、`RouteEntry` 迁入 `SAEA.MVC.Routing` 后，类型全名变化会让 `MvcInvoker.InvokeResult(RouteTable, ...)`、`RouteTable.GetOrAdd`、`RouteEntry.ParmaTypes` 等引用处在元数据层面一起变化，消费方必须重编译。这不是成员增删，但确属源码级破坏。已确认「冻结」= 不改名、不增删成员、不换参数与返回类型，**接受命名空间搬迁带来的重编译**；本次不留语义变更：第 5.1 节的合并只去掉重复实现，`BindingFlags` 差异按 `isPost` 保留。本次除命名空间搬迁导致的重编译外，还有三处公开 API 变更：类型改名 `Routing` → `RouteEntry`（第 4.5 节）、`WSMessage` / `WSMessageType` 的命名空间迁移（第 4.2 节，消费方若在控制器里写 `MessageAsync(WSMessage message)` 需补 `using SAEA.MVC.WebSockets;`）、`HttpGet` / `HttpPost` 改名为 `HttpGetAttribute` / `HttpPostAttribute`（第 4.2 节、第 5.3 节）。写 `typeof(HttpGet)` / `typeof(HttpPost)` 或按 `GetType().Name` 字符串比较的消费代码必须改用 `HttpGetAttribute` / `HttpPostAttribute`；`[HttpGet]` 标注写法不受影响。消费方至少要改 `RouteTable.GetOrAdd` 的返回值类型声明，并为引用的 22 个类型补对应子命名空间 `using`（第 4.3 节、第 6.1 节）。
2. **负缓存语义必须原样保留。** 现有实现依赖「`ConcurrentDictionary.GetOrAdd` 把 null 当缓存值」（`RouteTable.cs` 第 77–81、185–189 行）。切 1 若把 `ActionResolver` 改成「返回空列表 + 另存状态」，很容易顺手改成「不缓存未命中」，结果是每个未知 action 请求都重新反射全类型方法，属于性能回归且难以从用例看出。兜底条件是 `Controller_UnknownPostAction_Returns404`（`HttpRoutingTests.cs` 第 149 行）的双请求断言必须继续通过，且 GET 侧应补一条同样的双请求用例（当前只有单请求，见第 3.3 节）。
3. **旧式 csproj 演示工程是验证盲区。** `Src/SAEA.MVCTest`、`Src/SAEA.Mvc.ServiceTest` 都在 `Src/SAEA.Sockets.sln` 内（sln 第 62、40 行），但它们是 `ToolsVersion 15.0` + `packages/` 的旧式工程，`dotnet build` 下的还原与构建不保证与 SDK 式工程一致。命名空间反转后两者都要补 `using`（第 6.1 节），所以遗漏风险同时落在 `SAEA.MVC.Tests` 和这两个演示工程上；`dotnet test` 只能兜住测试工程，两个演示工程必须单独构建确认。
4. **「根不反向依赖 WebSockets 内部类型」这一要求在当前结构里已经不成立。** 四处代码直接指向 `WebSocketsFeature`：`Hosting/SAEAMvcApplication.cs` 订阅 `WebSocketsFeature.OnDisconnected`、调用 `WebSocketsFeature.StopAll()`；`Hosting/MvcInvoker.cs` 把 `HttpContext.Current.WebSockets` 转成 `WebSocketsFeature` 并调用 `Attach` 与 `HandleActionException`；`Hosting/HttpContext.cs` 构造 `WebSocketsFeature`。四个作者面向目录改子命名空间后，这些引用是 `SAEA.MVC.Hosting` 与 `SAEA.MVC.WebSockets` 两个同级子命名空间之间的引用，方向本身允许，但「根命名空间不反向依赖」这条表述已经没有对应的根命名空间。建议把要求改写为「`Hosting` 只通过两个静态入口（`StopAll`、`OnDisconnected`）与一次类型转换接触 WebSockets，且不直接摸 `WebSocketConnectionRegistry` 的内部状态」。
5. **`Tool/` 目录名与命名空间拼写不一致会被本次结构放大。** 目标表写 `Tool/CodeGenerate/`，现盘是 `Tool/CodeGenerte/`，命名空间是 `SAEA.MVC.Tool.CodeGenerte`。本次按第 4.2 节的说明保留现盘目录，结果是「目录 = 职责」这一目标在该目录上打了折扣。若希望目录与命名空间同时正确，需要连带改三个文件的 `namespace`，那就是公开 API 变更，与第 9 节第一条冲突，须另立条目。

## 11. 决策记录

以下为本次已确认的决策，只记结论。

| 序号 | 决策 | 结论 |
| --- | --- | --- |
| 1 | 命名空间策略 | 所有目录都映射同名子命名空间，没有例外：`Hosting/` → `SAEA.MVC.Hosting`、`Controllers/` → `SAEA.MVC.Controllers`、`Results/` → `SAEA.MVC.Results`、`Attributes/` → `SAEA.MVC.Attributes`、`Routing/` → `SAEA.MVC.Routing`、`Invocation/` → `SAEA.MVC.Invocation`、`WebSockets/` → `SAEA.MVC.WebSockets`，`Tool/` 与 `Tool/CodeGenerte/` 沿用现有两个命名空间（第 4.2 节、第 4.3 节）。2026-11-19 取消「作者面向目录留在根命名空间」的例外 |
| 2 | 根命名空间 | `SAEA.MVC` 根命名空间下不再保留任何类型；22 个作者面向的类型随目录迁入子命名空间，消费方补 `using` 后重新编译（第 4.3 节、第 6.1 节） |
| 3 | 巨型类切分 | 允许按职责拆分 6 个巨型类，新增类型全部 `internal`，公开面只保留原入口（第 5 节） |
| 4 | GET/POST 合并 | `RouteTable` 的 GET/POST 双份实现合并为一处，`isPost` 入参只决定 `BindingFlags`，`DeclaredOnly` 差异保持不变，行为零变更（第 5.1 节）；语义统一另立项 |
| 5 | 静态上下文 | 新类型内部不再读静态环境，由公开入口取一次 `HttpContext.Current` 后显式传参（第 5.3 节） |
| 6 | WebSocket 计时器 | `StopAll()` 顺手修复 `_sweeper` 泄漏：`Dispose` 计时器并把字段置 `null`（第 5.4 节） |
| 7 | 拼写 | 不改 `CodeGenerte` 目录名与 `Temple` 类名拼写，只做搬迁（第 4.2 节、第 9 节） |
| 8 | 结果类型 | 不与 `SAEA.Http` 的 `Http*Result` 系列合并，记作独立立项（第 5.6 节） |
| 9 | 类型改名 | 类型 `Routing` 改名为 `RouteEntry`，文件 `Routing.cs` 同步改名为 `RouteEntry.cs`，命名空间仍为 `SAEA.MVC.Routing`；这是本次第一处公开类型改名（第 4.5 节）；命名空间迁移见第 10 条，特性改名见第 11 条 |
| 10 | WSMessage 归属 | `WSMessage` / `WSMessageType` 移入 `WebSockets/` 并把命名空间改为 `SAEA.MVC.WebSockets`，作为本次第二处公开 API 变更；名称不改，4 处引用补 `using`（第 4.2 节、第 6.1 节） |
| 11 | 特性改名 | `HttpGet` / `HttpPost` 改名为 `HttpGetAttribute` / `HttpPostAttribute`，动词判定改为类型判断；`[HttpGet]` 简写仍可用，`ConstHelper.HTTPGET` / `HTTPPOST` 保留但不再被本仓库引用（第 5.3 节、第 9 节） |