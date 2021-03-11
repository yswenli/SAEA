using System.Collections.Generic;
using SAEA.MQTT.Client;
using SAEA.MQTT.Diagnostics;
using SAEA.MQTT.Server;

namespace SAEA.MQTT
{
    public interface IMqttFactory : IMqttClientFactory, IMqttServerFactory
    {
        IMqttNetLogger DefaultLogger { get; }

        IDictionary<object, object> Properties { get; }
    }
}
