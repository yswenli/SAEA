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
*文件名： Pkcs8Command
*版本号： net10.0
*唯一标识：1eb053e9-01b5-4d99-8648-c853ed5fdef3
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 14:04:10
*描述：pkcs8 子命令的私钥格式转换
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 14:04:10
*修改人： yswenli
*版本号： net10.0
*描述：新增 pkcs8 子命令的私钥格式转换
*
*****************************************************************************/
using Org.BouncyCastle.Asn1;

namespace SAEA.Sockets.SslTlsTool.OpenSsl.Commands;

/// <summary>pkcs8：在 PKCS#1 与 PKCS#8 之间转换私钥，并按需加解密。</summary>
internal static class Pkcs8Command
{
    private static readonly string[] Allowed = { "-topk8", "-in", "-out", "-inform", "-outform", "-nocrypt", "-v1", "-v2", "-passin", "-passout" };

    /// <summary>执行一次 pkcs8 调用。</summary>
    /// <param name="args">子命令之后的参数</param>
    /// <param name="interactive">是否来自交互模式</param>
    /// <returns>退出码，0 成功、1 失败</returns>
    public static int Run(string[] args, bool interactive)
    {
        var reader = new OptionReader("pkcs8", args, Allowed);
        var derIn = IsDer(reader, "-inform");
        var derOut = IsDer(reader, "-outform");
        if (interactive && derOut && !reader.Has("-out"))
            throw new OpenSslException("交互模式下二进制输出必须给 -out。");
        var data = Read(reader, derIn);
        // 只有输入确实加密时才提示口令，避免对明文私钥空转一次交互。
        var passIn = reader.Has("-passin") || IsEncrypted(data, derIn)
            ? PasswordResolver.Resolve(reader.Get("-passin"), reader.Get("-in") ?? "stdin", false, interactive)
            : null;
        var key = KeyCodec.ReadPrivateKey(data, passIn, derIn);
        if (!reader.Has("-topk8"))
        {
            if (reader.Has("-passout"))
                throw new OpenSslException("pkcs8: 非 -topk8 模式不支持 -passout。");
            if (reader.Has("-v1") || reader.Has("-v2"))
                throw new OpenSslException("pkcs8: -v1/-v2 只与 -topk8 一起使用。");
            var pkcs1 = KeyCodec.WritePkcs1(key);
            Write(reader, derOut, derOut ? pkcs1 : Encoding.ASCII.GetBytes(PemCodec.Encode("RSA PRIVATE KEY", pkcs1)));
            return 0;
        }
        if (reader.Has("-nocrypt") && (reader.Has("-passout") || reader.Has("-v1") || reader.Has("-v2")))
            throw new OpenSslException("pkcs8: -nocrypt 不能与 -passout/-v1/-v2 同时使用。");
        if (reader.Has("-v1") && reader.Has("-v2"))
            throw new OpenSslException("pkcs8: -v1 与 -v2 不能同时使用。");
        if (reader.Has("-v1") && reader.GetRequired("-v1") != "PBE-SHA1-3DES")
            throw new OpenSslException("pkcs8: -v1 只支持 PBE-SHA1-3DES。");
        var cipher = reader.Has("-v1") ? "pbe-sha1-3des" : reader.Has("-v2") ? reader.GetRequired("-v2") : "aes-256-cbc";
        string? passOut = null;
        if (!reader.Has("-nocrypt"))
        {
            passOut = PasswordResolver.Resolve(reader.Get("-passout"), reader.Get("-out") ?? "stdout", true, interactive);
            if (string.IsNullOrEmpty(passOut))
                throw new OpenSslException("pkcs8: PKCS#8 加密口令不能为空。");
        }
        Write(reader, derOut, KeyCodec.WritePkcs8(key, passOut, cipher, derOut));
        return 0;
    }

    /// <summary>判断输入是否已是加密私钥。</summary>
    /// <param name="data">输入字节</param>
    /// <param name="der">true 表示 DER，false 表示 PEM</param>
    /// <returns>已加密时返回 true</returns>
    // 加密 PKCS#8 的 DER 顶层 SEQUENCE 只有算法与密文两段；PEM 则带 ENCRYPTED 头。
    internal static bool IsEncrypted(byte[] data, bool der) => der
        ? Asn1Sequence.GetInstance(Asn1Object.FromByteArray(data)).Count == 2
        : Encoding.ASCII.GetString(data).Contains("ENCRYPTED", StringComparison.Ordinal);

    /// <summary>按 -in 或 stdin 读取输入。</summary>
    /// <param name="reader">已解析的参数</param>
    /// <param name="der">true 表示按二进制读取 stdin</param>
    /// <returns>输入字节</returns>
    internal static byte[] Read(OptionReader reader, bool der)
    {
        var path = reader.Get("-in");
        if (path is not null) return File.Exists(path) ? File.ReadAllBytes(path) : throw new OpenSslException($"error: 找不到文件 \"{path}\"。");
        if (!der) return Encoding.UTF8.GetBytes(Console.In.ReadToEnd());
        using var stdin = Console.OpenStandardInput();
        using var buffer = new MemoryStream();
        stdin.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <summary>按 -out 或 stdout 写出结果。</summary>
    /// <param name="reader">已解析的参数</param>
    /// <param name="der">true 表示写原始二进制</param>
    /// <param name="payload">输出字节</param>
    internal static void Write(OptionReader reader, bool der, byte[] payload)
    {
        var path = reader.Get("-out");
        if (path is not null) { File.WriteAllBytes(path, payload); return; }
        if (!der) { Console.Out.Write(Encoding.ASCII.GetString(payload)); return; }
        Console.Out.Flush(); // 原始二进制写 stdout 前清空文本缓冲，避免诊断项混入。
        using var stdout = Console.OpenStandardOutput();
        stdout.Write(payload, 0, payload.Length);
        stdout.Flush();
    }

    /// <summary>解析 -inform/-outform 的取值。</summary>
    /// <param name="reader">已解析的参数</param>
    /// <param name="option">参数名</param>
    /// <returns>DER 时返回 true，PEM 或未给时返回 false</returns>
    internal static bool IsDer(OptionReader reader, string option)
    {
        if (!reader.Has(option)) return false;
        var value = reader.GetRequired(option);
        if (value == "PEM") return false;
        if (value == "DER") return true;
        throw new OpenSslException($"error: 参数 \"{option}\" 只支持 PEM 或 DER，实际为 \"{value}\"。");
    }
}