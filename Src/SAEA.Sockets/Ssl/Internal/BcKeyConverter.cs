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
*命名空间：SAEA.Sockets.Ssl.Internal
*文件名： BcKeyConverter
*版本号： v26.11.5.1
*唯一标识：569487b4-06be-465a-bffa-0703a5df83cc
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/5 15:06:36
*描述：X509Certificate2 与 BouncyCastle 密钥/证书的转换
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 13:11:19
*修改人： yswenli
*版本号： v26.11.5.1
*描述：规范文件头 banner
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;

namespace SAEA.Sockets.Ssl.Internal
{
    /// <summary>
    /// 把 .NET 证书与私钥转换为 BouncyCastle TLS 需要的类型。
    /// </summary>
    internal static class BcKeyConverter
    {
        /// <summary>
        /// 构建服务端/客户端证书链。优先使用 <see cref="X509Chain"/> 找到中间证书；
        /// 构建失败时退化为仅包含叶子证书。
        /// </summary>
        /// <param name="crypto">当前连接的 BC 加密实例。</param>
        /// <param name="certificate">叶子证书。</param>
        /// <param name="certificateRequestContext">
        /// TLS 1.3 证书请求上下文；TLS 1.2 传 null。TLS 1.3 下必须非 null（可为空数组）。
        /// </param>
        public static Certificate ToCertificateChain(BcTlsCrypto crypto, X509Certificate2 certificate,
            byte[] certificateRequestContext = null)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));

            var certificates = new List<TlsCertificate>();

            try
            {
                using (var chain = new X509Chain())
                {
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;

                    chain.Build(certificate);

                    if (chain.ChainElements.Count > 0)
                    {
                        foreach (X509ChainElement element in chain.ChainElements)
                        {
                            var elementCertificate = element.Certificate;
                            if (elementCertificate == null) continue;
                            certificates.Add(new BcTlsCertificate(crypto, elementCertificate.RawData));
                        }
                    }
                }
            }
            catch
            {
                // 某些平台不支持 X509Chain；退化为叶子证书。
            }

            if (certificates.Count == 0)
                certificates.Add(new BcTlsCertificate(crypto, certificate.RawData));

            if (certificateRequestContext == null)
                return new Certificate(certificates.ToArray());

            var entries = new CertificateEntry[certificates.Count];
            for (var i = 0; i < certificates.Count; i++)
                entries[i] = new CertificateEntry(certificates[i], null);

            return new Certificate(certificateRequestContext, entries);
        }

        /// <summary>
        /// 提取证书私钥。
        /// </summary>
        public static AsymmetricKeyParameter GetPrivateKey(X509Certificate2 certificate)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));
            if (!certificate.HasPrivateKey)
                throw new SaeaTlsException("证书不包含私钥。");

            try
            {
                using (var rsa = certificate.GetRSAPrivateKey())
                {
                    if (rsa != null)
                        return DotNetUtilities.GetRsaKeyPair(rsa).Private;
                }
            }
            catch (CryptographicException) { }

            try
            {
                using (var ecdsa = certificate.GetECDsaPrivateKey())
                {
                    if (ecdsa != null)
                        return DotNetUtilities.GetECDsaKeyPair(ecdsa).Private;
                }
            }
            catch (CryptographicException) { }

            throw new SaeaTlsException("不支持的证书私钥类型。");
        }

        /// <summary>
        /// 根据证书私钥类型返回 TLS 签名算法。
        /// </summary>
        public static short GetSignatureAlgorithm(X509Certificate2 certificate)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));

            try
            {
                using (var rsa = certificate.GetRSAPrivateKey())
                {
                    if (rsa != null) return SignatureAlgorithm.rsa;
                }
            }
            catch (CryptographicException) { }

            try
            {
                using (var ecdsa = certificate.GetECDsaPrivateKey())
                {
                    if (ecdsa != null) return SignatureAlgorithm.ecdsa;
                }
            }
            catch (CryptographicException) { }

            return SignatureAlgorithm.rsa;
        }

        /// <summary>
        /// 证书是否为 ECDSA（用于筛选密码套件）。
        /// </summary>
        public static bool IsEcdsaCertificate(X509Certificate2 certificate)
        {
            if (certificate == null) return false;
            // 1.2.840.10045.2.1 = id-ecPublicKey
            return string.Equals(certificate.PublicKey?.Oid?.Value, "1.2.840.10045.2.1",
                StringComparison.Ordinal);
        }

        /// <summary>
        /// 为当前上下文选择签名算法；无法匹配时退化为 SHA-256。
        /// </summary>
        public static SignatureAndHashAlgorithm ChooseSignatureAndHashAlgorithm(
            TlsContext context, IList<SignatureAndHashAlgorithm> peerSigAlgs, short signatureAlgorithm)
        {
            var supported = peerSigAlgs;
            if (supported == null || supported.Count == 0)
                supported = TlsUtilities.GetDefaultSupportedSignatureAlgorithms(context);

            var algorithm = TlsUtilities.ChooseSignatureAndHashAlgorithm(context, supported, signatureAlgorithm);
            if (algorithm != null) return algorithm;

            return SignatureAndHashAlgorithm.GetInstance(Org.BouncyCastle.Tls.HashAlgorithm.sha256, signatureAlgorithm);
        }

        private static readonly object CipherSuiteNameLock = new object();
        private static Dictionary<int, string> _cipherSuiteNames;

        /// <summary>
        /// 获取密码套件名称。BC 2.6 未公开名称查询 API，这里从常量字段建立映射。
        /// </summary>
        public static string GetCipherSuiteName(int cipherSuite)
        {
            if (cipherSuite <= 0) return null;

            var map = _cipherSuiteNames;
            if (map == null)
            {
                lock (CipherSuiteNameLock)
                {
                    if (_cipherSuiteNames == null)
                    {
                        var built = new Dictionary<int, string>();
                        var fields = typeof(CipherSuite).GetFields(
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        foreach (var field in fields)
                        {
                            if (field.FieldType != typeof(int)) continue;
                            try
                            {
                                var value = (int)field.GetValue(null);
                                if (!built.ContainsKey(value))
                                    built[value] = field.Name;
                            }
                            catch { }
                        }
                        _cipherSuiteNames = built;
                    }
                    map = _cipherSuiteNames;
                }
            }

            string name;
            return map.TryGetValue(cipherSuite, out name) ? name : "0x" + cipherSuite.ToString("X4");
        }
    }
}