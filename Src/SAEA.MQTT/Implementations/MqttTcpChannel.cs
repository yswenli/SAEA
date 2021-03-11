#if !WINDOWS_UWP
using SAEA.MQTT.Channel;
using SAEA.MQTT.Client.Options;
using System;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using SAEA.MQTT.Exceptions;
using SAEA.Sockets;
using System.Net;
using SAEA.Sockets.Core.Tcp;

namespace SAEA.MQTT.Implementations
{
    public sealed class MqttTcpChannel : IMqttChannel
    {
        readonly IMqttClientOptions _clientOptions;
        readonly MqttClientTcpOptions _options;

        Stream _stream;

        public MqttTcpChannel(IMqttClientOptions clientOptions)
        {
            _clientOptions = clientOptions ?? throw new ArgumentNullException(nameof(clientOptions));
            _options = (MqttClientTcpOptions)clientOptions.ChannelOptions;

            IsSecureConnection = clientOptions.ChannelOptions?.TlsOptions?.UseTls == true;
        }

        public MqttTcpChannel(Stream stream, string endpoint, X509Certificate2 clientCertificate)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));

            Endpoint = endpoint;

            IsSecureConnection = stream is SslStream;

            ClientCertificate = clientCertificate;
        }

        public string Endpoint { get; private set; }

        public bool IsSecureConnection { get; }

        public X509Certificate2 ClientCertificate { get; }

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            IClientSocket client = null;
            try
            {
                SocketOptionBuilder sb = SocketOptionBuilder.Instance;
                sb = sb.SetReadBufferSize(_options.BufferSize);
                sb = sb.SetWriteBufferSize(_options.BufferSize);
                if (!_options.NoDelay)
                    sb = sb.SetDelay();

                IPAddress ip;

                string ipStr = _options.Server;

                if (ipStr.IndexOf("localhost") > -1)
                {
                    ip = IPAddress.Parse("127.0.0.1");
                }
                else
                {
                    ip = IPAddress.Parse(ipStr);
                }

                if (_options.AddressFamily == AddressFamily.InterNetworkV6 || ip.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    sb = sb.UseIPv6();
                }
                sb = sb.SetIPEndPoint(new IPEndPoint(ip, _options.GetPort()));

                client = SocketFactory.CreateClientSocket(sb.Build());

                _stream = await ((StreamClientSocket)client).ConnectAsync(InternalUserCertificateValidationCallback, LoadCertificates, !_options.TlsOptions.IgnoreCertificateRevocationErrors);

                Endpoint = client.Endpoint;
            }
            catch (Exception)
            {
                client?.Dispose();
                throw;
            }
        }

        public Task DisconnectAsync(CancellationToken cancellationToken)
        {
            Dispose();
            return Task.FromResult(0);
        }

        public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer is null) throw new ArgumentNullException(nameof(buffer));

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var stream = _stream;

                if (stream == null)
                {
                    return 0;
                }

                if (!stream.CanRead)
                {
                    return 0;
                }

                // Workaround for: https://github.com/dotnet/corefx/issues/24430
                using (cancellationToken.Register(Dispose))
                {
                    return await stream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (ObjectDisposedException)
            {
                // Indicate a graceful socket close.
                return 0;
            }
            catch (IOException exception)
            {
                if (exception.InnerException is SocketException socketException)
                {
                    ExceptionDispatchInfo.Capture(socketException).Throw();
                }

                throw;
            }
        }

        public async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer is null) throw new ArgumentNullException(nameof(buffer));

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Workaround for: https://github.com/dotnet/corefx/issues/24430
                using (cancellationToken.Register(Dispose))
                {
                    var stream = _stream;

                    if (stream == null)
                    {
                        throw new MqttCommunicationException("The TCP connection is closed.");
                    }

                    await stream.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (ObjectDisposedException)
            {
                throw new MqttCommunicationException("The TCP connection is closed.");
            }
            catch (IOException exception)
            {
                if (exception.InnerException is SocketException socketException)
                {
                    ExceptionDispatchInfo.Capture(socketException).Throw();
                }

                throw;
            }
        }

        public void Dispose()
        {
            // When the stream is disposed it will also close the socket and this will also dispose it.
            // So there is no need to dispose the socket again.
            // https://stackoverflow.com/questions/3601521/should-i-manually-dispose-the-socket-after-closing-it
            try
            {
                _stream?.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (NullReferenceException)
            {
            }

            _stream = null;
        }

        bool InternalUserCertificateValidationCallback(object sender, X509Certificate x509Certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            #region OBSOLETE

#pragma warning disable CS0618 // Type or member is obsolete
            var certificateValidationCallback = _options?.TlsOptions?.CertificateValidationCallback;
#pragma warning restore CS0618 // Type or member is obsolete
            if (certificateValidationCallback != null)
            {
                return certificateValidationCallback(x509Certificate, chain, sslPolicyErrors, _clientOptions);
            }
            #endregion

            var certificateValidationHandler = _options?.TlsOptions?.CertificateValidationHandler;
            if (certificateValidationHandler != null)
            {
                var context = new MqttClientCertificateValidationCallbackContext
                {
                    Certificate = x509Certificate,
                    Chain = chain,
                    SslPolicyErrors = sslPolicyErrors,
                    ClientOptions = _options
                };

                return certificateValidationHandler(context);
            }

            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            if (chain.ChainStatus.Any(c => c.Status == X509ChainStatusFlags.RevocationStatusUnknown || c.Status == X509ChainStatusFlags.Revoked || c.Status == X509ChainStatusFlags.OfflineRevocation))
            {
                if (_options?.TlsOptions?.IgnoreCertificateRevocationErrors != true)
                {
                    return false;
                }
            }

            if (chain.ChainStatus.Any(c => c.Status == X509ChainStatusFlags.PartialChain))
            {
                if (_options?.TlsOptions?.IgnoreCertificateChainErrors != true)
                {
                    return false;
                }
            }

            return _options?.TlsOptions?.AllowUntrustedCertificates == true;
        }

        X509CertificateCollection LoadCertificates()
        {
            var certificates = new X509CertificateCollection();
            if (_options.TlsOptions.Certificates == null)
            {
                return certificates;
            }

            foreach (var certificate in _options.TlsOptions.Certificates)
            {
                certificates.Add(certificate);
            }

            return certificates;
        }
    }
}
#endif
