using System;

namespace SAEA.MQTT.Client.Connecting
{
    public class MqttClientConnectedEventArgs : EventArgs
    {
        public MqttClientConnectedEventArgs(MqttClientAuthenticateResult authenticateResult)
        {
            AuthenticateResult = authenticateResult ?? throw new ArgumentNullException(nameof(authenticateResult));
        }

        public MqttClientAuthenticateResult AuthenticateResult { get; }
    }
}
