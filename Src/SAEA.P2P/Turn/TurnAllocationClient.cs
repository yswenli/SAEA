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
*文件名： TurnAllocationClient
*版本号： v26.11.5.1
*唯一标识：46f7a97f-58dc-4fc0-a6f9-20f038bbb504
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 10:00:00
*描述：TURN 分配客户端——权限与通道登记、三层刷新定时器与重连重放
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 10:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TURN 分配客户端
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Common;
using SAEA.P2P.Stun;

namespace SAEA.P2P.Turn
{
    /// <summary>
    /// TURN 分配客户端：保存中继地址与生命周期，登记 permission（按对端 IP 去重）与 channel，
    /// 并按 allocation / permission / channel 三层各自的一次性定时器续期。
    /// 任一层续期失败都把分配标记为失效并交回 <see cref="TurnClient"/> 重连；
    /// 登记表与定时器句柄由同一把锁保护，锁内只做快照，收发包一律在锁外执行。
    /// </summary>
    public sealed class TurnAllocationClient : IDisposable
    {
        /// <summary>
        /// 所属 TURN 客户端，请求发送与定时器挂载都经过它
        /// </summary>
        private readonly TurnClient _client;

        /// <summary>
        /// allocation 层续期间隔
        /// </summary>
        private readonly TimeSpan _allocationRefreshInterval;

        /// <summary>
        /// permission 层续期间隔
        /// </summary>
        private readonly TimeSpan _permissionRefreshInterval;

        /// <summary>
        /// channel 层续期间隔
        /// </summary>
        private readonly TimeSpan _channelRefreshInterval;

        /// <summary>
        /// 保护地址、登记表与定时器句柄的同步对象
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 已登记的 permission，键为对端 IP；端口不参与放行，因此同 IP 不同端口只保留一条
        /// </summary>
        private readonly Dictionary<IPAddress, PermissionEntry> _permissions = new Dictionary<IPAddress, PermissionEntry>();

        /// <summary>
        /// 已登记的 channel，键为通道号
        /// </summary>
        private readonly Dictionary<ushort, ChannelEntry> _channels = new Dictionary<ushort, ChannelEntry>();

        /// <summary>
        /// 中继地址，服务端分配给出，重连后原地更新
        /// </summary>
        private IPEndPoint _relayedEndPoint;

        /// <summary>
        /// 映射地址，服务端看到的客户端出口地址
        /// </summary>
        private IPEndPoint _mappedEndPoint;

        /// <summary>
        /// 生命周期，单位秒；续期成功后按服务端应答更新
        /// </summary>
        private uint _lifetime;

        /// <summary>
        /// 当前控制连接对应的服务端端点，重连后原地更新
        /// </summary>
        private IPEndPoint _serverEndPoint;

        /// <summary>
        /// 分配是否已失效；失效后通道数据不再发送，等待重连重放
        /// </summary>
        private bool _isExpired;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// allocation 层当前的一次性定时器句柄
        /// </summary>
        private IDisposable _allocationTimer;

        /// <summary>
        /// 创建分配客户端；由 <see cref="TurnClient"/> 在 Allocate 成功后调用
        /// </summary>
        /// <param name="client">所属 TURN 客户端</param>
        /// <param name="relayedEndPoint">中继地址</param>
        /// <param name="mappedEndPoint">映射地址</param>
        /// <param name="lifetime">生命周期，单位秒</param>
        /// <param name="serverEndPoint">服务端端点</param>
        /// <param name="allocationRefreshInterval">allocation 续期间隔</param>
        /// <param name="permissionRefreshInterval">permission 续期间隔</param>
        /// <param name="channelRefreshInterval">channel 续期间隔</param>
        /// <exception cref="ArgumentNullException">client 或任一地址为 null</exception>
        internal TurnAllocationClient(
            TurnClient client,
            IPEndPoint relayedEndPoint,
            IPEndPoint mappedEndPoint,
            uint lifetime,
            IPEndPoint serverEndPoint,
            TimeSpan allocationRefreshInterval,
            TimeSpan permissionRefreshInterval,
            TimeSpan channelRefreshInterval)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }
            if (relayedEndPoint == null)
            {
                throw new ArgumentNullException(nameof(relayedEndPoint));
            }

            _client = client;
            _relayedEndPoint = relayedEndPoint;
            _mappedEndPoint = mappedEndPoint;
            _lifetime = lifetime;
            _serverEndPoint = serverEndPoint;
            _allocationRefreshInterval = allocationRefreshInterval;
            _permissionRefreshInterval = permissionRefreshInterval;
            _channelRefreshInterval = channelRefreshInterval;
        }

        /// <summary>
        /// 中继地址：对端可直接把数据发到这里
        /// </summary>
        public IPEndPoint RelayedEndPoint
        {
            get
            {
                lock (_sync)
                {
                    return _relayedEndPoint;
                }
            }
        }

        /// <summary>
        /// 映射地址：服务端看到的客户端出口地址
        /// </summary>
        public IPEndPoint MappedEndPoint
        {
            get
            {
                lock (_sync)
                {
                    return _mappedEndPoint;
                }
            }
        }

        /// <summary>
        /// 生命周期，单位秒
        /// </summary>
        public uint Lifetime
        {
            get
            {
                lock (_sync)
                {
                    return _lifetime;
                }
            }
        }

        /// <summary>
        /// 当前控制连接对应的服务端端点
        /// </summary>
        public IPEndPoint ServerEndPoint
        {
            get
            {
                lock (_sync)
                {
                    return _serverEndPoint;
                }
            }
        }

        /// <summary>
        /// 分配是否已失效；失效表示刷新失败，需等待 <see cref="TurnClient"/> 重连重放
        /// </summary>
        public bool IsExpired
        {
            get
            {
                lock (_sync)
                {
                    return _isExpired;
                }
            }
        }

        /// <summary>
        /// 已登记的 permission 数量，按对端 IP 计数
        /// </summary>
        public int PermissionCount
        {
            get
            {
                lock (_sync)
                {
                    return _permissions.Count;
                }
            }
        }

        /// <summary>
        /// 已登记的 channel 数量
        /// </summary>
        public int ChannelCount
        {
            get
            {
                lock (_sync)
                {
                    return _channels.Count;
                }
            }
        }

        /// <summary>
        /// 启动 allocation 层定时器；由 <see cref="TurnClient"/> 在首次分配成功后调用
        /// </summary>
        public void Start()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }
            }

            ArmAllocationTimer();
        }

        /// <summary>
        /// 重连后原地更新分配信息，不重建登记表也不改动定时器
        /// </summary>
        /// <param name="relayedEndPoint">新的中继地址</param>
        /// <param name="mappedEndPoint">新的映射地址</param>
        /// <param name="lifetime">新的生命周期，单位秒</param>
        /// <param name="serverEndPoint">新的服务端端点</param>
        /// <exception cref="ArgumentNullException">任一地址为 null</exception>
        public void ApplyAllocation(IPEndPoint relayedEndPoint, IPEndPoint mappedEndPoint, uint lifetime, IPEndPoint serverEndPoint)
        {
            if (relayedEndPoint == null)
            {
                throw new ArgumentNullException(nameof(relayedEndPoint));
            }
            if (mappedEndPoint == null)
            {
                throw new ArgumentNullException(nameof(mappedEndPoint));
            }
            if (serverEndPoint == null)
            {
                throw new ArgumentNullException(nameof(serverEndPoint));
            }

            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }
                _relayedEndPoint = relayedEndPoint;
                _mappedEndPoint = mappedEndPoint;
                _lifetime = lifetime;
                _serverEndPoint = serverEndPoint;
            }
        }

        /// <summary>
        /// 申请 permission 并按对端 IP 登记；同一 IP 重复申请只保留一条登记与一个定时器
        /// </summary>
        /// <param name="peer">对端地址，放行按 IP 生效，端口不参与匹配</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>申请完成的任务</returns>
        /// <exception cref="ArgumentNullException">peer 为 null</exception>
        /// <exception cref="TurnClientException">服务端拒绝或请求超时</exception>
        public async Task CreatePermissionAsync(IPEndPoint peer, CancellationToken cancellationToken)
        {
            if (peer == null)
            {
                throw new ArgumentNullException(nameof(peer));
            }

            var request = TurnMessage.CreateCreatePermission(new[] { peer });
            await _client.SendAuthenticatedAsync(request, cancellationToken).ConfigureAwait(false);

            // 只有新 IP 才需要补一个续期定时器，已有 IP 的定时器保持原节奏
            if (RegisterPermission(peer))
            {
                ArmPermissionTimer(peer.Address);
            }
        }

        /// <summary>
        /// 绑定 channel 并登记为可续期的通道；绑定成功同时按对端 IP 补一条 permission 登记
        /// </summary>
        /// <param name="channelNumber">通道号，取值 0x4000 到 0x4FFF</param>
        /// <param name="peer">对端地址</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>绑定完成的任务</returns>
        /// <exception cref="ArgumentOutOfRangeException">通道号不合法</exception>
        /// <exception cref="ArgumentNullException">peer 为 null</exception>
        /// <exception cref="TurnClientException">服务端拒绝或请求超时</exception>
        public async Task ChannelBindAsync(ushort channelNumber, IPEndPoint peer, CancellationToken cancellationToken)
        {
            if (!ChannelData.IsValidChannelNumber(channelNumber))
            {
                throw new ArgumentOutOfRangeException(nameof(channelNumber), "通道号必须落在 0x4000 到 0x4FFF 之间");
            }
            if (peer == null)
            {
                throw new ArgumentNullException(nameof(peer));
            }

            var request = TurnMessage.CreateChannelBind(channelNumber, peer);
            await _client.SendAuthenticatedAsync(request, cancellationToken).ConfigureAwait(false);

            // ChannelBind 本身会在服务端建权限，这里登记一条以便后续按 IP 续期
            if (RegisterPermission(peer))
            {
                ArmPermissionTimer(peer.Address);
            }

            RegisterChannel(channelNumber, peer);
            ArmChannelTimer(channelNumber);
        }

        /// <summary>
        /// 用 Send 指示发送数据；Send 不产生应答，因此只发帧不登记事务
        /// </summary>
        /// <param name="peer">对端地址</param>
        /// <param name="payload">应用数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>发送完成的任务</returns>
        /// <exception cref="ArgumentNullException">peer 或 payload 为 null</exception>
        public Task SendAsync(IPEndPoint peer, byte[] payload, CancellationToken cancellationToken)
        {
            if (peer == null)
            {
                throw new ArgumentNullException(nameof(peer));
            }
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }
            cancellationToken.ThrowIfCancellationRequested();

            var request = TurnMessage.CreateSend(peer, payload);
            _client.SendFrame(request.ToArray());
            return Task.CompletedTask;
        }

        /// <summary>
        /// 用 ChannelData 发送数据；未登记的通道号照常发送，由服务端按无权限丢弃
        /// </summary>
        /// <param name="channelNumber">通道号，取值 0x4000 到 0x4FFF</param>
        /// <param name="payload">应用数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>发送完成的任务</returns>
        /// <exception cref="ArgumentOutOfRangeException">通道号不合法</exception>
        /// <exception cref="ArgumentNullException">payload 为 null</exception>
        /// <exception cref="TurnClientException">分配已失效</exception>
        public Task SendChannelDataAsync(ushort channelNumber, byte[] payload, CancellationToken cancellationToken)
        {
            if (!ChannelData.IsValidChannelNumber(channelNumber))
            {
                throw new ArgumentOutOfRangeException(nameof(channelNumber), "通道号必须落在 0x4000 到 0x4FFF 之间");
            }
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfExpired();

            var frame = ChannelData.Encode(channelNumber, payload, _client.IsReliable);
            _client.SendFrame(frame);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 刷新分配生命周期；成功后按 allocation 层间隔重新挂一次性定时器
        /// </summary>
        /// <param name="lifetime">请求的生命周期，单位秒；为 null 时沿用当前值</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>刷新完成的任务</returns>
        /// <exception cref="TurnClientException">服务端拒绝、请求超时或分配已失效</exception>
        public async Task RefreshAsync(uint? lifetime, CancellationToken cancellationToken)
        {
            uint requested;
            lock (_sync)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }
                requested = lifetime.HasValue ? lifetime.Value : _lifetime;
            }

            var request = TurnMessage.CreateRefresh(requested);
            StunMessage response;
            try
            {
                response = await _client.SendAuthenticatedAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 分配层失败通常意味着服务端已删除分配：先标记失效并把重连交给 TurnClient，再让调用方看到异常
                FailRefresh(RefreshLayer.Allocation, ex);
                throw;
            }

            uint refreshed;
            if (TurnMessage.TryGetLifetime(response, out refreshed) && refreshed > 0)
            {
                lock (_sync)
                {
                    if (!_disposed)
                    {
                        _lifetime = refreshed;
                    }
                }
            }

            ArmAllocationTimer();
        }

        /// <summary>
        /// 释放分配：发 LIFETIME=0 的 Refresh；无论成败都停止全部定时器并标记失效
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>释放完成的任务</returns>
        /// <exception cref="TurnClientException">服务端拒绝或请求超时</exception>
        public async Task ReleaseAsync(CancellationToken cancellationToken)
        {
            try
            {
                var request = TurnMessage.CreateRefresh(0);
                await _client.SendAuthenticatedAsync(request, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // 本地必须无条件停表，服务端拒绝不改变客户端已释放的事实
                StopAllTimers();
                lock (_sync)
                {
                    _isExpired = true;
                }
            }
        }

        /// <summary>
        /// 重连后按已登记的表项重放 permission 与 channel；任一请求失败直接抛出，由调用方继续退避重试
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>重放完成的任务</returns>
        /// <exception cref="TurnClientException">服务端拒绝或请求超时</exception>
        public async Task RestoreAsync(CancellationToken cancellationToken)
        {
            IPEndPoint[] peers;
            KeyValuePair<ushort, IPEndPoint>[] channels;
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                var peerList = new List<IPEndPoint>(_permissions.Count);
                foreach (var entry in _permissions.Values)
                {
                    peerList.Add(entry.Peer);
                }
                peers = peerList.ToArray();

                var channelList = new List<KeyValuePair<ushort, IPEndPoint>>(_channels.Count);
                foreach (var pair in _channels)
                {
                    channelList.Add(new KeyValuePair<ushort, IPEndPoint>(pair.Key, pair.Value.Peer));
                }
                channels = channelList.ToArray();
            }

            for (var i = 0; i < peers.Length; i++)
            {
                var permission = TurnMessage.CreateCreatePermission(new[] { peers[i] });
                await _client.SendAuthenticatedAsync(permission, cancellationToken).ConfigureAwait(false);
            }

            for (var i = 0; i < channels.Length; i++)
            {
                var bind = TurnMessage.CreateChannelBind(channels[i].Key, channels[i].Value);
                await _client.SendAuthenticatedAsync(bind, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 重连重放成功后恢复可用状态，并按三层间隔重新挂全部定时器
        /// </summary>
        public void MarkRestored()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }
                _isExpired = false;
            }

            ArmAllocationTimer();
            ArmAllPermissionTimers();
            ArmAllChannelTimers();
        }

        /// <summary>
        /// 释放分配客户端：停止全部定时器，不发任何网络请求；重复调用为空操作
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
                _isExpired = true;
            }

            // 句柄释放放到锁外，避免定时器工厂的回调在锁内重新进入
            StopAllTimers();
        }

        /// <summary>
        /// 挂一次 allocation 层一次性定时器；新句柄先装好再释放旧的，避免续期窗口断档
        /// </summary>
        private void ArmAllocationTimer()
        {
            // 工厂可能被测试替换，回调也可能同步触发，因此必须在锁外创建
            var timer = _client.Schedule(_allocationRefreshInterval, OnAllocationTimer);

            IDisposable stale;
            var discard = false;
            lock (_sync)
            {
                stale = _allocationTimer;
                if (_disposed)
                {
                    _allocationTimer = null;
                    discard = true;
                }
                else
                {
                    _allocationTimer = timer;
                }
            }

            DisposeTimer(stale);
            if (discard)
            {
                DisposeTimer(timer);
            }
        }

        /// <summary>
        /// 为非 null 的指定通道号挂一次性续期定时器
        /// </summary>
        /// <param name="channelNumber">通道号</param>
        private void ArmChannelTimer(ushort channelNumber)
        {
            var timer = _client.Schedule(_channelRefreshInterval, () => OnChannelTimer(channelNumber));

            IDisposable stale = null;
            var discard = false;
            lock (_sync)
            {
                ChannelEntry entry;
                if (_disposed || !_channels.TryGetValue(channelNumber, out entry))
                {
                    discard = true;
                }
                else
                {
                    stale = entry.Timer;
                    entry.Timer = timer;
                }
            }

            DisposeTimer(stale);
            if (discard)
            {
                DisposeTimer(timer);
            }
        }

        /// <summary>
        /// 为指定对端 IP 挂一次性续期定时器；同一 IP 始终只保留一个句柄
        /// </summary>
        /// <param name="address">对端 IP</param>
        private void ArmPermissionTimer(IPAddress address)
        {
            var timer = _client.Schedule(_permissionRefreshInterval, () => OnPermissionTimer(address));

            IDisposable stale = null;
            var discard = false;
            lock (_sync)
            {
                PermissionEntry entry;
                if (_disposed || !_permissions.TryGetValue(address, out entry))
                {
                    discard = true;
                }
                else
                {
                    stale = entry.Timer;
                    entry.Timer = timer;
                }
            }

            DisposeTimer(stale);
            if (discard)
            {
                DisposeTimer(timer);
            }
        }

        /// <summary>
        /// 为所有已登记 permission 重新挂定时器
        /// </summary>
        private void ArmAllPermissionTimers()
        {
            IPAddress[] addresses;
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }
                addresses = new IPAddress[_permissions.Count];
                _permissions.Keys.CopyTo(addresses, 0);
            }

            for (var i = 0; i < addresses.Length; i++)
            {
                ArmPermissionTimer(addresses[i]);
            }
        }

        /// <summary>
        /// 为所有已登记 channel 重新挂定时器
        /// </summary>
        private void ArmAllChannelTimers()
        {
            ushort[] channels;
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }
                channels = new ushort[_channels.Count];
                _channels.Keys.CopyTo(channels, 0);
            }

            for (var i = 0; i < channels.Length; i++)
            {
                ArmChannelTimer(channels[i]);
            }
        }

        /// <summary>
        /// 登记 permission；已存在同 IP 时只更新代表地址
        /// </summary>
        /// <param name="peer">对端地址</param>
        /// <returns>新增登记返回 true</returns>
        private bool RegisterPermission(IPEndPoint peer)
        {
            var address = peer.Address;
            lock (_sync)
            {
                if (_disposed)
                {
                    return false;
                }

                PermissionEntry entry;
                if (_permissions.TryGetValue(address, out entry))
                {
                    // 端口不参与放行，保留最新地址只是为了续期请求里带一个具体端口
                    entry.Peer = peer;
                    return false;
                }

                _permissions[address] = new PermissionEntry { Peer = peer };
                return true;
            }
        }

        /// <summary>
        /// 登记 channel；同号重复绑定时只更新对端地址
        /// </summary>
        /// <param name="channelNumber">通道号</param>
        /// <param name="peer">对端地址</param>
        private void RegisterChannel(ushort channelNumber, IPEndPoint peer)
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                ChannelEntry entry;
                if (_channels.TryGetValue(channelNumber, out entry))
                {
                    entry.Peer = peer;
                    return;
                }

                _channels[channelNumber] = new ChannelEntry { Peer = peer };
            }
        }

        /// <summary>
        /// 分配层定时器入口：一次性定时器已经触发，异步刷新交给线程池，异常不逃出计时器线程
        /// </summary>
        private void OnAllocationTimer()
        {
            var ignored = Task.Run((Func<Task>)RefreshAllocationAsync);
        }

        /// <summary>
        /// 定时器驱动的分配续期；失败状态已由 <see cref="RefreshAsync"/> 登记，这里只吞掉异常
        /// </summary>
        /// <returns>续期任务</returns>
        private async Task RefreshAllocationAsync()
        {
            try
            {
                await RefreshAsync(null, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                P2PLogHelper.Debug("TurnAllocationClient", "分配续期失败：" + ex.GetType().Name);
            }
        }

        /// <summary>
        /// permission 层定时器入口
        /// </summary>
        /// <param name="address">对端 IP</param>
        private void OnPermissionTimer(IPAddress address)
        {
            var ignored = Task.Run((Func<Task>)(() => RefreshPermissionAsync(address)));
        }

        /// <summary>
        /// channel 层定时器入口
        /// </summary>
        /// <param name="channelNumber">通道号</param>
        private void OnChannelTimer(ushort channelNumber)
        {
            var ignored = Task.Run((Func<Task>)(() => RefreshChannelAsync(channelNumber)));
        }

        /// <summary>
        /// 续期一个 permission：成功后再挂下一次定时器，失败则整层失效
        /// </summary>
        /// <param name="address">对端 IP</param>
        /// <returns>续期任务</returns>
        private async Task RefreshPermissionAsync(IPAddress address)
        {
            IPEndPoint peer;
            lock (_sync)
            {
                if (_disposed || _isExpired)
                {
                    return;
                }

                PermissionEntry entry;
                if (!_permissions.TryGetValue(address, out entry))
                {
                    return;
                }
                peer = entry.Peer;
            }

            try
            {
                var request = TurnMessage.CreateCreatePermission(new[] { peer });
                await _client.SendAuthenticatedAsync(request, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                P2PLogHelper.Debug("TurnAllocationClient", "permission 续期失败：" + ex.GetType().Name);
                FailRefresh(RefreshLayer.Permission, ex);
                return;
            }

            ArmPermissionTimer(address);
        }

        /// <summary>
        /// 续期一个 channel：成功后再挂下一次定时器，失败则整层失效
        /// </summary>
        /// <param name="channelNumber">通道号</param>
        /// <returns>续期任务</returns>
        private async Task RefreshChannelAsync(ushort channelNumber)
        {
            IPEndPoint peer;
            lock (_sync)
            {
                if (_disposed || _isExpired)
                {
                    return;
                }

                ChannelEntry entry;
                if (!_channels.TryGetValue(channelNumber, out entry))
                {
                    return;
                }
                peer = entry.Peer;
            }

            try
            {
                var request = TurnMessage.CreateChannelBind(channelNumber, peer);
                await _client.SendAuthenticatedAsync(request, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                P2PLogHelper.Debug("TurnAllocationClient", "channel 续期失败：" + ex.GetType().Name);
                FailRefresh(RefreshLayer.Channel, ex);
                return;
            }

            ArmChannelTimer(channelNumber);
        }

        /// <summary>
        /// 某层续期失败：标记分配失效、停掉该层定时器并通知 <see cref="TurnClient"/> 重连
        /// </summary>
        /// <param name="layer">失败的层</param>
        /// <param name="error">失败原因</param>
        private void FailRefresh(RefreshLayer layer, Exception error)
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }
                _isExpired = true;
            }

            StopLayerTimers(layer);
            _client.OnRefreshFailed(this, error);
        }

        /// <summary>
        /// 停止指定层的全部定时器
        /// </summary>
        /// <param name="layer">目标层</param>
        private void StopLayerTimers(RefreshLayer layer)
        {
            switch (layer)
            {
                case RefreshLayer.Allocation:
                    StopAllocationTimer();
                    break;
                case RefreshLayer.Permission:
                    StopPermissionTimers();
                    break;
                case RefreshLayer.Channel:
                    StopChannelTimers();
                    break;
            }
        }

        /// <summary>
        /// 停止 allocation 层定时器
        /// </summary>
        private void StopAllocationTimer()
        {
            IDisposable stale;
            lock (_sync)
            {
                stale = _allocationTimer;
                _allocationTimer = null;
            }

            DisposeTimer(stale);
        }

        /// <summary>
        /// 停止全部 permission 定时器，登记表保持不变以便重连后重放
        /// </summary>
        private void StopPermissionTimers()
        {
            var timers = new List<IDisposable>();
            lock (_sync)
            {
                foreach (var entry in _permissions.Values)
                {
                    if (entry.Timer != null)
                    {
                        timers.Add(entry.Timer);
                        entry.Timer = null;
                    }
                }
            }

            for (var i = 0; i < timers.Count; i++)
            {
                DisposeTimer(timers[i]);
            }
        }

        /// <summary>
        /// 停止全部 channel 定时器，登记表保持不变以便重连后重放
        /// </summary>
        private void StopChannelTimers()
        {
            var timers = new List<IDisposable>();
            lock (_sync)
            {
                foreach (var entry in _channels.Values)
                {
                    if (entry.Timer != null)
                    {
                        timers.Add(entry.Timer);
                        entry.Timer = null;
                    }
                }
            }

            for (var i = 0; i < timers.Count; i++)
            {
                DisposeTimer(timers[i]);
            }
        }

        /// <summary>
        /// 停止三层全部定时器
        /// </summary>
        private void StopAllTimers()
        {
            StopAllocationTimer();
            StopPermissionTimers();
            StopChannelTimers();
        }

        /// <summary>
        /// 分配失效或已释放时拒绝发送通道数据
        /// </summary>
        /// <exception cref="ObjectDisposedException">客户端已释放</exception>
        /// <exception cref="TurnClientException">分配已失效</exception>
        private void ThrowIfExpired()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }
                if (_isExpired)
                {
                    throw new TurnClientException(0, "TURN 分配已失效");
                }
            }
        }

        /// <summary>
        /// 释放定时器句柄；已释放或为 null 时忽略
        /// </summary>
        /// <param name="timer">定时器句柄</param>
        private static void DisposeTimer(IDisposable timer)
        {
            if (timer == null)
            {
                return;
            }

            try
            {
                timer.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // 一次性定时器可能已自行结束，重复释放按空操作处理
            }
        }

        /// <summary>
        /// 续期失败所属的层
        /// </summary>
        private enum RefreshLayer
        {
            /// <summary>
            /// 分配层
            /// </summary>
            Allocation,

            /// <summary>
            /// 权限层
            /// </summary>
            Permission,

            /// <summary>
            /// 通道层
            /// </summary>
            Channel
        }

        /// <summary>
        /// permission 登记项
        /// </summary>
        private sealed class PermissionEntry
        {
            /// <summary>
            /// 代表对端地址，端口只用于续期请求
            /// </summary>
            public IPEndPoint Peer;

            /// <summary>
            /// 一次性续期定时器句柄
            /// </summary>
            public IDisposable Timer;
        }

        /// <summary>
        /// channel 登记项
        /// </summary>
        private sealed class ChannelEntry
        {
            /// <summary>
            /// 绑定的对端地址
            /// </summary>
            public IPEndPoint Peer;

            /// <summary>
            /// 一次性续期定时器句柄
            /// </summary>
            public IDisposable Timer;
        }
    }
}