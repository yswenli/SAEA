/****************************************************************************
*项目名称：SAEA.MQTT.Interface
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Interface
*类 名 称：IMqttManagedClient
*版 本 号： v4.5.1.2
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/14 19:50:26
*描述：
*=====================================================================
*修改时间：2019/1/14 19:50:26
*修 改 人： yswenli
*版 本 号： v4.5.1.2
*描    述：
*****************************************************************************/
using SAEA.MQTT.Event;
using SAEA.MQTT.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAEA.MQTT.Interface
{
    public interface IMqttManagedClient : IMessageReceiver, IMessagePublisher, IDisposable
    {
        bool IsStarted { get; }
        bool IsConnected { get; }
        int PendingApplicationMessagesCount { get; }
        IMqttManagedClientOptions Options { get; }

        event EventHandler<MqttClientConnectedEventArgs> Connected;
        event EventHandler<MqttClientDisconnectedEventArgs> Disconnected;

        event EventHandler<MqttMessageProcessedEventArgs> ApplicationMessageProcessed;
        event EventHandler<MqttMessageSkippedEventArgs> ApplicationMessageSkipped;

        event EventHandler<MqttManagedProcessFailedEventArgs> ConnectingFailed;
        event EventHandler<MqttManagedProcessFailedEventArgs> SynchronizingSubscriptionsFailed;

        Task StartAsync(IMqttManagedClientOptions options);
        Task StopAsync();

        Task SubscribeAsync(IEnumerable<TopicFilter> topicFilters);
        Task UnsubscribeAsync(IEnumerable<string> topics);

        Task PublishAsync(MqttManagedMessage applicationMessages);
    }
}
