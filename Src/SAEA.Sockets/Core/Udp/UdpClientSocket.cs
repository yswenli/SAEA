/****************************************************************************
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*项目名称：SAEA.Sockets.Core.Udp
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Sockets.Core.Udp
*类 名 称：UdpClientSocket
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/28 18:01:43
*描述：
*=====================================================================
*修改时间：2020/12/28 18:01:43
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Common;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets.Core.Udp
{
    public class UdpClientSocket : IClientSocket, IDisposable
    {
        const int SIO_UDP_CONNRESET = unchecked((int)0x9800000C);

        Socket _udpSocket;

        IUserToken _userToken;

        SocketAsyncEventArgs _connectArgs;

        UserTokenFactory _userTokenFactory;

        public ISocketOption SocketOption { get; set; }

        IPEndPoint _remoteEndPoint;

        public string Endpoint { get => _udpSocket?.RemoteEndPoint?.ToString(); }

        public bool Connected { get => _udpSocket.Connected; }
        public IUserToken UserToken { get => _userToken; private set => _userToken = value; }

        public bool IsDisposed { get; private set; } = false;

        public Socket Socket => _udpSocket;

        public event OnErrorHandler OnError;

        public event OnDisconnectedHandler OnDisconnected;

        public event OnClientReceiveHandler OnReceive;

        protected OnClientReceiveBytesHandler OnClientReceive = null;

        protected void RaiseOnError(string id, Exception ex)
        {
            OnError?.Invoke(id, ex);
        }

        /// <summary>
        /// iocp 客户端 socket
        /// </summary>
        /// <param name="socketOption"></param>
        public UdpClientSocket(ISocketOption socketOption)
        {
            SocketOption = socketOption;

            _userTokenFactory = new UserTokenFactory();

            _udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, SocketOption.ReusePort);
            _udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, SocketOption.Broadcasted);

            //设置多播
            if (!string.IsNullOrEmpty(SocketOption.MultiCastHost) && !string.IsNullOrEmpty(SocketOption.IP))
            {
                _udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);
                MulticastOption mcastOption = new MulticastOption(IPAddress.Parse(SocketOption.MultiCastHost), IPAddress.Parse(SocketOption.IP));
                _udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, mcastOption);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _udpSocket.IOControl(SIO_UDP_CONNRESET, new byte[4], new byte[4]);
            }

            _udpSocket.SendTimeout = _udpSocket.ReceiveTimeout = SocketOption.TimeOut;
            _udpSocket.SendBufferSize = SocketOption.WriteBufferSize;
            _udpSocket.ReceiveBufferSize = SocketOption.ReadBufferSize;

            OnClientReceive = new OnClientReceiveBytesHandler(OnReceived);

            _connectArgs = new SocketAsyncEventArgs
            {
                RemoteEndPoint = new IPEndPoint(IPAddress.Parse(SocketOption.IP), SocketOption.Port)
            };
            _connectArgs.Completed += ConnectArgs_Completed;

            _userToken = _userTokenFactory.Create(socketOption.Context, socketOption.ReadBufferSize, IO_Completed);

            _remoteEndPoint = new IPEndPoint(IPAddress.Parse(SocketOption.IP), SocketOption.Port);

            _userToken.Socket = _udpSocket;
        }

        /// <summary>
        /// 指定绑定ip
        /// </summary>
        /// <param name="ip"></param>
        public void Bind(IPEndPoint ipEndPint)
        {
            _udpSocket.Bind(ipEndPint);
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
        /// <param name="callBack"></param>
        public void ConnectAsync(Action<SocketError> callBack = null)
        {
            if (!_udpSocket.ConnectAsync(_connectArgs))
            {
                ProcessConnected(_connectArgs);
            }
        }

        /// <summary>
        /// 连接到服务器
        /// </summary>
        /// <param name="timeOut"></param>
        public void Connect()
        {
            ConnectAsync();
        }

        void ConnectArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            if (e.LastOperation == SocketAsyncOperation.Connect)
                ProcessConnected(e);
            else
            {
                OnError?.Invoke("", new Exception($"connection failed： {e.LastOperation}"));
            }

        }

        void ProcessConnected(SocketAsyncEventArgs e)
        {
            _userToken.ID = _udpSocket.LocalEndPoint.ToString();
            _userToken.Socket = _udpSocket;
            _userToken.Linked = _userToken.Actived = DateTimeHelper.Now;

            var readArgs = _userToken.ReadArgs;
            ProcessReceive(readArgs);
        }


        void IO_Completed(object sender, SocketAsyncEventArgs e)
        {
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                case SocketAsyncOperation.ReceiveFrom:
                    ProcessReceived(e);
                    break;
                case SocketAsyncOperation.Send:
                case SocketAsyncOperation.SendTo:
                    ProcessSended(e);
                    break;
                default:
                    Disconnect();
                    break;
            }
        }


        protected virtual void OnReceived(byte[] data)
        {
            OnReceive?.Invoke(data);
        }


        void ProcessReceive(SocketAsyncEventArgs readArgs)
        {
            if (_userToken.Socket != null)
            {
                if (!_userToken.Socket.ReceiveAsync(readArgs))
                {
                    ProcessReceived(readArgs);
                }
            }

        }


        void ProcessReceived(SocketAsyncEventArgs readArgs)
        {
            try
            {
                if (readArgs.BytesTransferred > 0 && readArgs.SocketError == SocketError.Success)
                {
                    _userToken.Actived = DateTimeHelper.Now;

                    var data = readArgs.Buffer.AsSpan().Slice(readArgs.Offset, readArgs.BytesTransferred).ToArray();

                    OnClientReceive?.Invoke(data);

                    ProcessReceive(readArgs);
                }
                else
                {
                    ProcessDisconnected(new Exception($"SocketError:{readArgs.SocketError},the remote server closed connection"));
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(_userToken.ID, ex);
                Disconnect();
            }
        }


        void ProcessSended(SocketAsyncEventArgs e)
        {
            _userToken.Actived = DateTimeHelper.Now;
            _userToken.Set();
        }



        void ProcessDisconnected(Exception ex)
        {
            try
            {
                _userToken.Clear();
            }
            catch { }
            OnDisconnected?.Invoke(_userToken.ID, ex);
        }



        /// <summary>
        /// 异步发送
        /// </summary>
        /// <param name="ipEndPoint"></param>
        /// <param name="data"></param>
        public void SendAsync(IPEndPoint ipEndPoint, byte[] data)
        {
            try
            {
                if (data == null || !data.Any() || data.Length > Model.SocketOption.UDPMaxLength) throw new ArgumentOutOfRangeException("SendAsync Incorrect length of data sent");

                if (UserToken.WaitOne(SocketOption.TimeOut))
                {
                    var writeArgs = UserToken.WriteArgs;

                    writeArgs.SetBuffer(data, 0, data.Length);

                    writeArgs.RemoteEndPoint = ipEndPoint;

                    if (!UserToken.Socket.SendToAsync(writeArgs))
                    {
                        ProcessSended(writeArgs);
                    }
                }
                else
                {
                    OnError?.Invoke($"An exception occurs when a message is sending:{ipEndPoint.ToString()}", new TimeoutException("Sending data timeout"));
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(_remoteEndPoint.ToString(), ex);
                UserToken.Set();
                Disconnect();
            }
        }

        /// <summary>
        /// iocp发送
        /// </summary>
        /// <param name="data"></param>
        public void SendAsync(byte[] data)
        {
            SendAsync(_remoteEndPoint, data);
        }

        /// <summary>
        /// 同步发送
        /// </summary>
        /// <param name="data"></param>
        public void Send(byte[] data)
        {
            if (data == null || !data.Any() || data.Length > Model.SocketOption.UDPMaxLength) throw new ArgumentOutOfRangeException("Send Incorrect length of data sent");

            try
            {
                var offset = 0;
                do
                {
                    var iResult = _udpSocket.BeginSendTo(data, offset, data.Length - offset, SocketFlags.None, _remoteEndPoint, null, null);

                    offset += _udpSocket.EndSend(iResult);
                }
                while (offset < data.Length);

                _userToken.Actived = DateTimeHelper.Now;
            }
            catch (Exception ex)
            {
                ProcessDisconnected(ex);
            }
        }

        public void BeginSend(byte[] data)
        {
            if (data == null || !data.Any() || data.Length > Model.SocketOption.UDPMaxLength) throw new ArgumentOutOfRangeException("BeginSend Incorrect length of data sent");

            _userToken.Socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, _remoteEndPoint, null, null);

            _userToken.Actived = DateTimeHelper.Now;
        }


        public Task SendAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                var data = new byte[count];

                Buffer.BlockCopy(buffer, offset, data, 0, count);

                if (data == null || !data.Any() || data.Length > Model.SocketOption.UDPMaxLength) throw new ArgumentOutOfRangeException("SendAsync Incorrect length of data sent");

                Send(data);

            }, cancellationToken);
        }

        public Task<int> ReceiveAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("The current method can only be used in stream implementation!");
        }

        public Stream GetStream()
        {
            throw new NotSupportedException("UDP does not support streaming mode");
        }

        public void Disconnect(Exception ex)
        {
            var mex = ex;

            try
            {
                if (_userToken != null && _userToken.Socket != null)
                {
                    _userToken.Socket.Close();
                }
            }
            catch (Exception sex)
            {
                if (mex != null) mex = sex;
            }
            if (mex == null)
            {
                mex = new Exception("当前Socket已主动关闭！");
            }
            if (_userToken != null)
                OnDisconnected?.Invoke(_userToken.ID, mex);

            _userToken.Clear();
        }

        public void Disconnect()
        {
            this.Disconnect(null);
        }

        public void Dispose()
        {
            this.Disconnect();
            IsDisposed = true;
        }
    }
}
