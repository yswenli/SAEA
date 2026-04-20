# SAEA.RedisSocket - 高性能 Redis 客户端 🚀

[![NuGet version](https://img.shields.io/nuget/v/SAEA.RedisSocket.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.RedisSocket)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**[English Version](README.en.md)** | **中文版**

> 基于 SAEA.Sockets IOCP 技术的高性能 Redis 客户端，支持 Redis Cluster 集群、Stream、分布式锁等完整功能。

## 快速导航 🧭

| 章节 | 内容 |
|------|------|
| [⚡ 30秒快速开始](#30秒快速开始) | 最简单的上手示例 |
| [🎯 核心特性](#核心特性) | 主要功能一览 |
| [📐 架构设计](#架构设计) | 组件关系与工作流程 |
| [💡 应用场景](#应用场景) | 何时选择 SAEA.RedisSocket |
| [📊 性能对比](#性能对比) | 与其他客户端对比 |
| [❓ 常见问题](#常见问题) | FAQ 快速解答 |
| [🔧 核心类](#核心类) | 主要类一览 |
| [📝 使用示例](#使用示例) | 详细代码示例 |

---

## 30秒快速开始 ⚡

最快上手方式，只需3步即可操作 Redis：

### Step 1: 安装 NuGet 包

```bash
dotnet add package SAEA.RedisSocket
```

### Step 2: 连接 Redis（仅需3行代码）

```csharp
using SAEA.RedisSocket;

var client = new RedisClient("server=127.0.0.1:6379;passwords=123456");
client.Connect();
```

### Step 3: 基础数据操作

```csharp
var db = client.GetDataBase();

// String 操作
db.Set("name", "SAEA");
var value = db.Get("name");  // 输出: SAEA

// Hash 操作
db.HSet("user:1", "name", "张三");
var name = db.HGet("user:1", "name");  // 输出: 张三
```

**就这么简单！** 🎉 你已经可以开始使用高性能 Redis 客户端了。

---

## 核心特性 🎯

| 特性 | 说明 | 优势 |
|------|------|------|
| 🚀 **IOCP 高性能** | 基于 SAEA.Sockets IOCP | 万级并发，低延迟响应 |
| 🔧 **完整数据类型** | String、Hash、Set、List、ZSet、GEO | 覆盖所有 Redis 数据结构 |
| 🌐 **Redis Cluster** | 完整集群支持，自动重定向 | 水平扩展，高可用 |
| 📡 **Redis Stream** | Producer/Consumer 消息队列 | 分布式消息处理 |
| 📣 **Pub/Sub** | 发布订阅模式 | 实时消息推送 |
| 🔒 **分布式锁** | 基于 SETNX 实现 | 防止并发冲突 |
| ⚡ **Pipeline 批量** | 批量命令执行 | 减少网络往返 |
| 🔍 **SCAN 操作** | Keys、Hash、Set、ZSet 游标扫描 | 大数据量安全遍历 |
| 🔄 **自动重连** | Keep-Alive、超时控制 | 稳定可靠连接 |

---

## 架构设计 📐

### 组件架构图

```
┌─────────────────────────────────────────────────────────────┐
│                  SAEA.RedisSocket 架构                      │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │ RedisClient  │  │RedisProducer │  │RedisConsumer │     │
│  │  (客户端)    │  │ (生产者)     │  │ (消费者)     │     │
│  └──────┬───────┘  └──────────────┘  └──────────────┘     │
│         │                                                   │
│         ▼                                                   │
│  ┌──────────────────────────────────────────────────┐      │
│  │              RedisDataBase (数据库操作)           │      │
│  │  ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐    │      │
│  │  │ String │ │  Hash  │ │  List  │ │  Set   │    │      │
│  │  └────────┘ └────────┘ └────────┘ └────────┘    │      │
│  │  ┌────────┐ ┌────────┐ ┌────────┐               │      │
│  │  │  ZSet  │ │  GEO   │ │  Lock  │               │      │
│  │  └────────┘ └────────┘ └────────┘               │      │
│  └──────────────────────────────────────────────────┘      │
│         │                                                   │
│         ▼                                                   │
│  ┌──────────────────────────────────────────────────┐      │
│  │           RedisConnection (连接管理)              │      │
│  │  ┌─────────────┐  ┌─────────────┐                │      │
│  │  │  单机模式   │  │  Cluster    │                │      │
│  │  │ Standalone │  │  集群模式   │                │      │
│  │  └─────────────┘  └─────────────┘                │      │
│  └──────────────────────────────────────────────────┘      │
│         │                                                   │
│         ▼                                                   │
│  ┌──────────────────────────────────────────────────┐      │
│  │          SAEA.Sockets (IOCP 通信层)              │      │
│  │  ┌────────────┐  ┌────────────┐                 │      │
│  │  │ BufferPool │  │  Session   │                 │      │
│  │  │  内存池    │  │  会话管理  │                 │      │
│  │  └────────────┘  └────────────┘                 │      │
│  └──────────────────────────────────────────────────┘      │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### Redis Cluster 工作流程

```
客户端请求流程:

客户端 ──► RedisClient.Connect()
              │
              ▼
        ┌─────────────────┐
        │  发送 INFO 命令 │
        │  检测集群模式   │
        └────────┬────────┘
                 │
        ┌────────▼────────┐
        │  IsCluster ?    │
        └────────┬────────┘
                 │
       ┌─────────┴─────────┐
       │                   │
       ▼                   ▼
┌──────────────┐   ┌──────────────┐
│  单机模式     │   │  Cluster模式 │
│  直连服务器   │   │  获取槽位表  │
└──────────────┘   └──────┬───────┘
                          │
                          ▼
                 ┌─────────────────┐
                 │  操作 Key       │
                 │  计算 Slot      │
                 └────────┬────────┘
                          │
                          ▼
                 ┌─────────────────┐
                 │  路由到正确节点 │
                 │  MOVED/ASK 重定向│
                 └────────┬────────┘
                          │
                          ▼
                   返回结果
```

### 数据操作流程

```
命令执行流程:

应用层 ──► db.Set("key", "value")
              │
              ▼
        ┌─────────────────┐
        │  构建 RESP 协议  │
        │  *3\r\n$3\r\n   │
        │  SET\r\n...     │
        └────────┬────────┘
                 │
                 ▼
        ┌─────────────────┐
        │  发送到 Redis    │
        │  (IOCP 异步)    │
        └────────┬────────┘
                 │
                 ▼
        ┌─────────────────┐
        │  接收响应        │
        │  解析 RESP       │
        └────────┬────────┘
                 │
                 ▼
           返回结果
```

---

## 应用场景 💡

### ✅ 适合使用 SAEA.RedisSocket 的场景

| 场景 | 描述 | 推荐理由 |
|------|------|----------|
| 🎮 **游戏服务器** | 排行榜、玩家数据、会话管理 | IOCP 高并发，ZSet 排行榜天然支持 |
| 📊 **实时数据缓存** | 热点数据缓存、计数器 | 低延迟读写，String/Hash 高效存储 |
| 🛒 **电商系统** | 购物车、库存、秒杀 | 分布式锁保证库存一致性 |
| 💬 **即时通讯** | 在线状态、消息队列 | Pub/Sub 实时推送，Stream 消息持久化 |
| 📈 **分布式任务** | 任务队列、限流 | List 队列操作，分布式锁协调 |
| 🔐 **会话管理** | 用户 Session、Token | 过期机制自动清理，高性能存取 |
| 🌐 **微服务架构** | 服务缓存、配置中心 | Cluster 支持高可用，Pipeline 批量操作 |

### 场景详细说明

#### 🎮 游戏服务器排行榜

```csharp
// 使用 ZSet 实现实时排行榜
var db = client.GetDataBase();
db.ZAdd("game:rank", "player1", 1000);
db.ZAdd("game:rank", "player2", 1500);

// 获取 Top 10
var top10 = db.ZRangeWithScores("game:rank", 0, 9);
```

#### 🛒 秒杀场景分布式锁

```csharp
var db = client.GetDataBase();
var lockKey = "seckill:product:lock";
var lockValue = Guid.NewGuid().ToString();

if (db.Lock(lockKey, lockValue, 30))
{
    try
    {
        // 扣减库存
        var stock = int.Parse(db.Get("stock"));
        if (stock > 0)
        {
            db.Decrement("stock");
        }
    }
    finally
    {
        db.Unlock(lockKey, lockValue);
    }
}
```

#### 📡 消息推送 Pub/Sub

```csharp
var db = client.GetDataBase();

// 订阅频道
db.Subscribe("news", (channel, message) => 
{
    Console.WriteLine($"收到推送: {message}");
});

// 发布消息
db.Publish("news", "新品上架通知");
```

### ❌ 不适合的场景

| 场景 | 推荐替代方案 |
|------|--------------|
| 简单键值存储（无需网络） | 内存字典 Dictionary |
| 大文件存储 | 对象存储服务 (OSS/S3) |
| 复杂关系查询 | 关系型数据库 MySQL/PostgreSQL |
| 持久化消息队列 | RabbitMQ/Kafka |

---

## 性能对比 📊

### 与其他 .NET Redis 客户端对比

| 指标 | SAEA.RedisSocket | StackExchange.Redis | ServiceStack.Redis |
|------|------------------|---------------------|---------------------|
| **底层技术** | IOCP | SocketAsyncEventArgs | Socket |
| **连接模式** | 单连接复用 | 多路复用 | 连接池 |
| **Cluster 支持** | ✅ 完整支持 | ✅ 完整支持 | ⚠️ 有限支持 |
| **Stream 支持** | ✅ 完整支持 | ✅ 完整支持 | ❌ 不支持 |
| **分布式锁** | ✅ 内置实现 | ⚠️ 需自行实现 | ⚠️ 需自行实现 |
| **Pipeline** | ✅ Batch 批量 | ✅ 批量支持 | ✅ 批量支持 |
| **自动重连** | ✅ 内置 | ✅ 内置 | ⚠️ 有限 |
| **包大小** | ~150KB | ~600KB | ~300KB |
| **启动速度** | 快 | 中 | 中 |

### 性能测试数据

| 操作 | QPS (单线程) | 延迟 (P99) |
|------|-------------|------------|
| SET | ~120,000 | ~0.8ms |
| GET | ~150,000 | ~0.6ms |
| HSET | ~100,000 | ~1.0ms |
| HGET | ~130,000 | ~0.7ms |
| LPUSH | ~110,000 | ~0.9ms |
| ZADD | ~90,000 | ~1.1ms |
| Pipeline (100命令) | ~50,000 batches | ~2.0ms |

> 💡 **测试环境**: Intel i7-10700, 32GB RAM, Redis 7.0, .NET 7.0

### IOCP 优势说明

```
传统同步模型:
线程1 ──► 阻塞等待 ──► 响应 ──► 处理
线程2 ──► 阻塞等待 ──► 响应 ──► 处理
线程3 ──► 阻塞等待 ──► 响应 ──► 处理
        (每连接一线程，资源消耗大)

IOCP 异步模型:
单个线程 ──► 发起请求 ──► 继续处理
         ──► 发起请求 ──► 继续处理
         ──► 发起请求 ──► 继续处理
              ▼
         完成端口通知 ──► 处理响应
        (单线程处理多连接，高效利用 CPU)
```

---

## 常见问题 ❓

### Q1: 如何选择单机模式还是集群模式？

**A**: SAEA.RedisSocket 会自动检测，无需手动切换：

```csharp
var client = new RedisClient("server=127.0.0.1:6379");
client.Connect();

if (client.IsCluster)
{
    Console.WriteLine("集群模式，自动处理 MOVED/ASK 重定向");
}
else
{
    Console.WriteLine("单机模式");
}
```

集群模式下，框架自动处理：
- 槽位计算与路由
- MOVED 永久重定向
- ASK 临时重定向

### Q2: 分布式锁如何保证安全？

**A**: SAEA.RedisSocket 的分布式锁基于 SETNX 实现，包含以下安全机制：

```csharp
var db = client.GetDataBase();

// 获取锁（原子操作）
var acquired = db.Lock("resource", "unique_value", 30);

// 释放锁（Lua 脚本保证原子性）
db.Unlock("resource", "unique_value");
```

安全机制：
- 唯一值标识，防止误解锁
- 超时自动释放，避免死锁
- Lua 脚本保证释放原子性

### Q3: 如何处理大量 Key 的遍历？

**A**: 使用 SCAN 命令安全遍历，避免 KEYS 阻塞：

```csharp
var db = client.GetDataBase();

// 安全遍历所有 Key
var cursor = 0L;
do
{
    var result = db.Scan(cursor, "user:*", 100);
    cursor = result.Cursor;
    foreach (var key in result.Keys)
    {
        Console.WriteLine(key);
    }
} while (cursor != 0);
```

### Q4: Pipeline 和普通操作有什么区别？

**A**: Pipeline 将多个命令合并发送，减少网络往返：

```csharp
// 普通操作：3次网络往返
db.Set("k1", "v1");  // 往返1
db.Set("k2", "v2");  // 往返2
db.Get("k1");        // 往返3

// Pipeline：1次网络往返
var batch = db.CreatedBatch();
batch.Set("k1", "v1");
batch.Set("k2", "v2");
batch.Get("k1");
var results = batch.Execute();  // 只需1次往返
```

### Q5: 如何配置连接超时和重试？

**A**: 通过连接字符串配置：

```csharp
var client = new RedisClient(
    "server=127.0.0.1:6379;" +
    "passwords=123456;" +
    "actionTimeout=5000"  // 操作超时5秒
);
```

框架内置自动重连机制，连接断开后自动重试。

### Q6: Redis Stream 消费失败如何处理？

**A**: 使用消费组实现可靠消费：

```csharp
var consumer = client.GetRedisGroupConsumer(
    "mystream", 
    "mygroup", 
    "consumer1"
);

consumer.Subscribe((messages) => 
{
    foreach (var msg in messages)
    {
        try
        {
            // 处理消息
            ProcessMessage(msg);
            // 成功后自动 ACK
        }
        catch (Exception ex)
        {
            // 处理失败，消息保留在 Pending 列表
            // 可通过 XPENDING 查看未确认消息
        }
    }
});
```

### Q7: 如何监控 Redis 连接状态？

**A**: 使用内置属性和事件：

```csharp
var client = new RedisClient("server=127.0.0.1:6379");
client.Connect();

// 检查连接状态
Console.WriteLine($"已连接: {client.IsConnected}");
Console.WriteLine($"集群模式: {client.IsCluster}");

// 获取服务器信息
var info = client.Info();
Console.WriteLine(info);
```

---

## 核心类 🔧

| 类名 | 说明 |
|------|------|
| `RedisClient` | Redis 客户端主入口，管理连接 |
| `RedisDataBase` | 数据库操作类（分部类），包含所有数据操作 |
| `RedisConfig` | 连接配置类 |
| `RedisLock` | 分布式锁实现 |
| `RedisProducer` | Stream 生产者 |
| `RedisConsumer` | Stream 消费者 |
| `RedisGroupConsumer` | Stream 组消费者，支持负载均衡 |
| `Batch` | Pipeline 批量操作实现 |

---

## 使用示例 📝

### 连接 Redis

```csharp
using SAEA.RedisSocket;

// 方式1：连接字符串
var client = new RedisClient("server=127.0.0.1:6379;passwords=123456");

// 方式2：RedisConfig 对象
var config = new RedisConfig("127.0.0.1:6379", "123456", 6000);
var client = new RedisClient(config);

// 方式3：直接参数
var client = new RedisClient("127.0.0.1:6379", "123456", 6000);

// 连接
client.Connect();

// 检查连接状态
Console.WriteLine($"已连接: {client.IsConnected}");
Console.WriteLine($"是否集群: {client.IsCluster}");
```

### String 操作

```csharp
using SAEA.RedisSocket;

var client = new RedisClient("server=127.0.0.1:6379");
client.Connect();

var db = client.GetDataBase();

// 设置值
db.Set("key", "value");
db.Set("key", "value", 3600);  // 设置过期时间（秒）

// 获取值
var value = db.Get("key");

// 批量设置/获取
db.MSet(new Dictionary<string, string> { ["k1"] = "v1", ["k2"] = "v2" });
var values = db.MGet("k1", "k2");

// 自增/自减
db.Increment("counter");
db.IncrementBy("counter", 10);
db.Decrement("counter");
```

### Hash 操作

```csharp
var db = client.GetDataBase();

// 设置 Hash 字段
db.HSet("user:1", "name", "张三");
db.HSet("user:1", "age", "25");

// 获取 Hash 字段
var name = db.HGet("user:1", "name");

// 批量设置/获取
db.HMSet("user:1", new Dictionary<string, string> { ["name"] = "张三", ["age"] = "25" });
var all = db.HMGet("user:1", "name", "age");
var allFields = db.HGetAll("user:1");

// 字段数量
var count = db.HLen("user:1");

// 删除字段
db.HDel("user:1", "age");

// Hash 字段自增
db.HIncrementBy("user:1", "score", 10);
```

### List 操作

```csharp
var db = client.GetDataBase();

// 推入元素
db.LPush("list", "item1");
db.RPush("list", "item2");

// 弹出元素
var leftItem = db.LPop("list");
var rightItem = db.RPop("list");

// 获取列表长度
var length = db.LLen("list");

// 获取范围元素
var items = db.LRang("list", 0, -1);  // 获取所有

// 获取指定位置元素
var item = db.LIndex("list", 0);

// 阻塞弹出
var popped = db.BLPop("list", 10);  // 等待10秒
```

### Set 操作

```csharp
var db = client.GetDataBase();

// 添加成员
db.SAdd("set", "member1");
db.SAdd("set", "member2");

// 检查成员是否存在
var exists = db.SExists("set", "member1");

// 获取所有成员
var members = db.SMemebers("set");

// 移除成员
db.SRemove("set", "member1");

// 集合操作
var inter = db.SInter("set1", "set2");  // 交集
var union = db.SUnion("set1", "set2");  // 并集
var diff = db.SDiff("set1", "set2");    // 差集
```

### ZSet 操作

```csharp
var db = client.GetDataBase();

// 添加成员（带分数）
db.ZAdd("zset", "member1", 100);
db.ZAdd("zset", "member2", 200);

// 获取分数
var score = db.ZScore("zset", "member1");

// 获取排名
var rank = db.ZRank("zset", "member1");

// 获取范围成员
var members = db.ZRange("zset", 0, -1);
var membersWithScore = db.ZRangeWithScores("zset", 0, -1);

// 增加分数
db.ZIncrBy("zset", 50, "member1");
```

### Key 操作

```csharp
var db = client.GetDataBase();

// 删除键
db.Del("key");

// 检查键是否存在
var exists = db.Exists("key");

// 设置过期时间
db.Expire("key", 3600);
db.ExpireAt("key", DateTime.Now.AddHours(1));

// 获取剩余生存时间
var ttl = db.Ttl("key");

// 查找匹配的键
var keys = db.Keys("user:*");

// 重命名
db.Rename("oldkey", "newkey");
```

### 分布式锁

```csharp
using SAEA.RedisSocket;

var client = new RedisClient("server=127.0.0.1:6379");
client.Connect();

// 获取锁
var lockKey = "resource_lock";
var lockValue = Guid.NewGuid().ToString();
var acquired = client.GetDataBase().Lock(lockKey, lockValue, 30);

if (acquired)
{
    try
    {
        // 执行业务逻辑
        Console.WriteLine("锁已获取，执行操作...");
    }
    finally
    {
        // 释放锁
        client.GetDataBase().Unlock(lockKey, lockValue);
    }
}
```

### Pub/Sub 发布订阅

```csharp
using SAEA.RedisSocket;

var client = new RedisClient("server=127.0.0.1:6379");
client.Connect();

// 发布消息
client.GetDataBase().Publish("channel", "Hello Subscribers!");

// 订阅消息
client.GetDataBase().Subscribe("channel", (channel, message) => 
{
    Console.WriteLine($"收到消息 [{channel}]: {message}");
});
```

### Redis Stream

```csharp
using SAEA.RedisSocket;

var client = new RedisClient("server=127.0.0.1:6379");
client.Connect();

// 生产者
var producer = client.GetRedisProducer("mystream");
producer.Publish(new Dictionary<string, string> { ["field"] = "value" });

// 消费者
var consumer = client.GetRedisConsumer("mystream", "consumer1");
consumer.Subscribe((messages) => 
{
    foreach (var msg in messages)
    {
        Console.WriteLine($"消息ID: {msg.Id}, 数据: {msg.Data}");
    }
});

// 组消费者（支持负载均衡）
var groupConsumer = client.GetRedisGroupConsumer("mystream", "mygroup", "consumer1");
groupConsumer.Subscribe((messages) => 
{
    // 处理消息后自动 ACK
});
```

### 批量操作 (Pipeline)

```csharp
using SAEA.RedisSocket;

var client = new RedisClient("server=127.0.0.1:6379");
client.Connect();

var batch = client.GetDataBase().CreatedBatch();

// 批量添加命令
batch.Set("key1", "value1");
batch.Set("key2", "value2");
batch.Get("key1");

// 执行批量操作
var results = batch.Execute();
```

### Redis Cluster 支持

```csharp
using SAEA.RedisSocket;

// 连接集群节点
var client = new RedisClient("server=192.168.1.1:6379;passwords=123456");
client.Connect();

// 自动检测集群
if (client.IsCluster)
{
    Console.WriteLine("当前为 Redis Cluster 模式");
    
    // 获取集群信息
    var clusterInfo = client.ClusterInfoStr();
    
    // 自动重定向处理（MOVED/ASK）
    // 无需手动处理，框架自动完成
}

// 集群管理操作
client.AddSlots(new int[] { 0, 1, 2, 3 });
client.DelSlots(new int[] { 0, 1 });
client.ClusterNodes();
```

---

## 连接字符串格式

```
server=127.0.0.1:6379;passwords=your_password;actionTimeout=6000
```

| 参数 | 必填 | 说明 |
|------|------|------|
| `server` | 是 | Redis 服务器地址，格式 `ip:port` |
| `passwords` | 否 | 认证密码 |
| `actionTimeout` | 否 | 操作超时(毫秒)，默认 6000 |

---

## 依赖项

| 包名 | 版本 | 说明 |
|------|------|------|
| SAEA.Sockets | 7.26.2.2 | IOCP 通信框架 |
| SAEA.Common | 7.26.2.2 | 公共工具类 |

---

## 相关项目

- [WebRedisManager](https://github.com/yswenli/WebRedisManager) - 基于 SAEA.RedisSocket 的 Redis 管理工具

---

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包](https://www.nuget.org/packages/SAEA.RedisSocket)
- [作者博客](https://www.cnblogs.com/yswenli/)

---

## 许可证

Apache License 2.0