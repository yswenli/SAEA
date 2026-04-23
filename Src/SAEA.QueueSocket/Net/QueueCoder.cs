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
*唯一标识：93be61fd-669b-4545-a9d3-b0ac7b156523
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/05/09 15:22:09
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/05/09 15:22:09
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
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
        public byte[] Encode(ISocketProtocal protocal)
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
        public List<ISocketProtocal> Decode(byte[] data, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
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
            var result = new List<QueueMsg>();

            // 追加数据到缓冲区
            AppendData(data);

            if (_bufferCount >= MIN)
            {
                try
                {
                    // 直接使用Span解析，避免ToArray复制
                    var span = _buffer.AsSpan(_bufferOffset, _bufferCount);
                    var list = Decode(span, out int offset);
                    if (list != null && list.Count > 0)
                    {
                        foreach (var item in list)
                        {
                            result.Add(new QueueMsg()
                            {
                                Type = item.Type,
                                Name = item.Name,
                                Topic = item.Topic,
                                Data = item.Data
                            });
                        }
                        // 移动已解析的数据
                        _bufferOffset += offset;
                        _bufferCount -= offset;
                        // 如果剩余空间较大且数据量较小，压缩缓冲区
                        if (_bufferOffset > 4096 && _bufferCount < _bufferOffset)
                        {
                            CompactBuffer();
                        }
                        return result;
                    }
                    else if (offset > 0)
                    {
                        // 解析跳过了一段损坏数据，更新偏移
                        _bufferOffset += offset;
                        _bufferCount -= offset;
                    }
                }
                catch
                {
                    // 解码异常：丢弃前1字节，避免损坏数据永久滞留
                    _bufferOffset += 1;
                    _bufferCount -= 1;
                }
            }
            return result;
        }

        /// <summary>
        /// 追加数据到内部缓冲区
        /// </summary>
        private void AppendData(byte[] data)
        {
            if (data == null || data.Length == 0) return;

            // 如果缓冲区已空，重置偏移以充分利用空间
            if (_bufferCount == 0 && _bufferOffset > 0)
            {
                _bufferOffset = 0;
            }

            // 检查剩余空间是否足够
            int remainingSpace = _buffer.Length - _bufferOffset - _bufferCount;
            if (remainingSpace < data.Length)
            {
                // 如果空间不足，先尝试压缩
                if (_bufferOffset > 0)
                {
                    CompactBuffer();
                    remainingSpace = _buffer.Length - _bufferCount;
                }

                // 如果仍然不够，扩容
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

            // 复制新数据到缓冲区
            Buffer.BlockCopy(data, 0, _buffer, _bufferOffset + _bufferCount, data.Length);
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
            List<byte> list = new List<byte>();

            var total = 12;

            var nlen = 0;

            var tlen = 0;

            byte[] n = null;
            byte[] tp = null;
            byte[] d = null;

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

            list.Add((byte)queueSocketMsg.Type);
            list.AddRange(BitConverter.GetBytes(total));
            list.AddRange(BitConverter.GetBytes(nlen));
            if (nlen > 0)
                list.AddRange(n);
            list.AddRange(BitConverter.GetBytes(tlen));
            if (tlen > 0)
                list.AddRange(tp);
            if (d != null)
                list.AddRange(d);
            var arr = list.ToArray();
            list.Clear();
            return arr;
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
        /// 从ReadOnlySpan中读取Int32，兼容netstandard2.0
        /// </summary>
        private static int ReadInt32(ReadOnlySpan<byte> data, int offset)
        {
            // 使用stackalloc避免堆分配
            Span<byte> temp = stackalloc byte[4];
            data.Slice(offset, 4).CopyTo(temp);
            return BitConverter.ToInt32(temp.ToArray(), 0);
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