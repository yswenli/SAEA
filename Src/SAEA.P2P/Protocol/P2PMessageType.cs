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
*唯一标识：2ceeca68-14a0-4e51-92e4-173656c911f9
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/4/20 15:38:24
*描述：P2P消息类型枚举，定义协议消息类别
*
*=====================================================================
*修改标记
*修改时间：2026/4/20 15:38:24
*修改人： yswenli
*版本号： v26.4.23.1
*描述：P2P消息类型枚举，定义协议消息类别
*
*****************************************************************************/
namespace SAEA.P2P.Protocol
{
    public enum P2PMessageType : byte
    {
        Register = 0x10,
        RegisterAck = 0x11,
        NodeList = 0x12,
        NatProbe = 0x20,
        NatProbeAck = 0x21,
        PunchRequest = 0x30,
        PunchSync = 0x31,
        PunchAck = 0x32,
        PunchReady = 0x33,
        RelayRequest = 0x40,
        RelayData = 0x41,
        RelayAck = 0x42,
        LocalDiscover = 0x50,
        LocalDiscoverAck = 0x51,
        AuthChallenge = 0x60,
        AuthResponse = 0x61,
        AuthSuccess = 0x62,
        Heartbeat = 0x70,
        HeartbeatAck = 0x71,
        UserData = 0x80
    }
}