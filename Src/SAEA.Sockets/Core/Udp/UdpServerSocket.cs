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
*命名空间：SAEA.Sockets.Core.Udp
*文件名： UdpServerSocket
*版本号： v26.4.23.1
*唯一标识：15d7340f-5df8-4131-b972-1c78d992dfab
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/29 15:50:22
*描述：UdpServerSocket套接字类
*
*=====================================================================
*修改标记
*修改时间：2020/12/29 15:50:22
*修改人： yswenli
*版本号： v26.4.23.1
*描述：UdpServerSocket套接字类
*
*****************************************************************************/
using SAEA.Common.Caching;
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
    public class UdpServerSocket : IServerSocket, IDisposable
    {
        const int SIO_UDP_CONNRESET = unchecked((int)0x9800000C);

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

        /// <summary>
        /// 接收数据事件（Span 版本）。data 仅在回调期间有效。
        /// </summary>
        public event OnServerReceiveSpanHandler OnServerReceiveSpan;

        #endregion

        /// <summary>
        /// iocp 服务器 socket
        /// </summary>>
        /// <param name="socketOption"></param>
        public UdpServerSocket(ISocketOption socketOption)
        {
            _sessionManager = new SessionManager(socketOption.Context, socketOption.ReadBufferSize, socketOption.MaxConnects, IO_Completed, new TimeSpan(0, 0, 0, 0, socketOption.FreeTime));
            _sessionManager.OnTimeOut += _sessionManager_OnTimeOut;
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
                _udpSocket.SendTimeout = _udpSocket.ReceiveTimeout = SocketOption.ActionTimeout;
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
        /// 
        /// </summary>
        /// <param name="readArgs"></param>
        private void ProcessReceive(SocketAsyncEventArgs readArgs)
        {
            var socket = _udpSocket;
            if (socket == null) return;

            IUserToken token;
            if (readArgs == null)
            {
                token = SessionManager.BeginBindUserToken(socket);
            }
            else
            {
                token = (IUserToken)readArgs.UserToken;
            }

            if (token == null) return;

            try
            {
                if (!socket.ReceiveFromAsync(token.ReadArgs))
                {
                    ThreadPool.QueueUserWorkItem((state) =>
                    {
                        ProcessReceived(token.ReadArgs);
                    });
                }
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                OnError?.Invoke(token.ID, ex);
                Disconnect(token, ex);
            }
        }

        /// <summary>
        /// 处理接收到数据
        /// </summary>
        /// <param name="readArgs"></param>
        void ProcessReceived(SocketAsyncEventArgs readArgs)
        {
            var userToken = readArgs?.UserToken as IUserToken;
            if (userToken == null) return;

            try
            {
                if (string.IsNullOrEmpty(userToken.ID))
                    SessionManager.EndBindUserToken(userToken, readArgs.RemoteEndPoint.ToString());

                if (readArgs.SocketError == SocketError.Success && readArgs.BytesTransferred > 0)
                {
                    _sessionManager.Active(userToken.ID);

                    var dataSpan = readArgs.Buffer.AsSpan(readArgs.Offset, readArgs.BytesTransferred);

                    OnServerReceiveSpan?.Invoke(userToken, dataSpan);

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
            var userToken = e.UserToken as IUserToken;
            if (userToken == null) return;
            try
            {
                var owner = userToken.TakeSendingOwner();
                if (owner != null)
                {
                    try { owner.Dispose(); } catch { }
                }
                _sessionManager.Active(userToken.ID);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"An exception occurs when a message is sended:{userToken?.ID}", ex);
            }
            userToken.ReleaseWrite();
        }

        #region send method

        /// <summary>
        /// UDP 异步发送核心：seg 指向的内存由 owner 持有；owner 为 null 表示调用方内存（零拷贝）。
        /// 所有权在发送完成（ProcessSended）或失败路径释放。
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="seg"></param>
        /// <param name="owner"></param>
        private void SendAsyncRaw(IUserToken userToken, ArraySegment<byte> seg, IDisposable owner)
        {
            SendAsyncRaw(userToken, null, seg, owner);
        }

        /// <summary>
        /// UDP 异步发送核心（可显式指定远端地址，用于广播/组播）。
        /// userToken 为 null 时释放 owner 并上报错误；ipEndPoint 为 null 时使用会话的远端地址。
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="ipEndPoint"></param>
        /// <param name="seg"></param>
        /// <param name="owner"></param>
        private void SendAsyncRaw(IUserToken userToken, IPEndPoint ipEndPoint, ArraySegment<byte> seg, IDisposable owner)
        {
            if (userToken == null)
            {
                owner?.Dispose();
                if (ipEndPoint != null)
                {
                    OnError?.Invoke(ipEndPoint.ToString(), new KernelException("Failed to send data,current session does not exist！"));
                }
                return;
            }
            if (seg.Array == null || seg.Count == 0)
            {
                owner?.Dispose();
                return;
            }
            bool acquired = false;
            bool transferred = false;
            try
            {
                try { _sessionManager.Active(userToken.ID); } catch { }
                if (userToken.WaitWrite(SocketOption.ActionTimeout) && userToken.Socket != null)
                {
                    acquired = true;
                    userToken.SendingOwner = owner;
                    transferred = true;
                    var writeArgs = userToken.WriteArgs;
                    writeArgs.RemoteEndPoint = ipEndPoint ?? userToken.ReadArgs.RemoteEndPoint;
                    writeArgs.SetBuffer(seg.Array, seg.Offset, seg.Count);
                    if (!userToken.Socket.SendToAsync(writeArgs))
                    {
                        ProcessSended(writeArgs);
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
                    try { userToken.TakeSendingOwner()?.Dispose(); } catch { }
                }
                else
                {
                    owner?.Dispose();
                    transferred = true;
                }
                OnError?.Invoke($"An exception occurs when a message is sending:{userToken?.ID}", ex);
                if (acquired)
                {
                    try { userToken.ReleaseWrite(); } catch { }
                }
            }
            finally
            {
                if (!transferred) owner?.Dispose();
            }
        }


        /// <summary>
        /// 同步发送数据（按会话）
        /// </summary>
        /// <remarks>
        /// netstandard2.0 没有 Socket.SendTo(ReadOnlySpan&lt;byte&gt;)，非数组内存会先复制一次到池化缓冲（唯一的边界拷贝）。
        /// 若输入为调用方数组（MemoryMarshal.TryGetArray 精确匹配）则零拷贝直发，发送完成前调用方不得修改或复用该内存。
        /// </remarks>
        /// <param name="sessionID"></param>
        /// <param name="data"></param>
        public void Send(string sessionID, ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) return;
            var userToken = _sessionManager.Get(sessionID);
            if (userToken == null) return;
            var writer = new PooledBufferWriter(data.Length);
            try
            {
                if (data.Length > Model.SocketOption.UDPMaxLength) throw new ArgumentOutOfRangeException("Send Incorrect length of data sent");
                data.CopyTo(writer.GetSpan(data.Length));
                writer.Advance(data.Length);
                writer.TryGetArray(out var rented);
                _sessionManager.Active(userToken.ID);
                userToken.Socket.SendTo(rented.Array, rented.Offset, rented.Count, SocketFlags.None, userToken.ReadArgs.RemoteEndPoint);
            }
            catch (Exception ex)
            {
                var kex = new KernelException("An exception occurs when a message is sending:" + ex.Message, ex);
                Disconnect(userToken, kex);
            }
            finally
            {
                writer.Dispose();
            }
        }

        /// <summary>
        /// 异步发送数据（按会话）
        /// </summary>
        /// <remarks>
        /// netstandard2.0 没有 Socket.SendTo(ReadOnlySpan&lt;byte&gt;)，非数组内存会先复制一次到池化缓冲（唯一的边界拷贝）。
        /// 若输入为调用方数组（MemoryMarshal.TryGetArray 精确匹配）则零拷贝直发，发送完成前调用方不得修改或复用该内存。
        /// </remarks>
        /// <param name="sessionID"></param>
        /// <param name="data"></param>
        public void SendAsync(string sessionID, ReadOnlyMemory<byte> data)
        {
            if (data.Length == 0) return;
            var userToken = _sessionManager.Get(sessionID);
            if (userToken == null)
            {
                throw new KernelException("Failed to send data,current session does not exist！");
            }
            if (data.Length > Model.SocketOption.UDPMaxLength) throw new ArgumentException("SendAsync Incorrect length of data sent");
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
            if (data.Length == 0)
            {
                owner?.Dispose();
                return;
            }
            var userToken = _sessionManager.Get(sessionID);
            if (userToken == null)
            {
                owner?.Dispose();
                throw new KernelException("Failed to send data,current session does not exist！");
            }
            if (data.Length > Model.SocketOption.UDPMaxLength)
            {
                owner?.Dispose();
                throw new ArgumentException("SendAsync Incorrect length of data sent");
            }
            ArraySegment<byte> rented;
            if (MemoryMarshal.TryGetArray(data, out var segment) && segment.Array != null)
            {
                SendAsyncRaw(userToken, segment, owner);
                return;
            }
            var writer = new PooledBufferWriter(data.Length);
            try
            {
                data.Span.CopyTo(writer.GetSpan(data.Length));
                writer.Advance(data.Length);
                if (!writer.TryGetArray(out rented) || rented.Array == null)
                {
                    writer.Dispose();
                    owner?.Dispose();
                    return;
                }
            }
            catch
            {
                writer.Dispose();
                owner?.Dispose();
                throw;
            }
            owner?.Dispose();
            SendAsyncRaw(userToken, rented, writer);
        }

        /// <summary>
        /// 异步发送协议对象（按会话）
        /// </summary>
        /// <remarks>
        /// 编码结果写入池化缓冲；netstandard2.0 没有 Socket.SendTo(ReadOnlySpan&lt;byte&gt;)，
        /// 因此编码完成后必然发生一次到池化缓冲的边界拷贝（编码本身即写入该缓冲）。
        /// </remarks>
        /// <param name="sessionID"></param>
        /// <param name="protocal"></param>
        public void SendAsync(string sessionID, ISocketProtocal protocal)
        {
            if (protocal == null) return;
            var userToken = _sessionManager.Get(sessionID);
            if (userToken?.Coder == null) return;
            var bodyLen = protocal.BodyLength;
            var size = bodyLen > 0 && bodyLen < int.MaxValue - 64 ? (int)bodyLen + 64 : 64;
            var writer = new PooledBufferWriter(size);
            try
            {
                userToken.Coder.Encode(protocal, writer);
            }
            catch (Exception ex)
            {
                writer.Dispose();
                OnError?.Invoke(userToken?.ID ?? "", ex);
                return;
            }
            if (writer.WrittenCount > Model.SocketOption.UDPMaxLength)
            {
                writer.Dispose();
                throw new ArgumentException("SendAsync Incorrect length of data sent");
            }
            writer.TryGetArray(out var rented);
            SendAsyncRaw(userToken, rented, writer);
        }

        /// <summary>
        /// 回复并关闭会话
        /// </summary>
        /// <remarks>
        /// netstandard2.0 没有 Socket.SendTo(ReadOnlySpan&lt;byte&gt;)，非数组内存会先复制一次到池化缓冲（唯一的边界拷贝）。
        /// 若输入为调用方数组（MemoryMarshal.TryGetArray 精确匹配）则零拷贝直发，发送完成前调用方不得修改或复用该内存。
        /// </remarks>
        /// <param name="sessionID"></param>
        /// <param name="data"></param>
        public void End(string sessionID, ReadOnlyMemory<byte> data)
        {
            if (data.Length == 0) return;
            var userToken = _sessionManager.Get(sessionID);
            if (userToken != null && userToken.Socket != null)
            {
                _sessionManager.Active(userToken.ID);
                Send(sessionID, data.Span);
                Disconnect(userToken);
            }
        }

        /// <summary>
        /// 异步发送数据到指定地址（广播/组播）
        /// </summary>
        /// <remarks>
        /// netstandard2.0 没有 Socket.SendTo(ReadOnlySpan&lt;byte&gt;)，非数组内存会先复制一次到池化缓冲（唯一的边界拷贝）。
        /// 若输入为调用方数组（MemoryMarshal.TryGetArray 精确匹配）则零拷贝直发，发送完成前调用方不得修改或复用该内存。
        /// </remarks>
        /// <param name="ipEndPoint"></param>
        /// <param name="data"></param>
        public void SendAsync(IPEndPoint ipEndPoint, ReadOnlyMemory<byte> data)
        {
            if (data.Length == 0) return;
            if (data.Length > Model.SocketOption.UDPMaxLength) throw new ArgumentException("SendAsync Incorrect length of data sent");
            var userToken = SessionManager.Get(ipEndPoint.ToString());
            var remoteEndPoint = new IPEndPoint(ipEndPoint.Address, SocketOption.Port);
            if (MemoryMarshal.TryGetArray(data, out var seg))
            {
                SendAsyncRaw(userToken, remoteEndPoint, seg, null);
                return;
            }
            var writer = new PooledBufferWriter(data.Length);
            data.Span.CopyTo(writer.GetSpan(data.Length));
            writer.Advance(data.Length);
            writer.TryGetArray(out var rented);
            SendAsyncRaw(userToken, remoteEndPoint, rented, writer);
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

                if (userToken != null && !string.IsNullOrEmpty(userToken.ID))
                    OnDisconnected?.Invoke(userToken.ID, ex);
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        /// <param name="sessionID"></param>
        public void Disconnect(string sessionID)
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
            Socket socket = null;
            try { socket = Interlocked.Exchange(ref _udpSocket, null); } catch { }
            try { socket?.Dispose(); } catch { }
            try { _sessionManager.Clear(); } catch { }
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
