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
*文件名： Pkcs12Command
*版本号： net10.0
*唯一标识：18fa0dd8-8462-42e5-8509-35a83239df5a
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 14:50:45
*描述：pkcs12 子命令的导出与读取
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 14:50:45
*修改人： yswenli
*版本号： net10.0
*描述：新增 pkcs12 导出与读取
*
*****************************************************************************/
namespace SAEA.Sockets.SslTlsTool.OpenSsl.Commands;

/// <summary>pkcs12：导出 PFX，或将 PFX 拆成私钥与证书 PEM。</summary>
internal static class Pkcs12Command
{
    private static readonly string[] Allowed =
        { "-export", "-in", "-inkey", "-out", "-name", "-passin", "-passout", "-nodes", "-nocerts", "-nokeys", "-clcerts", "-cacerts", "-certfile", "-legacy", "-nomac", "-info" };
    public static int Run(string[] args, bool interactive)
    {
        var reader = new OptionReader("pkcs12", args, Allowed);
        return reader.Has("-export") ? Export(reader, interactive) : Read(reader, interactive);
    }
    private static int Export(OptionReader reader, bool interactive)
    {
        if (!reader.Has("-in") || !reader.Has("-inkey")) throw new OpenSslException("pkcs12: -export 需要 -in 与 -inkey。");
        if (interactive && !reader.Has("-out")) throw new OpenSslException("交互模式下二进制输出必须给 -out。");
        foreach (var option in new[] { "-nocerts", "-nokeys", "-clcerts", "-cacerts" }) if (reader.Has(option)) throw new OpenSslException($"pkcs12: 参数 \"{option}\" 只用于读取模式。");
        if (reader.Has("-passout") && (reader.Has("-nodes") || reader.Has("-nomac")))
            throw new OpenSslException("pkcs12: -nodes/-nomac 与 -passout 只能选一个。");
        var leaf = CertificateCodec.ReadFirst(ReadFile(reader.GetRequired("-in")), der: false);
        var keyPath = reader.GetRequired("-inkey");
        var keyPassword = reader.Has("-passin")
            ? PasswordResolver.Resolve(reader.Get("-passin"), keyPath, false, interactive) : null;
        var key = KeyCodec.ReadPrivateKey(ReadFile(keyPath), keyPassword, der: false);
        var legacy = reader.Has("-legacy"); var alias = reader.Get("-name");
        var codec = new Pkcs12Codec(legacy);
        codec.AddCertificate(leaf, alias, isLeaf: true);
        var certCount = 1;
        foreach (var path in reader.GetAll("-certfile"))
            foreach (var cert in CertificateCodec.ReadAll(ReadFile(path), der: false)) { codec.AddCertificate(cert, null, isLeaf: false); certCount++; }
        codec.AddPrivateKey(key, leaf, alias);
        // codec 只在 password 为 null 时不加密、不写 MAC，-nodes/-nomac 因此不解析 -passout。
        var password = reader.Has("-nodes") || reader.Has("-nomac") ? null
            : PasswordResolver.Resolve(reader.Get("-passout"), reader.Get("-out") ?? "stdout", true, interactive);
        // 给了 -passout 却没解析出口令（如 env: 变量缺失）时不能静默产出无保护 PFX。
        if (reader.Has("-passout") && password is null)
            throw new OpenSslException("pkcs12: -passout 未提供口令；不能生成无保护的 PFX。");
        var data = codec.Save(password);
        if (reader.Has("-info"))
            Console.Error.WriteLine($"PKCS#12 条目：1 个私钥、{certCount} 张证书；算法：{(legacy ? "3DES/RC2-40" : "AES-256-CBC")}，MAC：{(password is null ? "无" : legacy ? "HMAC-SHA-1" : "HMAC-SHA-256")}。");
        // 二进制写 stdout 前先清空文本缓冲，避免 -info 摘要混入。
        if (reader.Has("-out")) File.WriteAllBytes(reader.GetRequired("-out"), data);
        else { Console.Out.Flush(); using var stdout = Console.OpenStandardOutput(); stdout.Write(data, 0, data.Length); }
        return 0;
    }
    private static int Read(OptionReader reader, bool interactive)
    {
        foreach (var option in new[] { "-passout", "-legacy", "-name", "-certfile", "-nomac" }) if (reader.Has(option)) throw new OpenSslException($"pkcs12: 参数 \"{option}\" 只用于 -export。");
        var only = new[] { "-nocerts", "-clcerts", "-cacerts" }.Where(reader.Has).ToArray();
        if (only.Length > 1) throw new OpenSslException("pkcs12: -nocerts/-clcerts/-cacerts 只能选一个。");
        var input = reader.Has("-in") ? ReadFile(reader.GetRequired("-in")) : ReadStdin();
        var passIn = reader.Get("-passin");
        Pkcs12Bundle bundle;
        string? password;
        if (passIn is not null)
        {
            password = PasswordResolver.Resolve(passIn, reader.Get("-in") ?? "stdin", false, interactive);
            bundle = Load(input, password);
        }
        else
        {
            // -export -nodes 的产物不带 MAC，先按无口令装载；失败再索要口令，避免对无口令 PFX 空转一次提示。
            try { bundle = Pkcs12Codec.Load(input, null); password = null; }
            catch (Exception)
            {
                password = PasswordResolver.Resolve(null, reader.Get("-in") ?? "stdin", false, interactive);
                bundle = Load(input, password);
            }
        }
        var keyOnly = only.FirstOrDefault() == "-nocerts"; var leafOnly = only.FirstOrDefault() == "-clcerts"; var chainOnly = only.FirstOrDefault() == "-cacerts";
        var text = new StringBuilder();
        if (!leafOnly && !chainOnly && !reader.Has("-nokeys") && bundle.PrivateKey is { } privateKey)
            text.Append(Encoding.ASCII.GetString(KeyCodec.WritePkcs8(privateKey, reader.Has("-nodes") ? null : password, null, der: false)));
        if (!keyOnly && !chainOnly && bundle.Leaf is { } leaf) text.Append(PemCodec.Encode("CERTIFICATE", leaf.GetEncoded()));
        if (!keyOnly && !leafOnly)
            foreach (var cert in bundle.Chain.Where(cert => !cert.Equals(bundle.Leaf)))
                text.Append(PemCodec.Encode("CERTIFICATE", cert.GetEncoded()));
        if (reader.Has("-info"))
            Console.Error.WriteLine($"PKCS#12 条目：{(bundle.PrivateKey is null ? 0 : 1)} 个私钥、{bundle.Chain.Count} 张证书。");
        if (reader.Has("-out")) PemCodec.WriteFile(reader.GetRequired("-out"), text.ToString());
        else Console.Out.Write(text.ToString());
        return 0;
    }
    private static Pkcs12Bundle Load(byte[] input, string? password)
    {
        try { return Pkcs12Codec.Load(input, password); }
        catch (Exception) { throw new OpenSslException("error: 口令错误或文件损坏。"); }
    }
    private static byte[] ReadFile(string path) =>
        File.Exists(path) ? File.ReadAllBytes(path) : throw new OpenSslException($"error: 找不到文件 \"{path}\"。");
    private static byte[] ReadStdin()
    {
        using var stdin = Console.OpenStandardInput(); using var buffer = new MemoryStream();
        stdin.CopyTo(buffer);
        return buffer.ToArray();
    }
}