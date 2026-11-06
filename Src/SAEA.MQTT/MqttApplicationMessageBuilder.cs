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
*命名空间：SAEA.MQTT
*文件名： MqttApplicationMessageBuilder
*版本号： v26.4.23.1
*唯一标识：aad7044c-f7e2-49bf-9b6e-a19760cfff63
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttApplicationMessageBuilder接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SAEA.MQTT.Exceptions;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT
{
    /// <summary>
    /// 应用消息构造器，链式设置主题、载荷与 MQTT 5.0 扩展属性
    /// </summary>
    public class MqttApplicationMessageBuilder
    {
        private MqttQualityOfServiceLevel _qualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce;
        private string _topic;
        private byte[] _payload;
        private bool _retain;
        private string _contentType;
        private string _responseTopic;
        private byte[] _correlationData;
        private ushort? _topicAlias;
        private List<uint> _subscriptionIdentifiers;
        private uint? _messageExpiryInterval;
        private MqttPayloadFormatIndicator? _payloadFormatIndicator;
        private List<MqttUserProperty> _userProperties;

        /// <summary>
        /// 设置消息主题
        /// </summary>
        /// <param name="topic">消息主题，不做通配符校验</param>
        /// <returns>当前构造器，便于链式调用</returns>
        public MqttApplicationMessageBuilder WithTopic(string topic)
        {
            _topic = topic;
            return this;
        }

        /// <summary>
        /// 以字节数组设置载荷，直接引用传入数组，不做复制
        /// </summary>
        /// <param name="payload">载荷字节数组，为null表示不携带载荷</param>
        /// <returns>当前构造器，便于链式调用</returns>
        public MqttApplicationMessageBuilder WithPayload(byte[] payload)
        {
            _payload = payload;
            return this;
        }

        /// <summary>
        /// 以字节序列设置载荷，传入数组时直接引用，否则复制为数组
        /// </summary>
        /// <param name="payload">载荷字节序列，为null时清空载荷</param>
        /// <returns>当前构造器，便于链式调用</returns>
        public MqttApplicationMessageBuilder WithPayload(IEnumerable<byte> payload)
        {
            if (payload == null)
            {
                _payload = null;
                return this;
            }

            _payload = payload as byte[];

            if (_payload == null)
            {
                _payload = payload.ToArray();
            }

            return this;
        }

        /// <summary>
        /// 从流当前位置起读取剩余全部内容作为载荷
        /// </summary>
        /// <param name="payload">待读取的流，需支持 Length 与 Read，为null时清空载荷</param>
        /// <returns>当前构造器，便于链式调用</returns>
        public MqttApplicationMessageBuilder WithPayload(Stream payload)
        {
            if (payload == null)
            {
                _payload = null;
                return this;
            }

            return WithPayload(payload, payload.Length - payload.Position);
        }

        /// <summary>
        /// 从流当前位置起读取指定长度作为载荷
        /// </summary>
        /// <param name="payload">待读取的流，为null或总长度为0时清空载荷</param>
        /// <param name="length">读取的字节数，应与流中剩余可读长度一致</param>
        /// <returns>当前构造器，便于链式调用</returns>
        public MqttApplicationMessageBuilder WithPayload(Stream payload, long length)
        {
            if (payload == null)
            {
                _payload = null;
                return this;
            }

            if (payload.Length == 0)
            {
                _payload = null;
            }
            else
            {
                _payload = new byte[length];
                payload.Read(_payload, 0, _payload.Length);
            }

            return this;
        }

        /// <summary>
        /// 以 UTF-8 编码设置载荷，null 或空字符串时清空载荷
        /// </summary>
        /// <param name="payload">作为载荷的文本，按 UTF-8 编码为字节</param>
        /// <returns>当前构造器，便于链式调用</returns>
        public MqttApplicationMessageBuilder WithPayload(string payload)
        {
            if (payload == null)
            {
                _payload = null;
                return this;
            }

            _payload = string.IsNullOrEmpty(payload) ? null : Encoding.UTF8.GetBytes(payload);
            return this;
        }

        /// <summary>
        /// 设置服务质量等级
        /// </summary>
        /// <param name="qualityOfServiceLevel">服务质量等级，不做取值校验</param>
        /// <returns>当前构造器，便于链式调用</returns>
        public MqttApplicationMessageBuilder WithQualityOfServiceLevel(MqttQualityOfServiceLevel qualityOfServiceLevel)
        {
            _qualityOfServiceLevel = qualityOfServiceLevel;
            return this;
        }

        /// <summary>
        /// 以整数形式设置服务质量等级
        /// </summary>
        /// <param name="qualityOfServiceLevel">服务质量等级，取值 0-2，超出范围抛 ArgumentOutOfRangeException</param>
        /// <returns>当前构造器，便于链式调用</returns>
        public MqttApplicationMessageBuilder WithQualityOfServiceLevel(int qualityOfServiceLevel)
        {
            if (qualityOfServiceLevel < 0 || qualityOfServiceLevel > 2)
            {
                throw new ArgumentOutOfRangeException(nameof(qualityOfServiceLevel));
            }

            return WithQualityOfServiceLevel((MqttQualityOfServiceLevel)qualityOfServiceLevel);
        }

        /// <summary>
        /// 设置保留消息标志
        /// </summary>
        /// <param name="value">为 true 时作为保留消息发布，默认 true</param>
        /// <returns>当前构造器，便于链式调用</returns>
        public MqttApplicationMessageBuilder WithRetainFlag(bool value = true)
        {
            _retain = value;
            return this;
        }

        /// <summary>
        /// 使用至少一次（QoS 1）投递
        /// </summary>
        /// <returns>当前构造器，便于链式调用</returns>
        public MqttApplicationMessageBuilder WithAtLeastOnceQoS()
        {
            _qualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce;
            return this;
        }

        /// <summary>
        /// 使用至多一次（QoS 0）投递
        /// </summary>
        /// <returns>当前构造器，便于链式调用</returns>
        public MqttApplicationMessageBuilder WithAtMostOnceQoS()
        {
            _qualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce;
            return this;
        }

        /// <summary>
        /// 使用恰好一次（QoS 2）投递
        /// </summary>
        /// <returns>当前构造器，便于链式调用</returns>
        public MqttApplicationMessageBuilder WithExactlyOnceQoS()
        {
            _qualityOfServiceLevel = MqttQualityOfServiceLevel.ExactlyOnce;
            return this;
        }

        /// <summary>
        /// 追加一条用户属性，名称或值为null时抛 ArgumentNullException，仅在 MQTT 5.0 下生效
        /// </summary>
        /// <param name="name">属性名，不能为null</param>
        /// <param name="value">属性值，不能为null</param>
        /// <returns>当前构造器，便于链式调用</returns>
        public MqttApplicationMessageBuilder WithUserProperty(string name, string value)
        {
            if (_userProperties == null)
            {
                _userProperties = new List<MqttUserProperty>();
            }

            _userProperties.Add(new MqttUserProperty(name, value));
            return this;
        }

        /// <summary>
        /// 设置内容类型，仅在 MQTT 5.0 下生效
        /// </summary>
        /// <param name="contentType">载荷的 MIME 类型描述</param>
        /// <returns>当前构造器，便于链式调用</returns>
        public MqttApplicationMessageBuilder WithContentType(string contentType)
        {
            _contentType = contentType;
            return this;
        }

        /// <summary>
        /// 设置响应主题，仅在 MQTT 5.0 下生效
        /// </summary>
        /// <param name="responseTopic">接收方回复时使用的主题</param>
        /// <returns>当前构造器，便于链式调用</returns>
        public MqttApplicationMessageBuilder WithResponseTopic(string responseTopic)
        {
            _responseTopic = responseTopic;
            return this;
        }

        /// <summary>
        /// 设置关联数据，仅在 MQTT 5.0 下生效
        /// </summary>
        /// <param name="correlationData">关联请求与响应的数据，直接引用传入数组</param>
        /// <returns>当前构造器，便于链式调用</returns>
        public MqttApplicationMessageBuilder WithCorrelationData(byte[] correlationData)
        {
            _correlationData = correlationData;
            return this;
        }

        /// <summary>
        /// 设置主题别名，仅在 MQTT 5.0 下生效
        /// </summary>
        /// <param name="topicAlias">主题别名，构建时取值 0 会被判为协议违规</param>
        /// <returns>当前构造器，便于链式调用</returns>
        public MqttApplicationMessageBuilder WithTopicAlias(ushort topicAlias)
        {
            _topicAlias = topicAlias;
            return this;
        }

        /// <summary>
        /// 追加一个订阅标识符，仅在 MQTT 5.0 下生效
        /// </summary>
        /// <param name="subscriptionIdentifier">订阅标识符，重复调用会累积多个</param>
        /// <returns>当前构造器，便于链式调用</returns>
        public MqttApplicationMessageBuilder WithSubscriptionIdentifier(uint subscriptionIdentifier)
        {
            if (_subscriptionIdentifiers == null)
            {
                _subscriptionIdentifiers = new List<uint>();
            }

            _subscriptionIdentifiers.Add(subscriptionIdentifier);
            return this;
        }

        /// <summary>
        /// 设置消息过期间隔，仅在 MQTT 5.0 下生效
        /// </summary>
        /// <param name="messageExpiryInterval">过期间隔，单位为秒</param>
        /// <returns>当前构造器，便于链式调用</returns>
        public MqttApplicationMessageBuilder WithMessageExpiryInterval(uint messageExpiryInterval)
        {
            _messageExpiryInterval = messageExpiryInterval;
            return this;
        }

        /// <summary>
        /// 设置载荷格式指示，仅在 MQTT 5.0 下生效
        /// </summary>
        /// <param name="payloadFormatIndicator">载荷格式指示，标明载荷是二进制还是 UTF-8 文本</param>
        /// <returns>当前构造器，便于链式调用</returns>
        public MqttApplicationMessageBuilder WithPayloadFormatIndicator(MqttPayloadFormatIndicator payloadFormatIndicator)
        {
            _payloadFormatIndicator = payloadFormatIndicator;
            return this;
        }

        /// <summary>
        /// 构建应用消息实例，主题与主题别名均为空或别名为0时抛 MqttProtocolViolationException
        /// </summary>
        /// <returns>按当前设置生成的应用消息</returns>
        public MqttApplicationMessage Build()
        {
            if (!_topicAlias.HasValue && string.IsNullOrEmpty(_topic))
            {
                throw new MqttProtocolViolationException("Topic or TopicAlias is not set.");
            }

            if (_topicAlias == 0)
            {
                throw new MqttProtocolViolationException("A Topic Alias of 0 is not permitted. A sender MUST NOT send a PUBLISH packet containing a Topic Alias which has the value 0 [MQTT-3.3.2-8].");
            }

            var applicationMessage = new MqttApplicationMessage
            {
                Topic = _topic,
                Payload = _payload,
                QualityOfServiceLevel = _qualityOfServiceLevel,
                Retain = _retain,
                ContentType = _contentType,
                ResponseTopic = _responseTopic,
                CorrelationData = _correlationData,
                TopicAlias = _topicAlias,
                SubscriptionIdentifiers = _subscriptionIdentifiers,
                MessageExpiryInterval = _messageExpiryInterval,
                PayloadFormatIndicator = _payloadFormatIndicator,
                UserProperties = _userProperties
            };

            return applicationMessage;
        }
    }
}
