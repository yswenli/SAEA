﻿namespace SAEA.MQTT.Server
{
    public class MqttServerTcpEndpointOptions : MqttServerTcpEndpointBaseOptions
    {
        public MqttServerTcpEndpointOptions()
        {
            IsEnabled = true;
            Port = 1883;
        }
    }
}
