using System;

namespace SAEA.MQTT.Extensions.ManagedClient
{
    public class ApplicationMessageSkippedEventArgs : EventArgs
    {
        public ApplicationMessageSkippedEventArgs(ManagedMqttApplicationMessage applicationMessage)
        {
            ApplicationMessage = applicationMessage ?? throw new ArgumentNullException(nameof(applicationMessage));
        }

        public ManagedMqttApplicationMessage ApplicationMessage { get; }
    }
}
