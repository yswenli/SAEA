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
 *命名空间：SAEA.MQTT.Internal
 *文件名： MqttConnectionCapabilities
 *版本号： v26.4.23.1
 *唯一标识：d0a51c92-2c6e-4f4c-8f8e-5e7b3f0c1102
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2021/03/11 16:46:45
 *描述：MqttConnectionCapabilities类
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/05 00:00:00
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：MqttConnectionCapabilities类
 *
 *****************************************************************************/
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Internal
{
    /// <summary>
    /// 每个连接一份的能力快照与协商结果，是运行时所有能力相关决策的唯一事实来源。
    /// </summary>
    internal sealed class MqttConnectionCapabilities
    {
        public MqttProtocolVersion ProtocolVersion { get; set; }

        // 本端（服务端）通告并执行
        public ushort ReceiveMaximum { get; set; } = ushort.MaxValue;

        public uint MaximumPacketSize { get; set; } = 268435455;

        public ushort TopicAliasMaximum { get; set; }

        public MqttQualityOfServiceLevel MaximumQoS { get; set; } = MqttQualityOfServiceLevel.ExactlyOnce;

        public bool RetainAvailable { get; set; } = true;

        public bool WildcardSubscriptionAvailable { get; set; } = true;

        public bool SubscriptionIdentifiersAvailable { get; set; } = true;

        public bool SharedSubscriptionAvailable { get; set; } = true;

        public ushort? ServerKeepAlive { get; set; }

        public uint MaximumSessionExpiryInterval { get; set; } = uint.MaxValue;

        // 对端（客户端）通告，约束本端出站
        public ushort ClientReceiveMaximum { get; set; } = ushort.MaxValue;

        public uint ClientMaximumPacketSize { get; set; } = 268435455;

        public ushort ClientTopicAliasMaximum { get; set; }

        // 协商结果
        public uint SessionExpiryInterval { get; set; }

        public string AssignedClientIdentifier { get; set; }

        public bool RequestProblemInformation { get; set; } = true;

        public bool IsV5 => ProtocolVersion == MqttProtocolVersion.V500;
    }
}