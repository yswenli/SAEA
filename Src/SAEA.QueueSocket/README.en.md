# SAEA.QueueSocket - High-Performance In-Memory Message Queue 📊

[![NuGet version](https://img.shields.io/nuget/v/SAEA.QueueSocket.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.QueueSocket)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**English Version** | **[中文版](README.md)**

> High-performance in-memory message queue based on SAEA.Sockets, using publish/subscribe pattern, supporting tens of thousands of concurrent message distributions.

## Quick Navigation 🧭

| Section | Content |
|---------|---------|
| [⚡ 30-Second Quick Start](#30-second-quick-start) | Server + Producer + Consumer Example |
| [🎯 Core Features](#core-features) | Main Framework Features |
| [📐 Architecture Design](#architecture-design) | Pub/Sub Flow Diagram |
| [💡 Use Cases](#use-cases) | When to Choose SAEA.QueueSocket |
| [📊 Performance Comparison](#performance-comparison) | vs Redis Pub/Sub |
| [❓ FAQ](#faq) | Frequently Asked Questions |
| [🔧 Core Classes](#core-classes) | Main Classes Overview |
| [📝 Usage Examples](#usage-examples) | Detailed Code Examples |

---

## 30-Second Quick Start ⚡

The fastest way to get started - run a message queue system in 3 steps:

### Step 1: Install NuGet Package

```bash
dotnet add package SAEA.QueueSocket
```

### Step 2: Start Queue Server

```csharp
using SAEA.QueueSocket;

var server = new QServer(port: 39654);
server.Start();
Console.WriteLine("Message queue server started!");
```

### Step 3a: Create Producer to Publish Messages

```csharp
var producer = new Producer("producer_1", "127.0.0.1:39654");
producer.Connect();
producer.Publish("orders", "Order data: {id: 123}");
```

### Step 3b: Create Consumer to Subscribe Messages

```csharp
var consumer = new Consumer("consumer_1", "127.0.0.1:39654");
consumer.OnMessage += (msg) => 
    Console.WriteLine($"Received: {Encoding.UTF8.GetString(msg.Data)}");
consumer.Connect();
consumer.Subscribe("orders");
```

**That's it!** 🎉 You've implemented a high-performance publish/subscribe message queue system.

---

## Core Features 🎯

| Feature | Description | Benefit |
|---------|-------------|---------|
| 🚀 **IOCP High Performance** | Based on SAEA.Sockets completion ports | Supports tens of thousands of concurrent connections |
| 📮 **Publish/Subscribe Pattern** | Standard Pub/Sub message queue | Decouples producers and consumers |
| 📂 **Topic Routing** | Message distribution by topic | Flexible message categorization |
| 👥 **Multiple Subscribers** | Multiple subscribers per topic | Message broadcast distribution |
| 💓 **Heartbeat Keep-Alive** | Ping/Pong heartbeat mechanism | Automatic connection status detection |
| ⚡ **Batch Processing** | Batcher optimizes throughput | Efficient message batching |
| 🔒 **Connection Management** | Max connections and message backlog limits | Prevents resource exhaustion |
| 💾 **In-Memory Queue** | MessageQueue in-memory storage | No external dependencies |

---

## Architecture Design 📐

### Pub/Sub Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                  SAEA.QueueSocket Architecture              │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌────────────┐                    ┌────────────┐           │
│  │  Producer  │                    │  Producer  │           │
│  │            │                    │            │           │
│  └─────┬──────┘                    └─────┬──────┘           │
│        │                                 │                  │
│        │ Publish(topic, msg)             │                  │
│        │                                 │                  │
│        └──────────────┬──────────────────┘                  │
│                       │                                     │
│                       ▼                                     │
│              ┌─────────────────┐                            │
│              │     QServer     │                            │
│              │                 │                            │
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
│   │        │  │        │  │        │                        │
│   └────────┘  └────────┘  └────────┘                        │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### Message Flow Process

```
Message Publishing Flow:

Producer ──► Publish(topic, msg) ──► QServer Receives
                                            │
                                            ▼
                                     ┌─────────────┐
                                     │   Exchange   │
                                     │   Routing    │
                                     └─────────────┘
                                            │
                                            ▼
                                     ┌─────────────┐
                                     │ MessageQueue│
                                     │   Enqueue   │
                                     └─────────────┘
                                            │
                                            ▼
                                     ┌─────────────┐
                                     │  Topic Match│
                                     │ Subscriber  │
                                     │    List     │
                                     └─────────────┘
                                            │
                         ┌──────────────────┼──────────────────┐
                         │                  │                  │
                         ▼                  ▼                  ▼
                    Consumer A        Consumer B        Consumer C
                    OnMessage()       OnMessage()       OnMessage()


Consumer Subscription Flow:

Consumer ──► Connect() ──► QServer Establishes Connection
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
                            │  Register   │
                            │ Subscription│
                            └─────────────┘
                                   │
                                   ▼
                            Wait for Message
                                   Push
                                   │
                                   ▼
                            OnMessage() Triggered
```

---

## Use Cases 💡

### ✅ Scenarios Suitable for SAEA.QueueSocket

| Scenario | Description | Recommendation Reason |
|----------|-------------|----------------------|
| 📈 **Real-time Data Push** | Stock quotes, sports scores | High throughput, low latency |
| 🔔 **Event Notification** | System events, order status changes | Decouples system modules |
| 🏢 **System Decoupling** | Asynchronous communication between microservices | Pub/Sub pattern naturally decouples |
| 📋 **Log Collection** | Distributed log aggregation | Topic categorization, multiple subscribers |
| 📢 **Message Broadcasting** | Multi-client synchronized notifications | One-to-many message distribution |
| 🎮 **Game Servers** | Player state synchronization | IOCP high concurrency support |

### ❌ Unsuitable Scenarios

| Scenario | Recommended Alternative |
|----------|------------------------|
| Message persistence required | RabbitMQ, Kafka |
| Cross-process message queue | Redis Stream |
| Message acknowledgment mechanism needed | RabbitMQ |

---

## Performance Comparison 📊

### vs Redis Pub/Sub

| Metric | SAEA.QueueSocket | Redis Pub/Sub | Advantage |
|--------|------------------|---------------|-----------|
| **Dependencies** | Pure memory, no external dependencies | Requires Redis server | **Simpler deployment** |
| **Latency** | ~0.5ms | ~2-5ms (network) | **Lower latency** |
| **Throughput** | 100,000+ msg/s | 50,000+ msg/s | **Higher throughput** |
| **Topic Support** | ✅ Full support | ✅ Full support | Same |
| **Message Persistence** | In-memory queue | Not persisted | Same |
| **Multiple Subscribers** | ✅ Supported | ✅ Supported | Same |
| **Deployment Complexity** | Simple | Medium | **Simpler** |

### Performance Test Data

| Test Scenario | Connections | Messages/sec | Avg Latency |
|--------------|-------------|--------------|-------------|
| Single Topic Single Subscriber | 2 | 120,000 | 0.3ms |
| Single Topic 10 Subscribers | 11 | 100,000 | 0.5ms |
| 10 Topics 10 Subscribers Each | 101 | 80,000 | 0.8ms |

> 💡 **Note**: Test environment: Intel i7-10700, 32GB RAM, .NET 6.0

---

## FAQ ❓

### Q1: What's the difference between SAEA.QueueSocket and Redis Pub/Sub?

**A**: Main differences:
- **Deployment**: SAEA.QueueSocket requires no external service, embedded in application; Redis requires separate deployment
- **Latency**: SAEA.QueueSocket uses direct memory connection, lower latency (~0.5ms vs ~2-5ms)
- **Persistence**: Both are in-memory queues, messages lost on restart
- **Use Cases**: SAEA.QueueSocket suitable for single-machine high-performance scenarios; Redis suitable for distributed scenarios

### Q2: How to ensure messages are not lost?

**A**: SAEA.QueueSocket is designed as a high-performance in-memory queue, messages are stored in memory. For message persistence:
- Use message queues with persistence support like RabbitMQ, Kafka
- Or implement message acknowledgment and retry mechanism at business layer

### Q3: Is there a limit on Topic count?

**A**: There's no hard limit on Topic count, determined by system memory. Recommendations:
- Single Topic subscriber count not exceeding 100
- Total Topics not exceeding 1000
- Periodically clean up unused Topics

### Q4: How to handle consumer disconnection and reconnection?

**A**: Consumer supports event notifications:

```csharp
consumer.OnDisconnected += () => 
{
    Console.WriteLine("Connection lost, reconnecting...");
    Task.Delay(1000).Wait();
    consumer.Connect();
    consumer.Subscribe("orders");
};
```

### Q5: How to handle message backlog?

**A**: 
- `QServer` supports configuration of max message backlog
- Monitor `CalcInfo` callback to get queue status
- Increase consumer count to improve consumption speed
- Use `Batcher` for batch processing to improve efficiency

### Q6: Is message filtering supported?

**A**: Topic-level message filtering is supported:
- Consumers only subscribe to Topics of interest
- One consumer can subscribe to multiple Topics
- Use `Unsubscribe()` to cancel subscription

### Q7: How to view server status?

**A**: Use the `CalcInfo` method:

```csharp
server.CalcInfo((sessionCount, topicCount, queuedCount) => 
{
    Console.WriteLine($"Connections: {sessionCount}");
    Console.WriteLine($"Topics: {topicCount}");
    Console.WriteLine($"Pending messages: {queuedCount}");
});
```

---

## Core Classes 🔧

| Class | Description |
|-------|-------------|
| `QServer` | Queue server, manages connections and message distribution |
| `QClient` | Queue client base class, supports publishing and subscribing |
| `Producer` | Producer wrapper class, simplifies message publishing |
| `Consumer` | Consumer wrapper class, simplifies message subscription |
| `MessageQueue` | Topic-based message queue storage |
| `Exchange` | Message exchange core class, responsible for routing |
| `QueueMsg` | Queue message entity |
| `QueueSocketMsgType` | Message type enumeration |

---

## Usage Examples 📝

### Complete Server Example

```csharp
using SAEA.QueueSocket;

var server = new QServer(port: 39654, maxConnections: 1000, maxQueued: 10000);
server.Start();

server.CalcInfo((sessionCount, topicCount, queuedCount) => 
{
    Console.WriteLine($"[Stats] Connections: {sessionCount}, Topics: {topicCount}, Queue: {queuedCount}");
});

Console.WriteLine("Queue server started, port: 39654");
Console.ReadLine();
```

### Complete Producer Example

```csharp
using SAEA.QueueSocket;

var producer = new Producer("producer_001", "127.0.0.1:39654");

producer.OnError += (ex) => Console.WriteLine($"Error: {ex.Message}");
producer.OnDisconnected += () => Console.WriteLine("Disconnected");

producer.Connect();

while (true)
{
    var message = Console.ReadLine();
    if (message == "exit") break;
    
    producer.Publish("orders", message);
    Console.WriteLine("Message published");
}

producer.Close();
```

### Complete Consumer Example

```csharp
using SAEA.QueueSocket;
using System.Text;

var consumer = new Consumer("consumer_001", "127.0.0.1:39654");

consumer.OnMessage += (msg) => 
{
    var content = Encoding.UTF8.GetString(msg.Data);
    Console.WriteLine($"[Received] Topic: {msg.Topic}, Message: {content}");
};

consumer.OnError += (ex) => Console.WriteLine($"Error: {ex.Message}");
consumer.OnDisconnected += () => 
{
    Console.WriteLine("Disconnected, reconnecting in 5 seconds...");
    Task.Delay(5000).Wait();
    consumer.Connect();
    consumer.Subscribe("orders");
};

consumer.Connect();
consumer.Subscribe("orders");
consumer.Subscribe("notifications");

Console.WriteLine("Consumer started, waiting for messages...");
Console.ReadLine();

consumer.Close();
```

### Using QClient Base Class (Both Publish and Subscribe)

```csharp
using SAEA.QueueSocket;
using System.Text;

var client = new QClient("client_001", "127.0.0.1:39654");

client.OnMessage += (msg) => 
    Console.WriteLine($"Received: {Encoding.UTF8.GetString(msg.Data)}");
client.OnError += (ex) => Console.WriteLine($"Error: {ex.Message}");
client.OnDisconnected += () => Console.WriteLine("Disconnected");

client.Connect();

client.Subscribe("topic1");
client.Publish("topic2", "Hello from client");

client.Unsubscribe("topic1");
client.Close();
```

---

## Message Types

```csharp
public enum QueueSocketMsgType : byte
{
    Ping = 1,       // Heartbeat request
    Pong = 2,       // Heartbeat response
    Publish = 3,    // Publish message
    Subcribe = 4,   // Subscribe request
    Unsubcribe = 5, // Unsubscribe
    Close = 6,      // Close connection
    Data = 7        // Data message
}
```

---

## Topic Features

| Feature | Description |
|---------|-------------|
| **Dynamic Creation** | Topics created on-demand, no pre-definition needed |
| **Multiple Subscribers** | Same Topic can have multiple subscribers |
| **Message Isolation** | Each Topic has its own message queue |
| **Broadcast Distribution** | Messages broadcast to all Topic subscribers (except sender) |

---

## Dependencies

| Package | Version | Description |
|---------|---------|-------------|
| SAEA.Sockets | 7.26.2.2 | IOCP communication framework |
| SAEA.Common | 7.26.2.2 | Common utilities |

---

## More Resources

- [GitHub Repository](https://github.com/yswenli/SAEA)
- [NuGet Package](https://www.nuget.org/packages/SAEA.QueueSocket)
- [Author's Blog](https://www.cnblogs.com/yswenli/)

---

## License

Apache License 2.0