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
*文件名： MqttUnsubscribePacket
*版本号： v26.4.23.1
*唯一标识：1e7981a3-b2a7-4e65-ba14-818124ee1daf
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttUnsubscribePacket接口
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

namespace SAEA.MQTT.Packets
{
    /// <summary>
    /// UNSUBSCRIBE 报文，客户端请求取消一个或多个订阅
    /// </summary>
    public sealed class MqttUnsubscribePacket : MqttBasePacket, IMqttPacketWithIdentifier
    {
        /// <summary>
        /// 报文标识符，用于把 UNSUBACK 与本次取消订阅请求配对
        /// </summary>
        public ushort PacketIdentifier { get; set; }

        /// <summary>
        /// 待取消订阅的主题过滤器
        /// </summary>
        public List<string> TopicFilters { get; set; } = new List<string>();

        #region Added in MQTTv5

        /// <summary>
        /// 取消订阅属性，仅 MQTT 5.0 使用
        /// </summary>
        public MqttUnsubscribePacketProperties Properties { get; set; }

        #endregion

        /// <summary>
        /// 返回包含报文标识符与各主题过滤器的描述文本
        /// </summary>
        /// <returns>报文描述文本</returns>
        public override string ToString()
        {
            var topicFiltersText = string.Join(",", TopicFilters);
            return string.Concat("Unsubscribe: [PacketIdentifier=", PacketIdentifier, "] [TopicFilters=", topicFiltersText, "]");
        }
    }
}
