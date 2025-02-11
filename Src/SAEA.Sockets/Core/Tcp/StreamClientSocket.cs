/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets.Core.Tcp
*文件名： StreamClientSocket
*版本号： v7.0.0.1
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 15:54:21
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;

namespace SAEA.Sockets.Core.Tcp
{
    /// <summary>
    /// 流模式下的tcp client socket
    /// </summary>
    public class StreamClientSocket : IClientSocket
    {
        Socket _socket;

        bool _isSsl = false;

        public ISocketOption SocketOption { get; set; }

        Stream _stream;

        public bool Connected
        {
            get; private set;
        } = false;

        public bool IsDisposed { get; private set; } = false;

        public string Endpoint
        {
            get
            {

                if (_socket != null && _socket.Connected)

                    return _socket?.LocalEndPoint?.ToString();

                return string.Empty;
            }
        }

        public Socket Socket => _socket;

        public IContext<ICoder> Context { get; private set; }

        public event OnDisconnectedHandler OnDisconnected;

        [Obsolete("此方法为IOCP中所用")]
        public event OnClientReceiveHandler OnReceive;


        public event OnErrorHandler OnError;

        CancellationToken _cancellationToken;


        IPEndPoint _serverIPEndpint;

        /// <summary>
        /// 流模式下的tcp client socket
        /// </summary>
        /// <param name="socketOption"></param>
        /// <param name="cancellationToken"></param>
        public StreamClientSocket(ISocketOption socketOption, CancellationToken cancellationToken)
        {
            SocketOption = socketOption;
            _cancellationToken = cancellationToken;
        }

        /// <summary>
        /// 流模式下的tcp client socket
        /// </summary>
        /// <param name="socketOption"></param>
        public StreamClientSocket(ISocketOption socketOption) : this(socketOption, CancellationToken.None)
        {
            Context = SocketOption.Context;

            if (SocketOption.UseIPV6)
            {
                _socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);

                if (string.IsNullOrEmpty(SocketOption.IP))
                    _serverIPEndpint = (new IPEndPoint(IPAddress.IPv6Any, SocketOption.Port));
                else
                    _serverIPEndpint = (new IPEndPoint(IPAddress.Parse(SocketOption.IP), SocketOption.Port));
            }
            else
            {
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                if (string.IsNullOrEmpty(SocketOption.IP))
                    _serverIPEndpint = (new IPEndPoint(IPAddress.Any, SocketOption.Port));
                else
                    _serverIPEndpint = (new IPEndPoint(IPAddress.Parse(SocketOption.IP), SocketOption.Port));
            }

            if (SocketOption.ReusePort)
                _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, SocketOption.ReusePort);

            _socket.NoDelay = SocketOption.NoDelay;
        }

        /// <summary>
        /// 流模式下的tcp client socket
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="stream"></param>
        /// <param name="isSsl"></param>
        public StreamClientSocket(Socket socket, Stream stream, bool isSsl = false)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));

            _stream = stream ?? throw new ArgumentNullException(nameof(stream));

            _isSsl = isSsl;
        }

        /// <summary>
        /// 指定绑定ip
        /// </summary>
        /// <param name="ipEndPoint"></param>
        public void Bind(IPEndPoint ipEndPoint)
        {
            _socket.Bind(ipEndPoint);
        }

        /// <summary>
        /// 指定绑定ip
        /// </summary>
        /// <param name="ip"></param>
        public void Bind(string ip)
        {
            Bind(new IPEndPoint(IPAddress.Parse(ip), 0));
        }


        /// <summary>
        /// Connect
        /// </summary>
        public void Connect()
        {
            if (!Connected)
            {
                _socket.Connect(SocketOption.IP, SocketOption.Port);

                if (_isSsl)
                {
                    _stream = new SslStream(new NetworkStream(_socket, true), false, InternalUserCertificateValidationCallback);

                    ((SslStream)_stream).AuthenticateAsClient(SocketOption.IP, LoadCertificates(), SocketOption.SslProtocol, true);
                }
                else
                {
                    _stream = new NetworkStream(_socket, true);
                }

                _stream.ReadTimeout = SocketOption.TimeOut;
                _stream.WriteTimeout = SocketOption.TimeOut;

                Connected = true;
            }
        }

        /// <summary>
        /// 某些特定证书处理连接方法
        /// </summary>
        /// <param name="ucc"></param>
        /// <param name="func"></param>
        /// <param name="ignoreCerErrs"></param>
        /// <returns></returns>
        public async Task<Stream> ConnectAsync(RemoteCertificateValidationCallback ucc, Func<X509CertificateCollection> func, bool ignoreCerErrs)
        {
            if (!Connected)
            {

                await _socket.ConnectAsync(SocketOption.IP, SocketOption.Port).ConfigureAwait(false);

                var stream = new NetworkStream(_socket, true);

                if (_isSsl)
                {
                    var sslStream = new SslStream(stream, false, ucc);
                    await sslStream.AuthenticateAsClientAsync(SocketOption.IP, func.Invoke(), SocketOption.SslProtocol, !ignoreCerErrs).ConfigureAwait(false);
                    _stream = sslStream;
                }
                else
                {
                    _stream = stream;
                }
                Connected = true;
            }
            return _stream;
        }


        /// <summary>
        /// ConnectAsync
        /// </summary>
        /// <param name="callBack"></param>
        public void ConnectAsync(Action<SocketError> callBack)
        {
            try
            {
                var result = ConnectAsync().Result;

                callBack?.Invoke(result);
            }
            catch
            {
                callBack?.Invoke(SocketError.SocketError);
            }
        }

        /// <summary>
        /// ConnectAsync
        /// </summary>
        /// <returns></returns>
        public async Task<SocketError> ConnectAsync()
        {
            if (!Connected)
            {
                try
                {
                    await _socket.ConnectAsync(SocketOption.IP, SocketOption.Port);

                    if (_isSsl)
                    {
                        _stream = new SslStream(new NetworkStream(_socket, true), false, InternalUserCertificateValidationCallback);

                        ((SslStream)_stream).AuthenticateAsClient(SocketOption.IP, LoadCertificates(), SocketOption.SslProtocol, true);
                    }
                    else
                    {
                        _stream = new NetworkStream(_socket, true);
                    }

                    _stream.ReadTimeout = SocketOption.TimeOut;

                    _stream.WriteTimeout = SocketOption.TimeOut;

                    this.Connected = true;

                    return SocketError.Success;
                }
                catch
                {
                    return SocketError.SocketError;
                }
            }
            return SocketError.Success;
        }

        /// <summary>
        /// Send
        /// </summary>
        /// <param name="buffer"></param>
        public void Send(byte[] buffer)
        {
            _stream.Write(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// SendAsync
        /// </summary>
        /// <param name="buffer"></param>
        [Obsolete("建议使用SendAsync(byte[] buffer, int offset, int count)或其他方法代替")]
        public void SendAsync(byte[] buffer)
        {
            Task.WaitAll(_stream.WriteAsync(buffer, 0, buffer.Length));
        }

        /// <summary>
        /// 异步发送
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public async Task SendAsync(byte[] buffer, int offset, int count)
        {
            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(SocketOption.TimeOut)))
            {
                await SendAsync(buffer, offset, count, cts.Token);
            }
        }

        /// <summary>
        /// 异步发送
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task SendAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await _stream.WriteAsync(buffer, offset, count, cancellationToken);
        }


        /// <summary>
        /// 异步接收
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public async Task ReceiveAsync(byte[] buffer, int offset, int count)
        {
            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(SocketOption.TimeOut)))
            {
                await _stream.ReadAsync(buffer, offset, count, cts.Token);
            }
        }

        /// <summary>
        /// 异步接收
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<int> ReceiveAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return await _stream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        /// <summary>
        /// GetStream
        /// </summary>
        /// <returns></returns>
        public Stream GetStream()
        {
            return _stream;
        }

        /// <summary>
        /// 断开
        /// </summary>
        /// <param name="ex"></param>
        public void Disconnect()
        {
            if (this.Connected)
            {
                try
                {
                    _socket.Shutdown(SocketShutdown.Both);
                    OnDisconnected?.Invoke(SocketOption.IP + ":" + SocketOption.Port, null);
                }
                catch (Exception ex)
                {
                    OnDisconnected?.Invoke(SocketOption.IP + ":" + SocketOption.Port, ex);
                }
                finally
                {
                    _socket.Close();
                }
                this.Connected = false;
            }
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            this.Disconnect();
            IsDisposed = true;
        }


        #region ssl
        private bool InternalUserCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return false;
        }


        private X509CertificateCollection LoadCertificates()
        {
            var certificates = new X509CertificateCollection();
            if (SocketOption.X509Certificate2 == null)
            {
                return certificates;
            }

            certificates.Add(SocketOption.X509Certificate2);

            return certificates;
        }
        #endregion

        /// <summary>
        /// BeginSend
        /// </summary>
        /// <param name="data"></param>
        public void BeginSend(byte[] data)
        {
            SendAsync(data);
        }
    }
}
