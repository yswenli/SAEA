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
*文件名： Crl2Pkcs7Command
*版本号： net10.0
*唯一标识：37ec0d9e-9007-489f-8a41-ea9b2d2f0eae
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 15:05:25
*描述：crl2pkcs7 把证书装入 certs-only PKCS#7
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 15:05:25
*修改人： yswenli
*版本号： net10.0
*描述：新增 crl2pkcs7 打包
*
*****************************************************************************/
namespace SAEA.Sockets.SslTlsTool.OpenSsl.Commands;

/// <summary>crl2pkcs7：把多张证书装进 certs-only PKCS#7。</summary>
internal static class Crl2Pkcs7Command
{
    private static readonly string[] Allowed = { "-nocrl", "-certfile", "-out", "-outform" };
    public static int Run(string[] args, bool interactive)
    {
        var reader = new OptionReader("crl2pkcs7", args, Allowed);
        if (!reader.Has("-nocrl")) throw new OpenSslException("crl2pkcs7: 不支持读取 CRL，请加 -nocrl。");
        if (!reader.Has("-certfile")) throw new OpenSslException("crl2pkcs7: 需要至少一个 -certfile。");
        var der = IsDer(reader); var certificates = reader.GetAll("-certfile").SelectMany(path => CertificateCodec.ReadAll(ReadFile(path), der: false)).ToList();
        if (interactive && der && !reader.Has("-out"))
            throw new OpenSslException("交互模式下二进制输出必须给 -out。");
        var data = Pkcs7Codec.CreateCertsOnly(certificates, der);
        if (reader.Has("-out")) File.WriteAllBytes(reader.GetRequired("-out"), data);
        else if (!der) Console.Out.Write(Encoding.ASCII.GetString(data));
        else { Console.Out.Flush(); using var stdout = Console.OpenStandardOutput(); stdout.Write(data, 0, data.Length); }
        return 0;
    }
    private static byte[] ReadFile(string path) => File.Exists(path) ? File.ReadAllBytes(path) : throw new OpenSslException($"error: 找不到文件 \"{path}\"。");
    private static bool IsDer(OptionReader reader)
    {
        var value = reader.Has("-outform") ? reader.GetRequired("-outform") : "PEM";
        if (value != "PEM" && value != "DER") throw new OpenSslException($"error: 参数 \"-outform\" 只支持 PEM 或 DER，实际为 \"{value}\"。");
        return value == "DER";
    }
}