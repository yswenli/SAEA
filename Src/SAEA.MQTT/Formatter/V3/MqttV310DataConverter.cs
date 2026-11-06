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
*命名空间：SAEA.MQTT.Formatter.V3
*文件名： MqttV310DataConverter
*版本号： v26.4.23.1
*唯一标识：0613c63e-acee-420c-b024-c3ce418d9be9
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttV310DataConverter转换器类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.MQTT.Client.Connecting;
using SAEA.MQTT.Client.Disconnecting;
using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Client.Publishing;
using SAEA.MQTT.Client.Subscribing;
using SAEA.MQTT.Client.Unsubscribing;
using SAEA.MQTT.Exceptions;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using MqttClientSubscribeResult = SAEA.MQTT.Client.Subscribing.MqttClientSubscribeResult;

namespace SAEA.MQTT.Formatter.V3
{
    /// <summary>
    /// MQTT 3.1.0 的数据转换器，在协议报文与客户端、服务端应用模型之间转换
    /// </summary>
    public class MqttV310DataConverter : IMqttDataConverter
    {
        /// <summary>
        /// 由应用消息创建 PUBLISH 报文，DUP 标志置为 false
        /// </summary>
        /// <param name="applicationMessage">待发布的应用消息，不能为 null</param>
        /// <returns>PUBLISH 报文</returns>
        public MqttPublishPacket CreatePublishPacket(MqttApplicationMessage applicationMessage)
        {
            if (applicationMessage == null) throw new ArgumentNullException(nameof(applicationMessage));

            return new MqttPublishPacket
            {
                Topic = applicationMessage.Topic,
                Payload = applicationMessage.Payload,
                QualityOfServiceLevel = applicationMessage.QualityOfServiceLevel,
                Retain = applicationMessage.Retain,
                Dup = false
            };
        }

        /// <summary>
        /// 由 PUBLISH 报文创建 PUBACK 报文，原因码为成功
        /// </summary>
        /// <param name="publishPacket">待确认的 PUBLISH 报文</param>
        /// <returns>PUBACK 报文</returns>
        public MqttPubAckPacket CreatePubAckPacket(MqttPublishPacket publishPacket)
        {
            return new MqttPubAckPacket
            {
                PacketIdentifier = publishPacket.PacketIdentifier,
                ReasonCode = MqttPubAckReasonCode.Success
            };
        }

        /// <summary>
        /// 由 PUBLISH 报文创建 PUBREC 报文，原因码为成功
        /// </summary>
        /// <param name="publishPacket">QoS 2 的 PUBLISH 报文</param>
        /// <returns>PUBREC 报文</returns>
        public MqttBasePacket CreatePubRecPacket(MqttPublishPacket publishPacket)
        {
            return new MqttPubRecPacket
            {
                PacketIdentifier = publishPacket.PacketIdentifier,
                ReasonCode = MqttPubRecReasonCode.Success
            };
        }

        /// <summary>
        /// 由 PUBLISH 报文还原应用消息
        /// </summary>
        /// <param name="publishPacket">收到的 PUBLISH 报文，不能为 null</param>
        /// <returns>应用消息</returns>
        public MqttApplicationMessage CreateApplicationMessage(MqttPublishPacket publishPacket)
        {
            if (publishPacket == null) throw new ArgumentNullException(nameof(publishPacket));

            return new MqttApplicationMessage
            {
                Topic = publishPacket.Topic,
                Payload = publishPacket.Payload,
                QualityOfServiceLevel = publishPacket.QualityOfServiceLevel,
                Retain = publishPacket.Retain
            };
        }

        /// <summary>
        /// 由 CONNACK 报文生成客户端鉴权结果，无法识别的返回码抛出协议违规异常
        /// </summary>
        /// <param name="connAckPacket">收到的 CONNACK 报文，不能为 null</param>
        /// <returns>客户端鉴权结果，含会话是否已存在与结果码</returns>
        public MqttClientAuthenticateResult CreateClientConnectResult(MqttConnAckPacket connAckPacket)
        {
            if (connAckPacket == null) throw new ArgumentNullException(nameof(connAckPacket));

            MqttClientConnectResultCode resultCode;
            switch (connAckPacket.ReturnCode.Value)
            {
                case MqttConnectReturnCode.ConnectionAccepted:
                    {
                        resultCode = MqttClientConnectResultCode.Success;
                        break;
                    }

                case MqttConnectReturnCode.ConnectionRefusedUnacceptableProtocolVersion:
                    {
                        resultCode = MqttClientConnectResultCode.UnsupportedProtocolVersion;
                        break;
                    }

                case MqttConnectReturnCode.ConnectionRefusedNotAuthorized:
                    {
                        resultCode = MqttClientConnectResultCode.NotAuthorized;
                        break;
                    }

                case MqttConnectReturnCode.ConnectionRefusedBadUsernameOrPassword:
                    {
                        resultCode = MqttClientConnectResultCode.BadUserNameOrPassword;
                        break;
                    }

                case MqttConnectReturnCode.ConnectionRefusedIdentifierRejected:
                    {
                        resultCode = MqttClientConnectResultCode.ClientIdentifierNotValid;
                        break;
                    }

                case MqttConnectReturnCode.ConnectionRefusedServerUnavailable:
                    {
                        resultCode = MqttClientConnectResultCode.ServerUnavailable;
                        break;
                    }

                default:
                    throw new MqttProtocolViolationException("Received unexpected return code.");
            }

            return new MqttClientAuthenticateResult
            {
                IsSessionPresent = connAckPacket.IsSessionPresent,
                ResultCode = resultCode
            };
        }

        /// <summary>
        /// 由客户端连接选项创建 CONNECT 报文，保持连接时间换算为秒
        /// </summary>
        /// <param name="willApplicationMessage">遗嘱消息，未配置遗嘱时为 null</param>
        /// <param name="options">客户端连接选项，不能为 null</param>
        /// <returns>CONNECT 报文</returns>
        public MqttConnectPacket CreateConnectPacket(MqttApplicationMessage willApplicationMessage, IMqttClientOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            return new MqttConnectPacket
            {
                ClientId = options.ClientId,
                Username = options.Credentials?.Username,
                Password = options.Credentials?.Password,
                CleanSession = options.CleanSession,
                KeepAlivePeriod = (ushort)options.KeepAlivePeriod.TotalSeconds,
                WillMessage = willApplicationMessage
            };
        }

        /// <summary>
        /// 由连接校验上下文创建 CONNACK 报文，返回码按 3.1.0 语义转换
        /// </summary>
        /// <param name="connectionValidatorContext">连接校验上下文，不能为 null</param>
        /// <returns>CONNACK 报文</returns>
        public MqttConnAckPacket CreateConnAckPacket(MqttConnectionValidatorContext connectionValidatorContext)
        {
            if (connectionValidatorContext == null) throw new ArgumentNullException(nameof(connectionValidatorContext));

            return new MqttConnAckPacket
            {
                ReturnCode = new MqttConnectReasonCodeConverter().ToConnectReturnCode(connectionValidatorContext.ReasonCode),
                IsSessionPresent = connectionValidatorContext.IsSessionPresent
            };
        }

        /// <summary>
        /// 合并 SUBSCRIBE 与 SUBACK 报文生成客户端订阅结果，返回码数量与主题过滤器数量不一致时抛出协议违规异常
        /// </summary>
        /// <param name="subscribePacket">发出的 SUBSCRIBE 报文，不能为 null</param>
        /// <param name="subAckPacket">收到的 SUBACK 报文，不能为 null</param>
        /// <returns>客户端订阅结果，按主题过滤器逐项对应返回码</returns>
        public MqttClientSubscribeResult CreateClientSubscribeResult(MqttSubscribePacket subscribePacket, MqttSubAckPacket subAckPacket)
        {
            if (subscribePacket == null) throw new ArgumentNullException(nameof(subscribePacket));
            if (subAckPacket == null) throw new ArgumentNullException(nameof(subAckPacket));

            if (subAckPacket.ReturnCodes.Count != subscribePacket.TopicFilters.Count)
            {
                throw new MqttProtocolViolationException("The return codes are not matching the topic filters [MQTT-3.9.3-1].");
            }

            var result = new MqttClientSubscribeResult();

            result.Items.AddRange(subscribePacket.TopicFilters.Select((t, i) =>
                new MqttClientSubscribeResultItem(t, (MqttClientSubscribeResultCode)subAckPacket.ReturnCodes[i])));

            return result;
        }

        /// <summary>
        /// 合并 UNSUBSCRIBE 与 UNSUBACK 报文生成客户端取消订阅结果，3.1.0 无原因码，各项均为成功
        /// </summary>
        /// <param name="unsubscribePacket">发出的 UNSUBSCRIBE 报文，不能为 null</param>
        /// <param name="unsubAckPacket">收到的 UNSUBACK 报文，不能为 null</param>
        /// <returns>客户端取消订阅结果</returns>
        public MqttClientUnsubscribeResult CreateClientUnsubscribeResult(MqttUnsubscribePacket unsubscribePacket, MqttUnsubAckPacket unsubAckPacket)
        {
            if (unsubscribePacket == null) throw new ArgumentNullException(nameof(unsubscribePacket));
            if (unsubAckPacket == null) throw new ArgumentNullException(nameof(unsubAckPacket));

            var result = new MqttClientUnsubscribeResult();

            result.Items.AddRange(unsubscribePacket.TopicFilters.Select((t, i) =>
                new MqttClientUnsubscribeResultItem(t, MqttClientUnsubscribeResultCode.Success)));

            return result;
        }

        /// <summary>
        /// 由订阅选项创建 SUBSCRIBE 报文
        /// </summary>
        /// <param name="options">客户端订阅选项，不能为 null</param>
        /// <returns>SUBSCRIBE 报文</returns>
        public MqttSubscribePacket CreateSubscribePacket(MqttClientSubscribeOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var subscribePacket = new MqttSubscribePacket();
            subscribePacket.TopicFilters.AddRange(options.TopicFilters);

            return subscribePacket;
        }

        /// <summary>
        /// 由 SUBSCRIBE 报文与服务端订阅结果创建 SUBACK 报文
        /// </summary>
        /// <param name="subscribePacket">客户端发来的 SUBSCRIBE 报文，不能为 null</param>
        /// <param name="subscribeResult">服务端订阅处理结果</param>
        /// <returns>SUBACK 报文，携带相同的报文标识符与逐项返回码</returns>
        public MqttSubAckPacket CreateSubAckPacket(MqttSubscribePacket subscribePacket, Server.MqttClientSubscribeResult subscribeResult)
        {
            var subackPacket = new MqttSubAckPacket
            {
                PacketIdentifier = subscribePacket.PacketIdentifier
            };

            subackPacket.ReturnCodes.AddRange(subscribeResult.ReturnCodes);

            return subackPacket;
        }

        /// <summary>
        /// 由取消订阅选项创建 UNSUBSCRIBE 报文
        /// </summary>
        /// <param name="options">客户端取消订阅选项，不能为 null</param>
        /// <returns>UNSUBSCRIBE 报文</returns>
        public MqttUnsubscribePacket CreateUnsubscribePacket(MqttClientUnsubscribeOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var unsubscribePacket = new MqttUnsubscribePacket();
            unsubscribePacket.TopicFilters.AddRange(options.TopicFilters);

            return unsubscribePacket;
        }

        /// <summary>
        /// 由 UNSUBSCRIBE 报文与原因码集合创建 UNSUBACK 报文，3.1.0 编码时会忽略原因码
        /// </summary>
        /// <param name="unsubscribePacket">客户端发来的 UNSUBSCRIBE 报文，不能为 null</param>
        /// <param name="reasonCodes">每个主题过滤器对应的原因码</param>
        /// <returns>UNSUBACK 报文</returns>
        public MqttUnsubAckPacket CreateUnsubAckPacket(MqttUnsubscribePacket unsubscribePacket, List<MqttUnsubscribeReasonCode> reasonCodes)
        {
            return new MqttUnsubAckPacket
            {
                PacketIdentifier = unsubscribePacket.PacketIdentifier,
                ReasonCodes = reasonCodes
            };
        }

        /// <summary>
        /// 由客户端断开选项创建 DISCONNECT 报文，3.1.0 不支持原因码与原因字符串，设置后抛出协议违规异常
        /// </summary>
        /// <param name="options">客户端断开选项，不能为 null</param>
        /// <returns>DISCONNECT 报文</returns>
        public MqttDisconnectPacket CreateDisconnectPacket(MqttClientDisconnectOptions options)
        {
            if (options.ReasonCode != MqttClientDisconnectReason.NormalDisconnection || options.ReasonString != null)
            {
                throw new MqttProtocolViolationException("Reason codes and reason string for disconnect are only supported for MQTTv5.");
            }

            return new MqttDisconnectPacket();
        }

        /// <summary>
        /// 由 PUBACK 报文生成 QoS 1 发布结果，原因码固定为成功
        /// </summary>
        /// <param name="pubAckPacket">收到的 PUBACK 报文，可为 null</param>
        /// <returns>客户端发布结果，报文为 null 时不含报文标识符</returns>
        public MqttClientPublishResult CreatePublishResult(MqttPubAckPacket pubAckPacket)
        {
            return new MqttClientPublishResult
            {
                PacketIdentifier = pubAckPacket?.PacketIdentifier,
                ReasonCode = MqttClientPublishReasonCode.Success
            };
        }

        /// <summary>
        /// 由 PUBREC 与 PUBCOMP 报文生成 QoS 2 发布结果，任一报文为 null 时返回未指明错误
        /// </summary>
        /// <param name="pubRecPacket">收到的 PUBREC 报文</param>
        /// <param name="pubCompPacket">收到的 PUBCOMP 报文</param>
        /// <returns>客户端发布结果，成功时携带 PUBCOMP 的报文标识符</returns>
        public MqttClientPublishResult CreatePublishResult(MqttPubRecPacket pubRecPacket, MqttPubCompPacket pubCompPacket)
        {
            if (pubRecPacket == null || pubCompPacket == null)
            {
                return new MqttClientPublishResult
                {
                    ReasonCode = MqttClientPublishReasonCode.UnspecifiedError
                };
            }

            return new MqttClientPublishResult
            {
                PacketIdentifier = pubCompPacket.PacketIdentifier,
                ReasonCode = MqttClientPublishReasonCode.Success
            };
        }
    }
}
