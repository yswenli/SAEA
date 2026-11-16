# 阶段 9 T9.3：STUN/TURN 互通核对清单

**结论**：本清单用于把本仓库的 STUN/TURN 实现与标准 STUN/TURN 实现（公共 STUN 服务器、coturn、第三方客户端）逐条对齐核对；本轮无公网环境，清单内**全部互通条目均未运行**，只交付命令模板、预期行为与判定口径。

## 1. 口径与环境

### 1.1 本轮口径

- **D9**：本轮没有公网环境（无公网出口、无公网可达主机），凡是要访问公共 STUN 服务器、独立 coturn 实例或第三方客户端的条目都不具备执行条件。
- **D14**：本轮只交付核对清单，不产出互通结论；下面每条命令在具备公网环境后按原样执行，再把结果回填到「实际结果」列。
- 因此全文「实际结果」列只有两种取值：`未运行` + 原因，或本机确实跑过的仓内用例（见第 5 节），后者只表示编码/鉴权符合向量，不表示与外部实现完成互通。

### 1.2 本机实测环境

| 项 | 实测值 |
| --- | --- |
| 操作系统 | Microsoft Windows NT 10.0.26300.0 |
| 主机名 | WALLE |
| `dotnet --version` | 11.0.100 |
| 可用运行时 | Microsoft.NETCore.App 8.0.14 / 10.0.15（另有 3.1.32、6.0.36、11.0.0） |
| 单测工程目标框架 | `net10.0`（`Src/SAEA.P2P.Tests`） |
| 服务端宿主目标框架 | `net8.0`（`Src/SAEA.P2PTest`），故用 `C:\Program Files\dotnet\dotnet.exe` 启动；`where.exe dotnet` 指向同一路径 |
| coturn / turnutils | 本机未安装，故本文所有 `turnutils_*` 参数名均需先用 `-h` 核对 |

### 1.3 被测服务端启动方式

```bash
dotnet run --project Src/SAEA.P2PTest -c Release -- --server --config <配置路径>
```

`--server` 之外的命令行覆盖开关（`--listen`、`--stun-port`、`--turn-port`、`--tls-port`、`--realm`、`--shared-secret`、`--tls-cert`、`--change-address` 等）见 `Src/SAEA.P2PTest/Program.cs` 的 `PrintUsage`；口令类参数只在命令行或配置文件里给，日志与异常不回显。

### 1.4 配置示例（只有键名，值一律占位）

```json
{
  "ListenAddress": "<listen-ip>",
  "StunPort": 3478,
  "TurnPort": 3478,
  "TurnTlsPort": "<tls-port>",
  "Realm": "<realm>",
  "SharedSecret": "<shared-secret>",
  "EnableIpv6": false,
  "EnableDualMode": false,
  "TlsCertificatePath": "<pfx-path>",
  "TlsCertificatePassword": "<pfx-password>"
}
```

| 键名 | 类型 | 源码默认值 | 说明 |
| --- | --- | --- | --- |
| `ListenAddress` | string | `0.0.0.0` | 监听地址；`EnableIpv6` 为 true 且仍为默认值时改监听 `IPv6Any` |
| `StunPort` | int | `3478` | STUN 独立监听端口；`0` 不单独监听；与 `TurnPort` 相同时由 TURN 的 UDP 监听应答 Binding |
| `TurnPort` | int | `3478` | TURN 的 UDP 与 TCP 同号监听；两者协议不同不冲突 |
| `TurnTlsPort` | int | `0` | TURN over TLS 端口；`0` 关闭，启用时必须给 `TlsCertificatePath` |
| `Realm` | string | 空（必填） | 认证域，必须与签发临时凭据的一侧一致 |
| `SharedSecret` | string | 空（必填） | 共享密钥，必须与签发临时凭据的一侧一致 |
| `EnableIpv6` | bool | `false` | 是否改用 IPv6 监听 |
| `EnableDualMode` | bool | `false` | 当前监听层没有 DualMode 开关，只记录提示并按地址族监听 |
| `TlsCertificatePath` | string | `null` | PFX 路径；启用 TLS 时必填且文件必须存在 |
| `TlsCertificatePassword` | string | `null` | PFX 口令，无口令时留空 |

另有两个对本清单有影响的键：`RelayPortRange`（`Min`/`Max`，默认 `49152`/`65535`）与 `ChangeRequestAddressPair`（RFC 5780 第二地址，默认 `null`）。

## 2. A. 公共 STUN 互通

| 编号 | 目的 | 命令 | 预期 | 实际结果 | 备注 |
| --- | --- | --- | --- | --- | --- |
| A1 | DNS 与可达性 | `nslookup stun.l.google.com` | 解析出 A/AAAA 记录 | **未运行**：无公网环境（D9），本轮只交付核对清单（D14） | 若 DNS 被拦截，改用 `stun1.l.google.com` 或自建 stub |
| A2 | 标准 Binding（0x0001/0x0101） | 见 2.1 两条命令 | 成功应答 `0x0101` 且带 `XOR-MAPPED-ADDRESS` | **未运行**：无公网环境（D9），本轮只交付核对清单（D14） | 本地基线命令不等价于互通 |
| A3 | RFC 5780 行为发现（可选） | 见 2.1 的 CHANGE-REQUEST 形式 | 公共 STUN 通常不给第二地址，`CHANGE-REQUEST` 可能被忽略或回 420 | **未运行**：无公网环境（D9），本轮只交付核对清单（D14） | 多数公共 STUN 只部署单地址，判定口径要允许「不支持」 |

### 2.1 A2/A3 命令模板

本地基线（只证明本仓库客户端能对本仓 STUN 端口发 Binding，不是互通）：

```bash
dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "FullyQualifiedName~StunClient"
```

互通需要自备调用脚本（本仓库没有对公网 STUN 开箱即用的命令行入口）。脚本形态：构造 `SaeaUdpTransport` 绑定本机临时端口 → 用该传输创建 `StunClient` → 调 `BindingAsync`（`StunClient.BindingAsync(IPEndPoint server)`，见 `Src/SAEA.P2P/Stun/StunClient.cs`）→ 打印 `StunBindingResult` 的 XOR-MAPPED-ADDRESS 与耗时。占位命令：

```bash
# 需自备调用脚本；参数占位
dotnet run --project <本机脚本工程> -- --stun-host <stun-host> --stun-port 3478
```

预期判定：应答类型 `0x0101`（Binding Success Response），属性里必须有 `XOR-MAPPED-ADDRESS`；缺映射地址时 `StunClient` 会按 `ProtocolError` 返回失败（`ParseSuccess`）。

RFC 5780 行为发现（可选）：同一脚本在请求里追加 `CHANGE-REQUEST`（`0x0003`，`0x02`=换端口、`0x04`=换地址），观察是否回 420 或从另一地址响应；公共 STUN 大概率不支持，故本项只作可选参考。

## 3. B. coturn 互通

| 编号 | 目的 | 命令 | 预期 | 实际结果 | 备注 |
| --- | --- | --- | --- | --- | --- |
| B1 | TURN over UDP Allocate | 见 3.1 | `0x0103` 成功应答并带 `XOR-RELAYED-ADDRESS` | **未运行**：无公网环境（D9），本轮只交付核对清单（D14） | coturn 侧需开 `use-auth-secret` 或静态用户 |
| B2 | TURN over TCP | 见 3.1（TCP 开关） | 同上，控制连接走 TCP | **未运行**：无公网环境（D9），本轮只交付核对清单（D14） | 参数名需按本机工具版本用 `-h` 核对；分帧口径见 6.3 |
| B3 | TURN over TLS | 见 3.1（TLS 开关） | 同上，控制连接走 TLS | **未运行**：无公网环境（D9），本轮只交付核对清单（D14） | 需要证书与 443/5349 端口；参数名需按本机工具版本用 `-h` 核对 |
| B4 | 标准 long-term credential | 见 3.2 | 401 挑战 → 带 USERNAME/REALM/NONCE/MESSAGE-INTEGRITY 重发 → 成功 | **未运行**：无公网环境（D9），本轮只交付核对清单（D14） | 直接配 `Username`/`Password`，不是 REST 临时凭据 |
| B5 | REST 临时凭据互认 | 见 3.3 | 两侧 `SharedSecret`/`Realm` 一致时 Accept | **未运行**：无公网环境（D9），本轮只交付核对清单（D14） | 派生算法见 3.3，注意是 HMAC-SHA1 |
| B6 | RFC 6062 TCP 中继 | 见 3.4 | `Connect` 回 `CONNECTION-ID`；另开数据连接 `ConnectionBind` 成功 | **未运行**：无公网环境（D9），本轮只交付核对清单（D14） | coturn 需开启 TCP 中继相关配置 |

### 3.1 B1/B2/B3：`turnutils_uclient` 模板

```bash
# B1 UDP（参数名需按本机工具版本用 -h 核对）
turnutils_uclient -u <user> -w <password> -p <turn-port> <turn-host>

# B2 TCP（同上的 TCP 传输开关，按 -h 核对具体开关名）
turnutils_uclient -t -u <user> -w <password> -p <turn-port> <turn-host>

# B3 TLS（安全连接开关按 -h 核对；端口填 443 或 5349）
turnutils_uclient -S -u <user> -w <password> -p 5349 <turn-host>
```

判定：客户端侧确认拿到 relay 地址；服务端侧确认分配已建立（`TurnServer.AllocationCount` 增长），且应答带 `XOR-RELAYED-ADDRESS`（`HandleAllocate` 里由 `StunAddressCodec.EncodeAddress(StunAttributeType.XorRelayedAddress, ...)` 生成）。TLS 行额外看 `TurnTlsListener` 是否完成握手。

### 3.2 B4：long-term credential 路径

本仓库客户端侧直接给口令，不走临时凭据：

- `TurnClientOptions.Username`：直接配置的用户名，用于与 coturn 等入口互通。
- `TurnClientOptions.Password`：直接配置的口令，与 `Username` 成对使用。
- 与 `TurnClientOptions.Credential`（信令下发的临时凭据）二选一。

互通命令占位（需自备调用脚本，构造 `TurnClientOptions` 后交给 `TurnClient`）：

```bash
dotnet run --project <本机脚本工程> -- --turn-host <turn-host> --turn-port 3478 --transport udp --username <user> --password <password> --realm <realm>
```

判定：首包回 401 + `REALM`/`NONCE`，随后带 `USERNAME`/`REALM`/`NONCE`/`MESSAGE-INTEGRITY` 的重发拿到 `0x0103`；`TurnAuthContext` 侧校验长期密钥。

### 3.3 B5：REST 临时凭据互认（coturn `use-auth-secret`）

口径（以源码为准，见 `Src/SAEA.P2P/Turn/TurnCredentialService.cs`）：

- `username = <过期 Unix 秒数>:<任意标识>`（`DefaultSeparator` 为 `":"`，用户标识不能含分隔符）。
- `password = base64(HMAC-SHA1(<shared-secret>, <username>))`。
- 两侧 `SharedSecret` 与 `Realm` 必须一致；过期时间按 UTC 秒比较。

互通命令占位：

```bash
# 先用同一密钥算用户名与口令，再交给客户端
turnutils_uclient -u "<expiry-unix-seconds>:<id>" -w "<base64-hmac-sha1>" -p <turn-port> <turn-host>
```

判定：同密钥同域下 Accept；改一位密钥或跨域必须回 401。

### 3.4 B6：RFC 6062 TCP 中继

前提：coturn 侧开启 TCP 中继相关配置；本仓库侧用 `TurnClientOptions.RequestedTransportProtocol = 6` 申请 TCP 分配，`DataTransportFactory` 提供数据连接（为 null 时用内置明文 TCP 实现）。

```bash
# Connect（控制连接发 Allocate(REQUESTED-TRANSPORT=6) + Connect，参数名按 -h 核对）
turnutils_uclient -t <TCP 中继开关，按 -h 核对> -e <peer-host> -g <peer-port> -u <user> -w <password> -p <turn-port> <turn-host>
```

判定：`Connect` 成功应答带 `CONNECTION-ID`；随后另开一条 TCP/TLS 连接发 `ConnectionBind`，服务端接管为数据连接。本仓库侧不支持在 UDP 控制连接上做 TCP 中继（`HandleAllocate` 回 442），因此控制连接必须是 TCP 或 TLS。

## 4. C. 反向互通：本仓库服务端被第三方客户端访问

| 编号 | 目的 | 命令 | 预期 | 实际结果 | 备注 |
| --- | --- | --- | --- | --- | --- |
| C1-UDP | `turnutils_uclient` 打本仓库 TURN UDP | 见 4.1 | 拿到 `XOR-RELAYED-ADDRESS` | **未运行**：无公网环境（D9），本轮只交付核对清单（D14） | 需先按 1.3 启动 `--server` |
| C1-TCP | `turnutils_uclient` 打本仓库 TURN TCP | 见 4.1 | 同上（控制连接 TCP） | **未运行**：无公网环境（D9），本轮只交付核对清单（D14） | 参数名需按本机工具版本用 `-h` 核对 |
| C1-TLS | `turnutils_uclient` 打本仓库 TURN TLS | 见 4.1 | 同上（控制连接 TLS） | **未运行**：无公网环境（D9），本轮只交付核对清单（D14） | 需 `TurnTlsPort` + 证书 |
| C2 | 公共 STUN 探测工具打本仓库 STUN 端口 | 见 4.2 | `0x0101` + `XOR-MAPPED-ADDRESS` | **未运行**：无公网环境（D9），本轮只交付核对清单（D14） | 公网探测需要本机端口可被外网访问 |

### 4.1 C1 命令模板

```bash
# 先起服务端（另开一个终端）
dotnet run --project Src/SAEA.P2PTest -c Release -- --server --config <配置路径>

# C1-UDP：参数名需按本机工具版本用 -h 核对
turnutils_uclient -u <user> -w <password> -p <turn-port> <本机可达地址>

# C1-TCP：加 TCP 传输开关，按 -h 核对
turnutils_uclient -t -u <user> -w <password> -p <turn-port> <本机可达地址>

# C1-TLS：加安全连接开关，按 -h 核对；端口用配置里的 TurnTlsPort
turnutils_uclient -S -u <user> -w <password> -p <turn-tls-port> <本机可达地址>
```

注意：本机自测时 `ListenAddress` 用具体网卡地址或 `127.0.0.1`，`ServerHost` 会用该地址推导中继地址；监听 `0.0.0.0` 时中继地址取本机同族非回环的第一个地址。

### 4.2 C2 公共 STUN 探测

```bash
# 任意公共 STUN 客户端工具，指向本仓库 STUN 端口（本机测试用回环或内网地址）
<公共 STUN 测试工具> --server <本机可达地址>:3478
```

判定：应答 `0x0101` 且 `XOR-MAPPED-ADDRESS` 等于探测端的公网映射；只配 `TurnPort=3478`（`StunPort` 也 3478）时 Binding 同样能得到应答，见第 5 节。

## 5. D. RFC 5769 向量与本机已运行项

以下两项在本机按给出的命令真实跑过，输出为原文尾部。

### 5.1 `Rfc5769Vector`

```bash
dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "FullyQualifiedName~Rfc5769Vector"
```

输出尾部（实测）：

```text
已通过! - 失败:     0，通过:     4，已跳过:     0，总计:     4，持续时间: 58 ms - SAEA.P2P.Tests.dll (net10.0)
```

退出码 0（同一用例集以 `--no-build` 再跑一次，命令尾部追加 `exit=$LASTEXITCODE` 实测为 `exit=0`，该次耗时 49 ms）。

结果属性：**已运行（单元向量，不等于互通验证）**。数量与最初预期（3 个）不一致，实际为 4 个。

### 5.2 `TurnAuth`

```bash
dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "FullyQualifiedName~TurnAuth"
```

输出尾部（实测）：

```text
已通过! - 失败:     0，通过:     6，已跳过:     0，总计:     6，持续时间: 54 ms - SAEA.P2P.Tests.dll (net10.0)
```

退出码 0（同样以 `--no-build` 复跑确认，耗时 47 ms）。

结果属性：**已运行（单元向量，不等于互通验证）**。该用例集覆盖：正确凭据、时间戳过期回 401、realm 不匹配回 401、同一 nonce 跨源地址被拒、用 Base64 密码当 key 校验失败、`TurnCredential.ToString` 不含密码。

### 5.3 这两项说明什么、不说明什么

- 说明：本仓库的 STUN 编解码、`MESSAGE-INTEGRITY`/`FINGERPRINT` 计算与 RFC 5769 官方样例逐字节一致；临时凭据的格式、过期与派生校验与自身向量一致。
- 不说明：不等于与 coturn 或公共 STUN 完成互通。coturn 的 nonce 轮换策略、通道号隔离、错误码细节、TLS 握手参数等都没有在本轮触及。
- 上述代码块里的「已通过」是 `dotnet test` 自身输出原文，仅指这两个用例集，不是本清单对互通给出的结论。

## 6. E. 端口与传输对照表

### 6.1 默认端口与对齐点

| 项 | 本仓库默认（源码） | coturn 侧常见默认 | 对齐点 / 差异 |
| --- | --- | --- | --- |
| STUN 独立监听 | `StunPort = 3478`；`0` 表示不单独监听 | `listening-port 3478` | 同端口 |
| TURN UDP | `TurnPort = 3478` | `listening-port 3478` | 同端口 |
| TURN TCP | `TurnPort = 3478`（与 UDP 同号） | `listening-port 3478` | 同号，不同协议不冲突 |
| TURN over TLS | `TurnTlsPort = 0`（默认关闭），启用需 `TlsCertificatePath` | `tls-listening-port 5349` | 本仓库不写死 5349，由配置给；443/5349 都只是约定 |
| 中继端口区间 | `RelayPortRange` `49152`–`65535` | `min-port 49152` / `max-port 65535` | 一致 |
| 默认分配生命周期 | `DefaultLifetimeSeconds = 600` | 600 | 同量级；请求更长时按 `MaxLifetimeSeconds = 3600` 截断 |
| 凭据有效期 | `CredentialTtlSeconds = 600` | 由 `use-auth-secret` 的过期戳决定 | 只看用户名里的 UTC 秒 |
| nonce 有效期 | `NonceLifetimeSeconds = 600` | 由 coturn nonce 策略决定 | 需实测，本仓库按 600s 失效 |
| 并发配额 | `MaxAllocations = 1024`、`MaxAllocationsPerUser = 8` | 由 coturn 配置决定 | 超限回 486 `AllocationQuotaReached` |

coturn 一列取其公开配置项名称与常见默认值；本机未安装 coturn，执行前请用 `turnserver -h` 与配置文件注释确认。

### 6.2 TURN UDP 端口同时应答 Binding

行为（只写结论，不贴代码）：

- `Src/SAEA.P2PTest/Server/ServerHost.cs` 的 `StartTurnUdpAsync` 用同一个 `StunServer` 监听 `TurnPort`，并把 `RequestHandler = Server.HandleRequest`、`DatagramHandler = Server.HandleDatagram` 挂上去。
- `Src/SAEA.P2P/StunServer/StunServer.cs` 的 `OnDatagramReceived` 先给 `DatagramHandler`（ChannelData）一次机会，再解析 STUN；`Method == Binding` 的请求由 `HandleBinding` 自己应答，只有非 Binding 请求才走 `DispatchOtherMethod`。
- 因此 `StunPort == TurnPort`（默认都 3478）时 `StartStunAsync` 不再单独绑定 STUN 端口（避免重复绑定），Binding 由 TURN 的 UDP 监听应答；`StunPort` 设成其他值时才有独立 STUN 监听。
- `Src/SAEA.P2P/TurnServer/TurnServer.cs` 侧只处理非 Binding：`HandleRequest`/`HandleStreamRequest` 分派 Allocate/Refresh/CreatePermission/ChannelBind/Connect/ConnectionBind/Send，`HandleDatagram` 只认首字节 `0x40`–`0x4F` 的 ChannelData。

对互通的直接含义：coturn 的 `turnutils_uclient` 与本仓库客户端都可能在 TURN 端口上先发 Binding 探测，这条路径由 `StunServer` 应答，与 TURN 分配是两条独立分支。

### 6.3 值得先核对的传输差异

- 流式控制连接按 STUN 自身 20 字节头 + length 自定界（`Src/SAEA.P2P/Turn/TurnStreamFramer.cs`，明确「不加长度前缀」）；而 `StunClient` 的 TCP 模式发送时加了 2 字节长度前缀（`Src/SAEA.P2P/Stun/StunClient.cs` 的 `Send`，按 RFC 4571）。两者面向的场景不同（ICE 的 STUN over TCP 与 TURN 控制连接），但用本仓库 `StunClient` 直连本仓库 TURN 的 TCP 端口时需要先确认分帧口径，否则会读不出报文。
- `EnableDualMode` 目前只记录提示：监听层（`SaeaUdpTransport`、`TurnTcpListener`）没有 DualMode 开关，双栈要按地址族分别监听。
- RFC 5780 换端口：`ServerHost.BuildAlternateEndPoint` 强制备用端点与主端点同端口，而 `StunServerOptions.CanChangePort` 要求备用端点端口不同，所以 `--server` 宿主下「换端口」类 `CHANGE-REQUEST` 会回 420；换地址只有在配置了 `ChangeRequestAddressPair` 且地址不同时才成立。

## 7. F. 未运行清单汇总

本轮未运行的条目：**A1、A2、A3、B1、B2、B3、B4、B5、B6、C1-UDP、C1-TCP、C1-TLS、C2**，共 13 条，原因统一为「无公网环境（D9），本轮只交付核对清单（D14）」；可交付物只有命令模板、预期行为与判定口径，没有互通结论。