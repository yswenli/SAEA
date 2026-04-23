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
*命名空间：SAEA.P2P.Model
*文件名： NodeInfo
*版本号： v26.4.23.1
*唯一标识：1ba7e980-33d4-4edd-8b85-9113c86245a5
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