namespace SAEA.MQTT.Packets
{
    public interface IMqttPacketWithIdentifier
    {
        ushort PacketIdentifier { get; set; }
    }
}
