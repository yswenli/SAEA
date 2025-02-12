/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.QueueSocket.Net
*文件名： QCoder
*版本号： v7.0.0.1
*唯一标识：88f5a779-8294-47bc-897b-8357a09f2fdb
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/5 18:01:56
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/5 18:01:56
*修改人： yswenli
*版本号： v7.0.0.1
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

        // 定义一个字节列表缓冲区
        private List<byte> _buffer = new List<byte>();

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
            _buffer.AddRange(data);
            if (_buffer.Count >= MIN)
            {
                var array = _buffer.ToArray();
                var list = Decode(array, out int offset);
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
                    _buffer.RemoveRange(0, offset);
                    return result;
                }
                array.Clear();
            }
            return result;
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
            offset = 0;
            if (data != null && data.Length > offset + MIN)
            {
                var list = new List<QueueSocketMsg>();
                while (data.Length >= offset + MIN)
                {
                    var typeValue = data[offset];
                    if (typeValue < 1 || typeValue > 7)
                    {
                        //丢弃通信中接收到的不正常数据，并在data数组中的找到中找到第一个正确的typeValue
                        for (var i = 0; i < data.Length; i++)
                        {
                            if (data[i] >= 1 && data[i] <= 7)
                            {
                                typeValue = data[i];
                                offset = i;
                                break;
                            }
                        }
                        if (offset == 0)
                        {
                            return list;
                        }
                    }
                    QueueSocketMsgType type = (QueueSocketMsgType)typeValue;
                    offset += 1;
                    var total = BitConverter.ToInt32(data, offset);
                    if (total > 100 * 1024 * 1024) throw new Exception("QueueSocketMsg is too large");
                    if (data.Length >= offset + total)
                    {
                        offset += 4;
                        var qm = new QueueSocketMsg(type);
                        qm.Total = total;

                        qm.NameLength = BitConverter.ToInt32(data, offset);
                        offset += 4;

                        if (qm.NameLength > 0)
                        {
                            var narr = data.AsSpan().Slice(offset, qm.NameLength).ToArray();
                            qm.Name = Encoding.UTF8.GetString(narr);
                        }
                        offset += qm.NameLength;

                        qm.TopicLength = BitConverter.ToInt32(data, offset);
                        offset += 4;

                        if (qm.TopicLength > 0)
                        {
                            var tarr = data.AsSpan().Slice(offset, qm.TopicLength).ToArray();
                            qm.Topic = Encoding.UTF8.GetString(tarr);
                        }
                        offset += qm.TopicLength;

                        var dlen = qm.Total - 4 - 4 - qm.NameLength - 4 - qm.TopicLength;

                        if (dlen > 0)
                        {
                            var darr = data.AsSpan().Slice(offset, dlen).ToArray();
                            qm.Data = darr;
                        }
                        offset += dlen;
                        list.Add(qm);
                    }
                    else
                    {
                        break;
                    }
                }
                return list;
            }
            return null;
        }

        /// <summary>
        /// 清除方法，清除编码器内部状态
        /// </summary>
        public void Clear()
        {
            _buffer?.Clear();
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
