using SAEA.MQTT.Client.Receiving;

namespace SAEA.MQTT
{
    public interface IApplicationMessageReceiver
    {
        IMqttApplicationMessageReceivedHandler ApplicationMessageReceivedHandler { get; set; }
    }
}
