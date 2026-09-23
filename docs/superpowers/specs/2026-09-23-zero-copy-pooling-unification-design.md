# 全仓网络热路径零拷贝池化统一设计

- 日期：2026-09-23
- 范围：所有引用 `SAEA.Sockets` 的工程 + `SAEA.Sockets` / `SAEA.Common` 网络原语
- 方案：复用 `MemoryPoolManager` 分层池 + 附加式 owner 发送重载 + 全链路切片化
- 关联：`2026-04-18-memory-pool-performance-optimization-design.md`、`2026-09-20-readonly-sequence-zero-copy-decoder-design.md`、`2026-09-21-span-memory-pipeline-design.md`、`2026-09-22-queuesocket-3-party-bench-design.md`

## 摘要

本设计在 `2026-09-21-span-memory-pipeline-design.md` 已落地的「Span/Memory 公共面 + `PooledBufferWriter` + `SendingOwner` + `MemoryPoolManager`」基座之上，做**池化落地 pass**：把网络收发热路径上的 `new byte[]` 与隐式 `ToArray()` 替换为分层池租借/归还，并把输入/输出参数尽可能切片化，最终使 QServer/其他 16 个工程在持续流量下**不产生逐帧 GC 分配**。

旗舰靶点仍是 `Src/SAEA.QueueSocket`（QServer 分发拼接的 LOH 分配），其余工程按统一契约分期接入。

---

## 一、关键事实核对

以下均为本轮实际读取源码核对结论（含行号，行号可能随改动漂移）。

### 1.1 池化基座（已存在，直接复用）

| 类型 | 位置 | 关键事实 |
| --- | --- | --- |
| `MemoryPoolManager` | `Src/SAEA.Common/Caching/MemoryPoolManager.cs` | 静态分层池。`SmallThreshold=4KB`、`MediumThreshold=64KB`、`LargeThreshold=1MB`；Small=`ArrayPool<byte>.Shared`，Medium=`Create(64KB,100)`，Large=`Create(1MB,50)`。`Rent(int)`、`Return(byte[], int originalSize=-1)`、`RentPooled(int)`、`GetStatistics()`。`Return` 无 `originalSize` 时按 `buffer.Length` 推断层级。 |
| `PooledBuffer` | `.../Caching/PooledBuffer.cs` | `sealed : IDisposable`。持 `byte[] Buffer`、`int Length`（有效数据长度）、`int Capacity`、`BufferSizeTier Tier`、私有 `ArrayPool<byte> _pool`。`AsSpan()`/`AsMemory()` 返回 `Length` 范围。`Dispose()` 按 `_pool` 正确归还，幂等。 |
| `PooledBufferWriter` | `.../Caching/PooledBufferWriter.cs` | `sealed : IBufferWriter<byte>, IDisposable`。ctor(`initialCapacity=4096`) 从 `MemoryPoolManager.Rent` 租借；`WrittenCount/WrittenSpan/WrittenMemory`、`Advance`、`GetMemory/GetSpan`、`TryGetArray(out ArraySegment)`（供 `SetBuffer` 零拷贝直发）、`Clear()`（复位不归还）；`Dispose()` 用 `_requestedSize` 归还（层级正确），幂等。非线程安全。 |
| `DecodedFrames` | `Src/SAEA.Sockets/Base/FrameDecoder.cs` 等 | 既有 ArrayPool 解码缓冲（基于 `MemoryPoolManager`）。 |
| `IUserToken.SendingOwner` | `Src/SAEA.Sockets/Interface/IUserToken.cs:99` | `IDisposable SendingOwner { get; set; }` 与 `TakeSendingOwner()`（`:105`）——发送缓冲所有权载体已就绪。 |

### 1.2 发送面现状（缺口 = 本次要补的）

| 接口/类型 | 位置 | 关键事实 |
| --- | --- | --- |
| `IServerSocket` | `Src/SAEA.Sockets/IServerSocket.cs:44` | 已有 `Send(string, ReadOnlySpan<byte>)`、`SendAsync(string, ReadOnlyMemory<byte>)`、`SendAsync(string, ISocketProtocal)`、`SendAsync(IPEndPoint, ReadOnlyMemory<byte>)`；**缺 owner 重载**。 |
| `IClientSocket` | `Src/SAEA.Sockets/IClientSocket.cs:47` | 已有 `Send(ReadOnlySpan<byte>)`、`SendAsync(ReadOnlyMemory<byte>)`、`SendAsync(ISocketProtocal)`、`SendAsync(ReadOnlyMemory<byte>, CancellationToken)`；**本次新增 owner 重载**（IOCP 客户端），`Send(ReadOnlySpan)` 去 `ToArray()` 并纳入写门。 |
| `IocpServerSocket` | `Src/SAEA.Sockets/Core/Tcp/IocpServerSocket.cs` | `Send :447` 与 `SendAsync :459` 对**非数组背衬**输入会 `MemoryPoolManager.Rent` 并构造 `PooledBufferWriter` 作 owner，交 `SendAsyncRaw(userToken, seg, writer) :369`；`async` 路径数组背衬时 `SendAsyncRaw(..., null)`（零拷贝、所有权归调用方）。`ProcessSended :346` 里 `TakeSendingOwner()?.Dispose()` 完成归还；`AbandonSendingOwner :442` 在发送超时时**放弃所有权交 GC**。`End :504` 仍 `data.ToArray()`。 |
| `IocpClientSocket` | `Src/SAEA.Sockets/Core/Tcp/IocpClientSocket.cs` | `SendAsyncRaw(ArraySegment<byte>, IDisposable owner) :523` 已支持 owner（`ProcessSended :501` 归还）。但 **`Send(ReadOnlySpan<byte>) :574` 用 `data.ToArray() :584` 拷贝**，再同步 `BeginSend/EndSend :588-589`（阻塞）；`ProcessReceived :436` 的兼容分支 `:464 dataSpan.ToArray()`。 |
| 其余实现者 | `StreamServerSocket`、`UdpServerSocket`（IServerSocket）；`StreamClientSocket`、`UdpClientSocket`（IClientSocket） | 若把 owner 重载加到接口，需同步实现（拷贝后立即 `owner.Dispose()` 即可保语义）。 |

### 1.3 QueueSocket 现状（旗舰靶点）

| 类型 | 位置 | 关键事实 |
| --- | --- | --- |
| `QueueCoder` | `Src/SAEA.QueueSocket/Net/QueueCoder.cs` | `Encode(ISocketProtocal, IBufferWriter<byte>) :61`（已有）；`Encode(QueueSocketMsg) :191` 返回 `byte[]`（`:218 new byte[1+total]`）；`WriteFrame(byte[] buffer, int offset, type, byte[] nameBytes, byte[] topicBytes, byte[] data) :233`；`GetQueueResult(byte[]) :83` → `GetQueueResult(ReadOnlySpan) :93`；`DecodeTo(ReadOnlySpan<byte>, List<QueueMsg>) :414`（`:~357/:~375` name/topic `ToArray()`、payload `qm.Data`）；`_buffer :52`（每连接接收累积缓冲，增长 `new byte[]`，`Clear() :526` 重置）。 |
| `QueueMsg` | `Src/SAEA.QueueSocket/Model/QueueMsg.cs` | `Data : byte[]`、`internal bool IsPooled`；`Dispose()` 在 `IsPooled` 时归还 `ArrayPool<byte>.Shared`（**跨池混用**，与 `MemoryPoolManager` 不一致）。 |
| `QueueSocketMsg` | `Src/SAEA.QueueSocket/Net/QueueSocketMsg.cs` | `MINLENGTH = 1+4+4+0+4+0+0`；`Data : byte[]`、`IsPooled`；`Dispose()` 同样归还 `ArrayPool<byte>.Shared`。 |
| `QServer` | `Src/SAEA.QueueSocket/QServer.cs` | `_exchange_OnBatched :97` → `_serverSokcet.SendAsync(id, data.AsMemory())`（`data` 来自 `ClassificationBatcher` 的 `new byte[]`）；`_serverSokcet_OnReceiveSpan :124` → `GetQueueResult` + `list.Clear() :135`（**不归还** `QueueMsg`）；`Reply :144`。 |
| `Exchange` | `Src/SAEA.QueueSocket/Model/Exchange.cs` | ctor `_classificationBatcher = ClassificationBatcher.GetInstance(5000,100) :110`（**全局单例**）；`_classificationBatcher_OnBatched(id, byte[]) :124`；`AcceptPublish :134`（`:140 _messageQueue.Enqueue(topic, pInfo.Data)`）；`DispatchLoop :182`（每订阅者每批次 `:248 new byte[bufferSize]` LOH）；`TryDequeue :212`；`Unsubscribe :288`；`Clear :313`。 |
| `MessageQueue` | `Src/SAEA.QueueSocket/Model/MessageQueue.cs` | `ConcurrentDictionary<string, FastQueue<byte[]>> _dic :43`；`maxPendingMsgCount`。 |
| `QClient` | `Src/SAEA.QueueSocket/QClient.cs` | `_batcher = new Batcher<byte[]>(1000,50)`；`_batcher_OnBatched :212`（`:224 new byte[totalLength]` 拼接；`:233 _clientSocket.Send(span)` 同步）；`_clientSocket_OnReceiveSpan :188`；`Publish :277`。 |
| `QueueMsgPool` / `QueueMsgListPool` | `Src/SAEA.QueueSocket/Model/QueueMsgPool.cs` / `QueueMsgListPool.cs` | 已存在但**休眠**：`QueueMsgPool.Rent/Return`（`Return` 内部 `msg.Dispose()`）、`QueueMsgListPool.Rent/Return`（`Return` 会把列表内每个 `QueueMsg` 归还池）。注意：`Return` 语义假定「列表里所有 `QueueMsg` 均未被转让所有权」。 |
| 共享 Batcher | `Src/SAEA.Common/Caching/Batcher.cs`、`ClassificationBatcher.cs` | `Batcher` 非泛型拼接用 `new byte[totalLength] :259`。被 `SAEA.MessageSocket`、`SAEA.WebSocket` 共用，**本次不修改**。 |

### 1.4 分片现实（`new byte[]` 全仓普查）

按工程统计（已排除 `bin`/`obj`/`packages`、`Audio.Net/Base`、`Newtonsoft`、测试工程）：

| 工程 | 处数 | 工程 | 处数 |
| --- | --- | --- | --- |
| SAEA.Socket5 | 15 | SAEA.DNS | 14 |
| SAEA.Audio.Net（Core） | 13 | SAEA.MQTT | 12 |
| SAEA.Sockets | 7 | SAEA.QueueSocket | 6 |
| SAEA.Http | 5 | SAEA.MVC | 5 |
| SAEA.WebSocket | 5 | SAEA.P2P | 4 |
| SAEA.RPC | 3 | SAEA.FileSocket | 1 |

> 说明：`SAEA.MessageSocket`、`SAEA.RedisSocket`、`SAEA.FTP` 工程内 `new byte[]` 计数为 0；它们的零拷贝收益来自 socket 发送路径（`Coder.Encode` 产物经 owner 重载直发），而非数组字面量替换。测试工程的 `new byte[]`（P2PTest 42、QueueSocketTest 10 等）**不在本次范围**。
> 注意：`new byte[]` 只是代理指标；真正的热路径分配还包括 `ToArray()`、`Encoding.*.GetBytes/GetString`、`List<byte[]>` 拼接等。实施时以「逐帧分配是否为零」为准。

---

## 二、目标 / 范围 / 硬不变量

### 2.1 目标

1. 逐帧热路径零 `new byte[]`：payload、name/topic、分发拼接缓冲、发送拷贝全部池化。
2. 发送缓冲生命周期安全：池化缓冲通过 owner 机制存活至异步发送完成，且**恰好归还一次**。
3. 参数切片化：网络与编解码 API 的输入/输出尽量使用 `ReadOnlyMemory<byte>` / `ReadOnlySpan<byte>` / `IBufferWriter<byte>`。
4. 性能不退步：吞吐不低于当前 HEAD，`B/frame` 不升高，Gen0 显著下降并趋近 0。
5. 全仓统一：17 个引用 `SAEA.Sockets` 的工程按统一契约接入，而非只修 QueueSocket。

### 2.2 范围

- `SAEA.Sockets` 发送面（owner 重载、客户端 `Send(ReadOnlySpan)` 去 `ToArray()`）。
- `SAEA.QueueSocket` 全链路（接收解码 → 队列 → 分发拼接 → 发送；客户端同样）。
- 其余网络工程（Sockets/P2P/Http/MVC/WebSocket/MQTT/DNS/Audio.Net/RPC/Socket5/FileSocket/MessageSocket/RedisSocket/FTP/Socket5/…）的网络收发热路径。
- 新增必要的**附加式** API 与**新增**池化类型。

### 2.3 非目标

- 不改内嵌第三方：`Src/SAEA.Audio.Net/Base/**`（NAudio/NSpeex）、`Src/SAEA.Common/Newtonsoft.Json/**`。
- 不改非网络工具类（`SAEA.Common` 的 `FileHelper`/`SerializeHelper`/`ApiHelper` 等）。
- 不改测试工程源码（仅新增回归测试）；不改 `Src/packages/**`；不改 README。
- 不修改共享的 `Batcher`/`ClassificationBatcher` 既有行为（MessageSocket/WebSocket 在用）。
- 不引入 `System.IO.Pipelines`；保持 netstandard2.0 可编译。
- 不追求「彻底删除所有 Span/Memory 兼容分支」；兼容分支可保留但不得位于热路径。

### 2.4 硬不变量

- **INV-1 线格式不变**：QueueSocket 帧格式 `[1B Type][4B Total][4B NameLen][Name][4B TopicLen][Topic][Data]`，`Total = 12 + NameLen + TopicLen + DataLen`，小端。池化只改内存来源，不改字节布局。
- **INV-2 恰好归还一次**：任一池化缓冲（`PooledBuffer`/`PooledBufferWriter`/池化 `byte[]`）在整个生命周期内**恰好** `Dispose`/`Return` 一次；发送超时放弃所有权时**不归还**（交 GC），且后续不得再访问。
- **INV-3 发送期间存活**：异步发送引用的池化缓冲，必须在发送完成回调之前保持有效；同步发送在返回前完成，可在返回后立即归还。
- **INV-4 兼容面不变**：既有 public/protected 签名不删除、不改语义；owner 重载为**新增**方法。
- **INV-5 诊断守恒**：静默后 `MemoryPoolManager.GetStatistics()` 的 `Rented == Returned`（按层级），偏差仅来自「放弃所有权的超时发送」与「仍在途的发送」，且不得随时间累积。

---

## 三、核心设计

### 3.1 池化原语与所有权模型

沿用基座，不加新池。统一约定：

- 需要「值 + 长度」的 payload/拼接缓冲 → `MemoryPoolManager.RentPooled(len)` 得 `PooledBuffer`。
- 需要「可增长写入 + 可零拷贝直发」的发送/拼接缓冲 → `new PooledBufferWriter(capacity)`。
- 需要「临时连续 byte[]」且立即使用（如同步发送）→ `MemoryPoolManager.Rent(len)` + `Return(buf, len)`。

**所有权规则**

- **R1**：每个池化缓冲同一时刻只有一个 owner（`PooledBuffer`/`PooledBufferWriter` 实例引用计数为概念上的 1）。
- **R2 payload 所有权（接收侧）**：`DecodeTo` 复制 payload 到 `RentPooled(dlen)`，`QueueMsg.Data` 指向其 `AsMemory()`，owner 挂在 `QueueMsg`。`AcceptPublish` 将 owner **转让**给 `MessageQueue`；其余分支（Ping/Subcribe/Unsubcribe/Close/Data/丢弃/异常）由 `QServer` 在帧处理结束后归还。转让用 `DetachOwner()` 实现，使后续 `QueueMsg.Dispose()` 成为 no-op。
- **R3 分发缓冲所有权**：`DispatchLoop` 把每条消息的分帧字节写入 `PooledBufferWriter`（精确 `GetSpan`/`Advance`），输入 payload owner 在写完该条后立即归还；batch writer 作为 owner 交给 `SendAsync(id, memory, owner)`，由 `ProcessSended` 归还。
- **R4 发送超时**：沿用 `AbandonSendingOwner`，放弃所有权（不归还池）以避免对在途缓冲的竞态；该放弃计入 INV-5 的允许偏差。
- **R5 客户端拼接所有权**：`QClient` 的 batch writer 落 `PooledBufferWriter`；IOCP 客户端用 `SendAsync(writer.WrittenMemory, writer)`，owner 由 `ProcessSended` 归还；非 IOCP 客户端实现为「拷贝发送后立即 `owner.Dispose()`」。
- **R6 异常安全**：任何 `Insert`/`Send`/`SendAsync` 失败都必须 `Dispose` 对应 owner；用 `try/finally` 或「先转让、失败再归还」模式保证。

### 3.2 服务端 owner 发送重载（IServerSocket）

服务端发布分发与客户端批量发布都需要 owner 重载：服务端分发发送是异步的，batch writer 必须存活至完成；客户端因默认使用 IOCP（`IocpClientSocket`）也采用「gated 异步发送」，同样需要 owner 承载批量 writer。**客户端 owner 重载按用户裁决纳入**（见 3.3）。

在 `IServerSocket` 新增：

```csharp
void SendAsync(string sessionID, ReadOnlyMemory<byte> data, IDisposable owner);
```

`IocpServerSocket` 实现：

```csharp
public void SendAsync(string sessionID, ReadOnlyMemory<byte> data, IDisposable owner)
{
    if (data.Length == 0) { owner?.Dispose(); return; }
    var userToken = GetUserToken(sessionID);
    if (userToken == null) { owner?.Dispose(); return; }

    if (MemoryMarshal.TryGetArray(data, out var seg))
    {
        SendAsyncRaw(userToken, seg, owner);
        return;
    }

    var writer = new PooledBufferWriter(data.Length);
    data.Span.CopyTo(writer.GetSpan(data.Length));
    writer.Advance(data.Length);
    if (!writer.TryGetArray(out var rented)) { writer.Dispose(); owner?.Dispose(); return; }
    owner?.Dispose();
    SendAsyncRaw(userToken, rented, writer);
}
```

旧 `SendAsync(string, ReadOnlyMemory<byte>)` 改为 `SendAsync(sessionID, data, null)` 委派，语义不变。`UdpServerSocket`/`StreamServerSocket` 实现为「拷贝发送后立即 `owner?.Dispose()`」。

### 3.3 客户端发送（owner 异步重载 + 同步去 `ToArray()` + 写门）

客户端默认使用 IOCP（`SocketFactory.CreateClientSocket` → `IocpClientSocket`），故**纳入 owner 重载**。

在 `IClientSocket` 新增：

```csharp
void SendAsync(ReadOnlyMemory<byte> data, IDisposable owner);
```

`IocpClientSocket` 实现（复用已支持 owner 的 `SendAsyncRaw :523`）：

```csharp
public void SendAsync(ReadOnlyMemory<byte> data, IDisposable owner)
{
    if (data.Length == 0) { owner?.Dispose(); return; }
    if (MemoryMarshal.TryGetArray(data, out var seg)) { SendAsyncRaw(seg, owner); return; }
    var writer = new PooledBufferWriter(data.Length);
    data.Span.CopyTo(writer.GetSpan(data.Length));
    writer.Advance(data.Length);
    if (!writer.TryGetArray(out var rented)) { writer.Dispose(); owner?.Dispose(); return; }
    owner?.Dispose();
    SendAsyncRaw(rented, writer);
}
```

旧 `SendAsync(ReadOnlyMemory<byte>)` 改为 `SendAsync(data, null)` 委派。`StreamClientSocket`/`UdpClientSocket` 实现为「拷贝发送后立即 `owner?.Dispose()`」。

**并发写门（必须）**：`SendAsyncRaw` 已用 `userToken.WaitWrite` 串行化异步发送，但同步 `Send(ReadOnlySpan)` 直接 `BeginSend`，二者并发会交错/损坏。因此同步 `Send` 改为：

```csharp
public void Send(ReadOnlySpan<byte> data)
{
    if (data.Length == 0) return;
    if (!Connected) { OnError?.Invoke("", new Exception("SAEA SocketError:发送失败,当前连接已断开")); return; }
    var rented = MemoryPoolManager.Rent(data.Length);
    try
    {
        data.CopyTo(rented);
        if (!_userToken.WaitWrite(SocketOption.ActionTimeout))
        {
            OnError?.Invoke($"SAEA SocketError:发送消息时发生异常,{_userToken?.ID}", new TimeoutException("发送数据超时"));
            return;
        }
        try
        {
            var offset = 0;
            while (offset < data.Length)
            {
                var n = _socket.BeginSend(rented, offset, data.Length - offset, SocketFlags.None, null, null);
                offset += _socket.EndSend(n);
            }
            _userToken.Actived = DateTimeHelper.Now;
        }
        finally { _userToken.ReleaseWrite(); }
    }
    catch (Exception ex) { Disconnect(ex); }
    finally { MemoryPoolManager.Return(rented, data.Length); }
}
```

效果：消除 `data.ToArray() :584` 的逐次分配；同步发送与在途异步发送经同一 `WaitWrite` 门串行，避免交错。`ProcessReceived` 的兼容分支 `:464 dataSpan.ToArray()` 保留（非 owner 型订阅者需要 `byte[]`），不计入热路径。

### 3.4 切片化契约

- **编码输入**：`ICoder.Encode` 家族参数由 `byte[]` 放宽为 `ReadOnlyMemory<byte>`（既有 `byte[]` 入参调用方经隐式转换不破坏）。出参属性是真正破坏点。
- **`QueueMsg.Data` / `QueueSocketMsg.Data`**：`byte[]` → `ReadOnlyMemory<byte>`（破坏 `SAEA.QueueSocket` + 其测试 + README；`SAEA.RPC` 不依赖 QueueSocket，`SAEA.Sockets` 不反向依赖，故面可控）。
- **新增 `IBufferWriter` 出参重载**：`Encode(QueueSocketMsg, IBufferWriter<byte>)`、`RpcCoder.Encode(RSocketMsg, IBufferWriter<byte>)`、`P2PCoder` 等，供调用方写入池化 writer，避免「返回 `byte[]` 再拷贝」。
- **`WriteFrame` 切片化**：新增 `WriteFrame(Span<byte> buffer, int offset, type, ReadOnlySpan<byte> name, ReadOnlySpan<byte> topic, ReadOnlySpan<byte> data)`（保留旧 `byte[]` 重载）。
- **name/topic 零分配解码**：`_buffer` 本身是 `byte[]`，故新增数组重载 `GetQueueResult(byte[] buffer, int count)` → `DecodeTo(byte[] buffer, int start, int count, List<QueueMsg>)`，内部直接 `Encoding.UTF8.GetString(buffer, start + nameOffset, nameLen)`（netstandard2.0 支持 `GetString(byte[],int,int)`），**不再 `ToArray()`**。仅当输入为任意 span（外部调用）时退回 scratch 路径。

### 3.5 池化批处理（QueueSocket）

新增**独立**的池化批处理器（不改 `Batcher`/`ClassificationBatcher`），语义等价但 flush 产物为单个 `PooledBufferWriter`：

- `PooledBatcher`：接收 `PooledBufferWriter`（每个订阅者一个子批次），flush 时把子批次拼接进合并 writer，`Dispose` 每个子批次，回调 `OnBatched(PooledBufferWriter batch)`。
- `PooledClassificationBatcher`：`ConcurrentDictionary<string, PooledBatcher>`，按 id 分桶，回调 `OnBatched(string id, PooledBufferWriter batch)`。
- flush 取大小沿用现网参数（`size=5000`、`timeout=100`）。
- `Clear`/`Dispose` 必须显式排空并 `Dispose` 所有在册 writer（避免泄漏），这不同于 `Batcher<T>.Clear` 的直接丢弃行为。

放置位置（**已裁决**）：放进 `SAEA.Common/Caching/` 作为**新增**类型（`PooledBatcher.cs` / `PooledClassificationBatcher.cs`）；不触碰既有 `Batcher`/`ClassificationBatcher`（MessageSocket/WebSocket 在用），可被其他工程复用。

---

## 四、QueueSocket 全链路设计（旗舰）

### 4.1 接收解码（服务端）

1. `_buffer` 用 `MemoryPoolManager` 池化：增长时 `Rent` 新缓冲 → 复制 → `Return` 旧缓冲；`Clear()` 时若容量 > 8192 则归还并重置为 4096，否则清零复用。
2. `GetQueueResult(byte[] buffer, int count)` 数组重载，避免 `ToArray()`；name/topic 直接 `GetString(buffer, offset, len)`。payload 复制到 `RentPooled(dlen)` 并把 owner 挂到 `QueueMsg`（R2）。
3. `QueueMsg` 实例通过 `QueueMsgPool.Rent()` 复用；`QServer` 处理完在 `finally` 中归还未被转让者。

### 4.2 队列与分发

1. `MessageQueue` 存储类型 `FastQueue<byte[]>` → `FastQueue<PooledBuffer>`（或 `FastQueue<IDisposable+值>`）。入队即所有权转让（R2）。队列满/丢弃路径必须 `Dispose`。
2. `Exchange.DispatchLoop`：
   - 从 `MessageQueue.TryDequeue` 取 `PooledBuffer`（`AsSpan()` 作为 payload 源）。
   - 每个订阅者用 `PooledBufferWriter` 写帧（3.4 的 span `WriteFrame`），`nameBytes` 缓存于订阅绑定信息（避免每批次 `GetBytes`），`topicBytes` 已在循环外。
   - 写完一条消息的全部订阅者帧后立即 `Dispose` 该 payload（R3）。
   - batch writer `Insert` 进 `PooledClassificationBatcher`；`Insert` 失败要 `Dispose`（R6）。
3. 回调链：`PooledClassificationBatcher` flush → `Exchange._classificationBatcher_OnBatched(id, writer)` → 事件 → `QServer._exchange_OnBatched(id, writer)` → `_serverSokcet.SendAsync(id, writer.WrittenMemory, writer)`（3.2 owner 重载）。
4. `Unsubscribe`/`Clear`/`Dispose` 需排空并归还所有 in-flight 缓冲。

### 4.3 客户端

- `QClient._batcher` 改为池化 `PooledBatcher`；`_batcher_OnBatched` 用 `PooledBufferWriter` 拼接，`_clientSocket.SendAsync(writer.WrittenMemory, writer)` 异步发送、owner 由 `ProcessSended` 归还（R5）。IOCP 客户端因此无需同步阻塞，减轻线程池压力。
- `Publish` 的 payload 经 `QueueCoder` 写入 writer，避免中间 `byte[]`。
- `Subscribe/Unsubscribe/Close`/`HeartAsync` 控制帧仍走同步 `Send`（经 3.3 写门串行），保证 `Close` 帧在 `Disconnect` 前发出。

### 4.4 编码器内部

- `Encode(QueueSocketMsg)` 保留 `byte[]` 出参（兼容），新增 `Encode(QueueSocketMsg, IBufferWriter<byte>)`；`QServer`/`QClient` 热路径用 writer 重载。
- `EncodeForList`：避免 `List<byte[]>` 拼接，改单 writer 顺序写多帧。
- `WriteFrame` 增 span 重载。

---

## 五、SAEA.Sockets 补强

1. `IServerSocket` + `IocpServerSocket`/`StreamServerSocket`/`UdpServerSocket` 增加服务端 owner 重载（3.2）。
2. `IClientSocket` + `IocpClientSocket`/`StreamClientSocket`/`UdpClientSocket` 增加客户端 owner 重载（3.3）。
3. `IocpClientSocket.Send(ReadOnlySpan)` 改池化临时缓冲（去 `ToArray()`）并纳入 `WaitWrite` 写门（3.3）。
4. `IocpServerSocket.End :504` 可选：改用 owner 路径或池化临时缓冲，去除 `data.ToArray()`（低优先）。
5. `BufferManager`/`SocketKeeper`/`UserTokenFactory`/Udp 的少量 `new byte[]`：改池化或按需延后（见第六节表格）。

---

## 六、其余工程分期零拷贝清单

统一契约：**接收复制进 `RentPooled` → 处理 → 归还；发送写入 `PooledBufferWriter` → owner 重载（服务端）或同步发送 + 归还（客户端）**。逐工程落点：

| 工程 | 主要落点（已验证） | 设计动作 |
| --- | --- | --- |
| SAEA.Sockets | `UdpClientSocket:114`、`UdpServerSocket:124`、`BufferManager:63`、`SocketKeeper:53`、`UserTokenFactory:82`；`IocpClientSocket:584`、`IocpServerSocket:504` | 发送去 `ToArray()`；缓冲池化 |
| SAEA.QueueSocket | 见第四节 | 旗舰全链路 |
| SAEA.P2P | `P2PClient:439`、`P2PServer:384`、`RelayManager:123`、`KeyExchange:65`；`P2PCoder.EncodeP2P`（`PooledBufferWriter(64)` 后 `WrittenSpan.ToArray()`） | writer 出参重载 + owner 发送 |
| SAEA.Http | `RequestDataReader:49`、`HttpSessionManager:78`、`HttpFileResult:46,67`、`IFileResult:36`；`GetRequest(string,byte[])` + `List<byte> _cache` | 响应缓冲池化、文件结果流式 |
| SAEA.MVC | `BigDataResult:66,83`、`DataResult:56`、`FileResult:48,69` | 写入 writer 后 owner 发送 |
| SAEA.WebSocket | `WSCoder:115,142,169,189`、`WSSServerImpl:108` | 解码 `new byte[]` 归池、发送走 writer |
| SAEA.MQTT | `MqttChannelAdapter:58,295,325`、`MqttPacketBodyReader:110,115`、`MqttPacketWriter:48,49,124`、`MqttPacketReader:42`、`PlatformAbstractionLayer:42`、`MqttApplicationMessageBuilder:113`、`MqttConnectionValidatorContext:69` | reader/writer/body 缓冲池化 |
| SAEA.DNS | `TcpRequestCoder:91,100`、`MailExchangeResourceRecord:47,63`、`ResourceRecord:80`、`TextResourceRecord:66`、`CharacterString:79,99,124`、`Domain:94,124,188` | 构造用池化 writer、减少临时数组 |
| SAEA.Audio.Net（Core） | `AcmChatCodec:90`、`ALawChatCodec:59,70`、`G722ChatCodec:69,82`、`MuLawChatCodec:59,70`、`SpeexChatCodec:97,99,122,126`、`UncompressedPcmChatCodec:50,57` | 每帧编解码 scratch 复用/池化 |
| SAEA.RPC | `RpcCoder:167,180,200` | `Encode(RSocketMsg, IBufferWriter<byte>)` 重载 + owner 发送 |
| SAEA.Socket5 | `Socks5Codec:32,34,78,121,151,188,288,331,377,440,450`、`Socks5Server:353,379,463,569` | 代理热路径缓冲池化 |
| SAEA.FileSocket | `Client:92` | 缓冲池化 |
| SAEA.Common | `Batcher:259`（共享，不改）；其余为非网络工具（跳过） | 仅新增池化原语（3.5） |
| MessageSocket/RedisSocket/FTP | 工程内 0 个 `new byte[]`，但经 socket `Coder.Encode` 产物发送 | 接入 owner 重载发送路径（若走 SAEA.Sockets） |

---

## 七、分期与实施顺序

- **P0 基座校验**：确认 `PooledBufferWriter`/`PooledBuffer`/`MemoryPoolManager`/`SendingOwner` 行为与 `GetStatistics` 可用；补所有权单测。
- **P1 发送面**：服务端 owner 重载（含 3 个实现者）；客户端 owner 重载（含 3 个实现者）；`IocpClientSocket.Send(ReadOnlySpan)` 去 `ToArray()` 并纳入写门。
- **P2 QueueSocket payload 池化**：`_buffer` 池化、`GetQueueResult` 数组重载、`DecodeTo` 复制进 `RentPooled`、`QueueMsg` owner/`DetachOwner`、`QueueMsgPool` 接线、`MessageQueue` 存 `PooledBuffer`。
- **P3 QueueSocket 分发生命周期**：`PooledBatcher`/`PooledClassificationBatcher`、`Exchange.DispatchLoop` 使用、`QServer` 发送走 owner 重载。
- **P4 QueueSocket 客户端**：`QClient` batch writer + 同步发送归还；控制帧池化。
- **P5 编码器与 name/topic**：`WriteFrame` span 重载、`Encode(...,IBufferWriter)`、name/topic scratch、`EncodeForList` 单 writer。
- **P6 其余工程**：按第六节顺序逐工程接入（每工程一个可独立验证的小步）。
- **P7 收尾**：门禁、基准对比、清理未用兼容分支、文档。

每阶段独立提交、独立跑门禁；失败即停不越级。

---

## 八、测试与验收（DoD）

**功能回归**

- `Src/SAEA.Sockets.sln` Debug/Release 构建 0 error。
- `SAEA.QueueSocketTest --functional` 全通过（FT1–FT13）。
- `SAEA.QueueSocketTest --all` 全通过。
- `SAEA.P2PTest --all` 310/310；`SAEA.P2PTest --bench-iocp` 29/29。
- 三端（Producer/QServer/Consumer）无头联调无停顿、消息不丢不乱序。

**新增池化测试（必须）**

- `FT-Pool-1 所有权转让`：Publish 后 `MessageQueue` 持有 owner，`QueueMsg.Dispose` 为 no-op，不双归还。
- `FT-Pool-2 丢弃/断开归还`：无订阅者、队列满、连接断开、异常分支均归还 payload。
- `FT-Pool-3 守恒`：持续流量后静默，断言 `MemoryPoolManager.GetStatistics()` 各层级 `Rented == Returned`（持有允许的在途/超时偏差，且重复采样不增长）。
- `FT-Pool-4 字节等价`：池化编码输出与旧 `byte[]` 编码逐字节一致（线格式 INV-1）。
- `FT-Pool-5 发送存活`：异步分发发送期间 buffer 不被归还（压力 + 小批量高频 flush）。
- `FT-Pool-6 客户端不交错`：并发「批量 `SendAsync(owner)` + 同步控制帧 `Send`」压力下，接收端解析出的帧完整、无字节交错（写门生效）。

**性能**

- 基准 `--bench-queue`：`msg/s` 不低于当前 HEAD；`B/frame` 不高于当前 HEAD；Gen0 明显下降。
- 维持线程池缓解（`QueueSocketThreadPool.EnsureConfigured`）。

**代码质量**

- 无新增 `//` 行内注释（`///` 允许）。
- public/protected 签名 diff 仅含「新增 owner 重载 / 放宽入参 / 计划内的出参切片」。
- 工作区干净，按显式路径提交。

---

## 九、风险与缓解

| 风险 | 说明 | 缓解 |
| --- | --- | --- |
| 双重归还 / 归还后使用 | 转让语义实现错误最易在 Publish 路径发生 | `DetachOwner` 使后续 Dispose no-op；FT-Pool-1/3 |
| 异步发送中缓冲被归还 | 忘记走 owner 重载 | 服务端只经 owner 重载发 batch；FT-Pool-5 |
| 超时放弃 | `AbandonSendingOwner` 不归还 | 计入 INV-5 允许偏差；文档标注 |
| 队列满/断开泄漏 | 丢弃分支遗漏归还 | 中央化「归还此批」辅助函数；FT-Pool-2 |
| 层级错配 | `Return` 无 `originalSize` 按 `Length` 推断 | 统一用 `PooledBuffer`/`PooledBufferWriter`（自带大小）；原始数组路径传 `originalSize` |
| 同步/异步发送交错 | 批量 `SendAsync` 与同步 `Send` 并发导致字节交错 | 同步 `Send` 纳入 `WaitWrite` 写门（3.3）；FT-Pool-6 |
| 接口新增破坏外部实现 | 加 `IServerSocket`/`IClientSocket` owner 重载 | INV-4；文档标注破坏性 |
| 多线程 | `PooledBufferWriter` 非线程安全 | 每批次/每发送独占，不跨线程共享 |
| 共享 Batcher 误改 | MessageSocket/WebSocket 回归 | 只新增池化类型，不改旧类型 |

---

## 十、兼容性与迁移

- **入参放宽**（`byte[]`→`ReadOnlyMemory<byte>`）：源兼容（隐式转换）。
- **出参切片**（`QueueMsg.Data`/`QueueSocketMsg.Data`→`ReadOnlyMemory<byte>`）：破坏性，限于 `SAEA.QueueSocket`、其测试、README；无下游 `SAEA.RPC` 依赖。
- **新增 owner 重载 / writer 重载**：附加式，不删旧签名。
- **线格式**：完全不变。
- **netstandard2.0**：仅用该目标可用 API（`MemoryMarshal`、`Span.IndexOf`、`BinaryPrimitives`、`Buffer.BlockCopy`、`Encoding.GetString(byte[],int,int)`）。

---

## 十一、预计修改文件清单（初始）

**修改**

- `Src/SAEA.Sockets/IServerSocket.cs`、`Core/Tcp/IocpServerSocket.cs`、`Core/Tcp/StreamServerSocket.cs`、`Core/Udp/UdpServerSocket.cs`
- `Src/SAEA.Sockets/IClientSocket.cs`、`Core/Tcp/IocpClientSocket.cs`（owner 重载 + `Send` 去 `ToArray()` + 写门）、`Core/Tcp/StreamClientSocket.cs`、`Core/Udp/UdpClientSocket.cs`
- `Src/SAEA.QueueSocket/Model/QueueMsg.cs`、`Model/MessageQueue.cs`、`Model/QueueMsgPool.cs`、`Model/QueueMsgListPool.cs`、`Model/Exchange.cs`
- `Src/SAEA.QueueSocket/Net/QueueCoder.cs`、`Net/QueueSocketMsg.cs`
- `Src/SAEA.QueueSocket/QServer.cs`、`QClient.cs`
- 其余工程按第六节对应的 Coder/Batcher/发送点

**新增**

- `Src/SAEA.Common/Caching/PooledBatcher.cs` + `PooledClassificationBatcher.cs`（已定）
- `Src/SAEA.QueueSocketTest/FunctionalTests.cs` 内新增 FT-Pool-1..6（及必要时 `TestHarness` 辅助）

---

## 十二、预期收益

- QueueSocket 分发路径每帧 `new byte[]`（含 `bufferSize` LOH）归零；payload/name/topic 分配归零。
- 高频流量下 Gen0 趋近 0、LOH 分配消失；`B/frame` 不升高。
- 全仓 16 个工程共享同一池化契约，后续新代码有统一基线。
- 保留既有 Span/Memory 公共面与线格式，迁移成本低、风险可控。

---

## 十三、已裁决项

1. **池化批处理器放置**：放入 `SAEA.Common/Caching/`，新增 `PooledBatcher`/`PooledClassificationBatcher`（可复用，不修改旧 `Batcher`/`ClassificationBatcher`）。
2. **客户端 owner 异步重载**：纳入——客户端默认走 IOCP，故 `IClientSocket`/`IocpClientSocket` 增加 owner 重载，并给同步 `Send` 加 `WaitWrite` 写门（3.3）。
3. **`SAEA.Common` 非网络工具类**（`FileHelper`/`SerializeHelper` 等）：**跳过**，仅做网络热路径。