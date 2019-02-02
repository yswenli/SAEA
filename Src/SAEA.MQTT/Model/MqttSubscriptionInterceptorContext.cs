/****************************************************************************
*项目名称：SAEA.MQTT.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Model
*类 名 称：MqttSubscriptionInterceptorContext
*版 本 号： V4.1.2.2
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 10:23:03
*描述：
*=====================================================================
*修改时间：2019/1/15 10:23:03
*修 改 人： yswenli
*版 本 号： V4.1.2.2
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.MQTT.Model
{
    public class MqttSubscriptionInterceptorContext
    {
        public MqttSubscriptionInterceptorContext(string clientId, TopicFilter topicFilter)
        {
            ClientId = clientId;
            TopicFilter = topicFilter ?? throw new ArgumentNullException(nameof(topicFilter));
        }

        public string ClientId { get; }

        public TopicFilter TopicFilter { get; }

        public bool AcceptSubscription { get; set; } = true;

        public bool CloseConnection { get; set; }
    }
}
