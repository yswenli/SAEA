# SAEA.MVC RESTful 谓词支持设计

## 1. 背景与目标

既有实现只有 `HttpGetAttribute` / `HttpPostAttribute`，动词判定、路由条目键与调用入口都以「GET / POST」两值为前提：`VerbPolicy` 只识别两个特性，`RouteTable.GetOrAdd` 与 `MvcInvoker.InvokeResult` 只接受 `bool isPost`，`Allow` 最多列出两项，OPTIONS 在进入路由前直接返回空响应。RESTful 风格的 PUT / PATCH / DELETE 只能靠方法名与两段式地址绕开，无法做到同路径按谓词分派。

本次目标：

| 项 | 内容 |
| --- | --- |
| 谓词数量 | 7 个：GET、POST、PUT、DELETE、PATCH、HEAD、OPTIONS |
| 特性形态 | 统一派生自公开抽象基类 `HttpMethodAttribute`，保留 `()` 与 `(string template)` 两种构造 |
| 路由 | 属性路由（`[Route]` + 动词特性模板）与两段式 `/{controller}/{action}` 都支持 7 个谓词 |
| HEAD | 复用 GET 的结果，但不发送正文，`Content-Length` 与 GET 一致 |
| OPTIONS | 可被显式 `[HttpOptions]` 接管；未声明时返回 204 + `Allow` |
| 兼容 | `bool isPost` 旧重载保留，既有调用方不改变行为 |

## 2. 范围

### 2.1 范围内

- 新增 `HttpPutAttribute`、`HttpDeleteAttribute`、`HttpPatchAttribute`、`HttpHeadAttribute`、`HttpOptionsAttribute`，并把 `HttpGetAttribute` / `HttpPostAttribute` 收拢到公共基类。
- 属性路由表构建、两段式动词判定、405 的 `Allow` 生成统一改为多谓词集合。
- HEAD 的正文抑制链路，直到 `SAEA.Http.HttpResponse` 成帧。
- OPTIONS 的路由后应答（204 + `Allow`）。
- `RouteTable.GetOrAdd` 与 `MvcInvoker` 的 `string method` 重载。

### 2.2 范围外

- 修改 `[Route]` 模板语法：仍只有静态段、`{name}` 路径参数与 `[controller]` / `[action]` 令牌。
- 为 `EventStream`（SSE）给出与 GET 等价的 `Content-Length`。
- SDK 代码生成器产出 PUT / PATCH / DELETE 方法。
- 按谓词拆分 `OutputCache` 的缓存键；缓存键仍由路径与查询串决定。

## 3. 特性类型结构

### 3.1 `HttpMethodAttribute`

公开抽象基类，实现 `IFilter`，特性用法为 `[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]`：

| 成员 | 说明 |
| --- | --- |
| `Method` | 只读，具体谓词名（GET / POST / PUT / DELETE / PATCH / HEAD / OPTIONS），由各派生特性提供 |
| `Template` | 只读，路由模板；`null` 表示不声明模板 |
| `Order` | 可读写，沿用 `IFilter` 的排序语义 |

抽象基类只承载数据，不参与匹配。基类仍实现 `IFilter`，因为过滤器发现逻辑按 `IFilter` 收集方法特性，拆掉接口会让谓词特性从过滤器链里消失。

### 3.2 七个具体特性

```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class HttpGetAttribute : HttpMethodAttribute
{
    public HttpGetAttribute();
    public HttpGetAttribute(string template);
}
```

`HttpPostAttribute`、`HttpPutAttribute`、`HttpDeleteAttribute`、`HttpPatchAttribute`、`HttpHeadAttribute`、`HttpOptionsAttribute` 形状完全一致，仅在 `Method` 取值上不同。控制器里沿用 `[HttpGet]` 等简写（特性名后缀约定），代码里按类型名比较的需改用带 `Attribute` 后缀的名称。

### 3.3 与既有发现逻辑的关系

- 路由构建器读取 `Template` 与 `Method`，不再按类型名逐一判断 `HttpGetAttribute` / `HttpPostAttribute`。
- 过滤器收集仍按 `IFilter` 处理，`Order` 语义不变。
- `RouteAttribute` 不实现 `IFilter`，也不属于谓词族：它只声明路径，不参与动词判定。

## 4. 动词判定与 `Allow` 生成

### 4.1 属性路由

- 匹配按「路径 + 谓词」进行：同一路径形状下，各 action 按各自声明的谓词入库。
- 路径命中但谓词不符返回 405，`Allow` 取该路径上所有显式声明谓词的并集；不回落到两段式。
- 判断谓词时统一忽略大小写（`OrdinalIgnoreCase`），与请求行里的方法写法无关。
- 只声明 `[Route]`、没有任何谓词特性的条目对 6 个可声明谓词放行：GET/POST/PUT/DELETE/PATCH/HEAD 都执行该 action；OPTIONS 不执行，交由框架回 204 + `Allow`，`Allow` 给全 6 项 `GET, HEAD, POST, PUT, DELETE, PATCH`。
- 冲突检测按「路径形状 + 动词」建键：只声明 `[Route]` 的条目只占用 GET/POST/PUT/DELETE/PATCH 五个动词键，不占用 HEAD 与 OPTIONS，因此可与同路径的显式 `[HttpHead]` / `[HttpOptions]` 共存。与 `[HttpHead]` 共存时 HEAD 命中显式 action，其余动词仍走 `[Route]` 条目；与 `[HttpOptions]` 共存时 OPTIONS 命中显式 action，其余动词仍走 `[Route]` 条目，这类路径不会产生 405。

### 4.2 两段式

- action 上没有任何谓词特性：不限谓词，GET/POST/PUT/DELETE/PATCH/HEAD 全部放行；OPTIONS 不执行 action，按 204 + `Allow` 应答。
- action 上声明了一个或多个：允许集合取声明的并集，与 `Request.Method` 比较，不符返回 405 并带 `Allow`；action、过滤器、WebSocket 入口都不执行。
- `WebConfig.DefaultRoute` 默认路由与 `/{Controller}/{Action}` 两条分支共用同一判定。

### 4.3 `Allow` 的组成与顺序

固定顺序 `GET, HEAD, POST, PUT, DELETE, PATCH`，只输出显式声明过的项；显式声明 `[HttpOptions]` 时在末位追加 `OPTIONS`：

| 声明 | `Allow` |
| --- | --- |
| `[HttpGet]` | `GET, HEAD` |
| `[HttpGet]` + `[HttpPost]` | `GET, HEAD, POST` |
| `[HttpPut]` + `[HttpDelete]` | `PUT, DELETE` |
| `[HttpHead]` | `HEAD`（不反补 `GET`） |
| `[HttpOptions]` | `OPTIONS`（排末位） |

- 声明 GET 时同时列出 HEAD，因为 HEAD 复用 GET 的 action。
- `OPTIONS` 只在显式声明 `[HttpOptions]` 时排末位，未声明时不出现；只声明 `[HttpOptions]` 的路径对其它谓词返回 405 时 `Allow` 为 `OPTIONS`，满足 405 必须带 `Allow` 的要求（RFC 7231 §6.5.5）。
- 两段式与属性路由使用同一生成函数，保证两处 `Allow` 文本一致。

## 5. HEAD 语义与实现

### 5.1 行为

- HEAD 请求优先匹配显式 `[HttpHead]` action；没有显式声明时回落到同路径的 GET action。
- 回落到 GET 时执行完整管线（过滤器、输出缓存、参数绑定、action 本体），只抑制正文写出，因此 `Content-Length` 与同一资源的 GET 响应一致。
- 显式 `[HttpHead]` 优先于 GET 别名：两条同时存在时 HEAD 只执行 `[HttpHead]` 的 action，不会再执行 GET 的 action。

### 5.2 实现位置

| 位置 | 职责 |
| --- | --- |
| `SAEA.Http.HttpResponse.SuppressBody` | 新增公开属性；为 `true` 时只成帧报头，不写正文 |
| `SAEA.Http.HttpResponse.ToBytes` | `Content-Length` 仍按实际正文长度计算，正文拷贝按 `SuppressBody` 跳过 |
| MVC 管道 | 识别到 HEAD 请求后、写出响应前置 `SuppressBody = true` |
| `BigDataResult` | `SuppressBody` 为 `true` 时按文件长度直接发报头并短路，不进入分块读取与流式写出 |

`SuppressBody` 放在 `SAEA.Http` 而不是 MVC 内部，是因为成帧发生在 HTTP 层；MVC 只负责在正确的时机置位，避免每个 `ActionResult` 各自判断谓词。

## 6. OPTIONS 语义

### 6.1 流程

1. 按路径与谓词正常路由。
2. 命中显式 `[HttpOptions]` action：照常执行 action 与过滤器，返回它的结果。
3. 未声明 `[HttpOptions]`：由框架应答 `204 No Content`，写入 `Allow` 头（按 4.3 生成），不执行任何 action。
4. 未声明任何谓词特性的 action 收到 OPTIONS 同样不执行，走第 3 步。
5. 路径完全未命中：仍按 404 处理。

### 6.2 与旧行为差异

以前 OPTIONS 在进入路由前直接返回空响应，既不读路由也不生成 `Allow`。现在 OPTIONS 进入正常路由流程，因此：路径不存在时是 404 而不是空 200；未声明 `[HttpOptions]` 的已注册路径一定会带 `Allow`；显式 `[HttpOptions]` 可以接管预检逻辑。

## 7. 公开 API 变化与兼容性

### 7.1 `RouteTable.GetOrAdd`

```csharp
public RouteEntry GetOrAdd(Type controllerType, string actionName, string method);
public RouteEntry GetOrAdd(Type controllerType, string actionName, bool isPost);
```

- `method` 直接接收谓词字符串（`GET`、`POST`、`PUT` 等）。
- 旧 `bool isPost` 重载保留，语义固定为 `isPost ? "POST" : "GET"`，不引入第三种解释。

### 7.2 `MvcInvoker`

```csharp
public static ActionResult InvokeResult(..., bool isPost);
public static ActionResult InvokeResult(..., string method);
public static Task<ActionResult> InvokeResultAsync(..., bool isPost);
public static Task<ActionResult> InvokeResultAsync(..., string method);
```

新增重载的 `string method` 是最后一个参数；旧重载转调新重载并传 `GET` / `POST`。已有调用方与方法重载决议都不受影响。

### 7.3 SDK 代码生成器

`ApiMapping` 的 `HttpMethods` 元数据已包含全部谓词，但生成器仍只按 GET / POST 产出客户端方法与调用代码。生成器不在本次改动范围，属已知限制。

## 8. 破坏性变更

| 变更 | 影响 | 处置 |
| --- | --- | --- |
| 405 的 `Allow` 增加 `HEAD` | 声明 GET 的路径，`Allow` 由 `GET` 变为 `GET, HEAD` | 按字符串严格比对 `Allow` 的客户端需要更新 |
| OPTIONS 不再一律空响应 | 以前 OPTIONS 短路返回空响应；现在路由后可能是 204 + `Allow`、`[HttpOptions]` 的执行结果或 404 | 依赖「OPTIONS 恒为空」的客户端与中间件需要复核 |

**关于谓词特性基类（源码兼容，二进制需重新编译）**：`HttpGetAttribute` / `HttpPostAttribute` 的公开成员 `Template`、`Order` 已上移到 `HttpMethodAttribute`。类型声明本身保持源码兼容——`[HttpGet]` / `[HttpPost]` 写法、`typeof(HttpGetAttribute)`、`is Attribute`、`is IFilter` 全都不变，既有源码不需要改动。变化发生在二进制层：已编译的旧程序集绑定的是 `HttpGetAttribute::get_Template` / `::get_Order`，这些访问器在派生类上已不存在，运行时可能抛 `MissingMethodException`，升级后必须重新编译。需要覆盖全部谓词时，判断基类 `HttpMethodAttribute` 或读取它的 `Method` 属性。

## 9. 已知限制

- HEAD 指向 `EventStream`（SSE）时只回报头、不写任何事件流内容，连接正常结束，也不启动心跳：SSE 是无限流，无法给出与 GET 等价的 `Content-Length`；需要探询能力或 `Allow` 时请用 OPTIONS 或 GET。
- `ApiMapping` / SDK 生成器仍只产出 GET / POST 两种方法。
- 一个 action 仍只能有一个模板来源；同一路径的不同谓词必须分到不同 action。
- `OutputCache` 的缓存键不包含谓词，本次不改变其生成方式；HEAD 复用 GET 结果时沿用同一条缓存。
- 两段式的 PUT/DELETE/PATCH 用 `Instance | Public` 反射范围解析 action，会把继承的 public 成员（如 `object.ToString`、`GetType`）也当成 action，`/Controller/ToString` 这类地址可能被执行；GET/HEAD 用 `DeclaredOnly`，不受影响。
- 同名 action 重载：两个重载时静默取其中一个，三个及以上直接返回 500；调用方应避免同名重载。

## 10. 测试覆盖点

1. 7 个谓词特性分别在属性路由下命中对应 action。
2. 两段式 action 声明单个或多个谓词时，允许集合正确，其余谓词返回 405。
3. 405 的 `Allow` 顺序为 `GET, HEAD, POST, PUT, DELETE, PATCH`，声明 GET 时含 HEAD，显式声明 `[HttpOptions]` 时 `OPTIONS` 排末位、只声明 `[HttpOptions]` 的路径 405 时 `Allow` 为 `OPTIONS`。
4. HEAD 命中 GET action：状态码、报头与 GET 一致，正文为空，`Content-Length` 等于 GET 正文长度。
5. 显式 `[HttpHead]` 与 `[HttpGet]` 同时存在时，HEAD 只执行 `[HttpHead]` 的 action。
6. OPTIONS 未声明 `[HttpOptions]`：返回 204 + `Allow`。
7. OPTIONS 声明了 `[HttpOptions]`：执行该 action，不返回 204。
8. 未声明任何谓词特性的 action 收到 OPTIONS：不执行，返回 204 + `Allow`。
9. `RouteTable.GetOrAdd(type, action, "PUT")` 与 `GetOrAdd(type, action, false)`、`GetOrAdd(type, action, true)` 的等价关系。
10. `MvcInvoker.InvokeResult` / `InvokeResultAsync` 新旧重载对同一 action 返回一致结果。
11. `BigDataResult` 在 HEAD 下只发报头、不读文件块。
12. 统一配置下同一路径不同谓词不产生启动期冲突；同路径同谓词仍抛异常。

手工验收：`dotnet run --project Src/SAEA.MVCTest`，对同一资源分别发 GET / HEAD / POST / PUT / PATCH / DELETE / OPTIONS，核对状态码、`Allow` 与 `Content-Length`。
构建验收：`dotnet build Src/SAEA.Sockets.sln -c Debug` 0 error；`dotnet test Src/SAEA.MVC.Tests/SAEA.MVC.Tests.csproj -c Release` 全绿。