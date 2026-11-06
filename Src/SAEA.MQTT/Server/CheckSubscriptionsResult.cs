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
*命名空间：SAEA.MQTT.Server
*文件名： CheckSubscriptionsResult
*版本号： v26.4.23.1
*唯一标识：50405ccb-e277-41e7-b717-423a0e9da6ea
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：CheckSubscriptionsResult接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.MQTT.Protocol;
using System.Collections.Generic;

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 客户端订阅匹配结果，记录是否命中订阅、最终投递 QoS 及全部匹配项
    /// </summary>
    public struct CheckSubscriptionsResult
    {
        /// <summary>
        /// 是否有至少一个订阅过滤器匹配该主题
        /// </summary>
        public bool IsSubscribed { get; set; }

        /// <summary>
        /// 投递给该客户端的最终 QoS，取所有匹配项中的最高值；未订阅时无意义
        /// </summary>
        public MqttQualityOfServiceLevel QualityOfServiceLevel { get; set; }

        /// <summary>
        /// 所有匹配的订阅过滤器及其选项。无匹配时为空列表而不是 null。
        /// </summary>
        internal List<MqttSubscriptionMatch> Matches { get; set; }
    }
}
