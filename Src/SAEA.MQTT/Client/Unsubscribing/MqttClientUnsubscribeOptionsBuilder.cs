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
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.MQTT.Packets;
using System;
using System.Collections.Generic;

namespace SAEA.MQTT.Client.Unsubscribing
{
    /// <summary>
    /// 取消订阅选项构建器，所有 With 方法均返回自身以支持链式调用
    /// </summary>
    public class MqttClientUnsubscribeOptionsBuilder
    {
        private readonly MqttClientUnsubscribeOptions _unsubscribeOptions = new MqttClientUnsubscribeOptions();

        /// <summary>
        /// 按名称与值追加一条 UNSUBSCRIBE 报文的用户自定义属性
        /// </summary>
        /// <param name="name">属性名，不能为 null</param>
        /// <param name="value">属性值，不能为 null</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientUnsubscribeOptionsBuilder WithUserProperty(string name, string value)
        {
            if (name is null) throw new ArgumentNullException(nameof(name));
            if (value is null) throw new ArgumentNullException(nameof(value));

            return WithUserProperty(new MqttUserProperty(name, value));
        }

        /// <summary>
        /// 追加一条已构造好的用户自定义属性
        /// </summary>
        /// <param name="userProperty">用户自定义属性，不能为 null</param>
        /// <returns>构建器自身，便于链式调用</returns>
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

        /// <summary>
        /// 追加一个要取消订阅的主题过滤器
        /// </summary>
        /// <param name="topic">主题过滤器，不能为 null</param>
        /// <returns>构建器自身，便于链式调用</returns>
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

        /// <summary>
        /// 用主题过滤器对象追加一个要取消订阅的主题，只取其主题字符串
        /// </summary>
        /// <param name="topicFilter">主题过滤器，不能为 null</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientUnsubscribeOptionsBuilder WithTopicFilter(MqttTopicFilter topicFilter)
        {
            if (topicFilter is null) throw new ArgumentNullException(nameof(topicFilter));

            return WithTopicFilter(topicFilter.Topic);
        }

        /// <summary>
        /// 生成取消订阅选项，不校验主题过滤器是否为空
        /// </summary>
        /// <returns>生成的取消订阅选项</returns>
        public MqttClientUnsubscribeOptions Build()
        {
            return _unsubscribeOptions;
        }
    }
}
