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
*文件名： SaeaTlsServerPeer
*版本号： v26.11.5.1
*唯一标识：5830c721-98c8-4a20-a68d-8786929b3436
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/5 15:06:36
*描述：BouncyCastle TLS 服务端 Peer
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
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;

namespace SAEA.Sockets.Ssl.Internal
{
    /// <summary>
    /// 服务端 TLS Peer：负责 SNI 证书选择、签名凭据生成与客户端证书校验。
    /// </summary>
    internal sealed class SaeaTlsServerPeer : DefaultTlsServer
    {
        private readonly SaeaTlsOptions _options;
        private readonly SaeaTlsServerTransport _transport;

        private X509Certificate2 _selectedCertificate;
        private string _sniHostName;

        public SaeaTlsServerPeer(SaeaTlsOptions options, SaeaTlsServerTransport transport)
            : base(new BcTlsCrypto())
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        /// <summary>SNI 主机名。</summary>
        public string SniHostName => _sniHostName;

        /// <summary>当前连接实际使用的证书。</summary>
        public X509Certificate2 SelectedCertificate => _selectedCertificate ?? _options.ServerCertificate;

        /// <summary>协商出的安全参数。</summary>
        public SecurityParameters SecurityParameters => m_context?.SecurityParameters;

        /// <inheritdoc />
        public override ProtocolVersion[] GetProtocolVersions()
            => SaeaTlsProtocolMapper.Map(_options.Protocols);

        /// <inheritdoc />
        public override void ProcessClientExtensions(IDictionary<int, byte[]> clientExtensions)
        {
            base.ProcessClientExtensions(clientExtensions);

            var names = TlsExtensionsUtilities.GetServerNameExtensionClient(clientExtensions);
            if (names != null)
            {
                foreach (var name in names)
                {
                    if (name != null && name.NameType == NameType.host_name)
                    {
                        _sniHostName = Encoding.ASCII.GetString(name.NameData);
                        break;
                    }
                }
            }

            try
            {
                _selectedCertificate = _options.ServerCertificateSelector?.Invoke(_sniHostName)
                                       ?? _options.ServerCertificate;
            }
            catch
            {
                _selectedCertificate = _options.ServerCertificate;
            }

            if (_selectedCertificate == null)
                throw new TlsFatalAlert(AlertDescription.unrecognized_name);
        }

        /// <inheritdoc />
        protected override int[] GetSupportedCipherSuites()
        {
            var suites = base.GetSupportedCipherSuites();
            var certificate = _selectedCertificate ?? _options.ServerCertificate;
            if (certificate == null || suites == null) return suites;

            var ecdsa = BcKeyConverter.IsEcdsaCertificate(certificate);
            var filtered = new List<int>(suites.Length);

            foreach (var suite in suites)
            {
                var keyExchange = TlsUtilities.GetKeyExchangeAlgorithm(suite);

                // TLS 1.3 套件与证书类型无关，签名算法在握手时选择。
                if (keyExchange == KeyExchangeAlgorithm.NULL)
                {
                    filtered.Add(suite);
                    continue;
                }

                if (ecdsa)
                {
                    if (keyExchange == KeyExchangeAlgorithm.ECDHE_ECDSA ||
                        keyExchange == KeyExchangeAlgorithm.ECDH_ECDSA)
                        filtered.Add(suite);
                }
                else
                {
                    if (keyExchange == KeyExchangeAlgorithm.RSA ||
                        keyExchange == KeyExchangeAlgorithm.RSA_PSK ||
                        keyExchange == KeyExchangeAlgorithm.DHE_RSA ||
                        keyExchange == KeyExchangeAlgorithm.ECDHE_RSA ||
                        keyExchange == KeyExchangeAlgorithm.DH_RSA ||
                        keyExchange == KeyExchangeAlgorithm.ECDH_RSA)
                        filtered.Add(suite);
                }
            }

            return filtered.Count > 0 ? filtered.ToArray() : suites;
        }

        /// <inheritdoc />
        public override TlsCredentials GetCredentials()
        {
            var certificate = SelectedCertificate;
            if (certificate == null)
                throw new TlsFatalAlert(AlertDescription.internal_error);

            // TLS 1.3 没有传统密钥交换分支，必须显式给出签名凭据。
            if (TlsUtilities.IsTlsV13(m_context))
                return BuildSigner(certificate);

            return base.GetCredentials();
        }

        /// <inheritdoc />
        protected override TlsCredentialedSigner GetRsaSignerCredentials()
            => BuildSigner(SelectedCertificate);

        /// <inheritdoc />
        protected override TlsCredentialedSigner GetECDsaSignerCredentials()
            => BuildSigner(SelectedCertificate);

        /// <inheritdoc />
        protected override TlsCredentialedSigner GetDsaSignerCredentials()
            => BuildSigner(SelectedCertificate);

        /// <inheritdoc />
        public override CertificateRequest GetCertificateRequest()
        {
            if (!_options.ClientCertificateRequired)
                return null;

            var signatureAlgorithms = TlsUtilities.GetDefaultSupportedSignatureAlgorithms(m_context);

            // TLS 1.3 的 CertificateRequest 使用空（但非 null）的 certificate_request_context，
            // 否则 BC 会在 Send13ServerHelloCoda 中抛 internal_error。
            if (TlsUtilities.IsTlsV13(m_context))
                return new CertificateRequest(Array.Empty<byte>(), signatureAlgorithms, null, null);

            return new CertificateRequest(
                new[]
                {
                    ClientCertificateType.rsa_sign,
                    ClientCertificateType.ecdsa_sign,
                    ClientCertificateType.dss_sign
                },
                signatureAlgorithms,
                null);
        }

        /// <inheritdoc />
        public override void NotifyClientCertificate(Certificate clientCertificate)
        {
            var empty = clientCertificate == null || clientCertificate.IsEmpty;

            if (empty)
            {
                if (_options.ClientCertificateRequired)
                    throw new TlsFatalAlert(AlertDescription.bad_certificate);
                return;
            }

            var leaf = SaeaCertificateValidator.ToX509Certificate2(clientCertificate);
            var additionalChain = SaeaCertificateValidator.ToX509Certificate2Collection(clientCertificate);

            // mTLS 场景默认不强制要求受信根，便于内网自签名部署；生产应显式配置回调。
            var errors = SaeaCertificateValidator.ValidateChain(
                leaf, additionalChain, _options.CheckCertificateRevocation, true, out var chain);

            var callback = _options.CertificateValidationCallback;
            if (callback != null)
            {
                if (!callback(null, leaf, chain, errors))
                    throw new TlsFatalAlert(AlertDescription.bad_certificate);
            }
            else if (!_options.AllowInvalidCertificates && errors != SslPolicyErrors.None)
            {
                throw new TlsFatalAlert(AlertDescription.bad_certificate);
            }

            _transport.SetRemoteCertificate(leaf);
        }

        /// <inheritdoc />
        public override void NotifyHandshakeBeginning()
        {
            base.NotifyHandshakeBeginning();
            _transport.NoteHandshakeStarted();
        }

        /// <inheritdoc />
        public override void NotifyHandshakeComplete()
        {
            base.NotifyHandshakeComplete();
            _transport.NoteHandshakeCompleted();
        }

        /// <inheritdoc />
        public override void NotifyAlertRaised(short alertLevel, short alertDescription, string message, Exception cause)
        {
            base.NotifyAlertRaised(alertLevel, alertDescription, message, cause);

            if (alertLevel == AlertLevel.fatal)
                _transport.NoteHandshakeFailed(new SaeaTlsException(
                    $"TLS 服务端告警：{AlertDescription.GetText(alertDescription)} {message}".Trim(), cause));
        }

        /// <inheritdoc />
        public override void NotifyAlertReceived(short alertLevel, short alertDescription)
        {
            base.NotifyAlertReceived(alertLevel, alertDescription);

            if (alertLevel == AlertLevel.fatal)
                _transport.NoteHandshakeFailed(new SaeaTlsException(
                    $"收到客户端 TLS 致命告警：{AlertDescription.GetText(alertDescription)}"));
        }

        private TlsCredentialedSigner BuildSigner(X509Certificate2 certificate)
        {
            if (certificate == null)
                throw new TlsFatalAlert(AlertDescription.internal_error);

            var crypto = (BcTlsCrypto)m_context.Crypto;
            var chain = BcKeyConverter.ToCertificateChain(crypto, certificate,
                TlsUtilities.IsTlsV13(m_context) ? Array.Empty<byte>() : null);
            var signatureAlgorithm = BcKeyConverter.GetSignatureAlgorithm(certificate);

            var peerSignatureAlgorithms = m_context.SecurityParameters?.ClientSigAlgs;
            var signatureAndHash = BcKeyConverter.ChooseSignatureAndHashAlgorithm(
                m_context, peerSignatureAlgorithms, signatureAlgorithm);

            // 直接使用证书私钥句柄签名，避免依赖私钥导出（不可导出证书同样可用）。
            return new SaeaTlsCredentialedSigner(certificate, chain, signatureAndHash);
        }
    }
}