using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Server
{
    public struct CheckSubscriptionsResult
    {
        public bool IsSubscribed { get; set; }

        public MqttQualityOfServiceLevel QualityOfServiceLevel { get; set; }
    }
}
