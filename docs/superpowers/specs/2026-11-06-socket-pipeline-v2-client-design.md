# SAEA.Sockets.Pipeline v2：客户端通道链接入设计

上游依据：`docs/superpowers/specs/2026-11-05-socket-pipeline-design.md` §2.2（v1 非目标：不做客户端通道链）、§5.1（钩子时序）、§6.1（配置校验）、§6.3（断开收口）。

- 目标：`SaeaClientSocket` 与服务端同构地接入通道链，v1 留下的「客户端构造即抛 `NotSupportedException`」退场。
- 不动：链路核心包（`SAEA.Sockets.Pipeline`）的公开契约、接收泵、分帧器、编解码层；客户端既有事件模式（`OnClientReceiveSpan`）与 `GetStream()` 的语义。

## 一、v1 的做法与代价

v1 在 `SaeaClientSocket` 构造阶段读到 `IPipelineSocketOption` 上的任一钩子就直接抛 `NotSupportedException`。这样处理的原因是客户端没有 `ChannelManager` 与 `sessionID` 两张表：服务端靠 `_pipelineCts[sessionId]` 管理每连接的泵，而客户端只有一条连接、一个 `_userToken`，粘一份服务端结构反而是负担。

代价是「客户端想用同一套链」必须自己写适配层，而两端的线格式又完全一样（`P_LEN=8` 小端 body 长度 + `P_Type=1` + body）。

## 二、变更

### 2.1 钩子字段（构造期取值）

`SaeaClientSocket` 构造时从 `IPipelineSocketOption` 取 `ConnectionOpened` / `InputHandler`，分别存入只读字段 `_connectionOpened`（`Action<ChannelInfo>`）与 `_inputHandler`（`Func<IUserToken, PipeReader, IReceiveFlowControl, CancellationToken, Task>`），另外用一个 `_pipelineCts` 保存当前泵的取消源。不再做任何拒绝。

### 2.2 启动时机

客户端只有一次连接，启动点比服务端的 `RaiseAccepted` 少一层管理，但要处理 TLS：

- **明文**：`ProcessConnected` 内 `SetupConnection()`、`ProcessReceive(readArgs)` 之后，`_connectCallBack` 通知结果之前调用 `StartPipelineIfNeeded()`。此时 `_connection.Input` 已可读，用户拿到「连接成功」回调时通道链已在收数据。
- **TLS**：`ProcessConnected` 只做握手登记，`HandshakeTask.ContinueWith` 的成功分支里调用 `StartPipelineIfNeeded()`，同样在 `_connectCallBack?.Invoke(SocketError.Success)` 之前。握手失败分支与 `_connection == null` 的 TLS 初始化失败分支都不启动——链位于明文侧，握手没成就不存在明文可读。
- **未配置钩子**：`StartPipelineIfNeeded()` 立即返回 `true`，路径与 v1 完全一致。

`StartPipelineIfNeeded()` 的步骤与服务端 `SaeaServerSocket.StartPipelineIfNeeded` 对齐：

1. 用当前 `_userToken` 造 `ChannelInfo`（`ID` / `ClientSocket` / `Stream` / `UserToken` / `Tls`），先同步调用 `_connectionOpened`；
2. 建 `CancellationTokenSource` 存入 `_pipelineCts`；
3. `new ReceiveFlowControl(connection, Disconnect)` 作为应用级闸门，泵自行 `Dispose` 连接即走 `Disconnect()`；
4. `Task.Run` 里 `await handler(_userToken, connection.Input, flow, cts.Token)`，异常走 `RaiseError`，`finally` 里「仍是当前泵才清空」`_pipelineCts` 并释放 `cts`。

### 2.3 与事件泵互斥

`StartSpanPump` 的启动条件补一个门禁：

```csharp
if (_connection != null && OnClientReceiveSpan != null && _inputHandler == null)
    StartSpanPump(_connection);
```

即：配置了输入处理器时，即使订阅了 `OnClientReceiveSpan` 也不启动事件泵，`OnClientReceiveSpan` 对该连接不再触发。理由是两者读同一个 `PipeReader`，并发读会抢数据并触发 Pipe 的非法并发读。**不静默**：这一条写进 README，并有用例（`--client` 场景里订阅 `OnClientReceiveSpan` 断言计数为 0）。

### 2.4 断开与取消

`Disconnect(Exception)` 在收口连接门面之前先 `StopPipeline()`：

- `StopPipeline()` 只对当前 `_pipelineCts` 置位取消，不释放（取消源由泵的 `finally` 释放），重复调用安全；
- 顺序是先取消再 `connection.Dispose()`，避免泵在已完成的 `PipeReader` 上继续读；
- 泵侧把取消当正常收口，不 `RaiseError`；
- 服务端 `Disconnect(userToken)` 的对手语义不变。

### 2.5 建链失败

`_connectionOpened` 是用户代码。它抛异常时说明通道没登记成功，继续启动输入处理器只会放大错误，因此：

- `RaiseError(userToken.ID, ex)` 上报；
- `Disconnect(ex)` 收口；
- `StartPipelineIfNeeded()` 返回 `false`，调用方不再启动泵，并把 `_connectCallBack` 报成 `SocketError.SocketError`；
- 明文与 TLS 两个分支都要 `_connectEvent.Set()`，否则 `Connect()` 的等待方会挂住。

### 2.6 配置校验

`SocketOptionBuilder.Build()` 的三条规则天然覆盖客户端，无需新增分支：

1. 配 `InputHandler` / `ConnectionOpened` 必须 `.UseStream()`；
2. `InputHandler` 必须与 `ConnectionOpened` 成对（否则拿不到通道登记信息）；
3. 违反任一条抛 `InvalidOperationException`，消息写明缺哪一步。

客户端不再有「构造即抛 `NotSupportedException`」这条路径。

## 三、使用方胶水

`SAEA.Sockets.Pipeline` 不引用 `SAEA.Sockets`（保留「可脱离 socket 复用」），因此 `IServerSocket` / `SaeaClientSocket` 到 `IRawByteStream` 的适配仍放在使用方。演示工程 `Src/SAEA.Sockets.PipelineTest/SocketStreamAdapter.cs` 现在有两套构造：

- `SocketStreamAdapter(channelId, input, flow, IServerSocket server)`：服务端，内部转成发送委托；
- `SocketStreamAdapter(channelId, input, flow, Func<ReadOnlyMemory<byte>, IDisposable, Task> send)`：客户端与服务端通用。

客户端侧只需把发送委托接到 `client.SendAsync(data, owner)`：客户端重载会按需复制并在完成后释放 `owner`，适配器不接管所有权，`owner` 的原样传递是契约。

## 四、可复制的客户端装配

`Src/SAEA.Sockets.PipelineTest/Program.cs` 的 `RunClientScenarioAsync` 是一份完整可跑的实现：

- `.UseStream()` + `.UseConnectionOpened(...)` 建 `IChannel` 并登记到 `IPipelineHost`；
- `.UseInputHandler(async (userToken, input, flow, token) => { ... })` 里取通道、建 `SocketStreamAdapter`、`new ChannelPipelineExecutor(...)`、`Attach(pipeline)`、`Start()`，然后 `await executor.Completion`；
- 入站请求用 `client.SendAsync` 发手工编码帧（只验证接管读循环）；出站回执用 handler 里的 `ctx.WriteAsync(entity)`，走 `EnvelopeWrapHandler` → `CodecEncoderHandler` → `FrameEncoderHandler` → 适配器发送委托 → `client.SendAsync(data, owner)`，服务端读到这一帧即证明发送委托接通；
- 断开时执行器的 `Completion` 完成、`IPipelineHost` 中移除会话。

入口：`--client`（明文）与 `--client-tls`（TLS，验证握手完成前不启动、握手后首帧可达）。

## 五、已知限制

- `GetStream()` 与 `UseInputHandler` 互斥：配置处理器后 `PipeReader` 归泵独占，调用方不能再对同一连接读流（写不影响）。
- 客户端一次连接对应一个输入处理器；重连会新建 `_connection` 与 `_pipelineCts`，旧泵在 `finally` 里因引用不等而不会误清新令牌。
- 客户端链不提供「按会话分发」：单连接单通道，多路复用由业务在链上层做。

## 六、验证

| 项 | 入口 | 结论 |
|---|---|---|
| 客户端明文接入 | `dotnet run --project Src/SAEA.Sockets.PipelineTest -c Debug -- --client` | PASS，`OnClientReceiveSpan` 计数为 0，出站心跳经链回写成功 |
| 客户端 TLS 接入 | `--client-tls` | PASS，握手后首帧由链解出，出站心跳经链回写成功 |
| 服务端既有路径 | `--socket`、`--tls`、`--all` | PASS，无回归 |
| 构建 | `dotnet build Src/SAEA.Sockets.sln -c Debug` | 0 errors |
| 单元测试 | `SAEA.Common.Tests` / `SAEA.MQTT.Tests` / `SAEA.FTP.Tests` / `SAEA.Sockets.Pipeline.Tests` | 全绿（详见提交说明） |