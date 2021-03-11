using System.Threading.Tasks;

namespace SAEA.MQTT.Client.ExtendedAuthenticationExchange
{
    public interface IMqttExtendedAuthenticationExchangeHandler
    {
        Task HandleRequestAsync(MqttExtendedAuthenticationExchangeContext context);
    }
}
