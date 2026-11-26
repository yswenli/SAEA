# SAEA.MVC - 轻量级高性能 MVC Web 框架 🌐

[![NuGet version](https://img.shields.io/nuget/v/SAEA.MVC.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.MVC)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**[English Version](README.en.md)** | **中文版**

> 基于 .NET Standard 2.0 的轻量级 MVC Web 框架，采用异步 IO，自宿主运行，无需 IIS/Kestrel。
>
> ⚡ **零拷贝内核**：构建于 SAEA.Sockets 的 Span/Memory 零拷贝收发与内存池通道之上，显著降低分配与 GC。

## 快速导航 🧭

| 章节                    | 内容            |
| --------------------- | ------------- |
| [⚡ 30秒快速开始](#30秒快速开始) | 最简单的上手示例      |
| [🛣️ 路由](#路由)          | 两段式约定与属性路由    |
| [🎯 核心特性](#核心特性)      | 框架的主要功能       |
| [📐 架构设计](#架构设计)      | 组件关系与工作流程     |
| [💡 应用场景](#应用场景)      | 何时选择 SAEA.MVC |
| [📊 性能对比](#性能对比)      | 与其他框架对比       |
| [❓ 常见问题](#常见问题)       | FAQ 快速解答      |
| [🔧 核心类](#核心类)        | 主要类一览         |
| [📝 使用示例](#使用示例)      | 详细代码示例        |

***

## 30秒快速开始 ⚡

最快上手方式，只需3步即可启动 Web 服务器：

### Step 1: 安装 NuGet 包

```bash
dotnet add package SAEA.MVC
```

### Step 2: 创建 MVC 应用（仅需5行代码）

```csharp
using SAEA.MVC.Hosting;

var config = SAEAMvcApplicationConfigBuilder.Read();
var app = new SAEAMvcApplication(config);
app.SetDefault("home", "index");
app.Start();
```

### Step 3: 创建第一个 Controller

```csharp
using SAEA.MVC.Attributes;
using SAEA.MVC.Controllers;
using SAEA.MVC.Results;

public class HomeController : Controller
{
    [HttpGet]
    public ActionResult Index()
    {
        return Content("Hello, SAEA.MVC!");
    }
}
```

**就这么简单！** 🎉 访问 `http://localhost:28080/home/index` 即可看到结果。

***

## 路由 🛣️

默认约定按 URL 末尾两段定位：`/{任何前缀}/{Controller}/{Action}`，控制器名是「类名 + Controller」，比较忽略大小写，所以 `/home/index` 与 `/api/v1/home/index` 都命中 `HomeController.Index`。这只是默认约定，不是限制：声明了属性路由的 action 按模板优先匹配，模板可以完全替换两段式地址。

两段式地址与 `WebConfig.DefaultRoute` 默认路由在定位 action 时都省略 `Async` 后缀：地址里写 `/test/sleep` 就会命中 `TestController.SleepAsync`。精确名优先，`Foo` 与 `FooAsync` 同时存在时 `/controller/foo` 命中 `Foo`；这条回退只作用于按方法名匹配的两段式，属性路由始终按模板匹配，与方法名无关。

### 属性路由写法

```csharp
using SAEA.MVC.Attributes;
using SAEA.MVC.Controllers;
using SAEA.MVC.Results;

[Route("api/v1/users")]              // 类级前缀
public class UserController : Controller
{
    [HttpGet]                        // GET /api/v1/users
    public ActionResult List()
    {
        return Json(new { Total = 2 });
    }

    [HttpGet("{id}")]                // GET /api/v1/users/{id}
    public ActionResult Get(string id)
    {
        return Json(new { Id = id });
    }

    [HttpPost]                       // POST /api/v1/users
    public ActionResult Create(string name)
    {
        return Json(new { Created = name });
    }
}

public class OrderController : Controller
{
    [Route("api/v1/orders/{id}")]    // 类上没有 [Route]：方法模板即绝对路径；[Route] 不限动词
    public ActionResult Get(string id)
    {
        return Json(new { Id = id });
    }
}

public class HealthController : Controller
{
    [Route("health")]                // 单段路径同样可用
    public ActionResult Ping()
    {
        return Content("ok");
    }
}

[Route("api/v1/[controller]")]       // [controller]：类名去掉 Controller 后缀，ProductController → Product
public class ProductController : Controller
{
    [HttpGet]                        // GET /api/v1/product
    public ActionResult List()
    {
        return Json(new { Total = 2 });
    }

    [HttpGet("[action]/{id}")]       // [action]：方法名去掉 Async 后缀，GetAsync → Get，即 GET /api/v1/product/get/5
    public ActionResult GetAsync(string id)
    {
        return Json(new { Id = id });
    }
}
```

- `[Route(template)]` 只声明路径，不限定请求方法，6 个可声明动词（GET/HEAD/POST/PUT/DELETE/PATCH）都接受；OPTIONS 不执行该 action，由框架回 204 + `Allow: GET, HEAD, POST, PUT, DELETE, PATCH`。
- `[HttpGet(template)]`、`[HttpPost(template)]`、`[HttpPut(template)]`、`[HttpDelete(template)]`、`[HttpPatch(template)]`、`[HttpHead(template)]`、`[HttpOptions(template)]` 既声明路径又限定动词。
- 类上的 `[Route]` 与方法模板按 `/` 拼接；方法上的动词特性不带模板时，类级模板本身就是路径。
- 类上只有 `[Route]`、方法上没有任何路由特性的 action 不进入属性路由，继续走两段式，避免类级模板吞掉全部 action。
- 模板里可以用 `[controller]` / `[action]` 令牌代替手写控制器名与方法名，令牌识别与后缀剥离都忽略大小写；地址本身的比较同样忽略大小写，所以模板替换出 `Product`、`Get` 后，`/api/v1/product/get/5` 照样命中。
- 类级模板里也可以写 `[action]`，按每个 action 分别替换：`[Route("api/v1/[controller]/[action]")]` 会让该控制器的每个 action 各走自己的地址。

令牌在属性路由表构建期完成替换，替换后的绝对路径就是 `RouteEntry.Template`；SDK 生成器输出与冲突检测都使用替换后的结果。

### RESTful 控制器示例

同一资源上的不同操作按谓词分开声明，路径模板只在类上写一次：

```csharp
using SAEA.MVC.Attributes;
using SAEA.MVC.Controllers;
using SAEA.MVC.Results;

[Route("api/v1/articles")]
public class ArticleController : Controller
{
    [HttpGet]                        // GET /api/v1/articles       —— 列表
    public ActionResult List()
    {
        return Json(new[] { new { Id = 1, Title = "Hello" } });
    }

    [HttpGet("{id}")]                // GET /api/v1/articles/5     —— 单条；HEAD 同样命中但不发送正文
    public ActionResult Get(string id)
    {
        return Json(new { Id = id, Title = "Hello" });
    }

    [HttpPost]                       // POST /api/v1/articles      —— 新建
    public ActionResult Create(string title)
    {
        return Json(new { Id = 3, Title = title });
    }

    [HttpPut("{id}")]                // PUT /api/v1/articles/5     —— 全量替换
    public ActionResult Replace(string id, string title)
    {
        return Json(new { Id = id, Title = title });
    }

    [HttpPatch("{id}")]              // PATCH /api/v1/articles/5   —— 局部更新
    public ActionResult Patch(string id, string title)
    {
        return Json(new { Id = id, Title = title });
    }

    [HttpDelete("{id}")]             // DELETE /api/v1/articles/5  —— 删除
    public ActionResult Delete(string id)
    {
        return Empty();
    }

    [HttpOptions]                    // OPTIONS /api/v1/articles   —— 可选：显式接管，不写则由框架回 204 + Allow
    public ActionResult Options()
    {
        return Empty();
    }
}
```

- 同一路径不同谓词分别命中各自动作；`[HttpPut("{id}")]` 与 `[HttpPatch("{id}")]` 共用同一形状的模板，只有动词不同，启动期不冲突。
- 没有声明 `[HttpOptions]` 时，OPTIONS 由框架统一应答 204 + `Allow`，不需要每个控制器都写一遍。
- `[HttpHead]` 可选：不声明时 HEAD 自动复用 GET 的动作。

### 模板语义

| 规则 | 说明 |
| --- | --- |
| 静态段 | `api/v1/users`，逐段比较，忽略大小写 |
| 令牌 `[controller]` | 换成控制器类名去掉结尾 `Controller` 后缀，如 `UserController` → `User`；令牌识别与后缀剥离都忽略大小写 |
| 令牌 `[action]` | 换成 action 方法名去掉结尾 `Async` 后缀，如 `GetAsync` → `Get`；令牌识别与后缀剥离都忽略大小写 |
| 令牌位置 | 必须独占一整段，`[controller]`、`[action]` 单独成段合法；`v1-[action]`、`[controller]s`、`{[action]}` 这类混写非法 |
| 路径参数 | `{id}`，必选且只占一个段；参数名只允许字母、数字、下划线 |
| 占位符位置 | 必须独占一段，`user-{id}` 这类混写非法 |
| 首尾 `/` | 忽略；连续 `//` 合并为一个 |
| 尾斜杠 | `/users/5/` 与 `/users/5` 等价 |
| query | 不参与匹配，`/users/5?x=1` 只按 `/users/5` 匹配 |

匹配顺序：

1. 静态模板（无参数）按归一化路径直接命中，字典查找。
2. 参数模板按段数分桶后匹配，静态段多的模板优先。
3. 两段式约定。
4. 静态文件。
5. 404。

属性路由先于静态文件判断，声明了与静态文件同名的路径会遮蔽该文件，需要更具体的模板或换一个前缀。

命中的路径参数先写入 `Request.Parmas`，覆盖同名 query 与表单值；action 形参按名字取值，数值等基础类型会自动转换，与两段式的绑定方式一致。

### 请求方法

框架提供七个谓词特性，全部派生自公开基类 `HttpMethodAttribute`（`SAEA.MVC.Attributes`），构造形式统一为 `()` 与 `(string template)`：

| 特性 | 限定的动词 |
| --- | --- |
| `[HttpGet]` | GET |
| `[HttpPost]` | POST |
| `[HttpPut]` | PUT |
| `[HttpDelete]` | DELETE |
| `[HttpPatch]` | PATCH |
| `[HttpHead]` | HEAD |
| `[HttpOptions]` | OPTIONS |

- `[Route]` 不限动词；任一动词特性限定为对应动词，属性路由与两段式两条路径按同一口径判定。
- 属性路由路径命中但动词不符时返回 **405 Method Not Allowed**，响应头 `Allow` 列出该路径声明的动词（规则见下方「Allow 头」），不会回落到两段式。
- 该路径完全没有属性路由条目时不算冲突，继续走两段式。
- 方法名大小写不敏感：框架入口统一把请求方法规范成大写再分发，请求行里的 `get` / `options` 与全大写等价；无法识别的方法（如 `TRACE`）返回 **501 Not Implemented**，而不是 505。
- 两段式 action 没有任何动词特性时不限动词，GET/POST/PUT/DELETE/PATCH/HEAD 全部放行，OPTIONS 由框架按 204 + `Allow` 应答；声明了一个或多个时，允许动词取声明的并集，请求方法与允许集合忽略大小写比较，不符时返回 **405 Method Not Allowed** 并带 `Allow`，action、过滤器与 WebSocket 入口都不执行。`WebConfig.DefaultRoute` 默认路由与 `/{Controller}/{Action}` 两条两段式分支都生效。

### Allow 头

`Allow` 由 action 上**显式声明**的动词生成，固定顺序 `GET, HEAD, POST, PUT, DELETE, PATCH`；显式声明了 `[HttpOptions]` 时在末位追加 `OPTIONS`：

- 只列出声明过的动词，其余省略；声明 `[HttpGet]` 时同时列出 `HEAD`，因为 HEAD 复用 GET 的结果。
- 声明 `[HttpHead]` 不会反过来补 `GET`。
- 只有显式声明 `[HttpOptions]` 时 `OPTIONS` 才排在末位，未声明则一律不出现；只声明 `[HttpOptions]` 的路径对其它谓词返回 405 时 `Allow` 就是 `OPTIONS`，满足 405 必须带 `Allow` 的要求（RFC 7231 §6.5.5）。
- 只声明 `[Route]`、没有任何谓词特性的属性路由条目等价于放行全部 6 个可声明动词：GET/POST/PUT/DELETE/PATCH/HEAD 都执行该 action，OPTIONS 不执行，由框架回 204 + `Allow: GET, HEAD, POST, PUT, DELETE, PATCH`。
- 未声明任何动词特性的 action 不限动词，不会产生 405；`Allow` 只出现在 405 与框架自动应答的 OPTIONS 响应里。

### HEAD 与 OPTIONS

- **HEAD**：复用 GET 的结果。`[HttpGet]` 声明的 action 也响应 HEAD，执行流程与 GET 一致（过滤器、输出缓存、参数绑定都照跑），只是不发送响应正文；`Content-Length` 仍是同一资源 GET 正文的长度，客户端可以据此判断资源大小。
- 显式 `[HttpHead]` 优先于 GET 别名：同一路径同时声明 `[HttpGet]` 与 `[HttpHead]` 时，HEAD 请求执行 `[HttpHead]` 的 action；只有没有显式 `[HttpHead]` 时才回落到 GET 的 action。
- **OPTIONS**：先匹配显式 `[HttpOptions]` action，命中就正常执行；没有声明时返回 **204 No Content** 并带 `Allow` 头，`Allow` 按上方规则生成。未声明任何动词特性的 action 收到 OPTIONS 也不会被执行，同样返回 204 + `Allow`。
- OPTIONS 不再像以前那样在进入路由前直接返回空响应短路：现在先路由，命中 `[HttpOptions]` 就交给它，否则由框架按 `Allow` 统一应答。
- 完全未命中的路径收到 OPTIONS 返回 **404 Not Found**；命中的是静态文件时仍按 204 自动应答（静态资源是真实存在的路径，不做 404）。
- `[Route]` 与显式 `[HttpHead]` / `[HttpOptions]` 可以在同一路径共存：启动期冲突检测里，只带 `[Route]` 的条目只占用 GET/POST/PUT/DELETE/PATCH 五个动词键，不占用 HEAD 与 OPTIONS。
- 与 `[HttpHead]` 共存时 HEAD 命中显式 action，其余动词仍走 `[Route]` 条目；与 `[HttpOptions]` 共存时 OPTIONS 命中显式 action，其余动词仍走 `[Route]` 条目，这类路径不会产生 405。

### 启动期校验

属性路由表在 `SAEAMvcApplication` 构造时构建并校验，有问题直接启动失败：

- 模板非法（`{id?}`、`{*path}`、`{id:int}`、占位名含非法字符）抛 `ArgumentException`。
- 同一 action 声明了多个带模板的路由特性（如 `[Route("x")]` 与 `[HttpGet("y")]`）抛 `ArgumentException`。
- 同一路径形状 + 同一动词被两个 action 声明抛 `InvalidOperationException`；`{id}` 与 `{uid}` 视为同一形状，同路径不同动词合法。
- 段里出现方括号但既不是 `[controller]` 也不是 `[action]`（含 `[foo]`、未闭合的 `[controller`）抛 `ArgumentException`；这类写法以前会被当字面静态段静默注册，现在直接启动失败。
- 后缀剥离后为空（类名恰好是 `Controller`、方法名恰好是 `Async`）抛 `ArgumentException`。
- 同一控制器里 `Foo` 与 `FooAsync` 都用 `[action]` 时两者解析到同一路径，动词相同即启动期 `InvalidOperationException` 冲突；要共存就显式写不同模板或分动词。

### 不支持的能力

- 令牌不能与字面量混写：`v1-[action]`、`[controller]s`、`{[action]}` 均非法，令牌必须独占一段。
- 只有 `[controller]` 与 `[action]` 两个令牌，没有 `[area]` 等其它令牌。
- 可选参数 `{id?}`、通配 `{*path}`、类型约束 `{id:int}` / `{id:guid}`。
- `~/` 绝对前缀语法。
- 一个 action 声明多条路径：一个 action 只能有一个模板来源。

### 地址对照表

| 请求 | 命中 | 说明 |
| --- | --- | --- |
| `GET /api/v1/users` | `UserController.List` | 类级前缀 + 方法级无参 |
| `GET /api/v1/users/5` | `UserController.Get`，`id = "5"` | 路径参数绑定同名形参 |
| `GET /API/V1/Users/5/` | 同上 | 忽略大小写与尾斜杠 |
| `GET /api/v1/users/5?x=1` | 同上 | query 不参与匹配 |
| `POST /api/v1/users` | `UserController.Create` | 同路径不同动词 |
| `HEAD /api/v1/users` | `UserController.List` | HEAD 复用 GET，不发送正文，`Content-Length` 与 GET 一致 |
| `DELETE /api/v1/users` | 405，`Allow: GET, HEAD, POST` | 路径命中但动词不符 |
| `OPTIONS /api/v1/users` | 204，`Allow: GET, HEAD, POST` | 未声明 `[HttpOptions]`，由框架统一应答 |
| `GET /api/v1/orders/9` | `OrderController.Get` | 方法级模板即绝对路径 |
| `POST /api/v1/orders/9` | 同上 | `[Route]` 不限动词 |
| `GET /health` | `HealthController.Ping` | 单段属性路由 |
| `GET /api/v1/product/get/5` | `ProductController.GetAsync`，`id = "5"` | `[controller]` → `Product`、`[action]` → `Get`，匹配忽略大小写 |
| `GET /API/V1/PRODUCT/GET/5` | 同上 | 令牌替换结果与路径比较都忽略大小写 |
| `GET /home/index` | `HomeController.Index` | 未声明属性路由，走两段式 |
| `GET /test/sleep` | `TestController.SleepAsync` | 两段式省略 `Async` 后缀 |
| `POST /home/index` | 405，`Allow: GET, HEAD` | 两段式 action 只标了 `[HttpGet]`，同样拦截 |

### 路由表与调用入口

`RouteTable.GetOrAdd` 新增 `(Type controllerType, string actionName, string method)` 重载，`method` 直接传谓词字符串：

```csharp
using SAEA.MVC.Routing;

var table = new RouteTable();
RouteEntry get = table.GetOrAdd(typeof(HomeController), "Index", "GET");
RouteEntry put = table.GetOrAdd(typeof(HomeController), "Index", "PUT");
```

原有的 `(Type, string, bool isPost)` 重载保留，等价于 `isPost ? "POST" : "GET"`，既有调用方不用改。

`MvcInvoker.InvokeResult(...)` 与 `MvcInvoker.InvokeResultAsync(...)` 各新增一个末尾参数为 `string method` 的重载，可用任意谓词同步/异步调用 action；原 `bool isPost` 重载同样保留并映射到 `GET`/`POST`。

### 兼容性变更与已知限制

升级时注意两条行为变化：

- **405 的 `Allow` 增加 `HEAD`**：属性路由与两段式的 405 响应中，声明 GET 的路径现在会同时列出 `HEAD`（如 `Allow: GET, HEAD`）。
- **OPTIONS 行为改变**：以前 OPTIONS 在进入路由前直接返回空响应；现在先路由，命中 `[HttpOptions]` 就执行它，未声明但路径存在（含静态资源）返回 204 + `Allow`，路径完全未命中返回 404。依赖「OPTIONS 一律空响应」的客户端或中间件需要复核。

已知限制：

- HEAD 指向 `EventStream`（SSE）时只回报头、不写任何事件流内容，连接正常结束，也不启动心跳：SSE 是无限流，无法给出与 GET 等价的 `Content-Length`；需要探询能力或 `Allow` 时请用 OPTIONS 或 GET。
- `ApiMapping` / SDK 代码生成器仍只产出 GET/POST 两种方法；其 `HttpMethods` 元数据已包含全部动词，生成器本身尚未跟上。
- 两段式的 PUT/DELETE/PATCH 用 `Instance | Public` 反射范围解析 action，会把继承的 public 成员（如 `object.ToString`、`GetType`）也当成 action，`/Controller/ToString` 这类地址可能被执行；GET/HEAD 用 `DeclaredOnly`，不受影响。
- 同名 action 重载：两个重载时静默取其中一个，三个及以上直接返回 500；调用方应避免同名重载。

***

## 核心特性 🎯

| 特性              | 说明                    | 优势              |
| --------------- | --------------------- | --------------- |
| 🚀 **跨平台异步 IO** | 基于 SAEA.Sockets 异步模型  | 面向高并发场景，CPU 利用率高 |
| 🏠 **自宿主运行**    | 无需 IIS/Kestrel        | 独立进程，部署简单       |
| 📦 **轻量级**      | 无复杂依赖                 | 启动快，内存占用低       |
| 🎯 **完整 MVC**   | Controller/Action/路由  | 标准 MVC 开发体验     |
| 🔧 **AOP 过滤器**  | ActionFilterAttribute | 方法拦截、日志、权限控制    |
| 🔀 **RESTful 谓词** | 七个 HTTP 方法特性          | 属性路由与两段式通用      |
| 💾 **输出缓存**     | OutputCacheAttribute  | 方法级缓存，提升响应速度    |
| 📡 **SSE 推送**   | Server-Sent Events    | 实时服务器推送         |
| 📁 **静态文件**     | 自动缓存、大文件分块            | 智能区分大小文件传输      |
| 📥 **Range 请求**  | 单区间 `bytes=a-b` 等      | 分段下载与断点续传       |
| 🌐 **跨域支持**     | CORS 配置               | 前后端分离项目友好       |

***

## 架构设计 📐

### 组件架构图

```
┌─────────────────────────────────────────────────────────────┐
│                     SAEA.MVC 架构                            │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │                   SAEAMvcApplication                   │  │
│  │                     (应用程序入口)                      │  │
│  └────────────────────────┬─────────────────────────────┘  │
│                           │                                 │
│              ┌────────────┼────────────┐                   │
│              │            │            │                    │
│         ┌────▼────┐  ┌───▼────┐  ┌───▼────┐               │
│         │  Route  │  │ Filter │  │ Static │               │
│         │  Table  │  │ (AOP)  │  │ File   │               │
│         │ (路由)  │  │(过滤器)│  │(静态)  │               │
│         └────┬────┘  └───┬────┘  └───┬────┘               │
│              │           │            │                     │
│              └───────────┼────────────┘                     │
│                          │                                   │
│                  ┌───────▼───────┐                          │
│                  │  Controller   │                          │
│                  │   (控制器)    │                          │
│                  └───────┬───────┘                          │
│                          │                                   │
│              ┌───────────┼───────────┐                      │
│              │           │           │                      │
│         ┌────▼───┐  ┌───▼────┐  ┌──▼───┐                  │
│         │Action  │  │ Http   │  │Result│                  │
│         │Result  │  │Context │  │Types │                  │
│         │(结果)  │  │(上下文)│  │(类型)│                  │
│         └────────┘  └────────┘  └──────┘                  │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │                 SAEA.Sockets (底层)                  │  │
│  │                   异步 IO 通信引擎                   │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 请求处理流程图

```
HTTP 请求处理流程:

客户端请求 ──► SAEA.Sockets 接收
                    │
                    ▼
          ┌─────────────────┐
          │   HttpParser    │ 解析 HTTP 请求
          │   解析请求头/体  │
          └─────────────────┘
                    │
                    ▼
          ┌─────────────────┐
          │   RouteTable    │ 路由匹配
          │   查找 Controller│
          └─────────────────┘
                    │
                    ▼
          ┌─────────────────┐
          │ ActionFilter    │ 执行前置过滤器
          │ OnActionExecuting│
          └─────────────────┘
                    │
                    ▼
          ┌─────────────────┐
          │   Controller    │ 执行 Action 方法
          │   Action 执行   │
          └─────────────────┘
                    │
                    ▼
          ┌─────────────────┐
          │ ActionResult    │ 生成响应结果
          │ ExecuteResult   │
          └─────────────────┘
                    │
                    ▼
          ┌─────────────────┐
          │ ActionFilter    │ 执行后置过滤器
          │ OnActionExecuted│
          └─────────────────┘
                    │
                    ▼
          ┌─────────────────┐
          │ OutputCache     │ 缓存处理
          │ (如果启用)      │
          └─────────────────┘
                    │
                    ▼
             HTTP 响应返回客户端


ActionResult 类型分支:

ActionResult ──┬── JsonResult ────────► JSON 序列化输出
               │
               ├── ContentResult ────► 文本/HTML 输出
               │
               ├── FileResult ───────► 文件下载(小文件)
               │
               ├── BigDataResult ────► 大文件流式传输
               │
               ├── DataResult ───────► 二进制数据输出
               │
               ├── EmptyResult ──────► 空响应
               │
               └── EventStream ───────► Server-Sent Events 推送
```

***

## 应用场景 💡

### ✅ 适合使用 SAEA.MVC 的场景

| 场景              | 描述                | 推荐理由       |
| --------------- | ----------------- | ---------- |
| 🔧 **API 服务**   | RESTful API、微服务后端 | 轻量高效，启动快速  |
| 🎮 **游戏后端**     | 游戏服务器 HTTP 接口     | 异步 IO 支撑高并发 |
| 📊 **管理后台**     | 内部管理系统            | 自宿主部署简单    |
| 🤖 **IoT 网关**   | 设备数据接口服务          | 低资源占用      |
| 📡 **实时推送**     | SSE 服务器推送         | 内置 SSE 支持  |
| 🖥️ **嵌入式 Web** | 设备 Web 管理界面       | 独立进程运行     |
| 📦 **微服务**      | 轻量级微服务节点          | 无需复杂依赖     |

### ❌ 不适合的场景

| 场景             | 推荐替代方案                |
| -------------- | --------------------- |
| 大型企业应用         | ASP.NET Core MVC      |
| 需要 Razor 视图    | ASP.NET Core MVC      |
| WebSocket 双向通信 | SAEA.WebSocket        |
| 复杂认证授权         | ASP.NET Core Identity |

### 与 ASP.NET Core 对比

| 特性             | SAEA.MVC | ASP.NET Core |
| -------------- | -------- | ------------ |
| **启动速度**       | 毫秒级      | 秒级           |
| **内存占用**       | 低（轻量自宿主）   | 较高          |
| **部署复杂度**      | 单文件      | 需运行时/容器      |
| **Razor 视图**   | ❌        | ✅            |
| **依赖注入**       | 手动       | 内置           |
| **异步 IO 性能** | ⭐⭐⭐⭐⭐    | ⭐⭐⭐⭐         |
| **跨平台**        | 全平台      | 全平台          |

***

## 性能对比 📊

### 同机实测（参考）

以下是一轮同机压测的快照。被测站点是装配在 SAEA.Http 之上的 SAEA.MVC 应用；SAEA.Http 作为独立服务器没有单独采集基准。六档各跑 3 轮，表中为中位数。

| 场景 | RPS | p50 / p99 |
|---|---:|---:|
| HTTP 串行（5,000 次，短连接） | 1,144 | 0.70 / 1.78 ms |
| HTTP 并发 16（10 s，短连接） | 5,830 | 2.11 / 12.36 ms |
| HTTP 并发 64（10 s，短连接） | 5,408 | 10.10 / 27.72 ms |
| HTTPS 串行（2,000 次，短连接） | 203 | 4.51 / 8.13 ms |
| 静态 100 KB 串行（命中内存缓存） | 345 RPS / 33.7 MiB·s⁻¹ | 2.51 / 4.41 ms |
| WebSocket 单连接回显 | 7,892 条/秒 | 0.12 / 0.22 ms |

口径与边界：

- 整组都是**短连接**口径：每个请求单独建连、请求带 `Connection: close`、读到 EOF 收尾；长连接口径见下一节。
- 客户端与服务端分为两个进程、同机走 IPv4 回环；HTTPS 为自签证书、固定 TLS 1.2、客户端跳过证书校验；静态文件命中内存缓存，不代表首次磁盘读取与大文件分块路径。
- 三轮原值：HTTP 串行 1,153 / 1,144 / 1,138；并发 16 为 5,830 / 5,380 / 5,831；并发 64 为 5,408 / 5,326 / 5,795；HTTPS 213 / 165 / 203；静态 345 / 281 / 421（33.7 / 27.4 / 41.1 MiB·s⁻¹）；WebSocket 7,892 / 5,752 / 8,127 条/秒。HTTPS 每请求一次握手、静态档只有 300 次样本，这两档轮间波动最大。
- 两个并发档每 3–6 万条连接有 1–8 条没拿到响应（约 1×10⁻⁴，表现为 `unexpected EOF before response header` 或收尾 RST），RPS 的分母是实际完成的请求数。同一套压测器、同一并发打 Kestrel 最小 API 的 33,951 条短连接为 0 失败，定位为 SAEA 连接层的竞态，不是压测器缺陷：上一代连接遗留的断开调用在令牌已被回收复用后，把新连接的套接字关掉了。上表是修复前采集的数据；修复后同一压测器在并发 64 / 128 / 256 下共复测 1,000,000 条短连接，0 条无响应失败（并发 128 有 1 条在客户端 `connect` 阶段本地资源耗尽，请求未发出）。
- 单轮快照：同一探针跨轮波动可达 20%–35%（两档并发在三次重复之间相差约 8%），只能用于同一轮内的相对比较，不能当生产绝对值，也不能与其他框架横向对比。

历史验收记录（旧版本、旧口径的数据）见 [MVC / TLS 验收记录](../../docs/superpowers/plans/2026-11-13-mvc-tls-acceptance.md)。

### 长连接压测（进程隔离）

压测客户端与服务端分为两个进程，按长连接口径测框架自身开销：每 worker 一条 IPv4 回环连接、HTTP/1.1 keep-alive、回传会话 Cookie；并发 1 / 16 / 64。每格预热 3 s 后连续跑两个 5 s 窗口，只取第二个窗口（第一个窗口仍含进程启动的一次性开销），每档重复 3 轮取中位数。5 场景 × 3 并发 × 3 轮 = 45 格全部 `status=ok`。

| 场景 | 并发 1 | 并发 16 | 并发 64 |
| --- | ---: | ---: | ---: |
| 两段式路由 `/bench/echo` | 6,385 / 0.15 | 52,186 / 0.28 | 57,372 / 0.97 |
| 属性路由 `/api/v1/bench/echo` | 6,238 / 0.15 | 55,570 / 0.27 | 55,464 / 0.97 |
| 属性路由 + 路径参数 | 6,030 / 0.16 | 56,289 / 0.27 | 60,763 / 0.92 |
| 输出缓存命中 | 6,783 / 0.14 | 57,016 / 0.27 | 62,105 / 0.91 |
| JSON 返回 | 6,496 / 0.15 | 52,028 / 0.28 | 56,904 / 0.94 |

（每格为 RPS / p50；环境为 Windows 10、16 逻辑核、.NET 10、Release 构建、工作站 GC。）

- p99 中位落在 0.22–0.25 ms（并发 1）、1.04–1.28 ms（并发 16）、3.34–4.08 ms（并发 64）；p50 中位随并发上升为 0.15 → 0.27 → 0.94 ms。
- 吞吐在并发 64 仍高于并发 16（57,372 对 52,186，比值 1.10），没有出现并发升高后的回落。
- 每请求服务端 CPU（进程级 `Process.TotalProcessorTime` ÷ 完成请求数）随并发下降：并发 1 为 160–175 µs，并发 16 为 124–136 µs，并发 64 为 117–125 µs。按窗口折算，并发 1 约占 1.0 个核，并发 16 与 64 都约占 6.4–6.8 个核（16 逻辑核的 40%–43%）；这不代表服务端还有余量，压测客户端与后台程序跑在同一台机器上。
- 五种入口在各并发档内的 RPS 跨度不超过 12%；输出缓存档在并发 64 最高，但缓存收益需要用单独对照才能判定，本表不据此下结论。
- 绝对值不可跨轮比较：同一份代码换个时间复跑，串行档在 6.4k–10k 之间、并发 16 在 37k–56k 之间、并发 64 在 33k–62k 之间都出现过，差异来自后台负载与机器状态。本表只用于同一轮内的相对比较。

口径与边界：

- 连接口径：HTTP/1.1 默认回 `Connection: keep-alive` 并真正复用连接，HTTP/1.0 默认关闭，请求显式 `Connection: close` 时才关闭。
- 会话：无 Cookie 请求每次新建会话，会话过期后由清理线程从会话表移除（`SweepExpired`），长时间压测内存不再随请求数单调增长；本轮仍回传 Cookie 以固定单一会话，减少与会话分配相关的噪声。
- 覆盖范围：只覆盖极短响应体的回环场景，大响应体与 TLS 未纳入本节（TLS 见上一节的短连接档）。

部署建议：高并发宿主建议启用 server GC。依据是同一台 16 核机器上仅切换 GC 模式（不改代码）：旧代码并发 16 从 33.6k–40.1k 提到 42.4k–45.9k，并发 64 从 25.3k–34.4k 提到 43.9k–44.1k，p99 从 9.3 ms 降到 3.9 ms。写法二选一：项目文件加 `<ServerGarbageCollection>true</ServerGarbageCollection>`，或运行时设环境变量 `DOTNET_gcServer=1`。多核、并发连接较多的宿主收益明显；单核或低并发场景收益有限，工作站 GC 的启动与内存占用更友好。

### 每请求分配与 CPU 成本

方法：压测客户端与服务端分为两个进程，五个入口、HTTP/1.1 keep-alive（每 worker 一条原始 socket）、回传会话 Cookie；并发 1 / 16 / 64，预热 3 s 后连续跑两个 5 s 窗口并只取第二个，每档 3 轮取中位数。分配与服务端 CPU 取自同一个窗口。

每请求分配的取数方式：服务端进程内 `GC.GetTotalAllocatedBytes(true)` 在测量窗口首尾取差值 ÷ 窗口内完成请求数，覆盖该进程全部托管分配（socket、解析、分发、编码与运行时记账）。

| 场景 | 并发 1 | 并发 16 | 并发 64 |
| --- | ---: | ---: | ---: |
| 两段式路由 `/bench/echo` | 1,768 | 1,768 | 1,767 |
| 属性路由 `/api/v1/bench/echo` | 1,856 | 1,856 | 1,855 |
| 属性路由 + 路径参数 | 2,512 | 2,512 | 2,511 |
| 输出缓存命中 | 1,896 | 1,896 | 1,895 |
| JSON 返回 | 3,624 | 3,624 | 3,623 |

（单位 B/req。）

每请求 CPU（µs/req，同一窗口）：

| 场景 | 并发 1 | 并发 16 | 并发 64 |
| --- | ---: | ---: | ---: |
| 两段式路由 `/bench/echo` | 160 | 124 | 118 |
| 属性路由 `/api/v1/bench/echo` | 175 | 132 | 125 |
| 属性路由 + 路径参数 | 162 | 136 | 120 |
| 输出缓存命中 | 163 | 135 | 117 |
| JSON 返回 | 166 | 135 | 121 |

结论与边界：

- 分配量与并发几乎无关：同一场景三档并发之间差异小于 0.1%，每请求分配只取决于请求形态，不随并发数变化。
- 五种入口的分配跨度是 1,767–3,624 B/req；JSON 档最高，因为每次请求都要序列化并生成响应体。
- 输出缓存命中档不回 action，服务端计数探针覆盖不到，该行的分母改用客户端完成数，与其余各行同一窗口同一批请求。
- 每请求 CPU 只作同轮相对比较：进程级合计包含 GC、JIT 与线程池的后台开销，不是单请求延迟。

### 与其他 Web 框架对比

| 指标            | SAEA.MVC | ASP.NET Core | NancyFX  | HttpListener |
| ------------- | -------- | ------------ | -------- | ------------ |
| **吞吐量**       | 高        | 高            | 中等       | 较低           |
| **平均延迟**      | 低        | 低            | 中等       | 较高           |
| **内存占用**      | 低（轻量自宿主） | 中等          | 中等       | 低            |
| **启动时间**      | 快（无 IIS/Kestrel） | 较慢       | 中等       | 快            |
| **并发连接**      | 面向高并发 | 面向高并发 | 中等 | 中等 |

本机实测补充（同一台机器、同一套压测器、响应正文逐字节一致）：

| 实现 | 每请求分配（20,000 次窗口） | 稳态（200,000 次） |
| --- | ---: | ---: |
| SAEA.MVC（两段式路由） | 1,770 B | 1,768 B |
| ASP.NET Core 最小 API | 1,017 B | 1,016 B |
| ASP.NET Core MVC 控制器 | 2,040 B | 2,004 B |

- 20,000 次窗口下 Kestrel MVC 控制器含约 35–44 B/req 的启动摊销，稳态为 2,004 B/req；SAEA 与最小 API 的固定项只有约 2 B/req，两个口径基本一致。按稳态算，SAEA.MVC 比 Kestrel MVC 控制器省 12%，是最小 API 的 1.74 倍。
- 两侧线上字节并不等价：SAEA 每个响应多写 `X-Powered-By`、4 个 CORS 头、`Cache-Control`、`Connection`、`Set-Cookie`，约 440 B；`Content-Type` 两侧一致。
- Spring Boot 未在本机实测。公开基准 TechEmpower Round 23（2025-02，最后一轮，项目已于 2026-03 归档）的 Fortunes 测试中 Spring（Java）约 24.4 万 req/s、ASP.NET Core 约 61 万 req/s，前者约为后者的 0.4 倍。这是吞吐口径、跨机跨实现；该轮更换了 56 核服务器与 40 Gbps 网络，网络受限项比前几轮高 3–4 倍，绝对数字不能跨轮比较，也不能与上面的每请求分配（B/req）相减。

### 输出缓存效果

| 场景       | 无缓存        | 有缓存 (60s)   | 效果            |
| -------- | ---------- | ----------- | ------------- |
| JSON API | 每次请求重新生成   | 命中缓存直接返回    | 显著降低 CPU 与延迟  |
| 静态页面     | 每次请求读取/渲染  | 命中缓存直接返回    | 显著降低 IO 与延迟   |

### 异步 IO vs 阻塞模型

| 模型                  | 并发性能  | 内存效率  | CPU 利用率 |
| ------------------- | ----- | ----- | ------- |
| **异步 IO（SAEA.MVC）** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐   |
| Thread Pool         | ⭐⭐⭐   | ⭐⭐⭐   | ⭐⭐⭐     |
| Blocking            | ⭐⭐    | ⭐⭐    | ⭐⭐      |

> 💡 **提示**: 异步 IO 在高并发场景下能避免线程阻塞与线程膨胀。底层实现由 .NET 运行时按平台选择：Windows 使用 IOCP，Linux 使用 epoll，macOS 使用 kqueue。

***

## 常见问题 ❓

### Q1: SAEA.MVC 与 ASP.NET Core MVC 有什么区别？

**A**: 主要区别：

| 特性   | SAEA.MVC    | ASP.NET Core MVC |
| ---- | ----------- | ---------------- |
| 宿主方式 | 自宿主（独立进程）   | Kestrel/IIS      |
| 视图引擎 | 无 Razor     | Razor/MVC Views  |
| 依赖注入 | 手动          | 内置 DI 容器         |
| 配置方式 | 代码配置        | appsettings.json |
| 适用场景 | 轻量级 API、嵌入式 | 企业级应用            |

SAEA.MVC 更适合轻量级、高性能 API 服务场景。

### Q2: 如何启用输出缓存？

**A**: 使用 `OutputCacheAttribute` 特性：

```csharp
[OutputCache(Duration = 60)]  // 缓存 60 秒
public ActionResult GetList()
{
    return Json(GetDataFromDatabase());
}
```

缓存键由「请求动词 + 实际命中路径 + 规范化 query」组成：无 query 时为 `GET:/home/list`，有 query 时为 `GET:/home/list?a=1&b=2`。query 按键名 `Ordinal` 升序排序后用 `&` 连接，因此 `?a=1&b=2` 与 `?b=2&a=1` 是同一条缓存；同一路径不同动词、同一路径不同路径参数各自独立。表单体（`Request.Forms`）不参与缓存键。

### Q3: 如何处理文件上传？

**A**: 通过 `HttpContext.Request.PostFiles` 获取，`FilePart.Data` 是只读 `Stream`（可 `Seek`），保存文件用 `FilePart.Save`（内部从 `Data` 复制，可重复保存）：

```csharp
[HttpPost]
public ActionResult Upload()
{
    var files = HttpContext.Request.PostFiles;
    foreach (var file in files)
    {
        var path = Path.Combine(uploadPath, file.FileName);
        file.Save(path);

        // 需要自行处理内容时直接读 Data 流，读前先归零
        file.Data.Position = 0;
    }
    return Json(new { Count = files.Count });
}
```

### Q4: 如何实现全局异常处理？

**A**: 挂 `SAEAMvcApplication.OnException`：action 抛出的异常会连同一份上下文交给它，返回值直接作为响应。

```csharp
using SAEA.Common.Serialization;
using SAEA.MVC.Hosting;
using SAEA.MVC.Results;

var app = new SAEAMvcApplication(config);

app.OnException += (httpContext, ex) =>
{
    var json = SerializeHelper.Serialize(new { Error = ex.Message });
    return new ContentResult(json, System.Net.HttpStatusCode.InternalServerError);
};
```

`ActionFilterAttribute` 不是异常通道：`OnActionExecuted` 只在 action 正常返回之后执行，看不到抛出的异常；过滤器适合日志、鉴权、改写结果这类横向逻辑。

### Q5: 如何配置跨域 (CORS)？

**A**: 用过滤器给响应补 CORS 头：

```csharp
public class CorsFilter : ActionFilterAttribute
{
    public override ActionResult OnActionExecuting()
    {
        var headers = HttpContext.Current.Response.Headers;
        headers["Access-Control-Allow-Origin"] = "*";
        headers["Access-Control-Allow-Methods"] = "GET,POST,PUT,DELETE,PATCH,HEAD,OPTIONS";
        headers["Access-Control-Allow-Headers"] = "Content-Type";

        // 返回 EmptyResult 表示放行，继续执行 action
        return EmptyResult.Default;
    }

    public override void OnActionExecuted(ref ActionResult result)
    {
    }
}
```

这里的 `Access-Control-Allow-Methods` 是 CORS 预检响应里的允许方法列表，与框架自动生成的响应 `Allow` 头不是一回事：前者供浏览器判断可发起哪些方法，包含 `OPTIONS`；后者按 action 上显式声明的动词生成，仅在 action 显式声明 `[HttpOptions]` 时把 `OPTIONS` 列在末位。

### Q6: 如何实现 SSE 服务器推送？

**A**: 返回 `EventStream`（控制器里用 `GetEventStream()` 创建）：

```csharp
public ActionResult StreamEvents()
{
    var stream = GetEventStream();
    
    // 定时发送事件
    Task.Run(async () =>
    {
        for (int i = 0; i < 10; i++)
        {
            stream.ServerSent($"Event {i} at {DateTime.Now}");
            await Task.Delay(1000);
        }
    });
    
    return stream;
}

// 客户端
// var source = new EventSource("/home/streamevents");
// source.onmessage = e => console.log(e.data);
```

### Q7: 如何部署到生产环境？

**A**: 部署步骤：

1. **发布应用程序**

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

1. **配置文件** (可选)

```csharp
var config = new SAEAMvcApplicationConfig
{
    Root = "wwwroot",
    Port = 80,
    IsStaticsCached = true,
    MaxConnects = 5000,
    Timeout = 180
};
SAEAMvcApplicationConfigBuilder.Write(config);
```

1. **注册为 Windows 服务** (可选)

```bash
sc create SAEAMvc binPath="path/to/your/app.exe" start=auto
```

***

## 核心类 🔧

| 类名                         | 说明                    |
| -------------------------- | --------------------- |
| `SAEAMvcApplication`       | MVC 应用程序主类            |
| `SAEAMvcApplicationConfig` | 应用配置类                 |
| `Controller`               | MVC 控制器基类             |
| `ActionResult`             | 操作结果抽象基类              |
| `JsonResult`               | JSON 序列化结果            |
| `ContentResult`            | 文本/HTML 内容结果          |
| `FileResult`               | 文件下载结果                |
| `BigDataResult`            | 大文件流式传输结果             |
| `EventStream`              | Server-Sent Events 结果 |
| `HttpGetAttribute` 等七个谓词特性 | HTTP 方法特性：`[HttpGet]`、`[HttpPost]`、`[HttpPut]`、`[HttpDelete]`、`[HttpPatch]`、`[HttpHead]`、`[HttpOptions]`，均可用无参或带模板构造 |
| `HttpMethodAttribute`      | 谓词特性抽象基类（`Method` / `Template` / `Order`） |
| `RouteAttribute`           | 路由模板特性                |
| `ActionFilterAttribute`    | AOP 过滤器基类             |
| `OutputCacheAttribute`     | 输出缓存特性                |
| `RouteTable`               | 路由表                   |
| `WSController`             | WebSocket 控制器基类        |
| `WebSocketAttribute`       | 标记 WebSocket 入口 action  |
| `WSMessage`                | WebSocket 收到的消息        |
| `IWebSocketConnection`     | 已建立的 WebSocket 连接     |
| `HttpContext`              | HTTP 上下文；实现 `IReusableHttpContext`，同一条连接复用同一实例，`HttpContext.Current` 指向的实例会在下一次请求被重置，不要跨请求保存 |
| `HttpRequest`              | HTTP 请求对象             |
| `HttpResponse`             | HTTP 响应对象（`SuppressBody` 为 true 时不发送正文，用于 HEAD） |

***

## 目录与命名空间结构

源码按职责分目录，目录、文件与命名空间一一对应（内部类型一并列出）：

| 目录 | 文件（类型） | 命名空间 |
| -------------------------- | --------------------- | ------------------------- |
| `Hosting/` | `SAEAMvcApplication`、`SAEAMvcApplicationConfig`、`SAEAMvcApplicationConfigBuilder`、`HttpContext`、`MvcInvoker` | `SAEA.MVC.Hosting` |
| `Controllers/` | `Controller`、`WSController` | `SAEA.MVC.Controllers` |
| `Results/` | `ActionResult`、`JsonResult`、`ContentResult`、`DataResult`、`FileResult`、`BigDataResult`、`EventStream`、`EmptyResult` | `SAEA.MVC.Results` |
| `Attributes/` | `IFilter`、`ActionFilterAttribute`、`OutputCacheAttribute`、`HttpMethodAttribute`、`HttpGetAttribute`、`HttpPostAttribute`、`HttpPutAttribute`、`HttpDeleteAttribute`、`HttpPatchAttribute`、`HttpHeadAttribute`、`HttpOptionsAttribute`、`RouteAttribute`、`WebSocketAttribute` | `SAEA.MVC.Attributes` |
| `Routing/` | `RouteTable`、`RouteEntry`、`AreaCollection`（internal）、`AttributeRouteTable`（internal）、`AttributeRouteEntry`（internal）、`RoutePath`（internal）、`RouteSegmentMatcher`（internal）、`RouteTemplateParser`（internal） | `SAEA.MVC.Routing` |
| `Invocation/` | `ActionResolver`（internal）、`FilterCollector`（internal）、`VerbPolicy`（internal）、`ParameterBinder`（internal）、`ActionInvoker`（internal）、`RequestPipeline`（internal） | `SAEA.MVC.Invocation` |
| `WebSockets/` | `WebSocketsFeature`（internal）、`MVCWebSocketConnection`（internal）、`WSMessage`（含 `WSMessageType`）、`WebSocketHandshake`（internal）、`WebSocketConnectionRegistry`（internal） | `SAEA.MVC.WebSockets` |
| `Tool/` | `APISdkCodeGenerator`、`CodeType` | `SAEA.MVC.Tool` |
| `Tool/CodeGenerte/` | `ApiMapping`、`CSharpTemple`（含 `SaeaApiSdk`）、`JsTemple` | `SAEA.MVC.Tool.CodeGenerte` |

命名空间规则：所有目录都映射到同名子命名空间，没有例外——`Hosting/` → `SAEA.MVC.Hosting`，`Controllers/` → `SAEA.MVC.Controllers`，`Results/` → `SAEA.MVC.Results`，`Attributes/` → `SAEA.MVC.Attributes`，`Routing/` → `SAEA.MVC.Routing`，`Invocation/` → `SAEA.MVC.Invocation`，`WebSockets/` → `SAEA.MVC.WebSockets`，`Tool/` → `SAEA.MVC.Tool`，`Tool/CodeGenerte/` → `SAEA.MVC.Tool.CodeGenerte`。`SAEA.MVC` 根命名空间下不再有任何类型。

这属于对外可见的破坏性变更：升级后只有 `using SAEA.MVC;` 的代码在用到本包类型时都要补对应子命名空间的 `using` 并重新编译。控制器文件通常需要 `using SAEA.MVC.Attributes;`、`using SAEA.MVC.Controllers;`、`using SAEA.MVC.Results;`，启动与配置文件需要 `using SAEA.MVC.Hosting;`，类型全名（如 `SAEA.MVC.Hosting.HttpContext`）同样要按新命名空间写。

> **本次改名**：类型 `Routing` 改名为 `RouteEntry`，与它「单条 action 的路由条目」的职责对齐；目录名 `Routing/` 沿用原名。
>
> 类型 `HttpGet` / `HttpPost` 已改名为 `HttpGetAttribute` / `HttpPostAttribute`，并新增 `HttpPutAttribute`、`HttpDeleteAttribute`、`HttpPatchAttribute`、`HttpHeadAttribute`、`HttpOptionsAttribute`；控制器里继续写 `[HttpGet]` / `[HttpPost]` 等简写即可（特性名后缀约定）。代码里写 `typeof(HttpGet)` 或按类型名字符串比较的需改用新名。

引用 `SAEA.MVC.Routing` 下的类型时要补一行 `using SAEA.MVC.Routing;`，例如：

```csharp
using SAEA.MVC.Routing;

var table = new RouteTable();
// GetOrAdd 返回的就是一条路由条目，Template 是替换令牌后的绝对路径
RouteEntry entry = table.GetOrAdd(typeof(HomeController), "Index", false);
```

***

## 旧 → 新命名空间对照表

| 类型 | 原命名空间 | 新命名空间 |
| -------------------------- | --------------------- | ------------------------- |
| `RouteTable` | `SAEA.MVC` | `SAEA.MVC.Routing` |
| `Routing`（改名 `RouteEntry`） | `SAEA.MVC` | `SAEA.MVC.Routing` |
| `AreaCollection` | `SAEA.MVC` | `SAEA.MVC.Routing` |
| `AttributeRouteTable` | `SAEA.MVC` | `SAEA.MVC.Routing` |
| `WebSocketsFeature` | `SAEA.MVC` | `SAEA.MVC.WebSockets` |
| `MVCWebSocketConnection` | `SAEA.MVC` | `SAEA.MVC.WebSockets` |
| `WSMessage`、`WSMessageType` | `SAEA.MVC` | `SAEA.MVC.WebSockets` |
| `SAEAMvcApplication`、`SAEAMvcApplicationConfig`、`SAEAMvcApplicationConfigBuilder`、`HttpContext`、`MvcInvoker` | `SAEA.MVC` | `SAEA.MVC.Hosting` |
| `Controller`、`WSController` | `SAEA.MVC` | `SAEA.MVC.Controllers` |
| `ActionResult`、`JsonResult`、`ContentResult`、`DataResult`、`FileResult`、`BigDataResult`、`EventStream`、`EmptyResult` | `SAEA.MVC` | `SAEA.MVC.Results` |
| `IFilter`、`ActionFilterAttribute`、`OutputCacheAttribute`、`HttpMethodAttribute`、`HttpGetAttribute`、`HttpPostAttribute`、`HttpPutAttribute`、`HttpDeleteAttribute`、`HttpPatchAttribute`、`HttpHeadAttribute`、`HttpOptionsAttribute`、`RouteAttribute`、`WebSocketAttribute` | `SAEA.MVC` | `SAEA.MVC.Attributes` |

上表每一行都要补 `using`：把原先的一条 `using SAEA.MVC;` 换成实际用到的子命名空间组合，控制器文件一般是 `SAEA.MVC.Controllers` + `SAEA.MVC.Results` + `SAEA.MVC.Attributes`，启动代码是 `SAEA.MVC.Hosting`，WebSocket 控制器再加 `SAEA.MVC.WebSockets`。

***

## 使用示例 📝

### 启动 MVC 应用

```csharp
using SAEA.MVC.Hosting;

// 读取配置
var config = SAEAMvcApplicationConfigBuilder.Read();

// 创建 MVC 应用
var app = new SAEAMvcApplication(config);

// 设置默认路由
app.SetDefault("home", "index");
app.SetDefault("index.html");

// 设置禁止访问路径
app.SetForbiddenAccessList("/content/");
app.SetForbiddenAccessList(".config");

// 启动服务
app.Start();

Console.WriteLine($"MVC 服务已启动，端口: {config.Port}");
```

### 定义 Controller

```csharp
using SAEA.MVC.Attributes;
using SAEA.MVC.Controllers;
using SAEA.MVC.Results;

[LogAttribute]  // 类级别过滤器
public class HomeController : Controller
{
    [HttpGet]
    public ActionResult Index()
    {
        return Content("Hello, SAEA.MVC!");
    }

    [HttpGet]
    public ActionResult GetInfo(string name, int age)
    {
        var result = new { Name = name, Age = age };
        return Json(result);
    }

    [HttpPost]
    public ActionResult Login(string username, string password)
    {
        if (username == "admin" && password == "123456")
            return Json(new { Success = true, Message = "登录成功" });
        return Json(new { Success = false, Message = "登录失败" });
    }

    [HttpPost]
    public ActionResult Upload(string name)
    {
        var files = HttpContext.Request.PostFiles;
        return Json(new { Name = name, FileCount = files.Count });
    }

    public ActionResult Download()
    {
        var filePath = HttpContext.Server.MapPath("/files/document.pdf");

        // null 表示路径越界或非法，按 404 处理
        if (filePath == null)
        {
            return new ContentResult("o_o，找不到任何内容", System.Net.HttpStatusCode.NotFound);
        }

        return File(filePath);
    }

    [OutputCache(Duration = 60)]
    public ActionResult Cached()
    {
        return Content($"时间: {DateTime.Now}");
    }
}
```

`Server.MapPath` 对越出站点根目录或非法的路径返回 `null`，调用方必须判空后再当作文件路径使用。

### ActionResult 类型

```csharp
// JSON 结果
return Json(new { Id = 1, Name = "Test" });

// 文本/HTML 内容
return Content("<h1>Hello World</h1>");

// 文件下载
return File(@"C:\files\document.pdf");

// 大文件流式传输
return BigData(@"C:\files\large_video.mp4");

// 二进制数据
return Data(new byte[] { 0x01, 0x02, 0x03 });

// 空结果（直接操作 Response）
var response = HttpContext.Response;
response.ContentType = "text/html";
response.Write("<h3>自定义响应</h3>");
response.End();
return Empty();
```

### 附件下载（Content-Disposition）

`File(...)`、`BigData(...)` 的重载与 `FileResult` / `BigDataResult` 的 `fileName` 构造参数会生成附件下载头：

```text
Content-Disposition: attachment; filename="..."; filename*=UTF-8''...
```

```csharp
// 小文件一行式附件下载
return File(filePath, "报告.pdf");

// 大文件流式附件下载
return BigData(filePath, "报告.pdf");

// 直接构造结果
return new FileResult(stream, "application/octet-stream", HttpStatusCode.OK, "报告.pdf");
return new BigDataResult(stream, "application/octet-stream", HttpStatusCode.OK, false, "报告.pdf");
```

- 文件名为 `null` 或空白时按普通响应输出，不写 `Content-Disposition`。
- 文件名只取最后一个路径段（同时识别 `/` 与 `\`），并删除 CR/LF 等控制字符，避免响应头注入。
- 非 ASCII 字符按 RFC 5987 编码进 `filename*`，同时给出 ASCII 回退名；中文在回退名中会变成 `_`。
- 非文件结果（如导出的 CSV 文本）可手动写头：

```csharp
using SAEA.Http.Common;

var disposition = ContentDispositionHelper.Build("导出.csv");
if (disposition != null)
    HttpContext.Current.Response.Headers["Content-Disposition"] = disposition;
```

`ContentDispositionHelper.Build(fileName, inline: true)` 可生成 `inline` 前缀。

### Range 请求（分段下载）

静态文件、`File(...)` 与 `BigData(...)`（含 `FileResult` / `BigDataResult`）都支持单区间 `Range`：

- 超过 4MB 的静态文件走 `BigDataResult` 流式路径，按区间定位后只发送区间字节。
- `bytes=a-b`、`bytes=a-`、`bytes=-N` 命中时返回 `206 Partial Content`，带 `Content-Range: bytes a-b/total` 与 `Accept-Ranges: bytes`。
- 区间超出正文长度时返回 `416 Requested Range Not Satisfiable`，带 `Content-Range: bytes */total` 与 `Content-Length: 0`。
- 没有 `Range` 的普通文件响应仍是 `200`，但会带 `Accept-Ranges: bytes`。
- 多区间（含逗号）与非法语法按 RFC 7233 忽略 `Range`，返回 `200` 全量；不支持 `multipart/byteranges` 与 `If-Range`。
- `HEAD` 带 `Range` 时只回报头，不读正文；`Content-Length` 等于区间长度，`416` 时为 `0`。
- `Range` 只在响应状态为 `200 OK` 时处理；其它状态保持原状态与完整正文，不写 `Accept-Ranges` / `Content-Range`。
- `IsZiped` 只影响内存正文（静态小文件与 `FileResult`）：开启后不启用 `Range`，也不写 `Accept-Ranges`。流式 `BigDataResult` 不做 gzip，`Range` 正常生效且响应不写 `Content-Encoding`。MVC 默认 `IsZiped = false`，开启后同样遵循该规则。
- 不可定位的流（`CanSeek == false`）按完整响应发送：不写 `Accept-Ranges`，不带 `Content-Length`，用 `Connection: close` 界定正文。
- `BigDataResult(Stream ...)` 提供 `leaveOpen`：默认 `false` 关闭调用方传入的流，传 `true` 则由调用方关闭；`filePath` 构造不提供该参数，总是关闭内部打开的文件流。

```csharp
// 控制器返回的文件同样按 Range 切片
public ActionResult Download()
{
    return File(@"C:\files\document.pdf");
}
```

### SSE 服务器推送

```csharp
using SAEA.MVC.Results;

public ActionResult StreamEvents()
{
    var stream = GetEventStream();
    
    stream.ServerSent("Hello Client!");
    stream.ServerSent(DateTime.Now.ToString(), "update");
    
    return stream;
}

// 客户端通过 EventSource 接收
// var source = new EventSource("/home/streamevents");
// source.onmessage = (e) => console.log(e.data);
```

### AOP 过滤器

```csharp
using SAEA.MVC.Attributes;
using SAEA.MVC.Controllers;
using SAEA.MVC.Results;

// 自定义过滤器
public class LogAttribute : ActionFilterAttribute
{
    public override ActionResult OnActionExecuting()
    {
        Console.WriteLine($"请求: {HttpContext.Current.Request.Url}");

        // 返回 EmptyResult 表示放行，继续执行 action；返回其它结果则直接作为响应
        return EmptyResult.Default;
    }

    public override void OnActionExecuted(ref ActionResult result)
    {
        Console.WriteLine("响应完成");
    }
}

// 应用过滤器
[LogAttribute]                       // 类级别过滤器：作用于该控制器全部 action
public class UserController : Controller
{
    [HttpGet]
    [OutputCache(Duration = 60)]     // 方法级别过滤器：只作用于 List
    public ActionResult List()
    {
        return Json(new { Total = 2 });
    }
}
```

### 配置项

```csharp
using SAEA.MVC.Hosting;

var config = new SAEAMvcApplicationConfig
{
    Root = "wwwroot",           // 网站根目录
    Port = 28080,               // 监听端口
    IsStaticsCached = true,     // 静态文件缓存
    IsZiped = false,            // GZIP 压缩
    BufferSize = 1024 * 64,     // 缓冲区大小
    MaxConnects = 1000,         // 最大连接数
    DefaultPage = "index.html", // 默认首页
    IsDebug = false,            // 调试模式
    Timeout = 180,              // 连接空闲回收时间(秒)
    ActionTimeout = 0,          // action 执行超时(秒)，0 表示不启用超时包装（同步 action 在当前线程直接执行），大于 0 才包装超时并返回 408
    MaxRequestBodySize = 20 * 1024 * 1024, // 请求体上限（字节），只限制 body
    LargeBodyThreshold = 1024 * 1024, // 请求体落盘阈值（字节），0 表示始终留在内存
    TempDirectory = null,       // 请求体临时目录，null 表示系统临时目录下的 saea-http
    RequestDrainTimeout = 2000, // 413 后等待并丢弃剩余请求体的超时（毫秒），0 表示不排空
    ConnectTimeout = 6          // 连接超时(秒)
};

SAEAMvcApplicationConfigBuilder.Write(config);
```

### 🧩 处理器链装配

MVC 内部已经装好 HTTP/WebSocket 协议节点与链尾异常终结器，`ConfigurePipeline` 只暴露两者之间的扩展位，每条连接调用一次：

```csharp
using System;
using System.Threading.Tasks;
using SAEA.MVC.Hosting;
using SAEA.Sockets.Pipeline;

public sealed class MyHandler : InboundHandlerAdapter
{
    public override Task ChannelActiveAsync(IChannelHandlerContext ctx)
    {
        Console.WriteLine($"[{ctx.ChannelId}] 连接建立");
        return Task.CompletedTask;
    }

    public override Task ChannelInactiveAsync(IChannelHandlerContext ctx)
    {
        Console.WriteLine($"[{ctx.ChannelId}] 连接断开");
        return Task.CompletedTask;
    }
}

var app = new SAEAMvcApplication(root: "wwwroot", port: 28080);

app.ConfigurePipeline = (channelId, pipeline) =>
{
    pipeline.AddLast(new MyHandler());
};

app.Start();
```

必须在 `app.Start()` 之前赋值；Start 之后再赋值会抛 `InvalidOperationException`。

普通 HTTP 请求由派发节点直接交给 `RequestDelegate`（或 MVC 路由），不会再流到 `ConfigurePipeline` 追加的处理器，因此自定义入站处理器只适合做连接级观测（在线数统计、连接日志等），或处理 WebSocket 升级后下发的原始帧。

### 🔐 TLS / HTTPS / WSS

`app.UseSsl(...)` 透传到 `WebConfig.UseSsl`，在同一个监听端口上同时提供 HTTPS 与 WSS：

```csharp
using System.Security.Authentication;
using SAEA.MVC.Hosting;
using SAEA.Sockets.Ssl;

var app = new SAEAMvcApplication(root: "wwwroot", port: 28443);

app.UseSsl(o =>
{
    o.Protocols = SslProtocols.Tls12;
    o.ServerCertificate = SaeaCertificateLoader.FromPfx("server.pfx", "password");
});

app.Start();
```

要点：

- 必须在 `app.Start()` 之前调用；Start 之后再调用会抛 `InvalidOperationException`。
- 不配置时保持明文 HTTP/WS，行为与之前一致；重复调用以最后一次为准。
- 同一端口同时承载 HTTPS 与 WSS：WebSocket 升级复用同一条加密连接，客户端用 `https://` 与 `wss://` 连接即可。
- 证书可用 `SaeaCertificateLoader.FromPfx` / `FromStore` / `FromPem`；多域名用 `ServerCertificateSelector`（SNI），双向认证用 `ClientCertificates` 配合 `ClientCertificateRequired`。

### 📏 协议上限

| 限制 | 默认值 | 超限行为 |
|------|--------|----------|
| 请求行 + 报头 | 64KB | 返回 431 Request Header Fields Too Large |
| 请求体 | 20MB | 返回 413 Request Entity Too Large |
| 413 排空超时 | 2000ms | `RequestDrainTimeout`，413 后等待并丢弃剩余请求体的最长时间，`0` 表示不排空 |
| `Transfer-Encoding: chunked` | 支持 | 解码后长度受请求体上限约束，原始字节计入分帧上限，超限返回 413 |
| `Expect: 100-continue` | 支持 | 请求体未到齐前先回 100 Continue，每个请求最多一次；与 chunked 可组合 |

请求体上限可用 `SAEAMvcApplicationConfig.MaxRequestBodySize`（或 `SAEAMvcApplicationConfig.json` 同名键）调整，默认 20MB；报头 64KB 仍是固定行为，chunked 解码后的长度同样受该请求体上限约束。

请求体超限时先写出 413，再半关写方向并按有界预算排空剩余请求体：定长请求按 `Content-Length` 与已到达字节之差计算，chunked 无法推断剩余长度时按 `MaxRequestBodySize` 兜底，`400`/`431` 不排空。排空期间到达的字节直接丢弃、不进入业务，上限由 `SAEAMvcApplicationConfig.RequestDrainTimeout`（默认 2000 毫秒，`0` 表示不排空）控制；达到字节上限或超时后关闭连接，避免未读数据触发 RST 让客户端读不到 413。被 400/413 拒绝的请求不会先收到 100 Continue。100 Continue 由 HTTP 层通过裸套接字直发，不经过响应编码链。

请求体超过 `SAEAMvcApplicationConfig.LargeBodyThreshold`（默认 1MB）时解码阶段写入 `TempDirectory`（默认系统临时目录下的 `saea-http`）下的临时文件，请求结束后删除；`LargeBodyThreshold = 0` 表示禁用落盘。分帧仍要求整帧可寻址，落盘只去掉「整帧 → body 数组」的重复复制与请求结束后的驻留，不降低分帧期的单连接峰值。

### 异步 Action

```csharp
using SAEA.MVC.Attributes;
using SAEA.MVC.Controllers;
using SAEA.MVC.Results;

public class AsyncController : Controller
{
    [HttpGet]
    public async Task<ActionResult> GetData()
    {
        var data = await GetDataFromDatabaseAsync();
        return Json(data);
    }

    private async Task<List<User>> GetDataFromDatabaseAsync()
    {
        await Task.Delay(100);
        return new List<User> { new User { Id = 1, Name = "Test" } };
    }
}
```

需要在超时后提前退出时，给 action 加一个 `CancellationToken` 形参：

```csharp
using System.Threading;
using SAEA.MVC.Attributes;
using SAEA.MVC.Controllers;
using SAEA.MVC.Results;

public class ExportController : Controller
{
    [HttpGet]
    public async Task<ActionResult> Export(CancellationToken token)
    {
        var rows = await QueryAsync(token);

        return Json(rows);
    }

    private async Task<List<Row>> QueryAsync(CancellationToken token)
    {
        await Task.Delay(100, token);

        return new List<Row>();
    }
}
```

- 声明了 `CancellationToken` 形参时，令牌由框架按每次请求注入。
- 不声明形参时，从 `((SAEA.MVC.Hosting.HttpContext)HttpContext.Current).RequestAborted` 读取；`HttpContext.Current` 的静态类型是 `SAEA.Http.Model.IHttpContext`，取用前需要判型或强转。
- 只有显式配置 `ActionTimeout > 0` 才启用动作超时包装：单次调用超过 `ActionTimeout` 秒仍未完成时，框架返回 **408 Request Timeout**，并 `Cancel()` 该令牌。默认（`ActionTimeout = 0`）不启用超时包装：同步 action 阻塞会直接占用消费线程，没有 408 兜底；异步 action 由框架真正 `await`，等待期间线程归还线程池，不会被单个请求占死。`Timeout` 只负责连接空闲回收，单独调小它不会改变动作超时，但会提前回收空闲连接；只为缩短动作超时而调小 `Timeout` 会先回收连接、导致 408 写不出去，应改用 `ActionTimeout`。
- 令牌只在本次 action 调用期间有效：action 返回后取消源即释放，不要继续注册回调或交给后台任务长期持有。未进入 action 的请求（404、405、静态文件、`RequestDelegate`）读到的是不可取消的默认令牌。本次调用没有任何取消者时（`ActionTimeout = 0` 且 action 未声明 `CancellationToken` 形参）读到的同样是不可取消的默认令牌，此时 `CanBeCanceled` 为 `false`，语义与「有取消源但从不取消」一致。
- 这是**协作式取消**：只有观察令牌的代码才会停止，比如 `token.ThrowIfCancellationRequested()`、`Task.Delay(..., token)`、向令牌注册的回调。没有观察令牌的同步代码无法被强制终止，会继续在后台跑完，其后续异常由框架观察并丢弃，不会再写响应。

### 异步入口

框架自身也提供了一套异步入口。它和上面的「异步 Action」是两件事：异步 Action 描述你写的控制器方法，异步入口描述框架把请求处理完、并等待响应真正写出。

| 同步入口 | 异步入口 | 说明 |
| --- | --- | --- |
| `IHttpContext.HttpHandle` | `HttpContextBase.HttpHandleAsync` | 请求处理总入口；`IHttpContext` 未新增成员，复用能力由独立接口 `SAEA.Http.Model.IReusableHttpContext` 声明，非 `HttpContextBase` 派生实现仍走同步入口 |
| `HttpContext.GetActionResult` | `HttpContext.GetActionResultAsync` | 路由与 action 结果获取 |
| `MvcInvoker.InvokeResult` | `MvcInvoker.InvokeResultAsync` | 执行 action，异步 action 在这里被真正 `await` |
| `HttpResponse.Send` / `End` | `HttpResponse.SendAsync` / `EndAsync` | 发送响应；`EndAsync` 写完成后才提交关闭 |
| `HttpResponse.WriteRaw` | `HttpResponse.WriteRawAsync` | 直接写原始字节（握手失败等场景） |
| `HttpResponse.SendHeader` / `SendData` / `SendEnd` | 对应的 `*Async` | 分段写场景（SSE、大文件） |
| `WebHost.Send` / `End` | `WebHost.SendAsync` / `EndAsync` | 发送字节；写失败仍只记日志，不向业务抛出 |

约定：

- 同步入口全部保留，内部是「异步实现 + `GetAwaiter().GetResult()`」：一份实现两套入口，不会出现行为漂移。
- `IHttpContext` 接口没有新增成员，复用能力由独立接口 `SAEA.Http.Model.IReusableHttpContext` 声明；只重写了 `HttpHandle` 的自定义实现不会被绕过。
- 自定义 `IWebHost` 实现（不继承内置 `WebHost`）在异步路径上回退为同步写，行为与改动前一致。
- 线程占用实测：64 个并发请求打「`await Task.Delay(200)` 后返回」的 action，服务端进程线程数稳定在 30；把同一个 action 改成同步 `Thread.Sleep(200)`，线程数为 46。等待期间线程确实归还了线程池。
- 异步入口**不提升吞吐**：同一批压测（并发 1 / 16 / 64）改动前后差异在 ±6% 以内。这是预期结果——本机测出的瓶颈是每请求 CPU 与 GC（当前每请求分配约 1.8–3.6 KB，见上文「每请求分配与 CPU 成本」），不是线程模型。

### WebSocket 长连接

WebSocket 复用同一个端口和同一套路由，不需要额外监听端口：继承 `WSController`，在 `OpenAsync` 上标记 `[WebSocket]` 即可。入口默认是 `/{controller}/open`，也可以在 `OpenAsync` 上加 `[Route]` / `[HttpGet]` 声明任意路径。

```csharp
using SAEA.MVC.Attributes;
using SAEA.MVC.Controllers;
using SAEA.MVC.WebSockets;
using System;
using System.Threading.Tasks;

public class ChatController : WSController
{
    [WebSocket]
    public override async Task OpenAsync()
    {
        // 等价于 HttpContext.Current.WebSockets.AcceptWebSocketAsync()
        var ws = await WebSockets.AcceptWebSocketAsync();
        await ws.SendAsync("welcome");
    }

    public override async Task MessageAsync(WSMessage message)
    {
        var ws = WebSockets.Current;
        if (ws == null) return;

        if (message.IsText) await ws.SendAsync(message.Text);
        else await ws.SendAsync(message.Data);
    }

    public override Task CloseAsync(WSCloseReason reason)
    {
        // 连接已关闭，这里只做清理
        return Task.CompletedTask;
    }

    public override Task ErrorAsync(Exception exception)
    {
        return Task.CompletedTask;
    }
}
```

客户端连接：

```javascript
const ws = new WebSocket("ws://127.0.0.1:28080/chat/open");
ws.onmessage = e => console.log(e.data);
ws.send("hello");
```

要点：

- 入口默认是 `/{controller}/open`（两段式路由省略 `Async` 后缀，`OpenAsync` 因此对应地址里的 `open`），属性路由同样生效：给 `OpenAsync` 加 `[Route("ws/chat")]` 后客户端连 `ws://host/ws/chat` 即可。未指向入口的 `/chat/message` 之类地址会被拒绝并返回 400；`POST/PUT/DELETE` 等非 GET 请求即使路由命中了入口，握手校验也只放行 GET，同样返回 400（没命中任何 action 的地址仍是 404）。
- `OpenAsync` 必须调用 `AcceptWebSocketAsync()` 显式接受连接，并尽快返回：它会先写 101 响应，再把连接切到原始字节模式。
- `MessageAsync` 只会收到完整的 Text/Binary 消息（分片已重组）；Ping 由框架自动回 Pong。
- 每条连接都有独立的上下文，`WebSockets.Current` 返回当前连接的 `IWebSocketConnection`，`ws.Id` 可作为连接标识。
- 普通 action、静态文件、SSE 与 WebSocket 共用端口，互不影响。
- 关闭握手：服务端主动关闭时先写 Close 帧并等待底层发送完成，再对发送方向执行 `Shutdown(Send)`（FIN），最后释放会话。这样客户端收到的是正常的 Close 帧，而不是 RST（10054）。

已知限制：

- 不提供广播辅助类。多连接广播需要自行维护 `IWebSocketConnection` 集合（可按 `ws.Id` 索引）。
- 服务端不会主动发起 Ping，需要保活请在业务层定时发送消息或自行实现心跳。
- 握手失败（非升级请求、缺少 `Sec-WebSocket-Key` 等）直接返回 400/426，不进入 MVC 的 `OnException` 异常链。
- 升级后的连接不再参与 HTTP 会话超时回收；正常与异常断开都由 `WebConfig.ConnectionClosed` 通知到 `WebSocketConnectionRegistry.OnDisconnected` 统一收尾。通知在连接发布前到达时 `WebSocketConnectionRegistry.Register` 会补一次存活检查，连接表另有 5 分钟一次的扫描兜底清理 `Closed` 或底层已断开的残项。
- `Request` 与 `Session` 是握手那一刻的快照：WS 消息不会刷新 `Session` 的滑动过期，需要长会话状态请放到连接对象自己的字段/字典里。
- Controller 实例按路由共享，**连接相关的状态不能放在 Controller 字段上**，请用 `WebSockets.Current` 或自建字典按连接 id 管理。
- 每条长连接会一直占用一个 `MaxConnects` 配额，需要按实际并发调大配置。
- `OpenAsync` 必须接受连接后尽快返回：`MvcInvoker` 会以动作超时（`ActionTimeout > 0 ? ActionTimeout : Timeout`，默认 180 秒）同步等待该 Task。超时后已升级的连接不会再写 HTTP 408（那样会污染 WebSocket 流），而是回调 `ErrorAsync` 并以 `ServerShutdown` 关闭；在 `OpenAsync` 里跑接收长循环就会走到这个分支。长活逻辑请放 `MessageAsync`。超时同样会取消该次入口的取消令牌（入口没有 `CancellationToken` 形参时用 `RequestAborted` 读取），在入口里等 `Task.Delay(..., token)` 这类观察令牌的操作会被一并中止。
- `IsDebug = true` 只额外打开请求调试日志，不改变空闲清理策略：长连接的空闲回收统一按 `WebConfig.Timeout`（默认 180 秒）判断，WebSocket 连接在升级后被豁免。
- 配置 `UseSsl` 后支持 HTTPS 与 WSS；`permessage-deflate` 扩展仍不支持；也可以继续在反向代理层终止 TLS。
- 每条连接有 1024 帧 / 4MB 的待处理队列上限（含单条消息），超过会以 `ServerShutdown` 主动关闭连接；超大消息请自行分片。

***

## 依赖项

| 包名           | 版本       | 说明        |
| ------------ | -------- | --------- |
| SAEA.Sockets | 26.9.21.1 | Socket 通信框架 |
| SAEA.Http    | 26.9.21.1 | HTTP 服务器  |
| SAEA.Common  | 26.9.21.1 | 公共工具类     |
| SAEA.WebSocket | 26.10.8.1 | WebSocket 编解码与协议 |

***

## 相关项目

- [SAEA.Rested](https://github.com/yswenli/SAEA.Rested) - 基于 SAEA.MVC 的 REST API 示例项目

***

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包](https://www.nuget.org/packages/SAEA.MVC)
- [作者博客](https://www.cnblogs.com/yswenli/)

***

## 许可证

Apache License 2.0
