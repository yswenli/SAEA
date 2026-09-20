# SAEA.Sockets 流式零拷贝解码器设计文档

**日期：** 2026-09-20
**范围：** `SAEA.Sockets`（解码器内核 + TCP IOCP 接收热路径），测试与基准落在 `SAEA.P2PTest`
**方案：** 方案 A —— 增量式池化累加器 + `SequenceReader`，可加（additive）API

---

## 概述

当前 `BaseCoder` 的解码并非零拷贝、也非流式：

- 接收路径先把 socket 缓冲 `ToArray()`（`IocpClientSocket.cs:452`、`IocpServerSocket.cs:335`）。
- `BaseCoder.Decode` 再把入参 `data.ToArray()` 写入 `MemoryStream`（`BaseCoder.cs:94`）。
- 每消费一帧通过 `RemoveFromBuffer` 把剩余字节整体重拷（`BaseCoder.cs:246-260`），小包多帧时近似 O(n²)。
- payload 恒为 `new byte[count]`，大数据还多一次池化中转拷贝（`BaseCoder.cs:186/213/214`），且大帧分配后又立即复制，池化收益被抵消。

本设计引入增量式 `FrameDecoder` 内核，并新增可加 API：

1. 用 `ArrayPool` 池化累加器 + head/tail 索引替换 `MemoryStream`，消除逐帧剩余数据重拷（摊还 O(1)）。
2. 新增 `Decode(ReadOnlySequence<byte>, ...)`：直接在只读序列上解析，无中间缓冲、无 `MemoryStream`。
3. 新增 `DecodeStream(ReadOnlySpan<byte>, IFrameHandler, ...)`：真正零拷贝，payload 切片仅在回调期间有效。
4. 现有 `Decode(byte[])` / `Decode(ReadOnlySpan<byte>)` / `ICoder` / `ISocketProtocal` / `byte[] Content` **全部保留**，内部改由新内核实现，公共签名与可观察行为不变。
5. TCP IOCP 收发路径暴露可加的 Span 事件；当无 `byte[]` 订阅者时跳过 `ToArray()` 分配。

**兼容性：** 纯增量；不破坏任何现有公共契约与调用方。
**非目标：** UDP/Stream/P2P 接收路径接线、`System.IO.Pipelines` 重写、把公共协议 payload 改为 `Memory<byte>`。

---

## 一、关键事实核对（实现前已验证）

| 事实 | 结论 / 出处 | 对设计的影响 |
|------|------------|-------------|
| 框架约束 | `SAEA.Sockets` = netstandard2.0 / C# 8，引用 `System.IO.Pipelines 10.0.6`（含 `System.Memory`） | `ReadOnlySequence<byte>`、`SequenceReader<byte>`、`BinaryPrimitives` 可用 |
| BaseCoder 派生类 | 仅 `P2PCoder : BaseCoder`（`P2PCoder.cs:40`），只调用 `Decode`，无重写 | 替换内部实现安全 |
| 其他 ICoder | `WSCoder`/`HttpCoder`/`FTPCoder` 各自独立实现 `ICoder` | 不受影响 |
| `ICoder` 契约 | 仅声明 `Decode(byte[])`、`Encode`、`Clear()`（`ICoder.cs:40-62`） | 新增 span/sequence 方法放 `BaseCoder`，不改接口 |
| `_buffer` 可见性 | `BaseCoder._buffer` 为 `private`（`BaseCoder.cs:62`） | 可安全替换为累加器 |
| `Clear()` 调用点 | `BaseUserToken.Clear()`（`BaseUserToken.cs:87`）、`UserTokenPool.cs:159`、socket 断开处 | 累加器归还池化缓冲的挂点 |
| 线格式 | 8 字节长度（`BitConverter`，小端）+ 1 字节 Type + body；`P_LEN=8`、`P_Type=1`、`P_Head=9` | 解析必须与 `ToLong` 一致 |
| 心跳语义 | `bodyLen==0 && type==Heart` → 消费 9 字节、回调 `onHeart`、**不加入结果**（`BaseCoder.cs:110-115`） | 必须逐字保持 |
| 空 body 语义 | 普通帧 `bodyLen==0` → `Content = Array.Empty<byte>()`（**非 null**）（`BaseCoder.cs:179`） | 必须逐字保持 |
| BigData 语义 | `type==BigData` → `onFile(content)`，**不加入结果**（`BaseCoder.cs:118-123`） | 必须逐字保持 |
| IOCP 客户端 | `OnClientReceiveSpan` 为 internal 且无订阅者；真实订阅在 `OnReceive`（`IocpClientSocket.cs:145-150/449/456`） | 需公开并接线 |
| IOCP 服务端 | 无 Span 事件；`OnReceiveBytes` 经 `OnServerReceiveBytes` 委托调用（`IocpServerSocket.cs:261-264/337`） | 需新增事件 |
| 测试工程 | `SAEA.P2PTest` 直接引用 `SAEA.Sockets`（`SAEA.P2PTest.csproj:10`） | 可直接对 `BaseCoder` 做单测与基准 |

---

## 二、架构与新增组件

```
┌──────────────────────────────────────────────────────────────┐
│                       BaseCoder (改)                          │
│  Decode(byte[]) / Decode(ReadOnlySpan)  ← 旧签名，内部走新内核 │
│  Decode(ReadOnlySequence) ──────────────► 无状态解析           │
│  DecodeStream(ReadOnlySpan, IFrameHandler) ─► 零拷贝流式解析   │
│         │                                                     │
│         └──► FrameDecoder (新增，池化累加器 + SequenceReader)  │
└──────────────────────────────────────────────────────────────┘
```

### 2.1 `SocketFrame`（新增，readonly ref struct）

**位置：** `SAEA.Sockets/Base/SocketFrame.cs`

```csharp
namespace SAEA.Sockets.Base
{
    /// <summary>
    /// 单帧视图。Content 仅在 IFrameHandler.OnFrame 回调期间有效。
    /// </summary>
    public readonly ref struct SocketFrame
    {
        public readonly long BodyLength;
        public readonly byte Type;
        public readonly ReadOnlySpan<byte> Content;

        public SocketFrame(long bodyLength, byte type, ReadOnlySpan<byte> content);
    }
}
```

### 2.2 `IFrameHandler`（新增）

**位置：** `SAEA.Sockets/Interface/IFrameHandler.cs`

```csharp
namespace SAEA.Sockets.Interface
{
    public interface IFrameHandler
    {
        /// <summary>
        /// 收到完整数据帧。frame.Content 仅在本次调用期间有效。
        /// </summary>
        void OnFrame(in SocketFrame frame);
    }
}
```

> 说明：`in SocketFrame` 的 `ref struct` 参数在 C# 7.2+ 合法，且可避免额外拷贝。

### 2.3 `FrameDecoder`（新增，内核）

**位置：** `SAEA.Sockets/Base/FrameDecoder.cs`

**职责：** 增量拆帧；维护跨调用的未消费字节，输出完整帧。

**内部状态：**
- `byte[] _buffer`：`ArrayPool<byte>` 租用，容量按需倍增（1KB 起）。
- `int _start` / `int _end`：有效数据的起止索引（`_start` 为已消费头，`_end` 为已写入尾）。
- `int _maxFrameLength`：单帧最大 body 长度，默认 `int.MaxValue - P_Head`。

**API：**

```csharp
internal sealed class FrameDecoder
{
    public void Append(ReadOnlySpan<byte> data);        // 写入并压缩（按需），唯一一次复制
    public bool TryReadFrame(out SocketFrame frame, out DateTime? heartAt, out byte[] fileContent);
    public void Clear();                                // 归还池化缓冲并复位
}
```

> 上述为内部形态，允许实现时以更贴近调用方的签名落地；公共 API 以 `BaseCoder` 为准。

**压缩策略：**
- `TryReadFrame` 消费 `n` 字节时仅 `_start += n`。
- 当 `_start == _end` 时复位为 0；当 `_start > 0` 且剩余空间不足以容纳下一次写入（或 `_start >= 容量/2`）时，将 `[_start, _end)` 搬移到 0。
- 相比旧 `RemoveFromBuffer` 的「每帧重拷剩余」，仅在必要时搬移，摊还 O(1)。

**解析规则（与旧实现逐字对齐）：**
1. 若 `_end - _start < P_Head` → 返回 false（半包）。
2. 读 8 字节小端长度 → `bodyLen`；`_start + 1` 处读 type。
3. 校验：`bodyLen < 0 || bodyLen > _maxFrameLength` → 抛 `KernelException($"非法的数据帧长度: {bodyLen}")`（见 §5 行为变更说明）。
4. `bodyLen == 0 && type == Heart` → 消费 `P_Head`，置 `heartAt = DateTimeHelper.Now`，返回 true（不产出帧）。
5. `type == BigData` → 若 `_end - _start < P_Head + bodyLen` 返回 false；否则 `fileContent = 拷贝(_start+P_Head, bodyLen)`，消费 `P_Head+bodyLen`，返回 true（不产出帧）。
6. 普通帧 → 若不足则返回 false；否则 `frame = new SocketFrame(bodyLen, type, _buffer.AsSpan(_start+P_Head, (int)bodyLen))`，消费 `P_Head+bodyLen`，返回 true。**注意：`frame.Content` 直接指向累加器数组，回调返回后失效。**

---

## 三、BaseCoder 改造（可加 API）

**位置：** `SAEA.Sockets/Base/BaseCoder.cs`（保留常量与静态方法）

### 3.1 保留（签名与可观察行为不变）

- `List<ISocketProtocal> Decode(byte[] data, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)`
- `List<ISocketProtocal> Decode(ReadOnlySpan<byte> data, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)`
- `static long GetLength / GetType / GetContent`、`Clear()`、`Encode()`

内部实现改为驱动 `FrameDecoder`；产出帧的 `Content` 仍为精确大小 `byte[]`（空 body 为 `Array.Empty<byte>()`，非 null），心跳/BigData 语义完全一致。

### 3.2 新增

```csharp
// 无状态：直接在只读序列上解析（支持多段），payload 物化为精确 byte[]
public List<ISocketProtocal> Decode(
    ReadOnlySequence<byte> data,
    Action<DateTime> onHeart = null,
    Action<byte[]> onFile = null);

// 零拷贝流式：payload 以切片交付，仅在 OnFrame 回调期间有效
public void DecodeStream(
    ReadOnlySpan<byte> data,
    IFrameHandler handler,
    Action<DateTime> onHeart = null,
    Action<byte[]> onFile = null);
```

- `Decode(ReadOnlySequence)` 不经过累加器：用 `SequenceReader<byte>` 沿序列读取 header，`reader.Sequence.Slice(...).ToArray()`（`BuffersExtensions.ToArray`）物化 payload，`reader.Advance` 前进。无 `MemoryStream`、无中间 `ToArray`。
- `DecodeStream` 使用 per-instance `FrameDecoder`：`Append(data)` 后循环 `TryReadFrame`，命中普通帧时调用 `handler.OnFrame(in frame)`，命中心跳调用 `onHeart`，命中 BigData 以 `byte[]` 调用 `onFile`。该方法不产生 `List`、不做 payload 物化。

### 3.3 `BaseCoder.OnReceiveSpan` 内部事件

保留现有 `internal event OnReceiveSpanHandler OnReceiveSpan`；在 `Decode(byte[])` / `Decode(ReadOnlySpan)` / `DecodeStream` 入口处按现状 `OnReceiveSpan?.Invoke(data)`（`data` 为入参 Span），维持内部语义。`Decode(ReadOnlySequence)` 不触发该事件（序列无连续 Span，且该事件当前无订阅者）。

### 3.4 `MaxFrameLength`

新增 `public static int MaxFrameLength { get; set; } = int.MaxValue - P_Head;`，供部署方按需收紧；`FrameDecoder` 读取该值。

---

## 四、TCP IOCP 热路径接线

### 4.1 新增公共 Span 事件

- `SAEA.Sockets/Handler/OnServerReceiveSpanHandler.cs`（新增）
  ```csharp
  public delegate void OnServerReceiveSpanHandler(IUserToken userToken, ReadOnlySpan<byte> data);
  ```
- `SAEA.Sockets/Handler/OnClientReceiveSpanHandler.cs`（新增）
  ```csharp
  public delegate void OnClientReceiveSpanHandler(ReadOnlySpan<byte> data);
  ```
  > 实现时删除 `IocpClientSocket` 内嵌的 internal 委托，统一改用公共 `OnClientReceiveSpanHandler`，避免与公共委托重名。

- `IocpServerSocket` 新增 `public event OnServerReceiveSpanHandler OnServerReceiveSpan;`
- `IocpClientSocket` 将 `OnClientReceiveSpan` 提升为 `public event OnClientReceiveSpanHandler OnClientReceiveSpan;`

**契约：** 事件参数 `data` 仅在事件回调期间有效，订阅者需自行复制以保留。

### 4.2 接收路径改动

**`IocpClientSocket.ProcessReceived`（`:433-466`）：**

```csharp
var dataSpan = readArgs.Buffer.AsSpan(readArgs.Offset, readArgs.BytesTransferred);

// 零拷贝路径：始终触发（无分配）
OnClientReceiveSpan?.Invoke(dataSpan);

// 兼容路径：仅在有 byte[] 消费方时分配
if (_deliverBytesToLegacyPath)
    OnClientReceive?.Invoke(dataSpan.ToArray());
```

**`IocpServerSocket.ProcessReceived`（`:323-345`）：**

```csharp
var dataSpan = readArgs.Buffer.AsSpan(readArgs.Offset, readArgs.BytesTransferred);

OnServerReceiveSpan?.Invoke(userToken, dataSpan);

if (_deliverBytesToLegacyPath)
    OnServerReceiveBytes.Invoke(userToken, dataSpan.ToArray());
```

**`_deliverBytesToLegacyPath` 安全判定（保守，避免破坏子类）：**
- `IocpClientSocket` 构造函数一次性计算：`_deliverBytesToLegacyPath = GetType() != typeof(IocpClientSocket) || OnReceive != null;`
- `IocpServerSocket` 构造函数一次性计算：`_deliverBytesToLegacyPath = GetType() != typeof(IocpServerSocket) || OnReceive != null;`
- 语义：仅当「类型恰为本类且无公共 `OnReceive` 订阅者」时才跳过 `ToArray()`；任何子类或被订阅场景都保持旧行为原样。
- 由于 `OnClientReceive` 在构造时绑定到可重写的 `OnReceived`，子类重写场景必须继续收到 `byte[]`，故对子类一律走兼容路径。

### 4.3 不改动

- `IocpServerSocket.OnReceiveBytes` / `IocpClientSocket.OnReceived` 的 `byte[]` 虚方法保持存在与语义。
- `IServerSocket` / `IClientSocket` 接口不变（事件加在具体类上，可加式）。
- UDP、Stream、P2P 接收路径本轮不动。

---

## 五、行为变更与边界处理

| 场景 | 旧行为 | 新行为 | 说明 |
|------|--------|--------|------|
| 半包 | 缓存等待 | 缓存等待 | 一致 |
| 粘包/多帧 | 循环解析 | 循环解析 | 一致 |
| 心跳（0 body + Heart） | 消费不产出 | 消费不产出 | 一致 |
| 空 body 普通帧 | `Content = Array.Empty<byte>()` | 同 | 一致 |
| BigData | `onFile(content)` | 同 | 一致 |
| `bodyLen < 0` | 强转/错位消费（未定义） | 抛 `KernelException` | **有意的健壮性修正** |
| `bodyLen > int.MaxValue` | 长整型比较下永不满足（挂起） | 抛 `KernelException` | **有意的健壮性修正** |
| `bodyLen > MaxFrameLength`（默认 `int.MaxValue-P_Head`） | 不适用 | 抛 `KernelException` | 可配置上限 |

- **零拷贝契约：** `SocketFrame.Content` 与 Span 事件参数仅在对应回调期间有效，回调返回后不得引用。XML 注释与 README 显式写明。
- **线程模型：** `BaseCoder` / `FrameDecoder` 仍按 per-session 单线程使用（与现状一致），不引入跨线程共享。
- **资源释放：** `Clear()` 归还池化缓冲并复位索引；会话回收（`BaseUserToken.Clear()` / `UserTokenPool` / 断开）会触发。

---

## 六、测试与基准（SAEA.P2PTest）

### 6.1 功能测试（新增 `Tests/StreamDecoderTest.cs`）

| 用例 | 内容 |
|------|------|
| `PartialFrameTest` | 逐字节喂入，仅完整后产出 |
| `FragmentedAcrossAppendsTest` | 一帧跨多次 `Append` |
| `MultipleFramesSingleAppendTest` | 单次含多帧（粘包） |
| `HeartbeatTest` | 心跳触发 `onHeart` 且不产出 |
| `EmptyBodyTest` | 空 body 产出非 null 空数组 |
| `BigDataTest` | 触发 `onFile` 且不产出 |
| `LargeFrameTest` | 1MB 帧内容逐字节一致 |
| `MultiSegmentSequenceTest` | `Decode(ReadOnlySequence)` 对多段序列解析正确 |
| `MalformedLengthTest` | 负长度/超 `MaxFrameLength` 抛 `KernelException` |
| `ZeroCopyLifetimeTest` | `DecodeStream` 切片内容正确；回调内复制后仍有效 |
| `ParityTest` | 对输入矩阵，新 `Decode` 与 `LegacyDecoder`（旧算法副本）结果逐字节一致 |

### 6.2 IOCP 端到端

- 现有 `--all`（189 项）必须全绿。
- 新增一条 Span 路径用例（订阅 `OnClientReceiveSpan`/`OnServerReceiveSpan`，用 `DecodeStream` 解码并校验）。
- 兼容用例：仅订阅 `OnReceive` 的旧消费方行为不变。

### 6.3 基准（扩展 `Tests/PerformanceTest.cs`）

新增「流式零拷贝解码」章节，输出 `ops/sec` 与 `GC.GetAllocatedBytesForCurrentThread()` 的每操作分配：

| 对比组 | 负载 | 指标 |
|--------|------|------|
| `LegacyDecoder`（测试内置旧算法副本） | 64B / 4KB / 1MB | ops/sec、alloc/op |
| `BaseCoder.Decode(byte[])`（新内核） | 同上 | ops/sec、alloc/op |
| `BaseCoder.DecodeStream`（零拷贝） | 同上 | ops/sec、alloc/op |

- 旧算法副本 `LegacyDecoder` 放在测试工程内，避免在发布代码中保留旧实现。
- 计时沿用现有 ConsoleHelper 异步队列约定；测量段前后 `Task.Delay(~500)` 刷队列。

---

## 七、兼容性与风险

| 风险 | 缓解 |
|------|------|
| 切片生命周期误用 | 回调式 API + `in` 参数 + XML 注释 + 专门生命周期测试 |
| 新旧双路径维护 | 旧签名内部委托同一内核，减少分叉 |
| 子类依赖 `byte[]` 回调 | 4.2 保守判定：子类一律走旧路径 |
| 池化缓冲长期驻留 | `Clear()` 归还；累加器按需增长，空闲时不额外占用 |
| 非法长度改为抛异常 | 仅在畸形输入触发；`MaxFrameLength` 默认不误伤合法帧；Parity/Malformed 测试覆盖 |
| `ReadOnlySequence` 多段读取正确性 | `SequenceReader` 覆盖，另加多段序列测试 |

---

## 八、修改文件清单

| 文件 | 类型 |
|------|------|
| `Src/SAEA.Sockets/Base/FrameDecoder.cs` | 新增 |
| `Src/SAEA.Sockets/Base/SocketFrame.cs` | 新增 |
| `Src/SAEA.Sockets/Interface/IFrameHandler.cs` | 新增 |
| `Src/SAEA.Sockets/Handler/OnServerReceiveSpanHandler.cs` | 新增 |
| `Src/SAEA.Sockets/Handler/OnClientReceiveSpanHandler.cs` | 新增 |
| `Src/SAEA.Sockets/Base/BaseCoder.cs` | 改（内部内核 + 可加 API） |
| `Src/SAEA.Sockets/Core/Tcp/IocpClientSocket.cs` | 改（Span 事件提升/接线） |
| `Src/SAEA.Sockets/Core/Tcp/IocpServerSocket.cs` | 改（新增 Span 事件/接线） |
| `Src/SAEA.P2PTest/Tests/StreamDecoderTest.cs` | 新增 |
| `Src/SAEA.P2PTest/Tests/PerformanceTest.cs` | 改（零拷贝基准） |
| `Src/SAEA.P2PTest/TestHarness.cs` / `Program.cs` | 改（挂载新测试） |

---

## 九、实施顺序

1. `FrameDecoder` + `SocketFrame` + `IFrameHandler` 内核。  
2. `BaseCoder` 内部替换 + 新增 `Decode(ReadOnlySequence)` / `DecodeStream`，跑 Parity 测试。  
3. IOCP 客户/服务端 Span 事件与保守跳过 `ToArray()`。  
4. `StreamDecoderTest` 功能与端到端用例。  
5. `PerformanceTest` 旧/新/零拷贝基准。  
6. 全量 `--all` 回归（≥189 项全绿）。

---

## 十、预期收益

- 解码路径消除 `MemoryStream` 读写与逐帧剩余数据重拷，小包多帧场景由近似 O(n²) 降为摊还 O(1)。
- `DecodeStream` 在 payload 边界实现零拷贝；`Decode(ReadOnlySequence)` 消除入参 `ToArray`。
- 无 `byte[]` 订阅者的 IOCP 路径消除一次接收分配。
- 旧公共 API 与可观察行为不变，所有现有消费方无感受益。