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
*唯一标识：c6f24377-556a-4061-b791-6d6757b7a4e9
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MQTT服务端类
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MQTT服务端类
*
*****************************************************************************/
using System;
using System.Security.Authentication;
using SAEA.MQTT.Certificates;

namespace SAEA.MQTT.Server
{
    public class MqttServerTlsTcpEndpointOptions : MqttServerTcpEndpointBaseOptions
    {
        ICertificateProvider _certificateProvider;

        public MqttServerTlsTcpEndpointOptions()
        {
            Port = 8883;
        }

        [Obsolete("Please use CertificateProvider with 'BlobCertificateProvider' instead.")]
        public byte[] Certificate { get; set; }

        [Obsolete("Please use CertificateProvider with 'BlobCertificateProvider' including password property instead.")]
        public IMqttServerCertificateCredentials CertificateCredentials { get; set; }

#if !WINDOWS_UWP
        public System.Net.Security.RemoteCertificateValidationCallback RemoteCertificateValidationCallback { get; set; }
#endif
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

        public bool ClientCertificateRequired { get; set; }

        public bool CheckCertificateRevocation { get; set; }

        public SslProtocols SslProtocol { get; set; } = SslProtocols.Tls12;
    }
}
