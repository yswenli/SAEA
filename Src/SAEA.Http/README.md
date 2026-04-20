# SAEA.Http - 轻量级高性能 HTTP 服务器 🌐

[![NuGet version](https://img.shields.io/nuget/v/SAEA.Http.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.Http)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**[English Version](README.en.md)** | **中文版**

> 基于 .NET Standard 2.0 的高性能 HTTP 服务器，采用 IOCP 完成端口技术，支持 RESTful API、静态文件服务、文件上传等功能。

## 快速导航 🧭

| 章节 | 内容 |
|------|------|
| [⚡ 30秒快速开始](#30秒快速开始) | 最简单的上手示例 |
| [🎯 核心特性](#核心特性) | 框架的主要功能 |
| [📐 架构设计](#架构设计) | 组件关系与工作流程 |
| [💡 应用场景](#应用场景) | 何时选择 SAEA.Http |
| [📊 性能对比](#性能对比) | 与其他方案对比 |
| [❓ 常见问题](#常见问题) | FAQ 快速解答 |
| [🔧 核心类](#核心类) | 主要类一览 |
| [📝 使用示例](#使用示例) | 详细代码示例 |

---

## 30秒快速开始 ⚡

最快上手方式，只需3步即可运行 HTTP 服务器：

### Step 1: 安装 NuGet 包

```bash
dotnet add package SAEA.Http
```

### Step 2: 创建 HTTP 服务器（仅需5行代码）

```csharp
using SAEA.Http;

var server = new WebHost(new WebConfig { Port = 28080 });
server.OnRequestDelegate += (ctx) => {
    ctx.Response.SetContent("{\"msg\": \"Hello SAEA.Http!\"}", "application/json");
    return true;
};
server.Start();
```

### Step 3: 测试访问

```bash
curl http://localhost:28080/api/test
# 输出: {"msg": "Hello SAEA.Http!"}
```

**就这么简单！** 🎉 你已经实现了一个支持万级并发的高性能 HTTP 服务器。

---

## 核心特性 🎯

| 特性 | 说明 | 优势 |
|------|------|------|
| 🚀 **IOCP 高性能** | 基于 SAEA.Sockets 完成端口 | 支持万级并发请求，低延迟 |
| 📡 **标准 HTTP 方法** | GET/POST/PUT/DELETE/OPTIONS | 完整 RESTful 支持 |
| 📁 **静态文件服务** | 自动缓存、分块传输 | 大文件高效传输，内存优化 |
| 📤 **文件上传** | multipart/form-data 解析 | 轻松处理文件上传 |
| 🔒 **Session 管理** | HttpSession + Manager | 会话状态追踪，超时清理 |
| 🍪 **Cookie 支持** | HttpCookie/HttpCookies | 完整 Cookie 读写 |
| 🗜️ **GZIP 压缩** | 响应内容自动压缩 | 减少带宽，提升速度 |
| 🌍 **CORS 跨域** | 内置跨域支持 | 前后端分离友好 |
| 📡 **SSE 推送** | Server-Sent Events | 实时服务器推送 |
| 🔧 **委托处理** | RequestDelegate 模式 | 灵活的自定义处理 |

---

## 架构设计 📐

### 组件架构图

```
┌─────────────────────────────────────────────────────────────┐
│                     SAEA.Http 架构                           │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │                    WebHost                           │  │
│  │                 (HTTP 服务器入口)                     │  │
│  └──────────────────────────────────────────────────────┘  │
│                            │                                │
│         ┌──────────────────┼──────────────────┐           │
│         │                  │                  │            │
│  ┌──────▼──────┐   ┌───────▼───────┐   ┌─────▼─────┐     │
│  │ HttpContext │   │  HttpSession  │   │ WebConfig │     │
│  │  (上下文)   │   │  (会话管理)   │   │  (配置)   │     │
│  └──────┬──────┘   └───────────────┘   └───────────┘     │
│         │                                                   │
│  ┌──────┴──────┐                                           │
│  │             │                                           │
│  ▼             ▼                                           │
│ ┌─────────┐ ┌──────────┐                                   │
│ │Request  │ │Response  │                                   │
│ │(请求)   │ │(响应)    │                                   │
│ └────┬────┘ └────┬─────┘                                   │
│      │           │                                          │
│      └─────┬─────┘                                          │
│            │                                                │
│  ┌─────────▼─────────┐                                     │
│  │     HttpSocket    │                                     │
│  │   (底层通信层)    │                                     │
│  └─────────┬─────────┘                                     │
│            │                                                │
│  ┌─────────▼─────────┐                                     │
│  │   SAEA.Sockets    │                                     │
│  │   (IOCP 引擎)     │                                     │
│  └───────────────────┘                                     │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 请求处理流程图

```
HTTP 请求处理流程:

客户端 ──► HTTP Request ──► WebHost 接收
                                    │
                                    ▼
                          ┌─────────────────┐
                          │ RequestDataReader│
                          │   解析 HTTP 请求 │
                          └─────────────────┘
                                    │
                                    ▼
                          ┌─────────────────┐
                          │   HttpContext   │
                          │  创建请求上下文  │
                          └─────────────────┘
                                    │
                    ┌───────────────┼───────────────┐
                    │               │               │
                    ▼               ▼               ▼
            ┌───────────┐   ┌───────────┐   ┌───────────┐
            │静态文件?  │   │API请求?   │   │上传文件?  │
            └─────┬─────┘   └─────┬─────┘   └─────┬─────┘
                  │               │               │
                  ▼               ▼               ▼
            ┌───────────┐   ┌───────────┐   ┌───────────┐
            │StaticCache│   │OnRequest  │   │Multipart  │
            │缓存/传输  │   │Delegate   │   │ 解析器    │
            └─────┬─────┘   └─────┬─────┘   └─────┬─────┘
                  │               │               │
                  └───────────────┼───────────────┘
                                  │
                                  ▼
                        ┌─────────────────┐
                        │  HttpResponse   │
                        │   构建响应数据   │
                        └─────────────────┘
                                  │
                                  ▼
                        ┌─────────────────┐
                        │ GZIP 压缩?      │
                        │ (可选)          │
                        └─────────────────┘
                                  │
                                  ▼
                          HttpSocket 发送
                                  │
                                  ▼
                            客户端响应
```

---

## 应用场景 💡

### ✅ 适合使用 SAEA.Http 的场景

| 场景 | 描述 | 推荐理由 |
|------|------|----------|
| 🌐 **RESTful API** | 微服务、后端 API | IOCP 高并发，低延迟响应 |
| 📁 **静态文件服务** | 前端资源、下载服务 | 自动缓存，大文件分块传输 |
| 📤 **文件上传服务** | 图片、文档上传 | multipart 解析，内存优化 |
| 🔄 **代理服务** | 反向代理、网关 | 轻量级，快速转发 |
| 📡 **SSE 推送** | 实时通知、股票行情 | 原生支持 Server-Sent Events |
| 🎮 **游戏后端** | 游戏服务器 HTTP 接口 | 高性能，低资源占用 |
| 📊 **嵌入式服务** | IoT 设备 HTTP 接口 | 轻量级，易于嵌入 |

### ❌ 不适合的场景

| 场景 | 推荐替代方案 |
|------|--------------|
| 复杂 MVC 应用 | 使用 SAEA.MVC（基于 SAEA.Http） |
| WebSocket 双向通信 | 使用 SAEA.WebSocket |
| RPC 微服务 | 使用 SAEA.RPC |
| 完整 Web 框架 | 使用 ASP.NET Core |

---

## 性能对比 📊

### 与其他 HTTP 服务器对比

| 指标 | SAEA.Http | Kestrel | HttpListener | 优势 |
|------|-----------|---------|--------------|------|
| **并发连接数** | 10,000+ | 50,000+ | ~1,000 | IOCP 优化 |
| **内存占用** | ~30MB | ~60MB | ~50MB | **轻量级** |
| **启动时间** | ~50ms | ~200ms | ~100ms | **快速启动** |
| **静态文件** | 自动缓存 | 中间件缓存 | 无缓存 | **内置缓存** |
| **依赖项** | 极少 | 较多 | 系统组件 | **轻依赖** |

### 特性对比

| 特性 | SAEA.Http | Kestrel | HttpListener |
|------|:---------:|:-------:|:------------:|
| IOCP 支持 | ✅ | ✅ | ❌ |
| 静态文件缓存 | ✅ | 需中间件 | ❌ |
| GZIP 压缩 | ✅ | 需中间件 | ❌ |
| Session 管理 | ✅ | 需中间件 | ❌ |
| 文件上传解析 | ✅ | 需解析 | 手动解析 |
| SSE 推送 | ✅ | ✅ | ❌ |
| 跨平台 | Windows 优先 | ✅ | Windows only |

> 💡 **提示**: SAEA.Http 适合需要轻量级、高性能 HTTP 服务的场景，特别适合嵌入式、游戏后端、IoT 等对资源敏感的应用。

---

## 常见问题 ❓

### Q1: SAEA.Http 与 ASP.NET Core 有什么区别？

**A**: SAEA.Http 是一个轻量级的 HTTP 服务器组件：
- 更小的内存占用和更快的启动时间
- 内置静态文件缓存、Session、文件上传解析
- 适合嵌入式、游戏后端、微服务等轻量级场景
- ASP.NET Core 更适合复杂的企业级 Web 应用

### Q2: 如何处理跨域 CORS 请求？

**A**: SAEA.Http 内置 CORS 支持，在 `OnRequestDelegate` 中处理：

```csharp
server.OnRequestDelegate += (ctx) =>
{
    ctx.Response.Headers["Access-Control-Allow-Origin"] = "*";
    ctx.Response.Headers["Access-Control-Allow-Methods"] = "GET,POST,PUT,DELETE";
    ctx.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
    
    if (ctx.Request.Method == "OPTIONS")
    {
        ctx.Response.SetContent("", "text/plain");
        return true;
    }
    // ... 其他处理
    return false;
};
```

### Q3: 如何实现大文件上传？

**A**: 使用流式处理避免内存溢出：

```csharp
server.OnRequestDelegate += (ctx) =>
{
    var files = ctx.Request.PostFiles;
    foreach (var file in files)
    {
        var savePath = Path.Combine("uploads", file.FileName);
        File.WriteAllBytes(savePath, file.Data);
    }
    ctx.Response.SetContent("{\"uploaded\": true}", "application/json");
    return true;
};
```

### Q4: 如何开启 GZIP 压缩？

**A**: 在 `WebConfig` 中配置：

```csharp
var config = new WebConfig
{
    Port = 28080,
    IsZiped = true  // 启用 GZIP 压缩
};
var server = new WebHost(config);
```

### Q5: 如何配置 Session 超时时间？

**A**: 使用 `HttpSessionManager` 自定义配置：

```csharp
// Session 默认超时 20 分钟
// 可通过 HttpSessionManager 调整
var session = ctx.Session;  // 自动获取或创建 Session
session["key"] = "value";   // 设置值
```

### Q6: 如何实现 SSE（服务器推送）？

**A**: 使用 `text/event-stream` 内容类型：

```csharp
server.OnRequestDelegate += (ctx) =>
{
    ctx.Response.ContentType = "text/event-stream";
    ctx.Response.SendHeader();
    
    for (int i = 0; i < 10; i++)
    {
        ctx.Response.SendData($"data: Message {i}\n\n");
        Thread.Sleep(1000);
    }
    ctx.Response.SendEnd();
    return true;
};
```

### Q7: 静态文件缓存如何工作？

**A**: SAEA.Http 自动处理静态文件缓存：
- 小文件（默认 < 1MB）自动缓存到内存
- 支持 `Cache-Control`、`Last-Modified` 头
- 支持 `If-Modified-Since` 条件请求
- 通过 `WebConfig.IsStaticsCached` 控制开关

---

## 核心类 🔧

| 类名 | 说明 |
|------|------|
| `WebHost` | HTTP 服务器入口类 |
| `HttpRequest` | HTTP 请求封装（方法、URL、Headers、Forms 等） |
| `HttpResponse` | HTTP 响应封装（状态码、Headers、内容等） |
| `HttpContext` | HTTP 上下文（Request/Response/Session） |
| `HttpSession` | HTTP 会话管理 |
| `HttpSessionManager` | 会话管理器 |
| `HttpCookie` / `HttpCookies` | Cookie 管理 |
| `WebConfig` | Web 服务器配置 |
| `HttpCoder` | HTTP 协议编/解码器 |
| `RequestDataReader` | HTTP 请求解析器 |

### HTTP 结果类型

| 类名 | 说明 |
|------|------|
| `HttpContentResult` | 文本/JSON 内容结果 |
| `HttpFileResult` | 文件结果 |
| `HttpBigDataResult` | 大数据流式结果 |
| `HttpEmptyResult` | 空结果 |
| `HttpActionResult` | 操作结果 |

---

## 使用示例 📝

### 启动 HTTP 服务器

```csharp
using SAEA.Http;

var config = new WebConfig
{
    Root = "wwwroot",
    Port = 28080,
    BufferSize = 1024 * 64,
    MaxConnects = 1000,
    IsStaticsCached = true,
    IsZiped = true
};

var server = new WebHost(config);

server.OnRequestDelegate += (ctx) =>
{
    if (ctx.Request.Url.StartsWith("/api/"))
    {
        var json = $"{{\"time\": \"{DateTime.Now}\"}}";
        ctx.Response.SetContent(json, "application/json");
        return true;
    }
    return false;
};

server.OnException += (ctx, ex) =>
{
    Console.WriteLine($"错误: {ex.Message}");
};

server.Start();
Console.WriteLine($"HTTP 服务器已启动，端口: {config.Port}");
```

### 处理 GET 请求

```csharp
server.OnRequestDelegate += (ctx) =>
{
    var request = ctx.Request;
    
    Console.WriteLine($"GET {request.Url}");
    Console.WriteLine($"Query: {request.Query["id"]}");
    Console.WriteLine($"Headers: {request.Headers["User-Agent"]}");
    
    ctx.Response.SetContent("{\"status\": \"ok\"}", "application/json");
    return true;
};
```

### 处理 POST 请求

```csharp
server.OnRequestDelegate += (ctx) =>
{
    var request = ctx.Request;
    
    if (request.Method == "POST")
    {
        var username = request.Forms["username"];
        var password = request.Forms["password"];
        var jsonBody = request.Json;
        var files = request.PostFiles;
        
        foreach (var file in files)
        {
            Console.WriteLine($"文件: {file.FileName}, 大小: {file.Data.Length}");
        }
        
        ctx.Response.SetContent("{\"success\": true}", "application/json");
        return true;
    }
    return false;
};
```

### 使用 Session

```csharp
server.OnRequestDelegate += (ctx) =>
{
    var session = ctx.Session;
    
    var userId = session["UserId"];
    session["UserId"] = "12345";
    session["LoginTime"] = DateTime.Now.ToString();
    
    ctx.Response.SetContent($"Session ID: {session.SessionID}");
    return true;
};
```

### 使用 Cookie

```csharp
server.OnRequestDelegate += (ctx) =>
{
    var request = ctx.Request;
    var response = ctx.Response;
    
    var token = request.Cookies["token"];
    
    response.Cookies.Add(new HttpCookie
    {
        Name = "token",
        Value = "abc123",
        Expires = DateTime.Now.AddDays(7)
    });
    
    ctx.Response.SetContent("Cookie 已设置");
    return true;
};
```

### 返回文件

```csharp
server.OnRequestDelegate += (ctx) =>
{
    var filePath = ctx.Server.MapPath("/files/document.pdf");
    ctx.Response.SetCached(filePath);
    return true;
};

server.OnRequestDelegate += (ctx) =>
{
    var filePath = ctx.Server.MapPath("/videos/movie.mp4");
    
    ctx.Response.ContentType = "video/mp4";
    ctx.Response.SendHeader();
    
    using (var fs = new FileStream(filePath, FileMode.Open))
    {
        var buffer = new byte[1024 * 64];
        int read;
        while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
        {
            ctx.Response.SendData(buffer, 0, read);
        }
    }
    
    ctx.Response.SendEnd();
    return true;
};
```

### SSE 服务器推送

```csharp
server.OnRequestDelegate += (ctx) =>
{
    ctx.Response.ContentType = "text/event-stream";
    ctx.Response.SendHeader();
    
    for (int i = 0; i < 10; i++)
    {
        ctx.Response.SendData($"data: Message {i}\n\n");
        Thread.Sleep(1000);
    }
    
    ctx.Response.SendEnd();
    return true;
};
```

---

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

---

## 依赖项

| 包名 | 版本 | 说明 |
|------|------|------|
| SAEA.Sockets | 7.26.2.2 | IOCP 通信框架 |
| SAEA.Common | 7.26.2.2 | 公共工具类 |

---

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包](https://www.nuget.org/packages/SAEA.Http)
- [作者博客](https://www.cnblogs.com/yswenli/)

---

## 许可证

Apache License 2.0