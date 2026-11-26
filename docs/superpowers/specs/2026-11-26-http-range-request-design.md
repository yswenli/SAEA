# HTTP Range 请求设计（单区间）

## 目标

为 SAEA.Http 与 SAEA.MVC 的静态文件与流式大文件提供 HTTP `Range` 支持，使浏览器、下载器与断点续传客户端可以只取文件的一部分。

## 范围

本设计只覆盖单区间：

- `bytes=a-b`：闭区间，`b` 超过正文末尾时截断到 `total-1`。
- `bytes=a-`：从 `a` 到正文末尾。
- `bytes=-N`：正文末尾 `N` 个字节。

以下情形按 RFC 7233 的允许方式忽略 `Range`，返回 `200` 全量正文，不实现 `multipart/byteranges`：

- 多区间（区间规格中出现逗号）。
- unit 不等于 `bytes`、缺少 `-`、`-` 多于一个、数字解析失败、`b < a`、两侧都为空。
- 不支持 `If-Range`。

## 分层

- `SAEA.Http/Common/ByteRangeHeader.cs`：把 `Range` 头与正文总长度解析为 `Ignore`、`Satisfiable` 或 `Unsatisfiable`，`Start`/`End` 为闭区间，`Length` 为区间字节数。数字按 `NumberStyles.None` + `InvariantCulture` 解析，拒绝正负号、空白与 long 溢出。
- `HttpResponse.ApplyRangeHeader`：用于内存正文（静态小文件、控制器 `FileResult`）。只改写状态为 `200` 的响应；`_isZiped` 时直接返回。
- `HttpBigDataResult` 与 MVC `BigDataResult`：用于流式大文件，解析后定位到 `Start` 并只发送 `Length` 字节。
- `HttpContext.cs` 与 MVC `RequestPipeline.cs`：在 `SetCached` 之后、连接策略之前调用 `ApplyRangeHeader`，只对 `IFileResult` 生效。

## 响应语义

| 情形 | 状态 | 关键响应头 | 正文 |
| --- | --- | --- | --- |
| 无 `Range` | `200` | `Accept-Ranges: bytes` | 完整正文 |
| 可满足 | `206` | `Accept-Ranges: bytes`、`Content-Range: bytes a-b/total` | 区间字节 |
| 不可满足 | `416` | `Accept-Ranges: bytes`、`Content-Range: bytes */total` | 空，`Content-Length: 0` |
| 语法非法或多区间 | `200` | `Accept-Ranges: bytes`，不写 `Content-Range` | 完整正文 |

不可满足的判定顺序：

1. 先判语法。空头、unit 非法、多区间、连字符数量不对、数字解析失败、`b < a` 都按忽略处理。
2. 语法合法后，若 `totalLength == 0`，一律不可满足。这样 `bytes=-5`、`bytes=0-`、`bytes=0-0` 在空正文上都返回 `416`，避免写出 `bytes 0--1/0` 这类非法区间。
3. 再按 `-N`、`a-`、`a-b` 计算绝对区间。

## 压缩限制

`_isZiped == true` 时，内存正文不启用 `Range`，也不写 `Accept-Ranges`。原因是 gzip 压缩后的正文长度与 `Content-Length` 不一致，按未压缩长度计算区间会切错位置。

## 大文件与 HEAD

- 只有 `stream.CanSeek` 且总长度已知时才解析 `Range`；其余按完整响应处理。
- 206 时把流定位到 `Start`，只读 `Length` 字节；416 时只写报头与 `SendEnd()`。
- 保留 HEAD 的 `SuppressBody` 机制：206/416 的 `Content-Length` 分别等于区间长度与 0，正文由响应层统一丢弃。
- MVC `BigDataResult` 保持 `MarkResponseStarted` 语义与 `finally` 中的 `stream.Close()`；Http `HttpBigDataResult` 维持不关闭流的既有行为，并继续使用 `MemoryPoolManager` 的 1024 字节缓冲。

## 测试覆盖

- `SAEA.Http.Tests/ByteRangeHeaderTests`：三种区间写法、`b` 截断、不可满足、`b < a`、非法 unit、多区间、`-0`、空正文、大小写与空白、long 溢出。
- `SAEA.Http.Tests/HttpResponseRangeTests`：反射构造 `HttpResponse`，覆盖无 `Range`、206、尾部区间、416、非法语法、多区间、非 `200` 状态与 `_isZiped`。
- `SAEA.Http.Tests/HttpRangeHostTests`：真实 `WebHost` 端到端覆盖静态小文件的 206/尾部/416/无 `Range`/多区间回 `200`/HEAD，以及超过 4MB 静态大文件的 206 与 416。
- `SAEA.MVC.Tests/RangeRequestTests`：`TestAppFactory` 端到端覆盖静态小文件 206/尾部/416/无 `Range`/HEAD、原始 socket 多区间与非法语法回 `200`、4MB 以上大文件与控制器 `FileResult`、`BigDataResult`。

## 已知限制

- 只支持单区间，多区间回退为 `200` 全量。
- 不支持 `If-Range`，条件请求始终按当前正文计算。
- 内存正文切片使用 `Buffer.BlockCopy` 与 `int` 下标，正文超过 2GB 不支持。
- 开启 gzip 时内存正文不做 `Range`。
- 不可定位的流（如网络流、压缩流）不解析 `Range`。