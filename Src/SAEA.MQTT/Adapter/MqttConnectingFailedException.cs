using SAEA.MQTT.Client.Connecting;
using SAEA.MQTT.Exceptions;

namespace SAEA.MQTT.Adapter
{
    public class MqttConnectingFailedException : MqttCommunicationException
    {
        public MqttConnectingFailedException(MqttClientAuthenticateResult result)
            : base($"Connecting with MQTT server failed ({result.ResultCode.ToString()}).")
        {
            Result = result;
        }

        public MqttClientAuthenticateResult Result { get; }
        public MqttClientConnectResultCode ResultCode => Result.ResultCode;
    }
}
