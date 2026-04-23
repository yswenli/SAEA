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
*文件名： MqttPropertyID
*版本号： v26.4.23.1
*唯一标识：733f5b9b-2005-4218-9a17-58da520a3cf3
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
namespace SAEA.MQTT.Protocol
{
    public enum MqttPropertyId
    {
        PayloadFormatIndicator = 1,
        MessageExpiryInterval = 2,
        ContentType = 3,
        ResponseTopic = 8,
        CorrelationData = 9,
        SubscriptionIdentifier = 11,
        SessionExpiryInterval = 17,
        AssignedClientIdentifier = 18,
        ServerKeepAlive = 19,
        AuthenticationMethod = 21,
        AuthenticationData = 22,
        RequestProblemInformation = 23,
        WillDelayInterval = 24,
        RequestResponseInformation = 25,
        ResponseInformation = 26,
        ServerReference = 28,
        ReasonString = 31,
        ReceiveMaximum = 33,
        TopicAliasMaximum = 34,
        TopicAlias = 35,
        MaximumQoS = 36,
        RetainAvailable = 37,
        UserProperty = 38,
        MaximumPacketSize = 39,
        WildcardSubscriptionAvailable = 40,
        SubscriptionIdentifiersAvailable = 41,
        SharedSubscriptionAvailable = 42
    }
}
