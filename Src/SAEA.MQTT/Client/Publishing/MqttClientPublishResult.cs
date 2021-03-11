
using System.Collections.Generic;
using SAEA.MQTT.Packets;

namespace SAEA.MQTT.Client.Publishing
{
    public class MqttClientPublishResult
    {
        public ushort? PacketIdentifier { get; set; }

        public MqttClientPublishReasonCode ReasonCode { get; set; } = MqttClientPublishReasonCode.Success;

        public string ReasonString { get; set; }

        public List<MqttUserProperty> UserProperties { get; set; }
    }
}
