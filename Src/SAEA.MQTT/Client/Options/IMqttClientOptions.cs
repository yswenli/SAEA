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
*文件名： IMqttClientOptions
*版本号： v26.4.23.1
*唯一标识：ac41a7c4-8996-479b-bd42-553b7f7c8ca0
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttClientOptions接口
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
    /// 客户端连接选项，对应 CONNECT 报文的内容与属性
    /// </summary>
    public interface IMqttClientOptions
    {
        /// <summary>
        /// 客户端标识符，为空时由服务端在 CONNACK 中分配
        /// </summary>
        string ClientId { get; }
        /// <summary>
        /// 是否要求服务端丢弃已有会话重新开始
        /// </summary>
        bool CleanSession { get; }
        /// <summary>
        /// 用户名密码凭据，null 表示匿名连接
        /// </summary>
        IMqttClientCredentials Credentials { get; }
        /// <summary>
        /// 增强认证 AUTH 报文的处理器，null 表示不处理
        /// </summary>
        IMqttExtendedAuthenticationExchangeHandler ExtendedAuthenticationExchangeHandler { get; }
        /// <summary>
        /// 使用的 MQTT 协议版本
        /// </summary>
        MqttProtocolVersion ProtocolVersion { get; }
        /// <summary>
        /// 传输通道选项，连接时为必填
        /// </summary>
        IMqttClientChannelOptions ChannelOptions { get; }

        /// <summary>
        /// 单次网络读写的等待超时
        /// </summary>
        TimeSpan CommunicationTimeout { get; }
        /// <summary>
        /// 保活间隔，为 TimeSpan.Zero 时不发送 PINGREQ
        /// </summary>
        TimeSpan KeepAlivePeriod { get; }
        /// <summary>
        /// 遗嘱消息，null 表示不设置遗嘱
        /// </summary>
        MqttApplicationMessage WillMessage { get; }
        /// <summary>
        /// 遗嘱消息的延迟发布时长（秒），仅 MQTT 5.0 生效
        /// </summary>
        uint? WillDelayInterval { get; }

        /// <summary>
        /// 增强认证方法名，null 表示不使用增强认证
        /// </summary>
        string AuthenticationMethod { get; }
        /// <summary>
        /// 增强认证的初始数据，仅在设置认证方法时有意义
        /// </summary>
        byte[] AuthenticationData { get; }
        /// <summary>
        /// 客户端可接收的最大报文长度（字节），null 表示使用协议上限
        /// </summary>
        uint? MaximumPacketSize { get; }
        /// <summary>
        /// 客户端允许的在途 QoS 大于 0 报文数，null 表示使用协议上限
        /// </summary>
        ushort? ReceiveMaximum { get; }
        /// <summary>
        /// 是否要求服务端返回原因字符串与用户属性，null 表示按协议默认（返回）
        /// </summary>
        bool? RequestProblemInformation { get; }
        /// <summary>
        /// 是否向服务端请求响应信息，null 表示不携带该属性（按不请求处理）
        /// </summary>
        bool? RequestResponseInformation { get; }
        /// <summary>
        /// 会话过期时间（秒），null 表示不携带该属性，MQTT 5.0 下等同断开即丢弃会话
        /// </summary>
        uint? SessionExpiryInterval { get; }
        /// <summary>
        /// 客户端可接受的主题别名最大编号，null 或 0 表示不接受入站别名
        /// </summary>
        ushort? TopicAliasMaximum { get; }
        /// <summary>
        /// CONNECT 报文携带的用户自定义属性
        /// </summary>
        List<MqttUserProperty> UserProperties { get; set; }
    }
}