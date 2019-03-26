/****************************************************************************
*项目名称：SAEA.MQTT.Core.Implementations
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Core.Implementations
*类 名 称：MqttServerEventDispatcher
*版 本 号： v4.3.2.5
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 15:45:23
*描述：
*=====================================================================
*修改时间：2019/1/15 15:45:23
*修 改 人： yswenli
*版 本 号： v4.3.2.5
*描    述：
*****************************************************************************/
using SAEA.MQTT.Event;
using SAEA.MQTT.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.MQTT.Core.Implementations
{
    public class MqttServerEventDispatcher
    {
        public event EventHandler<MqttClientSubscribedTopicEventArgs> ClientSubscribedTopic;

        public event EventHandler<MqttClientUnsubscribedTopicEventArgs> ClientUnsubscribedTopic;

        public event EventHandler<MqttClientConnectedEventArgs> ClientConnected;

        public event EventHandler<MqttClientDisconnectedEventArgs> ClientDisconnected;

        public event EventHandler<MqttMessageReceivedEventArgs> ApplicationMessageReceived;

        public void OnClientSubscribedTopic(string clientId, TopicFilter topicFilter)
        {
            ClientSubscribedTopic?.Invoke(this, new MqttClientSubscribedTopicEventArgs(clientId, topicFilter));
        }

        public void OnClientUnsubscribedTopic(string clientId, string topicFilter)
        {
            ClientUnsubscribedTopic?.Invoke(this, new MqttClientUnsubscribedTopicEventArgs(clientId, topicFilter));
        }

        public void OnClientDisconnected(string clientId, bool wasCleanDisconnect)
        {
            ClientDisconnected?.Invoke(this, new MqttClientDisconnectedEventArgs(clientId, wasCleanDisconnect));
        }

        public void OnMessageReceived(string senderClientId, MqttMessage applicationMessage)
        {
            ApplicationMessageReceived?.Invoke(this, new MqttMessageReceivedEventArgs(senderClientId, applicationMessage));
        }

        public void OnClientConnected(string clientId)
        {
            ClientConnected?.Invoke(this, new MqttClientConnectedEventArgs(clientId));
        }
    }
}
