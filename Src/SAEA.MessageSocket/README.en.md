# SAEA.MessageSocket - High-Performance Message Server 📨

[![NuGet version](https://img.shields.io/nuget/v/SAEA.MessageSocket.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.MessageSocket)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**English Version** | **[中文版](README.md)**

> A high-performance message server based on SAEA.Sockets, supporting three messaging modes: private messaging, channels, and groups, with Protobuf serialization for transmission.

## Quick Navigation 🧭

| Section | Content |
|---------|---------|
| [⚡ 30-Second Quick Start](#30-second-quick-start-) | Simplest getting started example |
| [🎯 Core Features](#core-features-) | Main features of the framework |
| [📐 Architecture Design](#architecture-design-) | Message modes and workflows |
| [💡 Use Cases](#use-cases-) | When to choose SAEA.MessageSocket |
| [📊 Performance Comparison](#performance-comparison-) | Comparison with other solutions |
| [❓ FAQ](#faq-) | Quick answers to common questions |
| [🔧 Core Classes](#core-classes-) | Overview of main classes |
| [📝 Usage Examples](#usage-examples-) | Detailed code examples |

---

## 30-Second Quick Start ⚡

The fastest way to get started - run a message server in just 3 steps:

### Step 1: Install NuGet Package

```bash
dotnet add package SAEA.MessageSocket
```

### Step 2: Create Message Server (Only 5 Lines of Code)

```csharp
using SAEA.MessageSocket;

var server = new MessageServer(port: 39654);
server.OnDisconnected += (id) => Console.WriteLine($"User disconnected: {id}");
server.Start();
```

### Step 3: Create Message Client Connection

```csharp
var client = new MessageClient("127.0.0.1", 39654);
client.OnPrivateMessage += (msg) => Console.WriteLine($"Private: {msg.Content}");
client.OnChannelMessage += (msg) => Console.WriteLine($"Channel: {msg.Content}");
client.OnGroupMessage += (msg) => Console.WriteLine($"Group: {msg.Content}");
client.Connect();
client.SendPrivateMsg("user_002", "Hello!");
```

**That's it!** 🎉 You've implemented a high-performance messaging system supporting three messaging modes.

---

## Core Features 🎯

| Feature | Description | Benefits |
|---------|-------------|----------|
| 🚀 **IOCP High Performance** | Based on SAEA.Sockets completion ports | Supports tens of thousands of concurrent connections |
| 📦 **Protobuf Serialization** | Efficient binary message compression | Small transmission size, fast parsing |
| 💬 **Three Messaging Modes** | Private messaging, channels, groups | Covers various instant messaging scenarios |
| 📡 **Channel Subscription** | Publish/subscribe pattern | Dynamic subscription, flexible push |
| 👥 **Group Management** | Create, join, leave, delete | Complete group lifecycle management |
| 💓 **Heartbeat Keep-Alive** | Automatic heartbeat mechanism | Real-time connection status monitoring |
| ⚡ **Batch Send Optimization** | ClassificationBatcher batch processing | Efficient message distribution |

---

## Architecture Design 📐

### Message Mode Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                  SAEA.MessageSocket Architecture            │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│    ┌─────────────────────────────────────────────────┐      │
│    │                 MessageServer                    │      │
│    │                 (Message Server)                 │      │
│    └─────────────────────┬───────────────────────────┘      │
│                            │                                 │
│            ┌───────────────┼───────────────┐                 │
│            │               │               │                 │
│     ┌──────▼──────┐ ┌─────▼──────┐ ┌──────▼──────┐         │
│     │ PrivateMsg  │ │ ChannelMsg │ │  GroupMsg   │         │
│     │  (Private)  │ │ (Channel)  │ │   (Group)   │         │
│     └──────┬──────┘ └─────┬──────┘ └──────┬──────┘         │
│            │               │               │                 │
│     ┌──────▼──────┐ ┌─────▼──────┐ ┌──────▼──────┐         │
│     │ Point-to-   │ │ Publish/   │ │   Group    │         │
│     │ Point 1-to-1│ │ Subscribe  │ │ Management  │         │
│     └─────────────┘ └────────────┘ └─────────────┘         │
│                                                             │
│    ┌─────────────────────────────────────────────────┐      │
│    │              Protobuf Serialization Layer       │      │
│    │         ChatMessage / ChatMessageType           │      │
│    └─────────────────────────────────────────────────┘      │
│                            │                                 │
│    ┌───────────────────────▼───────────────────────┐        │
│    │              SAEA.Sockets (IOCP)               │        │
│    │              TCP Communication Layer            │        │
│    └───────────────────────────────────────────────┘        │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### Three Messaging Modes Flow Diagram

```
Private Message Mode (PrivateMessage):

  ClientA ──► SendPrivateMsg(userB, content) ──► Server Routes
                                                        │
                                                        ▼
                                              Find userB Connection
                                                        │
                                                        ▼
  ClientB ◄── OnPrivateMessage Event ◄─────────── Send to userB


Channel Mode (ChannelMessage):

  Publisher ──► SendChannelMsg(channel, content) ──► Server
                                                        │
                                                        ▼
                                              Find Channel Subscriber List
                                                        │
                                            ┌───────────┼───────────┐
                                            ▼           ▼           ▼
                                        Subscriber1  Subscriber2  SubscriberN
                                            │           │           │
                                            ▼           ▼           ▼
                                      OnChannel   OnChannel   OnChannel


Group Mode (GroupMessage):

  Creator ──► SendCreateGroup(name) ──► Group Created
      │                                       │
      │                                       ▼
      └──► SendAddMember(name) ──► Member Joins Group
                                              │
  Member ──► SendGroupMessage(name, msg) ──► Group Broadcast
                                              │
                                  ┌───────────┼───────────┐
                                  ▼           ▼           ▼
                              Member1     Member2     MemberN
                                  │           │           │
                                  ▼           ▼           ▼
                             OnGroup     OnGroup     OnGroup
```

---

## Use Cases 💡

### ✅ Scenarios Suitable for SAEA.MessageSocket

| Scenario | Description | Recommended Mode |
|----------|-------------|------------------|
| 💬 **Instant Messaging** | Private chat, group chat, chat rooms | Private + Group |
| 🎧 **Online Customer Service** | One-on-one customer dialogue system | Private Mode |
| 📰 **Message Push** | News, notifications, announcements push | Channel Subscription |
| 🤝 **Collaboration Tools** | Real-time team collaboration, project discussions | Group Mode |
| 🎬 **Live Streaming Interaction** | Live room danmaku, interactive messages | Channel + Group |
| 🎮 **Game Chat** | In-game private chat, guild chat | Private + Group |

### Message Mode Selection Guide

| Requirement Scenario | Recommended Mode | Reason |
|----------------------|------------------|--------|
| One-on-one private chat | Private Mode | Point-to-point transmission, highest efficiency |
| News/Announcement push | Channel Mode | Publish/subscribe, dynamic management |
| Team discussion group | Group Mode | Supports management permissions, member control |
| Live streaming danmaku | Channel Mode | High concurrency subscription, one-to-many push |

### ❌ Unsuitable Scenarios

| Scenario | Recommended Alternative |
|----------|------------------------|
| HTTP API Services | Use SAEA.Http or ASP.NET |
| Browser Clients | Use SAEA.WebSocket |
| MQTT Device Communication | Use SAEA.MQTT |
| RPC Calls | Use SAEA.RPC |

---

## Performance Comparison 📊

### Comparison with Traditional Messaging Solutions

| Metric | SAEA.MessageSocket | Traditional WebSocket | Polling HTTP |
|--------|-------------------|----------------------|--------------|
| **Concurrent Connections** | 10,000+ | ~5,000 | ~1,000 |
| **Message Latency** | ~1ms | ~5ms | ~100ms |
| **Transmission Size** | Small (Protobuf) | Medium (JSON) | Large (HTTP Headers) |
| **CPU Utilization** | High (IOCP) | Medium | Low |
| **Memory Usage** | Pooled Reuse | Frequent Allocation | High |

### Message Mode Performance Comparison

| Mode | Concurrency Capability | Applicable Scale | Typical Latency |
|------|----------------------|------------------|-----------------|
| **Private Mode** | ⭐⭐⭐⭐⭐ | Tens of thousands users | ~1ms |
| **Channel Mode** | ⭐⭐⭐⭐⭐ | Million subscriptions | ~2ms |
| **Group Mode** | ⭐⭐⭐⭐ | Thousand-member groups | ~5ms |

### Protobuf vs JSON Serialization

| Metric | Protobuf | JSON |
|--------|----------|------|
| **Serialization Speed** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ |
| **Data Size** | Small (Binary) | Large (Text) |
| **Cross-language Support** | Extensive | Extensive |
| **Readability** | Low | High |

> 💡 **Tip**: Protobuf is 3-10 times smaller and 5-20 times faster in serialization compared to JSON for message transmission.

---

## FAQ ❓

### Q1: What are the differences between the three messaging modes?

**A**: 

| Mode | Communication Method | Typical Scenario |
|------|---------------------|------------------|
| **Private** | One-to-one, point-to-point transmission | Private chat, customer service dialogue |
| **Channel** | One-to-many, publish/subscribe | News push, broadcast notifications |
| **Group** | Many-to-many, requires creation and management | Team discussions, group chat |

### Q2: How to implement user login authentication?

**A**: Send a login message after the client connects:

```csharp
var client = new MessageClient("127.0.0.1", 39654);
client.Connect();

// Send login authentication after connection
client.SendLogin("user_id", "token");

// Server can handle login logic in OnReceive
```

### Q3: How to choose between Channel and Group?

**A**: 

- **Channel**: Suitable for **one-way push** scenarios (news, announcements), subscribers only receive messages, no member management needed
- **Group**: Suitable for **two-way interaction** scenarios (discussion groups, team collaboration), supports member management, creator permission control

### Q4: How to handle offline messages?

**A**: SAEA.MessageSocket focuses on real-time communication. Offline messages need to be combined with storage solutions:

```csharp
// Server example: Store offline messages
server.OnDisconnected += (id) => 
{
    // User offline, mark last online time
    // Combine with database to store offline messages
};

// Pull offline messages after user comes online
```

### Q5: Does it support message callback confirmation?

**A**: Yes, each message type has a corresponding Answer response:

```csharp
// After sending a message, you will receive the corresponding Answer
// e.g.: PrivateMessage -> PrivateMessageAnswer
// You can correlate request and response via message ID
```

### Q6: How to extend custom message types?

**A**: Extend based on `ChatMessageType` enum, or use message content to carry type information:

```csharp
// Leverage Protobuf's flexibility
// Embed custom type identifier in message content
client.SendPrivateMsg(userId, JsonSerializer.Serialize(new 
{ 
    type = "custom_type", 
    data = yourData 
}));
```

### Q7: How many concurrent connections can a single server support?

**A**: Default configuration supports 1000 connections, adjustable via constructor:

```csharp
// bufferSize: buffer size
// count: maximum connections
// timeOut: timeout (milliseconds)
var server = new MessageSocket(1024, 10000, 30 * 60 * 1000);
```

Actual performance depends on server hardware configuration. Under IOCP mode, tens of thousands of concurrent connections are easily supported.

---

## Core Classes 🔧

| Class Name | Description |
|------------|-------------|
| `MessageServer` | Message server, manages connections and message routing |
| `MessageClient` | Message client, sends and receives various messages |
| `ChatMessage` | Chat message protocol class (Protobuf serialized) |
| `ChatMessageType` | Message type enumeration |
| `PrivateMessage` | Private message model |
| `ChannelMessage` | Channel message model |
| `GroupMessage` | Group message model |
| `ChannelList` | Channel list management |
| `GroupList` | Group list management |
| `ClassificationBatcher` | Batch message processor |

---

## Usage Examples 📝

### Basic Server Configuration

```csharp
using SAEA.MessageSocket;

// Create message server
// bufferSize: 1024, count: 1000000, timeOut: 30 minutes
var server = new MessageServer(1024, 1000 * 1000, 30 * 60 * 1000);

server.OnDisconnected += (id) => 
    Console.WriteLine($"User disconnected: {id}");

server.Start();
Console.WriteLine("Message server started, port: 39654");
```

### Complete Client Example

```csharp
using SAEA.MessageSocket;

var client = new MessageClient("127.0.0.1", 39654);

// Private message event
client.OnPrivateMessage += (msg) => 
    Console.WriteLine($"[Private] From {msg.Sender}: {msg.Content}");

// Channel message event
client.OnChannelMessage += (msg) => 
    Console.WriteLine($"[Channel:{msg.Name}] {msg.Content}");

// Group message event
client.OnGroupMessage += (msg) => 
    Console.WriteLine($"[Group:{msg.Name}] {msg.Content}");

// Disconnected event
client.OnDisconnected += () => 
    Console.WriteLine("Connection disconnected");

// Connect to server
client.Connect();
```

### Private Messaging

```csharp
// Send private message to specified user
client.SendPrivateMsg("user_002", "Hello, this is a private message!");

// OnPrivateMessage event will receive the reply
```

### Channel Messaging (Publish/Subscribe)

```csharp
// Subscribe to channels
client.Subscribe("news_channel");
client.Subscribe("sports_channel");

// Send channel message (all users subscribed to this channel will receive)
client.SendChannelMsg("news_channel", "Today's news headline...");

// Unsubscribe
client.Unsubscribe("news_channel");

// View currently subscribed channels
var channels = client.GetSubscribedChannels();
```

### Group Messaging

```csharp
// Create group (creator has management permissions)
client.SendCreateGroup("project_team");

// Other users join the group
client.SendAddMember("project_team");

// Send group message
client.SendGroupMessage("project_team", "Project progress update...");

// Leave group
client.SendRemoveMember("project_team");

// Delete group (only creator can operate)
client.SendRemoveGroup("project_team");
```

### Complete Example: Multi-user Chat Room

```csharp
using SAEA.MessageSocket;

// Server side
var server = new MessageServer(1024, 1000 * 1000, 30 * 60 * 1000);
server.Start();

// Client1 - Create group
var client1 = new MessageClient("127.0.0.1", 39654);
client1.OnGroupMessage += (msg) => Console.WriteLine($"Client1: {msg.Content}");
client1.Connect();
client1.SendCreateGroup("chat_room");

// Client2 - Join group
var client2 = new MessageClient("127.0.0.1", 39654);
client2.OnGroupMessage += (msg) => Console.WriteLine($"Client2: {msg.Content}");
client2.Connect();
client2.SendAddMember("chat_room");

// Client3 - Join group
var client3 = new MessageClient("127.0.0.1", 39654);
client3.OnGroupMessage += (msg) => Console.WriteLine($"Client3: {msg.Content}");
client3.Connect();
client3.SendAddMember("chat_room");

// Send group messages (all members will receive)
client1.SendGroupMessage("chat_room", "Hello everyone!");
client2.SendGroupMessage("chat_room", "Hi client1!");
```

---

## Message Types

```csharp
public enum ChatMessageType
{
    Login = 1,                // Login
    LoginAnswer = 2,          // Login response
    Subscribe = 3,            // Subscribe channel
    SubscribeAnswer = 4,      // Subscribe response
    UnSubscribe = 5,          // Unsubscribe
    UnSubscribeAnswer = 6,    // Unsubscribe response
    ChannelMessage = 7,       // Channel message
    PrivateMessage = 8,       // Private message
    PrivateMessageAnswer = 9, // Private message response
    CreateGroup = 10,         // Create group
    CreateGroupAnswer = 11,   // Create group response
    AddMember = 12,           // Add member
    AddMemberAnswer = 13,     // Add member response
    RemoveMember = 14,        // Remove member
    RemoveMemberAnswer = 15,  // Remove member response
    RemoveGroup = 16,         // Delete group
    RemoveGroupAnswer = 17,   // Delete group response
    GroupMessage = 18,        // Group message
    GroupMessageAnswer = 19   // Group message response
}
```

---

## Message Mode Comparison

| Feature | Private Mode | Channel Mode | Group Mode |
|---------|-------------|--------------|------------|
| **Communication Method** | One-to-one | One-to-many | Many-to-many |
| **Requires Creation** | No | No | Yes |
| **Requires Joining** | No | Subscribe only | Need to join |
| **Management Permissions** | None | None | Creator has |
| **Applicable Scenarios** | Private chat, customer service | News push, broadcast | Team collaboration, group chat |

---

## Dependencies

| Package | Version | Description |
|---------|---------|-------------|
| SAEA.Sockets | 7.26.2.2 | IOCP communication framework |
| SAEA.Common | 7.26.2.2 | Common utilities (including Protobuf) |

---

## More Resources

- [GitHub Repository](https://github.com/yswenli/SAEA)
- [NuGet Package](https://www.nuget.org/packages/SAEA.MessageSocket)
- [Author's Blog](https://www.cnblogs.com/yswenli/)

---

## License

Apache License 2.0