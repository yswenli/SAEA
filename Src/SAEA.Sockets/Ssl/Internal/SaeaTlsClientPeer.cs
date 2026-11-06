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
*文件名： SaeaTlsClientPeer
*版本号： v26.11.5.1
*唯一标识：038b3c46-bf75-4141-ab44-fcc1577b0747
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/5 15:06:36
*描述：BouncyCastle TLS 客户端 Peer
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
    /// 客户端 TLS Peer：负责 SNI、ALPN、服务端证书校验与客户端证书（mTLS）签名凭据。
    /// </summary>
    internal sealed class SaeaTlsClientPeer : DefaultTlsClient
    {
        private readonly SaeaTlsOptions _options;

        public SaeaTlsClientPeer(SaeaTlsOptions options, SaeaTlsClientTransport transport)
            : base(new BcTlsCrypto())
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            Transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        internal SaeaTlsOptions Options => _options;

        internal SaeaTlsClientTransport Transport { get; }

        internal TlsClientContext Context => m_context;

        /// <inheritdoc />
        public override ProtocolVersion[] GetProtocolVersions()
            => SaeaTlsProtocolMapper.Map(_options.Protocols);

        /// <inheritdoc />
        protected override IList<ServerName> GetSniServerNames()
        {
            if (string.IsNullOrEmpty(_options.TargetHost))
                return null;

            return new List<ServerName>
            {
                new ServerName(NameType.host_name, Encoding.ASCII.GetBytes(_options.TargetHost))
            };
        }

        /// <inheritdoc />
        protected override IList<ProtocolName> GetProtocolNames()
            => SaeaTlsPeerHelpers.BuildProtocolNames(_options.ApplicationProtocols);

        /// <inheritdoc />
        public override TlsAuthentication GetAuthentication()
            => new SaeaTlsClientAuthentication(this);

        /// <inheritdoc />
        public override void NotifyHandshakeBeginning()
        {
            base.NotifyHandshakeBeginning();
            Transport.NoteHandshakeStarted();
        }

        /// <inheritdoc />
        public override void NotifyHandshakeComplete()
        {
            base.NotifyHandshakeComplete();
            Transport.NoteHandshakeCompleted();
        }

        /// <inheritdoc />
        public override void NotifyAlertRaised(short alertLevel, short alertDescription, string message, Exception cause)
        {
            base.NotifyAlertRaised(alertLevel, alertDescription, message, cause);

            if (alertLevel == AlertLevel.fatal)
                Transport.NoteHandshakeFailed(new SaeaTlsException(
                    $"TLS 客户端告警：{AlertDescription.GetText(alertDescription)} {message}".Trim(), cause));
        }

        /// <inheritdoc />
        public override void NotifyAlertReceived(short alertLevel, short alertDescription)
        {
            base.NotifyAlertReceived(alertLevel, alertDescription);

            if (alertLevel == AlertLevel.fatal)
                Transport.NoteHandshakeFailed(new SaeaTlsException(
                    $"收到服务端 TLS 致命告警：{AlertDescription.GetText(alertDescription)}"));
        }
    }

    /// <summary>
    /// 客户端认证：校验服务端证书、生成 mTLS 客户端凭据。
    /// </summary>
    internal sealed class SaeaTlsClientAuthentication : TlsAuthentication
    {
        private readonly SaeaTlsClientPeer _peer;

        public SaeaTlsClientAuthentication(SaeaTlsClientPeer peer)
        {
            _peer = peer ?? throw new ArgumentNullException(nameof(peer));
        }

        /// <inheritdoc />
        public void NotifyServerCertificate(TlsServerCertificate serverCertificate)
        {
            var certificate = serverCertificate?.Certificate;
            var leaf = SaeaCertificateValidator.ToX509Certificate2(certificate);

            if (leaf == null)
                throw new TlsFatalAlert(AlertDescription.bad_certificate);

            var additionalChain = SaeaCertificateValidator.ToX509Certificate2Collection(certificate);

            var errors = SaeaCertificateValidator.ValidateServerCertificate(
                leaf, additionalChain, _peer.Options.TargetHost,
                _peer.Options.CheckCertificateRevocation, out var chain);

            var callback = _peer.Options.CertificateValidationCallback;
            if (callback != null)
            {
                if (!callback(null, leaf, chain, errors))
                    throw new TlsFatalAlert(AlertDescription.bad_certificate);
            }
            else if (!_peer.Options.AllowInvalidCertificates && errors != SslPolicyErrors.None)
            {
                throw new TlsFatalAlert(AlertDescription.bad_certificate);
            }

            _peer.Transport.SetRemoteCertificate(leaf);
        }

        /// <inheritdoc />
        public TlsCredentials GetClientCredentials(CertificateRequest certificateRequest)
        {
            var certificates = _peer.Options.ClientCertificates;
            if (certificates == null || certificates.Count == 0) return null;

            X509Certificate2 selected = null;
            foreach (var item in certificates)
            {
                var candidate = item as X509Certificate2;
                if (candidate != null && candidate.HasPrivateKey)
                {
                    selected = candidate;
                    break;
                }
            }

            if (selected == null) return null;

            var context = _peer.Context;
            var crypto = (BcTlsCrypto)context.Crypto;
            var chain = BcKeyConverter.ToCertificateChain(crypto, selected,
                TlsUtilities.IsTlsV13(context)
                    ? certificateRequest?.GetCertificateRequestContext() ?? Array.Empty<byte>()
                    : null);
            var signatureAlgorithm = BcKeyConverter.GetSignatureAlgorithm(selected);

            var peerSignatureAlgorithms = certificateRequest?.SupportedSignatureAlgorithms;
            var signatureAndHash = BcKeyConverter.ChooseSignatureAndHashAlgorithm(
                context, peerSignatureAlgorithms, signatureAlgorithm);

            _peer.Transport.SetLocalCertificate(selected);

            // 直接使用证书私钥句柄签名，避免依赖私钥导出（不可导出证书同样可用）。
            return new SaeaTlsCredentialedSigner(selected, chain, signatureAndHash);
        }
    }

    /// <summary>
    /// Peer 共用辅助方法。
    /// </summary>
    internal static class SaeaTlsPeerHelpers
    {
        public static IList<ProtocolName> BuildProtocolNames(IList<string> protocols)
        {
            if (protocols == null || protocols.Count == 0) return null;

            var result = new List<ProtocolName>(protocols.Count);
            foreach (var protocol in protocols)
            {
                if (string.IsNullOrEmpty(protocol)) continue;
                result.Add(ProtocolName.AsUtf8Encoding(protocol));
            }

            return result.Count > 0 ? result : null;
        }
    }
}