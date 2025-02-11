/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using SAEA.Common;
using SAEA.Sockets.Interface;

namespace SAEA.WebSocket.Model
{
    /// <summary>
    /// 编码协议
    /// </summary>
    public class WSCoder : ICoder
    {
        private List<byte> _buffer;

        /// <summary>
        /// 编码协议
        /// </summary>
        public WSCoder()
        {
            _buffer = new List<byte>(4096);
        }

        /// <summary>
        /// 编码协议数据
        /// </summary>
        /// <param name="protocal">协议数据</param>
        /// <returns>编码后的字节数组</returns>
        public byte[] Encode(ISocketProtocal protocal)
        {
            return protocal.ToBytes();
        }

        /// <summary>
        /// 解码数据
        /// </summary>
        /// <param name="data">接收到的数据</param>
        /// <param name="onHeart">心跳回调</param>
        /// <param name="onFile">文件回调</param>
        public List<ISocketProtocal> Decode(byte[] data, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
        {
            return Decode(data);
        }

        /// <summary>
        /// 解码数据的具体实现
        /// </summary>
        /// <param name="data">接收到的数据</param>
        private List<ISocketProtocal> Decode(byte[] data)
        {
            // 将新数据添加到缓冲区
            _buffer.AddRange(data);
            List<ISocketProtocal> result = new List<ISocketProtocal>();
            while (_buffer.Count > 3)
            {
                byte[] payloadData;
                var opcode = (byte)(_buffer[0] & 0x0f);
                bool mask = (_buffer[1] & 0x80) == 0x80; // 是否包含掩码  
                int payloadLen = _buffer[1] & 0x7F; // 数据长度
                var buffer = _buffer.ToArray();
                if (payloadLen + 2 > _buffer.Count) break;
                if (mask)
                {
                    var masks = new byte[4];
                    if (payloadLen == 126)
                    {
                        var len = (ushort)(_buffer[2] << 8 | _buffer[3]);
                        if (len + 8 > _buffer.Count) break;
                        buffer.AsSpan().Slice(4, 4).CopyTo(masks);
                        payloadData = new byte[len];
                        buffer.AsSpan().Slice(8, len).CopyTo(payloadData);
                        DoMask(payloadData, 0, len, masks);
                        result.Add(new WSProtocal(opcode, payloadData));
                        if (_buffer.Count >= 8 + len)
                            _buffer.RemoveRange(0, 8 + len);
                        else
                            break;
                    }
                    else
                    {
                        if (_buffer.Count < 6 + payloadLen) break;
                        buffer.AsSpan().Slice(2, 4).CopyTo(masks);
                        payloadData = new byte[payloadLen];
                        buffer.AsSpan().Slice(6, payloadLen).CopyTo(payloadData);
                        DoMask(payloadData, 0, payloadLen, masks);
                        result.Add(new WSProtocal(opcode, payloadData));
                        if (_buffer.Count >= 6 + payloadLen)
                            _buffer.RemoveRange(0, 6 + payloadLen);
                        else
                            break;
                    }
                }
                else
                {
                    if (payloadLen == 126)
                    {
                        var len = (ushort)(buffer[2] << 8 | buffer[3]);
                        if (len + 8 > buffer.Length) break;
                        payloadData = new byte[len];
                        buffer.AsSpan().Slice(4, len).CopyTo(payloadData);
                        result.Add(new WSProtocal(opcode, payloadData));
                        if (_buffer.Count >= 4 + len)
                            _buffer.RemoveRange(0, 4 + len);
                        else
                            break;
                    }
                    else
                    {
                        if (_buffer.Count < 2 + payloadLen) break;
                        payloadData = new byte[payloadLen];
                        buffer.AsSpan().Slice(2, payloadLen).CopyTo(payloadData);
                        result.Add(new WSProtocal(opcode, payloadData));
                        if (_buffer.Count >= 2 + payloadLen)
                            _buffer.RemoveRange(0, 2 + payloadLen);
                        else
                            break;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 对数据进行掩码处理
        /// </summary>
        /// <param name="buffer">数据缓冲区</param>
        /// <param name="offset">偏移量</param>
        /// <param name="length">数据长度</param>
        /// <param name="masks">掩码</param>
        public static void DoMask(byte[] buffer, int offset, int length, byte[] masks)
        {
            for (var i = 0; i < length; i++)
            {
                buffer[offset + i] = (byte)(buffer[offset + i] ^ masks[i % 4]);
            }
        }

        /// <summary>
        /// 清空缓冲区
        /// </summary>
        public void Clear()
        {
            _buffer.Clear();
        }
    }
}

