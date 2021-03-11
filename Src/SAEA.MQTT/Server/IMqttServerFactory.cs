using System;
using System.Collections.Generic;
using SAEA.MQTT.Adapter;
using SAEA.MQTT.Diagnostics;

namespace SAEA.MQTT.Server
{
    public interface IMqttServerFactory
    {
        IList<Func<IMqttFactory, IMqttServerAdapter>> DefaultServerAdapters { get; }

        IMqttServer CreateMqttServer();

        IMqttServer CreateMqttServer(IMqttNetLogger logger);

        IMqttServer CreateMqttServer(IEnumerable<IMqttServerAdapter> adapters);

        IMqttServer CreateMqttServer(IEnumerable<IMqttServerAdapter> adapters, IMqttNetLogger logger);
    }
}