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
 *文件名： MqttSharedSubscriptionMember
 *版本号： v26.4.23.1
 *唯一标识：8f3a1c7e-2b5d-4c90-8e1a-7d6f9b0c2a44
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/05 00:00:00
 *描述：MqttSharedSubscriptionMember类
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/05 00:00:00
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：MqttSharedSubscriptionMember类
 *
 *****************************************************************************/
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 共享订阅组中的一个成员。分发时由注册表轮转选取。
    /// </summary>
    internal sealed class MqttSharedSubscriptionMember
    {
        public string ClientId { get; set; }

        public MqttQualityOfServiceLevel QualityOfServiceLevel { get; set; }

        public uint? SubscriptionIdentifier { get; set; }

        public bool RetainAsPublished { get; set; }
    }
}