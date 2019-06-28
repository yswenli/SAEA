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
*文件名： IocpClientSocket
*版本号： v4.5.6.7
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
*版本号： v4.5.6.7
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets.Core.Tcp
{
    /// <summary>
    /// iocp 客户端 socket
    /// </summary>
    public class IocpClientSocket : IClientSocket, IDisposable
    {
        Socket _socket;

        string _ip = string.Empty;

        int _port = 39654;

        IUserToken _userToken;

        SocketAsyncEventArgs _connectArgs;

        UserTokenFactory _userTokenFactory;


        MemoryStream _strean;

        Action<SocketError> _connectCallBack;

        AutoResetEvent _connectEvent = new AutoResetEvent(true);

        ISocketOption _SocketOption;

        public string Endpoint { get => _socket?.RemoteEndPoint?.ToString(); }

        public bool Connected { get; set; }
        public IUserToken UserToken { get => _userToken; private set => _userToken = value; }

        public bool IsDisposed { get; private set; } = false;

        public Socket Socket => _socket;

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
        public IocpClientSocket(ISocketOption socketOption) : this(socketOption.Context, socketOption.IP, socketOption.Port, socketOption.ReadBufferSize, socketOption.TimeOut)
        {
            _SocketOption = socketOption;
        }

        /// <summary>
        /// iocp 客户端 socket
        /// </summary>
        /// <param name="context"></param>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="bufferSize"></param>
        /// <param name="timeOut"></param>
        public IocpClientSocket(IContext context, string ip = "127.0.0.1", int port = 39654, int bufferSize = 100 * 1024, int timeOut = 60 * 1000)
        {
            _userTokenFactory = new UserTokenFactory();

            _socket = new Socket(AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Stream, ProtocolType.Tcp);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _socket.NoDelay = true;
            _socket.SendTimeout = _socket.ReceiveTimeout = timeOut;
            _socket.KeepAlive();


            _ip = ip;
            _port = port;

            _strean = new MemoryStream();

            OnClientReceive = new OnClientReceiveBytesHandler(OnReceived);

            _connectArgs = new SocketAsyncEventArgs
            {
                RemoteEndPoint = new IPEndPoint(IPAddress.Parse(_ip), _port)
            };
            _connectArgs.Completed += ConnectArgs_Completed;

            _userToken = _userTokenFactory.Create(context, bufferSize, IO_Completed);
        }

        /// <summary>
        /// 连接到服务器
        /// </summary>
        /// <param name="callBack"></param>
        public void ConnectAsync(Action<SocketError> callBack = null)
        {
            _connectEvent.WaitOne();
            if (!Connected)
            {
                _connectCallBack = callBack;
                if (!_socket.ConnectAsync(_connectArgs))
                {
                    ProcessConnected(_connectArgs);
                }
            }
        }

        /// <summary>
        /// 连接到服务器
        /// </summary>
        /// <returns></returns>
        public async Task ConnectAsync()
        {
            if (!Connected)
            {
                await _socket.ConnectAsync(_SocketOption.IP, _SocketOption.Port).ConfigureAwait(false);

                Connected = true;

                _userToken.ID = _socket.LocalEndPoint.ToString();
                _userToken.Socket = _socket;
                _userToken.Linked = _userToken.Actived = DateTime.Now;

                var readArgs = _userToken.ReadArgs;
                if (!_userToken.Socket.ReceiveAsync(readArgs))
                    ProcessReceive(readArgs);
            }
        }

        /// <summary>
        /// 连接到服务器
        /// </summary>
        /// <param name="timeOut"></param>
        public void Connect(int timeOut = 30 * 1000)
        {
            var connected = false;

            ConnectAsync((s) =>
            {
                connected = true;
            });

            var step = 0;

            while (!connected)
            {
                Thread.Sleep(10);

                step += 10;

                if (step >= timeOut)
                {
                    return;
                }
            }
        }

        void ConnectArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            ProcessConnected(e);
        }

        void ProcessConnected(SocketAsyncEventArgs e)
        {
            Connected = (e.SocketError == SocketError.Success);

            if (Connected)
            {
                _userToken.ID = e.ConnectSocket.LocalEndPoint.ToString();
                _userToken.Socket = _socket;
                _userToken.Linked = _userToken.Actived = DateTime.Now;

                var readArgs = _userToken.ReadArgs;
                if (!_userToken.Socket.ReceiveAsync(readArgs))
                    ProcessReceive(readArgs);
                _connectCallBack?.Invoke(e.SocketError);
            }
            _connectEvent.Set();
        }


        void IO_Completed(object sender, SocketAsyncEventArgs e)
        {
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    ProcessReceive(e);
                    break;
                case SocketAsyncOperation.Send:
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



        void ProcessReceive(SocketAsyncEventArgs e)
        {
            try
            {
                if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
                {

                    _userToken.Actived = DateTimeHelper.Now;

                    var data = new byte[e.BytesTransferred];

                    Buffer.BlockCopy(e.Buffer, e.Offset, data, 0, e.BytesTransferred);

                    OnClientReceive?.Invoke(data);
                    _strean.Position = _strean.Length;
                    _strean.Write(data, 0, data.Length);


                    if (_userToken.Socket != null && !_userToken.Socket.ReceiveAsync(e))
                        ProcessReceive(e);
                }
                else
                {
                    ProcessDisconnected(new Exception("SocketError:" + e.SocketError.ToString()));
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
            _userToken.Set();
            _userToken.Actived = DateTimeHelper.Now;
        }



        void ProcessDisconnected(Exception ex)
        {
            Connected = false;
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
        /// <param name="userToken"></param>
        /// <param name="data"></param>
        public void SendAsync(IUserToken userToken, byte[] data)
        {
            try
            {
                userToken.WaitOne();

                var writeArgs = userToken.WriteArgs;

                writeArgs.SetBuffer(data, 0, data.Length);

                if (!userToken.Socket.SendAsync(writeArgs))
                {
                    ProcessSended(writeArgs);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(userToken.ID, ex);
                userToken.Set();
                Disconnect();
            }
        }

        /// <summary>
        /// iocp发送
        /// </summary>
        /// <param name="data"></param>
        public void Send(byte[] data)
        {
            SendAsync(UserToken, data);
        }

        /// <summary>
        /// 同步发送
        /// </summary>
        /// <param name="data"></param>
        protected void SendSync(byte[] data)
        {
            if (data == null) return;

            if (Connected)
            {
                try
                {
                    var offset = 0;
                    do
                    {
                        var iResult = _socket.BeginSend(data, offset, data.Length - offset, SocketFlags.None, null, null);
                        offset += _socket.EndSend(iResult);
                    }
                    while (offset < data.Length);

                    _userToken.Actived = DateTimeHelper.Now;
                }
                catch (Exception ex)
                {
                    ProcessDisconnected(ex);
                }
            }
            else
                OnError?.Invoke("", new Exception("发送失败：当前连接已断开"));
        }

        public void BeginSend(byte[] data)
        {
            if (Connected)
            {
                _userToken.Socket.BeginSend(data, 0, data.Length, SocketFlags.None, null, null);
            }
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
            return _strean.ReadAsync(buffer, offset, count, cancellationToken);
        }


        public Stream GetStream()
        {
            throw new InvalidOperationException("iocp暂不支持流模式");
        }

        public void Disconnect(Exception ex)
        {
            var mex = ex;

            if (this.Connected)
            {
                try
                {
                    if (_userToken != null && _userToken.Socket != null)
                        _userToken.Socket.Close();
                }
                catch (Exception sex)
                {
                    if (mex != null) mex = sex;
                }
                this.Connected = false;
                if (mex == null)
                {
                    mex = new Exception("当前Socket已主动断开连接！");
                }
                if (_userToken != null)
                    OnDisconnected?.Invoke(_userToken.ID, mex);

                _userToken.Clear();
            }
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
