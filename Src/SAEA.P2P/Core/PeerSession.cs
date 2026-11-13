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
*修改时间：2026/11/13 18:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增可覆盖通道类型的选路完成标记，供局域网发现模式写入 Local 通道
*
*****************************************************************************/
using System;
using SAEA.P2P.Channel;
using SAEA.P2P.Ice;

namespace SAEA.P2P.Core
{
    /// <summary>
    /// 与单个对端的会话：本端短凭据、ICE 选路会话与选路后的连接描述，阶段由客户端推进
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
        /// 是否为本端发起（发起方为控制方）
        /// </summary>
        internal bool IsInitiator { get; }
        
        /// <summary>
        /// 本端 ufrag，用于连通性检查的短凭据
        /// </summary>
        internal string LocalUfrag { get; }
        
        /// <summary>
        /// 本端口令，用于连通性检查的完整性校验
        /// </summary>
        internal string LocalPassword { get; }
        
        /// <summary>
        /// 本端 ICE 角色
        /// </summary>
        internal IceRole Role { get; }
        
        /// <summary>
        /// 角色冲突裁决值，大者优先
        /// </summary>
        internal ulong Tiebreaker { get; }
        
        /// <summary>
        /// 会话阶段，由客户端推进
        /// </summary>
        internal PeerSessionState Phase { get; private set; }
        
        /// <summary>
        /// 选路会话，登记后由客户端创建并挂载
        /// </summary>
        internal IceSession Ice { get; private set; }
        
        /// <summary>
        /// 协商参数，trickle 候选通过它增量加入
        /// </summary>
        internal IceSessionParameters Parameters { get; private set; }
        
        /// <summary>
        /// 选路完成后才有值
        /// </summary>
        internal P2PConnection Connection { get; private set; }
        
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
        /// 创建带 ICE 参数的会话
        /// </summary>
        /// <param name="sessionId">会话唯一标识，不可为空</param>
        /// <param name="peerId">对端节点标识，不可为空</param>
        /// <param name="isInitiator">是否为本端发起</param>
        /// <param name="localUfrag">本端 ufrag，不可为空</param>
        /// <param name="localPassword">本端口令，不可为空</param>
        /// <param name="role">本端 ICE 角色</param>
        /// <param name="tiebreaker">角色冲突裁决值</param>
        internal PeerSession(string sessionId, string peerId, bool isInitiator, string localUfrag, string localPassword, IceRole role, ulong tiebreaker)
            : this(sessionId, peerId)
        {
            if (string.IsNullOrWhiteSpace(localUfrag))
            {
                throw new ArgumentException("本端 ufrag 不能为空", nameof(localUfrag));
            }

            if (string.IsNullOrWhiteSpace(localPassword))
            {
                throw new ArgumentException("本端口令不能为空", nameof(localPassword));
            }

            IsInitiator = isInitiator;
            LocalUfrag = localUfrag;
            LocalPassword = localPassword;
            Role = role;
            Tiebreaker = tiebreaker;
            Phase = PeerSessionState.Signaling;
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
        /// 挂载选路会话与协商参数，会话进入候选收集阶段
        /// </summary>
        /// <param name="ice">选路会话，不可为 null</param>
        /// <param name="parameters">协商参数，不可为 null</param>
        internal void AttachIceSession(IceSession ice, IceSessionParameters parameters)
        {
            if (ice == null)
            {
                throw new ArgumentNullException(nameof(ice));
            }

            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            Ice = ice;
            Parameters = parameters;
            Phase = PeerSessionState.Gathering;
            Active();
        }
        
        /// <summary>
        /// 推进会话阶段
        /// </summary>
        /// <param name="phase">目标阶段</param>
        internal void MarkPhase(PeerSessionState phase)
        {
            Phase = phase;
            Active();
        }
        
        /// <summary>
        /// 标记选路完成，记录通道类型与两端端点
        /// </summary>
        /// <param name="result">选路结果，不可为 null</param>
        internal void MarkConnected(IceSessionResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            MarkConnected(result, result.ChannelType);
        }

        /// <summary>
        /// 标记选路完成并覆盖通道类型：局域网发现模式把通道覆盖为 Local，端点仍取选中候选对
        /// </summary>
        /// <param name="result">选路结果，不可为 null</param>
        /// <param name="channelType">要写入连接的通道类型</param>
        internal void MarkConnected(IceSessionResult result, ChannelType channelType)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            Connection = new P2PConnection(PeerId, channelType, result.LocalEndPoint, result.RemoteEndPoint);
            Channel = channelType;
            Phase = PeerSessionState.Connected;
            Active();
        }
        
        /// <summary>
        /// 把会话标记为已关闭
        /// </summary>
        internal void Close()
        {
            Phase = PeerSessionState.Closed;
            Active();
        }
        
        /// <summary>
        /// 返回包含会话标识、对端标识与阶段的描述
        /// </summary>
        /// <returns>会话描述字符串</returns>
        public override string ToString()
        {
            return $"PeerSession[SessionId={SessionId}, PeerId={PeerId}, Phase={Phase}]";
        }
        
        /// <summary>
        /// 会话阶段，从信令协商到选路完成
        /// </summary>
        internal enum PeerSessionState
        {
            /// <summary>信令协商中：已登记会话，尚未拿到对端参数</summary>
            Signaling = 0,
            /// <summary>候选收集中</summary>
            Gathering = 1,
            /// <summary>连通性检查中</summary>
            Checking = 2,
            /// <summary>选路完成，可收发业务帧</summary>
            Connected = 3,
            /// <summary>会话已关闭</summary>
            Closed = 4
        }
    }
}