# SAEA.Http - Lightweight High-Performance HTTP Server 🌐

[![NuGet version](https://img.shields.io/nuget/v/SAEA.Http.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.Http)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**English** | **[中文版](README.md)**

> A high-performance HTTP server for .NET Standard 2.0 with asynchronous IO, RESTful APIs, static file serving and file upload.
>
> ⚡ **Streaming core**: both receive and send run on a `SAEA.Sockets.Pipeline` handler chain — framing, protocol parsing, dispatching and response encoding are separate chain nodes.

## Navigation 🧭

| Section | Content |
|---------|---------|
| [⚡ Quick Start](#quick-start) | Smallest working example |
| [🎯 Features](#features) | What the package offers |
| [🔗 Pipeline Assembly](#pipeline-assembly) | Custom handlers and ordering rules |
| [📐 Architecture](#architecture) | Components and request flow |
| [💡 Scenarios](#scenarios) | When SAEA.Http fits |
| [📊 Performance](#performance) | Comparison with alternatives |
| [❓ FAQ](#faq) | Common questions |
| [🔧 Core Types](#core-types) | Main classes |
| [📝 Examples](#examples) | Detailed samples |

---

## Quick Start ⚡

Three steps to a running HTTP server:

### Step 1: Install the NuGet package

```bash
dotnet add package SAEA.Http
```

### Step 2: Create the server (5 lines)

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

### Step 3: Test it

```bash
curl http://localhost:28080/api/test
# output: {"msg": "Hello SAEA.Http!"}
```

**That's it!** 🎉 You now have a high-throughput HTTP server.

---

## Features 🎯

| Feature | Description | Benefit |
|---------|-------------|---------|
| 🚀 **Cross-platform async IO** | Built on SAEA.Sockets async IO | Low latency under high concurrency |
| 📡 **Standard HTTP methods** | GET/POST/PUT/DELETE/OPTIONS | Full RESTful support |
| 📁 **Static files** | Automatic caching, chunked transfer | Efficient large-file delivery |
| 📤 **File upload** | multipart/form-data parsing | Uploads without manual parsing |
| 🔒 **Session management** | HttpSession + Manager | State tracking with idle expiry |
| 🍪 **Cookies** | HttpCookie/HttpCookies | Full read/write support |
| 🗜️ **GZIP compression** | Automatic response compression | Less bandwidth |
| 🌍 **CORS** | Built-in cross-origin support | Front-end friendly |
| 📡 **SSE** | Server-Sent Events | Server push out of the box |
| 🔗 **Handler chain** | `WebConfig.ConfigurePipeline` | Append custom inbound handlers |

---

## Pipeline Assembly 🔗

The package wires its own transport and protocol nodes, then hands the not-yet-started chain to `WebConfig.ConfigurePipeline` once per connection:

```csharp
using SAEA.Http;

var server = new WebHost(port: 28080);

server.WebConfig.ConfigurePipeline = (channelId, pipeline) =>
{
    // You may only append handlers here; the chain freezes after Start.
    pipeline.AddLast(new MyInboundHandler());
};

server.Start();
```

Ordering is a hard constraint:

- The chain starts with the outbound prefix: `HttpResponseEncoder` leftmost, `OutboundSerializingHandler` rightmost. Custom outbound handlers must keep that prefix contiguous.
- Protocol nodes (`HttpRequestDecoder` → `HttpProtocolHandler` → dispatch → raw-frame sink) always sit before your handlers.
- Handlers added in `ConfigurePipeline` run after the protocol nodes and before the tail `ExceptionTerminalHandler`.
- Assign `ConfigurePipeline` before `WebHost.Start()`; assigning it after the server starts throws `InvalidOperationException`.
- Business writes go through `pipeline.WriteAsync(...)` (or `ctx.Pipeline.WriteAsync(...)` inside handlers); `ctx.WriteAsync(...)` only forwards to the next outbound node inside an outbound callback.

The three business entry points live on `WebConfig`; there are no receive or lifecycle events to subscribe to:

| Member | Description |
|--------|-------------|
| `WebConfig.RequestDelegate` | Request entry, `void (IHttpContext)`; replaces default routing when set |
| `WebConfig.ExceptionHandler` | Exception entry, `IHttpResult (IHttpContext, Exception)`; return null for the default error response |
| `WebConfig.ConnectionClosed` | Connection closed notification, `void (string channelId, Exception ex)`; `ex` is null on graceful close |

### Protocol limits

| Limit | Default | Notes |
|-------|---------|-------|
| Request body | 64MB | `HttpRequestSplitter` `maxRequestSize`; body only |
| Request header | 64KB | Separate limit; exceeding it returns 431 |
| Framing buffer | `MaxFrameLength + 9 + ReadBufferSize` | Derived by the executor; do not set a smaller value |
| Concurrent messages | 1 | HTTP is half-duplex; one request at a time per connection |
| `Transfer-Encoding: chunked` | Not supported | Returns 400 instead of misreading chunk bytes |

---

## Architecture 📐

### Components

```
┌──────────────────────────────────────────────────────────────┐
│                       SAEA.Http                              │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌────────────────────────────────────────────────────────┐  │
│  │                      WebHost                           │  │
│  │                   (server entry point)                 │  │
│  └────────────────────────────────────────────────────────┘  │
│                            │                                 │
│         ┌──────────────────┼──────────────────┐              │
│         │                  │                  │              │
│  ┌──────▼──────┐   ┌───────▼───────┐   ┌─────▼─────┐         │
│  │ HttpContext │   │  HttpSession  │   │ WebConfig │         │
│  └──────┬──────┘   └───────────────┘   └───────────┘         │
│         │                                                    │
│  ┌──────┴──────┐                                             │
│  │             │                                             │
│  ▼             ▼                                             │
│ ┌─────────┐ ┌──────────┐                                     │
│ │Request  │ │Response  │                                     │
│ └────┬────┘ └────┬─────┘                                     │
│      └─────┬─────┘                                           │
│  ┌─────────▼──────────────────────────┐                      │
│  │            HttpSocket              │                      │
│  │   (chain assembly and shutdown)    │                      │
│  └─────────┬──────────────────────────┘                      │
│  ┌─────────▼──────────────────────────┐                      │
│  │      SAEA.Sockets.Pipeline         │                      │
│  │ framing/decoding/dispatch/encoding │                      │
│  └─────────┬──────────────────────────┘                      │
│  ┌─────────▼──────────────────────────┐                      │
│  │           SAEA.Sockets             │                      │
│  └────────────────────────────────────┘                      │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

### Request flow

```
Client ──► HTTP Request ──► pump (PipeReader + splitter)
                                    │
                                    ▼
                          ┌───────────────────┐
                          │ HttpRequestDecoder│
                          └───────────────────┘
                                    │
                                    ▼
                          ┌────────────────────┐
                          │ HttpProtocolHandler│
                          │ upgrade / 413 / 431│
                          └────────────────────┘
                                    │
                                    ▼
                          ┌─────────────────────┐
                          │ HttpDispatchHandler │
                          └─────────────────────┘
                                    │
                    ┌───────────────┼───────────────┐
                    ▼               ▼               ▼
            ┌───────────┐   ┌───────────┐   ┌───────────┐
            │static?    │   │API?       │   │upload?    │
            └─────┬─────┘   └─────┬─────┘   └─────┬─────┘
                  ▼               ▼               ▼
            ┌───────────┐   ┌───────────┐   ┌───────────┐
            │StaticCache│   │Request    │   │Multipart  │
            │           │   │Delegate   │   │           │
            └─────┬─────┘   └─────┬─────┘   └─────┬─────┘
                  └───────────────┼───────────────┘
                                  ▼
                        ┌───────────────────┐
                        │  HttpResponse     │
                        └───────────────────┘
                                  ▼
                        ┌───────────────────┐
                        │HttpResponseEncoder│
                        └───────────────────┘
                                  ▼
                            transport write
                                  ▼
                             client response
```

---

## Scenarios 💡

### ✅ Good fit

| Scenario | Why |
|----------|-----|
| 🌐 **RESTful APIs** | Async IO handles high concurrency with low latency |
| 📁 **Static file serving** | Built-in cache and chunked transfer |
| 📤 **Upload services** | multipart parsing with bounded memory |
| 🔄 **Proxies / gateways** | Lightweight forwarding |
| 📡 **SSE push** | Server-Sent Events supported |
| 🎮 **Game backends** | High throughput, low footprint |
| 📊 **Embedded services** | Small and easy to host |

### ❌ Better served elsewhere

| Scenario | Alternative |
|----------|-------------|
| Full MVC applications | SAEA.MVC (built on SAEA.Http) |
| WebSocket bidirectional traffic | SAEA.WebSocket |
| RPC microservices | SAEA.RPC |
| Full web framework | ASP.NET Core |

---

## Performance 📊

### Server comparison

| Metric | SAEA.Http | Kestrel | HttpListener |
|--------|-----------|---------|--------------|
| **Concurrency** | High | High | Medium |
| **Memory** | Low | Higher | Medium |
| **Startup** | Fast | Slower | Medium |
| **Static files** | Built-in cache | Middleware | None |
| **Dependencies** | Minimal | Many | OS components |

### Feature comparison

| Feature | SAEA.Http | Kestrel | HttpListener |
|---------|:---------:|:-------:|:------------:|
| Async IO | ✅ | ✅ | ✅ |
| Static file cache | ✅ | Middleware | ❌ |
| GZIP | ✅ | Middleware | ❌ |
| Session | ✅ | Middleware | ❌ |
| Upload parsing | ✅ | Manual | Manual |
| SSE | ✅ | ✅ | ❌ |
| Cross-platform | ✅ | ✅ | Windows only |

> 💡 SAEA.Http targets lightweight, high-throughput services — embedded, game backends and IoT where resource usage matters.

---

## FAQ ❓

### Q1: How is SAEA.Http different from ASP.NET Core?

**A**: SAEA.Http is a small HTTP server component:
- Lower memory footprint and faster startup
- Built-in static cache, session and upload parsing
- Suitable for embedded, game backend and microservice scenarios
- ASP.NET Core is the better choice for large enterprise applications

### Q2: How do I handle CORS?

**A**: SAEA.Http has built-in CORS support; handle it in `RequestDelegate`:

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
    // ... other handling
};
```

Call `server.SetCrossDomainHeaders("token", "auth")` to allow extra custom headers.

### Q3: How do I upload large files?

**A**: The body is fully read according to `Content-Length` before dispatch; anything above 64MB returns 413:

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

### Q4: How do I enable GZIP?

**A**: Through the `WebHost` constructor (enabled by default):

```csharp
var server = new WebHost(port: 28080, isZiped: true);
```

### Q5: How do I change the session timeout?

**A**: Use `HttpSessionManager`:

```csharp
// Default session timeout is 20 minutes
var session = ctx.Session;  // get or create
session["key"] = "value";
```

### Q6: How do I implement SSE?

**A**: Use `text/event-stream`; `SendHeader(-1)` means unknown length and keeps the connection alive:

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

### Q7: How does static file caching work?

**A**: SAEA.Http handles it automatically:
- Small files (under 1MB by default) are cached in memory
- `Cache-Control` and `Last-Modified` are supported
- `If-Modified-Since` conditional requests are supported
- Toggle with the `isStaticsCached` constructor argument

### Q8: Is chunked request body supported?

**A**: No. A request with `Transfer-Encoding: chunked` gets a 400 and the connection is closed, because framing by `Content-Length` would misinterpret chunk bytes as the next request.

---

## Core Types 🔧

| Type | Description |
|------|-------------|
| `WebHost` | Server entry point |
| `HttpRequest` | Request wrapper (method, URL, headers, forms …) |
| `HttpResponse` | Response wrapper (status, headers, body …) |
| `HttpContext` | Request context (Request/Response/Session) |
| `HttpSession` / `HttpSessionManager` | Session state and management |
| `HttpCookie` / `HttpCookies` | Cookie handling |
| `WebConfig` | Configuration: request/exception/close delegates and chain callback |
| `RequestDataReader` | HTTP request parser |
| `IHttpUpgradeTarget` | Protocol upgrade negotiation |
| `IChannelPipeline` | Handler chain and the single business write entry `WriteAsync` |

### Result types

| Type | Description |
|------|-------------|
| `HttpContentResult` | Text/JSON result |
| `HttpFileResult` | File result |
| `HttpBigDataResult` | Streamed large payload |
| `HttpEmptyResult` | Empty result |
| `HttpActionResult` | Action result |

---

## Examples 📝

### Start a server

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
    Console.WriteLine($"error: {ex.Message}");
    return null;
};

server.Start();
Console.WriteLine("HTTP server started on port 28080");
```

### Handle GET

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

### Handle POST

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
            Console.WriteLine($"file: {file.FileName}, size: {file.Data.Length}");
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

### Use sessions

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

### Use cookies

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

    response.Write("cookie set");
    response.End();
};
```

### Return files

```csharp
using SAEA.Http.Model;

server.WebConfig.RequestDelegate = ctx =>
{
    var filePath = ctx.Server.MapPath("/files/document.pdf");
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
            // SendData hands the bytes off immediately; a single buffer can be reused.
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

### SSE push

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

### Custom handler

Handlers appended in `ConfigurePipeline` sit after the protocol nodes and before the tail exception terminal. Plain HTTP requests are handed to `WebConfig.RequestDelegate` by the dispatch node and are not propagated further, so an inbound handler here is meant for connection-level observation or for raw frames received after a protocol upgrade:

```csharp
using SAEA.Sockets.Pipeline;

public sealed class ConnectionObserver : InboundHandlerAdapter
{
    /// <summary>Lifecycle notifications are dispatched to every handler in chain order; no forwarding needed.</summary>
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

    /// <summary>Only messages not consumed by the protocol layer arrive here, e.g. raw frames after an upgrade.</summary>
    public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
    {
        var frame = msg as PooledMessage;
        if (frame != null)
            Console.WriteLine($"[{ctx.ChannelId}] {frame.Frame.Length} bytes");

        return ctx.FireChannelReadAsync(msg);
    }
}

server.WebConfig.ConfigurePipeline = (channelId, pipeline) =>
{
    pipeline.AddLast(new ConnectionObserver());
};
```

Raw frames after an upgrade live in pooled memory: `Frame` is only valid inside the callback, so copy the bytes before keeping them or handing them to asynchronous code.

---

## Configuration

`WebConfig` port, root and buffer sizes are initialized by the `WebHost` constructor; business code sets the delegates:

```csharp
var server = new WebHost(
    root: "wwwroot",            // site root
    port: 28080,                // listen port
    isStaticsCached: true,      // static cache
    isZiped: false,             // GZIP
    bufferSize: 1024 * 64,      // receive buffer (64KB)
    maxConnects: 1000,          // max connections
    timeout: 180,               // idle timeout (seconds)
    connectTimeout: 3);         // connect timeout (seconds)

server.WebConfig.ConfigurePipeline = (id, pipeline) => { /* ... */ };
server.WebConfig.RequestDelegate = ctx => { /* ... */ };
server.WebConfig.ExceptionHandler = (ctx, ex) => null;
server.WebConfig.ConnectionClosed = (id, ex) => { /* ... */ };
```

---

## Dependencies

| Package | Description |
|---------|-------------|
| SAEA.Sockets | Socket transport |
| SAEA.Sockets.Pipeline | Handler chain, framing and codecs |
| SAEA.Common | Shared utilities |

---

## Resources

- [GitHub repository](https://github.com/yswenli/SAEA)
- [NuGet package](https://www.nuget.org/packages/SAEA.Http)
- [Author blog](https://www.cnblogs.com/yswenli/)

---

## License

Apache License 2.0