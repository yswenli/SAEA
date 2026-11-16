# SAEA.P2P - P2P Communication with ICE-Style Hole Punching and STUN/TURN Relay

<p align="center">
  <img src="https://img.shields.io/badge/Version-26.11.5.1-blue?style=for-the-badge" alt="Version">
  <img src="https://img.shields.io/badge/NuGet-SAEA.P2P-green?style=for-the-badge" alt="NuGet">
  <img src="https://img.shields.io/badge/.NET-Standard%202.0-purple?style=for-the-badge" alt=".NET">
  <img src="https://img.shields.io/badge/License-Apache%202-orange?style=for-the-badge" alt="License">
</p>

<p align="center">
  <b>🚀 Punch first, relay when punching fails: peers talk without a forwarding server in front</b>
</p>

<p align="center">
  <b>English</b> | <a href="README.md">中文</a>
</p>

---

## 🎯 What It Solves

Two peers sit behind NATs, neither knows the other's public address, and neither knows what that NAT will let through. SAEA.P2P splits the problem into three steps:

1. Use ICE candidate gathering and connectivity checks to get a UDP / TCP direct path working;
2. When direct fails, fall back along a ladder to the built-in STUN/TURN relay (UDP → TCP → TLS);
3. On a LAN without a signaling server, exchange the same ICE parameters over multicast discovery.

Both STUN and TURN are implemented in this repository, so relay works without deploying coturn; when you need to align with an external implementation, see the "Alignment with coturn" section under "Server".

### Capabilities

| Capability | Description | Main implementation |
|------------|-------------|---------------------|
| 🧭 **Candidate gathering** | host / srflx / prflx / relay candidates; mapped addresses come from STUN Binding, relay addresses from TURN Allocate | `Ice/CandidateGatherer`, `Stun/StunClient`, `Turn/TurnClient` |
| 🕳️ **UDP hole punching** | Connectivity checks per candidate pair; the selected pair carries data frames | `Ice/CheckList`, `Ice/PairSelector` |
| 🔌 **TCP hole punching** | active ↔ passive one-way active connect; simultaneous open must be enabled explicitly and is not supported on Windows | `Ice/TcpCandidateGatherer` |
| 🔄 **TURN fallback** | When direct fails, try TURN over UDP / TCP / TLS in ladder order | `Ice/RouteLadder` |
| 🌐 **IPv6** | Gathers IPv6 candidates; `StunServerOptions.EnableDualMode` enables dual-stack listening | `Ice/IceOptions`, `StunServer/StunServer` |
| 📡 **Signaling-free LAN discovery** | Discovery datagrams exchange candidates and ufrag/pwd without any signaling server | `Discovery/LocalDiscovery` |
| 🔑 **Dual TURN authentication** | Temporary credentials (HMAC-SHA1 derived) and standard long-term credential entry points | `Turn/TurnCredentialService`, `Turn/TurnAuthContext` |
| 📦 **No external dependency** | STUN / TURN servers are built in, no third-party component to deploy | `StunServer/`, `TurnServer/` |

---

## 🚀 Quick Start

```csharp
using System;
using System.Text;
using SAEA.P2P;
using SAEA.P2P.Channel;

// 1. Create the client and register with the signaling server
var client = P2PQuick.Client("10.0.0.1", 39654, "user-A");

// Fires when a peer session is established and a channel is selected; P2PConnection.Channel is the route result
client.OnPeerConnected += conn =>
    Console.WriteLine($"🔗 Connected to {conn.PeerId} over {conn.Channel}");

// Business frame received: data is a ReadOnlySpan<byte> valid only during the callback, copy it to keep it
client.OnDataReceived += (peerId, data) =>
    Console.WriteLine($"📨 {peerId}: {Encoding.UTF8.GetString(data.ToArray())}");

// Error callback: an error code plus a message
client.OnError += (code, message) => Console.WriteLine($"❌ {code}: {message}");

await client.RegisterAsync();

// 2. Connect to a peer: ICE parameters are exchanged over signaling, then the ICE session selects a route
var connection = await client.ConnectToPeerAsync("user-B");
Console.WriteLine($"✅ Channel: {connection.Channel}, remote endpoint: {connection.RemoteEndPoint}");

// 3. Send a business frame
client.Send("user-B", Encoding.UTF8.GetBytes("Hello! 👋"));
```

- Use `P2PQuick.Client(host, port, nodeId, password)` when the node needs a password; registration then uses challenge-response authentication.
- Use `P2PQuick.ClientFull(...)` to tune ICE budgets or supply TURN credentials directly, or chain `P2PClientBuilder` and call `BuildClient()`.
- `Send` takes a `ReadOnlySpan<byte>` payload that is read only for the duration of the call; the `data` passed to `OnDataReceived` is likewise valid only during the callback.

---

## 🔀 Connection Flow

1. **Register**: `RegisterAsync()` connects to the signaling server (TCP by default, TLS with `EnableTls()`) and completes challenge-response for the peer id.
2. **Exchange ICE parameters**: the caller sends `ConnectRequest` and a `SessionOffer` carrying ufrag / pwd / role / tiebreaker; the callee answers with `SessionAnswer`; later candidates trickle in incrementally. These signaling message types fall in the `0x90` range.
3. **Connectivity checks**: both sides send STUN Binding per candidate pair, with `MESSAGE-INTEGRITY` keyed from ufrag / pwd.
4. **Nomination and channel takeover**: the selected candidate pair is reported as `ChannelType`, and the data plane switches from STUN messages to business frames with the `0xC0` prefix.
5. **Fallback**: if a stage fails, the ladder advances to the next `IceStage`, up to TURN over TLS.

```
  A                      signaling server                     B
  │  ① ConnectRequest ────────>│                            │
  │                           │──── ConnectRequest ───────>│
  │  ② SessionOffer(ufrag/pwd) >│                           │
  │                           │──── SessionOffer ─────────>│
  │                           │<─── SessionAnswer(ufrag/pwd)│
  │<── SessionAnswer ─────────│                            │
  │  ③ trickle candidates exchanged both ways (0x90 range)  │
  │  ④ UDP direct connectivity checks (STUN Binding + MESSAGE-INTEGRITY) │
  │  ⑤ Nominate the selected pair → ⑥ data plane takes over │
  │<════════ business frames (1-byte prefix 0xC0) ═════════>│
  │  ⑦ Direct stages all fail: TURN over UDP → TCP → TLS    │
```

---

## 🛣️ Channels and the Route Ladder

### ChannelType Values

| Value | Number | Meaning | When selected |
|-------|--------|---------|---------------|
| `UdpDirect` | 0 | UDP direct | A UDP candidate pair passed connectivity checks |
| `TcpDirect` | 1 | TCP direct (active ↔ passive) | One side actively connected while the other was listening passively |
| `RelayUdp` | 2 | TURN over UDP relay | All direct stages failed and a TURN UDP allocation is available |
| `RelayTcp` | 3 | TURN over TCP relay | UDP relay unavailable; the control connection runs over TCP |
| `RelayTls` | 4 | TURN over TLS relay | Both earlier relay stages unavailable, or the control connection must be encrypted |
| `Local` | 5 | LAN discovery direct | No-signaling mode; parameters came from discovery datagrams, data plane is still UDP direct |

`Local` differs from `UdpDirect` only in where the ICE parameters came from and in the reported channel marker; the data plane is UDP direct in both cases.

### Ladder Order and Budgets

The `IceStage` enum order is the fallback priority: `UdpDirect` → `TcpDirect` → `TurnUdp` → `TurnTcp` → `TurnTls`. The ladder stops at the first stage that connects; if all five fail it throws `NoCandidatePair`, and when the total session budget runs out it throws `IceTimeout`.

| Budget | Field | Default |
|--------|-------|---------|
| Candidate gathering | `IceSessionOptions.GatheringBudget` | 3s |
| UDP direct stage | `IceSessionOptions.UdpDirectBudget` | 10s |
| TCP direct stage | `IceSessionOptions.TcpDirectBudget` | 10s |
| Single TURN stage | `IceSessionOptions.TurnStageBudget` | 15s |
| Total session budget | `IceSessionOptions.SessionBudget` | 90s |

When `TurnClientOptions` is not configured the three TURN stages are skipped and only the two direct stages remain.

### When Relay Is Used

- Both peers are behind symmetric NAT, or behind port-restricted NAT where punching fails;
- The network only allows TCP, or only a fixed outbound port such as 443;
- Deployment requires all traffic to leave through a fixed egress for auditing and rate limiting.

---

## 📡 LAN Direct Connection (No Signaling)

`P2PQuick.LocalNet` starts discovery with the defaults (UDP 39655, multicast 224.0.0.250) and returns a client you can call `ConnectToPeerAsync` on directly.

```csharp
using SAEA.P2P;
using SAEA.P2P.Channel;

// Custom discovery port and multicast group: P2PQuick.LocalNet("file-server", 39655, "224.0.0.250")
var client = P2PQuick.LocalNet("file-server");

// Discovery only reports nodes; connecting is up to the application, and both sides must call ConnectToPeerAsync
client.OnPeerDiscovered += node =>
{
    Console.WriteLine($"🔍 Discovered {node.NodeId} ({node.LocalAddress}:{node.LocalPort})");
    _ = client.ConnectToPeerAsync(node.NodeId);
};

client.OnPeerConnected += conn =>
    Console.WriteLine($"✅ Connected to {conn.PeerId} over {conn.Channel}");   // ChannelType.Local
```

The flow is: build the client with `new P2PClient(nodeId, LocalDiscoveryOptions, IceSessionOptions)` to supply the discovery settings → `StartLocalAsync()` starts discovery → `OnPeerDiscovered` reports nodes → the application calls `ConnectToPeerAsync(peerId)` → ICE selects a route using the parameters and candidates from the discovery datagrams → the channel marker is `ChannelType.Local`. Discovery datagrams repeat periodically and expired nodes drop off the list, but an established data plane is not torn down by discovery timeouts.

### 🔐 Security Boundary of the No-Signaling Mode

- **No identity authentication**: any host on the same broadcast domain can send discovery datagrams and claim any `NodeId`.
- **HMAC guarantees integrity only, not confidentiality**: setting `LocalDiscoveryOptions.SharedKey` adds an HMAC-SHA256 tag but does not encrypt the payload; candidate addresses and ufrag / pwd stay in plaintext.
- With an empty `SharedKey` no tag is attached, so any host can forge datagrams.
- **Use it on this link only**: switch to signaling mode when you need authentication or when traffic crosses the public internet.

---

## 🖥️ Server

There are two server roles, both implemented in this repository:

- **Signaling server**: `P2PServer`, assembled with `P2PServerBuilder`, handles node registration, online state and ICE parameter forwarding, and listens on `39654` by default; its settings live in `P2PServerOptions` (`Port`, `MaxNodes`, `Tls`, `SharedSecret`, `Realm`, `CredentialTtlSeconds`, `TurnUrls`).
- **STUN / TURN listeners**: the `StunServer` and `TurnServer` classes; `Src/SAEA.P2PTest` is a ready-made host that takes the listen address, ports, realm, shared secret and relay port range from a JSON configuration.

Start the host with:

```bash
dotnet run --project Src/SAEA.P2PTest -c Release -- --server --config <config-path>
```

`--help` lists every switch (`--listen`, `--stun-port`, `--turn-port`, `--tls-port`, `--realm`, `--shared-secret`, `--credential-ttl`, `--nonce-lifetime`, `--relay-min`, `--relay-max`, `--max-allocations`, `--max-allocations-per-user`, `--max-bytes`, `--max-lifetime`, `--tls-cert`, `--tls-cert-password`, `--change-address`, `--ipv6`, `--dual-mode`); without `--config` the built-in defaults apply plus any command-line overrides. Shared secrets and certificate passwords are never printed.

### Configuration Keys and Defaults

The table below lists the host's configuration keys (STUN / TURN listeners):

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `ListenAddress` | string | `0.0.0.0` | Listen address; switches to `IPv6Any` when `EnableIpv6` is true and the value is still the default |
| `StunPort` | int | `3478` | Dedicated STUN port; `0` disables the separate listener |
| `TurnPort` | int | `3478` | TURN listens on the same port for UDP and TCP; different protocols do not conflict |
| `TurnTlsPort` | int | `0` | TURN over TLS port; `0` disables it, and enabling it requires a certificate |
| `Realm` | string | empty (required) | Authentication realm; must match `P2PServerOptions.Realm` of the signaling server that issues temporary credentials |
| `SharedSecret` | string | empty (required) | Shared secret for validating TURN temporary credentials; the signaling auth key is configured separately in `P2PServerOptions` |
| `EnableIpv6` | bool | `false` | Whether to listen on IPv6 instead |
| `EnableDualMode` | bool | `false` | Whether to enable dual-stack listening: the dedicated STUN listener and the TURN UDP listener use one socket for IPv4 and IPv6 when listening on the IPv6 any address, and the switch has no effect on other listen addresses |
| `TlsCertificatePath` | string | `null` | PFX path; required and the file must exist when TLS is enabled |
| `TlsCertificatePassword` | string | `null` | PFX password, leave empty when there is none |
| `CredentialTtlSeconds` | int | `600` | Lifetime of issued TURN temporary credentials |
| `NonceLifetimeSeconds` | int | `600` | TURN nonce lifetime |
| `RelayPortRange` | object | `Min=49152`, `Max=65535` | Relay port range |
| `MaxAllocations` | int | `1024` | Total concurrent allocation limit |
| `MaxAllocationsPerUser` | int | `8` | Per-user concurrent allocation limit |
| `MaxLifetimeSeconds` | uint | `3600` | Allocation lifetime cap; longer requests are clamped to it |

Two more keys live in the same configuration object: `MaxBytes` (default `0`, meaning no cap on total relayed bytes per allocation) and `ChangeRequestAddressPair` (default `null`, the RFC 5780 second address).

### Authentication Model

**Signaling registration auth**: after the client sets a password with `SetNodeIdPassword` / `P2PQuick.Client(..., password)`, registration uses challenge-response and the reply value is `Base64(HMAC-SHA256(SharedSecret, nonce + "|" + peerId))`; `SharedSecret` comes from the signaling server's `P2PServerOptions`, and an empty value means authentication is not required.

**TURN layer 1: temporary credentials (coturn `use-auth-secret` semantics)**: when `P2PServerOptions.SharedSecret` is not empty and `TurnUrls` is configured, the signaling server sends a separate `TurnCredential` signaling message after authentication succeeds, carrying realm, username, password, ttl and `Urls`.

- username = "expiry Unix seconds" + `:` + "user identifier", and the identifier must not contain the separator;
- password = `Base64(HMAC-SHA1(shared secret, username))` (SHA1, not SHA256);
- when `TurnUrls` is unconfigured or no shared secret is set, no credentials are issued and the client can only use direct paths.

**TURN layer 2: standard long-term credential**: supply `TurnClientOptions.Username` / `TurnClientOptions.Password` directly for interop with coturn and other implementations; this is mutually exclusive with a `TurnCredential` pushed over signaling.

### STUN and TURN Sharing the Same Port for Binding

When `StunPort` equals `TurnPort` (both 3478 by default), the host does not bind a separate STUN port and Binding requests are answered by the TURN UDP listener — the two share a single `StunServer` instance: each datagram is offered to the TURN ChannelData entry point first, then parsed as STUN, and requests with `Method == Binding` are answered by the STUN side while every other method is dispatched to TURN. A distinct `StunPort` value adds a separate STUN listener.

### Alignment with coturn

| Alignment item | Specification | Implementation notes |
|----------------|---------------|----------------------|
| STUN methods and attributes | RFC 8489 | Binding request / success response, XOR-MAPPED-ADDRESS, SOFTWARE, FINGERPRINT |
| TURN methods and attributes | RFC 8656 | Allocate / Refresh / CreatePermission / ChannelBind / Send, XOR-RELAYED-ADDRESS, XOR-PEER-ADDRESS, LIFETIME |
| TCP relay and data connections | RFC 6062 | Allocate with `REQUESTED-TRANSPORT=6`, Connect returns `CONNECTION-ID`, a separate connection carries ConnectionBind |
| Candidates, pairs and nomination | RFC 8445 | Candidate priority, pair state machine, controlling / controlled roles settled by tiebreaker |
| First-byte multiplexing ranges | RFC 7983 | `0x00`–`0x3F` is STUN, `0x40`–`0x4F` is ChannelData, `0xC0` is a business frame |
| realm / nonce and long-term credentials | RFC 8489 long-term credential | 401 challenge carries REALM / NONCE, the nonce is bound to the source address and expires after `NonceLifetimeSeconds` |
| Integrity check vectors | RFC 5769 | MESSAGE-INTEGRITY / FINGERPRINT match the official samples byte for byte |
| Framing | RFC 4571 and STUN self-delimiting | TCP direct business frames carry a 2-byte length prefix; TURN over TCP/TLS control connections carry no length prefix |

---

## 📦 Transport and Framing

| Scenario | Framing | Notes |
|----------|---------|-------|
| UDP direct / `Local` | 1-byte prefix `0xC0` | Datagram limit 65506 bytes, so the business payload limit is 65505 |
| TCP direct | RFC 4571 | Each frame carries a 2-byte big-endian length prefix; the frame body is still `0xC0` + payload |
| TURN over UDP | TURN ChannelData | First byte `0x40`–`0x4F`, delimited by the Length field |
| TURN over TCP / TLS control connection | No length prefix | Delimited by STUN's own 20-byte header plus Length; channel data is delimited by the 4-byte ChannelData header |

UDP multiplexes by first byte: `0x00`–`0x3F` goes to STUN, `0xC0` is treated as a business frame, everything else is dropped.

---

## 🌐 IPv6

- **Candidate gathering**: `IceOptions.EnableIpv6` defaults to true and gathers both IPv4 and IPv6 addresses; turning it off stops IPv6 gathering.
- **Dual-stack listening**: `StunServerOptions.EnableDualMode` applies when the primary endpoint is `::` and no alternate endpoint is configured, using one socket for both IPv4 and IPv6; the self-test host passes `EnableDualMode` straight to `StunServerOptions` of the dedicated STUN listener and the TURN UDP listener, so the switch only takes effect when the primary endpoint is the IPv6 any address. TURN over TCP/TLS listeners always run dual-stack on the IPv6 any address, regardless of this switch.
- **v4-mapped normalization**: candidate construction, candidate pair matching and TCP passive matching all map `::ffff:a.b.c.d` back to the corresponding IPv4 address so one address is not treated as two families.
- **No cross-family pairs**: `IceOptions.AllowCrossFamilyPairs` defaults to `false`, so an IPv4 candidate never pairs with an IPv6 candidate.
- **Relay**: IPv6 relay addresses can be allocated; an `Allocate` whose `REQUESTED-ADDRESS-FAMILY` differs from the relay address family returns 440 (`AddressFamilyNotSupported`), and a `CreatePermission` / `Connect` whose peer address family differs from the relay address family returns 443 (`PeerAddressFamilyMismatch`).
- **NAT66**: there are no automated cases and configuring the network needs administrator rights; public IPv6 scenarios are treated as direct connections.

---

## 🔐 Security Boundary

- **No application-layer encryption on the data plane**: business frames travel in plaintext; add your own protection in the payload if you need confidentiality.
- **TLS is used for control connections only**: the signaling connection (`EnableTls()`) and the TURN over TLS control connection (`ChannelType.RelayTls`); UDP / TCP direct paths and TURN over UDP/TCP relayed data are not encrypted.
- **Credentials and shared secrets never reach the logs**: logs record only realm, ttl, endpoints and channel type; usernames, passwords and shared secrets are never written out.
- **Signaling registration auth is a per-peer-id challenge-response**, not an identity system; an empty `SharedSecret` means no authentication and must not be used as access control.
- The boundaries of the signaling-free LAN mode are covered above in "LAN Direct Connection (No Signaling)".

---

## ⚠️ Known Limitations

- On Windows, TCP hole punching only guarantees the active ↔ passive combination; cases where both sides are passive are marked unsupported.
- TCP end-to-end cases do not traverse NAT (there is no transport injection point) and only cover the direct path.
- NAT66 has no automated cases and needs administrator rights to configure the network.
- No SDP / WebRTC support, so there is no browser interoperability.
- Aggressive nomination is not implemented; nomination follows the regular flow.
- Interop with public STUN servers and coturn **was not run this round**; only the checklist was delivered: [STUN/TURN interoperability checklist](../../docs/superpowers/specs/2026-11-11-p2p-interop-checklist.md).

---

## 🧪 Tests and Acceptance

```bash
# Full run: 243 passed / 2 skipped / 0 failed, about 4 min 17 s
dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release

# Unit group: 190 cases (codecs, protocol headers, state machines, local wiring)
dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "TestCategory=Unit"

# Transport matrix: 29 cases (1 skipped, longest running)
dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "TestCategory=Matrix"

# IPv6 + LAN discovery + NAT simulation: 26 cases (1 skipped)
dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "TestCategory=Ipv6|TestCategory=LocalNet|TestCategory=Nat"
```

Groups are marked with a class-level `TestCategory` on each test class, and the constants live in `Src/SAEA.P2P.Tests/P2PTestSuite.cs`. Run a single case with `--filter "FullyQualifiedName~<test name>"`.

---

## 📦 Installation

```bash
# NuGet
dotnet add package SAEA.P2P

# Or Package Manager
Install-Package SAEA.P2P
```

---

## 🔗 Related Resources

| Resource | Link |
|----------|------|
| 📂 **GitHub** | [yswenli/SAEA](https://github.com/yswenli/SAEA) |
| 📦 **NuGet** | [SAEA.P2P](https://www.nuget.org/packages/SAEA.P2P) |
| 📖 **Transport docs** | [SAEA.Sockets](../SAEA.Sockets/README.md) |
| 🧪 **Self-test host** | [SAEA.P2PTest](../SAEA.P2PTest) |
| 📝 **Blog** | [Author's blog](https://www.cnblogs.com/yswenli/) |

---

## 📄 License

Apache License 2.0 - free to use, modify and distribute!

---

<p align="center">
  <b>💡 Questions? Issues and PRs are welcome!</b>
</p>

<p align="center">
  Made with ❤️ by <a href="https://github.com/yswenli">yswenli</a>
</p>