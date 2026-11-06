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
*命名空间：SAEA.MQTT.Client
*文件名： MqttClientExtensions
*版本号： v26.4.23.1
*唯一标识：d26d4ac2-8740-4325-81fe-9d3993f79199
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientExtensions接口
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
using SAEA.MQTT.Client.ExtendedAuthenticationExchange;
using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Client.Publishing;
using SAEA.MQTT.Client.Receiving;
using SAEA.MQTT.Client.Subscribing;
using SAEA.MQTT.Client.Unsubscribing;
using SAEA.MQTT.Protocol;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.Client
{
    /// <summary>
    /// 为 <see cref="IMqttClient"/> 提供回调设置与常用重载的扩展方法
    /// </summary>
    public static class MqttClientExtensions
    {
        /// <summary>
        /// 以异步委托设置连接成功回调，传入 null 清除回调
        /// </summary>
        /// <param name="client">目标客户端，不能为 null</param>
        /// <param name="handler">连接成功时执行的异步委托，null 表示清除回调</param>
        /// <returns>原客户端，便于链式调用</returns>
        public static IMqttClient UseConnectedHandler(this IMqttClient client, Func<MqttClientConnectedEventArgs, Task> handler)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            if (handler == null)
            {
                return client.UseConnectedHandler((IMqttClientConnectedHandler)null);
            }

            return client.UseConnectedHandler(new MqttClientConnectedHandlerDelegate(handler));
        }

        /// <summary>
        /// 以同步委托设置连接成功回调，回调返回后即视为完成
        /// </summary>
        /// <param name="client">目标客户端，不能为 null</param>
        /// <param name="handler">连接成功时执行的同步委托，null 表示清除回调</param>
        /// <returns>原客户端，便于链式调用</returns>
        public static IMqttClient UseConnectedHandler(this IMqttClient client, Action<MqttClientConnectedEventArgs> handler)
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
        /// <param name="client">目标客户端，不能为 null</param>
        /// <param name="handler">连接成功处理器，null 表示清除回调</param>
        /// <returns>原客户端，便于链式调用</returns>
        public static IMqttClient UseConnectedHandler(this IMqttClient client, IMqttClientConnectedHandler handler)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            client.ConnectedHandler = handler;
            return client;
        }

        /// <summary>
        /// 以异步委托设置连接断开回调，传入 null 清除回调
        /// </summary>
        /// <param name="client">目标客户端，不能为 null</param>
        /// <param name="handler">连接断开时执行的异步委托，null 表示清除回调</param>
        /// <returns>原客户端，便于链式调用</returns>
        public static IMqttClient UseDisconnectedHandler(this IMqttClient client, Func<MqttClientDisconnectedEventArgs, Task> handler)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            if (handler == null)
            {
                return client.UseDisconnectedHandler((IMqttClientDisconnectedHandler)null);
            }

            return client.UseDisconnectedHandler(new MqttClientDisconnectedHandlerDelegate(handler));
        }

        /// <summary>
        /// 以同步委托设置连接断开回调，回调返回后即视为完成
        /// </summary>
        /// <param name="client">目标客户端，不能为 null</param>
        /// <param name="handler">连接断开时执行的同步委托，null 表示清除回调</param>
        /// <returns>原客户端，便于链式调用</returns>
        public static IMqttClient UseDisconnectedHandler(this IMqttClient client, Action<MqttClientDisconnectedEventArgs> handler)
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
        /// <param name="client">目标客户端，不能为 null</param>
        /// <param name="handler">连接断开处理器，null 表示清除回调</param>
        /// <returns>原客户端，便于链式调用</returns>
        public static IMqttClient UseDisconnectedHandler(this IMqttClient client, IMqttClientDisconnectedHandler handler)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            client.DisconnectedHandler = handler;
            return client;
        }

        /// <summary>
        /// 以异步委托设置应用消息接收回调，传入 null 清除回调
        /// </summary>
        /// <param name="client">目标客户端，不能为 null</param>
        /// <param name="handler">收到消息时执行的异步委托，null 表示清除回调</param>
        /// <returns>原客户端，便于链式调用</returns>
        public static IMqttClient UseApplicationMessageReceivedHandler(this IMqttClient client, Func<MqttApplicationMessageReceivedEventArgs, Task> handler)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            if (handler == null)
            {
                return client.UseApplicationMessageReceivedHandler((IMqttApplicationMessageReceivedHandler)null);
            }

            return client.UseApplicationMessageReceivedHandler(new MqttApplicationMessageReceivedHandlerDelegate(handler));
        }

        /// <summary>
        /// 以同步委托设置应用消息接收回调，回调返回后即视为完成
        /// </summary>
        /// <param name="client">目标客户端，不能为 null</param>
        /// <param name="handler">收到消息时执行的同步委托，null 表示清除回调</param>
        /// <returns>原客户端，便于链式调用</returns>
        public static IMqttClient UseApplicationMessageReceivedHandler(this IMqttClient client, Action<MqttApplicationMessageReceivedEventArgs> handler)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            if (handler == null)
            {
                return client.UseApplicationMessageReceivedHandler((IMqttApplicationMessageReceivedHandler)null);
            }

            return client.UseApplicationMessageReceivedHandler(new MqttApplicationMessageReceivedHandlerDelegate(handler));
        }

        /// <summary>
        /// 直接设置应用消息接收处理器
        /// </summary>
        /// <param name="client">目标客户端，不能为 null</param>
        /// <param name="handler">应用消息接收处理器，null 表示清除回调</param>
        /// <returns>原客户端，便于链式调用</returns>
        public static IMqttClient UseApplicationMessageReceivedHandler(this IMqttClient client, IMqttApplicationMessageReceivedHandler handler)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            client.ApplicationMessageReceivedHandler = handler;
            return client;
        }

        /// <summary>
        /// 用上次连接时保存的选项重新连接
        /// </summary>
        /// <param name="client">目标客户端，不能为 null</param>
        /// <returns>重连完成的任务；客户端从未连接过时抛出 InvalidOperationException</returns>
        public static Task ReconnectAsync(this IMqttClient client)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            if (client.Options == null)
            {
                throw new InvalidOperationException("_ReconnectAsync_ can be used only if _ConnectAsync_ was called before.");
            }

            return client.ConnectAsync(client.Options);
        }

        /// <summary>
        /// 以默认选项（正常断开）断开连接
        /// </summary>
        /// <param name="client">目标客户端，不能为 null</param>
        /// <returns>断开完成的任务</returns>
        public static Task DisconnectAsync(this IMqttClient client)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            return client.DisconnectAsync(new MqttClientDisconnectOptions());
        }

        /// <summary>
        /// 订阅多个主题过滤器
        /// </summary>
        /// <param name="client">目标客户端，不能为 null</param>
        /// <param name="topicFilters">主题过滤器集合，不能为 null</param>
        /// <returns>订阅结果</returns>
        public static Task<MqttClientSubscribeResult> SubscribeAsync(this IMqttClient client, params MqttTopicFilter[] topicFilters)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (topicFilters == null) throw new ArgumentNullException(nameof(topicFilters));

            var options = new MqttClientSubscribeOptions();
            options.TopicFilters.AddRange(topicFilters);

            return client.SubscribeAsync(options);
        }

        /// <summary>
        /// 以指定 QoS 等级订阅单个主题
        /// </summary>
        /// <param name="client">目标客户端，不能为 null</param>
        /// <param name="topic">主题过滤器，不能为 null</param>
        /// <param name="qualityOfServiceLevel">订阅时请求的 QoS 等级</param>
        /// <returns>订阅结果</returns>
        public static Task<MqttClientSubscribeResult> SubscribeAsync(this IMqttClient client, string topic, MqttQualityOfServiceLevel qualityOfServiceLevel)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            return client.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(topic).WithQualityOfServiceLevel(qualityOfServiceLevel).Build());
        }

        /// <summary>
        /// 以 QoS0 订阅单个主题
        /// </summary>
        /// <param name="client">目标客户端，不能为 null</param>
        /// <param name="topic">主题过滤器，不能为 null</param>
        /// <returns>订阅结果</returns>
        public static Task<MqttClientSubscribeResult> SubscribeAsync(this IMqttClient client, string topic)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            return client.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(topic).Build());
        }

        /// <summary>
        /// 取消订阅多个主题过滤器
        /// </summary>
        /// <param name="client">目标客户端，不能为 null</param>
        /// <param name="topicFilters">主题过滤器集合，不能为 null</param>
        /// <returns>取消订阅结果</returns>
        public static Task<MqttClientUnsubscribeResult> UnsubscribeAsync(this IMqttClient client, params string[] topicFilters)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (topicFilters == null) throw new ArgumentNullException(nameof(topicFilters));

            var options = new MqttClientUnsubscribeOptions();
            options.TopicFilters.AddRange(topicFilters);

            return client.UnsubscribeAsync(options);
        }

        /// <summary>
        /// 连接服务端，不提供取消令牌
        /// </summary>
        /// <param name="client">目标客户端，不能为 null</param>
        /// <param name="options">连接选项，不能为 null</param>
        /// <returns>服务端返回的连接认证结果</returns>
        public static Task<MqttClientAuthenticateResult> ConnectAsync(this IMqttClient client, IMqttClientOptions options)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            return client.ConnectAsync(options, CancellationToken.None);
        }

        /// <summary>
        /// 按指定选项断开连接，不提供取消令牌
        /// </summary>
        /// <param name="client">目标客户端，不能为 null</param>
        /// <param name="options">断开选项，不能为 null</param>
        /// <returns>断开完成的任务</returns>
        public static Task DisconnectAsync(this IMqttClient client, MqttClientDisconnectOptions options)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            return client.DisconnectAsync(options, CancellationToken.None);
        }

        /// <summary>
        /// 回复增强认证数据，不提供取消令牌
        /// </summary>
        /// <param name="client">目标客户端，不能为 null</param>
        /// <param name="data">要发送的认证数据，不能为 null</param>
        /// <returns>报文发出后完成的任务</returns>
        public static Task SendExtendedAuthenticationExchangeDataAsync(this IMqttClient client, MqttExtendedAuthenticationExchangeData data)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            return client.SendExtendedAuthenticationExchangeDataAsync(data, CancellationToken.None);
        }

        /// <summary>
        /// 按指定选项订阅，不提供取消令牌
        /// </summary>
        /// <param name="client">目标客户端，不能为 null</param>
        /// <param name="options">订阅选项，不能为 null</param>
        /// <returns>订阅结果</returns>
        public static Task<MqttClientSubscribeResult> SubscribeAsync(this IMqttClient client, MqttClientSubscribeOptions options)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            return client.SubscribeAsync(options, CancellationToken.None);
        }

        /// <summary>
        /// 按指定选项取消订阅，不提供取消令牌
        /// </summary>
        /// <param name="client">目标客户端，不能为 null</param>
        /// <param name="options">取消订阅选项，不能为 null</param>
        /// <returns>取消订阅结果</returns>
        public static Task<MqttClientUnsubscribeResult> UnsubscribeAsync(this IMqttClient client, MqttClientUnsubscribeOptions options)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            return client.UnsubscribeAsync(options, CancellationToken.None);
        }

        /// <summary>
        /// 发布单条应用消息，不提供取消令牌
        /// </summary>
        /// <param name="client">目标客户端，不能为 null</param>
        /// <param name="applicationMessage">待发布的消息，不能为 null</param>
        /// <returns>发布结果</returns>
        public static Task<MqttClientPublishResult> PublishAsync(this IMqttClient client, MqttApplicationMessage applicationMessage)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (applicationMessage == null) throw new ArgumentNullException(nameof(applicationMessage));

            return client.PublishAsync(applicationMessage, CancellationToken.None);
        }

        /// <summary>
        /// 按集合顺序逐条发布，前一条完成后再发送下一条
        /// </summary>
        /// <param name="client">目标客户端，不能为 null</param>
        /// <param name="applicationMessages">待发布的消息集合，不能为 null</param>
        /// <returns>全部消息发布完成后完成的任务；任一条失败即抛出异常</returns>
        public static async Task PublishAsync(this IMqttClient client, IEnumerable<MqttApplicationMessage> applicationMessages)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (applicationMessages == null) throw new ArgumentNullException(nameof(applicationMessages));

            foreach (var applicationMessage in applicationMessages)
            {
                await client.PublishAsync(applicationMessage).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 向指定主题发布空负载消息，QoS0 且不保留
        /// </summary>
        /// <param name="client">目标客户端，不能为 null</param>
        /// <param name="topic">消息主题，不能为 null</param>
        /// <returns>发布结果</returns>
        public static Task<MqttClientPublishResult> PublishAsync(this IMqttClient client, string topic)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            return client.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .Build());
        }

        /// <summary>
        /// 向指定主题发布二进制负载，QoS0 且不保留
        /// </summary>
        /// <param name="client">目标客户端，不能为 null</param>
        /// <param name="topic">消息主题，不能为 null</param>
        /// <param name="payload">消息负载字节序列</param>
        /// <returns>发布结果</returns>
        public static Task<MqttClientPublishResult> PublishAsync(this IMqttClient client, string topic, IEnumerable<byte> payload)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            return client.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .Build());
        }

        /// <summary>
        /// 向指定主题发布字符串负载，QoS0 且不保留
        /// </summary>
        /// <param name="client">目标客户端，不能为 null</param>
        /// <param name="topic">消息主题，不能为 null</param>
        /// <param name="payload">消息负载文本</param>
        /// <returns>发布结果</returns>
        public static Task<MqttClientPublishResult> PublishAsync(this IMqttClient client, string topic, string payload)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            return client.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .Build());
        }

        /// <summary>
        /// 以指定 QoS 等级向指定主题发布字符串负载，不保留
        /// </summary>
        /// <param name="client">目标客户端，不能为 null</param>
        /// <param name="topic">消息主题，不能为 null</param>
        /// <param name="payload">消息负载文本</param>
        /// <param name="qualityOfServiceLevel">发布使用的 QoS 等级</param>
        /// <returns>发布结果</returns>
        public static Task<MqttClientPublishResult> PublishAsync(this IMqttClient client, string topic, string payload, MqttQualityOfServiceLevel qualityOfServiceLevel)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            return client.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(qualityOfServiceLevel)
                .Build());
        }

        /// <summary>
        /// 以指定 QoS 等级发布字符串负载并可设置保留标志
        /// </summary>
        /// <param name="client">目标客户端，不能为 null</param>
        /// <param name="topic">消息主题，不能为 null</param>
        /// <param name="payload">消息负载文本</param>
        /// <param name="qualityOfServiceLevel">发布使用的 QoS 等级</param>
        /// <param name="retain">是否由服务端保留该消息</param>
        /// <returns>发布结果</returns>
        public static Task<MqttClientPublishResult> PublishAsync(this IMqttClient client, string topic, string payload, MqttQualityOfServiceLevel qualityOfServiceLevel, bool retain)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            return client.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(qualityOfServiceLevel)
                .WithRetainFlag(retain)
                .Build());
        }

        /// <summary>
        /// 发布字符串负载并可设置保留标志，QoS0
        /// </summary>
        /// <param name="client">目标客户端，不能为 null</param>
        /// <param name="topic">消息主题，不能为 null</param>
        /// <param name="payload">消息负载文本</param>
        /// <param name="retain">是否由服务端保留该消息</param>
        /// <returns>发布结果</returns>
        public static Task<MqttClientPublishResult> PublishAsync(this IMqttClient client, string topic, string payload, bool retain)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            return client.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithRetainFlag(retain)
                .Build());
        }
    }
}