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
*文件名： MqttV500PropertiesReader
*版本号： v26.4.23.1
*唯一标识：398621a4-8b81-43b8-8145-4405bc08b4cf
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttV500PropertiesReader接口
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MqttV500PropertiesReader接口
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using SAEA.MQTT.Exceptions;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Formatter.V5
{
    public class MqttV500PropertiesReader
    {
        private readonly IMqttPacketBodyReader _body;
        private readonly int _length;
        private readonly int _targetOffset;

        public MqttV500PropertiesReader(IMqttPacketBodyReader body)
        {
            _body = body ?? throw new ArgumentNullException(nameof(body));

            if (!body.EndOfStream)
            {
                _length = (int)body.ReadVariableLengthInteger();
            }

            _targetOffset = body.Offset + _length;
        }

        public MqttPropertyId CurrentPropertyId { get; private set; }

        public bool MoveNext()
        {
            if (_length == 0)
            {
                return false;
            }

            if (_body.Offset >= _targetOffset)
            {
                return false;
            }

            CurrentPropertyId = (MqttPropertyId)_body.ReadByte();
            return true;
        }

        public void AddUserPropertyTo(List<MqttUserProperty> userProperties)
        {
            if (userProperties == null) throw new ArgumentNullException(nameof(userProperties));

            var name = _body.ReadStringWithLengthPrefix();
            var value = _body.ReadStringWithLengthPrefix();

            userProperties.Add(new MqttUserProperty(name, value));
        }

        public string ReadReasonString()
        {
            return _body.ReadStringWithLengthPrefix();
        }

        public string ReadAuthenticationMethod()
        {
            return _body.ReadStringWithLengthPrefix();
        }

        public byte[] ReadAuthenticationData()
        {
            return _body.ReadWithLengthPrefix();
        }

        public bool ReadRetainAvailable()
        {
            return _body.ReadBoolean();
        }

        public uint ReadSessionExpiryInterval()
        {
            return _body.ReadFourByteInteger();
        }

        public ushort ReadReceiveMaximum()
        {
            return _body.ReadTwoByteInteger();
        }

        public MqttQualityOfServiceLevel ReadMaximumQoS()
        {
            byte value = _body.ReadByte();
            if (value > 1)
            {
                throw new MqttProtocolViolationException($"Unexpected Maximum QoS value: {value}");
            }
            
            return (MqttQualityOfServiceLevel)value;
        }

        public string ReadAssignedClientIdentifier()
        {
            return _body.ReadStringWithLengthPrefix();
        }

        public string ReadServerReference()
        {
            return _body.ReadStringWithLengthPrefix();
        }

        public ushort ReadTopicAliasMaximum()
        {
            return _body.ReadTwoByteInteger();
        }

        public uint ReadMaximumPacketSize()
        {
            return _body.ReadFourByteInteger();
        }

        public ushort ReadServerKeepAlive()
        {
            return _body.ReadTwoByteInteger();
        }

        public string ReadResponseInformation()
        {
            return _body.ReadStringWithLengthPrefix();
        }

        public bool ReadSharedSubscriptionAvailable()
        {
            return _body.ReadBoolean();
        }

        public bool ReadSubscriptionIdentifiersAvailable()
        {
            return _body.ReadBoolean();
        }

        public bool ReadWildcardSubscriptionAvailable()
        {
            return _body.ReadBoolean();
        }

        public uint ReadSubscriptionIdentifier()
        {
            return _body.ReadVariableLengthInteger();
        }

        public MqttPayloadFormatIndicator? ReadPayloadFormatIndicator()
        {
            return (MqttPayloadFormatIndicator)_body.ReadByte();
        }

        public uint ReadMessageExpiryInterval()
        {
            return _body.ReadFourByteInteger();
        }

        public ushort ReadTopicAlias()
        {
            return _body.ReadTwoByteInteger();
        }

        public string ReadResponseTopic()
        {
            return _body.ReadStringWithLengthPrefix();
        }

        public byte[] ReadCorrelationData()
        {
            return _body.ReadWithLengthPrefix();
        }

        public string ReadContentType()
        {
            return _body.ReadStringWithLengthPrefix();
        }

        public uint ReadWillDelayInterval()
        {
            return _body.ReadFourByteInteger();
        }

        public bool RequestResponseInformation()
        {
            return _body.ReadBoolean();
        }

        public bool RequestProblemInformation()
        {
            return _body.ReadBoolean();
        }

        public void ThrowInvalidPropertyIdException(Type type)
        {
            throw new MqttProtocolViolationException($"Property ID '{CurrentPropertyId}' is not supported for package type '{type.Name}'.");
        }
    }
}
