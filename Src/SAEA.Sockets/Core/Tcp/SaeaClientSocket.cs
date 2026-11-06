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
*文件名： SaeaClientSocket
*版本号： v26.4.23.1
*唯一标识：0b952b1a-434b-4df2-aab1-7f3c4fed6eb3
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/01/23 11:16:53
*描述：SaeaClientSocket接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 13:33:30
*修改人： yswenli
*版本号： v26.11.5.1
*描述：客户端接入通道链钩子：连接成功后登记通道并启动输入处理器，断开时取消
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Sockets.Base;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using SAEA.Sockets.Ssl;
using SAEA.Sockets.Ssl.Internal;

using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets.Core.Tcp
{
    /// <summary>
    /// saea 客户端 socket。启用 <see cref="IPipelineSocketOption"/> 的钩子后，
    /// 连接建立即登记通道并由输入处理器接管读循环，此时 <see cref="OnClientReceiveSpan"/> 不再触发。
    /// </summary>
    public class SaeaClientSocket : IClientSocket, IDisposable
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
        /// 接收字节数据事件
        /// </summary>
        protected OnClientReceiveBytesHandler OnClientReceive = null;

        /// <summary>
        /// 是否为基类客户端类型（非派生类）
        /// </summary>
        private readonly bool _isBaseClientType;

        /// <summary>
        /// 流 / TLS 连接门面；仅在 UseStream 或启用 TLS 时创建。
        /// </summary>
        private SaeaConnection _connection;

        /// <summary>
        /// TLS 初始化异常（创建传输失败时记录，供连接回调使用）。
        /// </summary>
        private Exception _tlsSetupException;

        /// <summary>
        /// 连接建立回调；未配置通道链时为 null。
        /// </summary>
        private readonly Action<ChannelInfo> _connectionOpened;

        /// <summary>
        /// 接管读循环的输入处理器；未配置通道链时为 null。
        /// </summary>
        private readonly Func<IUserToken, PipeReader, IReceiveFlowControl, CancellationToken, Task> _inputHandler;

        /// <summary>
        /// 输入处理器的取消源；未启用通道链时为 null，由泵自身负责释放。
        /// </summary>
        private CancellationTokenSource _pipelineCts;

        /// <summary>
        /// 是否有接收操作在途（0 否，1 是）。
        /// </summary>
        private int _receiving;

        /// <summary>
        /// 是否启用 TLS。
        /// </summary>
        private bool IsTlsActive => SocketOption?.Tls != null && SocketOption.Tls.Enabled;

        /// <summary>
        /// 接收数据事件（Span 版本）。data 仅在回调期间有效。
        /// </summary>
        public event OnClientReceiveSpanHandler OnClientReceiveSpan;

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
        /// saea 客户端 socket
        /// </summary>
        /// <param name="socketOption">Socket选项</param>
        public SaeaClientSocket(ISocketOption socketOption)
        {
            SocketOption = socketOption;

            // 成对配置与 UseStream 要求由 SocketOptionBuilder.Build() 校验，这里只取结果。
            var pipelineOption = socketOption as IPipelineSocketOption;
            if (pipelineOption != null)
            {
                _connectionOpened = pipelineOption.ConnectionOpened;
                _inputHandler = pipelineOption.InputHandler;
            }

            // Stream / TLS 模式可能未设置上下文；补齐默认上下文，避免令牌初始化失败。
            if (socketOption.Context == null)
                socketOption.Context = new BaseContext<BaseCoder>();

            Context = socketOption.Context;

            _isBaseClientType = GetType() == typeof(SaeaClientSocket);

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
        /// saea 客户端 socket
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="ip">IP地址</param>
        /// <param name="port">端口</param>
        /// <param name="bufferSize">缓冲区大小</param>
        /// <param name="timeOut">超时时间</param>
        public SaeaClientSocket(IContext<ICoder> context, string ip = "127.0.0.1", int port = 39654, int bufferSize = 64 * 1024, int timeOut = 180 * 1000)
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

                SetupConnection();

                ProcessReceive(readArgs);

                // 连接门面存在且有事件订阅者时，由后台泵交付解密/流式数据；配置了输入处理器时读循环归它独占。
                if (_connection != null && OnClientReceiveSpan != null && _inputHandler == null)
                    StartSpanPump(_connection);
            }

            // TLS：握手成功后才回调；握手失败回调 SocketError。
            if (Connected && IsTlsActive && _connection != null)
            {
                _connection.HandshakeTask.ContinueWith(t =>
                {
                    if (t.IsFaulted || t.IsCanceled)
                    {
                        var ex = _tlsSetupException ?? GetHandshakeException(t);
                        RaiseError("", ex);
                        _connectCallBack?.Invoke(SocketError.SocketError);
                        try { Disconnect(ex); } catch { }
                    }
                    else
                    {
                        // TLS 握手完成即明文可读，通道链在回调通知成功之前建立。
                        if (!StartPipelineIfNeeded())
                        {
                            _connectCallBack?.Invoke(SocketError.SocketError);
                            _connectEvent.Set();
                            return;
                        }

                        _connectCallBack?.Invoke(SocketError.Success);
                    }
                    _connectEvent.Set();
                }, TaskContinuationOptions.ExecuteSynchronously);
                return;
            }

            if (Connected && IsTlsActive && _connection == null)
            {
                var ex = _tlsSetupException ?? new SaeaTlsException("TLS 初始化失败。");
                RaiseError("", ex);
                _connectCallBack?.Invoke(SocketError.SocketError);
                try { Disconnect(ex); } catch { }
                _connectEvent.Set();
                return;
            }

            // 明文连接：连接建立即可读取，通道链在回调通知结果之前建立。
            if (Connected && !StartPipelineIfNeeded())
            {
                _connectCallBack?.Invoke(SocketError.SocketError);
                _connectEvent.Set();
                return;
            }

            _connectCallBack?.Invoke(e.SocketError);
            _connectEvent.Set();
        }

        /// <summary>
        /// 创建流 / TLS 连接门面。
        /// </summary>
        private void SetupConnection()
        {
            if (!SocketOption.UseStream && !IsTlsActive) return;

            _connection = new SaeaConnection(_userToken.ID, _socket, SocketOption,
                (segment, owner) => SendAsyncRaw(segment, owner),
                ResumeReceive);

            if (!IsTlsActive) return;

            try
            {
                var tls = SaeaTlsTransport.CreateClient(SocketOption.Tls);
                _connection.AttachTls(tls);
            }
            catch (Exception ex)
            {
                _tlsSetupException = ex;
                try { _connection?.Dispose(); } catch { }
                _connection = null;
            }
        }

        private static Exception GetHandshakeException(Task task)
        {
            if (task.IsCanceled) return new SaeaTlsException("TLS 握手已取消。");
            var exception = task.Exception;
            if (exception == null) return new SaeaTlsException("TLS 握手失败。");
            return exception.GetBaseException();
        }

        /// <summary>
        /// 因背压暂停后恢复接收。
        /// </summary>
        private void ResumeReceive()
        {
            if (!Connected || IsDisposed) return;
            var token = _userToken;
            if (token == null || token.Socket == null || !token.Socket.Connected) return;

            var connection = _connection;
            if (connection != null && connection.IsReceivePaused) return;

            ProcessReceive(token.ReadArgs);
        }

        /// <summary>
        /// 连接门面的事件消费泵：从 Pipe 读取明文并回调 <see cref="OnClientReceiveSpan"/>。
        /// 仅在存在订阅者时启动；订阅者与 <see cref="GetStream"/> 消费者互斥。
        /// </summary>
        private void StartSpanPump(SaeaConnection connection)
        {
            var reader = connection.Input;
            if (reader == null) return;

            Task.Run(async () =>
            {
                while (true)
                {
                    ReadResult result;
                    try
                    {
                        result = await reader.ReadAsync().ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        RaiseError(_userToken?.ID ?? "", ex);
                        break;
                    }

                    var buffer = result.Buffer;
                    try
                    {
                        if (!buffer.IsEmpty)
                        {
                            foreach (var segment in buffer)
                            {
                                if (segment.Length == 0) continue;
                                try
                                {
                                    OnClientReceiveSpan?.Invoke(segment.Span);
                                }
                                catch (Exception ex)
                                {
                                    RaiseError(_userToken?.ID ?? "", ex);
                                }

                                if (!_isBaseClientType)
                                {
                                    try
                                    {
                                        OnClientReceive?.Invoke(segment.ToArray());
                                    }
                                    catch (Exception ex)
                                    {
                                        RaiseError(_userToken?.ID ?? "", ex);
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        reader.AdvanceTo(buffer.End);
                    }

                    if (result.IsCompleted) break;
                }
            });
        }

        /// <summary>
        /// 启动接管读循环的输入处理器。仅在配置了钩子且连接门面存在时启动，
        /// 与 <see cref="StartSpanPump"/> 互斥：配置处理器时事件泵不再启动。
        /// </summary>
        /// <returns>未配置钩子或启动成功返回 true；连接建立回调抛异常时断开连接并返回 false。</returns>
        private bool StartPipelineIfNeeded()
        {
            if (_inputHandler == null || _connection == null) return true;

            var connection = _connection;
            var userToken = _userToken;
            if (userToken == null || string.IsNullOrEmpty(userToken.ID)) return true;

            var channel = new ChannelInfo
            {
                ID = userToken.ID,
                ClientSocket = _socket,
                Stream = connection.Stream,
                UserToken = userToken,
                Tls = connection.Tls
            };

            try
            {
                _connectionOpened?.Invoke(channel);
            }
            catch (Exception ex)
            {
                // 通道未登记时继续启动输入处理器只会放大错误，按建链失败处理。
                RaiseError(userToken.ID, ex);
                Disconnect(ex);
                return false;
            }

            var cts = new CancellationTokenSource();
            _pipelineCts = cts;

            var flow = new ReceiveFlowControl(connection, Disconnect);
            var handler = _inputHandler;
            var input = connection.Input;

            _ = Task.Run(async () =>
            {
                try
                {
                    await handler(userToken, input, flow, cts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    RaiseError(userToken.ID, ex);
                }
                finally
                {
                    // 只在仍是当前泵时清空，避免重连后误清新令牌。
                    if (ReferenceEquals(_pipelineCts, cts)) _pipelineCts = null;
                    cts.Dispose();
                }
            });

            return true;
        }

        /// <summary>
        /// 取消输入处理器令牌。取消只置位，取消源由泵自身的收口负责释放。
        /// </summary>
        private void StopPipeline()
        {
            var cts = _pipelineCts;
            if (cts == null) return;

            try { cts.Cancel(); } catch (ObjectDisposedException) { }
        }

        /// <summary>
        /// TLS 模式下的应用数据发送。返回 true 表示已由 TLS 通道接管。
        /// </summary>
        private bool TrySendViaTls(ReadOnlySpan<byte> data)
        {
            if (!IsTlsActive) return false;

            var connection = _connection;
            if (connection == null)
            {
                RaiseError("", _tlsSetupException ?? new SaeaTlsException("TLS 连接尚未就绪。"));
                return true;
            }

            connection.SendApplicationData(data, ex => RaiseError(_userToken?.ID ?? "", ex));
            return true;
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
        }

        /// <summary>
        /// 处理接收操作
        /// </summary>
        /// <param name="readArgs">读取操作的SocketAsyncEventArgs对象</param>
        void ProcessReceive(SocketAsyncEventArgs readArgs)
        {
            if (readArgs == null) return;

            // 同一时刻只允许一个接收操作在途；背压恢复依赖该标志避免重复 ReceiveAsync。
            if (Interlocked.CompareExchange(ref _receiving, 1, 0) != 0) return;

            if (_userToken != null && _userToken.Socket != null && _userToken.Socket.Connected)
            {
                try
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
                catch (Exception ex)
                {
                    Interlocked.Exchange(ref _receiving, 0);
                    RaiseError(_userToken?.ID ?? "", ex);
                }
            }
            else
            {
                Interlocked.Exchange(ref _receiving, 0);
                OnError?.Invoke("", new Exception("SAEA SocketError:发送失败,当前连接已断开"));
            }
        }

        /// <summary>
        /// 触发 <see cref="OnError"/>。订阅者自身抛出异常时吞掉，避免在接收循环内二次抛出或被
        /// 外层 <c>catch</c> 再次触发（去重）。
        /// </summary>
        private void RaiseError(string id, Exception ex)
        {
            try { OnError?.Invoke(id, ex); } catch { }
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

                        var connection = _connection;
                        if (connection != null)
                        {
                            // 流 / TLS 模式：原始字节交给连接门面（TLS 解密后写入 Pipe）。
                            connection.OnRawReceived(dataSpan);
                        }
                        else
                        {
                            // 零拷贝路径：始终触发（无分配）
                            try
                            {
                                OnClientReceiveSpan?.Invoke(dataSpan);
                            }
                            catch (Exception ex)
                            {
                                RaiseError(_userToken.ID, ex);
                            }

                            // 兼容路径：仅当存在 byte[] 消费方时才复制
                            if (!_isBaseClientType)
                            {
                                var buffer = dataSpan.ToArray();

                                try
                                {
                                    OnClientReceive?.Invoke(buffer);
                                }
                                catch (Exception ex)
                                {
                                    RaiseError(_userToken.ID, ex);
                                }
                            }
                        }
                    }

                    // 本次接收已完成，允许下一次接收。
                    Interlocked.Exchange(ref _receiving, 0);

                    // Pipe 背压暂停：等待 ResumeReceive 恢复。
                    if (_connection != null && _connection.IsReceivePaused) return;

                    if (readArgs.SocketError == SocketError.Success && _userToken.Socket != null && _userToken.Socket.Connected)
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
                    Interlocked.Exchange(ref _receiving, 0);
                    Disconnect(new Exception("SAEA SocketError:远程服务器关闭连接"));
                }
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref _receiving, 0);
                RaiseError(_userToken.ID, ex);
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
                var token = _userToken;
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
                OnError?.Invoke(_userToken == null ? "" : _userToken.ID, ex);
            }
        }

        /// <summary>
        /// saea 异步发送核心：seg 指向的内存由 owner 持有；owner 为 null 表示调用方内存（零拷贝）。
        /// 所有权在发送完成（ProcessSended）或失败路径释放。
        /// </summary>
        private void SendAsyncRaw(ArraySegment<byte> seg, IDisposable owner)
        {
            var userToken = _userToken;
            if (seg.Array == null || seg.Count == 0)
            {
                owner?.Dispose();
                return;
            }
            bool transferred = false;
            try
            {
                if (userToken != null && userToken.Socket != null && userToken.Socket.Connected)
                {
                    if (userToken.WaitWrite(SocketOption.ActionTimeout))
                    {
                        userToken.SendingOwner = owner;
                        userToken.IsSending = true;
                        transferred = true;
                        var writeArgs = userToken.WriteArgs;
                        writeArgs.SetBuffer(seg.Array, seg.Offset, seg.Count);
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
                if (transferred)
                {
                    ProcessSended(userToken.WriteArgs);
                }
                else
                {
                    owner?.Dispose();
                    transferred = true;
                }
                OnError?.Invoke(userToken?.ID ?? "", ex);
                try { Disconnect(); } catch { }
            }
            finally
            {
                if (!transferred) owner?.Dispose();
            }
        }

        /// <summary>
        /// 同步发送：TLS 激活时走 TLS 通道，否则用 BeginSend/EndSend 循环发送入参副本；连接断开或超时经 OnError 报告
        /// </summary>
        /// <param name="data">待发送数据，为空时直接返回</param>
        public void Send(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) return;
            if (TrySendViaTls(data)) return;
            if (!Connected)
            {
                OnError?.Invoke("", new Exception("SAEA SocketError:发送失败,当前连接已断开"));
                return;
            }
            var userToken = _userToken;
            if (userToken == null) return;
            if (!userToken.WaitWrite(SocketOption.ActionTimeout))
            {
                OnError?.Invoke($"SAEA SocketError:发送消息时发生异常,{userToken.ID}", new TimeoutException("发送数据超时"));
                return;
            }
            try
            {
                var copy = data.ToArray();
                var offset = 0;
                do
                {
                    var iResult = _socket.BeginSend(copy, offset, copy.Length - offset, SocketFlags.None, null, null);
                    offset += _socket.EndSend(iResult);
                }
                while (offset < copy.Length);
                userToken.Actived = DateTimeHelper.Now;
            }
            catch (Exception ex)
            {
                Disconnect(ex);
            }
            finally
            {
                userToken.ReleaseWrite();
            }
        }

        /// <summary>
        /// 异步发送：能直接取到底层数组时零拷贝直发，否则复制进池化缓冲；TLS 激活时走 TLS 通道
        /// </summary>
        /// <param name="data">待发送数据，为空时直接返回</param>
        public void SendAsync(ReadOnlyMemory<byte> data)
        {
            if (data.Length == 0) return;
            if (TrySendViaTls(data.Span)) return;
            if (MemoryMarshal.TryGetArray(data, out var seg))
            {
                SendAsyncRaw(seg, null);
                return;
            }
            var writer = new PooledBufferWriter(data.Length);
            data.Span.CopyTo(writer.GetSpan(data.Length));
            writer.Advance(data.Length);
            writer.TryGetArray(out var rented);
            SendAsyncRaw(rented, writer);
        }

        /// <summary>
        /// 异步发送并转移缓冲所有权：零拷贝直发时由发送完成回调释放 owner，需复制时先复制再立即释放 owner；空数据直接释放
        /// </summary>
        /// <param name="data">待发送数据</param>
        /// <param name="owner">data 所占缓冲的所有权对象，可为 null</param>
        public void SendAsync(ReadOnlyMemory<byte> data, IDisposable owner)
        {
            if (data.Length == 0)
            {
                owner?.Dispose();
                return;
            }
            if (TrySendViaTls(data.Span))
            {
                owner?.Dispose();
                return;
            }
            if (MemoryMarshal.TryGetArray(data, out var segment) && segment.Array != null)
            {
                SendAsyncRaw(segment, owner);
                return;
            }
            PooledBufferWriter writer = null;
            ArraySegment<byte> rented;
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
            SendAsyncRaw(rented, writer);
        }

        /// <summary>
        /// 先按线格式编码再异步发送；编码失败或 coder 未初始化时经 OnError 报告，缓冲由本方法释放
        /// </summary>
        /// <param name="protocal">待发送的协议对象，为 null 时直接返回</param>
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
            writer.TryGetArray(out var rented);
            if (rented.Array != null && TrySendViaTls(new ReadOnlySpan<byte>(rented.Array, rented.Offset, rented.Count)))
            {
                writer.Dispose();
                return;
            }
            SendAsyncRaw(rented, writer);
        }

        /// <summary>
        /// 在线程池上执行异步发送，取消令牌只作用于排队阶段，不中断已开始的发送
        /// </summary>
        /// <param name="data">待发送数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>发送任务</returns>
        public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            return Task.Run(() => SendAsync(data), cancellationToken);
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
            var connection = _connection;
            if (connection == null)
                throw new KernelException("SAEA SocketError:当前方法只在stream实现中才能使用!");
            return connection.ReadAsync(buffer, offset, count, cancellationToken);
        }

        /// <summary>
        /// 获取流
        /// </summary>
        /// <returns></returns>
        public Stream GetStream()
        {
            var connection = _connection;
            if (connection == null)
                throw new InvalidOperationException("SAEA SocketError:当前连接未启用流模式或 TLS。");
            return connection.Stream;
        }

        /// <summary>
        /// 当前连接是否已完成 TLS 握手；未启用 TLS 时与 <see cref="Connected"/> 一致。
        /// </summary>
        public bool IsAuthenticated
        {
            get
            {
                if (!Connected) return false;
                var connection = _connection;
                if (!IsTlsActive) return true;
                return connection?.Tls?.IsAuthenticated == true;
            }
        }

        /// <summary>
        /// 连接级 TLS 信息；未启用 TLS 时为 null。
        /// </summary>
        public ISaeaTlsContext TlsContext => _connection?.Tls;

        /// <summary>
        /// 连接并在启用 TLS 时等待握手完成。
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>连接与握手均成功的任务</returns>
        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            CancellationTokenRegistration registration = default;
            if (cancellationToken.CanBeCanceled)
            {
                registration = cancellationToken.Register(() =>
                {
                    if (tcs.TrySetCanceled())
                    {
                        try { Disconnect(); } catch { }
                    }
                });
            }

            ConnectAsync(error =>
            {
                registration.Dispose();
                if (error == SocketError.Success)
                {
                    tcs.TrySetResult(true);
                }
                else
                {
                    var ex = _tlsSetupException ?? (_connection?.Tls?.HandshakeException);
                    if (ex != null)
                        tcs.TrySetException(ex);
                    else
                        tcs.TrySetException(new SocketException((int)error));
                }
            });

            return tcs.Task;
        }

        /// <summary>
        /// 主动断开连接
        /// </summary>
        /// <param name="ex">异常对象</param>
        public void Disconnect(Exception ex)
        {
            this.Connected = false;

            // 先取消读循环接手方，再由连接门面收口，避免泵在已完成的 PipeReader 上继续读。
            StopPipeline();

            var mex = ex;

            try
            {
                var connection = _connection;
                if (connection != null)
                {
                    try { connection.Dispose(); } catch { }
                }
                _connection = null;
            }
            catch { }

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