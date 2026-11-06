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
*命名空间：SAEA.MQTT.Certificates
*文件名： X509CertificateProvider
*版本号： v26.11.5.1
*唯一标识：35ca751e-76d5-40a7-b883-6696770e7617
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 12:04:50
*描述：基于 X509 证书的客户端证书提供者
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:50
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
#if !WINDOWS_UWP
using System;
using System.Security.Cryptography.X509Certificates;

namespace SAEA.MQTT.Certificates
{
    /// <summary>
    /// 直接返回既有证书实例的提供器
    /// </summary>
    public class X509CertificateProvider : ICertificateProvider
    {
        readonly X509Certificate2 _certificate;

        /// <summary>
        /// 使用已有证书实例初始化实例
        /// </summary>
        /// <param name="certificate">证书实例，不能为null</param>
        public X509CertificateProvider(X509Certificate2 certificate)
        {
            _certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
        }

        /// <summary>
        /// 获取初始化时传入的证书实例
        /// </summary>
        /// <returns>与构造时传入的同一个证书实例，不复制也不释放</returns>
        public X509Certificate2 GetCertificate()
        {
            return _certificate;
        }
    }
}
#endif