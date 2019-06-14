/****************************************************************************
*项目名称：SAEA.MQTT
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Core.Implementations
*类 名 称：MqttMessageBuilder
*版 本 号： v4.5.6.7
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/14 19:00:57
*描述：
*=====================================================================
*修改时间：2019/1/14 19:00:57
*修 改 人： yswenli
*版 本 号： v4.5.6.7
*描    述：
*****************************************************************************/
using SAEA.MQTT.Core.Protocol;
using SAEA.MQTT.Exceptions;
using SAEA.MQTT.Model;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SAEA.MQTT.Core.Implementations
{
    public class MqttMessageBuilder
    {
        private MqttQualityOfServiceLevel _qualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce;
        private string _topic;
        private byte[] _payload;
        private bool _retain;

        public MqttMessageBuilder WithTopic(string topic)
        {
            _topic = topic;
            return this;
        }

        public MqttMessageBuilder WithPayload(IEnumerable<byte> payload)
        {
            if (payload == null)
            {
                _payload = null;
                return this;
            }

            _payload = payload.ToArray();
            return this;
        }

        public MqttMessageBuilder WithPayload(Stream payload)
        {
            return WithPayload(payload, payload.Length - payload.Position);
        }

        public MqttMessageBuilder WithPayload(Stream payload, long length)
        {
            if (payload == null)
            {
                _payload = null;
                return this;
            }

            if (payload.Length == 0)
            {
                _payload = new byte[0];
            }
            else
            {
                _payload = new byte[length];
                payload.Read(_payload, 0, _payload.Length);
            }

            return this;
        }

        public MqttMessageBuilder WithPayload(string payload)
        {
            if (payload == null)
            {
                _payload = null;
                return this;
            }

            _payload = string.IsNullOrEmpty(payload) ? new byte[0] : Encoding.UTF8.GetBytes(payload);
            return this;
        }

        public MqttMessageBuilder WithQualityOfServiceLevel(MqttQualityOfServiceLevel qualityOfServiceLevel)
        {
            _qualityOfServiceLevel = qualityOfServiceLevel;
            return this;
        }

        public MqttMessageBuilder WithRetainFlag(bool value = true)
        {
            _retain = value;
            return this;
        }

        public MqttMessageBuilder WithAtLeastOnceQoS()
        {
            _qualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce;
            return this;
        }

        public MqttMessageBuilder WithAtMostOnceQoS()
        {
            _qualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce;
            return this;
        }

        public MqttMessageBuilder WithExactlyOnceQoS()
        {
            _qualityOfServiceLevel = MqttQualityOfServiceLevel.ExactlyOnce;
            return this;
        }

        public MqttMessage Build()
        {
            if (string.IsNullOrEmpty(_topic))
            {
                throw new MqttProtocolViolationException("Topic is not set.");
            }

            return new MqttMessage
            {
                Topic = _topic,
                Payload = _payload ?? new byte[0],
                QualityOfServiceLevel = _qualityOfServiceLevel,
                Retain = _retain
            };
        }
    }
}
