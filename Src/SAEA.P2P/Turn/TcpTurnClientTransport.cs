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
*命名空间：SAEA.P2P.Turn
*文件名： TcpTurnClientTransport
*版本号： v26.11.5.1
*唯一标识：4d742679-5e31-41dc-93d6-8df92d0add71
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 04:10:00
*描述：TURN over TCP 客户端控制传输
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 04:10:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TURN over TCP 客户端控制传输
*
*****************************************************************************/
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Common;
using SAEA.P2P.Transport;

namespace SAEA.P2P.Turn
{
    /// <summary>
    /// TURN over TCP 客户端控制传输：在一条长连接上收发，读取侧由共用读泵按 TURN 自身长度字段分帧。
    /// 写入串行化在锁内完成，写失败只标记断开，由 TURN 客户端按超时失败并重连。
    /// </summary>
    public sealed class TcpTurnClientTransport : ITurnClientTransport
    {
        /// <summary>
        /// TURN 服务端端点
        /// </summary>
        private readonly IPEndPoint _serverEndPoint;

        /// <summary>
        /// 串行化写入的同步对象
        /// </summary>
        private readonly object _sendSync = new object();

        /// <summary>
        /// 保护连接与读泵生命周期的同步对象
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 底层 TCP 传输；打开成功前为 null
        /// </summary>
        private SaeaTcpTransport _tcp;

        /// <summary>
        /// 流式读泵；打开成功前为 null
        /// </summary>
        private TurnStreamPump _pump;

        /// <summary>
        /// 是否已完成打开流程
        /// </summary>
        private bool _opened;

        /// <summary>
        /// 是否因写失败而不可再用
        /// </summary>
        private volatile bool _broken;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 收到一整帧时触发，参数是副本，可在回调返回后继续持有
        /// </summary>
        public event Action<byte[]> FrameReceived;

        /// <summary>
        /// 创建客户端传输；构造阶段不发起连接，由 <see cref="OpenAsync"/> 建链
        /// </summary>
        /// <param name="serverEndPoint">TURN 服务端端点</param>
        /// <exception cref="ArgumentNullException">端点为 null</exception>
        public TcpTurnClientTransport(IPEndPoint serverEndPoint)
        {
            if (serverEndPoint == null)
            {
                throw new ArgumentNullException(nameof(serverEndPoint));
            }

            _serverEndPoint = serverEndPoint;
        }

        /// <summary>
        /// 本地绑定的端点；尚未打开或已释放时为 null
        /// </summary>
        public IPEndPoint LocalEndPoint
        {
            get
            {
                var tcp = _tcp;
                return tcp == null || _disposed ? null : tcp.LocalEndPoint;
            }
        }

        /// <summary>
        /// TURN 服务端端点
        /// </summary>
        public IPEndPoint ServerEndPoint
        {
            get { return _serverEndPoint; }
        }

        /// <summary>
        /// 是否为可靠传输；TCP 恒为 true
        /// </summary>
        public bool IsReliable
        {
            get { return true; }
        }

        /// <summary>
        /// 是否已连接且读泵仍在运行
        /// </summary>
        public bool IsConnected
        {
            get
            {
                var tcp = _tcp;
                var pump = _pump;
                return !_disposed && !_broken && tcp != null && tcp.IsConnected && pump != null && pump.IsRunning;
            }
        }

        /// <summary>
        /// 连接到服务端并启动读泵；已打开时重复调用直接返回
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>建链与读泵启动完成的任务</returns>
        /// <exception cref="ObjectDisposedException">已释放</exception>
        public async Task OpenAsync(CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }
                if (_opened)
                {
                    return;
                }

                // 必须在 await 之前占位，否则并发调用会各自建链
                _opened = true;
            }

            var tcp = new SaeaTcpTransport();
            TurnStreamPump pump = null;

            try
            {
                await tcp.ConnectAsync(_serverEndPoint, cancellationToken).ConfigureAwait(false);

                var stream = tcp.Stream;
                if (stream == null)
                {
                    throw new InvalidOperationException("TCP 传输已连接但没有可用的字节流");
                }

                pump = new TurnStreamPump(
                    (buffer, count, token) => stream.ReadAsync(buffer, 0, count, token),
                    RaiseFrameReceived);

                lock (_sync)
                {
                    if (_disposed)
                    {
                        throw new ObjectDisposedException(GetType().Name);
                    }

                    _tcp = tcp;
                    _pump = pump;
                }

                pump.Start();
            }
            catch
            {
                // 失败路径必须释放尚未接管的连接，并复位打开标记让调用方可以重试
                lock (_sync)
                {
                    _opened = false;
                    _tcp = null;
                    _pump = null;
                }

                if (pump != null)
                {
                    try { pump.Dispose(); } catch { }
                }

                try { tcp.Dispose(); } catch { }
                throw;
            }
        }

        /// <summary>
        /// 发送一整帧；串行写入并即时刷新，失败只记调试日志并标记断开
        /// </summary>
        /// <param name="frame">完整帧字节，不得为 null</param>
        /// <exception cref="ArgumentNullException">frame 为 null</exception>
        public void Send(byte[] frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            var tcp = _tcp;
            if (_disposed || _broken || tcp == null)
            {
                return;
            }

            var stream = tcp.Stream;
            if (stream == null)
            {
                return;
            }

            try
            {
                lock (_sendSync)
                {
                    stream.Write(frame, 0, frame.Length);
                    stream.Flush();
                }
            }
            catch (Exception ex)
            {
                // 写失败说明连接已不可用；不向调用方抛，由 TURN 客户端按超时失败并重连
                MarkBroken("写失败：" + ex.GetType().Name);
            }
        }

        /// <summary>
        /// 先停读泵再释放底层连接；可重复调用，不抛异常
        /// </summary>
        public void Dispose()
        {
            TurnStreamPump pump;
            SaeaTcpTransport tcp;

            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                pump = _pump;
                tcp = _tcp;
                _pump = null;
                _tcp = null;
            }

            // 先取消读泵，让读取尽快退出，再由底层连接打断可能卡住的读
            if (pump != null)
            {
                try { pump.Dispose(); } catch { }
            }

            if (tcp != null)
            {
                try { tcp.Dispose(); } catch { }
            }
        }

        /// <summary>
        /// 逐订阅者触发帧事件；单个订阅者抛异常不影响其余订阅者与后续接收
        /// </summary>
        /// <param name="frame">完整帧字节</param>
        private void RaiseFrameReceived(byte[] frame)
        {
            var handler = FrameReceived;
            if (handler == null)
            {
                return;
            }

            var subscribers = handler.GetInvocationList();
            for (var i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    ((Action<byte[]>)subscribers[i])(frame);
                }
                catch (Exception ex)
                {
                    // 订阅者异常不能打断读泵
                    P2PLogHelper.Debug("TcpTurnClientTransport", "帧回调异常：" + ex.GetType().Name);
                }
            }
        }

        /// <summary>
        /// 写失败时标记连接不可再用并记录原因
        /// </summary>
        /// <param name="reason">失败原因，仅用于调试日志</param>
        private void MarkBroken(string reason)
        {
            _broken = true;
            P2PLogHelper.Debug("TcpTurnClientTransport", reason);
        }
    }
}
