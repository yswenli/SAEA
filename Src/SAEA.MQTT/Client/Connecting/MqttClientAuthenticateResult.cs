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
*文件名： MqttClientAuthenticateResult
*版本号： v26.4.23.1
*唯一标识：244fe14d-986e-4115-afd0-b7664f684f4a
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientAuthenticateResult接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System.Collections.Generic;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Client.Connecting
{
    // TODO: Consider renaming this to _MqttClientConnectResult_
    /// <summary>
    /// 客户端连接认证结果，由服务端 CONNACK 报文转换而来
    /// </summary>
    public class MqttClientAuthenticateResult
    {
        /// <summary>
        /// 连接结果码，MQTT 3.1.1 的返回码会映射到同一枚举
        /// </summary>
        public MqttClientConnectResultCode ResultCode { get; set; }

        /// <summary>
        /// 服务端是否复用了已有会话，仅在 CleanSession 为 false 时可能为 true
        /// </summary>
        public bool IsSessionPresent { get; set; }

        /// <summary>
        /// 服务端是否支持通配符订阅，null 表示服务端未下发该属性
        /// </summary>
        public bool? WildcardSubscriptionAvailable { get; set; }

        /// <summary>
        /// 服务端是否支持保留消息，null 表示服务端未下发该属性
        /// </summary>
        public bool? RetainAvailable { get; set; }

        /// <summary>
        /// 服务端分配的客户端标识符，客户端使用空标识连接时下发
        /// </summary>
        public string AssignedClientIdentifier { get; set; }

        /// <summary>
        /// 服务端确认的增强认证方法名，需与 CONNECT 中提交的一致
        /// </summary>
        public string AuthenticationMethod { get; set; }

        /// <summary>
        /// 增强认证的附加数据，仅在服务端同时下发认证方法时有意义
        /// </summary>
        public byte[] AuthenticationData { get; set; }

        /// <summary>
        /// 服务端允许接收的最大报文长度（字节），约束客户端出站报文
        /// </summary>
        public uint? MaximumPacketSize { get; set; }

        /// <summary>
        /// 服务端返回的结果说明，仅供诊断
        /// </summary>
        public string ReasonString { get; set; }

        /// <summary>
        /// 服务端允许的 QoS 大于 0 的在途发布数量上限
        /// </summary>
        public ushort? ReceiveMaximum { get; set; }
        
        /// <summary>
        /// 服务端支持的最高 QoS 等级，服务端未下发时按 QoS2 处理
        /// </summary>
        public MqttQualityOfServiceLevel MaximumQoS { get; set; }

        /// <summary>
        /// 服务端用于构造响应主题的信息
        /// </summary>
        public string ResponseInformation { get; set; }

        /// <summary>
        /// 服务端接受的主题别名最大编号，0 或 null 表示不支持出站别名
        /// </summary>
        public ushort? TopicAliasMaximum { get; set; }

        /// <summary>
        /// 服务端建议客户端改连的服务器地址
        /// </summary>
        public string ServerReference { get; set; }

        /// <summary>
        /// 服务端指定的保活间隔（秒），会取代客户端请求值
        /// </summary>
        public ushort? ServerKeepAlive { get; set; }

        /// <summary>
        /// 服务端确认的会话过期时间（秒），0 表示连接断开即丢弃会话
        /// </summary>
        public uint? SessionExpiryInterval { get; set; }

        /// <summary>
        /// 服务端是否支持订阅标识符，null 表示服务端未下发该属性
        /// </summary>
        public bool? SubscriptionIdentifiersAvailable { get; set; }

        /// <summary>
        /// 服务端是否支持共享订阅，null 表示服务端未下发该属性
        /// </summary>
        public bool? SharedSubscriptionAvailable { get; set; }

        /// <summary>
        /// 服务端下发的用户自定义属性
        /// </summary>
        public List<MqttUserProperty> UserProperties { get; set; }
    }
}
