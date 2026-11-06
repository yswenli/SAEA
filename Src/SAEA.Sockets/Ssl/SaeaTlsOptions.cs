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
*文件名： SaeaTlsOptions
*版本号： v26.11.5.1
*唯一标识：22e2d1da-f69c-4f73-9313-92fbed0d844e
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/5 15:06:36
*描述：统一 TLS 配置
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
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace SAEA.Sockets.Ssl
{
    /// <summary>
    /// TLS 协议版本常量。
    /// </summary>
    /// <remarks>
    /// <c>netstandard2.0</c> 的 <see cref="SslProtocols"/> 未定义 <c>Tls13</c>，
    /// 这里使用其运行时值（0x3000）补充定义，便于在低版本目标框架下表达 TLS 1.3。
    /// </remarks>
    public static class SaeaTlsProtocols
    {
        /// <summary>TLS 1.3 协议值（0x3000），用于在低版本目标框架下表达 TLS 1.3</summary>
        public const SslProtocols Tls13 = (SslProtocols)12288;

        /// <summary>默认启用 TLS 1.2 + TLS 1.3。</summary>
        public const SslProtocols Default = SslProtocols.Tls12 | Tls13;
    }

    /// <summary>
    /// 统一 TLS 配置。所有 SSL 行为都从这里进入。
    /// </summary>
    public sealed class SaeaTlsOptions
    {
        /// <summary>是否启用 TLS。</summary>
        public bool Enabled { get; set; }

        /// <summary>启用的协议版本，默认 TLS 1.2 + TLS 1.3。</summary>
        public SslProtocols Protocols { get; set; } = SaeaTlsProtocols.Default;

        /// <summary>客户端目标主机名；用于 SNI 和证书主机名校验。</summary>
        public string TargetHost { get; set; }

        /// <summary>服务端默认证书。</summary>
        public X509Certificate2 ServerCertificate { get; set; }

        /// <summary>服务端 SNI 证书选择器：参数为客户端 SNI 主机名，返回 null 时回退到 ServerCertificate。</summary>
        public Func<string, X509Certificate2> ServerCertificateSelector { get; set; }

        /// <summary>客户端证书集合（用于 mTLS）。</summary>
        public X509Certificate2Collection ClientCertificates { get; set; }

        /// <summary>服务端是否要求客户端证书。</summary>
        public bool ClientCertificateRequired { get; set; }

        /// <summary>是否允许无效证书（仅建议测试环境使用）。</summary>
        public bool AllowInvalidCertificates { get; set; }

        /// <summary>是否检查证书吊销状态。</summary>
        public bool CheckCertificateRevocation { get; set; }

        /// <summary>自定义证书校验回调。null 时使用内置校验。</summary>
        public RemoteCertificateValidationCallback CertificateValidationCallback { get; set; }

        /// <summary>ALPN 协议列表（可选）。</summary>
        public IList<string> ApplicationProtocols { get; set; }

        /// <summary>握手超时，默认 10 秒。</summary>
        public TimeSpan HandshakeTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>握手开始回调。</summary>
        public Action<SaeaTlsHandshakeContext> HandshakeStarted { get; set; }

        /// <summary>握手成功回调。</summary>
        public Action<SaeaTlsHandshakeContext> HandshakeCompleted { get; set; }

        /// <summary>握手失败回调。</summary>
        public Action<SaeaTlsHandshakeContext> HandshakeFailed { get; set; }
    }
}