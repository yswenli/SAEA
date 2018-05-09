/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.QueueSocket.Net
*文件名： QueueSocketMsg
*版本号： V1.0.0.0
*唯一标识：69a2f1bc-89b4-4e9b-be5b-ae24301d0409
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/5 18:06:01
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/5 18:06:01
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/

using SAEA.QueueSocket.Type;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.QueueSocket.Net
{
    /// <summary>
    /// 队列消息实体
    /// </summary>
    public class QueueSocketMsg : IDisposable
    {

        const int MIN = (1 + 4 + 4 + 0 + 4 + 0 + 0);

        public int Total
        {
            get; set;
        }

        public byte Type
        {
            get; set;
        }

        public int NLen
        {
            get; set;
        }

        public string Name
        {
            get; set;
        }

        public int TLen
        {
            get; set;
        }

        public string Topic
        {
            get; set;
        }

        public string Data
        {
            get; set;
        }
        public QueueSocketMsg(QueueSocketMsgType type) : this(type, string.Empty)
        {

        }
        public QueueSocketMsg(QueueSocketMsgType type, string name) : this(type, name, string.Empty)
        {

        }
        public QueueSocketMsg(QueueSocketMsgType type, string name, string topic) : this(type, name, topic, string.Empty)
        {

        }
        public QueueSocketMsg(QueueSocketMsgType type, string name, string topic, string data)
        {
            this.Type = (byte)type;
            this.Name = name;
            this.Topic = topic;
            this.Data = data;
        }


        public byte[] ToBytes()
        {
            List<byte> list = new List<byte>();

            var total = 4 + 4 + 4;

            var nlen = 0;

            var tlen = 0;

            byte[] n = null;
            byte[] tp = null;
            byte[] d = null;

            if (!string.IsNullOrEmpty(this.Name))
            {
                n = Encoding.UTF8.GetBytes(this.Name);
                nlen = n.Length;
                total += nlen;
            }
            if (!string.IsNullOrEmpty(this.Topic))
            {
                tp = Encoding.UTF8.GetBytes(this.Topic);
                tlen = tp.Length;
                total += tlen;
            }
            if (!string.IsNullOrEmpty(this.Data))
            {
                d = Encoding.UTF8.GetBytes(this.Data);
                total += d.Length;
            }

            list.Add(this.Type);
            list.AddRange(BitConverter.GetBytes(total));
            list.AddRange(BitConverter.GetBytes(nlen));
            if (nlen > 0)
                list.AddRange(n);
            list.AddRange(BitConverter.GetBytes(tlen));
            if (tlen > 0)
                list.AddRange(tp);
            if (d != null)
                list.AddRange(d);
            return list.ToArray();
        }

        /// <summary>
        /// 获取类型
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static QueueSocketMsgType GetType(byte data)
        {
            return (QueueSocketMsgType)data;
        }


        public static int GetTotal(byte[] data)
        {
            return BitConverter.ToInt32(data, 1);
        }


        /// <summary>
        /// 从缓存中转换消息内容
        /// </summary>
        /// <param name="data"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static QueueSocketMsg[] Parse(byte[] data, out int offset)
        {
            offset = 0;

            if (data != null && data.Length > offset + MIN)
            {
                var list = new List<QueueSocketMsg>();

                while (data.Length > offset + MIN)
                {
                    var total = BitConverter.ToInt32(data, offset + 1);

                    if (data.Length >= offset + total)
                    {
                        offset += 5;

                        var qm = new QueueSocketMsg((QueueSocketMsgType)data[0]);
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
                    return list.ToArray();
            }
            offset = 0;
            return null;
        }

        public void Dispose()
        {
            this.Name = this.Topic = this.Data = string.Empty;
            this.Total = this.NLen = this.TLen;
            this.Type = 0;
        }
    }
}
