using System;
using System.Threading;
using System.Threading.Tasks;
using SAEA.MQTT.Client;
using SAEA.MQTT.Client.Connecting;
using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Formatter;

namespace SAEA.MQTT.Tests.TestInfrastructure
{
    internal sealed class MqttTestClient : IAsyncDisposable
    {
        private readonly IMqttClient _client;

        private MqttTestClient(IMqttClient client)
        {
            _client = client;
        }

        public IMqttClient Client => _client;

        public MqttClientAuthenticateResult ConnectResult { get; private set; }

        public static async Task<MqttTestClient> ConnectAsync(
            int port,
            MqttProtocolVersion version,
            string clientId = null,
            Action<MqttClientOptionsBuilder> configure = null,
            CancellationToken cancellationToken = default)
        {
            var client = new MqttFactory().CreateMqttClient();

            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer("127.0.0.1", port)
                .WithProtocolVersion(version)
                .WithClientId(clientId ?? "test-" + Guid.NewGuid().ToString("N"))
                .WithCleanSession()
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(5))
                .WithCommunicationTimeout(TimeSpan.FromSeconds(10));

            configure?.Invoke(optionsBuilder);

            var testClient = new MqttTestClient(client);
            testClient.ConnectResult = await client
                .ConnectAsync(optionsBuilder.Build(), cancellationToken)
                .ConfigureAwait(false);

            return testClient;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_client.IsConnected)
                {
                    await _client.DisconnectAsync().ConfigureAwait(false);
                }
            }
            catch
            {
                // Disconnect is best effort during test cleanup.
            }

            _client.Dispose();
        }
    }
}