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
*文件名： MqttPacketBodyReader
*版本号： v26.4.23.1
*唯一标识：1b799856-4528-475d-8661-4330b91889f4
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttPacketBodyReader类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Common.Caching;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using SAEA.MQTT.Exceptions;

namespace SAEA.MQTT.Formatter
{
    /// <summary>
    /// MQTT 报文体读取器，在已接收的缓冲区上按协议顺序读取字段并推进读取位置
    /// </summary>
    public sealed class MqttPacketBodyReader : IMqttPacketBodyReader
    {
        readonly byte[] _buffer;
        readonly int _initialOffset;
        readonly int _length;

        int _offset;

        /// <summary>
        /// 在指定缓冲区的片段上创建读取器，底层数据不会被复制
        /// </summary>
        /// <param name="buffer">报文体所在缓冲区</param>
        /// <param name="offset">报文体在缓冲区中的起始索引</param>
        /// <param name="length">报文体在缓冲区中的结束索引，等于起始索引加报文体字节数</param>
        public MqttPacketBodyReader(byte[] buffer, int offset, int length)
        {
            _buffer = buffer;
            _initialOffset = offset;
            _offset = offset;
            _length = length;
        }

        /// <summary>
        /// 当前读取位置在底层缓冲区中的索引
        /// </summary>
        public int Offset => _offset;

        /// <summary>
        /// 报文体中从当前读取位置到末尾的字节数
        /// </summary>
        public int Length => _length - _offset;

        /// <summary>
        /// 读取位置是否已到达报文体末尾
        /// </summary>
        public bool EndOfStream => _offset == _length;

        /// <summary>
        /// 将读取位置移动到相对报文体起始的指定偏移
        /// </summary>
        /// <param name="position">相对报文体起始的偏移，超出报文体范围时后续读取会抛异常</param>
        public void Seek(int position)
        {
            _offset = _initialOffset + position;
        }

        /// <summary>
        /// 读取单字节并推进读取位置
        /// </summary>
        /// <returns>读取到的字节</returns>
        public byte ReadByte()
        {
            ValidateReceiveBuffer(1);

            return _buffer[_offset++];
        }

        /// <summary>
        /// 读取布尔值，仅 0 与 1 合法
        /// </summary>
        /// <returns>true 表示 1，false 表示 0</returns>
        public bool ReadBoolean()
        {
            ValidateReceiveBuffer(1);

            var buffer = _buffer[_offset++];

            if (buffer == 0)
            {
                return false;
            }

            if (buffer == 1)
            {
                return true;
            }

            throw new MqttProtocolViolationException("Boolean values can be 0 or 1 only.");
        }

        /// <summary>
        /// 读取从当前读取位置到末尾的全部数据并返回独立副本，不推进读取位置
        /// </summary>
        /// <returns>剩余数据的副本，修改它不影响底层缓冲区</returns>
        public byte[] ReadRemainingData()
        {
            var bufferLength = _length - _offset;
            byte[] buffer = null;
            bool rented = false;

            try
            {
                // Use MemoryPoolManager for small buffers
                if (bufferLength <= MemoryPoolManager.SmallThreshold)
                {
                    buffer = MemoryPoolManager.Rent(bufferLength);
                    rented = true;
                }
                else
                {
                    buffer = new byte[bufferLength];
                }
                Array.Copy(_buffer, _offset, buffer, 0, bufferLength);

                // Return a copy that won't be returned to pool
                var result = new byte[bufferLength];
                Buffer.BlockCopy(buffer, 0, result, 0, bufferLength);
                return result;
            }
            finally
            {
                if (rented && buffer != null)
                {
                    MemoryPoolManager.Return(buffer, bufferLength);
                }
            }
        }

        /// <summary>
        /// 读取两字节整数（大端序）
        /// </summary>
        /// <returns>解码后的无符号两字节整数</returns>
        public ushort ReadTwoByteInteger()
        {
            ValidateReceiveBuffer(2);

            var msb = _buffer[_offset++];
            var lsb = _buffer[_offset++];
            
            return (ushort)(msb << 8 | lsb);
        }

        /// <summary>
        /// 读取四字节整数（大端序）
        /// </summary>
        /// <returns>解码后的无符号四字节整数</returns>
        public uint ReadFourByteInteger()
        {
            ValidateReceiveBuffer(4);

            var byte0 = _buffer[_offset++];
            var byte1 = _buffer[_offset++];
            var byte2 = _buffer[_offset++];
            var byte3 = _buffer[_offset++];

            return (uint)(byte0 << 24 | byte1 << 16 | byte2 << 8 | byte3);
        }

        /// <summary>
        /// 读取变长字节整数，每字节低 7 位有效、最高位表示是否继续读取
        /// </summary>
        /// <returns>解码后的整数值，超过 4 字节时抛出协议违规异常</returns>
        public uint ReadVariableLengthInteger()
        {
            var multiplier = 1;
            var value = 0U;
            byte encodedByte;

            do
            {
                encodedByte = ReadByte();
                value += (uint)((encodedByte & 127) * multiplier);

                if (multiplier > 2097152)
                {
                    throw new MqttProtocolViolationException("Variable length integer is invalid.");
                }

                multiplier *= 128;
            } while ((encodedByte & 128) != 0);

            return value;
        }

        /// <summary>
        /// 读取带两字节长度前缀的二进制数据
        /// </summary>
        /// <returns>读取到的字节数组副本</returns>
        public byte[] ReadWithLengthPrefix()
        {
            return ReadSegmentWithLengthPrefix().ToArray();
        }

        private ArraySegment<byte> ReadSegmentWithLengthPrefix()
        {
            var length = ReadTwoByteInteger();

            ValidateReceiveBuffer(length);

            var result = new ArraySegment<byte>(_buffer, _offset, length);
            _offset += length;

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateReceiveBuffer(int length)
        {
            if (_length < _offset + length)
            {
                throw new MqttProtocolViolationException($"Expected at least {_offset + length} bytes but there are only {_length} bytes");
            }
        }

        /// <summary>
        /// 读取带两字节长度前缀的 UTF-8 字符串
        /// </summary>
        /// <returns>解码后的字符串</returns>
        public string ReadStringWithLengthPrefix()
        {
            var buffer = ReadSegmentWithLengthPrefix();
            return Encoding.UTF8.GetString(buffer.Array, buffer.Offset, buffer.Count);
        }
    }
}
