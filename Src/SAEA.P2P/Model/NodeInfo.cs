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
*唯一标识：ebe5037d-a1a2-43a5-bf52-ddc463cc596e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 15:44:01
*描述：NodeInfo接口
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
using System.Collections.Generic;
using System.Text;
using SAEA.P2P.Common;
using SAEA.P2P.NAT;

namespace SAEA.P2P.Model
{
    /// <summary>
    /// 对端节点信息，服务器登记并在节点列表与打洞通知中下发
    /// </summary>
    public class NodeInfo
    {
        /// <summary>
        /// 节点唯一标识，服务器据此索引与寻址对端
        /// </summary>
        public string NodeId { get; set; }
        
        /// <summary>
        /// 节点显示名称，仅用于日志与界面展示
        /// </summary>
        public string NodeName { get; set; }
        
        /// <summary>
        /// NAT 映射后的公网 IP 文本，由服务器按注册来源地址回填；未注册时为 null
        /// </summary>
        public string PublicAddress { get; set; }
        
        /// <summary>
        /// NAT 映射后的公网端口；未注册时为 0
        /// </summary>
        public int PublicPort { get; set; }
        
        /// <summary>
        /// 节点内网 IP 文本，供同网段直连使用
        /// </summary>
        public string LocalAddress { get; set; }
        
        /// <summary>
        /// 节点内网监听端口
        /// </summary>
        public int LocalPort { get; set; }
        
        /// <summary>
        /// 节点所在 NAT 类型，未探测时为 Unknown
        /// </summary>
        public NATType NatType { get; set; } = NATType.Unknown;
        
        /// <summary>
        /// 注册时间，UTC；默认取当前 UTC 时间
        /// </summary>
        public DateTime RegisteredTime { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// 最后一次活跃时间，UTC；默认取当前 UTC 时间
        /// </summary>
        public DateTime LastActiveTime { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// 节点声明的服务名到服务说明的映射，默认空字典
        /// </summary>
        public Dictionary<string, string> Services { get; set; } = new Dictionary<string, string>();
        
        /// <summary>
        /// 节点当前状态，默认 Init
        /// </summary>
        public NodeState State { get; set; } = NodeState.Init;
        
        /// <summary>
        /// 是否在线：已注册、已连接或空闲三种状态视为在线
        /// </summary>
        public bool IsOnline
        {
            get
            {
                return State == NodeState.Registered || 
                       State == NodeState.Connected ||
                       State == NodeState.Idle;
            }
        }
        
        /// <summary>
        /// 生成「公网 IP:端口」文本，供打洞通知与日志使用
        /// </summary>
        /// <returns>公网 endpoint 文本；公网 IP 为空时返回 null</returns>
        public string GetPublicEndPoint()
        {
            if (string.IsNullOrEmpty(PublicAddress))
            {
                return null;
            }
            return $"{PublicAddress}:{PublicPort}";
        }
        
        /// <summary>
        /// 生成「内网 IP:端口」文本，供同网段直连使用
        /// </summary>
        /// <returns>内网 endpoint 文本；内网 IP 为空时返回 null</returns>
        public string GetLocalEndPoint()
        {
            if (string.IsNullOrEmpty(LocalAddress))
            {
                return null;
            }
            return $"{LocalAddress}:{LocalPort}";
        }
        
        /// <summary>
        /// 汇总节点标识、公网与内网地址、NAT 类型、状态及在线标记，用于日志输出
        /// </summary>
        /// <returns>单行节点摘要</returns>
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
        
        /// <summary>
        /// 复制节点标识、地址、NAT 类型、时间与状态；Services 不复制，副本为空字典
        /// </summary>
        /// <returns>新的节点信息副本，与源对象不共享任何引用</returns>
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