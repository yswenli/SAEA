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
*修改时间：2026/11/13 14:04:10
*修改人： yswenli
*版本号： net10.0
*描述：补 pkcs8 与 pkey 子命令的帮助文本
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
            case "x509":
                return "用法：SAEA.Sockets.SslTlsTool x509 [选项]\n"
                    + "读取证书、打印所选信息并在 PEM 与 DER 之间转换；-in 缺省 stdin，-out 缺省 stdout。\n"
                    + "读取/输出：-inform PEM|DER（默认 PEM）、-outform PEM|DER（默认 PEM）、-noout\n"
                    + "打印：-subject、-issuer、-dates、-serial、-fingerprint（默认 SHA-1）、-sha256、-pubkey\n"
                    + "签发：-req、-CA <文件>、-CAkey <文件>、-CAcreateserial、-days <正整数>、-extfile <文件>、-extensions <段名>、-copy_extensions copy|none\n"
                    + "不支持：-text、-nameopt（姓名固定 oneline 风格）；打印项写 stdout 且位于证书数据之前。\n";
            case "pkcs8":
                return "用法：SAEA.Sockets.SslTlsTool pkcs8 [选项]\n在 PKCS#1 与 PKCS#8 之间转换私钥；-in 缺省 stdin，-out 缺省 stdout。\n"
                    + "选项：-in、-out、-inform PEM|DER、-outform PEM|DER、-topk8、-nocrypt、-v1 PBE-SHA1-3DES、-v2 aes-128-cbc|aes-192-cbc|aes-256-cbc|des3（默认 aes-256-cbc）、-passin、-passout；-v1 与 -v2 互斥，非 -topk8 不支持 -passout。\n";
            case "pkey":
                return "用法：SAEA.Sockets.SslTlsTool pkey [选项]\n读取私钥并输出 PKCS#8（默认）、PKCS#1（-traditional）或公钥（-pubout）；-in 缺省 stdin，-out 缺省 stdout。\n"
                    + "选项：-in、-out、-inform PEM|DER、-outform PEM|DER、-pubout、-traditional、-aes-128-cbc、-aes-192-cbc、-aes-256-cbc、-des3（与 -traditional 互斥）、-passin、-passout。\n";
            default:
                // 过渡实现：Task 3 起按子命令逐个替换为真实参数清单。
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