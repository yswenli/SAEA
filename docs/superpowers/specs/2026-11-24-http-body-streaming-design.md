# HTTP 请求体流式化与服务器端硬化设计

- 日期：2026-11-24
- 状态：已确认，分阶段实施
- 范围：`SAEA.Http`、`SAEA.MVC`（文档与示例）、`SAEA.Http.Tests`、`SAEA.MVC.Tests`；`SAEA.Sockets`、`SAEA.Sockets.Pipeline` 仅为 413 有界排空新增最小接口
- 不在范围：`SAEA.Sockets`、`SAEA.Sockets.Pipeline` 既有收发主路径的重构

## 1. 目标

1. 请求体与上传文件不再以 `byte[]` 暴露，统一改为只读 `Stream`。
2. 大请求体按阈值落盘，避免请求结束后整段驻留内存、避免重复复制。
3. multipart 解析改为单遍流式扫描，不依赖整段 `byte[]`。
4. 支持 `Transfer-Encoding: chunked`。
5. 支持 `Expect: 100-continue`。
6. 分帧与收发主路径保持现状；仅为 413 有界排空新增最小跨包接口（`IDrainableChannel`、`IDrainedCloseStream`、`IGracefulCloseFlow`）。

## 2. 公开契约变更（破坏式）

- `HttpBase` 移除 `Body` 属性，避免请求与响应共用同一成员。
- `HttpResponse.Body` 保持 `byte[]`，响应写入路径与现有 API 不变。
- `HttpRequest.Body` 改为 `Stream`（只读、可 `Seek`、`Position` 可读写）。
  - 无请求体时为 `null`。
  - 生命周期与请求一致：请求结束（`HttpRequest.Dispose` / 连接复用复位）后不可再读，读到已释放对象抛 `ObjectDisposedException`。
- `HttpMessage.Body` 改为 `Stream`，作为请求消息的内部载体，语义与 `HttpRequest.Body` 一致。
- `FilePart.Data` 改为 `Stream`，语义同上；`FilePart` 实现 `IDisposable`，释放时删除其临时文件。
- `FilePart.Save(string filePath)` 从 `Data` 复制：可 `Seek` 时先回到 0，复制完成后恢复调用前的位置，便于重复保存。
- `RequestDataReader.AnalysisBody(byte[], HttpMessage)` 保留，内部把传入字节包装成内存流，供既有测试与外部调用继续使用。

## 3. 存储与生命周期

- 新增 `WebConfig.LargeBodyThreshold`，默认 `1 * 1024 * 1024`；取值 `0` 表示禁用落盘（始终内存）。
- 新增 `WebConfig.TempDirectory`，默认 `null`，解析为 `Path.Combine(Path.GetTempPath(), "saea-http")`；启动期确保目录存在。
- 请求体不超过阈值：解码器对整帧 `PooledMessage` 执行 `Retain()`，请求体流直接读取 `frame.Frame` 的 body 切片，零复制；流释放时 `Release()` 归还整帧。
- 请求体超过阈值：解码时把 body 写入临时文件（命名含连接标识与随机串，禁止用客户端文件名），请求体流读该文件；请求释放或流释放时删除文件。
- 临时文件删除失败只记日志，不向业务抛异常。
- 已知峰值：分帧契约要求整帧可寻址，落盘只消除「整帧 → body 数组」的重复复制与请求结束后的驻留，不降低分帧期的单连接峰值；README 必须写明。

## 4. multipart 流式解析

- `RequestDataReader` 的 multipart 解析改为从 `Stream` 顺序读取，使用池化 64KB 缓冲与增量 boundary 匹配，整体保持 O(n)。
- 保留现有语义：容忍起始 boundary 之前的 preamble；`MaxParts = 1024`；单段报头块 `MaxPartHeaderBytes = 16 * 1024`；多文件全部返回；畸形请求体返回 `false` 并记 `LogHelper.Error`，不抛异常。
- 文件分段写入策略：先写内存，累计超过 `LargeBodyThreshold` 后转为临时文件；`FilePart.Data` 指向最终载体。
- 文本字段与文件名继续按 UTF-8 解码；分段值不做 `Trim()`。
- JSON 与 `application/x-www-form-urlencoded` 从 `Body` 流读取，禁止先整体 `ToArray()`。

## 5. chunked

- `HttpRequestSplitter` 识别 `Transfer-Encoding: chunked`，改为增量状态机：
  - 支持 chunk 扩展，忽略扩展内容；
  - 支持 trailer，读到空行结束；
  - 解码后总长度超过 `MaxRequestBodySize` 时抛 413；
  - 畸形 chunk 头、缺终止块、同时存在 `Content-Length` 时抛 400。
- 状态机必须跨多次 `TrySplit` 继续，不得每次从帧首重扫（避免 O(n²)）。
- `HttpRequestDecoder` 负责把 chunked 负载解码进 `Body`（内存或落盘），`ContentLength` 表示解码后的长度。
- 删除 `ChunkedNotSupported` 的抛错路径与 README「不支持 chunked」表述。
- `Content-Length` 按 64 位整数解析：语法非法回 400；数值超出 `long` 上限回 413 且剩余量按未知（`-1`）交回处理器兜底；超过 `MaxRequestBodySize` 的合法长度按 413 汇报 `Content-Length` 与实际到达字节之差，不受 `int` 上限截断。
- 原始 chunk 字节超过 `MaxFrameLength` 与解码后长度超过 `MaxRequestBodySize` 共用 413 语义。

## 6. 100-continue（仅 `SAEA.Http`）

- `HttpRequestSplitter` 新增对 `Expect` 报头的扫描（第 4 个定帧相关字段）。
- 当请求含 `Expect: 100-continue` 且确实有请求体时，在请求体尚未到齐前触发一次回调。
- 回调由 `HttpSocket` 在创建分帧器时绑定，通过 `_serverSokcet.SendAsync(channelId, bytes)` 直接写出 `HTTP/1.1 100 Continue\r\n\r\n`；发送失败只记日志，不影响请求处理。
- 每个请求最多发送一次；分帧器在帧切出或 `Reset()` 后清空该状态。
- 不等待客户端确认后再继续读取；服务端仍按正常节奏读 body。
- 报头阶段的畸形与定长超限在回 100 之前判定，这类 `400`/`413` 请求不会先收到 100 Continue；chunked 的畸形与超限可能到后续分块才被发现，此时 100 已发出，属协议允许。
- 已知限制：100 Continue 通过裸套接字直发、不参与出站串行化，同一连接上「流水发送 + `Expect`」时可能与上一条响应交错，属非合规客户端用法。

## 7. 413 有界排空（新增最小跨包接口）

- `HttpRequestSplitter` 在 413 时给出剩余请求体字节数：定长为 `Content-Length` 减已到达字节；剩余量未知（chunked、`Content-Length` 超 `long` 上限）时为 `-1`，由处理器按 `MaxRequestBodySize` 兜底。
- `HttpProtocolHandler` 写出错误响应后按 `WebConfig.RequestDrainTimeout` 声明排空规格；`SAEA.Sockets.Pipeline` 新增 `IDrainableChannel`（连接级规格的一次性存取）与 `IDrainedCloseStream`（收口末端执行半关排空），`SAEA.Sockets` 新增 `IGracefulCloseFlow`（把排空交给连接层）。
- `SaeaConnection` 半关写方向后按排空规格读取并丢弃剩余请求体：排空字节只在连接内计数、不写入 Pipe，因此不受接收背压暂停影响；计数单位为解密后的明文字节（TLS 连接）。
- 排空在到达字节预算、超时、对端提前半关（入站结束或已断开）时结束，随后关闭连接；排空开始时同时解除传输层与应用级暂停，恢复的是传输层接收，读到的字节只计数丢弃、不写入 `Pipe`，因此不会交给业务。
- 接收投递增加每令牌重入保护，避免排空与正常接收对同一 socket 并发读取；接收占用在断开与异常分支统一释放。

## 8. 本次不做的项

- 全局并发请求体预算：不新增闸门，仅在 README 说明 `MaxConnects × 单请求预算` 是理论最坏值。
- 413 排空仍是一次性读完剩余请求体的有界过程：定长按精确剩余量，chunked 或 `Content-Length` 超 `long` 上限时按 `MaxRequestBodySize` 兜底；不做「排到一半直接放弃」的提前收口。
- 分帧阶段流式落盘（把 body 边收边写文件）：需要改 `IFrameSplitter` 契约，超出本次范围。

## 9. 迁移指引

- `byte[] body = request.Body;` → `using (var reader = new StreamReader(request.Body, Encoding.UTF8)) { ... }`。
- `FilePart.Data`：`CollectionAssert.AreEqual(expected, part.Data)` 类代码改为读取流；保存文件用 `part.Save(path)`。
- 需要随机访问时先 `Body.Seek(0, SeekOrigin.Begin)`；流不可重复读取时自行复制。

## 10. 验收

- `dotnet build Src/SAEA.Sockets.sln -c Debug` 0 错误，警告数不高于改动前。
- `SAEA.Http.Tests`、`SAEA.MVC.Tests` 串行 `-c Release` 全绿。
- 新增用例至少覆盖：
  - 阈值边界（等于阈值走内存、超过阈值落盘）；
  - 落盘请求结束后临时文件被删除；
  - 内存流生命周期内字节与原始 body 一致；
  - multipart 流式解析的多文件、前导内容、上限；
  - chunked 正常、超限 413、畸形 400、跨读边界；
  - 非数组背衬的 chunked 输入在成功与异常路径上各只归还一次帧引用；
  - 100-continue 收到 `100 Continue` 后仍能正常收到最终响应；
  - `Content-Length` 超过 `int` 上限时 413 汇报的剩余量不被截断，数值溢出按剩余量未知处理；
  - 413 排空期间客户端提前半关时连接立即收口，不等满排空超时。
- 分配口径按仓库约定：`GC.GetTotalAllocatedBytes(true)` 窗口差 ÷ 请求数，不引用 RPS。

## 11. 风险

- 公开 API 破坏式变更，需要 README、示例与下游代码同步；未同步的调用方会在编译期失败（预期）。
- `HttpMessage` 在连接内复用，请求体流必须随 `ResetForNextRequest` 释放并归还整帧，漏归还会造成整帧缓冲泄漏。
- 大请求体落盘会增加磁盘 I/O；临时目录空间不足时解码失败，按 500 处理并记录日志。
- chunked 与 100-continue 的组合需要额外用例覆盖。