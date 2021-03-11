using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    public interface IMqttServerClientConnectedHandler
    {
        Task HandleClientConnectedAsync(MqttServerClientConnectedEventArgs eventArgs);
    }
}
