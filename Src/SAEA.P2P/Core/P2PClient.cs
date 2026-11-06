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
*命名空间：SAEA.P2P.Core
*文件名： P2PClient
*版本号： v26.4.23.1
*唯一标识：805c44a8-e97c-43fb-803c-2cdc38f99887
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 16:27:00
*描述：P2PClient接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:20:16
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using SAEA.Sockets;
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using SAEA.P2P.Builder;
using SAEA.P2P.Channel;
using SAEA.P2P.Common;
using SAEA.P2P.Discovery;
using SAEA.P2P.Model;
using SAEA.P2P.NAT;
using SAEA.P2P.Protocol;
using SAEA.P2P.Relay;
using SAEA.P2P.Security;

namespace SAEA.P2P.Core
{
    /// <summary>
    /// P2P 客户端，负责信令注册、NAT 探测、打洞、中继与局域网发现
    /// </summary>
    public class P2PClient
    {
        private P2POptions _options;
        private SaeaClientSocket _signalSocket;
        private P2PCoder _coder = new P2PCoder();
        
        private CryptoService _cryptoService;
        private AuthManager _authManager;
        private HolePuncher _holePuncher;
        private RelayManager _relayManager;
        private LocalDiscovery _localDiscovery;
        
        private NodeState _state = NodeState.Init;
        private ConcurrentDictionary<string, PeerSession> _peers = new ConcurrentDictionary<string, PeerSession>();
        private ConcurrentDictionary<string, NodeInfo> _nodes = new ConcurrentDictionary<string, NodeInfo>();
        
        private string _sessionId;
        
        /// <summary>
        /// 本节点标识，取自客户端配置
        /// </summary>
        public string NodeId => _options.NodeId;
        /// <summary>
        /// 当前节点状态
        /// </summary>
        public NodeState State => _state;
        /// <summary>
        /// 是否已注册或处于可用状态
        /// </summary>
        public bool IsConnected => _state == NodeState.Connected || _state == NodeState.Registered;
        
        /// <summary>
        /// 状态切换时触发，参数为旧状态与新状态
        /// </summary>
        public event Action<NodeState, NodeState> OnStateChanged;
        /// <summary>
        /// 向信令服务器注册成功时触发
        /// </summary>
        public event Action OnServerConnected;
        /// <summary>
        /// 与信令服务器断开时触发，参数为断开原因描述
        /// </summary>
        public event Action<string> OnServerDisconnected;
        /// <summary>
        /// 对端会话建立的通知事件，当前实现未触发
        /// </summary>
        public event Action<string, ChannelType> OnPeerConnected;
        /// <summary>
        /// 对端会话断开的通知事件，当前实现未触发
        /// </summary>
        public event Action<string, string> OnPeerDisconnected;
        /// <summary>
        /// 收到用户数据时触发，参数为来源节点标识与负载副本
        /// </summary>
        public event Action<string, byte[]> OnMessageReceived;
        /// <summary>
        /// 发生错误时触发，参数为错误码与错误描述
        /// </summary>
        public event Action<string, string> OnError;
        /// <summary>
        /// 局域网发现到其他节点时触发
        /// </summary>
        public event Action<DiscoveredNode> OnLocalNodeDiscovered;
        
        /// <summary>
        /// 已知节点集合，键为节点标识，由信令服务器的节点列表维护
        /// </summary>
        public ConcurrentDictionary<string, NodeInfo> KnownNodes => _nodes;
        /// <summary>
        /// 对端会话集合，键为对端节点标识
        /// </summary>
        public ConcurrentDictionary<string, PeerSession> PeerSessions => _peers;
        
        /// <summary>
        /// 按配置创建客户端，并按需初始化加密、鉴权、打洞、中继与本地发现组件
        /// </summary>
        /// <param name="options">客户端配置，不可为 null</param>
        public P2PClient(P2POptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            
            if (_options.Encryption.Enabled)
                _cryptoService = new CryptoService(_options.Encryption.Key);
            
            if (!string.IsNullOrEmpty(_options.NodeIdPassword))
                _authManager = new AuthManager(_options.NodeIdPassword, _cryptoService);
            
            if (_options.HolePunch.Enabled)
            {
                _holePuncher = new HolePuncher(
                    _options.HolePunch.Strategy,
                    _options.HolePunch.SyncTimeoutMs,
                    _options.HolePunch.MaxAttempts);
            }
            
            if (_options.Relay.Enabled)
                _relayManager = new RelayManager(_options.Relay.TimeoutMs, _options.Relay.Quota);
            
            if (_options.Discovery.EnableLocalDiscovery)
            {
                _localDiscovery = new LocalDiscovery(
                    _options.NodeId,
                    _options.Discovery.LocalDiscoveryPort,
                    _options.Discovery.MulticastAddress,
                    _options.Discovery.DiscoveryIntervalMs,
                    _options.Discovery.DiscoveryTimeoutMs);
                _localDiscovery.OnNodeDiscovered += node => OnLocalNodeDiscovered?.Invoke(node);
            }
            
            P2PLogHelper.SetLevel(_options.Logging.Level);
        }
        
        /// <summary>
        /// 连接信令服务器并发送注册请求，已注册或已连接时抛出 P2PException
        /// </summary>
        /// <returns>报文发送完成的任务，注册结果由事件异步通知</returns>
        public async Task ConnectAsync()
        {
            if (_state == NodeState.Connected || _state == NodeState.Registered)
                throw new P2PException(ErrorCode.RegisterFailed);
            
            SetState(NodeState.Connecting);
            
            if (string.IsNullOrEmpty(_options.ServerAddress))
            {
                SetState(NodeState.Connected);
                StartLocalDiscovery();
                return;
            }
            
            var option = SocketOptionBuilder.Instance
                .SetSocket(SAEASocketType.Tcp)
                .UsePacket()
                .SetIP(_options.ServerAddress)
                .SetPort(_options.ServerPort)
                .SetConnectTimeout(_options.Timeout.ConnectTimeoutMs)
                .Build();
            
            _signalSocket = SocketFactory.CreateClientSocket(option) as SaeaClientSocket;
            _signalSocket.OnClientReceiveSpan += OnSignalReceiveSpan;
            _signalSocket.OnDisconnected += (id, ex) =>
            {
                SetState(NodeState.Disconnected);
                OnServerDisconnected?.Invoke("Signal server disconnected");
            };
            _signalSocket.OnError += (id, ex) => OnError?.Invoke(ErrorCode.RegisterServerUnavailable, ex.Message);
            
            _signalSocket.Connect();
            
            if (!_signalSocket.Connected)
            {
                SetState(NodeState.Error);
                throw new P2PException(ErrorCode.RegisterServerUnavailable);
            }
            
            SetState(NodeState.Authenticating);
            
            var registerContent = Encoding.UTF8.GetBytes(_options.NodeId);
            var registerPacket = _coder.EncodeP2P(P2PMessageType.Register, registerContent);
            _signalSocket.SendAsync(registerPacket);
            
            P2PLogHelper.Info(NodeId, "Connecting to signal server");
            
            if (_localDiscovery != null)
                StartLocalDiscovery();
        }
        
        /// <summary>
        /// 同步等待 ConnectAsync 完成，异常由 AggregateException 包装抛出
        /// </summary>
        public void Connect()
        {
            ConnectAsync().Wait();
        }
        
        private void StartLocalDiscovery()
        {
            _localDiscovery?.Start();
            P2PLogHelper.Info(NodeId, "Local discovery started");
        }
        
        private void OnSignalReceiveSpan(ReadOnlySpan<byte> data)
        {
            using (var frames = _coder.DecodeP2P(data))
            {
                foreach (var frame in frames.Frames)
                {
                    ProcessSignalMessage(frame);
                }
            }
        }
        
        private void ProcessSignalMessage(ISocketProtocal protocol)
        {
            switch ((P2PMessageType)protocol.Type)
            {
                case P2PMessageType.RegisterAck:
                    ProcessRegisterAck(protocol.Content);
                    break;
                case P2PMessageType.NodeList:
                    ProcessNodeList(protocol.Content);
                    break;
                case P2PMessageType.AuthChallenge:
                    ProcessAuthChallenge(protocol.Content);
                    break;
                case P2PMessageType.AuthSuccess:
                    ProcessAuthSuccess();
                    break;
                case P2PMessageType.PunchReady:
                    ProcessPunchReady(protocol.Content);
                    break;
                case P2PMessageType.NatProbeAck:
                    ProcessNatProbeAck(protocol.Content);
                    break;
                case P2PMessageType.RelayAck:
                    ProcessRelayAck(protocol.Content);
                    break;
                case P2PMessageType.RelayData:
                    ProcessRelayData(protocol.Content);
                    break;
                case P2PMessageType.UserData:
                    ProcessUserData(protocol.Content);
                    break;
                case P2PMessageType.Heartbeat:
                    SendHeartbeatAck();
                    break;
            }
        }
        
        private void ProcessRegisterAck(ReadOnlyMemory<byte> content)
        {
            var text = P2PContent.GetString(content);
            var parts = text.Split('|');
            
            if (parts[0] == "OK")
            {
                _sessionId = parts.Length > 1 ? parts[1] : Guid.NewGuid().ToString("N");
                SetState(NodeState.Registered);
                OnServerConnected?.Invoke();
                P2PLogHelper.Info(NodeId, "Registered successfully");
                
                SendNatProbe();
            }
            else
            {
                SetState(NodeState.Error);
                var errorCode = parts.Length > 1 ? parts[1] : ErrorCode.RegisterFailed;
                OnError?.Invoke(errorCode, "Registration failed");
            }
        }
        
        private void ProcessAuthChallenge(ReadOnlyMemory<byte> content)
        {
            if (_authManager == null) return;
            
            var challenge = new AuthChallenge
            {
                ChallengeData = P2PContent.GetString(content)
            };
            
            var response = _authManager.ComputeResponse(challenge);
            var responsePacket = _coder.EncodeP2P(P2PMessageType.AuthResponse,
                Encoding.UTF8.GetBytes(response));
            _signalSocket.SendAsync(responsePacket);
            
            P2PLogHelper.Debug(NodeId, "Sent auth response");
        }
        
        private void ProcessAuthSuccess()
        {
            P2PLogHelper.Info(NodeId, "Authentication successful");
        }
        
        private void ProcessNodeList(ReadOnlyMemory<byte> content)
        {
            var text = P2PContent.GetString(content);
            var nodeIds = text.Split(',');
            
            foreach (var nodeId in nodeIds)
            {
                if (!string.IsNullOrEmpty(nodeId) && nodeId != _options.NodeId)
                {
                    _nodes[nodeId] = new NodeInfo { NodeId = nodeId, State = NodeState.Registered };
                }
            }
            
            P2PLogHelper.Debug(NodeId, $"Received node list: {nodeIds.Length} nodes");
        }
        
        private void ProcessNatProbeAck(ReadOnlyMemory<byte> content)
        {
            var text = P2PContent.GetString(content);
            var parts = text.Split('|');
            
            if (parts.Length >= 2)
            {
                var publicAddr = ParseEndPoint(parts[0] + ":" + parts[1]);
                if (_holePuncher != null)
                {
                    _holePuncher.SetPublicAddress(publicAddr);
                }
                P2PLogHelper.Debug(NodeId, $"NAT probe ack: public address {publicAddr}");
            }
            
            if (parts.Length >= 3 && int.TryParse(parts[2], out var natValue))
            {
                var natType = (NATType)natValue;
                if (_holePuncher != null)
                {
                    _holePuncher.SetNATType(natType);
                }
            }
        }
        
        private void ProcessPunchReady(ReadOnlyMemory<byte> content)
        {
            var text = P2PContent.GetString(content);
            var parts = text.Split('|');
            if (parts.Length < 3) return;
            
            var targetId = parts[0];
            var targetPublicAddr = ParseEndPoint(parts[1]);
            var targetLocalAddr = ParseEndPoint(parts[2]);
            
            SetState(NodeState.HolePunching);
            
            var session = new PeerSession(Guid.NewGuid().ToString("N"), targetId);
            session.PublicAddress = targetPublicAddr?.Address.ToString();
            session.PublicPort = targetPublicAddr?.Port ?? 0;
            session.LocalAddress = targetLocalAddr?.Address.ToString();
            session.LocalPort = targetLocalAddr?.Port ?? 0;
            
            _peers[targetId] = session;
            
            P2PLogHelper.Debug(NodeId, $"Punch ready for {targetId}");
        }
        
        private void ProcessRelayAck(ReadOnlyMemory<byte> content)
        {
            if (_relayManager == null) return;
            
            var text = P2PContent.GetString(content);
            var parts = text.Split('|');
            var relaySessionId = parts[0];
            var peerId = parts.Length > 1 ? parts[1] : null;
            
            if (!string.IsNullOrEmpty(peerId))
            {
                var session = _peers.GetOrAdd(peerId, id => new PeerSession(Guid.NewGuid().ToString("N"), id));
                session.Channel = ChannelType.Relay;
                session.RelaySessionId = relaySessionId;
                session.Active();
                
                if (_relayManager.GetSession(relaySessionId) == null)
                    _relayManager.AttachSession(relaySessionId, NodeId, peerId);
            }
            
            P2PLogHelper.Info(NodeId, $"Relay session created: {relaySessionId} peer: {peerId}");
        }
        
        private void ProcessRelayData(ReadOnlyMemory<byte> content)
        {
            // content layout: {relaySessionId}|{sourceId}|{targetId}|{payload}
            var span = content.Span;
            var first = span.IndexOf((byte)'|');
            if (first <= 0) return;
            var second = P2PContent.IndexOf(span, (byte)'|', first + 1);
            if (second < 0) return;
            var third = P2PContent.IndexOf(span, (byte)'|', second + 1);
            if (third < 0) return;
            
            var sourceId = P2PContent.GetString(content, first + 1, second - first - 1);
            var payload = content.Slice(third + 1).ToArray();
            
            OnMessageReceived?.Invoke(sourceId, payload);
        }
        
        private void ProcessUserData(ReadOnlyMemory<byte> content)
        {
            // content layout: {sourceId}|{payload}
            var span = content.Span;
            var first = span.IndexOf((byte)'|');
            if (first <= 0) return;
            
            var peerId = P2PContent.GetString(content, 0, first);
            var payload = content.Slice(first + 1).ToArray();
            
            OnMessageReceived?.Invoke(peerId, payload);
        }
        
        private void SetState(NodeState newState)
        {
            var oldState = _state;
            _state = newState;
            OnStateChanged?.Invoke(oldState, newState);
            P2PLogHelper.Debug(NodeId, $"State changed: {oldState} -> {newState}");
        }
        
        /// <summary>
        /// 经信令服务器请求与对端打洞，状态非 Registered 时抛出 P2PException
        /// </summary>
        /// <param name="peerId">目标节点标识，不可为空</param>
        /// <returns>请求已发出即返回 true，实际连通结果由事件通知</returns>
        public async Task<bool> ConnectToPeerAsync(string peerId)
        {
            if (_state != NodeState.Registered)
                throw new P2PException(ErrorCode.PunchFailed);
            
            if (string.IsNullOrEmpty(peerId))
                throw new ArgumentException("Peer ID cannot be null or empty", nameof(peerId));
            
            var requestContent = Encoding.UTF8.GetBytes(peerId);
            var requestPacket = _coder.EncodeP2P(P2PMessageType.PunchRequest, requestContent);
            _signalSocket.SendAsync(requestPacket);
            
            P2PLogHelper.Info(NodeId, $"Requesting connection to peer: {peerId}");
            
            return true;
        }
        
        /// <summary>
        /// 向对端发送用户数据，中继会话走中继转发，其余经信令服务器转发
        /// </summary>
        /// <param name="peerId">对端节点标识，需已存在会话</param>
        /// <param name="data">待发送负载，不可为 null 或空数组</param>
        public void Send(string peerId, byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new P2PException(ErrorCode.DiscoveryNoResponse);
            
            var session = _peers.TryGetValue(peerId, out var s) ? s : null;
            if (session == null)
                throw new P2PException(ErrorCode.DiscoveryNoCandidates);
            
            if (_signalSocket == null)
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "Signal server is not connected");
            
            if (session.Channel == ChannelType.Relay && _relayManager != null && !string.IsNullOrEmpty(session.RelaySessionId))
            {
                var relayData = _relayManager.EncodeRelayData(session.RelaySessionId, NodeId, peerId, data);
                _signalSocket.SendAsync(relayData);
            }
            else
            {
                // Direct delivery through the signal server: {sourceId}|{targetId}|{payload}
                var header = Encoding.UTF8.GetBytes($"{NodeId}|{peerId}|");
                var combined = new byte[header.Length + data.Length];
                Buffer.BlockCopy(header, 0, combined, 0, header.Length);
                Buffer.BlockCopy(data, 0, combined, header.Length, data.Length);
                
                var packet = _coder.EncodeP2P(P2PMessageType.UserData, combined);
                _signalSocket.SendAsync(packet);
            }
            
            session.Active();
            P2PLogHelper.Trace(NodeId, $"Sent {data.Length} bytes to {peerId}");
        }
        
        /// <summary>
        /// 向信令服务器请求为指定对端建立中继会话，未启用中继时抛出 P2PException
        /// </summary>
        /// <param name="peerId">目标节点标识</param>
        public void SendRelayRequest(string peerId)
        {
            if (_relayManager == null)
                throw new P2PException(ErrorCode.RelayFailed);

            if (_signalSocket == null)
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "Signal server is not connected");

            var requestContent = Encoding.UTF8.GetBytes(peerId);
            var requestPacket = _coder.EncodeP2P(P2PMessageType.RelayRequest, requestContent);
            _signalSocket.SendAsync(requestPacket);
            
            P2PLogHelper.Info(NodeId, $"Requesting relay to: {peerId}");
        }
        
        private void SendNatProbe()
        {
            var probePacket = _coder.EncodeP2P(P2PMessageType.NatProbe);
            _signalSocket.SendAsync(probePacket);
        }
        
        private void SendHeartbeatAck()
        {
            var ackPacket = _coder.EncodeP2P(P2PMessageType.HeartbeatAck);
            _signalSocket.SendAsync(ackPacket);
        }
        
        /// <summary>
        /// 向信令服务器发送心跳，信令连接未建立时抛出 P2PException
        /// </summary>
        public void SendHeartbeat()
        {
            if (_signalSocket == null)
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "Signal server is not connected");

            var heartbeatPacket = _coder.EncodeP2P(P2PMessageType.Heartbeat);
            _signalSocket.SendAsync(heartbeatPacket);
        }
        
        private IPEndPoint ParseEndPoint(string addr)
        {
            if (string.IsNullOrEmpty(addr)) return null;
            
            // Support both IPv4 ("1.2.3.4:80") and IPv6 ("::1:80" / "[::1]:80").
            var idx = addr.LastIndexOf(':');
            if (idx <= 0 || idx == addr.Length - 1) return null;
            
            var ipPart = addr.Substring(0, idx).Trim('[', ']');
            var portPart = addr.Substring(idx + 1);
            
            if (!IPAddress.TryParse(ipPart, out var ip)) return null;
            if (!int.TryParse(portPart, out var port) || port < 0 || port > 65535) return null;
            
            return new IPEndPoint(ip, port);
        }
        
        /// <summary>
        /// 取指定对端的会话
        /// </summary>
        /// <param name="peerId">对端节点标识</param>
        /// <returns>会话对象，不存在时返回 null</returns>
        public PeerSession GetSession(string peerId)
        {
            return _peers.TryGetValue(peerId, out var session) ? session : null;
        }
        
        /// <summary>
        /// 停止本地发现、断开信令连接并清空节点与会话，状态置为 Disconnected
        /// </summary>
        public void Disconnect()
        {
            _localDiscovery?.Stop();
            _signalSocket?.Disconnect();
            _peers.Clear();
            _nodes.Clear();
            SetState(NodeState.Disconnected);
            P2PLogHelper.Info(NodeId, "Disconnected");
        }
    }
}