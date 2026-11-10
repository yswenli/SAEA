# SAEA.WebSocket - 高性能 WebSocket 服务器/客户端 🔌

[![NuGet version](https://img.shields.io/nuget/v/SAEA.WebSocket.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.WebSocket)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**[English Version](README.en.md)** | **中文版**

> 基于 .NET Standard 2.0 的高性能 WebSocket 组件，完整实现 RFC 6455 协议，面向高并发连接场景。
>
> ⚡ **零拷贝内核**：构建于 SAEA.Sockets 的 Span/Memory 零拷贝收发、内存池与 `SAEA.Sockets.Pipeline` 处理器链之上，显著降低分配与 GC。
>
> 🧩 **处理器链装配**：接收、发送、握手与分帧都由链上节点完成，使用者只在自己的处理器里写业务，不再有面向使用者的收发事件。

## 快速导航 🧭

| 章节 | 内容 |
|------|------|
| [⚡ 30秒快速开始](#30秒快速开始) | 最简单的上手示例 |
| [🎯 核心特性](#核心特性) | 框架的主要功能 |
| [📐 架构设计](#架构设计) | 组件关系与工作流程 |
| [💡 应用场景](#应用场景) | 何时选择 SAEA.WebSocket |
| [📊 性能对比](#性能对比) | 与其他方案对比 |
| [❓ 常见问题](#常见问题) | FAQ 快速解答 |
| [🔧 核心类](#核心类) | 主要类一览 |
| [📝 使用示例](#使用示例) | 详细代码示例 |

---

## 30秒快速开始 ⚡

最快上手方式，只需3步即可运行 WebSocket 服务器：

### Step 1: 安装 NuGet 包

```bash
dotnet add package SAEA.WebSocket
```

### Step 2: 创建 WebSocket 服务器

```csharp
using SAEA.Sockets.Pipeline;
using SAEA.WebSocket;
using SAEA.WebSocket.Model;

var server = new WSServer(port: 39654);

// 每个连接装配一次：收到什么就回什么
server.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new EchoHandler(server, id));
server.Start();

sealed class EchoHandler : InboundHandlerAdapter
{
    private readonly WSServer _server;
    private readonly string _id;

    public EchoHandler(WSServer server, string id)
    {
        _server = server;
        _id = id;
    }

    public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
    {
        var frame = msg as WSProtocal;
        if (frame == null)
            return ctx.FireChannelReadAsync(msg);

        // 帧体由池化缓冲承载，先复制再交给别的线程或延后使用
        _server.Reply(_id, new WSProtocal(frame.Type, frame.Content.ToArray()));
        return Task.CompletedTask;
    }
}
```

### Step 3: 创建 WebSocket 客户端连接

```csharp
using SAEA.Sockets.Pipeline;
using SAEA.WebSocket;
using SAEA.WebSocket.Model;

var client = new WSClient("ws://127.0.0.1:39654");
client.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new PrintHandler());

if (client.Connect())
{
    client.Send("Hello WebSocket!");
}

sealed class PrintHandler : InboundHandlerAdapter
{
    public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
    {
        if (msg is WSProtocal frame)
            Console.WriteLine(Encoding.UTF8.GetString(frame.Content.Span));

        return Task.CompletedTask;
    }
}
```

**就这么简单！** 🎉 你已经实现了一个面向高并发场景的高性能 WebSocket 通信系统。

---

## 核心特性 🎯

| 特性 | 说明 | 优势 |
|------|------|------|
| 🚀 **跨平台异步 IO** | 底层 IO 由 .NET 运行时选择 | 面向高并发连接场景，CPU 利用率高 |
| 📜 **RFC 6455 完整实现** | 标准 WebSocket 协议 | 兼容所有浏览器和客户端 |
| 🧩 **处理器链** | `ConfigurePipeline` 逐连接装配 | 业务与协议分层，收发不再依赖事件 |
| 🔒 **SSL/TLS 加密** | `wss://` 与证书配置，TLS 由 BouncyCastle 实现 | 不依赖 `SslStream`，全平台一致 |
| 💬 **多帧类型支持** | Text、Binary、Ping/Pong、Close | 满足各种通信场景 |
| 💓 **内置心跳应答** | Ping 自动回 Pong、Close 自动回 Close 后收口 | 心跳保活不需要业务代码 |
| 🧵 **分片消息重组** | continuation 拼接、分片间允许控制帧 | 大消息按消息边界交付 |
| 📏 **消息体积上限** | 单条消息默认 64MB，超限按协议错误收口 | 防止超大消息撑爆内存 |
| 📡 **子协议协商** | `Sec-WebSocket-Protocol` 支持 | 灵活的协议扩展 |
| 📦 **内存池复用** | 帧体走 `MemoryPoolManager` 池化缓冲 | GC 压力更低 |

---

## 架构设计 📐

### 组件架构图

```
┌───────────────────────────────────────────────────────────────┐
│                    SAEA.WebSocket 架构                         │
├───────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌──────────────┐        ┌──────────────┐                    │
│  │   WSServer   │        │   WSClient   │                    │
│  │  (服务器端)  │        │  (客户端)    │                    │
│  └──────┬───────┘        └──────┬───────┘                    │
│         │  每个连接一条链        │                             │
│         └──────────┬───────────┘                              │
│                    ▼                                           │
│         ┌──────────────────────┐                               │
│         │   IChannelPipeline   │  ← SAEA.Sockets.Pipeline      │
│         │   (按序经过各节点)   │                               │
│         └──────────┬───────────┘                               │
│                    │                                           │
│   出站 ◄───────────┼───────────► 入站                          │
│  WsMessageEncoder  │          WsHandshakeHandler               │
│  OutboundSerializing│          WsFrameDecoder                  │
│                    │          业务处理器 → ExceptionTerminal   │
└───────────────────────────────────────────────────────────────┘
```

### 链路顺序

| 方向 | 节点顺序（从左到右） | 职责 |
|------|----------------------|------|
| 出站 | `WsMessageEncoder` → `OutboundSerializingHandler` | 帧编码（服务端不掩码、客户端掩码）、合并写出 |
| 入站 | `WsHandshakeHandler` → `WsFrameDecoder` → **业务处理器** → `ExceptionTerminalHandler` | 握手校验与协议切换、分片重组、业务处理、异常收口 |

入站方向还可插入会话处理器（服务端 `SessionHandler`、客户端 `ClientSessionHandler`），它负责 Ping→Pong 自动应答与 Close 收口；这两类控制帧不会出现在业务处理器里，数据帧与 Pong 才会向下派发。

### 工作流程图

```
客户端连接流程:

客户端 ──► TCP 建立 ──► ChannelActiveAsync（传输级活跃，握手尚未完成）
                              │
                              ▼
                    WsHandshakeSplitter 切出握手块
                              │
                              ▼
                    WsHandshakeHandler 校验并回 101
                              │
                              ▼
                    切到 WsFrameSplitter（协议升级完成）
                              │
                              ▼
                    首帧到达 = 正式进入数据阶段

数据接收流程:

TCP 字节 ──► WsFrameSplitter ──► WsFrameDecoder
                                      │
                                      ▼
                          ┌───────────────────────┐
                          │ 分片重组 / 消息上限判断│
                          └───────────┬───────────┘
                                      │
                                      ▼
                            业务处理器 ChannelReadAsync
                                      │
                                      ▼
                          ctx.Pipeline.WriteAsync(WSProtocal)
                                      │
                                      ▼
                    WsMessageEncoder ──► 发送响应帧给客户端
```

---

## 应用场景 💡

### ✅ 适合使用 SAEA.WebSocket 的场景

| 场景 | 描述 | 推荐理由 |
|------|------|----------|
| 💬 **即时通讯** | 私聊、群聊、客服系统 | 双向实时通信，低延迟 |
| 📊 **实时数据推送** | 股票行情、体育比分、监控数据 | 高吞吐量，实时性强 |
| 🎮 **游戏通信** | 实时对战、状态同步 | 异步 IO 支持高并发玩家在线 |
| 🤖 **IoT 设备** | 设备监控、远程控制 | 支持大量设备并发连接 |
| 📝 **协作工具** | 在线编辑、白板共享 | 双向同步，实时协作 |
| 🔔 **推送通知** | 系统消息、订单通知 | 即时触达，无需轮询 |

### ❌ 不适合的场景

| 场景 | 推荐替代方案 |
|------|--------------|
| 简单 HTTP API | 使用 SAEA.Http 或 ASP.NET |
| 原生 TCP 协议 | 使用 SAEA.Sockets |
| MQTT 设备 | 使用 SAEA.MQTT |
| Redis 缓存 | 使用 SAEA.RedisSocket |

---

## 性能对比 📊

### 与传统 WebSocket 方案对比

| 指标 | SAEA.WebSocket | 传统 WebSocket | 优势 |
|------|----------------|----------------|------|
| **并发连接数** | 面向高并发 | 中等 | **异步 IO** |
| **CPU 利用率** | 高 | 中 | **异步 IO** |
| **内存占用** | 池化复用 | 频繁分配 | **GC 压力降低** |
| **延迟** | 低 | 较高 | **低延迟响应** |
| **吞吐量** | 高 | 中 | **高吞吐** |

### 与 SignalR 对比

| 特性 | SAEA.WebSocket | SignalR | 说明 |
|------|----------------|---------|------|
| **依赖** | 轻量级 | ASP.NET Core | SAEA 更轻量 |
| **协议支持** | 纯 WebSocket | WebSocket + LongPolling 等 | SignalR 更全面 |
| **性能** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | SAEA 异步 IO 优化 |
| **学习曲线** | 简单 | 中等 | SAEA 更易上手 |
| **跨平台** | .NET Standard | .NET Core | 两者都跨平台 |

> 💡 **提示**: SAEA.WebSocket 基于跨平台异步 IO 实现高并发，适合对性能要求极高的场景。

---

## 常见问题 ❓

### Q1: WebSocket 和 HTTP 长轮询有什么区别？

**A**: WebSocket 是全双工通信协议，相比 HTTP 长轮询：
- 连接保持：WebSocket 建立后保持连接，无需重复握手
- 实时性：服务端可主动推送，延迟更低
- 效率：无 HTTP 头开销，数据传输更高效
- 资源：减少服务器连接数和带宽消耗

### Q2: 如何实现 SSL/TLS 安全连接？

**A**: 服务端按证书启动，客户端用 `wss://` 或显式配置 TLS 选项：

```csharp
// 服务端：pfx 证书
var server = new WSServer(port: 443, protocols: SslProtocols.Tls12,
    pfxPath: "server.pfx", pwd: "password");
server.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new EchoHandler(server, id));
server.Start();

// 客户端：地址里带 wss:// 即启用 TLS
var client = new WSClient("wss://127.0.0.1:443");
```

需要自签证书、指定目标主机或放宽校验时，用带 `Action<SaeaTlsOptions>` 的重载：

```csharp
var client = new WSClient("127.0.0.1", 443, SubProtocolType.Default, "", 64 * 1024, tls =>
{
    tls.TargetHost = "127.0.0.1";
    tls.AllowInvalidCertificates = true;   // 仅自签证书的测试环境
});
```

### Q3: 如何处理自定义协议/消息格式？

**A**: 帧体还是你自己的字节，在处理器里做序列化即可：

```csharp
sealed class JsonHandler : InboundHandlerAdapter
{
    public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
    {
        if (msg is WSProtocal frame)
        {
            // 帧体只在本次回调内有效，需要跨线程或延后使用时先复制
            var json = Encoding.UTF8.GetString(frame.Content.Span);
            var my = JsonConvert.DeserializeObject<MyMessage>(json);
            // ...
        }

        return Task.CompletedTask;
    }
}

client.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new JsonHandler());
client.Send(JsonConvert.SerializeObject(new MyMessage { Type = 1, Content = "Hello" }));
```

### Q4: 心跳机制是如何工作的？

**A**: 链路内置应答：收到 Ping 自动回 Pong，收到 Close 自动回 Close 后收口，业务处理器只会看到数据帧和 Pong。

```csharp
// 客户端主动发心跳
client.Ping();

// Pong 会作为 WSProtocal（Type = Pong）派发到业务处理器
if (frame.Type == WSProtocalType.Pong)
    Console.WriteLine("服务器存活");
```

建议定期发送 Ping（如每30秒），超过超时时间未响应则认为连接断开。

### Q5: 如何设置子协议？

**A**: 客户端在构造函数里指定子协议：

```csharp
// 客户端指定子协议
var client = new WSClient("ws://127.0.0.1:39654", SubProtocolType.Json);

// 服务端在业务处理器里按会话判断，不需要额外注册
server.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new ChatHandler(id));
```

### Q6: 支持的最大连接数是多少？

**A**: 服务器构造函数默认 `maxConnects: 1000`，可显式调整：

```csharp
var server = new WSServer(port: 39654, bufferSize: 64 * 1024, maxConnects: 10000);
server.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new EchoHandler(server, id));
server.Start();
```

实际性能取决于服务器硬件配置和网络环境。

### Q7: 如何处理二进制数据？

**A**: 直接发送和接收字节数组：

```csharp
// 发送二进制数据
byte[] binaryData = new byte[] { 0x01, 0x02, 0x03 };
client.Send(binaryData, WSProtocalType.Binary);

// 接收时按类型分支
sealed class BinaryAwareHandler : InboundHandlerAdapter
{
    public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
    {
        if (msg is WSProtocal frame)
        {
            if (frame.Type == WSProtocalType.Binary)
                ProcessBinary(frame.Content.ToArray());
            else if (frame.Type == WSProtocalType.Text)
                Console.WriteLine(Encoding.UTF8.GetString(frame.Content.Span));
        }

        return Task.CompletedTask;
    }
}
```

### Q8: 连接建立/断开通知在哪里？

**A**: 在自己的处理器里重写 `ChannelActiveAsync` / `ChannelInactiveAsync`，或对服务端使用 `Clients` 快照。

```csharp
sealed class LifecycleHandler : InboundHandlerAdapter
{
    public override Task ChannelActiveAsync(IChannelHandlerContext ctx)
    {
        // 传输级已连通；服务端此刻 WebSocket 握手可能还没完成
        return Task.CompletedTask;
    }

    public override Task ChannelInactiveAsync(IChannelHandlerContext ctx)
    {
        // 连接已收口，可在这里清理会话状态
        return Task.CompletedTask;
    }

    public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
    {
        // 收到第一帧即代表握手已完成、进入数据阶段
        return ctx.FireChannelReadAsync(msg);
    }
}
```

与旧版 `OnConnected` 的差异：`ChannelActiveAsync` 是**传输级**通知，服务端在 TCP 建立后立即触发（早于握手完成）；客户端因为 `Connect()` 内部要等握手结果，通常在握手成功后才观察到。需要「握手完成」这个确切时机时，以收到首帧为准。

### Q9: 发送被拒绝是怎么回事？

**A**: 出站待写项有上限（默认 `MaxPendingWrites = 256`），超过上限的发送会被拒绝并抛异常，`WSClient.Send` 会记录失败日志而不向业务线程抛出。

```csharp
// 批量发送时用回包计数节流，避免瞬时打满出站预算
int sent = 0;
int acked = 0;   // 在业务处理器的 ChannelReadAsync 里 Interlocked.Increment

for (int i = 0; i < total; i++)
{
    client.Send(payload);
    Interlocked.Increment(ref sent);

    while (Volatile.Read(ref acked) < Volatile.Read(ref sent) - 128)
        Thread.Sleep(1);
}
```

需要自定义链参数时，可在 `ConfigurePipeline` 里加自己的处理器，但链首的出站前缀与链尾的异常收口节点由框架保留。

---

## 核心类 🔧

| 类名 | 说明 |
|------|------|
| `WSServer` | WebSocket 服务器，支持 SSL/TLS，`ConfigurePipeline` 装配链路 |
| `WSClient` | WebSocket 客户端，支持 `ws://`、`wss://` 与 TLS 选项重载 |
| `WSProtocal` | WebSocket 帧封装，`Content` 为池化缓冲的只读视图 |
| `WSProtocalType` | 协议类型枚举（Text/Binary/Ping/Pong/Close） |
| `WSCoder` | 直接的编解码器，需要脱离处理器链手写收发时使用 |
| `WSUserToken` | 用户会话令牌，管理单个连接状态 |
| `WsFrameDecoder` | 分片重组、控制帧穿插与消息上限校验 |
| `WsMessageEncoder` | 出站帧编码器（服务端不掩码、客户端掩码） |
| `WsHandshakeHandler` | 握手校验、101/请求应答与协议切换 |

---

## 使用示例 📝

### WebSocket 服务器

```csharp
using SAEA.Sockets.Pipeline;
using SAEA.WebSocket;
using SAEA.WebSocket.Model;

var server = new WSServer(port: 39654);

server.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new EchoHandler(server, id));
server.Start();

Console.WriteLine($"WebSocket 服务器已启动，当前连接数: {server.Clients.Count}");
```

### WSS 安全服务器

```csharp
using System.Security.Authentication;
using SAEA.WebSocket;

var server = new WSServer(port: 443, protocols: SslProtocols.Tls12,
    pfxPath: "server.pfx", pwd: "password");

server.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new EchoHandler(server, id));
server.Start();
```

### WebSocket 客户端

```csharp
using SAEA.Sockets.Pipeline;
using SAEA.WebSocket;

var client = new WSClient("ws://127.0.0.1:39654");
client.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new PrintHandler());

if (client.Connect())
{
    client.Send("Hello WebSocket!");
    client.Ping();
}
```

### 在处理器里回包

```csharp
sealed class ReplyHandler : InboundHandlerAdapter
{
    public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
    {
        if (!(msg is WSProtocal frame))
            return ctx.FireChannelReadAsync(msg);

        // 走当前连接的链路写出，帧编码由 WsMessageEncoder 完成
        return ctx.Pipeline.WriteAsync(new WSProtocal(frame.Type, frame.Content.ToArray()));
    }
}
```

服务端还可用 `server.Reply(id, frame)` / `server.Disconnect(id)` 从任意处理器按会话操作。

### URL 格式支持

```csharp
var client1 = new WSClient("ws://127.0.0.1:39654");
var client2 = new WSClient("wss://secure.example.com:443");  // SSL
var client3 = new WSClient("127.0.0.1", 39654);  // IP + 端口
var client4 = new WSClient(new Uri("ws://example.com/ws"));
```

### 子协议协商

```csharp
// 常量：Empty / Default / Json / Protocol2，也可直接传自定义字符串
var client = new WSClient("ws://127.0.0.1:39654", SubProtocolType.Json);
```

### 处理不同帧类型

```csharp
using SAEA.WebSocket.Model;
using SAEA.WebSocket.Type;

sealed class FrameSwitchHandler : InboundHandlerAdapter
{
    public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
    {
        if (!(msg is WSProtocal frame))
            return ctx.FireChannelReadAsync(msg);

        switch (frame.Type)
        {
            case WSProtocalType.Text:
                Console.WriteLine($"文本消息: {Encoding.UTF8.GetString(frame.Content.Span)}");
                break;
            case WSProtocalType.Binary:
                Console.WriteLine($"二进制消息: {frame.Content.Length} 字节");
                break;
            case WSProtocalType.Pong:
                Console.WriteLine("心跳响应");
                break;
        }

        return Task.CompletedTask;
    }
}
```

> ⚠️ `Close` 帧由内置会话处理器处理（回写 Close 后收口），不会进入业务处理器。

---

## 协议帧类型

```csharp
public enum WSProtocalType : byte
{
    Cont = 0,      // 连续帧
    Text = 1,      // 文本消息
    Binary = 2,    // 二进制消息
    Close = 8,     // 关闭帧
    Ping = 9,      // 心跳请求
    Pong = 10      // 心跳响应
}
```

---

## 依赖项

| 包名 | 版本 | 说明 |
|------|------|------|
| SAEA.Sockets | 26.11.5.1 | Socket 通信框架 |
| SAEA.Sockets.Pipeline | 26.11.5.1 | 处理器链与分帧执行器 |
| SAEA.Common | 26.11.5.1 | 公共工具类 |

---

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包](https://www.nuget.org/packages/SAEA.WebSocket)
- [作者博客](https://www.cnblogs.com/yswenli/)

---

## 许可证

Apache License 2.0