/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MQTT.Implementations
*文件名： MqttClientAdapterFactory
*版本号： v26.4.23.1
*唯一标识：2ade3427-3f71-4b45-a4de-8a4b95f30368
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientAdapterFactory接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.MQTT.Adapter;
using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Diagnostics;
using SAEA.MQTT.Formatter;
using System;

namespace SAEA.MQTT.Implementations
{
    /// <summary>
    /// 客户端信道适配器工厂，按信道选项创建 TCP 或 WebSocket 适配器
    /// </summary>
    public class MqttClientAdapterFactory : IMqttClientAdapterFactory
    {
        readonly IMqttNetLogger _logger;

        /// <summary>
        /// 使用指定日志记录器创建客户端信道适配器工厂
        /// </summary>
        /// <param name="logger">根日志记录器，不能为null</param>
        public MqttClientAdapterFactory(IMqttNetLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 按客户端选项的信道类型创建适配器，TCP 走 MqttTcpChannel，WebSocket 走 MqttWebSocketChannel
        /// </summary>
        /// <param name="options">客户端连接选项，不能为null</param>
        /// <returns>尚未建立连接的客户端信道适配器</returns>
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
