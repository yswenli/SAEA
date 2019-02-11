/****************************************************************************
*项目名称：SAEA.MQTT
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Core.Implementations
*类 名 称：MqttWebSocketChannel
*版 本 号： V4.1.2.2
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/14 19:07:44
*描述：
*=====================================================================
*修改时间：2019/1/14 19:07:44
*修 改 人： yswenli
*版 本 号： V4.1.2.2
*描    述：
*****************************************************************************/
using SAEA.MQTT.Interface;
using SAEA.MQTT.Model;
using System;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.Core.Implementations
{
    public class MqttWebSocketChannel : IMqttChannel
    {
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly MqttClientWebSocketOptions _options;

        private WebSocket _webSocket;

        public MqttWebSocketChannel(MqttClientWebSocketOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public MqttWebSocketChannel(WebSocket webSocket, string endpoint)
        {
            _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
            Endpoint = endpoint;
        }

        public string Endpoint { get; }


        public async Task ConnectAsync()
        {
            CancellationToken cancellationToken = new CancellationToken(false);
            await ConnectAsync(cancellationToken);
        }

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
                    clientWebSocket.Options.ClientCertificates.Add(new X509Certificate(certificate));
                }
            }

            await clientWebSocket.ConnectAsync(new Uri(uri), cancellationToken).ConfigureAwait(false);

            _webSocket = clientWebSocket;
        }

        public async Task DisconnectAsync()
        {
            if (_webSocket == null)
            {
                return;
            }

            if (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.Connecting)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None).ConfigureAwait(false);
            }

            Dispose();
        }

        public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var response = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, count), cancellationToken).ConfigureAwait(false);
            return response.Count;
        }

        public async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // This lock is required because the client will throw an exception if _SendAsync_ is 
            // called from multiple threads at the same time. But this issue only happens with several
            // framework versions.
            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _webSocket.SendAsync(new ArraySegment<byte>(buffer, offset, count), WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public void Dispose()
        {
            _sendLock?.Dispose();

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

        private IWebProxy CreateProxy()
        {
            if (string.IsNullOrEmpty(_options.ProxyOptions?.Address))
            {
                return null;
            }

            var proxyUri = new Uri(_options.ProxyOptions.Address);

            if (!string.IsNullOrEmpty(_options.ProxyOptions.Username) && !string.IsNullOrEmpty(_options.ProxyOptions.Password))
            {
                var credentials =
                    new NetworkCredential(_options.ProxyOptions.Username, _options.ProxyOptions.Password, _options.ProxyOptions.Domain);

                return new WebProxy(proxyUri, _options.ProxyOptions.BypassOnLocal, _options.ProxyOptions.BypassList, credentials);
            }

            return new WebProxy(proxyUri, _options.ProxyOptions.BypassOnLocal, _options.ProxyOptions.BypassList);

        }
    }
}