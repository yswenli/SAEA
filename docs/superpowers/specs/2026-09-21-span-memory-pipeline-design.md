# SAEA Span/Memory/Pipe 全链路改造设计文档

**日期：** 2026-09-21
**范围：** 全解决方案公共面现代化（`SAEA.Common` 原语 + `SAEA.Sockets` 协议/编解码/Socket 层 + `SAEA.P2P` 优化 + 7 个外部项目机械适配）
**方案：** 方案 1 —— 原语先行 + 全链路 Span/Memory 化（**破坏性重构**，仅保留 netstandard2.0）

---

## 概述

SAEA 已在上一轮完成「流式零拷贝解码器」内核（`FrameDecoder` / `DecodeStream` / `Decode(ReadOnlySequence)`，见 `2026-09-20-readonly-sequence-zero-copy-decoder-design.md`），但**公共面仍是 `byte[]`**，`DecodeStream` 事实上无调用方，零拷贝能力被挡在接口之外：

- `ICoder` 仅声明 `byte[] Encode(ISocketProtocal)` + `Decode(byte[])`（`Src/SAEA.Sockets/Interface/ICoder.cs:40-62`）。
- `ISocketProtocal.Content` 为 `byte[]`，`BaseSocketProtocal.ToBytes()` 用 `List<byte>` 逐字节累加后 `ToArray()`（`Src/SAEA.Sockets/Base/BaseSocketProtocal.cs`）。
- `BaseCoder` 的 `List<ISocketProtocal>` 路径仍 `frame.Content.ToArray()` 物化。
- IOCP 收发仍经 `SocketAsyncEventArgs.Buffer` + `.ToArray()`，兼容分支保留 `_isBaseClientType`/`_isBaseServerType`。
- 发送公共面全是 `byte[]`；`UdpServerSocket` 与两个 Stream socket 无 Span 事件；`MemoryBuffer` 完全未使用；`Core/RioExtention.cs` 是早年注释掉的 Pipe 适配尝试（死代码）。
- P2P/relay 有约 12 处 `Array.IndexOf` + `Buffer.BlockCopy` + `new byte[]`。

本设计把 **Span/Memory/`ReadOnlySequence`/`IBufferWriter`/`PipeReader`** 贯穿到公共接口与全部实现，并删除 `byte[]` 公共面。

**兼容性：** 破坏性变更；调用方需按迁移表改造（见 §九）。
**非目标：** 不改线格式、不把 socket 接收语义改为「按帧投递」、不发布 nupkg、不引入 `net8.0` 多目标。

---

## 一、关键事实核对（实现前已验证）

| 事实 | 结论 / 出处 | 对设计的影响 |
|------|------------|-------------|
| 框架约束 | 仅保留 `netstandard2.0` / C# 8，引用 `System.IO.Pipelines 10.0.6`（含 `System.Memory`） | `ReadOnlySequence<byte>`、`IBufferWriter<byte>`、`PipeReader` 可用；`SequenceReader<byte>` 不可用（上一轮已确认） |
| ns2.0 限制 | `Encoding.GetBytes(ReadOnlySpan<char>, Span<byte>)` 不可用；`Socket.Send(Span)`/`SendTo(Span)` 不可用；`Stream.WriteAsync(ReadOnlyMemory)` 不可用；`ArrayBufferWriter<T>` 存在性不确定 | 需 `ArrayPool<char>` 中转、发送侧租池拷贝、自建 `PooledBufferWriter` |
| `ICoder` 实现 | **8 个 / 7 个项目**：`BaseCoder`(SAEA.Sockets)、`FTPCoder`(SAEA.FTP)、`WSCoder`(SAEA.WebSocket)、`QueueCoder`(SAEA.QueueSocket)、`JUnpacker`(SAEA.Sockets.TcpTest)、`RpcCoder`(SAEA.RPC)、`HttpCoder`(SAEA.Http)、`RedisCoder`(SAEA.RedisSocket) | 接口一改，全部须同步 |
| `ISocketProtocal` 实现 | **2 个**：`BaseSocketProtocal`、`WSProtocal`(SAEA.WebSocket) | `WSProtocal` 需同步 |
| `IClientSocket`/`IServerSocket` 实现 | 各 **3 个**：`IocpClient/ServerSocket`、`UdpClient/ServerSocket`、`StreamClient/ServerSocket` | 接口一改，全部须同步 |
| `BaseCoder` 派生 | 仅 `P2PCoder : BaseCoder`（`Src/SAEA.P2P/Protocol/P2PCoder.cs`），只调用 `Decode`，无重写 | `DecodeP2P` 需适配新返回类型 |
| 线格式 | 8 字节小端长度 + 1 字节 Type + body；`P_LEN=8`、`P_Type=1`、`P_Head=9`，`SmallDataThreshold=4KB` | 解析必须逐字保持 |
| 心跳语义 | `bodyLen==0 && type==Heart` → 消费 9 字节、回调 `onHeart`、**不产出** | 必须逐字保持 |
| 空 body 语义 | 普通帧 `bodyLen==0` → `Content` **非 null**（长度 0） | 必须逐字保持 |
| BigData 语义 | `type==BigData` → `onFile(content)`，**不产出帧** | 必须逐字保持 |
| 非法长度 | `bodyLen < 0 \|\| > MaxFrameLength` → 抛 `KernelException` | 必须保持 |
| 既有池化原语 | `SAEA.Common/Caching/`：`MemoryPoolManager`（三级 `ArrayPool<byte>`，Small=4KB/Medium=64KB）、`PooledBuffer`、`PooledBytes` | 新 `PooledBufferWriter` 落在此处并复用 `MemoryPoolManager` |
| 死代码 | `Src/SAEA.Sockets/Core/RioExtention.cs`（注释掉的 Pipe 适配） | 本阶段删除 |
| 测试工程 | `SAEA.P2PTest` 直接引用 `SAEA.Sockets`；`--all` 当前 **255 项全绿**；`LegacyDecoder` 保留旧字节算法作 oracle | 可做 parity 与端到端基准 |

---

## 二、目标 / 范围 / 硬不变量

### 2.1 目标

1. 消除公共面上的 `byte[]`：编解码、协议对象、socket 收发、Shortcut 全部改为 Span/Memory/`ReadOnlySequence`/`IBufferWriter`。
2. 让上一轮的零拷贝解码内核真正被调用（`DecodeStream` 成为 IOCP/Stream/UDP 接收主路径）。
3. Stream 路径以 `PipeReader`/`PipeWriter` 重写读循环。
4. P2P/relay 顺带 Span 化优化。
5. 分配/GC 为第一验收指标，吞吐不退化。

### 2.2 范围内 / 范围外

**范围内（全量适配）**

| 领域 | 处理方式 |
|------|----------|
| `SAEA.Common` | 新增 `PooledBufferWriter` |
| `SAEA.Sockets` 协议/编解码 | 接口现代化 + `BaseCoder` 内核接线 + `DecodedFrames` |
| `SAEA.Sockets` IOCP | Span 事件唯一化、发送 Span/Memory、零中间分配直发 |
| `SAEA.Sockets` Stream | `PipeReader`/`PipeWriter` 重写读循环，对外暴露 `PipeReader Input` |
| `SAEA.Sockets` UDP | 补 Span 事件、删 `ToArray()`、发送 Span/Memory |
| `SAEA.Sockets` Shortcut | 事件/发送改 Span/Memory |
| `SAEA.P2P` | P2P/relay Span 化优化 |
| FTP / WebSocket / QueueSocket / RPC / Http / RedisSocket / TcpTest | **机械适配**（改到编译通过 + 行为不变） |

**范围外**

- 线格式、`SocketProtocalType` 取值、事件时序。
- socket 接收语义（仍投递「原始字节块」，**不**按帧投递）。
- `net8.0` 多目标、新包发布、CI 流水线。

### 2.3 硬不变量（验收必须逐条成立）

1. 线格式：8B 小端长度 + 1B Type + body。
2. `P_LEN=8`、`P_Type=1`、`P_Head=9`。
3. 心跳 `bodyLen==0 && type==Heart` → 消费 9B、回调 `onHeart`、不产出。
4. 空 body 普通帧 → `Content` 非 null（长度 0）。
5. `type==BigData` → `onFile(content)`、不产出帧。
6. 多段 `ReadOnlySequence` 支持；增量拆帧（半包缓存、粘包循环）。
7. `MaxFrameLength` 守卫；非法长度抛 `KernelException`。
8. 连接/断开/超时/发送完成/`Dispose` 时序不变。
9. 心跳/发送/接收回调顺序不变。

---

## 三、基础原语

### 3.1 `PooledBufferWriter`（新增）

**位置：** `Src/SAEA.Common/Caching/PooledBufferWriter.cs`

**动机：** ns2.0 下 `ArrayBufferWriter<T>` 可用性不确定；需要「数组背衬 + 可归还 + 可零拷贝直发」的写入器。

```csharp
namespace SAEA.Common.Caching
{
    public sealed class PooledBufferWriter : IBufferWriter<byte>, IDisposable
    {
        public PooledBufferWriter(int initialCapacity = 4096);
        public ReadOnlySpan<byte> WrittenSpan { get; }
        public ReadOnlyMemory<byte> WrittenMemory { get; }
        public int WrittenCount { get; }
        public void Advance(int count);
        public Memory<byte> GetMemory(int sizeHint = 0);
        public Span<byte> GetSpan(int sizeHint = 0);
        public bool TryGetArray(out ArraySegment<byte> segment);  // 供 SocketAsyncEventArgs.SetBuffer
        public void Clear();                                       // 复位，不归还
        public void Dispose();                                     // 归还池化缓冲
    }
}
```

**实现要点：**
- 背衬数组由 `MemoryPoolManager` 租用，按需倍增（倍增时归还旧缓冲）。
- `TryGetArray` 暴露 `(buffer, 0, WrittenCount)`，配合 `SendAsync(ISocketProtocal)` 实现「编码一次、直发、完成归还」。
- `Dispose` 幂等。

### 3.2 复用既有原语

`MemoryPoolManager` / `PooledBuffer` / `PooledBytes` 保持公共形态；`PooledBufferWriter` 只新增，不改其签名。

---

## 四、协议与编解码改造

### 4.1 `ISocketProtocal`（破坏性）

**位置：** `Src/SAEA.Sockets/Interface/ISocketProtocal.cs`

```csharp
public interface ISocketProtocal
{
    long BodyLength { get; }
    byte Type { get; }
    ReadOnlyMemory<byte> Content { get; }        // 仅 get
    void WriteTo(IBufferWriter<byte> writer);    // 取代 byte[] ToBytes()
}
```

- **决策：** `Content` 采用 **`get` + 构造函数 / `protected set`**，不做公开可写。
- **决策：** 删除 `byte[] ToBytes()`；头部+体的序列化统一走 `WriteTo`。

### 4.2 `BaseSocketProtocal`（破坏性）

**位置：** `Src/SAEA.Sockets/Base/BaseSocketProtocal.cs`

```csharp
public sealed class BaseSocketProtocal : ISocketProtocal
{
    public ReadOnlyMemory<byte> Content { get; protected set; }

    public BaseSocketProtocal(long bodyLength, byte type, ReadOnlyMemory<byte> content);

    public void WriteTo(IBufferWriter<byte> writer);   // 直写 8B 长度 + 1B type + body，无 List<byte> 累加
}
```

- 删除 `List<byte>` 累积与 `ToArray()`；`WriteTo` 用 `BinaryPrimitives.WriteInt64LittleEndian` 写头，`writer.Write(Content.Span)` 写体。
- `Content` 默认 `ReadOnlyMemory<byte>.Empty`（非 null）。

### 4.3 `DecodedFrames`（新增，替换 `List<ISocketProtocal>`）

**位置：** `Src/SAEA.Sockets/Base/DecodedFrames.cs`

```csharp
public sealed class DecodedFrames : IDisposable
{
    public int Count { get; }
    public ISocketProtocal this[int index] { get; }
    public ReadOnlySpan<ISocketProtocal> Frames { get; }
    public void Dispose();   // 归还所有池化帧体，幂等
}
```

**实现要点（决策：池化租用 + `IDisposable` 批次）：**
- `BaseCoder.Decode` 解析时**单次租用**一块 `ArrayPool<byte>` 缓冲（容量按帧体总长估算，必要时增长），逐帧 `Slice` 成 `ReadOnlyMemory<byte>` 并构造 `BaseSocketProtocal`。
- 每批次 **1 次池操作、0 次 `new byte[]`**，无 `List<byte>`、无 `ToArray()`。
- 帧体与 `onFile` 的内存**在批次 `Dispose` 前有效**；`Dispose` 归还租用缓冲并清空。

### 4.4 `ICoder`（破坏性）

**位置：** `Src/SAEA.Sockets/Interface/ICoder.cs`

```csharp
public interface ICoder
{
    void Encode(ISocketProtocal p, IBufferWriter<byte> writer);

    DecodedFrames Decode(
        ReadOnlySequence<byte> data,
        Action<DateTime> onHeart = null,
        Action<ReadOnlyMemory<byte>> onFile = null);

    void DecodeStream(
        ReadOnlySpan<byte> data,
        IFrameHandler handler,
        Action<DateTime> onHeart = null,
        Action<ReadOnlySpan<byte>> onFile = null);

    void Clear();
}
```

- 删除 `byte[] Encode`、`List<ISocketProtocal> Decode(byte[])`、`Decode(ReadOnlySpan)` 返回 `List` 的形态。
- `DecodeStream` 为主推零拷贝路径：`handler.OnFrame` 的 `SocketFrame.Content` 与 `onFile` 的 `ReadOnlySpan<byte>` **仅在回调期间有效**。
- `Decode` 的 `onFile` 为 `ReadOnlyMemory<byte>`，**在批次 `Dispose` 前有效**。

### 4.5 `BaseCoder`（破坏性接线）

**位置：** `Src/SAEA.Sockets/Base/BaseCoder.cs`

- 保留常量与静态 `GetLength/GetType/GetContent`、`MaxFrameLength`。
- `Decode(ReadOnlySequence)`：无累加器，直接沿序列拆帧，帧体写入批次池化缓冲；心跳/BigData/空 body 语义不变。
- `DecodeStream(ReadOnlySpan, IFrameHandler, ...)`：驱动 per-instance `FrameDecoder`，命中普通帧调 `handler.OnFrame(in frame)`（零拷贝切片）。
- `Encode(ISocketProtocal, IBufferWriter)`：委托 `p.WriteTo(writer)`（或就地编码）。
- `Clear()`：归还累加器与批次缓冲并复位。
- `FrameDecoder`/`SocketFrame`/`IFrameHandler` 形态保持上一轮设计；`FrameDecoder` 的 BigData 产出由 `byte[]` 调整为写入当前 `IBufferWriter`/池化 slice。

### 4.6 连带

- `P2PCoder.DecodeP2P` 适配 `DecodedFrames`（或改用 `DecodeStream` + handler）。
- `P2PProtocol`、`WSProtocal` 使用 `protected set` 写入 `Content`。
- `FTPCoder`/`WSCoder`/`QueueCoder`/`RpcCoder`/`HttpCoder`/`RedisCoder`/`JUnpacker`：按新 `ICoder` 签名机械适配，保持各自行为。

---

## 五、Socket 层改造

### 5.1 接口与事件（破坏性）

**位置：** `Src/SAEA.Sockets/IClientSocket.cs`、`IServerSocket.cs`、`Handler/*`

- 接收：删除 `byte[]` 事件与 `OnClientReceiveBytesHandler`/`OnServerReceiveBytesHandler` 的公共使用；`OnClientReceiveSpan` / `OnServerReceiveSpan` 成为**唯一**接收事件（`ReadOnlySpan<byte>`，回调内有效）。
- 删除 `IocpClientSocket._isBaseClientType` / `IocpServerSocket._isBaseServerType` 兼容开关及其 `.ToArray()` 分支。
- 发送：

```csharp
void Send(ReadOnlySpan<byte> data);                                   // 租池拷贝一次（ns2.0 无 Socket.Send(Span)）
void SendAsync(ReadOnlyMemory<byte> data);
void SendAsync(ISocketProtocal protocal);
Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken token);
```

- 删除全部 `byte[]` 发送成员。
- `IUserToken` 增加「发送中缓冲区所有权」字段：`SendAsync(ISocketProtocal)` 编码进 `PooledBufferWriter` 后转移所有权给 socket，`ProcessSended` 时归还；`SendAsync(ReadOnlyMemory)` 在 `TryGetArray` 失败时租池拷贝并在完成时归还。

### 5.2 IOCP（`IocpClientSocket` / `IocpServerSocket`）

**位置：** `Src/SAEA.Sockets/Core/Tcp/IocpClientSocket.cs`、`IocpServerSocket.cs`

- 接收：`readArgs.Buffer.AsSpan(offset, transferred)` 直接投递 span（零拷贝）；删除 `.ToArray()` 与 `_isBase*Type` 判定。
- `SendAsync(ReadOnlyMemory)`：`MemoryMarshal.TryGetArray` 成功 → `SetBuffer(segment)` 零拷贝直发；否则租池拷贝一次。
- `SendAsync(ISocketProtocal)`：编码进 `PooledBufferWriter` → `TryGetArray` → `SetBuffer` 直发 → 完成归还，**零中间分配**。
- 保持 `SocketAsyncEventArgs` 直读模型；**不引入 `Pipe`**。

### 5.3 Stream（`StreamClientSocket` / `StreamServerSocket` / `SocketStream`）

**位置：** `Src/SAEA.Sockets/Core/Tcp/*`、`Src/SAEA.Sockets/Core/SocketStream.cs`

- 读循环以 `PipeReader.Create(networkStream)` 重写：`ReadAsync()` 拿 `ReadOnlySequence<byte>`；单段按 span、多段按段投递，复用 §4 拆帧内核。
- 发送以 `PipeWriter.Create(networkStream)`（或原 `Stream.WriteAsync`）承接 `ReadOnlyMemory<byte>`；ns2.0 下内部仍有一次 `byte[]` 拷贝，接受。
- Stream socket 新增 `PipeReader Input { get; }` 对外暴露。
- `GetStream()` 与 `SocketStream` 保留（`Stream` 契约在 ns2.0 仅 `byte[]`），仅机械现代化。

### 5.4 UDP（`UdpClientSocket` / `UdpServerSocket`）

**位置：** `Src/SAEA.Sockets/Core/Udp/*`

- 补 Span 接收事件、删除 `dataSpan.ToArray()`。
- 发送补 Span/Memory 重载；ns2.0 无 `Socket.SendTo(Span)`，发送侧租池拷贝一次。

### 5.5 Shortcut

**位置：** `Src/SAEA.Sockets/Shortcut/TCPClient.cs`、`TCPServer.cs`、`UDPClient.cs`、`UDPServer.cs`

- 事件签名改 `ReadOnlyMemory<byte>` / span；`Send`/`SendAsync` 改 Span/Memory；泛型约束 `where Coder : class, ICoder` 不变。

### 5.6 其他

- `SocketFactory` / `SocketOptionBuilder` / `SessionManager` / `UserTokenPool` / `BaseUserToken`：随接口与所有权字段做接线调整。
- 删除死代码 `Src/SAEA.Sockets/Core/RioExtention.cs`。

---

## 六、P2P / relay 优化（`SAEA.P2P`）

**位置：** `Src/SAEA.P2P/Protocol/P2PCoder.cs`、`P2PProtocol.cs`、`Relay/RelayManager.cs`、`Relay/RelaySession.cs`、`Channel/TCPChannel.cs`、`Channel/UDPChannel.cs`、`Core/P2PClient.cs`、`Core/P2PServer.cs`

- `Array.IndexOf` → `Span.IndexOf`。
- `Buffer.BlockCopy` 拼接 / `new byte[]` → `IBufferWriter` 单次写入或池化 slice。
- `P2PCoder` 改 `DecodedFrames` / `DecodeStream`。
- 保持协议语义与转发行为不变。

---

## 七、外部项目机械适配（改到编译通过、行为不变）

| 项目 | 文件 |
|------|------|
| SAEA.FTP | `Src/SAEA.FTP/Net/FTPCoder.cs` |
| SAEA.WebSocket | `Src/SAEA.WebSocket/Model/WSCoder.cs`、`WSProtocal.cs` |
| SAEA.QueueSocket | `Src/SAEA.QueueSocket/Net/QueueCoder.cs` |
| SAEA.RPC | `Src/SAEA.RPC/Net/RpcCoder.cs` |
| SAEA.Http | `Src/SAEA.Http/Base/Net/HttpCoder.cs` |
| SAEA.RedisSocket | `Src/SAEA.RedisSocket/Base/Net/RedisCoder.cs` |
| SAEA.Sockets.TcpTest | `Src/SAEA.Sockets.TcpTest/JContext.cs`（`JUnpacker`） |

---

## 八、测试、基准与验收

### 8.1 测试

- 现有 **255 项**全部迁移到新 API 并保持全绿；`StreamDecoderTest` 继续以 `LegacyDecoder`（旧字节算法副本）作 oracle 做 parity 比对。
- 新增：
  - `DecodedFrames` 生命周期：切片正确、多次 `Dispose` 幂等、租用归还到池。
  - `PooledBufferWriter` 单元测试：`GetSpan`/`Advance`/扩容/`TryGetArray`/`Dispose`。
  - 多段 `ReadOnlySequence` 拆帧。
  - Stream `PipeReader` 端到端。
  - UDP span 事件。
  - 7 个外部项目至少编译通过（有测试工程的跑测试）。

### 8.2 分配断言

- 用 `GC.GetTotalAllocatedBytes` / `GC.CollectionCount` 断言接收路径 **B/frame < 100 且 Gen0 增量 = 0**（可放在 `IocpBenchmark` 或专门测试）。

### 8.3 基准（扩展 `IocpBenchmark`）

- 保留 benchmark 专用 legacy 切片（`LegacyDecoder` + 旧发送）作对照。
- 指标：f/s、MiB/s、B/frame、GC0/1/2。
- 目标：新路径 **B/frame < 100**、**GC0 增量 0**、**吞吐不退化（±5% 容差）**。

### 8.4 验收（DoD）

1. `dotnet build Src/SAEA.Sockets.sln -c Release` → 0 error。
2. 所有测试工程全绿（255 + 新增）。
3. `IocpBenchmark` 三项指标达标。
4. `DecodeStream` 路径无 `byte[]`/`List<byte>`/`ToArray()`。
5. §2.3 硬不变量全部成立。
6. 版本 bump 至 `26.9.21.1`；`README.md`/`README.en.md` 测试计数与基准行同步。

---

## 九、破坏性与迁移

| 旧 API | 新 API | 迁移 |
|--------|--------|------|
| `byte[] ISocketProtocal.Content` | `ReadOnlyMemory<byte> Content { get; }` | 构造时传入；`.Span`/`.ToArray()` 取用 |
| `byte[] ISocketProtocal.ToBytes()` | `void WriteTo(IBufferWriter<byte>)` | 用 `PooledBufferWriter` 或 `PipeWriter` |
| `byte[] ICoder.Encode(ISocketProtocal)` | `void Encode(ISocketProtocal, IBufferWriter<byte>)` | 预租 writer |
| `List<ISocketProtocal> ICoder.Decode(byte[])` | `DecodedFrames Decode(ReadOnlySequence<byte>)` | `using` 包裹，循环索引 |
| `Action<byte[]> onFile` | `Action<ReadOnlySpan<byte>>`（stream）/`Action<ReadOnlyMemory<byte>>`（batch） | 回调内用或复制 |
| `event ... OnReceive(byte[])` | `OnClientReceiveSpan`/`OnServerReceiveSpan(ReadOnlySpan<byte>)` | 回调内用或复制 |
| `void Send(byte[])` | `void Send(ReadOnlySpan<byte>)` | 直接传 span |

- 属**破坏性发布**，README 增加「byte[] → Span/Memory 迁移对照」小节。
- 不发布 nupkg。
- 仓库既有「`.cs` 文件头版本号 `v26.4.23.1` 未同步」属已知遗留，本次仅 bump `.csproj` 版本（14 个库，不含测试项目）。

---

## 十、风险与缓解

| 风险 | 缓解 |
|------|------|
| ns2.0 API 可用性假设（`PipeReader.Create(Stream)`、`IBufferWriter`、`ArrayBufferWriter`） | 步骤 1 先实测；`PooledBufferWriter` 自建兜底 |
| Stream 读循环重写是行为风险最高处 | `StreamDecoderTest` + TCP 端到端测试 + parity 兜底 |
| 破坏面覆盖 7 个外部项目 | 交付顺序把「机械适配恢复全解决方案编译」设为独立一步，编译门禁 |
| `DecodedFrames` 生命周期误用 | `IDisposable` + XML 注释 + 专门生命周期测试 |
| 批次池化缓冲长期驻留 | `Dispose` 归还；容量按帧体总长、不无限增长 |
| `TryGetArray` 失败的发送路径产生拷贝 | 接受一次池化拷贝；基准断言 B/frame 仍 < 100 |
| `Content` 切片指向池化缓冲，批次 `Dispose` 后失效 | XML 注释 + 迁移小节显式写明有效期 |
| 池化缓冲归还后仍被引用（use-after-free） | 测试覆盖 + 文档契约 + `Clear`/`Dispose` 幂等 |

---

## 十一、修改文件清单

**新增**

| 文件 | 说明 |
|------|------|
| `Src/SAEA.Common/Caching/PooledBufferWriter.cs` | `IBufferWriter<byte>` 池化写入器 |
| `Src/SAEA.Sockets/Base/DecodedFrames.cs` | `IDisposable` 帧批次 |
| `Src/SAEA.P2PTest/Tests/SpanPipelineTest.cs` | 新增功能/生命周期测试 |

**改（`SAEA.Sockets`）**

`Interface/ISocketProtocal.cs`、`Interface/ICoder.cs`、`Base/BaseSocketProtocal.cs`、`Base/BaseCoder.cs`、`Base/FrameDecoder.cs`、`IClientSocket.cs`、`IServerSocket.cs`、`Handler/*`、`Core/Tcp/IocpClientSocket.cs`、`Core/Tcp/IocpServerSocket.cs`、`Core/Tcp/StreamClientSocket.cs`、`Core/Tcp/StreamServerSocket.cs`、`Core/SocketStream.cs`、`Core/Udp/UdpClientSocket.cs`、`Core/Udp/UdpServerSocket.cs`、`Core/BaseUserToken.cs`、`Core/UserTokenPool.cs`、`Core/SessionManager.cs`、`Shortcut/TCPClient.cs`、`Shortcut/TCPServer.cs`、`Shortcut/UDPClient.cs`、`Shortcut/UDPServer.cs`、`SocketFactory.cs`、`SocketOptionBuilder.cs`

**删除**

`Src/SAEA.Sockets/Core/RioExtention.cs`

**改（其他库）**

`Src/SAEA.P2P/Protocol/P2PCoder.cs`、`Protocol/P2PProtocol.cs`、`Relay/RelayManager.cs`、`Relay/RelaySession.cs`、`Channel/TCPChannel.cs`、`Channel/UDPChannel.cs`、`Core/P2PClient.cs`、`Core/P2PServer.cs`、`Src/SAEA.FTP/Net/FTPCoder.cs`、`Src/SAEA.WebSocket/Model/WSCoder.cs`、`Model/WSProtocal.cs`、`Src/SAEA.QueueSocket/Net/QueueCoder.cs`、`Src/SAEA.RPC/Net/RpcCoder.cs`、`Src/SAEA.Http/Base/Net/HttpCoder.cs`、`Src/SAEA.RedisSocket/Base/Net/RedisCoder.cs`、`Src/SAEA.Sockets.TcpTest/JContext.cs`

**改（测试/文档/版本）**

`Src/SAEA.P2PTest/Tests/*`（含 `IocpBenchmark.cs`、`StreamDecoderTest.cs`、`PerformanceTest.cs`）、`Src/SAEA.P2PTest/TestHarness.cs`、`Program.cs`、`README.md`、`README.en.md`、14 个库 `.csproj` 版本号

---

## 十二、实施顺序

1. `SAEA.Common` `PooledBufferWriter` + 单元测试（并实测 ns2.0 API 可用性）。
2. 协议/编解码：`ISocketProtocal`/`ICoder`/`BaseSocketProtocal`/`BaseCoder`/`DecodedFrames` + 测试。
3. 7 个外部 `ICoder` 实现机械适配（恢复全解决方案可编译）。
4. `IClientSocket`/`IServerSocket`/Handler/`IUserToken` 接口现代化。
5. IOCP 客户端/服务端实现 + 基准。
6. Stream（`PipeReader`）+ UDP 实现 + 测试。
7. Shortcut 适配。
8. P2P/relay 优化 + 测试。
9. 文档/版本 bump/最终验证（`--all` + Release build + `IocpBenchmark`）。

---

## 十三、预期收益

- 公共面彻底 `byte[]` 化退出，上一轮零拷贝内核真正生效；接收路径 B/frame < 100、Gen0 不增长。
- `SendAsync(ISocketProtocal)` 编码 + 发送合并为一次池化写、零中间分配。
- Stream 读循环基于 `PipeReader`，多段数据零拷贝拆帧。
- P2P/relay 去掉逐字节拼接与 `new byte[]`。
- 单个 spec 覆盖全解决方案，机械适配有编译门禁兜底。