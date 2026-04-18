# SAEA.RPC - 高性能远程过程调用框架

[![NuGet version](https://img.shields.io/nuget/v/SAEA.RPC.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.RPC)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

## 简介

SAEA.RPC 是一个轻量级、高性能的 RPC（远程过程调用）框架，基于 SAEA.Sockets 的 IOCP 技术实现。只需几行代码，就能实现服务的远程调用，性能远超同类产品。

框架采用简洁的设计理念：
- **服务端**：用 `[RPCService]` 特性标记服务类，启动 `ServiceProvider` 即可
- **客户端**：使用 `RPCServiceProxy` 动态生成的代理类，像调用本地方法一样调用远程服务

## 特性

- **极简使用** - 几行代码即可完成 RPC 服务搭建
- **高性能 IOCP** - 基于 SAEA.Sockets 完成端口技术
- **AOP 过滤器** - 支持 `ActionFilterAttribute` 方法拦截
- **服务端推送** - 支持 `Notice<T>` 向订阅客户端推送消息
- **多路复用连接池** - `ConsumerMultiplexer` 支持负载均衡和重连
- **自动代码生成** - `CodeGnerater` 动态生成客户端代理
- **二进制序列化** - 使用 Protobuf 高效传输

## NuGet 安装

```bash
# Package Manager
Install-Package SAEA.RPC -Version 7.26.2.2

# .NET CLI
dotnet add package SAEA.RPC --version 7.26.2.2
```

## 核心类

| 类名 | 说明 |
|------|------|
| `ServiceProvider` | RPC 服务提供者主类 |
| `ServiceConsumer` | RPC 消费者主类 |
| `RPCServiceProxy` | 动态生成的客户端代理基类 |
| `RPCServiceAttribute` | 标记类为 RPC 服务 |
| `NoRpcAttribute` | 标记方法不对外开放 |
| `ActionFilterAttribute` | AOP 过滤器抽象基类 |
| `RPCMapping` | 服务映射缓存表 |
| `ConsumerMultiplexer` | 多路复用连接管理器 |

## 快速使用

### 服务端

```csharp
using SAEA.RPC;

// 定义 RPC 服务
[RPCService]
public class HelloService
{
    public string Hello(string name)
    {
        return $"Hello, {name}!";
    }

    public User GetUser(int id)
    {
        return new User { Id = id, Name = "Test" };
    }
}

// 启动 RPC 服务
var provider = new ServiceProvider(port: 39654);
provider.OnErr += (ex) => Console.WriteLine($"错误: {ex.Message}");
provider.Start();

Console.WriteLine("RPC 服务已启动，端口: 39654");
```

### 客户端

```csharp
using SAEA.RPC;

// 连接 RPC 服务
var url = "rpc://127.0.0.1:39654";
var proxy = new RPCServiceProxy(url);
proxy.OnErr += (ex) => Console.WriteLine($"错误: {ex.Message}");

// 调用远程方法
var result = proxy.HelloService.Hello("SAEA");
Console.WriteLine(result);  // 输出: Hello, SAEA!

var user = proxy.HelloService.GetUser(1);
Console.WriteLine($"用户: {user.Name}");
```

### AOP 过滤器

```csharp
using SAEA.RPC.Model;

// 定义自定义过滤器
public class LogAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(string serviceName, string methodName, object[] args)
    {
        Console.WriteLine($"调用前: {serviceName}.{methodName}");
    }

    public override void OnActionExecuted(string serviceName, string methodName, object result, Exception ex)
    {
        Console.WriteLine($"调用后: {serviceName}.{methodName}, 结果: {result}");
    }
}

// 应用过滤器
[RPCService]
public class UserService
{
    [Log]
    public string Login(string username, string password)
    {
        return "登录成功";
    }
}
```

### 服务端推送

```csharp
using SAEA.RPC;

// 服务端推送消息
var provider = new ServiceProvider(port: 39654);
provider.Start();

// 向订阅的客户端推送消息
provider.Notice("channel", new Notification { Message = "系统通知" });
```

### 多服务地址负载均衡

```csharp
using SAEA.RPC;

// 配置多个服务地址
var urls = new[] 
{
    "rpc://192.168.1.1:39654",
    "rpc://192.168.1.2:39654",
    "rpc://192.168.1.3:39654"
};

// 多路复用连接池
var multiplexer = new ConsumerMultiplexer(urls);
var proxy = new RPCServiceProxy(multiplexer);

// 自动负载均衡调用
var result = proxy.UserService.GetUser(1);
```

## 传输协议

RPC 消息格式：

```
| Type(1字节) | Total(4字节) | SeqNo(8字节) | SLen(4字节) | ServiceName | MLen(4字节) | MethodName | Data |
```

消息类型：
- `Ping/Pong` - 心跳
- `Request/Response` - 请求响应
- `Notice` - 服务端推送
- `Error` - 错误消息
- `Close` - 关闭连接

## 依赖项

| 包名 | 版本 | 说明 |
|------|------|------|
| SAEA.Sockets | 7.26.2.2 | IOCP 通信框架 |
| SAEA.Common | 7.26.2.2 | 序列化工具 |

## 性能对比

SAEA.RPC 采用二进制序列化 + IOCP 通信，性能远超基于 HTTP/JSON 的 RPC 框架：

| 特性 | SAEA.RPC | HTTP RPC |
|------|----------|----------|
| 序列化 | Protobuf | JSON |
| 传输协议 | TCP | HTTP |
| 连接模式 | 长连接 | 短连接 |
| 并发模型 | IOCP | 阻塞 |

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包](https://www.nuget.org/packages/SAEA.RPC)
- [作者博客](https://www.cnblogs.com/yswenli/)

## 许可证

Apache License 2.0