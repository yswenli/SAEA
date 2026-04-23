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
*唯一标识：3f740c24-bc2f-4246-85a7-c6567b8c7e79
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MQTT协议数据包类
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MQTT协议数据包类
*
*****************************************************************************/
using System.Collections.Generic;
using System.Linq;

namespace SAEA.MQTT.Packets
{
    public sealed class MqttSubscribePacket : MqttBasePacket, IMqttPacketWithIdentifier
    {
        public ushort PacketIdentifier { get; set; }

        public List<MqttTopicFilter> TopicFilters { get; set; } = new List<MqttTopicFilter>();

        #region Added in MQTTv5

        public MqttSubscribePacketProperties Properties { get; set; }

        #endregion

        public override string ToString()
        {
            var topicFiltersText = string.Join(",", TopicFilters.Select(f => f.Topic + "@" + f.QualityOfServiceLevel));
            return string.Concat("Subscribe: [PacketIdentifier=", PacketIdentifier, "] [TopicFilters=", topicFiltersText, "]");
        }
    }
}
