# SAEA.MVC - 轻量级高性能 MVC Web 框架

[![NuGet version](https://img.shields.io/nuget/v/SAEA.MVC.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.MVC)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

## 简介

SAEA.MVC 是一个轻量级、高性能的 ASP.NET MVC 框架实现，基于 SAEA.Sockets 的 IOCP 技术。无需 IIS 或 Kestrel，自宿主运行，几行代码即可启动 Web 服务器。

框架提供完整的 MVC 体验：
- **Controller/Action** - 标准的控制器/动作模式
- **路由系统** - 自动发现 Controller，支持 GET/POST 区分
- **多种结果类型** - JSON、Content、File、SSE 等
- **AOP 过滤器** - 支持方法拦截、输出缓存
- **静态文件服务** - 自动区分大小文件优化传输

## 特性

- **自宿主运行** - 无需 IIS/Kestrel，独立进程启动
- **轻量高性能** - 基于 IOCP 异步通信
- **多种 ActionResult** - JSON、Content、File、Data、BigData、SSE
- **AOP 过滤器** - ActionFilterAttribute 方法拦截
- **输出缓存** - OutputCacheAttribute 方法级缓存
- **异步支持** - async/await 异步 Action
- **SSE 推送** - Server-Sent Events 服务器推送
- **静态文件** - 自动缓存、大文件分块传输
- **跨域支持** - CORS 配置

## NuGet 安装

```bash
# Package Manager
Install-Package SAEA.MVC -Version 7.26.2.2

# .NET CLI
dotnet add package SAEA.MVC --version 7.26.2.2
```

## 核心类

| 类名 | 说明 |
|------|------|
| `SAEAMvcApplication` | MVC 应用程序主类 |
| `SAEAMvcApplicationConfig` | 应用配置类 |
| `Controller` | MVC 控制器基类 |
| `ActionResult` | 操作结果抽象基类 |
| `HttpGet/HttpPost` | HTTP 方法特性 |
| `ActionFilterAttribute` | AOP 过滤器基类 |
| `OutputCacheAttribute` | 输出缓存特性 |
| `RouteTable` | 路由表 |
| `HttpContext` | HTTP 上下文 |

## 快速使用

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
        return File(HttpContext.Server.MapPath("/files/document.pdf"));
    }

    [OutputCache(Duration = 60)]
    public ActionResult Cached()
    {
        return Content($"时间: {DateTime.Now}");
    }
}
```

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
    
    // 发送事件
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

## 依赖项

| 包名 | 版本 | 说明 |
|------|------|------|
| SAEA.Sockets | 7.26.2.2 | IOCP 通信框架 |
| SAEA.Http | 7.26.2.2 | HTTP 服务器 |
| SAEA.Common | 7.26.2.2 | 公共工具类 |

## 相关项目

- [SAEA.Rested](https://github.com/yswenli/SAEA.Rested) - 基于 SAEA.MVC 的 REST API 示例项目

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包](https://www.nuget.org/packages/SAEA.MVC)
- [作者博客](https://www.cnblogs.com/yswenli/)

## 许可证

Apache License 2.0