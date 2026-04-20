# SAEA.P2P Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a complete P2P communication component with NAT traversal, relay fallback, local discovery, authentication and encryption.

**Architecture:** Layered design with Builder pattern for configuration, Protocol layer extending SAEA.Sockets.BaseCoder, NAT/HolePunch module for UDP traversal, Relay module for fallback, and Security module for auth/encryption.

**Tech Stack:** SAEA.Sockets (TCP/UDP/IOCP), SAEA.Common (AESHelper, LogHelper, SerializeHelper), .NET Standard 2.0

---

## Phase 1: Core Framework

### Task 1: Error Code Definitions

**Files:**
- Create: `Src/SAEA.P2P/Common/ErrorCode.cs`
- Test: Verify compilation

**Step 1: Write ErrorCode class**

```csharp
namespace SAEA.P2P.Common
{
    public static class ErrorCode
    {
        public const string EP01 = "EP01";
        public const string EP01_Desc = "服务器地址未配置，请先调用SetServer设置信令地址";
        
        public const string EP02 = "EP02";
        public const string EP02_Desc = "服务器地址格式错误，请使用有效域名/IP";
        
        public const string EP03 = "EP03";
        public const string EP03_Desc = "端口范围无效，端口应在1-65535范围内";
        
        public const string EP04 = "EP04";
        public const string EP04_Desc = "节点ID未设置，请调用SetNodeId设置唯一标识";
        
        public const string EP05 = "EP05";
        public const string EP05_Desc = "节点ID格式错误，使用字母数字组合，长度≤64";
        
        public const string EP06 = "EP06";
        public const string EP06_Desc = "加密密钥未设置，请提供AES密钥";
        
        public const string EP07 = "EP07";
        public const string EP07_Desc = "加密密钥长度错误，请使用16/24/32字节密钥";
        
        public const string EP08 = "EP08";
        public const string EP08_Desc = "超时值无效，请设置合理的超时值";
        
        public const string EP09 = "EP09";
        public const string EP09_Desc = "Builder配置不完整，请确保调用必要的Set方法";
        
        public const string ED01 = "ED01";
        public const string ED01_Desc = "发送数据为空，请提供有效数据";
        
        public const string ED02 = "ED02";
        public const string ED02_Desc = "发送数据超长，UDP数据超过64KB限制";
        
        public const string ED03 = "ED03";
        public const string ED03_Desc = "消息解码失败，协议格式不匹配";
        
        public const string ED06 = "ED06";
        public const string ED06_Desc = "目标节点不存在，请先建立连接";
        
        public const string EO01 = "EO01";
        public const string EO01_Desc = "未连接时发送，请先调用Connect";
        
        public const string EO02 = "EO02";
        public const string EO02_Desc = "重复连接，已处于连接状态";
        
        public const string EO03 = "EO03";
        public const string EO03_Desc = "连接超时，网络不可达或服务器无响应";
        
        public const string EH01 = "EH01";
        public const string EH01_Desc = "NAT类型不支持穿透，将自动降级中继";
        
        public const string EH02 = "EH02";
        public const string EH02_Desc = "打洞超时，双方未能完成打洞";
        
        public const string ER01 = "ER01";
        public const string ER01_Desc = "中继服务未启用，请调用EnableRelay";
        
        public const string ER02 = "ER02";
        public const string ER02_Desc = "中继服务器断开，请检查服务器状态";
        
        public const string EA01 = "EA01";
        public const string EA01_Desc = "身份认证失败，密钥不匹配或节点未注册";
        
        public const string EA03 = "EA03";
        public const string EA03_Desc = "节点ID重复，请更换唯一节点ID";
        
        public const string EE01 = "EE01";
        public const string EE01_Desc = "加密初始化失败，密钥格式错误";
        
        public const string EE02 = "EE02";
        public const string EE02_Desc = "解密失败，密钥不匹配或数据损坏";
        
        public static string GetDescription(string code)
        {
            switch (code)
            {
                case EP01: return EP01_Desc;
                case EP02: return EP02_Desc;
                case EP03: return EP03_Desc;
                case EP04: return EP04_Desc;
                case EP05: return EP05_Desc;
                case EP06: return EP06_Desc;
                case EP07: return EP07_Desc;
                case EP08: return EP08_Desc;
                case EP09: return EP09_Desc;
                case ED01: return ED01_Desc;
                case ED02: return ED02_Desc;
                case ED03: return ED03_Desc;
                case ED06: return ED06_Desc;
                case EO01: return EO01_Desc;
                case EO02: return EO02_Desc;
                case EO03: return EO03_Desc;
                case EH01: return EH01_Desc;
                case EH02: return EH02_Desc;
                case ER01: return ER01_Desc;
                case ER02: return ER02_Desc;
                case EA01: return EA01_Desc;
                case EA03: return EA03_Desc;
                case EE01: return EE01_Desc;
                case EE02: return EE02_Desc;
                default: return "未知错误";
            }
        }
    }
}
```

**Step 2: Verify compilation**

Run: `dotnet build Src/SAEA.P2P/SAEA.P2P.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Src/SAEA.P2P/Common/ErrorCode.cs
git commit -m "feat(p2p): add ErrorCode definitions"
```

---

### Task 2: P2PException Class

**Files:**
- Create: `Src/SAEA.P2P/Common/P2PException.cs`

**Step 1: Write P2PException class**

```csharp
using System;

namespace SAEA.P2P.Common
{
    public class P2PException : Exception
    {
        public string ErrorCode { get; }
        
        public P2PException(string errorCode) 
            : base(ErrorCode.GetDescription(errorCode))
        {
            ErrorCode = errorCode;
        }
        
        public P2PException(string errorCode, string message) 
            : base(message)
        {
            ErrorCode = errorCode;
        }
        
        public P2PException(string errorCode, Exception innerException) 
            : base(ErrorCode.GetDescription(errorCode), innerException)
        {
            ErrorCode = errorCode;
        }
        
        public P2PException(string errorCode, string message, Exception innerException) 
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
        
        public override string ToString()
        {
            return $"[{ErrorCode}] {Message}";
        }
    }
}
```

**Step 2: Verify compilation**

Run: `dotnet build Src/SAEA.P2P/SAEA.P2P.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Src/SAEA.P2P/Common/P2PException.cs
git commit -m "feat(p2p): add P2PException class"
```

---

### Task 3: P2PMessageType Enum

**Files:**
- Create: `Src/SAEA.P2P/Protocol/P2PMessageType.cs`

**Step 1: Write P2PMessageType enum**

```csharp
namespace SAEA.P2P.Protocol
{
    public enum P2PMessageType : byte
    {
        Register       = 0x10,
        RegisterAck    = 0x11,
        Unregister     = 0x12,
        NodeList       = 0x13,
        NodeQuery      = 0x14,
        NodeQueryAck   = 0x15,
        
        NatProbe       = 0x20,
        NatProbeAck    = 0x21,
        NatTypeReport  = 0x22,
        
        PunchRequest   = 0x30,
        PunchReady     = 0x31,
        PunchStart     = 0x32,
        PunchSync      = 0x33,
        PunchAck       = 0x34,
        
        RelayRequest   = 0x40,
        RelayAck       = 0x41,
        RelayData      = 0x42,
        RelayEnd       = 0x43,
        
        LocalDiscover  = 0x50,
        LocalDiscoverAck = 0x51,
        
        AuthChallenge  = 0x60,
        AuthResponse   = 0x61,
        AuthSuccess    = 0x62,
        AuthFailed     = 0x63,
        KeyExchange    = 0x64,
        
        Heartbeat      = 0x70,
        HeartbeatAck   = 0x71,
        
        UserData       = 0x80,
        UserDataAck    = 0x81
    }
}
```

**Step 2: Verify compilation**

Run: `dotnet build Src/SAEA.P2P/SAEA.P2P.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Src/SAEA.P2P/Protocol/P2PMessageType.cs
git commit -m "feat(p2p): add P2PMessageType enum"
```

---

### Task 4: NATType Enum

**Files:**
- Create: `Src/SAEA.P2P/NAT/NATType.cs`

**Step 1: Write NATType enum**

```csharp
namespace SAEA.P2P.NAT
{
    public enum NATType
    {
        Unknown = 0,
        FullCone = 1,
        RestrictedCone = 2,
        PortRestrictedCone = 3,
        Symmetric = 4
    }
}
```

**Step 2: Verify compilation**

Run: `dotnet build Src/SAEA.P2P/SAEA.P2P.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Src/SAEA.P2P/NAT/NATType.cs
git commit -m "feat(p2p): add NATType enum"
```

---

### Task 5: HolePunchStrategy Enum

**Files:**
- Create: `Src/SAEA.P2P/NAT/HolePunchStrategy.cs`

**Step 1: Write HolePunchStrategy enum**

```csharp
namespace SAEA.P2P.NAT
{
    public enum HolePunchStrategy
    {
        PreferDirect = 0,
        PreferRelay = 1,
        DirectOnly = 2,
        RelayOnly = 3
    }
}
```

**Step 2: Verify compilation**

Run: `dotnet build Src/SAEA.P2P/SAEA.P2P.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Src/SAEA.P2P/NAT/HolePunchStrategy.cs
git commit -m "feat(p2p): add HolePunchStrategy enum"
```

---

### Task 6: NodeState Enum

**Files:**
- Create: `Src/SAEA.P2P/Common/NodeState.cs`

**Step 1: Write NodeState enum**

```csharp
namespace SAEA.P2P.Common
{
    public enum NodeState
    {
        Init = 0,
        Connecting = 1,
        Authenticating = 2,
        Registered = 3,
        HolePunching = 4,
        Relaying = 5,
        Connected = 6,
        Idle = 7,
        Disconnected = 8,
        Error = 9
    }
}
```

**Step 2: Verify compilation**

Run: `dotnet build Src/SAEA.P2P/SAEA.P2P.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Src/SAEA.P2P/Common/NodeState.cs
git commit -m "feat(p2p): add NodeState enum"
```

---

### Task 7: ChannelType Enum

**Files:**
- Create: `Src/SAEA.P2P/Channel/ChannelType.cs`

**Step 1: Write ChannelType enum**

```csharp
namespace SAEA.P2P.Channel
{
    public enum ChannelType
    {
        Direct = 0,
        Relay = 1,
        Local = 2
    }
}
```

**Step 2: Verify compilation**

Run: `dotnet build Src/SAEA.P2P/SAEA.P2P.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Src/SAEA.P2P/Channel/ChannelType.cs
git commit -m "feat(p2p): add ChannelType enum"
```

---

### Task 8: P2POptions Model

**Files:**
- Create: `Src/SAEA.P2P/Builder/P2POptions.cs`

**Step 1: Write P2POptions class**

```csharp
using System;
using SAEA.P2P.NAT;

namespace SAEA.P2P.Builder
{
    public class P2POptions
    {
        public string ServerAddress { get; set; }
        public int ServerPort { get; set; } = 39654;
        public string NodeId { get; set; }
        public string NodeIdPassword { get; set; }
        
        public bool EnableHolePunch { get; set; } = true;
        public HolePunchStrategy HolePunchStrategy { get; set; } = HolePunchStrategy.PreferDirect;
        public int HolePunchTimeout { get; set; } = 10000;
        public int HolePunchRetry { get; set; } = 3;
        public NATType? OverrideNATType { get; set; }
        
        public bool EnableRelay { get; set; } = true;
        public int RelayTimeout { get; set; } = 60000;
        public long MaxRelayQuota { get; set; } = 10 * 1024 * 1024;
        
        public bool EnableLocalDiscovery { get; set; }
        public int DiscoveryPort { get; set; } = 39655;
        public string MulticastGroup { get; set; } = "224.0.0.250";
        public int DiscoveryInterval { get; set; } = 5000;
        
        public bool EnableEncryption { get; set; }
        public string EncryptionKey { get; set; }
        public bool EnableTls { get; set; }
        
        public int ConnectTimeout { get; set; } = 5000;
        public int FreeTime { get; set; } = 180000;
        public int PeerHeartbeat { get; set; } = 30000;
        public int PeerHeartbeatRetry { get; set; } = 3;
        
        public bool EnableLogging { get; set; } = true;
        public int LogLevel { get; set; } = 2;
        public string LogPath { get; set; }
        public bool LogToConsole { get; set; } = true;
        
        public void Validate()
        {
            if (!string.IsNullOrEmpty(ServerAddress))
            {
                if (ServerPort <= 0 || ServerPort > 65535)
                    throw new P2PException(Common.ErrorCode.EP03);
            }
            
            if (string.IsNullOrEmpty(NodeId))
                throw new P2PException(Common.ErrorCode.EP04);
            
            if (NodeId.Length > 64)
                throw new P2PException(Common.ErrorCode.EP05);
            
            if (EnableEncryption && string.IsNullOrEmpty(EncryptionKey))
                throw new P2PException(Common.ErrorCode.EP06);
            
            if (EnableEncryption && EncryptionKey != null)
            {
                var keyLen = EncryptionKey.Length;
                if (keyLen != 16 && keyLen != 24 && keyLen != 32)
                    throw new P2PException(Common.ErrorCode.EP07);
            }
            
            if (ConnectTimeout <= 0)
                throw new P2PException(Common.ErrorCode.EP08);
        }
    }
}
```

**Step 2: Verify compilation**

Run: `dotnet build Src/SAEA.P2P/SAEA.P2P.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Src/SAEA.P2P/Builder/P2POptions.cs
git commit -m "feat(p2p): add P2POptions model"
```

---

### Task 9: P2PServerOptions Model

**Files:**
- Create: `Src/SAEA.P2P/Builder/P2PServerOptions.cs`

**Step 1: Write P2PServerOptions class**

```csharp
using System;

namespace SAEA.P2P.Builder
{
    public class P2PServerOptions
    {
        public string BindIP { get; set; } = "0.0.0.0";
        public int Port { get; set; } = 39654;
        public int MaxNodes { get; set; } = 1000;
        
        public bool EnableRelay { get; set; } = true;
        public int MaxRelayConnections { get; set; } = 100;
        public long MaxRelayQuotaPerNode { get; set; } = 10 * 1024 * 1024;
        
        public bool EnableTls { get; set; }
        public string CertificatePath { get; set; }
        public string CertificatePassword { get; set; }
        
        public int FreeTime { get; set; } = 180000;
        public int ActionTimeout { get; set; } = 180000;
        
        public bool EnableLogging { get; set; } = true;
        public int LogLevel { get; set; } = 2;
        public string LogPath { get; set; }
        
        public void Validate()
        {
            if (Port <= 0 || Port > 65535)
                throw new P2PException(Common.ErrorCode.EP03);
            
            if (MaxNodes <= 0)
                throw new P2PException(Common.ErrorCode.EP08);
            
            if (EnableTls && string.IsNullOrEmpty(CertificatePath))
                throw new P2PException(Common.ErrorCode.EP09, "TLS需要证书路径");
        }
    }
}
```

**Step 2: Verify compilation**

Run: `dotnet build Src/SAEA.P2P/SAEA.P2P.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Src/SAEA.P2P/Builder/P2PServerOptions.cs
git commit -m "feat(p2p): add P2PServerOptions model"
```

---

### Task 10: P2PProtocol Class

**Files:**
- Create: `Src/SAEA.P2P/Protocol/P2PProtocol.cs`

**Step 1: Write P2PProtocol class extending BaseSocketProtocal**

```csharp
using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;

namespace SAEA.P2P.Protocol
{
    public class P2PProtocol : BaseSocketProtocal
    {
        public P2PProtocol()
        {
        }
        
        public P2PProtocol(P2PMessageType type, byte[] content)
        {
            Type = (byte)type;
            if (content != null)
            {
                BodyLength = content.Length;
                Content = content;
            }
            else
            {
                BodyLength = 0;
                Content = null;
            }
        }
        
        public static P2PProtocol Create(P2PMessageType type)
        {
            return new P2PProtocol(type, null);
        }
        
        public static P2PProtocol Create(P2PMessageType type, byte[] content)
        {
            return new P2PProtocol(type, content);
        }
        
        public P2PMessageType GetMessageType()
        {
            return (P2PMessageType)Type;
        }
    }
}
```

**Step 2: Verify compilation**

Run: `dotnet build Src/SAEA.P2P/SAEA.P2P.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Src/SAEA.P2P/Protocol/P2PProtocol.cs
git commit -m "feat(p2p): add P2PProtocol extending BaseSocketProtocal"
```

---

### Task 11: P2PCoder Class

**Files:**
- Create: `Src/SAEA.P2P/Protocol/P2PCoder.cs`

**Step 1: Write P2PCoder class extending BaseCoder**

```csharp
using System.Collections.Generic;
using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;

namespace SAEA.P2P.Protocol
{
    public class P2PCoder : BaseCoder
    {
        public P2PCoder()
        {
        }
        
        public List<P2PProtocol> DecodeP2P(byte[] data)
        {
            var result = new List<P2PProtocol>();
            var protocols = Decode(data);
            
            foreach (var protocol in protocols)
            {
                var p2pProtocol = new P2PProtocol();
                p2pProtocol.BodyLength = protocol.BodyLength;
                p2pProtocol.Type = protocol.Type;
                p2pProtocol.Content = protocol.Content;
                result.Add(p2pProtocol);
            }
            
            return result;
        }
        
        public byte[] EncodeP2P(P2PMessageType type)
        {
            var protocol = P2PProtocol.Create(type);
            return Encode(protocol);
        }
        
        public byte[] EncodeP2P(P2PMessageType type, byte[] content)
        {
            var protocol = P2PProtocol.Create(type, content);
            return Encode(protocol);
        }
    }
}
```

**Step 2: Verify compilation**

Run: `dotnet build Src/SAEA.P2P/SAEA.P2P.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Src/SAEA.P2P/Protocol/P2PCoder.cs
git commit -m "feat(p2p): add P2PCoder extending BaseCoder"
```

---

### Task 12: NodeInfo Model

**Files:**
- Create: `Src/SAEA.P2P/Model/NodeInfo.cs`

**Step 1: Write NodeInfo class**

```csharp
using System;
using System.Net;
using SAEA.P2P.NAT;

namespace SAEA.P2P.Model
{
    public class NodeInfo
    {
        public string NodeId { get; set; }
        public string NodeName { get; set; }
        public IPEndPoint PublicAddress { get; set; }
        public IPEndPoint LocalAddress { get; set; }
        public NATType NatType { get; set; } = NATType.Unknown;
        public DateTime RegisteredTime { get; set; }
        public DateTime LastActiveTime { get; set; }
        public string[] Services { get; set; }
        public NodeState State { get; set; } = NodeState.Init;
        
        public bool IsOnline
        {
            get { return State != NodeState.Disconnected && State != NodeState.Error; }
        }
        
        public override string ToString()
        {
            return $"NodeInfo: {NodeId}, Public: {PublicAddress}, NAT: {NatType}, State: {State}";
        }
    }
}
```

**Step 2: Verify compilation**

Run: `dotnet build Src/SAEA.P2P/SAEA.P2P.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Src/SAEA.P2P/Model/NodeInfo.cs
git commit -m "feat(p2p): add NodeInfo model"
```

---

### Task 13: PeerSession Model

**Files:**
- Create: `Src/SAEA.P2P/Core/PeerSession.cs`

**Step 1: Write PeerSession class**

```csharp
using System;
using System.Net;
using SAEA.P2P.Channel;
using SAEA.P2P.Common;

namespace SAEA.P2P.Core
{
    public class PeerSession
    {
        public string SessionId { get; set; }
        public string PeerId { get; set; }
        public NodeState State { get; set; } = NodeState.Init;
        public IPEndPoint PublicAddress { get; set; }
        public IPEndPoint LocalAddress { get; set; }
        public ChannelType Channel { get; set; } = ChannelType.Direct;
        
        public DateTime CreatedTime { get; set; } = DateTime.Now;
        public DateTime LastActiveTime { get; set; } = DateTime.Now;
        
        public int SendSequence { get; set; } = 0;
        public int ReceiveSequence { get; set; } = 0;
        
        public string RelaySessionId { get; set; }
        
        public bool IsExpired(int timeoutMs)
        {
            return (DateTime.Now - LastActiveTime).TotalMilliseconds > timeoutMs;
        }
        
        public void Active()
        {
            LastActiveTime = DateTime.Now;
        }
        
        public int NextSendSeq()
        {
            return ++SendSequence;
        }
        
        public override string ToString()
        {
            return $"PeerSession: {SessionId}, Peer: {PeerId}, Channel: {Channel}, State: {State}";
        }
    }
}
```

**Step 2: Verify compilation**

Run: `dotnet build Src/SAEA.P2P/SAEA.P2P.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Src/SAEA.P2P/Core/PeerSession.cs
git commit -m "feat(p2p): add PeerSession model"
```

---

### Task 14: P2PClientBuilder - Part 1

**Files:**
- Create: `Src/SAEA.P2P/Builder/P2PClientBuilder.cs`

**Step 1: Write P2PClientBuilder class (basic structure)**

```csharp
using System;
using System.Net;
using SAEA.P2P.NAT;

namespace SAEA.P2P.Builder
{
    public class P2PClientBuilder
    {
        private readonly P2POptions _options;
        
        public P2PClientBuilder()
        {
            _options = new P2POptions();
        }
        
        public P2PClientBuilder SetServer(string address, int port)
        {
            if (string.IsNullOrEmpty(address))
                throw new P2PException(Common.ErrorCode.EP02);
            
            if (port <= 0 || port > 65535)
                throw new P2PException(Common.ErrorCode.EP03);
            
            _options.ServerAddress = address;
            _options.ServerPort = port;
            return this;
        }
        
        public P2PClientBuilder SetServer(IPEndPoint endpoint)
        {
            return SetServer(endpoint.Address.ToString(), endpoint.Port);
        }
        
        public P2PClientBuilder SetNodeId(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
                throw new P2PException(Common.ErrorCode.EP04);
            
            if (nodeId.Length > 64)
                throw new P2PException(Common.ErrorCode.EP05);
            
            _options.NodeId = nodeId;
            return this;
        }
        
        public P2PClientBuilder SetNodeIdPassword(string password)
        {
            _options.NodeIdPassword = password;
            return this;
        }
        
        public P2PClientBuilder SetTimeout(int timeoutMs)
        {
            if (timeoutMs <= 0)
                throw new P2PException(Common.ErrorCode.EP08);
            
            _options.ConnectTimeout = timeoutMs;
            return this;
        }
        
        public P2POptions GetOptions()
        {
            return _options;
        }
    }
}
```

**Step 2: Verify compilation**

Run: `dotnet build Src/SAEA.P2P/SAEA.P2P.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Src/SAEA.P2P/Builder/P2PClientBuilder.cs
git commit -m "feat(p2p): add P2PClientBuilder basic structure"
```

---

### Task 15: P2PClientBuilder - Part 2 (HolePunch Methods)

**Files:**
- Modify: `Src/SAEA.P2P/Builder/P2PClientBuilder.cs`

**Step 1: Add HolePunch configuration methods**

Add after `SetTimeout` method:

```csharp
        public P2PClientBuilder EnableHolePunch()
        {
            _options.EnableHolePunch = true;
            return this;
        }
        
        public P2PClientBuilder EnableHolePunch(HolePunchStrategy strategy)
        {
            _options.EnableHolePunch = true;
            _options.HolePunchStrategy = strategy;
            return this;
        }
        
        public P2PClientBuilder SetHolePunchTimeout(int timeoutMs)
        {
            _options.HolePunchTimeout = timeoutMs;
            return this;
        }
        
        public P2PClientBuilder SetHolePunchRetry(int retryCount)
        {
            _options.HolePunchRetry = retryCount;
            return this;
        }
        
        public P2PClientBuilder SetNATType(NATType natType)
        {
            _options.OverrideNATType = natType;
            return this;
        }
```

**Step 2: Verify compilation**

Run: `dotnet build Src/SAEA.P2P/SAEA.P2P.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Src/SAEA.P2P/Builder/P2PClientBuilder.cs
git commit -m "feat(p2p): add HolePunch config methods to P2PClientBuilder"
```

---

### Task 16: P2PClientBuilder - Part 3 (Relay & Discovery)

**Files:**
- Modify: `Src/SAEA.P2P/Builder/P2PClientBuilder.cs`

**Step 1: Add Relay and Discovery configuration methods**

Add after `SetNATType` method:

```csharp
        public P2PClientBuilder EnableRelay()
        {
            _options.EnableRelay = true;
            return this;
        }
        
        public P2PClientBuilder EnableRelay(int timeoutMs, long maxQuota)
        {
            _options.EnableRelay = true;
            _options.RelayTimeout = timeoutMs;
            _options.MaxRelayQuota = maxQuota;
            return this;
        }
        
        public P2PClientBuilder EnableLocalDiscovery()
        {
            _options.EnableLocalDiscovery = true;
            return this;
        }
        
        public P2PClientBuilder EnableLocalDiscovery(int port)
        {
            _options.EnableLocalDiscovery = true;
            _options.DiscoveryPort = port;
            return this;
        }
        
        public P2PClientBuilder EnableLocalDiscovery(int port, string multicastGroup)
        {
            _options.EnableLocalDiscovery = true;
            _options.DiscoveryPort = port;
            _options.MulticastGroup = multicastGroup;
            return this;
        }
        
        public P2PClientBuilder SetDiscoveryInterval(int intervalMs)
        {
            _options.DiscoveryInterval = intervalMs;
            return this;
        }
```

**Step 2: Verify compilation**

Run: `dotnet build Src/SAEA.P2P/SAEA.P2P.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Src/SAEA.P2P/Builder/P2PClientBuilder.cs
git commit -m "feat(p2p): add Relay and Discovery config to P2PClientBuilder"
```

---

### Task 17: P2PClientBuilder - Part 4 (Security & Logging)

**Files:**
- Modify: `Src/SAEA.P2P/Builder/P2PClientBuilder.cs`

**Step 1: Add Security and Logging configuration methods**

Add after `SetDiscoveryInterval` method:

```csharp
        public P2PClientBuilder EnableEncryption()
        {
            _options.EnableEncryption = true;
            return this;
        }
        
        public P2PClientBuilder EnableEncryption(string key)
        {
            if (!string.IsNullOrEmpty(key))
            {
                var keyLen = key.Length;
                if (keyLen != 16 && keyLen != 24 && keyLen != 32)
                    throw new P2PException(Common.ErrorCode.EP07);
            }
            
            _options.EnableEncryption = true;
            _options.EncryptionKey = key;
            return this;
        }
        
        public P2PClientBuilder EnableTls()
        {
            _options.EnableTls = true;
            return this;
        }
        
        public P2PClientBuilder SetFreeTime(int freeTimeMs)
        {
            _options.FreeTime = freeTimeMs;
            return this;
        }
        
        public P2PClientBuilder SetPeerHeartbeat(int intervalMs)
        {
            _options.PeerHeartbeat = intervalMs;
            return this;
        }
        
        public P2PClientBuilder EnableLogging()
        {
            _options.EnableLogging = true;
            return this;
        }
        
        public P2PClientBuilder EnableLogging(int level)
        {
            _options.EnableLogging = true;
            _options.LogLevel = level;
            return this;
        }
        
        public P2PClientBuilder EnableLogging(int level, string path)
        {
            _options.EnableLogging = true;
            _options.LogLevel = level;
            _options.LogPath = path;
            return this;
        }
        
        public P2PClientBuilder SetLogToConsole(bool enable)
        {
            _options.LogToConsole = enable;
            return this;
        }
```

**Step 2: Verify compilation**

Run: `dotnet build Src/SAEA.P2P/SAEA.P2P.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Src/SAEA.P2P/Builder/P2PClientBuilder.cs
git commit -m "feat(p2p): add Security and Logging config to P2PClientBuilder"
```

---

### Task 18: P2PClientBuilder - Part 5 (Build Method)

**Files:**
- Modify: `Src/SAEA.P2P/Builder/P2PClientBuilder.cs`

**Step 1: Add Build method**

Add after `SetLogToConsole` method:

```csharp
        public P2POptions Build()
        {
            _options.Validate();
            return _options;
        }
```

**Step 2: Verify compilation**

Run: `dotnet build Src/SAEA.P2P/SAEA.P2P.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Src/SAEA.P2P/Builder/P2PClientBuilder.cs
git commit -m "feat(p2p): add Build method to P2PClientBuilder"
```

---

### Task 19: P2PServerBuilder

**Files:**
- Create: `Src/SAEA.P2P/Builder/P2PServerBuilder.cs`

**Step 1: Write P2PServerBuilder class**

```csharp
using System;

namespace SAEA.P2P.Builder
{
    public class P2PServerBuilder
    {
        private readonly P2PServerOptions _options;
        
        public P2PServerBuilder()
        {
            _options = new P2PServerOptions();
        }
        
        public P2PServerBuilder SetPort(int port)
        {
            if (port <= 0 || port > 65535)
                throw new P2PException(Common.ErrorCode.EP03);
            
            _options.Port = port;
            return this;
        }
        
        public P2PServerBuilder SetIP(string ip)
        {
            _options.BindIP = ip;
            return this;
        }
        
        public P2PServerBuilder SetMaxNodes(int maxNodes)
        {
            _options.MaxNodes = maxNodes;
            return this;
        }
        
        public P2PServerBuilder EnableRelay()
        {
            _options.EnableRelay = true;
            return this;
        }
        
        public P2PServerBuilder EnableRelay(int maxConnections, long maxQuotaPerNode)
        {
            _options.EnableRelay = true;
            _options.MaxRelayConnections = maxConnections;
            _options.MaxRelayQuotaPerNode = maxQuotaPerNode;
            return this;
        }
        
        public P2PServerBuilder EnableTls(string certPath, string password)
        {
            _options.EnableTls = true;
            _options.CertificatePath = certPath;
            _options.CertificatePassword = password;
            return this;
        }
        
        public P2PServerBuilder SetFreeTime(int freeTimeMs)
        {
            _options.FreeTime = freeTimeMs;
            return this;
        }
        
        public P2PServerBuilder EnableLogging()
        {
            _options.EnableLogging = true;
            return this;
        }
        
        public P2PServerBuilder EnableLogging(int level)
        {
            _options.EnableLogging = true;
            _options.LogLevel = level;
            return this;
        }
        
        public P2PServerBuilder EnableLogging(int level, string path)
        {
            _options.EnableLogging = true;
            _options.LogLevel = level;
            _options.LogPath = path;
            return this;
        }
        
        public P2PServerOptions Build()
        {
            _options.Validate();
            return _options;
        }
    }
}
```

**Step 2: Verify compilation**

Run: `dotnet build Src/SAEA.P2P/SAEA.P2P.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Src/SAEA.P2P/Builder/P2PServerBuilder.cs
git commit -m "feat(p2p): add P2PServerBuilder"
```

---

### Task 20: P2PLogHelper Wrapper

**Files:**
- Create: `Src/SAEA.P2P/Common/P2PLogHelper.cs`

**Step 1: Write P2PLogHelper class**

```csharp
using SAEA.Common;

namespace SAEA.P2P.Common
{
    internal static class P2PLogHelper
    {
        private static int _logLevel = 2;
        
        public static void SetLevel(int level)
        {
            _logLevel = level;
        }
        
        public static void Trace(string nodeId, string message)
        {
            if (_logLevel <= 0)
                LogHelper.Debug($"[{nodeId}] {message}");
        }
        
        public static void Debug(string nodeId, string message)
        {
            if (_logLevel <= 1)
                LogHelper.Debug($"[{nodeId}] {message}");
        }
        
        public static void Info(string nodeId, string message)
        {
            if (_logLevel <= 2)
                LogHelper.Info($"[{nodeId}] {message}");
        }
        
        public static void Warning(string nodeId, string message)
        {
            if (_logLevel <= 3)
                LogHelper.Warn($"[{nodeId}] {message}", null);
        }
        
        public static void Error(string nodeId, string errorCode, string message)
        {
            LogHelper.Error($"[{nodeId}] [{errorCode}] {message}", null);
        }
        
        public static void Error(string nodeId, string errorCode, string message, System.Exception ex)
        {
            LogHelper.Error($"[{nodeId}] [{errorCode}] {message}", ex);
        }
    }
}
```

**Step 2: Verify compilation**

Run: `dotnet build Src/SAEA.P2P/SAEA.P2P.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Src/SAEA.P2P/Common/P2PLogHelper.cs
git commit -m "feat(p2p): add P2PLogHelper wrapper"
```

---

## Phase 2: Test Project Setup

### Task 21: Update P2PTest csproj

**Files:**
- Modify: `Src/SAEA.P2PTest/SAEA.P2PTest.csproj`

**Step 1: Update csproj with proper references**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\SAEA.Common\SAEA.Common.csproj" />
    <ProjectReference Include="..\SAEA.Sockets\SAEA.Sockets.csproj" />
    <ProjectReference Include="..\SAEA.P2P\SAEA.P2P.csproj" />
  </ItemGroup>

</Project>
```

**Step 2: Verify compilation**

Run: `dotnet build Src/SAEA.P2PTest/SAEA.P2PTest.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Src/SAEA.P2PTest/SAEA.P2PTest.csproj
git commit -m "feat(p2p): update P2PTest csproj with references"
```

---

### Task 22: P2PTest Program.cs

**Files:**
- Modify: `Src/SAEA.P2PTest/Program.cs`

**Step 1: Write menu-driven test entry**

```csharp
using System;
using System.Threading.Tasks;
using SAEA.Common;

namespace SAEA.P2PTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            ConsoleHelper.Title = "SAEA.P2P Test";
            
            while (true)
            {
                ConsoleHelper.WriteLine("SAEA.P2P Test");
                ConsoleHelper.WriteLine("====================");
                ConsoleHelper.WriteLine("1 = Builder Test");
                ConsoleHelper.WriteLine("2 = ErrorCode Test");
                ConsoleHelper.WriteLine("3 = Protocol Test");
                ConsoleHelper.WriteLine("4 = Quick Start Demo");
                ConsoleHelper.WriteLine("5 = Server Test");
                ConsoleHelper.WriteLine("6 = Client Test");
                ConsoleHelper.WriteLine("7 = Hole Punch Test");
                ConsoleHelper.WriteLine("8 = Relay Test");
                ConsoleHelper.WriteLine("9 = Local Discovery Test");
                ConsoleHelper.WriteLine("10 = Auth & Encryption Test");
                ConsoleHelper.WriteLine("0 = Exit");
                
                var key = ConsoleHelper.ReadLine();
                
                switch (key)
                {
                    case "1":
                        BuilderTest.Run();
                        break;
                    case "2":
                        ErrorCodeTest.Run();
                        break;
                    case "3":
                        ProtocolTest.Run();
                        break;
                    case "4":
                        QuickStartTest.Run();
                        break;
                    case "5":
                        await ServerTest.RunAsync();
                        break;
                    case "6":
                        await ClientTest.RunAsync();
                        break;
                    case "7":
                        await HolePunchTest.RunAsync();
                        break;
                    case "8":
                        await RelayTest.RunAsync();
                        break;
                    case "9":
                        await LocalDiscoveryTest.RunAsync();
                        break;
                    case "10":
                        await AuthEncryptionTest.RunAsync();
                        break;
                    case "0":
                        return;
                }
                
                ConsoleHelper.WriteLine("Press any key to continue...");
                ConsoleHelper.ReadLine();
            }
        }
    }
}
```

**Step 2: Verify compilation**

Run: `dotnet build Src/SAEA.P2PTest/SAEA.P2PTest.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Src/SAEA.P2PTest/Program.cs
git commit -m "feat(p2p): add P2PTest menu-driven entry"
```

---

### Task 23: BuilderTest

**Files:**
- Create: `Src/SAEA.P2PTest/Tests/BuilderTest.cs`

**Step 1: Write BuilderTest class**

```csharp
using System;
using System.Net;
using SAEA.Common;
using SAEA.P2P.Builder;
using SAEA.P2P.NAT;

namespace SAEA.P2PTest.Tests
{
    public static class BuilderTest
    {
        public static void Run()
        {
            ConsoleHelper.WriteLine("=== Builder Test ===");
            
            TestClientBuilderBasic();
            TestClientBuilderFull();
            TestClientBuilderValidation();
            TestServerBuilder();
            
            ConsoleHelper.WriteLine("Builder Test Completed!");
        }
        
        static void TestClientBuilderBasic()
        {
            ConsoleHelper.WriteLine("Test: ClientBuilder Basic");
            
            var options = new P2PClientBuilder()
                .SetServer("127.0.0.1", 39654)
                .SetNodeId("test-node-001")
                .Build();
            
            ConsoleHelper.WriteLine($"Server: {options.ServerAddress}:{options.ServerPort}");
            ConsoleHelper.WriteLine($"NodeId: {options.NodeId}");
            ConsoleHelper.WriteLine($"HolePunch: {options.EnableHolePunch}");
            ConsoleHelper.WriteLine("PASS");
        }
        
        static void TestClientBuilderFull()
        {
            ConsoleHelper.WriteLine("Test: ClientBuilder Full Config");
            
            var options = new P2PClientBuilder()
                .SetServer("signal.example.com", 39654)
                .SetNodeId("node-full-001")
                .SetNodeIdPassword("secret-key")
                .EnableHolePunch(HolePunchStrategy.PreferDirect)
                .SetHolePunchTimeout(10000)
                .SetHolePunchRetry(5)
                .EnableRelay()
                .EnableLocalDiscovery(39655, "224.0.0.250")
                .EnableEncryption("aes-key-12345678")
                .EnableTls()
                .SetTimeout(5000)
                .EnableLogging(1)
                .Build();
            
            ConsoleHelper.WriteLine($"Strategy: {options.HolePunchStrategy}");
            ConsoleHelper.WriteLine($"DiscoveryPort: {options.DiscoveryPort}");
            ConsoleHelper.WriteLine($"EncryptionKey: {options.EncryptionKey}");
            ConsoleHelper.WriteLine("PASS");
        }
        
        static void TestClientBuilderValidation()
        {
            ConsoleHelper.WriteLine("Test: ClientBuilder Validation");
            
            try
            {
                new P2PClientBuilder()
                    .SetServer("", 39654)
                    .Build();
                ConsoleHelper.WriteLine("FAIL - should throw EP02");
            }
            catch (P2PException ex) when (ex.ErrorCode == "EP02")
            {
                ConsoleHelper.WriteLine($"Correctly caught: {ex.ErrorCode}");
                ConsoleHelper.WriteLine("PASS");
            }
            
            try
            {
                new P2PClientBuilder()
                    .SetNodeId("")
                    .Build();
                ConsoleHelper.WriteLine("FAIL - should throw EP04");
            }
            catch (P2PException ex) when (ex.ErrorCode == "EP04")
            {
                ConsoleHelper.WriteLine($"Correctly caught: {ex.ErrorCode}");
                ConsoleHelper.WriteLine("PASS");
            }
            
            try
            {
                new P2PClientBuilder()
                    .SetNodeId("test-node")
                    .EnableEncryption("short-key")
                    .Build();
                ConsoleHelper.WriteLine("FAIL - should throw EP07");
            }
            catch (P2PException ex) when (ex.ErrorCode == "EP07")
            {
                ConsoleHelper.WriteLine($"Correctly caught: {ex.ErrorCode}");
                ConsoleHelper.WriteLine("PASS");
            }
        }
        
        static void TestServerBuilder()
        {
            ConsoleHelper.WriteLine("Test: ServerBuilder");
            
            var options = new P2PServerBuilder()
                .SetPort(39654)
                .SetIP("0.0.0.0")
                .SetMaxNodes(1000)
                .EnableRelay()
                .EnableLogging()
                .Build();
            
            ConsoleHelper.WriteLine($"Port: {options.Port}");
            ConsoleHelper.WriteLine($"MaxNodes: {options.MaxNodes}");
            ConsoleHelper.WriteLine($"EnableRelay: {options.EnableRelay}");
            ConsoleHelper.WriteLine("PASS");
        }
    }
}
```

**Step 2: Verify compilation**

Run: `dotnet build Src/SAEA.P2PTest/SAEA.P2PTest.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Src/SAEA.P2PTest/Tests/BuilderTest.cs
git commit -m "feat(p2p): add BuilderTest"
```

---

### Task 24: ErrorCodeTest

**Files:**
- Create: `Src/SAEA.P2PTest/Tests/ErrorCodeTest.cs`

**Step 1: Write ErrorCodeTest class**

```csharp
using System;
using SAEA.Common;
using SAEA.P2P.Common;

namespace SAEA.P2PTest.Tests
{
    public static class ErrorCodeTest
    {
        public static void Run()
        {
            ConsoleHelper.WriteLine("=== ErrorCode Test ===");
            
            TestErrorCodeDefinitions();
            TestP2PException();
            
            ConsoleHelper.WriteLine("ErrorCode Test Completed!");
        }
        
        static void TestErrorCodeDefinitions()
        {
            ConsoleHelper.WriteLine("Test: ErrorCode Definitions");
            
            ConsoleHelper.WriteLine($"EP01: {ErrorCode.GetDescription(ErrorCode.EP01)}");
            ConsoleHelper.WriteLine($"EP02: {ErrorCode.GetDescription(ErrorCode.EP02)}");
            ConsoleHelper.WriteLine($"EO01: {ErrorCode.GetDescription(ErrorCode.EO01)}");
            ConsoleHelper.WriteLine($"EH01: {ErrorCode.GetDescription(ErrorCode.EH01)}");
            ConsoleHelper.WriteLine($"ER01: {ErrorCode.GetDescription(ErrorCode.ER01)}");
            ConsoleHelper.WriteLine($"EA01: {ErrorCode.GetDescription(ErrorCode.EA01)}");
            ConsoleHelper.WriteLine("PASS");
        }
        
        static void TestP2PException()
        {
            ConsoleHelper.WriteLine("Test: P2PException");
            
            try
            {
                throw new P2PException(ErrorCode.EP01);
            }
            catch (P2PException ex)
            {
                ConsoleHelper.WriteLine($"Exception: {ex.ToString()}");
                ConsoleHelper.WriteLine($"ErrorCode: {ex.ErrorCode}");
                ConsoleHelper.WriteLine($"Message: {ex.Message}");
            }
            
            try
            {
                throw new P2PException(ErrorCode.EO01, "Custom message");
            }
            catch (P2PException ex)
            {
                ConsoleHelper.WriteLine($"Custom: {ex.ToString()}");
            }
            
            try
            {
                try
                {
                    throw new InvalidOperationException("Inner error");
                }
                catch (Exception inner)
                {
                    throw new P2PException(ErrorCode.ED03, inner);
                }
            }
            catch (P2PException ex)
            {
                ConsoleHelper.WriteLine($"With inner: {ex.ToString()}");
                ConsoleHelper.WriteLine($"Inner type: {ex.InnerException?.GetType().Name}");
            }
            
            ConsoleHelper.WriteLine("PASS");
        }
    }
}
```

**Step 2: Verify compilation**

Run: `dotnet build Src/SAEA.P2PTest/SAEA.P2PTest.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Src/SAEA.P2PTest/Tests/ErrorCodeTest.cs
git commit -m "feat(p2p): add ErrorCodeTest"
```

---

### Task 25: ProtocolTest

**Files:**
- Create: `Src/SAEA.P2PTest/Tests/ProtocolTest.cs`

**Step 1: Write ProtocolTest class**

```csharp
using System;
using System.Text;
using SAEA.Common;
using SAEA.P2P.Protocol;

namespace SAEA.P2PTest.Tests
{
    public static class ProtocolTest
    {
        public static void Run()
        {
            ConsoleHelper.WriteLine("=== Protocol Test ===");
            
            TestMessageTypeEnum();
            TestP2PProtocol();
            TestP2PCoder();
            
            ConsoleHelper.WriteLine("Protocol Test Completed!");
        }
        
        static void TestMessageTypeEnum()
        {
            ConsoleHelper.WriteLine("Test: P2PMessageType Enum");
            
            ConsoleHelper.WriteLine($"Register: {(byte)P2PMessageType.Register} = 0x{(byte)P2PMessageType.Register:X2}");
            ConsoleHelper.WriteLine($"RegisterAck: {(byte)P2PMessageType.RegisterAck} = 0x{(byte)P2PMessageType.RegisterAck:X2}");
            ConsoleHelper.WriteLine($"NatProbe: {(byte)P2PMessageType.NatProbe} = 0x{(byte)P2PMessageType.NatProbe:X2}");
            ConsoleHelper.WriteLine($"PunchSync: {(byte)P2PMessageType.PunchSync} = 0x{(byte)P2PMessageType.PunchSync:X2}");
            ConsoleHelper.WriteLine($"RelayData: {(byte)P2PMessageType.RelayData} = 0x{(byte)P2PMessageType.RelayData:X2}");
            ConsoleHelper.WriteLine($"UserData: {(byte)P2PMessageType.UserData} = 0x{(byte)P2PMessageType.UserData:X2}");
            ConsoleHelper.WriteLine("PASS");
        }
        
        static void TestP2PProtocol()
        {
            ConsoleHelper.WriteLine("Test: P2PProtocol Create & Serialize");
            
            var protocol1 = P2PProtocol.Create(P2PMessageType.Heartbeat);
            var bytes1 = protocol1.ToBytes();
            ConsoleHelper.WriteLine($"Heartbeat bytes length: {bytes1.Length}");
            
            var data = Encoding.UTF8.GetBytes("Hello P2P");
            var protocol2 = P2PProtocol.Create(P2PMessageType.UserData, data);
            var bytes2 = protocol2.ToBytes();
            ConsoleHelper.WriteLine($"UserData bytes length: {bytes2.Length}");
            ConsoleHelper.WriteLine($"BodyLength: {protocol2.BodyLength}");
            ConsoleHelper.WriteLine($"Type: {protocol2.GetMessageType()}");
            
            ConsoleHelper.WriteLine("PASS");
        }
        
        static void TestP2PCoder()
        {
            ConsoleHelper.WriteLine("Test: P2PCoder Encode & Decode");
            
            var coder = new P2PCoder();
            
            var data = Encoding.UTF8.GetBytes("Test message content");
            var encoded = coder.EncodeP2P(P2PMessageType.UserData, data);
            ConsoleHelper.WriteLine($"Encoded length: {encoded.Length}");
            
            var decoded = coder.DecodeP2P(encoded);
            ConsoleHelper.WriteLine($"Decoded count: {decoded.Count}");
            
            if (decoded.Count > 0)
            {
                var msg = decoded[0];
                ConsoleHelper.WriteLine($"Type: {msg.GetMessageType()}");
                ConsoleHelper.WriteLine($"BodyLength: {msg.BodyLength}");
                ConsoleHelper.WriteLine($"Content: {Encoding.UTF8.GetString(msg.Content)}");
            }
            
            var heartbeat = coder.EncodeP2P(P2PMessageType.Heartbeat);
            var decoded2 = coder.DecodeP2P(heartbeat);
            ConsoleHelper.WriteLine($"Heartbeat decoded type: {decoded2[0].GetMessageType()}");
            
            ConsoleHelper.WriteLine("PASS");
        }
    }
}
```

**Step 2: Verify compilation**

Run: `dotnet build Src/SAEA.P2PTest/SAEA.P2PTest.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Src/SAEA.P2PTest/Tests/ProtocolTest.cs
git commit -m "feat(p2p): add ProtocolTest"
```

---

### Task 26: QuickStartTest

**Files:**
- Create: `Src/SAEA.P2PTest/Tests/QuickStartTest.cs`

**Step 1: Write QuickStartTest class (placeholder for future)**

```csharp
using System;
using SAEA.Common;
using SAEA.P2P.Builder;

namespace SAEA.P2PTest.Tests
{
    public static class QuickStartTest
    {
        public static void Run()
        {
            ConsoleHelper.WriteLine("=== Quick Start Demo ===");
            
            DemoClientBuilder();
            DemoServerBuilder();
            
            ConsoleHelper.WriteLine("Quick Start Demo Completed!");
            ConsoleHelper.WriteLine("Note: Full P2PQuick shortcuts will be added after P2PClient/P2PServer implementation");
        }
        
        static void DemoClientBuilder()
        {
            ConsoleHelper.WriteLine("Demo: Quick Client Builder");
            
            var options = new P2PClientBuilder()
                .SetServer("127.0.0.1", 39654)
                .SetNodeId("demo-client-001")
                .EnableHolePunch()
                .EnableRelay()
                .EnableLogging()
                .Build();
            
            ConsoleHelper.WriteLine("Client options created:");
            ConsoleHelper.WriteLine($"  Server: {options.ServerAddress}:{options.ServerPort}");
            ConsoleHelper.WriteLine($"  NodeId: {options.NodeId}");
            ConsoleHelper.WriteLine($"  HolePunch: {options.EnableHolePunch}");
            ConsoleHelper.WriteLine($"  Relay: {options.EnableRelay}");
        }
        
        static void DemoServerBuilder()
        {
            ConsoleHelper.WriteLine("Demo: Quick Server Builder");
            
            var options = new P2PServerBuilder()
                .SetPort(39654)
                .EnableRelay()
                .EnableLogging()
                .Build();
            
            ConsoleHelper.WriteLine("Server options created:");
            ConsoleHelper.WriteLine($"  Port: {options.Port}");
            ConsoleHelper.WriteLine($"  EnableRelay: {options.EnableRelay}");
        }
    }
}
```

**Step 2: Verify compilation**

Run: `dotnet build Src/SAEA.P2PTest/SAEA.P2PTest.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Src/SAEA.P2PTest/Tests/QuickStartTest.cs
git commit -m "feat(p2p): add QuickStartTest placeholder"
```

---

### Task 27: Placeholder Tests for Future Phases

**Files:**
- Create: `Src/SAEA.P2PTest/Tests/ServerTest.cs`
- Create: `Src/SAEA.P2PTest/Tests/ClientTest.cs`
- Create: `Src/SAEA.P2PTest/Tests/HolePunchTest.cs`
- Create: `Src/SAEA.P2PTest/Tests/RelayTest.cs`
- Create: `Src/SAEA.P2PTest/Tests/LocalDiscoveryTest.cs`
- Create: `Src/SAEA.P2PTest/Tests/AuthEncryptionTest.cs`

**Step 1: Write placeholder test classes**

Each file:

```csharp
using System;
using System.Threading.Tasks;
using SAEA.Common;

namespace SAEA.P2PTest.Tests
{
    public static class ServerTest
    {
        public static async Task RunAsync()
        {
            ConsoleHelper.WriteLine("=== Server Test ===");
            ConsoleHelper.WriteLine("TODO: Implement after P2PServer class is ready");
            await Task.CompletedTask;
        }
    }
}
```

(Similar placeholders for ClientTest, HolePunchTest, RelayTest, LocalDiscoveryTest, AuthEncryptionTest)

**Step 2: Verify compilation**

Run: `dotnet build Src/SAEA.P2PTest/SAEA.P2PTest.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Src/SAEA.P2PTest/Tests/
git commit -m "feat(p2p): add placeholder tests for future phases"
```

---

## Phase 3: Core Implementation

### Task 28: Update SAEA.P2P csproj References

**Files:**
- Modify: `Src/SAEA.P2P/SAEA.P2P.csproj`

**Step 1: Update csproj with SAEA.Sockets reference**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>8.0</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\SAEA.Common\SAEA.Common.csproj" />
    <ProjectReference Include="..\SAEA.Sockets\SAEA.Sockets.csproj" />
  </ItemGroup>

</Project>
```

**Step 2: Verify compilation**

Run: `dotnet build Src/SAEA.P2P/SAEA.P2P.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Src/SAEA.P2P/SAEA.P2P.csproj
git commit -m "feat(p2p): add SAEA.Sockets reference to csproj"
```

---

### Task 29: Phase 1 Complete - Verification

**Step 1: Build entire solution**

Run: `dotnet build Src/SAEA.Sockets.sln`
Expected: All projects build successfully

**Step 2: Run P2PTest**

Run: `dotnet run --project Src/SAEA.P2PTest/SAEA.P2PTest.csproj`
Select: `1` (Builder Test)
Expected: All tests pass

**Step 3: Commit Phase 1 completion**

```bash
git add -A
git commit -m "feat(p2p): Phase 1 complete - Core framework with Builder, Protocol, ErrorCode"
```

---

## Phase 4: Core Classes (P2PClient/P2PServer)

> Note: Tasks 30-50 will implement P2PClient, P2PServer, HolePuncher, RelayManager, etc.
> These tasks follow the same pattern: Write failing test → Run → Implement → Pass → Commit

(To be continued in subsequent plan updates as Phase 1 is verified working)

---

## Phase 5: README Documentation

### Task 51: SAEA.P2P README.md

**Files:**
- Create: `Src/SAEA.P2P/README.md`

**Step 1: Write README.md**

(Following SAEA.Sockets README structure with:
- Quick start examples
- Builder configuration examples
- Architecture diagram
- Core features table
- Error codes reference
- Dependencies)

**Step 2: Commit**

```bash
git add Src/SAEA.P2P/README.md
git commit -m "docs(p2p): add README.md"
```

---

## Summary

**Phase 1 Tasks (1-29):** Core framework - enums, models, builders, protocol, tests setup
**Phase 2 Tasks (30-50):** Core implementation - P2PClient, P2PServer, HolePuncher, RelayManager
**Phase 3 Tasks (51+):** Documentation, integration tests, scenarios

**Estimated Duration:** Phase 1 ~2 hours, Phase 2 ~4 hours, Phase 3 ~1 hour

**Test Strategy:** Each component has dedicated test file in SAEA.P2PTest/Tests/
**Commit Strategy:** Frequent commits after each task completion
**DRY/YAGNI:** Reuse SAEA.Sockets and SAEA.Common, minimal new code

---

**Plan complete. Proceed to execution or adjust as needed.**