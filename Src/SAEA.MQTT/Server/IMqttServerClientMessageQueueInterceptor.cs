using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    public interface IMqttServerClientMessageQueueInterceptor
    {
        Task InterceptClientMessageQueueEnqueueAsync(MqttClientMessageQueueInterceptorContext context);
    }
}
