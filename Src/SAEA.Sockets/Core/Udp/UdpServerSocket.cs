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
*类 名 称：UdpServerSocket
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/28 18:01:17
*描述：
*=====================================================================
*修改时间：2020/12/28 18:01:17
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace SAEA.Sockets.Core.Udp
{
    public class UdpServerSocket : IServerSokcet, IDisposable
    {
        const int SIO_UDP_CONNRESET = unchecked((int)0x9800000C);

        const int MaxLength = 65506;

        Socket _udpSocket = null;

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
        public UdpServerSocket(ISocketOption socketOption)
        {
            _sessionManager = new SessionManager(socketOption.Context, socketOption.ReadBufferSize, socketOption.Count, IO_Completed, new TimeSpan(0, 0, socketOption.TimeOut));
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
            if (_udpSocket == null)
            {
                _udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                if (SocketOption.ReusePort)
                    _udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, SocketOption.ReusePort);

                if (SocketOption.Broadcasted)
                {
                    _udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                }

                //设置多播
                if (!string.IsNullOrEmpty(SocketOption.MultiCastHost))
                {
                    _udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);
                    MulticastOption mcastOption = new MulticastOption(IPAddress.Parse(SocketOption.MultiCastHost), IPAddress.Parse(SocketOption.IP));
                    _udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, mcastOption);
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _udpSocket.IOControl(SIO_UDP_CONNRESET, new byte[4], new byte[4]);
                }

                if (SocketOption.UseIPV6)
                {
                    if (string.IsNullOrEmpty(SocketOption.IP))
                        _udpSocket.Bind(new IPEndPoint(IPAddress.IPv6Any, SocketOption.Port));
                    else
                        _udpSocket.Bind(new IPEndPoint(IPAddress.Parse(SocketOption.IP), SocketOption.Port));
                }
                else
                {
                    if (string.IsNullOrEmpty(SocketOption.IP))
                        _udpSocket.Bind(new IPEndPoint(IPAddress.Any, SocketOption.Port));
                    else
                        _udpSocket.Bind(new IPEndPoint(IPAddress.Parse(SocketOption.IP), SocketOption.Port));
                }

                _udpSocket.SendBufferSize = SocketOption.WriteBufferSize;
                _udpSocket.ReceiveBufferSize = SocketOption.ReadBufferSize;

                ProcessReceive(null);
            }
        }

        private void IO_Completed(object sender, SocketAsyncEventArgs e)
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
            if (readArgs == null)
            {
                readArgs = _sessionManager.GetArg();
            }

            System.Threading.Tasks.Task.Run(() => {
                var buffer = new byte[24];
                EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                _udpSocket.ReceiveFrom(buffer, SocketFlags.None, ref remoteEP);

                if (!_udpSocket.ReceiveFromAsync(readArgs))
                {
                    ProcessReceived(readArgs);
                }
            });

            
        }

        /// <summary>
        /// 处理接收到数据
        /// </summary>
        /// <param name="readArgs"></param>
        void ProcessReceived(SocketAsyncEventArgs readArgs)
        {
            var userToken = (IUserToken)readArgs.UserToken;

            if (userToken == null)
            {
                userToken = _sessionManager.BindUserToken(readArgs, _udpSocket);

                OnAccepted?.Invoke(userToken);
            }

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

                writeArgs.RemoteEndPoint = userToken.ReadArgs.RemoteEndPoint;

                writeArgs.SetBuffer(data, 0, data.Length);

                if (!userToken.Socket.SendToAsync(writeArgs))
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
                    sendNum += userToken.Socket.SendTo(data, offset, data.Length - offset, SocketFlags.None, userToken.ReadArgs.RemoteEndPoint);

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

                return userToken.Socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, userToken.ReadArgs.RemoteEndPoint, null, null);
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

            if (userToken != null && userToken.Socket != null)
            {
                _sessionManager.Active(userToken.ID);

                Send(userToken, data);

                Disconnect(userToken);
            }
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
                if (ex == null) ex = new KernelException("The remote client has been closed.");
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
                _udpSocket.Close(10 * 1000);
            }
            catch { }
            try
            {
                _sessionManager.Clear();
            }
            catch { }
            try
            {
                _udpSocket.Dispose();
                _udpSocket = null;
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
