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
*修改时间：2026/11/13 13:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：删除自定义打洞、NAT 探测与中继转发，收窄为纯信令服务器
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SAEA.Sockets;
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using SAEA.P2P.Builder;
using SAEA.P2P.Common;
using SAEA.P2P.Model;
using SAEA.P2P.Protocol;
using SAEA.P2P.Security;
using SAEA.P2P.Signaling;
using SAEA.P2P.Turn;

namespace SAEA.P2P.Core
{
    /// <summary>
    /// P2P 信令服务器，负责节点注册、会话消息转发、TURN 临时凭据下发与节点列表广播；不承载业务数据，也不做打洞地址交换或中继转发
    /// </summary>
    public class P2PServer
    {
        private P2PServerOptions _options;
        private SaeaServerSocket _serverSocket;
        private P2PCoder _coder = new P2PCoder();
        private readonly ConcurrentDictionary<string, P2PCoder> _sessionCoders = new ConcurrentDictionary<string, P2PCoder>();
        
        private ConcurrentDictionary<string, NodeInfo> _nodes = new ConcurrentDictionary<string, NodeInfo>();
        private ConcurrentDictionary<string, string> _sessionToNode = new ConcurrentDictionary<string, string>();
        private ConcurrentDictionary<string, string> _nodeToSession = new ConcurrentDictionary<string, string>();
        private ConcurrentDictionary<string, AuthChallenge> _pendingChallenges = new ConcurrentDictionary<string, AuthChallenge>();

        /// <summary>
        /// 已通过鉴权的会话标识
        /// </summary>
        private readonly ConcurrentDictionary<string, byte> _authedSessions = new ConcurrentDictionary<string, byte>();

        /// <summary>
        /// 信令会话表，键为会话标识
        /// </summary>
        private readonly ConcurrentDictionary<string, SignalingSessionEntry> _sessions = new ConcurrentDictionary<string, SignalingSessionEntry>();

        /// <summary>
        /// 共享密钥的 UTF-8 字节，作为鉴权 HMAC 的密钥；未配置密钥时为 null
        /// </summary>
        private readonly byte[] _authKeyBytes;

        /// <summary>
        /// TURN 临时凭据签发服务；未配置共享密钥时为 null
        /// </summary>
        private readonly TurnCredentialService _credentialService;

        /// <summary>
        /// 会话超时扫描定时器
        /// </summary>
        private Timer _sessionSweepTimer;

        /// <summary>
        /// 回错误后延迟断开的时间，单位毫秒；给底层发送缓冲留出落地窗口，避免对端收到 RST 而不是错误报文
        /// </summary>
        private const int DisconnectGraceMilliseconds = 200;

        /// <summary>
        /// 监听端口，取自服务器配置
        /// </summary>
        public int Port => _options.Port;
        /// <summary>
        /// 当前已注册的节点数
        /// </summary>
        public int NodeCount => _nodes.Count;
        /// <summary>
        /// 服务是否已启动
        /// </summary>
        public bool IsRunning { get; private set; }
        
        /// <summary>
        /// 节点注册成功时触发，参数为节点标识与公网地址，当前实现固定传 null 地址
        /// </summary>
        public event Action<string, IPEndPoint> OnNodeRegistered;
        /// <summary>
        /// 节点掉线时触发，参数为节点标识
        /// </summary>
        public event Action<string> OnNodeUnregistered;
        /// <summary>
        /// 发生错误时触发，参数为会话标识或节点标识与错误码
        /// </summary>
        public event Action<string, string> OnError;
        
        /// <summary>
        /// 已注册节点集合，键为节点标识
        /// </summary>
        public ConcurrentDictionary<string, NodeInfo> Nodes => _nodes;
        
        /// <summary>
        /// 按配置创建服务器
        /// </summary>
        /// <param name="options">服务器配置，不可为 null</param>
        public P2PServer(P2PServerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            
            P2PLogHelper.SetLevel(_options.Logging.Level);

            // 密钥为空表示不启用信令鉴权，同时也就不签发 TURN 凭据
            if (!string.IsNullOrEmpty(_options.SharedSecret))
            {
                _authKeyBytes = Encoding.UTF8.GetBytes(_options.SharedSecret);
                _credentialService = new TurnCredentialService(_options.SharedSecret, _options.Realm, _options.CredentialTtlSeconds);
            }
        }
        
        /// <summary>
        /// 启动 TCP 监听并挂接连接、收包、断开与错误回调，成功后 IsRunning 为 true
        /// </summary>
        public void Start()
        {
            var option = SocketOptionBuilder.Instance
                .SetSocket(SAEASocketType.Tcp)
                .UsePacket()
                .SetIP(_options.BindIP)
                .SetPort(_options.Port)
                .SetMaxConnects(_options.MaxNodes)
                .SetFreeTime(_options.Timeout.FreeTimeMs)
                .SetActionTimeOut(_options.Timeout.ReceiveTimeoutMs)
                .Build();
            
            _serverSocket = SocketFactory.CreateServerSocket(option) as SaeaServerSocket;
            _serverSocket.OnAccepted += OnAccepted;
            _serverSocket.OnServerReceiveSpan += OnServerReceiveSpan;
            _serverSocket.OnDisconnected += OnDisconnected;
            _serverSocket.OnError += (id, ex) => OnError?.Invoke(id, ex.Message);
            
            _serverSocket.Start();
            IsRunning = true;

            // 扫描周期取会话超时的一半，最短 100ms，保证用例可以在秒级观察到超时
            var sweepPeriod = Math.Max(100, _options.Timeout.SessionTimeoutMs / 2);
            _sessionSweepTimer = new Timer(SweepSessions, null, sweepPeriod, sweepPeriod);
            
            P2PLogHelper.Info("P2PServer", $"Started on {_options.BindIP}:{_options.Port}");
        }
        
        /// <summary>
        /// 停止监听并清空节点、会话映射、待处理鉴权挑战与信令会话
        /// </summary>
        public void Stop()
        {
            if (_sessionSweepTimer != null)
            {
                _sessionSweepTimer.Dispose();
                _sessionSweepTimer = null;
            }

            _serverSocket?.Stop();
            IsRunning = false;
            _nodes.Clear();
            _sessionToNode.Clear();
            _nodeToSession.Clear();
            _pendingChallenges.Clear();
            _authedSessions.Clear();
            _sessions.Clear();
            _sessionCoders.Clear();
            
            P2PLogHelper.Info("P2PServer", "Stopped");
        }
        
        private void OnAccepted(object obj)
        {
            var userToken = obj as IUserToken;
            P2PLogHelper.Debug("P2PServer", $"New connection: {userToken?.ID}");
        }
        
        private void OnServerReceiveSpan(IUserToken userToken, ReadOnlySpan<byte> data)
        {
            var sessionId = userToken?.ID;
            if (string.IsNullOrEmpty(sessionId)) return;

            var coder = GetSessionCoder(sessionId);
            using (var frames = coder.DecodeP2P(data))
            {
                foreach (var frame in frames.Frames)
                {
                    ProcessMessage(sessionId, frame);
                }
            }
        }
        
        /// <summary>
        /// 每会话独立的有状态解码器：<see cref="BaseCoder"/> 的半包缓存是实例字段，
        /// 跨会话并发调用会损坏拆帧状态。编码仍用共享 <see cref="_coder"/>（无状态）。
        /// </summary>
        private P2PCoder GetSessionCoder(string sessionId)
        {
            return _sessionCoders.GetOrAdd(sessionId, id => new P2PCoder());
        }
        
        private void ProcessMessage(string sessionId, ISocketProtocal protocol)
        {
            switch ((P2PMessageType)protocol.Type)
            {
                case P2PMessageType.Register:
                    ProcessRegister(sessionId, protocol.Content);
                    break;
                case P2PMessageType.AuthResponse:
                    ProcessAuthResponse(sessionId, protocol.Content);
                    break;
                case P2PMessageType.ConnectRequest:
                    ProcessConnectRequest(sessionId, protocol.Content);
                    break;
                case P2PMessageType.SessionOffer:
                case P2PMessageType.SessionAnswer:
                case P2PMessageType.TrickleCandidate:
                case P2PMessageType.EndOfCandidates:
                    ForwardSessionMessage(sessionId, protocol.Content);
                    break;
                case P2PMessageType.SessionClose:
                    ProcessSessionClose(sessionId, protocol.Content);
                    break;
                case P2PMessageType.Heartbeat:
                    SendHeartbeatAck(sessionId);
                    break;
            }
        }
        
        /// <summary>
        /// 处理新格式注册：登记节点、回 RegisterAck，按配置下发鉴权挑战或 TURN 凭据
        /// </summary>
        /// <param name="sessionId">底层会话标识</param>
        /// <param name="content">Register 报文负载</param>
        private void ProcessRegister(string sessionId, ReadOnlyMemory<byte> content)
        {
            // 旧客户端仍发裸节点标识，按首字节区分，避免迁移期间整体不可用
            if (!LooksLikeJson(content))
            {
                ProcessLegacyRegister(sessionId, content);
                return;
            }

            var message = SignalingJsonCodec.Decode(content.ToArray());
            if (message.Type == P2PMessageType.Error)
            {
                SendSignaling(sessionId, message);
                return;
            }

            var peerId = message.PeerId;
            if (string.IsNullOrEmpty(peerId))
            {
                SendError(sessionId, ErrorCode.RegisterPeerIdMissing, "注册缺少节点标识");
                return;
            }

            if (peerId.IndexOf('|') >= 0 || peerId.IndexOf(',') >= 0)
            {
                SendError(sessionId, ErrorCode.RegisterPeerIdInvalid, "节点标识不能包含竖线或逗号");
                return;
            }

            string replacedSessionId = null;
            if (_nodes.ContainsKey(peerId))
            {
                if (_options.RejectDuplicatePeer)
                {
                    SendError(sessionId, ErrorCode.RegisterPeerIdDuplicate, "节点标识已被占用");
                    DisconnectAfterGrace(sessionId);
                    return;
                }

                // 顶替旧连接：先记住旧会话，登记完新会话后再断开，避免旧连接的清理逻辑删掉新映射
                if (_nodeToSession.TryGetValue(peerId, out var previous))
                {
                    replacedSessionId = previous;
                }
            }

            var node = new NodeInfo
            {
                NodeId = peerId,
                RegisteredTime = DateTime.UtcNow,
                LastActiveTime = DateTime.UtcNow,
                State = NodeState.Registered
            };

            _nodes[peerId] = node;
            _sessionToNode[sessionId] = peerId;
            _nodeToSession[peerId] = sessionId;

            if (replacedSessionId != null && replacedSessionId != sessionId)
            {
                SendError(replacedSessionId, ErrorCode.RegisterPeerIdDuplicate, "节点标识被新连接顶替");
                DisconnectAfterGrace(replacedSessionId);
            }

            var ack = SignalingMessage.Create(P2PMessageType.RegisterAck);
            ack.PeerId = peerId;
            ack.SessionId = sessionId;
            ack.ServerTime = DateTimeOffset.UtcNow;
            SendSignaling(sessionId, ack);

            if (_authKeyBytes != null)
            {
                var challenge = AuthChallenge.Create();
                _pendingChallenges[sessionId] = challenge;
                var challengeMessage = SignalingMessage.Create(P2PMessageType.AuthChallenge);
                challengeMessage.Nonce = challenge.ChallengeData;
                SendSignaling(sessionId, challengeMessage);
            }
            else
            {
                // 未配置共享密钥时注册即视为鉴权通过
                _authedSessions[sessionId] = 1;
                SendTurnCredential(sessionId, peerId);
            }

            OnNodeRegistered?.Invoke(peerId, null);
            P2PLogHelper.Info("P2PServer", $"Signaling node registered: {peerId}");
            BroadcastNodeList();
        }

        /// <summary>
        /// 处理旧格式注册：负载为裸节点标识，供尚未迁移的客户端使用
        /// </summary>
        /// <param name="sessionId">底层会话标识</param>
        /// <param name="content">Register 报文负载</param>
        private void ProcessLegacyRegister(string sessionId, ReadOnlyMemory<byte> content)
        {
            var nodeId = P2PContent.GetString(content);
            
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
        
        /// <summary>
        /// 校验鉴权应答：HMAC-SHA256(共享密钥, nonce + "|" + peerId) 的 Base64，
        /// 通过后标记会话已鉴权、回 AuthSuccess 并按配置下发 TURN 凭据。
        /// 旧客户端的裸字符串应答按既有行为直接放行，新客户端按 HMAC-SHA256 校验。
        /// </summary>
        /// <param name="sessionId">底层会话标识</param>
        /// <param name="content">AuthResponse 报文负载</param>
        private void ProcessAuthResponse(string sessionId, ReadOnlyMemory<byte> content)
        {
            // 旧客户端回的是裸字符串而非 JSON，保持迁移期可用：沿用旧行为直接放行
            if (!LooksLikeJson(content))
            {
                if (_pendingChallenges.TryRemove(sessionId, out _))
                {
                    _serverSocket.SendAsync(sessionId, _coder.EncodeP2P(P2PMessageType.AuthSuccess));
                    P2PLogHelper.Info("P2PServer", "Legacy auth response accepted");
                }
                return;
            }

            if (!_pendingChallenges.TryGetValue(sessionId, out var challenge))
            {
                SendError(sessionId, ErrorCode.AuthFailed, "没有待处理的鉴权挑战");
                return;
            }

            var peerId = _sessionToNode.TryGetValue(sessionId, out var nodeId) ? nodeId : null;
            if (peerId == null)
            {
                SendError(sessionId, ErrorCode.RegisterAuthFailed, "会话尚未注册");
                return;
            }

            var message = SignalingJsonCodec.Decode(content.ToArray());
            if (message.Type == P2PMessageType.Error || !VerifyAuthResponse(challenge.ChallengeData, peerId, message.Response))
            {
                SendError(sessionId, ErrorCode.RegisterAuthFailed, "鉴权应答校验失败");
                DisconnectAfterGrace(sessionId);
                return;
            }

            _pendingChallenges.TryRemove(sessionId, out _);
            _authedSessions[sessionId] = 1;

            SendSignaling(sessionId, SignalingMessage.Create(P2PMessageType.AuthSuccess));
            SendTurnCredential(sessionId, peerId);
            P2PLogHelper.Info("P2PServer", $"Signaling node authenticated: {peerId}");
        }

        /// <summary>
        /// 判断负载首个非空白字节是否为左花括号，用于区分新 JSON 报文与旧裸负载
        /// </summary>
        /// <param name="content">报文负载</param>
        /// <returns>是 JSON 对象时返回 true</returns>
        private static bool LooksLikeJson(ReadOnlyMemory<byte> content)
        {
            var span = content.Span;
            for (var i = 0; i < span.Length; i++)
            {
                var current = span[i];
                if (current == (byte)' ' || current == (byte)'\t' || current == (byte)'\r' || current == (byte)'\n')
                {
                    continue;
                }
                return current == (byte)'{';
            }
            return false;
        }

        /// <summary>
        /// 按 HMAC-SHA256(共享密钥, nonce + "|" + peerId) 的 Base64 校验鉴权应答
        /// </summary>
        /// <param name="nonce">服务端下发的挑战随机数</param>
        /// <param name="peerId">节点标识</param>
        /// <param name="response">客户端回传的应答</param>
        /// <returns>与期望值完全一致时返回 true</returns>
        private bool VerifyAuthResponse(string nonce, string peerId, string response)
        {
            if (_authKeyBytes == null || string.IsNullOrEmpty(response))
            {
                return false;
            }

            using (var hmac = new HMACSHA256(_authKeyBytes))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(nonce + "|" + peerId));
                var expected = Convert.ToBase64String(hash);
                return string.Equals(expected, response, StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// 在下发条件满足时向已通过鉴权的会话发送 TURN 临时凭据
        /// </summary>
        /// <param name="sessionId">底层会话标识</param>
        /// <param name="peerId">节点标识</param>
        private void SendTurnCredential(string sessionId, string peerId)
        {
            if (_credentialService == null || _options.TurnUrls == null || _options.TurnUrls.Length == 0)
            {
                return;
            }

            var credential = _credentialService.Create(peerId);
            var message = SignalingMessage.Create(P2PMessageType.TurnCredential);
            message.PeerId = peerId;
            message.Realm = credential.Realm;
            message.Username = credential.Username;
            message.Password = credential.Password;
            message.Ttl = _credentialService.TtlSeconds;
            message.Urls = (string[])_options.TurnUrls.Clone();
            SendSignaling(sessionId, message);
        }

        /// <summary>
        /// 以 JSON 负载编码后按指定会话发送信令报文
        /// </summary>
        /// <param name="sessionId">底层会话标识</param>
        /// <param name="message">信令报文</param>
        private void SendSignaling(string sessionId, SignalingMessage message)
        {
            _serverSocket.SendAsync(sessionId, _coder.EncodeP2P(message.Type, SignalingJsonCodec.Encode(message)));
        }

        /// <summary>
        /// 向指定会话回一条 Error 报文
        /// </summary>
        /// <param name="sessionId">底层会话标识，为空时直接返回</param>
        /// <param name="code">错误码</param>
        /// <param name="text">错误说明</param>
        private void SendError(string sessionId, string code, string text)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                return;
            }
            SendSignaling(sessionId, SignalingJsonCodec.CreateError(code, text));
        }

        /// <summary>
        /// 延迟关闭会话，先让此前入队的错误报文发出去
        /// </summary>
        /// <param name="sessionId">底层会话标识，为空时直接返回</param>
        private void DisconnectAfterGrace(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                return;
            }

            // 发送是异步的，立即断开会让内核丢弃未落地的数据，这里留一个固定宽限窗口
            Task.Delay(DisconnectGraceMilliseconds).ContinueWith(t =>
            {
                try
                {
                    _serverSocket.Disconnect(sessionId);
                }
                catch (Exception ex)
                {
                    P2PLogHelper.Error("P2PServer", "DisconnectAfterGrace", "延迟断开失败", ex);
                }
            });
        }

        /// <summary>
        /// 处理 ConnectRequest：校验已鉴权与目标在线，登记会话后把请求转发给目标
        /// </summary>
        /// <param name="sessionId">底层会话标识</param>
        /// <param name="content">ConnectRequest 报文负载</param>
        private void ProcessConnectRequest(string sessionId, ReadOnlyMemory<byte> content)
        {
            if (!_authedSessions.ContainsKey(sessionId))
            {
                SendError(sessionId, ErrorCode.RegisterAuthFailed, "会话尚未通过鉴权");
                return;
            }

            var senderPeerId = _sessionToNode.TryGetValue(sessionId, out var sourceId) ? sourceId : null;
            if (senderPeerId == null)
            {
                SendError(sessionId, ErrorCode.RegisterPeerIdMissing, "会话尚未注册");
                return;
            }

            var message = SignalingJsonCodec.Decode(content.ToArray());
            if (message.Type == P2PMessageType.Error)
            {
                SendSignaling(sessionId, message);
                return;
            }

            var targetPeerId = message.TargetPeerId;
            if (string.IsNullOrEmpty(targetPeerId))
            {
                SendError(sessionId, ErrorCode.RegisterPeerIdMissing, "会话请求缺少目标节点标识");
                return;
            }

            // 不能与自己建会话，否则对端定位会落到本端
            if (string.Equals(targetPeerId, senderPeerId, StringComparison.Ordinal))
            {
                SendError(sessionId, ErrorCode.RegisterPeerIdInvalid, "不能与自身建立会话");
                return;
            }

            if (!_nodeToSession.TryGetValue(targetPeerId, out var targetSessionId) || !_nodes.ContainsKey(targetPeerId))
            {
                SendError(sessionId, ErrorCode.SessionTargetOffline, "目标节点不在线");
                return;
            }

            var signalSessionId = message.SessionId;
            if (string.IsNullOrEmpty(signalSessionId))
            {
                signalSessionId = Guid.NewGuid().ToString("N");
            }

            var entry = new SignalingSessionEntry
            {
                SessionId = signalSessionId,
                PeerA = senderPeerId,
                PeerB = targetPeerId
            };
            entry.Touch();
            _sessions[signalSessionId] = entry;

            var forward = SignalingMessage.Create(P2PMessageType.ConnectRequest);
            forward.PeerId = senderPeerId;
            forward.TargetPeerId = targetPeerId;
            forward.SessionId = signalSessionId;
            SendSignaling(targetSessionId, forward);

            P2PLogHelper.Info("P2PServer", $"Signaling session created: {signalSessionId} {senderPeerId} -> {targetPeerId}");
        }

        /// <summary>
        /// 按会话标识把一条会话内消息转发给对端，同时回填来源节点标识并刷新活动时间
        /// </summary>
        /// <param name="sessionId">底层会话标识</param>
        /// <param name="content">会话内消息负载</param>
        private void ForwardSessionMessage(string sessionId, ReadOnlyMemory<byte> content)
        {
            var senderPeerId = _sessionToNode.TryGetValue(sessionId, out var sourceId) ? sourceId : null;
            if (senderPeerId == null)
            {
                SendError(sessionId, ErrorCode.RegisterPeerIdMissing, "会话尚未注册");
                return;
            }

            var message = SignalingJsonCodec.Decode(content.ToArray());
            if (message.Type == P2PMessageType.Error)
            {
                SendSignaling(sessionId, message);
                return;
            }

            var signalSessionId = message.SessionId;
            if (string.IsNullOrEmpty(signalSessionId) || !_sessions.TryGetValue(signalSessionId, out var entry))
            {
                SendError(sessionId, ErrorCode.SessionNotFound, "信令会话不存在");
                return;
            }

            var targetPeerId = string.Equals(entry.PeerA, senderPeerId, StringComparison.Ordinal) ? entry.PeerB : entry.PeerA;
            if (!_nodeToSession.TryGetValue(targetPeerId, out var targetSessionId))
            {
                SendError(sessionId, ErrorCode.SessionTargetOffline, "对端节点不在线");
                return;
            }

            entry.Touch();
            message.PeerId = senderPeerId;
            message.SessionId = signalSessionId;
            SendSignaling(targetSessionId, message);
        }

        /// <summary>
        /// 处理 SessionClose：转发给对端并删除会话，重复拆除按幂等处理
        /// </summary>
        /// <param name="sessionId">底层会话标识</param>
        /// <param name="content">SessionClose 报文负载</param>
        private void ProcessSessionClose(string sessionId, ReadOnlyMemory<byte> content)
        {
            var senderPeerId = _sessionToNode.TryGetValue(sessionId, out var sourceId) ? sourceId : null;
            if (senderPeerId == null)
            {
                return;
            }

            var message = SignalingJsonCodec.Decode(content.ToArray());
            if (message.Type == P2PMessageType.Error)
            {
                return;
            }

            var signalSessionId = message.SessionId;
            if (string.IsNullOrEmpty(signalSessionId) || !_sessions.TryRemove(signalSessionId, out var entry))
            {
                // 已拆除的会话再次拆除按幂等处理，不回错误
                return;
            }

            var targetPeerId = string.Equals(entry.PeerA, senderPeerId, StringComparison.Ordinal) ? entry.PeerB : entry.PeerA;
            if (_nodeToSession.TryGetValue(targetPeerId, out var targetSessionId))
            {
                message.PeerId = senderPeerId;
                message.SessionId = signalSessionId;
                SendSignaling(targetSessionId, message);
            }

            P2PLogHelper.Info("P2PServer", $"Signaling session closed: {signalSessionId}");
        }

        /// <summary>
        /// 定时扫描会话表，把超过会话超时未活动的会话两端都通知拆除
        /// </summary>
        /// <param name="state">定时器状态参数，未使用</param>
        private void SweepSessions(object state)
        {
            try
            {
                var timeoutTicks = TimeSpan.FromMilliseconds(_options.Timeout.SessionTimeoutMs).Ticks;
                var now = DateTime.UtcNow.Ticks;

                foreach (var pair in _sessions)
                {
                    var entry = pair.Value;
                    if (now - entry.LastActiveTicks < timeoutTicks)
                    {
                        continue;
                    }

                    _sessions.TryRemove(entry.SessionId, out _);

                    SendSessionCloseToPeer(entry.PeerA, entry.SessionId, "会话超时");
                    SendSessionCloseToPeer(entry.PeerB, entry.SessionId, "会话超时");
                    P2PLogHelper.Info("P2PServer", $"Signaling session timeout: {entry.SessionId}");
                }
            }
            catch (Exception ex)
            {
                // 定时器回调里抛异常会打断后续扫描，这里兜住只记日志
                P2PLogHelper.Error("P2PServer", ErrorCode.SessionTimeout, "会话超时扫描失败", ex);
            }
        }

        /// <summary>
        /// 向指定节点所在会话发送一条 SessionClose
        /// </summary>
        /// <param name="peerId">节点标识</param>
        /// <param name="signalSessionId">信令会话标识</param>
        /// <param name="reason">拆除原因</param>
        private void SendSessionCloseToPeer(string peerId, string signalSessionId, string reason)
        {
            if (string.IsNullOrEmpty(peerId))
            {
                return;
            }

            if (!_nodeToSession.TryGetValue(peerId, out var targetSessionId))
            {
                return;
            }

            var message = SignalingMessage.Create(P2PMessageType.SessionClose);
            message.SessionId = signalSessionId;
            message.Reason = reason;
            SendSignaling(targetSessionId, message);
        }

        /// <summary>
        /// 拆除指定节点参与的全部信令会话，并通知对端
        /// </summary>
        /// <param name="nodeId">节点标识，为空时直接返回</param>
        private void RemoveSessionsOfNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return;
            }

            foreach (var pair in _sessions)
            {
                var entry = pair.Value;
                if (!string.Equals(entry.PeerA, nodeId, StringComparison.Ordinal) &&
                    !string.Equals(entry.PeerB, nodeId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!_sessions.TryRemove(entry.SessionId, out _))
                {
                    continue;
                }

                var otherPeerId = string.Equals(entry.PeerA, nodeId, StringComparison.Ordinal) ? entry.PeerB : entry.PeerA;
                SendSessionCloseToPeer(otherPeerId, entry.SessionId, "对端连接已断开");
            }
        }
        
        private void OnDisconnected(string sessionId, Exception ex)
        {
            _sessionCoders.TryRemove(sessionId, out _);
            _pendingChallenges.TryRemove(sessionId, out _);
            _authedSessions.TryRemove(sessionId, out _);

            var nodeId = _sessionToNode.TryGetValue(sessionId, out var id) ? id : null;
            if (nodeId != null)
            {
                _sessionToNode.TryRemove(sessionId, out _);

                // 节点标识可能已被新连接顶替，只有映射仍指向本会话时才删节点
                if (_nodeToSession.TryGetValue(nodeId, out var currentSessionId) && currentSessionId == sessionId)
                {
                    _nodeToSession.TryRemove(nodeId, out _);
                    _nodes.TryRemove(nodeId, out _);
                    OnNodeUnregistered?.Invoke(nodeId);
                    P2PLogHelper.Info("P2PServer", $"Node disconnected: {nodeId}");
                    BroadcastNodeList();
                }
            }

            // 断开连接时把该节点参与的信令会话一并拆除，避免对端继续等待
            RemoveSessionsOfNode(nodeId);
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
        
        /// <summary>
        /// 取已注册节点的信息
        /// </summary>
        /// <param name="nodeId">节点标识</param>
        /// <returns>节点信息，未注册时返回 null</returns>
        public NodeInfo GetNode(string nodeId)
        {
            return _nodes.TryGetValue(nodeId, out var node) ? node : null;
        }
        
        /// <summary>
        /// 一条信令会话的两端节点与最后活动时间
        /// </summary>
        private sealed class SignalingSessionEntry
        {
            /// <summary>
            /// 会话标识
            /// </summary>
            public string SessionId;

            /// <summary>
            /// 发起方节点标识
            /// </summary>
            public string PeerA;

            /// <summary>
            /// 应答方节点标识
            /// </summary>
            public string PeerB;

            private long _lastActiveTicks;

            /// <summary>
            /// 最后活动时间，UTC Tick
            /// </summary>
            public long LastActiveTicks
            {
                get { return Interlocked.Read(ref _lastActiveTicks); }
            }

            /// <summary>
            /// 把最后活动时间刷成当前 UTC 时间
            /// </summary>
            public void Touch()
            {
                Interlocked.Exchange(ref _lastActiveTicks, DateTime.UtcNow.Ticks);
            }
        }
    }
}