/****************************************************************************
*项目名称：SAEA.MQTT.Common.Log
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Common.Log
*类 名 称：IMqttNetChildLogger
*版 本 号： v4.2.3.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 10:17:11
*描述：
*=====================================================================
*修改时间：2019/1/15 10:17:11
*修 改 人： yswenli
*版 本 号： v4.2.3.1
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.MQTT.Common.Log
{
    public interface IMqttNetChildLogger
    {
        IMqttNetChildLogger CreateChildLogger(string source = null);

        void Verbose(string message, params object[] parameters);

        void Info(string message, params object[] parameters);

        void Warning(Exception exception, string message, params object[] parameters);

        void Error(Exception exception, string message, params object[] parameters);
    }
}
