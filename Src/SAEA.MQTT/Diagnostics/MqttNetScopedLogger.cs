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
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;

namespace SAEA.MQTT.Diagnostics
{
    /// <summary>
    /// 绑定固定来源的日志记录器，所有日志都带上该来源
    /// </summary>
    public sealed class MqttNetScopedLogger : IMqttNetScopedLogger
    {
        readonly IMqttNetLogger _logger;
        readonly string _source;

        /// <summary>
        /// 创建作用域日志记录器
        /// </summary>
        /// <param name="logger">底层日志记录器，不能为 null</param>
        /// <param name="source">该作用域固定使用的日志来源，不能为 null</param>
        public MqttNetScopedLogger(IMqttNetLogger logger, string source)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        /// <summary>
        /// 在当前作用域基础上派生出新的作用域日志记录器，两者共用底层记录器
        /// </summary>
        /// <param name="source">新作用域使用的日志来源</param>
        /// <returns>使用新来源的作用域日志记录器</returns>
        public IMqttNetScopedLogger CreateScopedLogger(string source)
        {
            return new MqttNetScopedLogger(_logger, source);
        }

        /// <summary>
        /// 以当前作用域的来源向底层记录器发布日志消息
        /// </summary>
        /// <param name="logLevel">日志级别</param>
        /// <param name="message">消息模板，含 {0} 等占位符</param>
        /// <param name="parameters">格式化消息模板的参数数组，不能为 null</param>
        /// <param name="exception">关联异常，无异常时为 null</param>
        public void Publish(MqttNetLogLevel logLevel, string message, object[] parameters, Exception exception)
        {
            _logger.Publish(logLevel, _source, message, parameters, exception);
        }
    }
}