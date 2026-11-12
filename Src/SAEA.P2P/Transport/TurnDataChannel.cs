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
*文件名： TurnDataChannel
*版本号： v26.11.5.1
*唯一标识：df786ff8-4309-487c-a3bf-15c2a3199667
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 20:00:00
*描述：TURN 中继数据通道——按对端收发数据报并承载权限与通道绑定
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 20:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TURN 中继数据通道
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Common;
using SAEA.P2P.Ice;
using SAEA.P2P.Turn;

namespace SAEA.P2P.Transport
{
    /// <summary>
    /// TURN 中继数据通道：把一个已建立的 TURN 分配包装成 <see cref="IUdpTransport"/>，
    /// 收到 Data 指示时按对端端点原样上报，收到 ChannelData 时按本实例登记的通道号反查对端。
    /// 本地端点固定为中继地址，不能绑定；权限与通道绑定都委托给分配客户端完成。
    /// </summary>
    public sealed class TurnDataChannel : IUdpTransport, ITurnRelayPermission, IDisposable
    {
        /// <summary>
        /// 通道号下界，RFC 5766 规定从 0x4000 起
        /// </summary>
        private const ushort FirstChannelNumber = 0x4000;

        /// <summary>
        /// 通道号上界
        /// </summary>
        private const ushort LastChannelNumber = 0x4FFF;

        /// <summary>
        /// 所属 TURN 客户端，接收事件从它订阅
        /// </summary>
        private readonly TurnClient _client;

        /// <summary>
        /// 承载本通道的分配客户端，发送、权限与通道绑定都经过它
        /// </summary>
        private readonly TurnAllocationClient _allocation;

        /// <summary>
        /// 保护通道号映射与释放状态的同步对象
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 通道号到对端的映射，用于 ChannelData 反查来源
        /// </summary>
        private readonly Dictionary<ushort, IPEndPoint> _channelPeers = new Dictionary<ushort, IPEndPoint>();

        /// <summary>
        /// 对端到通道号的映射，用于发送时选择 ChannelData 而不是 Send 指示
        /// </summary>
        private readonly Dictionary<IPEndPoint, ushort> _peerChannels = new Dictionary<IPEndPoint, ushort>();

        /// <summary>
        /// 是否已订阅 TURN 客户端的接收事件
        /// </summary>
        private bool _receiving;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 创建中继数据通道；构造阶段不订阅事件也不发任何报文
        /// </summary>
        /// <param name="client">所属 TURN 客户端</param>
        /// <param name="allocation">已建立的分配</param>
        /// <exception cref="ArgumentNullException">client 或 allocation 为 null</exception>
        public TurnDataChannel(TurnClient client, TurnAllocationClient allocation)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }
            if (allocation == null)
            {
                throw new ArgumentNullException(nameof(allocation));
            }

            _client = client;
            _allocation = allocation;
        }

        /// <summary>
        /// 收到数据报时触发，参数为来源端点与已由 TURN 客户端复制好的数据
        /// </summary>
        public event Action<IPEndPoint, ReadOnlyMemory<byte>> DatagramReceived;

        /// <summary>
        /// 本地端点：固定为中继地址；已释放时为 null
        /// </summary>
        public IPEndPoint LocalEndPoint
        {
            get
            {
                lock (_sync)
                {
                    if (_disposed || _allocation == null)
                    {
                        return null;
                    }
                    return _allocation.RelayedEndPoint;
                }
            }
        }

        /// <summary>
        /// 是否可用：未释放且分配仍然存在
        /// </summary>
        public bool IsConnected
        {
            get
            {
                lock (_sync)
                {
                    return !_disposed && _allocation != null;
                }
            }
        }

        /// <summary>
        /// 订阅 TURN 客户端的 Data 与 ChannelData 事件；重复调用为空操作
        /// </summary>
        public void StartReceive()
        {
            lock (_sync)
            {
                if (_disposed || _receiving)
                {
                    return;
                }
                _receiving = true;
            }

            _client.DataReceived += OnDataReceived;
            _client.ChannelDataReceived += OnChannelDataReceived;
        }

        /// <summary>
        /// 不支持绑定：中继通道的本地端点由服务端分配，调用方不能替换
        /// </summary>
        /// <param name="localEndPoint">本地端点</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>不会返回</returns>
        /// <exception cref="NotSupportedException">固定抛出</exception>
        public Task BindAsync(IPEndPoint localEndPoint, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("中继通道的本地端点固定为中继地址，不能绑定");
        }

        /// <summary>
        /// 向对端发送一个数据报：已绑定通道的对端走 ChannelData，其余走 Send 指示
        /// </summary>
        /// <param name="remoteEndPoint">目标端点</param>
        /// <param name="data">待发送数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>发送完成的任务</returns>
        /// <exception cref="ArgumentNullException">remoteEndPoint 为 null</exception>
        /// <exception cref="ObjectDisposedException">本实例已释放</exception>
        public Task SendToAsync(IPEndPoint remoteEndPoint, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            if (remoteEndPoint == null)
            {
                throw new ArgumentNullException(nameof(remoteEndPoint));
            }

            ushort channel;
            bool bound;
            lock (_sync)
            {
                ThrowIfDisposedLocked();
                bound = _peerChannels.TryGetValue(remoteEndPoint, out channel);
            }

            // 分配层按数组编码报文，这里必须先转成数组，避免把调用方内存的生命周期带进发送路径
            var payload = data.ToArray();
            if (bound)
            {
                return _allocation.SendChannelDataAsync(channel, payload, cancellationToken);
            }
            return _allocation.SendAsync(remoteEndPoint, payload, cancellationToken);
        }

        /// <summary>
        /// 为对端地址建立中继权限；同一 IP 只申请一次，端口不参与匹配
        /// </summary>
        /// <param name="remoteAddresses">对端 IP 列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>权限建立完成的任务</returns>
        /// <exception cref="ArgumentNullException">remoteAddresses 为 null</exception>
        public async Task CreatePermissionsAsync(IReadOnlyList<IPAddress> remoteAddresses, CancellationToken cancellationToken)
        {
            if (remoteAddresses == null)
            {
                throw new ArgumentNullException(nameof(remoteAddresses));
            }
            if (remoteAddresses.Count == 0)
            {
                return;
            }

            var seen = new HashSet<IPAddress>();
            for (var i = 0; i < remoteAddresses.Count; i++)
            {
                var address = remoteAddresses[i];
                if (address == null || !seen.Add(address))
                {
                    continue;
                }

                // 权限只按 IP 放行，服务端不匹配端口，这里统一填 0
                await _allocation.CreatePermissionAsync(new IPEndPoint(address, 0), cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 为对端绑定通道；已登记的对端直接返回，否则从 0x4000 起取本实例未占用的最小通道号
        /// </summary>
        /// <param name="peer">对端端点</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>绑定完成的任务</returns>
        /// <exception cref="ArgumentNullException">peer 为 null</exception>
        /// <exception cref="ObjectDisposedException">本实例已释放</exception>
        /// <exception cref="InvalidOperationException">本实例的通道号已用尽</exception>
        /// <exception cref="TurnClientException">服务端拒绝或请求超时</exception>
        public async Task BindChannelAsync(IPEndPoint peer, CancellationToken cancellationToken)
        {
            if (peer == null)
            {
                throw new ArgumentNullException(nameof(peer));
            }

            ushort channel;
            lock (_sync)
            {
                ThrowIfDisposedLocked();
                if (_peerChannels.TryGetValue(peer, out channel))
                {
                    return;
                }

                // 先占号再发请求：并发绑定同一对端时不会让两路请求抢到同一个通道号
                channel = FindFreeChannelLocked();
                _channelPeers[channel] = peer;
            }

            try
            {
                await _allocation.ChannelBindAsync(channel, peer, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                lock (_sync)
                {
                    _channelPeers.Remove(channel);
                }
                throw;
            }

            lock (_sync)
            {
                if (!_disposed)
                {
                    _peerChannels[peer] = channel;
                }
            }
        }

        /// <summary>
        /// 退订接收事件并清空通道映射；重复调用为空操作。
        /// 只做本地清理，不发任何报文，分配释放由调用方显式完成。
        /// </summary>
        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
                _channelPeers.Clear();
                _peerChannels.Clear();
            }

            try
            {
                _client.DataReceived -= OnDataReceived;
            }
            catch (ObjectDisposedException)
            {
                // 客户端已释放时退订为空操作
            }
            try
            {
                _client.ChannelDataReceived -= OnChannelDataReceived;
            }
            catch (ObjectDisposedException)
            {
                // 客户端已释放时退订为空操作
            }
        }

        /// <summary>
        /// Data 指示入口：对端端点由 TURN 客户端给出，直接上报
        /// </summary>
        /// <param name="remoteEndPoint">对端端点</param>
        /// <param name="data">应用数据副本</param>
        private void OnDataReceived(IPEndPoint remoteEndPoint, byte[] data)
        {
            if (data == null)
            {
                return;
            }

            RaiseDatagramReceived(remoteEndPoint, new ReadOnlyMemory<byte>(data));
        }

        /// <summary>
        /// ChannelData 入口：按本实例登记的通道号反查对端，未登记时丢弃
        /// </summary>
        /// <param name="channelNumber">通道号</param>
        /// <param name="data">应用数据副本</param>
        private void OnChannelDataReceived(ushort channelNumber, byte[] data)
        {
            if (data == null)
            {
                return;
            }

            IPEndPoint peer;
            lock (_sync)
            {
                if (_disposed || !_channelPeers.TryGetValue(channelNumber, out peer))
                {
                    // 没有对端信息就无法判断来源，未登记的通道号只能丢弃
                    return;
                }
            }

            RaiseDatagramReceived(peer, new ReadOnlyMemory<byte>(data));
        }

        /// <summary>
        /// 逐订阅者触发数据报事件；单个订阅者异常不影响其余订阅者与接收循环
        /// </summary>
        /// <param name="remoteEndPoint">来源端点</param>
        /// <param name="data">应用数据</param>
        private void RaiseDatagramReceived(IPEndPoint remoteEndPoint, ReadOnlyMemory<byte> data)
        {
            var handler = DatagramReceived;
            if (handler == null)
            {
                return;
            }

            var subscribers = handler.GetInvocationList();
            for (var i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    ((Action<IPEndPoint, ReadOnlyMemory<byte>>)subscribers[i])(remoteEndPoint, data);
                }
                catch (Exception ex)
                {
                    P2PLogHelper.Debug("TurnDataChannel", "数据报回调异常：" + ex.GetType().Name);
                }
            }
        }

        /// <summary>
        /// 取本实例未占用的最小通道号；调用方必须已持有锁
        /// </summary>
        /// <returns>可用通道号</returns>
        /// <exception cref="InvalidOperationException">通道号已用尽</exception>
        private ushort FindFreeChannelLocked()
        {
            for (var number = (int)FirstChannelNumber; number <= LastChannelNumber; number++)
            {
                var channel = (ushort)number;
                if (!_channelPeers.ContainsKey(channel))
                {
                    return channel;
                }
            }

            throw new InvalidOperationException("通道号已用尽");
        }

        /// <summary>
        /// 释放前检查；调用方必须已持有锁
        /// </summary>
        /// <exception cref="ObjectDisposedException">本实例已释放</exception>
        private void ThrowIfDisposedLocked()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}