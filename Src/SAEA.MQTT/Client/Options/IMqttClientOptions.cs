using SAEA.MQTT.Client.ExtendedAuthenticationExchange;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Packets;
using System;
using System.Collections.Generic;

namespace SAEA.MQTT.Client.Options
{
    public interface IMqttClientOptions
    {
        string ClientId { get; }
        bool CleanSession { get; }
        IMqttClientCredentials Credentials { get; }
        IMqttExtendedAuthenticationExchangeHandler ExtendedAuthenticationExchangeHandler { get; }
        MqttProtocolVersion ProtocolVersion { get; }
        IMqttClientChannelOptions ChannelOptions { get; }

        TimeSpan CommunicationTimeout { get; }
        TimeSpan KeepAlivePeriod { get; }
        MqttApplicationMessage WillMessage { get; }
        uint? WillDelayInterval { get; }

        string AuthenticationMethod { get; }
        byte[] AuthenticationData { get; }
        uint? MaximumPacketSize { get; }
        ushort? ReceiveMaximum { get; }
        bool? RequestProblemInformation { get; }
        bool? RequestResponseInformation { get; }
        uint? SessionExpiryInterval { get; }
        ushort? TopicAliasMaximum { get; }
        List<MqttUserProperty> UserProperties { get; set; }
    }
}