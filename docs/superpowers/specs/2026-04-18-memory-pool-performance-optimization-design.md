# SAEA 内存池性能优化设计文档

## 概述

对 SAEA 项目进行全面 byte[] 性能优化，使用 Memory pooling、Memory<byte>、Span<byte> 等技术减少 GC 压力，提升性能。

**优化方案：** 方案 C - 混合方案
- Socket 层：Span 内部传递（最高性能）
- 其他层级：分级内存池（改动可控）

**API 兼容性：** 保持现有公共 API 不变，仅内部优化

---

## 一、架构概览

### 整体架构

```
┌─────────────────────────────────────────────────────────────────┐
│                    MemoryPoolManager (新增)                      │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐              │
│  │ SmallPool   │  │ MediumPool  │  │ LargePool   │              │
│  │ (< 4KB)     │  │ (4KB-64KB)  │  │ (> 64KB)    │              │
│  │ ArrayPool   │  │ Custom Pool │  │ Custom Pool │              │
│  └─────────────┘  └─────────────┘  └─────────────┘              │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                    Socket 层 - Span 内部传递                     │
│  收发路径: Socket → Span<byte> → 编解码 → Span → 公共API        │
│  (内部全 Span 传递，仅边界处 ToArray)                            │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                    其他层级 - 分级内存池                         │
│  处理路径: 数据 → Rent(size) → 处理 → Return(buffer)            │
└─────────────────────────────────────────────────────────────────┘
```

---

## 二、新增组件

### 2.1 MemoryPoolManager

**位置：** `SAEA.Common/Caching/MemoryPoolManager.cs`

**职责：** 统一内存池管理，按大小分级租用/归还

**API 设计：**

```csharp
namespace SAEA.Common.Caching
{
    public static class MemoryPoolManager
    {
        private static readonly ArrayPool<byte> _smallPool = ArrayPool<byte>.Shared;
        private static readonly ArrayPool<byte> _mediumPool = ArrayPool<byte>.Create(64 * 1024, 100);
        private static readonly ArrayPool<byte> _largePool = ArrayPool<byte>.Create(1024 * 1024, 50);
        
        public static byte[] Rent(int size);
        public static void Return(byte[] buffer, int originalSize = -1);
        public static BufferSizeTier GetTier(int size);
        public static PooledBuffer RentPooled(int size);
        public static MemoryPoolStatistics GetStatistics();
    }
}
```

### 2.2 PooledBuffer

**位置：** `SAEA.Common/Caching/PooledBuffer.cs`

**职责：** 池化缓冲区封装，支持 IDisposable 自动归还

**API 设计：**

```csharp
namespace SAEA.Common.Caching
{
    public sealed class PooledBuffer : IDisposable
    {
        public byte[] Buffer { get; }
        public int Length { get; }
        public int Capacity { get; }
        public Span<byte> AsSpan();
        public Memory<byte> AsMemory();
        public void Dispose();
    }
}
```

### 2.3 BufferSizeTier

**位置：** `SAEA.Common/Caching/BufferSizeTier.cs`

```csharp
namespace SAEA.Common.Caching
{
    public enum BufferSizeTier
    {
        Small = 0,   // < 4KB
        Medium = 1,  // 4KB - 64KB
        Large = 2    // > 64KB
    }
}
```

### 2.4 MemoryPoolStatistics

**位置：** `SAEA.Common/Caching/MemoryPoolStatistics.cs`

**职责：** 统计监控内存池使用情况

---

## 三、Socket 层优化

### 3.1 Socket 层完整组件列表

| 文件 | 类 | 优化类型 |
|------|-----|----------|
| `Core/Tcp/IocpServerSocket.cs` | IocpServerSocket | Span 内部传递 |
| `Core/Tcp/IocpClientSocket.cs` | IocpClientSocket | Span 内部传递 |
| `Core/Tcp/StreamServerSocket.cs` | StreamServerSocket | Span 内部传递 |
| `Core/Tcp/StreamClientSocket.cs` | StreamClientSocket | Span 内部传递 |
| `Core/Udp/UdpServerSocket.cs` | UdpServerSocket | Span 内部传递 |
| `Core/Udp/UdpClientSocket.cs` | UdpClientSocket | Span 内部传递 |
| `Shortcut/TCPServer.cs` | TCPServer | 随底层优化 |
| `Shortcut/TCPClient.cs` | TCPClient | 随底层优化 |
| `Shortcut/UDPServer.cs` | UDPServer | 随底层优化 |
| `Shortcut/UDPClient.cs` | UDPClient | 随底层优化 |
| `Core/BufferManager.cs` | BufferManager | 扩展分级支持 |
| `Core/SocketStream.cs` | SocketStream | Span/Memory |
| `Base/BaseUserToken.cs` | BaseUserToken | 缓冲区池化 |
| `Base/BaseCoder.cs` | BaseCoder | 内存池优化 |
| `Base/BaseSocketProtocal.cs` | BaseSocketProtocal | Span 优化 |
| `Model/SocketOption.cs` | SocketOption | 新增池配置 |
| `SocketFactory.cs` | SocketFactory | 支持池配置 |
| `SocketOptionBuilder.cs` | SocketOptionBuilder | 新增 UseMemoryPool() |
| `UserTokenFactory.cs` | UserTokenFactory | 使用 BufferManager |

### 3.2 Span 内部传递设计

**核心改动：内部事件签名**

```csharp
// 公共 API 保持 byte[] 不变
public event OnReceiveHandler OnReceive;

// 新增内部签名
internal event OnReceiveSpanHandler OnReceiveSpan;
```

**IocpClientSocket 改动示例：**

```csharp
// ProcessReceived 优化
var dataSpan = readArgs.Buffer.AsSpan()
    .Slice(readArgs.Offset, readArgs.BytesTransferred);

OnClientReceiveSpan?.Invoke(dataSpan);

if (OnClientReceive != null)
{
    OnClientReceive.Invoke(dataSpan.ToArray());
}
```

### 3.3 SocketAsyncEventArgs 缓冲区管理

**UserTokenFactory 改动：**

```csharp
// 使用 BufferManager 共享池替代独立分配
_bufferManager.SetBuffer(userToken.ReadArgs);
```

### 3.4 数据流对比

| 阶段 | 优化前 | 优化后 |
|------|--------|--------|
| Socket接收 | new byte[] | Span (无拷贝) |
| Decode | new byte[] | Span (无拷贝) |
| 事件传递 | byte[] | Span (无拷贝) |
| 公共API | byte[] | ToArray 仅此处 |

---

## 四、其他层级优化

### 4.1 RPC 层

**RpcCoder.Decode 改动：**

- 小数据直接分配
- 大数据使用 MemoryPoolManager.Rent

### 4.2 WebSocket 层

**WSCoder.Decode 改动：**

- masks (4字节) 使用 `stackalloc byte[4]`
- payload 根据大小选择直接分配或池化

### 4.3 MQTT 层

**改动组件：**
- MqttChannelAdapter: 接收包体池化
- MqttPacketWriter: 缓冲区池化

### 4.4 Http/MVC 层

**改动组件：**
- HttpBigDataResult: 池化缓冲区 + 直接发送
- DataResult: 池化流读取缓冲区

### 4.5 FileSocket 层

**Client.sendFileBase 改动：**

- 使用 MemoryPoolManager.Rent 替代 new byte[]
- 直接发送有效部分，无需二次拷贝

### 4.6 其他项目

| 项目 | 优化策略 | 关键改动 |
|------|----------|----------|
| Redis | 池化响应缓冲区 | RedisCoder |
| Queue | Span 传递 + 池化 | QueueCoder |
| Message | 池化消息缓冲区 | MessageCoder |
| FTP | 池化传输缓冲区 | Upload/Download |
| DNS | 池化查询缓冲区 | ByteStream |

---

## 五、修改范围预估

| 项目 | 预估改动处 |
|------|-----------|
| SAEA.Sockets | ~25 处 |
| SAEA.Common | ~40 处 (新增 + 修改) |
| SAEA.RPC | ~8 处 |
| SAEA.WebSocket | ~10 处 |
| SAEA.Http/MVC | ~8 处 |
| SAEA.RedisSocket | ~5 处 |
| SAEA.QueueSocket | ~6 处 |
| SAEA.MessageSocket | ~5 处 |
| SAEA.MQTT | ~10 处 |
| SAEA.FileSocket | ~6 处 |
| SAEA.FTP | ~4 处 |
| SAEA.DNS | ~4 处 |
| **总计** | **~127 处** |

---

## 六、测试与验证

### 6.1 新增测试

**位置：** 各测试项目扩展

| 测试文件 | 测试内容 |
|----------|----------|
| PerformanceTests.cs | MemoryAllocationTest, GCTest, ThroughputTest, LatencyTest |
| MemoryPoolTests.cs | RentReturnTest, TierSelectionTest, ConcurrentAccessTest |
| SpanPassingTests.cs | SpanToPublicApiTest, SpanLifecycleTest |

### 6.2 性能目标

| 测试场景 | 优化前 | 优化后目标 |
|----------|--------|------------|
| 10K 连接收发 byte[] 分配 | ~5000/s | ~500/s |
| Gen0 GC 频率 | ~50 次/分钟 | ~10 次/分钟 |
| 吞吐量 | 基准 | +10~20% |
| 延迟 P99 | 基准 | -5~10% |

### 6.3 兼容性验证

- 所有现有测试必须继续通过
- 公共 API 签名不变

---

## 七、风险与缓解

| 风险 | 缓解措施 |
|------|----------|
| Span 生命周期管理复杂 | 明确边界，仅在公共 API 处 ToArray |
| 池归还遗漏 | PooledBuffer IDisposable 自动归还 |
| 并发池访问 | ArrayPool 已内置线程安全 |
| 大改动影响稳定性 | 分阶段实施，充分测试 |

---

## 八、实施顺序

1. **阶段一：** MemoryPoolManager + PooledBuffer 实现
2. **阶段二：** Socket 层 Span 内部传递优化
3. **阶段三：** 高频层级优化 (RPC/WebSocket/MQTT)
4. **阶段四：** 其他层级优化
5. **阶段五：** 性能测试与验证

---

## 九、预期收益

- **减少 GC 压力：** 预计减少 60-80% byte[] 相关 GC
- **降低内存占用：** 连接密集场景降低 30-50%
- **提升吞吐量：** 高并发场景提升 10-20%