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
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.MQTT.Exceptions;
using SAEA.MQTT.Protocol;
using System;

namespace SAEA.MQTT
{
    /// <summary>
    /// 已废弃的构造器类型，等价于 MqttTopicFilterBuilder，仅为命名兼容保留
    /// </summary>
    [Obsolete("Use MqttTopicFilterBuilder instead. It is just a renamed version to align with general namings in this lib.")]
    public class TopicFilterBuilder : MqttTopicFilterBuilder
    {
    }

    /// <summary>
    /// 订阅过滤器构造器，链式设置主题过滤表达式与服务质量等级
    /// </summary>
    public class MqttTopicFilterBuilder
    {
        MqttQualityOfServiceLevel _qualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce;
        string _topic;

        /// <summary>
        /// 设置订阅的主题过滤表达式
        /// </summary>
        /// <param name="topic">主题过滤表达式，构建时才校验是否为空</param>
        /// <returns>当前构造器，便于链式调用</returns>
        public MqttTopicFilterBuilder WithTopic(string topic)
        {
            _topic = topic;
            return this;
        }

        /// <summary>
        /// 设置订阅的服务质量等级
        /// </summary>
        /// <param name="qualityOfServiceLevel">服务质量等级，不做取值校验</param>
        /// <returns>当前构造器，便于链式调用</returns>
        public MqttTopicFilterBuilder WithQualityOfServiceLevel(MqttQualityOfServiceLevel qualityOfServiceLevel)
        {
            _qualityOfServiceLevel = qualityOfServiceLevel;
            return this;
        }

        /// <summary>
        /// 使用至少一次（QoS 1）订阅
        /// </summary>
        /// <returns>当前构造器，便于链式调用</returns>
        public MqttTopicFilterBuilder WithAtLeastOnceQoS()
        {
            _qualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce;
            return this;
        }

        /// <summary>
        /// 使用至多一次（QoS 0）订阅
        /// </summary>
        /// <returns>当前构造器，便于链式调用</returns>
        public MqttTopicFilterBuilder WithAtMostOnceQoS()
        {
            _qualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce;
            return this;
        }

        /// <summary>
        /// 使用恰好一次（QoS 2）订阅
        /// </summary>
        /// <returns>当前构造器，便于链式调用</returns>
        public MqttTopicFilterBuilder WithExactlyOnceQoS()
        {
            _qualityOfServiceLevel = MqttQualityOfServiceLevel.ExactlyOnce;
            return this;
        }

        /// <summary>
        /// 构建订阅过滤器，仅填充主题与服务质量等级，主题为空时抛 MqttProtocolViolationException
        /// </summary>
        /// <returns>按当前设置生成的订阅过滤器</returns>
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
