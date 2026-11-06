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
*命名空间：SAEA.MQTT.Formatter
*文件名： IMqttDataConverter
*版本号： v26.4.23.1
*唯一标识：55d2edd8-43b3-4741-93eb-92d630d20a19
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttDataConverter转换器类
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
using SAEA.MQTT.Client.Connecting;
using SAEA.MQTT.Client.Disconnecting;
using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Client.Publishing;
using SAEA.MQTT.Client.Subscribing;
using SAEA.MQTT.Client.Unsubscribing;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Server;
using MqttClientSubscribeResult = SAEA.MQTT.Client.Subscribing.MqttClientSubscribeResult;

namespace SAEA.MQTT.Formatter
{
    /// <summary>
    /// MQTT 数据转换器，在协议报文与客户端、服务端应用模型之间双向转换
    /// </summary>
    public interface IMqttDataConverter
    {
        /// <summary>
        /// 由应用消息创建 PUBLISH 报文，DUP 标志置为 false
        /// </summary>
        /// <param name="applicationMessage">待发布的应用消息</param>
        /// <returns>PUBLISH 报文</returns>
        MqttPublishPacket CreatePublishPacket(MqttApplicationMessage applicationMessage);

        /// <summary>
        /// 由 PUBLISH 报文创建 PUBACK 确认报文，原因码为成功
        /// </summary>
        /// <param name="publishPacket">待确认的 PUBLISH 报文</param>
        /// <returns>PUBACK 报文</returns>
        MqttPubAckPacket CreatePubAckPacket(MqttPublishPacket publishPacket);

        /// <summary>
        /// 由 QoS 2 的 PUBLISH 报文创建 PUBREC 报文，原因码为成功
        /// </summary>
        /// <param name="publishPacket">QoS 2 的 PUBLISH 报文</param>
        /// <returns>PUBREC 报文</returns>
        MqttBasePacket CreatePubRecPacket(MqttPublishPacket publishPacket);

        /// <summary>
        /// 由 PUBLISH 报文还原应用消息
        /// </summary>
        /// <param name="publishPacket">收到的 PUBLISH 报文</param>
        /// <returns>应用消息</returns>
        MqttApplicationMessage CreateApplicationMessage(MqttPublishPacket publishPacket);

        /// <summary>
        /// 由 CONNACK 报文生成客户端鉴权结果
        /// </summary>
        /// <param name="connAckPacket">收到的 CONNACK 报文</param>
        /// <returns>客户端鉴权结果，含会话是否已存在与结果码</returns>
        MqttClientAuthenticateResult CreateClientConnectResult(MqttConnAckPacket connAckPacket);

        /// <summary>
        /// 由客户端连接选项创建 CONNECT 报文
        /// </summary>
        /// <param name="willApplicationMessage">遗嘱消息，未配置遗嘱时为 null</param>
        /// <param name="options">客户端连接选项</param>
        /// <returns>CONNECT 报文</returns>
        MqttConnectPacket CreateConnectPacket(MqttApplicationMessage willApplicationMessage, IMqttClientOptions options);

        /// <summary>
        /// 由连接校验上下文创建 CONNACK 报文
        /// </summary>
        /// <param name="connectionValidatorContext">连接校验上下文</param>
        /// <returns>CONNACK 报文</returns>
        MqttConnAckPacket CreateConnAckPacket(MqttConnectionValidatorContext connectionValidatorContext);

        /// <summary>
        /// 合并 SUBSCRIBE 与 SUBACK 报文生成客户端订阅结果
        /// </summary>
        /// <param name="subscribePacket">发出的 SUBSCRIBE 报文</param>
        /// <param name="subAckPacket">收到的 SUBACK 报文</param>
        /// <returns>客户端订阅结果，按主题过滤器逐项对应返回码</returns>
        MqttClientSubscribeResult CreateClientSubscribeResult(MqttSubscribePacket subscribePacket, MqttSubAckPacket subAckPacket);

        /// <summary>
        /// 合并 UNSUBSCRIBE 与 UNSUBACK 报文生成客户端取消订阅结果
        /// </summary>
        /// <param name="unsubscribePacket">发出的 UNSUBSCRIBE 报文</param>
        /// <param name="unsubAckPacket">收到的 UNSUBACK 报文</param>
        /// <returns>客户端取消订阅结果</returns>
        MqttClientUnsubscribeResult CreateClientUnsubscribeResult(MqttUnsubscribePacket unsubscribePacket, MqttUnsubAckPacket unsubAckPacket);

        /// <summary>
        /// 由订阅选项创建 SUBSCRIBE 报文
        /// </summary>
        /// <param name="options">客户端订阅选项</param>
        /// <returns>SUBSCRIBE 报文</returns>
        MqttSubscribePacket CreateSubscribePacket(MqttClientSubscribeOptions options);

        /// <summary>
        /// 由 SUBSCRIBE 报文与服务端订阅结果创建 SUBACK 报文
        /// </summary>
        /// <param name="subscribePacket">客户端发来的 SUBSCRIBE 报文</param>
        /// <param name="subscribeResult">服务端订阅处理结果</param>
        /// <returns>SUBACK 报文</returns>
        MqttSubAckPacket CreateSubAckPacket(MqttSubscribePacket subscribePacket, Server.MqttClientSubscribeResult subscribeResult);

        /// <summary>
        /// 由取消订阅选项创建 UNSUBSCRIBE 报文
        /// </summary>
        /// <param name="options">客户端取消订阅选项</param>
        /// <returns>UNSUBSCRIBE 报文</returns>
        MqttUnsubscribePacket CreateUnsubscribePacket(MqttClientUnsubscribeOptions options);

        /// <summary>
        /// 由 UNSUBSCRIBE 报文与原因码集合创建 UNSUBACK 报文
        /// </summary>
        /// <param name="unsubscribePacket">客户端发来的 UNSUBSCRIBE 报文</param>
        /// <param name="reasonCodes">每个主题过滤器对应的原因码</param>
        /// <returns>UNSUBACK 报文</returns>
        MqttUnsubAckPacket CreateUnsubAckPacket(MqttUnsubscribePacket unsubscribePacket, List<MqttUnsubscribeReasonCode> reasonCodes);

        /// <summary>
        /// 由客户端断开选项创建 DISCONNECT 报文
        /// </summary>
        /// <param name="options">客户端断开选项</param>
        /// <returns>DISCONNECT 报文</returns>
        MqttDisconnectPacket CreateDisconnectPacket(MqttClientDisconnectOptions options);

        /// <summary>
        /// 由 PUBACK 报文生成 QoS 1 发布结果
        /// </summary>
        /// <param name="pubAckPacket">收到的 PUBACK 报文，可为 null</param>
        /// <returns>客户端发布结果</returns>
        MqttClientPublishResult CreatePublishResult(MqttPubAckPacket pubAckPacket);

        /// <summary>
        /// 由 PUBREC 与 PUBCOMP 报文生成 QoS 2 发布结果
        /// </summary>
        /// <param name="pubRecPacket">收到的 PUBREC 报文</param>
        /// <param name="pubCompPacket">收到的 PUBCOMP 报文</param>
        /// <returns>客户端发布结果，任一报文为 null 时返回未指明错误</returns>
        MqttClientPublishResult CreatePublishResult(MqttPubRecPacket pubRecPacket, MqttPubCompPacket pubCompPacket);
    }
}
