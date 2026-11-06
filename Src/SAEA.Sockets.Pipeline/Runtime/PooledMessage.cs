using System;
using System.Threading;
using SAEA.Common.Caching;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 队列条目持有的帧字节，配合引用计数管理背衬数组的归属。
    /// <para>
    /// 所有权约定：创建时引用计数为 1，表示链上的一份引用。入站 handler 把消息传给下游后
    /// 不得再使用该消息；需要跨 <c>await</c> 留存时必须先 <see cref="Retain"/>，用完再
    /// <see cref="Release"/> 那一份。每条消息的执行包在 <c>finally</c> 里无条件
    /// <see cref="Release"/> 一次收口，因此重复 <see cref="Release"/> 必须安全。
    /// </para>
    /// <para>
    /// 背衬分两种：<see cref="IsPooled"/> 为 <c>true</c> 时来自内存池，归零后归还；
    /// 为 <c>false</c> 时是精确分配的数组，归零后只丢引用，交给 GC。
    /// </para>
    /// </summary>
    public sealed class PooledMessage : IDisposable
    {
        private ReadOnlyMemory<byte> _frame;
        private byte[] _buffer;
        private readonly int _originalSize;
        private readonly bool _isPooled;
        private int _refCount;
        private volatile bool _released;

        /// <summary>
        /// 创建队列条目。
        /// </summary>
        /// <param name="frame">对外暴露的帧字节，可以是背衬数组的一段。</param>
        /// <param name="buffer">背衬数组；非池化背衬同样指向这里。</param>
        /// <param name="originalSize">向内存池借用的原始大小，用于确定归还到哪一级池。</param>
        /// <param name="isPooled">背衬是否来自内存池，决定归零时归还还是交给 GC。</param>
        internal PooledMessage(ReadOnlyMemory<byte> frame, byte[] buffer, int originalSize, bool isPooled)
        {
            _frame = frame;
            _buffer = buffer;
            _originalSize = originalSize;
            _isPooled = isPooled;
            _refCount = 1;
        }

        /// <summary>
        /// 队列条目拥有的帧字节。释放后访问抛 <see cref="ObjectDisposedException"/>，
        /// 不允许读到已归还内存。
        /// </summary>
        public ReadOnlyMemory<byte> Frame
        {
            get
            {
                if (_released)
                    throw new ObjectDisposedException(nameof(PooledMessage));
                return _frame;
            }
        }

        /// <summary>背衬是否来自内存池。为 <c>false</c> 表示精确分配的数组。</summary>
        public bool IsPooled
        {
            get { return _isPooled; }
        }

        /// <summary>引用计数是否已归零、背衬是否已交还。</summary>
        public bool IsReleased
        {
            get { return _released; }
        }

        /// <summary>
        /// 引用计数 +1，表示又一份引用（例如透传分支要把原始帧字节留到下游）。
        /// 对已释放实例抛 <see cref="ObjectDisposedException"/>。
        /// </summary>
        public void Retain()
        {
            if (_released)
                throw new ObjectDisposedException(nameof(PooledMessage));
            Interlocked.Increment(ref _refCount);
        }

        /// <summary>
        /// 引用计数 -1；归零时归还背衬并置 <see cref="IsReleased"/>。
        /// 对已归零实例是 no-op（幂等），不抛异常、不重复归还。
        /// </summary>
        public void Release()
        {
            if (Volatile.Read(ref _refCount) <= 0)
                return;
            if (Interlocked.Decrement(ref _refCount) != 0)
                return;
            if (_released)
                return;
            _released = true;

            var buffer = _buffer;
            _buffer = null;
            _frame = ReadOnlyMemory<byte>.Empty;

            if (_isPooled && buffer != null)
                MemoryPoolManager.Return(buffer, _originalSize);
        }

        /// <summary>等价于 <see cref="Release"/>。</summary>
        public void Dispose()
        {
            Release();
        }
    }
}