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
*文件名： BlobCertificateProvider
*版本号： v26.4.23.1
*唯一标识：2654c9e0-0ecc-40f3-b106-f3e3bc40e35e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：BlobCertificateProvider接口
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
using System.Security.Cryptography.X509Certificates;

namespace SAEA.MQTT.Certificates
{
    /// <summary>
    /// 从证书字节内容创建证书的提供器
    /// </summary>
    public class BlobCertificateProvider : ICertificateProvider
    {
        /// <summary>
        /// 使用证书字节内容初始化实例
        /// </summary>
        /// <param name="blob">证书原始字节，不能为null</param>
        public BlobCertificateProvider(byte[] blob)
        {
            Blob = blob ?? throw new ArgumentNullException(nameof(blob));
        }

        /// <summary>
        /// 证书原始字节内容
        /// </summary>
        public byte[] Blob { get; }

        /// <summary>
        /// 读取证书使用的密码，为空时按无密码证书处理
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// 依据当前 Blob 与 Password 新建证书实例
        /// </summary>
        /// <returns>新建的证书实例，调用方负责释放</returns>
        public X509Certificate2 GetCertificate()
        {
            if (string.IsNullOrEmpty(Password))
            {
                // Use a different overload when no password is specified. Otherwise the constructor will fail.
                return new X509Certificate2(Blob);
            }

            return new X509Certificate2(Blob, Password);
        }
    }
}
