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
*文件名： OpenSslCommand
*版本号： net10.0
*唯一标识：7a788036-a75f-4355-b8e6-7ff0f12634ec
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 13:40:21
*描述：openssl 兼容子命令的分派入口
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 13:54:11
*修改人： yswenli
*版本号： net10.0
*描述：x509 接到 X509Command.Run
*
*****************************************************************************/
using SAEA.Sockets.SslTlsTool.OpenSsl.Commands;

namespace SAEA.Sockets.SslTlsTool.OpenSsl;

/// <summary>openssl 兼容子命令的分派入口。</summary>
internal static class OpenSslCommand
{
    private static readonly HashSet<string> Commands = new(StringComparer.Ordinal)
    {
        "req", "x509", "pkcs12", "pkcs8", "pkey", "crl2pkcs7", "pkcs7", "version",
    };

    /// <summary>首 token 是否交给兼容层：空串或 - 开头的选项仍走遗留分支。</summary>
    /// <param name="token">首个参数</param>
    /// <returns>非 - 开头且非空时返回 true</returns>
    public static bool IsCompat(string token) => token.Length > 0 && token[0] != '-';

    /// <summary>首 token 是否为受支持的子命令名。</summary>
    /// <param name="token">首个参数</param>
    /// <returns>命中子命令清单时返回 true</returns>
    public static bool IsCommand(string token) => Commands.Contains(token);

    /// <summary>执行一次兼容子命令，错误写 stderr。</summary>
    /// <param name="args">含子命令名的完整参数</param>
    /// <param name="interactive">是否来自交互模式</param>
    /// <returns>退出码，0 成功、1 任何失败</returns>
    public static int Run(string[] args, bool interactive)
    {
        var command = args[0];
        try
        {
            if (!IsCommand(command))
                throw new OpenSslException($"error: 不支持的子命令 \"{command}\"；支持：{OpenSslUsage.CommandList()}。");
            var tail = args.Length > 1 ? args[1..] : Array.Empty<string>();
            foreach (var token in tail)
            {
                if (!string.Equals(token, "-help", StringComparison.Ordinal))
                    continue;
                // -help 短路：不读文件、不校验其它参数，帮助只写 stdout。
                string text;
                try { text = OpenSslUsage.For(command); }
                catch (OpenSslException) { text = OpenSslUsage.Pending(command); }
                Console.Out.WriteLine(text);
                return 0;
            }
            if (command == "version")
            {
                // -help 已在上面短路；version 只认 -help，其余参数显式报错，避免脚本以为参数生效。
                if (args.Length > 1)
                    throw new OpenSslException("version: 不接受参数；只支持 -help。");
                var version = typeof(OpenSslCommand).Assembly.GetName().Version?.ToString() ?? "dev";
                Console.Out.WriteLine($"SAEA.Sockets.SslTlsTool {version}（openssl 兼容子集）");
                return 0;
            }
            // 过渡实现：Task 3 起逐个替换为 Commands.*Command.Run(tail, interactive)。
            switch (command)
            {
                case "x509":
                    return X509Command.Run(tail, interactive);
                default:
                    throw new OpenSslException($"error: {command} 尚未实现。");
            }
        }
        catch (OpenSslException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("error: " + ex.Message);
            return 1;
        }
    }
}