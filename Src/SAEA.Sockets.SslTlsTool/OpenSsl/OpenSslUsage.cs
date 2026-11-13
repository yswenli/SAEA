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
*文件名： OpenSslUsage
*版本号： net10.0
*唯一标识：5f2c5798-465f-4d2f-b6a2-5a4e0b1883f8
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 13:40:21
*描述：兼容子命令的帮助文本
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 13:40:21
*修改人： yswenli
*版本号： net10.0
*描述：新增兼容层帮助文本
*
*****************************************************************************/
namespace SAEA.Sockets.SslTlsTool.OpenSsl;

/// <summary>兼容子命令的帮助文本。</summary>
internal static class OpenSslUsage
{
    /// <summary>列出支持的子命令。</summary>
    /// <returns>以顿号分隔的子命令清单</returns>
    public static string CommandList() => "req、x509、pkcs12、pkcs8、pkey、crl2pkcs7、pkcs7、version";

    /// <summary>取子命令的完整帮助文本。</summary>
    /// <param name="command">子命令名</param>
    /// <returns>帮助文本</returns>
    public static string For(string command)
    {
        switch (command)
        {
            case "version":
                return "用法：SAEA.Sockets.SslTlsTool version\n\n输出本工具版本，并声明与 openssl version 不兼容。";
            default:
                // 过渡实现：Task 2 起按子命令逐个替换为真实参数清单。
                throw new OpenSslException($"usage: 未实现的子命令 {command}。");
        }
    }

    /// <summary>尚未实现子命令的占位帮助。</summary>
    /// <param name="command">子命令名</param>
    /// <returns>帮助文本</returns>
    public static string Pending(string command) =>
        $"用法：SAEA.Sockets.SslTlsTool {command} [选项]\n"
        + "该子命令尚未实现，当前只接受 -help；参数清单（-in、-inform、-out、-outform 等）随实现补齐。\n"
        + $"支持的子命令：{CommandList()}。";
}