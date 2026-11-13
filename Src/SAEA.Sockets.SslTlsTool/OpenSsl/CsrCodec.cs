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
*文件名： CsrCodec
*版本号： net10.0
*唯一标识：42b1c86c-8c74-4dd9-abea-3fec6b6b9c6d
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 14:21:12
*描述：CSR 的生成与读取，以及用 CA 签发证书
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 14:21:12
*修改人： yswenli
*版本号： net10.0
*描述：新增 CSR 的生成与读取，以及用 CA 签发证书
*
*****************************************************************************/
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.X509;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace SAEA.Sockets.SslTlsTool.OpenSsl;

/// <summary>CSR 生成与读取，以及用 CA 私钥签发证书。</summary>
internal static class CsrCodec
{
    public static Pkcs10CertificationRequest Create(AsymmetricKeyParameter key, X509Name subject, IList<Extension>? extensions, string digest)
    {
        Asn1Set attributes = DerSet.Empty;
        if (extensions != null && extensions.Count > 0)
        {
            var generator = new X509ExtensionsGenerator();
            foreach (var extension in extensions) generator.AddExtension(extension.ExtnID, extension.Critical.IsTrue, extension.ExtnValue.GetOctets());
            attributes = new DerSet(new AttributeX509(PkcsObjectIdentifiers.Pkcs9AtExtensionRequest, new DerSet(generator.Generate())));
        }
        // BC 的 4 参重载要求公钥，直接传私钥会抛 expected public key。
        return new Pkcs10CertificationRequest(new Asn1SignatureFactory(SignatureAlgorithm(digest), key), subject, PublicKey(key), attributes);
    }
    public static Pkcs10CertificationRequest Read(byte[] data, bool der) => der ? new Pkcs10CertificationRequest(data)
        : new Pkcs10CertificationRequest(PemCodec.DecodeFirst(Encoding.ASCII.GetString(data), "CERTIFICATE REQUEST", "NEW CERTIFICATE REQUEST"));
    public static X509Certificate Sign(Pkcs10CertificationRequest csr, AsymmetricKeyParameter caKey, X509Certificate caCert, BigInteger serial, DateTime notBefore, DateTime notAfter, IList<Extension> extensions, string digest)
    {
        var generator = new X509V3CertificateGenerator();
        generator.SetSerialNumber(serial);
        generator.SetIssuerDN(caCert.SubjectDN);
        generator.SetSubjectDN(CertificationRequestInfo.GetInstance(Asn1Sequence.GetInstance(csr.ToAsn1Object())[0]).Subject);
        generator.SetNotBefore(notBefore);
        generator.SetNotAfter(notAfter);
        generator.SetPublicKey(csr.GetPublicKey());
        foreach (var extension in extensions) generator.AddExtension(extension.ExtnID, extension.Critical.IsTrue, extension.ExtnValue.GetOctets());
        return generator.Generate(new Asn1SignatureFactory(SignatureAlgorithm(digest), caKey));
    }
    private static AsymmetricKeyParameter PublicKey(AsymmetricKeyParameter key) =>
        key is RsaPrivateCrtKeyParameters rsa ? new RsaKeyParameters(false, rsa.Modulus, rsa.PublicExponent) : key;
    private static string SignatureAlgorithm(string digest) =>
        digest.Replace("-", string.Empty).ToUpperInvariant() is "SHA256" or "SHA256WITHRSA" ? "SHA256WITHRSA"
            : throw new OpenSslException($"不支持的摘要 \"{digest}\"；只支持 SHA-256。");
}