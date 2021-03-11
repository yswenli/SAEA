using System;
using System.Threading.Tasks;
using SAEA.MQTT.Server;

namespace SAEA.MQTT.Adapter
{
    public interface IMqttServerAdapter : IDisposable
    {
        Func<IMqttChannelAdapter, Task> ClientHandler { get; set; }

        Task StartAsync(IMqttServerOptions options);
        Task StopAsync();
    }
}
