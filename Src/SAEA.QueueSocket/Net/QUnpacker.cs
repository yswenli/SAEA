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
    public sealed class QUnpacker : IUnpacker
    {
        static readonly int MIN = 1 + 4 + 4 + 0 + 4 + 0 + 0;

        private List<byte> _buffer = new List<byte>();

        public void Unpack(byte[] data, Action<ISocketProtocal> unpackCallback, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
        {

        }

        /// <summary>
        /// 队列编解码器
        /// </summary>
        public QueueCoder QueueCoder { get; set; } = new QueueCoder();

        /// <summary>
        /// 包解析
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public List<QueueResult> GetQueueResult(byte[] data)
        {
            try
            {
                _buffer.AddRange(data);

                if (_buffer.Count > (1 + 4 + 4 + 0 + 4 + 0 + 0))
                {
                    var buffer = _buffer.ToArray();

                    var list = QUnpacker.Decode(buffer, out int offset);
                    if (list != null && list.Count>0)
                    {
                        var result = new List<QueueResult>();
                        foreach (var item in list)
                        {
                            result.Add(new QueueResult()
                            {
                                Type = (QueueSocketMsgType)item.Type,
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
                ConsoleHelper.WriteLine("QCoder.GetQueueResult error:" + ex.Message + ex.Source);
                _buffer.Clear();
            }
            return null;
        }

       
        /// <summary>
        /// socket 传输字节编码
        /// 格式为：1+4+4+x+4+x+4
        /// </summary>
        /// <param name="queueSocketMsg"></param>
        /// <returns></returns>
        public static byte[] Encode(QueueSocketMsg queueSocketMsg)
        {
            List<byte> list = new List<byte>();

            var total = 4 + 4 + 4;

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

            list.Add(queueSocketMsg.Type);
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


        public static List<QueueSocketMsg> Decode(byte[] data,out int offset)
        {
            offset = 0;

            try
            {
                if (data != null && data.Length > offset + MIN)
                {
                    var list = new List<QueueSocketMsg>();

                    while (data.Length > offset + MIN)
                    {
                        var total = BitConverter.ToInt32(data, offset + 1);

                        if (data.Length >= offset + total + 1)
                        {
                            var qm = new QueueSocketMsg((QueueSocketMsgType)data[offset]);
                            offset += 5;
                            qm.Total = total;

                            qm.NLen = BitConverter.ToInt32(data, offset);
                            offset += 4;

                            if (qm.NLen > 0)
                            {
                                var narr = data.AsSpan().Slice(offset, qm.NLen).ToArray();
                                qm.Name = Encoding.UTF8.GetString(narr);
                            }
                            offset += qm.NLen;

                            qm.TLen = BitConverter.ToInt32(data, offset);

                            offset += 4;

                            if (qm.TLen > 0)
                            {
                                var tarr = data.AsSpan().Slice(offset, qm.TLen).ToArray();
                                qm.Topic = Encoding.UTF8.GetString(tarr);
                            }
                            offset += qm.TLen;

                            var dlen = qm.Total - 4 - 4 - qm.NLen - 4 - qm.TLen;

                            if (dlen > 0)
                            {
                                var darr = data.AsSpan().Slice(offset, dlen).ToArray();
                                qm.Data = darr;
                                offset += dlen;
                            }
                            list.Add(qm);
                        }
                        else
                        {
                            break;
                        }
                    }

                    return list;
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteLine($"QCoder.Decode error:{ex.Message} stack:{ex.StackTrace} data:{data.Length} offset:{offset}");
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



    }
}
