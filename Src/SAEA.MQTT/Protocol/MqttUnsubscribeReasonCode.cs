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
*文件名： MqttUnsubscribeReasonCode
*版本号： v26.4.23.1
*唯一标识：f0f57ef1-17f6-44d5-9c07-d8e9d8422643
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MQTT协议定义类
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MQTT协议定义类
*
*****************************************************************************/
namespace SAEA.MQTT.Protocol
{
    public enum MqttUnsubscribeReasonCode
    {
        Success = 0,
        NoSubscriptionExisted = 17,
        UnspecifiedError = 128,
        ImplementationSpecificError = 131,
        NotAuthorized = 135,
        TopicFilterInvalid = 143,
        PacketIdentifierInUse = 145
    }
}
