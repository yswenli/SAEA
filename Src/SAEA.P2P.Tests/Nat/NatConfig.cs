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
*文件名： NatConfig
*版本号： net10.0
*唯一标识：a5c74e8b-4b5f-41f0-b7c2-48c53831686e
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 13:40:00
*描述：NAT 模拟器的映射行为与运行参数
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 13:40:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 NAT 模拟器配置
*
*****************************************************************************/
using System;

namespace SAEA.P2P.Tests.Nat
{
    /// <summary>
    /// 映射行为：外部端口按什么粒度分配。
    /// </summary>
    public enum NatMappingBehavior
    {
        /// <summary>
        /// 端点无关映射（EIM）：同一内部端点复用同一个外部端口
        /// </summary>
        EndpointIndependent,

        /// <summary>
        /// 地址相关映射（ADM）：同一内部端点访问不同目标地址时使用不同外部端口
        /// </summary>
        AddressDependent,

        /// <summary>
        /// 地址端口相关映射（APDM）：同一内部端点访问不同目标端点时使用不同外部端口
        /// </summary>
        AddressAndPortDependent
    }

    /// <summary>
    /// NAT 模拟器配置。只描述行为，不引用被测代码里的任何常量。
    /// </summary>
    public sealed class NatConfig
    {
        /// <summary>
        /// 映射行为
        /// </summary>
        public NatMappingBehavior Mapping { get; set; } = NatMappingBehavior.EndpointIndependent;

        /// <summary>
        /// 过滤行为
        /// </summary>
        public NatFilterBehavior Filter { get; set; } = NatFilterBehavior.EndpointIndependent;

        /// <summary>
        /// 是否尽量让外部端口等于内部端口
        /// </summary>
        public bool PortPreservation { get; set; } = true;

        /// <summary>
        /// 是否允许同一 NAT 后的两个主机通过外部映射互访
        /// </summary>
        public bool Hairpinning { get; set; } = true;

        /// <summary>
        /// 映射空闲超时；从最后一次出站或放行的入站开始计时
        /// </summary>
        public TimeSpan MappingLifetime { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// 复制一份配置，便于同一预设派生出不同超时的实例
        /// </summary>
        /// <returns>配置副本</returns>
        public NatConfig Clone()
        {
            return new NatConfig
            {
                Mapping = Mapping,
                Filter = Filter,
                PortPreservation = PortPreservation,
                Hairpinning = Hairpinning,
                MappingLifetime = MappingLifetime
            };
        }
    }
}