# SAEA.Http - 轻量级高性能 HTTP 服务器

[![NuGet version](https://img.shields.io/nuget/v/SAEA.Http.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.Http)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

## 简介

SAEA.Http 是一个轻量级、高性能的 HTTP 服务器组件，基于 SAEA.Sockets 的 IOCP 技术实现。支持标准 HTTP 方法、静态文件服务、文件上传、Session 管理、GZIP 压缩等功能，是 SAEA.MVC 的底层 HTTP 支持。

## 特性

- **高性能 IOCP** - 基于 SAEA.Sockets 完成端口技术
- **标准 HTTP 方法** - GET/POST/PUT/DELETE/OPTIONS
- **静态文件服务** - 自动缓存、大文件分块传输
- **文件上传** - multipart/form-data 解析
- **Session 管理** - HttpSession 和 HttpSessionManager
- **Cookie 管理** - HttpCookie/HttpCookies
- **GZIP 压缩** - 响应内容压缩
- **跨域 CORS** - 内置跨域支持
- **自定义处理** - RequestDelegate 委托

## NuGet 安装

```bash
# Package Manager
Install-Package SAEA.Http -Version 7.26.2.2

# .NET CLI
dotnet add package SAEA.Http --version 7.26.2.2
```

## 核心类

| 类名 | 说明 |
|------|------|
| `WebHost` | HTTP 服务器入口类 |
| `HttpRequest` | HTTP 请求封装 |
| `HttpResponse` | HTTP 响应封装 |
| `HttpContext` | HTTP 上下文（请求/响应/Session） |
| `HttpCoder` | HTTP 协议编码/解码器 |
| `RequestDataReader` | HTTP 请求解析器 |
| `HttpSession` | HTTP 会话 |
| `HttpSessionManager` | 会话管理器 |
| `WebConfig` | Web 服务器配置 |

## 快速使用

### 启动 HTTP 服务器

```csharp
using SAEA.Http;

// 创建配置
var config = new WebConfig
{
    Root = "wwwroot",
    Port = 28080,
    BufferSize = 1024 * 64,
    MaxConnects = 1000,
    IsStaticsCached = true,
    IsZiped = true
};

// 创建 HTTP 服务器
var server = new WebHost(config);

// 注册自定义请求处理
server.OnRequestDelegate += (context) =>
{
    if (context.Request.Url.StartsWith("/api/"))
    {
        // 处理 API 请求
        var json = $"{{\"time\": \"{DateTime.Now}\"}}";
        context.Response.SetContent(json, "application/json");
        return true;  // 已处理
    }
    return false;  // 未处理，继续默认处理
};

// 注册异常处理
server.OnException += (context, ex) =>
{
    Console.WriteLine($"错误: {ex.Message}");
};

// 启动服务器
server.Start();

Console.WriteLine($"HTTP 服务器已启动，端口: {config.Port}");
```

### 处理 GET 请求

```csharp
server.OnRequestDelegate += (context) =>
{
    var request = context.Request;
    
    Console.WriteLine($"GET {request.Url}");
    Console.WriteLine($"Query: {request.Query["id"]}");
    Console.WriteLine($"Headers: {request.Headers["User-Agent"]}");
    
    // 返回 JSON
    context.Response.SetContent("{\"status\": \"ok\"}", "application/json");
    return true;
};
```

### 处理 POST 请求

```csharp
server.OnRequestDelegate += (context) =>
{
    var request = context.Request;
    
    if (request.Method == "POST")
    {
        // 表单数据
        var username = request.Forms["username"];
        var password = request.Forms["password"];
        
        // JSON 请求体
        var jsonBody = request.Json;
        
        // 上传文件
        var files = request.PostFiles;
        foreach (var file in files)
        {
            Console.WriteLine($"文件: {file.FileName}, 大小: {file.Data.Length}");
        }
        
        context.Response.SetContent("{\"success\": true}", "application/json");
        return true;
    }
    return false;
};
```

### 使用 Session

```csharp
server.OnRequestDelegate += (context) =>
{
    var session = context.Session;
    
    // 获取 Session 值
    var userId = session["UserId"];
    
    // 设置 Session 值
    session["UserId"] = "12345";
    session["LoginTime"] = DateTime.Now.ToString();
    
    // 获取 Session ID
    var sessionId = session.SessionID;
    
    context.Response.SetContent($"Session ID: {sessionId}");
    return true;
};
```

### 使用 Cookie

```csharp
server.OnRequestDelegate += (context) =>
{
    var request = context.Request;
    var response = context.Response;
    
    // 读取 Cookie
    var token = request.Cookies["token"];
    
    // 设置 Cookie
    response.Cookies.Add(new HttpCookie
    {
        Name = "token",
        Value = "abc123",
        Expires = DateTime.Now.AddDays(7)
    });
    
    context.Response.SetContent("Cookie 已设置");
    return true;
};
```

### 返回文件

```csharp
server.OnRequestDelegate += (context) =>
{
    var filePath = context.Server.MapPath("/files/document.pdf");
    
    // 小文件（自动缓存）
    context.Response.SetCached(filePath);
    return true;
};

// 大文件流式传输
server.OnRequestDelegate += (context) =>
{
    var filePath = context.Server.MapPath("/videos/movie.mp4");
    
    // 发送响应头
    context.Response.ContentType = "video/mp4";
    context.Response.SendHeader();
    
    // 分块发送数据
    using (var fs = new FileStream(filePath, FileMode.Open))
    {
        var buffer = new byte[1024 * 64];
        int read;
        while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
        {
            context.Response.SendData(buffer, 0, read);
        }
    }
    
    context.Response.SendEnd();
    return true;
};
```

### SSE 服务器推送

```csharp
server.OnRequestDelegate += (context) =>
{
    context.Response.ContentType = "text/event-stream";
    context.Response.SendHeader();
    
    // 发送事件
    for (int i = 0; i < 10; i++)
    {
        context.Response.SendData($"data: Message {i}\n\n");
        Thread.Sleep(1000);
    }
    
    context.Response.SendEnd();
    return true;
};
```

### 静态文件服务

静态文件服务由框架自动处理：
- 小文件自动缓存（MemoryCache）
- 大文件分块传输
- 支持 Cache-Control、Last-Modified
- 支持 MIME 类型自动识别

## 配置项

```csharp
var config = new WebConfig
{
    Root = "wwwroot",           // 网站根目录
    Port = 28080,               // 监听端口
    BufferSize = 1024 * 64,     // 缓冲区大小（64KB）
    MaxConnects = 1000,         // 最大连接数
    IsStaticsCached = true,     // 静态文件缓存
    IsZiped = false,            // GZIP 压缩
    ConnectTimeout = 6000       // 连接超时（毫秒）
};
```

## HTTP 结果类型

| 类名 | 说明 |
|------|------|
| `HttpContentResult` | 文本内容结果 |
| `HttpFileResult` | 文件结果 |
| `HttpBigDataResult` | 大数据流式结果 |
| `HttpEmptyResult` | 空结果 |

## 依赖项

| 包名 | 版本 | 说明 |
|------|------|------|
| SAEA.Sockets | 7.26.2.2 | IOCP 通信框架 |
| SAEA.Common | 7.26.2.2 | 公共工具类 |

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包](https://www.nuget.org/packages/SAEA.Http)
- [作者博客](https://www.cnblogs.com/yswenli/)

## 许可证

Apache License 2.0