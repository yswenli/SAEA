using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    public interface IMqttServerClientUnsubscribedTopicHandler
    {
        Task HandleClientUnsubscribedTopicAsync(MqttServerClientUnsubscribedTopicEventArgs eventArgs);
    }
}
