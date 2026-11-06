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
 *文件名： SaeaCertificateFactory
 *版本号： v26.11.5.1
 *唯一标识：d79d14ae-70f1-408d-a1cb-19325f15ae36
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/6 13:38:08
 *描述：开发用证书生成入口
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

using SAEA.Sockets.Ssl.Internal;

namespace SAEA.Sockets.Ssl
{
    /// <summary>
    /// 开发用证书生成入口。生成的证书仅用于开发/测试，请勿用于生产。
    /// </summary>
    public static class SaeaCertificateFactory
    {
        /// <summary>
        /// 创建单张自签证书。issuer 与 subject 相同。
        /// </summary>
        /// <param name="options">生成参数；为 null 时抛 ArgumentNullException</param>
        /// <returns>自签证书与私钥</returns>
        /// <exception cref="ArgumentException">
        /// options.Purpose 为 CA，或主题 DN、SAN、KeySize、有效期非法
        /// </exception>
        public static SaeaCertificateBundle CreateSelfSigned(SaeaCertificateOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options.Purpose == SaeaCertificatePurpose.CertificateAuthority)
                throw new ArgumentException("根证书请使用 SaeaCertificateAuthority.Create 生成。", nameof(options));

            var spec = SaeaCertificateWriter.Resolve(options, options.Purpose);
            return new SaeaCertificateBundle(SaeaCertificateWriter.CreateSelfSigned(spec));
        }
    }
}