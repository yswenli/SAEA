# SAEA.Socket5 - SOCKS5 代理服务器与客户端 🔌

[![NuGet version](https://img.shields.io/nuget/v/SAEA.Socket5.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.Socket5)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**[English Version](README.en.md)** | **中文版**

> 基于 .NET Standard 2.0 的 SOCKS5 代理实现，采用 SAEA.Sockets 传输层，完整支持 RFC 1928（CONNECT / BIND / UDP ASSOCIATE）与 RFC 1929（用户名/密码认证），可用于正向代理、内网穿透、UDP 转发等场景。

## 快速导航 🧭

| 章节 | 内容 |
|------|------|
| [⚡ 30秒快速开始](#30秒快速开始) | 服务端 + 客户端最快上手 |
| [🎯 核心特性](#核心特性) | 支持的能力一览 |
| [📐 协议流程](#协议流程) | 三条命令的交互时序 |
| [🔧 核心类](#核心类) | 主要类与配置项 |
| [📝 使用示例](#使用示例) | 服务端 / CONNECT / BIND / UDP |
| [❓ 常见问题](#常见问题) | FAQ 快速解答 |

---

## 30秒快速开始 ⚡

### Step 1: 安装 NuGet 包

```bash
dotnet add package SAEA.Socket5
```

### Step 2: 启动代理服务端

```csharp
using SAEA.Socket5.Model;
using SAEA.Socket5.Server;

var server = new Socks5Server(new Socks5ServerOptions
{
    IP = "0.0.0.0",
    Port = 1080,
    AllowedMethods = new[] { Socks5AuthMethod.NoAuth }
});

server.Start();
Console.WriteLine("SOCKS5 代理已启动，端口: 1080");
```

### Step 3: 通过代理访问目标

```csharp
using SAEA.Socket5.Client;
using SAEA.Socket5.Model;

var client = new Socks5Client(new Socks5ClientOptions
{
    ProxyHost = "127.0.0.1",
    ProxyPort = 1080
});

using var tunnel = client.Connect("www.example.com", 80);
// tunnel 即为通往目标主机的透明流
```

> 配置与枚举类型位于 `SAEA.Socket5.Model`，服务端位于 `SAEA.Socket5.Server`，客户端位于 `SAEA.Socket5.Client`。

---

## 核心特性 🎯

| 特性 | 说明 |
|------|------|
| 🔗 **CONNECT** | 建立到目标主机的 TCP 隧道（正向代理） |
| 📡 **BIND** | 代理侧监听端口，等待远端连入（两阶段握手） |
| 📦 **UDP ASSOCIATE** | 经代理收发 UDP 数据报（RFC 1928 UDP 请求头） |
| 🔒 **用户名/密码鉴权** | RFC 1929，支持字典或自定义委托校验器 |
| 🌐 **多地址类型** | IPv4（`0x01`）、域名（`0x03`）、IPv6（`0x04`） |
| ⚡ **基于 SAEA.Sockets** | 服务端 `IServerSocket` / 客户端 `IClientSocket`，字节级流式握手 |
| 🧩 **半包安全** | 所有读取均为精确长度循环读取，天然兼容 TCP 半包 |

---

## 协议流程 📐

### CONNECT

```
客户端                    代理                     目标
  │  1. 方法协商(05 NMETHODS…)  │                       │
  │ ─────────────────────────► │                       │
  │  2. 方法选择(05 METHOD)     │                       │
  │ ◄───────────────────────── │                       │
  │  3. 请求(05 01 00 ATYP ADDR PORT)                    │
  │ ─────────────────────────► │  4. TCP 连接          │
  │                            │ ────────────────────► │
  │  5. 响应(05 00 00 ATYP BND.ADDR BND.PORT)            │
  │ ◄───────────────────────── │                       │
  │  6. 双向透明转发             │ ◄───────────────────► │
```

### BIND（两阶段）

先返回代理分配好的监听地址/端口（第一阶段），待远端连入后再返回对端地址（第二阶段），之后即为隧道。

### UDP ASSOCIATE

先经 TCP 控制连接完成 UDP 关联并取得代理中继端点，随后：

- 客户端 → 代理：`RSV(2) FRAG(1) ATYP ADDR PORT DATA`
- 代理 → 客户端：同样的封装格式

> 关联的生命周期与 TCP 控制连接一致；关闭控制连接即结束关联。

---

## 核心类 🔧

| 类 / 接口 | 命名空间 | 说明 |
|-----------|----------|------|
| `Socks5Server` | `SAEA.Socket5.Server` | SOCKS5 代理服务端（`Start` / `Stop` / `Dispose`） |
| `Socks5Client` | `SAEA.Socket5.Client` | SOCKS5 客户端（`Connect` / `Bind` / `UdpAssociate` 及异步版本） |
| `Socks5BindResult` | `SAEA.Socket5.Client` | BIND 结果（`FirstReply`、`WaitForBindComplete`、`ControlStream`） |
| `Socks5UdpClient` | `SAEA.Socket5.Client` | UDP 关联客户端（`SendTo` / `TryReceive`） |
| `Socks5ServerOptions` | `SAEA.Socket5.Model` | 服务端配置 |
| `Socks5ClientOptions` | `SAEA.Socket5.Model` | 客户端配置 |
| `ISocks5UserValidator` | `SAEA.Socket5.Model` | 用户名/密码校验接口 |
| `DefaultUserValidator` | `SAEA.Socket5.Model` | 默认校验器（字典 / 自定义委托） |
| `Socks5Codec` | `SAEA.Socket5.Protocol` | RFC 1928/1929 字节级编解码 |

### 服务端配置 `Socks5ServerOptions`

| 属性 | 默认值 | 说明 |
|------|--------|------|
| `IP` | `""` | 监听 IP，留空表示监听任意地址 |
| `Port` | `1080` | 监听端口 |
| `AllowedMethods` | `{ NoAuth }` | 允许的认证方式（`NoAuth` / `UserPass`） |
| `UserValidator` | `new DefaultUserValidator()` | 用户名/密码校验器（默认不含任何用户，会拒绝全部凭据） |
| `BufferSize` | `65536` | 读写缓冲区大小（字节） |
| `ActionTimeout` | `180000` | 单连接动作超时（毫秒） |

### 客户端配置 `Socks5ClientOptions`

| 属性 | 默认值 | 说明 |
|------|--------|------|
| `ProxyHost` | `null` | 代理服务器地址（IP 或域名） |
| `ProxyPort` | `1080` | 代理服务器端口 |
| `BindIP` | `""` | 本地绑定 IP，留空表示由系统分配 |
| `UserName` | `null` | 用户名，留空表示使用无认证 |
| `Password` | `null` | 密码 |
| `Timeout` | `30000` | 连接/动作超时（毫秒） |

---

## 使用示例 📝

### 服务端：启用用户名/密码鉴权

启用 `UserPass` 时**必须提供含用户的校验器**，否则全部凭据都会被拒绝：

```csharp
using System.Collections.Generic;
using SAEA.Socket5.Model;
using SAEA.Socket5.Server;

var server = new Socks5Server(new Socks5ServerOptions
{
    IP = "0.0.0.0",
    Port = 1080,
    AllowedMethods = new[] { Socks5AuthMethod.UserPass },
    UserValidator = new DefaultUserValidator(new Dictionary<string, string>
    {
        ["user"] = "pass"
    })
});

server.Start();
```

也可使用自定义委托：

```csharp
UserValidator = new DefaultUserValidator((user, pwd) => user == "user" && pwd == "pass")
```

### 客户端：CONNECT

```csharp
using SAEA.Socket5.Client;
using SAEA.Socket5.Model;

var client = new Socks5Client(new Socks5ClientOptions
{
    ProxyHost = "127.0.0.1",
    ProxyPort = 1080,
    UserName = "user",   // 代理无需鉴权时可省略
    Password = "pass"
});

using var tunnel = await client.ConnectAsync("www.example.com", 80);
```

> 一个 `Socks5Client` 实例对应一条到代理的 TCP 连接，**仅支持发起一次命令**（SOCKS5 协议限制：一条连接只处理一个请求）。需要访问多个目标时，请为每个命令创建独立实例。

### 客户端：BIND

```csharp
using SAEA.Socket5.Client;
using SAEA.Socket5.Model;

var client = new Socks5Client(new Socks5ClientOptions { ProxyHost = "127.0.0.1", ProxyPort = 1080 });

var bind = client.Bind("0.0.0.0", 0);

// 第一阶段：代理已分配监听地址/端口（bind.FirstReply.Host / bind.FirstReply.Port），需告知远端使其连入
// 第二阶段：远端连入后返回，ControlStream 即为与对端的隧道
var peer = bind.WaitForBindComplete();

using var tunnel = bind.ControlStream;
```

> 服务端会自行分配监听地址与端口用于等待远端连入，客户端请求中携带的地址/端口仅作提示（`0.0.0.0:0` 表示不限）。

### 客户端：UDP ASSOCIATE

```csharp
using SAEA.Socket5.Client;
using SAEA.Socket5.Model;

var client = new Socks5Client(new Socks5ClientOptions { ProxyHost = "127.0.0.1", ProxyPort = 1080 });

using var udp = client.UdpAssociate();

udp.SendTo("8.8.8.8", 53, dnsQuery);

if (udp.TryReceive(out var host, out var port, out var data, timeoutMs: 5000))
{
    // data 为去除 SOCKS5 UDP 头后的负载，host/port 为数据来源
}
```

---

## 常见问题 ❓

### Q1: 支持哪些 SOCKS5 命令？

完整支持 RFC 1928 的 CONNECT、BIND、UDP ASSOCIATE 三条命令，以及 IPv4 / 域名 / IPv6 三种地址类型。

### Q2: 启用 UserPass 后所有连接都被拒绝？

默认的 `DefaultUserValidator` 不含任何用户，会拒绝全部凭据。请在 `UserValidator` 中提供含用户的实例或自定义校验委托。

### Q3: 一个 `Socks5Client` 能同时访问多个目标吗？

不能。SOCKS5 协议规定一条 TCP 连接只处理一个请求，因此一个 `Socks5Client` 仅支持一次命令；多次调用会抛出 `InvalidOperationException`。

### Q4: BIND / UDP 中继上报的地址不可达？

代理上报的是**控制连接的本地终结点地址**（已规整为 IPv4），保证客户端可沿同一可达地址回访中继端口。请确保客户端与代理之间该地址是可达的。

### Q5: UDP 关联结束的条件是什么？

UDP 关联的生命周期等于承载它的 TCP 控制连接——关闭 `Socks5Client`（或释放 `Socks5UdpClient`，后者会一并释放其所属客户端）即结束关联。

---

## 测试 🧪

`SAEA.Socket5Test` 是一个自验证控制台集成测试工程（失败以非 0 退出码结束），覆盖协议功能、数据完整性与性能：

```bash
dotnet run --project Src/SAEA.Socket5Test
```

| 用例 | 验证内容 |
|------|----------|
| 无认证 CONNECT 经代理回显 | 方法协商（NoAuth）+ CONNECT + 数据回显 |
| 用户名/密码 认证通过 | RFC 1929 鉴权成功路径 |
| 用户名/密码 认证被拒 | 凭据错误必须被拒绝 |
| BIND 两阶段握手 | BIND 两次响应 + 隧道互通 |
| UDP ASSOCIATE 经代理回显 | UDP 数据报封装 / 解封装 |
| 会话缺失时 Disconnect 不抛异常 | 会话已被回收时断开的健壮性回归 |
| 数据完整性：跨缓冲边界的多尺寸校验 | 1B / 2B / 1KB / 64KB±1 / 128KB / 200KB 逐段字节级校验 |
| 数据传输：8MB 大包回显 | 大包吞吐 + 字节级一致性 |
| 性能：单连接高频往返 1000 次 | 平均往返延迟、每秒往返次数 |
| 性能：64 并发连接吞吐 | 并发会话聚合吞吐 |
| 性能：UDP 1000 数据报吞吐 | UDP 回包率与吞吐 |

本机（回环）参考量级：单连接 8MB 回显约 450 MB/s、64 并发约 370 MB/s、单次往返约 0.3 ms。

> 注：并发用例的耗时断言预算为 30s。若该用例明显变慢，通常意味着中继任务重新落回线程池线程——中继与会话处理使用专用线程（`TaskCreationOptions.LongRunning`）才能支撑大量并发会话。

---

## 依赖项

| 项目 | 说明 |
|------|------|
| SAEA.Sockets | 传输层（IOCP 通信框架） |
| SAEA.Common | 公共工具类 |

---

## 更多资源

- [GitHub 仓库](https://github.com/yswenli/SAEA)
- [NuGet 包](https://www.nuget.org/packages/SAEA.Socket5)
- [RFC 1928 - SOCKS Protocol Version 5](https://www.rfc-editor.org/rfc/rfc1928)
- [RFC 1929 - Username/Password Authentication for SOCKS V5](https://www.rfc-editor.org/rfc/rfc1929)

---

## 许可证

Apache License 2.0
