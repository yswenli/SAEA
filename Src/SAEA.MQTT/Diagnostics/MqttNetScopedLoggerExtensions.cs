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
*文件名： MqttNetScopedLoggerExtensions
*版本号： v26.4.23.1
*唯一标识：2be504ca-7bfb-41ae-857d-622c3c6e5b59
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttNetScopedLoggerExtensions接口
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
    /// 作用域日志记录器的便捷扩展方法
    /// </summary>
    public static class MqttNetScopedLoggerExtensions
    {
        /// <summary>
        /// 以 Verbose 级别发布日志消息
        /// </summary>
        /// <param name="logger">目标作用域日志记录器</param>
        /// <param name="message">消息模板，含 {0} 等占位符</param>
        /// <param name="parameters">格式化消息模板的参数数组，省略时为空数组</param>
        public static void Verbose(this IMqttNetScopedLogger logger, string message, params object[] parameters)
        {
            logger.Publish(MqttNetLogLevel.Verbose, message, parameters, null);
        }

        /// <summary>
        /// 以 Info 级别发布日志消息
        /// </summary>
        /// <param name="logger">目标作用域日志记录器</param>
        /// <param name="message">消息模板，含 {0} 等占位符</param>
        /// <param name="parameters">格式化消息模板的参数数组，省略时为空数组</param>
        public static void Info(this IMqttNetScopedLogger logger, string message, params object[] parameters)
        {
            logger.Publish(MqttNetLogLevel.Info, message, parameters, null);
        }

        /// <summary>
        /// 以 Warning 级别发布带异常的日志消息
        /// </summary>
        /// <param name="logger">目标作用域日志记录器</param>
        /// <param name="exception">关联异常，可为 null</param>
        /// <param name="message">消息模板，含 {0} 等占位符</param>
        /// <param name="parameters">格式化消息模板的参数数组，省略时为空数组</param>
        public static void Warning(this IMqttNetScopedLogger logger, Exception exception, string message, params object[] parameters)
        {
            logger.Publish(MqttNetLogLevel.Warning, message, parameters, exception);
        }

        /// <summary>
        /// 以 Error 级别发布带异常的日志消息
        /// </summary>
        /// <param name="logger">目标作用域日志记录器</param>
        /// <param name="exception">关联异常，可为 null</param>
        /// <param name="message">消息模板，含 {0} 等占位符</param>
        /// <param name="parameters">格式化消息模板的参数数组，省略时为空数组</param>
        public static void Error(this IMqttNetScopedLogger logger, Exception exception, string message, params object[] parameters)
        {
            logger.Publish(MqttNetLogLevel.Error, message, parameters, exception);
        }
    }
}
