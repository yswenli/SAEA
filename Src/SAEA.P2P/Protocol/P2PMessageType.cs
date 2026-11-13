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
*命名空间：SAEA.P2P.Protocol
*文件名： P2PMessageType
*版本号： v26.4.23.1
*唯一标识：c858cb70-28bb-4c6d-bfb0-e409547d0504
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 15:38:24
*描述：P2PMessageType类型枚举
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 13:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：删除打洞、自定义中继、NAT 探测与信令承载业务数据的消息类型
*
*****************************************************************************/
namespace SAEA.P2P.Protocol
{
    /// <summary>
    /// P2P 协议消息类型，单字节取值按阶段分段：0x10 注册、0x50 局域网发现、0x60 鉴权、0x70 心跳、0x90 信令会话
    /// </summary>
    public enum P2PMessageType : byte
    {
        /// <summary>
        /// 客户端向信令服务器注册，内容为节点标识
        /// </summary>
        Register = 0x10,
        /// <summary>
        /// 服务器回注册结果
        /// </summary>
        RegisterAck = 0x11,
        /// <summary>
        /// 服务器下发在线节点标识列表，逗号分隔
        /// </summary>
        NodeList = 0x12,
        /// <summary>
        /// 局域网内广播发现请求
        /// </summary>
        LocalDiscover = 0x50,
        /// <summary>
        /// 局域网节点回发现应答，携带自身信息
        /// </summary>
        LocalDiscoverAck = 0x51,
        /// <summary>
        /// 服务器下发鉴权挑战
        /// </summary>
        AuthChallenge = 0x60,
        /// <summary>
        /// 客户端回复鉴权应答
        /// </summary>
        AuthResponse = 0x61,
        /// <summary>
        /// 服务器通知鉴权通过
        /// </summary>
        AuthSuccess = 0x62,
        /// <summary>
        /// 心跳保活，无内容
        /// </summary>
        Heartbeat = 0x70,
        /// <summary>
        /// 心跳应答，无内容
        /// </summary>
        HeartbeatAck = 0x71,
        /// <summary>
        /// 客户端请求与目标节点建立会话，负载含目标节点标识与会话标识
        /// </summary>
        ConnectRequest = 0x90,
        /// <summary>
        /// 发起方下发本端 ICE 参数（ufrag/pwd/role/tiebreaker）与候选列表
        /// </summary>
        SessionOffer = 0x91,
        /// <summary>
        /// 应答方下发本端 ICE 参数与候选列表，role 与 tiebreaker 填自身值
        /// </summary>
        SessionAnswer = 0x92,
        /// <summary>
        /// 会话期间增量补充候选
        /// </summary>
        TrickleCandidate = 0x93,
        /// <summary>
        /// 本端候选已发送完毕
        /// </summary>
        EndOfCandidates = 0x94,
        /// <summary>
        /// 服务端下发 TURN 临时凭据
        /// </summary>
        TurnCredential = 0x95,
        /// <summary>
        /// 请求拆除会话
        /// </summary>
        SessionClose = 0x96,
        /// <summary>
        /// 错误应答，负载含错误码与说明
        /// </summary>
        Error = 0x97
    }
}