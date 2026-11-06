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
 *文件名： SaeaCertificateOptions
 *版本号： v26.11.5.1
 *唯一标识：58177c72-2eeb-43ee-995a-e9bec6c363cb
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/6 13:38:08
 *描述：证书生成参数
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/6 13:38:08
 *修改人： yswenli
 *版本号： v26.11.5.1
 *描述：新增开发用证书生成 API
 *
 *****************************************************************************/
using System;

namespace SAEA.Sockets.Ssl
{
    /// <summary>
    /// 证书生成参数。未赋值的成员按证书用途取默认值，默认值在生成时解析，不会回写到本实例。
    /// </summary>
    public sealed class SaeaCertificateOptions
    {
        /// <summary>
        /// 证书用途，默认 <see cref="SaeaCertificatePurpose.Server"/>。
        /// </summary>
        public SaeaCertificatePurpose Purpose { get; set; }

        /// <summary>
        /// 完整主题 DN，如 <c>CN=localhost,O=SAEA</c>；为 null 或空白时按用途取默认值。
        /// </summary>
        public string Subject { get; set; }

        /// <summary>
        /// 服务端 DNS SAN。为 null 时按主题 CN 与 <c>localhost</c> 推导；空数组表示不写 dNSName。
        /// </summary>
        public string[] DnsNames { get; set; }

        /// <summary>
        /// 服务端 IP SAN。为 null 时取 <c>127.0.0.1</c>；空数组表示不写 iPAddress。
        /// </summary>
        public string[] IpAddresses { get; set; }

        /// <summary>
        /// RSA 密钥长度。0 或未赋值表示 2048，非 0 时只支持 2048/3072/4096。
        /// </summary>
        public int KeySize { get; set; }

        /// <summary>
        /// 生效时间。未赋值时取生成时刻减 1 天，留出时钟偏差余量。
        /// </summary>
        public DateTimeOffset NotBefore { get; set; }

        /// <summary>
        /// 到期时间。未赋值时服务端/客户端取生成时刻加 30 天，CA 取生成时刻加 3650 天。
        /// </summary>
        public DateTimeOffset NotAfter { get; set; }

        /// <summary>
        /// PFX 口令。为 null 时取默认口令 <c>saea</c>；显式空字符串表示无口令。
        /// </summary>
        public string PfxPassword { get; set; }
    }
}