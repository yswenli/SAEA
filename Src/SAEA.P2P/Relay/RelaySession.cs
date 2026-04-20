using System;

namespace SAEA.P2P.Relay
{
    public class RelaySession
    {
        public string SessionId { get; set; }
        public string SourceNodeId { get; set; }
        public string TargetNodeId { get; set; }
        public RelayState State { get; set; } = RelayState.Pending;
        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
        public DateTime LastActiveTime { get; set; } = DateTime.UtcNow;
        public long BytesTransferred { get; set; }
        public long MaxQuota { get; set; }
        
        public bool IsExpired(int timeoutMs)
        {
            return (DateTime.UtcNow - LastActiveTime).TotalMilliseconds > timeoutMs;
        }
        
        public bool IsOverQuota => BytesTransferred > MaxQuota;
        
        public void AddBytes(long count)
        {
            BytesTransferred += count;
            LastActiveTime = DateTime.UtcNow;
        }
        
        public void Activate()
        {
            State = RelayState.Active;
            LastActiveTime = DateTime.UtcNow;
        }
        
        public void Close()
        {
            State = RelayState.Closed;
        }
    }
    
    public enum RelayState
    {
        Pending = 0,
        Active = 1,
        Idle = 2,
        Closed = 3
    }
}