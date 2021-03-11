using System.Collections.Generic;

namespace SAEA.MQTT.Client.Subscribing
{
    public class MqttClientSubscribeResult
    {
        public List<MqttClientSubscribeResultItem> Items { get; } = new List<MqttClientSubscribeResultItem>();
    }
}
