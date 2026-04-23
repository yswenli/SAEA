/****************************************************************************
 * 
   ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MQTT.Implementations
*文件名： MqttTcpServerListener
*版本号： v26.4.23.1
*唯一标识：f32f3d4c-041a-4b23-8ba5-a4781f874b56
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using SAEA.MQTT.Adapter;
using SAEA.MQTT.Diagnostics;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Internal;
using SAEA.MQTT.Server;
using SAEA.Sockets;
using SAEA.Sockets.Model;
using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.Implementations
{
    public sealed class MqttTcpServerListener : IDisposable
    {
        readonly IMqttNetScopedLogger _logger;
        readonly IMqttNetLogger _rootLogger;
        readonly AddressFamily _addressFamily;
        readonly MqttServerTcpEndpointBaseOptions _options;
        readonly MqttServerTlsTcpEndpointOptions _tlsOptions;
        readonly X509Certificate2 _tlsCertificate;

        //CrossPlatformSocket _socket;
        IPEndPoint _localEndPoint;

        IServerSocket _serverSokcet;

        public MqttTcpServerListener(
            AddressFamily addressFamily,
            MqttServerTcpEndpointBaseOptions options,
            X509Certificate2 tlsCertificate,
            IMqttNetLogger logger)
        {
            _addressFamily = addressFamily;
            _options = options;
            _tlsCertificate = tlsCertificate;
            _rootLogger = logger;
            _logger = logger.CreateScopedLogger(nameof(MqttTcpServerListener));

            if (_options is MqttServerTlsTcpEndpointOptions tlsOptions)
            {
                _tlsOptions = tlsOptions;
            }
        }

        public Func<IMqttChannelAdapter, Task> ClientHandler { get; set; }

        public bool Start(bool treatErrorsAsWarning, CancellationToken cancellationToken)
        {
            try
            {
                var builder = SocketOptionBuilder.Instance;

                var boundIp = _options.BoundInterNetworkAddress;

                if (_addressFamily == AddressFamily.InterNetworkV6)
                {
                    builder = builder.UseIPv6();

                    boundIp = _options.BoundInterNetworkV6Address;
                }

                _localEndPoint = new IPEndPoint(boundIp, _options.Port);

                _logger.Info($"Starting TCP listener for {_localEndPoint} TLS={_tlsCertificate != null}.");

                builder = builder.ReusePort(_options.ReuseAddress);

                if (!_options.NoDelay)
                {
                    builder = builder.SetDelay();
                }

                if (_tlsCertificate != null)
                {
                    builder = builder.WithSsl(_tlsCertificate, System.Security.Authentication.SslProtocols.Tls12);
                }

                _serverSokcet = SocketFactory.CreateServerSocket(builder.UseStream().SetIPEndPoint(_localEndPoint).Build());

                _serverSokcet.OnAccepted += _serverSokcet_OnAccepted;

                _serverSokcet.Start(_options.ConnectionBacklog);

                return true;
            }
            catch (Exception exception)
            {
                if (!treatErrorsAsWarning)
                {
                    throw;
                }

                _logger.Warning(exception, "Error while creating listener socket for local end point '{0}'.", _localEndPoint);
                return false;
            }
        }


        private void _serverSokcet_OnAccepted(object obj)
        {
            Task.Run(() =>
            {
                var ci = (ChannelInfo)obj;

                Stream stream = ci.Stream;

                string remoteEndPoint = ci.ID;

                X509Certificate2 clientCertificate = null;

                if (_tlsCertificate != null)
                {
                    SslStream sslStream = (SslStream)stream;

                    clientCertificate = sslStream.RemoteCertificate as X509Certificate2;

                    if (clientCertificate == null && sslStream.RemoteCertificate != null)
                    {
                        clientCertificate = new X509Certificate2(sslStream.RemoteCertificate.Export(X509ContentType.Cert));
                    }
                }

                var clientHandler = ClientHandler;

                if (clientHandler != null)
                {
                    using (var clientAdapter = new MqttChannelAdapter(new MqttTcpChannel(stream, remoteEndPoint, clientCertificate), new MqttPacketFormatterAdapter(new MqttPacketWriter()), _rootLogger))
                    {
                        clientHandler(clientAdapter).ConfigureAwait(false).GetAwaiter().GetResult();
                    }
                }

            });

        }

        public void Dispose()
        {
            _serverSokcet?.Dispose();

            _tlsCertificate?.Dispose();

        }
    }
}