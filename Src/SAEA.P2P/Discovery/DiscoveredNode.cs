using System;
using System.Net;

namespace SAEA.P2P.Discovery
{
    public class DiscoveredNode
    {
        public string NodeId { get; set; }
        public string NodeName { get; set; }
        public IPEndPoint LocalAddress { get; set; }
        public string[] Services { get; set; }
        public DateTime DiscoveredTime { get; set; } = DateTime.UtcNow;
        public DateTime LastSeenTime { get; set; } = DateTime.UtcNow;

        public bool IsExpired(int timeoutMs)
        {
            return (DateTime.UtcNow - LastSeenTime).TotalMilliseconds > timeoutMs;
        }

        public void Refresh()
        {
            LastSeenTime = DateTime.UtcNow;
        }

        public override string ToString()
        {
            return $"DiscoveredNode: {NodeId} at {LocalAddress}";
        }
    }
}