/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets
*文件名： Class1
*版本号： V1.0.0.0
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
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace SAEA.Sockets.Core
{
    /// <summary>
    /// iocp 客户端 socket
    /// </summary>
    public abstract class BaseClientSocket : IDisposable
    {
        Socket _socket;

        string _ip = string.Empty;

        int _port = 39654;

        ContextFactory _contextFactory;

        IUserToken _userToken;

        SocketAsyncEventArgs _connectArgs;

        bool _connected = false;

        Action<SocketError> _connectCallBack;

        AutoResetEvent _connectEvent = new AutoResetEvent(true);

        public bool Connected { get => _connected; set => _connected = value; }
        public IUserToken UserToken { get => _userToken; private set => _userToken = value; }

        ArgsPool _argsPool;

        public event OnErrorHandler OnError;

        public event OnDisconnectedHandler OnDisconnected;

        protected OnClientReceiveBytesHandler OnClientReceive;

        protected void RaiseOnError(string id, Exception ex)
        {
            OnError?.Invoke(id, ex);
        }

        /// <summary>
        /// iocp 客户端 socket
        /// </summary>
        /// <param name="context"></param>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        public BaseClientSocket(IContext context, string ip = "127.0.0.1", int port = 39654, int bufferSize = 100 * 1024)
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _ip = ip;
            _port = port;

            _contextFactory = new ContextFactory(context, bufferSize, false);

            _argsPool = new ArgsPool(IO_Completed, false);

            OnClientReceive = new OnClientReceiveBytesHandler(OnReceived);

            _connectArgs = new SocketAsyncEventArgs
            {
                RemoteEndPoint = new IPEndPoint(IPAddress.Parse(_ip), _port)
            };
            _connectArgs.Completed += ConnectArgs_Completed;

        }

        /// <summary>
        /// 连接到服务器
        /// </summary>
        /// <param name="callBack"></param>
        public void ConnectAsync(Action<SocketError> callBack = null)
        {
            _connectEvent.WaitOne();
            if (!_connected)
            {
                _connectCallBack = callBack;
                if (!_socket.ConnectAsync(_connectArgs))
                {
                    ProcessConnected(_connectArgs);
                }
            }
        }

        void ConnectArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            ProcessConnected(e);
        }

        void ProcessConnected(SocketAsyncEventArgs e)
        {
            _connectEvent.Set();
            _connected = (e.SocketError == SocketError.Success);
            if (_connected)
            {
                _userToken = _contextFactory.GetUserToken(e.ConnectSocket);
                var readArgs = _argsPool.GetArgs(_userToken, true);
                if (!e.ConnectSocket.ReceiveAsync(readArgs))
                    ProcessReceive(readArgs);
                _connectCallBack?.Invoke(e.SocketError);
            }
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
                    _argsPool.Free(e);
                    Disconnect();
                    break;
            }
        }

        protected abstract void OnReceived(byte[] data);

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

                    if (!_userToken.Socket.ReceiveAsync(e))
                        ProcessReceive(e);
                }
                else
                {
                    _argsPool.Free(e);
                    ProcessDisconnected(new Exception("SocketError:" + e.SocketError.ToString()));
                }
            }
            catch (Exception ex)
            {
                _argsPool.Free(e);
                OnError?.Invoke(_userToken.ID, ex);
                Disconnect();
                ConsoleHelper.WriteLine(ex.Message);
            }
        }

        void ProcessSended(SocketAsyncEventArgs e)
        {
            _userToken.Set();
            _userToken.Actived = DateTimeHelper.Now;
            _argsPool.Free(e);

        }

        void ProcessDisconnected(Exception ex)
        {
            _connected = false;
            _connectEvent.Set();
            try
            {
                if (_userToken.Socket.Connected)
                    _userToken.Socket.Shutdown(SocketShutdown.Send);
            }
            catch { }
            OnDisconnected?.Invoke(_userToken.ID, ex);
        }

        /// <summary>
        /// 异步发送
        /// </summary>
        /// <param name="data"></param>
        protected void SendAsync(byte[] data)
        {
            if (_connected)
            {
                try
                {
                    _userToken.WaitOne();

                    var writeArgs = _argsPool.GetArgs(_userToken);
                    writeArgs.SetBuffer(data, 0, data.Length);

                    if (!_userToken.Socket.SendAsync(writeArgs))
                    {
                        ProcessSended(writeArgs);
                    }
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(_userToken.ID, ex);
                    Disconnect();
                    _userToken.Set();
                }
            }
            else
                OnError?.Invoke("", new Exception("发送失败：当前连接已断开"));
        }

        /// <summary>
        /// 同步发送
        /// </summary>
        /// <param name="data"></param>
        protected void Send(byte[] data)
        {
            if (data == null) return;

            if (_connected)
            {
                var sendNum = 0;

                int offset = 0;

                try
                {

                    while (_connected)
                    {
                        sendNum += _socket.Send(data, offset, data.Length - offset, SocketFlags.None);

                        offset += sendNum;

                        if (sendNum == data.Length)
                        {
                            break;
                        }
                    }
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

        protected void SendTo(IPEndPoint remoteEP, byte[] data)
        {
            if (data == null) return;

            if (_connected)
            {
                var sendNum = 0;

                int offset = 0;

                try
                {
                    while (true)
                    {
                        sendNum += _socket.SendTo(data, offset, data.Length - offset, SocketFlags.None, remoteEP);

                        offset += sendNum;

                        if (sendNum == data.Length)
                        {
                            break;
                        }
                    }
                    if (!_connected)
                        ConnectArgs_Completed(this, _connectArgs);
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

        public void Disconnect(Exception ex = null)
        {
            var mex = ex;

            if (this.Connected)
            {
                try
                {
                    if (_userToken != null && _userToken.Socket != null)
                        _userToken.Socket.Disconnect(true);
                }
                catch (Exception sex)
                {
                    if (mex != null) mex = sex;
                }
                this.Connected = false;
                if (mex == null)
                {
                    mex = new Exception("当前用户已主动断开连接！");
                }
                if (_userToken != null)
                    OnDisconnected?.Invoke(_userToken.ID, mex);

            }
        }

        public void Dispose()
        {
            this.Disconnect();
            try
            {
                _contextFactory.Free(_userToken);
            }
            catch { };

        }


    }
}
