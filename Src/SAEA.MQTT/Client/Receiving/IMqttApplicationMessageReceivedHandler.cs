using System.Threading.Tasks;

namespace SAEA.MQTT.Client.Receiving
{
    public interface IMqttApplicationMessageReceivedHandler
    {
        Task HandleApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs eventArgs);
    }
}
