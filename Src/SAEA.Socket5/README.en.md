# SAEA.Socket5 - SOCKS5 Proxy Server & Client 🔌

[![NuGet version](https://img.shields.io/nuget/v/SAEA.Socket5.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.Socket5)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**English Version** | **[中文版](README.md)**

> A SOCKS5 proxy implementation based on .NET Standard 2.0, built on the SAEA.Sockets transport layer. It fully supports RFC 1928 (CONNECT / BIND / UDP ASSOCIATE) and RFC 1929 (username/password authentication), suitable for forward proxying, intranet tunneling, UDP forwarding, and similar scenarios.

## Quick Navigation 🧭

| Section | Content |
|------|------|
| [⚡ 30-Second Quick Start](#30-second-quick-start) | Fastest way to get started with Server + Client |
| [🎯 Core Features](#core-features) | Overview of supported capabilities |
| [📐 Protocol Flow](#protocol-flow) | Interaction sequence of the three commands |
| [🔧 Core Classes](#core-classes) | Main classes and options |
| [📝 Usage Examples](#usage-examples) | Server / CONNECT / BIND / UDP |
| [❓ FAQ](#faq) | Quick answers to common questions |

---

## 30-Second Quick Start ⚡

### Step 1: Install the NuGet package

```bash
dotnet add package SAEA.Socket5
```

### Step 2: Start the proxy server

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
Console.WriteLine("SOCKS5 proxy started on port 1080");
```

### Step 3: Access a target through the proxy

```csharp
using SAEA.Socket5.Client;
using SAEA.Socket5.Model;

var client = new Socks5Client(new Socks5ClientOptions
{
    ProxyHost = "127.0.0.1",
    ProxyPort = 1080
});

using var tunnel = client.Connect("www.example.com", 80);
// tunnel is a transparent stream to the target host
```

> Options and enums live in `SAEA.Socket5.Model`; the server is in `SAEA.Socket5.Server`; the client is in `SAEA.Socket5.Client`.

---

## Core Features 🎯

| Feature | Description |
|------|------|
| 🔗 **CONNECT** | Establish a TCP tunnel to the target host (forward proxy) |
| 📡 **BIND** | Proxy listens on a port and waits for a remote peer (two-phase handshake) |
| 📦 **UDP ASSOCIATE** | Send/receive UDP datagrams through the proxy (RFC 1928 UDP request header) |
| 🔒 **Username/password auth** | RFC 1929, supporting dictionary-based or delegate-based validators |
| 🌐 **Multiple address types** | IPv4 (`0x01`), domain (`0x03`), IPv6 (`0x04`) |
| ⚡ **Built on SAEA.Sockets** | Server `IServerSocket` / client `IClientSocket`, byte-level stream handshake |
| 🧩 **Half-packet safe** | All reads use exact-length looped reads, naturally tolerant of TCP fragmentation |

---

## Protocol Flow 📐

### CONNECT

```
Client                    Proxy                    Target
  │  1. Method negotiation (05 NMETHODS…)  │                     │
  │ ─────────────────────────────────────► │                     │
  │  2. Method selection (05 METHOD)       │                     │
  │ ◄───────────────────────────────────── │                     │
  │  3. Request (05 01 00 ATYP ADDR PORT)                        │
  │ ─────────────────────────────────────► │  4. TCP connect     │
  │                                        │ ──────────────────► │
  │  5. Reply (05 00 00 ATYP BND.ADDR BND.PORT)                  │
  │ ◄───────────────────────────────────── │                     │
  │  6. Bidirectional transparent forwarding│ ◄─────────────────► │
```

### BIND (two-phase)

The proxy first returns the listening address/port it allocated (phase one), then returns the peer address once a remote host connects (phase two); the connection then becomes a tunnel.

### UDP ASSOCIATE

First performs the UDP association over the TCP control connection and obtains the proxy relay endpoint, then:

- Client → Proxy: `RSV(2) FRAG(1) ATYP ADDR PORT DATA`
- Proxy → Client: the same encapsulation format

> The association's lifetime equals that of the TCP control connection; closing the control connection ends the association.

---

## Core Classes 🔧

| Class / Interface | Namespace | Description |
|-----------|----------|------|
| `Socks5Server` | `SAEA.Socket5.Server` | SOCKS5 proxy server (`Start` / `Stop` / `Dispose`) |
| `Socks5Client` | `SAEA.Socket5.Client` | SOCKS5 client (`Connect` / `Bind` / `UdpAssociate` and async variants) |
| `Socks5BindResult` | `SAEA.Socket5.Client` | BIND result (`FirstReply`, `WaitForBindComplete`, `ControlStream`) |
| `Socks5UdpClient` | `SAEA.Socket5.Client` | UDP association client (`SendTo` / `TryReceive`) |
| `Socks5ServerOptions` | `SAEA.Socket5.Model` | Server options |
| `Socks5ClientOptions` | `SAEA.Socket5.Model` | Client options |
| `ISocks5UserValidator` | `SAEA.Socket5.Model` | Username/password validation interface |
| `DefaultUserValidator` | `SAEA.Socket5.Model` | Default validator (dictionary / custom delegate) |
| `Socks5Codec` | `SAEA.Socket5.Protocol` | RFC 1928/1929 byte-level codec |

### Server options `Socks5ServerOptions`

| Property | Default | Description |
|------|--------|------|
| `IP` | `""` | Listen IP; empty means listen on any address |
| `Port` | `1080` | Listen port |
| `AllowedMethods` | `{ NoAuth }` | Allowed auth methods (`NoAuth` / `UserPass`) |
| `UserValidator` | `new DefaultUserValidator()` | Username/password validator (empty by default — rejects all credentials) |
| `BufferSize` | `65536` | Read/write buffer size (bytes) |
| `ActionTimeout` | `180000` | Per-connection action timeout (milliseconds) |

### Client options `Socks5ClientOptions`

| Property | Default | Description |
|------|--------|------|
| `ProxyHost` | `null` | Proxy server address (IP or domain) |
| `ProxyPort` | `1080` | Proxy server port |
| `BindIP` | `""` | Local bind IP; empty means system-assigned |
| `UserName` | `null` | Username; empty means no authentication |
| `Password` | `null` | Password |
| `Timeout` | `30000` | Connect/action timeout (milliseconds) |

---

## Usage Examples 📝

### Server: enable username/password authentication

When enabling `UserPass`, you **must provide a validator that contains users**, otherwise all credentials are rejected:

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

A custom delegate is also supported:

```csharp
UserValidator = new DefaultUserValidator((user, pwd) => user == "user" && pwd == "pass")
```

### Client: CONNECT

```csharp
using SAEA.Socket5.Client;
using SAEA.Socket5.Model;

var client = new Socks5Client(new Socks5ClientOptions
{
    ProxyHost = "127.0.0.1",
    ProxyPort = 1080,
    UserName = "user",   // omit if the proxy requires no authentication
    Password = "pass"
});

using var tunnel = await client.ConnectAsync("www.example.com", 80);
```

> A `Socks5Client` instance corresponds to one TCP connection to the proxy and **supports only one command** (SOCKS5 limitation: one connection handles one request). Create a separate instance for each command when accessing multiple targets.

### Client: BIND

```csharp
using SAEA.Socket5.Client;
using SAEA.Socket5.Model;

var client = new Socks5Client(new Socks5ClientOptions { ProxyHost = "127.0.0.1", ProxyPort = 1080 });

var bind = client.Bind("0.0.0.0", 0);

// Phase one: the proxy has allocated a listening address/port (bind.FirstReply.Host / bind.FirstReply.Port);
// inform the remote peer so it can connect in.
// Phase two: returned once the remote peer connects; ControlStream is the tunnel to the peer.
var peer = bind.WaitForBindComplete();

using var tunnel = bind.ControlStream;
```

### Client: UDP ASSOCIATE

```csharp
using SAEA.Socket5.Client;
using SAEA.Socket5.Model;

var client = new Socks5Client(new Socks5ClientOptions { ProxyHost = "127.0.0.1", ProxyPort = 1080 });

using var udp = client.UdpAssociate();

udp.SendTo("8.8.8.8", 53, dnsQuery);

if (udp.TryReceive(out var host, out var port, out var data, timeoutMs: 5000))
{
    // data is the payload with the SOCKS5 UDP header stripped; host/port is the source
}
```

---

## FAQ ❓

### Q1: Which SOCKS5 commands are supported?

All three RFC 1928 commands — CONNECT, BIND, and UDP ASSOCIATE — plus IPv4 / domain / IPv6 address types.

### Q2: All connections are rejected after enabling UserPass?

The default `DefaultUserValidator` contains no users and rejects all credentials. Provide a validator instance containing users, or a custom validation delegate, via `UserValidator`.

### Q3: Can one `Socks5Client` access multiple targets at once?

No. SOCKS5 specifies one request per TCP connection, so a `Socks5Client` supports only one command; further calls throw `InvalidOperationException`.

### Q4: The BIND / UDP relay address reported by the proxy is unreachable?

The proxy reports the **local endpoint address of the control connection** (normalized to IPv4), so the client can reach the relay port along the same reachable address. Make sure that address is reachable between client and proxy.

### Q5: When does a UDP association end?

The association's lifetime equals the TCP control connection carrying it — closing the `Socks5Client` (or disposing the `Socks5UdpClient`, which also disposes its owning client) ends the association.

---

## Tests 🧪

`SAEA.Socket5Test` is a self-validating console integration test project (exits with a non-zero code on failure). It covers protocol behavior, data integrity and performance:

```bash
dotnet run --project Src/SAEA.Socket5Test
```

| Case | What it verifies |
|------|------------------|
| No-auth CONNECT echo via proxy | Method negotiation (NoAuth) + CONNECT + echo |
| Username/password auth succeeds | RFC 1929 success path |
| Username/password auth rejected | Wrong credentials must be rejected |
| BIND two-phase handshake | Both BIND replies + tunnel exchange |
| UDP ASSOCIATE echo via proxy | UDP datagram encode/decode |
| Disconnect on missing session does not throw | Robustness regression when a session was already reclaimed |
| Data integrity: multi-size across buffer boundaries | 1B / 2B / 1KB / 64KB±1 / 128KB / 200KB, byte-exact per size |
| Data transfer: 8MB echo | Bulk throughput + byte-exact integrity |
| Performance: 1000 round trips on one connection | Average RTT and round trips per second |
| Performance: 64 concurrent connections | Aggregate throughput across sessions |
| Performance: 1000 UDP datagrams | UDP response rate and throughput |

Reference magnitudes on localhost (loopback): ~450 MB/s for a single-connection 8MB echo, ~370 MB/s at 64 concurrent connections, ~0.3 ms per round trip.

> Note: the concurrency case has a 30s time budget. If it becomes noticeably slower, it usually means the relay tasks fell back onto thread pool threads — relay and session handling must use dedicated threads (`TaskCreationOptions.LongRunning`) to sustain many concurrent sessions.

---

## Dependencies

| Project | Description |
|------|------|
| SAEA.Sockets | Transport layer (IOCP communication framework) |
| SAEA.Common | Common utilities |

---

## More Resources

- [GitHub Repository](https://github.com/yswenli/SAEA)
- [NuGet Package](https://www.nuget.org/packages/SAEA.Socket5)
- [RFC 1928 - SOCKS Protocol Version 5](https://www.rfc-editor.org/rfc/rfc1928)
- [RFC 1929 - Username/Password Authentication for SOCKS V5](https://www.rfc-editor.org/rfc/rfc1929)

---

## License

Apache License 2.0
