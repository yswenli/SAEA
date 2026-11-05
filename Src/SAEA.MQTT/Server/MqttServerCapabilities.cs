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
 *文件名： MqttServerCapabilities
 *版本号： v26.4.23.1
 *唯一标识：c1f3c4b1-6a6e-4a5e-9d2f-3f0a55f6a101
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2021/03/11 16:46:45
 *描述：MqttServerCapabilities类
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/05 00:00:00
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：MqttServerCapabilities类
 *
 *****************************************************************************/
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 服务端在 CONNACK 中通告并在运行时执行的能力。
    /// </summary>
    public sealed class MqttServerCapabilities
    {
        public ushort ReceiveMaximum { get; set; } = ushort.MaxValue;

        public uint MaximumPacketSize { get; set; } = 268435455;

        public ushort TopicAliasMaximum { get; set; }

        public bool RetainAvailable { get; set; } = true;

        public bool WildcardSubscriptionAvailable { get; set; } = true;

        public bool SubscriptionIdentifiersAvailable { get; set; } = true;

        public bool SharedSubscriptionAvailable { get; set; } = true;

        public MqttQualityOfServiceLevel MaximumQoS { get; set; } = MqttQualityOfServiceLevel.ExactlyOnce;

        public ushort? ServerKeepAlive { get; set; }

        public uint? MaximumSessionExpiryInterval { get; set; }
    }
}