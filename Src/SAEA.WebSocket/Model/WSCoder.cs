/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.WebSocket
*文件名： Class1
*版本号： v4.1.2.5
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
*版本号： v4.1.2.5
*描述：
*
*****************************************************************************/
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using SAEA.WebSocket.Type;
using System;
using System.Collections.Generic;

namespace SAEA.WebSocket.Model
{
    public class WSCoder : IUnpacker
    {
        private List<byte> _buffer = new List<byte>();

        private object _locker = new object();

        /// <summary>
        /// 服务器端收包处理
        /// 客户发送的数据是必须含掩码的
        /// </summary>
        /// <param name="data"></param>
        /// <param name="unpackCallback"></param>
        /// <param name="OnHeart"></param>
        /// <param name="OnFile"></param>
        public void Unpack(byte[] data, Action<ISocketProtocal> unpackCallback, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
        {
            DeCode(data, unpackCallback);
        }

        /// <summary>
        /// 数据分析
        /// </summary>
        /// <param name="data"></param>
        /// <param name="callBack"></param>
        private void DeCode(byte[] data, Action<ISocketProtocal> callBack)
        {
            lock (_locker)
            {
                byte[] payloadData = null;

                _buffer.AddRange(data);

                var buffer = _buffer.ToArray();

                var opcode = (byte)(buffer[0] & 0x0f);
                bool mask = (buffer[1] & 0x80) == 0x80; // 是否包含掩码  
                int payloadLen = buffer[1] & 0x7F; // 数据长度  

                if (payloadLen + 6 > buffer.Length) return;

                if (mask)
                {
                    var masks = new byte[4];
                    if (payloadLen == 126)
                    {
                        var len = (ushort)(buffer[2] << 8 | buffer[3]);
                        if (len + 8 > buffer.Length) return;

                        Array.Copy(buffer, 4, masks, 0, 4);
                        payloadData = new byte[len];
                        Array.Copy(buffer, 8, payloadData, 0, len);
                        DoMask(payloadData, 0, len, masks);
                        callBack?.Invoke(new WSProtocal((WSProtocalType)opcode, payloadData));
                        _buffer.RemoveRange(0, 8 + len);
                    }
                    else
                    {
                        Array.Copy(buffer, 2, masks, 0, 4);
                        payloadData = new byte[payloadLen];
                        Array.Copy(buffer, 6, payloadData, 0, payloadLen);
                        DoMask(payloadData, 0, payloadLen, masks);
                        callBack?.Invoke(new WSProtocal((WSProtocalType)opcode, payloadData));
                        _buffer.RemoveRange(0, 6 + payloadLen);
                    }
                }
                else
                {
                    if (payloadLen == 126)
                    {
                        var len = (ushort)(buffer[2] << 8 | buffer[3]);
                        if (len + 8 > buffer.Length) return;

                        payloadData = new byte[len];
                        Array.Copy(buffer, 4, payloadData, 0, len);
                        callBack?.Invoke(new WSProtocal((WSProtocalType)opcode, payloadData));
                        _buffer.RemoveRange(0, 4 + len);
                    }
                    else
                    {
                        payloadData = new byte[payloadLen];
                        Array.Copy(buffer, 2, payloadData, 0, payloadLen);
                        callBack?.Invoke(new WSProtocal((WSProtocalType)opcode, payloadData));
                        _buffer.RemoveRange(0, 2 + payloadLen);
                    }
                }
            }
        }


        /// <summary>
        /// 掩码运算
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        /// <param name="masks"></param>
        public static void DoMask(byte[] buffer, int offset, int length, byte[] masks)
        {
            for (var i = 0; i < length; i++)
            {
                buffer[offset + i] = (byte)(buffer[offset + i] ^ masks[i % 4]);
            }
        }

        public void Clear()
        {
            _buffer.Clear();
        }

    }
}
