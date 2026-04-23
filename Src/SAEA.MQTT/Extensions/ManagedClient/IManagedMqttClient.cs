/****************************************************************************
 * 
   ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MQTT.Extensions.ManagedClient
*文件名： IManagedMqttClient
*版本号： v26.4.23.1
*唯一标识：a782fec7-3f4c-4cfd-99bb-5a5ef85da531
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using SAEA.MQTT.Client.Connecting;
using SAEA.MQTT.Client.Disconnecting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SAEA.MQTT.Client;

namespace SAEA.MQTT.Extensions.ManagedClient
{
    public interface IManagedMqttClient : IApplicationMessageReceiver, IApplicationMessagePublisher, IDisposable
    {
        /// <summary>
        /// Gets the internally used MQTT client.
        /// This property should be used with caution because manipulating the internal client might break the managed client.
        /// </summary>
        IMqttClient InternalClient { get; }

        bool IsStarted { get; }
        
        bool IsConnected { get; }
        
        int PendingApplicationMessagesCount { get; }
        
        IManagedMqttClientOptions Options { get; }
        
        IMqttClientConnectedHandler ConnectedHandler { get; set; }
        
        IMqttClientDisconnectedHandler DisconnectedHandler { get; set; }

        IApplicationMessageProcessedHandler ApplicationMessageProcessedHandler { get; set; }
        
        IApplicationMessageSkippedHandler ApplicationMessageSkippedHandler { get; set; }

        IConnectingFailedHandler ConnectingFailedHandler { get; set; }
        
        ISynchronizingSubscriptionsFailedHandler SynchronizingSubscriptionsFailedHandler { get; set; }

        Task StartAsync(IManagedMqttClientOptions options);
        
        Task StopAsync();
        
        Task PingAsync(CancellationToken cancellationToken);

        Task SubscribeAsync(IEnumerable<MqttTopicFilter> topicFilters);
        
        Task UnsubscribeAsync(IEnumerable<string> topics);

        Task PublishAsync(ManagedMqttApplicationMessage applicationMessages);
    }
}