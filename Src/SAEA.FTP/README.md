# SAEA.FTP - FTP 服务器/客户端组件

[![NuGet version](https://img.shields.io/nuget/v/SAEA.FTP.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.FTP)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

## 简介

SAEA.FTP 是一个基于 SAEA.Sockets 高性能 IOCP 异步通信框架实现的 FTP 服务器/客户端组件。提供完整的 FTP 协议支持，包括用户认证、被动模式传输、文件上传下载、目录管理等功能，适用于文件服务器、FTP 客户端工具、自动化文件传输等场景。

## 特性

- **IOCP 高性能** - 基于 SAEA.Sockets 完成端口技术
- **FTP Server** - 完整的 FTP 服务器实现
- **FTP Client** - 完整的 FTP 客户端实现
- **被动模式** - PASV 数据传输模式
- **进度回调** - 上传下载实时进度
- **匿名用户** - 支持匿名登录
- **UTF-8** - 支持 UTF-8 编码文件名
- **配置持久化** - 用户配置自动保存

## NuGet 安装

```bash
# Package Manager
Install-Package SAEA.FTP -Version 7.26.2.2

# .NET CLI
dotnet add package SAEA.FTP --version 7.26.2.2
```

## 核心类

| 类名 | 说明 |
|------|------|
| `FTPServer` | FTP 服务器主类 |
| `FTPClient` | FTP 客户端主类 |
| `FTPDataManager` | 数据接收管理 |
| `FTPStream` | 数据流缓存处理 |
| `FTPServerConfigManager` | 服务端配置管理 |
| `FTPClientConfigManager` | 客户端配置管理 |
| `FTPUser` | FTP 用户模型 |
| `FTPCommand` | FTP 命令枚举 |

## 快速使用

### FTP 服务器

```csharp
using SAEA.FTP;

// 创建配置
var serverConfig = new ServerConfig
{
    IP = "127.0.0.1",
    Port = 21,
    BufferSize = 1024 * 64,
    Root = @"C:\FTPRoot"  // FTP 根目录
};

// 保存配置
FTPServerConfigManager.Save();

// 创建 FTP 服务器
var ftpServer = new FTPServer(serverConfig.IP, serverConfig.Port, serverConfig.BufferSize);

// 注册日志事件
ftpServer.OnLog += (message) => 
{
    Console.WriteLine(message);
};

// 启动服务器
ftpServer.Start();

Console.WriteLine("FTP 服务器已启动，端口: 21");
```

### FTP 客户端 - 连接

```csharp
using SAEA.FTP;

// 创建 FTP 客户端
var client = new FTPClient(ip: "127.0.0.1", port: 21, username: "admin", pwd: "password");

// 注册事件
client.OnDisconnected += () => Console.WriteLine("连接断开");

// 连接服务器
client.Connect();

// 获取当前目录
var currentDir = client.CurrentDir();
Console.WriteLine($"当前目录: {currentDir}");
```

### FTP 客户端 - 上传文件

```csharp
using SAEA.FTP;

var client = new FTPClient("127.0.0.1", 21, "admin", "password");
client.Connect();

// 上传文件（带进度）
var filePath = @"C:\Data\document.pdf";
client.Upload(filePath, (offset, total) => 
{
    var progress = (double)offset / total * 100;
    Console.WriteLine($"上传进度: {progress:F2}% ({offset / 1024}KB / {total / 1024}KB)");
});

Console.WriteLine("上传完成");
```

### FTP 客户端 - 下载文件

```csharp
using SAEA.FTP;

var client = new FTPClient("127.0.0.1", 21, "admin", "password");
client.Connect();

// 下载文件（带进度）
var fileName = "document.pdf";
var localPath = @"C:\Downloads\document.pdf";
client.Download(fileName, localPath, (offset, total) => 
{
    var progress = (double)offset / total * 100;
    Console.WriteLine($"下载进度: {progress:F2}%");
});

Console.WriteLine("下载完成");
```

### FTP 客户端 - 目录操作

```csharp
using SAEA.FTP;

var client = new FTPClient("127.0.0.1", 21, "admin", "password");
client.Connect();

// 列出目录内容（详细列表）
var list = client.List();
foreach (var item in list)
{
    Console.WriteLine(item);
}

// 切换目录
client.CWD("subfolder");

// 创建目录
client.MKD("new_folder");

// 删除目录
client.RMD("old_folder");

// 切换到父目录
client.CDUP();
```

### FTP 客户端 - 文件操作

```csharp
using SAEA.FTP;

var client = new FTPClient("127.0.0.1", 21, "admin", "password");
client.Connect();

// 删除文件
client.DELE("old_file.txt");

// 重命名文件
client.RNFR("old_name.txt");  // 指定原文件名
client.RNTO("new_name.txt");  // 指定新文件名

// 获取文件大小
var size = client.SIZE("document.pdf");
Console.WriteLine($"文件大小: {size} bytes");
```

### 配置用户

```csharp
using SAEA.FTP;

// 服务端配置用户
var config = new ServerConfig();
config.Users = new List<FTPUser>
{
    new FTPUser { Username = "admin", Password = "password", HomeDir = @"C:\FTPRoot\Admin" },
    new FTPUser { Username = "user1", Password = "pass123", HomeDir = @"C:\FTPRoot\User1" }
};

FTPServerConfigManager.Save();

// 客户端使用配置文件
var clientConfig = FTPClientConfigManager.Read();
clientConfig.IP = "127.0.0.1";
clientConfig.Port = 21;
clientConfig.Username = "admin";
clientConfig.Password = "password";
FTPClientConfigManager.Write();
```

## 支持的 FTP 命令

| 命令 | 功能 | 服务端 | 客户端 |
|------|------|--------|--------|
| `USER/PASS` | 用户认证 | ✅ | ✅ |
| `PASV` | 被动模式 | ✅ | ✅ |
| `LIST/MLSD/NLST` | 目录列表 | ✅ | ✅ |
| `CWD/CDUP/PWD` | 目录操作 | ✅ | ✅ |
| `MKD/RMD` | 创建/删除目录 | ✅ | ✅ |
| `STOR/RETR` | 上传/下载 | ✅ | ✅ |
| `DELE` | 删除文件 | ✅ | ✅ |
| `RNFR/RNTO` | 重命名 | - | ✅ |
| `SIZE` | 文件大小 | ✅ | ✅ |
| `NOOP` | 心跳保活 | ✅ | ✅ |
| `QUIT` | 退出连接 | ✅ | ✅ |

## 被动模式 (PASV)

SAEA.FTP 仅支持 PASV 被动模式进行数据传输：

```
客户端 → 服务器: PASV
服务器 → 客户端: 227 Entering Passive Mode (IP, Port)
客户端: 连接服务器指定的数据端口
客户端 ↔ 服务器: 数据传输
```

## 应用场景

- **文件服务器** - 企业内部文件共享
- **FTP 客户端工具** - 文件管理工具开发
- **自动化传输** - 定时文件同步
- **备份系统** - 远程文件备份

## 依赖项

| 包名 | 版本 | 说明 |
|------|------|------|
| SAEA.Sockets | 7.26.2.2 | IOCP 通信框架 |
| SAEA.Common | 7.26.2.2 | 公共工具类 |

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包](https://www.nuget.org/packages/SAEA.FTP)
- [作者博客](https://www.cnblogs.com/yswenli/)

## 许可证

Apache License 2.0