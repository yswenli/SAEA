/****************************************************************************
*项目名称：SAEA.MQTT.Common.Serializer
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Common.Serializer
*类 名 称：MqttPacketWriter
*版 本 号： v5.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 11:05:32
*描述：
*=====================================================================
*修改时间：2019/1/15 11:05:32
*修 改 人： yswenli
*版 本 号： v5.0.0.1
*描    述：
*****************************************************************************/
using SAEA.MQTT.Core.Protocol;
using System;
using System.Text;

namespace SAEA.MQTT.Common.Serializer
{
    public class MqttPacketWriter
    {
        public static int MaxBufferSize = 4096;

        private byte[] _buffer = new byte[128];

        private int _position;

        public int Length { get; private set; }

        public static byte BuildFixedHeader(MqttControlPacketType packetType, byte flags = 0)
        {
            var fixedHeader = (int)packetType << 4;
            fixedHeader |= flags;
            return (byte)fixedHeader;
        }

        public static ArraySegment<byte> EncodeRemainingLength(int length)
        {
            if (length <= 0)
            {
                return new ArraySegment<byte>(new byte[1], 0, 1);
            }

            var buffer = new byte[4];
            var bufferOffset = 0;

            var x = length;
            do
            {
                var encodedByte = x % 128;
                x = x / 128;
                if (x > 0)
                {
                    encodedByte = encodedByte | 128;
                }

                buffer[bufferOffset] = (byte)encodedByte;
                bufferOffset++;
            } while (x > 0);

            return new ArraySegment<byte>(buffer, 0, bufferOffset);
        }

        public void WriteWithLengthPrefix(string value)
        {
            WriteWithLengthPrefix(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        public void WriteWithLengthPrefix(byte[] value)
        {
            EnsureAdditionalCapacity(value.Length + 2);

            Write((ushort)value.Length);
            Write(value, 0, value.Length);
        }

        public void Write(byte @byte)
        {
            EnsureAdditionalCapacity(1);

            _buffer[_position] = @byte;
            IncreasePostition(1);
        }

        public void Write(ushort value)
        {
            EnsureAdditionalCapacity(2);

            _buffer[_position] = (byte)(value >> 8);
            IncreasePostition(1);
            _buffer[_position] = (byte)value;
            IncreasePostition(1);
        }

        public void Write(byte[] array, int offset, int count)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));

            EnsureAdditionalCapacity(count);

            Array.Copy(array, offset, _buffer, _position, count);
            IncreasePostition(count);
        }

        public void Reset()
        {
            Length = 5;
        }

        public void Seek(int offset)
        {
            EnsureCapacity(offset);
            _position = offset;
        }

        public byte[] GetBuffer()
        {
            return _buffer;
        }

        public void FreeBuffer()
        {
            // This method frees the used memory by shrinking the buffer. This is required because the buffer
            // is used across several messages. In general this is not a big issue because subsequent Ping packages
            // have the same size but a very big publish package with 100 MB of payload will increase the buffer 
            // a lot and the size will never reduced. So this method tries to find a size which can be held in 
            // memory for a long time without causing troubles.

            if (_buffer.Length < MaxBufferSize)
            {
                return;
            }

            Array.Resize(ref _buffer, MaxBufferSize);
        }

        private void EnsureAdditionalCapacity(int additionalCapacity)
        {
            var freeSpace = _buffer.Length - _position;
            if (freeSpace >= additionalCapacity)
            {
                return;
            }

            EnsureCapacity(_buffer.Length + additionalCapacity - freeSpace);
        }

        private void EnsureCapacity(int capacity)
        {
            var newBufferLength = _buffer.Length;

            if (newBufferLength >= capacity)
            {
                return;
            }

            while (newBufferLength < capacity)
            {
                newBufferLength *= 2;
            }

            Array.Resize(ref _buffer, newBufferLength);
        }

        private void IncreasePostition(int length)
        {
            _position += length;

            if (_position > Length)
            {
                Length = _position;
            }
        }
    }
}
