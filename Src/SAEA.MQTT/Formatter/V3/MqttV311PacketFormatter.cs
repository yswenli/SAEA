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
*文件名： MqttV311PacketFormatter
*版本号： v26.4.23.1
*唯一标识：9a94668b-4aec-4320-ad31-9baeb90ccf0b
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttV311PacketFormatter类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.MQTT.Exceptions;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Formatter.V3
{
    /// <summary>
    /// MQTT 3.1.1 报文编解码器，在 3.1.0 实现基础上替换连接报文的协议名、协议级别与连接确认标志
    /// </summary>
    public sealed class MqttV311PacketFormatter : MqttV310PacketFormatter
    {
        /// <summary>
        /// 绑定报文写入器
        /// </summary>
        /// <param name="packetWriter">报文写入器，编码结果写入其内部缓冲区</param>
        public MqttV311PacketFormatter(IMqttPacketWriter packetWriter)
            : base(packetWriter)
        {
        }

        /// <summary>
        /// 编码 CONNECT 报文，协议名为 MQTT、协议级别为 4，只设置用户名时不允许携带密码
        /// </summary>
        /// <param name="packet">待编码的 CONNECT 报文</param>
        /// <param name="packetWriter">报文写入器</param>
        /// <returns>固定头首字节</returns>
        protected override byte EncodeConnectPacket(MqttConnectPacket packet, IMqttPacketWriter packetWriter)
        {
            ValidateConnectPacket(packet);

            packetWriter.WriteWithLengthPrefix("MQTT");
            packetWriter.Write(4); // 3.1.2.2 Protocol Level 4

            byte connectFlags = 0x0;
            if (packet.CleanSession)
            {
                connectFlags |= 0x2;
            }

            if (packet.WillMessage != null)
            {
                connectFlags |= 0x4;
                connectFlags |= (byte)((byte)packet.WillMessage.QualityOfServiceLevel << 3);

                if (packet.WillMessage.Retain)
                {
                    connectFlags |= 0x20;
                }
            }

            if (packet.Password != null && packet.Username == null)
            {
                throw new MqttProtocolViolationException("If the User Name Flag is set to 0, the Password Flag MUST be set to 0 [MQTT-3.1.2-22].");
            }

            if (packet.Password != null)
            {
                connectFlags |= 0x40;
            }

            if (packet.Username != null)
            {
                connectFlags |= 0x80;
            }

            packetWriter.Write(connectFlags);
            packetWriter.Write(packet.KeepAlivePeriod);
            packetWriter.WriteWithLengthPrefix(packet.ClientId);

            if (packet.WillMessage != null)
            {
                packetWriter.WriteWithLengthPrefix(packet.WillMessage.Topic);
                packetWriter.WriteWithLengthPrefix(packet.WillMessage.Payload);
            }

            if (packet.Username != null)
            {
                packetWriter.WriteWithLengthPrefix(packet.Username);
            }

            if (packet.Password != null)
            {
                packetWriter.WriteWithLengthPrefix(packet.Password);
            }

            return MqttPacketWriter.BuildFixedHeader(MqttControlPacketType.Connect);
        }

        /// <summary>
        /// 编码 CONNACK 报文，用连接确认标志表示会话是否已存在
        /// </summary>
        /// <param name="packet">待编码的 CONNACK 报文</param>
        /// <param name="packetWriter">报文写入器</param>
        /// <returns>固定头首字节</returns>
        protected override byte EncodeConnAckPacket(MqttConnAckPacket packet, IMqttPacketWriter packetWriter)
        {
            byte connectAcknowledgeFlags = 0x0;
            if (packet.IsSessionPresent)
            {
                connectAcknowledgeFlags |= 0x1;
            }

            packetWriter.Write(connectAcknowledgeFlags);
            packetWriter.Write((byte)packet.ReturnCode);

            return MqttPacketWriter.BuildFixedHeader(MqttControlPacketType.ConnAck);
        }

        /// <summary>
        /// 解码 CONNACK 报文，从连接确认标志低 1 位读取会话是否已存在
        /// </summary>
        /// <param name="body">报文体读取器</param>
        /// <returns>CONNACK 报文</returns>
        protected override MqttBasePacket DecodeConnAckPacket(IMqttPacketBodyReader body)
        {
            ThrowIfBodyIsEmpty(body);

            var packet = new MqttConnAckPacket();

            var acknowledgeFlags = body.ReadByte();

            packet.IsSessionPresent = (acknowledgeFlags & 0x1) > 0;
            packet.ReturnCode = (MqttConnectReturnCode)body.ReadByte();

            return packet;
        }
    }
}
