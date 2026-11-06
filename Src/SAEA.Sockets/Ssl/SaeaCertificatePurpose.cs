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
 *文件名： SaeaCertificatePurpose
 *版本号： v26.11.5.1
 *唯一标识：a5709533-b803-427c-b6d4-77463791592a
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/6 13:38:08
 *描述：证书用途
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/6 13:38:08
 *修改人： yswenli
 *版本号： v26.11.5.1
 *描述：新增开发用证书生成 API
 *
 *****************************************************************************/
namespace SAEA.Sockets.Ssl
{
    /// <summary>
    /// 证书用途。
    /// </summary>
    public enum SaeaCertificatePurpose
    {
        /// <summary>服务端证书，用于 TLS 服务端并写入 SAN。</summary>
        Server,

        /// <summary>客户端证书，用于 mTLS 客户端，不写 SAN。</summary>
        Client,

        /// <summary>根证书颁发机构，仅由 <see cref="SaeaCertificateAuthority"/> 创建。</summary>
        CertificateAuthority
    }
}