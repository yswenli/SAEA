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
*文件名： StreamServerSocket
*版本号： v26.4.23.1
*唯一标识：f5a8059b-284f-42b1-ac04-273e261cda4a
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/01/23 11:16:53
*描述：StreamServerSocket套接字类
*
*=====================================================================
*修改标记
*修改时间：2019/01/23 11:16:53
*修改人： yswenli
*版本号： v26.4.23.1
*描述：StreamServerSocket套接字类
*
*****************************************************************************/
using SAEA.Common.Caching;
using SAEA.Sockets.Base;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets.Core.Tcp
{
    /// <summary>
    /// 服务器 socket
    /// </summary>
    public class StreamServerSocket : IServerSocket, IDisposable
    {
        Socket _listener;

        int _clientCounts;

        private readonly CancellationToken _cancellationToken;

        /// <summary>
        /// 本服务器已接受连接的用户令牌集合。用于在 <see cref="Stop"/> 时逐连接取消/完成
        /// <see cref="StreamUserToken.Input"/>（<see cref="ChannelManager.Clear"/> 不会触碰令牌）。
        /// </summary>
        private readonly ConcurrentDictionary<string, StreamUserToken> _tokens = new ConcurrentDictionary<string, StreamUserToken>();

        /// <summary>
        /// 客户端连接数
        /// </summary>
        public int ClientCounts { get => _clientCounts; private set => _clientCounts = value; }

        /// <summary>
        /// 配置项
        /// </summary>
        public ISocketOption SocketOption { get; set; }

        volatile bool _isStoped = true;

        /// <summary>
        /// 是否已释放
        /// </summary>
        public bool IsDisposed
        {
            get; set;
        } = false;

        #region events

        /// <summary>
        /// 客户端连接事件
        /// </summary>
        public event OnAcceptedHandler OnAccepted;
        /// <summary>
        /// 错误事件
        /// </summary>
        public event OnErrorHandler OnError;
        /// <summary>
        /// 客户端断开事件
        /// </summary>
        public event OnDisconnectedHandler OnDisconnected;
        /// <summary>
        /// 接收数据事件
        /// </summary>
        public event OnReceiveHandler OnReceive;

        /// <summary>
        /// 接收数据事件（Span 版本）。data 仅在回调期间有效。
        /// </summary>
        public event OnServerReceiveSpanHandler OnServerReceiveSpan;

        #endregion

        /// <summary>
        /// 服务器 socket
        /// </summary>
        /// <param name="socketOption">socket 配置选项</param>
        /// <param name="cancellationToken">取消令牌</param>
        public StreamServerSocket(ISocketOption socketOption, CancellationToken cancellationToken)
        {
            SocketOption = socketOption;
            _cancellationToken = cancellationToken;
        }

        /// <summary>
        /// 为单条连接创建编码器。镜像 <see cref="SAEA.Sockets.Core.UserTokenFactory"/>：优先按
        /// <see cref="ISocketOption.Context"/> 中配置的 Unpacker 类型新建实例；Stream 模式通常不设置
        /// Context（<see cref="SocketOptionBuilder.UseStream"/> 不设置），此时回退到
        /// <see cref="BaseCoder"/>。
        /// </summary>
        /// <returns>新的编码器实例</returns>
        private ICoder CreateCoder()
        {
            var unpacker = SocketOption?.Context?.Unpacker;
            if (unpacker != null)
            {
                return (ICoder)Activator.CreateInstance(unpacker.GetType());
            }
            return new BaseCoder();
        }

        /// <summary>
        /// 启动服务
        /// </summary>
        /// <param name="backlog">挂起连接队列的最大长度</param>
        public void Start(int backlog = 10 * 1000)
        {
            if (_listener == null && _isStoped)
            {
                IPEndPoint ipEndPoint = null;

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

                _listener.Bind(ipEndPoint);

                _listener.Listen(backlog);

                _isStoped = false;

                Task.Run(ProcessAccepte);
            }
        }

        /// <summary>
        /// 远程证书验证回调
        /// </summary>
        public System.Net.Security.RemoteCertificateValidationCallback RemoteCertificateValidationCallback { get; set; }

        /// <summary>
        /// 会话管理器
        /// </summary>
        public SessionManager SessionManager => throw new NotImplementedException();

        /// <summary>
        /// 处理接受客户端连接
        /// </summary>
        private async Task ProcessAccepte()
        {
            while (!_isStoped)
            {
                Socket clientSocket = null;
                try
                {
                    if (_listener == null) break;

                    try
                    {
                        clientSocket = await _listener.AcceptAsync().ConfigureAwait(false);

                        clientSocket.NoDelay = SocketOption.NoDelay;

                        clientSocket.ReceiveBufferSize = SocketOption.ReadBufferSize;

                        clientSocket.SendBufferSize = SocketOption.WriteBufferSize;

                        Stream nsStream;

                        if (SocketOption.WithSsl)
                        {
                            nsStream = new SslStream(new NetworkStream(clientSocket), false);

                            await ((SslStream)nsStream).AuthenticateAsServerAsync(SocketOption.X509Certificate2, false, SslProtocols.Ssl3 | SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12, false).ConfigureAwait(false);
                        }
                        else
                        {
                            nsStream = new NetworkStream(clientSocket, true);
                        }

                        var id = clientSocket.RemoteEndPoint.ToString();

                        var ci = ChannelManager.Instance.Set(id, clientSocket, nsStream);

                        var token = new StreamUserToken
                        {
                            ID = id,
                            Socket = clientSocket,
                            Stream = nsStream,
                            Coder = CreateCoder()
                        };

                        token.Input = PipeReader.Create(nsStream, new StreamPipeReaderOptions(leaveOpen: true));

                        ci.UserToken = token;

                        _tokens[id] = token;

                        OnAccepted?.Invoke(ci);

                        _ = Task.Run(() => ProcessAccepted(ci, token));
                    }
                    catch
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), _cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (ObjectDisposedException oex)
                {
                    OnError?.Invoke(string.Empty, oex);
                }
                catch (AuthenticationException aex)
                {
                    OnError?.Invoke(string.Empty, aex);
                    OnDisconnected?.Invoke(SocketOption.IP + "_" + SocketOption.Port, aex);
                    clientSocket?.Close(SocketOption.ActionTimeout);
                }
                catch (Exception exception)
                {
                    OnError?.Invoke(string.Empty, exception);

                    if (exception is SocketException s && s.SocketErrorCode == SocketError.OperationAborted)
                    {
                        OnDisconnected?.Invoke(SocketOption.IP + "_" + SocketOption.Port, exception);
                    }
                    clientSocket?.Close(SocketOption.ActionTimeout);
                }
            }
        }

        /// <summary>
        /// 处理已接受的客户端连接。仅在存在接收订阅者时读取：使用每连接的 <see cref="PipeReader"/>
        /// 循环读取，逐段触发 <see cref="OnServerReceiveSpan"/>（零拷贝，仅回调期间有效）与
        /// <see cref="OnReceive"/>（byte[]，Plan 2C 前兼容）。多段 <see cref="ReadOnlySequence{T}"/>
        /// 按段投递；需要连续帧的消费者应使用 <see cref="IFrameCoder.DecodeStream"/> 拆帧内核，而非假设
        /// 一次回调等于一个 PDU。
        /// </summary>
        /// <remarks>
        /// 循环条件为 <c>!_isStoped &amp;&amp; (OnReceive != null || OnServerReceiveSpan != null)</c>，
        /// 精确保持旧实现 <c>!_isStoped &amp;&amp; OnReceive != null</c> 的门控语义。原因：当无人订阅接收事件时，
        /// 基于 <see cref="OnAcceptedHandler"/> 的消费者会自行读取 <see cref="ChannelInfo.Stream"/>——如
        /// <c>SAEA.MQTT.Implementations.MqttTcpServerListener</c>、<c>SAEA.WebSocket.Core.WSSServerImpl</c>、
        /// <c>SAEA.Socket5.Server.Socks5Server</c>——若此处无条件读取会与其争抢同一 <see cref="Stream"/>。
        /// 在循环内判空与在接纳时判空等价：旧实现在无订阅者时同样永不进入循环并永久退出。
        /// </remarks>
        /// <param name="ci">通道信息</param>
        /// <param name="token">通道用户令牌；其 <see cref="StreamUserToken.Input"/> 以 <c>leaveOpen: true</c>
        /// 创建，完成/取消读取均不释放 <see cref="StreamUserToken.Stream"/>（网络流归 <see cref="ChannelInfo.Stream"/> 所有）</param>
        async Task ProcessAccepted(ChannelInfo ci, StreamUserToken token)
        {
            if (ci == null || token == null) return;

            var reader = token.Input;

            if (reader == null)
            {
                _tokens.TryRemove(ci.ID, out _);
                return;
            }

            try
            {
                while (!_isStoped && (OnReceive != null || OnServerReceiveSpan != null))
                {
                    ReadResult result;
                    try
                    {
                        result = await reader.ReadAsync(_cancellationToken).ConfigureAwait(false);
                    }
                    catch (IOException iex)
                    {
                        if (!_isStoped) OnDisconnected?.Invoke(ci.ID, iex);
                        break;
                    }
                    catch (SocketException sex)
                    {
                        if (!_isStoped) OnDisconnected?.Invoke(ci.ID, sex);
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (!_isStoped) OnError?.Invoke(ci.ID, ex);
                        break;
                    }

                    if (result.IsCanceled) break;

                    var buffer = result.Buffer;

                    try
                    {
                        foreach (var segment in buffer)
                        {
                            if (segment.Length == 0) continue;
                            ChannelManager.Instance.Refresh(ci.ID);
                            OnServerReceiveSpan?.Invoke(token, segment.Span);
                            OnReceive?.Invoke(token, segment.ToArray());
                        }
                    }
                    finally
                    {
                        try { reader.AdvanceTo(buffer.End); } catch { }
                    }

                    if (result.IsCompleted) break;
                }
            }
            finally
            {
                try { reader.Complete(); } catch { }
                _tokens.TryRemove(ci.ID, out _);
            }
        }

        /// <summary>
        /// 获取当前会话对象
        /// </summary>
        /// <param name="sessionID">会话ID</param>
        /// <returns>会话对象</returns>
        public object GetCurrentObj(string sessionID)
        {
            return ChannelManager.Instance.Get(sessionID);
        }

        /// <summary>
        /// 异步发送数据
        /// </summary>
        /// <param name="sessionID">会话ID</param>
        /// <param name="data">数据</param>
        public void SendAsync(string sessionID, byte[] data)
        {
            var channel = ChannelManager.Instance.Get(sessionID);
            ChannelManager.Instance.Refresh(sessionID);
            if (channel == null || channel.ClientSocket == null || !channel.ClientSocket.Connected)
                throw new KernelException("Failed to send data,current session does not exist！");
            channel.Stream.WriteAsync(data, 0, data.Length);
        }

        /// <summary>
        /// 同步发送数据
        /// </summary>
        /// <param name="sessionID">会话ID</param>
        /// <param name="data">数据</param>
        public void Send(string sessionID, byte[] data)
        {
            var channel = ChannelManager.Instance.Get(sessionID);
            ChannelManager.Instance.Refresh(sessionID);
            channel.Stream.Write(data, 0, data.Length);
        }

        /// <summary>
        /// 结束会话并发送数据
        /// </summary>
        /// <param name="sessionID">会话ID</param>
        /// <param name="data">数据</param>
        public void End(string sessionID, byte[] data)
        {
            var channel = ChannelManager.Instance.Get(sessionID);
            ChannelManager.Instance.Refresh(sessionID);
            if (channel != null && channel.Stream != null && channel.Stream.CanWrite)
            {
                channel.Stream.Write(data, 0, data.Length);
                Disconnect(sessionID);
            }
        }

        /// <summary>
        /// 异步发送数据到指定终结点
        /// </summary>
        /// <param name="ipEndPoint">终结点</param>
        /// <param name="data">数据</param>
        public void SendAsync(IPEndPoint ipEndPoint, byte[] data)
        {
            SendAsync(ipEndPoint.ToString(), data);
        }

        public void Send(string sessionID, ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) return;
            Send(sessionID, data.ToArray());
        }

        public void SendAsync(string sessionID, ReadOnlyMemory<byte> data)
        {
            if (data.Length == 0) return;
            SendAsync(sessionID, data.ToArray());
        }

        public void SendAsync(string sessionID, ISocketProtocal protocal)
        {
            if (protocal == null) return;
            var coder = SocketOption?.Context?.Unpacker;
            if (coder == null) return;
            using (var writer = new PooledBufferWriter(protocal.BodyLength > 0 && protocal.BodyLength < int.MaxValue - 64 ? (int)protocal.BodyLength + 64 : 64))
            {
                coder.Encode(protocal, writer);
                SendAsync(sessionID, writer.WrittenSpan.ToArray());
            }
        }

        public void End(string sessionID, ReadOnlyMemory<byte> data)
        {
            End(sessionID, data.ToArray());
        }

        public void SendAsync(IPEndPoint ipEndPoint, ReadOnlyMemory<byte> data)
        {
            SendAsync(ipEndPoint.ToString(), data.ToArray());
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        /// <param name="sessionID">会话ID</param>
        public void Disconnect(string sessionID)
        {
            if (string.IsNullOrEmpty(sessionID)) return;

            var channel = ChannelManager.Instance.Get(sessionID);

            // 会话可能已被 Stop()/Clear() 回收，或已被先前的一次 Disconnect 移除，
            // 此处必须判空（否则 channel.ClientSocket 会抛 NullReferenceException）
            if (channel == null)
            {
                ChannelManager.Instance.Remove(sessionID);
                return;
            }

            var socket = channel.ClientSocket;
            if (socket != null)
            {
                try
                {
                    socket.Close();
                }
                catch { }

                OnDisconnected?.Invoke(sessionID, null);
            }
            ChannelManager.Instance.Remove(sessionID);
        }

        /// <summary>
        /// 关闭。先置 <c>_isStoped</c>，再逐连接 <see cref="StreamUserToken.Clear"/>（取消挂起读取并完成
        /// <see cref="StreamUserToken.Input"/>），最后 <see cref="ChannelManager.Clear"/> 关闭底层 Socket。
        /// </summary>
        public void Stop()
        {
            _isStoped = true;
            try
            {
                foreach (var token in _tokens.Values)
                {
                    try { token.Clear(); } catch { }
                }
                _tokens.Clear();

                ChannelManager.Instance.Clear();
                SocketOption.X509Certificate2?.Dispose();
                _listener.Close();
            }
            catch { }

            try
            {
                _listener?.Dispose();
                _listener = null;
            }
            catch { }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Stop();
            ChannelManager.Instance.Clear();
            IsDisposed = true;
        }
    }
}