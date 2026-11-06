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
 *修改时间：2026/11/6 12:04:26
 *修改人： yswenli
 *版本号： v26.11.5.1
 *描述：补全公开成员的 XML 注释
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
        /// <summary>
        /// 服务端广播的可用接收窗口（Receive Maximum），即允许的并发未确认 QoS 1/2 消息数
        /// </summary>
        public ushort ReceiveMaximum { get; set; } = ushort.MaxValue;

        /// <summary>
        /// 服务端可接受的最大报文总长度（字节），默认 256 MB 减 1
        /// </summary>
        public uint MaximumPacketSize { get; set; } = 268435455;

        /// <summary>
        /// 服务端支持的入站主题别名数量，0 表示不支持主题别名
        /// </summary>
        public ushort TopicAliasMaximum { get; set; }

        /// <summary>
        /// 是否允许客户端发布 retained 消息，为 false 时 CONNACK 通告 Retain Available 为 0
        /// </summary>
        public bool RetainAvailable { get; set; } = true;

        /// <summary>
        /// 是否允许通配符订阅
        /// </summary>
        public bool WildcardSubscriptionAvailable { get; set; } = true;

        /// <summary>
        /// 是否允许订阅携带订阅标识符
        /// </summary>
        public bool SubscriptionIdentifiersAvailable { get; set; } = true;

        /// <summary>
        /// 是否允许共享订阅
        /// </summary>
        public bool SharedSubscriptionAvailable { get; set; } = true;

        /// <summary>
        /// 服务端支持的最高服务质量等级
        /// </summary>
        public MqttQualityOfServiceLevel MaximumQoS { get; set; } = MqttQualityOfServiceLevel.ExactlyOnce;

        /// <summary>
        /// 强制客户端使用的保活秒数，null 表示沿用客户端请求值
        /// </summary>
        public ushort? ServerKeepAlive { get; set; }

        /// <summary>
        /// 允许的会话过期时间上限（秒），null 表示不限制
        /// </summary>
        public uint? MaximumSessionExpiryInterval { get; set; }
    }
}