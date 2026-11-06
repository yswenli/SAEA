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
*文件名： IMqttServerOptions
*版本号： v26.4.23.1
*唯一标识：6672762a-2e49-4e59-9f75-fdc8851d922c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttServerOptions接口
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
    /// MQTT 服务端配置契约，聚合端点、会话策略、拦截器、能力声明与存储
    /// </summary>
    public interface IMqttServerOptions
    {
        /// <summary>
        /// 服务端自身的客户端标识，用于填充服务端发布消息的来源
        /// </summary>
        string ClientId { get; set; }

        /// <summary>
        /// 是否保留断开客户端的会话，关闭时每次连接都按清理会话处理
        /// </summary>
        bool EnablePersistentSessions { get; }

        /// <summary>
        /// 单客户端待投递消息队列上限，默认 250
        /// </summary>
        int MaxPendingMessagesPerClient { get; }
        /// <summary>
        /// 队列达到上限时的丢弃策略，默认丢弃最旧的排队消息
        /// </summary>
        MqttPendingMessagesOverflowStrategy PendingMessagesOverflowStrategy { get; }

        /// <summary>
        /// 单次网络读写的默认超时，默认 15 秒
        /// </summary>
        TimeSpan DefaultCommunicationTimeout { get; }
        /// <summary>
        /// 保活检测的扫描间隔，默认 500 毫秒
        /// </summary>
        TimeSpan KeepAliveMonitorInterval { get; }

        /// <summary>
        /// 连接校验器，为 null 时接受所有连接
        /// </summary>
        IMqttServerConnectionValidator ConnectionValidator { get; }
        /// <summary>
        /// 订阅拦截器，为 null 时接受所有订阅
        /// </summary>
        IMqttServerSubscriptionInterceptor SubscriptionInterceptor { get; }
        /// <summary>
        /// 取消订阅拦截器，为 null 时接受所有取消订阅
        /// </summary>
        IMqttServerUnsubscriptionInterceptor UnsubscriptionInterceptor { get; }
        /// <summary>
        /// 应用消息发布拦截器，为 null 时消息直接分发
        /// </summary>
        IMqttServerApplicationMessageInterceptor ApplicationMessageInterceptor { get; }
        /// <summary>
        /// 客户端消息队列入队拦截器，为 null 时消息直接入队
        /// </summary>
        IMqttServerClientMessageQueueInterceptor ClientMessageQueueInterceptor { get; }

        /// <summary>
        /// 服务端支持的能力声明，用于 MQTT 5.0 连接阶段的能力协商
        /// </summary>
        MqttServerCapabilities Capabilities { get; }

        /// <summary>
        /// 明文 TCP 端点的监听配置
        /// </summary>
        MqttServerTcpEndpointOptions DefaultEndpointOptions { get; }
        /// <summary>
        /// TLS 端点的监听配置，未启用时不影响明文端点
        /// </summary>
        MqttServerTlsTcpEndpointOptions TlsEndpointOptions { get; }

        /// <summary>
        /// 保留消息的持久化存储，为 null 时保留消息仅存于内存
        /// </summary>
        IMqttServerStorage Storage { get; }

        /// <summary>
        /// 保留消息管理器，启动时不可为 null，默认为内存实现
        /// </summary>
        IMqttRetainedMessagesManager RetainedMessagesManager { get; }

        /// <summary>
        /// 消息无任何订阅者命中时的回调拦截器，用于记录或旁路投递
        /// </summary>
        IMqttServerApplicationMessageInterceptor UndeliveredMessageInterceptor { get; set; }

        /// <summary>
        /// 客户端断开连接时的回调处理器
        /// </summary>
        IMqttServerClientDisconnectedHandler ClientDisconnectedInterceptor { get; set; }
    }
}