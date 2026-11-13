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
*文件名： ReqCommand
*版本号： net10.0
*唯一标识：722108eb-63a2-48bb-9d39-49ba75356af8
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 14:21:12
*描述：req 子命令的 CSR 生成与自签
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 14:21:12
*修改人： yswenli
*版本号： net10.0
*描述：新增 req 子命令的 CSR 生成与自签
*
*****************************************************************************/
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace SAEA.Sockets.SslTlsTool.OpenSsl.Commands;

/// <summary>req：新建私钥并输出 CSR，或用 -x509 直接自签成证书。</summary>
internal static class ReqCommand
{
    private static readonly string[] Allowed =
    {
        "-new", "-x509", "-newkey", "-key", "-keyout", "-nodes", "-noenc",
        "-days", "-subj", "-addext", "-sha256", "-out", "-passout",
    };

    public static int Run(string[] args, bool interactive)
    {
        var reader = new OptionReader("req", args, Allowed);
        var selfSign = reader.Has("-x509");
        // -nodes 与 -noenc 是 openssl 的同义开关，下文统一按 noEncrypt 判断。
        var noEncrypt = reader.Has("-nodes") || reader.Has("-noenc");
        if (!reader.Has("-new") && !selfSign)
            throw new OpenSslException("req: -new 与 -x509 至少给一个。");
        if (reader.Has("-new") && selfSign)
            throw new OpenSslException("req: -new 与 -x509 不能同时使用；openssl 允许，本工具为简化只支持二选一。");
        if (noEncrypt && reader.Has("-passout"))
            throw new OpenSslException("req: -nodes/-noenc 与 -passout 不能同时使用。");
        if (reader.Has("-newkey") && reader.Has("-key"))
            throw new OpenSslException("req: -newkey 与 -key 不能同时使用。");
        if (reader.Has("-newkey") && !reader.Has("-keyout"))
            throw new OpenSslException("req: -newkey 必须配 -keyout。");
        if (!reader.Has("-newkey") && reader.Has("-keyout"))
            Console.Error.WriteLine("req: 未给 -newkey，忽略 -keyout，不覆盖既有私钥。");
        if (!reader.Has("-subj")) throw new OpenSslException("req: 需要 -subj；交互式 DN 输入未支持。");
        var subject = SubjectNameParser.Parse(reader.GetRequired("-subj"));
        var extensions = ExtensionSpec.ParseAddext(reader.GetAll("-addext"));
        var key = reader.Has("-newkey") ? Generate(reader.GetRequired("-newkey")) : ReadKey(reader.GetRequired("-key"));
        if (reader.Has("-newkey"))
        {
            string? passOut = null;
            if (reader.Has("-passout"))
            {
                passOut = PasswordResolver.Resolve(reader.Get("-passout"), reader.GetRequired("-keyout"), true, interactive);
                if (string.IsNullOrEmpty(passOut)) throw new OpenSslException("req: PKCS#8 加密口令不能为空。");
            }
            // openssl 在两者都缺时会交互要口令；本工具不做隐式交互，改为明文写出并提示，避免非交互脚本挂起。
            else if (!noEncrypt)
                Console.Error.WriteLine("req: 未给 -nodes/-noenc 也未给 -passout，私钥按明文写出；需要加密请给 -passout。");
            File.WriteAllBytes(reader.GetRequired("-keyout"), KeyCodec.WritePkcs8(key, passOut, "aes-256-cbc", false));
        }
        // PemCodec.Encode 返回文本，这里统一转成 ASCII 字节再交给按字节写出的 WriteOutput。
        var payload = Encoding.ASCII.GetBytes(selfSign
            ? PemCodec.Encode("CERTIFICATE", SignSelf(subject, key, extensions, reader.GetInt("-days", 30)).GetEncoded())
            : PemCodec.Encode("CERTIFICATE REQUEST", CsrCodec.Create(key, subject, extensions, "SHA256").GetEncoded()));
        WriteOutput(reader.Get("-out"), payload);
        return 0;
    }

    private static AsymmetricKeyParameter Generate(string spec)
    {
        if (!spec.StartsWith("rsa:", StringComparison.Ordinal)
            || !int.TryParse(spec.Substring(4), out var bits)
            || (bits != 1024 && bits != 2048 && bits != 3072 && bits != 4096))
            throw new OpenSslException("req: -newkey 只支持 rsa:1024|2048|3072|4096。");
        var generator = new RsaKeyPairGenerator();
        generator.Init(new KeyGenerationParameters(new SecureRandom(), bits));
        return generator.GenerateKeyPair().Private;
    }

    private static AsymmetricKeyParameter ReadKey(string path) => File.Exists(path)
        // req 无 -passin，只接受明文私钥。
        ? KeyCodec.ReadPrivateKey(File.ReadAllBytes(path), null, false)
        : throw new OpenSslException($"error: 找不到文件 \"{path}\"。");

    private static X509Certificate SignSelf(X509Name subject, AsymmetricKeyParameter key, IList<Extension> extensions, int days)
    {
        if (days <= 0) throw new OpenSslException("req: -days 必须是正整数。");
        var rsa = key as RsaPrivateCrtKeyParameters ?? throw new OpenSslException("req: 自签只支持 RSA 私钥。");
        var generator = new X509V3CertificateGenerator();
        generator.SetSerialNumber(new BigInteger(64, new SecureRandom()));
        // 自签的 issuer 与 subject 一致，没有 CA 参与。
        generator.SetIssuerDN(subject);
        generator.SetSubjectDN(subject);
        generator.SetNotBefore(DateTime.UtcNow.AddMinutes(-5));
        generator.SetNotAfter(DateTime.UtcNow.AddDays(days));
        generator.SetPublicKey(new RsaKeyParameters(false, rsa.Modulus, rsa.PublicExponent));
        foreach (var extension in extensions) generator.AddExtension(extension.ExtnID, extension.Critical.IsTrue, extension.ExtnValue.GetOctets());
        return generator.Generate(new Asn1SignatureFactory("SHA256WITHRSA", key));
    }

    private static void WriteOutput(string? path, byte[] payload)
    {
        if (path is null) { Console.Out.Write(Encoding.ASCII.GetString(payload)); return; }
        File.WriteAllBytes(path, payload);
    }
}