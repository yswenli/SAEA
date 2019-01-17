/****************************************************************************
*项目名称：SAEA.MQTT
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT
*类 名 称：MqttFactory
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 10:03:45
*描述：
*=====================================================================
*修改时间：2019/1/15 10:03:45
*修 改 人： yswenli
*版 本 号： V3.6.2.2
*描    述：
*****************************************************************************/
using SAEA.MQTT.Common.Log;
using SAEA.MQTT.Core;
using SAEA.MQTT.Core.Implementations;
using SAEA.MQTT.Interface;
using System;
using System.Collections.Generic;

namespace SAEA.MQTT
{
    public class MqttFactory : IMqttClientFactory, IMqttServerFactory
    {
        public IMqttClient CreateMqttClient()
        {
            return CreateMqttClient(new MqttNetLogger());
        }

        public IMqttClient CreateMqttClient(IMqttNetLogger logger)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            return new MqttClient(new MqttClientAdapterFactory(), logger);
        }

        public IMqttClient CreateMqttClient(IMqttClientAdapterFactory adapterFactory)
        {
            if (adapterFactory == null) throw new ArgumentNullException(nameof(adapterFactory));

            return new MqttClient(adapterFactory, new MqttNetLogger());
        }

        public IMqttClient CreateMqttClient(IMqttNetLogger logger, IMqttClientAdapterFactory adapterFactory)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            if (adapterFactory == null) throw new ArgumentNullException(nameof(adapterFactory));

            return new MqttClient(adapterFactory, logger);
        }

        public IMqttServer CreateMqttServer()
        {
            var logger = new MqttNetLogger();
            return CreateMqttServer(logger);
        }

        public IMqttServer CreateMqttServer(IMqttNetLogger logger)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            return CreateMqttServer(new List<IMqttServerAdapter> { new MqttTcpServerAdapter(logger.CreateChildLogger()) }, logger);
        }

        public IMqttServer CreateMqttServer(IEnumerable<IMqttServerAdapter> adapters, IMqttNetLogger logger)
        {
            if (adapters == null) throw new ArgumentNullException(nameof(adapters));
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            return new MqttServer(adapters, logger.CreateChildLogger());
        }
    }
}
