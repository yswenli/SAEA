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
*文件名： MqttDisconnectReasonCode
*版本号： v26.4.23.1
*唯一标识：2b47432a-e79a-45a4-9cd9-36badaec37a7
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttDisconnectReasonCode接口
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
    /// MQTT 5.0 断开连接（DISCONNECT）原因码
    /// </summary>
    public enum MqttDisconnectReasonCode
    {
        /// <summary>
        /// 正常断开连接，不发送遗嘱消息
        /// </summary>
        NormalDisconnection = 0,
        /// <summary>
        /// 断开连接并让服务端发布遗嘱消息
        /// </summary>
        DisconnectWithWillMessage = 4,
        /// <summary>
        /// 未指明错误
        /// </summary>
        UnspecifiedError = 128,
        /// <summary>
        /// 收到的报文格式不正确
        /// </summary>
        MalformedPacket = 129,
        /// <summary>
        /// 收到的报文不符合协议要求
        /// </summary>
        ProtocolError = 130,
        /// <summary>
        /// 报文有效但当前实现不接受
        /// </summary>
        ImplementationSpecificError = 131,
        /// <summary>
        /// 未通过授权
        /// </summary>
        NotAuthorized = 135,
        /// <summary>
        /// 服务端繁忙
        /// </summary>
        ServerBusy = 137,
        /// <summary>
        /// 服务端正在关闭
        /// </summary>
        ServerShuttingDown = 139,
        /// <summary>
        /// 超过保活时间内未收到任何报文
        /// </summary>
        KeepAliveTimeout = 141,
        /// <summary>
        /// 同一客户端标识的新连接接管了当前会话
        /// </summary>
        SessionTakenOver = 142,
        /// <summary>
        /// 主题过滤器格式不合法
        /// </summary>
        TopicFilterInvalid = 143,
        /// <summary>
        /// 主题名格式不合法
        /// </summary>
        TopicNameInvalid = 144,
        /// <summary>
        /// 未确认的 QoS 1/2 报文数超过接收最大值
        /// </summary>
        ReceiveMaximumExceeded = 147,
        /// <summary>
        /// 使用了未声明的主题别名
        /// </summary>
        TopicAliasInvalid = 148,
        /// <summary>
        /// 报文超过允许的最大长度
        /// </summary>
        PacketTooLarge = 149,
        /// <summary>
        /// 消息发送速率超过限制
        /// </summary>
        MessageRateTooHigh = 150,
        /// <summary>
        /// 配额超限
        /// </summary>
        QuotaExceeded = 151,
        /// <summary>
        /// 因管理操作断开连接
        /// </summary>
        AdministrativeAction = 152,
        /// <summary>
        /// 负载与声明的负载格式不符
        /// </summary>
        PayloadFormatInvalid = 153,
        /// <summary>
        /// 不支持保留消息
        /// </summary>
        RetainNotSupported = 154,
        /// <summary>
        /// 不支持请求的 QoS 等级
        /// </summary>
        QoSNotSupported = 155,
        /// <summary>
        /// 客户端应改用另一台服务器
        /// </summary>
        UseAnotherServer = 156,
        /// <summary>
        /// 服务端已迁移到新地址
        /// </summary>
        ServerMoved = 157,
        /// <summary>
        /// 不支持共享订阅
        /// </summary>
        SharedSubscriptionsNotSupported = 158,
        /// <summary>
        /// 连接速率超过限制
        /// </summary>
        ConnectionRateExceeded = 159,
        /// <summary>
        /// 达到服务端允许的最大连接时长
        /// </summary>
        MaximumConnectTime = 160,
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
