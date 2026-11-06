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
*文件名： MqttClientSessionApplicationMessagesQueue
*版本号： v26.4.23.1
*唯一标识：b3972f1a-e633-4ff5-b500-6d9c647914c9
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientSessionApplicationMessagesQueue接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.MQTT.Internal;
using SAEA.MQTT.Protocol;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 单会话的待投递消息队列，按配置的上限与溢出策略控制排队长度
    /// </summary>
    public sealed class MqttClientSessionApplicationMessagesQueue : IDisposable
    {
        readonly AsyncQueue<MqttQueuedApplicationMessage> _messageQueue = new AsyncQueue<MqttQueuedApplicationMessage>();

        readonly IMqttServerOptions _options;

        /// <summary>
        /// 创建消息队列，队列上限与溢出策略取自服务端配置
        /// </summary>
        /// <param name="options">服务端配置，不可为 null</param>
        public MqttClientSessionApplicationMessagesQueue(IMqttServerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// 当前排队消息数量
        /// </summary>
        public int Count => _messageQueue.Count;

        /// <summary>
        /// 以最简参数入队，等价于 RetainAsPublished=false、无订阅标识符、无到期时间
        /// </summary>
        /// <param name="applicationMessage">应用消息，不可为 null</param>
        /// <param name="senderClientId">发布方标识，可为 null</param>
        /// <param name="qualityOfServiceLevel">本次投递使用的 QoS</param>
        /// <param name="isRetainedMessage">是否为 retained 补发</param>
        public void Enqueue(MqttApplicationMessage applicationMessage, string senderClientId, MqttQualityOfServiceLevel qualityOfServiceLevel, bool isRetainedMessage)
        {
            if (applicationMessage == null) throw new ArgumentNullException(nameof(applicationMessage));

            Enqueue(new MqttQueuedApplicationMessage
            {
                ApplicationMessage = applicationMessage,
                SenderClientId = senderClientId,
                SubscriptionQualityOfServiceLevel = qualityOfServiceLevel,
                IsRetainedMessage = isRetainedMessage,
                RetainAsPublished = false,
                SubscriptionIdentifiers = null,
                EnqueuedAt = MqttTime.UtcNow
            });
        }

        /// <summary>
        /// 以完整参数入队，携带到期时间、RetainAsPublished 与订阅标识符
        /// </summary>
        /// <param name="applicationMessage">应用消息，不可为 null</param>
        /// <param name="senderClientId">发布方标识，可为 null</param>
        /// <param name="qualityOfServiceLevel">本次投递使用的 QoS</param>
        /// <param name="isRetainedMessage">是否为 retained 补发</param>
        /// <param name="expiresAt">消息到期时间，为 null 表示不过期</param>
        /// <param name="retainAsPublished">转发时是否保留发布端的 Retain 标志</param>
        /// <param name="subscriptionIdentifiers">要写入 MQTT 5.0 转发报文的订阅标识符，为 null 表示不带</param>
        public void Enqueue(
            MqttApplicationMessage applicationMessage,
            string senderClientId,
            MqttQualityOfServiceLevel qualityOfServiceLevel,
            bool isRetainedMessage,
            DateTimeOffset? expiresAt,
            bool retainAsPublished,
            IList<uint> subscriptionIdentifiers)
        {
            if (applicationMessage == null) throw new ArgumentNullException(nameof(applicationMessage));

            Enqueue(new MqttQueuedApplicationMessage
            {
                ApplicationMessage = applicationMessage,
                SenderClientId = senderClientId,
                SubscriptionQualityOfServiceLevel = qualityOfServiceLevel,
                IsRetainedMessage = isRetainedMessage,
                ExpiresAt = expiresAt,
                RetainAsPublished = retainAsPublished,
                SubscriptionIdentifiers = subscriptionIdentifiers == null ? null : new List<uint>(subscriptionIdentifiers),
                EnqueuedAt = MqttTime.UtcNow
            });
        }

        /// <summary>
        /// 入队已封装好的消息项；队列达到上限时按溢出策略丢弃新消息或最旧消息
        /// </summary>
        /// <param name="queuedApplicationMessage">待入队的消息项，不可为 null</param>
        public void Enqueue(MqttQueuedApplicationMessage queuedApplicationMessage)
        {
            if (queuedApplicationMessage == null) throw new ArgumentNullException(nameof(queuedApplicationMessage));

            lock (_messageQueue)
            {
                if (_messageQueue.Count >= _options.MaxPendingMessagesPerClient)
                {
                    if (_options.PendingMessagesOverflowStrategy == MqttPendingMessagesOverflowStrategy.DropNewMessage)
                    {
                        return;
                    }

                    if (_options.PendingMessagesOverflowStrategy == MqttPendingMessagesOverflowStrategy.DropOldestQueuedMessage)
                    {
                        _messageQueue.TryDequeue();
                    }
                }

                _messageQueue.Enqueue(queuedApplicationMessage);
            }
        }

        /// <summary>
        /// 异步取出一条消息，取消或队列关闭时返回 null
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>取出的消息项，未能取出时为 null</returns>
        public async Task<MqttQueuedApplicationMessage> DequeueAsync(CancellationToken cancellationToken)
        {
            var dequeueResult = await _messageQueue.TryDequeueAsync(cancellationToken).ConfigureAwait(false);
            if (!dequeueResult.IsSuccess)
            {
                return null;
            }

            return dequeueResult.Item;
        }

        /// <summary>
        /// 清空队列中尚未投递的消息
        /// </summary>
        public void Clear()
        {
            _messageQueue.Clear();
        }

        /// <summary>
        /// 释放底层队列资源
        /// </summary>
        public void Dispose()
        {
            _messageQueue?.Dispose();
        }
    }
}
