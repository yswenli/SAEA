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
*修改时间：2026/11/13 10:56:15
*修改人： yswenli
*版本号： v26.11.5.1
*描述：按信令与 ICE 重写客户端编排
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using SAEA.P2P.Builder;
using SAEA.P2P.Common;
using SAEA.P2P.Ice;
using SAEA.P2P.Model;
using SAEA.P2P.Protocol;
using SAEA.P2P.Signaling;
using SAEA.P2P.Turn;

namespace SAEA.P2P.Core
{
    /// <summary>
    /// P2P 客户端：用信令客户端完成会话协商，用 ICE 会话完成候选收集与选路，并对外提供收发与断开入口
    /// </summary>
    public sealed class P2PClient : IDisposable
    {
        private readonly SignalingClient _signaling;
        private readonly SignalingOptions _signalingOptions;
        private readonly IceSessionOptions _iceOptions;
        private readonly CancellationTokenSource _lifetimeCts;
        private readonly ConcurrentDictionary<string, PeerSession> _sessionsById;
        private readonly ConcurrentDictionary<string, PeerSession> _sessionsByPeer;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<SignalingMessage>> _pendingAnswers;
        // 候选早于会话描述到达时先暂存，挂载 ICE 会话后统一补加
        private readonly ConcurrentDictionary<string, List<Candidate>> _pendingCandidates;
        private volatile bool _registered;
        private int _disposed;

        /// <summary>
        /// 本端节点标识，取自信令配置
        /// </summary>
        public string PeerId => _signalingOptions.PeerId;

        /// <summary>
        /// 是否已注册到信令服务器
        /// </summary>
        public bool IsRegistered => _registered;

        /// <summary>
        /// 对端会话建立并选出通道时触发
        /// </summary>
        public event Action<P2PConnection> OnPeerConnected;

        /// <summary>
        /// 对端会话关闭时触发，参数为对端标识与关闭原因
        /// </summary>
        public event Action<string, string> OnPeerDisconnected;

        /// <summary>
        /// 发现对端节点时触发
        /// </summary>
        public event Action<NodeInfo> OnPeerDiscovered;

        /// <summary>
        /// 收到业务帧时触发，载荷仅在回调期间有效，跨回调保存必须复制
        /// </summary>
        public event P2PDataReceivedHandler OnDataReceived;

        /// <summary>
        /// 发生错误时触发，参数为错误码与中文说明
        /// </summary>
        public event Action<string, string> OnError;

        /// <summary>
        /// 按信令与 ICE 配置创建客户端，构造时完成配置校验
        /// </summary>
        /// <param name="signaling">信令配置，不可为 null，且节点标识不能为空白</param>
        /// <param name="ice">ICE 会话配置，不可为 null</param>
        /// <exception cref="ArgumentNullException">任一配置为 null</exception>
        /// <exception cref="P2PException">节点标识为空，或信令配置校验失败</exception>
        public P2PClient(SignalingOptions signaling, IceSessionOptions ice)
        {
            if (signaling == null) throw new ArgumentNullException(nameof(signaling));
            if (ice == null) throw new ArgumentNullException(nameof(ice));
            if (string.IsNullOrWhiteSpace(signaling.PeerId)) throw new P2PException(ErrorCode.RegisterPeerIdMissing, "节点标识不能为空");

            _signalingOptions = signaling;
            _iceOptions = ice;
            _lifetimeCts = new CancellationTokenSource();
            _sessionsById = new ConcurrentDictionary<string, PeerSession>(StringComparer.Ordinal);
            _sessionsByPeer = new ConcurrentDictionary<string, PeerSession>(StringComparer.Ordinal);
            _pendingAnswers = new ConcurrentDictionary<string, TaskCompletionSource<SignalingMessage>>(StringComparer.Ordinal);
            _pendingCandidates = new ConcurrentDictionary<string, List<Candidate>>(StringComparer.Ordinal);

            _signaling = new SignalingClient(signaling);
            _signaling.OnRegistered += HandleRegistered;
            _signaling.OnConnectRequest += HandleConnectRequest;
            _signaling.OnSessionOffer += HandleSessionOffer;
            _signaling.OnSessionAnswer += HandleSessionAnswer;
            _signaling.OnTrickleCandidate += HandleTrickleCandidate;
            _signaling.OnTurnCredential += HandleTurnCredential;
            _signaling.OnSessionClose += HandleSessionClose;
            _signaling.OnDisconnected += HandleDisconnected;
            _signaling.OnError += HandleSignalingError;
        }

        /// <summary>
        /// 用总配置创建客户端，信令与 ICE 子配置按默认值补齐
        /// </summary>
        /// <param name="options">总配置，不可为 null</param>
        public P2PClient(P2POptions options) : this(CreateSignalingOptions(options), new IceSessionOptions())
        {
        }

        /// <summary>
        /// 连接信令服务器并完成注册
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>注册成功后的本端节点信息</returns>
        /// <exception cref="P2PException">连接失败、注册超时或服务端返回错误</exception>
        public async Task<NodeInfo> RegisterAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await _signaling.RegisterAsync(cancellationToken).ConfigureAwait(false);
            _registered = true;
            return new NodeInfo
            {
                NodeId = PeerId, NodeName = _signalingOptions.NodeName, State = NodeState.Registered,
                RegisteredTime = DateTime.UtcNow, LastActiveTime = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 与指定对端建立 P2P 连接：先经信令交换 ICE 参数，再由 ICE 会话选出可用通道
        /// </summary>
        /// <param name="peerId">对端节点标识，不可为空白</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>已建立的连接描述</returns>
        /// <exception cref="ArgumentException">peerId 为空白</exception>
        /// <exception cref="P2PException">尚未注册、等待应答超时或选路失败</exception>
        public async Task<P2PConnection> ConnectToPeerAsync(string peerId, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(peerId)) throw new ArgumentException("对端标识不能为空", nameof(peerId));
            if (!_registered) throw new P2PException(ErrorCode.NotConnected, "尚未注册到信令服务器");

            PeerSession current;
            if (_sessionsByPeer.TryGetValue(peerId, out current) && current.Phase == PeerSession.PeerSessionState.Connected)
                return current.Connection;

            var sessionId = Guid.NewGuid().ToString("N");
            var session = new PeerSession(sessionId, peerId, true, CreateUfrag(), CreatePassword(), IceRole.Controlling, CreateTiebreaker());
            _sessionsById[sessionId] = session;
            _sessionsByPeer[peerId] = session;
            var answerTcs = new TaskCompletionSource<SignalingMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingAnswers[sessionId] = answerTcs;

            IceSession ice = null;
            try
            {
                // 服务端要先由 ConnectRequest 为会话登记收发双方，否则后续会话内消息会被直接丢弃
                await _signaling.SendAsync(new SignalingMessage
                {
                    Type = P2PMessageType.ConnectRequest, PeerId = PeerId, TargetPeerId = peerId, SessionId = sessionId
                }, cancellationToken).ConfigureAwait(false);

                await _signaling.SendAsync(new SignalingMessage
                {
                    Type = P2PMessageType.SessionOffer, SessionId = sessionId, PeerId = PeerId,
                    Ufrag = session.LocalUfrag, Pwd = session.LocalPassword, Role = "controlling",
                    Tiebreaker = session.Tiebreaker, Candidates = new SignalingCandidate[0], EndOfCandidates = false
                }, cancellationToken).ConfigureAwait(false);

                var budget = _iceOptions.SessionBudget + TimeSpan.FromSeconds(5);
                var completed = await Task.WhenAny(answerTcs.Task, Task.Delay(budget, cancellationToken)).ConfigureAwait(false);
                if (completed != answerTcs.Task)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new P2PException(ErrorCode.SessionTimeout, "等待对端应答超时");
                }

                var answer = await answerTcs.Task.ConfigureAwait(false);
                var parameters = CreateParameters(session, answer.Ufrag, answer.Pwd, IceRole.Controlling);
                AddCandidates(parameters, answer.Candidates);
                ice = new IceSession(_iceOptions, parameters);
                AttachIceHandlers(session, ice);
                session.AttachIceSession(ice, parameters);
                DrainPendingCandidates(sessionId, parameters, ice);

                session.MarkPhase(PeerSession.PeerSessionState.Checking);
                var result = await ice.StartAsync(cancellationToken).ConfigureAwait(false);
                session.MarkConnected(result);
                OnPeerConnected?.Invoke(session.Connection);
                return session.Connection;
            }
            catch
            {
                if (ice != null) { try { ice.Dispose(); } catch { } }
                CloseAndRemove(session);
                throw;
            }
            finally
            {
                _pendingAnswers.TryRemove(sessionId, out _);
            }
        }

        // 具名方法才能在释放时退订，匿名订阅无法退订
        private void HandleRegistered(SignalingMessage message)
        {
            _registered = true;
        }

        private void HandleDisconnected(string reason)
        {
            _registered = false;
            // 数据面由 ICE 会话独立维护，信令断开只影响后续协商，不拆已建立的会话
            P2PLogHelper.Warning(PeerId, "信令连接已断开：" + reason);
        }

        private void HandleSignalingError(string code, string message)
        {
            var errorCode = string.IsNullOrEmpty(code) ? ErrorCode.RegisterFailed : code;
            var text = string.IsNullOrEmpty(message) ? "信令错误" : message;
            // 在途请求失去信令通道后不会再有应答，逐个唤醒避免调用方一直等到超时
            foreach (var item in _pendingAnswers)
            {
                TaskCompletionSource<SignalingMessage> pending;
                if (_pendingAnswers.TryRemove(item.Key, out pending))
                    pending.TrySetException(new P2PException(errorCode, text));
            }
            OnError?.Invoke(code, message);
        }

        private void HandleConnectRequest(SignalingMessage message)
        {
            if (message == null || string.IsNullOrWhiteSpace(message.SessionId) || string.IsNullOrWhiteSpace(message.PeerId))
            {
                P2PLogHelper.Warning(PeerId, "连接请求缺少会话标识或对端标识，已忽略");
                return;
            }

            if (_sessionsById.ContainsKey(message.SessionId)) return;

            var session = new PeerSession(message.SessionId, message.PeerId, false, CreateUfrag(), CreatePassword(), IceRole.Controlled, CreateTiebreaker());
            if (_sessionsById.TryAdd(session.SessionId, session)) _sessionsByPeer[session.PeerId] = session;
        }

        private void HandleSessionOffer(SignalingMessage message)
        {
            if (message == null || string.IsNullOrWhiteSpace(message.SessionId))
            {
                P2PLogHelper.Warning(PeerId, "会话描述缺少会话标识，已忽略");
                return;
            }

            PeerSession session;
            if (!_sessionsById.TryGetValue(message.SessionId, out session))
            {
                if (string.IsNullOrWhiteSpace(message.PeerId))
                {
                    P2PLogHelper.Warning(PeerId, "会话描述缺少对端标识，已忽略");
                    return;
                }
                session = new PeerSession(message.SessionId, message.PeerId, false, CreateUfrag(), CreatePassword(), IceRole.Controlled, CreateTiebreaker());
                _sessionsById[session.SessionId] = session;
                _sessionsByPeer[session.PeerId] = session;
            }

            IceSession ice = null;
            try
            {
                var parameters = CreateParameters(session, message.Ufrag, message.Pwd, IceRole.Controlled);
                AddCandidates(parameters, message.Candidates);
                ice = new IceSession(_iceOptions, parameters);
                AttachIceHandlers(session, ice);
                session.AttachIceSession(ice, parameters);
                DrainPendingCandidates(session.SessionId, parameters, ice);

                // 先回本端参数再启动选路，对端拿到 ufrag 与口令后才能开始连通性检查
                _ = SendAndLogAsync(new SignalingMessage
                {
                    Type = P2PMessageType.SessionAnswer, SessionId = session.SessionId, PeerId = PeerId,
                    Ufrag = session.LocalUfrag, Pwd = session.LocalPassword, Role = "controlled",
                    Tiebreaker = session.Tiebreaker, Candidates = new SignalingCandidate[0], EndOfCandidates = false
                }, "会话应答");
                _ = StartInboundAsync(session, _lifetimeCts.Token);
            }
            catch (Exception ex)
            {
                // 入向报文异常不能打断接收线程，也不能留下没有被挂载的会话
                P2PLogHelper.Error(PeerId, ErrorCode.ProtocolMalformed, "处理会话描述失败", ex);
                if (ice != null) { try { ice.Dispose(); } catch { } }
                CloseAndRemove(session);
            }
        }

        private void HandleSessionAnswer(SignalingMessage message)
        {
            if (message == null || string.IsNullOrEmpty(message.SessionId)) return;
            TaskCompletionSource<SignalingMessage> pending;
            if (_pendingAnswers.TryGetValue(message.SessionId, out pending)) pending.TrySetResult(message);
        }

        private void HandleTrickleCandidate(SignalingMessage message)
        {
            if (message == null || string.IsNullOrEmpty(message.SessionId) || message.Candidates == null || message.Candidates.Length == 0)
                return;

            PeerSession session;
            if (!_sessionsById.TryGetValue(message.SessionId, out session))
            {
                P2PLogHelper.Warning(PeerId, "收到未知会话的候选，已丢弃：" + message.SessionId);
                return;
            }

            for (int i = 0; i < message.Candidates.Length; i++)
            {
                Candidate candidate;
                if (!SignalingCandidateMapper.TryToCandidate(message.Candidates[i], out candidate))
                {
                    P2PLogHelper.Warning(PeerId, "收到非法的远端候选，已跳过");
                    continue;
                }

                var parameters = session.Parameters;
                var ice = session.Ice;
                if (parameters == null && ice == null)
                {
                    // 会话还在等对端描述，先暂存，避免候选丢失
                    var pending = _pendingCandidates.GetOrAdd(message.SessionId, id => new List<Candidate>());
                    lock (pending) { pending.Add(candidate); }
                    continue;
                }

                if (parameters != null) parameters.AddRemoteCandidate(candidate);
                if (ice != null) ice.AddRemoteCandidate(candidate);
            }
        }

        private void HandleTurnCredential(SignalingMessage message)
        {
            var turn = _iceOptions.Turn;
            if (message == null || turn == null)
            {
                P2PLogHelper.Warning(PeerId, "未配置 TURN，忽略下发的临时凭据");
                return;
            }

            // 日志只记领域与有效期，用户名与口令都属于凭据，不能落盘
            if (string.IsNullOrWhiteSpace(message.Username) || string.IsNullOrWhiteSpace(message.Password)
                || string.IsNullOrWhiteSpace(message.Realm) || message.Ttl <= 0)
            {
                P2PLogHelper.Warning(PeerId, "TURN 临时凭据不完整，已忽略：realm=" + message.Realm + "，ttl=" + message.Ttl);
                return;
            }

            turn.Username = null;
            turn.Password = null;
            var expiresAt = DateTimeOffset.UtcNow.AddSeconds(message.Ttl);
            turn.Credential = new TurnCredential(message.Username, message.Password, message.Realm, message.Username, expiresAt, expiresAt.ToUnixTimeSeconds());
            P2PLogHelper.Info(PeerId, "已更新 TURN 临时凭据：realm=" + message.Realm + "，ttl=" + message.Ttl);
        }

        private void HandleSessionClose(SignalingMessage message)
        {
            if (message == null || string.IsNullOrEmpty(message.SessionId)) return;
            PeerSession session;
            if (!_sessionsById.TryGetValue(message.SessionId, out session)) return;

            var peerId = session.PeerId;
            CloseAndRemove(session);
            OnPeerDisconnected?.Invoke(peerId, message.Reason ?? "对端已关闭会话");
        }

        private async Task StartInboundAsync(PeerSession session, CancellationToken token)
        {
            try
            {
                session.MarkPhase(PeerSession.PeerSessionState.Checking);
                var result = await session.Ice.StartAsync(token).ConfigureAwait(false);
                session.MarkConnected(result);
                OnPeerConnected?.Invoke(session.Connection);
            }
            catch (OperationCanceledException)
            {
                CloseAndRemove(session);
            }
            catch (ObjectDisposedException)
            {
                CloseAndRemove(session);
            }
            catch (P2PException ex)
            {
                P2PLogHelper.Error(PeerId, ex.ErrorCode, "入向会话选路失败：" + ex.Message);
                OnError?.Invoke(ex.ErrorCode, ex.Message);
                CloseAndRemove(session);
            }
            catch (Exception ex)
            {
                // 后台任务上的异常无人观察，这里兜住并通知调用方
                P2PLogHelper.Error(PeerId, ErrorCode.NoCandidatePair, "入向会话选路异常", ex);
                OnError?.Invoke(ErrorCode.NoCandidatePair, ex.Message);
                CloseAndRemove(session);
            }
        }

        private void OnLocalCandidateDiscovered(PeerSession session, Candidate candidate)
        {
            if (candidate == null) return;
            _ = SendAndLogAsync(new SignalingMessage
            {
                Type = P2PMessageType.TrickleCandidate, SessionId = session.SessionId, PeerId = PeerId,
                Candidates = new[] { SignalingCandidateMapper.ToSignaling(candidate) }
            }, "本地候选");
        }

        /// <summary>
        /// 向已建立连接的对端发送业务帧
        /// </summary>
        /// <param name="peerId">对端节点标识</param>
        /// <param name="data">业务载荷，仅在调用期间读取</param>
        /// <exception cref="P2PException">与对端的通道尚未建立</exception>
        public void Send(string peerId, ReadOnlySpan<byte> data)
        {
            ThrowIfDisposed();
            PeerSession session;
            if (string.IsNullOrEmpty(peerId) || !_sessionsByPeer.TryGetValue(peerId, out session)
                || session.Phase != PeerSession.PeerSessionState.Connected || session.Ice == null)
            {
                throw new P2PException(ErrorCode.NotConnected, "与对端的通道尚未建立");
            }

            session.Ice.Send(data);
            session.Active();
        }

        /// <summary>
        /// 关闭与指定对端的会话并通知对端；会话不存在时直接返回
        /// </summary>
        /// <param name="peerId">对端节点标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>关闭完成的任务</returns>
        public async Task DisconnectPeerAsync(string peerId, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(peerId)) return;
            PeerSession session;
            if (!_sessionsByPeer.TryGetValue(peerId, out session)) return;

            try
            {
                await _signaling.SendAsync(new SignalingMessage
                {
                    Type = P2PMessageType.SessionClose, SessionId = session.SessionId, PeerId = PeerId, Reason = "本端关闭"
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 关闭通知尽力而为，发送失败不能阻止本地会话清理
                P2PLogHelper.Warning(PeerId, "发送会话关闭报文失败：" + ex.GetType().Name);
            }

            CloseAndRemove(session);
            OnPeerDisconnected?.Invoke(peerId, "本端已关闭会话");
        }

        /// <summary>
        /// 释放客户端：取消生命周期令牌、退订信令事件、关闭全部会话并释放信令连接；可重复调用
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

            try { _lifetimeCts.Cancel(); } catch (ObjectDisposedException) { }

            _signaling.OnRegistered -= HandleRegistered;
            _signaling.OnConnectRequest -= HandleConnectRequest;
            _signaling.OnSessionOffer -= HandleSessionOffer;
            _signaling.OnSessionAnswer -= HandleSessionAnswer;
            _signaling.OnTrickleCandidate -= HandleTrickleCandidate;
            _signaling.OnTurnCredential -= HandleTurnCredential;
            _signaling.OnSessionClose -= HandleSessionClose;
            _signaling.OnDisconnected -= HandleDisconnected;
            _signaling.OnError -= HandleSignalingError;

            foreach (var item in _sessionsById) CloseAndRemove(item.Value);

            _lifetimeCts.Dispose();
            _signaling.Dispose();
            _sessionsById.Clear();
            _sessionsByPeer.Clear();
            _pendingAnswers.Clear();
            _pendingCandidates.Clear();
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0) throw new ObjectDisposedException(nameof(P2PClient));
        }

        private void CloseAndRemove(PeerSession session)
        {
            if (session == null) return;
            var ice = session.Ice;
            if (ice != null) { try { ice.Dispose(); } catch { } }
            session.Close();
            _sessionsById.TryRemove(session.SessionId, out _);
            _sessionsByPeer.TryRemove(session.PeerId, out _);
        }

        private void AttachIceHandlers(PeerSession session, IceSession ice)
        {
            // 闭包捕获会话，回调里不必再按会话标识反查，也不会因字典清理丢失目标
            ice.LocalCandidateDiscovered += candidate => OnLocalCandidateDiscovered(session, candidate);
            ice.FrameReceived += frame => OnDataReceived?.Invoke(session.PeerId, frame.Span);
            ice.RouteSelected += result => P2PLogHelper.Info(PeerId, "对端 " + session.PeerId + " 选路完成：" + result);
        }

        private void DrainPendingCandidates(string sessionId, IceSessionParameters parameters, IceSession ice)
        {
            List<Candidate> pending;
            if (!_pendingCandidates.TryRemove(sessionId, out pending)) return;
            lock (pending)
            {
                for (int i = 0; i < pending.Count; i++)
                {
                    parameters.AddRemoteCandidate(pending[i]);
                    ice.AddRemoteCandidate(pending[i]);
                }
            }
        }

        private async Task SendAndLogAsync(SignalingMessage message, string subject)
        {
            try
            {
                await _signaling.SendAsync(message, _lifetimeCts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 候选与应答都是辅助报文，发送失败只记日志，不打断选路
                P2PLogHelper.Warning(PeerId, subject + "发送失败：" + ex.GetType().Name);
            }
        }

        private static SignalingOptions CreateSignalingOptions(P2POptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            var timeout = options.Timeout ?? new TimeoutOptions();
            var encryption = options.Encryption ?? new EncryptionOptions();
            return new SignalingOptions
            {
                ServerAddress = options.ServerAddress, ServerPort = options.ServerPort, PeerId = options.NodeId,
                SharedSecret = options.NodeIdPassword, ConnectTimeoutMs = timeout.ConnectTimeoutMs,
                HeartbeatIntervalMs = timeout.HeartbeatIntervalMs, EnableTls = encryption.TlsEnabled,
                TlsTargetHost = options.ServerAddress
            };
        }

        private static IceSessionParameters CreateParameters(PeerSession session, string remoteUfrag, string remotePassword, IceRole role)
        {
            return new IceSessionParameters
            {
                LocalUfrag = session.LocalUfrag, LocalPassword = session.LocalPassword,
                RemoteUfrag = remoteUfrag, RemotePassword = remotePassword,
                Role = role, Tiebreaker = session.Tiebreaker, Component = 1
            };
        }

        private static void AddCandidates(IceSessionParameters parameters, SignalingCandidate[] candidates)
        {
            if (candidates == null) return;
            for (int i = 0; i < candidates.Length; i++)
            {
                Candidate candidate;
                if (SignalingCandidateMapper.TryToCandidate(candidates[i], out candidate))
                    parameters.AddRemoteCandidate(candidate);
            }
        }

        private static string CreateUfrag()
        {
            return CreateRandomToken(6).Substring(0, 8);
        }

        private static string CreatePassword()
        {
            return CreateRandomToken(18).Substring(0, 24);
        }

        private static ulong CreateTiebreaker()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var buffer = new byte[8];
                rng.GetBytes(buffer);
                return BitConverter.ToUInt64(buffer, 0);
            }
        }

        private static string CreateRandomToken(int byteCount)
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var buffer = new byte[byteCount];
                rng.GetBytes(buffer);
                // 换成 URL 安全字符集：信令 JSON 与 SDP 风格参数都不需要再转义
                return Convert.ToBase64String(buffer).Replace("=", string.Empty).Replace('+', '-').Replace('/', '_');
            }
        }
    }
}