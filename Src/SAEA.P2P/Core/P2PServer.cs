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
*文件名： P2PServer
*版本号： v26.4.23.1
*唯一标识：99e25206-bdaa-4caa-8a6c-332fdbabd438
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 16:27:00
*描述：P2PServer服务端类
*
*=====================================================================
*修改标记
*修改时间：2026/04/20 16:27:00
*修改人： yswenli
*版本号： v26.4.23.1
*描述：P2PServer服务端类
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using SAEA.Sockets;
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using SAEA.P2P.Builder;
using SAEA.P2P.Common;
using SAEA.P2P.Model;
using SAEA.P2P.NAT;
using SAEA.P2P.Protocol;
using SAEA.P2P.Relay;
using SAEA.P2P.Security;

namespace SAEA.P2P.Core
{
    public class P2PServer
    {
        private P2PServerOptions _options;
        private IocpServerSocket _serverSocket;
        private P2PCoder _coder = new P2PCoder();
        
        private RelayManager _relayManager;
        private ConcurrentDictionary<string, NodeInfo> _nodes = new ConcurrentDictionary<string, NodeInfo>();
        private ConcurrentDictionary<string, string> _sessionToNode = new ConcurrentDictionary<string, string>();
        private ConcurrentDictionary<string, string> _nodeToSession = new ConcurrentDictionary<string, string>();
        private ConcurrentDictionary<string, AuthChallenge> _pendingChallenges = new ConcurrentDictionary<string, AuthChallenge>();
        
        public int Port => _options.Port;
        public int NodeCount => _nodes.Count;
        public bool IsRunning { get; private set; }
        
        public event Action<string, IPEndPoint> OnNodeRegistered;
        public event Action<string> OnNodeUnregistered;
        public event Action<string, string> OnRelayStarted;
        public event Action<string> OnRelayEnded;
        public event Action<string, string> OnError;
        
        public ConcurrentDictionary<string, NodeInfo> Nodes => _nodes;
        
        public P2PServer(P2PServerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            
            if (_options.Relay.Enabled)
                _relayManager = new RelayManager(_options.Timeout.SessionTimeoutMs, _options.Relay.MaxQuota);
            
            P2PLogHelper.SetLevel(_options.Logging.Level);
        }
        
        public void Start()
        {
            var option = SocketOptionBuilder.Instance
                .SetSocket(SAEASocketType.Tcp)
                .UseIocp()
                .SetIP(_options.BindIP)
                .SetPort(_options.Port)
                .SetMaxConnects(_options.MaxNodes)
                .SetFreeTime(_options.Timeout.FreeTimeMs)
                .SetActionTimeOut(_options.Timeout.ReceiveTimeoutMs)
                .Build();
            
            _serverSocket = SocketFactory.CreateServerSocket(option) as IocpServerSocket;
            _serverSocket.OnAccepted += OnAccepted;
            _serverSocket.OnReceive += OnReceive;
            _serverSocket.OnDisconnected += OnDisconnected;
            _serverSocket.OnError += (id, ex) => OnError?.Invoke(id, ex.Message);
            
            _serverSocket.Start();
            IsRunning = true;
            
            P2PLogHelper.Info("P2PServer", $"Started on {_options.BindIP}:{_options.Port}");
        }
        
        public void Stop()
        {
            _serverSocket?.Stop();
            IsRunning = false;
            _nodes.Clear();
            _sessionToNode.Clear();
            _nodeToSession.Clear();
            _pendingChallenges.Clear();
            
            P2PLogHelper.Info("P2PServer", "Stopped");
        }
        
        private void OnAccepted(object obj)
        {
            var userToken = obj as IUserToken;
            P2PLogHelper.Debug("P2PServer", $"New connection: {userToken?.ID}");
        }
        
        private void OnReceive(ISession session, byte[] data)
        {
            var protocols = _coder.DecodeP2P(data);
            foreach (var p in protocols)
            {
                ProcessMessage(session.ID, p);
            }
        }
        
        private void ProcessMessage(string sessionId, P2PProtocol protocol)
        {
            switch (protocol.GetMessageType())
            {
                case P2PMessageType.Register:
                    ProcessRegister(sessionId, protocol.Content);
                    break;
                case P2PMessageType.AuthResponse:
                    ProcessAuthResponse(sessionId, protocol.Content);
                    break;
                case P2PMessageType.NatProbe:
                    ProcessNatProbe(sessionId);
                    break;
                case P2PMessageType.PunchRequest:
                    ProcessPunchRequest(sessionId, protocol.Content);
                    break;
                case P2PMessageType.RelayRequest:
                    ProcessRelayRequest(sessionId, protocol.Content);
                    break;
                case P2PMessageType.RelayData:
                    ProcessRelayData(sessionId, protocol.Content);
                    break;
                case P2PMessageType.Heartbeat:
                    SendHeartbeatAck(sessionId);
                    break;
            }
        }
        
        private void ProcessRegister(string sessionId, byte[] content)
        {
            if (content == null) return;
            var nodeId = Encoding.UTF8.GetString(content);
            
            if (_nodes.ContainsKey(nodeId))
            {
                var failAckContent = Encoding.UTF8.GetBytes($"FAIL|{ErrorCode.RegisterPeerIdDuplicate}");
                var failAckPacket = _coder.EncodeP2P(P2PMessageType.RegisterAck, failAckContent);
                _serverSocket.SendAsync(sessionId, failAckPacket);
                OnError?.Invoke(sessionId, ErrorCode.RegisterPeerIdDuplicate);
                return;
            }
            
            var node = new NodeInfo
            {
                NodeId = nodeId,
                RegisteredTime = DateTime.UtcNow,
                LastActiveTime = DateTime.UtcNow,
                State = NodeState.Registered
            };
            
            _nodes[nodeId] = node;
            _sessionToNode[sessionId] = nodeId;
            _nodeToSession[nodeId] = sessionId;
            
            var okAckContent = Encoding.UTF8.GetBytes($"OK|{sessionId}");
            var okAckPacket = _coder.EncodeP2P(P2PMessageType.RegisterAck, okAckContent);
            _serverSocket.SendAsync(sessionId, okAckPacket);
            
            var challenge = AuthChallenge.Create();
            _pendingChallenges[sessionId] = challenge;
            var challengePacket = _coder.EncodeP2P(P2PMessageType.AuthChallenge,
                Encoding.UTF8.GetBytes(challenge.ChallengeData));
            _serverSocket.SendAsync(sessionId, challengePacket);
            
            OnNodeRegistered?.Invoke(nodeId, null);
            P2PLogHelper.Info("P2PServer", $"Node registered: {nodeId}");
            
            BroadcastNodeList();
        }
        
        private void ProcessAuthResponse(string sessionId, byte[] content)
        {
            if (content == null) return;
            
            var response = Encoding.UTF8.GetString(content);
            
            if (_pendingChallenges.TryRemove(sessionId, out var challenge))
            {
                var ackPacket = _coder.EncodeP2P(P2PMessageType.AuthSuccess);
                _serverSocket.SendAsync(sessionId, ackPacket);
                P2PLogHelper.Debug("P2PServer", $"Auth success for session {sessionId}");
            }
        }
        
        private void ProcessNatProbe(string sessionId)
        {
            var userToken = _serverSocket.GetCurrentObj(sessionId) as IUserToken;
            if (userToken != null && userToken.Socket != null)
            {
                var remoteEP = userToken.Socket.RemoteEndPoint as IPEndPoint;
                if (remoteEP != null)
                {
                    var probeAckContent = Encoding.UTF8.GetBytes($"{remoteEP.Address}|{remoteEP.Port}|{(int)NATType.Unknown}");
                    var probeAckPacket = _coder.EncodeP2P(P2PMessageType.NatProbeAck, probeAckContent);
                    _serverSocket.SendAsync(sessionId, probeAckPacket);
                    P2PLogHelper.Debug("P2PServer", $"Nat probe ack sent to {sessionId}: {remoteEP}");
                }
            }
        }
        
        private void ProcessPunchRequest(string sessionId, byte[] content)
        {
            if (content == null) return;
            var targetId = Encoding.UTF8.GetString(content);
            
            var sourceId = _sessionToNode.TryGetValue(sessionId, out var s) ? s : null;
            if (sourceId == null) return;
            
            if (_nodes.TryGetValue(targetId, out var targetNode))
            {
                var targetSessionId = _nodeToSession.TryGetValue(targetId, out var ts) ? ts : null;
                if (targetSessionId != null)
                {
                    var sourceNode = _nodes[sourceId];
                    
                    var readyContent = Encoding.UTF8.GetBytes($"{targetId}|{targetNode.GetPublicEndPoint()}|{targetNode.GetLocalEndPoint()}");
                    var readyPacket = _coder.EncodeP2P(P2PMessageType.PunchReady, readyContent);
                    _serverSocket.SendAsync(sessionId, readyPacket);
                    
                    var notifyContent = Encoding.UTF8.GetBytes($"{sourceId}|{sourceNode.GetPublicEndPoint()}|{sourceNode.GetLocalEndPoint()}");
                    var notifyPacket = _coder.EncodeP2P(P2PMessageType.PunchReady, notifyContent);
                    _serverSocket.SendAsync(targetSessionId, notifyPacket);
                    
                    P2PLogHelper.Debug("P2PServer", $"Punch request: {sourceId} -> {targetId}");
                }
                else
                {
                    OnError?.Invoke(sessionId, ErrorCode.PunchNoRoute);
                }
            }
            else
            {
                OnError?.Invoke(sessionId, ErrorCode.DiscoveryNoCandidates);
            }
        }
        
        private void ProcessRelayRequest(string sessionId, byte[] content)
        {
            if (_relayManager == null)
            {
                OnError?.Invoke(sessionId, ErrorCode.RelayFailed);
                return;
            }
            
            if (content == null) return;
            var targetId = Encoding.UTF8.GetString(content);
            var sourceId = _sessionToNode.TryGetValue(sessionId, out var s) ? s : null;
            
            if (sourceId == null) return;
            
            var relaySession = _relayManager.CreateSession(sourceId, targetId);
            
            var ackContent = Encoding.UTF8.GetBytes(relaySession.SessionId);
            var ackPacket = _coder.EncodeP2P(P2PMessageType.RelayAck, ackContent);
            _serverSocket.SendAsync(sessionId, ackPacket);
            
            var targetSessionId = _nodeToSession.TryGetValue(targetId, out var ts) ? ts : null;
            if (targetSessionId != null)
            {
                var targetAckContent = Encoding.UTF8.GetBytes(relaySession.SessionId);
                var targetAckPacket = _coder.EncodeP2P(P2PMessageType.RelayAck, targetAckContent);
                _serverSocket.SendAsync(targetSessionId, targetAckPacket);
            }
            
            OnRelayStarted?.Invoke(sourceId, targetId);
            P2PLogHelper.Info("P2PServer", $"Relay started: {sourceId} -> {targetId}");
        }
        
        private void ProcessRelayData(string sessionId, byte[] content)
        {
            if (_relayManager == null || content == null) return;
            
            var text = Encoding.UTF8.GetString(content);
            var parts = text.Split('|');
            if (parts.Length >= 4)
            {
                var relaySessionId = parts[0];
                var sourceId = parts[1];
                var targetId = parts[2];
                
                var headerLen = Encoding.UTF8.GetBytes($"{relaySessionId}|{sourceId}|{targetId}|").Length;
                var payload = new byte[content.Length - headerLen];
                Buffer.BlockCopy(content, headerLen, payload, 0, payload.Length);
                
                var relaySession = _relayManager.GetSession(relaySessionId);
                if (relaySession != null)
                {
                    relaySession.AddBytes(payload.Length);
                    
                    var targetSessionId = _nodeToSession.TryGetValue(targetId, out var ts) ? ts : null;
                    if (targetSessionId != null)
                    {
                        var forwardPacket = _coder.EncodeP2P(P2PMessageType.RelayData, content);
                        _serverSocket.SendAsync(targetSessionId, forwardPacket);
                        
                        P2PLogHelper.Trace("P2PServer", $"Relay data forwarded: {relaySessionId}, {payload.Length} bytes");
                    }
                }
                else
                {
                    OnError?.Invoke(sessionId, ErrorCode.RelaySessionNotFound);
                }
            }
        }
        
        private void OnDisconnected(string sessionId, Exception ex)
        {
            var nodeId = _sessionToNode.TryGetValue(sessionId, out var id) ? id : null;
            if (nodeId != null)
            {
                _nodes.TryRemove(nodeId, out _);
                _sessionToNode.TryRemove(sessionId, out _);
                _nodeToSession.TryRemove(nodeId, out _);
                _pendingChallenges.TryRemove(sessionId, out _);
                OnNodeUnregistered?.Invoke(nodeId);
                P2PLogHelper.Info("P2PServer", $"Node disconnected: {nodeId}");
                
                BroadcastNodeList();
            }
        }
        
        private void BroadcastNodeList()
        {
            var nodeList = string.Join(",", _nodes.Keys);
            var listContent = Encoding.UTF8.GetBytes(nodeList);
            var listPacket = _coder.EncodeP2P(P2PMessageType.NodeList, listContent);
            
            foreach (var sessionId in _sessionToNode.Keys)
            {
                _serverSocket.SendAsync(sessionId, listPacket);
            }
        }
        
        private void SendHeartbeatAck(string sessionId)
        {
            var ackPacket = _coder.EncodeP2P(P2PMessageType.HeartbeatAck);
            _serverSocket.SendAsync(sessionId, ackPacket);
        }
        
        public NodeInfo GetNode(string nodeId)
        {
            return _nodes.TryGetValue(nodeId, out var node) ? node : null;
        }
        
        public int ActiveRelaySessionCount => _relayManager?.ActiveSessionCount ?? 0;
    }
}