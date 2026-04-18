# SAEA.DNS - DNS 服务器/客户端组件

[![NuGet version](https://img.shields.io/nuget/v/SAEA.DNS.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.DNS)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

## 简介

SAEA.DNS 是一个基于 SAEA.Sockets IOCP 技术的高性能 DNS 组件库，提供 DNS 服务器和 DNS 客户端功能。支持 UDP/TCP 双协议、多种 DNS 记录类型，适用于 DNS 代理服务器、自定义 DNS 解析、网络工具开发等场景。

## 特性

- **IOCP 高性能** - 基于 SAEA.Sockets 完成端口技术
- **DNS Server** - DNS 代理服务器
- **DNS Client** - 正向查询和反向查询
- **双协议支持** - UDP/TCP 自动切换
- **截断降级** - UDP 截断自动降级 TCP
- **多种记录类型** - A/AAAA/NS/CNAME/PTR/MX/TXT/SOA
- **本地记录配置** - 自定义 DNS 记录映射
- **事件驱动** - 请求/响应/错误事件回调

## NuGet 安装

```bash
# Package Manager
Install-Package SAEA.DNS -Version 7.26.2.2

# .NET CLI
dotnet add package SAEA.DNS --version 7.26.2.2
```

## 核心类

| 类名 | 说明 |
|------|------|
| `DnsServer` | DNS 服务器 |
| `DnsClient` | DNS 客户端 |
| `DnsRequestMessage` | DNS 请求消息 |
| `DnsResponseMessage` | DNS 响应消息 |
| `DnsRecords` | DNS 记录管理类 |
| `UdpRequestCoder` | UDP 协议编码器 |
| `TcpRequestCoder` | TCP 协议编码器 |
| `RecordType` | 记录类型枚举 |
| `ResponseCode` | 响应码枚举 |

## DNS 记录类型

| 类型 | 值 | 说明 |
|------|-----|------|
| A | 1 | IPv4 地址 |
| AAAA | 28 | IPv6 地址 |
| NS | 2 | 名称服务器 |
| CNAME | 5 | 规范名称别名 |
| SOA | 6 | 授权起始记录 |
| PTR | 12 | 反向查询指针 |
| MX | 15 | 邮件交换服务器 |
| TXT | 16 | 文本记录 |
| SRV | 33 | 服务定位记录 |

## 快速使用

### DNS 服务器

```csharp
using SAEA.DNS;

// 创建 DNS 服务器
var server = new DnsServer(port: 53);

// 注册事件
server.OnRequested += (sender, e) => 
{
    var request = e.Request;
    Console.WriteLine($"DNS 请求: {request.Questions[0].Domain}");
};

server.OnResponded += (sender, e) => 
{
    var response = e.Response;
    Console.WriteLine($"DNS 响应已发送");
};

server.OnErrored += (sender, e) => 
{
    Console.WriteLine($"DNS 错误: {e.Exception.Message}");
};

// 启动服务器
server.Start();

Console.WriteLine("DNS 服务器已启动，端口: 53");
```

### 自定义 DNS 记录

```csharp
using SAEA.DNS;
using SAEA.DNS.Model;

// 创建 DNS 记录映射
var records = new DnsRecords();

// 添加 A 记录
records.Add("www.example.com", "192.168.1.100");
records.Add("api.example.com", "192.168.1.101");

// 添加 CNAME 记录
records.AddCNAME("alias.example.com", "www.example.com");

// 配置服务器使用自定义记录
var server = new DnsServer(records);
server.Start();

// 查询 www.example.com 将返回 192.168.1.100
```

### DNS 客户端查询

```csharp
using SAEA.DNS;
using System.Net;

// 创建 DNS 客户端（指定上游 DNS 服务器）
var dnsServer = new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53);
var client = new DnsClient(dnsServer);

// 查询 A 记录（IPv4）
var response = client.Query("www.google.com", RecordType.A);
foreach (var record in response.Answers)
{
    Console.WriteLine($"IP: {record}");
}

// 查询 AAAA 记录（IPv6）
var ipv6Response = client.Query("www.google.com", RecordType.AAAA);

// 查询 MX 记录
var mxResponse = client.Query("gmail.com", RecordType.MX);
```

### 反向 DNS 查询

```csharp
using SAEA.DNS;

var client = new DnsClient(new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53));

// 反向查询 IP 对应的域名
var ip = "8.8.8.8";
var ptrName = DnsClient.GetPTRName(ip);
var response = client.Query(ptrName, RecordType.PTR);

foreach (var record in response.Answers)
{
    Console.WriteLine($"域名: {record}");
}
```

### DNS 代理服务器

```csharp
using SAEA.DNS;
using SAEA.DNS.Model;
using System.Net;

// 创建代理 DNS 服务器
var upstreamDNS = new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53);
var upstreamClient = new DnsClient(upstreamDNS);

// 本地自定义记录
var localRecords = new DnsRecords();
localRecords.Add("internal.local", "10.0.0.1");

var server = new DnsServer((sender, e) => 
{
    var domain = e.Request.Questions[0].Domain.ToString();
    
    // 先检查本地记录
    if (localRecords.Contains(domain))
    {
        e.Response = localRecords.GetResponse(e.Request);
        return;
    }
    
    // 转发到上游 DNS
    e.Response = upstreamClient.Query(e.Request);
});

server.Start();
```

### UDP/TCP 协议切换

客户端自动处理 UDP 截断降级到 TCP：

```csharp
// 当 UDP 响应被截断（Truncated=true）时，自动使用 TCP 重新查询
var client = new DnsClient(new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53));
var response = client.Query("large-domain.com", RecordType.TXT);

// 框架自动处理：
// 1. 首先使用 UDP 查询
// 2. 如果响应截断，自动切换到 TCP
```

### 直接使用编码器

```csharp
using SAEA.DNS.Coder;
using SAEA.DNS.Protocol;

var dnsEndpoint = new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53);

// UDP 编码器（自动降级到 TCP）
var coder = new UdpRequestCoder(dnsEndpoint, new TcpRequestCoder(dnsEndpoint));

// 创建请求
var request = new DnsRequestMessage();
request.Questions.Add(new Question(new Domain("www.google.com"), RecordType.A, RecordClass.IN));

// 发送请求
var response = coder.Code(request);

// 处理响应
foreach (var answer in response.Answers)
{
    Console.WriteLine(answer);
}
```

## DNS 消息结构

```
DNS Message:
├── Header
│   ├── ID (16 bits)
│   ├── Flags (16 bits)
│   │   ├── QR (Query/Response)
│   │   ├── Opcode
│   │   ├── AA (Authoritative Answer)
│   │   ├── TC (Truncated)
│   │   ├── RD (Recursion Desired)
│   │   ├── RA (Recursion Available)
│   │   └── Rcode (Response Code)
│   ├── QDCOUNT (Question Count)
│   ├── ANCOUNT (Answer Count)
│   ├── NSCOUNT (NS Count)
│   └── ARCOUNT (Additional Count)
├── Questions
├── Answers
├── Authority Records
└── Additional Records
```

## 应用场景

- **DNS 代理服务器** - 企业内部 DNS 缓存代理
- **自定义 DNS** - 内网域名映射
- **DNS 客户端工具** - DNS 查询工具开发
- **网络调试** - DNS 解析调试

## 依赖项

| 包名 | 版本 | 说明 |
|------|------|------|
| SAEA.Sockets | 7.26.2.2 | IOCP 通信框架 |
| SAEA.Common | 7.26.2.2 | 公共工具类 |

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包](https://www.nuget.org/packages/SAEA.DNS)
- [作者博客](https://www.cnblogs.com/yswenli/)

## 许可证

Apache License 2.0