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
*命名空间：SAEA.MQTT.Client.Options
*文件名： MqttClientOptions
*版本号： v26.4.23.1
*唯一标识：2dcb5779-4d6f-44bf-8190-b7cdd24da25f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientOptions接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.MQTT.Client.ExtendedAuthenticationExchange;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Packets;
using System;
using System.Collections.Generic;

namespace SAEA.MQTT.Client.Options
{
    /// <summary>
    /// 客户端连接选项，对应 CONNECT 报文的内容与属性，各属性均有默认值，连接前只需补齐通道选项
    /// </summary>
    public class MqttClientOptions : IMqttClientOptions
    {
        /// <summary>
        /// 客户端标识符，默认是新建的 32 位无连字符 GUID 字符串
        /// </summary>
        public string ClientId { get; set; } = Guid.NewGuid().ToString("N");
        /// <summary>
        /// 是否要求服务端丢弃已有会话重新开始，默认 true
        /// </summary>
        public bool CleanSession { get; set; } = true;
        /// <summary>
        /// 用户名密码凭据，null 表示匿名连接
        /// </summary>
        public IMqttClientCredentials Credentials { get; set; }
        /// <summary>
        /// 增强认证 AUTH 报文的处理器，null 表示不处理
        /// </summary>
        public IMqttExtendedAuthenticationExchangeHandler ExtendedAuthenticationExchangeHandler { get; set; }
        /// <summary>
        /// 使用的 MQTT 协议版本，默认 MQTT 3.1.1
        /// </summary>
        public MqttProtocolVersion ProtocolVersion { get; set; } = MqttProtocolVersion.V311;

        /// <summary>
        /// 传输通道选项（TCP、WebSocket 等），连接时为必填
        /// </summary>
        public IMqttClientChannelOptions ChannelOptions { get; set; }
        /// <summary>
        /// 单次网络读写的等待超时，默认 10 秒
        /// </summary>
        public TimeSpan CommunicationTimeout { get; set; } = TimeSpan.FromSeconds(10);
        /// <summary>
        /// 保活间隔，默认 15 秒，为 TimeSpan.Zero 时不发送 PINGREQ
        /// </summary>
        public TimeSpan KeepAlivePeriod { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// 遗嘱消息，null 表示不设置遗嘱
        /// </summary>
        public MqttApplicationMessage WillMessage { get; set; }
        /// <summary>
        /// 遗嘱消息的延迟发布时长（秒），仅 MQTT 5.0 生效
        /// </summary>
        public uint? WillDelayInterval { get; set; }

        /// <summary>
        /// 增强认证方法名，null 表示不使用增强认证
        /// </summary>
        public string AuthenticationMethod { get; set; }
        /// <summary>
        /// 增强认证的初始数据，仅在设置认证方法时有意义
        /// </summary>
        public byte[] AuthenticationData { get; set; }

        /// <summary>
        /// 客户端可接收的最大报文长度（字节），null 表示使用协议上限
        /// </summary>
        public uint? MaximumPacketSize { get; set; }
        /// <summary>
        /// 客户端允许的在途 QoS 大于 0 报文数，null 表示使用协议上限
        /// </summary>
        public ushort? ReceiveMaximum { get; set; }
        /// <summary>
        /// 是否要求服务端返回原因字符串与用户属性，null 表示按协议默认（返回）
        /// </summary>
        public bool? RequestProblemInformation { get; set; }
        /// <summary>
        /// 是否向服务端请求响应信息，null 表示不携带该属性（按不请求处理）
        /// </summary>
        public bool? RequestResponseInformation { get; set; }
        /// <summary>
        /// 会话过期时间（秒），null 表示不携带该属性，MQTT 5.0 下等同断开即丢弃会话
        /// </summary>
        public uint? SessionExpiryInterval { get; set; }
        /// <summary>
        /// 客户端可接受的主题别名最大编号，null 或 0 表示不接受入站别名
        /// </summary>
        public ushort? TopicAliasMaximum { get; set; }
        /// <summary>
        /// CONNECT 报文携带的用户自定义属性
        /// </summary>
        public List<MqttUserProperty> UserProperties { get; set; }
    }
}
