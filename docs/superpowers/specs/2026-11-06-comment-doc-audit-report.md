# 全仓 XML 注释审查与修补报告

- 审查日期：2026-11-06
- 体例基准：`Src/SAEA.Common/IPHelper.cs`
- 审查范围：`Src/` 下 1,016 个 `.cs`（排除 `obj/`、`bin/`、`Src/SAEA.Common/Newtonsoft.Json/`、`Src/SAEA.Audio.Net/Base/NAudio/`、`Src/SAEA.Audio.Net/Base/NSpeex/`）
- 审查方式：Roslyn 语法树遍历，逐个公开成员比对注释节点（语法级判定，不校验 `<see cref>` 指向是否有效）

---

## 一、结论

1. 全仓注释偏差从 **5,755 处**（基线）降到 **6 处**。剩余 6 处全部位于 `SAEA.Audio.Net/Net/TransferClient.cs` 与 `TransferServer.cs`：这两个文件在本任务期间有用户并发改动，按约定未触碰，留待该改动提交后补（明细见 7.4）。
2. 库项目与测试演示工程（1,016 个 `.cs`、7,371 个公开成员）已按 `Src/SAEA.Common/IPHelper.cs` 体例补齐：类型与公开成员有中文 `<summary>`，方法/构造函数每个参数有 `<param>`，非 `void` 有 `<returns>`，泛型有 `<typeparam>`。
3. 数量最多的是「整段没有注释」共 4,837 处，占 84%。真正写错或写偏的（参数标签与签名不符、XML 破损、描述与实现相反）只有 **20 处 / 14 个文件**，已在批次 1 全部修完。
4. 库项目文件头 banner 从「746 合规 / 33 字段不全 / 25 无 banner / 1 旧版头」收敛到 **804 合规、0 不合规**；唯一例外是 `SAEA.Audio.Net/Properties/AssemblyInfo.cs`（程序集特性文件、无命名空间，保留原样，见第六节）。
5. 仍有 4 组重复 `唯一标识`（12 个文件）：3 组在测试工程（按约定不回填存量 banner），1 组是库项目占位 GUID `a1b2c3d4-e5f6-7890-abcd-ef1234567890`（`SAEA.Common/ObjectAsyncLock.cs`、`ObjectLock.cs`、`SAEA.DNS/Common/Utils/ObjectStringifier.cs`）。属既有 banner，按「既有 banner 保持原值」未改，列为建议项。
6. 提交按项目拆分：批次 1 `cb68a47f` 混入了 Pipeline 功能代码与 banner 更新（注释修复未独立成提交，作为历史事实保留）；批次 2 起共 41 个提交（批次 2 两个、批次 3 的库项目与测试工程各 16 个、banner 规范化 7 个），可按项目单独回滚。
7. 验证：`dotnet build Src/SAEA.Sockets.sln -c Debug` → 0 错误 / 3,721 警告（主体为 CS1591 缺失 XML 注释）；四个 MSTest 全通过：Common 59、MQTT 153、FTP 13、Sockets.Pipeline 158。

---

## 二、判定口径

| 项目 | 规则 |
| --- | --- |
| 类型（class/struct/interface/enum/delegate） | 必须有中文 `<summary>` |
| `public` / `protected` 方法、构造函数 | 必须有中文 `<summary>`；每个参数有 `<param>`；非 `void` 有 `<returns>` |
| 属性、字段、事件、枚举成员、索引器 | 必须有中文 `<summary>` |
| 摘要语言 | 中文（纯英文摘要记为偏差） |
| 参数标签 | 必须与签名参数同名；签名里没有的参数标签视为多余 |

优先级定义：

- **P0**：类型/方法/构造函数无 `<summary>`、缺 `<param>`、缺 `<returns>`、`<summary>` 为空
- **P1**：属性/字段/事件/枚举成员/索引器无 `<summary>`
- **P2**：摘要为英文、参数说明为英文、多余 `<param>`、未知标签、XML 结构异常

---

## 三、修复前基线

| 指标 | 数值 |
| --- | --- |
| 审查文件 | 1,016 个 `.cs` |
| 含公开成员的文件 | 1,006 |
| 公开类型 / 方法 / 其他成员 | 973 / 3,063 / 3,335 |
| 公开成员合计 | 7,371 |
| 已注释成员 | 2,534（34.4%） |
| 偏差总数 | 5,755（P0 3,347 / P1 2,120 / P2 288） |
| 其中库项目 | 4,744（P0 2,522 / P1 2,016 / P2 206） |
| 其中测试与演示工程 | 1,011（P0 825 / P1 104 / P2 82） |

---

## 四、批次 1：已修复的 20 处缺陷

只改注释行，未触碰任何代码语句。按缺陷类型汇总：

| 缺陷类型 | 处数 | 说明 |
| --- | ---: | --- |
| `<param>` 标签与签名不符或多余 | 13 | 标签名拼错、大小写不符、签名中已无该参数 |
| 缺 `<param>` | 1 | `description` 参数没有标签 |
| XML 结构破损 | 2 | `// <summary>` 少斜杠导致注释失效；泛型 `List<QueueMsg>` 未转义 |
| 摘要描述与实现相反 | 4 | 实现已迁移到 SAEA 收发/包模式，注释仍写 iocp |

逐文件明细：

| # | 文件 | 偏差 | 处理 |
| --- | --- | --- | --- |
| 1 | `SAEA.Common/ApiHelper.cs` | 存在签名中不存在的 `<param name="timeOut">` | 删除该标签 |
| 2 | `SAEA.Common/StringHelper.cs` | `// <summary>` 少两个斜杠，整段注释失效 | 改为 `/// <summary>` |
| 3 | `SAEA.Common/Threading/TinyThreadPool.cs` | `<param name="imThreadPoolException">` 与参数 `tinyThreadPoolException` 不符 | 标签改名 |
| 4 | `SAEA.FTPTest/Common/IconHelper.cs` | `<param name="LargeIcon">` 大小写不符；`description` 参数无标签 | 改名 + 补标签 |
| 5 | `SAEA.Http/HttpResponse.cs` | 存在签名中不存在的 `<param name="close">` | 删除该标签 |
| 6 | `SAEA.Http/HttpUtility.cs` | 存在签名中不存在的 `<param name="isData">` | 删除该标签 |
| 7 | `SAEA.MVC/EventStream.cs` | `<param name="str">` 与实际参数 `t` 不符 | 标签改名 |
| 8 | `SAEA.QueueSocket/Model/QueueMsgListPool.cs` | 摘要中 `List<QueueMsg>` 未转义，XML 中被当作标签 | 改为 `List&lt;QueueMsg&gt;`（2 处） |
| 9 | `SAEA.RPC/Model/ActionFilterAtrribute.cs` | `<param name="serviceInfo">` 不存在，实为 `serviceName` 与 `methodName` | 拆成两个标签 |
| 10 | `SAEA.RedisSocket/Base/Net/RedisCoder.cs` | `<param name="OnHeart">` 与参数 `onHeart` 大小写不符 | 标签改名 |
| 11 | `SAEA.RedisSocket/Core/Operation/RedisSetOperation.cs` | `<param name="values">` 不存在，实为 `source` 与 `destination` | 拆成两个标签 |
| 12 | `SAEA.Sockets/Core/Udp/UdpClientSocket.cs` | `<param name="ip">` 应为 `ipEndPint`；多余 `<param name="timeOut">`；摘要写「iocp 客户端 socket」而实现走 SAEA 收发 | 改名 + 删除 + 更正为「saea 客户端 socket」 |
| 13 | `SAEA.Sockets/Shortcut/UDPServer.cs` | 存在签名中不存在的 `<param name="ip">` | 删除该标签 |
| 14 | `SAEA.Sockets/SocketOptionBuilder.cs` | 3 处摘要写「启用iocp」，实现是包模式开关；存在多余的 `<param name="context">` | 改为「启用包模式」+ 删除标签 |

修复后复跑审计：`stale-param` = 0、`doc-diagnostic` = 0；偏差总数由 5,791 降至 5,755。

验证（批次 1 完成时执行）：`dotnet build Src/SAEA.Sockets.sln -c Debug` 通过（0 errors）；`SAEA.Common.Tests` 59、`SAEA.MQTT.Tests` 153、`SAEA.FTP.Tests` 13 全部通过。

---

## 五、剩余缺口分布

### 5.1 按项目（库项目）

| 项目 | 公开成员 | 已注释 | 覆盖率 | 偏差 | 缺口性质 |
| --- | ---: | ---: | ---: | ---: | --- |
| SAEA.MQTT | 1,969 | 109 | 6% | 1,908 | 大面积零注释 |
| SAEA.P2P | 461 | 5 | 1% | 470 | 几乎整包零注释 |
| SAEA.Common | 761 | 392 | 52% | 413 | 老文件零注释 + 32 条英文摘要 |
| SAEA.RedisSocket | 661 | 299 | 45% | 401 | 枚举成员集中（`RequestType` 113 处） |
| SAEA.DNS | 382 | 85 | 22% | 316 | 模型类零注释 |
| SAEA.Sockets | 550 | 395 | 72% | 274 | 51 条英文摘要 + 66 条签名级 |
| SAEA.Sockets.Pipeline | 245 | 245 | 100% | 224 | 全是缺 param/returns |
| SAEA.FTP | 263 | 91 | 35% | 201 | 模型与协议码零注释 |
| SAEA.Http | 308 | 203 | 66% | 133 | 21 条英文摘要 |
| SAEA.Socket5 | 103 | 103 | 100% | 86 | 全是缺 param/returns |
| SAEA.QueueSocket | 113 | 63 | 56% | 56 | 零注释为主 |
| SAEA.Audio.Net | 98 | 63 | 64% | 53 | 历史代码 |
| SAEA.MVC | 147 | 103 | 70% | 53 | 零注释为主 |
| SAEA.MessageSocket | 53 | 2 | 4% | 52 | 整包零注释 |
| SAEA.RPC | 80 | 36 | 45% | 47 | 零注释为主 |
| SAEA.WebSocket | 75 | 46 | 61% | 32 | 零注释为主 |
| SAEA.FileSocket | 31 | 9 | 29% | 25 | 整包零注释 |
| **小计** | **6,300** | **2,249** | **35.7%** | **4,744** | |

测试与演示工程另有 1,011 处，其中 `SAEA.MVCTest`（224）、`SAEA.MQTT.Tests`（175）、`SAEA.Sockets.Pipeline.Tests`（168）、`SAEA.RPCTest`（90）最多。按协作约定，`Src/*Test/` 存量文件不强制回填，建议本轮不动。

### 5.2 按成员类型（库项目）

| 成员类型 | 缺整段注释 | 缺 param | 缺 returns | 英文摘要 |
| --- | ---: | ---: | ---: | ---: |
| 方法 | 1,332 | 286 | 152 | 92 |
| 属性 | 1,175 | — | — | 26 |
| 枚举成员 | 520 | — | — | 3 |
| 类型（class/interface/enum/struct） | 431 | — | — | 41 |
| 构造函数 | 257 | 36 | — | 36 |
| 字段 | 222 | — | — | 5 |
| 事件 | 96 | — | — | 1 |
| 委托 | 15 | 9 | — | — |
| 索引器 | 3 | — | — | 1 |
| **合计** | **4,051** | **331** | **152** | **205** |

另有 `<summary>` 为空 4 处（方法 2、类型 2）、参数说明为英文 1 处（构造函数）；四项相加 4,051 + 331 + 152 + 205 + 4 + 1 = 4,744，与 5.1 小计一致。

### 5.3 零注释文件

库项目中「有公开成员但一条 `<summary>` 都没有」的文件 **363 个**。偏差最多的 20 个：

| 文件 | 公开成员 |
| --- | ---: |
| `SAEA.P2P/Builder/P2POptions.cs` | 64 |
| `SAEA.FTP/Model/ServerResponseCode.cs` | 42 |
| `SAEA.P2P/Builder/P2PServerOptions.cs` | 34 |
| `SAEA.MQTT/Formatter/V5/MqttV500PropertiesReader.cs` | 32 |
| `SAEA.MQTT/Client/Options/MqttClientOptionsBuilder.cs` | 32 |
| `SAEA.MQTT/Formatter/V5/MqttV500PropertiesWriter.cs` | 31 |
| `SAEA.P2P/Common/ErrorCode.cs` | 31 |
| `SAEA.MQTT/Client/Disconnecting/MqttClientDisconnectReason.cs` | 31 |
| `SAEA.MQTT/Protocol/MqttDisconnectReasonCode.cs` | 30 |
| `SAEA.MQTT/Client/MqttClientExtensions.cs` | 29 |
| `SAEA.MQTT/Protocol/MqttPropertyID.cs` | 28 |
| `SAEA.P2P/Builder/P2PClientBuilder.cs` | 28 |
| `SAEA.Common/Caching/Batcher.cs` | 26 |
| `SAEA.MQTT/Protocol/MqttConnectReasonCode.cs` | 23 |
| `SAEA.MQTT/Client/Connecting/MqttClientConnectResultCode.cs` | 23 |
| `SAEA.MQTT/Server/MqttServerExtensions.cs` | 23 |
| `SAEA.MQTT/Extensions/ManagedClient/ManagedMqttClientExtensions.cs` | 23 |
| `SAEA.P2P/Core/P2PClient.cs` | 23 |
| `SAEA.MessageSocket/MessageClient.cs` | 22 |
| `SAEA.P2P/Core/PeerSession.cs` | 22 |

---

## 六、附带发现：文件头 banner

以仓库约定的 banner 模板逐字段核对（`CLR版本`、`机器名称`、`公司名称`、`命名空间`、`文件名`、`版本号`、`唯一标识`、`当前的用户域`、`创建人`、`电子邮箱`、`创建时间`、`描述`、`修改标记`）：

| 状态 | 修复前 | 最终 |
| --- | ---: | ---: |
| 完全合规 | 746 | 804 |
| 有 banner 但字段/结构不全 | 33 | 0 |
| 完全没有 banner | 25 | 1 |
| 旧版头（`*项目名称：` + `*类 名 称：`） | 1 | 0 |

唯一保留的例外是 `SAEA.Audio.Net/Properties/AssemblyInfo.cs`：程序集特性文件没有命名空间，强行套模板只能写空值，按「不为凑格式造假字段」处理，保持原样。

修复过程中补写或重建了 46 个文件头：`SAEA.Sockets` 21、`SAEA.Socket5` 11、`SAEA.Http` 5、`SAEA.MVC` 5、`SAEA.FTP` 2、`SAEA.QueueSocket` 1、`SAEA.Audio.Net` 1（`Core/INAudioDemoPlugin.cs` 旧版头）。缺失字段的取值：`机器名称`/`当前的用户域` 用 `WALLE`，`创建时间` 取该文件首个提交时间，`唯一标识` 每个文件新生成 GUID，`版本号` 取所属 `.csproj` 的 `<Version>`（`SAEA.Audio.Net` 无 `<Version>`，按约定写目标框架 `net48`）。

重复 `唯一标识` 仍有 4 组共 12 个文件，其中占位 GUID `a1b2c3d4-e5f6-7890-abcd-ef1234567890` 一组 3 个文件**都在库项目内**：

- `SAEA.Common/ObjectAsyncLock.cs`
- `SAEA.Common/ObjectLock.cs`
- `SAEA.DNS/Common/Utils/ObjectStringifier.cs`

其余 3 组都在测试工程。

---

## 七、后续批次建议

按「改动风险从低到高、收益从高到低」排序：

| 批次 | 内容 | 条目 | 涉及文件 | 特点 |
| --- | --- | ---: | ---: | --- |
| 2 | 补 `<param>` / `<returns>` | 483 | 96 | 改写模板固定，但必须先读签名与实现，机械度最低风险也最低 |
| 3a | MQTT 整段补注释 | 1,860 | 261 | 工作量最大；MQTT 5 协议码可对照规范写准语义 |
| 3b | P2P + MessageSocket + FileSocket | 529 | 43 | 零注释为主，包规模小，适合先做样板 |
| 3c | Common + RedisSocket + DNS | 1,028 | 106 | 枚举成员多，需按业务语义逐条写 |
| 3d | Sockets + Pipeline + Socket5 + FTP + Http + MVC + RPC + WebSocket + QueueSocket + Audio.Net | 634 | 122 | 主流包，要求措辞与现有注释风格一致 |
| 4 | 英文摘要转中文、空摘要、多余标签 | 210 | 82 | 可与批次 2 合并 |
| 5 | banner 补齐与 GUID 换新 | 59 | 59 | 与注释无关，可独立提交 |

批次 2 按项目拆分：`SAEA.Sockets.Pipeline` 224 条 / 43 文件，`SAEA.Socket5` 83 条 / 4 文件，`SAEA.Sockets` 66 条 / 18 文件，其余 12 个项目共 110 条 / 31 文件。

### 7.1 批次 2 执行记录（Pipeline + Socket5，已完成）

| 项目 | 提交 | 文件 | 新增注释行 | 修复条目 | 复查剩余 |
| --- | --- | ---: | ---: | ---: | --- |
| `SAEA.Sockets.Pipeline` | `0f806e08` | 43 | 224 | 224（缺 param 150 + 缺 returns 74） | 0 |
| `SAEA.Socket5` | `8128e4b0` | 4 | 83 | 83（缺 param 66 + 缺 returns 17） | 3（枚举成员英文摘要，P2） |

- 改动全部为新增 `/// <param>` / `/// <returns>` 行，未改任何签名、实现或既有摘要措辞；未改 banner 修改标记（与批次 1 处理方式一致）。
- 构建：`SAEA.Sockets.Pipeline`、`SAEA.Socket5` 各 `dotnet build -c Release` 均 0 警告 0 错误。
- 测试：`dotnet test Src/SAEA.Sockets.Pipeline.Tests/SAEA.Sockets.Pipeline.Tests.csproj -c Release` → 157 通过、0 失败。
- 全仓偏差由 5,755 降至 5,437。

### 7.2 批次 3 执行记录（库项目，已完成）

| 项目 | 提交 | 文件数 | 说明 |
| --- | --- | ---: | --- |
| `SAEA.MQTT` | `493f4e03` | 262 | 偏差 2,378 处，另补 3 个无 banner 文件 |
| `SAEA.P2P` | `4d096061` | 32 | |
| `SAEA.MessageSocket` | `0fdd1ff7` | 7 | |
| `SAEA.FileSocket` | `018fb28b` | 4 | |
| `SAEA.Common` | `1354343f` | 49 | 补 8 个无 banner 文件 |
| `SAEA.RedisSocket` | `9d9bd5b7` | 35 | |
| `SAEA.DNS` | `8152f648` | 38 | `Common/Utils/ByteStream.cs` 补空「描述」 |
| `SAEA.Sockets` | `2cc5e86e` | 48 | |
| `SAEA.FTP` | `c7ef2f97` | 18 | |
| `SAEA.Http` | `e219909d` | 26 | |
| `SAEA.WebSocket` | `0d7e4c80` | 5 | |
| `SAEA.QueueSocket` | `1b1b0af4` | 10 | |
| `SAEA.MVC` | `e7c7d074` | 18 | |
| `SAEA.RPC` | `5d16a512` | 10 | |
| `SAEA.Audio.Net` | `52629f54` | 10 | 不含 `Net/TransferClient.cs`、`Net/TransferServer.cs` |
| `SAEA.Socket5` | `bddd2bd9` | 1 | 批次 2 遗留的 3 处英文枚举摘要 |

偏差轨迹：5,437 → 2,957（MQTT + P2P/MessageSocket/FileSocket）→ 1,853（Common/RedisSocket/DNS）→ 1,007（其余库项目）。

### 7.3 批次 3 执行记录（测试与演示工程，已完成）

| 项目 | 提交 | 文件数 |
| --- | --- | ---: |
| `SAEA.MVCTest` | `e01d73d3` | 16 |
| `SAEA.MQTT.Tests` | `a32912bb` | 26 |
| `SAEA.Sockets.Pipeline.Tests` | `4d93a192` | 18 |
| `SAEA.RPCTest` | `477d66c2` | 12 |
| `SAEA.MQTTTest` | `371fd682` | 9 |
| `SAEA.Common.Tests` | `dd8ebc4a` | 9 |
| `SAEA.P2PTest` | `653bc40c` | 28 |
| `SAEA.Mvc.ServiceTest` | `5dcfff3a` | 9 |
| `SAEA.FTPTest` | `73eee908` | 11 |
| `SAEA.QueueSocketTest` | `6ea0ffaa` | 4 |
| `SAEA.Sockets.TcpTest` | `dde445f7` | 6 |
| `SAEA.Sockets.PipelineTest` | `f7d4b5e9` | 3 |
| `SAEA.FTP.Tests` | `a0f7004b` | 1 |
| `SAEA.RedisSocketTest` | `5c88e2cf` | 1 |
| `SAEA.Audio.Test` | `081c975c` | 1 |
| `SAEA.Socket5Test` | `37c0e008` | 1 |

偏差轨迹：1,007 → 65（本轮测试工程）→ 6。测试工程的存量 banner 按仓库约定不回填。

### 7.4 批次 5 执行记录（banner 规范化，已完成）

| 项目 | 提交 | 文件数 |
| --- | --- | ---: |
| `SAEA.Sockets` | `41a3f658` | 21 |
| `SAEA.Http` | `c323a322` | 5 |
| `SAEA.MVC` | `e006fe89` | 5 |
| `SAEA.FTP` | `6f253101` | 2 |
| `SAEA.Socket5` | `d3360395` | 11 |
| `SAEA.QueueSocket` | `a3352144` | 1 |
| `SAEA.Audio.Net` | `260c0d38` | 1 |

库项目 banner 复核：804 合规 / 0 不合规 / 1 例外（`AssemblyInfo.cs`）。新增文件头的取值规则见第六节。

### 7.5 剩余 6 处偏差（用户并发改动文件）

| 文件 | 缺失项 |
| --- | --- |
| `SAEA.Audio.Net/Net/TransferClient.cs` | `OnReceive` 事件缺 `<summary>`；`Ping`、`Disconnect` 为英文摘要 |
| `SAEA.Audio.Net/Net/TransferServer.cs` | `OnReceive` 事件、`Start`、`Stop` 缺 `<summary>` |

这两个文件在本任务期间由用户并发修改，未纳入任何提交；待其提交后按同一体例补 6 条即可归零。

若按「一个包一个提交」，全量补完约 5,755 行新增注释；另有每个被改文件的 4 行 banner 修改标记，最多约 700 个文件。

各批次条目之和：483（签名级）+ 1,860 + 529 + 1,028 + 634（整段）+ 210（摘要语言）= 4,744，与第五节库项目偏差总数一致；批次 5 的 59 个 banner 文件与注释条目互不重叠。

---

## 八、风险与边界

1. **补注释不等于补格式**。批次 3 每条都要对照实现判断语义，尤其 `SAEA.Sockets` 的 Span/Memory 生命周期、池化 buffer 归还、`iocp`/`saea` 命名这类批次 1 出现过的坑，写错会引入新的「注释与实现不符」。
2. **枚举成员占 520 处**，其中 MQTT 原因码、FTP 响应码、Redis 命令类型必须按协议语义写，不能靠成员名直译。
3. **banner 修改标记**：按约定，被修改文件的 `修改标记` 段要整体覆盖为本次信息，因此每个文件至少多 4 行 diff，会放大提交体积；`唯一标识` 必须新生成，不能照抄。
4. **审计是语法级**：`<see cref="...">` 指向是否存在、`<paramref>` 是否拼错都不在检查范围内，需要编译器 `CS1574` 辅助。
5. **测试与演示工程**已确认纳入范围（1,011 处）：按项目放在库项目之后处理，单独成批、单独提交，避免与库项目提交混在一起；`Src/*Test/` 的存量 banner 仍按仓库约定不回填。
6. 本报告数据基于修复后的 HEAD 重新审计，`SAEA.Audio.Net/Net/TransferClient.cs`、`TransferServer.cs` 存在与本次无关的未提交改动，未纳入统计口径变更。

---

## 九、复现方式

审计工具位于 `C:\Users\yswen\AppData\Local\Temp\opencode\docaudit\`（未纳入版本库）：

```powershell
dotnet run --project C:\Users\yswen\AppData\Local\Temp\opencode\docaudit\docaudit.csproj -c Release -- "D:\WorkBench\Walle\SAEA\Src" <输出目录>
```

输出：

- `violations.csv`：逐条偏差（severity / category / project / path / line / kind / member / detail）
- `stats.csv`：按项目的成员数与 P0/P1/P2 汇总
- `filestats.csv`：每文件公开成员数与缺注释数

banner 核对脚本：`C:\Users\yswen\AppData\Local\Temp\opencode\docaudit\banner-audit.ps1`。

---

## 十、决策与结果

已确认并执行（2026-11-06）：

1. 批次 2 先做 `SAEA.Sockets.Pipeline` + `SAEA.Socket5`，按「一个项目一个提交」推进 —— 已完成（见 7.1）。
2. 批次 3 先定 3 个样板文件（`SAEA.P2P/Common/ErrorCode.cs`、`SAEA.MQTT/Protocol/MqttConnectReasonCode.cs`、`SAEA.Common/Caching/Batcher.cs`），覆盖「枚举成员 / 协议码 / 类与属性」三类写法，确认措辞后按 `MQTT → P2P/MessageSocket/FileSocket → Common/RedisSocket/DNS → 其余库项目 → 测试演示工程` 的顺序逐项目推进 —— 已完成（见 7.2、7.3）。
3. 注释样式统一为多行 `<summary>`，与 `Src/SAEA.Common/IPHelper.cs` 体例一致。
4. banner 修补不单独立项，被修改文件的 `修改标记` 段整体覆盖为本次信息；缺失文件头在批次 5 统一补齐 —— 已完成（见 7.4）。
5. 测试与演示工程的 1,011 处**纳入范围**，按项目单独成批、单独提交，存量 banner 不回填 —— 已完成（见 7.3）。

剩余事项：

1. `SAEA.Audio.Net/Net/TransferClient.cs`、`TransferServer.cs` 的 6 条偏差，待用户并发改动提交后补齐（见 7.5）。
2. 4 组重复 `唯一标识`（12 个文件）未改：3 组在测试工程、1 组属库项目既有 banner；如需统一换新 GUID，建议单独立项，避免与本次注释改动混在一起。
3. `SAEA.Common/Newtonsoft.Json/`、`SAEA.Audio.Net/Base/NAudio/`、`Base/NSpeex/` 为内嵌第三方源码，全程未改。

### 10.1 子代理报告的实现问题（只报告，未改）

补注释过程中读代码发现的疑似缺陷，均未修改，按项目整理后另行评估：

- `SAEA.MQTT`：`WithDefaultEndpointBoundIPV6Address` 用 `IPAddress.Any`；`Batcher<T>.InsertAsync` 的取消令牌不生效；`MqttPacketReader.ReadFixedHeaderAsync` 未累计剩余长度字段字节；`BlockingQueue.RemoveFirst()` 空队列抛 `NullReferenceException`。
- `SAEA.P2P`：`EncryptionOptions`/`ServerTlsOptions` 多项配置未接线；`HolePuncher.PunchAsync` 成功分支不可达；`OnPeerConnected`/`OnPeerDisconnected`/`OnRelayEnded` 从不触发但 README 有示例。
- `SAEA.Http`：`HttpRequest.UserHostAddress` 返回连接标识而非客户端 IP；`Dispose()` 未 `Init` 时抛空引用；`HttpUtility.MapPath` 静态缓存跨 root 串用；默认 `contentType` 拼成 `text/plane`。
- `SAEA.Common`：`MemoryCache.Active` 值类型 `default` 可能使过期键复活；`CryptoService.Encrypt(byte[])` 走 UTF-8 字符串转换破坏二进制。
- `SAEA.RedisSocket`：`RedisConfig` 连接串 `actionTimeout=` 分支会抛 `IndexOutOfRangeException`；`ToString()` 缺 `;`。
- `SAEA.RPC`：`RSocketMsg.Dispose` 与缓冲池归还相关问题。
- `SAEA.Audio.Net`：`VStorage.TryRemoveChannelUserMapping` 移除最后一个用户后空集合不清理。
- 测试演示工程：`MVCTest` 中硬编码密码、登录不校验口令、JWT 解析失败覆盖真实原因；`FTPTest` P/Invoke 图标句柄未释放；`Mvc.ServiceTest` 静态缓存与文件数据不同步。