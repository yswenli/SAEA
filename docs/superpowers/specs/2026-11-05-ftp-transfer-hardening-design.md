# SAEA.FTP 传输可靠性、冲突处理与并行加速设计（2026-11-05）

## 1. 背景与问题

用户在使用 `SAEA.FTP` 时遇到两类问题：

1. **网络抖动直接中断上传/下载**：没有自动重试，也没有断点续传，只能整文件重来。
2. **同名文件被静默覆盖**：上传时服务器已有同名文件、或下载时本地已有同名文件，都没有“覆盖 / 跳过 / 改名”的确认。

同时，用户反馈上传/下载速度明显慢于同类工具，希望支持动态缓冲和并行传输，并开放配置。

## 2. 目标

- 上传、下载在网络抖动导致的临时错误下自动重试；服务器支持时从断点续传，不支持时安全地整文件重来。
- 上传、下载两端都做同名检测；通过每文件回调让调用方决定“覆盖 / 跳过 / 改名”；没有回调时默认跳过并返回结果，**绝不静默覆盖**。
- 大文件优先使用多条数据连接分段并行传输；服务器不支持 `REST`、不允许并发或分片连续失败时，自动降级为单连接。
- 提升传输吞吐：动态缓冲区、按连接/分片的并行任务、避免每收到一段就打开/关闭文件。
- 补齐现代 FTP 常用扩展：`FEAT`、`REST`、`MDTM`、`EPSV`、`TYPE I`、标准 `MLSD`，服务端在每次数据传输结束后正确回复 `226`。
- 自带 `FTPServer` 与 `FTPClient` 一起改造，保证两者能互相配合完成并行与续传。

## 3. 非目标

- 不实现 FTPS（`AUTH TLS` / `PBSZ` / `PROT`）、SFTP、HTTP 代理、SOCKS 代理。
- 不实现主动模式（`PORT` / `EPRT` 建连）；服务端继续返回“未实现/禁用”，客户端使用 `PASV`/`EPSV`。
- 不实现 `MODE B` / `MODE C`、`SITE` 命令、服务器端文件锁、配额管理。
- 不提供图形化确认框；确认通过调用方回调完成，库本身不阻塞等待 UI。
- 不改变第三方客户端默认体验：自带服务端对已存在文件的 `STOR` 默认仍按 FTP 惯例覆盖。

## 4. 反目标（即使“能跑”也算失败）

- 任何情况下静默覆盖同名文件。
- 失败后留下半截文件却当成成功；或把临时文件暴露为目标文件。
- 对不可重试错误（权限、路径不存在、磁盘满、认证失败）无限重试。
- 服务器不支持 `REST` 或并发时仍然强行并行，导致数据错乱。
- 并行失败后目标文件被写坏，且无法回退到单连接完成。
- 为了并行而破坏现有 `Upload` / `Download` 调用方。

## 5. 现状与关键问题（代码定位）

- `Src/SAEA.FTP/FTPClient.cs`
  - `Upload` 直接 `STOR`，按 `_client.Config.BufferSize`（默认 10KB）同步循环发送；不等待最终 `226`。
  - `Download` 先 `SIZE`，再 `RETR`，通过 `FTPDataManager.Checked` 轮询临时文件长度；`FTPDataManager.Receive` 每收到一段都 `File.Open` / `Close` 一次，开销很大。
  - 进度回调由单独轮询线程驱动，存在竞态和精度问题。
- `Src/SAEA.FTP/Core/FTPDataManager.cs`
  - 下载临时文件路径在 `Data` 目录；`FileComplete` 使用 `File.Move`，在目标已存在时行为依赖运行时，且没有显式冲突处理。
  - `_list` 用于目录列表，文件传输应改为直接流式写盘。
- `Src/SAEA.FTP/Net/ClientSocket.cs`
  - 单控制连接、单 `FTPDataManager`；`PASV` 解析只支持 IPv4 的 `227` 格式；没有 `EPSV`、`REST`、`MDTM`、`FEAT`。
  - 响应泵用 `FTPStream.ReadText()` 把所有完整行拼成一个字符串再 `ServerResponse.Parse`，无法正确处理多行 `FEAT`/`MLSD` 和 `150` 中间响应。
- `Src/SAEA.FTP/Net/ServerSocket.cs`
  - **整个服务端只有一个 `FTPStream`**，多控制连接会互相串命令；必须改为每连接一个缓冲区。
  - 控制连接命令在接收回调里同步执行；`SendFile` / `ReceiveFile` 会阻塞到数据连接关闭。
- `Src/SAEA.FTP/Core/FTPServerConfigManager.cs`、`Model/FTPUser.cs`
  - 会话状态（`CurrentPath`、`FTPDataManager`、`FTPDataSocketManager`、`IsLogin`）挂在按用户名共享的 `FTPUser` 上；同一账号多会话会互相覆盖。并行传输前必须把状态下沉到连接级。
- `Src/SAEA.FTP/FTPServer.cs`
  - 没有 `REST`、`MDTM`、`EPSV`；`FEAT` 回复格式不符合多行响应；`TYPE` 永远回复 `A`；`RETR`/`STOR` 结束后没有 `226`；`MLSD` 的 `modify` 使用 `FileTimeUtc` 而不是标准时间格式。
  - `PORT` 被禁用；`PASV` 每次用 `IPHelper.GetFreePort()`，并发时存在端口竞争。

## 6. 已确认的产品决策

以下为对话中用户明确确认的内容：

1. 上传和下载**两边都要**处理同名冲突。
2. 冲突选项为**覆盖 / 跳过 / 改名**；没有回调时**默认跳过并返回结果**，不静默覆盖。
3. 网络抖动导致的中断要**自动重试**，并尽量断点续传。
4. 大文件要**单文件多数据连接分段并行**；服务器不支持就**单连接处理**。
5. 允许为了并行**额外开 2–4 条控制连接**（同一账号重复登录）；服务器限制并发时自动降级。
6. `FTPClient` 与 `FTPServer` **一起改**。
7. 自带 `FTPServer` 的 `STOR` 默认**保持标准覆盖行为**以兼容第三方客户端；额外提供配置让服务端强制拒绝或改名。

## 7. 实现者已经确定的默认值（用户未反对）

这些是实现者拍板的默认值，写进文档供评审；实现时可调整，但改变默认行为需要说明。

### 7.1 并行

- `EnableParallel = true`
- `MaxSessions = 4`（包含主连接；即最多再开 3 条附加会话）
- `ParallelThresholdBytes = 8MB`：小于该值不并行。
- `MinSegmentSize = 1MB`：分片数不超过 `fileSize / MinSegmentSize`，避免小分片。
- 分片数 `N = min(MaxSessions, ceil(fileSize / MinSegmentSize))`；每片大小 `ceil(fileSize / N)`，最后一片取剩余长度。
- **分片边界（下载）**：标准 `RETR` 没有结束偏移，只有 `REST` 起点。每个分片连接执行 `REST <offset>` + `RETR`，客户端**精确读取 `len` 字节后主动关闭数据连接**；只要已收到 `len` 字节就判定该分片成功，忽略随后可能出现的 `226`（服务器刚好发完）或 `426`（服务器检测到客户端提前关闭）。不读取、不丢弃边界之后的字节，因此不会浪费带宽。分片读取不足 `len` 字节且连接被对端关闭，才判定为失败并重试。
- **服务端配合（下载）**：数据连接被客户端提前关闭时回复 `426`，属于正常现象，不写错误日志、不标记文件损坏。
- **分片写入（上传）**：分片 0 先执行 `REST 0` + `STOR <target>`（完成截断并固定目标名），分片 0 的数据连接建立成功后，其余分片才以 `REST <offset>` + `STOR <target>` 启动（屏障）。其余分片允许向 EOF 之后写入。
- **并行上传能力探测**：正式传输前，用临时远端文件 `.<target>.saea-probe-<guid>` 做一次“写 1KB → `REST 1024` 再写 1KB → `REST 1024` + `RETR` 读回比对”的探测；一致才启用并行上传，否则只用单连接。探测文件无论成败都删除。
- **并行上传完成判定**：所有分片都收到 `226`，且最终 `SIZE <target>` 等于本地长度。任一条件不满足即视为并行失败。
- **降级规则**：任一会话建立失败、`REST`/`STOR` 被拒、分片连续失败、探测失败或服务器返回并发限制时，关闭并释放所有附加会话，回退单连接，**从 0 重传并截断远端文件**（并行上传不做断点续传）。
- **并行下载降级**：服务器不支持 `REST`（`500/502`）时直接单连接整文件下载；分片失败重试仍失败时，关闭分片连接，单连接从 0 重新下载到新的临时文件，成功的旧临时文件在替换前删除。

### 7.2 重试与续传

- `MaxRetries = 3`（首次尝试之外最多再重试 3 次，总尝试 4 次）。
- 退避：`RetryDelay * 2^(attempt-1)`，默认基数 500ms，上限 10s，加少量随机抖动。
- 可重试：连接被重置、数据连接提前关闭、读写超时、`421`、`425`、`426`、`450`、`451`。
- 不可重试：`500/501/502/503/504`（命令/参数错误）、`530`（认证失败）、`550`（不存在/无权限）、`452`（服务器磁盘满）、本地文件不存在/无权限/磁盘满。
- 控制连接断开：重新 `Connect()`、重新登录、恢复工作目录，再继续；恢复失败则按不可重试失败返回。
- 下载续传：保留 `.part` 临时文件；重试时取当前长度，校验远端 `SIZE` 与 `MDTM` 未变化后 `REST <offset>` + `RETR`；变化则从 0 重新下载。分片下载的重试仍以该分片的 `offset` 为起点，不改变其它分片。
- 上传续传：**仅单连接**支持。校验远端当前大小等于本地已发送偏移后 `REST <offset>` + `STOR`；服务器不支持或大小不一致则从 0 重传（并截断远端文件）。并行上传失败一律回退单连接并从 0 开始，不使用“远端长度”作为续传依据（并行写入可能留下空洞，长度不可信）。
- **续传范围**：同一次调用内的自动重试始终可用；跨调用/进程重启的续传需要 `EnableResumeAcrossCalls = true`（默认 `false`）。开启时临时文件名固定为 `.<target>.saea-part`，并写同名 `.meta` 记录校验信息（下载记录远端 `SIZE`/`MDTM`，上传记录本地长度/最后写入时间）；开始前校验，不匹配则删除重来。关闭时临时文件用 GUID 名，`KeepPartialOnFailure` 只决定是否保留，不承诺可以续传。并行传输永不跨调用续传。
- 最终失败时：默认保留 `.part`（`KeepPartialOnFailure = true`）；调用方可通过选项关闭并在失败时删除。

### 7.3 缓冲区与线程

- `BufferSize = 64KB`，`MinBufferSize = 16KB`，`MaxBufferSize = 1MB`；`EnableAdaptiveBuffer = true`。
- 自适应策略：以分片为单位统计吞吐；连续稳定传输时逐步放大缓冲区（不超过 `MaxBufferSize`），出现超时/重试或吞吐下降时回退到较小值。算法采用简单的步进式调整，不做复杂预测。
- 下载：每个分片一个长期打开的 `FileStream`（`FileShare.ReadWrite`，随机写），接收回调直接写入，不再每段开关文件。
- 上传：每个分片一个读取任务，使用池化缓冲区读取本地文件并发送；分片任务本身由 `Task` 承载，数量受 `MaxSessions` 限制，不创建无界线程。
- 进度：所有分片共享一个原子计数器，回调报告聚合进度 `(已完成字节, 总字节)`；总字节未知时第二个参数为 `0`。

### 7.4 冲突检测

- 上传：先 `SIZE <remote>`；`213` 表示存在，`550` 表示不存在；`500/502` 表示不支持，再尝试 `MLSD <dir>` 匹配文件名。仍无法判断时，标记 `ExistsUnknown`。
- 下载：直接检查本地目标路径是否存在（`File.Exists`），并读取长度。
- `ExistsUnknown` 且没有回调时，按默认策略跳过（安全优先）；有回调时把 `ExistsUnknown = true` 交给调用方决定。
- **回调次数**：每个逻辑文件只调用一次 `ConflictHandler`，且在所有会话/重试开始之前完成决策。解析出的最终目标名在整个传输（含并行分片、重连、重试）中复用，任何分片或重试都不会再次触发回调。
- 覆盖上传：按标准 `STOR` 覆盖；覆盖下载：写临时文件，成功后原子替换目标。
- 跳过：不建立数据连接，直接返回 `Skipped`。
- 改名：先调用回调给出的新名字；没有给则按 `{name} ({index}){ext}` 递增，直到目标不存在；仍冲突则继续递增，超过 1000 次报错。
- 下载改名只改本地目标文件名；上传改名只改远端文件名，本地文件名不变。
- 已知限制：检测与真正写入之间存在 TOCTOU 窗口，第三方服务器无法完全避免；自带服务端可通过覆盖策略降低风险。

### 7.5 服务端覆盖策略

- `ServerConfig.OverwritePolicy`：
  - `Overwrite`（默认）：保持 FTP 标准行为，`STOR` 直接覆盖。
  - `Reject`：`REST` 偏移为 0 且目标已存在时回复 `550`；非 0 偏移用于续传/并行，不拒绝。
  - `Rename`：服务端自动生成唯一文件名；在最终 `226`/`250` 回复中附加 `NEWNAME=<name>`（非标准扩展），SAEA 客户端可解析，第三方客户端可能忽略。
- 默认值必须是 `Overwrite`，保证第三方客户端行为不变。
- `FEAT` 额外声明策略：`SAEA-OVERWRITE=Overwrite|Reject|Rename`。SAEA 客户端据此决定并行上传与改名处理：
  - `Overwrite`：按正常流程并行。
  - `Reject`：目标已存在时由冲突回调决定；若回调选“覆盖”而服务器拒绝，返回 `Failed` 并带明确错误，不静默改名。
  - `Rename`：客户端禁用并行上传，改用单连接，并从 `226 ... NEWNAME=` 中取实际文件名写入 `FTPTransferResult.RemotePath`。解析不到时按原目标名视为完成。

## 8. 公共 API 设计

新增文件建议放在 `Src/SAEA.FTP/Model/` 与 `Src/SAEA.FTP/Core/`，命名空间保持 `SAEA.FTP` / `SAEA.FTP.Model` / `SAEA.FTP.Core`。

### 8.1 冲突与结果类型

```csharp
public enum FTPTransferDirection { Upload, Download }

public enum FTPConflictAction { Overwrite, Skip, Rename }

public sealed class FTPConflictContext
{
    public FTPTransferDirection Direction { get; set; }
    public string LocalPath { get; set; }
    public string RemotePath { get; set; }
    public bool TargetExists { get; set; }
    public bool ExistsUnknown { get; set; }
    public long? LocalSize { get; set; }
    public long? RemoteSize { get; set; }
    public DateTime? RemoteModified { get; set; }
    public long IncomingSize { get; set; }
}

public sealed class FTPConflictResult
{
    public FTPConflictAction Action { get; set; }
    public string NewName { get; set; }
}

public enum FTPTransferStatus { Completed, Skipped, Renamed, Failed }

public sealed class FTPTransferResult
{
    public FTPTransferStatus Status { get; set; }
    public string LocalPath { get; set; }
    public string RemotePath { get; set; }
    public long BytesTransferred { get; set; }
    public int Attempts { get; set; }
    public int SessionsUsed { get; set; }
    public bool Resumed { get; set; }
    public FTPConflictAction? ConflictAction { get; set; }
    public Exception Error { get; set; }
}
```

### 8.2 传输选项

```csharp
public sealed class FTPTransferOptions
{
    // 可选值约定：数值型 0 表示“未设置”，回落到 ClientConfig；显式 >0 优先。
    public bool EnableParallel { get; set; } = true;
    public int MaxSessions { get; set; } = 4;
    public long ParallelThresholdBytes { get; set; } = 8L * 1024 * 1024;
    public long MinSegmentSize { get; set; } = 1L * 1024 * 1024;

    public int BufferSize { get; set; } = 0;        // 0 = 用 ClientConfig.BufferSize
    public int MinBufferSize { get; set; } = 16 * 1024;
    public int MaxBufferSize { get; set; } = 1024 * 1024;
    public bool EnableAdaptiveBuffer { get; set; } = true;

    public int MaxRetries { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);
    public bool EnableResume { get; set; } = true;
    public bool EnableResumeAcrossCalls { get; set; } = false;
    public bool KeepPartialOnFailure { get; set; } = true;

    public FTPConflictAction DefaultConflictAction { get; set; } = FTPConflictAction.Skip;
    public string RenamePattern { get; set; } = "{name} ({index}){ext}";
    public Func<FTPConflictContext, FTPConflictResult> ConflictHandler { get; set; }

    public TimeSpan CommandTimeout { get; set; } = TimeSpan.Zero; // Zero = 用 ClientConfig
    public TimeSpan DataTimeout { get; set; } = TimeSpan.Zero;    // Zero = 用 ClientConfig
}
```

**配置优先级**：`FTPTransferOptions` 中显式设置的值 > `ClientConfig` > 全局默认。`ClientConfig.BufferSize` 默认从 10KB 调整为 64KB；`new FTPClient(..., bufferSize: 10240)` 这类旧构造参数会写入 `ClientConfig.BufferSize`，因此旧调用方仍得到 10KB，而不会被新默认值覆盖。`FTPServer` 的构造参数 `bufferSize` 同理写入 `ServerConfig`，优先级为 `ServerConfig` > 构造参数 > 默认 64KB。

### 8.3 FTPClient 方法

保留现有签名，新增重载：

```csharp
public FTPTransferResult Upload(string filePath, FTPTransferOptions options,
    Action<long, long> uploading = null);

public FTPTransferResult Upload(string filePath, string remoteFileName,
    FTPTransferOptions options, Action<long, long> uploading = null);

public FTPTransferResult Download(string fileName, string filePath,
    FTPTransferOptions options, Action<long, long> downing = null);
```

- 现有 `void Upload(...)` 内部调用新重载，使用默认选项；失败时抛异常，`Skipped`/`Renamed` 无法通过返回值区分（文档注明，推荐新重载）。
- 现有 `string Download(...)` 返回实际本地路径；`Skipped` 返回 `null`。
- 新重载不抛业务失败（`Status = Failed` 且带 `Error`），只对参数错误抛异常；这样调用方可以统一处理“跳过/改名/失败”。

### 8.4 其他公开配置

`ClientConfig` 增加：`ConnectTimeout`、`CommandTimeout`、`DataTimeout`、`Encoding`（默认 UTF-8）。
`ServerConfig` 增加：`OverwritePolicy`、`PassivePortMin`、`PassivePortMax`、`MaxSessionsPerUser`（0 = 不限制）、`DataTimeout`。
保留 `BufferSize` 属性，但默认值从 10KB 调整为 64KB；旧配置仍可显式设置小值。

## 9. 协议实现细节

### 9.1 客户端响应处理

- 用每连接一个的响应读取器替换当前“整段文本拼一次”的方式：
  - 支持单行 `NNN text`、多行 `NNN-...` 直到 `NNN ` 结束行。
  - 区分 `1xx` 中间响应与最终响应；数据命令先返回 `150`，传输结束后再读 `226`。
  - 命令与响应严格串行；超时按 `CommandTimeout` 处理。
- `FEAT` 在登录后发送一次，解析 `REST STREAM`、`SIZE`、`MDTM`、`MLSD`、`EPSV`、`UTF8` 等能力；不支持 `FEAT` 的服务器按保守默认值处理。
- 文件传输前发送 `TYPE I`；若服务器拒绝，继续按字节发送（现有行为兼容）。
- 数据连接优先 `EPSV`（解析 `229 (|||port|)`）；失败回退 `PASV`。`PASV` 返回的 IP 为 `0.0.0.0` 或与服务器不一致时，使用控制连接的对端 IP（NAT 场景）。

### 9.2 服务端命令

- `FEAT`：返回标准多行响应，声明 `REST STREAM`、`SIZE`、`MDTM`、`MLSD`、`UTF8`、`EPSV`，并附加 `SAEA-OVERWRITE=<策略>`。
- `REST <offset>`：记录到会话，回复 `350`；下一次 `RETR`/`STOR` 使用后清零。`RETR` 的偏移超过文件长度时返回 `551`/`554`；`STOR` 允许 `offset > 文件长度`（EOF 之后写入，形成空洞），用于并行上传，不返回错误。服务器不支持稀疏写时返回 `551`，客户端据探测结果本就不会走并行。
- `RETR`：从会话 `REST` 偏移开始读；`150` → 数据发送 → 数据连接正常关闭后 `226`。客户端为分片边界提前关闭数据连接时，回复 `426`，不视为服务器错误。
- `STOR`：`REST = 0` 时截断目标（或按覆盖策略处理）；`REST > 0` 时定位写入、不截断；`150` → 接收 → 关闭后 `226`。
- `MDTM <file>`：返回 `213 YYYYMMDDHHMMSS`（UTC）。
- `MLSD`：使用标准 `type=...;size=...;modify=YYYYMMDDHHMMSS; name` 格式。
- `EPSV`：分配被动端口并回复 `229 Entering Extended Passive Mode (|||port|)`；`PORT`/`EPRT` 仍返回禁用/未实现。
- `TYPE I` / `TYPE A`：都回复 `200`，文件传输按二进制处理；`LIST`/`NLST` 使用 ASCII。
- `ABOR`：非目标，回复 `502` 或忽略。
- 所有数据传输结束后必须回复 `226`（或失败时 `4xx/5xx`），客户端据此判断完成；提前关闭数据连接除外（回 `426`）。

### 9.3 服务端会话模型

- 新增 `FTPSession`（按控制连接 id 索引）：
  - `UserName`、`Authenticated`
  - `CurrentFtpPath`、`CurrentPath`
  - `FTPDataSocketManager`、`FTPDataManager`
  - `RestOffset`
  - `TransferInProgress`
  - `Type`（A/I）、`Utf8`
  - 每会话串行命令队列（避免长传输阻塞接收回调）
- `FTPServerConfigManager` 只保留凭证配置（用户名/密码/Root）；新增连接级会话表（`ConcurrentDictionary<string, FTPSession>`）。
- `ServerSocket` 的 `FTPStream` 改为 `ConcurrentDictionary<string, FTPStream>`；接收时按连接 id 取/建，断开时移除；一次接收循环处理多行命令。
- `FTPServer` 的所有命令改用会话状态；`USER` 只做凭证查找，`PASS` 只把当前会话标记为已登录，不再修改全局 `IsLogin`。
- 每个控制连接一个数据端口/数据 socket；`PASV`/`EPSV` 使用会话自己的 `FTPDataSocketManager`。
- 被动端口分配加锁/租约，优先使用配置范围；释放后归还，避免并发重复分配。
- 允许同一账号多会话；`MaxSessionsPerUser` 可限制（默认 0 = 不限制）。

### 9.4 服务端数据写入

- 单连接上传：可以直接写目标文件（`FileMode.OpenOrCreate`，`REST=0` 时 `SetLength(0)`；`REST>0` 时 `Seek`）。
- 并行上传：多个数据连接写入同一目标文件的不同偏移，使用 `FileShare.ReadWrite`，每个连接独立 `FileStream` 定位写入；全部由客户端协调，服务端不假设“最后一个连接”何时结束。
- 下载：直接按 `REST` 偏移读文件发送。
- 失败/断开：关闭文件句柄，记录日志；目标文件可能保留部分内容（FTP 标准行为），由客户端重试/续传修复。

## 10. 兼容性

- 现有 `Upload(string, Action<long,long>)`、`Download(string, string, Action<long,long>)` 保留。
- 默认 `BufferSize` 从 10KB 调到 64KB：行为更快，不改变协议语义。
- 默认冲突策略改为“跳过”：这是有意的安全变更；依赖旧“静默覆盖”的调用方需要显式传 `DefaultConflictAction = Overwrite` 或回调返回覆盖。
- 自带服务端默认 `OverwritePolicy = Overwrite`，第三方客户端（FileZilla、curl、FtpWebRequest 等）行为不变。
- 不修改 `SAEA.FTP.csproj` 的 `Version`，不修改文件头 banner 版本号（除非用户另行要求）。
- 目标框架保持 `netstandard2.0`、`LangVersion 8.0`；不使用 netstandard2.0 之外的 API（例如 `File.Move(src, dst, overwrite)` 不可用，需要用先删除/替换或流式复制实现原子替换）。

## 11. 错误分类与重试矩阵

| 场景 | 是否重试 | 处理 |
| --- | --- | --- |
| 控制连接 reset / 超时 | 是 | 重连、重登录、恢复路径后续传 |
| 数据连接提前关闭 / reset | 是 | 新建数据连接，按偏移续传 |
| 读写超时 | 是 | 断开数据连接，退避后重试 |
| `421` 服务不可用 | 是 | 重连后重试 |
| `425` 无法打开数据连接 | 是 | 重新 `PASV`/`EPSV` 后重试 |
| `426` 传输中止 | 是 | 按偏移续传 |
| `450` / `451` | 是 | 退避后重试 |
| `452` 磁盘满 | 否 | 失败返回 |
| `500/501/502/503/504` | 否 | 失败返回（协议/命令问题） |
| `530` 认证失败 | 否 | 失败返回 |
| `550` 不存在/无权限 | 否 | 失败返回 |
| 本地文件不存在/无权限/磁盘满 | 否 | 失败返回 |
| 冲突默认跳过 | 不适用 | 返回 `Skipped`，不重试 |

## 12. 验证计划

建议新增 `Src/SAEA.FTP.Tests`（`net10.0` + MSTest，参考 `SAEA.Common.Tests` 的结构），用本机回环启动 `FTPServer` 并跑 `FTPClient`。测试不依赖外部 FTP 服务。

### 12.1 冲突

1. 上传到已存在远端文件：回调返回 `Skip` → `Status = Skipped`，远端内容不变。
2. 上传回调返回 `Overwrite` → 远端内容更新。
3. 上传回调返回 `Rename` / 自动改名 → 远端出现新名字，原文件不变。
4. 无回调时默认 `Skip` → 远端内容不变，结果可判定。
5. 下载到已存在本地文件：三种动作同上，且 `Skipped` 时本地内容不变。
6. `SIZE`/`MLSD` 都不可用的模拟服务器 → `ExistsUnknown` + 默认跳过；回调可覆盖。

### 12.2 重试与续传

1. 数据连接传输到一半被强制关闭 → 自动重试，最终文件哈希与源一致。
2. 服务器支持 `REST` → 重试从断点继续（通过服务器日志/偏移断言）。
3. 服务器不支持 `REST` → 从 0 重传，最终结果正确。
4. 传输期间远端文件被修改 → 放弃续传，整文件重来。
5. 不可重试错误（如 `550`）→ 只尝试一次，返回 `Failed`。
6. 控制连接被断开 → 重连后续传成功。
7. 跨调用续传关闭（默认）→ 失败后新调用从头开始；开启后同目标可续传，远端/本地校验信息不匹配时自动丢弃。
8. 单连接上传中断后远端大小被篡改 → 不使用该长度续传，从 0 重传并截断。

### 12.3 并行

1. 64MB 文件、4 会话并行上传/下载 → 文件哈希一致，`SessionsUsed = 4`。
2. 并行下载各分片在边界精确关闭数据连接 → 每片只收到 `len` 字节，服务器回 `426` 时客户端仍判成功，最终哈希一致。
3. 服务器限制单账号 1 会话 → 自动降级单连接，结果正确。
4. 服务器不支持 `REST STOR` → 探测失败，上传降级单连接；下载仍可并行（若支持 `REST RETR`）。
5. 分片中途失败 → 分片重试；仍失败则整体降级，最终结果正确。
6. 并行进度回调单调递增，最终 `(total, total)`。
7. 并行下载失败后目标文件不被替换为半截文件。
8. 并行上传降级重传后不残留空洞文件（最终 `SIZE` 与本地一致，抽样比对首/中/尾）。
9. 服务端 `Rename` 策略下：客户端禁用并行上传，单连接上传并回填 `NEWNAME`；`Reject` + 回调选覆盖时返回 `Failed`。

### 12.4 服务端协议

1. `FEAT` 多行响应可被客户端解析。
2. `REST` + `RETR` / `STOR` 偏移正确；`REST 0` 截断，`REST > 0` 不截断。
3. 每次 `RETR`/`STOR` 后收到 `226`。
4. `MDTM` 格式正确；`MLSD` 格式可被常见客户端解析。
5. `EPSV` 可建立数据连接；`PASV` 仍可用。
6. 同一账号两条控制连接：各自 `CWD` 互不影响，可同时传输不同文件。
7. 覆盖策略：默认覆盖；`Reject` 返回 `550`；`Rename` 返回新名字。
8. 第三方客户端冒烟：`curl` 或 Python `ftplib` 上传/下载/列目录。

### 12.5 性能

- 在本机回环和局域网各跑一次 64MB/256MB 文件：
  - 记录旧实现与新实现的耗时、吞吐。
  - 验收：并行可用时，大文件吞吐明显高于单连接；单连接至少不慢于旧实现。
  - 不断言具体倍数，避免环境噪声导致假失败。
- 内存：并行 4 会话、1MB 缓冲时，峰值内存有界；无缓冲区泄漏。

## 13. 实施顺序

1. **客户端响应层**：多行/中间响应解析、`FEAT`、`TYPE I`、`EPSV`/`PASV` 归一化、`226` 等待。
2. **服务端会话层**：`FTPSession`、每连接 `FTPStream`、命令队列、按会话的路径/数据 socket/端口分配。
3. **服务端协议**：`FEAT`、`REST`（`RETR`+`STOR`）、`MDTM`、`EPSV`、标准 `MLSD`、`226`、覆盖策略。
4. **客户端单连接可靠性**：冲突检测/回调/结果类型、临时文件与原子替换、重试分类、断点续传。
5. **并行与自适应缓冲**：多会话分片、聚合进度、失败降级、缓冲区调整。
6. **测试与文档**：新增测试项目、README 更新（中英文）、性能记录。

每步都应可独立编译；第 4 步完成后再接第 5 步，避免并行掩盖单连接问题。

## 14. 留给实现者的决定

以下问题不影响“做什么”，实现者可以自行决定并在代码/README 中记录：

- 自适应缓冲的具体步进算法（步长、采样窗口、回退阈值）。
- 是否需要 `ABOR`；默认不实现（并行下载靠关闭数据连接结束分片）。
- 被动端口范围的默认值（建议 0 表示系统分配，配置范围时才固定）。
- 测试项目是否加入 `Src/SAEA.Sockets.sln`（建议加入，便于 `dotnet test`）。
- 临时文件清理的时机（建议成功后立即删除；失败按 `KeepPartialOnFailure` 处理）。

## 16. 设计评审问题与结论（2026-11-05）

针对实现前的独立评审，以下 5 个会导致“猜错就写坏”的问题已定论，并已写回第 7、8、9 节。

1. **并行下载的分片边界**：`RETR` 无结束偏移。分片连接读满 `len` 字节后主动关闭；读满即成功，`426` 视为正常提前关闭。服务端对提前关闭回 `426`。这样并行下载只依赖 `REST RETR`，标准 FTP 即可实现。
2. **并行上传的初始化与空洞**：分片 0 先 `REST 0 + STOR` 截断并固定目标名，其余分片随后 `REST <offset> + STOR`。正式传输前用临时探测文件验证 `REST STOR` 与“EOF 之后写入 + 读回一致”。并行上传失败一律回退单连接并从 0 重传，不用远端长度做续传依据，避免空洞被误判为成功。
3. **服务端 `Rename`/`Reject` 与并行、回调次数**：冲突回调每个逻辑文件只调用一次，最终目标名在所有会话和重试间共享。`FEAT` 声明 `SAEA-OVERWRITE=...`；`Rename` 时客户端禁用并行上传并读取 `NEWNAME`，`Reject` 时覆盖请求失败即返回 `Failed`，不静默改名。
4. **续传范围**：同一次调用内始终支持；跨调用续传默认关闭，开启后用确定性 `.part` + `.meta` 校验；并行传输永不跨调用续传。
5. **配置优先级**：`FTPTransferOptions` 显式值 > `ClientConfig` > 默认；`0`/`TimeSpan.Zero` 表示未设置。旧构造函数参数写入 `ClientConfig`/`ServerConfig`，不会被新默认的 64KB 覆盖。

## 15. 风险

- **服务端会话重构风险最高**：现有代码大量依赖按用户名的共享状态；必须先完成会话下沉，再接并行，否则会出现路径串台、数据端口互抢。
- **并行上传依赖非标准扩展**：`REST` + `STOR` 并非所有服务器支持；降级逻辑必须充分测试。
- **TOCTOU**：冲突检测后仍可能被其他客户端抢先写入；自带服务端可通过 `Reject`/`Rename` 策略降低风险，第三方服务器只能尽力。
- **服务器连接数限制**：同一账号多会话可能触发 `421`/`530`/`425`；必须能自动降级且不丢数据。
- **弱网重试放大**：退避和最大次数必须生效，避免把服务器打垮。
- **netstandard2.0 限制**：原子替换、`RandomAccess`、部分 `FileStream` 选项不可用，需要用兼容写法（临时文件 + 删除/移动 + 逐段复制）。