# SAEA.MQTT MQTT 5.0 运行时语义补齐 — 实施计划

- 日期：2026-11-05
- 依据：`docs/superpowers/specs/2026-11-05-mqtt5-runtime-semantics-design.md`（v2）
- 目标：按 P1–P7 分批落地，每批独立可编译、可测试、可回滚
- 执行方式：每个任务交给一个 sub-agent；主会话负责验收、冲突消解与阶段门禁

---

## 零、执行约定（所有 sub-agent 必读）

### 0.1 仓库与构建

- 仓库根：`D:\WorkBench\Walle\SAEA`
- **没有 `.sln`**，必须按项目路径构建：

```bash
dotnet build Src/SAEA.MQTT/SAEA.MQTT.csproj -c Release
dotnet test  Src/SAEA.MQTT.Tests/SAEA.MQTT.Tests.csproj -c Release
```

- 目标框架 `netstandard2.0`，`LangVersion 8.0`，未启用 `<Nullable>`。不要使用：
  - `PriorityQueue<,>`、`System.Threading.Channels`、`record`、`init`、文件作用域命名空间、`required`、模式匹配新语法（C#9+）。
- 可用：`Span<T>`/`Memory<T>`（项目已启用 `AllowUnsafeBlocks`）、`ValueTuple`、`SemaphoreSlim`、`System.Threading.Timer`。

### 0.2 代码风格

- 现有文件顶部有版权块，新增文件保持一致（复制同目录文件的头部，改文件名/命名空间）。
- 命名空间使用块式 `namespace SAEA.MQTT.Xxx { ... }`。
- 不重排、不格式化未改动的代码；不做无关清理。
- 私有字段 `_camelCase`；`readonly` 能加就加；`ConfigureAwait(false)` 与服务端现有风格一致。

### 0.3 工作区隔离

- 当前工作区已有大量与本任务无关的未提交修改（Http/MVC/WebSocket/README 等）。**绝对不要** `git checkout`、`git reset`、`git stash`、`git clean` 或回滚任何非本任务文件。
- 只修改本计划列出的 `Src/SAEA.MQTT/**` 与 `Src/SAEA.MQTT.Tests/**` 文件。
- 不要提交 commit；由主会话统一验收后处理。

### 0.4 兼容性红线

- V310/V311 的报文字节、会话清理、will 行为、`CleanSession` 语义保持不变。
- 所有 V5 新逻辑必须在 `capabilities.IsV5` 或 `ProtocolVersion == V500` 处短路。
- 新增公共 API 可以破坏编译，但不得删除既有公共属性/方法；只允许“新增 + 修正语义”。

### 0.5 每个任务的交接格式

sub-agent 完成后必须报告：

1. 改动文件清单（绝对或仓库相对路径）。
2. 构建与测试命令及原始结果（通过/失败数）。
3. 与计划不一致之处及原因。
4. 未完成项/已知风险。

主会话只有在“构建通过 + 该阶段用例通过 + 无越界改动”时才进入下一阶段。

---

## 一、依赖关系与执行顺序

```
P1 能力协商/测试骨架
 └─► P2 时间/会话过期/will
      └─► P3 消息过期
           ├─► P4 Topic Alias
           ├─► P5 共享订阅/订阅选项
           └─► P6 流控/包大小/原因码
                └─► P7 文档/版本/全量回归
```

- P4、P5、P6 理论上可并行，但都改 `MqttClientConnection.cs` / `MqttClient.cs`，**串行执行**以避免冲突。
- P1 是所有阶段的前置：没有能力上下文，后续无法区分 V3/V5 行为。

---

## P1 — 能力协商、CONNACK、测试骨架

### T1.1 新建测试项目

**产出**

- `Src/SAEA.MQTT.Tests/SAEA.MQTT.Tests.csproj`
- `Src/SAEA.MQTT.Tests/TestInfrastructure/TestTimeProvider.cs`（先建空壳，P2 使用）
- `Src/SAEA.MQTT/SAEA.MQTT.csproj` 增加 `InternalsVisibleTo`

**csproj 内容**（与 `SAEA.Common.Tests` 对齐版本）：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <LangVersion>10.0</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="MSTest.TestAdapter" Version="3.1.1" />
    <PackageReference Include="MSTest.TestFramework" Version="3.1.1" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\SAEA.MQTT\SAEA.MQTT.csproj" />
  </ItemGroup>
</Project>
```

`SAEA.MQTT.csproj` 增加：

```xml
<ItemGroup>
  <InternalsVisibleTo Include="SAEA.MQTT.Tests" />
</ItemGroup>
```

**验证**

```bash
dotnet build Src/SAEA.MQTT.Tests/SAEA.MQTT.Tests.csproj -c Release
```

**完成定义**：空项目可构建；`internal` 类型可在测试项目中访问（先用一个占位测试引用 `SAEA.MQTT` 的 internal 类型验证，验证后可删）。

---

### T1.2 服务端能力配置面

**新增** `Src/SAEA.MQTT/Server/MqttServerCapabilities.cs`

按设计 §4.1 定义 POCO，默认值必须是规范默认值。

**修改**

- `Server/IMqttServerOptions.cs`：增加 `MqttServerCapabilities Capabilities { get; }`
- `Server/MqttServerOptions.cs`：实现属性（返回实例，可在 builder 中替换）
- `Server/MqttServerOptionsBuilder.cs`：按设计 §4.1 增加 9 个 `WithXxx`
- `Server/MqttServer.cs`：`StartAsync` 中快照并校验
  - `ReceiveMaximum == 0` → 抛 `MqttConfigurationException`
  - `MaximumPacketSize == 0` → 钳制为 1；`> 268435455` → 钳制为 268435455
  - `MaximumQoS` 只能是 0/1/2 对应枚举；`ExactlyOnce` 表示不限制
  - `ServerKeepAlive == 0` → 抛 `MqttConfigurationException`（0 在 V5 中是禁用 keepalive，若允许需显式区分；本轮按非法处理）
- 快照存到 `MqttServer` 的字段，并传给 `_clientSessionsManager`（新增构造参数或内部属性）

**注意**：`MqttServerOptionsBuilder` 现有 `Build()` 逻辑不要改坏；新方法保持链式返回 `this`。

**验证**：`dotnet build Src/SAEA.MQTT/SAEA.MQTT.csproj -c Release`

---

### T1.3 连接能力上下文与协商器

**新增**

- `Src/SAEA.MQTT/Internal/MqttConnectionCapabilities.cs`（设计 §6.1）
- `Src/SAEA.MQTT/Internal/MqttCapabilityNegotiator.cs`

`Negotiate` 签名：

```csharp
internal static MqttConnectionCapabilities Negotiate(
    MqttServerCapabilities serverCapabilities,
    MqttConnectPacket connectPacket,
    MqttProtocolVersion protocolVersion,
    string assignedClientIdentifier)
```

**实现要点**

- 只用 `MqttConnectPacket` 的公共属性；V310/V311 的 `Properties` 为 null，全部按默认值。
- `connectPacket.Properties` 可能为 null，需空安全。
- 客户端 ReceiveMaximum / MaximumPacketSize / TopicAliasMaximum 从 CONNECT 属性读取，缺省分别为 65535 / 268435455 / 0。
- `SessionExpiryInterval = Min(clientExpiry, server.MaximumSessionExpiryInterval ?? uint.MaxValue)`。
- `RequestProblemInformation` 缺省 true。
- `AssignedClientIdentifier`：客户端 ClientId 为空时生成（沿用现有生成规则；当前可能没有该规则，若为空则用 `"saea-" + Guid.NewGuid().ToString("N")`，并在 §5.1 记录）。

**验证**：先写纯单测 `Capabilities/MqttCapabilityNegotiatorTests.cs`（可直接在 T1.6 补齐）。

---

### T1.4 格式化器接入能力上下文

**修改**

- `Formatter/MqttPacketFormatterAdapter.cs`
  - 增加 `internal MqttConnectionCapabilities Capabilities { get; private set; }`
  - 增加 `internal void AttachCapabilities(MqttConnectionCapabilities capabilities)`
  - 在 `AttachCapabilities` 中若当前 formatter 为 V5，调用其 converter 的 internal setter
- `Formatter/V5/MqttV500DataConverter.cs`
  - 增加 `internal MqttConnectionCapabilities Capabilities { get; set; }`

**调用点**

- `Server/MqttClientSessionsManager.HandleClientConnectionAsync`：`ValidateConnectionAsync` 之后、拒绝分支（`connectionValidatorContext.ReasonCode != Success`）与 `CreateClientConnection` 之前执行协商并 `AttachCapabilities`。这是**唯一正确注入点**，因为 CONNACK 有两个构造点（拒绝路径与 `MqttClientConnection.RunInternalAsync` 接受路径）。
- 协商使用 `connectionValidatorContext.AssignedClientIdentifier`（validator 可能已写入）。
- 若协商失败（配置异常），在发送 CONNACK 前记录并关闭连接。

**验证**：构建通过；T1.6 的 CONNACK 用例能读到上下文。

---

### T1.5 CONNACK 与客户端连接结果

**修改** `Formatter/V5/MqttV500DataConverter.cs`

`CreateConnAckPacket` 按设计 §5.1.1 表格重写：

- 删除 `TopicAliasMaximum = ushort.MaxValue`。
- 仅写“非默认值”的属性。
- `MaximumQoS` 为 `ExactlyOnce` 时不写（规范默认 2）。
- `ReasonString`/`UserProperty` 受 `RequestProblemInformation` 控制。
- `SessionExpiryInterval` 仅在与 CONNECT 值不同时写。

`CreateClientConnectResult` 补齐：

- `ReceiveMaximum`、`MaximumPacketSize`、`TopicAliasMaximum` 读属性；缺省保留 null。
- `MaximumQoS` 保持现有“折算为枚举”的行为（非可空），但 `ExactlyOnce` 表示 2。
- 新增 `SessionExpiryInterval`、`SharedSubscriptionAvailable`、`SubscriptionIdentifiersAvailable`。

**同时修改** `Client/Connecting/MqttClientAuthenticateResult.cs` 增加上述三个属性。

**验证**：`dotnet build`；T1.6 字节断言。

---

### T1.6 P1 测试与门禁

**新增**

- `Src/SAEA.MQTT.Tests/TestInfrastructure/MqttTestServer.cs`
  - 随机端口启动 `MqttServer`，提供 `Port`、`StopAsync`、`DisposeAsync`
  - 使用 `MqttServerOptionsBuilder.WithDefaultEndpointPort(0)`（若无 0 端口支持，则用 `TcpListener` 探测空闲端口后传入）
- `Src/SAEA.MQTT.Tests/TestInfrastructure/MqttTestClient.cs`
  - 封装 `MqttClient`，支持 `V310/V311/V500`
  - 提供 `ConnectAsync`、`SubscribeAsync`、`PublishAsync`、`DisconnectAsync`
- `Src/SAEA.MQTT.Tests/Capabilities/ConnAckCapabilityTests.cs`

**用例**

1. V5 默认配置：CONNACK 不包含 `TopicAliasMaximum`、`ReceiveMaximum`、`MaximumPacketSize` 等默认值属性。
2. 能力全关（Retain/Wildcard/SubId/Shared=false，MaximumQoS=AtMostOnce，ReceiveMaximum=10，MaximumPacketSize=1024，TopicAliasMaximum=5）：CONNACK 全部正确写出。
3. 不再硬编码 65535：默认 V5 CONNACK 的 `TopicAliasMaximum` 必须为 null（或 0，取决于实现读取方式；以属性缺失为准）。
4. `RequestProblemInformation=0`：带 ReasonString 的拒绝场景不写 ReasonString。
5. 同端口 V310/V311/V500 依次连接成功，且各自 `MqttClientAuthenticateResult` 正常。

**额外：V3 黄金字节**

- `Src/SAEA.MQTT.Tests/Compatibility/V3GoldenBytesTests.cs`
- 先在本阶段用**改动前**的代码生成基线（若已改动，则从 git HEAD 的 `Src/SAEA.MQTT` 构建一个临时基线，或手工构造期望字节）。
- 覆盖 V311 CONNECT/CONNACK、SUBSCRIBE/SUBACK、PUBLISH QoS0/1/2、PING。
- 仅比较报文层字节，不比较时间戳/客户端 ID 随机值（固定输入）。

**门禁**

```bash
dotnet test Src/SAEA.MQTT.Tests/SAEA.MQTT.Tests.csproj -c Release
```

必须全绿，且 V3 黄金用例通过。

---

## P2 — 时间源、Session Expiry、Will Delay

### T2.1 时间源与到期调度器

**新增**

- `Src/SAEA.MQTT/Internal/IMqttTimeProvider.cs`
- `Src/SAEA.MQTT/Internal/MqttSystemTimeProvider.cs`
- `Src/SAEA.MQTT/Internal/MqttTime.cs`
- `Src/SAEA.MQTT/Internal/MqttExpiryScheduler.cs`

**实现要点（设计 §3.4 / §6.8）**

- 最小堆节点：`DateTimeOffset DueAt`、`long Generation`、`Action Callback`、`bool Cancelled`。
- `Schedule` 返回 `IDisposable`，`Dispose` 仅置取消位。
- 单 `Timer`，`Change` 到堆顶；`RunDueTasks()` 供测试直接驱动。
- 回调 try/catch，异常写日志（`IMqttNetLogger`）。
- `Dispose` 时停止 Timer，清空堆，不再执行回调。

**测试** `Scheduling/MqttExpirySchedulerTests.cs`：

- 到期按时间顺序执行。
- 取消后不执行。
- 过期任务在 `RunDueTasks` 时一次全部执行。
- 回调抛异常不影响后续任务。

---

### T2.2 会话过期管理器

**新增** `Src/SAEA.MQTT/Server/MqttSessionExpiryManager.cs`

职责：

- 依据设计 §5.2 计算有效过期时间。
- 连接断开时登记会话到期与 will 到期。
- 重连时取消登记。
- 到期时删除会话、注销共享订阅（P5 接入）、发布未发布的 will。

**修改**

- `Server/MqttClientSessionsManager.cs`
  - 构造函数创建/持有 `MqttSessionExpiryManager`
  - `CleanUpClient` 拆分：连接清理 vs 会话去留
  - 会话删除统一走 `RemoveSession(clientId, epoch)`
- `Server/MqttClientSession.cs`
  - 增加设计 §6.6 的字段
- `Server/IMqttServerPersistedSession.cs`
  - 增加 `SessionExpiryInterval`、`WillDelayInterval`、`DisconnectedAt`（只加字段，不实现恢复）

**注意**：

- `EnablePersistentSessions == false` → V5 有效过期强制 0。
- V3 连接不进入新逻辑，保持 `CleanSession` 原判定。
- 会话删除必须幂等，代际不匹配时不得删除。

**测试** `SessionExpiry/SessionExpiryTests.cs`：

- expiry=0 立即删除，重连 `SessionPresent=false`。
- expiry=30：假时钟 +29s 重连 `SessionPresent=true`；+31s 重连 `false`。
- `uint.MaxValue` 永不过期。
- `MaximumSessionExpiryInterval` 下调并在 CONNACK 可见。
- `CleanStart=1` 覆盖。

---

### T2.3 Will 所有权与断开路径统一

**修改**

- `Server/MqttClientConnection.cs`
  - 新增 `_willMessage`、`_connectionEpoch`
  - 删除 `finally` 中无条件发布 will 的逻辑
  - 统一 `StopAsync(MqttClientDisconnectReason reason, string reasonString, bool sendDisconnect)`
  - DISCONNECT 入站读取 `ReasonCode` 与 `Properties`
  - 协议错误/畸形包路径也进入 `HandleConnectionClosed`
- `Server/MqttClientSessionsManager.cs`
  - 新增 `HandleConnectionClosed(...)`
  - 接管逻辑按设计 §5.3.2 表格处理
- `Server/MqttClientSession.cs`
  - `PendingWillMessage`、`WillPublished`、`DisconnectedAt`

**实现顺序建议**

1. 先引入 `epoch` 与 `HandleConnectionClosed`，保持现有行为（will 仍立即发布），跑通回归。
2. 再加入 Will Delay 调度与接管矩阵。
3. 最后删除旧的 `finally` 发布路径。

**测试** `Will/WillDelayTests.cs`：

- 0x00 不发布；0x04 发布；协议错误发布。
- WillDelay=5：+4s 重连不发布；+6s 发布。
- 接管四组合（CleanStart × WillDelay）。
- retained will 更新 retained 存储。

**门禁**：P2 全绿后再动 P3。

---
## P3 — Message Expiry

### T3.1 队列元数据与出队过滤

**修改**

- `Server/MqttQueuedApplicationMessage.cs`：增加 `SubscriptionIdentifier`、`ExpiresAt`、`EnqueuedAt`
- `Server/MqttClientSessionApplicationMessagesQueue.cs`
  - `Enqueue` 增加参数（保留旧重载转调新重载）
  - 入队时若消息带 `MessageExpiryInterval`，计算 `ExpiresAt = MqttTime.UtcNow + seconds`
  - 提供 `TryDequeue(out MqttQueuedApplicationMessage)`（若当前是直接 `Dequeue`，改造为可跳过过期项）
- `Server/MqttClientSession.cs`
  - `EnqueueApplicationMessage` 传入匹配结果中的 QoS、订阅标识符

**出站过滤位置**

- `Server/MqttClientConnection.cs` 的发送循环：取出队列项后，若 `ExpiresAt <= now` → 丢弃，继续取下一条，不发送。
- 不要丢弃后立即 break，否则队列中后续未过期消息会被饿死。

**转发属性**

- `Formatter/V5/MqttV500DataConverter.CreatePublishPacket` 或调用方：V5 且 `ExpiresAt.HasValue` 时写 `MessageExpiryInterval = 剩余秒数`（向上取整，最小 1；<=0 已在出队时丢弃）。

**注意**：消息过期只影响“首次发送”。QoS2 已发出并进入 PUBREC/PUBREL 流程的消息，ACK 处理不得因过期而跳过。

---

### T3.2 retained TTL 与持久化

**修改**

- `Server/MqttRetainedMessagesManager.cs`
  - `Dictionary<string, MqttRetainedMessageEntry>`
  - `SetMessageAsync` 记录 `RetainedAt`、`ExpiresAt`
  - `GetSubscribedMessagesAsync` 过滤过期并删除
  - `LoadAsync` 兼容旧格式（无时间戳 → `RetainedAt = now`）
- `Server/IMqttServerStorage.cs` 与其实现/序列化点
  - 增加载荷字段；若现有存储是 `MqttApplicationMessage` 列表，新增并行元数据或包装类型
  - 保持旧数据可读

**注意**

- retained 过期清理采用“读取时惰性清理”，不新增定时器。
- 过期 retained 不投递给新订阅者；`RetainHandling` 逻辑（P5）读取的是清理后的结果。

**测试** `MessageExpiry/*.cs`

- 入队后过期 → 不发送。
- 转发属性为剩余值（构造 `ExpiresAt = now + 7s`，断言写 7 或 6，按取整规则固定）。
- retained 过期 → 新订阅者收不到。
- 持久化重载后过期时间保持（可用内存存储替身）。
- V311 → 无 `MessageExpiryInterval` 属性。

---

## P4 — Topic Alias 双向

### T4.1 别名内部组件

**新增**

- `Src/SAEA.MQTT/Internal/MqttTopicAliasResolver.cs`（入站）
  - `ushort Maximum`、`Dictionary<ushort,string>`
  - `bool TryResolve(ushort alias, string topic, out string resolvedTopic, out AliasError error)`
  - `AliasError { None, Zero, ExceedsMaximum, Unknown }`
- `Src/SAEA.MQTT/Internal/MqttTopicAliasAssigner.cs`（出站）
  - `ushort Maximum`、双向字典、FIFO 淘汰
  - `bool TryAssign(string topic, out ushort alias, out bool includeTopic)`

**测试**：纯单测，不依赖连接。

---

### T4.2 服务端接入

**修改** `Server/MqttClientConnection.cs`

- 删除现有 `_topicAlias` 裸字典，改用 `MqttTopicAliasResolver`
- `HandleTopicAlias` 的未命中分支不再为空，改为返回错误并触发 0x94
- 出站发送前，用 `MqttTopicAliasAssigner` 处理 `MqttPublishPacket`：
  - 首次：写 Topic + TopicAlias
  - 后续：Topic 置空 + TopicAlias
- `Dispose` 时清空（随对象销毁自然完成）

**错误路径**：0x94 需要 P6 的原因码发送能力。若 P6 尚未实现，先在 P4 用“记录日志 + 关闭连接”的临时路径，并在 P6 替换为 DISCONNECT 0x94；用例在 P6 补断言。为避免返工，**建议 P4 先做一个最小的 `SendDisconnectAsync(reasonCode)`**，P6 再统一收敛。

---

### T4.3 客户端接入

**修改**

- `Client/MqttClient.cs`
  - 连接后读取服务端 `TopicAliasMaximum`，设置出站 assigner
  - 入站 PUBLISH 解析别名：`MqttClient` 在 `TryProcessReceivedPacketAsync` 或解码后统一解析
  - 每次 `ConnectAsync` 重置两个组件
- `Adapter/MqttChannelAdapter.cs`
  - 如客户端在解码层就能拿到别名，优先在此解析；否则在 `MqttClient` 层
- **必须修复**：空 Topic + 未知别名不能当成合法主题

**测试** `TopicAlias/*.cs`

- 服务端入站：0、超上限、未知 → 0x94
- 服务端入站：合法映射与覆盖映射
- 服务端出站：客户端上限 0 / >0 两种行为
- 客户端双向
- 跨连接不复用

---

## P5 — Shared Subscriptions 与订阅选项

### T5.1 `$share` 解析

**修改** `MqttTopicFilter.cs`

- 增加 `internal string ParsedFilter`、`internal string ShareName`
- 增加 `internal static bool TryParseShared(string topic, out string shareName, out string filter)`
- `Topic` 保持原样（公共面不变）

**修改** `Server/MqttClientSubscriptionsManager.cs`

- `SubscribeAsync` 对每个过滤器解析
- 结构非法 → 该过滤器 SUBACK 0x8F
- `NoLocal == true` 且在共享组 → 协议错误 0x82（见设计 §8.3 注）
- 合法共享 → 以 `ParsedFilter` 参与匹配，并向注册表登记

**注意**：SUBACK 原因码按过滤器逐一返回；现有实现可能只返回一个值，需要改为 `List<MqttSubscribeReasonCode>` 与过滤器一一对应。

---

### T5.2 匹配契约升级

**修改**

- `Server/CheckSubscriptionsResult.cs`：增加 `List<MqttSubscriptionMatch> Matches`
- 新增 `Server/MqttSubscriptionMatch.cs`
- `Server/MqttClientSubscriptionsManager.cs`：`CheckSubscriptions` 收集所有匹配项（不只最大 QoS）

**语义**

- `NoLocal` 过滤发生在匹配阶段，需要知道发布者 ClientId。当前 `CheckSubscriptions(topic, qos)` 没有发布者参数，需扩展为 `CheckSubscriptions(topic, qos, publisherClientId)`；保留旧重载转调 `publisherClientId = null`。
- 非共享与共享都要返回；共享的匹配由分发器决定“是否选中本会话”。

---

### T5.3 共享订阅注册表

**新增** `Server/MqttSharedSubscriptionRegistry.cs`（设计 §6.3）

**接入点**

- `MqttClientSessionsManager` 构造时创建单例
- 订阅成功 → `Add`
- 取消订阅 → `Remove`
- 会话删除/过期/CleanStart 覆盖 → `RemoveClient`
- 连接断开但会话未结束 → **保留**成员（离线会话仍可排队）

**线程安全**

- 单锁；锁内只做选择/增删，不做入队、不做日志以外的回调。

**测试**：直接对注册表做并发增删与轮转单测。

---

### T5.4 分发算法

**修改** `Server/MqttClientSessionsManager.cs` 的分发循环

伪代码：

```
foreach session in snapshot:
    match = session.SubscriptionsManager.CheckSubscriptions(topic, qos, senderClientId)
    if !match.IsSubscribed: continue

    nonShared = match.Matches.Where(!IsShared)
    if nonShared.Any():
        enqueueOne(session, mergedOptions(nonShared))

foreach sharedKey in registry.GetMatchingKeys(topic):
    if registry.TrySelect(sharedKey, out member):
        enqueueTo(member, sharedKey match options)
```

**约束**

- 每个共享键只选一个成员。
- 共享成员已被分配的会话若在投递前结束，不重投（设计 §5.6.3）。
- retained 补发跳过共享过滤器。
- 负向 PUBACK/PUBREC 不重投。

---

### T5.5 订阅选项与订阅标识符全链路

**链路**

```
MqttSubscribePacket.Properties.SubscriptionIdentifier
  → MqttClientSubscriptionsManager（存到每个 MqttTopicFilter 或匹配结构）
  → CheckSubscriptionsResult.Matches
  → MqttClientSession.EnqueueApplicationMessage
  → MqttQueuedApplicationMessage.SubscriptionIdentifier
  → MqttClientConnection 发送时构造 MqttPublishPacketProperties.SubscriptionIdentifiers
```

**修改**

- `MqttClientSubscriptionsManager.SubscribeAsync`：保存订阅标识符（注意：一个 SUBSCRIBE 包只有一个标识符，作用于该包内全部过滤器）
- `MqttClientSession.EnqueueApplicationMessage`：接收匹配结果
- `MqttQueuedApplicationMessage`：已由 P3 增加字段
- `MqttClientConnection` 发送路径：把队列项的标识符写入 PUBLISH 属性（多值）
- `RetainAsPublished` / `RetainHandling`：按设计 §5.6.4 表执行

**测试** `SharedSubscriptions/*.cs`、`SubscriptionOptions/*.cs`

- 两成员轮转
- 不同 ShareName 各一份
- 共享 + 非共享并存
- NoLocal 共享 → 0x82
- 新成员不补 retained
- 成员过期后不再选中
- `SharedSubscriptionAvailable=0` → SUBACK 0x9E
- NoLocal / RAP / RetainHandling 0/1/2
- 多过滤器匹配：一份消息、多个标识符、QoS 最大

---
## P6 — 流控、包大小、原因码闭环

### T6.1 服务端入站流控

**新增** `Src/SAEA.MQTT/Internal/MqttInboundFlowControl.cs`

```csharp
internal sealed class MqttInboundFlowControl
{
    public MqttInboundFlowControl(ushort receiveMaximum);
    public bool TryAcquire(ushort packetIdentifier, MqttQualityOfServiceLevel qos);
    public void Release(ushort packetIdentifier);
    public int Available { get; }
}
```

**修改** `Server/MqttClientConnection.cs`

- QoS1 PUBLISH 处理：`TryAcquire` 失败 → `SendDisconnectAsync(0x93)` + 关闭。
- PUBACK 发送后 `Release(id)`。
- QoS2：`TryAcquire` 在收到 PUBLISH 时；`Release(id)` **必须在发送 PUBCOMP 之后**，不能在 PUBREC 后。
- QoS0 不占用配额。
- 重复包标识符（DUP 重发）不得重复扣减：`TryAcquire` 对已存在 id 返回 true 且不扣减。

---

### T6.2 双向包大小

**修改** `Adapter/MqttChannelAdapter.cs`

- 增加 `internal uint MaximumPacketSize { get; set; } = 268435455;`
- `ReceiveAsync`：读固定头后、分配正文前，计算总长度；超限 → 抛 `MqttMaximumPacketSizeExceededException`（新增 `Internal/`）
- `SendPacketAsync`：`Encode` 后比较 `packetData.Count`；超限 → 抛同一异常

**新增** `Src/SAEA.MQTT/Internal/MqttMaximumPacketSizeExceededException.cs`（含 `Direction`、`PacketType`）

**调用方处理**

- `MqttClientConnection.SendAsync`：捕获出站异常
  - PUBLISH → 丢弃 + Warning
  - 其它 → Error + 断开
- 入站异常 → 0x95 + 关闭（V3 直接关闭）
- `MqttClient` 出站：PUBLISH 超限返回失败结果或抛 `MqttProtocolViolationException`；入站超限 0x95 后关闭

**设置时机**

- 服务端：CONNECT 协商后，`_adapter.MaximumPacketSize = capabilities.ClientMaximumPacketSize`
- 客户端：CONNACK 解析后，`_adapter.MaximumPacketSize = serverMaximum`

---

### T6.3 客户端发送配额

**修改** `Client/MqttClient.cs`

- 新增 `SemaphoreSlim _publishQuota`，初值 = 服务端 ReceiveMaximum（缺省 65535）
- `PublishAsync` QoS>0：`await _publishQuota.WaitAsync(cancellationToken)`，发送失败也要释放
- PUBACK / PUBCOMP 到达：`Release()`
- 连接关闭/重连：`Dispose` 旧信号量并新建，防止旧等待者泄漏
- QoS0 不走配额

**测试要点**

- 服务端 ReceiveMaximum=1，客户端并发两条 QoS1 → 第二条等待，第一条 ACK 后放行。
- 连接中途断开，等待者被取消而不是永久阻塞。

---

### T6.4 DISCONNECT 属性与原因码映射

**修改** `Client/Disconnecting/MqttClientDisconnectOptions.cs`

```csharp
public uint? SessionExpiryInterval { get; set; }
public List<MqttUserProperty> UserProperties { get; set; }
```

**修改** `Formatter/V5/MqttV500DataConverter.cs` → `CreateDisconnectPacket`

- 写 `ReasonString`、`SessionExpiryInterval`、`UserProperties`
- 受 Maximum Packet Size 约束（编码后校验由适配器完成）

**修改** `Client/MqttClient.cs`

- `DisconnectAsync`：**不覆盖**用户给的 `ReasonCode`
- 用户传 `null` 时用 `NormalDisconnection`
- 本地校验：CONNECT SessionExpiry==0 且 DISCONNECT 非 0 → 抛 `MqttProtocolViolationException`

**新增/集中** 原因码映射（`Server/MqttReasonCodeMapper.cs` 或 `MqttClientConnection` 私有方法）

按设计 §5.7.4 表实现；所有断开路径复用，禁止散落魔法数字。

**服务端 DISCONNECT 入站**

- 读取 `ReasonCode`、`SessionExpiryInterval`、`ReasonString`
- 0x00 → 删除 will
- 0x04 → 发布 will
- 其它 → 发布 will
- SessionExpiry 非 0 且 CONNECT 为 0 → 0x82 断开（F13）

---

### T6.5 P6 测试与门禁

`Src/SAEA.MQTT.Tests/FlowControl/*.cs`

- 服务端 ReceiveMaximum=1：并发 QoS1 第二条触发 0x93
- QoS2 配额在 PUBCOMP 后释放（PUBREC 后仍占用）
- 客户端 ReceiveMaximum 生效
- 出站超限 PUBLISH 被丢弃且连接存活
- 入站超限 → 0x95
- 原因码映射逐条断言
- 客户端 DISCONNECT 原因码不被覆盖、属性正确

**门禁**

```bash
dotnet test Src/SAEA.MQTT.Tests/SAEA.MQTT.Tests.csproj -c Release
```

---

## P7 — 文档、版本、全量回归

### T7.1 文档与版本

- 更新 `Src/SAEA.MQTT/README.md`（若存在）：
  - 默认 V311
  - V5 启用方式
  - 已实现语义清单与明确不支持项
- 更新根 `README.md` / `README.en.md` 中 MQTT 段落（只改 MQTT 相关，不动其它未提交内容）
- `Src/SAEA.MQTT/SAEA.MQTT.csproj`：`Version` 从 `26.11.5.1` 提升（建议 `26.11.6.1`），并补充 `PackageReleaseNotes`

### T7.2 全量回归与引用方修复

- 构建 `Src/SAEA.MQTT`、`Src/SAEA.MQTT.Tests`、`Src/SAEA.MQTTTest`
- 检查所有引用 `IMqttServerOptions` 的实现（当前仓库内只有 `MqttServerOptions`；`SAEA.MQTTTest` 可能自定义）
- 检查 `MqttClientAuthenticateResult` 新属性是否影响序列化/展示
- 跑 V3 黄金字节
- 跑完整用例矩阵

**若引用方编译失败**：

- 优先修引用方调用点，不删除新 API。
- 记录破坏性变更清单，供 README/发布说明使用。

### T7.3 验收报告

新增 `docs/superpowers/plans/2026-11-05-mqtt5-runtime-semantics-acceptance.md`（或直接追加到本计划末尾），逐条勾选设计 §11 的 A1–A17，并附命令输出摘要。

---

## 二、sub-agent 派发模板

每次派发使用如下结构，避免上下文漂移：

```
任务：T{x.y} {名称}
依据：docs/superpowers/specs/2026-11-05-mqtt5-runtime-semantics-design.md 第 {n} 节
范围：只允许修改下列文件 {list}
约束：
  - netstandard2.0 / C# 8，不用 C#9+ 语法与现代集合
  - 不改 V3 行为；V5 逻辑必须短路
  - 不回滚/不碰工作区其它未提交文件
  - 不提交 git
实现要点：{粘贴计划中该任务要点}
验证：dotnet build ... && dotnet test ...
交付：改动文件、命令原始输出、偏差说明、风险
```

**并行限制**：同一阶段内若多个任务改同一文件，串行；跨阶段一律串行。

---

## 三、阶段门禁脚本

每个阶段结束执行：

```bash
dotnet build Src/SAEA.MQTT/SAEA.MQTT.csproj -c Release
dotnet build Src/SAEA.MQTT.Tests/SAEA.MQTT.Tests.csproj -c Release
dotnet test  Src/SAEA.MQTT.Tests/SAEA.MQTT.Tests.csproj -c Release --logger "console;verbosity=normal"
```

门禁判定：

1. 0 warning 新增（既有 warning 不计）。
2. 0 failed / 0 skipped。
3. 本阶段新增用例全部通过。
4. `git status --short` 中本阶段改动仅限计划文件。
5. V3 黄金用例持续通过。

---

## 四、回滚策略

- 每阶段一个逻辑单元，出问题按阶段回滚：`git diff` 定位，手工还原该阶段文件（不使用 `git reset`，避免影响工作区其它改动）。
- P1 的能力上下文是后续基础，若 P1 失败，后续不启动。
- P2 的 will/会话改动风险最高：保留“旧路径开关”直到 P2 全绿，再删除旧分支。
- P6 的原因码改动面广：先集中实现 `MqttReasonCodeMapper`，再逐路径替换，便于单点回退。

---

## 五、验收映射（计划 → 设计）

| 设计验收项 | 对应任务 |
| --- | --- |
| A1/A2 | T1.1、T1.6 |
| A3 | T1.6（黄金字节）、T7.2 |
| A4/A5 | T1.2–T1.6 |
| A6 | T2.2 |
| A7/A8 | T2.3 |
| A9 | T3.1、T3.2 |
| A10 | T4.1–T4.3 |
| A11/A12 | T5.1–T5.5 |
| A13/A14/A15/A16 | T6.1–T6.5 |
| A17 | 各阶段门禁 + T7.2 |

---

## 六、已知未决点（执行时需拍板）

1. **空 ClientId 自动分配**：现有服务器是否允许空 ClientId、生成规则是什么，需在 T1.3 读代码确认；若现有行为是拒绝，则保持拒绝，`AssignedClientIdentifier` 仅在确有分配逻辑时写。
2. **`MqttSubscribeReasonCode` 现有返回结构**：若当前 SUBACK 只支持单一原因码，T5.1 需要先扩展为逐过滤器列表；这属于内部/公共变更，需在 README 记录。
3. **retained 持久化格式**：`IMqttServerStorage` 的具体实现与序列化方式需在 T3.2 读代码确认；若实现方在仓库外，采用“新增包装类型 + 兼容旧读取”。
4. **`MqttServerOptionsBuilder` 默认端口 0**：若 `WithDefaultEndpointPort(0)` 不被支持，测试基建改用端口探测。
5. **服务端 shutdown 时 will**：本轮选择直接发布；若现有 `StopAsync` 超时窗口太短，允许“记录待发布并在下次启动补发”的降级，但必须在 P7 文档注明。

---

## 七、完成定义（整体）

- 设计 §11 的 A1–A17 全部有测试或字节级证据。
- V3 黄金字节零差异。
- 同端口 V310/V311/V500 共存用例稳定通过 10 次以上（可加循环）。
- `dotnet build` 与 `dotnet test` 全绿。
- README 明确“已实现 / 未实现”，不夸大。
- 版本号已提升，发布说明包含破坏性变更清单。
