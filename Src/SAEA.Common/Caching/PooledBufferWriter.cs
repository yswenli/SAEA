/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.Common.Caching
*文件名： PooledBufferWriter
*版本号： v26.11.5.1
*唯一标识：467886df-1c7f-4e90-93c9-7b5c1703dea8
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 12:40:36
*描述：基于内存池的缓冲区写入器
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:45
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
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

        /// <summary>
        /// 初始化写入器并从内存池租用不小于 initialCapacity 的缓冲区
        /// </summary>
        /// <param name="initialCapacity">初始容量（字节），小于等于 0 时按 4096 处理</param>
        public PooledBufferWriter(int initialCapacity = 4096)
        {
            if (initialCapacity <= 0) initialCapacity = 4096;
            _requestedSize = initialCapacity;
            _buffer = MemoryPoolManager.Rent(initialCapacity);
        }

        /// <summary>
        /// 已写入的字节数
        /// </summary>
        public int WrittenCount
        {
            get { ThrowIfDisposed(); return _written; }
        }

        /// <summary>
        /// 已写入数据的只读 Span，长度为 WrittenCount
        /// </summary>
        public ReadOnlySpan<byte> WrittenSpan
        {
            get { ThrowIfDisposed(); return _buffer.AsSpan(0, _written); }
        }

        /// <summary>
        /// 已写入数据的只读 Memory，长度为 WrittenCount
        /// </summary>
        public ReadOnlyMemory<byte> WrittenMemory
        {
            get { ThrowIfDisposed(); return _buffer.AsMemory(0, _written); }
        }

        /// <summary>
        /// 确认已向 GetMemory 或 GetSpan 返回的区域写入 count 字节，缓冲区不足或 count 为负时抛异常
        /// </summary>
        /// <param name="count">本次提交的字节数</param>
        public void Advance(int count)
        {
            ThrowIfDisposed();
            if (count < 0 || count > _buffer.Length - _written)
                throw new ArgumentOutOfRangeException(nameof(count));
            _written += count;
        }

        /// <summary>
        /// 获取从当前写入位置到缓冲区末尾的可写 Memory，容量不足时自动扩容
        /// </summary>
        /// <param name="sizeHint">期望的最小连续可写字节数，小于等于 0 时按 1 处理</param>
        /// <returns>从写入位置开始的可写区域，长度可能大于 sizeHint</returns>
        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            Ensure(sizeHint);
            return _buffer.AsMemory(_written);
        }

        /// <summary>
        /// 获取从当前写入位置到缓冲区末尾的可写 Span，容量不足时自动扩容
        /// </summary>
        /// <param name="sizeHint">期望的最小连续可写字节数，小于等于 0 时按 1 处理</param>
        /// <returns>从写入位置开始的可写区域，长度可能大于 sizeHint</returns>
        public Span<byte> GetSpan(int sizeHint = 0)
        {
            Ensure(sizeHint);
            return _buffer.AsSpan(_written);
        }

        /// <summary>
        /// 取出已写入数据的数组段，用于零拷贝直发，无需先提交写入
        /// </summary>
        /// <param name="segment">指向底层数组、范围从 0 到 WrittenCount</param>
        /// <returns>始终为 true</returns>
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

        /// <summary>
        /// 将缓冲区归还内存池并作废该写入器，重复调用无副作用
        /// </summary>
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