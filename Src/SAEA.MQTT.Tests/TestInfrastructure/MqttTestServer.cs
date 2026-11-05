using System;
using System.Net;
using System.Threading.Tasks;
using SAEA.MQTT.Server;

namespace SAEA.MQTT.Tests.TestInfrastructure
{
    internal sealed class MqttTestServer : IAsyncDisposable
    {
        private readonly IMqttServer _server;

        private MqttTestServer(IMqttServer server, int port)
        {
            _server = server;
            Port = port;
        }

        public int Port { get; }

        public IMqttServer Server => _server;

        public MqttClientSessionsManager ClientSessionsManager
        {
            get
            {
                var server = _server as MqttServer;
                if (server == null)
                {
                    throw new InvalidOperationException("The test server is not a MqttServer instance.");
                }

                return server.ClientSessionsManager;
            }
        }

        public static async Task<MqttTestServer> StartAsync(Action<MqttServerOptionsBuilder> configure = null)
        {
            var port = FreePort.GetFreePort();

            var optionsBuilder = new MqttServerOptionsBuilder()
                .WithDefaultEndpoint()
                .WithDefaultEndpointPort(port)
                .WithDefaultEndpointBoundIPAddress(IPAddress.Loopback)
                .WithDefaultEndpointBoundIPV6Address(IPAddress.None);

            configure?.Invoke(optionsBuilder);

            var server = new MqttFactory().CreateMqttServer();
            await server.StartAsync(optionsBuilder.Build()).ConfigureAwait(false);

            return new MqttTestServer(server, port);
        }

        public async Task StopAsync()
        {
            try
            {
                await _server.StopAsync().ConfigureAwait(false);
            }
            catch
            {
                // Tests only care about a clean listener shutdown.
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);

            try
            {
                _server.Dispose();
            }
            catch
            {
                // Dispose calls StopAsync again. Ignore failures from partially started servers.
            }
        }
    }
}