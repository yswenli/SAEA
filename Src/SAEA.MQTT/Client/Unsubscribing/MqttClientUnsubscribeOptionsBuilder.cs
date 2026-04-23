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
*命名空间：SAEA.MQTT.Client.Unsubscribing
*文件名： MqttClientUnsubscribeOptionsBuilder
*版本号： v26.4.23.1
*唯一标识：c316af8b-2b49-4458-8e24-8c01ab62c87d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientUnsubscribeOptionsBuilder接口
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MqttClientUnsubscribeOptionsBuilder接口
*
*****************************************************************************/
using SAEA.MQTT.Packets;
using System;
using System.Collections.Generic;

namespace SAEA.MQTT.Client.Unsubscribing
{
    public class MqttClientUnsubscribeOptionsBuilder
    {
        private readonly MqttClientUnsubscribeOptions _unsubscribeOptions = new MqttClientUnsubscribeOptions();

        public MqttClientUnsubscribeOptionsBuilder WithUserProperty(string name, string value)
        {
            if (name is null) throw new ArgumentNullException(nameof(name));
            if (value is null) throw new ArgumentNullException(nameof(value));

            return WithUserProperty(new MqttUserProperty(name, value));
        }

        public MqttClientUnsubscribeOptionsBuilder WithUserProperty(MqttUserProperty userProperty)
        {
            if (userProperty is null) throw new ArgumentNullException(nameof(userProperty));

            if (_unsubscribeOptions.UserProperties is null)
            {
                _unsubscribeOptions.UserProperties = new List<MqttUserProperty>();
            }

            _unsubscribeOptions.UserProperties.Add(userProperty);

            return this;
        }

        public MqttClientUnsubscribeOptionsBuilder WithTopicFilter(string topic)
        {
            if (topic is null) throw new ArgumentNullException(nameof(topic));

            if (_unsubscribeOptions.TopicFilters is null)
            {
                _unsubscribeOptions.TopicFilters = new List<string>();
            }

            _unsubscribeOptions.TopicFilters.Add(topic);

            return this;
        }

        public MqttClientUnsubscribeOptionsBuilder WithTopicFilter(MqttTopicFilter topicFilter)
        {
            if (topicFilter is null) throw new ArgumentNullException(nameof(topicFilter));

            return WithTopicFilter(topicFilter.Topic);
        }

        public MqttClientUnsubscribeOptions Build()
        {
            return _unsubscribeOptions;
        }
    }
}
