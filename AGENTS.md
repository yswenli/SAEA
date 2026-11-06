# AGENTS.md

## 协作约定（优先于助手默认行为）

- 全程中文；结论先行，再给一句话依据。回复精简但不省略依据与风险。
- 位置以「文件 + 符号/章节」为主锚，`文件:行号` 为辅；编辑后重新核对行号再引用。
- 有上游设计依据时给出处（如 `docs/superpowers/specs/<文件>.md` 的章节）；没有就写「无上游设计依据」，不编造。
- 代码与注释不写「见 PRD §X」「按设计 vX」这类溯源标记，规则直接写完整；跨文件引用用「文件名 + 主题」。
- 严格按任务范围执行，不顺手改无关文件或重构；范围外问题先报告，然后继续原任务。必要的连带改动（接口调用方、README 中英同步）不算扩面。
- 多文件改动、对外行为变更、不可逆操作或存在多种合理实现时：先给 2–3 个方案 + 权衡，暂停等明确许可再动手；中途失效回到比选，不自行换方案。
- 不执行 `git push`、`reset --hard`、`clean`、改写已有提交，除非当次点名。
- 凭据不写入代码/注释/文档/日志/提交；文档只写位置。
- 未实际运行的构建/测试不得声称通过，报告附关键输出或退出码；环境缺失导致的失败写「环境原因」，只做静态核对写「未运行」。
- 流程型 skill（brainstorming / grilling 等）只让渡「步骤顺序」，不因 skill 放宽范围与证据要求。

## 仓库结构

- 唯一解决方案：`Src/SAEA.Sockets.sln`；`Src/<组件>/` 一个目录一个 NuGet 包。
- 库项目统一 `netstandard2.0`，语言版本 C# 8 或更低 → 库代码不要用 C# 9+ 语法（record、file-scoped namespace、init 等）。
- `Src/*Test/` 是可运行的控制台演示/自测程序，不是单元测试工程。真正的 MSTest 只有三个：`SAEA.Common.Tests`、`SAEA.MQTT.Tests`、`SAEA.FTP.Tests`（均 `net10.0`），且**都不在 sln 里**。
- 依赖链：`SAEA.Common` → `SAEA.Sockets` → Http / WebSocket / MQTT / RPC / Redis / FTP / FileSocket / QueueSocket / DNS / MessageSocket / P2P → MVC。
- `SAEA.Common/Newtonsoft.Json/` 是内嵌第三方源码（MIT），不要把它的 API 当本项目约定去改。
- 少数历史演示工程仍是 .NET Framework 4.6.2/4.8（如 `SAEA.MVCTest`、`SAEA.FTPTest`、`SAEA.Audio.*`）；它们在 Windows 上可随 sln 构建，缺 targeting pack 时只构建目标库即可。

## 构建与测试

需要 .NET 10 SDK（当前 10.0.403 已实测）。

```bash
# 全量构建：0 errors（当前 93 warnings，多为内嵌 Newtonsoft.Json 的 CS3021）
dotnet build Src/SAEA.Sockets.sln -c Debug

# 单元测试：三个 MSTest 工程不在 sln 中，必须单独指定
dotnet test Src/SAEA.Common.Tests/SAEA.Common.Tests.csproj -c Release   # 59 passed
dotnet test Src/SAEA.MQTT.Tests/SAEA.MQTT.Tests.csproj -c Release       # 153 passed
dotnet test Src/SAEA.FTP.Tests/SAEA.FTP.Tests.csproj -c Release         # 13 passed

# 跑单个用例：--filter "FullyQualifiedName~MemoryPoolManagerTests"
```

- 单测不依赖外部服务：MQTT 用 `TestInfrastructure` 的进程内 server + 随机端口，FTP 在测试内启动本地临时 server。
- `Src/SAEA.RedisSocketTest` 是交互式演示，需要本地 Redis（默认 `127.0.0.1:6379`），不能当单测跑。
- 控制台自测入口（参数见各自 `Program.cs`）：
  - `dotnet run --project Src/SAEA.P2PTest -c Release -- --all`；另有 `--tls`、`--tls-stress`、`--bench-tls`、`--bench-iocp`。
  - `dotnet run --project Src/SAEA.QueueSocketTest -c Release -- --functional`；另有 `--all`、`--bench-queue`。
  - 不带参数会进入交互式菜单，自动化场景必须显式传参。

## 编码与 API 要点

- 收发主路径是零拷贝 Span/Memory + 内存池：`OnServerReceiveSpan` / `OnClientReceiveSpan` 的 `data` 仅在回调期间有效，跨回调或异步保存必须复制。
- 从池里借的 buffer 必须 `try/finally` 归还：`MemoryPoolManager.Rent` / `Return`（`SAEA.Common/Caching/MemoryPoolManager.cs`）。热路径优先复用池，避免 `new byte[]`。
- TLS 由 BouncyCastle 实现，不依赖 `SslStream`；统一入口是 `SocketOptionBuilder.WithSsl(...)`，事件模式与流模式都可用。
- 两种收发模式：`.UseIocp()`（SocketAsyncEventArgs，UDP 仅此模式）与 `.UseStream()`（Pipelines，需要 `GetStream()` / TLS 流式读写时用）。`UseIocp` 是历史命名，不是 Windows 限定。
- 包版本在各 `.csproj` 的 `<Version>`（当前多为 `26.11.5.1`，MQTT 为 `26.11.6.1`）；`GeneratePackageOnBuild=true`，构建即产出 nupkg。

## 代码风格与注释规范

- 基线：遵循 Microsoft C# 编码约定与 .NET 库设计准则，落地时以**同目录既有文件**的实际写法为第一参照；两者冲突时先跟本仓库现况，不引入新的风格流派。
- 语法上限：库项目 `LangVersion 8.0` + `netstandard2.0`，禁用 C# 9+ 语法（record、file-scoped namespace、init、目标类型 `new`、全局 using 等）；可空引用类型未启用，不为局部改动单独开启。测试工程是 `net10.0`，按各自 `LangVersion` 放宽。
- 命名与格式：类型/方法/属性/常量/枚举成员 PascalCase，私有字段 `_camelCase`，参数与局部变量 camelCase，接口 `I` 前缀，异步方法 `Async` 后缀，事件 `OnXxx` 配 `protected virtual OnXxx(...)` 触发方法；4 空格缩进、Allman 大括号、成员间空一行；单行 `if` 可省大括号但同一方法内保持一致。
- 文件组织：一个文件一个公开类型，文件名与类型名一致；`using` 在命名空间外；成员顺序沿用同文件现况（常量 → 字段 → 构造 → 属性 → 方法 → 事件/嵌套类型）。`#region` 不是硬约定，只在文件已有分区时沿用，新文件不强制添加。
- 文件头 banner：所有源码文件（`*.cs`）都必须在文件最顶部带统一 banner，新建文件同样必须补齐，不得省略。格式与 `Src/SAEA.Common/IPHelper.cs` 文件头完全一致（见下方模板），并按实际内容填写 `CLR版本`、`机器名称`、`公司名称`、`命名空间`、`文件名`、`版本号`（与该 `.csproj` 的 `<Version>` 一致）、`唯一标识`（新 GUID）、`当前的用户域`、`创建人`、`电子邮箱`、`创建时间`、`描述`；`修改标记` 段在修改文件时更新修改时间、修改人、版本号、描述。既有文件已带 banner 的保持原值，只在修改该文件时更新 `修改标记` 段。

```csharp
/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Common
*文件名： IPHelper
*版本号： v26.4.23.1
*唯一标识：2f89dd56-b6ad-4520-bf01-1c001cb0e184
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/13 17:01:50
*描述：IPHelper接口
*
*=====================================================================
*修改标记
*修改时间：2019/11/13 17:01:50
*修改人： yswenli
*版本号： v26.4.23.1
*描述：IPHelper接口
*
*****************************************************************************/
```
- XML 注释：体例参照 `Src/SAEA.Common/IPHelper.cs` —— 类型和每个 `public` 方法都写 `/// <summary>`，一句中文短语说清职责（如「获取正在使用中的端口」）；方法一律带 `<param name="...">` 与 `<returns>`，没有补充说明就留空标签，有就写中文（如 `指定IP地址,默认全部地址`）；不堆长篇 `<para>`。新增或修改的 `public`/`protected` 成员按此补齐，签名或行为变了同步改注释，注释与实现不符按缺陷处理。
- 行内注释：全中文，只写「为什么」与不明显的约束（线程安全、`Span`/`Memory` 生命周期、池化 buffer 必须归还、单位、边界、字节序）；不复述代码，不写溯源标记，不保留注释掉的死代码，变更原因进提交信息。`Src/SAEA.Common/Newtonsoft.Json/` 等内嵌源码不改风格、不格式化。
- 测试与自检：MSTest 用 `[TestClass]`/`[TestMethod]`，方法名 `被测对象_场景_期望`（如 `GetTier_SmallBuffer_ReturnsSmall`），一个用例聚焦一个行为，注释同样用中文；提交前确认无 C# 9+ 语法、公开 API 注释齐全、命名与注释密度同目录一致，并按「构建与测试」跑通构建。

## 文档与发布

- 根目录与每个组件都维护 `README.md` + `README.en.md` 双份，改一份必须同步另一份。
- 新设计与计划放 `docs/superpowers/specs/`、`docs/superpowers/plans/`，文件名以 `YYYY-MM-DD-` 开头。`docs/plans/` 已被 `.gitignore` 忽略，新文件不会进版本库。
- 仓库无 CI / `.editorconfig` / `global.json`，质量把关靠上述手动构建 + 三个单测工程。