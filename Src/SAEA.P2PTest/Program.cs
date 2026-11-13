using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Common;
using SAEA.P2PTest.Server;
using SAEA.P2PTest.Tests;

namespace SAEA.P2PTest
{
    class Program
    {
        /// <summary>
        /// 需要取值的服务端参数；用于识别未识别参数时跳过取值
        /// </summary>
        private static readonly string[] ValueOptions =
        {
            "--config",
            "--listen",
            "--stun-port",
            "--turn-port",
            "--tls-port",
            "--realm",
            "--shared-secret",
            "--credential-ttl",
            "--nonce-lifetime",
            "--relay-min",
            "--relay-max",
            "--max-allocations",
            "--max-allocations-per-user",
            "--max-bytes",
            "--max-lifetime",
            "--tls-cert",
            "--tls-cert-password",
            "--change-address"
        };

        /// <summary>
        /// 不带取值的服务端参数
        /// </summary>
        private static readonly string[] FlagOptions = { "--server", "--ipv6", "--dual-mode", "--help", "-h" };

        static async Task Main(string[] args)
        {
            if (args != null && (HasFlag(args, "--help") || HasFlag(args, "-h")))
            {
                PrintUsage();
                return;
            }

            if (args != null && HasFlag(args, "--server"))
            {
                await RunServerAsync(args);
                return;
            }

            if (args != null && Array.Exists(args, a => a == "--all"))
            {
                await RunAllAsync();
                Environment.ExitCode = TestHarness.HasFailures ? 1 : 0;
                return;
            }

            if (args != null && Array.Exists(args, a => a == "--smoke"))
            {
                await RunSmokeAsync();
                return;
            }

            if (args != null && Array.Exists(args, a => a == "--tls"))
            {
                TestHarness.Reset();
                await TlsTest.RunAsync();
                await TlsInteropTest.RunAsync();
                Environment.ExitCode = TestHarness.HasFailures ? 1 : 0;
                await Task.Delay(500);
                return;
            }

            if (args != null && Array.Exists(args, a => a == "--tls-stress"))
            {
                TestHarness.Reset();
                await TlsStressTest.RunAsync();
                Environment.ExitCode = TestHarness.HasFailures ? 1 : 0;
                await Task.Delay(500);
                return;
            }

            if (args != null && Array.Exists(args, a => a == "--bench-tls"))
            {
                TestHarness.Reset();
                await TlsBenchmark.RunAsync();
                Environment.ExitCode = TestHarness.HasFailures ? 1 : 0;
                await Task.Delay(500);
                return;
            }

            if (args != null && Array.Exists(args, a => a == "--bench-iocp"))
            {
                TestHarness.Reset();
                await IocpBenchmark.RunAsync();
                Environment.ExitCode = TestHarness.HasFailures ? 1 : 0;
                await Task.Delay(500);
                return;
            }

            while (true)
            {
                ConsoleHelper.Title = "SAEA.P2P Test";

                ConsoleHelper.WriteLine("SAEA.P2P Test");
                ConsoleHelper.WriteLine("1 = BuilderTest");
                ConsoleHelper.WriteLine("2 = ErrorCodeTest");
                ConsoleHelper.WriteLine("3 = ProtocolTest");
                ConsoleHelper.WriteLine("4 = QuickStartTest");
                ConsoleHelper.WriteLine("5 = ServerTest");
                ConsoleHelper.WriteLine("6 = ClientTest");
                ConsoleHelper.WriteLine("7 = HolePunchTest（ICE 打洞）");
                ConsoleHelper.WriteLine("8 = RelayTest（TURN 中继）");
                ConsoleHelper.WriteLine("9 = LocalDiscoveryTest");
                ConsoleHelper.WriteLine("10 = AuthEncryptionTest");
                ConsoleHelper.WriteLine("11 = EdgeCaseTest");
                ConsoleHelper.WriteLine("12 = ProtocolAdvancedTest");
                ConsoleHelper.WriteLine("13 = SecurityAdvancedTest");
                ConsoleHelper.WriteLine("14 = RelayAdvancedTest（TURN 中继进阶）");
                ConsoleHelper.WriteLine("15 = ConcurrencyTest");
                ConsoleHelper.WriteLine("16 = LifecycleTest");
                ConsoleHelper.WriteLine("17 = IntegrationTest");
                ConsoleHelper.WriteLine("18 = PerformanceTest");
                ConsoleHelper.WriteLine("19 = Run all advanced tests");
                ConsoleHelper.WriteLine("20 = StreamDecoderTest");
                ConsoleHelper.WriteLine("21 = IocpReceiveBenchmark");
                ConsoleHelper.WriteLine("22 = StreamPipelineTest");
                ConsoleHelper.WriteLine("23 = UdpPipelineTest");
                ConsoleHelper.WriteLine("24 = TlsTest");
                ConsoleHelper.WriteLine("25 = TlsInteropTest");
                ConsoleHelper.WriteLine("26 = TlsStressTest");
                ConsoleHelper.WriteLine("27 = TlsBenchmark");
                ConsoleHelper.WriteLine("0 = Exit");

                var pressedKey = ConsoleHelper.ReadLine();

                switch (pressedKey)
                {
                    case "1":
                        BuilderTest.Run();
                        break;
                    case "2":
                        ErrorCodeTest.Run();
                        break;
                    case "3":
                        ProtocolTest.Run();
                        break;
                    case "4":
                        QuickStartTest.Run();
                        break;
                    case "5":
                        await ServerTest.RunAsync();
                        break;
                    case "6":
                        await ClientTest.RunAsync();
                        break;
                    case "7":
                        await HolePunchTest.RunAsync();
                        break;
                    case "8":
                        await RelayTest.RunAsync();
                        break;
                    case "9":
                        await LocalDiscoveryTest.RunAsync();
                        break;
                    case "10":
                        await AuthEncryptionTest.RunAsync();
                        break;
                    case "11":
                        EdgeCaseTest.Run();
                        break;
                    case "12":
                        ProtocolAdvancedTest.Run();
                        break;
                    case "13":
                        SecurityAdvancedTest.Run();
                        break;
                    case "14":
                        await RelayAdvancedTest.RunAsync();
                        break;
                    case "15":
                        ConcurrencyTest.Run();
                        break;
                    case "16":
                        await LifecycleTest.RunAsync();
                        break;
                    case "17":
                        await IntegrationTest.RunAsync();
                        break;
                    case "18":
                        PerformanceTest.Run();
                        break;
                    case "19":
                        await RunAllAsync();
                        break;
                    case "20":
                        StreamDecoderTest.Run();
                        break;
                    case "21":
                        await IocpBenchmark.RunAsync();
                        break;
                    case "22":
                        await StreamPipelineTest.RunAsync();
                        break;
                    case "23":
                        await UdpPipelineTest.RunAsync();
                        break;
                    case "24":
                        await TlsTest.RunAsync();
                        break;
                    case "25":
                        await TlsInteropTest.RunAsync();
                        break;
                    case "26":
                        await TlsStressTest.RunAsync();
                        break;
                    case "27":
                        await TlsBenchmark.RunAsync();
                        break;
                    case "0":
                        return;
                }
            }
        }

        static async Task RunAllAsync()
        {
            TestHarness.Reset();

            BuilderTest.Run();
            ErrorCodeTest.Run();
            ProtocolTest.Run();
            QuickStartTest.Run();
            EdgeCaseTest.Run();
            SpanPipelineTest.Run();
            ProtocolAdvancedTest.Run();
            SecurityAdvancedTest.Run();
            await ServerTest.RunAsync();
            await ClientTest.RunAsync();
            await HolePunchTest.RunAsync();
            await RelayTest.RunAsync();
            await RelayAdvancedTest.RunAsync();
            ConcurrencyTest.Run();
            await LifecycleTest.RunAsync();
            await IntegrationTest.RunAsync();
            PerformanceTest.Run();
            StreamDecoderTest.Run();
            await StreamDecoderTest.RunIocpClientAsync();
            await StreamDecoderTest.RunIocpServerAsync();
            await StreamDecoderTest.RunIocpClientSpanExceptionAsync();
            await StreamPipelineTest.RunAsync();
            await UdpPipelineTest.RunAsync();
            await TlsTest.RunAsync();
            await TlsInteropTest.RunAsync();
            await TlsStressTest.RunAsync();

            TestHarness.WriteSummary("ALL ADVANCED TESTS");

            // ConsoleHelper 输出为异步队列，退出前留出时间刷出。
            await Task.Delay(500);
        }

        /// <summary>
        /// 离线冒烟自测：只跑不依赖外网、不启动 TLS/TURN/ICE 重活的套件
        /// </summary>
        static async Task RunSmokeAsync()
        {
            TestHarness.Reset();

            BuilderTest.Run();
            ErrorCodeTest.Run();
            QuickStartTest.Run();
            ProtocolTest.Run();
            ProtocolAdvancedTest.Run();
            EdgeCaseTest.Run();
            ConcurrencyTest.Run();
            SecurityAdvancedTest.Run();
            await ServerTest.RunAsync();
            await ClientTest.RunAsync();
            await LifecycleTest.RunAsync();
            await IntegrationTest.RunAsync();

            TestHarness.WriteSummary("SMOKE");
            Environment.ExitCode = TestHarness.HasFailures ? 1 : 0;

            // ConsoleHelper 输出为异步队列，退出前留出时间刷出。
            await Task.Delay(500);
        }

        /// <summary>
        /// 打印用法；只有开关与配置项名称，不回显任何密钥或口令
        /// </summary>
        static void PrintUsage()
        {
            Console.WriteLine("SAEA.P2P 自测程序用法：");
            Console.WriteLine("  （不带参数）进入交互菜单");
            Console.WriteLine("  --all                运行全部高级自测");
            Console.WriteLine("  --smoke              离线冒烟自测（不跑 TLS/TURN/ICE/性能）");
            Console.WriteLine("  --tls                TLS 互通自测");
            Console.WriteLine("  --tls-stress         TLS 压力自测");
            Console.WriteLine("  --bench-tls          TLS 基准测试");
            Console.WriteLine("  --bench-iocp         IOCP 基准测试");
            Console.WriteLine("  --server             启动 STUN/TURN 服务端宿主");
            Console.WriteLine("  --config <path>      指定服务端 JSON 配置；省略时用默认值加命令行覆盖");
            Console.WriteLine("  --listen <ip>        监听地址");
            Console.WriteLine("  --stun-port <n>      STUN 独立监听端口，0 表示不单独监听");
            Console.WriteLine("  --turn-port <n>      TURN UDP/TCP 监听端口");
            Console.WriteLine("  --tls-port <n>       TURN over TLS 监听端口，0 表示不启用");
            Console.WriteLine("  --realm <s>          认证域");
            Console.WriteLine("  --shared-secret <s>  共享密钥");
            Console.WriteLine("  --credential-ttl <n> 凭据有效期，单位秒");
            Console.WriteLine("  --nonce-lifetime <n> nonce 有效期，单位秒");
            Console.WriteLine("  --relay-min <n>      中继端口区间下界");
            Console.WriteLine("  --relay-max <n>      中继端口区间上界");
            Console.WriteLine("  --max-allocations <n>            并发分配总数上限");
            Console.WriteLine("  --max-allocations-per-user <n>   单用户并发分配上限");
            Console.WriteLine("  --max-bytes <n>      单个分配的累计中继字节上限，0 表示不限制");
            Console.WriteLine("  --max-lifetime <n>   分配生命周期上限，单位秒");
            Console.WriteLine("  --tls-cert <path>    PFX 证书文件路径");
            Console.WriteLine("  --tls-cert-password <s>  证书口令");
            Console.WriteLine("  --change-address <ip>    RFC 5780 第二地址");
            Console.WriteLine("  --ipv6               改用 IPv6 监听");
            Console.WriteLine("  --dual-mode          要求双栈监听");
            Console.WriteLine("  --help, -h           打印本用法");
            Console.WriteLine("共享密钥与证书口令只能来自配置文件或 --shared-secret / --tls-cert-password，不会打印。");
        }

        /// <summary>
        /// 服务端宿主模式：解析配置与覆盖开关、启动监听、等待 Ctrl+C 后有序停止。
        /// 任何失败路径都只打印参数名与异常消息类型，不输出密钥与口令。
        /// </summary>
        /// <param name="args">命令行参数</param>
        /// <returns>运行到收到停止信号后完成的任务</returns>
        static async Task RunServerAsync(string[] args)
        {
            ServerConfig? config;
            try
            {
                ValidateServerArguments(args);
                config = LoadServerConfig(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("参数或配置错误：" + ex.GetType().Name + "：" + ex.Message);
                Environment.ExitCode = 1;
                return;
            }

            if (config == null)
            {
                return;
            }

            ServerHost host;
            try
            {
                host = new ServerHost(config);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("启动失败：" + ex.GetType().Name + "：" + ex.Message);
                Environment.ExitCode = 1;
                return;
            }

            using (host)
            using (var cts = new CancellationTokenSource())
            {
                try
                {
                    await host.StartAsync(cts.Token);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("启动失败：" + ex.GetType().Name + "：" + ex.Message);
                    Environment.ExitCode = 1;
                    return;
                }

                Console.WriteLine("服务端已启动，按 Ctrl+C 停止");

                var shutdown = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                Console.CancelKeyPress += (sender, e) =>
                {
                    // 先挡住运行时默认的强制终止，让主流程把监听释放干净再退出
                    e.Cancel = true;
                    shutdown.TrySetResult(true);
                };

                await shutdown.Task;
                Console.WriteLine("正在停止…");
            }
        }

        /// <summary>
        /// 加载配置；带 --config 但文件不存在时直接报错返回 null，并置退出码 1
        /// </summary>
        /// <param name="args">命令行参数</param>
        /// <returns>加载并应用覆盖后的配置；失败时为 null</returns>
        static ServerConfig? LoadServerConfig(string[] args)
        {
            var path = GetOptionValue(args, "--config");
            if (!string.IsNullOrEmpty(path) && !File.Exists(path))
            {
                Console.Error.WriteLine("找不到配置文件：" + path);
                Environment.ExitCode = 1;
                return null;
            }

            var config = string.IsNullOrEmpty(path) ? new ServerConfig() : ServerConfig.Load(path);

            if (config.RelayPortRange == null)
            {
                // --config 与内建默认值都保证非空，这里只兜底避免覆盖逻辑解引用空对象
                config.RelayPortRange = new RelayPortRangeConfig();
            }

            ApplyServerOverrides(config, args);
            return config;
        }

        /// <summary>
        /// 把命令行覆盖开关应用到配置；未出现的开关保持原值
        /// </summary>
        /// <param name="config">服务端配置</param>
        /// <param name="args">命令行参数</param>
        static void ApplyServerOverrides(ServerConfig config, string[] args)
        {
            var listen = GetOptionValue(args, "--listen");
            if (listen != null)
            {
                config.ListenAddress = listen;
            }

            config.StunPort = GetIntOption(args, "--stun-port", config.StunPort);
            config.TurnPort = GetIntOption(args, "--turn-port", config.TurnPort);
            config.TurnTlsPort = GetIntOption(args, "--tls-port", config.TurnTlsPort);

            var realm = GetOptionValue(args, "--realm");
            if (realm != null)
            {
                config.Realm = realm;
            }

            var sharedSecret = GetOptionValue(args, "--shared-secret");
            if (sharedSecret != null)
            {
                config.SharedSecret = sharedSecret;
            }

            config.CredentialTtlSeconds = GetIntOption(args, "--credential-ttl", config.CredentialTtlSeconds);
            config.NonceLifetimeSeconds = GetIntOption(args, "--nonce-lifetime", config.NonceLifetimeSeconds);
            config.RelayPortRange.Min = GetIntOption(args, "--relay-min", config.RelayPortRange.Min);
            config.RelayPortRange.Max = GetIntOption(args, "--relay-max", config.RelayPortRange.Max);
            config.MaxAllocations = GetIntOption(args, "--max-allocations", config.MaxAllocations);
            config.MaxAllocationsPerUser = GetIntOption(args, "--max-allocations-per-user", config.MaxAllocationsPerUser);
            config.MaxBytes = GetLongOption(args, "--max-bytes", config.MaxBytes);
            config.MaxLifetimeSeconds = GetUIntOption(args, "--max-lifetime", config.MaxLifetimeSeconds);

            var certPath = GetOptionValue(args, "--tls-cert");
            if (certPath != null)
            {
                config.TlsCertificatePath = certPath;
            }

            var certPassword = GetOptionValue(args, "--tls-cert-password");
            if (certPassword != null)
            {
                config.TlsCertificatePassword = certPassword;
            }

            var changeAddress = GetOptionValue(args, "--change-address");
            if (changeAddress != null)
            {
                config.ChangeRequestAddressPair = changeAddress;
            }

            if (HasFlag(args, "--ipv6"))
            {
                config.EnableIpv6 = true;
            }

            if (HasFlag(args, "--dual-mode"))
            {
                config.EnableDualMode = true;
            }
        }

        /// <summary>
        /// 校验参数都属于服务端模式支持的开关，避免拼错后静默走默认值
        /// </summary>
        /// <param name="args">命令行参数</param>
        /// <exception cref="ArgumentException">存在未识别参数或开关缺少取值</exception>
        static void ValidateServerArguments(string[] args)
        {
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (Contains(FlagOptions, arg))
                {
                    continue;
                }

                if (Contains(ValueOptions, arg))
                {
                    if (i + 1 >= args.Length)
                    {
                        throw new ArgumentException("参数 " + arg + " 缺少取值");
                    }
                    // 跳过该开关的取值，否则会被误判为未识别参数
                    i++;
                    continue;
                }

                throw new ArgumentException("未识别的参数：" + arg + "，可用 --help 查看用法");
            }
        }

        /// <summary>
        /// 取开关后面的取值；未出现时返回 null
        /// </summary>
        /// <param name="args">命令行参数</param>
        /// <param name="name">开关名</param>
        /// <returns>开关取值；未出现时为 null</returns>
        /// <exception cref="ArgumentException">开关存在但缺少取值</exception>
        static string? GetOptionValue(string[] args, string name)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (i + 1 >= args.Length)
                {
                    throw new ArgumentException("参数 " + name + " 缺少取值");
                }

                return args[i + 1];
            }

            return null;
        }

        /// <summary>
        /// 判断布尔开关是否存在
        /// </summary>
        /// <param name="args">命令行参数</param>
        /// <param name="name">开关名</param>
        /// <returns>存在返回 true</returns>
        static bool HasFlag(string[] args, string name)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 读取 int 型覆盖开关；未出现时返回默认值
        /// </summary>
        /// <param name="args">命令行参数</param>
        /// <param name="name">开关名</param>
        /// <param name="current">未出现时保留的当前值</param>
        /// <returns>覆盖后的值</returns>
        /// <exception cref="ArgumentException">取值不是 int</exception>
        static int GetIntOption(string[] args, string name, int current)
        {
            var text = GetOptionValue(args, name);
            if (text == null)
            {
                return current;
            }

            int value;
            if (!TryParseInt(text, out value))
            {
                throw new ArgumentException("参数 " + name + " 需要整数，实际为：" + text);
            }

            return value;
        }

        /// <summary>
        /// 读取 uint 型覆盖开关；未出现时返回默认值
        /// </summary>
        /// <param name="args">命令行参数</param>
        /// <param name="name">开关名</param>
        /// <param name="current">未出现时保留的当前值</param>
        /// <returns>覆盖后的值</returns>
        /// <exception cref="ArgumentException">取值不是 uint</exception>
        static uint GetUIntOption(string[] args, string name, uint current)
        {
            var text = GetOptionValue(args, name);
            if (text == null)
            {
                return current;
            }

            uint value;
            if (!uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                throw new ArgumentException("参数 " + name + " 需要非负整数，实际为：" + text);
            }

            return value;
        }

        /// <summary>
        /// 读取 long 型覆盖开关；未出现时返回默认值
        /// </summary>
        /// <param name="args">命令行参数</param>
        /// <param name="name">开关名</param>
        /// <param name="current">未出现时保留的当前值</param>
        /// <returns>覆盖后的值</returns>
        /// <exception cref="ArgumentException">取值不是 long</exception>
        static long GetLongOption(string[] args, string name, long current)
        {
            var text = GetOptionValue(args, name);
            if (text == null)
            {
                return current;
            }

            long value;
            if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                throw new ArgumentException("参数 " + name + " 需要整数，实际为：" + text);
            }

            return value;
        }

        /// <summary>
        /// 按不变区域性解析整数，避免受系统区域设置影响
        /// </summary>
        /// <param name="text">待解析文本</param>
        /// <param name="value">解析结果</param>
        /// <returns>解析成功返回 true</returns>
        static bool TryParseInt(string text, out int value)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// 判断数组里是否存在指定项，忽略大小写
        /// </summary>
        /// <param name="items">候选数组</param>
        /// <param name="value">待匹配值</param>
        /// <returns>存在返回 true</returns>
        static bool Contains(string[] items, string value)
        {
            for (var i = 0; i < items.Length; i++)
            {
                if (string.Equals(items[i], value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}