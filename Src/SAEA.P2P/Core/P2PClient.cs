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
*修改时间：2026/04/20 16:27:00
*修改人： yswenli
*版本号： v26.4.23.1
*描述：P2PClient接口
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
                    ProcessRegisterAck(protocol.Content.ToArray());
                    break;
                case P2PMessageType.NodeList:
                    ProcessNodeList(protocol.Content.ToArray());
                    break;
                case P2PMessageType.AuthChallenge:
                    ProcessAuthChallenge(protocol.Content.ToArray());
                    break;
                case P2PMessageType.AuthSuccess:
                    ProcessAuthSuccess();
                    break;
                case P2PMessageType.PunchReady:
                    ProcessPunchReady(protocol.Content.ToArray());
                    break;
                case P2PMessageType.NatProbeAck:
                    ProcessNatProbeAck(protocol.Content.ToArray());
                    break;
                case P2PMessageType.RelayAck:
                    ProcessRelayAck(protocol.Content.ToArray());
                    break;
                case P2PMessageType.RelayData:
                    ProcessRelayData(protocol.Content.ToArray());
                    break;
                case P2PMessageType.UserData:
                    ProcessUserData(protocol.Content.ToArray());
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
            
            if (parts.Length >= 3 && int.TryParse(parts[2], out var natValue))
            {
                var natType = (NATType)natValue;
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
            
            var text = Encoding.UTF8.GetString(content);
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
        
        private void ProcessRelayData(byte[] content)
        {
            if (content == null) return;
            
            // content layout: {relaySessionId}|{sourceId}|{targetId}|{payload}
            var first = Array.IndexOf(content, (byte)'|');
            if (first <= 0) return;
            var second = Array.IndexOf(content, (byte)'|', first + 1);
            if (second < 0) return;
            var third = Array.IndexOf(content, (byte)'|', second + 1);
            if (third < 0) return;
            
            var sourceId = Encoding.UTF8.GetString(content, first + 1, second - first - 1);
            var payloadOffset = third + 1;
            var payload = new byte[content.Length - payloadOffset];
            if (payload.Length > 0)
                Buffer.BlockCopy(content, payloadOffset, payload, 0, payload.Length);
            
            OnMessageReceived?.Invoke(sourceId, payload);
        }
        
        private void ProcessUserData(byte[] content)
        {
            if (content == null) return;
            
            // content layout: {sourceId}|{payload}
            var first = Array.IndexOf(content, (byte)'|');
            if (first <= 0) return;
            
            var peerId = Encoding.UTF8.GetString(content, 0, first);
            var payloadOffset = first + 1;
            var payload = new byte[content.Length - payloadOffset];
            if (payload.Length > 0)
                Buffer.BlockCopy(content, payloadOffset, payload, 0, payload.Length);
            
            OnMessageReceived?.Invoke(peerId, payload);
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