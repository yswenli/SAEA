/****************************************************************************
*项目名称：SAEA.MQTT.Common.Serializer
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Common.Serializer
*类 名 称：IMqttPacketSerializer
*版 本 号： V3.6.2.2
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 10:13:46
*描述：
*=====================================================================
*修改时间：2019/1/15 10:13:46
*修 改 人： yswenli
*版 本 号： V3.6.2.2
*描    述：
*****************************************************************************/
using SAEA.MQTT.Core.Packets;
using SAEA.MQTT.Core.Protocol;
using System;

namespace SAEA.MQTT.Common.Serializer
{
    public interface IMqttPacketSerializer
    {
        MqttProtocolVersion ProtocolVersion { get; set; }

        ArraySegment<byte> Serialize(MqttBasePacket mqttPacket);

        MqttBasePacket Deserialize(MqttReceivedPacket receivedMqttPacket);

        void FreeBuffer();
    }
}
