using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    public interface IMqttServerClientDisconnectedHandler
    {
        Task HandleClientDisconnectedAsync(MqttServerClientDisconnectedEventArgs eventArgs);
    }
}
