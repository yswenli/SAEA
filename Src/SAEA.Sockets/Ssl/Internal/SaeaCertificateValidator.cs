/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               
 *Copyright (c) yswenli All Rights Reserved.
 *CLR版本： netstandard2.0
 *命名空间：SAEA.Sockets.Ssl.Internal
 *文件名： SaeaCertificateValidator
 *描述：证书链与主机名校验
 *
 *****************************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

using Org.BouncyCastle.Tls;
using Org.BouncyCastle.X509;

namespace SAEA.Sockets.Ssl.Internal
{
    /// <summary>
    /// X.509 证书校验工具。使用 BouncyCastle 解析 SAN，使用 <see cref="X509Chain"/> 构建链。
    /// </summary>
    internal static class SaeaCertificateValidator
    {
        /// <summary>把 BC 证书链转换为 .NET 证书集合。</summary>
        public static X509Certificate2Collection ToX509Certificate2Collection(Certificate certificate)
        {
            var collection = new X509Certificate2Collection();
            if (certificate == null || certificate.IsEmpty) return collection;

            var count = certificate.Length;
            for (var i = 0; i < count; i++)
            {
                var item = certificate.GetCertificateAt(i);
                if (item == null) continue;
                collection.Add(new X509Certificate2(item.GetEncoded()));
            }
            return collection;
        }

        /// <summary>把 BC 证书链的叶子证书转换为 .NET 证书。</summary>
        public static X509Certificate2 ToX509Certificate2(Certificate certificate)
        {
            if (certificate == null || certificate.IsEmpty) return null;
            var leaf = certificate.GetCertificateAt(0);
            return leaf == null ? null : new X509Certificate2(leaf.GetEncoded());
        }

        /// <summary>
        /// 校验服务端证书：链 + 主机名。
        /// </summary>
        public static SslPolicyErrors ValidateServerCertificate(
            X509Certificate2 certificate,
            X509Certificate2Collection additionalChain,
            string targetHost,
            bool checkRevocation,
            out X509Chain chain)
        {
            chain = null;

            if (certificate == null)
                return SslPolicyErrors.RemoteCertificateNotAvailable;

            var errors = ValidateChain(certificate, additionalChain, checkRevocation, false, out chain);

            if (!MatchHostName(certificate, targetHost))
                errors |= SslPolicyErrors.RemoteCertificateNameMismatch;

            return errors;
        }

        /// <summary>
        /// 构建并校验证书链。
        /// </summary>
        /// <param name="certificate">叶子证书</param>
        /// <param name="additionalChain">随握手一起下发的中间证书</param>
        /// <param name="checkRevocation">是否在线检查吊销状态</param>
        /// <param name="allowUnknownRoot">是否接受未知/自签名根（用于内网 mTLS 默认行为）</param>
        /// <param name="chain">构建出的链</param>
        public static SslPolicyErrors ValidateChain(
            X509Certificate2 certificate,
            X509Certificate2Collection additionalChain,
            bool checkRevocation,
            bool allowUnknownRoot,
            out X509Chain chain)
        {
            chain = null;

            if (certificate == null)
                return SslPolicyErrors.RemoteCertificateNotAvailable;

            try
            {
                chain = new X509Chain();
                chain.ChainPolicy.RevocationMode = checkRevocation
                    ? X509RevocationMode.Online
                    : X509RevocationMode.NoCheck;
                chain.ChainPolicy.VerificationFlags = allowUnknownRoot
                    ? X509VerificationFlags.AllowUnknownCertificateAuthority
                    : X509VerificationFlags.NoFlag;

                if (additionalChain != null)
                {
                    foreach (var item in additionalChain)
                    {
                        if (item == null) continue;
                        if (string.Equals(item.Thumbprint, certificate.Thumbprint, StringComparison.OrdinalIgnoreCase))
                            continue;
                        chain.ChainPolicy.ExtraStore.Add(item);
                    }
                }

                var ok = chain.Build(certificate);
                return ok ? SslPolicyErrors.None : SslPolicyErrors.RemoteCertificateChainErrors;
            }
            catch
            {
                return SslPolicyErrors.RemoteCertificateChainErrors;
            }
        }

        /// <summary>
        /// 主机名匹配：优先 SAN dNSName，其次 iPAddress，最后回退 CN。
        /// 通配符只允许最左侧一个标签。
        /// </summary>
        public static bool MatchHostName(X509Certificate2 certificate, string host)
        {
            if (certificate == null) return false;
            if (string.IsNullOrEmpty(host)) return true;

            host = host.Trim().TrimEnd('.');

            IPAddress ipAddress;
            var isIp = IPAddress.TryParse(host, out ipAddress);

            try
            {
                var parser = new X509CertificateParser();
                var bcCertificate = parser.ReadCertificate(certificate.RawData);
                var names = bcCertificate.GetSubjectAlternativeNames();

                var hasDnsName = false;

                if (names != null)
                {
                    foreach (var entry in names)
                    {
                        var items = entry as IList;
                        if (items == null || items.Count < 2) continue;

                        var type = Convert.ToInt32(items[0]);

                        if (isIp)
                        {
                            if (type != 7) continue;
                            var bytes = items[1] as byte[];
                            if (bytes != null && ipAddress.Equals(new IPAddress(bytes)))
                                return true;
                        }
                        else
                        {
                            if (type != 2) continue;
                            var dns = items[1] as string;
                            if (string.IsNullOrEmpty(dns)) continue;
                            hasDnsName = true;
                            if (MatchDnsName(dns, host)) return true;
                        }
                    }
                }

                if (isIp || hasDnsName) return false;

                var commonName = certificate.GetNameInfo(X509NameType.SimpleName, false);
                return !string.IsNullOrEmpty(commonName) && MatchDnsName(commonName, host);
            }
            catch
            {
                return false;
            }
        }

        private static bool MatchDnsName(string pattern, string host)
        {
            if (string.IsNullOrEmpty(pattern)) return false;

            pattern = pattern.Trim().TrimEnd('.');
            host = host.TrimEnd('.');

            if (string.Equals(pattern, host, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!pattern.StartsWith("*.", StringComparison.Ordinal))
                return false;

            var suffix = pattern.Substring(1); // ".example.com"
            if (!host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return false;

            var left = host.Substring(0, host.Length - suffix.Length);
            return left.Length > 0 && left.IndexOf('.') < 0;
        }
    }
}