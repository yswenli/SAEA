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
*命名空间：SAEA.MQTT.Formatter.V5
*文件名： MqttV500DataConverter
*版本号： v26.4.23.1
*唯一标识：d12a2bff-3298-4017-ba30-cdca38d00799
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttV500DataConverter转换器类
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
using SAEA.MQTT.Internal;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using MqttClientSubscribeResult = SAEA.MQTT.Client.Subscribing.MqttClientSubscribeResult;

namespace SAEA.MQTT.Formatter.V5
{
    /// <summary>
    /// MQTT 5.0 的数据转换器，在协议报文与客户端、服务端应用模型之间转换，并映射 5.0 属性
    /// </summary>
    public class MqttV500DataConverter : IMqttDataConverter
    {
        internal MqttConnectionCapabilities Capabilities { get; set; }

        /// <summary>
        /// 由应用消息创建 PUBLISH 报文，拷贝 5.0 属性，DUP 标志置为 false
        /// </summary>
        /// <param name="applicationMessage">待发布的应用消息，不能为 null</param>
        /// <returns>PUBLISH 报文</returns>
        public MqttPublishPacket CreatePublishPacket(MqttApplicationMessage applicationMessage)
        {
            if (applicationMessage == null) throw new ArgumentNullException(nameof(applicationMessage));

            var packet = new MqttPublishPacket
            {
                Topic = applicationMessage.Topic,
                Payload = applicationMessage.Payload,
                QualityOfServiceLevel = applicationMessage.QualityOfServiceLevel,
                Retain = applicationMessage.Retain,
                Dup = false,
                Properties = new MqttPublishPacketProperties
                {
                    ContentType = applicationMessage.ContentType,
                    CorrelationData = applicationMessage.CorrelationData,
                    MessageExpiryInterval = applicationMessage.MessageExpiryInterval,
                    PayloadFormatIndicator = applicationMessage.PayloadFormatIndicator,
                    ResponseTopic = applicationMessage.ResponseTopic,
                    SubscriptionIdentifiers = applicationMessage.SubscriptionIdentifiers,
                    TopicAlias = applicationMessage.TopicAlias
                }
            };

            if (applicationMessage.UserProperties != null)
            {
                packet.Properties.UserProperties = new List<MqttUserProperty>();
                packet.Properties.UserProperties.AddRange(applicationMessage.UserProperties);
            }

            return packet;
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
        /// 由 PUBLISH 报文还原应用消息，属性缺失时用户属性返回空集合
        /// </summary>
        /// <param name="publishPacket">收到的 PUBLISH 报文</param>
        /// <returns>应用消息</returns>
        public MqttApplicationMessage CreateApplicationMessage(MqttPublishPacket publishPacket)
        {
            return new MqttApplicationMessage
            {
                Topic = publishPacket.Topic,
                Payload = publishPacket.Payload,
                QualityOfServiceLevel = publishPacket.QualityOfServiceLevel,
                Retain = publishPacket.Retain,
                ResponseTopic = publishPacket.Properties?.ResponseTopic,
                ContentType = publishPacket.Properties?.ContentType,
                CorrelationData = publishPacket.Properties?.CorrelationData,
                MessageExpiryInterval = publishPacket.Properties?.MessageExpiryInterval,
                SubscriptionIdentifiers = publishPacket.Properties?.SubscriptionIdentifiers,
                TopicAlias = publishPacket.Properties?.TopicAlias,
                PayloadFormatIndicator = publishPacket.Properties?.PayloadFormatIndicator,
                UserProperties = publishPacket.Properties?.UserProperties ?? new List<MqttUserProperty>()
            };
        }

        /// <summary>
        /// 由 CONNACK 报文生成客户端鉴权结果，按 5.0 原因码直接映射，服务端未声明时最大 QoS 取 QoS 2
        /// </summary>
        /// <param name="connAckPacket">收到的 CONNACK 报文，不能为 null</param>
        /// <returns>客户端鉴权结果，含服务端声明的各项能力</returns>
        public MqttClientAuthenticateResult CreateClientConnectResult(MqttConnAckPacket connAckPacket)
        {
            if (connAckPacket == null) throw new ArgumentNullException(nameof(connAckPacket));

            return new MqttClientAuthenticateResult
            {
                IsSessionPresent = connAckPacket.IsSessionPresent,
                ResultCode = (MqttClientConnectResultCode)connAckPacket.ReasonCode.Value,
                WildcardSubscriptionAvailable = connAckPacket.Properties?.WildcardSubscriptionAvailable,
                RetainAvailable = connAckPacket.Properties?.RetainAvailable,
                AssignedClientIdentifier = connAckPacket.Properties?.AssignedClientIdentifier,
                AuthenticationMethod = connAckPacket.Properties?.AuthenticationMethod,
                AuthenticationData = connAckPacket.Properties?.AuthenticationData,
                MaximumPacketSize = connAckPacket.Properties?.MaximumPacketSize,
                ReasonString = connAckPacket.Properties?.ReasonString,
                ReceiveMaximum = connAckPacket.Properties?.ReceiveMaximum,
                MaximumQoS = connAckPacket.Properties?.MaximumQoS ?? MqttQualityOfServiceLevel.ExactlyOnce,
                ResponseInformation = connAckPacket.Properties?.ResponseInformation,
                TopicAliasMaximum = connAckPacket.Properties?.TopicAliasMaximum,
                ServerReference = connAckPacket.Properties?.ServerReference,
                ServerKeepAlive = connAckPacket.Properties?.ServerKeepAlive,
                SessionExpiryInterval = connAckPacket.Properties?.SessionExpiryInterval,
                SubscriptionIdentifiersAvailable = connAckPacket.Properties?.SubscriptionIdentifiersAvailable,
                SharedSubscriptionAvailable = connAckPacket.Properties?.SharedSubscriptionAvailable,
                UserProperties = connAckPacket.Properties?.UserProperties
            };
        }

        /// <summary>
        /// 由客户端连接选项创建 CONNECT 报文，携带 5.0 连接属性，保持连接时间换算为秒
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
                WillMessage = willApplicationMessage,
                Properties = new MqttConnectPacketProperties
                {
                    AuthenticationMethod = options.AuthenticationMethod,
                    AuthenticationData = options.AuthenticationData,
                    WillDelayInterval = options.WillDelayInterval,
                    MaximumPacketSize = options.MaximumPacketSize,
                    ReceiveMaximum = options.ReceiveMaximum,
                    RequestProblemInformation = options.RequestProblemInformation,
                    RequestResponseInformation = options.RequestResponseInformation,
                    SessionExpiryInterval = options.SessionExpiryInterval,
                    TopicAliasMaximum = options.TopicAliasMaximum,
                    UserProperties = options.UserProperties
                }
            };
        }

        /// <summary>
        /// 由连接校验上下文创建 CONNACK 报文，仅在服务端改写能力或允许问题信息时才写入对应属性
        /// </summary>
        /// <param name="connectionValidatorContext">连接校验上下文，不能为 null</param>
        /// <returns>CONNACK 报文</returns>
        public MqttConnAckPacket CreateConnAckPacket(MqttConnectionValidatorContext connectionValidatorContext)
        {
            if (connectionValidatorContext == null) throw new ArgumentNullException(nameof(connectionValidatorContext));

            var capabilities = Capabilities;   // 可能为 null（测试/异常路径）

            // Request Problem Information = 0 时，CONNACK 不携带诊断信息。
            var includeProblemInformation = capabilities == null || capabilities.RequestProblemInformation;

            var properties = new MqttConnAckPacketProperties
            {
                AuthenticationMethod = connectionValidatorContext.AuthenticationMethod,
                AuthenticationData = connectionValidatorContext.ResponseAuthenticationData,
                AssignedClientIdentifier = connectionValidatorContext.AssignedClientIdentifier
            };

            if (includeProblemInformation)
            {
                properties.UserProperties = connectionValidatorContext.ResponseUserProperties;
                properties.ReasonString = connectionValidatorContext.ReasonString;
            }

            if (capabilities != null)
            {
                if (capabilities.ReceiveMaximum != ushort.MaxValue)
                {
                    properties.ReceiveMaximum = capabilities.ReceiveMaximum;
                }

                if (capabilities.MaximumPacketSize != 268435455)
                {
                    properties.MaximumPacketSize = capabilities.MaximumPacketSize;
                }

                if (capabilities.TopicAliasMaximum != 0)
                {
                    properties.TopicAliasMaximum = capabilities.TopicAliasMaximum;
                }

                if (capabilities.MaximumQoS != MqttQualityOfServiceLevel.ExactlyOnce)
                {
                    properties.MaximumQoS = capabilities.MaximumQoS;
                }

                if (!capabilities.RetainAvailable)
                {
                    properties.RetainAvailable = false;
                }

                if (!capabilities.WildcardSubscriptionAvailable)
                {
                    properties.WildcardSubscriptionAvailable = false;
                }

                if (!capabilities.SubscriptionIdentifiersAvailable)
                {
                    properties.SubscriptionIdentifiersAvailable = false;
                }

                if (!capabilities.SharedSubscriptionAvailable)
                {
                    properties.SharedSubscriptionAvailable = false;
                }

                // ServerKeepAlive 仅在服务端确实改写时写
                var clientKeepAlivePeriod = connectionValidatorContext.KeepAlivePeriod;
                if (capabilities.ServerKeepAlive.HasValue &&
                    (!clientKeepAlivePeriod.HasValue || capabilities.ServerKeepAlive.Value != clientKeepAlivePeriod.Value))
                {
                    properties.ServerKeepAlive = capabilities.ServerKeepAlive.Value;
                }

                // SessionExpiry 仅在服务端下调时写
                var clientSessionExpiry = connectionValidatorContext.SessionExpiryInterval ?? 0;
                if (capabilities.SessionExpiryInterval != clientSessionExpiry)
                {
                    properties.SessionExpiryInterval = capabilities.SessionExpiryInterval;
                }
            }
            else
            {
                // 无上下文时保持既有语义：仅写 validator 提供的字段（不再硬编码 TopicAliasMaximum）
            }

            return new MqttConnAckPacket
            {
                ReasonCode = connectionValidatorContext.ReasonCode,
                IsSessionPresent = connectionValidatorContext.IsSessionPresent,
                Properties = properties
            };
        }

        /// <summary>
        /// 合并 SUBSCRIBE 与 SUBACK 报文生成客户端订阅结果，原因码数量与主题过滤器数量不一致时抛出协议违规异常
        /// </summary>
        /// <param name="subscribePacket">发出的 SUBSCRIBE 报文，不能为 null</param>
        /// <param name="subAckPacket">收到的 SUBACK 报文，不能为 null</param>
        /// <returns>客户端订阅结果，按主题过滤器逐项对应原因码</returns>
        public MqttClientSubscribeResult CreateClientSubscribeResult(MqttSubscribePacket subscribePacket, MqttSubAckPacket subAckPacket)
        {
            if (subscribePacket == null) throw new ArgumentNullException(nameof(subscribePacket));
            if (subAckPacket == null) throw new ArgumentNullException(nameof(subAckPacket));

            if (subAckPacket.ReasonCodes.Count != subscribePacket.TopicFilters.Count)
            {
                throw new MqttProtocolViolationException("The reason codes are not matching the topic filters [MQTT-3.9.3-1].");
            }

            var result = new MqttClientSubscribeResult();

            result.Items.AddRange(subscribePacket.TopicFilters.Select((t, i) =>
                new MqttClientSubscribeResultItem(t, (MqttClientSubscribeResultCode)subAckPacket.ReasonCodes[i])));

            return result;
        }

        /// <summary>
        /// 合并 UNSUBSCRIBE 与 UNSUBACK 报文生成客户端取消订阅结果，原因码数量与主题过滤器数量不一致时抛出协议违规异常
        /// </summary>
        /// <param name="unsubscribePacket">发出的 UNSUBSCRIBE 报文，不能为 null</param>
        /// <param name="unsubAckPacket">收到的 UNSUBACK 报文，不能为 null</param>
        /// <returns>客户端取消订阅结果，按主题过滤器逐项对应原因码</returns>
        public MqttClientUnsubscribeResult CreateClientUnsubscribeResult(MqttUnsubscribePacket unsubscribePacket, MqttUnsubAckPacket unsubAckPacket)
        {
            if (unsubscribePacket == null) throw new ArgumentNullException(nameof(unsubscribePacket));
            if (unsubAckPacket == null) throw new ArgumentNullException(nameof(unsubAckPacket));

            if (unsubAckPacket.ReasonCodes.Count != unsubscribePacket.TopicFilters.Count)
            {
                throw new MqttProtocolViolationException("The return codes are not matching the topic filters [MQTT-3.9.3-1].");
            }

            var result = new MqttClientUnsubscribeResult();

            result.Items.AddRange(unsubscribePacket.TopicFilters.Select((t, i) =>
                new MqttClientUnsubscribeResultItem(t, (MqttClientUnsubscribeResultCode)unsubAckPacket.ReasonCodes[i])));

            return result;
        }

        /// <summary>
        /// 由订阅选项创建 SUBSCRIBE 报文，携带订阅标识符与用户属性
        /// </summary>
        /// <param name="options">客户端订阅选项，不能为 null</param>
        /// <returns>SUBSCRIBE 报文</returns>
        public MqttSubscribePacket CreateSubscribePacket(MqttClientSubscribeOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var packet = new MqttSubscribePacket
            {
                Properties = new MqttSubscribePacketProperties()
            };

            packet.TopicFilters.AddRange(options.TopicFilters);
            packet.Properties.SubscriptionIdentifier = options.SubscriptionIdentifier;
            packet.Properties.UserProperties = options.UserProperties;

            return packet;
        }

        /// <summary>
        /// 由 SUBSCRIBE 报文与服务端订阅结果创建 SUBACK 报文
        /// </summary>
        /// <param name="subscribePacket">客户端发来的 SUBSCRIBE 报文，不能为 null</param>
        /// <param name="subscribeResult">服务端订阅处理结果</param>
        /// <returns>SUBACK 报文，携带相同的报文标识符与逐项原因码</returns>
        public MqttSubAckPacket CreateSubAckPacket(MqttSubscribePacket subscribePacket, Server.MqttClientSubscribeResult subscribeResult)
        {
            var subackPacket = new MqttSubAckPacket
            {
                PacketIdentifier = subscribePacket.PacketIdentifier
            };

            subackPacket.ReasonCodes.AddRange(subscribeResult.ReasonCodes);

            return subackPacket;
        }

        /// <summary>
        /// 由取消订阅选项创建 UNSUBSCRIBE 报文，携带用户属性
        /// </summary>
        /// <param name="options">客户端取消订阅选项，不能为 null</param>
        /// <returns>UNSUBSCRIBE 报文</returns>
        public MqttUnsubscribePacket CreateUnsubscribePacket(MqttClientUnsubscribeOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var packet = new MqttUnsubscribePacket
            {
                Properties = new MqttUnsubscribePacketProperties()
            };

            packet.TopicFilters.AddRange(options.TopicFilters);
            packet.Properties.UserProperties = options.UserProperties;

            return packet;
        }

        /// <summary>
        /// 由 UNSUBSCRIBE 报文与原因码集合创建 UNSUBACK 报文
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
        /// 由客户端断开选项创建 DISCONNECT 报文，选项为 null 时按正常断开处理且不带属性
        /// </summary>
        /// <param name="options">客户端断开选项，可为 null</param>
        /// <returns>DISCONNECT 报文</returns>
        public MqttDisconnectPacket CreateDisconnectPacket(MqttClientDisconnectOptions options)
        {
            var packet = new MqttDisconnectPacket
            {
                ReasonCode = options == null
                    ? MqttDisconnectReasonCode.NormalDisconnection
                    : (MqttDisconnectReasonCode)options.ReasonCode
            };

            if (options != null &&
                (options.SessionExpiryInterval.HasValue || options.UserProperties != null || options.ReasonString != null))
            {
                packet.Properties = new MqttDisconnectPacketProperties
                {
                    SessionExpiryInterval = options.SessionExpiryInterval,
                    UserProperties = options.UserProperties,
                    ReasonString = options.ReasonString
                };
            }

            return packet;
        }

        /// <summary>
        /// 由 PUBACK 报文生成 QoS 1 发布结果，报文为 null 时按无响应处理并视为成功
        /// </summary>
        /// <param name="pubAckPacket">收到的 PUBACK 报文，可为 null</param>
        /// <returns>客户端发布结果，携带原因码、原因字符串与用户属性</returns>
        public MqttClientPublishResult CreatePublishResult(MqttPubAckPacket pubAckPacket)
        {
            var result = new MqttClientPublishResult
            {
                ReasonCode = MqttClientPublishReasonCode.Success,
                ReasonString = pubAckPacket?.Properties?.ReasonString,
                UserProperties = pubAckPacket?.Properties?.UserProperties
            };

            if (pubAckPacket != null)
            {
                // QoS 0 has no response. So we treat it as a success always.
                // Both enums have the same values. So it can be easily converted.
                result.ReasonCode = (MqttClientPublishReasonCode)pubAckPacket.ReasonCode;

                result.PacketIdentifier = pubAckPacket.PacketIdentifier;
            }

            return result;
        }

        /// <summary>
        /// 由 PUBREC 与 PUBCOMP 报文生成 QoS 2 发布结果，以 PUBCOMP 为准，报文标识符未找到时按未指明错误返回
        /// </summary>
        /// <param name="pubRecPacket">收到的 PUBREC 报文</param>
        /// <param name="pubCompPacket">收到的 PUBCOMP 报文</param>
        /// <returns>客户端发布结果，任一报文为 null 时返回未指明错误</returns>
        public MqttClientPublishResult CreatePublishResult(MqttPubRecPacket pubRecPacket, MqttPubCompPacket pubCompPacket)
        {
            if (pubRecPacket == null || pubCompPacket == null)
            {
                return new MqttClientPublishResult
                {
                    ReasonCode = MqttClientPublishReasonCode.UnspecifiedError
                };
            }

            // The PUBCOMP is the last packet in QoS 2. So we use the results from that instead of PUBREC.
            if (pubCompPacket.ReasonCode == MqttPubCompReasonCode.PacketIdentifierNotFound)
            {
                return new MqttClientPublishResult
                {
                    PacketIdentifier = pubCompPacket.PacketIdentifier,
                    ReasonCode = MqttClientPublishReasonCode.UnspecifiedError,
                    ReasonString = pubCompPacket.Properties?.ReasonString,
                    UserProperties = pubCompPacket.Properties?.UserProperties
                };
            }

            var result = new MqttClientPublishResult
            {
                PacketIdentifier = pubCompPacket.PacketIdentifier,
                ReasonCode = MqttClientPublishReasonCode.Success,
                ReasonString = pubCompPacket.Properties?.ReasonString,
                UserProperties = pubCompPacket.Properties?.UserProperties
            };

            if (pubRecPacket.ReasonCode.HasValue)
            {
                // Both enums share the same values.
                result.ReasonCode = (MqttClientPublishReasonCode)pubRecPacket.ReasonCode.Value;
            }

            return result;
        }
    }
}
