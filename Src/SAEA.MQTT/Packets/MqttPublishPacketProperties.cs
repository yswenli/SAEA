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
*文件名： MqttPublishPacketProperties
*版本号： v26.4.23.1
*唯一标识：07a9ecdc-eb01-4029-acd6-0e574c906800
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttPublishPacketProperties接口
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
    /// PUBLISH 报文的属性集合，均为 MQTT 5.0 新增
    /// </summary>
    public sealed class MqttPublishPacketProperties
    {
        /// <summary>
        /// 载荷格式指示，用于声明载荷是字节流还是 UTF-8 文本
        /// </summary>
        public MqttPayloadFormatIndicator? PayloadFormatIndicator { get; set; }

        /// <summary>
        /// 消息过期时间，单位秒，超时后服务端不再投递
        /// </summary>
        public uint? MessageExpiryInterval { get; set; }

        /// <summary>
        /// 主题别名，与主题名二选一，需在对方通告的上限内
        /// </summary>
        public ushort? TopicAlias { get; set; }

        /// <summary>
        /// 响应主题，供请求/响应模式的对端回复
        /// </summary>
        public string ResponseTopic { get; set; }

        /// <summary>
        /// 关联数据，用于把响应与请求配对
        /// </summary>
        public byte[] CorrelationData { get; set; }

        /// <summary>
        /// 用户自定义属性
        /// </summary>
        public List<MqttUserProperty> UserProperties { get; set; }

        /// <summary>
        /// 订阅标识符列表，由服务端在转发时按匹配的订阅写入
        /// </summary>
        public List<uint> SubscriptionIdentifiers { get; set; }

        /// <summary>
        /// 载荷的内容类型描述
        /// </summary>
        public string ContentType { get; set; }
    }
}
