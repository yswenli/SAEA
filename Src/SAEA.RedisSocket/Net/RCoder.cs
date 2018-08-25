/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RedisSocket.Net
*文件名： RCoder
*版本号： V1.0.0.0
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
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SAEA.RedisSocket.Net
{
    /// <summary>
    /// 通信数据接收解析器
    /// </summary>
    public sealed class RCoder : ICoder
    {
        private object _locker = new object();

        List<byte> _buffer = new List<byte>();

        long _position = 0;

        /// <summary>
        /// 服务器端收包处理
        /// </summary>
        /// <param name="data"></param>
        /// <param name="OnHeart"></param>
        /// <param name="OnUnPackage"></param>
        /// <param name="onFile"></param>
        public void Pack(byte[] data, Action<DateTime> onHeart, Action<ISocketProtocal> onUnPackage, Action<byte[]> onFile)
        {
            lock (_locker)
            {
                _buffer.AddRange(data);

                //while (_buffer.Count > 0)
                //{
                //var buffer = new List<byte>();
                //for (int i = 0; i < _buffer.Count; i++)
                //{
                //    _position++;
                //    buffer.Add(_buffer[i]);
                //    if (i > 0 && buffer[i - 1] == 13 && buffer[i] == 10)
                //    {
                //        onUnPackage.Invoke(new RMessage() { Content = buffer.ToArray() });
                //        buffer.Clear();
                //        break;
                //    }
                //}
                //if (buffer.Count == 0)
                //{
                //    _buffer.RemoveRange(0, (int)_position);
                //    _position = 0;
                //}
                //buffer.Clear();
                //buffer = null;

                //}

                if (_buffer.LastIndexOf(13) == _buffer.Count - 2 && _buffer.LastIndexOf(10) == _buffer.Count - 1)
                {
                    onUnPackage.Invoke(new RMessage() { Content = _buffer.ToArray() });
                    _buffer.Clear();
                }
            }
        }




        public void Dispose()
        {
            _buffer.Clear();
            _buffer = null;
        }

    }
}
