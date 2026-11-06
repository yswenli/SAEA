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
*文件名： IMqttNetScopedLogger
*版本号： v26.4.23.1
*唯一标识：59c8a012-9df8-42cf-8798-34008e8b7d31
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttNetScopedLogger类
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
    /// 绑定固定日志来源的作用域日志记录器抽象
    /// </summary>
    public interface IMqttNetScopedLogger
    {
        /// <summary>
        /// 在当前作用域基础上派生出新的作用域日志记录器
        /// </summary>
        /// <param name="source">新作用域使用的日志来源标识，不能为 null</param>
        /// <returns>使用新来源的作用域日志记录器</returns>
        IMqttNetScopedLogger CreateScopedLogger(string source);

        /// <summary>
        /// 以当前作用域的来源发布日志消息
        /// </summary>
        /// <param name="logLevel">日志级别</param>
        /// <param name="message">消息模板，含 {0} 等占位符</param>
        /// <param name="parameters">格式化消息模板的参数数组，不能为 null</param>
        /// <param name="exception">关联异常，无异常时为 null</param>
        void Publish(MqttNetLogLevel logLevel, string message, object[] parameters, Exception exception);
    }
}
