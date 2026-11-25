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
| 📡 **Standard HTTP methods** | GET/POST/PUT/DELETE/PATCH/HEAD/OPTIONS (HEAD returns headers only) | Full RESTful support |
| 📁 **Static files** | Automatic caching, chunked transfer | Efficient large-file delivery |
| 📤 **File upload** | multipart/form-data parsing | Uploads without manual parsing |
| 🔒 **Session management** | HttpSession + Manager | State tracking with idle expiry |
| 🍪 **Cookies** | HttpCookie/HttpCookies | Full read/write support |
| 🗜️ **GZIP compression** | Automatic response compression | Less bandwidth |
| 🌍 **CORS** | Built-in cross-origin support | Front-end friendly |
| 📡 **SSE** | Server-Sent Events | Server push out of the box |
| 🔗 **Handler chain** | `WebConfig.ConfigurePipeline` | Append custom inbound handlers |
| 🔐 **TLS / HTTPS** | `WebConfig.UseSsl` | HTTPS and WSS on one port; certificates from file, store or PEM |

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
| Request body | 20MB | `WebConfig.MaxRequestBodySize`; body only |
| Body spool threshold | 1MB | `WebConfig.LargeBodyThreshold`; above it the body is written to a temp file, `0` disables spooling |
| Temp directory | `saea-http` under the system temp directory | `WebConfig.TempDirectory`; ensured to exist at startup |
| 413 drain timeout | 2000ms | `WebConfig.RequestDrainTimeout`; how long to keep reading and discarding the remaining body after a 413; `0` disables draining |
| Request header | 64KB | Separate limit; exceeding it returns 431 |
| Framing buffer | `MaxFrameLength + 9 + ReadBufferSize` | Derived by the executor; do not set a smaller value |
| Concurrent messages | 1 | HTTP is half-duplex; one request at a time per connection |
| `Transfer-Encoding: chunked` | Supported | Decoded chunk by chunk; the decoded length is capped by `MaxRequestBodySize` and the raw chunk bytes count toward `MaxFrameLength`; exceeding either returns 413 |
| `Expect: 100-continue` | Supported | Sends 100 Continue while the body is still incomplete, at most once per request; works together with chunked |

The request body limit is **per request**, not a global memory cap; the per-connection framing budget is roughly `MaxRequestBodySize + 64KB + ReadBufferSize`, so lower it to fit your memory budget when `MaxConnects` is large.

A request body above 16MB exceeds the pool's per-array limit, so each request allocates a fresh full-frame buffer on the large object heap and drops it afterwards; with the 20MB default that is the per-request allocation, so prefer client-side chunking or a lower limit for sustained large uploads.

The spool threshold does not change the per-connection peak during framing: the whole frame must still be addressable, so spooling only removes the extra "frame → body array" copy and the post-request residency.

An oversized body first gets a 413; the server then half-closes the write side and drains the remaining body within a bounded budget: `Content-Length` is parsed as a 64-bit integer, and for fixed-length requests the unread bytes are that value minus what has arrived; chunked requests, or lengths beyond the `long` range, fall back to `MaxRequestBodySize` because the remainder cannot be inferred; `400` and `431` do no draining. Bytes that arrive during draining are discarded and never reach the application (counted as decrypted plaintext on TLS connections), and `WebConfig.RequestDrainTimeout` bounds the drain; the connection closes once the byte budget, the timeout, or an early client half-close is reached, so unread data cannot trigger a RST that hides the 413 from the client.

100 Continue is written directly to the raw socket, bypassing the response encoding chain. Header-phase malformation and fixed-length overflow are decided before the 100 is sent, so such rejected requests never receive 100 Continue first; for chunked requests the malformation or overflow may only surface in a later chunk, in which case the 100 has already been sent, which the protocol allows. When `Expect: 100-continue` is combined with chunked, the splitter first confirms through the chunk state machine that the body is incomplete, sends 100 once, then keeps reading and decoding the chunks. Known limitation: 100 Continue bypasses outbound serialization, so pipelined requests carrying `Expect` on one connection may interleave with the previous response, which is a non-compliant client usage.

---

## TLS / HTTPS 🔐

`WebConfig.UseSsl(...)` is the only TLS entry point; the SAEA.Sockets TLS wrapper performs the handshake, and once configured HTTPS and `wss://` share a single listening port:

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

Notes:

- Call it before `WebHost.Start()`; calling it after the server starts throws `InvalidOperationException` (same freeze semantics as `ConfigurePipeline`).
- Without `UseSsl` the server stays on plain HTTP exactly as before; a second call replaces the first.
- One port serves both HTTPS and WSS: the WebSocket upgrade reuses the same encrypted connection, so the protocol layer needs no distinction.
- Load certificates with `SaeaCertificateLoader.FromPfx` / `FromStore` / `FromPem`; use `ServerCertificateSelector` for SNI (multiple host names) and `ClientCertificates` plus `ClientCertificateRequired` for mutual TLS.

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
                          │ reuse or create ctx │
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

### Context reuse

`HttpContext` implements `SAEA.Http.Model.IReusableHttpContext` (`Src/SAEA.Http/Model/IReusableHttpContext.cs`): requests on the same connection share one context instance, which is dropped together with the connection.

- By default a context is still created and released per request; only a context implementing `IReusableHttpContext` is reused, and a custom `IHttpContext` that does not declare the interface keeps the unchanged behavior.
- Before reuse the framework calls `ResetForNextRequest()` to reset the state left by the previous request, then hands the context to `HttpHandle` / `HttpHandleAsync`.
- Reuse happens serially within a single connection, and `HttpContext.Current` points to that reused instance; the next request resets it. Do not keep the context in a background task or a static field for cross-request use.
- A custom context that declares `IReusableHttpContext` must restore all cross-request state in `ResetForNextRequest()` to the values of a fresh instance, otherwise data from the previous request leaks into the next one; a context upgraded to WebSocket does not take part in reuse.

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

### Measured on one machine (reference)

The table below is one snapshot from a same-machine load test. The site under test is a SAEA.MVC application assembled on top of SAEA.Http; SAEA.Http as a standalone server has not been benchmarked separately. Client and server run in two separate processes over IPv4 loopback on one machine, and each of the six cases is the median of 3 runs.

| Case | RPS | p50 / p99 |
|---|---:|---:|
| HTTP serial (5,000 requests, short connections) | 1,144 | 0.70 / 1.78 ms |
| HTTP 16 concurrent (10 s, short connections) | 5,830 | 2.11 / 12.36 ms |
| HTTP 64 concurrent (10 s, short connections) | 5,408 | 10.10 / 27.72 ms |
| HTTPS serial (2,000 requests, short connections) | 203 | 4.51 / 8.13 ms |
| Static 100 KB serial (memory cache hit) | 345 RPS / 33.7 MiB·s⁻¹ | 2.51 / 4.41 ms |
| WebSocket single-connection echo | 7,892 msg/s | 0.12 / 0.22 ms |

Scope and boundaries:

- The whole group is measured on **short connections**: each request opens its own connection, carries `Connection: close` and reads to EOF; for the keep-alive basis see the "Keep-alive benchmark (process-isolated)" section of [SAEA.MVC](../SAEA.MVC/README.md).
- HTTPS uses a self-signed certificate, TLS 1.2 fixed and certificate validation skipped; static files are served from the in-memory cache, so this is not the first-read or large-file chunked path.
- Raw values per round: HTTP serial 1,153 / 1,144 / 1,138; 16 concurrency 5,830 / 5,380 / 5,831; 64 concurrency 5,408 / 5,326 / 5,795; HTTPS 213 / 165 / 203; static 345 / 281 / 421 (33.7 / 27.4 / 41.1 MiB·s⁻¹); WebSocket 7,892 / 5,752 / 8,127 msg/s. HTTPS does one handshake per request and the static cell has only 300 samples, so these two cells vary most between rounds.
- Both concurrency cells had 1–8 connections out of every 30,000–60,000 receive no response before the fix (about 1×10⁻⁴, showing up as `unexpected EOF before response header` or an RST at teardown), and the RPS denominator is the number of requests actually completed. The table above was collected before the fix; after the connection-layer race was fixed (a disconnect left over from the previous connection closed a new connection whose token had already been reused), the same load generator completed 1,000,000 short connections at concurrency 64 / 128 / 256 with 0 no-response failures.
- Single-round snapshot: the same probe varies 20%–35% between rounds (the two concurrency cells differ by about 8% across their three repeats), so use it only for relative comparison within one run, never as a production absolute value, and never for cross-framework comparison.

The historical acceptance record (old version, old methodology) is at [MVC / TLS acceptance record](../../docs/superpowers/plans/2026-11-13-mvc-tls-acceptance.md).

### Per-request processing cost

- Response framing no longer builds the headers as a string and then encodes it: the header order has a single segmented description, and the string output (`BuildHeader`), the length accounting and the direct byte write all derive from it, so the final frame is allocated once and written directly, with no header string or intermediate byte array. The signature and textual output of `BuildHeader` are unchanged.
- Request parsing now scans the header text by index: the request line, each header line, the query string and the cookies no longer produce intermediate arrays, and the header and query dictionaries are pre-sized from the line count / separator count.
- In-process measurement on one machine (request parsing + dispatch + response encoding, **excluding socket send/receive**): per-request allocation 15,640 B -> 7,600 B. This is an in-process figure; for per-request allocation on the full network path see the "Per-request allocation and CPU cost" section in the MVC README.

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

**A**: The body is fully read according to `Content-Length` before dispatch; anything above 20MB returns 413. Raise the limit before `Start` when needed:

The multipart parser tolerates a preamble before the first boundary, and caps a request at 1024 parts with each part header block limited to 16KB; above those caps it keeps what was already parsed and logs an error.

Multipart parsing scans once in 64KB buffered chunks and tolerates a boundary split across reads; a file part larger than LargeBodyThreshold spills to a temp file that is deleted when the request is released.

The request body and `FilePart.Data` are read-only `Stream` instances (seekable, `Position` readable and writable); save files with `FilePart.Save` or copy from `Data` yourself:

```csharp
// Must be set before Start; body only, in bytes
server.WebConfig.MaxRequestBodySize = 64 * 1024 * 1024;

server.WebConfig.RequestDelegate = ctx =>
{
    var files = ctx.Request.PostFiles;
    foreach (var file in files)
    {
        var savePath = Path.Combine("uploads", file.FileName);
        file.Save(savePath);   // copies from Data; can be called repeatedly
    }

    // Non-multipart requests can read the raw body stream; rewind first and restore when done
    var body = ctx.Request.Body;
    if (body != null)
    {
        body.Position = 0;
        using (var reader = new StreamReader(body, Encoding.UTF8, true, 4096, true))
        {
            var text = reader.ReadToEnd();
        }
        body.Position = 0;
    }

    ctx.Response.ContentType = "application/json; charset=utf-8";
    ctx.Response.Write("{\"uploaded\": true}");
    ctx.Response.End();
};
```

A body above `WebConfig.LargeBodyThreshold` (1MB by default) is spooled to a temp file during decoding and deleted when the request ends, so the payload does not stay resident afterwards; `0` disables spooling. Framing still requires the whole frame to be addressable: spooling removes the extra "frame → body array" copy and the post-request residency, but does not lower the per-connection peak during framing.

```csharp
server.WebConfig.LargeBodyThreshold = 1024 * 1024;                          // spool threshold in bytes; 0 keeps everything in memory
server.WebConfig.TempDirectory = Path.Combine(Path.GetTempPath(), "uploads-tmp"); // defaults to saea-http under the system temp directory
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

**A**: Yes. The splitter frames the whole request with a chunk state machine, and the decoder strips chunk headers and trailers before dispatch; the decoded length is capped by `MaxRequestBodySize` and the raw chunk bytes count toward `MaxFrameLength`, so exceeding either limit returns 413.

---

## Core Types 🔧

| Type | Description |
|------|-------------|
| `WebHost` | Server entry point |
| `HttpRequest` | Request wrapper (method, URL, headers, forms …) |
| `HttpResponse` | Response wrapper (status, headers, body …) |
| `HttpContext` | Request context (Request/Response/Session); implements `IReusableHttpContext` so one instance is reused per connection, while a custom context that does not declare the interface is still created per request |
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

### Async writes

`WebHost` and `HttpResponse` both expose async entry points, and the synchronous methods are kept:

| Synchronous entry | Async entry |
| --- | --- |
| `WebHost.Send` / `WebHost.End` | `WebHost.SendAsync` / `WebHost.EndAsync` |
| `HttpResponse.Send` / `HttpResponse.End` | `HttpResponse.SendAsync` / `HttpResponse.EndAsync` |
| `HttpResponse.WriteRaw` | `HttpResponse.WriteRawAsync` |
| `HttpResponse.SendHeader` / `SendData` / `SendEnd` | The matching `*Async` methods |

- The synchronous methods are internally "async implementation + `GetAwaiter().GetResult()`", so both paths share one implementation.
- `EndAsync` waits for the write to actually complete before committing the close, so a large response body is not truncated by the close on a slow client.
- Write failures are still logged only and never thrown to business code, same as the synchronous entries.
- A custom `IWebHost` implementation (one that does not inherit the built-in `WebHost`) falls back to synchronous writes on the async path.

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

`Server.MapPath` returns `null` for paths that escape the site root or are invalid; callers must check for null before using the result as a file path.

```csharp
using SAEA.Http.Model;

server.WebConfig.RequestDelegate = ctx =>
{
    var filePath = ctx.Server.MapPath("/files/document.pdf");

    // null means the path escapes the site root or is invalid; respond with 404
    if (filePath == null)
    {
        ctx.Response.SetCached(new HttpContentResult("o_o, no content found", System.Net.HttpStatusCode.NotFound));
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
    timeout: 180,               // idle connection reclamation (seconds)
    connectTimeout: 3);         // connect timeout (seconds)

server.WebConfig.ActionTimeout = 120; // action execution timeout (seconds): 0 disables the wrapper (sync actions run inline), above 0 wraps the call and returns 408
server.WebConfig.MaxRequestBodySize = 20 * 1024 * 1024; // request body limit in bytes, body only, must be set before Start
server.WebConfig.LargeBodyThreshold = 1024 * 1024; // spool threshold in bytes; 0 keeps everything in memory, must be set before Start
server.WebConfig.TempDirectory = null; // spool directory; null means saea-http under the system temp directory
server.WebConfig.RequestDrainTimeout = 2000; // drain timeout in ms for the remaining body after a 413; 0 disables draining
server.WebConfig.ConfigurePipeline = (id, pipeline) => { /* ... */ };
server.WebConfig.UseSsl(o => { /* ... */ });
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