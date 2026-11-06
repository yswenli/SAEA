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
*命名空间：SAEA.MQTT.Client.Disconnecting
*文件名： MqttClientDisconnectReason
*版本号： v26.4.23.1
*唯一标识：167557c6-3a08-462f-8f21-57ed2031f2b1
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientDisconnectReason接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.MQTT.Client.Disconnecting
{
    /// <summary>
    /// 连接断开原因，取值与 MQTT 5.0 DISCONNECT 原因码一致
    /// </summary>
    public enum MqttClientDisconnectReason
    {
        /// <summary>
        /// 正常断开，不触发遗嘱消息
        /// </summary>
        NormalDisconnection = 0,
        /// <summary>
        /// 主动断开但仍要求服务端发布遗嘱消息
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
        /// 报文字段有效但当前实现不接受
        /// </summary>
        ImplementationSpecificError = 131,
        /// <summary>
        /// 未通过授权
        /// </summary>
        NotAuthorized = 135,
        /// <summary>
        /// 服务端繁忙，稍后可重试
        /// </summary>
        ServerBusy = 137,
        /// <summary>
        /// 服务端正在关闭，稍后可重连
        /// </summary>
        ServerShuttingDown = 139,
        /// <summary>
        /// 认证方法不匹配或不被支持
        /// </summary>
        BadAuthenticationMethod = 140,
        /// <summary>
        /// 保活超时，超过 1.5 倍保活间隔未收到对端报文
        /// </summary>
        KeepaliveTimeout = 141,
        /// <summary>
        /// 同一客户端标识的新连接接管了当前会话
        /// </summary>
        SessionTakenOver = 142,
        /// <summary>
        /// 订阅时提交的主题过滤器不合法
        /// </summary>
        TopicFilterInvalid = 143,
        /// <summary>
        /// 主题名不合法
        /// </summary>
        TopicNameInvalid = 144,
        /// <summary>
        /// 在途的 QoS 大于 0 报文数超过协商的 Receive Maximum
        /// </summary>
        ReceiveMaximumExceeded = 147,
        /// <summary>
        /// 主题别名非法或超出协商范围
        /// </summary>
        TopicAliasInvalid = 148,
        /// <summary>
        /// 报文超过允许的最大长度
        /// </summary>
        PacketTooLarge = 149,
        /// <summary>
        /// 报文发送速率过高
        /// </summary>
        MessageRateTooHigh = 150,
        /// <summary>
        /// 配额超限
        /// </summary>
        QuotaExceeded = 151,
        /// <summary>
        /// 管理员主动断开连接
        /// </summary>
        AdministrativeAction = 152,
        /// <summary>
        /// 负载内容与声明的负载格式不符
        /// </summary>
        PayloadFormatInvalid = 153,
        /// <summary>
        /// 对端不支持保留消息
        /// </summary>
        RetainNotSupported = 154,
        /// <summary>
        /// 对端不支持请求的 QoS 等级
        /// </summary>
        QosNotSupported = 155,
        /// <summary>
        /// 应改用另一台服务器
        /// </summary>
        UseAnotherServer = 156,
        /// <summary>
        /// 服务器已迁移，应连接响应中给出的新地址
        /// </summary>
        ServerMoved = 157,
        /// <summary>
        /// 对端不支持共享订阅
        /// </summary>
        SharedSubscriptionsNotSupported = 158,
        /// <summary>
        /// 连接速率超过限制
        /// </summary>
        ConnectionRateExceeded = 159,
        /// <summary>
        /// 连接时长超过服务端允许的上限
        /// </summary>
        MaximumConnectTime = 160,
        /// <summary>
        /// 对端不支持订阅标识符
        /// </summary>
        SubscriptionIdentifiersNotSupported = 161,
        /// <summary>
        /// 对端不支持通配符订阅
        /// </summary>
        WildcardSubscriptionsNotSupported = 162
    }
}
