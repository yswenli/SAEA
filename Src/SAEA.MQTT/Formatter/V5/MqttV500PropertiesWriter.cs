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
*文件名： MqttV500PropertiesWriter
*版本号： v26.4.23.1
*唯一标识：6835a576-7000-4482-9be3-b59ac3a66ffe
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MQTT数据格式化类
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MQTT数据格式化类
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Formatter.V5
{
    public sealed class MqttV500PropertiesWriter
    {
        readonly MqttPacketWriter _packetWriter = new MqttPacketWriter();

        public int Length => _packetWriter.Length;

        public void WriteUserProperties(List<MqttUserProperty> userProperties)
        {
            if (userProperties == null || userProperties.Count == 0)
            {
                return;
            }

            foreach (var property in userProperties)
            {
                _packetWriter.Write((byte)MqttPropertyId.UserProperty);
                _packetWriter.WriteWithLengthPrefix(property.Name);
                _packetWriter.WriteWithLengthPrefix(property.Value);
            }
        }

        public void WriteCorrelationData(byte[] value)
        {
            Write(MqttPropertyId.CorrelationData, value);
        }

        public void WriteAuthenticationData(byte[] value)
        {
            Write(MqttPropertyId.AuthenticationData, value);
        }

        public void WriteReasonString(string value)
        {
            Write(MqttPropertyId.ReasonString, value);
        }

        public void WriteResponseTopic(string value)
        {
            Write(MqttPropertyId.ResponseTopic, value);
        }

        public void WriteContentType(string value)
        {
            Write(MqttPropertyId.ContentType, value);
        }

        public void WriteServerReference(string value)
        {
            Write(MqttPropertyId.ServerReference, value);
        }

        public void WriteAuthenticationMethod(string value)
        {
            Write(MqttPropertyId.AuthenticationMethod, value);
        }

        public void WriteTo(IMqttPacketWriter packetWriter)
        {
            if (packetWriter == null) throw new ArgumentNullException(nameof(packetWriter));

            packetWriter.WriteVariableLengthInteger((uint)_packetWriter.Length);
            packetWriter.Write(_packetWriter);
        }

        public void WriteSessionExpiryInterval(uint? value)
        {
            WriteAsFourByteInteger(MqttPropertyId.SessionExpiryInterval, value);
        }

        public void WriteSubscriptionIdentifier(uint? value)
        {
            WriteAsVariableLengthInteger(MqttPropertyId.SubscriptionIdentifier, value);
        }

        public void WriteSubscriptionIdentifiers(IEnumerable<uint> value)
        {
            if (value == null)
            {
                return;
            }

            foreach (var subscriptionIdentifier in value)
            {
                WriteAsVariableLengthInteger(MqttPropertyId.SubscriptionIdentifier, subscriptionIdentifier);
            }
        }

        public void WriteTopicAlias(ushort? value)
        {
            Write(MqttPropertyId.TopicAlias, value);
        }

        public void WriteMessageExpiryInterval(uint? value)
        {
            WriteAsFourByteInteger(MqttPropertyId.MessageExpiryInterval, value);
        }

        public void WritePayloadFormatIndicator(MqttPayloadFormatIndicator? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            Write(MqttPropertyId.PayloadFormatIndicator, (byte)value.Value);
        }

        public void WriteWillDelayInterval(uint? value)
        {
            WriteAsFourByteInteger(MqttPropertyId.WillDelayInterval, value);
        }

        public void WriteRequestProblemInformation(bool? value)
        {
            Write(MqttPropertyId.RequestProblemInformation, value);
        }

        public void WriteRequestResponseInformation(bool? value)
        {
            Write(MqttPropertyId.RequestResponseInformation, value);
        }

        public void WriteReceiveMaximum(ushort? value)
        {
            Write(MqttPropertyId.ReceiveMaximum, value);
        }

        public void WriteMaximumQoS(MqttQualityOfServiceLevel? value)
        {
            if (!value.HasValue || value.Value > MqttQualityOfServiceLevel.AtLeastOnce)
            {
                return;
            }

            _packetWriter.Write((byte)MqttPropertyId.MaximumQoS);
            _packetWriter.Write((byte)value.Value);
        }

        public void WriteMaximumPacketSize(uint? value)
        {
            WriteAsFourByteInteger(MqttPropertyId.MaximumPacketSize, value);
        }

        public void WriteRetainAvailable(bool? value)
        {
            Write(MqttPropertyId.RetainAvailable, value);
        }

        public void WriteAssignedClientIdentifier(string value)
        {
            Write(MqttPropertyId.AssignedClientIdentifier, value);
        }

        public void WriteTopicAliasMaximum(ushort? value)
        {
            Write(MqttPropertyId.TopicAliasMaximum, value);
        }

        public void WriteWildcardSubscriptionAvailable(bool? value)
        {
            Write(MqttPropertyId.WildcardSubscriptionAvailable, value);
        }

        public void WriteSubscriptionIdentifiersAvailable(bool? value)
        {
            Write(MqttPropertyId.SubscriptionIdentifiersAvailable, value);
        }

        public void WriteSharedSubscriptionAvailable(bool? value)
        {
            Write(MqttPropertyId.SharedSubscriptionAvailable, value);
        }

        public void WriteServerKeepAlive(ushort? value)
        {
            Write(MqttPropertyId.ServerKeepAlive, value);
        }

        public void WriteResponseInformation(string value)
        {
            Write(MqttPropertyId.ResponseInformation, value);
        }

        void Write(MqttPropertyId id, bool? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            _packetWriter.Write((byte)id);
            _packetWriter.Write(value.Value ? (byte)0x1 : (byte)0x0);
        }

        void Write(MqttPropertyId id, byte? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            _packetWriter.Write((byte)id);
            _packetWriter.Write(value.Value);
        }

        void Write(MqttPropertyId id, ushort? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            _packetWriter.Write((byte)id);
            _packetWriter.Write(value.Value);
        }

        void WriteAsVariableLengthInteger(MqttPropertyId id, uint? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            _packetWriter.Write((byte)id);
            _packetWriter.WriteVariableLengthInteger(value.Value);
        }

        void WriteAsFourByteInteger(MqttPropertyId id, uint? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            _packetWriter.Write((byte)id);
            _packetWriter.Write((byte)(value.Value >> 24));
            _packetWriter.Write((byte)(value.Value >> 16));
            _packetWriter.Write((byte)(value.Value >> 8));
            _packetWriter.Write((byte)value.Value);
        }

        void Write(MqttPropertyId id, string value)
        {
            if (value == null)
            {
                return;
            }

            _packetWriter.Write((byte)id);
            _packetWriter.WriteWithLengthPrefix(value);
        }

        void Write(MqttPropertyId id, byte[] value)
        {
            if (value == null)
            {
                return;
            }

            _packetWriter.Write((byte)id);
            _packetWriter.WriteWithLengthPrefix(value);
        }
    }
}
