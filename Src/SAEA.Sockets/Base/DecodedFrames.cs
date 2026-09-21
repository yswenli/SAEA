using System;
using System.Buffers;
using System.Collections.Generic;
using SAEA.Common.Caching;
using SAEA.Sockets.Interface;

namespace SAEA.Sockets.Base
{
    /// <summary>
    /// 解码帧批次。Batch 模式由 <see cref="BaseCoder"/> 提供单块池化背衬；
    /// Borrowed 模式（如 WSCoder）由各帧自持内存。Dispose 归还背衬并释放实现 IDisposable 的帧。
    /// 无终结器：忘记 Dispose 将静默丢失池化缓冲。
    /// </summary>
    public sealed class DecodedFrames : IDisposable
    {
        private ISocketProtocal[] _frames;
        private int _count;
        private byte[] _pooledBuffer;
        private int _pooledRequestedSize;
        private bool _disposed;

        public DecodedFrames(int capacity = 4)
        {
            if (capacity <= 0) capacity = 4;
            _frames = new ISocketProtocal[capacity];
        }

        public int Count
        {
            get { ThrowIfDisposed(); return _count; }
        }

        public ISocketProtocal this[int index]
        {
            get { ThrowIfDisposed(); return _frames[index]; }
        }

        /// <summary>帧切片；仅在批次 Dispose 前有效。</summary>
        public ReadOnlySpan<ISocketProtocal> Frames
        {
            get { ThrowIfDisposed(); return _frames.AsSpan(0, _count); }
        }

        /// <summary>供非 BaseCoder 的 coder 组装帧。</summary>
        public void Add(ISocketProtocal frame)
        {
            ThrowIfDisposed();
            if (_count == _frames.Length)
            {
                var bigger = new ISocketProtocal[_frames.Length * 2];
                Array.Copy(_frames, bigger, _count);
                _frames = bigger;
            }
            _frames[_count++] = frame;
        }

        internal void SetPooledBuffer(byte[] buffer, int requestedSize)
        {
            _pooledBuffer = buffer;
            _pooledRequestedSize = requestedSize;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_pooledBuffer != null)
            {
                MemoryPoolManager.Return(_pooledBuffer, _pooledRequestedSize);
                _pooledBuffer = null;
            }
            for (var i = 0; i < _count; i++)
            {
                var d = _frames[i] as IDisposable;
                if (d != null) d.Dispose();
            }
            _count = 0;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DecodedFrames));
        }
    }

    internal enum FrameEventKind { Heart, File, Data }

    internal struct FrameEvent
    {
        public FrameEventKind Kind;
        public long BodyLength;
        public byte Type;
        public int Offset;
        public int Length;
        public DateTime HeartAt;
    }

    /// <summary>
    /// 驱动 per-instance FrameDecoder 时，把帧体复制进单块可增长池化缓冲；
    /// 回调延迟到缓冲定型后按原顺序触发，从而让 ReadOnlyMemory 在批次 Dispose 前稳定有效。
    /// </summary>
    internal sealed class FramesCollector : IFrameHandler
    {
        private byte[] _pooled;
        private int _pooledRequestedSize;
        private int _offset;
        private FrameEvent[] _events = new FrameEvent[4];
        private int _eventCount;
        private int _dataCount;

        public void OnFrame(in SocketFrame frame)
        {
            var off = Append(frame.Content);
            AddEvent(new FrameEvent
            {
                Kind = FrameEventKind.Data,
                BodyLength = frame.BodyLength,
                Type = frame.Type,
                Offset = off,
                Length = frame.Content.Length
            });
            _dataCount++;
        }

        public void AddHeart(DateTime at)
        {
            AddEvent(new FrameEvent { Kind = FrameEventKind.Heart, HeartAt = at });
        }

        public void AddFile(ReadOnlySpan<byte> content)
        {
            var off = Append(content);
            AddEvent(new FrameEvent { Kind = FrameEventKind.File, Offset = off, Length = content.Length });
        }

        public DecodedFrames Build(Action<DateTime> onHeart, Action<ReadOnlyMemory<byte>> onFile)
        {
            var frames = new DecodedFrames(_dataCount == 0 ? 1 : _dataCount);
            frames.SetPooledBuffer(_pooled, _pooledRequestedSize);

            var buffer = _pooled;
            for (var i = 0; i < _eventCount; i++)
            {
                var e = _events[i];
                switch (e.Kind)
                {
                    case FrameEventKind.Heart:
                        if (onHeart != null) onHeart(e.HeartAt);
                        break;
                    case FrameEventKind.File:
                        if (onFile != null)
                            onFile(e.Length == 0 ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte>(buffer, e.Offset, e.Length));
                        break;
                    case FrameEventKind.Data:
                        var content = e.Length == 0 ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte>(buffer, e.Offset, e.Length);
                        frames.Add(new BaseSocketProtocal(e.BodyLength, e.Type, content));
                        break;
                }
            }

            _pooled = null; // 所有权转移给 frames
            _offset = 0;
            _eventCount = 0;
            _dataCount = 0;
            return frames;
        }

        private int Append(ReadOnlySpan<byte> content)
        {
            if (content.Length == 0) return -1;
            Ensure(content.Length);
            var off = _offset;
            content.CopyTo(_pooled.AsSpan(off));
            _offset += content.Length;
            return off;
        }

        private void AddEvent(FrameEvent e)
        {
            if (_eventCount == _events.Length)
            {
                var bigger = new FrameEvent[_events.Length * 2];
                Array.Copy(_events, bigger, _eventCount);
                _events = bigger;
            }
            _events[_eventCount++] = e;
        }

        private void Ensure(int incoming)
        {
            if (_pooled == null)
            {
                _pooledRequestedSize = Math.Max(MemoryPoolManager.SmallThreshold, incoming);
                _pooled = MemoryPoolManager.Rent(_pooledRequestedSize);
                return;
            }
            if ((long)_offset + incoming <= _pooled.Length) return;

            long required = (long)_offset + incoming;
            if (required > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(incoming));

            var newSize = _pooled.Length;
            while (newSize < required)
            {
                if (newSize > int.MaxValue / 2) { newSize = (int)required; break; }
                newSize <<= 1;
            }

            // 归还旧缓冲必须用其“原始请求大小”，否则会被错误分级到更大的池（code-quality 审查发现）。
            var oldSize = _pooledRequestedSize;
            var bigger = MemoryPoolManager.Rent(newSize);
            Buffer.BlockCopy(_pooled, 0, bigger, 0, _offset);
            MemoryPoolManager.Return(_pooled, oldSize);
            _pooled = bigger;
            _pooledRequestedSize = newSize;
        }
    }
}