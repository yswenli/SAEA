using System;
using SAEA.Common.Caching;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 增量分帧累加器。非线程安全，只能由泵协程独占使用。
    /// <para>
    /// 背衬数组来自内存池，按需扩容：先尝试把 <c>_start</c> 之后的残余字节原地搬回下标 0，
    /// 仍装不下再按两倍增长租借更大的缓冲，并归还旧缓冲。累加器不设物理容量上限，
    /// 未成帧残余的业务上限由调用方在每轮排空后检查。
    /// </para>
    /// </summary>
    public sealed class FrameAccumulator : IDisposable
    {
        private const int DefaultCapacity = 1024;

        private byte[] _buffer;
        private int _bufferSize;
        private int _start;
        private int _end;

        /// <summary>以 1024 字节初始容量创建累加器。</summary>
        public FrameAccumulator()
        {
            _buffer = MemoryPoolManager.Rent(DefaultCapacity);
            _bufferSize = DefaultCapacity;
        }

        /// <summary>当前尚未消费的字节数。</summary>
        public int BufferedLength
        {
            get { return _end - _start; }
        }

        /// <summary>当前尚未消费字节的只读切片；没有可读数据时返回空。</summary>
        public ReadOnlyMemory<byte> Buffered
        {
            get
            {
                var length = _end - _start;
                if (length == 0)
                    return ReadOnlyMemory<byte>.Empty;
                return new ReadOnlyMemory<byte>(_buffer, _start, length);
            }
        }

        /// <summary>把一段字节追加到未消费字节之后。</summary>
        public void Append(ReadOnlyMemory<byte> data)
        {
            if (data.Length == 0)
                return;
            EnsureCapacity(data.Length);
            data.Span.CopyTo(_buffer.AsSpan(_end));
            _end += data.Length;
        }

        /// <summary>消费未消费字节中的前 <paramref name="count"/> 字节；<paramref name="count"/> 必须在 [0, BufferedLength]。</summary>
        public void Consume(int count)
        {
            if (count < 0 || count > _end - _start)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (count == 0)
                return;

            _start += count;
            if (_start == _end)
            {
                _start = 0;
                _end = 0;
            }
        }

        private void EnsureCapacity(int extra)
        {
            if (_buffer == null)
            {
                // Reset() 归还缓冲后同一实例仍可继续使用，这里按需重新租借。
                var size = DefaultCapacity;
                while (size < extra)
                {
                    size = checked(size * 2);
                }
                _buffer = MemoryPoolManager.Rent(size);
                _bufferSize = size;
                return;
            }

            var required = checked(_end + extra);
            if (required <= _buffer.Length)
                return;

            // 先尝试原地紧凑，避免无谓扩容。
            if (_start > 0)
            {
                var length = _end - _start;
                Buffer.BlockCopy(_buffer, _start, _buffer, 0, length);
                _end = length;
                _start = 0;
                if (checked(_end + extra) <= _buffer.Length)
                    return;
            }

            var newSize = _buffer.Length;
            while (newSize < _end + extra)
            {
                newSize = checked(newSize * 2);
            }

            var next = MemoryPoolManager.Rent(newSize);
            Buffer.BlockCopy(_buffer, 0, next, 0, _end);
            MemoryPoolManager.Return(_buffer, _bufferSize);
            _buffer = next;
            _bufferSize = newSize;
        }

        /// <summary>
        /// 清空内部状态并归还池化缓冲。连接断开或握手失败时由泵在 finally 中调用；可重复调用。
        /// </summary>
        public void Reset()
        {
            var buffer = _buffer;
            if (buffer != null)
            {
                var size = _bufferSize;
                _buffer = null;
                _bufferSize = 0;
                MemoryPoolManager.Return(buffer, size);
            }

            _start = 0;
            _end = 0;
        }

        /// <summary>等价于 <see cref="Reset"/>；可重复调用。</summary>
        public void Dispose()
        {
            Reset();
        }
    }
}