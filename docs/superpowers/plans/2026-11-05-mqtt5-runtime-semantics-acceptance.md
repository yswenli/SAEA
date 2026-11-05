# MQTT 5.0 运行时语义 — 验收报告

- 对应设计：`docs/superpowers/specs/2026-11-05-mqtt5-runtime-semantics-design.md`
- 对应计划：`docs/superpowers/plans/2026-11-05-mqtt5-runtime-semantics.md`
- 验收日期：2026-11-05
- 发布版本：`SAEA.MQTT 26.11.6.1`
- 目标框架：生产代码 `netstandard2.0` / C# 8.0；测试 `net10.0` (MSTest)

---

## 一、命令与结果摘要

```text
dotnet build Src/SAEA.MQTT/SAEA.MQTT.csproj -c Release
  -> 0 个错误（仅既有 CS0618 过时 API 警告）

dotnet build Src/SAEA.MQTTTest/SAEA.MQTTTest.csproj -c Release
  -> SAEA.MQTTTest -> bin/Release/net10.0/SAEA.MQTTTest.dll，0 个错误

dotnet test Src/SAEA.MQTT.Tests/SAEA.MQTT.Tests.csproj -c Release
  -> 已通过! - 失败: 0，通过: 153，已跳过: 0，总计: 153
```

测试数量演进：P1 基线 141 → P6b 150 → P7 收尾 153（新增
`Capabilities/DefaultProtocolVersionTests.cs` 3 个用例）。

---

## 二、设计 §11 验收清单

| # | 验收项 | 结论 | 证据（测试 / 产物） |
| --- | --- | --- | --- |
| A1 | 同一端口接受 V310/V311/V500 | ✅ | `ConnAckCapabilityTests.SamePort_AcceptsV310V311AndV500Sequentially` |
| A2 | V311 为客户端默认；V500 需显式开启 | ✅ | `DefaultProtocolVersionTests.ClientOptions_DefaultProtocolVersion_IsV311`、`ClientWithoutExplicitVersion_ConnectsAsV311`；`MqttClientOptions.ProtocolVersion` 默认 `V311` |
| A3 | V3 CONNACK/SUBACK/PUBLISH 字节无变化 | ✅ | `Compatibility/V3GoldenBytesTests` 8 个用例全部通过（Connect / ConnAck / Subscribe / SubAck / Publish QoS0-2 / Ping） |
| A4 | CONNACK 按配置写出能力，且不再硬编码 `TopicAliasMaximum` | ✅ | `ConnAckCapabilityTests.V5_DefaultConfiguration_DoesNotWriteDefaultProperties`、`V5_NonDefaultCapabilities_AreAllWrittenToConnAck`、`V5_TopicAliasMaximum_IsNotHardcodedTo65535` |
| A5 | 每个为 0 的能力都有对应拒绝/错误码用例 | ✅ | `SharedSubscriptionValidationTests`（0x9E/0xA2/0xA1）、`ServerReasonCodeTests.RetainedPublish_WhenRetainUnavailable_DisconnectsWith0x9A`、`QoSAboveServerMaximum_DisconnectsWith0x9B`、`ConnAckCapabilityTests.V5_RequestProblemInformationZero_SuppressesReasonString` |
| A6 | Session Expiry 四种取值行为正确 | ✅ | `SessionExpiryTests`：`ExpiryZero_RemovesSessionImmediately`、`Expiry30_SessionPresentBeforeExpiry`、`Expiry30_SessionGoneAfterExpiry`、`ExpiryMaxValue_NeverExpires`、`MaximumSessionExpiryInterval_DownAdjusted`、`CleanStartOne_OverridesExistingSession` |
| A7 | Will Delay 与接管四组合正确 | ✅ | `WillDelayTests.Takeover_CleanStartFalse_DelayZero_PublishesImmediately`、`Takeover_CleanStartFalse_DelayPositive_CancelsWill`、`Takeover_CleanStartTrue_PublishesWillImmediately`、`WillDelay_ReconnectBeforeDelay_DoesNotPublishWill`、`WillDelay_Elapsed_PublishesWill` |
| A8 | 协议错误断开仍发布 will | ✅ | `WillDelayTests.ProtocolError_PublishesWill`、`NormalDisconnect_DoesNotPublishWill`、`DisconnectWithWillMessage_PublishesWill` |
| A9 | 消息过期在队列/转发/retained/持久化四处生效 | ✅ | `MessageExpiryTests`：`QueuedMessageExpired_IsNotSent`、`ForwardedExpiry_IsRemainingSeconds`、`RetainedMessageExpired_IsNotDelivered`、`RetainedMessageExpiry_SurvivesPersistenceReload`、`RetainedExpiry_OverwriteRefreshesRetainedAt`、`V311_DoesNotReceiveMessageExpiryProperty` |
| A10 | Topic Alias 双向合法/非法路径正确 | ✅ | `TopicAliasIntegrationTests` 8 个用例（服务端入站 0/越界/未知→0x94、映射与覆盖、服务端出站别名、客户端双向、不跨连接复用）；`MqttTopicAliasResolverTests` / `MqttTopicAliasAssignerTests` 单元覆盖 |
| A11 | 共享订阅只投一份、轮转、可清理、不补 retained | ✅ | `SharedSubscriptionDispatchTests` 7 个用例；`MqttSharedSubscriptionRegistryTests` 8 个用例；`SharedSubscriptionValidationTests.V5_DeleteSession_RemovesRegistryMember` / `V5_Unsubscribe_RemovesRegistryMember` |
| A12 | NoLocal/RAP/RetainHandling/SubscriptionIdentifier 生效 | ✅ | `SubscriptionOptionTests` 7 个用例；`SubscriptionIdentifierTests` 3 个用例（含 V311 不收订阅标识符） |
| A13 | Receive Maximum 双向生效；QoS2 在 PUBCOMP 释放 | ✅ | `ServerReceiveMaximumTests.QoS2_QuotaReleasedAfterPubComp_AllowsNextPublish`、`QoS2_SecondPublishWithoutPubComp_DisconnectsWith0x93`、`QoS1_PipelinedPublishes_DoNotLeakQuota`；`ClientReceiveMaximumTests`、`ClientPublishQuotaTests`、`MqttInboundFlowControlTests` |
| A14 | Maximum Packet Size 双向生效 | ✅ | `MaximumPacketSizeTests` 3 个用例（入站 0x95、出站丢弃、客户端本地抛出协议违规） |
| A15 | V5 断开原因码符合映射表 | ✅ | `ServerReasonCodeTests`（0x81/0x82/0x9A/0x9B/逐过滤器 SUBACK 降级）、`TopicAliasIntegrationTests`（0x94）、`ServerReceiveMaximumTests`（0x93） |
| A16 | 客户端 DISCONNECT 可携带属性且不覆盖原因码 | ✅ | `ClientDisconnectTests.DisconnectAsync_PreservesUserReasonCodeAndProperties`、`DisconnectAsync_RaisesSessionExpiryAboveConnectValue_IsRejected` |
| A17 | `dotnet test` 全绿，无跳过 | ✅ | 153 passed / 0 failed / 0 skipped |

---

## 三、破坏性变更清单（供发布说明引用）

1. `IMqttServerOptions` 新增 `MqttServerCapabilities Capabilities { get; }`。
   自定义 `IMqttServerOptions` 实现需补充该属性。
2. `IMqttServerPersistedSession` 新增会话过期相关字段（`SessionExpiryInterval`、
   `SessionExpiryDeadline` 等），自定义持久化实现需同步。
3. `MqttClientDisconnectOptions` 新增 `SessionExpiryInterval`（`uint?`）与
   `UserProperties`（`List<MqttUserProperty>`）；V3 连接忽略。
4. `MqttServerOptionsBuilder` 新增能力配置方法（`WithReceiveMaximum`、
   `WithMaximumPacketSize`、`WithTopicAliasMaximum`、`WithRetainAvailable`、
   `WithWildcardSubscriptionAvailable`、`WithSubscriptionIdentifiersAvailable`、
   `WithSharedSubscriptionAvailable`、`WithMaximumQoS`、`WithServerKeepAlive`、
   `WithMaximumSessionExpiryInterval`）。
5. 服务端在 CONNECT 的 ClientId 为空且协议为 V5 时自动分配
   `saea-<guid>`，并在 CONNACK 回写 `Assigned Client Identifier`；
   V3 行为保持原状。

仓库内引用方 `Src/SAEA.MQTTTest` 已通过编译验证，无需修改。

---

## 四、已知限制与未覆盖项

| 项 | 说明 |
| --- | --- |
| 端口 0 | 库本身可绑定系统分配端口，但当前公共 API 不暴露实际监听端口；测试基建改为预取空闲端口（`FreePort`），未新增端口发现 API |
| 服务端关闭时的遗嘱 | 保持原有语义：`StopAsync` 不发 DISCONNECT，连接关闭后按非正常断开处理（与 V3 行为一致），未单独引入 0x8B 抑制遗嘱 |
| 重启后会话恢复 | 按设计 D2 不做；`IMqttServerPersistedSession` 仅扩展字段，Session Expiry 只作用于内存会话 |
| 增强认证 | 仅保留 `AuthenticationMethod` / `AuthenticationData` 透传，不实现 OAuth2 等具体方法 |
| 集群 / 桥接 / MQTT-SN / UDP / QoS 3 | 明确不在本轮范围 |

---

## 五、文档与版本

- `Src/SAEA.MQTT/README.md` / `README.en.md`：
  新增 MQTT 5.0 运行时语义清单、服务端能力配置示例、默认协议版本说明、
  不支持项清单，并扩充协议版本对比表。
- 根 `README.md` / `README.en.md`：仅包含 MQTT 组件入口与链接，无 MQTT 行为描述，
  本轮无需修改。
- `Src/SAEA.MQTT/SAEA.MQTT.csproj`：`Version` 提升至 `26.11.6.1`，
  `PackageReleaseNotes` 写入 5.0 语义与破坏性变更摘要。