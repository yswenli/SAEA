/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.WebSocket.Model
*文件名： WSProtocal
*版本号： v26.4.23.1
*唯一标识：fba0be19-48e3-4299-a333-7670501a13ef
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：WSProtocal类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:30
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Buffers;
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

        private byte[] _buffer;

        /// <summary>
        /// 报文体字节长度
        /// </summary>
        public long BodyLength { get; protected set; }
        /// <summary>
        /// 帧操作码（opcode）
        /// </summary>
        public byte Type { get; protected set; }
        /// <summary>
        /// 报文体只读视图，无内容或已释放时为空
        /// </summary>
        public ReadOnlyMemory<byte> Content { get; protected set; } = ReadOnlyMemory<byte>.Empty;
        /// <summary>
        /// 缓冲区是否从内存池租用，为 true 时 Dispose 需按 BodyLength 归还
        /// </summary>
        public bool IsPooled { get; set; }

        /// <summary>
        /// 以操作码与报文体创建协议帧
        /// </summary>
        /// <param name="type">帧操作码</param>
        /// <param name="content">报文体，null 视为空</param>
        public WSProtocal(byte type, byte[] content)
        {
            Type = type;
            _buffer = content;
            BodyLength = content == null ? 0 : content.Length;
            Content = content == null ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte>(content);
        }

        /// <summary>
        /// 以帧类型枚举与报文体创建协议帧
        /// </summary>
        /// <param name="type">帧类型枚举</param>
        /// <param name="content">报文体，null 或空数组视为无内容</param>
        public WSProtocal(WSProtocalType type, byte[] content)
        {
            Type = (byte)type;
            _buffer = content;
            BodyLength = (content == null || content.Length == 0) ? 0 : content.Length;
            Content = (content == null || content.Length == 0) ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte>(content);
        }

        /// <summary>
        /// 从可能大于实际内容的池化缓冲区创建协议对象
        /// </summary>
        /// <param name="type">帧操作码（opcode）</param>
        /// <param name="content">缓冲区（内容位于 [0, bodyLength)）</param>
        /// <param name="bodyLength">实际内容长度，非正数时按空内容处理</param>
        public WSProtocal(byte type, byte[] content, int bodyLength)
        {
            Type = type;
            _buffer = content;
            BodyLength = bodyLength <= 0 ? 0 : bodyLength;
            Content = (content == null || bodyLength <= 0) ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte>(content, 0, bodyLength);
        }

        /// <summary>
        /// 把帧写入缓冲区，不做掩码，供服务端发送
        /// </summary>
        /// <param name="writer">目标缓冲区写入器</param>
        public void WriteTo(IBufferWriter<byte> writer) { WriteFrame(writer, false); }

        /// <summary>
        /// 把帧掩码后写入缓冲区，供客户端发送
        /// </summary>
        /// <param name="writer">目标缓冲区写入器</param>
        public void WriteMaskedTo(IBufferWriter<byte> writer) { WriteFrame(writer, true); }

        private void WriteFrame(IBufferWriter<byte> writer, bool masked)
        {
            ulong len = (ulong)BodyLength;
            byte byte1 = (byte)(0x80 | Type);
            byte[] maskBytes = null;

            if (len < 126)
            {
                var s = writer.GetSpan(2);
                s[0] = byte1; s[1] = (byte)((masked ? 0x80 : 0) | (byte)len);
                writer.Advance(2);
            }
            else if (len < 65536)
            {
                var s = writer.GetSpan(4);
                s[0] = byte1; s[1] = (byte)((masked ? 0x80 : 0) | 126);
                s[2] = (byte)((ushort)len >> 8); s[3] = (byte)(ushort)len;
                writer.Advance(4);
            }
            else
            {
                var s = writer.GetSpan(10);
                s[0] = byte1; s[1] = (byte)((masked ? 0x80 : 0) | 127);
                var l = len;
                for (int i = 0; i < 8; i++) s[2 + i] = (byte)(l >> (56 - 8 * i));
                writer.Advance(10);
            }

            if (masked)
            {
                maskBytes = _mask.ToBytes();
                var m = writer.GetSpan(4);
                for (int i = 0; i < 4; i++) m[i] = maskBytes[i];
                writer.Advance(4);
            }

            if (len > 0)
            {
                var payload = writer.GetSpan((int)len);
                Content.Span.CopyTo(payload);
                if (masked)
                    for (int i = 0; i < (int)len; i++) payload[i] = (byte)(payload[i] ^ maskBytes[i % 4]);
                writer.Advance((int)len);
            }
        }

        /// <summary>
        /// 释放缓冲区并清空内容，池化缓冲按 BodyLength 归还内存池
        /// </summary>
        public void Dispose()
        {
            if (_buffer != null)
            {
                if (IsPooled)
                {
                    // 必须按租用时的原始长度归还，_buffer.Length 可能已被池放大到更大的分级
                    MemoryPoolManager.Return(_buffer, (int)BodyLength);
                    IsPooled = false;
                }
                _buffer = null;
            }
            Content = ReadOnlyMemory<byte>.Empty;
        }
    }
}