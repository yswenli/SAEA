using System;

namespace SAEA.MQTT.Exceptions
{
    public class MqttProtocolViolationException : Exception
    {
        public MqttProtocolViolationException(string message)
            : base(message)
        {
        }
    }
}
