# 文件结果的 Content-Disposition 设计

## 背景与目标

SAEA.Http 与 SAEA.MVC 的文件结果此前只能设置 `Content-Type`，业务要提供"另存为"文件名，必须自己拼 `Content-Disposition` 头。头值里同时包含 quoted-string 与 RFC 5987 扩展值，还要防止 CR/LF 注入，容易写错。

本设计让四个文件结果类直接携带下载文件名，并在响应写出时统一生成：

```text
Content-Disposition: attachment; filename="..."; filename*=UTF-8''...
```

目标：

- 小文件与大文件流式两条路径行为一致。
- 业务侧一行式使用：`File(filePath, fileName)`、`BigData(filePath, fileName)` 或结果类构造参数。
- 文件名为空时退回原有普通响应，不改变既有调用方的行为。

## API

新增标记接口 `SAEA.Http.Model.IAttachmentResult`：

```csharp
public interface IAttachmentResult
{
    string FileName { get; }
}
```

新增静态工具 `SAEA.Http.Common.ContentDispositionHelper`：

```csharp
public static string Build(string fileName, bool inline = false)
```

结果类新增构造重载（原有重载全部保留）：

```csharp
// SAEA.Http.Model.HttpFileResult / SAEA.MVC.Results.FileResult
public HttpFileResult(string filePath, bool isStaticsCached, string fileName);
public HttpFileResult(Stream stream, string contentType, HttpStatusCode status, string fileName);
public HttpFileResult(bool isStaticsCached, string filePath, string contentType, HttpStatusCode status, string fileName);

// SAEA.Http.Model.HttpBigDataResult / SAEA.MVC.Results.BigDataResult
public HttpBigDataResult(Stream stream, string contentType, HttpStatusCode status, bool leaveOpen, string fileName);
public HttpBigDataResult(string filePath, string fileName, HttpStatusCode status = HttpStatusCode.OK);
```

MVC 控制器新增：

```csharp
protected ActionResult File(string filePath, string fileName);
protected ActionResult BigData(string filePath, string fileName);
```

`ResponseHeaderType` 末尾新增 `ContentDisposition = 30`，`Description` 为 `Content-Disposition`。

## 编码规则

`ContentDispositionHelper.Build` 的处理顺序：

1. `null`、空串或纯空白输入直接返回 `null`。
2. 只保留最后一个路径段，`/` 与 `\` 都作为分隔符，目录不会进入文件名。
3. 删除全部 `char.IsControl` 字符，覆盖 CR、LF、TAB 等。
4. 清洗后仍为空白时返回 `null`。
5. ASCII 回退名：`0x20`–`0x7E` 保留，`"` 与 `\` 替换为 `_`，其余字符（含中文）逐个替换为 `_`；回退名为空时用 `download`。
6. 扩展值使用 `Uri.EscapeDataString` 做 UTF-8 百分号编码，写出 `filename*=UTF-8''` 加编码结果。

输出示例：

```text
attachment; filename="report.pdf"; filename*=UTF-8''report.pdf
attachment; filename="__.pdf"; filename*=UTF-8''%E6%8A%A5%E5%91%8A.pdf
```

`inline` 为 `true` 时前缀改为 `inline`。

## 安全

- CR/LF 与其余控制字符在拼接前删除，避免响应头注入或头截断。
- 回退名中的 `"` 与 `\` 被替换，quoted-string 不会被提前闭合。
- 路径只取最后一段，避免调用方把 `../` 等目录信息带进下载文件名。
- 非 ASCII 字符只出现在 `filename*` 的百分号编码里，头值始终是可见 ASCII。

## 应用位置

- `HttpResponse.SetCached` 的 `IFileResult` 分支：小文件结果在写 `Content-Type` 与正文后写头。
- `HttpBigDataResult` / `BigDataResult` 构造函数：在 `response.ContentType` 之后、所有 `SendHeader` 分支之前写头，206、200 与 416 分支都会带上该头。
- 大文件结果的 `leaveOpen`、`total` 计算与 Range 逻辑保持不变。

## 非目标

- 不根据文件名自动修改 `Content-Type`，`Content-Type` 仍由调用方显式传入。
- `inline` 仅通过 helper 参数手动设置，结果类固定生成 `attachment`。
- 不改变 Range 语义、分块发送与连接关闭策略。
- 不处理 `Content-Disposition` 的 `filename*` 语言标签等可选参数。

## 测试覆盖

- `ContentDispositionHelperTests`：ASCII、中文、`inline`、CR/LF 清除、路径只取最后一段、`null` 与空白输入。
- `HttpResponseAttachmentTests`：`SetCached` 对带文件名与不带文件名的文件结果分别写头与不写头。
- `HttpRangeHostTests`：真实宿主下 `HttpBigDataResult` 流式结果的 `Content-Disposition`。
- `RangeRequestTests`：MVC 控制器 `FileResult` 与 `BigDataResult` 两条端到端路径的 `Content-Disposition`。