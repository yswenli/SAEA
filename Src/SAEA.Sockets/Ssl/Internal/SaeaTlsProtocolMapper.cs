/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               
 *Copyright (c) yswenli All Rights Reserved.
 *CLR版本： netstandard2.0
 *命名空间：SAEA.Sockets.Ssl.Internal
 *文件名： SaeaTlsProtocolMapper
 *描述：SslProtocols 到 BouncyCastle ProtocolVersion 的映射
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