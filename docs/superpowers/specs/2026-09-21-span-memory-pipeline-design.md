# SAEA Span/Memory/Pipe 全链路改造设计文档

**日期：** 2026-09-21（rev.3，第二轮深度审查后修订）
**范围：** 全解决方案公共面现代化（`SAEA.Common` 原语 + `SAEA.Sockets` 协议/编解码/Socket 层 + `SAEA.P2P` 优化 + 外部项目全量适配）
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

> **rev.2 修订摘要（深度审查发现，详见各节）：** ① `BaseSocketProtocal` **不得 sealed**（`P2PProtocol` 继承）；② 成员改 `protected set` + 构造函数，杜绝对象初始化器赋值；③ `WSProtocal` 需保留池化背衬数组；④ 外部适配范围由「7 项目」修正为**全解决方案**（§七）；⑤ `DecodeStream` 从 `ICoder` 拆到 `IFrameCoder`；⑥ 修正 Stream 层前提（`StreamClientSocket` 无读循环）；⑦ 补全待删 `byte[]` Socket 成员清单；⑧ 补 ns2.0 `Encoding.GetString(ReadOnlySpan)` 缺失事实。
>
> **rev.3 修订摘要（第二轮深度审查发现，详见各节）：** ⑨ **P2P 解码所有权**——`DecodeP2P` 改返回 `DecodedFrames`，`P2PProtocol` 降为编码便捷类型（§4.6/§六）；`BaseCoder` 静态 `GetType`/`GetContent` 无调用方删除、`GetLength` 改 Span、删未订阅 `internal event OnReceiveSpan`（§4.5）；⑩ 补受影响文件 `Discovery/LocalDiscovery.cs`、`NAT/HolePuncher.cs`、`Model/ChannelInfo.cs`、`SAEA.Http/WebHost.cs`+`HttpSocket*.cs`、`SAEA.Sockets.UdpTest/Program.cs`（§七/§十一）；⑪ 服务端接收事件载荷由 `ISession` 收紧为 `IUserToken`（§5.1）；⑫ `FTPCoder` 额外 `Decode(byte[], Action<ISocketProtocal>, ...)` 重载须保留（§七）。
>
> **rev.3 追加更正（四路并行深挖后）：** ⑬ `WSProtocal` 现状**无背衬字段**且默认 `ToBytes()` **原地掩码改写 `Content`** → 新增私有 `_buffer` + 拆 `WriteTo`/`WriteMaskedTo`（§4.6/§9）；⑭ `SAEA.MQTT`/`SAEA.DNS` **非范围外**（依赖 `StreamClientSocket`/`Shortcut.UDPClient`，须适配）（§七）；⑮ 补全 `IocpServerSocket`/`UdpServerSocket`/`Stream*` 非接口公共发送成员、`P2PCoder.GetP2PMessageType`、BigData 连续性证据、`UserToken.Coder` 已静态确认、sln 实为 33 工程 + 2 文件夹（§一/§5.1/§5.3）；⑯ **`Action<ReadOnlySpan<byte>>` 非法** → 新增具名委托 `FileSpanHandler`（§4.4）；⑰ `ICoder.Decode(ReadOnlySequence)` 定为**有状态**（复用 `_decoder` 半包缓存），与旧 `Decode(byte[])` 语义一致，`onFile` 延迟回调保序（§4.4）。

---

## 一、关键事实核对（实现前已验证）

| 事实 | 结论 / 出处 | 对设计的影响 |
|------|------------|-------------|
| 框架约束 | 仅保留 `netstandard2.0` / C# 8，引用 `System.IO.Pipelines 10.0.6`（含 `System.Memory`） | `ReadOnlySequence<byte>`、`IBufferWriter<byte>`、`PipeReader` 可用；`SequenceReader<byte>` 不可用（上一轮已确认） |
| ns2.0 限制（写） | `Encoding.GetBytes(ReadOnlySpan<char>, Span<byte>)` 不可用；`Socket.Send(Span)`/`SendTo(Span)` 不可用；`Stream.WriteAsync(ReadOnlyMemory)` 不可用；`ArrayBufferWriter<T>` 存在性不确定 | 需 `ArrayPool<char>` 中转、发送侧租池拷贝、自建 `PooledBufferWriter` |
| ns2.0 限制（读） | **`Encoding.GetString(ReadOnlySpan<byte>)` 不可用**（netstandard2.1+）；`Stream.ReadAsync(Memory<byte>)`/`Socket.ReceiveAsync(Memory<byte>)` 均不可用 | `P2PProtocol.GetContentAsString` 等须 `.Span.ToArray()`；Memory 异步收发须桥接 `ArraySegment` |
| `ICoder` 实现 | **8 个 / 7 个项目**：`BaseCoder`(SAEA.Sockets)、`FTPCoder`(SAEA.FTP)、`WSCoder`(SAEA.WebSocket)、`QueueCoder`(SAEA.QueueSocket)、`JUnpacker`(SAEA.Sockets.TcpTest)、`RpcCoder`(SAEA.RPC)、`HttpCoder`(SAEA.Http)、`RedisCoder`(SAEA.RedisSocket) | 接口一改，全部须同步 |
| `ISocketProtocal` 实现 | **2 个**：`BaseSocketProtocal`、`WSProtocal`(SAEA.WebSocket) | `WSProtocal` 需同步 |
| `BaseSocketProtocal` 派生 | **`P2PProtocol : BaseSocketProtocal`**（`Src/SAEA.P2P/Protocol/P2PProtocol.cs:38`），其 ctor 写 `Type`/`Content`/`BodyLength` | 基类**不得 sealed**；成员须 `protected set` 或经 base ctor |
| `IClientSocket`/`IServerSocket` 实现 | 各 **3 个**：`IocpClient/ServerSocket`、`UdpClient/ServerSocket`、`StreamClient/ServerSocket` | 接口一改，全部须同步 |
| `BaseCoder` 派生 | 仅 `P2PCoder : BaseCoder`（`Src/SAEA.P2P/Protocol/P2PCoder.cs`）；`DecodeP2P` 调有状态 `Decode(ReadOnlySpan)` 返回 `List`，`EncodeP2P` 返回 `byte[]` | 需适配 `DecodedFrames` / `IBufferWriter` |
| socket↔coder 耦合 | socket 层**不调用 coder**（只投递原始字节，由消费方如 `P2PClient`/`WSClient` 解码）；`IContext<ICoder>` 仅暴露 `UserToken`/`Unpacker`；`IUserToken.Coder` 可提供会话 coder | `DecodeStream` 主路径落在**消费方 + 基准**；`SendAsync(ISocketProtocal)` 经 `UserToken.Coder` 取 coder |
| Stream 层现状 | `StreamClientSocket` **无读循环**（手工 Stream API，`OnReceive` 标注 `Obsolete` 从不触发）；`StreamServerSocket` 用**共享** `_receiveBuffer` 的 accept 读循环，且 `SessionManager` 抛 `NotImplementedException` | PipeReader 重写只对 server 有意义；须避免回退现状 |
| `WSProtocal` 现状 | `: ISocketProtocal, IDisposable`；`IsPooled` + `MemoryPoolManager.Return(Content,...)` 需原始 `byte[]`；`WSCoder.DoMask` 原地改写；`ToBytes(bool masked)` 有副作用 | `Content` 改 `ReadOnlyMemory` 后须保留背衬数组字段；非机械适配 |
| 线格式 | 8 字节小端长度 + 1 字节 Type + body；`P_LEN=8`、`P_Type=1`、`P_Head=9`，`SmallDataThreshold=4KB` | 解析必须逐字保持 |
| 心跳语义 | `bodyLen==0 && type==Heart` → 消费 9 字节、回调 `onHeart`、**不产出** | 必须逐字保持 |
| 空 body 语义 | 普通帧 `bodyLen==0` → `Content` **非 null**（长度 0） | 必须逐字保持 |
| BigData 语义 | `type==BigData` → `onFile(content)`，**不产出帧** | 必须逐字保持 |
| 非法长度 | `bodyLen < 0 \|\| > MaxFrameLength` → 抛 `KernelException` | 必须保持 |
| 既有池化原语 | `SAEA.Common/Caching/`：`MemoryPoolManager`（三级 `ArrayPool<byte>`，Small=4KB/Medium=64KB）、`PooledBuffer`、`PooledBytes` | 新 `PooledBufferWriter` 落在此处并复用 `MemoryPoolManager` |
| 死代码 | `Src/SAEA.Sockets/Core/RioExtention.cs`（注释掉的 Pipe 适配） | 本阶段删除 |
| 测试工程 | `SAEA.P2PTest` 直接引用 `SAEA.Sockets`；`--all` 当前 **255 项全绿**；`LegacyDecoder` 保留旧字节算法作 oracle | 可做 parity 与端到端基准 |
| 解决方案构成 | `SAEA.Sockets.sln` 共 **35 条 `Project(` 记录 = 33 个 .csproj + 2 个解决方案文件夹**（`Tests`/`solution`）；磁盘上另有 **`SAEA.Common.Tests`、`TestConnectionReuseApp` 两个未纳入 sln** 的工程 | §七/§八的「35 条目」口径正确；未入 sln 的工程不受 Release 门禁覆盖，新测试放 `SAEA.P2PTest` |
| `ISocketProtocal.ToBytes()` 副作用 | `WSProtocal.ToBytes()` → `ToBytes(true)`，其中 `WSCoder.DoMask(this.Content, ...)`（`WSProtocal.cs:141`）**原地掩码改写 `Content`**；`ToBytes(false)`（服务端 `WSServerImpl.Reply`，`WSServerImpl.cs:229`）不改写 | 新 `WriteTo` **不得**原地改 `Content`；掩码须改为「写入 writer」；客户端/服务端须显式选掩码与否（§4.6） |
| `WSProtocal` 池化生命周期 | `WSCoder.Decode` 仅在 `> MemoryPoolManager.SmallThreshold(4096)` 时 `Rent` 并置 `IsPooled`（`WSCoder.cs:125/152/177/197`）；`WSProtocal.Dispose()` 归还，但**库代码从不调用 `Dispose`**（仅 `SAEA.WebSocketTest/Program.cs` 调用）→ 现存租用不归还缺陷 | 保留 `IsPooled` 语义；本设计不新增/不修复该既有缺陷，但需在新测试中避免引入同样泄漏（§十） |
| 其他 coder 现状 | `FTPCoder`/`QueueCoder`/`RpcCoder`/`HttpCoder`/`RedisCoder`/`JUnpacker` 的接口 `Decode` 与部分 `Encode`/`Clear` **本就抛 `NotImplementedException`**（`FTPCoder.cs:44/58`、`QueueCoder.cs:63/75`、`RpcCoder.cs:58`、`HttpCoder.cs:65`、`RedisCoder.cs:55`、`JContext.cs:38/91`） | 其新接口适配主要是**签名变更**，风险低；各自额外重载须保留（§4.6） |
| `P2PCoder` 额外面 | `DecodeP2P` **无 `onFile` 参数**，内部 `base.Decode(data, onHeart)` → BigData/File 帧对 P2P **静默丢弃**；并重新用对象初始化器把每帧包成 `P2PProtocol`（多一次分配）；另有 `GetP2PMessageType(byte[])` 静态 | 必须逐字保留丢弃语义；`GetP2PMessageType` 须保留（§4.6/§6） |
| 内核调用方现状 | `TryReadFrame` 仅 `BaseCoder` 内部调用；`DecodeStream`/`Decode(ReadOnlySequence)` **无任何生产调用方**（仅测试：`IocpBenchmark.cs:84/132`、`StreamDecoderTest.cs:181/184/219`、`PerformanceTest.cs:138`） | 确认「零拷贝内核被接口挡住」；socket 层零分配的测量须靠测试驱动（§八） |
| BigData 连续性 | `FrameDecoder` 先缓冲整帧再产出（`FrameDecoder.cs:100-101`），`EnsureCapacity` 归并到 offset 0，故 `bodyLen` 字节在 `_buffer[_start+P_Head..]` **连续**，可如 `Data` 一样切片（`FrameDecoder.cs:113`） | BigData span 化可行；切片仅在下次 `Append`/`EnsureCapacity`/`Clear` 前有效（§4.5） |

---

## 二、目标 / 范围 / 硬不变量

### 2.1 目标

1. 消除公共面上的 `byte[]`：编解码、协议对象、socket 收发、Shortcut 全部改为 Span/Memory/`ReadOnlySequence`/`IBufferWriter`。
2. 让上一轮的零拷贝解码内核真正被调用（`DecodeStream`/`Decode(ReadOnlySequence)` 成为**帧式消费方**的主推路径；socket 层只投递原始字节，落地在消费方与基准）。
3. Stream 路径以 `PipeReader`/`PipeWriter` 重写**服务端**读循环。
4. P2P/relay 顺带 Span 化优化。
5. 分配/GC 为第一验收指标，吞吐不退化。

### 2.2 范围内 / 范围外

**范围内（全量适配）**

| 领域 | 处理方式 |
|------|----------|
| `SAEA.Common` | 新增 `PooledBufferWriter` |
| `SAEA.Sockets` 协议/编解码 | 接口现代化 + `BaseCoder` 内核接线 + `DecodedFrames` + `IFrameCoder` 拆分 |
| `SAEA.Sockets` IOCP | Span 事件唯一化、发送 Span/Memory、零中间分配直发 |
| `SAEA.Sockets` Stream | **服务端**读循环以 `PipeReader` 重写；client 仅发送/Stream 现代化 |
| `SAEA.Sockets` UDP | 补 Span 事件、删 `ToArray()`、发送 Span/Memory |
| `SAEA.Sockets` Shortcut | 事件/发送改 Span/Memory |
| `SAEA.P2P` | P2P/relay Span 化优化 |
| 外部项目（见 §七） | **全量适配**（改到 `SAEA.Sockets.sln` 编译通过 + 行为不变） |

**范围外**

- 线格式、`SocketProtocalType` 取值、事件时序。
- socket 接收语义（仍投递「原始字节块」，**不**按帧投递）。
- `net8.0` 多目标、新包发布、CI 流水线。
- 独立编解码栈（`SAEA.MQTT` 的 `PacketFormatterAdapter`、`SAEA.DNS`、`SAEA.Socket5`）不依赖 `ICoder`/`ISocketProtocal`，不在本阶段范围。

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

- **决策：** 三成员均 **只读**；实现类经构造函数或 `protected set` 赋值，不做公开可写。
- **决策：** 删除 `byte[] ToBytes()`；头部+体的序列化统一走 `WriteTo`。
- **迁移：** 现存的 `new BaseSocketProtocal { BodyLength = ..., Type = ..., Content = ... }` 对象初始化器（`BaseCoder.cs:113/192`、`LegacyDecoder.cs:60`、`FileSocket`、`Audio.Net`、`Shortcut`、P2P 等）必须改为构造函数调用。

### 4.2 `BaseSocketProtocal`（破坏性）

**位置：** `Src/SAEA.Sockets/Base/BaseSocketProtocal.cs`

```csharp
public class BaseSocketProtocal : ISocketProtocal   // 非 sealed：P2PProtocol 继承
{
    public long BodyLength { get; protected set; }
    public byte Type { get; protected set; }
    public ReadOnlyMemory<byte> Content { get; protected set; }

    public BaseSocketProtocal();                                             // Content = Empty
    public BaseSocketProtocal(byte type, ReadOnlyMemory<byte> content);      // BodyLength 由 content.Length 推导
    public BaseSocketProtocal(long bodyLength, byte type, ReadOnlyMemory<byte> content);

    // 既有静态工厂保留并改用新 ctor：Parse / ParseRequest / ParseStream
    public void WriteTo(IBufferWriter<byte> writer);   // 直写 8B 长度 + 1B type + body，无 List<byte> 累加
}
```

- **决策：** **不 sealed**（`P2PProtocol`、后续派生都需要）。成员 `protected set` 供派生类 ctor 赋值。
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
- **无终结器（刻意）**：忘记 `Dispose` 不会崩溃，但会静默丢失池化缓冲（不同于旧 `List`/`byte[]` 靠 GC 回收）。调用方必须 `using`；见 §十。

### 4.4 `ICoder` + `IFrameCoder`（破坏性，rev.2 拆分）

**位置：** `Src/SAEA.Sockets/Interface/ICoder.cs`、新增 `Interface/IFrameCoder.cs`

```csharp
public interface ICoder
{
    void Encode(ISocketProtocal p, IBufferWriter<byte> writer);

    DecodedFrames Decode(
        ReadOnlySequence<byte> data,
        Action<DateTime> onHeart = null,
        Action<ReadOnlyMemory<byte>> onFile = null);

    void Clear();
}

// 帧式（9 字节头 + body）编解码器专用：仅 BaseCoder 及其派生实现。
public interface IFrameCoder : ICoder
{
    // 注意：C# 不允许 Action<ReadOnlySpan<byte>>（ref struct 不能作泛型实参），必须用具名委托。
    void DecodeStream(
        ReadOnlySpan<byte> data,
        IFrameHandler handler,
        Action<DateTime> onHeart = null,
        FileSpanHandler onFile = null);
}

public delegate void FileSpanHandler(ReadOnlySpan<byte> content);
```

- **rev.3 更正（编译性）**：`Action<ReadOnlySpan<byte>>` **不是合法 C#**（`ref struct` 不能作 `Action<T>` 类型实参）。故新增具名委托 `FileSpanHandler`（位于 `Interface/IFrameCoder.cs`）承载 span 版 `onFile`。`Action<ReadOnlyMemory<byte>>`（batch 版）合法。
- **rev.3 更正（语义）**：`ICoder.Decode(ReadOnlySequence<byte>)` 实现为**有状态**——驱动 per-instance `FrameDecoder`（复用半包缓存），从而与旧 `Decode(byte[])`（有状态）语义一致。旧代码中「无状态 `Decode(ReadOnlySequence)`」无任何生产调用方，故合并为单一有状态方法。多段序列按段喂入；帧体复制进批次池化缓冲以获得 `Dispose` 前稳定的 `ReadOnlyMemory`；`onFile` 采用**事件延迟**（先记录 offset/时间、待缓冲定型后再按原顺序回调），保证顺序与内存有效期。

- **rev.2 决策：** `DecodeStream` 的拆帧语义绑定 SAEA 的 8B+1B 线格式，**不属通用 `ICoder`**。`BaseCoder : IFrameCoder`，`P2PCoder` 继承获得；`FTPCoder`/`WSCoder`/`QueueCoder`/`RpcCoder`/`HttpCoder`/`RedisCoder`/`JUnpacker` 只实现 `ICoder`。
- 删除 `byte[] Encode`、`List<ISocketProtocal> Decode(byte[])`、有状态 `Decode(ReadOnlySpan)` 返回 `List` 的形态。
- `Decode(ReadOnlySequence)` **无状态**；`DecodeStream` 为 per-instance **有状态**增量解码（半包缓存）。
- `DecodeStream` 回调：`handler.OnFrame` 的 `SocketFrame.Content` 与 `onFile` 的 `ReadOnlySpan<byte>` **仅在回调期间有效**。
- `Decode` 的 `onFile` 为 `ReadOnlyMemory<byte>`，**在批次 `Dispose` 前有效**。

### 4.5 `BaseCoder`（破坏性接线）

**位置：** `Src/SAEA.Sockets/Base/BaseCoder.cs`

- 类型改 `public class BaseCoder : IFrameCoder`。
- 保留常量 `P_LEN`/`P_Type`/`P_Head`/`SmallDataThreshold`/`MaxFrameLength`；静态 `GetLength` 改 `GetLength(ReadOnlySpan<byte>)`（`byte[]` 隐式转换，`ProtocolAdvancedTest` 调用点无需改）。**删除无调用方的 `GetType`/`GetContent`**，并删除从未订阅的 `internal delegate OnReceiveSpanHandler` / `internal event OnReceiveSpan`（`BaseCoder.cs:74/79`）。
- `Decode(ReadOnlySequence)`：无累加器，直接沿序列拆帧，帧体写入批次池化缓冲；心跳/BigData/空 body 语义不变。
- `DecodeStream(ReadOnlySpan, IFrameHandler, ...)`：驱动 per-instance `FrameDecoder`，命中普通帧调 `handler.OnFrame(in frame)`（零拷贝切片）。
- `Encode(ISocketProtocal, IBufferWriter)`：委托 `p.WriteTo(writer)`（或就地编码）。
- `Clear()`：归还累加器与批次缓冲并复位。
- `FrameDecoder`/`SocketFrame`/`IFrameHandler` 形态保持上一轮设计；`FrameDecoder.TryReadFrame` 的 BigData 产出由 `out byte[] fileContent` 改为 `out ReadOnlySpan<byte> fileContent`（指向 `_buffer` 切片，仅在 `DecodeStream` 回调期间有效），消除 BigData 路径的 `new byte[]` + `Buffer.BlockCopy`。

### 4.6 连带

- `P2PCoder`（**解码所有权，rev.3 明确**）：`DecodeP2P` 现返回 `List<P2PProtocol>`，被 **7 处**内部调用点以 `P2PProtocol` 消费并传入 `ProcessMessage`/`ProcessSignalMessage`——`Core/P2PClient.cs:185`、`Core/P2PServer.cs:128`、`Discovery/LocalDiscovery.cs:144`、`Channel/TCPChannel.cs:98`、`Channel/UDPChannel.cs:104`、`Relay/RelayManager.cs:132`、`NAT/HolePuncher.cs:104`。**决策：解码路径统一到 `DecodedFrames` + `BaseSocketProtocal`**（不新增帧工厂）：`DecodeP2P(ReadOnlySequence<byte>, ...)` 返回 `DecodedFrames`；调用点改 `using var frames = ...; foreach (var frame in frames.Frames) ProcessXxx(sessionId, frame);`；`ProcessMessage`/`ProcessSignalMessage` 参数类型由 `P2PProtocol` 改为 `ISocketProtocal`（`Type` 以 `(P2PMessageType)frame.Type` 读取，`Content` 直接取切片）。批次 `Dispose` 前切片有效；确需跨批留存者由调用方显式复制。
- `P2PProtocol`：**降为编码便捷类型**——保留 `Create(...)` 与 `GetContentAsString`，删除仅解码用的无参 `new P2PProtocol{...}` 对象初始化器路径；ctor 改经 base ctor 赋值（`base((byte)messageType, content)`）。`GetContentAsString` 因 ns2.0 无 `Encoding.GetString(ReadOnlySpan)`，改用 `Encoding.UTF8.GetString(Content.Span.ToArray())`。
- `P2PCoder` 额外公共面须保留：静态 `GetP2PMessageType(byte[])`（`P2PCoder.cs:84`，读 `data[P_LEN]`）；`DecodeP2P` **无 `onFile` 参数**且 BigData/File 帧对 P2P **静默丢弃**（`P2PCoder.cs:45` 只传 `onHeart`）——此语义逐字保持。
- `EncodeP2P(...)`（**决策：保留 `byte[]` 现签名以限制 churn**，编码侧非验收热路径）：内部改用 `PooledBufferWriter` + `WriteTo` 后 `ToArray()`；另加 `EncodeP2P(..., IBufferWriter<byte>)` 零拷贝重载供新路径/基准。`P2PCoder : BaseCoder` 自动获得 `IFrameCoder`。
- **`WSProtocal`（重点，非机械）**：现状**无背衬字段**——`Content`（`byte[]`）直接持有 `WSCoder.Decode` 租用的数组，`IsPooled` 标记是否需归还（`WSProtocal.cs:50-57`）；`Dispose` 调 `MemoryPoolManager.Return(this.Content, this.Content.Length)`。改造：`Content` 改 `ReadOnlyMemory<byte>` **并新增私有 `byte[] _buffer` 背衬字段**（供 `Dispose` 归还原始数组），`IsPooled` 保留。删除 `ToBytes()`/`ToBytes(bool)`；改为 `WriteTo(IBufferWriter<byte>)`（**不掩码**，等价旧 `ToBytes(false)`）与 `WriteMaskedTo(IBufferWriter<byte>)`（写入时掩码，等价旧 `ToBytes(true)`），二者**均不得原地改写 `Content`**。
- **掩码选择下移**：`WSCoder.Encode(ISocketProtocal, writer)` 及 `WSClient`/`WSServerImpl`/`WSSServerImpl` 须显式选择掩码变体（旧默认 `ToBytes()`=掩码、服务端 `Reply` 用 `ToBytes(false)`=不掩码）；掩码算法 `DoMask`/`DoMaskUnsafe` 与线格式保持不变。
- `FTPCoder`/`WSCoder`/`QueueCoder`/`RpcCoder`/`HttpCoder`/`RedisCoder`/`JUnpacker`：按新 `ICoder` 签名适配，保持各自行为（`HttpCoder.Decode` 现为 `NotImplementedException`，可维持）。

---

## 五、Socket 层改造

### 5.1 接口与事件（破坏性）

**位置：** `Src/SAEA.Sockets/IClientSocket.cs`、`IServerSocket.cs`、`Handler/*`

- 接收：删除 `byte[]` 事件与 `OnClientReceiveBytesHandler`/`OnServerReceiveBytesHandler` 的公共使用；`OnClientReceiveSpan` / `OnServerReceiveSpan` 成为**唯一**接收事件（`ReadOnlySpan<byte>`，回调内有效）。
  - rev.3 载荷澄清：`Handler/OnReceiveHandler.cs` 同文件定义 `OnReceiveHandler(ISession, byte[])`（服务端旧事件）与 `OnClientReceiveHandler(byte[])`（客户端旧事件）；服务端旧事件载荷为 `ISession`，新 `OnServerReceiveSpanHandler` 载荷为 `IUserToken`（`IUserToken : ISession`，更具体、兼容更窄），消费者须改签名。
- 删除 `IocpClientSocket._isBaseClientType` / `IocpServerSocket._isBaseServerType` 兼容开关及其 `.ToArray()` 分支。
- 发送改为 Span/Memory/`ISocketProtocal`：

```csharp
// IClientSocket
void Send(ReadOnlySpan<byte> data);                       // 租池拷贝一次（ns2.0 无 Socket.Send(Span)）
void SendAsync(ReadOnlyMemory<byte> data);
void SendAsync(ISocketProtocal protocal);
Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken token);

// IServerSocket
void Send(string sessionID, ReadOnlySpan<byte> data);
void SendAsync(string sessionID, ReadOnlyMemory<byte> data);
void SendAsync(string sessionID, ISocketProtocal protocal);
void End(string sessionID, ReadOnlyMemory<byte> data);
void SendAsync(IPEndPoint ipEndPoint, ReadOnlyMemory<byte> data);
```

- **待删的 `byte[]` 发送/接收成员清单（rev.2 补全）：**
  - `IClientSocket`：`BeginSend(byte[])`、`Send(byte[])`、`SendAsync(byte[])`、`SendAsync(byte[], int, int, CancellationToken)`、`ReceiveAsync(byte[], int, int, CancellationToken)`。
  - `IServerSocket`：`SendAsync(string, byte[])`、`Send(string, byte[])`、`End(string, byte[])`、`SendAsync(IPEndPoint, byte[])`。
  - 实现类内部辅助 / 非接口公共成员（rev.3 补全，须同步改造或删除）：`IocpClientSocket` 另有 `public IUserToken UserToken`、`SendAsync(IUserToken, byte[])`；`IocpServerSocket` 另有 `SendAsync(IUserToken, byte[])`、`Send(IUserToken, byte[])`、`BeginSend(IUserToken, byte[])`、`EndSend(IUserToken, IAsyncResult)`、`Disconnect(IUserToken, Exception)`；`UdpClientSocket` 另有 `SendAsync(IPEndPoint, byte[])`/`SendAsync(byte[])`/`BeginSend(byte[])`（其 `ReceiveAsync`/`GetStream` 本就抛 `NotSupportedException`，保留）；`UdpServerSocket` 另有 `SendAsync(IUserToken, byte[])`/`Send(IUserToken, byte[])`/`SendAsync(string, byte[])`/`BeginSend(IUserToken, byte[])`/`EndSend(IUserToken, IAsyncResult)`/`End(string, byte[])`/`SendAsync(IPEndPoint, byte[])`；`StreamClientSocket.SendAsync(byte[])`（`[Obsolete]`，直接删）/`Send(byte[])`/`BeginSend(byte[])`/`ReceiveAsync(byte[], int, int[, CancellationToken])`；`StreamServerSocket` 仅有 `SendAsync(string, byte[])`/`Send(string, byte[])`/`End(string, byte[])`/`SendAsync(IPEndPoint, byte[])`（无 `Send/EndSend`）。
  - ns2.0 注意：`Socket.ReceiveAsync(Memory<byte>)`/`Stream.ReadAsync(Memory<byte>)` 不可用；Memory 接收需桥接 `ArraySegment`（租池 + `Task.Factory.FromAsync` 或既有 `SocketAsyncEventArgs` 模型）。
- `IUserToken` 增加「发送中缓冲区所有权」字段：`SendAsync(ISocketProtocal)` 编码进 `PooledBufferWriter` 后转移所有权给 socket，`ProcessSended` 时归还；`SendAsync(ReadOnlyMemory)` 在 `TryGetArray` 失败时租池拷贝并在完成时归还。
- **coder 来源（rev.3 已静态确认，非假设）：** socket 不持有新 coder 实例；`SendAsync(ISocketProtocal)` 经 `Context.UserToken.Coder`（客户端）/ 会话 `UserToken.Coder`（服务端）取 coder。`BaseContext<Coder>` ctor 已 `Unpacker = Activator.CreateInstance<Coder>()` 且 `UserToken.Coder = Unpacker`（`BaseContext.cs:49-51`）；`UserTokenFactory.Create` 复制 `UnpackerType` 并 `userToken.Coder = ...`（`UserTokenFactory.cs:58-64`），`UserTokenPool` ctor 预填（`UserTokenPool.cs:68`）。注意 `UserTokenFactory.Create` 在 `context.UserToken.Coder` 为 null 时会在 `:59` 抛异常——正常 `SocketOptionBuilder.UseIocp<T/context>()` 流程非 null。

### 5.2 IOCP（`IocpClientSocket` / `IocpServerSocket`）

**位置：** `Src/SAEA.Sockets/Core/Tcp/IocpClientSocket.cs`、`IocpServerSocket.cs`

- 接收：`readArgs.Buffer.AsSpan(offset, transferred)` 直接投递 span（零拷贝）；删除 `.ToArray()` 与 `_isBase*Type` 判定。
- `SendAsync(ReadOnlyMemory)`：`MemoryMarshal.TryGetArray` 成功 → `SetBuffer(segment)` 零拷贝直发；否则租池拷贝一次。
- `SendAsync(ISocketProtocal)`：取会话 coder → `Encode` 进 `PooledBufferWriter` → `TryGetArray` → `SetBuffer` 直发 → 完成归还，**零中间分配**。
- 保持 `SocketAsyncEventArgs` 直读模型；**不引入 `Pipe`**。

### 5.3 Stream（`StreamClientSocket` / `StreamServerSocket` / `SocketStream`）（rev.2 修正前提）

**位置：** `Src/SAEA.Sockets/Core/Tcp/*`、`Src/SAEA.Sockets/Core/SocketStream.cs`

- **现状澄清：** `StreamClientSocket` 是薄封装，**没有读循环**，其 `OnReceive` 已 `Obsolete` 且从不触发；因此「读循环 PipeReader 化」**只适用于 `StreamServerSocket` 的 accept 读循环**。
- `StreamServerSocket`：以 per-connection `PipeReader.Create(networkStream)` 重写 accept 读循环（替换共享 `_receiveBuffer` + `Read`），`ReadAsync()` 拿 `ReadOnlySequence<byte>`；单段按 span、多段按段投递，复用 §4 拆帧内核。`SessionManager` 当前抛 `NotImplementedException`——保持不回归即可，不强行新增语义。
- 发送：`PipeWriter.Create(networkStream)`（或原 `Stream.WriteAsync`）承接 `ReadOnlyMemory<byte>`；ns2.0 下内部仍有一次 `byte[]` 拷贝，接受。
- `PipeReader Input` 暴露在 `StreamServerSocket` 的会话/`ChannelInfo`（定义于 `Src/SAEA.Sockets/Model/ChannelInfo.cs`，持有 `Socket`/`Stream`/`Expired`；由 `Core/ChannelManager.cs:Set(id, socket, stream)` 创建）上；`StreamClientSocket` 不新增 Input（无读循环，避免制造第二个真相源）。
- **现状保留**：`StreamServerSocket.SendAsync(string,byte[])` 当前 `channel.Stream.WriteAsync(...)` **未 await（fire-and-forget）**，`Send`/`End` 用同步 `Stream.Write`（`StreamServerSocket.cs:299/311/325`）；改 Memory 时保持该异步/同步分工，不引入新的时序语义。`StreamServerSocket.SessionManager` 抛 `NotImplementedException`（`:165`），不回归即可。
- `GetStream()` 与 `SocketStream` 保留（`Stream` 契约在 ns2.0 仅 `byte[]`），仅机械现代化。

### 5.4 UDP（`UdpClientSocket` / `UdpServerSocket`）

**位置：** `Src/SAEA.Sockets/Core/Udp/*`

- 补 Span 接收事件、删除 `dataSpan.ToArray()`。
- 发送补 Span/Memory 重载；ns2.0 无 `Socket.SendTo(Span)`，发送侧租池拷贝一次。

### 5.5 Shortcut

**位置：** `Src/SAEA.Sockets/Shortcut/TCPClient.cs`、`TCPServer.cs`、`UDPClient.cs`、`UDPServer.cs`

- 事件签名改 `ReadOnlyMemory<byte>` / span；`Send`/`SendAsync` 改 Span/Memory；泛型约束 `where Coder : class, ICoder` 不变。
- `BaseSocketProtocal.Parse(...).ToBytes()` 调用点 → `SendAsync(ISocketProtocal)`。

### 5.6 其他

- `SocketFactory` / `SocketOptionBuilder` / `SessionManager` / `UserTokenPool` / `UserTokenFactory` / `BaseUserToken` / `BaseContext`：随接口与所有权字段做接线调整。
- 删除死代码 `Src/SAEA.Sockets/Core/RioExtention.cs`。

---

## 六、P2P / relay 优化（`SAEA.P2P`）

**位置：** `Src/SAEA.P2P/Protocol/P2PCoder.cs`、`P2PProtocol.cs`、`Relay/RelayManager.cs`、`Relay/RelaySession.cs`、`Channel/TCPChannel.cs`、`Channel/UDPChannel.cs`、`Core/P2PClient.cs`、`Core/P2PServer.cs`、`Discovery/LocalDiscovery.cs`、`NAT/HolePuncher.cs`

- `Array.IndexOf` → `Span.IndexOf`。
- **解码路径** `Buffer.BlockCopy` 拼接 / `new byte[]` → `DecodedFrames` 池化切片（见 §4.6，7 处调用点改 `using` + `ISocketProtocal`）。
- `P2PCoder` 改 `DecodedFrames`；`EncodeP2P` 保留 `byte[]` 签名（内部 `PooledBufferWriter`）并新增 `IBufferWriter<byte>` 重载。编码侧 `new byte[]` 的完全消除为**非目标**（非验收热路径）。
- 保持协议语义与转发行为不变。

---

## 七、外部项目全量适配（rev.2：由 7 项目修正为全解决方案）

`SAEA.Sockets.sln` 共 35 个条目（含 2 个解决方案文件夹）。凡实现 `ICoder`/`ISocketProtocal` 或调用 `ToBytes()`/`Decode(byte[])` 的项目都必须改到编译通过。

| 项目 | 关键文件 | 说明 |
|------|----------|------|
| SAEA.FTP | `Net/FTPCoder.cs` | `Encode` 返回 `ToBytes()`；**另有额外重载 `Decode(byte[], Action<ISocketProtocal>, ...)` 须保留并适配** |
| SAEA.WebSocket | `Model/WSCoder.cs`、`Model/WSProtocal.cs`、`WSClient.cs`、`Core/WSServerImpl.cs`、`Core/WSSServerImpl.cs` | `ICoder` + `ISocketProtocal` 双实现；池化/掩码 |
| SAEA.QueueSocket | `Net/QueueCoder.cs` 及消费方 | `ICoder` |
| SAEA.RPC | `Net/RpcCoder.cs`、`Net/RServer.cs`、`Net/RClient.cs` | `ICoder` + `Encode` 返回 `byte[]` |
| SAEA.Http | `Base/Net/HttpCoder.cs`、`HttpResponse.cs`、`WebHost.cs`、`Base/Net/HttpSocket.cs`、`Base/Net/HttpSocketDebug.cs` | `ICoder`（`Decode` 已 `NotImplementedException`）；`HttpResponse.ToBytes()` 是**自有**方法（非 `ISocketProtocal.ToBytes()`），但 `WebHost.Send/End(IUserToken, byte[])` 与 `HttpSocket*` 的 `Send/End(...byte[])` 随服务端发送 API 改 Memory |
| SAEA.RedisSocket | `Base/Net/RedisCoder.cs` | `ICoder` |
| SAEA.Sockets.TcpTest | `JContext.cs`(`JUnpacker`)、`JServer.cs`、`JClient.cs`、`JClient2.cs`、`StreamServerSocketTests.cs` | `ICoder` + `Decode` 消费方；`StreamServerSocketTests` 直接 `new StreamServerSocket(...)` |
| SAEA.FileSocket | `Server.cs`、`Client.cs` | `Decode`、`Parse(...).ToBytes()`、`Send(byte[])` |
| SAEA.Audio.Net | `Net/TransferServer.cs`、`Net/TransferClient.cs` | `Decode`、`ParseRequest(...).ToBytes()` |
| SAEA.MessageSocket | `MessageServer.cs`、`MessageClient.cs` | `Parse(...).ToBytes()`、`Coder.Decode` |
| SAEA.MVC | 经 `SAEA.Http` 间接 | 随 Http 迁移 |
| SAEA.MQTT | `Implementations/MqttTcpChannel.cs` | **不实现 `ICoder`，但 `:84` 强转 `Core.Tcp.StreamClientSocket`**，受 Stream 发送/接收 API 变更影响 |
| SAEA.DNS | `Coder/UdpRequestCoder.cs` | **不实现 `ICoder`，但使用 `Shortcut.UDPClient`（`OnReceive`、`SendAsync(byte[])`）**，受 Shortcut 事件/发送变更影响 |
| SAEA.WebSocketTest | `Program.cs` | `coder.Decode(frame)`、`new WSProtocal(...)` |
| SAEA.Sockets.UdpTest | `Program.cs` | Shortcut `UDPServer<BaseCoder>` 事件 `Action<..., ISocketProtocal>`（`Content` 由 `byte[]` 改 `ReadOnlyMemory`）须适配 |
| SAEA.MVCTest / SAEA.FileTest / SAEA.MessageTest / SAEA.FTPTest / SAEA.HttpTest / SAEA.RPCTest / SAEA.QueueSocketTest / SAEA.RedisSocketTest / SAEA.Audio.Test | 各测试 | 随对应库签名编译 |

> **rev.3 更正（原「MQTT/DNS 范围外」有误）**：`SAEA.MQTT` 与 `SAEA.DNS` 虽不实现 `ICoder`/`ISocketProtocal`，但依赖被改动的 socket 公共面，**须纳入适配**：`SAEA.MQTT/Implementations/MqttTcpChannel.cs:84` 强转 `SAEA.Sockets.Core.Tcp.StreamClientSocket`；`SAEA.DNS/Coder/UdpRequestCoder.cs:92/98/111` 使用 `Shortcut.UDPClient` 的 `OnReceive` 与 `SendAsync(byte[])`。`SAEA.Socket5` 仅用 `System.Net.Sockets.UdpClient`，确属范围外。

---

## 八、测试、基准与验收

### 8.1 测试

- 现有 **255 项**全部迁移到新 API 并保持全绿；`StreamDecoderTest` 继续以 `LegacyDecoder`（旧字节算法副本）作 oracle 做 parity 比对。
- 已知需迁移的测试调用：`StreamDecoderTest` 全量 `Decode(byte[])`/`ToBytes()`、`IocpBenchmark.cs:77/125` 的 `decoder.Decode(data).Count`、`PerformanceTest` 的 `Decode(...)`+`new BaseSocketProtocal{...}.ToBytes()`、`ProtocolTest`/`ProtocolAdvancedTest` 的 `DecodeP2P(...)` 返回 `List<P2PProtocol>` + `BaseCoder.GetLength(new byte[])`、`WebSocketTest/Program.cs` 的 `coder.Decode(frame)`。
- 新增：
  - `DecodedFrames` 生命周期：切片正确、多次 `Dispose` 幂等、租用归还到池。
  - `PooledBufferWriter` 单元测试：`GetSpan`/`Advance`/扩容/`TryGetArray`/`Dispose`。
  - 多段 `ReadOnlySequence` 拆帧。
  - Stream `PipeReader` 端到端（服务端）。
  - UDP span 事件。
  - 外部项目至少编译通过（有测试工程的跑测试）。

### 8.2 分配断言

- 用 `GC.GetTotalAllocatedBytes` / `GC.CollectionCount` 断言接收路径 **B/frame < 100 且 Gen0 增量 = 0**。
- **测量口径（rev.2 澄清）：** 断言对象是 **`BaseCoder.DecodeStream` 消费路径**（`IocpBenchmark` 改为用 `DecodeStream` + `IFrameHandler` 驱动），不是裸 socket 字节投递；socket 层已零分配。

### 8.3 基准（扩展 `IocpBenchmark`）

- 保留 benchmark 专用 legacy 切片（`LegacyDecoder` + 旧发送）作对照。
- 新路径改用 `DecodeStream` 逐帧消费；旧路径沿用 legacy oracle。
- 指标：f/s、MiB/s、B/frame、GC0/1/2。
- 目标：新路径 **B/frame < 100**、**GC0 增量 0**、**吞吐不退化（±5% 容差）**。

### 8.4 验收（DoD）

1. `dotnet build Src/SAEA.Sockets.sln -c Release` → 0 error（覆盖全部 35 个条目）。
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
| `new BaseSocketProtocal { BodyLength=, Type=, Content= }` | `new BaseSocketProtocal(type, content)` / `(bodyLength, type, content)` | 对象初始化器 → 构造函数（成员改 `protected set`） |
| `BaseSocketProtocal` 可被继承且成员可写 | 仍可继承；成员 `protected set` | `P2PProtocol` 改经 base ctor |
| `byte[] ICoder.Encode(ISocketProtocal)` | `void Encode(ISocketProtocal, IBufferWriter<byte>)` | 预租 writer；或 `SendAsync(ISocketProtocal)` |
| `List<ISocketProtocal> ICoder.Decode(byte[])` | `DecodedFrames Decode(ReadOnlySequence<byte>)` | `using` 包裹，循环索引 |
| `ICoder.Decode(ReadOnlySpan)`（有状态） | `IFrameCoder.DecodeStream(ReadOnlySpan, IFrameHandler, ...)` | 帧式消费方改用 `IFrameCoder` |
| `Action<byte[]> onFile` | `Action<ReadOnlySpan<byte>>`（stream）/`Action<ReadOnlyMemory<byte>>`（batch） | 回调内用或复制 |
| `event ... OnReceive(byte[])` | `OnClientReceiveSpan`/`OnServerReceiveSpan(ReadOnlySpan<byte>)` | 回调内用或复制 |
| `void Send/BeginSend/SendAsync(byte[]...)` | `Send(ReadOnlySpan<byte>)`/`SendAsync(ReadOnlyMemory<byte>)`/`SendAsync(ISocketProtocal)` | 见 §5.1 清单 |
| `WSProtocal.ToBytes()` / `ToBytes(bool)` | `WriteTo(IBufferWriter)`（不掩码）/ `WriteMaskedTo(IBufferWriter)`（掩码）+ 私有 `byte[] _buffer` | 掩码写入 writer，**不原地改 `Content`**；客户端用 `WriteMaskedTo`、服务端 `Reply` 用 `WriteTo` |
| `List<P2PProtocol> P2PCoder.DecodeP2P(...)` | `DecodedFrames P2PCoder.DecodeP2P(...)` | 7 处调用点 `using` + `foreach(frames.Frames)`；`ProcessMessage`/`ProcessSignalMessage` 参数改 `ISocketProtocal`；`P2PProtocol` 仅保留编码用途 |

- 属**破坏性发布**，README 增加「byte[] → Span/Memory 迁移对照」小节。
- 不发布 nupkg。
- 仓库既有「`.cs` 文件头版本号 `v26.4.23.1` 未同步」属已知遗留，本次仅 bump `.csproj` 版本（14 个库，不含测试项目）。

---

## 十、风险与缓解

| 风险 | 缓解 |
|------|------|
| ns2.0 API 可用性假设（`PipeReader.Create(Stream)`、`IBufferWriter`、`ArrayBufferWriter`、`Encoding.GetString(ReadOnlySpan)`） | 步骤 1 先实测；`PooledBufferWriter` 自建兜底；已知 `GetString(ReadOnlySpan)` 须 `ToArray()` |
| Stream 读循环重写是行为风险最高处 | 仅服务端；`StreamDecoderTest` + TCP 端到端 + parity 兜底；不动 `StreamClientSocket` |
| 破坏面覆盖全解决方案（35 条目） | 交付顺序把「外部实现全量适配恢复编译」设为独立一步，`SAEA.Sockets.sln` 编译门禁 |
| `WSProtocal` 池化 + 掩码语义被改坏 | 保留背衬 `byte[]`/`IsPooled`；掩码只发生在 `WriteTo`；WebSocket 测试 |
| `DecodedFrames` 生命周期误用 | `IDisposable` + XML 注释 + 专门生命周期测试；**无终结器，泄漏静默**，迁移文档显式警示 |
| 批次池化缓冲长期驻留 | `Dispose` 归还；容量按帧体总长、不无限增长 |
| `TryGetArray` 失败的发送路径产生拷贝 | 接受一次池化拷贝；基准断言 B/frame 仍 < 100 |
| `Content` 切片指向池化缓冲，批次 `Dispose` 后失效 | XML 注释 + 迁移小节显式写明有效期 |
| 池化缓冲归还后仍被引用（use-after-free） | 测试覆盖 + 文档契约 + `Clear`/`Dispose` 幂等 |
| socket 不调用 coder，`DecodeStream` 主路径依赖消费方改造 | DoD/基准显式以 `DecodeStream` 消费路径为测量口径 |

---

## 十一、修改文件清单

**新增**

| 文件 | 说明 |
|------|------|
| `Src/SAEA.Common/Caching/PooledBufferWriter.cs` | `IBufferWriter<byte>` 池化写入器 |
| `Src/SAEA.Sockets/Base/DecodedFrames.cs` | `IDisposable` 帧批次 |
| `Src/SAEA.Sockets/Interface/IFrameCoder.cs` | 帧式编解码器接口（rev.2） |
| `Src/SAEA.P2PTest/Tests/SpanPipelineTest.cs` | 新增功能/生命周期测试 |

**改（`SAEA.Sockets`）**

`Interface/ISocketProtocal.cs`、`Interface/ICoder.cs`、`Base/BaseSocketProtocal.cs`、`Base/BaseCoder.cs`、`Base/FrameDecoder.cs`、`IClientSocket.cs`、`IServerSocket.cs`、`Handler/*`、`Core/Tcp/IocpClientSocket.cs`、`Core/Tcp/IocpServerSocket.cs`、`Core/Tcp/StreamClientSocket.cs`、`Core/Tcp/StreamServerSocket.cs`、`Core/SocketStream.cs`、`Core/Udp/UdpClientSocket.cs`、`Core/Udp/UdpServerSocket.cs`、`Core/ChannelManager.cs`、`Model/ChannelInfo.cs`、`Core/BaseUserToken.cs`、`Core/UserTokenPool.cs`、`Core/UserTokenFactory.cs`、`Core/SessionManager.cs`、`Base/BaseContext.cs`、`Shortcut/TCPClient.cs`、`Shortcut/TCPServer.cs`、`Shortcut/UDPClient.cs`、`Shortcut/UDPServer.cs`、`SocketFactory.cs`、`SocketOptionBuilder.cs`

**删除**

`Src/SAEA.Sockets/Core/RioExtention.cs`

**改（其他库，rev.2 扩展）**

`Src/SAEA.P2P/Protocol/P2PCoder.cs`、`Protocol/P2PProtocol.cs`、`Relay/RelayManager.cs`、`Relay/RelaySession.cs`、`Channel/TCPChannel.cs`、`Channel/UDPChannel.cs`、`Core/P2PClient.cs`、`Core/P2PServer.cs`、`Discovery/LocalDiscovery.cs`、`NAT/HolePuncher.cs`、`Src/SAEA.FTP/Net/FTPCoder.cs`、`Src/SAEA.WebSocket/Model/WSCoder.cs`、`Model/WSProtocal.cs`、`WSClient.cs`、`Core/WSServerImpl.cs`、`Core/WSSServerImpl.cs`、`Src/SAEA.QueueSocket/Net/QueueCoder.cs`、`Src/SAEA.RPC/Net/RpcCoder.cs`、`Net/RServer.cs`、`Net/RClient.cs`、`Src/SAEA.Http/Base/Net/HttpCoder.cs`、`HttpResponse.cs`、`WebHost.cs`、`Base/Net/HttpSocket.cs`、`Base/Net/HttpSocketDebug.cs`、`Src/SAEA.RedisSocket/Base/Net/RedisCoder.cs`、`Src/SAEA.Sockets.TcpTest/JContext.cs`、`JServer.cs`、`JClient.cs`、`JClient2.cs`、`Src/SAEA.FileSocket/Server.cs`、`Client.cs`、`Src/SAEA.Audio.Net/Net/TransferServer.cs`、`Net/TransferClient.cs`、`Src/SAEA.MessageSocket/MessageServer.cs`、`MessageClient.cs`、`Src/SAEA.Sockets.UdpTest/Program.cs`、`Src/SAEA.WebSocketTest/Program.cs`、`Src/SAEA.Sockets.TcpTest/StreamServerSocketTests.cs`、`Src/SAEA.MQTT/Implementations/MqttTcpChannel.cs`、`Src/SAEA.DNS/Coder/UdpRequestCoder.cs`

**改（测试/文档/版本）**

`Src/SAEA.P2PTest/Tests/*`（含 `IocpBenchmark.cs`、`StreamDecoderTest.cs`、`PerformanceTest.cs`、`ProtocolTest.cs`、`ProtocolAdvancedTest.cs`）、`Src/SAEA.P2PTest/TestHarness.cs`、`Program.cs`、`README.md`、`README.en.md`、14 个库 `.csproj` 版本号

---

## 十二、实施顺序

1. `SAEA.Common` `PooledBufferWriter` + 单元测试（并实测 ns2.0 API 可用性，含 `Encoding.GetString(ReadOnlySpan)` 须 `ToArray()`）。
2. 协议/编解码：`ISocketProtocal`/`ICoder`/`IFrameCoder`/`BaseSocketProtocal`/`BaseCoder`/`DecodedFrames` + 测试。
3. 外部 `ICoder`/`ISocketProtocal` 实现与所有 `ToBytes()`/`Decode(byte[])` 调用方**全量适配**（恢复 `SAEA.Sockets.sln` 可编译）。
4. `IClientSocket`/`IServerSocket`/Handler/`IUserToken` 接口现代化。
5. IOCP 客户端/服务端实现 + 基准。
6. Stream（服务端 `PipeReader`）+ UDP 实现 + 测试。
7. Shortcut 适配。
8. P2P/relay 优化 + 测试。
9. 文档/版本 bump/最终验证（`--all` + Release build + `IocpBenchmark`）。

---

## 十三、预期收益

- 公共面彻底 `byte[]` 退出，上一轮零拷贝内核真正生效；接收路径 B/frame < 100、Gen0 不增长。
- `SendAsync(ISocketProtocal)` 编码 + 发送合并为一次池化写、零中间分配。
- Stream 服务端读循环基于 `PipeReader`，多段数据零拷贝拆帧。
- P2P/relay 去掉逐字节拼接与 `new byte[]`。
- 单个 spec 覆盖全解决方案，全量适配有编译门禁兜底。