# SslTlsTool openssl 兼容子命令实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在不改变现有 `--self-signed`/`--ca`/`--verify` 行为的前提下，为 SslTlsTool 增加 8 个 openssl 风格子命令，覆盖 PEM、DER、PKCS#7、PKCS#12、PKCS#8、PKCS#1 的生成与互转。

**Architecture:** 兼容层独立放在 `Src/SAEA.Sockets.SslTlsTool/OpenSsl/`，`Program.Main` 只增加一处分派；每个子命令一个处理类，编解码按格式拆成 codec；全部验证由一个 PowerShell 7 脚本 `tests/verify-openssl-compat.ps1` 承担，不新增测试工程。

**Tech Stack:** .NET 10、BouncyCastle.Cryptography 2.6.2（经 `SAEA.Sockets` 传递引用）、PowerShell 7。上游依据：`docs/superpowers/specs/2026-11-13-ssl-tls-tool-openssl-compat-design.md` §7/§8/§12/§13。

## 验证总则

- 唯一自动化证据是 `Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1`（pwsh 7）。每个任务把本任务用例加进脚本并跑到 PASS，末行必须 `N passed, 0 failed`，否则任务未完成。
- 本机无 openssl，脚本不包含与外部 openssl 的差分用例，全部用例只断言本工具自身的可观察行为（退出码、stdout/stderr、文件字节与结构）。`SAEA_OPENSSL` 不参与脚本逻辑，README 只把它写成「本机装了 openssl 时可用同一组参数手工对拍」的建议。
- 不新增测试工程、不新增 `PackageReference`；不改任何 `.csproj` 的 `<Version>`；提交前跑 `dotnet build Src/SAEA.Sockets.sln -c Debug`（0 errors），提交说明附脚本末行输出。兼容分支退出码只用 0/1，遗留分支的 0/1/2/3 语义不动。

## 文件结构

落点 `Src/SAEA.Sockets.SslTlsTool/OpenSsl/`；`OpenSsl/` 根下的类型命名空间为 `SAEA.Sockets.SslTlsTool.OpenSsl`，`OpenSsl/Commands/` 下的类型命名空间为 `SAEA.Sockets.SslTlsTool.OpenSsl.Commands`。对应设计 §12：

- 分派与参数（5）：`OpenSslCommand`、`OpenSslUsage`、`OptionReader`、`PasswordResolver`、`OpenSslException`
- 编解码（6）：`PemCodec`、`KeyCodec`、`CertificateCodec`、`Pkcs12Codec`、`Pkcs7Codec`、`CsrCodec`；解析与辅助（4）：`SubjectNameParser`、`ExtensionSpec`、`MiniConfigReader`、`SerialFile`
- 子命令处理（`Commands/` 是 §12 的落地细化，分派仍在 `OpenSslCommand`）：`Commands/{Req,X509,Pkcs8,Pkey,Pkcs12,Crl2Pkcs7,Pkcs7}Command.cs`

验证脚本：`Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1`。`OpenSsl/` 下新建 `.cs` 一律带 AGENTS.md 统一 banner，`CLR版本`/`版本号`（含 `修改标记` 段）写 `net10.0`。

本计划里的代码片段只保留与该文件相关的 `using`，实现时按编译器提示补齐其余命名空间（工具工程 `ImplicitUsings` 已开，仍缺 `System.Text`、`System.Text.RegularExpressions`、`System.Globalization`、`System.Security.Cryptography` 以及 `Org.BouncyCastle.*` 具体子命名空间）；补齐 `using` 不算偏离计划，改签名或改行为才算。

### Task 1: 兼容层骨架与分派

**Files:**

- 新建 `Src/SAEA.Sockets.SslTlsTool/OpenSsl/{OpenSslException,OptionReader,OpenSslUsage,OpenSslCommand}.cs`
- 修改 `Src/SAEA.Sockets.SslTlsTool/Program.cs`（`Main` 与 `RunInteractive` 各加一处分派）
- 新建 `Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1`；本任务不建 `OpenSsl/Commands/`

- [ ] **Step 1: 新建 `OpenSslException.cs`**

文件头按 AGENTS.md banner 模板补齐（`唯一标识` 用 `[guid]::NewGuid().ToString()` 生成，`CLR版本`/`版本号` 写 `net10.0`）。

```csharp
namespace SAEA.Sockets.SslTlsTool.OpenSsl;

/// <summary>兼容子命令的统一错误载体，Message 即写 stderr 的整句。</summary>
internal sealed class OpenSslException : Exception
{
    public OpenSslException(string message)
        : base(message)
    {
    }
}
```

- [ ] **Step 2: 新建 `OptionReader.cs`**

`-x value` 为取值型，开关型由调用方用 `Has` 判断；值缺失由 `GetRequired`/`GetInt`/`GetAll` 报 `参数 "-x" 缺少取值。`；`allowedOptions` 之外立即报错；只有 `-addext` 与 `-certfile` 可重复；非 `-` 开头项进 `Positional`。开关型后面紧跟位置参数时会被当作它的取值，调用方不要这样写。

```csharp
using System.Globalization;

namespace SAEA.Sockets.SslTlsTool.OpenSsl;

/// <summary>单横线参数读取：取值/开关、重复聚合、缺值与未知参数报错。</summary>
internal sealed class OptionReader
{
    private static readonly HashSet<string> Repeatable = new(StringComparer.Ordinal) { "-addext", "-certfile" };
    private readonly Dictionary<string, List<string?>> _options = new(StringComparer.Ordinal);
    private readonly List<string> _positional = new();

    public OptionReader(string command, string[] args, string[] allowedOptions)
    {
        var allowed = new HashSet<string>(allowedOptions, StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (!IsOption(token))
            {
                _positional.Add(token);
                continue;
            }
            if (!allowed.Contains(token))
                throw new OpenSslException($"error: 不支持的参数 \"{token}\"；支持的参数见 -help。");
            string? value = null;
            if (i + 1 < args.Length && !IsOption(args[i + 1]))
                value = args[++i];
            if (!_options.TryGetValue(token, out var values))
                _options[token] = values = new List<string?>();
            else if (!Repeatable.Contains(token))
                throw new OpenSslException($"error: 参数 \"{token}\" 重复。");
            values.Add(value);
        }
    }

    public IReadOnlyList<string> Positional => _positional;
    public bool Has(string name) => _options.ContainsKey(name);
    public string? Get(string name) => !_options.TryGetValue(name, out var values)
        ? null
        // 给了参数却没跟取值（如末尾只有 -passin）时按缺值报错，不再退化成“未给”。
        : values[0] ?? throw new OpenSslException($"error: 参数 \"{name}\" 缺少取值。");
    public string GetRequired(string name) => Get(name) ?? throw new OpenSslException($"error: 参数 \"{name}\" 缺少取值。");
    public int GetInt(string name, int defaultValue) => !Has(name) ? defaultValue : ParseInt(name, GetRequired(name));
    public IReadOnlyList<string> GetAll(string name) => !_options.TryGetValue(name, out var values)
        ? Array.Empty<string>()
        : values.Select(value => value ?? throw new OpenSslException($"error: 参数 \"{name}\" 缺少取值。")).ToList();

    private static int ParseInt(string name, string text)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            throw new OpenSslException($"error: 参数 \"{name}\" 需要整数，实际为 \"{text}\"。");
        return value;
    }

    // 单横线且长度大于 1 才算参数；"-" 与空串按位置参数处理。
    private static bool IsOption(string token) => token.Length > 1 && token[0] == '-';
}
```

- [ ] **Step 3: 新建 `OpenSslUsage.cs`**

`For` 只实现 `version`，其余分支 Task 2 起逐个替换成真实参数清单；`Pending` 是过渡占位，保证未实现子命令的 `-help` 仍退出 0（设计 §7 要求 `-help` 不执行任何读写）。

```csharp
namespace SAEA.Sockets.SslTlsTool.OpenSsl;

/// <summary>兼容子命令的帮助文本。</summary>
internal static class OpenSslUsage
{
    public static string CommandList() => "req、x509、pkcs12、pkcs8、pkey、crl2pkcs7、pkcs7、version";

    public static string For(string command)
    {
        switch (command)
        {
            case "version":
                return "用法：SAEA.Sockets.SslTlsTool version\n\n输出本工具版本，并声明与 openssl version 不兼容。";
            default:
                // 过渡实现：Task 2 起按子命令逐个替换为真实参数清单。
                throw new OpenSslException($"usage: 未实现的子命令 {command}。");
        }
    }

    public static string Pending(string command) =>
        $"用法：SAEA.Sockets.SslTlsTool {command} [选项]\n"
        + "该子命令尚未实现，当前只接受 -help；参数清单（-in、-inform、-out、-outform 等）随实现补齐。\n"
        + $"支持的子命令：{CommandList()}。";
}
```

- [ ] **Step 4: 新建 `OpenSslCommand.cs`**

`-help` 先短路；`version` 输出 `SAEA.Sockets.SslTlsTool <版本>（openssl 兼容子集）`，版本取程序集版本、为空写 `dev`；其余子命令 Task 2 起逐个替换成 `Commands.*Command.Run(tail, interactive)`。

```csharp
namespace SAEA.Sockets.SslTlsTool.OpenSsl;

/// <summary>openssl 兼容子命令的分派入口。</summary>
internal static class OpenSslCommand
{
    private static readonly HashSet<string> Commands = new(StringComparer.Ordinal)
    {
        "req", "x509", "pkcs12", "pkcs8", "pkey", "crl2pkcs7", "pkcs7", "version",
    };

    /// <summary>首 token 是否交给兼容层：空串或 - 开头的选项仍走遗留分支。</summary>
    public static bool IsCompat(string token) => token.Length > 0 && token[0] != '-';

    public static bool IsCommand(string token) => Commands.Contains(token);

    public static int Run(string[] args, bool interactive)
    {
        var command = args[0];
        try
        {
            if (!IsCommand(command))
                throw new OpenSslException($"error: 不支持的子命令 \"{command}\"；支持：{OpenSslUsage.CommandList()}。");
            var tail = args.Length > 1 ? args[1..] : Array.Empty<string>();
            foreach (var token in tail)
            {
                if (!string.Equals(token, "-help", StringComparison.Ordinal))
                    continue;
                // -help 短路：不读文件、不校验其它参数，帮助只写 stdout。
                string text;
                try { text = OpenSslUsage.For(command); }
                catch (OpenSslException) { text = OpenSslUsage.Pending(command); }
                Console.Out.WriteLine(text);
                return 0;
            }
            if (command == "version")
            {
                // -help 已在上面短路；version 只认 -help，其余参数显式报错，避免脚本以为参数生效。
                if (args.Length > 1)
                    throw new OpenSslException("version: 不接受参数；只支持 -help。");
                var version = typeof(OpenSslCommand).Assembly.GetName().Version?.ToString() ?? "dev";
                Console.Out.WriteLine($"SAEA.Sockets.SslTlsTool {version}（openssl 兼容子集）");
                return 0;
            }
            // 过渡实现：Task 2 起逐个替换为 Commands.*Command.Run(tail, interactive)。
            throw new OpenSslException($"error: {command} 尚未实现。");
        }
        catch (OpenSslException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("error: " + ex.Message);
            return 1;
        }
    }
}
```

- [ ] **Step 5: 改 `Program.cs` 增加分派**

`using` 区加 `using SAEA.Sockets.SslTlsTool.OpenSsl;`。`Main` 的 `try` 块改前只有 `return Run(args);`。兼容分支必须放在 `try/finally` **之前**：`SAEA.Common` 的 `ConsoleHelper` 静态构造会经 `DateTimeHelper` 往 **stdout** 打版权横幅，而 `finally` 里的 `ConsoleHelper.Flush()` 正好会触发它；绕开 `ConsoleHelper` 才能保证 `-outform DER` 无 `-out` 时 stdout 只有二进制。改后：

```csharp
        // 兼容分支只用 Console.Out/Console.Error，不经过 ConsoleHelper，避免 SAEA.Common 的启动横幅污染 stdout。
        if (args is { Length: > 0 } && OpenSslCommand.IsCompat(args[0]))
            return OpenSslCommand.Run(args, interactive: false);

        try
        {
            return Run(args);
        }
```

交互模式在 `RunInteractive` 的 `var exitCode = Execute(tokens, true);` 之前加：

```csharp
            if (tokens.Length > 0 && OpenSslCommand.IsCompat(tokens[0]))
            {
                OpenSslCommand.Run(tokens, interactive: true);
                continue;
            }
```
入口谓词用 `IsCompat` 而非 `IsCommand`：否则未知子命令 `foo` 落回遗留分支报「未知参数」，拿不到 `不支持的子命令` 文案，也不符合设计 §7。

- [ ] **Step 6: 新建 `tests/verify-openssl-compat.ps1`**

本任务 3 条用例；`-help` 用例断言的 `-inform` 来自 `OpenSslUsage.Pending`，Task 2 换成真实清单后该断言继续成立。第 1 条同时断言 stdout 不含 `yswenli` 版权横幅：`SAEA.Common/DateTimeHelper` 的静态构造会往 stdout 打横幅，兼容分支只要经过 `ConsoleHelper` 就会污染数据流。

```powershell
param([string]$ToolPath)
if ($PSVersionTable.PSVersion.Major -lt 7) { [Console]::Error.WriteLine('需要 PowerShell 7（pwsh）。'); exit 2 }
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..'
$script:passed = 0; $script:failed = 0

function Invoke-Tool {
    param([string[]]$Arguments)
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'   # 采集 stderr 时不能中断脚本
    try {
        $raw = if ($ToolPath) { & $ToolPath @Arguments 2>&1 } else { & dotnet run --project $project -c Release -- @Arguments 2>&1 }
        $code = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $previous }
    $out = [System.Collections.Generic.List[string]]::new(); $err = [System.Collections.Generic.List[string]]::new()
    foreach ($item in $raw) {
        if ($item -is [System.Management.Automation.ErrorRecord]) { $err.Add($item.ToString()) } else { $out.Add([string]$item) }
    }
    [pscustomobject]@{ ExitCode = $code; StdOut = ($out -join "`n"); StdErr = ($err -join "`n") }
}
function Invoke-Case {
    param([string]$Name, [scriptblock]$Body)
    try {
        if ((& $Body) -eq $true) { $script:passed++; Write-Host "PASS $Name" } else { $script:failed++; Write-Host "FAIL $Name：断言未通过" }
    }
    catch { $script:failed++; Write-Host "FAIL $Name：$($_.Exception.Message)" }
}
$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ('saea-ssltls-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tmp -Force | Out-Null
try {
    Invoke-Case -Name 'version 输出兼容子集标记' -Body {
        $r = Invoke-Tool -Arguments @('version'); $r.ExitCode -eq 0 -and $r.StdOut -like '*兼容子集*' -and $r.StdOut -notlike '*yswenli*'
    }
    Invoke-Case -Name '未知子命令报错' -Body {
        $r = Invoke-Tool -Arguments @('foo'); $r.ExitCode -eq 1 -and $r.StdErr -like '*不支持的子命令*'
    }
    Invoke-Case -Name 'x509 -help 退出 0 且列出 -inform' -Body {
        $r = Invoke-Tool -Arguments @('x509', '-help'); $r.ExitCode -eq 0 -and $r.StdOut -like '*-inform*'
    }
}
finally { Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue }
'{0} passed, {1} failed' -f $script:passed, $script:failed
if ($script:failed -gt 0) { exit 1 }
exit 0
```

- [ ] **Step 7: 构建并跑脚本**

```bash
dotnet build Src/SAEA.Sockets.sln -c Debug
pwsh Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1
```

期望构建 0 errors；脚本末行 `3 passed, 0 failed`、退出码 0。

- [ ] **Step 8: 提交**

```bash
git add Src/SAEA.Sockets.SslTlsTool/OpenSsl Src/SAEA.Sockets.SslTlsTool/Program.cs Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1
git commit -m "feat(ssltls): 增加 openssl 兼容子命令骨架与验证脚本"
```

### Task 2: x509 读取与格式转换

**Files:**

- 新建 `Src/SAEA.Sockets.SslTlsTool/OpenSsl/PemCodec.cs`、`OpenSsl/CertificateCodec.cs`、`OpenSsl/Commands/X509Command.cs`
- 修改 `OpenSsl/OpenSslUsage.cs`（补 `x509` 帮助）、`OpenSsl/OpenSslCommand.cs`（把过渡异常换成 `X509Command.Run`）、`tests/verify-openssl-compat.ps1`（追加 6 条用例）；本任务只做读取/打印/PEM-DER 互转，`-req` 留到 Task 5

- [ ] **Step 1: 脚本追加 x509 用例（先红）**

在 `try {` 块内、现有三条用例之后插入。源证书用遗留 `--self-signed` 生成，不依赖 openssl：

```powershell
    $src = Join-Path $tmp 'src'; $pem = Join-Path $src 'localhost.crt'; $der = Join-Path $tmp 'cert.der'; $back = Join-Path $tmp 'cert.pem'
    $null = Invoke-Tool -Arguments @('--self-signed', '--out', $src)
    Invoke-Case -Name 'x509 PEM→DER→PEM 往返' -Body {
        $a = Invoke-Tool -Arguments @('x509', '-in', $pem, '-outform', 'DER', '-out', $der)
        $b = Invoke-Tool -Arguments @('x509', '-in', $der, '-inform', 'DER', '-out', $back)
        $a.ExitCode -eq 0 -and $b.ExitCode -eq 0 -and
            ([System.Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPemFile($pem).Thumbprint -eq
             [System.Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPemFile($back).Thumbprint)
    }
    Invoke-Case -Name 'x509 -subject 输出 oneline 姓名' -Body {
        $r = Invoke-Tool -Arguments @('x509', '-in', $pem, '-subject', '-noout'); $r.ExitCode -eq 0 -and $r.StdOut -like '*CN =*'
    }
    Invoke-Case -Name 'x509 -dates 输出 notBefore' -Body {
        $r = Invoke-Tool -Arguments @('x509', '-in', $pem, '-dates', '-noout'); $r.ExitCode -eq 0 -and $r.StdOut -like '*notBefore=*'
    }
    Invoke-Case -Name 'x509 -serial 输出十进制转为大写十六进制' -Body {
        $r = Invoke-Tool -Arguments @('x509', '-in', $pem, '-serial', '-noout'); $r.ExitCode -eq 0 -and $r.StdOut -like '*serial=*'
    }
    Invoke-Case -Name 'x509 -fingerprint -sha256 格式' -Body {
        $r = Invoke-Tool -Arguments @('x509', '-in', $pem, '-fingerprint', '-sha256', '-noout'); $r.ExitCode -eq 0 -and $r.StdOut -match '(?m)^SHA256 Fingerprint=([0-9A-F]{2}:){31}[0-9A-F]{2}$'
    }
    Invoke-Case -Name 'x509 -text 报暂不支持' -Body {
        $r = Invoke-Tool -Arguments @('x509', '-in', $pem, '-text'); $r.ExitCode -eq 1 -and $r.StdErr -like '*暂不支持*'
    }
```

- [ ] **Step 2: 运行脚本确认失败（期望 `3 passed, 6 failed`）**

```bash
pwsh Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1
```

- [ ] **Step 3: 新建 `OpenSsl/PemCodec.cs`**

按 AGENTS.md banner 模板补头（GUID 用 `[guid]::NewGuid().ToString()`，`CLR版本`/`版本号` 写 `net10.0`，`命名空间` 写 `SAEA.Sockets.SslTlsTool.OpenSsl`）。

```csharp
namespace SAEA.Sockets.SslTlsTool.OpenSsl;

/// <summary>PEM 块切分与写出：保序、64 列、只用 LF、不写 BOM。</summary>
internal static class PemCodec
{
    private static readonly Regex Block = new(
        "-----BEGIN ([A-Z0-9 ]+)-----(.*?)-----END \\1-----",
        RegexOptions.Singleline | RegexOptions.CultureInvariant);

    /// <summary>按出现顺序取出全部 PEM 块；空白与 BOM 不参与 base64。</summary>
    public static List<KeyValuePair<string, byte[]>> ParseAll(string text)
    {
        var blocks = new List<KeyValuePair<string, byte[]>>();
        foreach (Match match in Block.Matches(text))
        {
            var body = new string(match.Groups[2].Value
                .Where(ch => !char.IsWhiteSpace(ch) && ch != '\uFEFF').ToArray());
            blocks.Add(new KeyValuePair<string, byte[]>(match.Groups[1].Value, Convert.FromBase64String(body)));
        }
        return blocks;
    }
    /// <summary>读整个 PEM 文件；StreamReader 默认检测并剥离 BOM。</summary>
    public static string ReadFile(string path) => File.Exists(path) ? File.ReadAllText(path) : throw new OpenSslException($"error: 找不到文件 \"{path}\"。");
    /// <summary>把 DER 编成 PEM：64 列 base64、LF 换行、末尾恰好一个换行。</summary>
    public static string Encode(string label, byte[] der)
    {
        var base64 = Convert.ToBase64String(der);
        var text = new StringBuilder();
        text.Append("-----BEGIN ").Append(label).Append("-----\n");
        for (var i = 0; i < base64.Length; i += 64)
            text.Append(base64, i, Math.Min(64, base64.Length - i)).Append('\n');
        return text.Append("-----END ").Append(label).Append("-----\n").ToString();
    }
    /// <summary>写 PEM 文件：固定 UTF-8 无 BOM。</summary>
    public static void WriteFile(string path, string text) => File.WriteAllText(path, text, new UTF8Encoding(false));
    /// <summary>取第一个指定块头的 DER。</summary>
    public static byte[] DecodeFirst(string text, params string[] labels)
    {
        foreach (var block in ParseAll(text))
            foreach (var label in labels)
                if (string.Equals(block.Key, label, StringComparison.Ordinal))
                    return block.Value;
        throw new OpenSslException($"error: 未找到 {string.Join(" 或 ", labels)} 块。");
    }
}
```

- [ ] **Step 4: 新建 `OpenSsl/CertificateCodec.cs`**

按 Step 3 的 banner 规则补头。BC 2.6.2 的 `X509Certificate` 已无 `GetFingerprint`，改用 `DigestUtilities.CalculateDigest` 计算同一摘要，输出格式不变：

```csharp
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace SAEA.Sockets.SslTlsTool.OpenSsl;

/// <summary>证书与链的 PEM/DER 读写，以及姓名、日期、序列号、指纹、公钥的文本格式化。</summary>
internal static class CertificateCodec
{
    private static readonly X509CertificateParser Parser = new();
    private static readonly Dictionary<string, string> NameShort = new(StringComparer.Ordinal)
    {
        [X509Name.CN.Id] = "CN", [X509Name.O.Id] = "O", [X509Name.OU.Id] = "OU", [X509Name.C.Id] = "C",
        [X509Name.L.Id] = "L", [X509Name.ST.Id] = "ST", [X509Name.E.Id] = "emailAddress",
    };
    public static X509Certificate ReadFirst(byte[] data, bool der) => ReadAll(data, der)[0];
    /// <summary>PEM 只取 CERTIFICATE 块；DER 按单张证书解析。</summary>
    public static List<X509Certificate> ReadAll(byte[] data, bool der)
    {
        if (der)
            return new List<X509Certificate> { Parser.ReadCertificate(data) };
        var certificates = new List<X509Certificate>();
        foreach (var block in PemCodec.ParseAll(Encoding.UTF8.GetString(data)))
            if (block.Key == "CERTIFICATE")
                certificates.Add(Parser.ReadCertificate(block.Value));
        return certificates.Count > 0 ? certificates : throw new OpenSslException("error: 输入中没有 CERTIFICATE 块。");
    }
    /// <summary>按输出格式返回证书字节：DER 为原始编码，PEM 为 CERTIFICATE 块。</summary>
    public static byte[] Write(X509Certificate cert, bool der) => der
        ? cert.GetEncoded()
        : Encoding.ASCII.GetBytes(PemCodec.Encode("CERTIFICATE", cert.GetEncoded()));
    public static string FormatSubject(X509Certificate cert) => FormatName(cert.SubjectDN);
    public static string FormatIssuer(X509Certificate cert) => FormatName(cert.IssuerDN);
    public static string FormatDates(X509Certificate cert) =>
        "notBefore=" + FormatDate(cert.NotBefore) + "\nnotAfter=" + FormatDate(cert.NotAfter);
    public static string FormatSerial(X509Certificate cert) =>
        // 前缀由调用方拼，这里只返回大写十六进制。
        cert.SerialNumber.ToString(16).ToUpperInvariant();
    /// <summary>大写十六进制、按字节插冒号；algorithm 传 "SHA-1" 或 "SHA-256"。</summary>
    public static string FormatFingerprint(X509Certificate cert, string algorithm)
    {
        var hex = Convert.ToHexString(DigestUtilities.CalculateDigest(algorithm, cert.GetEncoded()));
        // 每 2 个字符后补冒号，再去掉末尾多余的一个。
        var text = Regex.Replace(hex, "..", "$0:").TrimEnd(':');
        return (algorithm == "SHA-256" ? "SHA256 Fingerprint=" : "SHA1 Fingerprint=") + text;
    }
    public static string FormatPublicKey(X509Certificate cert) =>
        PemCodec.Encode("PUBLIC KEY", cert.CertificateStructure.SubjectPublicKeyInfo.GetEncoded());
    private static string FormatName(X509Name name)
    {
        var parts = name.GetOidList().Zip(name.GetValueList(), (oid, value) =>
            (NameShort.TryGetValue(oid.Id, out var shortName) ? shortName : oid.Id) + " = " + value);
        return string.Join(", ", parts);
    }
    private static string FormatDate(DateTime value) =>
        value.ToUniversalTime().ToString("MMM d HH:mm:ss yyyy", CultureInfo.InvariantCulture) + " GMT";
}
```

- [ ] **Step 5: 新建 `OpenSsl/Commands/X509Command.cs`**

按 Step 3 的 banner 规则补头，`文件名` 写 `X509Command`。诊断项一律先写 stdout，随后才写证书本体；`-noout` 只抑制后者：

```csharp
using Org.BouncyCastle.X509;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace SAEA.Sockets.SslTlsTool.OpenSsl.Commands;

/// <summary>x509：读取证书、打印所选信息，并在 PEM 与 DER 之间转换。</summary>
internal static class X509Command
{
    private static readonly string[] Allowed =
    {
        "-in", "-inform", "-out", "-outform", "-noout", "-subject", "-issuer", "-dates", "-serial",
        "-fingerprint", "-sha256", "-pubkey", "-text", "-nameopt", "-req", "-CA", "-CAkey",
        "-CAcreateserial", "-days", "-extfile", "-extensions", "-copy_extensions",
    };
    public static int Run(string[] args, bool interactive)
    {
        var reader = new OptionReader("x509", args, Allowed);
        // -text / -nameopt 无论其它参数是否合法都先报错；-req 的签发分支在 Task 5 接入。
        if (reader.Has("-text"))
            throw new OpenSslException("x509: -text 暂不支持；请用 -subject/-issuer/-dates/-serial/-fingerprint，或用外部工具查看完整 ASN.1。");
        if (reader.Has("-nameopt"))
            throw new OpenSslException("x509: -nameopt 暂不支持；姓名固定 oneline 风格（CN = localhost, O = SAEA）。");
        if (reader.Has("-req"))
            throw new OpenSslException("x509: -req 将在任务 5 实现。");
        foreach (var option in new[]
                 { "-CA", "-CAkey", "-CAcreateserial", "-days", "-extfile", "-extensions", "-copy_extensions" })
            if (reader.Has(option))
                throw new OpenSslException($"x509: 参数 \"{option}\" 只与 -req 一起使用。");
        var derIn = IsDer(reader, "-inform"); var derOut = IsDer(reader, "-outform");
        if (interactive && derOut && !reader.Has("-out"))
            throw new OpenSslException("交互模式下二进制输出必须给 -out。");
        var certificates = CertificateCodec.ReadAll(ReadInput(reader, derIn), derIn);
        var report = new StringBuilder();
        foreach (var cert in certificates)
            AppendReport(report, reader, cert);
        // 诊断项先写 stdout，再输出证书本体；-noout 只抑制后者。
        Console.Out.Write(report.ToString());
        if (reader.Has("-noout"))
            return 0;
        var payload = certificates.SelectMany(cert => CertificateCodec.Write(cert, derOut)).ToArray();
        WriteOutput(reader, derOut, payload);
        return 0;
    }
    private static byte[] ReadInput(OptionReader reader, bool der)
    {
        if (!reader.Has("-in"))
        {
            if (!der)
                return Encoding.UTF8.GetBytes(Console.In.ReadToEnd());
            using var stdin = Console.OpenStandardInput();
            using var buffer = new MemoryStream();
            stdin.CopyTo(buffer);
            return buffer.ToArray();
        }
        var path = reader.GetRequired("-in");
        return File.Exists(path) ? File.ReadAllBytes(path) : throw new OpenSslException($"error: 找不到文件 \"{path}\"。");
    }
    private static void WriteOutput(OptionReader reader, bool der, byte[] payload)
    {
        if (reader.Has("-out"))
        {
            File.WriteAllBytes(reader.GetRequired("-out"), payload);
            return;
        }
        if (!der)
        {
            Console.Out.Write(Encoding.ASCII.GetString(payload));
            return;
        }
        Console.Out.Flush(); // 原始二进制写 stdout 前清空文本缓冲，避免诊断项混入。
        using var stdout = Console.OpenStandardOutput();
        stdout.Write(payload, 0, payload.Length);
        stdout.Flush();
    }
    private static void AppendReport(StringBuilder report, OptionReader reader, X509Certificate cert)
    {
        if (reader.Has("-subject"))
            report.Append("subject=").Append(CertificateCodec.FormatSubject(cert)).Append('\n');
        if (reader.Has("-issuer"))
            report.Append("issuer=").Append(CertificateCodec.FormatIssuer(cert)).Append('\n');
        if (reader.Has("-dates"))
            report.Append(CertificateCodec.FormatDates(cert)).Append('\n');
        if (reader.Has("-serial"))
            report.Append("serial=").Append(CertificateCodec.FormatSerial(cert)).Append('\n');
        if (reader.Has("-fingerprint"))
            report.Append(CertificateCodec.FormatFingerprint(
                cert, reader.Has("-sha256") ? "SHA-256" : "SHA-1")).Append('\n');
        if (reader.Has("-pubkey"))
            report.Append(CertificateCodec.FormatPublicKey(cert));
    }
    private static bool IsDer(OptionReader reader, string option)
    {
        if (!reader.Has(option))
            return false;
        var value = reader.GetRequired(option);
        if (value == "PEM" || value == "DER")
            return value == "DER";
        throw new OpenSslException($"error: 参数 \"{option}\" 只支持 PEM 或 DER，实际为 \"{value}\"。");
    }
}
```

- [ ] **Step 6: 补 `OpenSslUsage` 帮助与 `OpenSslCommand` 分派**

`OpenSslUsage.For` 的 `switch` 在 `version` 之后加：

```csharp
            case "x509":
                return "用法：SAEA.Sockets.SslTlsTool x509 [选项]\n"
                    + "读取证书、打印所选信息并在 PEM 与 DER 之间转换；-in 缺省 stdin，-out 缺省 stdout。\n"
                    + "读取/输出：-inform PEM|DER（默认 PEM）、-outform PEM|DER（默认 PEM）、-noout\n"
                    + "打印：-subject、-issuer、-dates、-serial、-fingerprint（默认 SHA-1）、-sha256、-pubkey\n"
                    + "签发：-req、-CA <文件>、-CAkey <文件>、-CAcreateserial、-days <正整数>、-extfile <文件>、-extensions <段名>、-copy_extensions copy|none\n"
                    + "不支持：-text、-nameopt（姓名固定 oneline 风格）；打印项写 stdout 且位于证书数据之前。\n";
```

`OpenSslCommand.cs` 加 `using SAEA.Sockets.SslTlsTool.OpenSsl.Commands;`，并把过渡异常替换为：

```csharp
            switch (command)
            {
                case "x509":
                    return X509Command.Run(tail, interactive);
                default:
                    throw new OpenSslException($"error: {command} 尚未实现。");
            }
```

- [ ] **Step 7: 构建工具**

```bash
dotnet build Src/SAEA.Sockets.SslTlsTool/SAEA.Sockets.SslTlsTool.csproj -c Release
```

- [ ] **Step 8: 脚本跑到 PASS（期望 `9 passed, 0 failed`）**

```bash
pwsh Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1
```

- [ ] **Step 9: 全量构建（0 errors）**

```bash
dotnet build Src/SAEA.Sockets.sln -c Debug
```

- [ ] **Step 10: 提交**

```bash
git add Src/SAEA.Sockets.SslTlsTool/OpenSsl/PemCodec.cs Src/SAEA.Sockets.SslTlsTool/OpenSsl/CertificateCodec.cs Src/SAEA.Sockets.SslTlsTool/OpenSsl/Commands/X509Command.cs Src/SAEA.Sockets.SslTlsTool/OpenSsl/OpenSslUsage.cs Src/SAEA.Sockets.SslTlsTool/OpenSsl/OpenSslCommand.cs Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1
git commit -m "feat(ssltls): 支持 x509 读取与 PEM/DER 转换"
```

### Task 3: pkcs8 与 pkey（私钥格式与口令）

**Files:**

- 新建 `Src/SAEA.Sockets.SslTlsTool/OpenSsl/PasswordResolver.cs`、`OpenSsl/KeyCodec.cs`
- 修改 `Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1`、`OpenSsl/OpenSslUsage.cs`；命令类与 `OpenSslCommand` 接线留给本任务 Step 7

- [ ] **Step 1: 脚本追加 pkcs8 用例（先红）**

在 `try {` 块内、x509 用例之后插入；私钥用 Task 2 的 `--self-signed` 产物 `localhost.key`：

```powershell
    $key = Join-Path $src 'localhost.key'; $p8 = Join-Path $tmp 'key.p8'; $enc = Join-Path $tmp 'enc.p8'
    Invoke-Case -Name 'pkcs8 -topk8 -nocrypt 输出 PRIVATE KEY' -Body {
        $r = Invoke-Tool -Arguments @('pkcs8', '-topk8', '-nocrypt', '-in', $key, '-out', $p8)
        $r.ExitCode -eq 0 -and (Get-Content $p8 -Raw) -like '*BEGIN PRIVATE KEY*'
    }
    Invoke-Case -Name 'pkcs8 错误口令退出 1 且报口令错误' -Body {
        $a = Invoke-Tool -Arguments @('pkcs8', '-topk8', '-in', $key, '-passout', 'pass:x', '-out', $enc)
        $b = Invoke-Tool -Arguments @('pkcs8', '-in', $enc, '-passin', 'pass:y', '-out', (Join-Path $tmp 'bad.p8'))
        $a.ExitCode -eq 0 -and $b.ExitCode -eq 1 -and $b.StdErr -like '*口令错误*'
    }
```

- [ ] **Step 2: 新建 `OpenSsl/PasswordResolver.cs`**

按 AGENTS.md banner 模板补头（`唯一标识` 用 `[guid]::NewGuid().ToString()`，`CLR版本`/`版本号` 写 `net10.0`，`命名空间` 写 `SAEA.Sockets.SslTlsTool.OpenSsl`，`文件名` 写 `PasswordResolver`）：

```csharp
namespace SAEA.Sockets.SslTlsTool.OpenSsl;

/// <summary>解析 -passin/-passout 口令来源，必要时在交互终端提示输入。</summary>
internal static class PasswordResolver
{
    public static string? Resolve(string? spec, string target, bool confirm, bool interactive)
    {
        if (spec is null) return Prompt(target, confirm);
        if (spec.StartsWith("pass:", StringComparison.Ordinal)) return spec.Substring(5);
        if (spec.StartsWith("env:", StringComparison.Ordinal)) return Environment.GetEnvironmentVariable(spec.Substring(4)) is { Length: > 0 } value ? value : null;
        if (spec.StartsWith("file:", StringComparison.Ordinal))
        {
            var path = spec.Substring(5);
            if (!File.Exists(path)) throw new OpenSslException($"error: 找不到口令文件 \"{path}\"。");
            // 只取首行并去掉行尾 CR/LF，其余字节原样（§10）。
            return File.ReadLines(path).FirstOrDefault() ?? throw new OpenSslException($"error: 口令文件为空 \"{path}\"。");
        }
        if (spec == "stdin") return interactive
            ? throw new OpenSslException("交互模式下不支持 -passin stdin；请用 pass:、env: 或 file:。")
            : Console.ReadLine();
        throw new OpenSslException($"error: 不支持的口令来源 \"{spec}\"；支持 pass:、env:、file:、stdin。");
    }
    private static string Prompt(string target, bool confirm)
    {
        if (Console.IsInputRedirected) throw new OpenSslException("需要口令：请用 -passin/-passout，或从交互终端运行。");
        var first = ReadLine("Enter pass phrase for " + target + ": ");
        if (!confirm) return first;
        return first == ReadLine("Verifying - Enter pass phrase for " + target + ": ") ? first : throw new OpenSslException("两次输入的口令不一致。");
    }
    private static string ReadLine(string prompt)
    {
        Console.Error.Write(prompt);
        var text = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) { Console.Error.WriteLine(); return text.ToString(); }
            if (key.Key == ConsoleKey.Backspace) { if (text.Length > 0) text.Length--; }
            else if (!char.IsControl(key.KeyChar)) text.Append(key.KeyChar);
        }
    }
}
```

- [ ] **Step 3: 新建 `OpenSsl/KeyCodec.cs`**

按 Step 2 的 banner 规则补头，`文件名` 写 `KeyCodec`：

```csharp
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace SAEA.Sockets.SslTlsTool.OpenSsl;

internal static class KeyCodec
{
    private static readonly SecureRandom Random = new();
    public static AsymmetricKeyParameter ReadPrivateKey(byte[] data, string? password, bool der) =>
        der ? ReadDer(data, password) : ReadPem(Encoding.ASCII.GetString(data), password);
    public static byte[] WritePkcs8(AsymmetricKeyParameter key, string? password, string? cipher, bool der)
    {
        var info = PrivateKeyInfoFactory.CreatePrivateKeyInfo(key);
        return Encode(password is null ? info.GetEncoded() : Encrypt(key, info, password, cipher ?? "aes-256-cbc"),
            der, password is null ? "PRIVATE KEY" : "ENCRYPTED PRIVATE KEY");
    }
    public static byte[] WritePkcs1(AsymmetricKeyParameter key) => key is RsaPrivateCrtKeyParameters rsa
        ? new RsaPrivateKeyStructure(rsa.Modulus, rsa.PublicExponent, rsa.Exponent, rsa.P, rsa.Q, rsa.DP, rsa.DQ, rsa.QInv).GetEncoded()
        : throw new OpenSslException("error: 只有 RSA 私钥可以输出 PKCS#1。");
    public static byte[] WritePublicKey(AsymmetricKeyParameter key, bool der) =>
        Encode(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(key).GetEncoded(), der, "PUBLIC KEY");
    private static byte[] Encode(byte[] data, bool der, string label) =>
        der ? data : Encoding.ASCII.GetBytes(PemCodec.Encode(label, data));
    // PKCS#8 明文 3 段、加密 PKCS#8 2 段、PKCS#1 为 9 个 INTEGER，按段数区分。
    private static AsymmetricKeyParameter ReadDer(byte[] data, string? password)
    {
        var sequence = Asn1Sequence.GetInstance(Asn1Object.FromByteArray(data));
        if (sequence.Count == 2) return PrivateKeyFactory.CreateKey(Decrypt(data, password));
        if (sequence.Count == 3) return PrivateKeyFactory.CreateKey(PrivateKeyInfo.GetInstance(sequence));
        var rsa = RsaPrivateKeyStructure.GetInstance(sequence);
        return new RsaPrivateCrtKeyParameters(rsa.Modulus, rsa.PublicExponent, rsa.PrivateExponent,
            rsa.Prime1, rsa.Prime2, rsa.Exponent1, rsa.Exponent2, rsa.Coefficient);
    }
    private static AsymmetricKeyParameter ReadPem(string text, string? password)
    {
        // 旧式 PEM 的 DEK-Info 头会破坏 base64，只能交给 PemReader。
        if (text.Contains("Proc-Type: 4,ENCRYPTED", StringComparison.Ordinal)) return ReadLegacy(text, password);
        foreach (var block in PemCodec.ParseAll(text))
            if (block.Key is "PRIVATE KEY" or "ENCRYPTED PRIVATE KEY" or "RSA PRIVATE KEY") return ReadDer(block.Value, password);
        throw new OpenSslException("error: 输入中没有私钥块。");
    }
    private static AsymmetricKeyParameter ReadLegacy(string text, string? password)
    {
        var value = new PemReader(new StringReader(text), new FixedPassword(password ?? string.Empty)).ReadObject();
        return value is AsymmetricCipherKeyPair pair ? pair.Private : value as AsymmetricKeyParameter
            ?? throw new OpenSslException("error: 无法解析私钥。");
    }
    private static PrivateKeyInfo Decrypt(byte[] data, string? password)
    {
        if (password is null) throw new OpenSslException("error: 私钥已加密，需要口令。");
        try
        {
            var info = EncryptedPrivateKeyInfo.GetInstance(Asn1Object.FromByteArray(data));
            return PrivateKeyInfoFactory.CreatePrivateKeyInfo(password.ToCharArray(), info);
        }
        // BC 对不同算法分别抛 CryptoException / PkcsException，统一成口令错误，避免成为口令探测手段。
        catch (Exception ex) when (ex is not OpenSslException)
        {
            throw new OpenSslException("error: 口令错误或文件损坏。");
        }
    }
    private static byte[] Encrypt(AsymmetricKeyParameter key, PrivateKeyInfo info, string password, string cipher)
    {
        // PBE-SHA1-3DES 只对应 pkcs8 -v1；-v2 des3 与 pkey -des3 走下面的 PBES2 分支。
        if (cipher == "pbe-sha1-3des")
            return new Pkcs8Generator(key, Pkcs8Generator.PbeSha1_3DES) { Password = password.ToCharArray(), SecureRandom = Random }.Generate().Content;
        var salt = new byte[16]; Random.NextBytes(salt);
        return EncryptedPrivateKeyInfoFactory.CreateEncryptedPrivateKeyInfo(
            CipherOid(cipher), NistObjectIdentifiers.IdHmacWithSha256, password.ToCharArray(), salt, 2048, Random, info).GetEncoded();
    }
    private static DerObjectIdentifier CipherOid(string cipher) => cipher switch
    {
        "aes-128-cbc" => NistObjectIdentifiers.IdAes128Cbc, "aes-192-cbc" => NistObjectIdentifiers.IdAes192Cbc,
        "aes-256-cbc" => NistObjectIdentifiers.IdAes256Cbc, "des3" => PkcsObjectIdentifiers.DesEde3Cbc,
        _ => throw new OpenSslException($"error: 不支持的加密算法 \"{cipher}\"；支持 aes-128-cbc、aes-192-cbc、aes-256-cbc、des3。"),
    };
    private sealed class FixedPassword(string password) : IPasswordFinder
    {
        public char[] GetPassword() => password.ToCharArray();
    }
}
```

- [ ] **Step 4: 补 `OpenSslUsage` 帮助并构建**

本步骤先编译再过 Step 5。需要核对的 BC 2.6.2 签名：`EncryptedPrivateKeyInfoFactory.CreateEncryptedPrivateKeyInfo` 是否接受 `PrivateKeyInfo`（若只接受 `AsymmetricKeyParameter`，传 `PrivateKeyFactory.CreateKey(info)`），`Pkcs8Generator.PbeSha1_3DES` 常量名，`Pkcs8Generator` 的 `Password`/`SecureRandom` 属性名。签名不符时按实际形态改写，算法与迭代 2048 不变。

`OpenSslUsage.For` 在 `x509` 分支后补 `pkcs8`、`pkey` 两段帮助，列出读取/输出/加密选项，`pkcs8` 注明 `-v2` 默认 `aes-256-cbc`。构建：

```bash
dotnet build Src/SAEA.Sockets.SslTlsTool/SAEA.Sockets.SslTlsTool.csproj -c Release
```

本步骤结束时 `pkcs8`/`pkey` 尚未在 `OpenSslCommand` 接线，Step 1 的两条用例仍 FAIL，属预期，由本任务 Step 7 接线后转绿。

- [ ] **Step 5: 新建 `OpenSsl/Commands/Pkcs8Command.cs`**

banner 一句：`唯一标识` 用 `[guid]::NewGuid().ToString()`，`CLR版本`/`版本号` 写 `net10.0`，`命名空间` 写 `SAEA.Sockets.SslTlsTool.OpenSsl.Commands`，`文件名` 写 `Pkcs8Command`。I/O 与格式辅助声明为 `internal`，供 `PkeyCommand` 复用：

```csharp
using Org.BouncyCastle.Asn1;

namespace SAEA.Sockets.SslTlsTool.OpenSsl.Commands;

/// <summary>pkcs8：在 PKCS#1 与 PKCS#8 之间转换私钥，并按需加解密。</summary>
internal static class Pkcs8Command
{
    private static readonly string[] Allowed = { "-topk8", "-in", "-out", "-inform", "-outform", "-nocrypt", "-v1", "-v2", "-passin", "-passout" };

    public static int Run(string[] args, bool interactive)
    {
        var reader = new OptionReader("pkcs8", args, Allowed);
        var derIn = IsDer(reader, "-inform");
        var derOut = IsDer(reader, "-outform");
        if (interactive && derOut && !reader.Has("-out"))
            throw new OpenSslException("交互模式下二进制输出必须给 -out。");
        var data = Read(reader, derIn);
        // 只有输入确实加密时才提示口令，避免对明文私钥空转一次交互。
        var passIn = reader.Has("-passin") || IsEncrypted(data, derIn)
            ? PasswordResolver.Resolve(reader.Get("-passin"), reader.Get("-in") ?? "stdin", false, interactive)
            : null;
        var key = KeyCodec.ReadPrivateKey(data, passIn, derIn);
        if (!reader.Has("-topk8"))
        {
            if (reader.Has("-passout"))
                throw new OpenSslException("pkcs8: 非 -topk8 模式不支持 -passout。");
            if (reader.Has("-v1") || reader.Has("-v2"))
                throw new OpenSslException("pkcs8: -v1/-v2 只与 -topk8 一起使用。");
            var pkcs1 = KeyCodec.WritePkcs1(key);
            Write(reader, derOut, derOut ? pkcs1 : Encoding.ASCII.GetBytes(PemCodec.Encode("RSA PRIVATE KEY", pkcs1)));
            return 0;
        }
        if (reader.Has("-nocrypt") && (reader.Has("-passout") || reader.Has("-v1") || reader.Has("-v2")))
            throw new OpenSslException("pkcs8: -nocrypt 不能与 -passout/-v1/-v2 同时使用。");
        if (reader.Has("-v1") && reader.Has("-v2"))
            throw new OpenSslException("pkcs8: -v1 与 -v2 不能同时使用。");
        if (reader.Has("-v1") && reader.GetRequired("-v1") != "PBE-SHA1-3DES")
            throw new OpenSslException("pkcs8: -v1 只支持 PBE-SHA1-3DES。");
        var cipher = reader.Has("-v1") ? "pbe-sha1-3des" : reader.Has("-v2") ? reader.GetRequired("-v2") : "aes-256-cbc";
        string? passOut = null;
        if (!reader.Has("-nocrypt"))
        {
            passOut = PasswordResolver.Resolve(reader.Get("-passout"), reader.Get("-out") ?? "stdout", true, interactive);
            if (string.IsNullOrEmpty(passOut))
                throw new OpenSslException("pkcs8: PKCS#8 加密口令不能为空。");
        }
        Write(reader, derOut, KeyCodec.WritePkcs8(key, passOut, cipher, derOut));
        return 0;
    }

    // 加密 PKCS#8 的 DER 顶层 SEQUENCE 只有算法与密文两段；PEM 则带 ENCRYPTED 头。
    internal static bool IsEncrypted(byte[] data, bool der) => der
        ? Asn1Sequence.GetInstance(Asn1Object.FromByteArray(data)).Count == 2
        : Encoding.ASCII.GetString(data).Contains("ENCRYPTED", StringComparison.Ordinal);

    internal static byte[] Read(OptionReader reader, bool der)
    {
        var path = reader.Get("-in");
        if (path is not null) return File.Exists(path) ? File.ReadAllBytes(path) : throw new OpenSslException($"error: 找不到文件 \"{path}\"。");
        if (!der) return Encoding.UTF8.GetBytes(Console.In.ReadToEnd());
        using var stdin = Console.OpenStandardInput();
        using var buffer = new MemoryStream();
        stdin.CopyTo(buffer);
        return buffer.ToArray();
    }

    internal static void Write(OptionReader reader, bool der, byte[] payload)
    {
        var path = reader.Get("-out");
        if (path is not null) { File.WriteAllBytes(path, payload); return; }
        if (!der) { Console.Out.Write(Encoding.ASCII.GetString(payload)); return; }
        Console.Out.Flush(); // 原始二进制写 stdout 前清空文本缓冲，避免诊断项混入。
        using var stdout = Console.OpenStandardOutput();
        stdout.Write(payload, 0, payload.Length);
        stdout.Flush();
    }

    internal static bool IsDer(OptionReader reader, string option)
    {
        if (!reader.Has(option)) return false;
        var value = reader.GetRequired(option);
        if (value == "PEM") return false;
        if (value == "DER") return true;
        throw new OpenSslException($"error: 参数 \"{option}\" 只支持 PEM 或 DER，实际为 \"{value}\"。");
    }
}
```

- [ ] **Step 6: 新建 `OpenSsl/Commands/PkeyCommand.cs`**

banner 规则同 Step 5，`文件名` 写 `PkeyCommand`：

```csharp
using Org.BouncyCastle.Crypto.Parameters;

namespace SAEA.Sockets.SslTlsTool.OpenSsl.Commands;

/// <summary>pkey：读取私钥，输出 PKCS#8、PKCS#1 或公钥，并可按需加密。</summary>
internal static class PkeyCommand
{
    private static readonly string[] Allowed =
    {
        "-in", "-out", "-inform", "-outform", "-pubout", "-traditional",
        "-aes-128-cbc", "-aes-192-cbc", "-aes-256-cbc", "-des3", "-passin", "-passout",
    };

    public static int Run(string[] args, bool interactive)
    {
        var reader = new OptionReader("pkey", args, Allowed);
        var derIn = Pkcs8Command.IsDer(reader, "-inform");
        var derOut = Pkcs8Command.IsDer(reader, "-outform");
        if (interactive && derOut && !reader.Has("-out"))
            throw new OpenSslException("交互模式下二进制输出必须给 -out。");
        var ciphers = new[] { "-aes-128-cbc", "-aes-192-cbc", "-aes-256-cbc", "-des3" }.Where(reader.Has).ToArray();
        if (ciphers.Length > 1)
            throw new OpenSslException("pkey: 加密算法只能选一个。");
        var cipher = ciphers.FirstOrDefault()?.Substring(1);
        if (reader.Has("-traditional") && cipher is not null)
            throw new OpenSslException("pkey: -traditional 不能与加密算法同时使用。");
        if (reader.Has("-pubout") && (reader.Has("-traditional") || cipher is not null))
            throw new OpenSslException("pkey: -pubout 不能与 -traditional 或加密算法同时使用。");
        // 没有 cipher 时 -passout 无处生效，报错而不是静默输出明文，避免误以为已加密。
        if (reader.Has("-passout") && cipher is null)
            throw new OpenSslException("pkey: 未指定加密算法时不能给 -passout；需要加密请加 -aes-256-cbc 等。");
        var data = Pkcs8Command.Read(reader, derIn);
        var passIn = reader.Has("-passin") || Pkcs8Command.IsEncrypted(data, derIn)
            ? PasswordResolver.Resolve(reader.Get("-passin"), reader.Get("-in") ?? "stdin", false, interactive)
            : null;
        var key = KeyCodec.ReadPrivateKey(data, passIn, derIn);
        if (reader.Has("-pubout"))
        {
            var pub = key is RsaPrivateCrtKeyParameters rsa
                ? new RsaKeyParameters(false, rsa.Modulus, rsa.PublicExponent)
                : throw new OpenSslException("error: 只有 RSA 私钥可以输出公钥。");
            Pkcs8Command.Write(reader, derOut, KeyCodec.WritePublicKey(pub, derOut));
            return 0;
        }
        if (reader.Has("-traditional"))
        {
            var pkcs1 = KeyCodec.WritePkcs1(key);
            Pkcs8Command.Write(reader, derOut, derOut ? pkcs1 : Encoding.ASCII.GetBytes(PemCodec.Encode("RSA PRIVATE KEY", pkcs1)));
            return 0;
        }
        string? passOut = null;
        if (cipher is not null)
        {
            passOut = PasswordResolver.Resolve(reader.Get("-passout"), reader.Get("-out") ?? "stdout", true, interactive);
            if (string.IsNullOrEmpty(passOut))
                throw new OpenSslException("pkey: PKCS#8 加密口令不能为空。");
        }
        Pkcs8Command.Write(reader, derOut, KeyCodec.WritePkcs8(key, passOut, cipher, derOut));
        return 0;
    }
}
```

- [ ] **Step 7: 补 `OpenSslUsage` 帮助并接线 `OpenSslCommand`**

`OpenSslUsage.For` 在 `x509` 分支后加：

```csharp
            case "pkcs8":
                return "用法：SAEA.Sockets.SslTlsTool pkcs8 [选项]\n在 PKCS#1 与 PKCS#8 之间转换私钥；-in 缺省 stdin，-out 缺省 stdout。\n"
                    + "选项：-in、-out、-inform PEM|DER、-outform PEM|DER、-topk8、-nocrypt、-v1 PBE-SHA1-3DES、-v2 aes-128-cbc|aes-192-cbc|aes-256-cbc|des3（默认 aes-256-cbc）、-passin、-passout；-v1 与 -v2 互斥，非 -topk8 不支持 -passout。\n";
            case "pkey":
                return "用法：SAEA.Sockets.SslTlsTool pkey [选项]\n读取私钥并输出 PKCS#8（默认）、PKCS#1（-traditional）或公钥（-pubout）；-in 缺省 stdin，-out 缺省 stdout。\n"
                    + "选项：-in、-out、-inform PEM|DER、-outform PEM|DER、-pubout、-traditional、-aes-128-cbc、-aes-192-cbc、-aes-256-cbc、-des3（与 -traditional 互斥）、-passin、-passout。\n";
```

`OpenSslCommand.Run` 的 `switch` 加：

```csharp
                case "pkcs8":
                    return Pkcs8Command.Run(tail, interactive);
                case "pkey":
                    return PkeyCommand.Run(tail, interactive);
```

- [ ] **Step 8: 脚本补齐 Task 3 余下用例**

在 Step 1 的两条用例之后插入，`$key`、`$p8` 沿用 Step 1 的变量：

```powershell
    $p8test = Join-Path $tmp 'key-test.p8'; $plain = Join-Path $tmp 'plain.pem'
    Invoke-Case -Name 'pkey -traditional 输出 RSA PRIVATE KEY' -Body { $o = Join-Path $tmp 'rsa.pem'; $r = Invoke-Tool -Arguments @('pkey', '-in', $key, '-traditional', '-out', $o); $r.ExitCode -eq 0 -and (Get-Content $o -Raw) -like '*BEGIN RSA PRIVATE KEY*' }
    Invoke-Case -Name 'pkcs8 -topk8 -passout 输出 ENCRYPTED PRIVATE KEY' -Body { $r = Invoke-Tool -Arguments @('pkcs8', '-topk8', '-in', $key, '-passout', 'pass:test', '-out', $p8test); $r.ExitCode -eq 0 -and (Get-Content $p8test -Raw) -like '*BEGIN ENCRYPTED PRIVATE KEY*' }
    Invoke-Case -Name 'pkey -passin 解密回明文 PRIVATE KEY' -Body { $r = Invoke-Tool -Arguments @('pkey', '-in', $p8test, '-passin', 'pass:test', '-out', $plain); $text = Get-Content $plain -Raw; $r.ExitCode -eq 0 -and $text -like '*BEGIN PRIVATE KEY*' -and $text -notlike '*ENCRYPTED*' }
    Invoke-Case -Name 'pkey -passin env:SAEA_TEST_PWD 成功' -Body { $env:SAEA_TEST_PWD = 'test'; $o = Join-Path $tmp 'env.pem'; $r = Invoke-Tool -Arguments @('pkey', '-in', $p8test, '-passin', 'env:SAEA_TEST_PWD', '-out', $o); $r.ExitCode -eq 0 -and (Get-Content $o -Raw) -like '*BEGIN PRIVATE KEY*' }
    Remove-Item Env:\SAEA_TEST_PWD -ErrorAction SilentlyContinue
    Invoke-Case -Name 'pkcs8 -v1 与 -v2 同给退出 1' -Body { $r = Invoke-Tool -Arguments @('pkcs8', '-topk8', '-v1', 'PBE-SHA1-3DES', '-v2', 'aes-256-cbc', '-in', $key, '-passout', 'pass:test', '-out', $p8test); $r.ExitCode -eq 1 -and $r.StdErr -like '*不能同时使用*' }
```

本步跑完，Step 1 的两条 `pkcs8` 用例（`-nocrypt` 输出与错误口令退出 1）随命令接线一并转绿。

- [ ] **Step 9: 构建并跑脚本**

```bash
dotnet build Src/SAEA.Sockets.SslTlsTool/SAEA.Sockets.SslTlsTool.csproj -c Release
pwsh Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1
```

0 errors；末行 `16 passed, 0 failed`，退出码 0（Task 2 的 9 条 + Task 3 的 2 条 + 本步 5 条）。

- [ ] **Step 10: 提交**

```bash
git add Src/SAEA.Sockets.SslTlsTool/OpenSsl/PasswordResolver.cs Src/SAEA.Sockets.SslTlsTool/OpenSsl/KeyCodec.cs Src/SAEA.Sockets.SslTlsTool/OpenSsl/Commands/Pkcs8Command.cs Src/SAEA.Sockets.SslTlsTool/OpenSsl/Commands/PkeyCommand.cs Src/SAEA.Sockets.SslTlsTool/OpenSsl/OpenSslUsage.cs Src/SAEA.Sockets.SslTlsTool/OpenSsl/OpenSslCommand.cs Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1
git commit -m "feat(ssltls): 支持 pkcs8 与 pkey 私钥格式转换"
```

### Task 4: req（CSR 与自签）
**Files:**
- 新建 `Src/SAEA.Sockets.SslTlsTool/OpenSsl/{SubjectNameParser,ExtensionSpec,MiniConfigReader,CsrCodec}.cs`
- 修改 `Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1`（追加 4 条 req 用例）

四个新文件按 Task 1 Step 1 的 banner 模板补头（`唯一标识` 用 `[guid]::NewGuid().ToString()`，`CLR版本`/`版本号` 写 `net10.0`，`命名空间` 写 `SAEA.Sockets.SslTlsTool.OpenSsl`）。
- [ ] **Step 1: 脚本追加 req 用例（先红）**
在 `try {` 块内、pkey 用例之后插入：
```powershell
    Invoke-Case -Name 'req -new -newkey 输出 CERTIFICATE REQUEST' -Body { $k = Join-Path $tmp 'k.pem'; $o = Join-Path $tmp 'r.csr'; $r = Invoke-Tool -Arguments @('req', '-new', '-newkey', 'rsa:2048', '-keyout', $k, '-subj', '/CN=localhost', '-out', $o); $r.ExitCode -eq 0 -and (Get-Content $o -Raw) -like '*BEGIN CERTIFICATE REQUEST*' }
    Invoke-Case -Name 'req 缺 -subj 退出 1' -Body { $r = Invoke-Tool -Arguments @('req', '-new', '-newkey', 'rsa:2048', '-keyout', (Join-Path $tmp 'n.pem'), '-out', (Join-Path $tmp 'n.csr')); $r.ExitCode -eq 1 -and $r.StdErr -like '*需要 -subj*' }
    Invoke-Case -Name 'req -addext subjectAltName 生成 CSR（签发断言 Task 5 补）' -Body { $o = Join-Path $tmp 'san.csr'; $r = Invoke-Tool -Arguments @('req', '-new', '-newkey', 'rsa:2048', '-keyout', (Join-Path $tmp 'san.pem'), '-subj', '/CN=localhost', '-addext', 'subjectAltName=DNS:localhost,IP:127.0.0.1', '-out', $o); $r.ExitCode -eq 0 -and (Get-Content $o -Raw) -like '*BEGIN CERTIFICATE REQUEST*' }
    Invoke-Case -Name 'req -addext 未知扩展退出 1' -Body { $r = Invoke-Tool -Arguments @('req', '-new', '-newkey', 'rsa:2048', '-keyout', (Join-Path $tmp 'bad.pem'), '-subj', '/CN=localhost', '-addext', 'authorityInfoAccess=OCSP;URI:http://ocsp.example', '-out', (Join-Path $tmp 'bad.csr')); $r.ExitCode -eq 1 -and $r.StdErr -like '*不支持的扩展*' }
```
- [ ] **Step 2: 新建 `OpenSsl/SubjectNameParser.cs`**
```csharp
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;

namespace SAEA.Sockets.SslTlsTool.OpenSsl;

/// <summary>把 openssl 斜杠格式的 -subj 解析成 X509Name；分隔符为 / 或 ,，支持 \/、\,、\\ 转义，不支持 + 多值。</summary>
internal static class SubjectNameParser
{
    private static readonly Dictionary<string, DerObjectIdentifier> Known = new(StringComparer.Ordinal)
    {
        ["C"] = X509Name.C, ["O"] = X509Name.O, ["OU"] = X509Name.OU, ["CN"] = X509Name.CN,
        ["L"] = X509Name.L, ["ST"] = X509Name.ST, ["emailAddress"] = X509Name.EmailAddress,
    };
    public static X509Name Parse(string value)
    {
        if (value.Length == 0 || value[0] != '/') throw Invalid(value);
        var oids = new List<DerObjectIdentifier>();
        var values = new List<string>();
        var index = 1;
        while (index < value.Length)
        {
            var text = new StringBuilder();
            var escaped = false;
            while (index < value.Length)
            {
                var ch = value[index];
                if (escaped) { text.Append(ch); escaped = false; index++; continue; }
                if (ch == '\\') { escaped = true; index++; continue; }
                if (ch == '/' || ch == ',') break;
                if (ch == '+') throw new OpenSslException("req: -subj 不支持 + 多值。");
                text.Append(ch);
                index++;
            }
            var split = text.ToString().IndexOf('=');
            if (split <= 0 || split == text.Length - 1 || !Known.TryGetValue(text.ToString(0, split), out var oid)) throw Invalid(value);
            oids.Add(oid);
            values.Add(text.ToString(split + 1, text.Length - split - 1));
            index++; // 跳过分隔符；值里的分隔符必须写转义。
        }
        return oids.Count > 0 ? new X509Name(oids, values) : throw Invalid(value);
    }
    private static OpenSslException Invalid(string value) => new($"req: 无法解析 -subj：\"{value}\"。");
}
```
- [ ] **Step 3: 新建 `OpenSsl/ExtensionSpec.cs`**
```csharp
using System.Net;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Pkcs;

namespace SAEA.Sockets.SslTlsTool.OpenSsl;

/// <summary>解析 -addext 与扩展配置段，并搬运 CSR 请求中的四类扩展。</summary>
internal static class ExtensionSpec
{
    private static readonly DerObjectIdentifier[] Copyable = { X509Extensions.SubjectAlternativeName, X509Extensions.BasicConstraints, X509Extensions.KeyUsage, X509Extensions.ExtendedKeyUsage };
    public static IList<X509Extension> ParseAddext(IEnumerable<string> values) => values.Select(item =>
    {
        var split = item.IndexOf('=');
        if (split <= 0) throw new OpenSslException($"req: 扩展需要 name=value 形式：\"{item}\"。");
        var body = item.Substring(split + 1).Trim();
        var critical = body.StartsWith("critical,", StringComparison.Ordinal);
        return new X509Extension(critical, new DerOctetString(Build(item.Substring(0, split).Trim(), critical ? body.Substring(9).Trim() : body).GetEncoded()));
    }).ToList();
    public static IList<X509Extension> CopyFromRequest(Pkcs10CertificationRequest csr)
    {
        var requested = csr.GetRequestedExtensions();
        return requested == null ? new List<X509Extension>()
            : Copyable.Select(oid => requested.GetExtension(oid)).OfType<X509Extension>().ToList();
    }
    public static IList<X509Extension> FromConfig(MiniConfigReader config, string section) =>
        ParseAddext(config.Section(section).Select(pair => pair.Key + "=" + pair.Value));
    private static Asn1Encodable Build(string name, string body)
    {
        switch (name)
        {
            case "subjectAltName": return new GeneralNames(body.Split(',').Select(San).ToArray());
            case "basicConstraints": return Constraints(name, body);
            case "keyUsage": return new KeyUsage(body.Split(',').Aggregate(0, (all, token) => all | Usage(name, token)));
            case "extendedKeyUsage": return new ExtendedKeyUsage(body.Split(',').Select(token => Purpose(name, token)).ToArray());
            default: throw new OpenSslException($"req: 不支持的扩展 \"{name}\"；支持：subjectAltName、basicConstraints、keyUsage、extendedKeyUsage。");
        }
    }
    private static GeneralName San(string token)
    {
        var item = token.Trim();
        var colon = item.IndexOf(':');
        if (colon <= 0) throw BadValue("subjectAltName", item);
        var text = item.Substring(colon + 1);
        switch (item.Substring(0, colon))
        {
            case "DNS": return new GeneralName(GeneralName.DnsName, text);
            case "email": return new GeneralName(GeneralName.Rfc822Name, text);
            case "URI": return new GeneralName(GeneralName.UniformResourceIdentifier, text);
            case "IP": return IPAddress.TryParse(text, out var address)
                ? new GeneralName(GeneralName.IPAddress, new DerOctetString(address.GetAddressBytes()))
                : throw BadValue("subjectAltName", item);
            default: throw BadValue("subjectAltName", item);
        }
    }
    private static BasicConstraints Constraints(string name, string body)
    {
        switch (body.ToUpperInvariant())
        {
            case "CA:TRUE": return new BasicConstraints(true);
            case "CA:FALSE": return new BasicConstraints(false);
            default: throw BadValue(name, body);
        }
    }
    private static int Usage(string name, string token) => token.Trim() switch
    {
        "digitalSignature" => KeyUsage.DigitalSignature, "nonRepudiation" => KeyUsage.NonRepudiation,
        "keyEncipherment" => KeyUsage.KeyEncipherment, "dataEncipherment" => KeyUsage.DataEncipherment,
        "keyAgreement" => KeyUsage.KeyAgreement, "keyCertSign" => KeyUsage.KeyCertSign,
        "cRLSign" => KeyUsage.CrlSign, "encipherOnly" => KeyUsage.EncipherOnly,
        "decipherOnly" => KeyUsage.DecipherOnly, _ => throw BadValue(name, token.Trim()),
    };
    private static KeyPurposeID Purpose(string name, string token) => token.Trim() switch
    {
        "serverAuth" => KeyPurposeID.id_kp_serverAuth, "clientAuth" => KeyPurposeID.id_kp_clientAuth,
        "codeSigning" => KeyPurposeID.id_kp_codeSigning, "emailProtection" => KeyPurposeID.id_kp_emailProtection,
        "timeStamping" => KeyPurposeID.id_kp_timeStamping, "OCSPSigning" => KeyPurposeID.id_kp_OCSPSigning,
        _ => throw BadValue(name, token.Trim()),
    };
    private static OpenSslException BadValue(string name, string value) => new($"req: 不支持的扩展 {name} 取值 \"{value}\"。");
}
```
- [ ] **Step 4: 新建 `OpenSsl/MiniConfigReader.cs`**
```csharp
namespace SAEA.Sockets.SslTlsTool.OpenSsl;

/// <summary>只认 [section]、key=value 与 ;/# 注释的最小配置读取器。</summary>
internal sealed class MiniConfigReader
{
    private readonly Dictionary<string, List<KeyValuePair<string, string>>> _sections = new(StringComparer.Ordinal);
    public static MiniConfigReader Load(string path)
    {
        if (!File.Exists(path)) throw new OpenSslException($"error: 找不到文件 \"{path}\"。");
        var reader = new MiniConfigReader();
        List<KeyValuePair<string, string>>? current = null;
        foreach (var line in File.ReadAllLines(path).Select(raw => raw.Trim()))
        {
            if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;
            if (line[0] == '[' && line[line.Length - 1] == ']')
            {
                var name = line.Substring(1, line.Length - 2).Trim();
                reader._sections[name] = current = reader._sections.TryGetValue(name, out var existing) ? existing : new List<KeyValuePair<string, string>>();
                continue;
            }
            var split = line.IndexOf('=');
            if (current == null || split <= 0) throw new OpenSslException($"error: 无法解析配置行 \"{line}\"。");
            current.Add(new KeyValuePair<string, string>(line.Substring(0, split).Trim(), line.Substring(split + 1).Trim()));
        }
        return reader;
    }
    public IReadOnlyList<KeyValuePair<string, string>> Section(string name) =>
        _sections.TryGetValue(name, out var entries) ? entries : throw new OpenSslException($"x509: -extensions 指定的段落 \"{name}\" 不存在。");
}
```
- [ ] **Step 5: 新建 `OpenSsl/CsrCodec.cs`**
BC 2.6.2 的证书生成器没有 `SetSubject`，主体用 `SetSubjectDN`；`Generate` 只接受单参 `ISignatureFactory` 重载。
```csharp
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.X509;

namespace SAEA.Sockets.SslTlsTool.OpenSsl;

/// <summary>CSR 生成与读取，以及用 CA 私钥签发证书。</summary>
internal static class CsrCodec
{
    public static Pkcs10CertificationRequest Create(AsymmetricKeyParameter key, X509Name subject, IList<X509Extension>? extensions, string digest)
    {
        Asn1Set attributes = DerSet.Empty;
        if (extensions != null && extensions.Count > 0)
        {
            var generator = new X509ExtensionsGenerator();
            foreach (var extension in extensions) generator.AddExtension(extension.Oid, extension.IsCritical, extension.Value.GetOctets());
            attributes = new DerSet(new AttributeX509(PkcsObjectIdentifiers.Pkcs9AtExtensionRequest, new DerSet(generator.Generate())));
        }
        return new Pkcs10CertificationRequest(new Asn1SignatureFactory(SignatureAlgorithm(digest), key), subject, key, attributes);
    }
    public static Pkcs10CertificationRequest Read(byte[] data, bool der) => der ? new Pkcs10CertificationRequest(data)
        : new Pkcs10CertificationRequest(PemCodec.DecodeFirst(Encoding.ASCII.GetString(data), "CERTIFICATE REQUEST", "NEW CERTIFICATE REQUEST"));
    public static X509Certificate Sign(Pkcs10CertificationRequest csr, AsymmetricKeyParameter caKey, X509Certificate caCert, BigInteger serial, DateTime notBefore, DateTime notAfter, IList<X509Extension> extensions, string digest)
    {
        var generator = new X509V3CertificateGenerator();
        generator.SetSerialNumber(serial);
        generator.SetIssuerDN(caCert.SubjectDN);
        generator.SetSubjectDN(CertificationRequestInfo.GetInstance(Asn1Sequence.GetInstance(csr.ToAsn1Object())[0]).Subject);
        generator.SetNotBefore(notBefore);
        generator.SetNotAfter(notAfter);
        generator.SetPublicKey(csr.GetPublicKey());
        foreach (var extension in extensions) generator.AddExtension(extension.Oid, extension.IsCritical, extension.Value.GetOctets());
        return generator.Generate(new Asn1SignatureFactory(SignatureAlgorithm(digest), caKey));
    }
    private static string SignatureAlgorithm(string digest) =>
        digest.Replace("-", string.Empty).ToUpperInvariant() is "SHA256" or "SHA256WITHRSA" ? "SHA256WITHRSA"
            : throw new OpenSslException($"不支持的摘要 \"{digest}\"；只支持 SHA-256。");
}
```
- [ ] **Step 6: 本段不接线**：`req` 尚未接入 `OpenSslCommand`，4 条用例仍 FAIL，由本任务 Step 8 接入 `ReqCommand` 后转绿。本段代码含三处需按 BC 2.6.2 实际形态核对的调用：`Pkcs10CertificationRequest` 的构造重载（是否接受 `Asn1SignatureFactory` + `X509Name` + `AsymmetricKeyParameter` + `Asn1Set`）、`X509V3CertificateGenerator` 的 setter 名（`SetSubjectDN`/`SetIssuerDN`/`SetSerialNumber`）、`AttributeX509` 的命名空间 `Org.BouncyCastle.Asn1.X509`。编译不过时按实际签名改写，摘要仍固定 SHA-256WITHRSA。
- [ ] **Step 7: 新建 `OpenSsl/Commands/ReqCommand.cs`**

banner 一句说明：按 Task 1 模板补头，`唯一标识` 用 `[guid]::NewGuid().ToString()` 现生成，`CLR版本`/`版本号` 写 `net10.0`，`命名空间` 写 `SAEA.Sockets.SslTlsTool.OpenSsl.Commands`，`文件名` 写 `ReqCommand`。`-sha256` 是唯一摘要，收到即按 SHA-256 签，不另分支；`-x509` 自签不传 `-addext` 时不写 SAN；`-keyout` 只在 `-newkey` 模式落盘私钥，未给 `-nodes` 且给了 `-passout` 时写加密 PKCS#8。

```csharp
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace SAEA.Sockets.SslTlsTool.OpenSsl.Commands;

/// <summary>req：新建私钥并输出 CSR，或用 -x509 直接自签成证书。</summary>
internal static class ReqCommand
{
    private static readonly string[] Allowed =
    {
        "-new", "-x509", "-newkey", "-key", "-keyout", "-nodes", "-noenc",
        "-days", "-subj", "-addext", "-sha256", "-out", "-passout",
    };

    public static int Run(string[] args, bool interactive)
    {
        var reader = new OptionReader("req", args, Allowed);
        var selfSign = reader.Has("-x509");
        // -nodes 与 -noenc 是 openssl 的同义开关，下文统一按 noEncrypt 判断。
        var noEncrypt = reader.Has("-nodes") || reader.Has("-noenc");
        if (!reader.Has("-new") && !selfSign)
            throw new OpenSslException("req: -new 与 -x509 至少给一个。");
        if (reader.Has("-new") && selfSign)
            throw new OpenSslException("req: -new 与 -x509 不能同时使用；openssl 允许，本工具为简化只支持二选一。");
        if (noEncrypt && reader.Has("-passout"))
            throw new OpenSslException("req: -nodes/-noenc 与 -passout 不能同时使用。");
        if (reader.Has("-newkey") && reader.Has("-key"))
            throw new OpenSslException("req: -newkey 与 -key 不能同时使用。");
        if (reader.Has("-newkey") && !reader.Has("-keyout"))
            throw new OpenSslException("req: -newkey 必须配 -keyout。");
        if (!reader.Has("-newkey") && reader.Has("-keyout"))
            Console.Error.WriteLine("req: 未给 -newkey，忽略 -keyout，不覆盖既有私钥。");
        var subject = SubjectNameParser.Parse(reader.GetRequired("-subj"));
        var extensions = ExtensionSpec.ParseAddext(reader.GetAll("-addext"));
        var key = reader.Has("-newkey") ? Generate(reader.GetRequired("-newkey")) : ReadKey(reader.GetRequired("-key"));
        if (reader.Has("-newkey"))
        {
            string? passOut = null;
            if (reader.Has("-passout"))
            {
                passOut = PasswordResolver.Resolve(reader.Get("-passout"), reader.GetRequired("-keyout"), true, interactive);
                if (string.IsNullOrEmpty(passOut)) throw new OpenSslException("req: PKCS#8 加密口令不能为空。");
            }
            // openssl 在两者都缺时会交互要口令；本工具不做隐式交互，改为明文写出并提示，避免非交互脚本挂起。
            else if (!noEncrypt)
                Console.Error.WriteLine("req: 未给 -nodes/-noenc 也未给 -passout，私钥按明文写出；需要加密请给 -passout。");
            File.WriteAllBytes(reader.GetRequired("-keyout"), KeyCodec.WritePkcs8(key, passOut, "aes-256-cbc", false));
        }
        var payload = selfSign
            ? PemCodec.Encode("CERTIFICATE", SignSelf(subject, key, extensions, reader.GetInt("-days", 30)).GetEncoded())
            : PemCodec.Encode("CERTIFICATE REQUEST", CsrCodec.Create(key, subject, extensions, "SHA256").GetEncoded());
        WriteOutput(reader.Get("-out"), payload);
        return 0;
    }

    private static AsymmetricKeyParameter Generate(string spec)
    {
        if (!spec.StartsWith("rsa:", StringComparison.Ordinal)
            || !int.TryParse(spec.Substring(4), out var bits)
            || (bits != 1024 && bits != 2048 && bits != 3072 && bits != 4096))
            throw new OpenSslException("req: -newkey 只支持 rsa:1024|2048|3072|4096。");
        var generator = new RsaKeyPairGenerator();
        generator.Init(new KeyGenerationParameters(new SecureRandom(), bits));
        return generator.GenerateKeyPair().Private;
    }

    private static AsymmetricKeyParameter ReadKey(string path) => File.Exists(path)
        // req 无 -passin，只接受明文私钥。
        ? KeyCodec.ReadPrivateKey(File.ReadAllBytes(path), null, false)
        : throw new OpenSslException($"error: 找不到文件 \"{path}\"。");

    private static X509Certificate SignSelf(X509Name subject, AsymmetricKeyParameter key, IList<X509Extension> extensions, int days)
    {
        if (days <= 0) throw new OpenSslException("req: -days 必须是正整数。");
        var rsa = key as RsaPrivateCrtKeyParameters ?? throw new OpenSslException("req: 自签只支持 RSA 私钥。");
        var generator = new X509V3CertificateGenerator();
        generator.SetSerialNumber(new BigInteger(64, new SecureRandom()));
        // 自签的 issuer 与 subject 一致，没有 CA 参与。
        generator.SetIssuerDN(subject);
        generator.SetSubjectDN(subject);
        generator.SetNotBefore(DateTime.UtcNow.AddMinutes(-5));
        generator.SetNotAfter(DateTime.UtcNow.AddDays(days));
        generator.SetPublicKey(new RsaKeyParameters(false, rsa.Modulus, rsa.PublicExponent));
        foreach (var extension in extensions) generator.AddExtension(extension.Oid, extension.IsCritical, extension.Value.GetOctets());
        return generator.Generate(new Asn1SignatureFactory("SHA256WITHRSA", key));
    }

    private static void WriteOutput(string? path, byte[] payload)
    {
        if (path is null) { Console.Out.Write(Encoding.ASCII.GetString(payload)); return; }
        File.WriteAllBytes(path, payload);
    }
}
```

- [ ] **Step 8: 补 `OpenSslUsage` 帮助并接线 `OpenSslCommand`**

`OpenSslUsage.For` 的 `switch` 加：

```csharp
            case "req":
                return "用法：SAEA.Sockets.SslTlsTool req [选项]\n生成 CSR（-new）或直接自签证书（-x509）；-out 缺省 stdout，-newkey 生成的私钥写 -keyout。\n"
                    + "选项：-new、-x509（二选一）、-newkey rsa:1024|2048|3072|4096（必须配 -keyout）、-key、-nodes/-noenc、-days（默认 30）、-subj、-addext、-sha256、-out、-passout。\n";
```

`OpenSslCommand.Run` 的 `switch` 加：

```csharp
                case "req":
                    return ReqCommand.Run(tail, interactive);
```

- [ ] **Step 9: 脚本改断言并补用例**

把 Step 1 第 3 条用例整体替换为完整断言（CSR 头 + SAN 扩展 OID `2.5.29.17` 的 DER 编码 `0603551D11`），并在其后追加 `-new`/`-x509` 互斥用例：

```powershell
    Invoke-Case -Name 'req -addext subjectAltName 生成 CSR 且含 SAN 扩展' -Body { $o = Join-Path $tmp 'san.csr'; $r = Invoke-Tool -Arguments @('req', '-new', '-newkey', 'rsa:2048', '-keyout', (Join-Path $tmp 'san.pem'), '-subj', '/CN=localhost', '-addext', 'subjectAltName=DNS:localhost,IP:127.0.0.1', '-out', $o); $der = [Convert]::FromBase64String(((Get-Content $o -Raw) -replace '-----[^-]+-----', '') -replace '\s', ''); $r.ExitCode -eq 0 -and (Get-Content $o -Raw) -like '*BEGIN CERTIFICATE REQUEST*' -and [BitConverter]::ToString($der).Replace('-', '').Contains('0603551D11') }
    Invoke-Case -Name 'req -new 与 -x509 同给退出 1' -Body { $r = Invoke-Tool -Arguments @('req', '-new', '-x509', '-newkey', 'rsa:2048', '-keyout', (Join-Path $tmp 'both.pem'), '-subj', '/CN=localhost'); $r.ExitCode -eq 1 -and $r.StdErr -like '*不能同时使用*' }
```

Step 1 的 4 条 req 用例此前因 `req` 未接线全部 FAIL，本步接线后一并转绿。

- [ ] **Step 10: 构建并跑脚本**

```bash
dotnet build Src/SAEA.Sockets.SslTlsTool/SAEA.Sockets.SslTlsTool.csproj -c Release
pwsh Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1
```

0 errors；末行 `21 passed, 0 failed`，退出码 0（Task 2 的 9 条 + Task 3 的 7 条 + Task 4 的 4 条 + 本步新增 1 条；Step 1 第 3 条只是改断言，不增条数）。

- [ ] **Step 11: 提交**

```bash
git add Src/SAEA.Sockets.SslTlsTool/OpenSsl/SubjectNameParser.cs Src/SAEA.Sockets.SslTlsTool/OpenSsl/ExtensionSpec.cs Src/SAEA.Sockets.SslTlsTool/OpenSsl/MiniConfigReader.cs Src/SAEA.Sockets.SslTlsTool/OpenSsl/CsrCodec.cs Src/SAEA.Sockets.SslTlsTool/OpenSsl/Commands/ReqCommand.cs Src/SAEA.Sockets.SslTlsTool/OpenSsl/OpenSslUsage.cs Src/SAEA.Sockets.SslTlsTool/OpenSsl/OpenSslCommand.cs Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1
git commit -m "feat(ssltls): 支持 req 生成 CSR 与自签证书"
```

### Task 5: x509 -req 用 CA 签发 CSR

**Files:**

- 修改 `Src/SAEA.Sockets.SslTlsTool/OpenSsl/Commands/X509Command.cs`（`-req` 分支）、`Src/SAEA.Sockets.SslTlsTool/OpenSsl/OpenSslUsage.cs`（签发行补 `-passin`）、`Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1`（追加 1 条用例）
- 新建 `Src/SAEA.Sockets.SslTlsTool/OpenSsl/SerialFile.cs`

`SerialFile.cs` 的 banner 用一句说明代替整段：按 Task 1 模板补头，`唯一标识` 用 `[guid]::NewGuid().ToString()`，`CLR版本`/`版本号` 写 `net10.0`，`命名空间` 写 `SAEA.Sockets.SslTlsTool.OpenSsl`，`文件名` 写 `SerialFile`。

- [ ] **Step 1: 新建 `OpenSsl/SerialFile.cs`**

序列号文件固定为 `<CA 文件名>.srl`（设计 §8.2）：`.crt`/`.pem`/`.cer` 换成 `.srl`，其余原样追加 `.srl`；`-CAcreateserial` 读旧值加 1 并回写大写十六进制加一个换行，未给时只在文件存在时读取，否则取 64 位正随机数且不落盘。

```csharp
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;

namespace SAEA.Sockets.SslTlsTool.OpenSsl;

/// <summary>CA 序列号文件：旧值加一后回写，或取 64 位正随机数。</summary>
internal static class SerialFile
{
    public static BigInteger Next(string caPath, bool createSerial)
    {
        var path = PathFor(caPath);
        try
        {
            if (File.Exists(path))
            {
                var old = BigInteger.Parse(File.ReadAllText(path).Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                if (!createSerial) return old;
                var next = old + BigInteger.One;
                File.WriteAllText(path, next.ToString("X", CultureInfo.InvariantCulture) + "\n");
                return next;
            }
            if (!createSerial) return RandomValue();
            File.WriteAllText(path, "1\n");
            return BigInteger.One;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            throw new OpenSslException($"x509: 无法读写序列号文件 \"{path}\"：{ex.Message}");
        }
    }

    public static string PathFor(string caPath)
    {
        var extension = Path.GetExtension(caPath);
        return extension.Equals(".crt", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".pem", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".cer", StringComparison.OrdinalIgnoreCase)
            ? caPath.Substring(0, caPath.Length - extension.Length) + ".srl"
            : caPath + ".srl";
    }

    private static BigInteger RandomValue()
    {
        var bytes = RandomNumberGenerator.GetBytes(8);
        bytes[0] &= 0x7F; // 最高位清零，保证序列号为正数。
        var value = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
        return value.IsZero ? BigInteger.One : value;
    }
}
```

- [ ] **Step 2: 把 `X509Command` 的 `-req` 临时分支换成签发实现**

白名单加 `-passin`，并把它一并放进「只与 -req 一起使用」的检查；`-CAserial` 仍不进白名单，由 `OptionReader` 报 `error: 不支持的参数 "-CAserial"；支持的参数见 -help。`（比设计 §8.2 的专用文案更早拦截，语义一致）。把 `if (reader.Has("-req"))` 的两行占位换成 `return SignRequest(reader, interactive);`，并在类顶部补 `using Org.BouncyCastle.Asn1.X509;` 与 `using Org.BouncyCastle.Pkcs;`；`ReadInput` 末尾改用共用的 `ReadFile`，让 `-CA`/`-CAkey` 的缺文件文案与 `-in` 一致。证书主体取 CSR 的 subject、issuer 取 CA、公钥取 CSR，摘要固定 SHA-256；`-copy_extensions copy` 与 `-extfile` 同给报错，`-extfile` 与 `-extensions` 必须成对；输出沿用 `IsDer`/`WriteOutput`。

```csharp
    private static int SignRequest(OptionReader reader, bool interactive)
    {
        var copy = reader.Get("-copy_extensions");
        if (copy is not null && copy != "copy" && copy != "none")
            throw new OpenSslException($"x509: -copy_extensions 只支持 copy 或 none，实际为 \"{copy}\"。");
        if (copy == "copy" && reader.Has("-extfile"))
            throw new OpenSslException("x509: -copy_extensions 与 -extfile 不能同时使用。");
        if (reader.Has("-extfile") != reader.Has("-extensions"))
            throw new OpenSslException("x509: -extfile 与 -extensions 必须同时给出。");
        var days = reader.GetInt("-days", 30);
        if (days <= 0) throw new OpenSslException("x509: -days 必须是正整数。");
        var derIn = IsDer(reader, "-inform");
        var csr = CsrCodec.Read(ReadInput(reader, derIn), derIn);
        var caPath = reader.GetRequired("-CA");
        var caCert = CertificateCodec.ReadFirst(ReadFile(caPath), false);
        var keyPath = reader.GetRequired("-CAkey");
        // 未给 -passin 时按明文私钥读取，不隐式提示，避免脚本卡在终端。
        var key = KeyCodec.ReadPrivateKey(ReadFile(keyPath), reader.Has("-passin")
            ? PasswordResolver.Resolve(reader.Get("-passin"), keyPath, false, interactive)
            : null, false);
        var extensions = ExtensionsFor(reader, csr, copy);
        var cert = CsrCodec.Sign(csr, key, caCert, SerialFile.Next(caPath, reader.Has("-CAcreateserial")),
            DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddDays(days), extensions, "SHA256");
        var derOut = IsDer(reader, "-outform");
        WriteOutput(reader, derOut, CertificateCodec.Write(cert, derOut));
        return 0;
    }

    private static IList<X509Extension> ExtensionsFor(OptionReader reader, Pkcs10CertificationRequest csr, string? copy) =>
        reader.Has("-extfile")
            ? ExtensionSpec.FromConfig(MiniConfigReader.Load(reader.GetRequired("-extfile")), reader.GetRequired("-extensions"))
            : copy == "copy" ? ExtensionSpec.CopyFromRequest(csr) : new List<X509Extension>();

    private static byte[] ReadFile(string path) =>
        File.Exists(path) ? File.ReadAllBytes(path) : throw new OpenSslException($"error: 找不到文件 \"{path}\"。");
```

`OpenSslUsage` 的 `x509` 帮助只改签发行：

```csharp
                    + "签发：-req、-CA <文件>、-CAkey <文件>、-passin <口令源>、-CAcreateserial、-days <正整数>、-extfile <文件>、-extensions <段名>、-copy_extensions copy|none\n"
```

- [ ] **Step 3: 脚本追加签发用例**

在 `try {` 块内、req 用例之后插入。CA 用 `req -x509 -newkey` 现造：`--ca` 属遗留分支，产物与参数组合不受本计划约束；`req -x509` 与叶子签发共用本任务的代码路径，CA 的 subject 与 `.srl` 落点都可控。`<ca>.srl` 断言用 `-cmatch` 保证大写；SAN 一致性用「叶子 SAN 的 DER 原样出现在 CSR 的 DER 里」比对，避免脚本调用 `CertificateRequest.LoadSigningRequestPem`（其参数是 `ReadOnlySpan<char>`，PowerShell 不绑定）。

```powershell
    $ca = Join-Path $tmp 'ca.crt'; $caKey = Join-Path $tmp 'ca.key'
    $csr = Join-Path $tmp 'leaf.csr'; $leaf = Join-Path $tmp 'leaf.crt'; $srl = Join-Path $tmp 'ca.srl'
    $null = Invoke-Tool -Arguments @('req', '-x509', '-newkey', 'rsa:2048', '-keyout', $caKey, '-subj', '/CN=SAEA Test CA', '-days', '3650', '-out', $ca)
    $null = Invoke-Tool -Arguments @('req', '-new', '-newkey', 'rsa:2048', '-keyout', (Join-Path $tmp 'leaf.key'), '-subj', '/CN=localhost', '-addext', 'subjectAltName=DNS:localhost,IP:127.0.0.1', '-out', $csr)
    Invoke-Case -Name 'x509 -req 用 CA 签发 CSR 并写序列号文件' -Body {
        $r = Invoke-Tool -Arguments @('x509', '-req', '-in', $csr, '-CA', $ca, '-CAkey', $caKey, '-CAcreateserial', '-days', '30', '-copy_extensions', 'copy', '-out', $leaf)
        $caCert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPemFile($ca)
        $leafCert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPemFile($leaf)
        $leafSan = ($leafCert.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.17' }).RawData
        $csrDer = [Convert]::FromBase64String(((Get-Content -LiteralPath $csr -Raw) -replace '-----[^-]+-----', '') -replace '\s', '')
        $r.ExitCode -eq 0 -and (Test-Path -LiteralPath $srl) -and
            ((Get-Content -LiteralPath $srl -Raw).Trim() -cmatch '^[0-9A-F]+$') -and
            ($leafCert.Issuer -eq $caCert.Subject) -and ($leafSan.Length -gt 0) -and
            ([Convert]::ToHexString($csrDer).Contains([Convert]::ToHexString($leafSan)))
    }
```

- [ ] **Step 4: 构建并跑脚本**

```bash
dotnet build Src/SAEA.Sockets.SslTlsTool/SAEA.Sockets.SslTlsTool.csproj -c Release
pwsh Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1
```

0 errors；末行 `22 passed, 0 failed`，退出码 0（Task 4 的 21 条 + 本步 1 条）。

- [ ] **Step 5: 提交**

```bash
git add Src/SAEA.Sockets.SslTlsTool/OpenSsl/SerialFile.cs Src/SAEA.Sockets.SslTlsTool/OpenSsl/Commands/X509Command.cs Src/SAEA.Sockets.SslTlsTool/OpenSsl/OpenSslUsage.cs Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1
git commit -m "feat(ssltls): 支持 x509 -req 用 CA 签发 CSR"
```

### Task 6: pkcs12（PFX/P12 读写）

**Files:**
- 新建 `Src/SAEA.Sockets.SslTlsTool/OpenSsl/Pkcs12Codec.cs`
- 修改 `Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1`（命令类、usage 与 switch 接线留给本任务 Step 4–5）

默认参数钉死 openssl 3：私钥与证书都用 AES-256-CBC + PBKDF2 迭代 2048 + HMAC-SHA-256，MAC 迭代 2048，salt 8 字节；`useLegacy` 改走 3DES 私钥 / RC2-40 证书 / SHA-1；`password` 为 `null` 时不加密也不写 MAC，空串按空口令加密。

- [ ] **Step 1: 脚本追加导出与读回用例**
复用 Task 5 已产出的 `$tmp`、`$leaf` 与 `leaf.key`；命令尚未接线，两条用例此时应 FAIL。

```powershell
    $pfx = Join-Path $tmp 'leaf.pfx'
    Invoke-Case -Name 'pkcs12 -export 导出 PFX' -Body {
        $r = Invoke-Tool -Arguments @('pkcs12', '-export', '-in', $leaf, '-inkey', (Join-Path $tmp 'leaf.key'), '-out', $pfx, '-passout', 'pass:test')
        $r.ExitCode -eq 0 -and $r.StdErr -notmatch 'error' -and (Test-Path -LiteralPath $pfx) -and (Get-Item -LiteralPath $pfx).Length -gt 0
    }
    Invoke-Case -Name 'pkcs12 -in 读回私钥' -Body {
        $pfxKey = Join-Path $tmp 'pfx.key'
        $r = Invoke-Tool -Arguments @('pkcs12', '-in', $pfx, '-passin', 'pass:test', '-nocerts', '-nodes', '-out', $pfxKey)
        $r.ExitCode -eq 0 -and (Get-Content -LiteralPath $pfxKey -Raw) -match '-----BEGIN PRIVATE KEY-----'
    }
```
- [ ] **Step 2: 写 Pkcs12Codec.cs**
`Pkcs12Store.Save` 在 BC 2.6.2 中硬编码迭代 1024、salt 20 字节、MAC 用 SHA-1 且无 setter，所以 `Save` 全部手工构造 `Pfx` + `ContentInfo` + `SafeBag` + `AuthenticatedSafe` + `MacData`；`Load` 仍用 `Pkcs12StoreBuilder`。
```csharp
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.Oiw;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace SAEA.Sockets.SslTlsTool.OpenSsl;

/// <summary>PKCS#12 打包与拆包，默认对齐 openssl 3 的算法、迭代与 MAC 参数。</summary>
internal sealed class Pkcs12Codec
{
    private const int Iterations = 2048;
    private const int SaltSize = 8;
    private readonly bool _useLegacy;
    private readonly byte[]? _saltOverride;
    private readonly byte[]? _macSaltOverride;
    private readonly SecureRandom _random = new();
    private readonly List<(X509Certificate Cert, string? Alias, bool IsLeaf)> _certs = new();
    private readonly List<(AsymmetricKeyParameter Key, X509Certificate Leaf, string? Alias)> _keys = new();

    public Pkcs12Codec(bool useLegacy) : this(useLegacy, null, null) { }

    // salt 固定只用于结构级对拍，CLI 不暴露该入口。
    internal Pkcs12Codec(bool useLegacy, byte[]? saltOverride, byte[]? macSaltOverride) =>
        (_useLegacy, _saltOverride, _macSaltOverride) = (useLegacy, saltOverride, macSaltOverride);

    public void AddCertificate(X509Certificate cert, string? alias, bool isLeaf) => _certs.Add((cert, alias, isLeaf));

    public void AddChain(IEnumerable<X509Certificate> chain, string? aliasPrefix) => _certs.AddRange(chain.Select(cert => (cert, aliasPrefix, false)));

    public void AddPrivateKey(AsymmetricKeyParameter key, X509Certificate leaf, string? alias) => _keys.Add((key, leaf, alias));

    /// <summary>password 为 null 时不加密、不写 MAC；空串按空口令加密。</summary>
    public byte[] Save(string? password)
    {
        var keys = new Asn1EncodableVector();
        foreach (var (key, leaf, alias) in _keys)
            keys.Add(password is null
                ? new SafeBag(PkcsObjectIdentifiers.KeyBag, PrivateKeyInfoFactory.CreatePrivateKeyInfo(key), Attributes(alias, leaf))
                : new SafeBag(PkcsObjectIdentifiers.Pkcs8ShroudedKeyBag, EncryptKey(key, password, _saltOverride), Attributes(alias, leaf)));
        var entries = _certs.OrderBy(entry => entry.IsLeaf ? 0 : 1)
            .Concat(_keys.Where(key => _certs.All(entry => !entry.Cert.Equals(key.Leaf))).Select(key => (key.Leaf, key.Alias, false)));
        var certs = new Asn1EncodableVector();
        foreach (var (cert, alias, _) in entries)
            certs.Add(new SafeBag(PkcsObjectIdentifiers.CertBag, new CertBag(PkcsObjectIdentifiers.X509Certificate, new DerOctetString(cert.GetEncoded())), Attributes(alias, cert)));
        var certBytes = new DerSequence(certs).GetDerEncoded();
        var certsInfo = password is null ? new ContentInfo(PkcsObjectIdentifiers.Data, new BerOctetString(certBytes)) : EncryptedCerts(certBytes, password);
        var keysInfo = new ContentInfo(PkcsObjectIdentifiers.Data, new BerOctetString(new DerSequence(keys).GetDerEncoded()));
        var authSafe = new AuthenticatedSafe(new[] { keysInfo, certsInfo }).GetEncoded();
        var pfx = new Pfx(new ContentInfo(PkcsObjectIdentifiers.Data, new BerOctetString(authSafe)), password is null ? null : Mac(authSafe, password, NextSalt(_macSaltOverride)));
        // GetEncoded 默认 DER；PFX 是二进制容器，不套 PEM。
        return pfx.GetEncoded();
    }

    /// <summary>读取 PKCS#12，私钥取首个 key 条目，证书按条目与链收集。</summary>
    public static Pkcs12Bundle Load(byte[] data, string? password)
    {
        var store = new Pkcs12StoreBuilder().Build();
        store.Load(new MemoryStream(data), password?.ToCharArray());
        var keys = store.Aliases.Where(store.IsKeyEntry).Select(alias => store.GetKey(alias).Key).ToList();
        var certs = store.Aliases.Where(store.IsKeyEntry)
            .SelectMany(alias => (store.GetCertificateChain(alias) ?? Array.Empty<X509CertificateEntry>()).Select(entry => entry.Certificate))
            .Concat(store.Aliases.Where(store.IsCertificateEntry).Select(alias => store.GetCertificate(alias).Certificate)).Distinct().ToList();
        var leaf = certs.FirstOrDefault(cert => !IsCa(cert)) ?? certs.FirstOrDefault();
        return new Pkcs12Bundle(keys.FirstOrDefault(), leaf, certs);
    }

    private EncryptedPrivateKeyInfo EncryptKey(AsymmetricKeyParameter key, string password, byte[]? salt) => _useLegacy
        ? EncryptedPrivateKeyInfoFactory.CreateEncryptedPrivateKeyInfo(PkcsObjectIdentifiers.PbeWithShaAnd3KeyTripleDesCbc, password.ToCharArray(), NextSalt(salt), Iterations, key)
        : EncryptedPrivateKeyInfoFactory.CreateEncryptedPrivateKeyInfo(NistObjectIdentifiers.IdAes256Cbc, PkcsObjectIdentifiers.IdHmacWithSha256, password.ToCharArray(), NextSalt(salt), Iterations, _random, key);

    private ContentInfo EncryptedCerts(byte[] data, string password)
    {
        var algID = _useLegacy
            ? new AlgorithmIdentifier(PkcsObjectIdentifiers.PbewithShaAnd40BitRC2Cbc, new Pkcs12PbeParams(NextSalt(_saltOverride), Iterations))
            : new AlgorithmIdentifier(PkcsObjectIdentifiers.IdPbeS2, PbeUtilities.GenerateAlgorithmParameters(NistObjectIdentifiers.IdAes256Cbc, PkcsObjectIdentifiers.IdHmacWithSha256, NextSalt(_saltOverride), Iterations, _random));
        if (PbeUtilities.CreateEngine(algID) is not IBufferedCipher cipher) throw new OpenSslException("error: 不支持的证书加密算法。");
        cipher.Init(true, PbeUtilities.GenerateCipherParameters(algID, password.ToCharArray()));
        return new ContentInfo(PkcsObjectIdentifiers.EncryptedData, new EncryptedData(PkcsObjectIdentifiers.Data, algID, new BerOctetString(cipher.DoFinal(data))));
    }

    private MacData Mac(byte[] authSafe, string password, byte[] salt)
    {
        IDigest digest = _useLegacy ? new Sha1Digest() : new Sha256Digest();
        var generator = new Pkcs12ParametersGenerator(digest);
        generator.Init(PbeParametersGenerator.Pkcs12PasswordToBytes(password.ToCharArray()), salt, Iterations);
        var hmac = new HMac(digest);
        hmac.Init(generator.GenerateDerivedMacParameters(digest.GetDigestSize() * 8));
        var oid = _useLegacy ? OiwObjectIdentifiers.IdSha1 : NistObjectIdentifiers.IdSha256;
        return new MacData(new DigestInfo(new AlgorithmIdentifier(oid, DerNull.Instance), MacUtilities.DoFinal(hmac, authSafe)), salt, Iterations);
    }

    private static Asn1Set Attributes(string? alias, X509Certificate cert)
    {
        var vector = new Asn1EncodableVector();
        if (!string.IsNullOrEmpty(alias)) vector.Add(new DerSequence(PkcsObjectIdentifiers.Pkcs9AtFriendlyName, new DerSet(new DerBmpString(alias))));
        var keyId = DigestUtilities.CalculateDigest("SHA-1", cert.CertificateStructure.SubjectPublicKeyInfo.PublicKey.GetBytes());
        vector.Add(new DerSequence(PkcsObjectIdentifiers.Pkcs9AtLocalKeyID, new DerSet(new DerOctetString(keyId))));
        return new DerSet(vector);
    }

    private static bool IsCa(X509Certificate cert) => cert.GetExtensionValue(X509Extensions.BasicConstraints) is Asn1OctetString extension
        && BasicConstraints.GetInstance(Asn1Object.FromByteArray(extension.GetOctets())).IsCA();

    private byte[] NextSalt(byte[]? over) => over ?? SecureRandom.GetNextBytes(_random, SaltSize);
}

internal sealed record Pkcs12Bundle(AsymmetricKeyParameter? PrivateKey, X509Certificate? Leaf, IReadOnlyList<X509Certificate> Chain);
```
- [ ] **Step 3: 构建**
```bash
dotnet build Src/SAEA.Sockets.SslTlsTool/SAEA.Sockets.SslTlsTool.csproj -c Release
```
0 errors；命令尚未接线，Step 1 用例仍 FAIL，属预期，由本任务 Step 4–5 接线后转绿。

本步骤必须先编译通过再进入 Step 4。需要核对的 BC 2.6.2 签名：`PbeUtilities.GenerateAlgorithmParameters` 的 `(scheme, digest, salt, iteration, random)` 重载、`PbeUtilities.CreateEngine(AlgorithmIdentifier)`、`PbeUtilities.GenerateCipherParameters(AlgorithmIdentifier, char[])`、`EncryptedPrivateKeyInfoFactory.CreateEncryptedPrivateKeyInfo` 的 PBES2 重载，以及 `MacData` / `EncryptedData` / `CertBag` / `SafeBag` 的构造函数。任一签名与上述代码不符时，按 2.6.2 的实际形态改写调用，算法、迭代 2048、salt 8 字节与 OID 保持不变，并在 Task 6 完成说明里记录实际差异。

- [ ] **Step 4: 新建 `OpenSsl/Commands/Pkcs12Command.cs`**

按 Step 3 的 banner 规则补头，`文件名` 写 `Pkcs12Command`。`-noout` 不在白名单（`-info` 摘要写 stderr，PEM 仍按 `-out`/stdout 输出）；`-nodes` 让读取输出的私钥不加密，`-nocerts`/`-clcerts`/`-cacerts` 互斥，`-info` 导出时打印算法名、读取时只列条目数。读取模式不接受 `-passout`（不区分读/写口令），输出私钥按 `-passin` 的口令重新加密；未给 `-passin` 时先按无口令装载，失败才索要口令；交互模式下导出 PFX 这类二进制输出必须给 `-out`。

```csharp
namespace SAEA.Sockets.SslTlsTool.OpenSsl.Commands;

/// <summary>pkcs12：导出 PFX，或将 PFX 拆成私钥与证书 PEM。</summary>
internal static class Pkcs12Command
{
    private static readonly string[] Allowed =
        { "-export", "-in", "-inkey", "-out", "-name", "-passin", "-passout", "-nodes", "-nocerts", "-nokeys", "-clcerts", "-cacerts", "-certfile", "-legacy", "-nomac", "-info" };
    public static int Run(string[] args, bool interactive)
    {
        var reader = new OptionReader("pkcs12", args, Allowed);
        return reader.Has("-export") ? Export(reader, interactive) : Read(reader, interactive);
    }
    private static int Export(OptionReader reader, bool interactive)
    {
        if (!reader.Has("-in") || !reader.Has("-inkey")) throw new OpenSslException("pkcs12: -export 需要 -in 与 -inkey。");
        if (interactive && !reader.Has("-out")) throw new OpenSslException("交互模式下二进制输出必须给 -out。");
        foreach (var option in new[] { "-nocerts", "-nokeys", "-clcerts", "-cacerts" }) if (reader.Has(option)) throw new OpenSslException($"pkcs12: 参数 \"{option}\" 只用于读取模式。");
        if (reader.Has("-passout") && (reader.Has("-nodes") || reader.Has("-nomac")))
            throw new OpenSslException("pkcs12: -nodes/-nomac 与 -passout 只能选一个。");
        var leaf = CertificateCodec.ReadFirst(ReadFile(reader.GetRequired("-in")), der: false);
        var keyPath = reader.GetRequired("-inkey");
        var keyPassword = reader.Has("-passin")
            ? PasswordResolver.Resolve(reader.Get("-passin"), keyPath, false, interactive) : null;
        var key = KeyCodec.ReadPrivateKey(ReadFile(keyPath), keyPassword, der: false);
        var legacy = reader.Has("-legacy"); var alias = reader.Get("-name");
        var codec = new Pkcs12Codec(legacy);
        codec.AddCertificate(leaf, alias, isLeaf: true);
        var certCount = 1;
        foreach (var path in reader.GetAll("-certfile"))
            foreach (var cert in CertificateCodec.ReadAll(ReadFile(path), der: false)) { codec.AddCertificate(cert, null, isLeaf: false); certCount++; }
        codec.AddPrivateKey(key, leaf, alias);
        // codec 只在 password 为 null 时不加密、不写 MAC，-nodes/-nomac 因此不解析 -passout。
        var password = reader.Has("-nodes") || reader.Has("-nomac") ? null
            : PasswordResolver.Resolve(reader.Get("-passout"), reader.Get("-out") ?? "stdout", true, interactive);
        // 给了 -passout 却没解析出口令（如 env: 变量缺失）时不能静默产出无保护 PFX。
        if (reader.Has("-passout") && password is null)
            throw new OpenSslException("pkcs12: -passout 未提供口令；不能生成无保护的 PFX。");
        var data = codec.Save(password);
        if (reader.Has("-info"))
            Console.Error.WriteLine($"PKCS#12 条目：1 个私钥、{certCount} 张证书；算法：{(legacy ? "3DES/RC2-40" : "AES-256-CBC")}，MAC：{(password is null ? "无" : legacy ? "HMAC-SHA-1" : "HMAC-SHA-256")}。");
        // 二进制写 stdout 前先清空文本缓冲，避免 -info 摘要混入。
        if (reader.Has("-out")) File.WriteAllBytes(reader.GetRequired("-out"), data);
        else { Console.Out.Flush(); using var stdout = Console.OpenStandardOutput(); stdout.Write(data, 0, data.Length); }
        return 0;
    }
    private static int Read(OptionReader reader, bool interactive)
    {
        foreach (var option in new[] { "-passout", "-legacy", "-name", "-certfile", "-nomac" }) if (reader.Has(option)) throw new OpenSslException($"pkcs12: 参数 \"{option}\" 只用于 -export。");
        var only = new[] { "-nocerts", "-clcerts", "-cacerts" }.Where(reader.Has).ToArray();
        if (only.Length > 1) throw new OpenSslException("pkcs12: -nocerts/-clcerts/-cacerts 只能选一个。");
        var input = reader.Has("-in") ? ReadFile(reader.GetRequired("-in")) : ReadStdin();
        var passIn = reader.Get("-passin");
        Pkcs12Bundle bundle;
        string? password;
        if (passIn is not null)
        {
            password = PasswordResolver.Resolve(passIn, reader.Get("-in") ?? "stdin", false, interactive);
            bundle = Load(input, password);
        }
        else
        {
            // -export -nodes 的产物不带 MAC，先按无口令装载；失败再索要口令，避免对无口令 PFX 空转一次提示。
            try { bundle = Pkcs12Codec.Load(input, null); password = null; }
            catch (Exception)
            {
                password = PasswordResolver.Resolve(null, reader.Get("-in") ?? "stdin", false, interactive);
                bundle = Load(input, password);
            }
        }
        var keyOnly = only.FirstOrDefault() == "-nocerts"; var leafOnly = only.FirstOrDefault() == "-clcerts"; var chainOnly = only.FirstOrDefault() == "-cacerts";
        var text = new StringBuilder();
        if (!leafOnly && !chainOnly && !reader.Has("-nokeys") && bundle.PrivateKey is { } privateKey)
            text.Append(Encoding.ASCII.GetString(KeyCodec.WritePkcs8(privateKey, reader.Has("-nodes") ? null : password, null, der: false)));
        if (!keyOnly && !chainOnly && bundle.Leaf is { } leaf) text.Append(PemCodec.Encode("CERTIFICATE", leaf.GetEncoded()));
        if (!keyOnly && !leafOnly)
            foreach (var cert in bundle.Chain.Where(cert => !cert.Equals(bundle.Leaf)))
                text.Append(PemCodec.Encode("CERTIFICATE", cert.GetEncoded()));
        if (reader.Has("-info"))
            Console.Error.WriteLine($"PKCS#12 条目：{(bundle.PrivateKey is null ? 0 : 1)} 个私钥、{bundle.Chain.Count} 张证书。");
        if (reader.Has("-out")) PemCodec.WriteFile(reader.GetRequired("-out"), text.ToString());
        else Console.Out.Write(text.ToString());
        return 0;
    }
    private static Pkcs12Bundle Load(byte[] input, string? password)
    {
        try { return Pkcs12Codec.Load(input, password); }
        catch (Exception) { throw new OpenSslException("error: 口令错误或文件损坏。"); }
    }
    private static byte[] ReadFile(string path) =>
        File.Exists(path) ? File.ReadAllBytes(path) : throw new OpenSslException($"error: 找不到文件 \"{path}\"。");
    private static byte[] ReadStdin()
    {
        using var stdin = Console.OpenStandardInput(); using var buffer = new MemoryStream();
        stdin.CopyTo(buffer);
        return buffer.ToArray();
    }
}
```

- [ ] **Step 5: 补 `OpenSslUsage` 帮助与 `OpenSslCommand` 分派**

`OpenSslUsage.For` 的 `switch` 在 `x509` 之后加：

```csharp
            case "pkcs12":
                return "用法：SAEA.Sockets.SslTlsTool pkcs12 [选项]\n"
                    + "导出：-export -in <叶子证书> -inkey <私钥> [-certfile <链>]... [-name <friendlyName>] [-passout <口令源>] [-legacy] [-nodes|-nomac]\n"
                    + "读取：-in <PFX> [-passin <口令源>] [-out <PEM>] [-nocerts|-clcerts|-cacerts] [-nokeys] [-nodes] [-info]\n"
                    + "口令源：pass:<文本>、env:<变量>、file:<路径>、stdin；-in 缺省 stdin，-out 缺省 stdout。\n"
                    + "读取输出顺序为私钥（PKCS#8）、叶子、链；-info 摘要写 stderr，-noout 不支持。\n";
```

`OpenSslCommand.cs` 的 `switch` 在 `x509` 分支后加：

```csharp
                case "pkcs12":
                    return Pkcs12Command.Run(tail, interactive);
```

- [ ] **Step 6: 脚本补齐 pkcs12 余下用例**

追加在 Task 5 用例之后，复用 Step 1 的 `$pfx`；Step 1 的两条用例随命令接线一并转绿。

```powershell
    Invoke-Case -Name 'pkcs12 -legacy 导出成功' -Body { $o = Join-Path $tmp 'legacy.pfx'; $r = Invoke-Tool -Arguments @('pkcs12', '-export', '-in', $leaf, '-inkey', (Join-Path $tmp 'leaf.key'), '-legacy', '-out', $o, '-passout', 'pass:test'); $r.ExitCode -eq 0 -and (Test-Path -LiteralPath $o) -and (Get-Item -LiteralPath $o).Length -gt 0 }
    Invoke-Case -Name 'pkcs12 -clcerts -nokeys 只输出证书' -Body { $r = Invoke-Tool -Arguments @('pkcs12', '-in', $pfx, '-passin', 'pass:test', '-clcerts', '-nokeys'); $r.ExitCode -eq 0 -and $r.StdOut -like '*BEGIN CERTIFICATE*' -and $r.StdOut -notlike '*PRIVATE KEY*' }
    Invoke-Case -Name 'pkcs12 -name 导出并读回' -Body { $named = Join-Path $tmp 'alias.pfx'; $e = Invoke-Tool -Arguments @('pkcs12', '-export', '-in', $leaf, '-inkey', (Join-Path $tmp 'leaf.key'), '-name', 'myalias', '-out', $named, '-passout', 'pass:test'); $r = Invoke-Tool -Arguments @('pkcs12', '-in', $named, '-passin', 'pass:test', '-out', (Join-Path $tmp 'alias.pem')); $e.ExitCode -eq 0 -and $r.ExitCode -eq 0 -and $r.StdErr -notmatch 'error' }
    Invoke-Case -Name 'pkcs12 错误口令退出 1 且报口令错误' -Body { $r = Invoke-Tool -Arguments @('pkcs12', '-in', $pfx, '-passin', 'pass:bad', '-out', (Join-Path $tmp 'bad.pem')); $r.ExitCode -eq 1 -and $r.StdErr -like '*口令错误*' }
    Invoke-Case -Name 'pkcs12 -export 缺 -inkey 退出 1' -Body { $r = Invoke-Tool -Arguments @('pkcs12', '-export', '-in', $leaf, '-out', (Join-Path $tmp 'nokey.pfx'), '-passout', 'pass:test'); $r.ExitCode -eq 1 -and $r.StdErr -like '*需要 -in 与 -inkey*' }
```

- [ ] **Step 7: 构建并跑脚本（期望 `29 passed, 0 failed`）**

```bash
dotnet build Src/SAEA.Sockets.SslTlsTool/SAEA.Sockets.SslTlsTool.csproj -c Release
pwsh Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1
```

构建 0 errors；脚本末行 `29 passed, 0 failed`、退出码 0（Task 5 末次 22 条 + 本任务 7 条）。

- [ ] **Step 8: 提交**

```bash
git add Src/SAEA.Sockets.SslTlsTool/OpenSsl/Pkcs12Codec.cs Src/SAEA.Sockets.SslTlsTool/OpenSsl/Commands/Pkcs12Command.cs Src/SAEA.Sockets.SslTlsTool/OpenSsl/OpenSslUsage.cs Src/SAEA.Sockets.SslTlsTool/OpenSsl/OpenSslCommand.cs Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1
git commit -m "feat(ssltls): 支持 pkcs12 读写 PFX/P12"
```

### Task 7: crl2pkcs7 与 pkcs7（certs-only PKCS#7）

**Files:**

- 新建 `Src/SAEA.Sockets.SslTlsTool/OpenSsl/Pkcs7Codec.cs`
- 修改 `Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1`（追加 3 条用例）；`Crl2Pkcs7Command`、`Pkcs7Command` 与 `OpenSslCommand`/`OpenSslUsage` 的接线留给本任务 Step 4–6

- [ ] **Step 1: 脚本追加 crl2pkcs7/pkcs7 用例（先红）**

追加在 Task 6 用例之后，复用 `$leaf`。断言取 PEM 头而非 `-outform DER` 首字节：`PKCS7` 标签是 `crl2pkcs7` 与 `pkcs7` 之间唯一靠文本传递的契约，`-print_certs` 用例直接依赖它；DER 首字节只证外层 SEQUENCE，证不了标签。第二条用例自行生成输入，不依赖上一条的副作用。

```powershell
    $p7 = Join-Path $tmp 'b.p7b'
    Invoke-Case -Name 'crl2pkcs7 -nocrl 生成 PKCS7 PEM' -Body {
        $r = Invoke-Tool -Arguments @('crl2pkcs7', '-nocrl', '-certfile', $leaf, '-out', $p7)
        $r.ExitCode -eq 0 -and (Test-Path -LiteralPath $p7) -and ((Get-Content -LiteralPath $p7 -Raw) -like '*BEGIN PKCS7*')
    }
    Invoke-Case -Name 'pkcs7 -print_certs 输出包内证书' -Body {
        $null = Invoke-Tool -Arguments @('crl2pkcs7', '-nocrl', '-certfile', $leaf, '-out', $p7)
        $r = Invoke-Tool -Arguments @('pkcs7', '-in', $p7, '-print_certs')
        $r.ExitCode -eq 0 -and $r.StdOut -like '*BEGIN CERTIFICATE*'
    }
    Invoke-Case -Name 'crl2pkcs7 缺 -nocrl 退出 1 且报不支持 CRL' -Body {
        $r = Invoke-Tool -Arguments @('crl2pkcs7', '-certfile', $leaf)
        $r.ExitCode -eq 1 -and $r.StdErr -like '*不支持读取 CRL*'
    }
```

- [ ] **Step 2: 新建 `OpenSsl/Pkcs7Codec.cs`**

`Pkcs7Codec.cs` 的 banner 用一句说明代替整段：按 Task 1 模板补头，`唯一标识` 用 `[guid]::NewGuid().ToString()`，`CLR版本`/`版本号` 写 `net10.0`，`命名空间` 写 `SAEA.Sockets.SslTlsTool.OpenSsl`，`文件名` 写 `Pkcs7Codec`。BouncyCastle 2.6.2 的 `SignedData.Certificates` 在无证书时为 `null`，必须判空；PEM 分支只认 `PKCS7` 标签，其它标签（含 `CERTIFICATE`）不做兼容猜测，没有 `PKCS7` 块时把原始字节当标准 PKCS#7 结构解析。

```csharp
using System.Text;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.X509;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace SAEA.Sockets.SslTlsTool.OpenSsl;

/// <summary>certs-only PKCS#7（P7B/P7C）的 PEM/DER 编解码。</summary>
internal static class Pkcs7Codec
{
    /// <summary>把证书集合编成 certs-only PKCS#7；der 为 false 时输出 PKCS7 PEM。</summary>
    public static byte[] CreateCertsOnly(IEnumerable<X509Certificate> certificates, bool der)
    {
        var list = certificates.ToList();
        if (list.Count == 0) throw new OpenSslException("error: 至少需要一张证书。");
        // certs-only 包没有内容与签名者，版本固定 1，摘要算法用 data（OID 1.2.840.113549.1.7.1）。
        var signedData = new SignedData(
            new DerInteger(1),
            new DerSet(),
            new ContentInfo(PkcsObjectIdentifiers.Data, null),
            new DerSet(list.Select(cert => cert.CertificateStructure).ToArray()),
            null,
            new DerSet());
        // GetEncoded 默认按 DER 编码，PEM 分支再套一层 PKCS7 标签。
        var encoded = new ContentInfo(PkcsObjectIdentifiers.SignedData, signedData).GetEncoded();
        return der ? encoded : Encoding.UTF8.GetBytes(PemCodec.Encode("PKCS7", encoded));
    }

    /// <summary>解析 certs-only PKCS#7；PEM 只认 PKCS7 标签，无该块时按原始字节解析。</summary>
    public static IReadOnlyList<X509Certificate> Read(byte[] data, bool der)
    {
        var encoded = der
            ? data
            : PemCodec.ParseAll(Encoding.UTF8.GetString(data))
                .Where(block => block.Key == "PKCS7").Select(block => block.Value).FirstOrDefault() ?? data;
        var signedData = SignedData.GetInstance(ContentInfo.GetInstance(Asn1Object.FromByteArray(encoded)).Content);
        var certificates = new List<X509Certificate>();
        if (signedData.Certificates is { } set)
            foreach (var item in set)
                certificates.Add(new X509Certificate(X509CertificateStructure.GetInstance(item)));
        return certificates;
    }
}
```

- [ ] **Step 3: 构建**

```bash
dotnet build Src/SAEA.Sockets.SslTlsTool/SAEA.Sockets.SslTlsTool.csproj -c Release
```

0 errors；本段结束时 `crl2pkcs7` 与 `pkcs7` 都未接线，Step 1 的三条用例全部 FAIL，属预期，由本任务 Step 4–6 接线后转绿。

- [ ] **Step 4: 新建 `OpenSsl/Commands/Pkcs7Command.cs`**
banner 用一句说明代替整段：`唯一标识` 用 `[guid]::NewGuid().ToString()`，`CLR版本`/`版本号` 写 `net10.0`，`命名空间` 写 `SAEA.Sockets.SslTlsTool.OpenSsl.Commands`，`文件名` 写 `Pkcs7Command`。`-noout` 只进白名单、不产生行为；DER 强制写文件，避免二进制混入 stdout 文本缓冲。

```csharp
namespace SAEA.Sockets.SslTlsTool.OpenSsl.Commands;

/// <summary>pkcs7：读取 certs-only PKCS#7，按 -print_certs 拆证书或按 -outform 重写。</summary>
internal static class Pkcs7Command
{
    private static readonly string[] Allowed = { "-in", "-inform", "-print_certs", "-out", "-outform", "-noout" };
    public static int Run(string[] args, bool interactive)
    {
        var reader = new OptionReader("pkcs7", args, Allowed);
        var derIn = IsDer(reader, "-inform"); var derOut = IsDer(reader, "-outform");
        if (reader.Has("-print_certs") && derOut)
            throw new OpenSslException("pkcs7: -print_certs 只输出 PEM 文本，不能与 -outform DER 一起用。");
        if (interactive && derOut && !reader.Has("-out"))
            throw new OpenSslException("交互模式下二进制输出必须给 -out。");
        var certificates = Pkcs7Codec.Read(ReadInput(reader), derIn);
        var data = reader.Has("-print_certs")
            ? Encoding.ASCII.GetBytes(string.Concat(certificates.Select(cert => PemCodec.Encode("CERTIFICATE", cert.GetEncoded()))))
            : Pkcs7Codec.CreateCertsOnly(certificates, derOut);
        if (reader.Has("-out")) File.WriteAllBytes(reader.GetRequired("-out"), data);
        else if (derOut) { Console.Out.Flush(); using var stdout = Console.OpenStandardOutput(); stdout.Write(data, 0, data.Length); }
        else Console.Out.Write(Encoding.ASCII.GetString(data));
        return 0;
    }
    private static byte[] ReadInput(OptionReader reader)
    {
        if (reader.Has("-in")) { var path = reader.GetRequired("-in"); return File.Exists(path) ? File.ReadAllBytes(path) : throw new OpenSslException($"error: 找不到文件 \"{path}\"。"); }
        using var stdin = Console.OpenStandardInput(); using var buffer = new MemoryStream(); stdin.CopyTo(buffer); return buffer.ToArray();
    }
    private static bool IsDer(OptionReader reader, string option)
    {
        var value = reader.Has(option) ? reader.GetRequired(option) : "PEM";
        if (value != "PEM" && value != "DER") throw new OpenSslException($"error: 参数 \"{option}\" 只支持 PEM 或 DER，实际为 \"{value}\"。");
        return value == "DER";
    }
}
```

- [ ] **Step 5: 新建 `OpenSsl/Commands/Crl2Pkcs7Command.cs`**
banner 同 Step 4，`文件名` 写 `Crl2Pkcs7Command`。缺 `-nocrl` 时不读任何文件直接报错；`-certfile` 可重复，按出现顺序累加。

```csharp
namespace SAEA.Sockets.SslTlsTool.OpenSsl.Commands;

/// <summary>crl2pkcs7：把多张证书装进 certs-only PKCS#7。</summary>
internal static class Crl2Pkcs7Command
{
    private static readonly string[] Allowed = { "-nocrl", "-certfile", "-out", "-outform" };
    public static int Run(string[] args, bool interactive)
    {
        var reader = new OptionReader("crl2pkcs7", args, Allowed);
        if (!reader.Has("-nocrl")) throw new OpenSslException("crl2pkcs7: 不支持读取 CRL，请加 -nocrl。");
        if (!reader.Has("-certfile")) throw new OpenSslException("crl2pkcs7: 需要至少一个 -certfile。");
        var der = IsDer(reader); var certificates = reader.GetAll("-certfile").SelectMany(path => CertificateCodec.ReadAll(ReadFile(path), der: false)).ToList();
        if (interactive && der && !reader.Has("-out"))
            throw new OpenSslException("交互模式下二进制输出必须给 -out。");
        var data = Pkcs7Codec.CreateCertsOnly(certificates, der);
        if (reader.Has("-out")) File.WriteAllBytes(reader.GetRequired("-out"), data);
        else if (!der) Console.Out.Write(Encoding.ASCII.GetString(data));
        else { Console.Out.Flush(); using var stdout = Console.OpenStandardOutput(); stdout.Write(data, 0, data.Length); }
        return 0;
    }
    private static byte[] ReadFile(string path) => File.Exists(path) ? File.ReadAllBytes(path) : throw new OpenSslException($"error: 找不到文件 \"{path}\"。");
    private static bool IsDer(OptionReader reader)
    {
        var value = reader.Has("-outform") ? reader.GetRequired("-outform") : "PEM";
        if (value != "PEM" && value != "DER") throw new OpenSslException($"error: 参数 \"-outform\" 只支持 PEM 或 DER，实际为 \"{value}\"。");
        return value == "DER";
    }
}
```

- [ ] **Step 6: 补 `OpenSslUsage` 帮助并接线 `OpenSslCommand`**
`OpenSslUsage.For` 的 `switch` 在 `x509` 分支后加：

```csharp
            case "crl2pkcs7":
                return "用法：SAEA.Sockets.SslTlsTool crl2pkcs7 -nocrl -certfile <文件> [-certfile <文件>]... [-out <文件>] [-outform PEM|DER]\n"
                    + "生成 certs-only PKCS#7（PEM 头 PKCS7）；-nocrl 必需，-out 缺省 stdout，-outform 缺省 PEM。\n";
            case "pkcs7":
                return "用法：SAEA.Sockets.SslTlsTool pkcs7 [选项]\n读取：-in <文件>、-inform PEM|DER（默认 stdin/PEM）；输出：-print_certs 逐张输出证书 PEM、-out <文件>、-outform PEM|DER（DER 需要 -out）\n"
                    + "-noout 为空操作：本工具不打印 subject/issuer 摘要行。\n";
```

八个子命令至此都有真实帮助文本，删除过渡占位：删掉 `OpenSslUsage.Pending(string)` 整个方法，并把 `OpenSslCommand.Run` 里 `-help` 短路中的 `try { text = OpenSslUsage.For(command); } catch (OpenSslException) { text = OpenSslUsage.Pending(command); }` 改成 `var text = OpenSslUsage.For(command);`。`For` 的 `default` 分支保留 `throw`（子命令集合与 `switch` 分支已一一对应，正常路径不可达，作为防御）。

`OpenSslCommand.cs` 的 `switch` 在 `pkcs12` 分支后加：

```csharp
                case "crl2pkcs7":
                    return Crl2Pkcs7Command.Run(tail, interactive);
                case "pkcs7":
                    return Pkcs7Command.Run(tail, interactive);
```

- [ ] **Step 7: 脚本补 `pkcs7 -outform DER` 用例（Step 1 三条随接线转绿）**
复用 `$p7`，只断言文件非空（不用首字节证标签，理由见 Step 1）：
```powershell
    Invoke-Case -Name 'pkcs7 -outform DER 有 -out 时输出非空文件' -Body {
        $d = Join-Path $tmp 'b.p7c'; $null = Invoke-Tool -Arguments @('crl2pkcs7', '-nocrl', '-certfile', $leaf, '-out', $p7)
        $r = Invoke-Tool -Arguments @('pkcs7', '-in', $p7, '-outform', 'DER', '-out', $d)
        $r.ExitCode -eq 0 -and (Test-Path -LiteralPath $d) -and (Get-Item -LiteralPath $d).Length -gt 0
    }
```

- [ ] **Step 8: 构建并跑脚本（期望 `33 passed, 0 failed`；构建 0 errors、脚本退出码 0）**
```bash
dotnet build Src/SAEA.Sockets.SslTlsTool/SAEA.Sockets.SslTlsTool.csproj -c Release
pwsh Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1
```

- [ ] **Step 9: 提交**
```bash
git add Src/SAEA.Sockets.SslTlsTool/OpenSsl/Pkcs7Codec.cs Src/SAEA.Sockets.SslTlsTool/OpenSsl/Commands/Pkcs7Command.cs Src/SAEA.Sockets.SslTlsTool/OpenSsl/Commands/Crl2Pkcs7Command.cs Src/SAEA.Sockets.SslTlsTool/OpenSsl/OpenSslUsage.cs Src/SAEA.Sockets.SslTlsTool/OpenSsl/OpenSslCommand.cs Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1
git commit -m "feat(ssltls): 支持 crl2pkcs7 与 pkcs7"
```
### Task 8: 文档同步、设计文档回填与全量验收

**Files:**

- Modify `Src/SAEA.Sockets.SslTlsTool/README.md`、`Src/SAEA.Sockets.SslTlsTool/README.en.md`
- Modify `README.md`、`README.en.md`、`Src/SAEA.Sockets/README.md`、`Src/SAEA.Sockets/README.en.md`
- Modify `docs/superpowers/specs/2026-11-13-ssl-tls-tool-openssl-compat-design.md`

- [ ] **Step 1: 回填设计文档 §8.3 参数表**
Modify `docs/superpowers/specs/2026-11-13-ssl-tls-tool-openssl-compat-design.md` 的 `### 8.3 pkcs12`（第 145 行起）。
a) 把 `| \`-certfile\` / \`-CAfile\` | 路径 | 追加到 PFX 的证书 | 无 |` 改成只列 `-certfile` 一行（openssl 的 `pkcs12` 本身没有 `-CAfile`，本工具不实现，出现即报不支持参数）：

```markdown
| `-certfile` | 路径，可重复 | 追加到 PFX 的证书 | 无 |
```

b) 在 `-nodes` 行后插入两行：

```markdown
| `-nokeys` | 开关 | 读取时不输出私钥 | 关 |
| `-nomac` | 开关 | 导出时不写 MAC，与 `-passout` 互斥 | 关 |
```

c) 表下正文（第 161 行）把「`-nodes` 与 `-passout` 同给报错」改成「`-nodes`/`-nomac` 与 `-passout` 同给报错」，并在该句后补一句：「`-nokeys` 只作用于读取模式，`-nomac` 只作用于导出模式。」表净增 3 行。

- [ ] **Step 2: 设计文档追加 `## 19. 实现回填` 并改状态**
同一文件，在 `## 18. 未决问题` 与文末 `状态：待评审` 之间插入新节，逐条写落地与本文的差异（全部改「本文写法 → 实际实现 + 原因」）：

```markdown
## 19. 实现回填

1. §12 的 `Pkcs12Codec.Save` 未用 `Pkcs12Store`：BouncyCastle 2.6.2 的 `Pkcs12Store` 不能指定迭代次数、salt 与 MAC 摘要算法，无法满足 §8.3 的「迭代 2048 / MAC HMAC-SHA-256」要求（`-legacy` 才走 SHA-1），改为按 PKCS#12 ASN.1 手工构造 `Pfx`（`Pkcs12Codec.cs` 私有方法，含 `AuthenticatedSafe`、`MacData`）。
2. §12 的目录细化：每个子命令一个文件，统一放 `OpenSsl/Commands/`（`X509Command`、`Pkcs12Command`、`Pkcs7Command` 等）；`OptionReader`、`OpenSslUsage`、`OpenSslException`、`OpenSslCommand` 留在 `OpenSsl/` 根。
3. `pkcs12` 在 §8.3 原表之外增加两个参数：`-nokeys`（读取时不输出私钥）、`-nomac`（导出时不写 MAC，与 `-passout` 互斥）；`-CAfile` 不再列出（openssl 的 `pkcs12` 本身没有该参数，本工具不实现，出现时报不支持参数）。
4. 退出码分两条分支并存：首参命中 `req`/`x509`/`pkcs8`/`pkey`/`pkcs12`/`crl2pkcs7`/`pkcs7`/`version` 走兼容分支（0/1），否则整段交给遗留解析器（0/1/2/3），与 §7 一致。
5. 兼容分支的用法/错误文本统一走 `OpenSslUsage`，stdout 只写数据，提示一律走 stderr，落实 §11.1。
6. `x509 -CAserial` 与 §8.2「遇到报专用文案」的差异：该参数不进白名单，由 `OptionReader` 统一报 `error: 不支持的参数 "-CAserial"；支持的参数见 -help。`，比专用文案更早拦截，语义（不支持该参数、序列号文件固定为 `<CA 文件名>.srl`）一致。
7. `OpenSsl/Commands/` 子目录使用独立命名空间 `SAEA.Sockets.SslTlsTool.OpenSsl.Commands`；`OpenSsl/` 根下的类型仍在 `SAEA.Sockets.SslTlsTool.OpenSsl`，与 §12「不再分子命名空间」按目录分层落地。
8. `PasswordResolver.Resolve` 去掉 `option` 参数（错误文案不拼参数名），签名实为 `Resolve(string? spec, string target, bool confirm, bool interactive)`；`req -newkey` 在既无 `-nodes` 也无 `-passout` 时按明文写私钥并在 stderr 提示，不隐式索要口令，避免非交互脚本挂起（已记入 §11.3）。
```

同时把文末 `状态：待评审` 改成 `状态：已实现，待验收`（文件只在文末写了状态，不要在文首新增）。

- [ ] **Step 3: 工具中文 README 新增「openssl 兼容子命令」章节**
Modify `Src/SAEA.Sockets.SslTlsTool/README.md`，插在 `## 参数`（第 486 行）之后、`## 交互模式`（第 557 行）之前，标题层级与既有文风一致：

```markdown
## openssl 兼容子命令
### 命令与参数
#### req / x509 / pkcs8 / pkey / pkcs12 / crl2pkcs7 / pkcs7 / version
### 格式矩阵
### 常用示例
### 与 openssl 的差异
### 退出码
### 口令来源
## 怎么验证
```

各小节内容按下面固定口径写，不写「按需补充」：
- `### 命令与参数`：8 个子命令各一张两列表（`参数` / `说明`），只列常用项，以设计文档 §8 各小节为准；每张小表前一行写该命令的一句话用途。
- `### 格式矩阵`：压 §9 主干成一张 4 列表（输入格式 / 目标格式 / 命令 / 备注），不复制 §9 全表。
- `### 常用示例`：7 条，依次为 DER→PEM、PKCS#8→PKCS#1、PFX→PEM、PEM+私钥→PFX、`-legacy` 导出、CSR 生成与签发、P7B 打包与拆解；每条给一行说明 + 一条 `SAEA.Sockets.SslTlsTool <子命令> ...` 命令，命令与英文版逐字一致。
- `### 与 openssl 的差异`：照搬 §11.3 全表（项目 / openssl 行为 / 本工具 / 影响 四列），保留其结论句，不加不减。
- `### 退出码`：兼容分支 0/1 与遗留分支 0/1/2/3 各一张小表；写明判断方式——首参是否命中那 8 个子命令（大小写敏感、只看第一个参数），未命中即走遗留分支。
- `### 口令来源`：按 §10 的四种来源逐条列（写法 / 示例 / 注意事项），并单独提示 `pass:` 明文会留在 shell history 与进程列表，优先 `env:` 或 `file:`。

- [ ] **Step 4: 同步英文 README**
Modify `Src/SAEA.Sockets.SslTlsTool/README.en.md`，与中文版逐节对应：`## openssl-compatible subcommands`（含 `### Commands and options`、`### Format matrix`、`### Common examples`、`### Differences from openssl`、`### Exit codes`、`### Passphrase sources`）与 `## How to verify`。用同等信息量的英文表达而非逐字直译；所有示例命令与中文版完全一致，退出码表数字与中文版一致。

- [ ] **Step 5: 改写既有句子（中英两版同批）**
- `## 产物清单` 附近出现的「用外部 openssl 转换」→「用本工具的兼容子命令，或外部 openssl」，并指向新章节。
- 「本工具只生成证书」→「本工具负责证书生成与格式转换」。
- `## 参数` 的「口令」小节与 PFX 相关描述里，默认算法句补 `-legacy` 分支：默认 AES-256-CBC + PBKDF2；加 `-legacy` 时证书 `PBE-SHA1-RC2-40`、私钥 `PBE-SHA1-3DES`、MAC SHA-1，迭代仍 2048。
英文版按同样三处改写，语义与中文一致。

- [ ] **Step 6: 补「怎么验证」小节**
两份 README 的该小节都写全四点：
1. 全量兼容用例：`pwsh Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1`；
2. `-ToolPath` 用来指向已构建的 `SAEA.Sockets.SslTlsTool.exe`（例如 `dotnet publish` 或 `-o` 自定义目录的产物）；不传时脚本用 `dotnet run --project Src/SAEA.Sockets.SslTlsTool -c Release --` 现编译运行；
3. 预期末行 `N passed, 0 failed`、退出码 0，`N` 以脚本实际用例数为准；
4. 本机装了 openssl 时，可用同一组参数手工对拍（`openssl x509 -in ...`、`openssl pkcs12 -export ...`），脚本不做差分；`SAEA_OPENSSL` 不是脚本参数。

- [ ] **Step 7: 根与 Sockets README 加一行指引**
- 根 `README.md`：在 `SAEA.Sockets` 条目处加一行「证书生成与格式转换：`Src/SAEA.Sockets.SslTlsTool`（支持 openssl 兼容子命令）」，`README.en.md` 同位置加英文对应句。
- `Src/SAEA.Sockets/README.md`：在证书/TLS 相关段落加一行「证书生成与格式转换在 `SAEA.Sockets.SslTlsTool` 侧完成，支持 openssl 兼容子命令」，`README.en.md` 同步。
四处都只加一行指向，不复制参数细节。

- [ ] **Step 8: 全量验收**
```bash
dotnet build Src/SAEA.Sockets.sln -c Debug
pwsh Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1
dotnet test Src/SAEA.Common.Tests/SAEA.Common.Tests.csproj -c Release
dotnet test Src/SAEA.MQTT.Tests/SAEA.MQTT.Tests.csproj -c Release
dotnet test Src/SAEA.FTP.Tests/SAEA.FTP.Tests.csproj -c Release
dotnet test Src/SAEA.Sockets.Pipeline.Tests/SAEA.Sockets.Pipeline.Tests.csproj -c Release
```
期望：构建 0 errors；脚本末行 `N passed, 0 failed`（Task 7 末次为 33，本任务不新增用例）且退出码 0；四个 MSTest 工程 59 / 153 / 13 / 157 passed。数字与 `AGENTS.md` 记录或前序任务末次输出不符时，写「以实际输出为准」并在本节记录差异与实际值，不修改基线数字。

- [ ] **Step 9: 手工冒烟遗留入口**
```bash
dotnet run --project Src/SAEA.Sockets.SslTlsTool -c Release -- --self-signed
dotnet run --project Src/SAEA.Sockets.SslTlsTool -c Release -- --verify
```
各跑一次，断言：退出码均为 0；`--self-signed` 的产物文件名/目录与改动前一致（`certs/` 下同名文件）；`--verify` 五项自检结论与改动前逐行一致。两项都不写进脚本，结果记录在本任务完成说明里。

- [ ] **Step 10: 提交**
```bash
git add Src/SAEA.Sockets.SslTlsTool/README.md Src/SAEA.Sockets.SslTlsTool/README.en.md README.md README.en.md Src/SAEA.Sockets/README.md Src/SAEA.Sockets/README.en.md docs/superpowers/specs/2026-11-13-ssl-tls-tool-openssl-compat-design.md
git commit -m "docs(ssltls): 同步 openssl 兼容子命令文档"
```

### Task 9: 脚本补强（.NET 读回、口令来源、互斥参数、遗留回归）

**Files:**

- Modify `Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1`

- [ ] **Step 1: 追加 .NET 读回用例（PFX / P7B / DER）**

插在 `try {` 块内、Task 7 用例之后。复用 Task 6 的 `$pfx`（导出时口令 `test`）、Task 7 的 `$p7`、Task 5 的 `$leaf`；`$pem` 不取 Task 2 用例块内的 `$src`（出了那个 `-Body` 就失效），在本段先用 `x509` 从 `$leaf` 转出。三条用例分别覆盖设计 §13 的「PKCS#12 带口令读回」「P7B 证书数量」「PEM→DER 再读回」。

```powershell
    Invoke-Case -Name '.NET 读回 PFX 含私钥' -Body {
        $c = [System.Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadPkcs12FromFile($pfx, 'test')
        $c.HasPrivateKey -and $c.Subject -like '*CN=localhost*'
    }
    Invoke-Case -Name '.NET 读回 P7B 证书数不少于 1' -Body {
        # $p7 是 PEM 封装，SignedCms.Decode 只吃 DER，先去掉 armor 再喂。
        $der = [Convert]::FromBase64String(((Get-Content -LiteralPath $p7 -Raw) -replace '-----[^-]+-----', '') -replace '\s', '')
        $cms = [System.Security.Cryptography.Pkcs.SignedCms]::new()
        $cms.Decode($der)
        $cms.Certificates.Count -ge 1 -and $cms.Certificates[0].Subject -like '*CN=localhost*'
    }
    Invoke-Case -Name '.NET 读回 DER 证书主题一致' -Body {
        $pem = Join-Path $tmp 'c.pem'; $der = Join-Path $tmp 'c.der'
        $a = Invoke-Tool -Arguments @('x509', '-in', $leaf, '-out', $pem)
        $b = Invoke-Tool -Arguments @('x509', '-in', $pem, '-outform', 'DER', '-out', $der)
        $c = [System.Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadCertificateFromFile($der)
        $a.ExitCode -eq 0 -and $b.ExitCode -eq 0 -and $c.Subject -like '*CN=localhost*'
    }
```

`LoadPkcs12FromFile`、`LoadCertificateFromFile`、`SignedCms` 都是 .NET 5+ API，Windows PowerShell 5.1 没有，脚本首行的 pwsh 7 检测是这些用例的前置条件。P7B 只有一张叶子证书（`crl2pkcs7 -certfile $leaf`），首张即叶子，故主题断言不会碰上链顺序问题。

- [ ] **Step 2: 追加 `-passin file:` 用例**

口令文件用 `WriteAllText` 写出，刻意不带结尾换行，验证解析时只按首行取值且不把 `\n` 当口令的一部分：

```powershell
    Invoke-Case -Name 'pkcs12 -passin file: 读取无结尾换行的口令' -Body {
        $pass = Join-Path $tmp 'pass.txt'; $o = Join-Path $tmp 'file.pem'
        [System.IO.File]::WriteAllText($pass, 'test')
        $r = Invoke-Tool -Arguments @('pkcs12', '-in', $pfx, '-passin', ('file:' + $pass), '-nocerts', '-nodes', '-out', $o)
        $r.ExitCode -eq 0 -and (Get-Content -LiteralPath $o -Raw) -like '*BEGIN PRIVATE KEY*'
    }
```

- [ ] **Step 3: 追加 `-passin stdin` 用例**

管道写法与 `Invoke-Tool` 不兼容（后者把 `2>&1` 收进自己的对象），必须在用例内直接调用；有 `$ToolPath` 时走可执行文件，否则走 `dotnet run`，两条分支的参数数组保持一致：

```powershell
    Invoke-Case -Name 'pkcs12 -passin stdin 从管道读口令' -Body {
        $o = Join-Path $tmp 'stdin.pem'
        $argv = @('pkcs12', '-in', $pfx, '-passin', 'stdin', '-nocerts', '-nodes', '-out', $o)
        $previous = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            if ($ToolPath) { 'test' | & $ToolPath @argv *> $null }
            else { 'test' | & dotnet run --project $project -c Release -- @argv *> $null }
            $code = $LASTEXITCODE
        }
        finally { $ErrorActionPreference = $previous }
        $code -eq 0 -and (Get-Content -LiteralPath $o -Raw) -like '*BEGIN PRIVATE KEY*'
    }
```

- [ ] **Step 4: 追加互斥参数用例**

`$key` 是 Task 3 在 `try {` 块里建的明文私钥，两条用例复用它，避免再从 stdin 读输入导致阻塞：

```powershell
    Invoke-Case -Name 'req -key 与 -newkey 同给退出 1' -Body {
        $r = Invoke-Tool -Arguments @('req', '-new', '-key', $key, '-newkey', 'rsa:2048', '-keyout', (Join-Path $tmp 'mx.key'), '-subj', '/CN=localhost')
        $r.ExitCode -eq 1 -and $r.StdErr -like '*不能同时使用*'
    }
    Invoke-Case -Name 'pkey -traditional 与加密算法同给退出 1' -Body {
        $r = Invoke-Tool -Arguments @('pkey', '-in', $key, '-traditional', '-aes-256-cbc')
        $r.ExitCode -eq 1 -and $r.StdErr -like '*不能同时使用*'
    }
```

- [ ] **Step 5: 追加遗留入口回归用例**

单独 `--verify` 没有生成模式会报「缺少 --self-signed 或 --ca」，按 README 的 `--self-signed --verify` 写法给；自检必须换一个空目录，否则第二次生成零写入会返回退出码 3（全部已存在而跳过），拿不到 0：

```powershell
    Invoke-Case -Name '遗留 --self-signed/--verify 仍退出 0' -Body {
        $dir = Join-Path $tmp 'legacy'; $ver = Join-Path $tmp 'legacy-verify'
        $a = Invoke-Tool -Arguments @('--self-signed', '--out', $dir)
        $b = Invoke-Tool -Arguments @('--self-signed', '--verify', '--out', $ver)
        $a.ExitCode -eq 0 -and
            (Test-Path -LiteralPath (Join-Path $dir 'localhost.crt')) -and
            (Test-Path -LiteralPath (Join-Path $dir 'localhost.key')) -and
            $b.ExitCode -eq 0 -and $b.StdOut -like '*通过*'
    }
```

- [ ] **Step 6: 构建并跑脚本**

```bash
dotnet build Src/SAEA.Sockets.sln -c Debug
pwsh Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1
```

期望构建 0 errors；脚本末行 `N passed, 0 failed`、退出码 0。本任务之后末次用例数在原有 33 条基础上 +8，若与实际不符以输出为准。

- [ ] **Step 7: 提交**

```bash
git add Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1
git commit -m "test(ssltls): 补充 openssl 兼容脚本的读回与互斥用例"
```

## 完成定义

- Task 1–9 的 Step 全部勾选，计划内无遗留占位注释。
- `pwsh Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1` 末行 `N passed, 0 failed`、退出码 0。
- `dotnet build Src/SAEA.Sockets.sln -c Debug` 0 errors。
- 四个 MSTest 工程数字与基线一致：`SAEA.Common.Tests` 59、`SAEA.MQTT.Tests` 153、`SAEA.FTP.Tests` 13、`SAEA.Sockets.Pipeline.Tests` 157 passed。
- 工具与根、Sockets 的 README 中英四对全部同步，示例命令两侧一致。
- `git status` 中本次改动之外没有新增脏文件；自测产物只落在已忽略的 `certs/` 或临时目录。