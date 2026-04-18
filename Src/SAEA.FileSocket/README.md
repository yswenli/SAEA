# SAEA.FileSocket - 高性能文件传输组件

[![NuGet version](https://img.shields.io/nuget/v/SAEA.FileSocket.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.FileSocket)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

## 简介

SAEA.FileSocket 是一个基于 SAEA.Sockets 的高性能文件传输组件，采用 IOCP 完成端口技术实现 TCP 文件传输。支持超大文件分块传输、断点续传、实时进度监控，适用于文件共享、数据同步、备份传输等场景。

## 特性

- **IOCP 高性能** - 基于 SAEA.Sockets 完成端口技术
- **大文件支持** - 分块传输，支持超大文件
- **断点续传** - offset 参数支持从指定位置续传
- **实时进度** - 发送/接收速度、数据量统计
- **心跳保活** - 10秒心跳间隔保持连接
- **事件驱动** - 完善的事件回调机制
- **传输协议** - 请求/允许/拒绝/数据流 四阶段握手

## NuGet 安装

```bash
# Package Manager
Install-Package SAEA.FileSocket -Version 7.26.2.2

# .NET CLI
dotnet add package SAEA.FileSocket --version 7.26.2.2
```

## 核心类

| 类名 | 说明 |
|------|------|
| `FileTransfer` | 文件传输主协调类 |
| `Client` | 文件发送客户端 |
| `Server` | 文件接收服务器 |
| `FileMessage` | 文件消息数据模型 |

## 快速使用

### 文件接收服务器

```csharp
using SAEA.FileSocket;

// 创建文件接收目录
var receivePath = @"C:\ReceivedFiles";

// 创建文件传输实例（既是服务器也是发送端）
var fileTransfer = new FileTransfer(receivePath, port: 39654);

// 注册事件
fileTransfer.OnReceiveEnd += (filePath) => 
{
    Console.WriteLine($"文件接收完成: {filePath}");
};

fileTransfer.OnDisplay += (info) => 
{
    Console.WriteLine($"传输状态: {info}");
};

// 启动服务器监听
fileTransfer.Start();

Console.WriteLine("文件接收服务器已启动，端口: 39654");
```

### 发送文件

```csharp
using SAEA.FileSocket;

var fileTransfer = new FileTransfer(@"C:\ReceivedFiles", port: 39654);
fileTransfer.Start();

// 发送文件到指定 IP
var targetIp = "192.168.1.100";
var fileName = @"C:\Data\large_video.mp4";

fileTransfer.SendFile(fileName, targetIp);

// OnReceiveEnd 和 OnDisplay 会显示传输进度和结果
```

### 带进度监控的传输

```csharp
using SAEA.FileSocket;

var fileTransfer = new FileTransfer(@"C:\ReceivedFiles", bufferSize: 100 * 1024);

// 监控传输进度
fileTransfer.OnDisplay += (info) => 
{
    // info 包含发送速度和接收速度信息
    Console.WriteLine(info);
};

// 接收完成
fileTransfer.OnReceiveEnd += (filePath) => 
{
    Console.WriteLine($"文件已保存: {filePath}");
    // 可以处理接收完成的文件
};

fileTransfer.Start();
fileTransfer.SendFile(@"C:\Data\backup.zip", "192.168.1.100");
```

### Server 端自定义处理

```csharp
using SAEA.FileSocket;
using SAEA.Sockets.Interface;

var server = new Server(port: 39654, bufferSize: 100 * 1024);

// 收到文件传输请求
server.OnRequested += (ID, fileName, length) => 
{
    Console.WriteLine($"请求接收文件: {fileName}, 大小: {length / 1024 / 1024}MB");
    // 返回 true 允许接收，false 拒绝接收
    return true;
};

// 收到文件数据块
server.OnFile += (userToken, content) => 
{
    // 可以自定义处理接收的数据块
};

// 发生错误
server.OnError += (ID, ex) => 
{
    Console.WriteLine($"传输错误: {ex.Message}");
};

server.Start();
```

### Client 端发送

```csharp
using SAEA.FileSocket;

var client = new Client(bufferSize: 100 * 1024);

// 连接接收端
client.Connect("192.168.1.100", 39654);

// 发送文件
client.SendFile(@"C:\Data\document.pdf", (success) => 
{
    if (success)
        Console.WriteLine("发送成功");
    else
        Console.WriteLine("发送失败");
});

// 断点续传（从指定位置继续）
long offset = 1024 * 1024 * 100;  // 从 100MB 处继续
client.SendFile(@"C:\Data\large_file.dat", offset, (success) => 
{
    Console.WriteLine($"续传结果: {success}");
});
```

### 自定义缓冲区大小

```csharp
using SAEA.FileSocket;

// 默认缓冲区 100KB，可自定义
var fileTransfer = new FileTransfer(
    filePath: @"C:\ReceivedFiles",
    port: 39654,
    bufferSize: 1024 * 1024  // 1MB 缓冲区
);

// 更大的缓冲区适合超大文件传输
// 更小的缓冲区适合网络不稳定情况
```

## 传输协议流程

```
发送端 → 接收端: 请求发送文件 (FileName, Length)
接收端 → 发送端: 允许接收 (AllowReceive) 或 拒绝接收 (RefuseReceive)
发送端 → 接收端: 分块发送数据流 (Data chunks)
接收端: 写入文件，累计已接收大小
接收端: 接收完成，触发 OnReceiveEnd
```

## 断点续传

当传输中断时，可以使用 `offset` 参数从指定位置继续传输：

```csharp
// 记录已传输的位置
long lastOffset = GetLastOffset(fileName);

// 从断点继续发送
client.SendFile(fileName, lastOffset, (success) => 
{
    if (success)
        ClearOffset(fileName);  // 清除断点记录
});
```

## 分块传输机制

- **默认块大小**: 100KB (`bufferSize = 100 * 1024`)
- **可自定义**: 构造函数参数可调整
- **异步发送**: 使用 `SendAsync` 异步发送数据块
- **线程安全**: 使用 `Interlocked` 线程安全计数

## 应用场景

- **文件共享** - 局域网文件快速传输
- **数据备份** - 大文件备份传输
- **同步工具** - 文件同步分发
- **远程传输** - 跨服务器数据迁移
- **视频传输** - 大视频文件传输

## 依赖项

| 包名 | 版本 | 说明 |
|------|------|------|
| SAEA.Sockets | 7.26.2.2 | IOCP 通信框架 |
| SAEA.Common | 7.26.2.2 | 公共工具类 |

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包](https://www.nuget.org/packages/SAEA.FileSocket)
- [作者博客](https://www.cnblogs.com/yswenli/)

## 许可证

Apache License 2.0