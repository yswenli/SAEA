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
*修改时间：2026/04/20 16:16:36
*修改人： yswenli
*版本号： v26.4.23.1
*描述：DiscoveredNode接口
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