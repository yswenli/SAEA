/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Sockets.Core.Tcp
*文件名： IocpServerSocket
*版本号： v26.4.23.1
*唯一标识：428f30fc-7c1c-4a82-a168-5af3b03cc7fc
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/01/23 11:16:53
*描述：IocpServerSocket接口
*
*=====================================================================
*修改标记
*修改时间：2019/01/23 11:16:53
*修改人： yswenli
*版本号： v26.4.23.1
*描述：IocpServerSocket接口
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets.Core.Tcp
{
    /// <summary>
    /// iocp 服务器 socket
    /// 支持使用自定义 IContext 来扩展
    /// </summary>
    public class IocpServerSocket : IServerSocket, IDisposable
    {
        // 监听Socket
        Socket _listener = null;

        // 客户端连接数量
        int _clientCounts;

        // 会话管理器
        private SessionManager _sessionManager;

        // 获取会话管理器
        public SessionManager SessionManager
        {
            get { return _sessionManager; }
        }

        // 是否已释放资源
        public bool IsDisposed
        {
            get; set;
        } = false;

        // 获取客户端连接数量
        public int ClientCounts { get => _clientCounts; private set => _clientCounts = value; }

        // Socket选项
        public ISocketOption SocketOption { get; set; }

        #region events

        // 客户端连接事件
        public event OnAcceptedHandler OnAccepted;

        // 错误事件
        public event OnErrorHandler OnError;

        // 客户端断开连接事件
        public event OnDisconnectedHandler OnDisconnected;

        /// <summary>
        /// 接收数据事件（Span 版本）。data 仅在回调期间有效。
        /// </summary>
        public event OnServerReceiveSpanHandler OnServerReceiveSpan;

        #endregion

        /// <summary>
        /// iocp 服务器 socket
        /// </summary>>
        /// <param name="socketOption"></param>
        public IocpServerSocket(ISocketOption socketOption)
        {
            // 初始化会话管理器
            _sessionManager = new SessionManager(socketOption.Context,
                socketOption.ReadBufferSize,
                socketOption.MaxConnects, IO_Completed,
                new TimeSpan(0, 0, 0, 0, socketOption.FreeTime));
            _sessionManager.OnTimeOut += _sessionManager_OnTimeOut;
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
                IPEndPoint ipEndPoint;

                if (SocketOption.UseIPV6)
                {
                    _listener = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
                    if (string.IsNullOrEmpty(SocketOption.IP))
                        ipEndPoint = (new IPEndPoint(IPAddress.IPv6Any, SocketOption.Port));
                    else
                        ipEndPoint = (new IPEndPoint(IPAddress.Parse(SocketOption.IP), SocketOption.Port));
                }
                else
                {
                    _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    if (string.IsNullOrEmpty(SocketOption.IP))
                        ipEndPoint = (new IPEndPoint(IPAddress.Any, SocketOption.Port));
                    else
                        ipEndPoint = (new IPEndPoint(IPAddress.Parse(SocketOption.IP), SocketOption.Port));
                }
                if (SocketOption.ReusePort)
                    _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, SocketOption.ReusePort);
                _listener.NoDelay = SocketOption.NoDelay;
                _listener.SendBufferSize = SocketOption.WriteBufferSize;
                _listener.ReceiveBufferSize = SocketOption.ReadBufferSize;
                _listener.Bind(ipEndPoint);
                _listener.Listen(backlog);

                ProcessAccept(null);
            }
        }

        private void AccepteArgs_Completed(object sender, SocketAsyncEventArgs acceptArgs)
        {
            if (acceptArgs.LastOperation == SocketAsyncOperation.Accept)
            {
                ProcessAccepted(acceptArgs);
            }
        }

        private void ProcessAccept(SocketAsyncEventArgs acceptArgs)
        {
            if (acceptArgs == null)
            {
                acceptArgs = new SocketAsyncEventArgs();
                acceptArgs.Completed += new EventHandler<SocketAsyncEventArgs>(AccepteArgs_Completed);
            }
            else
            {
                acceptArgs.AcceptSocket = null;
            }
            try
            {
                if (!IsDisposed && _listener != null && acceptArgs.SocketError == SocketError.Success)
                {
                    if (!_listener.AcceptAsync(acceptArgs))
                        ProcessAccepted(acceptArgs);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke("IocpServerSocket.ProcessAccepte", ex);
            }
        }

        private void ProcessAccepted(SocketAsyncEventArgs acceptArgs)
        {
            try
            {
                var socket = acceptArgs.AcceptSocket;

                if (socket == null || socket.Connected == false)
                {
                    return;
                }

                socket.NoDelay = SocketOption.NoDelay;

                socket.ReceiveBufferSize = SocketOption.ReadBufferSize;

                socket.SendBufferSize = SocketOption.WriteBufferSize;

                socket.ReceiveTimeout = socket.SendTimeout = SocketOption.ActionTimeout;

                var userToken = _sessionManager.BindUserToken(socket, SocketOption.ConnectTimeout);

                if (userToken == null)
                {
                    return;
                }

                var readArgs = userToken.ReadArgs;

                Interlocked.Increment(ref _clientCounts);

                OnAccepted?.Invoke(userToken);

                ProcessReceive(readArgs);
            }
            catch (Exception ex)
            {
                OnError?.Invoke("IocpServerSocket.ProcessAccepted", ex);
            }
            finally
            {
                ProcessAccept(acceptArgs);
            }
        }

        private void IO_Completed(object sender, SocketAsyncEventArgs e)
        {
            var userToken = (IUserToken)e.UserToken;
            try
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
                        Disconnect(userToken, new KernelException("Operation-exceptions，SocketAsyncOperation：" + e.LastOperation));
                        break;
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke("", ex);
                Disconnect(userToken, ex);
            }
        }

        /// <summary>
        /// 处理接收数据
        /// </summary>
        /// <param name="readArgs"></param>
        private void ProcessReceive(SocketAsyncEventArgs readArgs)
        {
            if (readArgs == null) return;
            var userToken = (IUserToken)readArgs.UserToken;
            try
            {
                if (userToken == null) return;
                if (userToken.Socket != null && userToken.Socket.Connected)
                {
                    lock (readArgs)
                    {
                        if (userToken.Socket != null && userToken.Socket.Connected)
                        {
                            bool willRaiseEvent;
                            try
                            {
                                willRaiseEvent = userToken.Socket.ReceiveAsync(readArgs);
                            }
                            catch (InvalidOperationException)
                            {
                                return;
                            }
                            if (!willRaiseEvent)
                            {
                                ThreadPool.QueueUserWorkItem((state) =>
                                {
                                    ProcessReceived(readArgs);
                                });
                            }
                        }
                        else
                        {
                            Disconnect(userToken, new KernelException("The remote client has been disconnected."));
                        }
                    }
                }
                else
                {
                    Disconnect(userToken, new KernelException("The remote client has been disconnected."));
                }
            }
            catch (Exception exp)
            {
                var kex = new KernelException("An exception occurs when a message is received:" + exp.Message, exp);
                LogHelper.Error($"An exception occurs when receiving:{userToken?.ID}", kex);
                OnError?.Invoke(userToken?.ID, kex);
            }
        }

        /// <summary>
        /// 处理接收到数据
        /// </summary>
        /// <param name="readArgs"></param>
        void ProcessReceived(SocketAsyncEventArgs readArgs)
        {
            try
            {
                if (readArgs == null || readArgs.UserToken == null) return;

                if (readArgs.BytesTransferred > 0 && readArgs.SocketError == SocketError.Success)
                {
                    var userToken = readArgs.UserToken as IUserToken;
                    try
                    {
                        userToken.Actived = DateTimeHelper.Now;
                        var dataSpan = readArgs.Buffer.AsSpan().Slice(readArgs.Offset, readArgs.BytesTransferred);
                        _sessionManager.Active(userToken.ID);

                        OnServerReceiveSpan?.Invoke(userToken, dataSpan);
                    }
                    catch (Exception ex)
                    {
                        OnError?.Invoke(userToken?.ID ?? "", ex);
                        LogHelper.Error($"An exception occurs when receiving:{userToken?.ID}", ex);
                    }
                    ProcessReceive(readArgs);
                }
                else
                {
                    try { Disconnect(readArgs.UserToken as IUserToken); } catch { }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke("", ex);
            }
        }

        void ProcessSended(SocketAsyncEventArgs e)
        {
            try
            {
                var token = e.UserToken as IUserToken;
                if (token == null) return;
                lock (token)
                {
                    if (!token.IsSending) return;
                    token.IsSending = false;
                    var owner = token.TakeSendingOwner();
                    if (owner != null)
                    {
                        try { owner.Dispose(); } catch { }
                    }
                    token.Actived = DateTimeHelper.Now;
                    token.ReleaseWrite();
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke("", ex);
            }
        }

        #region send method

        private void SendAsyncRaw(IUserToken userToken, ArraySegment<byte> seg, IDisposable owner)
        {
            if (userToken == null || seg.Array == null || seg.Count == 0)
            {
                owner?.Dispose();
                return;
            }
            bool transferred = false;
            try
            {
                try { _sessionManager.Active(userToken.ID); } catch { }
                if (userToken.WaitWrite(SocketOption.ActionTimeout) && userToken.Socket != null && userToken.Socket.Connected)
                {
                    var writeArgs = userToken.WriteArgs;
                    if (writeArgs != null)
                    {
                        userToken.SendingOwner = owner;
                        userToken.IsSending = true;
                        transferred = true;
                        writeArgs.SetBuffer(seg.Array, seg.Offset, seg.Count);
                        bool asyncPending = userToken.Socket.SendAsync(writeArgs);
                        if (!asyncPending)
                        {
                            ProcessSended(writeArgs);
                        }
                        else
                        {
                            Task.Run(async () =>
                            {
                                await Task.Delay(SocketOption.ActionTimeout);
                                lock (userToken)
                                {
                                    if (userToken.IsSending)
                                    {
                                        AbandonSendingOwner(userToken);
                                    }
                                }
                            });
                        }
                    }
                }
                else
                {
                    OnError?.Invoke($"An exception occurs when a message is sending:{userToken?.ID}", new TimeoutException("Sending data timeout"));
                }
            }
            catch (Exception ex)
            {
                if (transferred)
                {
                    ProcessSended(userToken.WriteArgs);
                }
                else
                {
                    owner?.Dispose();
                    transferred = true;
                }
                OnError?.Invoke($"An exception occurs when a message is sending:{userToken?.ID}", ex);
            }
            finally
            {
                if (!transferred) owner?.Dispose();
            }
        }

        /// <summary>
        /// 发送超时后原子摘除发送缓冲所有权，但不释放它。
        /// 旧的异步发送可能仍在读取该缓冲，此时归还池化数组会被再次租用并覆写，造成线上数据损坏；
        /// 因此这里主动放弃所有权交由 GC 回收（少量复用损失优于数据损坏），
        /// 同时避免稍后到达的完成回调误释放一次新的发送缓冲。
        /// </summary>
        private static void AbandonSendingOwner(IUserToken userToken)
        {
            userToken.TakeSendingOwner();
        }

        public void Send(string sessionID, ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) return;
            var userToken = _sessionManager.Get(sessionID);
            if (userToken == null) return;
            var writer = new PooledBufferWriter(data.Length);
            data.CopyTo(writer.GetSpan(data.Length));
            writer.Advance(data.Length);
            writer.TryGetArray(out var rented);
            SendAsyncRaw(userToken, rented, writer);
        }

        public void SendAsync(string sessionID, ReadOnlyMemory<byte> data)
        {
            if (data.Length == 0) return;
            var userToken = _sessionManager.Get(sessionID);
            if (userToken == null) return;
            if (MemoryMarshal.TryGetArray(data, out var seg))
            {
                SendAsyncRaw(userToken, seg, null);
                return;
            }
            var writer = new PooledBufferWriter(data.Length);
            data.Span.CopyTo(writer.GetSpan(data.Length));
            writer.Advance(data.Length);
            writer.TryGetArray(out var rented);
            SendAsyncRaw(userToken, rented, writer);
        }

        public void SendAsync(string sessionID, ReadOnlyMemory<byte> data, IDisposable owner)
        {
            var userToken = _sessionManager.Get(sessionID);
            if (userToken == null)
            {
                owner?.Dispose();
                throw new KernelException("Failed to send data,current session does not exist！");
            }
            SendAsync(userToken, data, owner);
        }

        void SendAsync(IUserToken userToken, ReadOnlyMemory<byte> data, IDisposable owner)
        {
            if (data.Length == 0)
            {
                owner?.Dispose();
                return;
            }
            ArraySegment<byte> rented;
            if (MemoryMarshal.TryGetArray(data, out var segment) && segment.Array != null)
            {
                SendAsyncRaw(userToken, segment, owner);
                return;
            }
            PooledBufferWriter writer = null;
            try
            {
                writer = new PooledBufferWriter(data.Length);
                data.Span.CopyTo(writer.GetSpan(data.Length));
                writer.Advance(data.Length);
            }
            catch
            {
                if (writer != null)
                {
                    writer.Dispose();
                    writer = null;
                }
                owner?.Dispose();
                throw;
            }
            if (!writer.TryGetArray(out rented) || rented.Array == null)
            {
                writer.Dispose();
                writer = null;
                owner?.Dispose();
                return;
            }
            owner?.Dispose();
            SendAsyncRaw(userToken, rented, writer);
        }

        public void SendAsync(string sessionID, ISocketProtocal protocal)
        {
            if (protocal == null) return;
            var userToken = _sessionManager.Get(sessionID);
            if (userToken == null) return;
            var coder = userToken.Coder;
            if (coder == null)
            {
                OnError?.Invoke(userToken?.ID ?? "", new InvalidOperationException("SAEA SocketError:coder 未初始化"));
                return;
            }
            var bodyLen = protocal.BodyLength;
            var size = bodyLen > 0 && bodyLen < int.MaxValue - 64 ? (int)bodyLen + 64 : 64;
            var writer = new PooledBufferWriter(size);
            try
            {
                coder.Encode(protocal, writer);
            }
            catch (Exception ex)
            {
                writer.Dispose();
                OnError?.Invoke(userToken?.ID ?? "", ex);
                return;
            }
            writer.TryGetArray(out var rented);
            SendAsyncRaw(userToken, rented, writer);
        }

        public void End(string sessionID, ReadOnlyMemory<byte> data)
        {
            var userToken = _sessionManager.Get(sessionID);
            if (userToken == null) return;
            try
            {
                if (userToken.Socket != null && userToken.Socket.Connected && data.Length > 0)
                {
                    var copy = data.ToArray();
                    var writeArgs = userToken.WriteArgs;
                    if (writeArgs != null && userToken.WaitWrite(SocketOption.ActionTimeout))
                    {
                        writeArgs.SetBuffer(copy, 0, copy.Length);
                        userToken.IsSending = true;
                        if (!userToken.Socket.SendAsync(writeArgs)) ProcessSended(writeArgs);
                    }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(userToken?.ID ?? "", ex);
            }
            Disconnect(userToken);
        }

        public void SendAsync(IPEndPoint ipEndPoint, ReadOnlyMemory<byte> data)
        {
            if (ipEndPoint == null) return;
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
            try
            {
                if (userToken == null || string.IsNullOrEmpty(userToken.ID)) return;
                // Capture the id before Free() resets userToken.ID to null.
                var sessionId = userToken.ID;
                if (_sessionManager.Free(userToken))
                {
                    Interlocked.Decrement(ref _clientCounts);
                    OnDisconnected?.Invoke(sessionId, ex);
                }
            }
            catch (Exception e)
            {
                LogHelper.Error($"An exception occurs when disconnecting:{userToken?.ID}", e);
                OnError?.Invoke("", e);
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        /// <param name="sessionID"></param>
        public void Disconnect(string sessionID)
        {
            if (string.IsNullOrEmpty(sessionID))
            {
                return;
            }
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
                _listener?.Close(10 * 1000);
            }
            catch { }
            try
            {
                _sessionManager.Clear();
            }
            catch { }
            try
            {
                _listener?.Dispose();
                _listener = null;
            }
            catch { }
        }

        public void Dispose()
        {
            try
            {
                try { Stop(); } catch { }
            }
            catch (Exception ex)
            {
                OnError?.Invoke("", ex);
            }
        }
    }
}