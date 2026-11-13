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
*文件名： PasswordResolver
*版本号： net10.0
*唯一标识：796803e1-4cf8-42e2-8c0e-f2a2b61faf17
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 14:04:10
*描述：-passin/-passout 口令来源解析
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 14:04:10
*修改人： yswenli
*版本号： net10.0
*描述：新增 -passin/-passout 口令来源解析
*
*****************************************************************************/
namespace SAEA.Sockets.SslTlsTool.OpenSsl;

/// <summary>解析 -passin/-passout 口令来源，必要时在交互终端提示输入。</summary>
internal static class PasswordResolver
{
    /// <summary>解析口令来源；未给来源时按交互提示处理。</summary>
    /// <param name="spec">口令来源字符串，如 pass:xxx</param>
    /// <param name="target">提示语中的路径</param>
    /// <param name="confirm">写出时是否需要二次确认</param>
    /// <param name="interactive">是否来自交互模式</param>
    /// <returns>口令；来源解析为空时返回 null</returns>
    public static string? Resolve(string? spec, string target, bool confirm, bool interactive)
    {
        if (spec is null) return Prompt(target, confirm);
        if (spec.StartsWith("pass:", StringComparison.Ordinal)) return spec.Substring(5);
        if (spec.StartsWith("env:", StringComparison.Ordinal)) return Environment.GetEnvironmentVariable(spec.Substring(4)) is { Length: > 0 } value ? value : null;
        if (spec.StartsWith("file:", StringComparison.Ordinal))
        {
            var path = spec.Substring(5);
            if (!File.Exists(path)) throw new OpenSslException($"error: 找不到口令文件 \"{path}\"。");
            // 只取首行并去掉行尾 CR/LF，其余字节原样。
            return File.ReadLines(path).FirstOrDefault() ?? throw new OpenSslException($"error: 口令文件为空 \"{path}\"。");
        }
        if (spec == "stdin") return interactive
            ? throw new OpenSslException("交互模式下不支持 -passin stdin；请用 pass:、env: 或 file:。")
            : Console.ReadLine();
        throw new OpenSslException($"error: 不支持的口令来源 \"{spec}\"；支持 pass:、env:、file:、stdin。");
    }
    private static string Prompt(string target, bool confirm)
    {
        if (Console.IsInputRedirected) throw new OpenSslException("需要口令：请用 -passin/-passout，或从交互终端运行。");
        var first = ReadLine("Enter pass phrase for " + target + ": ");
        if (!confirm) return first;
        return first == ReadLine("Verifying - Enter pass phrase for " + target + ": ") ? first : throw new OpenSslException("两次输入的口令不一致。");
    }
    private static string ReadLine(string prompt)
    {
        Console.Error.Write(prompt);
        var text = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) { Console.Error.WriteLine(); return text.ToString(); }
            if (key.Key == ConsoleKey.Backspace) { if (text.Length > 0) text.Length--; }
            else if (!char.IsControl(key.KeyChar)) text.Append(key.KeyChar);
        }
    }
}