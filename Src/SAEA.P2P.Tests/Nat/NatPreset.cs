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
*文件名： NatPreset
*版本号： net10.0
*唯一标识：82f8eb61-cdc3-43b8-8006-686d8f6bb76f
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 13:40:00
*描述：NAT 模拟器的预设组合
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 13:40:00
*修改人： yswenli
*版本号： net10.0
*描述：新增六种 NAT 预设
*
*****************************************************************************/
namespace SAEA.P2P.Tests.Nat
{
    /// <summary>
    /// 常用 NAT 预设。组合定义在测试工程内，不引用被测代码常量。
    /// </summary>
    public static class NatPreset
    {
        /// <summary>
        /// 全锥形：端点无关映射与过滤，端口保持，允许 hairpin
        /// </summary>
        /// <returns>预设配置</returns>
        public static NatConfig FullCone()
        {
            return new NatConfig
            {
                Mapping = NatMappingBehavior.EndpointIndependent,
                Filter = NatFilterBehavior.EndpointIndependent,
                PortPreservation = true,
                Hairpinning = true
            };
        }

        /// <summary>
        /// 限制锥形：端点无关映射，地址相关过滤
        /// </summary>
        /// <returns>预设配置</returns>
        public static NatConfig Restricted()
        {
            return new NatConfig
            {
                Mapping = NatMappingBehavior.EndpointIndependent,
                Filter = NatFilterBehavior.AddressDependent,
                PortPreservation = true,
                Hairpinning = true
            };
        }

        /// <summary>
        /// 端口限制锥形：端点无关映射，地址端口相关过滤
        /// </summary>
        /// <returns>预设配置</returns>
        public static NatConfig PortRestricted()
        {
            return new NatConfig
            {
                Mapping = NatMappingBehavior.EndpointIndependent,
                Filter = NatFilterBehavior.AddressAndPortDependent,
                PortPreservation = true,
                Hairpinning = true
            };
        }

        /// <summary>
        /// 对称型：地址端口相关映射与过滤，不保持端口，不支持 hairpin
        /// </summary>
        /// <returns>预设配置</returns>
        public static NatConfig Symmetric()
        {
            return new NatConfig
            {
                Mapping = NatMappingBehavior.AddressAndPortDependent,
                Filter = NatFilterBehavior.AddressAndPortDependent,
                PortPreservation = false,
                Hairpinning = false
            };
        }

        /// <summary>
        /// 保持端口的对称型：地址端口相关映射与过滤，但外部端口尽量等于内部端口
        /// </summary>
        /// <returns>预设配置</returns>
        public static NatConfig SymmetricKeepPort()
        {
            return new NatConfig
            {
                Mapping = NatMappingBehavior.AddressAndPortDependent,
                Filter = NatFilterBehavior.AddressAndPortDependent,
                PortPreservation = true,
                Hairpinning = false
            };
        }

        /// <summary>
        /// TCP 映射型：端点无关映射与过滤，端口保持，允许 hairpin，配合静态端口映射使用
        /// </summary>
        /// <returns>预设配置</returns>
        public static NatConfig TcpMapped()
        {
            return new NatConfig
            {
                Mapping = NatMappingBehavior.EndpointIndependent,
                Filter = NatFilterBehavior.EndpointIndependent,
                PortPreservation = true,
                Hairpinning = true
            };
        }
    }
}