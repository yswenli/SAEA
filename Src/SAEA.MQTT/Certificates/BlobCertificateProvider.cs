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
*唯一标识：5a21a803-14c6-4f12-a9ea-daddc51af2e6
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;
using System.Security.Cryptography.X509Certificates;

namespace SAEA.MQTT.Certificates
{
    public class BlobCertificateProvider : ICertificateProvider
    {
        public BlobCertificateProvider(byte[] blob)
        {
            Blob = blob ?? throw new ArgumentNullException(nameof(blob));
        }

        public byte[] Blob { get; }

        public string Password { get; set; }

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
