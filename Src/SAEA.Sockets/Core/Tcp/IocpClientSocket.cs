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
*文件名： IocpClientSocket
*版本号： v26.4.23.1
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 15:54:21
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Common;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

namespace SAEA.Sockets.Core.Tcp
{
    /// <summary>
    /// iocp 客户端 socket
    /// </summary>
    public class IocpClientSocket : IClientSocket, IDisposable
    {
        /// <summary>
        /// 套接字对象
        /// </summary>
        Socket _socket;

        /// <summary>
        /// 用户令牌
        /// </summary>
        IUserToken _userToken;

        /// <summary>
        /// 异步连接参数
        /// </summary>
        SocketAsyncEventArgs _connectArgs;

        /// <summary>
        /// 用户令牌工厂
        /// </summary>
        UserTokenFactory _userTokenFactory;

        /// <summary>
        /// 连接回调
        /// </summary>
        Action<SocketError> _connectCallBack;

        /// <summary>
        /// 连接事件
        /// </summary>
        AutoResetEvent _connectEvent = new AutoResetEvent(true);

        /// <summary>
        /// 连接超时定时器
        /// </summary>
        Timer _connectTimeoutTimer;

        /// <summary>
        /// 套接字选项
        /// </summary>
        public ISocketOption SocketOption { get; set; }

        /// <summary>
        /// 远程终结点
        /// </summary>
        public string Endpoint { get => _socket?.RemoteEndPoint?.ToString(); }

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool Connected { get; private set; }

        /// <summary>
        /// 用户令牌
        /// </summary>
        public IUserToken UserToken { get => _userToken; private set => _userToken = value; }

        /// <summary>
        /// 是否已释放
        /// </summary>
        public bool IsDisposed { get; private set; } = false;

        /// <summary>
        /// 套接字对象
        /// </summary>
        public Socket Socket => _socket;

        /// <summary>
        /// 上下文对象
        /// </summary>
        public IContext<ICoder> Context { get; private set; }

        /// <summary>
        /// 错误事件
        /// </summary>
        public event OnErrorHandler OnError;

        /// <summary>
        /// 断开连接事件
        /// </summary>
        public event OnDisconnectedHandler OnDisconnected;

        /// <summary>
        /// 接收数据事件
        /// </summary>
        public event OnClientReceiveHandler OnReceive;

        /// <summary>
        /// 接收字节数据事件
        /// </summary>
        protected OnClientReceiveBytesHandler OnClientReceive = null;

        /// <summary>
        /// 内部委托：接收数据时触发（Span版本）
        /// </summary>
        /// <param name="data">数据Span</param>
        internal delegate void OnClientReceiveSpanHandler(ReadOnlySpan<byte> data);

        /// <summary>
        /// 内部事件：客户端接收数据（Span版本）
        /// </summary>
        internal event OnClientReceiveSpanHandler OnClientReceiveSpan;

        /// <summary>
        /// 触发错误事件
        /// </summary>
        /// <param name="id">错误ID</param>
        /// <param name="ex">异常对象</param>
        protected void RaiseOnError(string id, Exception ex)
        {
            OnError?.Invoke(id, ex);
        }

        /// <summary>
        /// iocp 客户端 socket
        /// </summary>
        /// <param name="socketOption">Socket选项</param>
        public IocpClientSocket(ISocketOption socketOption)
        {
            SocketOption = socketOption;

            Context = socketOption.Context;

            _userTokenFactory = new UserTokenFactory();

            if (SocketOption.UseIPV6)
            {
                _socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
            }
            else
            {
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            }
            if (SocketOption.ReusePort)
            {
                _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, SocketOption.ReusePort);
            }
            _socket.NoDelay = SocketOption.NoDelay;
            _socket.SendTimeout = _socket.ReceiveTimeout = SocketOption.ActionTimeout;
            _socket.KeepAlive(SocketOption.FreeTime, SocketOption.ActionTimeout);

            OnClientReceive = new OnClientReceiveBytesHandler(OnReceived);

            _connectArgs = new SocketAsyncEventArgs
            {
                RemoteEndPoint = new IPEndPoint(IPAddress.Parse(SocketOption.IP), SocketOption.Port)
            };
            _connectArgs.Completed += ConnectArgs_Completed;

            _userToken = _userTokenFactory.Create(socketOption.Context, socketOption.ReadBufferSize, IO_Completed);

            // 初始化连接超时定时器
            _connectTimeoutTimer = new Timer(ConnectTimeoutCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// iocp 客户端 socket
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="ip">IP地址</param>
        /// <param name="port">端口</param>
        /// <param name="bufferSize">缓冲区大小</param>
        /// <param name="timeOut">超时时间</param>
        public IocpClientSocket(IContext<ICoder> context, string ip = "127.0.0.1", int port = 39654, int bufferSize = 64 * 1024, int timeOut = 180 * 1000)
            : this(new SocketOption() { Context = context, IP = ip, Port = port, ReadBufferSize = bufferSize, WriteBufferSize = bufferSize, ActionTimeout = timeOut })
        {

        }

        /// <summary>
        /// 指定绑定ip
        /// </summary>
        /// <param name="ipEndPint">IP终结点</param>
        public void Bind(IPEndPoint ipEndPint)
        {
            _socket.Bind(ipEndPint);
        }

        /// <summary>
        /// 指定绑定ip
        /// </summary>
        /// <param name="ip">IP地址</param>
        public void Bind(string ip)
        {
            Bind(new IPEndPoint(IPAddress.Parse(ip), 0));
        }

        /// <summary>
        /// 连接超时回调
        /// </summary>
        /// <param name="state">状态对象</param>
        private void ConnectTimeoutCallback(object state)
        {
            try
            {
                _socket.Close(SocketOption.ActionTimeout);
                OnError?.Invoke("", new TimeoutException($"连接超时，超时时间：{SocketOption.ConnectTimeout}毫秒"));
                _connectCallBack?.Invoke(SocketError.TimedOut);
            }
            catch { }
            finally
            {
                _connectTimeoutTimer.Change(Timeout.Infinite, Timeout.Infinite);
                _connectEvent.Set();
            }
        }

        /// <summary>
        /// 异步连接到服务器
        /// </summary>
        /// <param name="callBack">连接回调</param>
        public void ConnectAsync(Action<SocketError> callBack = null)
        {
            _connectEvent.WaitOne();
            if (!Connected)
            {
                _connectCallBack = callBack;

                // 启动连接超时定时器
                _connectTimeoutTimer.Change(SocketOption.ConnectTimeout, Timeout.Infinite);

                if (!_socket.ConnectAsync(_connectArgs))
                {
                    ProcessConnected(_connectArgs);
                }
            }
        }

        /// <summary>
        /// 同步连接到服务器
        /// </summary>
        public void Connect()
        {
            // 保证同一时刻只有一个连接操作
            _connectEvent.WaitOne();
            try
            {
                if (!Connected && !IsDisposed)
                {
                    // 与 ConnectAsync 保持一致的连接流程：使用 _connectArgs + _connectTimeoutTimer + _connectEvent
                    _connectCallBack = null;

                    // 启动连接超时定时器
                    _connectTimeoutTimer.Change(SocketOption.ConnectTimeout, Timeout.Infinite);

                    try
                    {
                        // 发起异步连接（使用已有的 SocketAsyncEventArgs）
                        if (!_socket.ConnectAsync(_connectArgs))
                        {
                            // 同步完成
                            ProcessConnected(_connectArgs);
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // 如果在发起连接时 socket 已被关闭/释放，抛出以便上层处理
                        throw;
                    }

                    // 等待连接完成或超时（ConnectTimeoutCallback 或 ProcessConnected 会 Set）
                    var signaled = _connectEvent.WaitOne(SocketOption.ConnectTimeout + 100);

                    // 停止超时定时器（ProcessConnected 也会停止它；此处为保险）
                    try { _connectTimeoutTimer.Change(Timeout.Infinite, Timeout.Infinite); } catch { }

                    if (!signaled || !Connected)
                    {
                        throw new TimeoutException($"连接超时，超时时间：{SocketOption.ConnectTimeout}毫秒");
                    }

                    // 此时 ProcessConnected 已处理 userToken 初始化与接收启动
                }
            }
            finally
            {
                // 释放等待许可，允许后续连接尝试
                _connectEvent.Set();
            }
        }

        /// <summary>
        /// 连接完成事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        void ConnectArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            if (e.LastOperation == SocketAsyncOperation.Connect)
                ProcessConnected(e);
            else
            {
                OnError?.Invoke("", new Exception($"SAEA SocketError:连接失败, {e.LastOperation}"));
            }
        }

        /// <summary>
        /// 处理连接完成
        /// </summary>
        /// <param name="e">事件参数</param>
        void ProcessConnected(SocketAsyncEventArgs e)
        {
            // 停止连接超时定时器
            _connectTimeoutTimer.Change(Timeout.Infinite, Timeout.Infinite);

            Connected = (e.SocketError == SocketError.Success);

            if (Connected)
            {
                _userToken.ID = e.ConnectSocket.LocalEndPoint.ToString();
                _userToken.Socket = _socket;
                _userToken.Linked = _userToken.Actived = DateTimeHelper.Now;

                var readArgs = _userToken.ReadArgs;
                ProcessReceive(readArgs);
            }
            _connectCallBack?.Invoke(e.SocketError);
            _connectEvent.Set();
        }

        /// <summary>
        /// IO完成事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        void IO_Completed(object sender, SocketAsyncEventArgs e)
        {
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
                        Disconnect();
                        break;
                }
            }
            catch (Exception ex)
            {
                // 捕获所有异常，避免未处理异常导致进程退出
                OnError?.Invoke(_userToken?.ID ?? "", ex);
                try { Disconnect(ex); } catch { }
            }
        }

        /// <summary>
        /// 处理接收到的数据
        /// </summary>
        /// <param name="data">接收到的数据</param>
        protected virtual void OnReceived(byte[] data)
        {
            OnReceive?.Invoke(data);
        }

        /// <summary>
        /// 处理接收操作
        /// </summary>
        /// <param name="readArgs">读取操作的SocketAsyncEventArgs对象</param>
        void ProcessReceive(SocketAsyncEventArgs readArgs)
        {
            if (_userToken != null && _userToken.Socket != null && _userToken.Socket.Connected)
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
            else
                OnError?.Invoke("", new Exception("SAEA SocketError:发送失败,当前连接已断开"));
        }

        /// <summary>
        /// 处理接收完成
        /// </summary>
        /// <param name="readArgs">读取操作的SocketAsyncEventArgs对象</param>
        void ProcessReceived(SocketAsyncEventArgs readArgs)
        {
            try
            {
                if (readArgs == null || readArgs.UserToken == null) return;

                if (readArgs.SocketError == SocketError.Success)
                {
                    _userToken.Actived = DateTimeHelper.Now;

                    if (readArgs.BytesTransferred > 0)
                    {
                        // 使用Span获取数据，避免立即复制
                        var dataSpan = readArgs.Buffer.AsSpan(readArgs.Offset, readArgs.BytesTransferred);

                        // 触发内部Span事件
                        OnClientReceiveSpan?.Invoke(dataSpan);

                        // 复制到精确大小的数组，避免内存池返回的超大数组导致下游逻辑错误
                        var buffer = dataSpan.ToArray();

                        try
                        {
                            OnClientReceive?.Invoke(buffer);
                        }
                        catch (Exception ex)
                        {
                            OnError?.Invoke(UserToken.ID, ex);
                        }
                    }

                    if (readArgs.SocketError == SocketError.Success && UserToken.Socket != null && UserToken.Socket.Connected)
                    {
                        ProcessReceive(readArgs);
                    }
                    else
                    {
                        Disconnect();
                    }
                }
                else
                {
                    Disconnect(new Exception("SAEA SocketError:远程服务器关闭连接"));
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(_userToken.ID, ex);
            }
        }

        /// <summary>
        /// 处理发送完成
        /// </summary>
        /// <param name="e">事件参数</param>
        void ProcessSended(SocketAsyncEventArgs e)
        {
            try
            {
                _userToken.Actived = DateTimeHelper.Now;
                _userToken.ReleaseWrite();
            }
            catch (Exception ex)
            {
                OnError?.Invoke(_userToken?.ID ?? "", ex);
            }
        }

        /// <summary>
        /// 异步发送数据
        /// </summary>
        /// <param name="userToken">用户令牌</param>
        /// <param name="data">数据</param>
        public void SendAsync(IUserToken userToken, byte[] data)
        {
            try
            {
                if (userToken != null && userToken.Socket != null && userToken.Socket.Connected)
                {
                    if (userToken.WaitWrite(SocketOption.ActionTimeout))
                    {
                        var writeArgs = userToken.WriteArgs;

                        writeArgs.SetBuffer(data, 0, data.Length);

                        if (!userToken.Socket.SendAsync(writeArgs))
                        {
                            ProcessSended(writeArgs);
                        }
                    }
                    else
                    {
                        OnError?.Invoke($"SAEA SocketError:发送消息时发生异常,{userToken?.ID}", new TimeoutException("发送数据超时"));
                    }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(userToken?.ID ?? "", ex);
                try { userToken?.ReleaseWrite(); } catch { }
                try { Disconnect(); } catch { }
            }
        }

        /// <summary>
        /// 异步发送数据
        /// </summary>
        /// <param name="data">数据</param>
        public void SendAsync(byte[] data)
        {
            SendAsync(UserToken, data);
        }

        /// <summary>
        /// 同步发送数据
        /// </summary>
        /// <param name="data">数据</param>
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
                    Disconnect(ex);
                }
            }
            else
                OnError?.Invoke("", new Exception("SAEA SocketError:发送失败,当前连接已断开"));
        }

        /// <summary>
        /// 异步发送数据
        /// </summary>
        /// <param name="data">数据</param>
        public void BeginSend(byte[] data)
        {
            try
            {
                if (Connected)
                {
                    _userToken.Socket.BeginSend(data, 0, data.Length, SocketFlags.None, null, null);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(_userToken?.ID ?? "", ex);
            }
        }

        /// <summary>
        /// 异步发送数据
        /// </summary>
        /// <param name="buffer">缓冲区</param>
        /// <param name="offset">偏移量</param>
        /// <param name="count">数量</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        public async Task SendAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                var data = new byte[count];

                Buffer.BlockCopy(buffer, offset, data, 0, count);

                Send(data);

            }, cancellationToken);
        }

        /// <summary>
        /// 异步接收数据
        /// </summary>
        /// <param name="buffer">缓冲区</param>
        /// <param name="offset">偏移量</param>
        /// <param name="count">数量</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        public Task<int> ReceiveAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new KernelException("SAEA SocketError:当前方法只在stream实现中才能使用!");
        }

        /// <summary>
        /// 获取流
        /// </summary>
        /// <returns></returns>
        public Stream GetStream()
        {
            throw new InvalidOperationException("SAEA SocketError:iocp暂不支持流模式");
        }

        /// <summary>
        /// 主动断开连接
        /// </summary>
        /// <param name="ex">异常对象</param>
        public void Disconnect(Exception ex)
        {
            this.Connected = false;

            var mex = ex;

            try
            {
                if (_userToken != null && _userToken.Socket != null)
                {
                    try
                    {
                        _userToken.Socket.Shutdown(SocketShutdown.Both);
                    }
                    catch { }
                    try { _userToken.Socket?.Close(); } catch { }
                }
            }
            catch (Exception sex)
            {
                if (mex != null) mex = sex;
            }
            finally
            {
                if (mex == null)
                {
                    mex = new Exception("SAEA SocketError:当前连接已主动断开");
                }
                if (_userToken != null && !string.IsNullOrEmpty(_userToken.ID))
                    OnDisconnected?.Invoke(_userToken.ID, mex);
            }

            try { _userToken.Clear(); } catch { }
        }

        /// <summary>
        /// 主动断开连接
        /// </summary>
        public void Disconnect()
        {
            this.Disconnect(null);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            IsDisposed = true;
            Disconnect();
        }
    }
}