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
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 服务端运行参数，涵盖端点监听、会话保持、各类拦截器与存储等可配置项
    /// </summary>
    public class MqttServerOptions : IMqttServerOptions
    {
        /// <summary>
        /// 服务端在 CONNACK 中通告的能力与限制
        /// </summary>
        public MqttServerCapabilities Capabilities { get; } = new MqttServerCapabilities();

        /// <summary>
        /// 明文 TCP 端点配置
        /// </summary>
        public MqttServerTcpEndpointOptions DefaultEndpointOptions { get; } = new MqttServerTcpEndpointOptions();

        /// <summary>
        /// TLS 端点配置
        /// </summary>
        public MqttServerTlsTcpEndpointOptions TlsEndpointOptions { get; } = new MqttServerTlsTcpEndpointOptions();

        /// <summary>
        /// 服务端自身发布消息时使用的客户端标识
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// 是否在客户端断开后保留会话，以便重连时续传未确认消息
        /// </summary>
        public bool EnablePersistentSessions { get; set; }

        /// <summary>
        /// 每个客户端允许积压的待发消息条数上限，默认 250
        /// </summary>
        public int MaxPendingMessagesPerClient { get; set; } = 250;

        /// <summary>
        /// 待发消息超过上限时采用的丢弃策略，默认丢弃队列中最旧的消息
        /// </summary>
        public MqttPendingMessagesOverflowStrategy PendingMessagesOverflowStrategy { get; set; } = MqttPendingMessagesOverflowStrategy.DropOldestQueuedMessage;

        /// <summary>
        /// 内部通信的默认超时时间，默认 15 秒
        /// </summary>
        public TimeSpan DefaultCommunicationTimeout { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// 保活检查线程的轮询间隔，默认 500 毫秒
        /// </summary>
        public TimeSpan KeepAliveMonitorInterval { get; set; } = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// 连接校验器，可在客户端连接时拒绝请求
        /// </summary>
        public IMqttServerConnectionValidator ConnectionValidator { get; set; }

        /// <summary>
        /// 应用消息发布拦截器，可修改或丢弃即将转发的消息
        /// </summary>
        public IMqttServerApplicationMessageInterceptor ApplicationMessageInterceptor { get; set; }

        /// <summary>
        /// 客户端消息队列拦截器，可对入队前的出站消息做调整
        /// </summary>
        public IMqttServerClientMessageQueueInterceptor ClientMessageQueueInterceptor { get; set; }

        /// <summary>
        /// 订阅拦截器，可拒绝或调整客户端订阅
        /// </summary>
        public IMqttServerSubscriptionInterceptor SubscriptionInterceptor { get; set; }

        /// <summary>
        /// 取消订阅拦截器，可拒绝或调整客户端取消订阅
        /// </summary>
        public IMqttServerUnsubscriptionInterceptor UnsubscriptionInterceptor { get; set; }

        /// <summary>
        /// 服务端存储，用于持久化会话与 retained 消息，为 null 表示不持久化
        /// </summary>
        public IMqttServerStorage Storage { get; set; }

        /// <summary>
        /// retained 消息管理器，默认使用内置的内存实现
        /// </summary>
        public IMqttRetainedMessagesManager RetainedMessagesManager { get; set; } = new MqttRetainedMessagesManager();

        /// <summary>
        /// 未被投递消息的拦截器，可在客户端离线丢弃消息前做处理
        /// </summary>
        public IMqttServerApplicationMessageInterceptor UndeliveredMessageInterceptor { get; set; }

        /// <summary>
        /// 客户端断开连接时触发的处理器
        /// </summary>
        public IMqttServerClientDisconnectedHandler ClientDisconnectedInterceptor { get; set; }
    }
}
