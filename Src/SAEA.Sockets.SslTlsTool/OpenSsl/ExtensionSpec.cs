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
*文件名： ExtensionSpec
*版本号： net10.0
*唯一标识：231d8ea2-88b1-4141-9d66-726fd8a2fd64
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 14:21:12
*描述：解析 -addext 与扩展配置段
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 14:21:12
*修改人： yswenli
*版本号： net10.0
*描述：新增 -addext 与扩展配置段解析
*
*****************************************************************************/
using System.Net;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Pkcs;

namespace SAEA.Sockets.SslTlsTool.OpenSsl;

/// <summary>解析 -addext 与扩展配置段，并搬运 CSR 请求中的四类扩展。</summary>
internal static class ExtensionSpec
{
    private static readonly DerObjectIdentifier[] Copyable = { X509Extensions.SubjectAlternativeName, X509Extensions.BasicConstraints, X509Extensions.KeyUsage, X509Extensions.ExtendedKeyUsage };
    private static readonly Dictionary<string, DerObjectIdentifier> Oids = new(StringComparer.Ordinal)
    {
        ["subjectAltName"] = X509Extensions.SubjectAlternativeName,
        ["basicConstraints"] = X509Extensions.BasicConstraints,
        ["keyUsage"] = X509Extensions.KeyUsage,
        ["extendedKeyUsage"] = X509Extensions.ExtendedKeyUsage,
    };
    public static IList<Extension> ParseAddext(IEnumerable<string> values) => values.Select(item =>
    {
        var split = item.IndexOf('=');
        if (split <= 0) throw new OpenSslException($"req: 扩展需要 name=value 形式：\"{item}\"。");
        var name = item.Substring(0, split).Trim();
        var body = item.Substring(split + 1).Trim();
        var critical = body.StartsWith("critical,", StringComparison.Ordinal);
        var text = critical ? body.Substring(9).Trim() : body;
        // Extension 自带 OID；BC 2.6.2 的 X509Extension 只有 critical/value，OID 存在外层映射里。
        return new Extension(Oid(name), DerBoolean.GetInstance(critical), new DerOctetString(Build(name, text).GetEncoded()));
    }).ToList();
    public static IList<Extension> CopyFromRequest(Pkcs10CertificationRequest csr)
    {
        var requested = csr.GetRequestedExtensions();
        if (requested == null) return new List<Extension>();
        var result = new List<Extension>();
        foreach (var oid in Copyable)
        {
            var extension = requested.GetExtension(oid);
            if (extension != null) result.Add(new Extension(oid, DerBoolean.GetInstance(extension.IsCritical), extension.Value));
        }
        return result;
    }
    public static IList<Extension> FromConfig(MiniConfigReader config, string section) =>
        ParseAddext(config.Section(section).Select(pair => pair.Key + "=" + pair.Value));
    private static DerObjectIdentifier Oid(string name) =>
        Oids.TryGetValue(name, out var oid) ? oid : throw Unsupported(name);
    private static Asn1Encodable Build(string name, string body)
    {
        switch (name)
        {
            case "subjectAltName": return new GeneralNames(body.Split(',').Select(San).ToArray());
            case "basicConstraints": return Constraints(name, body);
            case "keyUsage": return new KeyUsage(body.Split(',').Aggregate(0, (all, token) => all | Usage(name, token)));
            case "extendedKeyUsage": return new ExtendedKeyUsage(body.Split(',').Select(token => Purpose(name, token)).ToArray());
            default: throw Unsupported(name);
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
    private static OpenSslException Unsupported(string name) =>
        new($"req: 不支持的扩展 \"{name}\"；支持：subjectAltName、basicConstraints、keyUsage、extendedKeyUsage。");
    private static OpenSslException BadValue(string name, string value) => new($"req: 不支持的扩展 {name} 取值 \"{value}\"。");
}