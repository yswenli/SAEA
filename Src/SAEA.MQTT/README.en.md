# SAEA.MQTT - MQTT Protocol Server/Client 🤖

[![NuGet version](https://img.shields.io/nuget/v/SAEA.MQTT.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.MQTT)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**English Version** | **[中文版](README.md)**

> High-performance MQTT protocol implementation based on .NET Standard 2.0, using SAEA.Sockets IOCP technology, supporting MQTT 3.1/3.1.1/5.0 protocols, suitable for IoT device communication, message push, and other scenarios.

## Quick Navigation 🧭

| Section | Content |
|------|------|
| [⚡ 30-Second Quick Start](#30-second-quick-start) | Fastest way to get started with Broker + Client |
| [🎯 Core Features](#core-features) | Main features of the framework |
| [📐 Architecture Design](#architecture-design) | Component relationships and message flow |
| [💡 Use Cases](#use-cases) | When to choose SAEA.MQTT |
| [📊 Performance Comparison](#performance-comparison) | QoS level comparison |
| [❓ FAQ](#faq) | Quick answers to common questions |
| [🔧 Core Classes](#core-classes) | Overview of main classes |
| [📝 Usage Examples](#usage-examples) | Detailed code examples |

---

## 30-Second Quick Start ⚡

The fastest way to get started - run MQTT Broker and Client in just 3 steps:

### Step 1: Install NuGet Package

```bash
dotnet add package SAEA.MQTT
```

### Step 2: Create MQTT Broker Server (Just 10 Lines of Code)

```csharp
using SAEA.MQTT;
using SAEA.MQTT.Server;

var factory = new MqttFactory();
var server = factory.CreateMqttServer();

var options = new MqttServerOptionsBuilder()
    .WithDefaultEndpointPort(1883)
    .Build();

server.ClientConnected += (s, e) => Console.WriteLine($"Client connected: {e.ClientId}");
await server.StartAsync(options);
Console.WriteLine("MQTT Broker started, port: 1883");
```

### Step 3: Create MQTT Client Connection

```csharp
var factory = new MqttFactory();
var client = factory.CreateMqttClient();

var options = new MqttClientOptionsBuilder()
    .WithClientId("client_001")
    .WithTcpServer("127.0.0.1", 1883)
    .Build();

client.Connected += async (s, e) => {
    await client.SubscribeAsync("home/#");
    await client.PublishAsync(new MqttApplicationMessageBuilder()
        .WithTopic("home/temperature").WithPayload("25.5").Build());
};

await client.ConnectAsync(options);
```

**That's it!** 🎉 You've implemented a complete MQTT publish/subscribe system.

---

## Core Features 🎯

| Feature | Description | Benefits |
|------|------|------|
| 🚀 **High-Performance Broker** | IOCP async model | Supports tens of thousands of concurrent device connections |
| 📡 **Complete Protocol Support** | MQTT 3.1/3.1.1/5.0 | Compatible with all mainstream MQTT clients |
| 🔒 **TLS/SSL Encryption** | Secure connection support | Encrypted data transmission protection |
| 📊 **Three QoS Levels** | QoS 0/1/2 | Flexible message quality assurance |
| 🔄 **Managed Client** | Auto-reconnect, message queue | Automatic recovery on disconnect, no message loss |
| 🎯 **Message Interceptors** | Connection/message/subscription interception | Flexible permission control and auditing |
| 💾 **Persistent Sessions** | Session state persistence | Offline messages and subscription state retention |
| 🔗 **WebSocket Support** | WS/WSS protocols | Direct browser connection |
| 📨 **Will Messages** | Offline status notification | Automatic notification on abnormal device disconnect |
| 🏷️ **Topic Aliases** | MQTT 5.0 feature | Reduced network overhead |

---

## Architecture Design 📐

### Component Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                     SAEA.MQTT Architecture                  │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────────┐         ┌──────────────────┐        │
│  │   MqttServer     │         │  MqttClient       │        │
│  │   (Broker)       │         │  (Client)        │        │
│  └────────┬─────────┘         └────────┬─────────┘        │
│           │                            │                     │
│           │  ┌────────────────────────┐│                    │
│           │  │   MqttFactory          ││                    │
│           └──►   (Factory)           ◄┘                    │
│              └────────────────────────┘                    │
│                        │                                    │
│     ┌──────────────────┼──────────────────┐               │
│     │                  │                  │                │
│  ┌──▼────────┐   ┌─────▼──────┐   ┌──────▼─────┐         │
│  │ MqttServer │   │ MqttClient │   │ Managed    │         │
│  │ Options    │   │ Options     │   │ MqttClient │         │
│  │ Builder    │   │ Builder     │   │ (Managed) │         │
│  └────────────┘   └─────────────┘   └────────────┘         │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐ │
│  │                    Core Components                  │ │
│  ├──────────────────────────────────────────────────────┤ │
│  │ SessionManager │ RetainedMessages │ Interceptors   │ │
│  └──────────────────────────────────────────────────────┘ │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐ │
│  │                 SAEA.Sockets (IOCP)                  │ │
│  └──────────────────────────────────────────────────────┘ │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### MQTT Message Flow Diagram

```
Publish/Subscribe Flow:

Publisher            Broker              Subscriber
    │                  │                     │
    │  1. Connect      │                     │
    │ ────────────────►│                     │
    │                  │  2. Connect         │
    │                  │◄────────────────────│
    │                  │                     │
    │  3. Publish      │                     │
    │ ────────────────►│                     │
    │     (Topic)      │                     │
    │                  │  4. Forward         │
    │                  │ ───────────────────►│
    │                  │     (Message)       │
    │                  │                     │
    │                  │  5. PubAck (QoS1)   │
    │                  │◄────────────────────│
    │                  │                     │

QoS Level Processing Flow:

QoS 0 - At Most Once:
Publisher ──► Publish ──► Broker ──► Forward ──► Subscriber
            (No confirmation)

QoS 1 - At Least Once:
Publisher ──► Publish ──► Broker ──► Forward ──► Subscriber
     │          │           │            │
     │◄─ PubAck ─┘           │       ┌────┘
     │                       │◄──PubAck──┘

QoS 2 - Exactly Once:
Publisher ──► Publish ──► Broker ──► Forward ──► Subscriber
     │          │           │            │
     │◄─ PubRec ─┤           │       ┌────┘
     │          │           │       │
     ├─ PubRel ─►           │       │
     │          │◄─ PubRec ─┤       │
     │          │           │◄──────┘
     │          ├─ PubRel ─►│
     │          │           ├─ PubComp─►│
     │◄─ PubComp─┤           │       │◄───
```

### Session Management Flow

```
Client Session Lifecycle:

Client Connection Request
        │
        ▼
┌─────────────────┐
│ ConnectionValidator │ ◄── Connection Validation Interceptor
│ (Username/Password) │
└────────┬────────┘
         │
    ┌────▼────┐
    │ Valid?  │
    └────┬────┘
         │
    ┌────┴────┐
    │         │
   Yes        No
    │         │
    ▼         ▼
┌─────────┐  ┌──────────────┐
│ Session │  │ Return Error  │
│ Check   │  │ BadUserName   │
│Persist/ │  │ OrPassword    │
│ Temp    │  └──────────────┘
└────┬────┘
     │
     ▼
┌─────────────────┐
│ SessionManager  │
│ Load/Create     │
│ Session         │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Restore         │
│ Subscriptions   │
│ (Persistent)    │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Send Offline    │
│ Messages        │
│ (QoS 1/2)       │
└────────┬────────┘
         │
         ▼
    Client Ready
```

---

## Use Cases 💡

### ✅ Scenarios Suitable for SAEA.MQTT

| Scenario | Description | Recommendation Reason |
|------|------|----------|
| 🏠 **Smart Home** | Device status sync, remote control | Lightweight protocol, low-power device friendly |
| 🏭 **Industrial IoT** | Sensor data collection, device monitoring | QoS ensures data reliability |
| 🚗 **Connected Vehicles** | Vehicle location reporting, remote diagnostics | Supports mobile network weak connections |
| 📱 **Message Push** | App push notifications, instant messaging | WebSocket support, direct browser connection |
| 📊 **Real-time Data** | Stock quotes, sports scores | Pub/sub pattern, efficient broadcasting |
| 🌡️ **Environmental Monitoring** | Temperature/humidity, air quality reporting | Low bandwidth, suitable for NB-IoT |
| 🏥 **Smart Healthcare** | Medical device data transmission | TLS encryption, secure and reliable |
| 🌾 **Smart Agriculture** | Greenhouse monitoring, irrigation control | Supports large-scale concurrent devices |

### Typical Application Architecture Example

```
┌─────────────────────────────────────────────────────────────┐
│                    Smart Home System                        │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│   ┌─────────┐   ┌─────────┐   ┌─────────┐   ┌─────────┐   │
│   │Temp     │   │ Smart   │   │ Smart   │   │ Door    │   │
│   │Sensor   │   │ Bulb    │   │ Plug    │   │ Lock    │   │
│   └────┬────┘   └────┬────┘   └────┬────┘   └────┬────┘   │
│        │             │             │             │          │
│        └──────────────┼──────────────┼──────────────┘       │
│                       │              │                       │
│                       ▼              ▼                       │
│              ┌─────────────────────────────┐               │
│              │      SAEA.MQTT Broker       │               │
│              │      (Port 1883/8883)       │               │
│              └─────────────┬───────────────┘               │
│                            │                                │
│         ┌──────────────────┼──────────────────┐            │
│         │                  │                  │            │
│         ▼                  ▼                  ▼            │
│   ┌──────────┐      ┌──────────┐      ┌──────────┐        │
│   │ Mobile   │      │ Web       │      │ Data     │        │
│   │ App      │      │ Console   │      │ Analysis │        │
│   │(WebSocket)│     │(WebSocket)│     │(Subscribe)│        │
│   └──────────┘      └──────────┘      └──────────┘        │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### ❌ Unsuitable Scenarios

| Scenario | Recommended Alternative |
|------|--------------|
| Simple HTTP API | Use SAEA.Http or ASP.NET |
| Large file transfer | Use SAEA.Sockets directly |
| Video streaming | Use RTSP/WebRTC |
| Database access | Use SAEA.RedisSocket |

---

## Performance Comparison 📊

### QoS Service Quality Level Comparison

| QoS Level | Level Name | Message Delivery | Reliability | Latency | Bandwidth | Use Case |
|----------|----------|--------------|--------|------|----------|----------|
| **QoS 0** | At Most Once | At most once | ❌ Low | ⭐ Lowest | ⭐ Lowest | Environmental data, log reporting |
| **QoS 1** | At Least Once | At least once | ⚠️ Medium | ⭐⭐ Medium | ⭐⭐ Medium | General business messages |
| **QoS 2** | Exactly Once | Exactly once | ✅ High | ⭐⭐⭐ Higher | ⭐⭐⭐ Higher | Financial transactions, critical commands |

### QoS Detailed Comparison

```
QoS 0: At Most Once
┌─────────┐      Publish       ┌─────────┐      Forward      ┌─────────┐
│Publisher│ ─────────────────► │  Broker  │ ─────────────────► │Subscriber│
└─────────┘    (No ack)         └─────────┘    (No ack)         └─────────┘

Features: Fastest, may be lost
Use case: Sensor data (occasional loss acceptable)

QoS 1: At Least Once
┌─────────┐      Publish       ┌─────────┐      Forward      ┌─────────┐
│Publisher│ ─────────────────► │  Broker  │ ─────────────────► │Subscriber│
└─────────┘                    └─────────┘                    └─────────┘
     ▲                              ▲                              │
     │         PubAck               │         PubAck               │
     └──────────────────────────────┴──────────────────────────────┘

Features: Guaranteed delivery, may duplicate
Use case: General business messages (deduplication required)

QoS 2: Exactly Once
┌─────────┐      Publish       ┌─────────┐      Forward      ┌─────────┐
│Publisher│ ─────────────────► │  Broker  │ ─────────────────► │Subscriber│
└─────────┘                    └─────────┘                    └─────────┘
     ▲                              ▲                              │
     │         PubRec               │         PubRec               │
     │◄─────────────────────────────┴──────────────────────────────┤
     │                                                            │
     │         PubRel                                             │
     └──────────────────────────────►│◄────────────────────────────┤
     ▲                              │         PubRel               │
     │         PubComp              │                              │
     └──────────────────────────────┴─────────────────────────────►

Features: Guaranteed unique, most reliable
Use case: Critical messages, financial transactions
```

### MQTT Protocol Version Comparison

| Feature | MQTT 3.1 | MQTT 3.1.1 | MQTT 5.0 |
|------|----------|------------|----------|
| Basic pub/sub | ✅ | ✅ | ✅ |
| QoS 0/1/2 | ✅ | ✅ | ✅ |
| Will messages | ✅ | ✅ | ✅ |
| Clean session | ✅ | ✅ | ✅ |
| **Reason codes** | ❌ | ❌ | ✅ |
| **User properties** | ❌ | ❌ | ✅ |
| **Topic aliases** | ❌ | ❌ | ✅ |
| **Message expiry** | ❌ | ❌ | ✅ |
| **Session expiry** | ❌ | ❌ | ✅ |
| **Shared subscriptions** | ❌ | ❌ | ✅ |

---

## FAQ ❓

### Q1: How many device connections can the MQTT Broker support?

**A**: SAEA.MQTT is based on SAEA.Sockets IOCP technology, supporting 10,000+ concurrent connections on a single machine. Actual performance depends on:
- Server hardware configuration (CPU, memory)
- Network bandwidth
- Message frequency and size
- QoS level

Recommendation: Use multi-instance cluster deployment in production environments.

### Q2: How to choose between QoS 0/1/2?

**A**: Choose based on business requirements:

| Scenario | Recommended QoS | Reason |
|------|----------|------|
| Sensor temperature data | QoS 0 | Occasional loss acceptable, prioritize real-time |
| Device status sync | QoS 1 | Guaranteed delivery, application-level deduplication |
| Payment commands | QoS 2 | Guaranteed unique, no duplicates allowed |
| Log reporting | QoS 0 | Allow minor loss |
| Alert notifications | QoS 1 | Guaranteed delivery |

### Q3: How to implement device authentication?

**A**: Use connection validation interceptor:

```csharp
var options = new MqttServerOptionsBuilder()
    .WithDefaultEndpointPort(1883)
    .WithConnectionValidator(context => 
    {
        if (context.Username != "device1" || context.Password != "secret123")
        {
            context.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
            return;
        }
        context.ReasonCode = MqttConnectReasonCode.Success;
    })
    .Build();
```

### Q4: How to auto-reconnect after client disconnect?

**A**: Use `ManagedMqttClient` managed client:

```csharp
var factory = new MqttFactory();
var managedClient = factory.CreateManagedMqttClient();

var options = new ManagedMqttClientOptionsBuilder()
    .WithClientOptions(new MqttClientOptionsBuilder()
        .WithClientId("device_001")
        .WithTcpServer("127.0.0.1", 1883)
        .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))  // 5-second reconnect
        .Build())
    .Build();

await managedClient.StartAsync(options);
// Auto-reconnect, messages cached during disconnect, sent after reconnect
```

### Q5: How to implement encrypted message transmission?

**A**: Use TLS/SSL secure connection:

```csharp
// Server certificate configuration
var serverOptions = new MqttServerOptionsBuilder()
    .WithDefaultEndpointPort(8883)
    .WithEncryptedEndpoint()
    .WithEncryptionCertificate(File.ReadAllBytes("server.pfx"))
    .Build();

// Client connection
var clientOptions = new MqttClientOptionsBuilder()
    .WithClientId("secure_client")
    .WithTcpServer("127.0.0.1", 8883)
    .WithTls(new MqttClientTlsOptionsBuilder()
        .WithCertificateValidationCallback((cert, chain, errors) => true)
        .Build())
    .Build();
```

### Q6: How to use topic wildcards?

**A**: MQTT supports two wildcards:

| Wildcard | Description | Example |
|--------|------|------|
| `+` | Single-level wildcard | `home/+/temperature` matches `home/living/temperature` |
| `#` | Multi-level wildcard | `home/#` matches `home/living/temperature`, `home/bedroom/humidity` |

```csharp
// Subscribe to all room temperatures
await client.SubscribeAsync("home/+/temperature");

// Subscribe to all home messages
await client.SubscribeAsync("home/#");
```

### Q7: What is a will message? How to use it?

**A**: A will message is a message automatically published by the Broker when a client disconnects abnormally:

```csharp
var options = new MqttClientOptionsBuilder()
    .WithClientId("device_001")
    .WithTcpServer("127.0.0.1", 1883)
    .WithWillMessage(new MqttApplicationMessageBuilder()
        .WithTopic("device/status")          // Will topic
        .WithPayload("offline")              // Will content
        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
        .WithRetainFlag(true)                // Retain message
        .Build())
    .Build();

// When device disconnects abnormally, Broker automatically publishes "device/status" -> "offline"
```

---

## Core Classes 🔧

### Main Classes

| Class | Description |
|------|------|
| `MqttServer` | MQTT Broker server |
| `MqttClient` | Standard MQTT client |
| `ManagedMqttClient` | Managed client (auto-reconnect, message queue) |
| `MqttFactory` | MQTT object factory class |
| `MqttApplicationMessage` | Application message wrapper |
| `MqttTopicFilter` | Topic filter |

### Configuration Builders

| Class | Description |
|------|------|
| `MqttServerOptionsBuilder` | Server configuration builder |
| `MqttClientOptionsBuilder` | Client configuration builder |
| `ManagedMqttClientOptionsBuilder` | Managed client configuration builder |
| `MqttApplicationMessageBuilder` | Application message builder |
| `MqttTopicFilterBuilder` | Topic filter builder |

### Interceptor Interfaces

| Interface | Description |
|------|------|
| `IMqttServerConnectionValidator` | Connection validation interceptor |
| `IMqttServerApplicationMessageInterceptor` | Message interceptor |
| `IMqttServerSubscriptionInterceptor` | Subscription interceptor |
| `IMqttServerUnsubscriptionInterceptor` | Unsubscription interceptor |

### Protocol Enums

| Enum | Description |
|------|------|
| `MqttProtocolVersion` | Protocol version (V311, V500) |
| `MqttQualityOfServiceLevel` | QoS level (AtMostOnce, AtLeastOnce, ExactlyOnce) |
| `MqttConnectReasonCode` | Connection reason code |
| `MqttRetainHandling` | Retained message handling |

---

## Usage Examples 📝

### MQTT Broker Server

```csharp
using SAEA.MQTT;
using SAEA.MQTT.Server;

var factory = new MqttFactory();
var server = factory.CreateMqttServer();

var options = new MqttServerOptionsBuilder()
    .WithDefaultEndpoint()
    .WithDefaultEndpointPort(1883)
    .WithConnectionBacklog(1000)
    .WithMaxPendingMessagesPerClient(100)
    .Build();

server.ApplicationMessageReceived += (sender, e) => 
{
    var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
    Console.WriteLine($"Message received [ClientId: {e.ClientId}, Topic: {e.ApplicationMessage.Topic}]: {payload}");
};

server.ClientConnected += (sender, e) => 
    Console.WriteLine($"Client connected: {e.ClientId}");

server.ClientDisconnected += (sender, e) => 
    Console.WriteLine($"Client disconnected: {e.ClientId}");

await server.StartAsync(options);
Console.WriteLine("MQTT Broker started, port: 1883");
```

### MQTT Client

```csharp
using SAEA.MQTT;
using SAEA.MQTT.Client;

var factory = new MqttFactory();
var client = factory.CreateMqttClient();

var options = new MqttClientOptionsBuilder()
    .WithClientId("client_001")
    .WithTcpServer("127.0.0.1", 1883)
    .WithCleanSession()
    .WithKeepAlivePeriod(TimeSpan.FromSeconds(60))
    .Build();

client.ApplicationMessageReceived += (sender, e) => 
{
    var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
    Console.WriteLine($"Message received [Topic: {e.ApplicationMessage.Topic}]: {payload}");
};

client.Connected += (sender, e) => Console.WriteLine("Connected");
client.Disconnected += (sender, e) => Console.WriteLine("Disconnected");

await client.ConnectAsync(options);

await client.SubscribeAsync("home/temperature");
await client.SubscribeAsync("home/#");

var message = new MqttApplicationMessageBuilder()
    .WithTopic("home/temperature")
    .WithPayload("25.5")
    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
    .Build();

await client.PublishAsync(message);

await client.DisconnectAsync();
```

### Managed MQTT Client

```csharp
using SAEA.MQTT;
using SAEA.MQTT.Client;

var factory = new MqttFactory();
var managedClient = factory.CreateManagedMqttClient();

var options = new ManagedMqttClientOptionsBuilder()
    .WithClientOptions(new MqttClientOptionsBuilder()
        .WithClientId("managed_client")
        .WithTcpServer("127.0.0.1", 1883)
        .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
        .Build())
    .Build();

await managedClient.StartAsync(options);

await managedClient.PublishAsync(new MqttApplicationMessageBuilder()
    .WithTopic("test/topic")
    .WithPayload("Hello MQTT!")
    .Build());

await managedClient.SubscribeAsync("home/#");
```

### TLS/SSL Secure Connection

```csharp
using SAEA.MQTT.Client;

var options = new MqttClientOptionsBuilder()
    .WithClientId("secure_client")
    .WithTcpServer("127.0.0.1", 8883)
    .WithTls(new MqttClientTlsOptionsBuilder()
        .WithCertificateValidationCallback((cert, chain, errors) => true)
        .Build())
    .Build();

await client.ConnectAsync(options);
```

### Connection Validation Interceptor

```csharp
using SAEA.MQTT.Server;

var options = new MqttServerOptionsBuilder()
    .WithDefaultEndpointPort(1883)
    .WithConnectionValidator(context => 
    {
        if (context.Username != "admin" || context.Password != "password")
        {
            context.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
            return;
        }
        context.ReasonCode = MqttConnectReasonCode.Success;
    })
    .Build();
```

### Message Interceptor

```csharp
using SAEA.MQTT.Server;

var options = new MqttServerOptionsBuilder()
    .WithDefaultEndpointPort(1883)
    .WithApplicationMessageInterceptor(context => 
    {
        Console.WriteLine($"Intercepted message: {context.ApplicationMessage.Topic}");
        context.AcceptPublish = true;
    })
    .Build();
```

### MQTT 5.0 Features

```csharp
using SAEA.MQTT.Client;

var options = new MqttClientOptionsBuilder()
    .WithProtocolVersion(MqttProtocolVersion.V500)
    .WithClientId("mqtt5_client")
    .WithTcpServer("127.0.0.1", 1883)
    .WithSessionExpiryInterval(3600)
    .WithUserProperty("device", "sensor")
    .WithTopicAliasMaximum(10)
    .Build();

var message = new MqttApplicationMessageBuilder()
    .WithTopic("test/topic")
    .WithPayload("data")
    .WithUserProperty("priority", "high")
    .WithMessageExpiryInterval(60)
    .Build();
```

### Will Message

```csharp
using SAEA.MQTT.Client;

var options = new MqttClientOptionsBuilder()
    .WithClientId("client_with_will")
    .WithTcpServer("127.0.0.1", 1883)
    .WithWillMessage(new MqttApplicationMessageBuilder()
        .WithTopic("client/status")
        .WithPayload("offline")
        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
        .Build())
    .Build();
```

### WebSocket Connection

```csharp
using SAEA.MQTT.Client;

var options = new MqttClientOptionsBuilder()
    .WithClientId("ws_client")
    .WithWebSocketServer("broker.emqx.io:8083/mqtt")
    .Build();

await client.ConnectAsync(options);
```

---

## Supported Protocol Versions

| Version | Protocol Name | Level |
|------|----------|-------|
| MQTT 3.1 | MQIsdp | 3 |
| MQTT 3.1.1 | MQTT | 4 |
| MQTT 5.0 | MQTT | 5 |

---

## Dependencies

| Package | Version | Description |
|------|------|------|
| SAEA.Sockets | 7.26.2.2 | IOCP communication framework |
| SAEA.Common | 7.26.2.2 | Common utility classes |

---

## More Resources

- [GitHub Repository](https://github.com/yswenli/SAEA)
- [NuGet Package](https://www.nuget.org/packages/SAEA.MQTT)
- [MQTT Protocol Specification](http://mqtt.org/)
- [Author's Blog](https://www.cnblogs.com/yswenli/)

---

## License

Apache License 2.0