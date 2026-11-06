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
*命名空间：SAEA.MQTT.Client.Subscribing
*文件名： MqttClientSubscribeOptionsBuilder
*版本号： v26.4.23.1
*唯一标识：6660f10c-d6fb-42e5-a7bc-ea0837d7de1e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientSubscribeOptionsBuilder接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;
using System;
using System.Collections.Generic;

namespace SAEA.MQTT.Client.Subscribing
{
    /// <summary>
    /// 订阅选项构建器，所有 With 方法均返回自身以支持链式调用
    /// </summary>
    public class MqttClientSubscribeOptionsBuilder
    {
        private readonly MqttClientSubscribeOptions _subscribeOptions = new MqttClientSubscribeOptions();

        /// <summary>
        /// 追加一条 SUBSCRIBE 报文的用户自定义属性
        /// </summary>
        /// <param name="name">属性名，不能为 null</param>
        /// <param name="value">属性值，不能为 null</param>
        /// <returns>构建器自身，便于链式调用</returns>
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

        /// <summary>
        /// 设置订阅标识符，仅 MQTT 5.0 生效
        /// </summary>
        /// <param name="subscriptionIdentifier">订阅标识符，null 表示不设置</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientSubscribeOptionsBuilder WithSubscriptionIdentifier(uint? subscriptionIdentifier)
        {
            _subscribeOptions.SubscriptionIdentifier = subscriptionIdentifier;

            return this;
        }

        /// <summary>
        /// 用显式参数追加一个主题过滤器
        /// </summary>
        /// <param name="topic">主题过滤器，支持 + 与 # 通配符</param>
        /// <param name="qualityOfServiceLevel">订阅的 QoS 等级，默认最多一次</param>
        /// <param name="noLocal">是否不接收本客户端发布的消息，仅 MQTT 5.0 生效，null 表示不携带该选项</param>
        /// <param name="retainAsPublished">转发时是否保留发布端设置的保留标志，仅 MQTT 5.0 生效，null 表示不携带该选项</param>
        /// <param name="retainHandling">订阅时保留消息的发送方式，仅 MQTT 5.0 生效，null 表示不携带该选项</param>
        /// <returns>构建器自身，便于链式调用</returns>
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

        /// <summary>
        /// 通过回调配置并追加一个主题过滤器
        /// </summary>
        /// <param name="topicFilterBuilder">主题过滤器构建回调，不能为 null</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientSubscribeOptionsBuilder WithTopicFilter(Action<MqttTopicFilterBuilder> topicFilterBuilder)
        {
            if (topicFilterBuilder == null) throw new ArgumentNullException(nameof(topicFilterBuilder));

            var internalTopicFilterBuilder = new MqttTopicFilterBuilder();
            topicFilterBuilder(internalTopicFilterBuilder);

            return WithTopicFilter(internalTopicFilterBuilder);
        }

        /// <summary>
        /// 用主题过滤器构建器的当前内容追加一个主题过滤器
        /// </summary>
        /// <param name="topicFilterBuilder">主题过滤器构建器，不能为 null</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientSubscribeOptionsBuilder WithTopicFilter(MqttTopicFilterBuilder topicFilterBuilder)
        {
            if (topicFilterBuilder == null) throw new ArgumentNullException(nameof(topicFilterBuilder));

            return WithTopicFilter(topicFilterBuilder.Build());
        }

        /// <summary>
        /// 追加一个已构造好的主题过滤器
        /// </summary>
        /// <param name="topicFilter">主题过滤器，不能为 null</param>
        /// <returns>构建器自身，便于链式调用</returns>
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

        /// <summary>
        /// 生成订阅选项，不校验主题过滤器是否为空
        /// </summary>
        /// <returns>生成的订阅选项</returns>
        public MqttClientSubscribeOptions Build()
        {
            return _subscribeOptions;
        }
    }
}
