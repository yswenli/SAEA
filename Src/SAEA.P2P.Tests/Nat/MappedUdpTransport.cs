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
*文件名： MappedUdpTransport
*版本号： net10.0
*唯一标识：b6b43a37-231e-41d5-9fb6-0dbad90500bc
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 21:00:00
*描述：NAT 内部主机的 UDP 传输适配器，出向加地址头发往网关
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 14:52:07
*修改人： yswenli
*版本号： net10.0
*描述：新增仅网关构造、不可路由目标快速失败与未发出计数
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Transport;

namespace SAEA.P2P.Tests.Nat
{
    /// <summary>
    /// NAT 内部主机的传输适配器：出向把真实目标端点写进 6 字节地址头后发往 NAT 网关，
    /// 入向按地址头还原真实来源端点，使上层看到的是公网视角下的收发。
    /// </summary>
    public sealed class MappedUdpTransport : IUdpTransport
    {
        /// <summary>
        /// 承载内部主机地址的 UDP 传输
        /// </summary>
        private readonly SaeaUdpTransport _inner = new SaeaUdpTransport();

        /// <summary>
        /// NAT 内部网关端点
        /// </summary>
        private readonly IPEndPoint _gateway;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 不可路由的目标地址集合，发送前按目标地址判断是否按链路不可达失败
        /// </summary>
        private readonly HashSet<IPAddress> _unroutableDestinations;

        /// <summary>
        /// 因目标不可路由而未发出的数据报数量
        /// </summary>
        private long _droppedDatagramCount;

        /// <summary>
        /// 绑定内部主机地址、订阅底层接收并启动接收循环
        /// </summary>
        /// <param name="localEndPoint">内部主机端点，端口为 0 时由系统分配</param>
        /// <param name="gateway">NAT 内部网关</param>
        /// <exception cref="ArgumentNullException">localEndPoint 或 gateway 为 null</exception>
        public MappedUdpTransport(IPEndPoint localEndPoint, IPEndPoint gateway)
        {
            if (localEndPoint == null) throw new ArgumentNullException(nameof(localEndPoint));
            if (gateway == null) throw new ArgumentNullException(nameof(gateway));

            _gateway = gateway;
            _unroutableDestinations = new HashSet<IPAddress>();
            _inner.BindAsync(localEndPoint, CancellationToken.None).GetAwaiter().GetResult();
            _inner.DatagramReceived += OnInnerDatagram;
            _inner.StartReceive();
        }

        /// <summary>
        /// 只记住 NAT 内部网关，不做绑定，绑定由调用方通过 <see cref="BindAsync"/> 完成
        /// </summary>
        /// <param name="gateway">NAT 内部网关</param>
        /// <exception cref="ArgumentNullException">gateway 为 null</exception>
        public MappedUdpTransport(IPEndPoint gateway)
        {
            if (gateway == null) throw new ArgumentNullException(nameof(gateway));

            _gateway = gateway;
            _unroutableDestinations = new HashSet<IPAddress>();
            _inner.DatagramReceived += OnInnerDatagram;
        }

        /// <summary>
        /// 收到数据报时触发，来源为 NAT 还原后的真实远端端点
        /// </summary>
        public event Action<IPEndPoint, ReadOnlyMemory<byte>> DatagramReceived;

        /// <summary>
        /// 内部主机本地端点；未绑定或已释放时为 null
        /// </summary>
        public IPEndPoint LocalEndPoint
        {
            get { return _inner.LocalEndPoint; }
        }

        /// <summary>
        /// 底层传输是否已绑定并可收发
        /// </summary>
        public bool IsConnected
        {
            get { return _inner.IsConnected; }
        }

        /// <summary>
        /// 不可路由的目标地址集合：发送目标地址命中时按链路不可达立即失败，用于模拟目标不可达；
        /// 默认空，由用例在开始收发前配置
        /// </summary>
        public HashSet<IPAddress> UnroutableDestinations
        {
            get { return _unroutableDestinations; }
        }

        /// <summary>
        /// 因目标不可路由而未发出的数据报数量
        /// </summary>
        public long DroppedDatagramCount
        {
            get { return Interlocked.Read(ref _droppedDatagramCount); }
        }

        /// <summary>
        /// 绑定内部主机地址；底层在绑定时即开始接收，重复绑定或已释放由底层实现拒绝
        /// </summary>
        /// <param name="localEndPoint">本地端点</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>绑定完成的任务</returns>
        /// <exception cref="ArgumentNullException">localEndPoint 为 null</exception>
        /// <exception cref="InvalidOperationException">已绑定或已释放</exception>
        public Task BindAsync(IPEndPoint localEndPoint, CancellationToken cancellationToken)
        {
            return _inner.BindAsync(localEndPoint, cancellationToken);
        }

        /// <summary>
        /// 启动底层接收循环，幂等
        /// </summary>
        /// <exception cref="InvalidOperationException">尚未绑定或已释放</exception>
        public void StartReceive()
        {
            _inner.StartReceive();
        }

        /// <summary>
        /// 把真实目标端点写进地址头后发往 NAT 网关；目标地址命中不可路由集合时按链路不可达失败
        /// </summary>
        /// <param name="remoteEndPoint">真实目标端点</param>
        /// <param name="data">原始载荷</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>发送完成的任务；目标不可路由时为已失败的任务，让调用方立即按传输错误收尾</returns>
        /// <exception cref="ArgumentNullException">remoteEndPoint 为 null</exception>
        /// <exception cref="InvalidOperationException">尚未绑定或已释放</exception>
        public Task SendToAsync(IPEndPoint remoteEndPoint, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            if (remoteEndPoint == null) throw new ArgumentNullException(nameof(remoteEndPoint));

            if (_unroutableDestinations.Contains(remoteEndPoint.Address))
            {
                // 集合只在用例开始收发前配置、运行期只读，因此不必加锁；
                // 这里必须显式失败而不是静默丢弃，否则等待应答的检查会白耗掉整个阶段预算
                Interlocked.Increment(ref _droppedDatagramCount);
                return Task.FromException(new SocketException((int)SocketError.HostUnreachable));
            }

            var packet = NatPacket.Encode(remoteEndPoint, data.Span);
            return _inner.SendToAsync(_gateway, new ReadOnlyMemory<byte>(packet), cancellationToken);
        }

        /// <summary>
        /// 退订接收事件并释放底层传输，可重复调用
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try { _inner.DatagramReceived -= OnInnerDatagram; } catch { }
            _inner.Dispose();
        }

        /// <summary>
        /// 解析地址头后把真实来源与载荷交给上层；地址头非法时丢弃该报文
        /// </summary>
        /// <param name="from">NAT 网关端点</param>
        /// <param name="data">带地址头的报文</param>
        private void OnInnerDatagram(IPEndPoint from, ReadOnlyMemory<byte> data)
        {
            // 解码结果指向本次副本，跨回调保存也安全，不需要依赖底层缓冲的复用时机
            var buffer = data.ToArray();
            IPEndPoint source;
            ArraySegment<byte> payload;
            if (!NatPacket.TryDecode(buffer, 0, buffer.Length, out source, out payload)) return;

            var handler = DatagramReceived;
            if (handler != null)
            {
                handler(source, new ReadOnlyMemory<byte>(payload.Array, payload.Offset, payload.Count));
            }
        }
    }
}
