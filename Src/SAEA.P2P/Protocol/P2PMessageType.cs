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
*修改时间：2026/11/12 18:15:22
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 0x90 段信令会话消息类型
*
*****************************************************************************/
namespace SAEA.P2P.Protocol
{
    /// <summary>
    /// P2P 协议消息类型，单字节取值按阶段分段：0x10 注册、0x20 NAT 探测、0x30 打洞、0x40 中继、0x50 局域网发现、0x60 鉴权、0x70 心跳、0x80 业务数据、0x90 信令会话
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
        /// 客户端请求探测自身公网地址，无内容
        /// </summary>
        NatProbe = 0x20,
        /// <summary>
        /// 服务器回探测到的公网地址、端口与 NAT 类型
        /// </summary>
        NatProbeAck = 0x21,
        /// <summary>
        /// 请求服务器协调与指定节点打洞，内容为目标节点标识
        /// </summary>
        PunchRequest = 0x30,
        /// <summary>
        /// 打洞同步包，双方互发以打开各自的 NAT 映射
        /// </summary>
        PunchSync = 0x31,
        /// <summary>
        /// 打洞应答，确认已收到对方的 PunchSync
        /// </summary>
        PunchAck = 0x32,
        /// <summary>
        /// 服务器通知对端已就绪，内容为目标标识、公网与内网候选地址
        /// </summary>
        PunchReady = 0x33,
        /// <summary>
        /// 请求经服务器中继转发
        /// </summary>
        RelayRequest = 0x40,
        /// <summary>
        /// 经服务器中继的业务数据
        /// </summary>
        RelayData = 0x41,
        /// <summary>
        /// 中继会话的确认与状态回执
        /// </summary>
        RelayAck = 0x42,
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
        /// 业务数据，直连时原样承载上层负载，经信令服务器转发时前置来源与目标标识
        /// </summary>
        UserData = 0x80,
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