/****************************************************************************
*项目名称：SAEA.MQTT.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Core.Packets
*类 名 称：MqttPublishPacket
*版 本 号： v4.1.2.5
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 10:37:36
*描述：
*=====================================================================
*修改时间：2019/1/15 10:37:36
*修 改 人： yswenli
*版 本 号： v4.1.2.5
*描    述：
*****************************************************************************/

using SAEA.MQTT.Core.Protocol;
using SAEA.MQTT.Model;

namespace SAEA.MQTT.Core.Packets
{
    public class MqttPublishPacket : MqttBasePublishPacket
    {
        public bool Retain { get; set; }

        public MqttQualityOfServiceLevel QualityOfServiceLevel { get; set; }

        public bool Dup { get; set; }

        public string Topic { get; set; }

        public byte[] Payload { get; set; }

        public override string ToString()
        {
            return "Publish: [Topic=" + Topic + "]" +
                " [Payload.Length=" + Payload?.Length + "]" +
                " [QoSLevel=" + QualityOfServiceLevel + "]" +
                " [Dup=" + Dup + "]" +
                " [Retain=" + Retain + "]" +
                " [PacketIdentifier=" + PacketIdentifier + "]";
        }
    }
}
