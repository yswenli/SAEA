﻿/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets.Core.Tcp
*文件名： StreamServerSocket
*版本号： v7.0.0.1
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
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Sockets.Handler;
using SAEA.Sockets.Model;

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

                        OnAccepted?.Invoke(ci);

                        _ = Task.Run(() => ProcessAccepted(id, nsStream));
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
                    clientSocket?.Close(SocketOption.TimeOut);
                }
                catch (Exception exception)
                {
                    OnError?.Invoke(string.Empty, exception);

                    if (exception is SocketException s && s.SocketErrorCode == SocketError.OperationAborted)
                    {
                        OnDisconnected?.Invoke(SocketOption.IP + "_" + SocketOption.Port, exception);
                    }
                    clientSocket?.Close(SocketOption.TimeOut);
                }
            }
        }

        /// <summary>
        /// 处理已接受的客户端连接
        /// </summary>
        /// <param name="id">会话ID</param>
        /// <param name="nsStream">网络流</param>
        async Task ProcessAccepted(string id, Stream nsStream)
        {
            await Task.Yield();

            while (!_isStoped && OnReceive != null)
            {
                try
                {
                    var data = new byte[SocketOption.ReadBufferSize];

                    var len = nsStream.Read(data, 0, data.Length);

                    if (len > 0)
                    {
                        ChannelManager.Instance.Refresh(id);
                        OnReceive.Invoke(new Session(id), data.AsSpan().Slice(0, len).ToArray());
                    }
                }
                catch (IOException iex)
                {
                    OnDisconnected?.Invoke(id, iex);
                    break;
                }
                catch (SocketException sex)
                {
                    OnDisconnected?.Invoke(id, sex);
                    break;
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(id, ex);
                }
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

        /// <summary>
        /// 断开连接
        /// </summary>
        /// <param name="sessionID">会话ID</param>
        public void Disconnect(string sessionID)
        {
            var channel = ChannelManager.Instance.Get(sessionID);
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
        /// 关闭
        /// </summary>
        public void Stop()
        {
            _isStoped = true;
            try
            {
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
