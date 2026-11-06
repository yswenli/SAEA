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
*文件名： ManagedMqttClientExtensions
*版本号： v26.4.23.1
*唯一标识：461e336a-a194-4614-a9f1-4251c9d7321e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：ManagedMqttClientExtensions接口
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
using SAEA.MQTT.Client.Publishing;
using SAEA.MQTT.Client.Receiving;
using SAEA.MQTT.Protocol;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.Extensions.ManagedClient
{
    /// <summary>
    /// 托管客户端与消息发布者的链式调用扩展方法
    /// </summary>
    public static class ManagedMqttClientExtensions
    {
        /// <summary>
        /// 用异步回调设置连接成功处理器，传入null表示清除
        /// </summary>
        /// <param name="client">目标托管客户端，不能为null</param>
        /// <param name="handler">异步处理回调，可为null</param>
        /// <returns>同一个客户端实例</returns>
        public static IManagedMqttClient UseConnectedHandler(this IManagedMqttClient client, Func<MqttClientConnectedEventArgs, Task> handler)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            if (handler == null)
            {
                return client.UseConnectedHandler((IMqttClientConnectedHandler)null);
            }

            return client.UseConnectedHandler(new MqttClientConnectedHandlerDelegate(handler));
        }

        /// <summary>
        /// 用同步回调设置连接成功处理器，传入null表示清除
        /// </summary>
        /// <param name="client">目标托管客户端，不能为null</param>
        /// <param name="handler">同步处理回调，可为null</param>
        /// <returns>同一个客户端实例</returns>
        public static IManagedMqttClient UseConnectedHandler(this IManagedMqttClient client, Action<MqttClientConnectedEventArgs> handler)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            if (handler == null)
            {
                return client.UseConnectedHandler((IMqttClientConnectedHandler)null);
            }

            return client.UseConnectedHandler(new MqttClientConnectedHandlerDelegate(handler));
        }

        /// <summary>
        /// 直接设置连接成功处理器
        /// </summary>
        /// <param name="client">目标托管客户端，不能为null</param>
        /// <param name="handler">处理器实例，可为null表示清除</param>
        /// <returns>同一个客户端实例</returns>
        public static IManagedMqttClient UseConnectedHandler(this IManagedMqttClient client, IMqttClientConnectedHandler handler)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            client.ConnectedHandler = handler;
            return client;
        }

        /// <summary>
        /// 用异步回调设置连接断开处理器，传入null表示清除
        /// </summary>
        /// <param name="client">目标托管客户端，不能为null</param>
        /// <param name="handler">异步处理回调，可为null</param>
        /// <returns>同一个客户端实例</returns>
        public static IManagedMqttClient UseDisconnectedHandler(this IManagedMqttClient client, Func<MqttClientDisconnectedEventArgs, Task> handler)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            if (handler == null)
            {
                return client.UseDisconnectedHandler((IMqttClientDisconnectedHandler)null);
            }

            return client.UseDisconnectedHandler(new MqttClientDisconnectedHandlerDelegate(handler));
        }

        /// <summary>
        /// 用同步回调设置连接断开处理器，传入null表示清除
        /// </summary>
        /// <param name="client">目标托管客户端，不能为null</param>
        /// <param name="handler">同步处理回调，可为null</param>
        /// <returns>同一个客户端实例</returns>
        public static IManagedMqttClient UseDisconnectedHandler(this IManagedMqttClient client, Action<MqttClientDisconnectedEventArgs> handler)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            if (handler == null)
            {
                return client.UseDisconnectedHandler((IMqttClientDisconnectedHandler)null);
            }

            return client.UseDisconnectedHandler(new MqttClientDisconnectedHandlerDelegate(handler));
        }

        /// <summary>
        /// 直接设置连接断开处理器
        /// </summary>
        /// <param name="client">目标托管客户端，不能为null</param>
        /// <param name="handler">处理器实例，可为null表示清除</param>
        /// <returns>同一个客户端实例</returns>
        public static IManagedMqttClient UseDisconnectedHandler(this IManagedMqttClient client, IMqttClientDisconnectedHandler handler)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            client.DisconnectedHandler = handler;
            return client;
        }

        /// <summary>
        /// 用异步回调设置应用消息接收处理器，传入null表示清除
        /// </summary>
        /// <typeparam name="TReceiver">可接收应用消息的目标类型</typeparam>
        /// <param name="receiver">目标接收者，不能为null</param>
        /// <param name="handler">异步处理回调，可为null</param>
        /// <returns>同一个接收者实例</returns>
        public static TReceiver UseApplicationMessageReceivedHandler<TReceiver>(this TReceiver receiver, Func<MqttApplicationMessageReceivedEventArgs, Task> handler)
            where TReceiver : IApplicationMessageReceiver
        {
            if (receiver == null) throw new ArgumentNullException(nameof(receiver));

            if (handler == null)
            {
                return receiver.UseApplicationMessageReceivedHandler((IMqttApplicationMessageReceivedHandler)null);
            }

            return receiver.UseApplicationMessageReceivedHandler(new MqttApplicationMessageReceivedHandlerDelegate(handler));
        }

        /// <summary>
        /// 用同步回调设置应用消息接收处理器，传入null表示清除
        /// </summary>
        /// <typeparam name="TReceiver">可接收应用消息的目标类型</typeparam>
        /// <param name="receiver">目标接收者，不能为null</param>
        /// <param name="handler">同步处理回调，可为null</param>
        /// <returns>同一个接收者实例</returns>
        public static TReceiver UseApplicationMessageReceivedHandler<TReceiver>(this TReceiver receiver, Action<MqttApplicationMessageReceivedEventArgs> handler)
            where TReceiver : IApplicationMessageReceiver
        {
            if (receiver == null) throw new ArgumentNullException(nameof(receiver));

            if (handler == null)
            {
                return receiver.UseApplicationMessageReceivedHandler((IMqttApplicationMessageReceivedHandler)null);
            }

            return receiver.UseApplicationMessageReceivedHandler(new MqttApplicationMessageReceivedHandlerDelegate(handler));
        }

        /// <summary>
        /// 直接设置应用消息接收处理器
        /// </summary>
        /// <typeparam name="TReceiver">可接收应用消息的目标类型</typeparam>
        /// <param name="receiver">目标接收者，不能为null</param>
        /// <param name="handler">处理器实例，可为null表示清除</param>
        /// <returns>同一个接收者实例</returns>
        public static TReceiver UseApplicationMessageReceivedHandler<TReceiver>(this TReceiver receiver, IMqttApplicationMessageReceivedHandler handler)
            where TReceiver : IApplicationMessageReceiver
        {
            if (receiver == null) throw new ArgumentNullException(nameof(receiver));

            receiver.ApplicationMessageReceivedHandler = handler;
            return receiver;
        }

        /// <summary>
        /// 订阅一组主题过滤条件
        /// </summary>
        /// <param name="client">目标托管客户端，不能为null</param>
        /// <param name="topicFilters">主题过滤条件数组</param>
        /// <returns>登记完成的任务，不等待服务端确认</returns>
        public static Task SubscribeAsync(this IManagedMqttClient client, params MqttTopicFilter[] topicFilters)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            return client.SubscribeAsync(topicFilters);
        }

        /// <summary>
        /// 以指定服务质量订阅单个主题
        /// </summary>
        /// <param name="client">目标托管客户端，不能为null</param>
        /// <param name="topic">订阅主题，不能为null</param>
        /// <param name="qualityOfServiceLevel">服务质量等级</param>
        /// <returns>登记完成的任务，不等待服务端确认</returns>
        public static Task SubscribeAsync(this IManagedMqttClient client, string topic, MqttQualityOfServiceLevel qualityOfServiceLevel)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            return client.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(topic).WithQualityOfServiceLevel(qualityOfServiceLevel).Build());
        }

        /// <summary>
        /// 使用默认服务质量等级订阅单个主题
        /// </summary>
        /// <param name="client">目标托管客户端，不能为null</param>
        /// <param name="topic">订阅主题，不能为null</param>
        /// <returns>登记完成的任务，不等待服务端确认</returns>
        public static Task SubscribeAsync(this IManagedMqttClient client, string topic)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            return client.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(topic).Build());
        }

        /// <summary>
        /// 取消订阅一组主题
        /// </summary>
        /// <param name="client">目标托管客户端，不能为null</param>
        /// <param name="topicFilters">要取消订阅的主题数组</param>
        /// <returns>登记完成的任务，不等待服务端确认</returns>
        public static Task UnsubscribeAsync(this IManagedMqttClient client, params string[] topicFilters)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            return client.UnsubscribeAsync(topicFilters);
        }

        /// <summary>
        /// 按顺序逐条发布多条应用消息，不返回各条结果
        /// </summary>
        /// <param name="publisher">消息发布者，不能为null</param>
        /// <param name="applicationMessages">应用消息集合，不能为null</param>
        /// <returns>全部发送完成的异步任务</returns>
        public static async Task PublishAsync(this IApplicationMessagePublisher publisher, IEnumerable<MqttApplicationMessage> applicationMessages)
        {
            if (publisher == null) throw new ArgumentNullException(nameof(publisher));
            if (applicationMessages == null) throw new ArgumentNullException(nameof(applicationMessages));

            foreach (var applicationMessage in applicationMessages)
            {
                await publisher.PublishAsync(applicationMessage).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 在不使用取消令牌的情况下发布单条应用消息
        /// </summary>
        /// <param name="publisher">消息发布者，不能为null</param>
        /// <param name="applicationMessage">应用消息，不能为null</param>
        /// <returns>发布结果</returns>
        public static Task<MqttClientPublishResult> PublishAsync(this IApplicationMessagePublisher publisher, MqttApplicationMessage applicationMessage)
        {
            if (publisher == null) throw new ArgumentNullException(nameof(publisher));
            if (applicationMessage == null) throw new ArgumentNullException(nameof(applicationMessage));

            return publisher.PublishAsync(applicationMessage, CancellationToken.None);
        }

        /// <summary>
        /// 按顺序逐条发布多条应用消息，不返回各条结果
        /// </summary>
        /// <param name="publisher">消息发布者，不能为null</param>
        /// <param name="applicationMessages">应用消息数组，不能为null</param>
        /// <returns>全部发送完成的异步任务</returns>
        public static async Task PublishAsync(this IApplicationMessagePublisher publisher, params MqttApplicationMessage[] applicationMessages)
        {
            if (publisher == null) throw new ArgumentNullException(nameof(publisher));
            if (applicationMessages == null) throw new ArgumentNullException(nameof(applicationMessages));

            foreach (var applicationMessage in applicationMessages)
            {
                await publisher.PublishAsync(applicationMessage, CancellationToken.None).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 向指定主题发布空载荷消息
        /// </summary>
        /// <param name="publisher">消息发布者，不能为null</param>
        /// <param name="topic">目标主题，不能为null</param>
        /// <returns>发布结果</returns>
        public static Task<MqttClientPublishResult> PublishAsync(this IApplicationMessagePublisher publisher, string topic)
        {
            if (publisher == null) throw new ArgumentNullException(nameof(publisher));
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            return publisher.PublishAsync(builder => builder
                .WithTopic(topic));
        }

        /// <summary>
        /// 向指定主题发布字符串载荷
        /// </summary>
        /// <param name="publisher">消息发布者，不能为null</param>
        /// <param name="topic">目标主题，不能为null</param>
        /// <param name="payload">消息载荷</param>
        /// <returns>发布结果</returns>
        public static Task<MqttClientPublishResult> PublishAsync(this IApplicationMessagePublisher publisher, string topic, string payload)
        {
            if (publisher == null) throw new ArgumentNullException(nameof(publisher));
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            return publisher.PublishAsync(builder => builder
                .WithTopic(topic)
                .WithPayload(payload));
        }

        /// <summary>
        /// 以指定服务质量向主题发布字符串载荷
        /// </summary>
        /// <param name="publisher">消息发布者，不能为null</param>
        /// <param name="topic">目标主题，不能为null</param>
        /// <param name="payload">消息载荷</param>
        /// <param name="qualityOfServiceLevel">服务质量等级</param>
        /// <returns>发布结果</returns>
        public static Task<MqttClientPublishResult> PublishAsync(this IApplicationMessagePublisher publisher, string topic, string payload, MqttQualityOfServiceLevel qualityOfServiceLevel)
        {
            if (publisher == null) throw new ArgumentNullException(nameof(publisher));
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            return publisher.PublishAsync(builder => builder
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(qualityOfServiceLevel));
        }

        /// <summary>
        /// 以指定服务质量与保留标志向主题发布字符串载荷
        /// </summary>
        /// <param name="publisher">消息发布者，不能为null</param>
        /// <param name="topic">目标主题，不能为null</param>
        /// <param name="payload">消息载荷</param>
        /// <param name="qualityOfServiceLevel">服务质量等级</param>
        /// <param name="retain">是否设置保留标志</param>
        /// <returns>发布结果</returns>
        public static Task<MqttClientPublishResult> PublishAsync(this IApplicationMessagePublisher publisher, string topic, string payload, MqttQualityOfServiceLevel qualityOfServiceLevel, bool retain)
        {
            if (publisher == null) throw new ArgumentNullException(nameof(publisher));
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            return publisher.PublishAsync(builder => builder
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(qualityOfServiceLevel)
                .WithRetainFlag(retain));
        }

        /// <summary>
        /// 用构造回调生成消息并按指定取消令牌发布
        /// </summary>
        /// <param name="publisher">消息发布者，不能为null</param>
        /// <param name="builder">消息构造回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>发布结果</returns>
        public static Task<MqttClientPublishResult> PublishAsync(this IApplicationMessagePublisher publisher, Func<MqttApplicationMessageBuilder, MqttApplicationMessageBuilder> builder, CancellationToken cancellationToken)
        {
            if (publisher == null) throw new ArgumentNullException(nameof(publisher));

            var message = builder(new MqttApplicationMessageBuilder()).Build();
            return publisher.PublishAsync(message, cancellationToken);
        }

        /// <summary>
        /// 用构造回调生成消息并发布
        /// </summary>
        /// <param name="publisher">消息发布者，不能为null</param>
        /// <param name="builder">消息构造回调</param>
        /// <returns>发布结果</returns>
        public static Task<MqttClientPublishResult> PublishAsync(this IApplicationMessagePublisher publisher, Func<MqttApplicationMessageBuilder, MqttApplicationMessageBuilder> builder)
        {
            if (publisher == null) throw new ArgumentNullException(nameof(publisher));

            var message = builder(new MqttApplicationMessageBuilder()).Build();
            return publisher.PublishAsync(message, CancellationToken.None);
        }
    }
}
