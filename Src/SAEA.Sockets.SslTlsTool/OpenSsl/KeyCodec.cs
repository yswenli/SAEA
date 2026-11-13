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
*文件名： KeyCodec
*版本号： net10.0
*唯一标识：ed89b827-2ca6-43b7-a3f4-a2b287ff1d45
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 14:04:10
*描述：私钥的 PEM/DER 读写与加解密
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 14:04:10
*修改人： yswenli
*版本号： net10.0
*描述：新增私钥的 PEM/DER 读写与加解密
*
*****************************************************************************/
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace SAEA.Sockets.SslTlsTool.OpenSsl;

/// <summary>私钥的 PEM/DER 读写、PKCS#8 与 PKCS#1 转换及加解密。</summary>
internal static class KeyCodec
{
    private static readonly SecureRandom Random = new();
    /// <summary>读取私钥，支持明文与加密的 PKCS#8、PKCS#1。</summary>
    /// <param name="data">输入字节</param>
    /// <param name="password">读取口令，明文私钥传 null</param>
    /// <param name="der">true 表示 DER，false 表示 PEM</param>
    /// <returns>私钥参数</returns>
    public static AsymmetricKeyParameter ReadPrivateKey(byte[] data, string? password, bool der) =>
        der ? ReadDer(data, password) : ReadPem(Encoding.ASCII.GetString(data), password);
    /// <summary>输出 PKCS#8，password 为 null 时输出明文。</summary>
    /// <param name="key">私钥参数</param>
    /// <param name="password">写出口令，明文传 null</param>
    /// <param name="cipher">加密算法名，明文时忽略</param>
    /// <param name="der">true 表示 DER，false 表示 PEM</param>
    /// <returns>输出字节</returns>
    public static byte[] WritePkcs8(AsymmetricKeyParameter key, string? password, string? cipher, bool der)
    {
        var info = PrivateKeyInfoFactory.CreatePrivateKeyInfo(key);
        return Encode(password is null ? info.GetEncoded() : Encrypt(key, info, password, cipher ?? "aes-256-cbc"),
            der, password is null ? "PRIVATE KEY" : "ENCRYPTED PRIVATE KEY");
    }
    /// <summary>输出 PKCS#1 DER。</summary>
    /// <param name="key">私钥参数</param>
    /// <returns>PKCS#1 DER 字节</returns>
    public static byte[] WritePkcs1(AsymmetricKeyParameter key) => key is RsaPrivateCrtKeyParameters rsa
        ? new RsaPrivateKeyStructure(rsa.Modulus, rsa.PublicExponent, rsa.Exponent, rsa.P, rsa.Q, rsa.DP, rsa.DQ, rsa.QInv).GetEncoded()
        : throw new OpenSslException("error: 只有 RSA 私钥可以输出 PKCS#1。");
    /// <summary>输出公钥。</summary>
    /// <param name="key">公钥参数</param>
    /// <param name="der">true 表示 DER，false 表示 PEM</param>
    /// <returns>输出字节</returns>
    public static byte[] WritePublicKey(AsymmetricKeyParameter key, bool der) =>
        Encode(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(key).GetEncoded(), der, "PUBLIC KEY");
    private static byte[] Encode(byte[] data, bool der, string label) =>
        der ? data : Encoding.ASCII.GetBytes(PemCodec.Encode(label, data));
    // PKCS#8 明文 3 段、加密 PKCS#8 2 段、PKCS#1 为 9 个 INTEGER，按段数区分。
    private static AsymmetricKeyParameter ReadDer(byte[] data, string? password)
    {
        var sequence = Asn1Sequence.GetInstance(Asn1Object.FromByteArray(data));
        if (sequence.Count == 2) return PrivateKeyFactory.CreateKey(Decrypt(data, password));
        if (sequence.Count == 3) return PrivateKeyFactory.CreateKey(PrivateKeyInfo.GetInstance(sequence));
        var rsa = RsaPrivateKeyStructure.GetInstance(sequence);
        return new RsaPrivateCrtKeyParameters(rsa.Modulus, rsa.PublicExponent, rsa.PrivateExponent,
            rsa.Prime1, rsa.Prime2, rsa.Exponent1, rsa.Exponent2, rsa.Coefficient);
    }
    private static AsymmetricKeyParameter ReadPem(string text, string? password)
    {
        // 旧式 PEM 的 DEK-Info 头会破坏 base64，只能交给 PemReader。
        if (text.Contains("Proc-Type: 4,ENCRYPTED", StringComparison.Ordinal)) return ReadLegacy(text, password);
        foreach (var block in PemCodec.ParseAll(text))
            if (block.Key is "PRIVATE KEY" or "ENCRYPTED PRIVATE KEY" or "RSA PRIVATE KEY") return ReadDer(block.Value, password);
        throw new OpenSslException("error: 输入中没有私钥块。");
    }
    private static AsymmetricKeyParameter ReadLegacy(string text, string? password)
    {
        try
        {
            var value = new PemReader(new StringReader(text), new FixedPassword(password ?? string.Empty)).ReadObject();
            return value is AsymmetricCipherKeyPair pair ? pair.Private : value as AsymmetricKeyParameter
                ?? throw new OpenSslException("error: 无法解析私钥。");
        }
        // 旧式加密 PEM 的口令错误由填充校验暴露，统一成与 PKCS#8 相同的提示。
        catch (Exception ex) when (ex is not OpenSslException)
        {
            throw new OpenSslException("error: 口令错误或文件损坏。");
        }
    }
    private static PrivateKeyInfo Decrypt(byte[] data, string? password)
    {
        if (password is null) throw new OpenSslException("error: 私钥已加密，需要口令。");
        try
        {
            var info = EncryptedPrivateKeyInfo.GetInstance(Asn1Object.FromByteArray(data));
            return PrivateKeyInfoFactory.CreatePrivateKeyInfo(password.ToCharArray(), info);
        }
        // BC 对不同算法分别抛 CryptoException / PkcsException，统一成口令错误，避免成为口令探测手段。
        catch (Exception ex) when (ex is not OpenSslException)
        {
            throw new OpenSslException("error: 口令错误或文件损坏。");
        }
    }
    private static byte[] Encrypt(AsymmetricKeyParameter key, PrivateKeyInfo info, string password, string cipher)
    {
        // PBE-SHA1-3DES 只对应 pkcs8 -v1；-v2 des3 与 pkey -des3 走下面的 PBES2 分支。
        if (cipher == "pbe-sha1-3des")
            return new Pkcs8Generator(key, Pkcs8Generator.PbeSha1_3DES) { Password = password.ToCharArray(), SecureRandom = Random }.Generate().Content;
        var salt = new byte[16]; Random.NextBytes(salt);
        // BC 的 PBES2 参数工厂只接受 AES 系列，DES-EDE3-CBC 需要自行拼装参数后再走同一套口令派生。
        if (cipher == "des3") return EncryptPbes2Des3(info, password, salt);
        // PBKDF2 的 PRF 固定 HMAC-SHA256（OID 1.2.840.113549.2.9）。
        return EncryptedPrivateKeyInfoFactory.CreateEncryptedPrivateKeyInfo(
            CipherOid(cipher), PkcsObjectIdentifiers.IdHmacWithSha256, password.ToCharArray(), salt, 2048, Random, info).GetEncoded();
    }
    private static byte[] EncryptPbes2Des3(PrivateKeyInfo info, string password, byte[] salt)
    {
        var iv = new byte[8]; Random.NextBytes(iv);
        var parameters = new PbeS2Parameters(
            new KeyDerivationFunc(PkcsObjectIdentifiers.IdPbkdf2,
                new Pbkdf2Params(salt, 2048, new AlgorithmIdentifier(PkcsObjectIdentifiers.IdHmacWithSha256, DerNull.Instance))),
            new EncryptionScheme(PkcsObjectIdentifiers.DesEde3Cbc, new DerOctetString(iv)));
        var cipher = CipherUtilities.GetCipher(PkcsObjectIdentifiers.DesEde3Cbc);
        cipher.Init(true, PbeUtilities.GenerateCipherParameters(PkcsObjectIdentifiers.IdPbeS2, password.ToCharArray(), parameters));
        return new EncryptedPrivateKeyInfo(
            new AlgorithmIdentifier(PkcsObjectIdentifiers.IdPbeS2, parameters), cipher.DoFinal(info.GetEncoded())).GetEncoded();
    }
    private static DerObjectIdentifier CipherOid(string cipher) => cipher switch
    {
        "aes-128-cbc" => NistObjectIdentifiers.IdAes128Cbc, "aes-192-cbc" => NistObjectIdentifiers.IdAes192Cbc,
        "aes-256-cbc" => NistObjectIdentifiers.IdAes256Cbc, "des3" => PkcsObjectIdentifiers.DesEde3Cbc,
        _ => throw new OpenSslException($"error: 不支持的加密算法 \"{cipher}\"；支持 aes-128-cbc、aes-192-cbc、aes-256-cbc、des3。"),
    };
    private sealed class FixedPassword(string password) : IPasswordFinder
    {
        public char[] GetPassword() => password.ToCharArray();
    }
}