# SAEA.Audio.Net - 实时语音通信组件

## 简介

SAEA.Audio.Net 是一个基于 .NET 的实时语音通信库，支持多人语音通话。采用 UDP 协议传输，集成 NAudio 音频引擎，支持 Speex、G.722、G.711 等多种音频编解码格式，适用于语音聊天、会议系统、客服对话等场景。

## 特性

- **实时语音采集与播放** - NAudio 音频引擎
- **多格式音频编解码** - Speex/G.722/G.711/GSM 等
- **UDP 实时传输** - 低延迟语音传输
- **频道管理** - 用户邀请、加入频道、多人语音
- **质量可配置** - 低/中/高三种语音质量
- **IOCP 高性能** - 基于 SAEA.Sockets

## 核心类

| 类名 | 说明 |
|------|------|
| `AudioServer<T>` | 语音服务器 |
| `AudioClient` | 语音客户端 |
| `TransferServer<T>` | UDP 传输服务器 |
| `TransferClient` | UDP 传输客户端 |
| `AudioCapture` | 音频捕获器 |
| `AudioPlayer` | 音频播放器 |
| `INetworkChatCodec` | 编解码器接口 |

## 音频编解码器

| 编解码器 | 格式 | 采样率 | 质量 |
|----------|------|--------|------|
| `NarrowBandSpeexCodec` | Speex 窄带 | 8kHz | 低 |
| `WideBandSpeexCodec` | Speex 宽带 | 16kHz | 中 |
| `UltraWideBandSpeexCodec` | Speex 超宽带 | 32kHz | 高 |
| `G722ChatCodec` | G.722 | 16kHz | 中 |
| `ALawChatCodec` | G.711 A-law | 8kHz | 低 |
| `MuLawChatCodec` | G.711 Mu-law | 8kHz | 低 |
| `Gsm610ChatCodec` | GSM 6.10 | 8kHz | 低 |
| `UncompressedPcmChatCodec` | PCM 无压缩 | 8kHz | 最高 |

## 快速使用

### 语音服务器

```csharp
using SAEA.Audio.Net;

// 创建语音服务器
var server = new AudioServer<VStorage>(port: 39656);

server.OnInvited += (sender, args) => 
{
    Console.WriteLine($"用户 {args.FromID} 邀请 {args.ToID} 通话");
};

server.OnJoined += (channel, userId) => 
{
    Console.WriteLine($"用户 {userId} 加入频道 {channel}");
};

server.OnQuit += (channel, userId) => 
{
    Console.WriteLine($"用户 {userId} 离开频道 {channel}");
};

// 启动服务器
server.Start();

Console.WriteLine("语音服务器已启动，端口: 39656");
```

### 语音客户端

```csharp
using SAEA.Audio.Net;
using SAEA.Audio.Net.Core;

// 创建语音客户端（默认使用宽带 Speex）
var client = new AudioClient(ip: "127.0.0.1", port: 39656);

// 选择编解码器
client.SetCodec(new WideBandSpeexCodec());  // 中等质量

// 注册事件
client.OnInvited += (info) => 
{
    Console.WriteLine($"收到通话邀请: 来自 {info.FromID}");
    // 可以选择同意或拒绝
    client.Agree(info.FromID);
};

client.OnAgreed += (info) => 
{
    Console.WriteLine($"通话已建立，加入频道 {info.Channel}");
};

client.OnDisagreed += (info) => 
{
    Console.WriteLine("通话邀请被拒绝");
};

client.OnJoined += (info) => 
{
    Console.WriteLine("已加入语音频道");
};

// 连接服务器
client.Connect();
```

### 发起通话邀请

```csharp
using SAEA.Audio.Net;

var client = new AudioClient("127.0.0.1", 39656);
client.Connect();

// 邀请用户通话
client.Invite("user_002");

// 等待对方同意...
// 同意后双方自动加入同一频道，开始语音通话
```

### 接受/拒绝邀请

```csharp
// 接受邀请
client.OnInvited += (info) => 
{
    client.Agree(info.FromID);  // 同意通话
};

// 拒绝邀请
client.OnInvited += (info) => 
{
    client.Disagree(info.FromID);  // 拒绝通话
};
```

### 加入/退出频道

```csharp
// 加入指定频道（多人语音）
client.Join("meeting_room");

// 退出频道
client.Quit();
```

### 选择语音质量

```csharp
using SAEA.Audio.Net.Core;

// 低质量（节省带宽）
client.SetCodec(new NarrowBandSpeexCodec());

// 中等质量（默认）
client.SetCodec(new WideBandSpeexCodec());

// 高质量（需要更多带宽）
client.SetCodec(new UltraWideBandSpeexCodec());

// 无压缩 PCM（最高质量，带宽最大）
client.SetCodec(new UncompressedPcmChatCodec());

// G.722 宽带编码
client.SetCodec(new G722ChatCodec());

// G.711 A-law（电话质量）
client.SetCodec(new ALawChatCodec());
```

### 自定义存储实现

```csharp
using SAEA.Audio.Net.Model;

// 实现 IStorage 接口自定义频道用户映射
public class MyStorage : IStorage
{
    private Dictionary<string, List<string>> _channels = new();

    public void Add(string channel, string userId)
    {
        if (!_channels.ContainsKey(channel))
            _channels[channel] = new List<string>();
        _channels[channel].Add(userId);
    }

    public void Remove(string channel, string userId)
    {
        if (_channels.ContainsKey(channel))
            _channels[channel].Remove(userId);
    }

    public List<string> GetUsers(string channel)
    {
        return _channels.ContainsKey(channel) ? _channels[channel] : new List<string>();
    }
}

// 使用自定义存储
var server = new AudioServer<MyStorage>(39656);
```

## 传输协议类型

```csharp
public enum ProtocalType
{
    Ping/Pong,     // 心跳检测
    Invite,        // 邀请通话
    Agree,         // 同意通话
    Disagree,      // 拒绝通话
    Join,          // 加入频道
    Data,          // 音频数据传输
    Quit           // 退出频道
}
```

## 传输架构

```
AudioClient
├── AudioCapture (麦克风采集 → 编码)
├── TransferClient (UDP 发送)
└── AudioPlayer (解码 → 播放)

UDP Protocol

AudioServer
├── TransferServer (UDP 接收 → 转发)
└── VStorage (频道-用户映射)
```

## 应用场景

- **语音聊天** - 一对一语音通话
- **会议系统** - 多人语音会议
- **客服对话** - 客服语音系统
- **游戏语音** - 游戏实时语音
- **教学平台** - 在线语音教学

## 依赖项

| 项目 | 说明 |
|------|------|
| SAEA.Sockets | IOCP 通信框架 |
| SAEA.Common | 公共工具类 |
| NAudio | Windows 音频引擎（内置） |
| NSpeex | Speex 编解码（内置） |

## 注意事项

- 本项目基于 .NET Framework 4.6.2
- NAudio 仅支持 Windows 平台
- 需要麦克风和扬声器设备

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [作者博客](https://www.cnblogs.com/yswenli/)

## 许可证

Apache License 2.0