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
*文件名： MqttClientSession
*版本号： v26.4.23.1
*唯一标识：193ee43d-86f2-475e-a608-268f9865822e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using SAEA.MQTT.Diagnostics;
using SAEA.MQTT.Server.Status;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    public sealed class MqttClientSession
    {
        readonly IMqttNetScopedLogger _logger;

        readonly DateTime _createdTimestamp = DateTime.UtcNow;
        readonly IMqttRetainedMessagesManager _retainedMessagesManager;

        public MqttClientSession(
            string clientId,
            IDictionary<object, object> items,
            MqttServerEventDispatcher eventDispatcher,
            IMqttServerOptions serverOptions, 
            IMqttRetainedMessagesManager retainedMessagesManager,
            IMqttNetLogger logger)
        {
            ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            Items = items ?? throw new ArgumentNullException(nameof(items));
            _retainedMessagesManager = retainedMessagesManager ?? throw new ArgumentNullException(nameof(retainedMessagesManager));
            SubscriptionsManager = new MqttClientSubscriptionsManager(this, eventDispatcher, serverOptions);
            ApplicationMessagesQueue = new MqttClientSessionApplicationMessagesQueue(serverOptions);

            if (logger == null) throw new ArgumentNullException(nameof(logger));
            _logger = logger.CreateScopedLogger(nameof(MqttClientSession));
        }

        public string ClientId { get; }

        public bool IsCleanSession { get; set; } = true;

        public MqttApplicationMessage WillMessage { get; set; }

        public MqttClientSubscriptionsManager SubscriptionsManager { get; }

        public MqttClientSessionApplicationMessagesQueue ApplicationMessagesQueue { get; }

        /// <summary>
        /// Gets or sets a key/value collection that can be used to share data within the scope of this session.
        /// </summary>
        public IDictionary<object, object> Items { get; }

        public bool EnqueueApplicationMessage(MqttApplicationMessage applicationMessage, string senderClientId, bool isRetainedApplicationMessage)
        {
            var checkSubscriptionsResult = SubscriptionsManager.CheckSubscriptions(applicationMessage.Topic, applicationMessage.QualityOfServiceLevel);
            if (!checkSubscriptionsResult.IsSubscribed)
            {
                return false;
            }

            _logger.Verbose("Queued application message with topic '{0}' (ClientId: {1}).", applicationMessage.Topic, ClientId);

            ApplicationMessagesQueue.Enqueue(applicationMessage, senderClientId, checkSubscriptionsResult.QualityOfServiceLevel, isRetainedApplicationMessage);

            return true;
        }

        public async Task SubscribeAsync(ICollection<MqttTopicFilter> topicFilters)
        {
            if (topicFilters is null) throw new ArgumentNullException(nameof(topicFilters));

            await SubscriptionsManager.SubscribeAsync(topicFilters).ConfigureAwait(false);

            var matchingRetainedMessages = await _retainedMessagesManager.GetSubscribedMessagesAsync(topicFilters).ConfigureAwait(false);
            foreach (var matchingRetainedMessage in matchingRetainedMessages)
            {
                EnqueueApplicationMessage(matchingRetainedMessage, null, true);
            }
        }

        public Task UnsubscribeAsync(IEnumerable<string> topicFilters)
        {
            if (topicFilters is null) throw new ArgumentNullException(nameof(topicFilters));

            return SubscriptionsManager.UnsubscribeAsync(topicFilters);
        }

        public void FillStatus(MqttSessionStatus status)
        {
            status.ClientId = ClientId;
            status.CreatedTimestamp = _createdTimestamp;
            status.PendingApplicationMessagesCount = ApplicationMessagesQueue.Count;
            status.Items = Items;
        }
    }
}