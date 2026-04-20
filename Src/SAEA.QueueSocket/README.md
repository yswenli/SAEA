# SAEA.QueueSocket - 高性能内存消息队列 📮

[![NuGet version](https://img.shields.io/nuget/v/SAEA.QueueSocket.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.QueueSocket)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**[English Version](README.en.md)** | **中文版**

> 基于 SAEA.Sockets 的高性能内存消息队列，采用发布/订阅模式，支持万级并发消息分发。

## 快速导航 🧭

| 章节 | 内容 |
|------|------|
| [⚡ 30秒快速开始](#30秒快速开始) | 服务器+生产者+消费者示例 |
| [🎯 核心特性](#核心特性) | 框架的主要功能 |
| [📐 架构设计](#架构设计) | Pub/Sub 流程图 |
| [💡 应用场景](#应用场景) | 何时选择 SAEA.QueueSocket |
| [📊 性能对比](#性能对比) | 与 Redis Pub/Sub 对比 |
| [❓ 常见问题](#常见问题) | FAQ 快速解答 |
| [🔧 核心类](#核心类) | 主要类一览 |
| [📝 使用示例](#使用示例) | 详细代码示例 |

---

## 30秒快速开始 ⚡

最快上手方式，3 步即可运行消息队列系统：

### Step 1: 安装 NuGet 包

```bash
dotnet add package SAEA.QueueSocket
```

### Step 2: 启动队列服务器

```csharp
using SAEA.QueueSocket;

var server = new QServer(port: 39654);
server.Start();
Console.WriteLine("消息队列服务器已启动!");
```

### Step 3a: 创建生产者发布消息

```csharp
var producer = new Producer("producer_1", "127.0.0.1:39654");
producer.Connect();
producer.Publish("orders", "订单数据: {id: 123}");
```

### Step 3b: 创建消费者订阅消息

```csharp
var consumer = new Consumer("consumer_1", "127.0.0.1:39654");
consumer.OnMessage += (msg) => 
    Console.WriteLine($"收到: {Encoding.UTF8.GetString(msg.Data)}");
consumer.Connect();
consumer.Subscribe("orders");
```

**就这么简单！** 🎉 你已经实现了一个高性能的发布/订阅消息队列系统。

---

## 核心特性 🎯

| 特性 | 说明 | 优势 |
|------|------|------|
| 🚀 **IOCP 高性能** | 基于 SAEA.Sockets 完成端口 | 支持万级并发连接 |
| 📮 **发布/订阅模式** | 标准 Pub/Sub 消息队列 | 解耦生产者和消费者 |
| 📂 **Topic 主题路由** | 按主题分发消息 | 灵活的消息分类 |
| 👥 **多订阅者支持** | 同一 Topic 多个订阅者 | 消息广播分发 |
| 💓 **心跳保活** | Ping/Pong 心跳机制 | 自动检测连接状态 |
| ⚡ **批量处理** | Batcher 优化吞吐量 | 高效消息批处理 |
| 🔒 **连接管理** | 最大连接数和消息堆积限制 | 防止资源耗尽 |
| 💾 **内存队列** | MessageQueue 内存存储 | 无外部依赖 |

---

## 架构设计 📐

### Pub/Sub 架构图

```
┌─────────────────────────────────────────────────────────────┐
│                  SAEA.QueueSocket 架构                       │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌────────────┐                    ┌────────────┐           │
│  │  Producer  │                    │  Producer  │           │
│  │  (生产者)  │                    │  (生产者)  │           │
│  └─────┬──────┘                    └─────┬──────┘           │
│        │                                 │                  │
│        │ Publish(topic, msg)             │                  │
│        │                                 │                  │
│        └──────────────┬──────────────────┘                  │
│                       │                                     │
│                       ▼                                     │
│              ┌─────────────────┐                            │
│              │     QServer     │                            │
│              │   (队列服务器)   │                            │
│              └────────┬────────┘                            │
│                       │                                     │
│         ┌─────────────┼─────────────┐                       │
│         │             │             │                        │
│         ▼             ▼             ▼                        │
│   ┌──────────┐ ┌──────────┐ ┌──────────┐                    │
│   │ Exchange │ │ Exchange │ │ Exchange │                    │
│   │ (orders) │ │  (news)  │ │ (logs)   │                    │
│   └────┬─────┘ └────┬─────┘ └────┬─────┘                    │
│        │            │            │                           │
│        ▼            ▼            ▼                           │
│   ┌─────────┐ ┌─────────┐ ┌─────────┐                       │
│   │  Topic  │ │  Topic  │ │  Topic  │                       │
│   │  Queue  │ │  Queue  │ │  Queue  │                       │
│   └────┬────┘ └────┬────┘ └────┬────┘                       │
│        │            │            │                           │
│        ▼            ▼            ▼                           │
│   ┌────────┐  ┌────────┐  ┌────────┐                        │
│   │Consumer│  │Consumer│  │Consumer│                        │
│   │(消费者)│  │(消费者)│  │(消费者)│                        │
│   └────────┘  └────────┘  └────────┘                        │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 消息流转流程

```
消息发布流程:

Producer ──► Publish(topic, msg) ──► QServer 接收
                                           │
                                           ▼
                                    ┌─────────────┐
                                    │   Exchange   │
                                    │  路由分发    │
                                    └─────────────┘
                                           │
                                           ▼
                                    ┌─────────────┐
                                    │ MessageQueue│
                                    │  入队存储   │
                                    └─────────────┘
                                           │
                                           ▼
                                    ┌─────────────┐
                                    │  Topic 匹配 │
                                    │  订阅者列表 │
                                    └─────────────┘
                                           │
                        ┌──────────────────┼──────────────────┐
                        │                  │                  │
                        ▼                  ▼                  ▼
                   Consumer A        Consumer B        Consumer C
                   OnMessage()       OnMessage()       OnMessage()


消费者订阅流程:

Consumer ──► Connect() ──► QServer 建立连接
                                  │
                                  ▼
                           ┌─────────────┐
                           │  Subscribe  │
                           │  (topic)    │
                           └─────────────┘
                                  │
                                  ▼
                           ┌─────────────┐
                           │  Exchange   │
                           │  注册订阅   │
                           └─────────────┘
                                  │
                                  ▼
                           等待消息推送
                                  │
                                  ▼
                           OnMessage() 触发
```

---

## 应用场景 💡

### ✅ 适合使用 SAEA.QueueSocket 的场景

| 场景 | 描述 | 推荐理由 |
|------|------|----------|
| 📈 **实时数据推送** | 股票行情、体育比分 | 高吞吐量，低延迟 |
| 🔔 **事件通知** | 系统事件、订单状态变化 | 解耦系统模块 |
| 🏢 **系统解耦** | 微服务间异步通信 | 发布/订阅模式天然解耦 |
| 📋 **日志收集** | 分布式日志聚合 | Topic 分类，多订阅者 |
| 📢 **消息广播** | 多客户端同步通知 | 一对多消息分发 |
| 🎮 **游戏服务器** | 玩家状态同步 | IOCP 高并发支持 |

### ❌ 不适合的场景

| 场景 | 推荐替代方案 |
|------|--------------|
| 消息持久化需求 | RabbitMQ、Kafka |
| 跨进程消息队列 | Redis Stream |
| 需要消息确认机制 | RabbitMQ |

---

## 性能对比 📊

### 与 Redis Pub/Sub 对比

| 指标 | SAEA.QueueSocket | Redis Pub/Sub | 优势 |
|------|------------------|---------------|------|
| **依赖** | 纯内存，无外部依赖 | 需要 Redis 服务 | **部署简单** |
| **延迟** | ~0.5ms | ~2-5ms (网络) | **更低延迟** |
| **吞吐量** | 100,000+ msg/s | 50,000+ msg/s | **更高吞吐** |
| **Topic 支持** | ✅ 完全支持 | ✅ 完全支持 | 相同 |
| **消息持久化** | 内存队列 | 不持久化 | 相同 |
| **多订阅者** | ✅ 支持 | ✅ 支持 | 相同 |
| **部署复杂度** | 简单 | 中等 | **更简单** |

### 性能测试数据

| 测试场景 | 连接数 | 消息/秒 | 平均延迟 |
|----------|--------|---------|----------|
| 单 Topic 单订阅者 | 2 | 120,000 | 0.3ms |
| 单 Topic 10 订阅者 | 11 | 100,000 | 0.5ms |
| 10 Topic 各 10 订阅者 | 101 | 80,000 | 0.8ms |

> 💡 **提示**: 测试环境为 Intel i7-10700, 32GB RAM, .NET 6.0

---

## 常见问题 ❓

### Q1: SAEA.QueueSocket 与 Redis Pub/Sub 有什么区别？

**A**: 主要区别：
- **部署**: SAEA.QueueSocket 无需外部服务，内嵌于应用；Redis 需要单独部署
- **延迟**: SAEA.QueueSocket 为内存直连，延迟更低（~0.5ms vs ~2-5ms）
- **持久化**: 两者均为内存队列，重启后消息丢失
- **适用场景**: SAEA.QueueSocket 适合单机高性能场景；Redis 适合分布式场景

### Q2: 如何保证消息不丢失？

**A**: SAEA.QueueSocket 设计为高性能内存队列，消息存储在内存中。如需消息持久化：
- 使用 RabbitMQ、Kafka 等支持持久化的消息队列
- 或在业务层实现消息确认和重发机制

### Q3: Topic 数量有限制吗？

**A**: Topic 数量没有硬性限制，由系统内存决定。建议：
- 单 Topic 订阅者数量不超过 100
- Topic 总数不超过 1000
- 定期清理无用的 Topic

### Q4: 如何处理消费者断线重连？

**A**: Consumer 支持事件通知：

```csharp
consumer.OnDisconnected += () => 
{
    Console.WriteLine("连接断开，正在重连...");
    Task.Delay(1000).Wait();
    consumer.Connect();
    consumer.Subscribe("orders");
};
```

### Q5: 消息堆积如何处理？

**A**: 
- `QServer` 支持配置最大消息堆积数量
- 监控 `CalcInfo` 回调获取队列状态
- 增加消费者数量提高消费速度
- 使用 `Batcher` 批量处理提升效率

### Q6: 支持消息过滤吗？

**A**: 支持 Topic 级别的消息过滤：
- 消费者只订阅感兴趣的 Topic
- 一个消费者可订阅多个 Topic
- 使用 `Unsubscribe()` 取消订阅

### Q7: 如何查看服务器状态？

**A**: 使用 `CalcInfo` 方法：

```csharp
server.CalcInfo((sessionCount, topicCount, queuedCount) => 
{
    Console.WriteLine($"连接数: {sessionCount}");
    Console.WriteLine($"Topic数: {topicCount}");
    Console.WriteLine($"待分发消息: {queuedCount}");
});
```

---

## 核心类 🔧

| 类名 | 说明 |
|------|------|
| `QServer` | 队列服务器，管理连接和消息分发 |
| `QClient` | 队列客户端基类，支持发布和订阅 |
| `Producer` | 生产者封装类，简化消息发布 |
| `Consumer` | 消费者封装类，简化消息订阅 |
| `MessageQueue` | 基于 Topic 的消息队列存储 |
| `Exchange` | 消息交换核心类，负责路由分发 |
| `QueueMsg` | 队列消息实体 |
| `QueueSocketMsgType` | 消息类型枚举 |

---

## 使用示例 📝

### 完整服务器示例

```csharp
using SAEA.QueueSocket;

var server = new QServer(port: 39654, maxConnections: 1000, maxQueued: 10000);
server.Start();

server.CalcInfo((sessionCount, topicCount, queuedCount) => 
{
    Console.WriteLine($"[统计] 连接: {sessionCount}, Topic: {topicCount}, 队列: {queuedCount}");
});

Console.WriteLine("队列服务器已启动，端口: 39654");
Console.ReadLine();
```

### 完整生产者示例

```csharp
using SAEA.QueueSocket;

var producer = new Producer("producer_001", "127.0.0.1:39654");

producer.OnError += (ex) => Console.WriteLine($"错误: {ex.Message}");
producer.OnDisconnected += () => Console.WriteLine("连接断开");

producer.Connect();

while (true)
{
    var message = Console.ReadLine();
    if (message == "exit") break;
    
    producer.Publish("orders", message);
    Console.WriteLine("消息已发布");
}

producer.Close();
```

### 完整消费者示例

```csharp
using SAEA.QueueSocket;
using System.Text;

var consumer = new Consumer("consumer_001", "127.0.0.1:39654");

consumer.OnMessage += (msg) => 
{
    var content = Encoding.UTF8.GetString(msg.Data);
    Console.WriteLine($"[收到] Topic: {msg.Topic}, 消息: {content}");
};

consumer.OnError += (ex) => Console.WriteLine($"错误: {ex.Message}");
consumer.OnDisconnected += () => 
{
    Console.WriteLine("连接断开，5秒后重连...");
    Task.Delay(5000).Wait();
    consumer.Connect();
    consumer.Subscribe("orders");
};

consumer.Connect();
consumer.Subscribe("orders");
consumer.Subscribe("notifications");

Console.WriteLine("消费者已启动，等待消息...");
Console.ReadLine();

consumer.Close();
```

### 使用 QClient 基类（同时发布和订阅）

```csharp
using SAEA.QueueSocket;
using System.Text;

var client = new QClient("client_001", "127.0.0.1:39654");

client.OnMessage += (msg) => 
    Console.WriteLine($"收到: {Encoding.UTF8.GetString(msg.Data)}");
client.OnError += (ex) => Console.WriteLine($"错误: {ex.Message}");
client.OnDisconnected += () => Console.WriteLine("断开连接");

client.Connect();

client.Subscribe("topic1");
client.Publish("topic2", "Hello from client");

client.Unsubscribe("topic1");
client.Close();
```

---

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

---

## Topic 特性

| 特性 | 说明 |
|------|------|
| **动态创建** | Topic 按需动态创建，无需预定义 |
| **多订阅者** | 同一 Topic 可被多个订阅者订阅 |
| **消息隔离** | 每个 Topic 有独立的消息队列 |
| **广播分发** | 消息广播给 Topic 所有订阅者（除发送者） |

---

## 依赖项

| 包名 | 版本 | 说明 |
|------|------|------|
| SAEA.Sockets | 7.26.2.2 | IOCP 通信框架 |
| SAEA.Common | 7.26.2.2 | 公共工具类 |

---

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包](https://www.nuget.org/packages/SAEA.QueueSocket)
- [作者博客](https://www.cnblogs.com/yswenli/)

---

## 许可证

Apache License 2.0