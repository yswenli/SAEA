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
*文件名： MqttSubscribePacket
*版本号： v26.4.23.1
*唯一标识：793561e7-6dd6-4ae7-a755-8fe938553397
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttSubscribePacket接口
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
using System.Linq;

namespace SAEA.MQTT.Packets
{
    /// <summary>
    /// SUBSCRIBE 报文，客户端请求订阅一个或多个主题过滤器
    /// </summary>
    public sealed class MqttSubscribePacket : MqttBasePacket, IMqttPacketWithIdentifier
    {
        /// <summary>
        /// 报文标识符，用于把 SUBACK 与本次订阅请求配对
        /// </summary>
        public ushort PacketIdentifier { get; set; }

        /// <summary>
        /// 待订阅的主题过滤器及其 QoS 要求
        /// </summary>
        public List<MqttTopicFilter> TopicFilters { get; set; } = new List<MqttTopicFilter>();

        #region Added in MQTTv5

        /// <summary>
        /// 订阅属性，仅 MQTT 5.0 使用
        /// </summary>
        public MqttSubscribePacketProperties Properties { get; set; }

        #endregion

        /// <summary>
        /// 返回包含报文标识符与各主题过滤器的描述文本
        /// </summary>
        /// <returns>报文描述文本</returns>
        public override string ToString()
        {
            var topicFiltersText = string.Join(",", TopicFilters.Select(f => f.Topic + "@" + f.QualityOfServiceLevel));
            return string.Concat("Subscribe: [PacketIdentifier=", PacketIdentifier, "] [TopicFilters=", topicFiltersText, "]");
        }
    }
}
