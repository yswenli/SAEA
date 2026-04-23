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
*文件名： MqttV500PacketFormatter
*版本号： v26.4.23.1
*唯一标识：d41ade57-2d17-467a-ac89-53acd02761e7
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;
using SAEA.MQTT.Adapter;
using SAEA.MQTT.Packets;

namespace SAEA.MQTT.Formatter.V5
{
    public sealed class MqttV500PacketFormatter : IMqttPacketFormatter
    {
        readonly MqttV500PacketDecoder _decoder = new MqttV500PacketDecoder();
        readonly MqttV500PacketEncoder _encoder;

        public MqttV500PacketFormatter(IMqttPacketWriter writer)
        {
            _encoder = new MqttV500PacketEncoder(writer);
        }

        public IMqttDataConverter DataConverter { get; } = new MqttV500DataConverter();
        
        public ArraySegment<byte> Encode(MqttBasePacket mqttPacket)
        {
            if (mqttPacket == null) throw new ArgumentNullException(nameof(mqttPacket));

            return _encoder.Encode(mqttPacket);
        }

        public MqttBasePacket Decode(ReceivedMqttPacket receivedMqttPacket)
        {
            if (receivedMqttPacket == null) throw new ArgumentNullException(nameof(receivedMqttPacket));

            return _decoder.Decode(receivedMqttPacket);
        }
        
        public void FreeBuffer()
        {
            _encoder.FreeBuffer();
        }
    }
}
