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
using SAEA.MVC;

var config = SAEAMvcApplicationConfigBuilder.Read();
var app = new SAEAMvcApplication(config);
app.SetDefault("home", "index");
app.Start();
```

### Step 3: 创建第一个 Controller

```csharp
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

## 核心特性 🎯

| 特性              | 说明                    | 优势              |
| --------------- | --------------------- | --------------- |
| 🚀 **跨平台异步 IO** | 基于 SAEA.Sockets 异步模型  | 面向高并发场景，CPU 利用率高 |
| 🏠 **自宿主运行**    | 无需 IIS/Kestrel        | 独立进程，部署简单       |
| 📦 **轻量级**      | 无复杂依赖                 | 启动快，内存占用低       |
| 🎯 **完整 MVC**   | Controller/Action/路由  | 标准 MVC 开发体验     |
| 🔧 **AOP 过滤器**  | ActionFilterAttribute | 方法拦截、日志、权限控制    |
| 💾 **输出缓存**     | OutputCacheAttribute  | 方法级缓存，提升响应速度    |
| 📡 **SSE 推送**   | Server-Sent Events    | 实时服务器推送         |
| 📁 **静态文件**     | 自动缓存、大文件分块            | 智能区分大小文件传输      |
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

ActionResult ──┬── JsonActionResult ──► JSON 序列化输出
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
               └── SSEActionResult ───► Server-Sent Events 推送
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

以下是一轮同机压测的快照。被测站点是装配在 SAEA.Http 之上的 SAEA.MVC 应用；SAEA.Http 作为独立服务器没有单独采集基准。

| 场景 | 结果 |
|---|---:|
| HTTP 串行（5000 次，短连接） | 943 RPS |
| HTTP 并发 16（10s） | 4140 RPS |
| HTTP 并发 64（10s） | 3529 RPS |
| HTTPS 串行（2000 次，短连接） | 220 RPS |
| 静态 100KB 串行（命中内存缓存） | 529 RPS / 51.7 MiB/s |
| WebSocket 单连接回显 | 7078 条/秒 |

口径与边界：

- 客户端与服务端同机、同进程、走 IPv4 回环；并发场景下压测客户端本身已是瓶颈之一（本组 64 并发 RPS 为 16 并发的 0.85 倍），不能据此推断服务端的扩展性上限。
- 服务端对普通响应固定 `Connection: close`，每个请求都含一次 TCP 建连与断开，HTTPS 再叠加一次 TLS 握手，RPS 主要由建连成本决定，不代表业务处理上限。
- 自签证书、固定 TLS 1.2、客户端跳过证书校验；静态文件命中内存缓存，不代表首次磁盘读取与大文件分块传输路径。
- 单次运行快照，同一探针跨轮波动可达 20%–35%（同机另一次复跑为 HTTP 串行 749 RPS、HTTPS 串行 183 RPS、静态 100KB 串行 515 RPS、WebSocket 单连接 5782 条/秒）。只能用于同一轮内的相对比较，不能当生产绝对值，也不能与其他框架横向对比。

完整数据、压测失败率与资源占用见 [MVC / TLS 验收记录](../../docs/superpowers/plans/2026-11-13-mvc-tls-acceptance.md)。

### 与其他 Web 框架对比

| 指标            | SAEA.MVC | ASP.NET Core | NancyFX  | HttpListener |
| ------------- | -------- | ------------ | -------- | ------------ |
| **吞吐量**       | 高        | 高            | 中等       | 较低           |
| **平均延迟**      | 低        | 低            | 中等       | 较高           |
| **内存占用**      | 低（轻量自宿主） | 中等          | 中等       | 低            |
| **启动时间**      | 快（无 IIS/Kestrel） | 较慢       | 中等       | 快            |
| **并发连接**      | 面向高并发 | 面向高并发 | 中等 | 中等 |

> 📌 上表为定性比较，SAEA 未发布跨框架基准数据；实际性能取决于硬件与负载，请针对自身场景实测。

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

### Q3: 如何处理文件上传？

**A**: 通过 `HttpContext.Request.PostFiles` 获取：

```csharp
[HttpPost]
public ActionResult Upload()
{
    var files = HttpContext.Request.PostFiles;
    foreach (var file in files)
    {
        var path = Path.Combine(uploadPath, file.FileName);
        file.Save(path);
    }
    return Json(new { Count = files.Count });
}
```

### Q4: 如何实现全局异常处理？

**A**: 创建全局异常过滤器：

```csharp
public class GlobalExceptionFilter : ActionFilterAttribute
{
    public override void OnActionExecuted(HttpContext httpContext)
    {
        if (httpContext.Exception != null)
        {
            httpContext.Response.StatusCode = 500;
            httpContext.Response.Write(JsonConvert.SerializeObject(new
            {
                Error = httpContext.Exception.Message
            }));
            httpContext.Exception = null;
        }
        base.OnActionExecuted(httpContext);
    }
}

// 应用全局过滤器
[GlobalExceptionFilter]
public class BaseController : Controller { }
```

### Q5: 如何配置跨域 (CORS)？

**A**: 在配置中设置 CORS 头：

```csharp
public class CorsFilter : ActionFilterAttribute
{
    public override void OnActionExecuting(HttpContext httpContext)
    {
        httpContext.Response.Headers["Access-Control-Allow-Origin"] = "*";
        httpContext.Response.Headers["Access-Control-Allow-Methods"] = "GET,POST,PUT,DELETE";
        httpContext.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
        base.OnActionExecuting(httpContext);
    }
}
```

### Q6: 如何实现 SSE 服务器推送？

**A**: 返回 `SSEActionResult`：

```csharp
public ActionResult StreamEvents()
{
    var stream = new SSEActionResult();
    
    // 定时发送事件
    Task.Run(async () =>
    {
        for (int i = 0; i < 10; i++)
        {
            stream.Send("message", $"Event {i} at {DateTime.Now}");
            await Task.Delay(1000);
        }
        stream.Close();
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
| `JsonActionResult`         | JSON 序列化结果            |
| `ContentResult`            | 文本/HTML 内容结果          |
| `FileResult`               | 文件下载结果                |
| `BigDataResult`            | 大文件流式传输结果             |
| `SSEActionResult`          | Server-Sent Events 结果 |
| `HttpGet` / `HttpPost`     | HTTP 方法特性             |
| `ActionFilterAttribute`    | AOP 过滤器基类             |
| `OutputCacheAttribute`     | 输出缓存特性                |
| `RouteTable`               | 路由表                   |
| `WSController`             | WebSocket 控制器基类        |
| `WebSocketAttribute`       | 标记 WebSocket 入口 action  |
| `WSMessage`                | WebSocket 收到的消息        |
| `IWebSocketConnection`     | 已建立的 WebSocket 连接     |
| `HttpContext`              | HTTP 上下文              |
| `HttpRequest`              | HTTP 请求对象             |
| `HttpResponse`             | HTTP 响应对象             |

***

## 使用示例 📝

### 启动 MVC 应用

```csharp
using SAEA.MVC;

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
using SAEA.MVC;

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

### SSE 服务器推送

```csharp
using SAEA.MVC;

public ActionResult StreamEvents()
{
    var stream = GetEventStream();
    
    stream.Send("message", "Hello Client!");
    stream.Send("update", DateTime.Now.ToString());
    
    return stream;
}

// 客户端通过 EventSource 接收
// var source = new EventSource("/home/streamevents");
// source.onmessage = (e) => console.log(e.data);
```

### AOP 过滤器

```csharp
using SAEA.MVC.Model;

// 自定义过滤器
public class LogAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(HttpContext httpContext)
    {
        Console.WriteLine($"请求: {httpContext.Request.Url}");
    }

    public override void OnActionExecuted(HttpContext httpContext)
    {
        Console.WriteLine($"响应完成");
    }
}

// 应用过滤器
[LogAttribute]
public class UserController : Controller
{
    [HttpGet]
    [Log2Attribute]  // 方法级别过滤器
    public ActionResult List()
    {
        return Json(GetUsers());
    }
}
```

### 配置项

```csharp
using SAEA.MVC;

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
    Timeout = 180,              // 接口超时(秒)
    ConnectTimeout = 6          // 连接超时(秒)
};

SAEAMvcApplicationConfigBuilder.Write(config);
```

### 🧩 处理器链装配

MVC 内部已经装好 HTTP/WebSocket 协议节点与链尾异常终结器，`ConfigurePipeline` 只暴露两者之间的扩展位，每条连接调用一次：

```csharp
using System;
using System.Threading.Tasks;
using SAEA.MVC;
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
using SAEA.MVC;
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
| 请求体 | 64MB | 返回 413 Request Entity Too Large |
| `Transfer-Encoding: chunked` | 不支持 | 返回 400 Bad Request，当前不做 chunked 解码 |

这三个上限取自 `HttpRequestSplitter` 的默认构造，`WebConfig` 暂不提供配置项。

### 异步 Action

```csharp
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

### WebSocket 长连接

WebSocket 复用同一个端口和 `/{controller}/{action}` 路由，不需要额外监听端口：继承 `WSController`，在 `OpenAsync` 上标记 `[WebSocket]` 即可。

```csharp
using SAEA.MVC;
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

- 入口固定为 `/{controller}/open`（`OpenAsync` 的 `Async` 后缀在路由中会被忽略）；`/chat/message` 之类的地址，以及 `POST/PUT/DELETE /chat/open` 这类非 GET 请求，都会被握手校验拒绝并返回 400。
- `OpenAsync` 必须调用 `AcceptWebSocketAsync()` 显式接受连接，并尽快返回：它会先写 101 响应，再把连接切到原始字节模式。
- `MessageAsync` 只会收到完整的 Text/Binary 消息（分片已重组）；Ping 由框架自动回 Pong。
- 每条连接都有独立的上下文，`WebSockets.Current` 返回当前连接的 `IWebSocketConnection`，`ws.Id` 可作为连接标识。
- 普通 action、静态文件、SSE 与 WebSocket 共用端口，互不影响。
- 关闭握手：服务端主动关闭时先写 Close 帧并等待底层发送完成，再对发送方向执行 `Shutdown(Send)`（FIN），最后释放会话。这样客户端收到的是正常的 Close 帧，而不是 RST（10054）。

已知限制：

- 不提供广播辅助类。多连接广播需要自行维护 `IWebSocketConnection` 集合（可按 `ws.Id` 索引）。
- 服务端不会主动发起 Ping，需要保活请在业务层定时发送消息或自行实现心跳。
- 握手失败（非升级请求、缺少 `Sec-WebSocket-Key` 等）直接返回 400/426，不进入 MVC 的 `OnException` 异常链。
- 升级后的连接不再参与 HTTP 会话超时回收；正常与异常断开都由 `WebConfig.ConnectionClosed` 通知到 `WebSocketsFeature.OnDisconnected` 统一收尾。通知在连接发布前到达时 `Register` 会补一次存活检查，连接表另有 5 分钟一次的扫描兜底清理 `Closed` 或底层已断开的残项。
- `Request` 与 `Session` 是握手那一刻的快照：WS 消息不会刷新 `Session` 的滑动过期，需要长会话状态请放到连接对象自己的字段/字典里。
- Controller 实例按路由共享，**连接相关的状态不能放在 Controller 字段上**，请用 `WebSockets.Current` 或自建字典按连接 id 管理。
- 每条长连接会一直占用一个 `MaxConnects` 配额，需要按实际并发调大配置。
- `OpenAsync` 必须接受连接后尽快返回：`MvcInvoker` 会以 `WebConfig.Timeout`（默认 180 秒）同步等待该 Task。超时后已升级的连接不会再写 HTTP 408（那样会污染 WebSocket 流），而是回调 `ErrorAsync` 并以 `ServerShutdown` 关闭；在 `OpenAsync` 里跑接收长循环就会走到这个分支。长活逻辑请放 `MessageAsync`。
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
