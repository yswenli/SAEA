# SAEA.MVC WebSocket 集成设计

## 意图与为什么

让使用 SAEA.MVC 的用户不用再单独起一个 WebSocket 端口：**同一端口、同一套 `/{controller}/{action}` 路由**，在 Controller 的 action 上挂一个 attribute 就能提供 WebSocket 服务，写法尽量贴近 ASP.NET Core（`app.UseWebSockets()` + `HttpContext.WebSockets.AcceptWebSocketAsync()`）。

现在的 SAEA.WebSocket（`WSServer`）是独立端口的服务器，用多端口、多进程、反代转发都在额外成本；而 MVC 用户手里的连接本来就在 28080 上。

## 目标

- `WSController` 基类 + `[WebSocket]` attribute，四个可 override 的生命周期方法：`OpenAsync` / `MessageAsync` / `CloseAsync` / `ErrorAsync`。
- `HttpContext.Current.WebSockets.AcceptWebSocketAsync()`，像 ASP.NET Core 一样显式接受连接。
- 升级请求走普通 MVC 路由（默认 `/chat/open`，也可用属性路由声明任意握手地址），参数绑定、过滤器、Session、异常处理照常可用。
- WS 入口支持属性路由：路径模板可以包含静态段与 `{name}` 路径参数，参数值从 `Request.Parmas` 读取，模板语法与匹配规则见 `2026-11-17-mvc-attribute-routing-design.md` 的属性路由设计。
- 复用 SAEA.WebSocket 的帧编解码（`WSCoder` / `WSProtocal`），不重复实现协议。
- 普通 HTTP action、SSE（`IEventStream`）、大文件（`IBigDataResult`）行为不变。

## 非目标

- 不改变 `WSServer` / `WSClient` 的独立端口用法（只修共享的 `WSCoder` 帧解码缺陷）。
- 不做 `permessage-deflate` 扩展协商。
- 不做同端口 TLS（`wss://` 交给反向代理或继续用独立 `WSSServer`）。
- 不做多端点共享一个连接。
- 不做主动心跳与重连（客户端负责；协议层 Ping/Pong 自动应答）。

## 用户可见 API

```csharp
// 默认入口：GET /chat/open（在 OpenAsync 上加 [Route] / [HttpGet] 可改写路径）
public class ChatController : WSController
{
    // 不 override 时基类默认实现就是 Accept 并立即返回
    public override async Task OpenAsync()
    {
        var ws = await WebSockets.AcceptWebSocketAsync();   // 等价于 HttpContext.Current.WebSockets.AcceptWebSocketAsync()
        await ws.SendAsync("welcome");
    }

    public override async Task MessageAsync(WSMessage msg)
    {
        var ws = WebSockets.Current;                        // 同一连接，Accept 之后一直可用
        if (ws == null) return;
        if (msg.IsText) await ws.SendAsync(msg.Text);
        else await ws.SendAsync(msg.Data);
    }

    public override Task CloseAsync(WSCloseReason reason) => Task.CompletedTask;

    public override Task ErrorAsync(Exception ex) => Task.CompletedTask;
}
```

- `[WebSocket]` 挂在 `WSController.OpenAsync` 上（虚方法，子类 override 后依然生效，路由查属性用 `GetCustomAttributes(true)`）。
- `WSMessage`：`Type`（`Text` / `Binary`）、`IsText`、`Text`（UTF-8 字符串）、`Data`（`byte[]`）。分片在解码层已经重组完，业务代码看不到 `Continuation`。
- `HttpContext.Current.WebSockets` 类型为 `IWebSocketsFeature`：
  - `bool IsWebSocketRequest { get; }`
  - `IWebSocketConnection Current { get; }`（`AcceptWebSocketAsync` 成功后赋值；同一上下文重复 Accept 抛 `InvalidOperationException`）
  - `Task<IWebSocketConnection> AcceptWebSocketAsync(string subProtocol = null)`
- `IWebSocketConnection`：`string Id`、`WebSocketState State`（`Open` / `Closing` / `Closed`）、`Task SendAsync(string)`、`Task SendAsync(byte[])`、`Task CloseAsync(WSCloseReason reason = WSCloseReason.Normal)`、`void Abort()`。
- `WSCloseReason`：`Normal` / `GoingAway` / `ProtocolError` / `ServerShutdown` / `Abnormal`。前四个映射到 Close 帧状态码 `1000/1001/1002/1011`；`Abnormal`（1006）按 RFC 6455 **不允许出现在 Close 帧里**，仅作为 `CloseAsync` 回调的入参（对端 RST / 裸断开时），要主动关闭时用 `ServerShutdown`。
- `WSController` 额外提供 `protected IWebSocketsFeature WebSockets`（转发 `HttpContext.Current.WebSockets`），省掉每次写上下文。

## 数据流

```
浏览器  GET /chat/open  (Upgrade: websocket)
  → HttpSocket 收到请求 → HttpCoder.GetRequest → WebHost.OnRequested
  → MVC HttpContext.HttpHandle → 路由 /chat/open → MvcInvoker → ChatController.OpenAsync
  → WebSockets.AcceptWebSocketAsync()
       校验 Upgrade/Connection/Sec-WebSocket-Version/Sec-WebSocket-Key
       写 101 Switching Protocols（原始字节，不经 HttpResponse.ToBytes）
       HttpCoder 切 raw 模式：后续字节不再按 HTTP 解析
       创建 MVCWebSocketConnection（WSCoder + 上下文 + 控制器实例）
  → OpenAsync 返回 → HttpHandle 检测到已升级：不写响应、不 End、不 Dispose 上下文
  → 后续字节 → WSCoder.Decode → 按 opcode 分发：
       Text/Binary/Continuation → 串行 → MessageAsync(WSMessage)
       Ping                      → 自动回 Pong（不进业务代码）
       Pong                      → 忽略
       Close                     → 回 Close → CloseAsync(reason) → 断开、释放
       解码/业务异常              → ErrorAsync(ex) → 断开、释放
```

## 改动点

### SAEA.Http（薄抽象 + 升级口）

新增 `Model/IWebSocketsFeature.cs`、`Model/IWebSocketConnection.cs`、`Model/IWebSocketTransport.cs`。`WSMessage` 不放在这里（消息类型属于 MVC）。

- `IHttpContext` 增加 `IWebSocketsFeature WebSockets { get; }`（`HttpContextBase.Current` 的静态返回类型就是 `IHttpContext`，不加在接口上，用户写的 `HttpContext.Current.WebSockets` 编译不过）。
- `HttpContextBase` 提供默认实现 `NotSupportedWebSocketsFeature`（`IsWebSocketRequest` 可用，`AcceptWebSocketAsync` 抛 `NotSupportedException`）。
- **跨程序集拿不到 token，升级口要用 transport 收口**：`HttpResponse.UserToken` 是 `protected`、`HttpResponse.WebHost` 是 `internal`、`HttpCoder` 是 `internal`，仓库里也没有 `InternalsVisibleTo`。所以不要让 MVC 持有 `IUserToken`：
  - `interface IWebSocketTransport { string SessionId { get; } void Send(byte[] data); void SetNoTimeout(bool noTimeout); void Disconnect(); }`
  - 由 `HttpResponse.Upgrade` 返回，内部持有 `IWebHost` 与 `IUserToken`；MVC 只依赖这个接口，既不用公开 `UserToken`，也不用加 `InternalsVisibleTo`。
- `HttpResponse`：
  - `bool IsUpgraded { get; }`、`bool IsHandled { get; }`（已直接写过原始响应）
  - `public IWebSocketTransport Upgrade(byte[] handshake, Action<byte[]> onData, Action<Exception> onError)`
    —— 用 `WebHost.Send` 写 101（不走 `ToBytes`/压缩）→ `((HttpCoder)UserToken.Coder).UpgradeRaw(onData, onError)` → 标记 `IsUpgraded` → 返回 transport。
    **顺序必须是「先发 101，再切 raw」**：`SendAsyncRaw` 用 `userToken.WaitWrite` 串行化发送，101 先入队后，后续 WS 帧一定排在它后面；反过来就可能先发帧、后发 101。
  - `Upgrade` 里要判 `UserToken == null`（未连接的上下文 `Response.Init` 没跑过，`WebHost`/`UserToken` 都是 null），直接走失败分支，不能强转 `UserToken.Coder`。
  - `public bool WriteRaw(byte[] data, bool close)`：给 400/426 用，写原始字节并标记 `IsHandled`；`close=true` 时再 `WebHost.Disconnect`。必须 public（调用方在 SAEA.MVC）。
  - `Send()` / `SendHeader()` / `SendData()` / `SendEnd()` / `End()` 在 `IsUpgraded || IsHandled` 后为 no-op（避免误写 200 响应；`SendData`/`SendEnd` 是 SSE/大文件路径，同样不能漏）。
- `HttpCoder`（`internal`，`HttpResponse` 同程序集直接调用）：
  - `void UpgradeRaw(Action<byte[]> onData, Action<Exception> onError)`：置 raw 标记，并**立即把 `_cache` 里剩余字节交给 `onData` 后清空**。101 和客户端首帧可能落在同一个 TCP 段里：`GetRequest` 一次会把能解析的请求全部返回，帧字节只能靠 drain 拿回来，不 drain 就是丢首帧。
  - `GetRequest` 在 raw 模式下**不** `AddRange`，直接把本次 `data` 交给 `onData` 并返回空列表。异常包装放在 `UpgradeRaw` 内：捕获 `onData` 的异常 → 回调 `onError` → 不再向外抛（否则 `_serverSokcet_OnReceiveSpan` 的 `catch` 会再 `Disconnecte` 一次，变成二次关闭）。
  - **`Clear()` 必须复位 raw 标记与回调**：`UserTokenPool.Enqueue` 回收 token 时会调用 `Coder.Clear()`；不复位的话该 token 被复用时所有 HTTP 请求都会被当成 WS 帧，属于灾难性串话。
  - `GetRequest` 解析出一条带 `Upgrade: websocket` 的请求后**立即返回**，不得继续消费后面的字节。否则首帧可能被当成下一个 HTTP 请求解析并从 `_cache` 移除，`UpgradeRaw` 再 drain 也拿不回来。
  - `GetRequest` 的 `catch` 分支现在逐字节 `RemoveAt(0)`；如果粘在请求后的帧数据恰好含 `\r\n\r\n`，会被误当请求头并逐字节侵蚀。修正为「本轮已成功解析出至少一个请求后，解析失败就保留缓存并返回」。
- `WebHost`（只改具体类，**不动 `IWebHost` 接口**，避免多破一个公开契约）：
  - `public event Action<string, Exception> OnDisconnected`，构造函数里订阅 `_httpServer.OnDisconnected`。
  - `public void SetNoTimeout(string id, bool noTimeout)` → `_httpServer.SetNoTimeout(...)`；transport 通过它生效（若 `IWebHost` 不是 `SAEA.Http.WebHost`，静默退化为不豁免）。
  - `_serverSocket_OnRequested` 进入时先看 `(userToken.Coder as HttpCoder)?.IsRaw == true`，是就直接 return。这不是纯保险：`GetRequest` 会把同一段里能解析的请求一次性全部返回，**同段的第二个请求会在第一个请求升级之后才被分发**，不跳过就会往 WS 流里写一个 HTTP 响应。
  - 现在的 `using IHttpContext httpContext = ...` 必须改成“交权”写法：`HttpHandle` 返回后，只有 `!Response.IsUpgraded` 才 `Dispose()`；已升级上下文由 `MVCWebSocketConnection.CloseOnce` 负责 `Dispose`（`HttpContextBase.Dispose` 只清 `Request`/`Response`，不碰 socket）。
  - `HttpContextBase` 构造函数里 `CallContext<IHttpContext>.SetData("ContextBase.Current", this)` 只在创建时设置一次；WS 接收/分发发生在 IOCP 线程或线程池任务上，**分发前必须重新 `SetData`**，否则 `HttpContext.Current` 会是 null 或别的连接。
- `HttpContextBase.HttpHandle`：`Response.IsUpgraded || Response.IsHandled` 时跳过响应收尾（与 `IBigDataResult` / `IEventStream` 同一豁免点）。
- 握手校验读 `Request.Headers`（`Dictionary<string,string>`，`StringComparer.OrdinalIgnoreCase`，由 `HttpRequest.Init` 直接引用 `HttpMessage.Headers`）：
  - `Upgrade` 含 `websocket`；`Connection` **包含** `upgrade`（浏览器常发 `keep-alive, Upgrade`，不能相等比较）；`Sec-WebSocket-Version == 13`；`Sec-WebSocket-Key` 非空。
  - `RequestDataReader.GetRequestHeaders` 用 `row.Split(':')` 且**只接受恰好两段**，否则值写成空串；上述几个头的值都不含冒号，不受影响，但不要依赖带冒号的 header 做校验。
- `RequestDataReader` 的 request-target 归一化：`SAEA.WebSocket` 自带的 `WSClient` 发的是 absolute-form（`GET ws://host:port/chat/open HTTP/1.1`），原实现把整串赋给 `RelativeUrl`，路由匹配不到。新增 `NormalizeTarget`，把 `scheme://host` 前缀裁掉只留路径（`http://` / `ws://` 都适用）；相对形式（`/chat/open`）原样返回，不影响既有请求。
- `HttpBase` 的 `HeaderStr` / `Method` / `Url` / `RelativeUrl` / `Query` / `Forms` / `Parmas` / `Headers` / `ContentType` / `Body` 都是 **public** 属性/字段（`Parmas` 可整体替换）。握手校验可以直接读写 `Request.Headers`；参数绑定依赖 `MVC.HttpContext.HttpHandle` 先把 `Query`/`Forms` 合并进 `Parmas`，再由 `ParamsHelper.FillPamars` **只按 `ParameterInfo.Name`** 从 `NameValueCollection` 取值（不认 `[FromXxx]`、不认路径参数；名字取不到非引用类型会得到 0/空值）。
- 断开通知链路（现在完全缺失）：
  - `IHttpSocket` 只有 `OnRequested` / `OnError`，需加 `event Action<string, Exception> OnDisconnected` 与 `void SetNoTimeout(string id, bool noTimeout)`；接口是 `internal`，不算公开 API 变更。`HttpSocket` / `HttpSocketDebug` 两个实现同步补齐。
  - `IocpServerSocket.Disconnect(userToken, ex)` **先捕获 `sessionId = userToken.ID` 再 `SessionManager.Free`**，所以 `OnDisconnected` 拿到的是有效 id，不是被 reset 后的 null；且只有 `Free` 返回 `true` 才触发。`Free` → `UserTokenPool.Enqueue` 会 `userToken.Coder.Clear()` 并把 `userToken.ID` 置 null。
  - `IocpServerSocket.End` 发完会调 `Disconnect`，所以普通 `Connection: close` 响应也会触发 `OnDisconnected`。MVC 必须用「已升级连接表」按 sessionId 过滤，未知 id 直接忽略。
  - `HttpSocket` 的 `catch` 会 `unpacker.Clear()` 并 `Disconnecte(ut)`；`HttpSocketDebug` 的 `catch` 不 `Clear` 但同样 `Disconnecte(ut)`。raw 模式下回调异常必须在 transport 内吸收，否则会二次关闭。
  - `IocpServerSocket.Disconnect` 在 `Free` 返回 `false`（token 池已满）时**不会**抛 `OnDisconnected`，这是既有行为；连接表不能只靠断开事件清理（见「已知限制」）。
- 超时豁免：
  - `SessionManager.Active(id)` 现在固定用 `_freeTime`；它被 `IocpServerSocket.ProcessReceive`（每次收到数据）和 `SendAsyncRaw`（每次发送）调用，因此**只有完全空闲超过 `FreeTime` 才会被清理**。
  - `MemoryCache.Get` 命中过期项时会 `TryRemove` 并触发 `OnChanged(false)`，`SessionManager` 把它转成 `OnTimeOut` → `IocpServerSocket.Disconnect`；清理定时器每 `(int)freetime.TotalSeconds`（默认 180s）扫一次。`Set` 里 `timeOut.TotalSeconds < 1` 直接写 `DateTime.MaxValue`，所以 `TimeSpan.Zero` 就是“永不过期”。
  - `HttpSocket` 的 `FreeTime = timeout * 1000`（MVC 配置的 `Timeout`，默认 180s）；`HttpSocketDebug` 构造里**没有** `SetFreeTime`，始终用 `SocketOption.FreeTime` 默认的 180s，与传入的 `timeout` 无关。两者都要支持 `SetNoTimeout`，但测试时要知道 Debug 模式的超时行为不同。

### SAEA.Sockets（长连接不被 180s 清理）

- `SessionManager.SetNoTimeout(string id, bool noTimeout)`：内部用 `ConcurrentDictionary<string, byte>` 记录豁免集合；
  `Active(id)` 对豁免会话传 `TimeSpan.Zero`（`MemoryCache.Set` 里 `< 1s` 即 `DateTime.MaxValue`），非豁免仍用 `_freeTime`。
- `SetNoTimeout(id, true)` 要**立即** `Active(id)` 一次，把当前缓存项改成 `DateTime.MaxValue`；否则下一个清理周期（默认 180s）之前仍按旧过期时间被删。
- `SetNoTimeout(id, false)` 先移除标记，再 `Active(id)` 一次把过期时间恢复成 `_freeTime`（会话已不存在时 `Active` 自然 no-op）；这样即使调用方在 `CloseAsync` 里先清标记、稍后才断开，也不会留下永不过期的残项。
- `Free` 时移除豁免标记（`Free` 内 `userToken.ID` 还在，`UserTokenPool.Enqueue` 之后才置 null），避免 ID 复用泄漏。
- `SessionManager.Active` 是 `void`，不抛异常；`MemoryCache.Active` 内部先 `Get`，命中过期项会触发 `OnTimeOut` → `Disconnect`，这是既有行为。

### SAEA.MVC（主战场）

- `SAEA.MVC.csproj` 增加 `ProjectReference` 到 `..\SAEA.WebSocket\SAEA.WebSocket.csproj`（依赖方向：MVC → WebSocket；Http 不认识 WebSocket）。
- `WebSocketAttribute : Attribute`：`[AttributeUsage(AttributeTargets.Method)]`（默认 `Inherited = true`），无参。
- `WSController : Controller`：
  - `[WebSocket] public virtual Task OpenAsync()`（默认 `return WebSockets.AcceptWebSocketAsync();`）
  - `public virtual Task MessageAsync(WSMessage message)`
  - `public virtual Task CloseAsync(WSCloseReason reason)`
  - `public virtual Task ErrorAsync(Exception exception)`
  - `protected IWebSocketsFeature WebSockets`（`=> HttpContext.Current.WebSockets`）
  - `[WebSocket]` 挂在基类虚方法上；子类 override 后 `GetCustomAttributes(true)` 仍能取到（方法特性继承），子类不 override 时 `RouteTable` 回落到基类方法也能取到。
- `WebSocketsFeature : IWebSocketsFeature`（每个 HTTP 上下文一个，懒创建）：持有所属 `IHttpContext`，`Attach(WSController)` 由 `MvcInvoker` 在调用前设置；`AcceptWebSocketAsync` 内部完成握手、101、`HttpResponse.Upgrade`、连接对象创建、`SetNoTimeout(true)`，成功后把连接赋给 `Current` 并登记到全局连接表。`IsWebSocketRequest` 只读 `Request.Headers`（`Upgrade: websocket` + `Connection` 含 `upgrade` + `Sec-WebSocket-Key` 非空），不产生副作用，用于 `Controller` 里提前判断。
- 101 响应由 MVC 自己拼（不复用 `WSUserToken.GetReplayHandShake`）：后者用正则扫原始字节、且**只要客户端带了 `Sec-WebSocket-Protocol` 就固定回 `SAEA.WebSocket`**，与本期“只在调用方显式传 `subProtocol` 时回显”的约定冲突。MVC 直接用 `Request.Headers["Sec-WebSocket-Key"]` 算 `Base64(SHA1(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"))`，响应头固定为 `HTTP/1.1 101 Switching Protocols` + `Upgrade: websocket` + `Connection: Upgrade` + `Sec-WebSocket-Accept: ...`，`subProtocol` 非空时再追加 `Sec-WebSocket-Protocol`，以 `\r\n\r\n` 结尾。
- `MVCWebSocketConnection : IWebSocketConnection`：持有 `IWebSocketTransport`（不直接拿 `IUserToken`）、`WSCoder`、所属 `IHttpContext`、`WSController` 实例；负责分发与关闭。
- `HttpContext`：`override IWebSocketsFeature WebSockets`。
- **控制器发现**：`AreaCollection.RegistAll` 的三处 `BaseType.FullName == "SAEA.MVC.Controller"`（第 63/86/98 行，无参 + 两个带命名空间的重载）都要改成「基类链上有 `Controller` 且自身非 abstract」；`WSController` 是 abstract，自然被排除。
- `RouteTable`：GET 分支用 `DeclaredOnly`（第 72 行），子类不 override `OpenAsync` 时查不到；控制器是 `WSController` 派生时回落到基类同名方法（且只回落到 `WSController` 声明的那一层，不要捡 `Controller` 的公开方法）。POST 分支原本用 `GetMethods()` 但不认 `Async` 后缀，`POST /chat/open` 会查不到 action 抛 500；现在 GET/POST 共用同一个 `ResolveActions` 回退规则（仅 `BindingFlags` 不同），POST 能正常进入握手校验并返回 400。
- 普通 HTTP 访问 `WSController` 上**没有** `[WebSocket]` 的方法（`MessageAsync` / `CloseAsync` / `ErrorAsync`）→ 400：在 `InvokeResult` 里对 `routing.Instance is WSController && routing.Action` 无 `WebSocketAttribute` 直接返回 `ContentResult(..., BadRequest)`，不进入 action。`WSController` 子类不要放普通 HTTP action，需要普通 action 请用独立控制器（`AreaCollection` 只发现继承 `Controller` 的类型，`WSController` 本身是 abstract，不影响普通路由）。
- `MvcInvoker.MethodInvoke`：当前只识别 `Task<T>`（第 227 行），非泛型 `Task` 会被当成同步返回值 → `Task.Run` 包一层后 `task.Result` 拿到的是内层 `Task` 对象，最终变成 `JsonResult(Task)`，`OpenAsync` 根本没被等待。修成：`typeof(Task).IsAssignableFrom(action.ReturnType)` 统一走 `Wait(timeoutMs)`；`Task<T>` 取 `Result`，非泛型 `Task` 的 `data = null`。`Timeout` 用 `WebConfig.Timeout * 1000`，默认 180s。
- `MvcInvoker.InvokeResult`：在过滤器之前把 `routing.Instance as WSController` 交给 `HttpContext.Current.WebSockets as WebSocketsFeature`（`Attach`），后续消息分发要靠这个实例；同时把 `[WebSocket]` action 的返回值统一当 `EmptyResult`（默认 `OpenAsync` 返回 `Task`，否则会生成一个 `JsonResult(null)`）。
- `MvcInvoker` 还要为 `[WebSocket]` action 包一层异常处理：`Task.Wait` 抛的是 `AggregateException`，解包 `InnerException` 后交给 `WebSocketsFeature.HandleActionException(ex)`（已升级则 `ErrorAsync` 后关闭；未升级则 `Response.WriteRaw` 500 后关闭），不要让异常落进 `HttpContext.HttpHandle` 的 `OnException.Invoke`（那里没判空，见错误处理）。
- `RouteTable` 有个**既有 bug 会被 `[WebSocket]` 撞上**：GET 分支第 132 行 `var filters = actionAttrs.ToList().ConvertAll(b => b as IFilter)` 把 action 上**所有** attribute 都转成 `IFilter`（`actionAttrs` 是未过滤的全集），非 filter 的变成 `null`，紧接着 `OrderBy(b => b.Order)` 直接 NRE。所以 `[WebSocket][Auth]` 这种组合会在建路由时炸。改成只转换 `filterAttrs`（第 128 行那个已过滤的列表）。POST 分支第 200/204 行本来就是先过滤再转换，不用改。

## 连接、并发与上下文

- `WSMessage` / `WSCloseReason` 放在 `SAEA.MVC`（命名空间 `SAEA.MVC`），不要放进 `SAEA.WebSocket`，避免把 MVC 的会话模型耦合进协议库。
- 每个连接一个 `WSCoder`（内部 `List<byte>` 缓冲）。关闭时不主动 `Clear()`：IOCP 线程可能仍在 `Decode`，并发清空内部缓冲会造成数据竞争；连接对象整体交由 GC 回收。
- **接收回调不做业务**：`OnData` 只把字节喂给 `WSCoder.Decode`，解出的帧压入该连接的待处理队列；每条连接一个常驻异步泵（`SemaphoreSlim(1,1)` + 队列）按序 `await` 分发，保证 `MessageAsync` 顺序且不阻塞 IOCP 接收线程。
- **`DecodedFrames` 的帧不能跨批次持有**：`Frames` 是 `ReadOnlySpan`，`Dispose()` 会逐帧 `Dispose` 并把 `WSProtocal` 的池化缓冲归还。所以入队前必须把 payload 复制成队列项自己的 `byte[]`，然后立刻 `frames.Dispose()`；不能把 `WSProtocal` 引用直接放进队列。
- 队列有上限（默认 1024 帧或 4MB，超出即视为异常连接，走 `ErrorAsync` 后关闭），防止慢消费者把内存吃满。
- 分发前恢复 `HttpContext.Current`（`CallContext.SetData("ContextBase.Current", context)`），使 action 里能继续用 `Request` / `Session` / `WebSockets`；分发结束后置回 null，避免线程池线程长期持有上下文。
- Controller 实例按路由缓存复用（现状），**连接状态只能放连接对象**；同一控制器的多个连接会并发进入同一个实例方法，文档明确提示。
- 发送：`IWebSocketConnection.SendAsync` 用 `WSProtocal.WriteTo`（**不掩码**，服务端不得掩码）写到 `PooledBufferWriter` 得到 `byte[]`，再 `IWebSocketTransport.Send(bytes)`（内部用捕获的 `IUserToken` 调 `IWebHost.Send`，底层 `IServerSocket.SendAsync` 自带写信号量，天然串行）。不要用 `WSCoder.Encode`：它走 `WriteMaskedTo`，是客户端方向。
- 断开/关闭统一走一个幂等的 `CloseOnce(reason)`：回 Close 帧（能发就发）→ `transport.SetNoTimeout(false)` → 从连接表移除 → 回调 `CloseAsync(reason)` → `transport.Disconnect()` → 释放一份上下文引用。上下文用引用计数（泵一份、收尾一份），最后一份释放时才 `Dispose`，避免收尾发生在 IOCP 线程时把仍在 `MessageAsync` 里的上下文释放掉。`OnDisconnected` 触发的被动关闭也走同一个方法，只是不再回 Close 帧。
- 已升级连接表用 `ConcurrentDictionary<string, MVCWebSocketConnection>`，key 是 `IWebSocketTransport.SessionId`；`OnDisconnected` 里 `TryRemove` 成功才处理，未知 id 直接忽略。断开可能早于注册到达（`TryRemove` 落空），`Register` 写入后会再查一次 `transport.IsConnected`，已断则立即补收尾。

## 已知限制（写进 README）

- `HttpSession` 是 20 分钟滑动过期且 `Refresh()` 在 SAEA.Http 内部；WebSocket 连接持续期间只持有上下文快照，**`Session` 不会随 WS 消息刷新**。需要长会话状态请用连接对象自己的字段/字典。
- `Request` 是握手那一刻的快照（headers/cookies/query 都不再变化），`Body` 之后也没有意义；WS 消息的数据只能走帧回调。
- Controller 实例共享（既有限制），连接相关状态不能放字段。
- 同一 MVC 应用内所有 WS 连接共享 `MaxConnects` 配额；一条长连接会一直占一个 userToken 槽位，需要按实际并发调大。
- **`OpenAsync` 必须“接受连接后立刻返回”**：`MvcInvoker.MethodInvoke` 是同步 `task.Wait(timeoutMs)`，`timeoutMs = WebConfig.Timeout * 1000`（默认 180s）。在 `OpenAsync` 里跑 `while(true) await ReceiveAsync()` 这种长循环会超时；超时后已升级的连接不再写 408，而是 `ErrorAsync` + `ServerShutdown` 关闭连接（写 HTTP 408 会污染 WS 流）。长活逻辑放 `MessageAsync`。
- `HttpSocketDebug` 的 `FreeTime` 固定 180s（构造里没 `SetFreeTime`），与 `timeout` 参数无关；`SetNoTimeout` 仍然有效，但测试 Debug 模式时不要用 `Timeout` 配置推断空闲清理时间。
- `IocpServerSocket.Disconnect` 在 `SessionManager.Free` 返回 `false`（token 池满）时不会触发 `OnDisconnected`，连接表可能残留；`CloseOnce` 必须主动移除，另加一个低频的连接表扫描（5 分钟）清理 `State == Closed` 或 `transport.IsConnected == false` 的条目，并对仍在 `Open` 的死连接补一次收尾。
- `HttpCoder` 的 raw 标记是**挂在 userToken 上**的；`UserTokenPool.Enqueue` 会 `Coder.Clear()`，实现时必须复位，否则 token 复用时普通 HTTP 请求会被当成 WS 帧。
- 服务端不做 `permessage-deflate`、不做 `wss://`、不做主动 `Ping`；需要心跳时由客户端发 Ping 或业务层约定。

## 错误处理

| 场景 | 行为 |
| --- | --- |
| 普通 GET 访问 `[WebSocket]` action | 由 `AcceptWebSocketAsync` 直接写 `400`（`Response.WriteRaw`），不抛到 MVC 异常链 |
| 缺 `Sec-WebSocket-Key` / 方法不是 GET（含 `POST/PUT/DELETE /{controller}/open`） | 同上 `400` |
| `Sec-WebSocket-Version` 不是 13 | 写 `426 Upgrade Required` + `Sec-WebSocket-Version: 13` |
| 已升级后 `OpenAsync` 超过 `WebConfig.Timeout` | `ErrorAsync(TimeoutException)` → 以 `ServerShutdown` 关闭连接，不再写 HTTP `408` |
| 重复 `AcceptWebSocketAsync` | `InvalidOperationException`（已升级后调用） |
| `ErrorAsync` 之前握手已失败（`IsHandled`） | 不再二次写响应 |
| 帧解码异常 | `ErrorAsync(ex)` → 关连接 |
| 业务方法异常 | `ErrorAsync(ex)` → 关连接 |
| `ErrorAsync` 自身异常 | 记日志，直接关闭（不递归） |
| 对端断开 / RST | `SAEA.Http.WebHost.OnDisconnected` → `CloseAsync(WSCloseReason.Abnormal)` → 释放 |
| 服务端主动关闭 | `IWebSocketConnection.CloseAsync(reason)` 发 Close 帧后断开 |

> 说明：`HttpContext.HttpHandle` 里 `OnException.Invoke` 没有判空，未挂异常事件时会 NRE、连接被静默丢弃 —— 这也是握手失败**不走异常链**、直接写原始响应的原因。

## 配置与版本

- 不需要新的应用配置开关：只有挂了 `[WebSocket]` 的 action 才涉及升级。
- `MaxConnects` 复用 MVC 现有配置。
- **版本号不在本次改动范围内**：`csproj` 的 `Version`、文件头 banner 里的版本号都不动，由维护者手工处理。

## 编码器修复（已定：方案 A，修 `WSCoder`）

`WSCoder.Decode` 目前有多处会直接影响本特性，全部在本期修掉：

1. 补 `payloadLen == 127` 分支（8 字节大端长度）—— 现在超过 65535 字节的帧会被当成 127 字节解析，整条流错位。`len` 超过 `int.MaxValue` 直接抛异常（拒绝分配）。
2. 补分片重组：`opcode == 0`（`Continuation`）累积到上一条消息，直到 `FIN`；控制帧（Ping/Pong/Close）允许穿插且不参与重组。首帧就是 `Continuation`、或上一条消息未结束又来新的数据帧 → 协议错误。
3. `catch` 里不再静默吞异常，改为上抛（`Decode` 的调用方负责转 `ErrorAsync`）。现在的空 `catch` 还会让 `DecodedFrames` 里的半成品帧无人释放。
4. 主循环条件 `while (_buffer.Count > 3)` 改为 `> 1`，否则 2~3 字节的帧（空 Close / 空 Pong）永远解析不出来。每轮成功解析必须推进 `_buffer`，否则抛异常防死循环。
5. 删除/改写第 91 行 `if (payloadLen + 2 > _buffer.Count) break;` 这个前置判断：`payloadLen == 126/127` 时它把头部长度算错，大帧会被永久卡住；改为每个分支在读到自己的头部后再判完整性。
6. 校验 `RSV1/2/3 == 0`（没协商扩展）、客户端帧必须 `MASK == 1`（否则按协议错误关闭）、控制帧 `FIN == 1` 且 `payloadLen <= 125`。
7. `DecodedFrames.Add` 的约定：`Decode` 返回的批次由调用方 `Dispose`；`WSProtocal.IsPooled` 必须与 `MemoryPoolManager.Rent` 严格配对（当前只有 `len > 4KB` 才池化）。`MemoryPoolManager` 分 4KB / 64KB / 1MB 三级，`Return` 必须传原始请求长度（`WSProtocal.Dispose` 现在传的是 `_buffer.Length`，对 4KB 以下的 `new byte[]` 不受影响，但池化分支要保证租还一致）。

`WSUserToken.GetReplayHandShake` / `AnalysisHandSharkReply` 丢弃“和 101 粘在一起的首帧”、以及它无视客户端请求固定回显 `SubProtocolType.Default`，都是 `WSServer`/`WSClient` 独立用法里的问题，不在本特性路径上（MVC 自己拼 101、自己接管首帧），本期不动。

## 兼容性

- `IHttpContext` 增加 `WebSockets`：对**直接实现这个接口的外部代码**是破坏性变更；继承 `HttpContextBase` 的实现不受影响。`IWebHost` 接口**不加**成员（`OnDisconnected` / `SetNoTimeout` 只加在具体类 `SAEA.Http.WebHost` 上），因此 `IWebHost` 的既有实现者不受影响。发布说明需注明 `IHttpContext` 这一点。
- `AreaCollection` 的控制器发现放宽到「基类链上有 `Controller`」：历史上多层继承的控制器从「不被发现」变成「被发现」，属于修 bug，但会让原本静默失效的类突然可路由，发布说明提一句。
- 其余均为新增类型，无行为变更。

## 实现顺序

1. `SAEA.WebSocket/Model/WSCoder.cs`：先修解码（64 位长度、分片重组、异常上抛、`> 1`、头部完整性、RSV/MASK 校验）。这一步独立于 MVC，可先用 `WSServer` + `WSClient` 回归。
2. `SAEA.Http`：`IWebSocketsFeature` / `IWebSocketConnection` / `IWebSocketTransport`、`HttpResponse.Upgrade` / `WriteRaw`、`HttpCoder.UpgradeRaw` + raw 复位、`IHttpSocket`/`HttpSocket`/`HttpSocketDebug` 的 `OnDisconnected` 与 `SetNoTimeout`、`WebHost.OnDisconnected` / `SetNoTimeout` / `_onRequested` 守卫 / 上下文交权。
3. `SAEA.Sockets`：`SessionManager.SetNoTimeout` + `Active` 分支 + `Free` 清理豁免。
4. `SAEA.MVC`：`WebSocketAttribute`、`WSController`、`WSMessage`/`WSCloseReason`、`WebSocketsFeature`、`MVCWebSocketConnection`、`HttpContext.WebSockets`；再改 `AreaCollection`、`RouteTable`、`MvcInvoker`。
5. `SAEA.MVCTest`：`Controllers/ChatController.cs`、`wwwroot/websocket.html`，程序集老式 csproj 需手工加 `<Compile Include>` / `<Content Include>`。

## 验证清单

- `SAEA.MVCTest` 增加 `Controllers/ChatController.cs`（继承 `WSController`）与 `wwwroot/websocket.html` 测试页。
- 手测：握手成功、文本 echo、二进制 echo、>64K 大帧（64 位长度）、分片消息（`Continuation` 重组）、空 Close 帧、连续多帧、多客户端广播、客户端主动 close、服务端主动 close、非升级请求 400、`/chat/message` 直连 400、普通 action + SSE 回归。
- 用 `SAEA.WebSocket` 的 `WSClient` 做一次客户端兼容性验证。
- `SAEA.WebSocket` 原有测试路径回归一次（`WSServer` + `WSClient` 短消息、1MB 消息）。

## 留给实现的决定

- 子协议协商：`AcceptWebSocketAsync(subProtocol)` 仅在调用方显式传入时回显 `Sec-WebSocket-Protocol`，不做默认值。
- 是否提供按连接广播的辅助类（`WSConnectionManager`）：本期不做，用户可自行维护 `IWebSocketConnection` 列表。
- 心跳：不做服务端主动 Ping。

## 验证结果（2026-11-04）

编译：`SAEA.Http` / `SAEA.Sockets` / `SAEA.WebSocket` / `SAEA.MVC` / `SAEA.MVCTest` 全部 `-c Release` 0 错误。

`System.Net.WebSockets.ClientWebSocket` 冒烟（`SAEA.MVCTest`，端口 28080）：

| 用例 | 结果 |
| --- | --- |
| 普通 GET `/chat/open` | 400 |
| 普通 GET `/chat/message` | 400 |
| 握手 + welcome | 通过 |
| 文本 echo | 通过 |
| 二进制 echo（1~8 字节） | 通过 |
| 200KB 文本（64 位长度） | 通过 |
| 三段分片重组 | 通过 |
| 20 帧连续顺序 | 通过 |
| 两个客户端并发各 50 帧 | 通过 |
| 服务端主动 close（`CloseAsync(ServerShutdown)`） | 客户端收到 Close 帧（状态 1011） |
| 客户端主动 close | 通过 |
| 普通 action `/home/ping` | 200 `PONG` |
| 静态文件 `/index.html` | 200，11519 字节 |
| SSE `/eventstream/sendnotice`（JWT） | 200 `text/event-stream`，连续收到 `data:` 事件；SSE 期间普通请求仍正常 |
| 未 override `OpenAsync` 的派生类（`/echo/open`） | 回落基类实现，握手成功 |

`SAEA.WebSocket` 生态兼容：

| 用例 | 结果 |
| --- | --- |
| `WSClient` 连 `/chat/open`（absolute-form target） | 连接成功，welcome / 文本 / 1MB 二进制均一致 |

> 后续修复：`WSClient` 已改为发送 origin-form（`PathAndQuery`），见「聊天示例与关闭握手」。`NormalizeTarget` 仍保留 absolute-form 兼容。
| `WSServer` + `WSClient` 原有路径回归 | 文本 echo、1MB 二进制 echo 均通过 |

说明：现网 `SAEA.WebSocket` 的 `WSClient` 发送 absolute-form request-target，由此补了 `RequestDataReader.NormalizeTarget`；`WSCoder` 的解码修复同时被 `WSServer` 路径回归覆盖。

## 审查修复（2026-11-04）

深度审查后又修了 4 处：

| 问题 | 修复 |
| --- | --- |
| `NormalizeTarget` 把任意位置的 `://` 当 absolute-form，`/home/ping?url=http://x/y` 会被截成 `/y` | 只认开头为 `http://` / `https://` / `ws://` / `wss://` 的 target，相对形式原样返回 |
| `WSProtocal.Dispose` 按 `_buffer.Length` 归还池化缓冲，40KB 请求会因实际 64KB 被归到大型池 | 改传原始请求长度 `(int)BodyLength` |
| 握手期间先发布 `_connection` 再补喂早到字节，窗口内新帧可能插到早到帧前面 | 统一走 `_pending` 队列，`_draining` 串行排空，排空期间新数据继续入队 |
| README 声称断开统一由 `OnDisconnected` 收尾，未写 token 池满时的残留 | 中英文 README 补充「池满不触发通知 + 5 分钟空闲扫描兜底」 |

回归：

| 用例 | 结果 |
| --- | --- |
| `/home/ping?url=http://example.com/a/b` 与 `?next=ws://x/y` | 200 `PONG` |
| absolute-form `GET http://127.0.0.1:28080/home/ping` | 200 |
| 40KB 池化帧归还统计 | Medium rented/returned 各 +1，Large 不变 |
| 握手 + 两个早到帧同段发送 | 101 后按序收到 `welcome, early-1, early-2` |
| `ClientWebSocket` 全量冒烟 / 双客户端并发 / 服务端关闭 | 通过 |
| `WSClient` absolute-form 兼容 / `WSServer` 原有路径 | 通过 |
| `/home/ping`、`/index.html`、SSE | 通过 |

## 深度审查修复（第二轮，2026-11-04）

按 Standards + Spec 双轴审查清单修复：

| 问题 | 修复 |
| --- | --- |
| 空闲扫描只看 `State == Closed`，错过 `OnDisconnected` 的死连接永远留在表里 | `IWebSocketTransport` 增加 `IsConnected`；`Sweep` 回收「`Closed` 或 socket 已断开」的条目，并对仍为 `Open` 的死连接补一次 `HandleDisconnected` |
| `OnDisconnected` 可能早于 `Register` 到达，`TryRemove` 落空后死连接被注册进表 | `Register` 写入后复检 `IsAlive`，已断则立即移除并收尾 |
| `CleanupAsync` 可能由 IOCP 线程触发，此时泵仍在 `MessageAsync` 中，提前 `_context.Dispose()` 属 use-after-dispose | 上下文改为引用计数：泵与收尾各持一份，最后一份释放时才 `Dispose` |
| `CleanupAsync` 里的 `_coder.Clear()` 会与 IOCP 线程的 `Decode` 数据竞争 | 移除 `Clear()`，连接对象整体交给 GC |
| `WSCoder.MaxMessageSize` 仍是 64MB，与 4MB 队列预算矛盾 | MVC 侧收敛到 `MVCWebSocketConnection.MaxQueuedBytes`（4MB） |
| `POST /chat/open` 走 POST 分支查不到 `OpenAsync` → 抛异常变 500 | GET/POST 共用 `RouteTable.ResolveActions`，POST 进入握手校验返回 400 |
| 已升级后 `OpenAsync` 超时返回 `ContentResult(408)`，`HttpHandle` 因 `IsUpgraded` 静默丢弃，连接悬空 | 升级后超时改为 `ErrorAsync(TimeoutException)` + `ServerShutdown` 关闭 |
| `WebSocketState.Closing` 声明但从未使用 | 关闭/失败路径进入 `Closing`，收尾结束置 `Closed` |
| `NotSupportedWebSocketsFeature.IsWebSocketRequest` 只查 `Upgrade`，与 MVC 实现不一致 | 补齐 `Connection: upgrade` 与 `Sec-WebSocket-Key` 校验 |
| absolute-form 且无路径（`http://host?x=1`）时 `NormalizeTarget` 丢掉 query | 保留 query，补根路径 |
| 5 个新增 `SAEA.Http` 文件缺文件头 banner | 按仓库格式补齐 |
| 文档与实现不一致（`/echo/open` 验证行、README 空闲扫描表述、408 行为、`HttpSocketDebug` 180s） | 补 `EchoController` 使 `/echo/open` 可复现，README 与 spec 同步修正 |

回归：

| 用例 | 结果 |
| --- | --- |
| `POST /chat/open` / `PUT` / `DELETE` | 400（不再 500） |
| `GET /echo/open`（派生类未 override） | 101 + 握手成功 |
| `ClientWebSocket` 全量冒烟（握手/文本/二进制/大帧/分片/并发/双向 close） | 通过 |
| `WSClient` absolute-form 兼容 / `WSServer` 原有路径 | 通过 |
| `/home/ping`、`/index.html`、SSE、absolute-form 普通请求 | 通过 |
## 聊天示例与关闭握手（2026-11-04）

### ChatController 协议

`Src/SAEA.MVCTest/Controllers/ChatController.cs` 演示「同端口 + 同路由 + JWT 鉴权」的完整聊天服务端：

- 连接地址：`ws://host:28080/chat/open?token=<登录接口返回的 Authorization>`；不传 `token` 则以匿名身份连接，服务端分配 `guest-xxxxxxxx`。
- 鉴权：带 token 时 `OpenAsync` 先从 query 取 token，循环剥离多层 `Bearer ` 前缀，再用 `JWTPackage<UserInfo>.Parse(token, "yswenli")` 校验；失败直接 `WriteRaw` 401，不升级连接。身份统一取 `UserInfo.UserName`。匿名连接只允许 `whoami` / 文本或二进制回显 / `dm`，所有 `group.*` 操作返回 `error/login_required`。
- 非 GET / 非升级请求不进入鉴权，仍由握手校验返回 400。

客户端 -> 服务端（文本帧 JSON，`type` 字段）：

| type | 字段 | 说明 |
| --- | --- | --- |
| `whoami` | - | 返回当前用户（匿名连接附带 `anonymous=true`） |
| `group.create` | `group` | 建群，创建者自动入群 |
| `group.join` | `group` | 加群（自己声明即可，不做权限） |
| `group.leave` | `group` | 退群；空群自动删除 |
| `group.send` | `group`, `text` | 群发，广播给所有成员（含自己） |
| `group.list` | - | 列出所有群及成员 |
| `group.members` | `group` | 查询群成员 |
| `dm` | `to`, `text` | 私聊；对方不在线返回 `error/user_offline`，不存离线消息 |

服务端 -> 客户端：`welcome`、`whoami`、`group.created`、`group.joined`、`group.left`、`group.list`、`group.members`、`group.message`、`group.member_joined`、`group.member_left`（退群/掉线）、`dm`、`dm.sent`、`error`。

状态管理：

- 三张静态内存表：`user -> connection`、`connection.Id -> ChatUser`（名字 + 是否匿名）、`group -> 成员集合`；一把 `_sync` 锁保护，广播前取快照、锁外发送。
- 掉线（`CloseAsync`）自动退群并广播 `group.member_left(reason=disconnected)`；空群删除。
- 同一用户重复连接：新连接顶掉旧连接（旧连接 `GoingAway`），群关系按用户名保留。`CloseAsync` 用 `ws.Id` 校验归属，旧连接回调不会误踢新连接。

兼容：非 JSON 文本原样回显、二进制原样回显、`server-close` 触发 1011；4MB/1024 帧队列上限仍由连接层统一处理。

### 服务端关闭握手

现象：客户端主动关闭后，服务端回 Close 帧，但约一半的 `ClientWebSocket` 会抛
`The remote party closed the WebSocket connection without completing the close handshake`（底层 10054/RST）。

原因：Close 帧只是交给底层异步发送，随即 `Disconnect` 关闭 socket；若此时接收方向仍有未读数据，整条连接会被内核 reset，已经写出的 Close 帧被丢弃。

修复：

1. `IWebSocketTransport` 增加 `WaitSendCompleted(int timeoutMs)`，关闭前最多等待 1000ms 让 Close 帧写完。
2. 增加 `ShutdownSend()`：发送方向半关闭（FIN），让客户端先读完 Close 帧，再由正常断开链路回收会话。
3. `MVCWebSocketConnection.CleanupAsync` 的顺序改为：写 Close -> `WaitSendCompleted(1000)` -> `ShutdownSend()` -> `Disconnect()`。

结果：`ClientWebSocket` 连续 30 次正常关闭全部收到 `Close 1000`，不再出现 RST。

### WSClient request-target

`SAEA.WebSocket` 的 `WSClient` 原来把完整 URL（`ws://host/path?query`）直接写进请求行，路径型服务端无法正确路由，query（如 token）会丢失。`WSUserToken.RequestHandShark` 现在会把 `ws://` / `wss://` 绝对地址归一化为 `PathAndQuery`，相对地址原样使用。

### 验证

| 用例 | 结果 |
| --- | --- |
| 聊天协议端到端（非法 token 401、匿名 welcome/whoami/回显/群操作被拒、登录 welcome、whoami、建群/重复建群、加群通知、群列表/成员、群发、私聊在线/离线、退群、退群后禁发、空群删除、顶号 1001、重连保留群关系、断线退群） | 35/35 通过 |
| `ChatController` 生命周期（welcome、文本/二进制回显、`server-close` 1011、客户端关闭、4.5MB 超限 1002） | 通过 |
| 连续关闭（`ClientWebSocket` x10，3 轮） | 30/30 收到 Close 1000 |
| `ws-smoke`（普通 GET 400、文本/二进制/200KB/分片/顺序/关闭） | 通过 |
| `ws-extended`（普通 action、静态文件、双客户端各 50 帧、服务端关闭） | 通过 |
| `round2-fixes`（POST/PUT/DELETE/GET `/chat/open` 400、双握手 101、absolute-form） | 通过 |
| `review-fixes`（query 含 `://`、absolute-form、早到帧顺序） | 通过 |
| `ws-maxframe`（4.5MB 单帧 -> 1002） | 通过 |
| `ws-client-compat`（`WSClient` 连带 token 的 `/chat/open`，1MB 二进制） | 通过 |
| SSE / `/home/ping` / `/index.html` | 通过 |
