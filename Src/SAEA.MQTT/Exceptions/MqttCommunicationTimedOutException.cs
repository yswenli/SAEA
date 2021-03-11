using System;

namespace SAEA.MQTT.Exceptions
{
    public class MqttCommunicationTimedOutException : MqttCommunicationException
    {
        public MqttCommunicationTimedOutException()
        {
        }

        public MqttCommunicationTimedOutException(Exception innerException) : base(innerException)
        {
        }
    }
}
