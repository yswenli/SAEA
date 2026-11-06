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
*文件名： SaeaTlsServerTransport
*版本号： v26.11.5.1
*唯一标识：9272ce05-b906-41f7-862e-b736b9a60051
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/5 15:06:36
*描述：BouncyCastle TLS 服务端非阻塞传输
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
using System.Security.Cryptography.X509Certificates;

using Org.BouncyCastle.Tls;

namespace SAEA.Sockets.Ssl.Internal
{
    /// <summary>
    /// 服务端 TLS 非阻塞传输适配器。
    /// </summary>
    internal sealed class SaeaTlsServerTransport : SaeaTlsTransport
    {
        private readonly TlsServerProtocol _protocol;
        private readonly SaeaTlsServerPeer _peer;

        public SaeaTlsServerTransport(SaeaTlsOptions options)
            : base(options)
        {
            _peer = new SaeaTlsServerPeer(options, this);
            _protocol = new TlsServerProtocol();
        }

        private SecurityParameters SecurityParameters => _peer.SecurityParameters;

        /// <inheritdoc />
        public override bool IsServer => true;

        /// <inheritdoc />
        public override string SniHostName => _peer.SniHostName;

        /// <inheritdoc />
        public override string NegotiatedProtocol => SecurityParameters?.NegotiatedVersion?.Name;

        /// <inheritdoc />
        public override string CipherSuite =>
            BcKeyConverter.GetCipherSuiteName(SecurityParameters?.CipherSuite ?? 0);

        /// <inheritdoc />
        public override X509Certificate2 LocalCertificate => _peer.SelectedCertificate;

        /// <inheritdoc />
        public override X509Certificate2 RemoteCertificate
        {
            get
            {
                var certificate = base.RemoteCertificate;
                if (certificate != null) return certificate;

                var peerCertificate = SecurityParameters?.PeerCertificate;
                if (peerCertificate == null || peerCertificate.IsEmpty) return null;

                certificate = SaeaCertificateValidator.ToX509Certificate2(peerCertificate);
                if (certificate != null) SetRemoteCertificate(certificate);
                return certificate;
            }
        }

        /// <inheritdoc />
        public override void StartHandshake() => _protocol.Accept(_peer);

        /// <inheritdoc />
        public override void OfferInput(byte[] data, int offset, int count)
            => _protocol.OfferInput(data, offset, count);

        /// <inheritdoc />
        public override int AvailableInput => _protocol.GetAvailableInputBytes();

        /// <inheritdoc />
        public override int ReadInput(byte[] buffer, int offset, int count)
            => _protocol.ReadInput(buffer, offset, count);

        /// <inheritdoc />
        public override void WriteApplicationData(byte[] buffer, int offset, int count)
            => _protocol.WriteApplicationData(buffer, offset, count);

        /// <inheritdoc />
        public override int AvailableOutput => _protocol.GetAvailableOutputBytes();

        /// <inheritdoc />
        public override int ReadOutput(byte[] buffer, int offset, int count)
            => _protocol.ReadOutput(buffer, offset, count);

        /// <inheritdoc />
        public override bool IsHandshaking => _protocol.IsHandshaking;

        /// <inheritdoc />
        public override bool IsClosed => _protocol.IsClosed;

        /// <inheritdoc />
        public override void CloseInput()
        {
            try { _protocol.CloseInput(); } catch { }
        }

        /// <inheritdoc />
        public override void Close()
        {
            try { _protocol.Close(); } catch { }
        }
    }
}