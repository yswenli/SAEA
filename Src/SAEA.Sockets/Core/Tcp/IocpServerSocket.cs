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
*文件名： IocpServerSocket
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
using SAEA.Sockets.Model;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace SAEA.Sockets.Core.Tcp
{
    /// <summary>
    /// iocp 服务器 socket
    /// 支持使用自定义 IContext 来扩展
    /// </summary>
    public class IocpServerSocket : IServerSocket, IDisposable
    {
        Socket _listener = null;

        int _clientCounts;

        private SessionManager _sessionManager;

        public SessionManager SessionManager
        {
            get { return _sessionManager; }
        }

        public bool IsDisposed
        {
            get; set;
        } = false;

        public int ClientCounts { get => _clientCounts; private set => _clientCounts = value; }

        public ISocketOption SocketOption { get; set; }

        #region events

        public event OnAcceptedHandler OnAccepted;

        public event OnErrorHandler OnError;

        public event OnDisconnectedHandler OnDisconnected;

        public event OnReceiveHandler OnReceive;

        #endregion

        /// <summary>
        /// socket收到数据时的代理
        /// </summary>
        private OnServerReceiveBytesHandler OnServerReceiveBytes;

        /// <summary>
        /// iocp 服务器 socket
        /// </summary>>
        /// <param name="socketOption"></param>
        public IocpServerSocket(ISocketOption socketOption)
        {
            _sessionManager = new SessionManager(socketOption.Context, socketOption.ReadBufferSize, socketOption.Count, IO_Completed, new TimeSpan(0, 0, 0, 0, socketOption.TimeOut));
            _sessionManager.OnTimeOut += _sessionManager_OnTimeOut;
            OnServerReceiveBytes = new OnServerReceiveBytesHandler(OnReceiveBytes);
            SocketOption = socketOption;
        }


        /// <summary>
        /// 启动服务
        /// </summary>
        /// <param name="backlog"></param>
        public void Start(int backlog = 10 * 1000)
        {
            if (_listener == null)
            {
                _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                if (SocketOption.ReusePort)
                    _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, SocketOption.ReusePort);

                _listener.NoDelay = SocketOption.NoDelay;

                if (SocketOption.UseIPV6)
                {
                    if (string.IsNullOrEmpty(SocketOption.IP))
                        _listener.Bind(new IPEndPoint(IPAddress.IPv6Any, SocketOption.Port));
                    else
                        _listener.Bind(new IPEndPoint(IPAddress.Parse(SocketOption.IP), SocketOption.Port));
                }
                else
                {
                    if (string.IsNullOrEmpty(SocketOption.IP))
                        _listener.Bind(new IPEndPoint(IPAddress.Any, SocketOption.Port));
                    else
                        _listener.Bind(new IPEndPoint(IPAddress.Parse(SocketOption.IP), SocketOption.Port));
                }

                _listener.SendBufferSize = SocketOption.WriteBufferSize;
                _listener.ReceiveBufferSize = SocketOption.ReadBufferSize;

                _listener.Listen(backlog);

                var accepteArgs = new SocketAsyncEventArgs();

                accepteArgs.Completed += AccepteArgs_Completed;

                ProcessAccept(accepteArgs);
            }
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
            try
            {
                if (!IsDisposed && _listener != null)
                {
                    if (_listener?.AcceptAsync(e) == false)
                        ProcessAccepted(e);
                }
            }
            catch { }
        }

        private void ProcessAccepted(SocketAsyncEventArgs e)
        {
            try
            {
                var socket = e.AcceptSocket;

                socket.NoDelay = SocketOption.NoDelay;

                socket.ReceiveBufferSize = SocketOption.ReadBufferSize;

                socket.SendBufferSize = SocketOption.WriteBufferSize;

                socket.ReceiveTimeout = socket.SendTimeout = SocketOption.TimeOut;

                var userToken = _sessionManager.BindUserToken(socket);

                if (userToken != null)
                {
                    var readArgs = userToken.ReadArgs;

                    Interlocked.Increment(ref _clientCounts);

                    OnAccepted?.Invoke(userToken);

                    ProcessReceive(readArgs);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke("", ex);
            }
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
                        Disconnect(userToken, new KernelException("Operation-exceptions，SocketAsyncOperation：" + e.LastOperation));
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
        protected virtual void OnReceiveBytes(IUserToken userToken, byte[] data)
        {
            OnReceive?.Invoke(userToken, data);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="readArgs"></param>
        private void ProcessReceive(SocketAsyncEventArgs readArgs)
        {
            var userToken = (IUserToken)readArgs.UserToken;
            try
            {
                if (readArgs != null && userToken != null && userToken.Socket != null && userToken.Socket.Connected)
                {
                    if (!userToken.Socket.ReceiveAsync(readArgs))
                        ProcessReceived(readArgs);
                }
                else
                {
                    Disconnect(userToken, new KernelException("The remote client has been disconnected."));
                }

            }
            catch (Exception exp)
            {
                var kex = new KernelException("An exception occurs when a message is received:" + exp.Message, exp);
                OnError?.Invoke(userToken.ID, kex);
                Disconnect(userToken, kex);
            }
        }

        /// <summary>
        /// 处理接收到数据
        /// </summary>
        /// <param name="readArgs"></param>
        void ProcessReceived(SocketAsyncEventArgs readArgs)
        {
            var userToken = (IUserToken)readArgs.UserToken;

            try
            {
                if (readArgs.SocketError == SocketError.Success && readArgs.BytesTransferred > 0)
                {
                    _sessionManager.Active(userToken.ID);

                    var data = readArgs.Buffer.AsSpan().Slice(readArgs.Offset, readArgs.BytesTransferred).ToArray();

                    try
                    {
                        OnServerReceiveBytes.Invoke(userToken, data);
                    }
                    catch (Exception ex)
                    {
                        OnError?.Invoke(userToken.ID, ex);
                    }

                    ProcessReceive(readArgs);
                }
                else
                {
                    Disconnect(userToken, null);
                }
            }
            catch (Exception exp)
            {
                var kex = new KernelException("An exception occurs when a message is received:" + exp.Message, exp);
                OnError?.Invoke(userToken.ID, kex);
                Disconnect(userToken, kex);
            }
        }

        private void ProcessSended(SocketAsyncEventArgs e)
        {
            var userToken = (IUserToken)e.UserToken;
            try
            {
                _sessionManager.Active(userToken.ID);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"An exception occurs when a message is sended:{userToken?.ID}", ex);
            }
            userToken?.Set();
        }

        #region send method

        /// <summary>
        /// 异步发送
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="data"></param>
        public void SendAsync(IUserToken userToken, byte[] data)
        {
            userToken.WaitOne();

            try
            {
                var writeArgs = userToken.WriteArgs;

                writeArgs.SetBuffer(data, 0, data.Length);

                if (!userToken.Socket.SendAsync(writeArgs))
                {
                    ProcessSended(writeArgs);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"An exception occurs when a message is sending:{userToken?.ID}", ex);
            }
        }


        /// <summary>
        /// 异步发送
        /// </summary>
        /// <param name="sessionID"></param>
        /// <param name="data"></param>
        public void SendAsync(string sessionID, byte[] data)
        {
            var userToken = _sessionManager.Get(sessionID);

            if (userToken == null)
            {
                throw new KernelException("Failed to send data,current session does not exist！");
            }
            SendAsync(userToken, data);
        }


        /// <summary>
        /// 同步发送
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="data"></param>
        public void Send(IUserToken userToken, byte[] data)
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
                var kex = new KernelException("An exception occurs when a message is sending:" + ex.Message, ex);
                Disconnect(userToken, kex);
            }
        }


        /// <summary>
        /// 同步发送
        /// </summary>
        /// <param name="sessionID"></param>
        /// <param name="data"></param>
        public void Send(string sessionID, byte[] data)
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
        public IAsyncResult BeginSend(IUserToken userToken, byte[] data)
        {
            try
            {
                _sessionManager.Active(userToken.ID);

                return userToken.Socket.BeginSend(data, 0, data.Length, SocketFlags.None, null, null);
            }
            catch (Exception ex)
            {
                var kex = new KernelException("An exception occurs when a message is sending:" + ex.Message, ex);
                Disconnect(userToken, kex);
            }
            return null;
        }

        /// <summary>
        /// APM方式结束发送
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public int EndSend(IUserToken userToken, IAsyncResult result)
        {
            return userToken.Socket.EndSend(result);
        }

        /// <summary>
        /// 回复并关闭连接
        /// 用于http
        /// </summary>
        /// <param name="sessionID"></param>
        /// <param name="data"></param>
        public void End(string sessionID, byte[] data)
        {
            var userToken = _sessionManager.Get(sessionID);

            if (userToken != null && userToken.Socket != null && userToken.Socket.Connected)
            {
                _sessionManager.Active(userToken.ID);

                Send(userToken, data);

                Disconnect(userToken);
            }
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="ipEndPoint"></param>
        /// <param name="data"></param>
        public void SendAsync(IPEndPoint ipEndPoint, byte[] data)
        {
            SendAsync(ipEndPoint.ToString(), data);
        }
        #endregion



        private void _sessionManager_OnTimeOut(IUserToken userToken)
        {
            Disconnect(userToken);
        }


        public object GetCurrentObj(string sessionID)
        {
            return SessionManager.Get(sessionID);
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
                if (ex == null) ex = new KernelException("The remote client has been disconnected.");
                Interlocked.Decrement(ref _clientCounts);
                OnDisconnected?.Invoke(userToken.ID, ex);
                userToken = null;
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        /// <param name="sessionID"></param>
        public void Disconnecte(string sessionID)
        {
            var userToken = SessionManager.Get(sessionID);
            if (userToken != null)
                Disconnect(userToken);
        }

        /// <summary>
        /// 关闭
        /// </summary>
        public void Stop()
        {

            try
            {
                _listener.Close(10 * 1000);
            }
            catch { }
            try
            {
                _sessionManager.Clear();
            }
            catch { }
            try
            {
                _listener.Dispose();
                _listener = null;
            }
            catch { }
        }

        public void Dispose()
        {
            try
            {
                Stop();
                IsDisposed = true;
            }
            catch { }
        }



    }
}
