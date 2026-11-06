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
*唯一标识：317c38ee-0cef-431a-9d9d-61e6241d0159
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttTcpServerListener接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
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
    /// <summary>
    /// 单个 TCP 监听端点，绑定指定地址族与端口，接受连接后交给回调处理
    /// </summary>
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

        /// <summary>
        /// 创建监听端点，此时尚未绑定端口
        /// </summary>
        /// <param name="addressFamily">监听的地址族，IPv6 时使用选项中的 IPv6 绑定地址</param>
        /// <param name="options">端点选项，提供绑定地址、端口与收发参数</param>
        /// <param name="tlsCertificate">TLS 服务端证书，为null表示明文监听</param>
        /// <param name="logger">根日志记录器，用于创建该端点的子日志记录器</param>
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

        /// <summary>
        /// 新连接接入时的处理回调，为null时连接被直接丢弃
        /// </summary>
        public Func<IMqttChannelAdapter, Task> ClientHandler { get; set; }

        /// <summary>
        /// 绑定端口并开始接受连接
        /// </summary>
        /// <param name="treatErrorsAsWarning">为 true 时启动失败只记录警告并返回 false，为 false 时抛出异常</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>启动是否成功</returns>
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
                    builder = builder.WithSsl(o =>
                    {
                        o.ServerCertificate = _tlsCertificate;
                        o.Protocols = System.Security.Authentication.SslProtocols.Tls12;
                    });
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
                    // TLS 由 SAEA.Sockets 在底层完成，客户端证书从连接 TLS 上下文读取。
                    clientCertificate = ci.Tls?.RemoteCertificate;
                }

                var clientHandler = ClientHandler;

                if (clientHandler != null)
                {
                    using (var clientAdapter = new MqttChannelAdapter(new MqttTcpChannel(stream, remoteEndPoint, clientCertificate, ci.Tls != null), new MqttPacketFormatterAdapter(new MqttPacketWriter()), _rootLogger))
                    {
                        clientHandler(clientAdapter).ConfigureAwait(false).GetAwaiter().GetResult();
                    }
                }

            });

        }

        /// <summary>
        /// 释放监听套接字与持有的 TLS 证书
        /// </summary>
        public void Dispose()
        {
            _serverSokcet?.Dispose();

            _tlsCertificate?.Dispose();

        }
    }
}