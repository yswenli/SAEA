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
*文件名： SaeaUdpClientSocket
*版本号： v26.4.23.1
*唯一标识：af24961f-6acc-4f24-871a-5dc73a5b97af
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/29 15:50:22
*描述：UDP 客户端 socket
*
*=====================================================================
*修改标记
*修改时间：2026/11/9 10:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：携带所有权对象的发送重载签名同步为 Task，UDP 收发行为不变
*
*****************************************************************************/
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;

namespace SAEA.Sockets.Core.Udp
{
    /// <summary>
    /// UDP 客户端 socket：单套接字连接固定远端地址，支持广播与多播，仅事件（包）模式收发
    /// </summary>
    public class SaeaUdpClientSocket : IClientSocket, IDisposable
    {
        const int SIO_UDP_CONNRESET = unchecked((int)0x9800000C);

        Socket _udpSocket;

        IUserToken _userToken;

        SocketAsyncEventArgs _connectArgs;

        UserTokenFactory _userTokenFactory;

        /// <summary>
        /// 套接字选项，构造时用于广播、多播、超时与收发缓冲设置
        /// </summary>
        public ISocketOption SocketOption { get; set; }

        IPEndPoint _remoteEndPoint;

        /// <summary>
        /// 远端地址字符串，套接字未连接时为 null
        /// </summary>
        public string Endpoint { get => _udpSocket?.RemoteEndPoint?.ToString(); }

        /// <summary>
        /// 底层套接字是否处于已连接状态
        /// </summary>
        public bool Connected { get => _udpSocket.Connected; }
        /// <summary>
        /// 当前连接的令牌，构造时创建，断开时清空其内部引用
        /// </summary>
        public IUserToken UserToken { get => _userToken; private set => _userToken = value; }

        /// <summary>
        /// 是否已释放资源
        /// </summary>
        public bool IsDisposed { get; private set; } = false;

        /// <summary>
        /// 底层 UDP 套接字
        /// </summary>
        public Socket Socket => _udpSocket;

        /// <summary>
        /// 编解码上下文，取自套接字选项
        /// </summary>
        public IContext<ICoder> Context { get; private set; }

        /// <summary>
        /// 收发或内部处理异常时触发，参数为会话标识与异常
        /// </summary>
        public event OnErrorHandler OnError;

        /// <summary>
        /// 连接断开后触发，参数为会话标识与断开原因
        /// </summary>
        public event OnDisconnectedHandler OnDisconnected;

        /// <summary>
        /// 接收数据事件（Span 版本）。data 仅在回调期间有效。
        /// </summary>
        public event OnClientReceiveSpanHandler OnClientReceiveSpan;

        /// <summary>
        /// 触发错误事件
        /// </summary>
        /// <param name="id">会话标识</param>
        /// <param name="ex">异常</param>
        protected void RaiseOnError(string id, Exception ex)
        {
            OnError?.Invoke(id, ex);
        }

        /// <summary>
        /// saea 客户端 socket
        /// </summary>
        /// <param name="socketOption"></param>
        public SaeaUdpClientSocket(ISocketOption socketOption)
        {
            SocketOption = socketOption;

            Context = socketOption.Context;

            _userTokenFactory = new UserTokenFactory();

            _udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, SocketOption.ReusePort);
            _udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, SocketOption.Broadcasted);

            //设置多播
            if (!string.IsNullOrEmpty(SocketOption.MultiCastHost) && !string.IsNullOrEmpty(SocketOption.IP))
            {
                _udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);
                MulticastOption mcastOption = new MulticastOption(IPAddress.Parse(SocketOption.MultiCastHost), IPAddress.Parse(SocketOption.IP));
                _udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, mcastOption);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _udpSocket.IOControl(SIO_UDP_CONNRESET, new byte[4], new byte[4]);
            }

            _udpSocket.SendTimeout = _udpSocket.ReceiveTimeout = SocketOption.ActionTimeout;
            _udpSocket.SendBufferSize = SocketOption.WriteBufferSize;
            _udpSocket.ReceiveBufferSize = SocketOption.ReadBufferSize;

            _connectArgs = new SocketAsyncEventArgs
            {
                RemoteEndPoint = new IPEndPoint(IPAddress.Parse(SocketOption.IP), SocketOption.Port)
            };
            _connectArgs.Completed += ConnectArgs_Completed;

            _userToken = _userTokenFactory.Create(socketOption.Context, socketOption.ReadBufferSize, IO_Completed);

            _remoteEndPoint = new IPEndPoint(IPAddress.Parse(SocketOption.IP), SocketOption.Port);

            _userToken.Socket = _udpSocket;
        }

        /// <summary>
        /// 指定绑定ip
        /// </summary>
        /// <param name="ipEndPint"></param>
        public void Bind(IPEndPoint ipEndPint)
        {
            _udpSocket.Bind(ipEndPint);
        }

        /// <summary>
        /// 指定绑定ip
        /// </summary>
        /// <param name="ip"></param>
        public void Bind(string ip)
        {
            Bind(new IPEndPoint(IPAddress.Parse(ip), 0));
        }

        /// <summary>
        /// 连接到服务器
        /// </summary>
        /// <param name="callBack"></param>
        public void ConnectAsync(Action<SocketError> callBack = null)
        {
            if (!_udpSocket.ConnectAsync(_connectArgs))
            {
                ProcessConnected(_connectArgs);
            }
        }

        /// <summary>
        /// 连接到服务器
        /// </summary>
        public void Connect()
        {
            ConnectAsync();
        }


        void ConnectArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            if (e.LastOperation == SocketAsyncOperation.Connect)
                ProcessConnected(e);
            else
            {
                OnError?.Invoke("", new Exception($"udp creation failed： {e.LastOperation}"));
            }
        }


        void ProcessConnected(SocketAsyncEventArgs e)
        {
            _userToken.ID = _udpSocket.LocalEndPoint.ToString();
            _userToken.Socket = _udpSocket;
            _userToken.Linked = _userToken.Actived = DateTimeHelper.Now;

            var readArgs = _userToken.ReadArgs;
            ProcessReceive(readArgs);
        }


        void IO_Completed(object sender, SocketAsyncEventArgs e)
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
                    Disconnect();
                    break;
            }
        }


        void ProcessReceive(SocketAsyncEventArgs readArgs)
        {
            if (_userToken.Socket != null)
            {
                if (!_userToken.Socket.ReceiveAsync(readArgs))
                {
                    // 使用线程池避免直接递归调用导致栈溢出
                    ThreadPool.QueueUserWorkItem((state) =>
                    {
                        ProcessReceived(readArgs);
                    });
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

                    var dataSpan = readArgs.Buffer.AsSpan(readArgs.Offset, readArgs.BytesTransferred);

                    OnClientReceiveSpan?.Invoke(dataSpan);

                    ProcessReceive(readArgs);
                }
                else
                {
                    ProcessDisconnected(new Exception($"SocketError:{readArgs.SocketError},the remote server closed udp"));
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(_userToken.ID, ex);
                Disconnect();
            }
        }


        /// <summary>
        /// 处理发送完成
        /// </summary>
        /// <param name="e">事件参数</param>
        void ProcessSended(SocketAsyncEventArgs e)
        {
            var userToken = _userToken;
            if (userToken == null) return;
            try
            {
                var owner = userToken.TakeSendingOwner();
                userToken.Actived = DateTimeHelper.Now;
                if (owner != null)
                {
                    try { owner.Dispose(); } catch { }
                }
                userToken.ReleaseWrite();
            }
            catch (Exception ex)
            {
                OnError?.Invoke(userToken?.ID ?? "", ex);
            }
        }



        void ProcessDisconnected(Exception ex)
        {
            try
            {
                _userToken.Clear();
            }
            finally
            {
                if (_userToken != null && !string.IsNullOrEmpty(_userToken.ID))
                    OnDisconnected?.Invoke(_userToken.ID, ex);
            }
        }



        /// <summary>
        /// UDP 异步发送核心：seg 指向的内存由 owner 持有；owner 为 null 表示调用方内存（零拷贝）。
        /// 所有权在发送完成（ProcessSended）或失败路径释放。
        /// </summary>
        private void SendAsyncRaw(IPEndPoint ipEndPoint, ArraySegment<byte> seg, IDisposable owner)
        {
            var userToken = _userToken;
            if (seg.Array == null || seg.Count == 0)
            {
                owner?.Dispose();
                return;
            }
            bool acquired = false;
            bool transferred = false;
            try
            {
                if (userToken != null && userToken.Socket != null)
                {
                    if (userToken.WaitWrite(SocketOption.ActionTimeout))
                    {
                        acquired = true;
                        userToken.SendingOwner = owner;
                        transferred = true;
                        var writeArgs = userToken.WriteArgs;
                        writeArgs.RemoteEndPoint = ipEndPoint;
                        writeArgs.SetBuffer(seg.Array, seg.Offset, seg.Count);
                        if (!userToken.Socket.SendToAsync(writeArgs))
                        {
                            ProcessSended(writeArgs);
                        }
                    }
                    else
                    {
                        OnError?.Invoke($"An exception occurs when a message is sending:{ipEndPoint?.ToString()}", new TimeoutException("Sending data timeout"));
                    }
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
                OnError?.Invoke(userToken?.ID ?? _remoteEndPoint?.ToString(), ex);
                if (acquired)
                {
                    try { userToken.ReleaseWrite(); } catch { }
                }
                try { Disconnect(); } catch { }
            }
            finally
            {
                if (!transferred) owner?.Dispose();
            }
        }

        /// <summary>
        /// 同步发送数据
        /// </summary>
        /// <remarks>
        /// netstandard2.0 没有 Socket.SendTo(ReadOnlySpan&lt;byte&gt;)，非数组内存会先复制一次到池化缓冲（唯一的边界拷贝）。
        /// 若输入为调用方数组（MemoryMarshal.TryGetArray 精确匹配）则零拷贝直发，发送完成前调用方不得修改或复用该内存。
        /// </remarks>
        /// <param name="data"></param>
        public void Send(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) return;
            if (data.Length > Model.SocketOption.UDPMaxLength) throw new ArgumentOutOfRangeException("Send Incorrect length of data sent");
            var writer = new PooledBufferWriter(data.Length);
            try
            {
                data.CopyTo(writer.GetSpan(data.Length));
                writer.Advance(data.Length);
                writer.TryGetArray(out var rented);
                _udpSocket.SendTo(rented.Array, rented.Offset, rented.Count, SocketFlags.None, _remoteEndPoint);
                _userToken.Actived = DateTimeHelper.Now;
            }
            catch (Exception ex)
            {
                ProcessDisconnected(ex);
            }
            finally
            {
                writer.Dispose();
            }
        }

        /// <summary>
        /// 异步发送数据
        /// </summary>
        /// <remarks>
        /// 发送到已连接的远端地址；参数与拷贝语义见 <see cref="SendAsync(IPEndPoint, ReadOnlyMemory{byte})"/>。
        /// </remarks>
        /// <param name="data"></param>
        public void SendAsync(ReadOnlyMemory<byte> data)
        {
            SendAsync(_remoteEndPoint, data);
        }

        /// <summary>
        /// 异步发送并转移缓冲所有权到已连接的远端地址；零拷贝直发时由发送完成回调释放 owner，需复制时先复制再立即释放。
        /// UDP 保持原有的投递即返回语义，返回的 Task 仅用于对齐接口签名。
        /// </summary>
        /// <param name="data">待发送数据，为空时只释放 owner</param>
        /// <param name="owner">data 所占缓冲的所有权对象，可为 null</param>
        /// <returns>已投递时为完成状态的任务</returns>
        /// <exception cref="ArgumentOutOfRangeException">数据长度超过单包上限</exception>
        public Task SendAsync(ReadOnlyMemory<byte> data, IDisposable owner)
        {
            if (data.Length == 0)
            {
                owner?.Dispose();
                return Task.CompletedTask;
            }
            if (data.Length > Model.SocketOption.UDPMaxLength)
            {
                owner?.Dispose();
                throw new ArgumentOutOfRangeException("SendAsync Incorrect length of data sent");
            }
            ArraySegment<byte> rented;
            if (MemoryMarshal.TryGetArray(data, out var segment) && segment.Array != null)
            {
                SendAsyncRaw(_remoteEndPoint, segment, owner);
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
            SendAsyncRaw(_remoteEndPoint, rented, writer);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 异步发送数据（指定远端地址）
        /// </summary>
        /// <remarks>
        /// netstandard2.0 没有 Socket.SendTo(ReadOnlySpan&lt;byte&gt;)，非数组内存会先复制一次到池化缓冲（唯一的边界拷贝）。
        /// 若输入为调用方数组（MemoryMarshal.TryGetArray 精确匹配）则零拷贝直发，发送完成前调用方不得修改或复用该内存。
        /// <paramref name="data"/> 为 default 或零长度时直接返回、不发送（旧 byte[] 版对 null/空抛
        /// <see cref="ArgumentOutOfRangeException"/>，本重载沿用 Memory 面的静默返回语义）。
        /// </remarks>
        /// <param name="ipEndPoint">远端地址</param>
        /// <param name="data">数据</param>
        public void SendAsync(IPEndPoint ipEndPoint, ReadOnlyMemory<byte> data)
        {
            if (data.Length == 0) return;
            if (data.Length > Model.SocketOption.UDPMaxLength) throw new ArgumentOutOfRangeException("SendAsync Incorrect length of data sent");
            if (MemoryMarshal.TryGetArray(data, out var seg))
            {
                SendAsyncRaw(ipEndPoint, seg, null);
                return;
            }
            var writer = new PooledBufferWriter(data.Length);
            data.Span.CopyTo(writer.GetSpan(data.Length));
            writer.Advance(data.Length);
            writer.TryGetArray(out var rented);
            SendAsyncRaw(ipEndPoint, rented, writer);
        }

        /// <summary>
        /// 异步发送协议对象
        /// </summary>
        /// <remarks>
        /// 编码结果写入池化缓冲；netstandard2.0 没有 Socket.SendTo(ReadOnlySpan&lt;byte&gt;)，
        /// 因此编码完成后必然发生一次到池化缓冲的边界拷贝（编码本身即写入该缓冲）。
        /// </remarks>
        /// <param name="protocal"></param>
        public void SendAsync(ISocketProtocal protocal)
        {
            if (protocal == null) return;
            var coder = _userToken?.Coder;
            if (coder == null)
            {
                OnError?.Invoke(_userToken?.ID ?? "", new InvalidOperationException("SAEA SocketError:coder 未初始化"));
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
                OnError?.Invoke(_userToken?.ID ?? "", ex);
                return;
            }
            if (writer.WrittenCount > Model.SocketOption.UDPMaxLength)
            {
                writer.Dispose();
                throw new ArgumentOutOfRangeException("SendAsync Incorrect length of data sent");
            }
            writer.TryGetArray(out var rented);
            SendAsyncRaw(_remoteEndPoint, rented, writer);
        }

        /// <summary>
        /// 异步发送数据（可取消）
        /// </summary>
        /// <remarks>
        /// netstandard2.0 没有 Socket.SendTo(ReadOnlySpan&lt;byte&gt;)，非数组内存会先复制一次到池化缓冲（唯一的边界拷贝）。
        /// 若输入为调用方数组（MemoryMarshal.TryGetArray 精确匹配）则零拷贝直发，发送完成前调用方不得修改或复用该内存。
        /// </remarks>
        /// <param name="data"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            if (data.Length == 0) return Task.CompletedTask;
            return Task.Run(() => SendAsync(data), cancellationToken);
        }

        /// <summary>
        /// UDP 不支持流式接收，调用即抛 NotSupportedException
        /// </summary>
        /// <param name="buffer">缓冲区</param>
        /// <param name="offset">偏移量</param>
        /// <param name="count">数量</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>接收字节数</returns>
        /// <exception cref="NotSupportedException">UDP 无流式接收能力</exception>
        public Task<int> ReceiveAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("The current method can only be used in stream implementation!");
        }

        /// <summary>
        /// UDP 不支持流模式，调用即抛 NotSupportedException
        /// </summary>
        /// <returns>数据流</returns>
        /// <exception cref="NotSupportedException">UDP 无流模式</exception>
        public Stream GetStream()
        {
            throw new NotSupportedException("UDP does not support streaming mode");
        }

        /// <summary>
        /// 关闭套接字并触发断开事件、清空令牌；ex 为 null 时以主动关闭异常作为原因
        /// </summary>
        /// <param name="ex">断开原因，可为 null</param>
        public void Disconnect(Exception ex)
        {
            var mex = ex;

            try
            {
                if (_userToken != null && _userToken.Socket != null)
                {
                    _userToken.Socket.Close();
                }
            }
            catch (Exception sex)
            {
                if (mex != null) mex = sex;
            }
            if (mex == null)
            {
                mex = new Exception("The current udp has been actively closed");
            }

            if (_userToken != null && !string.IsNullOrEmpty(_userToken.ID))
                OnDisconnected?.Invoke(_userToken.ID, mex);

            _userToken.Clear();
        }

        /// <summary>
        /// 主动断开连接
        /// </summary>
        public void Disconnect()
        {
            this.Disconnect(null);
        }

        /// <summary>
        /// 断开连接并标记为已释放
        /// </summary>
        public void Dispose()
        {
            this.Disconnect();
            IsDisposed = true;
        }
    }
}
