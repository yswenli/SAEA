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

        Action<SocketError> _connectCallBack;

        AutoResetEvent _connectEvent = new AutoResetEvent(true);

        ISocketOption _socketOption;

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
            _socketOption = socketOption;

            _userTokenFactory = new UserTokenFactory();

            _udpSocket = new Socket(AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, ProtocolType.Udp);
            _udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, _socketOption.ReusePort);            
            _udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, _socketOption.Broadcasted);

            //设置多播
            if (!string.IsNullOrEmpty(_socketOption.MultiCastHost) && !string.IsNullOrEmpty(_socketOption.IP))
            {
                _udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);
                MulticastOption mcastOption = new MulticastOption(IPAddress.Parse(_socketOption.MultiCastHost), IPAddress.Parse(_socketOption.IP));
                _udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, mcastOption);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _udpSocket.IOControl(SIO_UDP_CONNRESET, new byte[4], new byte[4]);
            }

            _udpSocket.SendTimeout = _udpSocket.ReceiveTimeout = _socketOption.TimeOut;
            _udpSocket.SendBufferSize = _socketOption.WriteBufferSize;
            _udpSocket.ReceiveBufferSize = _socketOption.ReadBufferSize;

            OnClientReceive = new OnClientReceiveBytesHandler(OnReceived);

            _connectArgs = new SocketAsyncEventArgs
            {
                RemoteEndPoint = new IPEndPoint(IPAddress.Parse(_socketOption.IP), _socketOption.Port)
            };
            _connectArgs.Completed += ConnectArgs_Completed;

            _userToken = _userTokenFactory.Create(socketOption.Context, socketOption.ReadBufferSize, IO_Completed);

            _remoteEndPoint = new IPEndPoint(IPAddress.Parse(_socketOption.IP), _socketOption.Port);

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
                throw new Exception(e.LastOperation.ToString());
        }

        void ProcessConnected(SocketAsyncEventArgs e)
        {
            _userToken.ID = _udpSocket.LocalEndPoint.ToString();
            _userToken.Socket = _udpSocket;
            _userToken.Linked = _userToken.Actived = DateTime.Now;

            var readArgs = _userToken.ReadArgs;

            ProcessReceive(readArgs);

            _connectCallBack?.Invoke(e.SocketError);
            _connectEvent.Set();
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
                    ProcessDisconnected(new Exception("SocketError:the remote server closed connection"));
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
            _connectEvent.Set();
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
                if (data == null || !data.Any() || data.Length > SocketOption.UDPMaxLength) throw new ArgumentException("SendAsync 参数异常");

                UserToken.WaitOne();

                var writeArgs = UserToken.WriteArgs;

                writeArgs.SetBuffer(data, 0, data.Length);

                writeArgs.RemoteEndPoint = ipEndPoint;

                if (!UserToken.Socket.SendToAsync(writeArgs))
                {
                    ProcessSended(writeArgs);
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
            if (data == null) return;

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
            _userToken.Socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, _remoteEndPoint, null, null);
        }


        public Task SendAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                var data = new byte[count];

                Buffer.BlockCopy(buffer, offset, data, 0, count);

                Send(data);

            }, cancellationToken);
        }

        public Task<int> ReceiveAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new KernelException("当前方法只在stream实现中才能使用!");
        }

        public Stream GetStream()
        {
            throw new InvalidOperationException("udp不支持流模式");
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

        private void _sessionManager_OnTimeOut(IUserToken obj)
        {
            Disconnect();
        }

        public void Dispose()
        {
            this.Disconnect();
            IsDisposed = true;
        }

        public void Disconnect()
        {
            this.Disconnect(null);
        }
    }
}
