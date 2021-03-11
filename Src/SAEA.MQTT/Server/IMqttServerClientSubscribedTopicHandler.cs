using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    public interface IMqttServerClientSubscribedTopicHandler
    {
        Task HandleClientSubscribedTopicAsync(MqttServerClientSubscribedTopicEventArgs eventArgs);
    }
}
