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
*修改时间：2026/11/12 02:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TURN over TLS 客户端传输
*
*****************************************************************************/
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Sockets;
using SAEA.Sockets.Core.Tcp;

namespace SAEA.P2P.Turn
{
    /// <summary>
    /// TURN over TLS 客户端传输：用 SAEA.Sockets 的 BouncyCastle TLS 建立并维护一条长连接控制通道，
    /// 交给调用方一个双工流，由 TURN 客户端在其上按自身长度字段分帧。
    /// 证书校验默认开启，只有测试等场景才显式允许无效证书。
    /// </summary>
    public sealed class TurnTlsClientTransport : IDisposable
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
        /// 是否已释放
        /// </summary>
        private bool _disposed;

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
            get { return _client.IsAuthenticated; }
        }

        /// <summary>
        /// 断开连接并释放底层资源；可重复调用
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _client.Dispose();
        }
    }
}