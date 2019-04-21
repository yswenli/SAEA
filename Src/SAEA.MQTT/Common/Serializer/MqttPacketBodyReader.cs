/****************************************************************************
*项目名称：SAEA.MQTT.Common.Serializer
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Common.Serializer
*类 名 称：MqttPacketBodyReader
*版 本 号： v4.5.1.2
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 10:15:20
*描述：
*=====================================================================
*修改时间：2019/1/15 10:15:20
*修 改 人： yswenli
*版 本 号： v4.5.1.2
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.MQTT.Common.Serializer
{
    public class MqttPacketBodyReader
    {
        private readonly byte[] _buffer;
        private readonly int _length;

        private int _offset;

        public MqttPacketBodyReader(byte[] buffer, int offset, int length)
        {
            _buffer = buffer;
            _offset = offset;
            _length = length;
        }

        public int Length => _length - _offset;

        public bool EndOfStream => _offset == _length;

        public byte ReadByte()
        {
            ValidateReceiveBuffer(1);
            return _buffer[_offset++];
        }

        public ArraySegment<byte> ReadRemainingData()
        {
            return new ArraySegment<byte>(_buffer, _offset, _length - _offset);
        }

        public ushort ReadUInt16()
        {
            ValidateReceiveBuffer(2);

            var msb = _buffer[_offset++];
            var lsb = _buffer[_offset++];

            return (ushort)(msb << 8 | lsb);
        }

        public ArraySegment<byte> ReadWithLengthPrefix()
        {
            var length = ReadUInt16();

            ValidateReceiveBuffer(length);

            var result = new ArraySegment<byte>(_buffer, _offset, length);
            _offset += length;

            return result;
        }

        private void ValidateReceiveBuffer(ushort length)
        {
            if (_length < _offset + length)
            {
                throw new ArgumentOutOfRangeException(nameof(_buffer), $"expected at least {_offset + length} bytes but there are only {_length} bytes");
            }
        }

        public string ReadStringWithLengthPrefix()
        {
            var buffer = ReadWithLengthPrefix();
            return Encoding.UTF8.GetString(buffer.Array, buffer.Offset, buffer.Count);
        }
    }
}
