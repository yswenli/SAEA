# SAEA.DNS - 高性能 DNS 服务器/客户端组件 🌐

[![NuGet version](https://img.shields.io/nuget/v/SAEA.DNS.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.DNS)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**[English Version](README.en.md)** | **中文版**

> 基于 SAEA.Sockets IOCP 技术的高性能 DNS 组件，支持 DNS 服务器/客户端、UDP/TCP 双协议、多种记录类型。

## 快速导航 🧭

| 章节 | 内容 |
|------|------|
| [⚡ 30秒快速开始](#30秒快速开始) | 最简单的上手示例 |
| [🎯 核心特性](#核心特性) | 组件的主要功能 |
| [📐 架构设计](#架构设计) | 组件关系与工作流程 |
| [💡 应用场景](#应用场景) | 何时选择 SAEA.DNS |
| [📊 性能对比](#性能对比) | 与其他方案对比 |
| [❓ 常见问题](#常见问题) | FAQ 快速解答 |
| [🔧 核心类](#核心类) | 主要类一览 |
| [📝 使用示例](#使用示例) | 详细代码示例 |

---

## 30秒快速开始 ⚡

最快上手方式，只需3步即可运行 DNS 服务器和客户端：

### Step 1: 安装 NuGet 包

```bash
dotnet add package SAEA.DNS
```

### Step 2: 创建 DNS 服务器（仅需5行代码）

```csharp
using SAEA.DNS;

var server = new DnsServer(port: 53);
server.OnRequested += (s, e) => Console.WriteLine($"查询: {e.Request.Questions[0].Domain}");
server.Start();
```

### Step 3: 创建 DNS 客户端查询

```csharp
using SAEA.DNS;
using System.Net;

var client = new DnsClient(new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53));
var response = client.Query("www.google.com", RecordType.A);
foreach (var record in response.Answers)
    Console.WriteLine($"IP: {record}");
```

**就这么简单！** 🎉 你已经实现了一个支持 UDP/TCP 双协议的高性能 DNS 系统。

---

## 核心特性 🎯

| 特性 | 说明 | 优势 |
|------|------|------|
| 🚀 **IOCP 高性能** | 基于 SAEA.Sockets 完成端口技术 | 支持万级并发 DNS 查询 |
| 🖥️ **DNS Server** | 完整的 DNS 服务器实现 | 自建 DNS 代理/服务器 |
| 🔍 **DNS Client** | 正向查询与反向查询 | 快速解析域名/IP |
| 📡 **双协议支持** | UDP/TCP 自动切换 | UDP 优先，截断自动降级 TCP |
| 📋 **多记录类型** | A/AAAA/NS/CNAME/PTR/MX/TXT/SOA/SRV | 满足各类 DNS 需求 |
| ⚙️ **本地记录配置** | DnsRecords 自定义映射 | 内网域名解析、开发调试 |
| 🔄 **事件驱动** | 请求/响应/错误事件回调 | 灵活的事件处理机制 |
| 🌐 **IPv6 支持** | 完整 IPv6 记录支持 | 适应未来网络环境 |

---

## 架构设计 📐

### 组件架构图

```
┌─────────────────────────────────────────────────────────────┐
│                     SAEA.DNS 架构                            │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────┐              ┌──────────────┐            │
│  │  DnsServer   │              │  DnsClient   │            │
│  │  (DNS服务器) │              │  (DNS客户端) │            │
│  └──────┬───────┘              └──────┬───────┘            │
│         │                             │                      │
│         └──────────────┬──────────────┘                      │
│                        │                                     │
│              ┌─────────▼─────────┐                         │
│              │ DnsRequestMessage │                         │
│              │   DnsResponse     │                         │
│              └─────────┬─────────┘                         │
│                        │                                     │
│         ┌──────────────┼──────────────┐                    │
│         │              │              │                     │
│    ┌────▼────┐   ┌─────▼─────┐   ┌───▼────┐              │
│    │UdpCoder │   │ TcpCoder  │   │ DnsRecords│              │
│    │(UDP协议)│   │ (TCP协议) │   │ (记录管理)│              │
│    └────┬────┘   └─────┬─────┘   └───┬────┘              │
│         │              │              │                     │
│         └──────────────┼──────────────┘                     │
│                        │                                     │
│              ┌─────────▼─────────┐                         │
│              │   SAEA.Sockets    │                         │
│              │   (IOCP 底层)     │                         │
│              └───────────────────┘                         │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### DNS 查询流程图

```
DNS 服务器工作流程:

客户端 ──► UDP/TCP 请求 ──► DnsServer.OnRequested
                                    │
                                    ▼
                         ┌─────────────────────┐
                         │  检查本地记录       │
                         │  DnsRecords.Contains│
                         └─────────┬───────────┘
                                   │
                    ┌──────────────┼──────────────┐
                    │              │              │
              本地有记录      本地无记录        错误
                    │              │              │
                    ▼              ▼              ▼
            返回本地记录    转发上游DNS    返回错误响应
                    │              │
                    │              ▼
                    │      UpstreamClient.Query
                    │              │
                    └──────────────┤
                                   │
                                   ▼
                         DnsServer.OnResponded
                                   │
                                   ▼
                            返回响应给客户端

DNS 客户端查询流程:

应用程序 ──► DnsClient.Query ──► 创建 DnsRequestMessage
                                        │
                                        ▼
                              ┌─────────────────────┐
                              │ UdpRequestCoder     │
                              │ 发送 UDP 请求       │
                              └─────────┬───────────┘
                                        │
                              ┌─────────▼───────────┐
                              │ 检查 TC 标志        │
                              │ (响应是否截断)      │
                              └─────────┬───────────┘
                                        │
                         ┌──────────────┼──────────────┐
                         │              │              │
                      未截断         已截断         超时
                         │              │              │
                         ▼              ▼              ▼
                    返回结果    TcpRequestCoder    重试查询
                                     │
                                     ▼
                              发送 TCP 请求
                                     │
                                     ▼
                               返回完整结果
```

---

## 应用场景 💡

### ✅ 适合使用 SAEA.DNS 的场景

| 场景 | 描述 | 推荐理由 |
|------|------|----------|
| 🏢 **企业 DNS 代理** | 内网 DNS 缓存代理服务器 | IOCP 高性能，支持万级并发 |
| 🛡️ **广告过滤 DNS** | 自定义解析规则过滤广告 | 本地记录配置，灵活控制 |
| 🔧 **开发环境 DNS** | 本地开发域名解析 | 自定义域名映射，无需 hosts |
| 📊 **DNS 查询工具** | 批量域名查询工具 | 支持多种记录类型 |
| 🌐 **智能 DNS** | 根据来源 IP 返回不同解析 | 事件驱动，灵活扩展 |
| 📱 **内网 DNS 服务** | 私有域名系统 | 轻量级，部署简单 |
| 🔍 **网络调试** | DNS 解析问题排查 | 完整请求/响应事件 |

### ❌ 不适合的场景

| 场景 | 推荐替代方案 |
|------|--------------|
| 简单 HTTP 服务 | 使用 SAEA.Http |
| WebSocket 通信 | 使用 SAEA.WebSocket |
| MQTT 物联网 | 使用 SAEA.MQTT |
| RPC 调用 | 使用 SAEA.RPC |

---

## 性能对比 📊

### 与传统 DNS 方案对比

| 指标 | SAEA.DNS | 传统 DNS | 优势 |
|------|----------|----------|------|
| **并发查询** | 10,000+ QPS | ~1,000 QPS | **10倍提升** |
| **内存占用** | 池化复用 | 频繁分配 | **GC 压力降低** |
| **延迟** | ~1ms | ~10ms | **低延迟响应** |
| **协议支持** | UDP+TCP | 单协议 | **自动切换** |

### UDP vs TCP 协议对比

| 协议 | 速度 | 可靠性 | 适用场景 |
|------|------|--------|----------|
| **UDP (SAEA.DNS 默认)** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | 常规 DNS 查询 |
| **TCP (SAEA.DNS 降级)** | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | 大响应、区域传输 |

> 💡 **提示**: SAEA.DNS 默认使用 UDP，当响应被截断时自动降级到 TCP，兼顾速度与可靠性。

### DNS 记录类型支持

| 记录类型 | 代码 | 说明 |
|----------|------|------|
| A | `RecordType.A` | IPv4 地址 |
| AAAA | `RecordType.AAAA` | IPv6 地址 |
| NS | `RecordType.NS` | 名称服务器 |
| CNAME | `RecordType.CNAME` | 别名记录 |
| MX | `RecordType.MX` | 邮件交换 |
| TXT | `RecordType.TXT` | 文本记录 |
| PTR | `RecordType.PTR` | 反向查询 |
| SOA | `RecordType.SOA` | 授权起始 |
| SRV | `RecordType.SRV` | 服务定位 |

---

## 常见问题 ❓

### Q1: 如何创建一个 DNS 代理服务器？

**A**: 通过 `OnRequested` 事件转发到上游 DNS：

```csharp
var upstreamClient = new DnsClient(new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53));

var server = new DnsServer((s, e) => 
{
    // 转发到上游 DNS
    e.Response = upstreamClient.Query(e.Request);
});
server.Start();
```

### Q2: 如何配置自定义 DNS 记录？

**A**: 使用 `DnsRecords` 类添加本地映射：

```csharp
var records = new DnsRecords();
records.Add("www.local.com", "192.168.1.100");      // A 记录
records.AddCNAME("api.local.com", "www.local.com");  // CNAME
records.AddAAAA("ipv6.local.com", "::1");            // AAAA 记录

var server = new DnsServer(records);
server.Start();
```

### Q3: UDP 和 TCP 如何自动切换？

**A**: 当 DNS 响应超过 512 字节时，UDP 会设置截断标志（TC=1），`DnsClient` 会自动使用 TCP 重新查询：

```csharp
// 自动处理：UDP 截断 → TCP 重试
var response = client.Query("large-response.com", RecordType.TXT);
```

### Q4: 如何进行反向 DNS 查询？

**A**: 使用 PTR 记录类型，通过 `GetPTRName` 辅助方法：

```csharp
var ptrName = DnsClient.GetPTRName("8.8.8.8");
var response = client.Query(ptrName, RecordType.PTR);
foreach (var answer in response.Answers)
    Console.WriteLine($"域名: {answer}");
```

### Q5: DNS 服务器支持哪些事件？

**A**: 支持三个核心事件：

```csharp
server.OnRequested += (s, e) => { /* 收到请求 */ };
server.OnResponded += (s, e) => { /* 发送响应 */ };
server.OnErrored  += (s, e) => { /* 发生错误 */ };
```

### Q6: 如何指定上游 DNS 服务器？

**A**: 创建 `DnsClient` 时指定：

```csharp
// Google DNS
var client = new DnsClient(new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53));

// Cloudflare DNS
var client = new DnsClient(new IPEndPoint(IPAddress.Parse("1.1.1.1"), 53));

// 自定义 DNS
var client = new DnsClient(new IPEndPoint(IPAddress.Parse("192.168.1.1"), 53));
```

### Q7: 如何处理 DNS 查询超时？

**A**: `DnsClient` 内部已处理超时和重试，无需额外配置。如需自定义超时时间，可使用编码器：

```csharp
var udpCoder = new UdpRequestCoder(endpoint, timeout: 5000);
var tcpCoder = new TcpRequestCoder(endpoint, timeout: 10000);
```

---

## 核心类 🔧

| 类名 | 说明 |
|------|------|
| `DnsServer` | DNS 服务器，支持自定义记录和代理转发 |
| `DnsClient` | DNS 客户端，支持正向/反向查询 |
| `DnsRequestMessage` | DNS 请求消息封装 |
| `DnsResponseMessage` | DNS 响应消息封装 |
| `DnsRecords` | 本地 DNS 记录管理类 |
| `UdpRequestCoder` | UDP 协议编码器 |
| `TcpRequestCoder` | TCP 协议编码器 |
| `RecordType` | DNS 记录类型枚举 |
| `ResponseCode` | DNS 响应码枚举 |
| `Question` | DNS 查询问题类 |
| `Domain` | 域名封装类 |

---

## 使用示例 📝

### DNS 服务器（完整示例）

```csharp
using SAEA.DNS;
using SAEA.DNS.Model;

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
    Console.WriteLine($"DNS 响应已发送，Answer 数量: {response.Answers.Count}");
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

// 添加 AAAA 记录
records.AddAAAA("ipv6.example.com", "2001:db8::1");

// 添加 CNAME 记录
records.AddCNAME("alias.example.com", "www.example.com");

// 添加 MX 记录
records.AddMX("mail.example.com", "mail.example.com", 10);

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

// 查询 TXT 记录
var txtResponse = client.Query("google.com", RecordType.TXT);

// 查询 NS 记录
var nsResponse = client.Query("google.com", RecordType.NS);
```

### 反向 DNS 查询

```csharp
using SAEA.DNS;
using System.Net;

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

// 上游 DNS 服务器
var upstreamDNS = new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53);
var upstreamClient = new DnsClient(upstreamDNS);

// 本地自定义记录
var localRecords = new DnsRecords();
localRecords.Add("internal.local", "10.0.0.1");
localRecords.Add("api.internal.local", "10.0.0.2");

// 创建代理 DNS 服务器
var server = new DnsServer((sender, e) => 
{
    var domain = e.Request.Questions[0].Domain.ToString();
    
    // 先检查本地记录
    if (localRecords.Contains(domain))
    {
        e.Response = localRecords.GetResponse(e.Request);
        Console.WriteLine($"[本地] {domain}");
        return;
    }
    
    // 转发到上游 DNS
    e.Response = upstreamClient.Query(e.Request);
    Console.WriteLine($"[转发] {domain}");
});

server.Start();
Console.WriteLine("DNS 代理服务器已启动");
```

### 广告过滤 DNS

```csharp
using SAEA.DNS;
using SAEA.DNS.Model;
using System.Net;

// 广告域名列表
var adDomains = new HashSet<string>
{
    "ads.example.com",
    "tracker.example.com",
    "analytics.example.com"
};

var upstreamClient = new DnsClient(new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53));

var server = new DnsServer((sender, e) => 
{
    var domain = e.Request.Questions[0].Domain.ToString();
    
    // 检查是否为广告域名
    if (adDomains.Contains(domain))
    {
        // 返回空响应（阻止广告）
        e.Response = new DnsResponseMessage(e.Request);
        Console.WriteLine($"[阻止] {domain}");
        return;
    }
    
    // 正常转发
    e.Response = upstreamClient.Query(e.Request);
});

server.Start();
```

### UDP/TCP 协议切换

```csharp
using SAEA.DNS.Coder;
using SAEA.DNS.Protocol;
using System.Net;

var dnsEndpoint = new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53);

// UDP 编码器（自动降级到 TCP）
var coder = new UdpRequestCoder(dnsEndpoint, new TcpRequestCoder(dnsEndpoint));

// 创建请求
var request = new DnsRequestMessage();
request.Questions.Add(new Question(
    new Domain("www.google.com"), 
    RecordType.A, 
    RecordClass.IN));

// 发送请求
var response = coder.Code(request);

// 处理响应
foreach (var answer in response.Answers)
{
    Console.WriteLine(answer);
}
```

---

## DNS 消息结构

```
DNS Message:
├── Header (12 bytes)
│   ├── ID (16 bits)           - 请求标识
│   ├── Flags (16 bits)
│   │   ├── QR (1 bit)         - 查询/响应标志
│   │   ├── Opcode (4 bits)    - 操作码
│   │   ├── AA (1 bit)         - 授权应答标志
│   │   ├── TC (1 bit)         - 截断标志
│   │   ├── RD (1 bit)         - 递归请求标志
│   │   ├── RA (1 bit)         - 递归可用标志
│   │   └── Rcode (4 bits)     - 响应码
│   ├── QDCOUNT (16 bits)      - 问题数
│   ├── ANCOUNT (16 bits)      - 回答数
│   ├── NSCOUNT (16 bits)      - 授权记录数
│   └── ARCOUNT (16 bits)      - 附加记录数
├── Questions
│   └── QNAME + QTYPE + QCLASS
├── Answers
│   └── 资源记录
├── Authority Records
│   └── 授权服务器记录
└── Additional Records
    └── 附加资源记录
```

---

## 依赖项

| 包名 | 版本 | 说明 |
|------|------|------|
| SAEA.Sockets | 7.26.2.2 | IOCP 通信框架 |
| SAEA.Common | 7.26.2.2 | 公共工具类 |

---

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包](https://www.nuget.org/packages/SAEA.DNS)
- [作者博客](https://www.cnblogs.com/yswenli/)

---

## 许可证

Apache License 2.0