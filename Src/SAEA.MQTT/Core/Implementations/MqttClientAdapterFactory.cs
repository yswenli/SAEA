/****************************************************************************
*项目名称：SAEA.MQTT
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Core.Implementations
*类 名 称：MqttClientAdapterFactory
*版 本 号： V4.1.2.2
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/14 19:07:44
*描述：
*=====================================================================
*修改时间：2019/1/14 19:07:44
*修 改 人： yswenli
*版 本 号： V4.1.2.2
*描    述：
*****************************************************************************/
using SAEA.MQTT.Common.Log;
using SAEA.MQTT.Common.Serializer;
using SAEA.MQTT.Interface;
using SAEA.MQTT.Model;
using System;

namespace SAEA.MQTT.Core.Implementations
{
    public class MqttClientAdapterFactory : IMqttClientAdapterFactory
    {
        public IMqttChannelAdapter CreateClientAdapter(IMqttClientOptions options, IMqttNetChildLogger logger)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var serializer = new MqttPacketSerializer { ProtocolVersion = options.ProtocolVersion };

            switch (options.ChannelOptions)
            {
                case MqttClientTcpOptions _:
                    {
                        return new MqttChannelAdapter(new MqttTcpChannel(options), serializer, logger);
                    }

                case MqttClientWebSocketOptions webSocketOptions:
                    {
                        return new MqttChannelAdapter(new MqttWebSocketChannel(webSocketOptions), serializer, logger);
                    }

                default:
                    {
                        throw new NotSupportedException();
                    }
            }
        }
    }
}
