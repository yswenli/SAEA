/****************************************************************************
*项目名称：SAEA.MQTT.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Core.Packets
*类 名 称：MqttConnAckPacket
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

namespace SAEA.MQTT.Core.Packets
{
    public class MqttConnectPacket : MqttBasePacket
    {
        public MqttProtocolVersion ProtocolVersion { get; set; }

        public string ClientId { get; set; }

        public string Username { get; set; }

        public string Password { get; set; }

        public ushort KeepAlivePeriod { get; set; }

        public bool CleanSession { get; set; }

        public MqttMessage WillMessage { get; set; }

        public override string ToString()
        {
            return "Connect: [ClientId=" + ClientId + "] [Username=" + Username + "] [Password=" + Password + "] [KeepAlivePeriod=" + KeepAlivePeriod + "] [CleanSession=" + CleanSession + "]";
        }
    }
}
