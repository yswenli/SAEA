# SAEA.Sockets IOCP 统一传输与标准化 SSL/TLS 设计

> 状态：设计待批准（本文档不包含产品代码实现）
>
> 目标版本：`SAEA.Sockets` 26.11.x
>
> 适用范围：`Src/SAEA.Sockets` 及其所有 TCP 消费者（MQTT、WebSocket、Socket5、DNS、Shortcut、P2P、FTP 等）

---

## 1. 背景

当前 `SAEA.Sockets` 的 TCP 层存在两套并行引擎：

| 引擎 | 服务端 | 客户端 | SSL 实现 |
| --- | --- | --- | --- |
| IOCP | `SaeaServerSocket`（原 `IocpServerSocket`） | `SaeaClientSocket`（原 `IocpClientSocket`） | 无 |
| Stream | `StreamServerSocket` | `StreamClientSocket` | 直接 `new SslStream(...)` + `AuthenticateAsClient/ServerAsync` |

这带来四个结构性问题：

1. **两套 TCP 生命周期**：接收循环、发送串行化、超时、断开语义都各写一遍，缺陷修复需要改两处。
2. **SSL 只在 Stream 引擎可用**：`SocketOptionBuilder.WithSsl` 直接对 IOCP 抛 `NotSupportedException`；IOCP 的 `GetStream()` 也直接抛异常。
3. **SSL 能力被 `SslStream` 锁死**：`netstandard2.0` 没有 `SslServerAuthenticationOptions`、`SslClientAuthenticationOptions`、`ServerCertificateSelectionCallback`，服务端无法做 SNI，也无法在握手阶段做细粒度策略。
4. **SSL 代码散落**：`StreamServerSocket`、`StreamClientSocket` 各自直接 `new SslStream`，证书校验、协议版本、客户端证书逻辑互不一致；MQTT 还依赖 `(SslStream)stream` 强转。

本设计把 TCP 引擎统一为 IOCP（即当前的 `SaeaServerSocket` / `SaeaClientSocket`），并在 IOCP 之上提供：

- 统一的**双工 `Stream` 门面**，替代 `StreamClientSocket` / `StreamServerSocket` 的流能力；
- 基于 **BouncyCastle TLS** 的**唯一 SSL/TLS 入口**，覆盖证书加载、SNI、双向证书、握手回调、证书校验；
- 对 `UseStream()` / `UseIocp()` 的语义重定义：不再切换引擎，只切换“是否暴露流门面 / 消费方式”。

> 说明：工作区中 `IocpClientSocket.cs` / `IocpServerSocket.cs` 已被删除并重命名为 `SaeaClientSocket.cs` / `SaeaServerSocket.cs`，`SocketFactory` 已改为创建新类型。本设计在此基础上继续，不重复重命名工作。

---

## 2. 目标与非目标

### 2.1 目标

1. 删除 `StreamClientSocket` / `StreamServerSocket`，TCP 底层只有 IOCP 一套实现。
2. `SocketFactory` 对 TCP 永远返回 `SaeaClientSocket` / `SaeaServerSocket`。
3. `SaeaClientSocket.GetStream()` 与 `SaeaServerSocket` 的每连接 `ChannelInfo.Stream` 返回统一的双工流。
4. SSL/TLS 全部集中在 `SAEA.Sockets.Ssl` 命名空间，`SAEA.Sockets` 内不再出现 `new SslStream` / `AuthenticateAs*`。
5. 服务端支持：
   - 证书自动加载（PFX / 证书库 / SNI 选择器）；
   - SNI 按主机名选择证书；
   - 双向证书（mTLS）与客户端证书校验；
   - 握手开始 / 成功 / 失败回调；
   - 握手超时。
6. 客户端支持：
   - 目标主机名（SNI + 证书名校验）；
   - 客户端证书；
   - 服务端证书链 / 主机名校验回调；
   - 握手开始 / 成功 / 失败回调；
   - 握手完成后再回调 `ConnectAsync`。
7. 保持 `netstandard2.0` 单目标，`LangVersion 8.0`。
8. 现有消费者尽量少改：MQTT、WebSocket、Socket5、DNS、`Shortcut.TCPClient` 仍以 `Stream` 或 `On*Span` 使用。

### 2.2 非目标

- 不改 UDP 引擎。
- 不改 FTP 控制连接（`FtpControlConnection` 使用 .NET 原生 `TcpClient`/`NetworkStream`，不在本设计范围）。
- 不实现 TLS 会话恢复、0-RTT、DTLS。
- 不在本期实现证书热更新（SNI 选择器返回新证书需要重连生效）。
- 不引入 `System.IO.Pipelines` 之外的新 IO 抽象。

---

## 3. 总体架构

### 3.1 分层

```
┌──────────────────────────────────────────────────────────────────┐
│ 消费者：MQTT / WebSocket / Socket5 / DNS / Shortcut / P2P ...      │
│   - 流式：ChannelInfo.Stream / client.GetStream()                 │
│   - 事件式：OnServerReceiveSpan / OnClientReceiveSpan             │
└──────────────────────────────────────────────────────────────────┘
                              │
┌──────────────────────────────────────────────────────────────────┐
│ SaeaStream（双工 Stream 门面）                                     │
│   ReadAsync  ← PipeReader                                          │
│   WriteAsync → SaeaConnection.SendApplicationData                 │
└──────────────────────────────────────────────────────────────────┘
                              │
┌──────────────────────────────────────────────────────────────────┐
│ SaeaConnection（每连接状态机）                                     │
│   - Pipe（入站缓冲 / 背压）                                        │
│   - TLS 引擎（可选，BouncyCastle TlsProtocol）                      │
│   - 发送串行化（复用 IUserToken.WaitWrite / SendAsyncRaw）          │
│   - 握手 Task / 超时 / 关闭语义                                    │
└──────────────────────────────────────────────────────────────────┘
                              │
┌──────────────────────────────────────────────────────────────────┐
│ IOCP 传输：SaeaClientSocket / SaeaServerSocket                     │
│   SocketAsyncEventArgs 接收 / 发送，SessionManager，UserToken       │
└──────────────────────────────────────────────────────────────────┘
```

关键原则：

- **TLS 是传输变换，不是流模式专属**。开启 TLS 后：
  - 流模式：解密后的应用数据写入 `Pipe`，由 `SaeaStream` 读取；
  - 事件模式：解密后的应用数据通过 `OnServerReceiveSpan` / `OnClientReceiveSpan` 抛出；
  - 发送方向：应用数据先由 TLS 引擎加密，再把密文交给 IOCP 发送。
- **IOCP 只认识字节**。TLS 握手、告警、应用数据都由 `SaeaConnection` 把字节喂给 BC 引擎或从 BC 引擎取出。
- **`SocketOption.UseStream` 只表示“暴露流门面并独占入站数据”**，不再表示“换 Stream 引擎”。

### 3.2 数据流

入站（服务端/客户端相同）：

```
Socket.ReceiveAsync
  → ProcessReceived(SocketAsyncEventArgs)
      → 若 TLS：tls.OfferInput(cipher)
                  → DrainTlsOutput() → SendRaw(cipherOut)
                  → ReadInput(plain) → Pipe / On*Span
      → 若明文且流模式：Pipe
      → 若明文且事件模式：On*Span
```

出站：

```
SaeaStream.WriteAsync(plain)
  → connection.SendApplicationData(plain)
      → 若 TLS：tls.WriteApplicationData(plain) → ReadOutput(cipher) → SendRaw(cipher)
      → 若明文：SendRaw(plain)
```

---

## 4. 公共 API 设计

### 4.1 `SaeaTlsOptions`

新增 `Src/SAEA.Sockets/Ssl/SaeaTlsOptions.cs`：

```csharp
namespace SAEA.Sockets.Ssl
{
    /// <summary>统一 TLS 配置。所有 SSL 行为都从这里进入。</summary>
    public sealed class SaeaTlsOptions
    {
        /// <summary>是否启用 TLS。</summary>
        public bool Enabled { get; set; }

        /// <summary>启用的协议版本，默认 TLS 1.2 + TLS 1.3。</summary>
        public SslProtocols Protocols { get; set; } = SslProtocols.Tls12 | SslProtocols.Tls13;

        /// <summary>客户端目标主机名；用于 SNI 和证书主机名校验。</summary>
        public string TargetHost { get; set; }

        /// <summary>服务端默认证书。</summary>
        public X509Certificate2 ServerCertificate { get; set; }

        /// <summary>服务端 SNI 证书选择器：参数为客户端 SNI 主机名，返回 null 时回退到 ServerCertificate。</summary>
        public Func<string, X509Certificate2> ServerCertificateSelector { get; set; }

        /// <summary>客户端证书集合（用于 mTLS）。</summary>
        public X509Certificate2Collection ClientCertificates { get; set; }

        /// <summary>服务端是否要求客户端证书。</summary>
        public bool ClientCertificateRequired { get; set; }

        /// <summary>是否允许无效证书（仅建议测试环境使用）。</summary>
        public bool AllowInvalidCertificates { get; set; }

        /// <summary>是否检查证书吊销状态。</summary>
        public bool CheckCertificateRevocation { get; set; }

        /// <summary>自定义证书校验回调。null 时使用内置校验。</summary>
        public RemoteCertificateValidationCallback CertificateValidationCallback { get; set; }

        /// <summary>ALPN 协议列表（可选）。</summary>
        public IList<string> ApplicationProtocols { get; set; }

        /// <summary>握手超时，默认 10 秒。</summary>
        public TimeSpan HandshakeTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>握手开始回调。</summary>
        public Action<SaeaTlsHandshakeContext> HandshakeStarted { get; set; }

        /// <summary>握手成功回调。</summary>
        public Action<SaeaTlsHandshakeContext> HandshakeCompleted { get; set; }

        /// <summary>握手失败回调。</summary>
        public Action<SaeaTlsHandshakeContext> HandshakeFailed { get; set; }
    }
}
```

### 4.2 握手上下文与连接级 TLS 信息

```csharp
namespace SAEA.Sockets.Ssl
{
    /// <summary>握手回调上下文。</summary>
    public sealed class SaeaTlsHandshakeContext
    {
        public bool IsServer { get; internal set; }
        public string RemoteEndPoint { get; internal set; }
        public string TargetHost { get; internal set; }
        public string SniHostName { get; internal set; }
        public X509Certificate2 LocalCertificate { get; internal set; }
        public X509Certificate2 RemoteCertificate { get; internal set; }
        public string NegotiatedProtocol { get; internal set; }
        public string CipherSuite { get; internal set; }
        public Exception Exception { get; internal set; }
    }

    /// <summary>每连接 TLS 状态，握手成功后可由消费者读取。</summary>
    public interface ISaeaTlsContext
    {
        bool IsAuthenticated { get; }
        bool IsServer { get; }
        string SniHostName { get; }
        string NegotiatedProtocol { get; }
        string CipherSuite { get; }
        X509Certificate2 LocalCertificate { get; }
        X509Certificate2 RemoteCertificate { get; }
        Exception HandshakeException { get; }
    }
}
```

### 4.3 证书加载器

```csharp
namespace SAEA.Sockets.Ssl
{
    public static class SaeaCertificateLoader
    {
        /// <summary>从 PFX/P12 加载。</summary>
        public static X509Certificate2 FromPfx(string path, string password = null,
            X509KeyStorageFlags flags = X509KeyStorageFlags.EphemeralKeySet);

        /// <summary>从当前用户/本地计算机证书库按主题或指纹加载。</summary>
        public static X509Certificate2 FromStore(string subjectOrThumbprint,
            StoreLocation location = StoreLocation.CurrentUser, bool validOnly = true);

        /// <summary>从证书文件 + 私钥文件加载（PEM）。</summary>
        public static X509Certificate2 FromPem(string certificatePath, string privateKeyPath,
            string password = null);

        /// <summary>按目录自动加载：文件名匹配主机名（如 api.example.com.pfx / *.example.com.pfx）。</summary>
        public static Func<string, X509Certificate2> FromDirectory(string directory,
            string password = null, string searchPattern = "*.pfx");
    }
}
```

实现要点：

- PFX：`new X509Certificate2(path, password, X509KeyStorageFlags.EphemeralKeySet)`。
- 证书库：`X509Store` + `FindBySubjectName` / `FindByThumbprint`，只返回带私钥的证书。
- PEM：`netstandard2.0` 没有 `X509Certificate2.CreateFromPemFile`，使用 BouncyCastle `PemReader` 解析证书与私钥：
  - 证书：`X509Certificate2(der)`；
  - RSA 私钥：`PrivateKeyFactory.CreateKey(...)` → `RsaPrivateCrtKeyParameters` → `RSAParameters` → `RSA.ImportParameters` → `CopyWithPrivateKey`；
  - ECDSA 私钥：`ECPrivateKeyParameters` → `ECParameters` → `ECDsa.ImportParameters` → `CopyWithPrivateKey`。
- `FromDirectory` 返回的选择器做**最长后缀匹配**：
  1. 精确匹配 `host.pfx`；
  2. 通配符 `*.example.com.pfx`，取最长后缀；
  3. 默认证书 `default.pfx`；
  4. 都没有返回 `null`，由 `ServerCertificate` 兜底。
- 加载器内部缓存证书，避免每次 SNI 都读盘；缓存项由调用方 `Dispose` 的 `SaeaCertificateStore` 统一释放（可选）。

> 分期建议：PFX + 证书库在 Phase 2 完成；PEM 可作为 Phase 2b，避免阻塞主链路。

### 4.4 `SocketOption` / `ISocketOption` 扩展

`ISocketOption` 增加：

```csharp
/// <summary>是否暴露流门面（不切换引擎，TCP 始终 IOCP）。</summary>
bool UseStream { get; set; }

/// <summary>统一 TLS 配置。WithSsl 为兼容属性，内部映射到 Tls.Enabled。</summary>
SaeaTlsOptions Tls { get; set; }
```

保留兼容成员：

```csharp
bool WithSsl { get; set; }              // => Tls.Enabled
X509Certificate2 X509Certificate2 { get; set; } // => Tls.ServerCertificate
SslProtocols SslProtocol { get; set; }  // => Tls.Protocols
bool UseIocp { get; set; }              // 保留但 TCP 工厂忽略
```

`SocketOption` 默认值：

- `UseStream = false`；
- `UseIocp = true`（新默认；旧默认 false 会导致工厂选择 Stream，已无意义）；
- `Tls = new SaeaTlsOptions()`。

> 兼容说明：外部若只调用 `UseStream()`，行为仍是“流模式”；若只调用 `UseIocp()`，行为仍是“事件模式”。不调用两者时，TCP 默认事件模式（与旧 `UseIocp=true` 一致），不会静默变成流模式。

### 4.5 `SocketOptionBuilder` 扩展

```csharp
// 模式
public SocketOptionBuilder UseStream();   // UseStream = true,  UseIocp = true
public SocketOptionBuilder UseIocp();     // UseStream = false, UseIocp = true

// TLS：兼容旧重载（去掉“IOCP 不支持”的限制）
public SocketOptionBuilder WithSsl(SslProtocols protocols, string pfxFilePath = null, string pwd = "");
public SocketOptionBuilder WithSsl(X509Certificate2 certificate, SslProtocols protocols = SslProtocols.Tls12 | SslProtocols.Tls13);

// TLS：新配置入口
public SocketOptionBuilder WithSsl(Action<SaeaTlsOptions> configure);
public SocketOptionBuilder UseServerCertificateSelector(Func<string, X509Certificate2> selector);
public SocketOptionBuilder UseClientCertificate(X509Certificate2 certificate);
public SocketOptionBuilder RequireClientCertificate(bool required = true);
public SocketOptionBuilder SetRemoteCertificateValidation(RemoteCertificateValidationCallback callback);
public SocketOptionBuilder SetTlsTargetHost(string targetHost);
public SocketOptionBuilder SetHandshakeTimeout(TimeSpan timeout);
public SocketOptionBuilder OnTlsHandshake(
    Action<SaeaTlsHandshakeContext> started = null,
    Action<SaeaTlsHandshakeContext> completed = null,
    Action<SaeaTlsHandshakeContext> failed = null);
```

兼容映射：

- `WithSsl(protocols, pfx, pwd)`：加载 PFX → `Tls.ServerCertificate`，`Tls.Enabled = true`。
- `WithSsl(cert, protocols)`：`Tls.ServerCertificate = cert`，`Tls.Protocols = protocols`，`Tls.Enabled = true`。
- 旧的 `pfxFilePath = "*.pfx"` 默认值移除（改为 `null`）；保留重载签名，避免源码级破坏。

### 4.6 服务端连接访问

新增可选接口，避免破坏 `UdpServerSocket` 和外部 `IServerSocket` 实现：

```csharp
namespace SAEA.Sockets
{
    /// <summary>TCP 服务端每连接流 / TLS 信息访问。</summary>
    public interface IServerConnectionProvider
    {
        Stream GetStream(IUserToken userToken);
        ISaeaTlsContext GetTlsContext(IUserToken userToken);
    }
}
```

`SaeaServerSocket : IServerSocket, IServerConnectionProvider, IDisposable`。

`ChannelInfo` 增加：

```csharp
/// <summary>TLS 连接信息；未启用 TLS 时为 null。</summary>
public ISaeaTlsContext Tls { get; set; }
```

`StreamUserToken` 增加：

```csharp
/// <summary>该连接的 TLS 状态；未启用 TLS 时为 null。</summary>
public ISaeaTlsContext Tls { get; set; }

/// <summary>该连接的内部状态（供框架使用）。</summary>
internal SaeaConnection Connection { get; set; }
```

### 4.7 客户端

`SaeaClientSocket` 增加/调整：

```csharp
/// <summary>获取双工流。TCP 下始终可用；TLS 握手完成前读写会等待握手。</summary>
public Stream GetStream();

/// <summary>当前连接是否已完成 TLS 握手（未启用 TLS 时与 Connected 一致）。</summary>
public bool IsAuthenticated { get; }

/// <summary>连接级 TLS 信息。</summary>
public ISaeaTlsContext TlsContext { get; }

/// <summary>连接并在启用 TLS 时等待握手完成。</summary>
public Task ConnectAsync(CancellationToken cancellationToken = default);
```

保留既有 `void Connect()`、`void ConnectAsync(Action<SocketError>)` 签名；语义调整为：

- 未启用 TLS：与现在一致，TCP 连接成功即回调。
- 启用 TLS：TCP 连接成功后启动握手，**握手成功才回调 `SocketError.Success`**；失败回调 `SocketError.SocketError`，并通过 `OnError` 抛出 `SaeaTlsException`。
- 新增的 `Task ConnectAsync(CancellationToken)` 在握手成功后完成，供 MQTT 等 `async` 消费者直接 `await`。

---

## 5. IOCP 流门面设计

### 5.1 `SaeaStream`

新增 `Src/SAEA.Sockets/Core/SaeaStream.cs`（或 `Core/Tcp/SaeaStream.cs`）：

```csharp
public sealed class SaeaStream : Stream
{
    private readonly SaeaConnection _connection;

    internal SaeaStream(SaeaConnection connection) { _connection = connection; }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => _connection.CanWrite;

    public override int Read(byte[] buffer, int offset, int count)
        => ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _connection.ReadAsync(buffer, offset, count, cancellationToken);

    public override void Write(byte[] buffer, int offset, int count)
        => WriteAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _connection.WriteAsync(buffer, offset, count, cancellationToken);

    public override void Flush() { }
    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    protected override void Dispose(bool disposing) { if (disposing) _connection.Dispose(); base.Dispose(disposing); }
}
```

约束：

- `Read` / `Write` 的同步重载内部阻塞等待异步实现，仅为兼容旧 `Stream` 消费方；框架内部一律走 `ReadAsync` / `WriteAsync`。
- `ReadAsync` 在连接关闭时返回 `0`（EOF），异常通过 `OnError` 或抛出 `IOException` 呈现。
- `WriteAsync` 在 TLS 模式下等待握手完成；握手失败抛 `SaeaTlsException`。
- `Dispose` 默认关闭连接（`ownsConnection = true`）；服务端 `ChannelInfo` 由 `SaeaServerSocket` 持有，`SaeaStream.Dispose` 会触发连接关闭并从会话表移除。

### 5.2 `SaeaConnection`

新增 `Src/SAEA.Sockets/Core/SaeaConnection.cs`（internal）：

```csharp
internal sealed class SaeaConnection : IDisposable
{
    public string Id { get; }
    public Socket Socket { get; }
    public SaeaStream Stream { get; }
    public PipeReader Input { get; }
    public ISaeaTlsContext Tls { get; }

    // 握手完成信号；未启用 TLS 时已完成
    public Task HandshakeTask { get; }

    // 由 IOCP 接收回调调用；data 仅在调用期间有效
    public void OnRawReceived(ReadOnlySpan<byte> data);

    // 由 SaeaStream 调用
    public Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct);
    public Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct);

    // 由 TLS 引擎或握手驱动调用
    internal void SendRaw(byte[] data, int offset, int count);

    public void Dispose();
}
```

内部状态：

| 字段 | 说明 |
| --- | --- |
| `Pipe _pipe` | 入站应用数据缓冲；`PipeOptions` 使用 `useSynchronizationContext: false` |
| `PipeWriter _writer` | 接收回调写入 |
| `PipeReader _reader` | `SaeaStream.ReadAsync` 读取 |
| `SaeaTlsTransport _tls` | 可空；BC 适配器 |
| `TaskCompletionSource<bool> _handshakeTcs` | 握手完成/失败信号 |
| `SemaphoreSlim _tlsLock` | 保护 BC `TlsProtocol`（非线程安全），同时序列化入站/出站 TLS 操作 |
| `int _state` | 0=活动，1=关闭中，2=已关闭 |

### 5.3 入站：明文

`SaeaClientSocket.ProcessReceived` / `SaeaServerSocket.ProcessReceived` 在拿到 `SocketAsyncEventArgs` 后，根据连接状态分派：

```csharp
if (connection != null)                 // 流模式 / TLS
    connection.OnRawReceived(readArgs.Buffer.AsSpan(readArgs.Offset, readArgs.BytesTransferred));
else
    RaiseSpan(userToken, readArgs.Buffer.AsSpan(readArgs.Offset, readArgs.BytesTransferred));
```

`OnRawReceived`（明文）：

```csharp
var mem = _writer.GetSpan(data.Length);
data.CopyTo(mem);
_writer.Advance(data.Length);

var flush = _writer.FlushAsync();
if (!flush.IsCompleted)
{
    // 达到 pauseWriterThreshold：暂停 Socket 接收，避免无界内存增长
    _pauseReceive = true;
    _ = flush.AsTask().ContinueWith(_ => ResumeReceive());
}
```

> `PipeOptions` 建议：
> - `pauseWriterThreshold = Math.Max(ReadBufferSize * 4, 64 * 1024)`
> - `resumeWriterThreshold = Math.Max(ReadBufferSize * 2, 32 * 1024)`
>
> 服务端 `ReadBufferSize` 默认 64KB，即约 256KB 高水位 / 128KB 低水位。

### 5.4 入站：TLS

```csharp
void OnRawReceived(ReadOnlySpan<byte> cipher)
{
    byte[] rented = ArrayPool<byte>.Shared.Rent(cipher.Length);
    try
    {
        cipher.CopyTo(rented);

        PipeWriter writer = _writer;
        Task flushTask = null;

        if (!_tlsLock.Wait(timeout))
            throw new SaeaTlsException("TLS 入站处理超时");

        try
        {
            _tls.OfferInput(rented, 0, cipher.Length);   // BC 内部会复制
            DrainOutput();                                // 握手/告警密文 → 发送

            while (_tls.AvailableInput > 0)
            {
                int n = _tls.ReadInput(rented, 0, rented.Length);
                if (n <= 0) break;
                writer.Write(rented.AsSpan(0, n));
            }

            flushTask = writer.FlushAsync().AsTask();
        }
        finally
        {
            _tlsLock.Release();
        }

        if (!flushTask.IsCompleted)
            PauseReceive(flushTask);
    }
    finally
    {
        ArrayPool<byte>.Shared.Return(rented);
    }
}
```

要点：

- **必须使用非阻塞三元组**：`OfferInput` / `GetAvailableInputBytes` + `ReadInput` / `WriteApplicationData` + `GetAvailableOutputBytes` + `ReadOutput`。不要在非阻塞 `TlsProtocol` 上调用 `ReadApplicationData`，因为它在 `netstandard2.0` 分支会走 `SafeReadRecord`，在空输入时会阻塞或抛错。
- `OfferInput` 内部把数据复制进 `ByteQueueInputStream`，调用方可以立即归还租用数组。
- 握手阶段 `DrainOutput()` 产生的密文需要立即发送；应用数据阶段产生的密文（如 TLS 1.3 KeyUpdate、告警）同样发送。
- `_tlsLock.Wait(timeout)` 使用 `HandshakeTimeout` / `ActionTimeout`，避免死锁静默挂起。

`DrainOutput`：

```csharp
void DrainOutput()
{
    while (_tls.AvailableOutput > 0)
    {
        int n = _tls.ReadOutput(_sendBuffer, 0, _sendBuffer.Length);
        if (n <= 0) break;
        SendRaw(_sendBuffer, 0, n);
    }
}
```

### 5.5 出站

```csharp
async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
{
    // TLS 需等握手完成；明文连接此 Task 已完成
    await HandshakeTask.ConfigureAwait(false);
    ct.ThrowIfCancellationRequested();

    if (_tls == null)
    {
        SendPlain(buffer, offset, count);
        return;
    }

    // 加密必须在锁内完成，且要保证密文顺序
    _tlsLock.Wait();
    try
    {
        _tls.WriteApplicationData(buffer, offset, count);
        DrainOutput();
    }
    finally
    {
        _tlsLock.Release();
    }
}
```

`SendPlain` / `SendRaw` 复用现有 `IUserToken` 发送机制：

- `SendAsyncRaw(userToken, seg, owner)` 已通过 `WaitWrite` 串行化；
- 明文路径可保留零拷贝快速路径（数组支撑的 `ReadOnlyMemory`）；
- TLS 路径无法零拷贝：BC 会把明文复制进记录缓冲，密文再由 `ReadOutput` 复制到发送缓冲；使用 `ArrayPool` 租用发送缓冲，`owner` 在发送完成后归还。

> 性能说明：TLS 模式明确放弃零拷贝，这是协议加密的固有成本。事件模式 + 明文仍走现有 Span 零拷贝路径。
>
> 发送顺序：应用数据与握手/告警密文共享 `_tlsLock`，保证 BC 输出顺序与网络发送顺序一致。握手阶段应用数据被 `HandshakeTask` 挡住，因此不会与握手密文交叉。

### 5.6 关闭与 EOF

- 接收返回 `0` / `SocketError`：
  - 完成 `PipeWriter`，`SaeaStream.ReadAsync` 返回 `0`；
  - TLS 模式先 `CloseInput()`，把 close_notify 交给 `DrainOutput()` 发送（尽力而为）；
  - 触发 `OnDisconnected`，从 `SessionManager` / `ChannelManager` 移除。
- 主动 `Dispose`：
  1. `_state = Closing`；
  2. `PipeReader.CancelPendingRead()` + `Complete()`（唤醒挂起的读）；
  3. `_tls?.Close()`（尽量发送 close_notify）；
  4. `Socket.Shutdown(Both)` + `Close()`；
  5. `_handshakeTcs.TrySetCanceled()`；
  6. 释放租用缓冲、`SemaphoreSlim`。

---

## 6. BouncyCastle TLS 适配

### 6.1 依赖

`SAEA.Sockets.csproj` 增加：

```xml
<PackageReference Include="BouncyCastle.Cryptography" Version="2.6.2" />
```

选择 `2.6.2` 的理由：

- 与仓库 `SAEA.MVCTest` 已引用的 `BouncyCastle.Cryptography.2.6.2` 一致，避免程序集版本分叉；
- `lib/netstandard2.0` 可用；
- 已完成 `TlsClientProtocol` / `TlsServerProtocol` / `DefaultTlsServer` / `BcTlsCrypto` 等 API 调研。

`2.7.0` 可在后续统一升级；本设计不依赖 2.7.0 新 API。

### 6.2 传输适配器

新增：

```
Src/SAEA.Sockets/Ssl/Internal/SaeaTlsTransport.cs        // 抽象基类
Src/SAEA.Sockets/Ssl/Internal/SaeaTlsServerTransport.cs  // TlsServerProtocol
Src/SAEA.Sockets/Ssl/Internal/SaeaTlsClientTransport.cs  // TlsClientProtocol
Src/SAEA.Sockets/Ssl/Internal/SaeaTlsServerPeer.cs       // DefaultTlsServer 子类
Src/SAEA.Sockets/Ssl/Internal/SaeaTlsClientPeer.cs       // DefaultTlsClient 子类
Src/SAEA.Sockets/Ssl/Internal/BcKeyConverter.cs          // X509Certificate2 → BC 私钥
Src/SAEA.Sockets/Ssl/Internal/SaeaCertificateValidator.cs// 链 + 主机名校验
```

`SaeaTlsTransport` 暴露：

```csharp
internal abstract class SaeaTlsTransport : IDisposable
{
    public abstract void OfferInput(byte[] data, int offset, int count);
    public abstract int AvailableInput { get; }
    public abstract int ReadInput(byte[] buffer, int offset, int count);
    public abstract void WriteApplicationData(byte[] buffer, int offset, int count);
    public abstract int AvailableOutput { get; }
    public abstract int ReadOutput(byte[] buffer, int offset, int count);
    public abstract bool IsHandshaking { get; }
    public abstract bool IsClosed { get; }
    public abstract void Close();
}
```

### 6.3 服务端 Peer

```csharp
internal sealed class SaeaTlsServerPeer : DefaultTlsServer
{
    private readonly SaeaTlsOptions _options;
    private X509Certificate2 _selectedCertificate;
    private string _sniHostName;

    public SaeaTlsServerPeer(SaeaTlsOptions options)
        : base(new BcTlsCrypto())
    {
        _options = options;
    }

    public override void ProcessClientExtensions(IDictionary<int, byte[]> clientExtensions)
    {
        base.ProcessClientExtensions(clientExtensions);

        var names = TlsExtensionsUtilities.GetServerNameExtensionClient(clientExtensions);
        if (names != null)
        {
            foreach (var name in names)
            {
                if (name.NameType == NameType.host_name)
                {
                    _sniHostName = Encoding.ASCII.GetString(name.NameData);
                    break;
                }
            }
        }

        _selectedCertificate = _options.ServerCertificateSelector?.Invoke(_sniHostName)
                               ?? _options.ServerCertificate;

        if (_selectedCertificate == null)
            throw new TlsFatalAlert(AlertDescription.unrecognized_name);
    }

    public override TlsCredentials GetCredentials()
    {
        // TLS 1.3 与 legacy 分支都需要按证书类型选择 signer
        if (TlsUtilities.IsTlsV13(m_context))
            return BuildSigner(_selectedCertificate);

        return base.GetCredentials(); // 由 DefaultTlsServer 按 key exchange 选择 RSA/ECDSA
    }

    protected override TlsCredentialedSigner GetRsaSignerCredentials()
        => BuildSigner(_selectedCertificate);

    protected override TlsCredentialedSigner GetECDsaSignerCredentials()
        => BuildSigner(_selectedCertificate);

    public override CertificateRequest GetCertificateRequest()
    {
        if (!_options.ClientCertificateRequired)
            return null;

        var sigAlgs = TlsUtilities.GetDefaultSupportedSignatureAlgorithms(m_context);
        return new CertificateRequest(
            new[] { ClientCertificateType.rsa_sign, ClientCertificateType.ecdsa_sign },
            sigAlgs,
            null);
    }

    public override void NotifyClientCertificate(Certificate clientCertificate)
    {
        // 由 SaeaTlsServerTransport 在握手完成后读取 SecurityParameters.PeerCertificate
        // 此处仅做“必须提供证书”的硬性校验
        if (_options.ClientCertificateRequired &&
            (clientCertificate == null || clientCertificate.IsEmpty))
            throw new TlsFatalAlert(AlertDescription.bad_certificate);
    }

    private TlsCredentialedSigner BuildSigner(X509Certificate2 certificate)
    {
        var crypto = (BcTlsCrypto)m_context.Crypto;
        var chain = BcKeyConverter.ToCertificateChain(crypto, certificate);
        var privateKey = BcKeyConverter.GetPrivateKey(certificate);
        var peerSigAlgs = m_context.SecurityParameters.ClientSigAlgs;
        var sigAlg = TlsUtilities.ChooseSignatureAndHashAlgorithm(
            m_context, peerSigAlgs, BcKeyConverter.GetSignatureAlgorithm(certificate));
        return new BcDefaultTlsCredentialedSigner(
            new TlsCryptoParameters(m_context), crypto, privateKey, chain, sigAlg);
    }
}
```

补充：

- `base.GetCredentials()` 在 TLS 1.3 下需要显式走 `BuildSigner`，否则 `DefaultTlsServer` 可能按 `KeyExchangeAlgorithm` 走到未实现的 RSA 加密分支。
- `GetRsaSignerCredentials` / `GetECDsaSignerCredentials` 都返回 `BuildSigner`，实际算法由证书私钥类型决定。
- 若证书是 RSA，建议在 `GetSupportedCipherSuites()` 中过滤掉 ECDSA 套件；反之亦然。可在 `SaeaTlsServerPeer` 中重写 `GetSupportedCipherSuites()`，或由 `SaeaTlsOptions` 显式指定套件列表（本期不暴露）。
- `CertificateRequest` 的 `certificateAuthorities` 传 `null` 表示不限制 CA；客户端证书校验由 `SaeaCertificateValidator` 在握手后执行（见 6.5）。
- `ProcessClientExtensions` 在 `GetCredentials` 之前调用，SNI 选择结果保存在字段中；同一连接只选一次。

### 6.4 客户端 Peer

```csharp
internal sealed class SaeaTlsClientPeer : DefaultTlsClient
{
    private readonly SaeaTlsOptions _options;
    private readonly SaeaTlsClientTransport _transport;

    public SaeaTlsClientPeer(SaeaTlsOptions options, SaeaTlsClientTransport transport)
        : base(new BcTlsCrypto())
    {
        _options = options;
        _transport = transport;
    }

    public override IList<ServerName> GetSniServerNames()
    {
        if (string.IsNullOrEmpty(_options.TargetHost))
            return null;

        return new List<ServerName>
        {
            new ServerName(NameType.host_name, Encoding.ASCII.GetBytes(_options.TargetHost))
        };
    }

    public override TlsAuthentication GetAuthentication()
        => new SaeaTlsClientAuthentication(_options, _transport);
}
```

`SaeaTlsClientAuthentication`：

```csharp
public void NotifyServerCertificate(TlsServerCertificate serverCertificate)
{
    var cert = SaeaCertificateValidator.ToX509Certificate2(serverCertificate.Certificate);
    var errors = SaeaCertificateValidator.Validate(
        cert, _options.TargetHost, _options.CheckCertificateRevocation);

    if (_options.CertificateValidationCallback != null)
    {
        if (!_options.CertificateValidationCallback(null, cert, errors.Chain, errors.PolicyErrors))
            throw new TlsFatalAlert(AlertDescription.bad_certificate);
        return;
    }

    if (!_options.AllowInvalidCertificates && errors.PolicyErrors != SslPolicyErrors.None)
        throw new TlsFatalAlert(AlertDescription.bad_certificate);

    _transport.SetRemoteCertificate(cert);
}

public TlsCredentials GetClientCredentials(CertificateRequest request)
{
    var cert = _options.ClientCertificates?.Cast<X509Certificate2>()
        .FirstOrDefault(c => c.HasPrivateKey);
    if (cert == null) return null;

    var crypto = (BcTlsCrypto)m_context.Crypto;
    var chain = BcKeyConverter.ToCertificateChain(crypto, cert);
    var privateKey = BcKeyConverter.GetPrivateKey(cert);
    var sigAlg = TlsUtilities.ChooseSignatureAndHashAlgorithm(
        m_context, request.SupportedSignatureAlgorithms,
        BcKeyConverter.GetSignatureAlgorithm(cert));
    return new BcDefaultTlsCredentialedSigner(
        new TlsCryptoParameters(m_context), crypto, privateKey, chain, sigAlg);
}
```

> `GetClientCredentials` 中的 `m_context` 来自 `AbstractTlsClient` 的受保护字段；若实现为独立类，需要把 `TlsContext` 作为构造参数传入。

### 6.5 证书校验

`SaeaCertificateValidator` 负责：

1. 把 BC `Certificate` 链转成 `X509Certificate2Collection`；
2. 用 `X509Chain` 构建链：
   - `ChainPolicy.RevocationMode = CheckCertificateRevocation ? Online : NoCheck`；
   - `ChainPolicy.VerificationFlags = NoFlag`；
3. 主机名校验：
   - 优先 SAN `dNSName`；
   - 通配符只允许最左标签（`*.example.com` 匹配 `api.example.com`，不匹配 `a.b.example.com`）；
   - 无 SAN 时回退 CN；
   - IP 目标使用 `iPAddress` SAN 精确匹配；
4. 汇总为 `SslPolicyErrors`：
   - `RemoteCertificateNotAvailable`；
   - `RemoteCertificateChainErrors`；
   - `RemoteCertificateNameMismatch`；
5. 自定义回调优先；回调返回 `false` → 握手失败。

服务端 mTLS：

- 握手完成后从 `m_context.SecurityParameters.PeerCertificate` 取客户端证书链；
- 若 `ClientCertificateRequired` 且链为空 → 已在 `NotifyClientCertificate` 抛 `bad_certificate`；
- 若配置了 `CertificateValidationCallback`，对客户端证书执行同样的回调；
- 否则默认只做链构建（可选 `AllowInvalidCertificates` 跳过），**不默认强制信任根**，因为内网自签场景常见；生产建议显式配置回调或受信 CA。

### 6.6 协议版本映射

| `SslProtocols` | BC `ProtocolVersion` |
| --- | --- |
| `Ssl3` | 不支持，抛 `NotSupportedException` |
| `Tls` (TLS 1.0) | `TLSv10` |
| `Tls11` | `TLSv11` |
| `Tls12` | `TLSv12` |
| `Tls13` | `TLSv13` |
| `None` / `Default` | `TLSv12` + `TLSv13` |

在 `SaeaTlsServerPeer` / `SaeaTlsClientPeer` 重写：

```csharp
public override ProtocolVersion[] GetProtocolVersions()
    => SaeaTlsProtocolMapper.Map(_options.Protocols);
```

### 6.7 握手回调挂接

`SaeaTlsPeerBase`（`SaeaTlsServerPeer` / `SaeaTlsClientPeer` 的共同基类或组合对象）重写：

```csharp
public override void NotifyHandshakeBeginning()
{
    base.NotifyHandshakeBeginning();
    _transport.RaiseHandshakeStarted();
}

public override void NotifyHandshakeComplete()
{
    base.NotifyHandshakeComplete();
    _transport.RaiseHandshakeCompleted();
}

public override void NotifyAlertRaised(short level, short desc, string message, Exception cause)
{
    base.NotifyAlertRaised(level, desc, message, cause);
    if (level == AlertLevel.fatal)
        _transport.RaiseHandshakeFailed(new SaeaTlsException(
            $"TLS alert {AlertDescription.GetText(desc)}: {message}", cause));
}

public override void NotifyAlertReceived(short level, short desc)
{
    base.NotifyAlertReceived(level, desc);
    if (level == AlertLevel.fatal)
        _transport.RaiseHandshakeFailed(new SaeaTlsException(
            $"TLS alert received: {AlertDescription.GetText(desc)}"));
}
```

注意：

- 回调在持有 `_tlsLock` 的线程上触发；`HandshakeCompleted` 里如果调用 `stream.Write` 会重入 `_tlsLock`。`SemaphoreSlim` 不可重入，因此**回调必须在释放锁之后触发**：
  - Peer 只把状态写入 `SaeaTlsTransport` 的字段 / `TaskCompletionSource`；
  - `SaeaConnection` 在 `OfferInput` 返回、释放 `_tlsLock` 后统一 `RaisePendingCallbacks()`。
- 握手超时由 `SaeaConnection` 的 `Timer` 控制：超时后 `_handshakeTcs.TrySetException(new TimeoutException(...))`，关闭连接，触发 `HandshakeFailed`。

---

## 7. 服务端集成

### 7.1 接受连接流程

`SaeaServerSocket.ProcessAccepted`：

```
1. 配置 Socket（NoDelay / BufferSize / Timeout）
2. userToken = SessionManager.BindUserToken(socket, ...)
3. 若 SocketOption.UseStream 或 Tls.Enabled：
     connection = SaeaConnection.Create(...)
     userToken.Connection = connection
4. 若 Tls.Enabled：
     tls = SaeaTlsTransport.CreateServer(connection, TlsOptions)
     connection.AttachTls(tls)
     tls.BeginHandshake()          // 非阻塞，Accept()
5. 启动接收循环 ProcessReceive(userToken.ReadArgs)
6. 若无 TLS：
     RaiseAccepted(userToken / ChannelInfo)
   若 TLS：
     connection.HandshakeTask.ContinueWith(...)  // 握手成功后再 RaiseAccepted
7. 注册到 SessionManager / ChannelManager
```

`RaiseAccepted` 的 payload：

- `UseStream == true`：`ChannelInfo`
  ```csharp
  new ChannelInfo
  {
      ID = userToken.ID,
      ClientSocket = socket,
      Stream = connection.Stream,
      UserToken = userToken,
      Tls = connection.Tls,
      Expired = ...
  }
  ```
- `UseStream == false`：`IUserToken`（保持现状）；TLS 信息通过 `IServerConnectionProvider.GetTlsContext(userToken)` 获取。

> 为什么 TLS 握手成功后才触发 `OnAccepted`：
> - 消费者拿到连接即可读取客户端证书、协商协议、ALPN；
> - 握手失败不会产生一个“看似可用但实际不可读”的连接；
> - 无 TLS 时行为与现状一致，不引入延迟。
>
> 代价：握手期间不会有 `OnAccepted`。若消费者需要“TCP 已接入”的早期通知，可后续增加 `OnTcpAccepted` 事件；本期不做。

### 7.2 接收分派

```csharp
void ProcessReceived(SocketAsyncEventArgs readArgs)
{
    var token = (IUserToken)readArgs.UserToken;
    var conn = (token as StreamUserToken)?.Connection;

    if (conn != null)
        conn.OnRawReceived(readArgs.Buffer.AsSpan(readArgs.Offset, readArgs.BytesTransferred));
    else
        OnServerReceiveSpan?.Invoke(token, readArgs.Buffer.AsSpan(readArgs.Offset, readArgs.BytesTransferred));

    ProcessReceive(readArgs);
}
```

流模式下 `OnServerReceiveSpan` 的兼容：

- 若 `useStream == true` 且 `OnServerReceiveSpan != null`：启动一个泵任务，从 `PipeReader` 读并调用 `OnServerReceiveSpan`。此时消费者**不得**再读 `ChannelInfo.Stream`（数据已被泵消费）。文档标注为互斥。
- 若 `OnServerReceiveSpan == null`：数据留在 Pipe，由 `ChannelInfo.Stream` 消费者读取（与旧 `StreamServerSocket` 行为一致）。

### 7.3 发送 API

`SaeaServerSocket.SendAsync(userToken, ...)`：

- 未启用 TLS：保持现有实现。
- 启用 TLS：先经 `connection.SendApplicationData(...)` 加密，再走 IOCP 发送。
- `SendAsync(string id, ...)`：从 `ChannelManager` / 连接表解析 `SaeaConnection` 后转发。

### 7.4 停止 / 释放

- 逐连接 `connection.Dispose()`；
- `ChannelManager` 改为**每服务器实例**（见 8.1），`Clear()` 只关闭本服务器连接；
- 保留 `ChannelManager.Instance` 静态属性以兼容旧代码，但内部改为 `AsyncLocal`? 不，简单保留一个静态实例供显式使用，`SaeaServerSocket` 不再使用它。

---

## 8. 兼容与迁移

### 8.1 `ChannelManager` 调整

现状：`ChannelManager.Instance` 是全局单例，`Clear()` 会关闭所有服务器的连接，多服务器场景存在串扰。

调整：

- 构造函数由 `protected` 改为 `public ChannelManager(int timeOut = 60)`；
- `SaeaServerSocket` 持有自己的 `ChannelManager` 实例；
- `ChannelInfo` 增加 `Tls`；
- `ChannelManager.Set` 增加可选 `ISaeaTlsContext tls = null` 参数；
- 保留 `Instance` / `GetInstance` 静态 API，标注 `[Obsolete]`，仅用于兼容。

### 8.2 `SocketFactory`

删除 `StreamServerSocket` / `StreamClientSocket` 分支：

```csharp
public static IServerSocket CreateServerSocket(ISocketOption option, CancellationToken ct)
{
    if (option.SocketType == SAEASocketType.Tcp)
        return new SaeaServerSocket(option);
    // UDP 分支不变
}

public static IClientSocket CreateClientSocket(ISocketOption option)
{
    if (option.SocketType == SAEASocketType.Tcp)
        return new SaeaClientSocket(option);
    // UDP 分支不变
}
```

`UseIocp` 不再参与工厂选择；保留属性仅为源码兼容。

### 8.3 消费者迁移清单

| 位置 | 现状 | 迁移 |
| --- | --- | --- |
| `SAEA.MQTT/Implementations/MqttTcpServerListener.cs` | `UseStream().WithSsl(cert, Tls12)`；`(SslStream)stream` 取客户端证书 | `UseStream().WithSsl(o => { o.ServerCertificate = cert; o.Protocols = Tls12; })`；`ci.Stream` 直接使用；客户端证书从 `ci.Tls?.RemoteCertificate` 取 |
| `SAEA.MQTT/Implementations/MqttTcpChannel.cs` | `(StreamClientSocket)client).ConnectAsync(...)` | Builder 配置 `WithSsl(...)`；`await client.ConnectAsync(ct)`；`_stream = client.GetStream()`；`IsSecureConnection = client.IsAuthenticated` |
| `SAEA.MQTT/Implementations/MqttTcpChannel.cs` 流构造 | `IsSecureConnection = stream is SslStream` | 构造函数增加 `bool isSecure` 或 `ISaeaTlsContext tls` 参数 |
| `SAEA.WebSocket/Core/WSSServerImpl.cs` | `UseStream()` + `ChannelInfo.Stream` | 不变（类型改为 `SaeaServerSocket`，由工厂返回） |
| `SAEA.Socket5/Server/Socks5Server.cs` | `UseStream()` + `ChannelInfo.Stream` | 不变 |
| `SAEA.DNS/DnsServer.cs` | `UseStream()` + `GetStream()` | 不变 |
| `SAEA.Sockets/Shortcut/TCPClient.cs` | `UseIocp()` + `SocketStream` | 不变；`SocketStream` 继续基于 `OnClientReceiveSpan` |
| `SAEA.P2P` | `IocpClientSocket` / `IocpServerSocket` 类型引用 | 改为 `SaeaClientSocket` / `SaeaServerSocket` |
| `SAEA.FTP/Net/ServerSocket.cs` | `UseIocp()` + `IUserToken` | 不变 |
| `SAEA.FileSocket` / `SAEA.MessageSocket` | `OnServerReceiveSpan` | 不变 |
| `Src/SAEA.MVCTest` | 引用 `BouncyCastle.Cryptography 2.6.2` | 与 `SAEA.Sockets` 统一即可 |

### 8.4 删除清单

- `Src/SAEA.Sockets/Core/Tcp/StreamClientSocket.cs`
- `Src/SAEA.Sockets/Core/Tcp/StreamServerSocket.cs`
- 相关 `using System.Net.Security;`（仅保留 `RemoteCertificateValidationCallback` / `SslPolicyErrors` 所需）
- `SocketOptionBuilder.WithSsl` 中的 `UseIocp` 限制
- `SaeaClientSocket.GetStream()` 的 `InvalidOperationException`

### 8.5 `SocketStream` 的定位

保留 `Core/SocketStream.cs`：

- 它是**事件模式**的流包装器，基于 `OnClientReceiveSpan`；
- 不参与 TLS（TLS 已由 `SaeaConnection` 在更底层完成，`OnClientReceiveSpan` 拿到的是明文）；
- 与新的 `SaeaStream` 并存：
  - `client.GetStream()` → `SaeaStream`（推荐）；
  - `new SocketStream(client)` → 旧式包装（兼容）。

---

## 9. 生命周期与状态机

### 9.1 连接状态

```
Created
  → TcpConnected
      → (TLS) Handshaking
          → Authenticated
          → Failed → Closed
      → (明文) Authenticated（等价）
  → Closing
  → Closed
```

`SaeaConnection` 暴露：

```csharp
public bool IsConnected { get; }
public Task HandshakeTask { get; }
public ISaeaTlsContext Tls { get; }
```

### 9.2 握手超时

- 服务端：`Tls.HandshakeTimeout`，默认 10s；从 `BeginHandshake()` 开始计时。
- 客户端：`Tls.HandshakeTimeout`；若未设置，回退到 `ConnectTimeout`。
- 超时动作：`TrySetException(new TimeoutException(...))` → `HandshakeFailed` → 关闭 Socket → `OnError`。

### 9.3 失败语义

| 场景 | `ConnectAsync` 回调 | 事件 | 流读写 |
| --- | --- | --- | --- |
| TCP 连接失败 | `SocketError` | `OnError` | `IOException` |
| TLS 握手失败 | `SocketError.SocketError` | `OnError` + `HandshakeFailed` | `SaeaTlsException` |
| 证书校验失败 | `SocketError.SocketError` | `OnError` + `HandshakeFailed` | `SaeaTlsException` |
| 握手超时 | `SocketError.TimedOut` | `OnError` + `HandshakeFailed` | `TimeoutException` |
| 远端关闭 | — | `OnDisconnected` | `ReadAsync` 返回 0 |

---

## 10. 线程、性能与内存

### 10.1 线程模型

- 接收：`SocketAsyncEventArgs.Completed` 回调（IOCP 线程）。
- TLS 处理：在接收回调内同步执行 `OfferInput` + `ReadInput` + `DrainOutput`，避免额外线程切换。
- 发送：应用线程调用 `WriteAsync` → 加密 → `SendAsyncRaw`；`SendAsyncRaw` 内部 `WaitWrite` 串行化。
- 握手输出：接收回调内产生，`SendAsyncRaw` 直接发送；若 `WaitWrite` 超时则断开连接。
- 回调：`HandshakeStarted/Completed/Failed` 在释放 `_tlsLock` 后触发，避免重入死锁。

### 10.2 内存

- 明文事件模式：保持现有 Span 零拷贝。
- 流模式：`Pipe` 使用池化缓冲（`PipeOptions` 默认池）。
- TLS：
  - 入站租用 `ArrayPool<byte>` 复制密文，`OfferInput` 后立即归还；
  - 出站租用发送缓冲，`owner` 在发送完成后归还；
  - 每连接固定发送缓冲大小 = `max(WriteBufferSize, 16KB)`。

### 10.3 背压

- 流模式：`Pipe` 高/低水位 + 暂停 `ReceiveAsync`。
- 事件模式：无背压（与现状一致），消费者需自行控制。
- TLS 流模式：解密后写入 `Pipe`；若 `FlushAsync` 未完成，暂停 `ReceiveAsync`。

### 10.4 已知性能取舍

- TLS 模式失去零拷贝；
- 每连接一个 `SemaphoreSlim` + `Pipe`；
- BC TLS 的 CPU 成本高于 `SslStream`（后者可能使用 OS/OpenSSL 优化）。本设计优先“能力完整 + 跨平台一致”，性能基准在 Phase 5 补测。

---

## 11. 测试计划

### 11.1 单元测试

新增 `Src/SAEA.Sockets.Tests`（若不存在则创建，`net10.0`）：

- `SaeaCertificateLoader`：PFX、证书库、PEM（RSA/ECDSA）、错误密码、无私有钥；
- `BcKeyConverter`：RSA/ECDSA 私钥转换；
- `SaeaTlsProtocolMapper`：版本映射、非法值；
- `SaeaCertificateValidator`：SAN、通配符、IP、CN 回退、链错误；
- `SaeaTlsOptions` 默认值与 Builder 映射；
- `SocketFactory`：TCP 始终返回 `Saea*`，UDP 不受影响。

### 11.2 集成测试

- 明文：`SaeaServerSocket` + `SaeaClientSocket` 回显 1B / 4KB / 1MB；
- TLS 单向：BC 客户端 ↔ BC 服务端；自签 PFX；
- TLS 双向：服务端 `ClientCertificateRequired`，客户端提供证书；缺证书必须失败；
- SNI：服务端配置 `a.example.com.pfx` / `b.example.com.pfx`，客户端分别连接并断言 `RemoteCertificate.Subject`；
- 握手失败：客户端 `AllowInvalidCertificates=false` + 自签证书 → `HandshakeFailed`；
- 握手超时：服务端延迟不应答 → 客户端超时；
- 大包 + 并发：100 连接 × 1MB；
- 半关闭 / 远端 RST / 主动 Dispose；
- 背压：客户端只写不读，服务端 Pipe 达到高水位后不再读取 Socket（通过统计 `ReceiveAsync` 暂停验证）；
- 与 .NET `SslStream` 互操作：
  - `SslStream` 客户端 ↔ Saea TLS 服务端；
  - Saea TLS 客户端 ↔ `SslStream` 服务端；
- `openssl s_client -connect ... -servername a.example.com` 验证 SNI；
- MQTT over TLS（`MqttTcpServerListener` + `MqttTcpChannel`）；
- WebSocket `wss://`（若现有测试覆盖）。

### 11.3 回归测试

- `dotnet build` 全部 `netstandard2.0` 项目；
- 全部现有测试项目；
- `SAEA.P2PTest` 中 `StreamPipelineTest` / `StreamDecoderTest`（验证 `ChannelInfo.Stream` 行为）；
- FTP、P2P、MessageSocket、FileSocket 的 Span 事件路径。

### 11.4 验收标准

1. `grep -R "new SslStream"` 在 `Src/SAEA.Sockets` 中为 0；
2. `grep -R "StreamClientSocket\|StreamServerSocket"` 全仓为 0；
3. TCP 工厂只返回 `SaeaClientSocket` / `SaeaServerSocket`；
4. 明文、TLS 单向、TLS 双向、SNI、握手失败/超时用例全部通过；
5. MQTT TLS 端到端通过；
6. 现有测试无回归；
7. 公共 API 变更写入 `docs/superpowers/specs/2026-11-05-saea-sockets-iocp-ssl-api-migration.md`（实现阶段补）。

---

## 12. 实施阶段

### Phase 0：基线与骨架（0.5 天）

- 添加 `BouncyCastle.Cryptography 2.6.2`；
- 新增 `Ssl` 命名空间与 `SaeaTlsOptions` / `ISaeaTlsContext` / `SaeaCertificateLoader` 骨架；
- 扩展 `ISocketOption` / `SocketOption` / `SocketOptionBuilder`；
- 编译通过，不接运行时。

### Phase 1：IOCP 明文流门面（1 天）

- `SaeaConnection` + `SaeaStream` + `Pipe`；
- `SaeaClientSocket.GetStream()` 返回 `SaeaStream`；
- `SaeaServerSocket` 的 `UseStream` 分支创建 `ChannelInfo`；
- `ChannelManager` 实例化；
- 迁移 DNS / Socket5 / WebSocket / MQTT 明文路径；
- 删除 `Stream*` 的客户端/服务端引用。

### Phase 2：TLS 适配器（2 天）

- `SaeaTlsTransport` + `SaeaTlsServerTransport` + `SaeaTlsClientTransport`；
- `BcKeyConverter` + `SaeaCertificateValidator`；
- 握手回调、超时、协议映射；
- 服务端 SNI / mTLS / 客户端证书；
- 客户端 `TargetHost` / 客户端证书 / 校验回调；
- 单元测试 + 自签集成测试。

### Phase 3：Socket 集成（1.5 天）

- `SaeaClientSocket` 连接流程接入 TLS；
- `SaeaServerSocket` 接受流程接入 TLS；
- 发送/接收路径 TLS 分派；
- 关闭 / EOF / 错误语义；
- 互操作测试（`SslStream` / `openssl`）。

### Phase 4：消费者迁移与删除（1 天）

- 删除 `StreamClientSocket` / `StreamServerSocket`；
- `SocketFactory` 清理；
- `MqttTcpServerListener` / `MqttTcpChannel` 迁移；
- `P2P` 类型引用迁移；
- 全仓 `grep` 清理。

### Phase 5：硬化与文档（1 天）

- 背压 / 并发 / 大包压测；
- `openssl s_client` SNI 验证；
- 性能基线（明文 vs TLS）；
- API 迁移文档；
- 版本号与包说明更新。

总计约 7 天（不含评审与修复）。

---

## 13. 风险与缓解

| 风险 | 影响 | 缓解 |
| --- | --- | --- |
| BC 非阻塞握手驱动复杂 | 握手卡死 / 数据丢失 | 严格使用 `OfferInput` / `ReadInput` / `ReadOutput`；集成测试覆盖分片记录、一次多记录、握手后立即发数据 |
| `netstandard2.0` 缺少 PEM API | 证书加载受限 | PFX + 证书库优先；PEM 用 BC 解析并单测 |
| `X509Certificate2.GetRSAPrivateKey()` 在部分平台返回 null | TLS 启动失败 | 回退 `PrivateKey` / `Export(Pfx)` + BC 解析；明确错误信息 |
| SNI 选择证书与 cipher suite 不匹配（RSA 证书 + ECDSA 套件） | 握手失败 | 按证书类型过滤套件；集成测试覆盖 RSA/ECDSA |
| `OnAccepted` 延迟到握手完成 | 消费者感知变化 | 文档明确；无 TLS 路径不变 |
| TLS 模式失去零拷贝 | 吞吐下降 | 基准测试；明文事件模式保持零拷贝 |
| `ChannelManager` 从全局改为实例 | 旧代码依赖 `Instance` | 保留静态 API 并标 `[Obsolete]` |
| 外部实现 `ISocketOption` 需新增成员 | 源码破坏 | 仓库内唯一实现为 `SocketOption`；迁移文档说明 |
| `IServerSocket` 不新增成员 | 消费者拿不到流 | 用可选接口 `IServerConnectionProvider`，避免破坏 UDP/外部实现 |
| BC 包体积 | 发布包增大 | 可接受；后续可评估裁剪 |

---

## 14. 待确认决策

请在进入实现前确认以下选择：

1. **BouncyCastle 版本**：采用 `2.6.2`（与 `SAEA.MVCTest` 一致），还是直接上 `2.7.0`？
2. **TLS 握手完成后再触发 `OnAccepted`**：是否接受？若不接受，可改为“TCP 接入即触发，`ChannelInfo.Tls` 在握手后填充”。
3. **`ISocketOption` 新增 `UseStream` / `Tls` 成员**：是否接受这一源码级破坏？若需完全兼容，可改为扩展方法 + `SocketOption` 具体类型访问。
4. **`ChannelManager` 改为每服务器实例**：是否接受？静态 `Instance` 保留但标记过时。
5. **PEM 证书加载**：本期是否必须？若不必须，Phase 2 只做 PFX + 证书库，PEM 延后。
6. **默认协议版本**：采用 `TLS 1.2 + TLS 1.3`，还是为兼容旧设备保留 `TLS 1.0/1.1`？本设计建议默认 1.2+1.3，旧版本需显式开启。
7. **`SaeaClientSocket.ConnectAsync(Action<SocketError>)` 的 TLS 语义**：握手成功才回调，是否接受？

---

## 15. 附：关键 BC API 对照

| 用途 | API |
| --- | --- |
| 服务端协议 | `new TlsServerProtocol()`（非阻塞）+ `Accept(TlsServer)` |
| 客户端协议 | `new TlsClientProtocol()`（非阻塞）+ `Connect(TlsClient)` |
| 加密实现 | `new BcTlsCrypto()` |
| 服务端基类 | `DefaultTlsServer` / `AbstractTlsServer` |
| 客户端基类 | `DefaultTlsClient` / `AbstractTlsClient` |
| 喂入密文 | `TlsProtocol.OfferInput(byte[], int, int)` |
| 读取明文 | `GetAvailableInputBytes()` + `ReadInput(byte[], int, int)` |
| 写入明文 | `WriteApplicationData(byte[], int, int)` |
| 取走密文 | `GetAvailableOutputBytes()` + `ReadOutput(byte[], int, int)` |
| 握手回调 | `NotifyHandshakeBeginning` / `NotifyHandshakeComplete` / `NotifyAlertRaised` / `NotifyAlertReceived` |
| SNI 读取 | `TlsExtensionsUtilities.GetServerNameExtensionClient(IDictionary<int, byte[]>)` |
| SNI 写入 | `TlsExtensionsUtilities.AddServerNameExtensionClient(...)` |
| 证书链 | `new Certificate(TlsCertificate[])`，`BcTlsCertificate` |
| 签名凭据 | `BcDefaultTlsCredentialedSigner(TlsCryptoParameters, BcTlsCrypto, AsymmetricKeyParameter, Certificate, SignatureAndHashAlgorithm)` |
| 签名算法选择 | `TlsUtilities.ChooseSignatureAndHashAlgorithm(...)` |
| 客户端证书请求 | `new CertificateRequest(short[], IList<SignatureAndHashAlgorithm>, IList<X509Name>)` |
| 协商结果 | `TlsContext.SecurityParameters.NegotiatedVersion` / `CipherSuite` / `PeerCertificate` / `LocalCertificate` / `ApplicationProtocol` |