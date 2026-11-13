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
*文件名： SaeaUdpServerSocket
*版本号： v26.4.23.1
*唯一标识：15d7340f-5df8-4131-b972-1c78d992dfab
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/29 15:50:22
*描述：SaeaUdpServerSocket套接字类
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 13:29:37
*修改人： yswenli
*版本号： v26.11.5.1
*描述：接收/发送提交前登记在途操作，完成点配对结束，配合令牌两阶段回收
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Sockets.Base;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets.Core.Udp
{
    /// <summary>
    /// UDP 服务器 socket：单套接字接收全部客户端数据报，按远端地址区分会话，仅事件（包）模式收发
    /// </summary>
    public class SaeaUdpServerSocket : IServerSocket, IDisposable
    {
        const int SIO_UDP_CONNRESET = unchecked((int)0x9800000C);

        Socket _udpSocket = null;

        int _clientCounts;

        private SessionManager _sessionManager;

        /// <summary>
        /// 会话管理器，按远端地址维护令牌与会话缓存
        /// </summary>
        public SessionManager SessionManager
        {
            get { return _sessionManager; }
        }

        /// <summary>
        /// 是否已释放资源
        /// </summary>
        public bool IsDisposed
        {
            get; set;
        } = false;

        /// <summary>
        /// 当前已连接的客户端数量
        /// </summary>
        public int ClientCounts { get => _clientCounts; private set => _clientCounts = value; }

        /// <summary>
        /// 本服务器使用的套接字选项
        /// </summary>
        public ISocketOption SocketOption { get; set; }

        #region events

        /// <summary>
        /// 会话建立事件，预留接口，当前实现不会触发
        /// </summary>
        public event OnAcceptedHandler OnAccepted;

        /// <summary>
        /// 收发或内部处理异常时触发，参数为会话标识与异常
        /// </summary>
        public event OnErrorHandler OnError;

        /// <summary>
        /// 会话释放后触发，参数为会话标识与断开原因
        /// </summary>
        public event OnDisconnectedHandler OnDisconnected;

        /// <summary>
        /// 接收数据事件（Span 版本）。data 仅在回调期间有效。
        /// </summary>
        public event OnServerReceiveSpanHandler OnServerReceiveSpan;

        /// <summary>
        /// 接收数据事件（Span 版本），并给出该数据报的来源地址。地址与 data 均仅在回调期间有效。
        /// </summary>
        public event OnServerReceiveSpanFromHandler OnServerReceiveSpanFrom;

        #endregion

        /// <summary>
        /// 套接字实际绑定的本地端点；未启动或已停止时为 null
        /// </summary>
        public IPEndPoint LocalEndPoint
        {
            get
            {
                var socket = _udpSocket;
                if (socket == null) return null;
                return socket.LocalEndPoint as IPEndPoint;
            }
        }

        /// <summary>
        /// saea 服务器 socket
        /// </summary>>
        /// <param name="socketOption"></param>
        public SaeaUdpServerSocket(ISocketOption socketOption)
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
                // 先按配置决定地址族再建 socket：顺序反了会让 IPv6 配置拿到 IPv4 套接字，Bind 时必然失败
                var addressFamily = SocketOption.UseIPV6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork;
                _udpSocket = new Socket(addressFamily, SocketType.Dgram, ProtocolType.Udp);

                if (SocketOption.ReusePort)
                    _udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, SocketOption.ReusePort);

                if (SocketOption.Broadcasted)
                {
                    _udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                }

                //设置多播（选项级别必须与地址族一致，IPv6 组播用 SocketOptionLevel.IPv6）
                if (!string.IsNullOrEmpty(SocketOption.MultiCastHost))
                {
                    var multicastLevel = addressFamily == AddressFamily.InterNetworkV6 ? SocketOptionLevel.IPv6 : SocketOptionLevel.IP;
                    _udpSocket.SetSocketOption(multicastLevel, SocketOptionName.MulticastLoopback, true);
                    MulticastOption mcastOption = new MulticastOption(IPAddress.Parse(SocketOption.MultiCastHost), IPAddress.Parse(SocketOption.IP));
                    _udpSocket.SetSocketOption(multicastLevel, SocketOptionName.AddMembership, mcastOption);
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _udpSocket.IOControl(SIO_UDP_CONNRESET, new byte[4], new byte[4]);
                }

                if (SocketOption.UseIPV6)
                {
                    // 只在绑定 IPv6Any 时启用双栈，指定具体地址时保持单栈语义
                    if (SocketOption.DualMode && string.IsNullOrEmpty(SocketOption.IP))
                        _udpSocket.DualMode = true;

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

            // UDP 会话令牌同样来自池并会被复用：回收已请求时不得再提交，
            // 否则下一次复用的 SetBuffer 会因 args 仍在使用而抛异常并丢掉令牌。
            var baseToken = token as BaseUserToken;
            if (baseToken != null && !baseToken.TryBeginOperation())
            {
                return;
            }

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
            catch (ObjectDisposedException)
            {
                // 提交未成功：回滚在途计数，否则该令牌永远回不到 0、无法入池
                SessionManager.CompleteOperation(token);
            }
            catch (Exception ex)
            {
                SessionManager.CompleteOperation(token);
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

                    // readArgs.RemoteEndPoint 会被下一次接收复用，先复制出独立端点再交给回调
                    var from = readArgs.RemoteEndPoint as IPEndPoint;
                    var onReceiveSpanFrom = OnServerReceiveSpanFrom;
                    if (from != null && onReceiveSpanFrom != null)
                        onReceiveSpanFrom(new IPEndPoint(from.Address, from.Port), dataSpan);

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
            finally
            {
                // 与 ProcessReceive 的 TryBeginOperation 配对；延迟回收的令牌在计数归零时入池
                SessionManager.CompleteOperation(userToken);
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
            finally
            {
                userToken.ReleaseWrite();
                // 与 SendAsyncRaw 的 TryBeginOperation 配对
                SessionManager.CompleteOperation(userToken);
            }
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

                    // 回收已请求时不得提交：否则复用时 writeArgs 仍在途，令牌无法入池
                    var baseToken = userToken as BaseUserToken;
                    if (baseToken != null && !baseToken.TryBeginOperation())
                    {
                        try { userToken.TakeSendingOwner()?.Dispose(); } catch { }
                        try { userToken.ReleaseWrite(); } catch { }
                        acquired = false;
                        return;
                    }

                    try
                    {
                        if (!userToken.Socket.SendToAsync(writeArgs))
                        {
                            ProcessSended(writeArgs);
                        }
                    }
                    catch
                    {
                        // 提交失败要回滚在途计数，否则令牌永远无法入池
                        SessionManager.CompleteOperation(userToken);
                        throw;
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

        /// <summary>
        /// 向指定会话异步发送并转移缓冲所有权；数据为空只释放 owner，会话不存在或超过 UDP 单包上限时释放 owner 后抛异常。
        /// UDP 保持原有的投递即返回语义，返回的 Task 仅用于对齐接口签名。
        /// </summary>
        /// <param name="sessionID">会话标识</param>
        /// <param name="data">待发送数据</param>
        /// <param name="owner">data 所占缓冲的所有权对象，可为 null</param>
        /// <returns>已投递时为完成状态的任务</returns>
        /// <exception cref="KernelException">会话不存在</exception>
        /// <exception cref="ArgumentException">数据长度超过单包上限</exception>
        public Task SendAsync(string sessionID, ReadOnlyMemory<byte> data, IDisposable owner)
        {
            if (data.Length == 0)
            {
                owner?.Dispose();
                return Task.CompletedTask;
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
                return Task.CompletedTask;
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
                return Task.CompletedTask;
            }
            owner?.Dispose();
            SendAsyncRaw(userToken, rented, writer);
            return Task.CompletedTask;
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

        /// <summary>
        /// 向任意端点异步发送一个数据报：目标端口按原样使用、不查会话缓存，用于 ICE/TURN 的原始收发。
        /// </summary>
        /// <remarks>
        /// netstandard2.0 没有 Socket.SendToAsync(ReadOnlyMemory&lt;byte&gt;)，非数组内存会先复制一次到池化缓冲（唯一的边界拷贝）。
        /// 若输入为调用方数组（MemoryMarshal.TryGetArray 精确匹配）则零拷贝直发，任务完成前调用方不得修改或复用该内存。
        /// 每次调用独占一个 SocketAsyncEventArgs，不与按会话发送争用令牌上的写锁。
        /// </remarks>
        /// <param name="remoteEndPoint">目标端点，不做端口改写</param>
        /// <param name="data">待发送数据，长度不得超过 UDP 单包上限</param>
        /// <returns>发送完成即完成的任务；套接字错误时以 SocketException 失败</returns>
        /// <exception cref="ArgumentNullException">remoteEndPoint 为 null</exception>
        /// <exception cref="ArgumentOutOfRangeException">数据长度超过单包上限</exception>
        /// <exception cref="InvalidOperationException">套接字尚未启动或已停止</exception>
        public Task SendToAsync(IPEndPoint remoteEndPoint, ReadOnlyMemory<byte> data)
        {
            if (remoteEndPoint == null) throw new ArgumentNullException(nameof(remoteEndPoint));
            if (data.Length == 0) return Task.CompletedTask;
            if (data.Length > Model.SocketOption.UDPMaxLength) throw new ArgumentOutOfRangeException(nameof(data), "SendToAsync Incorrect length of data sent");

            var socket = _udpSocket;
            if (socket == null) throw new InvalidOperationException("The UDP server socket has not been started.");

            var context = new SendToContext();

            ArraySegment<byte> seg;
            if (!MemoryMarshal.TryGetArray(data, out seg) || seg.Array == null)
            {
                var writer = new PooledBufferWriter(data.Length);
                data.Span.CopyTo(writer.GetSpan(data.Length));
                writer.Advance(data.Length);
                if (!writer.TryGetArray(out seg) || seg.Array == null)
                {
                    writer.Dispose();
                    return Task.CompletedTask;
                }
                context.Writer = writer;
            }

            context.Completion = new TaskCompletionSource<bool>();
            context.Args = new SocketAsyncEventArgs();
            context.Args.UserToken = context;
            context.Args.RemoteEndPoint = remoteEndPoint;
            context.Args.SetBuffer(seg.Array, seg.Offset, seg.Count);
            context.Args.Completed += OnSendToCompleted;

            try
            {
                // 返回 true 表示挂起，由 IO 完成线程回调；返回 false 表示已完成，需自行走回调释放上下文
                if (!socket.SendToAsync(context.Args))
                {
                    OnSendToCompleted(socket, context.Args);
                }
            }
            catch
            {
                try { context.Args.Completed -= OnSendToCompleted; } catch { }
                try { context.Args.Dispose(); } catch { }
                if (context.Writer != null) context.Writer.Dispose();
                throw;
            }

            return context.Completion.Task;
        }

        /// <summary>
        /// SendToAsync 完成回调：结算结果并释放本次发送独占的上下文
        /// </summary>
        /// <param name="sender">发送方套接字</param>
        /// <param name="e">本次发送使用的参数对象</param>
        private void OnSendToCompleted(object sender, SocketAsyncEventArgs e)
        {
            var context = e == null ? null : e.UserToken as SendToContext;
            if (context == null) return;
            var expected = e.Count;
            try
            {
                if (e.SocketError == SocketError.Success && e.BytesTransferred == expected)
                {
                    context.Completion.TrySetResult(true);
                }
                else if (e.SocketError != SocketError.Success)
                {
                    context.Completion.TrySetException(new SocketException((int)e.SocketError));
                }
                else
                {
                    context.Completion.TrySetException(new KernelException("SendToAsync sent fewer bytes than requested."));
                }
            }
            finally
            {
                try { e.Completed -= OnSendToCompleted; } catch { }
                try { e.Dispose(); } catch { }
                if (context.Writer != null)
                {
                    try { context.Writer.Dispose(); } catch { }
                }
            }
        }
        #endregion



        private void _sessionManager_OnTimeOut(IUserToken userToken)
        {
            Disconnect(userToken);
        }


        /// <summary>
        /// 按会话标识取会话令牌，不存在时为 null
        /// </summary>
        /// <param name="sessionID">会话标识</param>
        /// <returns>会话令牌，不存在时为 null</returns>
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
            // Free() 会把 userToken.ID 置空，必须在释放前捕获用于 OnDisconnected。
            var sessionId = userToken?.ID;

            if (_sessionManager.Free(userToken))
            {
                if (ex == null) ex = new KernelException("The remote client has been closed.");
                Interlocked.Decrement(ref _clientCounts);

                if (!string.IsNullOrEmpty(sessionId))
                    OnDisconnected?.Invoke(sessionId, ex);
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

        /// <summary>
        /// 停止监听并释放套接字与会话，标记为已释放；内部异常被吞掉
        /// </summary>
        public void Dispose()
        {
            try
            {
                Stop();
                IsDisposed = true;
            }
            catch { }
        }

        /// <summary>
        /// 一次 SendToAsync 发送的上下文：独占的发送参数对象与可能借用的池化缓冲
        /// </summary>
        private sealed class SendToContext
        {
            /// <summary>
            /// 本次发送独占的发送参数对象
            /// </summary>
            public SocketAsyncEventArgs Args { get; set; }

            /// <summary>
            /// 用于把完成回调转换为任务的完成源
            /// </summary>
            public TaskCompletionSource<bool> Completion { get; set; }

            /// <summary>
            /// 非零拷贝路径借用的池化缓冲，完成后必须归还
            /// </summary>
            public PooledBufferWriter Writer { get; set; }
        }
    }
}
