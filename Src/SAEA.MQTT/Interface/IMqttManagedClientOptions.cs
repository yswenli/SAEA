/****************************************************************************
*项目名称：SAEA.MQTT.Interface
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Interface
*类 名 称：IMqttManagedClientOptions
*版 本 号： v4.3.1.2
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/14 19:53:36
*描述：
*=====================================================================
*修改时间：2019/1/14 19:53:36
*修 改 人： yswenli
*版 本 号： v4.3.1.2
*描    述：
*****************************************************************************/
using SAEA.MQTT.Model;
using System;

namespace SAEA.MQTT.Interface
{
    public interface IMqttManagedClientOptions
    {
        IMqttClientOptions ClientOptions { get; }

        TimeSpan AutoReconnectDelay { get; }

        TimeSpan ConnectionCheckInterval { get; }

        IMqttManagedClientStorage Storage { get; }

        int MaxPendingMessages { get; }

        MqttPendingMessagesOverflowStrategy PendingMessagesOverflowStrategy { get; }
    }
}
