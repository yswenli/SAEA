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
 *文件名： SaeaTlsHandshakeContext
 *描述：TLS 握手回调上下文
 *
 *****************************************************************************/
using System;
using System.Security.Cryptography.X509Certificates;

namespace SAEA.Sockets.Ssl
{
    /// <summary>
    /// 握手回调上下文。
    /// </summary>
    public sealed class SaeaTlsHandshakeContext
    {
        /// <summary>是否为服务端。</summary>
        public bool IsServer { get; internal set; }

        /// <summary>远端地址。</summary>
        public string RemoteEndPoint { get; internal set; }

        /// <summary>客户端目标主机名。</summary>
        public string TargetHost { get; internal set; }

        /// <summary>SNI 主机名。</summary>
        public string SniHostName { get; internal set; }

        /// <summary>本地证书。</summary>
        public X509Certificate2 LocalCertificate { get; internal set; }

        /// <summary>远端证书。</summary>
        public X509Certificate2 RemoteCertificate { get; internal set; }

        /// <summary>协商出的协议版本。</summary>
        public string NegotiatedProtocol { get; internal set; }

        /// <summary>协商出的加密套件。</summary>
        public string CipherSuite { get; internal set; }

        /// <summary>握手异常（失败时）。</summary>
        public Exception Exception { get; internal set; }
    }
}