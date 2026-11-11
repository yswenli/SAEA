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
*文件名： TurnServer
*版本号： v26.11.5.1
*唯一标识：006c2cd7-b4f0-424e-8483-f0161b76ad67
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 00:20:00
*描述：TURN 服务端分配、中继与权限处理
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 00:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TURN 服务端分配、中继与权限处理
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

using SAEA.P2P.Common;
using SAEA.P2P.Stun;
using SAEA.P2P.StunServer;
using SAEA.P2P.Turn;

namespace SAEA.P2P.TurnServer
{
    /// <summary>
    /// TURN 服务端：在 STUN 监听端口上分派 TURN 请求与 ChannelData，按分配维护独立的 UDP 中继 socket。
    /// 客户端面的监听 socket 只处理 STUN/TURN 报文与 ChannelData；中继流量走每个分配自己的裸 socket，
    /// 因此中继数据报的源地址始终等于 XOR-RELAYED-ADDRESS。
    /// </summary>
    public sealed class TurnServer : IDisposable
    {
        /// <summary>
        /// UDP 协议号
        /// </summary>
        public const byte UdpTransport = 17;

        /// <summary>
        /// TCP 协议号
        /// </summary>
        public const byte TcpTransport = 6;

        /// <summary>
        /// UDP 单包最大长度；中继接收缓冲按此分配
        /// </summary>
        private const int MaxPacketSize = 65535;

        /// <summary>
        /// 单个分配创建中继 socket 时的最大尝试次数，避免端口被系统占用时长时间循环
        /// </summary>
        private const int RelayBindAttempts = 16;

        /// <summary>
        /// 服务端配置
        /// </summary>
        private readonly TurnServerOptions _options;

        /// <summary>
        /// 时钟
        /// </summary>
        private readonly ITurnClock _clock;

        /// <summary>
        /// 临时凭据服务，供信令侧复用同一份共享密钥与认证域
        /// </summary>
        private readonly TurnCredentialService _credentials;

        /// <summary>
        /// 鉴权上下文
        /// </summary>
        private readonly TurnAuthContext _auth;

        /// <summary>
        /// 分配表
        /// </summary>
        private readonly TurnAllocationTable _allocations = new TurnAllocationTable();

        /// <summary>
        /// 中继端口分配器
        /// </summary>
        private readonly RelayPortAllocator _ports;

        /// <summary>
        /// 保护释放状态的同步对象
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 创建 TURN 服务端；监听由外部的 STUN 服务端提供，本类只负责报文处理
        /// </summary>
        /// <param name="options">服务端配置</param>
        /// <exception cref="ArgumentNullException">options 为 null</exception>
        /// <exception cref="ArgumentException">必填配置缺失</exception>
        /// <exception cref="ArgumentOutOfRangeException">数值配置越界</exception>
        public TurnServer(TurnServerOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            options.Validate();
            _options = options;
            _clock = options.Clock;
            _credentials = new TurnCredentialService(options.SharedSecret, options.Realm, options.CredentialTtlSeconds, TurnCredentialService.DefaultSeparator, () => _clock.UtcNow);

            // nonce 与凭据用同一份共享密钥，客户端地址参与 HMAC，换源地址即失效
            var nonces = new TurnNonceService(options.SharedSecret, options.NonceLifetimeSeconds, () => _clock.UtcNow);
            _auth = new TurnAuthContext(_credentials, nonces);
            _ports = new RelayPortAllocator(options.RelayPortMin, options.RelayPortMax);
        }

        /// <summary>
        /// 临时凭据服务；信令服务器用它为客户端签发凭据
        /// </summary>
        public TurnCredentialService CredentialService
        {
            get { return _credentials; }
        }

        /// <summary>
        /// 鉴权上下文
        /// </summary>
        public TurnAuthContext AuthContext
        {
            get { return _auth; }
        }

        /// <summary>
        /// 当前分配数量
        /// </summary>
        public int AllocationCount
        {
            get { return _allocations.Count; }
        }

        /// <summary>
        /// 处理一条非 Binding 的 STUN/TURN 报文；由 STUN 服务端的分派点调用
        /// </summary>
        /// <param name="request">请求或指示</param>
        /// <param name="remoteEndPoint">客户端来源端点</param>
        /// <param name="receivedOn">收到报文的监听端点</param>
        /// <returns>需要回给客户端的字节；不响应时为 null</returns>
        public byte[] HandleRequest(StunMessage request, IPEndPoint remoteEndPoint, StunServerEndpoint receivedOn)
        {
            if (_disposed || request == null || remoteEndPoint == null || receivedOn == null)
            {
                return null;
            }

            var now = _clock.UtcNow;

            // Send 指示没有事务，也不回应答；权限不足时静默丢弃
            if (request.Method == StunMessageType.Send)
            {
                if (request.Class == StunClass.Indication)
                {
                    HandleSendIndication(request, remoteEndPoint, receivedOn, now);
                }
                return null;
            }

            if (request.Class != StunClass.Request)
            {
                return null;
            }

            int errorCode;
            IReadOnlyList<ushort> unknownAttributes;
            if (!TurnMessage.TryValidateRequest(request, out errorCode, out unknownAttributes))
            {
                return BuildError(request, errorCode, null, unknownAttributes, null);
            }

            var auth = _auth.Authenticate(request, remoteEndPoint);
            if (!auth.Success)
            {
                return BuildChallenge(request, auth);
            }

            var key = auth.IntegrityKey;
            switch (request.Method)
            {
                case StunMessageType.Allocate:
                    return HandleAllocate(request, remoteEndPoint, receivedOn, auth, now);
                case StunMessageType.Refresh:
                    return HandleRefresh(request, remoteEndPoint, receivedOn, key, now);
                case StunMessageType.CreatePermission:
                    return HandleCreatePermission(request, remoteEndPoint, receivedOn, key, now);
                case StunMessageType.ChannelBind:
                    return HandleChannelBind(request, remoteEndPoint, receivedOn, key, now);
                default:
                    return BuildError(request, TurnErrorCode.BadRequest, null, null, key);
            }
        }

        /// <summary>
        /// 处理一条原始数据报；首字节落在 0x40 到 0x4F 时按 ChannelData 解析，其余交给 STUN 服务端
        /// </summary>
        /// <param name="data">数据报内容</param>
        /// <param name="remoteEndPoint">客户端来源端点</param>
        /// <param name="receivedOn">收到数据报的监听端点</param>
        /// <returns>已处理返回 true，STUN 服务端不应继续解析</returns>
        public bool HandleDatagram(ReadOnlyMemory<byte> data, IPEndPoint remoteEndPoint, StunServerEndpoint receivedOn)
        {
            if (_disposed || remoteEndPoint == null || receivedOn == null)
            {
                return false;
            }

            var span = data.Span;
            if (span.Length < ChannelData.HeaderLength)
            {
                return false;
            }

            // STUN 首字节高两位为 0；只有 ChannelData 落在 0x40 到 0x4F，其余复用区间静默丢弃
            var first = span[0];
            if (first < 0x40 || first > 0x4F)
            {
                return false;
            }

            ushort channelNumber;
            ReadOnlySpan<byte> payload;
            if (!ChannelData.TryDecode(span, out channelNumber, out payload))
            {
                return true;
            }

            var allocation = FindAllocation(remoteEndPoint, receivedOn);
            if (allocation == null)
            {
                return true;
            }

            var now = _clock.UtcNow;
            TurnChannel channel;
            if (!allocation.TryGetChannel(channelNumber, now, out channel))
            {
                return true;
            }

            if (!allocation.IsPermitted(channel.Peer, now))
            {
                allocation.AddDroppedDatagram();
                return true;
            }

            SendRelay(allocation, channel.Peer, new ReadOnlyMemory<byte>(payload.ToArray()));
            return true;
        }

        /// <summary>
        /// 移除并释放全部过期分配；也可由外部定时调用
        /// </summary>
        public void PurgeExpired()
        {
            var removed = _allocations.RemoveExpired(_clock.UtcNow);
            for (var i = 0; i < removed.Count; i++)
            {
                ReleaseAllocation(removed[i]);
            }
        }

        /// <summary>
        /// 释放全部分配与中继 socket；可重复调用
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
            }

            var removed = _allocations.Clear();
            for (var i = 0; i < removed.Count; i++)
            {
                ReleaseAllocation(removed[i]);
            }
        }

        /// <summary>
        /// 处理 Allocate：校验传输与地址族，检查配额后创建分配与中继 socket
        /// </summary>
        /// <param name="request">请求</param>
        /// <param name="remoteEndPoint">客户端来源端点</param>
        /// <param name="receivedOn">监听端点</param>
        /// <param name="auth">鉴权结果</param>
        /// <param name="now">当前时间</param>
        /// <returns>应答字节</returns>
        private byte[] HandleAllocate(StunMessage request, IPEndPoint remoteEndPoint, StunServerEndpoint receivedOn, TurnAuthResult auth, DateTimeOffset now)
        {
            var key = auth.IntegrityKey;

            byte protocol;
            if (!TurnMessage.TryGetRequestedTransport(request, out protocol))
            {
                return BuildError(request, TurnErrorCode.BadRequest, null, null, key);
            }

            if (protocol == TcpTransport)
            {
                // TCP 中继依赖 RFC 6062，只允许 TCP 或 TLS 控制连接，本轮暂不提供
                return BuildError(request, TurnErrorCode.UnsupportedTransportProtocol, "TCP relay is not available on UDP control connections", null, key);
            }

            if (protocol != UdpTransport)
            {
                return BuildError(request, TurnErrorCode.UnsupportedTransportProtocol, null, null, key);
            }

            TurnAddressFamily requestedFamily;
            if (TurnMessage.TryGetRequestedAddressFamily(request, out requestedFamily))
            {
                var relayFamily = _options.RelayAddress.AddressFamily == AddressFamily.InterNetwork
                    ? TurnAddressFamily.IPv4
                    : TurnAddressFamily.IPv6;
                if (requestedFamily != relayFamily)
                {
                    return BuildError(request, TurnErrorCode.AddressFamilyNotSupported, null, null, key);
                }
            }

            TurnAllocation existing;
            if (_allocations.TryGet(remoteEndPoint, receivedOn.LocalEndPoint, protocol, out existing))
            {
                return BuildError(request, TurnErrorCode.AllocationMismatch, null, null, key);
            }

            PurgeExpired();

            if (_allocations.Count >= _options.MaxAllocations
                || _allocations.CountByUser(auth.Username) >= _options.MaxAllocationsPerUser)
            {
                return BuildError(request, TurnErrorCode.AllocationQuotaReached, null, null, key);
            }

            var lifetime = ResolveLifetime(request);

            Socket relaySocket;
            int relayPort;
            if (!TryCreateRelaySocket(out relaySocket, out relayPort))
            {
                return BuildError(request, TurnErrorCode.InsufficientCapacity, "No relay port available", null, key);
            }

            var relayedEndPoint = new IPEndPoint(_options.RelayAddress, relayPort);
            var allocation = new TurnAllocation(
                remoteEndPoint,
                receivedOn.LocalEndPoint,
                protocol,
                auth.Username,
                relayedEndPoint,
                lifetime,
                key,
                now);
            allocation.RelaySocket = relaySocket;
            allocation.ControlEndpoint = receivedOn;

            if (!_allocations.TryAdd(allocation))
            {
                relaySocket.Dispose();
                _ports.Release(relayPort);
                return BuildError(request, TurnErrorCode.AllocationMismatch, null, null, key);
            }

            StartRelayLoop(allocation);

            var transactionId = request.TransactionId.ToArray();
            var attributes = new List<StunAttribute>
            {
                StunAddressCodec.EncodeAddress(StunAttributeType.XorRelayedAddress, relayedEndPoint, true, transactionId),
                StunAddressCodec.EncodeAddress(StunAttributeType.XorMappedAddress, remoteEndPoint, true, transactionId),
                StunAttributeCodec.CreateUInt32(StunAttributeType.Lifetime, lifetime)
            };
            return BuildSuccess(request, attributes, key);
        }

        /// <summary>
        /// 处理 Refresh：LIFETIME 为 0 时立即删除分配，其余按上限截断后刷新
        /// </summary>
        /// <param name="request">请求</param>
        /// <param name="remoteEndPoint">客户端来源端点</param>
        /// <param name="receivedOn">监听端点</param>
        /// <param name="key">长期密钥</param>
        /// <param name="now">当前时间</param>
        /// <returns>应答字节</returns>
        private byte[] HandleRefresh(StunMessage request, IPEndPoint remoteEndPoint, StunServerEndpoint receivedOn, byte[] key, DateTimeOffset now)
        {
            TurnAllocation allocation;
            if (!_allocations.TryGetAny(remoteEndPoint, receivedOn.LocalEndPoint, out allocation))
            {
                return BuildError(request, TurnErrorCode.AllocationMismatch, null, null, key);
            }

            uint requested;
            var hasLifetime = TurnMessage.TryGetLifetime(request, out requested);
            if (hasLifetime && requested == 0)
            {
                RemoveAllocation(allocation);
                return BuildSuccess(request, new[]
                {
                    StunAttributeCodec.CreateUInt32(StunAttributeType.Lifetime, 0)
                }, key);
            }

            var lifetime = hasLifetime ? Math.Min(requested, _options.MaxLifetimeSeconds) : _options.DefaultLifetimeSeconds;
            allocation.Refresh(lifetime, now);
            return BuildSuccess(request, new[]
            {
                StunAttributeCodec.CreateUInt32(StunAttributeType.Lifetime, lifetime)
            }, key);
        }

        /// <summary>
        /// 处理 CreatePermission：为请求里每个 XOR-PEER-ADDRESS 创建或刷新权限
        /// </summary>
        /// <param name="request">请求</param>
        /// <param name="remoteEndPoint">客户端来源端点</param>
        /// <param name="receivedOn">监听端点</param>
        /// <param name="key">长期密钥</param>
        /// <param name="now">当前时间</param>
        /// <returns>应答字节</returns>
        private byte[] HandleCreatePermission(StunMessage request, IPEndPoint remoteEndPoint, StunServerEndpoint receivedOn, byte[] key, DateTimeOffset now)
        {
            TurnAllocation allocation;
            if (!_allocations.TryGetAny(remoteEndPoint, receivedOn.LocalEndPoint, out allocation))
            {
                return BuildError(request, TurnErrorCode.AllocationMismatch, null, null, key);
            }

            var attributes = request.GetAll(StunAttributeType.XorPeerAddress);
            if (attributes.Count == 0)
            {
                return BuildError(request, TurnErrorCode.BadRequest, null, null, key);
            }

            var peers = new List<IPEndPoint>(attributes.Count);
            for (var i = 0; i < attributes.Count; i++)
            {
                IPEndPoint peer;
                if (!StunAddressCodec.TryDecodeAddress(attributes[i], request.TransactionId.Span, out peer))
                {
                    return BuildError(request, TurnErrorCode.BadRequest, null, null, key);
                }

                if (!IsSameFamily(peer.Address, allocation.RelayedEndPoint.Address))
                {
                    return BuildError(request, TurnErrorCode.PeerAddressFamilyMismatch, null, null, key);
                }

                peers.Add(peer);
            }

            for (var i = 0; i < peers.Count; i++)
            {
                allocation.AddPermission(peers[i], now);
            }

            return BuildSuccess(request, new StunAttribute[0], key);
        }

        /// <summary>
        /// 处理 ChannelBind：绑定通道并同时刷新对应权限
        /// </summary>
        /// <param name="request">请求</param>
        /// <param name="remoteEndPoint">客户端来源端点</param>
        /// <param name="receivedOn">监听端点</param>
        /// <param name="key">长期密钥</param>
        /// <param name="now">当前时间</param>
        /// <returns>应答字节</returns>
        private byte[] HandleChannelBind(StunMessage request, IPEndPoint remoteEndPoint, StunServerEndpoint receivedOn, byte[] key, DateTimeOffset now)
        {
            TurnAllocation allocation;
            if (!_allocations.TryGetAny(remoteEndPoint, receivedOn.LocalEndPoint, out allocation))
            {
                return BuildError(request, TurnErrorCode.AllocationMismatch, null, null, key);
            }

            ushort channelNumber;
            if (!TurnMessage.TryGetChannelNumber(request, out channelNumber))
            {
                return BuildError(request, TurnErrorCode.BadRequest, null, null, key);
            }

            IPEndPoint peer;
            if (!TurnMessage.TryGetPeerAddress(request, out peer))
            {
                return BuildError(request, TurnErrorCode.BadRequest, null, null, key);
            }

            if (!IsSameFamily(peer.Address, allocation.RelayedEndPoint.Address))
            {
                return BuildError(request, TurnErrorCode.PeerAddressFamilyMismatch, null, null, key);
            }

            if (!allocation.BindChannel(channelNumber, peer, now))
            {
                return BuildError(request, TurnErrorCode.BadRequest, "Channel number is already bound or under quarantine", null, key);
            }

            return BuildSuccess(request, new StunAttribute[0], key);
        }

        /// <summary>
        /// 处理 Send 指示：有权限才转发，否则丢弃并计数
        /// </summary>
        /// <param name="request">指示</param>
        /// <param name="remoteEndPoint">客户端来源端点</param>
        /// <param name="receivedOn">监听端点</param>
        /// <param name="now">当前时间</param>
        private void HandleSendIndication(StunMessage request, IPEndPoint remoteEndPoint, StunServerEndpoint receivedOn, DateTimeOffset now)
        {
            var allocation = FindAllocation(remoteEndPoint, receivedOn);
            if (allocation == null)
            {
                return;
            }

            IPEndPoint peer;
            if (!TurnMessage.TryGetPeerAddress(request, out peer))
            {
                return;
            }

            ReadOnlyMemory<byte> payload;
            if (!TurnMessage.TryGetData(request, out payload))
            {
                return;
            }

            if (!IsSameFamily(peer.Address, allocation.RelayedEndPoint.Address))
            {
                allocation.AddDroppedDatagram();
                return;
            }

            if (!allocation.IsPermitted(peer, now))
            {
                allocation.AddDroppedDatagram();
                return;
            }

            SendRelay(allocation, peer, payload);
        }

        /// <summary>
        /// 按客户端与控制端点查分配，不限定中继传输
        /// </summary>
        /// <param name="clientEndPoint">客户端来源端点</param>
        /// <param name="receivedOn">监听端点</param>
        /// <returns>分配；不存在时为 null</returns>
        private TurnAllocation FindAllocation(IPEndPoint clientEndPoint, StunServerEndpoint receivedOn)
        {
            TurnAllocation allocation;
            if (_allocations.TryGetAny(clientEndPoint, receivedOn.LocalEndPoint, out allocation))
            {
                return allocation;
            }
            return null;
        }

        /// <summary>
        /// 解析请求的生命周期，按服务端上限截断
        /// </summary>
        /// <param name="request">请求</param>
        /// <returns>生效的生命周期，单位秒</returns>
        private uint ResolveLifetime(StunMessage request)
        {
            uint requested;
            if (!TurnMessage.TryGetLifetime(request, out requested) || requested == 0)
            {
                return _options.DefaultLifetimeSeconds;
            }

            return Math.Min(requested, _options.MaxLifetimeSeconds);
        }

        /// <summary>
        /// 从中继端口区间取一个可绑定的 UDP socket
        /// </summary>
        /// <param name="socket">绑定的 socket；失败时为 null</param>
        /// <param name="port">绑定的端口；失败时为 0</param>
        /// <returns>成功返回 true</returns>
        private bool TryCreateRelaySocket(out Socket socket, out int port)
        {
            socket = null;
            port = 0;

            var attempts = Math.Min(_ports.AvailableCount, RelayBindAttempts);
            for (var i = 0; i < attempts; i++)
            {
                int candidate;
                if (!_ports.TryAllocate(out candidate))
                {
                    return false;
                }

                var created = new Socket(_options.RelayAddress.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                try
                {
                    created.Bind(new IPEndPoint(_options.RelayAddress, candidate));
                }
                catch (SocketException)
                {
                    created.Dispose();
                    _ports.Release(candidate);
                    continue;
                }

                socket = created;
                port = candidate;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 启动分配的中继接收循环
        /// </summary>
        /// <param name="allocation">分配</param>
        private void StartRelayLoop(TurnAllocation allocation)
        {
            var ignored = Task.Run(() => RelayLoopAsync(allocation));
        }

        /// <summary>
        /// 中继接收循环：读到一个数据报就按通道或权限转给客户端，socket 关闭时自然结束
        /// </summary>
        /// <param name="allocation">分配</param>
        /// <returns>循环结束的任务</returns>
        private async Task RelayLoopAsync(TurnAllocation allocation)
        {
            var buffer = new byte[MaxPacketSize];
            var relaySocket = allocation.RelaySocket;
            if (relaySocket == null)
            {
                return;
            }

            var anyEndPoint = relaySocket.AddressFamily == AddressFamily.InterNetworkV6
                ? new IPEndPoint(IPAddress.IPv6Any, 0)
                : new IPEndPoint(IPAddress.Any, 0);

            while (true)
            {
                SocketReceiveFromResult result;
                try
                {
                    result = await relaySocket.ReceiveFromAsync(
                        new ArraySegment<byte>(buffer),
                        SocketFlags.None,
                        anyEndPoint).ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException)
                {
                    return;
                }

                if (result.ReceivedBytes <= 0)
                {
                    continue;
                }

                var peer = result.RemoteEndPoint as IPEndPoint;
                if (peer == null)
                {
                    continue;
                }

                OnRelayDatagram(allocation, peer, new ReadOnlyMemory<byte>(buffer, 0, result.ReceivedBytes));
            }
        }

        /// <summary>
        /// 处理从中继 socket 收到的数据：优先按通道回 ChannelData，其次按权限回 Data 指示
        /// </summary>
        /// <param name="allocation">分配</param>
        /// <param name="peer">对端地址</param>
        /// <param name="payload">中继数据</param>
        private void OnRelayDatagram(TurnAllocation allocation, IPEndPoint peer, ReadOnlyMemory<byte> payload)
        {
            try
            {
                var now = _clock.UtcNow;
                TurnChannel channel;
                if (allocation.TryGetChannelByPeer(peer, now, out channel))
                {
                    if (!ReserveBytes(allocation, payload.Length))
                    {
                        return;
                    }

                    var channelData = ChannelData.Encode(channel.ChannelNumber, payload.Span, false);
                    SendToClient(allocation, channelData);
                    return;
                }

                if (!allocation.IsPermitted(peer, now))
                {
                    allocation.AddDroppedDatagram();
                    return;
                }

                if (!ReserveBytes(allocation, payload.Length))
                {
                    return;
                }

                var indication = TurnMessage.CreateData(peer, payload.ToArray());
                SendToClient(allocation, indication.ToArray());
            }
            catch (Exception ex)
            {
                P2PLogHelper.Debug("TurnServer", "处理中继数据失败：" + ex.Message);
            }
        }

        /// <summary>
        /// 检查并累加中继字节配额；超出上限时丢弃并计数
        /// </summary>
        /// <param name="allocation">分配</param>
        /// <param name="count">本次字节数</param>
        /// <returns>仍在配额内返回 true</returns>
        private bool ReserveBytes(TurnAllocation allocation, int count)
        {
            if (_options.MaxBytes > 0 && allocation.ByteCount + count > _options.MaxBytes)
            {
                allocation.AddDroppedDatagram();
                return false;
            }

            allocation.AddBytes(count);
            return true;
        }

        /// <summary>
        /// 通过中继 socket 把数据发给对端
        /// </summary>
        /// <param name="allocation">分配</param>
        /// <param name="peer">对端地址</param>
        /// <param name="payload">中继数据</param>
        private void SendRelay(TurnAllocation allocation, IPEndPoint peer, ReadOnlyMemory<byte> payload)
        {
            if (!ReserveBytes(allocation, payload.Length))
            {
                return;
            }

            var socket = allocation.RelaySocket;
            if (socket == null)
            {
                return;
            }

            try
            {
                var task = socket.SendToAsync(
                    new ArraySegment<byte>(payload.ToArray()),
                    SocketFlags.None,
                    peer);
                Observe(task);
            }
            catch (Exception ex)
            {
                P2PLogHelper.Debug("TurnServer", "中继发送失败：" + ex.Message);
            }
        }

        /// <summary>
        /// 把 ChannelData 或 Data 指示发回客户端控制端点
        /// </summary>
        /// <param name="allocation">分配</param>
        /// <param name="data">待发送字节</param>
        private void SendToClient(TurnAllocation allocation, byte[] data)
        {
            var endpoint = allocation.ControlEndpoint;
            if (endpoint == null)
            {
                return;
            }

            try
            {
                Observe(endpoint.SendAsync(allocation.ClientEndPoint, data));
            }
            catch (Exception ex)
            {
                P2PLogHelper.Debug("TurnServer", "回送客户端失败：" + ex.Message);
            }
        }

        /// <summary>
        /// 观察后台发送任务，仅记录失败，不阻塞调用线程
        /// </summary>
        /// <param name="task">发送任务</param>
        private static void Observe(Task task)
        {
            if (task == null || task.IsCompleted)
            {
                return;
            }

            task.ContinueWith(
                t =>
                {
                    var ignored = t.Exception;
                },
                TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <summary>
        /// 移除并释放一个分配
        /// </summary>
        /// <param name="allocation">分配</param>
        private void RemoveAllocation(TurnAllocation allocation)
        {
            TurnAllocation removed;
            if (_allocations.TryRemove(allocation.ClientEndPoint, allocation.LocalEndPoint, allocation.Transport, out removed))
            {
                ReleaseAllocation(removed);
            }
        }

        /// <summary>
        /// 归还中继端口并关闭中继 socket
        /// </summary>
        /// <param name="allocation">分配</param>
        private void ReleaseAllocation(TurnAllocation allocation)
        {
            if (allocation == null)
            {
                return;
            }

            _ports.Release(allocation.RelayedEndPoint.Port);
            allocation.Dispose();
        }

        /// <summary>
        /// 判断两个地址是否同族；v4-mapped v6 先归一化为 v4
        /// </summary>
        /// <param name="left">左地址</param>
        /// <param name="right">右地址</param>
        /// <returns>同族返回 true</returns>
        private static bool IsSameFamily(IPAddress left, IPAddress right)
        {
            return Normalize(left).AddressFamily == Normalize(right).AddressFamily;
        }

        /// <summary>
        /// 把 v4-mapped v6 地址归一化为 v4
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
        /// 构造成功应答并按长期密钥签名
        /// </summary>
        /// <param name="request">请求</param>
        /// <param name="attributes">成功属性</param>
        /// <param name="key">长期密钥；为 null 时不签名</param>
        /// <returns>应答字节</returns>
        private byte[] BuildSuccess(StunMessage request, IEnumerable<StunAttribute> attributes, byte[] key)
        {
            var response = new StunMessage(request.Method, StunClass.SuccessResponse, request.TransactionId.ToArray());
            var list = new List<StunAttribute>(attributes);
            AddSoftware(list);
            response.SetAttributes(list);

            if (key != null && key.Length > 0)
            {
                StunIntegrity.Append(response, key);
            }

            return response.ToArray();
        }

        /// <summary>
        /// 构造错误应答；带未知属性列表并按需签名
        /// </summary>
        /// <param name="request">请求</param>
        /// <param name="code">错误码</param>
        /// <param name="reason">原因短语；为 null 时取默认值</param>
        /// <param name="unknownAttributes">未知属性列表；为 null 时不携带</param>
        /// <param name="key">长期密钥；为 null 时不签名</param>
        /// <returns>应答字节</returns>
        private byte[] BuildError(StunMessage request, int code, string reason, IEnumerable<ushort> unknownAttributes, byte[] key)
        {
            var response = new StunMessage(request.Method, StunClass.ErrorResponse, request.TransactionId.ToArray());
            var attributes = new List<StunAttribute>
            {
                TurnErrorCode.Create(code, reason)
            };
            AddUnknownAttributes(attributes, unknownAttributes);
            AddSoftware(attributes);
            response.SetAttributes(attributes);

            if (key != null && key.Length > 0)
            {
                StunIntegrity.Append(response, key);
            }

            return response.ToArray();
        }

        /// <summary>
        /// 构造 401/438 挑战应答；挑战不应签名
        /// </summary>
        /// <param name="request">请求</param>
        /// <param name="auth">鉴权结果</param>
        /// <returns>应答字节</returns>
        private byte[] BuildChallenge(StunMessage request, TurnAuthResult auth)
        {
            var response = new StunMessage(request.Method, StunClass.ErrorResponse, request.TransactionId.ToArray());
            var attributes = new List<StunAttribute>();
            for (var i = 0; i < auth.ChallengeAttributes.Count; i++)
            {
                attributes.Add(auth.ChallengeAttributes[i]);
            }
            AddSoftware(attributes);
            response.SetAttributes(attributes);
            return response.ToArray();
        }

        /// <summary>
        /// 追加 UNKNOWN-ATTRIBUTES 属性
        /// </summary>
        /// <param name="attributes">目标属性列表</param>
        /// <param name="unknownAttributes">属性类型列表</param>
        private static void AddUnknownAttributes(List<StunAttribute> attributes, IEnumerable<ushort> unknownAttributes)
        {
            if (unknownAttributes == null)
            {
                return;
            }

            var value = new List<byte>();
            foreach (var type in unknownAttributes)
            {
                value.Add((byte)(type >> 8));
                value.Add((byte)type);
            }

            if (value.Count > 0)
            {
                attributes.Add(new StunAttribute(StunAttributeType.UnknownAttributes, value.ToArray()));
            }
        }

        /// <summary>
        /// 追加 SOFTWARE 属性；未配置时跳过
        /// </summary>
        /// <param name="attributes">目标属性列表</param>
        private void AddSoftware(List<StunAttribute> attributes)
        {
            if (!string.IsNullOrEmpty(_options.Software))
            {
                attributes.Add(new StunAttribute(StunAttributeType.Software, System.Text.Encoding.UTF8.GetBytes(_options.Software)));
            }
        }
    }
}