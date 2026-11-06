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
*文件名： MqttPacketFormatterAdapter
*版本号： v26.4.23.1
*唯一标识：07378f5a-1e5a-4e11-b7a1-687ac1774326
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttPacketFormatterAdapter适配器类
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
using System.Runtime.CompilerServices;
using SAEA.MQTT.Adapter;
using SAEA.MQTT.Exceptions;
using SAEA.MQTT.Formatter.V3;
using SAEA.MQTT.Formatter.V5;
using SAEA.MQTT.Internal;
using SAEA.MQTT.Packets;

namespace SAEA.MQTT.Formatter
{
    /// <summary>
    /// MQTT 报文编解码适配器，按协议版本选择具体编解码器，并可自动探测 CONNECT 报文的协议版本
    /// </summary>
    public sealed class MqttPacketFormatterAdapter
    {
        IMqttPacketFormatter _formatter;
               
        /// <summary>
        /// 按协议版本创建适配器，内部新建报文写入器
        /// </summary>
        /// <param name="protocolVersion">MQTT 协议版本，不能为 Unknown</param>
        public MqttPacketFormatterAdapter(MqttProtocolVersion protocolVersion)
            : this(protocolVersion, new MqttPacketWriter())
        {
        }

        /// <summary>
        /// 按协议版本与外部写入器创建适配器
        /// </summary>
        /// <param name="protocolVersion">MQTT 协议版本，不能为 Unknown</param>
        /// <param name="writer">报文写入器</param>
        public MqttPacketFormatterAdapter(MqttProtocolVersion protocolVersion, IMqttPacketWriter writer)
            : this(writer)
        {
            UseProtocolVersion(protocolVersion);
        }

        /// <summary>
        /// 仅绑定报文写入器创建适配器，协议版本需后续指定或探测
        /// </summary>
        /// <param name="writer">报文写入器</param>
        public MqttPacketFormatterAdapter(IMqttPacketWriter writer)
        {
            Writer = writer;
        }        

        /// <summary>
        /// 当前使用的 MQTT 协议版本，未指定且未探测时为 Unknown
        /// </summary>
        public MqttProtocolVersion ProtocolVersion { get; private set; } = MqttProtocolVersion.Unknown;

        internal MqttConnectionCapabilities Capabilities { get; private set; }

        internal void AttachCapabilities(MqttConnectionCapabilities capabilities)
        {
            Capabilities = capabilities;
            var converter = _formatter?.DataConverter as MqttV500DataConverter;
            if (converter != null)
            {
                converter.Capabilities = capabilities;
            }
        }

        /// <summary>
        /// 当前协议版本对应的数据转换器
        /// </summary>
        public IMqttDataConverter DataConverter
        {
            get
            {
                ThrowIfFormatterNotSet();

                return _formatter.DataConverter;
            }
        }
                
        /// <summary>
        /// 绑定的报文写入器
        /// </summary>
        public IMqttPacketWriter Writer { get; }

        /// <summary>
        /// 将报文编码为字节缓冲区片段
        /// </summary>
        /// <param name="packet">待编码的报文</param>
        /// <returns>编码结果，仅在调用 FreeBuffer 之前有效</returns>
        public ArraySegment<byte> Encode(MqttBasePacket packet)
        {
            if (packet == null) throw new ArgumentNullException(nameof(packet));

            ThrowIfFormatterNotSet();

            return _formatter.Encode(packet);
        }

        /// <summary>
        /// 将原始报文解码为报文对象
        /// </summary>
        /// <param name="receivedMqttPacket">已解析固定头的原始报文</param>
        /// <returns>解码后的报文</returns>
        public MqttBasePacket Decode(ReceivedMqttPacket receivedMqttPacket)
        {
            if (receivedMqttPacket == null) throw new ArgumentNullException(nameof(receivedMqttPacket));

            ThrowIfFormatterNotSet();

            return _formatter.Decode(receivedMqttPacket);
        }

        /// <summary>
        /// 释放编码缓冲区，尚未设置编解码器时忽略
        /// </summary>
        public void FreeBuffer()
        {
            _formatter?.FreeBuffer();
        }

        /// <summary>
        /// 解析 CONNECT 报文中的协议名与协议级别，切换编解码器并把报文体读取位置回退到起点
        /// </summary>
        /// <param name="receivedMqttPacket">收到的 CONNECT 原始报文</param>
        public void DetectProtocolVersion(ReceivedMqttPacket receivedMqttPacket)
        {
            var protocolVersion = ParseProtocolVersion(receivedMqttPacket);

            // Reset the position of the stream because the protocol version is part of 
            // the regular CONNECT packet. So it will not properly deserialized if this
            // data is missing.
            receivedMqttPacket.Body.Seek(0);

            UseProtocolVersion(protocolVersion);
        }

        /// <summary>
        /// 按协议版本创建对应的报文编解码器
        /// </summary>
        /// <param name="protocolVersion">MQTT 协议版本</param>
        /// <param name="writer">报文写入器</param>
        /// <returns>对应版本的报文编解码器</returns>
        public static IMqttPacketFormatter GetMqttPacketFormatter(MqttProtocolVersion protocolVersion, IMqttPacketWriter writer)
        {
            if (protocolVersion == MqttProtocolVersion.Unknown)
            {
                throw new InvalidOperationException("MQTT protocol version is invalid.");
            }
            
            switch (protocolVersion)
            {
                case MqttProtocolVersion.V500:
                    {
                        return new MqttV500PacketFormatter(writer);
                    }
                case MqttProtocolVersion.V311:
                    {
                        return new MqttV311PacketFormatter(writer);
                    }
                case MqttProtocolVersion.V310:
                    {
                        return new MqttV310PacketFormatter(writer);
                    }
                default:
                    {
                        throw new NotSupportedException();
                    }
            }
        }

        void UseProtocolVersion(MqttProtocolVersion protocolVersion)
        {
            if (protocolVersion == MqttProtocolVersion.Unknown)
            {
                throw new InvalidOperationException("MQTT protocol version is invalid.");
            }

            ProtocolVersion = protocolVersion;
            _formatter = GetMqttPacketFormatter(protocolVersion, Writer);
        }

        static MqttProtocolVersion ParseProtocolVersion(ReceivedMqttPacket receivedMqttPacket)
        {
            if (receivedMqttPacket == null) throw new ArgumentNullException(nameof(receivedMqttPacket));

            if (receivedMqttPacket.Body.Length < 7)
            {
                // 2 byte protocol name length
                // at least 4 byte protocol name
                // 1 byte protocol level
                throw new MqttProtocolViolationException("CONNECT packet must have at least 7 bytes.");
            }

            var protocolName = receivedMqttPacket.Body.ReadStringWithLengthPrefix();
            var protocolLevel = receivedMqttPacket.Body.ReadByte();

            if (protocolName == "MQTT")
            {
                if (protocolLevel == 5)
                {
                    return MqttProtocolVersion.V500;
                }

                if (protocolLevel == 4)
                {
                    return MqttProtocolVersion.V311;
                }

                throw new MqttProtocolViolationException($"Protocol level '{protocolLevel}' not supported.");
            }

            if (protocolName == "MQIsdp")
            {
                if (protocolLevel == 3)
                {
                    return MqttProtocolVersion.V310;
                }

                throw new MqttProtocolViolationException($"Protocol level '{protocolLevel}' not supported.");
            }

            throw new MqttProtocolViolationException($"Protocol '{protocolName}' not supported.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void ThrowIfFormatterNotSet()
        {
            if (_formatter == null)
            {
                throw new InvalidOperationException("Protocol version not set or detected.");
            }
        }
    }
}
