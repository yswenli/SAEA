/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.P2P.TurnServer
*文件名： TurnTlsListener
*版本号： v26.11.5.1
*唯一标识：4b66a3da-3b42-491b-9420-0426d383159d
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 02:30:00
*描述：TURN over TLS 控制连接监听
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 02:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TURN over TLS 控制连接监听
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading.Tasks;

using SAEA.Sockets;
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Model;
using SAEA.Sockets.Ssl;

namespace SAEA.P2P.TurnServer
{
    /// <summary>
    /// TURN over TLS 控制连接监听器：用 PFX 证书与口令完成 BouncyCastle TLS 握手，
    /// 握手成功后把这条流交给同一个 <see cref="TurnServer"/>；分帧与分派复用 TCP 的会话循环。
    /// </summary>
    public sealed class TurnTlsListener : IDisposable
    {
        /// <summary>
        /// 控制连接空闲超时，单位毫秒。
        /// SAEA.Sockets 默认 180s 会早于 TURN 的 permission 刷新间隔 240s，
        /// 导致刷新报文还没发出控制连接就被回收，因此调到 24 小时。
        /// </summary>
        internal const int ControlIdleTimeoutMilliseconds = 24 * 60 * 60 * 1000;

        /// <summary>
        /// 单次读取的最大字节数；分帧器自己保留半包状态，不依赖本次读取的边界
        /// </summary>
        private const int ReceiveBufferSize = 8192;

        /// <summary>
        /// 共用的 TURN 服务端
        /// </summary>
        private readonly TurnServer _turnServer;

        /// <summary>
        /// TLS 监听配置；构造阶段完成证书加载，避免启动时才发现证书不可用
        /// </summary>
        private readonly ISocketOption _socketOption;

        /// <summary>
        /// 保护监听实例、连接集合与释放状态
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 已握手的连接，释放时要一并关闭
        /// </summary>
        private readonly List<ChannelInfo> _channels = new List<ChannelInfo>();

        /// <summary>
        /// 底层 TLS 监听
        /// </summary>
        private SaeaServerSocket _server;

        /// <summary>
        /// 是否已启动
        /// </summary>
        private bool _started;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 创建监听器；使用默认 TLS 协议版本，构造阶段不绑定端口
        /// </summary>
        /// <param name="listenEndPoint">监听端点，端口为 0 时由系统分配</param>
        /// <param name="turnServer">共用的 TURN 服务端</param>
        /// <param name="certificatePath">PFX 证书文件路径</param>
        /// <param name="certificatePassword">证书口令；无口令时传 null 或空串</param>
        /// <exception cref="ArgumentNullException">端点、服务端或证书路径为 null</exception>
        /// <exception cref="ArgumentException">证书路径为空</exception>
        public TurnTlsListener(IPEndPoint listenEndPoint, TurnServer turnServer, string certificatePath,
            string certificatePassword)
            : this(listenEndPoint, turnServer, certificatePath, certificatePassword, SaeaTlsProtocols.Default)
        {
        }

        /// <summary>
        /// 创建监听器并指定 TLS 协议版本；构造阶段不绑定端口
        /// </summary>
        /// <param name="listenEndPoint">监听端点，端口为 0 时由系统分配</param>
        /// <param name="turnServer">共用的 TURN 服务端</param>
        /// <param name="certificatePath">PFX 证书文件路径</param>
        /// <param name="certificatePassword">证书口令；无口令时传 null 或空串</param>
        /// <param name="protocols">启用的 TLS 协议版本</param>
        /// <exception cref="ArgumentNullException">端点、服务端或证书路径为 null</exception>
        /// <exception cref="ArgumentException">证书路径为空</exception>
        public TurnTlsListener(IPEndPoint listenEndPoint, TurnServer turnServer, string certificatePath,
            string certificatePassword, SslProtocols protocols)
        {
            if (listenEndPoint == null)
            {
                throw new ArgumentNullException(nameof(listenEndPoint));
            }
            if (turnServer == null)
            {
                throw new ArgumentNullException(nameof(turnServer));
            }
            if (certificatePath == null)
            {
                throw new ArgumentNullException(nameof(certificatePath));
            }
            if (certificatePath.Length == 0)
            {
                throw new ArgumentException("必须提供 TLS 证书文件路径", nameof(certificatePath));
            }

            _turnServer = turnServer;
            _socketOption = SocketOptionBuilder.Instance
                .UseStream()
                .SetIPEndPoint(listenEndPoint)
                .SetReadBufferSize(ReceiveBufferSize)
                .SetFreeTime(ControlIdleTimeoutMilliseconds)
                .WithSsl(protocols, certificatePath, certificatePassword)
                .Build();
        }

        /// <summary>
        /// 绑定后的实际本地端点；未启动或已释放时为 null
        /// </summary>
        public IPEndPoint LocalEndPoint
        {
            get
            {
                SaeaServerSocket server;
                lock (_sync)
                {
                    server = _server;
                }

                return server == null ? null : server.LocalEndPoint;
            }
        }

        /// <summary>
        /// 绑定并开始接受 TLS 连接；重复调用无副作用
        /// </summary>
        /// <exception cref="ObjectDisposedException">已释放</exception>
        /// <exception cref="SocketException">绑定或监听失败</exception>
        public void Start()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }
                if (_started)
                {
                    return;
                }

                var server = new SaeaServerSocket(_socketOption);
                server.OnAccepted += OnAccepted;
                try
                {
                    server.Start();
                }
                catch (Exception)
                {
                    // 绑定失败时释放已创建的会话池，避免端口或缓冲被占住
                    try { server.Dispose(); } catch (Exception) { }
                    throw;
                }

                _server = server;
                _started = true;
            }
        }

        /// <summary>
        /// 关闭全部已接受连接与监听；可重复调用
        /// </summary>
        public void Dispose()
        {
            SaeaServerSocket server;
            List<ChannelInfo> channels;
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                server = _server;
                _server = null;
                channels = new List<ChannelInfo>(_channels);
                _channels.Clear();
            }

            // 先关连接再关监听：连接的读循环会走 finally 释放该连接上的 allocation
            for (var i = 0; i < channels.Count; i++)
            {
                CloseChannel(channels[i]);
            }

            if (server != null)
            {
                try { server.Stop(); } catch (Exception) { }
                try { server.Dispose(); } catch (Exception) { }
            }
        }

        /// <summary>
        /// 握手完成后的接入回调；证书握手失败时 SAEA.Sockets 不会触发该回调
        /// </summary>
        /// <param name="obj">通道对象；流模式下为 <see cref="ChannelInfo"/></param>
        private void OnAccepted(object obj)
        {
            var channel = obj as ChannelInfo;
            if (channel == null)
            {
                return;
            }
            if (channel.ClientSocket == null || channel.Stream == null)
            {
                CloseChannel(channel);
                return;
            }

            IPEndPoint localEndPoint;
            IPEndPoint remoteEndPoint;
            try
            {
                localEndPoint = channel.ClientSocket.LocalEndPoint as IPEndPoint;
                remoteEndPoint = channel.ClientSocket.RemoteEndPoint as IPEndPoint;
            }
            catch (ObjectDisposedException)
            {
                CloseChannel(channel);
                return;
            }
            catch (SocketException)
            {
                CloseChannel(channel);
                return;
            }

            if (localEndPoint == null || remoteEndPoint == null)
            {
                CloseChannel(channel);
                return;
            }

            var control = new StreamTurnControlChannel(channel.Stream, localEndPoint, remoteEndPoint);
            var shouldClose = false;
            lock (_sync)
            {
                if (_disposed)
                {
                    shouldClose = true;
                }
                else
                {
                    _channels.Add(channel);
                }
            }

            if (shouldClose)
            {
                CloseChannel(channel);
                return;
            }

            var stream = channel.Stream;
            var session = new TurnStreamSession(_turnServer, control,
                buffer => stream.ReadAsync(buffer, 0, buffer.Length));
            var ignored = Task.Run(() => RunSessionAsync(session, channel));
        }

        /// <summary>
        /// 运行单条连接的会话循环，结束后关闭该连接
        /// </summary>
        /// <param name="session">会话循环</param>
        /// <param name="channel">连接对应的通道</param>
        /// <returns>会话结束的任务</returns>
        private async Task RunSessionAsync(TurnStreamSession session, ChannelInfo channel)
        {
            try
            {
                await session.RunAsync().ConfigureAwait(false);
            }
            finally
            {
                lock (_sync)
                {
                    _channels.Remove(channel);
                }

                CloseChannel(channel);
            }
        }

        /// <summary>
        /// 关闭连接的流与 socket，并忽略重复关闭
        /// </summary>
        /// <param name="channel">连接对应的通道；可为 null</param>
        private static void CloseChannel(ChannelInfo channel)
        {
            if (channel == null)
            {
                return;
            }

            try
            {
                if (channel.Stream != null)
                {
                    channel.Stream.Dispose();
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception)
            {
            }

            try
            {
                if (channel.ClientSocket != null)
                {
                    channel.ClientSocket.Dispose();
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception)
            {
            }
        }
    }
}