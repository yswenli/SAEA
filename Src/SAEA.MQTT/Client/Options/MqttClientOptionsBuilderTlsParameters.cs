/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MQTT.Client.Options
*文件名： MqttClientOptionsBuilderTlsParameters
*版本号： v26.4.23.1
*唯一标识：de6a210e-71b4-4088-8653-d53b06a20fd1
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientOptionsBuilderTlsParameters接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace SAEA.MQTT.Client.Options
{
    /// <summary>
    /// 构建阶段使用的 TLS 参数，仅在 UseTls 为 true 时转写到 MqttClientTlsOptions
    /// </summary>
    public class MqttClientOptionsBuilderTlsParameters
    {
        /// <summary>
        /// 是否启用 TLS，默认 false
        /// </summary>
        public bool UseTls { get; set; }

        /// <summary>
        /// 已废弃的证书校验回调，改用 CertificateValidationHandler
        /// </summary>
        [Obsolete("This property will be removed soon. Use CertificateValidationHandler instead.")]
        public Func<X509Certificate, X509Chain, SslPolicyErrors, IMqttClientOptions, bool> CertificateValidationCallback
        {
            get;
            set;
        }

        /// <summary>
        /// 证书校验处理器，返回 true 表示接受该证书，null 表示按默认规则校验
        /// </summary>
        public Func<MqttClientCertificateValidationCallbackContext, bool> CertificateValidationHandler { get; set; }

#if NETCOREAPP3_1 || NET5_0
        /// <summary>
        /// 使用的 TLS 协议版本，默认 TLS 1.3
        /// </summary>
        public SslProtocols SslProtocol { get; set; } = SslProtocols.Tls13;
#else
        /// <summary>
        /// 使用的 TLS 协议版本，默认 TLS 1.2
        /// </summary>
        public SslProtocols SslProtocol { get; set; } = SslProtocols.Tls12;
#endif

#if WINDOWS_UWP
        /// <summary>
        /// 客户端证书集合，每项为证书原始字节，null 表示不带客户端证书
        /// </summary>
        public IEnumerable<IEnumerable<byte>> Certificates { get; set; }
#else
        /// <summary>
        /// 客户端证书集合，null 表示不带客户端证书
        /// </summary>
        public IEnumerable<X509Certificate> Certificates { get; set; }
#endif

#if NETCOREAPP3_1
	    /// <summary>
	    /// TLS 应用层协议协商列表，null 表示不启用 ALPN
	    /// </summary>
	    public List<SslApplicationProtocol> ApplicationProtocols { get;set; }
#endif 

	    /// <summary>
	    /// 是否接受不受信任的证书，默认 false
	    /// </summary>
	    public bool AllowUntrustedCertificates { get; set; }

        /// <summary>
        /// 是否忽略证书链校验错误，默认 false
        /// </summary>
        public bool IgnoreCertificateChainErrors { get; set; }

        /// <summary>
        /// 是否忽略证书吊销状态校验错误，默认 false
        /// </summary>
        public bool IgnoreCertificateRevocationErrors { get; set; }
    }
}
