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
*唯一标识：652c3d18-ec8a-48ce-902d-9ddc9edc609d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttV500PropertiesWriter接口
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
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Formatter.V5
{
    /// <summary>
    /// MQTT 5.0 属性写入器，把属性按标识加值的形式累积到内部缓冲区，再整体写入报文体
    /// </summary>
    public sealed class MqttV500PropertiesWriter
    {
        readonly MqttPacketWriter _packetWriter = new MqttPacketWriter();

        /// <summary>
        /// 已累积的属性字节数，不含属性段前的变长长度字段
        /// </summary>
        public int Length => _packetWriter.Length;

        /// <summary>
        /// 逐项写入用户属性，null 或空集合时不写入
        /// </summary>
        /// <param name="userProperties">待写入的用户属性集合</param>
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

        /// <summary>
        /// 写入关联数据属性，值为 null 时不写入
        /// </summary>
        /// <param name="value">待写入的二进制关联数据</param>
        public void WriteCorrelationData(byte[] value)
        {
            Write(MqttPropertyId.CorrelationData, value);
        }

        /// <summary>
        /// 写入认证数据属性，值为 null 时不写入
        /// </summary>
        /// <param name="value">待写入的二进制认证数据</param>
        public void WriteAuthenticationData(byte[] value)
        {
            Write(MqttPropertyId.AuthenticationData, value);
        }

        /// <summary>
        /// 写入原因字符串属性，值为 null 时不写入
        /// </summary>
        /// <param name="value">待写入的原因说明文本</param>
        public void WriteReasonString(string value)
        {
            Write(MqttPropertyId.ReasonString, value);
        }

        /// <summary>
        /// 写入响应主题属性，值为 null 时不写入
        /// </summary>
        /// <param name="value">接收方回发响应时使用的主题</param>
        public void WriteResponseTopic(string value)
        {
            Write(MqttPropertyId.ResponseTopic, value);
        }

        /// <summary>
        /// 写入内容类型属性，值为 null 时不写入
        /// </summary>
        /// <param name="value">载荷内容的 MIME 类型描述</param>
        public void WriteContentType(string value)
        {
            Write(MqttPropertyId.ContentType, value);
        }

        /// <summary>
        /// 写入服务端参考地址属性，值为 null 时不写入
        /// </summary>
        /// <param name="value">服务端建议客户端使用的另一服务地址</param>
        public void WriteServerReference(string value)
        {
            Write(MqttPropertyId.ServerReference, value);
        }

        /// <summary>
        /// 写入认证方法属性，值为 null 时不写入
        /// </summary>
        /// <param name="value">认证方式名称</param>
        public void WriteAuthenticationMethod(string value)
        {
            Write(MqttPropertyId.AuthenticationMethod, value);
        }

        /// <summary>
        /// 先把已累积属性的总字节数写成变长整数前缀，再把这些属性整体写入目标写入器
        /// </summary>
        /// <param name="packetWriter">接收属性段的目标写入器</param>
        public void WriteTo(IMqttPacketWriter packetWriter)
        {
            if (packetWriter == null) throw new ArgumentNullException(nameof(packetWriter));

            packetWriter.WriteVariableLengthInteger((uint)_packetWriter.Length);
            packetWriter.Write(_packetWriter);
        }

        /// <summary>
        /// 以四字节整数写入会话过期间隔属性，值为 null 时不写入
        /// </summary>
        /// <param name="value">会话过期时间，单位秒</param>
        public void WriteSessionExpiryInterval(uint? value)
        {
            WriteAsFourByteInteger(MqttPropertyId.SessionExpiryInterval, value);
        }

        /// <summary>
        /// 以变长整数写入单个订阅标识符属性，值为 null 时不写入
        /// </summary>
        /// <param name="value">订阅标识符</param>
        public void WriteSubscriptionIdentifier(uint? value)
        {
            WriteAsVariableLengthInteger(MqttPropertyId.SubscriptionIdentifier, value);
        }

        /// <summary>
        /// 逐个写入多个订阅标识符属性，集合为 null 时不写入
        /// </summary>
        /// <param name="value">订阅标识符集合</param>
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

        /// <summary>
        /// 以大端序两字节写入主题别名属性，值为 null 时不写入
        /// </summary>
        /// <param name="value">主题别名</param>
        public void WriteTopicAlias(ushort? value)
        {
            Write(MqttPropertyId.TopicAlias, value);
        }

        /// <summary>
        /// 以四字节整数写入消息过期间隔属性，值为 null 时不写入
        /// </summary>
        /// <param name="value">消息过期时间，单位秒</param>
        public void WriteMessageExpiryInterval(uint? value)
        {
            WriteAsFourByteInteger(MqttPropertyId.MessageExpiryInterval, value);
        }

        /// <summary>
        /// 以单字节写入载荷格式指示属性，值为 null 时不写入
        /// </summary>
        /// <param name="value">载荷格式指示，UTF-8 或未指定</param>
        public void WritePayloadFormatIndicator(MqttPayloadFormatIndicator? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            Write(MqttPropertyId.PayloadFormatIndicator, (byte)value.Value);
        }

        /// <summary>
        /// 以四字节整数写入遗嘱延迟间隔属性，值为 null 时不写入
        /// </summary>
        /// <param name="value">遗嘱延迟时间，单位秒</param>
        public void WriteWillDelayInterval(uint? value)
        {
            WriteAsFourByteInteger(MqttPropertyId.WillDelayInterval, value);
        }

        /// <summary>
        /// 以单字节写入请求问题信息属性，true 写 1、false 写 0，值为 null 时不写入
        /// </summary>
        /// <param name="value">是否请求服务端在响应中携带问题信息</param>
        public void WriteRequestProblemInformation(bool? value)
        {
            Write(MqttPropertyId.RequestProblemInformation, value);
        }

        /// <summary>
        /// 以单字节写入请求响应信息属性，true 写 1、false 写 0，值为 null 时不写入
        /// </summary>
        /// <param name="value">是否请求服务端在响应中携带响应信息</param>
        public void WriteRequestResponseInformation(bool? value)
        {
            Write(MqttPropertyId.RequestResponseInformation, value);
        }

        /// <summary>
        /// 以大端序两字节写入接收最大值属性，值为 null 时不写入
        /// </summary>
        /// <param name="value">客户端可同时处理的未确认消息数上限</param>
        public void WriteReceiveMaximum(ushort? value)
        {
            Write(MqttPropertyId.ReceiveMaximum, value);
        }

        /// <summary>
        /// 以单字节写入最大 QoS 属性，QoS 2 或 null 时不写入
        /// </summary>
        /// <param name="value">服务端支持的最高 QoS 等级</param>
        public void WriteMaximumQoS(MqttQualityOfServiceLevel? value)
        {
            if (!value.HasValue || value.Value > MqttQualityOfServiceLevel.AtLeastOnce)
            {
                return;
            }

            _packetWriter.Write((byte)MqttPropertyId.MaximumQoS);
            _packetWriter.Write((byte)value.Value);
        }

        /// <summary>
        /// 以四字节整数写入最大报文长度属性，值为 null 时不写入
        /// </summary>
        /// <param name="value">可接受的最大报文总字节数</param>
        public void WriteMaximumPacketSize(uint? value)
        {
            WriteAsFourByteInteger(MqttPropertyId.MaximumPacketSize, value);
        }

        /// <summary>
        /// 以单字节写入保留消息可用属性，true 写 1、false 写 0，值为 null 时不写入
        /// </summary>
        /// <param name="value">服务端是否支持保留消息</param>
        public void WriteRetainAvailable(bool? value)
        {
            Write(MqttPropertyId.RetainAvailable, value);
        }

        /// <summary>
        /// 写入服务端分配的客户端标识属性，值为 null 时不写入
        /// </summary>
        /// <param name="value">服务端分配的客户端标识</param>
        public void WriteAssignedClientIdentifier(string value)
        {
            Write(MqttPropertyId.AssignedClientIdentifier, value);
        }

        /// <summary>
        /// 以大端序两字节写入主题别名最大值属性，值为 null 时不写入
        /// </summary>
        /// <param name="value">服务端允许客户端使用的最大主题别名</param>
        public void WriteTopicAliasMaximum(ushort? value)
        {
            Write(MqttPropertyId.TopicAliasMaximum, value);
        }

        /// <summary>
        /// 以单字节写入通配订阅可用属性，true 写 1、false 写 0，值为 null 时不写入
        /// </summary>
        /// <param name="value">服务端是否支持通配符订阅</param>
        public void WriteWildcardSubscriptionAvailable(bool? value)
        {
            Write(MqttPropertyId.WildcardSubscriptionAvailable, value);
        }

        /// <summary>
        /// 以单字节写入订阅标识符可用属性，true 写 1、false 写 0，值为 null 时不写入
        /// </summary>
        /// <param name="value">服务端是否支持订阅标识符</param>
        public void WriteSubscriptionIdentifiersAvailable(bool? value)
        {
            Write(MqttPropertyId.SubscriptionIdentifiersAvailable, value);
        }

        /// <summary>
        /// 以单字节写入共享订阅可用属性，true 写 1、false 写 0，值为 null 时不写入
        /// </summary>
        /// <param name="value">服务端是否支持共享订阅</param>
        public void WriteSharedSubscriptionAvailable(bool? value)
        {
            Write(MqttPropertyId.SharedSubscriptionAvailable, value);
        }

        /// <summary>
        /// 以大端序两字节写入服务端保活时间属性，值为 null 时不写入
        /// </summary>
        /// <param name="value">服务端要求的保活间隔，单位秒</param>
        public void WriteServerKeepAlive(ushort? value)
        {
            Write(MqttPropertyId.ServerKeepAlive, value);
        }

        /// <summary>
        /// 写入响应信息属性，值为 null 时不写入
        /// </summary>
        /// <param name="value">服务端返回的响应信息文本</param>
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
