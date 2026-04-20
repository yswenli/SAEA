# SAEA.Common - 高性能通用工具类库 🛠️

[![NuGet version](https://img.shields.io/nuget/v/SAEA.Common.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.Common)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**[English Version](README.en.md)** | **中文版**

> 基于 .NET Standard 2.0 的高性能通用工具类库，提供序列化、缓存、加密、网络请求等基础能力，是 SAEA 项目家族的核心依赖。

## 快速导航 🧭

| 章节 | 内容 |
|------|------|
| [⚡ 30秒快速开始](#30秒快速开始) | 最简单的上手示例 |
| [🎯 核心特性](#核心特性) | 类库的主要功能 |
| [📐 架构设计](#架构设计) | 模块结构与组件关系 |
| [💡 应用场景](#应用场景) | 何时使用各工具类 |
| [📊 性能对比](#性能对比) | 序列化性能对比数据 |
| [❓ 常见问题](#常见问题) | FAQ 快速解答 |
| [🔧 核心类](#核心类) | 主要类一览 |
| [📝 使用示例](#使用示例) | 详细代码示例 |

---

## 30秒快速开始 ⚡

最快上手方式，只需3步即可使用序列化和缓存功能：

### Step 1: 安装 NuGet 包

```bash
dotnet add package SAEA.Common
```

### Step 2: JSON 序列化（仅需2行代码）

```csharp
using SAEA.Common.Serialization;

// 序列化对象
var json = SerializeHelper.Serialize(new { Name = "SAEA", Version = "7.26" });
// 反序列化对象
var obj = SerializeHelper.Deserialize<MyClass>(json);

// 或者使用扩展方法
var json2 = myObject.ToJsonString();
var obj2 = json.JsonToObj<MyClass>();
```

### Step 3: 高性能缓存

```csharp
using SAEA.Common.Caching;

// 创建10分钟自动过期的缓存
var cache = new MemoryCache<string>(TimeSpan.FromMinutes(10));
cache.Set("key", "value");
var val = cache.Get("key");
var val2 = cache.GetOrAdd("key2", () => ComputeExpensiveValue());
```

**就这么简单！** 🎉 你已经掌握了 SAEA.Common 的核心用法。

---

## 核心特性 🎯

| 特性 | 说明 | 优势 |
|------|------|------|
| 📦 **多格式序列化** | JSON / Protobuf / Binary 三种方式 | 灵活选择，满足不同场景需求 |
| ⚡ **高性能缓存** | MemoryCache / BlockingQueue / FastQueue | 减少重复计算，提升响应速度 |
| 🔒 **加密与压缩** | AES / MD5 / GZip / Deflate | 数据安全与传输优化 |
| 🌐 **网络请求** | HttpClient 连接池 / WebClient 扩展 | 连接复用，支持重试机制 |
| 📁 **文件操作** | 同步/异步读写，流式处理 | 高效IO，支持大文件处理 |
| ⏰ **时间处理** | 时区转换 / Unix时间戳 / 格式化 | 跨时区应用无忧 |
| 🔄 **对象复制** | 表达式树高性能深拷贝 | 比反射快10倍以上 |
| 📝 **异步日志** | 批量写入 / 文件滚动 / 控制台彩色 | 生产环境日志解决方案 |

---

## 架构设计 📐

### 模块架构图

```
┌─────────────────────────────────────────────────────────────┐
│                     SAEA.Common 架构                         │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐           │
│  │  Serialize  │ │   Caching   │ │  Encryption │           │
│  │  序列化模块  │ │   缓存模块   │ │   加密模块   │           │
│  └──────┬──────┘ └──────┬──────┘ └──────┬──────┘           │
│         │               │               │                   │
│  ┌──────▼──────┐ ┌──────▼──────┐ ┌──────▼──────┐           │
│  │  JSON/PB/   │ │MemoryCache  │ │ AES/MD5/    │           │
│  │  Binary     │ │Queue/Batcher│ │ GZip        │           │
│  └─────────────┘ └─────────────┘ └─────────────┘           │
│                                                             │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐           │
│  │   Network   │ │     IO      │ │    Utils    │           │
│  │   网络模块   │ │   文件模块   │ │   工具模块   │           │
│  └──────┬──────┘ └──────┬──────┘ └──────┬──────┘           │
│         │               │               │                   │
│  ┌──────▼──────┐ ┌──────▼──────┐ ┌──────▼──────┐           │
│  │HttpClient   │ │FileHelper   │ │LogHelper    │           │
│  │ApiHelper    │ │Async RW     │ │ConfigHelper │           │
│  └─────────────┘ └─────────────┘ │FastCopy     │           │
│                                  └─────────────┘           │
└─────────────────────────────────────────────────────────────┘
```

### 数据流转图

```
序列化流程:

对象 ──► SerializeHelper ──► 格式选择
                                    │
                    ┌───────────────┼───────────────┐
                    │               │               │
                    ▼               ▼               ▼
              JSON序列化     Protobuf序列化    Binary序列化
              (可读性强)      (高性能小体积)    (兼容性好)
                    │               │               │
                    └───────────────┴───────────────┘
                                    │
                                    ▼
                            byte[] / string


缓存流程:

请求数据 ──► 检查缓存
                │
        ┌───────┴───────┐
        │               │
      命中            未命中
        │               │
        ▼               ▼
   返回缓存值    计算新值 ──► 存入缓存 ──► 返回
                        │
                        ▼
                  过期自动清理
```

---

## 应用场景 💡

### ✅ 各模块适用场景

| 模块 | 场景 | 推荐理由 |
|------|------|----------|
| 📦 **序列化** | API 响应、数据存储、RPC 通信 | 多格式支持，一套代码多种输出 |
| ⚡ **缓存** | 热点数据、配置缓存、限流计数 | 内存缓存零 IO 延迟 |
| 📋 **队列** | 任务调度、消息处理、生产消费模型 | 线程安全，支持阻塞/异步 |
| 🔒 **加密** | 密码存储、敏感数据保护、数据签名 | AES 对称加密，安全可靠 |
| 📦 **压缩** | 网络传输、日志归档、大文件存储 | GZip/Deflate 双模式 |
| 🌐 **网络** | HTTP API 调用、Web 抓取、第三方集成 | 连接池复用，自动重试 |
| 📁 **文件** | 日志写入、配置存储、临时文件 | 同步异步双模式 |
| 🔄 **复制** | DTO 转换、实体映射、深拷贝 | 表达式树，性能极致 |

### ❌ 不适合的场景

| 场景 | 推荐替代方案 |
|------|--------------|
| 分布式缓存 | 使用 Redis (SAEA.RedisSocket) |
| 高吞吐消息队列 | 使用 RabbitMQ / Kafka |
| 大规模日志收集 | 使用 ELK / Seq |
| 数据库访问 | 使用 EF Core / Dapper |

---

## 性能对比 📊

### 序列化性能对比

| 格式 | 序列化速度 | 反序列化速度 | 数据大小 | 适用场景 |
|------|-----------|-------------|----------|----------|
| **JSON** | ⭐⭐⭐ | ⭐⭐⭐ | 较大 | API 通信、配置文件 |
| **Protobuf** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | 最小 | RPC 通信、存储 |
| **Binary** | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ | 中等 | 内部通信、兼容性好 |

### 对象复制性能对比

| 方式 | 性能 | 说明 |
|------|------|------|
| **FastCopy (表达式树)** | ⭐⭐⭐⭐⭐ | 编译后直接调用，性能最优 |
| **ModelCloneHelper** | ⭐⭐⭐⭐ | 反射缓存优化，灵活性好 |
| **传统反射** | ⭐⭐ | 每次反射，性能最差 |

### 缓存组件对比

| 组件 | 特点 | 吞吐量 | 适用场景 |
|------|------|--------|----------|
| **MemoryCache** | 自动过期、线程安全 | 高 | 短期缓存、配置缓存 |
| **BlockingQueue** | 阻塞式、线程安全 | 中 | 生产消费模型 |
| **FastQueue** | 异步、基于 Channels | 极高 | 高吞吐异步处理 |

> 💡 **提示**: FastQueue 基于 `System.Threading.Channels` 实现，是 .NET Core 推荐的高性能异步队列方案。

---

## 常见问题 ❓

### Q1: JSON 和 Protobuf 如何选择？

**A**: 
- **JSON**: 可读性强，适合 API 响应、配置文件、调试场景
- **Protobuf**: 性能高、体积小，适合 RPC 通信、存储、内部数据交换

```csharp
// JSON - 可读性好
var json = SerializeHelper.Serialize(obj);

// Protobuf - 性能最优
var bytes = SerializeHelper.PBSerialize(obj);
```

### Q2: MemoryCache 如何设置过期时间？

**A**: 创建时指定过期时间，过期后自动清理：

```csharp
// 10分钟后过期
var cache = new MemoryCache<string>(TimeSpan.FromMinutes(10));

// 永不过期（使用最大值）
var permanent = new MemoryCache<string>(TimeSpan.MaxValue);
```

### Q3: 如何实现高性能对象复制？

**A**: 使用 `FastCopy` 表达式树复制，比反射快 10 倍以上：

```csharp
// 深拷贝（反射方式，简单但较慢）
var copy1 = myObject.DeepCopy();

// 表达式树复制（最快，编译后直接调用）
var copy2 = FastCopy<MyClass, MyClass>.Copy(myObject);

// 不同类型间映射
var dto = ModelCloneHelper.Copy<UserDto>(userEntity);
```

### Q4: AESHelper 加密的密码有什么要求？

**A**: 密码可以是任意字符串，内部会自动转换为密钥：

```csharp
// 加密
var encrypted = AESHelper.Encrypt("敏感数据", "your-password");

// 解密
var original = AESHelper.Decrypt(encrypted, "your-password");
```

### Q5: FastQueue 和 BlockingQueue 如何选择？

**A**: 
- **FastQueue**: 异步模式（`await`），高吞吐，推荐用于 .NET Core
- **BlockingQueue**: 同步阻塞模式，适合传统同步代码

```csharp
// FastQueue - 异步（推荐）
var data = await fastQueue.ReadAsync();
await fastQueue.WriteAsync("data");

// BlockingQueue - 同步阻塞
var item = queue.Dequeue(TimeSpan.FromSeconds(5));
```

### Q6: 如何实现异步批量日志写入？

**A**: 使用 `LogHelper` 内置异步批量写入：

```csharp
// 自动异步写入文件
LogHelper.Info("普通信息");
LogHelper.Warn("警告信息");
LogHelper.Error("错误信息", exception);

// 控制台彩色输出
ConsoleHelper.WriteLine("成功", ConsoleColor.Green);
ConsoleHelper.WriteLineError("失败", ConsoleColor.Red);
```

### Q7: ConfigHelper 配置文件保存在哪里？

**A**: 默认保存在程序运行目录，以 `{类名}.json` 命名：

```csharp
// 创建配置对象
var config = new ConfigHelper<AppConfig>();
config.Instance.ServerIP = "127.0.0.1";
config.Save();  // 保存到 AppConfig.json

// 下次启动自动加载
var loaded = config.Read();
```

---

## 核心类 🔧

### 序列化类

| 类名 | 说明 |
|------|------|
| `SerializeHelper` | 核心序列化工具，支持 JSON/Protobuf/Binary |
| `JsonConvert` | JSON 转换器 |
| `JsonSerializer` | JSON 序列化器 |

### 缓存与队列

| 类名 | 说明 |
|------|------|
| `MemoryCache<T>` | 自定义过期缓存，自动清理机制 |
| `BlockingQueue<T>` | 阻塞式线程安全队列 |
| `FastQueue<T>` | 基于 Channels 的高性能异步队列 |
| `HashMap<T1,T2,T3>` | 类似 Redis HashSet 的多层键值存储 |
| `Batcher<T>` | 批量数据打包处理 |

### 加密与压缩

| 类名 | 说明 |
|------|------|
| `AESHelper` | AES 对称加密/解密 |
| `MD5Helper` | MD5 哈希计算（字符串、文件、流） |
| `GZipHelper` | GZip/Deflate 压缩解压 |

### 网络与 IO

| 类名 | 说明 |
|------|------|
| `HttpClientHelper` | HttpClient 连接池封装 |
| `ApiHelper` | WebClient 扩展，支持重试、日志跟踪 |
| `FileHelper` | 文件读写操作（同步/异步） |
| `IPHelper` | IP 地址工具，端口检测 |
| `DNSHelper` | DNS 解析工具 |

### 工具类

| 类名 | 说明 |
|------|------|
| `DateTimeHelper` | 时间处理，时区转换，Unix 时间戳 |
| `ByteHelper` | 字节操作，位操作扩展 |
| `HexConverter` | 自定义进制转换 |
| `LogHelper` | 异步日志记录 |
| `ConsoleHelper` | 控制台彩色输出 |
| `ConfigHelper<T>` | 对象持久化配置 |
| `ModelCloneHelper` | 实体属性复制转换 |
| `FastCopy<TIn,TOut>` | 表达式树高性能复制 |

---

## 使用示例 📝

### JSON 序列化

```csharp
using SAEA.Common.Serialization;

public class User
{
    public string Name { get; set; }
    public int Age { get; set; }
}

var user = new User { Name = "张三", Age = 25 };

// 方式1: 静态方法
var json = SerializeHelper.Serialize(user);
var obj = SerializeHelper.Deserialize<User>(json);

// 方式2: 扩展方法
var json2 = user.ToJsonString();
var obj2 = json2.JsonToObj<User>();
```

### Protobuf 序列化（高性能二进制）

```csharp
using SAEA.Common.Serialization;

[ProtoContract]  // 需要添加 ProtoContract 特性
public class ProtoUser
{
    [ProtoMember(1)]
    public string Name { get; set; }
    
    [ProtoMember(2)]
    public int Age { get; set; }
}

var user = new ProtoUser { Name = "张三", Age = 25 };

// 序列化为二进制
var bytes = SerializeHelper.PBSerialize(user);

// 反序列化
var obj = SerializeHelper.PBDeserialize<ProtoUser>(bytes);
```

### Binary 二进制序列化

```csharp
using SAEA.Common.Serialization;

// 序列化
var bytes = myObject.ToBytes();

// 反序列化
var obj = bytes.ToInstance<MyClass>();
```

### MemoryCache 缓存

```csharp
using SAEA.Common.Caching;

// 创建10分钟过期缓存
var cache = new MemoryCache<string>(TimeSpan.FromMinutes(10));

// 设置值
cache.Set("key1", "value1");

// 获取值
var value = cache.Get("key1");

// 获取或添加（懒加载）
var value2 = cache.GetOrAdd("key2", () => 
{
    // 只有在 key2 不存在时才执行
    return ExpensiveComputation();
});

// 删除
cache.Remove("key1");

// 清空
cache.Clear();
```

### BlockingQueue 阻塞队列

```csharp
using SAEA.Common.Caching;

var queue = new BlockingQueue<int>();

// 生产者线程
Task.Run(() =>
{
    for (int i = 0; i < 100; i++)
    {
        queue.Enqueue(i);
        Thread.Sleep(100);
    }
});

// 消费者线程
Task.Run(() =>
{
    while (true)
    {
        // 阻塞等待，最多等5秒
        var item = queue.Dequeue(TimeSpan.FromSeconds(5));
        if (item != default)
        {
            Console.WriteLine($"处理: {item}");
        }
    }
});
```

### FastQueue 异步队列

```csharp
using SAEA.Common.Caching;

var queue = new FastQueue<string>();

// 生产者
_ = Task.Run(async () =>
{
    for (int i = 0; i < 100; i++)
    {
        await queue.WriteAsync($"数据{i}");
        await Task.Delay(100);
    }
});

// 消费者
_ = Task.Run(async () =>
{
    while (true)
    {
        var data = await queue.ReadAsync();
        Console.WriteLine($"处理: {data}");
    }
});
```

### Batcher 批量处理

```csharp
using SAEA.Common.Caching;

var batcher = new Batcher<int>(batchSize: 100);

// 注册批量处理事件
batcher.OnBatch += (items) =>
{
    Console.WriteLine($"批量处理 {items.Count} 条数据");
    // 批量写入数据库...
};

// 添加数据
for (int i = 0; i < 500; i++)
{
    batcher.Add(i);
}

// 手动触发处理
batcher.Flush();
```

### AES 加密解密

```csharp
using SAEA.Common.Encryption;

string plainText = "敏感数据";
string password = "my-secret-password";

// 加密
string encrypted = AESHelper.Encrypt(plainText, password);

// 解密
string decrypted = AESHelper.Decrypt(encrypted, password);

Console.WriteLine($"原文: {plainText}");
Console.WriteLine($"加密: {encrypted}");
Console.WriteLine($"解密: {decrypted}");
```

### MD5 哈希

```csharp
using SAEA.Common.Encryption;

// 字符串 MD5
var hash1 = MD5Helper.GetStringMd5("hello world");

// 文件 MD5
var hash2 = MD5Helper.GetFileMd5(@"C:\file.txt");

// 流 MD5
using var stream = File.OpenRead(@"C:\file.txt");
var hash3 = MD5Helper.GetStreamMd5(stream);
```

### GZip 压缩解压

```csharp
using SAEA.Common.Encryption;

string text = "这是一段需要压缩的长文本...";

// 压缩
byte[] compressed = GZipHelper.Compress(text);

// 解压
string original = GZipHelper.Decompress(compressed);

// Deflate 压缩（另一种算法）
byte[] deflated = GZipHelper.DeflateCompress(text);
string original2 = GZipHelper.DeflateDecompress(deflated);
```

### HttpClient 网络请求

```csharp
using SAEA.Common;

// GET 请求
var html = await HttpClientHelper.GetAsync("https://api.example.com/data");

// POST JSON
var data = new { Name = "张三", Age = 25 };
var result = await HttpClientHelper.PostJsonAsync("https://api.example.com/post", data);

// 带请求头
var headers = new Dictionary<string, string>
{
    { "Authorization", "Bearer token123" },
    { "Content-Type", "application/json" }
};
var response = await HttpClientHelper.PostJsonAsync("https://api.example.com/post", data, headers);
```

### ApiHelper（支持重试）

```csharp
using SAEA.Common;

var apiHelper = new ApiHelper();

// GET 请求
apiHelper.Get("https://api.example.com/data", (response) =>
{
    Console.WriteLine($"响应: {response}");
});

// POST 请求
apiHelper.Post("https://api.example.com/post", jsonData, (response) =>
{
    Console.WriteLine($"响应: {response}");
});
```

### 文件操作

```csharp
using SAEA.Common.IO;

// 同步写入
FileHelper.Write("file.txt", "Hello World");

// 同步读取
var content = FileHelper.Read("file.txt");

// 异步写入
await FileHelper.WriteAsync("file.txt", "Hello World");

// 异步读取
var content2 = await FileHelper.ReadAsync("file.txt");

// 追加内容
FileHelper.Append("file.txt", "\n追加的内容");

// 异步追加
await FileHelper.AppendAsync("file.txt", "\n更多内容");
```

### 日志记录

```csharp
using SAEA.Common;

// 异步日志（自动写入文件）
LogHelper.Info("普通信息日志");
LogHelper.Warn("警告信息日志");
LogHelper.Debug("调试信息日志");
LogHelper.Error("错误信息日志");
LogHelper.Error("异常日志", exception);

// 控制台彩色输出
ConsoleHelper.WriteLine("成功信息", ConsoleColor.Green);
ConsoleHelper.WriteLine("警告信息", ConsoleColor.Yellow);
ConsoleHelper.WriteLineError("错误信息", ConsoleColor.Red);
```

### 配置持久化

```csharp
using SAEA.Common;

public class AppConfig
{
    public string ServerIP { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 8080;
    public bool EnableSSL { get; set; } = false;
}

// 创建配置对象（自动加载或创建）
var config = new ConfigHelper<AppConfig>();

// 修改配置
config.Instance.ServerIP = "192.168.1.100";
config.Instance.Port = 9000;
config.Instance.EnableSSL = true;

// 保存到文件（AppConfig.json）
config.Save();

// 重新加载
config.Read();
```

### 对象复制

```csharp
using SAEA.Common;

// 深拷贝（反射方式）
var copy1 = originalObject.DeepCopy();

// 表达式树高性能复制（推荐）
var copy2 = FastCopy<User, User>.Copy(originalObject);

// 不同类型间属性映射
public class UserEntity { public string Name { get; set; } }
public class UserDto { public string Name { get; set; } }

var entity = new UserEntity { Name = "张三" };
var dto = ModelCloneHelper.Copy<UserDto>(entity);
```

### 时间处理

```csharp
using SAEA.Common;

// 获取当前时间戳（秒）
var timestamp = DateTimeHelper.GetTimestamp();

// 时间戳转日期
var date = DateTimeHelper.FromTimestamp(timestamp);

// UTC 时间转换
var utc = DateTimeHelper.ToUTC(DateTime.Now);
var local = DateTimeHelper.ToLocal(DateTime.UtcNow);

// 格式化输出
var formatted = DateTimeHelper.ToString(DateTime.Now, "yyyy-MM-dd HH:mm:ss");
```

---

## 依赖包

| 包名 | 版本 | 说明 |
|------|------|------|
| protobuf-net | 3.2.56 | Protobuf 序列化 |
| Nito.AsyncEx | 5.1.2 | 异步编程增强 |
| System.IO.Pipelines | 10.0.2 | 高性能 IO 管道 |
| System.Threading.Channels | 10.0.2 | 线程安全通道 |
| System.Memory | 4.6.3 | 内存优化 |

---

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包](https://www.nuget.org/packages/SAEA.Common)
- [作者博客](https://www.cnblogs.com/yswenli/)

---

## 许可证

Apache License 2.0