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
*文件名： PeerSession
*版本号： v26.4.23.1
*唯一标识：15f52bf2-21a3-4cd9-afee-dba466531a77
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 15:44:01
*描述：
*
*=====================================================================
*修改标记
*修改时间：2026/04/20 15:44:01
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;
using SAEA.P2P.Channel;
using SAEA.P2P.Common;

namespace SAEA.P2P.Core
{
    public class PeerSession
    {
        public string SessionId { get; }
        
        public string PeerId { get; }
        
        public NodeState State { get; set; } = NodeState.Init;
        
        public string PublicAddress { get; set; }
        
        public int PublicPort { get; set; }
        
        public string LocalAddress { get; set; }
        
        public int LocalPort { get; set; }
        
        public ChannelType Channel { get; set; } = ChannelType.Direct;
        
        public DateTime CreatedTime { get; } = DateTime.UtcNow;
        
        public DateTime LastActiveTime { get; private set; } = DateTime.UtcNow;
        
        public long SendSequence { get; private set; }
        
        public long ReceiveSequence { get; set; }
        
        public string RelaySessionId { get; set; }
        
        private readonly object _lock = new object();
        
        public PeerSession(string sessionId, string peerId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));
            }
            
            if (string.IsNullOrWhiteSpace(peerId))
            {
                throw new ArgumentException("Peer ID cannot be null or empty", nameof(peerId));
            }
            
            SessionId = sessionId;
            PeerId = peerId;
        }
        
        public bool IsExpired(int timeoutMs)
        {
            lock (_lock)
            {
                return (DateTime.UtcNow - LastActiveTime).TotalMilliseconds > timeoutMs;
            }
        }
        
        public void Active()
        {
            lock (_lock)
            {
                LastActiveTime = DateTime.UtcNow;
            }
        }
        
        public long NextSendSeq()
        {
            lock (_lock)
            {
                return ++SendSequence;
            }
        }
        
        public long NextReceiveSeq()
        {
            lock (_lock)
            {
                return ++ReceiveSequence;
            }
        }
        
        public string GetPublicEndPoint()
        {
            if (string.IsNullOrEmpty(PublicAddress))
            {
                return null;
            }
            return $"{PublicAddress}:{PublicPort}";
        }
        
        public string GetLocalEndPoint()
        {
            if (string.IsNullOrEmpty(LocalAddress))
            {
                return null;
            }
            return $"{LocalAddress}:{LocalPort}";
        }
        
        public override string ToString()
        {
            return $"PeerSession[SessionId={SessionId}, PeerId={PeerId}, State={State}, Channel={Channel}]";
        }
    }
}