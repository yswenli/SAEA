# SAEA.Sockets - 跨平台高性能 Socket 通信框架 🔌

[![NuGet version](https://img.shields.io/nuget/v/SAEA.Sockets.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.Sockets)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**[English Version](README.en.md)** | **中文版**

> 基于 .NET Standard 2.0 的跨平台 Socket 通信框架。底层异步 IO 机制由 .NET 运行时按操作系统自动选择：Windows 使用 IOCP，Linux 使用 epoll，macOS 使用 kqueue，对外始终是同一套 API。收发路径基于 SAEA.Common 零拷贝（Span/Memory）与内存池化。

## 快速导航 🧭

| 章节 | 内容 |
|------|------|
| [⚡ 30秒快速开始](#30秒快速开始) | 最简单的上手示例 |
| [🎯 核心特性](#核心特性) | 框架的主要功能 |
| [🖥️ 平台支持](#平台支持) | 三平台与底层 IO 机制 |
| [📐 架构设计](#架构设计) | 组件关系与工作流程 |
| [💡 应用场景](#应用场景) | 何时选择 SAEA.Sockets |
| [📊 实现模式对比](#实现模式对比) | 两种收发实现与选型 |
| [❓ 常见问题](#常见问题) | FAQ 快速解答 |
| [🔧 核心类](#核心类) | 主要类一览 |
| [📝 使用示例](#使用示例) | 详细代码示例 |

---

## 30秒快速开始 ⚡

最快上手方式，只需3步即可运行 TCP 服务器：

### Step 1: 安装 NuGet 包

```bash
dotnet add package SAEA.Sockets
```

### Step 2: 创建 TCP 服务器（仅需5行代码）

```csharp
using SAEA.Sockets;
using SAEA.Sockets.Handler;

// 创建服务器配置
var option = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)
    .UsePacket()
    .SetPort(39654)
    .Build();

var server = SocketFactory.CreateServerSocket(option);
server.OnServerReceiveSpan += (userToken, data) => server.Send(userToken.ID, data);  // 收到消息立即回复
server.Start();
```

### Step 3: 创建 TCP 客户端连接

```csharp
var client = SocketFactory.CreateClientSocket(option);
client.OnClientReceiveSpan += (data) => Console.WriteLine(Encoding.UTF8.GetString(data.ToArray()));
client.Connect();
client.SendAsync(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("Hello SAEA!")));
```

**就这么简单！** 🎉 你已经实现了一个面向高并发场景的 TCP 通信系统。

---

## 核心特性 🎯

| 特性 | 说明 | 优势 |
|------|------|------|
| 🚀 **跨平台异步 IO** | 底层机制由 .NET 运行时在 Windows/Linux/macOS 上自动选择 IOCP / epoll / kqueue | 同一套 API 三平台运行，无需条件编译或切换实现 |
| 🔒 **SSL/TLS 加密** | 内置 BouncyCastle TLS 1.2/1.3，事件模式与流模式通用 | 自动证书加载、SNI、双向证书与握手回调，不依赖 `SslStream` |
| 📡 **双协议支持** | TCP + UDP 双模式 | TCP 可靠传输，UDP 高速广播 |
| 🌐 **IPv6 支持** | 完全兼容 IPv6 协议 | 适应未来网络环境 |
| 💾 **零拷贝 + 内存池** | `OnServerReceiveSpan` Span 收包、BufferManager、UserTokenPool、MemoryPoolManager | 零拷贝解析，减少内存分配，降低 GC 压力 |
| 🔄 **会话管理** | SessionManager 自动管理 | 超时自动清理，连接状态追踪 |
| 🛠️ **自定义协议** | ICoder 接口扩展 | 灵活的协议编解码器 |
| 🔗 **Builder 配置** | 链式配置构建器 | 代码简洁，易于理解 |

---

## 平台支持 🖥️

| 平台 | 底层异步 IO 机制 |
|------|------------------|
| Windows | IOCP（I/O Completion Port） |
| Linux | epoll |
| macOS | kqueue |

三平台使用同一套 API，无需条件编译，也无需切换实现。底层机制由 .NET 运行时根据当前操作系统自动选择；框架侧只负责收发调度、协议编解码与会话管理。

> ⚠️ **命名说明**：`UsePacket()` 是历史命名，指框架的 `SocketAsyncEventArgs` 收发实现，**不是 Windows 限定**——它在 Linux/macOS 上同样可用，底层分别落到 epoll/kqueue。TCP 只有这一套底层引擎（`SaeaServerSocket` / `SaeaClientSocket`）。
>
> `UseStream()` 不再切换引擎，而是在同一引擎上额外暴露双工 `Stream` 门面（`GetStream()` / `ChannelInfo.Stream`），由该门面独占入站数据；TLS 与事件模式、流模式都能组合使用。

---

## 架构设计 📐

### 组件架构图

```
┌─────────────────────────────────────────────────────────────┐
│                     SAEA.Sockets 架构                        │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────────┐      ┌──────────────────┐            │
│  │ SaeaServerSocket │      │ SaeaClientSocket │            │
│  │ (SocketAsyncEvt) │      │ (SocketAsyncEvt) │            │
│  └────────┬─────────┘      └────────┬─────────┘            │
│           │        SaeaConnection (Pipe + TLS)              │
│           └──────────────┬──────────┘                       │
│                          │                                   │
│            ┌───────▼───────┐                                │
│            │  BaseSocket   │                                │
│            │   (基类)      │                                │
│            └───────┬───────┘                                │
│                    │                                         │
│     ┌──────────────┼──────────────┐                        │
│     │              │              │                         │
│  ┌──▼──┐      ┌────▼───┐     ┌───▼────┐                   │
│  │Pool│      │ Session│     │  Coder │                   │
│  │(池)│      │Manager │     │(编解码)│                   │
│  └──┬──┘      └────┬───┘     └───┬────┘                   │
│     │              │              │                         │
│  BufferPool    UserToken      ICoder                      │
│  (内存池)      (用户令牌)    (协议接口)                   │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 工作流程图

```
客户端连接流程:

客户端 ──► Connect ──► Server.Accepted
                              │
                              ▼
                    ┌─────────────────┐
                    │  UserTokenPool  │ 分配用户令牌
                    │   获取 Token    │
                    └─────────────────┘
                              │
                              ▼
                    ┌─────────────────┐
                    │ SessionManager  │ 注册会话
                    │  添加 Session   │
                    └─────────────────┘
                              │
                              ▼
                    OnAccepted 事件触发

数据接收流程:

网络数据 ──► 异步 IO 完成（IOCP/epoll/kqueue）──► BufferManager 接收
                                │
                                ▼
                      ┌─────────────────┐
                      │ SaeaConnection  │ （可选）TLS 解密
                      └─────────────────┘
                                │
                    ┌───────────┴───────────┐
                    ▼                       ▼
          ┌─────────────────┐     ┌─────────────────┐
          │ 流模式：Pipe    │     │ 事件模式：      │
          │ Stream 门面     │     │ Span 回调       │
          └─────────────────┘     └─────────────────┘
                                │
                                ▼
                      OnServerReceiveSpan 事件触发（事件模式）
                                │
                                ▼
                      ┌─────────────────┐
                      │     Coder       │ 编码响应
                      │   Encode()      │
                      └─────────────────┘
                                │
                                ▼
                      SendAsync(ReadOnlyMemory<byte>) 发送数据
```

---

## 应用场景 💡

### ✅ 适合使用 SAEA.Sockets 的场景

| 场景 | 描述 | 推荐理由 |
|------|------|----------|
| 🎮 **游戏服务器** | 实时对战、状态同步 | 异步 IO + 会话管理，适合大量长连接 |
| 📊 **实时数据推送** | 股票行情、体育比分 | 高吞吐量，低延迟 |
| 🤖 **IoT 设备通信** | 传感器数据上报 | 支持大量设备并发连接 |
| 💬 **即时通讯** | 私聊、群聊、客服系统 | 会话管理完善，事件驱动 |
| 📁 **文件传输** | 大文件分块传输 | 内存池优化，减少 GC |
| 🔗 **RPC 通信** | 微服务间通信 | 二进制协议，高效传输 |

### ❌ 不适合的场景

| 场景 | 推荐替代方案 |
|------|--------------|
| 简单 HTTP API | 使用 SAEA.Http 或 ASP.NET |
| 浏览器客户端 | 使用 SAEA.WebSocket |
| MQTT 设备 | 使用 SAEA.MQTT |
| Redis 缓存 | 使用 SAEA.RedisSocket |

---

## 实现模式对比 📊

TCP 只有一套底层引擎，`UsePacket()` 与 `UseStream()` 只决定数据如何交付，不再切换实现：

| 对比项 | 事件模式（`UsePacket()`） | 流模式（`UseStream()`） |
|--------|-------------------------|--------------------------|
| 主要类 | `SaeaServerSocket` / `SaeaClientSocket` | 同左（同一引擎） |
| 接收方式 | 每连接一个 `SocketAsyncEventArgs`，通过 `On*ReceiveSpan` 交付 | 每连接一个 `PipeReader`，通过 `GetStream()` / `ChannelInfo.Stream` 读写 |
| UDP | ✅ 支持（UDP 仅支持事件模式） | ❌ 不支持 |
| SSL/TLS | ✅ 支持（解密后走 Span 回调） | ✅ 支持（解密后进入 `Pipe`） |
| 数据消费 | 只走 Span 事件 | `Stream` 门面独占入站数据 |
| 底层 IO 机制 | 由 .NET 运行时选择：Windows IOCP / Linux epoll / macOS kqueue | 同上 |

> 命名说明：`UsePacket()` 指 `SocketAsyncEventArgs` 收发实现，与操作系统无关。两种模式互斥，后调用的方法生效。

### 关于性能数字

本 README 不提供跨框架性能对比数字（与 TouchSocket、DotNetty 等的差异请以自己场景的压测为准）。可复现的实测数据只有零拷贝收发路径：运行 `SAEA.P2PTest --bench-iocp`（4KB 帧、单机回环），结果见根目录 README。

---

## 常见问题 ❓

### Q1: 这个框架只能在 Windows 上运行吗？

**A**: 不是。Windows、Linux、macOS 使用同一套 API。底层异步 IO 机制由 .NET 运行时按操作系统自动选择——Windows 是 IOCP，Linux 是 epoll，macOS 是 kqueue——框架的收发调度、编解码和会话管理逻辑完全一致，不需要条件编译。

`UsePacket()` 中的 "Iocp" 是历史命名，指 `SocketAsyncEventArgs` 收发实现，不表示 Windows 限定。

### Q2: 如何实现自定义协议？

**A**: 实现 `ICoder` 接口或继承 `BaseCoder`：

```csharp
using System;
using System.Buffers;
using System.Text;
using SAEA.Sockets.Base;

// 自定义编码器：演示「复用 BaseCoder 的帧式编解码 + 按 Type 过滤」。
// 注意 BaseCoder.Encode/Decode 非 virtual，无法 override；EncodeText/Handle 仅为包装示意，
// 框架经 ICoder 调用的是基类的 Encode/Decode。若需自定义线格式，请直接实现 ICoder。
public class MyCoder : BaseCoder
{
    public const byte Text = 1;

    // 编码：把业务数据封装为自定义类型帧后写入 writer
    public void EncodeText(ReadOnlyMemory<byte> payload, IBufferWriter<byte> writer)
    {
        Encode(new BaseSocketProtocal(Text, payload), writer);
    }

    // 解码：传入收到的序列，返回的批次必须 using（帧内存在 Dispose 前有效）
    public void Handle(ReadOnlySequence<byte> data)
    {
        using (var frames = Decode(data))
        {
            foreach (var frame in frames.Frames)
            {
                if (frame.Type == Text)
                    Console.WriteLine($"收到文本帧: {Encoding.UTF8.GetString(frame.Content.ToArray())}");
            }
        }
    }
}
```

接线方式（`UsePacket<T>()` 内部构造 `BaseContext<T>`，把编码器交给框架使用）：

```csharp
using SAEA.Sockets;

var option = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)
    .UsePacket<MyCoder>()   // 注入自定义编码器
    .SetPort(39654)
    .Build();
```

### Q3: 如何配置 SSL/TLS 加密？

**A**: 通过 `WithSsl(...)` 统一入口配置，事件模式与流模式都可以启用：

```csharp
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using SAEA.Sockets;
using SAEA.Sockets.Ssl;

// 服务端：自动从 PFX 加载证书，默认启用 TLS 1.2 + TLS 1.3
var serverOption = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)
    .SetIP("0.0.0.0")
    .SetPort(443)
    .WithSsl(o =>
    {
        o.ServerCertificate = SaeaCertificateLoader.FromPfx("server.pfx", "password");
        o.Protocols = SaeaTlsProtocols.Default;
        o.ServerCertificateSelector = sni => sni switch
        {
            "api.example.com" => apiCertificate,
            _ => defaultCertificate
        };
        o.ClientCertificateRequired = true;            // 双向证书（mTLS）
        o.HandshakeCompleted = context =>
            Console.WriteLine($"TLS 握手完成：{context.NegotiatedProtocol}");
    })
    .Build();

// 客户端：设置 SNI 目标主机与证书校验策略
var clientOption = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)
    .SetIP("127.0.0.1")                    // 实际连接地址：SetIP 只接受 IP 字面量
    .SetPort(443)
    .WithSsl(o =>
    {
        o.TargetHost = "api.example.com";  // SNI 与证书主机名校验使用
        o.ClientCertificates = new X509Certificate2Collection(clientCertificate);
        o.CertificateValidationCallback = (sender, cert, chain, errors) => errors == SslPolicyErrors.None;
    })
    .Build();
```

握手成功后才触发 `OnAccepted`（服务端）或 `ConnectAsync` 完成（客户端）；失败时通过 `OnError` / `SaeaTlsOptions.HandshakeFailed`（或 `OnTlsHandshake(failed: ...)`）上报。`ChannelInfo.Tls` / `StreamUserToken.Tls` 可读取协商协议、密码套件与对端证书。内置 TLS 基于 BouncyCastle，不使用 `SslStream`，因此不要求流模式。

### Q4: 内存池的作用是什么？

**A**: `BufferManager` 和 `UserTokenPool` 通过预分配和复用内存：
- 减少 GC（垃圾回收）压力
- 避免频繁的内存分配/释放
- 提升整体性能和稳定性

### Q5: TCP 和 UDP 如何选择？

**A**: 
- **TCP**: 可靠传输、有序到达、适用游戏、聊天、RPC
- **UDP**: 高速传输、无连接、适用视频流、实时广播

SAEA.Sockets 同时支持两种模式，可根据场景灵活选择。

### Q6: 支持的最大连接数是多少？

**A**: `MaxConnects` 默认 1000，可通过 `SetMaxConnects()` 调整：

```csharp
var option = SocketOptionBuilder.Instance
    .SetMaxConnects(10000)  // 设置最大连接数
    .Build();
```

实际可用连接数还受操作系统约束：Linux/macOS 需要同步调高文件描述符上限（如 `ulimit -n`），Windows 取决于句柄与内存；最终值以压测结果为准。

---

## 核心类 🔧

| 类名 | 说明 |
|------|------|
| `SaeaServerSocket` / `SaeaClientSocket` | TCP 服务器/客户端（统一 IOCP 引擎，支持事件模式、流模式与内置 TLS） |
| `SaeaConnection` | 每条 TCP 连接的 Pipe + TLS 门面（背压、加解密与握手状态） |
| `SaeaTlsOptions` / `SaeaTlsProtocols` | TLS 配置、协议版本与回调 |
| `SaeaCertificateLoader` | PFX/P12、PEM、证书库与目录加载（目录模式支持 SNI 匹配） |
| `ChannelInfo` | 流模式下的连接信息（`Stream`、`Tls`、`UserToken`） |
| `UdpServerSocket` / `UdpClientSocket` | UDP 服务器/客户端 |
| `SocketOptionBuilder` | 链式配置构建器 |
| `SocketFactory` | Socket 工厂类 |
| `SessionManager` | 会话管理器 |
| `BaseCoder` | 默认协议编码器（8字节长度 + 1字节类型 + 内容） |
| `BufferManager` | 内存缓冲池（复用 `byte[]` 缓冲区） |
| `UserTokenPool` | 用户令牌池 |

---

## 使用示例 📝

### TCP 服务器（SocketAsyncEventArgs 模式）

```csharp
using SAEA.Sockets;
using SAEA.Sockets.Handler;

// 使用 Builder 链式配置服务器
var option = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)  // 设置 Socket 类型为 TCP
    .UsePacket()                       // SocketAsyncEventArgs 模式（跨平台）
    .SetIP("127.0.0.1")              // 监听 IP
    .SetPort(39654)                  // 监听端口
    .SetReadBufferSize(1024 * 64)    // 读缓冲区大小 64KB
    .SetMaxConnects(1000)            // 最大连接数
    .Build();                        // 构建配置

var server = SocketFactory.CreateServerSocket(option);

// 注册事件处理
server.OnAccepted += (obj) => 
    Console.WriteLine($"客户端连接: {((SAEA.Sockets.Interface.IUserToken)obj).ID}");

server.OnServerReceiveSpan += (userToken, data) => 
{
    var message = Encoding.UTF8.GetString(data.ToArray());
    Console.WriteLine($"收到数据: {message}");
    server.Send(userToken.ID, data);  // 回复客户端
};

server.OnDisconnected += (id, ex) => 
    Console.WriteLine($"客户端断开: {id}");

server.Start();
Console.WriteLine("服务器已启动!");
```

### TCP 客户端（SocketAsyncEventArgs 模式）

```csharp
using SAEA.Sockets;

var option = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)
    .UsePacket()
    .SetIP("127.0.0.1")
    .SetPort(39654)
    .Build();

var client = SocketFactory.CreateClientSocket(option);

// 注册事件处理
client.OnClientReceiveSpan += (data) => 
    Console.WriteLine($"收到: {Encoding.UTF8.GetString(data.ToArray())}");

client.OnDisconnected += (id, ex) => 
    Console.WriteLine("连接断开");

// 连接服务器
client.Connect();

// 发送数据
client.SendAsync(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("Hello SAEA!")));
```

### UDP 服务器

```csharp
using SAEA.Sockets;

var option = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Udp)  // UDP 模式
    .SetIP("127.0.0.1")
    .SetPort(39655)
    .Build();

var server = SocketFactory.CreateServerSocket(option);

server.OnServerReceiveSpan += (userToken, data) => 
{
    Console.WriteLine($"收到 UDP 数据");
    server.Send(userToken.ID, data);  // 回发数据
};

server.Start();
```

### SSL/TLS 安全连接

服务端（事件模式，TLS 解密后的明文进入 `OnServerReceiveSpan`）：

```csharp
using SAEA.Sockets;
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Model;
using SAEA.Sockets.Ssl;

var option = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)
    .SetIP("127.0.0.1")
    .SetPort(443)
    .WithSsl(o =>
    {
        o.ServerCertificate = SaeaCertificateLoader.FromPfx("server.pfx", "password");
        o.Protocols = SaeaTlsProtocols.Default;
    })
    .Build();

var server = new SAEA.Sockets.Core.Tcp.SaeaServerSocket(option);
server.Start();
```

流模式（握手成功后通过 `ChannelInfo.Stream` 读写）：

```csharp
var option = SocketOptionBuilder.Instance
    .UseStream()
    .SetIP("127.0.0.1")
    .SetPort(443)
    .WithSsl(o => o.ServerCertificate = SaeaCertificateLoader.FromPfx("server.pfx", "password"))
    .Build();

var server = new SAEA.Sockets.Core.Tcp.SaeaServerSocket(option);
server.OnAccepted += payload =>
{
    var channel = (ChannelInfo)payload;
    var buffer = new byte[4096];
    var read = channel.Stream.Read(buffer, 0, buffer.Length);
    channel.Stream.Write(buffer, 0, read);
};
server.Start();
```

客户端：

```csharp
var option = SocketOptionBuilder.Instance
    .UseStream()                       // 或不调用，使用事件模式
    .SetIP("127.0.0.1")
    .SetPort(443)
    .WithSsl(o =>
    {
        o.TargetHost = "localhost";    // SNI + 证书主机名校验
        o.AllowInvalidCertificates = true;
    })
    .Build();

using (var client = new SaeaClientSocket(option))
{
    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        await client.ConnectAsync(cts.Token);

    var stream = client.GetStream();   // 流模式下获取双工流
    var payload = Encoding.UTF8.GetBytes("hello");
    await stream.WriteAsync(payload, 0, payload.Length);
}
```

### 快捷封装类

```csharp
using SAEA.Sockets.Shortcut;

// TCP 服务器快捷封装
var tcpServer = new TCPServer(39654);
tcpServer.OnReceive += (sender, userToken, data) => tcpServer.SendAsync(userToken.ID, data);
tcpServer.Start();

// TCP 客户端快捷封装
var tcpClient = new TCPClient("127.0.0.1", 39654);
tcpClient.OnReceive += (sender, data) => Console.WriteLine(Encoding.UTF8.GetString(data.ToArray()));
tcpClient.Connect();
tcpClient.SendAsync("Hello!");

// UDP 快捷封装
var udpServer = new UDPServer(39655);
var udpClient = new UDPClient("127.0.0.1", 39655);
```

### 自定义协议编码器

```csharp
using System;
using System.Buffers;
using System.Text;
using SAEA.Sockets.Base;

// 自定义编码器：演示「复用 BaseCoder 的帧式编解码 + 按 Type 过滤」。
// 注意 BaseCoder.Encode/Decode 非 virtual，无法 override；EncodeText/Handle 仅为包装示意，
// 框架经 ICoder 调用的是基类的 Encode/Decode。若需自定义线格式，请直接实现 ICoder。
public class MyCoder : BaseCoder
{
    public const byte Text = 1;

    // 编码：把业务数据封装为自定义类型帧后写入 writer
    public void EncodeText(ReadOnlyMemory<byte> payload, IBufferWriter<byte> writer)
    {
        Encode(new BaseSocketProtocal(Text, payload), writer);
    }

    // 解码：传入收到的序列，返回的批次必须 using（帧内存在 Dispose 前有效）
    public void Handle(ReadOnlySequence<byte> data)
    {
        using (var frames = Decode(data))
        {
            foreach (var frame in frames.Frames)
            {
                if (frame.Type == Text)
                    Console.WriteLine($"收到文本帧: {Encoding.UTF8.GetString(frame.Content.ToArray())}");
            }
        }
    }
}
```

接线方式（`UsePacket<T>()` 内部构造 `BaseContext<T>`，把编码器交给框架使用）：

```csharp
using SAEA.Sockets;

var option = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)
    .UsePacket<MyCoder>()   // 注入自定义编码器
    .SetPort(39654)
    .Build();
```

---

## 从 byte[] 迁移 🔄

近期版本移除了 `byte[]` 公共面，统一改为 `Span` / `Memory`。下表列出已删除成员及其替代：

| 已删除 | 替代 |
|--------|------|
| `IClientSocket.OnReceive`（`OnClientReceiveHandler`，`byte[]`） | `OnClientReceiveSpan`（`ReadOnlySpan<byte>`，回调内同步消费/复制） |
| `IServerSocket.OnReceive`（`OnReceiveHandler`，`(ISession, byte[])`） | `OnServerReceiveSpan`（`(IUserToken, ReadOnlySpan<byte>)`） |
| `IClientSocket.BeginSend(byte[])` / `Send(byte[])` / `SendAsync(byte[])` / `SendAsync(byte[], int, int, CancellationToken)` | `Send(ReadOnlySpan<byte>)` / `SendAsync(ReadOnlyMemory<byte>)` / `SendAsync(ReadOnlyMemory<byte>, CancellationToken)`；原 offset/count 语义用 `data.AsMemory(offset, count)` 表达 |
| `IClientSocket.ReceiveAsync(byte[], int, int, CancellationToken)`（接口成员） | 改用 span 事件；具体实现 `SaeaClientSocket.ReceiveAsync` 保留并从连接 `Pipe` 读取（无连接门面时抛 `KernelException`），`UdpClientSocket.ReceiveAsync` 抛 `NotSupportedException` |
| `IServerSocket.SendAsync(string, byte[])` / `Send(string, byte[])` / `End(string, byte[])` / `SendAsync(IPEndPoint, byte[])` | `SendAsync(string, ReadOnlyMemory<byte>)` / `Send(string, ReadOnlySpan<byte>)` / `End(string, ReadOnlyMemory<byte>)` / `SendAsync(IPEndPoint, ReadOnlyMemory<byte>)` |
| `IocpClientSocket.UserToken` | 不再公开；按场景改用 `Connected` 或 `ChannelInfo.UserToken` / `Context.UserToken` |
| `IocpServerSocket` / `IocpClientSocket` | 重命名为 `SaeaServerSocket` / `SaeaClientSocket`（`SocketFactory` 已返回新类型） |
| `StreamServerSocket` / `StreamClientSocket` | 已删除；改用 `SaeaServerSocket` / `SaeaClientSocket` 并调用 `UseStream()`，通过 `GetStream()` 或 `ChannelInfo.Stream` 读写 |
| `UseSsl()` / `SetSslCertificate(...)` 与 `SslStream` 直连 | 统一改为 `WithSsl(...)` + `SaeaTlsOptions`；TLS 由 BouncyCastle 实现，事件模式与流模式均可用 |
| `ChannelManager.Instance` / `ChannelManager.GetInstance` | 每个服务器实例持有自己的 `ChannelManager`；旧静态入口仅为源码兼容保留并标记 `[Obsolete]` |
| 快捷封装 `TCPClient` / `TCPServer` / `UDPClient` / `UDPServer` 的 `OnReceive` | `TCPClient` / `TCPServer.OnReceive` 由 `byte[]` 改为 `ReadOnlyMemory<byte>`（`UDPClient` / `UDPServer.OnReceive` 交付 `ISocketProtocal` 帧）；**source/binary breaking，需重新编译** |

> ⚠️ **迁移注意**
> 1. **快捷封装的 `OnReceive` 为签名级 breaking 变更**（`byte[]` → `ReadOnlyMemory<byte>`），由版本号 bump 承载，**不提供兼容 shim**，请重新编译调用方。
> 2. 旧 `OnReceive` 事件已删除，改用 `OnClientReceiveSpan` / `OnServerReceiveSpan` 即可。
> 3. span 回调参数（`data`）**仅在本次回调期间有效**，必须同步消费或在回调内 `ToArray()` 复制；**切勿将 span 保存到字段或跨线程/异步使用**，否则为 use-after-free。

---

## 默认协议格式

`BaseCoder` 实现的默认消息协议：

```
| 8字节长度 | 1字节类型 | N字节内容 |
```

- **长度**: 数据总长度（用于消息边界识别）
- **类型**: 消息类型标识（可扩展）
- **内容**: 实际消息数据

---

## 依赖项

| 包名 | 版本 | 说明 |
|------|------|------|
| SAEA.Common | 26.11.5.1 | 通用工具类库 |
| BouncyCastle.Cryptography | 2.6.2 | 内置 TLS 1.2/1.3 协议实现 |
| Pipelines.Sockets.Unofficial | 2.2.16 | Pipeline Socket 扩展 |
| System.IO.Pipelines | 10.0.14 | 高性能 IO 管道 |

---

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包](https://www.nuget.org/packages/SAEA.Sockets)
- [作者博客](https://www.cnblogs.com/yswenli/)

---

## 许可证

Apache License 2.0