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
*命名空间：SAEA.MQTT.Formatter
*文件名： IMqttPacketFormatter
*版本号： v26.4.23.1
*唯一标识：70af0e1c-c94d-4ce7-95ee-b6d85fae209d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttPacketFormatter类
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

namespace SAEA.MQTT.Formatter
{
    /// <summary>
    /// MQTT 报文编解码器，按协议版本在报文对象与字节缓冲区之间转换
    /// </summary>
    public interface IMqttPacketFormatter
    {
        /// <summary>
        /// 该协议版本对应的数据转换器
        /// </summary>
        IMqttDataConverter DataConverter { get; }

        /// <summary>
        /// 将报文编码为字节缓冲区片段
        /// </summary>
        /// <param name="mqttPacket">待编码的报文</param>
        /// <returns>编码结果，仅在调用 FreeBuffer 之前有效</returns>
        ArraySegment<byte> Encode(MqttBasePacket mqttPacket);

        /// <summary>
        /// 将原始报文体解码为报文对象
        /// </summary>
        /// <param name="receivedMqttPacket">已解析固定头的原始报文</param>
        /// <returns>解码后的报文</returns>
        MqttBasePacket Decode(ReceivedMqttPacket receivedMqttPacket);

        /// <summary>
        /// 释放编码过程占用的缓冲区
        /// </summary>
        void FreeBuffer();
    }
}