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
*文件名： MqttNetGlobalLogger
*版本号： v26.4.23.1
*唯一标识：771993fc-c32e-48ef-b2a6-7f29680d49cb
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttNetGlobalLogger类
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
    /// 进程级全局日志出口，供所有日志记录器汇聚日志消息
    /// </summary>
    public static class MqttNetGlobalLogger
    {
        /// <summary>
        /// 任一记录器向全局出口发布日志消息时触发
        /// </summary>
        public static event EventHandler<MqttNetLogMessagePublishedEventArgs> LogMessagePublished;

        /// <summary>
        /// 是否存在全局日志监听器
        /// </summary>
        public static bool HasListeners => LogMessagePublished != null;

        /// <summary>
        /// 向全局出口发布一条已组装的日志消息
        /// </summary>
        /// <param name="logMessage">待发布的日志消息，不能为 null</param>
        public static void Publish(MqttNetLogMessage logMessage)
        {
            if (logMessage == null) throw new ArgumentNullException(nameof(logMessage));

            LogMessagePublished?.Invoke(null, new MqttNetLogMessagePublishedEventArgs(logMessage));
        }
    }
}
