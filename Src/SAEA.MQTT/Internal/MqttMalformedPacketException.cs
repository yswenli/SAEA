/****************************************************************************
*Copyright (c) 2022 Oceanstar
*FileName： MqttMalformedPacketException
*Description：MQTT 报文无法解析（固定头或正文结构非法）时抛出。
*用于把“解析失败”与“解析成功但违反协议”区分开，映射到 MQTT 5.0 的
*0x81 Malformed Packet 原因码。
*****************************************************************************/
using SAEA.MQTT.Exceptions;

namespace SAEA.MQTT.Internal
{
    internal sealed class MqttMalformedPacketException : MqttProtocolViolationException
    {
        public MqttMalformedPacketException(string message)
            : base(message)
        {
        }
    }
}