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
*命名空间：SAEA.MQTT.Client.Connecting
*文件名： MqttClientConnectResultCode
*版本号： v26.4.23.1
*唯一标识：370ab253-eed6-410f-b702-634b97000cb5
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientConnectResultCode接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.MQTT.Client.Connecting
{
    /// <summary>
    /// 客户端连接结果码，取值与 MQTT 5.0 CONNACK 原因码一致
    /// </summary>
    public enum MqttClientConnectResultCode
    {
        /// <summary>
        /// 连接成功，服务端接受连接
        /// </summary>
        Success = 0,
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
        /// 客户端请求的协议版本不受支持
        /// </summary>
        UnsupportedProtocolVersion = 132,
        /// <summary>
        /// 客户端标识符不合法
        /// </summary>
        ClientIdentifierNotValid = 133,
        /// <summary>
        /// 用户名或密码错误
        /// </summary>
        BadUserNameOrPassword = 134,
        /// <summary>
        /// 客户端未通过授权
        /// </summary>
        NotAuthorized = 135,
        /// <summary>
        /// 服务端不可用
        /// </summary>
        ServerUnavailable = 136,
        /// <summary>
        /// 服务端繁忙，稍后可重试
        /// </summary>
        ServerBusy = 137,
        /// <summary>
        /// 客户端已被服务端封禁
        /// </summary>
        Banned = 138,
        /// <summary>
        /// 认证方法不匹配或不被支持
        /// </summary>
        BadAuthenticationMethod = 140,
        /// <summary>
        /// 主题名不合法
        /// </summary>
        TopicNameInvalid = 144,
        /// <summary>
        /// 报文超过服务端允许的最大长度
        /// </summary>
        PacketTooLarge = 149,
        /// <summary>
        /// 配额超限
        /// </summary>
        QuotaExceeded = 151,
        /// <summary>
        /// 负载内容与声明的负载格式不符
        /// </summary>
        PayloadFormatInvalid = 153,
        /// <summary>
        /// 服务端不支持保留消息
        /// </summary>
        RetainNotSupported = 154,
        /// <summary>
        /// 服务端不支持请求的 QoS 等级
        /// </summary>
        QoSNotSupported = 155,
        /// <summary>
        /// 客户端应改用另一台服务器
        /// </summary>
        UseAnotherServer = 156,
        /// <summary>
        /// 服务端已迁移，客户端应连接响应中给出的新地址
        /// </summary>
        ServerMoved = 157,
        /// <summary>
        /// 连接速率超过限制
        /// </summary>
        ConnectionRateExceeded = 159
    }
}
