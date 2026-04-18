# SAEA.Common - 高性能通用工具类库

[![NuGet version](https://img.shields.io/nuget/v/SAEA.Common.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.Common)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

## 简介

SAEA.Common 是 SAEA 项目家族的基础工具类库，提供了一系列高性能、易用的工具类、扩展方法和数据结构。它是所有 SAEA 子项目的公共依赖，为上层应用提供序列化、缓存、加密、网络请求、文件操作等基础能力。

## 特性

- **多格式序列化** - 支持 JSON、Protobuf、二进制三种序列化方式
- **高性能缓存** - MemoryCache、BlockingQueue、FastQueue 等缓存组件
- **加密与压缩** - AES、MD5、GZip 加密压缩工具
- **网络请求** - HttpClient 连接池封装、WebClient 扩展
- **文件操作** - 同步/异步文件读写
- **时间处理** - 时区转换、Unix 时间戳
- **对象复制** - 表达式树高性能深拷贝
- **日志记录** - 异步批量日志写入

## NuGet 安装

```bash
# Package Manager
Install-Package SAEA.Common -Version 7.26.2.2

# .NET CLI
dotnet add package SAEA.Common --version 7.26.2.2
```

## 核心类一览

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

## 快速使用

### 序列化

```csharp
using SAEA.Common.Serialization;

// JSON 序列化
var json = SerializeHelper.Serialize(myObject);
var obj = SerializeHelper.Deserialize<MyClass>(json);

// 扩展方法
var json2 = myObject.ToJsonString();
var obj2 = json.JsonToObj<MyClass>();

// Protobuf 序列化（高性能二进制）
var bytes = SerializeHelper.PBSerialize(myObject);
var obj3 = SerializeHelper.PBDeserialize<MyClass>(bytes);

// 二进制序列化
var bytes2 = myObject.ToBytes();
var obj4 = bytes2.ToInstance<MyClass>();
```

### 缓存

```csharp
using SAEA.Common.Caching;

// MemoryCache - 自动过期缓存
var cache = new MemoryCache<string>(TimeSpan.FromMinutes(10));
cache.Set("key", "value");
var val = cache.Get("key");
var val2 = cache.GetOrAdd("key2", () => "new value");

// BlockingQueue - 阻塞队列
var queue = new BlockingQueue<int>();
queue.Enqueue(1);
var item = queue.Dequeue(TimeSpan.FromSeconds(5));

// FastQueue - 高性能异步队列
var fastQueue = new FastQueue<string>();
await fastQueue.WriteAsync("data");
var data = await fastQueue.ReadAsync();

// Batcher - 批量处理
var batcher = new Batcher<int>(batchSize: 100);
batcher.OnBatch += (items) => Console.WriteLine($"处理 {items.Count} 条数据");
batcher.Add(1);
batcher.Add(2);
```

### 加密与压缩

```csharp
using SAEA.Common.Encryption;

// AES 加密
var encrypted = AESHelper.Encrypt("hello", "password");
var decrypted = AESHelper.Decrypt(encrypted, "password");

// MD5 哈希
var hash = MD5Helper.GetStringMd5("hello");
var fileHash = MD5Helper.GetFileMd5(@"C:\file.txt");

// GZip 压缩
var compressed = GZipHelper.Compress("hello world");
var original = GZipHelper.Decompress(compressed);
```

### 网络请求

```csharp
using SAEA.Common;

// HttpClient 快捷请求
var html = await HttpClientHelper.GetAsync("https://api.example.com/data");
var result = await HttpClientHelper.PostJsonAsync("https://api.example.com/post", myData);

// ApiHelper - 支持重试和日志
var apiHelper = new ApiHelper();
apiHelper.Get("https://api.example.com/data", (response) => 
{
    Console.WriteLine(response);
});
```

### 文件操作

```csharp
using SAEA.Common.IO;

// 同步读写
FileHelper.Write("file.txt", "content");
var content = FileHelper.Read("file.txt");

// 异步读写
await FileHelper.WriteAsync("file.txt", "content");
var content2 = await FileHelper.ReadAsync("file.txt");

// 追加写入
FileHelper.Append("file.txt", "more content");
```

### 日志

```csharp
using SAEA.Common;

// 异步日志
LogHelper.Error("发生错误", ex);
LogHelper.Warn("警告信息");
LogHelper.Info("普通信息");
LogHelper.Debug("调试信息");

// 控制台彩色输出
ConsoleHelper.WriteLine("绿色文字", ConsoleColor.Green);
ConsoleHelper.WriteLineError("红色错误", ConsoleColor.Red);
```

### 配置持久化

```csharp
using SAEA.Common;

// 配置对象自动持久化到 JSON 文件
public class AppConfig
{
    public string ServerIP { get; set; }
    public int Port { get; set; }
}

var config = new ConfigHelper<AppConfig>();
config.Instance.ServerIP = "127.0.0.1";
config.Instance.Port = 8080;
config.Save();  // 保存到 appconfig.json

// 读取配置
var loaded = config.Read();
```

### 对象复制

```csharp
using SAEA.Common;

// 高性能深拷贝
var copy = myObject.DeepCopy();

// 不同类型间属性映射
var dto = ModelCloneHelper.Copy<UserDto>(userEntity);

// 表达式树高性能复制
var copy2 = FastCopy<MyClass, MyClass>.Copy(myObject);
```

### 时间处理

```csharp
using SAEA.Common;

// Unix 时间戳
var timestamp = DateTimeHelper.GetTimestamp();
var date = DateTimeHelper.FromTimestamp(timestamp);

// 时区转换
var utc = DateTimeHelper.ToUTC(localTime);
var local = DateTimeHelper.ToLocal(utcTime);

// 格式化输出
var formatted = DateTimeHelper.ToString(date, "yyyy-MM-dd HH:mm:ss");
```

## 依赖包

| 包名 | 版本 | 说明 |
|------|------|------|
| protobuf-net | 3.2.56 | Protobuf 序列化 |
| Nito.AsyncEx | 5.1.2 | 异步编程增强 |
| System.IO.Pipelines | 10.0.2 | 高性能 IO 管道 |
| System.Threading.Channels | 10.0.2 | 线程安全通道 |
| System.Memory | 4.6.3 | 内存优化 |

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包](https://www.nuget.org/packages/SAEA.Common)
- [作者博客](https://www.cnblogs.com/yswenli/)

## 许可证

Apache License 2.0