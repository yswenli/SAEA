using System;
using System.Collections.Generic;
using System.Text;
using SAEA.P2P.Common;
using SAEA.P2P.NAT;

namespace SAEA.P2P.Model
{
    public class NodeInfo
    {
        public string NodeId { get; set; }
        
        public string NodeName { get; set; }
        
        public string PublicAddress { get; set; }
        
        public int PublicPort { get; set; }
        
        public string LocalAddress { get; set; }
        
        public int LocalPort { get; set; }
        
        public NATType NatType { get; set; } = NATType.Unknown;
        
        public DateTime RegisteredTime { get; set; } = DateTime.UtcNow;
        
        public DateTime LastActiveTime { get; set; } = DateTime.UtcNow;
        
        public Dictionary<string, string> Services { get; set; } = new Dictionary<string, string>();
        
        public NodeState State { get; set; } = NodeState.Init;
        
        public bool IsOnline
        {
            get
            {
                return State == NodeState.Registered || 
                       State == NodeState.Connected ||
                       State == NodeState.Idle;
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
            var sb = new StringBuilder();
            sb.Append($"NodeInfo[NodeId={NodeId}");
            sb.Append($", NodeName={NodeName}");
            sb.Append($", Public={GetPublicEndPoint()}");
            sb.Append($", Local={GetLocalEndPoint()}");
            sb.Append($", NAT={NatType}");
            sb.Append($", State={State}");
            sb.Append($", Online={IsOnline}");
            sb.Append("]");
            return sb.ToString();
        }
        
        public NodeInfo Clone()
        {
            return new NodeInfo
            {
                NodeId = NodeId,
                NodeName = NodeName,
                PublicAddress = PublicAddress,
                PublicPort = PublicPort,
                LocalAddress = LocalAddress,
                LocalPort = LocalPort,
                NatType = NatType,
                RegisteredTime = RegisteredTime,
                LastActiveTime = LastActiveTime,
                State = State
            };
        }
    }
}