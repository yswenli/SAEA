/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RedisSocket.Net
*文件名： RCoder
*版本号： V3.1.1.0
*唯一标识：25bd9770-a61b-4fae-adda-e122d3834826
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/16 10:26:33
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/16 10:26:33
*修改人： yswenli
*版本号： V3.1.1.0
*描述：
*
*****************************************************************************/
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;

namespace SAEA.RedisSocket.Net
{
    /// <summary>
    /// 通信数据接收解析器
    /// </summary>
    public sealed class RUnpacker : IUnpacker
    {
        private object _locker = new object();

        List<byte> _buffer = new List<byte>();

        /// <summary>
        /// 服务器端收包处理
        /// </summary>
        /// <param name="data"></param>
        /// <param name="OnHeart"></param>
        /// <param name="OnUnPackage"></param>
        /// <param name="onFile"></param>
        public void Unpack(byte[] data, Action<ISocketProtocal> unpackCallback, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
        {
            lock (_locker)
            {
                _buffer.AddRange(data);

                if (_buffer.LastIndexOf(13) == _buffer.Count - 2 && _buffer.LastIndexOf(10) == _buffer.Count - 1)
                {
                    unpackCallback.Invoke(new RMessage() { Content = _buffer.ToArray() });
                    _buffer.Clear();
                }
            }
        }




        public void Clear()
        {
            _buffer.Clear();
        }

    }
}
