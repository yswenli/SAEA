using System.Collections.Generic;

namespace SAEA.MQTT.Packets
{
    public sealed class MqttUnsubscribePacketProperties
    {
        public List<MqttUserProperty> UserProperties { get; set; }
    }
}
