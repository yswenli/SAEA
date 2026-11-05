# SAEA.MQTT MQTT 5.0 运行时语义补齐设计（修订版）

- 日期：2026-11-05
- 修订：v2，按代码审计 + MQTT 5.0 OASIS 规范审计修正
- 范围：`Src/SAEA.MQTT`（服务端 + 客户端）、新增 `Src/SAEA.MQTT.Tests`
- 方案：V310/V311 行为不变；同一监听端口按 CONNECT 自动识别协议版本；补齐 MQTT 5.0 运行时语义
- 关联：`2026-09-21-span-memory-pipeline-design.md`、`2026-09-23-zero-copy-pooling-unification-design.md`

## 摘要

现有 V5 **报文层**完整：编解码器、`*Properties` 类、Reason Code 枚举、CONNACK 能力属性写入都已具备。缺的是**运行时语义**：

1. 能力协商：编码器能写，`CreateConnAckPacket` 只填 4 个字段并硬编码 `TopicAliasMaximum = 65535`。
2. Session Expiry / Will Delay：只解析，不执行；will 存在共享的 `MqttClientSession` 上，接管时存在竞态。
3. Message Expiry：只透传，不递减、不清理 retained。
4. Topic Alias：仅服务端入站映射，无上限校验、未知别名被静默忽略；客户端完全未实现。
5. Shared Subscriptions：无 `$share` 解析，匹配即全量入队。
6. 流控：Receive Maximum / Maximum Packet Size 只解析，不执行。

本设计按规范补齐，服务端与客户端对称；V3 连接不产生任何额外行为。

---

## 一、目标与非目标

### 1.1 目标

- 同一 TCP/TLS 端口同时接受 V310、V311、V500；每连接版本在首包 CONNECT 后固定。
- 客户端默认 `V311`，显式 `WithProtocolVersion(MqttProtocolVersion.V500)` 启用 V5。
- 六类语义按 MQTT 5.0（OASIS Standard 2019-03-07）实现，覆盖正向、负向、多版本共存。
- 服务端通告的每项能力都有对应执行代码。
- 新增 `Src/SAEA.MQTT.Tests`（MSTest，net10.0），可 `dotnet test`。

### 1.2 非目标

- 不实现 MQTT-SN、UDP、桥接、集群、规则引擎。
- 不实现具体增强认证方法（OAuth2/SCRAM 等）；只保留 AUTH 通道。
- 不实现 QoS 3（协议不存在）。
- 不重写 V5 编解码器。
- **不实现进程重启后的会话恢复**（见 §5.2）；现有会话持久化接口只扩展字段。
- 不把服务端出站发送循环改成并发（见 §7.4）。

### 1.3 反目标

- 让 V3 客户端多发属性、多收报文或改变现有行为。
- 只在单侧实现，造成“看起来支持”。
- 通告能力却不执行。
- 用 `Task.Delay`/轮询冒充过期与流控正确性；必须用可替换时钟、计数器和真实时间基准。
- 协议错误断开时抑制 will（规范不允许，见 §5.3）。

---

## 二、约束与事实基线

### 2.1 工程约束

| 项 | 值 |
| --- | --- |
| 目标框架 | `netstandard2.0` |
| 语言版本 | `LangVersion 8.0`；未启用 `<Nullable>` |
| 集合约束 | 无 `PriorityQueue<,>`（.NET 6+）、无 `System.Threading.Channels`；最小堆需手写 |
| 时间源 | 内部 `IMqttTimeProvider`（`DateTimeOffset UtcNow`），测试用假时钟；不暴露到公共 options |
| 定时器 | 单个 `System.Threading.Timer` 驱动到期调度器；测试可直接调用 `RunDueTasks()` |
| 测试可见性 | csproj 增加 `<InternalsVisibleTo Include="SAEA.MQTT.Tests" />` |
| 测试项目 | MSTest + net10.0 + `IsPackable=false` |
| 版本 | 完成后提升 `SAEA.MQTT.csproj` 的 `Version` |

### 2.2 已核对的现状（行号可能漂移）

| 能力 | 现状 | 位置 |
| --- | --- | --- |
| 版本探测 | `MqttChannelAdapter.ReceivePacketAsync` 对首包调用 `DetectProtocolVersion`；首包非 CONNECT 时解码前抛异常，服务端只能关 TCP | `Adapter/MqttChannelAdapter.cs:206-208`、`Formatter/MqttPacketFormatterAdapter.cs:99-109` |
| CONNACK 编码 | 编码器已支持全部 V5 能力属性 | `Formatter/V5/MqttV500PacketEncoder.cs:225-241` |
| CONNACK 组装 | `CreateConnAckPacket` 只填少量字段 + 硬编码 `TopicAliasMaximum = ushort.MaxValue` | `Formatter/V5/MqttV500DataConverter.cs:176-191` |
| 能力上下文 | 不存在；`MqttV500DataConverter` 无参构造，看不到 `IMqttServerOptions` | `Formatter/V5/MqttV500DataConverter.cs:49` |
| Topic Alias 入站 | `HandleTopicAlias` 写入无上限 `Dictionary<ushort,string>`；未知别名分支为空 | `Server/MqttClientConnection.cs:52、375-400` |
| DISCONNECT 入站 | 分支只清 will、置 Clean、停止；不读 `ReasonCode`/`Properties` | `Server/MqttClientConnection.cs:237-243` |
| will | 存于共享的 `MqttClientSession.WillMessage`，`finally` 无条件发布；接管时新连接先写入，旧连接后执行 | `Server/MqttClientConnection.cs:190、274-278`、`Server/MqttClientSessionsManager.cs:472-505` |
| 停止路径 | `StopAsync` 只在 `SessionTakenOver`/`KeepAliveTimeout` 发 DISCONNECT；catch-all 直接 `StopInternal()` | `Server/MqttClientConnection.cs:118-140、254-266` |
| 会话清理 | `CleanUpClient` 同时删会话、关 channel、发事件；`EnablePersistentSessions=false` 时无条件删会话 | `Server/MqttClientSessionsManager.cs:309-334` |
| 消息分发 | `DispatchApplicationMessage` 入 `BlockingCollection`；后台消费者在 `lock(_sessions)` 内遍历所有会话并 `Enqueue` | `Server/MqttClientSessionsManager.cs:245-250、405-415` |
| 订阅匹配 | `CheckSubscriptions` 只收集 QoS，返回 `CheckSubscriptionsResult{IsSubscribed,QualityOfServiceLevel}` | `Server/MqttClientSubscriptionsManager.cs:201-234、268-284` |
| 订阅选项 | `NoLocal`/`RetainAsPublished`/`RetainHandling` 字段已存在，但从未参与匹配或发送 | `MqttTopicFilter.cs:51-61` |
| 队列项 | `MqttQueuedApplicationMessage` 无时间戳、无订阅标识符 | `Server/MqttQueuedApplicationMessage.cs:37-54` |
| retained | `Dictionary<string,MqttApplicationMessage>`，无存留时间；持久化只传 `MqttApplicationMessage` | `Server/MqttRetainedMessagesManager.cs:46`、`Server/IMqttServerStorage.cs:39-41` |
| 客户端断开 | `MqttClientDisconnectOptions` 只有 `ReasonCode`/`ReasonString`；`CreateDisconnectPacket` 不写 Properties；`DisconnectAsync` 把本地 reason 覆盖为 Normal | `Client/Disconnecting/MqttClientDisconnectOptions.cs:36-38`、`Formatter/V5/MqttV500DataConverter.cs:281-295`、`Client/MqttClient.cs:178` |
| 客户端连接结果 | `MqttClientAuthenticateResult.MaximumQoS` 非可空并已折默认值；ReceiveMaximum/MaximumPacketSize/TopicAliasMaximum 仍为 null | `Formatter/V5/MqttV500DataConverter.cs:130-141` |
| 多订阅标识符 | PUBLISH 多值写入/读取已实现 | `Formatter/V5/MqttV500PropertiesWriter.cs:113-124`、`MqttV500PacketEncoder.cs:291` |
| 服务端启动顺序 | retained.Start/Load → sessions.Start → keepAlive.Start → adapters.Start | `Server/MqttServer.cs:254-292` |
| 服务端出站 QoS | QoS1/2 顺序发送并等待 ACK（`SendAndReceive` 语义），天然不超过 1 个在途 | `Server/MqttClientConnection.cs:450-490` |

### 2.3 规范事实基线（本设计引用的强制条款）

| 编号 | 事实 | 影响 |
| --- | --- | --- |
| F1 | 服务端在 CONNACK 的 Session Expiry Interval 用于告知“使用了与 CONNECT 不同的值”；缺省则沿用 CONNECT 值 | §5.1、§5.2 |
| F2 | `Receive Maximum` 缺省 65535；为 0 是协议错误 | §5.1、§5.7 |
| F3 | Send quota 每发一个 QoS>0 PUBLISH 减 1；每收到 PUBACK **或 PUBCOMP** 加 1 | §5.7（QoS2 在 PUBCOMP 释放，不是 PUBREC） |
| F4 | 收到超过 Receive Maximum 的 QoS>0 PUBLISH 时，用 0x93 断开 | §5.7 |
| F5 | will 在“Will Delay 到期”或“会话结束”二者先到时发布；Will Delay 到期前建立新连接则 MUST NOT 发布 | §5.3 |
| F6 | 收到 DISCONNECT 0x00 才删除 will；其它原因断开都要发布 | §5.3 |
| F7 | Session Expiry 缺省/为 0 时，连接关闭即会话结束；`0xFFFFFFFF` 永不过期 | §5.2 |
| F8 | Topic Alias 0 非法；超过对端通告的 Topic Alias Maximum 是协议错误；映射仅限本网络连接 | §5.5 |
| F9 | 共享订阅的 ShareName 不得含 `/`、`+`、`#`；`NoLocal=1` 是协议错误 0x9E；新成员不补发 retained | §5.6 |
| F10 | 订阅标识符在 SUBACK 前由服务端存储，转发 PUBLISH 时必须带上（可多个） | §5.6 |
| F11 | 超过对端 Maximum Packet Size 的报文不得发送；接收方超限可断开 | §5.7 |
| F12 | 可用的能力位为 0 时，对应行为是协议错误或拒绝，不是静默忽略 | §5.1、§8 |

---
## 三、总体设计

### 3.1 改动面

| 层 | 文件/新增 | 改动 |
| --- | --- | --- |
| 能力 | 新增 `Internal/MqttConnectionCapabilities.cs` | 每连接一份的能力快照 + 协商结果 |
| 能力 | `Server/MqttServerCapabilities.cs`（新增，公开 POCO） | 服务端可配置能力 |
| 能力 | `Server/IMqttServerOptions.cs`、`MqttServerOptions.cs`、`MqttServerOptionsBuilder.cs` | 暴露 `Capabilities` + 若干 `WithXxx` |
| 能力 | `Formatter/MqttPacketFormatterAdapter.cs` | `AttachCapabilities(...)`，把上下文交给 V5 转换器 |
| 能力 | `Formatter/V5/MqttV500DataConverter.cs` | `CreateConnAckPacket` 按上下文写属性；`CreateClientConnectResult` 读全属性 |
| 版本 | `Adapter/MqttChannelAdapter.cs`、`Formatter/MqttPacketFormatterAdapter.cs` | 首包非 CONNECT 时抛可识别异常；版本固定 |
| 时间 | 新增 `Internal/IMqttTimeProvider.cs`、`MqttSystemTimeProvider.cs`、`MqttExpiryScheduler.cs` | 可替换时钟 + 手写最小堆调度器 |
| 会话 | `Server/MqttClientSession.cs`、`MqttClientSessionsManager.cs`、新增 `MqttSessionExpiryManager.cs` | 会话过期、will 延迟、接管竞态 |
| 消息 | `Server/MqttQueuedApplicationMessage.cs`、`MqttClientSessionApplicationMessagesQueue.cs` | 入队时间、订阅标识符、消息过期 |
| retained | `Server/MqttRetainedMessagesManager.cs`、`IMqttServerStorage.cs` | 存留时间、TTL 清理、过期不投递 |
| 订阅 | `MqttTopicFilter.cs`、`Server/MqttClientSubscriptionsManager.cs`、`CheckSubscriptionsResult.cs`、新增 `Server/MqttSharedSubscriptionRegistry.cs`、`MqttSubscriptionMatch.cs` | `$share` 解析、匹配选项、共享组轮询 |
| 流控 | `Server/MqttClientConnection.cs`、`Client/MqttClient.cs`、`Adapter/MqttChannelAdapter.cs` | Receive Maximum 配额、包大小、topic alias、原因码 |
| 客户端 | `Client/Disconnecting/MqttClientDisconnectOptions.cs`、`Client/Options/*` | DISCONNECT 属性、能力结果 |
| 测试 | 新增 `Src/SAEA.MQTT.Tests` | MSTest 用例矩阵 |

### 3.2 每连接能力上下文

新增内部类型 `MqttConnectionCapabilities`，**每连接一份**，是运行时所有决策的唯一事实来源。它分三组字段：

1. **服务端能力（本端通告、本端执行）**：`RetainAvailable`、`MaximumQoS`、`WildcardSubscriptionAvailable`、`SubscriptionIdentifiersAvailable`、`SharedSubscriptionAvailable`、`ReceiveMaximum`、`MaximumPacketSize`、`TopicAliasMaximum`、`ServerKeepAlive`、`MaximumSessionExpiryInterval`。
2. **客户端能力（对端通告、约束本端出站）**：`ClientReceiveMaximum`、`ClientMaximumPacketSize`、`ClientTopicAliasMaximum`。
3. **协商结果**：`ProtocolVersion`、`SessionExpiryInterval`、`AssignedClientIdentifier`、`RequestProblemInformation`。

生命周期与注入路径：

```
CONNECT 到达
  → MqttChannelAdapter 首包探测版本（V310/V311/V500）
  → MqttClientConnection 解码 MqttConnectPacket
  → CapabilityNegotiator.Negotiate(serverCapabilities, connectPacket)   // 纯函数，可单测
  → PacketFormatterAdapter.AttachCapabilities(capabilities)
  → V5DataConverter.CreateConnAckPacket(...) 读取上下文写属性
```

`MqttV500DataConverter` 由 `MqttPacketFormatterAdapter` 创建，而后者随 `MqttChannelAdapter` **每连接一份**，因此在其上挂一个 `internal` 字段即可做到连接隔离，不需要改公共 `IMqttDataConverter` 接口。

**注入必须发生在两处之前**。CONNACK 有两个构造点：

1. `MqttClientSessionsManager.HandleClientConnectionAsync`：连接被拒绝时直接调用 `CreateConnAckPacket(connectionValidatorContext)`。
2. `MqttClientConnection.RunInternalAsync`：连接被接受后调用同一方法。

因此协商与 `AttachCapabilities` 必须放在 `HandleClientConnectionAsync` 中、**`ValidateConnectionAsync` 之后、第 139 行拒绝分支与 `CreateClientConnection` 之前**。放在 validator 之后的原因：自定义 `IMqttServerConnectionValidator` 可能写入 `AssignedClientIdentifier`，协商函数必须使用最终值；validator 本身不读取能力上下文，因此顺序无副作用。这样拒绝路径与接受路径都能拿到能力上下文。

### 3.3 版本探测与多版本共存

- 现状已能在同一端口按 CONNECT 的 Protocol Version 选择 V310/V311/V500 格式化器，本设计**保留**该机制。
- 修正一处健壮性缺陷：首包不是 CONNECT 时，当前在 `DetectProtocolVersion` 内部抛 `MqttProtocolViolationException`，但此时格式化器尚未选定。改为在适配器层显式判断并抛出带原因码的 `MqttProtocolViolationException`，由服务端统一按“无版本连接的协议错误”关闭 TCP（V3 语义不变：V3 无 DISCONNECT 报文可发）。
- 版本一旦选定不可变更；后续报文若出现与版本不符的字段，按解码器既有规则报错。
- V3 与 V5 共用监听端口、连接表、会话表、retained 存储；**行为分叉点只在能力上下文和使用处**，不在管理器层。

### 3.4 时间源与到期调度

```csharp
internal interface IMqttTimeProvider
{
    DateTimeOffset UtcNow { get; }
}

internal static class MqttTime
{
    internal static IMqttTimeProvider Provider { get; set; } = MqttSystemTimeProvider.Instance;
    internal static DateTimeOffset UtcNow => Provider.UtcNow;
}
```

`MqttExpiryScheduler`：

- 手写二叉最小堆（`netstandard2.0` 无 `PriorityQueue`），元素为 `(dueTime, generation, callback)`。
- 单个 `Timer`，每次设置为堆顶到期时间；到期时在**线程池**上取出所有 `dueTime <= now` 的元素执行。
- 取消采用“代际 + 惰性删除”：`Cancel(handle)` 只置位，堆中残留元素在弹出时丢弃。
- 提供 `internal void RunDueTasks()`：测试中把假时钟推进后直接驱动，避免真实等待。
- 回调内部捕获异常并记日志，绝不让调度器线程崩溃。

调度器只服务两个语义：**会话过期**与**will 延迟**。二者都以“连接断开”为起点，因此合并到 `MqttSessionExpiryManager` 统一登记，避免两套定时器互相竞争。

### 3.5 服务端出站路径的既有不变量

- 出站 QoS1/2 目前是“发送 → 等待 PUBACK/PUBCOMP”的顺序模型。本设计**不改变**该模型，只在其上加配额断言（D3）。
- 所有出站报文最终经过 `MqttClientConnection.SendAsync` → `MqttChannelAdapter.SendPacketAsync`。包大小裁剪与 Topic Alias 映射都收敛到该层，避免散落。
- V3 连接上，这些新增逻辑全部由 `capabilities.ProtocolVersion == V500` 短路。

---

## 四、公共 API 变更

破坏性变更被允许，但必须做到“V3 行为零变化、V5 语义可配置、默认值符合规范”。

### 4.1 服务端

新增公开 POCO：

```csharp
public sealed class MqttServerCapabilities
{
    public ushort ReceiveMaximum { get; set; } = ushort.MaxValue;          // 65535
    public uint MaximumPacketSize { get; set; } = 268435455;               // 协议上限
    public ushort TopicAliasMaximum { get; set; } = 0;                     // 默认不启用
    public bool RetainAvailable { get; set; } = true;
    public bool WildcardSubscriptionAvailable { get; set; } = true;
    public bool SubscriptionIdentifiersAvailable { get; set; } = true;
    public bool SharedSubscriptionAvailable { get; set; } = true;
    public MqttQualityOfServiceLevel MaximumQoS { get; set; } = MqttQualityOfServiceLevel.ExactlyOnce;
    public ushort? ServerKeepAlive { get; set; }                            // null=沿用客户端
    public uint? MaximumSessionExpiryInterval { get; set; }                 // null=不下调
}
```

`IMqttServerOptions` 增加：

```csharp
MqttServerCapabilities Capabilities { get; }
```

`MqttServerOptionsBuilder` 增加链式方法：

```csharp
WithReceiveMaximum(ushort)
WithMaximumPacketSize(uint)
WithTopicAliasMaximum(ushort)
WithRetainAvailable(bool)
WithWildcardSubscriptionAvailable(bool)
WithSubscriptionIdentifiersAvailable(bool)
WithSharedSubscriptionAvailable(bool)
WithMaximumQoS(MqttQualityOfServiceLevel)
WithServerKeepAlive(ushort?)
WithMaximumSessionExpiryInterval(uint?)   // D6：null = 不下调
```

`Capabilities` 在 `MqttServer.StartAsync` 时**快照**一次，之后修改 options 不影响已启动服务器。默认值全部是规范默认值，因此**不配置时 V5 行为与规范一致**。

### 4.2 客户端

`MqttClientDisconnectOptions` 增加（V5 生效，V3 忽略）：

```csharp
public uint? SessionExpiryInterval { get; set; }
public List<MqttUserProperty> UserProperties { get; set; }
```

规则：CONNECT 的 Session Expiry 为 0 时，DISCONNECT 携带非 0 是协议错误（F13）。客户端在本地校验；服务端收到时以 0x82 断开。

`MqttClientAuthenticateResult` 经核对**已经完整**（`ReceiveMaximum`、`MaximumPacketSize`、`TopicAliasMaximum`、`SessionExpiryInterval`、`SharedSubscriptionAvailable`、`SubscriptionIdentifiersAvailable` 均存在），`MqttV500DataConverter.CreateClientConnectResult` 也已逐字段赋值。**本轮 P1 无需修改这两个文件**，只需保证服务端 CONNACK 真的写出这些属性。

`MqttClientOptionsBuilder` **已存在**下列方法，本轮只核对语义、不重复添加：

```csharp
WithSessionExpiryInterval(uint?)
WithReceiveMaximum(ushort?)
WithMaximumPacketSize(uint?)
WithTopicAliasMaximum(ushort?)
WithProtocolVersion(MqttProtocolVersion)
WithWillDelayInterval(uint?)
```

需要补的是“这些选项真正进入运行时”：
- `WithSessionExpiryInterval` 的值确实写入 CONNECT，并在重连时按 CONNACK 结果更新。
- `WithReceiveMaximum` 作为客户端**入站**配额执行。
- `WithMaximumPacketSize` 作为客户端**入站**上限执行。
- `WithTopicAliasMaximum` 作为客户端**入站**别名上限执行。

### 4.3 内部可见性

- `IMqttTimeProvider`、`MqttExpiryScheduler`、`MqttConnectionCapabilities`、`MqttSharedSubscriptionRegistry`、`MqttSubscriptionMatch` 全部 `internal`。
- `SAEA.MQTT.csproj` 增加 `InternalsVisibleTo("SAEA.MQTT.Tests")`。
- 公共 `MqttServerCapabilities`、`MqttClientDisconnectOptions` 新属性、`MqttClientAuthenticateResult` 新属性是仅有的公开面扩张。

### 4.4 V3 兼容性硬约束

| 约束 | 验证方式 |
| --- | --- |
| V310/V311 CONNACK 字节与改动前一致 | 黄金字节测试（§9.4） |
| V3 连接不写任何属性 | 编码器断言 + 抓包字节比对 |
| V3 不因 `Capabilities` 被拒 | 能力校验只在 V5 分支 |
| V3 会话清理与 will 语义保持原逻辑 | 回归测试 |
| `CleanSession=true` 行为不变 | 回归测试 |

---
## 五、运行时语义详设

### 5.1 能力协商与 CONNACK

#### 5.1.1 协商函数

新增 `internal static class MqttCapabilityNegotiator`，纯函数：

```csharp
internal static MqttConnectionCapabilities Negotiate(
    MqttServerCapabilities server,
    MqttConnectPacket connect,
    MqttProtocolVersion version,
    string assignedClientId);
```

纯函数便于单测，不依赖连接与时间。

协商规则：

| 项 | 服务端取值 | CONNACK 写入条件 |
| --- | --- | --- |
| `ReceiveMaximum` | `server.ReceiveMaximum`，0 视为配置错误（启动时抛 `MqttConfigurationException`） | `!= 65535` 时写 |
| `MaximumPacketSize` | 钳制到 `[1, 268435455]` | `!= 268435455` 时写 |
| `TopicAliasMaximum` | `server.TopicAliasMaximum` | `!= 0` 时写 |
| `MaximumQoS` | 只能是 0/1/2；2 表示“不限制” | `!= ExactlyOnce` 时写（2 不可编码，按规范省略） |
| `RetainAvailable` | `server.RetainAvailable` | `== false` 时写 0 |
| `WildcardSubscriptionAvailable` | 同上 | `== false` 时写 0 |
| `SubscriptionIdentifiersAvailable` | 同上 | `== false` 时写 0 |
| `SharedSubscriptionAvailable` | 同上 | `== false` 时写 0 |
| `ServerKeepAlive` | `server.ServerKeepAlive ?? connect.KeepAlive` | `!= connect.KeepAlive` 时写 |
| `SessionExpiryInterval` | `min(connect.SessionExpiry ?? 0, server.MaximumSessionExpiryInterval ?? uint.MaxValue)` | `!= connect.SessionExpiry` 时写（F1） |
| `AssignedClientIdentifier` | 客户端 ClientId 为空时生成 | 仅生成时写 |
| `ReasonString`/`UserProperty` | 诊断信息 | 仅 `connect.RequestProblemInformation != 0` 时写（F12） |

#### 5.1.2 `CreateConnAckPacket` 改造

- 删除硬编码 `TopicAliasMaximum = ushort.MaxValue`。
- 从 `MqttConnectionCapabilities` 读取全部字段写入 `MqttConnAckPacketProperties`。
- **V3 不经过该分支**，天然不写属性。
- `MqttClientAuthenticateResult` 侧同步读取全部属性；缺省按规范默认值折算（ReceiveMaximum=65535、MaximumPacketSize=268435455、TopicAliasMaximum=0、MaximumQoS=2、其余 true），但公共属性保持 `null` 以区分“服务端显式通告”与“默认”。内部消费用折算值，公共结果保留 null。

#### 5.1.3 能力位的执行（通告即执行）

| 通告为 0/不支持 | 服务端入站时行为 |
| --- | --- |
| `RetainAvailable=0` 且 PUBLISH `Retain=1` | DISCONNECT 0x9A（F12） |
| `MaximumQoS=0/1` 且 PUBLISH QoS 更高 | DISCONNECT 0x9B |
| `RetainAvailable=0` 且 CONNECT 带 Will Retain | CONNACK 0x9A 后关闭 |
| `MaximumQoS` 低于 Will QoS | CONNACK 0x9B 后关闭 |
| `WildcardSubscriptionAvailable=0` 且订阅含 `+`/`#` | 该过滤器 SUBACK 0xA2 |
| `SubscriptionIdentifiersAvailable=0` 且 SUBSCRIBE 带订阅标识符 | DISCONNECT 0xA1 |
| `SharedSubscriptionAvailable=0` 且订阅 `$share/...` | 该过滤器 SUBACK 0x9E |

“通告即执行”是本设计的验收硬指标：每个为 0 的能力必须有对应用例。

---

### 5.2 Session Expiry

#### 5.2.1 有效值

```csharp
uint clientExpiry = connect.SessionExpiryInterval ?? 0;     // V5；V3 不适用
uint effective   = Math.Min(clientExpiry, server.MaxSessionExpiryInterval ?? uint.MaxValue);
```

- `EnablePersistentSessions == false` 时，V5 也强制 `effective = 0`（保留旧开关语义：服务器不保存任何会话）。
- V3 连接**完全沿用**现有 `CleanSession` + `EnablePersistentSessions` 逻辑，不进入本节的过期调度。

#### 5.2.2 状态机

```
CONNECT(CleanStart=1) ──► 删除旧会话状态 ──► 新建会话
CONNECT(CleanStart=0, 有会话) ──► SessionPresent=1，复用
CONNECT(CleanStart=0, 无会话) ──► SessionPresent=0，新建
连接关闭:
  effective == 0            ──► 立即结束会话（同步删除）
  0 < effective < UINT_MAX  ──► 登记到期 = now + effective
  effective == UINT_MAX     ──► 不登记，永不过期
重连（未到期）              ──► 取消到期登记
```

- “结束会话”必须原子地：移除 `MqttClientSession`、注销共享订阅成员、丢弃队列、移除 will（若尚未发布则先按 §5.3 发布）。
- 到期回调只处理“仍然处于断开状态且代际匹配”的会话，防止重连后被误删。
- `MqttClientSessionsManager.CleanUpClient` 的职责拆分：它只负责**连接**清理与事件；会话是否删除由 `MqttSessionExpiryManager` 依据上述规则决定。

#### 5.2.3 持久化接口

`IMqttServerPersistedSession` 扩展字段（D2：只扩展，不实现恢复）：

```csharp
uint SessionExpiryInterval { get; }
uint WillDelayInterval { get; }
DateTimeOffset? DisconnectedAt { get; }
```

仅供未来实现重启恢复；本轮不读取。

#### 5.2.4 客户端侧

- 客户端在 CONNACK 中读到服务端下调后的 Session Expiry，写入 `MqttClientAuthenticateResult.SessionExpiryInterval`。
- 客户端 DISCONNECT 可携带 `SessionExpiryInterval`（§4.2）。本地校验：CONNECT 值为 0 时不允许非 0（F13）；不满足则不发送该属性并抛 `MqttProtocolViolationException`。

---

### 5.3 Will 与 Will Delay

#### 5.3.1 所有权修正（消除接管竞态）

现状：will 存在**共享**的 `MqttClientSession.WillMessage`，旧连接 `finally` 无条件发布；新连接可能已把新 will 写入同一字段。

改为：

- `MqttClientConnection` 持有 `readonly MqttApplicationMessage _willMessage`（来自本次 CONNECT）。
- 会话上的 `WillMessage` 仅代表“已断开、尚未决定”的悬挂 will。
- 连接断开时调用唯一的入口：

```csharp
_sessionsManager.HandleConnectionClosed(
    clientId, connectionEpoch, disconnectContext, willMessage, willDelay, sessionExpiry);
```

`connectionEpoch` 是每次建连递增的 `long`，用于幂等与防止旧连接覆盖新连接。

#### 5.3.2 断开原因分类

| 场景 | will 处置 |
| --- | --- |
| 收到 DISCONNECT 0x00 | **删除**，不发布 |
| 收到 DISCONNECT 0x04（Disconnect with Will Message） | 发布，受 Will Delay 约束 |
| 收到其它非 0 DISCONNECT | 发布，受 Will Delay 约束 |
| 协议错误 / 畸形包 | **必须发布**（反目标 #5） |
| I/O 错误、keep-alive 超时、对端关闭 | 发布 |
| 服务端主动停止（shutdown） | 发布；允许“待重启后发布”的延迟（规范允许） |
| 会话接管（SessionTakenOver） | 见下表 |

接管时（D7）：

| 新连接 CleanStart | WillDelay | 结果 |
| --- | --- | --- |
| 0 | 0 | 旧连接关闭即**立即发布** |
| 0 | >0 | **取消**旧 will（新连接在 delay 内接管） |
| 1 | 任意 | 旧会话结束 → **立即发布** |

#### 5.3.3 调度

- `willDelay == 0` 或会话立即结束 → 立即同步发布。
- `0 < willDelay` → 在 `MqttSessionExpiryManager` 登记 `WillDueAt = now + willDelay`，同时登记会话到期。
- 两个到期点先到者触发 will；will 发布后置 `_willPublished`，会话到期不再重复发布。
- 发布动作复用 `MqttClientSessionsManager.DispatchApplicationMessage`，因此 retained、消息过期、订阅匹配、共享订阅全部走同一条路径。
- **幂等**：以 `(clientId, epoch)` 为键，重复回调只生效一次。

---

### 5.4 Message Expiry

#### 5.4.1 数据流

```
接收 PUBLISH（含 MessageExpiryInterval=E）
  → AcceptedAt = MqttTime.UtcNow
  → 入队时记录 ExpiresAt = AcceptedAt + E
  → 出队发送前: if (now >= ExpiresAt) 丢弃
  → 转发时 MessageExpiryInterval = max(0, ceil((ExpiresAt-now).TotalSeconds))
  → retained 存储: 记录 RetainedAt，读取时按同一公式计算剩余值
```

- 剩余值为 0 或负数：**不发送**该 PUBLISH（规范要求不得发送已过期消息）。
- QoS2 已进入 PUBREL/PUBCOMP 阶段的消息即使过期，仍必须完成确认序列（F14）——过期只影响“是否首次发送”，不影响协议握手。
- retained：读取时惰性清理，过期条目删除且不投递；写入时覆盖旧值并刷新 `RetainedAt`。
- 持久化：`IMqttServerStorage` 的 retained 载荷增加 `RetainedAt`，否则重启后过期语义丢失。
- V3（`MessageExpiryInterval == null`）零开销：不计算、不写属性。

#### 5.4.2 内部元数据

`MqttApplicationMessage` 公共面不变；新增内部扩展载体：

```csharp
internal sealed class MqttMessageMetadata
{
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset EnqueuedAt { get; set; }
}
```

通过 `MqttQueuedApplicationMessage` 携带，不污染公共消息类型。`MqttClientSessionApplicationMessagesQueue` 的入队重载增加 `subscriptionIdentifier` 与 `expiresAt`。

---

### 5.5 Topic Alias

#### 5.5.1 服务端入站

`HandleTopicAlias` 重写为有状态、有校验的解析：

| 输入 | 行为 |
| --- | --- |
| 无 `TopicAlias` | 使用 Topic，原样处理 |
| `TopicAlias == 0` | 协议错误 → DISCONNECT 0x94（F8） |
| `TopicAlias > capabilities.TopicAliasMaximum` | 协议错误 → DISCONNECT 0x94 |
| `TopicAlias` 合法 + Topic 非空 | 建立/覆盖映射，使用 Topic |
| `TopicAlias` 合法 + Topic 为空 | 查映射；命中则用映射 Topic；未命中 → DISCONNECT 0x94 |
| `capabilities.TopicAliasMaximum == 0` 且出现别名 | 0x94 |

映射随连接销毁；`MqttClientConnection.Dispose` 清空字典。

#### 5.5.2 服务端出站

- 上限来自客户端 CONNECT 的 `TopicAliasMaximum`（`capabilities.ClientTopicAliasMaximum`）。
- 为 0 → 永不写别名。
- 大于 0 → 每连接维护 `topic -> alias` 与 `alias -> topic` 双向映射；同一 topic 第二次起发送“空 Topic + alias”。
- 映射满时按简单 FIFO 淘汰（不追求最优，先保证正确）。
- 只对转发的 PUBLISH 生效；CONNECT/CONNACK/SUBACK 等不涉及。
- 重新连接必须清空，绝不跨连接复用（F8）。

#### 5.5.3 客户端双向

- 出站：客户端按 CONNACK 的 `TopicAliasMaximum` 使用别名；大于 0 时才启用；首包带 Topic，后续可空 Topic。
- 入站：客户端用自己 CONNECT 中的 `TopicAliasMaximum` 校验服务端发来的别名；非法或未知 → 抛 `MqttProtocolViolationException` 并断开（V5 可发 DISCONNECT 0x94）。
- 客户端的别名表随每次 `ConnectAsync` 重置。
- 空 Topic + 未知别名时，客户端当前会把空 Topic 当成合法主题；这是必须修的缺陷。

---
### 5.6 Shared Subscriptions 与订阅选项

#### 5.6.1 解析

`MqttTopicFilter` 增加内部字段（不改变公共面）：

```csharp
internal string ParsedFilter { get; set; }   // 非共享时等于 Topic
internal string ShareName { get; set; }      // null 表示非共享
```

解析规则（F9）：

```
"$share/{ShareName}/{filter}"
  - 必须以 "$share/" 开头
  - ShareName 至少 1 个字符，且不含 '/', '+', '#'
  - ShareName 后必须是 '/'
  - 其后的 filter 必须合法（复用现有 MqttTopicFilterComparer 的校验）
```

- 结构不合法 → 该过滤器返回 SUBACK 0x8F（Topic Filter invalid），不影响同一 SUBSCRIBE 中的其它过滤器。
- `NoLocal == true` 的共享订阅是**协议错误** → DISCONNECT 0x82（见 §8.3 注）。
- `$share` 只对 V5 生效；V3 保持现状（当作普通主题字面量，可订阅但不可发布——沿用现有 `MqttTopicValidator` 规则）。

#### 5.6.2 全局注册表（D1）

新增 `internal sealed class MqttSharedSubscriptionRegistry`，由 `MqttClientSessionsManager` 持有唯一实例。

```csharp
internal sealed class MqttSharedSubscriptionRegistry
{
    // key = (shareName, filter)，value = 有序成员环
    void Add(string shareName, string filter, string clientId, MqttSharedSubscriptionMember member);
    void Remove(string clientId);                 // 会话结束时整体注销
    void Remove(string shareName, string filter, string clientId);
    bool TrySelect(string shareName, string filter, out MqttSharedSubscriptionMember member);
}
```

- 每个键维护一个轮转索引，`TrySelect` 做 round-robin，保证同组内分摊。
- 线程安全：单锁保护；锁内只做选择，不做入队（避免与 `_sessions` 锁交叉）。
- 成员信息包含 `clientId`、会话引用、订阅标识符、QoS 与订阅选项。
- 会话清理（会话过期、CleanStart 覆盖、显式删除）必须调用 `Remove(clientId)`，否则会出现“幽灵成员”。

#### 5.6.3 分发算法

`DispatchApplicationMessage` 对每个会话计算匹配时，改为：

1. **非共享订阅**：按现有逻辑，每个会话最多一份。
2. **共享订阅**：对每个 `(ShareName, Filter)` 键，若主题匹配，从注册表选**恰好一个**成员入队。
3. 同一会话既可能收到非共享的一份，也可能被共享组选中；这是规范允许的（不同订阅彼此独立）。

关键约束：

- `NoLocal`：匹配阶段即排除“发布者 == 订阅者”的该条过滤器。
- 共享订阅**不投递 retained 消息**给新成员（F9）：`SubscribeAsync` 补发 retained 时跳过共享过滤器。
- 共享消息一旦分配给某会话，该会话若在投递前结束，**不得改投其它成员**（规范 MQTT-4.8.2-6）。
- 收到 PUBACK/PUBREC 且原因码 >= 0x80 时，丢弃该消息且**不重投**其它订阅者。
- 共享成员的 QoS 取该成员的订阅 QoS，不取全局最大。

#### 5.6.4 订阅选项真正生效

`CheckSubscriptions` 的返回契约从“最大 QoS”升级为“匹配选项集合”：

```csharp
internal struct MqttSubscriptionMatch
{
    public MqttQualityOfServiceLevel QualityOfServiceLevel;
    public bool NoLocal;
    public bool RetainAsPublished;
    public MqttRetainHandling RetainHandling;
    public uint? SubscriptionIdentifier;
    public bool IsShared;
    public string ShareName;
}

internal struct CheckSubscriptionsResult
{
    public bool IsSubscribed;
    public MqttQualityOfServiceLevel QualityOfServiceLevel;   // 兼容旧字段
    public List<MqttSubscriptionMatch> Matches;               // 新增
}
```

发送决策：

| 选项 | 服务端行为 |
| --- | --- |
| 多个过滤器匹配同一会话 | 合并为**一份** PUBLISH，QoS 取最大，订阅标识符取并集，`RetainAsPublished` 取逻辑或 |
| `RetainAsPublished=false` | 转发时 `Retain=false`（retained 补发除外，补发必须 `Retain=true`） |
| `RetainAsPublished=true` | 转发时保留原始 `Retain` 标志 |
| `RetainHandling=0` | 订阅建立时补发匹配的 retained |
| `RetainHandling=1` | 仅当该过滤器此前不存在时补发 |
| `RetainHandling=2` | 从不补发 |
| `SubscriptionIdentifier` | SUBSCRIBE 包的属性，适用于该包内全部过滤器；转发 PUBLISH 时写入所有匹配值 |

`MqttSubscribePacketProperties.SubscriptionIdentifier` 已存在；需要把它沿“订阅存储 → 匹配 → 入队 → 编码”全链路传递。当前链路中断在 `MqttClientSession.EnqueueApplicationMessage`，因此 `MqttQueuedApplicationMessage` 必须新增该字段。

---

### 5.7 流控、包大小与原因码闭环

#### 5.7.1 服务端入站 Receive Maximum

- 配额初值 = `capabilities.ReceiveMaximum`。
- 收到 QoS1 PUBLISH：配额 -1；发出 PUBACK 后 +1。
- 收到 QoS2 PUBLISH：配额 -1；**发出 PUBCOMP 后 +1**（F3，不是 PUBREC）。
- 配额为 0 时又收到 QoS>0 PUBLISH → DISCONNECT 0x93 并关闭。
- QoS0 不占配额。
- 用 `ConcurrentDictionary<ushort, byte>` 记录未完成的数据包标识符，避免重复计数；PUBCOMP 到达时移除。

新增 `internal sealed class MqttInboundFlowControl`，方法：

```csharp
bool TryAcquire(ushort packetIdentifier, MqttQualityOfServiceLevel qos);
void Release(ushort packetIdentifier);
int Available { get; }
```

#### 5.7.2 服务端出站 Receive Maximum

- 上限 = 客户端 CONNECT 的 `ReceiveMaximum`（缺省 65535）。
- 现有出站模型是“发送一个 → 等待 ACK”，在途数恒为 1（D3）。
- 因此只加**断言与度量**：`_outboundInFlight` 在 `SendAsync` 前置自增、ACK 后自减；若超过客户端上限则记错误并断开（说明既有模型被破坏）。
- 不改为并发发送；不在本轮引入发送窗口调度。

#### 5.7.3 Maximum Packet Size

两个方向：

| 方向 | 规则 |
| --- | --- |
| 入站 | `MqttChannelAdapter.ReceiveAsync` 读固定头后、分配正文前，比较 `RemainingLength + 固定头长度` 与本端上限；超限 → 0x95 并关闭，避免大缓冲分配 |
| 出站 | 编码后比较长度与对端上限；PUBLISH 超限则丢弃并记日志；其它控制报文超限则视为配置错误，断开连接 |

实现点：

- `MqttChannelAdapter` 新增 `internal uint MaximumPacketSize { get; set; }`，连接建立后按方向设置。
- 新增内部异常 `MqttMaximumPacketSizeExceededException`，携带方向与报文类型，便于调用方决定“丢弃”还是“断开”。
- `MqttChannelAdapter.SendPacketAsync` 在 `Encode` 之后比较长度；调用方（`MqttClientConnection.SendAsync`）捕获并分类处理。

#### 5.7.4 原因码闭环

V5 断开必须带原因码，映射固定为：

| 内部情形 | Reason Code |
| --- | --- |
| 无法解析的固定头/报文 | 0x81 Malformed Packet |
| 解析成功但违反协议 | 0x82 Protocol Error |
| 入站 QoS>0 超过 Receive Maximum | 0x93 |
| Topic Alias 非法/未知/超上限 | 0x94 |
| 收到超过本端 Maximum Packet Size | 0x95 |
| Retain 不可用却收到 Retain=1 | 0x9A |
| QoS 超出 Maximum QoS | 0x9B |
| 不支持共享订阅却收到 `$share` | 0x9E |
| 不支持订阅标识符却收到 | 0xA1 |
| 不支持通配符却收到 | 0xA2 |
| 会话被接管 | 0x8E |
| keep-alive 超时 | 0x8D |
| 管理端主动断开 | 0x98 |

- V3 连接**不发** DISCONNECT（协议无此报文），直接关闭，保持现状。
- V5 发送 DISCONNECT 时同样受对端 Maximum Packet Size 约束；超限则只关闭不发送。
- `ReasonString`/`UserProperty` 仅在客户端 `RequestProblemInformation != 0` 时附加。

#### 5.7.5 客户端侧流控

- 客户端用服务端 CONNACK 的 `ReceiveMaximum` 初始化发送配额；`PublishAsync` 在 QoS>0 时先 `await` 配额，收到 PUBACK/PUBCOMP 后释放。
- 用 `SemaphoreSlim` 实现，避免自旋；连接关闭时释放所有等待者并置为失效，防止重连后沿用旧配额。
- 客户端入站同样执行 Receive Maximum 与包大小校验；超限发 0x93/0x95 后关闭。

---
### 5.8 客户端对称实现

服务端语义必须与客户端对称，否则“V5 支持”只对第三方客户端有效。客户端改动集中在 `MqttClient` 与 `MqttChannelAdapter`，且全部由 `Options.ProtocolVersion == V500` 短路。

| 语义 | 客户端行为 |
| --- | --- |
| 能力协商 | 解析 CONNACK 全部能力属性，折算默认值存入内部 `MqttClientConnectionCapabilities`；公共 `MqttClientAuthenticateResult` 保留 null 语义 |
| Session Expiry | CONNECT 发送 `Options.SessionExpiryInterval`；记录 CONNACK 返回值；DISCONNECT 可携带新值（F13 校验） |
| Will Delay | 仅发送属性；执行在服务端 |
| Message Expiry | 发布时使用消息自带 `MessageExpiryInterval`；接收时直接采用服务端递减后的值；不自行倒计时 |
| Topic Alias | 出站按 CONNACK `TopicAliasMaximum`，入站按本端 CONNECT `TopicAliasMaximum`；双向映射每连接重置 |
| Shared Subscription | 订阅过滤器原样发送 `$share/...`；客户端不做本地分组 |
| 订阅选项 | `MqttClientSubscribeOptions` 已携带 `MqttTopicFilter`，选项原样编码；SUBACK 原因码按过滤器返回 |
| Receive Maximum | 出站配额 `SemaphoreSlim`；入站超额 → 0x93 |
| Maximum Packet Size | 出站编码后校验；入站读头校验；超限按 §5.7.3 |
| 原因码 | `DisconnectAsync` 不再覆盖用户原因码；缺失时按错误类型补默认值 |

客户端断开的现有缺陷与修复：

1. `MqttClient.DisconnectAsync` 目前把 `ReasonCode` 覆盖成 `NormalDisconnection`（`Client/MqttClient.cs:178`）。改为仅在用户未指定时使用默认值。
2. `CreateDisconnectPacket` 不写 Properties。改为写入 `ReasonString`、`SessionExpiryInterval`、`UserProperties`，并受对端 Maximum Packet Size 约束。
3. `DisconnectInternalAsync` 在异常路径应发送 0x80 类原因码（若能安全发送），而不是静默关闭。

---

## 六、内部契约与数据结构

### 6.1 `MqttConnectionCapabilities`

```csharp
internal sealed class MqttConnectionCapabilities
{
    public MqttProtocolVersion ProtocolVersion { get; set; }

    // 本端（服务端）通告并执行
    public ushort ReceiveMaximum { get; set; } = ushort.MaxValue;
    public uint MaximumPacketSize { get; set; } = 268435455;
    public ushort TopicAliasMaximum { get; set; }
    public MqttQualityOfServiceLevel MaximumQoS { get; set; } = MqttQualityOfServiceLevel.ExactlyOnce;
    public bool RetainAvailable { get; set; } = true;
    public bool WildcardSubscriptionAvailable { get; set; } = true;
    public bool SubscriptionIdentifiersAvailable { get; set; } = true;
    public bool SharedSubscriptionAvailable { get; set; } = true;
    public ushort? ServerKeepAlive { get; set; }
    public uint MaximumSessionExpiryInterval { get; set; } = uint.MaxValue;

    // 对端（客户端）通告，约束本端出站
    public ushort ClientReceiveMaximum { get; set; } = ushort.MaxValue;
    public uint ClientMaximumPacketSize { get; set; } = 268435455;
    public ushort ClientTopicAliasMaximum { get; set; }

    // 协商结果
    public uint SessionExpiryInterval { get; set; }
    public string AssignedClientIdentifier { get; set; }
    public bool RequestProblemInformation { get; set; } = true;

    public bool IsV5 => ProtocolVersion == MqttProtocolVersion.V500;
}
```

### 6.2 订阅匹配契约

见 §5.6.4。`CheckSubscriptionsResult` 保留旧属性以兼容内部调用点，新增 `Matches`；`MqttClientSession.EnqueueApplicationMessage` 改为接收 `MqttSubscriptionMatch` 并写入队列项。

### 6.3 共享订阅注册表

```csharp
internal sealed class MqttSharedSubscriptionMember
{
    public string ClientId { get; set; }
    public MqttQualityOfServiceLevel QualityOfServiceLevel { get; set; }
    public uint? SubscriptionIdentifier { get; set; }
    public bool RetainAsPublished { get; set; }
}

internal sealed class MqttSharedSubscriptionRegistry
{
    public void Add(string shareName, string filter, MqttSharedSubscriptionMember member);
    public void Remove(string shareName, string filter, string clientId);
    public void RemoveClient(string clientId);
    public IReadOnlyList<string> GetSharedKeysForTopic(string topic); // 仅返回匹配的 (shareName,filter)
    public bool TrySelect(string shareName, string filter, out MqttSharedSubscriptionMember member);
}
```

### 6.4 队列项扩展

```csharp
public class MqttQueuedApplicationMessage
{
    // 现有字段保持不变
    public uint? SubscriptionIdentifier { get; set; }      // 新增
    public DateTimeOffset? ExpiresAt { get; set; }          // 新增
    public DateTimeOffset EnqueuedAt { get; set; }          // 新增
}
```

`MqttClientSessionApplicationMessagesQueue.Enqueue(...)` 增加可选参数；旧重载保留（内部调用，不影响公共 API 兼容性要求，但减少改动面）。

### 6.5 retained 管理器扩展

```csharp
sealed class MqttRetainedMessageEntry
{
    public MqttApplicationMessage Message { get; set; }
    public DateTimeOffset RetainedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}
```

- `_messages` 改为 `Dictionary<string, MqttRetainedMessageEntry>`。
- `GetSubscribedMessagesAsync` 返回前过滤过期并顺带删除。
- `SetMessageAsync` 写入 `RetainedAt/ExpiresAt`。
- `LoadAsync/SaveAsync` 与 `IMqttServerStorage` 的载荷同步扩展；旧载荷（无时间戳）按 `RetainedAt = now` 处理，保持兼容读取。

### 6.6 会话与连接字段

`MqttClientSession` 新增：

```csharp
public uint SessionExpiryInterval { get; set; }
public uint WillDelayInterval { get; set; }
public long ConnectionEpoch { get; set; }          // 最后建立连接的代际
public DateTimeOffset? DisconnectedAt { get; set; }
public MqttApplicationMessage PendingWillMessage { get; set; }
public bool WillPublished { get; set; }
```

`MqttClientConnection` 新增：

```csharp
readonly MqttApplicationMessage _willMessage;       // 本次连接的快照
readonly long _connectionEpoch;
readonly MqttInboundFlowControl _inboundFlowControl;
readonly MqttTopicAliasResolver _inboundAliases;    // 入站
readonly MqttTopicAliasAssigner _outboundAliases;   // 出站
```

### 6.7 格式化器适配

`MqttPacketFormatterAdapter` 新增：

```csharp
internal MqttConnectionCapabilities Capabilities { get; private set; }
internal void AttachCapabilities(MqttConnectionCapabilities capabilities);
```

- `AttachCapabilities` 同时设置到 `MqttV500DataConverter` 的内部字段；V3 转换器忽略。
- `CreateConnAckPacket` 在 `Capabilities == null` 时回退到规范默认值（保护测试与异常路径），但生产路径必须已附加。

### 6.8 时间与调度

```csharp
internal interface IMqttTimeProvider { DateTimeOffset UtcNow { get; } }
internal sealed class MqttSystemTimeProvider : IMqttTimeProvider { ... }

internal sealed class MqttExpiryScheduler : IDisposable
{
    public IDisposable Schedule(DateTimeOffset dueAt, Action callback);
    public void Start();
    internal void RunDueTasks();   // 测试驱动
}
```

`MqttSessionExpiryManager` 内部持有该调度器，并对外暴露：

```csharp
void OnSessionDisconnected(MqttClientSession session, long epoch, bool publishWillNow);
void OnSessionReconnected(MqttClientSession session, long epoch);
void OnSessionRemoved(string clientId);
```

---
## 七、并发、生命周期与不变量

### 7.1 锁与所有权

| 数据 | 所有者 | 同步方式 |
| --- | --- | --- |
| `_connections` | `MqttClientSessionsManager` | `lock(_sessions)`（沿用） |
| `_sessions` | `MqttClientSessionsManager` | `lock(_sessions)`（沿用） |
| 订阅表 | `MqttClientSubscriptionsManager` | `ReaderWriterLockSlim`（沿用） |
| 共享订阅注册表 | 全局单例 | 独立 `lock`，**锁内不入队、不回调** |
| 入站配额 | 每连接 | 独立 `lock` |
| 出站别名表 | 每连接 | 独立 `lock` |
| 到期调度堆 | 全局单例 | 独立 `lock` + 代际取消 |

锁序约定：`_sessions` → 订阅表 → 注册表 → 每连接锁。任何路径不得反向获取。分发循环**先取快照**（在 `_sessions` 锁内复制会话列表），再在锁外做匹配与入队，避免长临界区。

### 7.2 会话接管时序（修复 will 竞态）

目标时序：

```
新连接 CONNECT(clientId=X)
  lock(_sessions):
    old = _connections[X]
  old.RequestStop(SessionTakenOver, newCleanStart, newWillDelay)
     ├─ 设置 old 的断开原因与代际
     ├─ 若 WillDelay == 0 或 newCleanStart == 1 → 立即发布 old will
     └─ 否则标记 old will 作废
  等待 old 退出（已有 Task 等待机制）
  创建/复用 X 的会话，SessionPresent 按 CleanStart 计算
```

不变量：

- will 的发布决定只由**旧连接**在其自己的停止上下文里做出一次，新连接绝不写旧连接的 will。
- `(clientId, epoch)` 保证重复调用幂等。
- 旧连接退出与新连接建立之间的会话复用必须在同一临界区判定 `SessionPresent`，否则会误报 0/1。

### 7.3 断开路径统一

现状有三条停止路径，本设计收敛为一条：

```csharp
Task StopAsync(MqttClientDisconnectReason reason, string reasonString, bool sendDisconnect)
```

- `sendDisconnect=true` 且 V5 → 先按 §5.7.4 发 DISCONNECT（受包大小约束）。
- 然后调用 `MqttClientSessionsManager.HandleConnectionClosed(...)`，由它统一决定 will 与会话去留。
- `finally` 中只做资源释放（channel、CTS、调度注销），**不再发布 will**。
- 所有 catch 分支必须携带具体原因码进入该路径，禁止空 catch 直接关闭。

### 7.4 分发、队列与背压

- `DispatchApplicationMessage` 保持单消费者 `BlockingCollection`，保证全局顺序。
- 分发热路径增加共享订阅选择与消息过期判断；这些操作不引入 `await`。
- 队列上限 `MaxPendingMessagesPerClient` 与 `MqttPendingMessagesOverflowStrategy` 保持现状（V3/V5 共用）。
- **不改变**出站串行模型（D3）；Receive Maximum 只做上限断言。
- 过期消息在出队时丢弃；若队列因此为空，消费者继续等待，不忙轮询。

### 7.5 关闭顺序

`MqttServer.StopAsync` 顺序调整为：

1. 停止接收新连接（adapter stop）。
2. 逐个连接 `StopAsync(ServerShuttingDown)`，按 §5.3 发布 will（允许延迟到重启后，但本轮直接发布）。
3. 停止 keep-alive monitor。
4. 停止 sessions manager 与到期调度器。
5. 保存 retained。

调度器必须在会话管理器之后释放，避免回调访问已销毁的会话表。

---

## 八、错误处理与原因码

### 8.1 入站错误分类

| 阶段 | 检测 | V5 处置 | V3 处置 |
| --- | --- | --- | --- |
| 首包非 CONNECT | 版本未知 | 直接关闭（无法编码 DISCONNECT） | 直接关闭 |
| 固定头非法 | 解码器 | 0x81 后关闭 | 直接关闭 |
| 正文解析失败 | 解码器 | 0x81 | 直接关闭 |
| 语义违反 | 各处理器 | 具体码（§5.7.4），缺省 0x82 | 直接关闭 |
| 超过 Receive Maximum | 流控 | 0x93 | 不适用（无该语义） |
| 超过 Maximum Packet Size | 适配器 | 0x95 | 不适用 |
| 认证/授权失败 | validator | CONNACK 0x87/0x88 后关闭 | CONNACK 对应码后关闭 |

所有 V5 错误路径都必须：先尝试发 DISCONNECT，再关闭；发送失败则直接关闭。

### 8.2 出站裁剪

- PUBLISH：超过对端 Maximum Packet Size → 丢弃，记 `Warning`，不中断连接。
- 其它控制报文：超过上限 → 记 `Error` 并断开（属于配置/实现错误）。
- Topic Alias 只减少长度，不作为“超限补救”手段。

### 8.3 SUBACK 原因码

| 情形 | 码 |
| --- | --- |
| 成功 | 授予的 QoS（0/1/2） |
| 过滤器语法非法 | 0x8F |
| 通配符不可用 | 0xA2 |
| 共享订阅不可用 | 0x9E |
| QoS 超出 Maximum QoS | 0x9B |
| 未授权 | 0x87 |

**注（对既有审计结论的修正）**：`NoLocal=1` 的共享订阅按规范是 Protocol Error（MQTT-3.8.3-5），规范未为其分配专用码，因此使用 0x82，而不是 0x9E。0x9E 的规范含义是“服务端不支持共享订阅”，仅用于能力位为 0 的场景。该修正记入 §14 D9。

### 8.4 服务端主动拒绝连接

| 情形 | CONNACK 码 |
| --- | --- |
| Retain 不可用但 Will Retain=1 | 0x9A |
| Will QoS 超出 Maximum QoS | 0x9B |
| 不支持共享订阅（仅 Will 无关，不适用） | — |
| 会话过期值非法（配置下限冲突） | 0x82 |
| 客户端标识符非法/为空且不允许自动分配 | 0x85 |

拒绝后必须关闭网络连接（规范要求）。

---

## 九、测试策略

### 9.1 测试项目

`Src/SAEA.MQTT.Tests/SAEA.MQTT.Tests.csproj`，参照 `Src/SAEA.Common.Tests`：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="..." />
    <PackageReference Include="MSTest.TestAdapter" Version="..." />
    <PackageReference Include="MSTest.TestFramework" Version="..." />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\SAEA.MQTT\SAEA.MQTT.csproj" />
  </ItemGroup>
</Project>
```

版本号与 `SAEA.Common.Tests` 保持一致，避免引入第二套包版本。

### 9.2 测试基建

| 工具 | 用途 |
| --- | --- |
| `MqttTestServer` | 在随机端口启动 `MqttServer`，返回端口与停止句柄 |
| `MqttTestClient` | 封装 `MqttClient`（V310/V311/V500），提供原始报文注入能力 |
| `RawMqttConnection` | 直接 TCP + 手写 V5 报文字节，验证服务端对畸形/越界输入的反应 |
| `FakeMqttTimeProvider` | 实现 `IMqttTimeProvider`，可 `Advance(TimeSpan)` |
| `TestClockScope` | `IDisposable`，替换/恢复 `MqttTime.Provider` |
| `PacketCapture` | 在 `MqttChannelAdapter` 编解码处记录字节，用于 V3 黄金比对 |

### 9.3 用例矩阵

**能力协商**
- V5 默认配置：CONNACK 不写任何“等于默认值”的属性。
- 能力全关：CONNACK 写出 5 个 0 位。
- `TopicAliasMaximum` 不再硬编码为 65535。
- 客户端 CONNECT 带 `RequestProblemInformation=0`：CONNACK 不含 ReasonString。
- V310/V311/V500 在同一端口依次连接成功。

**Session Expiry**
- expiry=0：断开即删除会话，重连 SessionPresent=0。
- expiry=30：假时钟 +29s 重连 → SessionPresent=1；+31s → 0。
- expiry=UINT_MAX：长时间推进不删除。
- CleanStart=1 覆盖已有会话。
- 服务端 `MaximumSessionExpiryInterval` 下调并在 CONNACK 回写。
- 会话过期时共享订阅成员被注销（分发不再选中该成员）。

**Will / Will Delay**
- 正常 DISCONNECT 0x00：不发布 will。
- 0x04：发布 will。
- 协议错误断开：**发布** will。
- WillDelay=5：+4s 重连 → 不发布；+6s → 发布。
- 接管 + CleanStart=0 + WillDelay=0 → 立即发布。
- 接管 + CleanStart=0 + WillDelay>0 → 不发布。
- 接管 + CleanStart=1 → 发布。
- Will Retain=1 → retained 存储更新。

**Message Expiry**
- 入队后过期：不出队发送。
- 转发时属性为剩余秒数（非原值）。
- retained 过期：新订阅者收不到，且条目被删除。
- 持久化后重载：过期时间保持。
- V3：无属性、无行为变化。

**Topic Alias**
- 入站 alias=0 → 0x94。
- 入站 alias > 通告上限 → 0x94。
- 入站仅 alias（空 Topic）+ 已建映射 → 正确路由。
- 入站未知 alias → 0x94（不是静默忽略）。
- 出站：客户端上限=0 时从不带 alias；>0 时第二次起空 Topic。
- 跨连接不复用映射。

**Shared Subscriptions**
- `$share/g/topic` 两个成员：消息只投一份且轮转。
- 两个不同 ShareName：各投一份。
- 共享 + 非共享并存：各自独立。
- `NoLocal=1` 的共享订阅 → 0x82。
- 新成员不补发 retained。
- 成员会话结束后不再被选中。
- `SharedSubscriptionAvailable=0` → SUBACK 0x9E。

**订阅选项**
- `NoLocal`：发布者自己收不到。
- `RetainAsPublished=false/true` 的标志位差异。
- `RetainHandling=0/1/2` 三种补发行为。
- 多过滤器匹配：一份消息、多个订阅标识符、QoS 取最大。

**流控**
- 服务端 Receive Maximum=1：并发两条 QoS1，第二条触发 0x93。
- QoS2 配额在 PUBCOMP 后释放（PUBREC 后仍占用）。
- 客户端 Receive Maximum 生效。
- 两条方向的最大包大小：超限 PUBLISH 被丢弃且连接存活。
- 入站超限包 → 0x95。

**V3 回归**
- V311 CONNACK 字节与基线一致（§9.4）。
- V310 同样。
- 以上全部 V5 用例在 V311 客户端下不产生属性、不产生新报文。

### 9.4 V3 字节级回归

1. 在改动前用当前代码生成并保存基线：CONNECT → CONNACK、SUBSCRIBE → SUBACK、PUBLISH（QoS0/1/2）、PINGREQ/RESP、DISCONNECT。
2. 以固定客户端 ID、固定 flags、固定 payload 生成确定性字节。
3. 测试中同一输入重放，逐字节比较。
4. 任何差异都必须解释为“仅新增内部字段/无报文变化”。

### 9.5 运行

```bash
dotnet build Src/SAEA.MQTT/SAEA.MQTT.csproj -c Release
dotnet test  Src/SAEA.MQTT.Tests/SAEA.MQTT.Tests.csproj -c Release
```

注意：仓库根目录没有 `.sln`，因此按项目路径构建与测试。

---
## 十、实施阶段

每个阶段独立可编译、可测试、可回滚；阶段结束必须 `dotnet build` + `dotnet test` 通过，且不修改上一阶段的验收结论。行号以实际代码为准。

### P1 — 能力协商、CONNACK、测试骨架

**目标**：服务端能按配置写出正确的 CONNACK 能力属性；V3 字节不变；测试项目可运行。

| 任务 | 内容 | 主要文件 |
| --- | --- | --- |
| P1.1 | 新建 MSTest 项目并加入仓库引用；csproj 增加 `InternalsVisibleTo` | `Src/SAEA.MQTT.Tests/*`、`Src/SAEA.MQTT/SAEA.MQTT.csproj` |
| P1.2 | 新增 `MqttServerCapabilities`；`IMqttServerOptions.Capabilities`；builder 链式方法；启动时快照与校验（ReceiveMaximum>0、MaximumPacketSize 钳制） | `Server/MqttServerCapabilities.cs`、`IMqttServerOptions.cs`、`MqttServerOptions.cs`、`MqttServerOptionsBuilder.cs`、`MqttServer.cs` |
| P1.3 | 新增 `MqttConnectionCapabilities` 与 `MqttCapabilityNegotiator` | `Internal/*.cs` |
| P1.4 | `MqttPacketFormatterAdapter.AttachCapabilities`；`MqttV500DataConverter` 持有上下文 | `Formatter/MqttPacketFormatterAdapter.cs`、`Formatter/V5/MqttV500DataConverter.cs` |
| P1.5 | 重写 `CreateConnAckPacket`，删除硬编码 `TopicAliasMaximum=65535`；`CreateClientConnectResult` 补齐属性 | 同上 |
| P1.6 | 用例：默认不写属性、全关写 0、V310/V311/V500 同端口共存 | `Capabilities/*.cs` |

**完成定义**：V5 CONNACK 字节可断言；V311/V310 CONNACK 与基线一致；`dotnet test` 全绿。

---

### P2 — 时间源、Session Expiry、Will Delay

**目标**：过期与 will 延迟由真实时钟驱动、可用假时钟确定性测试；接管竞态消除。

| 任务 | 内容 | 主要文件 |
| --- | --- | --- |
| P2.1 | `IMqttTimeProvider`、`MqttSystemTimeProvider`、`MqttTime` | `Internal/*.cs` |
| P2.2 | `MqttExpiryScheduler`（手写最小堆 + 单 Timer + 惰性取消 + `RunDueTasks`） | `Internal/MqttExpiryScheduler.cs` |
| P2.3 | `MqttSessionExpiryManager`；按 §5.2 状态机处理会话去留 | `Server/MqttSessionExpiryManager.cs` |
| P2.4 | will 所有权迁移到连接；`HandleConnectionClosed` 统一入口；`epoch` 幂等 | `Server/MqttClientConnection.cs`、`MqttClientSessionsManager.cs`、`MqttClientSession.cs` |
| P2.5 | 收敛停止路径为 `StopAsync(reason, ...)`；协议错误也走 will 发布 | 同上 |
| P2.6 | 用例：§9.3 的 Session Expiry 与 Will 两组 | `SessionExpiry/*.cs`、`Will/*.cs` |

**完成定义**：假时钟推进可验证过期；接管四种组合全部符合 D7；无 `Task.Delay` 参与正确性判定。

---

### P3 — Message Expiry

**目标**：消息在队列、转发、retained、持久化四处都遵守过期语义。

| 任务 | 内容 | 主要文件 |
| --- | --- | --- |
| P3.1 | 队列项增加 `ExpiresAt/EnqueuedAt/SubscriptionIdentifier`；入队计算 | `Server/MqttQueuedApplicationMessage.cs`、`MqttClientSessionApplicationMessagesQueue.cs` |
| P3.2 | 出队与发送前丢弃过期消息；转发时写剩余秒数 | `Server/MqttClientConnection.cs`、`Formatter/V5/MqttV500DataConverter.cs` |
| P3.3 | retained 增加 `RetainedAt/ExpiresAt`；读取时过滤并删除 | `Server/MqttRetainedMessagesManager.cs` |
| P3.4 | 持久化载荷扩展；旧载荷兼容读取 | `Server/IMqttServerStorage.cs`、`MqttRetainedMessagesManager.cs` |
| P3.5 | 用例：§9.3 的 Message Expiry 组 | `MessageExpiry/*.cs` |

**完成定义**：过期消息不投递；剩余值正确；V3 无属性新增。

---

### P4 — Topic Alias 双向

**目标**：服务端与客户端都能正确建立、校验、销毁别名映射。

| 任务 | 内容 | 主要文件 |
| --- | --- | --- |
| P4.1 | `MqttTopicAliasResolver`（入站）与 `MqttTopicAliasAssigner`（出站） | `Internal/*.cs` |
| P4.2 | 服务端入站接入 + 非法别名走 0x94 | `Server/MqttClientConnection.cs` |
| P4.3 | 服务端出站按客户端上限分配别名 | 同上、`Formatter/V5/MqttV500DataConverter.cs` |
| P4.4 | 客户端双向实现；连接重置 | `Client/MqttClient.cs`、`Adapter/MqttChannelAdapter.cs` |
| P4.5 | 用例：§9.3 的 Topic Alias 组 | `TopicAlias/*.cs` |

**完成定义**：未知别名不再静默忽略；`TopicAliasMaximum` 被真实执行；跨连接零残留。

---

### P5 — Shared Subscriptions 与订阅选项

**目标**：`$share` 可解析、可分摊、可清理；四个订阅选项真实生效；订阅标识符全链路传递。

| 任务 | 内容 | 主要文件 |
| --- | --- | --- |
| P5.1 | `MqttTopicFilter` 解析 `$share`；非法结构返回 0x8F | `MqttTopicFilter.cs`、`Server/MqttClientSubscriptionsManager.cs` |
| P5.2 | `MqttSubscriptionMatch` + `CheckSubscriptionsResult.Matches` | `Server/CheckSubscriptionsResult.cs`、`MqttSubscriptionMatch.cs` |
| P5.3 | `MqttSharedSubscriptionRegistry` + 会话生命周期挂钩 | `Server/MqttSharedSubscriptionRegistry.cs`、`MqttClientSessionsManager.cs`、`MqttSessionExpiryManager.cs` |
| P5.4 | 分发算法接入（非共享 + 共享、NoLocal、retained 抑制、不重投） | `Server/MqttClientSessionsManager.cs`、`MqttClientSession.cs` |
| P5.5 | `RetainAsPublished`/`RetainHandling`/`SubscriptionIdentifier` 生效 | `Server/MqttClientConnection.cs`、`Formatter/V5/MqttV500DataConverter.cs` |
| P5.6 | 用例：§9.3 的 Shared 与订阅选项两组 | `SharedSubscriptions/*.cs`、`SubscriptionOptions/*.cs` |

**完成定义**：同组只投一份且轮转；成员结束即注销；订阅标识符出现在转发 PUBLISH。

---

### P6 — 流控、包大小、原因码闭环

**目标**：Receive Maximum / Maximum Packet Size 双向执行；V5 断开必带正确原因码；客户端 DISCONNECT 属性可用。

| 任务 | 内容 | 主要文件 |
| --- | --- | --- |
| P6.1 | `MqttInboundFlowControl`；服务端入站配额；QoS2 在 PUBCOMP 释放 | `Internal/MqttInboundFlowControl.cs`、`Server/MqttClientConnection.cs` |
| P6.2 | 服务端出站在途断言 | 同上 |
| P6.3 | 适配器双向包大小校验（入站读头前、出站编码后） | `Adapter/MqttChannelAdapter.cs` |
| P6.4 | 客户端发送配额 `SemaphoreSlim` + 入站校验 | `Client/MqttClient.cs` |
| P6.5 | `MqttClientDisconnectOptions` 扩展；不再覆盖原因码；写 Properties | `Client/Disconnecting/MqttClientDisconnectOptions.cs`、`Formatter/V5/MqttV500DataConverter.cs`、`Client/MqttClient.cs` |
| P6.6 | 原因码映射表集中实现，所有断开路径复用 | `Server/MqttClientConnection.cs` 或新增 `MqttReasonCodeMapper.cs` |
| P6.7 | 用例：§9.3 的流控组 | `FlowControl/*.cs` |

**完成定义**：并发 QoS1 超额触发 0x93；QoS2 配额时机正确；超限包不被分配；客户端原因码不被覆盖。

---

### P7 — 文档、版本、全量回归

| 任务 | 内容 |
| --- | --- |
| P7.1 | 更新 `Src/SAEA.MQTT/README.md`（若存在）与根 README 的 MQTT 能力说明 |
| P7.2 | 提升 `SAEA.MQTT.csproj` 的 `Version` |
| P7.3 | 跑完整测试矩阵 + V3 黄金字节比对 |
| P7.4 | 检查 `Src/SAEA.MQTTTest` 等引用方是否因公共 API 变更而编译失败，并修复 |
| P7.5 | 输出验收报告（对应 §11 逐条勾选） |

**完成定义**：全仓相关项目构建通过；验收清单 100% 有证据。

---

## 十一、验收标准

| # | 验收项 | 证据 |
| --- | --- | --- |
| A1 | 同一端口接受 V310/V311/V500 | 共存测试日志 |
| A2 | V311 为客户端默认；V500 需显式开启 | 选项单测 + 连接结果 |
| A3 | V3 CONNACK/SUBACK/PUBLISH 字节无变化 | 黄金字节比对 |
| A4 | CONNACK 按配置写出能力，且不再硬编码 `TopicAliasMaximum` | 报文断言 |
| A5 | 每个为 0 的能力都有对应拒绝/错误码用例 | §5.1.3 用例 |
| A6 | Session Expiry 四种取值行为正确 | 假时钟用例 |
| A7 | Will Delay 与接管四组合正确 | 接管用例 |
| A8 | 协议错误断开仍发布 will | 原始报文注入用例 |
| A9 | 消息过期在队列/转发/retained/持久化四处生效 | 四组用例 |
| A10 | Topic Alias 双向合法/非法路径正确 | 用例 + 字节断言 |
| A11 | 共享订阅只投一份、轮转、可清理、不补 retained | 用例 |
| A12 | NoLocal/RAP/RetainHandling/SubscriptionIdentifier 生效 | 用例 |
| A13 | Receive Maximum 双向生效；QoS2 在 PUBCOMP 释放 | 配额用例 |
| A14 | Maximum Packet Size 双向生效 | 越界用例 |
| A15 | V5 断开原因码符合映射表 | 原因码用例 |
| A16 | 客户端 DISCONNECT 可携带属性且不覆盖原因码 | 客户端用例 |
| A17 | `dotnet test` 全绿，无跳过 | CI 输出 |

---

## 十二、风险与缓解

| 风险 | 影响 | 缓解 |
| --- | --- | --- |
| `netstandard2.0` + C# 8 缺少现代集合/异步原语 | 调度器与流控实现受限 | 手写最小堆；`SemaphoreSlim`/`lock`；不引入新依赖 |
| 公共 API 变更导致 `SAEA.MQTTTest` 等引用方编译失败 | 仓库构建红 | P1 结束即全仓构建；P7.4 集中修复 |
| 会话接管与新连接并发导致死锁/误删 | 数据错乱 | 统一 epoch + 单一入口 + 锁序约定；专项压测用例 |
| 共享订阅注册表泄漏 | 内存增长 | 所有会话结束路径强制 `RemoveClient`；用例断言注册表计数 |
| 到期调度器 Timer 回调异常 | 调度线程崩溃 | 回调级 try/catch + 日志 |
| V3 行为被能力分支污染 | 兼容性回归 | 所有新增逻辑以 `IsV5` 短路；黄金字节测试作为门禁 |
| 出站串行模型下 Receive Maximum 断言误触发 | 误断开 | 断言上限取客户端值，缺省 65535；仅记录并保护 |
| retained 持久化格式变更导致旧数据不可读 | 升级失败 | 读取侧兼容无时间戳载荷 |
| 过度实现导致范围膨胀 | 交付延期 | §13 明确不做项；每阶段独立验收 |

---

## 十三、明确不做

- MQTT-SN、MQTT over UDP、WebSocket 传输改造。
- 桥接、集群、共享持久化后端。
- 具体增强认证算法（OAuth2、SCRAM、Kerberos）。
- QoS 3。
- 进程重启后的会话恢复（仅扩展持久化字段）。
- 服务端出站并发化 / 发送窗口调度。
- 主题别名的最优淘汰策略（先用 FIFO）。
- 规则引擎、消息路由 DSL。

---

## 十四、决策记录

| 编号 | 决策 | 理由 |
| --- | --- | --- |
| D1 | 全局 `MqttSharedSubscriptionRegistry` 挂在 `MqttClientSessionsManager`；每会话订阅管理器只存成员关系 | 共享订阅作用域是 Server，不是 Session |
| D2 | 本轮不实现重启会话恢复，只扩展 `IMqttServerPersistedSession` 字段 | 控制范围；避免引入恢复状态机 |
| D3 | 服务端出站保持串行；`ReceiveMaximum` 作为上限断言而非并发重构 | 现有模型在途恒为 1，重构收益低、风险高 |
| D4 | 内部 `IMqttTimeProvider` + `InternalsVisibleTo`；公共 options 不暴露时钟 | 可测试性与公共面稳定兼得 |
| D5 | `NoLocal`/`RetainAsPublished`/`RetainHandling` 对所有 V5 订阅生效；匹配契约返回选项集合 | 选项已存在但从未执行，属于“假支持” |
| D6 | `WithMaximumSessionExpiryInterval(uint?)`，默认 `null` = 不下调 | 默认行为最保守，不改变客户端期望 |
| D7 | 接管时 `WillDelayInterval==0` 立即发布；`>0` 取消 | 符合规范非规范说明与 MQTT-3.1.3-9 |
| D8 | 保持 `MqttClientAuthenticateResult` 可空公共形状，V5 默认值折叠到内部能力对象 | 避免公共语义突变 |
| D9 | `NoLocal=1` 的共享订阅使用 0x82，而非 0x9E | 规范仅将 0x9E 定义为“不支持共享订阅”；NoLocal 违规无专用码，按 Protocol Error 处理 |

---

## 附录 A：规范条款索引

| 主题 | 条款 |
| --- | --- |
| Will Message 发布条件 | MQTT-3.1.2-8、3.1.2-10 |
| Will Delay | 3.1.3.2.2、MQTT-3.1.3-9 |
| Session Expiry | 3.1.2.11.2、MQTT-3.1.2-23、3.2.2.3.2 |
| Receive Maximum | 3.1.2.11.3、3.2.2.3.3、4.9、MQTT-4.9.0-1..3 |
| Maximum Packet Size | 3.1.2.11.4、3.2.2.3.6、MQTT-3.1.2-24 |
| Topic Alias | 3.3.2.3.4、MQTT-3.3.2-7..11 |
| Shared Subscriptions | 4.8.2、MQTT-4.8.2-1..6 |
| No Local | MQTT-3.8.3-4、MQTT-3.8.3-5 |
| Subscription Identifier | 3.3.2.3.8、3.8.2.1.2 |
| 能力位 | 3.2.2.3.9–3.2.2.3.13 |
| DISCONNECT 属性 | 3.14.2.2.2、MQTT-3.14.2-2 |
| 错误处理 | 4.13 |
