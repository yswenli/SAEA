using SAEA.MQTT.Packets;
using System;

namespace SAEA.MQTT.PacketDispatcher
{
    public interface IMqttPacketAwaiter : IDisposable
    {
        void Complete(MqttBasePacket packet);

        void Fail(Exception exception);

        void Cancel();
    }
}