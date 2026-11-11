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
*文件名： TurnAllocation
*版本号： v26.11.5.1
*唯一标识：45302433-2df0-42f7-be55-47599ff0299f
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 00:20:00
*描述：TURN 分配及其权限、通道与中继 socket
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 00:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TURN 分配及其权限、通道与中继 socket
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

using SAEA.P2P.StunServer;

namespace SAEA.P2P.TurnServer
{
    /// <summary>
    /// TURN 分配：一个客户端、一个控制连接本地端点与一种中继传输对应一个分配。
    /// 权限、通道与配额计数都在本对象内维护，三层计时互不影响；所有公开方法按内部锁串行化。
    /// </summary>
    public sealed class TurnAllocation : IDisposable
    {
        /// <summary>
        /// 保护权限、通道、配额与过期时间的同步对象
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 按 IP 索引的权限
        /// </summary>
        private readonly Dictionary<IPAddress, TurnPermission> _permissions = new Dictionary<IPAddress, TurnPermission>();

        /// <summary>
        /// 按通道号索引的通道
        /// </summary>
        private readonly Dictionary<ushort, TurnChannel> _channels = new Dictionary<ushort, TurnChannel>();

        /// <summary>
        /// 通道解绑后禁止改绑到其它对端的截止时间
        /// </summary>
        private readonly Dictionary<ushort, DateTimeOffset> _channelQuarantine = new Dictionary<ushort, DateTimeOffset>();

        /// <summary>
        /// 长期密钥；用于给本分配的应答与挑战签名
        /// </summary>
        private readonly byte[] _integrityKey;

        /// <summary>
        /// 中继 socket；UDP 分配由服务端创建并注入
        /// </summary>
        private Socket _relaySocket;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 创建分配
        /// </summary>
        /// <param name="clientEndPoint">客户端来源端点</param>
        /// <param name="localEndPoint">控制连接的本地端点</param>
        /// <param name="transport">中继传输协议号，UDP 为 17、TCP 为 6</param>
        /// <param name="username">通过鉴权的用户名</param>
        /// <param name="relayedEndPoint">中继地址</param>
        /// <param name="lifetimeSeconds">生命周期，单位秒</param>
        /// <param name="integrityKey">长期密钥，可为 null</param>
        /// <param name="now">当前时间</param>
        /// <exception cref="ArgumentNullException">端点或用户名为 null</exception>
        /// <exception cref="ArgumentOutOfRangeException">生命周期不是正数</exception>
        public TurnAllocation(
            IPEndPoint clientEndPoint,
            IPEndPoint localEndPoint,
            byte transport,
            string username,
            IPEndPoint relayedEndPoint,
            uint lifetimeSeconds,
            byte[] integrityKey,
            DateTimeOffset now)
        {
            if (clientEndPoint == null)
            {
                throw new ArgumentNullException(nameof(clientEndPoint));
            }
            if (localEndPoint == null)
            {
                throw new ArgumentNullException(nameof(localEndPoint));
            }
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentNullException(nameof(username));
            }
            if (relayedEndPoint == null)
            {
                throw new ArgumentNullException(nameof(relayedEndPoint));
            }
            if (lifetimeSeconds == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lifetimeSeconds), "分配生命周期必须大于 0");
            }

            ClientEndPoint = clientEndPoint;
            LocalEndPoint = localEndPoint;
            Transport = transport;
            Username = username;
            RelayedEndPoint = relayedEndPoint;
            LifetimeSeconds = lifetimeSeconds;
            ExpiresAt = now.AddSeconds(lifetimeSeconds);
            _integrityKey = integrityKey == null ? new byte[0] : (byte[])integrityKey.Clone();
        }

        /// <summary>
        /// 客户端来源端点，含 IP 与端口
        /// </summary>
        public IPEndPoint ClientEndPoint { get; private set; }

        /// <summary>
        /// 控制连接的本地端点
        /// </summary>
        public IPEndPoint LocalEndPoint { get; private set; }

        /// <summary>
        /// 中继传输协议号，UDP 为 17、TCP 为 6
        /// </summary>
        public byte Transport { get; private set; }

        /// <summary>
        /// 通过鉴权的用户名
        /// </summary>
        public string Username { get; private set; }

        /// <summary>
        /// 中继地址，必须与分配出来时一致
        /// </summary>
        public IPEndPoint RelayedEndPoint { get; private set; }

        /// <summary>
        /// 当前生命周期，单位秒
        /// </summary>
        public uint LifetimeSeconds { get; private set; }

        /// <summary>
        /// 过期时间
        /// </summary>
        public DateTimeOffset ExpiresAt { get; private set; }

        /// <summary>
        /// 已中继的累计字节数
        /// </summary>
        public long ByteCount
        {
            get { lock (_sync) { return _byteCount; } }
        }

        /// <summary>
        /// 因未授权或超配额丢弃的数据报数量
        /// </summary>
        public long DroppedDatagramCount
        {
            get { lock (_sync) { return _droppedDatagramCount; } }
        }

        /// <summary>
        /// 权限数量，含尚未清理的过期项
        /// </summary>
        public int PermissionCount
        {
            get { lock (_sync) { return _permissions.Count; } }
        }

        /// <summary>
        /// 通道数量，含尚未清理的过期项
        /// </summary>
        public int ChannelCount
        {
            get { lock (_sync) { return _channels.Count; } }
        }

        /// <summary>
        /// 中继 socket；由服务端注入，未注入时为 null
        /// </summary>
        internal Socket RelaySocket
        {
            get { lock (_sync) { return _relaySocket; } }
            set { lock (_sync) { _relaySocket = value; } }
        }

        /// <summary>
        /// 发送 Data 指示或 ChannelData 时使用的控制端点；由服务端注入
        /// </summary>
        internal StunServerEndpoint ControlEndpoint { get; set; }

        /// <summary>
        /// 累计中继字节数
        /// </summary>
        private long _byteCount;

        /// <summary>
        /// 累计丢弃数据报数
        /// </summary>
        private long _droppedDatagramCount;

        /// <summary>
        /// 判断分配是否已过期
        /// </summary>
        /// <param name="now">当前时间</param>
        /// <returns>已过期返回 true</returns>
        public bool IsExpired(DateTimeOffset now)
        {
            lock (_sync)
            {
                return now >= ExpiresAt;
            }
        }

        /// <summary>
        /// 刷新生命周期
        /// </summary>
        /// <param name="lifetimeSeconds">新生命周期，单位秒，必须大于 0</param>
        /// <param name="now">当前时间</param>
        /// <exception cref="ArgumentOutOfRangeException">生命周期不是正数</exception>
        public void Refresh(uint lifetimeSeconds, DateTimeOffset now)
        {
            if (lifetimeSeconds == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lifetimeSeconds), "刷新用的生命周期必须大于 0");
            }

            lock (_sync)
            {
                LifetimeSeconds = lifetimeSeconds;
                ExpiresAt = now.AddSeconds(lifetimeSeconds);
            }
        }

        /// <summary>
        /// 创建或刷新一个对端 IP 的权限；端口不参与匹配
        /// </summary>
        /// <param name="peer">对端地址</param>
        /// <param name="now">当前时间</param>
        /// <exception cref="ArgumentNullException">peer 为 null</exception>
        public void AddPermission(IPEndPoint peer, DateTimeOffset now)
        {
            if (peer == null)
            {
                throw new ArgumentNullException(nameof(peer));
            }

            var address = Normalize(peer.Address);
            lock (_sync)
            {
                TurnPermission permission;
                if (_permissions.TryGetValue(address, out permission) && !permission.IsExpired(now))
                {
                    permission.Refresh(now);
                    return;
                }

                _permissions[address] = new TurnPermission(address, now);
            }
        }

        /// <summary>
        /// 判断该对端是否被授权；只看 IP，任意端口都放行
        /// </summary>
        /// <param name="peer">对端地址</param>
        /// <param name="now">当前时间</param>
        /// <returns>有有效权限返回 true</returns>
        public bool IsPermitted(IPEndPoint peer, DateTimeOffset now)
        {
            if (peer == null)
            {
                return false;
            }

            var address = Normalize(peer.Address);
            lock (_sync)
            {
                TurnPermission permission;
                if (!_permissions.TryGetValue(address, out permission))
                {
                    return false;
                }

                if (permission.IsExpired(now))
                {
                    _permissions.Remove(address);
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// 绑定通道；同一通道号改绑到不同对端时，若旧绑定刚过期则需等待 300 秒
        /// </summary>
        /// <param name="channelNumber">通道号，取值 0x4000 到 0x4FFF</param>
        /// <param name="peer">对端地址</param>
        /// <param name="now">当前时间</param>
        /// <returns>绑定成功返回 true；通道号被占用或处于隔离期返回 false</returns>
        /// <exception cref="ArgumentOutOfRangeException">通道号不在允许区间</exception>
        /// <exception cref="ArgumentNullException">peer 为 null</exception>
        public bool BindChannel(ushort channelNumber, IPEndPoint peer, DateTimeOffset now)
        {
            if (channelNumber < 0x4000 || channelNumber > 0x4FFF)
            {
                throw new ArgumentOutOfRangeException(nameof(channelNumber), "通道号必须落在 0x4000 到 0x4FFF 之间");
            }
            if (peer == null)
            {
                throw new ArgumentNullException(nameof(peer));
            }

            lock (_sync)
            {
                TurnChannel existing;
                if (_channels.TryGetValue(channelNumber, out existing))
                {
                    if (!existing.IsExpired(now))
                    {
                        if (!EndPointEquals(existing.Peer, peer))
                        {
                            return false;
                        }

                        existing.Refresh(now);
                        AddPermissionLocked(peer, now);
                        return true;
                    }

                    // 旧绑定已过期：记下隔离截止时间，再按隔离规则判断能否改绑
                    _channels.Remove(channelNumber);
                    _channelQuarantine[channelNumber] = existing.ExpiresAt.AddSeconds(TurnChannel.RebindQuarantineSeconds);
                }

                DateTimeOffset quarantineUntil;
                if (_channelQuarantine.TryGetValue(channelNumber, out quarantineUntil))
                {
                    if (now < quarantineUntil)
                    {
                        return false;
                    }

                    _channelQuarantine.Remove(channelNumber);
                }

                _channels[channelNumber] = new TurnChannel(channelNumber, peer, now);
                AddPermissionLocked(peer, now);
                return true;
            }
        }

        /// <summary>
        /// 按通道号取有效通道；过期项会被移除并进入隔离期
        /// </summary>
        /// <param name="channelNumber">通道号</param>
        /// <param name="now">当前时间</param>
        /// <param name="channel">取到的通道；失败时为 null</param>
        /// <returns>存在且未过期返回 true</returns>
        public bool TryGetChannel(ushort channelNumber, DateTimeOffset now, out TurnChannel channel)
        {
            channel = null;
            lock (_sync)
            {
                TurnChannel candidate;
                if (!_channels.TryGetValue(channelNumber, out candidate))
                {
                    return false;
                }

                if (candidate.IsExpired(now))
                {
                    _channels.Remove(channelNumber);
                    _channelQuarantine[channelNumber] = candidate.ExpiresAt.AddSeconds(TurnChannel.RebindQuarantineSeconds);
                    return false;
                }

                channel = candidate;
                return true;
            }
        }

        /// <summary>
        /// 按对端地址找有效通道；端口必须完全一致
        /// </summary>
        /// <param name="peer">对端地址</param>
        /// <param name="now">当前时间</param>
        /// <param name="channel">取到的通道；失败时为 null</param>
        /// <returns>存在且未过期返回 true</returns>
        public bool TryGetChannelByPeer(IPEndPoint peer, DateTimeOffset now, out TurnChannel channel)
        {
            channel = null;
            if (peer == null)
            {
                return false;
            }

            lock (_sync)
            {
                foreach (var pair in _channels)
                {
                    var candidate = pair.Value;
                    if (candidate.IsExpired(now))
                    {
                        continue;
                    }

                    if (EndPointEquals(candidate.Peer, peer))
                    {
                        channel = candidate;
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// 累加已中继字节数
        /// </summary>
        /// <param name="count">本次字节数</param>
        /// <returns>累加后的总量</returns>
        public long AddBytes(int count)
        {
            lock (_sync)
            {
                _byteCount += count;
                return _byteCount;
            }
        }

        /// <summary>
        /// 累加丢弃计数
        /// </summary>
        public void AddDroppedDatagram()
        {
            lock (_sync)
            {
                _droppedDatagramCount++;
            }
        }

        /// <summary>
        /// 取长期密钥的副本；不存在时为空数组
        /// </summary>
        /// <returns>密钥副本</returns>
        internal byte[] GetIntegrityKey()
        {
            var copy = new byte[_integrityKey.Length];
            Buffer.BlockCopy(_integrityKey, 0, copy, 0, _integrityKey.Length);
            return copy;
        }

        /// <summary>
        /// 关闭中继 socket 并清空权限与通道；可重复调用
        /// </summary>
        public void Dispose()
        {
            Socket socket;
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                socket = _relaySocket;
                _relaySocket = null;
                _permissions.Clear();
                _channels.Clear();
                _channelQuarantine.Clear();
            }

            if (socket != null)
            {
                try
                {
                    socket.Dispose();
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        /// <summary>
        /// 在已持锁的情况下创建或刷新权限
        /// </summary>
        /// <param name="peer">对端地址</param>
        /// <param name="now">当前时间</param>
        private void AddPermissionLocked(IPEndPoint peer, DateTimeOffset now)
        {
            var address = Normalize(peer.Address);
            TurnPermission permission;
            if (_permissions.TryGetValue(address, out permission) && !permission.IsExpired(now))
            {
                permission.Refresh(now);
                return;
            }

            _permissions[address] = new TurnPermission(address, now);
        }

        /// <summary>
        /// 把 v4-mapped v6 地址归一化为 v4，避免同一 IP 出现两种键
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns>归一化后的地址</returns>
        private static IPAddress Normalize(IPAddress address)
        {
            if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv4MappedToIPv6)
            {
                return address.MapToIPv4();
            }
            return address;
        }

        /// <summary>
        /// 比较两个端点；地址先归一化再比较
        /// </summary>
        /// <param name="left">左端点</param>
        /// <param name="right">右端点</param>
        /// <returns>完全一致返回 true</returns>
        private static bool EndPointEquals(IPEndPoint left, IPEndPoint right)
        {
            return left.Port == right.Port && Normalize(left.Address).Equals(Normalize(right.Address));
        }
    }
}