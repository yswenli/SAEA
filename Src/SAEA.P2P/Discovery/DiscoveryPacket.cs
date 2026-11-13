/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.P2P.Discovery
*文件名： DiscoveryPacket
*版本号： v26.11.5.1
*唯一标识：2b45a982-278d-4e92-9176-403646b9b6aa
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 16:59:24
*描述：无信令局域网发现报文的传输模型
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 16:59:24
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增无信令局域网发现报文模型
*
*****************************************************************************/
using SAEA.P2P.Signaling;

namespace SAEA.P2P.Discovery
{
    /// <summary>
    /// 无信令局域网发现报文，由节点以 UDP 组播或广播发出，接收方从数据报来源取得端点
    /// </summary>
    public sealed class DiscoveryPacket
    {
        /// <summary>
        /// 发现协议版本，当前只接受 1
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// 发送方节点唯一标识
        /// </summary>
        public string PeerId { get; set; }

        /// <summary>
        /// 发送方节点名称，未设置时为 null
        /// </summary>
        public string NodeName { get; set; }

        /// <summary>
        /// ICE 用户名片段，供对端发起连接时使用
        /// </summary>
        public string Ufrag { get; set; }

        /// <summary>
        /// ICE 口令，与用户名片段配对使用
        /// </summary>
        public string Pwd { get; set; }

        /// <summary>
        /// ICE 角色，取 controlling 或 controlled
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// ICE 角色冲突裁决值，角色相同时按数值大的一方为 controlling
        /// </summary>
        public ulong Tiebreaker { get; set; }

        /// <summary>
        /// 发送方可直连的候选列表，无候选时为 null
        /// </summary>
        public SignalingCandidate[] Candidates { get; set; }

        /// <summary>
        /// 报文生成时间，Unix 毫秒，用于拒收过期报文
        /// </summary>
        public long TimestampUnixMs { get; set; }

        /// <summary>
        /// 报文内容的 HMAC-SHA256 标签，Base64；未配置共享密钥时为 null
        /// </summary>
        public string Hmac { get; set; }
    }
}