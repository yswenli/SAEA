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
using System.IO;
using System.Linq;

using SAEA.Common;
using SAEA.Sockets.Interface;
using SAEA.WebSocket.Type;

namespace SAEA.WebSocket.Model
{
    /// <summary>
    /// websocket 数据协议
    /// </summary>
    public class WSProtocal : ISocketProtocal, IDisposable
    {
        int _mask = RandomHelper.GetInt(1);

        public long BodyLength { get; set; }
        public byte[] Content { get; set; }
        public byte Type { get; set; }

        public WSProtocal(byte type, byte[] content)
        {
            this.Type = type;
            if (content != null)
                this.BodyLength = content.Length;
            else
                this.BodyLength = 0;
            this.Content = content;
        }

        public WSProtocal(WSProtocalType type, byte[] content)
        {
            this.Type = (byte)type;
            if (content != null)
                this.BodyLength = content.Length;
            else
                this.BodyLength = 0;
            this.Content = content;
        }


        /// <summary>
        /// 将当前实体转换成websocket所需的结构
        /// </summary>
        /// <param name="masked"></param>
        /// <returns></returns>
        public byte[] ToBytes(bool masked)
        {
            int payloadLength;

            byte[] extPayloadLength;

            ulong len = (ulong)this.BodyLength;

            if (len < 126)
            {
                payloadLength = (byte)len;
                extPayloadLength = new byte[0];
            }
            else if (len < 0x010000)
            {
                payloadLength = (byte)126;
                extPayloadLength = ((ushort)len).InternalToByteArray(EndianOrder.Big);
            }
            else
            {
                payloadLength = (byte)127;
                extPayloadLength = len.InternalToByteArray(EndianOrder.Big);
            }

            using (var buff = new MemoryStream())
            {
                var header = (int)0x1;
                header = (header << 1) + (byte)0x0;
                header = (header << 1) + (byte)0x0;
                header = (header << 1) + (byte)0x0;
                header = (header << 4) + this.Type;

                if (masked)
                    header = (header << 1) + (byte)0x1;
                else
                    header = (header << 1) + (byte)0x0;

                header = (header << 7) + (byte)payloadLength;
                buff.Write(((ushort)header).InternalToByteArray(EndianOrder.Big), 0, 2);


                byte[] maskBytes = null;

                if (masked)
                {
                    maskBytes = _mask.ToBytes();
                    buff.Write(maskBytes, 0, 4);
                }


                if (payloadLength > 125)
                    buff.Write(extPayloadLength, 0, payloadLength == 126 ? 2 : 8);


                if (payloadLength > 0)
                {
                    if (masked)
                    {
                        WSCoder.DoMask(this.Content, 0, this.Content.Length, maskBytes);
                    }

                    buff.Write(this.Content, 0, (int)this.BodyLength);
                }

                buff.Flush();

                return buff.ToArray();
            }
        }

        /// <summary>
        /// 将当前实体转换成websocket所需的结构
        /// </summary>
        /// <returns></returns>
        public byte[] ToBytes()
        {
            return ToBytes(true);
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            if(this.Content!=null && this.Content.Any())
            {
                this.Content.Clear();
            }
        }
    }
}
