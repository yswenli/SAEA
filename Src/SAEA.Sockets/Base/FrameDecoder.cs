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
 *命名空间：SAEA.Sockets.Base
 *文件名： FrameDecoder
 *版本号： v26.4.23.1
 *唯一标识：c8d5e2f3-0a4b-4d7e-9f32-1b6c7d8e9f0a
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/09/20 17:07:21
 *描述：FrameDecoder增量式池化拆帧器
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/09/20 17:07:21
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：FrameDecoder增量式池化拆帧器
 *
 *****************************************************************************/
using System;
using System.Buffers;
using SAEA.Common;
using SAEA.Sockets.Model;

namespace SAEA.Sockets.Base
{
    internal enum FrameKind
    {
        Data,
        Heart,
        File
    }

    /// <summary>
    /// 增量式拆帧器：池化累加器 + head/tail 索引。非线程安全，按会话单线程使用。
    /// </summary>
    internal sealed class FrameDecoder
    {
        private byte[] _buffer;
        private int _start;
        private int _end;
        private readonly int _maxFrameLength;

        public FrameDecoder(int maxFrameLength)
        {
            _buffer = ArrayPool<byte>.Shared.Rent(1024);
            _maxFrameLength = maxFrameLength < BaseCoder.P_Head ? int.MaxValue - BaseCoder.P_Head : maxFrameLength;
        }

        public int BufferedLength => _end - _start;

        public void Append(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) return;

            // Clear() 会归还池化缓冲并把 _buffer 置空；再次复用同一 coder（UserToken 池化场景）时按需重新租用。
            if (_buffer == null)
                _buffer = ArrayPool<byte>.Shared.Rent(Math.Max(1024, data.Length));

            EnsureCapacity(data.Length);
            data.CopyTo(_buffer.AsSpan(_end));
            _end += data.Length;
        }

        public bool TryReadFrame(out FrameKind kind, out SocketFrame frame, out ReadOnlySpan<byte> fileContent, out DateTime heartAt)
        {
            kind = FrameKind.Data;
            frame = default;
            fileContent = default;
            heartAt = default;

            if (_end - _start < BaseCoder.P_Head) return false;

            var bodyLen = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(_buffer.AsSpan(_start, BaseCoder.P_LEN));
            var type = _buffer[_start + BaseCoder.P_LEN];

            if (bodyLen < 0 || bodyLen > _maxFrameLength)
                throw new KernelException($"非法的数据帧长度: {bodyLen}");

            if (bodyLen == 0 && type == (byte)SocketProtocalType.Heart)
            {
                Consume(BaseCoder.P_Head);
                heartAt = DateTimeHelper.Now;
                kind = FrameKind.Heart;
                return true;
            }

            var total = BaseCoder.P_Head + (int)bodyLen;
            if (_end - _start < total) return false;

            if (type == (byte)SocketProtocalType.BigData)
            {
                fileContent = _buffer.AsSpan(_start + BaseCoder.P_Head, (int)bodyLen);
                Consume(total);
                kind = FrameKind.File;
                return true;
            }

            frame = new SocketFrame(bodyLen, type, _buffer.AsSpan(_start + BaseCoder.P_Head, (int)bodyLen));
            Consume(total);
            kind = FrameKind.Data;
            return true;
        }

        private void Consume(int count)
        {
            _start += count;
            if (_start == _end)
            {
                _start = 0;
                _end = 0;
            }
        }

        private void EnsureCapacity(int incoming)
        {
            if (_start > 0 && _buffer.Length - _end < incoming)
            {
                var len = _end - _start;
                Buffer.BlockCopy(_buffer, _start, _buffer, 0, len);
                _start = 0;
                _end = len;
            }

            long required = (long)_end + incoming;
            if (required <= _buffer.Length) return;
            if (required > int.MaxValue)
                throw new KernelException($"数据帧过大: {required}");

            var newSize = _buffer.Length;
            while (newSize < required)
            {
                if (newSize > int.MaxValue / 2) { newSize = (int)required; break; }
                newSize <<= 1;
            }

            var bigger = ArrayPool<byte>.Shared.Rent(newSize);
            Buffer.BlockCopy(_buffer, _start, bigger, 0, _end - _start);
            _end -= _start;
            _start = 0;
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = bigger;
        }

        public void Clear()
        {
            if (_buffer != null)
            {
                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = null;
            }
            _start = 0;
            _end = 0;
        }
    }
}