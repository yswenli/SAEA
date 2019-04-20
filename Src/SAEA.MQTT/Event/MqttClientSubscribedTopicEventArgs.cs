/****************************************************************************
*项目名称：SAEA.MQTT.Event
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Event
*类 名 称：MqttClientSubscribedTopicEventArgs
*版 本 号： v4.3.3.7
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 10:26:48
*描述：
*=====================================================================
*修改时间：2019/1/15 10:26:48
*修 改 人： yswenli
*版 本 号： v4.3.3.7
*描    述：
*****************************************************************************/
using SAEA.MQTT.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.MQTT.Event
{
    public class MqttClientSubscribedTopicEventArgs : EventArgs
    {
        public MqttClientSubscribedTopicEventArgs(string clientId, TopicFilter topicFilter)
        {
            ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            TopicFilter = topicFilter ?? throw new ArgumentNullException(nameof(topicFilter));
        }

        public string ClientId { get; }

        public TopicFilter TopicFilter { get; }
    }
}
