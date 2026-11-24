# HTTP 请求体流式化与服务器端硬化设计

- 日期：2026-11-24
- 状态：已确认，分阶段实施
- 范围：`SAEA.Http`、`SAEA.MVC`（文档与示例）、`SAEA.Http.Tests`、`SAEA.MVC.Tests`
- 不在范围：`SAEA.Sockets`、`SAEA.Sockets.Pipeline` 的任何接口改动

## 1. 目标

1. 请求体与上传文件不再以 `byte[]` 暴露，统一改为只读 `Stream`。
2. 大请求体按阈值落盘，避免请求结束后整段驻留内存、避免重复复制。
3. multipart 解析改为单遍流式扫描，不依赖整段 `byte[]`。
4. 支持 `Transfer-Encoding: chunked`。
5. 支持 `Expect: 100-continue`。
6. 不引入跨包接口：分帧与传输层保持现状。

## 2. 公开契约变更（破坏式）

- `HttpBase` 移除 `Body` 属性，避免请求与响应共用同一成员。
- `HttpResponse.Body` 保持 `byte[]`，响应写入路径与现有 API 不变。
- `HttpRequest.Body` 改为 `Stream`（只读、可 `Seek`、`Position` 可读写）。
  - 无请求体时为 `null`。
  - 生命周期与请求一致：请求结束（`HttpRequest.Dispose` / 连接复用复位）后不可再读，读到已释放对象抛 `ObjectDisposedException`。
- `HttpMessage.Body` 改为 `Stream`，作为请求消息的内部载体，语义与 `HttpRequest.Body` 一致。
- `FilePart.Data` 改为 `Stream`，语义同上；`FilePart` 实现 `IDisposable`，释放时删除其临时文件。
- `FilePart.Save(string filePath)` 从 `Data` 复制：可 `Seek` 时先回到 0，复制完成后回到 0，便于重复保存。
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

## 6. 100-continue（仅 `SAEA.Http`）

- `HttpRequestSplitter` 新增对 `Expect` 报头的扫描（第 4 个定帧相关字段）。
- 当请求含 `Expect: 100-continue` 且确实有请求体时，在请求体尚未到齐前触发一次回调。
- 回调由 `HttpSocket` 在创建分帧器时绑定，通过 `_serverSokcet.SendAsync(channelId, bytes)` 直接写出 `HTTP/1.1 100 Continue\r\n\r\n`；发送失败只记日志，不影响请求处理。
- 每个请求最多发送一次；分帧器在帧切出或 `Reset()` 后清空该状态。
- 不等待客户端确认后再继续读取；服务端仍按正常节奏读 body。

## 7. 本次不做的项

- 全局并发请求体预算：不新增闸门，仅在 README 说明 `MaxConnects × 单请求预算` 是理论最坏值。
- 413 后的半关与请求体排空：不改传输层，仅在 README 记录客户端可能收到 RST。
- 分帧阶段流式落盘（把 body 边收边写文件）：需要改 `IFrameSplitter` 契约，超出本次范围。

## 8. 迁移指引

- `byte[] body = request.Body;` → `using (var reader = new StreamReader(request.Body, Encoding.UTF8)) { ... }`。
- `FilePart.Data`：`CollectionAssert.AreEqual(expected, part.Data)` 类代码改为读取流；保存文件用 `part.Save(path)`。
- 需要随机访问时先 `Body.Seek(0, SeekOrigin.Begin)`；流不可重复读取时自行复制。

## 9. 验收

- `dotnet build Src/SAEA.Sockets.sln -c Debug` 0 错误，警告数不高于改动前。
- `SAEA.Http.Tests`、`SAEA.MVC.Tests` 串行 `-c Release` 全绿。
- 新增用例至少覆盖：
  - 阈值边界（等于阈值走内存、超过阈值落盘）；
  - 落盘请求结束后临时文件被删除；
  - 内存流生命周期内字节与原始 body 一致；
  - multipart 流式解析的多文件、前导内容、上限；
  - chunked 正常、超限 413、畸形 400、跨读边界；
  - 100-continue 收到 `100 Continue` 后仍能正常收到最终响应。
- 分配口径按仓库约定：`GC.GetTotalAllocatedBytes(true)` 窗口差 ÷ 请求数，不引用 RPS。

## 10. 风险

- 公开 API 破坏式变更，需要 README、示例与下游代码同步；未同步的调用方会在编译期失败（预期）。
- `HttpMessage` 在连接内复用，请求体流必须随 `ResetForNextRequest` 释放并归还整帧，漏归还会造成整帧缓冲泄漏。
- 大请求体落盘会增加磁盘 I/O；临时目录空间不足时解码失败，按 500 处理并记录日志。
- chunked 与 100-continue 的组合需要额外用例覆盖。