/****************************************************************************
*项目名称：SAEA.MQTT.Common.Log
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Common.Log
*类 名 称：MqttNetChildLogger
*版 本 号： v4.1.2.5
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 10:33:37
*描述：
*=====================================================================
*修改时间：2019/1/15 10:33:37
*修 改 人： yswenli
*版 本 号： v4.1.2.5
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.MQTT.Common.Log
{
    public class MqttNetChildLogger : IMqttNetChildLogger
    {
        private readonly IMqttNetLogger _logger;
        private readonly string _source;

        public MqttNetChildLogger(IMqttNetLogger logger, string source)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _source = source;
        }

        public IMqttNetChildLogger CreateChildLogger(string source)
        {
            string childSource;
            if (!string.IsNullOrEmpty(_source))
            {
                childSource = _source + "." + source;
            }
            else
            {
                childSource = source;
            }

            return new MqttNetChildLogger(_logger, childSource);
        }

        public void Verbose(string message, params object[] parameters)
        {
            _logger.Publish(MqttNetLogLevel.Verbose, _source, message, parameters, null);
        }

        public void Info(string message, params object[] parameters)
        {
            _logger.Publish(MqttNetLogLevel.Info, _source, message, parameters, null);
        }

        public void Warning(Exception exception, string message, params object[] parameters)
        {
            _logger.Publish(MqttNetLogLevel.Warning, _source, message, parameters, exception);
        }

        public void Error(Exception exception, string message, params object[] parameters)
        {
            _logger.Publish(MqttNetLogLevel.Error, _source, message, parameters, exception);
        }
    }
}
