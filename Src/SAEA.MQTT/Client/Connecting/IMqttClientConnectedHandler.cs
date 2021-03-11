using System.Threading.Tasks;

namespace SAEA.MQTT.Client.Connecting
{
    public interface IMqttClientConnectedHandler
    {
        Task HandleConnectedAsync(MqttClientConnectedEventArgs eventArgs);
    }
}
