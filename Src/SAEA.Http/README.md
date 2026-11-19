# SAEA.Http - 轻量级高性能 HTTP 服务器 🌐

[![NuGet version](https://img.shields.io/nuget/v/SAEA.Http.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.Http)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**[English Version](README.en.md)** | **中文版**

> 基于 .NET Standard 2.0 的高性能 HTTP 服务器，采用异步 IO，支持 RESTful API、静态文件服务、文件上传等功能。
>
> ⚡ **流式内核**：接收与发送都跑在 `SAEA.Sockets.Pipeline` 的处理器链上，分帧、协议解析、业务派发与响应编码各占一个节点。

## 快速导航 🧭

| 章节 | 内容 |
|------|------|
| [⚡ 30秒快速开始](#30秒快速开始) | 最简单的上手示例 |
| [🎯 核心特性](#核心特性) | 框架的主要功能 |
| [🔗 处理器链装配](#处理器链装配) | 自定义处理器与装配顺序约束 |
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
using System.Net;
using SAEA.Http;

var server = new WebHost(port: 28080);
server.WebConfig.RequestDelegate = ctx =>
{
    ctx.Response.Status = HttpStatusCode.OK;
    ctx.Response.ContentType = "application/json; charset=utf-8";
    ctx.Response.Write("{\"msg\": \"Hello SAEA.Http!\"}");
    ctx.Response.End();
};
server.Start();
```

### Step 3: 测试访问

```bash
curl http://localhost:28080/api/test
# 输出: {"msg": "Hello SAEA.Http!"}
```

**就这么简单！** 🎉 你已经实现了一个面向高并发场景的高性能 HTTP 服务器。

---

## 核心特性 🎯

| 特性 | 说明 | 优势 |
|------|------|------|
| 🚀 **跨平台异步 IO** | 基于 SAEA.Sockets 异步 IO | 面向高并发请求场景，低延迟 |
| 📡 **标准 HTTP 方法** | GET/POST/PUT/DELETE/PATCH/HEAD/OPTIONS（HEAD 只回报头） | 完整 RESTful 支持 |
| 📁 **静态文件服务** | 自动缓存、分块传输 | 大文件高效传输，内存优化 |
| 📤 **文件上传** | multipart/form-data 解析 | 轻松处理文件上传 |
| 🔒 **Session 管理** | HttpSession + Manager | 会话状态追踪，超时清理 |
| 🍪 **Cookie 支持** | HttpCookie/HttpCookies | 完整 Cookie 读写 |
| 🗜️ **GZIP 压缩** | 响应内容自动压缩 | 减少带宽，提升速度 |
| 🌍 **CORS 跨域** | 内置跨域支持 | 前后端分离友好 |
| 📡 **SSE 推送** | Server-Sent Events | 实时服务器推送 |
| 🔗 **处理器链扩展** | `WebConfig.ConfigurePipeline` | 在协议链上追加自定义入站处理器 |
| 🔐 **TLS / HTTPS** | `WebConfig.UseSsl` | 同一端口承载 HTTPS 与 WSS，证书可来自文件、存储或 PEM |

---

## 处理器链装配 🔗

包内先装好自己的传输接线与协议节点，再把尚未启动的链交给 `WebConfig.ConfigurePipeline`，每条连接调用一次：

```csharp
using SAEA.Http;

var server = new WebHost(port: 28080);

server.WebConfig.ConfigurePipeline = (channelId, pipeline) =>
{
    // 只能在这里追加处理器；Start 之后链结构冻结。
    pipeline.AddLast(new MyInboundHandler());
};

server.Start();
```

装配顺序是硬约束：

- 链首是出站前缀，`HttpResponseEncoder` 在最左、`OutboundSerializingHandler` 在最右；自定义出站处理器必须让出站节点保持连续前缀。
- 协议节点（`HttpRequestDecoder` → `HttpProtocolHandler` → 派发 → 升级后原始帧收口）固定排在你的处理器之前。
- 你在 `ConfigurePipeline` 里追加的处理器排在协议节点之后、链尾 `ExceptionTerminalHandler` 之前。
- `ConfigurePipeline` 必须在 `WebHost.Start()` 之前赋值，服务启动后再赋值会抛 `InvalidOperationException`。
- 业务写一律用 `pipeline.WriteAsync(...)`（或在处理器内用 `ctx.Pipeline.WriteAsync(...)`）；`ctx.WriteAsync(...)` 只在出站回调内向下游转发。

请求/异常/连接关闭三个业务入口都在 `WebConfig` 上，不再有事件订阅：

| 成员 | 说明 |
|------|------|
| `WebConfig.RequestDelegate` | 请求处理入口，签名 `void (IHttpContext)`，非空时取代默认路由处理 |
| `WebConfig.ExceptionHandler` | 异常处理入口，签名 `IHttpResult (IHttpContext, Exception)`，返回 null 表示按默认规则生成错误响应 |
| `WebConfig.ConnectionClosed` | 连接关闭通知，签名 `void (string channelId, Exception ex)`，正常断开时 `ex` 为 null |

### 协议上限

| 限制 | 默认值 | 说明 |
|------|--------|------|
| 请求体长度 | 64MB | `HttpRequestSplitter` 的 `maxRequestSize`，只限制 body |
| 请求头长度 | 64KB | 独立上限，超限回 431 |
| 分帧缓冲 | `MaxFrameLength + 9 + ReadBufferSize` | 由执行器推导，不要手动填小于该值 |
| 并发消息数 | 1 | HTTP 单工请求/响应，同连接串行处理 |
| `Transfer-Encoding: chunked` | 不支持 | 显式回 400，避免把 chunk 字节当成下一条请求 |

---

## TLS / HTTPS 🔐

`WebConfig.UseSsl(...)` 是唯一的 TLS 入口，底层由 SAEA.Sockets 的 TLS 包装层完成握手；配置后 HTTPS 与 `wss://` 共用同一个监听端口：

```csharp
using System.Security.Authentication;
using SAEA.Http;
using SAEA.Sockets.Ssl;

var server = new WebHost(root: "wwwroot", port: 28443);

server.WebConfig.UseSsl(o =>
{
    o.Protocols = SslProtocols.Tls12;
    o.ServerCertificate = SaeaCertificateLoader.FromPfx("server.pfx", "password");
});

server.Start();
```

要点：

- 必须在 `WebHost.Start()` 之前调用；服务启动后再调用会抛 `InvalidOperationException`（与 `ConfigurePipeline` 同一套冻结语义）。
- 不调用 `UseSsl` 时保持明文 HTTP，行为与之前一致；重复调用以最后一次为准。
- 同一端口同时承载 HTTPS 与 WSS：WebSocket 升级复用同一条加密连接，协议层无需区分。
- 证书可用 `SaeaCertificateLoader.FromPfx` / `FromStore` / `FromPem`；多域名可配 `ServerCertificateSelector`（SNI），双向认证用 `ClientCertificates` 配合 `ClientCertificateRequired`。

---

## 架构设计 📐

### 组件架构图

```
┌──────────────────────────────────────────────────────────────┐
│                       SAEA.Http 架构                          │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌────────────────────────────────────────────────────────┐  │
│  │                      WebHost                           │  │
│  │                   (HTTP 服务器入口)                     │  │
│  └────────────────────────────────────────────────────────┘  │
│                            │                                 │
│         ┌──────────────────┼──────────────────┐              │
│         │                  │                  │              │
│  ┌──────▼──────┐   ┌───────▼───────┐   ┌─────▼─────┐         │
│  │ HttpContext │   │  HttpSession  │   │ WebConfig │         │
│  │  (上下文)   │   │  (会话管理)   │   │  (配置)   │         │
│  └──────┬──────┘   └───────────────┘   └───────────┘         │
│         │                                                    │
│  ┌──────┴──────┐                                             │
│  │             │                                             │
│  ▼             ▼                                             │
│ ┌─────────┐ ┌──────────┐                                     │
│ │Request  │ │Response  │                                     │
│ │(请求)   │ │(响应)    │                                     │
│ └────┬────┘ └────┬─────┘                                     │
│      │           │                                           │
│      └─────┬─────┘                                           │
│            │                                                 │
│  ┌─────────▼──────────────────────────┐                      │
│  │            HttpSocket              │                      │
│  │       (处理器链装配与收口)          │                      │
│  └─────────┬──────────────────────────┘                      │
│            │                                                 │
│  ┌─────────▼──────────────────────────┐                      │
│  │      SAEA.Sockets.Pipeline         │                      │
│  │  分帧 / 解码 / 派发 / 编码 / 发送   │                      │
│  └─────────┬──────────────────────────┘                      │
│            │                                                 │
│  ┌─────────▼──────────────────────────┐                      │
│  │           SAEA.Sockets             │                      │
│  │           异步 IO 引擎             │                      │
│  └────────────────────────────────────┘                      │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

### 请求处理流程图

```
HTTP 请求处理流程:

客户端 ──► HTTP Request ──► 接收泵（PipeReader + 分帧器）
                                    │
                                    ▼
                          ┌─────────────────┐
                          │ HttpRequestDecoder│
                          │  解析报头与报体  │
                          └─────────────────┘
                                    │
                                    ▼
                          ┌─────────────────┐
                          │ HttpProtocolHandler│
                          │ 升级协商 / 413/431│
                          └─────────────────┘
                                    │
                                    ▼
                          ┌─────────────────┐
                          │ HttpDispatchHandler│
                          │  复用或创建上下文 │
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
            │StaticCache│   │Request    │   │Multipart  │
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
                        │HttpResponseEncoder│
                        │ 编码 + 串行化    │
                        └─────────────────┘
                                  │
                                  ▼
                          传输层写出（socket）
                                  │
                                  ▼
                            客户端响应
```

### 上下文复用

`HttpContext` 实现 `SAEA.Http.Model.IReusableHttpContext`（`Src/SAEA.Http/Model/IReusableHttpContext.cs`）：同一条连接上的请求复用同一个上下文实例，连接失活时随连接一起丢弃。

- 默认仍是每个请求新建并释放一个上下文；只有实现了 `IReusableHttpContext` 的上下文才会被复用，未声明该接口的自定义 `IHttpContext` 行为不变。
- 复用前框架调用 `ResetForNextRequest()` 把上一次请求留下的状态复位，再把上下文交给 `HttpHandle` / `HttpHandleAsync`。
- 复用只在单条连接内串行发生，`HttpContext.Current` 指向的就是这个被复用的实例；下一次请求会重置它。不要把上下文对象存到后台任务或静态字段里跨请求使用。
- 自定义上下文若声明实现 `IReusableHttpContext`，必须自己在 `ResetForNextRequest()` 中把所有跨请求状态还原成新建实例的初值，否则上一次请求的数据会串到下一次请求；WebSocket 升级后的上下文不参与复用。

---

## 应用场景 💡

### ✅ 适合使用 SAEA.Http 的场景

| 场景 | 描述 | 推荐理由 |
|------|------|----------|
| 🌐 **RESTful API** | 微服务、后端 API | 异步 IO 支撑高并发，低延迟响应 |
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
- 该组为短连接口径（每个请求单独建连），不代表默认 keep-alive 下的吞吐；连接策略为 HTTP/1.1 默认复用连接、HTTP/1.0 默认关闭、请求显式 `Connection: close` 时才关闭。
- 自签证书、固定 TLS 1.2、客户端跳过证书校验；静态文件命中内存缓存，不代表首次磁盘读取与大文件分块传输路径。
- 单次运行快照，同一探针跨轮波动可达 20%–35%（同机另一次复跑为 HTTP 串行 749 RPS、HTTPS 串行 183 RPS、静态 100KB 串行 515 RPS、WebSocket 单连接 5782 条/秒）。只能用于同一轮内的相对比较，不能当生产绝对值，也不能与其他框架横向对比。

完整数据、压测失败率与资源占用见 [MVC / TLS 验收记录](../../docs/superpowers/plans/2026-11-13-mvc-tls-acceptance.md)。

### 请求处理开销

- 响应成帧不再先把报头拼成字符串再编码：报头顺序只有一份分段描述，字符串输出（`BuildHeader`）、长度统计与字节直写都从它派生，最终帧一次分配并直接写入，不再产生报头字符串与中间字节数组。`BuildHeader` 的签名与文本输出不变。
- 请求解析改为在报头文本上按下标扫描：请求行、每行报头、查询串与 Cookie 都不再拆出中间数组，报头字典与查询字典按行数 / 分隔符计数预置容量。
- 进程内同机实测（请求解析 + 分发 + 响应编码，**不含 socket 收发**）：每请求分配 15,640 B → 7,600 B。这是进程内口径，网络全路径的每请求分配见 MVC README 的「每请求分配（A/B 交替复压）」小节。

### 与其他 HTTP 服务器对比

| 指标 | SAEA.Http | Kestrel | HttpListener | 优势 |
|------|-----------|---------|--------------|------|
| **并发连接数** | 面向高并发 | 面向高并发 | 中等 | 异步 IO |
| **内存占用** | 低（轻量级） | 较高 | 中等 | **轻量级** |
| **启动时间** | 快 | 较慢 | 中等 | **快速启动** |
| **静态文件** | 自动缓存 | 中间件缓存 | 无缓存 | **内置缓存** |
| **依赖项** | 极少 | 较多 | 系统组件 | **轻依赖** |

### 特性对比

| 特性 | SAEA.Http | Kestrel | HttpListener |
|------|:---------:|:-------:|:------------:|
| 异步 IO 模型 | ✅ | ✅ | ✅ |
| 静态文件缓存 | ✅ | 需中间件 | ❌ |
| GZIP 压缩 | ✅ | 需中间件 | ❌ |
| Session 管理 | ✅ | 需中间件 | ❌ |
| 文件上传解析 | ✅ | 需解析 | 手动解析 |
| SSE 推送 | ✅ | ✅ | ❌ |
| 跨平台 | ✅ | ✅ | Windows only |

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

**A**: SAEA.Http 内置 CORS 支持，在 `RequestDelegate` 中处理：

```csharp
server.WebConfig.RequestDelegate = ctx =>
{
    ctx.Response.Headers["Access-Control-Allow-Origin"] = "*";
    ctx.Response.Headers["Access-Control-Allow-Methods"] = "GET,POST,PUT,DELETE";
    ctx.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type";

    if (ctx.Request.Method == "OPTIONS")
    {
        ctx.Response.Status = HttpStatusCode.NoContent;
        ctx.Response.End();
        return;
    }
    // ... 其他处理
};
```

需要按域名放行时可调用 `server.SetCrossDomainHeaders("token", "auth")` 追加允许的自定义头。

### Q3: 如何实现大文件上传？

**A**: 上传内容已按 `Content-Length` 完整读入后再交给业务，超过 64MB 会回 413：

```csharp
server.WebConfig.RequestDelegate = ctx =>
{
    var files = ctx.Request.PostFiles;
    foreach (var file in files)
    {
        var savePath = Path.Combine("uploads", file.FileName);
        File.WriteAllBytes(savePath, file.Data);
    }
    ctx.Response.ContentType = "application/json; charset=utf-8";
    ctx.Response.Write("{\"uploaded\": true}");
    ctx.Response.End();
};
```

### Q4: 如何开启 GZIP 压缩？

**A**: 通过 `WebHost` 构造参数开启（默认开启）：

```csharp
var server = new WebHost(port: 28080, isZiped: true);
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

**A**: 使用 `text/event-stream` 内容类型，`SendHeader(-1)` 表示长度未知、保持连接：

```csharp
server.WebConfig.RequestDelegate = ctx =>
{
    ctx.Response.ContentType = "text/event-stream";
    ctx.Response.SendHeader(-1);

    for (int i = 0; i < 10; i++)
    {
        ctx.Response.SendData(Encoding.UTF8.GetBytes($"data: Message {i}\n\n"));
        Thread.Sleep(1000);
    }
    ctx.Response.SendEnd();
};
```

### Q7: 静态文件缓存如何工作？

**A**: SAEA.Http 自动处理静态文件缓存：
- 小文件（默认 < 1MB）自动缓存到内存
- 支持 `Cache-Control`、`Last-Modified` 头
- 支持 `If-Modified-Since` 条件请求
- 通过 `WebHost` 的 `isStaticsCached` 参数控制开关

### Q8: 支持 chunked 请求体吗？

**A**: 不支持。收到 `Transfer-Encoding: chunked` 的请求会直接回 400 并关闭连接，因为按 `Content-Length` 切帧会把 chunk 字节误判成下一条请求。

---

## 核心类 🔧

| 类名 | 说明 |
|------|------|
| `WebHost` | HTTP 服务器入口类 |
| `HttpRequest` | HTTP 请求封装（方法、URL、Headers、Forms 等） |
| `HttpResponse` | HTTP 响应封装（状态码、Headers、内容等） |
| `HttpContext` | HTTP 上下文（Request/Response/Session）；实现 `IReusableHttpContext`，同一条连接复用同一实例，未声明该接口的自定义上下文仍每请求新建 |
| `HttpSession` | HTTP 会话管理 |
| `HttpSessionManager` | 会话管理器 |
| `HttpCookie` / `HttpCookies` | Cookie 管理 |
| `WebConfig` | Web 服务器配置，承载请求/异常/断开委托与链装配回调 |
| `RequestDataReader` | HTTP 请求解析器 |
| `IHttpUpgradeTarget` | 协议升级协商入口 |
| `IChannelPipeline` | 处理器链，业务写入口 `WriteAsync` |

### HTTP 结果类型

| 类名 | 说明 |
|------|------|
| `HttpContentResult` | 文本/JSON 内容结果 |
| `HttpFileResult` | 文件结果 |
| `HttpBigDataResult` | 大数据流式结果 |
| `HttpEmptyResult` | 空结果 |
| `HttpActionResult` | 操作结果 |

### 异步写出

`WebHost` 与 `HttpResponse` 都提供异步入口，同步方法保留：

| 同步入口 | 异步入口 |
| --- | --- |
| `WebHost.Send` / `WebHost.End` | `WebHost.SendAsync` / `WebHost.EndAsync` |
| `HttpResponse.Send` / `HttpResponse.End` | `HttpResponse.SendAsync` / `HttpResponse.EndAsync` |
| `HttpResponse.WriteRaw` | `HttpResponse.WriteRawAsync` |
| `HttpResponse.SendHeader` / `SendData` / `SendEnd` | 对应的 `*Async` |

- 同步方法内部就是「异步实现 + `GetAwaiter().GetResult()`」，两条路径共用一份实现。
- `EndAsync` 先等写出真正完成，再提交关闭，避免大响应体在慢客户端上被关闭截断。
- 写失败仍然只记日志、不向业务抛出，与同步入口一致。
- 自定义 `IWebHost` 实现（不继承内置 `WebHost`）在异步路径上回退为同步写。

---

## 使用示例 📝

### 启动 HTTP 服务器

```csharp
using System.Net;
using SAEA.Http;

var server = new WebHost(
    root: "wwwroot",
    port: 28080,
    isStaticsCached: true,
    isZiped: true,
    bufferSize: 1024 * 64,
    maxConnects: 1000,
    timeout: 180,
    connectTimeout: 3);

server.WebConfig.RequestDelegate = ctx =>
{
    if (ctx.Request.Url.StartsWith("/api/"))
    {
        ctx.Response.ContentType = "application/json; charset=utf-8";
        ctx.Response.Write($"{{\"time\": \"{DateTime.Now}\"}}");
        ctx.Response.End();
    }
};

server.WebConfig.ExceptionHandler = (ctx, ex) =>
{
    Console.WriteLine($"错误: {ex.Message}");
    return null;
};

server.Start();
Console.WriteLine($"HTTP 服务器已启动，端口: 28080");
```

### 处理 GET 请求

```csharp
server.WebConfig.RequestDelegate = ctx =>
{
    var request = ctx.Request;

    Console.WriteLine($"GET {request.Url}");
    Console.WriteLine($"Query: {request.Query["id"]}");
    Console.WriteLine($"Headers: {request.Headers["User-Agent"]}");

    ctx.Response.ContentType = "application/json; charset=utf-8";
    ctx.Response.Write("{\"status\": \"ok\"}");
    ctx.Response.End();
};
```

### 处理 POST 请求

```csharp
server.WebConfig.RequestDelegate = ctx =>
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

        ctx.Response.ContentType = "application/json; charset=utf-8";
        ctx.Response.Write("{\"success\": true}");
        ctx.Response.End();
        return;
    }

    ctx.Response.Status = HttpStatusCode.NotFound;
    ctx.Response.End();
};
```

### 使用 Session

```csharp
server.WebConfig.RequestDelegate = ctx =>
{
    var session = ctx.Session;

    var userId = session["UserId"];
    session["UserId"] = "12345";
    session["LoginTime"] = DateTime.Now.ToString();

    ctx.Response.Write($"Session ID: {session.SessionID}");
    ctx.Response.End();
};
```

### 使用 Cookie

```csharp
server.WebConfig.RequestDelegate = ctx =>
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

    response.Write("Cookie 已设置");
    response.End();
};
```

### 返回文件

`Server.MapPath` 对越出站点根目录或非法的路径返回 `null`，调用方必须判空后再当作文件路径使用。

```csharp
using SAEA.Http.Model;

server.WebConfig.RequestDelegate = ctx =>
{
    var filePath = ctx.Server.MapPath("/files/document.pdf");

    // null 表示路径越界或非法，按 404 处理
    if (filePath == null)
    {
        ctx.Response.SetCached(new HttpContentResult("o_o，找不到任何内容", System.Net.HttpStatusCode.NotFound));
        ctx.Response.End();
        return;
    }

    ctx.Response.SetCached(new HttpFileResult(filePath, true));
    ctx.Response.End();
};

server.WebConfig.RequestDelegate = ctx =>
{
    var filePath = ctx.Server.MapPath("/videos/movie.mp4");
    var fileInfo = new FileInfo(filePath);

    ctx.Response.ContentType = "video/mp4";
    ctx.Response.SendHeader(fileInfo.Length);

    using (var fs = new FileStream(filePath, FileMode.Open))
    {
        var buffer = new byte[1024 * 64];
        int read;
        while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
        {
            // SendData 立即写出，不持有传入数组，可以复用一个缓冲区。
            if (read == buffer.Length)
            {
                ctx.Response.SendData(buffer);
            }
            else
            {
                var chunk = new byte[read];
                Buffer.BlockCopy(buffer, 0, chunk, 0, read);
                ctx.Response.SendData(chunk);
            }
        }
    }

    ctx.Response.SendEnd();
};
```

### SSE 服务器推送

```csharp
server.WebConfig.RequestDelegate = ctx =>
{
    ctx.Response.ContentType = "text/event-stream";
    ctx.Response.SendHeader(-1);

    for (int i = 0; i < 10; i++)
    {
        ctx.Response.SendData(Encoding.UTF8.GetBytes($"data: Message {i}\n\n"));
        Thread.Sleep(1000);
    }

    ctx.Response.SendEnd();
};
```

### 自定义处理器

`ConfigurePipeline` 追加的处理器位于协议节点之后、链尾异常终结器之前。普通 HTTP 请求由派发节点直接交给 `WebConfig.RequestDelegate`，不再往下传；因此这里的入站处理器只适合做连接级观测，或处理升级后下发的原始帧：

```csharp
using SAEA.Sockets.Pipeline;

public sealed class ConnectionObserver : InboundHandlerAdapter
{
    /// <summary>生命周期通知由泵按链顺序逐个调用，不依赖处理器转发。</summary>
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

    /// <summary>只有未被协议层消费的消息会到这里，例如协议升级后的原始帧。</summary>
    public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
    {
        var frame = msg as PooledMessage;
        if (frame != null)
            Console.WriteLine($"[{ctx.ChannelId}] {frame.Frame.Length} 字节");

        return ctx.FireChannelReadAsync(msg);
    }
}

server.WebConfig.ConfigurePipeline = (channelId, pipeline) =>
{
    pipeline.AddLast(new ConnectionObserver());
};
```

升级后的原始帧是池化内存，`Frame` 只能在本次回调内读取；要跨回调或异步保存，必须先复制。

---

## 配置项

`WebConfig` 的端口、根目录、缓冲区等由 `WebHost` 构造参数初始化；业务可写的配置项如下：

```csharp
var server = new WebHost(
    root: "wwwroot",            // 网站根目录
    port: 28080,                // 监听端口
    isStaticsCached: true,      // 静态文件缓存
    isZiped: false,             // GZIP 压缩
    bufferSize: 1024 * 64,      // 接收缓冲区（64KB）
    maxConnects: 1000,          // 最大连接数
    timeout: 180,               // 连接空闲回收时间（秒）
    connectTimeout: 3);         // 连接建立超时（秒）

server.WebConfig.ActionTimeout = 120; // action 执行超时（秒），0 表示不启用超时包装（同步 action 在当前线程直接执行），大于 0 才包装超时并返回 408
server.WebConfig.ConfigurePipeline = (id, pipeline) => { /* ... */ };
server.WebConfig.UseSsl(o => { /* ... */ });
server.WebConfig.RequestDelegate = ctx => { /* ... */ };
server.WebConfig.ExceptionHandler = (ctx, ex) => null;
server.WebConfig.ConnectionClosed = (id, ex) => { /* ... */ };
```

---

## 依赖项

| 包名 | 说明 |
|------|------|
| SAEA.Sockets | Socket 通信框架 |
| SAEA.Sockets.Pipeline | 处理器链、分帧与编解码基础设施 |
| SAEA.Common | 公共工具类 |

---

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包](https://www.nuget.org/packages/SAEA.Http)
- [作者博客](https://www.cnblogs.com/yswenli/)

---

## 许可证

Apache License 2.0