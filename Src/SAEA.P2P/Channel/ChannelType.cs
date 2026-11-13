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
*命名空间：SAEA.P2P.Channel
*文件名： ChannelType
*版本号： v26.4.23.1
*唯一标识：a546fbe3-e654-4328-8f1c-29285338ab1e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 15:38:24
*描述：ChannelType类型枚举
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 18:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补充 Local 与 UdpDirect 的语义差异
*
*****************************************************************************/
namespace SAEA.P2P.Channel
{
    /// <summary>
    /// 节点间通信通道类型，由选路结果写入，不是默认值
    /// </summary>
    public enum ChannelType
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
        RelayUdp = 2,
        /// <summary>
        /// TURN over TCP 中继
        /// </summary>
        RelayTcp = 3,
        /// <summary>
        /// TURN over TLS 中继
        /// </summary>
        RelayTls = 4,
        /// <summary>
        /// 局域网发现直连：未使用信令服务器，ICE 参数经发现报文交换；数据面仍是 UDP 直连，因此与 UdpDirect 的差别只在 ICE 参数来源与上报的通道标识
        /// </summary>
        Local = 5
    }
}