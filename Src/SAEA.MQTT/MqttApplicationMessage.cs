using System.Collections.Generic;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT
{
    public class MqttApplicationMessage
    {
        public string Topic { get; set; }

        public byte[] Payload { get; set; }

        public MqttQualityOfServiceLevel QualityOfServiceLevel { get; set; }

        public bool Retain { get; set; }

        public List<MqttUserProperty> UserProperties { get; set; }

        public string ContentType { get; set; }

        public string ResponseTopic { get; set; }

        public MqttPayloadFormatIndicator? PayloadFormatIndicator { get; set; }

        public uint? MessageExpiryInterval { get; set; }

        public ushort? TopicAlias { get; set; }

        public byte[] CorrelationData { get; set; }

        public List<uint> SubscriptionIdentifiers { get; set; }
    }
}
