using System;
using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Server;

namespace SAEA.MQTT.Extensions.ManagedClient
{
    public interface IManagedMqttClientOptions
    {
        IMqttClientOptions ClientOptions { get; }

        bool AutoReconnect { get; }

        TimeSpan AutoReconnectDelay { get; }

        TimeSpan ConnectionCheckInterval { get; }

        IManagedMqttClientStorage Storage { get; }

        int MaxPendingMessages { get; }

        MqttPendingMessagesOverflowStrategy PendingMessagesOverflowStrategy { get; }
    }
}