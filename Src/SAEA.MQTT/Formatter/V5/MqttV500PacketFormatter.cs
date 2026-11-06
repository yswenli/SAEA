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
*唯一标识：6e19b30b-7fd9-40ef-bb86-8d11cdace93a
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttV500PacketFormatter类
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
using SAEA.MQTT.Adapter;
using SAEA.MQTT.Packets;

namespace SAEA.MQTT.Formatter.V5
{
    /// <summary>
    /// MQTT 5.0 报文编解码器，组合编码器与解码器完成报文与字节缓冲区之间的转换
    /// </summary>
    public sealed class MqttV500PacketFormatter : IMqttPacketFormatter
    {
        readonly MqttV500PacketDecoder _decoder = new MqttV500PacketDecoder();
        readonly MqttV500PacketEncoder _encoder;

        /// <summary>
        /// 绑定报文写入器并创建编码器
        /// </summary>
        /// <param name="writer">报文写入器</param>
        public MqttV500PacketFormatter(IMqttPacketWriter writer)
        {
            _encoder = new MqttV500PacketEncoder(writer);
        }

        /// <summary>
        /// MQTT 5.0 的数据转换器
        /// </summary>
        public IMqttDataConverter DataConverter { get; } = new MqttV500DataConverter();
        
        /// <summary>
        /// 编码报文
        /// </summary>
        /// <param name="mqttPacket">待编码的报文，不能为 null</param>
        /// <returns>编码结果片段，仅在调用 FreeBuffer 之前有效</returns>
        public ArraySegment<byte> Encode(MqttBasePacket mqttPacket)
        {
            if (mqttPacket == null) throw new ArgumentNullException(nameof(mqttPacket));

            return _encoder.Encode(mqttPacket);
        }

        /// <summary>
        /// 解码报文
        /// </summary>
        /// <param name="receivedMqttPacket">已解析固定头的原始报文，不能为 null</param>
        /// <returns>解码后的报文</returns>
        public MqttBasePacket Decode(ReceivedMqttPacket receivedMqttPacket)
        {
            if (receivedMqttPacket == null) throw new ArgumentNullException(nameof(receivedMqttPacket));

            return _decoder.Decode(receivedMqttPacket);
        }
        
        /// <summary>
        /// 释放编码器的缓冲区
        /// </summary>
        public void FreeBuffer()
        {
            _encoder.FreeBuffer();
        }
    }
}
