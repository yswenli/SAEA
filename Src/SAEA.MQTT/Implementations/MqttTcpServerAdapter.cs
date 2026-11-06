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
*命名空间：SAEA.MQTT.Implementations
*文件名： MqttTcpServerAdapter
*版本号： v26.11.5.1
*唯一标识：4d35e530-73f4-4d11-90ec-bb5974c94115
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 12:04:50
*描述：基于 TCP 的 MQTT 服务端适配器
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:50
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
#if !WINDOWS_UWP
using SAEA.MQTT.Adapter;
using SAEA.MQTT.Diagnostics;
using SAEA.MQTT.Server;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.Implementations
{
    /// <summary>
    /// 基于 TCP 的服务端适配器，按端点选项启动 IPv4/IPv6 监听器并把接入连接交给回调处理
    /// </summary>
    public sealed class MqttTcpServerAdapter : IMqttServerAdapter
    {
        readonly List<MqttTcpServerListener> _listeners = new List<MqttTcpServerListener>();
        readonly IMqttNetScopedLogger _logger;
        readonly IMqttNetLogger _rootLogger;

        CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// 使用指定日志记录器创建服务端适配器
        /// </summary>
        /// <param name="logger">根日志记录器，不能为null</param>
        public MqttTcpServerAdapter(IMqttNetLogger logger)
        {
            _rootLogger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger = logger.CreateScopedLogger(nameof(MqttTcpServerAdapter));
        }

        /// <summary>
        /// 新连接接入时的处理回调，为null时连接被直接丢弃
        /// </summary>
        public Func<IMqttChannelAdapter, Task> ClientHandler { get; set; }

        /// <summary>
        /// 监听端口创建失败时是否只记录警告而不抛出异常，默认 false
        /// </summary>
        public bool TreatSocketOpeningErrorAsWarning { get; set; }

        /// <summary>
        /// 按服务端选项启动监听，已启动时抛 InvalidOperationException，TLS 端点缺证书或无私钥时抛异常
        /// </summary>
        /// <param name="options">服务端选项，决定是否启用明文端点与 TLS 端点</param>
        /// <returns>已完成的异步任务，监听器在返回前已同步启动</returns>
        public Task StartAsync(IMqttServerOptions options)
        {
            if (_cancellationTokenSource != null) throw new InvalidOperationException("Server is already started.");

            _cancellationTokenSource = new CancellationTokenSource();

            if (options.DefaultEndpointOptions.IsEnabled)
            {
                RegisterListeners(options.DefaultEndpointOptions, null, _cancellationTokenSource.Token);
            }

            if (options.TlsEndpointOptions?.IsEnabled == true)
            {
                if (options.TlsEndpointOptions.CertificateProvider == null)
                {
                    throw new ArgumentException("TLS certificate is not set.");
                }

                var tlsCertificate = options.TlsEndpointOptions.CertificateProvider.GetCertificate();
                if (!tlsCertificate.HasPrivateKey)
                {
                    throw new InvalidOperationException("The certificate for TLS encryption must contain the private key.");
                }

                RegisterListeners(options.TlsEndpointOptions, tlsCertificate, _cancellationTokenSource.Token);
            }

            return PlatformAbstractionLayer.CompletedTask;
        }

        /// <summary>
        /// 停止监听并释放所有监听器
        /// </summary>
        /// <returns>已完成的异步任务</returns>
        public Task StopAsync()
        {
            Cleanup();
            return PlatformAbstractionLayer.CompletedTask;
        }

        /// <summary>
        /// 释放资源，行为与 StopAsync 相同
        /// </summary>
        public void Dispose()
        {
            Cleanup();
        }

        void Cleanup()
        {
            try
            {
                _cancellationTokenSource?.Cancel(false);
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;

                foreach (var listener in _listeners)
                {
                    listener.Dispose();
                }

                _listeners.Clear();
            }
        }

        void RegisterListeners(MqttServerTcpEndpointBaseOptions options, X509Certificate2 tlsCertificate, CancellationToken cancellationToken)
        {
            if (!options.BoundInterNetworkAddress.Equals(IPAddress.None))
            {
                var listenerV4 = new MqttTcpServerListener(AddressFamily.InterNetwork, options, tlsCertificate, _rootLogger)
                {
                    ClientHandler = OnClientAcceptedAsync
                };

                if (listenerV4.Start(TreatSocketOpeningErrorAsWarning, cancellationToken))
                {
                    _listeners.Add(listenerV4);
                }
            }

            if (!options.BoundInterNetworkV6Address.Equals(IPAddress.None))
            {
                var listenerV6 = new MqttTcpServerListener(AddressFamily.InterNetworkV6, options, tlsCertificate, _rootLogger)
                {
                    ClientHandler = OnClientAcceptedAsync
                };

                if (listenerV6.Start(TreatSocketOpeningErrorAsWarning, cancellationToken))
                {
                    _listeners.Add(listenerV6);
                }
            }
        }

        Task OnClientAcceptedAsync(IMqttChannelAdapter channelAdapter)
        {
            var clientHandler = ClientHandler;
            if (clientHandler == null)
            {
                return Task.FromResult(0);
            }

            return clientHandler(channelAdapter);
        }
    }
}
#endif