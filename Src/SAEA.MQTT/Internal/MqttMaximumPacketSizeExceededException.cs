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
 *命名空间：SAEA.MQTT.Internal
 *文件名： MqttMaximumPacketSizeExceededException
 *版本号： v26.4.23.1
 *唯一标识：c81f6d24-7b52-4e39-a0d1-6f3b9e2a1106
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2021/03/11 16:46:45
 *描述：MqttMaximumPacketSizeExceededException类
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/05 00:00:00
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：MqttMaximumPacketSizeExceededException类
 *
 *****************************************************************************/
using SAEA.MQTT.Exceptions;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Internal
{
    internal enum MqttPacketSizeDirection
    {
        Inbound = 0,
        Outbound = 1
    }

    /// <summary>
    /// Maximum Packet Size 被突破时抛出。调用方根据方向与报文类型决定是丢弃还是断开连接。
    /// </summary>
    internal sealed class MqttMaximumPacketSizeExceededException : MqttProtocolViolationException
    {
        public MqttMaximumPacketSizeExceededException(
            MqttPacketSizeDirection direction,
            MqttControlPacketType? packetType,
            uint maximumPacketSize,
            int packetSize)
            : base("The MQTT packet size of " + packetSize + " bytes exceeds the maximum packet size of " + maximumPacketSize + " bytes (" + direction + ").")
        {
            Direction = direction;
            PacketType = packetType;
            MaximumPacketSize = maximumPacketSize;
            PacketSize = packetSize;
        }

        public MqttPacketSizeDirection Direction { get; }

        public MqttControlPacketType? PacketType { get; }

        public uint MaximumPacketSize { get; }

        public int PacketSize { get; }
    }
}