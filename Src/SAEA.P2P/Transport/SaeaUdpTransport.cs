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
*文件名： SaeaUdpTransport
*版本号： v26.11.5.1
*唯一标识：f5f06a4a-c004-45d1-9c24-6456be6aaa06
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 12:15:00
*描述：基于 SAEA.Sockets 的 UDP 传输实现
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 12:15:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 UDP 传输实现
*
*****************************************************************************/
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Sockets;
using SAEA.Sockets.Core.Udp;
using SAEA.Sockets.Model;

namespace SAEA.P2P.Transport
{
    /// <summary>
    /// 基于 SAEA.Sockets UDP 服务端套接字的传输实现：不调用 connect，按数据报与任意端点收发。
    /// 借用套接字的带来源地址回调，回调内立即复制数据，交给上层的内存不受底层缓冲复用影响。
    /// </summary>
    public sealed class SaeaUdpTransport : IUdpTransport
    {
        /// <summary>
        /// 保护绑定的同步对象
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 底层 UDP 套接字，绑定成功前为 null
        /// </summary>
        private SaeaUdpServerSocket _server;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 收到数据报时触发，参数为来源端点与已复制的数据
        /// </summary>
        public event Action<IPEndPoint, ReadOnlyMemory<byte>> DatagramReceived;

        /// <summary>
        /// 本地绑定的端点；未绑定或已释放时为 null
        /// </summary>
        public IPEndPoint LocalEndPoint
        {
            get
            {
                var server = _server;
                return server == null || _disposed ? null : server.LocalEndPoint;
            }
        }

        /// <summary>
        /// 是否已绑定并可收发
        /// </summary>
        public bool IsConnected => _server != null && !_disposed;

        /// <summary>
        /// 绑定本地端点并开始接收；端口为 0 时由系统分配，实际端点从 <see cref="LocalEndPoint"/> 读取
        /// </summary>
        /// <param name="localEndPoint">本地端点</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>绑定完成的任务</returns>
        /// <exception cref="ArgumentNullException">localEndPoint 为 null</exception>
        /// <exception cref="InvalidOperationException">已绑定或已释放</exception>
        public Task BindAsync(IPEndPoint localEndPoint, CancellationToken cancellationToken)
        {
            if (localEndPoint == null) throw new ArgumentNullException(nameof(localEndPoint));
            cancellationToken.ThrowIfCancellationRequested();

            lock (_sync)
            {
                if (_disposed) throw new ObjectDisposedException(GetType().Name);
                if (_server != null) throw new InvalidOperationException("UDP 传输已经绑定，不能重复绑定。");

                var builder = SocketOptionBuilder.Instance
                    .SetSocket(SAEASocketType.Udp)
                    .UsePacket()
                    .SetIPEndPoint(localEndPoint)
                    .SetActionTimeOut(30 * 1000)
                    .SetFreeTime(180 * 1000);

                if (localEndPoint.AddressFamily == AddressFamily.InterNetworkV6)
                    builder.UseIPv6();

                var server = new SaeaUdpServerSocket(builder.Build());
                server.OnServerReceiveSpanFrom += OnServerReceiveSpanFrom;

                try
                {
                    server.Start();
                }
                catch
                {
                    server.OnServerReceiveSpanFrom -= OnServerReceiveSpanFrom;
                    try { server.Dispose(); } catch { }
                    throw;
                }

                _server = server;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 启动接收循环，幂等；SAEA 的 UDP 套接字在绑定（Start）时已开始接收，这里为空操作
        /// </summary>
        /// <exception cref="InvalidOperationException">尚未绑定或已释放</exception>
        public void StartReceive()
        {
            var server = _server;
            if (_disposed || server == null)
                throw new InvalidOperationException("UDP 传输尚未绑定或已释放。");
        }

        /// <summary>
        /// 向任意远端端点发送一个数据报
        /// </summary>
        /// <param name="remoteEndPoint">目标端点，端口按原样使用</param>
        /// <param name="data">待发送数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>发送完成的任务</returns>
        /// <exception cref="ArgumentNullException">remoteEndPoint 为 null</exception>
        /// <exception cref="InvalidOperationException">尚未绑定或已释放</exception>
        public Task SendToAsync(IPEndPoint remoteEndPoint, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            if (remoteEndPoint == null) throw new ArgumentNullException(nameof(remoteEndPoint));
            cancellationToken.ThrowIfCancellationRequested();

            var server = _server;
            if (_disposed || server == null)
                throw new InvalidOperationException("UDP 传输尚未绑定或已释放。");

            return server.SendToAsync(remoteEndPoint, data);
        }

        /// <summary>
        /// 底层带来源地址的接收回调：复制端点与数据后交给上层，并把该来源会话标记为不参与空闲清理
        /// </summary>
        /// <param name="remoteEndPoint">数据报来源端点</param>
        /// <param name="data">数据报内容，仅在回调期间有效</param>
        private void OnServerReceiveSpanFrom(IPEndPoint remoteEndPoint, ReadOnlySpan<byte> data)
        {
            var payload = new byte[data.Length];
            data.CopyTo(payload);

            var from = new IPEndPoint(remoteEndPoint.Address, remoteEndPoint.Port);

            // ICE 候选检查期间会长时间静默等待，来源会话不能按空闲超时被清掉
            try { _server?.SessionManager?.SetNoTimeout(from.ToString(), true); } catch { }

            var handler = DatagramReceived;
            if (handler != null) handler(from, payload);
        }

        /// <summary>
        /// 解绑回调并释放底层套接字
        /// </summary>
        public void Dispose()
        {
            SaeaUdpServerSocket server;
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
                server = _server;
                _server = null;
            }

            if (server != null)
            {
                try { server.OnServerReceiveSpanFrom -= OnServerReceiveSpanFrom; } catch { }
                try { server.Dispose(); } catch { }
            }
        }
    }
}