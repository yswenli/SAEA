# SAEA.QueueSocket - 高性能内存消息队列

[![NuGet version](https://img.shields.io/nuget/v/SAEA.QueueSocket.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.QueueSocket)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

## 简介

SAEA.QueueSocket 是一个基于 SAEA.Sockets 的高性能内存消息队列服务器/客户端组件。采用发布/订阅（Pub/Sub）模式，支持 Topic 主题路由，适用于高性能、低延迟的消息分发场景，如实时数据推送、事件通知、解耦系统等。

## 特性

- **高性能 IOCP** - 基于 SAEA.Sockets 完成端口技术
- **发布/订阅模式** - 标准 Pub/Sub 消息队列
- **Topic 主题路由** - 按主题分发消息
- **多订阅者** - 同一 Topic 支持多个订阅者
- **心跳保活** - Ping/Pong 心跳机制
- **批量处理** - Batcher 优化吞吐量
- **连接管理** - 最大连接数和消息堆积限制

## NuGet 安装

```bash
# Package Manager
Install-Package SAEA.QueueSocket -Version 7.26.2.2

# .NET CLI
dotnet add package SAEA.QueueSocket --version 7.26.2.2
```

## 核心类

| 类名 | 说明 |
|------|------|
| `QServer` | 队列服务器 |
| `QClient` | 队列客户端基类 |
| `Producer` | 生产者封装类 |
| `Consumer` | 消费者封装类 |
| `MessageQueue` | 基于 Topic 的消息队列存储 |
| `Exchange` | 消息交换核心类 |
| `QueueMsg` | 队列消息实体 |
| `QueueSocketMsgType` | 消息类型枚举 |

## 快速使用

### 启动队列服务器

```csharp
using SAEA.QueueSocket;

// 创建并启动队列服务器
var server = new QServer(port: 39654);
server.Start();

Console.WriteLine("消息队列服务器已启动，端口: 39654");
```

### 生产者 - 发布消息

```csharp
using SAEA.QueueSocket;

// 创建生产者
var producer = new Producer("producer_001", "127.0.0.1:39654");
producer.OnError += (ex) => Console.WriteLine($"错误: {ex.Message}");
producer.OnDisconnected += () => Console.WriteLine("连接断开");

// 连接服务器
producer.Connect();

// 发布消息到 Topic
producer.Publish("orders", "订单数据: {id: 123, amount: 100}");
producer.Publish("notifications", "系统通知: 新版本发布");
```

### 消费者 - 订阅消息

```csharp
using SAEA.QueueSocket;

// 创建消费者
var consumer = new Consumer("consumer_001", "127.0.0.1:39654");
consumer.OnError += (ex) => Console.WriteLine($"错误: {ex.Message}");
consumer.OnDisconnected += () => Console.WriteLine("连接断开");

// 注册消息接收事件
consumer.OnMessage += (msg) => 
{
    var content = Encoding.UTF8.GetString(msg.Data);
    Console.WriteLine($"收到消息 [Topic: {msg.Topic}]: {content}");
};

// 连接并订阅 Topic
consumer.Connect();
consumer.Subscribe("orders");  // 订阅订单 Topic
consumer.Subscribe("notifications");  // 订阅通知 Topic
```

### 使用 QClient 基类

```csharp
using SAEA.QueueSocket;

// QClient 同时支持发布和订阅
var client = new QClient("client_001", "127.0.0.1:39654");
client.OnMessage += (msg) => Console.WriteLine($"收到: {Encoding.UTF8.GetString(msg.Data)}");
client.OnError += (ex) => Console.WriteLine($"错误: {ex.Message}");
client.OnDisconnected += () => Console.WriteLine("断开连接");

client.Connect();

// 发布消息
client.Publish("topic1", "Hello Topic1");

// 订阅消息
client.Subscribe("topic2");

// 取消订阅
client.Unsubscribe("topic2");

// 关闭连接
client.Close();
```

### 获取服务器统计信息

```csharp
using SAEA.QueueSocket;

var server = new QServer(port: 39654);
server.Start();

// 获取统计信息
server.CalcInfo((sessionCount, topicCount, queuedCount) => 
{
    Console.WriteLine($"连接数: {sessionCount}");
    Console.WriteLine($"Topic数: {topicCount}");
    Console.WriteLine($"待分发消息: {queuedCount}");
});
```

## 消息类型

```csharp
public enum QueueSocketMsgType : byte
{
    Ping = 1,       // 心跳请求
    Pong = 2,       // 心跳响应
    Publish = 3,    // 发布消息
    Subcribe = 4,   // 订阅请求
    Unsubcribe = 5, // 取消订阅
    Close = 6,      // 关闭连接
    Data = 7        // 数据消息
}
```

## 消息流程

```
Producer.Publish(topic, content)
        │
        ▼
    QServer 接收
        │
        ▼
    Exchange.AcceptPublish()
        │
        ▼
    MessageQueue.Enqueue(topic)
        │
        ▼
    分发任务（按 Topic）
        │
        ▼
    Consumer 接收 OnMessage
```

## Topic 特性

- **动态创建** - Topic 按需动态创建，无需预定义
- **多订阅者** - 同一 Topic 可被多个订阅者订阅
- **消息隔离** - 每个 Topic 有独立的消息队列
- **广播分发** - 消息广播给 Topic 所有订阅者（除发送者）

## 应用场景

- **实时数据推送** - 股票行情、体育比分
- **事件通知** - 系统事件、订单状态变化
- **系统解耦** - 微服务间异步通信
- **日志收集** - 分布式日志聚合
- **消息广播** - 多客户端同步通知

## 依赖项

| 包名 | 版本 | 说明 |
|------|------|------|
| SAEA.Sockets | 7.26.2.2 | IOCP 通信框架 |
| SAEA.Common | 7.26.2.2 | 公共工具类 |

## 与 Redis Pub/Sub 对比

| 特性 | SAEA.QueueSocket | Redis Pub/Sub |
|------|------------------|---------------|
| 依赖 | 纯内存，无外部依赖 | 需要 Redis 服务 |
| 性能 | IOCP 高性能 | 网络 IO |
| Topic | 完全支持 | 完全支持 |
| 消息持久化 | 内存队列 | 不持久化 |

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包](https://www.nuget.org/packages/SAEA.QueueSocket)
- [作者博客](https://www.cnblogs.com/yswenli/)

## 许可证

Apache License 2.0