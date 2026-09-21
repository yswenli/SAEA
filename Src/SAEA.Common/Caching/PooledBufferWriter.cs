using System;
using System.Buffers;

namespace SAEA.Common.Caching
{
    /// <summary>
    /// 池化写入器：数组背衬、可归还、可零拷贝直发（TryGetArray 供 SocketAsyncEventArgs.SetBuffer）。
    /// 非线程安全。注意：GetSpan/GetMemory 取得的 span 在 Dispose 后失效，Dispose 前不得让其外泄。
    /// </summary>
    public sealed class PooledBufferWriter : IBufferWriter<byte>, IDisposable
    {
        private byte[] _buffer;
        private int _requestedSize;
        private int _written;
        private bool _disposed;

        public PooledBufferWriter(int initialCapacity = 4096)
        {
            if (initialCapacity <= 0) initialCapacity = 4096;
            _requestedSize = initialCapacity;
            _buffer = MemoryPoolManager.Rent(initialCapacity);
        }

        public int WrittenCount
        {
            get { ThrowIfDisposed(); return _written; }
        }

        public ReadOnlySpan<byte> WrittenSpan
        {
            get { ThrowIfDisposed(); return _buffer.AsSpan(0, _written); }
        }

        public ReadOnlyMemory<byte> WrittenMemory
        {
            get { ThrowIfDisposed(); return _buffer.AsMemory(0, _written); }
        }

        public void Advance(int count)
        {
            ThrowIfDisposed();
            if (count < 0 || count > _buffer.Length - _written)
                throw new ArgumentOutOfRangeException(nameof(count));
            _written += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            Ensure(sizeHint);
            return _buffer.AsMemory(_written);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            Ensure(sizeHint);
            return _buffer.AsSpan(_written);
        }

        public bool TryGetArray(out ArraySegment<byte> segment)
        {
            ThrowIfDisposed();
            segment = new ArraySegment<byte>(_buffer, 0, _written);
            return true;
        }

        /// <summary>复位写入位置，不归还缓冲。</summary>
        public void Clear()
        {
            ThrowIfDisposed();
            _written = 0;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_buffer != null)
            {
                MemoryPoolManager.Return(_buffer, _requestedSize);
                _buffer = null;
            }
            _written = 0;
        }

        private void Ensure(int sizeHint)
        {
            ThrowIfDisposed();
            if (sizeHint <= 0) sizeHint = 1;

            long required = (long)_written + sizeHint;
            if (required <= _buffer.Length) return;
            if (required > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(sizeHint));

            var newSize = _buffer.Length;
            while (newSize < required)
            {
                if (newSize > int.MaxValue / 2) { newSize = (int)required; break; }
                newSize <<= 1;
            }

            var bigger = MemoryPoolManager.Rent(newSize);
            Buffer.BlockCopy(_buffer, 0, bigger, 0, _written);
            MemoryPoolManager.Return(_buffer, _requestedSize);
            _buffer = bigger;
            _requestedSize = newSize;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PooledBufferWriter));
        }
    }
}