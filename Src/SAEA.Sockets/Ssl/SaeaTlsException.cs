/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.Sockets.Ssl
*文件名： SaeaTlsException
*版本号： v26.11.5.1
*唯一标识：83639b03-29aa-448c-85ee-45becaba0646
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/5 15:06:36
*描述：TLS 相关异常
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 13:11:19
*修改人： yswenli
*版本号： v26.11.5.1
*描述：规范文件头 banner
*
*****************************************************************************/
using System;

namespace SAEA.Sockets.Ssl
{
    /// <summary>
    /// TLS 握手、证书校验或协议协商失败时抛出的异常。
    /// </summary>
    public class SaeaTlsException : Exception
    {
        /// <summary>
        /// 构造 TLS 异常。
        /// </summary>
        public SaeaTlsException()
        {
        }

        /// <summary>
        /// 构造 TLS 异常。
        /// </summary>
        /// <param name="message">异常消息</param>
        public SaeaTlsException(string message) : base(message)
        {
        }

        /// <summary>
        /// 构造 TLS 异常。
        /// </summary>
        /// <param name="message">异常消息</param>
        /// <param name="innerException">引发本异常的内部异常</param>
        public SaeaTlsException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}