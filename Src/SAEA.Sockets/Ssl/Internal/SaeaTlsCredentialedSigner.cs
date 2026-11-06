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
*文件名： SaeaTlsCredentialedSigner
*版本号： v26.11.5.1
*唯一标识：47dd0dc2-dec5-4e56-b0e7-2baa1b8bcd95
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/5 15:06:36
*描述：直接使用 X509Certificate2 私钥完成 TLS 签名
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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto;

namespace SAEA.Sockets.Ssl.Internal
{
    /// <summary>
    /// 基于 <see cref="X509Certificate2"/> 私钥句柄的 TLS 签名凭据。
    /// </summary>
    /// <remarks>
    /// 不导出私钥参数，因此同时支持不可导出的 CNG/CAPI 私钥（从 PFX 导入或证书库读取的常见情况）。
    /// TLS 1.2 使用 PKCS#1 v1.5，TLS 1.3 使用 RSASSA-PSS；ECDSA 统一输出 DER 编码。
    /// </remarks>
    internal sealed class SaeaTlsCredentialedSigner : TlsCredentialedSigner
    {
        private readonly X509Certificate2 _certificate;
        private readonly Certificate _chain;
        private readonly SignatureAndHashAlgorithm _signatureAndHashAlgorithm;
        private readonly HashAlgorithmName _hashAlgorithm;
        private readonly bool _isEcdsa;
        private readonly bool _useRsaPss;

        public SaeaTlsCredentialedSigner(X509Certificate2 certificate, Certificate chain,
            SignatureAndHashAlgorithm signatureAndHashAlgorithm)
        {
            _certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
            _chain = chain ?? throw new ArgumentNullException(nameof(chain));
            _signatureAndHashAlgorithm = signatureAndHashAlgorithm
                ?? throw new ArgumentNullException(nameof(signatureAndHashAlgorithm));

            _isEcdsa = BcKeyConverter.IsEcdsaCertificate(certificate);
            _hashAlgorithm = MapHashAlgorithm(signatureAndHashAlgorithm.Hash);

            var signatureScheme = SignatureScheme.From(signatureAndHashAlgorithm);
            _useRsaPss = !_isEcdsa && SignatureScheme.IsRsaPss(signatureScheme);
        }

        /// <inheritdoc />
        public Certificate Certificate => _chain;

        /// <inheritdoc />
        public SignatureAndHashAlgorithm SignatureAndHashAlgorithm => _signatureAndHashAlgorithm;

        /// <inheritdoc />
        public TlsStreamSigner GetStreamSigner() => null;

        /// <inheritdoc />
        public byte[] GenerateRawSignature(byte[] hash)
        {
            if (hash == null) throw new ArgumentNullException(nameof(hash));

            if (_isEcdsa)
            {
                using (var ecdsa = _certificate.GetECDsaPrivateKey())
                {
                    if (ecdsa == null)
                        throw new TlsFatalAlert(AlertDescription.internal_error);

                    return ToDerSignature(ecdsa.SignHash(hash));
                }
            }

            using (var rsa = _certificate.GetRSAPrivateKey())
            {
                if (rsa == null)
                    throw new TlsFatalAlert(AlertDescription.internal_error);

                var padding = _useRsaPss ? RSASignaturePadding.Pss : RSASignaturePadding.Pkcs1;
                return rsa.SignHash(hash, _hashAlgorithm, padding);
            }
        }

        private static HashAlgorithmName MapHashAlgorithm(short hashAlgorithm)
        {
            switch (hashAlgorithm)
            {
                case Org.BouncyCastle.Tls.HashAlgorithm.md5:
                    return HashAlgorithmName.MD5;
                case Org.BouncyCastle.Tls.HashAlgorithm.sha1:
                    return HashAlgorithmName.SHA1;
                case Org.BouncyCastle.Tls.HashAlgorithm.sha224:
                    // .NET 未提供 SHA-224 的 HashAlgorithmName，退化为 SHA-256 会得到错误签名，
                    // 因此这里直接拒绝；TLS 1.2+ 的默认列表不会选到 SHA-224。
                    throw new TlsFatalAlert(AlertDescription.internal_error);
                case Org.BouncyCastle.Tls.HashAlgorithm.sha384:
                    return HashAlgorithmName.SHA384;
                case Org.BouncyCastle.Tls.HashAlgorithm.sha512:
                    return HashAlgorithmName.SHA512;
                default:
                    return HashAlgorithmName.SHA256;
            }
        }

        /// <summary>
        /// 把 .NET ECDSA 的 IEEE P1363（r||s）签名转换为 TLS 要求的 DER 编码。
        /// </summary>
        private static byte[] ToDerSignature(byte[] signature)
        {
            if (signature == null || signature.Length == 0) return signature;

            // 已经是 DER SEQUENCE 时直接返回。
            if (signature[0] == 0x30) return signature;

            if ((signature.Length & 1) != 0)
                return signature;

            var half = signature.Length / 2;
            var r = new Org.BouncyCastle.Math.BigInteger(1, signature, 0, half);
            var s = new Org.BouncyCastle.Math.BigInteger(1, signature, half, half);

            return new DerSequence(new DerInteger(r), new DerInteger(s)).GetEncoded(Asn1Encodable.Der);
        }
    }
}