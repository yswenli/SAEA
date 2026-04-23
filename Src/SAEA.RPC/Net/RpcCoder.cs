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
*命名空间：SAEA.RPC.Net
*文件名： RpcCoder
*版本号： v26.4.23.1
*唯一标识：833a88d7-8c3c-4b95-867c-7afe54459e9c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2025/02/10 17:07:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2025/02/10 17:07:21
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Common.Threading;
using SAEA.RPC.Model;
using SAEA.Sockets.Interface;

namespace SAEA.RPC.Net
{
    /// <summary>
    /// SAEA.RPC传输编解码
    /// 格式为：1+4+8+4+x+4+x+x
    /// </summary>
    public sealed class RpcCoder : ICoder
    {
        public byte[] Encode(ISocketProtocal protocal)
        {
            return Encode(protocal as RSocketMsg);
        }

        //内置
        public List<ISocketProtocal> Decode(byte[] data, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
        {
            throw new NotImplementedException();
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
            List<RSocketMsg> list = null;

            try
            {
                if (data != null && data.Length >= offset + MIN)
                {
                    list = new List<RSocketMsg>();

                    while (data.Length >= offset + MIN)
                    {
                        var total = BitConverter.ToUInt32(data, (int)offset + 1);

                        // 验证total至少为20（固定头部长度），防止数据错乱导致uint下溢
                        if (total < 20)
                        {
                            break;
                        }

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
                                // ServiceName通常较小，直接复制
                                var narr = new byte[qm.SLen];
                                Buffer.BlockCopy(data, (int)offset, narr, 0, narr.Length);
                                qm.ServiceName = Encoding.UTF8.GetString(narr);
                            }
                            offset += qm.SLen;

                            qm.MLen = BitConverter.ToUInt32(data, (int)offset);

                            offset += 4;

                            if (qm.MLen > 0)
                            {
                                // MethodName通常较小，直接复制
                                var tarr = new byte[qm.MLen];
                                Buffer.BlockCopy(data, (int)offset, tarr, 0, tarr.Length);
                                qm.MethodName = Encoding.UTF8.GetString(tarr);
                            }
                            offset += qm.MLen;

                            var dlen = qm.Total - 8 - 4 - 4 - qm.SLen - 4 - qm.MLen;

                            if (dlen > 0)
                            {
                                // 大数据使用内存池，小数据直接分配
                                if (dlen > MemoryPoolManager.SmallThreshold)
                                {
                                    var darr = MemoryPoolManager.Rent((int)dlen);
                                    Buffer.BlockCopy(data, (int)offset, darr, 0, (int)dlen);
                                    qm.Data = darr;
                                    qm.IsPooled = true;
                                }
                                else
                                {
                                    var darr = new byte[dlen];
                                    Buffer.BlockCopy(data, (int)offset, darr, 0, (int)dlen);
                                    qm.Data = darr;
                                    qm.IsPooled = false;
                                }
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
                // 如果已经解析出部分消息，先返回已解析的部分，避免已处理数据滞留缓冲区
                if (list != null && list.Count > 0)
                {
                    onDecode?.Invoke(list.ToArray(), offset);
                    return true;
                }
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
            _buffer.AddRange(data);

            if (_buffer.Count >= MIN)
            {
                var buffer = _buffer.ToArray();

                Decode(buffer, (list, offset) =>
                {
                    if (list != null)
                    {
                        foreach (var item in list)
                        {
                            TaskHelper.Run(() => 
                            { 
                                onServerUnpacked.Invoke(item);
                                // 消息处理完成后，如果数据来自内存池，归还缓冲区
                                if (item.IsPooled && item.Data != null)
                                {
                                    MemoryPoolManager.Return(item.Data, item.Data.Length);
                                    item.IsPooled = false;
                                    item.Data = null;
                                }
                            });
                        }
                        _buffer.RemoveRange(0, (int)offset);
                    }
                });
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