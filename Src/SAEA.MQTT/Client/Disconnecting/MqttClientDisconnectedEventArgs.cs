using System;
using SAEA.MQTT.Client.Connecting;

namespace SAEA.MQTT.Client.Disconnecting
{
    public sealed class MqttClientDisconnectedEventArgs : EventArgs
    {
        public MqttClientDisconnectedEventArgs(bool clientWasConnected, Exception exception, MqttClientAuthenticateResult authenticateResult, MqttClientDisconnectReason reason)
        {
            ClientWasConnected = clientWasConnected;
            Exception = exception;
            AuthenticateResult = authenticateResult;
            Reason = reason;

            ReasonCode = reason;
        }

        public bool ClientWasConnected { get; }

        public Exception Exception { get; }

        public MqttClientAuthenticateResult AuthenticateResult { get; }

        public MqttClientDisconnectReason Reason { get; set; }

        [Obsolete("Please use 'Reason' instead. This property will be removed in the future!")]
        public MqttClientDisconnectReason ReasonCode { get; set; }
    }
}
