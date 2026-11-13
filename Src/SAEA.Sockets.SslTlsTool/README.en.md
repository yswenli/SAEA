# SAEA.Sockets.SslTlsTool

`net10.0` · Developer tool · Apache 2.0

English | [中文](README.md)

A command-line tool that produces local TLS certificates for SAEA.Sockets: one command for a single self-signed certificate, or one command for a local root CA plus the server and client certificates it issues. Output goes to `certs/` under the current working directory by default.

Besides generating certificates, it ships a set of openssl-compatible subcommands that convert certificates and keys between PEM / DER / PFX / P7B / PKCS#8; see "openssl-compatible subcommands".

> These certificates are for development and testing only. The tool does not install the CA into any trust store and never touches the system certificate stores.

## Why this exists

TLS integration failures are usually certificate problems, not handshake code: the SAN lists only the hostname and omits `127.0.0.1`; the leaf has no `serverAuth` or `clientAuth`; the CA gets rebuilt for a new machine, which invalidates the copy already in the client trust store, and every connection fails with `UntrustedRoot`. None of this shows up at compile time; it surfaces as a vague handshake failure at runtime.

This tool prevents the three common cases up front:

- Server certificates get both a `dNSName` (`localhost`) and an `iPAddress` (`127.0.0.1`) SAN, so hostname and IP validation paths are both covered.
- EKU is fixed per purpose: the server leaf carries `serverAuth`, the client leaf carries `clientAuth` and no SAN.
- `--reuse-ca` reuses the existing CA, so re-issuing leaves does not replace the root and does not invalidate trust you already imported.

## Requirements and how to run it

- .NET 10 SDK or newer; `dotnet --version` tells you what you have.
- It is not a NuGet package, so there is no `dotnet add package` step. It lives in the repository and references `Src/SAEA.Sockets` through a `ProjectReference`.
- Run it from the repository root and put tool arguments after `--`:

```bash
dotnet run --project Src/SAEA.Sockets.SslTlsTool -c Release -- --help
```

Everything before `--` belongs to `dotnet run`; everything after goes to the tool.

The default output directory is the literal string `certs`, resolved against the **current working directory of the command**, not the project directory. Run from the repository root and you get `<repo>/certs`; run from `Src/` and you get `Src/certs`. When in doubt, pass an absolute path to `--out`.

The built executable is at:

```text
Src/SAEA.Sockets.SslTlsTool/bin/Release/net10.0/SAEA.Sockets.SslTlsTool.exe
```

To use it without `dotnet run`, publish it to a fixed directory (framework-dependent, so the .NET 10 runtime must still be installed):

```bash
dotnet publish Src/SAEA.Sockets.SslTlsTool -c Release -o ./tools/ssltlstool
```

For the full publish parameters per platform (Windows/Linux/macOS, self-contained or framework-dependent, single file), see "Shipping and packaging".

## Two commands to start with

### Self-signed certificate: one server, no mTLS

```bash
dotnet run --project Src/SAEA.Sockets.SslTlsTool -c Release -- --self-signed --verify
```

Writes `certs/localhost.pfx`, `certs/localhost.crt`, `certs/localhost.key`. SAN is `localhost` + `127.0.0.1`, PFX password is `saea`, valid for 30 days.

### Root CA with mTLS

```bash
dotnet run --project Src/SAEA.Sockets.SslTlsTool -c Release -- --ca --verify
```

Writes nine files: `ca.*`, `server.*`, `client.*`. Use this when the server sets `ClientCertificateRequired = true`. The CA is valid for 3650 days, leaves for 30.

The two modes can be combined: `--self-signed --ca` produces both sets in one run.

### Real output (excerpt)

Below is actual output from `--self-signed --ca --verify`, with repeated lines removed. Paths and thumbprints vary by machine.

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

With `--ca` alone the PFX count is 3 (CA, server, client), so the self-check line reads `通过（3 份，均含私钥）`.

Reusing an existing CA adds one line:

```text
复用 CA：...\certs\ca.pfx（CA 文件不改动，server/client 将被覆盖）
```

The three `ca.*` entries then report `已存在，未改写`, while `server.*` and `client.*` report `已写入`; the exit code is 0.

## What gets written

| File | Contents | Password | Purpose |
|------|----------|----------|---------|
| `<name>.pfx` | PKCS#12 with the private key | Yes, `saea` by default | Feed to `SaeaCertificateLoader.FromPfx`; curl / Java / Node need a conversion first (commands below) |
| `<name>.crt` | Single certificate in PEM, no chain | No | Import into a client or system trust store |
| `<name>.key` | **Unencrypted private key** in PEM, `BEGIN RSA PRIVATE KEY` (PKCS#1) | No | For nginx, OpenSSL, anything that wants a separate key file |
| `server.pfx` / `client.pfx` (CA mode) | PKCS#12 with the private key and the issuing CA certificate | Yes, `saea` by default | External tools get leaf + CA in one file |
| `server.fullchain.crt` | leaf + CA, concatenated by you | No | nginx, curl, anything that needs a full chain |

For an external tool that wants PEM or JKS, convert it with this tool's compatible subcommands or an external `openssl` (full options in "openssl-compatible subcommands"):

```bash
# to PEM (private key included), ready for curl / Node
SAEA.Sockets.SslTlsTool pkcs12 -in certs/server.pfx -nodes -out certs/server.pem
# Java: import the PKCS#12 into a JKS
keytool -importkeystore -srckeystore certs/server.pfx -srcstoretype PKCS12 -destkeystore server.jks
```

The password is `saea` by default; `openssl` prompts for it on import, and `keytool` interactively asks for the source (PKCS#12) and destination (JKS) passwords. Node only reads PEM — `NODE_EXTRA_CA_CERTS` is for CAs only and cannot point at a `.pfx`.

Naming:

- Self-signed mode uses `<name>` (default `localhost`, change with `--name`). `--name` only affects self-signed mode.
- CA mode uses the fixed names `ca`, `server`, `client`; `--name` is ignored and the tool prints a hint.

Things that trip people up:

- `.key` holds an unencrypted private key. `--pwd` and the default `saea` password **only protect the PFX**; they do nothing for `.key`. Leaking `.key` means leaking the identity; leaking `ca.key` means leaking the signing authority.
- `--pwd ""` (an explicit empty string) produces a PFX with no password. The tool prints a red warning, and it never echoes the password back.
- `.crt` and `.key` always use LF line endings and UTF-8 without BOM. `.crt` holds exactly one certificate; build the chain yourself when you need one:

```bash
cat certs/server.crt certs/ca.crt > certs/server.fullchain.crt
```

```powershell
[IO.File]::WriteAllText(
    "certs\server.fullchain.crt",
    (Get-Content -Raw certs\server.crt) + (Get-Content -Raw certs\ca.crt),
    [Text.UTF8Encoding]::new($false))
```

`Get-Content -Raw` keeps the original LF line endings, and `WriteAllText` writes UTF-8 without BOM, so `Set-Content`'s default encoding/CRLF cannot corrupt the certificate file.

## Reaching the service by IP or hostname

The certificate records a hostname or IP, not a URL. The port never goes into it: `18080` only affects the server listener, so changing the port needs no re-issue.

Browsers, curl, and .NET all validate against the SAN; the CN is display only. When you connect by IP, the certificate must carry an `iPAddress` SAN, and a `dNSName` alone is rejected.

### Scenarios and options

| Scenario | Command fragment | What lands in the certificate |
|------|------|------|
| Local testing | `--self-signed` (defaults) | `DNS: localhost`, `IP: 127.0.0.1` |
| LAN IP | `--self-signed --ip 192.168.1.125` | `DNS: localhost`, `IP: 192.168.1.125` |
| Internal hostname | `--self-signed --dns api.dev.local` | `DNS: api.dev.local`, `IP: 127.0.0.1`; the CN is no longer added |
| Several names and IPs | `--dns "api.dev.local,dev.local" --ip "192.168.1.125,10.0.0.8"` | `DNS: api.dev.local,dev.local`, `IP: 192.168.1.125,10.0.0.8` |
| IPv6 | `--self-signed --ip "::1"` | `DNS: localhost`, `IP: ::1` |
| Wildcard | `--dns "*.dev.local,localhost"` | `DNS: *.dev.local,localhost` |
| Outward mTLS | `--ca --dns api.dev.local --ip 192.168.1.125` | The server leaf gets `api.dev.local` and `192.168.1.125`; the client gets no SAN |

The `--dns` examples in the table leave `localhost` out to stay short. To run them with `--verify` you have to put it back into the list, as in `--dns "api.dev.local,localhost"`; otherwise check 5 always fails with exit code 2, because that check always handshakes with `TargetHost=localhost`.

### Three commands that were actually run

```bash
# Self-signed plus a LAN IP
dotnet run --project Src/SAEA.Sockets.SslTlsTool -c Release -- --self-signed --ip 192.168.1.125 --verify
```

Exit code 0, all five checks pass; the SAN is `DNS: localhost`, `IP: 192.168.1.125`. An explicit `--ip` replaces the default `127.0.0.1` outright; both do not coexist.

```bash
# CA mode: --ip affects the server leaf only
dotnet run --project Src/SAEA.Sockets.SslTlsTool -c Release -- --ca --ip 192.168.1.125 --verify
```

Exit code 0, and check 3 prints `链校验（CustomRootTrust）：server/client 均通过`. The client certificate has no SAN, so `--ip` does nothing to it.

```bash
# Wildcard: you must add localhost yourself
dotnet run --project Src/SAEA.Sockets.SslTlsTool -c Release -- --dns "*.dev.local,localhost" --verify
```

Exit code 0, SAN is `DNS: *.dev.local,localhost`. An explicit `--dns` replaces the default `localhost`, so it has to be written back into the list.

Once the certificate exists, verify it straight from another machine instead of writing a client first:

```bash
# CA mode: point curl at ca.crt and connect by IP
curl --cacert certs/ca.crt https://192.168.1.125:18080/
# Self-signed mode: no CA, so use -k just to see the SAN and whether the handshake completes
curl -k https://192.168.1.125:18080/
```

`-k` skips chain validation only; a hostname mismatch still fails, which is what separates "trust problem" from "wrong SAN".

### How to inspect the SAN

PowerShell reads the extension directly:

```powershell
$cert = [Security.Cryptography.X509Certificates.X509Certificate2]::new("certs\server.crt")
$raw = $cert.Extensions['2.5.29.17'].RawData
[Security.Cryptography.X509Certificates.X509SubjectAlternativeNameExtension]::new($raw).Format($false)
```

OpenSSL:

```bash
openssl x509 -in certs/server.crt -noout -text | grep -A1 'Subject Alternative Name'
```

Reading `server.crt` shows the leaf itself. To see what an external client actually receives, use `openssl s_client -connect 192.168.1.125:18080 -showcerts` and look at the certificate the server sends.

### Four traps

1. Do not put an IP in `--dns`. `--dns 192.168.1.125` produces `DNS: 192.168.1.125`, `IP: 127.0.0.1`; check 5 fails with exit code 2 and the exact message `自检第 5 项失败：回环握手失败：TLS 客户端告警：bad_certificate(42) Failed to read record`. Use `--ip` for IPs.
2. An explicit `--dns` / `--ip` replaces the list, it does not append. Check 5 always uses `TargetHost=localhost`, so an explicit list must contain `localhost`, or you get exit code 2.
3. The CN does nothing for IP access in a browser; only the SAN counts. `CN=192.168.1.125` without `--ip` always fails.
4. A changed IP or machine means a re-issue. To keep imported trust, re-issue leaves with `--ca --reuse-ca`; it overwrites `server.*` / `client.*` and leaves `ca.*` alone.

### Connecting a client by IP

```csharp
var option = SocketOptionBuilder.Instance
    .SetSocket(SAEASocketType.Tcp)
    .UseStream()
    .SetIP("192.168.1.125")            // IP literal only
    .SetPort(18080)
    .WithSsl(o =>
    {
        o.TargetHost = "192.168.1.125"; // keep localhost here and localhost is still what gets validated
    })
    .Build();
```

`TargetHost` drives both SNI and hostname validation. When the hostname is an IP literal the library matches the `iPAddress` SAN only and does not fall back to the CN. Client certificates, lifetime, and trust imports are the same as in the previous section.

## Wiring it into SAEA.Sockets

Two files are enough: the server reads `server.pfx`, the client reads `client.pfx`, both with password `saea`.

### Server (requires a client certificate)

```csharp
using System.Security.Cryptography.X509Certificates;
using SAEA.Sockets;
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Model;
using SAEA.Sockets.Ssl;

// The certificate must outlive the server; do not wrap it in using here
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
    // read and write on channel.Stream
};
server.Start();
```

Drop `.UseStream()` when you do not need `GetStream()`; `WithSsl(...)` works the same way in event mode.

### Client (presents a client certificate)

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
    .SetIP("127.0.0.1")            // actual connect address, IP literal only
    .SetPort(8443)
    .WithSsl(o =>
    {
        o.TargetHost = "localhost"; // used for both SNI and hostname validation
        o.ClientCertificates = new X509Certificate2Collection(clientCertificate);
    })
    .Build();

var client = new SaeaClientSocket(option);
await client.ConnectAsync();
```

`SetIP` accepts IP literals only; the hostname goes in `TargetHost`, and `TargetHost` is what hostname validation uses when the two differ.

### Lifetime

`SaeaCertificateLoader.FromPfx` returns an `X509Certificate2` that the caller owns. Once handed to `WithSsl(...)`, it must stay alive until the server has been stopped and disposed, or the client has disconnected; otherwise the peer handshake may hit a disposed handle. Keep it in a field or a local, and dispose it after the server or client is gone. Do not wrap the whole connection lifetime in `using`.

### Default validation and the CA

`FromPfx` only loads the leaf certificate, even when `server.pfx` contains the CA as well. During the handshake the library fills in the chain from the system trust store, so:

- **If the CA is not in the client trust store, default validation fails** with `UntrustedRoot`.
- Check 5 of `--verify` (the loopback handshake) tolerates chain errors; it only proves hostname matching. It is not evidence of a valid chain; that evidence is check 3.
- To make default validation pass, import `certs/ca.crt` into the trust store, or build an `X509Chain` with `CustomRootTrust` in your test code.

Importing the CA (local development only; remove it when done):

Windows, current user:

```powershell
Import-Certificate -FilePath certs\ca.crt -CertStoreLocation Cert:\CurrentUser\Root
# remove by thumbprint
Get-ChildItem Cert:\CurrentUser\Root | Where-Object { $_.Thumbprint -eq '<THUMBPRINT>' } | Remove-Item
```

For services or machine-wide trust, use `Cert:\LocalMachine\Root` (administrator required).

Debian / Ubuntu and macOS:

```bash
# Debian / Ubuntu
sudo cp certs/ca.crt /usr/local/share/ca-certificates/saea-dev-ca.crt
sudo update-ca-certificates
# remove
sudo rm /usr/local/share/ca-certificates/saea-dev-ca.crt && sudo update-ca-certificates --fresh

# macOS
sudo security add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain certs/ca.crt
# remove
sudo security remove-trusted-cert -d certs/ca.crt
```

Java and Node do not read the system trust store, so importing there has no effect on them:

```bash
# Java
keytool -importcert -cacerts -alias saea-dev-ca -file certs/ca.crt
keytool -delete -cacerts -alias saea-dev-ca    # remove

# Node
export NODE_EXTRA_CA_CERTS=certs/ca.crt
```

Recent Node versions can also use `--use-system-ca` to follow the system store.

## Shipping and packaging

### Distributing certificates per target system

| Recipient | Give them | Never give them |
|------|------|------|
| SAEA / Kestrel server | `server.pfx`, or `server.fullchain.crt` + `server.key` | `ca.key` |
| Client doing mTLS | `client.pfx` | `ca.key`, `server.key` |
| Client that only needs to trust the server | `ca.crt` and nothing else | any `.key`, any `.pfx` |
| nginx / curl / Node | `server.fullchain.crt` + `server.key` | a password-protected PFX |
| Java | a JKS or PKCS#12 converted from `server.pfx` | `ca.key` |

Pack and checksum:

```bash
tar -czf saea-certs.tar.gz certs/ca.crt certs/server.fullchain.crt certs/server.key
sha256sum saea-certs.tar.gz
```

```powershell
Get-FileHash saea-certs.tar.gz -Algorithm SHA256
```

Do not put `ca.key` in the archive. `certs/` is already ignored by `.gitignore`, and the archive itself should not be committed either.

Windows has two routes. The app reads the PFX directly and permissions come from the file ACL, or you import it into the machine store:

```powershell
$pwd = ConvertTo-SecureString 'saea' -AsPlainText -Force
Import-PfxCertificate -FilePath certs\server.pfx -CertStoreLocation Cert:\LocalMachine\My -Password $pwd
```

IIS / Http.Sys additionally needs private-key read access for the app pool account: in `certlm.msc` pick the certificate → All Tasks → Manage Private Keys, or use `WinHttpCertCfg.exe`. Writing to `LocalMachine` needs an elevated PowerShell; deleting it in `certlm.msc` revokes the machine-local trust.

On Linux put things under `/etc/ssl/saea/`:

```bash
sudo install -d -m 755 /etc/ssl/saea
sudo install -m 644 certs/ca.crt certs/server.fullchain.crt /etc/ssl/saea/
sudo install -m 600 certs/server.key /etc/ssl/saea/
sudo chown root:root /etc/ssl/saea/*
```

nginx needs two lines:

```nginx
ssl_certificate     /etc/ssl/saea/server.fullchain.crt;
ssl_certificate_key /etc/ssl/saea/server.key;
```

With mode 600 and owner root, only root can read the key. A systemd service running as a non-root user needs group read (`chown root:saea` plus 640) or an ACL; otherwise nginx or the app fails to start with `Permission denied`.

In containers, mount read-only instead of `COPY`-ing certs into the image:

```bash
docker run --rm -v "$PWD/certs:/certs:ro" -p 18080:18080 saea-app
```

For Kubernetes, mount a Secret at `/certs` with `readOnly: true` (the concrete manifest was not tested here; the usual Secret volume works). To trust this CA inside the container, use `update-ca-certificates` on Debian/Ubuntu/Alpine and `update-ca-trust` on RHEL-family images.

`--pwd` shows up in the process list and the shell history, and in containers it is easy to pull back out with `docker inspect`. The tool takes the password on the command line only: no environment variable and no interactive prompt, so there is no way around passing it there. Prefer generating at build time over runtime; if it has to be runtime, use a read-only mount plus 600 permissions. `--pwd ""` is only for throwaway environments, and only with 600 permissions and a read-only mount.

Client trust installation: the file to install is always `ca.crt` (or the self-signed certificate itself in self-signed mode), never a PFX:

| Target | Which file | Entry point |
|------|------|------|
| Windows, current user | `certs\ca.crt` | `Import-Certificate -FilePath certs\ca.crt -CertStoreLocation Cert:\CurrentUser\Root` |
| Debian / Ubuntu | `certs/ca.crt` | `cp` into `/usr/local/share/ca-certificates/`, then `update-ca-certificates` |
| macOS | `certs/ca.crt` | `security add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain certs/ca.crt` |
| Firefox (Linux) | `certs/client.pfx` | `pk12util -i certs/client.pfx -d sql:$HOME/.mozilla/firefox/<profile>` |
| Java | `certs/ca.crt` | `keytool -importcert -cacerts -alias saea-dev-ca -file certs/ca.crt` |
| Node | `certs/ca.crt` | `NODE_EXTRA_CA_CERTS=certs/ca.crt` |

Apart from the Firefox row, these commands and their removal steps are in "Default validation and the CA" above; what changes on the packaging side is the file you ship: `ca.crt`, not a PFX, and `client.pfx` goes only to browsers or apps that do mTLS.

Validity and rotation: leaves default to 30 days, the CA to 3650. Re-issue leaves with `--ca --reuse-ca` before they expire; clients then swap `server.*` only. As long as `ca.crt` is unchanged, no trust store needs updating. The tool issues no CRL or OCSP, so a leaked key or a certificate that must be revoked means rebuilding the CA and redistributing it.

### Publishing the tool itself

| RID | Mode | Output | Target machine | Status |
|------|------|------|------|------|
| `win-x64` | Framework-dependent single file | `SAEA.Sockets.SslTlsTool.exe`, about 7.2 MB | .NET 10 runtime | Tested |
| `linux-x64` | Self-contained single file | one ELF binary, about 80.6 MB | none | Tested, cross-published from Windows |
| `osx-x64` | Self-contained single file | not tested | not tested | Not tested |
| `osx-arm64` | Self-contained single file | not tested | not tested | Not tested |
| `win-arm64` | Self-contained single file | not tested | not tested | Not tested |

musl distributions such as Alpine cannot use `linux-x64`; publish `linux-musl-x64` instead (not tested). Reserve `linux-x64` for glibc distributions.

Run these from the repository root:

```bash
# Windows x64: framework-dependent, about 7.2 MB, needs the .NET 10 runtime
dotnet publish Src/SAEA.Sockets.SslTlsTool -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./tools/win-x64

# Linux x64: self-contained, about 80.6 MB, no runtime needed
dotnet publish Src/SAEA.Sockets.SslTlsTool -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o ./tools/linux-x64

# Apple Silicon (not tested): the RID must match the target architecture
dotnet publish Src/SAEA.Sockets.SslTlsTool -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -o ./tools/osx-arm64
```

`-r` is the target RID: `osx-x64` for Intel Macs, `osx-arm64` for Apple Silicon, `win-arm64` for ARM Windows. Pick wrong and it either will not start or fails with a format error. `--self-contained false` keeps the output small but the target needs a runtime; `true` bundles the runtime into the single file, hence 80 MB and up. `-p:PublishSingleFile=true` only changes packaging, not behaviour.

Cross-publishing: publishing `linux-x64` self-contained from Windows was tested and works. On the target, `chmod +x ./SAEA.Sockets.SslTlsTool` and run it; start with `--help` to confirm it launches.

NativeAOT and trimming are not recommended: neither path was validated for this tool, and BouncyCastle reflection may throw once trimmed. If you want them, first get `--ca --verify` passing on the target RID yourself.

Published output versus `dotnet run`: the output needs neither the repository nor the SDK and can be copied anywhere; but `--out` still resolves against the runtime working directory, so pass an absolute path when running it elsewhere.

## Options

Every option takes the form `--key value`; boolean switches take no value. A value may not start with `--`, or it is treated as the next option and rejected as a missing value.

### Mode (at least one)

| Option | Description |
|--------|-------------|
| `--self-signed` | Produce a single self-signed certificate |
| `--ca` | Produce a root CA plus server and client certificates |

Both may be given. With neither, the tool prints help and exits with code 1.

### Output and naming

| Option | Default | Description |
|--------|---------|-------------|
| `--out <dir>` | `certs` | Output directory, created recursively if missing; relative to the current working directory. If the path is an existing file, exit code 2 |
| `--name <base>` | `localhost` | File base name in self-signed mode, also used as the CN when `--cn` is absent. Only `[A-Za-z0-9._-]{1,100}`, not `.` or `..`, and not the reserved names `ca` / `server` / `client` (case-insensitive). Self-signed mode only |
| `--cn <subject>` | `CN=localhost` | Full subject DN. Affects the self-signed certificate and the CA-mode server leaf; not the CA or client. When omitted, self-signed mode uses `CN=<name>` (`--name`, default `localhost`) and CA mode fixes it at `CN=localhost` |
| `--ca-cn <subject>` | `CN=SAEA Dev Root CA` | CA mode only: CA subject |
| `--client-cn <subject>` | `CN=saea-client` | CA mode only: client subject |

BouncyCastle re-renders the DN after parsing, so the displayed order is the reverse of the input:

```text
--cn "CN=localhost, O=SAEA"   →   主题： O=SAEA, CN=localhost
```

That is expected; the CN is still there.

### Certificate contents

| Option | Default | Description |
|--------|---------|-------------|
| `--dns <a,b>` | Derived from the CN | Applies to server-purpose certificates (self-signed and the CA-mode server leaf). When omitted: if the CN exists and is not an IP literal, the SAN is `CN` + `localhost` (deduplicated); otherwise `localhost`. When given, it replaces the list entirely and the CN is **not** added back. Comma-separated; no whitespace or commas inside a name; each name at most 253 characters; internationalized domain names are not supported |
| `--ip <a,b>` | `127.0.0.1` | Applies to server-purpose certificates. When given, it replaces the list entirely. IPv4 and IPv6 are both accepted; each value goes through `IPAddress.TryParse` |
| `--days <n>` | `30` | Leaf valid for n days, range 1..36500. With `--ca` and no `--reuse-ca`, it may not exceed `--ca-days` |
| `--ca-days <n>` | `3650` | CA valid for n days, range 1..36500, CA mode only |
| `--keysize <n>` | `2048` | RSA key size; only `2048`, `3072`, `4096` |

Generation is always RSA with SHA-256. `NotBefore` is the generation time minus one day (clock-skew margin) and `NotAfter` is the generation time plus the configured days, so the actual span is one day longer than `--days`. The CA is `CA:TRUE` with `pathLen=0`; leaves are `CA:FALSE`.

`--dns ""` and `--ip ""` are rejected with a hint to omit the option instead.

### Passwords

| Option | Default | Description |
|--------|---------|-------------|
| `--pwd <password>` | `saea` | PFX password. An explicit empty string means no password, which is risky and the tool says so. The password is never echoed |
| `--ca-pwd <password>` | Falls back to `--pwd` | Used only with `--reuse-ca`, to read the existing CA |

### Existing files

| Option | Description |
|--------|-------------|
| `--reuse-ca` | Reuse `<out>/ca.pfx` (falling back to `ca.crt` + `ca.key`) to issue new leaves. Requires `--ca` and is mutually exclusive with `--force`. **Overwrites server/client unconditionally**; CA files are left untouched. `--ca-cn` and `--ca-days` are ignored with a hint. If the new leaves would expire after the existing CA, exit code 1 |
| `--force` | Overwrite existing files. When a CA already exists, it warns that imported trust will be invalidated. Mutually exclusive with `--reuse-ca` |

Behaviour outside those two options:

- Self-signed mode decides per file: an existing file is reported as `skipped` and the rest are still written. If all three are skipped, the exit code is 3 (and with `--verify`, the checks must still pass).
- In CA mode, if any of `ca.pfx`, `ca.crt`, `ca.key` exists and neither `--reuse-ca` nor `--force` is given, the tool fails with exit code 1 instead of silently rebuilding the CA.

### Other

| Option | Description |
|--------|-------------|
| `--verify` | Run the five self-checks after generation; any failure means exit code 2 |
| `--help` | Print help only; even alongside other arguments it prints help and exits 0 |

## openssl-compatible subcommands

When the first argument is exactly `req`, `x509`, `pkcs12`, `pkcs8`, `pkey`, `crl2pkcs7`, `pkcs7`, or `version`, the tool takes the compatible branch; anything else goes to the generate/verify branch above. Matching is case-sensitive and only looks at the first argument.

Commands below are written as `SAEA.Sockets.SslTlsTool <subcommand> ...`. From the repository root, prefix them with `dotnet run --project Src/SAEA.Sockets.SslTlsTool -c Release --`, or add the `dotnet publish` output to `PATH` and call the executable directly.

### Commands and options

#### req / x509 / pkcs8 / pkey / pkcs12 / crl2pkcs7 / pkcs7 / version

**`req`** generates a CSR, or self-signs a certificate directly.

| Option | Description |
|--------|-------------|
| `-new` / `-x509` | Pick one: emit a CSR / self-sign a certificate |
| `-newkey rsa:<bits>` | Generate a 1024 / 2048 / 3072 / 4096-bit RSA key on the spot; requires `-keyout` |
| `-key` / `-keyout` | Use an existing key / write the new key here |
| `-nodes` / `-noenc` | Write the key unencrypted; mutually exclusive with `-passout` |
| `-days` | Validity in days for `-x509`, default 30 |
| `-subj` | Subject DN in `/CN=localhost/O=SAEA` form; `,` also works as the separator |
| `-addext` | Extra extension, repeatable; only `subjectAltName`, `basicConstraints`, `keyUsage`, `extendedKeyUsage` |
| `-sha256` / `-out` / `-passout` | Signature digest (the only choice) / output path / passphrase source for the key |

**`x509`** reads a certificate, prints the requested fields, and converts between PEM and DER; with `-req` it signs a CSR using a CA.

| Option | Description |
|--------|-------------|
| `-in` / `-inform` | Input file / `PEM` or `DER`, default stdin / PEM |
| `-out` / `-outform` | Output file / `PEM` or `DER`, default stdout / PEM |
| `-noout` | Suppress the certificate body, print only the requested fields |
| `-subject` / `-issuer` / `-dates` / `-serial` / `-fingerprint` | Print subject, issuer, validity, serial, fingerprint |
| `-sha256` / `-pubkey` | Switch the fingerprint to SHA-256 / print the public key in PEM |
| `-req` / `-CA` / `-CAkey` | Treat the input as a CSR and sign it with the given CA certificate and key |
| `-passin` / `-CAcreateserial` / `-days` | CA key passphrase / write `<CA file name>.srl` / validity of the new certificate |
| `-extfile` / `-extensions` / `-copy_extensions` | Extension file / section name / copy extensions from the CSR (`copy` or `none`) |

`-text` and `-nameopt` are not supported: use the print options above instead, and names stay in oneline form (`CN = localhost, O = SAEA`).

**`pkcs8`** converts a key between PKCS#1 and PKCS#8.

| Option | Description |
|--------|-------------|
| `-topk8` | Emit PKCS#8; without it the output is PKCS#1 |
| `-nocrypt` | Emit unencrypted PKCS#8 |
| `-v1 PBE-SHA1-3DES` | Legacy PBE encryption |
| `-v2` | `aes-128-cbc` / `aes-192-cbc` / `aes-256-cbc` / `des3`, default `aes-256-cbc` |
| `-in` / `-out` / `-inform` / `-outform` | I/O and formats, default stdin / stdout / PEM / PEM |
| `-passin` / `-passout` | Read / write passphrase sources |

**`pkey`** reads a key and emits PKCS#8 (default), PKCS#1, or the public key.

| Option | Description |
|--------|-------------|
| `-traditional` | Emit PKCS#1; mutually exclusive with the cipher options below |
| `-pubout` | Emit the public key |
| `-aes-128-cbc` / `-aes-192-cbc` / `-aes-256-cbc` / `-des3` | Encrypt the emitted PKCS#8; pick at most one |
| `-in` / `-out` / `-inform` / `-outform` | I/O and formats, default stdin / stdout / PEM / PEM |
| `-passin` / `-passout` | Read / write passphrase sources |

`-pubout` cannot be combined with `-traditional` or a cipher; passing `-passout` without a cipher fails instead of silently writing plaintext.

**`pkcs12`** exports a PFX or splits one into PEM.

| Option | Description |
|--------|-------------|
| `-export` | Export a PFX; without it the command reads one |
| `-in` | Leaf certificate when exporting, the PFX when reading; default stdin |
| `-inkey` | Private key used for export |
| `-certfile` | Extra certificate to pack, repeatable |
| `-name` | friendlyName; written only when given |
| `-passout` / `-passin` | Write / read passphrase sources |
| `-legacy` | Switch to the old RC2-40 / 3DES / SHA-1 family |
| `-nodes` | No encryption; when reading, emit the key in plaintext |
| `-nokeys` / `-nocerts` / `-clcerts` / `-cacerts` | Read filters: skip the key / key only / leaf only / chain only |
| `-nomac` | Omit the MAC on export; mutually exclusive with `-passout` |
| `-info` | Write a summary to stderr |

The default is AES-256-CBC with PBKDF2 (2048 iterations) and an HMAC-SHA-256 MAC; `-legacy` switches to `PBE-SHA1-RC2-40` for certificates, `PBE-SHA1-3DES` for keys, and SHA-1 for the MAC, still with 2048 iterations. When reading, the output order is key (PKCS#8), leaf, then chain.

**`crl2pkcs7`** packs certificates into a certs-only PKCS#7.

| Option | Description |
|--------|-------------|
| `-nocrl` | Required; declares that no CRL is read |
| `-certfile` | Certificate to include, repeatable |
| `-out` / `-outform` | Output file / `PEM` or `DER`, default stdout / PEM |

**`pkcs7`** reads a PKCS#7 bundle and prints every certificate in it.

| Option | Description |
|--------|-------------|
| `-in` / `-inform` | Input file / `PEM` or `DER`, default stdin / PEM |
| `-print_certs` | Print every certificate in the bundle as PEM |
| `-out` / `-outform` | Output file / `PEM` or `DER`; DER requires `-out` |
| `-noout` | No-op: this tool does not print `subject=` / `issuer=` lines |

**`version`** prints the tool version and states that it is not compatible with `openssl version`.

| Option | Description |
|--------|-------------|
| `-help` | Print help for this command and exit 0 |

### Format matrix

| Input | Target | Command | Notes |
|-------|--------|---------|-------|
| PEM certificate | DER certificate | `x509 -out cert.der -outform DER` | One certificate per run |
| DER certificate | PEM certificate | `x509 -inform DER -out cert.pem` | The input format must be stated explicitly |
| PKCS#1 / PKCS#8 key | PKCS#1 | `pkey -traditional` | Legacy `Proc-Type: 4,ENCRYPTED` is read-only |
| PKCS#1 / PKCS#8 key | Encrypted PKCS#8 | `pkcs8 -topk8 -v2 aes-256-cbc` | `aes-256-cbc` is the default when `-v2` is omitted |
| PFX | PEM (key + certificates) | `pkcs12 -in ... -nodes -out ...` | Output order: key, leaf, chain |
| PEM certificate + key | PFX | `pkcs12 -export -in ... -inkey ...` | Add the chain with `-certfile` |
| PEM certificate / chain | P7B | `crl2pkcs7 -nocrl -certfile ...` | Certificates only, no private key |
| P7B | PEM certificates | `pkcs7 -print_certs` | No `subject=` / `issuer=` lines |
| CSR | Certificate | `x509 -req -CA ... -CAkey ...` | Without a CA, self-sign with `req -x509` first |

### Common examples

DER certificate to PEM (`-inform` declares the input; the output stays PEM by default):

```bash
SAEA.Sockets.SslTlsTool x509 -inform DER -in cert.der -out cert.pem
```

PKCS#8 key to PKCS#1 (`pkey` emits PKCS#8 by default; `-traditional` switches to PKCS#1):

```bash
SAEA.Sockets.SslTlsTool pkey -in key.p8 -traditional -out key.pem
```

PFX to PEM (`-nodes` writes the key in plaintext; order is key, leaf, chain):

```bash
SAEA.Sockets.SslTlsTool pkcs12 -in server.pfx -nodes -out server.pem
```

Certificate plus key into a PFX (add the chain with `-certfile`; without `-passout` the tool prompts):

```bash
SAEA.Sockets.SslTlsTool pkcs12 -export -in server.crt -inkey server.key -out server.pfx
```

Export with the legacy algorithm family (for old systems that only speak RC2 / 3DES):

```bash
SAEA.Sockets.SslTlsTool pkcs12 -export -in server.crt -inkey server.key -legacy -out server-legacy.pfx
```

Generate and sign a CSR (`req` produces the CSR and key, then the CA signs it):

```bash
SAEA.Sockets.SslTlsTool req -new -newkey rsa:2048 -keyout key.pem -subj "/CN=localhost" -out req.csr
SAEA.Sockets.SslTlsTool x509 -req -in req.csr -CA ca.crt -CAkey ca.key -CAcreateserial -days 365 -out server.crt
```

Pack and unpack a P7B (`crl2pkcs7` packs, `pkcs7 -print_certs` unpacks to PEM):

```bash
SAEA.Sockets.SslTlsTool crl2pkcs7 -nocrl -certfile server.crt -certfile ca.crt -out chain.p7b
SAEA.Sockets.SslTlsTool pkcs7 -in chain.p7b -print_certs -out chain.pem
```

### Differences from openssl

| Item | openssl | This tool | Impact |
|------|---------|-----------|--------|
| `Bag Attributes` | `pkcs12` writes attribute lines such as friendlyName | Not written | Scripts that read friendlyName from those lines break |
| `pkcs7 -print_certs` summary lines | Prints `subject=` / `issuer=` before each certificate | Writes PEM blocks only | `-noout` becomes a no-op; line-based parsers need changes |
| `pkcs7 -print_certs -outform DER` | Accepts the combination | Fails (`-print_certs` emits PEM text only) | The combination has no meaning; split the steps |
| `crl2pkcs7 -nocrl` without `-certfile` | Produces an empty certs-only bundle | Fails | Scripts that only build the empty bundle break, and the artifact has no real use |
| Key encryption when reading `pkcs12` | Re-encrypts with `-passout`, prompting when absent | Re-encrypts with the `-passin` passphrase; read mode rejects `-passout`, `-nodes` emits plaintext | Scripts with different read/write passphrases need `-nodes` or two steps |
| `pkcs12 -nodes` and the MAC | Disables encryption only, still writes a keyed MAC | `-nodes` / `-nomac` both omit the MAC and are mutually exclusive with `-passout` | A passphrase-less PFX cannot be integrity-checked; read it back directly |
| `x509 -text` | Full ASN.1 dump | Unsupported; fails and points at alternatives | Use an external tool when a dump is needed |
| `-nameopt` | Switches between RFC2253 / multiline and more | Unsupported; always oneline | Scripts that need RFC2253 must convert themselves |
| `-config` | Builds certificates from a config file | Unsupported; only `-extfile` / `-addext` | Existing `openssl.cnf` flows must become command lines |
| `req -newkey` without `-nodes` / `-passout` | Prompts for a passphrase and encrypts the key; non-interactive runs fail | Writes plaintext and warns on stderr | Never commit a plaintext key; pass `-passout` explicitly to encrypt |
| `version` | `OpenSSL 3.x.y …` | `<tool name> <version> (openssl 兼容子集)` | Version probes may misread it |
| PFX defaults | AES-256-CBC for certificates and keys, PBKDF2 with 2048 iterations, HMAC-SHA-256 MAC with 2048 iterations, 8-byte PBE and MAC salts | Same, hard-coded; `-legacy` uses RC2-40 / 3DES / SHA-1 | The parameters match openssl 3, so `openssl pkcs12 -info` shows the same algorithms, iteration counts, and salt length; the bytes still differ because salts and IVs are random |
| `-info` text | Fixed wording | Same information, different wording | Regexes keyed on `MAC:` and similar fields may fail |

Bottom line: programs that only read PEM / DER / PFX / P7B (.NET, Java, nginx, IIS) are unaffected; scripts that parse text output with regexes may be. Salts and IVs are random, so no two exports are byte-identical; this tool guarantees the same parameters, structure, entry order, and friendlyName rules.

### Exit codes

Dispatch is decided by the first argument only, case-sensitively. If it is one of `req`, `x509`, `pkcs12`, `pkcs8`, `pkey`, `crl2pkcs7`, `pkcs7`, `version`, the compatible branch runs; if it starts with `-` or there are no arguments, the legacy generate/verify branch runs.

Compatible branch:

| Code | Meaning |
|------|---------|
| `0` | The command succeeded; `-help` also exits 0 |
| `1` | Any failure: unknown subcommand, unknown option, invalid value, file I/O failure, wrong passphrase |

Legacy branch:

| Code | Meaning |
|------|---------|
| `0` | At least one new file was written; with `--verify`, self-checks passed |
| `1` | Argument error |
| `2` | Generation failed, reading the CA failed, writing failed, or a `--verify` check failed |
| `3` | No file was written (all existed and were skipped) |

### Passphrase sources

`-passin` and `-passout` share one syntax:

| Form | Example | Notes |
|------|---------|-------|
| `pass:<text>` | `-passout pass:saea` | The plaintext lands in shell history and the process list; prefer the next two |
| `env:<name>` | `-passin env:PFX_PASS` | A missing or empty variable counts as no passphrase and does not fall back to a prompt |
| `file:<path>` | `-passin file:pass.txt` | Reads the first line and strips the trailing CR / LF; a missing or empty file fails |
| `stdin` | `-passin stdin` | Reads one line; disabled in interactive mode |

If an operation needs a passphrase and no source is given, the tool prompts when stdin is not redirected (input is not echoed); otherwise it fails with `需要口令：请用 -passin/-passout，或从交互终端运行。` and exits 1. An empty passphrase is valid for PFX and rejected for encrypted PKCS#8.

### How to verify

1. Run the full compatibility suite: `pwsh Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1`.
2. `-ToolPath` points at an already-built `SAEA.Sockets.SslTlsTool.exe` (from `dotnet publish` or a custom `-o` directory); without it the script compiles and runs via `dotnet run --project Src/SAEA.Sockets.SslTlsTool -c Release --`.
3. Expect a final line of `N passed, 0 failed` and exit code 0; `N` follows the number of cases in the script (currently 33).
4. If `openssl` is installed locally, replay the same parameters by hand (`openssl x509 -in ...`, `openssl pkcs12 -export ...`); the script does no diffing, and `SAEA_OPENSSL` is not a script argument.

## Interactive mode

Start it with no arguments at all:

```bash
dotnet run --project Src/SAEA.Sockets.SslTlsTool -c Release
```

Each line takes exactly the same arguments as the command line, runs on Enter, and the prompt returns.

| Input | Behaviour |
|-------|-----------|
| `help` | Print the full help and continue |
| `exit` / `quit` / EOF (Ctrl+Z then Enter, or Ctrl+D) | Exit; the process exit code is 0 |
| `--help` | Print help and continue |
| Empty line | Ignored |
| Unclosed quote | Prints `命令里的双引号没有闭合。`, skips that line, continues |

Quoting rules:

- Arguments are separated by whitespace; double quotes wrap values containing spaces, e.g. `--cn "CN=localhost, O=SAEA"` or `--out "D:\dev certs"`.
- Inside quotes only `\"` is an escape; backslashes are preserved as-is, so a UNC path like `\\server\share` does not lose a separator.

A failed command does not end the session; the tool prints that run's exit code:

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

It is convenient when you are trying options by hand. In scripts use command-line mode, which is the only mode that returns the real exit code.

## What `--verify` checks

Checks run right after generation and stop at the first failure, printing `自检第 N 项失败：...` and returning exit code 2.

| Check | What it does | Notes |
|-------|--------------|-------|
| 1/5 | Reads every PFX back with `SaeaCertificateLoader.FromPfx` and asserts `HasPrivateKey` | 1 PFX for self-signed, 3 for CA mode, 4 when both are requested |
| 2/5 | Reads `.crt` and `.key` back with `FromPem` and asserts the private key is present; asserts byte-wise that there is no `CR` and no UTF-8 BOM | Same count as check 1 |
| 3/5 | Chain validation: `X509Chain` with `CustomRootTrust`, the CA placed in `CustomTrustStore`, `Build` succeeds for both server and client | CA mode only. Self-signed mode prints "not applicable, skipped" |
| 4/5 | CA has `BasicConstraints.CA == true`; the server SAN matches the expected DNS/IP names and its EKU contains `serverAuth`; the client has no SAN and its EKU contains `clientAuth` | Expected values are computed by the tool from the command line, not by reusing the library's derivation code |
| 5/5 | Starts a SAEA server in-process (`ClientCertificateRequired = true`) and a client (`TargetHost = "localhost"`, client certificate attached), echoes one frame on an OS-assigned free port | Tolerates chain errors only; a hostname mismatch must fail. Self-signed mode uses the same certificate on both ends |

Check 3 and check 5 are not interchangeable:

- Check 3 is the only chain evidence; it explicitly makes the CA the trust root.
- Check 5's callback accepts everything except a hostname mismatch, so it proves hostname matching, a completed handshake, and a working echo — not that the chain is trusted. Its output says so: "hostname match passed, chain not validated; see check 3".

Self-signed mode has no CA, so check 3 is skipped, and check 5 uses the same certificate on both sides. Passing there means the configuration is self-consistent, not that any external client will accept the certificate.

## Exit codes

| Code | Meaning |
|------|---------|
| `0` | At least one new file was written; with `--verify`, the checks passed |
| `1` | Argument error: unknown option, missing value, out-of-range value, conflicting options, reserved name, `ca.*` already present without `--reuse-ca` / `--force`, or no mode selected |
| `2` | Generation failed, reading the CA failed, writing failed, `--out` is an existing file, or `--verify` failed |
| `3` | No file was written (everything already existed and was skipped); with `--verify`, the checks passed |

Priority is 1 > 2 > 0/3: argument errors return 1 before generation starts, and 3 only happens when nothing went wrong but nothing was written either.

**3 is not a failure.** It means the run produced no new files, which is normal when you repeat the same command. With `--verify`, the existing files on disk are still checked.

PowerShell:

```powershell
dotnet run --project Src/SAEA.Sockets.SslTlsTool -c Release -- --ca --verify
$code = $LASTEXITCODE
switch ($code) {
    0 { Write-Host "Certificates written" }
    3 { Write-Host "Certificates already exist; nothing new written" }
    default { throw "Certificate generation failed with exit code $code" }
}
```

bash:

```bash
dotnet run --project Src/SAEA.Sockets.SslTlsTool -c Release -- --ca --verify
code=$?
case "$code" in
    0) echo "Certificates written" ;;
    3) echo "Certificates already exist; nothing new written" ;;
    *) echo "Certificate generation failed with exit code $code" >&2; exit "$code" ;;
esac
```

In CI you can accept only 0, or treat 3 as "already provisioned"; it depends on whether the run must produce new files.

## Troubleshooting

| Symptom / message | Cause | Fix |
|-------------------|-------|-----|
| `CA 文件已存在：<path>。要复用请加 --reuse-ca，要重建请加 --force。` | `ca.pfx` / `ca.crt` / `ca.key` already exist and neither reuse nor overwrite was requested | Add `--reuse-ca` to re-issue leaves, or `--force` to rebuild the CA (invalidates imported trust) |
| `参数 --reuse-ca 与 --force 互斥，只能选一个。` | Both were given | Pick one: reuse the old CA, or rebuild it |
| `参数 --reuse-ca 必须与 --ca 同时使用。` | `--reuse-ca` without `--ca` | Add `--ca` |
| `参数 --days 不能大于 --ca-days：新叶子的到期时间会晚于 CA。` | `--days` exceeds `--ca-days` | Lower `--days`, or raise `--ca-days` to at least the same value (max 36500) |
| `新叶子的到期时间晚于现有 CA（到期时间 ...，本地时间）。请减小 --days，或加 --force 重建 CA。` | The reused CA expires soon | Lower `--days`, or rebuild the CA with `--force` |
| `参数 --name 不能使用保留名 ca/server/client。` | CA mode owns those three names, and self-signed mode may not take them either | Pick another base name, e.g. `--name devbox` |
| `参数 --name 只允许 1..100 个 [A-Za-z0-9._-] 字符。` | The base name contains spaces, non-ASCII characters, or a path separator | Use letters, digits, dot, underscore, hyphen |
| `参数 --ip 的取值不是合法的 IP 地址：not-an-ip。` | A non-IP value in `--ip` | Use IPv4/IPv6 literals, comma-separated |
| `参数 --dns 的取值为空；要使用默认值请省略该参数。` | `--dns ""` was passed | Omit `--dns` instead |
| `参数 --dns 的取值无效：...（不能含空白或逗号，长度不超过 253）。` | A name contains whitespace, or an internationalized domain name was used | Remove the whitespace and use ASCII names |
| `找不到可复用的 CA：既没有 ...\ca.pfx，也没有 ca.crt + ca.key。请先用 --force 生成 CA。` | `--reuse-ca` but no CA in the output directory | Run `--ca` once (or `--ca --force`) first |
| `读取 CA 失败（口令错误、文件损坏或证书不是 CA）：...` | The old CA password differs from the current `--pwd`, or the file was replaced | Pass the old password with `--ca-pwd`; check the file; rebuild with `--force` as a last resort |
| `输出路径是一个已存在的文件，不能作为目录使用：...` | `--out` points at a file | Give it a directory path |
| `缺少 --self-signed 或 --ca，未执行任何生成。` | Only options like `--out` or `--verify` were given | Add a mode option |
| Subject prints as `O=SAEA, CN=localhost` | BouncyCastle renders the DN in reverse order | Expected; the CN is still present |
| `.key` is treated as an encrypted key, or someone expects it to need `saea` | `.key` is an unencrypted PKCS#1 key; the password only protects the PFX | `openssl rsa -in certs/server.key -noout -check` reads it directly; do not pass `-passin` |
| Client reports `UntrustedRoot` or `The remote certificate is invalid` | `ca.crt` is not in the client trust store | Import `ca.crt`, or use `CustomRootTrust` in test code |
| Java / Node still reject the certificate | They do not read the system trust store | Java: `keytool -importcert`; Node: `NODE_EXTRA_CA_CERTS` |
| `自检第 5 项失败：回环握手失败：...` | Port contention, handshake timeout, or a certificate that no longer matches `TargetHost` | Re-run (the port changes); if it persists, check that `--cn` / `--dns` still cover `localhost` |

Paths in error messages are absolute, so you can copy them straight into a file manager or shell.

## FAQ

### Q: A colleague needs `https://192.168.1.125:18080`. How do I generate the certificate?

Use `--self-signed --ip 192.168.1.125`, or `--ca --ip 192.168.1.125` for CA mode. The default `127.0.0.1` is replaced outright; `--dns` still defaults to `localhost`, which is what gets check 5 through, so do not drop it.

### Q: Does the port belong in the certificate?

No. The certificate holds hostnames and IPs only; the port comes from the listener and the URL, so moving off `18080` needs no re-issue. The SAN has no port field at all.

### Q: Why does `--dns 192.168.1.125` not work?

It writes a `dNSName`, a string SAN, while IP access looks only at `iPAddress` SANs. The observed SAN becomes `DNS: 192.168.1.125`, `IP: 127.0.0.1`, check 5 fails, exit code 2. Use `--ip` for IPs.

### Q: The browser still says "not secure" or the certificate is invalid.

Two cases. Self-signed mode has no CA, so the self-signed certificate itself must go into the trust store (that mode produces no `ca.crt`); CA mode needs `ca.crt` in the system or browser trust store. A name-mismatch error is not a trust problem; the SAN is wrong.

### Q: Why does the client report `UntrustedRoot`?

`ca.crt` is not in the client trust store, or an old CA from before a rebuild is. The library fills in the root from the system store during the handshake, so a missing root fails here. Java / Node do not read the system store; import there or set `NODE_EXTRA_CA_CERTS`.

### Q: Which files do I hand out for mTLS?

Server: `server.pfx` (or `server.fullchain.crt` + `server.key`) plus `ca.crt`. Client: `client.pfx` plus `ca.crt`. `ca.key` goes to nobody, and `server.key` only to tools that want a separate key file.

### Q: Does `--pwd` protect `.key`?

No. `--pwd` encrypts the PFX only; `.key` is an unencrypted PKCS#1 private key. To move a key around, encrypt the whole package or use key management; the password will not help.

### Q: Why exit code 3 on a repeated run?

Every target file already exists and `--force` was not given, so nothing was written. 3 is not a failure; with `--verify` the existing files are still checked. Add `--force` to overwrite, or `--ca --reuse-ca` to re-issue leaves only.

### Q: What is the difference between `--force` and `--reuse-ca`?

`--force` rebuilds the CA, invalidates the old `ca.crt`, and every client must re-import trust. `--reuse-ca` keeps `ca.*` and overwrites only `server.*` / `client.*`, so trust stores stay valid. They are mutually exclusive.

### Q: How do I give the `.pfx` to nginx / curl / Java / Node?

nginx wants `server.fullchain.crt` + `server.key`, and curl is simplest with the very same two: `curl --cacert certs/ca.crt --cert certs/server.fullchain.crt --key certs/server.key https://192.168.1.125:18080/`. A curl built against OpenSSL can also read PKCS#12 directly (`--cert certs/server.pfx:saea --cert-type P12`), but the curl shipped by many distributions does not, so do not rely on it across machines. Java converts with `keytool -importkeystore`; Node reads PEM only, so first run `pkcs12 -in certs/server.pfx -nodes -out certs/server.pem` to convert it. The conversion commands are under "What gets written" and "openssl-compatible subcommands".

### Q: The certificate expires in 30 days. What now?

Re-issue leaves with `--ca --reuse-ca`; `server.*` / `client.*` are overwritten, `ca.crt` is untouched, trust stores stay as they are. In self-signed mode just re-run with `--force`.

### Q: Can I use this in production?

No. The tool does no CSR, no domain validation, no renewal, and no CRL / OCSP. Production uses ACME or a real CA.

### Q: Should I still fill in the CN?

You can leave it out; modern clients look at the SAN only and the CN is for display. The default CN is `localhost`. `CN=192.168.1.125` without `--ip` is rejected by both browsers and the library.

### Q: How do I write IPv6?

`--ip "::1"`, or `--ip "::1,fe80::1"` for several. The observed SAN is `DNS: localhost`, `IP: ::1`. In a URL, IPv6 needs brackets: `https://[::1]:18080`.

### Q: How do wildcards work, and do they cross levels?

`--dns "*.dev.local,localhost"` matches one level: `api.dev.local` yes, `a.b.dev.local` no. An explicit `--dns` replaces the default `localhost`, so add it back yourself.

### Q: How do I list several names and IPs, and are they appended?

Comma-separated, all at once: `--dns "api.dev.local,dev.local" --ip "192.168.1.125,10.0.0.8"`. An explicit `--dns` / `--ip` replaces the list entirely; nothing is appended, not even the CN.

### Q: The machine or its IP changed. What do I do?

Re-issue leaves with `--ca --reuse-ca --ip <new address>`. If the old address is still in use, list both. With `--reuse-ca`, `--days` may not run past the old CA's expiry.

### Q: How do I package certificates for other systems?

Follow the delivery table under "Shipping and packaging", keep `ca.key` on the machine that generated it, and checksum archives with SHA-256. Containers mount certs read-only instead of baking them into the image.

### Q: Does the tool run on Linux / macOS, and how do I publish it?

Yes, it targets `net10.0`. With .NET 10 on the target, use `dotnet run` or a framework-dependent publish; without it, publish self-contained for that RID. `linux-x64` self-contained is tested; `osx-*` and `win-arm64` are not.

### Q: I forgot the PFX password. What now?

A PFX password cannot be recovered; rebuild. If `.crt` + `.key` are still around you can use them directly (`.key` has no password). If the CA password is lost, `--reuse-ca` is impossible: rebuild with `--force` and re-import the new `ca.crt` on clients.

### Q: What if `ca.key` leaks?

That is the signing authority leaking: whoever holds it can issue anything that CA is trusted for. Rebuild immediately with `--force`, ship the new `ca.crt` to every client, and remove the old CA from trust stores. There is no CRL / OCSP and no revocation channel, so replacing the root is the only move.

### Q: Does passing check 5 mean the chain is trusted?

No. Check 5 covers hostname matching and a working loopback handshake only; chain errors are let through. The chain evidence is check 3, and only in CA mode. Self-signed mode skips check 3 and cannot prove any external client will accept the certificate.

### Q: Why is the output directory not under the project?

`--out` defaults to `certs`, relative to the working directory of the command. From the repository root you get `<repo>/certs`; from `Src/` you get `Src/certs`. In scripts and CI, pass an absolute path.

## How it is built

The tool is a thin CLI. Certificate generation lives in the library under `SAEA.Sockets.Ssl`, and defaults match the library; PEM / DER / PFX / P7B / PKCS#8 conversion is implemented by the `OpenSsl/` compatibility layer. The tool itself parses arguments, prints output, and runs the self-checks.

```text
Src/SAEA.Sockets.SslTlsTool/
├── Program.cs              entry point, mode dispatch, generation flow, summary and trust hints, interactive loop and tokenizer
├── SslTlsToolOptions.cs    argument parsing and validation (charset, reserved names, ranges, conflicting options)
├── CertificateVerifier.cs  the five self-checks, independent SAN expectation, loopback handshake
├── GlobalUsing.cs          global using directives for the net10 project
├── OpenSsl/                openssl-compatible subcommands: dispatch, help, passphrase handling, format codecs
│   └── Commands/           one file each for req / x509 / pkcs12 / pkcs8 / pkey / crl2pkcs7 / pkcs7
└── SAEA.Sockets.SslTlsTool.csproj
```

`SslTlsToolOptions.TryReadValue` treats a next token that starts with `--` as a missing value, so a path or password cannot itself start with `--`, and there is no `--out=...` form. If you hit that case, rename the directory or pick a different password.

To generate certificates from your own code, use these library types:

- `SaeaCertificateFactory.CreateSelfSigned`
- `SaeaCertificateAuthority.Create` / `FromPfx` / `FromPem`
- `IssueServerCertificate` / `IssueClientCertificate`
- `SaeaCertificateBundle` (owns the certificate and key, and saves them)

A full programmatic example is in [`../SAEA.Sockets/README.en.md`](../SAEA.Sockets/README.en.md) (Chinese: [`../SAEA.Sockets/README.md`](../SAEA.Sockets/README.md)). Three things to remember: `Save*` methods do not create directories, so create them first; the returned objects own the private key and must be disposed; certificate objects must outlive the server or client that uses them.

## Security and cleanup

- These certificates are for development and testing. Do not use them in production, and do not treat this tool as a replacement for ACME / Let's Encrypt: it does no renewal and no domain validation, and a CSR is only produced by the compatible `req` subcommand.
- The repository `.gitignore` already ignores `*.pfx`, `*.key`, and `certs/`. `.crt` is not ignored, because certificates are public material.
- Do not commit `.key` or `.pfx` files, and do not put passwords in scripts, logs, or commit messages.
- Leaking `ca.key` means leaking the signing authority: whoever has it can issue any certificate that CA is trusted for.
- Remove the CA from the trust store when you are done. On Windows delete by thumbprint; on Debian/Ubuntu delete `/usr/local/share/ca-certificates/saea-dev-ca.crt` and run `update-ca-certificates --fresh`; on macOS use `security remove-trusted-cert`; for Java use `keytool -delete`.
- Moving to another machine or rebuilding the CA always invalidates the old trust; import the new `ca.crt`.
- A PFX produced with `--pwd ""` has no password at all, so anyone with the file has the private key. Use it only for throwaway containers or one-off environments.

## License

Apache License 2.0. See [`LICENSE`](../../LICENSE) in the repository root.

Related documentation:

- Repository overview: [`../../README.en.md`](../../README.en.md) ([中文](../../README.md))
- SAEA.Sockets TLS configuration and certificate notes: [`../SAEA.Sockets/README.en.md`](../SAEA.Sockets/README.en.md) ([中文](../SAEA.Sockets/README.md))