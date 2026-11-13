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
*文件名： Pkcs12Codec
*版本号： net10.0
*唯一标识：ddccfcf6-570b-4c49-a8e7-605522c09de1
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 14:49:41
*描述：PKCS#12 打包与拆包
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 14:49:41
*修改人： yswenli
*版本号： net10.0
*描述：新增 PKCS#12 打包与拆包
*
*****************************************************************************/
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
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

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