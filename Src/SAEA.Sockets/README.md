# SAEA.Sockets - 高性能 IOCP Socket 通信框架 🔌

[![NuGet version](https://img.shields.io/nuget/v/SAEA.Sockets.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.Sockets)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**[English Version](README.en.md)** | **中文版**

> 基于 .NET Standard 2.0 的高性能 Socket 通信框架，采用 Windows IOCP 完成端口技术，支持万级并发连接。

## 快速导航 🧭

| 章节 | 内容 |
|------|------|
| [⚡ 30秒快速开始](#30秒快速开始) | 最简单的上手示例 |
| [🎯 核心特性](#核心特性) | 框架的主要功能 |
| [📐 架构设计](#架构设计) | 组件关系与工作流程 |
| [💡 应用场景](#应用场景) | 何时选择 SAEA.Sockets |
| [📊 性能对比](#性能对比) | 与其他方案对比 |
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
    .UseIocp()
    .SetPort(39654)
    .Build();

var server = SocketFactory.CreateServerSocket(option);
server.OnReceive += (id, data) => server.Send(id, data);  // 收到消息立即回复
server.Start();
```

### Step 3: 创建 TCP 客户端连接

```csharp
var client = SocketFactory.CreateClientSocket(option);
client.OnReceive += (data) => Console.WriteLine(Encoding.UTF8.GetString(data));
client.Connect();
client.SendAsync(Encoding.UTF8.GetBytes("Hello SAEA!"));
```

**就这么简单！** 🎉 你已经实现了一个支持万级并发的高性能 TCP 通信系统。

---

## 核心特性 🎯

| 特性 | 说明 | 优势 |
|------|------|------|
| 🚀 **IOCP 高性能** | Windows 完成端口异步模型 | 支持万级并发连接，CPU 利用率高 |
| 🔒 **SSL/TLS 加密** | 流模式支持安全连接 | 数据传输加密，保护隐私 |
| 📡 **双协议支持** | TCP + UDP 双模式 | TCP 可靠传输，UDP 高速广播 |
| 🌐 **IPv6 支持** | 完全兼容 IPv6 协议 | 适应未来网络环境 |
| 💾 **内存池优化** | BufferManager、UserTokenPool | 减少内存分配，降低 GC 压力 |
| 🔄 **会话管理** | SessionManager 自动管理 | 超时自动清理，连接状态追踪 |
| 🛠️ **自定义协议** | ICoder 接口扩展 | 灵活的协议编解码器 |
| 🔗 **Builder 配置** | 链式配置构建器 | 代码简洁，易于理解 |

---

## 架构设计 📐

### 组件架构图

```
┌─────────────────────────────────────────────────────────────┐
│                     SAEA.Sockets 架构                        │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────┐      ┌──────────────┐                    │
│  │ IocpSocket   │      │ StreamSocket │                    │
│  │  (IOCP模式)  │      │  (流模式)    │                    │
│  └──────────────┘      └──────────────┘                    │
│         │                     │                             │
│         └──────────┬──────────┘                             │
│                    │                                         │
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

网络数据 ──► IOCP 完成 ──► BufferManager 接收
                                │
                                ▼
                      ┌─────────────────┐
                      │     Coder       │ 解码数据
                      │   Decode()      │
                      └─────────────────┘
                                │
                                ▼
                      OnReceive 事件触发
                                │
                                ▼
                      ┌─────────────────┐
                      │     Coder       │ 编码响应
                      │   Encode()      │
                      └─────────────────┘
                                │
                                ▼
                      SendAsync 发送数据
```

---

## 应用场景 💡

### ✅ 适合使用 SAEA.Sockets 的场景

| 场景 | 描述 | 推荐理由 |
|------|------|----------|
| 🎮 **游戏服务器** | 实时对战、状态同步 | IOCP 支持万级玩家同时在线 |
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

## 性能对比 📊

### 与传统 Socket 方案对比

| 指标 | SAEA.Sockets | 传统 Socket | 优势 |
|------|--------------|-------------|------|
| **并发连接数** | 10,000+ | ~1,000 | **10倍提升** |
| **CPU 利用率** | ~85% | ~30% | **高效利用** |
| **内存占用** | 池化复用 | 频繁分配 | **GC 压力降低** |
| **延迟** | ~1ms | ~10ms | **低延迟响应** |
| **吞吐量** | 高 | 中 | **高吞吐** |

### IOCP vs 其他异步模型

| 模型 | 并发性能 | 适用平台 | 复杂度 |
|------|----------|----------|--------|
| **IOCP (SAEA.Sockets)** | ⭐⭐⭐⭐⭐ | Windows | 中等 |
| Select | ⭐⭐ | 跨平台 | 简单 |
| Poll | ⭐⭐ | 跨平台 | 简单 |
| Epoll (Linux) | ⭐⭐⭐⭐ | Linux | 中等 |

> 💡 **提示**: IOCP 是 Windows 平台最高效的异步 IO 模型，专为高并发场景设计。

---

## 常见问题 ❓

### Q1: IOCP 是什么？为什么选择 IOCP？

**A**: IOCP (I/O Completion Port) 是 Windows 平台的完成端口技术，是目前 Windows 上最高效的异步 IO 模型。相比传统的 Select/Poll 模型：
- 支持更大并发连接数（万级以上）
- CPU 利用率更高（单个线程处理多个连接）
- 系统资源消耗更低

### Q2: 如何实现自定义协议？

**A**: 实现 `ICoder` 接口或继承 `BaseCoder`：

```csharp
public class MyCoder : BaseCoder
{
    public override List<byte[]> Decode(byte[] data)
    {
        // 自定义解码逻辑，例如：解析消息头、消息体
        return base.Decode(data);
    }

    public override byte[] Encode(byte[] data)
    {
        // 自定义编码逻辑，例如：添加消息头
        return base.Encode(data);
    }
}

// 使用自定义编码器
public class MyContext : BaseContext<MyCoder>
{
    public MyContext(BaseUserToken userToken) : base(userToken) { }
}
```

### Q3: 如何配置 SSL/TLS 加密？

**A**: 使用 Stream 模式并配置证书：

```csharp
var option = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)
    .UseStream()  // 使用流模式
    .UseSsl()     // 启用 SSL
    .SetSslCertificate("server.pfx", "password")
    .Build();
```

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

**A**: 默认配置支持 1000 个连接，可通过 `SetCount()` 调整：

```csharp
var option = SocketOptionBuilder.Instance
    .SetCount(10000)  // 设置最大连接数
    .Build();
```

实际性能取决于服务器硬件配置。

---

## 核心类 🔧

| 类名 | 说明 |
|------|------|
| `IocpServerSocket` / `IocpClientSocket` | IOCP 模式 TCP 服务器/客户端 |
| `StreamServerSocket` / `StreamClientSocket` | 流模式 TCP 服务器/客户端（支持 SSL） |
| `UdpServerSocket` / `UdpClientSocket` | UDP 服务器/客户端 |
| `SocketOptionBuilder` | 链式配置构建器 |
| `SocketFactory` | Socket 工厂类 |
| `SessionManager` | 会话管理器 |
| `BaseCoder` | 默认协议编码器（8字节长度 + 1字节类型 + 内容） |
| `BufferManager` | 内存缓冲池 |
| `UserTokenPool` | 用户令牌池 |

---

## 使用示例 📝

### TCP 服务器（IOCP 模式）

```csharp
using SAEA.Sockets;
using SAEA.Sockets.Handler;

// 使用 Builder 链式配置服务器
var option = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)  // 设置 Socket 类型为 TCP
    .UseIocp()                       // 使用 IOCP 异步模型
    .SetIP("127.0.0.1")              // 监听 IP
    .SetPort(39654)                  // 监听端口
    .SetBufferSize(1024 * 64)        // 缓冲区大小 64KB
    .SetCount(1000)                  // 最大连接数
    .Build();                        // 构建配置

var server = SocketFactory.CreateServerSocket(option);

// 注册事件处理
server.OnAccepted += (id) => 
    Console.WriteLine($"客户端连接: {id}");

server.OnReceive += (id, data) => 
{
    var message = Encoding.UTF8.GetString(data);
    Console.WriteLine($"收到数据: {message}");
    server.Send(id, data);  // 回复客户端
};

server.OnDisconnected += (id) => 
    Console.WriteLine($"客户端断开: {id}");

server.Start();
Console.WriteLine("服务器已启动!");
```

### TCP 客户端（IOCP 模式）

```csharp
using SAEA.Sockets;

var option = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)
    .UseIocp()
    .SetIP("127.0.0.1")
    .SetPort(39654)
    .Build();

var client = SocketFactory.CreateClientSocket(option);

// 注册事件处理
client.OnReceive += (data) => 
    Console.WriteLine($"收到: {Encoding.UTF8.GetString(data)}");

client.OnDisconnected += () => 
    Console.WriteLine("连接断开");

// 连接服务器
client.Connect();

// 发送数据
client.SendAsync(Encoding.UTF8.GetBytes("Hello SAEA!"));
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

server.OnReceive += (id, data) => 
{
    Console.WriteLine($"收到 UDP 数据");
    server.Send(id, data);  // 回发数据
};

server.Start();
```

### SSL/TLS 安全连接

```csharp
using SAEA.Sockets;

var option = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)
    .UseStream()              // 使用流模式（支持 SSL）
    .SetIP("127.0.0.1")
    .SetPort(443)
    .UseSsl()                 // 启用 SSL/TLS
    .SetSslCertificate("server.pfx", "password")  // 配置证书
    .Build();

var server = SocketFactory.CreateServerSocket(option);
server.Start();
```

### 快捷封装类

```csharp
using SAEA.Sockets.Shortcut;

// TCP 服务器快捷封装
var tcpServer = new TCPServer(39654);
tcpServer.OnReceive += (id, data) => tcpServer.Send(id, data);
tcpServer.Start();

// TCP 客户端快捷封装
var tcpClient = new TCPClient("127.0.0.1", 39654);
tcpClient.OnReceive += (data) => Console.WriteLine(Encoding.UTF8.GetString(data));
tcpClient.Connect();
tcpClient.Send("Hello!");

// UDP 快捷封装
var udpServer = new UDPServer(39655);
var udpClient = new UDPClient("127.0.0.1", 39655);
```

### 自定义协议编码器

```csharp
using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;

// 实现自定义编码器
public class MyCoder : BaseCoder
{
    public override List<byte[]> Decode(byte[] data)
    {
        // 自定义解码逻辑
        // 例如：解析自定义消息头、消息体
        return base.Decode(data);
    }

    public override byte[] Encode(byte[] data)
    {
        // 自定义编码逻辑
        // 例如：添加自定义消息头
        return base.Encode(data);
    }
}

// 使用自定义编码器
public class MyContext : BaseContext<MyCoder>
{
    public MyContext(BaseUserToken userToken) : base(userToken) { }
}
```

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
| SAEA.Common | 7.26.2.2 | 通用工具类库 |
| Pipelines.Sockets.Unofficial | 2.2.8 | Pipeline Socket 扩展 |
| System.IO.Pipelines | 10.0.2 | 高性能 IO 管道 |

---

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包](https://www.nuget.org/packages/SAEA.Sockets)
- [作者博客](https://www.cnblogs.com/yswenli/)

---

## 许可证

Apache License 2.0