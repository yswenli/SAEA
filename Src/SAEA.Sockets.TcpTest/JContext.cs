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


        public List<ISocketProtocal> Decode(byte[] data, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
        {
            throw new NotImplementedException();
        }


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


        public void Clear()
        {
            _cache.Clear();
        }

        public byte[] Encode(ISocketProtocal protocal)
        {
            throw new NotImplementedException();
        }
    }
}
