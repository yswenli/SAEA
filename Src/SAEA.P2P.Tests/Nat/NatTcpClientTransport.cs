/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               
 *Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.P2P.Tests.Nat
*文件名： NatTcpClientTransport
*版本号： net10.0
*唯一标识：a8daa3d6-3901-49e6-9968-9f0d0e0dc14e
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 16:44:01
*描述：把出向 TCP 连接改走用户态 NAT 网关的连接侧传输
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 16:44:01
*修改人： yswenli
*版本号： net10.0
*描述：新增 NAT 内部主机的 TCP 连接侧传输
*
 *****************************************************************************/
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Transport;

namespace SAEA.P2P.Tests.Nat
{
    /// <summary>
    /// 把出向连接改走用户态 NAT 网关的连接侧传输：先连到 <see cref="NatBox.InternalTcpGateway"/>，
    /// 首个 6 字节头告知 NAT 真实目标端点，之后由 NAT 以端口保持方式拨出并双向转发。
    /// 本地只绑内网地址、端口一律取 0，真正的端口保持由 NAT 侧完成。
    /// </summary>
    public sealed class NatTcpClientTransport : ITcpTransport
    {
        /// <summary>
        /// NAT 内部 TCP 网关端点
        /// </summary>
        private readonly IPEndPoint _gatewayEndPoint;

        /// <summary>
        /// 已连接的套接字；未连接或已释放时为 null
        /// </summary>
        private Socket _socket;

        /// <summary>
        /// 已建立连接的字节流；未连接或已释放时为 null
        /// </summary>
        private NetworkStream _stream;

        /// <summary>
        /// 内部侧本地端点；未连接或已释放时为 null
        /// </summary>
        private IPEndPoint _localEndPoint;

        /// <summary>
        /// 通过地址头告知 NAT 的真实目标端点
        /// </summary>
        private IPEndPoint _remoteEndPoint;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 创建连接侧传输，随后用 <see cref="ConnectAsync(IPEndPoint, CancellationToken)"/> 拨出
        /// </summary>
        /// <param name="gatewayEndPoint">NAT 内部 TCP 网关端点</param>
        /// <exception cref="ArgumentNullException">gatewayEndPoint 为 null</exception>
        public NatTcpClientTransport(IPEndPoint gatewayEndPoint)
        {
            if (gatewayEndPoint == null) throw new ArgumentNullException(nameof(gatewayEndPoint));

            _gatewayEndPoint = gatewayEndPoint;
        }

        /// <summary>
        /// 连接侧永不触发接受事件；用自定义访问器实现成空事件，避免编译器对未使用事件的警告
        /// </summary>
        public event Action<ITcpTransport> Accepted
        {
            add { }
            remove { }
        }

        /// <summary>
        /// 内部侧本地端点；未连接或已释放时为 null
        /// </summary>
        public IPEndPoint LocalEndPoint
        {
            get { return _disposed ? null : _localEndPoint; }
        }

        /// <summary>
        /// 是否已建立连接且未释放
        /// </summary>
        public bool IsConnected
        {
            get { return _stream != null && !_disposed; }
        }

        /// <summary>
        /// 字节流；未连接或已释放时为 null
        /// </summary>
        public Stream Stream
        {
            get { return _stream; }
        }

        /// <summary>
        /// 通过地址头告知 NAT 的真实目标端点
        /// </summary>
        public IPEndPoint RemoteEndPoint
        {
            get { return _remoteEndPoint; }
        }

        /// <summary>
        /// 连接到远端端点；本地地址由路由决定，不做绑定
        /// </summary>
        /// <param name="remoteEndPoint">真实目标端点，写入地址头后交给 NAT 拨出</param>
        /// <param name="cancellationToken">取消令牌，只在发起连接前检查一次</param>
        /// <returns>连接建立后完成的任务</returns>
        /// <exception cref="ArgumentNullException">remoteEndPoint 为 null</exception>
        /// <exception cref="InvalidOperationException">该实例已连接</exception>
        /// <exception cref="ObjectDisposedException">该实例已释放</exception>
        /// <exception cref="SocketException">网关不可达或被 NAT 拒绝</exception>
        public Task ConnectAsync(IPEndPoint remoteEndPoint, CancellationToken cancellationToken)
        {
            return ConnectAsync(remoteEndPoint, null, cancellationToken);
        }

        /// <summary>
        /// 从指定本地地址发起连接：先连 NAT 网关，再写出 6 字节地址头告知真实目标端点
        /// </summary>
        /// <param name="remoteEndPoint">真实目标端点，写入地址头后交给 NAT 拨出</param>
        /// <param name="localEndPoint">本地绑定端点，只取其地址，端口一律用 0</param>
        /// <param name="cancellationToken">取消令牌，只在发起连接前检查一次</param>
        /// <returns>连接建立后完成的任务</returns>
        /// <exception cref="ArgumentNullException">remoteEndPoint 为 null</exception>
        /// <exception cref="InvalidOperationException">该实例已连接</exception>
        /// <exception cref="ObjectDisposedException">该实例已释放</exception>
        /// <exception cref="SocketException">网关不可达或被 NAT 拒绝</exception>
        public async Task ConnectAsync(IPEndPoint remoteEndPoint, IPEndPoint localEndPoint, CancellationToken cancellationToken)
        {
            if (remoteEndPoint == null) throw new ArgumentNullException(nameof(remoteEndPoint));

            ThrowIfDisposed();

            if (_socket != null) throw new InvalidOperationException("该传输实例已经连接，不能重复连接。");

            cancellationToken.ThrowIfCancellationRequested();

            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                if (localEndPoint != null && localEndPoint.Address != null)
                {
                    // 端口一律用 0：真正的端口保持由 NAT 侧完成，这里只绑内网地址
                    socket.Bind(new IPEndPoint(localEndPoint.Address, 0));
                }

                await socket.ConnectAsync(_gatewayEndPoint).ConfigureAwait(false);

                // 地址头必须在业务字节之前到达，NAT 读满 6 字节后才会拨出
                var header = BuildHeader(remoteEndPoint);
                await socket.SendAsync(new ArraySegment<byte>(header), SocketFlags.None).ConfigureAwait(false);

                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }

                _socket = socket;
                _stream = new NetworkStream(socket, true);
                _localEndPoint = (IPEndPoint)socket.LocalEndPoint;
                _remoteEndPoint = remoteEndPoint;
            }
            catch
            {
                try { socket.Dispose(); } catch { }
                throw;
            }
        }

        /// <summary>
        /// 连接侧不支持接受连接
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>不会返回</returns>
        /// <exception cref="NotSupportedException">总是抛出</exception>
        public Task<ITcpTransport> AcceptAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException("连接侧传输不支持接受连接");
        }

        /// <summary>
        /// 释放传输：先关字节流（NetworkStream 拥有套接字），再兜底释放套接字，可重复调用
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try { _stream?.Dispose(); } catch { }
            try { _socket?.Dispose(); } catch { }

            _stream = null;
            _socket = null;
            _localEndPoint = null;
            _remoteEndPoint = null;
        }

        /// <summary>
        /// 生成告知 NAT 的 6 字节地址头；Span 只在本同步方法里出现，避免 async 方法触发 CS8936
        /// </summary>
        /// <param name="remoteEndPoint">真实目标端点</param>
        /// <returns>可直接写入套接字的地址头</returns>
        private static byte[] BuildHeader(IPEndPoint remoteEndPoint)
        {
            return NatPacket.Encode(remoteEndPoint, ReadOnlySpan<byte>.Empty);
        }

        /// <summary>
        /// 已释放时抛出异常
        /// </summary>
        /// <exception cref="ObjectDisposedException">该实例已释放</exception>
        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
        }
    }
}