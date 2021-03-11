using System;
using SAEA.MQTT.Adapter;
using SAEA.MQTT.Diagnostics;
using SAEA.MQTT.LowLevelClient;

namespace SAEA.MQTT.Client
{
    public interface IMqttClientFactory
    {
        IMqttFactory UseClientAdapterFactory(IMqttClientAdapterFactory clientAdapterFactory);

        ILowLevelMqttClient CreateLowLevelMqttClient();

        ILowLevelMqttClient CreateLowLevelMqttClient(IMqttNetLogger logger);

        ILowLevelMqttClient CreateLowLevelMqttClient(IMqttClientAdapterFactory clientAdapterFactory);

        ILowLevelMqttClient CreateLowLevelMqttClient(IMqttNetLogger logger, IMqttClientAdapterFactory clientAdapterFactory);

        IMqttClient CreateMqttClient();

        IMqttClient CreateMqttClient(IMqttNetLogger logger);

        IMqttClient CreateMqttClient(IMqttClientAdapterFactory adapterFactory);

        IMqttClient CreateMqttClient(IMqttNetLogger logger, IMqttClientAdapterFactory adapterFactory);
    }
}