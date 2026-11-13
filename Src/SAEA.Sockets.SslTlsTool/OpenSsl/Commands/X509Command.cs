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
*命名空间：SAEA.Sockets.SslTlsTool.OpenSsl.Commands
*文件名： X509Command
*版本号： net10.0
*唯一标识：591c3c00-c0e9-4044-a24e-46f0b9a96536
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 13:54:11
*描述：x509 子命令的读取与格式转换
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 14:30:26
*修改人： yswenli
*版本号： net10.0
*描述：新增 x509 -req 用 CA 签发 CSR
*
*****************************************************************************/
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
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
        "-passin", "-CAcreateserial", "-days", "-extfile", "-extensions", "-copy_extensions",
    };
    /// <summary>执行一次 x509 调用。</summary>
    /// <param name="args">子命令之后的参数</param>
    /// <param name="interactive">是否来自交互模式</param>
    /// <returns>退出码，0 成功、1 失败</returns>
    public static int Run(string[] args, bool interactive)
    {
        var reader = new OptionReader("x509", args, Allowed);
        // -text / -nameopt 无论其它参数是否合法都先报错；-req 走签发分支。
        if (reader.Has("-text"))
            throw new OpenSslException("x509: -text 暂不支持；请用 -subject/-issuer/-dates/-serial/-fingerprint，或用外部工具查看完整 ASN.1。");
        if (reader.Has("-nameopt"))
            throw new OpenSslException("x509: -nameopt 暂不支持；姓名固定 oneline 风格（CN = localhost, O = SAEA）。");
        if (reader.Has("-req"))
            return SignRequest(reader, interactive);
        foreach (var option in new[]
                 { "-CA", "-CAkey", "-passin", "-CAcreateserial", "-days", "-extfile", "-extensions", "-copy_extensions" })
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
        return ReadFile(path);
    }
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
        // 序列号文件用 System.Numerics，CsrCodec 用 BouncyCastle 的 BigInteger，按十进制字符串转接。
        var serial = new BigInteger(SerialFile.Next(caPath, reader.Has("-CAcreateserial")).ToString(CultureInfo.InvariantCulture));
        var cert = CsrCodec.Sign(csr, key, caCert, serial,
            DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddDays(days), extensions, "SHA256");
        var derOut = IsDer(reader, "-outform");
        WriteOutput(reader, derOut, CertificateCodec.Write(cert, derOut));
        return 0;
    }

    private static IList<Extension> ExtensionsFor(OptionReader reader, Pkcs10CertificationRequest csr, string? copy) =>
        reader.Has("-extfile")
            ? ExtensionSpec.FromConfig(MiniConfigReader.Load(reader.GetRequired("-extfile")), reader.GetRequired("-extensions"))
            : copy == "copy" ? ExtensionSpec.CopyFromRequest(csr) : new List<Extension>();

    private static byte[] ReadFile(string path) =>
        File.Exists(path) ? File.ReadAllBytes(path) : throw new OpenSslException($"error: 找不到文件 \"{path}\"。");
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