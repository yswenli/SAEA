/****************************************************************************
*项目名称：SAEA.MQTT.Interface
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Interface
*类 名 称：IMqttServer
*版 本 号： v4.2.1.6
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 10:20:48
*描述：
*=====================================================================
*修改时间：2019/1/15 10:20:48
*修 改 人： yswenli
*版 本 号： v4.2.1.6
*描    述：
*****************************************************************************/
using SAEA.MQTT.Core;
using SAEA.MQTT.Event;
using SAEA.MQTT.Model;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.MQTT.Interface
{
    public interface IMqttServer : IMessageReceiver, IMessagePublisher
    {
        event EventHandler Started;
        event EventHandler Stopped;

        event EventHandler<MqttClientConnectedEventArgs> ClientConnected;
        event EventHandler<MqttClientDisconnectedEventArgs> ClientDisconnected;
        event EventHandler<MqttClientSubscribedTopicEventArgs> ClientSubscribedTopic;
        event EventHandler<MqttClientUnsubscribedTopicEventArgs> ClientUnsubscribedTopic;

        IMqttServerOptions Options { get; }

        IList<IMqttClientSessionStatus> GetClientSessionsStatus();

        IList<MqttMessage> GetRetainedMessages();
        Task ClearRetainedMessagesAsync();

        Task SubscribeAsync(string clientId, IList<TopicFilter> topicFilters);
        Task UnsubscribeAsync(string clientId, IList<string> topicFilters);

        Task StartAsync(IMqttServerOptions options);
        Task StopAsync();
    }
}
