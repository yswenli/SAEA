using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    public interface IMqttClientSession
    {
        string ClientId { get; }

        Task StopAsync();
    }
}