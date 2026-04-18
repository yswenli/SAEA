# SAEA.MessageSocket - 高性能消息服务器

[![NuGet version](https://img.shields.io/nuget/v/SAEA.MessageSocket.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.MessageSocket)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

## 简介

SAEA.MessageSocket 是一个基于 SAEA.Sockets 的高性能消息服务器/客户端组件，支持私信、频道、群组三种消息模式。消息使用 Protobuf 序列化压缩传输，适用于即时通讯、在线客服、协作工具等场景。

## 特性

- **IOCP 高性能** - 基于 SAEA.Sockets 完成端口技术
- **Protobuf 序列化** - 高效二进制消息压缩
- **三种消息模式** - 私信、频道、群组
- **频道订阅** - 发布/订阅模式
- **群组管理** - 创建、加入、离开、删除
- **心跳保活** - 自动心跳机制
- **批量发送** - ClassificationBatcher 优化

## NuGet 安装

```bash
# Package Manager
Install-Package SAEA.MessageSocket -Version 7.26.2.2

# .NET CLI
dotnet add package SAEA.MessageSocket --version 7.26.2.2
```

## 核心类

| 类名 | 说明 |
|------|------|
| `MessageServer` | 消息服务器 |
| `MessageClient` | 消息客户端 |
| `ChatMessage` | 聊天消息协议类 |
| `ChatMessageType` | 消息类型枚举 |
| `PrivateMessage` | 私信消息模型 |
| `ChannelMessage` | 频道消息模型 |
| `GroupMessage` | 群组消息模型 |
| `ChannelList` | 频道列表管理 |
| `GroupList` | 群组列表管理 |

## 快速使用

### 启动消息服务器

```csharp
using SAEA.MessageSocket;

// 创建消息服务器
var server = new MessageServer(port: 39654);

server.OnDisconnected += (id) => Console.WriteLine($"用户断开: {id}");

// 启动服务器
server.Start();

Console.WriteLine("消息服务器已启动，端口: 39654");
```

### 消息客户端

```csharp
using SAEA.MessageSocket;
using System.Text;

// 创建消息客户端
var client = new MessageClient(ip: "127.0.0.1", port: 39654);

// 注册事件
client.OnPrivateMessage += (msg) => 
{
    Console.WriteLine($"收到私信 [来自: {msg.Sender}]: {msg.Content}");
};

client.OnChannelMessage += (msg) => 
{
    Console.WriteLine($"频道消息 [{msg.Name}]: {msg.Content}");
};

client.OnGroupMessage += (msg) => 
{
    Console.WriteLine($"群组消息 [{msg.Name}]: {msg.Content}");
};

client.OnDisconnected += () => Console.WriteLine("连接断开");

// 连接服务器
client.Connect();
```

### 私信消息

```csharp
// 发送私信给指定用户
client.SendPrivateMsg("user_002", "你好，这是一条私信！");

// OnPrivateMessage 事件会收到对方回复
```

### 频道消息（发布/订阅）

```csharp
// 订阅频道
client.Subscribe("news_channel");
client.Subscribe("sports_channel");

// 发送频道消息
client.SendChannelMsg("news_channel", "今日新闻头条...");

// 取消订阅
client.Unsubscribe("news_channel");
```

### 群组消息

```csharp
// 创建群组（创建者拥有管理权限）
client.SendCreateGroup("project_team");

// 添加成员到群组
client.SendAddMember("project_team");  // 添加自己
// 其他客户端调用此方法加入群组

// 发送群组消息
client.SendGroupMessage("project_team", "项目进度更新...");

// 离开群组
client.SendRemoveMember("project_team");

// 删除群组（仅创建者可操作）
client.SendRemoveGroup("project_team");
```

### 完整示例

```csharp
using SAEA.MessageSocket;

// 服务器端
var server = new MessageServer(1024, 1000 * 1000, 30 * 60 * 1000);
server.OnDisconnected += Server_OnDisconnected;
server.Start();

// 客户端1
var client1 = new MessageClient();
client1.OnPrivateMessage += (msg) => Console.WriteLine($"Client1 收到私信: {msg.Content}");
client1.OnChannelMessage += (msg) => Console.WriteLine($"Client1 频道消息: {msg.Content}");
client1.Connect();

// 客户端2
var client2 = new MessageClient();
client2.OnPrivateMessage += (msg) => Console.WriteLine($"Client2 收到私信: {msg.Content}");
client2.Connect();

// 测试私信
client1.SendPrivateMsg(client2.UserToken.ID, "你好 Client2！");

// 测试频道
client1.Subscribe("chat_channel");
client2.Subscribe("chat_channel");
client1.SendChannelMsg("chat_channel", "大家好！");

// 测试群组
var groupName = "test_group";
client1.SendCreateGroup(groupName);
client2.SendAddMember(groupName);
client1.SendGroupMessage(groupName, "群组广播消息！");
```

## 消息类型

```csharp
public enum ChatMessageType
{
    Login = 1,              // 登录
    LoginAnswer = 2,        // 登录响应
    Subscribe = 3,          // 订阅频道
    SubscribeAnswer = 4,    // 订阅响应
    UnSubscribe = 5,        // 取消订阅
    UnSubscribeAnswer = 6,  // 取消订阅响应
    ChannelMessage = 7,     // 频道消息
    PrivateMessage = 8,     // 私信消息
    PrivateMessageAnswer = 9, // 私信响应
    CreateGroup = 10,       // 创建群组
    CreateGroupAnswer = 11, // 创建群组响应
    AddMember = 12,         // 添加成员
    AddMemberAnswer = 13,   // 添加成员响应
    RemoveMember = 14,      // 移除成员
    RemoveMemberAnswer = 15, // 移除成员响应
    RemoveGroup = 16,       // 删除群组
    RemoveGroupAnswer = 17, // 删除群组响应
    GroupMessage = 18,      // 群组消息
    GroupMessageAnswer = 19 // 群组消息响应
}
```

## 消息模式对比

| 模式 | 说明 | 适用场景 |
|------|------|----------|
| **私信** | 点对点消息传输 | 一对一私聊 |
| **频道** | 发布/订阅，动态订阅 | 新闻推送、广播通知 |
| **群组** | 群组管理，创建者可删除 | 团队协作、讨论组 |

## 应用场景

- **即时通讯** - 私聊、群聊
- **在线客服** - 客服对话系统
- **协作工具** - 团队实时协作
- **通知推送** - 系统消息推送
- **直播互动** - 直播间聊天

## 依赖项

| 包名 | 版本 | 说明 |
|------|------|------|
| SAEA.Sockets | 7.26.2.2 | IOCP 通信框架 |
| SAEA.Common | 7.26.2.2 | 公共工具类（含 Protobuf） |

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包](https://www.nuget.org/packages/SAEA.MessageSocket)
- [作者博客](https://www.cnblogs.com/yswenli/)

## 许可证

Apache License 2.0