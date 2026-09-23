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
*命名空间：SAEA.QueueSocket.Net
*文件名： QueueCoder
*版本号： v26.4.23.1
*唯一标识：786d8174-e119-4b7d-b1be-5940506b6809
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/05/09 15:22:09
*描述：QueueCoder编解码类
*
*=====================================================================
*修改标记
*修改时间：2018/05/09 15:22:09
*修改人： yswenli
*版本号： v26.4.23.1
*描述：QueueCoder编解码类
*
*****************************************************************************/
using SAEA.Common;
using SAEA.QueueSocket.Model;
using SAEA.QueueSocket.Type;
using SAEA.Sockets.Interface;

using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.QueueSocket.Net
{
    /// <summary>
    /// 队列编码器类，实现ICoder接口
    /// </summary>
    public sealed class QueueCoder : ICoder
    {
        // 定义最小消息长度常量
        static readonly int MIN = 1 + 4 + 4 + 0 + 4 + 0 + 0;

        // 使用byte[]作为缓冲区，避免List<byte>内存不释放的问题
        private byte[] _buffer = new byte[4096];
        private int _bufferOffset = 0;
        private int _bufferCount = 0;

        /// <summary>
        /// 编码方法，将ISocketProtocal对象编码为字节数组
        /// </summary>
        /// <param name="protocal">ISocketProtocal对象</param>
        /// <returns>编码后的字节数组</returns>
        public void Encode(ISocketProtocal protocal, System.Buffers.IBufferWriter<byte> writer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 解码方法，将字节数组解码为ISocketProtocal对象列表
        /// </summary>
        /// <param name="data">待解码的字节数组</param>
        /// <param name="onHeart">心跳包处理回调</param>
        /// <param name="onFile">文件包处理回调</param>
        /// <returns>解码后的ISocketProtocal对象列表</returns>
        public SAEA.Sockets.Base.DecodedFrames Decode(System.Buffers.ReadOnlySequence<byte> data, Action<DateTime> onHeart = null, Action<ReadOnlyMemory<byte>> onFile = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 包解析
        /// </summary>
        /// <param name="data">待解析的字节数组</param>
        /// <returns>解析后的队列消息列表</returns>
        public List<QueueMsg> GetQueueResult(byte[] data)
        {
            return GetQueueResult(data.AsSpan());
        }

        /// <summary>
        /// 包解析（span 版本，避免整帧复制）
        /// </summary>
        /// <param name="data">待解析的字节Span</param>
        /// <returns>解析后的队列消息列表</returns>
        internal List<QueueMsg> GetQueueResult(ReadOnlySpan<byte> data)
        {
            var result = new List<QueueMsg>();

            AppendData(data);

            if (_bufferCount >= MIN)
            {
                try
                {
                    var span = _buffer.AsSpan(_bufferOffset, _bufferCount);
                    var offset = DecodeTo(span, result);
                    if (result.Count > 0)
                    {
                        _bufferOffset += offset;
                        _bufferCount -= offset;
                        if (_bufferOffset > 4096 && _bufferCount < _bufferOffset)
                        {
                            CompactBuffer();
                        }
                        return result;
                    }
                    else if (offset > 0)
                    {
                        _bufferOffset += offset;
                        _bufferCount -= offset;
                    }
                }
                catch
                {
                    _bufferOffset += 1;
                    _bufferCount -= 1;
                }
            }
            return result;
        }

        /// <summary>
        /// 追加数据到内部缓冲区
        /// </summary>
        private void AppendData(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) return;

            if (_bufferCount == 0 && _bufferOffset > 0)
            {
                _bufferOffset = 0;
            }

            int remainingSpace = _buffer.Length - _bufferOffset - _bufferCount;
            if (remainingSpace < data.Length)
            {
                if (_bufferOffset > 0)
                {
                    CompactBuffer();
                    remainingSpace = _buffer.Length - _bufferCount;
                }

                if (remainingSpace < data.Length)
                {
                    int newCapacity = Math.Max(_buffer.Length * 2, _bufferCount + data.Length);
                    byte[] newBuffer = new byte[newCapacity];
                    if (_bufferCount > 0)
                    {
                        Buffer.BlockCopy(_buffer, _bufferOffset, newBuffer, 0, _bufferCount);
                    }
                    _buffer = newBuffer;
                    _bufferOffset = 0;
                }
            }

            data.CopyTo(_buffer.AsSpan(_bufferOffset + _bufferCount));
            _bufferCount += data.Length;
        }

        /// <summary>
        /// 压缩缓冲区：将有效数据移动到数组开头
        /// </summary>
        private void CompactBuffer()
        {
            if (_bufferOffset > 0 && _bufferCount > 0)
            {
                Buffer.BlockCopy(_buffer, _bufferOffset, _buffer, 0, _bufferCount);
            }
            _bufferOffset = 0;
            // 如果缓冲区已空，直接重置偏移
            if (_bufferCount == 0)
            {
                _bufferOffset = 0;
            }
        }

        /// <summary>
        /// socket 传输字节编码
        /// 格式为：1+4+4+x+4+x+x
        /// </summary>
        /// <param name="queueSocketMsg">队列消息对象</param>
        /// <returns>编码后的字节数组</returns>
        public static byte[] Encode(QueueSocketMsg queueSocketMsg)
        {
            byte[] n = null;
            byte[] tp = null;
            byte[] d = null;
            var nlen = 0;
            var tlen = 0;
            var total = 12;

            if (!string.IsNullOrEmpty(queueSocketMsg.Name))
            {
                n = Encoding.UTF8.GetBytes(queueSocketMsg.Name);
                nlen = n.Length;
                total += nlen;
            }
            if (!string.IsNullOrEmpty(queueSocketMsg.Topic))
            {
                tp = Encoding.UTF8.GetBytes(queueSocketMsg.Topic);
                tlen = tp.Length;
                total += tlen;
            }
            if (queueSocketMsg.Data != null && queueSocketMsg.Data.Length > 0)
            {
                d = queueSocketMsg.Data;
                total += d.Length;
            }

            var arr = new byte[1 + total];
            WriteFrame(arr, 0, queueSocketMsg.Type, n, tp, d);
            return arr;
        }

        /// <summary>
        /// 按 QueueSocket 线格式将一帧写入指定缓冲区，返回写入结束后的偏移。
        /// </summary>
        /// <param name="buffer">目标缓冲区</param>
        /// <param name="offset">起始偏移</param>
        /// <param name="type">消息类型</param>
        /// <param name="nameBytes">已编码的名称</param>
        /// <param name="topicBytes">已编码的主题</param>
        /// <param name="data">数据</param>
        /// <returns>写入结束后的偏移</returns>
        internal static int WriteFrame(byte[] buffer, int offset, QueueSocketMsgType type, byte[] nameBytes, byte[] topicBytes, byte[] data)
        {
            var nlen = nameBytes == null ? 0 : nameBytes.Length;
            var tlen = topicBytes == null ? 0 : topicBytes.Length;
            var dlen = data == null ? 0 : data.Length;
            var total = 12 + nlen + tlen + dlen;

            buffer[offset++] = (byte)type;
            WriteInt32(buffer, offset, total);
            offset += 4;
            WriteInt32(buffer, offset, nlen);
            offset += 4;
            if (nlen > 0)
            {
                Buffer.BlockCopy(nameBytes, 0, buffer, offset, nlen);
                offset += nlen;
            }
            WriteInt32(buffer, offset, tlen);
            offset += 4;
            if (tlen > 0)
            {
                Buffer.BlockCopy(topicBytes, 0, buffer, offset, tlen);
                offset += tlen;
            }
            if (dlen > 0)
            {
                Buffer.BlockCopy(data, 0, buffer, offset, dlen);
                offset += dlen;
            }
            return offset;
        }

        private static void WriteInt32(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        /// <summary>
        /// 解码方法，将字节数组解码为QueueSocketMsg对象列表
        /// </summary>
        /// <param name="data">待解码的字节数组</param>
        /// <param name="offset">解码后的偏移量</param>
        /// <returns>解码后的QueueSocketMsg对象列表</returns>
        public static List<QueueSocketMsg> Decode(byte[] data, out int offset)
        {
            if (data != null)
            {
                return Decode(data.AsSpan(), out offset);
            }
            offset = 0;
            return null;
        }

        /// <summary>
        /// 解码方法，将ReadOnlySpan解码为QueueSocketMsg对象列表，避免数组复制
        /// </summary>
        /// <param name="data">待解码的字节Span</param>
        /// <param name="offset">解码后的偏移量</param>
        /// <returns>解码后的QueueSocketMsg对象列表</returns>
        public static List<QueueSocketMsg> Decode(ReadOnlySpan<byte> data, out int offset)
        {
            offset = 0;
            if (data.Length >= offset + MIN)
            {
                var list = new List<QueueSocketMsg>();
                while (data.Length >= offset + MIN)
                {
                    var typeValue = data[offset];
                    if (typeValue < 1 || typeValue > 7)
                    {
                        //丢弃通信中接收到的不正常数据，并在data数组中的找到中找到第一个正确的typeValue
                        bool found = false;
                        for (var i = offset + 1; i < data.Length; i++)
                        {
                            if (data[i] >= 1 && data[i] <= 7)
                            {
                                typeValue = data[i];
                                offset = i;
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            // 没有找到有效的type，跳过所有已扫描的数据
                            offset = data.Length;
                            return list;
                        }
                    }
                    QueueSocketMsgType type = (QueueSocketMsgType)typeValue;
                    int packetStart = offset;
                    offset += 1;
                    
                    // 确保有足够的字节读取total
                    if (offset + 4 > data.Length) break;
                    var total = ReadInt32(data, offset);
                    if (total < 0 || total > 100 * 1024 * 1024)
                    {
                        // total非法，丢弃这个字节，重新扫描
                        offset = packetStart + 1;
                        continue;
                    }
                    if (data.Length >= offset + total)
                    {
                        offset += 4;
                        var qm = new QueueSocketMsg(type);
                        qm.Total = total;

                        // 确保有足够的字节读取NameLength
                        if (offset + 4 > data.Length) { offset = packetStart; break; }
                        qm.NameLength = ReadInt32(data, offset);
                        if (qm.NameLength < 0 || qm.NameLength > total)
                        {
                            offset = packetStart + 1;
                            continue;
                        }
                        offset += 4;

                        if (qm.NameLength > 0)
                        {
                            if (offset + qm.NameLength > data.Length) { offset = packetStart; break; }
                            var narr = data.Slice(offset, qm.NameLength).ToArray();
                            qm.Name = Encoding.UTF8.GetString(narr);
                        }
                        offset += qm.NameLength;

                        // 确保有足够的字节读取TopicLength
                        if (offset + 4 > data.Length) { offset = packetStart; break; }
                        qm.TopicLength = ReadInt32(data, offset);
                        if (qm.TopicLength < 0 || qm.TopicLength > total)
                        {
                            offset = packetStart + 1;
                            continue;
                        }
                        offset += 4;

                        if (qm.TopicLength > 0)
                        {
                            if (offset + qm.TopicLength > data.Length) { offset = packetStart; break; }
                            var tarr = data.Slice(offset, qm.TopicLength).ToArray();
                            qm.Topic = Encoding.UTF8.GetString(tarr);
                        }
                        offset += qm.TopicLength;

                        var dlen = qm.Total - 4 - 4 - qm.NameLength - 4 - qm.TopicLength;
                        if (dlen < 0)
                        {
                            offset = packetStart + 1;
                            continue;
                        }

                        if (dlen > 0)
                        {
                            if (offset + dlen > data.Length) { offset = packetStart; break; }
                            var darr = data.Slice(offset, dlen).ToArray();
                            qm.Data = darr;
                        }
                        offset += dlen;
                        list.Add(qm);
                    }
                    else
                    {
                        // 数据不足，回退到包开头
                        offset = packetStart;
                        break;
                    }
                }
                return list;
            }
            return null;
        }

        /// <summary>
        /// 解码到 QueueMsg 列表，避免生成中间 QueueSocketMsg 对象。
        /// </summary>
        /// <param name="data">待解码的字节Span</param>
        /// <param name="result">解析结果追加目标</param>
        /// <returns>已消费的偏移量</returns>
        internal static int DecodeTo(ReadOnlySpan<byte> data, List<QueueMsg> result)
        {
            var offset = 0;
            if (data.Length < MIN)
            {
                return 0;
            }

            while (data.Length >= offset + MIN)
            {
                var typeValue = data[offset];
                if (typeValue < 1 || typeValue > 7)
                {
                    bool found = false;
                    for (var i = offset + 1; i < data.Length; i++)
                    {
                        if (data[i] >= 1 && data[i] <= 7)
                        {
                            typeValue = data[i];
                            offset = i;
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        return data.Length;
                    }
                }

                var type = (QueueSocketMsgType)typeValue;
                int packetStart = offset;
                offset += 1;

                if (offset + 4 > data.Length) { offset = packetStart; break; }
                var total = ReadInt32(data, offset);
                if (total < 0 || total > 100 * 1024 * 1024)
                {
                    offset = packetStart + 1;
                    continue;
                }
                if (data.Length < offset + total)
                {
                    offset = packetStart;
                    break;
                }

                var qm = new QueueMsg();
                qm.Type = type;
                offset += 4;

                if (offset + 4 > data.Length) { offset = packetStart; break; }
                var nameLength = ReadInt32(data, offset);
                if (nameLength < 0 || nameLength > total)
                {
                    offset = packetStart + 1;
                    continue;
                }
                offset += 4;

                if (nameLength > 0)
                {
                    if (offset + nameLength > data.Length) { offset = packetStart; break; }
                    qm.Name = Encoding.UTF8.GetString(data.Slice(offset, nameLength).ToArray());
                }
                offset += nameLength;

                if (offset + 4 > data.Length) { offset = packetStart; break; }
                var topicLength = ReadInt32(data, offset);
                if (topicLength < 0 || topicLength > total)
                {
                    offset = packetStart + 1;
                    continue;
                }
                offset += 4;

                if (topicLength > 0)
                {
                    if (offset + topicLength > data.Length) { offset = packetStart; break; }
                    qm.Topic = Encoding.UTF8.GetString(data.Slice(offset, topicLength).ToArray());
                }
                offset += topicLength;

                var dlen = total - 4 - 4 - nameLength - 4 - topicLength;
                if (dlen < 0)
                {
                    offset = packetStart + 1;
                    continue;
                }
                if (dlen > 0)
                {
                    if (offset + dlen > data.Length) { offset = packetStart; break; }
                    qm.Data = data.Slice(offset, dlen).ToArray();
                }
                offset += dlen;
                result.Add(qm);
            }

            return offset;
        }

        /// <summary>
        /// 从ReadOnlySpan中读取Int32，兼容netstandard2.0
        /// </summary>
        private static int ReadInt32(ReadOnlySpan<byte> data, int offset)
        {
            return data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24);
        }

        /// <summary>
        /// 清除方法，清除编码器内部状态并释放缓冲区
        /// </summary>
        public void Clear()
        {
            _bufferOffset = 0;
            _bufferCount = 0;
            // 释放大缓冲区，重新分配初始大小
            if (_buffer.Length > 8192)
            {
                _buffer = new byte[4096];
            }
        }

        /// <summary>
        /// 按指定格式编码
        /// </summary>
        /// <param name="cmdType">命令类型</param>
        /// <param name="name">名称</param>
        /// <param name="topic">主题</param>
        /// <param name="data">数据</param>
        /// <returns>编码后的字节数组</returns>
        public byte[] Encode(QueueSocketMsgType cmdType, string name, string topic, byte[] data)
        {
            return QueueCoder.Encode(new QueueSocketMsg(cmdType, name, topic, data));
        }

        /// <summary>
        /// 按指定格式编码批量处理
        /// </summary>
        /// <param name="cmdType">命令类型</param>
        /// <param name="name">名称</param>
        /// <param name="topic">主题</param>
        /// <param name="data">数据列表</param>
        /// <returns>编码后的字节数组</returns>
        public byte[] EncodeForList(QueueSocketMsgType cmdType, string name, string topic, List<byte[]> data)
        {
            List<byte> list = new List<byte>();
            if (data != null)
            {
                foreach (var item in data)
                {
                    list.AddRange(Encode(cmdType, name, topic, item));
                }
            }
            return list.ToArray();
        }

        /// <summary>
        /// 生成Ping消息
        /// </summary>
        /// <param name="name">名称</param>
        /// <returns>编码后的字节数组</returns>
        public byte[] Ping(string name)
        {
            return Encode(QueueSocketMsgType.Ping, name, string.Empty, null);
        }

        /// <summary>
        /// 生成Pong消息
        /// </summary>
        /// <param name="name">名称</param>
        /// <returns>编码后的字节数组</returns>
        public byte[] Pong(string name)
        {
            return Encode(QueueSocketMsgType.Pong, name, string.Empty, Encoding.UTF8.GetBytes(DateTimeHelper.ToString()));
        }

        /// <summary>
        /// 生成发布消息
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="topic">主题</param>
        /// <param name="data">数据</param>
        /// <returns>编码后的字节数组</returns>
        public byte[] Publish(string name, string topic, byte[] data)
        {
            return Encode(QueueSocketMsgType.Publish, name, topic, data);
        }

        /// <summary>
        /// 生成订阅消息
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="topic">主题</param>
        /// <returns>编码后的字节数组</returns>
        public byte[] Subscribe(string name, string topic)
        {
            return Encode(QueueSocketMsgType.Subcribe, name, topic, null);
        }

        /// <summary>
        /// 生成取消订阅消息
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="topic">主题</param>
        /// <returns>编码后的字节数组</returns>
        public byte[] Unsubcribe(string name, string topic)
        {
            return Encode(QueueSocketMsgType.Unsubcribe, name, topic, null);
        }

        /// <summary>
        /// 生成关闭消息
        /// </summary>
        /// <param name="name">名称</param>
        /// <returns>编码后的字节数组</returns>
        public byte[] Close(string name)
        {
            return Encode(QueueSocketMsgType.Close, name, string.Empty, null);
        }

        /// <summary>
        /// 生成数据消息
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="topic">主题</param>
        /// <param name="data">数据</param>
        /// <returns>编码后的字节数组</returns>
        public byte[] Data(string name, string topic, byte[] data)
        {
            return Encode(QueueSocketMsgType.Data, name, topic, data);
        }

    }
}