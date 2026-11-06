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
*命名空间：SAEA.MQTT.Packets
*文件名： MqttConnAckPacketProperties
*版本号： v26.4.23.1
*唯一标识：4015c346-9b22-4c5e-8329-9a60e655647e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttConnAckPacketProperties接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System.Collections.Generic;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Packets
{
    /// <summary>
    /// CONNACK 报文的属性集合，均为 MQTT 5.0 新增
    /// </summary>
    public sealed class MqttConnAckPacketProperties
    {
        /// <summary>
        /// 会话过期时间，单位秒
        /// </summary>
        public uint? SessionExpiryInterval { get; set; }

        /// <summary>
        /// 服务端允许客户端同时处于未确认状态的 QoS 1/2 报文数量上限
        /// </summary>
        public ushort? ReceiveMaximum { get; set; }

        /// <summary>
        /// 服务端支持的最大 QoS 等级
        /// </summary>
        public MqttQualityOfServiceLevel? MaximumQoS { get; set; }

        /// <summary>
        /// 服务端是否支持保留消息
        /// </summary>
        public bool? RetainAvailable { get; set; }

        /// <summary>
        /// 服务端可接受的最大报文长度，单位字节
        /// </summary>
        public uint? MaximumPacketSize { get; set; }

        /// <summary>
        /// 服务端为客户端分配的客户端标识
        /// </summary>
        public string AssignedClientIdentifier { get; set; }

        /// <summary>
        /// 服务端允许客户端使用的主题别名数量上限
        /// </summary>
        public ushort? TopicAliasMaximum { get; set; }

        /// <summary>
        /// 可读的原因描述
        /// </summary>
        public string ReasonString { get; set; }

        /// <summary>
        /// 用户自定义属性
        /// </summary>
        public List<MqttUserProperty> UserProperties { get; set; }

        /// <summary>
        /// 服务端是否支持通配符订阅
        /// </summary>
        public bool? WildcardSubscriptionAvailable { get; set; }

        /// <summary>
        /// 服务端是否支持订阅标识符
        /// </summary>
        public bool? SubscriptionIdentifiersAvailable { get; set; }

        /// <summary>
        /// 服务端是否支持共享订阅
        /// </summary>
        public bool? SharedSubscriptionAvailable { get; set; }

        /// <summary>
        /// 服务端指定客户端应使用的保活间隔，单位秒
        /// </summary>
        public ushort? ServerKeepAlive { get; set; }

        /// <summary>
        /// 供客户端构造请求/响应主题的附加信息
        /// </summary>
        public string ResponseInformation { get; set; }

        /// <summary>
        /// 服务端建议客户端改用或重连的服务器地址
        /// </summary>
        public string ServerReference { get; set; }

        /// <summary>
        /// 认证方法名，与 CONNECT 中的取值对应
        /// </summary>
        public string AuthenticationMethod { get; set; }

        /// <summary>
        /// 认证数据，内容由认证方法定义
        /// </summary>
        public byte[] AuthenticationData { get; set; }
    }
}