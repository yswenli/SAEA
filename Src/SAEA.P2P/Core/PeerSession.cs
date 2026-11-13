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
*命名空间：SAEA.P2P.Core
*文件名： PeerSession
*版本号： v26.4.23.1
*唯一标识：f849d1df-26d7-4c8c-81b7-50715f3bf1e0
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 15:44:01
*描述：PeerSession接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 10:21:07
*修改人： yswenli
*版本号： v26.11.5.1
*描述：通道类型改为 ICE 选路结果
*
*****************************************************************************/
using System;
using SAEA.P2P.Channel;
using SAEA.P2P.Common;

namespace SAEA.P2P.Core
{
    /// <summary>
    /// 与单个对端的会话，记录地址、通道类型、收发序号与活跃时间
    /// </summary>
    public class PeerSession
    {
        /// <summary>
        /// 会话唯一标识，创建后不可变
        /// </summary>
        public string SessionId { get; }
        
        /// <summary>
        /// 对端节点标识，创建后不可变
        /// </summary>
        public string PeerId { get; }
        
        /// <summary>
        /// 会话所处的节点状态，初始为 Init
        /// </summary>
        public NodeState State { get; set; } = NodeState.Init;
        
        /// <summary>
        /// 对端公网地址，尚未探测到时为 null
        /// </summary>
        public string PublicAddress { get; set; }
        
        /// <summary>
        /// 对端公网端口，未获取时为 0
        /// </summary>
        public int PublicPort { get; set; }
        
        /// <summary>
        /// 对端内网地址，尚未获取时为 null
        /// </summary>
        public string LocalAddress { get; set; }
        
        /// <summary>
        /// 对端内网端口，未获取时为 0
        /// </summary>
        public int LocalPort { get; set; }
        
        /// <summary>
        /// 当前使用的通道类型，默认直连
        /// </summary>
        public ChannelType Channel { get; set; } = ChannelType.UdpDirect;
        
        /// <summary>
        /// 会话创建时间，UTC
        /// </summary>
        public DateTime CreatedTime { get; } = DateTime.UtcNow;
        
        /// <summary>
        /// 最近一次活跃时间，UTC，由 Active 刷新
        /// </summary>
        public DateTime LastActiveTime { get; private set; } = DateTime.UtcNow;
        
        /// <summary>
        /// 已发送数据序号，由 NextSendSeq 自增
        /// </summary>
        public long SendSequence { get; private set; }
        
        /// <summary>
        /// 已接收数据序号，由 NextReceiveSeq 自增
        /// </summary>
        public long ReceiveSequence { get; set; }
        
        /// <summary>
        /// 中继会话标识，仅中继通道下有值
        /// </summary>
        public string RelaySessionId { get; set; }
        
        private readonly object _lock = new object();
        
        /// <summary>
        /// 创建会话
        /// </summary>
        /// <param name="sessionId">会话唯一标识，不可为空</param>
        /// <param name="peerId">对端节点标识，不可为空</param>
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
        
        /// <summary>
        /// 判断会话是否已超过指定时长未活跃
        /// </summary>
        /// <param name="timeoutMs">超时阈值，单位毫秒</param>
        /// <returns>已超时返回 true，否则返回 false</returns>
        public bool IsExpired(int timeoutMs)
        {
            lock (_lock)
            {
                return (DateTime.UtcNow - LastActiveTime).TotalMilliseconds > timeoutMs;
            }
        }
        
        /// <summary>
        /// 把最近活跃时间刷新为当前 UTC 时间
        /// </summary>
        public void Active()
        {
            lock (_lock)
            {
                LastActiveTime = DateTime.UtcNow;
            }
        }
        
        /// <summary>
        /// 取下一个发送序号
        /// </summary>
        /// <returns>自增后的发送序号，从 1 开始</returns>
        public long NextSendSeq()
        {
            lock (_lock)
            {
                return ++SendSequence;
            }
        }
        
        /// <summary>
        /// 取下一个接收序号
        /// </summary>
        /// <returns>自增后的接收序号，从 1 开始</returns>
        public long NextReceiveSeq()
        {
            lock (_lock)
            {
                return ++ReceiveSequence;
            }
        }
        
        /// <summary>
        /// 取对端公网地址的「地址:端口」文本
        /// </summary>
        /// <returns>公网地址为空时返回 null</returns>
        public string GetPublicEndPoint()
        {
            if (string.IsNullOrEmpty(PublicAddress))
            {
                return null;
            }
            return $"{PublicAddress}:{PublicPort}";
        }
        
        /// <summary>
        /// 取对端内网地址的「地址:端口」文本
        /// </summary>
        /// <returns>内网地址为空时返回 null</returns>
        public string GetLocalEndPoint()
        {
            if (string.IsNullOrEmpty(LocalAddress))
            {
                return null;
            }
            return $"{LocalAddress}:{LocalPort}";
        }
        
        /// <summary>
        /// 返回包含会话标识、对端标识、状态与通道类型的描述
        /// </summary>
        /// <returns>会话描述字符串</returns>
        public override string ToString()
        {
            return $"PeerSession[SessionId={SessionId}, PeerId={PeerId}, State={State}, Channel={Channel}]";
        }
    }
}