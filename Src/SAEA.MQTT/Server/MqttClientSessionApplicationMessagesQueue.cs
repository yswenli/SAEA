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
*唯一标识：2e67aeb7-eb2a-41fc-9293-83eeff050f0f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MQTT服务端类
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MQTT服务端类
*
*****************************************************************************/
using SAEA.MQTT.Internal;
using SAEA.MQTT.Protocol;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    public sealed class MqttClientSessionApplicationMessagesQueue : IDisposable
    {
        readonly AsyncQueue<MqttQueuedApplicationMessage> _messageQueue = new AsyncQueue<MqttQueuedApplicationMessage>();

        readonly IMqttServerOptions _options;

        public MqttClientSessionApplicationMessagesQueue(IMqttServerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public int Count => _messageQueue.Count;

        public void Enqueue(MqttApplicationMessage applicationMessage, string senderClientId, MqttQualityOfServiceLevel qualityOfServiceLevel, bool isRetainedMessage)
        {
            if (applicationMessage == null) throw new ArgumentNullException(nameof(applicationMessage));

            Enqueue(new MqttQueuedApplicationMessage
            {
                ApplicationMessage = applicationMessage,
                SenderClientId = senderClientId,
                SubscriptionQualityOfServiceLevel = qualityOfServiceLevel,
                IsRetainedMessage = isRetainedMessage
            });
        }

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

        public async Task<MqttQueuedApplicationMessage> DequeueAsync(CancellationToken cancellationToken)
        {
            var dequeueResult = await _messageQueue.TryDequeueAsync(cancellationToken).ConfigureAwait(false);
            if (!dequeueResult.IsSuccess)
            {
                return null;
            }

            return dequeueResult.Item;
        }

        public void Clear()
        {
            _messageQueue.Clear();
        }

        public void Dispose()
        {
            _messageQueue?.Dispose();
        }
    }
}
