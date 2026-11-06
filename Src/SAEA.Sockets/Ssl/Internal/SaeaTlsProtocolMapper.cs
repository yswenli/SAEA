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
*命名空间：SAEA.Sockets.Ssl.Internal
*文件名： SaeaTlsProtocolMapper
*版本号： v26.11.5.1
*唯一标识：a4ef7417-5153-4b8f-8a1d-4c7e3e534a87
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/5 15:06:36
*描述：SslProtocols 到 BouncyCastle ProtocolVersion 的映射
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
using System.Collections.Generic;
using System.Security.Authentication;

using Org.BouncyCastle.Tls;

namespace SAEA.Sockets.Ssl.Internal
{
    /// <summary>
    /// 将 <see cref="SslProtocols"/> 映射为 BouncyCastle <see cref="ProtocolVersion"/> 数组。
    /// </summary>
    internal static class SaeaTlsProtocolMapper
    {
        /// <summary>
        /// 映射协议版本。返回结果按由高到低排列。
        /// </summary>
        public static ProtocolVersion[] Map(SslProtocols protocols)
        {
            if (protocols == SslProtocols.None || protocols == SslProtocols.Default)
                return new[] { ProtocolVersion.TLSv13, ProtocolVersion.TLSv12 };

            if ((protocols & SslProtocols.Ssl2) != 0 || (protocols & SslProtocols.Ssl3) != 0)
                throw new NotSupportedException("SAEA TLS 不支持 SSL 2.0 / SSL 3.0。");

            var versions = new List<ProtocolVersion>(4);

            if ((protocols & SaeaTlsProtocols.Tls13) != 0)
                versions.Add(ProtocolVersion.TLSv13);
            if ((protocols & SslProtocols.Tls12) != 0)
                versions.Add(ProtocolVersion.TLSv12);
            if ((protocols & SslProtocols.Tls11) != 0)
                versions.Add(ProtocolVersion.TLSv11);
            if ((protocols & SslProtocols.Tls) != 0)
                versions.Add(ProtocolVersion.TLSv10);

            if (versions.Count == 0)
                return new[] { ProtocolVersion.TLSv13, ProtocolVersion.TLSv12 };

            return versions.ToArray();
        }
    }
}