# P1 验收记录（2026-11-10）

**计划：** `docs/superpowers/plans/2026-11-09-socket-pipeline-migration-plan.md`（P1-1 ～ P1-6）

**设计：** `docs/superpowers/specs/2026-11-06-socket-pipeline-migration-design.md` §八. D / §八. F / §4.1 / §十二 第 28、29 条

**范围：** `SAEA.Http` + `SAEA.MVC` + 3 个 demo（`SAEA.HttpTest`、`SAEA.MVCTest`、`SAEA.Mvc.ServiceTest`）+ 中英 README

---

## 一、构建与测试

| 命令 | 退出码 | 结果 |
|---|---|---|
| `dotnet build Src/SAEA.Sockets.sln -c Debug` | 0 | 0 errors / 1 warning |
| `dotnet test Src/SAEA.Http.Tests/SAEA.Http.Tests.csproj -c Debug` | 0 | **28 / 28**（本轮 22 → 28） |
| `dotnet test Src/SAEA.Sockets.Pipeline.Tests/SAEA.Sockets.Pipeline.Tests.csproj -c Release` | 0 | 260 / 260 |
| `dotnet test Src/SAEA.Common.Tests/SAEA.Common.Tests.csproj -c Release` | 0 | 59 / 59 |
| `dotnet test Src/SAEA.MQTT.Tests/SAEA.MQTT.Tests.csproj -c Release` | 0 | 153 / 153 |
| `dotnet test Src/SAEA.FTP.Tests/SAEA.FTP.Tests.csproj -c Release` | 0 | 13 / 13 |

本轮新增 6 条 Http 用例：

- 升级与协议错误：`HttpProtocolHandler_UpgradeAccepted_UpgradesRegisteredSession`、`HttpProtocolHandler_UpgradeWithFirstFrameInSameSegment_ForwardsRawFrame`（同 TCP 段「101 + WS 首帧」）、`HttpProtocolHandler_ChunkedRequest_Writes400AndCloses`
- 解码回填：`HttpRequestDecoder_JsonBody_FillsJson`、`HttpRequestDecoder_FormUrlEncodedBody_FillsForms`
- 分帧：`HttpRequestSplitter_MalformedHeader_Throws400`

此前已覆盖并本轮复跑通过的升级用例：`HttpProtocolHandler_UpgradeAccepted_SwitchesAfterWriteCompletes`（101 写成功后才切换）、`HttpProtocolHandler_UpgradeWriteFails_DoesNotSwitch`（写失败不切换）、`HttpProtocolHandler_UpgradeRejected_ResetsBarrier`（拒绝升级复位屏障）。

**唯一告警：** `Src/SAEA.MVCTest/Controllers/EventStreamController.cs:51` 的 `CS0168`（`catch (Exception ex)` 未使用 `ex`）。该文件本轮未改，属 demo 存量告警；此前增量构建未重编该工程所以不可见，不是本轮引入。

---

## 二、demo 冒烟（`SAEA.HttpTest`，`http://127.0.0.1:18080`）

| # | 场景 | 结果 |
|---|---|---|
| 1 | `GET /` 静态首页 | `200`，262 字节 |
| 2 | 同连接 `POST` 带 100KB body | `200` |
| 3 | `application/json` 请求体 | `200` |
| 4 | `Transfer-Encoding: chunked` | `400 BadRequest` 后收口 |
| 5 | 70KB 请求头 | `431 RequestHeaderFieldsTooLarge` |
| 6 | `Content-Length: 70000000` | `413 RequestEntityTooLarge`（只凭报头即判） |
| 7 | 请求行 `GARBAGE` | `400 BadRequest` |
| 8 | 空报头（首发即 `\r\n\r\n`） | `400 BadRequest` |
| 9 | 畸形请求之后再来正常 `GET /` | `200` |

第 7、8 项在本轮修复前是「无响应且不断开」：分帧器把「报头已结束但解析失败」当成数据不足返回 `false`，连接只能等对端关闭。已改为抛 `BadRequest` 走 413/431 同一条上链收口路径，并补单测与设计 §八. F 说明。

**MVC 冒烟（`SAEA.MVCTest`，`http://127.0.0.1:28080`）：** `/home/ping` → `200 PONG`；`ws://…/echo/open` 握手后 `Open`，发送后仍 `Open`；再次 `/home/ping` → `200`。

---

## 三、P1-6 条目对照

| 验收项 | 证据 |
|---|---|
| `Content-Length` 请求体 | `HttpRequestSplitter_ContentLengthBody_SplitsWholeRequest`、`HttpRequestDecoder_SplitAcrossReads_ProducesSingleMessage`、冒烟 #2 |
| 静态资源 | 冒烟 #1 / #9 |
| 101 写成功后才切换 | `HttpProtocolHandler_UpgradeAccepted_SwitchesAfterWriteCompletes`、`…UpgradesRegisteredSession` |
| 写失败不切换 | `HttpProtocolHandler_UpgradeWriteFails_DoesNotSwitch` |
| body 超限 413 | `HttpRequestSplitter_BodyOverMaxRequestSize_Throws413WithoutWaitingBody`、`HttpProtocolHandler_FrameProtocolError413_Writes413AndCloses`、冒烟 #6 |
| 头部超限 431 | `HttpRequestSplitter_HeaderOver64KB_Throws431`、`…431_Writes431AndCloses`、冒烟 #5 |
| chunked 显式 400 | `HttpRequestSplitter_Chunked_ThrowsChunkedToken`、`HttpProtocolHandler_ChunkedRequest_Writes400AndCloses`、冒烟 #4 |
| 报头完整但畸形 400 | `HttpRequestSplitter_MalformedHeader_Throws400`、冒烟 #7 / #8 |
| 同 TCP 段「101 + WS 首帧」 | `HttpProtocolHandler_UpgradeWithFirstFrameInSameSegment_ForwardsRawFrame` |
| 接收路径无整段 `ToArray()` | 逐处复核：仅 `HttpRawFrameSinkHandler`（原始字节 sink，语义要求 `byte[]`）与 `RequestDataReader` 的报头/请求体解析；无整帧拷贝 |
| 单测不依赖 demo | `Http.Tests` 只引用 `SAEA.Http`、`SAEA.Sockets.Pipeline` 两个工程，测试内用真实 `ChannelPipeline` + `ChannelPipelineExecutor`，未启动任何 demo |

---

## 四、与计划的偏差（均已回填计划与设计）

1. 装配入口用 `WebConfig.ConfigurePipeline`（`Action<string, IChannelPipeline>`），未新增 `WebHostOptions`（设计 §4.1 已回填）。
2. 413 / 431 / 400 由 `HttpProtocolHandler` 构造 `HttpResponse` 经 `HttpResponseEncoder` 写出，未使用 `RawFrame` 直发；`RawFrame` 只用于 `Send(byte[])` 主动推送。
3. MVC 应用级事件 `SAEAMvcApplication.OnException` / `OnRequestDelegate` 保留为公开 API，`Start()` 时转赋给 `WebConfig`，demo 源码零改动。
4. 畸形报文由「按数据不足继续等」改为「立即 400 收口」（设计 §八. F、计划 P1-6 已补条目）。

---

## 五、未运行项

| 项 | 原因 |
|---|---|
| `SAEA.Mvc.ServiceTest` 窗口冒烟 | WinExe 需人工点击；本轮只核对了 `ProjectReference` 与 `app.config` 构建产物（sln 0 错误） |
| `SAEA.HttpTest` 的 SSE / 大文件下载分支 | 需人工观察长连接行为，非 P1 验收条目 |
| 全量 clean rebuild 的告警数 | 未做 clean，只做增量；告警基线见 B0（约 3.8k CS1591） |

---

## 六、遗留风险

1. `HttpResponse` 的 101 只输出报头与空行；`Protocal` 为空时按 `HTTP/1.1` 兜底，自定义协议串需自行保证格式。
2. 升级后原始帧是池化内存，`Frame` 只在回调内有效；README 已明确要求跨回调复制。
3. `ConfigurePipeline` 追加的入站处理器看不到普通 HTTP 请求（已被派发节点消费），只用于连接级观测与升级后原始帧；README 中英已同步该边界。
4. MVC `ConnectionClosed` 走 `WebConfig` 委托，外部若仍依赖旧 `HttpContextBase` 事件，属已记录的破坏面。

---

## 七、下一步

P2：`SAEA.FileSocket`（`0 / 8 / +1` 长度域、`MaxConcurrentMessages = 1`、5 个事件删除）与 `SAEA.WebSocket`（11 个事件、continuation 分片与控制帧交织用例），详见计划 §四。