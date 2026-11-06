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
*文件名： MqttClientMessageQueueInterceptorContext
*版本号： v26.4.23.1
*唯一标识：57ac623f-ab4d-40eb-911f-0c94c6774554
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientMessageQueueInterceptorContext接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 客户端消息队列入队拦截的上下文，承载收发双方标识、应用消息与授予的订阅 QoS
    /// </summary>
    public class MqttClientMessageQueueInterceptorContext
    {
        /// <summary>
        /// 创建入队拦截上下文
        /// </summary>
        /// <param name="senderClientId">消息发布方标识，服务端自身发布时为服务端 ClientId</param>
        /// <param name="receiverClientId">消息接收方标识</param>
        /// <param name="applicationMessage">待入队的应用消息</param>
        /// <param name="subscriptionQualityOfServiceLevel">接收方订阅授予的 QoS，决定实际投递 QoS</param>
        public MqttClientMessageQueueInterceptorContext(string senderClientId, string receiverClientId, MqttApplicationMessage applicationMessage, MqttQualityOfServiceLevel subscriptionQualityOfServiceLevel)
        {
            SenderClientId = senderClientId;
            ReceiverClientId = receiverClientId;
            ApplicationMessage = applicationMessage;
            SubscriptionQualityOfServiceLevel = subscriptionQualityOfServiceLevel;
        }

        /// <summary>
        /// 消息发布方标识
        /// </summary>
        public string SenderClientId { get; }

        /// <summary>
        /// 消息接收方标识
        /// </summary>
        public string ReceiverClientId { get; }

        /// <summary>
        /// 待入队的应用消息，拦截器中可替换主题与载荷
        /// </summary>
        public MqttApplicationMessage ApplicationMessage { get; set; }

        /// <summary>
        /// 是否允许入队投递，默认 true；置为 false 时丢弃本次投递
        /// </summary>
        public bool AcceptEnqueue { get; set; } = true;

        /// <summary>
        /// 实际投递使用的 QoS，可在此下调或上调授予值
        /// </summary>
        public MqttQualityOfServiceLevel SubscriptionQualityOfServiceLevel { get; set; }
    }
}
