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
*命名空间：SAEA.MQTT.Diagnostics
*文件名： MqttNetScopedLogger
*版本号： v26.4.23.1
*唯一标识：d8821680-2c1b-409c-a841-082d5ab6d5fb
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttNetScopedLogger类
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MqttNetScopedLogger类
*
*****************************************************************************/
using System;

namespace SAEA.MQTT.Diagnostics
{
    public sealed class MqttNetScopedLogger : IMqttNetScopedLogger
    {
        readonly IMqttNetLogger _logger;
        readonly string _source;

        public MqttNetScopedLogger(IMqttNetLogger logger, string source)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public IMqttNetScopedLogger CreateScopedLogger(string source)
        {
            return new MqttNetScopedLogger(_logger, source);
        }

        public void Publish(MqttNetLogLevel logLevel, string message, object[] parameters, Exception exception)
        {
            _logger.Publish(logLevel, _source, message, parameters, exception);
        }
    }
}