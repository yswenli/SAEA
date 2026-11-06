# SAEA.Sockets.Pipeline - 双向通道链与可插拔编解码栈

<p align="center">
  <img src="https://img.shields.io/badge/版本-26.11.5.1-blue?style=for-the-badge" alt="Version">
  <img src="https://img.shields.io/badge/NuGet-SAEA.Sockets.Pipeline-green?style=for-the-badge" alt="NuGet">
  <img src="https://img.shields.io/badge/.NET-Standard%202.0-purple?style=for-the-badge" alt=".NET">
  <img src="https://img.shields.io/badge/协议-Apache%202-orange?style=for-the-badge" alt="License">
</p>

<p align="center">
  <b>🚀 每连接一条处理器链，分帧、编解码、鉴权、业务各归其位！</b>
</p>

<p align="center">
  <a href="README.en.md">English</a> | <b>中文</b>
</p>

---

> **🎯 问题场景：** 直接在 socket 回调里写业务，半包/粘包、鉴权、超时、背压很快就会缠成一团，改协议就要动收包代码。
>
> **SAEA.Sockets.Pipeline 方案：** 每连接一条双向链——接收泵独占 `PipeReader`，负责拆帧与背压；链上 handler 各管一件事；回包沿同一条链反向经过，所有权在链头交给传输层。

---

## ✨ 为什么选择 SAEA.Sockets.Pipeline？

| 对比项 | 手写收包循环 | SAEA.Sockets.Pipeline |
|--------|--------------|-----------------------|
| **半包/粘包** | 自己维护缓冲与游标 | 泵 + 可插拔分帧器，`TrySplit` 统一处理 |
| **鉴权/超时/背压** | 与业务代码缠绕 | 独立 handler，消息级 `CancellationToken` |
| **编解码** | 改协议要动收包代码 | 两级 codec：帧 → `Envelope` → 业务实体 |
| **出站** | 各处直接操作 socket | 同一条链反向传播，链头统一交接 `owner` |
| **连接隔离** | 状态容易串到别的连接 | 链、队列、执行器均为每连接一份 |

---

## 🚀 30 秒接入示例

下面的写法与 `Src/SAEA.Sockets.PipelineTest/Program.cs` 一致：`PipelineHost` 与 `SocketStreamAdapter` 见该演示工程，可直接复制；`BuildPipeline(options)` 即下一节的标准装配。

```csharp
var executors = new ConcurrentDictionary<string, ChannelPipelineExecutor>();
var host = new PipelineHost();                                    // 演示工程中的 IPipelineHost 实现
var options = new ChannelPipelineOptions { Splitter = LengthFieldSplitter.Default(64 * 1024) };
SaeaServerSocket server = null;

var option = new SocketOptionBuilder()
    .UseStream()                                                  // 钩子必须基于 UseStream 提供的 PipeReader
    .SetPort(39654)
    .UseConnectionOpened(info =>
    {
        var channel = new DefaultChannel(info.ID, reason => executors.TryGetValue(info.ID, out var running) ? running.BeginShutdown(reason) : Task.CompletedTask);
        channel.SetAttribute(TokenAuthHandler.TokenAttributeKey, "seed-token");
        host.Set(channel);
    })
    .UseInputHandler(async (userToken, input, flow, token) =>
    {
        var stream = new SocketStreamAdapter(userToken.ID, input, flow, server);
        var executor = new ChannelPipelineExecutor(stream, host.Get(userToken.ID), options, token, host);
        executor.Attach(BuildPipeline(options));                  // 先装配链，再启动泵
        executors[userToken.ID] = executor;
        executor.Start();
        try { await executor.Completion; }
        finally { executors.TryRemove(userToken.ID, out _); }
    })
    .Build();

server = new SaeaServerSocket(option);
server.Start();
```

---

## 🧱 架构一览

```
入站（链首 → 链尾）:
  Pipe → 接收泵（拆帧 + 背压）→ PooledMessage → CodecDecoderHandler → AuthHandler
       → EnvelopeUnwrapHandler → TokenAuthHandler → InboundLoggingHandler
       → MessageDispatcher → ExceptionTerminalHandler

出站（链尾 → 链首）:
  业务 ctx.WriteAsync(entity) → EnvelopeWrapHandler → CodecEncoderHandler
       → FrameEncoderHandler → 链头 → IRawByteStream.SendAsync(frame, owner) → 传输层
```

---

## 📐 线格式不变量

默认分帧配置（`LengthFieldSplitter.Default` + `LengthFieldFrameEncoder`）的线上布局固定为：

```
 0        1        2        3        4        5        6        7        8        9 ...
 +--------+--------+--------+--------+--------+--------+--------+--------+--------+----------------+
 |                   body 长度（8 字节，小端 int64）                       |  type  |     body       |
 +--------+--------+--------+--------+--------+--------+--------+--------+--------+----------------+
 |<-------------------------- 9 字节包头 P_Head ------------------------->|<--- body 长度 --->|

 长度字段 = body 的字节数（不包含 9 字节包头），不是整帧长度。
 整帧长度 = 9 + body 长度；body 可以为空（长度 0）。
 type 占 1 字节，取值 0..255，由 TypeRegistry 映射到 CLR 类型。
```

- `FrameConstants.P_LEN = 8`、`FrameConstants.P_Type = 1`、`FrameConstants.P_Head = 9`。
- `LengthFieldSplitter.Default` 只剥掉 8 字节长度字段，所以 `PooledMessage.Frame` 的布局是 `[1B type][body]`；`EnvelopeCodec` 再去掉 1 字节 type，得到独立的 `Envelope.Payload`。
- `LengthFieldFrameEncoder.Encode` 写入的就是 `body.Length`，且 type code 必须在 `0..255` 之内。

---

## 🧩 标准装配顺序

```csharp
var pipeline = ChannelPipeline.Create(options);
var codec = new EnvelopeCodec();

// 出站 handler 必须占据链首连续前缀。AddLast 是追加到链尾，所以先添加离传输层最近的
// FrameEncoderHandler，最后添加离业务最近的 EnvelopeWrapHandler。
// 出站传播方向是链尾 → 链首，实际经过顺序：
// EnvelopeWrapHandler → CodecEncoderHandler → FrameEncoderHandler → 传输层。
pipeline.AddLast(new FrameEncoderHandler(new LengthFieldFrameEncoder()));
pipeline.AddLast(new CodecEncoderHandler(codec));
pipeline.AddLast(new EnvelopeWrapHandler(registry, new JsonMessageCodec()));

// 入站：分帧已由接收泵完成，链上不再需要 FrameSplitterHandler。
pipeline.AddLast(new CodecDecoderHandler(codec));
pipeline.AddLast(new AuthHandler(new[] { 1, 3 }));                 // 未认证阶段放行的 type code
pipeline.AddLast(new EnvelopeUnwrapHandler(registry, new JsonMessageCodec()));
pipeline.AddLast(new TokenAuthHandler());
pipeline.AddLast(new InboundLoggingHandler());
pipeline.AddLast(new MessageDispatcher()
    .Register<LoginRequest>(loginHandler)
    .Register<Heartbeat>(heartbeatHandler));
pipeline.AddLast(new ExceptionTerminalHandler());
```

- 出站 handler 必须是链首连续前缀，不能夹在入站 handler 之后。`AddLast` 会校验：一旦链首已有入站 handler，再追加出站 handler 会抛 `InvalidOperationException`，此时应改用 `AddFirst` 插到链首。
- `AddFirst` 也有对称约束：入站 handler 不能插到链首已有的出站 handler 之前。
- 一个 handler 实例同时实现入站与出站接口会被拒绝；同一非共享实例不能同时挂到多条链（`IChannelHandler.IsSharable`）。

---

## 🔌 接入传输层：IRawByteStream 适配器

核心包不引用 `SAEA.Sockets`，使用方需要把 `IServerSocket` 适配成 `IRawByteStream`。下面是可直接复制的完整实现（与 `Src/SAEA.Sockets.PipelineTest/SocketStreamAdapter.cs` 一致）：

```csharp
using System;
using System.IO.Pipelines;
using System.Threading.Tasks;
using SAEA.Sockets;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Pipeline;

public sealed class SocketStreamAdapter : IRawByteStream
{
    private readonly string _channelId;
    private readonly PipeReader _input;
    private readonly IReceiveFlowControl _flow;
    private readonly IServerSocket _server;

    public SocketStreamAdapter(string channelId, PipeReader input, IReceiveFlowControl flow, IServerSocket server)
    {
        if (string.IsNullOrEmpty(channelId))
            throw new ArgumentException("连接标识不能为空。", nameof(channelId));
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        if (flow == null)
            throw new ArgumentNullException(nameof(flow));
        if (server == null)
            throw new ArgumentNullException(nameof(server));

        _channelId = channelId;
        _input = input;
        _flow = flow;
        _server = server;
    }

    public string ChannelId
    {
        get { return _channelId; }
    }

    public PipeReader Input
    {
        get { return _input; }
    }

    public Task SendAsync(ReadOnlyMemory<byte> data, IDisposable owner)
    {
        try
        {
            // owner 原样交给传输层：发送完成或失败由传输层释放，适配器绝不自行 Dispose。
            _server.SendAsync(_channelId, data, owner);
            return Task.CompletedTask;
        }
        catch (ChannelClosedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ChannelClosedException("连接已关闭或发送失败:" + _channelId + "," + ex.Message, ex);
        }
    }

    public void Pause()
    {
        _flow.Pause();
    }

    public void Resume()
    {
        _flow.Resume();
    }

    public Task CloseAsync(string reason)
    {
        return _flow.CloseAsync(reason);
    }
}
```

- `SendAsync` 返回 `Task.CompletedTask` 只表示“帧的所有权已交给传输层”，不表示对端已收到；`owner` 必须在返回前保持未释放。
- 连接已关闭时必须立即抛 `ChannelClosedException`，不能排队或静默成功；其他传输层异常也统一翻译成 `ChannelClosedException`。
- 只支持服务端接入；客户端不支持两个钩子，配置即 `NotSupportedException`。

---

## 🔐 消息所有权

- `PooledMessage` 创建时引用计数为 1，所有权归链。每条消息的执行包在 `finally` 中无条件 `Release()` 一次，所以无论异常抛在哪一环，引用计数都会归零；`Release()` 对已归零实例是 no-op。
- 入站 handler 把消息传给下游后不得再使用该消息。需要跨 `await` 或跨回调保存时，必须先 `Retain()`，并在自己的生命周期结束时 `Release()` 那一份；executor 释放的是队列条目那一份，两者不能互相替代。
- `CodecDecoderHandler` 在解码成功后立即释放队列条目；解码抛异常时不在该 handler 释放，交给执行包的 `finally` 兜底。
- `PooledMessage.Frame` 的背衬按长度二选一：`>= SmallFrameInlineThreshold`（默认 512B）从 `MemoryPoolManager` 借，`IsPooled == true`；小于阈值时精确 `new byte[len]`，`IsPooled == false`，归零后只丢引用。释放后再访问 `Frame` 会抛 `ObjectDisposedException`。
- `Envelope.Payload` 永远是独立 `new byte[len]`，绝不指向 `PooledMessage` 的池化背衬，因此没有归还责任；`EnvelopeCopyThreshold`（默认 4KB）只控制一条 `Warn` 日志，不改变拷贝方式。
- 需要原始帧字节（抓包、审计、二进制直通）时改用 `FramePassthroughHandler`：它 `Retain()` 后原样下传，由该分支自己的尾部释放。这条分支与 codec 分支互斥，不能装在同一条链上。
- 出站中间体 `EncodedBody` / `TransportFrame` 只允许出现在出站链内部；`await ctx.WriteAsync(...)` 正常返回即所有权交给链头/传输层，抛异常时由创建者 `Dispose`。

---

## ⏱️ 生命周期与关闭语义

- `Attach` 之前不能 `WriteAsync`：链头找不到执行器或字节流，抛 `InvalidOperationException("pipeline 尚未 Attach 到传输层。")`；`ctx.Channel` 同样不可用。
- `ChannelPipelineExecutor.Attach(pipeline)` 必须在 `Start()` 之前完成，否则 `Start()` 抛 `InvalidOperationException`。
- `CloseAsync(reason)` 只表示“已进入收口”：调用立即返回，后台收口继续执行，资源是否回收完要看 `ChannelPipelineExecutor.Completion`。
- 进入收口后再 `WriteAsync` 抛 `ChannelClosedException`；连接已关闭时 `IRawByteStream.SendAsync` 也必须立即抛同一异常。该判定先于消息类型判定：即使持有的是已摘链节点的旧 `ctx`、传出的也不是 `TransportFrame`，只要连接在收口就按关闭上报。
- 禁止再调用 `GetStream()` 或 `ChannelInfo.Stream.ReadAsync` 与泵抢 `PipeReader`：配置 `UseInputHandler` 后，该连接的 `PipeReader` 由泵独占，两个读者会互相抢数据并触发 Pipe 非法并发读。出站 `Stream.WriteAsync` 不受影响。
- 钩子启用后，该连接的 `OnServerReceiveSpan` 不再触发（`StartSpanPump` 不会启动）。
- `UseInputHandler` 必须与 `UseConnectionOpened` 成对配置，且必须 `.UseStream()`；`SocketOptionBuilder.Build()` 会校验并抛 `InvalidOperationException`。客户端构造 `SaeaClientSocket` 时配置任一钩子会抛 `NotSupportedException`。
- `UseConnectionOpened` 在 `RaiseAccepted` 内同步调用，只做轻量登记（创建 `IChannel`、写初始属性），禁止阻塞 IO 或 `await`。

---

## 💰 已知成本

- 入站每帧固定 **1 次拷贝**（executor 泵路径）：直接在 `Pipe` 缓冲上切帧，成帧后复制这一次成为 `PooledMessage`（`AllocateFrame`）。`AdvanceTo` 只推进到最后一条成帧位置，未成帧残字节留在 `Pipe` 里交给下一次 `ReadAsync`；队列条目要跨 `await` 存活，所以这一次拷贝省不掉，背衬不能指向 `Pipe` 内存。
- 纯链式（不用 executor、由 `FrameSplitterHandler` 在链首分帧）仍是 **2 次拷贝**：① `Pipe` → `FrameAccumulator`，② 累加器 → 独立 `PooledMessage`。
- 出站固定 **3 次拷贝**：① 实体 → `Envelope.Payload`（codec 产出的 `byte[]`），② `Payload` → `EncodedBody`（codec writer），③ `EncodedBody` → 最终帧缓冲（frame writer）。传输层按 `owner` 语义接管同一块缓冲，不再由管线额外拷贝；适配器如需留存字节，由适配器自己承担那次拷贝。
- 分配口径：小帧（默认 `< 512B`）走精确分配，每帧多一个 `byte[len]` 与一个 `PooledMessage`；大帧从 `MemoryPoolManager` 借背衬，只多一个 `PooledMessage`。这是“两级正交 + `IBufferWriter` 不能前插”的已知预算，不要按零拷贝要求它。
- 实测口径：`PipelineTest --bench`（265B 帧）约 480B/帧，其中管线自身约 352B（`byte[265]` 296B + `PooledMessage` 56B），其余来自基准夹具往 `Pipe` 写 64KB 分块时的段分配。

---

## 🧵 消息执行模型

- 每条连接一个消费协程：取出队列条目、拿到并发额度后**直接启动**该消息的执行链，不 `await`、不额外丢线程池；消息同步完成时就在消费协程上返回，继续取下一帧。
- 并发上限是 `MaxConcurrentMessages`，只在消息 `await` 挂起时才真正并行；单条消息内部（解码 → 鉴权 → 路由）严格串行，响应顺序不等于接收顺序。
- **同步阻塞或 CPU 密集的 handler 会占住消费协程**，推迟同连接后续帧；这类逻辑请自行 `Task.Run` 或改成异步。异步 handler（例如 20–50ms 的远程鉴权）不受影响。
- `MessageTimeout == 0`（默认）时 `ctx.CancellationToken` 就是连接级令牌，不额外创建链接令牌；配置超时才为每条消息创建 linked CTS，并在消息结束的 `finally` 中恢复与释放。

---

## 🛡️ 容量上限：MaxBufferedBytes 与 MaxFrameLength

| 配置 | 规则 |
|------|------|
| `Splitter` | 必填；为 null 时 `ChannelPipeline.Create` 抛 `ArgumentException` |
| `MaxBufferedBytes == 0` | 自动推导为 `Splitter.MaxFrameLength + FrameConstants.P_Head + ReadBufferSize`，即 `MaxFrameLength + 9 + ReadBufferSize` |
| `MaxBufferedBytes != 0` | 必须 `>= MaxFrameLength + P_Head`，否则 `Create` 抛 `ArgumentOutOfRangeException`（一个合法最大帧连整帧都攒不下） |
| 推导值或显式值 > `int.MaxValue` | `Create` 抛 `ArgumentOutOfRangeException` |
| 运行期超限 | 每轮把完整帧全部入队后检查 `Pipe` 中残余字节，仍 `> MaxBufferedBytes` 时以 `reason = "buffered-bytes-limit"` 关闭连接 |
| 单帧超长 | 在 `IFrameSplitter.TrySplit` 内检查，抛 `FrameProtocolException` |

- 默认 `MaxBufferedBytes = 1MB` 只适用于 `MaxFrameLength <= 960KB` 左右；协议允许更大帧时必须显式配置。
- `ReadBufferSize` 只是给“最大合法帧 + 一点尾巴”留的余量，不限制单次 `ReadAsync` 返回长度，也不影响 `Pipe` 的容量或残余字节的统计口径。
- `MaxBufferedBytes` 统计的是 `Pipe` 中未成帧的**残余字节**（与 `ChannelTraffic.BufferedBytes` 同口径），不是单帧长度；先排空完整帧再检查，避免一次读到很多合法帧被误杀。

---

## 🐞 调试断言边界

- `Debug` 构建下会对出站做一次轻量检查：**同一个 handler 节点**对同一个 `msg` 实例只允许调用一次 `ctx.WriteAsync`，重复写入会触发断言；不同节点转发同一实例（例如日志 handler 原样透传中间载体）是合法的。它覆盖的是“同一个处理器把同一条消息写两次”。
- 它**不覆盖**“忘记转发”：handler 直接返回、截断出站链，与“明确丢弃”在运行时不可区分，只能靠代码评审和端到端用例（断言回包确实到达）发现。
- 直接调用 `IRawByteStream.SendAsync` 绕过链头的情形也不做运行时检测，属于代码评审禁止项。

---

## 📋 内置 handler 速查

| Handler | 方向 | 作用 |
|---------|------|------|
| `FrameEncoderHandler` | 出站 | 把 `EncodedBody` 补成完整帧，包成 `TransportFrame` 交给链头 |
| `CodecEncoderHandler` | 出站 | `Envelope` → `EncodedBody`（调用 `ICodec.Encode`） |
| `EnvelopeWrapHandler` | 出站 | 业务实体 → `Envelope.Type` + `Envelope.Payload` |
| `OutboundLoggingHandler` | 出站 | 记录出站消息类型，原样转发 |
| `CodecDecoderHandler` | 入站 | `PooledMessage` → `Envelope`，随后释放帧字节 |
| `AuthHandler` | 入站 | 第一级鉴权：只看 `Envelope.Type` 白名单，不解析 payload |
| `EnvelopeUnwrapHandler` | 入站 | `Envelope` → 业务实体（按 `TypeRegistry` 路由） |
| `TokenAuthHandler` | 入站 | 第二级鉴权：实体还原后校验连接属性中的令牌 |
| `InboundLoggingHandler` | 入站 | 记录入站消息类型，原样转发 |
| `MessageDispatcher` | 入站 | 按精确 CLR 类型调用 `IMessageHandler`，支持兜底 |
| `ExceptionTerminalHandler` | 入站 | 链尾：记录异常、按类型决定是否断开、回收未被消费的消息 |
| `FramePassthroughHandler` | 入站 | 原始帧旁路（`Retain` 后下传），与 codec 分支互斥 |
| `FrameSplitterHandler` | 入站 | 不用接收泵、需要链内手工拆帧时才使用 |

可插拔实现：`LengthFieldSplitter`、`DelimiterSplitter`、`FixedLengthSplitter`、`LineSplitter`、`LengthFieldFrameEncoder`、`EnvelopeCodec`、`JsonMessageCodec`、`ProtobufMessageCodec`、`RawCopyCodec`、`TypeRegistry`。

---

## 🧪 演示与自测

```bash
dotnet run --project Src/SAEA.Sockets.PipelineTest -c Release -- --all     # 内存回环全链路
dotnet run --project Src/SAEA.Sockets.PipelineTest -c Release -- --socket  # 真实 SaeaServerSocket 接入
dotnet run --project Src/SAEA.Sockets.PipelineTest -c Release -- --tls     # 真实 socket + TLS，首帧在握手之后
dotnet run --project Src/SAEA.Sockets.PipelineTest -c Release -- --bench   # 与 BaseCoder 直连的吞吐/分配趋势
```

不带参数只打印用法，自动化场景必须显式传参；`--bench` 是趋势观测，不作门禁。单元测试工程不在解决方案内，需单独执行：

```bash
dotnet test Src/SAEA.Sockets.Pipeline.Tests/SAEA.Sockets.Pipeline.Tests.csproj -c Debug
```

---

## 📦 安装

```bash
# NuGet 安装
dotnet add package SAEA.Sockets.Pipeline

# 或通过 Package Manager
Install-Package SAEA.Sockets.Pipeline
```

---

## 🔗 相关资源

| 资源 | 链接 |
|------|------|
| 📂 **GitHub** | [yswenli/SAEA](https://github.com/yswenli/SAEA) |
| 📦 **NuGet** | [SAEA.Sockets.Pipeline](https://www.nuget.org/packages/SAEA.Sockets.Pipeline) |
| 📖 **文档** | [SAEA.Sockets](../SAEA.Sockets/README.md) |
| 🧪 **演示与自测** | [SAEA.Sockets.PipelineTest](../SAEA.Sockets.PipelineTest) |
| 📝 **博客** | [作者博客](https://www.cnblogs.com/yswenli/) |

---

## 📄 许可证

Apache License 2.0 - 自由使用、修改、分发！

---

<p align="center">
  <b>💡 有问题？欢迎提 Issue 或 PR！</b>
</p>

<p align="center">
  Made with ❤️ by <a href="https://github.com/yswenli">yswenli</a>
</p>