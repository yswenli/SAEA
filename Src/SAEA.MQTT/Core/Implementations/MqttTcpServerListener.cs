/****************************************************************************
*项目名称：SAEA.MQTT
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Core.Implementations
*类 名 称：MqttWebSocketChannel
*版 本 号： v4.3.1.2
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/14 19:07:44
*描述：
*=====================================================================
*修改时间：2019/1/14 19:07:44
*修 改 人： yswenli
*版 本 号： v4.3.1.2
*描    述：
*****************************************************************************/
using SAEA.MQTT.Common.Log;
using SAEA.MQTT.Common.Serializer;
using SAEA.MQTT.Event;
using SAEA.MQTT.Model;
using SAEA.Sockets;
using SAEA.Sockets.Core;
using SAEA.Sockets.Interface;
using System;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace SAEA.MQTT.Core.Implementations
{
    public class MqttTcpServerListener : IDisposable
    {
        ISocketOption socketOption;

        IServerSokcet serverSokcet;

        AddressFamily _addressFamily;

        CancellationToken _cancellationToken;

        IMqttNetChildLogger _logger;

        public MqttTcpServerListener(
            AddressFamily addressFamily,
            MqttServerTcpEndpointBaseOptions options,
            X509Certificate2 tlsCertificate,
            CancellationToken cancellationToken,
            IMqttNetChildLogger logger)
        {

            _cancellationToken = cancellationToken;
            _logger = logger;
            _addressFamily = addressFamily;

            var sb = new SocketOptionBuilder().SetSocket(Sockets.Model.SocketType.Tcp).UseStream();

            if (options is MqttServerTlsTcpEndpointOptions tlsOptions)
            {
                sb = sb.WithSsl(tlsCertificate, tlsOptions.SslProtocol);
            }

            sb = sb.SetPort(options.Port);

            if (_addressFamily == AddressFamily.InterNetworkV6)
            {
                sb = sb.UseIPV6();
            }

            socketOption = sb.Build();

            serverSokcet = SocketFactory.CreateServerSocket(socketOption, cancellationToken);

            serverSokcet.OnAccepted += ServerSokcet_OnAccepted;
        }

        private void ServerSokcet_OnAccepted(object userToken)
        {
            var id = userToken.ToString();

            var ci = ChannelManager.Current.Get(id);

            if (ci != null)
            {
                var clientAdapter = new MqttChannelAdapter(new MqttTcpChannel(ci.ClientSocket, ci.Stream), new MqttPacketSerializer(), _logger);

                ClientAccepted?.Invoke(this, new MqttServerAdapterClientAcceptedEventArgs(clientAdapter));
            }
        }

        public event EventHandler<MqttServerAdapterClientAcceptedEventArgs> ClientAccepted;

        public void Start()
        {
            serverSokcet.Start();
        }


        public void Dispose()
        {
            ChannelManager.Current.Clear();
            serverSokcet?.Dispose();
        }
    }
}