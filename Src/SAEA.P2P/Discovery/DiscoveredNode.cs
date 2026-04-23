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
*唯一标识：86453db6-bc9b-45e6-97ee-8b6f9c5ac61e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/4/20 16:16:36
*描述：已发现节点信息类，存储发现节点的基本信息
*
*=====================================================================
*修改标记
*修改时间：2026/4/20 16:16:36
*修改人： yswenli
*版本号： v26.4.23.1
*描述：已发现节点信息类，存储发现节点的基本信息
*
*****************************************************************************/
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