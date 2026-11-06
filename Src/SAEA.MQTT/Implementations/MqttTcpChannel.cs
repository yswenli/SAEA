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
*文件名： MqttTcpChannel
*版本号： v26.11.5.1
*唯一标识：36523cc8-691a-4dae-b36b-05460f59a690
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 12:04:50
*描述：基于 TCP 的 MQTT 信道实现
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
    /// <summary>
    /// 基于 TCP 的 MQTT 信道，底层使用 SAEA.Sockets 流模式，TLS 可用时同一条流即为加密流
    /// </summary>
    public sealed class MqttTcpChannel : IMqttChannel
    {
        readonly IMqttClientOptions _clientOptions;
        readonly MqttClientTcpOptions _options;

        Stream _stream;

        /// <summary>
        /// 以客户端连接选项创建信道，创建时尚未建立连接
        /// </summary>
        /// <param name="clientOptions">客户端连接选项，信道选项须为 MqttClientTcpOptions，不能为null</param>
        public MqttTcpChannel(IMqttClientOptions clientOptions)
        {
            _clientOptions = clientOptions ?? throw new ArgumentNullException(nameof(clientOptions));
            _options = (MqttClientTcpOptions)clientOptions.ChannelOptions;

            IsSecureConnection = clientOptions.ChannelOptions?.TlsOptions?.UseTls == true;
        }

        /// <summary>
        /// 以已建立的流创建信道，用于服务端接管接收到的连接
        /// </summary>
        /// <param name="stream">已建立的读写流，不能为null</param>
        /// <param name="endpoint">对端地址描述</param>
        /// <param name="clientCertificate">客户端证书，无证书时为null</param>
        /// <param name="isSecure">是否为加密连接，默认 false</param>
        public MqttTcpChannel(Stream stream, string endpoint, X509Certificate2 clientCertificate, bool isSecure = false)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));

            Endpoint = endpoint;

            IsSecureConnection = isSecure;

            ClientCertificate = clientCertificate;
        }

        /// <summary>
        /// 通道的对端地址，连接建立前为null
        /// </summary>
        public string Endpoint { get; private set; }

        /// <summary>
        /// 是否为加密连接，构造时按 TLS 选项预置，连接后以握手是否成功为准
        /// </summary>
        public bool IsSecureConnection { get; private set; }

        /// <summary>
        /// 客户端证书，仅在服务端以流创建信道时设置，否则为null
        /// </summary>
        public X509Certificate2 ClientCertificate { get; }

        /// <summary>
        /// 建立连接并取得可读写流，服务端地址按 IP 字面量解析
        /// </summary>
        /// <param name="cancellationToken">取消令牌，取消时中止连接并释放已创建的套接字</param>
        /// <returns>连接完成的异步任务</returns>
        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            IClientSocket client = null;
            try
            {
                SocketOptionBuilder sb = SocketOptionBuilder.Instance;
                sb = sb.UseStream();
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

                var tlsOptions = _options.TlsOptions;
                var useTls = tlsOptions?.UseTls == true;

                if (useTls)
                {
                    var certificates = LoadCertificates();
                    sb = sb.WithSsl(o =>
                    {
                        o.Protocols = tlsOptions.SslProtocol;
                        o.TargetHost = ipStr;
                        o.CertificateValidationCallback = InternalUserCertificateValidationCallback;
                        o.AllowInvalidCertificates = tlsOptions.AllowUntrustedCertificates;
                        o.CheckCertificateRevocation = !tlsOptions.IgnoreCertificateRevocationErrors;
                        if (certificates.Count > 0)
                            o.ClientCertificates = certificates;
                    });
                }

                client = SocketFactory.CreateClientSocket(sb.Build());

                await ((SaeaClientSocket)client).ConnectAsync(cancellationToken).ConfigureAwait(false);

                _stream = client.GetStream();

                IsSecureConnection = useTls && ((SaeaClientSocket)client).IsAuthenticated;

                Endpoint = client.Endpoint;
            }
            catch (Exception)
            {
                client?.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 断开连接，实际行为是释放底层流
        /// </summary>
        /// <param name="cancellationToken">取消令牌，本实现未使用</param>
        /// <returns>已完成的异步任务</returns>
        public Task DisconnectAsync(CancellationToken cancellationToken)
        {
            Dispose();
            return Task.FromResult(0);
        }

        /// <summary>
        /// 从底层流读取数据，流不存在或不可读时返回0
        /// </summary>
        /// <param name="buffer">接收缓冲区，不能为null</param>
        /// <param name="offset">写入缓冲区的起始偏移</param>
        /// <param name="count">本次最多读取的字节数</param>
        /// <param name="cancellationToken">取消令牌，取消后同时释放连接</param>
        /// <returns>实际读取的字节数，返回0表示对端已关闭连接</returns>
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

        /// <summary>
        /// 向底层流写入数据，连接已关闭时抛 MqttCommunicationException
        /// </summary>
        /// <param name="buffer">待发送缓冲区，不能为null</param>
        /// <param name="offset">发送数据的起始偏移</param>
        /// <param name="count">发送的字节数</param>
        /// <param name="cancellationToken">取消令牌，取消后同时释放连接</param>
        /// <returns>写入完成的异步任务</returns>
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

        /// <summary>
        /// 释放底层流并清空引用，重复调用安全
        /// </summary>
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

        X509Certificate2Collection LoadCertificates()
        {
            var certificates = new X509Certificate2Collection();
            if (_options.TlsOptions.Certificates == null)
            {
                return certificates;
            }

            foreach (var certificate in _options.TlsOptions.Certificates)
            {
                if (certificate == null) continue;
                certificates.Add(certificate as X509Certificate2 ?? new X509Certificate2(certificate));
            }

            return certificates;
        }
    }
}
#endif
