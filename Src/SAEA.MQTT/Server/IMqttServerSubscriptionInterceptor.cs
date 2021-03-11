using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    public interface IMqttServerSubscriptionInterceptor
    {
        Task InterceptSubscriptionAsync(MqttSubscriptionInterceptorContext context);
    }
}
