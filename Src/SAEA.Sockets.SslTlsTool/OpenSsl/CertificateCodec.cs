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
*命名空间：SAEA.Sockets.SslTlsTool.OpenSsl
*文件名： CertificateCodec
*版本号： net10.0
*唯一标识：efd2ae64-a313-473f-a04a-7beb870b131f
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 13:54:11
*描述：证书的 PEM/DER 读写与文本格式化
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 13:54:11
*修改人： yswenli
*版本号： net10.0
*描述：新增证书的 PEM/DER 读写与文本格式化
*
*****************************************************************************/
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
    /// <summary>读取第一张证书。</summary>
    /// <param name="data">输入字节</param>
    /// <param name="der">true 表示 DER，false 表示 PEM</param>
    /// <returns>第一张证书</returns>
    public static X509Certificate ReadFirst(byte[] data, bool der) => ReadAll(data, der)[0];
    /// <summary>PEM 只取 CERTIFICATE 块；DER 按单张证书解析。</summary>
    /// <param name="data">输入字节</param>
    /// <param name="der">true 表示 DER，false 表示 PEM</param>
    /// <returns>输入中的全部证书</returns>
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
    /// <param name="cert">证书</param>
    /// <param name="der">true 表示输出 DER，false 表示输出 PEM</param>
    /// <returns>证书字节</returns>
    public static byte[] Write(X509Certificate cert, bool der) => der
        ? cert.GetEncoded()
        : Encoding.ASCII.GetBytes(PemCodec.Encode("CERTIFICATE", cert.GetEncoded()));
    /// <summary>格式化主题名称。</summary>
    /// <param name="cert">证书</param>
    /// <returns>oneline 风格的姓名</returns>
    public static string FormatSubject(X509Certificate cert) => FormatName(cert.SubjectDN);
    /// <summary>格式化签发者名称。</summary>
    /// <param name="cert">证书</param>
    /// <returns>oneline 风格的姓名</returns>
    public static string FormatIssuer(X509Certificate cert) => FormatName(cert.IssuerDN);
    /// <summary>格式化生效与到期时间。</summary>
    /// <param name="cert">证书</param>
    /// <returns>notBefore 与 notAfter 两行</returns>
    public static string FormatDates(X509Certificate cert) =>
        "notBefore=" + FormatDate(cert.NotBefore) + "\nnotAfter=" + FormatDate(cert.NotAfter);
    /// <summary>格式化序列号。</summary>
    /// <param name="cert">证书</param>
    /// <returns>大写十六进制序列号</returns>
    public static string FormatSerial(X509Certificate cert) =>
        // 前缀由调用方拼，这里只返回大写十六进制。
        cert.SerialNumber.ToString(16).ToUpperInvariant();
    /// <summary>大写十六进制、按字节插冒号；algorithm 传 "SHA-1" 或 "SHA-256"。</summary>
    /// <param name="cert">证书</param>
    /// <param name="algorithm">摘要算法名</param>
    /// <returns>带算法前缀的指纹行</returns>
    public static string FormatFingerprint(X509Certificate cert, string algorithm)
    {
        var hex = Convert.ToHexString(DigestUtilities.CalculateDigest(algorithm, cert.GetEncoded()));
        // 每 2 个字符后补冒号，再去掉末尾多余的一个。
        var text = Regex.Replace(hex, "..", "$0:").TrimEnd(':');
        return (algorithm == "SHA-256" ? "SHA256 Fingerprint=" : "SHA1 Fingerprint=") + text;
    }
    /// <summary>把公钥编码成 PUBLIC KEY 块。</summary>
    /// <param name="cert">证书</param>
    /// <returns>公钥 PEM</returns>
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