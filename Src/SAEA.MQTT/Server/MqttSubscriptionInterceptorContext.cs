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
*文件名： MqttSubscriptionInterceptorContext
*版本号： v26.4.23.1
*唯一标识：59de662f-4400-488e-b2b3-f2903b75f6d3
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttSubscriptionInterceptorContext接口
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MqttSubscriptionInterceptorContext接口
*
*****************************************************************************/
using System.Collections.Generic;

namespace SAEA.MQTT.Server
{
    public class MqttSubscriptionInterceptorContext
    {
        public MqttSubscriptionInterceptorContext(string clientId, MqttTopicFilter topicFilter, IDictionary<object, object> sessionItems)
        {
            ClientId = clientId;
            TopicFilter = topicFilter;
            SessionItems = sessionItems;
        }

        public string ClientId { get; }

        public MqttTopicFilter TopicFilter { get; set; }

        /// <summary>
        /// Gets or sets a key/value collection that can be used to share data within the scope of this session.
        /// </summary>
        public IDictionary<object, object> SessionItems { get; }

        public bool AcceptSubscription { get; set; } = true;

        public bool CloseConnection { get; set; }
    }
}
