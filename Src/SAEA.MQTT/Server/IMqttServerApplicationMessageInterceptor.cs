using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    public interface IMqttServerApplicationMessageInterceptor
    {
        Task InterceptApplicationMessagePublishAsync(MqttApplicationMessageInterceptorContext context);
    }
}
