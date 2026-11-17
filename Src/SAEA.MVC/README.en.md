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
| [🛣️ Routing](#routing) | Two-segment convention and attribute routing |
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

## Routing 🛣️

By default an address is resolved from the last two segments of the URL: `/{any-prefix}/{Controller}/{Action}`, where the controller name is the class name plus `Controller` and the comparison ignores case, so both `/home/index` and `/api/v1/home/index` reach `HomeController.Index`. That is only the default convention, not a limit: an action that declares an attribute route is matched by its template first, and the template can replace the two-segment address entirely.

### Attribute routing

```csharp
using SAEA.MVC;

[Route("api/v1/users")]              // class-level prefix
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
    [Route("api/v1/orders/{id}")]    // no class-level [Route]: the method template is the absolute path; [Route] accepts any verb
    public ActionResult Get(string id)
    {
        return Json(new { Id = id });
    }
}

public class HealthController : Controller
{
    [Route("health")]                // a single-segment path works too
    public ActionResult Ping()
    {
        return Content("ok");
    }
}
```

- `[Route(template)]` declares a path only and accepts GET/POST/PUT/DELETE.
- `[HttpGet(template)]` / `[HttpPost(template)]` declare a path and restrict the verb.
- A class-level `[Route]` is joined with the method template by `/`; when the method only carries `[HttpGet]` / `[HttpPost]` without a template, the class template alone is the path.
- An action whose class carries `[Route]` but which has no routing attribute of its own stays on two-segment routing, so a class prefix never swallows every action.

### Template semantics

| Rule | Description |
| --- | --- |
| Static segments | `api/v1/users`, compared segment by segment, case-insensitive |
| Path parameters | `{id}`, required and exactly one segment; the name allows only letters, digits and underscores |
| Placeholder placement | must occupy a whole segment; `user-{id}` is invalid |
| Leading/trailing `/` | ignored; repeated `//` collapses into one |
| Trailing slash | `/users/5/` is equivalent to `/users/5` |
| Query string | not part of matching; `/users/5?x=1` matches `/users/5` |

Matching order:

1. Static templates (no parameters) are looked up directly by normalized path.
2. Parameter templates are matched inside their segment-count bucket, templates with more static segments first.
3. Two-segment convention.
4. Static files.
5. 404.

Attribute routes are checked before static files, so declaring the same path as a static file shadows that file; pick a more specific template or a different prefix.

Matched path parameters are written to `Request.Parmas` and override same-named query and form values; action parameters are bound by name, and numeric and other basic types are converted automatically, exactly like two-segment binding.

### HTTP methods

- `[Route]` does not restrict the verb; `[HttpGet]` / `[HttpPost]` restrict it.
- When an attribute-route path matches but the verb does not, the response is **405 Method Not Allowed** with an `Allow` header listing the verbs declared for that path, and it does not fall back to two-segment routing.
- A path with no attribute-route entry at all is not a conflict and continues to two-segment routing.
- A two-segment action is judged the same way: with no `[HttpGet]` / `[HttpPost]` on the action, no verb is restricted and GET/POST/PUT/DELETE all run as before; with one or both declared, the allowed set is their union (both declared means GET and POST), compared with the request method case-insensitively, and a mismatch returns **405 Method Not Allowed** with `Allow` without running the action, the filters or the WebSocket entry. Both the `WebConfig.DefaultRoute` default route and the `/{Controller}/{Action}` branch are covered.
- An `OPTIONS` request returns an empty response before routing and is not subject to this check.

### Startup validation

The attribute route table is built and validated in the `SAEAMvcApplication` constructor, so problems fail startup:

- An invalid template (`{id?}`, `{*path}`, `{id:int}`, an invalid placeholder name) throws `ArgumentException`.
- An action declaring more than one templated routing attribute (for example `[Route("x")]` together with `[HttpGet("y")]`) throws `ArgumentException`.
- The same path shape plus the same verb declared by two actions throws `InvalidOperationException`; `{id}` and `{uid}` count as the same shape, while the same path with different verbs is legal.

### Not supported

- Optional parameters `{id?}`, catch-all `{*path}`, type constraints `{id:int}` / `{id:guid}`.
- The `~/` absolute-prefix syntax.
- Multiple paths per action: an action has exactly one template source.

### Address map

| Request | Match | Notes |
| --- | --- | --- |
| `GET /api/v1/users` | `UserController.List` | class prefix plus a parameterless method |
| `GET /api/v1/users/5` | `UserController.Get`, `id = "5"` | path parameter bound to the same-named parameter |
| `GET /API/V1/Users/5/` | same as above | case and trailing slash ignored |
| `GET /api/v1/users/5?x=1` | same as above | query string is not matched |
| `POST /api/v1/users` | `UserController.Create` | same path, different verb |
| `DELETE /api/v1/users` | 405 with `Allow: GET, POST` | path matched but verb differs |
| `GET /api/v1/orders/9` | `OrderController.Get` | method template is the absolute path |
| `POST /api/v1/orders/9` | same as above | `[Route]` accepts any verb |
| `GET /health` | `HealthController.Ping` | single-segment attribute route |
| `GET /home/index` | `HomeController.Index` | no attribute route, two-segment routing |
| `POST /home/index` | 405 with `Allow: GET` | two-segment action that only carries `[HttpGet]`, blocked as well |

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

### Measured on one machine (reference)

The table below is one snapshot from a loopback load test. The site under test is a SAEA.MVC application assembled on top of SAEA.Http; SAEA.Http as a standalone server has not been benchmarked separately.

| Scenario | Result |
|---|---:|
| HTTP serial (5,000 requests, short connections) | 943 RPS |
| HTTP 16 concurrent (10 s) | 4,140 RPS |
| HTTP 64 concurrent (10 s) | 3,529 RPS |
| HTTPS serial (2,000 requests, short connections) | 220 RPS |
| Static 100 KB serial (memory cache hit) | 529 RPS / 51.7 MiB/s |
| WebSocket single-connection echo | 7,078 msg/s |

How to read these numbers:

- Client and server ran on the same machine in the same process over IPv4 loopback; under concurrency the load generator is itself a bottleneck (in this run 64-concurrent RPS is 0.85x the 16-concurrent figure), so it does not indicate the server's scaling limit.
- Ordinary responses are fixed to `Connection: close`, so every request pays a TCP connect and teardown and HTTPS adds a TLS handshake; RPS is dominated by connection setup rather than business processing.
- Self-signed certificate with TLS 1.2 fixed and certificate validation skipped; static files are served from the in-memory cache, so this is not the first-read or large-file chunked path.
- Single-run snapshot: the same probe varies 20%–35% between runs (another run on this machine measured 749 RPS HTTP serial, 183 RPS HTTPS serial, 515 RPS static 100 KB serial and 5,782 msg/s WebSocket single connection). Use it only for relative comparison within one run, never as a production absolute value or for cross-framework comparison.

Full data, failure rates and resource usage: [MVC / TLS acceptance record](../../docs/superpowers/plans/2026-11-13-mvc-tls-acceptance.md).

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

The cache key is the request verb plus the actual hit path plus the normalized query: `GET:/home/list` without a query, `GET:/home/list?a=1&b=2` with one. Query keys are sorted with `Ordinal` and joined by `&`, so `?a=1&b=2` and `?b=2&a=1` share one cache entry; the same path with different verbs and the same path with different path parameters are cached separately. The form body (`Request.Forms`) is not part of the key.

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

**A**: Subscribe to `SAEAMvcApplication.OnException`: an exception thrown by an action is handed to it together with the current context, and the returned value becomes the response.

```csharp
using SAEA.Common.Serialization;
using SAEA.MVC;

var app = new SAEAMvcApplication(config);

app.OnException += (httpContext, ex) =>
{
    var json = SerializeHelper.Serialize(new { Error = ex.Message });
    return new ContentResult(json, System.Net.HttpStatusCode.InternalServerError);
};
```

`ActionFilterAttribute` is not an exception channel: `OnActionExecuted` runs only after an action returns normally and never sees a thrown exception; filters are for cross-cutting work such as logging, authorization and rewriting results.

### Q5: How to configure CORS?

**A**: Use a filter to add the CORS headers to the response:

```csharp
public class CorsFilter : ActionFilterAttribute
{
    public override ActionResult OnActionExecuting()
    {
        var headers = HttpContext.Current.Response.Headers;
        headers["Access-Control-Allow-Origin"] = "*";
        headers["Access-Control-Allow-Methods"] = "GET,POST,PUT,DELETE";
        headers["Access-Control-Allow-Headers"] = "Content-Type";

        // Returning EmptyResult lets the action run
        return EmptyResult.Default;
    }

    public override void OnActionExecuted(ref ActionResult result)
    {
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
| `RouteAttribute` | Route template attribute |
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
        var filePath = HttpContext.Server.MapPath("/files/document.pdf");

        // null means the path escapes the site root or is invalid; respond with 404
        if (filePath == null)
        {
            return new ContentResult("o_o, no content found", System.Net.HttpStatusCode.NotFound);
        }

        return File(filePath);
    }

    [OutputCache(Duration = 60)]
    public ActionResult Cached()
    {
        return Content($"Time: {DateTime.Now}");
    }
}
```

`Server.MapPath` returns `null` for paths that escape the site root or are invalid; callers must check for null before using the result as a file path.

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
using SAEA.MVC;

// Custom filter
public class LogAttribute : ActionFilterAttribute
{
    public override ActionResult OnActionExecuting()
    {
        Console.WriteLine($"Request: {HttpContext.Current.Request.Url}");

        // Returning EmptyResult lets the action run; any other result becomes the response
        return EmptyResult.Default;
    }

    public override void OnActionExecuted(ref ActionResult result)
    {
        Console.WriteLine("Response completed");
    }
}

// Apply the filter
[LogAttribute]                       // class-level filter: applies to every action of this controller
public class UserController : Controller
{
    [HttpGet]
    [OutputCache(Duration = 60)]     // method-level filter: applies to List only
    public ActionResult List()
    {
        return Json(new { Total = 2 });
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

### 🧩 Handler Chain Wiring

MVC already installs the HTTP/WebSocket protocol nodes and the tail exception terminal; `ConfigurePipeline` exposes only the slot between them and runs once per connection:

```csharp
using System;
using System.Threading.Tasks;
using SAEA.MVC;
using SAEA.Sockets.Pipeline;

public sealed class MyHandler : InboundHandlerAdapter
{
    public override Task ChannelActiveAsync(IChannelHandlerContext ctx)
    {
        Console.WriteLine($"[{ctx.ChannelId}] connected");
        return Task.CompletedTask;
    }

    public override Task ChannelInactiveAsync(IChannelHandlerContext ctx)
    {
        Console.WriteLine($"[{ctx.ChannelId}] disconnected");
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

Assign it before `app.Start()`; assigning after the service starts throws `InvalidOperationException`.

Plain HTTP requests are handed to `RequestDelegate` (or MVC routing) by the dispatch node and never reach handlers added in `ConfigurePipeline`, so a custom inbound handler is meant for connection-level observation (online counters, connection logs) or for raw frames received after a WebSocket upgrade.

### 🔐 TLS / HTTPS / WSS

`app.UseSsl(...)` forwards to `WebConfig.UseSsl` and serves both HTTPS and WSS on the same listening port:

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

Notes:

- Call it before `app.Start()`; calling it after the service starts throws `InvalidOperationException`.
- Without it the server stays on plain HTTP/WS exactly as before; a second call replaces the first.
- One port serves both HTTPS and WSS: the WebSocket upgrade reuses the same encrypted connection, so clients simply use `https://` and `wss://`.
- Load certificates with `SaeaCertificateLoader.FromPfx` / `FromStore` / `FromPem`; use `ServerCertificateSelector` for SNI (multiple host names) and `ClientCertificates` plus `ClientCertificateRequired` for mutual TLS.

### 📏 Protocol Limits

| Limit | Default | On Exceed |
|-------|---------|-----------|
| Request line + headers | 64KB | Returns 431 Request Header Fields Too Large |
| Request body | 64MB | Returns 413 Request Entity Too Large |
| `Transfer-Encoding: chunked` | Not supported | Returns 400 Bad Request; chunked decoding is not implemented |

These limits come from the default `HttpRequestSplitter` constructor; `WebConfig` does not expose them yet.

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

WebSocket shares the same port and the same routing, so no extra listener is needed: inherit from `WSController` and mark `OpenAsync` with `[WebSocket]`. The entry point defaults to `/{controller}/open`, and `[Route]` / `[HttpGet]` on `OpenAsync` can declare any path you want.

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

- The entry point defaults to `/{controller}/open` (the `Async` suffix of `OpenAsync` is ignored by routing), and attribute routing applies here too: add `[Route("ws/chat")]` to `OpenAsync` and clients connect to `ws://host/ws/chat`. Addresses that do not lead to the entry, such as `/chat/message`, are rejected with 400; a non-GET request such as `POST/PUT/DELETE` is also rejected with 400 even when routing reaches the entry, because the handshake only accepts GET (an address that matches no action at all still returns 404).
- `OpenAsync` must call `AcceptWebSocketAsync()` to accept the connection and return quickly: it writes the 101 response first, then switches the connection to raw byte mode.
- `MessageAsync` only receives complete Text/Binary messages (fragments are reassembled); Ping frames are answered with Pong automatically.
- Each connection has its own context. `WebSockets.Current` returns the current `IWebSocketConnection`, and `ws.Id` can be used as a connection identifier.
- Plain actions, static files, SSE and WebSocket share the same port without interfering with each other.
- Close handshake: when the server initiates a close, it writes the Close frame, waits for the pending send to complete, then calls `Shutdown(Send)` (FIN) before releasing the session. Clients therefore receive a normal Close frame instead of an RST (10054).

Known limitations:

- No broadcast helper is provided. Multi-connection broadcast requires maintaining your own `IWebSocketConnection` collection (indexed by `ws.Id`).
- The server does not send Ping proactively; implement keep-alive at the application layer if needed.
- Handshake failures (non-upgrade request, missing `Sec-WebSocket-Key`, etc.) return 400/426 directly and bypass MVC's `OnException` pipeline.
- Upgraded connections are exempt from HTTP session timeout reclamation; both graceful and abnormal disconnects are reported through `WebConfig.ConnectionClosed` to `WebSocketsFeature.OnDisconnected`. If the notification arrives before the connection is published, `Register` performs an extra liveness check, and a 5-minute sweep reclaims leftover entries that are `Closed` or whose underlying connection is already gone.
- `Request` and `Session` are snapshots taken at handshake time: WS messages do not refresh the sliding expiration of `Session`. Keep long-lived session state in connection-scoped fields or a dictionary.
- Controller instances are shared per route, so **never store connection state in Controller fields**. Use `WebSockets.Current` or your own dictionary keyed by connection id.
- Every long-lived connection permanently occupies one `MaxConnects` slot; increase the configured limit according to your concurrency.
- `OpenAsync` must return quickly after accepting the connection: `MvcInvoker` waits synchronously on that Task for `WebConfig.Timeout` (180 seconds by default). On timeout an already-upgraded connection does not get an HTTP 408 (that would corrupt the WebSocket stream); instead `ErrorAsync` is invoked and the connection is closed with `ServerShutdown`. A receive loop inside `OpenAsync` will hit this path. Put long-running logic in `MessageAsync`.
- `IsDebug = true` only adds request debug logging; it does not change idle reclamation. Idle reclamation for long-lived connections follows `WebConfig.Timeout` (180 seconds by default), and upgraded WebSocket connections are exempt.
- `UseSsl` enables HTTPS and WSS; the `permessage-deflate` extension is still not supported, and terminating TLS at a reverse proxy remains a valid option.
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