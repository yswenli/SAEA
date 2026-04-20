# ![Logo](/logo.jpg) SAEA - 高性能网络通信框架家族 🚀

[![NuGet version (SAEA)](https://img.shields.io/nuget/v/SAEA.Sockets.svg?style=flat-square)](https://www.nuget.org/packages?q=saea)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**[English Version](README.en.md)** | **中文版**

> 基于 .NET Standard 2.0 的高性能 IOCP 网络通信框架，提供完整的网络应用解决方案。

**SAEA.Socket** 是一个 IOCP 高性能 sockets 网络框架，Src 中包含其使用场景，例如大文件传输、WebSocket 客户端和服务器、高性能消息队列、RPC、Redis 驱动、HTTP Server、MQTT、MVC、DNS、消息服务器等。

---

## 快速导航 🧭

| 章节 | 内容 |
|------|------|
| [⚡ 30秒快速开始](#30秒快速开始) | 最简单的上手方式 |
| [🧩 组件导航](#组件导航) | 选择合适的组件 |
| [📐 项目架构](#项目架构) | 整体架构设计 |
| [✨ 核心特性](#核心特性) | 主要功能亮点 |
| [📊 性能对比](#性能对比) | 与同类框架对比 |
| [❓ 常见问题](#常见问题) | FAQ 快速解答 |
| [🔗 资源链接](#资源链接) | 更多学习资源 |

---

## 30秒快速开始 ⚡

### 方式 1: 安装核心 Socket 框架

```bash
dotnet add package SAEA.Sockets
```

```csharp
// 创建 TCP 服务器（仅需5行代码）
var server = SocketFactory.CreateServerSocket(
    SocketOptionBuilder.Instance.SetSocket(SAEASocketType.Tcp).UseIocp().SetPort(39654).Build()
);
server.OnReceive += (id, data) => server.Send(id, data);
server.Start();
```

### 方式 2: 选择适合你的组件

根据你的需求选择对应组件：

| 你的需求 | 推荐组件 | 安装命令 |
|----------|----------|----------|
| Web API 开发 | **SAEA.MVC** | `dotnet add package SAEA.MVC` |
| WebSocket 实时通信 | **SAEA.WebSocket** | `dotnet add package SAEA.WebSocket` |
| P2P 直连通信 | **SAEA.P2P** | `dotnet add package SAEA.P2P` |
| Redis 缓存操作 | **SAEA.RedisSocket** | `dotnet add package SAEA.RedisSocket` |
| 微服务 RPC | **SAEA.RPC** | `dotnet add package SAEA.RPC` |
| IoT 设备通信 | **SAEA.MQTT** | `dotnet add package SAEA.MQTT` |
| 文件传输 | **SAEA.FileSocket** | `dotnet add package SAEA.FileSocket` |

---

## 组件导航 🧩

### 核心组件

| 组件 | 功能 | NuGet | 中文文档 | 英文文档 | 适用场景 |
|------|------|-------|---------|---------|----------|
| 🔌 **SAEA.Sockets** | IOCP Socket 通信框架 | [NuGet](https://www.nuget.org/packages/SAEA.Sockets) | [中文](Src/SAEA.Sockets/README.md) | [English](Src/SAEA.Sockets/README.en.md) | 游戏服务器、实时通信、IoT |
| 🔧 **SAEA.Common** | 通用工具类库 | [NuGet](https://www.nuget.org/packages/SAEA.Common) | [中文](Src/SAEA.Common/README.md) | [English](Src/SAEA.Common/README.en.md) | 序列化、缓存、加密 |

### Web 应用组件

| 组件 | 功能 | NuGet | 中文文档 | 英文文档 | 适用场景 |
|------|------|-------|---------|---------|----------|
| 🌐 **SAEA.Http** | HTTP 服务器 | [NuGet](https://www.nuget.org/packages/SAEA.Http) | [中文](Src/SAEA.Http/README.md) | [English](Src/SAEA.Http/README.en.md) | RESTful API、静态文件服务 |
| 🎨 **SAEA.MVC** | MVC Web 框架 | [NuGet](https://www.nuget.org/packages/SAEA.MVC) | [中文](Src/SAEA.MVC/README.md) | [English](Src/SAEA.MVC/README.en.md) | Web 应用、API 服务 |

### 实时通信组件

| 组件 | 功能 | NuGet | 中文文档 | 英文文档 | 适用场景 |
|------|------|-------|---------|---------|----------|
| 💬 **SAEA.WebSocket** | WebSocket 服务器/客户端 | [NuGet](https://www.nuget.org/packages/SAEA.WebSocket) | [中文](Src/SAEA.WebSocket/README.md) | [English](Src/SAEA.WebSocket/README.en.md) | 实时聊天、推送通知 |
| 🤖 **SAEA.MQTT** | MQTT 协议实现 | [NuGet](https://www.nuget.org/packages/SAEA.MQTT) | [中文](Src/SAEA.MQTT/README.md) | [English](Src/SAEA.MQTT/README.en.md) | IoT 设备、智能家居 |
| 📨 **SAEA.MessageSocket** | 消息服务器 | [NuGet](https://www.nuget.org/packages/SAEA.MessageSocket) | [中文](Src/SAEA.MessageSocket/README.md) | [English](Src/SAEA.MessageSocket/README.en.md) | 即时通讯、在线客服 |

### P2P 通信组件

| 组件 | 功能 | NuGet | 中文文档 | 英文文档 | 适用场景 |
|------|------|-------|---------|---------|----------|
| 🔗 **SAEA.P2P** | P2P 直连通信组件 | [NuGet](https://www.nuget.org/packages/SAEA.P2P) | [中文](Src/SAEA.P2P/README.md) | [English](Src/SAEA.P2P/README.en.md) | NAT 穿透、局域网发现、即时通讯、游戏对战 |

### 数据与存储组件

| 组件 | 功能 | NuGet | 中文文档 | 英文文档 | 适用场景 |
|------|------|-------|---------|---------|----------|
| 📦 **SAEA.RedisSocket** | Redis 客户端 | [NuGet](https://www.nuget.org/packages/SAEA.RedisSocket) | [中文](Src/SAEA.RedisSocket/README.md) | [English](Src/SAEA.RedisSocket/README.en.md) | Redis 缓存、分布式锁 |
| 📁 **SAEA.FileSocket** | 文件传输组件 | [NuGet](https://www.nuget.org/packages/SAEA.FileSocket) | [中文](Src/SAEA.FileSocket/README.md) | [English](Src/SAEA.FileSocket/README.en.md) | 大文件传输、断点续传 |
| 🗂️ **SAEA.FTP** | FTP 服务器/客户端 | [NuGet](https://www.nuget.org/packages/SAEA.FTP) | [中文](Src/SAEA.FTP/README.md) | [English](Src/SAEA.FTP/README.en.md) | 文件服务器、FTP 客户端 |

### 分布式系统组件

| 组件 | 功能 | NuGet | 中文文档 | 英文文档 | 适用场景 |
|------|------|-------|---------|---------|----------|
| 🔗 **SAEA.RPC** | RPC 远程调用 | [NuGet](https://www.nuget.org/packages/SAEA.RPC) | [中文](Src/SAEA.RPC/README.md) | [English](Src/SAEA.RPC/README.en.md) | 微服务通信、远程调用 |
| 📊 **SAEA.QueueSocket** | 内存消息队列 | [NuGet](https://www.nuget.org/packages/SAEA.QueueSocket) | [中文](Src/SAEA.QueueSocket/README.md) | [English](Src/SAEA.QueueSocket/README.en.md) | Pub/Sub、事件通知 |

### 网络工具组件

| 组件 | 功能 | NuGet | 中文文档 | 英文文档 | 适用场景 |
|------|------|-------|---------|---------|----------|
| 🌍 **SAEA.DNS** | DNS 服务器/客户端 | [NuGet](https://www.nuget.org/packages/SAEA.DNS) | [中文](Src/SAEA.DNS/README.md) | [English](Src/SAEA.DNS/README.en.md) | DNS 代理、域名解析 |

---

## 项目架构 📐

### SAEA 组件家族架构图

```
┌─────────────────────────────────────────────────────────────────┐
│                    SAEA 组件家族架构总览                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│   ┌───────────── 应用层组件 ─────────────┐                     │
│   │                                       │                     │
│   │  ┌─────────┐  ┌──────────┐           │                     │
│   │  │  MVC    │  │ WebSocket│           │                     │
│   │  │(Web框架)│  │(实时通信)│           │                     │
│   │  └────┬────┘  └────┬─────┘           │                     │
│   │       │            │                  │                     │
│   │  ┌────▼────┐  ┌───▼────┐  ┌─────────┐│                     │
│   │  │  RPC    │  │ MQTT   │  │ Message ││                     │
│   │  │(远程调用)│  │(IoT)  │  │ Socket  ││                     │
│   │  └────┬────┘  └───┬────┘  └────┬────┘│                     │
│   │       │           │            │      │                     │
│   │  ┌────▼────┐  ┌───▼────┐       │      │                     │
│   │  │  P2P    │  │ Redis  │       │      │                     │
│   │  │(NAT穿透)│  │(缓存)  │       │      │                     │
│   │  └────┬────┘  └───┬────┘       │      │                     │
│   │       │           │            │      │                     │
│   └─────────┼───────────┼────────────┼─────┘                     │
│             │           │            │                           │
│   ┌─────────▼───────────▼────────────▼─────┐                   │
│   │           SAEA.Sockets (核心层)         │                   │
│   │        IOCP 高性能 Socket 框架          │                   │
│   │  ┌──────────┐  ┌──────────┐  ┌──────┐ │                   │
│   │  │IocpSocket│  │StreamSock│  │UdpSock│ │                   │
│   │  │ (IOCP)   │  │(流模式)  │  │ (UDP) │ │                   │
│   │  └──────────┘  └──────────┘  └──────┘ │                   │
│   │  ┌──────────────────────────────────┐ │                   │
│   │  │  BufferPool │ Session │  Coder   │ │                   │
│   │  │  (内存池)   │(会话)  │(编解码)  │ │                   │
│   │  └──────────────────────────────────┘ │                   │
│   └──────────────────┬────────────────────┘                   │
│                      │                                          │
│   ┌──────────────────▼────────────────────┐                   │
│   │         SAEA.Common (基础层)           │                   │
│   │       通用工具类库 (序列化/缓存/加密)    │                   │
│   │  ┌────────┐  ┌───────┐  ┌───────┐   │                   │
│   │  │Serialize│  │ Cache │  │Encrypt│   │                   │
│   │  │(序列化) │  │(缓存) │  │(加密) │   │                   │
│   │  └────────┘  └───────┘  └───────┘   │                   │
│   └───────────────────────────────────────┘                   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 数据流架构图

```
客户端请求流程:

┌─────────┐      ┌──────────┐      ┌──────────┐
│ 客户端  │─────►│ Sockets  │─────►│  Coder   │
│ (TCP/UDP)│      │ IOCP接收 │      │  解码    │
└─────────┘      └──────────┘      └──────────┘
                                         │
                                         ▼
                    ┌────────────────────────────────┐
                    │    应用层处理（业务逻辑）        │
                    │  MVC / RPC / WebSocket / MQTT  │
                    └────────────────────────────────┘
                                         │
                                         ▼
                    ┌──────────┐      ┌──────────┐
                    │  Coder   │─────►│ Sockets  │─────► 客户端
                    │  编码    │      │ IOCP发送 │
                    └──────────┘      └──────────┘
```

---

## 核心特性 ✨

| 特性 | 说明 |
|------|------|
| 🚀 **高性能 IOCP** | Windows 完成端口技术，支持万级并发连接 |
| 📦 **内存池优化** | BufferManager、UserTokenPool 减少 GC 压力 |
| 🔒 **SSL/TLS 加密** | Stream 模式支持安全连接 |
| 🌐 **IPv6 支持** | 完全兼容 IPv6 协议 |
| 🔄 **会话管理** | SessionManager 自动管理连接会话 |
| 🛠️ **自定义协议** | ICoder 接口支持灵活的协议编解码 |
| 🔗 **链式配置** | SocketOptionBuilder 简洁易用 |
| 📝 **完整组件** | MVC、WebSocket、RPC、Redis、MQTT 等全场景覆盖 |

---

## 性能对比 📊

### SAEA.Sockets vs 传统 Socket

| 指标 | SAEA.Sockets | 传统 Socket | 提升 |
|------|--------------|-------------|------|
| **并发连接数** | 10,000+ | ~1,000 | **10倍** |
| **CPU 利用率** | ~85% | ~30% | **高效** |
| **内存占用** | 池化复用 | 频繁分配 | **低 GC** |
| **延迟** | ~1ms | ~10ms | **低延迟** |

### SAEA.RPC vs HTTP RPC

| 特性 | SAEA.RPC | HTTP RPC |
|------|----------|----------|
| **序列化** | Protobuf | JSON |
| **传输协议** | TCP | HTTP |
| **连接模式** | 长连接 | 短连接 |
| **并发模型** | IOCP | 阻塞 |
| **性能** | **高** | 中 |

---

## 常见问题 ❓

### Q1: SAEA 适合什么项目？

**A**: SAEA 适合以下场景：
- 🎮 游戏服务器（实时对战、状态同步）
- 📊 实时数据推送（股票行情、体育比分）
- 🤖 IoT 设备通信（传感器数据上报）
- 💬 即时通讯（私聊、群聊、客服系统）
- 🔗 微服务通信（RPC 远程调用）
- 📁 大文件传输（断点续传、高速传输）
- 🌐 P2P 直连通信（NAT 穿透、局域网发现）

### Q2: 如何选择合适的组件？

**A**: 根据你的需求选择：

```
你的需求                      推荐组件
─────────────────────────────────────
Web API 开发              →  SAEA.MVC
实时聊天/推送             →  SAEA.WebSocket
P2P 直连通信              →  SAEA.P2P
IoT 设备通信              →  SAEA.MQTT
Redis 缓存操作            →  SAEA.RedisSocket
微服务 RPC                →  SAEA.RPC
文件传输                  →  SAEA.FileSocket
自定义协议                →  SAEA.Sockets
```

### Q3: 是否支持跨平台？

**A**: 基于 .NET Standard 2.0，支持：
- ✅ Windows（IOCP 最佳性能）
- ✅ Linux（异步 Socket）
- ✅ macOS

### Q4: NuGet 如何安装？

**A**: 搜索 "SAEA" 或直接安装：

```bash
# Package Manager
Install-Package SAEA.Sockets -Version 7.26.2.2

# .NET CLI
dotnet add package SAEA.Sockets --version 7.26.2.2
```

[NuGet 地址](https://www.nuget.org/packages?q=saea)

### Q5: 如何快速上手？

**A**: 3 步快速开始：
1. 安装对应组件的 NuGet 包
2. 阅读组件 README 的 "30秒快速开始"
3. 查看完整示例代码

每个组件都有详细的使用示例和注释。

### Q6: 是否有示例项目？

**A**: Src 中包含多个测试项目，展示实际使用场景：
- `SAEA.SocketsTest` - Socket 使用示例
- `SAEA.WebSocketTest` - WebSocket 示例
- `SAEA.MVCTest` - MVC Web 应用示例
- `SAEA.RedisTest` - Redis 操作示例
- `SAEA.RPCTest` - RPC 调用示例
- `SAEA.P2PTest` - P2P NAT 穿透示例

---

## 实例截图 🖼️

<img src="https://github.com/yswenli/SAEA/blob/master/FileSocketTest.png?raw=true" width="400"/>
<img src="https://github.com/yswenli/SAEA/blob/master/QueueSocketTest.png?raw=true" width="400"/>
<img src="https://github.com/yswenli/SAEA/blob/master/SAEA.MVC.png?raw=true" width="400"/>
<img src="https://github.com/yswenli/SAEA/blob/master/SAEA.MVCTest.png?raw=true" width="400"/>
<img src="https://github.com/yswenli/SAEA/blob/master/SAEA.RedisTest.png?raw=true" width="400"/>
<img src="https://github.com/yswenli/SAEA/blob/master/SAEA.WebAPITest.png?raw=true" width="400"/>
<img src="https://github.com/yswenli/SAEA/blob/master/WebsocketTest.png?raw=true" width="400"/>
<img src="https://github.com/yswenli/SAEA/blob/master/redis%20cluster%20test.png?raw=true" width="400"/>
<img src="https://github.com/yswenli/SAEA/blob/master/rpc.png?raw=true" width="400"/>

---

## 资源链接 🔗

### 官方资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包列表](https://www.nuget.org/packages?q=saea)
- [作者博客](https://www.cnblogs.com/yswenli/)

### 相关项目

- [WebRedisManager](https://github.com/yswenli/WebRedisManager) - 基于 SAEA.RedisSocket 的 Redis 管理工具
- [SAEA.Rested](https://github.com/yswenli/SAEA.Rested) - 基于 SAEA.MVC 的 REST API 示例
- [GFF](https://github.com/yswenli/GFF) - 基于 SAEA 的仿 QQ 通信程序

### 技术支持

- QQ 群：788260487
- Issues：[GitHub Issues](https://github.com/yswenli/SAEA/issues)

---

## 许可证

Apache License 2.0