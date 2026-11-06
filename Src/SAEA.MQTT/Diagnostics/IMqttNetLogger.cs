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
*文件名： IMqttNetLogger
*版本号： v26.4.23.1
*唯一标识：acbeea03-0382-47ce-9a73-e1926239ec2b
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttNetLogger类
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
    /// MQTTNet 日志记录器抽象，负责组装并分发日志消息
    /// </summary>
    public interface IMqttNetLogger
    {
        /// <summary>
        /// 本记录器发布日志消息时触发
        /// </summary>
        event EventHandler<MqttNetLogMessagePublishedEventArgs> LogMessagePublished;

        /// <summary>
        /// 创建绑定指定来源的作用域日志记录器
        /// </summary>
        /// <param name="source">日志来源标识，通常为组件名，不能为 null</param>
        /// <returns>固定使用该来源的作用域日志记录器</returns>
        IMqttNetScopedLogger CreateScopedLogger(string source);

        /// <summary>
        /// 按指定级别发布日志消息
        /// </summary>
        /// <param name="logLevel">日志级别</param>
        /// <param name="source">日志来源标识，通常为组件名，可为 null</param>
        /// <param name="message">消息模板，含 {0} 等占位符，为 null 时按空字符串处理</param>
        /// <param name="parameters">格式化消息模板的参数数组，不能为 null</param>
        /// <param name="exception">关联异常，无异常时为 null</param>
        void Publish(MqttNetLogLevel logLevel, string source, string message, object[] parameters, Exception exception);
    }
}
