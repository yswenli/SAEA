/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.TcpP2P.Net
*文件名： QCoder
*版本号： V1.0.0.0
*唯一标识：3bce3dda-573a-4414-8d54-b2b7e1493251
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 21:31:28
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 21:31:28
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/

using SAEA.Commom;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.TcpP2P.Net
{
    public sealed class PCoder : ICoder
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

                    if (bodyLen == 0 && type == HolePunchingType.Heart) //空包认为是心跳包
                    {
                        var sm = new PSocketMsg() { BodyLength = bodyLen, Type = (byte)type };
                        _buffer.Clear();
                        onHeart?.Invoke(DateTimeHelper.Now);
                    }
                    else if (buffer.Length >= P_Head + bodyLen)
                    {
                        var sm = new PSocketMsg() { BodyLength = bodyLen, Type = (byte)type, Content = GetContent(buffer, P_Head, (int)bodyLen) };
                        _buffer.RemoveRange(0, (int)(P_Head + bodyLen));
                        bodyLen = 0;
                        onUnPackage?.Invoke(sm);
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

        public static HolePunchingType GetType(byte[] data)
        {
            return (HolePunchingType)data[P_LEN];
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
