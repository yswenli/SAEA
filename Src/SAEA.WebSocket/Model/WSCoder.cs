/****************************************************************************
*Copyright (c) 2018-2022yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.WebSocket
*文件名： Class1
*版本号： v7.0.0.1
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
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
using System;
using System.Buffers;

using SAEA.Sockets.Interface;

namespace SAEA.WebSocket.Model
{
    public class WSCoder : IUnpacker
    {
        private byte[] _buffer;
        private int _bufferLength = 0;

        private object _locker = new object();

        public WSCoder()
        {
            // 初始化缓冲区
            _buffer = ArrayPool<byte>.Shared.Rent(4096);
        }

        public void Unpack(byte[] data, Action<ISocketProtocal> unpackCallback, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
        {
            DeCode(data, unpackCallback);
        }

        private void DeCode(byte[] data, Action<ISocketProtocal> callBack)
        {
            byte[] payloadData = null;

            lock (_locker)
            {
                // 将新数据添加到缓冲区
                data.CopyTo(_buffer, _bufferLength);
                _bufferLength += data.Length;

                var buffer = _buffer.AsSpan(0, _bufferLength);

                var opcode = (byte)(buffer[0] & 0x0f);
                bool mask = (buffer[1] & 0x80) == 0x80; // 是否包含掩码  
                int payloadLen = buffer[1] & 0x7F; // 数据长度  

                if (payloadLen + 2 > buffer.Length) return;

                if (mask)
                {
                    var masks = new byte[4];
                    if (payloadLen == 126)
                    {
                        var len = (ushort)(buffer[2] << 8 | buffer[3]);
                        if (len + 8 > buffer.Length) return;

                        masks.CopyTo(_buffer, 4);
                        payloadData = ArrayPool<byte>.Shared.Rent(len);
                        buffer.Slice(8, len).CopyTo(payloadData);
                        DoMask(payloadData, 0, len, masks);
                        callBack?.Invoke(new WSProtocal(opcode, payloadData));
                        ArrayPool<byte>.Shared.Return(payloadData);
                        _bufferLength -= 8 + len;
                        Array.Copy(_buffer, 8 + len, _buffer, 0, _bufferLength);
                    }
                    else
                    {
                        masks.CopyTo(_buffer, 2);
                        payloadData = ArrayPool<byte>.Shared.Rent(payloadLen);
                        buffer.Slice(6, payloadLen).CopyTo(payloadData);
                        DoMask(payloadData, 0, payloadLen, masks);
                        callBack?.Invoke(new WSProtocal(opcode, payloadData));
                        ArrayPool<byte>.Shared.Return(payloadData);
                        _bufferLength -= 6 + payloadLen;
                        Array.Copy(_buffer, 6 + payloadLen, _buffer, 0, _bufferLength);
                    }
                }
                else
                {
                    if (payloadLen == 126)
                    {
                        var len = (ushort)(buffer[2] << 8 | buffer[3]);
                        if (len + 8 > buffer.Length) return;

                        payloadData = ArrayPool<byte>.Shared.Rent(len);
                        buffer.Slice(4, len).CopyTo(payloadData);
                        callBack?.Invoke(new WSProtocal(opcode, payloadData));
                        ArrayPool<byte>.Shared.Return(payloadData);
                        _bufferLength -= 4 + len;
                        Array.Copy(_buffer, 4 + len, _buffer, 0, _bufferLength);
                    }
                    else
                    {
                        payloadData = ArrayPool<byte>.Shared.Rent(payloadLen);
                        buffer.Slice(2, payloadLen).CopyTo(payloadData);
                        callBack?.Invoke(new WSProtocal(opcode, payloadData));
                        ArrayPool<byte>.Shared.Return(payloadData);
                        _bufferLength -= 2 + payloadLen;
                        Array.Copy(_buffer, 2 + payloadLen, _buffer, 0, _bufferLength);
                    }
                }
            }
        }

        public static void DoMask(byte[] buffer, int offset, int length, byte[] masks)
        {
            for (var i = 0; i < length; i++)
            {
                buffer[offset + i] = (byte)(buffer[offset + i] ^ masks[i % 4]);
            }
        }

        public void Clear()
        {
            lock (_locker)
            {
                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = ArrayPool<byte>.Shared.Rent(4096);
                _bufferLength = 0;
            }
        }

        ~WSCoder()
        {
            ArrayPool<byte>.Shared.Return(_buffer);
        }
    }
}

