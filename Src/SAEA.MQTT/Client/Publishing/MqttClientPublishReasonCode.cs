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
*命名空间：SAEA.MQTT.Client.Publishing
*文件名： MqttClientPublishReasonCode
*版本号： v26.4.23.1
*唯一标识：0198a04c-58f8-454e-84a8-3d0102d1109a
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientPublishReasonCode接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.MQTT.Client.Publishing
{
    /// <summary>
    /// MQTT 5.0 发布确认（PUBACK / PUBREC / PUBREL / PUBCOMP）原因码
    /// </summary>
    public enum MqttClientPublishReasonCode
    {
        /// <summary>
        /// 发布成功，消息已被接受
        /// </summary>
        Success = 0,
        /// <summary>
        /// 服务端已接受消息，但当前没有任何订阅匹配该主题
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
        /// 客户端未通过发布授权
        /// </summary>
        NotAuthorized = 135,
        /// <summary>
        /// 主题名不合法或不匹配任何订阅
        /// </summary>
        TopicNameInvalid = 144,
        /// <summary>
        /// 报文标识符已被其他在途报文占用
        /// </summary>
        PacketIdentifierInUse = 145,
        /// <summary>
        /// 配额超限
        /// </summary>
        QuotaExceeded = 151,
        /// <summary>
        /// 负载内容与声明的负载格式不符
        /// </summary>
        PayloadFormatInvalid = 153
    }
}
