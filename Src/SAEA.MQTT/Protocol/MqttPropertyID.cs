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
*唯一标识：58840c87-d535-4cd3-b7b9-77b38924d0e1
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttPropertyID接口
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
    /// MQTT 5.0 属性标识符，用于在报文的属性区标识属性种类
    /// </summary>
    public enum MqttPropertyId
    {
        /// <summary>
        /// 负载格式指示
        /// </summary>
        PayloadFormatIndicator = 1,
        /// <summary>
        /// 消息有效期，单位秒
        /// </summary>
        MessageExpiryInterval = 2,
        /// <summary>
        /// 内容类型描述
        /// </summary>
        ContentType = 3,
        /// <summary>
        /// 请求响应模式下的响应主题
        /// </summary>
        ResponseTopic = 8,
        /// <summary>
        /// 请求响应模式下的关联数据
        /// </summary>
        CorrelationData = 9,
        /// <summary>
        /// 订阅标识符
        /// </summary>
        SubscriptionIdentifier = 11,
        /// <summary>
        /// 会话过期时间，单位秒
        /// </summary>
        SessionExpiryInterval = 17,
        /// <summary>
        /// 服务端分配的客户端标识符
        /// </summary>
        AssignedClientIdentifier = 18,
        /// <summary>
        /// 服务端指定的保活时间，单位秒
        /// </summary>
        ServerKeepAlive = 19,
        /// <summary>
        /// 增强认证的方法名
        /// </summary>
        AuthenticationMethod = 21,
        /// <summary>
        /// 增强认证的附加数据
        /// </summary>
        AuthenticationData = 22,
        /// <summary>
        /// 是否允许服务端返回问题信息
        /// </summary>
        RequestProblemInformation = 23,
        /// <summary>
        /// 遗嘱消息延迟发布的时间，单位秒
        /// </summary>
        WillDelayInterval = 24,
        /// <summary>
        /// 是否请求服务端返回响应信息
        /// </summary>
        RequestResponseInformation = 25,
        /// <summary>
        /// 服务端返回的响应信息
        /// </summary>
        ResponseInformation = 26,
        /// <summary>
        /// 可供客户端改用的其他服务端地址
        /// </summary>
        ServerReference = 28,
        /// <summary>
        /// 原因描述字符串
        /// </summary>
        ReasonString = 31,
        /// <summary>
        /// 接收最大值，即允许的未确认 QoS 1/2 报文数
        /// </summary>
        ReceiveMaximum = 33,
        /// <summary>
        /// 主题别名最大值
        /// </summary>
        TopicAliasMaximum = 34,
        /// <summary>
        /// 主题别名
        /// </summary>
        TopicAlias = 35,
        /// <summary>
        /// 服务端支持的最大 QoS 等级
        /// </summary>
        MaximumQoS = 36,
        /// <summary>
        /// 服务端是否支持保留消息
        /// </summary>
        RetainAvailable = 37,
        /// <summary>
        /// 用户自定义的键值对属性
        /// </summary>
        UserProperty = 38,
        /// <summary>
        /// 允许的最大报文长度，单位字节
        /// </summary>
        MaximumPacketSize = 39,
        /// <summary>
        /// 服务端是否支持通配符订阅
        /// </summary>
        WildcardSubscriptionAvailable = 40,
        /// <summary>
        /// 服务端是否支持订阅标识符
        /// </summary>
        SubscriptionIdentifiersAvailable = 41,
        /// <summary>
        /// 服务端是否支持共享订阅
        /// </summary>
        SharedSubscriptionAvailable = 42
    }
}
