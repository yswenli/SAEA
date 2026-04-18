# SAEA.RedisSocket - 高性能 Redis 客户端

[![NuGet version](https://img.shields.io/nuget/v/SAEA.RedisSocket.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.RedisSocket)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

## 简介

SAEA.RedisSocket 是一个轻量级、高性能的 Redis 客户端库，基于 SAEA.Sockets 的 IOCP 技术实现。支持 .NET Standard 2.0，提供完整的 Redis 数据类型操作，支持 Redis Cluster 集群，是 .NET 应用访问 Redis 的理想选择。

## 特性

- **完整数据类型** - String、Hash、Set、List、ZSet、GEO
- **Redis Cluster** - 完整集群支持，自动重定向
- **Redis Stream** - Producer/Consumer 消息队列
- **Pub/Sub** - 发布订阅消息
- **分布式锁** - 基于 SETNX 实现分布式锁
- **批量操作** - Pipeline 批量命令
- **SCAN 操作** - Keys、Hash、Set、ZSet 游标扫描
- **自动重连** - Keep-Alive、超时控制

## NuGet 安装

```bash
# Package Manager
Install-Package SAEA.RedisSocket -Version 7.26.2.2

# .NET CLI
dotnet add package SAEA.RedisSocket --version 7.26.2.2
```

## 连接字符串格式

```
server=127.0.0.1:6379;passwords=your_password;actionTimeout=6000
```

| 参数 | 必填 | 说明 |
|------|------|------|
| `server` | 是 | Redis 服务器地址，格式 `ip:port` |
| `passwords` | 否 | 认证密码 |
| `actionTimeout` | 否 | 操作超时(毫秒)，默认 6000 |

## 核心类

| 类名 | 说明 |
|------|------|
| `RedisClient` | Redis 客户端主入口 |
| `RedisDataBase` | 数据库操作类（分部类） |
| `RedisConfig` | 连接配置类 |
| `RedisLock` | 分布式锁实现 |
| `RedisProducer` | Stream 生产者 |
| `RedisConsumer` | Stream 消费者 |
| `RedisGroupConsumer` | Stream 组消费者 |
| `Batch` | 批量操作实现 |

## 快速使用

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

## 依赖项

| 包名 | 版本 | 说明 |
|------|------|------|
| SAEA.Sockets | 7.26.2.2 | IOCP 通信框架 |
| SAEA.Common | 7.26.2.2 | 公共工具类 |

## 相关项目

- [WebRedisManager](https://github.com/yswenli/WebRedisManager) - 基于 SAEA.RedisSocket 的 Redis 管理工具

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包](https://www.nuget.org/packages/SAEA.RedisSocket)
- [作者博客](https://www.cnblogs.com/yswenli/)

## 许可证

Apache License 2.0