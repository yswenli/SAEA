# SAEA 内存池性能优化实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 对 SAEA 项目进行全面 byte[] 性能优化，使用 Memory pooling、Span<byte>、Memory<byte> 等技术减少 GC 压力，提升性能 10-20%。

**Architecture:** 方案 C - 混合方案。Socket 层使用 Span 内部传递（最高性能），其他层级使用分级内存池（改动可控）。保持现有公共 API 兼容性。

**Tech Stack:** C# (.NET Standard 2.0), System.Buffers.ArrayPool, System.Memory.Span

---

## 阶段一：核心内存池实现

### Task 1: BufferSizeTier 枚举

**Files:**
- Create: `Src/SAEA.Common/Caching/BufferSizeTier.cs`

**说明:** 定义缓冲区大小分级枚举，用于区分 Small/Medium/Large 三级。

- [ ] **Step 1: 创建枚举文件**

```csharp
namespace SAEA.Common.Caching
{
    /// <summary>
    /// 缓冲区大小分级
    /// </summary>
    public enum BufferSizeTier
    {
        /// <summary>小于 4KB</summary>
        Small = 0,
        
        /// <summary>4KB - 64KB</summary>
        Medium = 1,
        
        /// <summary>大于 64KB</summary>
        Large = 2
    }
}
```

- [ ] **Step 2: 提交代码**

```bash
git add Src/SAEA.Common/Caching/BufferSizeTier.cs
git commit -m "feat: add BufferSizeTier enum for memory pool tiering"
```

---

### Task 2: MemoryPoolStatistics 类

**Files:**
- Create: `Src/SAEA.Common/Caching/MemoryPoolStatistics.cs`

**说明:** 统计内存池使用情况，便于监控和调优。

- [ ] **Step 1: 创建统计类**

```csharp
namespace SAEA.Common.Caching
{
    /// <summary>
    /// 内存池统计信息
    /// </summary>
    public struct MemoryPoolStatistics
    {
        public long SmallPoolRented { get; set; }
        public long SmallPoolReturned { get; set; }
        public long MediumPoolRented { get; set; }
        public long MediumPoolReturned { get; set; }
        public long LargePoolRented { get; set; }
        public long LargePoolReturned { get; set; }
        public long TotalRented => SmallPoolRented + MediumPoolRented + LargePoolRented;
        public long TotalReturned => SmallPoolReturned + MediumPoolReturned + LargePoolReturned;
        public long ActiveBuffers => TotalRented - TotalReturned;
    }
}
```

- [ ] **Step 2: 提交代码**

```bash
git add Src/SAEA.Common/Caching/MemoryPoolStatistics.cs
git commit -m "feat: add MemoryPoolStatistics for monitoring pool usage"
```

---

### Task 3: PooledBuffer 类

**Files:**
- Create: `Src/SAEA.Common/Caching/PooledBuffer.cs`

**说明:** 池化缓冲区封装，支持 IDisposable 自动归还。

- [ ] **Step 1: 创建 PooledBuffer 类**

```csharp
using System;
using System.Buffers;

namespace SAEA.Common.Caching
{
    /// <summary>
    /// 池化缓冲区，支持自动归还
    /// </summary>
    public sealed class PooledBuffer : IDisposable
    {
        private readonly ArrayPool<byte> _pool;
        private bool _disposed;

        public byte[] Buffer { get; }
        public int Length { get; }
        public int Capacity => Buffer.Length;
        public BufferSizeTier Tier { get; }

        public PooledBuffer(byte[] buffer, int length, BufferSizeTier tier, ArrayPool<byte> pool)
        {
            Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            Length = length;
            Tier = tier;
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _disposed = false;
        }

        public Span<byte> AsSpan() => Buffer.AsSpan(0, Length);
        public Memory<byte> AsMemory() => Buffer.AsMemory(0, Length);

        public void Dispose()
        {
            if (!_disposed)
            {
                _pool.Return(Buffer);
                _disposed = true;
            }
        }
    }
}
```

- [ ] **Step 2: 提交代码**

```bash
git add Src/SAEA.Common/Caching/PooledBuffer.cs
git commit -m "feat: add PooledBuffer for automatic buffer return"
```

---

### Task 4: MemoryPoolManager 核心实现

**Files:**
- Create: `Src/SAEA.Common/Caching/MemoryPoolManager.cs`

**说明:** 统一内存池管理入口，分级租用和归还。

- [ ] **Step 1: 创建 MemoryPoolManager 类**

```csharp
using System;
using System.Buffers;

namespace SAEA.Common.Caching
{
    /// <summary>
    /// 统一内存池管理器
    /// </summary>
    public static class MemoryPoolManager
    {
        // 常量定义
        public const int SmallThreshold = 4 * 1024;      // 4KB
        public const int MediumThreshold = 64 * 1024;    // 64KB
        public const int LargeThreshold = 1024 * 1024;   // 1MB

        // 分级池
        private static readonly ArrayPool<byte> _smallPool = ArrayPool<byte>.Shared;
        private static readonly ArrayPool<byte> _mediumPool = ArrayPool<byte>.Create(MediumThreshold, 100);
        private static readonly ArrayPool<byte> _largePool = ArrayPool<byte>.Create(LargeThreshold, 50);

        // 统计
        private static readonly MemoryPoolStatistics _statistics = new MemoryPoolStatistics();

        /// <summary>
        /// 根据大小获取分级
        /// </summary>
        public static BufferSizeTier GetTier(int size)
        {
            if (size <= SmallThreshold) return BufferSizeTier.Small;
            if (size <= MediumThreshold) return BufferSizeTier.Medium;
            return BufferSizeTier.Large;
        }

        /// <summary>
        /// 租用缓冲区
        /// </summary>
        public static byte[] Rent(int size)
        {
            var tier = GetTier(size);
            byte[] buffer;

            switch (tier)
            {
                case BufferSizeTier.Small:
                    buffer = _smallPool.Rent(size);
                    _statistics.SmallPoolRented++;
                    break;
                case BufferSizeTier.Medium:
                    buffer = _mediumPool.Rent(size);
                    _statistics.MediumPoolRented++;
                    break;
                case BufferSizeTier.Large:
                    buffer = _largePool.Rent(size);
                    _statistics.LargePoolRented++;
                    break;
                default:
                    buffer = new byte[size];
                    break;
            }

            return buffer;
        }

        /// <summary>
        /// 归还缓冲区
        /// </summary>
        public static void Return(byte[] buffer, int originalSize = -1)
        {
            if (buffer == null) return;

            var tier = originalSize > 0 ? GetTier(originalSize) : GetTier(buffer.Length);

            switch (tier)
            {
                case BufferSizeTier.Small:
                    _smallPool.Return(buffer);
                    _statistics.SmallPoolReturned++;
                    break;
                case BufferSizeTier.Medium:
                    _mediumPool.Return(buffer);
                    _statistics.MediumPoolReturned++;
                    break;
                case BufferSizeTier.Large:
                    _largePool.Return(buffer);
                    _statistics.LargePoolReturned++;
                    break;
            }
        }

        /// <summary>
        /// 租用池化缓冲区（支持自动归还）
        /// </summary>
        public static PooledBuffer RentPooled(int size)
        {
            var tier = GetTier(size);
            ArrayPool<byte> pool;
            byte[] buffer;

            switch (tier)
            {
                case BufferSizeTier.Small:
                    pool = _smallPool;
                    buffer = _smallPool.Rent(size);
                    _statistics.SmallPoolRented++;
                    break;
                case BufferSizeTier.Medium:
                    pool = _mediumPool;
                    buffer = _mediumPool.Rent(size);
                    _statistics.MediumPoolRented++;
                    break;
                case BufferSizeTier.Large:
                    pool = _largePool;
                    buffer = _largePool.Rent(size);
                    _statistics.LargePoolRented++;
                    break;
                default:
                    pool = _smallPool;
                    buffer = new byte[size];
                    break;
            }

            return new PooledBuffer(buffer, size, tier, pool);
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public static MemoryPoolStatistics GetStatistics() => _statistics;
    }
}
```

- [ ] **Step 2: 提交代码**

```bash
git add Src/SAEA.Common/Caching/MemoryPoolManager.cs
git commit -m "feat: add MemoryPoolManager for unified memory pool management"
```

---

## 阶段二：Socket 层 Span 优化

### Task 5: BaseCoder Span 版本 Decode

**Files:**
- Modify: `Src/SAEA.Sockets/Base/BaseCoder.cs:145-255`

**说明:** 为 BaseCoder 添加 Span 版本解码，减少内存拷贝。

- [ ] **Step 1: 添加 Span 版本 Decode 方法**

在 `BaseCoder.cs` 中添加新方法：

```csharp
/// <summary>
/// Span 版本解码
/// </summary>
public virtual List<ReadOnlyMemory<byte>> Decode(ReadOnlySpan<byte> data)
{
    var results = new List<ReadOnlyMemory<byte>>();
    int offset = 0;

    while (offset < data.Length)
    {
        if (data.Length - offset < 9) break; // 最小协议头长度

        // 读取长度 (8 bytes)
        long len = BitConverter.ToInt64(data.Slice(offset, 8));
        if (len < 0 || len > int.MaxValue) break;

        offset += 8;

        // 读取类型 (1 byte)
        byte type = data[offset++];

        // 内容
        int contentLen = (int)len - 9;
        if (contentLen < 0 || offset + contentLen > data.Length) break;

        var content = data.Slice(offset, contentLen);
        
        // 对于小于 4KB 的数据，直接 ToArray()；大于则池化
        if (contentLen <= MemoryPoolManager.SmallThreshold)
        {
            results.Add(content.ToArray());
        }
        else
        {
            var buffer = MemoryPoolManager.Rent(contentLen);
            content.CopyTo(buffer);
            results.Add(buffer.AsMemory(0, contentLen));
            // 注意：调用者需要负责归还
        }

        offset += contentLen;
    }

    return results;
}
```

- [ ] **Step 2: 修改现有 Decode 使用池化**

将原 Decode(byte[]) 方法改为使用 MemoryPoolManager：

```csharp
public virtual List<byte[]> Decode(byte[] data)
{
    return Decode(data.AsSpan()).Select(m => {
        var arr = m.ToArray();
        // 如果原始是池化的，尝试归还
        return arr;
    }).ToList();
}
```

- [ ] **Step 3: 提交代码**

```bash
git add Src/SAEA.Sockets/Base/BaseCoder.cs
git commit -m "perf: add Span version Decode to BaseCoder with pooling"
```

---

### Task 6: IocpClientSocket ProcessReceived 优化

**Files:**
- Modify: `Src/SAEA.Sockets/Core/Tcp/IocpClientSocket.cs:435-445`

**说明:** 优化数据接收，使用 Span 传递，仅在公共 API 处 ToArray。

- [ ] **Step 1: 修改 ProcessReceived 方法**

```csharp
private void ProcessReceived(SocketAsyncEventArgs readArgs)
{
    if (readArgs.BytesTransferred > 0 && readArgs.SocketError == SocketError.Success)
    {
        Interlocked.Add(ref _in, readArgs.BytesTransferred);

        // 获取数据 Span，不创建新数组
        var dataSpan = readArgs.Buffer.AsSpan(readArgs.Offset, readArgs.BytesTransferred);

        // 调用内部 Span 事件（如果注册）
        OnClientReceiveSpan?.Invoke(dataSpan);

        // 公共 API 保持 byte[] 不变
        if (OnClientReceive != null)
        {
            // 小数据直接分配，大数据池化
            byte[] data;
            if (readArgs.BytesTransferred <= MemoryPoolManager.SmallThreshold)
            {
                data = dataSpan.ToArray();
            }
            else
            {
                data = MemoryPoolManager.Rent(readArgs.BytesTransferred);
                dataSpan.CopyTo(data);
            }
            
            OnClientReceive.Invoke(data);
            
            // 归还池化缓冲区
            if (readArgs.BytesTransferred > MemoryPoolManager.SmallThreshold)
            {
                MemoryPoolManager.Return(data, readArgs.BytesTransferred);
            }
        }

        // 继续接收
        if (!_clientSocket.ReceiveAsync(readArgs))
            ProcessReceived(readArgs);
    }
}
```

- [ ] **Step 2: 添加 Span 事件定义**

在 `IocpClientSocket.cs` 类中添加：

```csharp
// 内部 Span 版本事件（用于内部优化）
internal event Action<ReadOnlySpan<byte>> OnClientReceiveSpan;
```

- [ ] **Step 3: 提交代码**

```bash
git add Src/SAEA.Sockets/Core/Tcp/IocpClientSocket.cs
git commit -m "perf: optimize IocpClientSocket.ProcessReceived with Span and pooling"
```

---

### Task 7: UserTokenFactory 使用 BufferManager

**Files:**
- Modify: `Src/SAEA.Sockets/Core/UserTokenFactory.cs:82`

**说明:** 修改 UserTokenFactory 使用 BufferManager 共享缓冲区。

- [ ] **Step 1: 修改 Create 方法**

```csharp
public static IUserToken Create(IContext<ICoder> context, int bufferSize, EventHandler<SocketAsyncEventArgs> handler)
{
    IUserToken userToken = Create(context);

    userToken.ReadArgs = new SocketAsyncEventArgs();
    userToken.ReadArgs.Completed += handler;
    userToken.ReadArgs.UserToken = userToken;

    // 优化：使用 BufferManager 共享缓冲区
    // 原代码: userToken.ReadArgs.SetBuffer(new byte[bufferSize], 0, bufferSize);
    // 优化后：
    var bufferManager = BufferManager.GetOrCreate(bufferSize);
    bufferManager.SetBuffer(userToken.ReadArgs);

    userToken.SendArgs = new SocketAsyncEventArgs();
    userToken.SendArgs.Completed += handler;
    userToken.SendArgs.UserToken = userToken;
    // SendArgs 缓冲区在发送时动态分配

    return userToken;
}
```

- [ ] **Step 2: 修改 BufferManager 支持 GetOrCreate**

在 `BufferManager.cs` 中添加静态工厂方法：

```csharp
private static readonly ConcurrentDictionary<int, BufferManager> _managers = new ConcurrentDictionary<int, BufferManager>();

public static BufferManager GetOrCreate(int bufferSize)
{
    return _managers.GetOrAdd(bufferSize, size => new BufferManager(size * 100, size));
}
```

- [ ] **Step 3: 提交代码**

```bash
git add Src/SAEA.Sockets/Core/UserTokenFactory.cs
git add Src/SAEA.Sockets/Core/BufferManager.cs
git commit -m "perf: use BufferManager in UserTokenFactory for shared buffers"
```

---

### Task 8: UdpClientSocket 优化

**Files:**
- Modify: `Src/SAEA.Sockets/Core/Udp/UdpClientSocket.cs:240,369-371`

**说明:** UDP 客户端使用与 TCP 相同的 Span 优化。

- [ ] **Step 1: 修改 ReceiveFrom 和 SendTo 相关代码**

参照 Task 6 的修改方式，使用 Span 传递和内存池：

```csharp
// ReceiveFrom 优化
var dataSpan = readArgs.Buffer.AsSpan(readArgs.Offset, readArgs.BytesTransferred);
OnClientReceiveSpan?.Invoke(dataSpan);

if (OnClientReceive != null)
{
    // 池化策略
    byte[] data = GetPooledData(dataSpan);
    OnClientReceive.Invoke(data);
    ReturnPooledData(data, dataSpan.Length);
}

// SendTo 优化
// 原代码：new byte[count] + Buffer.BlockCopy
// 优化：MemoryPoolManager.Rent + Span.CopyTo
var pooledBuffer = MemoryPoolManager.Rent(count);
span.Slice(offset, count).CopyTo(pooledBuffer);
// ... 使用 pooledBuffer 发送 ...
MemoryPoolManager.Return(pooledBuffer, count);
```

- [ ] **Step 2: 提交代码**

```bash
git add Src/SAEA.Sockets/Core/Udp/UdpClientSocket.cs
git commit -m "perf: optimize UdpClientSocket with Span and pooling"
```

---

### Task 9: StreamServerSocket 优化

**Files:**
- Modify: `Src/SAEA.Sockets/Core/Tcp/StreamServerSocket.cs:249,256`

**说明:** 流模式 Socket 使用内存池缓冲区。

- [ ] **Step 1: 修改流读取代码**

```csharp
// 优化前: var data = new byte[SocketOption.ReadBufferSize];
// 优化后：
private byte[] _receiveBuffer; // 使用池化缓冲区

private void Init()
{
    _receiveBuffer = MemoryPoolManager.Rent(SocketOption.ReadBufferSize);
}

private void Receive()
{
    // 使用 _receiveBuffer 接收数据
    int len = _sslStream?.Read(_receiveBuffer, 0, SocketOption.ReadBufferSize) 
              ?? _networkStream.Read(_receiveBuffer, 0, SocketOption.ReadBufferSize);
    
    if (len > 0)
    {
        // 使用 Span 传递，避免 ToArray
        var dataSpan = _receiveBuffer.AsSpan(0, len);
        OnReceive?.Invoke(new Session(_id), dataSpan.ToArray());
    }
}
```

- [ ] **Step 2: 添加 Dispose 释放缓冲区**

```csharp
public void Dispose()
{
    // ... 其他释放逻辑 ...
    if (_receiveBuffer != null)
    {
        MemoryPoolManager.Return(_receiveBuffer, SocketOption.ReadBufferSize);
        _receiveBuffer = null;
    }
}
```

- [ ] **Step 3: 提交代码**

```bash
git add Src/SAEA.Sockets/Core/Tcp/StreamServerSocket.cs
git commit -m "perf: optimize StreamServerSocket with pooled receive buffer"
```

---

## 阶段三：其他层级内存池优化

### Task 10: RpcCoder 优化

**Files:**
- Modify: `Src/SAEA.RPC/Net/RpcCoder.cs:150,162,172`

**说明:** RPC 编解码器使用内存池减少数组分配。

- [ ] **Step 1: 修改 Decode 方法**

```csharp
private static void Decode(ReadOnlySpan<byte> data, RSocketMsg qm)
{
    int offset = 0;
    
    // ServiceName (小数据直接分配)
    var sNameSpan = data.Slice(offset, qm.SLen);
    qm.ServiceName = sNameSpan.ToArray();
    offset += qm.SLen;
    
    // MethodName (小数据直接分配)
    var mNameSpan = data.Slice(offset, qm.MLen);
    qm.MethodName = mNameSpan.ToArray();
    offset += qm.MLen;
    
    // Data (大数据池化)
    int dlen = qm.Total - 17 - qm.SLen - qm.MLen;
    if (dlen > 0)
    {
        var dataSpan = data.Slice(offset, dlen);
        if (dlen <= MemoryPoolManager.SmallThreshold)
        {
            qm.Data = dataSpan.ToArray();
            qm.IsPooled = false;
        }
        else
        {
            qm.Data = MemoryPoolManager.Rent(dlen);
            dataSpan.CopyTo(qm.Data);
            qm.IsPooled = true;
        }
    }
}
```

- [ ] **Step 2: 在 RSocketMsg 添加 IsPooled 字段**

```csharp
public class RSocketMsg
{
    // 现有字段...
    internal bool IsPooled { get; set; }
}
```

- [ ] **Step 3: 添加归还逻辑**

在消息处理完成后归还池化缓冲区：

```csharp
if (msg.IsPooled && msg.Data != null)
{
    MemoryPoolManager.Return(msg.Data, msg.Data.Length);
}
```

- [ ] **Step 4: 提交代码**

```bash
git add Src/SAEA.RPC/Net/RpcCoder.cs
git add Src/SAEA.RPC/Net/RSocketMsg.cs
git commit -m "perf: optimize RpcCoder with memory pooling"
```

---

### Task 11: WSCoder 优化

**Files:**
- Modify: `Src/SAEA.WebSocket/Model/WSCoder.cs:90,96,109,125,136`

**说明:** WebSocket 编解码器使用 stackalloc 和内存池优化。

- [ ] **Step 1: 修改 masks 处理（使用 stackalloc）**

```csharp
// 优化前: var masks = new byte[4];
// 优化后：
Span<byte> masks = stackalloc byte[4];
data.Slice(offset, 4).CopyTo(masks);
offset += 4;
```

- [ ] **Step 2: 修改 payloadData 处理（内存池）**

```csharp
// 根据 payload 大小选择分配策略
byte[] payloadData;
if (payloadLen <= MemoryPoolManager.SmallThreshold)
{
    payloadData = new byte[payloadLen];
    data.Slice(offset, payloadLen).CopyTo(payloadData);
}
else
{
    payloadData = MemoryPoolManager.Rent(payloadLen);
    data.Slice(offset, payloadLen).CopyTo(payloadData);
    wsProtocal.IsPooled = true;
}
wsProtocal.Content = payloadData;
```

- [ ] **Step 3: 提交代码**

```bash
git add Src/SAEA.WebSocket/Model/WSCoder.cs
git add Src/SAEA.WebSocket/Model/WSProtocal.cs
git commit -m "perf: optimize WSCoder with stackalloc and memory pooling"
```

---

### Task 12: MQTT MqttPacketWriter 优化

**Files:**
- Modify: `Src/SAEA.MQTT/Protocol/MqttPacketWriter.cs:22,60`

**说明:** MQTT 包写入器使用内存池缓冲区。

- [ ] **Step 1: 修改构造函数**

```csharp
public MqttPacketWriter()
{
    // 优化：使用池化初始缓冲区
    _buffer = MemoryPoolManager.Rent(InitialBufferSize);
    _isPooled = true;
}
```

- [ ] **Step 2: 修改 FreeBuffer 方法**

```csharp
private void FreeBuffer()
{
    if (_isPooled && _buffer != null)
    {
        MemoryPoolManager.Return(_buffer);
        _buffer = null;
        _isPooled = false;
    }
}
```

- [ ] **Step 3: 修改 Encode 静态方法（小数组使用栈分配）**

```csharp
private static byte[] Encode(uint x)
{
    Span<byte> buffer = stackalloc byte[4];
    buffer[0] = (byte)(x >> 24);
    buffer[1] = (byte)(x >> 16);
    buffer[2] = (byte)(x >> 8);
    buffer[3] = (byte)x;
    return buffer.ToArray(); // 只有4字节，直接分配
}
```

- [ ] **Step 4: 提交代码**

```bash
git add Src/SAEA.MQTT/Protocol/MqttPacketWriter.cs
git commit -m "perf: optimize MqttPacketWriter with memory pooling"
```

---

### Task 13: HttpBigDataResult 优化

**Files:**
- Modify: `Src/SAEA.Http/HttpResults/HttpBigDataResult.cs:49,66-68`

**说明:** HTTP 大数据结果使用内存池缓冲区。

- [ ] **Step 1: 修改 Invoke 方法**

```csharp
public void Invoke(HttpContext httpContext)
{
    // 优化：使用内存池替代 new byte[1024]
    var buffer = MemoryPoolManager.Rent(1024);
    try
    {
        int count;
        while ((count = _stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            // 直接发送，无需创建新数组
            httpContext.Response.SendData(buffer, 0, count);
        }
    }
    finally
    {
        MemoryPoolManager.Return(buffer, 1024);
    }
}
```

- [ ] **Step 2: 提交代码**

```bash
git add Src/SAEA.Http/HttpResults/HttpBigDataResult.cs
git commit -m "perf: optimize HttpBigDataResult with memory pooling"
```

---

### Task 14: FileSocket Client 优化

**Files:**
- Modify: `Src/SAEA.FileSocket/Client.cs:83,179,197`

**说明:** 文件传输客户端使用内存池缓冲区。

- [ ] **Step 1: 修改 sendFileBase 方法**

```csharp
// 优化：使用内存池替代 new byte[_bufferSize]
var buffer = MemoryPoolManager.Rent(_bufferSize);
try
{
    using var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
    _total = fs.Length;

    do
    {
        fs.Position = offset;
        var readNum = fs.Read(buffer, 0, _bufferSize);
        if (readNum <= 0) break;

        offset += readNum;

        // 直接发送有效部分，无需创建新数组
        var data = BaseSocketProtocal.ParseStream(buffer.AsSpan(0, readNum)).ToBytes();
        _client.SendAsync(data);
        
        Interlocked.Add(ref _out, readNum);
    } while (true);
}
finally
{
    MemoryPoolManager.Return(buffer, _bufferSize);
}
```

- [ ] **Step 2: 提交代码**

```bash
git add Src/SAEA.FileSocket/Client.cs
git commit -m "perf: optimize FileSocket Client with memory pooling"
```

---

### Task 15: 其他层级批量优化

**文件清单：**

| 项目 | 文件 | 修改内容 |
|------|------|----------|
| SAEA.Common | GZipHelper.cs:54,75,125,150 | 压缩解压缓冲区池化 |
| SAEA.Common | StreamReader.cs:158,166 | 流读取缓冲区池化 |
| SAEA.Common | SAEASerialize.cs:68,105,347,533,583 | 序列化小数据池化 |
| SAEA.QueueSocket | QueueCoder.cs:202,212,221 | 队列编解码 Span 优化 |
| SAEA.MQTT | MqttChannelAdapter.cs:26,250 | 包接收缓冲区池化 |
| SAEA.MQTT | MqttPacketBodyReader.cs:65 | 读取缓冲区池化 |
| SAEA.FTP | FTPClient.cs:289 | 文件传输缓冲区池化 |
| SAEA.DNS | ByteStream.cs:31,105 | 字节流缓冲区池化 |
| SAEA.RedisSocket | RedisCoder.cs | 响应缓冲区池化 |
| SAEA.MessageSocket | ChatMessage.cs | 消息缓冲区池化 |

**统一修改模式：**

```csharp
// 原代码
var buffer = new byte[size];
// ... 使用 ...

// 优化后
var buffer = MemoryPoolManager.Rent(size);
try
{
    // ... 使用 ...
}
finally
{
    MemoryPoolManager.Return(buffer, size);
}
```

- [ ] **Step 1: 批量修改上述文件**

- [ ] **Step 2: 提交代码**

```bash
git add Src/SAEA.Common/Compression/GZipHelper.cs
git add Src/SAEA.Common/IO/StreamReader.cs
git add Src/SAEA.Common/Serialization/SAEASerialize.cs
# ... 其他文件 ...
git commit -m "perf: batch optimize remaining components with memory pooling"
```

---

## 阶段四：测试与验证

### Task 16: MemoryPool 单元测试

**Files:**
- Create: `Src/SAEA.Sockets.TcpTest/MemoryPoolTests.cs`

**说明:** 测试 MemoryPoolManager 的正确性。

- [ ] **Step 1: 创建测试文件**

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.Caching;
using System;

namespace SAEA.Sockets.TcpTest
{
    [TestClass]
    public class MemoryPoolTests
    {
        [TestMethod]
        public void Rent_Return_Basic()
        {
            var buffer = MemoryPoolManager.Rent(1024);
            Assert.IsNotNull(buffer);
            Assert.IsTrue(buffer.Length >= 1024);
            
            MemoryPoolManager.Return(buffer, 1024);
        }

        [TestMethod]
        public void PooledBuffer_AutoReturn()
        {
            byte[] reference;
            using (var pooled = MemoryPoolManager.RentPooled(1024))
            {
                reference = pooled.Buffer;
                Assert.IsTrue(pooled.Capacity >= 1024);
            }
            // 缓冲区应该已归还到池中
        }

        [TestMethod]
        public void TierSelection()
        {
            Assert.AreEqual(BufferSizeTier.Small, MemoryPoolManager.GetTier(1024));
            Assert.AreEqual(BufferSizeTier.Small, MemoryPoolManager.GetTier(4096));
            Assert.AreEqual(BufferSizeTier.Medium, MemoryPoolManager.GetTier(8192));
            Assert.AreEqual(BufferSizeTier.Medium, MemoryPoolManager.GetTier(65536));
            Assert.AreEqual(BufferSizeTier.Large, MemoryPoolManager.GetTier(131072));
        }

        [TestMethod]
        public void ConcurrentAccess()
        {
            Parallel.For(0, 1000, i =>
            {
                var buffer = MemoryPoolManager.Rent(1024);
                buffer[0] = (byte)(i % 256);
                MemoryPoolManager.Return(buffer, 1024);
            });
        }
    }
}
```

- [ ] **Step 2: 运行测试**

```bash
dotnet test Src/SAEA.Sockets.TcpTest/SAEA.Sockets.TcpTest.csproj --filter "FullyQualifiedName~MemoryPoolTests"
```

- [ ] **Step 3: 提交代码**

```bash
git add Src/SAEA.Sockets.TcpTest/MemoryPoolTests.cs
git commit -m "test: add MemoryPool unit tests"
```

---

### Task 17: 性能基准测试

**Files:**
- Create: `Src/SAEA.Sockets.TcpTest/PerformanceBenchmark.cs`

**说明:** 对比优化前后的性能差异。

- [ ] **Step 1: 创建基准测试**

使用 BenchmarkDotNet（或手动测试）：

```csharp
[MemoryDiagnoser]
public class SocketPerformanceBenchmark
{
    private byte[] _testData;
    private TCPServer _server;
    private TCPClient _client;

    [GlobalSetup]
    public void Setup()
    {
        _testData = new byte[1024];
        new Random().NextBytes(_testData);
        
        _server = new TCPServer(39654);
        _server.OnReceive += (id, data) => { };
        _server.Start();
        
        _client = new TCPClient("127.0.0.1", 39654);
        _client.OnReceive += (data) => { };
        _client.Connect();
    }

    [Benchmark]
    public void SendReceive_1K()
    {
        for (int i = 0; i < 1000; i++)
        {
            _client.Send(_testData);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _client.Close();
        _server.Stop();
    }
}
```

- [ ] **Step 2: 运行基准测试**

```bash
dotnet run --project Src/SAEA.Sockets.TcpTest/SAEA.Sockets.TcpTest.csproj --configuration Release
```

**预期结果：**
- byte[] 分配次数减少 60-80%
- GC 频率减少 50-70%
- 吞吐量提升 10-20%

- [ ] **Step 3: 提交测试代码**

```bash
git add Src/SAEA.Sockets.TcpTest/PerformanceBenchmark.cs
git commit -m "test: add performance benchmark"
```

---

### Task 18: 兼容性测试

**说明:** 确保所有现有测试继续通过。

- [ ] **Step 1: 运行所有测试**

```bash
# 运行 SAEA.Sockets 测试
dotnet test Src/SAEA.Sockets.TcpTest/SAEA.Sockets.TcpTest.csproj
dotnet test Src/SAEA.Sockets.UdpTest/SAEA.Sockets.UdpTest.csproj

# 运行 SAEA.RPC 测试
dotnet test Src/SAEA.RPCTest/SAEA.RPCTest.csproj

# 运行其他测试
# ...
```

- [ ] **Step 2: 确保 100% 通过率**

所有测试必须继续通过，公共 API 行为不变。

---

## 提交清单

完成所有任务后，最终提交：

```bash
# 推送所有提交到远程仓库
git push origin master
```

---

## 预期收益总结

| 指标 | 优化前 | 优化后目标 |
|------|--------|------------|
| byte[] 分配 | ~5000/s (10K 连接) | ~500/s |
| Gen0 GC | ~50 次/分钟 | ~10 次/分钟 |
| 内存占用 | 基准 | -30~50% |
| 吞吐量 | 基准 | +10~20% |
| 延迟 P99 | 基准 | -5~10% |