/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets.Core.Tcp
*文件名： StreamClientSocket
*版本号： V4.0.0.1
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
*版本号： V4.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets.Core.Tcp
{
    public class StreamClientSocket : ISocket
    {
        Socket _socket;

        CancellationToken _CancellationToken;

        bool _connected = false;

        SocketOption _SocketOption;

        SslStream _SslStream;

        public bool Connected { get => _connected; set => _connected = value; }

        public bool IsDisposed { get; private set; } = false;

        public event OnDisconnectedHandler OnDisconnected;

        /// <summary>
        /// 客户端 socket
        /// </summary>
        /// <param name="socketOption"></param>
        /// <param name="cancellationToken"></param>
        public StreamClientSocket(SocketOption socketOption, CancellationToken cancellationToken) : this(cancellationToken, socketOption.IP, socketOption.Port, socketOption.BufferSize, socketOption.TimeOut)
        {

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
        public StreamClientSocket(CancellationToken cancellationToken, string ip = "127.0.0.1", int port = 39654, int bufferSize = 100 * 1024, int timeOut = 60 * 1000, SslProtocols sslProtocols = SslProtocols.Tls12)
        {
            _SocketOption = new SocketOption()
            {
                IP = ip,
                Port = port,
                BufferSize = bufferSize,
                TimeOut = timeOut,
                IsClient = true,
                Count = 1,
                SocketType = Model.SocketType.Tcp,
                WithSsl = true,
                SslProtocol = sslProtocols
            };

            _CancellationToken = cancellationToken;

            _socket = new Socket(AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Stream, ProtocolType.Tcp);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _socket.NoDelay = true;
            _socket.SendTimeout = _socket.ReceiveTimeout = 120 * 1000;


        }

        /// <summary>
        /// 可让服务器接受的连接使用
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="sslStream"></param>
        public StreamClientSocket(Socket socket, SslStream sslStream)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));

            _SslStream = sslStream ?? throw new ArgumentNullException(nameof(sslStream));
        }

        /// <summary>
        /// 连接到服务器
        /// </summary>
        public async void ConnectAsync()
        {
            await _socket.ConnectAsync(_SocketOption.IP, _SocketOption.Port).ConfigureAwait(false);

            _SslStream = new SslStream(new NetworkStream(_socket, true), false, InternalUserCertificateValidationCallback);

            _SslStream.ReadTimeout = _SocketOption.TimeOut;
            _SslStream.WriteTimeout = _SocketOption.TimeOut;

            await _SslStream.AuthenticateAsClientAsync(_SocketOption.IP, LoadCertificates(), _SocketOption.SslProtocol, true);

            this.Connected = true;
        }

        /// <summary>
        /// 异步发送
        /// </summary>
        /// <param name="buffer"></param>
        public Task SendAsync(byte[] buffer, int offset, int count)
        {
            return _SslStream.WriteAsync(buffer, offset, count, _CancellationToken);
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
            return _SslStream.WriteAsync(buffer, offset, count, cancellationToken);
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
            return _SslStream.ReadAsync(buffer, offset, count, _CancellationToken);
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
            return _SslStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        /// <summary>
        /// 断开
        /// </summary>
        /// <param name="ex"></param>
        public void Disconnect(Exception ex = null)
        {
            var mex = ex;

            if (this.Connected)
            {
                try
                {
                    _SslStream.Close();
                }
                catch (Exception sex)
                {
                    if (mex != null) mex = sex;
                }
                this.Connected = false;
                OnDisconnected?.Invoke(_SocketOption.IP + ":" + _SocketOption.Port, mex);
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
            if (_SocketOption.X509Certificate2 == null)
            {
                return certificates;
            }

            certificates.Add(_SocketOption.X509Certificate2);

            return certificates;
        }

        #endregion
    }
}
