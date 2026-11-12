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
*修改时间：2026/11/12 11:48:57
*修改人： yswenli
*版本号： v26.11.5.1
*描述：UDP 中继端口绑定失败时不再归还端口池，与 TCP 中继对齐
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
        /// TCP 中继监听的排队上限；中继连接通常一条分配只有少数几条，不需要大 backlog
        /// </summary>
        private const int TcpRelayBacklog = 16;

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
        /// TCP 中继连接表，跨分配共用；UDP 分配不会用到
        /// </summary>
        private readonly TurnConnectionTable _connections = new TurnConnectionTable();

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

            return ProcessRequest(request, new UdpTurnControlChannel(receivedOn, remoteEndPoint), out _);
        }

        /// <summary>
        /// 处理一条来自流式控制连接的 STUN/TURN 报文，应答写回同一条连接
        /// </summary>
        /// <param name="request">请求或指示</param>
        /// <param name="control">控制连接</param>
        /// <returns>true 表示这条控制连接已被接管为数据连接，会话循环不能再按 TURN 报文继续读取</returns>
        internal bool HandleStreamRequest(StunMessage request, ITurnControlChannel control)
        {
            if (_disposed || request == null || control == null)
            {
                return false;
            }

            bool takeOver;
            var response = ProcessRequest(request, control, out takeOver);
            if (response != null)
            {
                control.Send(response);
            }
            return takeOver;
        }

        /// <summary>
        /// 处理一条已解析的 STUN/TURN 报文；UDP 与流式控制连接共用这条路径
        /// </summary>
        /// <param name="request">请求或指示</param>
        /// <param name="control">控制连接</param>
        /// <param name="takeOver">true 表示处理完成后控制连接要转作数据连接</param>
        /// <returns>需要回给客户端的字节；不响应时为 null</returns>
        private byte[] ProcessRequest(StunMessage request, ITurnControlChannel control, out bool takeOver)
        {
            takeOver = false;
            var remoteEndPoint = control.RemoteEndPoint;
            var now = _clock.UtcNow;

            // Send 指示没有事务，也不回应答；权限不足时静默丢弃
            if (request.Method == StunMessageType.Send)
            {
                if (request.Class == StunClass.Indication)
                {
                    HandleSendIndication(request, control, now);
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
                    return HandleAllocate(request, control, auth, now);
                case StunMessageType.Refresh:
                    return HandleRefresh(request, control, key, now);
                case StunMessageType.CreatePermission:
                    return HandleCreatePermission(request, control, key, now);
                case StunMessageType.ChannelBind:
                    return HandleChannelBind(request, control, key, now);
                case StunMessageType.Connect:
                    return HandleConnect(request, control, key, now);
                case StunMessageType.ConnectionBind:
                    {
                        // takeOver 只可能由 ConnectionBind 产生，单独接住再统一赋给输出参数
                        var bindTakeOver = false;
                        var response = HandleConnectionBind(request, control, key, now, out bindTakeOver);
                        takeOver = bindTakeOver;
                        return response;
                    }
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

            ProcessChannelData(span, new UdpTurnControlChannel(receivedOn, remoteEndPoint));
            return true;
        }

        /// <summary>
        /// 处理一条来自流式控制连接的 ChannelData
        /// </summary>
        /// <param name="data">完整 ChannelData 报文</param>
        /// <param name="control">控制连接</param>
        internal void HandleStreamChannelData(ReadOnlySpan<byte> data, ITurnControlChannel control)
        {
            if (_disposed || control == null || data.Length < ChannelData.HeaderLength)
            {
                return;
            }

            ProcessChannelData(data, control);
        }

        /// <summary>
        /// 按通道号把 ChannelData 解出的数据发往对端；通道不存在或无权限时静默丢弃
        /// </summary>
        /// <param name="span">完整 ChannelData 报文</param>
        /// <param name="control">控制连接</param>
        private void ProcessChannelData(ReadOnlySpan<byte> span, ITurnControlChannel control)
        {
            ushort channelNumber;
            ReadOnlySpan<byte> payload;
            if (!ChannelData.TryDecode(span, out channelNumber, out payload))
            {
                return;
            }

            var allocation = FindAllocation(control.RemoteEndPoint, control.LocalEndPoint);
            if (allocation == null)
            {
                return;
            }

            var now = _clock.UtcNow;
            TurnChannel channel;
            if (!allocation.TryGetChannel(channelNumber, now, out channel))
            {
                return;
            }

            if (!allocation.IsPermitted(channel.Peer, now))
            {
                allocation.AddDroppedDatagram();
                return;
            }

            SendRelay(allocation, channel.Peer, new ReadOnlyMemory<byte>(payload.ToArray()));
        }

        /// <summary>
        /// 释放某条控制连接上的全部分配；控制连接断开时由监听器调用，避免中继 socket 与端口泄漏
        /// </summary>
        /// <param name="control">已断开的控制连接</param>
        internal void ReleaseControlChannel(ITurnControlChannel control)
        {
            if (control == null)
            {
                return;
            }

            var snapshot = _allocations.Snapshot();
            for (var i = 0; i < snapshot.Count; i++)
            {
                var allocation = snapshot[i];
                if (!ReferenceEquals(allocation.ControlChannel, control))
                {
                    continue;
                }

                TurnAllocation removed;
                if (_allocations.TryRemove(allocation.ClientEndPoint, allocation.LocalEndPoint, allocation.Transport, out removed))
                {
                    ReleaseAllocation(removed);
                }
            }
        }

        /// <summary>
        /// 移除并释放全部过期分配与超时未绑定的中继连接；也可由外部定时调用
        /// </summary>
        public void PurgeExpired()
        {
            var now = _clock.UtcNow;
            var removed = _allocations.RemoveExpired(now);
            for (var i = 0; i < removed.Count; i++)
            {
                ReleaseAllocation(removed[i]);
            }

            // 没等到 ConnectionBind 的连接不属于任何 socket 生命周期，只能在锁外单独关闭
            var expired = _connections.RemoveExpired(now, _options.ConnectionBindTimeout);
            for (var i = 0; i < expired.Count; i++)
            {
                DisposeSocket(expired[i].PeerSocket);
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

            // 兜底：各分配的释放已经带走名下的连接，这里只处理归属不明或竞态中残留的记录
            CloseAllConnectionRecords();
        }

        /// <summary>
        /// 处理 Allocate：校验传输与地址族，检查配额后创建分配与中继端点（UDP socket 或 TCP 监听）
        /// </summary>
        /// <param name="request">请求</param>
        /// <param name="control">控制连接</param>
        /// <param name="auth">鉴权结果</param>
        /// <param name="now">当前时间</param>
        /// <returns>应答字节</returns>
        private byte[] HandleAllocate(StunMessage request, ITurnControlChannel control, TurnAuthResult auth, DateTimeOffset now)
        {
            var key = auth.IntegrityKey;
            var remoteEndPoint = control.RemoteEndPoint;
            var localEndPoint = control.LocalEndPoint;

            byte protocol;
            if (!TurnMessage.TryGetRequestedTransport(request, out protocol))
            {
                return BuildError(request, TurnErrorCode.BadRequest, null, null, key);
            }

            if (protocol == TcpTransport && !control.IsStream)
            {
                // TCP 中继的数据面要接管一条流连接，UDP 控制连接没有可接管的字节流
                return BuildError(request, TurnErrorCode.UnsupportedTransportProtocol, "TCP relay requires a TCP or TLS control connection", null, key);
            }

            if (protocol != UdpTransport && protocol != TcpTransport)
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
            if (_allocations.TryGet(remoteEndPoint, localEndPoint, protocol, out existing))
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
            var isTcp = protocol == TcpTransport;

            // UDP 分配要一个裸 UDP socket 直接收发数据报；TCP 分配要一个监听 socket 等对端连入
            Socket relaySocket = null;
            Socket relayListener = null;
            int relayPort;
            if (isTcp)
            {
                if (!TryCreateTcpRelayListener(out relayListener, out relayPort))
                {
                    return BuildError(request, TurnErrorCode.InsufficientCapacity, "No relay port available", null, key);
                }
            }
            else if (!TryCreateRelaySocket(out relaySocket, out relayPort))
            {
                return BuildError(request, TurnErrorCode.InsufficientCapacity, "No relay port available", null, key);
            }

            var relayedEndPoint = new IPEndPoint(_options.RelayAddress, relayPort);
            var allocation = new TurnAllocation(
                remoteEndPoint,
                localEndPoint,
                protocol,
                auth.Username,
                relayedEndPoint,
                lifetime,
                key,
                now);
            if (!isTcp)
            {
                // TCP 分配的中继连接来自 TurnTcpRelay 的 accept，不挂 UDP 中继 socket
                allocation.RelaySocket = relaySocket;
            }
            allocation.ControlChannel = control;

            if (!_allocations.TryAdd(allocation))
            {
                // 加分配失败时监听与端口都要原样退回，否则每次重试都漏一个端口
                if (isTcp)
                {
                    DisposeSocket(relayListener);
                }
                else
                {
                    DisposeSocket(relaySocket);
                }
                _ports.Release(relayPort);
                return BuildError(request, TurnErrorCode.AllocationMismatch, null, null, key);
            }

            if (isTcp)
            {
                // TCP 中继没有报文接收循环，监听由 TurnTcpRelay 接管，等 ConnectionBind 再对接数据连接
                allocation.TcpRelay = new TurnTcpRelay(allocation, _connections, relayListener, _options.ConnectionBindTimeout);
                // 回调里捕获本次分配：对端连入时才知道该把 ConnectionAttempt 发到哪条控制连接
                allocation.TcpRelay.ConnectionAttemptNotifier = record => SendConnectionAttempt(allocation, record);
                allocation.TcpRelay.Start();
            }
            else
            {
                StartRelayLoop(allocation);
            }

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
        /// 处理 Connect：服务端主动连向对端，成功后在控制连接上回 CONNECTION-ID 等待 ConnectionBind
        /// </summary>
        /// <param name="request">请求</param>
        /// <param name="control">控制连接</param>
        /// <param name="key">长期密钥</param>
        /// <param name="now">当前时间</param>
        /// <returns>应答字节</returns>
        private byte[] HandleConnect(StunMessage request, ITurnControlChannel control, byte[] key, DateTimeOffset now)
        {
            if (!control.IsStream)
            {
                // Connect 的中继连接最终要交给另一条流连接承载，UDP 控制连接无法承载 TCP 中继
                return BuildError(request, TurnErrorCode.BadRequest, "Connect requires a TCP or TLS control connection", null, key);
            }

            TurnAllocation allocation;
            if (!_allocations.TryGet(control.RemoteEndPoint, control.LocalEndPoint, TcpTransport, out allocation))
            {
                return BuildError(request, TurnErrorCode.AllocationMismatch, null, null, key);
            }

            IPEndPoint peer;
            if (!TurnMessage.TryGetPeerAddress(request, out peer))
            {
                return BuildError(request, TurnErrorCode.BadRequest, null, null, key);
            }

            // 同一对端只允许一条连接，重复 Connect 会让两条数据泵争抢同一 peer socket
            if (_connections.ContainsPeer(allocation, peer))
            {
                return BuildError(request, TurnErrorCode.ConnectionAlreadyExists, null, null, key);
            }

            Socket socket = null;
            try
            {
                socket = new Socket(peer.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                if (!TryConnect(socket, peer, _options.RelayConnectTimeout))
                {
                    DisposeSocket(socket);
                    return BuildError(request, TurnErrorCode.ConnectionTimeoutOrFailure, null, null, key);
                }
            }
            catch (Exception ex)
            {
                // 建链失败与超时对客户端是同一结果，socket 必须就地关掉，不能留给 GC
                DisposeSocket(socket);
                P2PLogHelper.Debug("TurnServer", "连接对端失败：" + ex.GetType().Name);
                return BuildError(request, TurnErrorCode.ConnectionTimeoutOrFailure, null, null, key);
            }

            var record = new TurnConnectionRecord();
            record.Allocation = allocation;
            record.PeerEndPoint = peer;
            record.PeerSocket = socket;
            record.IsTls = control.IsTls;
            record.CreatedAt = now;
            record.Bound = false;

            try
            {
                _connections.Add(record);
            }
            catch (InvalidOperationException ex)
            {
                // 连接号耗尽时无法再登记，直接关掉已建好的连接，避免它成为没人管的句柄
                DisposeSocket(socket);
                P2PLogHelper.Debug("TurnServer", "登记 TCP 中继连接失败：" + ex.Message);
                return BuildError(request, TurnErrorCode.InsufficientCapacity, null, null, key);
            }

            return BuildSuccess(request, new[]
            {
                StunAttributeCodec.CreateUInt32(StunAttributeType.ConnectionId, record.ConnectionId)
            }, key);
        }

        /// <summary>
        /// 处理 ConnectionBind：把客户端另开的数据连接与指定连接 ID 的对端 socket 对接
        /// </summary>
        /// <param name="request">请求</param>
        /// <param name="control">数据连接（处理完成后会被接管）</param>
        /// <param name="key">长期密钥</param>
        /// <param name="now">当前时间</param>
        /// <param name="takeOver">true 表示这条流连接要转作数据连接</param>
        /// <returns>应答字节</returns>
        private byte[] HandleConnectionBind(StunMessage request, ITurnControlChannel control, byte[] key, DateTimeOffset now, out bool takeOver)
        {
            takeOver = false;

            // 数据面必须跑在另一条流连接上；UDP 控制连接没有可接管的字节流
            if (!control.IsStream)
            {
                return BuildError(request, TurnErrorCode.BadRequest, "ConnectionBind requires a TCP or TLS control connection", null, key);
            }

            uint connectionId;
            // 先解 CONNECTION-ID：定位不到连接，后面所有校验都无从做起
            if (!TurnMessage.TryGetConnectionId(request, out connectionId))
            {
                return BuildError(request, TurnErrorCode.BadRequest, null, null, key);
            }

            TurnConnectionRecord record;
            // 未绑定的连接会被定时清理，未知 ID 说明已回收或从未存在，按报文格式错误回
            if (!_connections.TryGet(connectionId, out record))
            {
                return BuildError(request, TurnErrorCode.BadRequest, null, null, key);
            }

            // 一条中继连接只能绑定一次：重复绑定会把两路数据泵接到同一个 peer socket 上
            if (record.Bound)
            {
                return BuildError(request, TurnErrorCode.BadRequest, null, null, key);
            }

            // 分配可能已释放而记录尚未清理，此时绑定会让数据泵挂到已关闭的中继上
            TurnAllocation live;
            if (!_allocations.TryGet(record.Allocation.ClientEndPoint, record.Allocation.LocalEndPoint, record.Allocation.Transport, out live)
                || !ReferenceEquals(live, record.Allocation))
            {
                return BuildError(request, TurnErrorCode.BadRequest, null, null, key);
            }

            // 数据连接必须来自控制连接的对端 IP：端口必然不同（客户端另开一条），只比 IP
            if (!IsSameAddress(record.Allocation.ControlChannel.RemoteEndPoint, control.RemoteEndPoint))
            {
                return BuildError(request, TurnErrorCode.Forbidden, null, null, key);
            }

            // 加密层必须与控制连接一致：TLS 分配不允许被明文连接接管，反之亦然
            if (record.IsTls != control.IsTls)
            {
                return BuildError(request, TurnErrorCode.Forbidden, null, null, key);
            }

            var relay = record.Allocation.TcpRelay;
            if (relay == null)
            {
                // 没有 TCP 中继端说明这条记录不属于 TCP 分配，正常流程不应出现
                return BuildError(request, TurnErrorCode.BadRequest, null, null, key);
            }

            // 先把后置状态与应答准备好，绑定成功就不再回到 TURN 报文循环
            record.Bound = true;
            takeOver = true;
            var response = BuildSuccess(request, new StunAttribute[0], key);

            try
            {
                relay.Bind(record, control);
            }
            catch (Exception ex)
            {
                // 绑定失败必须让会话继续按控制连接处理，否则这条连接会被误当成数据连接而无法再解析报文
                record.Bound = false;
                takeOver = false;
                P2PLogHelper.Debug("TurnServer", "绑定 TCP 中继数据连接失败：" + ex.GetType().Name);
                return BuildError(request, TurnErrorCode.BadRequest, null, null, key);
            }

            return response;
        }

        /// <summary>
        /// 处理 Refresh：LIFETIME 为 0 时立即删除分配，其余按上限截断后刷新
        /// </summary>
        /// <param name="request">请求</param>
        /// <param name="control">控制连接</param>
        /// <param name="key">长期密钥</param>
        /// <param name="now">当前时间</param>
        /// <returns>应答字节</returns>
        private byte[] HandleRefresh(StunMessage request, ITurnControlChannel control, byte[] key, DateTimeOffset now)
        {
            TurnAllocation allocation;
            if (!_allocations.TryGetAny(control.RemoteEndPoint, control.LocalEndPoint, out allocation))
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
        /// <param name="control">控制连接</param>
        /// <param name="key">长期密钥</param>
        /// <param name="now">当前时间</param>
        /// <returns>应答字节</returns>
        private byte[] HandleCreatePermission(StunMessage request, ITurnControlChannel control, byte[] key, DateTimeOffset now)
        {
            TurnAllocation allocation;
            if (!_allocations.TryGetAny(control.RemoteEndPoint, control.LocalEndPoint, out allocation))
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
        /// <param name="control">控制连接</param>
        /// <param name="key">长期密钥</param>
        /// <param name="now">当前时间</param>
        /// <returns>应答字节</returns>
        private byte[] HandleChannelBind(StunMessage request, ITurnControlChannel control, byte[] key, DateTimeOffset now)
        {
            TurnAllocation allocation;
            if (!_allocations.TryGetAny(control.RemoteEndPoint, control.LocalEndPoint, out allocation))
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
        /// <param name="control">控制连接</param>
        /// <param name="now">当前时间</param>
        private void HandleSendIndication(StunMessage request, ITurnControlChannel control, DateTimeOffset now)
        {
            var allocation = FindAllocation(control.RemoteEndPoint, control.LocalEndPoint);
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
        /// <param name="localEndPoint">控制连接的服务端本地端点</param>
        /// <returns>分配；不存在时为 null</returns>
        private TurnAllocation FindAllocation(IPEndPoint clientEndPoint, IPEndPoint localEndPoint)
        {
            TurnAllocation allocation;
            if (_allocations.TryGetAny(clientEndPoint, localEndPoint, out allocation))
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
                    // 绑定失败说明该端口被外部占用：归还后下次分配很可能又抽到它并同样失败，
                    // 因此把端口丢弃在池外（不归还），直接换下一个候选
                    created.Dispose();
                    continue;
                }
                catch (Exception)
                {
                    // 不是端口冲突类失败，端口没有问题，归还后再抛给上层
                    created.Dispose();
                    _ports.Release(candidate);
                    throw;
                }

                socket = created;
                port = candidate;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 从中继端口区间取一个可监听的 TCP socket
        /// </summary>
        /// <param name="listener">已开始监听的 socket；失败时为 null</param>
        /// <param name="port">绑定的端口；失败时为 0</param>
        /// <returns>成功返回 true</returns>
        private bool TryCreateTcpRelayListener(out Socket listener, out int port)
        {
            listener = null;
            port = 0;

            var attempts = Math.Min(_ports.AvailableCount, RelayBindAttempts);
            for (var i = 0; i < attempts; i++)
            {
                int candidate;
                if (!_ports.TryAllocate(out candidate))
                {
                    return false;
                }

                Socket created = null;
                try
                {
                    created = new Socket(_options.RelayAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    created.Bind(new IPEndPoint(_options.RelayAddress, candidate));
                    created.Listen(TcpRelayBacklog);
                }
                catch (SocketException)
                {
                    // 绑定或监听失败说明该端口被外部占用：归还后下次分配很可能又抽到它并同样失败，
                    // 因此把端口丢弃在池外（不归还），直接换下一个候选
                    if (created != null)
                    {
                        created.Dispose();
                    }
                    continue;
                }
                catch (Exception)
                {
                    // 不是端口冲突类失败，端口没有问题，归还后再抛给上层
                    if (created != null)
                    {
                        created.Dispose();
                    }
                    _ports.Release(candidate);
                    throw;
                }

                listener = created;
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
                var control = allocation.ControlChannel;
                if (control == null)
                {
                    return;
                }

                TurnChannel boundChannel;
                if (allocation.TryGetChannelByPeer(peer, now, out boundChannel))
                {
                    if (!ReserveBytes(allocation, payload.Length))
                    {
                        return;
                    }

                    // 流式控制连接需要按 4 字节补齐，UDP 下补齐字节会被对端当作新报文首字节
                    var channelData = ChannelData.Encode(boundChannel.ChannelNumber, payload.Span, control.IsStream);
                    SendToClient(control, channelData);
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
                SendToClient(control, indication.ToArray());
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
        /// 通过中继 socket 把数据发给对端；TCP 分配没有 UDP 中继 socket，直接丢弃
        /// </summary>
        /// <param name="allocation">分配</param>
        /// <param name="peer">对端地址</param>
        /// <param name="payload">中继数据</param>
        private void SendRelay(TurnAllocation allocation, IPEndPoint peer, ReadOnlyMemory<byte> payload)
        {
            var socket = allocation.RelaySocket;
            if (socket == null)
            {
                // TCP 分配的中继数据走 ConnectionBind 建立的字节泵，不会发 Data 指示
                return;
            }

            if (!ReserveBytes(allocation, payload.Length))
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
        /// 在分配的控制连接上下发 ConnectionAttempt，把对端主动连入时分配的连接 ID 告知客户端
        /// </summary>
        /// <param name="allocation">分配</param>
        /// <param name="record">新到的中继连接</param>
        private void SendConnectionAttempt(TurnAllocation allocation, TurnConnectionRecord record)
        {
            if (allocation == null || record == null)
            {
                return;
            }

            var control = allocation.ControlChannel;
            if (control == null || !control.IsStream)
            {
                // UDP 控制连接没有可接管的字节流，TCP 中继不可能落到这里，正常流程不应出现
                P2PLogHelper.Debug("TurnServer", "控制连接不支持 ConnectionAttempt");
                return;
            }

            try
            {
                var message = TurnMessage.CreateConnectionAttempt(record.ConnectionId, record.PeerEndPoint);
                StunIntegrity.Append(message, allocation.GetIntegrityKey());
                control.Send(message.ToArray());
            }
            catch (Exception ex)
            {
                // 对端已连入，通知失败不能影响中继本身，也不向 accept 线程外抛
                P2PLogHelper.Debug("TurnServer", "下发 ConnectionAttempt 失败：" + ex.GetType().Name);
            }
        }

        /// <summary>
        /// 把 ChannelData 或 Data 指示发回客户端控制连接
        /// </summary>
        /// <param name="control">控制连接</param>
        /// <param name="data">待发送字节</param>
        private void SendToClient(ITurnControlChannel control, byte[] data)
        {
            if (control == null || data == null)
            {
                return;
            }

            try
            {
                control.Send(data);
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
        /// 关闭中继 socket 与 TCP 监听后归还中继端口
        /// </summary>
        /// <param name="allocation">分配</param>
        private void ReleaseAllocation(TurnAllocation allocation)
        {
            if (allocation == null)
            {
                return;
            }

            // 先释放再归还端口：端口一旦回到池里就可能立刻被新分配复用，旧监听还占着会让新分配绑定失败
            allocation.Dispose();
            _ports.Release(allocation.RelayedEndPoint.Port);
        }

        /// <summary>
        /// 摘除并关闭连接表里残留的全部对端连接；释放服务端时兜底用，可重复调用
        /// </summary>
        private void CloseAllConnectionRecords()
        {
            var records = _connections.RemoveAll();
            for (var i = 0; i < records.Count; i++)
            {
                DisposeSocket(records[i].PeerSocket);
                records[i].PeerSocket = null;
            }
        }

        /// <summary>
        /// 等待一次 TCP 建链完成；超时或失败返回 false
        /// </summary>
        /// <param name="socket">已创建但尚未连接的 socket</param>
        /// <param name="peer">对端地址</param>
        /// <param name="timeout">等待上限</param>
        /// <returns>连上返回 true</returns>
        private static bool TryConnect(Socket socket, IPEndPoint peer, TimeSpan timeout)
        {
            var connectTask = socket.ConnectAsync(peer);

            // 等待期间不持有任何锁：分配表、连接表与同一控制连接上的其它请求都能继续被处理
            try
            {
                if (!connectTask.Wait(timeout))
                {
                    // 超时后调用方会关掉 socket，被打断的连接任务会以异常结束，这里挂上观察避免无人处理
                    Observe(connectTask);
                    return false;
                }
            }
            catch (AggregateException)
            {
                return false;
            }

            return connectTask.Status == TaskStatus.RanToCompletion;
        }

        /// <summary>
        /// 判断两个端点的 IP 是否相同；端口不参与比较
        /// </summary>
        /// <param name="left">左端点</param>
        /// <param name="right">右端点</param>
        /// <returns>IP 相同返回 true</returns>
        private static bool IsSameAddress(IPEndPoint left, IPEndPoint right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            return Normalize(left.Address).Equals(Normalize(right.Address));
        }

        /// <summary>
        /// 关闭 socket 并忽略重复关闭
        /// </summary>
        /// <param name="socket">socket；可为 null</param>
        private static void DisposeSocket(Socket socket)
        {
            if (socket == null)
            {
                return;
            }

            try
            {
                socket.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException ex)
            {
                P2PLogHelper.Debug("TurnServer", "关闭中继 socket 失败：" + ex.SocketErrorCode);
            }
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