/****************************************************************************
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets
*文件名： Class1
*版本号： V1.0.0.0
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 15:54:21
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;

namespace SAEA.Sockets.Model
{
    /// <summary>
    /// 通信数据接收解析器
    /// </summary>
    public sealed class Coder : ICoder
    {
        public const int P_LEN = 8;

        public const int P_Type = 1;

        public const int P_Head = 9;

        private List<byte> _buffer = new List<byte>();

        private object _locker = new object();
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

                while (_buffer.Count >= P_Head)
                {
                    var buffer = _buffer.ToArray();

                    var bodyLen = GetLength(buffer);

                    var type = GetType(buffer);

                    if (bodyLen == 0 && type == SocketProtocalType.Heart) //空包认为是心跳包
                    {
                        var sm = new SocketProtocal() { BodyLength = bodyLen, Type = (byte)type };
                        _buffer.Clear();
                        onHeart?.Invoke(DateTimeHelper.Now);
                    }
                    else if (buffer.Length >= P_Head + bodyLen)
                    {
                        if (type == SocketProtocalType.BigData)
                        {
                            var content = GetContent(buffer, P_Head, (int)bodyLen);
                            _buffer.RemoveRange(0, (int)(P_Head + bodyLen));
                            bodyLen = 0;
                            onFile?.Invoke(content);
                        }
                        else
                        {
                            var sm = new SocketProtocal() { BodyLength = bodyLen, Type = (byte)type, Content = GetContent(buffer, P_Head, (int)bodyLen) };
                            _buffer.RemoveRange(0, (int)(P_Head + bodyLen));
                            bodyLen = 0;
                            onUnPackage?.Invoke(sm);
                        }
                    }
                    else
                    {
                        return;
                    }
                }
            }
        }

        public static long GetLength(byte[] data)
        {
            return data.ToLong();
        }

        public static SocketProtocalType GetType(byte[] data)
        {
            return (SocketProtocalType)data[P_LEN];
        }

        public static byte[] GetContent(byte[] data, int offset, int count)
        {
            var buffer = new byte[count];
            Buffer.BlockCopy(data, offset, buffer, 0, count);
            return buffer;
        }

        public void Dispose()
        {
            _buffer.Clear();
            _buffer = null;
        }

    }
}
