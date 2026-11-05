/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               
 *Copyright (c) yswenli All Rights Reserved.
 *CLR版本： netstandard2.0
 *命名空间：SAEA.Sockets.Ssl
 *文件名： ISaeaTlsContext
 *描述：每连接 TLS 状态
 *
 *****************************************************************************/
using System;
using System.Security.Cryptography.X509Certificates;

namespace SAEA.Sockets.Ssl
{
    /// <summary>
    /// 每连接 TLS 状态，握手成功后可由消费者读取。
    /// </summary>
    public interface ISaeaTlsContext
    {
        /// <summary>是否已完成认证。</summary>
        bool IsAuthenticated { get; }

        /// <summary>是否为服务端。</summary>
        bool IsServer { get; }

        /// <summary>SNI 主机名。</summary>
        string SniHostName { get; }

        /// <summary>协商出的协议版本。</summary>
        string NegotiatedProtocol { get; }

        /// <summary>协商出的加密套件。</summary>
        string CipherSuite { get; }

        /// <summary>本地证书。</summary>
        X509Certificate2 LocalCertificate { get; }

        /// <summary>远端证书。</summary>
        X509Certificate2 RemoteCertificate { get; }

        /// <summary>握手异常。</summary>
        Exception HandshakeException { get; }
    }
}