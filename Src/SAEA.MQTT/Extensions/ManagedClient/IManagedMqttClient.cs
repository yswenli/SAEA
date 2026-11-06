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
*唯一标识：2267d891-08e0-4904-b05b-6eafdda01ab7
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IManagedMqttClient接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
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
    /// <summary>
    /// 托管MQTT客户端，在底层客户端之上提供自动重连、待发队列与订阅同步
    /// </summary>
    public interface IManagedMqttClient : IApplicationMessageReceiver, IApplicationMessagePublisher, IDisposable
    {
        /// <summary>
        /// 内部实际使用的MQTT客户端，直接操作可能破坏托管客户端的状态，需谨慎使用
        /// </summary>
        IMqttClient InternalClient { get; }

        /// <summary>
        /// 是否已启动
        /// </summary>
        bool IsStarted { get; }
        
        /// <summary>
        /// 内部客户端当前是否已连接
        /// </summary>
        bool IsConnected { get; }
        
        /// <summary>
        /// 排队等待发送的应用消息数量
        /// </summary>
        int PendingApplicationMessagesCount { get; }
        
        /// <summary>
        /// 当前生效的托管客户端选项，启动前为null
        /// </summary>
        IManagedMqttClientOptions Options { get; }
        
        /// <summary>
        /// 连接成功事件处理器，直接作用于内部客户端
        /// </summary>
        IMqttClientConnectedHandler ConnectedHandler { get; set; }
        
        /// <summary>
        /// 连接断开事件处理器，直接作用于内部客户端
        /// </summary>
        IMqttClientDisconnectedHandler DisconnectedHandler { get; set; }

        /// <summary>
        /// 应用消息处理完成事件处理器
        /// </summary>
        IApplicationMessageProcessedHandler ApplicationMessageProcessedHandler { get; set; }
        
        /// <summary>
        /// 应用消息被跳过事件处理器
        /// </summary>
        IApplicationMessageSkippedHandler ApplicationMessageSkippedHandler { get; set; }

        /// <summary>
        /// 连接失败事件处理器
        /// </summary>
        IConnectingFailedHandler ConnectingFailedHandler { get; set; }
        
        /// <summary>
        /// 订阅同步失败事件处理器
        /// </summary>
        ISynchronizingSubscriptionsFailedHandler SynchronizingSubscriptionsFailedHandler { get; set; }

        /// <summary>
        /// 用指定选项启动托管客户端，并加载已持久化的待发消息
        /// </summary>
        /// <param name="options">托管客户端选项，其 ClientOptions 不能为null</param>
        /// <returns>启动完成的异步任务</returns>
        Task StartAsync(IManagedMqttClientOptions options);
        
        /// <summary>
        /// 停止托管客户端并清空待发队列
        /// </summary>
        /// <returns>停止完成的异步任务</returns>
        Task StopAsync();
        
        /// <summary>
        /// 通过内部客户端发送心跳
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>心跳完成的异步任务</returns>
        Task PingAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 登记订阅，由后台在连接可用时下发并在重连后自动恢复
        /// </summary>
        /// <param name="topicFilters">主题过滤条件集合</param>
        /// <returns>登记完成的任务，不等待服务端确认</returns>
        Task SubscribeAsync(IEnumerable<MqttTopicFilter> topicFilters);
        
        /// <summary>
        /// 登记取消订阅，由后台在连接可用时下发
        /// </summary>
        /// <param name="topics">要取消订阅的主题集合</param>
        /// <returns>登记完成的任务，不等待服务端确认</returns>
        Task UnsubscribeAsync(IEnumerable<string> topics);

        /// <summary>
        /// 将消息放入待发队列，由后台按顺序发送
        /// </summary>
        /// <param name="applicationMessages">带唯一标识的待发消息</param>
        /// <returns>入队完成的异步任务</returns>
        Task PublishAsync(ManagedMqttApplicationMessage applicationMessages);
    }
}