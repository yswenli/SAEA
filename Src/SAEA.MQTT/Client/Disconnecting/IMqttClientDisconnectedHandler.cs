using System.Threading.Tasks;

namespace SAEA.MQTT.Client.Disconnecting
{
    public interface IMqttClientDisconnectedHandler
    {
        Task HandleDisconnectedAsync(MqttClientDisconnectedEventArgs eventArgs);
    }
}
