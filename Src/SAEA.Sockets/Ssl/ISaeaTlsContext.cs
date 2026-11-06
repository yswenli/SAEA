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
*文件名： ISaeaTlsContext
*版本号： v26.11.5.1
*唯一标识：50ea9de9-eb75-49dc-a89f-d96d28e68a4c
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/5 15:06:36
*描述：每连接 TLS 状态
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