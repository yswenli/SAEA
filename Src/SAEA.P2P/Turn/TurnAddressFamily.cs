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
*命名空间：SAEA.P2P.Turn
*文件名： TurnAddressFamily
*版本号： v26.11.5.1
*唯一标识：91231c66-63ed-4f28-876f-5d75eeff0eb4
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 22:30:00
*描述：TURN 请求与附加地址族的取值
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 22:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TURN 地址族取值
*
*****************************************************************************/
namespace SAEA.P2P.Turn
{
    /// <summary>
    /// TURN 地址族取值：对应 REQUESTED-ADDRESS-FAMILY 与 ADDITIONAL-ADDRESS-FAMILY 的家族字节。
    /// </summary>
    public enum TurnAddressFamily : byte
    {
        /// <summary>
        /// IPv4 地址族
        /// </summary>
        IPv4 = 0x01,

        /// <summary>
        /// IPv6 地址族
        /// </summary>
        IPv6 = 0x02
    }
}