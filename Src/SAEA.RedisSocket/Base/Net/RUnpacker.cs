/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket.Base.Net
*文件名： RUnpacker
*版本号： V3.6.2.2
*唯一标识：a22caf84-4c61-456e-98cc-cbb6cb2c6d6e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/11/5 20:45:02
*描述：
*
*=====================================================================
*修改标记
*创建时间：2018/11/5 20:45:02
*修改人： yswenli
*版本号： V3.6.2.2
*描述：
*
*****************************************************************************/
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;

namespace SAEA.RedisSocket.Base.Net
{
    /// <summary>
    /// 通信数据接收解析器
    /// </summary>
    public sealed class RUnpacker : IUnpacker
    {
        private object _locker = new object();

        List<byte> _buffer = new List<byte>(102400);

        /// <summary>
        /// 收包处理
        /// </summary>
        /// <param name="data"></param>
        /// <param name="OnHeart"></param>
        /// <param name="OnUnPackage"></param>
        /// <param name="onFile"></param>
        public void Unpack(byte[] data, Action<ISocketProtocal> unpackCallback, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
        {
            _buffer.AddRange(data);

            var sp = _buffer.AsReadOnly();

            var count = sp.Count;

            if (sp[count - 1] == 10 && sp[count - 2] == 13)
            {
                unpackCallback.Invoke(new RMessage() { Content = _buffer.ToArray() });
                _buffer.Clear();
            }
        }


        public void Clear()
        {
            _buffer.Clear();
        }

    }
}
