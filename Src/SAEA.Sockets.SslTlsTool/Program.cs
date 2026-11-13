/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               

*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.Sockets.SslTlsTool
*文件名： Program
*版本号： net10.0
*唯一标识：f48d115d-7e63-488d-b6ef-31ad43f2f02c
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 13:50:48
*描述：开发用证书生成工具的入口、生成流程与输出
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 13:50:42
*修改人： yswenli
*版本号： net10.0
*描述：兼容分支移到 try/finally 之前，避免 ConsoleHelper 的版权横幅污染 stdout
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Sockets.SslTlsTool.OpenSsl;

namespace SAEA.Sockets.SslTlsTool;

/// <summary>
/// 开发用证书生成工具：一条命令产出自签证书或本地 CA + server/client 文件。
/// </summary>
internal static class Program
{
    private const string DefaultPfxPassword = "saea";

    private const string DevelopmentOnlyWarning = "证书仅用于开发/测试，请勿用于生产。";

    private static int Main(string[] args)
    {
        // 兼容分支只用 Console.Out/Console.Error，不经过 ConsoleHelper，避免 SAEA.Common 的启动横幅污染 stdout。
        if (args is { Length: > 0 } && OpenSslCommand.IsCompat(args[0]))
            return OpenSslCommand.Run(args, interactive: false);

        try
        {
            return Run(args);
        }
        finally
        {
            // 输出由 ConsoleHelper 的后台线程异步写控制台，进程退出前必须等待队列写完；
            // 否则 --help、参数错误等立即返回的路径会丢掉后半段输出。
            ConsoleHelper.Flush();
        }
    }

    private static int Run(string[] args)
    {
        if (args == null || args.Length == 0)
            return RunInteractive();

        foreach (var argument in args)
        {
            // --help 与其它参数并存时只打印帮助，不执行生成也不校验其它取值。
            if (string.Equals(argument, "--help", StringComparison.Ordinal))
            {
                PrintHelp();
                return 0;
            }
        }

        return Execute(args, false);
    }

    /// <summary>
    /// 无参数启动时的交互模式：逐行读取与命令行同构的参数，执行后继续等待下一条命令。
    /// </summary>
    /// <returns>正常退出（exit/quit/EOF）时返回 0</returns>
    private static int RunInteractive()
    {
        ConsoleHelper.WriteLine("SAEA.Sockets.SslTlsTool 交互模式", ConsoleColor.Green);
        ConsoleHelper.WriteLine("输入 help 查看用法，exit 或 quit 退出。", ConsoleColor.Green);
        ConsoleHelper.WriteLine();

        while (true)
        {
            // ConsoleHelper 异步写控制台，读取输入前先等它写完，避免提示符与上一段输出交错。
            ConsoleHelper.Flush();
            Console.Write("> ");

            var line = Console.ReadLine();
            if (line == null)
            {
                ConsoleHelper.WriteLine();
                ConsoleHelper.WriteLine("已退出。", ConsoleColor.Green);
                return 0;
            }

            var trimmed = line.Trim();
            if (trimmed.Length == 0)
                continue;

            if (string.Equals(trimmed, "exit", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "quit", StringComparison.OrdinalIgnoreCase))
            {
                ConsoleHelper.WriteLine("已退出。", ConsoleColor.Green);
                return 0;
            }

            if (string.Equals(trimmed, "help", StringComparison.OrdinalIgnoreCase))
            {
                PrintHelp();
                continue;
            }

            string[] tokens;
            string? tokenizeError;
            if (!TryTokenize(line, out tokens, out tokenizeError))
            {
                ConsoleHelper.WriteLine(tokenizeError, ConsoleColor.Red);
                continue;
            }

            var helpRequested = false;
            foreach (var token in tokens)
            {
                if (string.Equals(token, "--help", StringComparison.Ordinal))
                {
                    helpRequested = true;
                    break;
                }
            }

            if (helpRequested)
            {
                PrintHelp();
                continue;
            }

            if (tokens.Length == 0)
                continue;

            if (tokens.Length > 0 && OpenSslCommand.IsCompat(tokens[0]))
            {
                OpenSslCommand.Run(tokens, interactive: true);
                continue;
            }

            var exitCode = Execute(tokens, true);
            if (exitCode != 0)
                ConsoleHelper.WriteLine("退出码：" + exitCode + "；交互模式继续，输入 exit 退出。", ConsoleColor.Yellow);
        }
    }

    /// <summary>
    /// 执行一次生成（命令行参数或交互模式的一行命令）。
    /// </summary>
    /// <param name="args">已拆分的参数</param>
    /// <param name="interactive">是否来自交互模式，用于选择错误提示措辞</param>
    /// <returns>退出码</returns>
    private static int Execute(string[] args, bool interactive)
    {
        SslTlsToolOptions options;
        string? error;
        if (!SslTlsToolOptions.TryParse(args, out options, out error))
        {
            ConsoleHelper.WriteLine(error, ConsoleColor.Red);
            ConsoleHelper.WriteLine(interactive ? "输入 help 查看用法。" : "运行 --help 查看用法。");
            return 1;
        }

        if (!options.SelfSigned && !options.Ca)
        {
            ConsoleHelper.WriteLine("缺少 --self-signed 或 --ca，未执行任何生成。", ConsoleColor.Red);
            if (interactive)
                ConsoleHelper.WriteLine("输入 help 查看用法。");
            else
                PrintHelp();
            return 1;
        }

        var outDirectory = options.OutDirectory;
        if (File.Exists(outDirectory))
        {
            ConsoleHelper.WriteLine("输出路径是一个已存在的文件，不能作为目录使用：" + Path.GetFullPath(outDirectory), ConsoleColor.Red);
            return 2;
        }

        try
        {
            Directory.CreateDirectory(outDirectory);
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteLine("无法创建输出目录 " + Path.GetFullPath(outDirectory) + "：" + ex.Message, ConsoleColor.Red);
            return 2;
        }

        var summary = new RunSummary();

        try
        {
            if (options.SelfSigned)
                GenerateSelfSigned(options, outDirectory, summary);

            if (options.Ca)
                GenerateCa(options, outDirectory, summary);
        }
        catch (ParameterException ex)
        {
            ConsoleHelper.WriteLine(ex.Message, ConsoleColor.Red);
            return 1;
        }
        catch (ToolFailureException ex)
        {
            ConsoleHelper.WriteLine(ex.Message, ConsoleColor.Red);
            return 2;
        }
        catch (ArgumentException ex)
        {
            ConsoleHelper.WriteLine("证书参数无效：" + ex.Message, ConsoleColor.Red);
            return 1;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            ConsoleHelper.WriteLine("写盘失败：" + ex.Message, ConsoleColor.Red);
            ConsoleHelper.WriteLine("请检查目标目录权限，或确认文件没有被其它程序占用。");
            return 2;
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteLine("生成证书失败：" + ex.Message, ConsoleColor.Red);
            return 2;
        }

        PrintSummary(options, summary);

        var exitCode = 0;
        if (options.Verify)
        {
            // 自检结果由 CertificateVerifier 同步写 Console；先等生成汇总的异步队列写完，保证输出顺序。
            ConsoleHelper.Flush();
            var plan = BuildVerificationPlan(options, summary);
            try
            {
                CertificateVerifier.Verify(plan);
            }
            catch (CertificateVerificationException ex)
            {
                ConsoleHelper.WriteLine("自检第 " + ex.Item + " 项失败：" + ex.Message, ConsoleColor.Red);
                exitCode = 2;
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteLine("自检失败：" + ex.Message, ConsoleColor.Red);
                exitCode = 2;
            }
        }

        ConsoleHelper.WriteLine();
        ConsoleHelper.WriteLine(DevelopmentOnlyWarning, ConsoleColor.Yellow);

        if (exitCode == 0 && summary.WrittenFileCount == 0)
            exitCode = 3;

        return exitCode;
    }

    /// <summary>
    /// 把交互模式的一行拆成参数：空白分隔，双引号可包住含空格的取值，\" 与 \\ 转义。
    /// </summary>
    /// <param name="line">输入行</param>
    /// <param name="tokens">拆分结果</param>
    /// <param name="error">失败原因，成功时为 null</param>
    /// <returns>双引号闭合时返回 true</returns>
    private static bool TryTokenize(string line, out string[] tokens, out string? error)
    {
        var list = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var hasToken = false;

        for (var index = 0; index < line.Length; index++)
        {
            var ch = line[index];

            if (ch == '"')
            {
                // 引号本身不进入取值，但出现引号要让空串也成为有效 token（如 --pwd ""）。
                inQuotes = !inQuotes;
                hasToken = true;
                continue;
            }

            if (!inQuotes && char.IsWhiteSpace(ch))
            {
                if (hasToken)
                {
                    list.Add(current.ToString());
                    current.Clear();
                    hasToken = false;
                }

                continue;
            }

            // 只把 \" 当作转义，反斜杠本身原样保留，避免 UNC 路径（\\server\share）被吃掉一个分隔符。
            if (inQuotes && ch == '\\' && index + 1 < line.Length && line[index + 1] == '"')
            {
                current.Append(line[++index]);
                hasToken = true;
                continue;
            }

            current.Append(ch);
            hasToken = true;
        }

        if (inQuotes)
        {
            tokens = Array.Empty<string>();
            error = "命令里的双引号没有闭合。";
            return false;
        }

        if (hasToken)
            list.Add(current.ToString());

        tokens = list.ToArray();
        error = null;
        return true;
    }

    private static void GenerateSelfSigned(SslTlsToolOptions options, string outDirectory, RunSummary summary)
    {
        var baseName = options.Name ?? "localhost";
        var subject = string.IsNullOrWhiteSpace(options.Subject) ? "CN=" + baseName : options.Subject!;

        var pfxPath = Path.Combine(outDirectory, baseName + ".pfx");
        var crtPath = Path.Combine(outDirectory, baseName + ".crt");
        var keyPath = Path.Combine(outDirectory, baseName + ".key");

        // 自签模式没有 CA 与独立客户端证书，回环握手用同一张证书同时充当服务端与客户端证书
        summary.ServerPfxPath = pfxPath;
        summary.ClientPfxPath = pfxPath;

        var writePfx = options.Force || !File.Exists(pfxPath);
        var writeCrt = options.Force || !File.Exists(crtPath);
        var writeKey = options.Force || !File.Exists(keyPath);

        if (!writePfx) PrintSkipped(pfxPath);
        if (!writeCrt) PrintSkipped(crtPath);
        if (!writeKey) PrintSkipped(keyPath);

        var title = "自签证书（" + baseName + "）";
        if (writePfx || writeCrt || writeKey)
        {
            var now = DateTimeOffset.UtcNow;
            var certificateOptions = new SaeaCertificateOptions
            {
                Purpose = SaeaCertificatePurpose.Server,
                Subject = subject,
                DnsNames = options.DnsNames,
                IpAddresses = options.IpAddresses,
                KeySize = options.KeySize,
                NotBefore = now.AddDays(-1),
                NotAfter = now.AddDays(options.Days),
                PfxPassword = options.Password,
            };

            using (var bundle = SaeaCertificateFactory.CreateSelfSigned(certificateOptions))
            {
                if (writePfx) bundle.SavePfx(pfxPath, options.Password);
                if (writeCrt) bundle.SaveCertificatePem(crtPath);
                if (writeKey) bundle.SavePrivateKeyPem(keyPath);

                var printed = CreatePrinted(title, bundle.Certificate);
                printed.Files.Add(new PrintedFile { Kind = "PFX", Path = Path.GetFullPath(pfxPath), Written = writePfx });
                printed.Files.Add(new PrintedFile { Kind = "证书 PEM", Path = Path.GetFullPath(crtPath), Written = writeCrt });
                printed.Files.Add(new PrintedFile { Kind = "私钥 PEM（明文）", Path = Path.GetFullPath(keyPath), Written = writeKey });
                summary.Certificates.Add(printed);
            }

            summary.WrittenFileCount += CountWritten(writePfx, writeCrt, writeKey);
        }
        else if (File.Exists(pfxPath))
        {
            // 全部跳过时仍打印已有证书的主题与指纹，便于脚本核对本次未改动的是哪张证书。
            using (var existing = SaeaCertificateLoader.FromPfx(pfxPath, EffectivePfxPassword(options)))
            {
                var printed = CreatePrinted(title + "，已存在", existing);
                printed.Files.Add(new PrintedFile { Kind = "PFX", Path = Path.GetFullPath(pfxPath), Written = false });
                printed.Files.Add(new PrintedFile { Kind = "证书 PEM", Path = Path.GetFullPath(crtPath), Written = false });
                printed.Files.Add(new PrintedFile { Kind = "私钥 PEM（明文）", Path = Path.GetFullPath(keyPath), Written = false });
                summary.Certificates.Add(printed);
            }
        }

        if (File.Exists(keyPath))
            summary.PlainKeyPaths.Add(Path.GetFullPath(keyPath));

        summary.Targets.Add(new CertificateVerificationTarget
        {
            Label = "自签 " + baseName,
            PfxPath = pfxPath,
            CrtPath = crtPath,
            KeyPath = keyPath,
            Password = EffectivePfxPassword(options),
            IsServer = true,
            ExpectedDnsNames = CertificateVerifier.ComputeExpectedDnsNames(subject, options.DnsNames),
            ExpectedIpAddresses = CertificateVerifier.ComputeExpectedIpAddresses(options.IpAddresses),
        });
    }

    private static void GenerateCa(SslTlsToolOptions options, string outDirectory, RunSummary summary)
    {
        summary.CaMode = true;

        var caPfxPath = Path.Combine(outDirectory, "ca.pfx");
        var caCrtPath = Path.Combine(outDirectory, "ca.crt");
        var caKeyPath = Path.Combine(outDirectory, "ca.key");
        var serverPfxPath = Path.Combine(outDirectory, "server.pfx");
        var serverCrtPath = Path.Combine(outDirectory, "server.crt");
        var serverKeyPath = Path.Combine(outDirectory, "server.key");
        var clientPfxPath = Path.Combine(outDirectory, "client.pfx");
        var clientCrtPath = Path.Combine(outDirectory, "client.crt");
        var clientKeyPath = Path.Combine(outDirectory, "client.key");

        var caExists = File.Exists(caPfxPath) || File.Exists(caCrtPath) || File.Exists(caKeyPath);

        if (caExists && !options.ReuseCa && !options.Force)
        {
            throw new ParameterException("CA 文件已存在：" + Path.GetFullPath(caPfxPath) +
                "。要复用请加 --reuse-ca，要重建请加 --force。");
        }

        if (options.Name != null)
        {
            ConsoleHelper.WriteLine("提示：--name 只在 --self-signed 模式生效，CA 模式固定使用 ca/server/client 作为文件名。");
        }

        var now = DateTimeOffset.UtcNow;
        var effectivePassword = EffectivePfxPassword(options);
        var caPfxPassword = options.ReuseCa ? ResolveCaPassword(options) : effectivePassword;

        SaeaCertificateAuthority? authority = null;
        try
        {
            var caWritten = false;

            if (options.ReuseCa)
            {
                authority = LoadAuthority(caPfxPath, caCrtPath, caKeyPath, caPfxPassword);

                if (options.CaSubject != null)
                    ConsoleHelper.WriteLine("提示：复用现有 CA，--ca-cn 已忽略。", ConsoleColor.Yellow);
                if (options.CaDaysSpecified)
                    ConsoleHelper.WriteLine("提示：复用现有 CA，--ca-days 已忽略。", ConsoleColor.Yellow);

                ConsoleHelper.WriteLine("复用 CA：" + Path.GetFullPath(File.Exists(caPfxPath) ? caPfxPath : caCrtPath) +
                    "（CA 文件不改动，server/client 将被覆盖）", ConsoleColor.Green);

                if (now.AddDays(options.Days).UtcDateTime > authority.Certificate.NotAfter.ToUniversalTime())
                {
                    throw new ParameterException("新叶子的到期时间晚于现有 CA（到期时间 " +
                        authority.Certificate.NotAfter.ToString("yyyy-MM-dd HH:mm:ss") +
                        "，本地时间）。请减小 --days，或加 --force 重建 CA。");
                }
            }
            else
            {
                if (caExists)
                    ConsoleHelper.WriteLine("警告：--force 将重建 CA，已导入该 CA 的信任会作废。");

                var caOptions = new SaeaCertificateOptions
                {
                    Purpose = SaeaCertificatePurpose.CertificateAuthority,
                    Subject = options.CaSubject,
                    KeySize = options.KeySize,
                    NotBefore = now.AddDays(-1),
                    NotAfter = now.AddDays(options.CaDays),
                    PfxPassword = options.Password,
                };

                authority = SaeaCertificateAuthority.Create(caOptions);
                authority.SavePfx(caPfxPath, options.Password);
                authority.SaveCertificatePem(caCrtPath);
                authority.SavePrivateKeyPem(caKeyPath);
                caWritten = true;
                summary.WrittenFileCount += 3;
            }

            var caPrinted = CreatePrinted("根 CA", authority.Certificate);
            caPrinted.Files.Add(new PrintedFile
            {
                Kind = "PFX",
                Path = Path.GetFullPath(caPfxPath),
                Written = caWritten,
            });
            caPrinted.Files.Add(new PrintedFile
            {
                Kind = "证书 PEM",
                Path = Path.GetFullPath(caCrtPath),
                Written = caWritten,
            });
            caPrinted.Files.Add(new PrintedFile
            {
                Kind = "私钥 PEM（明文）",
                Path = Path.GetFullPath(caKeyPath),
                Written = caWritten,
            });
            summary.Certificates.Add(caPrinted);

            summary.CaCertificateRaw = authority.Certificate.RawData;
            summary.CaThumbprint = authority.Certificate.Thumbprint;
            summary.CaCertificatePath = caCrtPath;
            summary.ServerPfxPath = serverPfxPath;
            summary.ClientPfxPath = clientPfxPath;

            var serverSubject = string.IsNullOrWhiteSpace(options.Subject) ? null : options.Subject;
            var serverOptions = new SaeaCertificateOptions
            {
                Purpose = SaeaCertificatePurpose.Server,
                Subject = serverSubject,
                DnsNames = options.DnsNames,
                IpAddresses = options.IpAddresses,
                KeySize = options.KeySize,
                NotBefore = now.AddDays(-1),
                NotAfter = now.AddDays(options.Days),
                PfxPassword = options.Password,
            };

            using (var serverBundle = authority.IssueServerCertificate(serverOptions))
            {
                serverBundle.SavePfx(serverPfxPath, options.Password);
                serverBundle.SaveCertificatePem(serverCrtPath);
                serverBundle.SavePrivateKeyPem(serverKeyPath);

                var printed = CreatePrinted("服务端证书（server）", serverBundle.Certificate);
                printed.Files.Add(new PrintedFile { Kind = "PFX", Path = Path.GetFullPath(serverPfxPath), Written = true });
                printed.Files.Add(new PrintedFile { Kind = "证书 PEM", Path = Path.GetFullPath(serverCrtPath), Written = true });
                printed.Files.Add(new PrintedFile { Kind = "私钥 PEM（明文）", Path = Path.GetFullPath(serverKeyPath), Written = true });
                summary.Certificates.Add(printed);
            }

            summary.WrittenFileCount += 3;

            var clientOptions = new SaeaCertificateOptions
            {
                Purpose = SaeaCertificatePurpose.Client,
                Subject = options.ClientSubject,
                KeySize = options.KeySize,
                NotBefore = now.AddDays(-1),
                NotAfter = now.AddDays(options.Days),
                PfxPassword = options.Password,
            };

            using (var clientBundle = authority.IssueClientCertificate(clientOptions))
            {
                clientBundle.SavePfx(clientPfxPath, options.Password);
                clientBundle.SaveCertificatePem(clientCrtPath);
                clientBundle.SavePrivateKeyPem(clientKeyPath);

                var printed = CreatePrinted("客户端证书（client）", clientBundle.Certificate);
                printed.Files.Add(new PrintedFile { Kind = "PFX", Path = Path.GetFullPath(clientPfxPath), Written = true });
                printed.Files.Add(new PrintedFile { Kind = "证书 PEM", Path = Path.GetFullPath(clientCrtPath), Written = true });
                printed.Files.Add(new PrintedFile { Kind = "私钥 PEM（明文）", Path = Path.GetFullPath(clientKeyPath), Written = true });
                summary.Certificates.Add(printed);
            }

            summary.WrittenFileCount += 3;

            summary.PlainKeyPaths.Add(Path.GetFullPath(caKeyPath));
            summary.PlainKeyPaths.Add(Path.GetFullPath(serverKeyPath));
            summary.PlainKeyPaths.Add(Path.GetFullPath(clientKeyPath));

            summary.Targets.Add(new CertificateVerificationTarget
            {
                Label = "根 CA",
                PfxPath = File.Exists(caPfxPath) ? caPfxPath : null,
                CrtPath = caCrtPath,
                KeyPath = caKeyPath,
                Password = caPfxPassword,
                IsCertificateAuthority = true,
            });

            summary.Targets.Add(new CertificateVerificationTarget
            {
                Label = "server",
                PfxPath = serverPfxPath,
                CrtPath = serverCrtPath,
                KeyPath = serverKeyPath,
                Password = effectivePassword,
                IsServer = true,
                ExpectedDnsNames = CertificateVerifier.ComputeExpectedDnsNames(
                    serverSubject ?? "CN=localhost", options.DnsNames),
                ExpectedIpAddresses = CertificateVerifier.ComputeExpectedIpAddresses(options.IpAddresses),
            });

            summary.Targets.Add(new CertificateVerificationTarget
            {
                Label = "client",
                PfxPath = clientPfxPath,
                CrtPath = clientCrtPath,
                KeyPath = clientKeyPath,
                Password = effectivePassword,
                IsClient = true,
            });
        }
        finally
        {
            if (authority != null)
                authority.Dispose();
        }
    }

    private static SaeaCertificateAuthority LoadAuthority(string caPfxPath, string caCrtPath, string caKeyPath,
        string password)
    {
        if (!File.Exists(caPfxPath) && !(File.Exists(caCrtPath) && File.Exists(caKeyPath)))
        {
            throw new ToolFailureException("找不到可复用的 CA：既没有 " + Path.GetFullPath(caPfxPath) +
                "，也没有 ca.crt + ca.key。请先用 --force 生成 CA。");
        }

        try
        {
            if (File.Exists(caPfxPath))
                return SaeaCertificateAuthority.FromPfx(caPfxPath, password);

            return SaeaCertificateAuthority.FromPem(caCrtPath, caKeyPath, password);
        }
        catch (Exception ex)
        {
            // 库内异常消息自带句号，这里去掉尾句号再拼接，避免出现「。。」。
            var reason = (ex.Message ?? string.Empty).TrimEnd('。');
            throw new ToolFailureException("读取 CA 失败（口令错误、文件损坏或证书不是 CA）：" + reason +
                "。请确认 --ca-pwd/--pwd，或用 --force 重建 CA。");
        }
    }

    private static CertificateVerificationPlan BuildVerificationPlan(SslTlsToolOptions options, RunSummary summary)
    {
        var plan = new CertificateVerificationPlan
        {
            AuthorityCertificate = summary.CaCertificateRaw,
            ServerPfxPath = summary.ServerPfxPath,
            ClientPfxPath = summary.ClientPfxPath,
            PfxPassword = EffectivePfxPassword(options),
        };

        foreach (var target in summary.Targets)
            plan.Targets.Add(target);

        return plan;
    }

    private static string EffectivePfxPassword(SslTlsToolOptions options)
    {
        return options.Password ?? DefaultPfxPassword;
    }

    private static string ResolveCaPassword(SslTlsToolOptions options)
    {
        if (options.CaPasswordSpecified)
            return options.CaPassword ?? string.Empty;

        return EffectivePfxPassword(options);
    }

    private static int CountWritten(bool pfx, bool crt, bool key)
    {
        var count = 0;
        if (pfx) count++;
        if (crt) count++;
        if (key) count++;
        return count;
    }

    private static PrintedCertificate CreatePrinted(string title, X509Certificate2 certificate)
    {
        return new PrintedCertificate
        {
            Title = title,
            Subject = certificate.Subject,
            Thumbprint = certificate.Thumbprint,
        };
    }

    private static void PrintSkipped(string path)
    {
        ConsoleHelper.WriteLine("skipped：" + Path.GetFullPath(path) + " 已存在，未覆盖（要覆盖请加 --force）。");
    }

    private static void PrintSummary(SslTlsToolOptions options, RunSummary summary)
    {
        ConsoleHelper.WriteLine();
        ConsoleHelper.WriteLine("生成结果：");

        foreach (var certificate in summary.Certificates)
        {
            ConsoleHelper.WriteLine("  " + certificate.Title, ConsoleColor.Green);
            ConsoleHelper.WriteLine("    主题： " + certificate.Subject, ConsoleColor.Green);
            ConsoleHelper.WriteLine("    SHA-1：" + certificate.Thumbprint, ConsoleColor.Green);

            foreach (var file in certificate.Files)
            {
                var state = file.Written ? "已写入" : "已存在，未改写";
                ConsoleHelper.WriteLine("    " + file.Kind + "：" + file.Path + "（" + state + "）", ConsoleColor.Green);
            }
        }

        if (summary.WrittenFileCount == 0)
            ConsoleHelper.WriteLine("  本次没有写入任何新文件（全部已存在并跳过）。", ConsoleColor.Yellow);

        ConsoleHelper.WriteLine();
        if (options.PasswordSpecified)
        {
            ConsoleHelper.WriteLine("PFX 口令：已按 --pwd 设置（不回显）", ConsoleColor.Green);
            if (string.IsNullOrEmpty(options.Password))
                ConsoleHelper.WriteLine("风险：--pwd 为空，PFX 没有口令保护，拿到文件即可使用私钥。", ConsoleColor.Red);
        }
        else
        {
            ConsoleHelper.WriteLine("PFX 口令：默认 saea", ConsoleColor.Green);
        }

        if (summary.PlainKeyPaths.Count > 0)
        {
            ConsoleHelper.WriteLine("注意：以下 .key 是明文私钥（RSA PRIVATE KEY，无口令），泄露等同身份或签发权泄露，请勿提交到版本库：", ConsoleColor.Yellow);
            foreach (var path in summary.PlainKeyPaths)
                ConsoleHelper.WriteLine("  " + path, ConsoleColor.Yellow);
        }

        if (summary.CaMode && summary.CaCertificatePath != null && summary.CaThumbprint != null)
            PrintTrustInstructions(summary.CaCertificatePath, summary.CaThumbprint);
    }

    private static void PrintTrustInstructions(string caCertificatePath, string caThumbprint)
    {
        var windowsPath = caCertificatePath.Replace('/', '\\');
        var unixPath = caCertificatePath.Replace('\\', '/');

        ConsoleHelper.WriteLine();
        ConsoleHelper.WriteLine("CA 信任导入 / 移除（仅本机开发使用，用完请移除）：", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  Windows 当前用户：", ConsoleColor.Yellow);
        ConsoleHelper.WriteLine("    Import-Certificate -FilePath " + windowsPath +
            " -CertStoreLocation Cert:\\CurrentUser\\Root", ConsoleColor.Yellow);
        ConsoleHelper.WriteLine("    移除：Get-ChildItem Cert:\\CurrentUser\\Root | Where-Object { $_.Thumbprint -eq '" +
            caThumbprint + "' } | Remove-Item", ConsoleColor.Yellow);
        ConsoleHelper.WriteLine("    以服务账号运行或需要全机信任时改用 Cert:\\LocalMachine\\Root（需管理员权限）。", ConsoleColor.Yellow);
        ConsoleHelper.WriteLine("  Debian / Ubuntu（curl、OpenSSL、系统浏览器生效）：", ConsoleColor.Yellow);
        ConsoleHelper.WriteLine("    sudo cp " + unixPath +
            " /usr/local/share/ca-certificates/saea-dev-ca.crt && sudo update-ca-certificates", ConsoleColor.Yellow);
        ConsoleHelper.WriteLine("    移除：sudo rm /usr/local/share/ca-certificates/saea-dev-ca.crt && sudo update-ca-certificates --fresh", ConsoleColor.Yellow);
        ConsoleHelper.WriteLine("  macOS：", ConsoleColor.Yellow);
        ConsoleHelper.WriteLine("    sudo security add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain " +
            unixPath, ConsoleColor.Yellow);
        ConsoleHelper.WriteLine("    移除：sudo security remove-trusted-cert -d " + unixPath, ConsoleColor.Yellow);
        ConsoleHelper.WriteLine("  Java 不读系统信任库，需要单独导入：", ConsoleColor.Yellow);
        ConsoleHelper.WriteLine("    keytool -importcert -cacerts -alias saea-dev-ca -file " + unixPath, ConsoleColor.Yellow);
        ConsoleHelper.WriteLine("    移除：keytool -delete -cacerts -alias saea-dev-ca", ConsoleColor.Yellow);
        ConsoleHelper.WriteLine("  Node 不读系统信任库，需要显式指定：", ConsoleColor.Yellow);
        ConsoleHelper.WriteLine("    NODE_EXTRA_CA_CERTS=" + unixPath + "（或使用新版的 --use-system-ca）", ConsoleColor.Yellow);
        ConsoleHelper.WriteLine("  说明：未把 CA 导入客户端信任库时，本库服务端只下发叶子证书，默认链校验会失败。", ConsoleColor.Yellow);
    }

    private static void PrintHelp()
    {
        ConsoleHelper.WriteLine("用法：SAEA.Sockets.SslTlsTool [选项]", ConsoleColor.Green);
        ConsoleHelper.WriteLine();
        ConsoleHelper.WriteLine("生成开发用自签证书，或生成本地根 CA 及其签发的服务端/客户端证书（mTLS）。", ConsoleColor.Green);
        ConsoleHelper.WriteLine("产物默认写入 ./certs。证书与私钥仅供开发/测试，请勿用于生产。", ConsoleColor.Green);
        ConsoleHelper.WriteLine();
        ConsoleHelper.WriteLine("交互模式：", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  不带任何参数时进入；逐行输入与命令行相同的参数，help 查看帮助，exit 或 quit 退出。", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  取值含空格时用双引号包住，例如：--cn \"CN=localhost, O=SAEA\"；引号内仅 \\\" 转义。", ConsoleColor.Green);
        ConsoleHelper.WriteLine();
        ConsoleHelper.WriteLine("模式（至少选一个）：", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  --self-signed            生成单张自签证书", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  --ca                     生成 CA + server + client", ConsoleColor.Green);
        ConsoleHelper.WriteLine();
        ConsoleHelper.WriteLine("输出：", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  --out <dir>              输出目录，默认 ./certs；不存在时递归创建", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  --name <base>            自签模式文件基名，默认 localhost；未给 --cn 时同时作为 CN", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  --cn <subject>           自签证书与服务端叶子的主题 DN，默认 CN=localhost；CA 模式只影响 server", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  --ca-cn <subject>        CA 模式：CA 主题 DN，默认 CN=SAEA Dev Root CA", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  --client-cn <subject>    CA 模式：客户端主题 DN，默认 CN=saea-client", ConsoleColor.Green);
        ConsoleHelper.WriteLine();
        ConsoleHelper.WriteLine("证书内容：", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  --dns <a,b>              服务端 DNS SAN；省略时按 CN 自动推导（CN + localhost）", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  --ip <a,b>               服务端 IP SAN，默认 127.0.0.1", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  --days <n>               叶子有效至 n 天后，默认 30，范围 1..36500", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  --ca-days <n>            CA 有效至 n 天后，默认 3650，范围 1..36500", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  --keysize <n>            RSA 密钥长度 2048|3072|4096，默认 2048", ConsoleColor.Green);
        ConsoleHelper.WriteLine();
        ConsoleHelper.WriteLine("口令：", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  --pwd <password>         PFX 口令，默认 saea；显式传空串表示无口令（有风险）", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  --ca-pwd <password>      复用 CA 时的读取口令，默认取 --pwd", ConsoleColor.Green);
        ConsoleHelper.WriteLine();
        ConsoleHelper.WriteLine("已存在文件：", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  --reuse-ca               复用 <out>/ca.pfx（缺失时退回 ca.crt + ca.key）签新叶子；必须与 --ca 同用", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  --force                  覆盖已存在文件；与 --reuse-ca 互斥", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  --verify                 生成后自检：PFX/PEM 读回、链、SAN/EKU、回环握手", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  --help                   打印本帮助", ConsoleColor.Green);
        ConsoleHelper.WriteLine();
        ConsoleHelper.WriteLine("退出码：", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  0  至少写入一个新文件；给了 --verify 时自检通过", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  1  参数错误（未知参数、缺值、取值越界、ca.* 已存在但未指定 --reuse-ca/--force 等）", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  2  生成、读取 CA、写盘或 --verify 自检失败", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  3  一个文件都没有写入（全部已存在而跳过）；给了 --verify 时自检通过", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  交互模式下的单条命令失败只打印退出码并继续等待；进程最终以 0 退出。", ConsoleColor.Green);
        ConsoleHelper.WriteLine();
        ConsoleHelper.WriteLine("示例：", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  SAEA.Sockets.SslTlsTool --self-signed --verify", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  SAEA.Sockets.SslTlsTool --ca --out ./certs --verify", ConsoleColor.Green);
        ConsoleHelper.WriteLine("  SAEA.Sockets.SslTlsTool --ca --reuse-ca --days 90", ConsoleColor.Green);
    }

    /// <summary>
    /// 本次生成的汇总：待自检目标、输出展示与 CA 信息。
    /// </summary>
    private sealed class RunSummary
    {
        /// <summary>生成的证书块，用于输出主题与指纹。</summary>
        public List<PrintedCertificate> Certificates { get; } = new List<PrintedCertificate>();

        /// <summary>待自检的证书。</summary>
        public List<CertificateVerificationTarget> Targets { get; } = new List<CertificateVerificationTarget>();

        /// <summary>明文私钥路径。</summary>
        public List<string> PlainKeyPaths { get; } = new List<string>();

        /// <summary>本次写入的文件数。</summary>
        public int WrittenFileCount { get; set; }

        /// <summary>是否执行了 CA 模式。</summary>
        public bool CaMode { get; set; }

        /// <summary>CA 证书 PEM 路径。</summary>
        public string? CaCertificatePath { get; set; }

        /// <summary>CA 指纹。</summary>
        public string? CaThumbprint { get; set; }

        /// <summary>CA 证书 DER 字节。</summary>
        public byte[]? CaCertificateRaw { get; set; }

        /// <summary>服务端 PFX 路径。</summary>
        public string? ServerPfxPath { get; set; }

        /// <summary>客户端 PFX 路径。</summary>
        public string? ClientPfxPath { get; set; }
    }

    /// <summary>
    /// 输出的单个证书块。
    /// </summary>
    private sealed class PrintedCertificate
    {
        /// <summary>显示名称。</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>证书主题。</summary>
        public string Subject { get; set; } = string.Empty;

        /// <summary>SHA-1 指纹。</summary>
        public string Thumbprint { get; set; } = string.Empty;

        /// <summary>关联文件。</summary>
        public List<PrintedFile> Files { get; } = new List<PrintedFile>();
    }

    /// <summary>
    /// 输出的单个文件。
    /// </summary>
    private sealed class PrintedFile
    {
        /// <summary>文件种类。</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>完整路径。</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>本次是否写入。</summary>
        public bool Written { get; set; }
    }

    /// <summary>
    /// 参数错误（退出码 1）。
    /// </summary>
    private sealed class ParameterException : Exception
    {
        /// <summary>
        /// 创建参数错误。
        /// </summary>
        /// <param name="message">中文原因</param>
        public ParameterException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// 生成/读取失败（退出码 2）。
    /// </summary>
    private sealed class ToolFailureException : Exception
    {
        /// <summary>
        /// 创建生成失败异常。
        /// </summary>
        /// <param name="message">中文原因</param>
        public ToolFailureException(string message)
            : base(message)
        {
        }
    }
}