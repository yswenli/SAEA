using System.Collections.Generic;

namespace SAEA.MQTT.Client.Unsubscribing
{
    public class MqttClientUnsubscribeResult
    {
        public List<MqttClientUnsubscribeResultItem> Items { get; }  =new List<MqttClientUnsubscribeResultItem>();
    }
}
