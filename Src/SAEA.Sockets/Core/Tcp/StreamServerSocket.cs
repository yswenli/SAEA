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
*文件名： StreamServerSocket
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
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets.Core.Tcp
{
    /// <summary>
    /// 服务器 socket
    /// </summary>
    public class StreamServerSocket : ISocket, IDisposable
    {
        Socket _listener;

        int _clientCounts;

        private readonly CancellationToken _cancellationToken;

        public int ClientCounts { get => _clientCounts; private set => _clientCounts = value; }


        SocketOption _SocketOption;


        bool _isStoped = true;

        #region events

        public event OnStreamAcceptedHandler OnAccepted;

        public event OnErrorHandler OnError;

        public event OnDisconnectedHandler OnDisconnected;

        #endregion



        /// <summary>
        /// 服务器 socket
        /// </summary>
        /// <param name="socketOption"></param>
        /// <param name="cancellationToken"></param>
        public StreamServerSocket(SocketOption socketOption, CancellationToken cancellationToken) : this(cancellationToken, socketOption.X509Certificate2, socketOption.BufferSize, socketOption.Count, socketOption.NoDelay, socketOption.TimeOut, socketOption.SslProtocol)
        {
            _SocketOption.UseIPV6 = socketOption.UseIPV6;
            _SocketOption.Port = socketOption.Port;
        }

        /// <summary>
        /// 服务器 socket
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="x509Certificate2"></param>
        /// <param name="bufferSize"></param>
        /// <param name="count"></param>
        /// <param name="noDelay"></param>
        /// <param name="timeOut"></param>
        /// <param name="sslProtocol"></param>
        public StreamServerSocket(CancellationToken cancellationToken, X509Certificate2 x509Certificate2, int bufferSize = 1024, int count = 10000, bool noDelay = true, int timeOut = 60 * 1000, SslProtocols sslProtocol = SslProtocols.Tls12)
        {
            _listener = new Socket(AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Stream, ProtocolType.Tcp);
            _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.NoDelay = noDelay;

            _SocketOption = new SocketOption()
            {
                BufferSize = bufferSize,
                Count = count,
                IsClient = false,
                NoDelay = noDelay,
                SocketType = Model.SocketType.Tcp,
                SslProtocol = sslProtocol,
                WithSsl = true,
                X509Certificate2 = x509Certificate2
            };
            _cancellationToken = cancellationToken;
        }
        /// <summary>
        /// 启动服务
        /// </summary>
        public void Start(int backlog = 10 * 1000)
        {
            _isStoped = false;

            if (_SocketOption.UseIPV6)
            {
                _listener.Bind(new IPEndPoint(IPAddress.IPv6Any, _SocketOption.Port));
            }
            else
            {
                _listener.Bind(new IPEndPoint(IPAddress.Any, _SocketOption.Port));
            }

            _listener.Listen(backlog);

            Task.Run(ProcessAccepted, _cancellationToken);
        }

        private async Task ProcessAccepted()
        {
            while (!_isStoped)
            {
                try
                {
                    var clientSocket = await _listener.AcceptAsync().ConfigureAwait(false);
                    clientSocket.NoDelay = true;
                    SslStream sslStream = new SslStream(new NetworkStream(clientSocket), false);
                    await sslStream.AuthenticateAsServerAsync(_SocketOption.X509Certificate2, false, _SocketOption.SslProtocol, false).ConfigureAwait(false);
                    OnAccepted?.Invoke(clientSocket, sslStream);
                }
                catch (ObjectDisposedException oex)
                {
                    OnError?.Invoke(string.Empty, oex);
                }
                catch (Exception exception)
                {
                    if (exception is SocketException s && s.SocketErrorCode == SocketError.OperationAborted)
                    {
                        OnDisconnected?.Invoke(_SocketOption.IP + "_" + _SocketOption.Port, exception);
                        return;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(1), _cancellationToken).ConfigureAwait(false);
                }
            }
        }


        /// <summary>
        /// 关闭
        /// </summary>
        public void Stop()
        {
            try
            {
                Dispose();
            }
            catch { }
        }

        public void Dispose()
        {
            _listener?.Dispose();
            _SocketOption.X509Certificate2?.Dispose();
        }
    }
}
