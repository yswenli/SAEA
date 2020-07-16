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
*版本号： v5.0.0.1
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
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
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

        IUserToken _userToken;

        SocketAsyncEventArgs _connectArgs;

        UserTokenFactory _userTokenFactory;

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
        public IocpClientSocket(ISocketOption socketOption)
        {
            _SocketOption = socketOption;

            _userTokenFactory = new UserTokenFactory();

            _socket = new Socket(AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Stream, ProtocolType.Tcp);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, _SocketOption.ReusePort);
            _socket.NoDelay = _SocketOption.NoDelay;
            _socket.SendTimeout = _socket.ReceiveTimeout = _SocketOption.TimeOut;
            _socket.KeepAlive();

            OnClientReceive = new OnClientReceiveBytesHandler(OnReceived);

            _connectArgs = new SocketAsyncEventArgs
            {
                RemoteEndPoint = new IPEndPoint(IPAddress.Parse(_SocketOption.IP), _SocketOption.Port)
            };
            _connectArgs.Completed += ConnectArgs_Completed;

            _userToken = _userTokenFactory.Create(socketOption.Context, socketOption.ReadBufferSize, IO_Completed);
        }

        /// <summary>
        /// iocp 客户端 socket
        /// </summary>
        /// <param name="context"></param>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="bufferSize"></param>
        /// <param name="timeOut"></param>
        public IocpClientSocket(IContext context, string ip = "127.0.0.1", int port = 39654, int bufferSize = 100 * 1024, int timeOut = 60 * 1000) : this(new SocketOption() { Context = context, IP = ip, Port = port, ReadBufferSize = bufferSize, WriteBufferSize = bufferSize, TimeOut = timeOut })
        {

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
        /// <param name="timeOut"></param>
        public void Connect()
        {
            if (!Connected)
            {
                _socket.Connect(_SocketOption.IP, _SocketOption.Port);

                _userToken.ID = _socket.LocalEndPoint.ToString();
                _userToken.Socket = _socket;
                _userToken.Linked = _userToken.Actived = DateTime.Now;

                var readArgs = _userToken.ReadArgs;

                ProcessReceive(readArgs);

                Connected = true;
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
                ProcessReceive(readArgs);
            }
            _connectCallBack?.Invoke(e.SocketError);
            _connectEvent.Set();
        }


        void IO_Completed(object sender, SocketAsyncEventArgs e)
        {
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    ProcessReceived(e);
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
                if (userToken != null && userToken.Socket != null && userToken.Socket.Connected)
                {
                    userToken.WaitOne();

                    var writeArgs = userToken.WriteArgs;

                    writeArgs.SetBuffer(data, 0, data.Length);

                    if (!userToken.Socket.SendAsync(writeArgs))
                    {
                        ProcessSended(writeArgs);
                    }
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
        public void SendAsync(byte[] data)
        {
            SendAsync(UserToken, data);
        }

        /// <summary>
        /// 同步发送
        /// </summary>
        /// <param name="data"></param>
        public void Send(byte[] data)
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
            throw new KernelException("当前方法只在stream实现中才能使用!");
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
                    {
                        try
                        {
                            _userToken.Socket.Shutdown(SocketShutdown.Both);
                        }
                        catch { }
                        _userToken.Socket.Close();
                    }
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
