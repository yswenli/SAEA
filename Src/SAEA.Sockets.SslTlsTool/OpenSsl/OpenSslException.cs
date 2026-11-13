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
*文件名： OpenSslException
*版本号： net10.0
*唯一标识：3b06b98e-5bf7-43eb-b9bd-8f02cb6fb6ad
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 13:40:21
*描述：兼容子命令的统一错误载体
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 13:40:21
*修改人： yswenli
*版本号： net10.0
*描述：新增兼容层错误类型
*
*****************************************************************************/
namespace SAEA.Sockets.SslTlsTool.OpenSsl;

/// <summary>兼容子命令的统一错误载体，Message 即写 stderr 的整句。</summary>
internal sealed class OpenSslException : Exception
{
    /// <summary>用整句错误信息创建异常。</summary>
    /// <param name="message">写 stderr 的整句错误信息</param>
    public OpenSslException(string message)
        : base(message)
    {
    }
}