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
*命名空间：SAEA.Sockets.Model
*文件名： BaseUnpacker
*版本号： V3.3.3.3
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*修改时间：2018/10/26 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/10/26 15:54:21
*修改人： yswenli
*版本号： V3.3.3.3
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;

namespace SAEA.Sockets.Model
{
    public class BaseUnpacker : IUnpacker
    {
        public const int P_LEN = 8;

        public const int P_Type = 1;

        public const int P_Head = 9;

        private List<byte> _buffer = new List<byte>();

        object _locker = new object();

        public void Unpack(byte[] data, Action<ISocketProtocal> unpackCallback, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
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
                            unpackCallback?.Invoke(sm);
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

        public void Clear()
        {
            _buffer.Clear();
            _buffer = null;
        }
    }
}
