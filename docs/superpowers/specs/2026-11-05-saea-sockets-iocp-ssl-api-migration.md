# SAEA.Sockets IOCP + SSL 重构 API 迁移说明

> 对应设计：`docs/superpowers/specs/2026-11-05-saea-sockets-iocp-ssl-design.md`
> 适用版本：`SAEA.Sockets 26.11.5.1` 及以上
> 目标框架：`netstandard2.0`（不提供多目标）

---

## 1. 迁移概览

本次重构把 TCP 层统一收敛到 IOCP：

- 删除 `StreamClientSocket`、`StreamServerSocket`；
- `IocpClientSocket` / `IocpServerSocket` 重命名为 `SaeaClientSocket` / `SaeaServerSocket`；
- TCP 的“流模式”不再是独立引擎，而是 IOCP 连接上的一个双工 `Stream` 门面；
- TLS 不再使用 `System.Net.Security.SslStream`，改由 BouncyCastle 实现协议层；
- 所有 TLS 配置集中到 `ISocketOption.Tls`（类型 `SaeaTlsOptions`）。

因此迁移通常只有三类动作：

1. 把旧类型名替换为 `Saea*`；
2. 把旧的 `WithSsl` / `X509Certificate2` / `SslProtocol` 分散配置改成 `WithSsl(o => ...)`；
3. 若使用流模式，从 `ChannelInfo.Stream` / `IServerConnectionProvider.GetStream` 取流，而不是依赖具体 Socket 类型。

---

## 2. 破坏性变更清单

| 变更 | 旧 | 新 | 处理方式 |
| --- | --- | --- | --- |
| 类型删除 | `StreamClientSocket` | — | 改用 `SaeaClientSocket` + `UseStream()` |
| 类型删除 | `StreamServerSocket` | — | 改用 `SaeaServerSocket` + `UseStream()` |
| 类型重命名 | `IocpClientSocket` | `SaeaClientSocket` | 全局替换类型名 |
| 类型重命名 | `IocpServerSocket` | `SaeaServerSocket` | 全局替换类型名 |
| 工厂返回类型 | 按 `UseStream` 分支返回不同实现 | TCP 始终返回 `Saea*` | 不要再对工厂结果做 `Stream*` 类型判断 |
| TLS 实现 | `SslStream` | BouncyCastle TLS | 不再能直接拿到 `SslStream`；改用 `ChannelInfo.Tls` / `GetTlsContext` |
| 服务端接受回调 | `OnAccepted` 总是 `IUserToken` | 流模式为 `ChannelInfo`，事件模式为 `IUserToken` | 按 `UseStream()` 分支处理 |
| 连接对象获取 | `GetCurrentObj` 语义随模式变化 | 流模式返回 `ChannelInfo`，事件模式返回 `IUserToken` | 统一用 `as` 判断 |
| `ChannelManager` | 全局静态单例 | 每个服务器实例持有实例；`Instance` 标记 `[Obsolete]` | 不再跨服务器共享通道 |
| 客户端连接 | `ConnectAsync` 回调即连接成功 | 启用 TLS 时握手成功后才回调 | 依赖回调时机做握手的代码可简化 |
| 事件模式 TLS | 无 | 解密后按 `On*ReceiveSpan` 交付 | 无需改回调签名 |

> `ChannelManager.Instance` / `GetInstance` 仍保留，仅用于源码兼容，新代码不要使用。

---

## 3. 类型映射

| 场景 | 迁移前 | 迁移后 |
| --- | --- | --- |
| TCP 服务端 | `new StreamServerSocket(option)` / `new IocpServerSocket(option)` | `new SaeaServerSocket(option)` |
| TCP 客户端 | `new StreamClientSocket(option)` / `new IocpClientSocket(option)` | `new SaeaClientSocket(option)` |
| 工厂 | `SocketFactory.CreateServerSocket(option)` | 返回 `IServerSocket`，TCP 运行时类型为 `SaeaServerSocket` |
| 工厂 | `SocketFactory.CreateClientSocket(option)` | 返回 `IClientSocket`，TCP 运行时类型为 `SaeaClientSocket` |
| 流对象 | `StreamServerSocket` 的具体 API | `ChannelInfo.Stream` 或 `IServerConnectionProvider.GetStream(userToken)` |
| TLS 状态 | `SslStream` 属性/自建包装 | `ChannelInfo.Tls` / `ISaeaTlsContext` |

---

## 4. 服务端迁移

### 4.1 流模式

迁移前（示意）：

```csharp
var option = SocketOptionBuilder.Instance
    .UseStream()
    .SetIP("0.0.0.0")
    .SetPort(8443)
    .SetSslProtocol(SslProtocols.Tls12)
    .SetX509Certificate2(certificate)
    .Build();

var server = new StreamServerSocket(option);
server.OnAccepted += token =>
{
    var userToken = token as IUserToken;
    var stream = ((StreamUserToken)userToken).Stream;
};
```

迁移后：

```csharp
var option = SocketOptionBuilder.Instance
    .UseStream()
    .SetIP("0.0.0.0")
    .SetPort(8443)
    .WithSsl(o =>
    {
        o.ServerCertificate = certificate;
        o.Protocols = SslProtocols.Tls12 | SaeaTlsProtocols.Tls13;
        // o.ServerCertificateSelector = sni => ...;   // SNI
        // o.ClientCertificateRequired = true;        // mTLS
    })
    .Build();

var server = new SaeaServerSocket(option);
server.OnAccepted += obj =>
{
    var channel = obj as ChannelInfo;
    if (channel == null) return;

    var stream = channel.Stream;        // 明文读写
    var tls = channel.Tls;              // 握手信息：协议、套件、证书、SNI
    var userToken = channel.UserToken;  // 原 IUserToken
};
```

要点：

- 启用 TLS 时，`OnAccepted` 只在握手成功后触发；握手失败走 `OnError` 并断开连接。
- 流模式必须自行消费 `channel.Stream`；事件模式的 `On*ReceiveSpan` 与流消费互斥。
- 服务器空闲回收依赖通道活跃时间，业务侧在自定义路径上应通过 `server.GetCurrentObj(id)` 或发送操作刷新通道。

### 4.2 事件模式

迁移前：

```csharp
var server = new IocpServerSocket(option);
server.OnServerReceive += (token, data) => { ... };
```

迁移后：

```csharp
var server = new SaeaServerSocket(option);
server.OnServerReceiveSpan += (token, span) => { ... };   // 推荐，零拷贝
// 或保留 OnServerReceive（数组形态，兼容）
```

启用 TLS 时回调收到的是**解密后的明文**，业务代码不需要感知 TLS。

### 4.3 按连接取流（可选接口）

`SaeaServerSocket` 实现 `IServerConnectionProvider`：

```csharp
var provider = server as IServerConnectionProvider;
Stream stream = provider?.GetStream(userToken);
ISaeaTlsContext tls = provider?.GetTlsContext(userToken);
```

---

## 5. 客户端迁移

### 5.1 流模式

```csharp
var option = SocketOptionBuilder.Instance
    .UseStream()
    .SetIP("127.0.0.1")
    .SetPort(8443)
    .WithSsl(o =>
    {
        o.TargetHost = "api.example.com";       // 同时用于 SNI 与主机名校验
        o.AllowInvalidCertificates = false;
        o.ClientCertificates = new X509Certificate2Collection(clientCertificate); // mTLS
    })
    .Build();

var client = new SaeaClientSocket(option);

using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
{
    await client.ConnectAsync(cts.Token);
}

var stream = client.GetStream();
await stream.WriteAsync(payload, 0, payload.Length);
var read = await stream.ReadAsync(buffer, 0, buffer.Length);
```

要点：

- `ConnectAsync(CancellationToken)` 在启用 TLS 时，**握手成功后才完成**；失败会抛出异常。
- 未启用 TLS 时行为与普通连接一致。
- `ConnectAsync(Action<SocketError>)` 同样在 TLS 握手成功后才回调。
- `IsAuthenticated` 可直接判断当前连接是否已完成 TLS 认证。

### 5.2 事件模式

```csharp
var client = new SaeaClientSocket(option);
client.OnClientReceiveSpan += (token, span) => { ... };
await client.ConnectAsync();
client.Send(payload);
```

TLS 下收到的是解密后的明文。

---

## 6. TLS 配置 API

### 6.1 `SaeaTlsOptions`

| 成员 | 说明 | 默认值 |
| --- | --- | --- |
| `Enabled` | 是否启用 TLS | `false` |

| `Protocols` | 协议版本 | `TLS 1.2 \| TLS 1.3` |
| `TargetHost` | 客户端目标主机（SNI + 校验） | `null` |
| `ServerCertificate` | 服务端默认证书 | `null` |
| `ServerCertificateSelector` | SNI 选择器，返回 `null` 回退默认证书 | `null` |
| `ClientCertificates` | 客户端证书集合（mTLS） | `null` |
| `ClientCertificateRequired` | 服务端是否要求客户端证书 | `false` |
| `AllowInvalidCertificates` | 跳过证书链校验（仅测试） | `false` |
| `CheckCertificateRevocation` | 检查吊销 | `false` |
| `CertificateValidationCallback` | 自定义校验 | `null` |
| `ApplicationProtocols` | ALPN 列表 | `null` |
| `HandshakeTimeout` | 握手超时 | `10s` |
| `HandshakeStarted` | 握手开始回调 | `null` |
| `HandshakeCompleted` | 握手成功回调 | `null` |
| `HandshakeFailed` | 握手失败回调 | `null` |

握手回调上下文 `SaeaTlsHandshakeContext` 提供：`IsServer`、`RemoteEndPoint`、`TargetHost`、`SniHostName`、`LocalCertificate`、`RemoteCertificate`、`NegotiatedProtocol`、`CipherSuite`、`Exception`。

### 6.2 `SocketOptionBuilder`

| 方法 | 说明 |
| --- | --- |
| `WithSsl(Action<SaeaTlsOptions>)` | 推荐入口，完整配置 |
| `WithSsl(X509Certificate2, ...)` | 兼容重载，指定证书 |
| `WithSsl(SslProtocols, pfxFilePath, pwd)` | 兼容重载，从 PFX 加载 |
| `UseServerCertificateSelector(Func<string, X509Certificate2>)` | SNI 选择器 |
| `UseClientCertificate(X509Certificate2)` | 追加客户端证书 |
| `RequireClientCertificate(bool)` | 要求客户端证书 |
| `SetRemoteCertificateValidation(RemoteCertificateValidationCallback)` | 自定义校验 |
| `SetTlsTargetHost(string)` | 客户端目标主机 |
| `SetHandshakeTimeout(TimeSpan)` | 握手超时 |
| `OnTlsHandshake(started, completed, failed)` | 一次性注册三个回调 |
| `UseStream()` | 暴露 `Stream` 门面（引擎仍为 IOCP） |
| `UseIocp()` | 兼容入口；会把 `UseStream` 置为 `false` |

### 6.3 协议常量

`netstandard2.0` 的 `SslProtocols` 没有 `Tls13`，使用：

```csharp
SaeaTlsProtocols.Tls13   // (SslProtocols)12288
SaeaTlsProtocols.Default // TLS 1.2 | TLS 1.3
```

### 6.4 证书加载

`SaeaCertificateLoader` 支持：

- `FromPfx(path, password)`：PFX/P12；
- `FromPem(certPath, keyPath, password)`：证书 + PEM 私钥（内部转 PKCS#12，带缓存）；
- `FromStore(subjectOrThumbprint, location, validOnly)`：证书库；
- `FromDirectory(directory, password)`：按 SNI 主机名自动匹配 `host.pfx`、`*.suffix.pfx`、`default.pfx`。

默认加载标志为 `EphemeralKeySet | Exportable`，避免私钥落盘并保持可导出。

---

## 7. 行为变化与注意事项

1. **`UseStream()` 不是切换引擎**
   它只表示“暴露双工 `Stream` 门面并由它独占入站数据”。底层永远是 IOCP。

2. **流消费与 Span 事件互斥**
   使用 `GetStream()` / `ChannelInfo.Stream` 时不要同时订阅 `On*ReceiveSpan`，否则同一份明文会被两处争抢。

3. **TLS 在两种模式下的数据形态一致**
   流模式：解密后写入 `Pipe`；
   事件模式：解密后按 span 触发 `On*ReceiveSpan`。

4. **握手回调时机**
   服务端 `OnAccepted`、客户端 `ConnectAsync` 完成都在握手成功之后；失败分别走 `OnError` / 异常。

5. **超时**
   `HandshakeTimeout` 控制握手；`ActionTimeout` 控制 TLS 锁等待与发送等待。业务侧阻塞式 `Send` 可能受 `ActionTimeout` 影响。

6. **通道实例化**
   `ChannelManager` 改为每个服务器实例各自持有；跨服务器共享通道的旧用法需要改成各自查询。

7. **释放**
   `SaeaClientSocket` / `SaeaServerSocket` 的 `Dispose` 会关闭连接、取消握手、释放 Pipe 与 TLS 资源。

---

## 8. 新增公共 API 一览

- 命名空间 `SAEA.Sockets.Ssl`
  - `SaeaTlsOptions`
  - `SaeaTlsProtocols`
  - `SaeaTlsHandshakeContext`
  - `ISaeaTlsContext`
  - `SaeaTlsException`
  - `SaeaCertificateLoader`
- 命名空间 `SAEA.Sockets`
  - `IServerConnectionProvider`
- 命名空间 `SAEA.Sockets.Model`
  - `ChannelInfo.Stream` / `ChannelInfo.Tls` / `ChannelInfo.UserToken`
  - `StreamUserToken.Tls` / `StreamUserToken.Connection`
- 命名空间 `SAEA.Sockets.Core`
  - `SaeaStream`

---

## 9. 验证

构建：

```powershell
dotnet build Src/SAEA.Sockets.sln -c Debug
```

TLS 场景（4 组：stream、event、mTLS TLS1.2、mTLS TLS1.3）：

```powershell
dotnet run --project Src/SAEA.P2PTest -c Debug -- --tls
```

互通场景（.NET `SslStream` 双向、SNI、证书拒绝）：

```powershell
dotnet run --project Src/SAEA.P2PTest -c Debug -- --tls
```

大包 / 背压 / 并发：

```powershell
dotnet run --project Src/SAEA.P2PTest -c Debug -- --tls-stress
```

明文 vs TLS 基线：

```powershell
dotnet run --project Src/SAEA.P2PTest -c Debug -- --bench-tls
```

全量回归：

```powershell
dotnet run --project Src/SAEA.P2PTest -c Debug -- --all
```

本轮结果：

| 项目 | 结果 |
| --- | --- |
| `Src/SAEA.Sockets.sln` 构建 | 0 错误 |
| `--tls` | 23/23 PASS（含 4 组 TLS 场景） |
| `--tls` 互通部分 | 全部 PASS（SslStream 双向、SNI a.local/b.local、未信任证书拒绝） |
| `--tls-stress` | 3/3 PASS（4MB 背压、20 并发、256KB 事件模式） |
| `--bench-tls` | plaintext ≈ 8509 ops/s，TLS ≈ 3836 ops/s（TLS/plain ≈ 0.45x） |
| `--all` | 348/348 PASS，0 failed |

> `openssl s_client` 未在本机安装，SNI 验证改用 .NET `SslStream` 的 `TargetHost` 触发真实 SNI 扩展，并由服务端 `ServerCertificateSelector` 观察与选证。

---

## 10. 已知限制

- 仅支持 RSA / ECDSA 私钥签名；SHA-224 在 TLS 签名中会以 `internal_error` 失败。
- PEM 私钥加载依赖 BouncyCastle 解析后转 PKCS#12，首次加载有额外开销（已缓存）。
- `SaeaTlsProtocols.Tls13` 是常量补丁，目标框架升级到内置 `Tls13` 后可直接使用框架值。
- `ChannelManager.Instance` 保留但已过时；不要在新代码中依赖全局单例。
- FTP 控制连接使用原生 `TcpClient`，不在本次迁移范围。

---

## 11. 回滚

本次变更以源码形式落地，未修改包外部契约以外的运行时行为。若需回滚：

1. 恢复 `StreamClientSocket` / `StreamServerSocket` 与旧 `Iocp*` 命名；
2. 移除 `BouncyCastle.Cryptography` 与 `Ssl` 命名空间；
3. 恢复消费者对 `SslStream` 的直接使用。

由于旧流实现与新流门面在 API 上高度相似，回滚主要影响 `Saea*` 类型名与 `WithSsl` 配置入口。