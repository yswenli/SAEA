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
*文件名： BaseServerSocket
*版本号： V3.6.2.1
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
*版本号： V3.6.2.1
*描述：
*
*****************************************************************************/

using SAEA.Common;
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

        int _clientCounts;

        private SessionManager _sessionManager;

        public SessionManager SessionManager
        {
            get { return _sessionManager; }
        }

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


        /// <summary>
        /// iocp 服务器 socket
        /// </summary>
        /// <param name="context"></param>
        /// <param name="bufferSize"></param>
        /// <param name="count"></param>
        /// <param name="noDelay"></param>
        /// <param name="timeOut"></param>
        public BaseServerSocket(IContext context, int bufferSize = 1024, int count = 10000, bool noDelay = true, int timeOut = 60 * 1000)
        {
            _sessionManager = new SessionManager(context, bufferSize, count, IO_Completed, new TimeSpan(0, 0, timeOut));
            _sessionManager.OnTimeOut += _sessionManager_OnTimeOut;
            OnServerReceiveBytes = new OnServerReceiveBytesHandler(OnReceiveBytes);
            _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.NoDelay = noDelay;
        }
        /// <summary>
        /// 启动服务
        /// </summary>
        /// <param name="port"></param>
        /// <param name="backlog"></param>
        public void Start(int port = 39654, int backlog = 10 * 1000)
        {
            _listener.Bind(new IPEndPoint(IPAddress.Any, port));
            _listener.Listen(backlog);

            var accepteArgs = new SocketAsyncEventArgs();
            accepteArgs.Completed += AccepteArgs_Completed;
            ProcessAccept(accepteArgs);
        }

        private void AccepteArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            if (e.LastOperation == SocketAsyncOperation.Accept)
            {
                ProcessAccepted(e);
            }
        }

        private void ProcessAccept(SocketAsyncEventArgs e)
        {
            e.AcceptSocket = null;
            if (!_listener.AcceptAsync(e))
            {
                ProcessAccepted(e);
            }
        }

        private void ProcessAccepted(SocketAsyncEventArgs e)
        {
            var userToken = _sessionManager.BindtUserToken(e.AcceptSocket);

            var readArgs = userToken.ReadArgs;

            Interlocked.Increment(ref _clientCounts);

            TaskHelper.Start(() => { OnAccepted?.Invoke(userToken); });

            if (!userToken.Socket.ReceiveAsync(readArgs))
            {
                ProcessReceived(readArgs);
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


        /// <summary>
        /// 处理接收到数据
        /// </summary>
        /// <param name="readArgs"></param>
        private void ProcessReceived(SocketAsyncEventArgs readArgs)
        {
            var userToken = (IUserToken)readArgs.UserToken;
            try
            {
                if (readArgs.SocketError == SocketError.Success && readArgs.BytesTransferred > 0)
                {
                    _sessionManager.Active(userToken.ID);

                    var data = new byte[readArgs.BytesTransferred];

                    Buffer.BlockCopy(readArgs.Buffer, readArgs.Offset, data, 0, readArgs.BytesTransferred);

                    OnServerReceiveBytes.Invoke(userToken, data);

                    if (userToken != null && userToken.Socket != null && userToken.Socket.Connected && readArgs != null)
                    {
                        if (!userToken.Socket.ReceiveAsync(readArgs))
                            ProcessReceived(readArgs);
                    }
                    else
                    {
                        Disconnect(userToken, new Exception("The remote client has been disconnected."));
                    }
                }
                else
                {
                    Disconnect(userToken, null);
                }

            }
            catch (Exception ex)
            {
                OnError?.Invoke(userToken.ID, ex);
                Disconnect(userToken, ex);
            }
        }

        private void ProcessSended(SocketAsyncEventArgs e)
        {
            var userToken = (IUserToken)e.UserToken;
            _sessionManager.Active(userToken.ID);
            userToken.WriteArgs.SetBuffer(null, 0, 0);
            userToken.Set();
        }

        #region send method

        /// <summary>
        /// 异步发送
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="data"></param>
        protected void SendAsync(IUserToken userToken, byte[] data)
        {
            userToken.WaitOne();

            var writeArgs = userToken.WriteArgs;

            writeArgs.SetBuffer(data, 0, data.Length);

            if (!userToken.Socket.SendAsync(writeArgs))
            {
                ProcessSended(writeArgs);
            }
        }


        /// <summary>
        /// 异步发送
        /// </summary>
        /// <param name="sessionID"></param>
        /// <param name="data"></param>
        protected void SendAsync(string sessionID, byte[] data)
        {
            var userToken = _sessionManager.Get(sessionID);
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
            try
            {
                _sessionManager.Active(userToken.ID);

                int sendNum = 0, offset = 0;

                while (true)
                {
                    sendNum += userToken.Socket.Send(data, offset, data.Length - offset, SocketFlags.None);

                    offset += sendNum;

                    if (sendNum == data.Length)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Disconnect(userToken, ex);
            }
        }


        /// <summary>
        /// 同步发送
        /// </summary>
        /// <param name="sessionID"></param>
        /// <param name="data"></param>
        protected void Send(string sessionID, byte[] data)
        {
            var userToken = _sessionManager.Get(sessionID);
            if (userToken != null)
            {
                Send(userToken, data);
            }
        }

        /// <summary>
        /// APM方式发送
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        protected IAsyncResult BeginSend(IUserToken userToken, byte[] data)
        {
            try
            {
                _sessionManager.Active(userToken.ID);

                return userToken.Socket.BeginSend(data, 0, data.Length, SocketFlags.None, null, null);

            }
            catch (Exception ex)
            {
                Disconnect(userToken, ex);
            }
            return null;
        }

        /// <summary>
        /// APM方式结束发送
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        protected int EndSend(IUserToken userToken, IAsyncResult result)
        {
            return userToken.Socket.EndSend(result);
        }


        /// <summary>
        /// 回复并关闭连接
        /// 用于http
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="data"></param>
        public void End(IUserToken userToken, byte[] data)
        {
            var result = userToken.Socket.BeginSend(data, 0, data.Length, SocketFlags.None, null, null);
            userToken.Socket.EndSend(result);
            Disconnect(userToken);
        }
        #endregion



        private void _sessionManager_OnTimeOut(IUserToken userToken)
        {
            Disconnect(userToken);
        }

        /// <summary>
        /// 断开客户端连接
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="ex"></param>
        public void Disconnect(IUserToken userToken, Exception ex = null)
        {
            if (_sessionManager.Free(userToken))
            {
                if (ex == null) ex = new Exception("The remote client has been disconnected.");
                Interlocked.Decrement(ref _clientCounts);
                OnDisconnected?.Invoke(userToken.ID, ex);
                userToken = null;
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
                _sessionManager.Clear();
            }
            catch { }
        }
    }
}
