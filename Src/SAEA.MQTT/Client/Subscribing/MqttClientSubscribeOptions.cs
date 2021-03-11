using System.Collections.Generic;
using SAEA.MQTT.Packets;

namespace SAEA.MQTT.Client.Subscribing
{
    public class MqttClientSubscribeOptions
    {
        public List<MqttTopicFilter> TopicFilters { get; set; } = new List<MqttTopicFilter>();

        public uint? SubscriptionIdentifier { get; set; }

        public List<MqttUserProperty> UserProperties { get; set; }
    }
}
