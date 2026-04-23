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
*唯一标识：0d5d734d-4f08-479f-b1aa-5b541304e465
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MQTT诊断日志类
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MQTT诊断日志类
*
*****************************************************************************/
using System;

namespace SAEA.MQTT.Diagnostics
{
    public static class MqttNetGlobalLogger
    {
        public static event EventHandler<MqttNetLogMessagePublishedEventArgs> LogMessagePublished;

        public static bool HasListeners => LogMessagePublished != null;

        public static void Publish(MqttNetLogMessage logMessage)
        {
            if (logMessage == null) throw new ArgumentNullException(nameof(logMessage));

            LogMessagePublished?.Invoke(null, new MqttNetLogMessagePublishedEventArgs(logMessage));
        }
    }
}
