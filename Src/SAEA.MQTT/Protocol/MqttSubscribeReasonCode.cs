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
*命名空间：SAEA.MQTT.Protocol
*文件名： MqttSubscribeReasonCode
*版本号： v26.4.23.1
*唯一标识：44b23491-5ad0-425c-acea-8fa020b4e20a
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttSubscribeReasonCode接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.MQTT.Protocol
{
    /// <summary>
    /// MQTT 5.0 订阅确认（SUBACK）中每个订阅请求的原因码
    /// </summary>
    public enum MqttSubscribeReasonCode
    {
        /// <summary>
        /// 订阅成功，授予的最大 QoS 为 0
        /// </summary>
        GrantedQoS0 = 0,
        /// <summary>
        /// 订阅成功，授予的最大 QoS 为 1
        /// </summary>
        GrantedQoS1 = 1,
        /// <summary>
        /// 订阅成功，授予的最大 QoS 为 2
        /// </summary>
        GrantedQoS2 = 2,
        /// <summary>
        /// 未指明错误
        /// </summary>
        UnspecifiedError = 128,
        /// <summary>
        /// 订阅有效但当前实现不接受
        /// </summary>
        ImplementationSpecificError = 131,
        /// <summary>
        /// 未通过授权
        /// </summary>
        NotAuthorized = 135,
        /// <summary>
        /// 主题过滤器格式不合法
        /// </summary>
        TopicFilterInvalid = 143,
        /// <summary>
        /// 报文标识符已被占用
        /// </summary>
        PacketIdentifierInUse = 145,
        /// <summary>
        /// 配额超限
        /// </summary>
        QuotaExceeded = 151,
        /// <summary>
        /// 不支持共享订阅
        /// </summary>
        SharedSubscriptionsNotSupported = 158,
        /// <summary>
        /// 不支持订阅标识符
        /// </summary>
        SubscriptionIdentifiersNotSupported = 161,
        /// <summary>
        /// 不支持通配符订阅
        /// </summary>
        WildcardSubscriptionsNotSupported = 162
    }
}
