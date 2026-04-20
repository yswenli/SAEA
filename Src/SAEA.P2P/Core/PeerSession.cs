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