using SAEA.MQTT.Formatter;

namespace SAEA.MQTT.Adapter
{
    public sealed class ReceivedMqttPacket
    {
        public ReceivedMqttPacket(byte fixedHeader, IMqttPacketBodyReader body, int totalLength)
        {
            FixedHeader = fixedHeader;
            Body = body;
            TotalLength = totalLength;
        }

        public byte FixedHeader { get; }

        public IMqttPacketBodyReader Body { get; }

        public int TotalLength { get; }
    }
}
