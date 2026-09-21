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
*修改时间：2018/03/18 02:16:04
*修改人： yswenli
*版本号： v26.4.23.1
*描述：WSProtocal类
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

        public long BodyLength { get; protected set; }
        public byte Type { get; protected set; }
        public ReadOnlyMemory<byte> Content { get; protected set; } = ReadOnlyMemory<byte>.Empty;
        public bool IsPooled { get; set; }

        public WSProtocal(byte type, byte[] content)
        {
            Type = type;
            _buffer = content;
            BodyLength = content == null ? 0 : content.Length;
            Content = content == null ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte>(content);
        }

        public WSProtocal(WSProtocalType type, byte[] content)
        {
            Type = (byte)type;
            _buffer = content;
            BodyLength = (content == null || content.Length == 0) ? 0 : content.Length;
            Content = (content == null || content.Length == 0) ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte>(content);
        }

        public void WriteTo(IBufferWriter<byte> writer) { WriteFrame(writer, false); }

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

        public void Dispose()
        {
            if (_buffer != null)
            {
                if (IsPooled)
                {
                    MemoryPoolManager.Return(_buffer, _buffer.Length);
                    IsPooled = false;
                }
                _buffer = null;
            }
            Content = ReadOnlyMemory<byte>.Empty;
        }
    }
}