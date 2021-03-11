using System.Collections.Generic;

namespace SAEA.MQTT.Packets
{
    public sealed class MqttPubRelPacketProperties
    {
        public string ReasonString { get; set; }

        public List<MqttUserProperty> UserProperties { get; set; }
    }
}
