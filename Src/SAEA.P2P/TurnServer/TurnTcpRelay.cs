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
*文件名： TurnTcpRelay
*版本号： v26.11.5.1
*唯一标识：60ceb70e-4533-4c05-a3fd-d18484d28e9d
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 04:00:00
*描述：TURN TCP 中继监听与双向数据泵
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 13:54:41
*修改人： yswenli
*版本号： v26.11.5.1
*描述：把 Bind 拆成 PrepareBind 与启动动作，让调用方先写完 ConnectionBind 应答再启动数据泵
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Common;

namespace SAEA.P2P.TurnServer
{
    /// <summary>
    /// 单个 TCP 分配的中继端：持有一个已绑定的监听 socket，接受对端连接并登记到连接表，
    /// ConnectionBind 之后把某条对端连接与数据连接对接成双向字节泵。
    /// 监听 socket 的所有权归本类，中继端口由服务端归还。
    /// </summary>
    internal sealed class TurnTcpRelay : IDisposable
    {
        /// <summary>
        /// 单方向中继缓冲大小；每个方向固定一份，避免每轮读取都分配
        /// </summary>
        private const int RelayBufferSize = 8192;

        /// <summary>
        /// 所属分配
        /// </summary>
        private readonly TurnAllocation _allocation;

        /// <summary>
        /// 共用的连接表
        /// </summary>
        private readonly TurnConnectionTable _connections;

        /// <summary>
        /// 已绑定好的中继监听 socket；释放责任在本类
        /// </summary>
        private readonly Socket _listener;

        /// <summary>
        /// 未绑定连接允许停留的时间
        /// </summary>
        private readonly TimeSpan _bindTimeout;

        /// <summary>
        /// 中继地址，即监听 socket 的本地端点
        /// </summary>
        private readonly IPEndPoint _relayedEndPoint;

        /// <summary>
        /// 保护启动标志、释放标志与已绑定流集合
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 已绑定并正在泵数据的流；释放时统一关闭，用来打断阻塞在读取上的泵
        /// </summary>
        private readonly List<Stream> _boundStreams = new List<Stream>();

        /// <summary>
        /// 未绑定连接的清理定时器；未启动时为 null
        /// </summary>
        private Timer _purgeTimer;

        /// <summary>
        /// accept 循环是否已启动
        /// </summary>
        private bool _started;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 创建 TCP 中继端
        /// </summary>
        /// <param name="allocation">所属分配</param>
        /// <param name="connections">共用的连接表</param>
        /// <param name="listener">已绑定的中继监听 socket，所有权交给本类</param>
        /// <param name="bindTimeout">未绑定连接允许停留的时间</param>
        /// <exception cref="ArgumentNullException">任一参数为 null</exception>
        public TurnTcpRelay(TurnAllocation allocation, TurnConnectionTable connections, Socket listener, TimeSpan bindTimeout)
        {
            if (allocation == null)
            {
                throw new ArgumentNullException(nameof(allocation));
            }
            if (connections == null)
            {
                throw new ArgumentNullException(nameof(connections));
            }
            if (listener == null)
            {
                throw new ArgumentNullException(nameof(listener));
            }

            _allocation = allocation;
            _connections = connections;
            _listener = listener;
            _bindTimeout = bindTimeout;
            _relayedEndPoint = listener.LocalEndPoint as IPEndPoint;
        }

        /// <summary>
        /// 中继地址，必须与 Allocate 应答里的 XOR-RELAYED-ADDRESS 一致
        /// </summary>
        internal IPEndPoint RelayedEndPoint
        {
            get { return _relayedEndPoint; }
        }

        /// <summary>
        /// 是否有新的中继连接到达的通知回调；由服务端在启动 accept 循环前挂上，负责在控制连接上发 ConnectionAttempt
        /// </summary>
        internal Action<TurnConnectionRecord> ConnectionAttemptNotifier { get; set; }

        /// <summary>
        /// 启动 accept 循环；重复调用无副作用
        /// </summary>
        public void Start()
        {
            lock (_sync)
            {
                if (_disposed || _started)
                {
                    return;
                }

                _started = true;

                // 只建连不绑定会让对端 socket 一直挂着，超时必须定时清掉
                if (_bindTimeout > TimeSpan.Zero)
                {
                    _purgeTimer = new Timer(PurgeExpiredConnections, null, _bindTimeout, _bindTimeout);
                }
            }

            var ignored = Task.Run(() => AcceptLoopAsync());
        }

        /// <summary>
        /// 校验并登记数据流，返回启动双向泵的动作；调用方必须先写完应答再执行该动作，否则数据面字节会与应答交错
        /// </summary>
        /// <param name="record">已 Bound 的连接记录</param>
        /// <param name="dataControl">客户端的数据连接</param>
        /// <returns>启动两个方向泵数据的动作，执行一次即可</returns>
        /// <exception cref="ArgumentNullException">record、dataControl 或 dataControl.DataStream 为 null</exception>
        /// <exception cref="ArgumentException">记录没有对端 socket</exception>
        /// <exception cref="ObjectDisposedException">本中继端已释放</exception>
        internal Action PrepareBind(TurnConnectionRecord record, ITurnControlChannel dataControl)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }
            if (dataControl == null)
            {
                throw new ArgumentNullException(nameof(dataControl));
            }

            var stream = dataControl.DataStream;
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(dataControl), "数据连接没有可用的字节流");
            }

            var peerSocket = record.PeerSocket;
            if (peerSocket == null)
            {
                throw new ArgumentException("连接记录没有对端 socket", nameof(record));
            }

            lock (_sync)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }

                // 登记已绑定的流，释放时才能打断阻塞在读取上的泵
                _boundStreams.Add(stream);
            }

            // 泵一旦启动就会往数据连接写对端字节，必须由调用方先完成应答写出再触发
            return () =>
            {
                var ignoredUp = Task.Run(() => PumpPeerToStreamAsync(record, stream));
                var ignoredDown = Task.Run(() => PumpStreamToPeerAsync(record, stream));
            };
        }

        /// <summary>
        /// 通知服务端有新的中继连接到达，由服务端向客户端发 ConnectionAttempt。
        /// 这里在 accept 循环线程上执行，回调异常必须就地吞掉，否则会中断后续接受连接
        /// </summary>
        /// <param name="record">新到的连接</param>
        internal void NotifyConnectionAttempt(TurnConnectionRecord record)
        {
            var notifier = ConnectionAttemptNotifier;
            if (notifier == null)
            {
                return;
            }

            try
            {
                notifier(record);
            }
            catch (Exception ex)
            {
                P2PLogHelper.Debug("TurnTcpRelay", "通知 ConnectionAttempt 失败：" + ex.GetType().Name);
            }
        }

        /// <summary>
        /// 停止 accept、关闭全部对端 socket 与已绑定的流；可重复调用。中继端口由服务端归还，这里不碰
        /// </summary>
        public void Dispose()
        {
            Timer timer;
            List<Stream> streams;
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                timer = _purgeTimer;
                _purgeTimer = null;
                streams = new List<Stream>(_boundStreams);
                _boundStreams.Clear();
            }

            if (timer != null)
            {
                timer.Dispose();
            }

            // 关闭监听才能打断阻塞的 accept；循环在异常分支看到已释放后静默退出
            DisposeSocket(_listener);

            var records = _connections.RemoveByAllocation(_allocation);
            for (var i = 0; i < records.Count; i++)
            {
                DisposeSocket(records[i].PeerSocket);
            }

            // 关流才能让阻塞在 ReadAsync 上的方向立刻结束，否则它要等到对端发数据才醒
            for (var i = 0; i < streams.Count; i++)
            {
                DisposeStream(streams[i]);
            }
        }

        /// <summary>
        /// accept 循环：每接受一条连接就登记到连接表；监听关闭后退出
        /// </summary>
        /// <returns>循环结束的任务</returns>
        private async Task AcceptLoopAsync()
        {
            while (true)
            {
                Socket accepted;
                try
                {
                    accepted = await _listener.AcceptAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (InvalidOperationException)
                {
                    return;
                }
                catch (SocketException ex)
                {
                    if (!IsDisposed())
                    {
                        P2PLogHelper.Debug("TurnTcpRelay", "中继监听接受连接失败：" + ex.SocketErrorCode);
                    }
                    return;
                }

                if (IsDisposed())
                {
                    DisposeSocket(accepted);
                    return;
                }

                var record = CreateRecord(accepted);
                if (record == null)
                {
                    DisposeSocket(accepted);
                    continue;
                }

                try
                {
                    _connections.Add(record);
                }
                catch (InvalidOperationException ex)
                {
                    // 连接号耗尽时无法再登记，直接关掉这条连接，避免它成为没人管的句柄
                    P2PLogHelper.Debug("TurnTcpRelay", "登记中继连接失败：" + ex.Message);
                    DisposeSocket(accepted);
                    return;
                }

                NotifyConnectionAttempt(record);
            }
        }

        /// <summary>
        /// 为刚接受的对端连接构造登记项
        /// </summary>
        /// <param name="accepted">对端 socket</param>
        /// <returns>登记项；取不到对端地址时为 null</returns>
        private TurnConnectionRecord CreateRecord(Socket accepted)
        {
            IPEndPoint peer;
            try
            {
                peer = accepted.RemoteEndPoint as IPEndPoint;
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
            catch (SocketException)
            {
                return null;
            }

            if (peer == null)
            {
                return null;
            }

            // 数据连接必须与申请分配的控制连接保持同一加密层，所以在这里固化下来
            var control = _allocation.ControlChannel;
            var record = new TurnConnectionRecord();
            record.Allocation = _allocation;
            record.PeerEndPoint = peer;
            record.PeerSocket = accepted;
            record.IsTls = control != null && control.IsTls;
            record.CreatedAt = DateTimeOffset.UtcNow;
            record.Bound = false;
            return record;
        }

        /// <summary>
        /// 清理本分配下超时未绑定的连接；定时器回调，异常不得外抛
        /// </summary>
        /// <param name="state">未使用</param>
        private void PurgeExpiredConnections(object state)
        {
            if (IsDisposed())
            {
                return;
            }

            List<TurnConnectionRecord> expired;
            try
            {
                expired = _connections.RemoveExpired(_allocation, DateTimeOffset.UtcNow, _bindTimeout);
            }
            catch (Exception ex)
            {
                P2PLogHelper.Debug("TurnTcpRelay", "清理未绑定中继连接失败：" + ex.Message);
                return;
            }

            for (var i = 0; i < expired.Count; i++)
            {
                DisposeSocket(expired[i].PeerSocket);
            }
        }

        /// <summary>
        /// 对端到客户端方向：读对端 socket 写入数据连接
        /// </summary>
        /// <param name="record">连接记录</param>
        /// <param name="stream">数据连接的字节流</param>
        /// <returns>泵结束的任务</returns>
        private async Task PumpPeerToStreamAsync(TurnConnectionRecord record, Stream stream)
        {
            var buffer = new byte[RelayBufferSize];
            var peerSocket = record.PeerSocket;
            try
            {
                while (true)
                {
                    var read = await peerSocket.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None).ConfigureAwait(false);
                    if (read <= 0)
                    {
                        break;
                    }

                    await stream.WriteAsync(buffer, 0, read, CancellationToken.None).ConfigureAwait(false);

                    // TLS 流会把数据留在自己的缓冲里，不刷这一块就发不出去
                    await stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
                    _allocation.AddBytes(read);
                }
            }
            catch (Exception ex)
            {
                if (!IsDisposed())
                {
                    P2PLogHelper.Debug("TurnTcpRelay", "中继到客户端方向结束：" + ex.GetType().Name);
                }
            }
            finally
            {
                CloseRecord(record, stream);
            }
        }

        /// <summary>
        /// 客户端到对端方向：读数据连接写入对端 socket
        /// </summary>
        /// <param name="record">连接记录</param>
        /// <param name="stream">数据连接的字节流</param>
        /// <returns>泵结束的任务</returns>
        private async Task PumpStreamToPeerAsync(TurnConnectionRecord record, Stream stream)
        {
            var buffer = new byte[RelayBufferSize];
            var peerSocket = record.PeerSocket;
            try
            {
                while (true)
                {
                    var read = await stream.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None).ConfigureAwait(false);
                    if (read <= 0)
                    {
                        break;
                    }

                    var offset = 0;
                    while (offset < read)
                    {
                        var sent = await peerSocket.SendAsync(new ArraySegment<byte>(buffer, offset, read - offset), SocketFlags.None).ConfigureAwait(false);
                        if (sent <= 0)
                        {
                            // 对端已不再接收，按连接结束处理，交给 finally 收尾
                            return;
                        }

                        offset += sent;
                        _allocation.AddBytes(sent);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!IsDisposed())
                {
                    P2PLogHelper.Debug("TurnTcpRelay", "客户端到中继方向结束：" + ex.GetType().Name);
                }
            }
            finally
            {
                CloseRecord(record, stream);
            }
        }

        /// <summary>
        /// 结束一条中继连接：先摘出连接表，再关闭数据流与对端 socket；两个方向都会走到这里，重复关闭安全
        /// </summary>
        /// <param name="record">连接记录</param>
        /// <param name="stream">数据连接的字节流</param>
        private void CloseRecord(TurnConnectionRecord record, Stream stream)
        {
            TurnConnectionRecord removed;
            _connections.TryRemove(record.ConnectionId, out removed);

            lock (_sync)
            {
                _boundStreams.Remove(stream);
            }

            // 先关流再关 socket：两侧任一处阻塞都能被立刻打断，不必等对端发数据
            DisposeStream(stream);
            DisposeSocket(record.PeerSocket);
        }

        /// <summary>
        /// 判断是否已释放
        /// </summary>
        /// <returns>已释放返回 true</returns>
        private bool IsDisposed()
        {
            lock (_sync)
            {
                return _disposed;
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
            catch (SocketException ex)
            {
                P2PLogHelper.Debug("TurnTcpRelay", "关闭中继 socket 失败：" + ex.SocketErrorCode);
            }
        }

        /// <summary>
        /// 关闭字节流并忽略重复关闭
        /// </summary>
        /// <param name="stream">字节流；可为 null</param>
        private static void DisposeStream(Stream stream)
        {
            if (stream == null)
            {
                return;
            }

            try
            {
                stream.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                P2PLogHelper.Debug("TurnTcpRelay", "关闭数据连接失败：" + ex.GetType().Name);
            }
        }
    }
}