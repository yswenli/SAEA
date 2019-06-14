/****************************************************************************
*项目名称：SAEA.MQTT.Event
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Event
*类 名 称：MqttMessageSkippedEventArgs
*版 本 号： v4.5.6.7
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/14 19:57:10
*描述：
*=====================================================================
*修改时间：2019/1/14 19:57:10
*修 改 人： yswenli
*版 本 号： v4.5.6.7
*描    述：
*****************************************************************************/
using SAEA.MQTT.Model;
using System;

namespace SAEA.MQTT.Event
{
    public class MqttMessageSkippedEventArgs : EventArgs
    {
        public MqttMessageSkippedEventArgs(MqttManagedMessage applicationMessage)
        {
            ApplicationMessage = applicationMessage ?? throw new ArgumentNullException(nameof(applicationMessage));
        }

        public MqttManagedMessage ApplicationMessage { get; }
    }
}
