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
*命名空间：SAEA.P2P.Discovery
*文件名： DiscoveredNode
*版本号： v26.4.23.1
*唯一标识：2fdb0d86-9a2f-4444-883d-f10856e2df89
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 16:16:36
*描述：DiscoveredNode接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 17:05:13
*修改人： yswenli
*版本号： v26.11.5.1
*描述：候选字段改为还原后的 ICE 候选
*
*****************************************************************************/
using System;
using System.Net;
using SAEA.P2P.Ice;

namespace SAEA.P2P.Discovery
{
    /// <summary>
    /// 局域网发现到的节点信息
    /// </summary>
    public class DiscoveredNode
    {
        /// <summary>
        /// 节点唯一标识
        /// </summary>
        public string NodeId { get; set; }
        /// <summary>
        /// 节点名称，未设置时由发现方以节点标识代替
        /// </summary>
        public string NodeName { get; set; }
        /// <summary>
        /// 节点所在的内网地址，发现应答报文不携带时为 null
        /// </summary>
        public IPEndPoint LocalAddress { get; set; }
        /// <summary>
        /// 节点声明的服务名列表
        /// </summary>
        public string[] Services { get; set; }
        /// <summary>
        /// 首次发现时间，UTC
        /// </summary>
        public DateTime DiscoveredTime { get; set; } = DateTime.UtcNow;
        /// <summary>
        /// 最近一次收到该节点报文的时间，UTC
        /// </summary>
        public DateTime LastSeenTime { get; set; } = DateTime.UtcNow;
        /// <summary>
        /// 节点公布发现协议版本，未携带时为 0
        /// </summary>
        public int ProtocolVersion { get; set; }
        /// <summary>
        /// 节点的 ICE 用户名片段，未携带时为 null
        /// </summary>
        public string Ufrag { get; set; }
        /// <summary>
        /// 节点的 ICE 口令，未携带时为 null
        /// </summary>
        public string Pwd { get; set; }
        /// <summary>
        /// 节点声明的 ICE 角色，取 controlling 或 controlled，未携带时为 null
        /// </summary>
        public string Role { get; set; }
        /// <summary>
        /// 节点声明的 ICE 角色冲突裁决值
        /// </summary>
        public ulong Tiebreaker { get; set; }
        /// <summary>
        /// 节点公布的可直连候选，已还原为 ICE 候选；未携带或全部非法时为 null
        /// </summary>
        public Candidate[] Candidates { get; set; }

        /// <summary>
        /// 判断节点是否已超过指定时长未刷新
        /// </summary>
        /// <param name="timeoutMs">超时阈值，单位毫秒</param>
        /// <returns>已超时返回 true，否则返回 false</returns>
        public bool IsExpired(int timeoutMs)
        {
            return (DateTime.UtcNow - LastSeenTime).TotalMilliseconds > timeoutMs;
        }

        /// <summary>
        /// 把最近可见时间刷新为当前 UTC 时间
        /// </summary>
        public void Refresh()
        {
            LastSeenTime = DateTime.UtcNow;
        }

        /// <summary>
        /// 返回包含节点标识与内网地址的可读文本
        /// </summary>
        /// <returns>节点描述字符串</returns>
        public override string ToString()
        {
            return $"DiscoveredNode: {NodeId} at {LocalAddress}";
        }
    }
}