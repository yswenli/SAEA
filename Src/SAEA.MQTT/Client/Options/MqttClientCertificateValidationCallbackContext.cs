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
*文件名： MqttClientCertificateValidationCallbackContext
*版本号： v26.4.23.1
*唯一标识：e0222744-e88b-4339-b794-b1a4b4eaded6
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientCertificateValidationCallbackContext接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace SAEA.MQTT.Client.Options
{
    /// <summary>
    /// 证书校验回调的上下文，汇总待校验证书与平台校验结论
    /// </summary>
    public class MqttClientCertificateValidationCallbackContext
    {
        /// <summary>
        /// 服务端提供的证书
        /// </summary>
        public X509Certificate Certificate { get; set; }

        /// <summary>
        /// 服务端证书链的构建结果
        /// </summary>
        public X509Chain Chain { get; set; }

        /// <summary>
        /// 平台 TLS 校验发现的策略错误
        /// </summary>
        public SslPolicyErrors SslPolicyErrors { get; set; }

        /// <summary>
        /// 发起本次连接的通道选项
        /// </summary>
        public IMqttClientChannelOptions ClientOptions { get; set; }
    }
}
