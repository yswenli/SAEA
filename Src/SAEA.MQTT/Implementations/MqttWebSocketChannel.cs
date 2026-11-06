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
*文件名： MqttWebSocketChannel
*版本号： v26.4.23.1
*唯一标识：9c14ce6c-4173-43a5-9b6c-900463029c08
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttWebSocketChannel通道类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.MQTT.Channel;
using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Internal;
using System;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.Implementations
{
    /// <summary>
    /// 基于 WebSocket 的 MQTT 信道，以二进制帧承载 MQTT 报文
    /// </summary>
    public sealed class MqttWebSocketChannel : IMqttChannel
    {
        readonly MqttClientWebSocketOptions _options;

        AsyncLock _sendLock = new AsyncLock();
        WebSocket _webSocket;

        /// <summary>
        /// 以客户端 WebSocket 选项创建信道，创建时尚未建立连接
        /// </summary>
        /// <param name="options">客户端 WebSocket 选项，不能为null</param>
        public MqttWebSocketChannel(MqttClientWebSocketOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// 以已建立的 WebSocket 创建信道，用于服务端接管连接
        /// </summary>
        /// <param name="webSocket">已建立的 WebSocket，不能为null</param>
        /// <param name="endpoint">对端地址描述</param>
        /// <param name="isSecureConnection">是否为加密连接</param>
        /// <param name="clientCertificate">客户端证书，无证书时为null</param>
        public MqttWebSocketChannel(WebSocket webSocket, string endpoint, bool isSecureConnection, X509Certificate2 clientCertificate)
        {
            _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));

            Endpoint = endpoint;
            IsSecureConnection = isSecureConnection;
            ClientCertificate = clientCertificate;
        }

        /// <summary>
        /// 通道的对端地址，仅由服务端接管连接时设置，客户端信道恒为null
        /// </summary>
        public string Endpoint { get; }

        /// <summary>
        /// 是否为加密连接，客户端侧按连接后的 wss 前缀判定
        /// </summary>
        public bool IsSecureConnection { get; private set; }

        /// <summary>
        /// 客户端证书，仅由服务端接管连接时设置，否则为null
        /// </summary>
        public X509Certificate2 ClientCertificate { get; }

        /// <summary>
        /// 建立 WebSocket 连接，URI 缺少 ws/wss 前缀时按 TLS 选项补齐
        /// </summary>
        /// <param name="cancellationToken">取消令牌，取消时中止连接并释放客户端套接字</param>
        /// <returns>连接完成的异步任务</returns>
        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            var uri = _options.Uri;
            if (!uri.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) && !uri.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
            {
                if (_options.TlsOptions?.UseTls == false)
                {
                    uri = "ws://" + uri;
                }
                else
                {
                    uri = "wss://" + uri;
                }
            }

            var clientWebSocket = new ClientWebSocket();
            try
            {
                SetupClientWebSocket(clientWebSocket);

                await clientWebSocket.ConnectAsync(new Uri(uri), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Prevent a memory leak when always creating new instance which will fail while connecting.
                clientWebSocket.Dispose();
                throw;
            }

            _webSocket = clientWebSocket;
            IsSecureConnection = uri.StartsWith("wss://", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 正常关闭 WebSocket 并释放发送锁，未连接时直接返回
        /// </summary>
        /// <param name="cancellationToken">取消令牌，取消时不等待关闭帧返回</param>
        /// <returns>断开完成的异步任务</returns>
        public async Task DisconnectAsync(CancellationToken cancellationToken)
        {
            if (_webSocket == null)
            {
                return;
            }

            if (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.Connecting)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, cancellationToken).ConfigureAwait(false);
            }

            Cleanup();
        }

        /// <summary>
        /// 接收一帧 WebSocket 数据，单次读取不超过请求的字节数
        /// </summary>
        /// <param name="buffer">接收缓冲区</param>
        /// <param name="offset">写入缓冲区的起始偏移</param>
        /// <param name="count">本次最多读取的字节数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>本帧实际读取的字节数</returns>
        public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var response = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, count), cancellationToken).ConfigureAwait(false);
            return response.Count;
        }

        /// <summary>
        /// 以二进制帧发送数据，发送锁保证同一时刻只有一个发送操作
        /// </summary>
        /// <param name="buffer">待发送缓冲区</param>
        /// <param name="offset">发送数据的起始偏移</param>
        /// <param name="count">发送的字节数</param>
        /// <param name="cancellationToken">取消令牌，等待发送锁与发送过程均可被取消</param>
        /// <returns>发送完成的异步任务</returns>
        public async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // The lock is required because the client will throw an exception if _SendAsync_ is 
            // called from multiple threads at the same time. But this issue only happens with several
            // framework versions.
            if (_sendLock == null)
            {
                return;
            }

            using (await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false))
            {
                await _webSocket.SendAsync(new ArraySegment<byte>(buffer, offset, count), WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 释放发送锁与 WebSocket，重复调用安全
        /// </summary>
        public void Dispose()
        {
            Cleanup();
        }

        void SetupClientWebSocket(ClientWebSocket clientWebSocket)
        {

            if (_options.ProxyOptions != null)
            {
                clientWebSocket.Options.Proxy = CreateProxy();
            }

            if (_options.RequestHeaders != null)
            {
                foreach (var requestHeader in _options.RequestHeaders)
                {
                    clientWebSocket.Options.SetRequestHeader(requestHeader.Key, requestHeader.Value);
                }
            }

            if (_options.SubProtocols != null)
            {
                foreach (var subProtocol in _options.SubProtocols)
                {
                    clientWebSocket.Options.AddSubProtocol(subProtocol);
                }
            }

            if (_options.CookieContainer != null)
            {
                clientWebSocket.Options.Cookies = _options.CookieContainer;
            }

            if (_options.TlsOptions?.UseTls == true && _options.TlsOptions?.Certificates != null)
            {
                clientWebSocket.Options.ClientCertificates = new X509CertificateCollection();
                foreach (var certificate in _options.TlsOptions.Certificates)
                {
#if WINDOWS_UWP
                    clientWebSocket.Options.ClientCertificates.Add(new X509Certificate(certificate));
#else
                    clientWebSocket.Options.ClientCertificates.Add(certificate);
#endif

                }
            }

            var certificateValidationHandler = _options.TlsOptions?.CertificateValidationHandler;
#if NETSTANDARD2_1
            if (certificateValidationHandler != null)
            {
                clientWebSocket.Options.RemoteCertificateValidationCallback = new System.Net.Security.RemoteCertificateValidationCallback((sender, certificate, chain, sslPolicyErrors) =>
                {
                    // TODO: Find a way to add client options to same callback. Problem is that they have a different type.
                    var context = new MqttClientCertificateValidationCallbackContext
                    {
                        Certificate = certificate,
                        Chain = chain,
                        SslPolicyErrors = sslPolicyErrors,
                        ClientOptions = _options
                    };

                    return certificateValidationHandler(context);
                });
            }
#else
            if (certificateValidationHandler != null)
            {
                throw new NotSupportedException("The remote certificate validation callback for Web Sockets is only supported for netstandard 2.1+");
            }
#endif
        }

        void Cleanup()
        {
            _sendLock?.Dispose();
            _sendLock = null;

            try
            {
                _webSocket?.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                _webSocket = null;
            }
        }

        IWebProxy CreateProxy()
        {
            if (string.IsNullOrEmpty(_options.ProxyOptions?.Address))
            {
                return null;
            }

#if WINDOWS_UWP
            throw new NotSupportedException("Proxies are not supported in UWP.");
#elif NETSTANDARD1_3
            throw new NotSupportedException("Proxies are not supported in netstandard 1.3.");
#else
            var proxyUri = new Uri(_options.ProxyOptions.Address);

            if (!string.IsNullOrEmpty(_options.ProxyOptions.Username) && !string.IsNullOrEmpty(_options.ProxyOptions.Password))
            {
                var credentials = new NetworkCredential(_options.ProxyOptions.Username, _options.ProxyOptions.Password, _options.ProxyOptions.Domain);
                return new WebProxy(proxyUri, _options.ProxyOptions.BypassOnLocal, _options.ProxyOptions.BypassList, credentials);
            }

            return new WebProxy(proxyUri, _options.ProxyOptions.BypassOnLocal, _options.ProxyOptions.BypassList);
#endif
        }
    }
}