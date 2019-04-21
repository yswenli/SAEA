/****************************************************************************
*项目名称：SAEA.MQTT.Common.Log
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Common.Log
*类 名 称：MqttNetGlobalLogger
*版 本 号： v4.5.1.2
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 10:34:07
*描述：
*=====================================================================
*修改时间：2019/1/15 10:34:07
*修 改 人： yswenli
*版 本 号： v4.5.1.2
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.MQTT.Common.Log
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
