# SAEA.Sockets.SslTlsTool

`net10.0` · 开发工具 · Apache 2.0

[English](README.en.md) | 中文

给 SAEA.Sockets 准备本地 TLS 证书的命令行工具：一条命令产出一张自签证书，或一套本地根 CA 及其签发的服务端/客户端证书。产物默认写入当前工作目录下的 `certs/`。

除生成证书外，另有一组 openssl 兼容子命令负责证书与私钥的格式转换（PEM / DER / PFX / P7B / PKCS#8），见「openssl 兼容子命令」。

> 证书只用于开发与测试。工具不会把 CA 装进任何信任库，也不会修改系统证书存储。

## 为什么需要它

TLS 联调失败，多数时候不是握手代码写错，而是证书本身有问题：SAN 只写了域名、漏掉 `127.0.0.1`；叶子证书缺 `serverAuth` 或 `clientAuth`；为了换一台机器重建 CA，客户端信任库里那份旧 CA 随之作废，连接报 `UntrustedRoot`。这些问题编译期看不出来，运行时只表现为一句含糊的握手失败。

这个工具把三类常见错误挡在生成阶段：

- 服务端证书默认同时写 `dNSName`（`localhost`）和 `iPAddress`（`127.0.0.1`）SAN，域名和 IP 两条校验路径都覆盖。
- EKU 按用途写死：服务端叶子带 `serverAuth`，客户端叶子带 `clientAuth` 且不写 SAN。
- CA 可以 `--reuse-ca` 复用，重签叶子不必重建根证书，已导入的信任不会作废。

## 环境要求与获取

- 需要 .NET 10 SDK 或更新版本，`dotnet --version` 能确认。
- 它不是 NuGet 包，没有 `dotnet add package` 这一步。代码在仓库内，通过 `ProjectReference` 引用 `Src/SAEA.Sockets`。
- 在仓库根目录运行，参数放在 `--` 之后：

```bash
dotnet run --project Src/SAEA.Sockets.SslTlsTool -c Release -- --help
```

`--` 之前的参数归 `dotnet run`，之后的才传给工具。

默认输出目录写的是 `certs`，解析时相对于**执行命令时的当前工作目录**，不是工程目录。在仓库根运行得到 `<repo>/certs`，在 `Src/` 下运行得到 `Src/certs`。不确定时用 `--out` 传绝对路径。

构建后的可执行文件位于：

```text
Src/SAEA.Sockets.SslTlsTool/bin/Release/net10.0/SAEA.Sockets.SslTlsTool.exe
```

想脱离 `dotnet run` 使用，可以发布到固定目录（框架依赖，仍需本机 .NET 10 运行时）：

```bash
dotnet publish Src/SAEA.Sockets.SslTlsTool -c Release -o ./tools/ssltlstool
```

各平台（Windows/Linux/macOS、自包含/框架依赖、单文件）的完整发布参数见「分发与打包」。

## 两条最短命令

### 自签证书：单个服务端自测

```bash
dotnet run --project Src/SAEA.Sockets.SslTlsTool -c Release -- --self-signed --verify
```

写入 `certs/localhost.pfx`、`certs/localhost.crt`、`certs/localhost.key`。SAN 为 `localhost` + `127.0.0.1`，PFX 口令 `saea`，有效至 30 天后。

### 根 CA + mTLS

```bash
dotnet run --project Src/SAEA.Sockets.SslTlsTool -c Release -- --ca --verify
```

写入 9 个文件：`ca.*`、`server.*`、`client.*`。服务端要设 `ClientCertificateRequired = true` 时用这套；CA 默认 3650 天，叶子 30 天。

两种模式可以同时给，`--self-signed --ca` 会在一次运行里把两套都生成。

### 真实输出节选

下面是 `--self-signed --ca --verify` 的实际输出（删去部分重复行，路径与指纹随机器变化）：

```text
生成结果：
  自签证书（localhost）
    主题： CN=localhost
    SHA-1：413966112F0B8395E14D4E80C85DB754FD650C5D
    PFX：...\certs\localhost.pfx（已写入）
    证书 PEM：...\certs\localhost.crt（已写入）
    私钥 PEM（明文）：...\certs\localhost.key（已写入）
  根 CA
    主题： CN=SAEA Dev Root CA
    ...
  服务端证书（server）
    主题： CN=localhost
    ...
  客户端证书（client）
    主题： CN=saea-client
    ...
PFX 口令：默认 saea
注意：以下 .key 是明文私钥（RSA PRIVATE KEY，无口令），泄露等同身份或签发权泄露，请勿提交到版本库：
  ...\certs\localhost.key
  ...\certs\ca.key
  ...\certs\server.key
  ...\certs\client.key
  1/5 PFX 读回：通过（4 份，均含私钥）
  2/5 PEM 读回与 LF/无 BOM：通过（4 份）
  3/5 链校验（CustomRootTrust）：server/client 均通过
  4/5 SAN / BasicConstraints / EKU：通过
  5/5 回环握手：主机名匹配通过，未校验证书链；链证据见第 3 项
证书仅用于开发/测试，请勿用于生产。
```

只跑 `--ca` 时 PFX 是 3 份（CA、server、client），自检行会相应变成 `通过（3 份，均含私钥）`。

复用已有 CA 会多一行：

```text
复用 CA：...\certs\ca.pfx（CA 文件不改动，server/client 将被覆盖）
```

此时 `ca.*` 三件显示「已存在，未改写」，`server.*` / `client.*` 显示「已写入」，退出码 0。

## 产物清单

| 文件 | 内容 | 口令 | 用途 |
|------|------|------|------|
| `<name>.pfx` | PKCS#12，含私钥 | 有，默认 `saea` | 交给 `SaeaCertificateLoader.FromPfx`；curl / Java / Node 需先自行转换（见下方命令） |
| `<name>.crt` | 单张证书 PEM，不含链 | 无 | 导入客户端或系统信任库 |
| `<name>.key` | **明文私钥** PEM，`BEGIN RSA PRIVATE KEY`（PKCS#1） | 无 | 给 nginx、OpenSSL 等需要独立私钥文件的工具 |
| `server.pfx` / `client.pfx`（CA 模式） | PKCS#12，含私钥，并带签发它的 CA 证书 | 有，默认 `saea` | 外部工具可直接拿到 leaf + CA |
| `server.fullchain.crt` | 自行拼接的 leaf + CA | 无 | nginx、curl 等需要 fullchain 的场合 |

外部工具需要 PEM 或 JKS 时，用本工具的兼容子命令或外部 `openssl` 转换（完整参数见「openssl 兼容子命令」）：

```bash
# 转成 PEM（含私钥），curl / Node 可直接用
SAEA.Sockets.SslTlsTool pkcs12 -in certs/server.pfx -nodes -out certs/server.pem
# Java：导入 PKCS#12 到 JKS
keytool -importkeystore -srckeystore certs/server.pfx -srcstoretype PKCS12 -destkeystore server.jks
```

口令默认 `saea`，`openssl` 导入时会提示输入；`keytool` 交互式询问源口令（PKCS#12）与目标口令（JKS）。Node 只认 PEM，`NODE_EXTRA_CA_CERTS` 只用于 CA，不能直接指向 `.pfx`。

命名规则：

- 自签模式用 `<name>`，默认 `localhost`，`--name` 可改；`--name` 只影响自签模式。
- CA 模式固定 `ca` / `server` / `client`，`--name` 不生效，只在输出里提示一行。

几条容易踩的事实：

- `.key` 是不加密的私钥，`--pwd` 和默认口令 `saea` **只保护 PFX**，对 `.key` 没有任何作用。`.key` 泄露等于身份泄露，`ca.key` 泄露等于签发权泄露。
- `--pwd ""`（显式空串）会生成无口令 PFX，工具会打一行红色风险提示，输出不回显口令。
- `.crt` / `.key` 统一 LF 换行、UTF-8 无 BOM；`.crt` 只有单张证书，需要链时自己拼：

```bash
cat certs/server.crt certs/ca.crt > certs/server.fullchain.crt
```

```powershell
[IO.File]::WriteAllText(
    "certs\server.fullchain.crt",
    (Get-Content -Raw certs\server.crt) + (Get-Content -Raw certs\ca.crt),
    [Text.UTF8Encoding]::new($false))
```

`Get-Content -Raw` 保留原文件的 LF 换行，`WriteAllText` 指定 UTF-8 无 BOM，避免 `Set-Content` 默认编码/CRLF 破坏证书文件。

## 用 IP 或域名访问

证书里写的是主机名或 IP，不是 URL。端口不进证书：`18080` 只影响服务端监听，换端口不用重签。

浏览器、curl、.NET 的默认校验都以 SAN 为准，CN 只用于显示。用 IP 访问时证书必须有 `iPAddress` SAN，只写 `dNSName` 一律不认。

### 场景与参数

| 场景 | 命令片段 | 证书里落什么 |
|------|------|------|
| 本机自测 | `--self-signed`（默认值） | `DNS: localhost`、`IP: 127.0.0.1` |
| 局域网 IP | `--self-signed --ip 192.168.1.125` | `DNS: localhost`、`IP: 192.168.1.125` |
| 内网域名 | `--self-signed --dns api.dev.local` | `DNS: api.dev.local`、`IP: 127.0.0.1`，CN 不再自动补进去 |
| 多域名 + 多 IP | `--dns "api.dev.local,dev.local" --ip "192.168.1.125,10.0.0.8"` | `DNS: api.dev.local,dev.local`、`IP: 192.168.1.125,10.0.0.8` |
| IPv6 | `--self-signed --ip "::1"` | `DNS: localhost`、`IP: ::1` |
| 通配符 | `--dns "*.dev.local,localhost"` | `DNS: *.dev.local,localhost` |
| mTLS 对外 | `--ca --dns api.dev.local --ip 192.168.1.125` | server 叶子写 `api.dev.local` 和 `192.168.1.125`；client 不写 SAN |

表格里的 `--dns` 示例为了简短省略了 `localhost`；要以 `--verify` 运行，就得把它写回列表（`--dns "api.dev.local,localhost"` 这样），否则自检第 5 项必失败、退出码 2，因为那一项固定用 `TargetHost=localhost` 握手。

### 三条实测命令

```bash
# 自签 + 局域网 IP
dotnet run --project Src/SAEA.Sockets.SslTlsTool -c Release -- --self-signed --ip 192.168.1.125 --verify
```

退出码 0，5 项全过；SAN 为 `DNS: localhost`、`IP: 192.168.1.125`。显式 `--ip` 把默认的 `127.0.0.1` 整个换掉，不会两条并存。

```bash
# CA 模式，--ip 只作用于服务端叶子
dotnet run --project Src/SAEA.Sockets.SslTlsTool -c Release -- --ca --ip 192.168.1.125 --verify
```

退出码 0，第 3 项输出 `链校验（CustomRootTrust）：server/client 均通过`。client 证书不写 SAN，`--ip` 对它没有作用。

```bash
# 通配符，必须自己带上 localhost
dotnet run --project Src/SAEA.Sockets.SslTlsTool -c Release -- --dns "*.dev.local,localhost" --verify
```

退出码 0，SAN 为 `DNS: *.dev.local,localhost`。显式 `--dns` 会顶掉默认的 `localhost`，所以这里把它写回列表。

证书生成后直接从另一台机器验证，省得先写客户端代码：

```bash
# CA 模式：让 curl 信任 ca.crt，按 IP 访问
curl --cacert certs/ca.crt https://192.168.1.125:18080/
# 自签模式：没有 CA，用 -k 只看 SAN 与握手是否通过
curl -k https://192.168.1.125:18080/
```

`-k` 只跳过链校验，主机名不匹配照样失败，所以它能区分「信任问题」和「SAN 写错」。

### 怎么核对 SAN

PowerShell 直接读扩展：

```powershell
$cert = [Security.Cryptography.X509Certificates.X509Certificate2]::new("certs\server.crt")
$raw = $cert.Extensions['2.5.29.17'].RawData
[Security.Cryptography.X509Certificates.X509SubjectAlternativeNameExtension]::new($raw).Format($false)
```

OpenSSL：

```bash
openssl x509 -in certs/server.crt -noout -text | grep -A1 'Subject Alternative Name'
```

读 `server.crt` 看的是叶子自己；要确认外部客户端实际收到什么，用 `openssl s_client -connect 192.168.1.125:18080 -showcerts` 看服务端下发的那张。

### 容易踩的四个坑

1. 不能用 `--dns` 写 IP。`--dns 192.168.1.125` 产出的 SAN 是 `DNS: 192.168.1.125`、`IP: 127.0.0.1`，自检第 5 项失败、退出码 2，原文是 `自检第 5 项失败：回环握手失败：TLS 客户端告警：bad_certificate(42) Failed to read record`。IP 一律走 `--ip`。
2. 显式 `--dns` / `--ip` 是「完全替换」不是追加。自检第 5 项固定用 `TargetHost=localhost`，显式列表里必须包含 `localhost`，否则退出码 2。
3. 浏览器按 IP 访问时 CN 不起作用，只匹配 SAN。只写 `CN=192.168.1.125` 而不给 `--ip`，一定失败。
4. IP 或机器变了要重签。想保住已经导入的信任就 `--ca --reuse-ca` 重签叶子，它会覆盖 `server.*` / `client.*`，`ca.*` 不动。

### 客户端按 IP 连接

```csharp
var option = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)
    .UseStream()
    .SetIP("192.168.1.125")            // 只能是 IP 字面量
    .SetPort(18080)
    .WithSsl(o =>
    {
        o.TargetHost = "192.168.1.125"; // 不能留 localhost，否则仍按 localhost 校验
    })
    .Build();
```

`TargetHost` 决定 SNI 和主机名校验；主机名是 IP 字面量时库只比对 `iPAddress` SAN，不回退 CN。mTLS 的客户端证书、生命周期和信任导入同上一节。

## 接入 SAEA.Sockets

两个文件就够了：服务端读 `server.pfx`，客户端读 `client.pfx`，口令都是 `saea`。

### 服务端（要求客户端证书）

```csharp
using System.Security.Cryptography.X509Certificates;
using SAEA.Sockets;
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Model;
using SAEA.Sockets.Ssl;

// 证书对象要活到服务端停止之后，不能在这里用 using 包住
var serverCertificate = SaeaCertificateLoader.FromPfx("certs/server.pfx", "saea");

var option = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)
    .UseStream()
    .SetIP("0.0.0.0")
    .SetPort(8443)
    .WithSsl(o =>
    {
        o.ServerCertificate = serverCertificate;
        o.ClientCertificateRequired = true;   // mTLS
    })
    .Build();

var server = new SaeaServerSocket(option);
server.OnAccepted += obj =>
{
    var channel = obj as ChannelInfo;
    if (channel == null) return;
    // channel.Stream 上做收发
};
server.Start();
```

不需要 `GetStream()` 时去掉 `.UseStream()` 即可，`WithSsl(...)` 在事件模式下同样可用。

### 客户端（带客户端证书）

```csharp
using System.Security.Cryptography.X509Certificates;
using SAEA.Sockets;
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Model;
using SAEA.Sockets.Ssl;

var clientCertificate = SaeaCertificateLoader.FromPfx("certs/client.pfx", "saea");

var option = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)
    .UseStream()
    .SetIP("127.0.0.1")            // 实际连接地址，只能是 IP 字面量
    .SetPort(8443)
    .WithSsl(o =>
    {
        o.TargetHost = "localhost"; // SNI 与证书主机名校验都看它
        o.ClientCertificates = new X509Certificate2Collection(clientCertificate);
    })
    .Build();

var client = new SaeaClientSocket(option);
await client.ConnectAsync();
```

`SetIP` 只接受 IP 字面量，域名交给 `TargetHost`；两者不一致时，主机名校验用 `TargetHost`。

### 生命周期

`SaeaCertificateLoader.FromPfx` 返回的 `X509Certificate2` 由调用方释放。交给 `WithSsl(...)` 之后，这个对象必须活到服务端 `Stop()` / `Dispose()` 或客户端断开之后，否则对端握手会拿到已释放的句柄。做法是把它存成字段或局部变量，在服务端/客户端释放之后再 `Dispose`；不要用 `using` 把整个连接过程包住。

### 默认校验与 CA 的关系

`FromPfx` 只承载叶子证书，即使 `server.pfx` 里带了 CA 也一样。服务端握手时下发的链由本库从系统信任库补齐，所以：

- **CA 没有导入客户端信任库时，客户端默认校验会失败**，报 `UntrustedRoot`。
- `--verify` 的第 5 项回环握手只容忍链错误（它验证的是主机名），不能当作链校验成功的证据；链证据在第 3 项。
- 要让默认校验通过，把 `certs/ca.crt` 导入信任库；或者在测试代码里用 `X509Chain` + `CustomRootTrust` 显式指定 CA。

导入信任库（仅本机开发，用完移除）：

Windows 当前用户：

```powershell
Import-Certificate -FilePath certs\ca.crt -CertStoreLocation Cert:\CurrentUser\Root
# 移除，按指纹定位
Get-ChildItem Cert:\CurrentUser\Root | Where-Object { $_.Thumbprint -eq '<THUMBPRINT>' } | Remove-Item
```

以服务账号运行或需要全机信任时改用 `Cert:\LocalMachine\Root`（管理员权限）。

Debian / Ubuntu 与 macOS：

```bash
# Debian / Ubuntu
sudo cp certs/ca.crt /usr/local/share/ca-certificates/saea-dev-ca.crt
sudo update-ca-certificates
# 移除
sudo rm /usr/local/share/ca-certificates/saea-dev-ca.crt && sudo update-ca-certificates --fresh

# macOS
sudo security add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain certs/ca.crt
# 移除
sudo security remove-trusted-cert -d certs/ca.crt
```

Java 与 Node 不读系统信任库，导入系统库对它们无效：

```bash
# Java
keytool -importcert -cacerts -alias saea-dev-ca -file certs/ca.crt
keytool -delete -cacerts -alias saea-dev-ca    # 移除

# Node
export NODE_EXTRA_CA_CERTS=certs/ca.crt
```

新版 Node 也可以用 `--use-system-ca` 走系统库。

## 分发与打包

### 证书按目标系统分发

| 接收方 | 给什么 | 不要给 |
|------|------|------|
| SAEA / Kestrel 服务端 | `server.pfx`，或 `server.fullchain.crt` + `server.key` | `ca.key` |
| 做 mTLS 的客户端 | `client.pfx` | `ca.key`、`server.key` |
| 只需要信任服务端的客户端 | 只有 `ca.crt` | 任何 `.key`、任何 `.pfx` |
| nginx / curl / Node | `server.fullchain.crt` + `server.key` | 带口令的 PFX |
| Java | 由 `server.pfx` 转出的 JKS 或 PKCS#12 | `ca.key` |

打包并校验：

```bash
tar -czf saea-certs.tar.gz certs/ca.crt certs/server.fullchain.crt certs/server.key
sha256sum saea-certs.tar.gz
```

```powershell
Get-FileHash saea-certs.tar.gz -Algorithm SHA256
```

压缩包里不要有 `ca.key`；`certs/` 已被 `.gitignore` 忽略，压缩包本身也别提交。

Windows 两种用法。应用直读 PFX，权限交给文件 ACL；或者导进本机证书存储：

```powershell
$pwd = ConvertTo-SecureString 'saea' -AsPlainText -Force
Import-PfxCertificate -FilePath certs\server.pfx -CertStoreLocation Cert:\LocalMachine\My -Password $pwd
```

IIS / Http.Sys 还要给应用池账号私钥读权限：`certlm.msc` 选中证书 → 所有任务 → 管理私钥，或用 `WinHttpCertCfg.exe`。写 `LocalMachine` 需要管理员 PowerShell；用完在 `certlm.msc` 删除即撤销本机信任。

Linux 放 `/etc/ssl/saea/`：

```bash
sudo install -d -m 755 /etc/ssl/saea
sudo install -m 644 certs/ca.crt certs/server.fullchain.crt /etc/ssl/saea/
sudo install -m 600 certs/server.key /etc/ssl/saea/
sudo chown root:root /etc/ssl/saea/*
```

nginx 两行就够了：

```nginx
ssl_certificate     /etc/ssl/saea/server.fullchain.crt;
ssl_certificate_key /etc/ssl/saea/server.key;
```

私钥 600 且属主 root 时只有 root 能读。systemd 服务以非 root 运行时要么给同组读权限（`chown root:saea` + 640），要么加 ACL；否则 nginx / 应用启动就报 `Permission denied`。

容器里只读挂载，不要 `COPY` 进镜像：

```bash
docker run --rm -v "$PWD/certs:/certs:ro" -p 18080:18080 saea-app
```

Kubernetes 用 Secret 挂到 `/certs` 并设 `readOnly: true`（具体清单未实测，按 Secret 卷的常规写法即可）。容器内要信任这张 CA，Debian/Ubuntu/Alpine 用 `update-ca-certificates`，RHEL 系用 `update-ca-trust`。

`--pwd` 会出现在进程列表和 shell history 里，容器里还容易被 `docker inspect` 带出来。工具只接受命令行口令，没有环境变量，也没有交互式提示，传口令这件事绕不开命令行：能在构建阶段生成就别放到运行时；必须运行时生成时，用只读挂载加 600 权限。`--pwd ""` 只适合一次性环境，且必须配合 600 权限与只读挂载。

客户端信任安装：装哪个文件固定是 `ca.crt`（自签模式则是那张自签证书本身），不要给 PFX：

| 目标 | 装哪个文件 | 入口 |
|------|------|------|
| Windows 当前用户 | `certs\ca.crt` | `Import-Certificate -FilePath certs\ca.crt -CertStoreLocation Cert:\CurrentUser\Root` |
| Debian / Ubuntu | `certs/ca.crt` | `cp` 到 `/usr/local/share/ca-certificates/` 后 `update-ca-certificates` |
| macOS | `certs/ca.crt` | `security add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain certs/ca.crt` |
| Firefox（Linux） | `certs/client.pfx` | `pk12util -i certs/client.pfx -d sql:$HOME/.mozilla/firefox/<profile>` |
| Java | `certs/ca.crt` | `keytool -importcert -cacerts -alias saea-dev-ca -file certs/ca.crt` |
| Node | `certs/ca.crt` | `NODE_EXTRA_CA_CERTS=certs/ca.crt` |

除 Firefox 那行，其余命令与移除方式都在上一节的「默认校验与 CA 的关系」里，这里只补打包侧差异：分发装的是 `ca.crt` 而不是 PFX，`client.pfx` 只装进需要做 mTLS 的浏览器或应用。

有效期与轮换：叶子默认 30 天，CA 默认 3650 天。叶子到期前用 `--ca --reuse-ca` 重签，客户端只换 `server.*`；`ca.crt` 没变，信任库不用重装。工具不生成 CRL / OCSP，私钥泄露或证书要作废时只能重建 CA 并重新分发。

### 工具自身发布到不同系统

| RID | 方式 | 产物 | 目标机要求 | 状态 |
|------|------|------|------|------|
| `win-x64` | 框架依赖单文件 | `SAEA.Sockets.SslTlsTool.exe`，约 7.2 MB | .NET 10 运行时 | 已实测 |
| `linux-x64` | 自包含单文件 | 单个 ELF，约 80.6 MB | 无 | 已实测，在 Windows 上交叉发布 |
| `osx-x64` | 自包含单文件 | 未实测 | 未实测 | 未实测 |
| `osx-arm64` | 自包含单文件 | 未实测 | 未实测 | 未实测 |
| `win-arm64` | 自包含单文件 | 未实测 | 未实测 | 未实测 |

Alpine 这类 musl 发行版不能用 `linux-x64`，要发 `linux-musl-x64`（未实测）；glibc 发行版才用 `linux-x64`。

在仓库根执行：

```bash
# Windows x64：框架依赖，约 7.2 MB，目标机要装 .NET 10 运行时
dotnet publish Src/SAEA.Sockets.SslTlsTool -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./tools/win-x64

# Linux x64：自包含，约 80.6 MB，目标机不需要运行时
dotnet publish Src/SAEA.Sockets.SslTlsTool -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o ./tools/linux-x64

# Apple Silicon（未实测）：RID 必须与目标机架构一致
dotnet publish Src/SAEA.Sockets.SslTlsTool -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -o ./tools/osx-arm64
```

`-r` 是目标 RID：Intel Mac 用 `osx-x64`，Apple Silicon 用 `osx-arm64`，ARM Windows 用 `win-arm64`，装错了要么起不来，要么直接报格式错误。`--self-contained false` 产物小但目标机得有运行时；`true` 把运行时打进单文件，所以 80 MB 起。`-p:PublishSingleFile=true` 只改打包形态，不改行为。

交叉发布：在 Windows 上发布 `linux-x64` 自包含包已实测可用。目标机 `chmod +x ./SAEA.Sockets.SslTlsTool` 后直接运行，先跑一次 `--help` 确认能起。

不推荐 NativeAOT 或 trimming：这两条路径本工具未验证，BouncyCastle 相关反射在裁剪后可能直接抛异常。要用先自己在目标 RID 上跑通 `--ca --verify`。

发布产物和 `dotnet run` 的差别：产物不需要仓库和 SDK，可以单独拷走；但 `--out` 默认仍相对运行时的工作目录，所以在别处运行时给绝对路径更稳。

## 参数

所有参数都是 `--key value` 形式，布尔开关不带值。取值不能以 `--` 开头，否则会被当成下一个选项并报「缺少取值」。

### 模式（至少选一个）

| 参数 | 说明 |
|------|------|
| `--self-signed` | 生成单张自签证书 |
| `--ca` | 生成根 CA + 服务端 + 客户端证书 |

两个可以同时给。都没给时打印帮助并退出码 1。

### 输出与命名

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `--out <dir>` | `certs` | 输出目录，不存在时递归创建；相对当前工作目录。指向已存在的文件时退出码 2 |
| `--name <base>` | `localhost` | 自签模式文件基名，同时用作 CN（未给 `--cn` 时）。只允许 `[A-Za-z0-9._-]{1,100}`，不能是 `.` / `..`，不能是保留名 `ca` / `server` / `client`（不区分大小写）。只在 `--self-signed` 生效 |
| `--cn <subject>` | `CN=localhost` | 完整主题 DN，影响自签证书和 CA 模式的 server 叶子，不影响 CA 与 client。未给时自签模式取 `CN=<name>`（`--name`，默认 `localhost`），CA 模式固定 `CN=localhost` |
| `--ca-cn <subject>` | `CN=SAEA Dev Root CA` | 仅 CA 模式，CA 主题 |
| `--client-cn <subject>` | `CN=saea-client` | 仅 CA 模式，客户端主题 |

主题按输入解析后由 BouncyCastle 重新渲染，显示顺序与输入相反：

```text
--cn "CN=localhost, O=SAEA"   →   主题： O=SAEA, CN=localhost
```

这是正常现象，CN 没有丢。

### 证书内容

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `--dns <a,b>` | 按 CN 推导 | 作用于服务端用途的证书（自签证书、CA 模式的 server 叶子）。省略时：CN 存在且不是 IP 字面量 → `CN` + `localhost`（去重）；否则 `localhost`。显式给则完全替换，**不会**再把 CN 补进去。逗号分隔，不能含空白或逗号，单个名称不超过 253 字符，不支持国际化域名 |
| `--ip <a,b>` | `127.0.0.1` | 作用于服务端用途的证书。显式给则完全替换。支持 IPv4 / IPv6，逐个 `IPAddress.TryParse` 校验 |
| `--days <n>` | `30` | 叶子有效至 n 天后，范围 1..36500。`--ca` 且不带 `--reuse-ca` 时不能大于 `--ca-days` |
| `--ca-days <n>` | `3650` | CA 有效至 n 天后，范围 1..36500，仅 CA 模式 |
| `--keysize <n>` | `2048` | RSA 密钥长度，只接受 `2048` / `3072` / `4096` |

生成算法固定 RSA + SHA-256。证书 `NotBefore` 是生成时刻减 1 天（留时钟偏差余量），`NotAfter` 是生成时刻加天数，所以实际跨度比 `--days` 多一天。CA 是 `CA:TRUE`、`pathLen=0`，叶子是 `CA:FALSE`。

`--dns ""` / `--ip ""` 会被拒绝，提示「要使用默认值请省略该参数」。

### 口令

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `--pwd <password>` | `saea` | PFX 口令。显式空串表示无口令，有风险，工具会提示。输出不回显口令 |
| `--ca-pwd <password>` | 取 `--pwd` | 仅 `--reuse-ca` 时用于读取旧 CA |

### 已存在文件

| 参数 | 说明 |
|------|------|
| `--reuse-ca` | 复用 `<out>/ca.pfx`（缺失时退回 `ca.crt` + `ca.key`）签新叶子。必须与 `--ca` 同用，与 `--force` 互斥。**无条件覆盖 server/client**，CA 文件不动；`--ca-cn` / `--ca-days` 被忽略并提示；新叶子的到期时间晚于现有 CA 时退出码 1 |
| `--force` | 覆盖已存在文件。CA 已存在时会警告「已导入的信任会作废」。与 `--reuse-ca` 互斥 |

不在这两个参数里的行为：

- 自签模式逐文件判断：某个文件已存在且没有 `--force` 就打印 `skipped` 并继续写其余文件；三个都跳过时退出码 3（给了 `--verify` 时还要自检通过）。
- CA 模式只要 `ca.pfx` / `ca.crt` / `ca.key` 任一存在，且既没有 `--reuse-ca` 也没有 `--force`，就报错退出，退出码 1，不会静默重建。

### 其他

| 参数 | 说明 |
|------|------|
| `--verify` | 生成后跑五项自检，任一项失败退出码 2 |
| `--help` | 只打印帮助；与其它参数并存时也只打印帮助，退出码 0 |

## openssl 兼容子命令

首个参数命中 `req`、`x509`、`pkcs12`、`pkcs8`、`pkey`、`crl2pkcs7`、`pkcs7`、`version` 之一时进入兼容分支，否则整段参数交给上面的生成/校验分支。大小写敏感，只看第一个参数。

下文命令统一写成 `SAEA.Sockets.SslTlsTool <子命令> ...`。在仓库根实际运行时，前面加 `dotnet run --project Src/SAEA.Sockets.SslTlsTool -c Release --`；也可以把 `dotnet publish` 的产物加入 `PATH` 后直接调用可执行文件。

### 命令与参数

#### req / x509 / pkcs8 / pkey / pkcs12 / crl2pkcs7 / pkcs7 / version

**`req`** 生成 CSR，或直接自签一张证书。

| 参数 | 说明 |
|------|------|
| `-new` / `-x509` | 二选一：输出 CSR / 直接自签证书 |
| `-newkey rsa:<bits>` | 现场生成 1024 / 2048 / 3072 / 4096 位 RSA 私钥，必须配 `-keyout` |
| `-key` / `-keyout` | 使用既有私钥 / 新私钥输出路径 |
| `-nodes` / `-noenc` | 私钥不加密，与 `-passout` 互斥 |
| `-days` | `-x509` 时的有效天数，默认 30 |
| `-subj` | `/CN=localhost/O=SAEA` 形式的 DN，`,` 也可作分隔符 |
| `-addext` | 追加扩展，可重复；只支持 `subjectAltName`、`basicConstraints`、`keyUsage`、`extendedKeyUsage` |
| `-sha256` / `-out` / `-passout` | 签名摘要（唯一选项） / 输出路径 / 私钥口令来源 |

**`x509`** 读取证书、打印所选信息，并在 PEM 与 DER 之间转换；加 `-req` 时用 CA 签发 CSR。

| 参数 | 说明 |
|------|------|
| `-in` / `-inform` | 输入文件 / `PEM` 或 `DER`，默认 stdin / PEM |
| `-out` / `-outform` | 输出文件 / `PEM` 或 `DER`，默认 stdout / PEM |
| `-noout` | 不输出证书本体，只打印所选信息 |
| `-subject` / `-issuer` / `-dates` / `-serial` / `-fingerprint` | 打印主题、签发者、有效期、序列号、指纹 |
| `-sha256` / `-pubkey` | 指纹改用 SHA-256 / 打印公钥 PEM |
| `-req` / `-CA` / `-CAkey` | 以 CSR 为输入，用给定 CA 证书与私钥签发 |
| `-passin` / `-CAcreateserial` / `-days` | CA 私钥口令 / 写 `<CA 文件名>.srl` / 新证书有效天数 |
| `-extfile` / `-extensions` / `-copy_extensions` | 扩展文件 / 段名 / 从 CSR 复制扩展（`copy` 或 `none`） |

`-text` 与 `-nameopt` 不支持：前者的替代是上面的打印项，姓名固定 oneline 风格（`CN = localhost, O = SAEA`）。

**`pkcs8`** 在 PKCS#1 与 PKCS#8 之间转换私钥。

| 参数 | 说明 |
|------|------|
| `-topk8` | 输出 PKCS#8；不给则输出 PKCS#1 |
| `-nocrypt` | 输出不加密的 PKCS#8 |
| `-v1 PBE-SHA1-3DES` | 旧式 PBE 加密 |
| `-v2` | `aes-128-cbc` / `aes-192-cbc` / `aes-256-cbc` / `des3`，默认 `aes-256-cbc` |
| `-in` / `-out` / `-inform` / `-outform` | 输入输出与格式，默认 stdin / stdout / PEM / PEM |
| `-passin` / `-passout` | 读取 / 写出口令来源 |

**`pkey`** 读取私钥，输出 PKCS#8（默认）、PKCS#1 或公钥。

| 参数 | 说明 |
|------|------|
| `-traditional` | 输出 PKCS#1，与下面的加密参数互斥 |
| `-pubout` | 输出公钥 |
| `-aes-128-cbc` / `-aes-192-cbc` / `-aes-256-cbc` / `-des3` | 加密输出的 PKCS#8，只能选一个 |
| `-in` / `-out` / `-inform` / `-outform` | 输入输出与格式，默认 stdin / stdout / PEM / PEM |
| `-passin` / `-passout` | 读取 / 写出口令来源 |

`-pubout` 不能与 `-traditional` 或加密参数同用；没给加密算法时给 `-passout` 会报错，不会静默输出明文。

**`pkcs12`** 导出 PFX，或把 PFX 拆成 PEM。

| 参数 | 说明 |
|------|------|
| `-export` | 导出 PFX；不给则为读取模式 |
| `-in` | 导出时是叶子证书，读取时是 PFX，默认 stdin |
| `-inkey` | 导出用私钥 |
| `-certfile` | 追加到 PFX 的证书，可重复 |
| `-name` | friendlyName，给了才写 |
| `-passout` / `-passin` | 写出 / 读取口令来源 |
| `-legacy` | 改用 RC2-40 / 3DES / SHA-1 旧算法族 |
| `-nodes` | 不加密；读取模式下输出明文私钥 |
| `-nokeys` / `-nocerts` / `-clcerts` / `-cacerts` | 读取筛选：不输出私钥 / 只要私钥 / 只要叶子 / 只要链 |
| `-nomac` | 导出时不写 MAC，与 `-passout` 互斥 |
| `-info` | 把摘要写到 stderr |

默认算法是 AES-256-CBC + PBKDF2（迭代 2048）、MAC 为 HMAC-SHA-256；`-legacy` 改为证书 `PBE-SHA1-RC2-40`、私钥 `PBE-SHA1-3DES`、MAC SHA-1，迭代仍 2048。读取输出顺序为私钥（PKCS#8）、叶子、链。

**`crl2pkcs7`** 把证书打包成 certs-only PKCS#7。

| 参数 | 说明 |
|------|------|
| `-nocrl` | 必需，声明不读 CRL |
| `-certfile` | 装入包的证书，可重复 |
| `-out` / `-outform` | 输出文件 / `PEM` 或 `DER`，默认 stdout / PEM |

**`pkcs7`** 读取 PKCS#7 包，逐张输出其中的证书。

| 参数 | 说明 |
|------|------|
| `-in` / `-inform` | 输入文件 / `PEM` 或 `DER`，默认 stdin / PEM |
| `-print_certs` | 输出包内全部证书 PEM |
| `-out` / `-outform` | 输出文件 / `PEM` 或 `DER`，DER 需要 `-out` |
| `-noout` | 空操作：本工具不打印 `subject=` / `issuer=` 摘要行 |

**`version`** 打印本工具版本，声明与 `openssl version` 不兼容。

| 参数 | 说明 |
|------|------|
| `-help` | 打印该命令的帮助并退出 0 |

### 格式矩阵

| 输入格式 | 目标格式 | 命令 | 备注 |
|----------|----------|------|------|
| PEM 证书 | DER 证书 | `x509 -out cert.der -outform DER` | 一次只处理单张 |
| DER 证书 | PEM 证书 | `x509 -inform DER -out cert.pem` | 输入格式必须显式声明 |
| PKCS#1 / PKCS#8 私钥 | PKCS#1 | `pkey -traditional` | 旧式 `Proc-Type: 4,ENCRYPTED` 只读不写 |
| PKCS#1 / PKCS#8 私钥 | 加密 PKCS#8 | `pkcs8 -topk8 -v2 aes-256-cbc` | 省略 `-v2` 时就是 `aes-256-cbc` |
| PFX | PEM（私钥 + 证书） | `pkcs12 -in ... -nodes -out ...` | 输出顺序：私钥、叶子、链 |
| PEM 证书 + 私钥 | PFX | `pkcs12 -export -in ... -inkey ...` | 链用 `-certfile` 追加 |
| PEM 证书 / 链 | P7B | `crl2pkcs7 -nocrl -certfile ...` | 只含证书，不含私钥 |
| P7B | PEM 证书 | `pkcs7 -print_certs` | 不输出 `subject=` / `issuer=` 摘要行 |
| CSR | 证书 | `x509 -req -CA ... -CAkey ...` | 没有 CA 时先用 `req -x509` 自签 |

### 常用示例

DER 证书转 PEM（`-inform` 声明输入格式，输出默认仍是 PEM）：

```bash
SAEA.Sockets.SslTlsTool x509 -inform DER -in cert.der -out cert.pem
```

PKCS#8 私钥转 PKCS#1（`pkey` 默认输出 PKCS#8，加 `-traditional` 换 PKCS#1）：

```bash
SAEA.Sockets.SslTlsTool pkey -in key.p8 -traditional -out key.pem
```

PFX 拆成 PEM（`-nodes` 让私钥以明文写出，输出顺序是私钥、叶子、链）：

```bash
SAEA.Sockets.SslTlsTool pkcs12 -in server.pfx -nodes -out server.pem
```

证书加私钥打包成 PFX（链用 `-certfile` 追加；缺 `-passout` 时交互提示口令）：

```bash
SAEA.Sockets.SslTlsTool pkcs12 -export -in server.crt -inkey server.key -out server.pfx
```

用旧算法族导出 PFX（兼容只认 RC2 / 3DES 的旧系统）：

```bash
SAEA.Sockets.SslTlsTool pkcs12 -export -in server.crt -inkey server.key -legacy -out server-legacy.pfx
```

生成 CSR 并签发（先 `req` 产 CSR 与私钥，再用 CA 签成证书）：

```bash
SAEA.Sockets.SslTlsTool req -new -newkey rsa:2048 -keyout key.pem -subj "/CN=localhost" -out req.csr
SAEA.Sockets.SslTlsTool x509 -req -in req.csr -CA ca.crt -CAkey ca.key -CAcreateserial -days 365 -out server.crt
```

打包与拆解 P7B（`crl2pkcs7` 打包证书，`pkcs7 -print_certs` 拆回 PEM）：

```bash
SAEA.Sockets.SslTlsTool crl2pkcs7 -nocrl -certfile server.crt -certfile ca.crt -out chain.p7b
SAEA.Sockets.SslTlsTool pkcs7 -in chain.p7b -print_certs -out chain.pem
```

### 与 openssl 的差异

| 项目 | openssl 行为 | 本工具 | 影响 |
|------|--------------|--------|------|
| `Bag Attributes` | `pkcs12` 输出时带 friendlyName 等属性行 | 不输出 | 靠该行判断 friendlyName 的脚本失效 |
| `pkcs7 -print_certs` 摘要行 | 每张证书前打 `subject=` / `issuer=` | 只输出 PEM 块 | `-noout` 成为空操作；按行切分的脚本需改 |
| `pkcs7 -print_certs -outform DER` | 接受组合 | 报错（`-print_certs` 只输出 PEM 文本） | 组合无实际意义，脚本需拆开 |
| `crl2pkcs7 -nocrl` 无 `-certfile` | 生成不含证书的 certs-only 包 | 报错 | 只跑空包的脚本失效，该产物无实际用途 |
| `pkcs12` 读取时的私钥加密 | 用 `-passout` 重新加密，缺省交互索要 | 用 `-passin` 的口令重新加密；读取模式不接受 `-passout`，`-nodes` 输出明文 | 读写口令不同的脚本需加 `-nodes` 或拆成两步 |
| `pkcs12 -nodes` 的 MAC | 只关加密，仍用口令写 MAC | `-nodes` / `-nomac` 都不写 MAC，与 `-passout` 互斥 | 无口令 PFX 无法做完整性校验，只能直接读回 |
| `x509 -text` | 完整 ASN.1 dump | 不支持，报错并给替代参数 | 需要 dump 时只能用外部工具 |
| `-nameopt` | 可切 RFC2253 / multiline 等 | 不支持，固定 oneline | 需要 RFC2253 的脚本要自行转换 |
| `-config` | 读配置文件生成证书 | 不支持，仅 `-extfile` / `-addext` | 既有 `openssl.cnf` 流程需改写成命令行 |
| `req -newkey` 未给 `-nodes` / `-passout` | 交互式索要口令并加密私钥，非交互时读不到口令即失败 | 明文写出并在 stderr 提示 | 明文私钥不得进版本库；脚本需要加密必须显式给 `-passout` |
| `version` | `OpenSSL 3.x.y …` | `<工具名> <版本>（openssl 兼容子集）` | 版本探测脚本可能误判 |
| PFX 默认参数 | 证书与私钥均 AES-256-CBC，PBKDF2 迭代 2048，MAC 为 HMAC-SHA-256、迭代 2048，PBE 与 MAC 的 salt 各 8 字节 | 同上，常量写死；`-legacy` 走 RC2-40 / 3DES / SHA-1 | 加密参数与 openssl 3 一致，`openssl pkcs12 -info` 显示的算法、迭代数、盐长相同；文件字节仍不同，因为 salt 与 IV 随机 |
| `-info` 文本 | 逐字固定格式 | 信息量对齐、措辞不同 | 匹配 `MAC:` 等字段的正则可能失效 |

结论：只读 PEM / DER / PFX / P7B 的程序（.NET、Java、nginx、IIS）不受影响；靠正则解析文本输出的脚本可能受影响。salt 与 IV 随机，任何两次导出都不可能字节相同，本工具保证的是参数、结构、条目顺序与 friendlyName 规则一致。

### 退出码

判断方式：只看第一个参数，且大小写敏感。命中 `req`、`x509`、`pkcs12`、`pkcs8`、`pkey`、`crl2pkcs7`、`pkcs7`、`version` 之一走兼容分支；首个参数以 `-` 开头或不带参数时走遗留生成/校验分支。

兼容分支：

| 码 | 含义 |
|----|------|
| `0` | 命令成功；`-help` 也是 0 |
| `1` | 任何失败：未知子命令、未知参数、取值非法、文件读写失败、口令错误 |

遗留分支：

| 码 | 含义 |
|----|------|
| `0` | 至少写入一个新文件；给了 `--verify` 时自检通过 |
| `1` | 参数错误 |
| `2` | 生成失败、读取 CA 失败、写盘失败、`--verify` 自检失败 |
| `3` | 一个文件都没写入（全部已存在而跳过） |

### 口令来源

`-passin` 与 `-passout` 共用一套语法：

| 写法 | 示例 | 注意事项 |
|------|------|----------|
| `pass:<明文>` | `-passout pass:saea` | 明文会留在 shell history 与进程列表，优先用后两种 |
| `env:<变量名>` | `-passin env:PFX_PASS` | 变量不存在或为空按未给口令处理，不回退到交互提示 |
| `file:<路径>` | `-passin file:pass.txt` | 只取首行并去掉行尾 CR / LF；文件不存在或为空报错 |
| `stdin` | `-passin stdin` | 只读一行；交互模式下禁用 |

未给口令源且操作需要口令时，标准输入没有重定向就交互提示（输入不回显），否则报 `需要口令：请用 -passin/-passout，或从交互终端运行。` 并退出 1。空口令对 PFX 合法，对加密 PKCS#8 报错。

### 怎么验证

1. 跑全量兼容用例：`pwsh Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1`。
2. `-ToolPath` 指向已构建的 `SAEA.Sockets.SslTlsTool.exe`（`dotnet publish` 或 `-o` 自定义目录的产物）；不传时脚本用 `dotnet run --project Src/SAEA.Sockets.SslTlsTool -c Release --` 现编译运行。
3. 预期末行 `N passed, 0 failed`、退出码 0，`N` 以脚本实际用例数为准（当前 33 条）。
4. 本机装了 `openssl` 时可拿同一组参数手工对拍（`openssl x509 -in ...`、`openssl pkcs12 -export ...`）；脚本不做差分，`SAEA_OPENSSL` 也不是脚本参数。

## 交互模式

不带任何参数启动就进入交互模式：

```bash
dotnet run --project Src/SAEA.Sockets.SslTlsTool -c Release
```

逐行输入与命令行完全相同的参数，回车执行，然后继续等下一行。

| 输入 | 行为 |
|------|------|
| `help` | 打印完整帮助，继续 |
| `exit` / `quit` / EOF（Ctrl+Z 后回车，或 Ctrl+D） | 退出，进程退出码 0 |
| `--help` | 只打印帮助，继续 |
| 空行 | 忽略 |
| 引号未闭合 | 提示 `命令里的双引号没有闭合。`，该行不执行，继续 |

引号规则：

- 空白分隔参数，双引号包住含空格的取值，例如 `--cn "CN=localhost, O=SAEA"` 或 `--out "D:\dev certs"`。
- 引号内只有 `\"` 是转义，反斜杠本身原样保留，所以 UNC 路径 `\\server\share` 不会被吃掉一个分隔符。

单条命令失败不会退出进程，只打印该次退出码：

```text
> --bogus
未知参数：--bogus。
输入 help 查看用法。
退出码：1；交互模式继续，输入 exit 退出。
> --self-signed --out "./dev certs" --verify
...
> exit
已退出。
```

手工反复试参数时用它比较省事；脚本里用命令行模式，拿到的才是真实退出码。

## `--verify` 的五项自检

生成完成后就地验证，任一失败立即停止，打印 `自检第 N 项失败：...`，退出码 2。

| 项 | 检查内容 | 说明 |
|----|----------|------|
| 1/5 | 每份 PFX 用 `SaeaCertificateLoader.FromPfx` 读回，且 `HasPrivateKey` | 自签 1 份、CA 3 份；两种模式同时给是 4 份 |
| 2/5 | `.crt` + `.key` 用 `FromPem` 读回且含私钥；逐字节断言无 `CR`、无 UTF-8 BOM | 数量同第 1 项 |
| 3/5 | 链校验：`X509Chain` + `CustomRootTrust`，CA 放进 `CustomTrustStore`，server 与 client 都能 `Build` | 仅 CA 模式。自签模式打印「不适用，已跳过」 |
| 4/5 | CA 的 `BasicConstraints.CA == true`；server 的 SAN 与期望 DNS/IP 一致且 EKU 含 `serverAuth`；client 无 SAN 且 EKU 含 `clientAuth` | 期望值由工具按命令行输入独立计算，不复用库的推导实现 |
| 5/5 | 进程内起 SAEA 服务端（`ClientCertificateRequired = true`）与客户端（`TargetHost = "localhost"`、带客户端证书），在系统分配的空闲端口上发一帧并原样收回 | 只容忍链错误；主机名不匹配必须失败。自签模式用同一张证书同时充当两端 |

第 3 项和第 5 项不能互相替代：

- 第 3 项是唯一的链证据，它显式拿 CA 当信任根。
- 第 5 项的回调只放行链错误（`errors` 里除主机名不匹配外一律返回 `true`），所以它证明的是「主机名匹配 + 双方能完成握手 + 数据能回环」，不证明链可信。输出里写的也是「未校验证书链；链证据见第 3 项」。

自签模式没有 CA，第 3 项跳过，第 5 项用同一张自签证书同时当服务端和客户端证书，因此那里的通过只代表配置自洽，不代表任何外部客户端会接受这张证书。

## 退出码

| 码 | 含义 |
|----|------|
| `0` | 至少写入一个新文件；给了 `--verify` 时自检通过 |
| `1` | 参数错误：未知参数、缺值、取值越界、互斥组合、保留名、`ca.*` 已存在但未指定 `--reuse-ca` / `--force`、缺少模式 |
| `2` | 生成失败、读取 CA 失败、写盘失败、`--out` 是已存在的文件、`--verify` 自检失败 |
| `3` | 一个文件都没写入（全部已存在而跳过）；给了 `--verify` 时自检通过 |

优先级是 1 > 2 > 0/3：参数错误在生成前就返回 1；只有「没有任何错误，但也没写任何文件」才是 3。

**3 不是失败**，只表示这次运行没有产出新文件，常见于重复执行同一条命令。给了 `--verify` 时它照样会读盘上现有的文件做自检。

PowerShell：

```powershell
dotnet run --project Src/SAEA.Sockets.SslTlsTool -c Release -- --ca --verify
$code = $LASTEXITCODE
switch ($code) {
    0 { Write-Host "已生成或覆盖证书" }
    3 { Write-Host "证书已存在，本次未写入新文件" }
    default { throw "证书生成失败，退出码 $code" }
}
```

bash：

```bash
dotnet run --project Src/SAEA.Sockets.SslTlsTool -c Release -- --ca --verify
code=$?
case "$code" in
    0) echo "已生成或覆盖证书" ;;
    3) echo "证书已存在，本次未写入新文件" ;;
    *) echo "证书生成失败，退出码 $code" >&2; exit "$code" ;;
esac
```

CI 可以只接受 0，也可以把 3 当作「已就绪」；取决于这一轮是否必须产出新文件。

## 故障排查

| 症状 / 报错 | 原因 | 处理 |
|-------------|------|------|
| `CA 文件已存在：<路径>。要复用请加 --reuse-ca，要重建请加 --force。` | 目录里已经有 `ca.pfx` / `ca.crt` / `ca.key`，本次既没复用也没覆盖 | 加 `--reuse-ca` 重签叶子，或加 `--force` 重建 CA（会作废旧信任） |
| `参数 --reuse-ca 与 --force 互斥，只能选一个。` | 两个都给了 | 二选一：复用旧 CA，或重建 |
| `参数 --reuse-ca 必须与 --ca 同时使用。` | 只写了 `--reuse-ca` | 补上 `--ca` |
| `参数 --days 不能大于 --ca-days：新叶子的到期时间会晚于 CA。` | `--days` 超过 `--ca-days` | 减小 `--days`，或把 `--ca-days` 调到不小于它（上限 36500） |
| `新叶子的到期时间晚于现有 CA（到期时间 ...，本地时间）。请减小 --days，或加 --force 重建 CA。` | 复用的旧 CA 快到期了 | 减小 `--days`，或 `--force` 重建 CA |
| `参数 --name 不能使用保留名 ca/server/client。` | CA 模式固定用这三个名字，自签也不能占用 | 换一个基名，例如 `--name devbox` |
| `参数 --name 只允许 1..100 个 [A-Za-z0-9._-] 字符。` | 基名里有空格、中文或路径分隔符 | 用字母、数字、点、下划线、短横线 |
| `参数 --ip 的取值不是合法的 IP 地址：not-an-ip。` | `--ip` 里有非 IP 值 | 改成 IPv4/IPv6 字面量，多个用逗号分隔 |
| `参数 --dns 的取值为空；要使用默认值请省略该参数。` | 写了 `--dns ""` | 直接省略 `--dns` |
| `参数 --dns 的取值无效：...（不能含空白或逗号，长度不超过 253）。` | 名称里带空格，或用了国际化域名 | 去掉空白，改用 ASCII 名称 |
| `找不到可复用的 CA：既没有 ...\ca.pfx，也没有 ca.crt + ca.key。请先用 --force 生成 CA。` | `--reuse-ca` 时目录里没有 CA | 先跑一次 `--ca`（或 `--ca --force`） |
| `读取 CA 失败（口令错误、文件损坏或证书不是 CA）：...` | 旧 CA 的口令与本次 `--pwd` 不一致，或文件被换过 | 用 `--ca-pwd` 传旧口令；确认文件没损坏；实在不行 `--force` 重建 |
| `输出路径是一个已存在的文件，不能作为目录使用：...` | `--out` 指向了文件 | 给它一个目录路径 |
| `缺少 --self-signed 或 --ca，未执行任何生成。` | 只给了 `--out`、`--verify` 这类参数 | 补上模式参数 |
| 主题显示成 `O=SAEA, CN=localhost` | BouncyCastle 渲染 DN 的顺序与输入相反 | 正常现象，CN 没丢 |
| 把 `.key` 当成加密私钥，以为它需要 `saea` 口令 | `.key` 本来就是无口令的 PKCS#1 明文私钥，口令只保护 PFX | `openssl rsa -in certs/server.key -noout -check` 直接能读；不要给它传 `-passin` |
| 客户端报 `UntrustedRoot`，或 `The remote certificate is invalid` | `ca.crt` 没进客户端信任库 | 导入 `ca.crt`，或在测试代码里用 `CustomRootTrust` |
| Java / Node 仍然不信任证书 | 它们不读系统信任库 | Java 用 `keytool -importcert`，Node 用 `NODE_EXTRA_CA_CERTS` |
| `自检第 5 项失败：回环握手失败：...` | 端口被占用、握手超时，或证书与 `TargetHost` 不匹配 | 重跑一次（会换端口）；仍失败就检查 `--cn` / `--dns` 是否还包含 `localhost` |

失败信息里带路径的，路径都是完整路径，可以直接复制去核对文件。

## 常见问题

### Q：要让同事用 `https://192.168.1.125:18080` 访问，证书怎么生成？

用 `--self-signed --ip 192.168.1.125`，CA 模式加 `--ca --ip 192.168.1.125`。默认的 `127.0.0.1` 会被整个替换掉；`--dns` 默认仍是 `localhost`，自检第 5 项就靠它通过，别顺手删。

### Q：端口要写进证书吗？

不写。证书里只有主机名和 IP，端口由监听和 URL 决定，`18080` 换成别的也不用重签。SAN 里没有端口这个字段。

### Q：`--dns 192.168.1.125` 为什么不生效？

它写的是 `dNSName`，得到一个字符串 SAN；IP 访问只查 `iPAddress` SAN。实测 SAN 变成 `DNS: 192.168.1.125`、`IP: 127.0.0.1`，自检第 5 项失败，退出码 2。IP 一律用 `--ip`。

### Q：浏览器还是提示「不安全」或证书无效？

先分两种情况。自签模式没有 CA，要把那张自签证书本身装进信任库（不是 `ca.crt`，自签模式不产出它）；CA 模式要把 `ca.crt` 装进系统或浏览器信任库。如果报错是名称不匹配一类，那不是信任问题，是 SAN 没写对。

### Q：客户端为什么报 `UntrustedRoot`？

`ca.crt` 没进客户端信任库，或者装的是重建前的旧 CA。库在握手时从系统信任库补齐根，根不在库里就报这个。Java / Node 不读系统库，要单独导入或设 `NODE_EXTRA_CA_CERTS`。

### Q：mTLS 要交付哪几个文件？

服务端 `server.pfx`（或 `server.fullchain.crt` + `server.key`）加 `ca.crt`；客户端 `client.pfx` 加 `ca.crt`。`ca.key` 谁都不给，`server.key` 只给需要独立私钥文件的工具。

### Q：`--pwd` 能保护 `.key` 吗？

不能。`--pwd` 只加密 PFX，`.key` 是无口令的 PKCS#1 明文私钥。要传私钥就整包加密或走密钥管理，别指望口令。

### Q：重复执行为什么退出码 3？

目标文件都已存在且没给 `--force`，本次一个都没写。3 不是失败，给了 `--verify` 时照样读盘上现有文件做自检。要覆盖加 `--force`，只换叶子用 `--ca --reuse-ca`。

### Q：`--force` 和 `--reuse-ca` 有什么区别？

`--force` 重建 CA，旧 `ca.crt` 作废，所有客户端要重装信任；`--reuse-ca` 保留 `ca.*`，只覆盖 `server.*` / `client.*`，信任库不用动。两者互斥。

### Q：`.pfx` 怎么给 nginx / curl / Java / Node？

nginx 要 `server.fullchain.crt` + `server.key`；curl 最省事也是用这两个：`curl --cacert certs/ca.crt --cert certs/server.fullchain.crt --key certs/server.key https://192.168.1.125:18080/`。OpenSSL 后端的 curl 也能直接读 PKCS#12（`--cert certs/server.pfx:saea --cert-type P12`），但发行版自带的 curl 未必支持，跨机器时不要依赖它。Java 用 `keytool -importkeystore` 转 JKS；Node 只认 PEM，先用 `pkcs12 -in certs/server.pfx -nodes -out certs/server.pem` 转成 PEM。转换命令在「产物清单」与「openssl 兼容子命令」。

### Q：证书 30 天到期怎么办？

用 `--ca --reuse-ca` 重签叶子，`server.*` / `client.*` 被覆盖，`ca.crt` 不动，信任库不用改。自签模式直接重跑并加 `--force`。

### Q：能用于生产吗？

不能。工具不做 CSR、不做域名验证、不做续期，也没有 CRL / OCSP。生产走 ACME 或正规 CA。

### Q：CN 还要不要填？

可以不填，现代客户端只看 SAN，CN 只用于显示。默认 CN 是 `localhost`；只写 `CN=192.168.1.125` 而不给 `--ip`，浏览器和库都不认。

### Q：IPv6 怎么写？

`--ip "::1"`，多个用 `--ip "::1,fe80::1"`。实测 SAN 为 `DNS: localhost`、`IP: ::1`。URL 里 IPv6 要加方括号：`https://[::1]:18080`。

### Q：通配符怎么写？能跨层级吗？

`--dns "*.dev.local,localhost"`，只匹配一级：`api.dev.local` 行，`a.b.dev.local` 不行。显式 `--dns` 会顶掉默认的 `localhost`，得自己带上。

### Q：多域名多 IP 怎么写？会追加吗？

逗号分隔、一次给全：`--dns "api.dev.local,dev.local" --ip "192.168.1.125,10.0.0.8"`。显式 `--dns` / `--ip` 是完全替换，既不追加默认值也不追加 CN。

### Q：换机器或 IP 变了怎么办？

重签叶子：`--ca --reuse-ca --ip <新地址>`。旧地址还要用就一起写进逗号列表。`--reuse-ca` 时 `--days` 不能超过旧 CA 的到期时间。

### Q：不同系统怎么打包分发？

按「分发与打包」的交付单元表给对应文件，`ca.key` 不出本机，压缩包用 SHA-256 校验。容器只做只读挂载，不要把证书打进镜像。

### Q：工具能在 Linux / macOS 上跑吗？怎么发布？

能，目标是 `net10.0`。目标机有 .NET 10 就 `dotnet run` 或框架依赖发布；没有就 `--self-contained true` 发对应 RID。`linux-x64` 自包含已实测，`osx-*` / `win-arm64` 未实测。

### Q：忘了 PFX 口令怎么办？

PFX 口令找不回来，只能重建。同目录的 `.crt` + `.key` 还在就能直接用（`.key` 无口令）；但 CA 口令丢了就没法 `--reuse-ca`，只能 `--force` 重建 CA，客户端要重装新 `ca.crt`。

### Q：`ca.key` 泄露了怎么办？

等于签发权泄露，拿到的人能签出任何被这张 CA 信任的证书。立刻 `--force` 重建 CA，把新 `ca.crt` 发给所有客户端，旧 CA 从信任库移除。工具没有 CRL / OCSP，没有吊销通道，只能换根。

### Q：自检第 5 项通过就代表链可信吗？

不代表。第 5 项只验证主机名匹配和回环握手，链错误是放行的；链证据是第 3 项，只在 CA 模式有。自签模式第 3 项跳过，不能用它证明外部客户端会接受这张证书。

### Q：输出目录为什么不在工程目录下？

`--out` 默认 `certs`，相对执行命令时的当前工作目录。仓库根运行得到 `<repo>/certs`，`Src/` 下运行得到 `Src/certs`。脚本和 CI 里直接传绝对路径。

## 实现与目录结构

工具是薄 CLI，证书生成逻辑在库 `SAEA.Sockets.Ssl` 里，默认值与库保持一致；PEM / DER / PFX / P7B / PKCS#8 的转换由 `OpenSsl/` 兼容层实现。工具自己负责参数解析、输出和自检。

```text
Src/SAEA.Sockets.SslTlsTool/
├── Program.cs              入口、模式分派、生成流程、汇总与信任说明输出、交互模式与参数分词
├── SslTlsToolOptions.cs    命令行解析与取值校验（字符集、保留名、范围、互斥组合）
├── CertificateVerifier.cs  五项自检，独立计算期望 SAN，回环握手
├── GlobalUsing.cs          net10 工程的 global using 声明
├── OpenSsl/                openssl 兼容子命令：分派、帮助、口令解析与各格式编解码
│   └── Commands/           req / x509 / pkcs12 / pkcs8 / pkey / crl2pkcs7 / pkcs7 各一个文件
└── SAEA.Sockets.SslTlsTool.csproj
```

`SslTlsToolOptions.TryReadValue` 把「下一个 token 以 `--` 开头」视为缺少取值，所以路径或口令本身不能以 `--` 开头，也没有 `--out=...` 这种等号写法。真遇到这种路径就换一个目录名或口令。

需要在自己的代码里生成证书时，用库里这几个类型：

- `SaeaCertificateFactory.CreateSelfSigned`
- `SaeaCertificateAuthority.Create` / `FromPfx` / `FromPem`
- `IssueServerCertificate` / `IssueClientCertificate`
- `SaeaCertificateBundle`（持有证书与私钥，负责保存）

可编程生成的完整示例见 [`../SAEA.Sockets/README.md`](../SAEA.Sockets/README.md)（英文：[`../SAEA.Sockets/README.en.md`](../SAEA.Sockets/README.en.md)）。注意三点：`Save*` 方法不会递归创建目录，先自己建好；返回对象持有私钥，用完要 `Dispose`；证书对象要活到服务端/客户端释放之后再释放。

## 安全与清理

- 这些证书只用于开发与测试。不要用在生产，也不要拿它替代 ACME / Let's Encrypt 流程——工具不做续期、不做域名验证，CSR 只在兼容子命令 `req` 里生成。
- 仓库 `.gitignore` 已忽略 `*.pfx`、`*.key` 和 `certs/`。`.crt` 不忽略，因为证书本身是公开物。
- 不要把 `.key` 或 `.pfx` 提交到版本库，也不要把口令写进脚本、日志或提交信息。
- `ca.key` 泄露等于签发权泄露：拿到它的人可以签出任何被该 CA 信任的证书。
- 用完把 CA 从信任库移除。Windows 按指纹删，Debian/Ubuntu 删 `/usr/local/share/ca-certificates/saea-dev-ca.crt` 后 `update-ca-certificates --fresh`，macOS 用 `security remove-trusted-cert`，Java 用 `keytool -delete`。
- 换机器或重建 CA 之后，旧的信任一定作废，需要重新导入新的 `ca.crt`。
- `--pwd ""` 生成的 PFX 没有口令保护，拿到文件就能用私钥，只适合临时容器或一次性环境。

## 许可证

Apache License 2.0，见仓库根目录的 [`LICENSE`](../../LICENSE)。

相关文档：

- 仓库说明：[`../../README.md`](../../README.md)（[English](../../README.en.md)）
- SAEA.Sockets 的 TLS 配置与证书说明：[`../SAEA.Sockets/README.md`](../SAEA.Sockets/README.md)（[English](../SAEA.Sockets/README.en.md)）