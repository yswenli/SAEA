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
*文件名： StunParseError
*版本号： v26.11.5.1
*唯一标识：59836685-efaf-4c89-b951-ad9607f9b969
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 15:00:00
*描述：STUN 报文解析失败原因
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 15:25:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增属性区截断的失败原因
*
*****************************************************************************/
namespace SAEA.P2P.Stun
{
    /// <summary>
    /// STUN 报文解析失败原因；解析入口返回该枚举而不是抛异常，调用方按原因决定丢弃或回错误
    /// </summary>
    public enum StunParseError
    {
        /// <summary>
        /// 解析成功
        /// </summary>
        None = 0,

        /// <summary>
        /// 报文短于 20 字节消息头
        /// </summary>
        TooShort = 1,

        /// <summary>
        /// 消息类型最高两位不为 0
        /// </summary>
        TopBitsNotZero = 2,

        /// <summary>
        /// 魔术字与协议常量不一致
        /// </summary>
        InvalidMagicCookie = 3,

        /// <summary>
        /// 头部声明长度与实际剩余字节数不一致
        /// </summary>
        LengthMismatch = 4,

        /// <summary>
        /// 头部声明长度不是 4 的倍数
        /// </summary>
        LengthNotAligned = 5,

        /// <summary>
        /// 属性区存在被截断的属性头或属性值
        /// </summary>
        AttributesTruncated = 6
    }
}