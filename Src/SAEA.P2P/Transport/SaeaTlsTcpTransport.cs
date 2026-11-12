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
*文件名： SaeaTlsTcpTransport
*版本号： v26.11.5.1
*唯一标识：84468a19-d783-4581-818e-0254a606f700
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 14:13:00
*描述：基于 SAEA.Sockets 的 TLS 版 TCP 传输实现
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 14:13:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：实现 TLS 数据连接传输：主动连接并等待握手完成，供加密层一致的 TCP 中继数据连接使用
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
using SAEA.Sockets.Model;

namespace SAEA.P2P.Transport
{
    /// <summary>
    /// 基于 SAEA.Sockets 的 TLS 版 TCP 传输实现。
    /// 只用于主动连接：连接时等待 TLS 握手完成后再交付字节流，分帧由上层约定；
    /// 证书校验默认开启，只有测试等场景才显式允许无效证书。
    /// </summary>
    public sealed class SaeaTlsTcpTransport : ITcpTransport
    {
        /// <summary>
        /// 单次读取的最大字节数，与上层常用报文大小匹配
        /// </summary>
        private const int ReceiveBufferSize = 8192;

        /// <summary>
        /// 空闲超时，单位毫秒。
        /// SAEA.Sockets 默认 180s 会在 TURN 的刷新与保活节奏之前回收数据连接，
        /// 长时间静默的中继连接因此调到 24 小时，避免被误判为空闲连接杀掉。
        /// </summary>
        private const int IdleTimeoutMilliseconds = 24 * 60 * 60 * 1000;

        /// <summary>
        /// 证书校验与 SNI 使用的主机名
        /// </summary>
        private readonly string _targetHost;

        /// <summary>
        /// 是否允许无效证书，仅测试等场景为 true
        /// </summary>
        private readonly bool _allowInvalidCertificates;

        /// <summary>
        /// 保护连接状态与释放标记的同步对象
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 底层 TLS 客户端；未连接或已释放时为 null
        /// </summary>
        private SaeaClientSocket _client;

        // 前置检查与真正的握手之间隔着 await，必须用该标记占位，否则并发调用会各自建链
        /// <summary>
        /// 是否有连接正在进行中
        /// </summary>
        private bool _connectorBusy;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 连接侧传输不会接受连接，订阅不会收到任何回调
        /// </summary>
        public event Action<ITcpTransport> Accepted
        {
            add { }
            remove { }
        }

        /// <summary>
        /// 创建 TLS 传输实例；构造阶段不发起连接，只用于主动连接，不接受连接
        /// </summary>
        /// <param name="targetHost">证书校验与 SNI 使用的主机名</param>
        /// <param name="allowInvalidCertificates">是否允许无效证书，默认 false，仅供测试显式传 true</param>
        /// <exception cref="ArgumentException">targetHost 为 null、空串或空白</exception>
        public SaeaTlsTcpTransport(string targetHost = "localhost", bool allowInvalidCertificates = false)
        {
            if (string.IsNullOrWhiteSpace(targetHost))
            {
                throw new ArgumentException("targetHost 不能为空", nameof(targetHost));
            }

            _targetHost = targetHost;
            _allowInvalidCertificates = allowInvalidCertificates;
        }

        /// <summary>
        /// 握手完成后的双工字节流；尚未连接或已释放时抛 <see cref="InvalidOperationException"/>
        /// </summary>
        public Stream Stream
        {
            get
            {
                var client = _client;
                if (client == null)
                {
                    throw new InvalidOperationException("TLS 传输尚未连接");
                }

                return client.GetStream();
            }
        }

        /// <summary>
        /// 本地绑定的端点；未连接或已释放时为 null
        /// </summary>
        public IPEndPoint LocalEndPoint
        {
            get
            {
                if (_disposed)
                {
                    return null;
                }

                var client = _client;
                if (client == null)
                {
                    return null;
                }

                var socket = client.Socket;
                return socket == null ? null : socket.LocalEndPoint as IPEndPoint;
            }
        }

        /// <summary>
        /// 是否可用于收发：要求 TLS 握手已完成且未被释放
        /// </summary>
        public bool IsConnected
        {
            get
            {
                var client = _client;
                return !_disposed && client != null && client.IsAuthenticated;
            }
        }

        /// <summary>
        /// 连接到远端端点并完成 TLS 握手，成功后取得字节流
        /// </summary>
        /// <param name="remoteEndPoint">目标端点</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>连接与握手均成功的任务；握手失败时抛异常且不占用本实例</returns>
        /// <exception cref="ArgumentNullException">remoteEndPoint 为 null</exception>
        /// <exception cref="ObjectDisposedException">本实例已释放</exception>
        /// <exception cref="InvalidOperationException">本实例已连接或正在连接中</exception>
        /// <exception cref="OperationCanceledException">连接前已被取消</exception>
        public async Task ConnectAsync(IPEndPoint remoteEndPoint, CancellationToken cancellationToken)
        {
            if (remoteEndPoint == null)
            {
                throw new ArgumentNullException(nameof(remoteEndPoint));
            }

            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            lock (_sync)
            {
                ThrowIfDisposed();
                if (_client != null) throw new InvalidOperationException("该传输实例已经连接，不能重复连接。");
                if (_connectorBusy) throw new InvalidOperationException("该传输实例正在连接中，不能重复连接。");

                // 必须在离开锁之前占位，否则并发调用都能通过上面的检查
                _connectorBusy = true;
            }

            SaeaClientSocket client = null;

            try
            {
                var option = SocketOptionBuilder.Instance
                    .SetSocket(SAEASocketType.Tcp)
                    .UseStream()
                    .SetIPEndPoint(remoteEndPoint)
                    .SetReadBufferSize(ReceiveBufferSize)
                    .SetActionTimeOut(30 * 1000)
                    .SetFreeTime(IdleTimeoutMilliseconds)
                    .WithSsl(o =>
                    {
                        o.TargetHost = _targetHost;
                        o.AllowInvalidCertificates = _allowInvalidCertificates;
                    })
                    .Build();

                client = new SaeaClientSocket(option);

                // 该重载会等 TLS 握手完成，握手失败在这里抛出，不能用只等连通的 TryConnectAsync
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                lock (_sync)
                {
                    if (_disposed)
                    {
                        DisposeQuietly(client);
                        client = null;
                        throw new ObjectDisposedException(GetType().Name);
                    }

                    // 套接字所有权已交给本实例，由 finally 负责的临时引用置空
                    _client = client;
                    client = null;
                }
            }
            finally
            {
                // 任何路径都要复位，否则一次失败会让该实例永久拒绝连接
                lock (_sync)
                {
                    _connectorBusy = false;
                }

                // 失败路径上套接字所有权还没交给本实例，必须在这里释放，否则句柄泄漏
                if (client != null)
                {
                    DisposeQuietly(client);
                }
            }
        }

        /// <summary>
        /// TLS 数据连接不支持指定本地端点：数据连接的去向由 TURN 服务端决定，不需要选择源地址
        /// </summary>
        /// <param name="remoteEndPoint">目标端点</param>
        /// <param name="localEndPoint">本地绑定端点</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>不会返回</returns>
        /// <exception cref="NotSupportedException">总是抛出</exception>
        public Task ConnectAsync(IPEndPoint remoteEndPoint, IPEndPoint localEndPoint, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("TLS 数据连接不支持指定本地端点。");
        }

        /// <summary>
        /// 不接受连接：本类型只用于主动连接
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>不会返回</returns>
        /// <exception cref="NotSupportedException">总是抛出</exception>
        public Task<ITcpTransport> AcceptAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException("TLS 传输只用于主动连接，不能接受连接。");
        }

        /// <summary>
        /// 断开连接并释放底层资源；可重复调用
        /// </summary>
        public void Dispose()
        {
            SaeaClientSocket client;

            lock (_sync)
            {
                if (_disposed) return;

                _disposed = true;
                client = _client;
                _client = null;
            }

            // 释放会回调底层套接字并可能抛异常，放在锁外且只记日志
            if (client != null)
            {
                try
                {
                    client.Dispose();
                }
                catch (Exception ex)
                {
                    P2PLogHelper.Debug("SaeaTlsTcpTransport", "释放底层连接失败：" + ex.GetType().Name);
                }
            }
        }

        /// <summary>
        /// 已释放时抛出 <see cref="ObjectDisposedException"/>
        /// </summary>
        /// <exception cref="ObjectDisposedException">本实例已释放</exception>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        /// <summary>
        /// 释放底层套接字并吞掉释放阶段的异常，避免掩盖原始失败原因
        /// </summary>
        /// <param name="client">待释放的套接字</param>
        private static void DisposeQuietly(SaeaClientSocket client)
        {
            if (client == null) return;

            try
            {
                client.Dispose();
            }
            catch (Exception ex)
            {
                P2PLogHelper.Debug("SaeaTlsTcpTransport", "释放未接管的连接失败：" + ex.GetType().Name);
            }
        }
    }
}