# SAEA.MQTT - 高性能 MQTT 协议框架 📡

[![NuGet version](https://img.shields.io/nuget/v/SAEA.MQTT.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.MQTT)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**[English Version](README.en.md)** | **中文版**

> 基于 .NET Standard 2.0 的高性能 MQTT 协议实现，采用 SAEA.Sockets IOCP 技术，支持 MQTT 3.1/3.1.1/5.0 协议，适用于 IoT 设备通信、消息推送等场景。

## 快速导航 🧭

| 章节 | 内容 |
|------|------|
| [⚡ 30秒快速开始](#30秒快速开始) | Broker + Client 最快上手 |
| [🎯 核心特性](#核心特性) | 框架的主要功能 |
| [📐 架构设计](#架构设计) | 组件关系与消息流程 |
| [💡 应用场景](#应用场景) | 何时选择 SAEA.MQTT |
| [📊 性能对比](#性能对比) | QoS 级别对比 |
| [❓ 常见问题](#常见问题) | FAQ 快速解答 |
| [🔧 核心类](#核心类) | 主要类一览 |
| [📝 使用示例](#使用示例) | 详细代码示例 |

---

## 30秒快速开始 ⚡

最快上手方式，只需3步即可运行 MQTT Broker 和 Client：

### Step 1: 安装 NuGet 包

```bash
dotnet add package SAEA.MQTT
```

### Step 2: 创建 MQTT Broker 服务器（仅需10行代码）

```csharp
using SAEA.MQTT;
using SAEA.MQTT.Server;

var factory = new MqttFactory();
var server = factory.CreateMqttServer();

var options = new MqttServerOptionsBuilder()
    .WithDefaultEndpointPort(1883)
    .Build();

server.ClientConnected += (s, e) => Console.WriteLine($"客户端连接: {e.ClientId}");
await server.StartAsync(options);
Console.WriteLine("MQTT Broker 已启动，端口: 1883");
```

### Step 3: 创建 MQTT 客户端连接

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

**就这么简单！** 🎉 你已经实现了一个完整的 MQTT 发布/订阅系统。

---

## 核心特性 🎯

| 特性 | 说明 | 优势 |
|------|------|------|
| 🚀 **高性能 Broker** | IOCP 异步模型 | 支持万级设备并发连接 |
| 📡 **完整协议支持** | MQTT 3.1/3.1.1/5.0 | 兼容所有主流 MQTT 客户端 |
| 🔒 **TLS/SSL 加密** | 安全连接支持 | 数据传输加密保护 |
| 📊 **三种 QoS 级别** | QoS 0/1/2 | 灵活的消息质量保证 |
| 🔄 **托管客户端** | 自动重连、消息队列 | 断线自动恢复，消息不丢失 |
| 🎯 **消息拦截器** | 连接/消息/订阅拦截 | 灵活的权限控制与审计 |
| 💾 **持久会话** | 会话状态持久化 | 离线消息、订阅状态保持 |
| 🔗 **WebSocket 支持** | WS/WSS 协议 | 浏览器直接连接 |
| 📨 **遗嘱消息** | 离线状态通知 | 设备异常断开自动通知 |
| 🏷️ **主题别名** | MQTT 5.0 特性 | 减少网络开销 |

---

## 架构设计 📐

### 组件架构图

```
┌─────────────────────────────────────────────────────────────┐
│                     SAEA.MQTT 架构                           │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────────┐         ┌──────────────────┐        │
│  │   MqttServer     │         │  MqttClient       │        │
│  │   (Broker)       │         │  (客户端)         │        │
│  └────────┬─────────┘         └────────┬─────────┘        │
│           │                            │                     │
│           │  ┌────────────────────────┐│                    │
│           │  │   MqttFactory          ││                    │
│           └──►   (工厂类)            ◄┘                    │
│              └────────────────────────┘                    │
│                        │                                    │
│     ┌──────────────────┼──────────────────┐               │
│     │                  │                  │                │
│  ┌──▼────────┐   ┌─────▼──────┐   ┌──────▼─────┐         │
│  │ MqttServer │   │ MqttClient │   │ Managed    │         │
│  │ Options    │   │ Options     │   │ MqttClient │         │
│  │ Builder    │   │ Builder     │   │ (托管客户端)│        │
│  └────────────┘   └─────────────┘   └────────────┘         │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐ │
│  │                    核心组件                            │ │
│  ├──────────────────────────────────────────────────────┤ │
│  │ SessionManager │ RetainedMessages │ Interceptors   │ │
│  └──────────────────────────────────────────────────────┘ │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐ │
│  │                 SAEA.Sockets (IOCP)                   │ │
│  └──────────────────────────────────────────────────────┘ │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### MQTT 消息流程图

```
发布/订阅流程:

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

QoS 级别处理流程:

QoS 0 - At Most Once (至多一次):
Publisher ──► Publish ──► Broker ──► Forward ──► Subscriber
            (无确认)

QoS 1 - At Least Once (至少一次):
Publisher ──► Publish ──► Broker ──► Forward ──► Subscriber
     │          │           │            │
     │◄─ PubAck ─┘           │       ┌────┘
     │                       │◄──PubAck──┘

QoS 2 - Exactly Once (恰好一次):
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

### 会话管理流程

```
客户端会话生命周期:

客户端连接请求
       │
       ▼
┌─────────────────┐
│ ConnectionValidator │ ◄── 连接验证拦截器
│ (用户名/密码验证)   │
└────────┬────────┘
         │
    ┌────▼────┐
    │ 验证通过? │
    └────┬────┘
         │
    ┌────┴────┐
    │         │
   Yes        No
    │         │
    ▼         ▼
┌─────────┐  ┌──────────────┐
│ 会话检查 │  │ 返回错误码    │
│ 持久/临时│  │ BadUserName  │
└────┬────┘  │ OrPassword   │
     │       └──────────────┘
     ▼
┌─────────────────┐
│ SessionManager   │
│ 加载/创建会话    │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ 恢复订阅        │
│ (持久会话)      │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ 发送离线消息     │
│ (QoS 1/2)       │
└────────┬────────┘
         │
         ▼
    客户端就绪
```

---

## 应用场景 💡

### ✅ 适合使用 SAEA.MQTT 的场景

| 场景 | 描述 | 推荐理由 |
|------|------|----------|
| 🏠 **智能家居** | 设备状态同步、远程控制 | 轻量级协议，低功耗设备友好 |
| 🏭 **工业物联网** | 传感器数据采集、设备监控 | QoS 保证数据可靠性 |
| 🚗 **车联网** | 车辆位置上报、远程诊断 | 支持移动网络弱网环境 |
| 📱 **消息推送** | App 推送通知、即时消息 | WebSocket 支持，浏览器直连 |
| 📊 **实时数据** | 股票行情、体育比分 | 发布订阅模式，高效广播 |
| 🌡️ **环境监测** | 温湿度、空气质量上报 | 低带宽，适合 NB-IoT |
| 🏥 **智慧医疗** | 医疗设备数据传输 | TLS 加密，安全可靠 |
| 🌾 **智慧农业** | 大棚监控、灌溉控制 | 支持大量设备并发 |

### 典型应用架构示例

```
┌─────────────────────────────────────────────────────────────┐
│                      智能家居系统                            │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│   ┌─────────┐   ┌─────────┐   ┌─────────┐   ┌─────────┐   │
│   │温度传感器│   │ 智能灯泡 │   │智能插座 │   │ 门锁    │   │
│   └────┬────┘   └────┬────┘   └────┬────┘   └────┬────┘   │
│        │             │             │             │          │
│        └──────────────┼──────────────┼──────────────┘       │
│                       │              │                       │
│                       ▼              ▼                       │
│              ┌─────────────────────────────┐               │
│              │      SAEA.MQTT Broker       │               │
│              │      (端口 1883/8883)       │               │
│              └─────────────┬───────────────┘               │
│                            │                                │
│         ┌──────────────────┼──────────────────┐            │
│         │                  │                  │            │
│         ▼                  ▼                  ▼            │
│   ┌──────────┐      ┌──────────┐      ┌──────────┐        │
│   │ 手机 App │      │ Web 控制台│     │ 数据分析 │        │
│   │ (WebSocket)│    │ (WebSocket)│    │ (订阅数据)│       │
│   └──────────┘      └──────────┘      └──────────┘        │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### ❌ 不适合的场景

| 场景 | 推荐替代方案 |
|------|--------------|
| 简单 HTTP API | 使用 SAEA.Http 或 ASP.NET |
| 大文件传输 | 使用 SAEA.Sockets 直接传输 |
| 视频流传输 | 使用 RTSP/WebRTC |
| 数据库访问 | 使用 SAEA.RedisSocket |

---

## 性能对比 📊

### QoS 服务质量级别对比

| QoS 级别 | 级别名称 | 消息传递次数 | 可靠性 | 延迟 | 带宽消耗 | 适用场景 |
|----------|----------|--------------|--------|------|----------|----------|
| **QoS 0** | At Most Once | 至多一次 | ❌ 低 | ⭐ 最低 | ⭐ 最低 | 环境数据、日志上报 |
| **QoS 1** | At Least Once | 至少一次 | ⚠️ 中 | ⭐⭐ 中等 | ⭐⭐ 中等 | 一般业务消息 |
| **QoS 2** | Exactly Once | 恰好一次 | ✅ 高 | ⭐⭐⭐ 较高 | ⭐⭐⭐ 较高 | 金融交易、重要指令 |

### QoS 详细对比

```
QoS 0: At Most Once (至多一次)
┌─────────┐      Publish       ┌─────────┐      Forward      ┌─────────┐
│Publisher│ ─────────────────► │  Broker  │ ─────────────────► │Subscriber│
└─────────┘    (无确认)         └─────────┘    (无确认)         └─────────┘

特点: 最快，可能丢失
适用: 传感器数据（偶尔丢失可接受）

QoS 1: At Least Once (至少一次)
┌─────────┐      Publish       ┌─────────┐      Forward      ┌─────────┐
│Publisher│ ─────────────────► │  Broker  │ ─────────────────► │Subscriber│
└─────────┘                    └─────────┘                    └─────────┘
     ▲                              ▲                              │
     │         PubAck               │         PubAck               │
     └──────────────────────────────┴──────────────────────────────┘

特点: 确保送达，可能重复
适用: 一般业务消息（需去重处理）

QoS 2: Exactly Once (恰好一次)
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

特点: 确保唯一，最可靠
适用: 重要消息、金融交易
```

### MQTT 协议版本对比

| 特性 | MQTT 3.1 | MQTT 3.1.1 | MQTT 5.0 |
|------|----------|------------|----------|
| 基本发布订阅 | ✅ | ✅ | ✅ |
| QoS 0/1/2 | ✅ | ✅ | ✅ |
| 遗嘱消息 | ✅ | ✅ | ✅ |
| 清洁会话 | ✅ | ✅ | ✅ |
| **原因码** | ❌ | ❌ | ✅ |
| **用户属性** | ❌ | ❌ | ✅ |
| **主题别名** | ❌ | ❌ | ✅ |
| **消息过期** | ❌ | ❌ | ✅ |
| **会话过期** | ❌ | ❌ | ✅ |
| **共享订阅** | ❌ | ❌ | ✅ |

---

## 常见问题 ❓

### Q1: MQTT Broker 能支持多少设备连接？

**A**: SAEA.MQTT 基于 SAEA.Sockets 的 IOCP 技术，单机可支持 10,000+ 并发连接。实际性能取决于：
- 服务器硬件配置（CPU、内存）
- 网络带宽
- 消息频率和大小
- QoS 级别

建议：生产环境使用多实例集群部署。

### Q2: QoS 0/1/2 如何选择？

**A**: 根据业务需求选择：

| 场景 | 推荐 QoS | 理由 |
|------|----------|------|
| 传感器温度数据 | QoS 0 | 偶尔丢失可接受，追求实时性 |
| 设备状态同步 | QoS 1 | 确保送达，应用层去重 |
| 支付指令 | QoS 2 | 确保唯一，不允许重复 |
| 日志上报 | QoS 0 | 允许少量丢失 |
| 告警通知 | QoS 1 | 确保送达 |

### Q3: 如何实现设备认证？

**A**: 使用连接验证拦截器：

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

### Q4: 客户端断线后如何自动重连？

**A**: 使用 `ManagedMqttClient` 托管客户端：

```csharp
var factory = new MqttFactory();
var managedClient = factory.CreateManagedMqttClient();

var options = new ManagedMqttClientOptionsBuilder()
    .WithClientOptions(new MqttClientOptionsBuilder()
        .WithClientId("device_001")
        .WithTcpServer("127.0.0.1", 1883)
        .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))  // 5秒重连
        .Build())
    .Build();

await managedClient.StartAsync(options);
// 自动重连，断线期间消息缓存，重连后自动发送
```

### Q5: 如何实现消息加密传输？

**A**: 使用 TLS/SSL 安全连接：

```csharp
// 服务端配置证书
var serverOptions = new MqttServerOptionsBuilder()
    .WithDefaultEndpointPort(8883)
    .WithEncryptedEndpoint()
    .WithEncryptionCertificate(File.ReadAllBytes("server.pfx"))
    .Build();

// 客户端连接
var clientOptions = new MqttClientOptionsBuilder()
    .WithClientId("secure_client")
    .WithTcpServer("127.0.0.1", 8883)
    .WithTls(new MqttClientTlsOptionsBuilder()
        .WithCertificateValidationCallback((cert, chain, errors) => true)
        .Build())
    .Build();
```

### Q6: 主题通配符如何使用？

**A**: MQTT 支持两种通配符：

| 通配符 | 说明 | 示例 |
|--------|------|------|
| `+` | 单级通配符 | `home/+/temperature` 匹配 `home/living/temperature` |
| `#` | 多级通配符 | `home/#` 匹配 `home/living/temperature`、`home/bedroom/humidity` |

```csharp
// 订阅所有房间的温度
await client.SubscribeAsync("home/+/temperature");

// 订阅家庭所有消息
await client.SubscribeAsync("home/#");
```

### Q7: 遗嘱消息是什么？如何使用？

**A**: 遗嘱消息（Will Message）是客户端异常断开时 Broker 自动发布的消息：

```csharp
var options = new MqttClientOptionsBuilder()
    .WithClientId("device_001")
    .WithTcpServer("127.0.0.1", 1883)
    .WithWillMessage(new MqttApplicationMessageBuilder()
        .WithTopic("device/status")          // 遗嘱主题
        .WithPayload("offline")              // 遗嘱内容
        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
        .WithRetainFlag(true)                // 保留消息
        .Build())
    .Build();

// 当设备异常断开时，Broker 自动发布 "device/status" -> "offline"
```

---

## 核心类 🔧

### 主要类

| 类名 | 说明 |
|------|------|
| `MqttServer` | MQTT Broker 服务器 |
| `MqttClient` | 标准 MQTT 客户端 |
| `ManagedMqttClient` | 托管客户端（自动重连、消息队列） |
| `MqttFactory` | MQTT 对象工厂类 |
| `MqttApplicationMessage` | 应用消息封装 |
| `MqttTopicFilter` | 主题过滤器 |

### 配置构建器

| 类名 | 说明 |
|------|------|
| `MqttServerOptionsBuilder` | 服务器配置构建器 |
| `MqttClientOptionsBuilder` | 客户端配置构建器 |
| `ManagedMqttClientOptionsBuilder` | 托管客户端配置构建器 |
| `MqttApplicationMessageBuilder` | 应用消息构建器 |
| `MqttTopicFilterBuilder` | 主题过滤器构建器 |

### 拦截器接口

| 接口 | 说明 |
|------|------|
| `IMqttServerConnectionValidator` | 连接验证拦截器 |
| `IMqttServerApplicationMessageInterceptor` | 消息拦截器 |
| `IMqttServerSubscriptionInterceptor` | 订阅拦截器 |
| `IMqttServerUnsubscriptionInterceptor` | 取消订阅拦截器 |

### 协议枚举

| 枚举 | 说明 |
|------|------|
| `MqttProtocolVersion` | 协议版本（V311、V500） |
| `MqttQualityOfServiceLevel` | QoS 级别（AtMostOnce、AtLeastOnce、ExactlyOnce） |
| `MqttConnectReasonCode` | 连接原因码 |
| `MqttRetainHandling` | 保留消息处理方式 |

---

## 使用示例 📝

### MQTT Broker 服务器

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
    Console.WriteLine($"收到消息 [ClientId: {e.ClientId}, Topic: {e.ApplicationMessage.Topic}]: {payload}");
};

server.ClientConnected += (sender, e) => 
    Console.WriteLine($"客户端连接: {e.ClientId}");

server.ClientDisconnected += (sender, e) => 
    Console.WriteLine($"客户端断开: {e.ClientId}");

await server.StartAsync(options);
Console.WriteLine("MQTT Broker 已启动，端口: 1883");
```

### MQTT 客户端

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
    Console.WriteLine($"收到消息 [Topic: {e.ApplicationMessage.Topic}]: {payload}");
};

client.Connected += (sender, e) => Console.WriteLine("已连接");
client.Disconnected += (sender, e) => Console.WriteLine("已断开");

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

### 托管 MQTT 客户端

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

### TLS/SSL 安全连接

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

### 连接验证拦截器

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

### 消息拦截器

```csharp
using SAEA.MQTT.Server;

var options = new MqttServerOptionsBuilder()
    .WithDefaultEndpointPort(1883)
    .WithApplicationMessageInterceptor(context => 
    {
        Console.WriteLine($"拦截消息: {context.ApplicationMessage.Topic}");
        context.AcceptPublish = true;
    })
    .Build();
```

### MQTT 5.0 特性

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

### 遗嘱消息

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

### WebSocket 连接

```csharp
using SAEA.MQTT.Client;

var options = new MqttClientOptionsBuilder()
    .WithClientId("ws_client")
    .WithWebSocketServer("broker.emqx.io:8083/mqtt")
    .Build();

await client.ConnectAsync(options);
```

---

## 支持的协议版本

| 版本 | 协议名称 | Level |
|------|----------|-------|
| MQTT 3.1 | MQIsdp | 3 |
| MQTT 3.1.1 | MQTT | 4 |
| MQTT 5.0 | MQTT | 5 |

---

## 依赖项

| 包名 | 版本 | 说明 |
|------|------|------|
| SAEA.Sockets | 7.26.2.2 | IOCP 通信框架 |
| SAEA.Common | 7.26.2.2 | 公共工具类 |

---

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包](https://www.nuget.org/packages/SAEA.MQTT)
- [MQTT 协议规范](http://mqtt.org/)
- [作者博客](https://www.cnblogs.com/yswenli/)

---

## 许可证

Apache License 2.0