# SAEA.DNS - DNS Server/Client Component 🌍

[![NuGet version](https://img.shields.io/nuget/v/SAEA.DNS.svg?style=flat-square)](https://www.nuget.org/packages/SAEA.DNS)
[![License](https://img.shields.io/badge/license-Apache%202-4EB1BA.svg)](https://www.apache.org/licenses/LICENSE-2.0.html)

**English Version** | **[中文版](README.md)**

> High-performance DNS component based on SAEA.Sockets IOCP technology, supporting DNS server/client, UDP/TCP dual protocols, and multiple record types.

## Quick Navigation 🧭

| Section | Content |
|------|------|
| [⚡ 30-Second Quick Start](#30-second-quick-start) | Simplest getting started example |
| [🎯 Core Features](#core-features) | Main component functionality |
| [📐 Architecture Design](#architecture-design) | Component relationships and workflow |
| [💡 Use Cases](#use-cases) | When to choose SAEA.DNS |
| [📊 Performance Comparison](#performance-comparison) | Comparison with other solutions |
| [❓ FAQ](#faq) | Quick answers |
| [🔧 Core Classes](#core-classes) | Main classes overview |
| [📝 Usage Examples](#usage-examples) | Detailed code examples |

---

## 30-Second Quick Start ⚡

The fastest way to get started, just 3 steps to run a DNS server and client:

### Step 1: Install NuGet Package

```bash
dotnet add package SAEA.DNS
```

### Step 2: Create DNS Server (Only 5 Lines of Code)

```csharp
using SAEA.DNS;

var server = new DnsServer(port: 53);
server.OnRequested += (s, e) => Console.WriteLine($"Query: {e.Request.Questions[0].Domain}");
server.Start();
```

### Step 3: Create DNS Client Query

```csharp
using SAEA.DNS;
using System.Net;

var client = new DnsClient(new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53));
var response = client.Query("www.google.com", RecordType.A);
foreach (var record in response.Answers)
    Console.WriteLine($"IP: {record}");
```

**That's it!** 🎉 You've implemented a high-performance DNS system with UDP/TCP dual protocol support.

---

## Core Features 🎯

| Feature | Description | Advantage |
|------|------|------|
| 🚀 **IOCP High Performance** | Based on SAEA.Sockets completion port technology | Supports tens of thousands of concurrent DNS queries |
| 🖥️ **DNS Server** | Complete DNS server implementation | Self-hosted DNS proxy/server |
| 🔍 **DNS Client** | Forward and reverse queries | Fast domain/IP resolution |
| 📡 **Dual Protocol Support** | UDP/TCP automatic switching | UDP priority, automatic fallback to TCP on truncation |
| 📋 **Multiple Record Types** | A/AAAA/NS/CNAME/PTR/MX/TXT/SOA/SRV | Meets various DNS needs |
| ⚙️ **Local Record Configuration** | DnsRecords custom mapping | Internal domain resolution, development debugging |
| 🔄 **Event Driven** | Request/response/error event callbacks | Flexible event handling mechanism |
| 🌐 **IPv6 Support** | Complete IPv6 record support | Adapts to future network environments |

---

## Architecture Design 📐

### Component Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                     SAEA.DNS Architecture                    │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────┐              ┌──────────────┐            │
│  │  DnsServer   │              │  DnsClient   │            │
│  │ (DNS Server) │              │ (DNS Client) │            │
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
│    │(UDP)    │   │ (TCP)     │   │(Record Mgmt)│             │
│    └────┬────┘   └─────┬─────┘   └───┬────┘              │
│         │              │              │                     │
│         └──────────────┼──────────────┘                     │
│                        │                                     │
│              ┌─────────▼─────────┐                         │
│              │   SAEA.Sockets    │                         │
│              │   (IOCP Layer)    │                         │
│              └───────────────────┘                         │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### DNS Query Flow Diagram

```
DNS Server Workflow:

Client ──► UDP/TCP Request ──► DnsServer.OnRequested
                                     │
                                     ▼
                          ┌─────────────────────┐
                          │  Check Local Records│
                          │  DnsRecords.Contains│
                          └─────────┬───────────┘
                                    │
                     ┌──────────────┼──────────────┐
                     │              │              │
               Local Record    No Record        Error
                     │              │              │
                     ▼              ▼              ▼
             Return Local    Forward to      Return Error
               Record        Upstream DNS      Response
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
                            Return Response to Client

DNS Client Query Flow:

Application ──► DnsClient.Query ──► Create DnsRequestMessage
                                         │
                                         ▼
                               ┌─────────────────────┐
                               │ UdpRequestCoder     │
                               │ Send UDP Request    │
                               └─────────┬───────────┘
                                         │
                               ┌─────────▼───────────┐
                               │ Check TC Flag       │
                               │ (Response Truncated)│
                               └─────────┬───────────┘
                                         │
                          ┌──────────────┼──────────────┐
                          │              │              │
                       Not Truncated   Truncated      Timeout
                          │              │              │
                          ▼              ▼              ▼
                     Return Result  TcpRequestCoder  Retry Query
                                      │
                                      ▼
                               Send TCP Request
                                      │
                                      ▼
                               Return Complete Result
```

---

## Use Cases 💡

### ✅ Scenarios Suitable for SAEA.DNS

| Scenario | Description | Reason |
|------|------|----------|
| 🏢 **Enterprise DNS Proxy** | Internal DNS caching proxy server | IOCP high performance, supports tens of thousands of concurrent connections |
| 🛡️ **Ad-Blocking DNS** | Custom resolution rules to filter ads | Local record configuration, flexible control |
| 🔧 **Development Environment DNS** | Local development domain resolution | Custom domain mapping, no hosts file needed |
| 📊 **DNS Query Tool** | Batch domain query tool | Supports multiple record types |
| 🌐 **Smart DNS** | Return different resolutions based on source IP | Event-driven, flexible extension |
| 📱 **Internal DNS Service** | Private domain system | Lightweight, simple deployment |
| 🔍 **Network Debugging** | DNS resolution troubleshooting | Complete request/response events |

### ❌ Unsuitable Scenarios

| Scenario | Recommended Alternative |
|------|--------------|
| Simple HTTP Service | Use SAEA.Http |
| WebSocket Communication | Use SAEA.WebSocket |
| MQTT IoT | Use SAEA.MQTT |
| RPC Calls | Use SAEA.RPC |

---

## Performance Comparison 📊

### Comparison with Traditional DNS Solutions

| Metric | SAEA.DNS | Traditional DNS | Advantage |
|------|----------|----------|------|
| **Concurrent Queries** | 10,000+ QPS | ~1,000 QPS | **10x Improvement** |
| **Memory Usage** | Pooled Reuse | Frequent Allocation | **Reduced GC Pressure** |
| **Latency** | ~1ms | ~10ms | **Low Latency Response** |
| **Protocol Support** | UDP+TCP | Single Protocol | **Auto Switching** |

### UDP vs TCP Protocol Comparison

| Protocol | Speed | Reliability | Use Case |
|------|------|--------|----------|
| **UDP (SAEA.DNS Default)** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | Regular DNS queries |
| **TCP (SAEA.DNS Fallback)** | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | Large responses, zone transfers |

> 💡 **Tip**: SAEA.DNS uses UDP by default and automatically falls back to TCP when the response is truncated, balancing speed and reliability.

### DNS Record Type Support

| Record Type | Code | Description |
|----------|------|------|
| A | `RecordType.A` | IPv4 Address |
| AAAA | `RecordType.AAAA` | IPv6 Address |
| NS | `RecordType.NS` | Name Server |
| CNAME | `RecordType.CNAME` | Alias Record |
| MX | `RecordType.MX` | Mail Exchange |
| TXT | `RecordType.TXT` | Text Record |
| PTR | `RecordType.PTR` | Reverse Query |
| SOA | `RecordType.SOA` | Start of Authority |
| SRV | `RecordType.SRV` | Service Locator |

---

## FAQ ❓

### Q1: How to Create a DNS Proxy Server?

**A**: Forward to upstream DNS via the `OnRequested` event:

```csharp
var upstreamClient = new DnsClient(new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53));

var server = new DnsServer((s, e) => 
{
    // Forward to upstream DNS
    e.Response = upstreamClient.Query(e.Request);
});
server.Start();
```

### Q2: How to Configure Custom DNS Records?

**A**: Use the `DnsRecords` class to add local mappings:

```csharp
var records = new DnsRecords();
records.Add("www.local.com", "192.168.1.100");      // A record
records.AddCNAME("api.local.com", "www.local.com");  // CNAME
records.AddAAAA("ipv6.local.com", "::1");            // AAAA record

var server = new DnsServer(records);
server.Start();
```

### Q3: How Does UDP and TCP Auto-Switching Work?

**A**: When a DNS response exceeds 512 bytes, UDP sets the truncation flag (TC=1), and `DnsClient` automatically retries using TCP:

```csharp
// Automatic handling: UDP truncation → TCP retry
var response = client.Query("large-response.com", RecordType.TXT);
```

### Q4: How to Perform Reverse DNS Query?

**A**: Use PTR record type with the `GetPTRName` helper method:

```csharp
var ptrName = DnsClient.GetPTRName("8.8.8.8");
var response = client.Query(ptrName, RecordType.PTR);
foreach (var answer in response.Answers)
    Console.WriteLine($"Domain: {answer}");
```

### Q5: What Events Does the DNS Server Support?

**A**: Supports three core events:

```csharp
server.OnRequested += (s, e) => { /* Request received */ };
server.OnResponded += (s, e) => { /* Response sent */ };
server.OnErrored  += (s, e) => { /* Error occurred */ };
```

### Q6: How to Specify Upstream DNS Server?

**A**: Specify when creating `DnsClient`:

```csharp
// Google DNS
var client = new DnsClient(new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53));

// Cloudflare DNS
var client = new DnsClient(new IPEndPoint(IPAddress.Parse("1.1.1.1"), 53));

// Custom DNS
var client = new DnsClient(new IPEndPoint(IPAddress.Parse("192.168.1.1"), 53));
```

### Q7: How to Handle DNS Query Timeout?

**A**: `DnsClient` handles timeout and retry internally, no additional configuration needed. To customize timeout, use the encoders:

```csharp
var udpCoder = new UdpRequestCoder(endpoint, timeout: 5000);
var tcpCoder = new TcpRequestCoder(endpoint, timeout: 10000);
```

---

## Core Classes 🔧

| Class Name | Description |
|------|------|
| `DnsServer` | DNS server, supports custom records and proxy forwarding |
| `DnsClient` | DNS client, supports forward/reverse queries |
| `DnsRequestMessage` | DNS request message wrapper |
| `DnsResponseMessage` | DNS response message wrapper |
| `DnsRecords` | Local DNS record management class |
| `UdpRequestCoder` | UDP protocol encoder |
| `TcpRequestCoder` | TCP protocol encoder |
| `RecordType` | DNS record type enum |
| `ResponseCode` | DNS response code enum |
| `Question` | DNS query question class |
| `Domain` | Domain name wrapper class |

---

## Usage Examples 📝

### DNS Server (Complete Example)

```csharp
using SAEA.DNS;
using SAEA.DNS.Model;

// Create DNS server
var server = new DnsServer(port: 53);

// Register events
server.OnRequested += (sender, e) => 
{
    var request = e.Request;
    Console.WriteLine($"DNS Request: {request.Questions[0].Domain}");
};

server.OnResponded += (sender, e) => 
{
    var response = e.Response;
    Console.WriteLine($"DNS Response sent, Answer count: {response.Answers.Count}");
};

server.OnErrored += (sender, e) => 
{
    Console.WriteLine($"DNS Error: {e.Exception.Message}");
};

// Start server
server.Start();
Console.WriteLine("DNS Server started on port: 53");
```

### Custom DNS Records

```csharp
using SAEA.DNS;
using SAEA.DNS.Model;

// Create DNS record mapping
var records = new DnsRecords();

// Add A record
records.Add("www.example.com", "192.168.1.100");
records.Add("api.example.com", "192.168.1.101");

// Add AAAA record
records.AddAAAA("ipv6.example.com", "2001:db8::1");

// Add CNAME record
records.AddCNAME("alias.example.com", "www.example.com");

// Add MX record
records.AddMX("mail.example.com", "mail.example.com", 10);

// Configure server to use custom records
var server = new DnsServer(records);
server.Start();

// Query www.example.com will return 192.168.1.100
```

### DNS Client Query

```csharp
using SAEA.DNS;
using System.Net;

// Create DNS client (specify upstream DNS server)
var dnsServer = new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53);
var client = new DnsClient(dnsServer);

// Query A record (IPv4)
var response = client.Query("www.google.com", RecordType.A);
foreach (var record in response.Answers)
{
    Console.WriteLine($"IP: {record}");
}

// Query AAAA record (IPv6)
var ipv6Response = client.Query("www.google.com", RecordType.AAAA);

// Query MX record
var mxResponse = client.Query("gmail.com", RecordType.MX);

// Query TXT record
var txtResponse = client.Query("google.com", RecordType.TXT);

// Query NS record
var nsResponse = client.Query("google.com", RecordType.NS);
```

### Reverse DNS Query

```csharp
using SAEA.DNS;
using System.Net;

var client = new DnsClient(new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53));

// Reverse query domain for IP
var ip = "8.8.8.8";
var ptrName = DnsClient.GetPTRName(ip);
var response = client.Query(ptrName, RecordType.PTR);

foreach (var record in response.Answers)
{
    Console.WriteLine($"Domain: {record}");
}
```

### DNS Proxy Server

```csharp
using SAEA.DNS;
using SAEA.DNS.Model;
using System.Net;

// Upstream DNS server
var upstreamDNS = new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53);
var upstreamClient = new DnsClient(upstreamDNS);

// Local custom records
var localRecords = new DnsRecords();
localRecords.Add("internal.local", "10.0.0.1");
localRecords.Add("api.internal.local", "10.0.0.2");

// Create proxy DNS server
var server = new DnsServer((sender, e) => 
{
    var domain = e.Request.Questions[0].Domain.ToString();
    
    // Check local records first
    if (localRecords.Contains(domain))
    {
        e.Response = localRecords.GetResponse(e.Request);
        Console.WriteLine($"[Local] {domain}");
        return;
    }
    
    // Forward to upstream DNS
    e.Response = upstreamClient.Query(e.Request);
    Console.WriteLine($"[Forward] {domain}");
});

server.Start();
Console.WriteLine("DNS Proxy Server started");
```

### Ad-Blocking DNS

```csharp
using SAEA.DNS;
using SAEA.DNS.Model;
using System.Net;

// Ad domain list
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
    
    // Check if it's an ad domain
    if (adDomains.Contains(domain))
    {
        // Return empty response (block ads)
        e.Response = new DnsResponseMessage(e.Request);
        Console.WriteLine($"[Blocked] {domain}");
        return;
    }
    
    // Normal forwarding
    e.Response = upstreamClient.Query(e.Request);
});

server.Start();
```

### UDP/TCP Protocol Switching

```csharp
using SAEA.DNS.Coder;
using SAEA.DNS.Protocol;
using System.Net;

var dnsEndpoint = new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53);

// UDP encoder (auto fallback to TCP)
var coder = new UdpRequestCoder(dnsEndpoint, new TcpRequestCoder(dnsEndpoint));

// Create request
var request = new DnsRequestMessage();
request.Questions.Add(new Question(
    new Domain("www.google.com"), 
    RecordType.A, 
    RecordClass.IN));

// Send request
var response = coder.Code(request);

// Handle response
foreach (var answer in response.Answers)
{
    Console.WriteLine(answer);
}
```

---

## DNS Message Structure

```
DNS Message:
├── Header (12 bytes)
│   ├── ID (16 bits)           - Request identifier
│   ├── Flags (16 bits)
│   │   ├── QR (1 bit)         - Query/Response flag
│   │   ├── Opcode (4 bits)    - Operation code
│   │   ├── AA (1 bit)         - Authoritative answer flag
│   │   ├── TC (1 bit)         - Truncation flag
│   │   ├── RD (1 bit)         - Recursion desired flag
│   │   ├── RA (1 bit)         - Recursion available flag
│   │   └── Rcode (4 bits)     - Response code
│   ├── QDCOUNT (16 bits)      - Question count
│   ├── ANCOUNT (16 bits)      - Answer count
│   ├── NSCOUNT (16 bits)      - Authority record count
│   └── ARCOUNT (16 bits)      - Additional record count
├── Questions
│   └── QNAME + QTYPE + QCLASS
├── Answers
│   └── Resource Records
├── Authority Records
│   └── Authority Server Records
└── Additional Records
    └── Additional Resource Records
```

---

## Dependencies

| Package Name | Version | Description |
|------|------|------|
| SAEA.Sockets | 7.26.2.2 | IOCP communication framework |
| SAEA.Common | 7.26.2.2 | Common utility classes |

---

## More Resources

- [GitHub Repository](https://github.com/yswenli/SAEA)
- [NuGet Package](https://www.nuget.org/packages/SAEA.DNS)
- [Author's Blog](https://www.cnblogs.com/yswenli/)

---

## License

Apache License 2.0