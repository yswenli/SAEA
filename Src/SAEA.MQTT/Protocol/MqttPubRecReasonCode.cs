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
*文件名： MqttPubRecReasonCode
*版本号： v26.4.23.1
*唯一标识：dae7235c-b30a-4036-bdf2-e545b69ca442
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttPubRecReasonCode代码枚举
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
    /// MQTT 5.0 QoS 2 发布响应（PUBREC）原因码
    /// </summary>
    public enum MqttPubRecReasonCode
    {
        /// <summary>
        /// 消息已被服务端接受
        /// </summary>
        Success = 0,
        /// <summary>
        /// 消息已接受但没有匹配的订阅者
        /// </summary>
        NoMatchingSubscribers = 16,
        /// <summary>
        /// 未指明错误
        /// </summary>
        UnspecifiedError = 128,
        /// <summary>
        /// 报文有效但当前实现不接受
        /// </summary>
        ImplementationSpecificError = 131,
        /// <summary>
        /// 未通过授权
        /// </summary>
        NotAuthorized = 135,
        /// <summary>
        /// 主题名格式不合法
        /// </summary>
        TopicNameInvalid = 144,
        /// <summary>
        /// 报文标识符已被占用
        /// </summary>
        PacketIdentifierInUse = 145,
        /// <summary>
        /// 配额超限
        /// </summary>
        QuotaExceeded = 151,
        /// <summary>
        /// 负载与声明的负载格式不符
        /// </summary>
        PayloadFormatInvalid = 153
    }
}
