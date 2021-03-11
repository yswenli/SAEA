using System;
using SAEA.MQTT.Adapter;
using SAEA.MQTT.Packets;

namespace SAEA.MQTT.Formatter
{
    public interface IMqttPacketFormatter
    {
        IMqttDataConverter DataConverter { get; }

        ArraySegment<byte> Encode(MqttBasePacket mqttPacket);

        MqttBasePacket Decode(ReceivedMqttPacket receivedMqttPacket);

        void FreeBuffer();
    }
}