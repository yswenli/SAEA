using SAEA.MQTT.Adapter;
using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Diagnostics;
using SAEA.MQTT.Formatter;
using System;

namespace SAEA.MQTT.Implementations
{
    public class MqttClientAdapterFactory : IMqttClientAdapterFactory
    {
        readonly IMqttNetLogger _logger;

        public MqttClientAdapterFactory(IMqttNetLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IMqttChannelAdapter CreateClientAdapter(IMqttClientOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            switch (options.ChannelOptions)
            {
                case MqttClientTcpOptions _:
                    {
                        return new MqttChannelAdapter(new MqttTcpChannel(options), new MqttPacketFormatterAdapter(options.ProtocolVersion, new MqttPacketWriter()), _logger);
                    }

                case MqttClientWebSocketOptions webSocketOptions:
                    {
                        return new MqttChannelAdapter(new MqttWebSocketChannel(webSocketOptions), new MqttPacketFormatterAdapter(options.ProtocolVersion, new MqttPacketWriter()), _logger);
                    }

                default:
                    {
                        throw new NotSupportedException();
                    }
            }
        }
    }
}
