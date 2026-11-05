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
 *文件名： MqttClientSubscriptionsManager
 *版本号： v26.4.23.1
 *唯一标识：1e190bc7-14a8-493f-abd9-7bf12afcee4a
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2021/03/11 16:46:45
 *描述：MqttClientSubscriptionsManager接口
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/05 00:00:00
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：补齐共享订阅解析、匹配契约与注册表接入
 *
 *****************************************************************************/
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    public sealed class MqttClientSubscriptionsManager
    {
        // We use a reader writer lock because the subscriptions are only read most of the time.
        // Since writing is done by multiple threads (server API or connection thread), we cannot avoid locking
        // completely by swapping references etc.
        readonly ReaderWriterLockSlim _subscriptionsLock = new ReaderWriterLockSlim();
        readonly Dictionary<string, MqttTopicFilter> _subscriptions = new Dictionary<string, MqttTopicFilter>();
        readonly MqttClientSession _clientSession;
        readonly IMqttServerOptions _options;
        readonly MqttServerEventDispatcher _eventDispatcher;
        readonly MqttSharedSubscriptionRegistry _sharedSubscriptionRegistry;

        public MqttClientSubscriptionsManager(
            MqttClientSession clientSession,
            MqttServerEventDispatcher eventDispatcher,
            IMqttServerOptions serverOptions)
            : this(clientSession, eventDispatcher, serverOptions, new MqttSharedSubscriptionRegistry())
        {
        }

        internal MqttClientSubscriptionsManager(
            MqttClientSession clientSession,
            MqttServerEventDispatcher eventDispatcher,
            IMqttServerOptions serverOptions,
            MqttSharedSubscriptionRegistry sharedSubscriptionRegistry)
        {
            _clientSession = clientSession ?? throw new ArgumentNullException(nameof(clientSession));
            _options = serverOptions ?? throw new ArgumentNullException(nameof(serverOptions));
            _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
            _sharedSubscriptionRegistry = sharedSubscriptionRegistry ?? throw new ArgumentNullException(nameof(sharedSubscriptionRegistry));
        }

        public async Task<MqttClientSubscribeResult> SubscribeAsync(MqttSubscribePacket subscribePacket, MqttConnectPacket connectPacket)
        {
            if (subscribePacket == null) throw new ArgumentNullException(nameof(subscribePacket));
            if (connectPacket == null) throw new ArgumentNullException(nameof(connectPacket));

            var result = new MqttClientSubscribeResult();

            var isV5 = _clientSession.IsV5;
            var capabilities = _options.Capabilities ?? new MqttServerCapabilities();

            // 订阅标识符是整包属性：不支持时属于协议错误，整包拒绝且不发送 SUBACK。
            if (isV5 &&
                subscribePacket.Properties?.SubscriptionIdentifier != null &&
                !capabilities.SubscriptionIdentifiersAvailable)
            {
                result.CloseConnection = true;
                result.DisconnectReasonCode = MqttDisconnectReasonCode.SubscriptionIdentifiersNotSupported;
                return result;
            }

            // Subscription Identifier 为 0 是协议错误 [MQTT-3.8.2.1.2]。
            if (isV5 && subscribePacket.Properties?.SubscriptionIdentifier == 0)
            {
                result.CloseConnection = true;
                result.DisconnectReasonCode = MqttDisconnectReasonCode.ProtocolError;
                return result;
            }

            foreach (var originalTopicFilter in subscribePacket.TopicFilters)
            {
                var interceptorContext = await InterceptSubscribeAsync(originalTopicFilter).ConfigureAwait(false);

                var finalTopicFilter = interceptorContext.TopicFilter;

                if (interceptorContext.CloseConnection)
                {
                    result.CloseConnection = true;
                }

                if (finalTopicFilter == null || string.IsNullOrEmpty(finalTopicFilter.Topic) || !interceptorContext.AcceptSubscription)
                {
                    result.ReturnCodes.Add(MqttSubscribeReturnCode.Failure);
                    result.ReasonCodes.Add(MqttSubscribeReasonCode.UnspecifiedError);
                    continue;
                }

                if (isV5)
                {
                    if (finalTopicFilter.Topic.StartsWith("$share/", StringComparison.Ordinal))
                    {
                        string shareName;
                        string parsedFilter;
                        if (!MqttTopicFilter.TryParseShared(finalTopicFilter.Topic, out shareName, out parsedFilter))
                        {
                            result.ReturnCodes.Add(MqttSubscribeReturnCode.Failure);
                            result.ReasonCodes.Add(MqttSubscribeReasonCode.TopicFilterInvalid);
                            continue;
                        }

                        if (finalTopicFilter.NoLocal == true)
                        {
                            // [MQTT-3.8.3-5] 共享订阅的 NoLocal 是协议错误。
                            result.CloseConnection = true;
                            result.DisconnectReasonCode = MqttDisconnectReasonCode.ProtocolError;
                            return result;
                        }

                        if (!capabilities.SharedSubscriptionAvailable)
                        {
                            result.ReturnCodes.Add(MqttSubscribeReturnCode.Failure);
                            result.ReasonCodes.Add(MqttSubscribeReasonCode.SharedSubscriptionsNotSupported);
                            continue;
                        }

                        // 共享过滤器内部的通配符同样受 Wildcard Subscription Available 约束。
                        if (!capabilities.WildcardSubscriptionAvailable && ContainsWildcard(parsedFilter))
                        {
                            result.ReturnCodes.Add(MqttSubscribeReturnCode.Failure);
                            result.ReasonCodes.Add(MqttSubscribeReasonCode.WildcardSubscriptionsNotSupported);
                            continue;
                        }

                        finalTopicFilter.ShareName = shareName;
                        finalTopicFilter.ParsedFilter = parsedFilter;
                        finalTopicFilter.SubscriptionIdentifier = subscribePacket.Properties?.SubscriptionIdentifier;
                    }
                    else
                    {
                        if (!capabilities.WildcardSubscriptionAvailable && ContainsWildcard(finalTopicFilter.Topic))
                        {
                            result.ReturnCodes.Add(MqttSubscribeReturnCode.Failure);
                            result.ReasonCodes.Add(MqttSubscribeReasonCode.WildcardSubscriptionsNotSupported);
                            continue;
                        }

                        finalTopicFilter.SubscriptionIdentifier = subscribePacket.Properties?.SubscriptionIdentifier;
                    }
                }

                // 服务端授予的 QoS 不得超过 Maximum QoS（允许下调）。
                if (isV5 && (int)finalTopicFilter.QualityOfServiceLevel > (int)capabilities.MaximumQoS)
                {
                    finalTopicFilter.QualityOfServiceLevel = capabilities.MaximumQoS;
                }

                result.ReturnCodes.Add(ConvertToSubscribeReturnCode(finalTopicFilter.QualityOfServiceLevel));
                result.ReasonCodes.Add(ConvertToSubscribeReasonCode(finalTopicFilter.QualityOfServiceLevel));

                // 存储键保持原始 Topic（包括 $share 前缀），以保证 UNSUBSCRIBE 精确匹配。
                bool existed;
                _subscriptionsLock.EnterWriteLock();
                try
                {
                    existed = _subscriptions.ContainsKey(finalTopicFilter.Topic);
                    _subscriptions[finalTopicFilter.Topic] = finalTopicFilter;
                }
                finally
                {
                    _subscriptionsLock.ExitWriteLock();
                }

                if (isV5 && finalTopicFilter.IsShared)
                {
                    _sharedSubscriptionRegistry.Add(
                        finalTopicFilter.ShareName,
                        finalTopicFilter.ParsedFilter,
                        CreateSharedSubscriptionMember(finalTopicFilter));
                }

                if (ShouldReplayRetainedMessages(isV5, finalTopicFilter, existed))
                {
                    result.RetainedMessageFilters.Add(finalTopicFilter);
                }

                await _eventDispatcher.SafeNotifyClientSubscribedTopicAsync(_clientSession.ClientId, finalTopicFilter).ConfigureAwait(false);
            }

            return result;
        }

        public async Task<List<MqttTopicFilter>> SubscribeAsync(IEnumerable<MqttTopicFilter> topicFilters)
        {
            if (topicFilters == null) throw new ArgumentNullException(nameof(topicFilters));

            var isV5 = _clientSession.IsV5;
            var replayFilters = new List<MqttTopicFilter>();

            foreach (var topicFilter in topicFilters)
            {
                if (topicFilter == null)
                {
                    continue;
                }

                var interceptorContext = await InterceptSubscribeAsync(topicFilter).ConfigureAwait(false);
                if (!interceptorContext.AcceptSubscription)
                {
                    continue;
                }

                if (isV5 && topicFilter.Topic != null && topicFilter.Topic.StartsWith("$share/", StringComparison.Ordinal))
                {
                    string shareName;
                    string parsedFilter;
                    if (!MqttTopicFilter.TryParseShared(topicFilter.Topic, out shareName, out parsedFilter))
                    {
                        // 服务端 API 没有 SUBACK，只能忽略非法共享过滤器。
                        continue;
                    }

                    topicFilter.ShareName = shareName;
                    topicFilter.ParsedFilter = parsedFilter;
                }

                bool existed;
                _subscriptionsLock.EnterWriteLock();
                try
                {
                    existed = _subscriptions.ContainsKey(topicFilter.Topic);
                    _subscriptions[topicFilter.Topic] = topicFilter;
                }
                finally
                {
                    _subscriptionsLock.ExitWriteLock();
                }

                if (isV5 && topicFilter.IsShared)
                {
                    _sharedSubscriptionRegistry.Add(
                        topicFilter.ShareName,
                        topicFilter.ParsedFilter,
                        CreateSharedSubscriptionMember(topicFilter));
                }

                if (ShouldReplayRetainedMessages(isV5, topicFilter, existed))
                {
                    replayFilters.Add(topicFilter);
                }

                await _eventDispatcher.SafeNotifyClientSubscribedTopicAsync(_clientSession.ClientId, topicFilter).ConfigureAwait(false);
            }

            return replayFilters;
        }

        public async Task<List<MqttUnsubscribeReasonCode>> UnsubscribeAsync(MqttUnsubscribePacket unsubscribePacket)
        {
            if (unsubscribePacket == null) throw new ArgumentNullException(nameof(unsubscribePacket));

            var reasonCodes = new List<MqttUnsubscribeReasonCode>();

            foreach (var topicFilter in unsubscribePacket.TopicFilters)
            {
                var interceptorContext = await InterceptUnsubscribeAsync(topicFilter).ConfigureAwait(false);
                if (!interceptorContext.AcceptUnsubscription)
                {
                    reasonCodes.Add(MqttUnsubscribeReasonCode.ImplementationSpecificError);
                    continue;
                }

                MqttTopicFilter removedSubscription = null;
                _subscriptionsLock.EnterWriteLock();
                try
                {
                    if (_subscriptions.TryGetValue(topicFilter, out removedSubscription))
                    {
                        _subscriptions.Remove(topicFilter);
                        reasonCodes.Add(MqttUnsubscribeReasonCode.Success);
                    }
                    else
                    {
                        reasonCodes.Add(MqttUnsubscribeReasonCode.NoSubscriptionExisted);
                    }
                }
                finally
                {
                    _subscriptionsLock.ExitWriteLock();
                }

                RemoveSharedSubscription(removedSubscription);
            }

            foreach (var topicFilter in unsubscribePacket.TopicFilters)
            {
                await _eventDispatcher.SafeNotifyClientUnsubscribedTopicAsync(_clientSession.ClientId, topicFilter).ConfigureAwait(false);
            }

            return reasonCodes;
        }

        public async Task UnsubscribeAsync(IEnumerable<string> topicFilters)
        {
            if (topicFilters == null) throw new ArgumentNullException(nameof(topicFilters));

            foreach (var topicFilter in topicFilters)
            {
                var interceptorContext = await InterceptUnsubscribeAsync(topicFilter).ConfigureAwait(false);
                if (!interceptorContext.AcceptUnsubscription)
                {
                    continue;
                }

                MqttTopicFilter removedSubscription = null;
                _subscriptionsLock.EnterWriteLock();
                try
                {
                    if (topicFilter != null && _subscriptions.TryGetValue(topicFilter, out removedSubscription))
                    {
                        _subscriptions.Remove(topicFilter);
                    }
                    else
                    {
                        removedSubscription = null;
                    }
                }
                finally
                {
                    _subscriptionsLock.ExitWriteLock();
                }

                RemoveSharedSubscription(removedSubscription);
            }
        }

        public CheckSubscriptionsResult CheckSubscriptions(string topic, MqttQualityOfServiceLevel qosLevel)
        {
            return CheckSubscriptions(topic, qosLevel, null);
        }

        public CheckSubscriptionsResult CheckSubscriptions(string topic, MqttQualityOfServiceLevel qosLevel, string publisherClientId)
        {
            Dictionary<string, MqttTopicFilter> subscriptionsCopy;
            _subscriptionsLock.EnterReadLock();
            try
            {
                subscriptionsCopy = new Dictionary<string, MqttTopicFilter>(_subscriptions);
            }
            finally
            {
                _subscriptionsLock.ExitReadLock();
            }

            var matches = new List<MqttSubscriptionMatch>();

            foreach (var subscription in subscriptionsCopy)
            {
                var filter = subscription.Value;
                if (filter == null)
                {
                    continue;
                }

                // 共享订阅由注册表在分发阶段处理，不参与本会话的本地匹配。
                if (filter.ShareName != null)
                {
                    continue;
                }

                if (filter.NoLocal == true &&
                    publisherClientId != null &&
                    string.Equals(publisherClientId, _clientSession.ClientId, StringComparison.Ordinal))
                {
                    continue;
                }

                var effectiveFilter = filter.ParsedFilter ?? filter.Topic;
                if (string.IsNullOrEmpty(effectiveFilter) || !MqttTopicFilterComparer.IsMatch(topic, effectiveFilter))
                {
                    continue;
                }

                matches.Add(new MqttSubscriptionMatch
                {
                    Filter = effectiveFilter,
                    QualityOfServiceLevel = Min(qosLevel, filter.QualityOfServiceLevel),
                    NoLocal = filter.NoLocal == true,
                    RetainAsPublished = filter.RetainAsPublished == true,
                    RetainHandling = filter.RetainHandling ?? MqttRetainHandling.SendAtSubscribe,
                    SubscriptionIdentifier = filter.SubscriptionIdentifier,
                    IsShared = false,
                    ShareName = null
                });
            }

            if (matches.Count == 0)
            {
                return new CheckSubscriptionsResult
                {
                    IsSubscribed = false,
                    Matches = new List<MqttSubscriptionMatch>()
                };
            }

            return new CheckSubscriptionsResult
            {
                IsSubscribed = true,
                QualityOfServiceLevel = matches.Max(m => m.QualityOfServiceLevel),
                Matches = matches
            };
        }

        MqttSharedSubscriptionMember CreateSharedSubscriptionMember(MqttTopicFilter topicFilter)
        {
            return new MqttSharedSubscriptionMember
            {
                ClientId = _clientSession.ClientId,
                QualityOfServiceLevel = topicFilter.QualityOfServiceLevel,
                SubscriptionIdentifier = topicFilter.SubscriptionIdentifier,
                RetainAsPublished = topicFilter.RetainAsPublished == true
            };
        }

        void RemoveSharedSubscription(MqttTopicFilter subscription)
        {
            if (subscription == null || subscription.ShareName == null)
            {
                return;
            }

            _sharedSubscriptionRegistry.Remove(
                subscription.ShareName,
                subscription.ParsedFilter,
                _clientSession.ClientId);
        }

        static bool ShouldReplayRetainedMessages(bool isV5, MqttTopicFilter filter, bool existed)
        {
            if (!isV5)
            {
                // V3 保持现状：所有成功订阅的过滤器都参与 retained 补发。
                return true;
            }

            // 共享订阅的新成员不补发 retained 消息。
            if (filter.ShareName != null)
            {
                return false;
            }

            var retainHandling = filter.RetainHandling ?? MqttRetainHandling.SendAtSubscribe;
            switch (retainHandling)
            {
                case MqttRetainHandling.SendAtSubscribe:
                    return true;
                case MqttRetainHandling.SendAtSubscribeIfNewSubscriptionOnly:
                    return !existed;
                case MqttRetainHandling.DoNotSendOnSubscribe:
                default:
                    return false;
            }
        }

        static bool ContainsWildcard(string topic)
        {
            return topic.IndexOf('+') >= 0 || topic.IndexOf('#') >= 0;
        }

        static MqttQualityOfServiceLevel Min(MqttQualityOfServiceLevel left, MqttQualityOfServiceLevel right)
        {
            return (int)left <= (int)right ? left : right;
        }

        static MqttSubscribeReturnCode ConvertToSubscribeReturnCode(MqttQualityOfServiceLevel qualityOfServiceLevel)
        {
            return (MqttSubscribeReturnCode)(int)qualityOfServiceLevel;
        }

        static MqttSubscribeReasonCode ConvertToSubscribeReasonCode(MqttQualityOfServiceLevel qualityOfServiceLevel)
        {
            return (MqttSubscribeReasonCode)(int)qualityOfServiceLevel;
        }

        async Task<MqttSubscriptionInterceptorContext> InterceptSubscribeAsync(MqttTopicFilter topicFilter)
        {
            var context = new MqttSubscriptionInterceptorContext(_clientSession.ClientId, topicFilter, _clientSession.Items);
            if (_options.SubscriptionInterceptor != null)
            {
                await _options.SubscriptionInterceptor.InterceptSubscriptionAsync(context).ConfigureAwait(false);
            }

            return context;
        }

        async Task<MqttUnsubscriptionInterceptorContext> InterceptUnsubscribeAsync(string topicFilter)
        {
            var context = new MqttUnsubscriptionInterceptorContext(_clientSession.ClientId, topicFilter, _clientSession.Items);
            if (_options.UnsubscriptionInterceptor != null)
            {
                await _options.UnsubscriptionInterceptor.InterceptUnsubscriptionAsync(context).ConfigureAwait(false);
            }

            return context;
        }
    }
}