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
*修改时间：2026/11/6 12:20:16
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using SAEA.P2P.Common;
using SAEA.P2P.Protocol;

namespace SAEA.P2P.Relay
{
    /// <summary>
    /// 中继会话管理：创建与跟踪中继会话、编解码中继数据帧、按配额限制并按空闲超时回收会话
    /// </summary>
    public class RelayManager
    {
        private ConcurrentDictionary<string, RelaySession> _sessions = new ConcurrentDictionary<string, RelaySession>();
        private P2PCoder _coder = new P2PCoder();
        private int _timeout;
        private long _defaultQuota;
        
        /// <summary>
        /// 会话创建或挂接成功时触发
        /// </summary>
        public event Action<RelaySession> OnRelayStarted;
        /// <summary>
        /// 会话关闭并从管理表移除后触发
        /// </summary>
        public event Action<RelaySession> OnRelayEnded;
        /// <summary>
        /// 收到会话状态为 Active 的中继数据时触发，参数依次为会话 ID、目标节点 ID、载荷
        /// </summary>
        public event Action<string, string, byte[]> OnRelayData;
        
        /// <summary>
        /// 按空闲超时与默认配额初始化中继管理器
        /// </summary>
        /// <param name="timeout">会话空闲超时毫秒数，超过该时长未收发数据的会话会被清理</param>
        /// <param name="defaultQuota">会话默认累计字节配额，小于等于 0 表示不限制</param>
        public RelayManager(int timeout = 60000, long defaultQuota = 10 * 1024 * 1024)
        {
            _timeout = timeout;
            _defaultQuota = defaultQuota;
        }
        
        /// <summary>
        /// 创建状态为 Pending 的中继会话，会话 ID 为新生成的 32 位十六进制串
        /// </summary>
        /// <param name="sourceId">发起中继的源节点标识</param>
        /// <param name="targetId">中继目标的节点标识</param>
        /// <param name="quota">会话累计字节配额，为 null 时取构造时的默认配额，小于等于 0 表示不限制</param>
        /// <returns>已写入管理表并触发 OnRelayStarted 的会话</returns>
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
        
        /// <summary>
        /// 按会话 ID 查询中继会话
        /// </summary>
        /// <param name="sessionId">中继会话 ID</param>
        /// <returns>匹配的会话；不存在时返回 null</returns>
        public RelaySession GetSession(string sessionId)
        {
            return _sessions.TryGetValue(sessionId, out var session) ? session : null;
        }
        
        /// <summary>
        /// 挂接信令服务端已分配的中继会话，沿用其会话 ID 且立即置为 Active，不生成新 ID
        /// </summary>
        /// <param name="sessionId">服务端分配的会话 ID，同 ID 的既有会话会被覆盖</param>
        /// <param name="sourceId">发起中继的源节点标识</param>
        /// <param name="targetId">中继目标的节点标识</param>
        /// <param name="quota">会话累计字节配额，为 null 时取构造时的默认配额，小于等于 0 表示不限制</param>
        /// <returns>已激活并触发 OnRelayStarted 的会话</returns>
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
        
        /// <summary>
        /// 将会话置为 Active 并刷新最后活跃时间；会话不存在时静默忽略
        /// </summary>
        /// <param name="sessionId">待激活的中继会话 ID</param>
        public void ActivateSession(string sessionId)
        {
            var session = GetSession(sessionId);
            if (session != null)
            {
                session.Activate();
                P2PLogHelper.Debug("RelayManager", $"Relay session activated: {sessionId}");
            }
        }
        
        /// <summary>
        /// 按 sessionId|sourceId|targetId|payload 布局打包中继数据帧，并累计会话发送字节数
        /// </summary>
        /// <param name="sessionId">中继会话 ID，会话不存在或已超配额时抛出 P2PException，且不会累计字节</param>
        /// <param name="sourceId">源节点标识，仅写入帧内容不参与路由</param>
        /// <param name="targetId">目标节点标识，既写入帧内容又作为 OnRelayData 的接收方参数</param>
        /// <param name="payload">载荷字节，不能为 null，长度会计入会话配额</param>
        /// <returns>编码后的 P2P 消息字节数组</returns>
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
        
        /// <summary>
        /// 解析中继数据帧，取第一个类型为 RelayData 且能被三个分隔符完整切分的帧
        /// </summary>
        /// <param name="data">完整的 P2P 消息字节，为 null 时按空数据处理</param>
        /// <returns>会话 ID、源节点 ID、目标节点 ID 与载荷副本；无合法帧时四项均为 null</returns>
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
        
        /// <summary>
        /// 处理收到的中继数据：会话存在且状态为 Active 时累计接收字节数并触发 OnRelayData
        /// </summary>
        /// <param name="data">完整的 P2P 消息字节</param>
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
        
        /// <summary>
        /// 将会话置为 Closed 并从管理表移除，随后触发 OnRelayEnded；会话不存在时静默忽略
        /// </summary>
        /// <param name="sessionId">待关闭的中继会话 ID</param>
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
        
        /// <summary>
        /// 关闭并移除所有空闲时间超过构造时 timeout 的会话
        /// </summary>
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
        
        /// <summary>
        /// 当前管理表中的会话数量，含 Pending 与 Active 会话
        /// </summary>
        public int ActiveSessionCount => _sessions.Count;
    }
}