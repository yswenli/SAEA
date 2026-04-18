# SAEA.Sockets - 高性能 IOCP Socket 通信框架

[![NuGet version](https://img.shields.io/nuget/v/SAEA.Sockets.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.Sockets)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

## 简介

SAEA.Sockets 是一个基于 .NET Standard 2.0 的高性能 Socket 通信框架，采用 **IOCP (I/O Completion Port)** 完成端口技术实现异步网络通信。它是 SAEA 项目家族的核心基础组件，为所有上层网络应用提供高性能的底层通信支持。

框架设计简洁优雅，支持 TCP、UDP、SSL/TLS 等多种通信模式，提供灵活的协议编码器扩展机制，是构建高性能网络应用的理想选择。

## 特性

- **IOCP 高性能** - 使用 Windows 完成端口技术，支持万级并发连接
- **TCP 流模式** - 支持 SSL/TLS 安全加密连接
- **UDP 模式** - 支持广播和组播
- **IPv6 支持** - 完全支持 IPv6 协议
- **对象池化** - BufferManager、UserTokenPool、SocketAsyncEventArgsPool 内存池优化
- **会话管理** - SessionManager 管理连接会话，支持超时自动清理
- **可扩展协议** - ICoder 接口支持自定义协议编解码
- **Builder 配置** - SocketOptionBuilder 链式配置，使用简便

## NuGet 安装

```bash
# Package Manager
Install-Package SAEA.Sockets -Version 7.26.2.2

# .NET CLI
dotnet add package SAEA.Sockets --version 7.26.2.2
```

## 核心类

| 类名 | 说明 |
|------|------|
| `IocpServerSocket` / `IocpClientSocket` | IOCP 模式 TCP 服务器/客户端 |
| `StreamServerSocket` / `StreamClientSocket` | 流模式 TCP 服务器/客户端（支持 SSL） |
| `UdpServerSocket` / `UdpClientSocket` | UDP 服务器/客户端 |
| `SocketOptionBuilder` | 链式配置构建器 |
| `SocketFactory` | Socket 工厂类 |
| `SessionManager` | 会话管理器 |
| `BaseCoder` | 默认协议编码器（8字节长度 + 1字节类型 + 内容） |

## 快速使用

### TCP 服务器（IOCP 模式）

```csharp
using SAEA.Sockets;
using SAEA.Sockets.Handler;

// 使用 Builder 配置服务器
var option = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)
    .UseIocp()
    .SetIP("127.0.0.1")
    .SetPort(39654)
    .SetBufferSize(1024 * 64)
    .SetCount(1000)
    .Build();

var server = SocketFactory.CreateServerSocket(option);

// 注册事件
server.OnAccepted += (id) => Console.WriteLine($"客户端连接: {id}");
server.OnReceive += (id, data) => 
{
    Console.WriteLine($"收到数据: {Encoding.UTF8.GetString(data)}");
    server.Send(id, data); // 回复客户端
};
server.OnDisconnected += (id) => Console.WriteLine($"客户端断开: {id}");

server.Start();
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

client.OnReceive += (data) => Console.WriteLine($"收到: {Encoding.UTF8.GetString(data)}");
client.OnDisconnected += () => Console.WriteLine("连接断开");

client.Connect();
client.SendAsync(Encoding.UTF8.GetBytes("Hello SAEA!"));
```

### UDP 服务器

```csharp
using SAEA.Sockets;

var option = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Udp)
    .SetIP("127.0.0.1")
    .SetPort(39655)
    .Build();

var server = SocketFactory.CreateServerSocket(option);
server.OnReceive += (id, data) => server.Send(id, data);
server.Start();
```

### SSL/TLS 安全连接

```csharp
using SAEA.Sockets;

var option = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)
    .UseStream()
    .SetIP("127.0.0.1")
    .SetPort(443)
    .UseSsl()  // 启用 SSL
    .SetSslCertificate("server.pfx", "password")
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
        return base.Decode(data);
    }

    public override byte[] Encode(byte[] data)
    {
        // 自定义编码逻辑
        return base.Encode(data);
    }
}

// 使用自定义编码器
public class MyContext : BaseContext<MyCoder>
{
    public MyContext(BaseUserToken userToken) : base(userToken) { }
}
```

## 默认协议格式

`BaseCoder` 实现的默认消息协议：

```
| 8字节长度 | 1字节类型 | N字节内容 |
```

## 依赖项

| 包名 | 版本 | 说明 |
|------|------|------|
| SAEA.Common | 7.26.2.2 | 通用工具类库 |
| Pipelines.Sockets.Unofficial | 2.2.8 | Pipeline Socket 扩展 |
| System.IO.Pipelines | 10.0.2 | 高性能 IO 管道 |

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包](https://www.nuget.org/packages/SAEA.Sockets)
- [作者博客](https://www.cnblogs.com/yswenli/)

## 许可证

Apache License 2.0