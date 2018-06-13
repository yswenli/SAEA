/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.QueueSocket.Net
*文件名： QCoder
*版本号： V1.0.0.0
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
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/

using SAEA.Commom;
using SAEA.QueueSocket.Model;
using SAEA.QueueSocket.Type;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.QueueSocket.Net
{
    public sealed class QCoder : ICoder
    {
        static readonly int MIN = 1 + 4 + 4 + 0 + 4 + 0 + 0;

        private List<byte> _buffer = new List<byte>();

        private object _locker = new object();

        public void Pack(byte[] data, Action<DateTime> onHeart, Action<ISocketProtocal> onUnPackage, Action<byte[]> onFile)
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
        /// <param name="OnQueueResult"></param>
        public void GetQueueResult(byte[] data, Action<QueueResult> OnQueueResult)
        {
            lock (_locker)
            {
                try
                {
                    _buffer.AddRange(data);

                    if (_buffer.Count > (1 + 4 + 4 + 0 + 4 + 0 + 0))
                    {
                        var buffer = _buffer.ToArray();

                        QCoder.Decode(buffer, (list, offset) =>
                        {
                            if (list != null)
                            {
                                foreach (var item in list)
                                {
                                    OnQueueResult?.Invoke(new QueueResult()
                                    {
                                        Type = (QueueSocketMsgType)item.Type,
                                        Name = item.Name,
                                        Topic = item.Topic,
                                        Data = item.Data
                                    });
                                }
                                _buffer.RemoveRange(0, offset);
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    ConsoleHelper.WriteLine("QCoder.GetQueueResult error:" + ex.Message + ex.Source);
                    _buffer.Clear();
                }
            }
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
            if (!string.IsNullOrEmpty(queueSocketMsg.Data))
            {
                d = Encoding.UTF8.GetBytes(queueSocketMsg.Data);
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

        /// <summary>
        /// socket 传输字节解码
        /// </summary>
        /// <param name="data"></param>
        /// <param name="onDecode"></param>
        public static bool Decode(byte[] data, Action<QueueSocketMsg[], int> onDecode)
        {
            int offset = 0;

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
                                var narr = new byte[qm.NLen];
                                Buffer.BlockCopy(data, offset, narr, 0, narr.Length);
                                qm.Name = Encoding.UTF8.GetString(narr);
                            }
                            offset += qm.NLen;

                            qm.TLen = BitConverter.ToInt32(data, offset);

                            offset += 4;

                            if (qm.TLen > 0)
                            {
                                var tarr = new byte[qm.TLen];
                                Buffer.BlockCopy(data, offset, tarr, 0, tarr.Length);
                                qm.Topic = Encoding.UTF8.GetString(tarr);
                            }
                            offset += qm.TLen;

                            var dlen = qm.Total - 4 - 4 - qm.NLen - 4 - qm.TLen;

                            if (dlen > 0)
                            {
                                var darr = new byte[dlen];
                                Buffer.BlockCopy(data, offset, darr, 0, dlen);
                                qm.Data = Encoding.UTF8.GetString(darr);
                                offset += dlen;
                            }
                            list.Add(qm);
                        }
                        else
                        {
                            break;
                        }
                    }
                    if (list.Count > 0)
                    {
                        onDecode?.Invoke(list.ToArray(), offset);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteLine($"QCoder.Decode error:{ex.Message} stack:{ex.StackTrace} data:{data.Length} offset:{offset}");
            }
            onDecode?.Invoke(null, 0);
            return false;
        }


        /// <summary>
        /// dispose
        /// </summary>
        public void Dispose()
        {
            _buffer.Clear();
            _buffer = null;
        }



    }
}
