# SAEA.MVC - Lightweight High-Performance MVC Web Framework 🎨

[![NuGet version](https://img.shields.io/nuget/v/SAEA.MVC.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.MVC)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**English Version** | **[中文版](README.md)**

> A lightweight MVC web framework based on .NET Standard 2.0, using async IO, self-hosted, no IIS/Kestrel required.
>
> ⚡ **Zero-copy core**: built on the SAEA.Sockets Span/Memory zero-copy send/receive path and pooled buffers, significantly reducing allocations and GC.

## Quick Navigation 🧭

| Section | Content |
| --------------------- | ------------- |
| [⚡ 30-Second Quick Start](#30-second-quick-start) | Simplest getting started example |
| [🎯 Core Features](#core-features) | Main features of the framework |
| [📐 Architecture Design](#architecture-design) | Component relationships and workflow |
| [💡 Use Cases](#use-cases) | When to choose SAEA.MVC |
| [📊 Performance Comparison](#performance-comparison) | Comparison with other frameworks |
| [❓ FAQ](#faq) | Quick FAQ answers |
| [🔧 Core Classes](#core-classes) | Overview of main classes |
| [📝 Usage Examples](#usage-examples) | Detailed code examples |

***

## 30-Second Quick Start ⚡

The fastest way to get started - start a web server in just 3 steps:

### Step 1: Install NuGet Package

```bash
dotnet add package SAEA.MVC
```

### Step 2: Create MVC Application (only 5 lines of code)

```csharp
using SAEA.MVC;

var config = SAEAMvcApplicationConfigBuilder.Read();
var app = new SAEAMvcApplication(config);
app.SetDefault("home", "index");
app.Start();
```

### Step 3: Create Your First Controller

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

**That's it!** 🎉 Visit `http://localhost:28080/home/index` to see the result.

***

## Core Features 🎯

| Feature | Description | Advantage |
| --------------- | --------------------- | --------------- |
| 🚀 **Cross-Platform Async IO** | Based on SAEA.Sockets async model | Built for high-concurrency connections, high CPU utilization |
| 🏠 **Self-Hosted** | No IIS/Kestrel required | Independent process, simple deployment |
| 📦 **Lightweight** | No complex dependencies | Fast startup, low memory footprint |
| 🎯 **Complete MVC** | Controller/Action/Routing | Standard MVC development experience |
| 🔧 **AOP Filters** | ActionFilterAttribute | Method interception, logging, permission control |
| 💾 **Output Caching** | OutputCacheAttribute | Method-level caching, improved response speed |
| 📡 **SSE Push** | Server-Sent Events | Real-time server push |
| 📁 **Static Files** | Auto caching, large file chunking | Smart handling of small and large file transfers |
| 🌐 **CORS Support** | CORS configuration | Friendly for frontend-backend separation projects |

***

## Architecture Design 📐

### Component Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                     SAEA.MVC Architecture                    │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │                   SAEAMvcApplication                   │  │
│  │                     (Application Entry)                │  │
│  └────────────────────────┬─────────────────────────────┘  │
│                           │                                 │
│              ┌────────────┼────────────┐                   │
│              │            │            │                    │
│         ┌────▼────┐  ┌───▼────┐  ┌───▼────┐               │
│         │  Route  │  │ Filter │  │ Static │               │
│         │  Table  │  │ (AOP)  │  │ File   │               │
│         │(Routing)│  │(Filter)│  │(Static)│               │
│         └────┬────┘  └───┬────┘  └───┬────┘               │
│              │           │            │                     │
│              └───────────┼────────────┘                     │
│                          │                                   │
│                  ┌───────▼───────┐                          │
│                  │  Controller   │                          │
│                  │ (Controller)  │                          │
│                  └───────┬───────┘                          │
│                          │                                   │
│              ┌───────────┼───────────┐                      │
│              │           │           │                      │
│         ┌────▼───┐  ┌───▼────┐  ┌──▼───┐                  │
│         │Action  │  │ Http   │  │Result│                  │
│         │Result  │  │Context │  │Types │                  │
│         │(Result)│  │(Context)│ │(Types)│                  │
│         └────────┘  └────────┘  └──────┘                  │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │              SAEA.Sockets (Base Layer)               │  │
│  │             Async IO Communication Engine            │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### Request Processing Flow

```
HTTP Request Processing Flow:

Client Request ──► SAEA.Sockets Receive
                     │
                     ▼
           ┌─────────────────┐
           │   HttpParser    │ Parse HTTP Request
           │   Parse Headers/Body  │
           └─────────────────┘
                     │
                     ▼
           ┌─────────────────┐
           │   RouteTable    │ Route Matching
           │   Find Controller│
           └─────────────────┘
                     │
                     ▼
           ┌─────────────────┐
           │ ActionFilter    │ Execute Pre-Filters
           │ OnActionExecuting│
           └─────────────────┘
                     │
                     ▼
           ┌─────────────────┐
           │   Controller    │ Execute Action Method
           │   Action Execute│
           └─────────────────┘
                     │
                     ▼
           ┌─────────────────┐
           │ ActionResult    │ Generate Response Result
           │ ExecuteResult   │
           └─────────────────┘
                     │
                     ▼
           ┌─────────────────┐
           │ ActionFilter    │ Execute Post-Filters
           │ OnActionExecuted│
           └─────────────────┘
                     │
                     ▼
           ┌─────────────────┐
           │ OutputCache     │ Cache Processing
           │ (if enabled)    │
           └─────────────────┘
                     │
                     ▼
              HTTP Response Returned to Client


ActionResult Type Branches:

ActionResult ──┬── JsonActionResult ──► JSON Serialization Output
                │
                ├── ContentResult ────► Text/HTML Output
                │
                ├── FileResult ───────► File Download (Small Files)
                │
                ├── BigDataResult ────► Large File Streaming
                │
                ├── DataResult ───────► Binary Data Output
                │
                ├── EmptyResult ──────► Empty Response
                │
                └── SSEActionResult ───► Server-Sent Events Push
```

***

## Use Cases 💡

### ✅ Scenarios Suitable for SAEA.MVC

| Scenario | Description | Reason |
| --------------- | ----------------- | ---------- |
| 🔧 **API Services** | RESTful API, Microservice Backend | Lightweight and efficient, fast startup |
| 🎮 **Game Backend** | Game Server HTTP Interface | high-concurrency support via async IO |
| 📊 **Admin Dashboard** | Internal Management System | Simple self-hosted deployment |
| 🤖 **IoT Gateway** | Device Data Interface Service | Low resource usage |
| 📡 **Real-time Push** | SSE Server Push | Built-in SSE support |
| 🖥️ **Embedded Web** | Device Web Management Interface | Independent process execution |
| 📦 **Microservices** | Lightweight Microservice Nodes | No complex dependencies |

### ❌ Unsuitable Scenarios

| Scenario | Recommended Alternative |
| -------------- | --------------------- |
| Large Enterprise Applications | ASP.NET Core MVC |
| Razor Views Required | ASP.NET Core MVC |
| WebSocket Bidirectional Communication | SAEA.WebSocket |
| Complex Authentication & Authorization | ASP.NET Core Identity |

### Comparison with ASP.NET Core

| Feature | SAEA.MVC | ASP.NET Core |
| -------------- | -------- | ------------ |
| **Startup Speed** | Milliseconds | Seconds |
| **Memory Usage** | Low (lightweight self-host) | Higher |
| **Deployment Complexity** | Single File | Requires Runtime/Container |
| **Razor Views** | ❌ | ✅ |
| **Dependency Injection** | Manual | Built-in |
| **Async IO Performance** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ |
| **Cross-Platform** | All Platforms | All Platforms |

***

## Performance Comparison 📊

### Comparison with Other Web Frameworks

| Metric | SAEA.MVC | ASP.NET Core | NancyFX | HttpListener |
| ------------- | -------- | ------------ | -------- | ------------ |
| **Throughput** | High | High | Medium | Lower |
| **Average Latency** | Low | Low | Medium | Higher |
| **Memory Usage** | Low (lightweight self-host) | Medium | Medium | Low |
| **Startup Time** | Fast (no IIS/Kestrel) | Slower | Medium | Fast |
| **Concurrent Connections** | Built for high concurrency | Built for high concurrency | Medium | Medium |

> 📌 The table above is a qualitative comparison. SAEA has not published cross-framework benchmarks; actual performance depends on hardware and workload, so measure your own scenario.

### Output Cache Effect

| Scenario | No Cache | Cached (60s) | Effect |
| -------- | ----- | --------- | ------- |
| JSON API | Regenerated per request | Served from cache | Significantly lower CPU and latency |
| Static Page | Read/rendered per request | Served from cache | Significantly lower IO and latency |

### Async IO vs Traditional Model

| Model | Concurrency Performance | Memory Efficiency | CPU Utilization |
| ------------------- | ----- | ----- | ------- |
| **Async IO (SAEA.MVC)** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| Thread Pool | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ |
| Blocking | ⭐⭐ | ⭐⭐ | ⭐⭐ |

> 💡 **Tip**: Async IO avoids thread blocking and thread explosion under high concurrency. The underlying implementation is chosen by the .NET runtime per platform: IOCP on Windows, epoll on Linux, kqueue on macOS.

***

## FAQ ❓

### Q1: What is the difference between SAEA.MVC and ASP.NET Core MVC?

**A**: Main differences:

| Feature | SAEA.MVC | ASP.NET Core MVC |
| ---- | ----------- | ---------------- |
| Hosting Method | Self-hosted (Independent Process) | Kestrel/IIS |
| View Engine | No Razor | Razor/MVC Views |
| Dependency Injection | Manual | Built-in DI Container |
| Configuration Method | Code Configuration | appsettings.json |
| Use Case | Lightweight API, Embedded | Enterprise Applications |

SAEA.MVC is more suitable for lightweight, high-performance API service scenarios.

### Q2: How to enable output caching?

**A**: Use the `OutputCacheAttribute` attribute:

```csharp
[OutputCache(Duration = 60)]  // Cache for 60 seconds
public ActionResult GetList()
{
    return Json(GetDataFromDatabase());
}
```

### Q3: How to handle file uploads?

**A**: Access via `HttpContext.Request.PostFiles`:

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

### Q4: How to implement global exception handling?

**A**: Create a global exception filter:

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

// Apply global filter
[GlobalExceptionFilter]
public class BaseController : Controller { }
```

### Q5: How to configure CORS?

**A**: Set CORS headers in configuration:

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

### Q6: How to implement SSE server push?

**A**: Return `SSEActionResult`:

```csharp
public ActionResult StreamEvents()
{
    var stream = new SSEActionResult();
    
    // Send events periodically
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

// Client
// var source = new EventSource("/home/streamevents");
// source.onmessage = e => console.log(e.data);
```

### Q7: How to deploy to production?

**A**: Deployment steps:

1. **Publish the Application**

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

2. **Configuration File** (Optional)

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

3. **Register as Windows Service** (Optional)

```bash
sc create SAEAMvc binPath="path/to/your/app.exe" start=auto
```

***

## Core Classes 🔧

| Class Name | Description |
| -------------------------- | --------------------- |
| `SAEAMvcApplication` | MVC Application Main Class |
| `SAEAMvcApplicationConfig` | Application Configuration Class |
| `Controller` | MVC Controller Base Class |
| `ActionResult` | Action Result Abstract Base Class |
| `JsonActionResult` | JSON Serialization Result |
| `ContentResult` | Text/HTML Content Result |
| `FileResult` | File Download Result |
| `BigDataResult` | Large File Streaming Result |
| `SSEActionResult` | Server-Sent Events Result |
| `HttpGet` / `HttpPost` | HTTP Method Attributes |
| `ActionFilterAttribute` | AOP Filter Base Class |
| `OutputCacheAttribute` | Output Cache Attribute |
| `RouteTable` | Route Table |
| `WSController` | WebSocket Controller Base Class |
| `WebSocketAttribute` | Marks the WebSocket entry action |
| `WSMessage` | Received WebSocket message |
| `IWebSocketConnection` | Established WebSocket connection |
| `HttpContext` | HTTP Context |
| `HttpRequest` | HTTP Request Object |
| `HttpResponse` | HTTP Response Object |

***

## Usage Examples 📝

### Start MVC Application

```csharp
using SAEA.MVC;

// Read configuration
var config = SAEAMvcApplicationConfigBuilder.Read();

// Create MVC application
var app = new SAEAMvcApplication(config);

// Set default route
app.SetDefault("home", "index");
app.SetDefault("index.html");

// Set forbidden access paths
app.SetForbiddenAccessList("/content/");
app.SetForbiddenAccessList(".config");

// Start service
app.Start();

Console.WriteLine($"MVC service started, port: {config.Port}");
```

### Define Controller

```csharp
using SAEA.MVC;

[LogAttribute]  // Class-level filter
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
            return Json(new { Success = true, Message = "Login successful" });
        return Json(new { Success = false, Message = "Login failed" });
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
        return Content($"Time: {DateTime.Now}");
    }
}
```

### ActionResult Types

```csharp
// JSON result
return Json(new { Id = 1, Name = "Test" });

// Text/HTML content
return Content("<h1>Hello World</h1>");

// File download
return File(@"C:\files\document.pdf");

// Large file streaming
return BigData(@"C:\files\large_video.mp4");

// Binary data
return Data(new byte[] { 0x01, 0x02, 0x03 });

// Empty result (manipulate Response directly)
var response = HttpContext.Response;
response.ContentType = "text/html";
response.Write("<h3>Custom Response</h3>");
response.End();
return Empty();
```

### SSE Server Push

```csharp
using SAEA.MVC;

public ActionResult StreamEvents()
{
    var stream = GetEventStream();
    
    stream.Send("message", "Hello Client!");
    stream.Send("update", DateTime.Now.ToString());
    
    return stream;
}

// Client receives via EventSource
// var source = new EventSource("/home/streamevents");
// source.onmessage = (e) => console.log(e.data);
```

### AOP Filters

```csharp
using SAEA.MVC.Model;

// Custom filter
public class LogAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(HttpContext httpContext)
    {
        Console.WriteLine($"Request: {httpContext.Request.Url}");
    }

    public override void OnActionExecuted(HttpContext httpContext)
    {
        Console.WriteLine($"Response completed");
    }
}

// Apply filter
[LogAttribute]
public class UserController : Controller
{
    [HttpGet]
    [Log2Attribute]  // Method-level filter
    public ActionResult List()
    {
        return Json(GetUsers());
    }
}
```

### Configuration Options

```csharp
using SAEA.MVC;

var config = new SAEAMvcApplicationConfig
{
    Root = "wwwroot",           // Website root directory
    Port = 28080,               // Listening port
    IsStaticsCached = true,     // Static file caching
    IsZiped = false,            // GZIP compression
    BufferSize = 1024 * 64,     // Buffer size
    MaxConnects = 1000,         // Maximum connections
    DefaultPage = "index.html", // Default homepage
    IsDebug = false,            // Debug mode
    Timeout = 180,              // Interface timeout (seconds)
    ConnectTimeout = 6          // Connection timeout (seconds)
};

SAEAMvcApplicationConfigBuilder.Write(config);
```

### Async Action

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

### WebSocket Long-Lived Connections

WebSocket shares the same port and the same `/{controller}/{action}` routing, so no extra listener is needed: inherit from `WSController` and mark `OpenAsync` with `[WebSocket]`.

```csharp
using SAEA.MVC;
using System;
using System.Threading.Tasks;

public class ChatController : WSController
{
    [WebSocket]
    public override async Task OpenAsync()
    {
        // Same as HttpContext.Current.WebSockets.AcceptWebSocketAsync()
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
        // The connection is already closed; clean up here
        return Task.CompletedTask;
    }

    public override Task ErrorAsync(Exception exception)
    {
        return Task.CompletedTask;
    }
}
```

Client side:

```javascript
const ws = new WebSocket("ws://127.0.0.1:28080/chat/open");
ws.onmessage = e => console.log(e.data);
ws.send("hello");
```

Notes:

- The entry point is always `/{controller}/open` (the `Async` suffix of `OpenAsync` is ignored by routing). Addresses such as `/chat/message`, as well as non-GET requests such as `POST/PUT/DELETE /chat/open`, are rejected by the handshake checks with 400.
- `OpenAsync` must call `AcceptWebSocketAsync()` to accept the connection and return quickly: it writes the 101 response first, then switches the connection to raw byte mode.
- `MessageAsync` only receives complete Text/Binary messages (fragments are reassembled); Ping frames are answered with Pong automatically.
- Each connection has its own context. `WebSockets.Current` returns the current `IWebSocketConnection`, and `ws.Id` can be used as a connection identifier.
- Plain actions, static files, SSE and WebSocket share the same port without interfering with each other.
- Close handshake: when the server initiates a close, it writes the Close frame, waits for the pending send to complete, then calls `Shutdown(Send)` (FIN) before releasing the session. Clients therefore receive a normal Close frame instead of an RST (10054).

Known limitations:

- No broadcast helper is provided. Multi-connection broadcast requires maintaining your own `IWebSocketConnection` collection (indexed by `ws.Id`).
- The server does not send Ping proactively; implement keep-alive at the application layer if needed.
- Handshake failures (non-upgrade request, missing `Sec-WebSocket-Key`, etc.) return 400/426 directly and bypass MVC's `OnException` pipeline.
- Upgraded connections are exempt from HTTP session timeout reclamation; normal disconnects are finalized through `OnDisconnected`. When the underlying token pool is full (`SessionManager.Free` returns `false`), no disconnect notification is raised; such connections stay in the internal registry until the 5-minute connection-table sweep reclaims entries that are `Closed` or whose underlying socket is gone.
- `Request` and `Session` are snapshots taken at handshake time: WS messages do not refresh the sliding expiration of `Session`. Keep long-lived session state in connection-scoped fields or a dictionary.
- Controller instances are shared per route, so **never store connection state in Controller fields**. Use `WebSockets.Current` or your own dictionary keyed by connection id.
- Every long-lived connection permanently occupies one `MaxConnects` slot; increase the configured limit according to your concurrency.
- `OpenAsync` must return quickly after accepting the connection: `MvcInvoker` waits synchronously on that Task for `WebConfig.Timeout` (180 seconds by default). On timeout an already-upgraded connection does not get an HTTP 408 (that would corrupt the WebSocket stream); instead `ErrorAsync` is invoked and the connection is closed with `ServerShutdown`. A receive loop inside `OpenAsync` will hit this path. Put long-running logic in `MessageAsync`.
- The idle-reclamation interval of `HttpSocketDebug` (`IsDebug = true`) is fixed at 180 seconds and is unrelated to the `Timeout` setting. WebSocket connections are exempt from it, but do not infer cleanup timing for other long-lived connections from `Timeout` in debug mode.
- The `permessage-deflate` extension is not supported and `wss://` is not provided; terminate TLS at a reverse proxy if needed.
- Each connection has a pending queue limit of 1024 frames / 4MB (including a single message). Exceeding it closes the connection with `ServerShutdown`; split large payloads yourself.

***

## Dependencies

| Package | Version | Description |
| ------------ | -------- | --------- |
| SAEA.Sockets | 26.9.21.1 | Socket Communication Framework |
| SAEA.Http | 26.9.21.1 | HTTP Server |
| SAEA.Common | 26.9.21.1 | Common Utility Classes |
| SAEA.WebSocket | 26.10.8.1 | WebSocket codec and protocol |

***

## Related Projects

- [SAEA.Rested](https://github.com/yswenli/SAEA.Rested) - REST API example project based on SAEA.MVC

***

## More Resources

- [GitHub Repository](https://github.com/yswenli/SAEA)
- [NuGet Package](https://www.nuget.org/packages/SAEA.MVC)
- [Author's Blog](https://www.cnblogs.com/yswenli/)

***

## License

Apache License 2.0