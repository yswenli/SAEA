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
*文件名： MqttServerExtensions
*版本号： v26.4.23.1
*唯一标识：b68ff1d1-726a-47f0-afc6-892d84410fd7
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttServerExtensions接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.MQTT.Client.Publishing;
using SAEA.MQTT.Client.Receiving;
using SAEA.MQTT.Protocol;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 为 MQTT 服务端提供注册事件处理器与内部订阅、发布的链式扩展方法
    /// </summary>
    public static class MqttServerExtensions
    {
        /// <summary>
        /// 用异步委托注册客户端连接成功处理器
        /// </summary>
        /// <param name="server">目标服务端，不能为 null</param>
        /// <param name="handler">连接成功回调，为 null 时清除已有处理器</param>
        /// <returns>同一个服务端实例，便于链式调用</returns>
        public static IMqttServer UseClientConnectedHandler(this IMqttServer server, Func<MqttServerClientConnectedEventArgs, Task> handler)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            if (handler == null)
            {
                return server.UseClientConnectedHandler((IMqttServerClientConnectedHandler)null);
            }

            return server.UseClientConnectedHandler(new MqttServerClientConnectedHandlerDelegate(handler));
        }

        /// <summary>
        /// 用同步委托注册客户端连接成功处理器
        /// </summary>
        /// <param name="server">目标服务端，不能为 null</param>
        /// <param name="handler">连接成功回调，为 null 时清除已有处理器</param>
        /// <returns>同一个服务端实例，便于链式调用</returns>
        public static IMqttServer UseClientConnectedHandler(this IMqttServer server, Action<MqttServerClientConnectedEventArgs> handler)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            if (handler == null)
            {
                return server.UseClientConnectedHandler((IMqttServerClientConnectedHandler)null);
            }

            return server.UseClientConnectedHandler(new MqttServerClientConnectedHandlerDelegate(handler));
        }

        /// <summary>
        /// 设置客户端连接成功处理器
        /// </summary>
        /// <param name="server">目标服务端，不能为 null</param>
        /// <param name="handler">处理器实例，为 null 时清除已有处理器</param>
        /// <returns>同一个服务端实例，便于链式调用</returns>
        public static IMqttServer UseClientConnectedHandler(this IMqttServer server, IMqttServerClientConnectedHandler handler)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            server.ClientConnectedHandler = handler;
            return server;
        }

        /// <summary>
        /// 用异步委托注册客户端断开连接处理器
        /// </summary>
        /// <param name="server">目标服务端，不能为 null</param>
        /// <param name="handler">断开连接回调，为 null 时清除已有处理器</param>
        /// <returns>同一个服务端实例，便于链式调用</returns>
        public static IMqttServer UseClientDisconnectedHandler(this IMqttServer server, Func<MqttServerClientDisconnectedEventArgs, Task> handler)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            if (handler == null)
            {
                return server.UseClientDisconnectedHandler((IMqttServerClientDisconnectedHandler)null);
            }

            return server.UseClientDisconnectedHandler(new MqttServerClientDisconnectedHandlerDelegate(handler));
        }

        /// <summary>
        /// 用同步委托注册客户端断开连接处理器
        /// </summary>
        /// <param name="server">目标服务端，不能为 null</param>
        /// <param name="handler">断开连接回调，为 null 时清除已有处理器</param>
        /// <returns>同一个服务端实例，便于链式调用</returns>
        public static IMqttServer UseClientDisconnectedHandler(this IMqttServer server, Action<MqttServerClientDisconnectedEventArgs> handler)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            if (handler == null)
            {
                return server.UseClientDisconnectedHandler((IMqttServerClientDisconnectedHandler)null);
            }

            return server.UseClientDisconnectedHandler(new MqttServerClientDisconnectedHandlerDelegate(handler));
        }

        /// <summary>
        /// 设置客户端断开连接处理器
        /// </summary>
        /// <param name="server">目标服务端，不能为 null</param>
        /// <param name="handler">处理器实例，为 null 时清除已有处理器</param>
        /// <returns>同一个服务端实例，便于链式调用</returns>
        public static IMqttServer UseClientDisconnectedHandler(this IMqttServer server, IMqttServerClientDisconnectedHandler handler)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            server.ClientDisconnectedHandler = handler;
            return server;
        }

        /// <summary>
        /// 用异步委托注册服务端收到应用消息的处理器
        /// </summary>
        /// <param name="server">目标服务端，不能为 null</param>
        /// <param name="handler">消息接收回调，为 null 时清除已有处理器</param>
        /// <returns>同一个服务端实例，便于链式调用</returns>
        public static IMqttServer UseApplicationMessageReceivedHandler(this IMqttServer server, Func<MqttApplicationMessageReceivedEventArgs, Task> handler)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            if (handler == null)
            {
                return server.UseApplicationMessageReceivedHandler((IMqttApplicationMessageReceivedHandler)null);
            }

            return server.UseApplicationMessageReceivedHandler(new MqttApplicationMessageReceivedHandlerDelegate(handler));
        }

        /// <summary>
        /// 用同步委托注册服务端收到应用消息的处理器
        /// </summary>
        /// <param name="server">目标服务端，不能为 null</param>
        /// <param name="handler">消息接收回调，为 null 时清除已有处理器</param>
        /// <returns>同一个服务端实例，便于链式调用</returns>
        public static IMqttServer UseApplicationMessageReceivedHandler(this IMqttServer server, Action<MqttApplicationMessageReceivedEventArgs> handler)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            if (handler == null)
            {
                return server.UseApplicationMessageReceivedHandler((IMqttApplicationMessageReceivedHandler)null);
            }

            return server.UseApplicationMessageReceivedHandler(new MqttApplicationMessageReceivedHandlerDelegate(handler));
        }

        /// <summary>
        /// 设置服务端收到应用消息的处理器
        /// </summary>
        /// <param name="server">目标服务端，不能为 null</param>
        /// <param name="handler">处理器实例，为 null 时清除已有处理器</param>
        /// <returns>同一个服务端实例，便于链式调用</returns>
        public static IMqttServer UseApplicationMessageReceivedHandler(this IMqttServer server, IMqttApplicationMessageReceivedHandler handler)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            server.ApplicationMessageReceivedHandler = handler;
            return server;
        }

        /// <summary>
        /// 以给定主题过滤器在服务端为该客户端建立订阅
        /// </summary>
        /// <param name="server">目标服务端，不能为 null</param>
        /// <param name="clientId">订阅方客户端标识，不能为 null</param>
        /// <param name="topicFilters">要订阅的主题过滤器，不能为 null 或空数组</param>
        /// <returns>订阅完成的任务</returns>
        public static Task SubscribeAsync(this IMqttServer server, string clientId, params MqttTopicFilter[] topicFilters)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (clientId == null) throw new ArgumentNullException(nameof(clientId));
            if (topicFilters == null) throw new ArgumentNullException(nameof(topicFilters));

            return server.SubscribeAsync(clientId, topicFilters);
        }

        /// <summary>
        /// 以指定主题和服务质量等级在服务端为该客户端建立订阅
        /// </summary>
        /// <param name="server">目标服务端，不能为 null</param>
        /// <param name="clientId">订阅方客户端标识，不能为 null</param>
        /// <param name="topic">要订阅的主题过滤器，不能为 null</param>
        /// <param name="qualityOfServiceLevel">订阅授予的服务质量等级</param>
        /// <returns>订阅完成的任务</returns>
        public static Task SubscribeAsync(this IMqttServer server, string clientId, string topic, MqttQualityOfServiceLevel qualityOfServiceLevel)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (clientId == null) throw new ArgumentNullException(nameof(clientId));
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            return server.SubscribeAsync(clientId, new MqttTopicFilterBuilder().WithTopic(topic).WithQualityOfServiceLevel(qualityOfServiceLevel).Build());
        }

        /// <summary>
        /// 以指定主题在服务端为该客户端建立订阅，服务质量等级取过滤器默认值
        /// </summary>
        /// <param name="server">目标服务端，不能为 null</param>
        /// <param name="clientId">订阅方客户端标识，不能为 null</param>
        /// <param name="topic">要订阅的主题过滤器，不能为 null</param>
        /// <returns>订阅完成的任务</returns>
        public static Task SubscribeAsync(this IMqttServer server, string clientId, string topic)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (clientId == null) throw new ArgumentNullException(nameof(clientId));
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            return server.SubscribeAsync(clientId, new MqttTopicFilterBuilder().WithTopic(topic).Build());
        }

        /// <summary>
        /// 在服务端为该客户端取消订阅指定主题过滤器
        /// </summary>
        /// <param name="server">目标服务端，不能为 null</param>
        /// <param name="clientId">取消订阅的客户端标识，不能为 null</param>
        /// <param name="topicFilters">要移除的主题过滤器，不能为 null</param>
        /// <returns>取消订阅完成的任务</returns>
        public static Task UnsubscribeAsync(this IMqttServer server, string clientId, params string[] topicFilters)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (clientId == null) throw new ArgumentNullException(nameof(clientId));
            if (topicFilters == null) throw new ArgumentNullException(nameof(topicFilters));

            return server.UnsubscribeAsync(clientId, topicFilters);
        }

        /// <summary>
        /// 按枚举顺序逐条发布应用消息，串行等待每一条发布完成
        /// </summary>
        /// <param name="server">目标服务端，不能为 null</param>
        /// <param name="applicationMessages">待发布的消息序列，不能为 null</param>
        /// <returns>全部消息发布完成的任务，首条失败即中断并抛出</returns>
        public static async Task PublishAsync(this IMqttServer server, IEnumerable<MqttApplicationMessage> applicationMessages)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (applicationMessages == null) throw new ArgumentNullException(nameof(applicationMessages));

            foreach (var applicationMessage in applicationMessages)
            {
                await server.PublishAsync(applicationMessage).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 发布单条应用消息
        /// </summary>
        /// <param name="server">目标服务端，不能为 null</param>
        /// <param name="applicationMessage">待发布的应用消息，不能为 null</param>
        /// <returns>发布结果，包含原因码与 QoS 1/2 的报文标识符</returns>
        public static Task<MqttClientPublishResult> PublishAsync(this IMqttServer server, MqttApplicationMessage applicationMessage)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (applicationMessage == null) throw new ArgumentNullException(nameof(applicationMessage));

            return server.PublishAsync(applicationMessage, CancellationToken.None);
        }

        /// <summary>
        /// 按数组顺序逐条发布应用消息，串行等待每一条发布完成
        /// </summary>
        /// <param name="server">目标服务端，不能为 null</param>
        /// <param name="applicationMessages">待发布的消息数组，不能为 null</param>
        /// <returns>全部消息发布完成的任务，使用无取消标记</returns>
        public static async Task PublishAsync(this IMqttServer server, params MqttApplicationMessage[] applicationMessages)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (applicationMessages == null) throw new ArgumentNullException(nameof(applicationMessages));

            foreach (var applicationMessage in applicationMessages)
            {
                await server.PublishAsync(applicationMessage, CancellationToken.None).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 向指定主题发布空载荷消息
        /// </summary>
        /// <param name="server">目标服务端，不能为 null</param>
        /// <param name="topic">目标主题，不能为 null</param>
        /// <returns>发布结果，包含原因码与 QoS 1/2 的报文标识符</returns>
        public static Task<MqttClientPublishResult> PublishAsync(this IMqttServer server, string topic)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            return server.PublishAsync(builder => builder
                .WithTopic(topic));
        }

        /// <summary>
        /// 向指定主题发布文本载荷，使用默认服务质量等级且不保留
        /// </summary>
        /// <param name="server">目标服务端，不能为 null</param>
        /// <param name="topic">目标主题，不能为 null</param>
        /// <param name="payload">文本载荷，按 UTF-8 编码写入</param>
        /// <returns>发布结果，包含原因码与 QoS 1/2 的报文标识符</returns>
        public static Task<MqttClientPublishResult> PublishAsync(this IMqttServer server, string topic, string payload)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            return server.PublishAsync(builder => builder
                .WithTopic(topic)
                .WithPayload(payload));
        }

        /// <summary>
        /// 向指定主题发布文本载荷并指定服务质量等级，不保留
        /// </summary>
        /// <param name="server">目标服务端，不能为 null</param>
        /// <param name="topic">目标主题，不能为 null</param>
        /// <param name="payload">文本载荷，按 UTF-8 编码写入</param>
        /// <param name="qualityOfServiceLevel">发布使用的服务质量等级</param>
        /// <returns>发布结果，包含原因码与 QoS 1/2 的报文标识符</returns>
        public static Task<MqttClientPublishResult> PublishAsync(this IMqttServer server, string topic, string payload, MqttQualityOfServiceLevel qualityOfServiceLevel)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            return server.PublishAsync(builder => builder
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(qualityOfServiceLevel));
        }

        /// <summary>
        /// 向指定主题发布文本载荷，并指定服务质量等级与保留标志
        /// </summary>
        /// <param name="server">目标服务端，不能为 null</param>
        /// <param name="topic">目标主题，不能为 null</param>
        /// <param name="payload">文本载荷，按 UTF-8 编码写入</param>
        /// <param name="qualityOfServiceLevel">发布使用的服务质量等级</param>
        /// <param name="retain">是否把消息保存为该主题的 retained 消息</param>
        /// <returns>发布结果，包含原因码与 QoS 1/2 的报文标识符</returns>
        public static Task<MqttClientPublishResult> PublishAsync(this IMqttServer server, string topic, string payload, MqttQualityOfServiceLevel qualityOfServiceLevel, bool retain)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            return server.PublishAsync(builder => builder
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(qualityOfServiceLevel)
                .WithRetainFlag(retain));
        }

        /// <summary>
        /// 用消息构建器构造消息后发布，并支持取消
        /// </summary>
        /// <param name="server">目标服务端，不能为 null</param>
        /// <param name="builder">基于空构建器补全消息内容的委托</param>
        /// <param name="cancellationToken">取消发布操作的标记</param>
        /// <returns>发布结果，包含原因码与 QoS 1/2 的报文标识符</returns>
        public static Task<MqttClientPublishResult> PublishAsync(this IMqttServer server, Func<MqttApplicationMessageBuilder, MqttApplicationMessageBuilder> builder, CancellationToken cancellationToken)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            var message = builder(new MqttApplicationMessageBuilder()).Build();
            return server.PublishAsync(message, cancellationToken);
        }

        /// <summary>
        /// 用消息构建器构造消息后发布，使用无取消标记
        /// </summary>
        /// <param name="server">目标服务端，不能为 null</param>
        /// <param name="builder">基于空构建器补全消息内容的委托</param>
        /// <returns>发布结果，包含原因码与 QoS 1/2 的报文标识符</returns>
        public static Task<MqttClientPublishResult> PublishAsync(this IMqttServer server, Func<MqttApplicationMessageBuilder, MqttApplicationMessageBuilder> builder)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            var message = builder(new MqttApplicationMessageBuilder()).Build();
            return server.PublishAsync(message, CancellationToken.None);
        }
    }
}
