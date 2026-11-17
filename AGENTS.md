# AGENTS.md

## 协作约定（优先于助手默认行为）

- 全程中文；结论先行，再给一句话依据。回复精简但不省略依据与风险。
- 位置以「文件 + 符号/章节」为主锚，`文件:行号` 为辅；编辑后重新核对行号再引用。
- 有上游设计依据时给出处（如 `docs/superpowers/specs/<文件>.md` 的章节）；没有就写「无上游设计依据」，不编造。
- 代码与注释不写「见 PRD §X」「按设计 vX」这类溯源标记，规则直接写完整；跨文件引用用「文件名 + 主题」。
- 严格按任务范围执行，不顺手改无关文件或重构；范围外问题先报告，然后继续原任务。必要的连带改动（接口调用方、README 中英同步）不算扩面。
- 改动代码后不得自行修改项目的包版本（`.csproj` 的 `<Version>`），也不得增删、升级或降级 `PackageReference` 版本；包版本管理由用户处理，除非当次任务明确要求。
- 多文件改动、对外行为变更、不可逆操作或存在多种合理实现时：先给 2–3 个方案 + 权衡，暂停等明确许可再动手；中途失效回到比选，不自行换方案。
- 不执行 `git push`、`reset --hard`、`clean`、改写已有提交，除非当次点名。
- 凭据不写入代码/注释/文档/日志/提交；文档只写位置。
- 未实际运行的构建/测试不得声称通过，报告附关键输出或退出码；环境缺失导致的失败写「环境原因」，只做静态核对写「未运行」。
- 具体执行类任务（落码、代码审查、测试补齐、批量文档同步等）一律交给子代理完成；主会话只做需求澄清、方案比选、任务拆解、委派与验收，不亲自改代码。子代理返回后由主会话复核改动范围与构建/测试证据再下结论，不直接采信其自述。
- 流程型 skill（brainstorming / grilling 等）只让渡「步骤顺序」，不因 skill 放宽范围与证据要求。

## 仓库结构

- 唯一解决方案：`Src/SAEA.Sockets.sln`；`Src/<组件>/` 一个目录一个 NuGet 包。
- 现代 NuGet 库项目统一 `netstandard2.0`，语言版本不超过 C# 8 → 库代码不要用 C# 9+ 语法（record、file-scoped namespace、init 等）；历史 `SAEA.Audio.Net` 为 net48 类库，属例外。
- `Src/*Test/` 是可运行的控制台/窗口演示与自测程序（`SAEA.FTPTest`、`SAEA.Audio.Test`、`SAEA.Mvc.ServiceTest` 为 WinExe），不是单元测试工程。真正的 MSTest 有十一个：`SAEA.Common.Tests`、`SAEA.FileSocket.Tests`、`SAEA.FTP.Tests`、`SAEA.Http.Tests`、`SAEA.MessageSocket.Tests`、`SAEA.MQTT.Tests`、`SAEA.MVC.Tests`、`SAEA.P2P.Tests`、`SAEA.QueueSocket.Tests`、`SAEA.RPC.Tests`、`SAEA.WebSocket.Tests`（均 `net10.0`），且**都不在 sln 里**。
- 依赖链：`SAEA.Common` → `SAEA.Sockets` / `SAEA.Sockets.Pipeline`；`SAEA.Sockets` → Http / WebSocket / MQTT / RPC / RedisSocket / FTP / FileSocket / QueueSocket / DNS / MessageSocket / P2P / Socket5；`Common` + Http + Sockets + WebSocket → MVC。
- `SAEA.Common/Newtonsoft.Json/` 是内嵌第三方源码（MIT），不要把它的 API 当本项目约定去改。
- 少数历史演示工程仍是 .NET Framework 4.6.2/4.8（`SAEA.MVCTest`、`SAEA.FTPTest`、`SAEA.Sockets.TcpTest`、`SAEA.Mvc.ServiceTest` 为 4.6.2，`SAEA.Audio.Net`、`SAEA.Audio.Test` 为 4.8）；它们在 Windows 上可随 sln 构建，缺 targeting pack 时只构建目标库即可。

## 构建与测试

需要 .NET 10 SDK（当前 10.0.403 已实测）；运行 `Src/SAEA.P2PTest` 还需 .NET 8 runtime（其目标框架为 `net8.0`）。

```bash
# 全量构建：0 errors；警告数随增量/全量重建与 SDK 变化（clean rebuild 实测约 3.8k，主体为 CS1591 缺失 XML 注释）
dotnet build Src/SAEA.Sockets.sln -c Debug

# 单元测试：MSTest 工程不在 sln 中，必须单独指定（下列为已实测的五个）
dotnet test Src/SAEA.Common.Tests/SAEA.Common.Tests.csproj -c Release                     # 59 passed
dotnet test Src/SAEA.MQTT.Tests/SAEA.MQTT.Tests.csproj -c Release                         # 153 passed
dotnet test Src/SAEA.FTP.Tests/SAEA.FTP.Tests.csproj -c Release                           # 13 passed
dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release                           # 243 passed / 2 skipped
dotnet test Src/SAEA.MVC.Tests/SAEA.MVC.Tests.csproj -c Release                           # 46 passed

# 跑单个用例：--filter "FullyQualifiedName~MemoryPoolManagerTests"
```

- 单测不依赖外部服务：MQTT 用 `TestInfrastructure` 的进程内 server + 随机端口，FTP 在测试内启动本地临时 server。
- `Src/SAEA.RedisSocketTest` 是交互式演示，需要本地 Redis（默认 `127.0.0.1:6379`），不能当单测跑。
- 自测入口（参数见各自 `Program.cs`）：
  - `dotnet run --project Src/SAEA.P2PTest -c Release -- --all`；另有 `--tls`、`--tls-stress`、`--bench-tls`、`--bench-iocp`。
  - `dotnet run --project Src/SAEA.QueueSocketTest -c Release -- --functional`；另有 `--all`、`--bench-queue`。
  - 不带参数会进入交互式菜单，自动化场景必须显式传参。

## 编码与 API 要点

- 收发主路径是零拷贝 Span/Memory + 内存池：`OnServerReceiveSpan` / `OnClientReceiveSpan` 的 `data` 仅在回调期间有效，跨回调或异步保存必须复制。
- 从池里借的 buffer 必须 `try/finally` 归还：`MemoryPoolManager.Rent` / `Return`（`SAEA.Common/Caching/MemoryPoolManager.cs`）。热路径优先复用池，避免 `new byte[]`。
- TLS 由 BouncyCastle 实现，不依赖 `SslStream`；统一入口是 `SocketOptionBuilder.WithSsl(...)`，事件模式与流模式都可用。
- 两种收发模式：`.UsePacket()`（SocketAsyncEventArgs，UDP 仅此模式）与 `.UseStream()`（Pipelines，需要 `GetStream()` / TLS 流式读写时用）。


## 代码风格与注释规范

- 基线：遵循 Microsoft C# 编码约定与 .NET 库设计准则，落地时以**同目录既有文件**的实际写法为第一参照；两者冲突时先跟本仓库现况，不引入新的风格流派。
- 语法上限：库项目以 `LangVersion 8.0` + `netstandard2.0` 为上限（`SAEA.DNS`、`SAEA.P2P` 未显式设置，默认 C# 7.3 亦符合），禁用 C# 9+ 语法（record、file-scoped namespace、init、目标类型 `new`、全局 using 等）；可空引用类型未启用，不为局部改动单独开启。测试工程是 `net10.0`，按各自 `LangVersion` 放宽。
- 命名与格式：类型/方法/属性/常量/枚举成员 PascalCase，私有字段 `_camelCase`，参数与局部变量 camelCase，接口 `I` 前缀，异步方法 `Async` 后缀；事件用 `Xxx` 命名时触发方法写 `protected virtual OnXxx(...)`，事件用 `OnXxx` 命名时触发方法写 `RaiseOnXxx(...)`，两者不可同名（C# 不允许同类型事件与方法同名）；4 空格缩进、Allman 大括号、成员间空一行；单行 `if` 可省大括号但同一方法内保持一致。
- 文件组织：一个文件一个公开类型，文件名与类型名一致；`using` 在命名空间外；成员顺序沿用同文件现况（常量 → 字段 → 构造 → 属性 → 方法 → 事件/嵌套类型）。`#region` 不是硬约定，只在文件已有分区时沿用，新文件不强制添加。
- 文件头 banner：本项目手写的 `.cs` 源码（含新建文件）必须在文件最顶部带统一 banner，格式与下方模板逐行一致，不得省略。范围排除 `obj/`、`bin/`、`Src/SAEA.Common/Newtonsoft.Json/`、`Src/SAEA.Audio.Net/Base/NAudio/` 等内嵌第三方源码；`Src/*Test/` 等测试与示例工程的存量文件不强制回填，其新增文件仍遵循本规则。字段填写：`CLR版本` 写项目实际目标框架（如 `netstandard2.0`、`net10.0`）；`机器名称`、`当前的用户域` 按填写人环境填写，会进入版本库；`命名空间`、`文件名`、`描述` 按实际内容；`版本号` 写 `v` + 该 `.csproj` 的 `<Version>` 数值（如 `v26.11.5.1`），无 `<Version>` 的测试/演示工程写其目标框架；`唯一标识` 每个文件新生成 GUID，禁止照抄示例或复用他文件的 GUID；`创建人`、`电子邮箱` 沿用项目默认值。既有 banner 保持原值，仅在该文件被修改时把 `修改标记` 段整体覆盖为本次修改信息（不追加历史）。
- XML 注释：体例参照 `Src/SAEA.Common/IPHelper.cs` —— 类型和每个 `public` 方法都写 `/// <summary>`，一句中文短语说清职责（如「获取正在使用中的端口」）；有参数写 `<param name="...">`、有返回值写 `<returns>`，没有补充说明就留空标签，有就写中文（如 `指定IP地址,默认全部地址`）；不堆长篇 `<para>`。新增或修改的 `public`/`protected` 成员按此补齐，签名或行为变了同步改注释，注释与实现不符按缺陷处理。
- 行内注释：全中文，只写「为什么」与不明显的约束（线程安全、`Span`/`Memory` 生命周期、池化 buffer 必须归还、单位、边界、字节序）；不复述代码，不写溯源标记，不保留注释掉的死代码，变更原因进提交信息。`Src/SAEA.Common/Newtonsoft.Json/` 等内嵌源码不改风格、不格式化。
- 测试与自检：MSTest 用 `[TestClass]`/`[TestMethod]`，方法名 `被测对象_场景_期望`（如 `GetTier_SmallBuffer_ReturnsSmall`），一个用例聚焦一个行为，注释同样用中文；库项目提交前确认无 C# 9+ 语法（测试工程按各自 `LangVersion`）、公开 API 注释齐全、命名与注释密度同目录一致，并按「构建与测试」跑通构建。

### 文件头 banner 模板

下列占位符按实际内容替换；ASCII 图形与分隔线保持原样，不要重排或删除空行。

```csharp
/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： <目标框架，如 netstandard2.0>
*机器名称：<填写人机器名>
*公司名称：yswenli
*命名空间：<命名空间>
*文件名： <文件名，不含扩展名>
*版本号： v<组件 csproj 的 Version>
*唯一标识：<新 GUID，不得复用>
*当前的用户域：<填写人用户域>
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：<yyyy/M/d HH:mm:ss>
*描述：<文件职责，一句中文>
*
*=====================================================================
*修改标记
*修改时间：<yyyy/M/d HH:mm:ss>
*修改人： <实际修改者>
*版本号： v<组件 csproj 的 Version>
*描述：<本次修改内容>
*
*****************************************************************************/
```

## 代码审查

- 审查范围：一次审查覆盖「代码 + 注释 + 文档 + 测试」四块，不能只看实现逻辑；改了代码却没同步注释或 README，按未完成处理。
- 代码本身：行为与边界（空值、长度、超时、取消）、异常路径、并发与线程安全、资源与池化 buffer 归还（`try/finally`）、C# 8 语法上限（测试工程按各自 `LangVersion`）、命名与格式是否符合「代码风格与注释规范」。
- 注释样式：检查文件头 banner 是否存在、字段是否与 `.csproj` 一致（`CLR版本`、`命名空间`、`文件名`、`版本号`），`唯一标识` 是否为该文件专属 GUID，`修改标记` 是否为本次整体覆盖而非追加历史；排除目录与 `Src/*Test/` 存量文件不要求回填。
- 注释内容：`public`/`protected` 成员的 XML 注释按 `Src/SAEA.Common/IPHelper.cs` 体例齐全（`summary`/`param`/`returns`），并与签名、行为、单位、边界一致；行内注释只解释「为什么」，不得有溯源标记、注释掉的死代码或与实现矛盾的描述；过期注释按缺陷计，不能只当成风格问题。
- 文档与 README：对外行为或公开 API 变化时，根目录与对应组件的 `README.md` + `README.en.md` 必须同步且中英一致，示例代码、参数说明、能力清单要与实现对齐；受影响的设计/计划文档（`docs/superpowers/`）一并更新；不得留下失效链接或已删除 API 的示例。
- 测试：新增或修复的行为要有对应 MSTest 用例，方法名符合 `被测对象_场景_期望`，用例不依赖外部服务；确实无法补用例时在报告中说明原因与风险。
- 版本与依赖：拦下擅自修改的 `.csproj` `<Version>`、`PackageReference` 增删或升降级，除非当次任务明确要求。
- 报告方式：按「阻断 / 应修 / 建议」分级，逐条给「文件 + 符号/章节」，可复现的附命令与输出；范围外问题只列不改。

## 文档与发布

- 根目录与每个组件都维护 `README.md` + `README.en.md` 双份，改一份必须同步另一份（`SAEA.Audio.Net` 目前只有 `README.md`，属历史例外，改动时补英文版）。
- 新设计与计划放 `docs/superpowers/specs/`、`docs/superpowers/plans/`，文件名以 `YYYY-MM-DD-` 开头。`docs/plans/` 已被 `.gitignore` 忽略，新文件不会进版本库。
- 仓库无 CI / `.editorconfig` / `global.json`，质量把关靠上述手动构建 + 单测工程。