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
*命名空间：SAEA.P2P.Stun
*文件名： StunAttributeType
*版本号： v26.11.5.1
*唯一标识：5ae97201-21f7-4d81-bf3b-e057c0634481
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 15:20:00
*描述：STUN 属性类型表
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 15:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 STUN 属性类型表
*
*****************************************************************************/
namespace SAEA.P2P.Stun
{
    /// <summary>
    /// STUN 属性类型；按来源分成 RFC 8489 核心、RFC 5780 扩展与 TURN 专用三组。
    /// 最高位为 0 表示 comprehension-required，为 1 表示 comprehension-optional。
    /// </summary>
    public enum StunAttributeType
    {
        // RFC 8489 核心属性与 ICE 专用属性

        /// <summary>
        /// 映射地址
        /// </summary>
        MappedAddress = 0x0001,

        /// <summary>
        /// 用户名
        /// </summary>
        Username = 0x0006,

        /// <summary>
        /// 消息完整性，HMAC-SHA1
        /// </summary>
        MessageIntegrity = 0x0008,

        /// <summary>
        /// 错误码
        /// </summary>
        ErrorCode = 0x0009,

        /// <summary>
        /// 未知属性列表
        /// </summary>
        UnknownAttributes = 0x000A,

        /// <summary>
        /// 认证域
        /// </summary>
        Realm = 0x0014,

        /// <summary>
        /// 一次性随机串
        /// </summary>
        Nonce = 0x0015,

        /// <summary>
        /// 消息完整性，HMAC-SHA256
        /// </summary>
        MessageIntegritySha256 = 0x001C,

        /// <summary>
        /// 密码算法
        /// </summary>
        PasswordAlgorithm = 0x001D,

        /// <summary>
        /// 用户名哈希
        /// </summary>
        UserHash = 0x001E,

        /// <summary>
        /// 异或映射地址
        /// </summary>
        XorMappedAddress = 0x0020,

        /// <summary>
        /// 候选优先级，ICE 专用
        /// </summary>
        Priority = 0x0024,

        /// <summary>
        /// 提名标记，ICE 专用
        /// </summary>
        UseCandidate = 0x0025,

        /// <summary>
        /// 软件名
        /// </summary>
        Software = 0x8022,

        /// <summary>
        /// 密码算法列表
        /// </summary>
        PasswordAlgorithms = 0x8002,

        /// <summary>
        /// 备用认证域
        /// </summary>
        AlternateDomain = 0x8003,

        /// <summary>
        /// 备用服务地址
        /// </summary>
        AlternateServer = 0x8023,

        /// <summary>
        /// 指纹
        /// </summary>
        Fingerprint = 0x8028,

        /// <summary>
        /// 受控方角色标记，ICE 专用
        /// </summary>
        IceControlled = 0x8029,

        /// <summary>
        /// 控制方角色标记，ICE 专用
        /// </summary>
        IceControlling = 0x802A,

        // RFC 5780 扩展属性

        /// <summary>
        /// 换地址或换端口请求
        /// </summary>
        ChangeRequest = 0x0003,

        /// <summary>
        /// 填充，仅用于保持报文长度
        /// </summary>
        Padding = 0x0026,

        /// <summary>
        /// 响应端口
        /// </summary>
        ResponsePort = 0x0027,

        /// <summary>
        /// 响应来源地址
        /// </summary>
        ResponseOrigin = 0x802B,

        /// <summary>
        /// 备用地址
        /// </summary>
        OtherAddress = 0x802C,

        // TURN 专用属性

        /// <summary>
        /// 通道号
        /// </summary>
        ChannelNumber = 0x000C,

        /// <summary>
        /// 分配生命周期
        /// </summary>
        Lifetime = 0x000D,

        /// <summary>
        /// 异或对端地址
        /// </summary>
        XorPeerAddress = 0x0012,

        /// <summary>
        /// 传输数据
        /// </summary>
        Data = 0x0013,

        /// <summary>
        /// 异或中继地址
        /// </summary>
        XorRelayedAddress = 0x0016,

        /// <summary>
        /// 申请偶数端口
        /// </summary>
        EvenPort = 0x0018,

        /// <summary>
        /// 请求的传输协议
        /// </summary>
        RequestedTransport = 0x0019,

        /// <summary>
        /// 禁止分片
        /// </summary>
        DontFragment = 0x001A,

        /// <summary>
        /// 预留令牌
        /// </summary>
        ReservationToken = 0x0022
    }
}