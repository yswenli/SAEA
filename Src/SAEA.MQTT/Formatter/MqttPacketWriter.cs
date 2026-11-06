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
*文件名： MqttPacketWriter
*版本号： v26.4.23.1
*唯一标识：bd5a6eab-f93a-4880-b7f6-ac2c4d695b26
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttPacketWriter接口
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
using System.Text;
using SAEA.Common.Caching;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Formatter
{
    /// <summary>
    /// MQTT 专用的内存流式写入器，只保留与 MQTT 编码相关的写入能力，扩容规则与 MemoryStream 一致，并可直接访问内部缓冲区
    /// </summary>
    public sealed class MqttPacketWriter : IMqttPacketWriter
    {
        static readonly ArraySegment<byte> ZeroVariableLengthIntegerArray = new ArraySegment<byte>(new byte[1], 0, 1);
        static readonly ArraySegment<byte> ZeroTwoByteIntegerArray = new ArraySegment<byte>(new byte[2], 0, 2);

        /// <summary>
        /// 初始缓冲区字节数，新建写入器时按此大小从内存池租用
        /// </summary>
        public static int InitialBufferSize = 128;

        /// <summary>
        /// 缓冲区上限字节数，释放缓冲区时超过该值则收缩到此大小
        /// </summary>
        public static int MaxBufferSize = 4096;

        byte[] _buffer;
        bool _isPooled;

        int _offset;

        /// <summary>
        /// 已写入的有效字节数
        /// </summary>
        public int Length { get; private set; }

        /// <summary>
        /// 构造函数，从内存池租用初始缓冲区
        /// </summary>
        public MqttPacketWriter()
        {
            _buffer = MemoryPoolManager.Rent(InitialBufferSize);
            _isPooled = true;
            _offset = 0;
        }

        /// <summary>
        /// 由报文类型与标志位拼出固定头首字节
        /// </summary>
        /// <param name="packetType">控制报文类型，放在高 4 位</param>
        /// <param name="flags">报文标志位，只使用低 4 位</param>
        /// <returns>固定头首字节</returns>
        public static byte BuildFixedHeader(MqttControlPacketType packetType, byte flags = 0)
        {
            var fixedHeader = (int)packetType << 4;
            fixedHeader |= flags;
            return (byte)fixedHeader;
        }

        /// <summary>
        /// 计算变长整数的编码字节数
        /// </summary>
        /// <param name="value">待编码的数值</param>
        /// <returns>编码所需字节数，1 到 4</returns>
        public static int GetLengthOfVariableInteger(uint value)
        {
            var result = 0;
            var x = value;
            do
            {
                x = x / 128;
                result++;
            } while (x > 0);

            return result;
        }

        /// <summary>
        /// 将数值编码为 MQTT 变长字节整数，每字节低 7 位有效、最高位表示是否续读
        /// </summary>
        /// <param name="value">待编码的数值</param>
        /// <returns>编码结果，长度 1 到 4 字节</returns>
        public static ArraySegment<byte> EncodeVariableLengthInteger(uint value)
        {
            if (value == 0)
            {
                return ZeroVariableLengthIntegerArray;
            }

            if (value <= 127)
            {
                return new ArraySegment<byte>(new[] { (byte)value }, 0, 1);
            }

            // 小数据使用stackalloc避免堆分配
            unsafe
            {
                byte* buffer = stackalloc byte[4];
                var bufferOffset = 0;

                var x = value;
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

                // 将结果复制到托管数组
                var result = new byte[bufferOffset];
                fixed (byte* resultPtr = result)
                {
                    for (int i = 0; i < bufferOffset; i++)
                        resultPtr[i] = buffer[i];
                }
                return new ArraySegment<byte>(result, 0, bufferOffset);
            }
        }

        /// <summary>
        /// 写入变长字节整数
        /// </summary>
        /// <param name="value">待写入的数值</param>
        public void WriteVariableLengthInteger(uint value)
        {
            Write(EncodeVariableLengthInteger(value));
        }

        /// <summary>
        /// 写入带两字节长度前缀的 UTF-8 字符串，空字符串或 null 写为长度 0
        /// </summary>
        /// <param name="value">待写入的字符串</param>
        public void WriteWithLengthPrefix(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                Write(ZeroTwoByteIntegerArray);
            }
            else
            {
                WriteWithLengthPrefix(Encoding.UTF8.GetBytes(value));
            }
        }

        /// <summary>
        /// 写入带两字节长度前缀的二进制数据，空数组或 null 写为长度 0
        /// </summary>
        /// <param name="value">待写入的数据</param>
        public void WriteWithLengthPrefix(byte[] value)
        {
            if (value == null || value.Length == 0)
            {
                Write(ZeroTwoByteIntegerArray);
            }
            else
            {
                EnsureAdditionalCapacity(value.Length + 2);
                Write((ushort)value.Length);
                Write(value, 0, value.Length);
            }
        }

        /// <summary>
        /// 写入单字节
        /// </summary>
        /// <param name="byte">待写入的字节</param>
        public void Write(byte @byte)
        {
            EnsureAdditionalCapacity(1);

            _buffer[_offset] = @byte;
            IncreasePosition(1);
        }

        /// <summary>
        /// 以大端序写入两字节整数
        /// </summary>
        /// <param name="value">待写入的数值</param>
        public void Write(ushort value)
        {
            EnsureAdditionalCapacity(2);

            _buffer[_offset] = (byte)(value >> 8);
            IncreasePosition(1);
            _buffer[_offset] = (byte)value;
            IncreasePosition(1);
        }

        /// <summary>
        /// 写入字节数组的指定片段，count 为 0 时不写入
        /// </summary>
        /// <param name="buffer">数据源</param>
        /// <param name="offset">数据源起始偏移</param>
        /// <param name="count">要写入的字节数</param>
        public void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            if (count == 0)
            {
                return;
            }

            EnsureAdditionalCapacity(count);

            Array.Copy(buffer, offset, _buffer, _offset, count);
            IncreasePosition(count);
        }

        /// <summary>
        /// 追加另一个报文写入器已写入的内容，仅支持 MqttPacketWriter 实例
        /// </summary>
        /// <param name="propertyWriter">作为数据来源的写入器</param>
        public void Write(IMqttPacketWriter propertyWriter)
        {
            if (propertyWriter == null) throw new ArgumentNullException(nameof(propertyWriter));

            if (propertyWriter is MqttPacketWriter writer)
            {
                if (writer.Length == 0)
                {
                    return;
                }

                Write(writer._buffer, 0, writer.Length);
                return;
            }

            throw new InvalidOperationException($"{nameof(propertyWriter)} must be of type {typeof(MqttPacketWriter).Name}");
        }

        /// <summary>
        /// 仅重置已写入长度，不移动写入位置
        /// </summary>
        /// <param name="length">新的已写入长度</param>
        public void Reset(int length)
        {
            Length = length;
        }

        /// <summary>
        /// 移动写入位置，目标超出容量时按倍数扩容
        /// </summary>
        /// <param name="position">目标位置，从 0 开始</param>
        public void Seek(int position)
        {
            EnsureCapacity(position);
            _offset = position;
        }

        /// <summary>
        /// 获取内部缓冲区，有效数据为前 Length 个字节
        /// </summary>
        /// <returns>内部缓冲区数组，不复制</returns>
        public byte[] GetBuffer()
        {
            return _buffer;
        }

        /// <summary>
        /// 缓冲区达到上限时归还内存池并换成标准大小的缓冲区，避免大报文长期占用内存
        /// </summary>
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

            // 归还旧的大缓冲区到内存池
            if (_isPooled)
            {
                MemoryPoolManager.Return(_buffer, _buffer.Length);
            }

            // 从内存池租用新的标准大小缓冲区
            _buffer = MemoryPoolManager.Rent(MaxBufferSize);
            _isPooled = true;
        }

        void Write(ArraySegment<byte> buffer)
        {
            Write(buffer.Array, buffer.Offset, buffer.Count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void EnsureAdditionalCapacity(int additionalCapacity)
        {
            var freeSpace = _buffer.Length - _offset;
            if (freeSpace >= additionalCapacity)
            {
                return;
            }

            EnsureCapacity(_buffer.Length + additionalCapacity - freeSpace);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void EnsureCapacity(int capacity)
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

            // 从内存池租用更大的缓冲区
            var newBuffer = MemoryPoolManager.Rent(newBufferLength);
            
            // 复制现有数据
            Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _offset);
            
            // 归还旧的缓冲区
            if (_isPooled)
            {
                MemoryPoolManager.Return(_buffer, _buffer.Length);
            }
            
            _buffer = newBuffer;
            _isPooled = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IncreasePosition(int length)
        {
            _offset += length;

            if (_offset > Length)
            {
                Length = _offset;
            }
        }
    }
}
