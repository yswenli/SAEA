# SAEA.MQTT - MQTT 协议服务器/客户端

[![NuGet version](https://img.shields.io/nuget/v/SAEA.MQTT.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.MQTT)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

## 简介

SAEA.MQTT 是一个完整的 MQTT 协议实现库，基于 SAEA.Sockets 的 IOCP 技术。提供 MQTT Broker 服务器和 MQTT 客户端功能，支持 MQTT 3.1、3.1.1、5.0 协议版本，适用于 IoT 设备通信、消息推送、实时数据传输等场景。

## 特性

- **MQTT Broker** - 完整的 MQTT 服务器实现
- **MQTT Client** - TCP/WebSocket 客户端
- **MQTT 5.0** - 支持最新 MQTT 5.0 协议特性
- **三种 QoS** - QoS 0/1/2 消息质量保证
- **TLS/SSL** - 安全加密连接
- **托管客户端** - 消息队列、自动重连、离线消息
- **拦截器** - 连接验证、消息拦截、订阅拦截
- **持久会话** - 支持会话持久化

## NuGet 安装

```bash
# Package Manager
Install-Package SAEA.MQTT -Version 7.26.2.2

# .NET CLI
dotnet add package SAEA.MQTT --version 7.26.2.2
```

## 核心类

| 类名 | 说明 |
|------|------|
| `MqttServer` | MQTT Broker 服务器 |
| `MqttClient` | MQTT 客户端 |
| `ManagedMqttClient` | 托管 MQTT 客户端（支持消息队列） |
| `MqttFactory` | MQTT 工厂类 |
| `MqttServerOptionsBuilder` | 服务器配置构建器 |
| `MqttClientOptionsBuilder` | 客户端配置构建器 |
| `MqttApplicationMessageBuilder` | 应用消息构建器 |
| `MqttTopicFilterBuilder` | 主题过滤器构建器 |

## 支持的协议版本

| 版本 | 协议名称 | Level |
|------|----------|-------|
| MQTT 3.1 | MQIsdp | 3 |
| MQTT 3.1.1 | MQTT | 4 |
| MQTT 5.0 | MQTT | 5 |

## QoS 服务质量

| QoS | 说明 |
|-----|------|
| `AtMostOnce (0)` | 至多一次，消息可能丢失 |
| `AtLeastOnce (1)` | 至少一次，消息可能重复 |
| `ExactlyOnce (2)` | 恰好一次，消息不丢失不重复 |

## 快速使用

### MQTT Broker 服务器

```csharp
using SAEA.MQTT;
using SAEA.MQTT.Server;

// 创建 MQTT 服务器
var factory = new MqttFactory();
var server = factory.CreateMqttServer();

// 配置服务器
var options = new MqttServerOptionsBuilder()
    .WithDefaultEndpoint()
    .WithDefaultEndpointPort(1883)
    .WithConnectionBacklog(1000)
    .Build();

// 注册事件
server.ApplicationMessageReceived += (sender, e) => 
{
    var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
    Console.WriteLine($"收到消息 [ClientId: {e.ClientId}, Topic: {e.ApplicationMessage.Topic}]: {payload}");
};

server.ClientConnected += (sender, e) => 
{
    Console.WriteLine($"客户端连接: {e.ClientId}");
};

server.ClientDisconnected += (sender, e) => 
{
    Console.WriteLine($"客户端断开: {e.ClientId}");
};

// 启动服务器
await server.StartAsync(options);

Console.WriteLine("MQTT Broker 已启动，端口: 1883");
```

### MQTT 客户端

```csharp
using SAEA.MQTT;
using SAEA.MQTT.Client;

// 创建 MQTT 客户端
var factory = new MqttFactory();
var client = factory.CreateMqttClient();

// 配置客户端
var options = new MqttClientOptionsBuilder()
    .WithClientId("client_001")
    .WithTcpServer("127.0.0.1", 1883)
    .WithCleanSession()
    .WithKeepAlivePeriod(TimeSpan.FromSeconds(60))
    .Build();

// 注册事件
client.ApplicationMessageReceived += (sender, e) => 
{
    var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
    Console.WriteLine($"收到消息 [Topic: {e.ApplicationMessage.Topic}]: {payload}");
};

client.Connected += (sender, e) => Console.WriteLine("已连接");
client.Disconnected += (sender, e) => Console.WriteLine("已断开");

// 连接服务器
await client.ConnectAsync(options);

// 订阅主题
await client.SubscribeAsync("home/temperature");
await client.SubscribeAsync("home/#");  // 通配符订阅

// 发布消息
var message = new MqttApplicationMessageBuilder()
    .WithTopic("home/temperature")
    .WithPayload("25.5")
    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
    .Build();

await client.PublishAsync(message);

// 断开连接
await client.DisconnectAsync();
```

### 托管 MQTT 客户端

```csharp
using SAEA.MQTT;
using SAEA.MQTT.Client;

// 托管客户端（支持消息队列和自动重连）
var factory = new MqttFactory();
var managedClient = factory.CreateManagedMqttClient();

var options = new ManagedMqttClientOptionsBuilder()
    .WithClientOptions(new MqttClientOptionsBuilder()
        .WithClientId("managed_client")
        .WithTcpServer("127.0.0.1", 1883)
        .Build())
    .Build();

// 启动托管客户端
await managedClient.StartAsync(options);

// 消息会自动排队，断线时缓存，重连后自动发送
await managedClient.PublishAsync(new MqttApplicationMessageBuilder()
    .WithTopic("test/topic")
    .WithPayload("Hello MQTT!")
    .Build());

// 持久订阅（自动重连后恢复订阅）
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
        // 验证用户名密码
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
        // 消息拦截处理
        Console.WriteLine($"拦截消息: {context.ApplicationMessage.Topic}");
        context.AcceptPublish = true;  // 接受发布
    })
    .Build();
```

### MQTT 5.0 特性

```csharp
using SAEA.MQTT.Client;

// MQTT 5.0 配置
var options = new MqttClientOptionsBuilder()
    .WithProtocolVersion(MqttProtocolVersion.V500)
    .WithClientId("mqtt5_client")
    .WithTcpServer("127.0.0.1", 1883)
    .WithSessionExpiryInterval(3600)  // 会话过期间隔
    .WithUserProperty("device", "sensor")  // 用户属性
    .WithTopicAliasMaximum(10)  // 主题别名最大值
    .Build();

// MQTT 5.0 消息
var message = new MqttApplicationMessageBuilder()
    .WithTopic("test/topic")
    .WithPayload("data")
    .WithUserProperty("priority", "high")
    .WithMessageExpiryInterval(60)  // 消息过期
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

## 应用场景

- **IoT 设备通信** - 传感器数据上报、设备控制
- **消息推送** - 客户端消息推送
- **实时数据** - 股票行情、体育比分
- **智能家居** - 设备状态同步
- **车联网** - 车辆数据上报

## 依赖项

| 包名 | 版本 | 说明 |
|------|------|------|
| SAEA.Sockets | 7.26.2.2 | IOCP 通信框架 |
| SAEA.Common | 7.26.2.2 | 公共工具类 |

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包](https://www.nuget.org/packages/SAEA.MQTT)
- [作者博客](https://www.cnblogs.com/yswenli/)

## 许可证

Apache License 2.0