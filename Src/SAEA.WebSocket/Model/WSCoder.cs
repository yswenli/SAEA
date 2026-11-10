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
*文件名： WSCoder
*版本号： v26.4.23.1
*唯一标识：ba3ea3ae-d654-4bd7-b04e-40f0aed57aa5
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：WSCoder编解码类
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 13:56:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：整理 using 顺序，移除未使用的引用
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Sockets.Interface;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;

namespace SAEA.WebSocket.Model
{
    /// <summary>
    /// 编码协议
    /// </summary>
    public class WSCoder : ICoder
    {
        private List<byte> _buffer;

        private List<byte> _fragments = new List<byte>();

        private byte _fragmentedOpcode;

        /// <summary>
        /// 单条消息（含分片重组后）允许的最大字节数，超过即按协议错误处理
        /// </summary>
        public int MaxMessageSize { get; set; } = 64 * 1024 * 1024;

        /// <summary>
        /// 是否要求收到的帧必须带掩码（服务端方向为 true，客户端方向为 false）
        /// </summary>
        public bool RequireMask { get; set; }

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
        /// <param name="writer">写入器</param>
        public void Encode(ISocketProtocal protocal, System.Buffers.IBufferWriter<byte> writer)
        {
            var ws = protocal as WSProtocal;
            if (ws != null) ws.WriteMaskedTo(writer);
            else protocal.WriteTo(writer);
        }

        /// <summary>
        /// 解码数据
        /// </summary>
        /// <param name="data">接收到的数据</param>
        /// <param name="onHeart">心跳回调</param>
        /// <param name="onFile">文件回调</param>
        /// <returns>本次解出的帧集合，其中的池化缓冲由调用方负责释放；协议错误时先释放已解出的帧再抛出异常</returns>
        public SAEA.Sockets.Base.DecodedFrames Decode(System.Buffers.ReadOnlySequence<byte> data,
            Action<DateTime> onHeart = null, Action<ReadOnlyMemory<byte>> onFile = null)
        {
            var frames = new SAEA.Sockets.Base.DecodedFrames();
            _buffer.AddRange(data.ToArray());
            try
            {
                while (TryDecodeFrame(frames)) { }
            }
            catch
            {
                // 协议错误：释放已解出的帧（含池化缓冲），并丢弃半成品分片。
                frames.Dispose();
                _fragmentedOpcode = 0;
                _fragments.Clear();
                throw;
            }
            return frames;
        }

        /// <summary>
        /// 尝试从缓冲区解出一帧；数据不完整时返回 false，协议错误时抛异常。
        /// </summary>
        bool TryDecodeFrame(SAEA.Sockets.Base.DecodedFrames frames)
        {
            if (_buffer.Count < 2) return false;

            var b0 = _buffer[0];
            var b1 = _buffer[1];

            if ((b0 & 0x70) != 0)
                throw new InvalidOperationException("WebSocket 帧出现未协商的 RSV 位");

            var fin = (b0 & 0x80) != 0;
            var opcode = (byte)(b0 & 0x0f);
            var masked = (b1 & 0x80) != 0;
            var len7 = b1 & 0x7f;

            if (opcode != 0 && opcode != 1 && opcode != 2 && opcode != 8 && opcode != 9 && opcode != 10)
                throw new InvalidOperationException($"WebSocket 不支持的 opcode: {opcode}");

            if (RequireMask && !masked)
                throw new InvalidOperationException("WebSocket 客户端帧必须带掩码");

            var extLen = len7 == 126 ? 2 : (len7 == 127 ? 8 : 0);
            var maskOffset = 2 + extLen;
            var headerLen = maskOffset + (masked ? 4 : 0);

            if (_buffer.Count < headerLen) return false;

            long payloadLen = len7;
            if (len7 == 126)
            {
                payloadLen = (long)((_buffer[2] << 8) | _buffer[3]);
            }
            else if (len7 == 127)
            {
                ulong l = 0;
                for (var i = 0; i < 8; i++) l = (l << 8) | _buffer[2 + i];
                if ((l & 0x8000000000000000UL) != 0)
                    throw new InvalidOperationException("WebSocket 帧长度非法");
                if (l > int.MaxValue)
                    throw new InvalidOperationException("WebSocket 帧长度超过上限");
                payloadLen = (long)l;
            }

            if (opcode >= 8)
            {
                if (!fin) throw new InvalidOperationException("WebSocket 控制帧不能分片");
                if (payloadLen > 125) throw new InvalidOperationException("WebSocket 控制帧长度超过 125");
            }

            if (payloadLen > MaxMessageSize)
                throw new InvalidOperationException("WebSocket 帧超过 MaxMessageSize");

            // 分片状态与消息总长的校验必须在分配 buffer 之前完成：
            // 若在 switch 里抛异常，当前 payload 可能是池化缓冲，就再也没人归还了。
            if (opcode == 0 && _fragmentedOpcode == 0)
                throw new InvalidOperationException("WebSocket 收到孤立的 Continuation 帧");

            if ((opcode == 1 || opcode == 2) && _fragmentedOpcode != 0)
                throw new InvalidOperationException("WebSocket 上一条分片消息尚未结束又收到新的数据帧");

            var appendsFragment = opcode == 0 || !fin;

            if (opcode < 8 && appendsFragment && _fragments.Count + payloadLen > MaxMessageSize)
                throw new InvalidOperationException("WebSocket 分片消息超过 MaxMessageSize");

            var total = headerLen + (int)payloadLen;
            if (_buffer.Count < total) return false;

            var len = (int)payloadLen;
            byte[] payload;
            var pooled = false;
            if (len == 0)
            {
                payload = Array.Empty<byte>();
            }
            else if (len > MemoryPoolManager.SmallThreshold)
            {
                payload = MemoryPoolManager.Rent(len);
                pooled = true;
            }
            else
            {
                payload = new byte[len];
            }

            if (len > 0)
            {
                if (masked)
                {
                    unsafe
                    {
                        byte* masks = stackalloc byte[4];
                        for (var i = 0; i < 4; i++) masks[i] = _buffer[maskOffset + i];
                        for (var i = 0; i < len; i++)
                            payload[i] = (byte)(_buffer[headerLen + i] ^ masks[i % 4]);
                    }
                }
                else
                {
                    for (var i = 0; i < len; i++) payload[i] = _buffer[headerLen + i];
                }
            }

            _buffer.RemoveRange(0, total);

            switch (opcode)
            {
                case 0:
                    if (_fragmentedOpcode == 0)
                        throw new InvalidOperationException("WebSocket 收到孤立的 Continuation 帧");
                    AppendFragment(payload, len);
                    if (pooled) MemoryPoolManager.Return(payload, len);
                    if (fin)
                    {
                        var body = _fragments.ToArray();
                        var messageOpcode = _fragmentedOpcode;
                        _fragmentedOpcode = 0;
                        _fragments.Clear();
                        frames.Add(new WSProtocal(messageOpcode, body));
                    }
                    break;

                case 1:
                case 2:
                    if (_fragmentedOpcode != 0)
                        throw new InvalidOperationException("WebSocket 上一条分片消息尚未结束又收到新的数据帧");
                    if (fin)
                    {
                        frames.Add(new WSProtocal(opcode, payload, len) { IsPooled = pooled });
                    }
                    else
                    {
                        _fragmentedOpcode = opcode;
                        _fragments.Clear();
                        AppendFragment(payload, len);
                        if (pooled) MemoryPoolManager.Return(payload, len);
                    }
                    break;

                default:
                    frames.Add(new WSProtocal(opcode, payload, len) { IsPooled = pooled });
                    break;
            }

            return true;
        }

        void AppendFragment(byte[] payload, int length)
        {
            if (_fragments.Count + length > MaxMessageSize)
                throw new InvalidOperationException("WebSocket 分片消息超过 MaxMessageSize");
            for (var i = 0; i < length; i++) _fragments.Add(payload[i]);
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
        /// 对数据进行掩码处理（使用stackalloc的unsafe版本）
        /// </summary>
        /// <param name="buffer">数据缓冲区</param>
        /// <param name="offset">偏移量</param>
        /// <param name="length">数据长度</param>
        /// <param name="masks">掩码指针</param>
        unsafe public static void DoMaskUnsafe(byte[] buffer, int offset, int length, byte* masks)
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
            _fragments.Clear();
            _fragmentedOpcode = 0;
        }
    }
}
