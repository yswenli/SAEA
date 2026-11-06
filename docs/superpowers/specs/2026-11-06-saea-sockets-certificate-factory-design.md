# 开发用证书生成器（SaeaCertificateFactory + SslTlsTest 工具）设计

- 日期：2026-11-06
- 状态：待确认
- 范围：`SAEA.Sockets` 新增公开生成 API；新增独立控制台工具工程 `SAEA.Sockets.SslTlsTest`

## 1. 为什么做

`WithSsl(...)` 需要 PFX 或 PEM 文件，但仓库只提供读取能力（`SaeaCertificateLoader`），没有生成能力。开发者在跑 TLS 自测、用 `openssl s_client` / curl / 浏览器连自己的 SAEA 服务端时，手边没有证书，只能临时敲 OpenSSL 命令：命令行长、平台差异大、SAN 常常漏掉 IP、口令与有效期每次都不一样。

`SAEA.P2PTest` 内部的 `TlsTest.CreateCertificate` 是内存自签，只够测试自己用：外部看不到文件，也无法给其他语言或工具使用。

所以需要两个东西：库内可编程的生成 API，和一个能一条命令产出全套文件的控制台工具。

## 2. 目标

- G1：`SAEA.Sockets.Ssl` 暴露易用的生成 API，支持两种模式——单张自签证书、本地根 CA 加由其签发的服务端/客户端证书。
- G2：新增控制台工具 `Src/SAEA.Sockets.SslTlsTest/`，一条命令产出上述文件，支持 PowerShell/bash/CI 脚本化调用，不依赖交互。
- G3：产物能被 `SaeaCertificateLoader.FromPfx` / `FromPem` 读回，`WithSsl` 现有示例无需修改。
- G4：默认值贴合本仓库自测场景（`localhost` + `127.0.0.1`、口令 `saea`），同时覆盖 mTLS（服务端要求客户端证书）。
- G5：纯 BouncyCastle 实现，目标框架仍是 `netstandard2.0`、C# 8，`.NET Framework 4.6.2` 消费方在运行时也能调用。

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

## 5. 现状约束（实现必须遵守）

- `SAEA.Sockets.csproj`：`netstandard2.0`、`LangVersion 8.0`、已引用 `BouncyCastle.Cryptography 2.6.2`、`System.IO.Pipelines`。
- `SaeaCertificateLoader` 打包 PKCS#12 后统一走 `new X509Certificate2(bytes, password, DefaultKeyStorageFlags)`；`DefaultKeyStorageFlags = EphemeralKeySet | Exportable`。生成器复用同一条路径，保证「自己写的能被自己读回」。
- `SaeaCertificateLoader.ReadPrivateKey` 只识别 `AsymmetricCipherKeyPair` 与 `AsymmetricKeyParameter`，但 BC 的 `PemReader` 对 `RSA PRIVATE KEY`（PKCS#1）和 `PRIVATE KEY`（PKCS#8）都会返回它认识的私钥对象。**实测两种格式 `FromPem` 均能读回且 `HasPrivateKey == true`（§16）**，因此本期不改 `ReadPrivateKey`，也不要求生成器写 PKCS#8。
- `SaeaCertificateLoader.FromPfx` 得到的是叶子证书：PKCS#12 里就算带了 CA，`new X509Certificate2(bytes, pwd)` 也只给叶子（实测：`X509Certificate2Collection.Import` 能看到 CA + 叶子两条，但 `FromPfx` 只有叶子）。
- 服务端 mTLS 校验走 `ValidateChain(..., allowUnknownRoot: true, ...)`，且 `VerificationFlags.AllowUnknownCertificateAuthority` 下 `X509Chain.Build` 对「只有叶子、CA 不在信任库」的情况返回 **true**（状态仍是 `PartialChain`，实测见 §16）。所以回环 mTLS 自检不需要导入信任库，也不需要把服务端设成 `AllowInvalidCertificates = true`；但**这不能当作「链校验通过」的证据**，链校验证据只能来自 `CustomRootTrust`。
- `SaeaCertificateValidator.ValidateServerCertificate` 走 `ValidateChain(..., allowUnknownRoot: false, ...)`，`ExtraStore` 只放握手带来的中间证书。因此 CA 必须进系统信任库，客户端才能在 `AllowInvalidCertificates = false` 下验证通过；实测「CA 只放 ExtraStore」仍然是 `UntrustedRoot`（§16）。工具必须把这个前提讲清楚，不能暗示「生成完就自动可信」。
- `SaeaCertificateValidator.MatchHostName`：证书有 SAN 时不再回退 CN，IP 只认 `iPAddress` SAN。默认产物必须同时写 `dNSName` 和 `iPAddress`。
- 仓库没有 tools 工程先例；`Src/<组件>/` 是一个目录一个 NuGet 包，`Src/*Test/` 是可运行演示。新工程按 `*Test` 惯例放 `Src/SAEA.Sockets.SslTlsTest/`，并加入 `Src/SAEA.Sockets.sln`（sln 里同时有 net462 老工程，新增 net8.0 工程不影响它们）。

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
    public string[] DnsNames { get; set; }            // 服务端默认 { "localhost" }
    public string[] IpAddresses { get; set; }         // 服务端默认 { "127.0.0.1" }
    public int KeySize { get; set; }                  // 默认 2048
    public DateTimeOffset NotBefore { get; set; }     // 默认 UtcNow.AddDays(-1)
    public DateTimeOffset NotAfter { get; set; }      // leaf 默认 +30 天；CA 默认 +3650 天
    public string PfxPassword { get; set; }           // 默认 "saea"
}

/// <summary>证书与其私钥，附带保存能力</summary>
public sealed class SaeaCertificateBundle : IDisposable
{
    public X509Certificate2 Certificate { get; }
    public void SavePfx(string path);                                  // 口令取 options.PfxPassword
    public void SaveCertificatePem(string path);                       // CERTIFICATE, PEM, LF 换行
    public void SavePrivateKeyPem(string path);                        // BC 默认输出 RSA PRIVATE KEY（PKCS#1），实测见 §16
    public void Dispose();
}

/// <summary>本地根 CA：持有私钥，用于签发叶子证书</summary>
public sealed class SaeaCertificateAuthority : IDisposable
{
    public X509Certificate2 Certificate { get; }

    public static SaeaCertificateAuthority Create(SaeaCertificateOptions options);
    public static SaeaCertificateAuthority FromPfx(string path, string password = null);   // 复用已有 CA，避免 --force 换 CA
    public static SaeaCertificateAuthority FromPem(string certificatePath, string privateKeyPath, string password = null);
    public SaeaCertificateBundle IssueServerCertificate(SaeaCertificateOptions options);
    public SaeaCertificateBundle IssueClientCertificate(SaeaCertificateOptions options);
    public void SavePfx(string path);                                  // 同 bundle 的三个保存方法，共用内部写出逻辑
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

- `CreateSelfSigned`：issuer == subject，自签；用途为 `Server` 时写 SAN 与 `serverAuth`，为 `Client` 时写 `clientAuth` 且不写 SAN。`Purpose = CertificateAuthority` 时抛 `ArgumentException`（根证书走 `SaeaCertificateAuthority.Create`）。
- `SaeaCertificateAuthority.Create`：签发 `CA:TRUE`（`pathLenConstraint = 0`）、`keyCertSign` + `cRLSign`（critical）、SKI 的根证书；`options.Purpose` 被忽略（强制 CA 语义），`DnsNames` / `IpAddresses` 被忽略。
- `FromPfx` / `FromPem`：复用已有 CA 证书与私钥；证书必须 `HasPrivateKey` 且 `BasicConstraints.CertificateAuthority == true`，否则抛 `SaeaTlsException`。这条入口是必需项：否则每次重跑工具都会换 CA，已导入的信任作废。
- `Issue*`：issuer 取 CA 的 Subject DN，签名用 CA 私钥；叶子 `CA:FALSE`、`keyUsage = digitalSignature | keyEncipherment`、EKU 按用途、写 AKI（`X509ExtensionUtilities.CreateAuthorityKeyIdentifier(caCertificate)`）与 SKI、服务端叶子写 SAN；`IssueClientCertificate` 忽略 `DnsNames` / `IpAddresses`。
- 默认值矩阵（`SaeaCertificateOptions` 未赋值时）：
  - `Purpose` = `Server`（枚举零值）。
  - `Subject` = `CN=localhost`（Server）/ `CN=saea-client`（Client）/ `CN=SAEA Dev Root CA`（CA）。
  - `DnsNames` = `{ "localhost" }`（仅 Server 生效）；`IpAddresses` = `{ "127.0.0.1" }`（仅 Server 生效）。
  - `KeySize` = 2048；`PfxPassword` = `"saea"`；`NotBefore` = `UtcNow.AddDays(-1)`；`NotAfter` = 叶子 `NotBefore.AddDays(31)`、CA `NotBefore.AddDays(3651)`（保证默认跨度为 30 / 3650 天）。
  - `DnsNames` / `IpAddresses` 显式设为空数组表示「不写该类 SAN」；设为 `null` 走上面的默认值。
- 签名算法统一 `SHA256WITHRSA`。
- 序列号：128 位随机正整数，每次生成不同（实测可生成）。
- `KeySize` 只接受 2048/3072/4096，其它值抛 `ArgumentException`。
- `NotAfter <= NotBefore` 抛 `ArgumentException`。
- `PfxPassword` 允许为空字符串（生成无口令 PKCS#12），但默认 `saea`；net8 实测空口令可读，net462 待冒烟确认（见 §13）。
- 返回对象持有证书私钥，调用方负责 `Dispose`；`SaeaCertificateAuthority` 内部缓存 CA 私钥，`Dispose` 后调用 `Issue*` 抛 `ObjectDisposedException`。
- 工厂与 CA 句柄都不保证线程安全，文档写明「生成阶段单线程使用」。
- 错误统一抛 `SaeaTlsException`（已有类型），参数错误抛 `ArgumentException`，文件写入错误保留原始 `IOException`。

## 7. 产物文件布局

工具默认输出目录 `./certs`（相对当前工作目录）。

自签模式（基名默认 `localhost`，可用 `--name` 覆盖）：

```
certs/localhost.pfx        PKCS#12，含私钥，口令 saea
certs/localhost.crt        PEM 证书（CERTIFICATE）
certs/localhost.key        PEM 私钥（BC 默认输出 RSA PRIVATE KEY，即 PKCS#1；实测 §16）
```

CA 模式：

```
certs/ca.pfx      certs/ca.crt      certs/ca.key
certs/server.pfx  certs/server.crt  certs/server.key     ← SAN: localhost + 127.0.0.1
certs/client.pfx  certs/client.crt  certs/client.key     ← EKU clientAuth，无 SAN
```

- 服务端 PFX 的 PKCS#12 条目带上 CA 证书（key entry 的证书数组为 leaf + CA），主题与私钥仍是叶子。这个链是给 `openssl pkcs12`、curl、Java/Node 等外部工具用的。
- **本库不读回链**：`SaeaCertificateLoader.FromPfx` 最终是 `new X509Certificate2(bytes, pwd, flags)`，只承载叶子；服务端下发链走 `BcKeyConverter.ToCertificateChain` → `X509Chain`，从系统信任库补。因此 CA 未导入信任库时，SAEA 服务端实际只发叶子——这是本期已知限制，不改 `SaeaCertificateLoader` / `BcKeyConverter`（改了属公开行为变更，超出已确认范围）。README 的信任导入说明必须把这个前提写清楚。
- 叶子文件命名可用 `--name` 改基名（只影响自签模式；CA 模式固定 `ca`/`server`/`client`，避免脚本猜名字）。

## 8. 工具契约（`Src/SAEA.Sockets.SslTlsTest/`）

- 工程：`net8.0`、`OutputType=Exe`、无 `Version`、`ProjectReference` 到 `SAEA.Sockets`；文件头 banner 的 `CLR版本` 写 `net8.0`，`版本号` 写 `net8.0`（测试/演示工程规则）。
- 无参数运行 → 打印帮助并 `exit 0`（工具要能被脚本直接调用，不用仓库其它 Test 工程的交互菜单）。

```
--self-signed            生成单张自签证书
--ca                     生成 CA + server + client
--out <dir>              输出目录，默认 ./certs
--name <base>            自签模式文件基名，默认 localhost
--cn <subject>           服务端叶子主题 DN，默认 CN=localhost；CA 模式只影响 server
--ca-cn <subject>        CA 模式：CA 主题 DN，默认 CN=SAEA Dev Root CA
--client-cn <subject>    CA 模式：客户端主题 DN，默认 CN=saea-client
--dns <a,b>              服务端 DNS SAN，默认 localhost
--ip <a,b>               服务端 IP SAN，默认 127.0.0.1
--days <n>               叶子有效期，默认 30
--ca-days <n>            CA 有效期，默认 3650
--keysize <n>            2048|3072|4096，默认 2048
--pwd <password>         PFX 口令，默认 saea
--reuse-ca               复用 <out>/ca.pfx（或 ca.crt+ca.key）签新叶子，不重建 CA
--force                  覆盖已存在文件；缺省时已存在则跳过并提示
--verify                 生成后自检（见 §9）
--help
```

- `--self-signed` 与 `--ca` 可同时给，两种都生成。
- `--ca` 在 `ca.*` 已存在且未给 `--force` 时不会静默重建 CA：直接报错并提示「要复用请加 `--reuse-ca`，要重建请加 `--force`」。这是硬性要求——重建 CA 会让所有已导入的信任作废。
- SAN 自动补齐：未显式给 `--dns` 时，若 `--cn` 里带了主机名（取 `CN=` 后到第一个逗号为止，且不是 IP 字面量），把它加在默认 `localhost` 之前，避免「改了 CN 但 SAN 不含它」导致主机名校验必然失败（对应 §4 反目标）。给了 `--dns` 就完全按用户给的来，不再自动补。
- `--name` 只影响自签模式文件基名，不影响证书内容。
- 仓库现有 `.gitignore` 只忽略 `*.pfx`，不忽略 `*.key` / `*.crt`。本期必须补 `certs/`（或 `*.key`），否则默认输出目录里的 `ca.key` 有被误提交的真实风险。
- 输出：每个文件的完整路径、证书主题、SHA-1 指纹、`PFX` 口令；CA 模式额外打印「信任 CA 的方法」三平台命令（Windows `Import-Certificate -FilePath certs/ca.crt -CertStoreLocation Cert:\CurrentUser\Root`；Debian 系 `cp certs/ca.crt /usr/local/share/ca-certificates/saea-dev-ca.crt && update-ca-certificates`；macOS `security add-trusted-cert -d -k /Library/Keychains/System.keychain certs/ca.crt`），并提示 `security remove-trusted-cert` / 证书管理器删除方式。
- 退出码：`0` 成功；`1` 参数错误（含 `--keysize 1024`）；`2` 生成或写盘失败；`3` 全部文件已存在而跳过（脚本可据此判断「这次没产出新东西」，但仍然不是失败）。
- 失败时向 stderr 打印一句中文原因与可执行建议，不输出堆栈（`--verbose` 不在本期）。

## 9. `--verify` 自检（替代单测的证据来源）

生成完立刻在进程内验证，任一项失败则 `exit 2`：

1. `SaeaCertificateLoader.FromPfx(pfxPath, pwd)` 成功且 `HasPrivateKey == true`（自签/叶子/CA 三份都要过）。
2. `SaeaCertificateLoader.FromPem(crtPath, keyPath)` 成功且 `HasPrivateKey == true`。生成器实际写出的是 PKCS#1 `RSA PRIVATE KEY`，loader 已支持，不需要额外分支（§16 实测）。
3. CA 模式：`X509Chain` + `X509ChainTrustMode.CustomRootTrust` + `CustomTrustStore` 放 CA，`Build(serverCert)` 成功；同时断言 `Build(clientCert)` 成功。这是**唯一**的链校验证据，且只在工具（net8）里用，库不得引入该 API。
4. 证书 SAN 与主题断言：按本次实际生效值（`--cn` / `--dns` / `--ip` 或默认）检查 `dNSName` 与 `iPAddress`；CA 证书检查 `BasicConstraints.CA == true`；server 证书 EKU 含 serverAuth、client 证书 EKU 含 clientAuth。用 `X509Certificate2.Extensions` 解析，不依赖 internal 类型。
5. CA 模式：一次本机回环 TLS 握手——`SocketOptionBuilder` 建服务端（`ServerCertificate = server.pfx` 读回的证书，`ClientCertificateRequired = true`）+ 客户端（`ClientCertificates = client.pfx`，`TargetHost = "localhost"`，`AllowInvalidCertificates = true`），`UseStream()` 各收发一帧并比对。客户端 `AllowInvalidCertificates = true` 是刻意的：自测机不一定装了 CA；服务端不设该开关，靠既有 `allowUnknownRoot: true` 接受客户端证书（§16 实测 `AllowUnknownCertificateAuthority` 下 `Build` 返回 true）。如果某个平台这一条走不通，才允许在服务端也打开 `AllowInvalidCertificates`，并在输出里明确写「本次握手未验证链，链证据见第 3 项」。

## 10. 实现要点（BouncyCastle 2.6.2，netstandard2.0）

- 密钥对：`RsaKeyPairGenerator` + `RsaKeyGenerationParameters(BigInteger.ValueOf(65537), new SecureRandom(), keySize, 64)`（命名空间 `Org.BouncyCastle.Crypto.Parameters`）。不要用 .NET 的 `RSA.Create(int)`，`.NET Framework 4.6.2` 运行时没有该重载，与 G5 冲突。
- 证书构造：`X509V3CertificateGenerator`；签名工厂 `new Asn1SignatureFactory("SHA256WITHRSA", signingKey, random)`；**只能用 `Generate(ISignatureFactory)`**（已 `SetPublicKey` 之后）。BC 2.6.2 里没有 `Generate(ISignatureFactory, AsymmetricKeyParameter)` 这个重载，照旧文档写会直接编译失败（实测 CS7036，§16）。
- 扩展：`X509Extensions.BasicConstraints` / `KeyUsage` / `ExtendedKeyUsage` / `SubjectKeyIdentifier` / `AuthorityKeyIdentifier` / `SubjectAlternativeName`。SKI 与 AKI 用非过时 API：`X509ExtensionUtilities.CreateSubjectKeyIdentifier(AsymmetricKeyParameter)`、`X509ExtensionUtilities.CreateAuthorityKeyIdentifier(X509Certificate)`；`SubjectKeyIdentifierStructure` 已标记过时，不要用。SAN 用 `GeneralNames(new[] { new GeneralName(GeneralName.DnsName, "localhost"), new GeneralName(GeneralName.IPAddress, "127.0.0.1") })` —— 常量名是 `DnsName`，不是 `DNSName`（§16 实测）。
- 时间：BC 按 UTC 处理，传 `options.NotBefore.UtcDateTime`；生成前校验区间非空。
- DN：直接交给 `new X509Name(options.Subject)` 解析完整 DN；解析异常包装为 `SaeaTlsException` 并保留 inner exception。注意 BC 渲染顺序与输入相反：输入 `CN=localhost,O=SAEA` 时 .NET 侧显示 `O=SAEA, CN=localhost`（§16 实测），打印与 README 示例按实际显示写，别让人以为 CN 丢了。
- 打包：`Pkcs12StoreBuilder().Build()` → `SetKeyEntry("saea", new AsymmetricKeyEntry(privateKey), chainEntries)` → `Save(stream, pwd.ToCharArray(), random)` → `new X509Certificate2(bytes, pwd, SaeaCertificateLoader.DefaultKeyStorageFlags)`；与 `SaeaCertificateLoader.LoadPemCore` 保持同一套路。空口令实测可保存并读回（net8）。
- PEM 写出：`PemWriter` 写 `Org.BouncyCastle.X509.X509Certificate`（`CERTIFICATE`）；私钥写 `PrivateKeyInfoFactory.CreatePrivateKeyInfo(privateKey)` —— **BC 2.6.2 实际输出 `RSA PRIVATE KEY`（PKCS#1），不是 PKCS#8**（§16 实测）。若确实需要 PKCS#8，得用 `new PemObject("PRIVATE KEY", keyInfo.GetEncoded())`；本期不要求，按 BC 默认写出即可。用 `StringWriter` 并把 `NewLine` 设为 `"\n"` 再落盘，避免 CRLF 影响跨平台工具。
- 不再改 `SaeaCertificateLoader`：`ReadPrivateKey` 已能读 PKCS#1 / PKCS#8（§16），加了分支也没有新的覆盖。
- 生成器内部把 BC 私钥与 `X509Certificate2` 成对持有；不要依赖 `X509Certificate2.GetRSAPrivateKey()` 取 CA 私钥（net462 上 EphemeralKeySet 行为差异大），`SaeaCertificateAuthority.Create` 直接保存生成时的 BC 私钥。
- `SaeaCertificateAuthority.FromPfx` 复用已有 CA 时，私钥从 `SaeaCertificateLoader.FromPfx` 拿到的 `X509Certificate2` 转 BC（可参考 `BcKeyConverter.GetPrivateKey` 的思路，但那是 internal，需要在本类内写一份或把 `BcKeyConverter` 改成 internal 可复用）。设计上优先在 `SaeaCertificateAuthority` 内部写一个小转换，避免动 internal 类的可见性。

## 11. 错误与边界

| 情形 | 行为 |
|---|---|
| `--out` 目录不存在 | 递归创建 |
| 目标文件已存在且无 `--force` | 跳过该文件，打印 `skipped`，其余继续；只要有文件被跳过且没有新文件产出，退出码 3 |
| CA 模式下 `ca.*` 已存在且未给 `--force` / `--reuse-ca` | 报错退出（码 1），提示二选一，不静默重建 CA |
| `--keysize` 非 2048/3072/4096 | 参数错误，退出码 1 |
| `--dns` / `--ip` 传空数组 | 回落到默认 `localhost` / `127.0.0.1`（库 API 里空数组是「不写该类 SAN」，CLI 层不暴露该语义） |
| `--pwd ""` | 生成无口令 PFX（允许，但提示风险） |
| CA 证书没有私钥（用户自己造的场景） | `SaeaTlsException("签发证书需要 CA 私钥。")` |
| `NotAfter <= NotBefore` | `ArgumentException`，工具转成退出码 1 |
| 文件写入失败（占用/无权限） | 退出码 2，stderr 给出路径 |

## 12. 文档

- `Src/SAEA.Sockets/README.md` 与 `README.en.md`：在 Q3「如何配置 SSL/TLS 加密」之后加「开发用证书怎么来」小节，含两条工具命令、产物清单、`SaeaCertificateLoader.FromPfx` 读回示例、CA 信任导入的三平台命令与移除方式、「仅限开发环境」警示。中英必须同步一致。
- 根 `README.md` 与 `README.en.md`（实测两份都存在）：测试/演示工程列表补一行 `SAEA.Sockets.SslTlsTest`，中英同步。
- 公开 API 的 XML 注释按 `Src/SAEA.Common/IPHelper.cs` 体例写全 `summary` / `param` / `returns`。
- 本篇设计文档即上游依据，勿在代码注释里写「见设计文档」之类溯源标记。

## 13. 验证与测试

- 构建：`dotnet build Src/SAEA.Sockets.sln -c Debug` 必须 0 error；新增工程与库都不能引入 C# 9+ 语法（工具工程是 net8.0，仍按同目录既有 `SAEA.P2PTest` 的写法走，不用 file-scoped namespace）。
- 功能证据：`dotnet run --project Src/SAEA.Sockets.SslTlsTest -c Release -- --self-signed --ca --out <临时目录> --verify`，退出码 0，并核对文件清单与 §9 五项结果；再跑一次不带 `--force` 确认「已存在」分支与退出码 3。
- 为什么没有 MSTest：仓库四个 MSTest 工程（Common / MQTT / FTP / Sockets.Pipeline）都不覆盖 `SAEA.Sockets` 的 Ssl 目录，新建 `SAEA.Sockets.Tests` 超出本期已确认范围。本期用 `--verify` 自检提供可复现证据；若后续要回归保护，再单独立项建测试工程（这是已知的缺口，不是遗漏）。
- 四个既有 MSTest 工程仍要跑一遍（`dotnet test` 各自工程），确认没有连带破坏：Common / MQTT / FTP / Sockets.Pipeline。本期不改 loader、不改 `WithSsl`，预期全绿。
- net462 冒烟（一次性，不进仓库）：本机已确认存在 net462 targeting pack（`C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2`，另有 4.7.2 / 4.8 / 4.8.1）。在系统临时目录建一个 net462 控制台工程，引用编译好的 `SAEA.Sockets.dll`，执行「生成自签 + PFX 读回 + PEM 读回 + 空口令 PFX 读回」四件事，贴出输出。若运行时仍不满足，记录「环境原因：缺 net462 运行时」，报告写「net462 未运行」，不放宽 G5 表述。

## 14. 风险

- BouncyCastle 2.6.2 的生成 API 已实测：`Generate` 只有单参重载、`PemWriter` 输出 PKCS#1、`SubjectKeyIdentifierStructure` 过时（见 §16）。剩余不确定项只有 net462 运行时行为（EphemeralKeySet 回退、空口令 PFX）。
- 系统信任库导入需要管理员/用户授权，且会在测试机上留痕。文档要求只导入当前用户或测试机，并给出移除命令。
- 生成的 CA 私钥是开发资产，泄露等于签发权泄露。文档与工具输出都要提示不要把 `ca.key` / `ca.pfx` 提交进仓库；仓库 `.gitignore` 目前只忽略 `*.pfx`，本期要补 `certs/` 或 `*.key`。
- 中文或特殊字符 DN 在不同工具解析上有差异，本期按 `X509Name` 原样处理，不做额外转义保证；DN 显示顺序会被 BC 反转（§16），文档按实际显示写。

## 15. 我替你做过的决定（可逐条否决）

1. 类型命名 `SaeaCertificateFactory` / `SaeaCertificateOptions` / `SaeaCertificateBundle` / `SaeaCertificateAuthority`，与 `SaeaCertificateLoader` 同前缀。
2. CA 句柄 `Create` 后返回可签发对象，而不是让用户自己管理 `AsymmetricKeyParameter`；公开签名完全不出现 BC 类型。
3. 返回 `SaeaCertificateBundle`（证书 + 保存方法），而不是「只返回 `X509Certificate2`，保存另给静态方法」。
4. PEM 私钥按 BC 默认写出（实测是 PKCS#1 `RSA PRIVATE KEY`），**不改** `SaeaCertificateLoader.ReadPrivateKey`——它已经能读 PKCS#1 与 PKCS#8（§16）。
5. 工具无参数时打印帮助而非进交互菜单。
6. 默认口令 `saea`、leaf 30 天、CA 3650 天、RSA 2048、SAN 为 `localhost` + `127.0.0.1`。
7. CA 模式固定文件名 `ca.*` / `server.*` / `client.*`，不支持 `--name` 改这三个。
8. `--verify` 里客户端回环握手用 `AllowInvalidCertificates = true`；服务端不设该开关，依赖既有 `allowUnknownRoot`；链校验单独用 `CustomRootTrust` 覆盖。
9. 工具工程名按 `Tls` 修正（原始需求写作 `SslTslTest`）。
10. CA 也提供 `SavePfx` / `SaveCertificatePem` / `SavePrivateKeyPem`（与 bundle 共用内部写出逻辑），工具因此不需要第二套 PEM/PFX 写出代码。
11. 不扩展 `SaeaCertificateLoader` / `BcKeyConverter` 去把 PFX 里的 CA 链读回并随握手发送：CA 进信任库才是本库补链的正途，改 loader 属公开行为变更、超出范围。此为已知限制，README 要写明。
12. `--cn` 只作用于服务端叶子；CA 与客户端主题各自有 `--ca-cn` / `--client-cn`。未显式给 `--dns` 时自动把 `CN` 中的主机名补进 SAN。
13. net462 只做一次性临时工程冒烟，不往仓库加任何 4.6.2 工程。
14. 增加 `SaeaCertificateAuthority.FromPfx` / `FromPem` 与 CLI `--reuse-ca`：没有这个入口，重跑工具会换掉 CA，已导入的信任全部作废。
15. CA 模式在 `ca.*` 已存在时默认报错，不静默重建；要重建必须显式 `--force`。
16. 「全部跳过」用退出码 3（不是 0），方便脚本区分「没产出新文件」。
17. `--ip` 同时支持 IPv4 与 IPv6（实测 `GeneralName(GeneralName.IPAddress, "::1")` 会编码成 16 字节，正确）。
18. 本期补仓库 `.gitignore`（`certs/` 或 `*.key`），现有规则只忽略 `*.pfx`。

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
| 空口令 PFX 在 net8 可读 | 是 | net462 仍需冒烟 |
| DN 显示顺序 | 输入 `CN=localhost,O=SAEA`，显示 `O=SAEA, CN=localhost` | §10 已注明 |

环境：`dotnet` SDK 10.0.403，Windows；net462 / 4.7.2 / 4.8 / 4.8.1 targeting pack 均存在。