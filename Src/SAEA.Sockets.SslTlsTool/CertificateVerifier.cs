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
*文件名： CertificateVerifier
*版本号： net10.0
*唯一标识：bda33210-eff5-4509-a8f3-4665618aadc4
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 13:50:48
*描述：生成结果的自检：PFX/PEM 读回、链、SAN/EKU 与回环握手
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 14:42:12
*修改人： yswenli
*版本号： net10.0
*描述：工程更名为 SslTlsTool，同步命名空间
*
*****************************************************************************/


namespace SAEA.Sockets.SslTlsTool;

/// <summary>
/// 自检失败，携带失败项编号。
/// </summary>
internal sealed class CertificateVerificationException : Exception
{
    /// <summary>失败项编号（1..5）。</summary>
    public int Item { get; }

    /// <summary>
    /// 创建自检失败异常。
    /// </summary>
    /// <param name="item">失败项编号</param>
    /// <param name="message">失败原因</param>
    public CertificateVerificationException(int item, string message)
        : base(message)
    {
        Item = item;
    }
}

/// <summary>
/// 单个待自检证书的路径与期望值。
/// </summary>
internal sealed class CertificateVerificationTarget
{
    /// <summary>显示用名称。</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>PFX 路径；不存在或未产出时为 null。</summary>
    public string? PfxPath { get; set; }

    /// <summary>证书 PEM 路径。</summary>
    public string? CrtPath { get; set; }

    /// <summary>私钥 PEM 路径。</summary>
    public string? KeyPath { get; set; }

    /// <summary>读取 PFX 的口令。</summary>
    public string? Password { get; set; }

    /// <summary>是否为根 CA。</summary>
    public bool IsCertificateAuthority { get; set; }

    /// <summary>是否为服务端叶子。</summary>
    public bool IsServer { get; set; }

    /// <summary>是否为客户端叶子。</summary>
    public bool IsClient { get; set; }

    /// <summary>期望的 DNS SAN；由工具按命令行输入独立计算。</summary>
    public string[]? ExpectedDnsNames { get; set; }

    /// <summary>期望的 IP SAN；由工具按命令行输入独立计算。</summary>
    public string[]? ExpectedIpAddresses { get; set; }
}

/// <summary>
/// 一次自检需要的全部输入。
/// </summary>
internal sealed class CertificateVerificationPlan
{
    /// <summary>需要做 PFX/PEM 读回与内容断言的证书。</summary>
    public IList<CertificateVerificationTarget> Targets { get; } = new List<CertificateVerificationTarget>();

    /// <summary>CA 证书 DER 字节；仅 CA 模式非 null。</summary>
    public byte[]? AuthorityCertificate { get; set; }

    /// <summary>服务端 PFX 路径；仅 CA 模式非 null。</summary>
    public string? ServerPfxPath { get; set; }

    /// <summary>客户端 PFX 路径；仅 CA 模式非 null。</summary>
    public string? ClientPfxPath { get; set; }

    /// <summary>读取服务端/客户端 PFX 的口令。</summary>
    public string? PfxPassword { get; set; }
}

/// <summary>
/// 生成结果自检：对应设计中的五项。任一项失败抛 <see cref="CertificateVerificationException"/>。
/// </summary>
internal static class CertificateVerifier
{
    private const string SubjectAlternativeNameOid = "2.5.29.17";

    private const string EnhancedKeyUsageOid = "2.5.29.37";

    private const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";

    private const string ClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";

    /// <summary>
    /// 依次执行五项自检，任一项失败立即抛异常并停止。
    /// </summary>
    /// <param name="plan">自检输入</param>
    public static void Verify(CertificateVerificationPlan plan)
    {
        VerifyPfxReadBack(plan);
        VerifyPemReadBack(plan);
        VerifyCertificateChain(plan);
        VerifyCertificateContent(plan);
        VerifyLoopbackHandshake(plan);
    }

    /// <summary>
    /// 按 CN 与显式 DNS 列表独立计算期望的 DNS SAN，不复用库内推导实现。
    /// </summary>
    /// <param name="subject">本次实际生效的主题 DN</param>
    /// <param name="dnsNames">显式给出的 DNS 列表；null 表示未给</param>
    /// <returns>期望的 DNS SAN 顺序</returns>
    public static string[] ComputeExpectedDnsNames(string? subject, string[]? dnsNames)
    {
        if (dnsNames != null)
            return dnsNames;

        var result = new List<string>();
        var commonName = ExtractCommonName(subject);

        if (!string.IsNullOrWhiteSpace(commonName) && !IsIpAddress(commonName))
            AddDistinct(result, commonName);

        AddDistinct(result, "localhost");
        return result.ToArray();
    }

    /// <summary>
    /// 计算期望的 IP SAN：未显式给出时取 127.0.0.1。
    /// </summary>
    /// <param name="ipAddresses">显式给出的 IP 列表；null 表示未给</param>
    /// <returns>期望的 IP SAN</returns>
    public static string[] ComputeExpectedIpAddresses(string[]? ipAddresses)
    {
        return ipAddresses ?? new[] { "127.0.0.1" };
    }

    private static void VerifyPfxReadBack(CertificateVerificationPlan plan)
    {
        var count = 0;

        foreach (var target in plan.Targets)
        {
            if (target.PfxPath == null)
                continue;

            X509Certificate2 certificate;
            try
            {
                certificate = SaeaCertificateLoader.FromPfx(target.PfxPath, target.Password);
            }
            catch (Exception ex)
            {
                throw new CertificateVerificationException(1,
                    target.Label + "：" + target.PfxPath + " 无法读回（" + ex.Message + "）。");
            }

            using (certificate)
            {
                if (!certificate.HasPrivateKey)
                    throw new CertificateVerificationException(1,
                        target.Label + "：" + target.PfxPath + " 读回后没有私钥。");
            }

            count++;
        }

        Console.WriteLine("  1/5 PFX 读回：通过（" + count + " 份，均含私钥）");
    }

    private static void VerifyPemReadBack(CertificateVerificationPlan plan)
    {
        var count = 0;

        foreach (var target in plan.Targets)
        {
            if (target.CrtPath == null || target.KeyPath == null)
                continue;

            CheckPemFile(target.CrtPath, target.Label);
            CheckPemFile(target.KeyPath, target.Label);

            X509Certificate2 certificate;
            try
            {
                certificate = SaeaCertificateLoader.FromPem(target.CrtPath, target.KeyPath);
            }
            catch (Exception ex)
            {
                throw new CertificateVerificationException(2,
                    target.Label + "：" + target.CrtPath + " + " + target.KeyPath + " 无法读回（" +
                    ex.Message + "）。");
            }

            using (certificate)
            {
                if (!certificate.HasPrivateKey)
                    throw new CertificateVerificationException(2,
                        target.Label + "：PEM 读回后没有私钥。");
            }

            count++;
        }

        Console.WriteLine("  2/5 PEM 读回与 LF/无 BOM：通过（" + count + " 份）");
    }

    private static void CheckPemFile(string path, string label)
    {
        var bytes = File.ReadAllBytes(path);

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            throw new CertificateVerificationException(2, label + "：" + path + " 带 UTF-8 BOM。");

        for (var index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] == 0x0D)
                throw new CertificateVerificationException(2, label + "：" + path + " 含 CR，应为 LF 换行。");
        }
    }

    private static void VerifyCertificateChain(CertificateVerificationPlan plan)
    {
        if (plan.AuthorityCertificate == null)
        {
            Console.WriteLine("  3/5 链校验：自签模式不适用，已跳过");
            return;
        }

        // 用 X509CertificateLoader 而非已过时的 byte[] 构造函数加载 CA，避免 SYSLIB0057。
        using (var authority = X509CertificateLoader.LoadCertificate(plan.AuthorityCertificate))
        using (var serverCertificate = SaeaCertificateLoader.FromPfx(plan.ServerPfxPath!, plan.PfxPassword))
        using (var clientCertificate = SaeaCertificateLoader.FromPfx(plan.ClientPfxPath!, plan.PfxPassword))
        {
            BuildChain(authority, serverCertificate, "server");
            BuildChain(authority, clientCertificate, "client");
        }

        Console.WriteLine("  3/5 链校验（CustomRootTrust）：server/client 均通过");
    }

    private static void BuildChain(X509Certificate2 authority, X509Certificate2 leaf, string label)
    {
        using (var chain = new X509Chain())
        {
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(authority);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

            if (chain.Build(leaf))
                return;

            var statuses = new List<string>();
            foreach (var status in chain.ChainStatus)
                statuses.Add(status.Status.ToString());

            throw new CertificateVerificationException(3,
                label + " 证书链校验失败：" + string.Join(",", statuses.ToArray()) + "。");
        }
    }

    private static void VerifyCertificateContent(CertificateVerificationPlan plan)
    {
        foreach (var target in plan.Targets)
        {
            using (var certificate = LoadTargetCertificate(target))
            {
                if (target.IsCertificateAuthority)
                    VerifyCertificateAuthority(certificate, target.Label);

                if (target.IsServer)
                {
                    VerifyAlternativeNames(certificate, target);
                    VerifyEnhancedKeyUsage(certificate, target.Label, ServerAuthenticationOid, "serverAuth");
                }

                if (target.IsClient)
                {
                    VerifyNoAlternativeNames(certificate, target.Label);
                    VerifyEnhancedKeyUsage(certificate, target.Label, ClientAuthenticationOid, "clientAuth");
                }
            }
        }

        Console.WriteLine("  4/5 SAN / BasicConstraints / EKU：通过");
    }

    private static X509Certificate2 LoadTargetCertificate(CertificateVerificationTarget target)
    {
        if (target.PfxPath != null)
            return SaeaCertificateLoader.FromPfx(target.PfxPath, target.Password);

        if (target.CrtPath != null && target.KeyPath != null)
            return SaeaCertificateLoader.FromPem(target.CrtPath, target.KeyPath);

        throw new CertificateVerificationException(4, target.Label + "：没有可读取的证书文件。");
    }

    private static void VerifyCertificateAuthority(X509Certificate2 certificate, string label)
    {
        var extension = certificate.Extensions["2.5.29.19"];
        if (extension == null)
            throw new CertificateVerificationException(4, label + "：缺少 BasicConstraints 扩展。");

        var basicConstraints = new X509BasicConstraintsExtension();
        basicConstraints.CopyFrom(extension);
        if (!basicConstraints.CertificateAuthority)
            throw new CertificateVerificationException(4, label + "：BasicConstraints.CA 不是 true。");
    }

    private static void VerifyEnhancedKeyUsage(X509Certificate2 certificate, string label, string oid,
        string usageName)
    {
        var extension = certificate.Extensions[EnhancedKeyUsageOid];
        if (extension == null)
            throw new CertificateVerificationException(4, label + "：缺少 EKU 扩展，期望包含 " + usageName + "。");

        var enhancedKeyUsage = new X509EnhancedKeyUsageExtension();
        enhancedKeyUsage.CopyFrom(extension);
        foreach (var item in enhancedKeyUsage.EnhancedKeyUsages)
        {
            if (string.Equals(item.Value, oid, StringComparison.Ordinal))
                return;
        }

        throw new CertificateVerificationException(4, label + "：EKU 不包含 " + usageName + "（" + oid + "）。");
    }

    private static void VerifyAlternativeNames(X509Certificate2 certificate, CertificateVerificationTarget target)
    {
        List<string> dnsNames;
        List<IPAddress> ipAddresses;
        bool hasExtension;
        ReadAlternativeNames(certificate, out dnsNames, out ipAddresses, out hasExtension);

        if (!hasExtension)
            throw new CertificateVerificationException(4, target.Label + "：证书缺少 SAN 扩展。");

        var expectedDnsNames = target.ExpectedDnsNames ?? Array.Empty<string>();
        var expectedIpAddresses = new List<IPAddress>();
        foreach (var value in target.ExpectedIpAddresses ?? Array.Empty<string>())
        {
            IPAddress? address;
            if (!IPAddress.TryParse(value, out address) || address == null)
                throw new CertificateVerificationException(4, target.Label + "：期望的 IP SAN 无效：" + value + "。");

            expectedIpAddresses.Add(address);
        }

        if (expectedDnsNames.Length != dnsNames.Count || expectedIpAddresses.Count != ipAddresses.Count)
            throw SanMismatch(target.Label, expectedDnsNames, expectedIpAddresses, dnsNames, ipAddresses);

        foreach (var expected in expectedDnsNames)
        {
            if (!ContainsDnsName(dnsNames, expected))
                throw SanMismatch(target.Label, expectedDnsNames, expectedIpAddresses, dnsNames, ipAddresses);
        }

        foreach (var expected in expectedIpAddresses)
        {
            if (!ContainsIpAddress(ipAddresses, expected))
                throw SanMismatch(target.Label, expectedDnsNames, expectedIpAddresses, dnsNames, ipAddresses);
        }
    }

    private static void VerifyNoAlternativeNames(X509Certificate2 certificate, string label)
    {
        List<string> dnsNames;
        List<IPAddress> ipAddresses;
        bool hasExtension;
        ReadAlternativeNames(certificate, out dnsNames, out ipAddresses, out hasExtension);

        if (hasExtension && (dnsNames.Count > 0 || ipAddresses.Count > 0))
            throw new CertificateVerificationException(4, label + "：客户端证书不应包含 SAN。");
    }

    private static CertificateVerificationException SanMismatch(string label, string[] expectedDnsNames,
        IList<IPAddress> expectedIpAddresses, IList<string> actualDnsNames, IList<IPAddress> actualIpAddresses)
    {
        return new CertificateVerificationException(4,
            label + "：SAN 不匹配。期望 DNS=[" + string.Join(",", expectedDnsNames) + "] IP=[" +
            JoinIpAddresses(expectedIpAddresses) + "]；实际 DNS=[" + string.Join(",", ToArray(actualDnsNames)) +
            "] IP=[" + JoinIpAddresses(actualIpAddresses) + "]。");
    }

    private static string[] ToArray(IList<string> values)
    {
        var result = new string[values.Count];
        values.CopyTo(result, 0);
        return result;
    }

    private static string JoinIpAddresses(IList<IPAddress> addresses)
    {
        var values = new string[addresses.Count];
        for (var index = 0; index < addresses.Count; index++)
            values[index] = addresses[index].ToString();

        return string.Join(",", values);
    }

    private static bool ContainsDnsName(IList<string> values, string expected)
    {
        foreach (var value in values)
        {
            if (string.Equals(value, expected, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool ContainsIpAddress(IList<IPAddress> values, IPAddress expected)
    {
        foreach (var value in values)
        {
            if (value.Equals(expected))
                return true;
        }

        return false;
    }

    private static void ReadAlternativeNames(X509Certificate2 certificate, out List<string> dnsNames,
        out List<IPAddress> ipAddresses, out bool hasExtension)
    {
        dnsNames = new List<string>();
        ipAddresses = new List<IPAddress>();

        var extension = certificate.Extensions[SubjectAlternativeNameOid];
        hasExtension = extension != null;
        if (extension == null)
            return;

        try
        {
            var alternativeNames =
                new X509SubjectAlternativeNameExtension(extension.RawData, extension.Critical);
            foreach (var name in alternativeNames.EnumerateDnsNames())
                dnsNames.Add(name);
            foreach (var address in alternativeNames.EnumerateIPAddresses())
                ipAddresses.Add(address);
            return;
        }
        catch (Exception)
        {
            // 新版 API 解析失败时退回 ASN.1，避免因平台差异误判。
            dnsNames.Clear();
            ipAddresses.Clear();
        }

        ReadAlternativeNamesFromAsn(extension.RawData, dnsNames, ipAddresses);
    }

    private static void ReadAlternativeNamesFromAsn(byte[] rawData, List<string> dnsNames,
        List<IPAddress> ipAddresses)
    {
        var reader = new AsnReader(rawData, AsnEncodingRules.DER);
        var sequence = reader.ReadSequence();
        var dnsTag = new Asn1Tag(TagClass.ContextSpecific, 2);
        var ipTag = new Asn1Tag(TagClass.ContextSpecific, 7);

        while (sequence.HasData)
        {
            var tag = sequence.PeekTag();

            if (tag.HasSameClassAndValue(dnsTag))
            {
                dnsNames.Add(sequence.ReadCharacterString(UniversalTagNumber.IA5String, dnsTag));
            }
            else if (tag.HasSameClassAndValue(ipTag))
            {
                ipAddresses.Add(new IPAddress(sequence.ReadOctetString(ipTag)));
            }
            else
            {
                sequence.ReadEncodedValue();
            }
        }
    }

    private static string? ExtractCommonName(string? subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
            return null;

        foreach (var part in SplitDistinguishedName(subject!))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0)
                continue;

            var name = part.Substring(0, separator).Trim();
            if (!string.Equals(name, "CN", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = part.Substring(separator + 1).Trim();
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                value = value.Substring(1, value.Length - 2);

            return value;
        }

        return null;
    }

    private static IEnumerable<string> SplitDistinguishedName(string value)
    {
        var builder = new StringBuilder();
        var quoted = false;

        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];

            if (current == '\\' && index + 1 < value.Length)
            {
                builder.Append(current);
                builder.Append(value[index + 1]);
                index++;
                continue;
            }

            if (current == '"')
                quoted = !quoted;

            if (current == ',' && !quoted)
            {
                yield return builder.ToString();
                builder.Clear();
                continue;
            }

            builder.Append(current);
        }

        yield return builder.ToString();
    }

    private static bool IsIpAddress(string value)
    {
        var address = default(IPAddress);
        return IPAddress.TryParse(value, out address);
    }

    private static void AddDistinct(IList<string> values, string value)
    {
        foreach (var existing in values)
        {
            if (string.Equals(existing, value, StringComparison.OrdinalIgnoreCase))
                return;
        }

        values.Add(value);
    }

    private static void VerifyLoopbackHandshake(CertificateVerificationPlan plan)
    {
        try
        {
            RunLoopbackHandshakeAsync(plan).GetAwaiter().GetResult();
        }
        catch (CertificateVerificationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CertificateVerificationException(5, "回环握手失败：" + ex.Message);
        }

        Console.WriteLine("  5/5 回环握手：主机名匹配通过，未校验证书链；链证据见第 3 项");
    }

    private static async Task RunLoopbackHandshakeAsync(CertificateVerificationPlan plan)
    {
        using (var serverCertificate = SaeaCertificateLoader.FromPfx(plan.ServerPfxPath!, plan.PfxPassword))
        using (var clientCertificate = SaeaCertificateLoader.FromPfx(plan.ClientPfxPath!, plan.PfxPassword))
        {
            Exception? lastStartError = null;

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                var port = GetFreePort();

                var serverOption = SocketOptionBuilder.Instance
                    .UseStream()
                    .SetIP("127.0.0.1")
                    .SetPort(port)
                    .SetReadBufferSize(8192)
                    .WithSsl(options =>
                    {
                        options.ServerCertificate = serverCertificate;
                        options.ClientCertificateRequired = true;
                        options.HandshakeTimeout = TimeSpan.FromSeconds(15);
                    })
                    .Build();

                var server = new SaeaServerSocket(serverOption);
                server.OnAccepted += obj =>
                {
                    var channel = obj as ChannelInfo;
                    if (channel == null) return;

                    Task.Run(async () =>
                    {
                        try
                        {
                            var buffer = new byte[64];
                            var read = await channel.Stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                            if (read > 0)
                            {
                                await channel.Stream.WriteAsync(buffer, 0, read).ConfigureAwait(false);
                                await channel.Stream.FlushAsync().ConfigureAwait(false);
                            }
                        }
                        catch
                        {
                        }
                    });
                };

                try
                {
                    server.Start();
                }
                catch (Exception ex)
                {
                    // Start 在 _listener 非空时会直接返回，重试前必须先 Stop 释放监听。
                    lastStartError = ex;
                    try { server.Stop(); } catch { }
                    try { server.Dispose(); } catch { }
                    continue;
                }

                var nameMismatchSeen = false;

                var clientOption = SocketOptionBuilder.Instance
                    .UseStream()
                    .SetIP("127.0.0.1")
                    .SetPort(port)
                    .SetReadBufferSize(8192)
                    .WithSsl(options =>
                    {
                        options.TargetHost = "localhost";
                        options.ClientCertificates = new X509Certificate2Collection(clientCertificate);
                        options.HandshakeTimeout = TimeSpan.FromSeconds(15);
                        options.CertificateValidationCallback = (sender, certificate, chain, errors) =>
                        {
                            // 只容忍链错误，主机名不匹配必须导致握手失败。
                            if ((errors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0)
                            {
                                nameMismatchSeen = true;
                                return false;
                            }

                            return true;
                        };
                    })
                    .Build();

                var client = new SaeaClientSocket(clientOption);
                try
                {
                    using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                    {
                        await client.ConnectAsync(cancellation.Token).ConfigureAwait(false);
                    }

                    if (nameMismatchSeen)
                        throw new CertificateVerificationException(5, "客户端报告服务端证书主机名不匹配。");

                    if (!client.IsAuthenticated)
                        throw new CertificateVerificationException(5, "客户端 TLS 握手未完成。");

                    var stream = client.GetStream();
                    var payload = Encoding.UTF8.GetBytes("saea-ssl-tls-verify");
                    await stream.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);

                    var response = new byte[payload.Length];
                    var total = 0;
                    while (total < response.Length)
                    {
                        var read = await stream.ReadAsync(response, total, response.Length - total)
                            .ConfigureAwait(false);
                        if (read <= 0) break;
                        total += read;
                    }

                    if (total != payload.Length)
                        throw new CertificateVerificationException(5,
                            "回环收发失败：期望 " + payload.Length + " 字节，实际 " + total + " 字节。");

                    for (var index = 0; index < payload.Length; index++)
                    {
                        if (payload[index] != response[index])
                            throw new CertificateVerificationException(5, "回环收发内容不一致。");
                    }

                    return;
                }
                catch (CertificateVerificationException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new CertificateVerificationException(5, "回环握手失败：" + ex.Message);
                }
                finally
                {
                    try { client.Dispose(); } catch { }
                    try { server.Stop(); } catch { }
                    try { server.Dispose(); } catch { }
                }
            }

            throw new CertificateVerificationException(5,
                "服务端连续 3 次绑定端口失败：" + (lastStartError == null ? "未知原因" : lastStartError.Message));
        }
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}