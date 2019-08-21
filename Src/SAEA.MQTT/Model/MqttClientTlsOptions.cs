/****************************************************************************
*项目名称：SAEA.MQTT.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Model
*类 名 称：MqttClientTlsOptions
*版 本 号： v5.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/14 19:17:02
*描述：
*=====================================================================
*修改时间：2019/1/14 19:17:02
*修 改 人： yswenli
*版 本 号： v5.0.0.1
*描    述：
*****************************************************************************/
using SAEA.MQTT.Interface;
using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace SAEA.MQTT.Model
{
    public class MqttClientTlsOptions
    {
        public bool UseTls { get; set; }

        public bool IgnoreCertificateRevocationErrors { get; set; }

        public bool IgnoreCertificateChainErrors { get; set; }

        public bool AllowUntrustedCertificates { get; set; }

        public List<byte[]> Certificates { get; set; }

        public SslProtocols SslProtocol { get; set; } = SslProtocols.Tls12;

        public Func<X509Certificate, X509Chain, SslPolicyErrors, IMqttClientOptions, bool> CertificateValidationCallback { get; set; }
    }
}
