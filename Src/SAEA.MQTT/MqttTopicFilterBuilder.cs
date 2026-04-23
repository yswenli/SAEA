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
*命名空间：SAEA.MQTT
*文件名： MqttTopicFilterBuilder
*版本号： v26.4.23.1
*唯一标识：6afbc76d-8c94-4181-ad3c-92aa168e61c8
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttTopicFilterBuilder接口
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MqttTopicFilterBuilder接口
*
*****************************************************************************/
using SAEA.MQTT.Exceptions;
using SAEA.MQTT.Protocol;
using System;

namespace SAEA.MQTT
{
    [Obsolete("Use MqttTopicFilterBuilder instead. It is just a renamed version to align with general namings in this lib.")]
    public class TopicFilterBuilder : MqttTopicFilterBuilder
    {
    }

    public class MqttTopicFilterBuilder
    {
        MqttQualityOfServiceLevel _qualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce;
        string _topic;

        public MqttTopicFilterBuilder WithTopic(string topic)
        {
            _topic = topic;
            return this;
        }

        public MqttTopicFilterBuilder WithQualityOfServiceLevel(MqttQualityOfServiceLevel qualityOfServiceLevel)
        {
            _qualityOfServiceLevel = qualityOfServiceLevel;
            return this;
        }

        public MqttTopicFilterBuilder WithAtLeastOnceQoS()
        {
            _qualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce;
            return this;
        }

        public MqttTopicFilterBuilder WithAtMostOnceQoS()
        {
            _qualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce;
            return this;
        }

        public MqttTopicFilterBuilder WithExactlyOnceQoS()
        {
            _qualityOfServiceLevel = MqttQualityOfServiceLevel.ExactlyOnce;
            return this;
        }

        public MqttTopicFilter Build()
        {
            if (string.IsNullOrEmpty(_topic))
            {
                throw new MqttProtocolViolationException("Topic is not set.");
            }

            return new MqttTopicFilter { Topic = _topic, QualityOfServiceLevel = _qualityOfServiceLevel };
        }
    }
}
