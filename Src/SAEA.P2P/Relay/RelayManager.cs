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
*命名空间：SAEA.P2P.Relay
*文件名： RelayManager
*版本号： v26.4.23.1
*唯一标识：819f6057-a9f6-4d17-9267-d54aa5a31361
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 16:10:37
*描述：RelayManager管理类
*
*=====================================================================
*修改标记
*修改时间：2026/04/20 16:10:37
*修改人： yswenli
*版本号： v26.4.23.1
*描述：RelayManager管理类
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using SAEA.P2P.Common;
using SAEA.P2P.Protocol;

namespace SAEA.P2P.Relay
{
    public class RelayManager
    {
        private ConcurrentDictionary<string, RelaySession> _sessions = new ConcurrentDictionary<string, RelaySession>();
        private P2PCoder _coder = new P2PCoder();
        private int _timeout;
        private long _defaultQuota;
        
        public event Action<RelaySession> OnRelayStarted;
        public event Action<RelaySession> OnRelayEnded;
        public event Action<string, string, byte[]> OnRelayData;
        
        public RelayManager(int timeout = 60000, long defaultQuota = 10 * 1024 * 1024)
        {
            _timeout = timeout;
            _defaultQuota = defaultQuota;
        }
        
        public RelaySession CreateSession(string sourceId, string targetId, long? quota = null)
        {
            var session = new RelaySession
            {
                SessionId = Guid.NewGuid().ToString("N"),
                SourceNodeId = sourceId,
                TargetNodeId = targetId,
                MaxQuota = quota ?? _defaultQuota,
                CreatedTime = DateTime.UtcNow
            };
            
            _sessions[session.SessionId] = session;
            P2PLogHelper.Info("RelayManager", $"Relay session created: {session.SessionId} ({sourceId} -> {targetId})");
            OnRelayStarted?.Invoke(session);
            return session;
        }
        
        public RelaySession GetSession(string sessionId)
        {
            return _sessions.TryGetValue(sessionId, out var session) ? session : null;
        }
        
        /// <summary>
        /// Track an existing relay session (created by the signal server) without
        /// generating a new id. Used by clients when they receive a RelayAck.
        /// </summary>
        public RelaySession AttachSession(string sessionId, string sourceId, string targetId, long? quota = null)
        {
            var session = new RelaySession
            {
                SessionId = sessionId,
                SourceNodeId = sourceId,
                TargetNodeId = targetId,
                MaxQuota = quota ?? _defaultQuota,
                CreatedTime = DateTime.UtcNow
            };
            session.Activate();
            
            _sessions[sessionId] = session;
            P2PLogHelper.Info("RelayManager", $"Relay session attached: {sessionId} ({sourceId} -> {targetId})");
            OnRelayStarted?.Invoke(session);
            return session;
        }
        
        public void ActivateSession(string sessionId)
        {
            var session = GetSession(sessionId);
            if (session != null)
            {
                session.Activate();
                P2PLogHelper.Debug("RelayManager", $"Relay session activated: {sessionId}");
            }
        }
        
        public byte[] EncodeRelayData(string sessionId, string sourceId, string targetId, byte[] payload)
        {
            var session = GetSession(sessionId);
            if (session == null)
                throw new P2PException(ErrorCode.RelaySessionNotFound, "Relay session not found");
            
            if (session.IsOverQuota)
                throw new P2PException(ErrorCode.RelayQuotaExceeded, "Relay quota exceeded");
            
            session.AddBytes(payload.Length);
            
            var content = System.Text.Encoding.UTF8.GetBytes(
                $"{sessionId}|{sourceId}|{targetId}|");
            var combined = new byte[content.Length + payload.Length];
            Buffer.BlockCopy(content, 0, combined, 0, content.Length);
            Buffer.BlockCopy(payload, 0, combined, content.Length, payload.Length);
            
            return _coder.EncodeP2P(P2PMessageType.RelayData, combined);
        }
        
        public (string sessionId, string sourceId, string targetId, byte[] payload) DecodeRelayData(byte[] data)
        {
            using (var frames = _coder.DecodeP2P(data))
            {
                foreach (var frame in frames.Frames)
                {
                    if ((P2PMessageType)frame.Type == P2PMessageType.RelayData && !frame.Content.IsEmpty)
                    {
                        // content layout: {sessionId}|{sourceId}|{targetId}|{payload}
                        var content = frame.Content;
                        var span = content.Span;
                        var first = span.IndexOf((byte)'|');
                        if (first <= 0) continue;
                        var second = P2PContent.IndexOf(span, (byte)'|', first + 1);
                        if (second < 0) continue;
                        var third = P2PContent.IndexOf(span, (byte)'|', second + 1);
                        if (third < 0) continue;
                        
                        var sessionId = P2PContent.GetString(content, 0, first);
                        var sourceId = P2PContent.GetString(content, first + 1, second - first - 1);
                        var targetId = P2PContent.GetString(content, second + 1, third - second - 1);
                        var payload = content.Slice(third + 1).ToArray();
                        
                        return (sessionId, sourceId, targetId, payload);
                    }
                }
            }
            return (null, null, null, null);
        }
        
        public void ProcessRelayData(byte[] data)
        {
            var (sessionId, sourceId, targetId, payload) = DecodeRelayData(data);
            if (sessionId != null)
            {
                var session = GetSession(sessionId);
                if (session != null && session.State == RelayState.Active)
                {
                    session.AddBytes(payload.Length);
                    OnRelayData?.Invoke(sessionId, targetId, payload);
                }
            }
        }
        
        public void CloseSession(string sessionId)
        {
            var session = GetSession(sessionId);
            if (session != null)
            {
                session.Close();
                _sessions.TryRemove(sessionId, out _);
                P2PLogHelper.Info("RelayManager", $"Relay session closed: {sessionId}, bytes: {session.BytesTransferred}");
                OnRelayEnded?.Invoke(session);
            }
        }
        
        public void CleanupExpiredSessions()
        {
            foreach (var kvp in _sessions)
            {
                if (kvp.Value.IsExpired(_timeout))
                {
                    CloseSession(kvp.Key);
                }
            }
        }
        
        public int ActiveSessionCount => _sessions.Count;
    }
}