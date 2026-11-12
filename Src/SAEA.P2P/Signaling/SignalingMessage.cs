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
*命名空间：SAEA.P2P.Signaling
*文件名： SignalingMessage
*版本号： v26.11.5.1
*唯一标识：36a23cf8-649b-4ffe-8e2c-dac69e561175
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 18:15:22
*描述：信令报文模型
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 18:15:22
*修改人： yswenli
*版本号： v26.11.5.1
*描述：信令报文模型
*
*****************************************************************************/
using System;
using SAEA.P2P.Protocol;

namespace SAEA.P2P.Signaling
{
    /// <summary>
    /// 信令报文模型，采用扁平结构：一条报文只承载一种消息，仅填写该类型用到的字段
    /// </summary>
    public class SignalingMessage
    {
        /// <summary>
        /// 协议版本
        /// </summary>
        public int Version { get; set; } = SignalingJsonCodec.CurrentProtocolVersion;

        /// <summary>
        /// 消息类型
        /// </summary>
        public P2PMessageType Type { get; set; }

        /// <summary>
        /// Register 本端节点标识
        /// </summary>
        public string PeerId { get; set; }

        /// <summary>
        /// Register 节点名称
        /// </summary>
        public string NodeName { get; set; }

        /// <summary>
        /// Register 能力标签，取 udp、tcp、ipv6、turn
        /// </summary>
        public string[] Capabilities { get; set; }

        /// <summary>
        /// 会话标识
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// RegisterAck 服务端时间
        /// </summary>
        public DateTimeOffset ServerTime { get; set; }

        /// <summary>
        /// ConnectRequest 目标节点标识
        /// </summary>
        public string TargetPeerId { get; set; }

        /// <summary>
        /// ICE 用户名片段
        /// </summary>
        public string Ufrag { get; set; }

        /// <summary>
        /// ICE 密码
        /// </summary>
        public string Pwd { get; set; }

        /// <summary>
        /// ICE 角色，取 controlling 或 controlled
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// ICE 角色冲突时的 64 位决胜值
        /// </summary>
        public ulong Tiebreaker { get; set; }

        /// <summary>
        /// 候选列表
        /// </summary>
        public SignalingCandidate[] Candidates { get; set; }

        /// <summary>
        /// 候选是否已发完
        /// </summary>
        public bool EndOfCandidates { get; set; }

        /// <summary>
        /// AuthChallenge 挑战随机数
        /// </summary>
        public string Nonce { get; set; }

        /// <summary>
        /// AuthResponse 鉴权应答结果
        /// </summary>
        public string Response { get; set; }

        /// <summary>
        /// TurnCredential TURN 领域
        /// </summary>
        public string Realm { get; set; }

        /// <summary>
        /// TurnCredential TURN 临时用户名
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// TurnCredential TURN 临时密码
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// TurnCredential 凭据有效期，单位秒
        /// </summary>
        public int Ttl { get; set; }

        /// <summary>
        /// TurnCredential TURN 服务地址列表
        /// </summary>
        public string[] Urls { get; set; }

        /// <summary>
        /// SessionClose 拆除原因
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Error 错误码
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// Error 错误说明
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 按消息类型创建只填了类型与协议版本的空报文
        /// </summary>
        /// <param name="type">消息类型</param>
        /// <returns>可直接填字段的报文对象</returns>
        public static SignalingMessage Create(P2PMessageType type)
        {
            return new SignalingMessage { Type = type };
        }
    }
}