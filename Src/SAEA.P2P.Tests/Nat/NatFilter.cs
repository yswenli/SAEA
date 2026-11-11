/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.P2P.Tests.Nat
*文件名： NatFilter
*版本号： net10.0
*唯一标识：fa6a1311-e661-4792-afd3-4f81192c0780
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 13:40:00
*描述：NAT 过滤行为与入向放行判定
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 13:40:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 NAT 过滤行为
*
*****************************************************************************/
using System.Net;

namespace SAEA.P2P.Tests.Nat
{
    /// <summary>
    /// 过滤行为：入向报文按什么粒度放行。
    /// </summary>
    public enum NatFilterBehavior
    {
        /// <summary>
        /// 端点无关过滤（EIF）：只要存在映射，任意来源都可入
        /// </summary>
        EndpointIndependent,

        /// <summary>
        /// 地址相关过滤（ADF）：来源地址必须是内部主机曾访问过的地址
        /// </summary>
        AddressDependent,

        /// <summary>
        /// 地址端口相关过滤（APDF）：来源地址与端口都必须与内部主机曾访问的目标完全一致
        /// </summary>
        AddressAndPortDependent
    }

    /// <summary>
    /// 入向放行判定：只依据映射记录的历史目标，不依赖内核行为。
    /// </summary>
    public static class NatFilter
    {
        /// <summary>
        /// 判断来自 <paramref name="remote"/> 的入向报文能否穿过映射
        /// </summary>
        /// <param name="mapping">命中的映射</param>
        /// <param name="remote">入向来源端点</param>
        /// <param name="behavior">过滤行为</param>
        /// <returns>允许入向时为 true</returns>
        public static bool Allows(NatMapping mapping, IPEndPoint remote, NatFilterBehavior behavior)
        {
            if (mapping == null || remote == null) return false;

            switch (behavior)
            {
                case NatFilterBehavior.EndpointIndependent:
                    return true;
                case NatFilterBehavior.AddressDependent:
                    return mapping.HasPermittedAddress(remote.Address);
                case NatFilterBehavior.AddressAndPortDependent:
                    return mapping.HasPermittedEndPoint(remote);
                default:
                    return false;
            }
        }
    }
}