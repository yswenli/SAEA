/****************************************************************************
*项目名称：SAEA.MQTT
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Core.Implementations
*类 名 称：MqttTcpChannel
*版 本 号： v5.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/14 19:07:44
*描述：
*=====================================================================
*修改时间：2019/1/14 19:07:44
*修 改 人： yswenli
*版 本 号： v5.0.0.1
*描    述：
*****************************************************************************/

using SAEA.MQTT.Exceptions;
using SAEA.MQTT.Interface;
using SAEA.MQTT.Model;
using SAEA.Sockets;
using SAEA.Sockets.Core;
using SAEA.Sockets.Core.Tcp;
using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.Core.Implementations
{
    public class MqttTcpChannel : IMqttChannel
    {
        private readonly IMqttClientOptions _clientOptions;

        private readonly MqttClientTcpOptions _options;


        IClientSocket _clientSocket;


        public MqttTcpChannel(IMqttClientOptions clientOptions)
        {
            _clientOptions = clientOptions ?? throw new ArgumentNullException(nameof(clientOptions));
            _options = (MqttClientTcpOptions)clientOptions.ChannelOptions;

            var builder = new SocketOptionBuilder()
                .UseStream()
                .SetIP(_options.Server)
                .SetPort(_options.Port ?? 1883);

            if (_options.TlsOptions.UseTls)
            {
                builder = builder.WithSsl(_options.TlsOptions.SslProtocol);
            }

            var option = builder.Build();

            _clientSocket = SocketFactory.CreateClientSocket(option);
        }



        public MqttTcpChannel(Socket socket, Stream sslStream)
        {
            _clientSocket = new StreamClientSocket(socket, sslStream);
        }

        public string Endpoint
        {
            get
            {
                if (_clientSocket.Connected)

                    return _clientSocket.Endpoint;

                return string.Empty;
            }
        }

        public void ConnectAsync()
        {
            _clientSocket.ConnectAsync();
        }

        public Task DisconnectAsync()
        {
            Dispose();
            return Task.FromResult(0);
        }

        public Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ChannelManager.Instance.Refresh(_clientSocket.Endpoint);
            return _clientSocket.ReceiveAsync(buffer, offset, count, cancellationToken);
        }

        public Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            while (!_clientSocket.Connected)
            {
                Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(true);

                if (cancellationToken.IsCancellationRequested) throw new MqttCommunicationTimedOutException();
            }

            ChannelManager.Instance.Refresh(_clientSocket.Endpoint);

            return _clientSocket.SendAsync(buffer, offset, count, cancellationToken);
        }

        public void Dispose()
        {
            ChannelManager.Instance.Remove(_clientSocket.Endpoint);
            _clientSocket.Dispose();
        }


        private X509CertificateCollection LoadCertificates()
        {
            var certificates = new X509CertificateCollection();
            if (_options.TlsOptions.Certificates == null)
            {
                return certificates;
            }

            foreach (var certificate in _options.TlsOptions.Certificates)
            {
                certificates.Add(new X509Certificate2(certificate));
            }

            return certificates;
        }

        Task IMqttChannel.ConnectAsync()
        {
            return Task.Run(() => ConnectAsync());
        }
    }
}
