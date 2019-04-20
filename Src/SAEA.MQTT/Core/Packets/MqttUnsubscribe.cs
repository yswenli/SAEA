/****************************************************************************
*项目名称：SAEA.MQTT.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Core.Packets
*类 名 称：MqttUnsubscribePacket
*版 本 号： v4.3.3.7
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 10:37:36
*描述：
*=====================================================================
*修改时间：2019/1/15 10:37:36
*修 改 人： yswenli
*版 本 号： v4.3.3.7
*描    述：
*****************************************************************************/
using System.Collections.Generic;

namespace SAEA.MQTT.Core.Packets
{
    public class MqttUnsubscribePacket : MqttBasePacket, IMqttPacketWithIdentifier
    {
        public ushort? PacketIdentifier { get; set; }

        public IList<string> TopicFilters { get; set; } = new List<string>();

        public override string ToString()
        {
            var topicFiltersText = string.Join(",", TopicFilters);
            return "Unsubscribe: [PacketIdentifier=" + PacketIdentifier + "] [TopicFilters=" + topicFiltersText + "]";
        }
    }
}
