/****************************************************************************
*项目名称：SAEA.MQTT.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Core.Packets
*类 名 称：MqttSubAckPacket
*版 本 号： v4.5.1.2
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 10:37:36
*描述：
*=====================================================================
*修改时间：2019/1/15 10:37:36
*修 改 人： yswenli
*版 本 号： v4.5.1.2
*描    述：
*****************************************************************************/
using SAEA.MQTT.Core.Protocol;
using SAEA.MQTT.Model;
using System.Collections.Generic;
using System.Linq;

namespace SAEA.MQTT.Core.Packets
{
    public class MqttSubAckPacket : MqttBasePacket, IMqttPacketWithIdentifier
    {
        public ushort? PacketIdentifier { get; set; }

        public IList<MqttSubscribeReturnCode> SubscribeReturnCodes { get; } = new List<MqttSubscribeReturnCode>();

        public override string ToString()
        {
            var subscribeReturnCodesText = string.Join(",", SubscribeReturnCodes.Select(f => f.ToString()));
            return "SubAck: [PacketIdentifier=" + PacketIdentifier + "] [SubscribeReturnCodes=" + subscribeReturnCodesText + "]";
        }
    }
}
