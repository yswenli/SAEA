using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    public interface IMqttServerUnsubscriptionInterceptor
    {
        Task InterceptUnsubscriptionAsync(MqttUnsubscriptionInterceptorContext context);
    }
}
