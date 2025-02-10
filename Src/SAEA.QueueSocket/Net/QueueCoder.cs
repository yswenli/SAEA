/****************************************************************************
*Copyright (c) 2018-2022yswenli All Rights Reserved.
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
    public sealed class QueueCoder : ICoder
    {
        static readonly int MIN = 1 + 4 + 4 + 0 + 4 + 0 + 0;

        private List<byte> _buffer = new List<byte>();

        public byte[] Encode(ISocketProtocal protocal)
        {
            throw new NotImplementedException();
        }

        public void Decode(byte[] data, Action<ISocketProtocal> unpackCallback, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
        {

        }

        /// <summary>
        /// 包解析
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public List<QueueMsg> GetQueueResult(byte[] data)
        {
            try
            {
                _buffer.AddRange(data);
                if (_buffer.Count >= MIN)
                {
                    var buffer = _buffer.ToArray();
                    var list = Decode(buffer, out int offset);
                    if (list != null && list.Count > 0)
                    {
                        var result = new List<QueueMsg>();
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
                }
            }
            catch (Exception ex)
            {
                _buffer.Clear();
                ConsoleHelper.WriteLine("QCoder.GetQueueResult error:" + ex.Message + ex.Source);
            }
            return null;
        }


        /// <summary>
        /// socket 传输字节编码
        /// 格式为：1+4+4+x+4+x+x
        /// </summary>
        /// <param name="queueSocketMsg"></param>
        /// <returns></returns>
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


        public static List<QueueSocketMsg> Decode(byte[] data, out int offset)
        {
            offset = 0;
            if (data != null && data.Length > offset + MIN)
            {
                var list = new List<QueueSocketMsg>();

                while (data.Length >= offset + MIN)
                {
                    var typeValue = data[offset];
                    if (typeValue < 1 || typeValue > 7) throw new Exception("QueueSocketMsgType is error");
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
        /// dispose
        /// </summary>
        public void Clear()
        {
            _buffer?.Clear();
        }

        /// <summary>
        /// 按指定格式编码
        /// </summary>
        /// <param name="cmdType"></param>
        /// <param name="contents"></param>
        /// <returns></returns>
        public byte[] Encode(QueueSocketMsgType cmdType, string name, string topic, byte[] data)
        {
            return QueueCoder.Encode(new QueueSocketMsg(cmdType, name, topic, data));
        }
        /// <summary>
        /// 按指定格式编码批量处理
        /// </summary>
        /// <param name="cmdType"></param>
        /// <param name="name"></param>
        /// <param name="topic"></param>
        /// <param name="data"></param>
        /// <returns></returns>
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

        public byte[] Ping(string name)
        {
            return Encode(QueueSocketMsgType.Ping, name, string.Empty, null);
        }

        public byte[] Pong(string name)
        {
            return Encode(QueueSocketMsgType.Pong, name, string.Empty, Encoding.UTF8.GetBytes(DateTimeHelper.ToString()));
        }

        public byte[] Publish(string name, string topic, byte[] data)
        {
            return Encode(QueueSocketMsgType.Publish, name, topic, data);
        }

        public byte[] Subscribe(string name, string topic)
        {
            return Encode(QueueSocketMsgType.Subcribe, name, topic, null);
        }

        public byte[] Unsubcribe(string name, string topic)
        {
            return Encode(QueueSocketMsgType.Unsubcribe, name, topic, null);
        }

        public byte[] Close(string name)
        {
            return Encode(QueueSocketMsgType.Close, name, string.Empty, null);
        }

        public byte[] Data(string name, string topic, byte[] data)
        {
            return Encode(QueueSocketMsgType.Data, name, topic, data);
        }

    }
}
