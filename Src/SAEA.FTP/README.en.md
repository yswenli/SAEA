# SAEA.FTP - FTP Server/Client Component 🗂️

[![NuGet version](https://img.shields.io/nuget/v/SAEA.FTP.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.FTP)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**English Version** | **[中文版](README.md)**

> FTP server/client component based on TCP sockets, supporting passive mode, resume, automatic retry, name-conflict handling, and multi-connection parallel transfer for large files.
>
> ⚡ **Reliable transfer core**: `REST` resume + exponential backoff retries + adaptive buffering. Large files are split into segments and transferred over multiple control sessions, automatically falling back to a single connection when the server does not support it.

## Quick Navigation 🧭

| Section | Content |
|---------|---------|
| [⚡ 30-Second Quick Start](#30-second-quick-start) | Simplest getting started example |
| [🎯 Core Features](#core-features) | Main component features |
| [📐 Architecture Design](#architecture-design) | Component relationships and workflow |
| [💡 Use Cases](#use-cases) | When to choose SAEA.FTP |
| [📊 Performance Comparison](#performance-comparison) | Comparison with other solutions |
| [❓ FAQ](#faq) | Quick answers to common questions |
| [🔧 Core Classes](#core-classes) | Overview of main classes |
| [📝 Usage Examples](#usage-examples) | Detailed code examples |

---

## 30-Second Quick Start ⚡

The fastest way to get started - run an FTP server in just 3 steps:

### Step 1: Install NuGet Package

```bash
dotnet add package SAEA.FTP
```

### Step 2: Create FTP Server

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

> You can also log in as `anonymous`; without an explicit user entry it uses the application directory as its root.

### Step 3: Create FTP Client Connection

```csharp
var client = new FTPClient("127.0.0.1", 21, "admin", "password");
client.Connect();

// Upload file
client.Upload(@"C:\Data\file.txt", (offset, total) => 
    Console.WriteLine($"Upload progress: {(double)offset / total * 100:F0}%"));
```

**That's it!** 🎉 You've implemented an FTP file server with high-speed transfer support.

---

## Core Features 🎯

| Feature | Description | Advantage |
|---------|-------------|-----------|
| 🚀 **High Performance** | Control channel runs on the SAEA.Sockets handler chain, one session per control link | Supports concurrent file transfers |
| 📡 **EPSV/PASV Passive Mode** | Extension passive mode with PASV fallback | Firewall and NAT friendly |
| 📊 **Progress Callback** | Real-time upload/download progress | Progress bars and throughput stats |
| 🔁 **Retry & Resume** | Auto reconnect, `REST` resume first | More reliable large-file transfers |
| ⚡ **Per-file Parallelism** | Multiple control sessions with segmented transfer | Much faster large-file transfers |
| 🛡️ **Conflict Handling** | Overwrite / Skip / Rename, one callback per file | Skips by default, never silently overwrites |
| 🧠 **Adaptive Buffer** | Dynamically 16KB–1MB | Balances throughput and memory |
| 🔐 **User Authentication** | Multi-user, anonymous login, session limits | Flexible access control |
| 📁 **Directory Management** | Complete directory operation support | Create, delete, traverse directories |
| 🔄 **File Operations** | Upload, download, rename, delete | Complete FTP command support |
| 💾 **Configuration Persistence** | Automatic user configuration saving | No database required, ready to use |
| 🌐 **UTF-8 Support** | Supports Chinese filenames | Good cross-platform compatibility |

---

## Architecture Design 📐

### Component Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                     SAEA.FTP Architecture                    │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌────────────────────┐      ┌────────────────────┐        │
│  │    FTPServer       │      │    FTPClient       │        │
│  │   (FTP Server)     │      │   (FTP Client)     │        │
│  └─────────┬──────────┘      └─────────┬──────────┘        │
│            │                           │                     │
│            │   ┌───────────────────────┘                    │
│            │   │                                            │
│  ┌─────────▼───▼─────────┐                                 │
│  │ FtpControlConnection  │  Control / FEAT / REST sessions │
│  │ ServerSession         │  One session per control link   │
│  └───────────┬───────────┘                                 │
│              │                                              │
│  ┌───────────▼───────────┐                                 │
│  │ FTPTransferOptions    │  Retry / parallel / conflict    │
│  │ FTPTransferResult     │  Result and statistics          │
│  └───────────┬───────────┘                                 │
│              │                                              │
│  ┌───────────▼───────────┐                                 │
│  │ SAEA.Sockets.Pipeline │  Control link (handler chain)   │
│  │ TcpClient             │  Data connection                │
│  └───────────────────────┘                                 │
│                                                             │
│  ┌───────────────────────┐                                 │
│  │   ConfigManager       │  Configuration persistence      │
│  │   FTPUser             │  Credentials and root directory │
│  └───────────────────────┘                                 │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### FTP Transfer Flow Diagram

```
Client Connection Flow:

Client ──► TCP Port 21 ──► FTPServer.Accepted
                                │
                                ▼
                       ┌─────────────────┐
                       │  User Auth      │
                       │ USER/PASS       │
                       └─────────────────┘
                                │
                                ▼
                       ┌─────────────────┐
                       │ Enter Command   │
                       │ Mode            │
                       │ Wait FTP cmds   │
                       └─────────────────┘

PASV Passive Mode File Transfer:

Client ──► EPSV command (fallback to PASV) ──► Server allocates data port
                               │
                               ▼
                     ┌─────────────────┐
                     │ 229/227 Reply   │
                     │ Port / IP:Port  │
                     └─────────────────┘
                               │
                               ▼
               Client connects to data port (non-21 port)
                               │
                               ▼
                     ┌─────────────────┐
                     │ STOR/RETR       │
                     │ Upload/Download │
                     └─────────────────┘
                               │
                               ▼
                     ┌─────────────────┐
                     │ Progress        │
                     │ Callback        │
                     │ (offset, total) │
                     └─────────────────┘
                               │
                               ▼
                     Transfer complete, close data connection
```

---

## Use Cases 💡

### ✅ Scenarios Suitable for SAEA.FTP

| Scenario | Description | Reason for Recommendation |
|----------|-------------|---------------------------|
| 📁 **Enterprise File Server** | Internal file sharing and distribution | One session per control link, multi-user concurrency |
| 🔄 **Automated File Transfer** | Scheduled sync, backup tasks | Clean API, easy integration into automation scripts |
| 💾 **Data Backup System** | Remote file backup | Progress callback, large file transfer support |
| 🖥️ **FTP Client Tools** | Developing FTP management software | Complete client implementation, all common commands supported |
| 📱 **Mobile Backend** | File upload/download service | Passive mode firewall compatible |
| 🏢 **Enterprise Data Exchange** | Cross-department file transfer | User authentication, permission management |

### ❌ Unsuitable Scenarios

| Scenario | Recommended Alternative |
|----------|------------------------|
| HTTP File Upload | Use SAEA.Http or ASP.NET |
| Large-scale CDN Distribution | Use professional CDN services |
| Real-time Video Streaming | Use SAEA.Sockets with custom protocol |
| Requires PORT Active Mode | Currently only supports EPSV/PASV passive mode |

---

## Performance Comparison 📊

### Compared with a Fixed Single-Connection Approach

| Capability | SAEA.FTP | Fixed Single Connection |
|------------|----------|-------------------------|
| **Large files** | Automatically segments across multiple sessions above `ParallelThresholdBytes` (8MB by default) | Sequential single-connection transfer |
| **Interruption recovery** | In-call `REST` resume + exponential backoff retry | Usually restarts the whole file |
| **Buffering** | Adaptive 16KB–1MB | Fixed size |
| **Name conflicts** | Overwrite / Skip / Rename callback, skip by default | Often silently overwrites |
| **Concurrency** | Configurable `MaxSessions`; falls back when unsupported | None |

> 💡 Real throughput depends on the network, disk, server concurrency limits, and passive port configuration. The table describes transfer-strategy differences, not guaranteed speed multipliers.

### EPSV/PASV Passive Mode Advantages

| Feature | EPSV/PASV Passive Mode | PORT Active Mode |
|---------|------------------------|------------------|
| **Firewall Compatibility** | ✅ High | ❌ Low |
| **Client Connection** | Client initiates data connection | Server actively connects to client |
| **Security** | ✅ Better | ⚠️ Lower |
| **Applicable Scenarios** | Modern network environments | Legacy networks |

> 💡 **Tip**: The server supports both `EPSV` and `PASV` passive mode. The client prefers `EPSV` and automatically falls back to `PASV`; `PORT` active mode is not supported.

---

## FAQ ❓

### Q1: Why is only passive mode supported?

**A**: The server implements `EPSV`/`PASV` passive mode; the client prefers `EPSV` and falls back to `PASV`:
- Compatible with firewalls and NAT environments
- The client initiates the data connection, so the server never connects back
- Mainstream FTP cloud services (Alibaba Cloud, Tencent Cloud) use passive mode

### Q2: How to configure user permissions?

**A**: Configure users and home directories through `FTPUser`:

```csharp
var config = new ServerConfig();

config.Users["admin"] = new FTPUser("admin", "password", @"C:\FTPRoot\Admin");
config.Users["anonymous"] = new FTPUser("anonymous", "", @"C:\FTPRoot\Public");

var ftpServer = new FTPServer(config);
ftpServer.Start();
```

### Q3: How to implement resume transfer?

**A**: A network interruption within the same call automatically reconnects and resumes from the transferred offset via `REST`. Cross-process resume must be enabled explicitly:

```csharp
var options = new FTPTransferOptions
{
    EnableResumeAcrossCalls = true,  // default false
    KeepPartialOnFailure = true      // keep .saea-part on failure
};

var result = client.Download("big.zip", @"D:\Data\big.zip", options);
```

When enabled, a deterministic `.<target>.saea-part` file and a `.meta` checksum record are used. If the remote size or modification time changed, the download restarts from zero. Parallel transfers never resume across calls.

### Q4: How to get upload/download progress?

**A**: Use progress callback parameters:

```csharp
client.Download("remote.txt", "local.txt", (offset, total) =>
{
    var percent = (double)offset / total * 100;
    Console.WriteLine($"Progress: {percent:F2}%");
});
```

`FTPTransferResult` also exposes `BytesTransferred`, `Attempts`, `SessionsUsed`, `Resumed`, `ConflictAction`, and `Error`.

### Q5: How to support anonymous login?

**A**: Log in with the `anonymous` user. If a matching entry exists in `ServerConfig.Users`, its root directory is used; otherwise the application directory is used:

```csharp
config.Users["anonymous"] = new FTPUser("anonymous", "", @"C:\FTPRoot\Public");
```

### Q6: What is the default FTP server port?

**A**: 
- **Control Port**: Default 21 (FTP commands)
- **Data Port**: Dynamically allocated in EPSV/PASV mode; pin a range with `PassivePortMin` / `PassivePortMax`.

It's recommended to use port 21 in production and open the passive data port range.

### Q7: How to handle large file uploads?

**A**: SAEA.FTP handles large files automatically:
- Streaming transfer, never loads the whole file into memory
- 64KB default buffer, adaptively resized between 16KB and 1MB
- Automatically enables multi-connection segmented transfer above `ParallelThresholdBytes` (8MB by default)
- Falls back to a single connection when the server lacks `REST`, limits concurrency, or `MaxSessions` is reached

```csharp
var options = new FTPTransferOptions
{
    MaxSessions = 4,                    // including the main connection
    ParallelThresholdBytes = 8 << 20,   // 8MB
    MinSegmentSize = 1 << 20,           // 1MB
    BufferSize = 64 * 1024
};
```

### Q8: Will existing files be overwritten?

**A**: Never silently. Both upload and download detect conflicts and skip by default. A `ConflictHandler` lets the caller decide:

```csharp
var options = new FTPTransferOptions
{
    ConflictHandler = ctx =>
    {
        Console.WriteLine($"Conflict: {ctx.RemotePath}");
        return new FTPConflictResult
        {
            Action = FTPConflictAction.Rename,   // Overwrite / Skip / Rename
            NewName = "backup.zip"
        };
    }
};

var upload = client.Upload(@"D:\Data\file.zip", options);

if (upload.Status == FTPTransferStatus.Skipped)
    Console.WriteLine("Existing file skipped");
```

The handler is invoked once per logical file, and the chosen target name stays consistent across sessions and retries. `FTPTransferResult.Status` returns `Completed`, `Skipped`, `Renamed`, or `Failed`; a skip never throws.

---

## Core Classes 🔧

| Class Name | Description |
|------------|-------------|
| `FTPServer` | FTP server main class; one session per control connection |
| `FTPClient` | FTP client main class |
| `FtpControlConnection` | Client control connection (FEAT/REST/EPSV/PASV) |
| `FtpServerCommandHandler` | Server command dispatcher; commands run synchronously in the session |
| `FtpCommandDecoder / FtpCommandEncoder` | Control channel command decoder (server) and encoder (client) |
| `FtpReplyDecoder / FtpReplyEncoder` | Control channel reply decoder (client) and encoder (server) |
| `FtpControlReplyHandler` | Client reply collector that feeds blocking readers |
| `FtpRawText` | Pre-formatted raw control reply text |
| `FTPTransferOptions` | Retry, parallel, buffering, and conflict options |
| `FTPTransferResult` | Transfer result and statistics |
| `FTPConflictContext/FTPConflictResult` | Conflict context and decision |
| `ServerConfig` | Server options (overwrite policy, port range, session limit) |
| `ClientConfig` | Client options (timeouts, buffer, encoding) |
| `FTPServerConfigManager` | Server configuration persistence |
| `FTPUser` | FTP credentials and root directory |
| `FTPCommand` | FTP command enumeration |

---

## Usage Examples 📝

### FTP Server Complete Configuration

```csharp
using SAEA.FTP;

var serverConfig = new ServerConfig
{
    IP = "0.0.0.0",
    Port = 21,
    BufferSize = 1024 * 64,
    OverwritePolicy = FTPOverwritePolicy.Overwrite, // default: standard overwrite
    PassivePortMin = 50000,
    PassivePortMax = 50100,
    MaxSessionsPerUser = 0 // 0 = unlimited; set to cap parallel sessions
};

serverConfig.Users["admin"] = new FTPUser("admin", "admin123", @"C:\FTPRoot\Admin");
serverConfig.Users["user1"] = new FTPUser("user1", "pass123", @"C:\FTPRoot\User1");
serverConfig.Users["anonymous"] = new FTPUser("anonymous", "", @"C:\FTPRoot\Public");

// Persist only when needed; passing a config object does not read/write global config
// FTPServerConfigManager.Get().Users["admin"] = serverConfig.Users["admin"];
// FTPServerConfigManager.Save();

var ftpServer = new FTPServer(serverConfig);

ftpServer.OnLog += (message, ex) =>
{
    Console.WriteLine(ex == null ? $"[FTP] {message}" : $"[FTP] {message}: {ex.Message}");
};

ftpServer.Start();
Console.WriteLine("FTP server started");
```

> The server `OverwritePolicy` defaults to `Overwrite` for third-party client compatibility. `Reject` returns `550` when the file exists; `Rename` generates a unique name and appends `NEWNAME=` to the `226` reply.

### FTP Client - Connect and Login

```csharp
using SAEA.FTP;
using SAEA.Sockets.Pipeline;
using System.Threading.Tasks;

var client = new FTPClient(
    ip: "127.0.0.1",
    port: 21,
    userName: "admin",
    password: "admin123");

// Connect/disconnect notifications now come from the handler chain; assign before Connect()
client.ConfigurePipeline = (id, pipeline) =>
{
    pipeline.AddLast(new ControlLifecycleHandler());
};

client.Connect();

var currentDir = client.CurrentDir();
Console.WriteLine($"Current directory: {currentDir}");

/// <summary>
/// Control connection lifecycle observer: logs once on open and once on close
/// </summary>
public sealed class ControlLifecycleHandler : InboundHandlerAdapter
{
    public override Task ChannelActiveAsync(IChannelHandlerContext ctx)
    {
        Console.WriteLine($"Control connection opened: {ctx.ChannelId}");
        return Task.CompletedTask;
    }

    public override Task ChannelInactiveAsync(IChannelHandlerContext ctx)
    {
        Console.WriteLine($"Control connection closed: {ctx.ChannelId}");
        return Task.CompletedTask;
    }
}
```

> The `OnConnected` and `Ondisconnected` events are removed; open/close notifications are delivered through `ChannelActiveAsync`/`ChannelInactiveAsync`. `ConfigurePipeline` runs once per control connection (including the extra sessions created for segmented transfers), and assigning it after `Connect()` throws `InvalidOperationException`. The same applies to `FTPServer.ConfigurePipeline`, which must be assigned before `Start()`; the `FTPServer.OnLog` log event is retained.

> The control channel is framed by lines, so a single command or reply is limited to 8KB (8192 bytes including the newline); connections exceeding the limit are closed.

### FTP Client - Upload File (with Progress)

```csharp
var client = new FTPClient("127.0.0.1", 21, "admin", "admin123");
client.Connect();

var filePath = @"C:\Data\large_file.zip";
var fileName = Path.GetFileName(filePath);

Console.WriteLine($"Starting upload: {fileName}");
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
    Console.Write($"\rUpload progress: {percent:F1}%");
});

sw.Stop();

Console.WriteLine($"\nStatus: {result.Status}, Sessions: {result.SessionsUsed}, Attempts: {result.Attempts}, Elapsed: {sw.Elapsed.TotalSeconds:F1}s");
```

### FTP Client - Download File (with Progress)

```csharp
var client = new FTPClient("127.0.0.1", 21, "admin", "admin123");
client.Connect();

var remoteFile = "document.pdf";
var localPath = @"C:\Downloads\document.pdf";

Console.WriteLine($"Starting download: {remoteFile}");
var sw = Stopwatch.StartNew();

var result = client.Download(remoteFile, localPath, new FTPTransferOptions(), (offset, total) =>
{
    var percent = (double)offset / total * 100;
    Console.Write($"\rDownload progress: {percent:F1}%");
});

sw.Stop();

if (result.Status == FTPTransferStatus.Completed)
    Console.WriteLine($"\nDownload complete! Local path: {result.LocalPath}, Elapsed: {sw.Elapsed.TotalSeconds:F1}s");
else
    Console.WriteLine($"\nDownload not completed: {result.Status}, Error: {result.Error?.Message}");
```

### FTP Client - Directory Operations

```csharp
var client = new FTPClient("127.0.0.1", 21, "admin", "admin123");
client.Connect();

var list = client.Dir();
Console.WriteLine("Directory listing:");
foreach (var item in list)
{
    Console.WriteLine($"  {item}");
}

client.ChangeDir("subfolder");
Console.WriteLine($"Changed to: {client.CurrentDir()}");

client.MakeDir("new_folder");
Console.WriteLine("Created new directory");

client.RemoveDir("old_folder");
Console.WriteLine("Deleted directory");

client.ChangeToParentDir();
Console.WriteLine("Returned to parent directory");
```

### FTP Client - File Operations

```csharp
var client = new FTPClient("127.0.0.1", 21, "admin", "admin123");
client.Connect();

var size = client.FileSize("document.pdf");
Console.WriteLine($"File size: {size} bytes");

client.Delete("old_file.txt");
Console.WriteLine("File deleted");

client.Rename("old_name.txt", "new_name.txt");
Console.WriteLine("File renamed");
```

### Configuration Management

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

## Supported FTP Commands

| Command | Function | Server | Client |
|---------|----------|--------|--------|
| `USER/PASS` | User authentication | ✅ | ✅ |
| `FEAT` | Extension negotiation | ✅ | ✅ |
| `EPSV/PASV` | Extended/standard passive mode | ✅ | ✅ |
| `LIST/MLSD/NLST` | Directory listing | ✅ | ✅ |
| `CWD/CDUP/PWD` | Directory operations | ✅ | ✅ |
| `MKD/RMD` | Create/delete directory | ✅ | ✅ |
| `REST` | Resume offset | ✅ | ✅ |
| `STOR` | Upload file | ✅ | ✅ |
| `RETR` | Download file | ✅ | ✅ |
| `SIZE` | File size | ✅ | ✅ |
| `MDTM` | Modification time | ✅ | ✅ |
| `DELE` | Delete file | ✅ | ✅ |
| `RNFR/RNTO` | Rename | ✅ | ✅ |
| `TYPE` | Transfer type | ✅ | ✅ |
| `NOOP` | Keep-alive | ✅ | ✅ |
| `QUIT` | Disconnect | ✅ | ✅ |

> The server advertises `SAEA-OVERWRITE=Overwrite|Reject|Rename` in `FEAT`, and the client uses it to decide parallel upload and rename handling. Scope is `TYPE I` binary, UTF-8 filenames, and extended passive mode; FTPS/SFTP, active mode, and `MODE B` are not included.

---

## Dependencies

| Package | Description |
|---------|-------------|
| SAEA.Sockets | Underlying communication framework (project reference; legacy components retained) |
| SAEA.Sockets.Pipeline | Handler chain runtime for control-channel codecs and dispatch (project reference) |
| SAEA.Common | Configuration, utilities, and logging support |

---

## More Resources

- [GitHub Repository](https://github.com/yswenli/SAEA)
- [NuGet Package](https://www.nuget.org/packages/SAEA.FTP)
- [Author's Blog](https://www.cnblogs.com/yswenli/)

---

## License

Apache License 2.0