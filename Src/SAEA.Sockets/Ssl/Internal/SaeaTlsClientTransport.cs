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
 *文件名： SaeaTlsClientTransport
 *描述：BouncyCastle TLS 客户端非阻塞传输
 *
 *****************************************************************************/
using System;
using System.Security.Cryptography.X509Certificates;

using Org.BouncyCastle.Tls;

namespace SAEA.Sockets.Ssl.Internal
{
    /// <summary>
    /// 客户端 TLS 非阻塞传输适配器。
    /// </summary>
    internal sealed class SaeaTlsClientTransport : SaeaTlsTransport
    {
        private readonly TlsClientProtocol _protocol;
        private readonly SaeaTlsClientPeer _peer;

        private X509Certificate2 _localCertificate;

        public SaeaTlsClientTransport(SaeaTlsOptions options)
            : base(options)
        {
            _peer = new SaeaTlsClientPeer(options, this);
            _protocol = new TlsClientProtocol();
        }

        private SecurityParameters SecurityParameters => _peer.Context?.SecurityParameters;

        /// <inheritdoc />
        public override bool IsServer => false;

        /// <inheritdoc />
        public override string SniHostName => _peer.Options.TargetHost;

        /// <inheritdoc />
        public override string NegotiatedProtocol => SecurityParameters?.NegotiatedVersion?.Name;

        /// <inheritdoc />
        public override string CipherSuite =>
            BcKeyConverter.GetCipherSuiteName(SecurityParameters?.CipherSuite ?? 0);

        /// <inheritdoc />
        public override X509Certificate2 LocalCertificate => _localCertificate;

        /// <summary>记录本次连接实际使用的客户端证书。</summary>
        internal void SetLocalCertificate(X509Certificate2 certificate)
        {
            _localCertificate = certificate;
        }

        /// <inheritdoc />
        public override void StartHandshake() => _protocol.Connect(_peer);

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