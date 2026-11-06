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
*文件名： SslTlsToolOptions
*版本号： net10.0
*唯一标识：e9b4a684-a875-48e6-8209-f8c5475dcd57
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 13:50:48
*描述：证书生成工具的命令行解析与校验
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 14:42:12
*修改人： yswenli
*版本号： net10.0
*描述：工程与选项类型更名为 SslTlsTool，并修正文件唯一标识重复
*
*****************************************************************************/


namespace SAEA.Sockets.SslTlsTool;

/// <summary>
/// 证书生成工具的命令行参数。解析后即完成取值校验，非法的组合不会进入生成流程。
/// </summary>
internal sealed class SslTlsToolOptions
{
    private static readonly Regex NamePattern =
        new Regex("^[A-Za-z0-9._-]{1,100}$", RegexOptions.CultureInvariant);

    private static readonly string[] ReservedNames = { "ca", "server", "client" };

    /// <summary>是否生成单张自签证书。</summary>
    public bool SelfSigned { get; private set; }

    /// <summary>是否生成 CA + server + client。</summary>
    public bool Ca { get; private set; }

    /// <summary>是否只打印帮助。</summary>
    public bool Help { get; private set; }

    /// <summary>输出目录，默认 ./certs。</summary>
    public string OutDirectory { get; private set; } = "certs";

    /// <summary>自签模式文件基名；未给时为 null，由生成流程取 localhost。</summary>
    public string? Name { get; private set; }

    /// <summary>自签证书与服务端叶子的主题 DN；未给时为 null，由库取默认值。</summary>
    public string? Subject { get; private set; }

    /// <summary>CA 主题 DN；未给时为 null，由库取默认值。</summary>
    public string? CaSubject { get; private set; }

    /// <summary>客户端主题 DN；未给时为 null，由库取默认值。</summary>
    public string? ClientSubject { get; private set; }

    /// <summary>显式给定的服务端 DNS SAN；未给时为 null，表示由库按 CN 推导。</summary>
    public string[]? DnsNames { get; private set; }

    /// <summary>显式给定的服务端 IP SAN；未给时为 null，表示由库取默认值。</summary>
    public string[]? IpAddresses { get; private set; }

    /// <summary>叶子有效天数。</summary>
    public int Days { get; private set; } = 30;

    /// <summary>CA 有效天数。</summary>
    public int CaDays { get; private set; } = 3650;

    /// <summary>CA 有效天数是否显式给出。</summary>
    public bool CaDaysSpecified { get; private set; }

    /// <summary>RSA 密钥长度。</summary>
    public int KeySize { get; private set; } = 2048;

    /// <summary>PFX 口令；未给时为 null，由库取默认口令 saea。</summary>
    public string? Password { get; private set; }

    /// <summary>是否显式给出 --pwd（用于区分“未给”与“给了空串”）。</summary>
    public bool PasswordSpecified { get; private set; }

    /// <summary>复用 CA 的读取口令；未给时为 null。</summary>
    public string? CaPassword { get; private set; }

    /// <summary>是否显式给出 --ca-pwd。</summary>
    public bool CaPasswordSpecified { get; private set; }

    /// <summary>是否复用已存在的 CA 签新叶子。</summary>
    public bool ReuseCa { get; private set; }

    /// <summary>是否覆盖已存在文件。</summary>
    public bool Force { get; private set; }

    /// <summary>是否在生成后自检。</summary>
    public bool Verify { get; private set; }

    /// <summary>
    /// 解析命令行参数并校验取值。
    /// </summary>
    /// <param name="args">命令行参数</param>
    /// <param name="options">解析结果；失败时为未完成校验的实例</param>
    /// <param name="error">失败原因，成功时为 null</param>
    /// <returns>参数合法时返回 true</returns>
    public static bool TryParse(string[] args, out SslTlsToolOptions options, out string? error)
    {
        options = new SslTlsToolOptions();
        error = null;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];

            switch (argument)
            {
                case "--self-signed":
                    options.SelfSigned = true;
                    break;
                case "--ca":
                    options.Ca = true;
                    break;
                case "--reuse-ca":
                    options.ReuseCa = true;
                    break;
                case "--force":
                    options.Force = true;
                    break;
                case "--verify":
                    options.Verify = true;
                    break;
                case "--help":
                    options.Help = true;
                    break;
                case "--out":
                    {
                        string value;
                        if (!TryReadValue(args, ref index, out value))
                        {
                            error = "参数 --out 缺少取值。";
                            return false;
                        }

                        if (string.IsNullOrWhiteSpace(value))
                        {
                            error = "参数 --out 不能为空。";
                            return false;
                        }

                        options.OutDirectory = value;
                        break;
                    }
                case "--name":
                    {
                        string value;
                        if (!TryReadValue(args, ref index, out value))
                        {
                            error = "参数 --name 缺少取值。";
                            return false;
                        }

                        options.Name = value;
                        break;
                    }
                case "--cn":
                    {
                        string value;
                        if (!TryReadValue(args, ref index, out value))
                        {
                            error = "参数 --cn 缺少取值。";
                            return false;
                        }

                        options.Subject = value;
                        break;
                    }
                case "--ca-cn":
                    {
                        string value;
                        if (!TryReadValue(args, ref index, out value))
                        {
                            error = "参数 --ca-cn 缺少取值。";
                            return false;
                        }

                        options.CaSubject = value;
                        break;
                    }
                case "--client-cn":
                    {
                        string value;
                        if (!TryReadValue(args, ref index, out value))
                        {
                            error = "参数 --client-cn 缺少取值。";
                            return false;
                        }

                        options.ClientSubject = value;
                        break;
                    }
                case "--dns":
                    {
                        string value;
                        if (!TryReadValue(args, ref index, out value))
                        {
                            error = "参数 --dns 缺少取值。";
                            return false;
                        }

                        options.DnsNames = value.Split(',');
                        break;
                    }
                case "--ip":
                    {
                        string value;
                        if (!TryReadValue(args, ref index, out value))
                        {
                            error = "参数 --ip 缺少取值。";
                            return false;
                        }

                        options.IpAddresses = value.Split(',');
                        break;
                    }
                case "--days":
                    {
                        string value;
                        int days;
                        if (!TryReadValue(args, ref index, out value) ||
                            !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out days))
                        {
                            error = "参数 --days 需要一个 1..36500 的整数。";
                            return false;
                        }

                        options.Days = days;
                        break;
                    }
                case "--ca-days":
                    {
                        string value;
                        int days;
                        if (!TryReadValue(args, ref index, out value) ||
                            !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out days))
                        {
                            error = "参数 --ca-days 需要一个 1..36500 的整数。";
                            return false;
                        }

                        options.CaDays = days;
                        options.CaDaysSpecified = true;
                        break;
                    }
                case "--keysize":
                    {
                        string value;
                        int keySize;
                        if (!TryReadValue(args, ref index, out value) ||
                            !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out keySize))
                        {
                            error = "参数 --keysize 只接受 2048/3072/4096。";
                            return false;
                        }

                        options.KeySize = keySize;
                        break;
                    }
                case "--pwd":
                    {
                        string value;
                        if (!TryReadValue(args, ref index, out value))
                        {
                            error = "参数 --pwd 缺少取值。";
                            return false;
                        }

                        options.Password = value;
                        options.PasswordSpecified = true;
                        break;
                    }
                case "--ca-pwd":
                    {
                        string value;
                        if (!TryReadValue(args, ref index, out value))
                        {
                            error = "参数 --ca-pwd 缺少取值。";
                            return false;
                        }

                        options.CaPassword = value;
                        options.CaPasswordSpecified = true;
                        break;
                    }
                default:
                    error = "未知参数：" + argument + "。";
                    return false;
            }
        }

        return options.Validate(out error);
    }

    private static bool TryReadValue(string[] args, ref int index, out string value)
    {
        if (index + 1 >= args.Length)
        {
            value = string.Empty;
            return false;
        }

        var next = args[index + 1];
        // 下一个 token 仍是选项时视为缺少取值，避免把开关名当成路径或口令。
        if (next.StartsWith("--", StringComparison.Ordinal))
        {
            value = string.Empty;
            return false;
        }

        index++;
        value = next;
        return true;
    }

    private bool Validate(out string? error)
    {
        error = null;

        if (ReuseCa && !Ca)
        {
            error = "参数 --reuse-ca 必须与 --ca 同时使用。";
            return false;
        }

        if (ReuseCa && Force)
        {
            error = "参数 --reuse-ca 与 --force 互斥，只能选一个。";
            return false;
        }

        if (KeySize != 2048 && KeySize != 3072 && KeySize != 4096)
        {
            error = "参数 --keysize 只接受 2048/3072/4096。";
            return false;
        }

        if (Days < 1 || Days > 36500)
        {
            error = "参数 --days 必须在 1..36500 之间。";
            return false;
        }

        if (CaDays < 1 || CaDays > 36500)
        {
            error = "参数 --ca-days 必须在 1..36500 之间。";
            return false;
        }

        if (Ca && !ReuseCa && Days > CaDays)
        {
            error = "参数 --days 不能大于 --ca-days：新叶子的到期时间会晚于 CA。";
            return false;
        }

        if (!ValidateDnsNames(out error))
            return false;

        if (!ValidateIpAddresses(out error))
            return false;

        if (Name != null && !ValidateName(Name, out error))
            return false;

        return true;
    }

    private bool ValidateDnsNames(out string? error)
    {
        error = null;
        if (DnsNames == null)
            return true;

        foreach (var dnsName in DnsNames)
        {
            if (string.IsNullOrWhiteSpace(dnsName))
            {
                error = "参数 --dns 的取值为空；要使用默认值请省略该参数。";
                return false;
            }

            if (dnsName.IndexOf(',') >= 0 || ContainsWhiteSpace(dnsName) || dnsName.Length > 253)
            {
                error = "参数 --dns 的取值无效：" + dnsName + "（不能含空白或逗号，长度不超过 253）。";
                return false;
            }
        }

        return true;
    }

    private bool ValidateIpAddresses(out string? error)
    {
        error = null;
        if (IpAddresses == null)
            return true;

        foreach (var ipAddress in IpAddresses)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                error = "参数 --ip 的取值为空；要使用默认值请省略该参数。";
                return false;
            }

            var address = default(IPAddress);
            if (!IPAddress.TryParse(ipAddress, out address))
            {
                error = "参数 --ip 的取值不是合法的 IP 地址：" + ipAddress + "。";
                return false;
            }
        }

        return true;
    }

    private static bool ValidateName(string name, out string? error)
    {
        error = null;

        if (!NamePattern.IsMatch(name))
        {
            error = "参数 --name 只允许 1..100 个 [A-Za-z0-9._-] 字符。";
            return false;
        }

        if (name == "." || name == "..")
        {
            error = "参数 --name 不能是 . 或 ..。";
            return false;
        }

        foreach (var reservedName in ReservedNames)
        {
            if (string.Equals(name, reservedName, StringComparison.OrdinalIgnoreCase))
            {
                error = "参数 --name 不能使用保留名 ca/server/client。";
                return false;
            }
        }

        return true;
    }

    private static bool ContainsWhiteSpace(string value)
    {
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch))
                return true;
        }

        return false;
    }
}