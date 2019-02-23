/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RPC.Net
*文件名： RCoder
*版本号： v4.1.2.5
*唯一标识：c56d3df1-0ff6-4497-828a-e6de342dd876
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/16 15:14:20
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/16 15:14:20
*修改人： yswenli
*版本号： v4.1.2.5
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.RPC.Model;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RPC.Net
{
    /// <summary>
    /// SAEA.RPC传输编解码
    /// 格式为：1+4+8+4+x+4+x+x
    /// </summary>
    public sealed class RCoder : IUnpacker
    {

        //内置
        public void Unpack(byte[] data, Action<ISocketProtocal> unpackCallback, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
        {

        }


        /// <summary>
        /// socket 传输字节编码
        /// 格式为：1+4+8+4+x+4+x+x
        /// </summary>
        /// <param name="queueSocketMsg"></param>
        /// <returns></returns>
        public byte[] Encode(RSocketMsg queueSocketMsg)
        {
            List<byte> list = new List<byte>();

            var total = 4 + 8 + 4 + 4;

            var nlen = 0;

            var tlen = 0;

            byte[] n = null;
            byte[] tp = null;
            byte[] d = null;

            if (!string.IsNullOrEmpty(queueSocketMsg.ServiceName))
            {
                n = Encoding.UTF8.GetBytes(queueSocketMsg.ServiceName);
                nlen = n.Length;
                total += nlen;
            }
            if (!string.IsNullOrEmpty(queueSocketMsg.MethodName))
            {
                tp = Encoding.UTF8.GetBytes(queueSocketMsg.MethodName);
                tlen = tp.Length;
                total += tlen;
            }
            if (queueSocketMsg.Data != null)
            {
                d = queueSocketMsg.Data;
                total += d.Length;
            }

            list.Add(queueSocketMsg.Type);
            list.AddRange(BitConverter.GetBytes(total));
            list.AddRange(BitConverter.GetBytes(queueSocketMsg.SequenceNumber));
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


        static readonly int MIN = 1 + 4 + 8 + 4 + 0 + 4 + 0 + 0;

        private List<byte> _buffer = new List<byte>();

        private object _locker = new object();


        /// <summary>
        /// socket 传输字节解码
        /// 格式为：1+4+8+4+x+4+x+x
        /// </summary>
        /// <param name="data"></param>
        /// <param name="onDecode"></param>
        bool Decode(byte[] data, Action<RSocketMsg[], uint> onDecode)
        {
            uint offset = 0;

            try
            {
                if (data != null && data.Length >= offset + MIN)
                {
                    var list = new List<RSocketMsg>();

                    while (data.Length >= offset + MIN)
                    {
                        var total = BitConverter.ToUInt32(data, (int)offset + 1);

                        if (data.Length >= offset + total + 1)
                        {
                            var qm = new RSocketMsg((RSocketMsgType)data[offset]);
                            offset += 5;
                            qm.Total = total;
                            qm.SequenceNumber = BitConverter.ToInt64(data, (int)offset);

                            offset += 8;

                            qm.SLen = BitConverter.ToUInt32(data, (int)offset);
                            offset += 4;


                            if (qm.SLen > 0)
                            {
                                var narr = new byte[qm.SLen];
                                Buffer.BlockCopy(data, (int)offset, narr, 0, narr.Length);
                                qm.ServiceName = Encoding.UTF8.GetString(narr);
                            }
                            offset += qm.SLen;

                            qm.MLen = BitConverter.ToUInt32(data, (int)offset);

                            offset += 4;

                            if (qm.MLen > 0)
                            {
                                var tarr = new byte[qm.MLen];
                                Buffer.BlockCopy(data, (int)offset, tarr, 0, tarr.Length);
                                qm.MethodName = Encoding.UTF8.GetString(tarr);
                            }
                            offset += qm.MLen;

                            var dlen = qm.Total - 8 - 4 - 4 - qm.SLen - 4 - qm.MLen;

                            if (dlen > 0)
                            {
                                var darr = new byte[dlen];
                                Buffer.BlockCopy(data, (int)offset, darr, 0, (int)dlen);
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
                    if (list.Count > 0)
                    {
                        onDecode?.Invoke(list.ToArray(), offset);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteLine($"RCoder.Decode error:{ex.Message} stack:{ex.StackTrace} data:{data.Length} offset:{offset}");
            }
            onDecode?.Invoke(null, 0);
            return false;
        }



        /// <summary>
        /// 获取传输包
        /// </summary>
        /// <param name="data"></param>
        /// <param name="onServerUnpacked"></param>
        public void Unpack(byte[] data, Action<RSocketMsg> onServerUnpacked)
        {
            lock (_locker)
            {
                _buffer.AddRange(data);

                if (_buffer.Count >= (1 + 4 + 4 + 0 + 4 + 0 + 0))
                {
                    var buffer = _buffer.ToArray();

                    this.Decode(buffer, (list, offset) =>
                    {
                        if (list != null)
                        {
                            foreach (var item in list)
                            {
                                TaskHelper.Start(() => { onServerUnpacked.Invoke(item); });
                            }
                            _buffer.RemoveRange(0, (int)offset);
                        }
                    });
                }
            }
        }

        /// <summary>
        /// dispose
        /// </summary>
        public void Clear()
        {
            _buffer.Clear();
        }

    }
}
