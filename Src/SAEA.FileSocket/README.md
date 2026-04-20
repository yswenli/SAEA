# SAEA.FileSocket - 高性能文件传输组件 📁

[![NuGet version](https://img.shields.io/nuget/v/SAEA.FileSocket.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.FileSocket)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**[English Version](README.en.md)** | **中文版**

> 基于 SAEA.Sockets 的高性能文件传输组件，采用 IOCP 完成端口技术，支持大文件分块传输、断点续传、实时进度监控。

## 快速导航 🧭

| 章节 | 内容 |
|------|------|
| [⚡ 30秒快速开始](#30秒快速开始) | 最简单的上手示例 |
| [🎯 核心特性](#核心特性) | 组件的主要功能 |
| [📐 架构设计](#架构设计) | 传输流程与协议 |
| [💡 应用场景](#应用场景) | 何时选择 SAEA.FileSocket |
| [📊 性能对比](#性能对比) | 传输性能数据 |
| [❓ 常见问题](#常见问题) | FAQ 快速解答 |
| [🔧 核心类](#核心类) | 主要类一览 |
| [📝 使用示例](#使用示例) | 详细代码示例 |

---

## 30秒快速开始 ⚡

最快上手方式，只需3步即可实现文件传输：

### Step 1: 安装 NuGet 包

```bash
dotnet add package SAEA.FileSocket
```

### Step 2: 创建文件接收服务器（仅需6行代码）

```csharp
using SAEA.FileSocket;

var fileTransfer = new FileTransfer(@"C:\ReceivedFiles", port: 39654);
fileTransfer.OnReceiveEnd += (path) => Console.WriteLine($"接收完成: {path}");
fileTransfer.Start();
```

### Step 3: 发送文件

```csharp
fileTransfer.SendFile(@"C:\Data\video.mp4", "192.168.1.100");
```

**就这么简单！** 🎉 你已经实现了一个支持大文件、断点续传的高性能文件传输系统。

---

## 核心特性 🎯

| 特性 | 说明 | 优势 |
|------|------|------|
| 🚀 **IOCP 高性能** | 基于 SAEA.Sockets 完成端口技术 | 支持高并发文件传输 |
| 📦 **大文件支持** | 分块传输机制 | 支持 TB 级超大文件 |
| 🔄 **断点续传** | offset 参数从指定位置续传 | 网络中断可恢复 |
| 📊 **实时进度** | 发送/接收速度、数据量统计 | 可视化监控传输状态 |
| 💓 **心跳保活** | 10秒心跳间隔保持连接 | 自动检测连接状态 |
| 📡 **事件驱动** | 完善的事件回调机制 | 灵活处理传输状态 |
| 🤝 **四阶段握手** | 请求/允许/拒绝/数据流协议 | 安全可靠的传输控制 |

---

## 架构设计 📐

### 组件架构图

```
┌─────────────────────────────────────────────────────────────┐
│                   SAEA.FileSocket 架构                       │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │                   FileTransfer                         │  │
│  │              (文件传输协调主类)                        │  │
│  └───────────────────────┬──────────────────────────────┘  │
│                          │                                  │
│           ┌──────────────┴──────────────┐                  │
│           │                             │                   │
│  ┌────────▼────────┐          ┌────────▼────────┐        │
│  │      Client     │          │      Server      │        │
│  │    (发送端)     │          │    (接收端)      │        │
│  └────────┬────────┘          └────────┬────────┘        │
│           │                             │                   │
│           │     ┌───────────────┐       │                   │
│           └────►│ FileMessage   │◄──────┘                  │
│                 │  (消息模型)   │                           │
│                 └───────┬───────┘                          │
│                         │                                   │
│              ┌──────────┴──────────┐                       │
│              │                     │                        │
│       ┌──────▼──────┐      ┌───────▼──────┐               │
│       │  Request    │      │   Chunk      │               │
│       │  (请求)     │      │   (数据块)   │               │
│       └─────────────┘      └──────────────┘               │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐  │
│  │              SAEA.Sockets (IOCP底层)                 │  │
│  └─────────────────────────────────────────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 文件传输流程图

```
文件传输完整流程:

发送端                                    接收端
  │                                         │
  │  1. SendFile(fileName, targetIp)        │
  │────────────────────────────────────────►│
  │                                         │
  │  2. 请求发送 (RequestMessage)            │
  │     ├── FileName                        │
  │     └── FileLength                      │
  │────────────────────────────────────────►│
  │                                         │
  │                              3. OnRequested 事件
  │                                 ├── 返回 true (允许)
  │                                 └── 返回 false (拒绝)
  │                                         │
  │  4. 允许接收 (AllowReceive)             │
  │◄────────────────────────────────────────│
  │                                         │
  │  5. 分块发送数据流                        │
  │     ┌─────────────────────────────────┐ │
  │     │ Chunk 1 (100KB)                 │ │
  │     │────────────────────────────────►│ │
  │     │ Chunk 2 (100KB)                 │ │
  │     │────────────────────────────────►│ │
  │     │ Chunk 3 (100KB)                 │ │
  │     │────────────────────────────────►│ │
  │     │ ...                             │ │
  │     │ Chunk N (剩余部分)              │ │
  │     │────────────────────────────────►│ │
  │     └─────────────────────────────────┘ │
  │                                         │
  │                              6. 写入文件 (File.WriteAllText)
  │                                         │
  │                              7. OnReceiveEnd 事件
  │                                 └── 触发接收完成回调
  │                                         │
  │  8. 发送完成回调                          │
  │◄────────────────────────────────────────│
  │                                         │

断点续传流程:

  │  SendFile(fileName, offset)             │
  │────────────────────────────────────────►│
  │  从 offset 位置继续发送                   │
  │  ├── 跳过已传输部分                       │
  │  └── 只发送剩余数据                       │
  │────────────────────────────────────────►│
```

### 传输协议详解

```
┌─────────────────────────────────────────────────────────────┐
│                    四阶段握手协议                            │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  阶段1: 请求 (RequestMessage)                               │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ 消息类型: Request                                    │   │
│  │ 内容: FileName + FileLength                         │   │
│  │ 说明: 发送端请求发送文件                              │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  阶段2: 响应 (ResponseMessage)                              │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ AllowReceive: 允许接收                               │   │
│  │ RefuseReceive: 拒绝接收                              │   │
│  │ 说明: 接收端决定是否接收                              │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  阶段3: 数据流 (DataMessage)                                │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ 分块大小: 默认 100KB                                  │   │
│  │ 发送方式: SendAsync 异步发送                          │   │
│  │ 说明: 分块传输文件内容                                │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  阶段4: 完成 (Complete)                                     │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ 触发: OnReceiveEnd 事件                              │   │
│  │ 动作: 保存文件、清理资源                              │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## 应用场景 💡

### ✅ 适合使用 SAEA.FileSocket 的场景

| 场景 | 描述 | 推荐理由 |
|------|------|----------|
| 📁 **局域网文件共享** | 快速传输大文件 | IOCP 高性能，百MB/s 传输速度 |
| 💾 **数据备份** | 定时备份大数据 | 断点续传保证可靠性 |
| 🔄 **文件同步** | 多服务器文件同步 | 分块传输，支持大文件 |
| 🎬 **视频传输** | 大视频文件传输 | 支持 TB 级文件 |
| 🏢 **企业内部传输** | 安全内网文件分发 | 私有协议，可控安全 |
| ☁️ **云端数据迁移** | 跨服务器数据迁移 | 高吞吐量，进度可监控 |

### ❌ 不适合的场景

| 场景 | 推荐替代方案 |
|------|--------------|
| 需要加密传输 | 使用 SAEA.Sockets + SSL |
| 跨互联网传输 | 使用 HTTPS 或 FTPS |
| 需要权限验证 | 自行添加认证层 |

---

## 性能对比 📊

### 传输性能数据

| 指标 | SAEA.FileSocket | 传统 HTTP 上传 | 优势 |
|------|-----------------|----------------|------|
| **传输速度** | ~100MB/s (局域网) | ~30MB/s | **3倍提升** |
| **最大文件** | 支持 TB 级 | 受内存限制 | **超大文件** |
| **断点续传** | ✅ 原生支持 | ❌ 需要额外实现 | **可靠性强** |
| **内存占用** | 低 (分块传输) | 高 (全量缓存) | **内存友好** |
| **并发传输** | 支持 | 需要多线程 | **高并发** |

### 分块传输效率

| 文件大小 | 默认块大小 (100KB) | 推荐块大小 | 传输耗时 (1Gbps) |
|----------|-------------------|------------|------------------|
| 100MB | 1000 块 | 100KB | ~1 秒 |
| 1GB | 10000 块 | 1MB | ~10 秒 |
| 10GB | 100000 块 | 10MB | ~100 秒 |
| 100GB | 1000000 块 | 10MB | ~17 分钟 |

> 💡 **提示**: 可根据网络环境和文件大小调整 `bufferSize` 参数优化传输效率。

### 与其他方案对比

| 方案 | 断点续传 | 进度监控 | 大文件支持 | 实现复杂度 |
|------|----------|----------|------------|------------|
| **SAEA.FileSocket** | ✅ | ✅ | ✅ TB级 | ⭐ 简单 |
| FTP | ✅ | ❌ | ✅ | ⭐⭐ 中等 |
| HTTP 上传 | ❌ | ✅ | ❌ 内存限制 | ⭐⭐ 中等 |
| 自定义 Socket | 需实现 | 需实现 | 需实现 | ⭐⭐⭐ 复杂 |

---

## 常见问题 ❓

### Q1: 如何实现断点续传？

**A**: 使用 `offset` 参数从指定位置继续传输：

```csharp
// 记录已传输的位置
long lastOffset = GetLastOffset(fileName);

// 从断点继续发送
client.SendFile(fileName, lastOffset, (success) => 
{
    if (success)
        Console.WriteLine("续传成功");
});

// 接收端会从 offset 位置继续写入
```

### Q2: 如何调整传输块大小？

**A**: 在构造函数中设置 `bufferSize` 参数：

```csharp
// 小文件或网络不稳定：使用较小块
var fileTransfer = new FileTransfer(path, port, bufferSize: 50 * 1024);

// 大文件或高速网络：使用较大块
var fileTransfer = new FileTransfer(path, port, bufferSize: 1024 * 1024);  // 1MB
```

### Q3: 如何监控传输进度？

**A**: 使用 `OnDisplay` 事件：

```csharp
fileTransfer.OnDisplay += (info) => 
{
    Console.WriteLine(info);  // 包含发送/接收速度
};

fileTransfer.OnReceiveEnd += (filePath) => 
{
    Console.WriteLine($"接收完成: {filePath}");
};
```

### Q4: 如何拒绝接收特定文件？

**A**: 使用 `Server` 类的 `OnRequested` 事件：

```csharp
var server = new Server(port: 39654);

server.OnRequested += (ID, fileName, length) => 
{
    // 根据文件名或大小决定是否接收
    if (length > 1024 * 1024 * 1024)  // 超过 1GB
    {
        Console.WriteLine($"拒绝接收大文件: {fileName}");
        return false;  // 拒绝
    }
    return true;  // 允许
};

server.Start();
```

### Q5: 支持同时传输多个文件吗？

**A**: 支持。创建多个 `Client` 实例或使用同一个 `FileTransfer` 发送多个文件：

```csharp
// 方式1: 使用 FileTransfer 顺序发送
fileTransfer.SendFile(file1, targetIp);
fileTransfer.SendFile(file2, targetIp);

// 方式2: 创建多个 Client 并发发送
var client1 = new Client();
var client2 = new Client();
client1.Connect(ip, port);
client2.Connect(ip, port);
client1.SendFile(file1, callback);
client2.SendFile(file2, callback);
```

### Q6: 传输中断后如何恢复？

**A**: FileSocket 支持断点续传，只需记录已传输的字节数：

```csharp
// 1. 定期保存已传输位置
long savedOffset = GetTransferredBytes(fileName);
SaveProgress(fileName, savedOffset);

// 2. 中断后从保存位置恢复
long lastOffset = LoadProgress(fileName);
client.SendFile(fileName, lastOffset, callback);
```

### Q7: 如何自定义接收路径？

**A**: 在 `FileTransfer` 或 `Server` 构造时指定：

```csharp
// FileTransfer 方式
var fileTransfer = new FileTransfer(@"C:\CustomPath", port: 39654);

// Server 方式
var server = new Server(port: 39654, filePath: @"C:\CustomPath");
```

---

## 核心类 🔧

| 类名 | 说明 |
|------|------|
| `FileTransfer` | 文件传输主协调类，同时支持发送和接收 |
| `Client` | 文件发送客户端，用于主动发送文件 |
| `Server` | 文件接收服务器，用于监听和接收文件 |
| `FileMessage` | 文件消息数据模型，定义传输协议 |
| `FileMessageCoder` | 文件消息编解码器 |

---

## 使用示例 📝

### 基础文件传输

```csharp
using SAEA.FileSocket;

var fileTransfer = new FileTransfer(@"C:\ReceivedFiles", port: 39654);

fileTransfer.OnReceiveEnd += (filePath) => 
    Console.WriteLine($"文件接收完成: {filePath}");

fileTransfer.OnDisplay += (info) => 
    Console.WriteLine($"传输状态: {info}");

fileTransfer.Start();
fileTransfer.SendFile(@"C:\Data\document.pdf", "192.168.1.100");
```

### Server 端自定义处理

```csharp
using SAEA.FileSocket;

var server = new Server(port: 39654, bufferSize: 100 * 1024);

server.OnRequested += (ID, fileName, length) => 
{
    Console.WriteLine($"请求接收文件: {fileName}, 大小: {length / 1024 / 1024}MB");
    return true;
};

server.OnFile += (userToken, content) => 
{
    // 自定义处理接收的数据块
};

server.OnError += (ID, ex) => 
    Console.WriteLine($"传输错误: {ex.Message}");

server.Start();
```

### Client 端发送（带回调）

```csharp
using SAEA.FileSocket;

var client = new Client(bufferSize: 100 * 1024);

client.Connect("192.168.1.100", 39654);

client.SendFile(@"C:\Data\document.pdf", (success) => 
{
    if (success)
        Console.WriteLine("发送成功");
    else
        Console.WriteLine("发送失败");
});

// 断点续传
long offset = 1024 * 1024 * 100;
client.SendFile(@"C:\Data\large_file.dat", offset, (success) => 
    Console.WriteLine($"续传结果: {success}"));
```

### 自定义缓冲区大小

```csharp
using SAEA.FileSocket;

// 小缓冲区：网络不稳定场景
var smallBuffer = new FileTransfer(path, port, bufferSize: 50 * 1024);

// 大缓冲区：高速网络大文件场景
var largeBuffer = new FileTransfer(path, port, bufferSize: 1024 * 1024);
```

---

## 分块传输机制

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `bufferSize` | 100KB | 每次传输的数据块大小 |
| `heartSpan` | 10秒 | 心跳保活间隔 |
| 发送方式 | `SendAsync` | 异步发送数据块 |
| 线程安全 | `Interlocked` | 线程安全计数 |

---

## 传输协议格式

FileSocket 使用四阶段握手协议：

```
┌──────────────┐     Request      ┌──────────────┐
│    发送端    │ ────────────────► │    接收端    │
│   (Client)   │                   │   (Server)   │
└──────────────┘                   └──────────────┘
       │                                  │
       │◄───── Allow/Refuse ─────────────│
       │                                  │
       │═══════ Data Chunks ═════════════►│
       │                                  │
       │◄───── Complete ──────────────────│
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
- [NuGet 包](https://www.nuget.org/packages/SAEA.FileSocket)
- [作者博客](https://www.cnblogs.com/yswenli/)

---

## 许可证

Apache License 2.0