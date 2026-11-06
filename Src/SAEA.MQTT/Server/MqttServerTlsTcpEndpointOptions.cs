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
*命名空间：SAEA.MQTT.Server
*文件名： MqttServerTlsTcpEndpointOptions
*版本号： v26.4.23.1
*唯一标识：9f25d40f-7550-473c-a638-2f6c6dc9df2b
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttServerTlsTcpEndpointOptions接口
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
using System.Security.Authentication;
using SAEA.MQTT.Certificates;

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// TLS 端点配置，默认端口 8883，需显式启用并配置证书
    /// </summary>
    public class MqttServerTlsTcpEndpointOptions : MqttServerTcpEndpointBaseOptions
    {
        ICertificateProvider _certificateProvider;

        /// <summary>
        /// 创建 TLS 端点配置，默认端口为 8883
        /// </summary>
        public MqttServerTlsTcpEndpointOptions()
        {
            Port = 8883;
        }

        /// <summary>
        /// 已废弃的证书二进制内容，仅在未设置 CertificateProvider 时用于兼容转换
        /// </summary>
        [Obsolete("Please use CertificateProvider with 'BlobCertificateProvider' instead.")]
        public byte[] Certificate { get; set; }

        /// <summary>
        /// 已废弃的证书口令，仅在未设置 CertificateProvider 时与 Certificate 一起用于兼容转换
        /// </summary>
        [Obsolete("Please use CertificateProvider with 'BlobCertificateProvider' including password property instead.")]
        public IMqttServerCertificateCredentials CertificateCredentials { get; set; }

#if !WINDOWS_UWP
        /// <summary>
        /// TLS 握手中对端证书的校验回调，为 null 时使用默认校验
        /// </summary>
        public System.Net.Security.RemoteCertificateValidationCallback RemoteCertificateValidationCallback { get; set; }
#endif
        /// <summary>
        /// 服务端证书提供者，未显式设置时由已废弃的 Certificate 与 CertificateCredentials 惰性转换得到
        /// </summary>
        public ICertificateProvider CertificateProvider
        {
            get
            {
                // Backward compatibility only. Gets converted to auto property when
                // obsolete properties are removed.
                if (_certificateProvider != null)
                {
                    return _certificateProvider;
                }

                if (Certificate == null)
                {
                    return null;
                }

                return new BlobCertificateProvider(Certificate)
                {
                    Password = CertificateCredentials?.Password
                };
            }

            set => _certificateProvider = value;
        }

        /// <summary>
        /// 是否要求客户端提供证书以做双向认证
        /// </summary>
        public bool ClientCertificateRequired { get; set; }

        /// <summary>
        /// 是否在 TLS 握手时检查证书吊销状态
        /// </summary>
        public bool CheckCertificateRevocation { get; set; }

        /// <summary>
        /// 使用的 SSL/TLS 协议版本，默认 TLS 1.2
        /// </summary>
        public SslProtocols SslProtocol { get; set; } = SslProtocols.Tls12;
    }
}
