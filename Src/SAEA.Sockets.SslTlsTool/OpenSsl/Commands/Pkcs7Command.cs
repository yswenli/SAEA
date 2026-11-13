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
*文件名： Pkcs7Command
*版本号： net10.0
*唯一标识：9b2b8c55-3a17-45e3-94cc-549648345914
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 15:05:02
*描述：pkcs7 读取 certs-only PKCS#7
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 15:05:02
*修改人： yswenli
*版本号： net10.0
*描述：新增 pkcs7 读取与重写
*
*****************************************************************************/
namespace SAEA.Sockets.SslTlsTool.OpenSsl.Commands;

/// <summary>pkcs7：读取 certs-only PKCS#7，按 -print_certs 拆证书或按 -outform 重写。</summary>
internal static class Pkcs7Command
{
    private static readonly string[] Allowed = { "-in", "-inform", "-print_certs", "-out", "-outform", "-noout" };
    public static int Run(string[] args, bool interactive)
    {
        var reader = new OptionReader("pkcs7", args, Allowed);
        var derIn = IsDer(reader, "-inform"); var derOut = IsDer(reader, "-outform");
        if (reader.Has("-print_certs") && derOut)
            throw new OpenSslException("pkcs7: -print_certs 只输出 PEM 文本，不能与 -outform DER 一起用。");
        if (interactive && derOut && !reader.Has("-out"))
            throw new OpenSslException("交互模式下二进制输出必须给 -out。");
        var certificates = Pkcs7Codec.Read(ReadInput(reader), derIn);
        var data = reader.Has("-print_certs")
            ? Encoding.ASCII.GetBytes(string.Concat(certificates.Select(cert => PemCodec.Encode("CERTIFICATE", cert.GetEncoded()))))
            : Pkcs7Codec.CreateCertsOnly(certificates, derOut);
        if (reader.Has("-out")) File.WriteAllBytes(reader.GetRequired("-out"), data);
        else if (derOut) { Console.Out.Flush(); using var stdout = Console.OpenStandardOutput(); stdout.Write(data, 0, data.Length); }
        else Console.Out.Write(Encoding.ASCII.GetString(data));
        return 0;
    }
    private static byte[] ReadInput(OptionReader reader)
    {
        if (reader.Has("-in")) { var path = reader.GetRequired("-in"); return File.Exists(path) ? File.ReadAllBytes(path) : throw new OpenSslException($"error: 找不到文件 \"{path}\"。"); }
        using var stdin = Console.OpenStandardInput(); using var buffer = new MemoryStream(); stdin.CopyTo(buffer); return buffer.ToArray();
    }
    private static bool IsDer(OptionReader reader, string option)
    {
        var value = reader.Has(option) ? reader.GetRequired(option) : "PEM";
        if (value != "PEM" && value != "DER") throw new OpenSslException($"error: 参数 \"{option}\" 只支持 PEM 或 DER，实际为 \"{value}\"。");
        return value == "DER";
    }
}