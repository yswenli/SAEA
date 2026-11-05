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

## 文档与发布

- 根目录与每个组件都维护 `README.md` + `README.en.md` 双份，改一份必须同步另一份。
- 新设计与计划放 `docs/superpowers/specs/`、`docs/superpowers/plans/`，文件名以 `YYYY-MM-DD-` 开头。`docs/plans/` 已被 `.gitignore` 忽略，新文件不会进版本库。
- 仓库无 CI / `.editorconfig` / `global.json`，质量把关靠上述手动构建 + 三个单测工程。