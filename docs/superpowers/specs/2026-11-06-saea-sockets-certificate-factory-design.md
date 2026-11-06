# 开发用证书生成器（SaeaCertificateFactory + SslTlsTool 工具）设计

- 日期：2026-11-06
- 状态：已实施（2026-11-06）
- 范围：`SAEA.Sockets` 新增公开生成 API；新增独立控制台工具工程 `SAEA.Sockets.SslTlsTool`

## 1. 为什么做

`WithSsl(...)` 需要 PFX 或 PEM 文件，但仓库只提供读取能力（`SaeaCertificateLoader`），没有生成能力。开发者在跑 TLS 自测、用 `openssl s_client` / curl / 浏览器连自己的 SAEA 服务端时，手边没有证书，只能临时敲 OpenSSL 命令：命令行长、平台差异大、SAN 常常漏掉 IP、口令与有效期每次都不一样。

`SAEA.P2PTest` 内部的 `TlsTest.CreateCertificate` 是内存自签，只够测试自己用：外部看不到文件，也无法给其他语言或工具使用。

所以需要两个东西：库内可编程的生成 API，和一个能一条命令产出全套文件的控制台工具。

## 2. 目标

- G1：`SAEA.Sockets.Ssl` 暴露易用的生成 API，支持两种模式——单张自签证书、本地根 CA 加由其签发的服务端/客户端证书。
- G2：新增控制台工具 `Src/SAEA.Sockets.SslTlsTool/`，一条命令产出上述文件；既支持 PowerShell/bash/CI 脚本化调用，也支持无参数进入交互模式逐行执行同一套参数。
- G3：产物能被 `SaeaCertificateLoader.FromPfx` / `FromPem` 读回，`WithSsl` 现有示例无需修改。
- G4：默认值贴合本仓库自测场景（`localhost` + `127.0.0.1`、口令 `saea`），同时覆盖 mTLS（服务端要求客户端证书）。
- G5：纯 BouncyCastle 实现，目标框架仍是 `netstandard2.0`、C# 8，`.NET Framework 4.6.2` 消费方在运行时也能调用。
- G6：库与工具的默认行为是同一套规则（SAN 推导、有效期、口令），工具不额外发明语义，避免「同参数、不同结果」。

## 3. 非目标

- 不接 ACME / Let's Encrypt / CSR 流程，不调用 `certreq` 或 OpenSSL 命令行。
- 不做证书热更新、CRL、OCSP。
- 不改 `SAEA.P2PTest`（已确认）：`--tls` / `--tls-stress` / `--bench-tls` 继续用内存证书，`TlsTest.CreateCertificate` 保持原样，不与新 API 去重。
- 不新增 NuGet 依赖，不修改任何 `.csproj` 的 `<Version>` 或 `PackageReference`。
- 不产出可用于生产的证书；工具与 API 都要在输出里明确标注「仅开发/测试」。
- 不新建 MSTest 工程（理由与替代验证见 §13）。

## 4. 反目标（做到这样就算失败）

- 生成的证书看起来能用，但一开真校验就必然是链错误或主机名不匹配，例如漏 SAN、leaf 缺 EKU、CA 缺 `keyCertSign`。
- 为了让示例跑通，默认引导用户设 `AllowInvalidCertificates = true`。
- 工具悄悄覆盖已有证书文件，或把 CA 私钥写到非预期位置。
- 库路径和 CLI 路径对同一份参数给出不同证书（尤其 SAN 与有效期）。

## 5. 现状约束（实现必须遵守）

- `SAEA.Sockets.csproj`：`netstandard2.0`、`LangVersion 8.0`、已引用 `BouncyCastle.Cryptography 2.6.2`、`System.IO.Pipelines`。
- `SaeaCertificateLoader` 打包 PKCS#12 后统一走 `new X509Certificate2(bytes, password, DefaultKeyStorageFlags)`；`DefaultKeyStorageFlags = EphemeralKeySet | Exportable`。
- `SaeaCertificateLoader.LoadPkcs12`（`byte[]` / `path` 两个重载）对旧运行时不支持 `EphemeralKeySet` 的情况有 `catch (ArgumentException)` / `catch (PlatformNotSupportedException)` 回退，改用默认 flags。**生成器必须复用这条回退路径**，不能直接 `new X509Certificate2(bytes, pwd, DefaultKeyStorageFlags)`，否则 net462 上会抛异常、G5 不成立。实现方式：把 `byte[]` 重载从 `private` 改成 `internal`（同程序集，不改公开 API），生成器调用它。
- `SaeaCertificateLoader.ReadPrivateKey` 只识别 `AsymmetricCipherKeyPair` 与 `AsymmetricKeyParameter`，但 BC 的 `PemReader` 对 `RSA PRIVATE KEY`（PKCS#1）和 `PRIVATE KEY`（PKCS#8）都会返回它认识的私钥对象。**实测两种格式 `FromPem` 均能读回且 `HasPrivateKey == true`（§16）**，因此本期不改 `ReadPrivateKey`，也不要求生成器写 PKCS#8。
- `SaeaCertificateLoader.FromPem` 用进程内静态缓存（`路径|路径|口令`）缓存结果，同一进程内改写文件后再次读取会拿到旧对象。工具每次进程只生成一次、读一次，不受影响；`--verify` 必须在生成完成后再读，不能预读。
- `SaeaCertificateLoader.FromPem` 的 `password` 同时用于「PEM 私钥口令」和「中转 PKCS#12 口令」，且 `LoadPemCore` 只读取证书文件中的第一张证书（链会被丢弃）。
- `SaeaCertificateLoader.FromPfx` 得到的是叶子证书：PKCS#12 里就算带了 CA，`new X509Certificate2(bytes, pwd)` 也只给叶子（实测：`X509Certificate2Collection.Import` 能看到 CA + 叶子两条，但 `FromPfx` 只有叶子）。
- 服务端 mTLS 校验走 `ValidateChain(..., allowUnknownRoot: true, ...)`，且 `VerificationFlags.AllowUnknownCertificateAuthority` 下 `X509Chain.Build` 对「只有叶子、CA 不在信任库」的情况返回 **true**（状态仍是 `PartialChain`，实测见 §16）。所以回环 mTLS 自检不需要导入信任库，也不需要把服务端设成 `AllowInvalidCertificates = true`；但**这不能当作「链校验通过」的证据**，链校验证据只能来自 `CustomRootTrust`。
- `SaeaCertificateValidator.ValidateServerCertificate` 走 `ValidateChain(..., allowUnknownRoot: false, ...)`，`ExtraStore` 只放握手带来的中间证书。因此 CA 必须进系统信任库，客户端才能在 `AllowInvalidCertificates = false` 下验证通过；实测「CA 只放 ExtraStore」仍然是 `UntrustedRoot`（§16）。工具必须把这个前提讲清楚，不能暗示「生成完就自动可信」。
- `SaeaCertificateValidator.MatchHostName` 的实际规则：主机是 IP 时只认 `iPAddress` SAN；存在 `dNSName` SAN 时不再回退 CN；只有 `iPAddress` SAN、而主机是域名时仍会回退 CN。默认产物必须同时写 `dNSName`（`localhost`）与 `iPAddress`（`127.0.0.1`），两条校验路径都要覆盖。
- `BcKeyConverter.ToCertificateChain` 用 `X509VerificationFlags.AllFlags` 构链；CA 未装进信任库时链长度只有 1，服务端只下发叶子。这印证了「必须导入 CA」的前提。
- `BcKeyConverter` 是程序集内 `internal` 类，`GetPrivateKey(X509Certificate2)` 同程序集可直接复用；它内部走 `GetRSAPrivateKey()` / `GetECDsaPrivateKey()`，依赖私钥可导出（`Exportable`）。
- 仓库 `.gitignore` 当前只忽略 `*.pfx`，不忽略 `*.key`；仓库内没有被跟踪的 `.crt` / `.key` / `.pem`（已核对）。本期必须补 `*.key` 与 `certs/`，否则 `--out` 任意目录或默认目录下的私钥有被误提交的真实风险。
- 仓库已有独立演示工程先例（如 `Src/TestConnectionReuseApp`，net10.0 控制台，未加入 sln）；`Src/<组件>/` 是一个目录一个 NuGet 包，`Src/*Test/` 是可运行演示。证书生成器是开发者工具而非演示程序，命名为 `Src/SAEA.Sockets.SslTlsTool/`（不以 `*Test` 结尾），并加入 `Src/SAEA.Sockets.sln`（sln 里同时有 net462 老工程，新增 net10.0 工程不影响它们）。

## 6. 公开 API（命名空间 `SAEA.Sockets.Ssl`）

三个类型：工厂、参数、证书包；CA 用独立句柄类型承载私钥。公开签名不出现 BouncyCastle 类型。

```csharp
/// <summary>证书用途</summary>
public enum SaeaCertificatePurpose
{
    Server,
    Client,
    CertificateAuthority
}

/// <summary>证书生成参数</summary>
public sealed class SaeaCertificateOptions
{
    public SaeaCertificatePurpose Purpose { get; set; }
    public string Subject { get; set; }               // 完整 DN，如 "CN=localhost,O=SAEA"
    public string[] DnsNames { get; set; }            // null = 按 Subject 的 CN + "localhost" 推导
    public string[] IpAddresses { get; set; }         // null = { "127.0.0.1" }
    public int KeySize { get; set; }                  // 0 或未赋值 = 2048
    public DateTimeOffset NotBefore { get; set; }     // 默认 UtcNow.AddDays(-1)
    public DateTimeOffset NotAfter { get; set; }      // 叶子默认 UtcNow.AddDays(30)；CA 默认 UtcNow.AddDays(3650)
    public string PfxPassword { get; set; }           // null = "saea"；"" = 无口令
}

/// <summary>证书与其私钥，附带保存能力</summary>
public sealed class SaeaCertificateBundle : IDisposable
{
    public X509Certificate2 Certificate { get; }
    public void SavePfx(string path, string password = null);          // 自签只写叶子；CA 签发写 leaf + CA 链
    public void SaveCertificatePem(string path);                       // 只写单张证书（CERTIFICATE）
    public void SavePrivateKeyPem(string path);                        // 明文私钥，BC 默认 RSA PRIVATE KEY（PKCS#1）
    public void Dispose();
}

/// <summary>本地根 CA：持有私钥，用于签发叶子证书</summary>
public sealed class SaeaCertificateAuthority : IDisposable
{
    public X509Certificate2 Certificate { get; }

    public static SaeaCertificateAuthority Create(SaeaCertificateOptions options);
    public static SaeaCertificateAuthority FromPfx(string path, string password = null);
    public static SaeaCertificateAuthority FromPem(string certificatePath, string privateKeyPath, string password = null);
    public SaeaCertificateBundle IssueServerCertificate(SaeaCertificateOptions options);
    public SaeaCertificateBundle IssueClientCertificate(SaeaCertificateOptions options);
    public void SavePfx(string path, string password = null);          // 只写 CA 自身
    public void SaveCertificatePem(string path);
    public void SavePrivateKeyPem(string path);
    public void Dispose();
}

public static class SaeaCertificateFactory
{
    public static SaeaCertificateBundle CreateSelfSigned(SaeaCertificateOptions options);
}
```

### 6.1 语义约定

**用途与签发**

- `CreateSelfSigned`：issuer == subject，自签；`Purpose = Server` 时写 SAN 与 `serverAuth`，`Purpose = Client` 时写 `clientAuth` 且不写 SAN。`Purpose = CertificateAuthority` 时抛 `ArgumentException`（根证书走 `SaeaCertificateAuthority.Create`）。
- `SaeaCertificateAuthority.Create`：签发 `CA:TRUE`、`pathLenConstraint = 0`（`new BasicConstraints(0)`）、`keyCertSign` + `cRLSign`（critical）、含 SKI 的根证书；忽略 `options.Purpose` / `DnsNames` / `IpAddresses`。
- `FromPfx` / `FromPem`：复用已有 CA 证书与私钥；证书必须 `HasPrivateKey` 且 `BasicConstraints.CertificateAuthority == true`，否则抛 `SaeaTlsException`。这条入口是必需项：否则每次重跑工具都会换 CA，已导入的信任作废。
- `Issue*`：issuer 取 CA 的 Subject DN，签名用 CA 私钥；以方法名为准（`options.Purpose` 被忽略），叶子 `CA:FALSE`（`new BasicConstraints(false)`，critical）、`keyUsage = digitalSignature | keyEncipherment`、EKU 按用途、写 AKI（`X509ExtensionUtilities.CreateAuthorityKeyIdentifier(caCertificate)`）与 SKI、服务端叶子写 SAN；`IssueClientCertificate` 忽略 `DnsNames` / `IpAddresses`。
- 签名算法统一 `SHA256WITHRSA`。
- 序列号：`new BigInteger(127, random).SetBit(127)`，128 位且必为正非零，每次生成不同。

**SAN 规则（库与 CLI 共用）**

仅 `Purpose = Server`（以及 `CreateSelfSigned` 的 Server 用途）生效；`Client` / `CertificateAuthority` 忽略 `DnsNames` / `IpAddresses`。

- `Subject` 为 `null` / 空白 → 取 §6.1 默认值矩阵中对应对象的默认 DN；非空但不是合法 DN → 抛 `ArgumentException`（保留 inner exception）。
- `DnsNames == null`：取 `Subject` 中的 `CN`，若 CN 存在且不是 IP 字面量，则 SAN 为 `{ CN, "localhost" }`（去重、保持顺序）；否则为 `{ "localhost" }`。
- `DnsNames != null`：完全按给定内容写；空数组表示「不写 dNSName」。数组内出现 `null` / 空白元素 → 抛 `ArgumentException`。
- `IpAddresses == null`：`{ "127.0.0.1" }`。
- `IpAddresses != null`：完全按给定内容写；空数组表示「不写 iPAddress」。数组内出现 `null` / 空白元素，或任一值不能被 `IPAddress.TryParse` 解析（IPv4 / IPv6 均可）→ 抛 `ArgumentException`。
- `CN` 解析通过 `X509Name` 的属性列表完成（`GetValueList(X509Name.CN)`），不做字符串切分，避免 `O=SAEA,CN=api.local` 或带转义逗号的 DN 漏取。

**默认值矩阵**

默认值在调用时按「正在生成的对象」解析，不依赖枚举零值：`CreateSelfSigned` 看 `Purpose`，`Authority.Create` 用 CA 列，`IssueServerCertificate` / `IssueClientCertificate` 用 Server / Client 列。

| 项 | Server | Client | CA |
|---|---|---|---|
| `Subject` | `CN=localhost` | `CN=saea-client` | `CN=SAEA Dev Root CA` |
| `DnsNames` | 见上（默认 `{ "localhost" }`） | 忽略 | 忽略 |
| `IpAddresses` | 见上（默认 `{ "127.0.0.1" }`） | 忽略 | 忽略 |
| `KeySize` | 2048 | 2048 | 2048 |
| `PfxPassword` | `saea` | `saea` | `saea` |
| `NotBefore` | `UtcNow.AddDays(-1)` | 同左 | 同左 |
| `NotAfter` | `UtcNow.AddDays(30)` | `UtcNow.AddDays(30)` | `UtcNow.AddDays(3650)` |

- `NotBefore` / `NotAfter` 用 `default(DateTimeOffset)` 表示「未赋值」；`NotAfter <= NotBefore` 抛 `ArgumentException`。
- `KeySize == 0` 视为未赋值（取 2048）；非 0 时只接受 2048/3072/4096，否则抛 `ArgumentException`。
- `NotAfter` 取「生成时刻 + 天数」，`NotBefore` 取「生成时刻 - 1 天」留时钟偏差余量；因此证书实际跨度比「天数」多 1 天，CLI 文案写「有效至 n 天后」而不是「有效期 n 天」。

**口令与保存**

- `PfxPassword` 为 `null` → 取默认 `saea`；显式空字符串 → 生成无口令 PKCS#12（net8 实测可读，net462 已冒烟通过，见 §16.2）。
- `SaeaCertificateBundle.SavePfx(string path, string password = null)`：`password` 为 `null` 时用创建该 bundle 的 `options.PfxPassword`；自签写单张叶子，CA 签发的叶子写 leaf + 签发者 CA 证书（链路给 `openssl pkcs12`、curl、Java/Node 等外部工具用）。
- `SaeaCertificateAuthority.SavePfx(string path, string password = null)`：只写 CA 自身；`password` 为 `null` 时用创建/加载 CA 时的口令。
- `SaveCertificatePem` / `SavePrivateKeyPem` 只写单张证书 / 单个私钥，不写链；`FromPem` 只读第一张证书。
- `SavePrivateKeyPem` 写出的私钥**不加密**（BC 默认 PKCS#1，无口令），调用方要按明文私钥对待。
- `SaeaCertificateAuthority.FromPem(certificatePath, privateKeyPath, password)` 的 `password` 语义与 `SaeaCertificateLoader.FromPem` 一致：既是 PEM 私钥口令，也是后续 `SavePfx` 的默认口令；`null` 表示两者均无口令。
- `SaeaCertificateAuthority.FromPfx(path, password)` 的 `password` 既是读取口令，也是后续 `SavePfx` 的默认口令。

**生命周期**

- 返回对象持有证书私钥，调用方负责 `Dispose`；`SaeaCertificateAuthority` 内部缓存 CA 私钥，`Dispose` 后调用 `Issue*` 抛 `ObjectDisposedException`。
- `Certificate` 属性返回的 `X509Certificate2` 所有权归 bundle / authority；把它交给 `WithSsl(...)` 后，bundle 必须活到连接/服务端释放之后再 `Dispose`，否则对端握手会拿到已释放的证书句柄。
- 工厂与 CA 句柄都不保证线程安全，文档写明「生成阶段单线程使用」。
- 错误统一抛 `SaeaTlsException`（已有类型），参数错误抛 `ArgumentException`（含 DN / SAN / IP / KeySize / 有效期），文件读写错误保留原始 `IOException`。

## 7. 产物文件布局

工具默认输出目录 `./certs`（相对当前工作目录）。

自签模式（基名默认 `localhost`，可用 `--name` 覆盖）：

```
certs/localhost.pfx        PKCS#12，含私钥，口令 saea
certs/localhost.crt        PEM 证书（CERTIFICATE）
certs/localhost.key        PEM 私钥（明文，BC 默认 RSA PRIVATE KEY 即 PKCS#1）
```

CA 模式：

```
certs/ca.pfx      certs/ca.crt      certs/ca.key          ← CA 私钥，明文，泄露=签发权泄露
certs/server.pfx  certs/server.crt  certs/server.key      ← SAN: localhost + 127.0.0.1
certs/client.pfx  certs/client.crt  certs/client.key      ← EKU clientAuth，无 SAN
```

- 服务端 PFX 的 PKCS#12 条目带上 CA 证书（key entry 的证书数组为 leaf + CA），主题与私钥仍是叶子。这个链是给 `openssl pkcs12`、curl、Java/Node 等外部工具用的。
- **本库不读回链**：`SaeaCertificateLoader.FromPfx` 最终是 `new X509Certificate2(bytes, pwd, flags)`，只承载叶子；服务端下发链走 `BcKeyConverter.ToCertificateChain` → `X509Chain`，从系统信任库补。因此 CA 未导入信任库时，SAEA 服务端实际只发叶子——这是本期已知限制，不改 `SaeaCertificateLoader` / `BcKeyConverter`（改了属公开行为变更，超出已确认范围）。README 的信任导入说明必须把这个前提写清楚。
- nginx / curl 若需要 leaf + CA 拼接的 fullchain，用 `cat server.crt ca.crt > server.fullchain.crt`；本期不额外产出链 PEM，避免与 `SaveCertificatePem`（单张）的语义混淆。
- `.crt` / `.key` 一律 LF 换行、UTF-8 无 BOM，便于跨平台工具读取。
- `.key` 是**明文私钥**，没有口令保护；`.pfx` 才有口令。文档、工具输出与 README 都必须分开表述，不能让人以为「口令 saea」保护了 `.key`。
- 叶子文件命名可用 `--name` 改基名（只影响自签模式；CA 模式固定 `ca`/`server`/`client`，避免脚本猜名字）。

## 8. 工具契约（`Src/SAEA.Sockets.SslTlsTool/`）

- 工程：`net10.0`、`OutputType=Exe`、无 `Version`、`ProjectReference` 到 `SAEA.Sockets`；文件头 banner 的 `CLR版本` 写 `net10.0`，`版本号` 写 `net10.0`（测试/演示工程规则）。
- 完全无参数运行 → 进入交互模式，`exit` / `quit` / EOF 时进程 `exit 0`。
- 交互模式逐行读取，按空白拆分参数；双引号可包住含空格的取值，引号内只把 `\"` 当转义、反斜杠本身原样保留（避免破坏 Windows/UNC 路径）；引号未闭合只提示该行参数错误并继续。命令与命令行参数完全同构，额外识别 `help` / `exit` / `quit`（不区分大小写）；`--help` 在交互模式只打印帮助并继续。
- 交互模式下的单条命令失败只打印该次退出码并继续等待，不改变进程最终退出码（正常退出固定为 0）。
- 给了参数但既没有 `--self-signed` 也没有 `--ca`（且没有 `--help`）→ 命令行模式打印帮助并 `exit 1`；交互模式只打印一句提示并继续下一条命令。
- `--help` 与其它参数并存时只打印帮助并 `exit 0`，不执行生成。
- 帮助里必须列出退出码含义（见本节末尾）与交互模式用法，否则脚本作者只能猜。

```
--self-signed            生成单张自签证书
--ca                     生成 CA + server + client
--out <dir>              输出目录，默认 ./certs
--name <base>            自签模式文件基名，默认 localhost；未给 --cn 时同时作为 CN
--cn <subject>           自签证书与服务端叶子的主题 DN，默认 CN=localhost；CA 模式只影响 server
--ca-cn <subject>        CA 模式：CA 主题 DN，默认 CN=SAEA Dev Root CA
--client-cn <subject>    CA 模式：客户端主题 DN，默认 CN=saea-client
--dns <a,b>              服务端 DNS SAN；未给时按 CN 自动推导（见 §6.1）
--ip <a,b>               服务端 IP SAN，默认 127.0.0.1
--days <n>               叶子有效至 n 天后，默认 30，范围 1..36500
--ca-days <n>            CA 有效至 n 天后，默认 3650，范围 1..36500
--keysize <n>            2048|3072|4096，默认 2048
--pwd <password>         PFX 口令，默认 saea；显式传空串表示无口令
--ca-pwd <password>      复用 CA 时的读取口令，默认取 --pwd
--reuse-ca               复用 <out>/ca.pfx（或 ca.crt+ca.key）签新叶子，不重建 CA
--force                  覆盖已存在文件，并与 --reuse-ca 互斥
--verify                 生成后自检（见 §9）
--help
```

**参数规则**

- `--self-signed` 与 `--ca` 可同时给，两种都生成。
- `--ca` 在 `ca.*` 已存在时不会静默重建 CA：
  - 给 `--reuse-ca` → 复用 `ca.pfx`（缺失时退回 `ca.crt` + `ca.key`）签发新叶子。**复用模式会无条件覆盖 `server.*` / `client.*`**，否则「重签叶子」永远写不出新文件；CA 文件本身不动。此时 `--ca-cn` / `--ca-days` 被忽略，并在输出里提示。
  - 给 `--force` → 删除并重建 CA 与叶子，输出警告「已导入的信任会作废」。
  - 都没给 → 退出码 1，提示「要复用请加 `--reuse-ca`，要重建请加 `--force`」。
- `--reuse-ca` 必须与 `--ca` 同时出现，否则退出码 1；`--reuse-ca` 与 `--force` 同时出现也退出码 1（语义互斥）。
- `--reuse-ca` 但 `ca.*` 不存在、读取失败、口令错误或证书不是 CA → 退出码 2。
- `--ca-pwd` 只在 `--reuse-ca` 时生效；未给时取 `--pwd`。
- `--dns` / `--ip` 未给 → 传 `null`，走 §6.1 的推导规则；给了就逐个校验：
  - `--ip`：`IPAddress.TryParse` 失败 → 退出码 1。
  - `--dns`：空串、含空白或逗号、长度 > 253 → 退出码 1；不校验国际化域名（本期不支持）。
  - `--dns ""` / `--ip ""`（显式空串）→ 退出码 1，提示「要使用默认值请省略该参数」；CLI 不暴露「不写该类 SAN」的语义（那是库 API 的能力）。
- `--days` / `--ca-days` 必须是 1..36500 的整数，否则退出码 1。新生成叶子的 `NotAfter` 晚于 CA 的 `NotAfter` 时也退出码 1（`--days > --ca-days` 属于这种情况）。
- `--name` 只在 `--self-signed` 时生效；只跑 `--ca` 时忽略并提示。取值必须是 `[A-Za-z0-9._-]{1,100}`，不得为 `.` / `..`，不得含路径分隔符，且不得为保留名 `ca` / `server` / `client`（不区分大小写），否则退出码 1。
- `--cn` 未给且给了 `--name` 时，自签证书主题取 `CN=<name>`；`--cn` 显式给时以 `--cn` 为准，文件名仍用 `--name`。
- `--pwd` 的「未给」与「给了空串」必须区分：解析层要保留「参数是否出现」标志；空串生成无口令 PFX 并提示风险。
- SAN 自动补齐统一由 §6.1 的规则决定：未显式给 `--dns` 时，`Subject` 的 CN 会自动进入 SAN，避免「改了 CN 但 SAN 不含它」导致主机名校验必然失败（对应 §4 反目标）。

**输出**

- 每个文件的完整路径、证书主题、SHA-1 指纹、PFX 口令状态：默认口令打印「默认 saea」，显式 `--pwd` 只打印「已按 --pwd 设置（不回显）」——避免口令进 CI 日志。
- 末尾固定打印一行 `证书仅用于开发/测试，请勿用于生产。`
- CA 模式额外打印信任导入与移除方法，并区分工具链：
  - Windows 当前用户：`Import-Certificate -FilePath certs\ca.crt -CertStoreLocation Cert:\CurrentUser\Root`；以服务账号运行或全机信任需 `Cert:\LocalMachine\Root`（管理员）。
  - Debian / Ubuntu：`sudo cp certs/ca.crt /usr/local/share/ca-certificates/saea-dev-ca.crt && sudo update-ca-certificates`（curl / OpenSSL / 系统浏览器生效）。
  - macOS：`sudo security add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain certs/ca.crt`；移除用 `sudo security remove-trusted-cert -d certs/ca.crt`。
  - Java 不读系统信任库，需 `keytool -importcert -cacerts -alias saea-dev-ca -file certs/ca.crt`；Node 需 `NODE_EXTRA_CA_CERTS=certs/ca.crt`（或新版的 `--use-system-ca`）。不要写「导入系统库后 Java/Node 直接可用」。
- 退出码（全模式统一，优先级 `1 > 2 > 0/3`）：
  - `0`：至少写入一个新文件（新建或覆盖），且给了 `--verify` 时自检通过。
  - `1`：参数错误（含 `--keysize 1024`、`--days 0`、`--dns ""`、保留名 `--name`、未知参数）。
  - `2`：生成、读取 CA、写盘或 `--verify` 自检失败。
  - `3`：一个文件都没有写入（全部已存在而跳过），且给了 `--verify` 时自检通过；脚本可据此判断「这次没产出新文件」，仍不是失败。
- 失败时向 stderr 打印一句中文原因与可执行建议，不输出堆栈（`--verbose` 不在本期）。
- `--out` 指向一个已存在的文件（而不是目录）→ 退出码 2，stderr 给出路径，不抛未处理异常。

## 9. `--verify` 自检（替代单测的证据来源）

生成完立刻在进程内验证，任一项失败则 `exit 2`，并打印失败项编号：

1. `SaeaCertificateLoader.FromPfx(pfxPath, pwd)` 成功且 `HasPrivateKey == true`（自签/叶子/CA 三份都要过）。
2. `SaeaCertificateLoader.FromPem(crtPath, keyPath)` 成功且 `HasPrivateKey == true`。生成器实际写出的是 PKCS#1 `RSA PRIVATE KEY`，loader 已支持，不需要额外分支（§16 实测）。同时按字节断言 `.crt` / `.key` 不含 `\r`、不以 UTF-8 BOM 开头。
3. CA 模式：`X509Chain` + `X509ChainTrustMode.CustomRootTrust` + `CustomTrustStore` 放 CA，`Build(serverCert)` 成功；同时断言 `Build(clientCert)` 成功。这是**唯一**的链校验证据，且只在工具（net10）里用，库不得引入该 API。
4. 证书 SAN 与主题断言：按本次实际生效值（`--cn` / `--dns` / `--ip` 或 §6.1 推导结果）检查 `dNSName` 与 `iPAddress`；CA 证书检查 `BasicConstraints.CA == true`；server 证书 EKU 含 serverAuth、client 证书 EKU 含 clientAuth。用 `X509Certificate2.Extensions` 解析（工具可用 `System.Formats.Asn1`），不依赖 internal 类型。
   - 期望值由工具按命令行输入独立计算，**不复用**库的 SAN 推导实现；这样才能发现库侧推导错误。两边的规则必须在 §6.1 保持一致。
5. CA 模式：一次本机回环 TLS 握手——`SocketOptionBuilder` 建服务端（`ServerCertificate = server.pfx` 读回的证书，`ClientCertificateRequired = true`）+ 客户端（`ClientCertificates = client.pfx`，`TargetHost = "localhost"`）。客户端设置 `CertificateValidationCallback`，断言 `(errors & SslPolicyErrors.RemoteCertificateNameMismatch) == 0` 后返回 true（即容忍链错误、但不容忍主机名不匹配）；**不设置 `AllowInvalidCertificates`**。`UseStream()` 各收发一帧并比对。服务端不设 `AllowInvalidCertificates`，靠既有 `allowUnknownRoot: true` 接受客户端证书（§16 实测 `AllowUnknownCertificateAuthority` 下 `Build` 返回 true）。输出写成「握手：主机名匹配通过，未校验证书链；链证据见第 3 项」。自签模式没有 CA、也没有独立客户端证书，第 5 项用同一张自签证书同时充当服务端与客户端证书（§16.3 实测通过）。

**端口与超时**

- 监听端口用 `TcpListener(IPAddress.Loopback, 0)` 取系统分配的空闲端口，关闭监听后立即交给 SAEA 服务端使用，禁止硬编码。
- 绑定失败时先调用服务端 `Stop()`（该方法会把内部 `_listener` 置空），再重新取端口重试，最多 3 次；不能对同一个未 `Stop()` 的实例重复 `Start()`，因为 `SaeaServerSocket.Start` 在 `_listener != null` 时直接返回。
- 双方 `HandshakeTimeout` 设为 15 秒；`--keysize 4096` 只影响生成耗时，不改变握手超时。
- 第 5 项失败一律 `exit 2`，不做「自动放宽校验」的降级；若将来在某个平台确认是库不支持，按缺陷单独立项。

## 10. 实现要点（BouncyCastle 2.6.2，netstandard2.0）

- 密钥对：`RsaKeyPairGenerator` + `RsaKeyGenerationParameters(BigInteger.ValueOf(65537), new SecureRandom(), keySize, 64)`（命名空间 `Org.BouncyCastle.Crypto.Parameters`）。不要用 .NET 的 `RSA.Create(int)`，`.NET Framework 4.6.2` 运行时没有该重载，与 G5 冲突。
- 证书构造：`X509V3CertificateGenerator`；签名工厂 `new Asn1SignatureFactory("SHA256WITHRSA", signingKey, random)`；**只能用 `Generate(ISignatureFactory)`**（已 `SetPublicKey` 之后）。BC 2.6.2 里没有 `Generate(ISignatureFactory, AsymmetricKeyParameter)` 这个重载，照旧文档写会直接编译失败（实测 CS7036，§16）。
- 扩展：`X509Extensions.BasicConstraints` / `KeyUsage` / `ExtendedKeyUsage` / `SubjectKeyIdentifier` / `AuthorityKeyIdentifier` / `SubjectAlternativeName`。SKI 与 AKI 用非过时 API：`X509ExtensionUtilities.CreateSubjectKeyIdentifier(AsymmetricKeyParameter)`、`X509ExtensionUtilities.CreateAuthorityKeyIdentifier(X509Certificate)`；`SubjectKeyIdentifierStructure` 已标记过时，不要用。EKU 用 `KeyPurposeID.id_kp_serverAuth` / `id_kp_clientAuth`（小写 `id_` 前缀），不要用会触发 CS0618 的 PascalCase 过时属性。
- SAN 用 `GeneralNames(new[] { new GeneralName(GeneralName.DnsName, "localhost"), new GeneralName(GeneralName.IPAddress, "127.0.0.1") })` —— 常量名是 `DnsName`，不是 `DNSName`（§16 实测）。
- `BasicConstraints`：CA 用 `new BasicConstraints(0)`（`cA=true` + `pathLenConstraint=0`），叶子用 `new BasicConstraints(false)`；两者都设为 critical。
- 时间：BC 按 UTC 处理，传 `options.NotBefore.UtcDateTime`；生成前校验区间非空。
- DN：直接交给 `new X509Name(options.Subject)` 解析完整 DN；解析异常包装为 `ArgumentException`（保留 inner exception）。注意 BC 渲染顺序与输入相反：输入 `CN=localhost,O=SAEA` 时 .NET 侧显示 `O=SAEA, CN=localhost`（§16 实测），打印与 README 示例按实际显示写，别让人以为 CN 丢了。推导 SAN 时用 `X509Name.GetValueList(X509Name.CN)`，不要对 DN 字符串做切分。
- 打包：`Pkcs12StoreBuilder().Build()` → `SetKeyEntry("saea", new AsymmetricKeyEntry(privateKey), chainEntries)` → `Save(stream, pwd.ToCharArray(), random)` → **调用 `SaeaCertificateLoader` 的 internal `LoadPkcs12(byte[], password, flags)`** 加载（复用旧运行时回退），不要直接 `new X509Certificate2`。空口令实测可保存并读回（net8）。
- PEM 写出：`PemWriter` 写 `Org.BouncyCastle.X509.X509Certificate`（`CERTIFICATE`）；私钥写 `PrivateKeyInfoFactory.CreatePrivateKeyInfo(privateKey)`（命名空间 `Org.BouncyCastle.Pkcs`）—— **BC 2.6.2 实际输出 `RSA PRIVATE KEY`（PKCS#1），不是 PKCS#8**（§16 实测）。若确实需要 PKCS#8，得用 `new PemObject("PRIVATE KEY", keyInfo.GetEncoded())`；本期不要求，按 BC 默认写出即可。用 `StringWriter` 并把 `NewLine` 设为 `"\n"`、`UTF8Encoding(false)` 落盘，避免 CRLF / BOM 影响跨平台工具。
- 不再改 `SaeaCertificateLoader.ReadPrivateKey`：它已能读 PKCS#1 / PKCS#8（§16），加了分支也没有新的覆盖。允许的内部改动共四处：把 `byte[]` 版 `LoadPkcs12` 提到 `internal`；新增 `internal LoadPemWithFlags(...)`（公开 `FromPem` 仍走原 flags 与缓存）；`BcKeyConverter.GetPrivateKey` 的 ECDsa 分支改用 `DotNetUtilities.GetKeyPair(AsymmetricAlgorithm)`（见 §14 / §16.2，公开 API 不变）；`SAEA.Common.ConsoleHelper` 新增 `public static void Flush()`（见 §14 / §16.3，纯增量，不改既有重载行为）。
- 生成器内部把 BC 私钥与 `X509Certificate2` 成对持有；生成路径直接保存生成时的 BC 私钥，不要从 `X509Certificate2` 反推。
- `SaeaCertificateAuthority.FromPfx` / `FromPem` 复用已有 CA 时，私钥转换直接用同程序集的 `BcKeyConverter.GetPrivateKey(X509Certificate2)`，不要复制一份转换逻辑。**该路径必须用 `X509KeyStorageFlags.Exportable`（不带 `EphemeralKeySet`）加载**：实测在 Windows + .NET 8 上，`EphemeralKeySet | Exportable` 加载出的证书调用 `GetRSAPrivateKey().ExportParameters(true)` 会抛 `CryptographicException`（「不支持请求的操作。」），`BcKeyConverter` 因此失败；改用 `Exportable` 后正常。实现上 `LoadAuthorityFromPfx` 传 `Exportable`，`LoadAuthorityFromPem` 走新增的 internal `SaeaCertificateLoader.LoadPemWithFlags(...)`；其它加载路径仍用 `DefaultKeyStorageFlags`，公开行为不变。
- 内部写出逻辑（PFX / 证书 PEM / 私钥 PEM）抽一个 internal 帮助类，`SaeaCertificateBundle` 与 `SaeaCertificateAuthority` 共用，避免两套实现漂移。

**文件拆分（一个公开类型一个文件）**

- `Src/SAEA.Sockets/Ssl/SaeaCertificatePurpose.cs`
- `Src/SAEA.Sockets/Ssl/SaeaCertificateOptions.cs`
- `Src/SAEA.Sockets/Ssl/SaeaCertificateBundle.cs`
- `Src/SAEA.Sockets/Ssl/SaeaCertificateAuthority.cs`
- `Src/SAEA.Sockets/Ssl/SaeaCertificateFactory.cs`
- `Src/SAEA.Sockets/Ssl/Internal/SaeaCertificateWriter.cs`（internal，PFX / PEM 写出与 options 解析）
- 工具：`Src/SAEA.Sockets.SslTlsTool/Program.cs`、`SslTlsToolOptions.cs`（参数解析）、`CertificateVerifier.cs`（§9 自检）

## 11. 错误与边界

| 情形 | 行为 |
|---|---|
| `--out` 目录不存在 | 递归创建 |
| `--out` 指向已存在的文件 | 退出码 2，stderr 给出路径 |
| 自签目标文件已存在，且没有 `--force` | 跳过该文件，打印 `skipped`，其余继续 |
| `--reuse-ca` 的 `server.*` / `client.*` 已存在 | 无条件覆盖（复用模式的目的就是重签） |
| CA 模式下 `ca.*` 已存在且未给 `--force` / `--reuse-ca` | 报错退出（码 1），提示二选一，不静默重建 CA |
| `--reuse-ca` 与 `--force` 同时出现 | 参数错误，退出码 1 |
| `--reuse-ca` 但 `ca.*` 不存在 / 读取失败 / 非 CA | 退出码 2 |
| `--name` 为 `ca` / `server` / `client`，或含非法字符 / 路径分隔符 | 参数错误，退出码 1 |
| `--keysize` 非 2048/3072/4096 | 参数错误，退出码 1 |
| `--days` / `--ca-days` 不在 1..36500 | 参数错误，退出码 1 |
| 新叶子 `NotAfter` 晚于 CA 的 `NotAfter` | 参数错误，退出码 1 |
| `--ip` 任一值不是合法 IP | 参数错误，退出码 1 |
| `--dns` 任一值为空 / 含空白或逗号 / 超长 | 参数错误，退出码 1 |
| `--dns ""` / `--ip ""` | 参数错误，退出码 1（默认值请省略参数） |
| 未知参数 / 参数缺少值 | 参数错误，退出码 1，stderr 提示用法 |
| 无参数 | 进入交互模式，`exit` / `quit` / EOF 时退出码 0 |
| 交互模式单条命令失败 | 打印该次退出码并继续，进程最终退出码 0 |
| 交互模式引号未闭合 | 提示该行参数错误并继续 |
| `--help` 与其它参数并存 | 只打印帮助，退出码 0；交互模式打印后继续 |
| `--pwd ""` | 生成无口令 PFX（允许，但提示风险） |
| CA 证书没有私钥 | `SaeaTlsException("签发证书需要 CA 私钥。")`，工具退出码 2 |
| CA 证书不是 CA（`BasicConstraints.CA != true`） | `SaeaTlsException`，工具退出码 2 |
| `NotAfter <= NotBefore` | `ArgumentException`，工具退出码 1 |
| 文件写入失败（占用/无权限） | 退出码 2，stderr 给出路径 |
| `--verify` 任一项失败 | 退出码 2，打印失败项编号 |
| 全部文件跳过且 `--verify` 通过 | 退出码 3 |
| 全部文件跳过但 `--verify` 失败 | 退出码 2 |

## 12. 文档

- `Src/SAEA.Sockets/README.md` 与 `README.en.md`：在 Q3「如何配置 SSL/TLS 加密」之后加「开发用证书怎么来」小节，含两条工具命令、产物清单（明确 `.key` 是明文私钥）、`SaeaCertificateLoader.FromPfx` 读回示例、fullchain 拼接命令、CA 信任导入的三平台命令与移除方式、「仅限开发环境」警示。中英必须同步一致。
- 根 `README.md` 与 `README.en.md`（实测两份都存在）：测试/演示工程列表（Q6）补一行 `SAEA.Sockets.SslTlsTool`，中英同步。
- 公开 API 的 XML 注释按 `Src/SAEA.Common/IPHelper.cs` 体例写全 `summary` / `param` / `returns`；有异常路径的写 `<exception>`。
- 本篇设计文档即上游依据，勿在代码注释里写「见设计文档」之类溯源标记。
- 每个新增 `.cs` 文件（库与工具）都要带 AGENTS 规定的文件头 banner：`CLR版本` 按实际目标框架、`版本号` 按组件 `.csproj` 的 `<Version>`（工具写 `net10.0`）、`唯一标识` 每个文件独立生成、`修改标记` 写本次修改。`Src/*Test/` 的存量文件不回填。
- 工具工程的 `--help` 文本就是它自己的使用文档，改参数时同步改帮助，不留过期说明。

## 13. 验证与测试

- 构建：`dotnet build Src/SAEA.Sockets.sln -c Debug` 必须 0 error；库不得引入 C# 9+ 语法；工具工程是 net10.0，仍按同目录既有 `SAEA.P2PTest` 的写法走，不用 file-scoped namespace。
- 功能证据：`dotnet run --project Src/SAEA.Sockets.SslTlsTool -c Release -- --self-signed --ca --out <临时目录> --verify`，退出码 0，并核对文件清单与 §9 五项结果。
- 已存在分支：对同一临时目录只跑 `--self-signed`（不带 `--force`），确认全部 `skipped`、退出码 3；再对 `--ca` 不带 `--reuse-ca` / `--force` 跑一次，确认退出码 1。
- 复用分支：`--ca --reuse-ca` 在已有 CA 上重签 server/client，确认 `server.*` / `client.*` 被覆盖、CA 指纹不变、叶子指纹变化、`--verify` 通过（退出码 0）。
- 参数分支：`--keysize 1024`、`--days 0`、`--dns ""`、`--ip not-an-ip`、`--name ca`、`--reuse-ca --force` 各跑一次，确认退出码 1。
- 交互分支：无参数启动后依次输入 `help`、`--bogus`、`--self-signed --out "<带空格目录>" --verify`、`--cn "CN=localhost, O=SAEA"`、`exit`，确认 `help` 打印完整帮助、非法命令只打印退出码 1 后继续、引号内带空格的目录与 DN 被当成单个取值、进程最终退出码 0；再用管道把 `--self-signed --verify` + `exit` 喂给无参数进程，确认同样可用。
- 为什么没有 MSTest：仓库四个 MSTest 工程（Common / MQTT / FTP / Sockets.Pipeline）都不覆盖 `SAEA.Sockets` 的 Ssl 目录，新建 `SAEA.Sockets.Tests` 超出本期已确认范围。本期用 `--verify` 自检提供可复现证据；若后续要回归保护，再单独立项建测试工程（这是已知的缺口，不是遗漏）。
- 四个既有 MSTest 工程仍要跑一遍（`dotnet test` 各自工程），确认没有连带破坏：Common / MQTT / FTP / Sockets.Pipeline。本期不改 loader 公开行为、不改 `WithSsl`，预期全绿。
- net462 冒烟（一次性，不进仓库）：本机已确认存在 net462 targeting pack（`C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2`，另有 4.7.2 / 4.8 / 4.8.1）。在系统临时目录建一个 net462 控制台工程，用 `ProjectReference` 指向 `Src/SAEA.Sockets/SAEA.Sockets.csproj`（自动带 BouncyCastle / Pipelines 传递依赖，不要裸引用 dll 再手补依赖），执行「生成自签 + PFX 读回 + PEM 读回 + 空口令 PFX 读回 + 复用 CA（`FromPfx` / `FromPem` 后重签叶子）」五件事，贴出输出。若运行时仍不满足，记录「环境原因：缺 net462 运行时」，报告写「net462 未运行」，不放宽 G5 表述。实测结果见 §16.2。

## 14. 风险

- BouncyCastle 2.6.2 的生成 API 已实测：`Generate` 只有单参重载、`PemWriter` 输出 PKCS#1、`SubjectKeyIdentifierStructure` 过时（见 §16）。net462 的 `EphemeralKeySet` 回退、空口令 PFX、CA 复用均已冒烟通过（§16.2）。
- **实测纠正**：先前认为「`Exportable` 标志能保证私钥可导出」，在 `EphemeralKeySet` 下不成立。Windows + .NET 8.0.14 上，用 `EphemeralKeySet | Exportable` 加载由 BouncyCastle 写出的 CA PFX，`HasPrivateKey` 为 true，但 `GetRSAPrivateKey().ExportParameters(true)` 抛 `CryptographicException: 不支持请求的操作。`，`BcKeyConverter.GetPrivateKey` 因而落到「不支持的证书私钥类型」，`--ca --reuse-ca` 必然退出码 2。改用 `X509KeyStorageFlags.Exportable`（非临时密钥集）后正常，CA 指纹不变、叶子可重签。见 §10、§15 #33。
- CA 复用路径改用非临时密钥集后，私钥会短暂落到当前用户的密钥容器，`Dispose` 时释放；这是复用能力可用的代价，其它加载路径不受影响。
- net462 上 `EphemeralKeySet` 不生效时会回退到默认 `X509KeyStorageFlags`，私钥可能落到当前用户密钥容器且不一定可导出。CA 复用路径已固定用 `Exportable`，实测 net462 可导出私钥并完成复用（§16.2）；生成与 PFX/PEM 读回也已通过。剩余风险是密钥容器留痕与清理，属运行时行为，本库不额外处理。
- **实测纠正**：BouncyCastle 2.6.2 的 net461 资产没有 `DotNetUtilities.GetECDsaKeyPair(ECDsa)` 重载（只有 `GetKeyPair(AsymmetricAlgorithm)`、`GetRsaKeyPair`、`GetDsaKeyPair`）。`BcKeyConverter.GetPrivateKey` 的方法体同时引用 RSA 与 ECDsa 两个重载，在 net462 上 JIT 编译整个方法即抛 `MissingMethodException`，连 RSA 分支都走不到。ECDsa 分支改用 `GetKeyPair(AsymmetricAlgorithm)` 后 net462 冒烟全部通过。方法本身只在证书生成/复用路径被调用，不影响既有 TLS 握手。
- 系统信任库导入需要管理员/用户授权，且会在测试机上留痕。文档要求只导入当前用户或测试机，并给出移除命令；`CurrentUser\Root` 只对当前账号生效，服务账号需单独处理。
- 生成的 CA 私钥是开发资产，泄露等于签发权泄露。文档与工具输出都要提示不要把 `ca.key` / `ca.pfx` 提交进仓库；`.key` 是明文私钥。仓库 `.gitignore` 本期补 `*.key` 与 `certs/`（`*.pfx` 已有）。
- 中文或特殊字符 DN 在不同工具解析上有差异，本期按 `X509Name` 原样处理，不做额外转义保证；DN 显示顺序会被 BC 反转（§16），文档按实际显示写。
- `--verify` 第 5 项刻意容忍链错误，真正的链证据只在第 3 项；两者不能互相替代，输出必须区分。
- 工具的输出走 `SAEA.Common.ConsoleHelper`（后台线程异步写控制台，并镜像到临时目录日志）。这类队列在进程退出时会丢掉未写出的行，实测 `--help` 只打出 11–17 行（正文约 45 行）。因此 `ConsoleHelper` 新增 `public static void Flush()`（按待写计数等待，最多 2 秒），工具在 `Main` 的 `finally` 里调用。任何短生命周期程序复用 `ConsoleHelper` 都必须自己 Flush，否则同样丢输出。

## 15. 我替你做过的决定（可逐条否决）

1. 类型命名 `SaeaCertificateFactory` / `SaeaCertificateOptions` / `SaeaCertificateBundle` / `SaeaCertificateAuthority`，与 `SaeaCertificateLoader` 同前缀。
2. CA 句柄 `Create` 后返回可签发对象，而不是让用户自己管理 `AsymmetricKeyParameter`；公开签名完全不出现 BC 类型。
3. 返回 `SaeaCertificateBundle`（证书 + 保存方法），而不是「只返回 `X509Certificate2`，保存另给静态方法」。
4. PEM 私钥按 BC 默认写出（实测是 PKCS#1 `RSA PRIVATE KEY`），**不改** `SaeaCertificateLoader.ReadPrivateKey`——它已经能读 PKCS#1 与 PKCS#8（§16）。
5. 工具无参数时进入交互模式（逐行执行与命令行同构的参数），而不是打印帮助后退出；交互只改变输入方式，不改变任何参数语义与单次命令的退出码。
6. 默认口令 `saea`、leaf 有效至 30 天后、CA 有效至 3650 天后、RSA 2048、SAN 为 `localhost` + `127.0.0.1`。
7. CA 模式固定文件名 `ca.*` / `server.*` / `client.*`，不支持 `--name` 改这三个。
8. `--verify` 第 5 项客户端用 `CertificateValidationCallback` 断言主机名匹配、容忍链错误；服务端不设 `AllowInvalidCertificates`，链校验单独用 `CustomRootTrust` 覆盖。
9. 工具工程名按 `Tls` 修正（原始需求写作 `SslTslTest`）。
10. CA 也提供 `SavePfx` / `SaveCertificatePem` / `SavePrivateKeyPem`（与 bundle 共用内部写出逻辑），工具因此不需要第二套 PEM/PFX 写出代码。
11. 不扩展 `SaeaCertificateLoader` / `BcKeyConverter` 去把 PFX 里的 CA 链读回并随握手发送：CA 进信任库才是本库补链的正途，改 loader 公开行为超出范围。此为已知限制，README 要写明。
12. `--cn` 只作用于服务端叶子与自签证书；CA 与客户端主题各自有 `--ca-cn` / `--client-cn`。SAN 推导统一走 §6.1。
13. net462 只做一次性临时工程冒烟，不往仓库加任何 4.6.2 工程。
14. 增加 `SaeaCertificateAuthority.FromPfx` / `FromPem` 与 CLI `--reuse-ca`：没有这个入口，重跑工具会换掉 CA，已导入的信任全部作废。
15. CA 模式在 `ca.*` 已存在时默认报错，不静默重建；要重建必须显式 `--force`。
16. 退出码全模式统一：`0` 有新文件产出、`1` 参数错误、`2` 生成/读取/自检失败、`3` 一个文件都没写但自检通过（优先级 `1 > 2 > 0/3`）。
17. `--ip` 同时支持 IPv4 与 IPv6（实测 `GeneralName(GeneralName.IPAddress, "::1")` 会编码成 16 字节，正确）。
18. 本期补仓库 `.gitignore` 的 `*.key` 与 `certs/`（现有规则只忽略 `*.pfx`）；不加 `*.crt`，证书是公开物。
19. SAN 推导在库层实现（`DnsNames == null` → CN + `localhost`），CLI 不另写一套；这样库与工具的默认行为一致。
20. `FromPem` 的 `password` 同时是 PEM 私钥口令与默认 PFX 口令，沿用 loader 既有语义。
21. `SavePfx` 对 CA 签发的叶子写 leaf + CA 链；所有 PEM 方法只写单张证书 / 单个私钥。
22. `SavePfx(string path, string password = null)` 允许覆盖创建时的口令；`null` = 用默认口令，`""` = 无口令。
23. 工具输出不回显用户显式传入的 PFX 口令，只打印状态。
24. `--verify` 用 `TcpListener(IPAddress.Loopback, 0)` 取空闲端口；绑定失败先 `Stop()` 再重试，最多 3 次。
25. `--reuse-ca` 与 `--force` 互斥；`--reuse-ca` 必须与 `--ca` 同时出现，且复用模式无条件覆盖 `server.*` / `client.*`。
26. `--name` 禁止使用 `ca` / `server` / `client` 保留名，并限制字符集与长度；未给 `--cn` 时 `--name` 同时作为 CN。
27. 有效期语义按「有效至 n 天后」映射：`NotBefore = UtcNow.AddDays(-1)`，`NotAfter = UtcNow.AddDays(days)`；文档与帮助不再写「有效期 n 天」。
28. 序列号用 `new BigInteger(127, random).SetBit(127)`，保证 128 位正非零。
29. `KeySize == 0` 视为未赋值（2048），不再当成非法值。
30. `PfxPassword == null` → 默认 `saea`；`""` → 无口令。
31. 新增 `--ca-pwd`（复用 CA 的读取口令，默认取 `--pwd`），避免「旧 CA 口令与新叶子口令必须相同」的硬绑定。
32. `--verify` 第 5 项失败不降级、不自动放宽校验；跨平台不支持按缺陷单独立项。
33. CA 复用路径用 `X509KeyStorageFlags.Exportable` 而非 `DefaultKeyStorageFlags` 加载：临时密钥集下私钥不可导出，`BcKeyConverter` 会失败（实测，见 §14）。为此给 `SaeaCertificateLoader` 增加 internal 的 `LoadPemWithFlags`，公开 `FromPem` 行为不变。
34. `BcKeyConverter.GetPrivateKey` 的 ECDsa 分支改用 `DotNetUtilities.GetKeyPair(AsymmetricAlgorithm)`：net461 资产没有 `GetECDsaKeyPair(ECDsa)`，否则 net462 上 JIT 即失败（§16.2）。
35. `SAEA.Common.ConsoleHelper` 增加 `public static void Flush()`：工具输出走它的异步队列，进程退出前必须等待写完，否则 `--help` 等立即退出路径丢行（§16.3）。纯增量 API，不改既有重载行为。
36. 自签模式的 `--verify` 第 5 项用同一张自签证书同时充当服务端与客户端证书：自签模式没有 CA 与独立客户端证书，原先按 CA 模式的 `server.pfx` / `client.pfx` 取值会拿到 null 并抛 `ArgumentNullException`（§16.3）。

## 16. 实测记录（2026-11-06）

用于核实第 10 节的 API 断言，探针工程放在系统临时目录（不进仓库）：一个 `netstandard2.0` + `LangVersion 8.0` 的类库引用 `BouncyCastle.Cryptography 2.6.2` 生成 CA 与叶子证书，一个 `net8.0` 控制台引用该库与 `SAEA.Sockets.csproj` 做读回与链校验。

命令：

```
dotnet run --project Runner/Runner.csproj -c Release
```

输出（节选，原文照抄）：

```
[PEM] headers: -----BEGIN CERTIFICATE----- -----BEGIN RSA PRIVATE KEY-----
[PKCS8] headers: -----BEGIN PRIVATE KEY-----
[SAN] 2:localhost|7:127.0.0.1
[IPv6 SAN] ok tag=7 value=#00000000000000000000000000000001 raw=87-10-00-...-01
[empty pwd pfx] ok hasKey=True
[PFX] subject=O=SAEA, CN=localhost hasKey=True
[PEM loader, traditional key] subject=O=SAEA, CN=localhost hasKey=True
[PEM loader, PKCS8 key] subject=O=SAEA, CN=localhost hasKey=True
[chain default+ExtraStore] ok=False count=2 flags=UntrustedRoot
[chain CustomRootTrust] ok=True count=2
[chain AllowUnknownCA, no extra] ok=True count=1 flags=PartialChain
[BcKeyConverter-like, no extra] ok=True count=1 flags=PartialChain
[pfx collection count] 2
   - CN=SAEA Dev Root CA hasKey=False
   - O=SAEA, CN=localhost hasKey=True
```

结论：

| 断言 | 实测结果 | 对设计的影响 |
|---|---|---|
| `X509V3CertificateGenerator.Generate(ISignatureFactory, AsymmetricKeyParameter)` 可用 | **编译失败** CS7036，只有 `Generate(ISignatureFactory)` 与三参备用签名重载 | §10 已改为单参重载 |
| `PemWriter.WriteObject(PrivateKeyInfo)` 输出 PKCS#8 `PRIVATE KEY` | 实际输出 `RSA PRIVATE KEY`（PKCS#1） | §7/§10 已改为按实际输出描述 |
| 现有 `FromPem` 读不了 PKCS#8 | **能读**，`HasPrivateKey=True` | 删掉「补 `ReadPrivateKey` 分支」的改动 |
| `FromPfx` 只承载叶子 | 是；`X509Certificate2Collection.Import` 能看到 CA + 叶子 2 条 | §5/§7 的限制描述成立 |
| CA 放 `ExtraStore` 就能通过客户端校验 | 否，`UntrustedRoot` | 「必须导入信任库」成立 |
| `CustomRootTrust` 能验证 CA 链 | 是，`ok=True` | §9 第 3 步成立（仅工具可用） |
| 服务端 mTLS 需要关掉校验才能接受未知根 | 否，`AllowUnknownCertificateAuthority` 下 `Build` 返回 true | §9 第 5 步服务端不用开 `AllowInvalidCertificates` |
| `GeneralName(GeneralName.DNSName, ...)` 可用 | **不存在**，常量名是 `DnsName` | §10 已修正 |
| IPv6 SAN 可用 | 是，编码为 16 字节 `::1` | §15 #17 |
| 空口令 PFX 在 net8 可读 | 是 | net462 已冒烟通过（§16.2） |
| DN 显示顺序 | 输入 `CN=localhost,O=SAEA`，显示 `O=SAEA, CN=localhost` | §10 已注明 |

环境：`dotnet` SDK 10.0.403，Windows；net462 / 4.7.2 / 4.8 / 4.8.1 targeting pack 均存在。

### 16.1 复用 CA 的加载标志复测

`--ca --reuse-ca` 首次实现后实测退出码 2，定位到加载标志。同一份 `ca.pfx`、同一口令，用独立探针比对：

| 加载标志 | `HasPrivateKey` | `rsa.ExportParameters(true)` | `DotNetUtilities.GetRsaKeyPair(rsa)` |
|---|---|---|---|
| `EphemeralKeySet \| Exportable`（原默认） | true | `CryptographicException: 不支持请求的操作。` | 失败 |
| `Exportable`（非临时密钥集） | true | 成功 | 成功 |

改为 `Exportable` 后：`--ca --reuse-ca --verify` 退出码 0，`ca.crt` 哈希不变，`server` / `client` 指纹变化，`--verify` 5/5 通过。环境同上（Windows + .NET 8.0.14 运行时）。

### 16.2 net462 冒烟与 BouncyCastle 资产差异

一次性 net462 控制台工程（`ProjectReference` 指向 `Src/SAEA.Sockets`，不进仓库）执行结果：

```
[pfx] hasKey=True subject=CN=localhost
[pem] hasKey=True subject=CN=localhost
[empty pwd pfx] hasKey=True
[ca-issued leaf] hasKey=True subject=CN=localhost
[reuse pfx] ca=CN=SAEA Dev Root CA
[reuse issue] ok subject=CN=saea-client
[reuse pem] ca=CN=SAEA Dev Root CA
```

首次运行在「复用 CA」两项失败：

```
MissingMethodException: 找不到方法:
Org.BouncyCastle.Security.DotNetUtilities.GetECDsaKeyPair(System.Security.Cryptography.ECDsa)
```

定位：net462 应用加载的是 BouncyCastle 的 **net461 资产**（`BouncyCastle.Cryptography, Version=2.0.0.0`），反射可见其 `DotNetUtilities` 只有 `GetDsaKeyPair`、`GetRsaKeyPair`、`GetKeyPair(AsymmetricAlgorithm)`，没有 `GetECDsaKeyPair(ECDsa)`；而库是按 netstandard 资产编译的。`BcKeyConverter.GetPrivateKey` 同时引用两个重载，JIT 整个方法时即失败，RSA 分支未执行。同一进程内直接调用 `GetRsaKeyPair(rsa)` 是成功的，可排除私钥导出问题。

修复：ECDsa 分支改用 `DotNetUtilities.GetKeyPair(ecdsa)`（两个资产都有此重载），公开 API 与既有 TLS 路径不受影响；修复后上表全部通过。`GetPrivateKey` 全仓库仅被证书生成的 `SaeaCertificateWriter` 调用。

### 16.3 场景矩阵复测与两处缺陷（2026-11-06 14:32）

最终审查阶段按 §8 / §11 的退出码契约逐条复跑，均在临时目录下执行：

| 场景 | 期望 | 实测 |
|---|---|---|
| `--self-signed --verify`（新目录） | 0 | 0 |
| 重复 `--self-signed`（文件已存在） | 3 | 3 |
| `--self-signed --ca --verify`（新目录） | 0 | 0 |
| `--ca`（新目录） | 0 | 0 |
| `--ca --reuse-ca --verify` | 0 | 0 |
| `--ca --reuse-ca`（空目录） | 2 | 2 |
| `--ca --force --verify` | 0 | 0 |
| `--ca`（`ca.*` 已存在、无 flag） | 1 | 1 |
| `--ca --reuse-ca --force` | 1 | 1 |
| `--reuse-ca`（单独） | 1 | 1 |
| `--bogus` | 1 | 1 |
| `--self-signed --days 0` | 1 | 1 |
| 无参数（进入交互，EOF 退出） | 0 | 0（改名后复测为交互模式） |
| `--out` 指向已存在文件 | 2 | 2 |
| `--ca --reuse-ca --ca-pwd wrong` | 2 | 2 |

过程中发现并修复两处缺陷：

1. **`--help` 丢输出**：输出改由 `ConsoleHelper` 的异步队列写出后，`Main` 一返回进程就退出，队列里未写出的行被丢弃。同一命令连跑 3 次分别只打出 17 / 11 / 14 行（正文约 45 行）。修复：`ConsoleHelper.Flush()` + 工具 `Main` 的 `finally` 调用；修复后连跑 3 次均为 61 行（含 banner 与空行），帮助尾部示例完整。
2. **自签模式 `--verify` 必然失败**：第 5 项固定读 `server.pfx` / `client.pfx`，自签模式这两项为 null，`SaeaCertificateLoader.FromPfx(null, ...)` 抛 `ArgumentNullException: Value cannot be null. (Parameter 'path')`，退出码 2。此前只验证过 `--self-signed --ca` 组合，漏了单独跑自签。修复：自签模式把同一张证书同时登记为服务端与客户端证书；修复后 5/5 通过。

顺带：`--verify` 第 3 项加载 CA 改用 `X509CertificateLoader.LoadCertificate(bytes)`，消除 .NET 10 的 `SYSLIB0057` 警告。

### 16.4 工具改名与交互模式（2026-11-06 14:42）

- 工程目录、`.csproj`、命名空间、程序集名统一由 `SAEA.Sockets.SslTlsTest` 改为 `SAEA.Sockets.SslTlsTool`；选项类型同步改为 `SslTlsToolOptions`，文件名改为 `SslTlsToolOptions.cs`；sln 只改工程名与路径，项目 GUID 不变。
- 无参数启动改为交互模式：逐行读取，命令与 CLI 参数完全同构，额外识别 `help` / `exit` / `quit`（不区分大小写）；双引号可包住含空格的取值，引号内只把 `\"` 当转义、反斜杠本身原样保留；引号未闭合按参数错误提示后继续。交互模式下单条命令失败只打印该次退出码，进程最终退出码固定为 0。
- 改名后按 §8 / §11 完整复跑（临时目录，`dotnet run --no-build`），全部符合预期：

| 场景 | 期望 | 实测 |
|---|---|---|
| CLI `--self-signed --verify`（新目录） | 0 | 0 |
| 重复 `--self-signed`（已存在） | 3 | 3 |
| CLI `--ca --verify`（新目录） | 0 | 0 |
| `--ca`（`ca.*` 已存在） | 1 | 1 |
| `--ca --reuse-ca --verify` | 0 | 0 |
| `--ca --reuse-ca --force` | 1 | 1 |
| `--reuse-ca`（单独） | 1 | 1 |
| `--bogus` | 1 | 1 |
| `--self-signed --days 0` | 1 | 1 |
| `--ca --reuse-ca`（空目录） | 2 | 2 |
| `--out` 指向已存在文件 | 2 | 2 |
| `--ca --reuse-ca --ca-pwd wrong` | 2 | 2 |
| `--ca --force --verify`（新目录） | 0 | 0 |
| `--self-signed --ca --verify`（新目录） | 0 | 0 |
| 无参数 + EOF | 0 | 0 |
| 交互 `--bogus` 后 `exit` | 进程 0，打印单次 1 | 0，打印「退出码：1」 |
| 交互 `help` 后 `quit` | 打印完整帮助后退出 0 | 0，帮助含交互说明与退出码 |
| 交互 `--cn "CN=localhost, O=SAEA"` | 解析为单个取值 | 主题输出 `O=SAEA, CN=localhost` |
| 交互 `--self-signed --out "<含空格目录>" --verify` | 0，1/5..5/5 通过 | 0，5/5 通过，目录生成 3 个文件 |
| 交互引号未闭合 | 提示并继续 | 提示「命令里的双引号没有闭合。」后继续，进程 0 |

- 同轮发现 `SslTlsToolOptions.cs` 的 banner `唯一标识` 与 `GlobalUsing.cs` 重复，已为选项文件重新生成 GUID；banner 的 `命名空间` / `文件名` / `修改标记` 同步改名。