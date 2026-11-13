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
*文件名： PkeyCommand
*版本号： net10.0
*唯一标识：84212625-10bc-4db2-843c-1d0620beb613
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 14:04:10
*描述：pkey 子命令的私钥输出
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 14:04:10
*修改人： yswenli
*版本号： net10.0
*描述：新增 pkey 子命令的私钥输出
*
*****************************************************************************/
using Org.BouncyCastle.Crypto.Parameters;

namespace SAEA.Sockets.SslTlsTool.OpenSsl.Commands;

/// <summary>pkey：读取私钥，输出 PKCS#8、PKCS#1 或公钥，并可按需加密。</summary>
internal static class PkeyCommand
{
    private static readonly string[] Allowed =
    {
        "-in", "-out", "-inform", "-outform", "-pubout", "-traditional",
        "-aes-128-cbc", "-aes-192-cbc", "-aes-256-cbc", "-des3", "-passin", "-passout",
    };

    /// <summary>执行一次 pkey 调用。</summary>
    /// <param name="args">子命令之后的参数</param>
    /// <param name="interactive">是否来自交互模式</param>
    /// <returns>退出码，0 成功、1 失败</returns>
    public static int Run(string[] args, bool interactive)
    {
        var reader = new OptionReader("pkey", args, Allowed);
        var derIn = Pkcs8Command.IsDer(reader, "-inform");
        var derOut = Pkcs8Command.IsDer(reader, "-outform");
        if (interactive && derOut && !reader.Has("-out"))
            throw new OpenSslException("交互模式下二进制输出必须给 -out。");
        var ciphers = new[] { "-aes-128-cbc", "-aes-192-cbc", "-aes-256-cbc", "-des3" }.Where(reader.Has).ToArray();
        if (ciphers.Length > 1)
            throw new OpenSslException("pkey: 加密算法只能选一个。");
        var cipher = ciphers.FirstOrDefault()?.Substring(1);
        if (reader.Has("-traditional") && cipher is not null)
            throw new OpenSslException("pkey: -traditional 不能与加密算法同时使用。");
        if (reader.Has("-pubout") && (reader.Has("-traditional") || cipher is not null))
            throw new OpenSslException("pkey: -pubout 不能与 -traditional 或加密算法同时使用。");
        // 没有 cipher 时 -passout 无处生效，报错而不是静默输出明文，避免误以为已加密。
        if (reader.Has("-passout") && cipher is null)
            throw new OpenSslException("pkey: 未指定加密算法时不能给 -passout；需要加密请加 -aes-256-cbc 等。");
        var data = Pkcs8Command.Read(reader, derIn);
        var passIn = reader.Has("-passin") || Pkcs8Command.IsEncrypted(data, derIn)
            ? PasswordResolver.Resolve(reader.Get("-passin"), reader.Get("-in") ?? "stdin", false, interactive)
            : null;
        var key = KeyCodec.ReadPrivateKey(data, passIn, derIn);
        if (reader.Has("-pubout"))
        {
            var pub = key is RsaPrivateCrtKeyParameters rsa
                ? new RsaKeyParameters(false, rsa.Modulus, rsa.PublicExponent)
                : throw new OpenSslException("error: 只有 RSA 私钥可以输出公钥。");
            Pkcs8Command.Write(reader, derOut, KeyCodec.WritePublicKey(pub, derOut));
            return 0;
        }
        if (reader.Has("-traditional"))
        {
            var pkcs1 = KeyCodec.WritePkcs1(key);
            Pkcs8Command.Write(reader, derOut, derOut ? pkcs1 : Encoding.ASCII.GetBytes(PemCodec.Encode("RSA PRIVATE KEY", pkcs1)));
            return 0;
        }
        string? passOut = null;
        if (cipher is not null)
        {
            passOut = PasswordResolver.Resolve(reader.Get("-passout"), reader.Get("-out") ?? "stdout", true, interactive);
            if (string.IsNullOrEmpty(passOut))
                throw new OpenSslException("pkey: PKCS#8 加密口令不能为空。");
        }
        Pkcs8Command.Write(reader, derOut, KeyCodec.WritePkcs8(key, passOut, cipher, derOut));
        return 0;
    }
}