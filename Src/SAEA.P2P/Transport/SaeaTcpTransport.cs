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
*命名空间：SAEA.P2P.Transport
*文件名： SaeaTcpTransport
*版本号： v26.11.5.1
*唯一标识：921c36f5-1c83-4743-bb60-fa7dcfcc0b83
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 13:05:00
*描述：基于 SAEA.Sockets 的 TCP 传输实现
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 13:05:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TCP 传输实现
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Sockets;
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

namespace SAEA.P2P.Transport
{
    /// <summary>
    /// 基于 SAEA.Sockets 流模式的 TCP 传输实现。
    /// 无参构造得到连接侧实例，带本地端点的构造得到监听侧实例；两侧都只暴露字节流，分帧由上层约定。
    /// </summary>
    public sealed class SaeaTcpTransport : ITcpTransport
    {
        /// <summary>
        /// 保护接受队列与等待者的同步对象
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 本实例是否为监听侧
        /// </summary>
        private readonly bool _isListener;

        /// <summary>
        /// 监听侧的服务端套接字，连接侧为 null
        /// </summary>
        private readonly SaeaServerSocket _listener;

        /// <summary>
        /// 监听侧接受连接后得到的会话标识，连接侧为 null
        /// </summary>
        private readonly string _sessionId;

        /// <summary>
        /// 连接侧的客户端套接字，监听侧为 null
        /// </summary>
        private SaeaClientSocket _client;

        /// <summary>
        /// 本实例使用的字节流
        /// </summary>
        private Stream _stream;

        /// <summary>
        /// 本地端点
        /// </summary>
        private IPEndPoint _localEndPoint;

        /// <summary>
        /// 已接受但还没有被 AcceptAsync 取走的连接
        /// </summary>
        private readonly Queue<ITcpTransport> _pendingAccepted = new Queue<ITcpTransport>();

        /// <summary>
        /// 正在等待下一个已接受连接的调用者
        /// </summary>
        private readonly LinkedList<TaskCompletionSource<ITcpTransport>> _acceptWaiters = new LinkedList<TaskCompletionSource<ITcpTransport>>();

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 监听侧接受新连接时触发
        /// </summary>
        public event Action<ITcpTransport> Accepted;

        /// <summary>
        /// 创建连接侧传输实例，随后用 <see cref="ConnectAsync"/> 连接目标端点
        /// </summary>
        public SaeaTcpTransport()
        {
            _isListener = false;
        }

        /// <summary>
        /// 创建监听侧传输实例并立即开始监听；端口为 0 时由系统分配，实际端点从 <see cref="LocalEndPoint"/> 读取
        /// </summary>
        /// <param name="localEndPoint">本地监听端点</param>
        /// <param name="backlog">等待接受的连接队列长度</param>
        /// <param name="configure">可选的套接字选项补充配置，用于追加 TLS 等设置</param>
        /// <exception cref="ArgumentNullException">localEndPoint 为 null</exception>
        public SaeaTcpTransport(IPEndPoint localEndPoint, int backlog = 128, Action<SocketOptionBuilder> configure = null)
        {
            if (localEndPoint == null) throw new ArgumentNullException(nameof(localEndPoint));

            _isListener = true;

            var builder = CreateBuilder(localEndPoint.Address);
            builder.SetIPEndPoint(localEndPoint);
            if (configure != null) configure(builder);

            var server = new SaeaServerSocket(builder.Build());
            server.OnAccepted += OnListenerAccepted;

            try
            {
                server.Start(backlog);
            }
            catch
            {
                server.OnAccepted -= OnListenerAccepted;
                try { server.Dispose(); } catch { }
                throw;
            }

            _listener = server;
            _localEndPoint = server.LocalEndPoint ?? localEndPoint;
        }

        /// <summary>
        /// 包装监听侧已接受的通道。仅供本类与派生场景使用，调用方通过 <see cref="AcceptAsync"/> 或 <see cref="Accepted"/> 获取实例。
        /// </summary>
        /// <param name="listener">所属监听套接字</param>
        /// <param name="channel">已接受的通道</param>
        private SaeaTcpTransport(SaeaServerSocket listener, ChannelInfo channel)
        {
            _isListener = false;
            _listener = listener;
            _sessionId = channel.ID;
            _stream = channel.Stream;
            _localEndPoint = channel.ClientSocket?.LocalEndPoint as IPEndPoint;
        }

        /// <summary>
        /// 字节流；未连接时为 null
        /// </summary>
        public Stream Stream => _stream;

        /// <summary>
        /// 本地绑定的端点；未绑定或已释放时为 null
        /// </summary>
        public IPEndPoint LocalEndPoint => _disposed ? null : _localEndPoint;

        /// <summary>
        /// 是否可用于收发：连接侧要求连接已建立，监听侧要求仍在监听
        /// </summary>
        public bool IsConnected
        {
            get
            {
                if (_disposed) return false;
                if (_listener != null && _sessionId != null) return _stream != null;
                if (_client != null) return _client.Connected;
                return false;
            }
        }

        /// <summary>
        /// 连接到远端端点并取得字节流
        /// </summary>
        /// <param name="remoteEndPoint">目标端点</param>
        /// <param name="cancellationToken">取消令牌，只作用于连接建立阶段</param>
        /// <returns>连接建立后完成的任务</returns>
        /// <exception cref="ArgumentNullException">remoteEndPoint 为 null</exception>
        /// <exception cref="NotSupportedException">本实例是监听侧</exception>
        /// <exception cref="InvalidOperationException">本实例已连接或已释放</exception>
        /// <exception cref="SocketException">连接失败</exception>
        public async Task ConnectAsync(IPEndPoint remoteEndPoint, CancellationToken cancellationToken)
        {
            if (remoteEndPoint == null) throw new ArgumentNullException(nameof(remoteEndPoint));
            if (_isListener) throw new NotSupportedException("监听侧传输不能主动连接。");
            cancellationToken.ThrowIfCancellationRequested();

            lock (_sync)
            {
                if (_disposed) throw new ObjectDisposedException(GetType().Name);
                if (_client != null) throw new InvalidOperationException("该传输实例已经连接，不能重复连接。");
            }

            var builder = CreateBuilder(remoteEndPoint.Address);
            var client = new SaeaClientSocket(builder.Build());

            var error = await client.TryConnectAsync(remoteEndPoint).ConfigureAwait(false);
            if (error != SocketError.Success)
            {
                try { client.Dispose(); } catch { }
                throw new SocketException((int)error);
            }

            lock (_sync)
            {
                if (_disposed)
                {
                    try { client.Dispose(); } catch { }
                    throw new ObjectDisposedException(GetType().Name);
                }

                _client = client;
                _stream = client.GetStream();
                _localEndPoint = client.Socket?.LocalEndPoint as IPEndPoint;
            }
        }

        /// <summary>
        /// 监听侧等待下一个已接受的连接；连接侧不支持
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>已建立的连接侧传输对象</returns>
        /// <exception cref="NotSupportedException">本实例不是监听侧</exception>
        /// <exception cref="ObjectDisposedException">本实例已释放</exception>
        /// <exception cref="OperationCanceledException">等待期间被取消</exception>
        public Task<ITcpTransport> AcceptAsync(CancellationToken cancellationToken)
        {
            if (!_isListener) throw new NotSupportedException("只有监听侧传输才能接受连接。");
            cancellationToken.ThrowIfCancellationRequested();

            TaskCompletionSource<ITcpTransport> waiter = null;
            LinkedListNode<TaskCompletionSource<ITcpTransport>> node = null;

            lock (_sync)
            {
                if (_disposed) throw new ObjectDisposedException(GetType().Name);

                if (_pendingAccepted.Count > 0)
                    return Task.FromResult(_pendingAccepted.Dequeue());

                waiter = new TaskCompletionSource<ITcpTransport>(TaskCreationOptions.RunContinuationsAsynchronously);
                node = _acceptWaiters.AddLast(waiter);
            }

            if (!cancellationToken.CanBeCanceled) return waiter.Task;

            var registration = cancellationToken.Register(() =>
            {
                lock (_sync)
                {
                    if (waiter.Task.IsCompleted) return;
                    _acceptWaiters.Remove(node);
                }
                waiter.TrySetCanceled();
            });

            // 无论正常完成还是取消，都要撤销注册，避免取消源长期持有等待者
            return waiter.Task.ContinueWith(task =>
            {
                registration.Dispose();
                return task;
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).Unwrap();
        }

        /// <summary>
        /// 释放传输对象：连接侧关闭客户端套接字，监听侧停止监听并关闭仍未取走的连接
        /// </summary>
        public void Dispose()
        {
            SaeaClientSocket client;
            var pending = new List<ITcpTransport>();
            var waiters = new List<TaskCompletionSource<ITcpTransport>>();

            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;

                client = _client;
                _client = null;
                _stream = null;

                while (_pendingAccepted.Count > 0) pending.Add(_pendingAccepted.Dequeue());
                foreach (var waiter in _acceptWaiters) waiters.Add(waiter);
                _acceptWaiters.Clear();
            }

            foreach (var waiter in waiters) waiter.TrySetCanceled();
            foreach (var transport in pending)
            {
                try { transport.Dispose(); } catch { }
            }

            if (client != null)
            {
                try { client.Dispose(); } catch { }
            }

            if (_isListener && _listener != null)
            {
                try { _listener.OnAccepted -= OnListenerAccepted; } catch { }
                try { _listener.Dispose(); } catch { }
            }

            if (_sessionId != null && _listener != null)
            {
                try { _listener.Disconnect(_sessionId); } catch { }
            }
        }

        /// <summary>
        /// 按地址族创建流模式套接字选项构建器
        /// </summary>
        /// <param name="address">决定地址族的地址</param>
        /// <returns>已设置流模式与超时参数的构建器</returns>
        private static SocketOptionBuilder CreateBuilder(IPAddress address)
        {
            var builder = SocketOptionBuilder.Instance
                .SetSocket(SAEASocketType.Tcp)
                .UseStream()
                .SetActionTimeOut(30 * 1000)
                .SetFreeTime(180 * 1000);

            if (address != null && address.AddressFamily == AddressFamily.InterNetworkV6)
                builder.UseIPv6();

            return builder;
        }

        /// <summary>
        /// 监听侧接受连接：包装成传输对象，先交给等待者，没有等待者时入队
        /// </summary>
        /// <param name="obj">流模式下为通道对象</param>
        private void OnListenerAccepted(object obj)
        {
            var channel = obj as ChannelInfo;
            if (channel == null) return;

            // ICE 连接检查期间可能长时间静默，会话不能按空闲超时被清掉
            try { _listener?.SessionManager?.SetNoTimeout(channel.ID, true); } catch { }

            var transport = new SaeaTcpTransport(_listener, channel);

            TaskCompletionSource<ITcpTransport> waiter = null;
            lock (_sync)
            {
                if (_disposed)
                {
                    try { transport.Dispose(); } catch { }
                    return;
                }

                if (_acceptWaiters.Count > 0)
                {
                    waiter = _acceptWaiters.First.Value;
                    _acceptWaiters.RemoveFirst();
                }
                else
                {
                    _pendingAccepted.Enqueue(transport);
                }
            }

            if (waiter != null) waiter.TrySetResult(transport);

            var handler = Accepted;
            if (handler != null) handler(transport);
        }
    }
}