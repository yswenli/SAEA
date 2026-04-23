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
*唯一标识：3277e38e-3ac4-4f86-9c3a-31ac9f541c9a
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 16:27:00
*描述：
*
*=====================================================================
*修改标记
*修改时间：2026/04/20 16:27:00
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using SAEA.Sockets;
using SAEA.Sockets.Core.Tcp;
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
    public class P2PClient
    {
        private P2POptions _options;
        private IocpClientSocket _signalSocket;
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
        
        public string NodeId => _options.NodeId;
        public NodeState State => _state;
        public bool IsConnected => _state == NodeState.Connected || _state == NodeState.Registered;
        
        public event Action<NodeState, NodeState> OnStateChanged;
        public event Action OnServerConnected;
        public event Action<string> OnServerDisconnected;
        public event Action<string, ChannelType> OnPeerConnected;
        public event Action<string, string> OnPeerDisconnected;
        public event Action<string, byte[]> OnMessageReceived;
        public event Action<string, string> OnError;
        public event Action<DiscoveredNode> OnLocalNodeDiscovered;
        
        public ConcurrentDictionary<string, NodeInfo> KnownNodes => _nodes;
        public ConcurrentDictionary<string, PeerSession> PeerSessions => _peers;
        
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
                .UseIocp()
                .SetIP(_options.ServerAddress)
                .SetPort(_options.ServerPort)
                .SetConnectTimeout(_options.Timeout.ConnectTimeoutMs)
                .Build();
            
            _signalSocket = SocketFactory.CreateClientSocket(option) as IocpClientSocket;
            _signalSocket.OnReceive += OnSignalReceive;
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
        
        public void Connect()
        {
            ConnectAsync().Wait();
        }
        
        private void StartLocalDiscovery()
        {
            _localDiscovery?.Start();
            P2PLogHelper.Info(NodeId, "Local discovery started");
        }
        
        private void OnSignalReceive(byte[] data)
        {
            var protocols = _coder.DecodeP2P(data);
            foreach (var p in protocols)
            {
                ProcessSignalMessage(p);
            }
        }
        
        private void ProcessSignalMessage(P2PProtocol protocol)
        {
            switch (protocol.GetMessageType())
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
        
        private void ProcessRegisterAck(byte[] content)
        {
            if (content == null) return;
            var text = Encoding.UTF8.GetString(content);
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
        
        private void ProcessAuthChallenge(byte[] content)
        {
            if (_authManager == null || content == null) return;
            
            var challenge = new AuthChallenge
            {
                ChallengeData = Encoding.UTF8.GetString(content)
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
        
        private void ProcessNodeList(byte[] content)
        {
            if (content == null) return;
            var text = Encoding.UTF8.GetString(content);
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
        
        private void ProcessNatProbeAck(byte[] content)
        {
            if (content == null) return;
            var text = Encoding.UTF8.GetString(content);
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
            
            if (parts.Length >= 3)
            {
                var natType = (NATType)int.Parse(parts[2]);
                if (_holePuncher != null)
                {
                    _holePuncher.SetNATType(natType);
                }
            }
        }
        
        private void ProcessPunchReady(byte[] content)
        {
            if (content == null) return;
            var text = Encoding.UTF8.GetString(content);
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
        
        private void ProcessRelayAck(byte[] content)
        {
            if (content == null || _relayManager == null) return;
            
            var relaySessionId = Encoding.UTF8.GetString(content);
            P2PLogHelper.Info(NodeId, $"Relay session created: {relaySessionId}");
        }
        
        private void ProcessRelayData(byte[] content)
        {
            if (content == null) return;
            
            var text = Encoding.UTF8.GetString(content);
            var parts = text.Split('|');
            if (parts.Length >= 4)
            {
                var sourceId = parts[1];
                var headerLen = Encoding.UTF8.GetBytes($"{parts[0]}|{parts[1]}|{parts[2]}|").Length;
                var payload = new byte[content.Length - headerLen];
                Buffer.BlockCopy(content, headerLen, payload, 0, payload.Length);
                
                OnMessageReceived?.Invoke(sourceId, payload);
            }
        }
        
        private void ProcessUserData(byte[] content)
        {
            if (content == null) return;
            
            var text = Encoding.UTF8.GetString(content);
            var idx = text.IndexOf('|');
            if (idx > 0)
            {
                var peerId = text.Substring(0, idx);
                var payloadText = text.Substring(idx + 1);
                var payload = Encoding.UTF8.GetBytes(payloadText);
                OnMessageReceived?.Invoke(peerId, payload);
            }
        }
        
        private void SetState(NodeState newState)
        {
            var oldState = _state;
            _state = newState;
            OnStateChanged?.Invoke(oldState, newState);
            P2PLogHelper.Debug(NodeId, $"State changed: {oldState} -> {newState}");
        }
        
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
        
        public void Send(string peerId, byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new P2PException(ErrorCode.DiscoveryNoResponse);
            
            var session = _peers.TryGetValue(peerId, out var s) ? s : null;
            if (session == null)
                throw new P2PException(ErrorCode.DiscoveryNoCandidates);
            
            if (session.Channel == ChannelType.Direct)
            {
                var userDataContent = Encoding.UTF8.GetBytes($"{NodeId}|");
                var combined = new byte[userDataContent.Length + data.Length];
                Buffer.BlockCopy(userDataContent, 0, combined, 0, userDataContent.Length);
                Buffer.BlockCopy(data, 0, combined, userDataContent.Length, data.Length);
                
                var packet = _coder.EncodeP2P(P2PMessageType.UserData, combined);
                _signalSocket.SendAsync(packet);
            }
            else if (session.Channel == ChannelType.Relay && _relayManager != null && !string.IsNullOrEmpty(session.RelaySessionId))
            {
                var relayData = _relayManager.EncodeRelayData(session.RelaySessionId, NodeId, peerId, data);
                _signalSocket.SendAsync(relayData);
            }
            
            session.Active();
            P2PLogHelper.Trace(NodeId, $"Sent {data.Length} bytes to {peerId}");
        }
        
        public void SendRelayRequest(string peerId)
        {
            if (_relayManager == null)
                throw new P2PException(ErrorCode.RelayFailed);
            
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
        
        public void SendHeartbeat()
        {
            var heartbeatPacket = _coder.EncodeP2P(P2PMessageType.Heartbeat);
            _signalSocket.SendAsync(heartbeatPacket);
        }
        
        private IPEndPoint ParseEndPoint(string addr)
        {
            if (string.IsNullOrEmpty(addr)) return null;
            var parts = addr.Split(':');
            if (parts.Length == 2)
                return new IPEndPoint(IPAddress.Parse(parts[0]), int.Parse(parts[1]));
            return null;
        }
        
        public PeerSession GetSession(string peerId)
        {
            return _peers.TryGetValue(peerId, out var session) ? session : null;
        }
        
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