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
*命名空间：SAEA.MQTT.Server
*文件名： MqttServerOptions
*版本号： v26.4.23.1
*唯一标识：cd7096ec-05c6-4900-8ce6-30a2688b06e9
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttServerOptions接口
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MqttServerOptions接口
*
*****************************************************************************/
using System;

namespace SAEA.MQTT.Server
{
    public class MqttServerOptions : IMqttServerOptions
    {
        public MqttServerTcpEndpointOptions DefaultEndpointOptions { get; } = new MqttServerTcpEndpointOptions();

        public MqttServerTlsTcpEndpointOptions TlsEndpointOptions { get; } = new MqttServerTlsTcpEndpointOptions();

        public string ClientId { get; set; }

        public bool EnablePersistentSessions { get; set; }

        public int MaxPendingMessagesPerClient { get; set; } = 250;

        public MqttPendingMessagesOverflowStrategy PendingMessagesOverflowStrategy { get; set; } = MqttPendingMessagesOverflowStrategy.DropOldestQueuedMessage;

        public TimeSpan DefaultCommunicationTimeout { get; set; } = TimeSpan.FromSeconds(15);

        public TimeSpan KeepAliveMonitorInterval { get; set; } = TimeSpan.FromMilliseconds(500);

        public IMqttServerConnectionValidator ConnectionValidator { get; set; }

        public IMqttServerApplicationMessageInterceptor ApplicationMessageInterceptor { get; set; }

        public IMqttServerClientMessageQueueInterceptor ClientMessageQueueInterceptor { get; set; }

        public IMqttServerSubscriptionInterceptor SubscriptionInterceptor { get; set; }

        public IMqttServerUnsubscriptionInterceptor UnsubscriptionInterceptor { get; set; }

        public IMqttServerStorage Storage { get; set; }

        public IMqttRetainedMessagesManager RetainedMessagesManager { get; set; } = new MqttRetainedMessagesManager();

        public IMqttServerApplicationMessageInterceptor UndeliveredMessageInterceptor { get; set; }

        public IMqttServerClientDisconnectedHandler ClientDisconnectedInterceptor { get; set; }
    }
}
