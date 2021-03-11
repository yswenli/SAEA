using System;

namespace SAEA.MQTT.Extensions.ManagedClient
{
    public class ManagedMqttApplicationMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public MqttApplicationMessage ApplicationMessage { get; set; }
    }
}
