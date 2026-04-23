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
*唯一标识：91a7dde3-9533-4086-8d52-9d1732e90c15
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
using SAEA.Common.Caching;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using SAEA.MQTT.Exceptions;

namespace SAEA.MQTT.Formatter
{
    public sealed class MqttPacketBodyReader : IMqttPacketBodyReader
    {
        readonly byte[] _buffer;
        readonly int _initialOffset;
        readonly int _length;

        int _offset;

        public MqttPacketBodyReader(byte[] buffer, int offset, int length)
        {
            _buffer = buffer;
            _initialOffset = offset;
            _offset = offset;
            _length = length;
        }

        public int Offset => _offset;

        public int Length => _length - _offset;

        public bool EndOfStream => _offset == _length;

        public void Seek(int position)
        {
            _offset = _initialOffset + position;
        }

        public byte ReadByte()
        {
            ValidateReceiveBuffer(1);

            return _buffer[_offset++];
        }
        
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

        public ushort ReadTwoByteInteger()
        {
            ValidateReceiveBuffer(2);

            var msb = _buffer[_offset++];
            var lsb = _buffer[_offset++];
            
            return (ushort)(msb << 8 | lsb);
        }

        public uint ReadFourByteInteger()
        {
            ValidateReceiveBuffer(4);

            var byte0 = _buffer[_offset++];
            var byte1 = _buffer[_offset++];
            var byte2 = _buffer[_offset++];
            var byte3 = _buffer[_offset++];

            return (uint)(byte0 << 24 | byte1 << 16 | byte2 << 8 | byte3);
        }

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

        public string ReadStringWithLengthPrefix()
        {
            var buffer = ReadSegmentWithLengthPrefix();
            return Encoding.UTF8.GetString(buffer.Array, buffer.Offset, buffer.Count);
        }
    }
}
