/****************************************************************************
*项目名称：SAEA.MQTT.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Model
*类 名 称：MqttManagedClientOptions
*版 本 号： v4.1.2.5
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 17:06:41
*描述：
*=====================================================================
*修改时间：2019/1/15 17:06:41
*修 改 人： yswenli
*版 本 号： v4.1.2.5
*描    述：
*****************************************************************************/
using SAEA.MQTT.Interface;
using System;

namespace SAEA.MQTT.Model
{
    public class MqttManagedClientOptions : IMqttManagedClientOptions
    {
        public IMqttClientOptions ClientOptions { get; set; }

        public TimeSpan AutoReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);

        public TimeSpan ConnectionCheckInterval { get; set; } = TimeSpan.FromSeconds(1);

        public IMqttManagedClientStorage Storage { get; set; }

        public int MaxPendingMessages { get; set; } = int.MaxValue;

        public MqttPendingMessagesOverflowStrategy PendingMessagesOverflowStrategy { get; set; } = MqttPendingMessagesOverflowStrategy.DropNewMessage;
    }
}
