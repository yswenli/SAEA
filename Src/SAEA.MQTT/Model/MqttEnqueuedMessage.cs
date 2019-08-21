/****************************************************************************
*项目名称：SAEA.MQTT.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Model
*类 名 称：MqttEnqueuedMessage
*版 本 号： v5.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 15:56:10
*描述：
*=====================================================================
*修改时间：2019/1/15 15:56:10
*修 改 人： yswenli
*版 本 号： v5.0.0.1
*描    述：
*****************************************************************************/
using SAEA.MQTT.Core.Implementations;
using SAEA.MQTT.Core.Packets;

namespace SAEA.MQTT.Model
{
    public class MqttEnqueuedMessage
    {
        public MqttEnqueuedMessage(MqttClientSession sender, MqttPublishPacket publishPacket)
        {
            Sender = sender;
            PublishPacket = publishPacket;
        }

        public MqttClientSession Sender { get; }

        public MqttPublishPacket PublishPacket { get; }
    }
}
