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
*文件名： IMqttServer
*版本号： v26.4.23.1
*唯一标识：150c66f7-32b9-4f1b-8868-9def26235545
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttServer服务端类
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：IMqttServer服务端类
*
*****************************************************************************/
using System;
using SAEA.MQTT.Server.Status;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    public interface IMqttServer : IApplicationMessageReceiver, IApplicationMessagePublisher, IDisposable
    {
        bool IsStarted { get; }
        IMqttServerStartedHandler StartedHandler { get; set; }
        IMqttServerStoppedHandler StoppedHandler { get; set; }

        IMqttServerClientConnectedHandler ClientConnectedHandler { get; set; }
        IMqttServerClientDisconnectedHandler ClientDisconnectedHandler { get; set; }
        IMqttServerClientSubscribedTopicHandler ClientSubscribedTopicHandler { get; set; }
        IMqttServerClientUnsubscribedTopicHandler ClientUnsubscribedTopicHandler { get; set; }

        IMqttServerOptions Options { get; }

        Task<IList<IMqttClientStatus>> GetClientStatusAsync();
        Task<IList<IMqttSessionStatus>> GetSessionStatusAsync();

        Task<IList<MqttApplicationMessage>> GetRetainedApplicationMessagesAsync();
        Task ClearRetainedApplicationMessagesAsync();

        Task SubscribeAsync(string clientId, ICollection<MqttTopicFilter> topicFilters);
        Task UnsubscribeAsync(string clientId, ICollection<string> topicFilters);

        Task StartAsync(IMqttServerOptions options);
        Task StopAsync();
    }
}