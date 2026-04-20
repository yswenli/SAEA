# SAEA.Common - High-Performance Common Utility Library 🔧

[![NuGet version](https://img.shields.io/nuget/v/SAEA.Common.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.Common)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**English Version** | **[中文版](README.md)**

> A high-performance common utility library based on .NET Standard 2.0, providing serialization, caching, encryption, network requests and other core capabilities. It's the core dependency of the SAEA project family.

## Quick Navigation 🧭

| Section | Content |
|------|------|
| [⚡ 30-Second Quick Start](#30-second-quick-start) | Simplest getting started examples |
| [🎯 Core Features](#core-features) | Main functionalities of the library |
| [📐 Architecture Design](#architecture-design) | Module structure and component relationships |
| [💡 Use Cases](#use-cases) | When to use each utility |
| [📊 Performance Comparison](#performance-comparison) | Serialization performance benchmarks |
| [❓ FAQ](#faq) | Quick answers to common questions |
| [🔧 Core Classes](#core-classes) | Overview of main classes |
| [📝 Usage Examples](#usage-examples) | Detailed code examples |

---

## 30-Second Quick Start ⚡

Get started in just 3 steps to use serialization and caching:

### Step 1: Install NuGet Package

```bash
dotnet add package SAEA.Common
```

### Step 2: JSON Serialization (Just 2 lines of code)

```csharp
using SAEA.Common.Serialization;

// Serialize object
var json = SerializeHelper.Serialize(new { Name = "SAEA", Version = "7.26" });
// Deserialize object
var obj = SerializeHelper.Deserialize<MyClass>(json);

// Or use extension methods
var json2 = myObject.ToJsonString();
var obj2 = json.JsonToObj<MyClass>();
```

### Step 3: High-Performance Caching

```csharp
using SAEA.Common.Caching;

// Create a cache with 10-minute auto-expiration
var cache = new MemoryCache<string>(TimeSpan.FromMinutes(10));
cache.Set("key", "value");
var val = cache.Get("key");
var val2 = cache.GetOrAdd("key2", () => ComputeExpensiveValue());
```

**That's it!** 🎉 You've mastered the core usage of SAEA.Common.

---

## Core Features 🎯

| Feature | Description | Benefits |
|------|------|------|
| 📦 **Multi-format Serialization** | JSON / Protobuf / Binary | Flexible choice for different scenarios |
| ⚡ **High-Performance Caching** | MemoryCache / BlockingQueue / FastQueue | Reduce redundant computation, improve response speed |
| 🔒 **Encryption & Compression** | AES / MD5 / GZip / Deflate | Data security and transmission optimization |
| 🌐 **Network Requests** | HttpClient connection pool / WebClient extensions | Connection reuse, retry mechanism support |
| 📁 **File Operations** | Sync/Async read/write, stream processing | Efficient IO, supports large file handling |
| ⏰ **Time Handling** | Timezone conversion / Unix timestamp / Formatting | Worry-free cross-timezone applications |
| 🔄 **Object Copying** | Expression tree high-performance deep copy | 10x faster than reflection |
| 📝 **Async Logging** | Batch writing / File rolling / Console coloring | Production-ready logging solution |

---

## Architecture Design 📐

### Module Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                     SAEA.Common Architecture                │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐           │
│  │  Serialize  │ │   Caching   │ │  Encryption │           │
│  │  Module     │ │   Module    │ │   Module    │           │
│  └──────┬──────┘ └──────┬──────┘ └──────┬──────┘           │
│         │               │               │                   │
│  ┌──────▼──────┐ ┌──────▼──────┐ ┌──────▼──────┐           │
│  │  JSON/PB/   │ │MemoryCache  │ │ AES/MD5/    │           │
│  │  Binary     │ │Queue/Batcher│ │ GZip        │           │
│  └─────────────┘ └─────────────┘ └─────────────┘           │
│                                                             │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐           │
│  │   Network   │ │     IO      │ │    Utils    │           │
│  │   Module    │ │   Module    │ │   Module    │           │
│  └──────┬──────┘ └──────┬──────┘ └──────┬──────┘           │
│         │               │               │                   │
│  ┌──────▼──────┐ ┌──────▼──────┐ ┌──────▼──────┐           │
│  │HttpClient   │ │FileHelper   │ │LogHelper    │           │
│  │ApiHelper    │ │Async RW     │ │ConfigHelper │           │
│  └─────────────┘ └─────────────┘ │FastCopy     │           │
│                                  └─────────────┘           │
└─────────────────────────────────────────────────────────────┘
```

### Data Flow Diagram

```
Serialization Flow:

Object ──► SerializeHelper ──► Format Selection
                                    │
                    ┌───────────────┼───────────────┐
                    │               │               │
                    ▼               ▼               ▼
              JSON Serialize  Protobuf Serialize  Binary Serialize
              (Readable)       (High Perf/Small)   (Compatible)
                    │               │               │
                    └───────────────┴───────────────┘
                                    │
                                    ▼
                            byte[] / string


Cache Flow:

Request Data ──► Check Cache
                    │
            ┌───────┴───────┐
            │               │
          Hit             Miss
            │               │
            ▼               ▼
       Return Value   Compute New ──► Store ──► Return
                        │
                        ▼
                  Auto Cleanup on Expiry
```

---

## Use Cases 💡

### ✅ Applicable Scenarios by Module

| Module | Scenario | Reason |
|------|------|----------|
| 📦 **Serialization** | API responses, data storage, RPC communication | Multi-format support, one code multiple outputs |
| ⚡ **Caching** | Hot data, config caching, rate limiting | In-memory cache with zero IO latency |
| 📋 **Queue** | Task scheduling, message processing, producer-consumer | Thread-safe, supports blocking/async |
| 🔒 **Encryption** | Password storage, sensitive data protection, data signing | AES symmetric encryption, secure and reliable |
| 📦 **Compression** | Network transmission, log archiving, large file storage | GZip/Deflate dual mode |
| 🌐 **Network** | HTTP API calls, web scraping, third-party integration | Connection pool reuse, auto retry |
| 📁 **File** | Log writing, config storage, temp files | Sync/async dual mode |
| 🔄 **Copy** | DTO conversion, entity mapping, deep copy | Expression tree, extreme performance |

### ❌ Unsuitable Scenarios

| Scenario | Recommended Alternative |
|------|--------------|
| Distributed caching | Use Redis (SAEA.RedisSocket) |
| High-throughput message queue | Use RabbitMQ / Kafka |
| Large-scale log collection | Use ELK / Seq |
| Database access | Use EF Core / Dapper |

---

## Performance Comparison 📊

### Serialization Performance

| Format | Serialization Speed | Deserialization Speed | Data Size | Use Case |
|------|-----------|-------------|----------|----------|
| **JSON** | ⭐⭐⭐ | ⭐⭐⭐ | Large | API communication, config files |
| **Protobuf** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | Smallest | RPC communication, storage |
| **Binary** | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ | Medium | Internal communication, good compatibility |

### Object Copying Performance

| Method | Performance | Description |
|------|------|------|
| **FastCopy (Expression Tree)** | ⭐⭐⭐⭐⭐ | Compiled direct call, best performance |
| **ModelCloneHelper** | ⭐⭐⭐⭐ | Reflection cache optimization, good flexibility |
| **Traditional Reflection** | ⭐⭐ | Per-call reflection, worst performance |

### Cache Component Comparison

| Component | Features | Throughput | Use Case |
|------|------|--------|----------|
| **MemoryCache** | Auto-expiration, thread-safe | High | Short-term cache, config cache |
| **BlockingQueue** | Blocking mode, thread-safe | Medium | Producer-consumer pattern |
| **FastQueue** | Async, Channels-based | Extremely High | High-throughput async processing |

> 💡 **Tip**: FastQueue is based on `System.Threading.Channels`, the recommended high-performance async queue solution for .NET Core.

---

## FAQ ❓

### Q1: How to choose between JSON and Protobuf?

**A**:
- **JSON**: Good readability, suitable for API responses, config files, debugging scenarios
- **Protobuf**: High performance, small size, suitable for RPC communication, storage, internal data exchange

```csharp
// JSON - Good readability
var json = SerializeHelper.Serialize(obj);

// Protobuf - Best performance
var bytes = SerializeHelper.PBSerialize(obj);
```

### Q2: How to set MemoryCache expiration time?

**A**: Specify expiration time when creating, auto cleanup after expiration:

```csharp
// Expires after 10 minutes
var cache = new MemoryCache<string>(TimeSpan.FromMinutes(10));

// Never expires (use max value)
var permanent = new MemoryCache<string>(TimeSpan.MaxValue);
```

### Q3: How to implement high-performance object copying?

**A**: Use `FastCopy` expression tree copy, 10x faster than reflection:

```csharp
// Deep copy (reflection method, simple but slower)
var copy1 = myObject.DeepCopy();

// Expression tree copy (fastest, compiled direct call)
var copy2 = FastCopy<MyClass, MyClass>.Copy(myObject);

// Mapping between different types
var dto = ModelCloneHelper.Copy<UserDto>(userEntity);
```

### Q4: What are the password requirements for AESHelper encryption?

**A**: Password can be any string, internally converted to key automatically:

```csharp
// Encrypt
var encrypted = AESHelper.Encrypt("sensitive data", "your-password");

// Decrypt
var original = AESHelper.Decrypt(encrypted, "your-password");
```

### Q5: How to choose between FastQueue and BlockingQueue?

**A**:
- **FastQueue**: Async mode (`await`), high throughput, recommended for .NET Core
- **BlockingQueue**: Sync blocking mode, suitable for traditional synchronous code

```csharp
// FastQueue - Async (recommended)
var data = await fastQueue.ReadAsync();
await fastQueue.WriteAsync("data");

// BlockingQueue - Sync blocking
var item = queue.Dequeue(TimeSpan.FromSeconds(5));
```

### Q6: How to implement async batch log writing?

**A**: Use `LogHelper` built-in async batch writing:

```csharp
// Auto async file writing
LogHelper.Info("Info message");
LogHelper.Warn("Warning message");
LogHelper.Error("Error message", exception);

// Console colored output
ConsoleHelper.WriteLine("Success", ConsoleColor.Green);
ConsoleHelper.WriteLineError("Failed", ConsoleColor.Red);
```

### Q7: Where is ConfigHelper config file saved?

**A**: Saved in program directory by default, named `{ClassName}.json`:

```csharp
// Create config object
var config = new ConfigHelper<AppConfig>();
config.Instance.ServerIP = "127.0.0.1";
config.Save();  // Save to AppConfig.json

// Auto load on next start
var loaded = config.Read();
```

---

## Core Classes 🔧

### Serialization Classes

| Class | Description |
|------|------|
| `SerializeHelper` | Core serialization utility, supports JSON/Protobuf/Binary |
| `JsonConvert` | JSON converter |
| `JsonSerializer` | JSON serializer |

### Caching & Queue

| Class | Description |
|------|------|
| `MemoryCache<T>` | Custom expiration cache, auto cleanup mechanism |
| `BlockingQueue<T>` | Blocking thread-safe queue |
| `FastQueue<T>` | High-performance async queue based on Channels |
| `HashMap<T1,T2,T3>` | Multi-level key-value storage similar to Redis HashSet |
| `Batcher<T>` | Batch data processing |

### Encryption & Compression

| Class | Description |
|------|------|
| `AESHelper` | AES symmetric encryption/decryption |
| `MD5Helper` | MD5 hash calculation (string, file, stream) |
| `GZipHelper` | GZip/Deflate compression and decompression |

### Network & IO

| Class | Description |
|------|------|
| `HttpClientHelper` | HttpClient connection pool wrapper |
| `ApiHelper` | WebClient extension with retry and logging support |
| `FileHelper` | File read/write operations (sync/async) |
| `IPHelper` | IP address utility, port detection |
| `DNSHelper` | DNS resolution utility |

### Utility Classes

| Class | Description |
|------|------|
| `DateTimeHelper` | Time handling, timezone conversion, Unix timestamp |
| `ByteHelper` | Byte operations, bitwise extensions |
| `HexConverter` | Custom base conversion |
| `LogHelper` | Async logging |
| `ConsoleHelper` | Console colored output |
| `ConfigHelper<T>` | Object persistence config |
| `ModelCloneHelper` | Entity property copy and conversion |
| `FastCopy<TIn,TOut>` | Expression tree high-performance copy |

---

## Usage Examples 📝

### JSON Serialization

```csharp
using SAEA.Common.Serialization;

public class User
{
    public string Name { get; set; }
    public int Age { get; set; }
}

var user = new User { Name = "John", Age = 25 };

// Method 1: Static method
var json = SerializeHelper.Serialize(user);
var obj = SerializeHelper.Deserialize<User>(json);

// Method 2: Extension method
var json2 = user.ToJsonString();
var obj2 = json2.JsonToObj<User>();
```

### Protobuf Serialization (High-Performance Binary)

```csharp
using SAEA.Common.Serialization;

[ProtoContract]  // Need to add ProtoContract attribute
public class ProtoUser
{
    [ProtoMember(1)]
    public string Name { get; set; }
    
    [ProtoMember(2)]
    public int Age { get; set; }
}

var user = new ProtoUser { Name = "John", Age = 25 };

// Serialize to binary
var bytes = SerializeHelper.PBSerialize(user);

// Deserialize
var obj = SerializeHelper.PBDeserialize<ProtoUser>(bytes);
```

### Binary Serialization

```csharp
using SAEA.Common.Serialization;

// Serialize
var bytes = myObject.ToBytes();

// Deserialize
var obj = bytes.ToInstance<MyClass>();
```

### MemoryCache

```csharp
using SAEA.Common.Caching;

// Create 10-minute expiration cache
var cache = new MemoryCache<string>(TimeSpan.FromMinutes(10));

// Set value
cache.Set("key1", "value1");

// Get value
var value = cache.Get("key1");

// Get or add (lazy loading)
var value2 = cache.GetOrAdd("key2", () => 
{
    // Only executed when key2 doesn't exist
    return ExpensiveComputation();
});

// Delete
cache.Remove("key1");

// Clear all
cache.Clear();
```

### BlockingQueue

```csharp
using SAEA.Common.Caching;

var queue = new BlockingQueue<int>();

// Producer thread
Task.Run(() =>
{
    for (int i = 0; i < 100; i++)
    {
        queue.Enqueue(i);
        Thread.Sleep(100);
    }
});

// Consumer thread
Task.Run(() =>
{
    while (true)
    {
        // Blocking wait, max 5 seconds
        var item = queue.Dequeue(TimeSpan.FromSeconds(5));
        if (item != default)
        {
            Console.WriteLine($"Processing: {item}");
        }
    }
});
```

### FastQueue Async Queue

```csharp
using SAEA.Common.Caching;

var queue = new FastQueue<string>();

// Producer
_ = Task.Run(async () =>
{
    for (int i = 0; i < 100; i++)
    {
        await queue.WriteAsync($"Data{i}");
        await Task.Delay(100);
    }
});

// Consumer
_ = Task.Run(async () =>
{
    while (true)
    {
        var data = await queue.ReadAsync();
        Console.WriteLine($"Processing: {data}");
    }
});
```

### Batcher Batch Processing

```csharp
using SAEA.Common.Caching;

var batcher = new Batcher<int>(batchSize: 100);

// Register batch processing event
batcher.OnBatch += (items) =>
{
    Console.WriteLine($"Batch processing {items.Count} items");
    // Batch write to database...
};

// Add data
for (int i = 0; i < 500; i++)
{
    batcher.Add(i);
}

// Manually trigger processing
batcher.Flush();
```

### AES Encryption/Decryption

```csharp
using SAEA.Common.Encryption;

string plainText = "Sensitive data";
string password = "my-secret-password";

// Encrypt
string encrypted = AESHelper.Encrypt(plainText, password);

// Decrypt
string decrypted = AESHelper.Decrypt(encrypted, password);

Console.WriteLine($"Original: {plainText}");
Console.WriteLine($"Encrypted: {encrypted}");
Console.WriteLine($"Decrypted: {decrypted}");
```

### MD5 Hash

```csharp
using SAEA.Common.Encryption;

// String MD5
var hash1 = MD5Helper.GetStringMd5("hello world");

// File MD5
var hash2 = MD5Helper.GetFileMd5(@"C:\file.txt");

// Stream MD5
using var stream = File.OpenRead(@"C:\file.txt");
var hash3 = MD5Helper.GetStreamMd5(stream);
```

### GZip Compression/Decompression

```csharp
using SAEA.Common.Encryption;

string text = "This is a long text that needs compression...";

// Compress
byte[] compressed = GZipHelper.Compress(text);

// Decompress
string original = GZipHelper.Decompress(compressed);

// Deflate compression (alternative algorithm)
byte[] deflated = GZipHelper.DeflateCompress(text);
string original2 = GZipHelper.DeflateDecompress(deflated);
```

### HttpClient Network Requests

```csharp
using SAEA.Common;

// GET request
var html = await HttpClientHelper.GetAsync("https://api.example.com/data");

// POST JSON
var data = new { Name = "John", Age = 25 };
var result = await HttpClientHelper.PostJsonAsync("https://api.example.com/post", data);

// With headers
var headers = new Dictionary<string, string>
{
    { "Authorization", "Bearer token123" },
    { "Content-Type", "application/json" }
};
var response = await HttpClientHelper.PostJsonAsync("https://api.example.com/post", data, headers);
```

### ApiHelper (With Retry Support)

```csharp
using SAEA.Common;

var apiHelper = new ApiHelper();

// GET request
apiHelper.Get("https://api.example.com/data", (response) =>
{
    Console.WriteLine($"Response: {response}");
});

// POST request
apiHelper.Post("https://api.example.com/post", jsonData, (response) =>
{
    Console.WriteLine($"Response: {response}");
});
```

### File Operations

```csharp
using SAEA.Common.IO;

// Sync write
FileHelper.Write("file.txt", "Hello World");

// Sync read
var content = FileHelper.Read("file.txt");

// Async write
await FileHelper.WriteAsync("file.txt", "Hello World");

// Async read
var content2 = await FileHelper.ReadAsync("file.txt");

// Append content
FileHelper.Append("file.txt", "\nAppended content");

// Async append
await FileHelper.AppendAsync("file.txt", "\nMore content");
```

### Logging

```csharp
using SAEA.Common;

// Async logging (auto write to file)
LogHelper.Info("Info log message");
LogHelper.Warn("Warning log message");
LogHelper.Debug("Debug log message");
LogHelper.Error("Error log message");
LogHelper.Error("Exception log", exception);

// Console colored output
ConsoleHelper.WriteLine("Success message", ConsoleColor.Green);
ConsoleHelper.WriteLine("Warning message", ConsoleColor.Yellow);
ConsoleHelper.WriteLineError("Error message", ConsoleColor.Red);
```

### Configuration Persistence

```csharp
using SAEA.Common;

public class AppConfig
{
    public string ServerIP { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 8080;
    public bool EnableSSL { get; set; } = false;
}

// Create config object (auto load or create)
var config = new ConfigHelper<AppConfig>();

// Modify config
config.Instance.ServerIP = "192.168.1.100";
config.Instance.Port = 9000;
config.Instance.EnableSSL = true;

// Save to file (AppConfig.json)
config.Save();

// Reload
config.Read();
```

### Object Copying

```csharp
using SAEA.Common;

// Deep copy (reflection method)
var copy1 = originalObject.DeepCopy();

// Expression tree high-performance copy (recommended)
var copy2 = FastCopy<User, User>.Copy(originalObject);

// Property mapping between different types
public class UserEntity { public string Name { get; set; } }
public class UserDto { public string Name { get; set; } }

var entity = new UserEntity { Name = "John" };
var dto = ModelCloneHelper.Copy<UserDto>(entity);
```

### Time Handling

```csharp
using SAEA.Common;

// Get current timestamp (seconds)
var timestamp = DateTimeHelper.GetTimestamp();

// Timestamp to date
var date = DateTimeHelper.FromTimestamp(timestamp);

// UTC time conversion
var utc = DateTimeHelper.ToUTC(DateTime.Now);
var local = DateTimeHelper.ToLocal(DateTime.UtcNow);

// Format output
var formatted = DateTimeHelper.ToString(DateTime.Now, "yyyy-MM-dd HH:mm:ss");
```

---

## Dependencies

| Package | Version | Description |
|------|------|------|
| protobuf-net | 3.2.56 | Protobuf serialization |
| Nito.AsyncEx | 5.1.2 | Async programming enhancement |
| System.IO.Pipelines | 10.0.2 | High-performance IO pipeline |
| System.Threading.Channels | 10.0.2 | Thread-safe channel |
| System.Memory | 4.6.3 | Memory optimization |

---

## More Resources

- [GitHub Repository](https://github.com/yswenli/SAEA)
- [NuGet Package](https://www.nuget.org/packages/SAEA.Common)
- [Author's Blog](https://www.cnblogs.com/yswenli/)

---

## License

Apache License 2.0