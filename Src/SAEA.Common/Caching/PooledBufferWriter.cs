using System;
using System.Buffers;

namespace SAEA.Common.Caching
{
    /// <summary>
    /// 池化写入器：数组背衬、可归还、可零拷贝直发（TryGetArray 供 SocketAsyncEventArgs.SetBuffer）。
    /// 非线程安全。
    /// </summary>
    public sealed class PooledBufferWriter : IBufferWriter<byte>, IDisposable
    {
        private byte[] _buffer;
        private int _written;
        private bool _disposed;

        public PooledBufferWriter(int initialCapacity = 4096)
        {
            if (initialCapacity <= 0) initialCapacity = 4096;
            _buffer = MemoryPoolManager.Rent(initialCapacity);
        }

        public int WrittenCount { get { return _written; } }

        public ReadOnlySpan<byte> WrittenSpan
        {
            get
            {
                if (_disposed) throw new ObjectDisposedException(nameof(PooledBufferWriter));
                return _buffer.AsSpan(0, _written);
            }
        }

        public ReadOnlyMemory<byte> WrittenMemory
        {
            get
            {
                if (_disposed) throw new ObjectDisposedException(nameof(PooledBufferWriter));
                return _buffer.AsMemory(0, _written);
            }
        }

        public void Advance(int count)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PooledBufferWriter));
            if (count < 0 || _written + count > _buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));
            _written += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            Ensure(sizeHint <= 0 ? 1 : sizeHint);
            return _buffer.AsMemory(_written);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            Ensure(sizeHint <= 0 ? 1 : sizeHint);
            return _buffer.AsSpan(_written);
        }

        public bool TryGetArray(out ArraySegment<byte> segment)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PooledBufferWriter));
            segment = new ArraySegment<byte>(_buffer, 0, _written);
            return true;
        }

        /// <summary>复位写入位置，不归还缓冲。</summary>
        public void Clear()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PooledBufferWriter));
            _written = 0;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_buffer != null)
            {
                MemoryPoolManager.Return(_buffer, _buffer.Length);
                _buffer = null;
            }
            _written = 0;
        }

        private void Ensure(int sizeHint)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PooledBufferWriter));
            var required = _written + sizeHint;
            if (required <= _buffer.Length) return;

            var newSize = _buffer.Length;
            while (newSize < required) newSize <<= 1;

            var bigger = MemoryPoolManager.Rent(newSize);
            Buffer.BlockCopy(_buffer, 0, bigger, 0, _written);
            MemoryPoolManager.Return(_buffer, _buffer.Length);
            _buffer = bigger;
        }
    }
}