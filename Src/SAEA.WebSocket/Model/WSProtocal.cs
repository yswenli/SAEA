/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.WebSocket
*文件名： Class1
*版本号： V3.1.0.0
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
*版本号： V3.1.0.0
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Sockets.Interface;
using SAEA.WebSocket.Type;
using System;
using System.IO;

namespace SAEA.WebSocket.Model
{
    public class WSProtocal : ISocketProtocal
    {
        Random _rnd = new Random();

        public long BodyLength { get; set; }
        public byte[] Content { get; set; }
        public byte Type { get; set; }

        public WSProtocal(byte[] type, byte[] content)
        {
            var t = BitConverter.ToInt32(type, 0);
            this.Type = (byte)t;
            this.Content = content;
            if (content != null)
                this.BodyLength = content.Length;
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
        /// <returns></returns>
        public byte[] ToBytes()
        {
            int _payloadLength;
            byte[] _extPayloadLength;

            ulong len = (ulong)this.BodyLength;
            if (len < 126)
            {
                _payloadLength = (byte)len;
                _extPayloadLength = new byte[0];
            }
            else if (len < 0x010000)
            {
                _payloadLength = (byte)126;
                _extPayloadLength = ((ushort)len).InternalToByteArray(ByteOrder.Big);
            }
            else
            {
                _payloadLength = (byte)127;
                _extPayloadLength = len.InternalToByteArray(ByteOrder.Big);
            }

            using (var buff = new MemoryStream())
            {
                var header = (int)0x1;
                header = (header << 1) + (int)0x0;
                header = (header << 1) + (int)0x0;
                header = (header << 1) + (int)0x0;
                header = (header << 4) + (int)this.Type;
                header = (header << 1) + (int)0x0;
                header = (header << 7) + (int)_payloadLength;
                buff.Write(((ushort)header).InternalToByteArray(ByteOrder.Big), 0, 2);

                if (_payloadLength > 125)
                    buff.Write(_extPayloadLength, 0, _payloadLength == 126 ? 2 : 8);


                if (_payloadLength > 0)
                {
                    buff.Write(this.Content, 0, (int)this.BodyLength);
                }

                buff.Flush();
                return buff.ToArray();
            }
        }
    }
}
