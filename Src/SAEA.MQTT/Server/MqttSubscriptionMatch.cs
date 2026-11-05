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
 *文件名： MqttSubscriptionMatch
 *版本号： v26.4.23.1
 *唯一标识：3b791f8d-6b1e-4a3a-9b4f-2d0f2e6b7c11
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/05 00:00:00
 *描述：MqttSubscriptionMatch类
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/05 00:00:00
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：MqttSubscriptionMatch类
 *
 *****************************************************************************/
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 单个订阅过滤器对某个主题的匹配结果及其生效选项。
    /// </summary>
    internal struct MqttSubscriptionMatch
    {
        /// <summary>
        /// 用于匹配的过滤器（共享订阅时为解析后的内部过滤器）。
        /// </summary>
        public string Filter { get; set; }

        public MqttQualityOfServiceLevel QualityOfServiceLevel { get; set; }

        public bool NoLocal { get; set; }

        public bool RetainAsPublished { get; set; }

        public MqttRetainHandling RetainHandling { get; set; }

        public uint? SubscriptionIdentifier { get; set; }

        public bool IsShared { get; set; }

        public string ShareName { get; set; }
    }
}