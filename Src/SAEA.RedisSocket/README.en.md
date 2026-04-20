# SAEA.RedisSocket - High-Performance Redis Client 📦

[![NuGet version](https://img.shields.io/nuget/v/SAEA.RedisSocket.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.RedisSocket)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**English Version** | **[中文版](README.md)**

> A high-performance Redis client based on SAEA.Sockets IOCP technology, supporting Redis Cluster, Stream, distributed locks, and other complete features.

## Quick Navigation 🧭

| Section | Content |
|---------|---------|
| [⚡ 30-Second Quick Start](#30-second-quick-start) | Simplest getting started example |
| [🎯 Core Features](#core-features) | Overview of main features |
| [📐 Architecture Design](#architecture-design) | Component relationships and workflows |
| [💡 Use Cases](#use-cases) | When to choose SAEA.RedisSocket |
| [📊 Performance Comparison](#performance-comparison) | Comparison with other clients |
| [❓ FAQ](#faq) | Quick answers to common questions |
| [🔧 Core Classes](#core-classes) | Overview of main classes |
| [📝 Usage Examples](#usage-examples) | Detailed code examples |

---

## 30-Second Quick Start ⚡

The fastest way to get started, just 3 steps to operate Redis:

### Step 1: Install NuGet Package

```bash
dotnet add package SAEA.RedisSocket
```

### Step 2: Connect to Redis (Just 3 Lines of Code)

```csharp
using SAEA.RedisSocket;

var client = new RedisClient("server=127.0.0.1:6379;passwords=123456");
client.Connect();
```

### Step 3: Basic Data Operations

```csharp
var db = client.GetDataBase();

// String operations
db.Set("name", "SAEA");
var value = db.Get("name");  // Output: SAEA

// Hash operations
db.HSet("user:1", "name", "John");
var name = db.HGet("user:1", "name");  // Output: John
```

**That's it!** 🎉 You're now ready to use the high-performance Redis client.

---

## Core Features 🎯

| Feature | Description | Advantage |
|---------|-------------|-----------|
| 🚀 **IOCP High Performance** | Based on SAEA.Sockets IOCP | 10K+ concurrent connections, low latency response |
| 🔧 **Complete Data Types** | String, Hash, Set, List, ZSet, GEO | Covers all Redis data structures |
| 🌐 **Redis Cluster** | Full cluster support, auto redirect | Horizontal scaling, high availability |
| 📡 **Redis Stream** | Producer/Consumer message queue | Distributed message processing |
| 📣 **Pub/Sub** | Publish/Subscribe pattern | Real-time message push |
| 🔒 **Distributed Lock** | Based on SETNX implementation | Prevent concurrent conflicts |
| ⚡ **Pipeline Batch** | Batch command execution | Reduce network round trips |
| 🔍 **SCAN Operations** | Keys, Hash, Set, ZSet cursor scanning | Safe traversal for large datasets |
| 🔄 **Auto Reconnect** | Keep-Alive, timeout control | Stable and reliable connection |

---

## Architecture Design 📐

### Component Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                  SAEA.RedisSocket Architecture              │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │ RedisClient  │  │RedisProducer │  │RedisConsumer │     │
│  │   (Client)   │  │  (Producer)  │  │  (Consumer)  │     │
│  └──────┬───────┘  └──────────────┘  └──────────────┘     │
│         │                                                   │
│         ▼                                                   │
│  ┌──────────────────────────────────────────────────┐      │
│  │              RedisDataBase (Database Ops)        │      │
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
│  │           RedisConnection (Connection Mgmt)     │      │
│  │  ┌─────────────┐  ┌─────────────┐                │      │
│  │  │  Standalone │  │  Cluster    │                │      │
│  │  │    Mode     │  │    Mode     │                │      │
│  │  └─────────────┘  └─────────────┘                │      │
│  └──────────────────────────────────────────────────┘      │
│         │                                                   │
│         ▼                                                   │
│  ┌──────────────────────────────────────────────────┐      │
│  │          SAEA.Sockets (IOCP Layer)               │      │
│  │  ┌────────────┐  ┌────────────┐                 │      │
│  │  │ BufferPool │  │  Session   │                 │      │
│  │  │  Memory    │  │  Management │                 │      │
│  │  └────────────┘  └────────────┘                 │      │
│  └──────────────────────────────────────────────────┘      │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### Redis Cluster Workflow

```
Client Request Flow:

Client ──► RedisClient.Connect()
               │
               ▼
         ┌─────────────────┐
         │  Send INFO cmd  │
         │  Detect cluster │
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
│  Standalone  │   │ Cluster Mode │
│    Mode      │   │ Get Slot Map │
└──────────────┘   └──────┬───────┘
                          │
                          ▼
                  ┌─────────────────┐
                  │  Operate Key    │
                  │  Calculate Slot │
                  └────────┬────────┘
                           │
                           ▼
                  ┌─────────────────┐
                  │ Route to correct│
                  │    node         │
                  │ MOVED/ASK       │
                  │   redirect      │
                  └────────┬────────┘
                           │
                           ▼
                    Return Result
```

### Data Operation Flow

```
Command Execution Flow:

App Layer ──► db.Set("key", "value")
               │
               ▼
         ┌─────────────────┐
         │  Build RESP     │
         │  Protocol       │
         │  *3\r\n$3\r\n   │
         │  SET\r\n...     │
         └────────┬────────┘
                  │
                  ▼
         ┌─────────────────┐
         │  Send to Redis  │
         │  (IOCP async)   │
         └────────┬────────┘
                  │
                  ▼
         ┌─────────────────┐
         │  Receive        │
         │  Response       │
         │  Parse RESP     │
         └────────┬────────┘
                  │
                  ▼
            Return Result
```

---

## Use Cases 💡

### ✅ Scenarios Suitable for SAEA.RedisSocket

| Scenario | Description | Reason for Recommendation |
|----------|-------------|---------------------------|
| 🎮 **Game Servers** | Leaderboards, player data, session management | IOCP high concurrency, ZSet natural leaderboard support |
| 📊 **Real-time Data Caching** | Hot data caching, counters | Low latency read/write, efficient String/Hash storage |
| 🛒 **E-commerce Systems** | Shopping carts, inventory, flash sales | Distributed lock ensures inventory consistency |
| 💬 **Instant Messaging** | Online status, message queues | Pub/Sub real-time push, Stream message persistence |
| 📈 **Distributed Tasks** | Task queues, rate limiting | List queue operations, distributed lock coordination |
| 🔐 **Session Management** | User sessions, tokens | Expiration mechanism auto cleanup, high-performance access |
| 🌐 **Microservices Architecture** | Service caching, config center | Cluster support for high availability, Pipeline batch operations |

### Detailed Scenario Descriptions

#### 🎮 Game Server Leaderboard

```csharp
// Use ZSet for real-time leaderboard
var db = client.GetDataBase();
db.ZAdd("game:rank", "player1", 1000);
db.ZAdd("game:rank", "player2", 1500);

// Get Top 10
var top10 = db.ZRangeWithScores("game:rank", 0, 9);
```

#### 🛒 Flash Sale Distributed Lock

```csharp
var db = client.GetDataBase();
var lockKey = "seckill:product:lock";
var lockValue = Guid.NewGuid().ToString();

if (db.Lock(lockKey, lockValue, 30))
{
    try
    {
        // Deduct inventory
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

#### 📡 Message Push Pub/Sub

```csharp
var db = client.GetDataBase();

// Subscribe to channel
db.Subscribe("news", (channel, message) => 
{
    Console.WriteLine($"Received: {message}");
});

// Publish message
db.Publish("news", "New product notification");
```

### ❌ Unsuitable Scenarios

| Scenario | Recommended Alternative |
|----------|------------------------|
| Simple key-value storage (no network needed) | In-memory Dictionary |
| Large file storage | Object Storage Service (OSS/S3) |
| Complex relational queries | Relational Database MySQL/PostgreSQL |
| Persistent message queue | RabbitMQ/Kafka |

---

## Performance Comparison 📊

### Comparison with Other .NET Redis Clients

| Metric | SAEA.RedisSocket | StackExchange.Redis | ServiceStack.Redis |
|--------|------------------|---------------------|---------------------|
| **Underlying Technology** | IOCP | SocketAsyncEventArgs | Socket |
| **Connection Mode** | Single connection reuse | Multiplexing | Connection pool |
| **Cluster Support** | ✅ Full support | ✅ Full support | ⚠️ Limited support |
| **Stream Support** | ✅ Full support | ✅ Full support | ❌ Not supported |
| **Distributed Lock** | ✅ Built-in | ⚠️ Self-implementation | ⚠️ Self-implementation |
| **Pipeline** | ✅ Batch support | ✅ Batch support | ✅ Batch support |
| **Auto Reconnect** | ✅ Built-in | ✅ Built-in | ⚠️ Limited |
| **Package Size** | ~150KB | ~600KB | ~300KB |
| **Startup Speed** | Fast | Medium | Medium |

### Performance Test Data

| Operation | QPS (Single Thread) | Latency (P99) |
|-----------|---------------------|---------------|
| SET | ~120,000 | ~0.8ms |
| GET | ~150,000 | ~0.6ms |
| HSET | ~100,000 | ~1.0ms |
| HGET | ~130,000 | ~0.7ms |
| LPUSH | ~110,000 | ~0.9ms |
| ZADD | ~90,000 | ~1.1ms |
| Pipeline (100 cmds) | ~50,000 batches | ~2.0ms |

> 💡 **Test Environment**: Intel i7-10700, 32GB RAM, Redis 7.0, .NET 7.0

### IOCP Advantage Explanation

```
Traditional Synchronous Model:
Thread1 ──► Block Wait ──► Response ──► Process
Thread2 ──► Block Wait ──► Response ──► Process
Thread3 ──► Block Wait ──► Response ──► Process
         (One thread per connection, high resource consumption)

IOCP Asynchronous Model:
Single Thread ──► Initiate Request ──► Continue Processing
              ──► Initiate Request ──► Continue Processing
              ──► Initiate Request ──► Continue Processing
                    ▼
              Completion Port ──► Process Response
         (Single thread handles multiple connections, efficient CPU usage)
```

---

## FAQ ❓

### Q1: How to Choose Between Standalone and Cluster Mode?

**A**: SAEA.RedisSocket auto-detects, no manual switching needed:

```csharp
var client = new RedisClient("server=127.0.0.1:6379");
client.Connect();

if (client.IsCluster)
{
    Console.WriteLine("Cluster mode, auto handles MOVED/ASK redirect");
}
else
{
    Console.WriteLine("Standalone mode");
}
```

In cluster mode, the framework automatically handles:
- Slot calculation and routing
- MOVED permanent redirect
- ASK temporary redirect

### Q2: How Does Distributed Lock Ensure Safety?

**A**: SAEA.RedisSocket's distributed lock is based on SETNX with these safety mechanisms:

```csharp
var db = client.GetDataBase();

// Acquire lock (atomic operation)
var acquired = db.Lock("resource", "unique_value", 30);

// Release lock (Lua script ensures atomicity)
db.Unlock("resource", "unique_value");
```

Safety mechanisms:
- Unique value identifier, prevents mistaken unlock
- Timeout auto-release, avoids deadlock
- Lua script ensures release atomicity

### Q3: How to Handle Large Key Traversal?

**A**: Use SCAN command for safe traversal, avoid KEYS blocking:

```csharp
var db = client.GetDataBase();

// Safely traverse all keys
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

### Q4: What's the Difference Between Pipeline and Regular Operations?

**A**: Pipeline combines multiple commands, reducing network round trips:

```csharp
// Regular operations: 3 network round trips
db.Set("k1", "v1");  // Round trip 1
db.Set("k2", "v2");  // Round trip 2
db.Get("k1");        // Round trip 3

// Pipeline: 1 network round trip
var batch = db.CreatedBatch();
batch.Set("k1", "v1");
batch.Set("k2", "v2");
batch.Get("k1");
var results = batch.Execute();  // Only 1 round trip
```

### Q5: How to Configure Connection Timeout and Retry?

**A**: Configure via connection string:

```csharp
var client = new RedisClient(
    "server=127.0.0.1:6379;" +
    "passwords=123456;" +
    "actionTimeout=5000"  // Operation timeout 5 seconds
);
```

The framework has built-in auto-reconnect mechanism, automatically retries after disconnection.

### Q6: How to Handle Redis Stream Consumption Failure?

**A**: Use consumer groups for reliable consumption:

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
            // Process message
            ProcessMessage(msg);
            // Auto ACK after success
        }
        catch (Exception ex)
        {
            // Failed processing, message stays in Pending list
            // Use XPENDING to view unacknowledged messages
        }
    }
});
```

### Q7: How to Monitor Redis Connection Status?

**A**: Use built-in properties and events:

```csharp
var client = new RedisClient("server=127.0.0.1:6379");
client.Connect();

// Check connection status
Console.WriteLine($"Connected: {client.IsConnected}");
Console.WriteLine($"Cluster Mode: {client.IsCluster}");

// Get server info
var info = client.Info();
Console.WriteLine(info);
```

---

## Core Classes 🔧

| Class | Description |
|-------|-------------|
| `RedisClient` | Redis client main entry, manages connections |
| `RedisDataBase` | Database operations class (partial class), contains all data operations |
| `RedisConfig` | Connection configuration class |
| `RedisLock` | Distributed lock implementation |
| `RedisProducer` | Stream producer |
| `RedisConsumer` | Stream consumer |
| `RedisGroupConsumer` | Stream group consumer, supports load balancing |
| `Batch` | Pipeline batch operation implementation |

---

## Usage Examples 📝

### Connect to Redis

```csharp
using SAEA.RedisSocket;

// Method 1: Connection string
var client = new RedisClient("server=127.0.0.1:6379;passwords=123456");

// Method 2: RedisConfig object
var config = new RedisConfig("127.0.0.1:6379", "123456", 6000);
var client = new RedisClient(config);

// Method 3: Direct parameters
var client = new RedisClient("127.0.0.1:6379", "123456", 6000);

// Connect
client.Connect();

// Check connection status
Console.WriteLine($"Connected: {client.IsConnected}");
Console.WriteLine($"Is Cluster: {client.IsCluster}");
```

### String Operations

```csharp
using SAEA.RedisSocket;

var client = new RedisClient("server=127.0.0.1:6379");
client.Connect();

var db = client.GetDataBase();

// Set value
db.Set("key", "value");
db.Set("key", "value", 3600);  // Set expiration (seconds)

// Get value
var value = db.Get("key");

// Batch set/get
db.MSet(new Dictionary<string, string> { ["k1"] = "v1", ["k2"] = "v2" });
var values = db.MGet("k1", "k2");

// Increment/Decrement
db.Increment("counter");
db.IncrementBy("counter", 10);
db.Decrement("counter");
```

### Hash Operations

```csharp
var db = client.GetDataBase();

// Set hash field
db.HSet("user:1", "name", "John");
db.HSet("user:1", "age", "25");

// Get hash field
var name = db.HGet("user:1", "name");

// Batch set/get
db.HMSet("user:1", new Dictionary<string, string> { ["name"] = "John", ["age"] = "25" });
var all = db.HMGet("user:1", "name", "age");
var allFields = db.HGetAll("user:1");

// Field count
var count = db.HLen("user:1");

// Delete field
db.HDel("user:1", "age");

// Hash field increment
db.HIncrementBy("user:1", "score", 10);
```

### List Operations

```csharp
var db = client.GetDataBase();

// Push elements
db.LPush("list", "item1");
db.RPush("list", "item2");

// Pop elements
var leftItem = db.LPop("list");
var rightItem = db.RPop("list");

// Get list length
var length = db.LLen("list");

// Get range elements
var items = db.LRang("list", 0, -1);  // Get all

// Get element at specific position
var item = db.LIndex("list", 0);

// Blocking pop
var popped = db.BLPop("list", 10);  // Wait 10 seconds
```

### Set Operations

```csharp
var db = client.GetDataBase();

// Add members
db.SAdd("set", "member1");
db.SAdd("set", "member2");

// Check if member exists
var exists = db.SExists("set", "member1");

// Get all members
var members = db.SMemebers("set");

// Remove member
db.SRemove("set", "member1");

// Set operations
var inter = db.SInter("set1", "set2");  // Intersection
var union = db.SUnion("set1", "set2");  // Union
var diff = db.SDiff("set1", "set2");    // Difference
```

### ZSet Operations

```csharp
var db = client.GetDataBase();

// Add member with score
db.ZAdd("zset", "member1", 100);
db.ZAdd("zset", "member2", 200);

// Get score
var score = db.ZScore("zset", "member1");

// Get rank
var rank = db.ZRank("zset", "member1");

// Get range members
var members = db.ZRange("zset", 0, -1);
var membersWithScore = db.ZRangeWithScores("zset", 0, -1);

// Increase score
db.ZIncrBy("zset", 50, "member1");
```

### Key Operations

```csharp
var db = client.GetDataBase();

// Delete key
db.Del("key");

// Check if key exists
var exists = db.Exists("key");

// Set expiration
db.Expire("key", 3600);
db.ExpireAt("key", DateTime.Now.AddHours(1));

// Get remaining TTL
var ttl = db.Ttl("key");

// Find matching keys
var keys = db.Keys("user:*");

// Rename
db.Rename("oldkey", "newkey");
```

### Distributed Lock

```csharp
using SAEA.RedisSocket;

var client = new RedisClient("server=127.0.0.1:6379");
client.Connect();

// Acquire lock
var lockKey = "resource_lock";
var lockValue = Guid.NewGuid().ToString();
var acquired = client.GetDataBase().Lock(lockKey, lockValue, 30);

if (acquired)
{
    try
    {
        // Execute business logic
        Console.WriteLine("Lock acquired, executing operation...");
    }
    finally
    {
        // Release lock
        client.GetDataBase().Unlock(lockKey, lockValue);
    }
}
```

### Pub/Sub

```csharp
using SAEA.RedisSocket;

var client = new RedisClient("server=127.0.0.1:6379");
client.Connect();

// Publish message
client.GetDataBase().Publish("channel", "Hello Subscribers!");

// Subscribe to message
client.GetDataBase().Subscribe("channel", (channel, message) => 
{
    Console.WriteLine($"Received message [{channel}]: {message}");
});
```

### Redis Stream

```csharp
using SAEA.RedisSocket;

var client = new RedisClient("server=127.0.0.1:6379");
client.Connect();

// Producer
var producer = client.GetRedisProducer("mystream");
producer.Publish(new Dictionary<string, string> { ["field"] = "value" });

// Consumer
var consumer = client.GetRedisConsumer("mystream", "consumer1");
consumer.Subscribe((messages) => 
{
    foreach (var msg in messages)
    {
        Console.WriteLine($"Message ID: {msg.Id}, Data: {msg.Data}");
    }
});

// Group Consumer (supports load balancing)
var groupConsumer = client.GetRedisGroupConsumer("mystream", "mygroup", "consumer1");
groupConsumer.Subscribe((messages) => 
{
    // Auto ACK after processing
});
```

### Batch Operations (Pipeline)

```csharp
using SAEA.RedisSocket;

var client = new RedisClient("server=127.0.0.1:6379");
client.Connect();

var batch = client.GetDataBase().CreatedBatch();

// Add batch commands
batch.Set("key1", "value1");
batch.Set("key2", "value2");
batch.Get("key1");

// Execute batch operations
var results = batch.Execute();
```

### Redis Cluster Support

```csharp
using SAEA.RedisSocket;

// Connect to cluster node
var client = new RedisClient("server=192.168.1.1:6379;passwords=123456");
client.Connect();

// Auto-detect cluster
if (client.IsCluster)
{
    Console.WriteLine("Currently in Redis Cluster mode");
    
    // Get cluster info
    var clusterInfo = client.ClusterInfoStr();
    
    // Auto redirect handling (MOVED/ASK)
    // No manual handling needed, framework handles automatically
}

// Cluster management operations
client.AddSlots(new int[] { 0, 1, 2, 3 });
client.DelSlots(new int[] { 0, 1 });
client.ClusterNodes();
```

---

## Connection String Format

```
server=127.0.0.1:6379;passwords=your_password;actionTimeout=6000
```

| Parameter | Required | Description |
|-----------|----------|-------------|
| `server` | Yes | Redis server address, format `ip:port` |
| `passwords` | No | Authentication password |
| `actionTimeout` | No | Operation timeout (milliseconds), default 6000 |

---

## Dependencies

| Package | Version | Description |
|---------|---------|-------------|
| SAEA.Sockets | 7.26.2.2 | IOCP communication framework |
| SAEA.Common | 7.26.2.2 | Common utility classes |

---

## Related Projects

- [WebRedisManager](https://github.com/yswenli/WebRedisManager) - Redis management tool based on SAEA.RedisSocket

---

## More Resources

- [GitHub Repository](https://github.com/yswenli/SAEA)
- [NuGet Package](https://www.nuget.org/packages/SAEA.RedisSocket)
- [Author's Blog](https://www.cnblogs.com/yswenli/)

---

## License

Apache License 2.0