# SAEA.WebSocket - 高性能 WebSocket 服务器/客户端

[![NuGet version](https://img.shields.io/nuget/v/SAEA.WebSocket.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.WebSocket)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

## 简介

SAEA.WebSocket 是一个基于 SAEA.Sockets 完整实现 RFC 6455 WebSocket 协议的高性能组件，提供 WebSocket 服务器和客户端功能。采用 IOCP 异步模型，支持万级并发连接，适用于实时通信、推送通知、在线聊天等场景。

## 特性

- **RFC 6455 完整实现** - 标准 WebSocket 协议
- **IOCP 高性能** - 支持万级并发连接
- **SSL/TLS 支持** - WSS 安全连接
- **多种帧类型** - Text、Binary、Ping/Pong、Close
- **心跳机制** - 内置 Ping/Pong 心跳
- **子协议协商** - 支持自定义子协议
- **批处理优化** - ClassificationBatcher 读写批处理

## NuGet 安装

```bash
# Package Manager
Install-Package SAEA.WebSocket -Version 7.26.2.2

# .NET CLI
dotnet add package SAEA.WebSocket --version 7.26.2.2
```

## 核心类

| 类名 | 说明 |
|------|------|
| `WSServer` | WebSocket 服务器 |
| `WSClient` | WebSocket 客户端 |
| `WSProtocal` | WebSocket 协议帧封装 |
| `WSProtocalType` | 协议类型枚举（Text/Binary/Ping/Pong/Close） |
| `WSCoder` | 消息编解码器 |

## 快速使用

### WebSocket 服务器

```csharp
using SAEA.WebSocket;
using SAEA.WebSocket.Model;

// 创建 WebSocket 服务器
var server = new WSServer(port: 39654);

// 注册事件
server.OnConnected += (id) => 
{
    Console.WriteLine($"客户端连接: {id}");
};

server.OnMessage += (id, data) => 
{
    var msg = Encoding.UTF8.GetString(data.Content);
    Console.WriteLine($"收到消息: {msg}");
    
    // 回复客户端
    server.Reply(id, data);
};

server.OnDisconnected += (id) => 
{
    Console.WriteLine($"客户端断开: {id}");
};

// 启动服务器
server.Start();
Console.WriteLine("WebSocket 服务器已启动，端口: 39654");
```

### WSS 安全服务器

```csharp
using SAEA.WebSocket;

// SSL/TLS 安全 WebSocket 服务器
var server = new WSServer(port: 443, useSSL: true);
server.SetCertificate("server.pfx", "password");
server.Start();
```

### WebSocket 客户端

```csharp
using SAEA.WebSocket;
using System.Text;

// 创建 WebSocket 客户端
var client = new WSClient("ws://127.0.0.1:39654");

// 注册事件
client.OnPong += () => Console.WriteLine("收到 Pong 响应");

client.OnMessage += (data) => 
{
    Console.WriteLine($"收到消息: {Encoding.UTF8.GetString(data.Content)}");
};

client.OnError += (ex) => Console.WriteLine($"错误: {ex.Message}");

client.OnDisconnected += () => Console.WriteLine("连接断开");

// 连接服务器
client.Connect();

// 发送文本消息
client.Send("Hello WebSocket!");

// 发送二进制消息
client.Send(new byte[] { 0x01, 0x02, 0x03 });

// 发送心跳
client.Ping();

// 关闭连接
client.Close();
```

### URL 格式

```csharp
// 支持多种 URL 格式
var client1 = new WSClient("ws://127.0.0.1:39654");
var client2 = new WSClient("wss://secure.example.com:443");  // SSL
var client3 = new WSClient("127.0.0.1", 39654);  // IP + 端口
var client4 = new WSClient(new Uri("ws://example.com/ws"));
```

### 子协议协商

```csharp
using SAEA.WebSocket;

// 指定子协议
var client = new WSClient("ws://127.0.0.1:39654", subProtocol: "chat");
```

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

## 应用场景

- **实时聊天** - 即时通讯、在线客服
- **推送通知** - 系统消息、订单通知
- **实时数据** - 股票行情、体育比分
- **协作工具** - 在线编辑、白板共享
- **游戏通信** - 实时对战、状态同步
- **IoT 设备** - 设备监控、远程控制

## 依赖项

| 包名 | 版本 | 说明 |
|------|------|------|
| SAEA.Sockets | 7.26.2.2 | IOCP 通信框架 |
| SAEA.Common | 7.26.2.2 | 公共工具类 |

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包](https://www.nuget.org/packages/SAEA.WebSocket)
- [作者博客](https://www.cnblogs.com/yswenli/)

## 许可证

Apache License 2.0