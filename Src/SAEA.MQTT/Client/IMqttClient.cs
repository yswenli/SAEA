using SAEA.MQTT.Client.Connecting;
using SAEA.MQTT.Client.Disconnecting;
using SAEA.MQTT.Client.ExtendedAuthenticationExchange;
using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Client.Subscribing;
using SAEA.MQTT.Client.Unsubscribing;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.Client
{
    public interface IMqttClient : IApplicationMessageReceiver, IApplicationMessagePublisher, IDisposable
    {
        bool IsConnected { get; }
        IMqttClientOptions Options { get; }

        IMqttClientConnectedHandler ConnectedHandler { get; set; }

        IMqttClientDisconnectedHandler DisconnectedHandler { get; set; }

        Task<MqttClientAuthenticateResult> ConnectAsync(IMqttClientOptions options, CancellationToken cancellationToken);
        Task DisconnectAsync(MqttClientDisconnectOptions options, CancellationToken cancellationToken);
        Task PingAsync(CancellationToken cancellationToken);

        Task SendExtendedAuthenticationExchangeDataAsync(MqttExtendedAuthenticationExchangeData data, CancellationToken cancellationToken);
        Task<MqttClientSubscribeResult> SubscribeAsync(MqttClientSubscribeOptions options, CancellationToken cancellationToken);
        Task<MqttClientUnsubscribeResult> UnsubscribeAsync(MqttClientUnsubscribeOptions options, CancellationToken cancellationToken);
    }
}