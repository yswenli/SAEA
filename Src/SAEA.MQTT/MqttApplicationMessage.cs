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
*命名空间：SAEA.MQTT
*文件名： MqttApplicationMessage
*版本号： v26.4.23.1
*唯一标识：53ddb36d-64f8-4d98-a5f5-8e8cac9dcf81
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttApplicationMessage接口
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
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT
{
    /// <summary>
    /// 应用层消息，承载主题、载荷与 MQTT 5.0 扩展属性
    /// </summary>
    public class MqttApplicationMessage
    {
        /// <summary>
        /// 消息主题，发布时主题与主题别名不能同时为空
        /// </summary>
        public string Topic { get; set; }

        /// <summary>
        /// 消息载荷原始字节，为null表示不携带载荷
        /// </summary>
        public byte[] Payload { get; set; }

        /// <summary>
        /// 发布使用的服务质量等级，默认 AtMostOnce
        /// </summary>
        public MqttQualityOfServiceLevel QualityOfServiceLevel { get; set; }

        /// <summary>
        /// 是否作为保留消息发布，服务端会将其保存为该主题的最新保留消息
        /// </summary>
        public bool Retain { get; set; }

        /// <summary>
        /// MQTT 5.0 用户自定义属性集合，为null或空表示不携带
        /// </summary>
        public List<MqttUserProperty> UserProperties { get; set; }

        /// <summary>
        /// MQTT 5.0 内容类型，用于描述载荷的 MIME 类型
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// MQTT 5.0 响应主题，请求响应模式下接收方回复该主题
        /// </summary>
        public string ResponseTopic { get; set; }

        /// <summary>
        /// MQTT 5.0 载荷格式指示，为null表示不携带该属性
        /// </summary>
        public MqttPayloadFormatIndicator? PayloadFormatIndicator { get; set; }

        /// <summary>
        /// MQTT 5.0 消息过期间隔，单位为秒，为null表示不携带该属性
        /// </summary>
        public uint? MessageExpiryInterval { get; set; }

        /// <summary>
        /// MQTT 5.0 主题别名，有效范围 1-65535，为null表示不使用别名
        /// </summary>
        public ushort? TopicAlias { get; set; }

        /// <summary>
        /// MQTT 5.0 关联数据，请求响应模式下用于关联请求与响应
        /// </summary>
        public byte[] CorrelationData { get; set; }

        /// <summary>
        /// MQTT 5.0 订阅标识符集合，由服务端在转发时按匹配的订阅写入
        /// </summary>
        public List<uint> SubscriptionIdentifiers { get; set; }
    }
}
