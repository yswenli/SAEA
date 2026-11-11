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
*文件名： StunConstants
*版本号： v26.11.5.1
*唯一标识：cb2eae4c-3641-4ebc-94d7-66a801244651
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 15:00:00
*描述：STUN 协议固定常量
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 15:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 STUN 协议固定常量
*
*****************************************************************************/
namespace SAEA.P2P.Stun
{
    /// <summary>
    /// STUN 协议固定常量
    /// </summary>
    public static class StunConstants
    {
        /// <summary>
        /// 魔术字，用于把 STUN 与其它协议区分开；在消息头中按大端存放
        /// </summary>
        public const uint MagicCookie = 0x2112A442;

        /// <summary>
        /// 消息头固定长度，单位字节
        /// </summary>
        public const int HeaderLength = 20;

        /// <summary>
        /// 事务 ID 长度，单位字节；对应 96 位
        /// </summary>
        public const int TransactionIdLength = 12;

        /// <summary>
        /// 属性头长度，单位字节；含 2 字节类型与 2 字节长度
        /// </summary>
        public const int AttributeHeaderLength = 4;

        /// <summary>
        /// FINGERPRINT 属性值计算完成后要与之异或的常量
        /// </summary>
        public const uint FingerprintXor = 0x5354554E;

        /// <summary>
        /// STUN 默认监听端口
        /// </summary>
        public const int DefaultPort = 3478;

        /// <summary>
        /// STUN over TLS 默认监听端口
        /// </summary>
        public const int TlsPort = 5349;
    }
}