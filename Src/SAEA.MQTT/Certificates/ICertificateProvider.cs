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
*命名空间：SAEA.MQTT.Certificates
*文件名： ICertificateProvider
*版本号： v26.4.23.1
*唯一标识：0ad87358-c994-4062-b602-31bdc33f0edd
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：ICertificateProvider接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System.Security.Cryptography.X509Certificates;

namespace SAEA.MQTT.Certificates
{
    /// <summary>
    /// 客户端证书提供器
    /// </summary>
    public interface ICertificateProvider
    {
        /// <summary>
        /// 获取用于TLS握手的客户端证书
        /// </summary>
        /// <returns>客户端证书实例</returns>
        X509Certificate2 GetCertificate();
    }
}
