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
*文件名： Pkcs7Codec
*版本号： net10.0
*唯一标识：1dd90ff5-06b3-4863-8399-847c57eaa177
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 15:05:02
*描述：certs-only PKCS#7 的 PEM/DER 编解码
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 15:05:02
*修改人： yswenli
*版本号： net10.0
*描述：新增 certs-only PKCS#7 的 PEM/DER 编解码
*
*****************************************************************************/
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