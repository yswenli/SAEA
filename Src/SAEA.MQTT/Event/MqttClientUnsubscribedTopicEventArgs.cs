/****************************************************************************
*项目名称：SAEA.MQTT.Event
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Event
*类 名 称：MqttClientUnsubscribedTopicEventArgs
*版 本 号： V3.6.2.2
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 10:27:22
*描述：
*=====================================================================
*修改时间：2019/1/15 10:27:22
*修 改 人： yswenli
*版 本 号： V3.6.2.2
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.MQTT.Event
{
    public class MqttClientUnsubscribedTopicEventArgs : EventArgs
    {
        public MqttClientUnsubscribedTopicEventArgs(string clientId, string topicFilter)
        {
            ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            TopicFilter = topicFilter ?? throw new ArgumentNullException(nameof(topicFilter));
        }

        public string ClientId { get; }

        public string TopicFilter { get; }
    }
}
