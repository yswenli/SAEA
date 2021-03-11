using SAEA.MQTT.Client.Options;

namespace SAEA.MQTT.Adapter
{
    public interface IMqttClientAdapterFactory
    {
        IMqttChannelAdapter CreateClientAdapter(IMqttClientOptions options);
    }
}
