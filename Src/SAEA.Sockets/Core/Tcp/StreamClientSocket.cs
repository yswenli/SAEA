/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets.Core.Tcp
*文件名： StreamClientSocket
*版本号： v6.0.0.1
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
*版本号： v6.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets.Core.Tcp
{
    public class StreamClientSocket : IClientSocket
    {
        Socket _socket;

        bool _isSsl = false;

        CancellationToken _cancellationToken;

        ISocketOption _socketOption;

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

                    return _socket?.RemoteEndPoint?.ToString();

                return string.Empty;
            }
        }

        public Socket Socket => _socket;

        public event OnDisconnectedHandler OnDisconnected;

        [Obsolete("此方法为IOCP中所用")]
        public event OnClientReceiveHandler OnReceive;


        public event OnErrorHandler OnError;

        /// <summary>
        /// 客户端 socket
        /// </summary>
        /// <param name="socketOption"></param>
        /// <param name="cancellationToken"></param>
        public StreamClientSocket(ISocketOption socketOption, CancellationToken cancellationToken) : this(cancellationToken, socketOption.IP, socketOption.Port, socketOption.ReadBufferSize, socketOption.TimeOut, socketOption.SslProtocol, socketOption.WithSsl)
        {
            _socketOption = socketOption;
        }

        /// <summary>
        /// 客户端 socket
        /// </summary>
        /// <param name="socketOption"></param>
        public StreamClientSocket(ISocketOption socketOption) : this(socketOption.IP, socketOption.Port, socketOption.ReadBufferSize, socketOption.TimeOut, socketOption.SslProtocol, socketOption.WithSsl)
        {
            _socketOption = socketOption;
        }

        /// <summary>
        /// 客户端 socket
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="bufferSize"></param>
        /// <param name="timeOut"></param>
        /// <param name="sslProtocols"></param>
        public StreamClientSocket(CancellationToken cancellationToken, string ip = "127.0.0.1", int port = 39654, int bufferSize = 100 * 1024, int timeOut = 60 * 1000, SslProtocols sslProtocols = SslProtocols.Tls12, bool userSsl = false)
        {
            _cancellationToken = cancellationToken;

            _socket = new Socket(AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Stream, ProtocolType.Tcp);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _socket.Blocking = true;
            _socket.NoDelay = true;
            _socket.SendTimeout = _socket.ReceiveTimeout = 120 * 1000;
            _socket.KeepAlive();

            _isSsl = userSsl;
        }

        /// <summary>
        /// 客户端 socket
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="bufferSize"></param>
        /// <param name="timeOut"></param>
        /// <param name="sslProtocols"></param>
        public StreamClientSocket(string ip = "127.0.0.1", int port = 39654, int bufferSize = 100 * 1024, int timeOut = 60 * 1000, SslProtocols sslProtocols = SslProtocols.Tls12, bool userSsl = false)
        {
            _socket = new Socket(AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Stream, ProtocolType.Tcp);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _socket.NoDelay = true;
            _socket.Blocking = true;
            _socket.SendTimeout = _socket.ReceiveTimeout = 120 * 1000;
            _isSsl = userSsl;
        }



        /// <summary>
        /// 可让服务器接受的连接使用
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="stream"></param>
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
        /// 连接到服务器
        /// </summary>
        public async Task ConnectAsync()
        {
            if (!Connected)
            {
                await _socket.ConnectAsync(_socketOption.IP, _socketOption.Port).ConfigureAwait(true);

                if (_isSsl)
                {
                    _stream = new SslStream(new NetworkStream(_socket, true), false, InternalUserCertificateValidationCallback);

                    ((SslStream)_stream).AuthenticateAsClient(_socketOption.IP, LoadCertificates(), _socketOption.SslProtocol, true);
                }
                else
                {
                    _stream = new NetworkStream(_socket, true);
                }

                _stream.ReadTimeout = _socketOption.TimeOut;

                _stream.WriteTimeout = _socketOption.TimeOut;

                this.Connected = true;
            }
        }

        public void Connect()
        {
            if (!Connected)
            {
                _socket.Connect(_socketOption.IP, _socketOption.Port);

                if (_isSsl)
                {
                    _stream = new SslStream(new NetworkStream(_socket, true), false, InternalUserCertificateValidationCallback);

                    ((SslStream)_stream).AuthenticateAsClient(_socketOption.IP, LoadCertificates(), _socketOption.SslProtocol, true);
                }
                else
                {
                    _stream = new NetworkStream(_socket, true);
                }

                _stream.ReadTimeout = _socketOption.TimeOut;
                _stream.WriteTimeout = _socketOption.TimeOut;

                Connected = true;
            }
        }

        public void ConnectAsync(Action<SocketError> callBack = null)
        {
            if (!Connected)
            {
                Task.Run(() =>
                {
                    try
                    {
                        _socket.ConnectAsync(_socketOption.IP, _socketOption.Port).GetAwaiter().GetResult();

                        if (_isSsl)
                        {
                            _stream = new SslStream(new NetworkStream(_socket, true), false, InternalUserCertificateValidationCallback);

                            ((SslStream)_stream).AuthenticateAsClient(_socketOption.IP, LoadCertificates(), _socketOption.SslProtocol, true);
                        }
                        else
                        {
                            _stream = new NetworkStream(_socket, true);
                        }

                        _stream.ReadTimeout = _socketOption.TimeOut;

                        _stream.WriteTimeout = _socketOption.TimeOut;

                        this.Connected = true;

                        callBack?.Invoke(SocketError.Success);
                    }
                    catch
                    {
                        callBack?.Invoke(SocketError.SocketError);
                    }
                }).Wait(_socketOption.TimeOut);
            }
        }


        public void Send(byte[] buffer)
        {
            _stream.Write(buffer, 0, buffer.Length);
        }

        public void SendAsync(byte[] buffer)
        {
            _stream.WriteAsync(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// 异步发送
        /// </summary>
        /// <param name="buffer"></param>
        public Task SendAsync(byte[] buffer, int offset, int count)
        {
            return _stream.WriteAsync(buffer, offset, count, _cancellationToken);
        }

        /// <summary>
        /// 异步发送
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task SendAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _stream.WriteAsync(buffer, offset, count, cancellationToken);
        }


        /// <summary>
        /// 异步接收
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public Task ReceiveAsync(byte[] buffer, int offset, int count)
        {
            return _stream.ReadAsync(buffer, offset, count, _cancellationToken);
        }

        /// <summary>
        /// 异步接收
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<int> ReceiveAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _stream.ReadAsync(buffer, offset, count, cancellationToken);
        }


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
                    OnDisconnected?.Invoke(_socketOption.IP + ":" + _socketOption.Port, null);
                }
                catch (Exception ex)
                {
                    OnDisconnected?.Invoke(_socketOption.IP + ":" + _socketOption.Port, ex);
                }
                finally
                {
                    _socket.Close();
                }
                this.Connected = false;
            }
        }

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
            if (_socketOption.X509Certificate2 == null)
            {
                return certificates;
            }

            certificates.Add(_socketOption.X509Certificate2);

            return certificates;
        }
        #endregion
        public void BeginSend(byte[] data)
        {
            throw new NotImplementedException();
        }        
    }
}
