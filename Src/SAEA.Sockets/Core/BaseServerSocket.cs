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

using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace SAEA.Sockets.Core
{
    /// <summary>
    /// iocp 服务器 socket
    /// 支持使用自定义 IContext 来扩展
    /// </summary>
    public abstract class BaseServerSocket
    {
        Socket _listener;

        int _bufferSize = 102400;

        ContextFactory _contextFactory;

        ArgsPool _argsPool;

        int _clientCounts;

        public int ClientCounts { get => _clientCounts; private set => _clientCounts = value; }

        #region events

        public event OnAcceptedHandler OnAccepted;

        public event OnErrorHandler OnError;

        public event OnDisconnectedHandler OnDisconnected;

        #endregion

        /// <summary>
        /// socket收到数据时的代理
        /// </summary>
        private OnServerReceiveBytesHandler OnServerReceiveBytes;



        public BaseServerSocket(IContext context, int bufferSize = 100 * 1024, bool cache = false, int count = 10000)
        {
            _bufferSize = bufferSize;

            _contextFactory = new ContextFactory(context, _bufferSize, cache, count);

            _argsPool = new ArgsPool(IO_Completed, cache, count * 2);

            OnServerReceiveBytes = new OnServerReceiveBytesHandler(OnReceiveBytes);
        }


        public void Start(int port = 39654, int backlog = 10 * 1000)
        {
            _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.Bind(new IPEndPoint(IPAddress.Any, port));
            _listener.Listen(backlog);
            ProcessAccept(null);
        }

        private void ProcessAccept(SocketAsyncEventArgs args)
        {
            if (args == null)
            {
                args = new SocketAsyncEventArgs();
                args.Completed += ProcessAccepted;
            }
            else
            {
                args.AcceptSocket = null;
            }
            if (!_listener.AcceptAsync(args))
            {
                ProcessAccepted(_listener, args);
            }
        }

        private void ProcessAccepted(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                var userToken = _contextFactory.GetUserToken(e.AcceptSocket);

                var readArgs = _argsPool.GetArgs(userToken, true);

                SessionManager.Add(userToken);

                try
                {
                    OnAccepted?.Invoke(userToken);
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(userToken.ID, new Exception("server在接受客户端时出现异常：" + ex.Message));
                }

                Interlocked.Increment(ref _clientCounts);

                if (!userToken.Socket.ReceiveAsync(readArgs))
                {
                    ProcessReceived(readArgs);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke("BaseServerSocket.ProcessAccepted", new Exception("server在接受客户端时出现异常：" + ex.Message));
            }
            //接入新的请求
            ProcessAccept(e);
        }


        private void IO_Completed(object sender, SocketAsyncEventArgs e)
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
                    try
                    {
                        var userToken = (IUserToken)e.UserToken;
                        Disconnect(userToken, new Exception("当前操作异常，SocketAsyncOperation：" + e.LastOperation));
                    }
                    catch { }
                    break;
            }
        }

        /// <summary>
        /// 收到服务器收到数据时的处理方法
        /// 需要继承者自行实现具体逻辑
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="data"></param>
        protected abstract void OnReceiveBytes(IUserToken userToken, byte[] data);

        private void ProcessReceived(SocketAsyncEventArgs readArgs)
        {
            var userToken = (IUserToken)readArgs.UserToken;
            try
            {
                if (readArgs.SocketError == SocketError.Success && readArgs.BytesTransferred > 0)
                {
                    SessionManager.Active(userToken.ID);

                    var data = new byte[readArgs.BytesTransferred];

                    Buffer.BlockCopy(readArgs.Buffer, readArgs.Offset, data, 0, readArgs.BytesTransferred);

                    OnServerReceiveBytes.Invoke(userToken, data);
                }
                else
                {
                    _argsPool.Free(readArgs);
                    Disconnect(userToken, null);
                }
            }
            catch (Exception ex)
            {
                _argsPool.Free(readArgs);
                OnError?.Invoke(userToken.ID, ex);
                Disconnect(userToken, ex);
            }

            if (userToken != null && userToken.Socket != null && !userToken.Socket.ReceiveAsync(readArgs))
            {
                ProcessReceived(readArgs);
            }
        }

        private void ProcessSended(SocketAsyncEventArgs e)
        {
            var userToken = (IUserToken)e.UserToken;
            SessionManager.Active(userToken.ID);
            _argsPool.Free(e);
        }

        /// <summary>
        /// 异步发送
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="data"></param>
        protected void SendAsync(IUserToken userToken, byte[] data)
        {
            if (userToken == null || userToken.Socket == null || !userToken.Socket.Connected)
            {
                Disconnect(userToken, new Exception("当前连接已被移除"));
                return;
            }

            var writeArgs = _argsPool.GetArgs(userToken);

            writeArgs.SetBuffer(data, 0, data.Length);

            if (!userToken.Socket.SendAsync(writeArgs))
            {
                ProcessSended(writeArgs);
            }
        }

        protected void SendAsync(string sessionID, byte[] data)
        {
            var userToken = SessionManager.Get(sessionID);
            if (userToken != null)
            {
                SendAsync(userToken, data);
            }
        }

        /// <summary>
        /// 同步发送
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="data"></param>
        protected void Send(IUserToken userToken, byte[] data)
        {
            if (userToken == null || userToken.Socket == null || !userToken.Socket.Connected)
            {
                Disconnect(userToken, new Exception("当前连接已被移除"));
                return;
            }

            try
            {
                int sendNum = 0, offset = 0;

                while (userToken != null && userToken.Socket != null && userToken.Socket.Connected)
                {
                    sendNum += userToken.Socket.Send(data, offset, data.Length - offset, SocketFlags.None);

                    offset += sendNum;

                    if (sendNum == data.Length)
                    {
                        break;
                    }
                }


                SessionManager.Active(userToken.ID);
            }
            catch (Exception ex)
            {
                Disconnect(userToken, ex);
            }
        }


        object _locker = new object();

        /// <summary>
        /// 断开客户端连接
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="ex"></param>
        public void Disconnect(IUserToken userToken, Exception ex = null)
        {
            lock (_locker)
            {
                if (userToken != null && userToken.Socket != null)
                {
                    _contextFactory.Free(userToken);
                    OnDisconnected?.Invoke(userToken.ID, ex);
                    SessionManager.Remove(userToken.ID);
                    Interlocked.Decrement(ref _clientCounts);
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
                _listener.Close();
                _contextFactory.Clear();
                SessionManager.Clear();
                _argsPool.Clear();
                _listener.Close(10 * 1000);
            }
            catch { }

        }
    }
}
