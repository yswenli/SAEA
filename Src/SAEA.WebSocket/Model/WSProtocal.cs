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

using SAEA.Common;
using SAEA.Common.Caching;
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

        /// <summary>
        /// 数据是否来自内存池
        /// </summary>
        public bool IsPooled { get; set; }

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
            if (content != null && content.Length > 0)
                this.BodyLength = content.Length;
            else
                this.BodyLength = 0;
            this.Content = content;
        }


        /// <summary>
        /// 将当前实体转换成websocket所需的结构
        /// 符合RFC 6455规范的帧格式编码
        /// </summary>
        /// <param name="masked">是否使用掩码</param>
        /// <returns>编码后的字节数组</returns>
        public byte[] ToBytes(bool masked)
        {
            ulong len = (ulong)this.BodyLength;

            var buff = new List<byte>();

            // RFC 6455 Frame Format:
            // Byte 1: FIN(1) | RSV1(1) | RSV2(1) | RSV3(1) | Opcode(4)
            // Byte 2: MASK(1) | Payload Length(7)
            
            // 字节1: FIN=1, RSV1-3=0, Opcode
            byte byte1 = (byte)(0x80 | this.Type);  // 0x80 = FIN位置1
            
            // 字节2: MASK位 + Payload Length
            byte byte2;
            byte[] extPayloadLength;

            if (len < 126)
            {
                byte2 = (byte)((masked ? 0x80 : 0x00) | (byte)len);
                extPayloadLength = new byte[0];
            }
            else if (len < 65536)
            {
                byte2 = (byte)((masked ? 0x80 : 0x00) | 126);
                extPayloadLength = ((ushort)len).InternalToByteArray(EndianOrder.Big);
            }
            else
            {
                byte2 = (byte)((masked ? 0x80 : 0x00) | 127);
                extPayloadLength = len.InternalToByteArray(EndianOrder.Big);
            }

            // 添加帧头（字节1和字节2）
            buff.Add(byte1);
            buff.Add(byte2);

            // 如果payload长度>=126，添加扩展长度字段
            if (len >= 126)
                buff.AddRange(extPayloadLength);

            byte[] maskBytes = null;

            // 如果使用掩码，添加4字节掩码
            if (masked)
            {
                maskBytes = _mask.ToBytes();
                buff.AddRange(maskBytes);
            }

            // 添加payload数据
            if (len > 0)
            {
                if (masked)
                {
                    WSCoder.DoMask(this.Content, 0, this.Content.Length, maskBytes);
                }

                buff.AddRange(this.Content);
            }
            return buff.ToArray();

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
            if (this.Content != null && this.Content.Any())
            {
                // 如果数据来自内存池，归还缓冲区
                if (this.IsPooled)
                {
                    MemoryPoolManager.Return(this.Content, this.Content.Length);
                    this.IsPooled = false;
                }
                else
                {
                    this.Content.Clear();
                }
                this.Content = null;
            }
        }
    }
}
