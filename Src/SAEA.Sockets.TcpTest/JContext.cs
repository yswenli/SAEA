/****************************************************************************
*项目名称：SAEA.Sockets.TcpTest
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Sockets.TcpTest
*类 名 称：JContext
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/30 15:27:27
*描述：
*=====================================================================
*修改时间：2020/12/30 15:27:27
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;

using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;

namespace SAEA.Sockets.TcpTest
{

    /// <summary>
    /// 解包
    /// </summary>
    public class JUnpacker : ICoder
    {
        const byte SplitByte = 126;//0x7E

        List<byte> _cache = new List<byte>();


        /// <summary>
        /// 直接从序列中解帧，当前未提供实现，调用即抛 NotImplementedException
        /// </summary>
        /// <param name="data">待解帧的只读序列</param>
        /// <param name="onHeart">心跳回调，当前实现忽略</param>
        /// <param name="onFile">文件数据回调，当前实现忽略</param>
        /// <returns>解出的帧集合，当前实现不返回</returns>
        public SAEA.Sockets.Base.DecodedFrames Decode(System.Buffers.ReadOnlySequence<byte> data, Action<DateTime> onHeart = null, Action<ReadOnlyMemory<byte>> onFile = null)
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// 把本次收到的字节追加到内部缓存，再以 0x7E 为分隔符切出一个完整帧
        /// </summary>
        /// <param name="data">本次收到的字节</param>
        /// <returns>含首尾 0x7E 的完整帧；尚未收齐时返回 null，未消费的字节留在缓存中</returns>
        public byte[] Decode(byte[] data)
        {
            _cache.AddRange(data);

            int start = -1, end = -1;

            bool started = false, ended = false;

            foreach (var item in _cache)
            {
                if (item == SplitByte)
                {
                    if (started && !ended)
                    {
                        end++;
                        ended = true;
                    }

                    if (!started)
                    {
                        start++;
                        end++;
                        started = true;
                    }
                }
                else
                {
                    if (!started) start++;
                    if (!ended) end++;
                }
            }

            if (ended)
            {
                var result = _cache.Skip(start).Take(end - start + 1).ToArray();
                _cache.RemoveAt(end);
                return result;
            }
            return null;
        }


        /// <summary>
        /// 清空内部缓存，用于连接重建时丢弃残留的半包数据
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
        }

        /// <summary>
        /// 把协议对象编码写入缓冲区，当前未提供实现，调用即抛 NotImplementedException
        /// </summary>
        /// <param name="protocal">待编码的协议对象</param>
        /// <param name="writer">输出缓冲区写入器</param>
        public void Encode(ISocketProtocal protocal, System.Buffers.IBufferWriter<byte> writer)
        {
            throw new NotImplementedException();
        }
    }
}
