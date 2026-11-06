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
*文件名： MqttClientSubscribeOptions
*版本号： v26.4.23.1
*唯一标识：5469fe33-a07d-4121-8bd6-4d39247a888e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientSubscribeOptions接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System.Collections.Generic;
using SAEA.MQTT.Packets;

namespace SAEA.MQTT.Client.Subscribing
{
    /// <summary>
    /// 订阅选项，对应 SUBSCRIBE 报文的主题过滤器列表与属性
    /// </summary>
    public class MqttClientSubscribeOptions
    {
        /// <summary>
        /// 要订阅的主题过滤器列表，默认是空列表，调用方需自行填充
        /// </summary>
        public List<MqttTopicFilter> TopicFilters { get; set; } = new List<MqttTopicFilter>();

        /// <summary>
        /// 订阅标识符，服务端会在匹配消息的属性中回带，仅 MQTT 5.0 支持，null 表示不设置
        /// </summary>
        public uint? SubscriptionIdentifier { get; set; }

        /// <summary>
        /// SUBSCRIBE 报文携带的用户自定义属性，null 表示不携带
        /// </summary>
        public List<MqttUserProperty> UserProperties { get; set; }
    }
}
