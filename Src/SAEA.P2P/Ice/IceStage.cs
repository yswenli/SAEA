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
*命名空间：SAEA.P2P.Ice
*文件名： IceStage
*版本号： v26.11.5.1
*唯一标识：f0ecca64-87f5-4d7b-8c9b-6802c4437e96
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 14:24:38
*描述：ICE 选路阶梯阶段枚举
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 14:24:38
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 ICE 选路阶梯阶段枚举
*
*****************************************************************************/
namespace SAEA.P2P.Ice
{
    /// <summary>
    /// 选路阶梯的阶段，顺序即回退优先级
    /// </summary>
    public enum IceStage
    {
        /// <summary>
        /// UDP 直连
        /// </summary>
        UdpDirect = 0,

        /// <summary>
        /// TCP 直连（active ↔ passive）
        /// </summary>
        TcpDirect = 1,

        /// <summary>
        /// TURN over UDP 中继
        /// </summary>
        TurnUdp = 2,

        /// <summary>
        /// TURN over TCP 中继
        /// </summary>
        TurnTcp = 3,

        /// <summary>
        /// TURN over TLS 中继
        /// </summary>
        TurnTls = 4
    }
}