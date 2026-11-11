# SAEA.FTP - 高性能 FTP 服务器/客户端组件 📁

[![NuGet version](https://img.shields.io/nuget/v/SAEA.FTP.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.FTP)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**[English Version](README.en.md)** | **中文版**

> 基于 TCP Socket 的 FTP 服务器/客户端组件，支持被动模式、断点续传、自动重试、同名冲突处理与单文件多连接并行传输。
>
> ⚡ **可靠传输内核**：`REST` 断点续传 + 指数退避重试 + 自适应缓冲区；大文件自动拆分为多分片，通过多条控制会话并行传输，服务器不支持时自动降级为单连接。

## 快速导航 🧭

| 章节 | 内容 |
|------|------|
| [⚡ 30秒快速开始](#30秒快速开始) | 最简单的上手示例 |
| [🎯 核心特性](#核心特性) | 组件的主要功能 |
| [📐 架构设计](#架构设计) | 组件关系与工作流程 |
| [💡 应用场景](#应用场景) | 何时选择 SAEA.FTP |
| [📊 性能对比](#性能对比) | 与其他方案对比 |
| [❓ 常见问题](#常见问题) | FAQ 快速解答 |
| [🔧 核心类](#核心类) | 主要类一览 |
| [📝 使用示例](#使用示例) | 详细代码示例 |

---

## 30秒快速开始 ⚡

最快上手方式，只需3步即可运行 FTP 服务器：

### Step 1: 安装 NuGet 包

```bash
dotnet add package SAEA.FTP
```

### Step 2: 创建 FTP 服务器

```csharp
using SAEA.FTP;

var config = new ServerConfig
{
    IP = "127.0.0.1",
    Port = 21,
    BufferSize = 1024 * 64
};

config.Users["admin"] = new FTPUser("admin", "password", @"C:\FTPRoot");

var ftpServer = new FTPServer(config);

ftpServer.OnLog += (msg, ex) => Console.WriteLine(msg);
ftpServer.Start();
```

> 也可以直接用 `anonymous` 用户名登录：未显式配置时，匿名用户以程序运行目录为根目录。

### Step 3: 创建 FTP 客户端连接

```csharp
var client = new FTPClient("127.0.0.1", 21, "admin", "password");
client.Connect();

// 上传文件
client.Upload(@"C:\Data\file.txt", (offset, total) => 
    Console.WriteLine($"上传进度: {(double)offset / total * 100:F0}%"));
```

**就这么简单！** 🎉 你已经实现了一个支持高速传输的 FTP 文件服务器。

---

## 核心特性 🎯

| 特性 | 说明 | 优势 |
|------|------|------|
| 🚀 **高性能传输** | 控制通道走 SAEA.Sockets 处理器链，每个控制连接独立会话 | 支持多文件并发传输 |
| 📡 **被动模式 EPSV/PASV** | 标准扩展被动模式，自动回退 PASV | 兼容防火墙与 NAT，稳定可靠 |
| 📊 **进度回调** | 实时上传/下载进度 | 支持进度条与速率统计 |
| 🔁 **自动重试与续传** | 网络抖动自动重连，优先 `REST` 断点续传 | 大文件传输更可靠 |
| ⚡ **单文件并行传输** | 多控制会话分段并行上传/下载 | 显著提升大文件速度 |
| 🛡️ **同名冲突处理** | 覆盖 / 跳过 / 改名，每文件回调一次 | 默认跳过，绝不静默覆盖 |
| 🧠 **自适应缓冲** | 16KB–1MB 动态调整 | 兼顾吞吐与内存 |
| 🔐 **用户认证** | 支持多用户、匿名登录、并发会话上限 | 灵活的权限管理 |
| 📁 **目录管理** | 完整的目录操作支持 | 创建、删除、遍历目录 |
| 🔄 **文件操作** | 上传、下载、重命名、删除 | 完整的 FTP 命令支持 |
| 💾 **配置持久化** | 用户配置自动保存 | 无需数据库，配置即用 |
| 🌐 **UTF-8 支持** | 支持中文文件名 | 跨平台兼容性好 |

---

## 架构设计 📐

### 组件架构图

```
┌─────────────────────────────────────────────────────────────┐
│                     SAEA.FTP 架构                            │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌────────────────────┐      ┌────────────────────┐        │
│  │    FTPServer       │      │    FTPClient       │        │
│  │   (FTP 服务器)     │      │   (FTP 客户端)     │        │
│  └─────────┬──────────┘      └─────────┬──────────┘        │
│            │                           │                     │
│            │   ┌───────────────────────┘                    │
│            │   │                                            │
│  ┌─────────▼───▼─────────┐                                 │
│  │ FtpControlConnection  │  控制连接 / FEAT / REST 会话    │
│  │ ServerSession         │  每控制连接独立会话             │
│  └───────────┬───────────┘                                 │
│              │                                              │
│  ┌───────────▼───────────┐                                 │
│  │ FTPTransferOptions    │  重试 / 并行 / 冲突策略         │
│  │ FTPTransferResult     │  结果与统计                     │
│  └───────────┬───────────┘                                 │
│              │                                              │
│  ┌───────────▼───────────┐                                 │
│  │ SAEA.Sockets.Pipeline │  控制连接（处理器链）               │
│  │ TcpClient             │  数据连接                     │
│  └───────────────────────┘                                 │
│                                                             │
│  ┌───────────────────────┐                                 │
│  │   ConfigManager       │  配置管理持久化                 │
│  │   FTPUser             │  用户凭证与根目录               │
│  └───────────────────────┘                                 │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### FTP 传输流程图

```
客户端连接流程:

客户端 ──► TCP 21端口 ──► FTPServer.Accepted
                                │
                                ▼
                      ┌─────────────────┐
                      │   用户认证       │
                      │ USER/PASS       │
                      └─────────────────┘
                                │
                                ▼
                      ┌─────────────────┐
                      │   进入命令模式   │
                      │ 等待 FTP 命令   │
                      └─────────────────┘

EPSV/PASV 被动模式文件传输:

客户端 ──► EPSV 命令（失败回退 PASV）──► 服务器分配数据端口
                              │
                              ▼
                    ┌─────────────────┐
                    │ 229/227 响应    │
                    │ 返回 Port/IP:Port│
                    └─────────────────┘
                              │
                              ▼
              客户端连接数据端口 (非21端口)
                              │
                              ▼
                    ┌─────────────────┐
                    │ STOR/RETR      │
                    │ 上传/下载文件  │
                    └─────────────────┘
                              │
                              ▼
                    ┌─────────────────┐
                    │ 进度回调        │
                    │ (offset, total) │
                    └─────────────────┘
                              │
                              ▼
                    传输完成，关闭数据连接
```

---

## 应用场景 💡

### ✅ 适合使用 SAEA.FTP 的场景

| 场景 | 描述 | 推荐理由 |
|------|------|----------|
| 📁 **企业文件服务器** | 内部文件共享与分发 | 每控制连接独立会话，支持多用户并发 |
| 🔄 **自动化文件传输** | 定时同步、备份任务 | 简洁 API，易于集成到自动化脚本 |
| 💾 **数据备份系统** | 远程文件备份 | 进度回调，支持大文件传输 |
| 🖥️ **FTP 客户端工具** | 开发 FTP 管理软件 | 完整客户端实现，支持所有常用命令 |
| 📱 **移动端后台** | 文件上传下载服务 | 被动模式兼容防火墙 |
| 🏢 **企业数据交换** | 跨部门文件传输 | 用户认证，权限管理 |

### ❌ 不适合的场景

| 场景 | 推荐替代方案 |
|------|--------------|
| HTTP 文件上传 | 使用 SAEA.Http 或 ASP.NET |
| 大规模 CDN 分发 | 使用专业 CDN 服务 |
| 实时视频流 | 使用 SAEA.Sockets 自定义协议 |
| 需要 PORT 主动模式 | 目前仅支持 EPSV/PASV 被动模式 |

---

## 性能对比 📊

### 与固定单连接方案对比

| 能力 | SAEA.FTP | 固定单连接方案 |
|------|----------|----------------|
| **大文件传输** | 超过 `ParallelThresholdBytes`（默认 8MB）自动多会话分片并行 | 单连接顺序传输 |
| **中断恢复** | 同一次调用内 `REST` 续传 + 指数退避重试 | 通常整文件重传 |
| **缓冲区** | 16KB–1MB 自适应调整 | 固定大小 |
| **同名冲突** | 覆盖 / 跳过 / 改名回调，默认跳过 | 常见为静默覆盖 |
| **并发扩展** | `MaxSessions` 可配；服务器不支持时自动降级 | 无 |

> 💡 实际吞吐取决于网络质量、磁盘、服务器并发限制与被动端口配置；上表描述传输策略差异，不承诺固定倍率。

### EPSV/PASV 被动模式优势

| 特性 | EPSV/PASV 被动模式 | PORT 主动模式 |
|------|-------------------|---------------|
| **防火墙兼容** | ✅ 高 | ❌ 低 |
| **客户端连接** | 客户端发起数据连接 | 服务器主动连接客户端 |
| **安全性** | ✅ 较好 | ⚠️ 较差 |
| **适用场景** | 现代网络环境 | 老旧网络 |

> 💡 **提示**: 服务端同时支持 `EPSV` 与 `PASV` 被动模式，客户端优先 `EPSV` 并自动回退 `PASV`；不支持 `PORT` 主动模式。

---

## 常见问题 ❓

### Q1: 为什么只支持被动模式？

**A**: 服务端实现 `EPSV`/`PASV` 被动模式，客户端优先 `EPSV`、失败时回退 `PASV`：
- 兼容防火墙和 NAT 环境
- 客户端主动发起数据连接，无需服务器回连客户端
- 主流 FTP 云服务（阿里云、腾讯云）均采用被动模式

### Q2: 如何配置用户权限？

**A**: 通过 `FTPUser` 配置用户和家目录：

```csharp
var config = new ServerConfig();

config.Users["admin"] = new FTPUser("admin", "password", @"C:\FTPRoot\Admin");
config.Users["anonymous"] = new FTPUser("anonymous", "", @"C:\FTPRoot\Public");

var ftpServer = new FTPServer(config);
ftpServer.Start();
```

### Q3: 如何实现断点续传？

**A**: 同一次调用内的网络中断会自动重连，并优先通过 `REST` 从已传输位置继续。跨进程重启的续传需显式开启：

```csharp
var options = new FTPTransferOptions
{
    EnableResumeAcrossCalls = true,  // 默认 false
    KeepPartialOnFailure = true      // 失败时保留 .saea-part
};

var result = client.Download("big.zip", @"D:\Data\big.zip", options);
```

开启后会使用固定临时文件 `.<目标名>.saea-part` 与 `.meta` 校验信息；远端文件大小或修改时间变化时自动从 0 重新传输。并行传输不跨调用续传。

### Q4: 如何获取上传/下载进度？

**A**: 使用进度回调参数：

```csharp
client.Download("remote.txt", "local.txt", (offset, total) =>
{
    var percent = (double)offset / total * 100;
    Console.WriteLine($"进度: {percent:F2}%");
});
```

`FTPTransferResult` 同时提供 `BytesTransferred`、`Attempts`、`SessionsUsed`、`Resumed`、`ConflictAction` 与 `Error` 等统计信息。

### Q5: 如何支持匿名登录？

**A**: 使用 `anonymous` 用户名即可登录。若在 `ServerConfig.Users` 中配置了同名用户，则使用其根目录；未配置时以程序运行目录为根目录：

```csharp
config.Users["anonymous"] = new FTPUser("anonymous", "", @"C:\FTPRoot\Public");
```

### Q6: FTP 服务器默认端口是什么？

**A**: 
- **控制端口**: 默认 21（FTP 命令）
- **数据端口**: EPSV/PASV 模式下服务器动态分配；可通过 `PassivePortMin` / `PassivePortMax` 固定范围。

建议在生产环境中使用 21 端口，并开放被动数据端口范围。

### Q7: 如何处理大文件上传？

**A**: SAEA.FTP 会自动处理大文件：
- 流式传输，不一次性加载到内存
- 默认 64KB 缓冲区，运行时可在 16KB–1MB 之间自适应
- 超过 `ParallelThresholdBytes`（默认 8MB）时自动启用多连接分片并行
- 服务器不支持 `REST`、并发受限或达到 `MaxSessions` 时自动降级为单连接

```csharp
var options = new FTPTransferOptions
{
    MaxSessions = 4,                    // 含主连接
    ParallelThresholdBytes = 8 << 20,   // 8MB
    MinSegmentSize = 1 << 20,           // 1MB
    BufferSize = 64 * 1024
};
```

### Q8: 同名文件会被覆盖吗？

**A**: 不会静默覆盖。上传与下载都会先检测冲突，默认执行“跳过”，并通过 `ConflictHandler` 由调用方决定：

```csharp
var options = new FTPTransferOptions
{
    ConflictHandler = ctx =>
    {
        Console.WriteLine($"冲突: {ctx.RemotePath}");
        return new FTPConflictResult
        {
            Action = FTPConflictAction.Rename,   // Overwrite / Skip / Rename
            NewName = "backup.zip"
        };
    }
};

var upload = client.Upload(@"D:\Data\file.zip", options);

if (upload.Status == FTPTransferStatus.Skipped)
    Console.WriteLine("已跳过同名文件");
```

每个逻辑文件只回调一次，选择的目标名在后续会话与重试中保持一致。`FTPTransferResult.Status` 会返回 `Completed`、`Skipped`、`Renamed` 或 `Failed`，不会因“跳过”抛异常。

---

## 核心类 🔧

| 类名 | 说明 |
|------|------|
| `FTPServer` | FTP 服务器主类，按控制连接隔离会话 |
| `FTPClient` | FTP 客户端主类 |
| `FtpControlConnection` | 客户端控制连接（FEAT/REST/EPSV/PASV） |
| `FtpServerCommandHandler` | 服务端命令分发处理器，命令交给会话同步执行 |
| `FtpCommandDecoder / FtpCommandEncoder` | 控制通道的命令解码（服务端）与编码（客户端） |
| `FtpReplyDecoder / FtpReplyEncoder` | 控制通道的应答解码（客户端）与编码（服务端） |
| `FtpControlReplyHandler` | 客户端应答收集器，把链上应答交给阻塞读取方 |
| `FtpRawText` | 已自带换行的原样控制回复文本 |
| `FTPTransferOptions` | 重试、并行、缓冲与冲突策略 |
| `FTPTransferResult` | 传输结果与统计 |
| `FTPConflictContext/FTPConflictResult` | 同名冲突上下文与决策 |
| `ServerConfig` | 服务端配置（覆盖策略、端口范围、会话上限） |
| `ClientConfig` | 客户端配置（超时、缓冲区、编码） |
| `FTPServerConfigManager` | 服务端配置持久化 |
| `FTPUser` | FTP 用户凭证与根目录 |
| `FTPCommand` | FTP 命令枚举 |

---

## 使用示例 📝

### FTP 服务器完整配置

```csharp
using SAEA.FTP;

var serverConfig = new ServerConfig
{
    IP = "0.0.0.0",
    Port = 21,
    BufferSize = 1024 * 64,
    OverwritePolicy = FTPOverwritePolicy.Overwrite, // 默认：标准覆盖
    PassivePortMin = 50000,
    PassivePortMax = 50100,
    MaxSessionsPerUser = 0 // 0 = 不限制；可限制并行会话数
};

serverConfig.Users["admin"] = new FTPUser("admin", "admin123", @"C:\FTPRoot\Admin");
serverConfig.Users["user1"] = new FTPUser("user1", "pass123", @"C:\FTPRoot\User1");
serverConfig.Users["anonymous"] = new FTPUser("anonymous", "", @"C:\FTPRoot\Public");

// 需要持久化时再写入配置文件；直接传入配置对象不会读写全局配置
// FTPServerConfigManager.Get().Users["admin"] = serverConfig.Users["admin"];
// FTPServerConfigManager.Save();

var ftpServer = new FTPServer(serverConfig);

ftpServer.OnLog += (message, ex) =>
{
    Console.WriteLine(ex == null ? $"[FTP] {message}" : $"[FTP] {message}: {ex.Message}");
};

ftpServer.Start();
Console.WriteLine("FTP 服务器已启动");
```

> 服务端 `OverwritePolicy` 默认为 `Overwrite`，以兼容第三方客户端；设为 `Reject` 时已存在文件返回 `550`，设为 `Rename` 时自动改名并在 `226` 回复中附带 `NEWNAME=`。

### FTP 客户端 - 连接登录

```csharp
using SAEA.FTP;
using SAEA.Sockets.Pipeline;

var client = new FTPClient(
    ip: "127.0.0.1",
    port: 21,
    userName: "admin",
    password: "admin123");

// 连接建立与断开改由处理器链通知；必须在 Connect() 之前赋值
client.ConfigurePipeline = (id, pipeline) =>
{
    pipeline.AddLast(new ControlLifecycleHandler());
};

client.Connect();

var currentDir = client.CurrentDir();
Console.WriteLine($"当前目录: {currentDir}");

/// <summary>
/// 控制连接生命周期观察器：建链与断开各打印一次
/// </summary>
public sealed class ControlLifecycleHandler : InboundHandlerAdapter
{
    public override Task ChannelActiveAsync(IChannelHandlerContext ctx)
    {
        Console.WriteLine($"控制连接已建立: {ctx.ChannelId}");
        return Task.CompletedTask;
    }

    public override Task ChannelInactiveAsync(IChannelHandlerContext ctx)
    {
        Console.WriteLine($"控制连接已断开: {ctx.ChannelId}");
        return Task.CompletedTask;
    }
}
```

> `OnConnected`、`Ondisconnected` 事件已删除，建链/断开通知统一由 `ChannelActiveAsync`/`ChannelInactiveAsync` 承担。`ConfigurePipeline` 会在每条控制连接（含并行分片的独立会话）上调用一次，`Connect()` 之后再赋值会抛 `InvalidOperationException`。服务端 `FTPServer.ConfigurePipeline` 同理，必须在 `Start()` 之前赋值；`FTPServer.OnLog` 日志事件保留。

> 控制通道按行分帧，单条命令或应答最长 8KB（8192 字节，含换行符），超出上限的连接会被直接关闭。

### FTP 客户端 - 上传文件（带进度）

```csharp
var client = new FTPClient("127.0.0.1", 21, "admin", "admin123");
client.Connect();

var filePath = @"C:\Data\large_file.zip";
var fileName = Path.GetFileName(filePath);

Console.WriteLine($"开始上传: {fileName}");
var sw = Stopwatch.StartNew();

var options = new FTPTransferOptions
{
    MaxRetries = 3,
    RetryDelay = TimeSpan.FromMilliseconds(500),
    MaxSessions = 4
};

var result = client.Upload(filePath, options, (offset, total) =>
{
    var percent = (double)offset / total * 100;
    Console.Write($"\r上传进度: {percent:F1}%");
});

sw.Stop();

Console.WriteLine($"\n状态: {result.Status}, 会话: {result.SessionsUsed}, 尝试: {result.Attempts}, 耗时: {sw.Elapsed.TotalSeconds:F1}s");
```

### FTP 客户端 - 下载文件（带进度）

```csharp
var client = new FTPClient("127.0.0.1", 21, "admin", "admin123");
client.Connect();

var remoteFile = "document.pdf";
var localPath = @"C:\Downloads\document.pdf";

Console.WriteLine($"开始下载: {remoteFile}");
var sw = Stopwatch.StartNew();

var result = client.Download(remoteFile, localPath, new FTPTransferOptions(), (offset, total) =>
{
    var percent = (double)offset / total * 100;
    Console.Write($"\r下载进度: {percent:F1}%");
});

sw.Stop();

if (result.Status == FTPTransferStatus.Completed)
    Console.WriteLine($"\n下载完成! 本地路径: {result.LocalPath}, 耗时: {sw.Elapsed.TotalSeconds:F1}s");
else
    Console.WriteLine($"\n下载未完成: {result.Status}, 错误: {result.Error?.Message}");
```

### FTP 客户端 - 目录操作

```csharp
var client = new FTPClient("127.0.0.1", 21, "admin", "admin123");
client.Connect();

var list = client.Dir();
Console.WriteLine("目录列表:");
foreach (var item in list)
{
    Console.WriteLine($"  {item}");
}

client.ChangeDir("subfolder");
Console.WriteLine($"切换到: {client.CurrentDir()}");

client.MakeDir("new_folder");
Console.WriteLine("创建新目录");

client.RemoveDir("old_folder");
Console.WriteLine("删除目录");

client.ChangeToParentDir();
Console.WriteLine("返回上级目录");
```

### FTP 客户端 - 文件操作

```csharp
var client = new FTPClient("127.0.0.1", 21, "admin", "admin123");
client.Connect();

var size = client.FileSize("document.pdf");
Console.WriteLine($"文件大小: {size} bytes");

client.Delete("old_file.txt");
Console.WriteLine("文件已删除");

client.Rename("old_name.txt", "new_name.txt");
Console.WriteLine("文件已重命名");
```

### 配置管理

```csharp
using SAEA.FTP;

var serverConfig = new ServerConfig
{
    IP = "0.0.0.0",
    Port = 21,
    BufferSize = 1024 * 64,
    OverwritePolicy = FTPOverwritePolicy.Overwrite
};

serverConfig.Users["admin"] = new FTPUser("admin", "password", @"D:\FTPData\Admin");
serverConfig.Users["user1"] = new FTPUser("user1", "pass123", @"D:\FTPData\User1");
serverConfig.Users["anonymous"] = new FTPUser("anonymous", "", @"D:\FTPData\Public");

var clientConfig = new ClientConfig
{
    IP = "127.0.0.1",
    Port = 21,
    UserName = "admin",
    Password = "password",
    BufferSize = 64 * 1024,
    ConnectTimeout = TimeSpan.FromSeconds(30),
    CommandTimeout = TimeSpan.FromSeconds(30),
    DataTimeout = TimeSpan.FromSeconds(60),
    Encoding = System.Text.Encoding.UTF8
};

var client = new FTPClient(clientConfig);
client.Connect();
```

---

## 支持的 FTP 命令

| 命令 | 功能 | 服务端 | 客户端 |
|------|------|--------|--------|
| `USER/PASS` | 用户认证 | ✅ | ✅ |
| `FEAT` | 扩展能力协商 | ✅ | ✅ |
| `EPSV/PASV` | 扩展/标准被动模式 | ✅ | ✅ |
| `LIST/MLSD/NLST` | 目录列表 | ✅ | ✅ |
| `CWD/CDUP/PWD` | 目录操作 | ✅ | ✅ |
| `MKD/RMD` | 创建/删除目录 | ✅ | ✅ |
| `REST` | 断点续传偏移 | ✅ | ✅ |
| `STOR` | 上传文件 | ✅ | ✅ |
| `RETR` | 下载文件 | ✅ | ✅ |
| `SIZE` | 文件大小 | ✅ | ✅ |
| `MDTM` | 修改时间 | ✅ | ✅ |
| `DELE` | 删除文件 | ✅ | ✅ |
| `RNFR/RNTO` | 重命名 | ✅ | ✅ |
| `TYPE` | 传输类型 | ✅ | ✅ |
| `NOOP` | 心跳保活 | ✅ | ✅ |
| `QUIT` | 退出连接 | ✅ | ✅ |

> 服务端在 `FEAT` 中声明 `SAEA-OVERWRITE=Overwrite|Reject|Rename`，客户端据此决定并行上传与改名处理。传输范围限定为 `TYPE I` 二进制、UTF-8 文件名与扩展被动模式；不包含 FTPS/SFTP、主动模式与 `MODE B`。

---

## 依赖项

| 包名 | 说明 |
|------|------|
| SAEA.Sockets | 底层通信框架（项目引用，历史组件仍保留） |
| SAEA.Sockets.Pipeline | 处理器链运行时，控制通道的编解码与分发（项目引用） |
| SAEA.Common | 配置、工具类与日志支持 |

---

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包](https://www.nuget.org/packages/SAEA.FTP)
- [作者博客](https://www.cnblogs.com/yswenli/)

---

## 许可证

Apache License 2.0