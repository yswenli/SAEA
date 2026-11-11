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
*文件名： NatBehaviorInfo
*版本号： v26.11.5.1
*唯一标识：2199fafd-700c-4fee-bfa3-9054d7df8382
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 21:00:00
*描述：NAT 映射与过滤行为的探测结果
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 21:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 NAT 行为探测结果
*
*****************************************************************************/
using System;
using System.Net;

namespace SAEA.P2P.Stun
{
    /// <summary>
    /// NAT 行为探测结果：映射行为、过滤行为与探测到的映射地址。
    /// 结果只供日志与诊断展示，不得用于候选优先级、选路或回退判定。
    /// </summary>
    public sealed class NatBehaviorInfo
    {
        /// <summary>
        /// 创建结果；只由 <see cref="Create"/> 调用
        /// </summary>
        private NatBehaviorInfo()
        {
        }

        /// <summary>
        /// 映射行为
        /// </summary>
        public NatBehaviorKind Mapping { get; private set; }

        /// <summary>
        /// 过滤行为
        /// </summary>
        public NatBehaviorKind Filtering { get; private set; }

        /// <summary>
        /// 基线请求得到的映射地址；基线失败时为 null
        /// </summary>
        public IPEndPoint MappedAddress { get; private set; }

        /// <summary>
        /// 基线探测使用的服务端端点
        /// </summary>
        public IPEndPoint PrimaryServer { get; private set; }

        /// <summary>
        /// 服务端给出的备用端点；单地址服务端为 null
        /// </summary>
        public IPEndPoint AlternateServer { get; private set; }

        /// <summary>
        /// 是否至少拿到了基线响应
        /// </summary>
        public bool HasMappedAddress
        {
            get { return MappedAddress != null; }
        }

        /// <summary>
        /// 构造探测结果
        /// </summary>
        /// <param name="mappedAddress">基线映射地址</param>
        /// <param name="primaryServer">基线<|place_holder_mm_span_0442|>服务端端点</param>
        /// <param name="alternateServer">备用端点</param>
        /// <param name="mapping">映射行为</param>
        /// <param name="filtering">过滤行为</param>
        /// <returns>探测结果</returns>
        internal static NatBehaviorInfo Create(IPEndPoint mappedAddress, IPEndPoint primaryServer, IPEndPoint alternateServer, NatBehaviorKind mapping, NatBehaviorKind filtering)
        {
            return new NatBehaviorInfo
            {
                MappedAddress = mappedAddress,
                PrimaryServer = primaryServer,
                AlternateServer = alternateServer,
                Mapping = mapping,
                Filtering = filtering
            };
        }

        /// <summary>
        /// 输出「映射：… ，过滤：…」形式的中文摘要，供日志使用
        /// </summary>
        /// <returns>摘要文本</returns>
        public override string ToString()
        {
            return "映射：" + Describe(Mapping) + "，过滤：" + Describe(Filtering);
        }

        /// <summary>
        /// 把行为标签转成中文说明
        /// </summary>
        /// <param name="kind">行为标签</param>
        /// <returns>中文说明</returns>
        private static string Describe(NatBehaviorKind kind)
        {
            switch (kind)
            {
                case NatBehaviorKind.EndpointIndependent:
                    return "端点无关";
                case NatBehaviorKind.AddressDependent:
                    return "地址相关";
                case NatBehaviorKind.AddressAndPortDependent:
                    return "地址端口相关";
                default:
                    return "未知";
            }
        }
    }
}