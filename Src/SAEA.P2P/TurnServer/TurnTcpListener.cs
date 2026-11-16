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
*文件名： TurnTcpListener
*版本号： v26.11.5.1
*唯一标识：d6b43e09-7b9a-47cd-bc22-231c93231643
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 01:30:00
*描述：TURN over TCP 控制连接监听与读取循环
*
*=====================================================================
*修改标记
*修改时间：2026/11/16 11:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：IPv6Any 监听启用双栈
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SAEA.P2P.TurnServer
{
    /// <summary>
    /// TURN over TCP 控制连接监听器：只负责接受连接、按流式分帧器切出 STUN 报文与 ChannelData，
    /// 再交给同一个 <see cref="TurnServer"/> 处理；分帧与分派走 TCP 与 TLS 共用的会话循环。
    /// 监听 IPv6 任意地址时开启 DualMode，同一套接字同时接受 IPv4 与 IPv6 连接。
    /// </summary>
    public sealed class TurnTcpListener : IDisposable
    {
        /// <summary>
        /// 监听队列长度
        /// </summary>
        private const int Backlog = 512;

        /// <summary>
        /// 监听端点
        /// </summary>
        private readonly IPEndPoint _listenEndPoint;

        /// <summary>
        /// 共用的 TURN 服务端
        /// </summary>
        private readonly TurnServer _turnServer;

        /// <summary>
        /// 保护监听 socket、连接集合与释放状态
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 已接受的连接，释放时要一并关闭
        /// </summary>
        private readonly List<Socket> _connections = new List<Socket>();

        /// <summary>
        /// 监听 socket
        /// </summary>
        private Socket _listener;

        /// <summary>
        /// 是否已启动
        /// </summary>
        private bool _started;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 创建监听器；构造阶段不绑定端口
        /// </summary>
        /// <param name="listenEndPoint">监听端点，端口为 0 时由系统分配</param>
        /// <param name="turnServer">共用的 TURN 服务端</param>
        /// <exception cref="ArgumentNullException">任一参数为 null</exception>
        public TurnTcpListener(IPEndPoint listenEndPoint, TurnServer turnServer)
        {
            if (listenEndPoint == null)
            {
                throw new ArgumentNullException(nameof(listenEndPoint));
            }
            if (turnServer == null)
            {
                throw new ArgumentNullException(nameof(turnServer));
            }

            _listenEndPoint = listenEndPoint;
            _turnServer = turnServer;
        }

        /// <summary>
        /// 绑定后的实际本地端点；未启动或已释放时为 null
        /// </summary>
        public IPEndPoint LocalEndPoint
        {
            get
            {
                Socket listener;
                lock (_sync)
                {
                    listener = _listener;
                }

                if (listener == null)
                {
                    return null;
                }

                try
                {
                    return listener.LocalEndPoint as IPEndPoint;
                }
                catch (ObjectDisposedException)
                {
                    return null;
                }
                catch (SocketException)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// 绑定并开始接受连接；监听 IPv6 任意地址时开启 DualMode，同一套接字同时接受 IPv4 与 IPv6 连接；重复调用无副作用
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

                var listener = new Socket(_listenEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                // IPv6Any 上 IPv6 套接字默认只收 IPv6，必须显式打开 DualMode 才能同时收 IPv4
                if (_listenEndPoint.AddressFamily == AddressFamily.InterNetworkV6
                    && _listenEndPoint.Address.Equals(IPAddress.IPv6Any))
                {
                    listener.DualMode = true;
                }
                listener.Bind(_listenEndPoint);
                listener.Listen(Backlog);
                _listener = listener;
                _started = true;
            }

            var ignored = Task.Run(() => AcceptLoopAsync());
        }

        /// <summary>
        /// 关闭监听与全部连接，并释放这些连接上的分配；可重复调用
        /// </summary>
        public void Dispose()
        {
            Socket listener;
            List<Socket> connections;
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                listener = _listener;
                _listener = null;
                connections = new List<Socket>(_connections);
                _connections.Clear();
            }

            DisposeSocket(listener);
            for (var i = 0; i < connections.Count; i++)
            {
                DisposeSocket(connections[i]);
            }
        }

        /// <summary>
        /// 接受连接循环；监听 socket 关闭后自然退出
        /// </summary>
        /// <returns>循环结束的任务</returns>
        private async Task AcceptLoopAsync()
        {
            Socket listener;
            lock (_sync)
            {
                listener = _listener;
            }

            if (listener == null)
            {
                return;
            }

            while (true)
            {
                Socket accepted;
                try
                {
                    accepted = await listener.AcceptAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException)
                {
                    return;
                }
                catch (InvalidOperationException)
                {
                    return;
                }

                var shouldClose = false;
                lock (_sync)
                {
                    if (_disposed)
                    {
                        shouldClose = true;
                    }
                    else
                    {
                        _connections.Add(accepted);
                    }
                }

                if (shouldClose)
                {
                    DisposeSocket(accepted);
                    return;
                }

                var connection = accepted;
                var ignored = Task.Run(() => ConnectionLoopAsync(connection));
            }
        }

        /// <summary>
        /// 单条连接的读取循环：交给共用的流式会话循环处理，连接断开时释放该连接上的全部分配
        /// </summary>
        /// <param name="connection">已连接的 socket</param>
        /// <returns>循环结束的任务</returns>
        private async Task ConnectionLoopAsync(Socket connection)
        {
            TcpTurnControlChannel control = null;
            TurnStreamSession session = null;
            try
            {
                var localEndPoint = connection.LocalEndPoint as IPEndPoint;
                var remoteEndPoint = connection.RemoteEndPoint as IPEndPoint;
                if (localEndPoint == null || remoteEndPoint == null)
                {
                    return;
                }

                control = new TcpTurnControlChannel(connection, localEndPoint, remoteEndPoint);
                session = new TurnStreamSession(_turnServer, control,
                    buffer => connection.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None));
                await session.RunAsync().ConfigureAwait(false);
            }
            finally
            {
                // 会话循环正常退出时已完成分配回收；构造失败时才需要在这里兜底释放
                if (control != null && session == null)
                {
                    _turnServer.ReleaseControlChannel(control);
                }

                // 连接被接管为数据连接后所有权归中继：这里再关会立刻打断双向泵，中继泵结束时自行关闭
                if (session == null || !session.IsTakenOver)
                {
                    DisposeSocket(connection);
                }

                // 接管与否都要从列表摘掉，否则长连接会把 _connections 撑到无界
                lock (_sync)
                {
                    _connections.Remove(connection);
                }
            }
        }

        /// <summary>
        /// 关闭 socket 并忽略重复关闭
        /// </summary>
        /// <param name="socket">socket；可为 null</param>
        private static void DisposeSocket(Socket socket)
        {
            if (socket == null)
            {
                return;
            }

            try
            {
                socket.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}