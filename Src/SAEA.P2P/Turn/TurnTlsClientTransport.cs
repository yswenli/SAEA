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
*文件名： TurnTlsClientTransport
*版本号： v26.11.5.1
*唯一标识：6e1f09cc-3b7a-4787-9c61-52a416d9fa51
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 02:30:00
*描述：TURN over TLS 客户端传输
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 04:10:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：实现 TURN 客户端传输契约，补 ServerEndPoint、OpenAsync、Send、FrameReceived 与共用的流式读泵
*
*****************************************************************************/
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Common;
using SAEA.Sockets;
using SAEA.Sockets.Core.Tcp;

namespace SAEA.P2P.Turn
{
    /// <summary>
    /// TURN over TLS 客户端传输：用 SAEA.Sockets 的 BouncyCastle TLS 建立并维护一条长连接控制通道，
    /// 交给调用方一个双工流，由 TURN 客户端在其上按自身长度字段分帧。
    /// 证书校验默认开启，只有测试等场景才显式允许无效证书。
    /// </summary>
    public sealed class TurnTlsClientTransport : ITurnClientTransport
    {
        /// <summary>
        /// 控制连接空闲超时，单位毫秒。
        /// SAEA.Sockets 默认 180s 会早于 TURN 的 permission 刷新间隔 240s，
        /// 导致刷新报文还没发出控制连接就被回收，因此调到 24 小时。
        /// </summary>
        internal const int ControlIdleTimeoutMilliseconds = 24 * 60 * 60 * 1000;

        /// <summary>
        /// 单次读取的最大字节数，与 TURN 流式分帧的常用报文大小匹配
        /// </summary>
        private const int ReceiveBufferSize = 8192;

        /// <summary>
        /// 底层 TLS 客户端
        /// </summary>
        private readonly SaeaClientSocket _client;

        /// <summary>
        /// TURN 服务端端点
        /// </summary>
        private readonly IPEndPoint _serverEndPoint;

        /// <summary>
        /// 串行化写入的同步对象
        /// </summary>
        private readonly object _sendSync = new object();

        /// <summary>
        /// 保护打开与读泵生命周期的同步对象
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 流式读泵；未通过 <see cref="OpenAsync"/> 打开时为 null
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
        /// 创建客户端传输；构造阶段不发起连接
        /// </summary>
        /// <param name="serverEndPoint">TURN 服务端 TLS 端点</param>
        /// <param name="targetHost">证书校验与 SNI 使用的主机名</param>
        /// <param name="allowInvalidCertificates">是否允许无效证书，默认 false；仅测试显式传 true</param>
        /// <exception cref="ArgumentNullException">端点为 null</exception>
        public TurnTlsClientTransport(IPEndPoint serverEndPoint, string targetHost = "localhost",
            bool allowInvalidCertificates = false)
        {
            if (serverEndPoint == null)
            {
                throw new ArgumentNullException(nameof(serverEndPoint));
            }

            _serverEndPoint = serverEndPoint;

            var option = SocketOptionBuilder.Instance
                .UseStream()
                .SetIPEndPoint(serverEndPoint)
                .SetReadBufferSize(ReceiveBufferSize)
                .SetFreeTime(ControlIdleTimeoutMilliseconds)
                .WithSsl(o =>
                {
                    o.TargetHost = targetHost;
                    o.AllowInvalidCertificates = allowInvalidCertificates;
                })
                .Build();

            _client = new SaeaClientSocket(option);
        }

        /// <summary>
        /// 本地绑定的端点；尚未连接或已释放时为 null
        /// </summary>
        public IPEndPoint LocalEndPoint
        {
            get
            {
                if (_disposed)
                {
                    return null;
                }

                var socket = _client.Socket;
                return socket == null ? null : socket.LocalEndPoint as IPEndPoint;
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
        /// 是否为可靠传输；TLS 恒为 true
        /// </summary>
        public bool IsReliable
        {
            get { return true; }
        }

        /// <summary>
        /// 是否已完成握手且读泵仍在运行；未通过 <see cref="OpenAsync"/> 打开时为 false
        /// </summary>
        public bool IsConnected
        {
            get
            {
                var pump = _pump;
                if (_disposed || _broken || !_client.IsAuthenticated)
                {
                    return false;
                }

                return pump != null && pump.IsRunning;
            }
        }

        /// <summary>
        /// 连接并完成 TLS 握手；握手失败时抛出异常且不会进入已认证状态
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>连接与握手均成功的任务</returns>
        /// <exception cref="ObjectDisposedException">已释放</exception>
        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            return _client.ConnectAsync(cancellationToken);
        }

        /// <summary>
        /// 握手成功后的双工字节流；未连接或已释放时抛 <see cref="InvalidOperationException"/>
        /// </summary>
        public Stream Stream
        {
            get { return _client.GetStream(); }
        }

        /// <summary>
        /// 当前连接是否已完成 TLS 握手
        /// </summary>
        public bool IsAuthenticated
        {
            get { return !_disposed && _client.IsAuthenticated; }
        }

        /// <summary>
        /// 连接并完成握手后启动读泵；已打开时重复调用直接返回
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>握手与读泵启动完成的任务</returns>
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

                // 必须在 await 之前占位，否则并发调用会各自握手
                _opened = true;
            }

            TurnStreamPump pump = null;

            try
            {
                await ConnectAsync(cancellationToken).ConfigureAwait(false);

                var stream = _client.GetStream();
                pump = new TurnStreamPump(
                    (buffer, count, token) => stream.ReadAsync(buffer, 0, count, token),
                    RaiseFrameReceived);

                lock (_sync)
                {
                    if (_disposed)
                    {
                        throw new ObjectDisposedException(GetType().Name);
                    }

                    _pump = pump;
                }

                pump.Start();
            }
            catch
            {
                // 失败路径复位打开标记，让调用方可以重试；读泵尚未接管时一并释放
                lock (_sync)
                {
                    _opened = false;
                    _pump = null;
                }

                if (pump != null)
                {
                    try { pump.Dispose(); } catch { }
                }

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
            if (_disposed || _broken)
            {
                return;
            }

            Stream stream;
            try
            {
                stream = _client.GetStream();
            }
            catch (Exception ex)
            {
                // 尚未握手完成时取不到流，按不可用处理
                P2PLogHelper.Debug("TurnTlsClientTransport", "取字节流失败：" + ex.GetType().Name);
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
                _broken = true;
                P2PLogHelper.Debug("TurnTlsClientTransport", "写失败：" + ex.GetType().Name);
            }
        }

        /// <summary>
        /// 先停读泵再断开连接并释放底层资源；可重复调用
        /// </summary>
        public void Dispose()
        {
            TurnStreamPump pump;

            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                pump = _pump;
                _pump = null;
            }

            // 先取消读泵，让读取尽快退出，再由底层连接打断可能卡住的读
            if (pump != null)
            {
                try { pump.Dispose(); } catch { }
            }

            try
            {
                _client.Dispose();
            }
            catch (Exception ex)
            {
                P2PLogHelper.Debug("TurnTlsClientTransport", "释放底层连接失败：" + ex.GetType().Name);
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
                    P2PLogHelper.Debug("TurnTlsClientTransport", "帧回调异常：" + ex.GetType().Name);
                }
            }
        }
    }
}
