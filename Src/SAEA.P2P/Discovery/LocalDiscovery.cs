/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.P2P.Discovery
*文件名： LocalDiscovery
*版本号： v26.4.23.1
*唯一标识：fb0291b1-d664-45e9-b1e1-62a3bce5f36c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 16:16:36
*描述：LocalDiscovery接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 17:05:13
*修改人： yswenli
*版本号： v26.11.5.1
*描述：改用无信令发现报文与 HMAC 认证，补充 ICE 参数、候选广播与节点更新通知
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using SAEA.P2P.Common;
using SAEA.P2P.Core;
using SAEA.P2P.Ice;
using SAEA.P2P.Signaling;
using SAEA.Sockets;
using SAEA.Sockets.Core.Udp;
using SAEA.Sockets.Model;

namespace SAEA.P2P.Discovery
{
    /// <summary>
    /// 局域网节点发现：周期以组播与广播发出带 ICE 参数和候选的报文，并从收到的报文来源端点维护已发现节点
    /// </summary>
    public class LocalDiscovery : IDisposable
    {
        private readonly string _nodeId;

        private readonly LocalDiscoveryOptions _options;

        private readonly IPAddress _multicastAddress;

        private readonly ConcurrentDictionary<string, DiscoveredNode> _discoveredNodes =
            new ConcurrentDictionary<string, DiscoveredNode>();

        private readonly object _candidateLock = new object();

        private readonly List<Candidate> _candidates = new List<Candidate>();

        private SignalingCandidate[] _candidateSnapshot = new SignalingCandidate[0];

        private string _nodeName;

        private string[] _services;

        private string _ufrag;

        private string _pwd;

        private string _role;

        private ulong _tiebreaker;

        private SaeaUdpClientSocket _socket;

        private Timer _timer;

        private IPEndPoint _localEndPoint;

        // 0 表示未启动，1 表示已启动；用 Interlocked 保证 Start 与 Stop 幂等
        private int _started;

        private bool _disposed;

        /// <summary>
        /// 首次收到某个节点的报文时触发，参数为该节点的信息
        /// </summary>
        public event Action<DiscoveredNode> OnNodeDiscovered;

        /// <summary>
        /// 再次收到已发现节点的报文时触发，参数为刷新后的节点信息
        /// </summary>
        public event Action<DiscoveredNode> OnNodeUpdated;

        /// <summary>
        /// 节点因超时被清理时触发，参数为被移除的节点
        /// </summary>
        public event Action<DiscoveredNode> OnNodeExpired;

        /// <summary>
        /// 已发现节点集合，键为节点标识
        /// </summary>
        public ConcurrentDictionary<string, DiscoveredNode> DiscoveredNodes
        {
            get { return _discoveredNodes; }
        }

        /// <summary>
        /// 接收发现报文时实际绑定的本地端点，未启动时为 null
        /// </summary>
        public IPEndPoint LocalEndPoint
        {
            get { return _localEndPoint; }
        }

        /// <summary>
        /// 创建发现实例，此时尚未占用端口
        /// </summary>
        /// <param name="nodeId">本节点标识，同时作为默认节点名称</param>
        /// <param name="port">发现报文使用的端口，默认 39655</param>
        /// <param name="multicastGroup">组播地址，默认 224.0.0.250</param>
        /// <param name="interval">周期补发间隔，单位毫秒，默认 5000</param>
        /// <param name="timeout">节点过期阈值，单位毫秒，默认 30000</param>
        public LocalDiscovery(string nodeId, int port = 39655, string multicastGroup = "224.0.0.250",
            int interval = 5000, int timeout = 30000)
            : this(nodeId, new LocalDiscoveryOptions
            {
                Port = port,
                MulticastGroup = multicastGroup,
                IntervalMilliseconds = interval,
                TimeoutMilliseconds = timeout
            })
        {
        }

        /// <summary>
        /// 按配置创建发现实例，此时尚未占用端口
        /// </summary>
        /// <param name="nodeId">本节点标识，同时作为默认节点名称</param>
        /// <param name="options">发现配置，构造后不应再修改</param>
        /// <exception cref="ArgumentException">节点标识为空、绑定地址不是 IPv4 或组播地址格式无效</exception>
        /// <exception cref="ArgumentNullException">配置为空</exception>
        /// <exception cref="ArgumentOutOfRangeException">端口、补发间隔或过期阈值越界</exception>
        public LocalDiscovery(string nodeId, LocalDiscoveryOptions options)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                throw new ArgumentException("节点标识不能为空", nameof(nodeId));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (options.Port < 1 || options.Port > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "发现端口必须在 1 到 65535 之间");
            }

            if (options.IntervalMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "周期补发间隔必须大于 0");
            }

            if (options.TimeoutMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "节点过期阈值必须大于 0");
            }

            if (options.BindAddress == null || options.BindAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException("绑定地址必须为 IPv4 地址", nameof(options));
            }

            IPAddress multicastAddress = null;
            if (!string.IsNullOrEmpty(options.MulticastGroup) && !IPAddress.TryParse(options.MulticastGroup, out multicastAddress))
            {
                throw new ArgumentException("组播地址格式无效：" + options.MulticastGroup, nameof(options));
            }

            _nodeId = nodeId;
            _nodeName = nodeId;
            _options = options;
            _multicastAddress = multicastAddress;
        }

        /// <summary>
        /// 设置对外广播的节点名称与服务列表，名称为 null 时回退为节点标识
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <param name="services">服务名列表，可为 null；内部保存副本，调用方后续修改不影响广播内容</param>
        public void SetNodeInfo(string nodeName, string[] services)
        {
            _nodeName = nodeName ?? _nodeId;
            _services = services == null ? null : (string[])services.Clone();
        }

        /// <summary>
        /// 设置对外广播的 ICE 参数，供对端收到发现报文后直接发起连接
        /// </summary>
        /// <param name="ufrag">ICE 用户名片段</param>
        /// <param name="pwd">ICE 口令</param>
        /// <param name="role">ICE 角色，取 controlling 或 controlled</param>
        /// <param name="tiebreaker">角色冲突裁决值</param>
        public void SetLocalIceParameters(string ufrag, string pwd, string role, ulong tiebreaker)
        {
            _ufrag = ufrag;
            _pwd = pwd;
            _role = role;
            _tiebreaker = tiebreaker;
        }

        /// <summary>
        /// 追加一个本端可直连候选并立即补发一次报文，已存在相同候选时不重复追加也不补发
        /// </summary>
        /// <param name="candidate">本端候选，非空</param>
        /// <exception cref="ArgumentNullException">候选为空</exception>
        public void AddLocalCandidate(Candidate candidate)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            var added = false;
            lock (_candidateLock)
            {
                var exists = false;
                foreach (var item in _candidates)
                {
                    if (IsSameCandidate(item, candidate))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    _candidates.Add(candidate);
                    _candidateSnapshot = BuildCandidateSnapshot(_candidates);
                    added = true;
                }
            }

            if (added)
            {
                AnnounceNow();
            }
        }

        /// <summary>
        /// 绑定发现端口、加入组播组并启动周期广播，已启动时直接返回
        /// </summary>
        /// <exception cref="ObjectDisposedException">实例已释放</exception>
        public void Start()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
            {
                return;
            }

            SaeaUdpClientSocket socket = null;
            Timer timer = null;
            try
            {
                var builder = SocketOptionBuilder.Instance
                    .SetSocket(SAEASocketType.Udp)
                    .UsePacket()
                    .SetIP(_options.BindAddress.ToString())
                    .SetPort(_options.Port)
                    .ReusePort()
                    .SetMultiCastHost(_options.MulticastGroup);

                if (_options.EnableBroadcast)
                {
                    builder = builder.UseBroadcast();
                }

                socket = SocketFactory.CreateClientSocket(builder.Build()) as SaeaUdpClientSocket;
                if (socket == null)
                {
                    throw new InvalidOperationException("无法创建 UDP 发现套接字");
                }

                socket.Bind(new IPEndPoint(_options.BindAddress, _options.Port));

                if (_multicastAddress != null)
                {
                    JoinMulticast(socket, _multicastAddress, _options.BindAddress);
                }

                socket.OnClientReceiveSpanFrom += OnReceiveDataSpanFrom;
                socket.StartReceive();

                _socket = socket;
                _localEndPoint = socket.LocalEndPoint;

                timer = new Timer(OnTimerTick, null, _options.IntervalMilliseconds, _options.IntervalMilliseconds);
                _timer = timer;

                // 所有权移交到字段后清空局部变量，避免下面的异常路径误释放正在使用的套接字与计时器
                socket = null;
                timer = null;

                AnnounceNow();

                P2PLogHelper.Info("LocalDiscovery",
                    "Started on " + _localEndPoint + ", multicast: " + _options.MulticastGroup);
            }
            catch
            {
                if (timer != null)
                {
                    try { timer.Dispose(); } catch { }
                }

                if (socket != null)
                {
                    try { socket.OnClientReceiveSpanFrom -= OnReceiveDataSpanFrom; } catch { }
                    try { socket.Dispose(); } catch { }
                }

                // 所有权已移交给字段后再失败时，局部变量为空，这里按字段再收一遍，避免留下半启动状态
                var currentTimer = Interlocked.Exchange(ref _timer, null);
                if (currentTimer != null)
                {
                    try { currentTimer.Dispose(); } catch { }
                }

                var currentSocket = Interlocked.Exchange(ref _socket, null);
                if (currentSocket != null)
                {
                    try { currentSocket.OnClientReceiveSpanFrom -= OnReceiveDataSpanFrom; } catch { }
                    try { currentSocket.Dispose(); } catch { }
                }

                _localEndPoint = null;
                Interlocked.Exchange(ref _started, 0);
                throw;
            }
        }

        /// <summary>
        /// 停止周期广播并断开套接字，可重复调用
        /// </summary>
        public void Stop()
        {
            if (Interlocked.Exchange(ref _started, 0) == 0)
            {
                return;
            }

            var timer = Interlocked.Exchange(ref _timer, null);
            if (timer != null)
            {
                try { timer.Dispose(); } catch { }
            }

            var socket = Interlocked.Exchange(ref _socket, null);
            _localEndPoint = null;

            if (socket != null)
            {
                try { socket.OnClientReceiveSpanFrom -= OnReceiveDataSpanFrom; } catch { }
                try { socket.Disconnect(); } catch { }
                try { socket.Dispose(); } catch { }
            }

            P2PLogHelper.Info("LocalDiscovery", "Stopped");
        }

        /// <summary>
        /// 立即发出一次发现报文，未启动时直接返回
        /// </summary>
        public void AnnounceNow()
        {
            var socket = _socket;
            if (socket == null)
            {
                return;
            }

            byte[] payload;
            try
            {
                payload = DiscoveryJsonCodec.Encode(BuildPacket(), _options.SharedKey);
            }
            catch (Exception ex)
            {
                P2PLogHelper.Error("LocalDiscovery", ErrorCode.DiscoveryFailed, "序列化发现报文失败", ex);
                return;
            }

            if (_multicastAddress != null)
            {
                SendTo(socket, new IPEndPoint(_multicastAddress, _options.Port), payload);
            }

            if (_options.EnableBroadcast)
            {
                SendTo(socket, new IPEndPoint(IPAddress.Broadcast, _options.Port), payload);
            }
        }

        /// <summary>
        /// 清理超过超时阈值的节点并触发 OnNodeExpired，可由调用方与内部计时器并发调用
        /// </summary>
        public void CleanupExpiredNodes()
        {
            foreach (var kvp in _discoveredNodes)
            {
                if (!kvp.Value.IsExpired(_options.TimeoutMilliseconds))
                {
                    continue;
                }

                DiscoveredNode removed;
                // TryRemove 决定触发权，多个并发清理里只有真正移除成功的那次发出通知
                if (!_discoveredNodes.TryRemove(kvp.Key, out removed))
                {
                    continue;
                }

                var handler = OnNodeExpired;
                if (handler != null)
                {
                    handler(removed);
                }

                P2PLogHelper.Debug("LocalDiscovery", "Node expired: " + kvp.Key);
            }
        }

        /// <summary>
        /// 停止发现并释放资源，可重复调用
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Stop();
        }

        /// <summary>
        /// 周期任务：补发一次发现报文并清理过期节点
        /// </summary>
        /// <param name="state">计时器状态，未使用</param>
        private void OnTimerTick(object state)
        {
            try
            {
                AnnounceNow();
                CleanupExpiredNodes();
            }
            catch (Exception ex)
            {
                // Timer 回调未捕获异常会终止进程，这里兜底记录
                P2PLogHelper.Error("LocalDiscovery", ErrorCode.DiscoveryFailed, "发现周期任务失败", ex);
            }
        }

        /// <summary>
        /// 就绪广播内容：本端标识、名称、服务、ICE 参数与候选快照
        /// </summary>
        /// <returns>待序列化的发现报文</returns>
        private DiscoveryPacket BuildPacket()
        {
            SignalingCandidate[] candidates;
            lock (_candidateLock)
            {
                candidates = _candidateSnapshot;
            }

            return new DiscoveryPacket
            {
                Version = DiscoveryJsonCodec.CurrentProtocolVersion,
                PeerId = _nodeId,
                NodeName = _nodeName,
                Services = _services == null ? null : (string[])_services.Clone(),
                Ufrag = _ufrag,
                Pwd = _pwd,
                Role = _role,
                Tiebreaker = _tiebreaker,
                TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Candidates = candidates
            };
        }

        /// <summary>
        /// 把 ICE 候选列表转成信令候选快照，未定义类型的候选跳过
        /// </summary>
        /// <param name="candidates">本端候选</param>
        /// <returns>可放入报文的信令候选数组</returns>
        private static SignalingCandidate[] BuildCandidateSnapshot(List<Candidate> candidates)
        {
            var result = new List<SignalingCandidate>(candidates.Count);
            foreach (var candidate in candidates)
            {
                try
                {
                    result.Add(SignalingCandidateMapper.ToSignaling(candidate));
                }
                catch (Exception ex)
                {
                    // 单个候选无法表达时只跳过它，不能让整次广播失败
                    P2PLogHelper.Warning("LocalDiscovery", "候选无法写入发现报文：" + candidate + "，" + ex.Message);
                }
            }
            return result.ToArray();
        }

        /// <summary>
        /// 判断两个候选是否是同一地址、端口与类型的重复项
        /// </summary>
        /// <param name="left">已有候选</param>
        /// <param name="right">待追加候选</param>
        /// <returns>完全等价返回 true</returns>
        private static bool IsSameCandidate(Candidate left, Candidate right)
        {
            return left.Transport == right.Transport
                && left.Component == right.Component
                && left.Type == right.Type
                && left.TcpType == right.TcpType
                && left.Port == right.Port
                && left.Address.Equals(right.Address);
        }

        /// <summary>
        /// 把收到的信令候选还原成 ICE 候选，非法项跳过
        /// </summary>
        /// <param name="source">报文中的候选，可为 null</param>
        /// <returns>还原出的候选；没有有效项时返回 null</returns>
        private static Candidate[] ToCandidates(SignalingCandidate[] source)
        {
            if (source == null || source.Length == 0)
            {
                return null;
            }

            var result = new List<Candidate>(source.Length);
            foreach (var item in source)
            {
                Candidate candidate;
                if (SignalingCandidateMapper.TryToCandidate(item, out candidate))
                {
                    result.Add(candidate);
                }
                else
                {
                    P2PLogHelper.Debug("LocalDiscovery", "忽略非法的发现候选：" + (item == null ? "null" : item.Address));
                }
            }

            return result.Count == 0 ? null : result.ToArray();
        }

        /// <summary>
        /// 加入组播组并打开组播回环；构造套接字时已按同一接口加入过，重复加入报错按已加入处理
        /// </summary>
        /// <param name="socket">已绑定的 UDP 客户端</param>
        /// <param name="group">组播地址</param>
        /// <param name="bindAddress">加入组播使用的本地接口地址</param>
        private static void JoinMulticast(SaeaUdpClientSocket socket, IPAddress group, IPAddress bindAddress)
        {
            socket.Socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);

            try
            {
                socket.Socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                    new MulticastOption(group, bindAddress));
            }
            catch (SocketException ex)
            {
                // 构造套接字时已按 SetMultiCastHost + SetIP 加入同一组：Windows 对重复加入返回
                // InvalidArgument，部分平台返回 AddressAlreadyInUse，两者都说明组播组已在位；
                // 其余错误（如地址非法）仍然抛出，避免组播静默失效
                if (ex.SocketErrorCode != SocketError.AddressAlreadyInUse
                    && ex.SocketErrorCode != SocketError.InvalidArgument)
                {
                    throw;
                }

                P2PLogHelper.Debug("LocalDiscovery", "组播组已加入：" + group);
            }
        }

        /// <summary>
        /// 向指定端点发送已编码的发现报文，单个目标失败不影响其余目标
        /// </summary>
        /// <param name="socket">发送用套接字</param>
        /// <param name="endPoint">目标端点</param>
        /// <param name="payload">已编码负载</param>
        private static void SendTo(SaeaUdpClientSocket socket, IPEndPoint endPoint, byte[] payload)
        {
            try
            {
                socket.SendAsync(endPoint, payload);
            }
            catch (Exception ex)
            {
                P2PLogHelper.Error("LocalDiscovery", ErrorCode.DiscoveryFailed, "发送发现报文失败：" + endPoint, ex);
            }
        }

        /// <summary>
        /// 接收发现报文：span 只在回调期间有效，先复制成数组再解析
        /// </summary>
        /// <param name="remoteEndPoint">数据报来源端点</param>
        /// <param name="data">报文内容</param>
        private void OnReceiveDataSpanFrom(IPEndPoint remoteEndPoint, ReadOnlySpan<byte> data)
        {
            if (remoteEndPoint == null || data.Length == 0)
            {
                return;
            }

            try
            {
                var payload = data.ToArray();

                DiscoveryPacket packet;
                string error;
                if (!DiscoveryJsonCodec.TryDecode(payload, _options.SharedKey, out packet, out error))
                {
                    P2PLogHelper.Debug("LocalDiscovery", "丢弃发现报文：" + error);
                    return;
                }

                if (string.IsNullOrEmpty(packet.PeerId) || packet.PeerId == _nodeId)
                {
                    return;
                }

                var isNew = false;
                var node = _discoveredNodes.GetOrAdd(packet.PeerId, id =>
                {
                    isNew = true;
                    return new DiscoveredNode { NodeId = id };
                });

                node.NodeName = string.IsNullOrEmpty(packet.NodeName) ? packet.PeerId : packet.NodeName;
                node.LocalAddress = new IPEndPoint(remoteEndPoint.Address, remoteEndPoint.Port);
                node.ProtocolVersion = packet.Version;
                node.Ufrag = packet.Ufrag;
                node.Pwd = packet.Pwd;
                node.Role = packet.Role;
                node.Tiebreaker = packet.Tiebreaker;
                node.Services = packet.Services == null ? null : (string[])packet.Services.Clone();
                node.Candidates = ToCandidates(packet.Candidates);
                node.Refresh();

                var handler = isNew ? OnNodeDiscovered : OnNodeUpdated;
                if (handler != null)
                {
                    handler(node);
                }

                if (isNew)
                {
                    P2PLogHelper.Info("LocalDiscovery", "Discovered node: " + packet.PeerId);
                }
            }
            catch (Exception ex)
            {
                P2PLogHelper.Error("LocalDiscovery", ErrorCode.DiscoveryFailed, "处理发现报文失败", ex);
            }
        }
    }
}