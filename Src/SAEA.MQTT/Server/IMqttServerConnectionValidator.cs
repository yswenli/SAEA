using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    public interface IMqttServerConnectionValidator
    {
        Task ValidateConnectionAsync(MqttConnectionValidatorContext context);
    }
}
