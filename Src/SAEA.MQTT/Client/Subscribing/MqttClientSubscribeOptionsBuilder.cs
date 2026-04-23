/****************************************************************************
 * 
   ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| _f 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
   ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ _f 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MQTT.Client.Subscribing
*文件名： MqttClientSubscribeOptionsBuilder.cs
*版本号： v26.4.23.1
*唯一标识：b37a99f5-05c6-4f84-bba7-eb9e0e166c98
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/3/11 16:46:45
*描述：MQTT客户端订阅
*
*=====================================================================
*修改标记
*修改时间：2021/3/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MQTT客户端订阅
*
*****************************************************************************/

using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;
using System;
using System.Collections.Generic;

namespace SAEA.MQTT.Client.Subscribing
{
    public class MqttClientSubscribeOptionsBuilder
    {
        private readonly MqttClientSubscribeOptions _subscribeOptions = new MqttClientSubscribeOptions();

        public MqttClientSubscribeOptionsBuilder WithUserProperty(string name, string value)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (value == null) throw new ArgumentNullException(nameof(value));

            if (_subscribeOptions.UserProperties == null)
            {
                _subscribeOptions.UserProperties = new List<MqttUserProperty>();
            }

            _subscribeOptions.UserProperties.Add(new MqttUserProperty(name, value));

            return this;
        }

        public MqttClientSubscribeOptionsBuilder WithSubscriptionIdentifier(uint? subscriptionIdentifier)
        {
            _subscribeOptions.SubscriptionIdentifier = subscriptionIdentifier;

            return this;
        }

        public MqttClientSubscribeOptionsBuilder WithTopicFilter(
            string topic,
            MqttQualityOfServiceLevel qualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
            bool? noLocal = null,
            bool? retainAsPublished = null,
            MqttRetainHandling? retainHandling = null)
        {
            return WithTopicFilter(new MqttTopicFilter
            {
                Topic = topic,
                QualityOfServiceLevel = qualityOfServiceLevel,
                NoLocal = noLocal,
                RetainAsPublished = retainAsPublished,
                RetainHandling = retainHandling
            });
        }

        public MqttClientSubscribeOptionsBuilder WithTopicFilter(Action<MqttTopicFilterBuilder> topicFilterBuilder)
        {
            if (topicFilterBuilder == null) throw new ArgumentNullException(nameof(topicFilterBuilder));

            var internalTopicFilterBuilder = new MqttTopicFilterBuilder();
            topicFilterBuilder(internalTopicFilterBuilder);

            return WithTopicFilter(internalTopicFilterBuilder);
        }

        public MqttClientSubscribeOptionsBuilder WithTopicFilter(MqttTopicFilterBuilder topicFilterBuilder)
        {
            if (topicFilterBuilder == null) throw new ArgumentNullException(nameof(topicFilterBuilder));

            return WithTopicFilter(topicFilterBuilder.Build());
        }

        public MqttClientSubscribeOptionsBuilder WithTopicFilter(MqttTopicFilter topicFilter)
        {
            if (topicFilter == null) throw new ArgumentNullException(nameof(topicFilter));

            if (_subscribeOptions.TopicFilters == null)
            {
                _subscribeOptions.TopicFilters = new List<MqttTopicFilter>();
            }

            _subscribeOptions.TopicFilters.Add(topicFilter);

            return this;
        }

        public MqttClientSubscribeOptions Build()
        {
            return _subscribeOptions;
        }
    }
}
