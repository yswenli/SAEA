# SAEA.RPC - 高性能远程过程调用框架 🚀

[![NuGet version](https://img.shields.io/nuget/v/SAEA.RPC.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.RPC)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**[English Version](README.en.md)** | **中文版**

> 基于 SAEA.Sockets 异步 IO 的高性能 RPC 框架，二进制传输，性能远超 HTTP/JSON RPC。
>
> ⚡ **零拷贝内核**：构建于 SAEA.Sockets 的 Span/Memory 零拷贝收发与内存池通道之上，显著降低分配与 GC。

## 快速导航 🧭

| 章节 | 内容 |
|------|------|
| [⚡ 30秒快速开始](#30秒快速开始) | 最简单的上手示例 |
| [🎯 核心特性](#核心特性) | 框架的主要功能 |
| [📐 架构设计](#架构设计) | 组件关系与调用流程 |
| [💡 应用场景](#应用场景) | 何时选择 SAEA.RPC |
| [📊 性能对比](#性能对比) | 与 HTTP RPC 对比 |
| [❓ 常见问题](#常见问题) | FAQ 快速解答 |
| [🔧 核心类](#核心类) | 主要类一览 |
| [📝 使用示例](#使用示例) | 详细代码示例 |

---

## 30秒快速开始 ⚡

最快上手方式，只需3步即可运行 RPC 服务：

### Step 1: 安装 NuGet 包

```bash
dotnet add package SAEA.RPC
```

### Step 2: 创建 RPC 服务端（仅需5行代码）

```csharp
using SAEA.RPC;

[RPCService]
public class HelloService
{
    public string SayHello(string name) => $"Hello, {name}!";
}

var provider = new ServiceProvider(port: 39654);
provider.Start();
```

### Step 3: 创建 RPC 客户端调用

```csharp
var proxy = new RPCServiceProxy("rpc://127.0.0.1:39654");
var result = proxy.HelloService.SayHello("SAEA");
Console.WriteLine(result);  // 输出: Hello, SAEA!
```

**就这么简单！** 🎉 你已经实现了一个高性能的 RPC 远程调用系统。

---

## 核心特性 🎯

| 特性 | 说明 | 优势 |
|------|------|------|
| 🔥 **极简使用** | `[RPCService]` 标记即可 | 零配置，几行代码完成服务搭建 |
| ⚡ **跨平台异步 IO** | 基于 SAEA.Sockets | 面向高并发，低延迟响应 |
| 📦 **二进制序列化** | Protobuf 高效传输 | 数据量小，序列化快 |
| 🔌 **动态代理** | 自动生成客户端代码 | 像调用本地方法一样调用远程服务 |
| 🛡️ **AOP 过滤器** | `ActionFilterAttribute` | 日志、权限、缓存统一拦截 |
| 📡 **服务端推送** | `Notice<T>` 推送消息 | 支持发布订阅模式 |
| ⚖️ **连接复用** | `ConsumerMultiplexer` | 同一服务地址按 `links` 复用连接 |
| 🔗 **连接池** | 长连接复用 | 减少连接开销，提升性能 |

---

## 架构设计 📐

### 组件架构图

```
┌─────────────────────────────────────────────────────────────┐
│                     SAEA.RPC 架构                            │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────────────┐      ┌─────────────────────┐      │
│  │    Client 端        │      │    Server 端        │      │
│  │                     │      │                     │      │
│  │  ┌───────────────┐  │      │  ┌───────────────┐  │      │
│  │  │RPCServiceProxy│  │      │  │ ServiceProvider│  │      │
│  │  │  (动态代理)    │  │      │  │  (服务提供者)  │  │      │
│  │  └───────┬───────┘  │      │  └───────┬───────┘  │      │
│  │          │          │      │          │          │      │
│  │  ┌───────▼───────┐  │      │  ┌───────▼───────┐  │      │
│  │  │ServiceConsumer│  │      │  │ [RPCService]  │  │      │
│  │  │  (消费者)     │  │      │  │  (服务类)     │  │      │
│  │  └───────┬───────┘  │      │  └───────┬───────┘  │      │
│  │          │          │      │          │          │      │
│  │  ┌───────▼───────┐  │      │  ┌───────▼───────┐  │      │
│  │  │ConsumerMulti- │  │      │  │ RPCMapping    │  │      │
│  │  │ plexer(连接池) │  │      │  │ (服务映射)    │  │      │
│  │  └───────┬───────┘  │      │  └───────┬───────┘  │      │
│  └──────────┼──────────┘      └──────────┼──────────┘      │
│             │                            │                  │
│             └────────────┬───────────────┘                  │
│                          │                                  │
│                 ┌────────▼────────┐                        │
│                 │  SAEA.Sockets   │                        │
│                 │    (异步 IO)    │                        │
│                 └─────────────────┘                        │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### RPC 调用流程图

```
客户端调用流程:

Client                        Server
  │                             │
  │  proxy.Method(args)         │
  │         │                   │
  │    ┌────▼────┐              │
  │    │ 动态代理 │              │
  │    │生成调用  │              │
  │    └────┬────┘              │
  │         │                   │
  │    ┌────▼────┐              │
  │    │Protobuf │              │
  │    │ 序列化  │              │
  │    └────┬────┘              │
  │         │                   │
  │    ┌────▼────┐   TCP/异步  ┌▼────────┐
  │    │发送请求 ├──────────────►接收请求 │
  │    └─────────┘              └────┬────┘
  │                                  │
  │                            ┌─────▼─────┐
  │                            │ RPCMapping│
  │                            │ 查找服务  │
  │                            └─────┬─────┘
  │                                  │
  │                            ┌─────▼─────┐
  │                            │ 调用方法  │
  │                            │执行逻辑   │
  │                            └─────┬─────┘
  │                                  │
  │                            ┌─────▼─────┐
  │                            │Protobuf   │
  │                            │ 序列化    │
  │                            └─────┬─────┘
  │                                  │
  │         ┌────────────────────────┘
  │    ┌────▼────┐              ┌────▼────┐
  │    │接收响应 ◄──────────────┤发送响应 │
  │    └────┬────┘              └─────────┘
  │         │
  │    ┌────▼────┐
  │    │Protobuf │
  │    │ 反序列化│
  │    └────┬────┘
  │         │
  │    ┌────▼────┐
  │    │返回结果 │
  │    └─────────┘
  │                             │
```

---

## 应用场景 💡

### ✅ 适合使用 SAEA.RPC 的场景

| 场景 | 描述 | 推荐理由 |
|------|------|----------|
| 🏢 **微服务通信** | 服务间高性能调用 | 二进制传输，低延迟，高吞吐 |
| 🎮 **游戏后端** | 游戏服务器间通信 | 异步 IO 支撑高并发，实时性强 |
| 🤖 **分布式系统** | 节点间协调通信 | 长连接复用，减少握手开销 |
| 📊 **实时数据处理** | 数据管道服务 | 高效序列化，处理速度快 |
| 🔧 **内部 API** | 内部服务调用 | 相比 HTTP REST，性能提升显著 |
| 💾 **数据访问层** | 数据服务代理 | 支持复杂对象传输 |

### ❌ 不适合的场景

| 场景 | 推荐替代方案 |
|------|--------------|
| 浏览器客户端调用 | 使用 SAEA.WebSocket 或 REST API |
| 跨语言调用 | 使用 gRPC 或 HTTP API |
| 公开外部 API | 使用 SAEA.Http 或 ASP.NET WebAPI |
| 简单配置服务 | 使用配置中心 |

---

## 性能对比 📊

### 与 HTTP RPC 对比

| 指标 | SAEA.RPC | HTTP/JSON RPC | 优势 |
|------|----------|---------------|------|
| **序列化方式** | Protobuf 二进制 | JSON 文本 | 体积更小 |
| **传输协议** | TCP 长连接 | HTTP 短连接 | 减少握手开销 |
| **并发模型** | 异步 IO | 阻塞/异步 | CPU 利用率高 |
| **延迟** | 低（长连接 + 二进制） | 较高（短连接 + 文本解析） | 减少握手与解析开销 |
| **吞吐量** | 高 | 较低 | 更高吞吐 |
| **连接复用** | 支持 | 需 Keep-Alive | 原生支持 |

> 📌 上表为定性比较，未发布跨框架基准数据；请针对自身负载实测。

### 序列化性能对比

| 格式 | 相对数据大小 | 序列化速度 | 反序列化速度 |
|------|----------|------------|--------------|
| **Protobuf** | 小 | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| JSON | 中 | ⭐⭐⭐ | ⭐⭐⭐ |
| XML | 大 | ⭐⭐ | ⭐⭐ |

> 💡 **提示**: SAEA.RPC 使用 Protobuf 二进制序列化，数据体积小、速度快，是高性能 RPC 的最佳选择。

---

## 常见问题 ❓

### Q1: SAEA.RPC 与 gRPC 有什么区别？

**A**: 
| 特性 | SAEA.RPC | gRPC |
|------|----------|------|
| 学习曲线 | 简单 | 中等 |
| 跨语言 | 仅 .NET | 多语言支持 |
| 传输协议 | 自定义 TCP | HTTP/2 |
| 适用场景 | .NET 内部通信 | 跨语言微服务 |

推荐在纯 .NET 环境下使用 SAEA.RPC，更简单高效。

### Q2: 如何实现服务注册与发现？

**A**: `ConsumerMultiplexer` 负责同一服务地址上的多连接复用（`links` 指定连接数，调用在连接间轮转）；多地址由使用方选好地址后分别创建：

```csharp
var urls = new[]
{
    "rpc://192.168.1.1:39654",
    "rpc://192.168.1.2:39654"
};
var index = 0;
string NextUrl() => urls[index++ % urls.Length];

// 取一个地址创建消费者；同一地址内部再按 links 复用连接
var proxy = new RPCServiceProxy(NextUrl(), links: 4);
```

也可结合 Consul、Nacos 等服务发现组件动态更新地址列表。

### Q3: 如何处理超时和异常？

**A**: 传输、调用与超时异常统一汇入 `ExceptionCollector`：

```csharp
ExceptionCollector.OnErr += (name, ex) =>
{
    Console.WriteLine($"[{name}] RPC 调用异常: {ex.Message}");
};

var proxy = new RPCServiceProxy("rpc://127.0.0.1:39654");
```

服务端共用同一个异常汇，无需单独订阅：
```csharp
var provider = new ServiceProvider(port: 39654);
provider.Start();
```

需要按连接观察链上异常时，用 `ConfigurePipeline` 追加处理器。回调必须在 `Start()` 之前赋值，服务启动后再赋值会抛 `InvalidOperationException`：

```csharp
var provider = new ServiceProvider(port: 39654);
provider.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new ExceptionLogHandler());
provider.Start();
```

消费者在构造时就建立连接，链启动后即冻结，构造完成后再赋值会抛 `InvalidOperationException`，因此装配回调必须作为构造参数传入：

```csharp
var proxy = new RPCServiceProxy("rpc://127.0.0.1:39654", 4, 5, 10 * 1000,
    (id, pipeline) => pipeline.AddLast(new ExceptionLogHandler()));
```

```csharp
/// <summary>
/// 链上异常观察器：打印后继续交给链尾收口
/// </summary>
public sealed class ExceptionLogHandler : InboundHandlerAdapter
{
    public override Task ChannelExceptionCaughtAsync(IChannelHandlerContext ctx, Exception ex)
    {
        Console.WriteLine($"{ctx.ChannelId} RPC 异常: {ex.Message}");
        return ctx.FireExceptionCaughtAsync(ex);
    }
}
```

### Q4: 如何实现日志记录和权限验证？

**A**: 使用 AOP 过滤器 `ActionFilterAttribute`：

```csharp
public class AuthAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(string service, string method, object[] args)
    {
        // 权限验证逻辑
        if (!ValidateToken())
            throw new UnauthorizedAccessException();
    }
}

[RPCService]
public class AdminService
{
    [Auth]
    public void DeleteUser(int id) { }
}
```

### Q5: 支持异步方法吗？

**A**: 支持 async/await：

```csharp
[RPCService]
public class UserService
{
    public async Task<User> GetUserAsync(int id)
    {
        return await _repository.FindAsync(id);
    }
}
```

### Q6: 如何隐藏某些方法不对外暴露？

**A**: 使用 `[NoRpc]` 特性：

```csharp
[RPCService]
public class UserService
{
    public string Login(string name) => "OK";  // 暴露
    
    [NoRpc]
    public void InternalMethod() { }  // 不暴露
}
```

### Q7: 如何实现服务端推送？

**A**: 消费者先 `RegistReceiveNotice()` 登记，再用 `OnNoticed` 接收；服务端调用 `Notice<T>` 广播给所有已登记的连接：

```csharp
using SAEA.Common.Serialization;

// 服务端推送（广播给所有已登记的消费者）
await provider.Notice("系统更新");

// 客户端订阅
var proxy = new RPCServiceProxy("rpc://127.0.0.1:39654");
proxy.OnNoticed += (serializeData) =>
{
    Console.WriteLine($"收到推送: {SAEASerialize.Deserialize<string>(serializeData)}");
};
proxy.RegistReceiveNotice();
```

`OnNoticed` 是领域通知事件，参数是推送载荷的序列化字节；需要按连接观察原始帧时改用处理器链。

---

## 核心类 🔧

| 类名 | 说明 |
|------|------|
| `ServiceProvider` | RPC 服务提供者主类，负责启动和管理服务 |
| `ServiceConsumer` | RPC 消费者主类，负责连接和调用 |
| `RPCServiceProxy` | 动态生成的客户端代理基类 |
| `RPCServiceAttribute` | 标记类为 RPC 服务 |
| `NoRpcAttribute` | 标记方法不对外开放 |
| `ActionFilterAttribute` | AOP 过滤器抽象基类 |
| `RPCMapping` | 服务映射缓存表 |
| `ConsumerMultiplexer` | 同一服务地址上的多连接复用管理器（`links` 决定连接数） |
| `Notice<T>` | 服务端推送消息类型 |

---

## 使用示例 📝

### 完整服务端示例

```csharp
using SAEA.RPC;
using SAEA.RPC.Model;

// 定义用户实体
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public DateTime CreateTime { get; set; }
}

// 定义日志过滤器
public class LogAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(string serviceName, string methodName, object[] args)
    {
        Console.WriteLine($"[{DateTime.Now}] 调用: {serviceName}.{methodName}");
    }

    public override void OnActionExecuted(string serviceName, string methodName, object result, Exception ex)
    {
        if (ex != null)
            Console.WriteLine($"[错误] {ex.Message}");
        else
            Console.WriteLine($"[完成] 返回: {result}");
    }
}

// 定义 RPC 服务
[RPCService]
public class UserService
{
    private List<User> _users = new List<User>();

    [Log]
    public string Register(string name)
    {
        var user = new User 
        { 
            Id = _users.Count + 1, 
            Name = name,
            CreateTime = DateTime.Now 
        };
        _users.Add(user);
        return $"用户 {name} 注册成功，ID: {user.Id}";
    }

    public User GetUser(int id)
    {
        return _users.FirstOrDefault(u => u.Id == id);
    }

    public List<User> GetAllUsers()
    {
        return _users;
    }

    [NoRpc]
    public void InternalMethod()
    {
        // 此方法不会暴露为 RPC 服务
    }
}

// 启动服务（异常统一汇入 ExceptionCollector，可按需在此之前订阅 ExceptionCollector.OnErr）
var provider = new ServiceProvider(port: 39654);
provider.Start();

Console.WriteLine("RPC 服务已启动，端口: 39654");
Console.ReadKey();
```

### 完整客户端示例

```csharp
using SAEA.RPC;

// 连接 RPC 服务（构造时即建立连接；异常统一汇入 ExceptionCollector）
var url = "rpc://127.0.0.1:39654";
var proxy = new RPCServiceProxy(url);

// 调用远程方法 - 注册用户
var result = proxy.UserService.Register("张三");
Console.WriteLine(result);  // 输出: 用户 张三 注册成功，ID: 1

// 调用远程方法 - 获取用户
var user = proxy.UserService.GetUser(1);
Console.WriteLine($"用户信息: {user.Name}, 注册时间: {user.CreateTime}");

// 调用远程方法 - 获取所有用户
var users = proxy.UserService.GetAllUsers();
Console.WriteLine($"总用户数: {users.Count}");
```

### 多服务地址负载均衡

`RPCServiceProxy` 一个实例绑定一个服务地址；同一地址内部的连接复用由 `links` 控制（`ConsumerMultiplexer` 轮转使用这些连接）。多地址需要由使用方自己选路：

```csharp
using SAEA.RPC;

// 配置多个服务地址，调用方自行轮转
var urls = new[]
{
    "rpc://192.168.1.1:39654",
    "rpc://192.168.1.2:39654",
    "rpc://192.168.1.3:39654"
};
var index = 0;
string NextUrl() => urls[index++ % urls.Length];

var proxy = new RPCServiceProxy(NextUrl(), links: 4);

for (int i = 0; i < 100; i++)
{
    var result = proxy.UserService.Register($"User_{i}");
    Console.WriteLine(result);
}
```

### 服务端推送示例

```csharp
using SAEA.Common.Serialization;
using SAEA.RPC;

// 服务端
var provider = new ServiceProvider(port: 39654);
provider.Start();

// 定时推送消息（广播给所有已登记的消费者）
var timer = new Timer(_ =>
{
    provider.Notice($"系统公告 当前时间: {DateTime.Now}");
}, null, 0, 5000);

// 客户端订阅
var proxy = new RPCServiceProxy("rpc://127.0.0.1:39654");
proxy.OnNoticed += (serializeData) =>
{
    Console.WriteLine($"收到推送: {SAEASerialize.Deserialize<string>(serializeData)}");
};
proxy.RegistReceiveNotice();
```

---

## 传输协议

RPC 消息格式：

```
| Type(1字节) | Total(4字节) | SeqNo(8字节) | SLen(4字节) | ServiceName | MLen(4字节) | MethodName | Data |
```

| 字段 | 大小 | 说明 |
|------|------|------|
| Type | 1 字节 | 消息类型 |
| Total | 4 字节 | 总长度 |
| SeqNo | 8 字节 | 序列号（请求响应匹配） |
| SLen | 4 字节 | 服务名长度 |
| ServiceName | N 字节 | 服务名称 |
| MLen | 4 字节 | 方法名长度 |
| MethodName | N 字节 | 方法名称 |
| Data | N 字节 | Protobuf 序列化参数 |

消息类型：
- `Ping/Pong` - 心跳保活
- `Request/Response` - 请求响应
- `Notice` - 服务端推送
- `Error` - 错误消息
- `Close` - 关闭连接

---

## 依赖项

| 包名 | 版本 | 说明 |
|------|------|------|
| SAEA.Sockets | 26.9.21.1 | Socket 通信框架 |
| SAEA.Common | 26.9.21.1 | 序列化工具 |

---

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包](https://www.nuget.org/packages/SAEA.RPC)
- [作者博客](https://www.cnblogs.com/yswenli/)

---

## 许可证

Apache License 2.0